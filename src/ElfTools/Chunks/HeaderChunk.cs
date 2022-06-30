using System;
using ElfTools.Enums;
using ElfTools.Utilities;

namespace ElfTools.Chunks
{
    public class HeaderChunk : Chunk
    {
        /// <summary>
        /// Size of an ELF header.
        /// </summary>
        public const int HeaderByteSize = 16 + 16 + 16 + 6 + 10; // The ELF header size is fixed

        /// <summary>
        /// Magic number for identifying ELF files.
        /// </summary>
        /// <remarks>(e_ident.EI_MAGx)</remarks>
        public byte[] MagicNumber { get; set; } = { 0x7f, 0x45, 0x4c, 0x46 };

        /// <summary>
        /// The architecture (= bit size) of this binary.
        /// </summary>
        /// <remarks>(e_ident.EI_CLASS)</remarks>
        public BinaryClass Class { get; set; }

        /// <summary>
        /// Data encoding endianness.
        /// </summary>
        /// <remarks>(e_ident.EI_DATA)</remarks>
        public BinaryEncoding Encoding { get; set; }

        /// <summary>
        /// File version.
        /// </summary>
        /// <remarks>(e_ident.EI_VERSION)</remarks>
        public BinaryVersion Version { get; set; }

        /// <summary>
        /// Target OS ABI.
        /// </summary>
        /// <remarks>(e_ident.EI_OSABI)</remarks>
        public TargetAbi TargetAbi { get; set; }

        /// <summary>
        /// Target OS ABI version.
        /// </summary>
        /// <remarks>(e_ident.EI_ABIVERSION)</remarks>
        public byte TargetAbiVersion { get; set; }

        /// <summary>
        /// Reserved bytes (padding).
        /// </summary>
        /// <remarks>(e_ident.EI_PAD)</remarks>
        public byte[] IdentifierPadding { get; set; } = new byte[16 - 9];

        /// <summary>
        /// Object file type.
        /// </summary>
        /// <remarks>(e_type)</remarks>
        public ObjectFileType ObjectFileType { get; set; }

        /// <summary>
        /// Target machine architecture.
        /// </summary>
        /// <remarks>(e_machine)</remarks>
        public MachineArchitecture TargetArchitecture { get; set; }

        /// <summary>
        /// Object file version.
        /// </summary>
        /// <remarks>(e_version)</remarks>
        public ObjectFileVersion ObjectFileVersion { get; set; }

        /// <summary>
        /// Virtual address of entrypoint.
        /// </summary>
        /// <remarks>(e_entry)</remarks>
        public ulong EntryPoint { get; set; }

        /// <summary>
        /// File offset of the program header table. This corresponds to the file offset of the active <see cref="ProgramHeaderTableChunk" /> object.
        /// </summary>
        /// <remarks>(e_phoff)</remarks>
        public ulong ProgramHeaderTableFileOffset { get; set; }

        /// <summary>
        /// File offset of the section header table. This corresponds to the file offset of the active <see cref="SectionHeaderTableChunk" /> object.
        /// </summary>
        /// <remarks>(e_shoff)</remarks>
        public ulong SectionHeaderTableFileOffset { get; set; }

        /// <summary>
        /// Processor-specific flags.
        /// </summary>
        /// <remarks>(e_flags)</remarks>
        public uint ProcessorSpecificFlags { get; set; }

        /// <summary>
        /// Header size.
        /// </summary>
        /// <remarks>(e_ehsize)</remarks>
        public ushort HeaderSize { get; set; }

        /// <summary>
        /// Size of a program header table entry.
        /// </summary>
        /// <remarks>(e_phentsize)</remarks>
        public ushort ProgramHeaderTableEntrySize { get; set; }

        /// <summary>
        /// Number of program header table entries.
        /// </summary>
        /// <remarks>(e_phnum)</remarks>
        public ushort ProgramHeaderTableEntryCount { get; set; }

        /// <summary>
        /// Size of a section header table entry.
        /// </summary>
        /// <remarks>(e_shentsize)</remarks>
        public ushort SectionHeaderTableEntrySize { get; set; }

        /// <summary>
        /// Number of section header table entries.
        /// </summary>
        /// <remarks>(e_shnum)</remarks>
        public ushort SectionHeaderTableEntryCount { get; set; }

        /// <summary>
        /// Section index of string table.
        /// </summary>
        /// <remarks>(e_shstrndx)</remarks>
        public ushort SectionHeaderStringTableIndex { get; set; }

        public override int ByteLength => HeaderByteSize;

        public override int WriteTo(Span<byte> buffer)
        {
            int offset = 0;

            // Identifier
            buffer.WriteBytes(MagicNumber, ref offset);
            buffer.WriteByte((byte)Class, ref offset);
            buffer.WriteByte((byte)Encoding, ref offset);
            buffer.WriteByte((byte)Version, ref offset);
            buffer.WriteByte((byte)TargetAbi, ref offset);
            buffer.WriteByte(TargetAbiVersion, ref offset);
            buffer.WriteBytes(IdentifierPadding, ref offset);

            // Target info
            buffer.WriteUInt16((ushort)ObjectFileType, ref offset);
            buffer.WriteUInt16((ushort)TargetArchitecture, ref offset);
            buffer.WriteUInt32((uint)ObjectFileVersion, ref offset);
            buffer.WriteUInt64(EntryPoint, ref offset);

            // Offsets
            buffer.WriteUInt64(ProgramHeaderTableFileOffset, ref offset);
            buffer.WriteUInt64(SectionHeaderTableFileOffset, ref offset);

            // Other
            buffer.WriteUInt32(ProcessorSpecificFlags, ref offset);
            buffer.WriteUInt16(HeaderSize, ref offset);

            // Table info
            buffer.WriteUInt16(ProgramHeaderTableEntrySize, ref offset);
            buffer.WriteUInt16(ProgramHeaderTableEntryCount, ref offset);
            buffer.WriteUInt16(SectionHeaderTableEntrySize, ref offset);
            buffer.WriteUInt16(SectionHeaderTableEntryCount, ref offset);
            buffer.WriteUInt16(SectionHeaderStringTableIndex, ref offset);

            return offset;
        }

        /// <summary>
        /// Initializes the chunk from the given buffer.
        /// </summary>
        /// <param name="buffer">Buffer containing chunk data.</param>
        /// <returns>Deserialized chunk object.</returns>
        public static HeaderChunk FromBytes(ReadOnlySpan<byte> buffer)
        {
            int offset = 0;

            return new HeaderChunk
            {
                // Identifier
                // 16 bytes
                MagicNumber = buffer.ReadBytes(ref offset, 4),
                Class = (BinaryClass)buffer.ReadByte(ref offset),
                Encoding = (BinaryEncoding)buffer.ReadByte(ref offset),
                Version = (BinaryVersion)buffer.ReadByte(ref offset),
                TargetAbi = (TargetAbi)buffer.ReadByte(ref offset),
                TargetAbiVersion = buffer.ReadByte(ref offset),
                IdentifierPadding = buffer.ReadBytes(ref offset, 16 - 9),

                // Target info
                // 16 bytes
                ObjectFileType = (ObjectFileType)buffer.ReadUInt16(ref offset),
                TargetArchitecture = (MachineArchitecture)buffer.ReadUInt16(ref offset),
                ObjectFileVersion = (ObjectFileVersion)buffer.ReadUInt32(ref offset),
                EntryPoint = buffer.ReadUInt64(ref offset),

                // Offsets
                // 16 bytes
                ProgramHeaderTableFileOffset = buffer.ReadUInt64(ref offset),
                SectionHeaderTableFileOffset = buffer.ReadUInt64(ref offset),

                // Other
                // 6 bytes
                ProcessorSpecificFlags = buffer.ReadUInt32(ref offset),
                HeaderSize = buffer.ReadUInt16(ref offset),

                // Table info
                // 10 bytes
                ProgramHeaderTableEntrySize = buffer.ReadUInt16(ref offset),
                ProgramHeaderTableEntryCount = buffer.ReadUInt16(ref offset),
                SectionHeaderTableEntrySize = buffer.ReadUInt16(ref offset),
                SectionHeaderTableEntryCount = buffer.ReadUInt16(ref offset),
                SectionHeaderStringTableIndex = buffer.ReadUInt16(ref offset)
            };
        }
    }
}