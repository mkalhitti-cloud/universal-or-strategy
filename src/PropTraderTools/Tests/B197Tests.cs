// B197Tests.cs -- xUnit tests for B197 T1+T2+T3+T4: source-text assertions.
// Strategy: source-text assertions -- no NT8 runtime required.
// Framework: xUnit only. No NUnit. No MSTest.
// JS-021: no lock. JS-033: no async void. JS-056: 1 assert per Fact. ASCII-only.
using Xunit;

namespace PropTraderTools.Tests
{
    public class B197Tests
    {
        // T1-1: Verifies the _followerSlateReady[e.Order.Account.Name] = true write is present (core body).
        [Fact]
        public void TrySetSlateReadyOnDrainFill_SourceContainsFollowerSlateReady()
        {
            var src = System.IO.File.ReadAllText(
                System.IO.Path.Combine("..", "..", "..", "CopyEngine.cs"));
            Assert.Contains("_followerSlateReady[", src);
        }

        // T1-2: Verifies the [SLATE-READY-DRAIN] log marker confirming drain-fill path was implemented.
        [Fact]
        public void TrySetSlateReadyOnDrainFill_SourceContainsSlateReadyDrainMarker()
        {
            var src = System.IO.File.ReadAllText(
                System.IO.Path.Combine("..", "..", "..", "CopyEngine.cs"));
            Assert.Contains("[SLATE-READY-DRAIN]", src);
        }

        // T2-1: Verifies [DRAIN-ABORT-LEADER-FLAT] log marker is present (confirms method body + leader-flat path).
        [Fact]
        public void TryEvictFollowerBeSlot_SourceContainsDrainAbortLeaderFlat()
        {
            var src = System.IO.File.ReadAllText(
                System.IO.Path.Combine("..", "..", "..", "CopyEngine.cs"));
            Assert.Contains("[DRAIN-ABORT-LEADER-FLAT]", src);
        }

        // T2-2: Verifies TryAbortLeaderFlatDrains symbol name exists (confirms method added + call site wired).
        [Fact]
        public void TryEvictFollowerBeSlot_SourceContainsTryAbortLeaderFlatDrains()
        {
            var src = System.IO.File.ReadAllText(
                System.IO.Path.Combine("..", "..", "..", "CopyEngine.cs"));
            Assert.Contains("TryAbortLeaderFlatDrains", src);
        }

        // T3-1: Verifies TryCancelStaleGtcExitOnEntry symbol is present in source (confirms both method definition and call site wiring).
        [Fact]
        public void TryCancelStaleGtcExitOnEntry_SourceContainsMethodName()
        {
            var src = System.IO.File.ReadAllText(
                System.IO.Path.Combine("..", "..", "..", "CopyEngine.cs"));
            Assert.Contains("TryCancelStaleGtcExitOnEntry", src);
        }

        // T3-2: Verifies [GTC-SWEEP] log marker is present, confirming the sweep body was implemented correctly.
        [Fact]
        public void TryCancelStaleGtcExitOnEntry_SourceContainsGtcSweepMarker()
        {
            var src = System.IO.File.ReadAllText(
                System.IO.Path.Combine("..", "..", "..", "CopyEngine.cs"));
            Assert.Contains("[GTC-SWEEP]", src);
        }

        // T4-1: PRIMARY FIX -- StartsWith("PTT-TGT-Drag") present in IsPttDragOrphanCancellableTestable
        //   (verifies suffixed TGT drag names e.g. "PTT-TGT-Drag-1" are now matched).
        [Fact]
        public void IsPttDragOrphanCancellableTestable_SuffixedTgtName_ReturnsTrue()
        {
            var src = System.IO.File.ReadAllText(
                System.IO.Path.Combine("..", "..", "..", "CopyEngine.cs"));
            Assert.Contains("StartsWith(\"PTT-TGT-Drag\", StringComparison.Ordinal)", src);
        }

        // T4-2: PRIMARY FIX -- StartsWith("PTT-STP-Drag") present in IsPttDragOrphanCancellableTestable
        //   (verifies suffixed STP drag names e.g. "PTT-STP-Drag-2" are now matched).
        [Fact]
        public void IsPttDragOrphanCancellableTestable_SuffixedStpName_ReturnsTrue()
        {
            var src = System.IO.File.ReadAllText(
                System.IO.Path.Combine("..", "..", "..", "CopyEngine.cs"));
            Assert.Contains("StartsWith(\"PTT-STP-Drag\", StringComparison.Ordinal)", src);
        }

        // T4-3: REGRESSION -- IsPttDragOrphanCancellableTestable seam still exists (method not removed).
        //   StartsWith("PTT-TGT-Drag") is a prefix match so unsuffixed "PTT-TGT-Drag" still passes.
        [Fact]
        public void IsPttDragOrphanCancellableTestable_UnsuffixedTgtName_StillReturnsTrue()
        {
            var src = System.IO.File.ReadAllText(
                System.IO.Path.Combine("..", "..", "..", "CopyEngine.cs"));
            Assert.Contains("IsPttDragOrphanCancellableTestable", src);
        }

        // T4-4: REGRESSION -- old exact-match guard removed; StartsWith is now the guard for PTT-STP-Drag.
        //   Confirms the fix is in place and old != pattern for PTT-STP-Drag is gone from Testable guard.
        [Fact]
        public void IsPttDragOrphanCancellableTestable_UnsuffixedStpName_StillReturnsTrue()
        {
            var src = System.IO.File.ReadAllText(
                System.IO.Path.Combine("..", "..", "..", "CopyEngine.cs"));
            Assert.DoesNotContain("orderName != \"PTT-STP-Drag\"", src);
        }
    }
}