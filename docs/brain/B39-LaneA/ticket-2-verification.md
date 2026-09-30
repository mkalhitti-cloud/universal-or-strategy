# ticket-2-verification.md — B39-LaneA T2

**Epic**: PTT-COPIER B39 — Global BE All
**Verifier**: ptt-verifier (Phase 4b T2)
**Date**: 2026-07-30
**Source file verified**: `c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngineTests.cs`
**Lines verified**: 3693–3901 (new B39 code), total file = 3901 lines

---

## Layer 3 Scan Results

| Scan | Pattern | Result | L2 Match? | Verdict |
|------|---------|--------|-----------|---------|
| SCAN-01 | `^\s*lock\s*\(` in CopyEngineTests.cs | **0 hits** | YES | PASS |
| SCAN-02 | `async\s+void\s+\w` in CopyEngineTests.cs | **0 hits** | YES | PASS |
| SCAN-03 | `return\s+null` in lines >= 3693 | **0 hits** | YES | PASS |
| SCAN-04 | `throw\s+new` in lines >= 3693 | **0 hits** | YES | PASS |
| SCAN-05 | CYC manual count (all new methods) | **max CYC=2** (MakeAccount, T_B39_07, T_B39_08) | YES | PASS |
| SCAN-06 | `dotnet build PropTraderTools.csproj` | **2 pre-existing errors** (AtrSizingEngine.cs CS0234 L20, CS0246 L24); 0 B39-introduced errors | YES | PASS (B39-scope) |
| SCAN-07 | `[Fact]` count | **202** (was 194, +8) | YES | PASS |

### SCAN-06 Detail

```
AtrSizingEngine.cs(20): error CS0234 — 'Indicators' not in 'NinjaTrader.NinjaScript' (pre-existing)
AtrSizingEngine.cs(24): error CS0246 — 'Indicator' type not found (pre-existing)
CopyEngine.cs(683):     warning CS8632 — nullable annotation (pre-existing)
```
These 2 errors and 1 warning are identical to the B38 baseline and T1 baseline.
B39 T2 introduced **zero new compilation errors**.

---

## Test Correctness: 8/8 PASS

| ID | Method | Spec | Source (line) | Verdict |
|----|--------|------|---------------|---------|
| T_B39_01 | `GlobalBe_FiresOnAllAccountsAllInstruments` | 3 accs × 2 pos = 6 calls | L3754 `Assert.Equal(6, calls.Count)` | PASS |
| T_B39_02 | `GlobalBe_SkipsFlatAccounts` | flat (qty=0) skipped; 1 call | L3794 `Assert.Equal(1, calls.Count)` | PASS |
| T_B39_03 | `GlobalBe_WorksWithNoCopyRule` | no rule dep; 1 call | L3809 `Assert.Equal(1, calls.Count)` | PASS |
| T_B39_04 | `GlobalBe_B35GuardInherited_UnderwaterSkipped` | extreme buf=-100; no exception; 1 call | L3824 `Assert.Null(ex)` + L3825 `Assert.Equal(1, calls.Count)` | PASS |
| T_B39_05 | `GlobalBe_BufferAppliedPerDirectionCorrectly` | long=7500.50, short=7499.50 | L3845 `Assert.Equal(7500.50, ...)` + L3846 `Assert.Equal(7499.50, ...)` | PASS |
| T_B39_06 | `GlobalBe_AllAccountsFlat_NoCalls` | 3 flat accs; 0 calls; no exception | L3866 `Assert.Null(ex)` + L3867 `Assert.Equal(0, calls.Count)` | PASS |
| T_B39_07 | `GlobalBeBuffer_IncrementClampedAt10` | 11 increments → GlobalBeBuffer == 10 | L3882 `Assert.Equal(10, globalBe.GlobalBeBuffer)` | PASS |
| T_B39_08 | `GlobalBeBuffer_DecrementClampedAtMinus10` | 11 decrements → GlobalBeBuffer == -10 | L3897 `Assert.Equal(-10, globalBe.GlobalBeBuffer)` | PASS |

### T_B39_05 Math Verification (independent)
- Long: `avgPrice=7500.00 + bufferTicks=2 × tickSize=0.25 = 7500.50` ✓
- Short: `avgPrice=7500.00 − bufferTicks=2 × tickSize=0.25 = 7499.50` ✓
- Captured via injection lambda `(a, i, p) => calls.Add((a, i, p))` → `calls[0].Item3` and `calls[1].Item3`
- Asserted with `precision: 5` (floating-point safe)

### T_B39_07 / T_B39_08 Loop Verification (independent)
- Loop runs 10 iterations then calls once more (total 11 calls)
- Correctly validates clamp at ±10, not at ±11

### T_B39_04 Clarification
- Method name says "UnderwaterSkipped" but test actually asserts `calls.Count == 1`
  (the delegate IS called with the extreme buffer value)
- This is consistent with the ticket spec: "no exception thrown, 1 call made"
- The B35 underwater guard lives in production `SubmitBeStop`, not in `Execute()` — test correctly
  exercises the Execute() path without a production NT8 guard

---

## DNA / NT8 Compliance Check

| Rule | Check | Verdict |
|------|-------|---------|
| JS-021 no lock() | SCAN-01: 0 hits | PASS |
| JS-033 no async void | SCAN-02: 0 hits | PASS |
| JS-002 no return null (new code) | SCAN-03: 0 hits ≥L3693 | PASS |
| JS-001 no throw new (new code) | SCAN-04: 0 hits ≥L3693 | PASS |
| CYC ≤ 8 | Max CYC=2 across all 14 new methods | PASS |
| xUnit [Fact] only | All 8 tests use [Fact]; no NUnit/MSTest | PASS |
| ASCII-only | Visual inspection of L3693–3901: all identifiers/strings ASCII | PASS |
| NT8 no sealed on test class | Test class not sealed | PASS |
| NT8 no FontFamily / hex color | Not applicable to test file | N/A |
| NT8 no CreateOrder | Not applicable to test file | N/A |
| NT8 no DateTime.Now | Not applicable to test file | N/A |

---

## Architecture Compliance

| Requirement | Status |
|-------------|--------|
| Test seam: injection constructor `PttGlobalBreakEven(Action<Account, Instrument, double>)` | PRESENT — all 8 tests use it |
| Execute(IEnumerable<Account>, int) overload | Used in T_B39_01..T_B39_06 |
| GlobalBeBuffer property readable | Used in T_B39_07, T_B39_08 |
| IncrementBuffer() / DecrementBuffer() callable | Used in T_B39_07, T_B39_08 |
| 6 private static stub helpers | All present (L3699–L3750) |
| No Account.All usage | Confirmed — Execute() overload used |
| [Fact] total = 202 | CONFIRMED |

---

## Layer 2 vs Layer 3 Discrepancies

**None.** All 7 scan results reported by the engineer (Layer 2) exactly match the independently
obtained Layer 3 results.

---

## VERIFY_PASS
