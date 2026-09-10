# 02-architecture-plan.md
# Epic: PTT-REPAIRS-07-NT8-BULK-SKIP
# Status: PLAN_COMPLETE (REVISION 1 -- REVIEW_FAIL violations RULE-01 + RULE-02 corrected)

---

## LANE-SPLIT GATE RESULT: LANES-APPROVED

Q1. Same method or within 50 lines? NO (changes span thousands of lines across disjoint class ranges)
Q2. Fix B design depends on Fix A final design? NO (independent class ranges, no shared state)
Q3. Each fix has standalone value if the other is blocked? YES
Q4. Each fix has an independent SIM verification path? YES

Two-ticket split approved per gate result.

---

## 1. Problem Statement

302 test failures of the form:

```
System.TypeInitializationException: The type initializer for '<Module>' threw an exception.
  --> System.ArgumentNullException: Value cannot be null.
       at AgileDotNetRT.Initialize() -> CopyEngine..cctor()
```

Root cause: `CopyEngine..cctor()` calls `AgileDotNetRT.Initialize()` which requires a live NT8 COM
context. The xUnit test host has no NT8 process. Any test that causes the CopyEngine singleton
or any AgileDotNetRT-protected type to be first accessed will trigger the module-level type
initializer and fail.

Resolution: Apply `[Fact(Skip = "NT8-runtime: CopyEngine.cctor requires NT8 host")]` to every
test that triggers the CopyEngine cctor, converting TypeInitializationException failures to
controlled Skips.

---

## 2. Skip Attribute (exact text -- ASCII-only, no curly quotes, inline only)

```csharp
[Fact(Skip = "NT8-runtime: CopyEngine.cctor requires NT8 host")]
```

- No SkipReason constants. No variables. Inline string only.
- ASCII-only. No Unicode. No curly quotes.

---

## 3. Jane Street Rule Audit

- JS-021 ASCII-only: Skip string verified ASCII-only. No Unicode characters. PASS.
- JS-003 No lock(): No concurrency code added. No lock() anywhere. PASS.
- JS-001 No throw in dispatch: No logic code added. PASS.
- JS-002 No return null: No logic code added. PASS.
- JS-010 No DateTime.Now: No logic code added. PASS.

ptt-architect constraint: This plan produces ONLY
docs/brain/PTT-REPAIRS-07-NT8-BULK-SKIP/02-architecture-plan.md and
docs/brain/PTT-REPAIRS-07-NT8-BULK-SKIP/04-tickets.md.
No .cs files are written by ptt-architect.

---

## 4. File Inventory (from live file analysis)

Single file affected: `src/PropTraderTools/CopyEngineTests.cs` (7153+ lines)

Class map (line numbers from file, verified by grep):

| Class                         | Start | End  | Total [Fact] | Already-Skipped | Active | TypeInit Fails |
|-------------------------------|-------|------|--------------|-----------------|--------|----------------|
| CopyEngineTests               |    16 | 4236 |          205 |              17 |    188 |            188 |
| CopyEngineB75Tests            |  4237 | 4905 |           61 |              14 |     47 |             47 |
| B77QxRaceGuardTests           |  4906 | 5128 |            8 |               0 |      8 |              8 |
| B78TargetDispatchTests        |  5307 | 5388 |            8 |               0 |      8 |              8 |
| B79BeAllTargetSnapshotTests   |  5478 | 5624 |            8 |               0 |      8 |              7 |
| B79BeReplaceAttemptGuardTests |  5625 | 5691 |            3 |               0 |      3 |              1 |
| B79CancelRaceGuardTests       |  5823 | 6455 |           64 |               0 |     64 |              5 |
| BwaveCycT1R1BeHelperTests     |  6469 | 6685 |           25 |               0 |     25 |              2 |
| BwaveCycTaR6HelperTests       |  7110 | 7268 |           17 |               0 |     17 |              6 |
| BwaveCycTaR7HelperTests       |  7272 |  EOF |           35 |               1 |     34 |             34 |

Classes NOT in scope (Lane A only -- DO NOT TOUCH):
- B78QxFollowerStopTests (lines 5129-5306)
- B78CancelFollowerGuardTests (lines 5389-5477)
- B79BeRetryAtmTriggerTests (lines 5692-5765)
- B79BeReplaceFallbackTests (lines 5766-5822)
- BwaveCycTaR2HelperTests (lines 6686-6811)
- BwaveCycTaR3HelperTests (lines 6812-7109)

---

## 5. Strategy Selection

### OPTION A -- Bulk replace within class range

Apply to classes where ALL active [Fact] tests fail TypeInit (no passing tests at risk):

| Class                   | Active | Skip Count | Already-Skipped to Preserve |
|-------------------------|--------|------------|------------------------------|
| CopyEngineTests         |    188 |        188 | 17 (lines with existing Skip)|
| CopyEngineB75Tests      |     47 |         47 | 14 (mix of single+multi-line)|
| B77QxRaceGuardTests     |      8 |          8 | 0                            |
| B78TargetDispatchTests  |      8 |          8 | 0                            |
| BwaveCycTaR7HelperTests |     34 |         34 | 1 (already uses target string)|

Mechanism: Within each class line range, replace every bare `[Fact]` annotation (no existing Skip)
with `[Fact(Skip = "NT8-runtime: CopyEngine.cctor requires NT8 host")]`.
Do NOT touch any `[Fact(Skip...)]` lines (whether single-line or multi-line format).

CopyEngineB75Tests already-skipped notes (14 total):
- 7 single-line at: 4532, 4570, 4576, 4582, 4674, 4735, 4824
- 7 multi-line (next line has Skip): 4538, 4546, 4554, 4562, 4844, 4852, 4861
Engineer must preserve ALL 14. Pattern: `[Fact(\n  Skip = ...)` must NOT be altered.

BwaveCycTaR7HelperTests already-skipped: 1 at line 8059 already uses the exact target string.
Engineer must NOT double-skip it.

### OPTION B -- Individual test Skip (engineer must verify via test run)

Apply to classes where only SOME active tests fail TypeInit (risk of skipping passing tests):

| Class                         | Active | TypeInit | Non-TypeInit | Spec Category | Plan Category | Justification                                                                 |
|-------------------------------|--------|----------|--------------|---------------|---------------|-------------------------------------------------------------------------------|
| B79BeAllTargetSnapshotTests   |      8 |        7 | 1 (passes)   | OPTION A      | OPTION B      | Spec-deviation: spec listed as OPTION A (all-TypeInit), but live class table shows 8 active tests with only 7 TypeInit failures -- 1 test passes. Applying OPTION A bulk-replace would Skip the 1 passing test, violating the DO-NOT-Skip-passing-tests constraint. OPTION B (individual Skip) is the only safe choice. This deviation is explicit and intentional. |
| B79BeReplaceAttemptGuardTests |      3 |        1 | 2 (pass)     | OPTION A      | OPTION B      | Spec-deviation: spec listed as OPTION A (all-TypeInit), but live class table shows 3 active tests with only 1 TypeInit failure -- 2 tests pass. Applying OPTION A bulk-replace would Skip 2 passing tests, violating the DO-NOT-Skip-passing-tests constraint. OPTION B (individual Skip) is the only safe choice. This deviation is explicit and intentional. |
| B79CancelRaceGuardTests       |     64 |        5 | 59 (Lane A)  | OPTION B      | OPTION B      | Spec-aligned.                                                                 |
| BwaveCycT1R1BeHelperTests     |     25 |        2 | 23           | OPTION B      | OPTION B      | Spec-aligned.                                                                 |
| BwaveCycTaR6HelperTests       |     17 |        6 | 11           | OPTION B      | OPTION B      | Spec-aligned.                                                                 |

**Spec deviation note (RULE-01 resolution):** The spec's OPTION A list for `B79BeAllTargetSnapshotTests` (7 TypeInit) and `B79BeReplaceAttemptGuardTests` (1 TypeInit) does not account for the passing tests that coexist in those classes. The spec's TypeInit counts are correct, but the OPTION A classification is incorrect for both: OPTION A requires ALL active tests to fail TypeInit (no passing tests at risk). Because both classes contain passing tests, OPTION A bulk-replace is unsafe and would violate the explicit constraint "DO NOT Skip currently passing tests." OPTION B is applied to both. This is a corrective deviation from the spec's categorization, fully documented here.

For each OPTION B class, the engineer MUST:
1. Run: `dotnet test --filter "FullyQualifiedName~{ClassName}"` 
2. Record exactly which tests report `TypeInitializationException` in their failure output
3. Apply `[Fact(Skip = "NT8-runtime: CopyEngine.cctor requires NT8 host")]` ONLY to those tests
4. Re-run test filter to confirm those tests now show as Skipped
5. Confirm all other tests in the class are unchanged

---

## 6. OPTION B Pre-Analysis (architect inference -- engineer must verify)

### B79BeReplaceAttemptGuardTests (1 TypeInit)

Tests in class (3 total):
- `T_B79_RG_01_BeReplaceAttempts_FieldExists` (line 5636): uses `typeof(CopyEngine).GetField()`
- `T_B79_RG_02_BeReplaceAttempts_StartsEmpty` (line 5652): calls `CopyEngine.Instance` directly
- `T_B79_RG_03_BeReplaceAttempts_GateIsAtThree` (line 5673): no CopyEngine reference at all

Architect inference: `T_B79_RG_02` is the TypeInit-failing test (calls `CopyEngine.Instance`).
`T_B79_RG_03` should pass (pure int comparison, no NT8 types).
`T_B79_RG_01` inference uncertain (`typeof(CopyEngine)` behavior depends on CLR state).
Engineer MUST verify by running filter before applying Skip.

### B79CancelRaceGuardTests (5 TypeInit)

Tests in class (64 total). Engineer-observed failure patterns for TypeInit triggers:
Tests calling `GetMethodBody().GetILAsByteArray()` (force-loads method IL, may trigger cctor):
- `T_DW_B79_09_01_CancelQxBrackets2Param_HasRemoveAllGuard` (line 5850)
- `T_DW_B79_09_02_CancelQxBrackets3Param_HasRemoveAllGuard` (line 5882)
- `T_DW_B79_09_03_CancelStaleBracketsLocal_HasRemoveAllGuard` (line 5919)

Tests calling `field.GetValue(null)` on a static CopyEngine field (definite cctor trigger):
- `B132_LaneB_DiagnosticMode_FieldExists` (line 5946)

Fifth TypeInit test: engineer must run filter to identify. The other 60 tests use only
`typeof(CopyEngine).GetMethod(...)` / `Assert.NotNull(m)` patterns (Lane A failures, not TypeInit).

DO NOT touch the 59 Lane A tests in this class.

### BwaveCycT1R1BeHelperTests (2 TypeInit)

Tests in class (25 total). Tests calling `CopyEngine.Instance` directly:
- `SelectBeRefPriceByDirection_ShouldReturnBid_WhenLongAndBidIsPositive` (line 6500, CopyEngine.Instance at 6504)
- `SelectBeRefPriceByDirection_ShouldReturnAsk_WhenLongAndBidIsZero` (line 6510, CopyEngine.Instance at 6514)
- `SelectBeRefPriceByDirection_ShouldReturnAsk_WhenShortAndAskIsPositive` (line 6520, CopyEngine.Instance at 6524)
- `SelectBeRefPriceByDirection_ShouldReturnBid_WhenShortAndAskIsZero` (line 6530, CopyEngine.Instance at 6534)
- `GetSenderAccountName_ShouldReturnEmpty_WhenSenderIsNull` (line 6565, CopyEngine.Instance at 6569)
- `GetSenderAccountName_ShouldReturnEmpty_WhenSenderIsNotAccount` (line 6575, CopyEngine.Instance at 6579)

Mission brief states 2 TypeInit failures. Architect cannot determine from static analysis which 2
of these 6 are the ones currently failing (4 may be Lane A failures already). Engineer MUST run
filter and identify by TypeInitializationException message.

DO NOT touch the other 23 tests in this class.

### BwaveCycTaR6HelperTests (6 TypeInit)

Tests in class (17 total). Tests that call `m.Invoke(null, ...)` on CopyEngine static methods
(these definitely execute CopyEngine code paths):
- `ExtractLegSuffix_ShouldReturnNull_WhenLeaderNameHasNoTrailingDigit` (line 7146)
- `ExtractLegSuffix_ShouldReturnDigit_WhenLeaderNameEndsWithDigit` (line 7156)
- `IsPositionStateRelevant_ShouldReturnFalse_WhenStateIsWorking` (line 7226)
- `IsPositionStateRelevant_ShouldReturnTrue_WhenStateIsFilled` (line 7235)
- `IsPositionStateRelevant_ShouldReturnTrue_WhenStateIsPartFilled` (line 7244)

Mission brief states 6 TypeInit failures. Architect identifies 5 clear triggers above.
Engineer must identify the 6th via test run filter.

DO NOT touch the other 11 tests in this class (Lane A scope).

### B79BeAllTargetSnapshotTests (7 TypeInit)

Tests in class (8 total):
- T_B79_BE_01 through T_B79_BE_08 (lines 5490-5613)

All 8 tests reference `NinjaTrader.Cbi.OrderState` enum values. The 1 test that does NOT fail
TypeInit must be determined by test run. Likely candidate: `T_B79_BE_08_TargetSnapshotStateOk_ExactlyFiveStates`
(only accesses `.Length` on a local array, no Assert.Contains/DoesNotContain).
Engineer MUST verify via test run before applying Skip to 7 tests.

---

## 7. Ticket Split

### Ticket 1 -- Pure TypeInit Classes + Partial OPTION B (same file, low risk)

Classes: CopyEngineTests, CopyEngineB75Tests, B77QxRaceGuardTests, B78TargetDispatchTests,
         BwaveCycTaR7HelperTests, B79BeAllTargetSnapshotTests, B79BeReplaceAttemptGuardTests

Target TypeInit-to-Skip conversions: 188 + 47 + 8 + 8 + 34 + 7 + 1 = 293

OPTION A classes (bulk replace within range): CopyEngineTests, CopyEngineB75Tests,
B77QxRaceGuardTests, B78TargetDispatchTests, BwaveCycTaR7HelperTests

OPTION B classes requiring test-run verification: B79BeAllTargetSnapshotTests,
B79BeReplaceAttemptGuardTests

SCAN-5 checkpoint after Ticket 1:
  Skipped >= 32 + 293 = 325
  Failed <= 450 - 293 = 157
  Passed >= 19

### Ticket 2 -- Mixed Classes (OPTION B only -- individual test Skip)

Classes: BwaveCycTaR6HelperTests, B79CancelRaceGuardTests, BwaveCycT1R1BeHelperTests

Target TypeInit-to-Skip conversions: 6 + 5 + 2 = 13

All three classes use OPTION B exclusively. Engineer must run test filter per class,
identify TypeInit-failing tests by exception message, and apply Skip only to those tests.

SCAN-5 checkpoint after Ticket 2 (full epic done):
  Skipped >= 325 + 13 = 338
  Failed <= 157 - 13 = 144
  Passed >= 19

---

## 8. Baseline and Target

BASELINE: Total: 501 | Passed: 19 | Failed: 450 | Skipped: 32

TARGET (after full epic, both tickets):
  Total: 501 (unchanged -- no tests added or removed)
  Skipped: >= 338
  Failed: <= 144
  Passed: >= 19 (must NOT decrease)
  Genuine regressions: 0

TypeInit test conversions: 306 total (188 + 47 + 8 + 8 + 34 + 7 + 1 + 6 + 5 + 2 + 1 = 307... adjusting)

Note on count: mission brief sums = 188 + 47 + 34 + 8 + 8 + 7 + 6 + 5 + 2 + 1 = 306.
This plan uses those counts as authoritative.

---

## 9. Critical Constraints

1. Touch ONLY `src/PropTraderTools/CopyEngineTests.cs`. No production code changes.
2. ASCII-only Skip strings. No Unicode, no curly quotes.
3. No lock() anywhere in the changed file.
4. DO NOT apply Skip to currently PASSING tests.
5. DO NOT apply Skip to tests already Skipped (double-Skip prevention).
   - CopyEngineTests: preserve 17 existing [Fact(Skip...)] annotations
   - CopyEngineB75Tests: preserve 14 existing [Fact(Skip...)] annotations (7 single-line + 7 multi-line)
   - BwaveCycTaR7HelperTests: preserve 1 existing [Fact(Skip...)] at line 8059
6. OPTION B classes: engineer runs test filter BEFORE applying any Skip.
7. Scope boundary: classes with "other N are Lane A scope" -- ONLY apply Skip to TypeInit-failing tests.

---

## 10. 7-Scan Checklist (per ticket)

```
SCAN-1: grep -c "lock(" src/PropTraderTools/CopyEngineTests.cs
        -> Expected: 0

SCAN-2: Check for non-ASCII characters in changed lines
        -> Expected: 0 non-ASCII in any [Fact(Skip = "...")] line

SCAN-3: dotnet build src/PropTraderTools/ --no-restore 2>&1 | Select-String "Error"
        -> Expected: 0 CS errors

SCAN-4: dotnet build src/PropTraderTools/ 2>&1 | Select-String " Error(s)"
        -> Expected: 0 Error(s)

SCAN-5: dotnet test src/PropTraderTools/ --no-build 2>&1 | Select-String "Total|Passed|Failed|Skipped"
        -> After Ticket 1: Skipped >= 325, Failed <= 157, Passed >= 19
        -> After Ticket 2: Skipped >= 338, Failed <= 144, Passed >= 19

SCAN-6: powershell -File .\deploy-sync.ps1
        -> Expected: output contains "SYNC COMPLETE"

SCAN-7: $f = Get-Item "src\PropTraderTools\CopyEngineTests.cs"; $f.LinkType + " count=" + $f.HardLinkCount
        -> Expected: LinkType = "HardLink" AND HardLinkCount >= 2 after sync
        -> Rationale: a count of 1 means NOT hard-linked (regular file); a properly synced
           hard link has LinkType="HardLink" and at least 2 directory entries (original + link).
           deploy-sync.ps1 failure would leave count=1; a passing SCAN-7 requires count >= 2.
```

---

## 11. NT8 API Surface

No NT8 API is used by these changes. The [Fact(Skip=...)] attribute is pure xUnit/C# syntax.
No calls to Account.All, CreateOrder, AtmStrategyCreate, AtmStrategyChangeStopTarget, or any
NinjaTrader API are introduced. No NT8 API verification via LSP required.

xUnit compatibility: [Fact(Skip = "...")] is a standard xUnit v2 feature. The project already
uses [Fact(Skip=...)] in 32 existing tests. No new package references required.

---

## 12. Data Flow Summary

Before skip:
  Test runs -> xUnit instantiates test class -> field initializer `_engine = CopyEngine.Instance`
  executes -> CopyEngine..cctor() -> AgileDotNetRT.Initialize() -> ArgumentNullException
  -> TypeInitializationException -> test FAILS

After skip:
  Test discovered -> xUnit reads [Fact(Skip="...")] attribute via reflection on test class metadata
  (does NOT instantiate the class) -> test recorded as SKIPPED -> no CopyEngine access
  -> no TypeInitializationException

---

## 13. Hard-Link Sync

After all changes to `src/PropTraderTools/CopyEngineTests.cs`, engineer MUST run:
```
powershell -File .\deploy-sync.ps1
```
This synchronizes the NinjaTrader hard link. SCAN-6 and SCAN-7 verify completion.

---

## 14. Component Summary

| Component              | Type            | Action                            |
|------------------------|-----------------|-----------------------------------|
| CopyEngineTests.cs     | xUnit test file | Add Skip to 306 [Fact] annotations |
| deploy-sync.ps1        | Build script    | Run after edit (hardlink sync)    |

Classes modified: 10 (per ticket split above)
Production files modified: 0
New files: 0

---

PLAN_COMPLETE
