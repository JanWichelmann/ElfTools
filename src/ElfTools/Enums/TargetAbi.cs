namespace ElfTools.Enums
{
    public enum TargetAbi : byte
    {
        None = 0,
        SystemV = 0,
        HpUx = 1,
        NetBsd = 2,
        Gnu = 3,
        Linux = 3,
        Solaris = 6,
        Aix = 7,
        Irix = 8,
        FreeBsd = 9,
        Tru64 = 10,
        Modesto = 11,
        OpenBsd = 12,
        ArmEabi = 64,
        Arm = 97,
        Standalone = 255
    }
}