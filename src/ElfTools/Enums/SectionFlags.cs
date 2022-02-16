using System;

namespace ElfTools.Enums
{
    [Flags]
    public enum SectionFlags : ulong
    {
        None = 0,
        Writable = 1 << 0,
        Alloc = 1 << 1,
        Executable = 1 << 2,
        Merge = 1 << 4,
        Strings = 1 << 5,
        InfoLink = 1 << 6,
        LinkOrder = 1 << 7,
        OsNonConforming = 1 << 8,
        Group = 1 << 9,
        ThreadLocalStorage = 1 << 10,
        Compressed = 1 << 11,
        GnuRetain = 1 << 21,
        Ordered = 1 << 30,
        Exclude = 1ul << 31
    }
}