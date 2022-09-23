using System;
using System.Collections.Generic;
using ElfTools.Chunks;
using ElfTools.Enums;
using ElfTools.Instrumentation;

namespace ElfTools.Utilities
{
    /// <summary>
    /// Provides functionality for querying an ELF file's symbol table.
    /// </summary>
    public class SymbolTableParser
    {
        private readonly Dictionary<ulong, string> _symbols = new();

        public SymbolTableParser(ElfFile elf, int symbolTableSectionIndex)
        {
            if(symbolTableSectionIndex < 0 || symbolTableSectionIndex >= elf.SectionHeaderTable.SectionHeaders.Count)
                return;

            // Get section headers
            var symbolTableHeader = elf.SectionHeaderTable.SectionHeaders[symbolTableSectionIndex];
            int stringTableSectionIndex = (int)symbolTableHeader.Link;
            if(stringTableSectionIndex < 0 || stringTableSectionIndex >= elf.SectionHeaderTable.SectionHeaders.Count)
                return;
            var stringTableHeader = elf.SectionHeaderTable.SectionHeaders[stringTableSectionIndex];
            if(stringTableHeader.Type != SectionType.StringTable)
                return;

            // Get chunks
            var symbolTableSectionChunkIndex = elf.GetChunkIndexForOffset(symbolTableHeader.FileOffset);
            if(symbolTableSectionChunkIndex == null || elf.Chunks[symbolTableSectionChunkIndex.Value.chunkIndex] is not SymbolTableChunk symbolTableChunk)
                throw new Exception("Could not resolve symbol table section index to section chunk.");

            var stringTableSectionChunkIndex = elf.GetChunkIndexForOffset(stringTableHeader.FileOffset);
            if(stringTableSectionChunkIndex == null || elf.Chunks[stringTableSectionChunkIndex.Value.chunkIndex] is not StringTableChunk stringTableChunk)
                throw new Exception("Could not resolve string table section index to section chunk.");

            // Load symbols
            foreach(var symbol in symbolTableChunk.Entries)
            {
                _symbols.TryAdd(symbol.Value, stringTableChunk.GetString(symbol.Name));
            }
        }

        /// <summary>
        /// Looks up the symbol for the given offset and returns its name (or null if it is not found).
        /// </summary>
        /// <param name="offset">Offset to look up.</param>
        public string? QuerySymbol(ulong offset)
        {
            if(_symbols.TryGetValue(offset, out string? symbol))
                return symbol;

            return null;
        }
    }
}