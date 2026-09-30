# Ticket Review: B26-LaneAB

**Epic**: B26-LaneAB
**Phase**: 3.5 (Ticket Review)
**Reviewer**: ptt-ticket-reviewer
**Input tickets**: `docs/brain/B26-LaneAB/04-tickets.md`
**Input plan**: `docs/brain/B26-LaneAB/02-architecture-plan.md` (REVIEW_PASS)
**Spec**: `specs/002-trade-copier-spec.html` § block-b26
**Rules**: `docs/standards/jane-street/RULES_CATALOG.md`

---

## T1 — DW-B26-01 wrong BreakEven overload + DW-B26-02 event signature (CopyEngine.cs)

**Ticket ID**: B26-AB-T1
**Files in scope**: `src/PropTraderTools/CopyEngine.cs`, `src/PropTraderTools/CopyEngineTests.cs`

### Traceability: PASS

| Change | Spec Req ID | Plan Section | Status |
|--------|-------------|--------------|--------|
| Change 1 — event declaration | DW-B26-02 | Plan §A Change 1 | Mapped |
| Change 2 — BreakEven call site | DW-B26-01 | Plan §A Change 2 | Mapped |
| Change 3 — PendingBeFired invoke | DW-B26-02 | Plan §A Change 3 | Mapped |
| Test 1 — T_B26_01_TrailBe_WithNoRule_StillMovesStop | DW-B26-01 | Plan §D Test 1 | Mapped |
| Test 2 — T_B26_02_PendingBeFired_CarriesAccountName | DW-B26-02 | Plan §D Test 2 | Mapped |

No phantom work. No plan work missing from this ticket.

### JS Pre-Check: PASS

| Rule | Check | Result |
|------|-------|--------|
| JS-021 — lock() | No `lock(` in Changes 1–3 | PASS |
| JS-001 — throw in hot path | No `throw new` introduced | PASS |
| JS-002 — return null | No `return null` introduced (guards use `return;`) | PASS |
| JS-033 — async void | No `async void` introduced | PASS |
| JS-036/037 — heap alloc | No new array allocations | PASS |

### CYC Pre-Check: PASS

| Method | Current CYC | Change | New CYC | Limit | Result |
|--------|-------------|--------|---------|-------|--------|
| `OnTrailBeAccountUpdate` | 5 | Change 2 is inside existing `if (instr != null)` branch — no new decision point | 5 | 8 | PASS |
| `OnPendingBeAccountUpdate` | 8 | Change 3 is a call-site argument change — no new branch | 8 | 8 | PASS |

SCAN-07 asserts: `OnTrailBeAccountUpdate` CYC = **5**, `OnPendingBeAccountUpdate` CYC = **8**.

### NT8 Check: PASS

| Rule | Check | Result |
|------|-------|--------|
| NT8-001 — init accessor | Not introduced | PASS |
| NT8-002 — record types | Not introduced | PASS |
| NT8-003 — volatile double | Not introduced | PASS |
| NT8-004 — ImmutableDictionary | Not introduced | PASS |
| DateTime.Now | Not referenced | PASS |
| FontFamily | Not referenced | PASS |
| Hex colors | Not referenced | PASS |
| ASCII-only identifiers | `accountName`, `acc`, `instr` — all ASCII | PASS |

### Test Coverage: PASS

| Method / Change | [Fact] Test | Status |
|-----------------|-------------|--------|
| Change 2 — `OnTrailBeAccountUpdate` call site | `T_B26_01_TrailBe_WithNoRule_StillMovesStop` | Present |
| Change 3 — `OnPendingBeAccountUpdate` invoke | `T_B26_02_PendingBeFired_CarriesAccountName` | Present |
| Change 1 — event declaration (compiler contract) | Covered by Test 2 (compile-time check + event assertion) | Present |

[Fact] count: Baseline 131 → Target **133** (stated in ticket header, AC-1, and SCAN-06). +2 new tests.

### Scan Checklist: PASS

All 7 scans present in T1:

| Scan | Target | Required Result | Present |
|------|--------|-----------------|---------|
| SCAN-01 | lock() in CopyEngine.cs | 0 results | ✅ |
| SCAN-02 | async void in CopyEngine.cs | 0 results | ✅ |
| SCAN-03 | return null in CopyEngine.cs | Same as baseline | ✅ |
| SCAN-04 | throw new in CopyEngine.cs | Same as baseline | ✅ |
| SCAN-05 | CreateOrder PTT- prefix in CopyEngine.cs | All PTT- prefixed | ✅ |
| SCAN-06 | [Fact] count in CopyEngineTests.cs | **133** | ✅ |
| SCAN-07 | CYC on OnTrailBeAccountUpdate + OnPendingBeAccountUpdate | 5 and 8 | ✅ |

### File Routing: PASS

| File | Path | Workspace |
|------|------|-----------|
| Source | `src/PropTraderTools/CopyEngine.cs` | Wave (`c:\WSGTA\universal-or-strategy\`) |
| Tests | `src/PropTraderTools/CopyEngineTests.cs` | Wave |

> **Note (non-blocking)**: Ticket test file path is `src/PropTraderTools/CopyEngineTests.cs`; plan §D references `src/PropTraderTools.Tests/CopyEngineTests.cs`. Both are Wave workspace paths. Engineer must confirm the actual path before writing. Not a routing violation, but flagged for engineer awareness.

### VERDICT: TICKET_REVIEW_PASS

---

## T2 — DW-B26-02 OnPendingBeFiredDispatch + OnBeConnected account guard (TradeCopierPanel.cs)

**Ticket ID**: B26-AB-T2
**Files in scope**: `src/PropTraderTools/TradeCopierPanel.cs`

### Traceability: PASS

| Change | Spec Req ID | Plan Section | Status |
|--------|-------------|--------------|--------|
| Change 4 — OnPendingBeFiredDispatch signature + body | DW-B26-02 | Plan §A Change 4 | Mapped |
| Change 5 — OnBeConnected signature + account guard | DW-B26-02 | Plan §A Change 5 | Mapped |

No phantom work. No plan work missing from this ticket.
Dependency on T1 explicitly stated: "**T1 must be complete first.** The `PendingBeFired` event must already be declared as `Action<string, string>` (Change 1 from T1)." Confirmed in Execution Order section.

### JS Pre-Check: PASS

| Rule | Check | Result |
|------|-------|--------|
| JS-021 — lock() | No `lock(` in Changes 4–5 | PASS |
| JS-001 — throw in hot path | No `throw new` introduced | PASS |
| JS-002 — return null | New guard uses `return;` (void return) — not `return null` | PASS |
| JS-033 — async void | No `async void` introduced; Dispatcher.InvokeAsync used correctly with lambda | PASS |
| JS-036/037 — heap alloc | No new array allocations | PASS |

### CYC Pre-Check: PASS

| Method | Current CYC | Change | New CYC | Limit | Result |
|--------|-------------|--------|---------|-------|--------|
| `OnPendingBeFiredDispatch` | 1 | Signature change + lambda arg only — no new decision point | 1 | 8 | PASS |
| `OnBeConnected` | 3 | New guard `if (_leaderAccount == null \|\| _leaderAccount.Name != accountName)` adds 1–2 branches | ≤ 5 | 8 | PASS |

SCAN-07 asserts: `OnBeConnected` CYC ≤ **5**, `OnPendingBeFiredDispatch` CYC = **1**.

### NT8 Check: PASS

| Rule | Check | Result |
|------|-------|--------|
| NT8-001 — init accessor | Not introduced | PASS |
| NT8-002 — record types | Not introduced | PASS |
| NT8-003 — volatile double | Not introduced | PASS |
| NT8-004 — ImmutableDictionary | Not introduced | PASS |
| sealed on TradeCopierWindow | Not introduced | PASS |
| DateTime.Now | Not referenced | PASS |
| FontFamily | Not referenced | PASS |
| Hex colors | Not referenced | PASS |
| ASCII-only identifiers | `accountName`, `instr` — all ASCII | PASS |

### Test Coverage: PASS

T2 specifies **0** new [Fact] tests. This is correct per the plan and per mandatory check 4 (T2 = 0 new tests). The UI dispatch path is verified via F5 compile clean + visual integration test. No public/internal methods are added by T2 — only existing methods are modified in place. No [Fact] gap exists.

### Mandatory Check 8 — Account Guard Exact Text: PASS

Required text present in Change 5 NEW block:
```
if (_leaderAccount == null || _leaderAccount.Name != accountName) return;
```
Confirmed verbatim in T2 Change 5 NEW text. AC-3 verification command greps for this exact string (1 result required).

### Mandatory Check 9 — DW-B26-02 Comment Exact Text: PASS

Required text present in Change 5 NEW block:
```
// DW-B26-02: only update state for the panel whose account fired BE
```
Confirmed verbatim in T2 Change 5 NEW text. AC-3 greps for the comment string. ✅

### Scan Checklist: PASS

All 7 scans present in T2:

| Scan | Target | Required Result | Present |
|------|--------|-----------------|---------|
| SCAN-01 | lock() in TradeCopierPanel.cs | 0 results | ✅ |
| SCAN-02 | async void in TradeCopierPanel.cs | 0 results | ✅ |
| SCAN-03 | return null in TradeCopierPanel.cs | Same as baseline | ✅ |
| SCAN-04 | throw new in TradeCopierPanel.cs | Same as baseline | ✅ |
| SCAN-05 | CreateOrder PTT- prefix in TradeCopierPanel.cs | All PTT- prefixed | ✅ |
| SCAN-06 | No 1-arg forms remain (OnBeConnected + OnPendingBeFiredDispatch) | All 2-arg | ✅ |
| SCAN-07 | CYC on OnBeConnected + OnPendingBeFiredDispatch | ≤5 and 1 | ✅ |

> Note: T2 SCAN-06 appropriately substitutes the [Fact]-count check (not applicable — no new tests) with a signature-clean check relevant to T2's scope. This is a correct adaptation per the ticket's test-free nature.

### File Routing: PASS

| File | Path | Workspace |
|------|------|-----------|
| Source | `src/PropTraderTools/TradeCopierPanel.cs` | Wave (`c:\WSGTA\universal-or-strategy\`) |

No Director workspace (.cs) paths. No test file required (0 new tests).

### VERDICT: TICKET_REVIEW_PASS

---

## Mandatory Check Summary

| # | Check | T1 | T2 |
|---|-------|----|----|
| 1 | Traceability — all changes map to DW-B26-01/02 | PASS | PASS |
| 2 | Spec coverage — DW-B26-01 and DW-B26-02 fully covered | PASS | PASS |
| 3 | Exact OLD→NEW text for all 5 changes | PASS (Changes 1–3) | PASS (Changes 4–5) |
| 4 | [Fact] coverage — T1=2 tests, T2=0 tests | PASS | PASS |
| 5 | JS pre-check — no lock/async void/return null/throw | PASS | PASS |
| 6 | NT8 constraints — no banned patterns | PASS | PASS |
| 7 | Dependency ordering — T2 depends on T1 stated | N/A | PASS |
| 8 | T2 account guard exact text present | N/A | PASS |
| 9 | T2 DW-B26-02 comment exact text present | N/A | PASS |
| 10 | CYC assertions — OnTrailBe=5, OnPendingBe=8, OnBeCon≤5, OnDispatch=1 | PASS | PASS |
| 11 | PTT- prefix — no new CreateOrder calls | PASS | PASS |
| 12 | [Fact] count target — baseline 131, target 133 stated | PASS | N/A |

**Violations**: None.

---

## Overall: TICKET_REVIEW_PASS

Both tickets pass all 12 mandatory checks. No Jane Street rule violations. No NT8 constraint violations. No CYC limit violations. All 5 changes carry exact OLD→NEW text. Both tickets carry full 7-scan checklists. File routing is Wave workspace throughout. T2 correctly declares dependency on T1.

**Cleared for Phase 4a (ptt-engineer).** Execute T1 first, then T2.
