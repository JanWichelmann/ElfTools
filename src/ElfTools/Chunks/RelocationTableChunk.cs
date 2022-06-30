using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using ElfTools.Utilities;

namespace ElfTools.Chunks
{
    /// <summary>
    /// Contains relocations without addend.
    /// </summary>
    public class RelocationTableChunk : SectionChunk
    {
        /// <summary>
        /// List of table entries.
        /// </summary>
        public List<RelocationEntry> Entries { get; set; } 

        /// <summary>
        /// Size of an entry. This must match <see cref="SectionHeaderTableChunk.SectionHeaderTableEntry.EntrySize" />.
        /// Entries are padded to achieve the given size.
        /// </summary>
        public int EntrySize { get; set; }

        /// <summary>
        /// Number of trailing bytes after the table entries.
        /// </summary>
        public int TrailingByteCount { get; set; }

        public override int ByteLength => Entries.Count * EntrySize + TrailingByteCount;

        public override int WriteTo(Span<byte> buffer)
        {
            int offset = 0;

            // Write chunks
            foreach(var entry in Entries)
            {
                buffer.WriteUInt64(entry.Offset, ref offset);
                buffer.WriteUInt64(entry.Info, ref offset);

                // Write alignment bytes
                for(int j = RelocationEntry.ByteLength; j < EntrySize; ++j)
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
        /// <param name="entryCount">Number of entries.</param>
        /// <returns>Deserialized chunk object.</returns>
        public static RelocationTableChunk FromBytes(ReadOnlySpan<byte> buffer, int entrySize, int entryCount)
        {
            int offset = 0;

            var list = new List<RelocationEntry>();
            for(int i = 0; i < entryCount; ++i)
            {
                var sectionHeader = new RelocationEntry
                {
                    Offset = buffer.ReadUInt64(ref offset),
                    Info = buffer.ReadUInt64(ref offset)
                };
                list.Add(sectionHeader);

                // Skip alignment bytes
                for(int j = RelocationEntry.ByteLength; j < entrySize; ++j)
                    buffer.ReadByte(ref offset);
            }

            return new RelocationTableChunk
            {
                Entries = list,
                EntrySize = entrySize,
                TrailingByteCount = buffer.Length - offset
            };
        }

        public class RelocationEntry
        {
            /// <summary>
            /// Byte length of an entry.
            /// </summary>
            public const int ByteLength = 8 + 8;

            /// <summary>
            /// File offset or virtual address where the relocation is applied.
            /// </summary>
            public ulong Offset { get; set; }

            /// <summary>
            /// Relocation info.
            /// </summary>
            public ulong Info { get; set; }
        }
    }
}