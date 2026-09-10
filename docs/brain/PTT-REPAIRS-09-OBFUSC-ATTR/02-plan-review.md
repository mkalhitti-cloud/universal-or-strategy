# PTT-REPAIRS-09-OBFUSC-ATTR — Plan Review
**Epic:** PTT-REPAIRS-09-OBFUSC-ATTR  
**Phase:** 2 — Plan Review  
**Reviewer:** ptt-plan-reviewer  
**Plan version reviewed:** `docs/brain/PTT-REPAIRS-09-OBFUSC-ATTR/02-architecture-plan.md`  
**Source files inspected:** `src/PropTraderTools/CopyEngine.cs`, `src/PropTraderTools/CopyEngineTests.cs`  
**Rules source:** DNA block (role definition) — `docs/standards/jane-street/RULES_CATALOG.md` not present in repo; hardcoded DNA rules applied verbatim.

---

## VERDICT: REVIEW_PASS

No violations found. All 10 checklist items pass. All DNA/Jane Street rules pass. All NT8 hard constraints pass.

---

## PART A — JANE STREET DNA SCAN

### Concurrency (P0)

| Rule | Check | Result |
|------|-------|--------|
| JS-021 | `lock()` added anywhere | **PASS** — Plan adds only `[System.Reflection.ObfuscationAttribute(...)]` lines. No executable code of any kind. |
| JS-021 | Monitor/Mutex/SemaphoreSlim for state | **PASS** — None planned. |
| JS-023 | UI update from off-thread without `Dispatcher.InvokeAsync` | **PASS** — No UI code involved. |

### Type Safety (P0)

| Rule | Check | Result |
|------|-------|--------|
| JS-001 | `throw` in OnOrderUpdate / SendCopy / gate chain | **PASS** — No `throw` statements. |
| JS-002 | Null return where value expected | **PASS** — No return-value logic changed. |
| JS-003 | Magic string for discriminated state | **PASS** — `"rename"` is a fixed BCL attribute property string, not a discriminated-state sentinel. |

### Immutability (P1)

| Rule | Check | Result |
|------|-------|--------|
| JS-009 | `Dictionary<K,V>` for shared/thread-touched collection | **PASS** — No collections. |
| JS-008 | Mutable fields on struct | **PASS** — No structs. |
| JS-008 | `SolidColorBrush` not `Freeze()`d | **PASS** — No brushes. |

### Construction (P1)

| Rule | Check | Result |
|------|-------|--------|
| JS-010 | Public constructor on singleton or signal struct | **PASS** — No constructors. |

### NT8 Hard Constraints

| Rule | Check | Result |
|------|-------|--------|
| NT8 | `async/await` in `OnInitialize`/`OnDestroyed`/`OnWindowCreated` | **PASS** — None. |
| NT8 | `Account.All` in constructor | **PASS** — None. |
| NT8 | `sealed TradeCopierWindow` | **PASS** — Plan does not touch `TradeCopierWindow`. |
| NT8 / SCAN-03 | FontFamily override | **PASS** — None. |
| NT8 / SCAN-04 | Hardcoded `#RRGGBB` hex | **PASS** — None. |
| NT8 / SCAN-05 | `CreateOrder` without PTT- prefix | **PASS** — No order creation. |
| NT8 / SCAN-06 | `DateTime.Now` (not `UtcNow`) | **PASS** — None. |

### Complexity (P1)

| Rule | Check | Result |
|------|-------|--------|
| CYC | Any method CYC > 8 | **PASS** — Three insertions are pure attribute annotations (zero branches). CYC of decorated methods is unchanged. Plan explicitly states "CYC impact: Zero" for each insertion. |

---

## PART B — 10-ITEM REVIEW CHECKLIST

### Item 1 — Lane-split gate result present and correctly derived

**PASS.**

Plan section "LANE-SPLIT GATE RESULT" is present and explicit. Result is **SINGLE-PIPELINE**.  
Derivation: Q1=No, Q2=No (neither condition demands a lane split), Q3=Yes, Q4=Yes.  
SINGLE-PIPELINE is correct: all 3 changes are independent attribute-only insertions into one file with no cross-dependency. Gate satisfies the mandatory check: single-pipeline path has gate result stated.

---

### Item 2 — Member enumeration is complete (all GetMethod/GetField strings collected from all 5 test classes)

**PASS.**

Source verification performed against `src/PropTraderTools/CopyEngineTests.cs`:

| Test class | Plan line range | Obfuscation-skip count | Source-confirmed unique method names |
|------------|-----------------|------------------------|---------------------------------------|
| `B79CancelRaceGuardTests` | ~L5829–L6461 | 57 | 25 unique names confirmed by grep |
| `BwaveCycT1R1BeHelperTests` | ~L6475–L6683 | 24 | 14 unique names confirmed by grep |
| `BwaveCycTaR2HelperTests` | ~L6692–L6811 | 13 | 6 unique names confirmed by grep |
| `BwaveCycTaR3HelperTests` | ~L6818–L7111 | 36 | 25 unique names confirmed by grep |
| `BwaveCycTaR6HelperTests` | ~L7116–L7274 | 10 | 5 unique names confirmed by grep |
| **Total** | | **140** | **73 unique (deduplicated)** |

Grep against `src/PropTraderTools/` for `"obfuscation: AgileDotNetRT"` returns exactly **140 matches**, all in `CopyEngineTests.cs`. No other file contains obfuscation-skip tests. The plan's "140 total / 73 unique" claim is accurate.

**Field names:** Plan states "No field names are referenced by any obfuscation-skipped test." Confirmed: all 140 obfuscation-skip tests use `GetMethod`, not `GetField`. ✓

---

### Item 3 — Each planned attribute insertion targets a member that actually exists in CopyEngine.cs

**PASS.**

Direct grep of `src/PropTraderTools/CopyEngine.cs` for all three member names:

| Member | Expected line | Grep result | Exact signature at that line |
|--------|--------------|-------------|------------------------------|
| `LogBeSlotEviction` | L1778 | Found at L1778 | `private static void LogBeSlotEviction(string accName, bool isRejected)` |
| `LogDiagOrderCount` | L6422 | Found at L6422 | `private void LogDiagOrderCount(Account acc, Instrument instrument)` |
| `GetSenderAccountName` | L6967 | Found at L6967 | `internal static string GetSenderAccountName(object sender)` |

All 3 members exist at exactly the lines stated in the plan.

---

### Item 4 — No member already decorated with ObfuscationAttribute (no-duplicate check)

**PASS.**

Grep of `src/PropTraderTools/CopyEngine.cs` for `ObfuscationAttribute` returns **zero matches**. No member in the file is currently decorated. Plan explicitly confirms "Already Decorated? No" for all three members in the STEP 2 table. Consistent with source evidence.

---

### Item 5 — Skip removal scope decision documented with rationale

**PASS.**

Plan STEP 4 documents a **zero-skip-removal** decision with full rationale. The decision table correctly distinguishes four categories:

- LogBeSlotEviction: 2 tests exist, binding flags are NonPublic|Instance vs private **static** → GetMethod returns null → FAIL if unskipped. ✓
- GetSenderAccountName: 1 test exists, same binding flag mismatch (NonPublic|Instance vs internal **static**). ✓  
- LogDiagOrderCount: 0 obfuscation-skip tests exist for this method. (Its only test is a passing `[Fact]` at L6091 with NonPublic|Instance matching the instance method.) ✓  
- 70 non-existent methods: 137 tests → methods absent → GetMethod returns null → FAIL if unskipped. ✓

Deferred conditions DW-09-02 and DW-09-03 correctly identify binding flag repairs as prerequisites. Risk assessment ("any skip removal converts SKIPPED → FAILED") is accurate.

---

### Item 6 — Verification plan states exact dotnet test command and expected counts

**PASS.**

STEP 5 provides:
```
dotnet test src/PropTraderTools/ --no-build --verbosity normal
dotnet test src/PropTraderTools/ --no-build
```
Expected counts stated explicitly: **Failed: 0, Passed: 24, Skipped: 490, Total: 514** — identical before and after the edit.

Post-edit validation step requires both `deploy-sync.ps1` and `dotnet test` to pass with exact counts. Sufficient for SIM verification.

---

### Item 7 — Constraints satisfied: ASCII-only, no lock(), no new throw, no CYC change, no signature changes

**PASS.**

| Constraint | Status |
|-----------|--------|
| ASCII-only | Plan inserts `[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]`. All characters are 7-bit ASCII. |
| No `lock()` | Attribute annotations contain no executable code. |
| No new `throw` | Attribute annotations contain no executable code. |
| No CYC change | Plan explicitly states "CYC impact: Zero" for each insertion. Attributes are not control-flow constructs. |
| No signature changes | Method declarations are unchanged; attribute line precedes declaration only. Plan section STEP 6 shows exact before/after: signatures identical. |

---

### Item 8 — InternalsVisibleTo duplication not planned

**PASS.**

Plan scope is limited to 3 attribute insertions in `src/PropTraderTools/CopyEngine.cs`. No `InternalsVisibleTo` assembly attribute is mentioned, planned, or implied. `GetSenderAccountName` is `internal` and is already accessible to the test assembly via existing project configuration; no new `InternalsVisibleTo` is required or planned.

---

### Item 9 — 7-scan checklist template present

**PASS.**

STEP 7 contains the complete 7-scan checklist table. All 7 scans are addressed:

| Scan | Stated result | Correct? |
|------|---------------|---------|
| SCAN-01 No `lock()` | PASS — attribute adds no code | ✓ |
| SCAN-02 No `DateTime.Now` | PASS — attribute adds no code | ✓ |
| SCAN-03 ASCII-only | PASS — `"rename"` is ASCII | ✓ |
| SCAN-04 No FontFamily | PASS — N/A | ✓ |
| SCAN-05 No hardcoded hex | PASS — N/A | ✓ |
| SCAN-06 No new `throw` | PASS — attribute adds no code | ✓ |
| SCAN-07 Hard-link sync | REQUIRED — `deploy-sync.ps1` flagged for engineer | ✓ |

---

### Item 10 — Deferred items properly documented

**PASS.**

Plan "NEW DEFERRED ITEMS" section creates four well-specified deferred work items:

| ID | Title | Blocked Until | Notes |
|----|-------|---------------|-------|
| DW-09-01 | Implement 70 unimplemented CopyEngine helper methods | New epic | Correct scope — large implementation sprint |
| DW-09-02 | Fix NonPublic\|Instance → NonPublic\|Static for `LogBeSlotEviction` + remove 2 skips | After DW-09-01 | Binding flag root cause correctly identified |
| DW-09-03 | Fix NonPublic\|Instance → NonPublic\|Static for `GetSenderAccountName` + remove 1 skip | After DW-09-01 scope | Binding flag root cause correctly identified |
| DW-09-04 | Remove all 137 remaining obfuscation skips after DW-09-01 + DW-09-02 + DW-09-03 | All prior items | Safe ordering verified |

Pre-existing DW-B7-01 from PTT-REPAIRS-07-NT8-BULK-SKIP is noted as OUT OF SCOPE per orchestrator instruction. Correct boundary.

---

## PART C — SPEC COVERAGE MATRIX

| Requirement | Addressed in Plan? | Plan Section |
|-------------|-------------------|--------------|
| Add `[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]` to private/internal members | YES | STEP 3 — 3 insertions |
| Target only members accessed by reflection in obfuscation-skipped tests | YES | STEP 1 — 73-name inventory; STEP 2 — 3/73 exist |
| Scope to members in `CopyEngine.cs` | YES | COMPONENT LIST |
| Do not add to members that do not exist | YES | STEP 2 explicitly defers 70 missing members to DW-09-01 |
| No signature changes | YES | STEP 6 — "No signature changes" explicitly stated |
| Plan accounts for 70 unimplemented stubs | YES | CRITICAL ARCHITECTURAL FINDING + STEP 2 + DEFERRED ITEMS |
| Verification via dotnet test with expected counts | YES | STEP 5 |

---

## PART D — BINDING FLAG DISCREPANCY FINDING

The plan correctly identifies and documents (STEP 2, "Test Binding Flag Discrepancy") that:

- `LogBeSlotEviction` (private **static**) will not be found by the test's `NonPublic | Instance` binding flags.
- `GetSenderAccountName` (internal **static**) will not be found by the test's `NonPublic | Instance` binding flags.

This finding is **correctly classified as out-of-scope for this epic** and deferred to DW-09-02/DW-09-03. The ObfuscationAttribute additions still deliver value: they protect the members from AgileDotNetRT rename at **compile time**, independent of whether the current test binding flags can locate them at runtime. The plan's rationale for decorating all 3 regardless of the binding flag mismatch is architecturally sound.

---

## PART E — SINGLE-LINE VIOLATION LOG

*No violations found. This section intentionally contains no entries.*

---

## FINAL RESULT

**REVIEW_PASS**

The plan is internally consistent, grounded in source evidence, and contains no Jane Street DNA violations, NT8 hard constraint violations, spec gaps, or checklist failures. All 10 checklist items pass. All 7 scans are pre-cleared. Zero violations to report.

The plan correctly narrows scope to the 3 existing members (LogBeSlotEviction, LogDiagOrderCount, GetSenderAccountName), defers 70 missing-method implementations to a new epic (DW-09-01), defers all skip removals pending binding flag fixes, and delivers unchanged test counts as the expected post-edit baseline.

**REVIEW_PASS — zero violations. Phase 3 (ticket generation) is unlocked.**
