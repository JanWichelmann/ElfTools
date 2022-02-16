using System;
using System.Collections.Immutable;
using System.Linq;
using ElfTools.Chunks;
using ElfTools.Enums;

namespace ElfTools.Instrumentation
{
    /// <summary>
    /// Provides tools for adding new sections to an existing ELF file.
    /// </summary>
    public class SectionAllocator
    {
        private readonly ElfBuilder _elfBuilder;

        /// <summary>
        /// Initializes a new section allocator for the given ELF builder.
        /// </summary>
        /// <param name="elfBuilder">ELF builder.</param>
        public SectionAllocator(ElfBuilder elfBuilder)
        {
            _elfBuilder = elfBuilder ?? throw new ArgumentNullException(nameof(elfBuilder));
        }

        /// <summary>
        /// Allocates a new PROGBITS section/LOAD segment at the given address and with the given contents.
        /// </summary>
        /// <param name="name">Section name.</param>
        /// <param name="address">Segment virtual/physical address.</param>
        /// <param name="size">Size of the section/segment (may be larger than the actual contents)</param>
        /// <param name="alignment">Section/segment alignment.</param>
        /// <param name="isWritable">Determines whether the segment is writable.</param>
        /// <param name="isExecutable">Determines whether the segment is executable.</param>
        /// <param name="contents">Data which should be copied to the section begin.</param>
        /// <returns>The index of the newly created section.</returns>
        public int AllocateProgBitsSection(string name, ulong address, int size, int alignment, bool isWritable, bool isExecutable, byte[] contents)
        {
            // Allocate extra space in program header, section string and section header tables
            _elfBuilder.AllocateFileMemory((int)_elfBuilder.Header.ProgramHeaderTableFileOffset + _elfBuilder.ProgramHeaderTable.ByteLength, 1 * _elfBuilder.ProgramHeaderTable.EntrySize); // Program header
            var stringTableSectionHeader = _elfBuilder.SectionHeaderTable.SectionHeaders[_elfBuilder.Header.SectionHeaderStringTableIndex];
            _elfBuilder.AllocateFileMemory((int)stringTableSectionHeader.FileOffset + (int)stringTableSectionHeader.Size, name.Length + 1); // String table
            _elfBuilder.AllocateFileMemory((int)_elfBuilder.Header.SectionHeaderTableFileOffset + _elfBuilder.SectionHeaderTable.ByteLength, _elfBuilder.SectionHeaderTable.EntrySize); // Section header table

            // Allocate new section
            int totalFileLength = _elfBuilder.Chunks.Sum(c => c.ByteLength);
            int newSectionOffset = ((totalFileLength & (alignment - 1)) != 0) ? totalFileLength + alignment - (totalFileLength & (alignment - 1)) : totalFileLength;
            _elfBuilder.AllocateFileMemory(totalFileLength, (newSectionOffset - totalFileLength) + size);

            // Add section name to string table
            int newSectionNameStringTableIndex = _elfBuilder.ExtendStringTable(_elfBuilder.Header.SectionHeaderStringTableIndex, name)[0];

            // Add new section
            int sectionIndex = _elfBuilder.CreateSection(new SectionHeaderTableChunk.SectionHeaderTableEntry
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
            });

            // Add new executable segment
            _elfBuilder.ExtendProgramHeaderTable(new ProgramHeaderTableChunk.ProgramHeaderTableEntry
            {
                Alignment = (ulong)alignment,
                Flags = SegmentFlags.Readable | (isWritable ? SegmentFlags.Writable : SegmentFlags.None) | (isExecutable ? SegmentFlags.Executable : SegmentFlags.None),
                Type = SegmentType.Load,
                FileOffset = (ulong)newSectionOffset,
                FileSize = (ulong)size,
                MemorySize = (ulong)size,
                PhysicalMemoryAddress = address,
                VirtualMemoryAddress = address
            });

            // Copy section content
            int newSectionChunkIndex = _elfBuilder.GetChunkIndexForOffset((ulong)newSectionOffset)!.Value.chunkIndex;
            _elfBuilder.Chunks[newSectionChunkIndex] = new RawSectionChunk { Data = contents.Concat(Enumerable.Repeat<byte>(0, size - contents.Length)).ToImmutableArray() };

            return sectionIndex;
        }
    }
}