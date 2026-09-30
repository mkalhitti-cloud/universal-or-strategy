# BWAVE-DW Repair LaneC -- Phase 5 Final Review

**Reviewer**: ptt-plan-reviewer (Phase 5)
**Epic**: BWAVE-DW Repair LaneC -- Surgical Fixes R-LC-1 (DW-C39-05b) and R-LC-2 (DW-C39-20)
**Date**: 2026-08-20
**Source Plan**: docs/brain/BWAVE-DW/Repair-LaneC/02-architecture-plan.md (REVIEW_PASS)
**Ticket Review**: docs/brain/BWAVE-DW/Repair-LaneC/04-ticket-review.md (TICKET_REVIEW_PASS both)
**Completion artifacts**:
  - docs/brain/BWAVE-DW/Repair-LaneC/ticket-R-LC-1-completion.md (BUILD_PASS)
  - docs/brain/BWAVE-DW/Repair-LaneC/ticket-R-LC-1-verification.md (VERIFY_PASS)
  - docs/brain/BWAVE-DW/Repair-LaneC/ticket-R-LC-2-completion.md (BUILD_PASS)
  - docs/brain/BWAVE-DW/Repair-LaneC/ticket-R-LC-2-verification.md (VERIFY_PASS)
**Prior deferred backlog**: docs/brain/BWAVE-DW/LaneC/06-deferred-backlog.md -- DOES NOT EXIST
  (Architecture plan correctly notes: no prior LaneC deferred backlog file exists.)

---

## STEP 1 -- Gate Prerequisite Checklist

| # | Prerequisite | Status | Evidence |
|---|--------------|--------|----------|
| G-1 | ticket-R-LC-1-completion.md exists | PASS | File confirmed in Repair-LaneC directory |
| G-2 | ticket-R-LC-1-verification.md exists and contains VERIFY_PASS | PASS | Final verdict: "VERIFY_PASS" |
| G-3 | ticket-R-LC-2-completion.md exists | PASS | File confirmed in Repair-LaneC directory |
| G-4 | ticket-R-LC-2-verification.md exists and contains VERIFY_PASS | PASS | Final verdict: "VERIFY_PASS" |
| G-5 | 04-ticket-review.md exists and shows TICKET_REVIEW_PASS for both tickets | PASS | Overall: "TICKET_REVIEW_PASS" -- both R-LC-1 and R-LC-2 |
| G-6 | 02-architecture-plan.md exists and shows REVIEW_PASS | PASS | Header: "Status: REVIEW_PASS" |
| G-7 | Prior 06-deferred-backlog.md consulted | PASS | docs/brain/BWAVE-DW/LaneC/06-deferred-backlog.md does not exist; plan noted this; no prior items to carry |

**Gate result: ALL PREREQUISITES SATISFIED. Phase 5 review proceeds.**

---

## STEP 2 -- Spec Requirements Matrix

| Requirement ID | Description | Addressed in Plan? | Addressed in Implementation? | Verified? |
|---------------|-------------|-------------------|------------------------------|-----------|
| B4 / DW-C39-05b | ApplyFeatureFlags call moved inside Dispatcher.InvokeAsync lambda in RefreshRuleRows() so flags apply AFTER rows are rebuilt | YES (plan §4 R-LC-1) | YES (ticket R-LC-1; line 173 confirmed) | YES (VERIFY_PASS; FACT-1 confirmed) |
| B5 / DW-C39-20 | Clear all remaining pending BE slots when last panel closes (prevent orphan AccountItemUpdate handlers) | YES (plan §4 R-LC-2) | YES (ticket R-LC-2; 3 changes confirmed) | YES (VERIFY_PASS; all 7 facts confirmed) |

**Spec coverage: 2/2 requirements satisfied.**

---

## STEP 3A -- Cross-File Coherence Check

### R-LC-1 and R-LC-2 File Separation

R-LC-1 touches: `TradeCopierWindow.cs` only.
R-LC-2 touches: `CopyEngine.cs`, `TradeCopierAddOn.cs`, `TradeCopierPanel.cs`.

Zero shared files between the two tickets. No conflict is possible at the file level.

**Cross-file conflict: NONE.**

### R-LC-1 Internal Coherence

The fix adds `ApplyFeatureFlags(CopyEngine.Instance.Flags)` as the final statement inside the
`Dispatcher.InvokeAsync` lambda in `RefreshRuleRows()`. The pre-existing call at line 154
(`OnLoaded`) is retained. The two-call pattern is intentional and documented in plan §4:

| Path | instruments.Count | Line 154 call | Lambda call |
|------|-------------------|---------------|-------------|
| No saved rules | 0 | Applies to default MES row (needed) | Never fires |
| Saved rules exist | > 0 | Applies before rows rebuilt (idempotent) | Applies after rebuild (CORRECT) |

`ApplyFeatureFlags` is a pure setter -- idempotent. Double-call on the saved-rules path is harmless.
Architecture plan §4 explicitly documents this dual-call design. COHERENT.

### R-LC-2 Internal Coherence -- IsPanelsEmpty() chain

The three-component chain is:

```
TradeCopierAddOn._panels (ConcurrentDictionary)
    TradeCopierAddOn.TryRemove(chart, out panel)  ← runs first in OnWindowDestroyed
    TradeCopierAddOn.IsPanelsEmpty() returns _panels.IsEmpty  ← NEW helper
        ↓ called from TradeCopierPanel.Detach()
    CopyEngine.ClearAllPendingBeSlots()  ← NEW method
        foreach _pendingBeSlots → AccountItemUpdate -= OnPendingBeAccountUpdate
        _pendingBeSlots.Clear()
```

Wiring verification:
- `TradeCopierAddOn.IsPanelsEmpty()` is `internal static bool` -- accessible from `TradeCopierPanel.Detach()` (same assembly). WIRED.
- `CopyEngine.ClearAllPendingBeSlots()` is `internal void` on `_engine` instance -- accessible from `TradeCopierPanel.Detach()` (same assembly, has `_engine` reference). WIRED.
- Guard placement in `Detach()`: AFTER `DisarmPendingBe(_leaderAccount)` (removes own slot first), BEFORE `// B32` comment. Ordering is correct. WIRED.
- Concurrent-close edge case: `TryRemove` is atomic; only the last panel to reduce `_panels` to zero sees `IsPanelsEmpty() == true`. `ConcurrentDictionary.Clear()` on an empty dict is safe. CORRECT.

**Chain complete: IsPanelsEmpty() exposed by AddOn, consumed by Panel, backed by CopyEngine.ClearAllPendingBeSlots. PASS.**

---

## STEP 3B -- Cross-File JS Violations (DNA Check)

### JS-021 (no lock) -- P0 CRITICAL

Independent verification performed by reviewer via `grep -r "lock\s*\(" src/PropTraderTools --include="*.cs"`.

**Result**: 37 matches returned -- ALL are comment text only (containing phrases such as
"no lock()", "no lock -- ConcurrentDictionary", etc.). Zero actual `lock(` keyword expressions
found anywhere in `src/PropTraderTools/`.

This is consistent with both engineer and verifier SCAN-01 reports:
- R-LC-1 SCAN-01: 0 matches (excluding comments) -- MATCH.
- R-LC-2 SCAN-01: 0 matches (excluding comments) -- MATCH.

**JS-021: PASS -- 0 actual lock() calls in all 4 modified files.**

### JS-033 (no async void) -- P0

- R-LC-1 SCAN-02: 2 comment-only matches in TradeCopierWindow.cs; 0 method declarations -- PASS.
- R-LC-2 SCAN-02: 10 comment-only matches across 3 files; 0 actual async void declarations -- PASS.

**JS-033: PASS.**

### JS-001 (no throw in hot path) -- P0

- R-LC-1 SCAN-04: 1 pre-existing `throw` at line 874 (converter, unrelated to change zone) -- PASS.
- R-LC-2 SCAN-04: 0 throw new -- PASS.

**JS-001: PASS -- 0 new throw in any change zone.**

### JS-002 (no return null) -- P0

- R-LC-1 SCAN-03: 6 pre-existing `return null` in TradeCopierWindow.cs; none in RefreshRuleRows change zone -- PASS.
- R-LC-2 SCAN-03: 48 pre-existing `return null` across 3 files; 0 new (new methods are void or bool) -- PASS.

**JS-002: PASS -- 0 new return null introduced.**

### JS-023 (no Monitor/Mutex/SemaphoreSlim for state) -- P0

Not applicable to any of the 4 modified files. Not present in new code. PASS.

### CYC <= 8 -- P1

| Method | File | CYC | <= 8? |
|--------|------|-----|-------|
| RefreshRuleRows | TradeCopierWindow.cs | 2 (lizard) / 3 (manual) | PASS |
| ClearAllPendingBeSlots | CopyEngine.cs | 3 (manual; lizard parser artifact noted) | PASS |
| IsPanelsEmpty | TradeCopierAddOn.cs | 1 | PASS |
| Detach (post R-LC-2 guard) | TradeCopierPanel.cs | ~6-7 (pre-existing + 1 new branch) | PASS |

Note on lizard artifact: Lizard reports CYC=9 for `ClearAllPendingBeSlots` due to C# parser context
confusion (all CopyEngine methods show `TrimSignal::` prefix indicating partial-class misattribution).
Manual inspection of lines 5771-5779 confirms exactly 3 decision points: base(1) + foreach(1) +
null guard(1) = CYC 3. Both engineer and independent verifier confirm this artifact identically.
True McCabe CYC = 3. PASS.

**CYC: ALL methods <= 8. PASS.**

### ASCII-only -- NT8 constraint

- SCAN-06 (R-LC-1): 0 non-ASCII bytes in TradeCopierWindow.cs -- PASS.
- SCAN-06 (R-LC-2): 0/0/0 non-ASCII bytes across all 3 files -- PASS.

**ASCII-only: PASS.**

---

## STEP 3C -- NT8 API Compliance

| API Check | R-LC-1 | R-LC-2 |
|-----------|--------|--------|
| No async/await in OnInitialize/OnWindowCreated/OnDestroyed | PASS (no lifecycle methods touched) | PASS (no lifecycle methods touched) |
| No Account.All in constructor | PASS (not introduced) | PASS (not introduced) |
| No sealed on TradeCopierWindow | PASS (not modified) | N/A |
| No FontFamily override | PASS (not introduced) | PASS (not introduced) |
| No hardcoded #RRGGBB hex | PASS (not introduced) | PASS (not introduced) |
| No CreateOrder without PTT- prefix | PASS (not introduced) | PASS (not introduced) |
| No DateTime.Now | PASS (not introduced) | PASS (not introduced) |
| Dispatcher.InvokeAsync usage | R-LC-1 uses existing pattern (line 168 ref) -- PASS | Not used (no UI work in R-LC-2) -- PASS |
| Account.AccountItemUpdate -= handler | N/A | Mirrors existing line 5758 DisarmPendingBe pattern -- PASS |
| ConcurrentDictionary.IsEmpty / .Clear() | N/A | .NET BCL, thread-safe -- PASS |

**NT8 API: ALL checks PASS.**

---

## STEP 3D -- NT8 Sync Gate

Both engineer and verifier artifacts confirm:

| Ticket | Engineer | Verifier |
|--------|----------|---------|
| R-LC-1 | 18/18 OK, 0 MISMATCH | 18/18 OK, 0 MISMATCH |
| R-LC-2 | 18/18 OK, 0 MISMATCH | 18/18 OK, 0 MISMATCH |

**NT8 sync: 18/18 OK confirmed by both engineer and verifier for both tickets. PASS.**

Note: F5 NinjaTrader 8 compile (SIM gate AC-6 / AC-7) is PENDING in both completion artifacts.
This is the standard status for this phase -- runtime SIM gates require a live NT8 host and are
outside the static review scope. Both dotnet build runs show 0 errors, confirming the code is
syntactically and structurally correct.

---

## STEP 3E -- Build Verification

| Ticket | Engineer Build | Verifier Build |
|--------|---------------|----------------|
| R-LC-1 | 0 errors (1 pre-existing xUnit2004 warning) | 0 errors, 0 warnings |
| R-LC-2 | 0 errors (1 pre-existing xUnit2004 warning at B131Tests.cs:165) | 0 errors, 0 warnings |

The warning count discrepancy (1 vs 0) is explained: the xUnit2004 warning at `B131Tests.cs:165` is
pre-existing and may have been resolved in a parallel lane commit between the two runs. Both agree on
0 errors. Non-blocking.

**BUILD: PASS -- 0 errors confirmed by both engineer and verifier.**

---

## STEP 3F -- All 7 Scans Final Sweep

### Aggregate Scan Results (both tickets, all 4 files)

| Scan | Scope | R-LC-1 Result | R-LC-2 Result | Aggregate |
|------|-------|---------------|---------------|-----------|
| SCAN-01 | lock() (uncommented) | 0 | 0 | ZERO |
| SCAN-02 | async void declarations | 0 | 0 | ZERO |
| SCAN-03 | return null (new only) | 0 new | 0 new | ZERO |
| SCAN-04 | throw new (new only) | 0 new | 0 new | ZERO |
| SCAN-05 | CYC > 8 (new/modified methods) | 0 violations | 0 violations | ZERO |
| SCAN-06 | Non-ASCII bytes | 0 | 0/0/0 | ZERO |
| SCAN-07 | NUnit/MSTest in production files | 0 | 0 | ZERO |

**All 7 scans zero across all 4 modified files in src/PropTraderTools/. PASS.**

---

## STEP 4 -- Section K: Deferred Work

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-DW-01 | DW-C38-01: Detach -- unsubscribe OnPendingBeArmedDispatch before clearing _leaderAccount; event could fire on detached panel | P1 | B5/B6/future | OPEN |
| DW-DW-02 | DW-C38-02: Detach -- _modules.Teardown() loop: verify all IPttModule impls call Dispose to avoid resource leaks | P2 | future | OPEN |
| DW-DW-03 | DW-C38-04: Detach -- _allAccounts.Clear() does not unsubscribe follower OrderUpdate/PositionUpdate handlers added in B41; potential memory leak | P1 | B5/B6/future | OPEN |
| DW-DW-04 | DW-C39-17 / DW-C39-06: OnAddRule -- BuildDynamicRuleRow() initializes buttons but no _rulesPanel.InvalidateMeasure() call; may leave stale layout on some WPF hosts | P2 | future | OPEN |
| DW-DW-05 | DW-C39-19 / DW-C39-09: OnAddRule -- no SaveRules() call after row add; rule not persisted to disk across NT8 sessions | P1 | B5/B6/future | OPEN |
| DW-C39-08 | OnAddRule -- no rule-count cap; unbounded rule row growth possible with rapid Add Rule clicks | P2 | future | OPEN |
| DW-C39-07 | ApplyFeatureFlags -- _trimBtns/_flattenBtns/_cancelBtns null-check absent (pre-existing risk) | P2 | future | OPEN |
| DW-LaneA-01 | T1 test DetachPanel_DoesNotDisarmSiblingPanelBeState uses structural assertion; consider integration SIM test for behavioral verification | P2 | future | OPEN |
| DW-LaneA-02 | T1 test DetachPanel_DisarmsOwnLeaderAccount uses structural assertion; no behavioral verification of line 591 call | P2 | future | OPEN |
| DW-LaneA-03 | T2 tests use reflection to invoke ApplyButtonGroupFlag; brittle if method signature changes | P2 | future | OPEN |
| DW-LaneA-04 | ApplyButtonGroupFlag null-guard on list argument absent (pre-existing; no crash observed) | P2 | future | OPEN |
| DW-WARN-B131 | Pre-existing xUnit2004 warning at B131Tests.cs:165 -- Assert.Equal() used for boolean condition; should be Assert.True() per xUnit best practice | P2 | future | OPEN |
| DW-RepairLC-01 | R-LC-1 SIM gate AC-6 (Starter license + persisted rules) PENDING -- requires live NT8 host + F5 compile | P1 | next SIM session | OPEN |
| DW-RepairLC-02 | R-LC-2 SIM gate AC-7 (arm BE ALL two accounts, close last chart, verify IsPendingSlotsEmpty==true) PENDING -- requires live NT8 host + F5 compile | P1 | next SIM session | OPEN |
| DW-B37-01..08 | B37 deferred items per mission brief -- not raised in this pipeline | P1/P2 | future | OPEN (pre-existing) |
| DW-C39-10..15 | C39 deferred items per mission brief -- not raised in this pipeline | P1/P2 | future | OPEN (pre-existing) |

**Note on DW-DW-01 through DW-DW-05**: These IDs map to the original BWAVE-DW mandate's DEFERRED list.
DW-C39-20 (DW-DW-05-equivalent) was addressed and CLOSED by R-LC-2 in this pipeline.
DW-C39-05 Part A+B were addressed in LaneA. DW-C39-05b (the Dispatcher timing sub-bug) was CLOSED by R-LC-1 in this pipeline.
Remaining items DW-DW-01 through DW-DW-03 and DW-DW-04/DW-DW-05 variants are still OPEN.

### Closed This Block

| ID | Description | Closed By |
|----|-------------|-----------|
| DW-C39-05b (B4) | ApplyFeatureFlags timing bug -- flags applied before rows rebuilt on startup with saved rules | R-LC-1 VERIFY_PASS |
| DW-C39-20 (B5) | Last-panel pending BE slot leak -- orphan AccountItemUpdate handlers after last chart close | R-LC-2 VERIFY_PASS |
| DW-LaneA-05 | ptt-sync-and-verify.ps1 output (18/18 OK) documented in completion artifacts | R-LC-1 and R-LC-2 completion reports |
| DW-LaneA-06 | F5 / dotnet build confirmation documented in completion artifacts | R-LC-1 and R-LC-2 completion + verification reports |

---

## STEP 5 -- Coherence Assessment

### Plan-to-Implementation Fidelity

| Plan Decision | Implemented? | Verified? |
|---------------|-------------|-----------|
| R-LC-1: ApplyFeatureFlags inside Dispatcher.InvokeAsync lambda at end of foreach | YES (line 173) | YES (FACT-1 in verifier) |
| R-LC-1: Line 154 ApplyFeatureFlags in OnLoaded retained | YES (confirmed at line 154) | YES (FACT-2 in verifier) |
| R-LC-1: RefreshRuleRows CYC unchanged | YES (CYC=2 lizard / 3 manual) | YES (verifier SCAN-05) |
| R-LC-2: ClearAllPendingBeSlots() added to CopyEngine.cs after IsPendingSlotsEmpty() | YES (lines 5771-5779) | YES (verifier CHANGE 2) |
| R-LC-2: IsPanelsEmpty() added to TradeCopierAddOn.cs | YES (line 57) | YES (verifier CHANGE 1) |
| R-LC-2: Detach() guard added after DisarmPendingBe, before B32 comment | YES (lines 594-595) | YES (verifier CHANGE 3) |
| R-LC-2: Unsubscription order: -= before .Clear() | YES (line 5776 before 5778) | YES (verifier fact table) |
| No lock() anywhere | CONFIRMED | CONFIRMED (SCAN-01 + reviewer grep) |
| No new NT8 API | CONFIRMED | CONFIRMED |
| 18/18 NT8 sync OK | CONFIRMED | CONFIRMED (both tickets) |
| Build 0 errors | CONFIRMED | CONFIRMED (both tickets) |

### System Coherence (4 modified files as a unit)

The four modified files form a complete and consistent system after this pipeline:

1. `TradeCopierWindow.cs` (R-LC-1): Dispatcher timing fix is self-contained within `RefreshRuleRows`.
   No cross-file dependencies introduced.

2. `TradeCopierAddOn.cs` (R-LC-2): `IsPanelsEmpty()` exposes `_panels.IsEmpty` as a static helper.
   Mirrors the existing `IsPendingSlotsEmpty()` pattern in CopyEngine. No new state introduced.

3. `CopyEngine.cs` (R-LC-2): `ClearAllPendingBeSlots()` follows the same unsubscribe-then-clear
   pattern as the existing `DisarmPendingBe(_leaderAccount)` method. No new fields introduced.

4. `TradeCopierPanel.cs` (R-LC-2): `Detach()` guard correctly invokes the AddOn check AFTER
   `TryRemove` (which runs in `OnWindowDestroyed` before `Detach()` is called), ensuring
   `_panels.IsEmpty` is accurate at the guard site.

The three-component chain (AddOn.IsPanelsEmpty → Panel.Detach guard → CopyEngine.ClearAllPendingBeSlots)
is complete, correctly ordered, and thread-safe. No missing links. No cross-file conflicts.

---

## Verdict

**FINAL_PASS**

Both spec requirements (B4 / DW-C39-05b and B5 / DW-C39-20) are satisfied in source code.
Cross-file JS violations: zero (lock() grep confirms 0 actual calls across all 4 modified files).
Missing wiring: none (IsPanelsEmpty → Detach guard → ClearAllPendingBeSlots chain verified complete).
All 7 scans zero across all 4 modified files.
NT8 sync: 18/18 OK confirmed by both engineer and verifier for both tickets.
Build: 0 errors confirmed by both engineer and verifier.
Section K written (required).
06-deferred-backlog.md written (required for FINAL_PASS gate).
