# B107 Architecture Plan: DW-B105 + DW-B106 Combined Fix

**Status**: REVIEW_PASS candidate  
**Epic**: B107-T1  
**Phase**: 1 (Architecture)  
**Author**: ptt-architect  
**Date**: 2026-08-10  
**Spec items closed**: DW-B105 (P1-HIGH), DW-B106 (P2-MEDIUM)

---

## 1. Problem Statement

### DW-B105 (P1-HIGH): TryReplacePttBeBrackets fires during QX-ALL sweep

`TryReplacePttBeBrackets` in [`CopyEngine.cs`](src/PropTraderTools/CopyEngine.cs) is invoked
on every cancel of a `PTT-BE-Stop-*` order via `OnOrderUpdate`. It cannot distinguish whether
the cancel was caused by an NT8 ATM sweep (legitimate recovery target) versus a deliberate
`QX-ALL` sweep (where `PttGlobalQuickExit.ExecuteOne` calls `CancelQxBrackets`).

When `QX-ALL` intentionally sweeps BE brackets, `TryReplacePttBeBrackets` MUST NOT run because
`ExecuteOne` is about to submit its own `PTT-QX-*` replacement brackets. Without the guard,
the BE-replace path races against the QX-ALL submit path, producing duplicate or conflicting
bracket orders.

### DW-B106 (P2-MEDIUM): Stale prior-session residues inflate SnapshotTargetOrders count

`SnapshotTargetOrders` in [`PttGlobalQuickExit.cs`](src/PropTraderTools/Features/PttGlobalQuickExit.cs)
collects ALL `Working/Accepted Limit` orders matching `Target*`, `PTT-QX-T*`, or `PTT-BE-Target-*`
into a single flat list. It does not discriminate between native ATM targets (current session) and
stale `PTT-QX-T*` residues left over from a prior session's partial-fill. `ResolveTargetCount` in
[`PttQuickExit.cs`](src/PropTraderTools/Features/PttQuickExit.cs) returns the raw count with no cap,
causing QX-ALL to submit more than 3 target brackets when stale residues are present.

---

## 2. Solution Architecture

Five precise code changes across exactly three files. No other files are touched.

### CHANGE A — `CopyEngine.cs`: Add `_qxCancelInProgress` field

**File**: [`src/PropTraderTools/CopyEngine.cs`](src/PropTraderTools/CopyEngine.cs)  
**Insertion point**: After `_beReplaceAttempts` field declaration (line 258, after closing `new ConcurrentDictionary<string, int>();`)  
**New field**:

```csharp
// DW-B105: QX-ALL intent guard. Set per follower account by PttGlobalQuickExit.ExecuteOne
// before CancelQxBrackets, cleared after. TryReplacePttBeBrackets returns early if set.
// ConcurrentDictionary: JS-021 lock-free. Key = acc.Name (string). Value = bool (unused).
internal readonly ConcurrentDictionary<string, bool> _qxCancelInProgress =
    new ConcurrentDictionary<string, bool>();
```

**Access modifier**: `internal readonly` — accessible from `PttGlobalQuickExit.cs` within the
same `PropTraderTools` assembly without changing any other access modifiers.

---

### CHANGE B — `CopyEngine.cs`: Add guard (3b) in `TryReplacePttBeBrackets`

**File**: [`src/PropTraderTools/CopyEngine.cs`](src/PropTraderTools/CopyEngine.cs)  
**Method**: `TryReplacePttBeBrackets(Order cancelledStop)`  
**Insertion point**: Between line 2284 (`return; // (3)` IsFlat guard) and line 2285
(`var acc = cancelledStop.Account;`)  
**New guard**:

```csharp
// (3b) DW-B105: QX-ALL intent-guard. If QX-ALL is actively cancelling BE brackets
// on this account, skip ATM-sweep recovery -- QX-ALL will submit PTT-QX-* brackets.
if (_qxCancelInProgress.ContainsKey(cancelledStop.Account.Name))
    return;
```

---

### CHANGE C — `PttGlobalQuickExit.cs`: Wrap `CancelQxBrackets` with try/finally in `ExecuteOne`

**File**: [`src/PropTraderTools/Features/PttGlobalQuickExit.cs`](src/PropTraderTools/Features/PttGlobalQuickExit.cs)  
**Method**: `ExecuteOne(Account, Instrument, int, List<...>, bool, double, int)`  
**Lines replaced**: 145-153 (the `if (!skipIfFollower)` block)  
**Replacement**:

```csharp
if (!skipIfFollower) // (1)
{
    NinjaTrader.Code.Output.Process(
        "[PTT-QX-GUARD] pre-cancel follower brackets: "
            + (acc != null ? acc.Name : "NULL"),
        NinjaTrader.NinjaScript.PrintTo.OutputTab1
    );
    // DW-B105: set intent-guard before cancel so TryReplacePttBeBrackets skips
    // ATM-sweep recovery during the QX-ALL sweep. Clear unconditionally after.
    CopyEngine.Instance?._qxCancelInProgress.TryAdd(acc.Name, true);
    try
    {
        CopyEngine.Instance?.CancelQxBrackets(acc, instr);
    }
    finally
    {
        CopyEngine.Instance?._qxCancelInProgress.TryRemove(acc.Name, out _);
    }
}
```

---

### FIX 1 — `PttQuickExit.cs`: Hard cap in `ResolveTargetCount`

**File**: [`src/PropTraderTools/Features/PttQuickExit.cs`](src/PropTraderTools/Features/PttQuickExit.cs)  
**Method**: `ResolveTargetCount(List<...> own, int leaderCount)`  
**Lines replaced**: 255-258 (expression-bodied method)  
**Replacement**:

```csharp
private static int ResolveTargetCount(
    System.Collections.Generic.List<(double Price, int Qty)> own,
    int leaderCount
)
{
    int raw = own?.Count > 0 ? own.Count : (leaderCount > 0 ? leaderCount : 3);
    return Math.Min(raw, 3); // DW-B106: QX-ALL contract -- always exactly 3 targets
}
```

**Note**: Default fallback changed from `2` to `3` (closes DW-B63-01 intent). `Math.Min` is a
library call, not a conditional branch — CYC is unchanged.

---

### FIX 2 — `PttGlobalQuickExit.cs`: Two-pass discriminator in `SnapshotTargetOrders`

**File**: [`src/PropTraderTools/Features/PttGlobalQuickExit.cs`](src/PropTraderTools/Features/PttGlobalQuickExit.cs)  
**Method**: `SnapshotTargetOrders(Account acc, NinjaTrader.Cbi.Instrument instr)`  
**Lines replaced**: 172-210 (entire method body including signature)  
**Replacement**:

```csharp
private static System.Collections.Generic.List<(
    double Price,
    int Qty
)> SnapshotTargetOrders(Account acc, NinjaTrader.Cbi.Instrument instr)
{
    var nativeTargets = new System.Collections.Generic.List<(double Price, int Qty)>();
    var pttTargets    = new System.Collections.Generic.List<(double Price, int Qty)>();
    if (acc == null || instr == null)
        return nativeTargets; // (1) JS-002: empty list, never null
    foreach (NinjaTrader.Cbi.Order o in acc.Orders) // (2)
    {
        if (o == null)
            continue;
        bool stateOk =
            o.OrderState == NinjaTrader.Cbi.OrderState.Working
            || o.OrderState == NinjaTrader.Cbi.OrderState.Accepted; // (3)
        bool instrOk = o.Instrument != null && o.Instrument.FullName == instr.FullName;
        if (!stateOk || !instrOk || o.OrderType != NinjaTrader.Cbi.OrderType.Limit)
            continue;
        if (string.IsNullOrEmpty(o.Name))
            continue;
        bool isNative =
            o.Name.StartsWith("Target", StringComparison.Ordinal)
            && o.Name.Length > 6
            && char.IsDigit(o.Name[6]); // (4)
        bool isPtt =
            (
                o.Name.StartsWith("PTT-QX-T", StringComparison.Ordinal)
                && o.Name.Length > 8
                && char.IsDigit(o.Name[8])
            )
            || o.Name.StartsWith("PTT-BE-Target-", StringComparison.Ordinal); // (5)
        if (isNative)
            nativeTargets.Add((o.LimitPrice, o.Quantity));
        else if (isPtt)
            pttTargets.Add((o.LimitPrice, o.Quantity));
    }
    // DW-B106: if ANY native ATM targets exist, use only those for the count.
    return nativeTargets.Count > 0 ? nativeTargets : pttTargets; // (6)
}
```

---

## 3. CYC Analysis

| Method | File | CYC Before | CYC After | Delta | Limit | Status |
|--------|------|-----------|-----------|-------|-------|--------|
| `TryReplacePttBeBrackets` | `CopyEngine.cs` | 6 | 7 | +1 | 8 | PASS |
| `ExecuteOne` | `PttGlobalQuickExit.cs` | 2 | 2 | 0 | 8 | PASS |
| `ResolveTargetCount` | `PttQuickExit.cs` | 2 | 2 | 0 | 8 | PASS |
| `SnapshotTargetOrders` | `PttGlobalQuickExit.cs` | 4 | 7 | +3 | 8 | PASS |

**`TryReplacePttBeBrackets` branch inventory after change**:
- (1) null guard: +1
- (2) `!IsFollowerAccount`: +1
- (3) `IsFlat`: +1
- (3b) `_qxCancelInProgress.ContainsKey` (NEW): +1
- (4) `prevAttempts >= 3`: +1
- (5)+(6) internal logic: +2 (existing)
- Total = 7

**`SnapshotTargetOrders` branch inventory after change**:
- (1) null guard `acc==null||instr==null`: +1
- (2) `o==null continue`: +1
- (3) `!stateOk||!instrOk||wrong type continue`: +1
- `string.IsNullOrEmpty continue`: +1
- (4) `if (isNative) ...add`: +1
- (5) `else if (isPtt) ...add`: +1
- (6) ternary return: +1
- Total = 7

Note: `isNative` and `isPtt` are bool assignments (compound expressions, not decision points).
The `if (isNative)` / `else if (isPtt)` are the counted branches.

**`ExecuteOne`**: `try/finally` adds zero branches (not a conditional). CYC = 2 unchanged.  
**`ResolveTargetCount`**: `Math.Min` is a library call, not a branch. CYC = 2 unchanged.

---

## 4. JS Compliance Analysis

| Rule | Requirement | New Code Behaviour | Status |
|------|-------------|-------------------|--------|
| JS-001 | No `throw` in hot paths | All new paths use early `return` or value return; no exception thrown | PASS |
| JS-002 | No `return null` | `SnapshotTargetOrders` returns empty `nativeTargets` list on null input (not null); all other new returns are `void` | PASS |
| JS-021 | No `lock()` | `_qxCancelInProgress` uses `ConcurrentDictionary.TryAdd` / `TryRemove` / `ContainsKey` — all atomic, no lock() | PASS |
| JS-033 | No `async void` | All new code is synchronous; no `async` keyword anywhere | PASS |
| ASCII-only | No Unicode in string literals or identifiers | `"[PTT-QX-GUARD]..."`, `"Target"`, `"PTT-QX-T"`, `"PTT-BE-Target-"`, field name `_qxCancelInProgress`, all comments — pure ASCII | PASS |
| JS-023 | Atomic primitives for shared counters | `ConcurrentDictionary` operations are thread-safe by contract | PASS |

---

## 5. Change Isolation: File Boundary Audit

| File | Changes | Other files required? |
|------|---------|-----------------------|
| `src/PropTraderTools/CopyEngine.cs` | CHANGE A (field), CHANGE B (guard 3b) | No |
| `src/PropTraderTools/Features/PttGlobalQuickExit.cs` | CHANGE C (ExecuteOne try/finally), FIX 2 (SnapshotTargetOrders two-pass) | No |
| `src/PropTraderTools/Features/PttQuickExit.cs` | FIX 1 (ResolveTargetCount block-body + cap) | No |

**Total files**: 3  
**New files created**: 0  
**Interface files changed**: 0  
**Test project files changed**: 0  
**Other PropTraderTools files changed**: 0  

`_qxCancelInProgress` is declared `internal readonly` on `CopyEngine`. `PttGlobalQuickExit.cs`
accesses it via `CopyEngine.Instance?._qxCancelInProgress` — both classes are in the
`PropTraderTools` assembly, so no access modifier change is required anywhere else.

---

## 6. Thread Safety: `_qxCancelInProgress` Set/Clear Timing

### Invariant

The guard MUST be SET before `CancelQxBrackets` dispatches any cancel orders, and MUST be
CLEARED unconditionally after `CancelQxBrackets` returns (even on exception).

### Guarantee: try/finally

```
ExecuteOne thread:
  _qxCancelInProgress.TryAdd(acc.Name, true)   <-- SET (atomic)
  try {
      CancelQxBrackets(acc, instr)              <-- cancel orders submitted
      // NT8 order events may fire here or asynchronously
  } finally {
      _qxCancelInProgress.TryRemove(acc.Name)  <-- CLEAR (atomic, always runs)
  }
```

`ConcurrentDictionary.TryAdd` and `TryRemove` are lock-free atomic operations. `ContainsKey`
in `TryReplacePttBeBrackets` is a wait-free read — consistent with concurrent TryAdd/TryRemove.

### Cross-thread scenario

NT8 may fire `OnOrderUpdate` callbacks on the UI thread while `ExecuteOne` runs on the same
thread. The guard window is narrow (duration of `CancelQxBrackets` only). After `finally`
clears the guard, all future cancel callbacks for this account proceed normally.

If `CancelQxBrackets` is called for a second account concurrently (unlikely but possible),
the `ConcurrentDictionary` key is `acc.Name` — each account has its own key, so separate
accounts do not interfere.

### No lock() anywhere

`ConcurrentDictionary` provides all required atomicity without `lock()`. JS-021 satisfied.

---

## 7. Spec Requirement Traceability

| Change | Closes | Spec Requirement |
|--------|--------|-----------------|
| CHANGE A + B | DW-B105 | Add `_qxCancelInProgress` guard field and early-return in `TryReplacePttBeBrackets` |
| CHANGE C | DW-B105 | Set/clear guard in `ExecuteOne` wrapping `CancelQxBrackets` via try/finally |
| FIX 1 | DW-B106 + DW-B63-01 | Hard cap at 3 in `ResolveTargetCount`; default fallback 2→3 |
| FIX 2 | DW-B106 | Two-pass `SnapshotTargetOrders` preferring native ATM targets over stale PTT residues |

---

## 8. Test Scope: Verifier Inspection Criteria

The verifier will perform code inspection (not runtime execution) of the three modified files
against the following criteria:

### T1 — `_qxCancelInProgress` field (CopyEngine.cs)
- Field is declared `internal readonly ConcurrentDictionary<string, bool>`
- Field is named `_qxCancelInProgress`
- Field is initialised `new ConcurrentDictionary<string, bool>()`
- Field appears after the `_beReplaceAttempts` field (line ~259)
- Comment references DW-B105 and JS-021

### T2 — Guard (3b) in `TryReplacePttBeBrackets` (CopyEngine.cs)
- Guard is placed between `return; // (3)` (IsFlat) and `var acc = cancelledStop.Account;`
- Guard reads `_qxCancelInProgress.ContainsKey(cancelledStop.Account.Name)`
- Guard body is `return;` (early exit, no throw)
- No `lock()` present in the guard or surrounding code

### T3 — `ExecuteOne` try/finally (PttGlobalQuickExit.cs)
- `TryAdd(acc.Name, true)` is called BEFORE `CancelQxBrackets`
- `CancelQxBrackets` is inside the `try` block
- `TryRemove(acc.Name, out _)` is inside the `finally` block
- The `if (!skipIfFollower)` condition wraps the entire try/finally
- No `lock()` present

### T4 — `ResolveTargetCount` cap (PttQuickExit.cs)
- Method uses block body (not expression body)
- `int raw = own?.Count > 0 ? own.Count : (leaderCount > 0 ? leaderCount : 3);`
  — fallback is `3` (not `2`)
- `return Math.Min(raw, 3);` is present
- Comment references DW-B106
- CYC = 2

### T5 — `SnapshotTargetOrders` two-pass (PttGlobalQuickExit.cs)
- Method declares `nativeTargets` and `pttTargets` as separate `List<(double Price, int Qty)>`
- `isNative` condition: `StartsWith("Target", Ordinal)` AND `Length > 6` AND `char.IsDigit([6])`
- `isPtt` condition: (`StartsWith("PTT-QX-T", Ordinal)` AND `Length > 8` AND `char.IsDigit([8])`) OR `StartsWith("PTT-BE-Target-", Ordinal)`
- `if (isNative)` adds to `nativeTargets`; `else if (isPtt)` adds to `pttTargets`
- Return: `nativeTargets.Count > 0 ? nativeTargets : pttTargets`
- Null/empty input returns `nativeTargets` (empty list, not null) — JS-002

### T6 — No null returns
- `SnapshotTargetOrders`: early return on null input returns `nativeTargets` (empty list)
- No `return null` anywhere in new code

### T7 — No lock()
- `grep -n "lock(" src/PropTraderTools/CopyEngine.cs` must return zero results in new code
- `grep -n "lock(" src/PropTraderTools/Features/PttGlobalQuickExit.cs` must return zero results in new code
- `grep -n "lock(" src/PropTraderTools/Features/PttQuickExit.cs` must return zero results in new code

---

## Summary

B107 delivers two surgical fixes in one ticket across three files:

- **DW-B105**: A `ConcurrentDictionary`-backed intent-guard field + early-return guard +
  try/finally set/clear eliminates the `TryReplacePttBeBrackets` / QX-ALL race condition with
  zero lock() and zero CYC increase beyond the single new branch.
- **DW-B106**: A two-pass `SnapshotTargetOrders` that discriminates native ATM targets from
  stale PTT residues, combined with a `Math.Min(raw, 3)` hard cap in `ResolveTargetCount`,
  ensures QX-ALL always submits exactly 3 target brackets.

All five changes satisfy JS-001, JS-002, JS-021, JS-033, and ASCII-only. No method exceeds
CYC = 7 (limit 8). Exactly three files are touched; no other files are modified.
