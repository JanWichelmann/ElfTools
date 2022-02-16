namespace ElfTools.Enums
{
    public enum SectionType : uint
    {
        Unused = 0,
        ProgBits = 1,
        SymbolTable = 2,
        StringTable = 3,
        RelocationEntriesWithAddends = 4,
        SymbolHashTable = 5,
        Dynamic = 6,
        Notes = 7,
        NoBits = 8,
        RelocationEntriesWithoutAddends = 9,
        Reserved = 10,
        DynamicSymbols = 11,
        InitArray = 14,
        FiniArray = 15,
        PreInitArray = 16,
        SectionGroup = 17,
        SymTabShndx = 18,
        NumberDefinedTypes = 19,
        GnuAttributes = 0x6ffffff5,
        GnuHashTable = 0x6ffffff6,
        GnuLibraryList = 0x6ffffff7,
        DsoChecksum = 0x6ffffff8,
        GnuVersionDefinition = 0x6ffffffd,
        GnuVersionNeeds = 0x6ffffffe,
        GnuVersionSymbolTable = 0x6fffffff
    }
}