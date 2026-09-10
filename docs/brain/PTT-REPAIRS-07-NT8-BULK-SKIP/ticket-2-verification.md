# ticket-2-verification.md
# Epic: PTT-REPAIRS-07-NT8-BULK-SKIP
# Ticket: T2 -- OPTION B Individual Skip (3 mixed classes)
# Phase: 4b -- Verification (RE-VERIFY after RETRY-1)
# Verifier: ptt-verifier
# Session: Re-verify session; RETRY-1 repairs to 3 pre-existing violations

---

## RETRY-1 CONTEXT

Prior VERIFY_FAIL identified 3 pre-existing violations in T2-scope classes:
1. BwaveCycTaR6HelperTests: 5 NT8-runtime skips, spec requires 6. Missing 6th TypeInit skip.
2. B79CancelRaceGuardTests: 3 of 4 TypeInit targets used wrong "obfuscation" skip reason.
3. BwaveCycT1R1BeHelperTests: 2 TypeInit targets used wrong "obfuscation" skip reason.

RETRY-1 engineer fixed all 3 violations by changing skip reasons on the affected tests.

---

## ENGINEER LAYER 2 SUMMARY (from ticket-2-completion.md -- RETRY-1)

File modified: src/PropTraderTools/CopyEngineTests.cs

Violation 1 fix (BwaveCycTaR6HelperTests):
  Line 7218: IsPositionStateRelevant_ShouldExist_AsPrivateStaticHelper
  Changed: "obfuscation: AgileDotNetRT..." -> "NT8-runtime: CopyEngine.cctor requires NT8 host"
  Post-fix NT8-runtime count in class: 6

Violation 2 fix (B79CancelRaceGuardTests):
  Line 5849: T_DW_B79_09_01_CancelQxBrackets2Param_HasRemoveAllGuard
  Line 5881: T_DW_B79_09_02_CancelQxBrackets3Param_HasRemoveAllGuard
  Line 5945: B132_LaneB_DiagnosticMode_FieldExists
  Changed: "obfuscation: AgileDotNetRT..." -> "NT8-runtime: CopyEngine.cctor requires NT8 host"
  Line 5918: already had NT8-runtime -- unchanged.
  Post-fix NT8-runtime count in class: 4

Violation 3 fix (BwaveCycT1R1BeHelperTests):
  Line 6564: GetSenderAccountName_ShouldReturnEmpty_WhenSenderIsNull
  Line 6574: GetSenderAccountName_ShouldReturnEmpty_WhenSenderIsNotAccount
  Changed: "obfuscation: AgileDotNetRT..." -> "NT8-runtime: CopyEngine.cctor requires NT8 host"
  Post-fix NT8-runtime count in class: 2
  23 non-TypeInit tests: already had "obfuscation" skip -- unchanged.

---

## VERIFIER INDEPENDENT CHECKS (Layer 3)

### Check 1: Violation 1 -- BwaveCycTaR6HelperTests (lines 7110-7268)

Command: Select-String -Path "src/PropTraderTools/CopyEngineTests.cs"
         -Pattern "NT8-runtime: CopyEngine.cctor requires NT8 host"
         | Where LineNumber -ge 7110 -and LineNumber -le 7268

Result:
  Line 7145: [Fact(Skip = "NT8-runtime: CopyEngine.cctor requires NT8 host")] -- ExtractLegSuffix_ShouldReturnNull
  Line 7155: [Fact(Skip = "NT8-runtime: CopyEngine.cctor requires NT8 host")] -- ExtractLegSuffix_ShouldReturnDigit
  Line 7218: [Fact(Skip = "NT8-runtime: CopyEngine.cctor requires NT8 host")] -- IsPositionStateRelevant_ShouldExist (THE FIX)
  Line 7225: [Fact(Skip = "NT8-runtime: CopyEngine.cctor requires NT8 host")] -- IsPositionStateRelevant_ShouldReturnFalse
  Line 7234: [Fact(Skip = "NT8-runtime: CopyEngine.cctor requires NT8 host")] -- IsPositionStateRelevant_ShouldReturnTrue_Filled
  Line 7243: [Fact(Skip = "NT8-runtime: CopyEngine.cctor requires NT8 host")] -- IsPositionStateRelevant_ShouldReturnTrue_PartFilled

NT8-runtime skip count: 6
Required: 6
CHECK 1: PASS -- Violation 1 FIXED.

### Check 2: Violation 2 -- B79CancelRaceGuardTests (lines 5823-6455)

Command: Select-String -Path "src/PropTraderTools/CopyEngineTests.cs"
         -Pattern "NT8-runtime: CopyEngine.cctor requires NT8 host"
         | Where LineNumber -ge 5823 -and LineNumber -le 6455

Result:
  Line 5849: [Fact(Skip = "NT8-runtime: CopyEngine.cctor requires NT8 host")] -- T_DW_B79_09_01 (FIXED)
  Line 5881: [Fact(Skip = "NT8-runtime: CopyEngine.cctor requires NT8 host")] -- T_DW_B79_09_02 (FIXED)
  Line 5918: [Fact(Skip = "NT8-runtime: CopyEngine.cctor requires NT8 host")] -- T_DW_B79_09_03 (already correct)
  Line 5945: [Fact(Skip = "NT8-runtime: CopyEngine.cctor requires NT8 host")] -- B132_LaneB_DiagnosticMode (FIXED)

NT8-runtime skip count: 4
Required per RETRY-1 scope: 4
Obfuscation-skipped Lane A tests: 59 (verified unchanged)
CHECK 2: PASS -- Violation 2 FIXED per RETRY-1 Director scope.

NOTE: Architecture plan (02-architecture-plan.md) states 5 TypeInit targets for B79CancelRaceGuardTests.
RETRY-1 Director scope explicitly states 4 NT8-runtime skips as the target. The 5th TypeInit target
was not identified in RETRY-1 scope (the prior VERIFY_FAIL said "1 more to identify via filter re-run"
but the Director scoped RETRY-1 to fix the 3 wrong-reason tests + preserve the 1 already correct).
The RETRY-1 scope as stated by the Director is satisfied.

### Check 3: Violation 3 -- BwaveCycT1R1BeHelperTests (lines 6469-6685)

Command: Select-String -Path "src/PropTraderTools/CopyEngineTests.cs"
         -Pattern "NT8-runtime: CopyEngine.cctor requires NT8 host"
         | Where LineNumber -ge 6469 -and LineNumber -le 6685

Result:
  Line 6564: [Fact(Skip = "NT8-runtime: CopyEngine.cctor requires NT8 host")] -- GetSenderAccountName_WhenSenderIsNull (FIXED)
  Line 6574: [Fact(Skip = "NT8-runtime: CopyEngine.cctor requires NT8 host")] -- GetSenderAccountName_WhenSenderIsNotAccount (FIXED)

NT8-runtime skip count: 2
Required: 2
CHECK 3: PASS -- Violation 3 FIXED.

### Check 4: Non-TypeInit tests in BwaveCycT1R1BeHelperTests

Command: Select-String -Pattern "obfuscation:" | Where LineNumber in [6469-6685] | Count

Result: 23 obfuscation-skipped tests -- UNCHANGED from prior session.
Required: 23 non-TypeInit tests with "obfuscation" skip reason, NOT changed.
CHECK 4: PASS.

### Check 5: No new violations introduced

Verified via 7-scan results below. No lock(), no non-ASCII, no FontFamily, no hex colors,
no CreateOrder violations, no DateTime.Now, no block().
CHECK 5: PASS.

---

## 7-SCAN RESULTS (Layer 3 -- independent)

### SCAN-01: lock() check
Command: Select-String -Path "src/PropTraderTools/CopyEngineTests.cs" -Pattern "lock\("
Result: No output (0 matches)
Required: 0
SCAN-01: PASS

### SCAN-02: Non-ASCII check
Command: [System.Text.RegularExpressions.Regex]::Matches((Get-Content "src/PropTraderTools/CopyEngineTests.cs" -Raw), '[^\x00-\x7F]').Count
Result: 0
Required: 0
SCAN-02: PASS

### SCAN-03: FontFamily scan
Command: Select-String -Path "src/PropTraderTools/CopyEngineTests.cs" -Pattern "FontFamily"
Result: No output (0 matches)
Required: 0
SCAN-03: PASS

### SCAN-04: Hex color scan
Command: Select-String -Path "src/PropTraderTools/CopyEngineTests.cs" -Pattern "#[0-9A-Fa-f]{6}"
Result: No output (0 matches)
Required: 0
SCAN-04: PASS

### SCAN-05: CreateOrder PTT- prefix check
Command: Select-String -Path "src/PropTraderTools/CopyEngineTests.cs" -Pattern "CreateOrder"
Result: 20 matches -- ALL in comments and test method names only (no live CreateOrder calls)
No live acc.CreateOrder() invocations exist in the test file.
Required: 0 live CreateOrder calls without PTT- prefix
SCAN-05: PASS

### SCAN-06: DateTime.Now check
Command: Select-String -Path "src/PropTraderTools/CopyEngineTests.cs" -Pattern "DateTime\.Now[^U]"
Result: No output (0 matches)
Required: 0
SCAN-06: PASS

### SCAN-07: block( check
Command: Select-String -Path "src/PropTraderTools/CopyEngineTests.cs" -Pattern "\bblock\s*\("
Result: No output (0 matches)
Required: 0
SCAN-07: PASS

---

## DNA RULE AUDIT (against actual source, CopyEngineTests.cs)

- JS-021 ASCII-only: SCAN-02 confirmed 0 non-ASCII characters. PASS.
- JS-003 No lock(): SCAN-01 confirmed 0 lock( matches. PASS.
- JS-001 No throw in dispatch: No logic code added (skip string changes only). PASS.
- JS-002 No return null: No logic code added. PASS.
- NT8 sealed keyword: Not applicable to test file. PASS.
- NT8 async/await: Not applicable to skip attribute changes. PASS.
- FontFamily=: SCAN-03 confirmed 0 matches. PASS.
- #RRGGBB hex colors: SCAN-04 confirmed 0 matches. PASS.
- CreateOrder PTT- prefix: SCAN-05 -- all CreateOrder references are in comments/names. PASS.
- DateTime.Now: SCAN-06 confirmed 0 matches. PASS.
- block( : SCAN-07 confirmed 0 matches. PASS.

---

## LAYER 2 vs LAYER 3 CROSS-CHECK

| Check | Engineer Layer 2 | Verifier Layer 3 | Match? |
|-------|-----------------|------------------|--------|
| BwaveCycTaR6HelperTests NT8-runtime count | 6 | 6 | YES |
| B79CancelRaceGuardTests NT8-runtime count | 4 | 4 | YES |
| BwaveCycT1R1BeHelperTests NT8-runtime count | 2 | 2 | YES |
| BwaveCycT1R1BeHelperTests obfuscation count | 23 | 23 | YES |
| B79CancelRaceGuardTests Lane A (obfuscation) count | 59 | 59 | YES |
| SCAN-01 lock( | 0 | 0 | YES |
| SCAN-02 non-ASCII | 0 | 0 | YES |
| SCAN-03 FontFamily | N/A | 0 | PASS |
| SCAN-04 hex color | N/A | 0 | PASS |
| SCAN-06 DateTime.Now | N/A | 0 | PASS |
| SCAN-07 block( | N/A | 0 | PASS |

All verifier-checked counts match engineer Layer 2 report.

---

## VIOLATIONS SUMMARY (RETRY-1)

All 3 violations from prior VERIFY_FAIL have been corrected:

| Violation | Prior State | Current State | Status |
|-----------|-------------|---------------|--------|
| 1: BwaveCycTaR6HelperTests missing 6th NT8-runtime skip (line 7218) | obfuscation skip | NT8-runtime skip | FIXED |
| 2a: B79CancelRaceGuardTests line 5849 wrong skip reason | obfuscation | NT8-runtime | FIXED |
| 2b: B79CancelRaceGuardTests line 5881 wrong skip reason | obfuscation | NT8-runtime | FIXED |
| 2c: B79CancelRaceGuardTests line 5945 wrong skip reason | obfuscation | NT8-runtime | FIXED |
| 3a: BwaveCycT1R1BeHelperTests line 6564 wrong skip reason | obfuscation | NT8-runtime | FIXED |
| 3b: BwaveCycT1R1BeHelperTests line 6574 wrong skip reason | obfuscation | NT8-runtime | FIXED |

No new violations introduced.
No production code modified.
Only CopyEngineTests.cs was modified.

---

## VERDICT

VERIFY_PASS

All 3 VERIFY_FAIL violations from the prior session have been correctly fixed in RETRY-1.
All 7 independent scans pass. No DNA rule violations. No new regressions.
The RETRY-1 scope as stated by the Director is fully satisfied.
