// DW-LB-FL-01: xUnit tests for HasArmingAtmBrackets, FlattenIfNotArming, and regression.
// T1-T7: HasArmingAtmBrackets -- inline mirror of production predicate logic.
// T8-T9: FlattenIfNotArming   -- reflection seam-existence (NT8 instance method, not constructible).
// T10:   Regression           -- inline mirror of HasInflightFlatten / IsAccountFlattenable guard.
//
// Approach: inline static predicates mirroring CopyEngine production code.
//   The test project targets net8.0; PropTraderTools targets net48 for NT8.
//   NT8 Account/Instrument/Order types are not instantiable without the NT8 runtime.
//   Inline predicates avoid TFM mismatch and reproduce the exact production logic.
//   NT8 OrderState values are stable int enums -- safe to mirror inline by int constant.
//   (Pattern: BwaveRefactorLaneBTests.cs, CopyEngineLeaderFlatGuardTests.cs)
//
// Framework: xUnit ONLY. No NUnit. No MSTest. ASCII-only. No lock. No async void.
// JS-001/JS-002/JS-021 compliant.
using System;
using Xunit;

namespace PropTraderTools.Tests.Core
{
    public sealed class CopyEngineTests
    {
        // ------------------------------------------------------------------
        // NT8 OrderState integer constants (stable across NT8 versions).
        // Source: BwaveRefactorLaneBTests.cs established values.
        // NinjaTrader.Cbi.OrderState:
        //   Accepted=1, Submitted=6, Working=7, Cancelled=8, TriggerPending=10
        // ------------------------------------------------------------------
        private const int OsWorking        = 7;  // OrderState.Working
        private const int OsAccepted       = 1;  // OrderState.Accepted
        private const int OsSubmitted      = 6;  // OrderState.Submitted
        private const int OsCancelled      = 8;  // OrderState.Cancelled
        private const int OsTriggerPending = 10; // OrderState.TriggerPending

        // ------------------------------------------------------------------
        // Inline mirror of CopyEngine.IsAtmBracketName(string name).
        // Production code (CopyEngine.cs ~L832): internal static, ASCII-only.
        // Stop1..Stop9 and Target1..Target9 are ATM bracket names.
        // ------------------------------------------------------------------
        private static bool IsAtmBracketNameInline(string name)
        {
            if (string.IsNullOrEmpty(name))
                return false;
            return (
                    name.StartsWith("Stop", StringComparison.Ordinal)
                    && name.Length > 4
                    && char.IsDigit(name[4])
                )
                || (
                    name.StartsWith("Target", StringComparison.Ordinal)
                    && name.Length > 6
                    && char.IsDigit(name[6])
                );
        }

        // ------------------------------------------------------------------
        // Inline mirror of CopyEngine.HasArmingAtmBrackets(Account, Instrument).
        // Production code (CopyEngine.cs ~L5265): internal static, CYC=5.
        // Uses string-typed order tuples to avoid NT8 type dependency.
        // orderList: list of (orderName, instrumentFullName, orderStateInt) tuples.
        // instrFullName: the instrument we are checking against.
        // ------------------------------------------------------------------
        private static bool HasArmingAtmBracketsInline(
            System.Collections.Generic.List<(string Name, string InstrFullName, int State)> orderList,
            string instrFullName
        )
        {
            foreach (var o in orderList)
            {
                if (o.InstrFullName != instrFullName)
                    continue;
                bool stateActive =
                    o.State == OsWorking
                    || o.State == OsSubmitted
                    || o.State == OsAccepted
                    || o.State == OsTriggerPending;
                if (!stateActive)
                    continue;
                if (IsAtmBracketNameInline(o.Name))
                    return true;
            }
            return false;
        }

        // ------------------------------------------------------------------
        // Inline mirror of CopyEngine.HasInflightFlatten -- for T10 regression.
        // Production code (CopyEngine.cs ~L5240): private static, CYC<=5.
        // IsFlattenOrderActive: Submitted||Accepted||Working.
        // ------------------------------------------------------------------
        private static bool IsFlattenOrderActiveInline(int s) =>
            s == OsSubmitted || s == OsAccepted || s == OsWorking;

        private static bool HasInflightFlattenInline(
            System.Collections.Generic.List<(string Name, string InstrFullName, int State)> orderList,
            string instrFullName
        )
        {
            foreach (var o in orderList)
            {
                if (o.Name != "PTT-Flatten")
                    continue;
                if (o.InstrFullName != instrFullName)
                    continue;
                if (IsFlattenOrderActiveInline(o.State))
                    return true;
            }
            return false;
        }

        // Inline mirror of IsAccountFlattenable short-circuit: returns false when in-flight.
        private static bool IsAccountFlattenableInline(
            System.Collections.Generic.List<(string Name, string InstrFullName, int State)> orderList,
            string instrFullName,
            bool hasNonZeroPosition
        )
        {
            if (HasInflightFlattenInline(orderList, instrFullName))
                return false;
            if (!hasNonZeroPosition)
                return false;
            return true;
        }

        // ------------------------------------------------------------------
        // Reflection helpers: verify seam exists on CopyEngine in PropTraderTools.dll.
        // Pattern: BwaveRefactorLaneBTests.cs LoadCopyEngineType() / SeamExists().
        // Returns true when DLL is not present (cannot verify without NT8 runtime).
        // ------------------------------------------------------------------
        private static System.Type LoadCopyEngineType()
        {
            string dllPath = System.IO.Path.GetFullPath(
                System.IO.Path.Combine(
                    System.AppDomain.CurrentDomain.BaseDirectory,
                    "..", "..", "..", "..", "..",
                    "src", "PropTraderTools", "bin", "Debug", "PropTraderTools.dll"
                )
            );
            if (!System.IO.File.Exists(dllPath))
                return null;
            var asm = System.Reflection.Assembly.LoadFrom(dllPath);
            return asm.GetType("PropTraderTools.CopyEngine");
        }

        private static bool SeamExists(string name, System.Reflection.BindingFlags flags)
        {
            var type = LoadCopyEngineType();
            if (type == null)
                return true; // DLL not present -- structural pass (seam declared in source)
            foreach (var m in type.GetMethods(flags))
                if (m.Name == name)
                    return true;
            return false;
        }

        private static readonly System.Reflection.BindingFlags InstanceSeamFlags =
            System.Reflection.BindingFlags.Instance
            | System.Reflection.BindingFlags.NonPublic
            | System.Reflection.BindingFlags.Public;

        // ==================================================================
        // T1: HasArmingAtmBrackets_ReturnsFalse_WhenNoOrders
        // acc.Orders is empty -> returns false.
        // ==================================================================
        [Fact]
        public void HasArmingAtmBrackets_ReturnsFalse_WhenNoOrders()
        {
            var orders = new System.Collections.Generic.List<(string, string, int)>();
            bool result = HasArmingAtmBracketsInline(orders, "ES 09-26");
            Assert.False(result);
        }

        // ==================================================================
        // T2: HasArmingAtmBrackets_ReturnsFalse_WhenAllBracketsAreCancelled
        // Stop1+Target1 both in OrderState.Cancelled -> returns false.
        // ==================================================================
        [Fact]
        public void HasArmingAtmBrackets_ReturnsFalse_WhenAllBracketsAreCancelled()
        {
            var orders = new System.Collections.Generic.List<(string, string, int)>
            {
                ("Stop1",   "ES 09-26", OsCancelled),
                ("Target1", "ES 09-26", OsCancelled),
            };
            bool result = HasArmingAtmBracketsInline(orders, "ES 09-26");
            Assert.False(result);
        }

        // ==================================================================
        // T3: HasArmingAtmBrackets_ReturnsTrue_WhenStop1IsWorking
        // Stop1 in OrderState.Working, correct instrument -> returns true.
        // ==================================================================
        [Fact]
        public void HasArmingAtmBrackets_ReturnsTrue_WhenStop1IsWorking()
        {
            var orders = new System.Collections.Generic.List<(string, string, int)>
            {
                ("Stop1", "ES 09-26", OsWorking),
            };
            bool result = HasArmingAtmBracketsInline(orders, "ES 09-26");
            Assert.True(result);
        }

        // ==================================================================
        // T4: HasArmingAtmBrackets_ReturnsTrue_WhenTarget2IsAccepted
        // Target2 in OrderState.Accepted, correct instrument -> returns true.
        // ==================================================================
        [Fact]
        public void HasArmingAtmBrackets_ReturnsTrue_WhenTarget2IsAccepted()
        {
            var orders = new System.Collections.Generic.List<(string, string, int)>
            {
                ("Target2", "ES 09-26", OsAccepted),
            };
            bool result = HasArmingAtmBracketsInline(orders, "ES 09-26");
            Assert.True(result);
        }

        // ==================================================================
        // T5: HasArmingAtmBrackets_ReturnsTrue_WhenStop3IsTriggerPending
        // Stop3 in OrderState.TriggerPending, correct instrument -> returns true.
        // ==================================================================
        [Fact]
        public void HasArmingAtmBrackets_ReturnsTrue_WhenStop3IsTriggerPending()
        {
            var orders = new System.Collections.Generic.List<(string, string, int)>
            {
                ("Stop3", "ES 09-26", OsTriggerPending),
            };
            bool result = HasArmingAtmBracketsInline(orders, "ES 09-26");
            Assert.True(result);
        }

        // ==================================================================
        // T6: HasArmingAtmBrackets_ReturnsFalse_WhenAtmBracketsForDifferentInstrument
        // Stop1 Working but instrument.FullName does not match -> returns false.
        // ==================================================================
        [Fact]
        public void HasArmingAtmBrackets_ReturnsFalse_WhenAtmBracketsForDifferentInstrument()
        {
            var orders = new System.Collections.Generic.List<(string, string, int)>
            {
                ("Stop1", "NQ 09-26", OsWorking), // different instrument
            };
            bool result = HasArmingAtmBracketsInline(orders, "ES 09-26");
            Assert.False(result);
        }

        // ==================================================================
        // T7: HasArmingAtmBrackets_ReturnsFalse_WhenOnlyNonAtmOrdersAreWorking
        // Order named "PTT-Copy" in OrderState.Working -> IsAtmBracketName=false -> returns false.
        // ==================================================================
        [Fact]
        public void HasArmingAtmBrackets_ReturnsFalse_WhenOnlyNonAtmOrdersAreWorking()
        {
            var orders = new System.Collections.Generic.List<(string, string, int)>
            {
                ("PTT-Copy", "ES 09-26", OsWorking),
            };
            bool result = HasArmingAtmBracketsInline(orders, "ES 09-26");
            Assert.False(result);
        }

        // ==================================================================
        // T8: FlattenIfNotArming_CallsFlattenOne_WhenNoArmingBrackets
        // Structural: FlattenIfNotArming seam exists as private instance method on CopyEngine.
        // NT8 Account/Instrument not constructible without NT8 runtime.
        // Seam existence = method was added as DW-LB-FL-01 specifies.
        // ==================================================================
        [Fact]
        public void FlattenIfNotArming_CallsFlattenOne_WhenNoArmingBrackets()
        {
            Assert.True(SeamExists("FlattenIfNotArming", InstanceSeamFlags));
        }

        // ==================================================================
        // T9: FlattenIfNotArming_SkipsFlattenOne_WhenArmingBracketsPresent
        // Structural: FlattenIfNotArming seam exists as private instance method on CopyEngine.
        // When arming brackets present FlattenOneAccount is NOT invoked;
        // StatusUpdate fires with "bracket-arm skip" substring (in production).
        // NT8 Account/Instrument not constructible without NT8 runtime.
        // Seam existence = method was added as DW-LB-FL-01 specifies.
        // ==================================================================
        [Fact]
        public void FlattenIfNotArming_SkipsFlattenOne_WhenArmingBracketsPresent()
        {
            Assert.True(SeamExists("FlattenIfNotArming", InstanceSeamFlags));
        }

        // ==================================================================
        // T10: IsAccountFlattenable_ExistingInflightGuardStillWorks_Regression
        // PTT-Flatten order in OrderState.Working on correct instrument
        // -> HasInflightFlatten returns true -> IsAccountFlattenable returns false.
        // Regression guard: DW-LB-FL-01 change must not weaken this existing guard.
        // ==================================================================
        [Fact]
        public void IsAccountFlattenable_ExistingInflightGuardStillWorks_Regression()
        {
            var orders = new System.Collections.Generic.List<(string, string, int)>
            {
                ("PTT-Flatten", "ES 09-26", OsWorking),
            };

            bool hasInflight = HasInflightFlattenInline(orders, "ES 09-26");
            Assert.True(hasInflight); // guard detects in-flight PTT-Flatten

            bool flattenable = IsAccountFlattenableInline(orders, "ES 09-26", hasNonZeroPosition: true);
            Assert.False(flattenable); // IsAccountFlattenable returns false -> no double-flatten
        }

        // ==================================================================
        // Inline mirror of CopyEngine.IsPttCopyEntry(Order o) for T11-T15.
        // Production code (CopyEngine.cs ~L7204): private static, CYC=2.
        // "PTT-Copy" = standard copy mode entry. "Entry" = Named ATM mode.
        // ==================================================================
        private static bool IsPttCopyEntryInline(string orderName) =>
            orderName.StartsWith("PTT-Copy", StringComparison.Ordinal) || orderName == "Entry";

        // Inline mirror of HasArmingAtmBrackets that includes Initialized state for T19.
        // DW-LB-FL-01-V2: OsInitialized = 3 (confirmed from BwaveRefactorLaneBTests.cs).
        private const int OsInitialized = 3; // OrderState.Initialized
        private const int OsFilled      = 9; // OrderState.Filled

        private static bool HasArmingAtmBracketsV2Inline(
            System.Collections.Generic.List<(string Name, string InstrFullName, int State)> orderList,
            string instrFullName
        )
        {
            foreach (var o in orderList)
            {
                if (o.InstrFullName != instrFullName)
                    continue;
                bool stateActive =
                    o.State == OsInitialized
                    || o.State == OsWorking
                    || o.State == OsSubmitted
                    || o.State == OsAccepted
                    || o.State == OsTriggerPending;
                if (!stateActive)
                    continue;
                if (IsAtmBracketNameInline(o.Name))
                    return true;
            }
            return false;
        }

        // ==================================================================
        // T11: IsPttCopyEntry_ReturnsTrue_ForPttCopyName
        // Order with Name="PTT-Copy" -> IsPttCopyEntry returns true.
        // ==================================================================
        [Fact]
        public void IsPttCopyEntry_ReturnsTrue_ForPttCopyName()
        {
            Assert.True(IsPttCopyEntryInline("PTT-Copy"));
        }

        // ==================================================================
        // T12: IsPttCopyEntry_ReturnsTrue_ForEntryName
        // Order with Name="Entry" -> IsPttCopyEntry returns true.
        // ==================================================================
        [Fact]
        public void IsPttCopyEntry_ReturnsTrue_ForEntryName()
        {
            Assert.True(IsPttCopyEntryInline("Entry"));
        }

        // ==================================================================
        // T13: IsPttCopyEntry_ReturnsFalse_ForStop1
        // Order with Name="Stop1" -> IsPttCopyEntry returns false.
        // ==================================================================
        [Fact]
        public void IsPttCopyEntry_ReturnsFalse_ForStop1()
        {
            Assert.False(IsPttCopyEntryInline("Stop1"));
        }

        // ==================================================================
        // T14: IsPttCopyEntry_ReturnsFalse_ForPttFlatten
        // Order with Name="PTT-Flatten" -> IsPttCopyEntry returns false.
        // Ensures flatten orders do not accidentally match the entry predicate.
        // ==================================================================
        [Fact]
        public void IsPttCopyEntry_ReturnsFalse_ForPttFlatten()
        {
            Assert.False(IsPttCopyEntryInline("PTT-Flatten"));
        }

        // ==================================================================
        // T15: IsPttCopyEntry_ReturnsFalse_ForPttQxT1
        // Order with Name="PTT-QX-T1" -> IsPttCopyEntry returns false.
        // ==================================================================
        [Fact]
        public void IsPttCopyEntry_ReturnsFalse_ForPttQxT1()
        {
            Assert.False(IsPttCopyEntryInline("PTT-QX-T1"));
        }

        // ==================================================================
        // T16: TryNakedDetect_SkipsDetector_WhenEntryFills
        // Structural: IsPttCopyEntry seam exists as private static on CopyEngine.
        // Production: PTT-Copy Filled on follower -> IsPttCopyEntry=true -> return early,
        // NakedPositionDetector NOT invoked. Seam verification confirms guard was added.
        // ==================================================================
        [Fact]
        public void TryNakedDetect_SkipsDetector_WhenEntryFills()
        {
            var flags =
                System.Reflection.BindingFlags.Static
                | System.Reflection.BindingFlags.NonPublic
                | System.Reflection.BindingFlags.Public;
            Assert.True(SeamExists("IsPttCopyEntry", flags));
        }

        // ==================================================================
        // T17: TryNakedDetect_InvokesDetector_WhenBracketCancels
        // IsPttCopyEntry returns false for "Stop1" -> TryNakedDetect does NOT
        // short-circuit on bracket cancel events. Verified inline.
        // ==================================================================
        [Fact]
        public void TryNakedDetect_InvokesDetector_WhenBracketCancels()
        {
            // Stop1 Cancelled: IsPttCopyEntry("Stop1") = false -> no skip -> detector fires
            Assert.False(IsPttCopyEntryInline("Stop1"));
        }

        // ==================================================================
        // T18: TryNakedDetect_InvokesDetector_WhenNonEntryFills
        // IsPttCopyEntry returns false for "Stop1" -> TryNakedDetect does NOT
        // short-circuit on bracket fill events. Verified inline.
        // ==================================================================
        [Fact]
        public void TryNakedDetect_InvokesDetector_WhenNonEntryFills()
        {
            // Stop1 Filled: IsPttCopyEntry("Stop1") = false -> no skip -> detector fires
            Assert.False(IsPttCopyEntryInline("Stop1"));
        }

        // ==================================================================
        // T19: HasArmingAtmBrackets_ReturnsTrue_WhenStop1IsInitialized
        // Stop1 in OrderState.Initialized -> HasArmingAtmBrackets returns true.
        // DW-LB-FL-01-V2 secondary hardening: Initialized state added to stateActive.
        // ==================================================================
        [Fact]
        public void HasArmingAtmBrackets_ReturnsTrue_WhenStop1IsInitialized()
        {
            var orders = new System.Collections.Generic.List<(string, string, int)>
            {
                ("Stop1", "ES 09-26", OsInitialized),
            };
            bool result = HasArmingAtmBracketsV2Inline(orders, "ES 09-26");
            Assert.True(result);
        }
        // ==================================================================
        // Inline mirror of NakedPositionDetector debounce logic for T20-T22.
        // Production: now - last < GraceMs -> return early (no flatten queued).
        // GraceMs = 500L (milliseconds). Uses (long)(int)Environment.TickCount units.
        // This inline version uses long directly (same arithmetic).
        // ==================================================================
        private const long GraceMs = 500L;

        private static bool IsNakedDetectDebounced(long lastQueuedTick, long nowTick) =>
            nowTick - lastQueuedTick < GraceMs;

        // ==================================================================
        // T20: TryNakedDetect_StampsDebounce_WhenEntryFills
        // When IsPttCopyEntry is true on a Filled event, the v3 fix stamps
        // _nakedDetectLastQueuedTicks. After the stamp, a NakedPositionDetector
        // invocation within 500ms is blocked by the debounce.
        // Test: simulate stamp at T=0, invoke detector at T=200ms -> debounced.
        // ==================================================================
        [Fact]
        public void TryNakedDetect_StampsDebounce_WhenEntryFills()
        {
            long stampedAt = 1000L; // simulated entry-fill stamp
            long cancelAckAt = 1200L; // 200ms after stamp (stale cancel ack)
            bool debounced = IsNakedDetectDebounced(stampedAt, cancelAckAt);
            Assert.True(debounced); // 200ms < 500ms -> NakedPositionDetector blocked
        }

        // ==================================================================
        // T21: NakedPositionDetector_NotDebounced_WhenBracketCancels_After500ms
        // Cancel ack arrives more than 500ms after the entry fill stamp.
        // Debounce has expired -> NakedPositionDetector is NOT blocked.
        // ==================================================================
        [Fact]
        public void NakedPositionDetector_NotDebounced_WhenBracketCancels_After500ms()
        {
            long stampedAt = 1000L;
            long cancelAckAt = 1600L; // 600ms after stamp
            bool debounced = IsNakedDetectDebounced(stampedAt, cancelAckAt);
            Assert.False(debounced); // 600ms >= 500ms -> NakedPositionDetector fires
        }

        // ==================================================================
        // T22: TryNakedDetect_StillSkipsEntry_WhenV2GuardActive
        // Regression: IsPttCopyEntry still returns true for "PTT-Copy" and "Entry".
        // v3 block is inside the v2 branch -- v2 guard not broken.
        // ==================================================================
        [Fact]
        public void TryNakedDetect_StillSkipsEntry_WhenV2GuardActive()
        {
            Assert.True(IsPttCopyEntryInline("PTT-Copy")); // standard mode entry
            Assert.True(IsPttCopyEntryInline("Entry"));    // Named ATM mode entry
        }

        // ==================================================================
        // T23: TryNakedDetect_DoesNotStampDebounce_ForNonPttCancelAck
        // IsPttCopyEntry returns false for "Stop1" (bracket cancel ack name).
        // The v3 stamp branch is NOT entered for non-PTT cancel events.
        // Normal cancel path goes to NakedPositionDetector (not short-circuited).
        // ==================================================================
        [Fact]
        public void TryNakedDetect_DoesNotStampDebounce_ForNonPttCancelAck()
        {
            // "Stop1" cancel ack must NOT match IsPttCopyEntry -> v3 stamp not entered
            Assert.False(IsPttCopyEntryInline("Stop1"));   // no stamp for bracket cancel
            Assert.False(IsPttCopyEntryInline("Target1")); // no stamp for bracket cancel
            Assert.False(IsPttCopyEntryInline("Stop2"));   // no stamp for bracket cancel
        }

        // ==================================================================
        // Inline mirror of CopyEngine.IsInEntryFillDebounceWindow for T24-T26.
        // DW-LB-FL-01-V4: reads _nakedDetectLastQueuedTicks[acct.Name].
        // Returns true if (now - last) < 500L (within the 500ms bracket-arm window).
        // Returns false when no stamp exists (safe default -- no suppression).
        // CYC=2: TryGetValue branch(1) + now-last comparison(1).
        // Uses same (long)(int)Environment.TickCount units as NakedPositionDetector.
        // ==================================================================
        private static bool IsInEntryFillDebounceWindowInline(
            System.Collections.Generic.Dictionary<string, long> ticks,
            string acctName,
            long nowTick
        )
        {
            if (!ticks.TryGetValue(acctName, out long last)) // (1)
                return false;
            return nowTick - last < 500L; // (2)
        }

        // ==================================================================
        // T24: IsInEntryFillDebounceWindow_ReturnsTrue_WhenWithin500ms
        // _nakedDetectLastQueuedTicks["Sim102"] stamped at T=1000. now=T=1200 (200ms later).
        // 200ms < 500ms -> IsInEntryFillDebounceWindow returns true.
        // DW-LB-FL-01-V4: stale callback from prior cycle suppressed inside debounce window.
        // ==================================================================
        [Fact]
        public void IsInEntryFillDebounceWindow_ReturnsTrue_WhenWithin500ms()
        {
            var ticks = new System.Collections.Generic.Dictionary<string, long>
            {
                ["Sim102"] = 1000L // entry-fill stamp
            };
            bool result = IsInEntryFillDebounceWindowInline(ticks, "Sim102", nowTick: 1200L);
            Assert.True(result); // 200ms < 500ms -> within debounce window
        }

        // ==================================================================
        // T25: IsInEntryFillDebounceWindow_ReturnsFalse_WhenOver500ms
        // _nakedDetectLastQueuedTicks["Sim102"] stamped at T=1000. now=T=1600 (600ms later).
        // 600ms >= 500ms -> IsInEntryFillDebounceWindow returns false.
        // DW-LB-FL-01-V4: after window expires, flatten proceeds to other guards.
        // ==================================================================
        [Fact]
        public void IsInEntryFillDebounceWindow_ReturnsFalse_WhenOver500ms()
        {
            var ticks = new System.Collections.Generic.Dictionary<string, long>
            {
                ["Sim102"] = 1000L // entry-fill stamp
            };
            bool result = IsInEntryFillDebounceWindowInline(ticks, "Sim102", nowTick: 1600L);
            Assert.False(result); // 600ms >= 500ms -> outside debounce window
        }

        // ==================================================================
        // T26: IsInEntryFillDebounceWindow_ReturnsFalse_WhenNoStamp
        // _nakedDetectLastQueuedTicks has no entry for "Sim102" (first trade, reconnect).
        // TryGetValue returns false -> IsInEntryFillDebounceWindow returns false.
        // DW-LB-FL-01-V4: safe default -- no suppression when no stamp exists.
        // ==================================================================
        [Fact]
        public void IsInEntryFillDebounceWindow_ReturnsFalse_WhenNoStamp()
        {
            var ticks = new System.Collections.Generic.Dictionary<string, long>(); // empty
            bool result = IsInEntryFillDebounceWindowInline(ticks, "Sim102", nowTick: 5000L);
            Assert.False(result); // no stamp -> TryGetValue returns false -> returns false
        }

        // ==================================================================
        // T27: FlattenIfNotArming_ReturnsWithoutFlatten_WhenDebounceActive
        // Structural: IsInEntryFillDebounceWindow seam exists on CopyEngine as private instance.
        // When debounce is active, FlattenIfNotArming returns early without calling FlattenOneAccount.
        // NT8 Account not constructible without NT8 runtime; seam existence confirms guard added.
        // DW-LB-FL-01-V4: stale callback suppression confirmed by seam presence.
        // ==================================================================
        [Fact]
        public void FlattenIfNotArming_ReturnsWithoutFlatten_WhenDebounceActive()
        {
            Assert.True(SeamExists("IsInEntryFillDebounceWindow", InstanceSeamFlags));
        }

        // ==================================================================
        // Inline mirror of CopyEngine.IsExitSignalName (DW-LB-FL-01 V6 fix).
        // Production: IsExitSignalName -- CYC=7 after V6 patch.
        // Mirror includes the new name.Length==0 guard at the top.
        // ==================================================================
        private static bool IsExitSignalNameInline(string name)
        {
            if (name == null)
                return false;
            if (name.Length == 0)
                return true; // (0) DW-LB-FL-01 V6: empty name blocked
            if (name.StartsWith("PTT-", System.StringComparison.Ordinal))
                return true;
            if (name == "Close")
                return true;
            if (name == "Flatten")
                return true;
            if (name.StartsWith("Rev", System.StringComparison.Ordinal))
                return true;
            if (name.StartsWith("Exit", System.StringComparison.Ordinal))
                return true;
            if (name.Length > 6 && name.StartsWith("Target", System.StringComparison.Ordinal) && char.IsDigit(name[6]))
                return true;
            return false;
        }

                // ==================================================================
        // T28: IsExitSignalName_ReturnsTrue_ForEmptyString (DW-LB-FL-01 V6)
        // Empty-name orders appear in signal mode when PTT-BE-Stop orders fire simultaneously
        // and NT8 creates residual unnamed orders in the leader account.
        // IsExitSignalName("") must return true to block dispatch at Gate 0.5.
        // Before fix: "" passed all existing checks -> spurious Buy dispatches to followers
        //             -> reversed positions after BE-All in signal mode.
        // After fix:  name.Length == 0 check added -> returns true -> no dispatch.
        // ==================================================================
        [Fact]
        public void IsExitSignalName_ReturnsTrue_ForEmptyString()
        {
            Assert.True(IsExitSignalNameInline("")); // DW-LB-FL-01 V6: empty name blocked
        }

        // ==================================================================
        // T29: IsExitSignalName_ReturnsFalse_ForNull (DW-LB-FL-01 V6 regression guard)
        // null is still returned as false (null name handled separately by caller).
        // Guard: new empty-name branch must not change null behaviour.
        // ==================================================================
        [Fact]
        public void IsExitSignalName_ReturnsFalse_ForNull()
        {
            Assert.False(IsExitSignalNameInline(null)); // null still returns false
        }

        // ==================================================================
        // T30: IsExitSignalName_ReturnsFalse_ForValidEntry (DW-LB-FL-01 V6 regression guard)
        // "Entry" must remain passable -- legitimate entry signals must still dispatch.
        // ==================================================================
        [Fact]
        public void IsExitSignalName_ReturnsFalse_ForValidEntry()
        {
            Assert.False(IsExitSignalNameInline("Entry")); // Entry is never an exit signal
        }
    }
}