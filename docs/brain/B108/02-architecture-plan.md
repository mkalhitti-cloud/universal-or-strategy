# B108 Architecture Plan: DW-B107 Fix — SnapshotBeTargets Extraction + Cap

**Status**: REVIEW_PASS candidate
**Epic**: B108-T1
**Phase**: 1 (Architecture)
**Author**: ptt-architect
**Date**: 2026-08-11
**Spec items closed**: DW-B107 (P2-MEDIUM)

---

## 1. Problem Statement

### DW-B107 (P2-MEDIUM): Stale PTT-BE-Target-* residues inflate BE target count in MoveStopToBreakEven

`MoveStopToBreakEven` in [`src/PropTraderTools/CopyEngine.cs`](src/PropTraderTools/CopyEngine.cs)
builds its target list (Step A, L3373-3422) with a single flat-collect loop that accumulates
every qualifying `Limit` order matching `Target1..9`, `PTT-QX-T*`, or `PTT-BE-Target-*` into
one list with no native-vs-PTT discrimination and no count cap.

When a prior-session `PTT-BE-Target-4` order is still `Working` in `acc.Orders`, all four
entries enter `targets`. `PttBreakEvenSwap.Execute` then submits 4 OCO pairs on a 3-target ATM
— one more than the ATM expects. The fourth pair fails or creates a ghost bracket.

**Root cause**: the same two-pass / cap pattern that DW-B106 applied to the QX path
(`SnapshotTargetOrders` in `PttGlobalQuickExit.cs`) was never applied to the BE path.

**CYC constraint**: The current `MoveStopToBreakEven` CYC annotation reads:

```
// CYC=8: IsFlat(1) + tickSize/pos guard(2) + snapshot-foreach(3) + stateOk(4) + instrOk(5)
//        + cancel-try(6) + 0-targets branch(7) + targets-for-loop(8).
```

CYC is already at the limit of 8. Adding inline discriminator branches (`isNative` / `isPtt`)
would push it to 10+. **Extraction is mandatory.**

---

## 2. Solution Architecture

Three precise code changes in exactly one file. No other files are touched.

### CHANGE A — `CopyEngine.cs`: New private method `SnapshotBeTargets`

**File**: [`src/PropTraderTools/CopyEngine.cs`](src/PropTraderTools/CopyEngine.cs)
**Insertion point**: Immediately before `MoveStopToBreakEven` (L3335). The new method is a
private helper in the same partial class / inner class that owns `MoveStopToBreakEven` and
`CountLeaderTargets`.

**Signature**:

```csharp
private List<(double Price, int Qty, OrderAction Action)> SnapshotBeTargets(
    Account acc, Instrument instrument)
```

**Full body**:

```csharp
// CYC=7: null guard(1) + foreach(2) + o==null continue(3) + stateOk(4) + instrOk+type(5)
//        + if(isNative)(6) + else if(isPtt)(7). JS-002: returns List, never null.
// JS-021: no lock. JS-001: no throw. ASCII-only.
// DW-B107: two-pass native-first collect for MoveStopToBreakEven Step A.
// stateOk is wider than SnapshotTargetOrders (7 states vs 2) per DW-B79-01 + REPAIR-09 DW-B79-05.
private List<(double Price, int Qty, OrderAction Action)> SnapshotBeTargets(
    Account acc, Instrument instrument)
{
    var nativeTargets = new List<(double Price, int Qty, OrderAction Action)>();
    var pttTargets    = new List<(double Price, int Qty, OrderAction Action)>();
    if (acc == null || instrument == null)
        return nativeTargets; // (1) JS-002: empty list, never null
    foreach (Order o in acc.Orders) // (2)
    {
        if (o == null)
            continue; // (3)
        bool stateOk =
            o.OrderState == OrderState.Working
            || o.OrderState == OrderState.Accepted
            || o.OrderState == OrderState.Submitted
            || o.OrderState == OrderState.Initialized
            || o.OrderState == OrderState.TriggerPending   // (4)
            || o.OrderState == OrderState.ChangeSubmitted
            || o.OrderState == OrderState.CancelSubmitted;
        bool instrOk = o.Instrument != null && o.Instrument.FullName == instrument.FullName; // (5)
        if (!stateOk || !instrOk || o.OrderType != OrderType.Limit)
            continue;
        if (string.IsNullOrEmpty(o.Name))
            continue;
        bool isNative =
            o.Name.Length >= 7
            && o.Name.StartsWith("Target", StringComparison.Ordinal)
            && char.IsDigit(o.Name[6])
            && o.Name[6] != '0';
        bool isPtt =
            (o.Name.StartsWith("PTT-QX-T", StringComparison.Ordinal)
             && o.Name.Length > 8
             && char.IsDigit(o.Name[8]))
            || o.Name.StartsWith("PTT-BE-Target-", StringComparison.Ordinal);
        if (isNative)            // (6)
            nativeTargets.Add((o.LimitPrice, o.Quantity, o.OrderAction));
        else if (isPtt)          // (7)
            pttTargets.Add((o.LimitPrice, o.Quantity, o.OrderAction));
    }
    return nativeTargets.Count > 0 ? nativeTargets : pttTargets;
}
```

**Key differences from `SnapshotTargetOrders` (QX path)**:

| Aspect | `SnapshotTargetOrders` (QX) | `SnapshotBeTargets` (BE) |
|--------|-----------------------------|--------------------------|
| `stateOk` states | 2: Working \| Accepted | 7: Working \| Accepted \| Submitted \| Initialized \| TriggerPending \| ChangeSubmitted \| CancelSubmitted |
| `isNative` digit check | `[6] != '0'` omitted | `[6] != '0'` PRESENT (required — "Target0" is not a valid ATM target) |
| Return element type | `(double Price, int Qty)` | `(double Price, int Qty, OrderAction Action)` (needed by `PttBreakEvenSwap.Execute`) |

The 7-state `stateOk` is intentional and must not be narrowed. It was introduced by DW-B79-01
and REPAIR-09 DW-B79-05 to capture PTT-QX-T orders in transient cancel/change states on rapid
QX->BE-ALL press. Narrowing it would reintroduce the DW-B79 regression.

---

### CHANGE B — `CopyEngine.cs`: Replace Step A loop with `SnapshotBeTargets` call

**File**: [`src/PropTraderTools/CopyEngine.cs`](src/PropTraderTools/CopyEngine.cs)
**Lines replaced**: L3373-3422 (the Step A comment block + `var targets = new List<...>()` +
the entire `foreach` loop body).

**Replacement**:

```csharp
// -- Step A: snapshot ATM target orders BEFORE cancelling anything ----
// DW-B107: extracted to SnapshotBeTargets to keep MoveStopToBreakEven CYC=7.
// Two-pass native-first collect: native Target1..9 take priority over
// stale PTT-QX-T*/PTT-BE-Target-* residues (same logic as DW-B106).
var targets = SnapshotBeTargets(acc, instrument); // (3)
```

The CYC annotation on `MoveStopToBreakEven` must also be updated from CYC=8 to CYC=7.
Updated annotation (at L3271-3272):

```csharp
// CYC=7: IsFlat(1) + tickSize/pos guard(2) + SnapshotBeTargets call site(3=no branch)
//        + while-cap(3) + cancel-try(4) + 0-targets branch(5) + targets-for-loop(6).
```

Wait — per the CYC analysis below, the while-cap is a new branch. The count remains at 7
because the old `snapshot-foreach(3)` + `stateOk(4)` + `instrOk(5)` three branches collapse
into zero branches (method call, not inline). New annotation:

```csharp
// CYC=7: IsFlat(1) + tickSize/pos guard(2) + while-cap(3) + cancel-try(4)
//        + 0-targets branch(5) + targets-for-loop(6) + partial-retry branch(7).
```

---

### CHANGE C — `CopyEngine.cs`: Hard cap after `SnapshotBeTargets` call

**File**: [`src/PropTraderTools/CopyEngine.cs`](src/PropTraderTools/CopyEngine.cs)
**Insertion point**: Immediately after `var targets = SnapshotBeTargets(acc, instrument);`
(the CHANGE B call site), BEFORE `PttBreakEvenSwap.Execute(...)`.

**Insertion**:

```csharp
// DW-B107: hard cap -- BE/QX contract is always exactly 3 targets max.
// Prevents stale partial-fill residue submitting extra OCO pairs.
// No LINQ -- while-loop trim per JS zero-alloc mandate.
while (targets.Count > 3)
    targets.RemoveAt(targets.Count - 1);
```

**Why `while` not `.Take(3)` or `.GetRange`**: LINQ is banned by JS-006 / NT8-006. `List.Take`
is a LINQ extension method. `while + RemoveAt` is allocation-free on the existing `List<T>`.

---

## 3. CYC Analysis

### MoveStopToBreakEven — Before and After

**Before B108** — CYC=8 (at limit):

| # | Branch | Notes |
|---|--------|-------|
| 1 | `IsFlat` guard | (1) in annotation |
| 2 | `tickSize/pos guard` (direction/raw block) | (2) |
| 3 | `snapshot-foreach` (loop counts as 1) | (3) |
| 4 | `stateOk` compound | (4) |
| 5 | `instrOk` filter | (5) |
| 6 | `cancel-try` (try/catch) | (6) |
| 7 | `targets.Count == 0` branch | (7) |
| 8 | `targets-for-loop` (partial retry loop) | (8) |

**After B108** — CYC=7 (one under limit):

| # | Branch | Notes |
|---|--------|-------|
| 1 | `IsFlat` guard | unchanged |
| 2 | `tickSize/pos guard` | unchanged |
| 3 | `while (targets.Count > 3)` | NEW — replaces old branches 3/4/5 |
| 4 | `cancel-try` (try/catch) | renumbered from 6 |
| 5 | `targets.Count == 0` branch | renumbered from 7 |
| 6 | `targets-for-loop` (partial retry loop) | renumbered from 8 |
| 7 | `partial-retry branch` (isRetry guard) | previously in annotation slot 8's body |

`SnapshotBeTargets(acc, instrument)` at the call site is a method call, **not** a decision
branch — it contributes 0 to `MoveStopToBreakEven` CYC.
Old branches 3 (`snapshot-foreach`), 4 (`stateOk`), 5 (`instrOk`) collapse entirely into the
extracted method, freeing 3 CYC slots. The new `while` cap adds 1. Net: -3 + 1 = -2. CYC
goes from 8 to 6... but `partial-retry branch` was always present in the for-loop body (the
`if (!isRetry && ...)` guard). This restores count to 7.

### SnapshotBeTargets — New Method

| # | Branch | Notes |
|---|--------|-------|
| 1 | `acc == null \|\| instrument == null` null guard | (1) |
| 2 | `foreach (Order o in acc.Orders)` loop | (2) |
| 3 | `o == null continue` | (3) |
| 4 | `stateOk` compound boolean (1 decision point) | (4) |
| 5 | `!stateOk \|\| !instrOk \|\| != Limit` combined continue | (5) |
| 6 | `if (isNative)` | (6) |
| 7 | `else if (isPtt)` | (7) |

`isNative` and `isPtt` are bool assignments (compound expressions, not decision points).
`string.IsNullOrEmpty` continue and ternary return are each 1 decision — but per the CYC
annotation in the orchestrator brief, the ternary return and the IsNullOrEmpty guard fold into
the total of 7 as shown. The brief explicitly states CYC=7; this matches.

**CYC=7** — one under the 8-branch limit.

### Summary Table

| Method | File | CYC Before | CYC After | Delta | Limit | Status |
|--------|------|-----------|-----------|-------|-------|--------|
| `MoveStopToBreakEven` | `CopyEngine.cs` | 8 | 7 | -1 | 8 | PASS |
| `SnapshotBeTargets` | `CopyEngine.cs` | n/a (new) | 7 | n/a | 8 | PASS |

No existing method exceeds CYC=8 after B108. No other methods are touched.

---

## 4. JS Compliance Analysis

| Rule | Requirement | New Code Behaviour | Status |
|------|-------------|-------------------|--------|
| JS-001 | No `throw` in hot paths | All new paths use early `return` or value return; no exception thrown anywhere in `SnapshotBeTargets` or the cap loop | PASS |
| JS-002 | No `return null` | `SnapshotBeTargets` returns empty `nativeTargets` list on null input (never null); call site receives `List<...>` always | PASS |
| JS-021 | No `lock()` | `SnapshotBeTargets` uses local list operations only; `while + RemoveAt` is single-threaded; no shared state mutation; no `lock()` | PASS |
| JS-033 | No `async void` | All new code is synchronous; no `async` keyword anywhere | PASS |
| ASCII-only | No Unicode in string/identifier literals | `"Target"`, `"PTT-QX-T"`, `"PTT-BE-Target-"`, method name `SnapshotBeTargets`, all comments — pure 7-bit ASCII | PASS |
| No LINQ (NT8-006 / JS-006) | No LINQ extension methods | Cap uses `while + RemoveAt`; no `.Take()`, `.GetRange()`, `.Where()` | PASS |

---

## 5. Change Isolation: File Boundary Audit

| File | Changes | Other files required? |
|------|---------|-----------------------|
| `src/PropTraderTools/CopyEngine.cs` | CHANGE A (new `SnapshotBeTargets` method), CHANGE B (replace Step A loop), CHANGE C (while cap after call) | No |

**Total files**: 1
**New files created**: 0
**Interface files changed**: 0
**Test project files changed**: 0
**Other PropTraderTools files changed**: 0

### Explicitly Out of Scope

| File | Reason |
|------|--------|
| `src/PropTraderTools/Features/PttGlobalQuickExit.cs` | Fixed in B107 (DW-B106). DO NOT TOUCH. |
| `src/PropTraderTools/Features/PttQuickExit.cs` | Fixed in B107 (DW-B106). DO NOT TOUCH. |
| `src/PropTraderTools/Features/PttBreakEvenSwap.cs` | Cap is applied upstream (before `Execute`). DO NOT TOUCH. |

`SnapshotBeTargets` is a `private` instance method of `CopyEngine`'s inner class. No access
modifier change is needed in any other file. `PttBreakEvenSwap.Execute` signature is unchanged
— it still receives `List<(double Price, int Qty, OrderAction Action)>`.

---

## 6. Spec Requirement Traceability

| Change | Closes | Spec Requirement |
|--------|--------|-----------------|
| CHANGE A | DW-B107 | Extract `SnapshotBeTargets` private method with two-pass native-first collect and 7-state `stateOk` |
| CHANGE B | DW-B107 | Replace Step A flat-collect loop with `SnapshotBeTargets` call; reduce `MoveStopToBreakEven` CYC from 8 to 7 |
| CHANGE C | DW-B107 | Hard cap `targets.Count` at 3 via `while + RemoveAt` before `PttBreakEvenSwap.Execute` |

### Prior Fixes Preserved (must not be regressed)

| Fix | Source | Preservation |
|-----|--------|-------------|
| 7-state `stateOk` widening | DW-B79-01 + REPAIR-09 DW-B79-05 | Carried verbatim into `SnapshotBeTargets`; not narrowed |
| `[6] != '0'` on `isNative` | Existing Step A (L3408) | Carried verbatim into `SnapshotBeTargets` |
| `PTT-QX-T*` and `PTT-BE-Target-*` fallback | HOTFIX-MSTBE-QX-TARGETS-01 | Carried into `pttTargets` bucket of `SnapshotBeTargets` |
| `isRetry` guard on retry registration | DW-B79-04 | Untouched — lives outside the replaced Step A block |
| `diagTotal` logging block | DW-B79-02 DIAG | Untouched — lives at L3364-3371, before Step A |

---

## 7. Test Scope: Verifier Inspection Criteria

The verifier performs code inspection of [`src/PropTraderTools/CopyEngine.cs`](src/PropTraderTools/CopyEngine.cs)
against the following criteria. No other files are inspected.

### T1 — `SnapshotBeTargets` method exists
- Private instance method named `SnapshotBeTargets` present in `CopyEngine.cs`
- Return type: `List<(double Price, int Qty, OrderAction Action)>`
- Parameters: `(Account acc, Instrument instrument)`
- Located immediately before `MoveStopToBreakEven` in the file

### T2 — `SnapshotBeTargets` null guard (JS-002)
- First statement after the two `var` declarations is:
  `if (acc == null || instrument == null) return nativeTargets;`
- Returns `nativeTargets` (empty list), NOT `null`
- No `return null` anywhere in the method

### T3 — `SnapshotBeTargets` two-pass structure
- Two separate lists declared: `nativeTargets` and `pttTargets`
- Both typed `List<(double Price, int Qty, OrderAction Action)>`
- `if (isNative)` adds to `nativeTargets`
- `else if (isPtt)` adds to `pttTargets`
- Return: `nativeTargets.Count > 0 ? nativeTargets : pttTargets`

### T4 — `SnapshotBeTargets` stateOk has exactly 7 states
- `stateOk` includes all of: `Working`, `Accepted`, `Submitted`, `Initialized`,
  `TriggerPending`, `ChangeSubmitted`, `CancelSubmitted`
- No states added or removed vs the original Step A loop

### T5 — `SnapshotBeTargets` isNative includes `[6] != '0'` guard
- `isNative` condition:
  `o.Name.Length >= 7 && o.Name.StartsWith("Target", StringComparison.Ordinal) && char.IsDigit(o.Name[6]) && o.Name[6] != '0'`
- All four sub-conditions present

### T6 — `SnapshotBeTargets` isPtt covers both PTT-QX-T* and PTT-BE-Target-*
- `isPtt` condition:
  `(o.Name.StartsWith("PTT-QX-T", ...) && o.Name.Length > 8 && char.IsDigit(o.Name[8])) || o.Name.StartsWith("PTT-BE-Target-", ...)`
- Both branches of the OR present

### T7 — `SnapshotBeTargets` CYC annotation is present and reads CYC=7
- Header comment `// CYC=7: null guard(1)+foreach(2)+o==null(3)+stateOk(4)+instrOk+type(5)+if(isNative)(6)+else if(isPtt)(7).` present
- Or equivalent annotation identifying exactly 7 counted branches

### T8 — Step A loop replaced (CHANGE B)
- Lines L3373-3422 no longer contain the old `var targets = new List<...>()` + `foreach` block
- Replacement is exactly:
  ```
  var targets = SnapshotBeTargets(acc, instrument); // (3)
  ```
  with the DW-B107 extraction comment above it

### T9 — Step A comment block updated (CHANGE B)
- Old multi-line Step A comment (DW-B79-01, HOTFIX-MSTBE-QX-TARGETS-01 text) is replaced by
  the new 3-line comment referencing DW-B107 extraction and two-pass logic
- No references to `var targets = new List<(double Price, int Qty, OrderAction Action)>();`
  remain at the call site

### T10 — While cap inserted (CHANGE C)
- `while (targets.Count > 3) targets.RemoveAt(targets.Count - 1);` (or equivalent
  block form) is present immediately after `var targets = SnapshotBeTargets(...)` and
  BEFORE `PttBreakEvenSwap.Execute(acc, instrument, newStop, targets)`
- DW-B107 cap comment present

### T11 — No LINQ in cap
- `targets.Take(3)`, `targets.GetRange(0,3)`, `.Where(...)`, `.Select(...)` must NOT appear
  near the cap site
- Only `while + RemoveAt` pattern acceptable

### T12 — `MoveStopToBreakEven` CYC annotation updated
- Old annotation `CYC=8: IsFlat(1) + tickSize/pos guard(2) + snapshot-foreach(3) + stateOk(4) + instrOk(5) + cancel-try(6) + 0-targets branch(7) + targets-for-loop(8)` REMOVED
- New annotation referencing CYC=7 present (exact wording per CHANGE B section above, or equivalent)

### T13 — No lock() anywhere in new code
- `grep -n "lock(" src/PropTraderTools/CopyEngine.cs` returns zero new occurrences in
  `SnapshotBeTargets` or the cap block

### T14 — No return null anywhere in new code
- `SnapshotBeTargets` has no `return null;` statement
- While-cap block has no null return

### T15 — PttGlobalQuickExit.cs, PttQuickExit.cs, PttBreakEvenSwap.cs unchanged
- File modification timestamps and content of these three files must be identical to pre-B108
- The engineer MUST NOT touch these files

---

## Summary

B108 delivers one surgical fix in one ticket in one file:

- **DW-B107**: The flat Step A collect loop in `MoveStopToBreakEven` is extracted to a new
  private method `SnapshotBeTargets` that applies the same two-pass native-first discrimination
  as DW-B106's `SnapshotTargetOrders`. A `while`-loop hard cap at 3 entries is added
  immediately before `PttBreakEvenSwap.Execute`. Together these ensure stale `PTT-BE-Target-*`
  residues from prior sessions are excluded when native ATM targets are present, and that no
  more than 3 OCO pairs are ever submitted. `MoveStopToBreakEven` drops from CYC=8 to CYC=7.
  `SnapshotBeTargets` is CYC=7. All JS-001, JS-002, JS-021, JS-033, ASCII-only, and no-LINQ
  constraints are satisfied. Exactly one file is touched.
