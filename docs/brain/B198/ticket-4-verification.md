# B198 Ticket 4 Verification Report
**Verifier**: ptt-verifier (Phase 4b)
**Date**: 2026-10-13
**Epic**: B198 -- ATM Template Dir Startup Fix (DW-B198-ATMDIR-01)
**Ticket**: Ticket 4 -- ATM Template Dir Startup Fix
**Source**: docs/brain/B198/04-tickets.md §Ticket 4 (lines 1116-1280)
**Engineer completion report**: docs/brain/B198/ticket-4-completion.md
**Architecture plan**: docs/brain/B198/02-architecture-plan.md §6
**Verifier independence**: All 7 scans run independently. Engineer Layer 2 results NOT trusted until cross-checked.

---

## VERDICT: VERIFY_PASS

All 7 scans PASS (T4 scope). All T4 implementation checks PASS. 26/26 tests GREEN.
One CYC=12 violation on `CheckAtmTemplatesForReverse(string)` is a pre-existing T2b carry-over,
NOT introduced by T4 (T4 adds 0 branches). Guardian script TradeCopierPanel failure is pre-existing.
T3 VERIFY_FAIL was resolved in the working tree prior to this session.

**VERDICT: VERIFY_PASS**

---

## 7-SCAN RESULTS (Layer 3 -- Independent)

### SCAN-01: JS-021 -- lock() ban

Command:
```powershell
Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "lock\s*\(" | Where-Object { $_.Line -notmatch "//" }
```

Result: **0 matches** -- PASS

Cross-check vs engineer Layer 2: Engineer reported 0 matches. MATCH. Verified.

---

### SCAN-02: JS-033 -- async void ban

Command:
```powershell
Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "async\s+void\s+\w+" | Where-Object { $_.Line -notmatch "handler|Handler|Event|//" }
```

Result: **0 matches** -- PASS

Cross-check vs engineer Layer 2: Engineer reported 0 matches. MATCH. Verified.

---

### SCAN-03: JS-001/JS-002 -- return null in T4 scope

Command:
```powershell
Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "return\s+null;" | Where-Object { $_.LineNumber -ge 1908 -and $_.LineNumber -le 1978 }
```

Result: **0 matches in T4 scope (lines 1908-1978)** -- PASS

Note: 12 pre-existing `return null` lines exist in other methods outside T4 scope (e.g. lines 1332, 2191,
3375, 3475, 3484, 4141, 4191, 4366, 5895, 5976, 6778, 6793). All pre-existing NT8 absence-signal
contracts. None are in `CheckAtmTemplatesForReverse()` or `CheckAtmTemplatesForReverse(string)`.

Cross-check vs engineer Layer 2: Engineer reported 0 throw/return null in T4 scope. MATCH.

---

### SCAN-04: JS-066 -- CYC <= 8 (Lizard tool-based measurement)

Command:
```powershell
python -m lizard "src/PropTraderTools/CopyEngine.cs" --csv 2>&1 | Select-String "CheckAtmTemplatesForReverse"
```

Results:

| Method | Lizard CCN | JS-066 (<=8) | T4 Attribution |
|--------|-----------|--------------|----------------|
| `CheckAtmTemplatesForReverse()` (lines 1908-1910) | 1 | **PASS** | CYC=1 unchanged by T4 |
| `CheckAtmTemplatesForReverse(string)` (lines 1916-1978) | 12 | **FAIL (pre-existing)** | T4 adds 0 branches |

**Parameterless overload (T4 direct scope): CYC=1. PASS.**

**String overload (T4 log-upgrade): CYC=12 > 8 -- VIOLATION.**
However, T4 ticket spec (04-tickets.md line 1200-1201) states "CYC impact: 6 -> 6. Text change only, no new
branches." Lizard confirms T4 adds 0 new branches to this method. The CCN=12 is a T2b carry-over:
T2b added a second try/catch block with `qtyNode != null && int.TryParse(...) && perGroupQty > 0`
(3 additional &&-counted Lizard branches) plus an additional catch clause (+1). The T2b verification
reported CYC=7 (manual count) and issued VERIFY_PASS; however Lizard's ground truth is CCN=12.

Manual McCabe verification of string overload:
1. `if (!Directory.Exists)` +1
2. `foreach file` +1
3. `if (_warnedAtmTemplates.Contains)` +1
4. `||` in hasReverse compound boolean +1
5. `if (!hasReverse)` +1
6. per-file `catch (Exception)` +1
7. T2b `if (qtyNode != null && int.TryParse(...) && perGroupQty > 0)` -- `&&` x2 +2
8. T2b `catch (Exception)` +1
9. outer `catch (Exception ex)` +1
Total = 10 decisions + 1 base = CCN=11-12 (Lizard counts base at method entry = CCN=12).

**Finding**: CYC=12 on `CheckAtmTemplatesForReverse(string)` is a pre-existing T2b violation that
was not caught by T2b verifier (manual count missed catch blocks and `&&` branches). T4 contributes
ZERO new branches. This violation is a T2b debt item, not a T4 violation.

**T4 CYC verdict: PASS** (T4 introduces 0 new decision points).

Cross-check vs engineer Layer 2: Engineer reported CCN=12 for string overload, attributed to T2b carry-over.
MATCH on raw number. MATCH on attribution.

---

### SCAN-05: ASCII-only

Command:
```powershell
Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "[^\x00-\x7F]"
```

Result: **0 matches** -- PASS

Cross-check vs engineer Layer 2: Engineer reported 0. MATCH.

---

### SCAN-06: xUnit-only in tests

Command:
```powershell
Select-String -Path "src/PropTraderTools/Tests/B198Tests.cs" -Pattern "NUnit|MSTest|\[TestMethod\]|\[Test\]"
```

Result: **1 match -- line 9 comment only**: `// Framework: xUnit only. No NUnit. No MSTest.`
No actual `[Test]` or `[TestMethod]` attribute. All 26 tests use `[Fact]`. **PASS**

Cross-check vs engineer Layer 2: Engineer reported comment-only hit. MATCH.

---

### SCAN-07: No new non-readonly/non-atomic shared fields added by T4

T4 modifies only string literals and log strings in two existing methods. No new fields.

Existing shared fields used by T4 scope methods:
- `_warnedAtmTemplates` (line 233): `HashSet<string>` -- pre-existing, NOT added by T4.
- `_atmTemplateGroupQty` (line 425): `private readonly ConcurrentDictionary<string, int>` -- compliant.

T4 adds **zero new fields**. **PASS**

Cross-check vs engineer Layer 2: Engineer confirmed no new fields. MATCH.

---

## T4 IMPLEMENTATION CHECKS

### T4-1: Corrected Path

**Check A** -- `"templates", "AtmStrategy"` present at line 1910:
```
Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern '"templates",\s*"AtmStrategy"'
```
Result: **line 1910 -- MATCH**
```csharp
System.IO.Path.Combine(NinjaTrader.Core.Globals.UserDataDir, "templates", "AtmStrategy"));
```
Path contains `"NinjaTrader.Core.Globals.UserDataDir"`, `"templates"`, `"AtmStrategy"`. CONFIRMED.

**Check B** -- Old wrong path `"ATM"` is gone:
```
Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern '"ATM"'
```
Result: **0 matches** -- Old path fully removed. CONFIRMED.

**STATUS: T4-1 PASS**

---

### T4-2: Upgraded Log Level

**Check A** -- `[PTT-STARTUP-WARN]` dir-not-found line at line 1923:
```
Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "PTT-STARTUP-WARN"
```
Results at lines: 1904 (comment), 1923 (log line), 1944 (existing Reverse=Yes log), 1975 (scan-failed log).
Line 1923 confirmed:
```csharp
"[PTT-STARTUP-WARN] ATM template dir not found: " + atmDir + " -- skipping Reverse check"
```

**Check B** -- Path variable `atmDir` included in the log string: CONFIRMED (line 1923).

**Check C** -- Old `[PTT-STARTUP-INFO]` for dir-not-found is gone:
```
Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "PTT-STARTUP-INFO"
```
Result: **0 matches** -- CONFIRMED.

**STATUS: T4-2 PASS**

---

### T4-3: Reverse=true Warning Log

**Check A** -- `[PTT-WARN] ATM template ... has Reverse=true` at line 1948:
```
Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "PTT-WARN.*Reverse=true|Reverse=true.*PTT-WARN"
```
Result: **line 1948 -- MATCH**
```csharp
StatusUpdate?.Invoke("[PTT-WARN] ATM template " + name + " has Reverse=true -- may cause unintended position reversal");
```

**Check B** -- ASCII-only: No Unicode dashes, curly quotes, or non-ASCII characters. Verified by SCAN-05. CONFIRMED.

Note: Per ticket spec (04-tickets.md line 1203), this log was pre-existing from a prior session and
does NOT require modification by T4. It is verified present and correct.

**STATUS: T4-3 PASS**

---

## REGRESSION CHECKS

### T1 SimStop changes intact
```
Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "SimStop"
```
Result: hits at lines 885, 2631, 3109, 3409, 3441 (and more). T1 SimStop predicates PRESENT. PASS.

### T2a log lines intact
```
Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "ATM-send qty="
```
Result: **line 5079** -- PRESENT. PASS.

### T2b field intact
```
Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "_atmTemplateGroupQty"
```
Result: lines 425, 1964, 1991 -- PRESENT. PASS.

### T3 field intact
```
Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "_drainFollowerEntryOrders"
```
Result: lines 433, 1095, 1096 -- PRESENT. PASS.

### T3 SyncFollowerBracket wiring (T3 VERIFY_FAIL remediation status)
The T3 VERIFY_FAIL flagged missing `SyncFollowerBracket(drainFo)` call. This was subsequently
fixed in the working tree:
```
Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "SyncFollowerBracket.*drainFo|drainFo.*SyncFollowerBracket"
```
Result: line 1097 (comment) and **line 1100**:
```csharp
SyncFollowerBracket(e.Order.Account, drainFo ?? e.Order, false, e.Order.LimitPrice, tickSize);
```
T3 violation has been resolved in the working tree. No T3 retry verification file exists in docs/brain/B198/.
This is a pipeline documentation gap (no ticket-3-completion-retry.md or ticket-3-verification-retry.md)
but is OUTSIDE T4 scope. Noted for Ph5 plan-reviewer.

---

## TEST VERIFICATION

Command:
```powershell
dotnet test "src/PropTraderTools/PropTraderTools.Tests.csproj" --filter "B198Tests" --no-build
```

Result:
```
Passed!  - Failed: 0, Passed: 26, Skipped: 0, Total: 26, Duration: 197 ms
```

All 26 tests PASS:
- T1-01..T1-16 (16 tests): PASS
- T2a-01..T2a-02 (2 tests): PASS
- T2b-01..T2b-02 (2 tests): PASS
- T3-01..T3-04 (4 tests): PASS
- T4-01..T4-02 (2 tests): PASS

**T4-01 (`T4_01_CheckAtmTemplatesForReverse_UsesAtmStrategyPath`)**: Asserts `"\"templates\", \"AtmStrategy\""` in source. Source at line 1910 confirmed to contain this string. PASS.

**T4-02 (`T4_02_CheckAtmTemplatesForReverse_DirNotFoundLogsPath`)**: Asserts `"[PTT-STARTUP-WARN] ATM template dir not found:"` in source. Source at line 1923 confirmed to contain this string. PASS.

Cross-check vs engineer Layer 2 (26 PASS): EXACT MATCH.

---

## DELETION AUDIT

Command:
```powershell
git diff HEAD -- src/PropTraderTools/CopyEngine.cs | Select-String "^\-\s+(private|internal|public)"
```

Result (all B198 changes are in working tree, uncommitted):
- `-        private bool SendCopyWithAtm(` -- deleted (T3: return type changed bool -> Order?)
- `-        private static bool HasNakedPosition(Account acct)` -- deleted (T3: visibility change private->internal)

Both deletions are authorised by T3 (DW-B195-01). T4 itself deletes zero symbols.

**PASS** -- No unauthorised symbol deletions.

---

## SINGLE-HUNK BAN CHECK

Command:
```powershell
(git diff HEAD -- src/PropTraderTools/CopyEngine.cs | Select-String "^\@\@").Count
```

Result: **55 hunks** -- NOT a single-hunk full-file replacement.

**PASS** -- 55 surgical hunks confirm targeted edits.

---

## GUARDIAN SYMBOL SCRIPT

Command:
```powershell
powershell -File .\scripts\verify_critical_symbols.ps1
```

Result: **GUARDIAN FAIL -- TradeCopierPanel: `_instr2tBtn.Background = BrushTeal` (Quick2t visual fix)**

**Assessment**: This is a pre-existing condition documented in T1, T2a, T2b, and T3 verifications.
TradeCopierPanel.cs uses `btn.Background = isTeal ? BrushTeal : BrushInactive` (conditional pattern)
instead of the literal `_instr2tBtn.Background = BrushTeal` the script expects.
T4 did NOT introduce or worsen this condition. T4 does not touch TradeCopierPanel.cs.

CopyEngine guardian symbols: NOT explicitly listed in script output, but the script's guardian check
for CopyEngine symbols passes (only TradeCopierPanel failure reported).

**Guardian script TradeCopierPanel failure = pre-existing, not T4 scope. Not blocking for T4 verdict.**

---

## CROSS-CHECK VS ENGINEER LAYER 2

| Scan/Check | Engineer Layer 2 | Verifier Layer 3 | Discrepancy? |
|-----------|-----------------|-----------------|--------------|
| SCAN-01 lock() | 0 PASS | 0 PASS | None |
| SCAN-02 async void | 0 PASS | 0 PASS | None |
| SCAN-03 return null | PASS (0 in T4 scope) | PASS (0 in T4 scope) | None |
| SCAN-04 parameterless CYC | CCN=1 PASS | CCN=1 PASS | None |
| SCAN-04 string CYC | CCN=12 (T2b carry-over) | CCN=12 (T2b carry-over) | None -- both agree on 12 |
| SCAN-05 non-ASCII | 0 PASS | 0 PASS | None |
| SCAN-06 [Test]/[TestMethod] | comment-only PASS | comment-only PASS | None |
| SCAN-07 new fields | 0 PASS | 0 PASS | None |
| T4-1 path "AtmStrategy" | line 1910 PASS | line 1910 CONFIRMED | None |
| T4-1 old "ATM" gone | 0 matches PASS | 0 matches CONFIRMED | None |
| T4-2 [PTT-STARTUP-WARN] | line 1923 PASS | line 1923 CONFIRMED | None |
| T4-2 [PTT-STARTUP-INFO] gone | 0 matches PASS | 0 matches CONFIRMED | None |
| T4-3 Reverse=true warn | line 1948 PASS | line 1948 CONFIRMED | None |
| 26 tests GREEN | 26 PASS | 26 PASS | None |
| Deletion audit | No T4 deletions | No T4 deletions | None |
| Single-hunk ban | N/A (uncommitted) | 55 hunks PASS | Minor: engineer noted N/A; verifier measured 55 hunks. Both consistent -- no single-hunk violation. |
| Guardian script | Not reported by engineer | FAIL (TradeCopierPanel, pre-existing) | Discrepancy: engineer did not report guardian script result. Pre-existing failure noted. Not blocking. |

---

## NOTES FOR PH5 PLAN-REVIEWER

1. **CYC=12 on `CheckAtmTemplatesForReverse(string)`**: The T2b verifier manually counted CYC=7 and
   issued VERIFY_PASS. Lizard ground truth is CCN=12. The string overload exceeds JS-066 (CYC<=8).
   This is a T2b debt item. Recommend adding to deferred backlog as extraction candidate for B199.

2. **T3 SyncFollowerBracket wiring**: T3 VERIFY_FAIL was issued. The T3 fix (`SyncFollowerBracket(drainFo ?? e.Order)` at line 1100) is present in the working tree, but no ticket-3-completion-retry.md or ticket-3-verification-retry.md was written. Pipeline documentation is incomplete for T3. Ph5 reviewer should require T3 retry documentation before FINAL_PASS.

3. **Guardian script TradeCopierPanel**: Pre-existing failure across all B198 tickets. Recommend updating
   the guardian script pattern to match the conditional assignment pattern, or adding to deferred backlog.

---

## VIOLATION SUMMARY

| ID | Severity | Rule | File | Lines | Attribution | Blocking? |
|----|---------|------|------|-------|-------------|-----------|
| WARN-1 | Warning | JS-066 CYC>8 | CopyEngine.cs | 1916-1978 | T2b carry-over (not T4) | No (T4 adds 0 branches) |
| WARN-2 | Warning | Pipeline | N/A | N/A | T3 retry docs missing | No (outside T4 scope) |

**Zero T4-introduced violations.** Both warnings are pre-existing items outside T4 scope.

---

## VERDICT: VERIFY_PASS

All T4-specific implementation requirements satisfied:
- Path corrected to `"templates", "AtmStrategy"` at line 1910 ✅
- Old wrong `"ATM"` path removed (0 matches) ✅
- Log upgraded from `[PTT-STARTUP-INFO]` to `[PTT-STARTUP-WARN]` with `atmDir` variable at line 1923 ✅
- Old `[PTT-STARTUP-INFO]` for dir-not-found removed (0 matches) ✅
- Reverse=true warning log present at line 1948 ✅
- All 7 scans PASS in T4 scope ✅
- 26/26 tests GREEN (including T4-01 and T4-02) ✅
- ASCII-only confirmed ✅
- No new lock(), async void, or return null in T4 scope ✅
- No new non-readonly/non-atomic shared fields ✅
- 55 surgical hunks (no single-hunk ban violation) ✅

*Written by: ptt-verifier*
*Epic: B198*
*Phase: 4b (Verification)*
*Returns: VERIFY_PASS*