using System.Diagnostics.CodeAnalysis;

namespace ElfTools.Enums
{
    // Converted from libc/elf/elf.h
    //   Regex:       ^#define\s+(DT_[^\s]+)\s+(0?x?[0-9a-f]+)\s+/\*\s(.*?)\s?\*/
    //   Replace:     /// <summary>\r\n/// \3\r\n/// </summary>\r\n\1 = \2,

    [SuppressMessage("ReSharper", "InconsistentNaming")]
    [SuppressMessage("ReSharper", "IdentifierTypo")]
    public enum DynamicEntryType : long
    {
        /// <summary>
        /// Marks end of dynamic section
        /// </summary>
        DT_NULL = 0,

        /// <summary>
        /// Name of needed library
        /// </summary>
        DT_NEEDED = 1,

        /// <summary>
        /// Size in bytes of PLT relocs
        /// </summary>
        DT_PLTRELSZ = 2,

        /// <summary>
        /// Processor defined value
        /// </summary>
        DT_PLTGOT = 3,

        /// <summary>
        /// Address of symbol hash table
        /// </summary>
        DT_HASH = 4,

        /// <summary>
        /// Address of string table
        /// </summary>
        DT_STRTAB = 5,

        /// <summary>
        /// Address of symbol table
        /// </summary>
        DT_SYMTAB = 6,

        /// <summary>
        /// Address of Rela relocs
        /// </summary>
        DT_RELA = 7,

        /// <summary>
        /// Total size of Rela relocs
        /// </summary>
        DT_RELASZ = 8,

        /// <summary>
        /// Size of one Rela reloc
        /// </summary>
        DT_RELAENT = 9,

        /// <summary>
        /// Size of string table
        /// </summary>
        DT_STRSZ = 10,

        /// <summary>
        /// Size of one symbol table entry
        /// </summary>
        DT_SYMENT = 11,

        /// <summary>
        /// Address of init function
        /// </summary>
        DT_INIT = 12,

        /// <summary>
        /// Address of termination function
        /// </summary>
        DT_FINI = 13,

        /// <summary>
        /// Name of shared object
        /// </summary>
        DT_SONAME = 14,

        /// <summary>
        /// Library search path (deprecated)
        /// </summary>
        DT_RPATH = 15,

        /// <summary>
        /// Start symbol search here
        /// </summary>
        DT_SYMBOLIC = 16,

        /// <summary>
        /// Address of Rel relocs
        /// </summary>
        DT_REL = 17,

        /// <summary>
        /// Total size of Rel relocs
        /// </summary>
        DT_RELSZ = 18,

        /// <summary>
        /// Size of one Rel reloc
        /// </summary>
        DT_RELENT = 19,

        /// <summary>
        /// Type of reloc in PLT
        /// </summary>
        DT_PLTREL = 20,

        /// <summary>
        /// For debugging; unspecified
        /// </summary>
        DT_DEBUG = 21,

        /// <summary>
        /// Reloc might modify .text
        /// </summary>
        DT_TEXTREL = 22,

        /// <summary>
        /// Address of PLT relocs
        /// </summary>
        DT_JMPREL = 23,

        /// <summary>
        /// Process relocations of object
        /// </summary>
        DT_BIND_NOW = 24,

        /// <summary>
        /// Array with addresses of init fct
        /// </summary>
        DT_INIT_ARRAY = 25,

        /// <summary>
        /// Array with addresses of fini fct
        /// </summary>
        DT_FINI_ARRAY = 26,

        /// <summary>
        /// Size in bytes of DT_INIT_ARRAY
        /// </summary>
        DT_INIT_ARRAYSZ = 27,

        /// <summary>
        /// Size in bytes of DT_FINI_ARRAY
        /// </summary>
        DT_FINI_ARRAYSZ = 28,

        /// <summary>
        /// Library search path
        /// </summary>
        DT_RUNPATH = 29,

        /// <summary>
        /// Flags for the object being loaded
        /// </summary>
        DT_FLAGS = 30,

        /// <summary>
        /// Start of encoded range
        /// </summary>
        DT_ENCODING = 32,

        /// <summary>
        /// Array with addresses of preinit fct
        /// </summary>
        DT_PREINIT_ARRAY = 32,

        /// <summary>
        /// size in bytes of DT_PREINIT_ARRAY
        /// </summary>
        DT_PREINIT_ARRAYSZ = 33,

        /// <summary>
        /// Address of SYMTAB_SHNDX section
        /// </summary>
        DT_SYMTAB_SHNDX = 34,

        /// <summary>
        /// Start of OS-specific
        /// </summary>
        DT_LOOS = 0x6000000d,

        /// <summary>
        /// End of OS-specific
        /// </summary>
        DT_HIOS = 0x6ffff000,

        /// <summary>
        /// Start of processor-specific
        /// </summary>
        DT_LOPROC = 0x70000000,

        /// <summary>
        /// End of processor-specific
        /// </summary>
        DT_HIPROC = 0x7fffffff,

        /// <summary>
        /// 
        /// </summary>
        DT_VALRNGLO = 0x6ffffd00,

        /// <summary>
        /// Prelinking timestamp
        /// </summary>
        DT_GNU_PRELINKED = 0x6ffffdf5,

        /// <summary>
        /// Size of conflict section
        /// </summary>
        DT_GNU_CONFLICTSZ = 0x6ffffdf6,

        /// <summary>
        /// Size of library list
        /// </summary>
        DT_GNU_LIBLISTSZ = 0x6ffffdf7,

        /// <summary>
        /// 
        /// </summary>
        DT_CHECKSUM = 0x6ffffdf8,

        /// <summary>
        /// 
        /// </summary>
        DT_PLTPADSZ = 0x6ffffdf9,

        /// <summary>
        /// 
        /// </summary>
        DT_MOVEENT = 0x6ffffdfa,

        /// <summary>
        /// 
        /// </summary>
        DT_MOVESZ = 0x6ffffdfb,

        /// <summary>
        /// Feature selection (DTF_*). 
        /// </summary>
        DT_FEATURE_1 = 0x6ffffdfc,

        /// <summary>
        /// Flags for DT_* entries, effecting the following DT_* entry. 
        /// </summary>
        DT_POSFLAG_1 = 0x6ffffdfd,

        /// <summary>
        /// Size of syminfo table (in bytes)
        /// </summary>
        DT_SYMINSZ = 0x6ffffdfe,

        /// <summary>
        /// Entry size of syminfo
        /// </summary>
        DT_SYMINENT = 0x6ffffdff,

        /// <summary>
        /// 
        /// </summary>
        DT_VALRNGHI = 0x6ffffdff,

        /// <summary>
        /// 
        /// </summary>
        DT_ADDRRNGLO = 0x6ffffe00,

        /// <summary>
        /// GNU-style hash table. 
        /// </summary>
        DT_GNU_HASH = 0x6ffffef5,

        /// <summary>
        /// 
        /// </summary>
        DT_TLSDESC_PLT = 0x6ffffef6,

        /// <summary>
        /// 
        /// </summary>
        DT_TLSDESC_GOT = 0x6ffffef7,

        /// <summary>
        /// Start of conflict section
        /// </summary>
        DT_GNU_CONFLICT = 0x6ffffef8,

        /// <summary>
        /// Library list
        /// </summary>
        DT_GNU_LIBLIST = 0x6ffffef9,

        /// <summary>
        /// Configuration information. 
        /// </summary>
        DT_CONFIG = 0x6ffffefa,

        /// <summary>
        /// Dependency auditing. 
        /// </summary>
        DT_DEPAUDIT = 0x6ffffefb,

        /// <summary>
        /// Object auditing. 
        /// </summary>
        DT_AUDIT = 0x6ffffefc,

        /// <summary>
        /// PLT padding. 
        /// </summary>
        DT_PLTPAD = 0x6ffffefd,

        /// <summary>
        /// Move table. 
        /// </summary>
        DT_MOVETAB = 0x6ffffefe,

        /// <summary>
        /// Syminfo table. 
        /// </summary>
        DT_SYMINFO = 0x6ffffeff,

        /// <summary>
        /// 
        /// </summary>
        DT_ADDRRNGHI = 0x6ffffeff,

        /// <summary>
        /// 
        /// </summary>
        DT_VERSYM = 0x6ffffff0,

        /// <summary>
        /// 
        /// </summary>
        DT_RELACOUNT = 0x6ffffff9,

        /// <summary>
        /// 
        /// </summary>
        DT_RELCOUNT = 0x6ffffffa,

        /// <summary>
        /// State flags, see DF_1_* below. 
        /// </summary>
        DT_FLAGS_1 = 0x6ffffffb,

        /// <summary>
        /// Address of version definition table
        /// </summary>
        DT_VERDEF = 0x6ffffffc,

        /// <summary>
        /// Number of version definitions
        /// </summary>
        DT_VERDEFNUM = 0x6ffffffd,

        /// <summary>
        /// Address of table with needed versions
        /// </summary>
        DT_VERNEED = 0x6ffffffe,

        /// <summary>
        /// Number of needed versions
        /// </summary>
        DT_VERNEEDNUM = 0x6fffffff,

        /// <summary>
        /// Shared object to load before self
        /// </summary>
        DT_AUXILIARY = 0x7ffffffd,

        /// <summary>
        /// Shared object to get values from
        /// </summary>
        DT_FILTER = 0x7fffffff,
    }
}