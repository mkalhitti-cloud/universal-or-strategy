# Block B157 -- Engineer Tickets
# ptt-architect Phase 3 output
# Plan status at write time: REVIEW_PASS (Cycle 2)
# Produced: 2026-08-10

---

## Ticket Index

| Ticket | Priority | Defect Cards | Files |
|---|---|---|---|
| B157-T1 | P0 | DW-B157-RG-01 | CopyEngine.cs, CopyEngineTests.cs |
| B157-T2 | P1/P2 | DW-B156-DEFER-1, DW-B153-05, DW-B153-06, DW-B156-DEFER-2 | CopyEngine.cs only |
| B157-T3 | P3 | DW-B156-DEFER-3 | CopyEngineTests.cs only |

Ticket dependency: T1 is P0 and should be implemented first. T2 and T3 are independent
of each other and of T1 -- all three may land in the same PR in any implementation order.

---
---

# B157-T1 (P0)

## Title
DW-B157-RG-01: IsReversalToFlatFollower logic inversion fix + regression tests

## Priority
P0

## Defect Cards Closed
- DW-B157-RG-01

## Files Touched
- `src/PropTraderTools/CopyEngine.cs`  (1-character change, line 6527)
- `src/PropTraderTools/CopyEngineTests.cs`  (1 test modified + 3 tests added)

## Scope Lock
This ticket touches ONLY:
- `CopyEngine.cs` line 6527 (single character `!` insertion -- no other lines in this file)
- `CopyEngineTests.cs` lines 8103-8125 (existing test rename + comment + assertion flip)
- `CopyEngineTests.cs` immediately after line 8125 (3 new [Fact] methods inserted)

Do NOT touch any other lines. Do NOT reformat surrounding code.

---

## Spec Requirements Satisfied
DW-B157-RG-01: `IsReversalToFlatFollower` returns `true` (skip) when follower is flat + direction
changed -- that is exactly the safe-to-copy case. The fix inverts the `followerIsFlat` predicate
so that `true` (skip) fires only when the follower has an open position and direction changed.

Corrected truth table (post-fix):

| followerIsFlat | currentAction != lastAction | Returns | Meaning |
|---|---|---|---|
| true  | true  | false | Flat follower + reversal = ALLOW copy |
| false | true  | true  | Open follower + reversal = SKIP (protection) |
| true  | false | false | Flat + same direction = ALLOW copy |
| false | false | false | Open + same direction = ALLOW copy |

---

## Change 1 of 5: CopyEngine.cs line 6527 -- logic inversion

File: `src/PropTraderTools/CopyEngine.cs`

**BEFORE (line 6527):**
```csharp
            return currentAction != lastAction && followerIsFlat;
```

**AFTER (line 6527):**
```csharp
            return currentAction != lastAction && !followerIsFlat;
```

Change size: insert one character `!` before `followerIsFlat`. Nothing else on this line or
in this method changes. Method signature (lines 6521-6525) is unchanged.

Verification: after edit `grep -n "followerIsFlat" src/PropTraderTools/CopyEngine.cs` must show
`!followerIsFlat` at line 6527.

Method signature (unchanged):
```csharp
// src/PropTraderTools/CopyEngine.cs ~line 6521
// CYC=2: base(1) + &&(1). internal static. No lock. No throw. ASCII-only.
internal static bool IsReversalToFlatFollower(
    OrderAction currentAction,
    OrderAction lastAction,
    bool followerIsFlat
)
```

CYC after fix: base(1) + `&&`(1) = 2. PASS (<=8).

---

## Change 2 of 5: CopyEngineTests.cs -- rename existing test + flip assertion + update comment

File: `src/PropTraderTools/CopyEngineTests.cs`

Current lines 8103-8125 (verified by plan-reviewer):
```csharp
        // PTT-REPAIRS-03 T1: verify reversal guard does NOT skip when follower has working entry orders.
        // BUG-A fix: followerIsFlat = IsFlat(pos) && !HasWorkingEntries() -- when HasWorkingEntries
        // returns true, followerIsFlat = false, and IsReversalToFlatFollower returns false (no skip).
        // Tests the sub-predicate IsReversalToFlatFollower(Buy, Sell, followerIsFlat: false) directly
        // (internal static, no NT8 Account/Instrument construction required).
        // This is Option B from the ticket spec -- pure predicate test, no mocking framework.
        [Fact(Skip = "NT8-runtime: CopyEngine.cctor requires NT8 host")]
        public void ShouldSkipForReversalGuard_AllowsEntryWhenFollowerHasWorkingOrders()
        {
            // Arrange: reversal scenario (Buy after Sell), but followerIsFlat=false because
            // HasWorkingEntries() returns true (working entry exists).
            // With the BUG-A fix: followerIsFlat = IsFlat(pos: true) && !HasWorkingEntries(true) = false.
            const OrderAction currentAction = OrderAction.Buy;
            const OrderAction lastAction    = OrderAction.Sell;
            const bool followerIsFlat       = false; // working order exists -> not truly flat

            // Act: IsReversalToFlatFollower is internal static; accessible via InternalsVisibleTo L46.
            // When followerIsFlat=false, reversal condition (cur != last && isFlat) is false.
            bool wouldSkip = CopyEngine.IsReversalToFlatFollower(currentAction, lastAction, followerIsFlat);

            // Assert: guard must NOT skip -- dispatch is allowed when follower has working orders.
            Assert.False(wouldSkip);
        }
```

**AFTER (replace lines 8103-8125 with):**
```csharp
        // DW-B157-RG-01 T1: protection path -- skip=true when follower has open position + direction changed.
        // followerIsFlat=false (open position), reversal (Buy after Sell).
        // Post-fix: currentAction != lastAction (true) && !followerIsFlat (true) = true = skip. CORRECT.
        // Pre-fix this returned false (no skip) due to inverted predicate -- follower with open position
        // would receive a reversal copy and attempt a bracket against an already-open position.
        [Fact(Skip = "NT8-runtime: CopyEngine.cctor requires NT8 host")]
        public void ShouldBlockReversalCopy_WhenFollowerHasOpenPosition()
        {
            // Arrange: reversal scenario (Buy after Sell), follower has an open position (not flat).
            const OrderAction currentAction = OrderAction.Buy;
            const OrderAction lastAction    = OrderAction.Sell;
            const bool followerIsFlat       = false; // open position exists

            // Act
            bool wouldSkip = CopyEngine.IsReversalToFlatFollower(currentAction, lastAction, followerIsFlat);

            // Assert: protection path -- guard MUST skip to preserve the open bracket.
            Assert.True(wouldSkip);
        }
```

Key changes to existing test:
1. Comment block (lines 8103-8108): replaced with DW-B157-RG-01 protection-path description
2. Method name: `ShouldSkipForReversalGuard_AllowsEntryWhenFollowerHasWorkingOrders` -> `ShouldBlockReversalCopy_WhenFollowerHasOpenPosition`
3. Arrange comment: updated to match new semantics (open position, not working-orders logic)
4. Assert comment: updated to protection-path description
5. `Assert.False(wouldSkip)` -> `Assert.True(wouldSkip)`

---

## Change 3 of 5: CopyEngineTests.cs -- add IsReversalToFlatFollower_FlatFollower_DirectionChanged_ReturnsFalse

Insert immediately after the closing `}` of `ShouldBlockReversalCopy_WhenFollowerHasOpenPosition`
(after the modified block ending at ~line 8125).

```csharp
        // DW-B157-RG-01 T2 (REGRESSION PROOF): flat follower + reversal must return false (allow copy).
        // Pre-fix, this input returned true (BUG): followerIsFlat=true, direction changed.
        // A flat follower has no live brackets; any direction is safe to copy.
        // If this test fails after implementation, the bug fix was not applied correctly.
        [Fact(Skip = "NT8-runtime: CopyEngine.cctor requires NT8 host")]
        public void IsReversalToFlatFollower_FlatFollower_DirectionChanged_ReturnsFalse()
        {
            // Arrange: flat follower (no open position), reversal (Buy after Sell).
            const OrderAction currentAction = OrderAction.Buy;
            const OrderAction lastAction    = OrderAction.Sell;
            const bool followerIsFlat       = true; // flat: no open position, no live brackets

            // Act
            bool result = CopyEngine.IsReversalToFlatFollower(currentAction, lastAction, followerIsFlat);

            // Assert: flat follower reversal must be ALLOWED (false = no skip).
            Assert.False(result);
        }
```

---

## Change 4 of 5: CopyEngineTests.cs -- add IsReversalToFlatFollower_OpenFollower_DirectionChanged_ReturnsTrue

Insert immediately after `IsReversalToFlatFollower_FlatFollower_DirectionChanged_ReturnsFalse`.

```csharp
        // DW-B157-RG-01 T3: open follower + reversal must return true (protection path).
        // Follower has a live Short position; leader issues a Long (reversal).
        // Copying would attempt a new bracket against an open Short -- correctly blocked.
        [Fact(Skip = "NT8-runtime: CopyEngine.cctor requires NT8 host")]
        public void IsReversalToFlatFollower_OpenFollower_DirectionChanged_ReturnsTrue()
        {
            // Arrange: open follower (Short position), reversal (Sell->Buy direction change).
            const OrderAction currentAction = OrderAction.Sell;
            const OrderAction lastAction    = OrderAction.Buy;
            const bool followerIsFlat       = false; // open Short position

            // Act
            bool result = CopyEngine.IsReversalToFlatFollower(currentAction, lastAction, followerIsFlat);

            // Assert: open follower + reversal must be BLOCKED (true = skip).
            Assert.True(result);
        }
```

---

## Change 5 of 5: CopyEngineTests.cs -- add IsReversalToFlatFollower_SameDirection_ReturnsFalse

Insert immediately after `IsReversalToFlatFollower_OpenFollower_DirectionChanged_ReturnsTrue`.

```csharp
        // DW-B157-RG-01 T4: same direction never triggers the reversal guard, regardless of flat state.
        // currentAction == lastAction => (currentAction != lastAction) = false => result = false.
        [Fact(Skip = "NT8-runtime: CopyEngine.cctor requires NT8 host")]
        public void IsReversalToFlatFollower_SameDirection_ReturnsFalse()
        {
            // Arrange: same direction (Buy->Buy), follower has open position.
            const OrderAction currentAction = OrderAction.Buy;
            const OrderAction lastAction    = OrderAction.Buy;
            const bool followerIsFlat       = false; // open position -- worst case for non-reversal

            // Act
            bool result = CopyEngine.IsReversalToFlatFollower(currentAction, lastAction, followerIsFlat);

            // Assert: same direction = no reversal = ALLOW copy (false = no skip).
            Assert.False(result);
        }
```

---

## Method Signatures

```csharp
// UNCHANGED signature, body fix only:
// src/PropTraderTools/CopyEngine.cs line 6521
// CYC=2. internal static. No lock. No throw. ASCII-only.
internal static bool IsReversalToFlatFollower(
    OrderAction currentAction,
    OrderAction lastAction,
    bool followerIsFlat
)
```

## CYC Targets

| Method | CYC | Limit | Pass? |
|---|---|---|---|
| IsReversalToFlatFollower | 2 | 8 | YES |

## xUnit [Fact] Names (exact)

| # | Name | Assertion | Note |
|---|---|---|---|
| T1-1 | `ShouldBlockReversalCopy_WhenFollowerHasOpenPosition` | `Assert.True(wouldSkip)` | Renamed + flipped from pre-fix |
| T1-2 | `IsReversalToFlatFollower_FlatFollower_DirectionChanged_ReturnsFalse` | `Assert.False(result)` | Regression proof: pre-fix this returned true |
| T1-3 | `IsReversalToFlatFollower_OpenFollower_DirectionChanged_ReturnsTrue` | `Assert.True(result)` | Protection path confirmation |
| T1-4 | `IsReversalToFlatFollower_SameDirection_ReturnsFalse` | `Assert.False(result)` | Same direction = never skip |

All four tests carry `[Fact(Skip = "NT8-runtime: CopyEngine.cctor requires NT8 host")]`.
The method under test is `internal static` (accessible via InternalsVisibleTo at CopyEngine.cs:46);
no NT8 Account/Instrument construction is required.

## JS Rule Constraints

| Rule | Requirement | Status |
|---|---|---|
| JS-013 | CYC<=8 per method | IsReversalToFlatFollower CYC=2. PASS |
| JS-021 | No lock() | Static method, no shared state. PASS |
| JS-001 | No throw in hot paths | No throw added. PASS |
| ASCII-001 | ASCII-only string literals | All literals are ASCII-only. PASS |
| NT8-006 | No DateTime.Now | Not introduced. PASS |

---

## 7-Scan Checklist -- B157-T1

Engineer must self-report all 7. Verifier re-runs independently.

**SCAN-01: lock() check**
```powershell
Select-String -Path 'src\PropTraderTools\CopyEngine.cs' -Pattern '^\s+lock\s*\('
```
Required: 0 matches in executable code (comment-only matches acceptable).
Result: [PASS / FAIL -- count]

**SCAN-02: async void check**
```powershell
Select-String -Path 'src\PropTraderTools\CopyEngine.cs','src\PropTraderTools\CopyEngineTests.cs' -Pattern '\basync\s+void\b'
```
Required: 0 matches in executable code.
Result: [PASS / FAIL -- count]

**SCAN-03: DateTime.Now check**
```powershell
Select-String -Path 'src\PropTraderTools\CopyEngine.cs','src\PropTraderTools\CopyEngineTests.cs' -Pattern 'DateTime\.Now\b'
```
Required: 0 matches in executable code.
Result: [PASS / FAIL -- count]

**SCAN-04: non-ASCII byte check**
```powershell
[System.IO.File]::ReadAllBytes('src\PropTraderTools\CopyEngine.cs') | Where-Object { $_ -gt 127 } | Measure-Object | Select-Object -ExpandProperty Count
[System.IO.File]::ReadAllBytes('src\PropTraderTools\CopyEngineTests.cs') | Where-Object { $_ -gt 127 } | Measure-Object | Select-Object -ExpandProperty Count
```
Required: 0 for both files.
Result: [PASS / FAIL]

**SCAN-05: CYC scan (Lizard or manual McCabe)**
```powershell
lizard src/PropTraderTools/CopyEngine.cs --CCN 8 -f IsReversalToFlatFollower
```
Required: IsReversalToFlatFollower CCN<=8 (expected 2).
Manual count: base(1) + &&(1) = 2.
Result: [PASS / FAIL -- actual CCN]

**SCAN-06: xUnit test run**
```powershell
dotnet test src/PropTraderTools.Tests/ --no-build --logger "console;verbosity=detailed"
```
Required: 0 test failures, 0 regressions. All 4 named [Fact] methods present in output
(they will appear as Skipped due to NT8-runtime Skip attribute -- skipped != failed).
Result: [PASS / FAIL -- failure count]

**SCAN-07: structural verification**
Verify each item by manual inspection (grep or diff):
- [ ] `CopyEngine.cs` line 6527 contains `!followerIsFlat` (not `followerIsFlat`)
- [ ] `CopyEngineTests.cs` contains method `ShouldBlockReversalCopy_WhenFollowerHasOpenPosition`
- [ ] `CopyEngineTests.cs` does NOT contain `ShouldSkipForReversalGuard_AllowsEntryWhenFollowerHasWorkingOrders` (old name removed)
- [ ] `CopyEngineTests.cs` contains `Assert.True(wouldSkip)` in `ShouldBlockReversalCopy_WhenFollowerHasOpenPosition`
- [ ] `CopyEngineTests.cs` contains method `IsReversalToFlatFollower_FlatFollower_DirectionChanged_ReturnsFalse`
- [ ] `CopyEngineTests.cs` contains method `IsReversalToFlatFollower_OpenFollower_DirectionChanged_ReturnsTrue`
- [ ] `CopyEngineTests.cs` contains method `IsReversalToFlatFollower_SameDirection_ReturnsFalse`
Result: [PASS / FAIL -- list any unchecked items]

---

## Post-Implementation Sync

After all .cs edits for this ticket:
```powershell
powershell -File .\deploy-sync.ps1
```
Required: zero errors from the sync script.

---
---

# B157-T2 (P1/P2)

## Title
DW-B156-DEFER-1 + DW-B153-05 + DW-B153-06 + DW-B156-DEFER-2:
TryEvictFollowerBeSlot decomposition + stale annotation fixes + dead code removal

## Priority
P1 (decomposition) / P2 (annotations + dead code)

## Defect Cards Closed
- DW-B156-DEFER-1: TryEvictFollowerBeSlot decomposition (CYC 14 -> 7)
- DW-B153-05: Stale CYC=4 annotation on CountLeaderTargets (lines 6639, 6646)
- DW-B153-06: Dead code removal UpdateLegTargetPrice (lines 3514-3532)
- DW-B156-DEFER-2: Stale CYC<=5 annotation on TrySetSlateReadyOnDrainFill (line 991)

## Files Touched
- `src/PropTraderTools/CopyEngine.cs` ONLY

## Scope Lock
This ticket touches ONLY:
- `CopyEngine.cs` lines 1925-1973 (TryEvictFollowerBeSlot -- annotation comment + method replaced with thin router)
- `CopyEngine.cs` immediately after line 1973 / before line 1975 (two new private helpers inserted)
- `CopyEngine.cs` line 991 (comment-only change: `CYC<=5` -> `CYC<=8`)
- `CopyEngine.cs` lines 6639 and 6646 (comment-only changes: `CYC=4`->`CYC=6`, `CYC<=5:`->`CYC<=6:`)
- `CopyEngine.cs` lines 3514-3532 inclusive (method + comment block deleted)

Do NOT touch any other lines. Do NOT reformat surrounding code. Do NOT modify CopyEngineTests.cs.

---

## Spec Requirements Satisfied

- DW-B156-DEFER-1: Parent `TryEvictFollowerBeSlot` had CYC=14 (confirmed by B156 T1 verifier).
  Extract two helpers to reduce parent to CYC<=8. No logic change. All log markers preserved.
- DW-B153-05: `CountLeaderTargets` annotations at lines 6639/6646 claimed CYC=4/CYC<=5.
  Strict McCabe (including base=1) yields CYC=6. Annotations were stale.
- DW-B153-06: `UpdateLegTargetPrice` (lines 3514-3532) has zero callers. Dead code removal.
- DW-B156-DEFER-2: `TrySetSlateReadyOnDrainFill` annotation at line 991 claimed CYC<=5.
  B156 T2 verifier counted CYC=8. Annotation was stale (8<=8 = still compliant, annotation wrong).

---

## Change 1 of 5 (DW-B156-DEFER-1): Replace TryEvictFollowerBeSlot body with thin router

File: `src/PropTraderTools/CopyEngine.cs`
Lines 1926-1973.

**BEFORE (lines 1926-1973 -- verified by plan + plan-reviewer):**
```
        //   Fix: evict slot AND reset attempt counter on Rejected for PTT-BE-Stop specifically.
        //   Flat-guard bypassed for Rejected: rejection can happen while position is still open
        //   (stop rejected but trade still live). The retry (500ms fallback) must be free to
        //   re-register a fresh slot.
        // Extracted: IsEvictTriggerState (terminal-state + name guard).
        // JS-021: ConcurrentDictionary ops are lock-free. JS-001: no throw. JS-002: void. ASCII.
        private void TryEvictFollowerBeSlot(OrderEventArgs e)
        {
            var o = e?.Order;
            if (o == null)
                return; // (1) null guard
            if (!IsEvictTriggerState(o))
                return; // (2) terminal-state gate
            _entryDispatchedOrders.Clear(); // DW-B95: instrument-level reset ...
            // DW-B156-01: leader-flat drain abort.
            // When leader's PTT-BE-Stop fills, the leader account is flat but follower drains are still pending.
            // Abort every follower drain whose Instrument matches this order so SubmitDrainedEntry never fires
            // for a now-flat leader position. Must run BEFORE the IsFollowerAccount early-return below.
            if (!IsFollowerAccount(o.Account) && o.OrderState == OrderState.Filled
                && IsFlat(FindPosition(o.Account, o.Instrument)))
            {
                int abortCount = 0;
                foreach (var kv in _pendingDispatchDrains)
                {
                    if (kv.Value.Instrument?.FullName == o.Instrument?.FullName)
                    {
                        AbortDrainOnFill(kv.Key);
                        abortCount++;
                    }
                }
                if (abortCount > 0)
                    NinjaTrader.Code.Output.Process(
                        "[DRAIN-ABORT-LEADER-FLAT] leader=" + o.Account.Name
                        + " instr=" + (o.Instrument?.FullName ?? "?")
                        + " aborted=" + abortCount,
                        NinjaTrader.NinjaScript.PrintTo.OutputTab1
                    );
            }
            if (!IsFollowerAccount(o.Account))
                return; // (3) follower-only evictions below
            bool isFilled = o.OrderState == OrderState.Filled;
            if (isFilled && !IsFlat(FindPosition(o.Account, o.Instrument)))
                return; // (4) flat-guard for Filled only
            string accName = o.Account.Name;
            bool slotEvicted = _pendingFollowerBeSlots.TryRemove(accName, out _);
            _beReplaceAttempts.TryRemove(accName, out _);
            _filledBeTargetCount.TryRemove(accName, out _);
            AbortDrainOnFill(accName);
            if (slotEvicted)
                LogBeSlotEviction(accName, o.OrderState == OrderState.Rejected);
        }
```

**AFTER (replace lines 1926-1973 -- thin router + two new helpers):**
```csharp
        //   Fix: evict slot AND reset attempt counter on Rejected for PTT-BE-Stop specifically.
        //   Flat-guard bypassed for Rejected: rejection can happen while position is still open
        //   (stop rejected but trade still live). The retry (500ms fallback) must be free to
        //   re-register a fresh slot.
        // Extracted: IsEvictTriggerState (terminal-state + name guard).
        // DW-B156-DEFER-1: decomposed IsLeaderFlatDrainAbort + RunFollowerEvictPath to reduce CYC.
        // CYC=7: e?.Order(1)+o==null(1)+IsEvictTriggerState(1)+o.Instrument?.FullName(1)+??(1)+IsFollowerAccount(1)=7
        // (base=1 + 6 decision points)
        // JS-021: no lock. JS-001: no throw. JS-002: void. ASCII-only.
        private void TryEvictFollowerBeSlot(OrderEventArgs e)
        {
            var o = e?.Order;                                                           // ?. (+1)
            if (o == null)                                                              // (+1)
                return;
            if (!IsEvictTriggerState(o))                                               // (+1)
                return;
            _entryDispatchedOrders.Clear(); // DW-B95: instrument-level reset -- fires for ALL accounts (leader + follower)
            string instrFull = o.Instrument?.FullName ?? string.Empty;                 // ?. (+1), ?? (+1)
            IsLeaderFlatDrainAbort(o, instrFull);
            if (!IsFollowerAccount(o.Account))                                         // (+1)
                return;
            RunFollowerEvictPath(o);
        }

        // DW-B156-DEFER-1: leader-flat drain abort path extracted from TryEvictFollowerBeSlot.
        // When leader's PTT-BE-Stop fills and leader is flat, aborts all pending follower drains
        // whose Instrument matches. Runs before follower-only eviction path.
        // CYC=8: base(1)+IsFollowerAccount(1)+OrderState(1)+IsFlat(1)+foreach(1)+if kv(1)+?.(1)+abortCount>0(1)=8
        // instrFull pre-computed by caller to avoid ?./?? cost inside this helper (JS-013).
        // JS-021: ConcurrentDictionary foreach is lock-free snapshot. JS-001: no throw. ASCII-only.
        private void IsLeaderFlatDrainAbort(Order o, string instrFull)
        {
            if (IsFollowerAccount(o.Account))                                           // guard: skip follower accounts (+1)
                return;
            if (o.OrderState != OrderState.Filled)                                     // (+1)
                return;
            if (!IsFlat(FindPosition(o.Account, o.Instrument)))                        // (+1)
                return;
            int abortCount = 0;
            foreach (var kv in _pendingDispatchDrains)                                 // (+1)
            {
                if (kv.Value.Instrument?.FullName == instrFull)                        // if (+1) + ?. (+1) = +2
                {
                    AbortDrainOnFill(kv.Key);
                    abortCount++;
                }
            }
            if (abortCount > 0)                                                         // (+1)
                NinjaTrader.Code.Output.Process(
                    "[DRAIN-ABORT-LEADER-FLAT] leader=" + o.Account.Name
                    + " instr=" + instrFull
                    + " aborted=" + abortCount,
                    NinjaTrader.NinjaScript.PrintTo.OutputTab1
                );
        }

        // DW-B156-DEFER-1: follower eviction path extracted from TryEvictFollowerBeSlot.
        // Handles flat-guard, slot eviction, retry reset, drain abort, log.
        // CYC=4: base(1)+if(isFilled&&...)(1)+&&(1)+if(slotEvicted)(1)=4.
        // JS-021: ConcurrentDictionary TryRemove is lock-free. JS-001: no throw. ASCII-only.
        private void RunFollowerEvictPath(Order o)
        {
            bool isFilled = o.OrderState == OrderState.Filled;
            if (isFilled && !IsFlat(FindPosition(o.Account, o.Instrument)))            // if(+1) + &&(+1)
                return;
            string accName = o.Account.Name;
            bool slotEvicted = _pendingFollowerBeSlots.TryRemove(accName, out _);
            _beReplaceAttempts.TryRemove(accName, out _);
            _filledBeTargetCount.TryRemove(accName, out _);
            AbortDrainOnFill(accName);
            if (slotEvicted)                                                            // (+1)
                LogBeSlotEviction(accName, o.OrderState == OrderState.Rejected);
        }
```

**Placement of new helpers:**
- `IsLeaderFlatDrainAbort` and `RunFollowerEvictPath` are inserted IMMEDIATELY after the closing
  `}` of `TryEvictFollowerBeSlot` (after original line 1973), BEFORE `LogBeSlotEviction`
  (which begins at original line 1975).
- Insert order: `IsLeaderFlatDrainAbort` first, then `RunFollowerEvictPath`.

**Log marker preservation:** The exact string `"[DRAIN-ABORT-LEADER-FLAT]"` from original
line 1952 is preserved verbatim in `IsLeaderFlatDrainAbort`. The log argument changes from
`o.Instrument?.FullName ?? "?"` to `instrFull` (pre-computed by parent) -- same value,
no `?.`/`??` cost inside the helper.

**No logic removed.** Every branch from the original lines 1934-1972 is accounted for in
the two helpers. Existing tests for `TryEvictFollowerBeSlot` and `DRAIN-ABORT` paths are
the regression gate (see SCAN-06).

---

## Change 2 of 5 (DW-B156-DEFER-2): Annotation fix at line 991

File: `src/PropTraderTools/CopyEngine.cs`

Line 991 -- comment-only change. Verified text before edit:
```
        // CYC<=5. JS-021: ConcurrentDictionary indexer is lock-free. JS-001: no throw. JS-002: void. ASCII-only.
```

**AFTER:**
```
        // CYC<=8. JS-021: ConcurrentDictionary indexer is lock-free. JS-001: no throw. JS-002: void. ASCII-only.
```

Change: `CYC<=5` -> `CYC<=8`. One token. No logic change. Zero behavior change.
Rationale: B156 T2 verifier strict McCabe: base(1)+order==null(1)+OrderState!=Filled(1)+
!StartsWith(1)+!IsFollowerAccount(1)+IsFlat(1)+?.(1)+??(1)=8. Annotation was under-counted.
8<=8 = still compliant.

---

## Change 3 of 5 (DW-B153-05): Annotation fix at line 6639

File: `src/PropTraderTools/CopyEngine.cs`

Line 6639 -- comment-only change. Verified text before edit:
```
        // CountLeaderTargets: CYC=4. Returns the number of Working native target limit orders
```

**AFTER:**
```
        // CountLeaderTargets: CYC=6. Returns the number of Working native target limit orders
```

Change: `CYC=4` -> `CYC=6`. One token. No logic change.

---

## Change 4 of 5 (DW-B153-05): Annotation fix at line 6646

File: `src/PropTraderTools/CopyEngine.cs`

Line 6646 -- comment-only change. Verified text before edit:
```
        // CYC<=5: rule null(1) + leader null(2) + foreach(3) + o null continue(4) + IsNativeLeaderTarget(5).
```

**AFTER:**
```
        // CYC<=6: rule null(1) + leader null(2) + foreach(3) + o null continue(4) + IsNativeLeaderTarget(5) + base(1).
```

Change: `CYC<=5:` -> `CYC<=6:`, append `+ base(1)` to breakdown. No logic change.
Rationale: Strict McCabe includes base=1; annotated formula was missing it.
base(1)+rule==null(1)+leader==null(1)+foreach(1)+o==null(1)+IsNativeLeaderTarget(1)=6.

---

## Change 5 of 5 (DW-B153-06): Remove dead code UpdateLegTargetPrice

File: `src/PropTraderTools/CopyEngine.cs`

**ENGINEER MUST RE-VERIFY BEFORE DELETING** (plan requires independent grep confirmation):
```powershell
Select-String -Path 'src\PropTraderTools\CopyEngine.cs' -Pattern 'UpdateLegTargetPrice'
Select-String -Path 'src\PropTraderTools\CopyEngineTests.cs' -Pattern 'UpdateLegTargetPrice'
```
Required: exactly 1 match in CopyEngine.cs (line 3519 = definition only), 0 matches in
CopyEngineTests.cs. If any additional callers are found, DO NOT delete -- escalate to
ptt-architect before proceeding.

Lines 3514-3532 (inclusive) verified by plan-reviewer Cycle 2. First line:
```
        // BWAVE-REFACTOR-LaneB-T5: extracted from CaptureOtherLegTargetPrices for-i body.
```
Last line (3532):
```
        }
```
Delete all 19 lines (3514 through 3532 inclusive), including the comment block and method body.
The blank line at 3513 and the comment at 3534 are NOT touched.

**No test changes required.** Zero test references confirmed by plan-reviewer.

---

## Method Signatures

```csharp
// MODIFIED: thin router -- CYC=7. private. void.
// src/PropTraderTools/CopyEngine.cs ~line 1926 (after edit, lines shift)
// JS-021: no lock. JS-001: no throw. JS-002: void. ASCII-only.
private void TryEvictFollowerBeSlot(OrderEventArgs e)

// NEW: extracted helper A -- CYC=8 (at limit). private. void.
// Placed immediately after TryEvictFollowerBeSlot, before LogBeSlotEviction.
// JS-021: ConcurrentDictionary foreach is lock-free. JS-001: no throw. ASCII-only.
private void IsLeaderFlatDrainAbort(Order o, string instrFull)

// NEW: extracted helper B -- CYC=4. private. void.
// Placed immediately after IsLeaderFlatDrainAbort.
// JS-021: ConcurrentDictionary TryRemove is lock-free. JS-001: no throw. ASCII-only.
private void RunFollowerEvictPath(Order o)
```

## CYC Targets

| Method | CYC Before | CYC After | Limit | Pass? |
|---|---|---|---|---|
| TryEvictFollowerBeSlot | 14 | 7 | 8 | YES |
| IsLeaderFlatDrainAbort | new | 8 | 8 | YES (at limit) |
| RunFollowerEvictPath | new | 4 | 8 | YES |
| CountLeaderTargets | annotation only | 6 (annotation corrected) | 8 | YES |
| TrySetSlateReadyOnDrainFill | annotation only | 8 (annotation corrected) | 8 | YES |
| UpdateLegTargetPrice | dead code removed | N/A | N/A | REMOVED |

## xUnit [Fact] Names
None. Pure structural extraction + annotation + dead-code. No new tests.

Regression gate: existing tests whose names contain `TryEvictFollowerBeSlot`, `DRAIN-ABORT`,
or `BeSlot` must all pass after implementation (SCAN-06).

## JS Rule Constraints

| Rule | Requirement | Status |
|---|---|---|
| JS-013 | CYC<=8 per method | All new/modified methods <= 8. See CYC table above. PASS |
| JS-021 | No lock() | No lock() added. ConcurrentDictionary throughout. PASS |
| JS-009 | No Dictionary for shared state | No new Dictionary<K,V> introduced. PASS |
| JS-001 | No throw in hot paths | No throw in any new/modified method. PASS |
| JS-002 | void return from new methods | Both new helpers are void. PASS |
| ASCII-001 | ASCII-only string literals | All literals (including log marker) are ASCII-only. PASS |
| NT8-006 | No DateTime.Now | Not introduced. PASS |

---

## 7-Scan Checklist -- B157-T2

Engineer must self-report all 7. Verifier re-runs independently.

**SCAN-01: lock() check**
```powershell
Select-String -Path 'src\PropTraderTools\CopyEngine.cs' -Pattern '^\s+lock\s*\('
```
Required: 0 matches in executable code.
Result: [PASS / FAIL -- count]

**SCAN-02: async void check**
```powershell
Select-String -Path 'src\PropTraderTools\CopyEngine.cs' -Pattern '\basync\s+void\b'
```
Required: 0 matches in executable code.
Result: [PASS / FAIL -- count]

**SCAN-03: DateTime.Now check**
```powershell
Select-String -Path 'src\PropTraderTools\CopyEngine.cs' -Pattern 'DateTime\.Now\b'
```
Required: 0 matches in executable code.
Result: [PASS / FAIL -- count]

**SCAN-04: non-ASCII byte check**
```powershell
[System.IO.File]::ReadAllBytes('src\PropTraderTools\CopyEngine.cs') | Where-Object { $_ -gt 127 } | Measure-Object | Select-Object -ExpandProperty Count
```
Required: 0.
Result: [PASS / FAIL]

**SCAN-05: CYC scan (Lizard or manual McCabe)**
```powershell
lizard src/PropTraderTools/CopyEngine.cs --CCN 8 -f TryEvictFollowerBeSlot -f IsLeaderFlatDrainAbort -f RunFollowerEvictPath
```
Required: TryEvictFollowerBeSlot<=8, IsLeaderFlatDrainAbort<=8, RunFollowerEvictPath<=8.
Expected: 7, 8, 4 respectively.
Result: [PASS / FAIL -- actual CCN values]

**SCAN-06: xUnit regression test run**
```powershell
dotnet test src/PropTraderTools.Tests/ --no-build --logger "console;verbosity=detailed"
```
Required: 0 test failures, 0 regressions.
Confirm passing: all tests containing `TryEvictFollowerBeSlot`, `DRAIN-ABORT`, or `BeSlot` in name.
Result: [PASS / FAIL -- failure count + list of regressions if any]

**SCAN-07: structural verification**
Verify each item by manual inspection:
- [ ] `TryEvictFollowerBeSlot` body is thin router (no inline abort logic, 8 lines body max)
- [ ] `IsLeaderFlatDrainAbort` method exists in file after `TryEvictFollowerBeSlot`
- [ ] `RunFollowerEvictPath` method exists in file after `IsLeaderFlatDrainAbort`
- [ ] Log marker `"[DRAIN-ABORT-LEADER-FLAT]"` still present in file (verbatim, in `IsLeaderFlatDrainAbort`)
- [ ] `UpdateLegTargetPrice` is ABSENT from file (grep returns 0 matches)
- [ ] Line containing `TrySetSlateReadyOnDrainFill` annotation reads `CYC<=8`
- [ ] Line 6639 annotation reads `CYC=6`
- [ ] Line 6646 annotation reads `CYC<=6`
Result: [PASS / FAIL -- list any unchecked items]

---

## Post-Implementation Sync

After all .cs edits for this ticket:
```powershell
powershell -File .\deploy-sync.ps1
```
Required: zero errors from the sync script.

---
---

# B157-T3 (P3)

## Title
DW-B156-DEFER-3: Exact-string assertion upgrade for [DRAIN-ABORT-LEADER-FLAT] log marker

## Priority
P3

## Defect Cards Closed
- DW-B156-DEFER-3

## Files Touched
- `src/PropTraderTools/CopyEngineTests.cs` ONLY

## Scope Lock
This ticket touches ONLY:
- `CopyEngineTests.cs`: one new [Fact] method added

The new test may be appended to the nearest logical region. The recommended placement is
immediately after the existing test at ~line 9379 (`TryEvictFollowerBeSlot_ShouldCallAbortDrain_WhenPositionFlat`)
or at the end of the same test class region. Do NOT modify any existing test. Do NOT touch CopyEngine.cs.

---

## Spec Requirements Satisfied

DW-B156-DEFER-3: The existing test at line 9364 asserts `Assert.Contains("DRAIN-ABORT-FLAT", src)`.
This is a substring match that would pass even if the log marker were changed to a different form
(e.g., `"[DRAIN-ABORT-FLAT-LEADER]"` or `"[DRAIN-ABORT-FLAT-v2]"`). The stronger assertion checks
the exact marker string `"[DRAIN-ABORT-LEADER-FLAT]"` including the leading bracket and the word
`LEADER`. The new test provides this stronger coverage as an ADDITION -- the existing test at line
9364 is NOT modified.

---

## Change 1 of 1: Add new [Fact] test method

File: `src/PropTraderTools/CopyEngineTests.cs`

**ADD the following method** in the `TryEvictFollowerBeSlot` test region (after ~line 9379):

```csharp
        // DW-B156-DEFER-3: exact-string assertion for DRAIN-ABORT-LEADER-FLAT log marker.
        // Stronger than existing test (~line 9364) which checks "DRAIN-ABORT-FLAT" (substring only).
        // This test checks the exact marker "[DRAIN-ABORT-LEADER-FLAT]" including brackets and LEADER.
        // Uses same file-read pattern as existing test at ~line 9357-9365.
        [Fact]
        public void TryEvictFollowerBeSlot_LeaderFlatDrainAbort_LogMarkerIsExactString()
        {
            // Arrange: resolve CopyEngine.cs source path via assembly location (same as ~line 9357).
            var srcFile = System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(typeof(CopyEngine).Assembly.Location) ?? string.Empty,
                "..", "..", "..", "..", "..", "src", "PropTraderTools", "CopyEngine.cs");
            srcFile = System.IO.Path.GetFullPath(srcFile);

            if (System.IO.File.Exists(srcFile))
            {
                // Act: read source.
                var src = System.IO.File.ReadAllText(srcFile);

                // Assert: exact marker string must be present (brackets, LEADER, hyphen positions).
                // Tests that IsLeaderFlatDrainAbort (extracted from TryEvictFollowerBeSlot, DW-B156-DEFER-1)
                // emits the precise marker string -- not just a substring.
                Assert.Contains("[DRAIN-ABORT-LEADER-FLAT]", src);
            }
            else
            {
                // Source file unavailable (CI without workspace): verify via reflection that
                // IsLeaderFlatDrainAbort exists as a private instance method (structural proxy).
                var m = typeof(CopyEngine).GetMethod(
                    "IsLeaderFlatDrainAbort",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                Assert.NotNull(m);
            }
        }
```

**Notes:**
- This test carries `[Fact]` (no Skip), matching the existing test at ~line 9381.
- The file-read pattern is identical to the existing test at lines 9357-9378 (same path construction).
- The assertion `Assert.Contains("[DRAIN-ABORT-LEADER-FLAT]", src)` verifies the exact string
  including leading `[`, word `LEADER`, and trailing `]`.
- The fallback (source unavailable) verifies `IsLeaderFlatDrainAbort` exists as a private method
  -- this provides structural proof that B157-T2 was implemented even when source is not on disk.
- The existing test `Assert.Contains("DRAIN-ABORT-FLAT", src)` at ~line 9364 is NOT modified.

---

## Method Signatures
No production method signatures -- test file only.

## CYC Targets
Not applicable. Test method. No CYC constraint on test bodies.

## xUnit [Fact] Names (exact)

| # | Name | [Fact] Attrs | Assert |
|---|---|---|---|
| T3-1 | `TryEvictFollowerBeSlot_LeaderFlatDrainAbort_LogMarkerIsExactString` | `[Fact]` (no Skip) | `Assert.Contains("[DRAIN-ABORT-LEADER-FLAT]", src)` |

## JS Rule Constraints

| Rule | Requirement | Status |
|---|---|---|
| ASCII-001 | ASCII-only string literals | All literals ASCII-only, including `"[DRAIN-ABORT-LEADER-FLAT]"`. PASS |
| JS-021 | No lock() | Test method uses no lock(). PASS |
| NT8-006 | No DateTime.Now | Not introduced. PASS |

---

## 7-Scan Checklist -- B157-T3

Engineer must self-report all 7. Verifier re-runs independently.

**SCAN-01: lock() check**
```powershell
Select-String -Path 'src\PropTraderTools\CopyEngineTests.cs' -Pattern '^\s+lock\s*\('
```
Required: 0 new matches (pre-existing matches, if any, acceptable).
Result: [PASS / FAIL -- new count]

**SCAN-02: async void check**
```powershell
Select-String -Path 'src\PropTraderTools\CopyEngineTests.cs' -Pattern '\basync\s+void\b'
```
Required: 0 matches.
Result: [PASS / FAIL -- count]

**SCAN-03: DateTime.Now check**
```powershell
Select-String -Path 'src\PropTraderTools\CopyEngineTests.cs' -Pattern 'DateTime\.Now\b'
```
Required: 0 new matches in executable code.
Result: [PASS / FAIL]

**SCAN-04: non-ASCII byte check**
```powershell
[System.IO.File]::ReadAllBytes('src\PropTraderTools\CopyEngineTests.cs') | Where-Object { $_ -gt 127 } | Measure-Object | Select-Object -ExpandProperty Count
```
Required: 0.
Result: [PASS / FAIL]

**SCAN-05: CYC scan**
Not applicable for test file. N/A.

**SCAN-06: xUnit test run**
```powershell
dotnet test src/PropTraderTools.Tests/ --no-build --logger "console;verbosity=detailed"
```
Required: 0 test failures. New test
`TryEvictFollowerBeSlot_LeaderFlatDrainAbort_LogMarkerIsExactString` must appear as PASSED
(not skipped -- no Skip attribute on this test).
Result: [PASS / FAIL -- failure count; confirm new test appears as PASSED]

**SCAN-07: structural verification**
Verify:
- [ ] `CopyEngineTests.cs` contains method `TryEvictFollowerBeSlot_LeaderFlatDrainAbort_LogMarkerIsExactString`
- [ ] Method carries `[Fact]` with NO Skip attribute
- [ ] Method body contains `Assert.Contains("[DRAIN-ABORT-LEADER-FLAT]", src)`
- [ ] The exact string `"[DRAIN-ABORT-LEADER-FLAT]"` uses straight double-quotes (ASCII 34), not curly quotes
- [ ] Existing test at ~line 9364 (`Assert.Contains("DRAIN-ABORT-FLAT", src)`) is UNCHANGED
Result: [PASS / FAIL -- list any unchecked items]

---

## Post-Implementation Sync

After all .cs edits for this ticket:
```powershell
powershell -File .\deploy-sync.ps1
```
Required: zero errors from the sync script.

---
---

# SIM Validation Reference

## SIM Plan: DW-B157-RG-01 (P0 -- must be SIM-confirmed before merge)

1. Configure: 1 leader account + 1 follower account. Copy mode enabled.
2. Execute Long entry on leader. Verify: follower receives Long copy. No `[PTT-COPY-GUARD]` in output.
3. Close Long on leader. Confirm both accounts flat.
4. Execute Short entry on leader (reversal: prior=Long, current=Short).
   **POST-FIX PASS:** follower receives Short copy. `[PTT-COPY-GUARD]` does NOT appear.
   **FAIL (bug present):** `[PTT-COPY-GUARD] skip reversal entry` appears and follower does not receive Short.
5. Confirm follower holds Short position.
6. Close Short on leader. Confirm both accounts flat.
7. Execute another Short entry (same direction: prior=Short, current=Short).
   Verify: follower receives Short copy. No guard block.
8. PROTECTION PATH: with follower holding open Short, execute Long entry on leader.
   **POST-FIX PASS:** `[PTT-COPY-GUARD] skip reversal entry` APPEARS. Follower does NOT receive Long.
   Open Short position preserved on follower.

## SIM Plan: DW-B156-DEFER-1 (P1 -- regression verification only)

1. Execute BE (Break-Even) workflow to trigger TryEvictFollowerBeSlot via OnOrderUpdate.
2. Confirm `[DRAIN-ABORT-LEADER-FLAT]` fires when leader PTT-BE-Stop fills (leader flat).
3. Confirm BE slot eviction log fires when follower BE slot is evicted.
4. No new behavior expected. SIM is regression-only for T2.

---
---

# Carry-Forward (B158+)

Items OPEN at end of B157 -- not addressed by any ticket above:

| ID | Item | Priority |
|---|---|---|
| DW-B154-05 | IsPttCopyEntryFilled delegation refactor | P3 |
| DW-B154-06 | TryTriggerBeRecovery post-deployment monitor | P2 |
| DW-B155-01 | B155 SIM gate pending | P2 |
| DW-BUG-0911-A-01 | Mid-close race condition in OnOrderUpdate | P2 |

---

_Tickets authored by ptt-architect, Block B157, Phase 3._
_Source verified at write time: CopyEngine.cs lines 991, 1920-1975, 3514-3532, 6521-6528, 6635-6648._
_Test verified at write time: CopyEngineTests.cs lines 8103-8125, 9350-9425._
_Plan status: REVIEW_PASS (Cycle 2, ptt-plan-reviewer 2026-08-10)._
