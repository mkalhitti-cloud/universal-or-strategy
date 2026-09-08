# Ticket Review: WAVE2-LANE-A (Revision Pass)

**Reviewer**: ptt-ticket-reviewer (Phase 3.5)
**Tickets file**: `docs/brain/WAVE2-LANE-A/04-tickets.md` (Revision — V1-V8 fixes applied)
**Plan file**: `docs/brain/WAVE2-LANE-A/02-architecture-plan.md` (REVIEW_PASS)
**Rules file**: `docs/standards/jane-street/RULES_CATALOG.md`
**Prior review verdict**: TICKET_REVIEW_FAIL (8 violations)

---

## V1–V8 Resolution Status

| Violation | Fix Required | Status |
|---|---|---|
| V1 (T1 file name) | Test class in `Wave2LaneATests.cs` | **RESOLVED** |
| V2 (phantom test) | `IsExitSignalName_AtmTarget_ReturnsTrue` kept | **RESOLVED** |
| V3 (missing plan test) | `IsNativeCloseOrFlattenSignal_CloseLowercase_ReturnsFalse` restored | **RESOLVED** |
| V4 (private helper) | `IsNativeCloseOrFlattenSignal` changed to `internal static` | **RESOLVED** |
| V5 (SCAN-06) | 3-step gate: build + sync + F5 | **RESOLVED** |
| V6 (T2 file name) | Test class in `Wave2LaneATests.cs` | **RESOLVED** |
| V7 (private helper) | `IsArmingOrderState` changed to `internal static` | **RESOLVED** |
| V8 (SCAN-06) | 3-step gate: build + sync + F5 | **RESOLVED** |

---

## T1 — IsExitSignalName CCN 9 → 8

### Traceability

| Plan Item | Ticket Coverage | Status |
|---|---|---|
| Plan §9 — `IsExitSignalName` CYC 9→8 | Ticket 1 §Required Changes + §Post-Extraction CCN Math | PASS |
| Plan §5 — new helper `IsNativeCloseOrFlattenSignal` (CCN=3) | Ticket 1 §Method Signatures + §Required Changes Change 2 | PASS |
| Plan §9.3 — line range 2347-2370 | Ticket 1 §Current Source lines 2347-2370 | PASS |
| Plan §9.5 — before/after implementation contract | Ticket 1 §Required Changes (Change 1 REMOVE/REPLACE blocks) | PASS |
| Plan §3 — JS-080, JS-021, JS-001, JS-002, ASCII | Ticket 1 §Spec Requirement IDs Satisfied | PASS |
| Plan §9.6 — test class `Wave2LaneAIsExitSignalNameTests` in `Wave2LaneATests.cs` | Ticket 1 §xUnit Test Class | PASS |

**WARN — Architecture plan divergence (§8/§9.5 `private static` → ticket `internal static`):**
Plan §8 declares `private static bool IsNativeCloseOrFlattenSignal(string name)` and §9.5 constraints
state "Helper is `private static bool` -- not `internal`". Ticket overrides this to `internal static`
with documented justification (V4 fix: xUnit requires `internal` for `InternalsVisibleTo` access;
`private` is inaccessible to the test assembly even with that attribute). The deviation is explicitly
documented in the Revision Notes table (V4). Engineering contract in the ticket is self-consistent
on `internal`. **WARN only — does not affect engineer correctness.**

Traceability: **PASS**

### JS Pre-Check

| Rule | Check | Result |
|---|---|---|
| JS-021 | No `lock()` in described code | PASS |
| JS-001 | No `throw new XxxException` in described code | PASS |
| JS-002 | No `return null` — helper returns `bool` | PASS |
| JS-080 | No C# 9+ features — only `if`/`return` chains and expression-body (`=>`, C# 6) | PASS |
| ASCII-only | String literals `"Close"`, `"Flatten"` are ASCII | PASS |

JS Pre-Check: **PASS**

### CYC Pre-Check

| Method | Expected CCN | Ticket CCN Math | Status |
|---|---|---|---|
| `IsExitSignalName` (post-extraction) | 8 | base(1)+null(1)+empty(1)+PTT-(1)+IsNativeClose(1)+Rev(1)+Exit(1)+IsAtmTarget(1) = 8 | PASS |
| `IsNativeCloseOrFlattenSignal` (new) | 3 | base(1)+Close(1)+Flatten(1) = 3 | PASS |

All methods ≤ 8. CYC Pre-Check: **PASS**

### NT8 Constraints

| Constraint | Status |
|---|---|
| No `AtmStrategyCreate` / `AtmStrategyChangeStopTarget` referenced | PASS |
| New helper is pure static — no NT8 API calls | PASS |
| `IsNativeCloseOrFlattenSignal` uses only string equality (`==`) | PASS |
| No `async/await` in lifecycle method | PASS |
| No `Dispatcher.InvokeAsync` needed (no UI access) | PASS |
| No `sealed` on `TradeCopierWindow` (not involved) | PASS |
| No `DateTime.Now` | PASS |
| No hardcoded hex color | PASS |

NT8 Check: **PASS**

### Test Coverage

Test class: `Wave2LaneAIsExitSignalNameTests` in `tests/PropTraderTools.Tests/Wave2LaneATests.cs` ✓

| # | Test name | Method under test | Boundary |
|---|---|---|---|
| 1 | `IsExitSignalName_NullInput_ReturnsFalse` | `IsExitSignalName` | null → false |
| 2 | `IsExitSignalName_EmptyString_ReturnsTrue` | `IsExitSignalName` | "" → true (DW-LB-FL-01) |
| 3 | `IsExitSignalName_PttPrefixed_ReturnsTrue` | `IsExitSignalName` | "PTT-Stop1" → true |
| 4 | `IsExitSignalName_CloseSignal_ReturnsTrue` | `IsExitSignalName` | "Close" → true |
| 5 | `IsExitSignalName_FlattenSignal_ReturnsTrue` | `IsExitSignalName` | "Flatten" → true |
| 6 | `IsExitSignalName_RevPrefix_ReturnsTrue` | `IsExitSignalName` | "RevEntry" → true |
| 7 | `IsExitSignalName_ExitPrefix_ReturnsTrue` | `IsExitSignalName` | "ExitLong" → true |
| 8 | `IsExitSignalName_AtmTarget_ReturnsTrue` | `IsExitSignalName` | "Target1" → true (B78 path) |
| 9 | `IsExitSignalName_EntrySignal_ReturnsFalse` | `IsExitSignalName` | "Entry" → false |
| 10 | `IsNativeCloseOrFlattenSignal_Close_ReturnsTrue` | `IsNativeCloseOrFlattenSignal` | "Close" → true |
| 11 | `IsNativeCloseOrFlattenSignal_Flatten_ReturnsTrue` | `IsNativeCloseOrFlattenSignal` | "Flatten" → true |
| 12 | `IsNativeCloseOrFlattenSignal_CloseLowercase_ReturnsFalse` | `IsNativeCloseOrFlattenSignal` | "close" → false (case-sensitive) |

Total: **12 [Fact] tests** — meets ≥ 12 minimum threshold ✓

All assertions use `Assert.True` / `Assert.False`. No `[Theory]`. No NUnit or MSTest.

**WARN — Summary table says `xUnit tests: 13` for Ticket 1** but the test table lists 12 entries and
`SCAN-07` states `"all 12 new Ticket 1 tests included"`. The two internal references are inconsistent.
The test table (12) and SCAN-07 (12) are consistent with each other; the Summary table value (13) is
the error. The minimum threshold of ≥ 12 is met. The engineer should implement the 12 tests as listed
in the test table — the Summary value of 13 is a typo. **WARN only — does not affect engineering.**

Test Coverage: **PASS**

### Scan Checklist

| Scan | Command present | Expected result specified |
|---|---|---|
| SCAN-01 | `lizard … --csv \| ConvertFrom-Csv … \| Where-Object {[int]$_.CCN -gt 8}` | EMPTY (no methods > CCN 8) |
| SCAN-02 | `grep -rn "lock(" src/PropTraderTools/CopyEngine.cs` | 0 matches in new/modified lines |
| SCAN-03 | `grep -n "throw new" src/PropTraderTools/CopyEngine.cs` | 0 new throw new in lines 2347-2380 |
| SCAN-04 | `grep -n "return null" src/PropTraderTools/CopyEngine.cs` | 0 new return null in lines 2347-2380 |
| SCAN-05 | `grep -Pn "[^\x00-\x7F]" src/PropTraderTools/CopyEngine.cs` | 0 non-ASCII in new/modified lines |
| SCAN-06 | Step 1: `dotnet build` + Step 2: `ptt-sync-and-verify.ps1` + Step 3: F5 in NT8 | 0 errors / 0 MISMATCH / NT8 green |
| SCAN-07 | `dotnet test tests/PropTraderTools.Tests/PropTraderTools.Tests.csproj` | ≥ 248 passing, 0 failing |

All 7 scans present. SCAN-06 includes the full 3-step NT8 gate (V5 fix confirmed). ✓

Scan Checklist: **PASS**

### File Routing

| File | Path | Workspace |
|---|---|---|
| Source | `src/PropTraderTools/CopyEngine.cs` | Wave (`c:\WSGTA\universal-or-strategy`) ✓ |
| Tests | `tests/PropTraderTools.Tests/Wave2LaneATests.cs` | Wave (`c:\WSGTA\universal-or-strategy`) ✓ |

File Routing: **PASS**

### T1 VERDICT: TICKET_REVIEW_PASS

---

## T2 — HasArmingAtmBrackets CCN 9 → 5

### Traceability

| Plan Item | Ticket Coverage | Status |
|---|---|---|
| Plan §10 — `HasArmingAtmBrackets` CYC 9→5 | Ticket 2 §Required Changes + §Post-Extraction CCN Math | PASS |
| Plan §5 — new helper `IsArmingOrderState` (CCN=6) | Ticket 2 §Method Signatures + §Required Changes Change 2 | PASS |
| Plan §10.3 — line range 5351-5369 | Ticket 2 §Current Source lines 5351-5369 | PASS |
| Plan §10.5 — before/after implementation contract | Ticket 2 §Required Changes (Change 1 REMOVE/REPLACE blocks) | PASS |
| Plan §3 — JS-080, JS-021, JS-001, JS-002, ASCII | Ticket 2 §Spec Requirement IDs Satisfied | PASS |
| Plan §10.6 — test class `Wave2LaneAHasArmingAtmBracketsTests` in `Wave2LaneATests.cs` | Ticket 2 §xUnit Test Class | PASS |
| Plan §12 — sequential execution, Ticket 1 prerequisite | Ticket 2 §SCOPE LOCK prerequisite statement | PASS |

**WARN — Architecture plan divergence (§8/§10.5 `private static` → ticket `internal static`):**
Plan §8 declares `private static bool IsArmingOrderState(OrderState s)` and §10.5 constraints
state "Helper is `private static bool` -- not `internal`". Ticket overrides to `internal static`
with documented justification (V7 fix). Same rationale as T1/V4. Documented in Revision Notes.
**WARN only — does not affect engineer correctness.**

Traceability: **PASS**

### JS Pre-Check

| Rule | Check | Result |
|---|---|---|
| JS-021 | No `lock()` in described code | PASS |
| JS-001 | No `throw new XxxException` in described code | PASS |
| JS-002 | No `return null` — helper returns `bool` | PASS |
| JS-080 | No C# 9+ features — only `if`/`return` chains | PASS |
| ASCII-only | No string literals in `IsArmingOrderState` | PASS |

JS Pre-Check: **PASS**

### CYC Pre-Check

| Method | Expected CCN | Ticket CCN Math | Status |
|---|---|---|---|
| `HasArmingAtmBrackets` (post-extraction) | 5 | base(1)+foreach(1)+instr-skip(1)+IsArmingOrderState(1)+IsAtmBracketName(1) = 5 | PASS |
| `IsArmingOrderState` (new) | 6 | base(1)+Initialized(1)+Working(1)+Submitted(1)+Accepted(1)+TriggerPending(1) = 6 | PASS |

All methods ≤ 8. CYC Pre-Check: **PASS**

### NT8 Constraints

| Constraint | Status |
|---|---|
| No `AtmStrategyCreate` / `AtmStrategyChangeStopTarget` referenced | PASS |
| New helper is pure static — no NT8 API calls | PASS |
| `IsArmingOrderState` uses only `OrderState` enum comparison | PASS |
| `HasArmingAtmBrackets` retains `acc.Orders.ToList()` snapshot (existing pattern, not removed) | PASS |
| No `async/await` in lifecycle method | PASS |
| No `Dispatcher.InvokeAsync` needed (no UI access) | PASS |
| No `DateTime.Now` | PASS |
| No hardcoded hex color | PASS |

NT8 Check: **PASS**

### Test Coverage

Test class: `Wave2LaneAHasArmingAtmBracketsTests` in `tests/PropTraderTools.Tests/Wave2LaneATests.cs` ✓

| # | Test name | Method under test | Boundary |
|---|---|---|---|
| 1 | `IsArmingOrderState_Initialized_ReturnsTrue` | `IsArmingOrderState` | Initialized → true |
| 2 | `IsArmingOrderState_Working_ReturnsTrue` | `IsArmingOrderState` | Working → true |
| 3 | `IsArmingOrderState_Submitted_ReturnsTrue` | `IsArmingOrderState` | Submitted → true |
| 4 | `IsArmingOrderState_Accepted_ReturnsTrue` | `IsArmingOrderState` | Accepted → true |
| 5 | `IsArmingOrderState_TriggerPending_ReturnsTrue` | `IsArmingOrderState` | TriggerPending → true |
| 6 | `IsArmingOrderState_Filled_ReturnsFalse` | `IsArmingOrderState` | Filled → false |
| 7 | `IsArmingOrderState_Cancelled_ReturnsFalse` | `IsArmingOrderState` | Cancelled → false |
| 8 | `IsArmingOrderState_Rejected_ReturnsFalse` | `IsArmingOrderState` | Rejected → false |

Total: **8 [Fact] tests** — meets ≥ 8 minimum threshold ✓

All 5 active states tested (true path). Three inactive states tested (false path). Full boundary coverage.
All assertions use `Assert.True` / `Assert.False`. No `[Theory]`. No NUnit or MSTest.

Test Coverage: **PASS**

### Scan Checklist

| Scan | Command present | Expected result specified |
|---|---|---|
| SCAN-01 | `lizard … --csv \| ConvertFrom-Csv … \| Where-Object {[int]$_.CCN -gt 8}` | EMPTY (no methods > CCN 8) |
| SCAN-02 | `grep -rn "lock(" src/PropTraderTools/CopyEngine.cs` | 0 matches in new/modified lines |
| SCAN-03 | `grep -n "throw new" src/PropTraderTools/CopyEngine.cs` | 0 new throw new in lines 5351-5385 |
| SCAN-04 | `grep -n "return null" src/PropTraderTools/CopyEngine.cs` | 0 new return null in lines 5351-5385 |
| SCAN-05 | `grep -Pn "[^\x00-\x7F]" src/PropTraderTools/CopyEngine.cs` | 0 non-ASCII in new/modified lines |
| SCAN-06 | Step 1: `dotnet build` + Step 2: `ptt-sync-and-verify.ps1` + Step 3: F5 in NT8 | 0 errors / 0 MISMATCH / NT8 green |
| SCAN-07 | `dotnet test tests/PropTraderTools.Tests/PropTraderTools.Tests.csproj` | ≥ 256 passing, 0 failing |

All 7 scans present. SCAN-06 includes the full 3-step NT8 gate (V8 fix confirmed). ✓

Scan Checklist: **PASS**

### File Routing

| File | Path | Workspace |
|---|---|---|
| Source | `src/PropTraderTools/CopyEngine.cs` | Wave (`c:\WSGTA\universal-or-strategy`) ✓ |
| Tests | `tests/PropTraderTools.Tests/Wave2LaneATests.cs` | Wave (`c:\WSGTA\universal-or-strategy`) ✓ |

File Routing: **PASS**

### T2 VERDICT: TICKET_REVIEW_PASS

---

## Non-Blocking Warnings (WARNs — for architect awareness, not engineer blockers)

| WARN | Location | Detail |
|---|---|---|
| WARN-1 | T1 Summary table | Summary says `xUnit tests: 13`; test table has 12 entries; SCAN-07 says 12. Summary value is a typo. Engineer implements 12 tests per the table. |
| WARN-2 | T1 §Method Signatures vs plan §8 | Plan says `private static`; tickets say `internal static`. Documented intentional override (V4 fix). Plan §8, §9.5 remain stale — architect may update plan for record-keeping, not required. |
| WARN-3 | T2 §Method Signatures vs plan §8 | Same as WARN-2 for `IsArmingOrderState`. Plan §8, §10.5 remain stale (V7 fix). |

None of these WARNs block engineering. The engineer reads the ticket. The ticket is internally consistent on `internal static` and 12 tests.

---

## Overall: TICKET_REVIEW_PASS

All 8 prior violations are resolved. All mandatory checks pass across both tickets.

| Check | T1 | T2 |
|---|---|---|
| Traceability | PASS | PASS |
| JS Pre-Check (JS-021/001/002/080) | PASS | PASS |
| CYC Pre-Check (all ≤ 8) | PASS | PASS |
| NT8 Constraints | PASS | PASS |
| Test Coverage (≥ 12 / ≥ 8 [Fact] tests) | PASS | PASS |
| Scan Checklist (SCAN-01 through SCAN-07) | PASS | PASS |
| File Routing | PASS | PASS |
| **Ticket Verdict** | **PASS** | **PASS** |

**Resolved violations**: V1 ✓ V2 ✓ V3 ✓ V4 ✓ V5 ✓ V6 ✓ V7 ✓ V8 ✓

---

## TICKET_REVIEW_PASS

The engineer may proceed to Phase 4a. Ticket 1 must be fully complete (Phase 4a PASS + Phase 4b PASS) before Ticket 2 begins.
