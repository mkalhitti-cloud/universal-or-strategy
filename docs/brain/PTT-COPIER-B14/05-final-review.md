# PTT-COPIER-B14 Final Review
# Phase: 5 (ptt-plan-reviewer)
# Date: 2026-07-14
# Reviewer: ptt-plan-reviewer
# Source files verified (Wave workspace):
#   - c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs
#   - c:\WSGTA\universal-or-strategy\src\PropTraderTools\TradeCopierPanel.cs
#   - c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngineTests.cs
# Brain artifacts read:
#   - docs/brain/PTT-COPIER-B14/02-architecture-plan.md (PLAN_COMPLETE)
#   - docs/brain/PTT-COPIER-B14/02-plan-review.md (REVIEW_PASS)
#   - docs/brain/PTT-COPIER-B14/04-tickets.md (TICKETS_COMPLETE)
#   - docs/brain/PTT-COPIER-B14/04-ticket-review.md (TICKET_REVIEW_PASS)
#   - docs/brain/PTT-COPIER-B14/ticket-1-completion.md (BUILD_PASS)
#   - docs/brain/PTT-COPIER-B14/ticket-1-verification.md (VERIFY_PASS)
#   - docs/brain/PTT-COPIER-B14/ticket-2-completion.md (BUILD_PASS)
#   - docs/brain/PTT-COPIER-B14/ticket-2-verification.md (VERIFY_PASS)
#   - docs/brain/PTT-COPIER-B13/06-deferred-backlog.md (prior open items)

---

## Section A — Spec Coverage

| Requirement | Source | Addressed? | Verified? | Notes |
|-------------|--------|------------|-----------|-------|
| DW-B12-DEFER-02 (original): Auto-trail stop from BE CONNECTED state | B13 §Open Items for B14 | YES — T1 | VERIFY_PASS | ArmTrailBe / DisarmTrailBe / OnTrailBeAccountUpdate all present in CopyEngine.cs at lines 1287–1348. All 3 TradeCopierPanel wiring points confirmed (OnBeConnected:761, OnBeClick:713, Detach:303). |
| DW-B12-DEFER-04: Align CopyEngineTests.cs test names with B12 §T1 §1.10 contract | B13 §Open Items for B14 | YES — T2 | VERIFY_PASS | 5/5 contract names present in file. 4/4 old names absent. 1 new short-direction test added. |
| Scope gate: no items beyond the 2 in-scope items | Plan §1 scope table | YES | PASS | Shelved items (DW-B9-01, DW-B9-03, DW-B12-DEFER-01 original, DW-B8-04) were not touched. |

**Section A verdict: PASS**

---

## Section B — Cross-File Coherence

### B.1 CopyEngine.cs exports `ArmTrailBe` and `DisarmTrailBe` as `internal`

- `ArmTrailBe`: `internal void ArmTrailBe(Instrument instr, Account masterAcc, int bufferTicks)` — confirmed at line 1287.
- `DisarmTrailBe`: `internal void DisarmTrailBe()` — confirmed at line 1310.
- Both are `internal` — accessible from `TradeCopierPanel` in the same `NinjaTrader.NinjaScript.AddOns` namespace.

**Verdict: PASS**

### B.2 TradeCopierPanel.cs calls both methods at correct lifecycle points

| Call Site | Method | Line | Trigger |
|-----------|--------|------|---------|
| `OnBeConnected` | `ArmTrailBe(_instrument, _leaderAccount, _beBuffer)` | 761 | BE FSM ARMED→CONNECTED (triggered by PendingBeFired event, dispatched to UI thread) |
| `OnBeClick` Connected→Idle | `DisarmTrailBe()` | 713 | User manually resets BE from CONNECTED state |
| `Detach()` | `DisarmTrailBe()` | 303 | Panel teardown — guards dangling subscription |

All 3 call sites confirmed in live source. Verified independently by ptt-verifier in ticket-1-verification.md.

**Verdict: PASS**

### B.3 No orphaned subscriptions possible

- `ArmTrailBe` subscribes `masterAcc.AccountItemUpdate += OnTrailBeAccountUpdate` and sets `_trailBeState = 1` (release fence) at line 1302–1303.
- `DisarmTrailBe` uses `Interlocked.CompareExchange(ref _trailBeState, 0, 1)` to atomically disarm, then unsubscribes at line 1316. Idempotent — CAS guard prevents double-unsubscribe.
- Exit paths from CONNECTED state that call `DisarmTrailBe`:
  1. User clicks BE (Connected→Idle) — `OnBeClick` case at line 713.
  2. Panel detach — `Detach()` at line 303.
- No other code path can reach CONNECTED without going through the BE FSM. No orphan possible.

**Note on Detach() ordering**: The verifier noted that `DisarmTrailBe()` in `Detach()` is placed at line 303, **after** the follower-item `foreach` loop (lines 299–301), rather than before it as specified in ticket §1.7. This is a non-functional deviation — `DisarmTrailBe()` is idempotent, and the `AccountItemUpdate` unsubscription for the trail watcher is independent of follower-account cleanup. No bug risk. Not a rule violation.

**Verdict: PASS**

---

## Section C — All 7 Cross-File Scans

Scans run on `CopyEngine.cs`, `TradeCopierPanel.cs`, and `CopyEngineTests.cs` in the Wave workspace.

### SCAN-01: `lock(` in B14 new code — JS-021 P0

Grep result: 4 matches found, **ALL in comment text** (lines 319, 562, 793, 1197 of CopyEngine.cs).
Zero matches in live code. Zero matches in B14 new methods (lines 1287–1348).

**Result: 0 lock() in live/new code. PASS.**

### SCAN-02: `async void` in new methods — JS-033 P0

Grep result: **0 matches** across all three files.
`OnTrailBeAccountUpdate` confirmed as `private void` (line 1329, CopyEngine.cs).
`OnBeConnected` confirmed as `private void` (line 752, TradeCopierPanel.cs) — NOT `async void`.

**Result: 0 async void. PASS.**

### SCAN-03: `return null` in B14 new methods — JS-002 P0

Grep result: 12 `return null;` matches found across PropTraderTools. **All 12 are in pre-existing (pre-B14) methods**:
- CopyEngine.cs lines 647, 1038, 1044, 1097 — pre-existing methods (FindLimitOrder, FindPosition, FindDominantOrder, etc.)
- TradeCopierWindow.cs lines 742, 744 — pre-existing
- TradeCopierAddOn.cs lines 257, 259, 503, 512, 518, 527 — pre-existing

**Zero `return null` in any B14 new methods** (ArmTrailBe, DisarmTrailBe, OnTrailBeAccountUpdate, OnBeConnected, Detach, all 7 new tests). All B14 guard exits use bare `return;`.

**Result: 0 return null in B14 code. PASS.**

### SCAN-04: CYC ≤ 8 — all new/modified methods

Independent reviewer counts from ticket-1-verification.md (Layer 3) confirmed:

| Method | File | Decision Points | CYC | Limit | Status |
|--------|------|----------------|-----|-------|--------|
| `ArmTrailBe` | CopyEngine.cs | instr null(1), acc null(2), IsFlat(3), MinValue(4) | **4** | 8 | PASS |
| `DisarmTrailBe` | CopyEngine.cs | CAS check(1), acc null(2) | **2** | 8 | PASS |
| `OnTrailBeAccountUpdate` | CopyEngine.cs | state(1), item(2), pnl(3), CAS(4), instr(5) | **5** | 8 | PASS |
| `OnBeConnected` | TradeCopierPanel.cs | beBtn2(1), instrument(2), leaderAccount(3) | **3** | 8 | PASS |
| `OnBeClick` Connected case | TradeCopierPanel.cs | unchanged from B12 | **5** | 8 | PASS |
| `Detach()` | TradeCopierPanel.cs | unchanged | **2** | 8 | PASS |
| 6 new T1 tests | CopyEngineTests.cs | linear flow | **1 each** | 8 | PASS |
| 4 renamed T2 tests | CopyEngineTests.cs | renames only | **1 each** | 8 | PASS |
| 1 new T2 test | CopyEngineTests.cs | linear flow | **1** | 8 | PASS |

**Result: All CYC ≤ 8. PASS.**

### SCAN-05: `volatile double` absent; `volatile long` present — NT8-003

- `volatile double` grep: 4 matches — all in **comment text only** ("volatile double banned"). Zero in live field declarations.
- `volatile long` grep: 1 match in live field — `private volatile long _trailBeLastPnl = 0L;` at CopyEngine.cs line 107. Correct.

**Result: 0 volatile double in live code; volatile long confirmed. PASS.**

### SCAN-06: `Math.Clamp` absent — NT8-034

Grep result: **0 matches** across CopyEngine.cs, TradeCopierPanel.cs, CopyEngineTests.cs new methods. (All existing TradeCopierPanel.cs `Math.Max/Min` patterns are pre-B14 and already compliant with NT8-034.)

**Result: 0 Math.Clamp. PASS.**

### SCAN-07: 5 contract names present; 4 old names absent — DW-B12-DEFER-04 contract

**5 contract names present** (grep: 5/5 confirmed):
- `Flatten_LimitOverload_LongPosition_EmitsSellLimitFullQty` — line 1318
- `Flatten_LimitOverload_ShortPosition_EmitsBuyToCoverLimitFullQty` — line 1344
- `Trim_LimitOverload_LongPosition_EmitsSellLimitAtRefPlusTick` — line 1364
- `Trim_LimitOverload_ShortPosition_EmitsBuyToCoverLimitAtRefMinusTick` — line 1392
- `DispatchCopy_PttPrefixGate_SkipsOrderNamedPttTrimLimit` — line 1421

**4 old names absent** (grep: 0/4 — none found):
- `Trim_LongPosition_EmitsLimitSellAtBidPlusBuffer` — absent
- `Flatten_LongPosition_EmitsLimitSellAtBidPlusBuffer` — absent
- `Flatten_ShortPosition_EmitsLimitBuyAtAskMinusBuffer` — absent
- `PttPrefixGate_SkipsDispatchForPttOrders` — absent

**Result: 5/5 contract names present, 4/4 old names absent. PASS.**

**Section C verdict: ALL 7 SCANS PASS.**

---

## Section D — NT8 Constraint Cross-File Audit

| Rule | Check | Files | Result |
|------|-------|-------|--------|
| NT8-003 | `volatile double` absent in live field declarations | CopyEngine.cs | PASS — `_trailBeLastPnl` is `volatile long`; BitConverter encoding used throughout (lines 1299, 1336, 1340, 1341) |
| NT8-018 | No `lock()` in any B14 method | CopyEngine.cs, TradeCopierPanel.cs | PASS — Interlocked.CompareExchange, Interlocked.Read, Interlocked.Increment only |
| NT8-019 | `OnTrailBeAccountUpdate` is plain `void` (not async void) | CopyEngine.cs | PASS — confirmed `private void` at line 1329 |
| NT8-026 | No `order.TrailPrice` reference | CopyEngine.cs | PASS — trail advances via `BreakEven(instr, newBuffer)` + `acc.Change()` internally; no TrailPrice field reference |
| NT8-031 | `using System.Threading` present for Interlocked | CopyEngine.cs | PASS — pre-existing (B10 T2). New test file uses `System.Threading.Interlocked` qualified form at line 1655. |
| NT8-034 | No `Math.Clamp` in new B14 code | CopyEngine.cs, TradeCopierPanel.cs | PASS — 0 Math.Clamp in new methods |

**Section D verdict: PASS — all NT8 constraints satisfied.**

---

## Section E — JS Rule Cross-File Audit

| Rule | Severity | Check | Result |
|------|----------|-------|--------|
| JS-021 (no lock()) | P0 | All new B14 methods in CopyEngine.cs and TradeCopierPanel.cs — zero `lock(` in live code | PASS |
| JS-001 (no throw in hot paths) | P0 | `OnTrailBeAccountUpdate` has no try/catch of its own and no `throw` statement. `BreakEven()` wraps `acc.Change()` internally. | PASS |
| JS-002 (no return null) | P0 | Zero `return null;` in any B14 new method. All guard exits use bare `return;`. | PASS |
| JS-023 (cross-thread fields volatile) | P1 | `_trailBeState` (volatile int), `_trailBeBufferTicks` (volatile int), `_trailBeLastPnl` (volatile long) — all confirmed volatile. Plain refs `_trailBeAccount` / `_trailBeInstrument` are single-writer UI thread, protected by `_trailBeState = 1` release fence. | PASS |
| JS-033 (no async void except event handlers) | P0 | `OnTrailBeAccountUpdate` is plain `void`. `OnBeConnected` is plain `void` (not async void) invoked via `Dispatcher.InvokeAsync`. No new `async void` methods introduced in B14. | PASS |
| JS-008 (SolidColorBrush.Freeze()) | P1 | No new brushes in B14. Existing `BrushConnected` (B12) already frozen via `MakeBrush()`. | PASS |

**Section E verdict: PASS — all JS rules satisfied.**

---

## Section F — Deferred Work Audit

| ID | Status entering B14 | B14 Action | Closed by | Notes |
|----|--------------------|-----------:|-----------|-------|
| DW-B12-DEFER-02 (original) | OPEN (B13 backlog) | CLOSED | T1 VERIFY_PASS | ArmTrailBe / DisarmTrailBe / OnTrailBeAccountUpdate implemented and independently verified. |
| DW-B12-DEFER-04 | OPEN (B13 backlog) | CLOSED | T2 VERIFY_PASS | 4 renames + 1 new test. 5/5 contract names present. 4/4 old names absent. |
| DW-B9-01 | SHELVED | Carry forward | — | No canvas drawing scope in B14. |
| DW-B9-03 | SHELVED (BLOCKED on DW-B8-04) | Carry forward | — | Prerequisite unresolved. |
| DW-B12-DEFER-01 (original) | SHELVED | Carry forward | — | No panel expansion scope in B14. |
| DW-B8-04 | OPEN (B13 backlog) | Carry forward | — | No change in B14 scope. |

**New deferred items discovered during B14 review**: None.

**Section F verdict: PASS — all entering deferred items correctly accounted for.**

---

## Section K — Deferred Work (MANDATORY)

### B14 Items Closed This Block

| ID | Item | Closed By |
|----|------|-----------|
| DW-B12-DEFER-02 (original) | Auto-trail stop from BE CONNECTED state | T1 VERIFY_PASS |
| DW-B12-DEFER-04 | Align CopyEngineTests.cs test names to B12 §T1 §1.10 contract | T2 VERIFY_PASS |

### Running Deferred Work Ledger (B10 onwards)

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-B10-01 | Remove BuildDiagRow / OnDiagGap001d / OnDiagGap002 scaffolding | P2 | B11 | CLOSED (B11 T1) |
| DW-B10-02 | Add 3 missing AtrSizingEngine xUnit tests | P1 | B11 | CLOSED (B11 T2) |
| DW-B10-03 | TradeCopierWindow.cs Arm BE column | P2 | B11 | CLOSED (B11 T2) |
| DW-B10-04 | Update NT8_ADDON_KNOWLEDGE.md with T4 chart attachment result | P1 | B11 | CLOSED (B11 T1) |
| DW-B8-04 | Fix click trader price lookup (Y-to-price axis conversion stub) | P2 | B15+ | OPEN |
| DW-B9-01 | ATR box visualization on chart canvas (carry from B9–B14, shelved) | P2 | B15+ | OPEN |
| DW-B9-03 | Click trader Bid+1/Ask-1 auto-offset — BLOCKED on DW-B8-04 | P3 | B15+ | OPEN (BLOCKED) |
| DW-B11-DEFER-01 | Flatten/Trim keyboard shortcuts to Limit orders | P1 | B12 | CLOSED (B12 T1) |
| DW-B12-DEFER-01 | Wire GetRefPrice() to _instrument.MarketData.Last.Price | P1 | B13 | CLOSED (B13 T1) |
| DW-B12-DEFER-02 | ATR fraction spinner startup sync (NotifyRiskChanged + NotifyAtrFractionChanged) | P2 | B13 | CLOSED (B13 T2) |
| DW-B12-DEFER-03 | Correct Math.Clamp comment attribution; add NT8-034 rule | P3 | B13 | CLOSED (B13 T3) |
| DW-B12-DEFER-01 (original) | Full-panel mode expansion: Buy Ask / Sell Bid quick-entry buttons | P2 | B15+ | OPEN |
| DW-B12-DEFER-02 (original) | Auto-trail stop from BE CONNECTED state | P3 | B14 | **CLOSED (B14 T1)** |
| DW-B12-DEFER-04 | Align CopyEngineTests.cs test names to B12 §T1 §1.10 contract | P3 | B14 | **CLOSED (B14 T2)** |

### Open Items for B15

| ID | Description | Priority |
|----|-------------|----------|
| DW-B8-04 | Fix click trader price lookup — replace hardcoded 0.0 stub in OnChartMouseDown | P2 |
| DW-B9-01 | ATR box visualization on chart canvas (carry from B9/B13/B14 — shelved) | P2 |
| DW-B9-03 | Click trader Bid+1/Ask-1 auto-offset — BLOCKED on DW-B8-04 | P3 |
| DW-B12-DEFER-01 (original) | Full-panel mode expansion: Buy Ask / Sell Bid quick-entry buttons | P2 |

**Total open items for B15: 4**

---

## Violations Log

**No violations found.**

| Rule | Status | Notes |
|------|--------|-------|
| JS-021 (no lock()) | PASS | 4 comment-only matches; 0 in live code across all B14 methods |
| JS-001 (no throw in hot paths) | PASS | OnTrailBeAccountUpdate has no throw; BreakEven wraps internally |
| JS-002 (no return null) | PASS | 0 return null in B14 new methods; 12 pre-existing occurrences untouched |
| JS-023 (volatile cross-thread) | PASS | All 3 volatile fields confirmed; plain refs protected by release fence |
| JS-033 (no async void) | PASS | All B14 methods are plain void; no async void introduced |
| JS-008 (brush freeze) | PASS | No new brushes |
| NT8-003 (volatile double ban) | PASS | volatile long + BitConverter confirmed |
| NT8-018 (lock ban) | PASS | Interlocked only |
| NT8-019 (async void ban) | PASS | OnTrailBeAccountUpdate is plain void |
| NT8-026 (no TrailPrice) | PASS | Not used |
| NT8-031 (using System.Threading) | PASS | Pre-existing; test uses fully-qualified form |
| NT8-034 (Math.Clamp ban) | PASS | 0 Math.Clamp in B14 code |
| CYC ≤ 8 | PASS | Maximum CYC = 5 (OnTrailBeAccountUpdate) |

---

## Pipeline Completeness Check

| Phase | Artifact | Status |
|-------|----------|--------|
| Phase 1 (ptt-architect) | 02-architecture-plan.md | PLAN_COMPLETE |
| Phase 2 (ptt-plan-reviewer) | 02-plan-review.md | REVIEW_PASS |
| Phase 3 (ptt-architect) | 04-tickets.md | TICKETS_COMPLETE |
| Phase 3.5 (ptt-ticket-reviewer) | 04-ticket-review.md | TICKET_REVIEW_PASS (T1 + T2) |
| Phase 4a T1 (ptt-engineer) | ticket-1-completion.md | BUILD_PASS |
| Phase 4b T1 (ptt-verifier) | ticket-1-verification.md | VERIFY_PASS |
| Phase 4a T2 (ptt-engineer) | ticket-2-completion.md | BUILD_PASS |
| Phase 4b T2 (ptt-verifier) | ticket-2-verification.md | VERIFY_PASS |
| Phase 5 (ptt-plan-reviewer) | 05-final-review.md | THIS FILE |
| Phase 5 (ptt-plan-reviewer) | 06-deferred-backlog.md | WRITTEN (see companion file) |

---

## Final Verdict

```
FINAL_PASS
```

**Block**: PTT-COPIER-B14
**Tickets**: 2/2 — T1 VERIFY_PASS, T2 VERIFY_PASS
**Spec requirements closed**: 2/2 — DW-B12-DEFER-02 (original), DW-B12-DEFER-04
**Cross-file scans**: 7/7 PASS
**JS rule violations**: 0
**NT8 constraint violations**: 0
**CYC > 8 violations**: 0
**Orphaned subscriptions**: 0 — all DisarmTrailBe exit paths verified
**Section K**: Written (running ledger updated, 4 open items carry to B15)
**06-deferred-backlog.md**: WRITTEN (pipeline gate satisfied)

The CopyEngine + TradeCopierPanel + CopyEngineTests form a coherent, complete system for the B14 scope. The auto-trail BE watcher is correctly armed on CONNECTED, disarmed on all exit paths (user click, panel detach), and uses the idiomatic lock-free release-fence pattern that mirrors the existing ArmPendingBe/DisarmPendingBe protocol. Test name alignment is complete per the B12 §T1 §1.10 contract.
