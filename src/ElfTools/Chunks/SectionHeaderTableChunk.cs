using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using ElfTools.Enums;
using ElfTools.Utilities;

namespace ElfTools.Chunks
{
    /// <summary>
    /// Contains the section header table.
    /// </summary>
    public class SectionHeaderTableChunk : Chunk
    {
        /// <summary>
        /// List of section headers.
        /// </summary>
        public List<SectionHeaderTableEntry> SectionHeaders { get; set; }

        /// <summary>
        /// Size of a single section header. This must match the <see cref="HeaderChunk.SectionHeaderTableEntrySize" /> value.
        /// Section headers are padded to achieve the given size.
        /// </summary>
        public int EntrySize { get; set; }

        public override int ByteLength => SectionHeaders.Count * EntrySize;

        public override int WriteTo(Span<byte> buffer)
        {
            int offset = 0;

            // Write chunks
            foreach(var entry in SectionHeaders)
            {
                buffer.WriteUInt32(entry.NameStringTableOffset, ref offset);
                buffer.WriteUInt32((uint)entry.Type, ref offset);
                buffer.WriteUInt64((ulong)entry.Flags, ref offset);
                buffer.WriteUInt64(entry.VirtualAddress, ref offset);
                buffer.WriteUInt64(entry.FileOffset, ref offset);
                buffer.WriteUInt64(entry.Size, ref offset);
                buffer.WriteUInt32(entry.Link, ref offset);
                buffer.WriteUInt32(entry.Info, ref offset);
                buffer.WriteUInt64(entry.Alignment, ref offset);
                buffer.WriteUInt64(entry.EntrySize, ref offset);

                // Write alignment bytes
                for(int j = SectionHeaderTableEntry.ByteLength; j < EntrySize; ++j)
                    buffer.WriteByte(0, ref offset);
            }

            return offset;
        }

        /// <summary>
        /// Initializes the chunk from the given buffer.
        /// </summary>
        /// <param name="buffer">Buffer containing chunk data.</param>
        /// <param name="entrySize">Size of one section header entry.</param>
        /// <param name="entryCount">Number of section header entries.</param>
        /// <returns>Deserialized chunk object.</returns>
        public static SectionHeaderTableChunk FromBytes(ReadOnlySpan<byte> buffer, ushort entrySize, ushort entryCount)
        {
            int offset = 0;

            var list = new List<SectionHeaderTableEntry>();
            for(int i = 0; i < entryCount; ++i)
            {
                var sectionHeader = new SectionHeaderTableEntry
                {
                    NameStringTableOffset = buffer.ReadUInt32(ref offset),
                    Type = (SectionType)buffer.ReadUInt32(ref offset),
                    Flags = (SectionFlags)buffer.ReadUInt64(ref offset),
                    VirtualAddress = buffer.ReadUInt64(ref offset),
                    FileOffset = buffer.ReadUInt64(ref offset),
                    Size = buffer.ReadUInt64(ref offset),
                    Link = buffer.ReadUInt32(ref offset),
                    Info = buffer.ReadUInt32(ref offset),
                    Alignment = buffer.ReadUInt64(ref offset),
                    EntrySize = buffer.ReadUInt64(ref offset)
                };
                list.Add(sectionHeader);

                // Skip alignment bytes
                for(int j = SectionHeaderTableEntry.ByteLength; j < entrySize; ++j)
                    buffer.ReadByte(ref offset);
            }

            return new SectionHeaderTableChunk
            {
                SectionHeaders = list,
                EntrySize = entrySize
            };
        }

        public class SectionHeaderTableEntry    :ICloneable
        {
            /// <summary>
            /// Byte length of a section header.
            /// </summary>
            public const int ByteLength = 4 + 4 + 8 + 8 + 8 + 8 + 4 + 4 + 8 + 8; // The ELF section header size is fixed

            /// <summary>
            /// Index of the section name in the section name string table.
            /// </summary>
            /// <remarks>(sh_name)</remarks>
            public uint NameStringTableOffset { get; set; }

            /// <summary>
            /// Section type.
            /// </summary>
            /// <remarks>(sh_type)</remarks>
            public SectionType Type { get; set; }

            /// <summary>
            /// Flags.
            /// </summary>
            /// <remarks>(sh_flags)</remarks>
            public SectionFlags Flags { get; set; }

            /// <summary>
            /// Virtual address at execution.
            /// </summary>
            /// <remarks>(sh_addr)</remarks>
            public ulong VirtualAddress { get; set; }

            /// <summary>
            /// File offset.
            /// </summary>
            /// <remarks>(sh_off)</remarks>
            public ulong FileOffset { get; set; }

            /// <summary>
            /// Section size in bytes.
            /// </summary>
            /// <remarks>(sh_size)</remarks>
            public ulong Size { get; set; }

            /// <summary>
            /// Link to another section.
            /// </summary>
            /// <remarks>(sh_link)</remarks>
            public uint Link { get; set; }

            /// <summary>
            /// Additional section information.
            /// </summary>
            /// <remarks>(sh_info)</remarks>
            public uint Info { get; set; }

            /// <summary>
            /// Alignment.
            /// </summary>
            /// <remarks>(sh_addralign)</remarks>
            public ulong Alignment { get; set; }

            /// <summary>
            /// Table entry size, if this section holds a table.
            /// </summary>
            /// <remarks>(sh_entsize)</remarks>
            public ulong EntrySize { get; set; }

            public object Clone()
            {
                return this.MemberwiseClone();
            }
        }
    }
}