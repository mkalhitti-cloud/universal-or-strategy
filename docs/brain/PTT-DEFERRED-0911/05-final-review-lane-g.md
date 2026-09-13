# PTT-DEFERRED-0911 Lane G — Final Review (Phase 5)

**Epic:** PTT-DEFERRED-0911
**Lane:** G
**Phase:** 5 (Final Review)
**Reviewer:** ptt-plan-reviewer
**Date:** 2026-09-11
**Verdict:** FINAL_PASS

---

## A. Documents Reviewed

| Document | Status |
|----------|--------|
| `docs/brain/PTT-DEFERRED-0911/02-architecture-plan-lane-g.md` (Rev 3) | REVIEW_PASS |
| `docs/brain/PTT-DEFERRED-0911/04-ticket-review-lane-g.md` | TICKET_REVIEW_PASS |
| `docs/brain/PTT-DEFERRED-0911/ticket-1-completion-lane-g.md` | BUILD_PASS |
| `docs/brain/PTT-DEFERRED-0911/ticket-1-verification-lane-g.md` | VERIFY_PASS |
| `docs/brain/PTT-DEFERRED-0911/06-deferred-backlog.md` (prior Lane F block) | READ (prior blocks only) |
| `docs/standards/RULES_CATALOG.md` | READ |

---

## B. System Coherence Check

Lane G modifies exactly 4 files to form a complete Option A-partial solution for DW-B26-01.
The 4 changes are examined as a cross-file coherent system below.

### B.1 — ICopyEngine.BreakEven (PttContracts.cs:99)

`void BreakEven(Account leader, Instrument instrument, int bufferTicks)` added to `ICopyEngine`
after `RelayCancel`, before closing brace. Short names used; `using NinjaTrader.Cbi;` present at
line 7. This is the interface contract that allows `StubEngine` in tests to satisfy the full
`ICopyEngine` surface without any NT8 runtime.

**Verdict:** Present and correct. Wires to P2 (CopyEngine.BreakEven public) and P4.1
(ExecuteBreakEven parameter type). PASS.

### B.2 — CopyEngine.BreakEven public (CopyEngine.cs:6871)

Access modifier changed from `internal` to `public`. Body unchanged (CYC=5). `CopyEngine`
is `internal sealed`; the `public` modifier on `BreakEven` is assembly-scoped — no external
exposure risk. `CopyEngine : ICopyEngine` is now satisfied for the new `BreakEven` interface
member without compile error.

**Verdict:** Single-word change confirmed by verifier (SCAN-07b, line 141-148 of verification
artifact). PASS.

### B.3 — TradeCopierPanel.ExecuteBreakEven (TradeCopierPanel.cs:3071)

`internal static void ExecuteBreakEven(ICopyEngine engine, Account leader, Instrument instrument,
int beBuffer)` inserted after `DispatchShortcut` closing brace (line 3067), before
`BuildRiskAtrRow` (line 3080). CYC=1 (single statement). `InternalsVisibleTo` already granted
for test project. No NT8 UI dependency. No lock. No throw.

**Verdict:** Confirmed at exact line by verifier (SCAN-07c, lines 150-158). PASS.

### B.4 — DispatchShortcut Key.B delegation (TradeCopierPanel.cs:3063-3065)

Key.B case now calls `ExecuteBreakEven(_engine, _leaderAccount, _instrument, _beBuffer)` —
NOT `_engine.BreakEven(...)` directly. This is the critical wiring check:

- `_engine` is typed `CopyEngine`; `CopyEngine : ICopyEngine` — implicit assignment, no cast.
- `ExecuteBreakEven` calls `engine.BreakEven(leader, instrument, beBuffer)` — behavioral identity
  with prior inline call confirmed.
- `DispatchShortcut` CYC unchanged at 5.

**Verdict:** Confirmed by verifier SCAN-07d (lines 162-169). `_engine.BreakEven(...)` direct call
is gone; `ExecuteBreakEven(...)` call is present. PASS.

### B.5 — LaneGTests / StubEngine / [Fact] (CopyEngineTests.cs:8996-9027)

`public class LaneGTests` with `private sealed class StubEngine : ICopyEngine` implementing all
5 interface members: `RelayBe`, `RelayTrim`, `RelayFlatten`, `RelayCancel`, `BreakEven`.
`StubEngine` records `BreakEvenCallCount` and `LastBuffer` — richer than plan's `LastBuffer`-only
design. `[Fact]` (no Skip) asserts `BreakEvenCallCount==1` AND `LastBuffer==3` — verifies the
dispatch path actually invokes `ICopyEngine.BreakEven` exactly once with the correct buffer value.

Old `[Fact(Skip="NT8-runtime: CopyEngine.cctor requires NT8 host")]`
`TradeCopierPanel_BeBufferBox_FieldDoesNotExist` confirmed ABSENT (verifier SCAN-07e, lines 171-177).
Only a reference comment at line 9017 remains; no active Skip test.

**Verdict:** Confirmed by verifier SCAN-07f (lines 179-188). New [Fact] passes 1/1. PASS.

---

## C. Cross-File Jane Street DNA Violations

All checks applied to the 4 changed files. Source of truth: verifier artifact (all scans
independently re-run at Layer 3 with zero discrepancies vs. Layer 2).

| Rule | Severity | Check | Files Checked | Result |
|------|----------|-------|---------------|--------|
| JS-021 (no lock) | P0 | SCAN-01: `Select-String "lock\("` | All 4 | 0 matches. PASS |
| JS-001 (no throw in dispatch) | P0 | ExecuteBreakEven body: void, CYC=1, no throw | TradeCopierPanel.cs | PASS |
| JS-002 (no null return) | P0 | All new methods void; no return null | All 4 | PASS |
| JS-003 (no magic string) | P0 | No string discriminators in any new code | All 4 | PASS |
| JS-008 (struct/brush immutability) | P1 | No struct or SolidColorBrush in T-G1 changes | N/A | N/A |
| JS-009 (no plain Dictionary) | P1 | No new Dictionary in T-G1 changes | N/A | N/A |
| JS-010 (no public constructor on singleton) | P1 | CopyEngine constructor unchanged; no new signal struct | CopyEngine.cs | PASS |
| JS-013 (CYC <= 8) | P1 | SCAN-02: max CYC=5 across all new/changed methods | All 4 | PASS |
| JS-023 (Dispatcher for UI updates) | P0 | ExecuteBreakEven: no WPF/UI mutation | TradeCopierPanel.cs | N/A |
| ASCII-001 (ASCII-only) | P0 | SCAN-04: 0 bytes > 127 in all 4 files | All 4 | PASS |
| NT8-001 (no async in lifecycle) | P0 | SCAN-03: 0 executable async/await | All 4 | PASS |
| NT8-002 (Account.All outside Loaded) | P0 | No Account.All in new code | N/A | N/A |
| NT8-003 (PTT- prefix on CreateOrder) | P0 | No CreateOrder in new code | N/A | N/A |
| NT8-004 (no FontFamily) | P1 | No FontFamily in changed files | All 4 | PASS |
| NT8-005 (no hex color) | P1 | No #RRGGBB in changed files | All 4 | PASS |
| NT8-006 (DateTime.UtcNow) | P1 | No DateTime.Now in new code | All 4 | PASS |
| NT8-007/NT8-008 (ATM API) | P0 | SCAN-05: 0 ATM API calls | All 4 | PASS |

**Zero violations across all applicable rules.**

---

## D. Wiring Verification (Critical Gate)

**Check:** Does `DispatchShortcut` Key.B call `ExecuteBreakEven` (not `_engine.BreakEven` directly)?

**Evidence:** Verifier SCAN-07d (ticket-1-verification-lane-g.md lines 162-169):
```
case Key.B:
    ExecuteBreakEven(_engine, _leaderAccount, _instrument, _beBuffer);
    break;
```
`_engine.BreakEven(...)` direct call confirmed GONE. `ExecuteBreakEven(...)` confirmed PRESENT.

**Verdict:** WIRING CONFIRMED. PASS.

---

## E. Spec Requirements Coverage (DW-B26-01)

| Requirement | Plan Section | Implemented | Verified |
|-------------|-------------|-------------|----------|
| Upgrade reflection test (Option B) to behavioral test (Option A-partial) | Section 1, 2 | Yes — old Skip test deleted; new [Fact] added | CONFIRMED |
| Add `BreakEven` to `ICopyEngine` | P1 | Yes — PttContracts.cs:98-100 | CONFIRMED |
| `CopyEngine.BreakEven` access: internal → public | P2 | Yes — CopyEngine.cs:6871 | CONFIRMED |
| `ExecuteBreakEven` static helper (ICopyEngine param) | P4.1 | Yes — TradeCopierPanel.cs:3071-3075 | CONFIRMED |
| `DispatchShortcut` Key.B delegates to `ExecuteBreakEven` | P4.2 | Yes — TradeCopierPanel.cs:3064 | CONFIRMED |
| `LaneGTests` + `StubEngine : ICopyEngine` + `[Fact]` (no Skip) | T | Yes — CopyEngineTests.cs:8996-9027 | CONFIRMED |
| Old `[Fact(Skip=...)]` deleted | T explicit | Yes — lines 7735-7747 removed | CONFIRMED |
| Full Option A (WPF instantiation) documented as blocked | Section 2, plan note | Yes — NT8 STA dispatcher requirement documented | CONFIRMED |

**DW-B26-01: CLOSED.** All spec requirements fully addressed.

---

## F. All 7 Scans Aggregate Result

Scans run independently by L2 (engineer) and L3 (verifier). Zero discrepancies.

| Scan | L2 Result | L3 Result | Match | Verdict |
|------|-----------|-----------|-------|---------|
| SCAN-01 (lock) | 0 matches | 0 matches | YES | PASS |
| SCAN-02 (CYC) | max=5; all <= 8 | max=5; all <= 8 | YES | PASS |
| SCAN-03 (async/await) | 0 new executable | 0 executable in new code | YES | PASS |
| SCAN-04 (non-ASCII) | 0 chars | 0 bytes in all 4 files | YES | PASS |
| SCAN-05 (ATM API) | 0 calls | 0 calls | YES | PASS |
| SCAN-06 (build+test) | 0 errors, 179p/363s/542t | 0 errors, 179p/363s/542t | YES | PASS |
| SCAN-07 (structural) | 4 items confirmed | 7 sub-items (a-g) confirmed | YES | PASS |

**All 7 scans: ZERO violations across src/PropTraderTools/.**

---

## G. Test Count and Quality

### G.1 — Count Verification

| Metric | Baseline (Lane F) | Expected (Lane G) | Actual (Layer 3) | Match |
|--------|-------------------|-------------------|------------------|-------|
| Passed | 178 | 179 | 179 | YES |
| Skipped | 364 | 363 | 363 | YES |
| Failed | 0 | 0 | 0 | YES |
| Total | 542 | 542 | 542 | YES |

Net delta: −1 skip (old `[Fact(Skip=...)]` deleted), +1 pass (new `[Fact]`). Total unchanged.

### G.2 — Test Strength Comparison

| Dimension | Old Option B (`TradeCopierPanel_BeBufferBox_FieldDoesNotExist`) | New Option A-partial (`TradeCopierPanel_ExecuteBreakEven_PassesBufferToEngine`) |
|-----------|----------------------------------------------------------------|--------------------------------------------------------------------------------|
| Type | `[Fact(Skip=...)]` — never ran | `[Fact]` — runs in every CI pass |
| Assertion | Reflection field-absence: verifies `_beBufferBox` field did NOT exist | Behavioral: verifies `ICopyEngine.BreakEven` called once (`BreakEvenCallCount==1`) with correct buffer (`LastBuffer==3`) |
| Coverage | Zero coverage (skipped) | Exercises full dispatch path from `ExecuteBreakEven` through `ICopyEngine` seam |
| NT8 dependency | Blocked by NT8 cctor requirement | None — `StubEngine` receives `null` Account/Instrument, no NT8 type construction |
| Regression signal | None | Catches regressions in buffer forwarding and call routing |

**New test is demonstrably stronger.** The ticket reviewer's additive `BreakEvenCallCount==1`
assertion (stronger than plan's `LastBuffer==3` only) is an additive improvement with no
violation — explicitly accepted in ticket review. PASS.

---

## H. Build Health

- `dotnet build`: 0 errors. Pre-existing CS0436 warnings are known type-conflict warnings
  from `NinjaTrader.Custom` assembly reference. Not introduced by Lane G.
- `dotnet test`: 0 failures.
- `dotnet test --filter "FullyQualifiedName~LaneGTests"`: Passed 1/1 (not Skip).

---

## I. Architecture Conformance Summary

| Plan Item | Disposition |
|-----------|------------|
| P1 (ICopyEngine.BreakEven) | Implemented as planned. |
| P2 (CopyEngine.BreakEven internal → public) | Implemented as planned. |
| P3 (DROPPED — field type stays CopyEngine) | Correctly absent. _engine field type unchanged. |
| P4.1 (ExecuteBreakEven static helper) | Implemented as planned. |
| P4.2 (DispatchShortcut Key.B update) | Implemented as planned. |
| T (LaneGTests + StubEngine + [Fact]) | Implemented; StubEngine stronger than plan (BreakEvenCallCount added). No violation. |

Zero plan deviations. Zero phantom work.

---

## J. Violation Summary

**Zero violations found across all categories.**

No rule citation required.

---

## K. Deferred Work (Section K — MANDATORY)

### K.1 — Items CLOSED This Block

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-B26-01 | Reflection test upgrade (Option B → Option A): `TradeCopierPanel_BeBufferBox_FieldDoesNotExist` reflection-based absence assertion replaced by `TradeCopierPanel_ExecuteBreakEven_PassesBufferToEngine` [Fact] (no Skip) via ICopyEngine seam. Option A-partial achieved. Full Option A (WPF instantiation) remains blocked by NT8 STA dispatcher requirement — documented in plan Section 2. | P2 | B28 | **CLOSED** |

### K.2 — Prior OPEN Items Carried Forward (Status Unchanged)

All OPEN items from Lane F are carried forward without change.

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-B24-01 | NT8-043 formal rule entry: Confirm null-conditional event unsubscription (`?.Event -=`) causes silent runtime crash under NT8 Roslyn. SCAN-07 = 0 across all lanes. Rule watch continues. | P2 | B27 or future | OPEN |
| DW-B24-02 | Manual E2E Ctrl+Shift+B runtime verification: Press Ctrl+Shift+B on a solo account in live NT8. Confirm BreakEven fires and no NullReferenceException. Still pending. | P1 | B27 pre-release | OPEN |
| DW-BUG-0911-A-01 | Mid-close race window: `FlatGuardLoop` catches `qty == 0` definitively but does NOT catch the race where a close order is in-flight (`qty > 0`) when a second dispatch fires before the first close fills. Complete elimination requires an atomic interlocked per-account slot. | P2 | B27 or future | OPEN |
| DW-PTT0911B-01 | SIM end-to-end verification: Protocol documented (plan §15 Lane E). Manual NinjaTrader SIM session execution pending. Must complete before production release. | P1 | pre-live | OPEN |
| DW-PTT0911B-02 | Gate reset on AddOn reload: `_followerSlateReady` resets on CopyEngine re-instantiation; absent-key = gate-open safe default. Informational. | P2 | future | OPEN |
| DW-PTT0911B-03 | Accounts added after Subscribe(): accounts connected after AddOn load do not receive PositionUpdate subscription. Hardening: re-subscribe on `Account.AccountConnected`. | P2 | future | OPEN |
| DW-PTT0911B-04 | `HasAnyActiveOrders` snapshot timing: ToList() snapshot may include terminal orders not yet cleared; gate stays closed one extra PositionUpdate tick — conservative false-negative only. | P2 | future | OPEN |
| DW-LD-01 | `_laneDDispatched` session-unbounded growth: entries persist for session lifetime; no cleanup on session end or position close. Growth bounded (~2.4 KB/session for 4 followers). | P2 | future | OPEN |
| DW-LD-02 | Market order ATM guard decision documented: `TryLaneDHook` no-op for Market-type orders by design. Future Market-with-ATM requires separate overload or order type routing. | P2 | future | OPEN |
| DW-LD-05 | T_LANE_E_07/08 behavioral key-format assertion deferred: structural reflection only due to `TypeInitializationException` on NT8 `Account` type in xUnit host. Deferred to NT8 SIM session (subsumes DW-PTT0911B-01). | P2 | pre-live SIM | OPEN |

### K.3 — New Deferred Items (This Block)

No new deferred items were discovered or identified during Lane G.

---

## Final Verdict

**FINAL_PASS**

All checks pass:
- System coherence: 4 files form a complete, coherent Option A-partial solution. ICopyEngine +
  CopyEngine.BreakEven (public) + ExecuteBreakEven (static seam) + LaneGTests [Fact] are fully
  wired and mutually consistent.
- Cross-file JS violations: ZERO.
- Wiring: DispatchShortcut Key.B confirmed delegating to ExecuteBreakEven (not _engine.BreakEven
  directly).
- Spec requirement DW-B26-01: CLOSED.
- All 7 scans: ZERO violations (L2 and L3 independent, zero discrepancies).
- Test count: 179 passed / 363 skipped / 0 failed / 542 total. Exact match to plan prediction.
- Test strength: new [Fact] demonstrably stronger than deleted Option B reflection test.
- Section K: present (K.1 closed, K.2 carried forward, K.3 none new).
- 06-deferred-backlog.md: Lane G block appended (see companion write).

---

*ptt-plan-reviewer · PTT-DEFERRED-0911 Lane G · Phase 5 · 2026-09-11*
