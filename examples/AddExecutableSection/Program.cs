using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using ElfTools;
using ElfTools.Chunks;
using ElfTools.Enums;
using ElfTools.Instrumentation;
using Iced.Intel;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using static Iced.Intel.AssemblerRegisters;

namespace AddExecutableSection
{
    class Program
    {
        static void Main(string[] args)
        {
            var elf = ElfReader.Load("/bin/ls");

            var elfBuilder = new ElfBuilder(elf, (chunks, filename) => ChunksToImage(chunks, $"/tmp/{filename}"));

            // For simplicity, we assume that the last chunk is the section table
            if(elfBuilder.Chunks.Last() is not SectionHeaderTableChunk)
                throw new InvalidOperationException("Section header table is not the last chunk");

            // Allocate extra space in program header, section string and section header tables
            const string newSectionName = ".instrument";
            elfBuilder.AllocateFileMemory((int)elfBuilder.Header.ProgramHeaderTableFileOffset + elfBuilder.ProgramHeaderTable.ByteLength, 1 * elfBuilder.ProgramHeaderTable.EntrySize); // Program header
            var stringTableSectionHeader = elfBuilder.SectionHeaderTable.SectionHeaders[elfBuilder.Header.SectionHeaderStringTableIndex];
            elfBuilder.AllocateFileMemory((int)stringTableSectionHeader.FileOffset + (int)stringTableSectionHeader.Size, newSectionName.Length + 1); // String table
            elfBuilder.AllocateFileMemory((int)elfBuilder.Header.SectionHeaderTableFileOffset + elfBuilder.SectionHeaderTable.ByteLength, elfBuilder.SectionHeaderTable.EntrySize); // Section header table

            // Allocate new section
            const int newSectionLength = 1024;
            const int newSectionAlignment = 0x1000;
            const int newSectionVirtualAddress = 0x30000;
            int totalFileLength = elfBuilder.Chunks.Sum(c => c.ByteLength);
            int newSectionOffset = ((totalFileLength & (newSectionAlignment - 1)) != 0) ? totalFileLength + newSectionAlignment - (totalFileLength & (newSectionAlignment - 1)) : totalFileLength;
            elfBuilder.AllocateFileMemory(totalFileLength, (newSectionOffset - totalFileLength) + newSectionLength);

            // Add section name to string table
            int newSectionNameStringTableIndex = elfBuilder.ExtendStringTable(elfBuilder.Header.SectionHeaderStringTableIndex, newSectionName)[0];

            // Add new section
            elfBuilder.CreateSection(new SectionHeaderTableChunk.SectionHeaderTableEntry
            {
                Alignment = newSectionAlignment,
                Flags = SectionFlags.Alloc | SectionFlags.Executable,
                Info = 0,
                Link = 0,
                Size = newSectionLength,
                Type = SectionType.ProgBits,
                EntrySize = 0,
                FileOffset = (ulong)newSectionOffset,
                VirtualAddress = newSectionVirtualAddress,
                NameStringTableOffset = (uint)newSectionNameStringTableIndex
            });

            // Add new executable segment
            elfBuilder.ExtendProgramHeaderTable(new ProgramHeaderTableChunk.ProgramHeaderTableEntry
            {
                Alignment = newSectionAlignment,
                Flags = SegmentFlags.Readable | SegmentFlags.Executable,
                Type = SegmentType.Load,
                FileOffset = (ulong)newSectionOffset,
                FileSize = newSectionLength,
                MemorySize = newSectionLength,
                PhysicalMemoryAddress = newSectionVirtualAddress,
                VirtualMemoryAddress = newSectionVirtualAddress
            });


            /* Create contents of new section */

            var assembler = new Assembler(64);

            // Output something
            var formatStringLabel = assembler.CreateLabel("format_string");
            
            assembler.push(rdi);
            assembler.push(rsi);
            assembler.sub(rsp, 0x8); // For stack alignment
            
            assembler.lea(rsi, __[formatStringLabel]);
            assembler.mov(edi, 1);
            assembler.xor(eax, eax);
            assembler.call(0x4c10); // __printf_chk@plt
            
            assembler.add(rsp, 0x8);
            assembler.pop(rsi);
            assembler.pop(rdi);

            // Replaced instructions
            assembler.push(r15);
            assembler.push(r14);
            assembler.push(r13);
            assembler.jmp(0x4dfa);

            // Format string for output
            assembler.Label(ref formatStringLabel);
            assembler.db(Encoding.ASCII.GetBytes("Hello World!\n\0"));

            using var newSectionContentStream = new MemoryStream();
            assembler.Assemble(new StreamCodeWriter(newSectionContentStream), newSectionVirtualAddress);
            while(newSectionContentStream.Length < newSectionLength)
                newSectionContentStream.WriteByte(0xCC);
            var newSectionContent = newSectionContentStream.ToArray();

            // Store new section data
            int newSectionChunkIndex = elfBuilder.GetChunkIndexForOffset((ulong)newSectionOffset)!.Value.chunkIndex;
            elfBuilder.Chunks[newSectionChunkIndex] = new RawSectionChunk { Data = newSectionContent.ToImmutableArray() };

            // Patch old code section
            elfBuilder.PatchRawBytesInSegment(0x4df4, new byte[] { 0xe9, 0x07, 0xb2, 0x02, 0x00, 0x90 }); // Instrumentation at the very beginning of main()

            ElfWriter.Store(elfBuilder.ToElfFile(), "/tmp/ls-instrumented");
        }

        static void ElfToImage(ElfFile elf, string path)
        {
            ChunksToImage(elf.Chunks, path);
        }

        private static Color[] _lastMap = null;

        static void ChunksToImage(IList<Chunk> chunks, string path)
        {
            //return;

            const int blockSize = 1;
            const int blocksPerRow = 128;
            const int blockPixelSize = 4;

            int totalBytes = chunks.Sum(c => c.ByteLength);

            Color[] map = new Color[totalBytes / blockSize + 1];
            int offset = 0;
            foreach(var chunk in chunks)
            {
                int chunkEnd = offset + chunk.ByteLength;
                while(offset < chunkEnd)
                {
                    map[offset / blockSize] = chunk switch
                    {
                        DynamicTableChunk => Color.Red,
                        HeaderChunk => Color.Yellow,
                        ProgramHeaderTableChunk => Color.Orange,
                        SectionHeaderTableChunk => Color.Green,
                        RawSectionChunk => Color.Black,
                        RelocationAddendTableChunk => Color.Blue,
                        RelocationTableChunk => Color.LightBlue,
                        StringTableChunk => Color.Brown,
                        SymbolTableChunk => Color.Pink,
                        NotesChunk => Color.Gray,
                        DummyChunk => Color.White,
                        _ => Color.Transparent
                    };

                    ++offset;
                }
            }

            // Do not print the same image again
            if(_lastMap != null && _lastMap.SequenceEqual(map))
                return;
            _lastMap = map;

            int rows = totalBytes / blocksPerRow + 1;

            var img = new Image<Rgba32>(blockPixelSize * blocksPerRow, blockPixelSize * rows);
            for(int i = 0; i < map.Length;)
            {
                int rowIndex = i / blocksPerRow;
                for(int k = 0; k < blockPixelSize; ++k)
                {
                    var rowSpan = img.GetPixelRowSpan(blockPixelSize * rowIndex + k);
                    for(int j = 0; j < blocksPerRow; ++j)
                    {
                        if(i + j < map.Length)
                        {
                            for(int l = 0; l < blockPixelSize; ++l)
                                rowSpan[blockPixelSize * j + l] = map[i + j].ToPixel<Rgba32>();
                        }
                    }
                }

                i += blocksPerRow;
            }

            img.Save(path, new PngEncoder());
        }

        static void ElfSegmentsToImage(ElfFile elf, string path)
        {
            const int blockSize = 1;
            int totalBytes = elf.GetByteLength();

            Rgb24[] map = new Rgb24[totalBytes / blockSize + 1];
            for(int i = 0; i < map.Length; ++i)
                map[i] = new Rgb24(255, 255, 255);
            foreach(var header in elf.ProgramHeaderTable.ProgramHeaders)
            {
                for(int i = (int)header.FileOffset; i < (int)header.FileOffset + (int)header.FileSize; ++i)
                {
                    if(i > map.Length)
                        continue;

                    var oldColor = map[i];
                    map[i] = new Rgb24((byte)(oldColor.R - 60), (byte)(oldColor.B - 60), (byte)(oldColor.G - 60));
                }
            }

            const int blocksPerRow = 128;

            int rows = totalBytes / blocksPerRow + 1;

            var img = new Image<Rgb24>(blocksPerRow, rows);
            for(int i = 0; i < map.Length;)
            {
                int rowIndex = i / blocksPerRow;
                var rowSpan = img.GetPixelRowSpan(rowIndex);
                for(int j = 0; j < blocksPerRow; ++j, ++i)
                {
                    if(i < map.Length)
                        rowSpan[j] = map[i];
                }
            }

            img.Save(path, new PngEncoder());
        }
    }
}