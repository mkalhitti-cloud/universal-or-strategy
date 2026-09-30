# B35-LaneB Architecture Plan
# Block: B35 | DW-B32-queue | 5x P0 BE Defects (Pipeline Formalization)
# Status: REVIEW_PASS_PENDING
# Spec: specs/002-trade-copier-spec.html id="section-b35" (LaneB card)
# Date: 2026-07-23

---

## Section A — Executive Summary

### Purpose
Five P0 break-even defects (DW-B32-01b, DW-B32-02, DW-B32-04b, DW-B32-07, DW-B32-08)
were identified in Block 32 and applied to the working tree but never passed through
the ptt-orchestrator pipeline. This block formally documents the changes, creates
ticket-level contracts with 7-scan checklists, adds one [Fact] test per defect, and
verifies all scans pass before marking the lane complete.

### Working-Tree Status
All 5 fixes are ALREADY PRESENT in the source files. The pipeline job is NOT to
re-implement them — it is to:
1. Formally document what changed and why (this plan)
2. Generate tickets with SCAN-01..07 checklists (04-tickets.md, Phase 3)
3. Add [Fact] tests for each fix (ptt-engineer writes them)
4. Verify all 7 scans pass (ptt-verifier confirms)

### Merge Sequence Constraint
LaneA (DW-B34-01 bracket-cancel-trim-flatten) pushes first.
LaneB engineer rebases on LaneA's commit before pushing.
LaneB's build tag is the FINAL tag and supersedes LaneA.

### Scope (3 files, no new files)
- `src/PropTraderTools/CopyEngine.cs`
- `src/PropTraderTools/TradeCopierPanel.cs`
- `src/PropTraderTools/CopyEngineTests.cs`

---

## Section B — Defect-by-Defect Analysis

### B.1 — DW-B32-01b | IsStopAlreadyAtBe Short Branch

**File**: `CopyEngine.cs`
**Method**: `IsStopAlreadyAtBe(Order order, double newStop, bool isLong)` (line ~610)
**CYC**: 2 (null guard + long/short branch) — within limit

**Root Cause**:
Short branch comparison was inverted. For short positions the stop starts ABOVE the
entry price (initial loss stop). BE should move the stop DOWN to entry+buffer.
The idempotency check must return true (already at BE) when
`order.StopPrice <= newStop`, not `>= newStop`.

**Before (bugged)**:
```
// both long and short used the same >= comparison
if (isLong) return order.StopPrice >= newStop;
return order.StopPrice >= newStop;   // BUG: always skips short BE
```

**After (fixed — verified at line 614-616)**:
```csharp
if (isLong)
    return order.StopPrice >= newStop;   // long: stop >= BE level -- already protected
return order.StopPrice <= newStop;        // short: stop <= BE level -- already protected
```

**Example**: entry=7491.75, newStop=7491.50, initial loss stop=7500
- Old: 7500 >= 7491.50 = true → skipped (BUG: never moved stop to BE)
- New: 7500 <= 7491.50 = false → proceeds to acc.Change() (CORRECT)

**Comment block at lines 602-608** documents the fix rationale.

---

### B.2 — DW-B32-02 | MoveStopToBreakEven Accepted State Filter

**File**: `CopyEngine.cs`
**Method**: `MoveStopToBreakEven(Account acc, Instrument instrument, int bufferTicks)` (line ~1481)
**CYC**: 6 — within limit

**Root Cause**:
NT8 ATM bracket stops transition through Accepted state before reaching Working.
The original filter only accepted Working orders, causing BE to silently miss stops
that were freshly placed and sitting in the Accepted state.

**Before (bugged)**:
```
if (order.OrderState != OrderState.Working) continue;
```

**After (fixed — verified at lines 1511-1513)**:
```csharp
if (order.OrderState != OrderState.Working &&
    order.OrderState != OrderState.Accepted)
    continue;
```

**Comment at line 1509-1513** documents the fix: "DW-B32-02: NT8 ATM stops sit in
Accepted state after placement; Working comes later. Accept both."

---

### B.3 — DW-B32-04b | BeState.Connected Removed (CS0117 Compile Fix)

**File**: `TradeCopierPanel.cs`
**Location**: BeState enum (line ~269-273) and OnBeUp method (line ~844)
**CYC of OnBeUp**: 1 — within limit

**Root Cause**:
The BeState FSM had three states: Idle, Connected, Armed. The Connected state was
removed in B32 when the "buffer change triggers live reprice" behavior was removed
(DW-B32-04). Removing Connected left a dangling reference in OnBeUp which caused
CS0117 compile error.

**Before (bugged)**:
```
private enum BeState { Idle, Connected, Armed }
// OnBeUp referenced BeState.Connected
```

**After (fixed — verified at lines 269-273 + 844)**:
```csharp
private enum BeState
{
    Idle,    // BE button shows "BE +N" -- inactive
    Armed,   // Watching price; fires once when entry+buffer crossed; amber border
}
// OnBeUp comment: "B32: Connected state removed -- buffer change no longer triggers live reprice"
```

**No reference to BeState.Connected** exists anywhere in the codebase after the fix.
The BeState FSM is now a strict 2-state machine: Idle <-> Armed.

---

### B.4 — DW-B32-07 | IsAtmSlotName Guard in MoveStopToBreakEven

**File**: `CopyEngine.cs`
**Method**: `MoveStopToBreakEven(Account acc, Instrument instrument, int bufferTicks)` (line ~1522)
**CYC**: 6 (unchanged by this fix — guard replaces old Stop\d+ filter) — within limit

**Root Cause**:
Without an ATM slot filter, MoveStopToBreakEven would attempt acc.Change() on NT8
ATM-owned stops (Stop1, Stop2). Per NT8-046, acc.Change() on ATM-owned orders is
silently rejected — making BE a no-op for the leader account's ATM brackets.

The fix uses the existing `IsAtmSlotName` helper (which matches Stop\d+ / Target\d+)
to skip ATM-owned orders before the acc.Change() path.

**Architecture note on acc.Change() scope**:
The acc.Change() at line 1547 runs ONLY on orders that pass ALL guards including
!IsAtmSlotName. These are PTT-created stop orders (not ATM-owned). The leader account
uses SubmitBeStop (B33 architecture) which creates a new PTT-BE-Stop — this is not
subject to NT8-046. MoveStopToBreakEven is called for FOLLOWER accounts only
(BreakEven() line 1757 explicitly skips leader). This design is correct.

**After (fixed — verified at line 1522)**:
```csharp
if (IsAtmSlotName(order.Name))   // (5a) skip ATM-owned stops — NT8-046: acc.Change() rejected
    continue;
```

**Comment at lines 1518-1523** documents: "DW-B32-10: Restore Stop\d+ filter. Path A
(TriggerAtmBreakEven) confirmed non-functional for Sim accounts -- ServerStrategies not
null but yields nothing with usable Brackets. Path B skips ATM-owned stops: acc.Change()
on Stop1/Stop2 is silently rejected by NT8 ATM engine (NT8-046)."

---

### B.5 — DW-B32-08 | SubmitBeStop Unconditional in BreakEven Leader Path

**File**: `CopyEngine.cs`
**Method**: `BreakEven(Account leader, Instrument instrument, int bufferTicks)` (line ~1737)
**CYC**: 6 — within limit

**Root Cause**:
The concern was that MoveStopToBreakEven(leader, ...) was conditionally gated and might
be skipped, causing BE to be a no-op on the leader. In B33 the architecture changed:
the leader now uses SubmitBeStop (which creates a new PTT-BE-Stop), and
MoveStopToBreakEven is called for followers only.

**Current architecture (verified at lines 1737-1759)**:
```csharp
internal void BreakEven(Account leader, Instrument instrument, int bufferTicks)
{
    if (leader == null) { ... return; }           // (1) null guard
    var leaderPos = FindPosition(leader, instrument);
    if (!IsFlat(leaderPos))                        // (2) position open
    {
        // isLong ternary + tick-align
        SubmitBeStop(leader, instrument, newStop); // (4) leader: new PTT-BE-Stop (not subject to NT8-046)
    }
    foreach (var acc in AllAccounts(instrument))  // (5) follower fan-out
    {
        if (acc == leader) continue;               // (6) skip leader
        MoveStopToBreakEven(acc, instrument, bufferTicks); // followers: PTT-created stops
    }
}
```

**Why SubmitBeStop inside IsFlat guard is CORRECT**: SubmitBeStop submits a new order.
Submitting a stop on a flat position would create an orphan order. The IsFlat guard
prevents this. "Unconditional" means: given an open position, SubmitBeStop fires with
no additional conditional logic — it is the ONLY statement in the !IsFlat block.

---

## Section C — Test Plan (5 new [Fact] tests)

All tests go in `CopyEngineTests.cs`, appended after the last B34 test block (~line 2826).

### T1 — DW-B32-01b
```
[Fact]
public void IsStopAlreadyAtBe_Short_ReturnsTrueWhenStopAtOrBelowEntry()
```
**Assert**:
- Reflectively invoke private static `IsStopAlreadyAtBe` via `BindingFlags.NonPublic | Static`
- Short at-entry: StopPrice=7491.50, newStop=7491.50, isLong=false → returns true (already at BE)
- Short below-entry: StopPrice=7491.25, newStop=7491.50, isLong=false → returns true (already past BE)
- Short above-entry: StopPrice=7500.00, newStop=7491.50, isLong=false → returns false (not yet at BE)
- Null order: returns false (null guard)

### T2 — DW-B32-02
```
[Fact]
public void MoveStopToBreakEven_IncludesAcceptedOrders_InStateFilter()
```
**Assert**: Structural source verification that method body contains
`"OrderState.Accepted"` via source-code reflection inspection. Uses the existing
`GetMethod("MoveStopToBreakEven", NonPublic | Instance)` pattern (verifies fix text
is present without requiring NT8 infrastructure).

### T3 — DW-B32-04b
```
[Fact]
public void BeState_EnumHasExpectedValues()
```
**Assert**:
- `typeof(TradeCopierPanel).GetNestedType("BeState", NonPublic)` is not null
- Enum has value named "Idle"
- Enum has value named "Armed"
- Enum does NOT have value named "Connected" (CS0117 regression guard)

### T4 — DW-B32-07
```
[Fact]
public void MoveStopToBreakEven_SkipsAtmOrders_ViaIsAtmSlotNameGuard()
```
**Assert**: Structural verification that `MoveStopToBreakEven` method body contains
`"IsAtmSlotName"` — confirms the NT8-046 guard is present via method reflection
(consistent with existing T_B32_01..04 pattern in the test file).

### T5 — DW-B32-08
```
[Fact]
public void BreakEven_WithOpenPosition_CallsSubmitBeStop_Unconditionally()
```
**Assert**: Structural verification that `BreakEven` (three-parameter overload:
`Account, Instrument, int`) method body contains `"SubmitBeStop"` — confirms leader
BE mechanism is present and not removed. Uses `GetMethod` with `NonPublic | Instance`
and `new[] { typeof(Account), typeof(Instrument), typeof(int) }` param types.

---

## Section D — Build Tag Change

LaneA sets the tag to:
```
"PTT-COPIER B35 | bracket-cancel-trim-flatten | {LaneA-date}"
```

LaneB SUPERSEDES with the final tag (change line ~41 of CopyEngine.cs after rebase):
```csharp
internal const string Tag = "PTT-COPIER B35 | bracket-cancel + BE-fixes | {date}";
```

Where `{date}` = the date the LaneB ptt-engineer writes the commit (e.g. `2026-07-23`).

---

## Section E — Jane Street / NT8 Gate Pre-Verification

| Rule | Applies To | Status |
|------|-----------|--------|
| JS-021 lock() ban | IsStopAlreadyAtBe, MoveStopToBreakEven, BreakEven, OnBeUp | PASS — no lock() in any changed method |
| JS-002 return null | IsStopAlreadyAtBe (bool), MoveStopToBreakEven (void), BreakEven (void) | PASS — no null returns |
| JS-001 no throw in hot path | MoveStopToBreakEven try/catch wraps acc.Change() | PASS — exception caught and logged, never propagated |
| JS-033 async void ban | No async added in any fix | PASS |
| NT8-046 acc.Change() on ATM banned | DW-B32-07: IsAtmSlotName guard skips Stop1/Stop2; leader uses SubmitBeStop | PASS |
| NT8-003 no volatile | No new volatile fields | PASS |
| SCAN-06 no DateTime.Now | DateTime.UtcNow in SubmitBeStop OCO ID only (acceptable); no Now in changed lines | PASS |
| CYC <= 8 | IsStopAlreadyAtBe=2, MoveStopToBreakEven=6, BreakEven=6, OnBeUp=1 | PASS — all within limit |

**GATE RESULT: PASS (pre-verified against working-tree source)**

---

## Section F — Scope Constraint

| File | Changes | Permitted |
|------|---------|-----------|
| `CopyEngine.cs` | Line ~616 (short branch), lines 1511-1513 (state filter), line 1522 (IsAtmSlotName guard), lines 1737-1759 (BreakEven — already correct), line ~41 (build tag) | YES |
| `TradeCopierPanel.cs` | Lines 269-273 (BeState enum), line 844 (OnBeUp comment) | YES |
| `CopyEngineTests.cs` | 5 new [Fact] methods appended at end | YES |
| Any other file | — | BANNED |
| New .cs files | — | BANNED |

**No new files. No changes outside the 3 listed files.**

---

## Section G — Hard-Link Gate

Before any .cs edit, engineer MUST run:
```powershell
powershell -File scripts\verify_links.ps1
```
Gate must PASS. If it fails, stop and report — do not proceed with edits.

---

## PLAN STATUS: REVIEW_PASS_PENDING
