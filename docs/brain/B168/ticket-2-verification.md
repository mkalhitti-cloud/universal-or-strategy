# Ticket B168-2 Verification Report
## PTT Verifier — Independent Verification
## Block: B168 | Ticket: B168-2 | Verifier: ptt-verifier

---

## Verdict: VERIFY_PASS

---

## Scope

File verified: `src/PropTraderTools/CopyEngineTests.cs`
Spec: DW-B168-01 §6 + 04-tickets.md B168-2

All checks run INDEPENDENTLY. Engineer scan results were NOT trusted until cross-checked.

---

## Check 8 — Test method definitions (IsLeaderLegBeingChanged_*)

Command:
```
Select-String -Path src/PropTraderTools/CopyEngineTests.cs -Pattern "IsLeaderLegBeingChanged_"
```

Results:
| Line | Method Name |
|------|-------------|
| 10242 | `public void IsLeaderLegBeingChanged_ChangeSubmitted_ReturnsTrue()` |
| 10249 | `public void IsLeaderLegBeingChanged_ChangePending_ReturnsTrue()` |
| 10256 | `public void IsLeaderLegBeingChanged_Working_ReturnsFalse()` |
| 10263 | `public void IsLeaderLegBeingChanged_Null_ReturnsFalse()` |

**Exactly 4 method definitions. PASS.**

Matches engineer self-report (SCAN-07): YES — identical line numbers and names.

---

## Check 9 — IsLeaderLegBeingChangedTestable call sites

Command:
```
Select-String -Path src/PropTraderTools/CopyEngineTests.cs -Pattern "IsLeaderLegBeingChangedTestable"
```

Results:
| Line | Content |
|------|---------|
| 10175 | `// DW-B168-01: Tests for IsLeaderLegBeingChanged via IsLeaderLegBeingChangedTestable seam.` (COMMENT) |
| 10245 | `Assert.True(CopyEngine.IsLeaderLegBeingChangedTestable(order));` |
| 10252 | `Assert.True(CopyEngine.IsLeaderLegBeingChangedTestable(order));` |
| 10259 | `Assert.False(CopyEngine.IsLeaderLegBeingChangedTestable(order));` |
| 10265 | `Assert.False(CopyEngine.IsLeaderLegBeingChangedTestable(null));` |

Code call sites: **4** (lines 10245, 10252, 10259, 10265). Line 10175 is a comment.
**One call per test, all via seam. PASS.**

Matches engineer self-report (SCAN-02): YES.

---

## Check 10 — DwB168Tests class body: no NT8 runtime dependencies

Command:
```
Select-String -Path src/PropTraderTools/CopyEngineTests.cs -Pattern "NinjaTrader\.Client|OnExecutionUpdate|Account\.All"
  | Where-Object { $_.LineNumber -gt 10175 }
```

Result: **(no output)** — zero NT8 runtime dependency references in new code.

Body inspection (lines 10177–10267):
- `StubOrder` uses `FormatterServices.GetUninitializedObject` + reflection — no NT8 runtime call ✅
- `NinjaTrader.Cbi.Order` type reference is used as the object type, but no NT8 runtime APIs are invoked (no Account, no OnExecutionUpdate, no Client) ✅
- All 4 test bodies use `StubOrder` helper or pass null — no runtime state required ✅

**PASS — no NT8 runtime dependencies.**

Matches engineer self-report (SCAN-03): YES.

---

## Check 11 — Exact test names match spec

Spec table (DW-B168-01 §6 and 04-tickets.md B168-2):
| # | Required Name | Present in File | Line |
|---|--------------|----------------|------|
| 1 | `IsLeaderLegBeingChanged_ChangeSubmitted_ReturnsTrue` | ✅ | 10242 |
| 2 | `IsLeaderLegBeingChanged_ChangePending_ReturnsTrue` | ✅ | 10249 |
| 3 | `IsLeaderLegBeingChanged_Working_ReturnsFalse` | ✅ | 10256 |
| 4 | `IsLeaderLegBeingChanged_Null_ReturnsFalse` | ✅ | 10263 |

**All 4 exact names match spec. PASS.**

Matches engineer self-report (SCAN-07): YES.

---

## Check 12 — DwB168Tests namespace

File namespace: `namespace PropTraderTools` (line 13).
Class declaration: `public class DwB168Tests` at line 10177 — inside the file's closing `}` at line 10271 which closes the `PropTraderTools` namespace.

**PASS — DwB168Tests is inside namespace PropTraderTools, same as all other test classes.**

Matches engineer self-report: YES (engineer stated "same namespace as all other test classes in file").

---

## DNA Rule Verification (test file)

| Rule | Check | Result |
|------|-------|--------|
| JS-021 | No `lock(` in DwB168Tests (lines 10175+) | PASS |
| ASCII | Zero non-ASCII characters in CopyEngineTests.cs | PASS |
| No NT8 runtime | No NinjaTrader.Client, OnExecutionUpdate, Account.All in test bodies | PASS |
| [Fact] decorator | All 4 tests have [Fact] | PASS |
| Seam access | All 4 use IsLeaderLegBeingChangedTestable, not direct private | PASS |
| StubOrder helper | Used for 3 order constructions; null test passes null directly | PASS |

---

## Comparison vs Engineer Self-Report

| Scan | Engineer | Verifier | Match |
|------|----------|----------|-------|
| SCAN-01 [Fact] count | 4 at lines 10241/10248/10255/10262 | 4 at lines 10241/10248/10255/10262 | YES |
| SCAN-02 seam calls | 4 call sites | 4 code call sites (+ 1 comment) | YES |
| SCAN-03 NT8 runtime | 0 references | 0 references | YES |
| SCAN-04 StubOrder usage | 3 constructions + null direct | 3 constructions + null direct | YES |
| SCAN-05 ASCII | 0 non-ASCII | 0 non-ASCII | YES |
| SCAN-06 lock() | 0 in test code | 0 in test code (lines >10175) | YES |
| SCAN-07 test names | All 4 match spec | All 4 match spec | YES |

**No discrepancies found. Engineer self-report is accurate.**

---

## Additional Observation

The `StubOrder` helper in `DwB168Tests` is self-contained within the class (private static). It uses `FormatterServices.GetUninitializedObject` + reflection for NT8 field injection — the same reflection-based pattern as `MakeOrder` in the existing test file. This is the approved NT8-stub pattern. No NT8 runtime is required for test execution.

---

## Verdict: VERIFY_PASS
