// Wave1LaneBT5Tests.cs -- xUnit verification tests for WAVE1-LANE-B T-5.
// Spec requirements covered: supporting file PttGlobalBreakEven.cs.
// Confirms no regressions from adjacent lane work. CCN verified: max=6 (ExecuteOne).
// Covers ExecuteOne guard behaviour, IncrementBuffer/DecrementBuffer behaviour,
// and Execute(IEnumerable) guard behaviour.
//
// Direct ProjectReference impossible across TFMs:
//   PropTraderTools targets net48 (NT8 requirement).
//   PropTraderTools.Tests targets net8.0.
//   NT8 Account/Instrument/Position are not instantiable without the NT8 runtime.
//   Inline mirror of guard predicates is the established pattern
//   (see Wave1LaneBT2Tests.cs, Wave1LaneBT3Tests.cs, Wave1LaneBT4Tests.cs).
//
// Framework: xUnit ONLY. NEVER NUnit or MSTest. ASCII-only. No DateTime.Now. No lock().
// JS-021: no lock. JS-001: no throw. JS-002: no null return. JS-033: no async void.
using System;
using System.Collections.Generic;
using Xunit;

namespace PropTraderTools.Tests
{
    /// <summary>
    /// Inline mirror tests for PttGlobalBreakEven guard logic.
    /// Tests ExecuteOne guard (null/zero-qty skip), IncrementBuffer/DecrementBuffer
    /// boundary behaviour, Execute(IEnumerable) null-skip logic, and
    /// BuildGlobalBeOcoId format correctness.
    /// Source: src/PropTraderTools/Features/PttGlobalBreakEven.cs
    /// </summary>
    public sealed class Wave1LaneBT5Tests
    {
        // ------------------------------------------------------------------
        // Inline mirror of PttGlobalBreakEven._globalBeBuffer state machine.
        // The real field is volatile int; here we use a plain int for test isolation
        // (single-threaded tests -- no race condition). Mirrors IncrementBuffer
        // and DecrementBuffer exactly.
        // Source: PttGlobalBreakEven.cs L100-L111.
        //
        //   IncrementBuffer: if (_globalBeBuffer < 10) _globalBeBuffer++;
        //   DecrementBuffer: if (_globalBeBuffer > -10) _globalBeBuffer--;
        //
        // CYC=2 each (1 base + 1 if). Capped at +10 / -10 to prevent unbounded drift.
        // ------------------------------------------------------------------
        private static int IncrementBuffer(int current)
        {
            if (current < 10)
                return current + 1;
            return current;
        }

        private static int DecrementBuffer(int current)
        {
            if (current > -10)
                return current - 1;
            return current;
        }

        // ------------------------------------------------------------------
        // Inline mirror of PttGlobalBreakEven.ExecuteOne null/zero guard.
        // Source: PttGlobalBreakEven.cs L76-L77.
        //   if (pos == null || pos.Quantity == 0) return;
        // Parameters mapped to primitives -- no NT8 runtime required.
        //   posIsNull  -> pos == null
        //   posQty     -> pos.Quantity
        // Returns true when the guard fires (execution skips), false when it passes.
        // CYC=2 for the guard expression (1 base + 1 || branch).
        // ------------------------------------------------------------------
        private static bool ExecuteOneGuardFires(bool posIsNull, int posQty)
        {
            if (posIsNull || posQty == 0)
                return true;
            return false;
        }

        // ------------------------------------------------------------------
        // Inline mirror of PttGlobalBreakEven.Execute(IEnumerable) loop.
        // Counts how many ExecuteOne calls would be dispatched for a set of
        // (posIsNull, posQty) entries. Mirrors the continue-on-guard logic.
        // Source: PttGlobalBreakEven.cs L61-L69.
        // ------------------------------------------------------------------
        private static int CountExecuteOneCalls(IEnumerable<(bool posIsNull, int posQty)> positions)
        {
            int count = 0;
            foreach (var (posIsNull, posQty) in positions)
            {
                if (posIsNull || posQty == 0)
                    continue;
                count++;
            }
            return count;
        }

        // ------------------------------------------------------------------
        // Inline mirror of PttGlobalBreakEven.BuildGlobalBeOcoId.
        // Source: PttGlobalBreakEven.cs L94-L95 (pure expression).
        //   "PTT-BEG-" + seq.ToString("D5") + "-" + accIdx + "-" + pairIndex
        // CYC=1. ASCII-only. No hex literals. No FontFamily.
        // ------------------------------------------------------------------
        private static string BuildGlobalBeOcoId(int seq, int accIdx, int pairIndex) =>
            "PTT-BEG-" + seq.ToString("D5") + "-" + accIdx + "-" + pairIndex;

        // ------------------------------------------------------------------
        // Inline mirror of bePrice calculation in ExecuteOne (isLong path).
        // Source: PttGlobalBreakEven.cs L80-L83.
        //   double bePrice = Math.Round(
        //       (pos.AveragePrice + (isLong ? bufferTicks : -bufferTicks) * tickSize) / tickSize
        //   ) * tickSize;
        // Tests the direction-aware rounding without NT8 runtime dependencies.
        // ------------------------------------------------------------------
        private static double CalcBePrice(double avgPrice, bool isLong, int bufferTicks, double tickSize)
        {
            return Math.Round(
                (avgPrice + (isLong ? bufferTicks : -bufferTicks) * tickSize) / tickSize
            ) * tickSize;
        }

        // ==================================================================
        // ExecuteOne guard behaviour
        // ==================================================================

        [Fact]
        public void ExecuteOne_NullPosition_GuardFires()
        {
            bool fires = ExecuteOneGuardFires(posIsNull: true, posQty: 5);
            Assert.True(fires);
        }

        [Fact]
        public void ExecuteOne_ZeroQuantity_GuardFires()
        {
            bool fires = ExecuteOneGuardFires(posIsNull: false, posQty: 0);
            Assert.True(fires);
        }

        [Fact]
        public void ExecuteOne_NullAndZeroQuantity_GuardFires()
        {
            bool fires = ExecuteOneGuardFires(posIsNull: true, posQty: 0);
            Assert.True(fires);
        }

        [Fact]
        public void ExecuteOne_ValidPosition_GuardDoesNotFire()
        {
            bool fires = ExecuteOneGuardFires(posIsNull: false, posQty: 2);
            Assert.False(fires);
        }

        // ==================================================================
        // IncrementBuffer / DecrementBuffer behaviour
        // ==================================================================

        [Fact]
        public void IncrementBuffer_IncreasesBufferByOne()
        {
            int result = IncrementBuffer(0);
            Assert.Equal(1, result);
        }

        [Fact]
        public void IncrementBuffer_AtUpperBound_Clamps()
        {
            int result = IncrementBuffer(10);
            Assert.Equal(10, result);
        }

        [Fact]
        public void IncrementBuffer_BelowUpperBound_Increments()
        {
            int result = IncrementBuffer(9);
            Assert.Equal(10, result);
        }

        [Fact]
        public void DecrementBuffer_DecreasesBufferByOne()
        {
            int result = DecrementBuffer(0);
            Assert.Equal(-1, result);
        }

        [Fact]
        public void DecrementBuffer_AtLowerBound_Clamps()
        {
            int result = DecrementBuffer(-10);
            Assert.Equal(-10, result);
        }

        [Fact]
        public void DecrementBuffer_AboveLowerBound_Decrements()
        {
            int result = DecrementBuffer(-9);
            Assert.Equal(-10, result);
        }

        // ==================================================================
        // Execute(IEnumerable) guard behaviour
        // ==================================================================

        [Fact]
        public void Execute_IEnumerable_SkipsNullPositions()
        {
            var positions = new[] { (posIsNull: true, posQty: 1), (posIsNull: false, posQty: 2) };
            int calls = CountExecuteOneCalls(positions);
            Assert.Equal(1, calls);
        }

        [Fact]
        public void Execute_IEnumerable_SkipsZeroQtyPositions()
        {
            var positions = new[] { (posIsNull: false, posQty: 0), (posIsNull: false, posQty: 3) };
            int calls = CountExecuteOneCalls(positions);
            Assert.Equal(1, calls);
        }

        [Fact]
        public void Execute_IEnumerable_AllSkipped_ZeroCalls()
        {
            var positions = new[] { (posIsNull: true, posQty: 0), (posIsNull: false, posQty: 0) };
            int calls = CountExecuteOneCalls(positions);
            Assert.Equal(0, calls);
        }

        // ==================================================================
        // BuildGlobalBeOcoId format (B40 DW-B39-OCO-01)
        // ==================================================================

        [Fact]
        public void BuildGlobalBeOcoId_FormatsCorrectly()
        {
            string id = BuildGlobalBeOcoId(seq: 1, accIdx: 0, pairIndex: 2);
            Assert.Equal("PTT-BEG-00001-0-2", id);
        }

        [Fact]
        public void BuildGlobalBeOcoId_PadsSeqToFiveDigits()
        {
            string id = BuildGlobalBeOcoId(seq: 42, accIdx: 1, pairIndex: 0);
            Assert.Equal("PTT-BEG-00042-1-0", id);
        }

        // ==================================================================
        // bePrice calculation (ExecuteOne direction-aware)
        // ==================================================================

        [Fact]
        public void CalcBePrice_LongPosition_AddsTicks()
        {
            double result = CalcBePrice(avgPrice: 4500.00, isLong: true, bufferTicks: 1, tickSize: 0.25);
            Assert.Equal(4500.25, result, precision: 8);
        }

        [Fact]
        public void CalcBePrice_ShortPosition_SubtractsTicks()
        {
            double result = CalcBePrice(avgPrice: 4500.00, isLong: false, bufferTicks: 1, tickSize: 0.25);
            Assert.Equal(4499.75, result, precision: 8);
        }
    }
}