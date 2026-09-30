// src/PropTraderTools/PttTickMath.cs
// B180: Pure-math tick-rounding helper. No NT8 dependencies.
// Extracted as a standalone class so tests can call it without triggering
// CopyEngine.cctor (which requires the NT8/AgileDotNet host to be running).
// CopyEngine.RoundToTickTestable delegates here for API consistency.
// CYC=2: (1) tickSize guard, (2) expression.
// JS-021: no lock. JS-001: no throw. Zero alloc. ASCII-only.
using System;

namespace PropTraderTools
{
    internal static class PttTickMath
    {
        // NT8 tick-rounding: AwayFromZero matches NT8 RoundToTickSize semantics
        // (mirrors TradeCopierPanel.AlignToTick pattern).
        // CYC=2: (1) tick guard, (2) expression.
        internal static double RoundToTick(double raw, double tick) =>
            tick > 0.0 ? Math.Round(raw / tick, MidpointRounding.AwayFromZero) * tick : raw;
    }
}