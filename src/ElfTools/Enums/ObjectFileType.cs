namespace ElfTools.Enums
{
    public enum ObjectFileType : ushort
    {
        None = 0,
        Relocatable = 1,
        Executable = 2,
        Shared = 3,
        Core = 4
    }
}