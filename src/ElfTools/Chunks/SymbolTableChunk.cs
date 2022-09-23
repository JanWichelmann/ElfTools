using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using ElfTools.Enums;
using ElfTools.Utilities;

namespace ElfTools.Chunks
{
    /// <summary>
    /// Contains a symbol table.
    /// </summary>
    public class SymbolTableChunk : SectionChunk
    {
        /// <summary>
        /// List of table entries.
        /// </summary>
        public List<SymbolTableEntry> Entries { get; set; }

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
                buffer.WriteUInt32(entry.Name, ref offset);
                buffer.WriteByte((byte)entry.Info, ref offset);
                buffer.WriteByte((byte)entry.Visibility, ref offset);
                buffer.WriteUInt16(entry.Section, ref offset);
                buffer.WriteUInt64(entry.Value, ref offset);
                buffer.WriteUInt64(entry.Size, ref offset);

                // Write alignment bytes
                for(int j = SymbolTableEntry.ByteLength; j < EntrySize; ++j)
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
        public static SymbolTableChunk FromBytes(ReadOnlySpan<byte> buffer, int entrySize)
        {
            int offset = 0;

            var list = new List<SymbolTableEntry>();
            while(offset <= buffer.Length - entrySize)
            {
                var symbolTableEntry = new SymbolTableEntry
                {
                    Name = buffer.ReadUInt32(ref offset),
                    Info = (SymbolInfo)buffer.ReadByte(ref offset),
                    Visibility = (SymbolVisibility)buffer.ReadByte(ref offset),
                    Section = buffer.ReadUInt16(ref offset),
                    Value = buffer.ReadUInt64(ref offset),
                    Size = buffer.ReadUInt64(ref offset)
                };
                list.Add(symbolTableEntry);

                // Skip alignment bytes
                for(int j = SymbolTableEntry.ByteLength; j < entrySize; ++j)
                    buffer.ReadByte(ref offset);
            }

            return new SymbolTableChunk
            {
                Entries = list,
                EntrySize = entrySize,
                TrailingByteCount = buffer.Length - offset
            };
        }

        public class SymbolTableEntry
        {
            /// <summary>
            /// Byte length of an entry.
            /// </summary>
            public const int ByteLength = 4 + 1 + 1 + 2 + 8 + 8;

            /// <summary>
            /// String table index of the symbol name.
            /// </summary>
            /// <remarks>(st_name)</remarks>
            public uint Name { get; set; }

            /// <summary>
            /// Symbol type and binding.
            /// </summary>
            /// <remarks>(st_info)</remarks>
            public SymbolInfo Info { get; set; }

            /// <summary>
            /// Symbol visibility.
            /// </summary>
            /// <remarks>(st_other)</remarks>
            public SymbolVisibility Visibility { get; set; }

            /// <summary>
            /// Section index.
            /// </summary>
            /// <remarks>(st_shndx)</remarks>
            public ushort Section { get; set; }

            /// <summary>
            /// Symbol value.
            /// </summary>
            /// <remarks>(st_value)</remarks>
            public ulong Value { get; set; }

            /// <summary>
            /// Symbol size.
            /// </summary>
            /// <remarks>(st_size)</remarks>
            public ulong Size { get; set; }
        }
    }
}