using System;
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
    }
}