using System;
using System.Diagnostics.CodeAnalysis;

namespace ElfTools.Enums
{
    /// <summary>
    /// Flags for the info column in the symbol table.
    /// </summary>
    [SuppressMessage("ReSharper", "ShiftExpressionZeroLeftOperand")]
    [Flags]
    public enum SymbolInfo : byte
    {
        BindLocal = 0 << 4,
        BindGlobal = 1 << 4,
        BindWeak = 2 << 4,
        BindNum = 3 << 4,
        BindGnuUnique = 10 << 4,
        MaskBind = 0xf << 4,

        TypeNoType = 0,
        TypeObject = 1,
        TypeFunc = 2,
        TypeSection = 3,
        TypeFile = 4,
        TypeCommon = 5,
        TypeTls = 6,
        TypeGnuIFunc = 10,
        MaskType = 0xf
    }
}