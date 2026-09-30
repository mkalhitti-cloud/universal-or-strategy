# Ticket Review: B113 — DW-B117 Cancel-After Fix
## Cycle: 2 (Final)
## Reviewer: ptt-ticket-reviewer (Phase 3.5)
## Date: 2026-08-26
## Rules Gate: PASS — RULES_CATALOG.md UTF-8 clean, JS-001..JS-110 readable, no P0 violations in ticket descriptions.

---

## T1 — DW-B117 Cancel-After Fix

**Title**: Remove Pre-Cancel, Add QX Cleanup State, Cancel Native ATM Brackets After PTT-QX Working

---

### Traceability

| Ticket Item | Spec / Plan Reference | Status |
|-------------|----------------------|--------|
| CHANGE-1: restructure ExecuteOne follower path | DW-B117 (root-cause fix), B113 plan REVIEW_PASS | PASS |
| CHANGE-2: add `_qxPendingFollowerCleanup` field | DW-B117 (cancel-after state), B113 plan REVIEW_PASS | PASS |
| REMOVE-PROBE: delete DW-B117-DIAG block | DW-B117 (probe removal), NO-PIPELINE-REPAIRS.md | PASS |
| CHANGE-3: dispatch `TryCleanupReArmedAtmBracket(e)` in OnOrderUpdate | DW-B117 (cancel-after trigger), B113 plan | PASS |
| CHANGE-4: new `TryCleanupReArmedAtmBracket` method | DW-B117 (cancel-after logic), B113 plan | PASS |
| ASSEMBLY-SEAM: `[InternalsVisibleTo]` + `internal` visibility | B113 plan (test seam requirement) | PASS |
| DW-B105 guard preserved (window now covers submit, not cancel) | DW-B105 (spec requirement) | PASS |
| DW-B112 `TryReplacePttBeBrackets` guard chain unchanged | DW-B112 (spec requirement) | PASS |
| NO-PIPELINE-REPAIRS.md update | DW-B117-DIAG probe removal protocol | PASS |
| 4 [Fact] tests in B113Tests.cs | B113 plan REVIEW_PASS (test mandate) | PASS |

No phantom work (items in ticket not in plan/spec): PASS.
No missing work (items in plan not covered by ticket): PASS.

**Traceability: PASS**

---

### JS Pre-Check (Jane Street DNA)

| Rule | Check | Source | Result |
|------|-------|--------|--------|
| JS-021 — No lock() | All AFTER blocks use ConcurrentDictionary TryAdd/TryGetValue/TryRemove. No `lock()` anywhere. | CHANGE-1, CHANGE-2, CHANGE-4 AFTER | PASS |
| JS-021 — No Dictionary<K,V> for shared state | `_qxPendingFollowerCleanup` is `ConcurrentDictionary` not `Dictionary`. `_qxCancelInProgress` unchanged (ConcurrentDictionary). | CHANGE-2 AFTER | PASS |
| JS-021 — No mutable struct fields | No struct definitions in any AFTER block. | All AFTER blocks | PASS |
| JS-001 — No throw new in hot path | No `throw new` in any AFTER block or test. | All AFTER blocks | PASS |
| JS-002 — No return null for optional value | `TryCleanupReArmedAtmBracket` is `void` — no return value. `_qxPendingFollowerCleanup` initialized at declaration. | CHANGE-2, CHANGE-4 | PASS |
| JS-008/009 — No mutable fields on struct; Freeze on brushes | No structs or brushes in scope. | N/A | PASS |
| JS-033 — No async void | All new/modified methods are synchronous `void`. `OnOrderUpdate` is NT8 event handler (pre-existing exempt). | All AFTER blocks | PASS |
| JS-025 — No UI update from non-UI thread without Dispatcher | No UI updates in any AFTER block. `Output.Process` is NT8 thread-safe. | All AFTER blocks | PASS |
| Empty string / missing-key as sentinel | No sentinel pattern — ConcurrentDictionary keyed on `acc.Name` (string identity, not sentinel). | CHANGE-2, CHANGE-4 | PASS |

**JS Pre-Check: PASS**

---

### CYC Pre-Check

| Method | File | Before | After | Assessment |
|--------|------|--------|-------|-----------|
| `ExecuteOne` | PttGlobalQuickExit.cs | 2 | 2 | PASS (≤8) |
| `TryCleanupReArmedAtmBracket` | CopyEngine.cs | N/A (new) | 5 | PASS (≤8) |
| `OnOrderUpdate` | CopyEngine.cs | N | N+1 | PASS (dispatch call adds 1 McCabe point; method was within budget) |
| ASSEMBLY-SEAM | CopyEngine.cs | N/A | 0 | PASS (attribute, no branch) |
| CHANGE-2 field | CopyEngine.cs | N/A | 0 | PASS (field declaration) |

CYC=5 for `TryCleanupReArmedAtmBracket` manually verified:
- Base = 1
- (1) outer compound guard (`if (... || ... || ...)`) = 1 McCabe point
- (2) `foreach` = 1
- (3) inner `if` inside foreach body = 1
- (4) `if (shouldRemove)` = 1
- `bool shouldRemove = tChar == '3' || ...` is a boolean assignment, NOT a decision point
- Total = 5 ✓

No method in any AFTER block exceeds CYC 8.

**CYC Pre-Check: PASS**

---

### NT8 Check

| Constraint | Ticket Claim | Verdict |
|-----------|-------------|---------|
| No async/await in lifecycle methods | No async in any AFTER block. | PASS |
| No `Account.All` call outside Loaded handler | No `Account.All` in any AFTER block. | PASS |
| No `sealed` on TradeCopierWindow | TradeCopierPanel.cs explicitly listed as NOT modified. | PASS |
| No `FontFamily` set on WPF element | No WPF/UI changes in any AFTER block. | PASS |
| No hardcoded hex color | No hex colors anywhere in AFTER blocks. | PASS |
| CreateOrder name starts "PTT-" | `TryCleanupReArmedAtmBracket` uses `acc.CancelOrder(toCancel)` — no `CreateOrder`. | PASS |
| No `DateTime.Now` usage | CHANGE-1 AFTER uses `DateTime.UtcNow.AddSeconds(2)`. CHANGE-4 AFTER uses `DateTime.UtcNow`. SCAN-06 includes `grep -n "DateTime\.Now[^U]"` verification. | PASS |
| `acc.CancelOrder(Order)` correct NT8 API signature | `acc.CancelOrder(toCancel)` where `acc` is `Account`, `toCancel` is `Order` — correct `Account.CancelOrder(Order)` signature per NT8_FULL_REFERENCE.md. | PASS |

**NT8 Check: PASS**

---

### Test Coverage

| New Method | [Fact] Test | Assertion Coverage |
|-----------|-------------|-------------------|
| `TryCleanupReArmedAtmBracket` (CHANGE-4) | `T_B113_03: QxPendingFollowerCleanup_ClearedAfterTtl` — tests TryRemove path on expired entry (shouldRemove=true branch) | PASS |
| `TryCleanupReArmedAtmBracket` name index logic | `T_B113_04: CancelAfter_TargetIndexMapping` — tests T1→Target1, T2→Target2, T3→Target3 mapping + guard conditions | PASS |
| `_qxPendingFollowerCleanup` TryAdd (CHANGE-1/CHANGE-2) | `T_B113_01: QxPendingFollowerCleanup_SetAfterExecuteOne_ForFollower` — tests TryAdd path, key presence, expiry window | PASS |
| `_qxPendingFollowerCleanup` absent on leader path (CHANGE-1) | `T_B113_02: QxPendingFollowerCleanup_NotSet_ForLeader` — tests absence of TryAdd on leader path | PASS |
| CHANGE-3 dispatch in OnOrderUpdate | Covered transitively via T_B113_03 and T_B113_04 which exercise TryCleanupReArmedAtmBracket logic directly | PASS |
| ASSEMBLY-SEAM (InternalsVisibleTo) | Required by T_B113_03 which calls internal method directly | PASS |
| REMOVE-PROBE (removal) | No test needed — deletion of diagnostic block has no behavioral contract to assert | N/A |

All 4 `[Fact]` tests: xUnit only (`using Xunit;`), synchronous `public void`, concrete Arrange/Act/Assert. No NUnit, no MSTest, no `async void`. PASS.

**Test Coverage: PASS**

---

### Scan Checklist (Item 15 — Cycle 2 Specific Fix)

Verifying the revised ticket's 7-scan checklist against the SCAN-01..SCAN-07 canonical format:

| Scan | Label Present | Command Present | Pass Criterion Present | Canonical Coverage |
|------|--------------|----------------|----------------------|-------------------|
| SCAN-01 | `#### SCAN-01 — No lock() in modified region` | `grep -n "lock("` on both files | 0 results in modified methods | lock() (JS-021) ✓ |
| SCAN-02 | `#### SCAN-02 — No async void introduced` | `grep -n "async void "` on both files | 0 results | async void (JS-033) ✓ |
| SCAN-03 | `#### SCAN-03 — No throw new Exception or return null introduced` | `grep -n "throw new"` + `grep -n "return null"` | 0 new occurrences | throw/null (JS-001/JS-002) ✓ |
| SCAN-04 | `#### SCAN-04 — ASCII-only strings and comments in modified region` | `grep -Pn "[^\x00-\x7F]"` on both files | 0 results | ASCII-only ✓ |
| SCAN-05 | `#### SCAN-05 — CYC <= 8 verified for all in-scope methods` | `python scripts/complexity_audit.py` | All green CYC ≤ 8 | CYC check ✓ |
| SCAN-06 | `#### SCAN-06 — NT8-API correctness and DateTime.Now ban` | `grep -n "CancelOrder"` + `grep -n "DateTime\.Now[^U]"` | correct sig, 0 DateTime.Now | NT8-API + DateTime.UtcNow ✓ |
| SCAN-07 | `#### SCAN-07 — ptt-sync-and-verify.ps1 passes 0 MISMATCH` | `powershell -File scripts\ptt-sync-and-verify.ps1` | 0 MISMATCH lines | sync gate ✓ |

All 7 scans present. All have SCAN-NN labeled headings. All have commands. All have pass criteria.
Cycle 1 FAIL reason (missing SCAN-01..SCAN-07 labeled format) is **RESOLVED**.

**Scan Checklist: PASS**

---

### File Routing

| File | Path | Correct Workspace |
|------|------|------------------|
| PttGlobalQuickExit.cs | `src/PropTraderTools/Features/PttGlobalQuickExit.cs` | Wave workspace ✓ |
| CopyEngine.cs | `src/PropTraderTools/CopyEngine.cs` | Wave workspace ✓ |
| B113Tests.cs | `src/PropTraderTools/Tests/B113Tests.cs` | Wave workspace ✓ |
| NO-PIPELINE-REPAIRS.md | `docs/brain/NO-PIPELINE-REPAIRS.md` | Wave workspace (non-.cs) ✓ |

No Director workspace paths for .cs files.

**File Routing: PASS**

---

### Cycle 2 Delta from Cycle 1

| Item | Cycle 1 | Cycle 2 |
|------|---------|---------|
| Items 1–14 | PASS | PASS (unchanged) |
| Item 15 — Scan Checklist format | **FAIL** — scans labeled by JS rule names, not SCAN-01..SCAN-07 | **PASS** — revised ticket uses `#### SCAN-01` through `#### SCAN-07` with labels, commands, and pass criteria |

---

### VERDICT: TICKET-B113-T1

| Check | Result |
|-------|--------|
| Traceability | PASS |
| JS Pre-Check | PASS |
| CYC Pre-Check | PASS |
| NT8 Check | PASS |
| Test Coverage | PASS |
| Scan Checklist | PASS |
| File Routing | PASS |

**TICKET-B113-T1: TICKET_REVIEW_PASS**

---

## Overall: TICKET_REVIEW_PASS

All 15 checklist items PASS. The single Cycle 1 violation (Item 15 — missing SCAN-01..SCAN-07 labeled format) has been corrected in the revised ticket. No new violations introduced. The engineer contract is complete and safe to execute.

**Gate result: TICKET_REVIEW_PASS**

---

*Reviewed by ptt-ticket-reviewer (Phase 3.5). Cycle 2 of 2. Safe to spawn ptt-engineer (Phase 4a).*
