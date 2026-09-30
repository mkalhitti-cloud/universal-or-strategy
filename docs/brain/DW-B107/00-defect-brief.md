# DW-B107 Defect Brief — Pipeline Intake Document

**Block**: B108 (next pipeline run after current testing batch)
**Defect ID**: DW-B107
**Status**: OPEN — awaiting pipeline
**Severity**: P2 (correctness violation, functionally benign in observed test)
**Discovered**: 2026-08-25 live test session
**Spec reference**: `specs/002-trade-copier-spec.html#section-b107`

---

## One-Line Summary

`MoveStopToBreakEven` Step A snapshots stale prior-session `PTT-BE-Target-*` orders from
`acc.Orders`, causing followers to submit 4 OCO bracket pairs on a 3-target ATM session.

---

## Observed Symptom

Live test: BE-ALL with Copier ON, Sim101 (master) + Sim102/103/104 (followers), MES SEP26,
Buy x7, 3-target ATM. Position subsequently stopped out. Output log:

```
[BE-DIAG-CANCEL] Sim102 PTT-BE order cancelled: PTT-BE-Target-1  (x2)
[BE-DIAG-CANCEL] Sim102 PTT-BE order cancelled: PTT-BE-Target-2  (x2)
[BE-DIAG-CANCEL] Sim102 PTT-BE order cancelled: PTT-BE-Target-3  (x2)
[BE-DIAG-CANCEL] Sim102 PTT-BE order cancelled: PTT-BE-Target-4  (x2)  <-- extra
```

Sim103/104: identical (4 targets each). Sim101 (leader): 3 targets only (correct).

No unprotected position. All accounts stopped out cleanly. Functionally benign in this test.

---

## Root Cause

**DW-B107-RC1**: `MoveStopToBreakEven` Step A (CopyEngine.cs ~L3380–3422) has:
1. No native-vs-PTT discrimination — `nativeTargets` (ATM `Target1..9`) and
   `pttTargets` (`PTT-QX-T*` / `PTT-BE-Target-*`) are collected into a single flat `targets` list.
2. No count cap — no `Math.Min(raw, 3)` or equivalent limit.

If a stale `PTT-BE-Target-4` from a prior session is still `Working` in `acc.Orders` when
BE-ALL fires, it is included in `targets` and `PttBreakEvenSwap.Execute` submits an extra
OCO pair (`PTT-BE-Stop-4` + `PTT-BE-Target-4`).

**Why Sim101 was not affected**: The leader uses native ATM bracket names (`Target1/2/3`),
not `PTT-BE-Target-*`. Stale `PTT-BE-Target-4` residues only accumulate on follower accounts.

**Precedent**: Same class as DW-B106, which fixed the QX path (`SnapshotTargetOrders` in
`PttGlobalQuickExit.cs` + `ResolveTargetCount` in `PttQuickExit.cs`). B107-T1 closed the QX
path but the BE path (`MoveStopToBreakEven` Step A) was not in B107 scope.

---

## File in Scope

| File | Method | Change |
|------|--------|--------|
| `src/PropTraderTools/CopyEngine.cs` | `MoveStopToBreakEven` (~L3380) | CHANGE A + CHANGE B (see below) |

No other files required.

---

## Proposed Fix (two changes, one method)

### CHANGE A — Two-pass collect in `MoveStopToBreakEven` Step A

**Replace** the current single flat `targets` collection loop with a two-pass collect that
separates native ATM targets from PTT targets, then merges using the native-first preference
rule — identical to the logic in `SnapshotTargetOrders` (DW-B106, PttGlobalQuickExit.cs).

**Current code** (Step A loop, CopyEngine.cs ~L3379–3422):
```csharp
var targets = new List<(double Price, int Qty, OrderAction Action)>(); // (3)
foreach (Order o in acc.Orders)
{
    // ... stateOk + instrOk + Limit filter ...
    bool isAtmTarget =
        !string.IsNullOrEmpty(o.Name)
        && (
            ( o.Name.Length >= 7
              && o.Name.StartsWith("Target", StringComparison.Ordinal)
              && char.IsDigit(o.Name[6])
              && o.Name[6] != '0' )
            || ( o.Name.StartsWith("PTT-QX-T", StringComparison.Ordinal)
                 && o.Name.Length > 8
                 && char.IsDigit(o.Name[8]) )
            || o.Name.StartsWith("PTT-BE-Target-", StringComparison.Ordinal)
        );
    if (!isAtmTarget)
        continue;
    targets.Add((o.LimitPrice, o.Quantity, o.OrderAction));
}
```

**Replacement** (two-pass collect + merge):
```csharp
// DW-B107: two-pass collect -- native ATM targets take priority over PTT residues.
// Same native-first preference as SnapshotTargetOrders (DW-B106, PttGlobalQuickExit.cs).
var nativeTargets = new List<(double Price, int Qty, OrderAction Action)>(); // (3a)
var pttTargets    = new List<(double Price, int Qty, OrderAction Action)>(); // (3b)
foreach (Order o in acc.Orders)
{
    // ... stateOk + instrOk + Limit filter (unchanged) ...
    if (string.IsNullOrEmpty(o.Name))
        continue;
    bool isNative =
        o.Name.Length >= 7
        && o.Name.StartsWith("Target", StringComparison.Ordinal)
        && char.IsDigit(o.Name[6])
        && o.Name[6] != '0'; // (4a)
    bool isPtt =
        (
            o.Name.StartsWith("PTT-QX-T", StringComparison.Ordinal)
            && o.Name.Length > 8
            && char.IsDigit(o.Name[8])
        )
        || o.Name.StartsWith("PTT-BE-Target-", StringComparison.Ordinal); // (4b)
    if (isNative)
        nativeTargets.Add((o.LimitPrice, o.Quantity, o.OrderAction));
    else if (isPtt)
        pttTargets.Add((o.LimitPrice, o.Quantity, o.OrderAction));
}
// DW-B107: if ANY native ATM targets exist, use only those (no stale PTT residue).
var targets = nativeTargets.Count > 0 ? nativeTargets : pttTargets; // (3c)
```

### CHANGE B — Cap at 3 after merge

**Insert immediately after** the `var targets = ...` line from CHANGE A, before
the `PttBreakEvenSwap.Execute(acc, instrument, newStop, targets);` call:

```csharp
// DW-B107: hard cap -- QX/BE contract is always exactly 3 targets max.
// Prevents stale partial-fill residue (PTT-BE-Target-4 etc.) from submitting extra OCO pairs.
// JS rules: no LINQ -- use while-loop to trim in place.
while (targets.Count > 3)
    targets.RemoveAt(targets.Count - 1);
```

**Note**: `targets` is a local `List<T>` (not the original `nativeTargets`/`pttTargets` list
when `nativeTargets.Count > 0` — it aliases `nativeTargets`). Trimming is safe. When
`pttTargets` is returned, it also aliases safely. If architect prefers a copy for clarity,
`GetRange(0, 3)` is acceptable but creates a heap allocation on the hot path — the
`while` loop is preferred per JS zero-alloc mandate.

---

## JS-DNA Compliance Requirements

| Rule | Requirement |
|------|-------------|
| JS-021 | No `lock()` — new code uses only local list operations |
| JS-001 | No `throw` — early `return` pattern preserved |
| JS-002 | No `return null` — `targets` is always a non-null list |
| JS-033 | No `async void` — synchronous method, unchanged |
| ASCII-only | All new comments and string literals must be 7-bit ASCII |
| No LINQ | `while` loop trim preferred over `.GetRange()`/`.Take()` |

---

## CYC Analysis

`MoveStopToBreakEven` current CYC = 8 (at limit).

CHANGE A adds two branches: `if (isNative)` and `else if (isPtt)`.
The original `if (!isAtmTarget) continue;` is removed (1 branch removed).
Net CYC delta: +2 - 1 = **+1 → CYC becomes 9 (OVER LIMIT)**.

**Resolution required before ticket is issued:**
The architect must extract the two-pass collect loop into a private helper method
`SnapshotBeTargets(Account acc, Instrument instrument)` returning
`List<(double Price, int Qty, OrderAction Action)>`, mirroring how
`SnapshotTargetOrders` was extracted in DW-B106. This keeps `MoveStopToBreakEven`
at CYC = 7 (removes the loop body entirely, one call replaces it) and the new
helper is CYC = 5 (null guard + foreach + stateOk + isNative + isPtt).

| Method | CYC Before | CYC After (with extraction) | Limit | Status |
|--------|-----------|----------------------------|-------|--------|
| `MoveStopToBreakEven` | 8 | 7 (loop body extracted) | 8 | PASS |
| `SnapshotBeTargets` (new) | N/A | 5 | 8 | PASS |

---

## Acceptance Criteria (for Ph4b verifier)

- **[T1]** New helper `SnapshotBeTargets(Account, Instrument)` exists in `CopyEngine.cs`
  with `nativeTargets`/`pttTargets` two-pass collect and native-first return.
- **[T2]** `MoveStopToBreakEven` Step A calls `SnapshotBeTargets` instead of the inline loop.
- **[T3]** `while (targets.Count > 3) targets.RemoveAt(targets.Count - 1);` cap present
  between the snapshot call and `PttBreakEvenSwap.Execute(...)` call.
- **[T4]** `MoveStopToBreakEven` CYC ≤ 8 (annotated in comment).
- **[T5]** `SnapshotBeTargets` CYC ≤ 8 (annotated in comment).
- **[T6]** Zero `lock(` in new code.
- **[T7]** Zero `return null` in new code.
- **[T8]** All new strings/comments ASCII-only.

---

## Reproduction Steps

1. Run at least one BE-ALL trade on followers in an NT8 session (creates `PTT-BE-Target-*`
   history in `acc.Orders`).
2. Do NOT restart NT8 between sessions (stale `PTT-BE-Target-4` persists as `Working`).
3. Enter long on Sim101, Copier ON. Press BE-ALL.
4. Observe: Sim102/103/104 each produce `PTT-BE-Target-1..4` cancels at stop-out.
   Sim101 produces `Target-1..3` only (correct).

---

## Severity / Priority

**P2 — Correctness violation, functionally benign in observed test.**

- Extra OCO pair stops out with the others; position fully closed; no unprotected contracts.
- Edge case risk: unexpected `[BE-DIAG] attempt-counter exhaustion` on next trade if the extra
  `PTT-BE-Stop-4` cancel arrives and exhausts the 3-attempt counter before the legitimate stops.

**Recommendation**: Fix in next pipeline block (B108). Not a blocker for Combo C re-test.

---

## What This Block Does NOT Do

- This block does NOT change `PttGlobalQuickExit.cs` or `PttQuickExit.cs` — those were
  already fixed in B107-T1.
- This block does NOT affect the QX path at all.
- This block does NOT change `PttBreakEvenSwap.Execute` — the cap is applied upstream,
  before calling Execute, so Execute itself remains unchanged.
