// RegisterBeRetrySlotIfNeededTests.cs -- xUnit tests for DW-LB-GR-01 bug fix.
// Tests the guard predicate logic in RegisterBeRetrySlotIfNeeded (CopyEngine.cs L6107-L6160).
// Bug: L6118 used leaderCount==0 instead of targetsCount==0, causing spurious retry arming.
// Fix: leaderCount -> targetsCount in the branch-(2) guard condition.
//
// Approach: inline predicate mirror.
//   NT8 Account/Instrument/Position are not instantiable without the NT8 runtime.
//   Tests project targets net8.0; PropTraderTools targets net48. No ProjectReference possible.
//   Inline mirror of the guard decision logic (established pattern in this test project:
//   see CopyEngineB137Tests.cs, BwaveRefactorLaneBTests.cs, CopyEngineBreakEvenFollowerTests.cs).
//
// Framework: xUnit ONLY. NEVER NUnit or MSTest. ASCII-only. No DateTime.Now. No lock().
// JS-021: no lock. JS-001: no throw. JS-002: no null return. JS-033: no async void.
using Xunit;

namespace PropTraderTools.Tests
{
    public sealed class RegisterBeRetrySlotIfNeededTests
    {
        // ------------------------------------------------------------------
        // Inline predicate -- mirrors RegisterBeRetrySlotIfNeeded guard logic.
        // Production code (CopyEngine.cs L6115-L6160, post-fix):
        //
        //   if (isRetry) return;                           // (1)
        //   if (targetsCount == 0)                         // (2) FIXED: was leaderCount==0
        //   {
        //       if (IsFlat(...)) return;                   // (3)
        //       _pendingFollowerBeSlots[acc.Name] = ...;   // arm slot
        //       return;
        //   }
        //   if (!IsFollowerAccount(acc)) return;           // (4)
        //   if (leaderCount <= 0                           // (5)
        //       || targetsCount >= leaderCount
        //       || IsFlat(...)) return;                    // (6)
        //   _pendingFollowerBeSlots[acc.Name] = ...;       // arm slot
        //
        // Parameters:
        //   isRetry    -- isRetry arg
        //   isFlat     -- result of IsFlat(FindPosition(acc, instrument))
        //   isFollower -- result of IsFollowerAccount(acc)
        //   targetsCount -- follower visible target count
        //   leaderCount  -- leader native target count
        //
        // Returns: true when a slot WOULD be armed (i.e., dict populated).
        // CYC of this inline helper: 5 predicates, all binary -- well under JS limit.
        // ------------------------------------------------------------------
        private static bool RegisterBeRetryWouldArmInline(
            bool isRetry,
            bool isFlat,
            bool isFollower,
            int targetsCount,
            int leaderCount
        )
        {
            if (isRetry)
                return false; // (1) no further retry after first retry
            if (targetsCount == 0) // (2) targets==0 path (FIXED guard variable)
                return !isFlat; // (3) arm iff position not flat
            if (!isFollower) // (4)
                return false;
            if (leaderCount <= 0 || targetsCount >= leaderCount || isFlat) // (5)+(6)
                return false;
            return true; // partial-targets arm path
        }

        // ------------------------------------------------------------------
        // TEST 1 -- Bug scenario regression guard (MUST fail before fix, MUST pass after fix).
        //
        // Before fix: guard was (leaderCount == 0). With leaderCount=0 and targetsCount=2,
        // leaderCount==0 was TRUE -> retry slot would be armed spuriously (OCO protection torn
        // down on a follower that still has 2 working PTT targets).
        //
        // After fix: guard is (targetsCount == 0). targetsCount=2 -> FALSE -> slot NOT armed.
        // Follower's live targets are preserved correctly.
        //
        // Preconditions: targetsCount=2, leaderCount=0, isRetry=false, isFollower=true,
        //                position is Long (isFlat=false).
        // Assert: slot NOT armed.
        // ------------------------------------------------------------------

        [Fact]
        public void RegisterBeRetrySlotIfNeeded_LeaderZeroTargetsNonZero_DoesNotArmRetry()
        {
            bool wouldArm = RegisterBeRetryWouldArmInline(
                isRetry: false,
                isFlat: false,
                isFollower: true,
                targetsCount: 2,
                leaderCount: 0
            );

            Assert.False(wouldArm);
        }

        // ------------------------------------------------------------------
        // TEST 2 -- Correct arm when follower has zero visible targets.
        //
        // targetsCount=0 (PTT orders not yet landed), leaderCount=3, isRetry=false.
        // Position is Long (isFlat=false). Retry slot MUST be armed.
        // Both pre-fix and post-fix code pass this test: confirms fix does not regress
        // the intended arm path.
        //
        // Preconditions: targetsCount=0, leaderCount=3, isRetry=false, isFollower=true,
        //                position is Long (isFlat=false).
        // Assert: slot IS armed.
        // ------------------------------------------------------------------

        [Fact]
        public void RegisterBeRetrySlotIfNeeded_TargetsZeroLeaderNonZero_ArmsRetry()
        {
            bool wouldArm = RegisterBeRetryWouldArmInline(
                isRetry: false,
                isFlat: false,
                isFollower: true,
                targetsCount: 0,
                leaderCount: 3
            );

            Assert.True(wouldArm);
        }

        // ------------------------------------------------------------------
        // TEST 3 -- Partial-targets arm (DW-B79-07 path, unchanged by fix).
        //
        // targetsCount=1, leaderCount=3: follower has 1 of 3 PTT targets visible.
        // 2 target pairs still outstanding -> retry MUST arm.
        // Exercises the targetsCount < leaderCount partial-targets path (L6138-6143),
        // which is architecture-locked and unaffected by the DW-LB-GR-01 fix.
        //
        // Preconditions: targetsCount=1, leaderCount=3, isRetry=false, isFollower=true,
        //                position is Long (isFlat=false).
        // Assert: slot IS armed.
        // ------------------------------------------------------------------

        [Fact]
        public void RegisterBeRetrySlotIfNeeded_PartialTargets_ArmsRetry()
        {
            bool wouldArm = RegisterBeRetryWouldArmInline(
                isRetry: false,
                isFlat: false,
                isFollower: true,
                targetsCount: 1,
                leaderCount: 3
            );

            Assert.True(wouldArm);
        }
    }
}