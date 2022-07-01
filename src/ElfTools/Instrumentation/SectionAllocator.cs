using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using ElfTools.Chunks;
using ElfTools.Enums;

namespace ElfTools.Instrumentation
{
    /// <summary>
    /// Provides extension functions for adding new sections to an existing ELF file.
    /// </summary>
    public static class SectionAllocator
    {
        /// <summary>
        /// Allocates a new PROGBITS section/LOAD segment at the given address and with the given contents.
        /// </summary>
        /// <param name="elf">ELF file.</param>
        /// <param name="name">Section name.</param>
        /// <param name="address">Segment virtual/physical address.</param>
        /// <param name="size">Size of the section/segment (may be larger than the actual contents)</param>
        /// <param name="alignment">Section/segment alignment.</param>
        /// <param name="isWritable">Determines whether the segment is writable.</param>
        /// <param name="isExecutable">Determines whether the segment is executable.</param>
        /// <param name="contents">Data which should be copied to the section begin.</param>
        /// <returns>The index of the newly created section.</returns>
        public static int AllocateProgBitsSection(this ElfFile elf, string name, ulong address, int size, int alignment, bool isWritable, bool isExecutable, byte[] contents)
        {
            // Allocate extra space in program header, section string and section header tables
            elf.AllocateFileMemory((int)elf.Header.ProgramHeaderTableFileOffset + elf.ProgramHeaderTable!.ByteLength, 1 * elf.ProgramHeaderTable.EntrySize); // Program header
            var stringTableSectionHeader = elf.SectionHeaderTable.SectionHeaders[elf.Header.SectionHeaderStringTableIndex];
            elf.AllocateFileMemory((int)stringTableSectionHeader.FileOffset + (int)stringTableSectionHeader.Size, name.Length + 1); // String table
            elf.AllocateFileMemory((int)elf.Header.SectionHeaderTableFileOffset + elf.SectionHeaderTable.ByteLength, elf.SectionHeaderTable.EntrySize); // Section header table

            // Allocate new section
            int totalFileLength = elf.Chunks.Sum(c => c.ByteLength);
            int newSectionOffset = ((totalFileLength & (alignment - 1)) != 0) ? totalFileLength + alignment - (totalFileLength & (alignment - 1)) : totalFileLength;
            elf.AllocateFileMemory(totalFileLength, (newSectionOffset - totalFileLength) + size);

            // Add section name to string table
            int newSectionNameStringTableIndex = elf.ExtendStringTable(elf.Header.SectionHeaderStringTableIndex, new[] { name }, null)[0];

            // Add new section
            int sectionIndex = elf.CreateSection(new SectionHeaderTableChunk.SectionHeaderTableEntry
            {
                Alignment = (ulong)alignment,
                Flags = SectionFlags.Alloc | (isWritable ? SectionFlags.Writable : SectionFlags.None) | (isExecutable ? SectionFlags.Executable : SectionFlags.None),
                Info = 0,
                Link = 0,
                Size = (ulong)size,
                Type = SectionType.ProgBits,
                EntrySize = 0,
                FileOffset = (ulong)newSectionOffset,
                VirtualAddress = address,
                NameStringTableOffset = (uint)newSectionNameStringTableIndex
            }, null);

            // Add new executable segment
            elf.ExtendProgramHeaderTable(new ProgramHeaderTableChunk.ProgramHeaderTableEntry
            {
                Alignment = (ulong)alignment,
                Flags = SegmentFlags.Readable | (isWritable ? SegmentFlags.Writable : SegmentFlags.None) | (isExecutable ? SegmentFlags.Executable : SegmentFlags.None),
                Type = SegmentType.Load,
                FileOffset = (ulong)newSectionOffset,
                FileSize = (ulong)size,
                MemorySize = (ulong)size,
                PhysicalMemoryAddress = address,
                VirtualMemoryAddress = address
            }, null);

            // Copy section content
            int newSectionChunkIndex = elf.GetChunkIndexForOffset((ulong)newSectionOffset)!.Value.chunkIndex;
            byte[] sectionContent = new byte[size];
            contents.CopyTo(sectionContent, 0);
            elf.Chunks[newSectionChunkIndex] = new RawSectionChunk { Data = sectionContent };

            return sectionIndex;
        }

        /// <summary>
        /// Creates a new symbol and associated string table with the given symbols.
        /// </summary>
        /// <param name="elf">ELF file.</param>
        /// <param name="symbolTableSectionName">Symbol table section name.</param>
        /// <param name="stringTableSectionName">String table section name.</param>
        /// <param name="symbols">Symbols.</param>
        public static void CreateSymbolTable(this ElfFile elf, string symbolTableSectionName, string stringTableSectionName, int targetSectionIndex, List<(ulong offset, string name)> symbols)
        {
            // Allocate extra space in section name string table
            var stringTableSectionHeader = elf.SectionHeaderTable.SectionHeaders[elf.Header.SectionHeaderStringTableIndex];
            elf.AllocateFileMemory((int)stringTableSectionHeader.FileOffset + (int)stringTableSectionHeader.Size, stringTableSectionName.Length + 1 + symbolTableSectionName.Length + 1);

            // Allocate extra space for section headers
            elf.AllocateFileMemory((int)elf.Header.SectionHeaderTableFileOffset + elf.SectionHeaderTable.ByteLength, 2 * elf.SectionHeaderTable.EntrySize);

            // Allocate aligned space for new sections
            int totalFileLength = elf.Chunks.Sum(c => c.ByteLength);
            int stringTableSectionOffset = (int)(((uint)totalFileLength + 8) & ~0x7);
            int stringTableSectionSize = symbols.Sum(s => s.name.Length + 1) + 1;
            int symbolTableSectionOffset = (int)(((uint)stringTableSectionOffset + stringTableSectionSize + 8) & ~0x7);
            int symbolTableSectionSize = symbols.Count * SymbolTableChunk.SymbolTableEntry.ByteLength;
            elf.AllocateFileMemory(totalFileLength, (symbolTableSectionOffset - totalFileLength) + symbolTableSectionSize);

            // Add section names to string table
            int sectionNameStringTableIndex = elf.ExtendStringTable(elf.Header.SectionHeaderStringTableIndex, new[] { stringTableSectionName, symbolTableSectionName }, null)[0];

            // Create string table section
            int stringTableSectionIndex = elf.CreateSection(new SectionHeaderTableChunk.SectionHeaderTableEntry
            {
                Alignment = 1,
                Flags = SectionFlags.None,
                Info = 0,
                Link = 0,
                Size = (ulong)stringTableSectionSize,
                Type = SectionType.StringTable,
                EntrySize = 0,
                FileOffset = (ulong)stringTableSectionOffset,
                VirtualAddress = 0,
                NameStringTableOffset = (uint)sectionNameStringTableIndex
            }, null);

            // Create symbol table section
            int symbolTableSectionIndex = elf.CreateSection(new SectionHeaderTableChunk.SectionHeaderTableEntry
            {
                Alignment = 8,
                Flags = SectionFlags.None,
                Info = (uint)symbols.Count,
                Link = (uint)stringTableSectionIndex,
                Size = (ulong)symbolTableSectionSize,
                Type = SectionType.SymbolTable,
                EntrySize = 0x18,
                FileOffset = (ulong)symbolTableSectionOffset,
                VirtualAddress = 0,
                NameStringTableOffset = (uint)(sectionNameStringTableIndex + stringTableSectionName.Length + 1)
            }, null);

            // Create string table chunk
            int stringTableSectionChunkIndex = elf.GetChunkIndexForOffset((ulong)stringTableSectionOffset)!.Value.chunkIndex;
            (StringTableChunk stringTableChunk, int[] offsets) = StringTableChunk.FromStrings(symbols.Select(s => s.name).ToArray());

            // Place string table chunk, while ensuring that the remaining space is correctly covered by a dummy chunk (alignment)
            var rawSectionChunk = (RawSectionChunk)elf.Chunks[stringTableSectionChunkIndex];
            elf.Chunks[stringTableSectionChunkIndex] = stringTableChunk;
            int remainingStringTableSectionChunkBytes = rawSectionChunk.ByteLength - stringTableChunk.ByteLength;
            if(remainingStringTableSectionChunkBytes > 0)
                elf.Chunks.Insert(stringTableSectionChunkIndex + 1, new DummyChunk { Data = Enumerable.Repeat<byte>(0, remainingStringTableSectionChunkBytes).ToArray() });

            // Create symbol table chunk
            int symbolTableSectionChunkIndex = elf.GetChunkIndexForOffset((ulong)symbolTableSectionOffset)!.Value.chunkIndex;
            var symbolTableChunk = new SymbolTableChunk { Entries = new List<SymbolTableChunk.SymbolTableEntry>(), EntrySize = 0x18 };
            for(int i = 0; i < symbols.Count; ++i)
            {
                symbolTableChunk.Entries.Add(new SymbolTableChunk.SymbolTableEntry
                {
                    Name = (uint)offsets[i],
                    Value = symbols[i].offset,
                    Size = 0,
                    Info = SymbolInfo.TypeFunc | SymbolInfo.BindLocal,
                    Visibility = SymbolVisibility.Default,
                    Section = (ushort)targetSectionIndex
                });
            }

            // Ensure that all allocated bytes are covered
            rawSectionChunk = (RawSectionChunk)elf.Chunks[symbolTableSectionChunkIndex];
            elf.Chunks[symbolTableSectionChunkIndex] = symbolTableChunk;
            int remainingSymbolTableSectionChunkBytes = symbolTableChunk.ByteLength - rawSectionChunk.ByteLength;
            if(remainingSymbolTableSectionChunkBytes > 0)
                symbolTableChunk.TrailingByteCount += remainingSymbolTableSectionChunkBytes;
        }
    }
}