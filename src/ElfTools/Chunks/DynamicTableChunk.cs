using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using ElfTools.Enums;
using ElfTools.Utilities;

namespace ElfTools.Chunks
{
    public record DynamicTableChunk : SectionChunk
    {
        /// <summary>
        /// List of table entries.
        /// </summary>
        public ImmutableList<DynamicTableEntry> Entries { get; init; } = ImmutableList<DynamicTableEntry>.Empty;

        /// <summary>
        /// Size of an entry. This must match <see cref="SectionHeaderTableChunk.SectionHeaderTableEntry.EntrySize" />.
        /// Entries are padded to achieve the given size.
        /// </summary>
        public int EntrySize { get; init; }

        /// <summary>
        /// Number of trailing bytes after the table entries.
        /// </summary>
        public int TrailingByteCount { get; init; }

        public override int ByteLength => Entries.Count * EntrySize + TrailingByteCount;

        public override int WriteTo(Span<byte> buffer)
        {
            int offset = 0;

            // Write chunks
            foreach(var entry in Entries)
            {
                buffer.WriteInt64((long)entry.Type, ref offset);
                buffer.WriteUInt64(entry.Value, ref offset);

                // Write alignment bytes
                for(int j = DynamicTableEntry.ByteLength; j < EntrySize; ++j)
                    buffer.WriteByte(0, ref offset);
            }

            // Write trailing bytes
            for(int i = 0; i < TrailingByteCount; ++i)
                buffer.WriteByte(0, ref offset);

            return offset;
        }

        /// <summary>
        /// Initializes the chunk from the given buffer.
        /// </summary>
        /// <param name="buffer">Buffer containing chunk data.</param>
        /// <param name="entrySize">Size of one entry.</param>
        /// <returns>Deserialized chunk object.</returns>
        public static DynamicTableChunk FromBytes(ReadOnlySpan<byte> buffer, int entrySize)
        {
            int offset = 0;

            var list = new List<DynamicTableEntry>();
            while(offset <= buffer.Length - entrySize)
            {
                var sectionHeader = new DynamicTableEntry
                {
                    Type = (DynamicEntryType)buffer.ReadInt64(ref offset),
                    Value = buffer.ReadUInt64(ref offset)
                };
                list.Add(sectionHeader);

                // Skip alignment bytes
                for(int j = DynamicTableEntry.ByteLength; j < entrySize; ++j)
                    buffer.ReadByte(ref offset);
            }

            return new DynamicTableChunk
            {
                Entries = list.ToImmutableList(),
                EntrySize = entrySize,
                TrailingByteCount = buffer.Length - offset
            };
        }

        public record DynamicTableEntry
        {
            /// <summary>
            /// Byte length of an entry.
            /// </summary>
            public const int ByteLength = 8 + 8;

            /// <summary>
            /// Entry type.
            /// </summary>
            /// <remarks>(d_tag)</remarks>
            public DynamicEntryType Type { get; init; }

            /// <summary>
            /// Entry value.
            /// </summary>
            /// <remarks>(d_un.d_val or d_un.d_ptr)</remarks>
            public ulong Value { get; init; }
        }
    }
}