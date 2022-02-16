namespace ElfTools.Enums
{
    public enum SegmentType : uint
    {
        Unused = 0,
        Load = 1,
        Dynamic = 2,
        Interpreter = 3,
        Note = 4,
        Reserved = 5,
        ProgramHeaderTable = 6,
        ThreadLocalStorage = 7,
        NumberOfTypes = 8,
        GnuEhFrame = 0x6474e550,
        GnuStack = 0x6474e551,
        GnuReadOnlyAfterRelocation = 0x6474e552,
        GnuProperty = 0x6474e553
    }
}