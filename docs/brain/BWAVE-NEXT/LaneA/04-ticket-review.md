# BWAVE-NEXT Lane A — Ticket Review

**Reviewer**: ptt-ticket-reviewer
**Date**: 2026-09-04
**Source tickets**: `docs/brain/BWAVE-NEXT/LaneA/04-tickets.md`
**Source plan**: `docs/brain/BWAVE-NEXT/LaneA/02-architecture-plan.md` (REVIEW_PASS cycle 2)
**Spec**: `docs/brain/BWAVE-NEXT/LaneA-mission-brief.md`
**Backlog refs**: `DW-NEW-08-naked-fill-race.md`, `DW-NEW-09-stale-orders-scan.md`

---

## Per-Ticket Findings

### T1 — DW-C38-04: Verify Module Teardown Ordering

#### Traceability
- **Spec Req ID**: DW-C38-04 — stated. ✓
- **Acceptance criteria match**: Spec requires (a) `_modules.Teardown()` before `_allAccounts.Clear()` confirmed, (b) all IPttModule implementations verified for missing unsubscribes, (c) fix any gap in the module's own Teardown(), (d) no new lock(), (e) 1 `[Fact]`. Ticket covers all five. ✓
- **No phantom work**: No items in ticket absent from plan/spec. ✓
- **No missing work**: Plan §3.T1 is fully represented. ✓
- **Out-of-scope items absent**: DW-C38-01 (already resolved) not touched. ✓

#### JS Pre-Check
- **7-scan present**: SCAN-01 through SCAN-07, all with commands and expected zero results. ✓
- **lock()**: Not described anywhere in ticket. ✓
- **async void**: Not described. ✓
- **return null**: Not described. Test method returns void. ✓
- **throw new XxxException**: Not described. ✓
- **CYC**: No production method modified. Test method CYC=1. ✓

#### NT8 Constraints
- `Account.Change()`: Not referenced. ✓
- `AtmStrategyCreate()`: Not referenced. ✓
- `AtmStrategyChangeStopTarget()`: Not referenced. ✓
- T4 hook location: N/A for T1. ✓
- **NT8 sync**: Correctly marked NOT REQUIRED (test-only ticket). ✓

#### Test Coverage
- **Required test**: `[Fact] Detach_ClearsAllModulesBeforeAccountList()` — present with full arrange/act/assert description. ✓
- **xUnit only**: `[Fact]` confirmed; no `[Test]`, no NUnit, no MSTest. ✓
- **Test data consistent with spec**: Spy module + reflection on `_modules` and `_allAccounts` — consistent with plan §3.T1. ✓

#### Completeness
- File(s): ✓ | Type: ✓ | Spec Req IDs: ✓ | Dependencies: ✓ | Change Description: ✓
- Method Signatures: ✓ (test method only, no production changes) | CYC Analysis: ✓
- Acceptance Criteria: ✓ | Test Coverage: ✓ | 7-Scan Checklist: ✓ | NT8 Sync: ✓
- Execution order: Covered in global section. ✓

#### File Routing
- `src/PropTraderTools/Tests/BwaveDwLaneATests.cs` — correct Wave workspace path. ✓

**VERDICT: TICKET_REVIEW_PASS**

---

### T2 — DW-LaneA-06: Collapse BuildArrowCluster Inline

#### Traceability
- **Spec Req ID**: DW-LaneA-06 — stated. ✓
- **Acceptance criteria match**: Spec requires (a) `BuildArrowCluster` deleted, (b) inlined into `BuildBufferedButtonsRow`, (c) teal buttons retain teal border+foreground, (d) `btn.Background` set AFTER `SetResourceReference`, (e) `dotnet build` 0 errors, (f) lizard ≤8, (g) no lock/async void/return null, (h) 2 `[Fact]`. Ticket covers all. ✓
- **No phantom work**: Inlining + delete + 2 tests = exactly what spec mandates. ✓
- **No missing work**: Plan §3.T2 fully represented. ✓

#### JS Pre-Check
- **7-scan present**: SCAN-01 through SCAN-07, all with correct commands and expected zero results. ✓
- **lock()**: Not described. ✓
- **async void**: Not described. ✓
- **return null**: Not described. ✓
- **throw new XxxException**: Not described. ✓
- **CYC**: `BuildBufferedButtonsRow` before=2, after=3 (well within 8). `BuildArrowCluster` deleted. ✓

#### NT8 Constraints
- `Account.Change()`: Not referenced. ✓
- `AtmStrategyCreate()`: Not referenced. ✓
- `AtmStrategyChangeStopTarget()`: Not referenced. ✓
- **NT8 sync**: REQUIRED. Instruction present with exact command `ptt-sync-and-verify.ps1`, expected `18/18 OK, 0 MISMATCH`, F5 gate. ✓

#### Test Coverage
- **Required tests**:
  - `[Fact] BuildBufferedButtonsRow_TealButtons_HaveTealBorderBrush()` — present. ✓
  - `[Fact] BuildBufferedButtonsRow_TrimButton_HasInactiveBackground()` — present. ✓
- Both tests have full arrange/act/assert descriptions. ✓
- **xUnit only**: `[Fact]` confirmed. ✓

#### Completeness
- File(s): ✓ | Type: ✓ | Spec Req IDs: ✓ | Dependencies: ✓ | Change Description: ✓
- Method Signatures: `BuildArrowCluster` deleted (return type documented), `BuildBufferedButtonsRow` signature unchanged, CYC delta shown. ✓
- CYC Analysis: Full before/after table with Lizard expected. ✓
- Acceptance Criteria: ✓ | Test Coverage: ✓ | 7-Scan Checklist: ✓ | NT8 Sync: ✓

#### File Routing
- `src/PropTraderTools/TradeCopierPanel.cs` — correct Wave workspace path. ✓

**VERDICT: TICKET_REVIEW_PASS**

---

### T3 — DW-DW-03 + DW-NEW-07: Two-Panel BE Integration Test

#### Traceability
- **Spec Req IDs**: DW-DW-03 and DW-NEW-07 — both stated. ✓
- **Acceptance criteria match**: Spec requires 3 scenarios (S1 sibling isolation, S2 own-account cleanup, S3 last-panel global cleanup), no WpfFact, no lock(), xUnit-only, test file append or new file. All covered. ✓
- **Dependency on T1 VERIFY_PASS**: Explicitly stated. ✓
  - *Note*: Mission brief execution order says "T1 and T2 → T3"; tickets and plan both say "T1 VERIFY_PASS only." This is consistent with plan §5 (REVIEW_PASS authority) — T3 exercises the Detach path T1 verifies, not T2's button region. Minor wording difference is not a FAIL.
- **No phantom work**: 3 tests + optional 1-line seam = exactly what spec mandates. ✓
- **No missing work**: Plan §3.T3 fully represented. ✓

#### JS Pre-Check
- **7-scan present**: SCAN-01 through SCAN-07, all with correct commands and expected zero results. ✓
- **lock()**: Not described. `ConcurrentDictionary.ContainsKey` explicitly noted as lock-free. ✓
- **async void**: Not described. ✓
- **return null**: Not described. Optional seam returns `bool`. ✓
- **throw new XxxException**: Not described. ✓
- **CYC**: Optional seam CYC=1 (expression body). Each test method CYC=1. ✓

#### NT8 Constraints
- `Account.Change()`: Not referenced. ✓
- `AtmStrategyCreate()`: Not referenced. ✓
- `AtmStrategyChangeStopTarget()`: Not referenced. ✓
- **NT8 sync**: Correctly conditional — required if `IsPendingBeSlotActive` seam added to `CopyEngine.cs`; skipped if test-only. ✓

#### Test Coverage
- **Required tests**:
  - `[Fact] Detach_PanelA_DoesNotClearPanelB_BeSlot()` — present with full arrange/act/assert. ✓
  - `[Fact] Detach_LastPanel_ClearsAllPendingBeSlots()` — present. ✓
  - `[Fact] Detach_OwnPanel_ClearsOwnBeSlot_ButNotOthers()` — present. ✓
- Test approach: CopyEngine API-driven. No `WpfFact` required. ✓
- State isolation noted: each test resets `_pendingBeSlots` before seeding. ✓
- **xUnit only**: `[Fact]` confirmed. ✓

#### Completeness
- File(s): ✓ | Type: ✓ | Spec Req IDs: ✓ | Dependencies: ✓ (T1 VERIFY_PASS stated) | Change Description: ✓
- Method Signatures: Optional seam signature with return type, parameter types stated. ✓
- CYC Analysis: ✓ | Acceptance Criteria: ✓ | Test Coverage: ✓ | 7-Scan Checklist: ✓ | NT8 Sync: ✓

#### File Routing
- `src/PropTraderTools/Tests/BwaveDwLaneATests.cs` (append) OR `BwaveNextLaneATests.cs` — both correct Wave workspace paths. ✓

**VERDICT: TICKET_REVIEW_PASS**

---

### T4 — DW-NEW-08 Option E: Accelerated Naked Detection

#### Traceability
- **Spec Req ID**: DW-NEW-08 (Option E) — stated. ✓
- **Acceptance criteria match**: DW-NEW-08 spec requires (a) NakedPositionDetector fires within 50ms on naked follower, (b) no false fires during bracket confirmation lag, (c) multi-follower isolation, (d) no lock/async void, (e) CYC <=8 all new methods. All covered. ✓
- **Hook location**: DW-NEW-08 spec names callback as "OnAccountOrderUpdate"; plan §2.4 confirmed no such method exists — the real callback is `OnOrderUpdate` at line 1355. Ticket correctly uses `OnOrderUpdate`. Plan REVIEW_PASS is authoritative; ticket follows plan. ✓
- **Method naming**: DW-NEW-08 files-affected table used name "GetNonFlatPosition"; ticket uses `FindOpenPositionInstrument`. Plan adopted the alternative name with the same semantics. Not a FAIL — plan-level renaming is within architect authority. ✓
- **No phantom work**: 1 field + 4 methods + 1 tail-call = scope per plan §3.T4. ✓
- **No missing work**: Plan §3.T4 fully represented. ✓
- **Lane B items absent**: Option D (cancel-before-dispatch) not in this ticket. ✓

#### JS Pre-Check
- **7-scan present**: SCAN-01 through SCAN-07, all with correct commands and expected zero results. ✓
- **lock()**: Not described. `ConcurrentDictionary` + `Environment.TickCount64` atomic ops explicitly stated. ✓
- **async void**: Not described. `Dispatcher.InvokeAsync` used correctly (lambda, not async void). ✓
- **return null (JS-002)**: `FindOpenPositionInstrument` returns `Instrument?` (nullable) via `?.Instrument` expression — no raw `return null` statement. JS-002 compliant. ✓
- **throw new XxxException**: Not described in any T4 method. ✓
- **CYC analysis**:
  - `TryNakedDetect` = 3 ✓
  - `NakedPositionDetector` = 5-6 ✓ (within 8)
  - `HasNakedPosition`: Plan states CYC=4 (conceptual); ticket explicitly notes "Lizard will count 7-8 branches" and confirms budget is <=8 regardless. This discrepancy is flagged proactively by the ticket and confirmed within budget. ✓ (See Note below)
  - `FindOpenPositionInstrument` = 1 ✓
  - `OnOrderUpdate` unchanged (unconditional call adds 0 to parent CYC) ✓

  **Note on HasNakedPosition CYC**: The plan §3.T4 states CYC=4 for `HasNakedPosition`. The ticket correctly identifies that Lizard's mechanical count will produce 7-8 (2 foreach loops + 5 branch points). The ticket explicitly flags this, notes the budget is still met (<=8), and instructs the engineer to run `lizard HasNakedPosition --CCN 8` to confirm post-implementation. The budget contract (<=8) is maintained. This is an informational discrepancy in the plan, not a ticket violation.

#### NT8 Constraints
- `Account.Change()`: Explicitly stated as NOT used. ✓
- `AtmStrategyCreate()`: Explicitly stated as NOT used. ✓
- `AtmStrategyChangeStopTarget()`: Explicitly stated as NOT used. ✓
- `Dispatcher.InvokeAsync` for UI marshal: Present and correctly described. ✓
- **NT8 sync**: REQUIRED. Instruction present with exact command, expected `18/18 OK, 0 MISMATCH`, F5 gate. ✓

#### Test Coverage
- **Required tests** (4 [Fact] minimum per plan §3.T4):
  - `[Fact] HasNakedPosition_ReturnsFalse_WhenNoPosition()` — present with full description. ✓
  - `[Fact] HasNakedPosition_ReturnsFalse_WhenStopOrderPresent()` — present. ✓
  - `[Fact] HasNakedPosition_ReturnsTrue_WhenNoProtectiveOrders()` — present. ✓
  - `[Fact] NakedPositionDetector_DoesNotFire_WithinGraceWindow()` — present with debounce test logic. ✓
- `[InternalsVisibleTo]` usage noted for internal method access. ✓
- **xUnit only**: `[Fact]` confirmed. ✓

#### Completeness
- File(s): ✓ | Type: ✓ | Spec Req IDs: ✓ | Dependencies: ✓ | Change Description: ✓
- Method Signatures: All 4 new methods with full return types and parameter types stated. Field type `ConcurrentDictionary<string, long>` stated. ✓
- CYC Analysis: Full table with per-method limits and Lizard expected. ✓
- Acceptance Criteria: ✓ | Test Coverage: ✓ | 7-Scan Checklist: ✓ | NT8 Sync: ✓
- Risk note for grace window calibration documented. ✓

#### File Routing
- `src/PropTraderTools/CopyEngine.cs` — correct Wave workspace path. ✓

**VERDICT: TICKET_REVIEW_PASS**

---

### T5 — DW-NEW-09: ActiveOrders Filter Wrapper

#### Traceability
- **Spec Req ID**: DW-NEW-09 — stated. ✓
- **Acceptance criteria match**: DW-NEW-09 spec requires (a) `ActiveOrders` CYC=1 static private, (b) line 3437 changed, (c) line 3637 changed, (d) all other sites unchanged, (e) `TryLogSFBTrace` unchanged, (f) build 0 errors, (g) 2 `[Fact]` with specific test data (14 Cancelled + 1 Working for T_bracket; 1 Cancelled + 1 Working for T_entry). All covered. ✓
- **Call site count**: 23 unchanged explicitly stated; 2 changed explicitly stated. Verification command provided. ✓
- **TryLogSFBTrace**: Explicitly confirmed unchanged at line ~1947. DW-NEW-09 spec cites line 1947; backlog cites line 1958. Ticket uses ~1947 (tilde = approximate). Minor variance acceptable — engineer verifies exact line during implementation. ✓
- **No phantom work**: 1 new method + 2 call-site changes + 2 tests = scope per spec. ✓
- **No missing work**: Plan §3.T5 fully represented. ✓

#### JS Pre-Check
- **7-scan present**: SCAN-01 through SCAN-07, all with correct commands and expected zero results. ✓
- **lock()**: Not described. `LINQ Where` explicitly noted as non-mutating. ✓
- **async void**: Not described. ✓
- **return null (JS-002)**: `ActiveOrders` returns `IEnumerable<Order>` — never null. ✓
- **throw new XxxException**: Not described. ✓
- **CYC**: `ActiveOrders`=1 (expression body). `FindFollowerBracketOrder`=1 (unchanged). `FindFollowerEntryOrder`=3 (unchanged). All <=8. ✓

#### NT8 Constraints
- `Account.Change()`: Not referenced. ✓
- `AtmStrategyCreate()`: Not referenced. ✓
- `AtmStrategyChangeStopTarget()`: Not referenced. ✓
- **NT8 sync**: REQUIRED. Instruction present with exact command, expected `18/18 OK, 0 MISMATCH`, F5 gate. ✓

#### Test Coverage
- **Required tests**:
  - `[Fact] FindFollowerBracketOrder_SkipsFilledAndCancelledOrders()` — present. Test data: 14 Cancelled + 1 Working StopMarket, assert Working stop returned. Matches spec exactly. ✓
  - `[Fact] FindFollowerEntryOrder_SkipsFilledAndCancelledEntries()` — present. Test data: 1 Cancelled "PTT-Copy" + 1 Working "PTT-Copy" Limit, assert Working returned. Matches spec exactly. ✓
- `FindFollowerBracketOrderTestable` test seam at lines 3583/3593 noted as unchanged. ✓
- **xUnit only**: `[Fact]` confirmed. ✓

#### Completeness
- File(s): ✓ | Type: ✓ | Spec Req IDs: ✓ | Dependencies: ✓ | Change Description: ✓
- Method Signatures: `ActiveOrders(Account acc)` with full return type. Call site changes documented. ✓
- CYC Analysis: Full before/after table. ✓
- Acceptance Criteria: ✓ | Test Coverage: ✓ | 7-Scan Checklist: ✓ | NT8 Sync: ✓

#### File Routing
- `src/PropTraderTools/CopyEngine.cs` — correct Wave workspace path. ✓

**VERDICT: TICKET_REVIEW_PASS**

---

## Execution Order Section Review

Global execution order is present at the top of `04-tickets.md`:
- **Parallel Group A** (T1 + T2, same session sequential commits) ✓
- **Parallel Group B** (T4 + T5, same session sequential commits, independent of Group A) ✓
- **Sequential** (T3, after T1 VERIFY_PASS only) ✓
- Sessions A and B can run concurrently (different source files) ✓
- Ph5 FINAL REVIEW only after all 5 reach VERIFY_PASS ✓

---

## Violations Found

**None.**

All 5 tickets pass every checklist item:
- No JS P0 violations described (no lock(), no async void non-event-handler, no return null, no throw in hot paths)
- All 7-scan checklists present in every ticket (SCAN-01 through SCAN-07)
- All required [Fact] test names present and match spec
- NT8 banned APIs (Account.Change, AtmStrategyCreate, AtmStrategyChangeStopTarget) explicitly excluded from all production tickets
- Dispatcher.InvokeAsync correctly specified for T4 UI marshal
- All method signatures include return types and parameter types
- File routing: all .cs paths point to `src/PropTraderTools/` (Wave workspace)
- CYC <=8 confirmed for all new and modified methods
- Execution order section present and correct

**Informational observations (not violations):**
1. **T4 — HasNakedPosition CYC plan vs ticket discrepancy**: Plan §3.T4 states CYC=4; ticket correctly identifies Lizard will count 7-8 branches. Both confirm <=8 budget is met. Ticket proactively flags this and provides verification instruction. No action needed by architect.
2. **T3 — Dependency wording**: Mission brief says "T1 and T2 → T3"; plan §5 and tickets say "T1 VERIFY_PASS only → T3". Plan (REVIEW_PASS) is authoritative. No action needed.
3. **T4 — Method naming**: DW-NEW-08 backlog used "GetNonFlatPosition"; plan adopted "FindOpenPositionInstrument". Plan-level rename is within architect authority. No action needed.

---

## Verdict

**TICKET_REVIEW_PASS**
