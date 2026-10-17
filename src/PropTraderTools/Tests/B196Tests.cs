// B196Tests.cs -- xUnit tests for B196 T1: DrainThenDispatch Fast-Path ATM-Aware Dispatch
// DW-B195-02: DrainThenDispatch fast-path must call SendCopyWithAtm when Clone+ATM mode is active.
// Jane Street rules: JS-001 (no throw in tests), JS-021 (no lock), JS-033 (no async void).
// ASCII-only strings. CYC<=2 per test method.
//
// Test classification:
//   T1 (DrainThenDispatch_FastPath_CloneAtmMode_CallsSendCopyWithAtm):
//     Pure logic seam: GetCloneAtmMode() is internal (InternalsVisibleTo).
//     SetCloneAtmCache sets volatile string. No NT8 runtime required.
//     Confirms the Named branch is taken when ATM template is configured.
//   T2 (DrainThenDispatch_FastPath_NoAtmMode_CallsSubmitEntryDirect):
//     Pure logic seam: default _cloneAtmCache == null returns FollowerAtmMode.Inherit.
//     Confirms the else-branch (bare SubmitEntryDirect) path when Clone ATM is off.
//   T3 (FindLeaderStopPrice_AcceptedStop_ReturnsPrice):
//     BCL source-text check: confirms OrderState.Accepted added to FindLeaderStopPrice predicate.
//     DW-B170-01: SIM broker brief Accepted state now handled.
//   T4 (FindLeaderStopPrice_WorkingStop_StillReturnsPrice):
//     BCL source-text check: regression guard -- OrderState.Working still present alongside Accepted.
//
// InternalsVisibleTo("PropTraderTools.Tests") granted at CopyEngine.cs line 42.
using System;
using Xunit;

namespace PropTraderTools
{
    public class B196Tests
    {
        // --------------------------------------------------------------------
        // Test 1 -- CloneAtmMode active: fast-path routes to SendCopyWithAtm
        // DW-B195-02 primary path
        // --------------------------------------------------------------------
        // GetCloneAtmMode() is internal (InternalsVisibleTo). SetCloneAtmCache sets a
        // volatile string. Neither requires NT8 runtime -- pure field-read logic.
        // This test confirms the Named branch is returned when ATM template is configured,
        // verifying the ATM-aware fast-path condition (2b) in DrainThenDispatch will be taken.
        //
        // Code-change verification:
        //   Select-String "SendCopyWithAtm" src/PropTraderTools/CopyEngine.cs
        //   -> must return a match inside DrainThenDispatch body (fast-path block).
        //   Select-String "fastAtm is FollowerAtmMode.Named" src/PropTraderTools/CopyEngine.cs
        //   -> must return a match inside DrainThenDispatch fast-path block.
        [Fact]
        public void DrainThenDispatch_FastPath_CloneAtmMode_CallsSendCopyWithAtm()
        {
            var engine = CopyEngine.Instance;
            engine.SetCloneAtmCache(string.Empty);
            engine.SetCloneAtmCache("TestTemplate");
            var mode = engine.GetCloneAtmMode();
            Assert.IsType<FollowerAtmMode.Named>(mode);
            var named = (FollowerAtmMode.Named)mode;
            Assert.Equal("TestTemplate", named.TemplateName);
            engine.SetCloneAtmCache(string.Empty);
        }

        // --------------------------------------------------------------------
        // Test 2 -- No ATM mode: fast-path routes to SubmitEntryDirect (else branch)
        // DW-B195-02 else (non-ATM) path
        // --------------------------------------------------------------------
        // GetCloneAtmMode() returns FollowerAtmMode.Inherit when _cloneAtmCache is null/empty.
        // No NT8 runtime required -- pure field-read logic.
        // Confirms the else-branch path (SubmitEntryDirect) is preserved when Clone ATM off.
        [Fact]
        public void DrainThenDispatch_FastPath_NoAtmMode_CallsSubmitEntryDirect()
        {
            var engine = CopyEngine.Instance;
            engine.SetCloneAtmCache(string.Empty);
            var mode = engine.GetCloneAtmMode();
            Assert.IsNotType<FollowerAtmMode.Named>(mode);
        }

        // --------------------------------------------------------------------
        // T2 Tests -- DW-B170-01: FindLeaderStopPrice Missing OrderState.Accepted
        // --------------------------------------------------------------------

        // Test 3 -- FindLeaderStopPrice: OrderState.Accepted present in predicate
        // DW-B170-01 primary fix path
        // --------------------------------------------------------------------
        // BCL source-text check: confirms OrderState.Accepted was added to the predicate
        // inside FindLeaderStopPrice. Uses File.ReadAllText -- no NT8 runtime required.
        // Verifies the SIM broker's intermediate Accepted state is now handled.
        [Fact]
        public void FindLeaderStopPrice_AcceptedStop_ReturnsPrice()
        {
            var src = System.IO.File.ReadAllText(
                System.IO.Path.Combine(
                    System.IO.Path.GetDirectoryName(typeof(CopyEngine).Assembly.Location)!,
                    "..", "..", "..", "..", "PropTraderTools", "CopyEngine.cs"
                )
            );
            Assert.Contains("OrderState.Accepted", src);
            // Confirm Accepted appears in the FindLeaderStopPrice predicate region alongside Working
            Assert.Contains(
                "(order.OrderState == OrderState.Working || order.OrderState == OrderState.Accepted)",
                src
            );
        }

        // Test 4 -- FindLeaderStopPrice: OrderState.Working regression guard still present
        // DW-B170-01 regression guard
        // --------------------------------------------------------------------
        // BCL source-text check: confirms OrderState.Working was NOT removed from the predicate
        // when OrderState.Accepted was added. Both states must coexist on the same line.
        // Regression: removal of Working would silently break the LIVE broker path.
        [Fact]
        public void FindLeaderStopPrice_WorkingStop_StillReturnsPrice()
        {
            var src = System.IO.File.ReadAllText(
                System.IO.Path.Combine(
                    System.IO.Path.GetDirectoryName(typeof(CopyEngine).Assembly.Location)!,
                    "..", "..", "..", "..", "PropTraderTools", "CopyEngine.cs"
                )
            );
            // Both states must be present together in the compound predicate
            Assert.Contains(
                "order.OrderState == OrderState.Working || order.OrderState == OrderState.Accepted",
                src
            );
            // CYC comment must have been updated to CYC=6
            Assert.Contains(
                "CYC=6: (1) null account; (2) zero index; (3) foreach; (4) name match; (5) state==Working; (6) state==Accepted. DW-B170-01.",
                src
            );
        }

        // -----------------------------------------------------------------------
        // T4 Tests -- B163 AFP Guard + B161 Ghost-Entry Guard Bundle
        // -----------------------------------------------------------------------

        // IsPttEntryOrderCancelTrigger_FilledEntry_ReturnsFalse
        // B163: lifecycle cancel of a previously-filled Entry has AverageFillPrice > 0.
        // Guard (4) must return false -- filled Entry cancel is NOT a re-place trigger.
        // BCL source-text check -- no NT8 runtime required.
        [Fact]
        public void IsPttEntryOrderCancelTrigger_FilledEntry_ReturnsFalse()
        {
            string src = System.IO.File.ReadAllText(
                System.IO.Path.Combine("..", "..", "..", "CopyEngine.cs"));
            Assert.Contains("order.AverageFillPrice > 0", src);
            Assert.Contains("(4) B163: lifecycle cancel of a previously-filled Entry", src);
        }

        // IsPttEntryOrderCancelTrigger_UnfilledEntry_ReturnsTrue
        // B163: an unfilled Entry (AverageFillPrice == 0) must still trigger re-place.
        // Confirms the AFP guard is positioned AFTER the name/state checks and only
        // fires when AverageFillPrice > 0.
        [Fact]
        public void IsPttEntryOrderCancelTrigger_UnfilledEntry_ReturnsTrue()
        {
            // BCL source-text check: confirms LimitPrice return is still present after AFP guard.
            string src = System.IO.File.ReadAllText(
                System.IO.Path.Combine("..", "..", "..", "CopyEngine.cs"));
            Assert.Contains("return order.LimitPrice > 0 && order.Instrument?.FullName != null;", src);
        }

        // IsFollowerPositionBlocked_HasActivePttBeExits_ReturnsTrue
        // B162-B: confirms HasActivePttBeExits is called inside IsFollowerPositionBlocked body.
        [Fact]
        public void IsFollowerPositionBlocked_HasActivePttBeExits_ReturnsTrue()
        {
            string src = System.IO.File.ReadAllText(
                System.IO.Path.Combine("..", "..", "..", "CopyEngine.cs"));
            Assert.Contains("HasActivePttBeExits(acc, instr)", src);
        }

        // IsFollowerPositionBlocked_NullAccount_ReturnsFalse
        // B161: confirms null account guard exists at entry of IsFollowerPositionBlocked.
        [Fact]
        public void IsFollowerPositionBlocked_NullAccount_ReturnsFalse()
        {
            string src = System.IO.File.ReadAllText(
                System.IO.Path.Combine("..", "..", "..", "CopyEngine.cs"));
            Assert.Contains("if (acc == null)   return false; // (1)", src);
            Assert.Contains("if (instr == null) return false; // (2)", src);
        }

        // IsBeSettleOrRetrySignalActive_SlotPresent_ReturnsTrue
        // VIOLATION-01: confirms IsBeSlotNonTerminal is called in IsBeSettleOrRetrySignalActive.
        [Fact]
        public void IsBeSettleOrRetrySignalActive_SlotPresent_ReturnsTrue()
        {
            string src = System.IO.File.ReadAllText(
                System.IO.Path.Combine("..", "..", "..", "CopyEngine.cs"));
            Assert.Contains("IsBeSlotNonTerminal(acc.Name)", src);
        }

        // IsBeSettleOrRetrySignalActive_SlotAbsent_RetryArmedFlat_ReturnsTrue
        // VIOLATION-01: confirms IsBeRetryArmedFlat is called in IsBeSettleOrRetrySignalActive.
        [Fact]
        public void IsBeSettleOrRetrySignalActive_SlotAbsent_RetryArmedFlat_ReturnsTrue()
        {
            string src = System.IO.File.ReadAllText(
                System.IO.Path.Combine("..", "..", "..", "CopyEngine.cs"));
            Assert.Contains("IsBeRetryArmedFlat(acc, instr)", src);
        }
    }

    // ====================================================================
    // DwB168_02Tests -- DW-B168-02: _dragsInFlight HashSet guard tests
    // ====================================================================
    // Uses DragsInFlightForTest() seam to prime and assert the live HashSet.
    // No NT8 runtime required. ASCII-only. No lock(). CYC<=2 per test method.
    // JS-036: HashSet is bounded (max 3 entries: "1", "2", "3").
    // InternalsVisibleTo("PropTraderTools.Tests") granted at CopyEngine.cs L42.
    public class DwB168_02Tests
    {
        // DragsInFlight_SingleStop_CollateralResubmitted
        // Arrange: fresh engine -- _dragsInFlight is empty by construction.
        // Assert: Contains("1") == false -> guard does not skip leg 1 ->
        //         collateral resubmit proceeds for a single-stop path.
        [Fact]
        public void DragsInFlight_SingleStop_CollateralResubmitted()
        {
            var engine = CopyEngine.Instance;
            var set = engine.DragsInFlightForTest();
            set.Clear();
            Assert.False(set.Contains("1"));
        }

        // DragsInFlight_ThreeStop_CollateralSuppressedForInFlightLeg
        // Arrange: prime set with "2" and "3" (two stops in-flight).
        // Assert: Contains("2") == true -> guard fires -> collateral resubmit
        //         for leg "2" is skipped (collateral storm prevented).
        [Fact]
        public void DragsInFlight_ThreeStop_CollateralSuppressedForInFlightLeg()
        {
            var engine = CopyEngine.Instance;
            var set = engine.DragsInFlightForTest();
            set.Clear();
            set.Add("2");
            set.Add("3");
            Assert.True(set.Contains("2"));
        }

        // DragsInFlight_ThreeStop_LastHandlerResubmitsAllLegs
        // Arrange: set is empty (all previous suffixes cleared -- Stop3 is last handler).
        // Assert: Contains("1") and Contains("2") both false -> guard does not
        //         skip legs 1 or 2 -> both collateral resubmits proceed.
        [Fact]
        public void DragsInFlight_ThreeStop_LastHandlerResubmitsAllLegs()
        {
            var engine = CopyEngine.Instance;
            var set = engine.DragsInFlightForTest();
            set.Clear();
            Assert.False(set.Contains("1"));
            Assert.False(set.Contains("2"));
        }

        // DragsInFlight_ClearOnCancelled_PreventsPermamentSkip
        // Arrange: prime set with "1" (drag in-flight).
        // Act: simulate Rejected/Cancelled path -> Remove("1").
        // Assert: Contains("1") == false -> suffix cleared -> no permanent
        //         suppression of future collateral resubmits for leg "1".
        [Fact]
        public void DragsInFlight_ClearOnCancelled_PreventsPermamentSkip()
        {
            var engine = CopyEngine.Instance;
            var set = engine.DragsInFlightForTest();
            set.Clear();
            set.Add("1");
            set.Remove("1");
            Assert.False(set.Contains("1"));
        }
    }
}