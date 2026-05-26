using System;

namespace WastelandRun.Cards
{
    /// <summary>Keyword flags on a card. Ethereal and Retain are mutually exclusive (enforced by OnValidate).</summary>
    [Flags]
    public enum CardKeyword
    {
        None    = 0,
        Exhaust = 1,
        Retain  = 2,
        Innate  = 4,
        Ethereal = 8
    }
}
