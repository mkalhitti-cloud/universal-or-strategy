# B119 Tickets -- DW-B128 Direction-Change Guard in DispatchCopy

**Block**: B119
**Defect**: DW-B128
**Plan**: `docs/brain/B119/02-architecture-plan.md` (REVIEW_PASS 2026-08-27)
**Status**: TICKETS_COMPLETE
**Author**: ptt-architect
**Date**: 2026-08-27

---

## Ticket Count: 1

This defect is a single cohesive change confined to one file (`CopyEngine.cs`) plus one new test
file (`B119Tests.cs`). No second concern exists. One ticket is the correct decomposition.

---

## T1 -- B119-T1: DW-B128 Direction-Change Guard in DispatchCopy

### 1. Spec Requirements

| Item | Reference |
|------|-----------|
| Defect ID | DW-B128 |
| Spec section | `specs/002-trade-copier-spec.html#section-dw-b128` |
| Prior guard (context) | `specs/002-trade-copier-spec.html#section-dw-b122` |
| Copy dispatch spec | `specs/002-trade-copier-spec.html#section-b8` |
| Architecture plan | `docs/brain/B119/02-architecture-plan.md` Sections 3.1-3.4 |
| JS-021 (no lock) | `docs/standards/jane-street/RULES_CATALOG.md` Rule JS-021 |
| CYC <= 8 standard | `docs/standards/jane-street/RULES_CATALOG.md` (Jane Street strict) |
| NT8 OrderAction enum | `docs/standards/NT8_FULL_REFERENCE.md` L854-859 |
| IsFlat live signature | `src/PropTraderTools/CopyEngine.cs` L3302 |
| FindPosition live sig | `src/PropTraderTools/CopyEngine.cs` L3348 |

---

### 2. Acceptance Criteria (behavioral)

1. **First entry (no prior direction)**: When the leader dispatches a Buy and
   `_lastLeaderDirection` has no key for that instrument, all followers receive the dispatch.
   The guard does NOT fire.

2. **Same direction repeat**: When the leader dispatches a Buy after a prior Buy,
   all followers receive the dispatch. The guard does NOT fire.

3. **Direction reversal -- follower is flat**: When the leader dispatches a Buy after
   a prior Sell and a follower is currently flat (no open position on that instrument),
   that follower is SKIPPED. The guard fires; `continue` is executed for that follower;
   a `[PTT-COPY-GUARD]` log line is emitted for that follower.

4. **Direction reversal -- follower has open position**: When the leader dispatches a Buy
   after a prior Sell and a follower has an open position on that instrument
   (`followerIsFlat = false`), that follower STILL receives the dispatch. The guard does
   NOT fire for a follower with an open trade.

5. **Per-follower independence**: Within a single dispatch call, each follower in
   `rule.FollowerAccounts` is evaluated independently. One follower being skipped does not
   affect other followers in the same loop iteration.

6. **Dictionary updated after the loop**: `_lastLeaderDirection[instr.FullName] = currentAction`
   executes once, AFTER the `foreach` loop closes, not inside it.

7. **No new lock()**: The field and all accesses use `ConcurrentDictionary` exclusively.
   Zero `lock()` statements introduced anywhere.

---

### 3. Method Signatures (exact)

#### 3a. New class-level field (add in CopyEngine class body)

```csharp
// B119: DW-B128 -- reversal entry guard.
// Keyed by instrument FullName, value is the last OrderAction dispatched for that instrument.
// ConcurrentDictionary: thread-safe without lock(). JS-021: no lock.
private readonly ConcurrentDictionary<string, OrderAction> _lastLeaderDirection
    = new ConcurrentDictionary<string, OrderAction>();
```

**Placement**: Near the other `ConcurrentDictionary` fields in the CopyEngine class body
(the plan identifies this area as ~L250 in the existing field block).

#### 3b. New internal static helper

```csharp
// B119: DW-B128 -- direction-change guard predicate.
// Returns true iff the current dispatch reverses the last direction AND the follower is flat.
// CYC=2 (one && expression in a single return). JS-001: no throw. JS-021: no lock. ASCII-only.
// internal static: directly callable from B119Tests.cs without reflection.
internal static bool IsReversalToFlatFollower(
    OrderAction currentAction,
    OrderAction lastAction,
    bool followerIsFlat)
```

**Return type**: `bool`
**Body**: `return currentAction != lastAction && followerIsFlat;`
**Placement**: After `IsFlat` at CopyEngine.cs L3305 (immediately following the closing `}` of
`IsFlat`).

#### 3c. DispatchCopy signature (UNCHANGED)

```csharp
private void DispatchCopy(Order order, CopyRule rule)
```

Only the **body** is modified (see Section 4). Signature, access modifier, and parameter types
are unchanged.

#### 3d. Existing helpers reused (DO NOT re-implement)

```csharp
// L3302 -- existing, no change
private static bool IsFlat(NinjaTrader.Cbi.Position pos)

// L3348 -- existing, no change
private Position FindPosition(Account acc, Instrument instrument)
```

---

### 4. Implementation Instructions

All changes are in `src/PropTraderTools/CopyEngine.cs` only (plus the new test file).
**DO NOT modify any other .cs file.**

---

#### Step 1 -- Add `_lastLeaderDirection` field

Find the existing `ConcurrentDictionary` field block in CopyEngine. Add the new field there:

```csharp
private readonly ConcurrentDictionary<string, OrderAction> _lastLeaderDirection
    = new ConcurrentDictionary<string, OrderAction>();
```

Verify: `grep -c "ConcurrentDictionary" src/PropTraderTools/CopyEngine.cs` count increases by 1.

---

#### Step 2 -- Add `IsReversalToFlatFollower` static helper

Insert the following method immediately after the closing `}` of `IsFlat` at CopyEngine.cs L3305:

```csharp
// B119: DW-B128 -- direction-change guard predicate.
// Returns true iff the current dispatch reverses the last direction AND the follower is flat.
// CYC=2 (one && expression in a single return). JS-001: no throw. JS-021: no lock. ASCII-only.
// internal static: directly callable from B119Tests.cs without reflection.
internal static bool IsReversalToFlatFollower(
    OrderAction currentAction,
    OrderAction lastAction,
    bool followerIsFlat)
{
    return currentAction != lastAction && followerIsFlat;
}
```

Verify: method is `internal static`, returns `bool`, body is a single `return` expression.

---

#### Step 3 -- Modify `DispatchCopy` body (L1784-L1869 in live source)

The live `foreach` block (L1824-L1868) currently has two separate skip guards:
- `if (acc == null)` at L1827-L1831
- `if (!PassesDailyCapCheck(acc))` at L1832-L1836

Apply these changes IN ORDER:

**3a. Before the `foreach` -- add instrument snapshot and direction lookup:**

Insert after the existing `int baseQty = ...` line (currently L1821) and before `int idx = 0;`:

```csharp
// B119: DW-B128 -- snapshot instrument and last direction once before the loop.
// TryGetValue is O(1) and allocation-free on ConcurrentDictionary.
OrderAction currentAction = order.OrderAction;
var instr = order.Instrument;
bool hasLastDirection = _lastLeaderDirection.TryGetValue(
    instr.FullName,
    out OrderAction lastAction);
```

**3b. Inside the `foreach` -- merge null+cap guards and add reversal guard:**

Replace the two consecutive separate guards (L1827-L1836):

```csharp
// REMOVE (L1827-L1831):
if (acc == null)
{
    idx++;
    continue;
}
// REMOVE (L1832-L1836):
if (!PassesDailyCapCheck(acc))
{
    idx++;
    continue;
}
```

With this merged compound guard followed immediately by the reversal guard:

```csharp
// Merged null + cap guard. Compound || = 1 McCabe branch (per project convention L1802).
// CYC budget: replaces 2 separate branches with 1 compound, freeing one slot for the guard below.
if (acc == null || !PassesDailyCapCheck(acc))
{
    idx++;
    continue;
}

// B119: DW-B128 reversal entry guard.
// Only fires when: (a) a prior direction exists for this instrument, AND
//                  (b) current direction differs from last, AND
//                  (c) this follower is flat (no open position).
// On first entry (hasLastDirection=false) guard cannot fire -- copy always proceeds.
bool followerIsFlat = IsFlat(FindPosition(acc, instr));
if (hasLastDirection && IsReversalToFlatFollower(currentAction, lastAction, followerIsFlat))
{
    NinjaTrader.Code.Output.Process(
        "[PTT-COPY-GUARD] skip reversal entry: "
            + acc.Name
            + " "
            + instr.FullName
            + " follower flat",
        NinjaTrader.NinjaScript.PrintTo.OutputTab1
    );
    idx++;
    continue;
}
```

**3c. After the `foreach` closing `}` -- update last direction:**

Insert immediately after the `foreach` closing brace (after current L1868 `}`):

```csharp
// B119: DW-B128 -- record direction dispatched for this instrument.
// Write happens AFTER the loop so all followers in this dispatch see the same lastAction.
_lastLeaderDirection[instr.FullName] = currentAction;
```

---

#### Step 4 -- Write test file `src/PropTraderTools/Tests/B119Tests.cs`

**Framework**: xUnit only. NEVER NUnit or MSTest.
**Class name**: `B119Tests`
**File**: `src/PropTraderTools/Tests/B119Tests.cs`

The test class MUST contain these 11 `[Fact]` methods (exact names):

##### Part A -- Pure unit tests for `IsReversalToFlatFollower` (no NT8 mocks; call directly)

| # | [Fact] Name | Inputs | Expected |
|---|-------------|--------|----------|
| A1 | `T_IsReversalToFlatFollower_SameDirection_Buy_NotFired` | Buy, Buy, flat=true | false |
| A2 | `T_IsReversalToFlatFollower_SameDirection_Sell_NotFired` | Sell, Sell, flat=true | false |
| A3 | `T_IsReversalToFlatFollower_Reversal_BuyToSell_FlatFollower_Fires` | Sell, Buy, flat=true | true |
| A4 | `T_IsReversalToFlatFollower_Reversal_SellToBuy_FlatFollower_Fires` | Buy, Sell, flat=true | true |
| A5 | `T_IsReversalToFlatFollower_Reversal_DirectionChange_NotFlat_NotFired` | Sell, Buy, flat=false | false |
| A6 | `T_IsReversalToFlatFollower_NoLastDirection_NotFired` | -- (hasLastDirection=false scenario; call helper with same action as workaround) | false |

> Note for A6: The "no last direction" case is controlled by `hasLastDirection` in DispatchCopy, not
> by the helper itself. Test A6 should verify that when `currentAction == lastAction` (the safe
> invariant for a first-dispatch placeholder), `IsReversalToFlatFollower` returns false regardless
> of flatness. This covers the "guard must not fire on first entry" invariant at the unit level.

##### Part B -- `_lastLeaderDirection` dictionary invariant tests (no NT8; no CopyEngine instance needed)

| # | [Fact] Name | Scenario | Expected |
|---|-------------|----------|----------|
| B1 | `T_DirDict_AbsentKey_TryGetValue_ReturnsFalse` | New dict, key "NQ 03-26 CME" absent | TryGetValue returns false |
| B2 | `T_DirDict_AfterWrite_KeyPresent_ReturnsBuy` | Write Buy for key | TryGetValue returns true, value=Buy |
| B3 | `T_DirDict_OverwriteUpdatesValue` | Write Buy then Sell for same key | TryGetValue returns Sell |

##### Part C -- `BuyToCover` / `SellShort` direction-change variants

| # | [Fact] Name | Inputs | Expected |
|---|-------------|--------|----------|
| C1 | `T_IsReversalToFlatFollower_BuyToCoverToSellShort_Flat_ReturnsTrue` | SellShort, BuyToCover, flat=true | true |
| C2 | `T_IsReversalToFlatFollower_SellShortToBuyToCover_Flat_ReturnsTrue` | BuyToCover, SellShort, flat=true | true |

**Total**: 11 `[Fact]` tests.

**Test isolation notes**:
- Part A tests: call `CopyEngine.IsReversalToFlatFollower(...)` directly (`internal static`,
  accessible from the test assembly via `InternalsVisibleTo` or same assembly).
- Part B tests: construct a `new ConcurrentDictionary<string, OrderAction>()` directly;
  no `CopyEngine` instance required.
- Part C tests: identical structure to Part A; call the same static helper.
- **Zero NT8 API calls in any test.** No `Account`, `Order`, or `Instrument` objects used.
- If NT8 types are not available in the test assembly, Part A, B, C tests still compile because
  `OrderAction` is an enum that can be referenced as `NinjaTrader.Cbi.OrderAction.Buy` etc.
  (it is a value type with no runtime NT8 dependency for pure value comparison).

---

### 5. 7-Scan Checklist (MANDATORY -- engineer contract)

Run all 7 scans after implementation. ALL must pass before closing this ticket.

```
SCAN-01 -- lock() audit
  Command: grep -r "lock(" src/PropTraderTools/CopyEngine.cs
  Expected: ZERO new lock() matches introduced by B119 changes.
            _lastLeaderDirection uses ConcurrentDictionary exclusively. No lock() anywhere.

SCAN-02 -- async void audit
  Command: grep -rn "async void " src/PropTraderTools/CopyEngine.cs
  Expected: ZERO new async void methods introduced by this ticket.

SCAN-03 -- return null audit
  Command: grep -rn "return null;" src/PropTraderTools/CopyEngine.cs
  Expected: ZERO new return null statements in IsReversalToFlatFollower or in the
            _lastLeaderDirection update block. (Pre-existing FindPosition return null at L3353
            is unchanged and exempt.)

SCAN-04 -- throw audit
  Command: grep -rn "throw " src/PropTraderTools/CopyEngine.cs
  Expected: ZERO new throw statements in IsReversalToFlatFollower or DispatchCopy modification.
            Helper returns bool with no throw path.

SCAN-05 -- ASCII audit
  Command: powershell -Command "$f='src/PropTraderTools/CopyEngine.cs'; [regex]::Matches([System.IO.File]::ReadAllText($f), '[^\x00-\x7F]').Count"
  Expected: ZERO non-ASCII characters introduced. Log string [PTT-COPY-GUARD] is 7-bit ASCII.
            All new identifiers (_lastLeaderDirection, IsReversalToFlatFollower, hasLastDirection,
            followerIsFlat, currentAction, lastAction, instr) are ASCII-only.

SCAN-06 -- CYC audit
  Command: python scripts/complexity_audit.py src/PropTraderTools/CopyEngine.cs
  Expected: DispatchCopy <= 8 (maintained via branch merge: branches 6+7 merged to 1 compound ||,
            reversal guard occupies freed slot, total = 8).
            IsReversalToFlatFollower <= 4 (body is a single && expression; CYC = 2 McCabe strict,
            upper bound 3 by tool counting; both well within limit).

SCAN-07 -- build audit
  Command: dotnet build src/PropTraderTools/PropTraderTools.csproj
  Expected: ZERO errors. ZERO new warnings. Build exits with code 0.
```

---

### 6. NT8 API Claims

| Claim | Verdict | Source |
|-------|---------|--------|
| `OrderAction` is `NinjaTrader.Cbi.OrderAction` enum | CONFIRMED | `docs/standards/NT8_FULL_REFERENCE.md` L854-859 |
| `IsFlat(NinjaTrader.Cbi.Position)` helper | EXISTING IN-FILE | `CopyEngine.cs` L3302 -- do not re-implement |
| `FindPosition(Account, Instrument)` helper | EXISTING IN-FILE | `CopyEngine.cs` L3348 -- do not re-implement |
| No new NT8 API surface introduced | CONFIRMED | `IsFlat` and `FindPosition` are already called in DispatchCopy context throughout CopyEngine.cs (25+ call sites per plan review) |

**Note**: `ConcurrentDictionary<string, OrderAction>` is a .NET BCL type, not an NT8 API.
No new NT8 namespace imports are required by this ticket.

---

### 7. JS Rule Compliance Attestation

| Rule | Requirement | This Ticket |
|------|-------------|-------------|
| JS-021 | No `lock()` anywhere | COMPLIANT -- `_lastLeaderDirection` is `ConcurrentDictionary`; `TryGetValue` and indexer-set are atomic. Zero `lock()` statements. |
| JS-001 | No `throw` in hot path | COMPLIANT -- `IsReversalToFlatFollower` has a single `return` expression; no throw path anywhere in the change. |
| JS-002 | No `return null` for missing values | COMPLIANT -- `TryGetValue` with `out` param is the correct pattern; no new `return null` sites. |
| JS-033 | No `async void` | COMPLIANT -- no new async methods introduced. |
| CYC <= 8 | All modified methods <= 8 branches | COMPLIANT -- `DispatchCopy` = 8 (branch merge documented in plan Section 3.3); `IsReversalToFlatFollower` = 2 (single && in one return). |
| ASCII-only | No Unicode in strings or identifiers | COMPLIANT -- `[PTT-COPY-GUARD]` log string is pure 7-bit ASCII; all new identifiers are ASCII-only. |
| No DateTime.Now | Use DateTime.UtcNow | NOT APPLICABLE -- no DateTime usage in this change. |
| PTT- prefix on CreateOrder | All order names prefixed | NOT APPLICABLE -- no `CreateOrder` calls in this change. |
| No FontFamily / hex colors | UI rules | NOT APPLICABLE -- no UI code in this change. |

---

### 8. Files Modified

| File | Change | Scope |
|------|--------|-------|
| `src/PropTraderTools/CopyEngine.cs` | Modify | 3 locations: (1) new field near existing ConcurrentDictionary fields; (2) `DispatchCopy` L1824-L1868 -- merge guards, add reversal guard, add pre-loop snapshots, add post-loop dict update; (3) new `IsReversalToFlatFollower` static helper after `IsFlat` at L3305 |
| `src/PropTraderTools/Tests/B119Tests.cs` | New file | 11 `[Fact]` tests; zero NT8 mocks; xUnit only |

**Not modified**: Any other `.cs` file, any panel file, any spec HTML file, any project file.

---

### 9. Spec Traceability Matrix

| Requirement | Addressed | Where |
|-------------|-----------|-------|
| DW-B128: block reversal entry to flat follower | YES | Step 3, reversal guard |
| Option A: direction-change guard with last-direction tracking | YES | Steps 1+3 (_lastLeaderDirection) |
| JS-021: no lock() | YES | ConcurrentDictionary throughout; SCAN-01 |
| JS-001: no throw in hot path | YES | IsReversalToFlatFollower returns bool; SCAN-04 |
| CYC <= 8 for all modified methods | YES | Branch merge + SCAN-06 |
| ASCII-only log output | YES | [PTT-COPY-GUARD] prefix; SCAN-05 |
| Reuse IsFlat, FindPosition | YES | Step 3 calls existing helpers at L3302, L3348 |
| Dictionary updated after loop | YES | Step 3c post-loop placement |
| Test: 11 [Fact] covering all direction combos | YES | Step 4, Parts A/B/C |
| Test: xUnit only (no NUnit/MSTest) | YES | Step 4 framework mandate |
| File scope: CopyEngine.cs + B119Tests.cs only | YES | Section 8 |
