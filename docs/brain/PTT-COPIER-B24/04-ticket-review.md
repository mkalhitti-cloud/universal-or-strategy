# Ticket Review: PTT-COPIER-B24
**Phase**: 3.5 (Ticket Review)
**Reviewer**: ptt-ticket-reviewer
**Date (First Pass)**: 2026-07-07
**Date (Second Pass)**: 2026-07-07
**Tickets Reviewed**: `docs/brain/PTT-COPIER-B24/04-tickets.md`
**Plan Reviewed**: `docs/brain/PTT-COPIER-B24/02-architecture-plan.md`

---

## Second-Pass Summary

Prior violation (First Pass): T1 SCAN-06 pass criterion read `Count = 128` — incorrect because T1
does not add tests; only T2 does. This created an impossible pass criterion for the T1 engineer.

Fix applied by architect: T1 SCAN-06 now reads:
> `Count = 126 (T1 does not add tests; T2 raises count to 128)`

T2 SCAN-06 is unchanged: `Count = 128 (126 existing + 2 new)`.

All 12 checks re-run below against the updated file. No new violations found.

---

## T1 — CopyEngine.cs: Add `BreakEven(Account, Instrument, int)` Overload + Fix `OnPendingBeAccountUpdate`

### Traceability: PASS

| Ticket Item | Plan/Spec Reference | Status |
|-------------|---------------------|--------|
| New `BreakEven(Account, Instrument, int)` overload | Plan Section 2 STEP 1; Spec REQ-B24-01, REQ-B24-03 | MAPPED |
| Single-line fix at `OnPendingBeAccountUpdate:1396` | Plan Section 2 STEP 2a; Spec REQ-B24-04 (1 of 6 sites) | MAPPED |
| `DW-B23-BE-ALLACCOUNTS-01` closure | Plan Root Cause Chain; Spec Defect ID | MAPPED |

No phantom work (items in ticket not in plan). No missing work (all T1-scoped plan items covered).

### JS Pre-Check: PASS

| Rule | Description | Verdict |
|------|-------------|---------|
| JS-021 | No `lock()` in new/modified code | PASS — new overload is lock-free; line 1396 change adds no lock |
| JS-002 | Null leader → `StatusUpdate?.Invoke(...)` + `return` | PASS — Branch 1 fires StatusUpdate then returns; no fall-through |
| JS-001 | No `throw new XxxException(...)` in hot path | PASS — no throw statement in new overload |
| JS-008 | No mutable struct fields | PASS — no struct described |
| JS-009 | No unfrozen SolidColorBrush | PASS — no UI brushes |
| JS-033 | Not `async void` | PASS — declared `internal void` |

### CYC Pre-Check: PASS

| Method | CYC | Verdict |
|--------|-----|---------|
| `BreakEven(Account, Instrument, int)` | 4 (1 base + 3 branches) | PASS — ≤ 8 |
| `OnPendingBeAccountUpdate` (1-line change only) | Unchanged | PASS |

### NT8 Check: PASS

| Constraint | Verdict |
|------------|---------|
| No `async/await` in lifecycle methods | PASS |
| No `Account.All` outside Loaded handler | PASS — `AllAccounts(Instrument)` wrapper used |
| No `sealed` on TradeCopierWindow | PASS |
| No FontFamily assignment | PASS |
| No hardcoded hex color | PASS |
| No `CreateOrder` with non-`PTT-` prefix | PASS |
| No `DateTime.Now` | PASS |
| CYC ≤ 8 | PASS — CYC = 4 |

### Test Coverage: PASS

| Method | Test Name | Verdict |
|--------|-----------|---------|
| `BreakEven(Account, Instrument, int)` — Branch 1 (null leader) | `BreakEven_WithLeaderAccount_NoRule_FiresStatusUpdateLeaderNull` | NAMED in T1, implemented in T2 ✅ |
| `BreakEven(Account, Instrument, int)` — Branch 2/3 (non-null, null instrument) | `BreakEven_AccountOverload_NullInstrument_NoException` | NAMED in T1, implemented in T2 ✅ |
| `OnPendingBeAccountUpdate` single-line parameter change | No new test required — no new logic path | PASS |

### Scan Checklist: PASS

All 7 scans present with correct PowerShell commands and pass criteria:

| Scan | Topic | Pass Criterion | Present |
|------|-------|----------------|---------|
| SCAN-01 | JS-021 no lock() | Zero matches | ✅ |
| SCAN-02 | JS-002 StatusUpdate string | Exactly 1 match | ✅ |
| SCAN-03 | CYC ≤ 8 new overload | CYC ≤ 8 (expected 3 or 4) | ✅ |
| SCAN-04 | 2-param overload unchanged | Exactly 1 match | ✅ |
| SCAN-05 | No stale 2-param CopyEngine.cs call | Zero matches | ✅ |
| SCAN-06 | `[Fact]` count baseline | **Count = 126** (T1 adds no tests) | ✅ FIXED |
| SCAN-07 | NT8-043 no null-conditional `-=` | Zero matches | ✅ |

> **SCAN-06 fix confirmed**: Prior criterion `Count = 128` changed to `Count = 126 (T1 does not add tests; T2 raises count to 128)`. Criterion is now achievable at T1 commit time.

### File Routing: PASS

- `c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs` → Wave workspace (`universal-or-strategy`) ✅
- No Director workspace (`universal-or-strategy-director`) `.cs` path references ✅

### VERDICT: TICKET_REVIEW_PASS

---

## T2 — TradeCopierPanel.cs + CopyEngineTests.cs: Update 5 Call Sites + Add 2 Tests

### Traceability: PASS

| Ticket Item | Plan/Spec Reference | Status |
|-------------|---------------------|--------|
| 5 call-site rewrites in TradeCopierPanel.cs | Plan Section 2 STEP 2b-f; Spec REQ-B24-04 (5 of 6 sites) | MAPPED |
| 2 new `[Fact]` tests in CopyEngineTests.cs | Plan Section 2 STEP 3; Spec REQ-B24-05 | MAPPED |
| T2 dependency on T1 (compile ordering) | Plan Section 2 STEP 2 preamble | MAPPED |

No phantom work. No missing plan items for T2 scope.

### JS Pre-Check: PASS

| Rule | Description | Verdict |
|------|-------------|---------|
| JS-021 | No `lock()` in modified code | PASS — all changes are single-line parameter additions; no lock introduced |
| JS-002 | Null `_leaderAccount` handled by overload Branch 1 | PASS — call sites pass `_leaderAccount` directly; overload's Branch 1 handles null |
| JS-001 | No `throw` in test code | PASS — tests use `Record.Exception` pattern exclusively |
| JS-008 | No mutable struct fields | PASS |
| JS-009 | No unfrozen SolidColorBrush | PASS |

### CYC Pre-Check: PASS

| Method | CYC | Verdict |
|--------|-----|---------|
| `OnBeUp`, `OnBeDown`, `OnBeConnected`, `OnBreakEven`, `DispatchShortcut` | Unchanged (single-line parameter change adds no branch) | PASS |

### NT8 Check: PASS

| Constraint | Verdict |
|------------|---------|
| No `async/await` in lifecycle methods | PASS — single-line rewrites only |
| No `sealed` on TradeCopierWindow | PASS |
| No FontFamily | PASS |
| No hardcoded hex color | PASS |
| No `CreateOrder` | PASS |
| No `DateTime.Now` | PASS |
| Test co-location (NT8-032) | PASS — tests appended to existing `CopyEngineTests.cs` |

### Test Coverage: PASS

| Test Method | Assertion Type | What It Verifies | Verdict |
|-------------|----------------|-----------------|---------|
| `BreakEven_WithLeaderAccount_NoRule_FiresStatusUpdateLeaderNull` | `Assert.Null(ex)` + `Assert.Equal(exact string, received)` | Branch 1: null leader fires StatusUpdate with exact sentinel, no throw | PASS |
| `BreakEven_AccountOverload_NullInstrument_NoException` | `Assert.Null(ex)` | Branch 2/3: non-null leader with null instrument does not throw | PASS |

Both tests are deterministic. `Assert.Equal` checks exact string equality. `Record.Exception` is the correct xUnit no-throw pattern.

### Scan Checklist: PASS

All 7 scans present with correct PowerShell commands and pass criteria:

| Scan | Topic | Pass Criterion | Present |
|------|-------|----------------|---------|
| SCAN-01 | JS-021 no lock() | Zero matches | ✅ |
| SCAN-02 | JS-002 StatusUpdate string | Exactly 1 match (from T1) | ✅ |
| SCAN-03 | CYC unchanged for 5 modified methods | CYC unchanged | ✅ |
| SCAN-04 | 2-param overload unchanged | Exactly 1 match | ✅ |
| SCAN-05 | All 5 panel call sites migrated | Zero old-form matches | ✅ |
| SCAN-06 | `[Fact]` count = 128 | Count = 128 (126 + 2) | ✅ |
| SCAN-07 | NT8-043 no null-conditional `-=` | Zero matches | ✅ |

### File Routing: PASS

- `c:\WSGTA\universal-or-strategy\src\PropTraderTools\TradeCopierPanel.cs` → Wave workspace ✅
- `c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngineTests.cs` → Wave workspace ✅
- No Director workspace `.cs` path references ✅

### VERDICT: TICKET_REVIEW_PASS

---

## Spec Coverage Matrix (Aggregate)

| Requirement ID | Description | Ticket | Covered |
|----------------|-------------|--------|---------|
| REQ-B24-01 | BreakEven fires for leader when no rule registered | T1 | ✅ |
| REQ-B24-02 | Follower fan-out preserved (backward compat) | T1 (loop body) | ✅ |
| REQ-B24-03 | Null leader guard emits StatusUpdate and returns | T1 (Branch 1) | ✅ |
| REQ-B24-04 | All 6 call sites updated to 3-param form | T1 (1) + T2 (5) | ✅ |
| REQ-B24-05 | Test count 126 → 128 | T2 | ✅ |
| DW-B23-BE-ALLACCOUNTS-01 | Defect closed | T1 + T2 | ✅ |

No uncovered requirements. No duplicate coverage.

---

## Overall: TICKET_REVIEW_PASS

**All checks passed on second pass.** The single prior violation (T1 SCAN-06 impossible pass criterion)
has been correctly remediated by the architect. Both tickets are cleared for engineer execution.

| Ticket | Traceability | JS Pre-Check | CYC Pre-Check | NT8 Check | Test Coverage | Scan Checklist | File Routing | Verdict |
|--------|-------------|-------------|--------------|----------|--------------|---------------|-------------|---------|
| T1 | PASS | PASS | PASS | PASS | PASS | PASS | PASS | **TICKET_REVIEW_PASS** |
| T2 | PASS | PASS | PASS | PASS | PASS | PASS | PASS | **TICKET_REVIEW_PASS** |

**Overall TICKET_REVIEW_PASS.** Safe to spawn engineer (Phase 4a).

---

*First pass: ptt-ticket-reviewer · PTT-COPIER-B24 · 2026-07-07 — TICKET_REVIEW_FAIL (T1 SCAN-06)*
*Second pass: ptt-ticket-reviewer · PTT-COPIER-B24 · 2026-07-07 — TICKET_REVIEW_PASS*
