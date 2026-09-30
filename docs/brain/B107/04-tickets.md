# B107 Tickets

**Status**: TICKETS_COMPLETE
**Epic**: B107-T1
**Phase**: 3 (Ticket Generation)
**Author**: ptt-architect
**Date**: 2026-08-10
**Source plan**: `docs/brain/B107/02-architecture-plan.md` (REVIEW_PASS — all 14 criteria)

---

## Ticket B107-T1

### Spec Requirements
- **DW-B105** (P1-HIGH): `_qxCancelInProgress` intent-guard — eliminates race between
  `TryReplacePttBeBrackets` ATM-sweep recovery and `PttGlobalQuickExit.ExecuteOne` QX-ALL sweep.
- **DW-B106** (P2-MEDIUM): `ResolveTargetCount` hard cap at 3 + `SnapshotTargetOrders`
  two-pass discriminator — prevents stale prior-session PTT residues from inflating the
  target-bracket count beyond 3.

---

### Files In Scope (ONLY these 3)

| File | Changes |
|------|---------|
| `src/PropTraderTools/CopyEngine.cs` | CHANGE A (field), CHANGE B (guard 3b) |
| `src/PropTraderTools/Features/PttGlobalQuickExit.cs` | CHANGE C (ExecuteOne try/finally), CHANGE D (SnapshotTargetOrders two-pass) |
| `src/PropTraderTools/Features/PttQuickExit.cs` | CHANGE E (ResolveTargetCount block-body + cap) |

**New files created**: 0
**Test project files changed**: 0
**Interface files changed**: 0
**Other PropTraderTools files changed**: 0

---

### Method Signatures Touched

| Method | File | Change Type |
|--------|------|-------------|
| `_qxCancelInProgress` (new field) | `CopyEngine.cs` | Field insertion after `_beReplaceAttempts` (line ~258) |
| `TryReplacePttBeBrackets(Order cancelledStop)` | `CopyEngine.cs` | Guard (3b) inserted at line ~2285 |
| `ExecuteOne(Account, Instrument, int, List<...>, bool, double, int)` | `PttGlobalQuickExit.cs` | Lines 145-153: if block replaced with try/finally variant |
| `SnapshotTargetOrders(Account acc, NinjaTrader.Cbi.Instrument instr)` | `PttGlobalQuickExit.cs` | Lines 172-210: entire method body replaced |
| `ResolveTargetCount(List<(double Price, int Qty)> own, int leaderCount)` | `PttQuickExit.cs` | Lines 255-258: expression-body replaced with block-body |

---

### Precise Code Changes (5 changes — verbatim)

#### CHANGE A — `CopyEngine.cs`: Insert `_qxCancelInProgress` field after line 258

**Insertion point**: After the `_beReplaceAttempts` field declaration (line 258, the line
ending `new ConcurrentDictionary<string, int>();`).

```csharp
// DW-B105: QX-ALL intent guard. Set per follower account by PttGlobalQuickExit.ExecuteOne
// before CancelQxBrackets, cleared after. TryReplacePttBeBrackets returns early if set.
// ConcurrentDictionary: JS-021 lock-free. Key = acc.Name (string). Value = bool (unused).
internal readonly ConcurrentDictionary<string, bool> _qxCancelInProgress =
    new ConcurrentDictionary<string, bool>();
```

**Access modifier**: `internal readonly` — accessible from `PttGlobalQuickExit.cs` within
the same `PropTraderTools` assembly. No other access modifiers need to change.

---

#### CHANGE B — `CopyEngine.cs`: Insert guard (3b) in `TryReplacePttBeBrackets`

**Insertion point**: Between `return; // (3)` (IsFlat guard, line ~2284) and
`var acc = cancelledStop.Account;` (line ~2285).

```csharp
// (3b) DW-B105: QX-ALL intent-guard. If QX-ALL is actively cancelling BE brackets
// on this account, skip ATM-sweep recovery -- QX-ALL will submit PTT-QX-* brackets.
if (_qxCancelInProgress.ContainsKey(cancelledStop.Account.Name))
    return;
```

**No variable is declared before the check.** The `var acc` line that follows must remain
immediately after this guard.

---

#### CHANGE C — `PttGlobalQuickExit.cs`: Replace ExecuteOne `if (!skipIfFollower)` block (lines 145-153)

**Lines replaced**: 145-153 (the `if (!skipIfFollower)` block).

**Replacement** (verbatim):

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

**Invariant**: `TryAdd` MUST appear BEFORE the `try` block. `TryRemove` MUST appear INSIDE
the `finally` block. `CancelQxBrackets` MUST be the only call inside the `try` block.
The `if (!skipIfFollower)` condition wraps the entire try/finally.

---

#### CHANGE D — `PttGlobalQuickExit.cs`: Replace `SnapshotTargetOrders` body (lines 172-210)

**Lines replaced**: 172-210 (entire method body including signature line).

**Replacement** (verbatim):

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

**Note on `isNative`/`isPtt`**: These are bool assignments — compound logical expressions,
not decision-point branches. Only the `if (isNative)` and `else if (isPtt)` lines are CYC
branches. CYC after = 7 (branch annotations (1) through (6) plus the `else if`).

---

#### CHANGE E — `PttQuickExit.cs`: Replace `ResolveTargetCount` body (lines 255-258)

**Lines replaced**: 255-258 (expression-bodied method).

**Replacement** (verbatim):

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

**Note**: Default fallback changed from `2` to `3` (closes DW-B63-01 intent).
`Math.Min` is a library call, not a conditional branch — CYC is unchanged at 2.

---

### JS Rule Constraints (per-change mapping)

| Change | JS Rules Applied |
|--------|-----------------|
| CHANGE A (`_qxCancelInProgress` field) | JS-021 (no lock — `ConcurrentDictionary` is lock-free); ASCII-only identifier |
| CHANGE B (guard 3b) | JS-001 (no throw — early `return`); JS-021 (no lock); ASCII-only comments |
| CHANGE C (try/finally) | JS-021 (no lock); JS-033 (no async void — synchronous); ASCII-only string literal `"[PTT-QX-GUARD]..."` |
| CHANGE D (SnapshotTargetOrders) | JS-002 (no return null — returns empty list); JS-001 (no throw); ASCII-only string literals; JS-021 (no lock) |
| CHANGE E (ResolveTargetCount) | JS-001 (no throw); JS-002 (no return null — returns int); ASCII-only comment |

---

### 7-Scan Checklist — MANDATORY (engineer contract)

The engineer MUST run ALL 7 scans to zero before reporting BUILD_PASS.
Any non-zero result in new or modified code = DO NOT PROCEED. Fix and re-scan.

---

#### SCAN-01 — lock() check (JS-021 P0)

```powershell
grep -rn "lock(" src/PropTraderTools/ --include="*.cs"
```

**Expected**: Zero new `lock(` occurrences in any of the 3 modified files.
Pre-existing `lock(` lines in unmodified files are out of scope for this ticket.

---

#### SCAN-02 — async void check (JS-033 P0)

```powershell
grep -rn "async void " src/PropTraderTools/ --include="*.cs"
```

**Expected**: Zero results in modified files for new code. All new methods in this ticket
are synchronous; no `async` keyword is added anywhere.

---

#### SCAN-03 — return null check (JS-002 P0)

```powershell
grep -rn "return null;" src/PropTraderTools/CopyEngine.cs
grep -rn "return null;" src/PropTraderTools/Features/PttGlobalQuickExit.cs
grep -rn "return null;" src/PropTraderTools/Features/PttQuickExit.cs
```

**Expected**: Zero new `return null;` in any modified method.
`SnapshotTargetOrders` returns empty `nativeTargets` list (not null) on null input.

---

#### SCAN-04 — non-ASCII check (ASCII-only mandate)

```powershell
grep -Pn "[^\x00-\x7F]" src/PropTraderTools/CopyEngine.cs
grep -Pn "[^\x00-\x7F]" src/PropTraderTools/Features/PttGlobalQuickExit.cs
grep -Pn "[^\x00-\x7F]" src/PropTraderTools/Features/PttQuickExit.cs
```

**Expected**: Zero non-ASCII characters in any changed lines. All new string literals,
identifiers, and comments are pure ASCII 7-bit.

---

#### SCAN-05 — CYC check (CYC ≤ 8 mandate)

Run `python scripts/complexity_audit.py` or manually count branches per method.

| Method | File | Expected CYC |
|--------|------|-------------|
| `TryReplacePttBeBrackets` | `CopyEngine.cs` | 7 |
| `ExecuteOne` | `PttGlobalQuickExit.cs` | 2 |
| `SnapshotTargetOrders` | `PttGlobalQuickExit.cs` | 7 |
| `ResolveTargetCount` | `PttQuickExit.cs` | 2 |

All must be ≤ 8. Ticket is invalid if any method exceeds 8.

---

#### SCAN-06 — field visibility check

```powershell
# Confirm declaration in CopyEngine.cs
grep -n "_qxCancelInProgress" src/PropTraderTools/CopyEngine.cs
```
**Expected**: Line declares `internal readonly ConcurrentDictionary<string, bool> _qxCancelInProgress`

```powershell
# Confirm access pattern in PttGlobalQuickExit.cs
grep -n "_qxCancelInProgress" src/PropTraderTools/Features/PttGlobalQuickExit.cs
```
**Expected**: Access via `CopyEngine.Instance?._qxCancelInProgress.TryAdd(...)` and
`CopyEngine.Instance?._qxCancelInProgress.TryRemove(...)`. No direct field declaration
in `PttGlobalQuickExit.cs` — it uses the `internal` field on `CopyEngine`.

---

#### SCAN-07 — try/finally integrity check

Manual inspection of `PttGlobalQuickExit.cs` around the `CancelQxBrackets` call site:

1. `CopyEngine.Instance?._qxCancelInProgress.TryAdd(acc.Name, true)` appears **before** the `try {` keyword.
2. `CopyEngine.Instance?.CancelQxBrackets(acc, instr)` appears **inside** the `try { ... }` block.
3. `CopyEngine.Instance?._qxCancelInProgress.TryRemove(acc.Name, out _)` appears **inside** the `finally { ... }` block.
4. No `lock(` keyword present anywhere in the surrounding code.
5. The entire `try/finally` construct is nested inside `if (!skipIfFollower)`.

---

### Acceptance Criteria (Ph4b verifier inspection checklist)

The verifier independently inspects the three modified files against these criteria.
Each `[Tx]` item is a binary pass/fail.

#### DW-B105 criteria

- **[T1]** `_qxCancelInProgress` field present in `CopyEngine.cs` —
  declared as `internal readonly ConcurrentDictionary<string, bool>`,
  initialised `new ConcurrentDictionary<string, bool>()`,
  located after the `_beReplaceAttempts` field (line ~259),
  comment references DW-B105 and JS-021.

- **[T2]** Guard (3b) present in `TryReplacePttBeBrackets` (`CopyEngine.cs`) —
  placed between `return; // (3)` (IsFlat guard) and `var acc = cancelledStop.Account;`,
  reads `_qxCancelInProgress.ContainsKey(cancelledStop.Account.Name)`,
  body is `return;` (early exit, no throw, no new variable before the check).

- **[T3]** try/finally structure in `ExecuteOne` (`PttGlobalQuickExit.cs`) —
  `TryAdd(acc.Name, true)` is **before** the `try` block,
  `CancelQxBrackets(acc, instr)` is **inside** the `try` block,
  `TryRemove(acc.Name, out _)` is **inside** the `finally` block,
  the `if (!skipIfFollower)` condition wraps the entire try/finally,
  no `lock(` anywhere.

- **[T4]** No `lock(` added in any of the 3 modified files (new code only).

#### DW-B106 criteria

- **[T5]** `ResolveTargetCount` (`PttQuickExit.cs`) —
  method uses block body (not expression body),
  `int raw = own?.Count > 0 ? own.Count : (leaderCount > 0 ? leaderCount : 3);` present
  (fallback is `3`, not `2`),
  `return Math.Min(raw, 3);` present with DW-B106 comment.

- **[T6]** `SnapshotTargetOrders` (`PttGlobalQuickExit.cs`) —
  `nativeTargets` and `pttTargets` declared as separate `List<(double Price, int Qty)>`,
  `isNative` condition: `StartsWith("Target", Ordinal)` AND `Length > 6` AND `char.IsDigit([6])`,
  `isPtt` condition: (`StartsWith("PTT-QX-T", Ordinal)` AND `Length > 8` AND `char.IsDigit([8])`)
  OR `StartsWith("PTT-BE-Target-", Ordinal)`,
  `if (isNative)` adds to `nativeTargets`; `else if (isPtt)` adds to `pttTargets`,
  return statement: `nativeTargets.Count > 0 ? nativeTargets : pttTargets`,
  null/empty input returns `nativeTargets` (empty list, not null) — JS-002 satisfied.

- **[T7]** No existing tests broken by these changes.
  (Note: test project may not compile due to pre-existing unrelated errors
  DW-B102-DEFER-01 and DW-B102-DEFER-02 — this is acceptable and does not fail T7.
  T7 concerns only regressions introduced by B107 changes.)

---

### CYC Before/After Table

| Method | File | CYC Before | CYC After | Delta | Limit | Status |
|--------|------|-----------|-----------|-------|-------|--------|
| `TryReplacePttBeBrackets` | `CopyEngine.cs` | 6 | 7 | +1 | 8 | PASS |
| `ExecuteOne` | `PttGlobalQuickExit.cs` | 2 | 2 | 0 | 8 | PASS |
| `SnapshotTargetOrders` | `PttGlobalQuickExit.cs` | 4 | 7 | +3 | 8 | PASS |
| `ResolveTargetCount` | `PttQuickExit.cs` | 2 | 2 | 0 | 8 | PASS |

`try/finally` adds zero branches (not a conditional construct).
`Math.Min` is a library call, not a branch.
`isNative`/`isPtt` are bool assignments (compound expressions, not decision-point branches).

---

### Spec-to-Change Traceability

| Spec Requirement | Change(s) | Closes |
|-----------------|-----------|--------|
| DW-B105: `_qxCancelInProgress` field | CHANGE A | DW-B105 |
| DW-B105: early-return guard in `TryReplacePttBeBrackets` | CHANGE B | DW-B105 |
| DW-B105: set/clear guard in `ExecuteOne` via try/finally | CHANGE C | DW-B105 |
| DW-B106: hard cap at 3 in `ResolveTargetCount` + fallback 2→3 | CHANGE E | DW-B106 + DW-B63-01 intent |
| DW-B106: two-pass `SnapshotTargetOrders` preferring native ATM targets | CHANGE D | DW-B106 |

---

### Build Verification Steps (engineer runs after all 5 changes applied)

1. Run all 7 scans above — every scan must return zero new findings.
2. Run `powershell -File scripts\ptt-sync-and-verify.ps1` — must show 0 MISMATCH lines.
3. Press **F5** in NinjaTrader 8 — must compile green (zero errors).
4. Confirm verifier criteria T1–T7 pass by code inspection.
5. Commit: `git commit -m "feat(ptt): B107 DW-B105 + DW-B106 intent-guard + target-count cap"`
