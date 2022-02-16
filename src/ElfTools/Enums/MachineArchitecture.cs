using System.Diagnostics.CodeAnalysis;

namespace ElfTools.Enums
{
    // Converted from libc/elf/elf.h
    //   Regex:       ^#define\s+(EM_[^\s]+)\s+([0-9]+)\s+/\*\s(.*?)\s\*/
    //   Replace:     /// <summary>\r\n/// \3\r\n/// </summary>\r\n\1 = \2,

    // TODO add proper names everywhere

    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public enum MachineArchitecture : ushort
    {
        /// <summary>
        /// No machine
        /// </summary>
        EM_NONE = 0,

        /// <summary>
        /// AT&T WE 32100
        /// </summary>
        EM_M32 = 1,

        /// <summary>
        /// SUN SPARC
        /// </summary>
        EM_SPARC = 2,

        /// <summary>
        /// Intel 80386
        /// </summary>
        EM_386 = 3,

        /// <summary>
        /// Motorola m68k family
        /// </summary>
        EM_68K = 4,

        /// <summary>
        /// Motorola m88k family
        /// </summary>
        EM_88K = 5,

        /// <summary>
        /// Intel MCU
        /// </summary>
        EM_IAMCU = 6,

        /// <summary>
        /// Intel 80860
        /// </summary>
        EM_860 = 7,

        /// <summary>
        /// MIPS R3000 big-endian
        /// </summary>
        EM_MIPS = 8,

        /// <summary>
        /// IBM System/370
        /// </summary>
        EM_S370 = 9,

        /// <summary>
        /// MIPS R3000 little-endian
        /// </summary>
        EM_MIPS_RS3_LE = 10,

        /// <summary>
        /// HPPA
        /// </summary>
        EM_PARISC = 15,

        /// <summary>
        /// Fujitsu VPP500
        /// </summary>
        EM_VPP500 = 17,

        /// <summary>
        /// Sun's "v8plus"
        /// </summary>
        EM_SPARC32PLUS = 18,

        /// <summary>
        /// Intel 80960
        /// </summary>
        EM_960 = 19,

        /// <summary>
        /// PowerPC
        /// </summary>
        EM_PPC = 20,

        /// <summary>
        /// PowerPC 64-bit
        /// </summary>
        EM_PPC64 = 21,

        /// <summary>
        /// IBM S390
        /// </summary>
        EM_S390 = 22,

        /// <summary>
        /// IBM SPU/SPC
        /// </summary>
        EM_SPU = 23,

        /// <summary>
        /// NEC V800 series
        /// </summary>
        EM_V800 = 36,

        /// <summary>
        /// Fujitsu FR20
        /// </summary>
        EM_FR20 = 37,

        /// <summary>
        /// TRW RH-32
        /// </summary>
        EM_RH32 = 38,

        /// <summary>
        /// Motorola RCE
        /// </summary>
        EM_RCE = 39,

        /// <summary>
        /// ARM
        /// </summary>
        EM_ARM = 40,

        /// <summary>
        /// Digital Alpha
        /// </summary>
        EM_FAKE_ALPHA = 41,

        /// <summary>
        /// Hitachi SH
        /// </summary>
        EM_SH = 42,

        /// <summary>
        /// SPARC v9 64-bit
        /// </summary>
        EM_SPARCV9 = 43,

        /// <summary>
        /// Siemens Tricore
        /// </summary>
        EM_TRICORE = 44,

        /// <summary>
        /// Argonaut RISC Core
        /// </summary>
        EM_ARC = 45,

        /// <summary>
        /// Hitachi H8/300
        /// </summary>
        EM_H8_300 = 46,

        /// <summary>
        /// Hitachi H8/300H
        /// </summary>
        EM_H8_300H = 47,

        /// <summary>
        /// Hitachi H8S
        /// </summary>
        EM_H8S = 48,

        /// <summary>
        /// Hitachi H8/500
        /// </summary>
        EM_H8_500 = 49,

        /// <summary>
        /// Intel Merced
        /// </summary>
        EM_IA_64 = 50,

        /// <summary>
        /// Stanford MIPS-X
        /// </summary>
        EM_MIPS_X = 51,

        /// <summary>
        /// Motorola Coldfire
        /// </summary>
        EM_COLDFIRE = 52,

        /// <summary>
        /// Motorola M68HC12
        /// </summary>
        EM_68HC12 = 53,

        /// <summary>
        /// Fujitsu MMA Multimedia Accelerator
        /// </summary>
        EM_MMA = 54,

        /// <summary>
        /// Siemens PCP
        /// </summary>
        EM_PCP = 55,

        /// <summary>
        /// Sony nCPU embeeded RISC
        /// </summary>
        EM_NCPU = 56,

        /// <summary>
        /// Denso NDR1 microprocessor
        /// </summary>
        EM_NDR1 = 57,

        /// <summary>
        /// Motorola Start*Core processor
        /// </summary>
        EM_STARCORE = 58,

        /// <summary>
        /// Toyota ME16 processor
        /// </summary>
        EM_ME16 = 59,

        /// <summary>
        /// STMicroelectronic ST100 processor
        /// </summary>
        EM_ST100 = 60,

        /// <summary>
        /// Advanced Logic Corp. Tinyj emb.fam
        /// </summary>
        EM_TINYJ = 61,

        /// <summary>
        /// AMD x86-64 architecture
        /// </summary>
        EM_X86_64 = 62,

        /// <summary>
        /// Sony DSP Processor
        /// </summary>
        EM_PDSP = 63,

        /// <summary>
        /// Digital PDP-10
        /// </summary>
        EM_PDP10 = 64,

        /// <summary>
        /// Digital PDP-11
        /// </summary>
        EM_PDP11 = 65,

        /// <summary>
        /// Siemens FX66 microcontroller
        /// </summary>
        EM_FX66 = 66,

        /// <summary>
        /// STMicroelectronics ST9+ 8/16 mc
        /// </summary>
        EM_ST9PLUS = 67,

        /// <summary>
        /// STmicroelectronics ST7 8 bit mc
        /// </summary>
        EM_ST7 = 68,

        /// <summary>
        /// Motorola MC68HC16 microcontroller
        /// </summary>
        EM_68HC16 = 69,

        /// <summary>
        /// Motorola MC68HC11 microcontroller
        /// </summary>
        EM_68HC11 = 70,

        /// <summary>
        /// Motorola MC68HC08 microcontroller
        /// </summary>
        EM_68HC08 = 71,

        /// <summary>
        /// Motorola MC68HC05 microcontroller
        /// </summary>
        EM_68HC05 = 72,

        /// <summary>
        /// Silicon Graphics SVx
        /// </summary>
        EM_SVX = 73,

        /// <summary>
        /// STMicroelectronics ST19 8 bit mc
        /// </summary>
        EM_ST19 = 74,

        /// <summary>
        /// Digital VAX
        /// </summary>
        EM_VAX = 75,

        /// <summary>
        /// Axis Communications 32-bit emb.proc
        /// </summary>
        EM_CRIS = 76,

        /// <summary>
        /// Infineon Technologies 32-bit emb.proc
        /// </summary>
        EM_JAVELIN = 77,

        /// <summary>
        /// Element 14 64-bit DSP Processor
        /// </summary>
        EM_FIREPATH = 78,

        /// <summary>
        /// LSI Logic 16-bit DSP Processor
        /// </summary>
        EM_ZSP = 79,

        /// <summary>
        /// Donald Knuth's educational 64-bit proc
        /// </summary>
        EM_MMIX = 80,

        /// <summary>
        /// Harvard University machine-independent object files
        /// </summary>
        EM_HUANY = 81,

        /// <summary>
        /// SiTera Prism
        /// </summary>
        EM_PRISM = 82,

        /// <summary>
        /// Atmel AVR 8-bit microcontroller
        /// </summary>
        EM_AVR = 83,

        /// <summary>
        /// Fujitsu FR30
        /// </summary>
        EM_FR30 = 84,

        /// <summary>
        /// Mitsubishi D10V
        /// </summary>
        EM_D10V = 85,

        /// <summary>
        /// Mitsubishi D30V
        /// </summary>
        EM_D30V = 86,

        /// <summary>
        /// NEC v850
        /// </summary>
        EM_V850 = 87,

        /// <summary>
        /// Mitsubishi M32R
        /// </summary>
        EM_M32R = 88,

        /// <summary>
        /// Matsushita MN10300
        /// </summary>
        EM_MN10300 = 89,

        /// <summary>
        /// Matsushita MN10200
        /// </summary>
        EM_MN10200 = 90,

        /// <summary>
        /// picoJava
        /// </summary>
        EM_PJ = 91,

        /// <summary>
        /// OpenRISC 32-bit embedded processor
        /// </summary>
        EM_OPENRISC = 92,

        /// <summary>
        /// ARC International ARCompact
        /// </summary>
        EM_ARC_COMPACT = 93,

        /// <summary>
        /// Tensilica Xtensa Architecture
        /// </summary>
        EM_XTENSA = 94,

        /// <summary>
        /// Alphamosaic VideoCore
        /// </summary>
        EM_VIDEOCORE = 95,

        /// <summary>
        /// Thompson Multimedia General Purpose Proc
        /// </summary>
        EM_TMM_GPP = 96,

        /// <summary>
        /// National Semi. 32000
        /// </summary>
        EM_NS32K = 97,

        /// <summary>
        /// Tenor Network TPC
        /// </summary>
        EM_TPC = 98,

        /// <summary>
        /// Trebia SNP 1000
        /// </summary>
        EM_SNP1K = 99,

        /// <summary>
        /// STMicroelectronics ST200
        /// </summary>
        EM_ST200 = 100,

        /// <summary>
        /// Ubicom IP2xxx
        /// </summary>
        EM_IP2K = 101,

        /// <summary>
        /// MAX processor
        /// </summary>
        EM_MAX = 102,

        /// <summary>
        /// National Semi. CompactRISC
        /// </summary>
        EM_CR = 103,

        /// <summary>
        /// Fujitsu F2MC16
        /// </summary>
        EM_F2MC16 = 104,

        /// <summary>
        /// Texas Instruments msp430
        /// </summary>
        EM_MSP430 = 105,

        /// <summary>
        /// Analog Devices Blackfin DSP
        /// </summary>
        EM_BLACKFIN = 106,

        /// <summary>
        /// Seiko Epson S1C33 family
        /// </summary>
        EM_SE_C33 = 107,

        /// <summary>
        /// Sharp embedded microprocessor
        /// </summary>
        EM_SEP = 108,

        /// <summary>
        /// Arca RISC
        /// </summary>
        EM_ARCA = 109,

        /// <summary>
        /// PKU-Unity & MPRC Peking Uni. mc series
        /// </summary>
        EM_UNICORE = 110,

        /// <summary>
        /// eXcess configurable cpu
        /// </summary>
        EM_EXCESS = 111,

        /// <summary>
        /// Icera Semi. Deep Execution Processor
        /// </summary>
        EM_DXP = 112,

        /// <summary>
        /// Altera Nios II
        /// </summary>
        EM_ALTERA_NIOS2 = 113,

        /// <summary>
        /// National Semi. CompactRISC CRX
        /// </summary>
        EM_CRX = 114,

        /// <summary>
        /// Motorola XGATE
        /// </summary>
        EM_XGATE = 115,

        /// <summary>
        /// Infineon C16x/XC16x
        /// </summary>
        EM_C166 = 116,

        /// <summary>
        /// Renesas M16C
        /// </summary>
        EM_M16C = 117,

        /// <summary>
        /// Microchip Technology dsPIC30F
        /// </summary>
        EM_DSPIC30F = 118,

        /// <summary>
        /// Freescale Communication Engine RISC
        /// </summary>
        EM_CE = 119,

        /// <summary>
        /// Renesas M32C
        /// </summary>
        EM_M32C = 120,

        /// <summary>
        /// Altium TSK3000
        /// </summary>
        EM_TSK3000 = 131,

        /// <summary>
        /// Freescale RS08
        /// </summary>
        EM_RS08 = 132,

        /// <summary>
        /// Analog Devices SHARC family
        /// </summary>
        EM_SHARC = 133,

        /// <summary>
        /// Cyan Technology eCOG2
        /// </summary>
        EM_ECOG2 = 134,

        /// <summary>
        /// Sunplus S+core7 RISC
        /// </summary>
        EM_SCORE7 = 135,

        /// <summary>
        /// New Japan Radio (NJR) 24-bit DSP
        /// </summary>
        EM_DSP24 = 136,

        /// <summary>
        /// Broadcom VideoCore III
        /// </summary>
        EM_VIDEOCORE3 = 137,

        /// <summary>
        /// RISC for Lattice FPGA
        /// </summary>
        EM_LATTICEMICO32 = 138,

        /// <summary>
        /// Seiko Epson C17
        /// </summary>
        EM_SE_C17 = 139,

        /// <summary>
        /// Texas Instruments TMS320C6000 DSP
        /// </summary>
        EM_TI_C6000 = 140,

        /// <summary>
        /// Texas Instruments TMS320C2000 DSP
        /// </summary>
        EM_TI_C2000 = 141,

        /// <summary>
        /// Texas Instruments TMS320C55x DSP
        /// </summary>
        EM_TI_C5500 = 142,

        /// <summary>
        /// Texas Instruments App. Specific RISC
        /// </summary>
        EM_TI_ARP32 = 143,

        /// <summary>
        /// Texas Instruments Prog. Realtime Unit
        /// </summary>
        EM_TI_PRU = 144,

        /// <summary>
        /// STMicroelectronics 64bit VLIW DSP
        /// </summary>
        EM_MMDSP_PLUS = 160,

        /// <summary>
        /// Cypress M8C
        /// </summary>
        EM_CYPRESS_M8C = 161,

        /// <summary>
        /// Renesas R32C
        /// </summary>
        EM_R32C = 162,

        /// <summary>
        /// NXP Semi. TriMedia
        /// </summary>
        EM_TRIMEDIA = 163,

        /// <summary>
        /// QUALCOMM DSP6
        /// </summary>
        EM_QDSP6 = 164,

        /// <summary>
        /// Intel 8051 and variants
        /// </summary>
        EM_8051 = 165,

        /// <summary>
        /// STMicroelectronics STxP7x
        /// </summary>
        EM_STXP7X = 166,

        /// <summary>
        /// Andes Tech. compact code emb. RISC
        /// </summary>
        EM_NDS32 = 167,

        /// <summary>
        /// Cyan Technology eCOG1X
        /// </summary>
        EM_ECOG1X = 168,

        /// <summary>
        /// Dallas Semi. MAXQ30 mc
        /// </summary>
        EM_MAXQ30 = 169,

        /// <summary>
        /// New Japan Radio (NJR) 16-bit DSP
        /// </summary>
        EM_XIMO16 = 170,

        /// <summary>
        /// M2000 Reconfigurable RISC
        /// </summary>
        EM_MANIK = 171,

        /// <summary>
        /// Cray NV2 vector architecture
        /// </summary>
        EM_CRAYNV2 = 172,

        /// <summary>
        /// Renesas RX
        /// </summary>
        EM_RX = 173,

        /// <summary>
        /// Imagination Tech. META
        /// </summary>
        EM_METAG = 174,

        /// <summary>
        /// MCST Elbrus
        /// </summary>
        EM_MCST_ELBRUS = 175,

        /// <summary>
        /// Cyan Technology eCOG16
        /// </summary>
        EM_ECOG16 = 176,

        /// <summary>
        /// National Semi. CompactRISC CR16
        /// </summary>
        EM_CR16 = 177,

        /// <summary>
        /// Freescale Extended Time Processing Unit
        /// </summary>
        EM_ETPU = 178,

        /// <summary>
        /// Infineon Tech. SLE9X
        /// </summary>
        EM_SLE9X = 179,

        /// <summary>
        /// Intel L10M
        /// </summary>
        EM_L10M = 180,

        /// <summary>
        /// Intel K10M
        /// </summary>
        EM_K10M = 181,

        /// <summary>
        /// ARM AARCH64
        /// </summary>
        EM_AARCH64 = 183,

        /// <summary>
        /// Amtel 32-bit microprocessor
        /// </summary>
        EM_AVR32 = 185,

        /// <summary>
        /// STMicroelectronics STM8
        /// </summary>
        EM_STM8 = 186,

        /// <summary>
        /// Tilera TILE64
        /// </summary>
        EM_TILE64 = 187,

        /// <summary>
        /// Tilera TILEPro
        /// </summary>
        EM_TILEPRO = 188,

        /// <summary>
        /// Xilinx MicroBlaze
        /// </summary>
        EM_MICROBLAZE = 189,

        /// <summary>
        /// NVIDIA CUDA
        /// </summary>
        EM_CUDA = 190,

        /// <summary>
        /// Tilera TILE-Gx
        /// </summary>
        EM_TILEGX = 191,

        /// <summary>
        /// CloudShield
        /// </summary>
        EM_CLOUDSHIELD = 192,

        /// <summary>
        /// KIPO-KAIST Core-A 1st gen.
        /// </summary>
        EM_COREA_1ST = 193,

        /// <summary>
        /// KIPO-KAIST Core-A 2nd gen.
        /// </summary>
        EM_COREA_2ND = 194,

        /// <summary>
        /// Synopsys ARCv2 ISA.
        /// </summary>
        EM_ARCV2 = 195,

        /// <summary>
        /// Open8 RISC
        /// </summary>
        EM_OPEN8 = 196,

        /// <summary>
        /// Renesas RL78
        /// </summary>
        EM_RL78 = 197,

        /// <summary>
        /// Broadcom VideoCore V
        /// </summary>
        EM_VIDEOCORE5 = 198,

        /// <summary>
        /// Renesas 78KOR
        /// </summary>
        EM_78KOR = 199,

        /// <summary>
        /// Freescale 56800EX DSC
        /// </summary>
        EM_56800EX = 200,

        /// <summary>
        /// Beyond BA1
        /// </summary>
        EM_BA1 = 201,

        /// <summary>
        /// Beyond BA2
        /// </summary>
        EM_BA2 = 202,

        /// <summary>
        /// XMOS xCORE
        /// </summary>
        EM_XCORE = 203,

        /// <summary>
        /// Microchip 8-bit PIC(r)
        /// </summary>
        EM_MCHP_PIC = 204,

        /// <summary>
        /// Intel Graphics Technology
        /// </summary>
        EM_INTELGT = 205,

        /// <summary>
        /// KM211 KM32
        /// </summary>
        EM_KM32 = 210,

        /// <summary>
        /// KM211 KMX32
        /// </summary>
        EM_KMX32 = 211,

        /// <summary>
        /// KM211 KMX16
        /// </summary>
        EM_EMX16 = 212,

        /// <summary>
        /// KM211 KMX8
        /// </summary>
        EM_EMX8 = 213,

        /// <summary>
        /// KM211 KVARC
        /// </summary>
        EM_KVARC = 214,

        /// <summary>
        /// Paneve CDP
        /// </summary>
        EM_CDP = 215,

        /// <summary>
        /// Cognitive Smart Memory Processor
        /// </summary>
        EM_COGE = 216,

        /// <summary>
        /// Bluechip CoolEngine
        /// </summary>
        EM_COOL = 217,

        /// <summary>
        /// Nanoradio Optimized RISC
        /// </summary>
        EM_NORC = 218,

        /// <summary>
        /// CSR Kalimba
        /// </summary>
        EM_CSR_KALIMBA = 219,

        /// <summary>
        /// Zilog Z80
        /// </summary>
        EM_Z80 = 220,

        /// <summary>
        /// Controls and Data Services VISIUMcore
        /// </summary>
        EM_VISIUM = 221,

        /// <summary>
        /// FTDI Chip FT32
        /// </summary>
        EM_FT32 = 222,

        /// <summary>
        /// Moxie processor
        /// </summary>
        EM_MOXIE = 223,

        /// <summary>
        /// AMD GPU
        /// </summary>
        EM_AMDGPU = 224,

        /// <summary>
        /// RISC-V
        /// </summary>
        EM_RISCV = 243,

        /// <summary>
        /// Linux BPF -- in-kernel virtual machine
        /// </summary>
        EM_BPF = 247,

        /// <summary>
        /// C-SKY
        /// </summary>
        EM_CSKY = 252
    }
}