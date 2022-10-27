using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using ElfTools.Chunks;
using ElfTools.Enums;

namespace ElfTools
{
    /// <summary>
    /// Provides functionality to read and parse ELF files.
    /// </summary>
    public static class ElfReader
    {
        /// <summary>
        /// Loads the given ELF file.
        /// </summary>
        /// <param name="path">Path to the ELF file.</param>
        /// <returns></returns>
        public static ElfFile Load(string path)
        {
            return Load(File.ReadAllBytes(path));
        }

        /// <summary>
        /// Loads an ELF file from the given buffer.
        /// </summary>
        /// <param name="elf">Byte array containing the ELF file data.</param>
        /// <returns></returns>
        public static ElfFile Load(byte[] elf)
        {
            var elfSpan = elf.AsSpan();
            SortedList<ulong, (int? sectionIndex, Chunk)> chunkList = new();

            // ELF header
            var headerSpan = elfSpan;
            HeaderChunk headerChunk = HeaderChunk.FromBytes
            (
                headerSpan
            );
            chunkList.Add(0, (null, headerChunk));
            if(headerChunk.Class != BinaryClass.Elf64 || headerChunk.Encoding != BinaryEncoding.LittleEndian)
                throw new InvalidOperationException("Currently only little-endian ELF64 files are supported.");

            // Program header table
            ProgramHeaderTableChunk? programHeaderTableChunk = null;
            if(headerChunk.ProgramHeaderTableFileOffset != 0)
            {
                var programHeaderTableSpan = elfSpan.Slice((int)headerChunk.ProgramHeaderTableFileOffset, headerChunk.ProgramHeaderTableEntryCount * headerChunk.ProgramHeaderTableEntrySize);
                programHeaderTableChunk = ProgramHeaderTableChunk.FromBytes
                (
                    programHeaderTableSpan,
                    headerChunk.ProgramHeaderTableEntrySize,
                    headerChunk.ProgramHeaderTableEntryCount
                );
                chunkList.Add(headerChunk.ProgramHeaderTableFileOffset, (null, programHeaderTableChunk));
            }

            // Section header table
            var sectionHeaderTableSpan = elfSpan.Slice((int)headerChunk.SectionHeaderTableFileOffset, headerChunk.SectionHeaderTableEntryCount * headerChunk.SectionHeaderTableEntrySize);
            SectionHeaderTableChunk sectionHeaderTableChunk = SectionHeaderTableChunk.FromBytes
            (
                sectionHeaderTableSpan,
                headerChunk.SectionHeaderTableEntrySize,
                headerChunk.SectionHeaderTableEntryCount
            );
            chunkList.Add(headerChunk.SectionHeaderTableFileOffset, (null, sectionHeaderTableChunk));

            // Find .dynamic section, as it contains info about other sections
            HashSet<int> parsedSectionIds = new();
            DynamicTableChunk? dynamicTableChunk = null;
            Dictionary<DynamicEntryType, List<ulong>> dynamicTableEntries = new();
            for(var i = 0; i < sectionHeaderTableChunk.SectionHeaders.Count; i++)
            {
                var sectionHeader = sectionHeaderTableChunk.SectionHeaders[i];
                if(sectionHeader.Type != SectionType.Dynamic)
                    continue;

                // Read section
                var sectionSpan = elfSpan.Slice((int)sectionHeader.FileOffset, (int)sectionHeader.Size);
                dynamicTableChunk = DynamicTableChunk.FromBytes(sectionSpan, (int)sectionHeader.EntrySize);

                // The table may contain metadata for parsing certain sections, so store an easy type -> values mapping
                dynamicTableEntries = dynamicTableChunk.Entries
                    .GroupBy(e => e.Type)
                    .ToDictionary
                    (
                        eg => eg.Key,
                        eg => eg
                            .Select(e => e.Value)
                            .ToList()
                    );

                chunkList.Add(sectionHeader.FileOffset, (i, dynamicTableChunk));
                parsedSectionIds.Add(i);
                break;
            }

            // Read relocations
            if(dynamicTableEntries.ContainsKey(DynamicEntryType.DT_RELA))
            {
                // Find associated section header
                ulong relocationTableAddress = dynamicTableEntries[DynamicEntryType.DT_RELA].First();
                var sectionHeaderIndex = sectionHeaderTableChunk.SectionHeaders.FindIndex(e => e.VirtualAddress == relocationTableAddress);
                var sectionHeader = sectionHeaderTableChunk.SectionHeaders[sectionHeaderIndex];

                // Read section
                var chunk = ReadRelocationChunk(DynamicEntryType.DT_RELA, dynamicTableEntries, sectionHeader, elfSpan);
                if(chunk != null)
                {
                    chunkList.Add(sectionHeader.FileOffset, (sectionHeaderIndex, chunk));
                    parsedSectionIds.Add(sectionHeaderIndex);
                }
            }

            if(dynamicTableEntries.ContainsKey(DynamicEntryType.DT_REL))
            {
                // Find associated section header
                ulong relocationTableAddress = dynamicTableEntries[DynamicEntryType.DT_REL].First();
                var sectionHeaderIndex = sectionHeaderTableChunk.SectionHeaders.FindIndex(e => e.VirtualAddress == relocationTableAddress);
                var sectionHeader = sectionHeaderTableChunk.SectionHeaders[sectionHeaderIndex];

                // Read section
                var chunk = ReadRelocationChunk(DynamicEntryType.DT_REL, dynamicTableEntries, sectionHeader, elfSpan);
                if(chunk != null)
                {
                    chunkList.Add(sectionHeader.FileOffset, (sectionHeaderIndex, chunk));
                    parsedSectionIds.Add(sectionHeaderIndex);
                }
            }

            if(dynamicTableEntries.ContainsKey(DynamicEntryType.DT_JMPREL))
            {
                // Find associated section header
                ulong relocationTableAddress = dynamicTableEntries[DynamicEntryType.DT_JMPREL].First();
                var sectionHeaderIndex = sectionHeaderTableChunk.SectionHeaders.FindIndex(e => e.VirtualAddress == relocationTableAddress);
                var sectionHeader = sectionHeaderTableChunk.SectionHeaders[sectionHeaderIndex];

                // Read section
                var chunk = ReadRelocationChunk(DynamicEntryType.DT_JMPREL, dynamicTableEntries, sectionHeader, elfSpan);
                if(chunk != null)
                {
                    chunkList.Add(sectionHeader.FileOffset, (sectionHeaderIndex, chunk));
                    parsedSectionIds.Add(sectionHeaderIndex);
                }
            }

            // Read remaining sections
            for(var i = 0; i < sectionHeaderTableChunk.SectionHeaders.Count; i++)
            {
                var sectionHeader = sectionHeaderTableChunk.SectionHeaders[i];

                // Skip already parsed sections
                if(parsedSectionIds.Contains(i))
                    continue;

                // Skip .bss section
                // This section is pointed out with some offset and size, but is not actually present in the file
                if(sectionHeader.Type == SectionType.NoBits)
                    continue;

                var sectionSpan = elfSpan.Slice((int)sectionHeader.FileOffset, (int)sectionHeader.Size);
                Chunk? chunk = sectionHeader.Type switch
                {
                    SectionType.StringTable => StringTableChunk.FromBytes(sectionSpan),
                    SectionType.SymbolTable => SymbolTableChunk.FromBytes(sectionSpan, (int)sectionHeader.EntrySize),
                    SectionType.DynamicSymbols => SymbolTableChunk.FromBytes(sectionSpan, (int)sectionHeader.EntrySize),
                    SectionType.Notes => NotesChunk.FromBytes(sectionSpan),
                    SectionType.GnuVersionDefinition => VerdefChunk.FromBytes(sectionSpan),
                    SectionType.GnuVersionNeeds => VerneedChunk.FromBytes(sectionSpan),
                    _ => sectionSpan.Length > 0 ? RawSectionChunk.FromBytes(sectionSpan) : null
                };

                if(chunk != null)
                    chunkList.Add(sectionHeader.FileOffset, (i, chunk));
            }

            // Store chunks ordered by base address
            // Ensure that there are no overlaps
            // Fill missing space with dummy chunks
            ulong nextBaseAddress = 0;
            int chunkIndex = 0;
            List<Chunk> chunks = new();
            foreach(var (baseAddress, (sectionIndex, chunk)) in chunkList)
            {
                // Overlap?
                if(baseAddress < nextBaseAddress)
                    throw new Exception($"The chunk at 0x{baseAddress:x16} overlaps with a chunk that ends at 0x{nextBaseAddress - 1:x16}.");

                // Missing space?
                if(baseAddress > nextBaseAddress)
                {
                    // Insert dummy chunk
                    chunks.Add(DummyChunk.FromBytes(elfSpan[(int)nextBaseAddress..(int)baseAddress]));
                    ++chunkIndex;
                }

                // Store chunk
                chunks.Add(chunk);
                ++chunkIndex;

                // Next
                nextBaseAddress = baseAddress + (ulong)chunk.ByteLength;
            }

            // If there is unread data left, read final dummy chunk
            if((int)nextBaseAddress < elfSpan.Length)
                chunks.Add(DummyChunk.FromBytes(elfSpan[(int)nextBaseAddress..]));

            // Create ELF object
            return new ElfFile
            {
                Chunks = chunks,
                Header = headerChunk,
                ProgramHeaderTable = programHeaderTableChunk,
                SectionHeaderTable = sectionHeaderTableChunk,
                DynamicTable = dynamicTableChunk
            };
        }

        /// <summary>
        /// Reads a relocation section from memory.
        /// </summary>
        /// <param name="relocationTableEntryTypeBase">Type of the dynamic table address entry for the given relocation table.</param>
        /// <param name="dynamicTableEntries">Parsed dynamic table.</param>
        /// <param name="sectionHeader">Section header of the relocation table.</param>
        /// <param name="elfSpan">Raw ELF data.</param>
        /// <returns></returns>
        private static Chunk? ReadRelocationChunk(DynamicEntryType relocationTableEntryTypeBase, Dictionary<DynamicEntryType, List<ulong>> dynamicTableEntries, SectionHeaderTableChunk.SectionHeaderTableEntry sectionHeader, Span<byte> elfSpan)
        {
            // Identify effective relocation entry type (relevant for PLT relocations, as those share metadata with other REL/RELA tables)
            DynamicEntryType relocationTableEntryType = relocationTableEntryTypeBase;
            if(relocationTableEntryTypeBase == DynamicEntryType.DT_JMPREL)
            {
                if(dynamicTableEntries.TryGetValue(DynamicEntryType.DT_PLTREL, out var pltRelocationTypes))
                    relocationTableEntryType = (DynamicEntryType)pltRelocationTypes.First();
                else
                    return null; // We could guess, but that would be unsafe. Rather not parse the chunk at all
            }

            // Determine corresponding metadata entry types
            DynamicEntryType? relocationTableEntrySizeType = relocationTableEntryTypeBase switch
            {
                DynamicEntryType.DT_RELA => DynamicEntryType.DT_RELAENT,
                DynamicEntryType.DT_REL => DynamicEntryType.DT_RELENT,
                DynamicEntryType.DT_JMPREL => relocationTableEntryType == DynamicEntryType.DT_REL ? DynamicEntryType.DT_RELENT : DynamicEntryType.DT_RELAENT,
                _ => null
            };
            DynamicEntryType? relocationTableSizeType = relocationTableEntryTypeBase switch
            {
                DynamicEntryType.DT_RELA => DynamicEntryType.DT_RELASZ,
                DynamicEntryType.DT_REL => DynamicEntryType.DT_RELSZ,
                DynamicEntryType.DT_JMPREL => DynamicEntryType.DT_PLTRELSZ,
                _ => null
            };

            // Compute entry size
            ulong relocationTableEntrySize = sectionHeader.EntrySize;
            if(relocationTableEntrySizeType != null && dynamicTableEntries.TryGetValue(relocationTableEntrySizeType.Value, out var relocationTableEntrySizes))
                relocationTableEntrySize = relocationTableEntrySizes.First();

            // Compute total table size
            ulong relocationTableSize = sectionHeader.Size;
            if(relocationTableSizeType != null && dynamicTableEntries.TryGetValue(relocationTableSizeType.Value, out var relocationTableSizes))
                relocationTableSize = relocationTableSizes.First();

            // TODO check what RELACOUNT does

            // Read section
            var sectionSpan = elfSpan.Slice((int)sectionHeader.FileOffset, (int)sectionHeader.Size);
            if(relocationTableEntryType == DynamicEntryType.DT_RELA)
                return RelocationAddendTableChunk.FromBytes(sectionSpan, (int)relocationTableEntrySize, (int)(relocationTableSize / relocationTableEntrySize));
            return RelocationTableChunk.FromBytes(sectionSpan, (int)relocationTableEntrySize, (int)(relocationTableSize / relocationTableEntrySize));
        }
    }
}