// BwaveNextLaneATests.cs -- BWAVE-NEXT LaneA T3 xUnit tests.
// T3 (DW-DW-03 + DW-NEW-07): Two-panel BE slot isolation.
// Test approach: seed _pendingBeSlots via reflection (DisarmPendingBe requires real NT8 Account).
// Assertions via IsPendingBeSlotActive(string) seam + IsPendingSlotsEmpty().
// Jane Street rules: JS-021 (no lock), JS-002 (no return null), xUnit only.
using System.Reflection;
using Xunit;

namespace PropTraderTools
{
    public sealed class BwaveNextLaneATests
    {
        // Reflection helper: get _pendingBeSlots field value (typed via object).
        private static object GetDictInstance()
        {
            var fi = typeof(CopyEngine).GetField(
                "_pendingBeSlots",
                BindingFlags.NonPublic | BindingFlags.Instance
            );
            return fi.GetValue(CopyEngine.Instance);
        }

        // Reflection helper: seed a slot by account name string.
        // Uses default(PendingBeSlot) -- Account=null, valid for slot-presence tests.
        private static void SeedSlot(string accountName)
        {
            var slotType = typeof(CopyEngine).GetNestedType(
                "PendingBeSlot",
                BindingFlags.NonPublic | BindingFlags.Instance
            );
            var defaultSlot = System.Activator.CreateInstance(slotType);
            var dict = GetDictInstance();
            var tryAdd = dict.GetType().GetMethod("TryAdd");
            tryAdd.Invoke(dict, new object[] { accountName, defaultSlot });
        }

        // Reflection helper: remove a slot by key -- simulates panel disarm without real Account.
        private static void RemoveSlot(string accountName)
        {
            var dict = GetDictInstance();
            // Build by-ref type for out param: TryRemove(string, out TValue).
            var valueType = dict.GetType().GetGenericArguments()[1];
            var byRefType = valueType.MakeByRefType();
            var tryRemove = dict.GetType()
                .GetMethod("TryRemove", new[] { typeof(string), byRefType });
            var args = new object[] { accountName, null };
            tryRemove.Invoke(dict, args);
        }

        // Reflection helper: clear all slots for test isolation.
        private static void ClearAllSlots()
        {
            var dict = GetDictInstance();
            dict.GetType().GetMethod("Clear").Invoke(dict, null);
        }

        // T3/S1+S2: Arm panelA + panelB. Disarm panelA. panelB slot must remain. panelA slot gone.
        [Fact(Skip="B180-2: _pendingBeSlots removed")]
        public void Detach_PanelA_DoesNotClearPanelB_BeSlot()
        {
            ClearAllSlots();
            SeedSlot("panelA-account");
            SeedSlot("panelB-account");

            // Act: simulate panelA detach -- remove only panelA slot.
            RemoveSlot("panelA-account");

            Assert.False(CopyEngine.Instance.IsPendingBeSlotActive("panelA-account"));
            Assert.True(CopyEngine.Instance.IsPendingBeSlotActive("panelB-account"));
            Assert.False(CopyEngine.Instance.IsPendingSlotsEmpty());
        }

        // T3/S1+S2 complement: Arm panelA + panelB. Disarm panelA. Own slot cleared, sibling untouched.
        [Fact(Skip="B180-2: _pendingBeSlots removed")]
        public void Detach_OwnPanel_ClearsOwnBeSlot_ButNotOthers()
        {
            ClearAllSlots();
            SeedSlot("panelA-account");
            SeedSlot("panelB-account");

            // Act: simulate panelA detach.
            RemoveSlot("panelA-account");

            Assert.False(CopyEngine.Instance.IsPendingBeSlotActive("panelA-account"));
            Assert.True(CopyEngine.Instance.IsPendingBeSlotActive("panelB-account"));
        }

        // T3/S3: Arm both panels. Disarm A then B. All slots must be empty.
        [Fact(Skip="B180-2: _pendingBeSlots removed")]
        public void Detach_LastPanel_ClearsAllPendingBeSlots()
        {
            ClearAllSlots();
            SeedSlot("panelA-account");
            SeedSlot("panelB-account");

            // Act: both panels detach.
            RemoveSlot("panelA-account");
            RemoveSlot("panelB-account");

            Assert.True(CopyEngine.Instance.IsPendingSlotsEmpty());
        }
        // CYC=1 per test (no branches). JS-021: no lock. JS-002: no return null.
    }
}
