using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using ElfTools.Enums;
using ElfTools.Utilities;

namespace ElfTools.Chunks
{
    public record ProgramHeaderTableChunk : Chunk
    {
        /// <summary>
        /// List of program headers.
        /// </summary>
        public ImmutableList<ProgramHeaderTableEntry> ProgramHeaders { get; init; } = ImmutableList<ProgramHeaderTableEntry>.Empty;

        /// <summary>
        /// Size of a single program header. This must match the <see cref="HeaderChunk.ProgramHeaderTableEntrySize" /> value.
        /// Program headers are padded to achieve the given size.
        /// </summary>
        public int EntrySize { get; init; }

        public override int ByteLength => ProgramHeaders.Count * EntrySize;

        public override int WriteTo(Span<byte> buffer)
        {
            int offset = 0;

            // Write chunks
            foreach(var entry in ProgramHeaders)
            {
                buffer.WriteUInt32((uint)entry.Type, ref offset);
                buffer.WriteUInt32((uint)entry.Flags, ref offset);
                buffer.WriteUInt64(entry.FileOffset, ref offset);
                buffer.WriteUInt64(entry.VirtualMemoryAddress, ref offset);
                buffer.WriteUInt64(entry.PhysicalMemoryAddress, ref offset);
                buffer.WriteUInt64(entry.FileSize, ref offset);
                buffer.WriteUInt64(entry.MemorySize, ref offset);
                buffer.WriteUInt64(entry.Alignment, ref offset);

                // Write alignment bytes
                for(int j = ProgramHeaderTableEntry.ByteLength; j < EntrySize; ++j)
                    buffer.WriteByte(0, ref offset);
            }

            return offset;
        }

        /// <summary>
        /// Initializes the chunk from the given buffer.
        /// </summary>
        /// <param name="buffer">Buffer containing chunk data.</param>
        /// <param name="entrySize">Size of one program header entry.</param>
        /// <param name="entryCount">Number of program header entries.</param>
        /// <returns>Deserialized chunk object.</returns>
        public static ProgramHeaderTableChunk FromBytes(ReadOnlySpan<byte> buffer, ushort entrySize, ushort entryCount)
        {
            int offset = 0;

            var list = new List<ProgramHeaderTableEntry>();
            for(int i = 0; i < entryCount; ++i)
            {
                var programHeader = new ProgramHeaderTableEntry
                {
                    Type = (SegmentType)buffer.ReadUInt32(ref offset),
                    Flags = (SegmentFlags)buffer.ReadUInt32(ref offset),
                    FileOffset = buffer.ReadUInt64(ref offset),
                    VirtualMemoryAddress = buffer.ReadUInt64(ref offset),
                    PhysicalMemoryAddress = buffer.ReadUInt64(ref offset),
                    FileSize = buffer.ReadUInt64(ref offset),
                    MemorySize = buffer.ReadUInt64(ref offset),
                    Alignment = buffer.ReadUInt64(ref offset)
                };
                list.Add(programHeader);

                // Skip alignment bytes
                for(int j = ProgramHeaderTableEntry.ByteLength; j < entrySize; ++j)
                    buffer.ReadByte(ref offset);
            }

            return new ProgramHeaderTableChunk
            {
                ProgramHeaders = list.ToImmutableList(),
                EntrySize = entrySize
            };
        }

        public record ProgramHeaderTableEntry
        {
            /// <summary>
            /// Byte length of a program header.
            /// </summary>
            public const int ByteLength = 4 + 4 + 8 + 8 + 8 + 8 + 8 + 8; // The ELF program header size is fixed

            /// <summary>
            /// Segment type.
            /// </summary>
            /// <remarks>(p_type)</remarks>
            public SegmentType Type { get; init; }

            /// <summary>
            /// Flags.
            /// </summary>
            /// <remarks>(p_flags)</remarks>
            public SegmentFlags Flags { get; init; }

            /// <summary>
            /// File offset of segment data.
            /// </summary>
            /// <remarks>(p_offset)</remarks>
            public ulong FileOffset { get; init; }

            /// <summary>
            /// Virtual memory address of segment data.
            /// </summary>
            /// <remarks>(p_vaddr)</remarks>
            public ulong VirtualMemoryAddress { get; init; }

            /// <summary>
            /// Physical memory address of segment data.
            /// </summary>
            /// <remarks>(p_paddr)</remarks>
            public ulong PhysicalMemoryAddress { get; init; }

            /// <summary>
            /// File size of segment data.
            /// </summary>
            /// <remarks>(p_filesz)</remarks>
            public ulong FileSize { get; init; }

            /// <summary>
            /// Memory size of segment data.
            /// </summary>
            /// <remarks>(p_memsz)</remarks>
            public ulong MemorySize { get; init; }

            /// <summary>
            /// Alignment.
            /// </summary>
            /// <remarks>(p_align)</remarks>
            public ulong Alignment { get; init; }
        }
    }
}