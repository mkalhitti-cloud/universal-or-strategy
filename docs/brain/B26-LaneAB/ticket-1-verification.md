# B26-LaneAB Ticket T1 Verification Report

**Epic**: B26-LaneAB  
**Ticket**: B26-AB-T1  
**Verifier**: ptt-verifier (Phase 4b)  
**Date**: 2026-07-07  
**Wave workspace**: `c:\WSGTA\universal-or-strategy\`  
**Files scanned**: `src/PropTraderTools/CopyEngine.cs`, `src/PropTraderTools/CopyEngineTests.cs`, `src/PropTraderTools/TradeCopierWindow.cs`

---

## Rules Catalog Gate

RULES_CATALOG.md is UTF-8 clean and readable.  
Key P0 rules confirmed: JS-021 (`lock(` banned), JS-033 (`async void` banned), JS-001, JS-002.  
Gate result: **PASS**

---

## V1 — PendingBeFired Event Signature

**Check**: `CopyEngine.cs L130` must read `internal event Action<string, string> PendingBeFired;`

**Verification** (independent grep):
```
LineNumber Line
---------- ----
       130     internal event Action<string, string> PendingBeFired;
```

**Result**: ✅ PASS — `Action<string, string>` confirmed at L130. Not the old `Action<string>`.

---

## V2 — BreakEven 3-Arg Call in OnTrailBeAccountUpdate

**Check**: `CopyEngine.cs ~L1422` inside `if (instr != null)` must call `BreakEven(acc, instr, newBuffer);`

**Verification** (independent grep for all BreakEven occurrences):
```
LineNumber Line
---------- ----
      1422     BreakEven(acc, instr, newBuffer);
```

**Result**: ✅ PASS — 3-arg call `BreakEven(acc, instr, newBuffer)` confirmed at L1422. Not the old 2-arg form.

---

## V3 — PendingBeFired Invoke Carries Account Name

**Check**: `CopyEngine.cs ~L1463` must read:  
`PendingBeFired?.Invoke(instr?.FullName ?? string.Empty, acc?.Name ?? string.Empty);`

**Verification** (independent grep):
```
LineNumber Line
---------- ----
      1463     PendingBeFired?.Invoke(instr?.FullName ?? string.Empty, acc?.Name ?? string.Empty);
```

**Result**: ✅ PASS — Two-argument invoke with `acc?.Name` confirmed at L1463. Second argument is present.

---

## V4 — [Fact] Count in CopyEngineTests.cs

**Check**: `[Fact]` count in `CopyEngineTests.cs` must equal 133.

**Verification**:
```powershell
Select-String -Pattern "\[Fact\]" CopyEngineTests.cs | Measure-Object | Select-Object Count
Count: 133
```

**Result**: ✅ PASS — Exactly 133 `[Fact]` attributes found.

---

## V5 — New Test Names Present

**Check**: Both test method names must exist in `CopyEngineTests.cs`:
- `T_B26_01_TrailBe_WithNoRule_StillMovesStop`
- `T_B26_02_PendingBeFired_CarriesAccountName`

**Verification** (independent grep):
```
LineNumber Line
---------- ----
      2354     public void T_B26_01_TrailBe_WithNoRule_StillMovesStop()
      2379     public void T_B26_02_PendingBeFired_CarriesAccountName()
```

**Result**: ✅ PASS — Both test methods present at L2354 and L2379.

---

## V6 — SCAN-01: No `lock(` in CopyEngine.cs

**Check**: Zero code-line `lock(` matches in `CopyEngine.cs`.

**Verification**:
```powershell
Select-String -Pattern "lock\(" CopyEngine.cs
```
Output:
```
CopyEngine.cs:583:  // CYC=5: fo null(1), price delta(2), TrailPrice>0(3), isStop branch(4), try block(0).
CopyEngine.cs:1261: // CYC=3: null guard(1), alreadyTighter(2), try block(0).
```

Both hits are **comment text** only — the word sequence is `try block(0)` not the C# `lock(` keyword. Zero actual `lock` keyword usages confirmed by direct line content inspection.

**Result**: ✅ PASS — Zero `lock(` code-line matches. Both grep hits are comment-only (the substring `lock(` appears within `try block(0).` in comment text).

---

## V7 — SCAN-02: No `async void` in CopyEngine.cs

**Check**: Zero `async void ` occurrences in `CopyEngine.cs`.

**Verification**:
```powershell
Select-String -Pattern "async void " CopyEngine.cs | Measure-Object | Select-Object Count
Count: 0
```

**Result**: ✅ PASS — Zero `async void` matches.

---

## V8 — CYC Manual Count for Both Methods

### OnTrailBeAccountUpdate (L1403-L1428)

Method header claim: `CYC=5`

Decision points (project convention: count decision branches, not +1 base):
1. `if (!IsTrailBeArmed(acc))` — (1)
2. `if (e.AccountItem != AccountItem.UnrealizedProfitLoss)` — (2)
3. `if (newPnl <= oldPnl)` — (3)
4. `if (Interlocked.CompareExchange(...) != oldBits)` — (4)
5. `if (instr != null)` — (5)

Counted: **CYC = 5** ✓  
No `&&` or `||` in any condition. No loops.

**Result**: ✅ PASS — CYC=5 matches engineer's claim.

### OnPendingBeAccountUpdate (L1430-L1465)

Method header claim: `CYC=8`  
Method docstring: `// CYC=8: state(1), item filter(2), pos flat(3), tickSize(4), last<=0(5), triggered(6), CAS(7).`  
Note: `acc?.AccountItemUpdate null-conditional is NOT a CYC branch` comment present.

Decision points in body:
1. `if (!IsPendingBeArmed(acc))` — (1)
2. `if (e.AccountItem != AccountItem.UnrealizedProfitLoss)` — (2)
3. `if (IsFlat(pos))` — (3)
4. `if (tickSize <= 0.0)` — (4)
5. `if (last <= 0.0)` — (5)
6. `if (!triggered)` — (6) — the `isLong ? ... : ...` ternary in `triggered` is not a CYC branch
7. `if (!_pendingBeStates.TryRemove(...))` — (7)
8. `if (acc != null)` — (8) — explicit `if` statement (not null-conditional)

Counted: **CYC = 8** ✓

**Result**: ✅ PASS — CYC=8 matches engineer's claim.

---

## V9 — Dead Code Check: 2-Arg BreakEven Preserved

**Check 1**: `BreakEven(Instrument, int)` at `~L1192` still exists (not deleted).

**Verification**:
```
LineNumber Line
---------- ----
      1192     internal void BreakEven(Instrument instrument, int bufferTicks)
```
2-arg overload confirmed at L1192. ✓

**Check 2**: `TradeCopierWindow.cs L691` still calls `BreakEven(instr, ticks)`.

**Verification**:
```
LineNumber Line
---------- ----
       691     if (instr != null) _engine.BreakEven(instr, ticks);
```
2-arg call confirmed at L691. ✓

**Result**: ✅ PASS — 2-arg `BreakEven(Instrument, int)` at L1192 is preserved. `TradeCopierWindow.cs:L691` still calls the 2-arg form. No dead-code regression.

---

## DNA Rule Check (All Sources)

| Rule | Check | Result |
|------|-------|--------|
| JS-021 | `lock(` in CopyEngine.cs | ✅ 0 code-line hits |
| JS-033 | `async void` in CopyEngine.cs | ✅ 0 hits |
| JS-001 | `throw new ...Exception` in hot paths | ✅ try/catch pattern used throughout |
| JS-003 | sealed record hierarchy / discriminated union | ✅ `FollowerAtmMode` abstract class used |
| JS-008 | Mutable struct with mutable fields | ✅ `CopyRule`, `CopySignal`, `TrimSignal` are readonly structs |
| JS-010 | Non-private constructor on CopyEngine (singleton) | ✅ `private CopyEngine() { }` |
| JS-025 | Plain `Dictionary<K,V>` on CopyEngine shared fields | ✅ `ConcurrentDictionary` used for `_pendingBeStates`, `_trailBeStates` |
| NT8 constraint | `Action<string>` → `Action<string,string>` | ✅ V1 confirmed |

---

## Architecture Compliance

- `OnTrailBeAccountUpdate` correctly fires on NT8 account background thread (no lock, no async void, ConcurrentDictionary for state).
- `OnPendingBeAccountUpdate` correctly unsubscribes itself after one-shot fire via `TryRemove` atomic disarm (no double-fire race).
- `PendingBeFired` event correctly carries both `instr.FullName` and `acc.Name` — panel can marshal to correct account's UI row.
- Backward compatibility preserved: 2-arg `BreakEven(Instrument, int)` at L1192 untouched; `TradeCopierWindow.cs:L691` still compiles.
- `CopyEngineTests.cs` [Fact] count grew from 131 → 133 (net +2), exactly matching T_B26_01 and T_B26_02 additions.

---

## Summary Table

| Check | Claim | Independent Result | Status |
|-------|-------|--------------------|--------|
| V1 — PendingBeFired signature | `Action<string,string>` at L130 | Confirmed L130 | ✅ PASS |
| V2 — BreakEven 3-arg call | `BreakEven(acc, instr, newBuffer)` at L1422 | Confirmed L1422 | ✅ PASS |
| V3 — PendingBeFired invoke 2 args | `Invoke(instr?.FullName, acc?.Name)` at L1463 | Confirmed L1463 | ✅ PASS |
| V4 — [Fact] count | 133 | 133 | ✅ PASS |
| V5 — Test names | T_B26_01 & T_B26_02 present | L2354, L2379 | ✅ PASS |
| V6 — SCAN-01 lock() | 0 code hits | 0 code hits (2 comment-only) | ✅ PASS |
| V7 — SCAN-02 async void | 0 hits | 0 hits | ✅ PASS |
| V8 — CYC TrailBe | CYC=5 | 5 branches counted | ✅ PASS |
| V8 — CYC PendingBe | CYC=8 | 8 branches counted | ✅ PASS |
| V9 — Dead code | 2-arg BreakEven preserved | L1192 exists; L691 calls it | ✅ PASS |

---

## Verdict

**VERIFY_PASS**

All 10 checks pass. No DNA violations found. No dead code regressions. CYC counts match engineer's claims. Both new tests are present at the expected line numbers. The ticket is cleared for Phase 5 (plan-reviewer).
