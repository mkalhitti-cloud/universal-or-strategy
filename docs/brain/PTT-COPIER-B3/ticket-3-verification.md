# PTT-COPIER-B3 Ticket T3 Verification

**Verifier:** PTT Verifier
**Date:** 2026-06-21
**Retry:** Yes — re-verification after engineer fix to Test 7 (`SetRuleEnabled_UnknownInstrument_NoException`)

---

## Scan Results

All scans run independently by the Verifier against `src/PropTraderTools/CopyEngineTests.cs`.

| Scan | Pattern | Expected | Actual | Result |
|------|---------|----------|--------|--------|
| SCAN-01 | `lock\s*(` | 0 | 0 | ? PASS |
| SCAN-02 | `DateTime.Now[^U]` | 0 | 0 | ? PASS |
| SCAN-03 | `new CopyEngine` | 0 | 0 | ? PASS |
| SCAN-04 | `CreateOrder` | 0 | 0 | ? PASS |
| SCAN-06 | `#[0-9A-Fa-f]{6}` | 0 | 0 | ? PASS |
| SCAN-07 | `FontFamily` | 0 | 0 | ? PASS |
| SCAN-08 | `NUnit` | 0 | 0 | ? PASS |
| SCAN-09 | `MSTest\|TestClass` | 0 | 0 | ? PASS |
| SCAN-10 | `Subscribe()` | 0 | 0 | ? PASS |
| ASCII  | Non-ASCII characters | 0 | 0 | ? PASS |
| [Fact] count | `\[Fact\]` | 17 | 17 | ? PASS |

---

## T3 Check Results (V01–V15)

| ID | Check | Result | Note |
|----|-------|--------|------|
| V01 | File exists at `src/PropTraderTools/CopyEngineTests.cs` | ? PASS | File present, 227 lines |
| V02 | `namespace PropTraderTools` present | ? PASS | Line 10 |
| V03 | `using Xunit;` present | ? PASS | Line 8 |
| V04 | No NUnit using or reference | ? PASS | SCAN-08 = 0 |
| V05 | No MSTest using or TestClass attribute | ? PASS | SCAN-09 = 0 |
| V06 | Exactly 17 `[Fact]` attributes | ? PASS | Count = 17 |
| V07 | `_engine = CopyEngine.Instance` (not `new CopyEngine()`) | ? PASS | Line 14; SCAN-03 = 0 |
| V08 | No `Subscribe()` call anywhere | ? PASS | SCAN-10 = 0 |
| V09 | No `lock(` anywhere | ? PASS | SCAN-01 = 0 |
| V10 | Each test method has a reset call (`SetEnabled(false)` or `SetEnabled(true)`) at start | ? PASS | All 17 tests confirmed: lines 25, 35, 45, 55, 65, 85, 106, 118, 132, 141, 150, 162, 172, 181, 189, 198, 212 |
| V11 | Tests 3–4 access `_dailyCapFloor` via `FieldInfo.GetValue` | ? PASS | Test 3: lines 47–48; Test 4: lines 57–58 |
| V12 | Tests 5–7 ALL access `_rules` via `FieldInfo` and work with `ConcurrentBag<CopyRule>` | ? PASS | Test 5: lines 68–69; Test 6: lines 89–90; Test 7: lines 110–112 (engineer fix confirmed) |
| V13 | Tests 16–17 access `IsDedup` via `MethodInfo` + `BindingFlags.NonPublic \| BindingFlags.Instance` | ? PASS | Test 16: lines 199–201; Test 17: lines 214–216 |
| V14 | All 17 method names match exactly the required list | ? PASS | All 17 names confirmed in order at lines 23, 33, 43, 53, 63, 83, 104, 116, 131, 139, 149, 160, 171, 180, 188, 196, 211 |
| V15 | No stub bodies (no `throw new NotImplementedException()`) | ? PASS | `NotImplementedException` = 0 occurrences; all methods have real assertions |

---

## V12 Detail — Engineer Fix Verified

The prior verification failure was: Test 7 (`SetRuleEnabled_UnknownInstrument_NoException`) did not access `_rules` via `FieldInfo`.

**Fix confirmed at lines 109–112:**
```csharp
// V12: verify _rules still accessible via FieldInfo after no-op call
var fi = GetField("_rules");
var bag = (ConcurrentBag<CopyRule>)fi.GetValue(_engine);
Assert.NotNull(bag);
```

All three of tests 5, 6, and 7 now access `_rules` via `FieldInfo.GetValue` and cast to `ConcurrentBag<CopyRule>`. V12 is satisfied.

---

## Summary

- Total checks: 15
- Passed: 15
- Failed: 0

---

## Decision

VERIFY_PASS
