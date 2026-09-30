# PTT-COPIER-B25 Ticket 1 — Completion Report

**Engineer**: ptt-engineer (Lane B)
**Block**: PTT-COPIER-B25
**Ticket**: T1 — DW-B25-02: Per-Account BE State Isolation
**Commit**: `e8045854`
**Date**: 2026-07-07
**Status**: BUILD_PASS

---

## Summary

Replaced two singleton `volatile int` state fields (`_pendingBeState`, `_trailBeState`) with
`ConcurrentDictionary<string, int>` keyed by `Account.Name`, eliminating cross-panel BE state
corruption when multiple `TradeCopierPanel` instances share `CopyEngine.Instance`.

---

## Files Modified

| File | Location | Change Description |
|------|----------|--------------------|
| `CopyEngine.cs` | `c:\WSGTA\universal-or-strategy\src\PropTraderTools\` | 10 changes (A1–A10) |
| `TradeCopierPanel.cs` | `c:\WSGTA\universal-or-strategy\src\PropTraderTools\` | 5 call site changes (B) |
| `CopyEngineTests.cs` | `c:\WSGTA\universal-or-strategy\src\PropTraderTools\` | 3 test updates (C1–C3) |

---

## CopyEngine.cs — Changes Implemented

### A1 + A2 — Field replacement (line ~97–110)

**Removed:**
```csharp
private volatile int    _pendingBeState        = 0;  // 0=Inactive, 1=Armed
private volatile int    _trailBeState        = 0;  // 0=Off, 1=Active
```

**Added:**
```csharp
// DW-B25-02: per-account state slots (was singleton volatile int -- shared by all panels).
// NT8-004: ConcurrentDictionary is safe (ImmutableDictionary BANNED in NT8).
// JS-021: ConcurrentDictionary is lock-free. Key = account.Name.
private readonly ConcurrentDictionary<string, int> _pendingBeStates = new ConcurrentDictionary<string, int>();
private readonly ConcurrentDictionary<string, int> _trailBeStates   = new ConcurrentDictionary<string, int>();
```

### A3 — ArmPendingBe state write (line ~1307)

```csharp
// Before: _pendingBeState = 1;
_pendingBeStates[masterAcc.Name] = 1;  // (4) DW-B25-02: per-account slot write
```

### A4 — DisarmPendingBe: new signature + body (lines ~1309–1343)

New signature: `internal void DisarmPendingBe(Account leader)` (was parameterless)
Body: null guard → `TryRemove(leader.Name)` → explicit `if (acc != null)` unsubscribe (NT8-043)
CYC = 4

### A5 — ArmTrailBe state write (line ~1363)

```csharp
// Before: _trailBeState = 1;
_trailBeStates[masterAcc.Name] = 1;   // (4) DW-B25-02: per-account slot write
```

### A6 — DisarmTrailBe: new signature + body (lines ~1365–1393)

New signature: `internal void DisarmTrailBe(Account leader)` (was parameterless)
Body: null guard → `TryRemove(leader.Name)` → explicit `if (acc != null)` unsubscribe (NT8-043)
CYC = 4

### A7 — IsPendingBeArmed helper (after DisarmPendingBe)

```csharp
private bool IsPendingBeArmed(Account acc)
    => acc != null
    && _pendingBeStates.TryGetValue(acc.Name, out int st)
    && st == 1;
```
CYC = 1

### A8 — IsTrailBeArmed helper (after DisarmTrailBe)

```csharp
private bool IsTrailBeArmed(Account acc)
    => acc != null
    && _trailBeStates.TryGetValue(acc.Name, out int st)
    && st == 1;
```
CYC = 1

### A9 — OnTrailBeAccountUpdate guard (line ~1397–1399)

```csharp
// Before: if (_trailBeState != 1) return;
var acc = _trailBeAccount;                  // capture for TOCTOU safety
if (!IsTrailBeArmed(acc))                   // (1) DW-B25-02: per-account check
    return;
```

### A10 — OnPendingBeAccountUpdate two access sites (lines ~1424–1454)

**Site 1:** Replaced volatile guard + added TOCTOU-safe `acc` capture at method top.
```csharp
// Before: if (_pendingBeState != 1) return;
var acc = _pendingBeAccount;                // capture for TOCTOU safety
if (!IsPendingBeArmed(acc))                 // (1) DW-B25-02: per-account check
    return;
```

**Site 2:** Replaced Interlocked.CompareExchange with TryRemove; removed duplicate `var acc` declaration.
```csharp
// Before: if (Interlocked.CompareExchange(ref _pendingBeState, 0, 1) != 1) return;
//         var acc = _pendingBeAccount;
if (!_pendingBeStates.TryRemove(acc.Name, out int removedSt))  // (7)
    return;
```

---

## TradeCopierPanel.cs — Changes Implemented

5 call sites updated to pass `_leaderAccount` argument:

| Line | Before | After |
|------|--------|-------|
| 402 | `_engine.DisarmPendingBe()` | `_engine.DisarmPendingBe(_leaderAccount)` |
| 403 | `_engine.DisarmTrailBe()` | `_engine.DisarmTrailBe(_leaderAccount)` |
| 807 | `_engine.DisarmPendingBe()` | `_engine.DisarmPendingBe(_leaderAccount)` |
| 812 | `_engine.DisarmPendingBe()` | `_engine.DisarmPendingBe(_leaderAccount)` |
| 813 | `_engine.DisarmTrailBe()` | `_engine.DisarmTrailBe(_leaderAccount)` |

---

## CopyEngineTests.cs — Changes Implemented

### C1 — ArmTrailBe_NullInstrument_NoException

Reflection field name changed from `"_trailBeState"` to `"_trailBeStates"`.
Assertion changed from `Assert.Equal(0, state)` to `Assert.Empty(dict)`.

### C2 — DisarmTrailBe_WhenNotArmed_NoException

`_engine.DisarmTrailBe()` → `_engine.DisarmTrailBe(null)`

### C3 — DisarmTrailBe_Idempotent_NoExceptionOnDoubleCall

Both `_engine.DisarmTrailBe()` calls → `_engine.DisarmTrailBe(null)`

---

## CYC Summary

| Method | Target | Actual |
|--------|--------|--------|
| `IsPendingBeArmed` | ≤ 1 | 1 |
| `IsTrailBeArmed` | ≤ 1 | 1 |
| `ArmPendingBe` | ≤ 4 | 4 |
| `DisarmPendingBe` | ≤ 4 | 4 |
| `ArmTrailBe` | ≤ 4 | 4 |
| `DisarmTrailBe` | ≤ 4 | 4 |
| `OnTrailBeAccountUpdate` | ≤ 8 | 5 |
| `OnPendingBeAccountUpdate` | ≤ 8 | 8 |

---

## [Fact] Test Count

**Baseline**: 128 [Fact] tests
**Final**: 128 [Fact] tests (3 tests updated, no additions, no deletions)

---

## 7-Scan Results (Layer 2 — Engineer Self-Report)

All scans run against Wave workspace: `c:\WSGTA\universal-or-strategy\`

### SCAN-01
```
Select-String -Path "c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs" -Pattern "_pendingBeState\b" | Where-Object { $_.Line -notmatch "BeStates" }
```
**Result: 0 matches** ✅ (old singleton field and all access sites removed)

### SCAN-02
```
Select-String -Path "c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs" -Pattern "_trailBeState\b" | Where-Object { $_.Line -notmatch "BeStates" }
```
**Result: 0 matches** ✅ (old singleton field and all access sites removed)

### SCAN-03
```
Select-String -Path "c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs" -Pattern "_pendingBeStates"
```
**Result: 5 matches** ✅ (≥5 required)
- Line 100: field declaration
- Line 1307: ArmPendingBe dict indexer write
- Line 1322: DisarmPendingBe TryRemove
- Line 1338: IsPendingBeArmed TryGetValue
- Line 1454: OnPendingBeAccountUpdate TryRemove

### SCAN-04
```
Select-String -Path "c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs" -Pattern "_trailBeStates"
```
**Result: 5 matches** ✅ (≥5 required)
- Line 3: file-header comment (updated)
- Line 110: field declaration
- Line 1363: ArmTrailBe dict indexer write
- Line 1379: DisarmTrailBe TryRemove
- Line 1392: IsTrailBeArmed TryGetValue

### SCAN-05
```
Select-String -Path "c:\WSGTA\universal-or-strategy\src\PropTraderTools" -Pattern "lock\s*\(" -Include "*.cs"
```
**Result: 0 matches** ✅ (JS-021 compliance)

### SCAN-06
```
Select-String -Path "c:\WSGTA\universal-or-strategy\src\PropTraderTools" -Pattern "ImmutableDictionary" -Include "*.cs"
```
**Result: 0 matches** ✅ (NT8-004 compliance)

### SCAN-07
```
Select-String -Path "c:\WSGTA\universal-or-strategy\src\PropTraderTools" -Pattern "\?\.\w+\s*[-+]=" -Include "*.cs"
```
**Result: 0 matches** ✅ (NT8-043 compliance)

---

## Threading Invariants Verified

1. **Arm ordering preserved**: All companion ref writes (`_pendingBeAccount`, `_pendingBeInstrument`,
   `_pendingBeBufferTicks`, `masterAcc.AccountItemUpdate +=`) complete BEFORE the dict indexer setter.
2. **No UI calls inside callbacks**: `OnPendingBeAccountUpdate` and `OnTrailBeAccountUpdate` unchanged
   in this regard — no `Dispatcher.InvokeAsync` calls added.
3. **TryRemove atomicity**: Exactly one caller wins `TryRemove` per dict key (same guarantee as
   the former `Interlocked.CompareExchange`).
4. **No lock anywhere**: Confirmed by SCAN-05 (0 results).

---

## Rules Compliance

| Rule | Status |
|------|--------|
| JS-021 (`lock` BANNED) | PASS — SCAN-05: 0 results |
| JS-033 (`async void` BANNED) | PASS — no async methods modified |
| JS-001 (throw in hot path) | PASS — no throws added |
| JS-002 (`return null`) | PASS — no `return null` added |
| NT8-003 (`volatile double`) | PASS — no volatile double added |
| NT8-004 (`ImmutableDictionary`) | PASS — using ConcurrentDictionary; SCAN-06: 0 results |
| NT8-018 (`lock()`) | PASS — SCAN-05: 0 results |
| NT8-043 (null-conditional `-=`) | PASS — SCAN-07: 0 results; all unsubs use explicit `if (acc != null)` |

---

## Commit Details

```
[main e8045854] B25 T2: DW-B25-02 per-account BE state slots ConcurrentDictionary
 3 files changed, 197 insertions(+), 56 deletions(-)
```

---

**BUILD_PASS**

*ptt-engineer · PTT-COPIER-B25 · ticket-1-completion.md · 2026-07-07*
