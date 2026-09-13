# Ticket 1 Verification: PTT-DEFERRED-0911 Lane F (Retry Cycle 2)

**Phase:** 4b (Verification)
**Verifier:** ptt-verifier
**Date:** 2026-09-11
**Ticket:** T1 -- DW-BUG-0911-A-02
**Retry cycle:** 2
**Scope:** TICKET 1 (DW-BUG-0911-A-02) ONLY

---

## Scope Lock

This verification covers Ticket 1 (DW-BUG-0911-A-02) ONLY.
No other tickets were read or evaluated in this session.

Files examined (READ-ONLY):
- `src/PropTraderTools/CopyEngine.cs`
- `src/PropTraderTools/CopyEngineTests.cs`
- `docs/brain/PTT-DEFERRED-0911/ticket-1-completion-lane-f.md`

---

## Prior VERIFY_FAIL Deviation Resolutions

### Deviation 1: FlatGuardLoop Location / Access Modifier

**VERIFY_FAIL finding (Retry 1):**
`FlatGuardLoop` was placed in a new `internal static class FlattenHelpers` outside `CopyEngine`.
Access was widened from `private` (spec T1.5) to `internal` without authorization.

**Orchestrator ruling:** Restore to `private static` inside `CopyEngine`.

**Verifier independent confirmation (Retry 2):**
- Source line 9113: `private static void FlatGuardLoop<TAccount>(` -- inside `CopyEngine` class body.
- Lines 9130-9131: closing braces are `}` (CopyEngine) then `}` (namespace). No intervening class.
- `grep FlattenHelpers src/PropTraderTools/CopyEngine.cs` -- 0 hits. Class does not exist.
- `grep FlattenHelpers src/PropTraderTools/CopyEngineTests.cs` -- 0 hits.
- Both call sites use bare `FlatGuardLoop(` (direct call within class) -- confirmed lines 5488, 5535.

**DEVIATION 1: RESOLVED** -- `private static` inside `CopyEngine` confirmed.

---

### Deviation 2: Test Body (Structural vs Behavioral .Invoke())

**VERIFY_FAIL finding (Retry 1):**
Ticket spec T1.4/SCAN-07(5) required behavioral `.Invoke()`. Implementation was structural-only.

**Orchestrator ruling:**
Behavioral `.Invoke()` is blocked by Agile.NET module cctor. Structural test is acceptable if:
(a) Verifies `FlatGuardLoop` second param is `Func<TAccount, bool>` (not `NinjaTrader.Cbi.Position?`)
(b) Has no `[Fact(Skip=...)]` annotation
and the test passes.

**Agile.NET cctor blocker rationale (documented in source code, lines 8874-8877):**
`CopyEngine` is obfuscated by Agile.NET. Agile.NET injects a module `.cctor` that initializes the
obfuscation runtime. Any `.Invoke()` on any method within the `PropTraderTools` assembly from the
xUnit test host triggers this `.cctor` before the reflective call executes. The `.cctor` attempts
to load NT8 runtime types/DLLs absent in the xUnit host, causing `TypeInitializationException`.
This is an assembly-level constraint, not a test design problem.

**How structural test satisfies spec intent:**
The spec goal is: confirm `NinjaTrader.Cbi.Position?` is eliminated from `FlatGuardLoop`'s
delegate parameter. The structural test directly verifies this at the metadata level:
- `parameters[1].ParameterType` is `Func`2` (Func<TAccount,bool>)
- `p1ReturnType == typeof(bool)` (NOT `NinjaTrader.Cbi.Position`)
This is a stronger contract assertion: it verifies the type at metadata level.

**Verifier independent confirmation (Retry 2):**
- Line 8878: `[Fact]` with no `Skip` argument -- confirmed.
- Line 8882: `typeof(CopyEngine)` -- confirmed (not `typeof(FlattenHelpers)`).
- Line 8883: `BindingFlags.NonPublic | BindingFlags.Static` -- no `Public` flag.
- Lines 8897-8900: `parameters[1].ParameterType` checked as `Func`2`, then
  `p1ReturnType = p1.GetGenericArguments().Last()` asserted as `typeof(bool)`.
- Test PASSES (not SKIPPED): dotnet test filter result = Passed:1, Skipped:0.

**DEVIATION 2: DOCUMENTED AND ACCEPTED** -- Agile.NET cctor constraint documented in source
code and test passes as structural param-type check.

---

## All 7 Independent Scan Results (Layer 3)

### SCAN-01: lock() pattern

```
Command: Select-String -Path "src\PropTraderTools\CopyEngine.cs","src\PropTraderTools\CopyEngineTests.cs" -Pattern "^\s+lock\s*\("
Result: (no output -- 0 hits)
```

**SCAN-01: PASS -- 0 hits**
Layer 2 report: 0. Layer 3 independent result: 0. No discrepancy.

---

### SCAN-02: McCabe complexity of FlatGuardLoop (manual count)

Source lines 9113-9129 (independently read):

```
private static void FlatGuardLoop<TAccount>(...)   base = 1
foreach (var acc in getAccounts())                +1  (loop decision)
if (isFlat(acc))                                  +1  (branch decision)
                                        TOTAL CYC = 3
```

No `&&` or `||` inside method body. No nested loops. No additional branches.

**SCAN-02: PASS -- CYC=3 (<=8)**
Layer 2 report: CYC=3. Layer 3 independent result: CYC=3. No discrepancy.

---

### SCAN-03: async/await in executable code

```
Command: Select-String -Path "src\PropTraderTools\CopyEngine.cs","src\PropTraderTools\CopyEngineTests.cs" -Pattern "\basync\b|\bawait\b" | Where-Object { $_ -notmatch "^\s*//" }
Result: 11 hits in CopyEngine.cs -- ALL confirmed comment-only lines
        0 hits in CopyEngineTests.cs
```

All 11 hits are in `// comment` annotations. Zero executable `async`/`await` keywords.

**SCAN-03: PASS -- 0 executable hits**
Layer 2 report: 0 executable. Layer 3 independent result: 0 executable. No discrepancy.

---

### SCAN-04: Non-ASCII bytes

```
Command (CopyEngine.cs):
  [System.IO.File]::ReadAllBytes("src/PropTraderTools/CopyEngine.cs") | Where-Object { $_ -gt 127 } | Measure-Object | Select-Object -ExpandProperty Count
Result: 0

Command (CopyEngineTests.cs):
  [System.IO.File]::ReadAllBytes("src/PropTraderTools/CopyEngineTests.cs") | Where-Object { $_ -gt 127 } | Measure-Object | Select-Object -ExpandProperty Count
Result: 0
```

**SCAN-04: PASS -- 0 non-ASCII bytes in both files**
Layer 2 report: 0. Layer 3 independent result: 0. No discrepancy.

---

### SCAN-05: AtmStrategy calls

```
Command: Select-String -Path "src\PropTraderTools\CopyEngine.cs","src\PropTraderTools\CopyEngineTests.cs" -Pattern "AtmStrategyCreate|AtmStrategyChangeStopTarget" | Where-Object { $_ -notmatch "^\s*//" }
Result:
  CopyEngine.cs:7846 -- // NT8 bans: no Account.Change(), no AtmStrategyCreate(), no AtmStrategyChangeStopTarget().
  CopyEngine.cs:8032 -- // NO Account.Change(). NO AtmStrategyCreate(). NO AtmStrategyChangeStopTarget()
```

Both are comment-only prohibition annotations. Zero executable calls.

**SCAN-05: PASS -- 0 executable hits**
Layer 2 report: 2 comment hits, 0 executable. Layer 3 independent result: same. No discrepancy.

---

### SCAN-06: dotnet build + dotnet test

```
Command: dotnet build src/PropTraderTools/ 2>&1
Result:
  Build succeeded.
  0 Warning(s)
  0 Error(s)

Command: dotnet test src/PropTraderTools/ --no-build 2>&1
Result:
  Passed! - Failed: 0, Passed: 176, Skipped: 364, Total: 540, Duration: 597 ms

Command: dotnet test src/PropTraderTools/ --no-build --filter "Flatten_Instrument_FlatFollower_SkipsDispatch_FlatGuard"
Result:
  Passed! - Failed: 0, Passed: 1, Skipped: 0, Total: 1, Duration: 127 ms
```

Target test `Flatten_Instrument_FlatFollower_SkipsDispatch_FlatGuard`: PASS (not SKIP).
Test delta vs pre-T1 baseline (Passed=175, Skipped=365): +1 Pass, -1 Skip. Correct.

**SCAN-06: PASS -- 0 build errors, 0 test failures, target test SKIP->PASS**
Layer 2 report: same results. No discrepancy.

---

### SCAN-07: Structural verification

**(a) FlatGuardLoop is `private static` INSIDE `CopyEngine` (not in a separate class):**
- CopyEngine.cs line 9113: `private static void FlatGuardLoop<TAccount>(` -- confirmed.
- Lines 9130-9131: `}` (CopyEngine class close), `}` (namespace close). No intervening class.
- `grep FlattenHelpers`: 0 hits in both files.
- PASS.

**(b) FlatGuardLoop second param is `Func<TAccount, bool> isFlat`:**
- CopyEngine.cs line 9115: `Func<TAccount, bool> isFlat,` -- confirmed.
- NOT `Func<TAccount, NinjaTrader.Cbi.Position?>`.
- PASS.

**(c) Both call sites pass bool-returning lambdas:**
- Line 5490: `acc => { var p = FindPosition(acc, instrument); return p == null || p.Quantity == 0; }`
  -- returns `bool`. PASS.
- Line 5537: `acc => { var p = FindPosition(acc, instrument); return p == null || p.Quantity == 0; }`
  -- returns `bool`. PASS.

**(d) No `[Fact(Skip=...)]` in CopyEngineTests.cs for target test:**
- Line 8878: `[Fact]` -- no `Skip` argument. PASS.

**(e) Test references `typeof(CopyEngine)` with `NonPublic | Static` BindingFlags:**
- Line 8882: `typeof(CopyEngine)` -- confirmed. PASS.
- Line 8883: `BindingFlags.NonPublic | BindingFlags.Static` -- no `Public` flag. PASS.

**SCAN-07: PASS on all sub-checks (a-e)**
Layer 2 report: all pass. Layer 3 independent result: all pass. No discrepancy.

---

## Semantic Verification

**Predicate: `pos == null || pos.Quantity == 0` (or semantically equivalent)**

Both call sites (independently read from CopyEngine.cs):

| Call Site | Line | Lambda |
|-----------|------|--------|
| Flatten(Instrument) | 5490 | `acc => { var p = FindPosition(acc, instrument); return p == null \|\| p.Quantity == 0; }` |
| Flatten(Account, Instrument) | 5537 | `acc => { var p = FindPosition(acc, instrument); return p == null \|\| p.Quantity == 0; }` |

Both lambdas:
- Express exact `pos == null || pos.Quantity == 0` semantics (using local `p` for clarity).
- Return `bool` (matching `Func<TAccount, bool> isFlat` parameter type).
- `FindPosition` returns the NT8 Position object; the lambda guards null-or-zero-quantity (flat condition).
- No NT8 `NinjaTrader.Cbi.Position?` type reference anywhere in the delegate or its body.

**SEMANTIC CHECK: PASS**

---

## DNA Rule Compliance

| Rule | Category | Result | Evidence |
|------|----------|--------|----------|
| JS-021 no lock | P0 CONCURRENCY | PASS | SCAN-01: 0 hits |
| JS-023 no Monitor/Mutex | P0 CONCURRENCY | PASS | SCAN-01 + no hits in grep |
| JS-001 no throw in dispatch | P0 TYPE SAFETY | PASS | FlatGuardLoop is void, no throw statement |
| JS-002 no null return | P0 TYPE SAFETY | PASS | FlatGuardLoop is void; lambdas return bool |
| JS-013 CYC<=8 | P1 COMPLEXITY | PASS | SCAN-02: CYC=3 |
| ASCII-001 no non-ASCII | P0 NT8 | PASS | SCAN-04: 0 non-ASCII bytes |
| async/await ban | P0 NT8 | PASS | SCAN-03: 0 executable hits |
| AtmStrategy ban | P0 NT8 | PASS | SCAN-05: 0 executable hits |
| private constructor | P1 CONSTRUCTION | N/A | No new singleton/struct construction in T1 scope |
| Freeze() on brushes | P1 IMMUTABILITY | N/A | No WPF brush creation in T1 scope |

---

## Architecture Compliance

| Requirement | Source | Result |
|-------------|--------|--------|
| FlatGuardLoop `private static` inside `CopyEngine` | T1.3/T1.5, arch plan §3.2 | PASS (line 9113) |
| Signature: `Func<TAccount,bool> isFlat` (not Position?) | T1.3 | PASS (line 9115) |
| Body: `if (isFlat(acc)) continue` pattern | T1.3 | PASS (lines 9120-9126) |
| Call site 1: `Flatten(Instrument)` uses FlatGuardLoop | T1.3 | PASS (line 5488) |
| Call site 2: `Flatten(Account,Instrument)` uses FlatGuardLoop | T1.3 | PASS (line 5535) |
| Both call sites pass `pos==null\|\|pos.Quantity==0` lambda | T1.3 | PASS (lines 5490, 5537) |
| `[Fact]` no Skip on target test | T1.4 | PASS (line 8878) |
| Test: `typeof(CopyEngine)` NonPublic\|Static | T1.4 | PASS (lines 8882-8883) |
| Test: asserts second param is `Func<TAccount,bool>` | T1.4 | PASS (lines 8897-8900) |
| Test passes (not skips) | T1.4 | PASS (SCAN-06 filter result) |
| No FlattenHelpers class | Deviation 1 fix | PASS (grep 0 hits) |
| Agile.NET cctor blocker documented in source | Deviation 2 docs | PASS (lines 8874-8877) |

---

## Layer 2 vs Layer 3 Discrepancy Report

| Scan | Engineer Layer 2 | Verifier Layer 3 | Discrepancy |
|------|-----------------|-----------------|-------------|
| SCAN-01 lock | 0 hits | 0 hits | None |
| SCAN-02 CYC | 3 | 3 | None |
| SCAN-03 async | 0 executable | 0 executable | None |
| SCAN-04 non-ASCII | 0 | 0 | None |
| SCAN-05 AtmStrategy | 0 executable | 0 executable | None |
| SCAN-06 build/test | 0 err, target PASS | 0 err, target PASS | None |
| SCAN-07 structural | all pass | all pass | None |

**No Layer 2 / Layer 3 discrepancies found.**

---

## Overall Verdict

All 7 independent scans: PASS.
Both prior VERIFY_FAIL deviations: RESOLVED (Deviation 1) / ACCEPTED with documentation (Deviation 2).
All DNA rules: PASS.
All architecture requirements: PASS.
Semantic predicate at both call sites: PASS.
Build: 0 errors. Test suite: 0 failures. Target test: PASS (not SKIP).

**VERIFY_PASS**

---

*ptt-verifier -- PTT-DEFERRED-0911 Lane F -- Ticket 1 -- Retry Cycle 2 -- 2026-09-11*
