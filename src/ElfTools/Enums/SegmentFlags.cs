using System;

namespace ElfTools.Enums
{
    [Flags]
    public enum SegmentFlags : uint
    {
        None = 0,
        Executable = 1 << 0,
        Writable = 1 << 1,
        Readable = 1 << 2
    }
}