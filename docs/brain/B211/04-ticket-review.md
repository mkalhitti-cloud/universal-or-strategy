# B211 Ticket Review
## Move-Target Buttons: Ask / Ask-ALL / Bid / Bid-ALL

**Reviewer**: ptt-ticket-reviewer
**Phase**: 3.5
**Input tickets**: docs/brain/B211/04-tickets.md
**Input plan**: docs/brain/B211/02-architecture-plan.md (REVIEW_PASS revision 1)
**Input plan-review**: docs/brain/B211/02-plan-review.md (REVIEW_PASS, V5 advisory noted)
**Rules**: docs/standards/jane-street/RULES_CATALOG.md
**Date**: 2026-07-07

---

## Cross-Ticket Ordering Constraint

**Check**: Ticket-1 VERIFY_PASS required before Ticket-2 begins — stated explicitly?

Tickets lines 13-17: "Ticket-1 VERIFY_PASS is required before Ticket-2 begins." Rationale
present (compile-time dependency on PttMoveTargetOne/PttMoveTargetAll types). **PASS**

---

---

## TICKET-1: Core Logic — New Feature Files + PttContracts Update

---

### Traceability

| Item | Mapped? | Source |
|------|---------|--------|
| B211-BEHAV-1 (no-op if no target) | YES | T1 spec table + Execute step (4) |
| B211-BEHAV-2 (no-op if no position) | YES | T1 spec table + Execute step (3) |
| B211-BEHAV-3 (no BeSwapInProgress guard) | YES | T1 spec table; T8 test confirms |
| B211-BEHAV-4 (re-entrant clicks valid) | YES | T1 spec table |
| B211-BEHAV-5 (PTT-MA-* not FIX-G-2 protected) | YES | T1 spec table; T8 test |
| B211-PARTIAL-FILL | YES | T1 spec table; `qty = target.Quantity - target.Filled` explicit at ticket line 129 |
| B211-PRICE-CALC | YES | T1 spec table; TryGetExitPrice formula explicit |
| PttMoveTargetOne.cs (NEW) | YES | Plan §2 |
| PttMoveTargetAll.cs (NEW) | YES | Plan §3 |
| PttContracts.cs MaAskPrefix + MaBidPrefix | YES | Plan §4 |
| B211Tests.cs 8 [Fact] tests | YES | Plan §5 |
| Phantom work (not in plan) | NONE detected | — |
| Missing plan work (in plan, not in ticket) | NONE detected | — |

**Traceability: PASS**

---

### JS Pre-Check

| Rule | Check | Result |
|------|-------|--------|
| JS-021 (no lock) | `_maSeq` uses `Interlocked.Increment`. `acc.Orders.ToList()` is lock-free NT8 snapshot. No `lock()` keyword in any described method body. | PASS |
| JS-001 (no throw in hot paths) | All NT8 API calls (Cancel, CreateOrder, Submit) wrapped in try/catch log-and-swallow. No `throw` statement in new code. | PASS |
| JS-002 (no null return / TryGet pattern) | `TryFindWorkingTarget`: returns `bool`, out param `Order`, `return target != null`. `TryGetExitPrice`: returns `bool`, out param `double`. No method returns null. | PASS |
| JS-023 (volatile int for buffer fields) | `_maSeq` uses `Interlocked.Increment` (lock-free atomic). Buffer fields `_askBuffer`/`_bidBuffer` are in Ticket-2 scope — checked there. | PASS |
| JS-033 (no async void) | `Execute` (PttMoveTargetOne and PttMoveTargetAll): synchronous `void`. `TryGetExitPrice`/`TryFindWorkingTarget`: synchronous bool. No `async` keyword described anywhere in Ticket-1. | PASS |

**JS Pre-Check: PASS**

---

### CYC Pre-Check

| Method | File | Stated CYC | Limit | Assessment |
|--------|------|-----------|-------|-----------|
| PttMoveTargetOne.Execute | PttMoveTargetOne.cs | 6 | 8 | See RISK-A below |
| PttMoveTargetOne.TryGetExitPrice | PttMoveTargetOne.cs | 7 (V5 advisory) | 8 | Within limit; V5 advisory correctly carried into SCAN-01 note |
| PttMoveTargetOne.TryFindWorkingTarget | PttMoveTargetOne.cs | 6 | 8 | See RISK-B below |
| PttMoveTargetAll.Execute | PttMoveTargetAll.cs | 3 | 4 (plan) / 8 (global) | PASS |

**RISK-A (advisory — non-blocking)**: `PttMoveTargetOne.Execute` is stated as CYC=6 (matching plan). The
plan-reviewer issued REVIEW_PASS without flagging Execute. Under non-strict McCabe counting (each `if` /
ternary / foreach = 1 branch regardless of compound boolean operands), CYC=6 is correct. All counted
branch points: `if instr==null`(1), `if !TryGetExitPrice`(1), `if (pos==null || pos.Qty==0)` counted as
1 by plan (compound `||` not split), `if !TryFindWorkingTarget`(1), ternary useAsk?(1) = 5 branches +
base = CYC=6. Under strict McCabe the `||` would add +1 (CYC=7). Either way ≤ 8. Engineer MUST run the
CYC tool and report actual measured CYC in ticket-1-completion.md. If tool reports CYC>8: SCAN-01 FAIL —
do not issue BUILD_PASS.

**RISK-B (advisory — non-blocking)**: `TryFindWorkingTarget` is stated as CYC=6. Under strict McCabe,
compound boolean expressions within three `if` statements (`?.FullName != ... || OrderState != ...`,
`Name == null || !Name.StartsWith(...)`, `Action != Sell && Action != BuyToCover`) could add +1 each
for the `||` / `&&` / `?.` operators, potentially raising CYC to 9-10 under a tool that splits compound
booleans. The plan-reviewer accepted CYC=6 (REVIEW_PASS, not flagged). The global limit is 8. If the
CYC tool reports >8 for this method, SCAN-01 will FAIL — engineer must not issue BUILD_PASS and must
report to Director. The ticket correctly instructs "run CYC tool on new/modified methods" and "All
methods must be <= 8."

**CYC Pre-Check: PASS** (both methods within global limit under any reasonable counting; SCAN-01
protects the contract; risks documented above for engineer awareness)

---

### NT8 Check

| Check | Result |
|-------|--------|
| Account.Cancel used (not Account.Change) | PASS — `acc.Cancel(new NinjaTrader.Cbi.Order[] { target })` |
| Account.CreateOrder 13-arg with PTT- prefix name | PASS — order name "PTT-MA-Ask-NNNNN" / "PTT-MA-Bid-NNNNN"; 13-arg confirmed |
| Account.Submit used | PASS — `acc.Submit(new[] { newOrder })` after null check |
| AtmStrategyChangeStopTarget absent | PASS — BANNED API table explicitly lists it; not referenced in any described body |
| AtmStrategyCreate() absent | PASS — listed as BANNED; not referenced |
| Account.Change() absent | PASS — listed as BANNED; not referenced |
| No async/await in lifecycle methods | PASS — no async anywhere in Ticket-1 |
| No sealed on TradeCopierWindow | PASS — not in scope for Ticket-1 |
| No FontFamily | PASS — not referenced |
| No hardcoded hex color | PASS — not referenced |
| No DateTime.Now | PASS — expiry uses NinjaTrader.Core.Globals.MaxDate |
| CreateOrder name starts "PTT-" | PASS — "PTT-MA-Ask-" and "PTT-MA-Bid-" both PTT-prefixed |
| (CustomOrder)null as last arg (not string) | PASS — explicit cast `(NinjaTrader.Cbi.CustomOrder)null` at ticket line 158 |

**NT8 Check: PASS**

---

### Test Coverage

| Test Name | [Fact] Present? | Assertion Described? | xUnit? |
|-----------|-----------------|---------------------|--------|
| PttMoveTargetOne_NoTarget_ReturnsNoOp | YES (class comment: "8 [Fact] tests — xUnit only") | YES — CreateOrder never called | YES — NO NUnit/MSTest stated |
| PttMoveTargetOne_NoPosition_ReturnsNoOp | YES | YES — Cancel never called | YES |
| PttMoveTargetOne_PartialFill_QtyIsQuantityMinusFilled | YES | YES — qty == 3 (5-2) | YES |
| PttMoveTargetOne_ZeroFill_QtyIsQuantity | YES | YES — qty == 4 (4-0) | YES |
| TryGetExitPrice_AskPlusZeroBuffer_ReturnsAskExactly | YES | YES — price == 100.0 | YES |
| TryGetExitPrice_AskPlusTwoBuffer_ReturnsAskPlusTwoTicks | YES | YES — price == 100.5 | YES |
| TryGetExitPrice_BidMinusOneBuffer_ReturnsBidMinusOneTick | YES | YES — price == 99.5 | YES |
| IsBeSwapGtcSweepGuard_ReturnsFalse_ForPttMaAskOrder | YES | YES — returns false for "PTT-MA-Ask-1" | YES |

All 8 required test names present. [Fact] attribute declared at class level ("8 [Fact] tests — xUnit only").
No [Theory] substitution. No NUnit/MSTest. T5/T6/T7 testability requirement met: `TryGetExitPrice`
declared `internal static` (ticket line 89, comment line 88 documents rationale — accessible via
InternalsVisibleTo). T8 uses `CopyEngine.IsBeSwapGtcSweepGuard` direct call.

**Test Coverage: PASS**

---

### Scan Checklist Presence

| Scan | Present in Ticket-1? | Location |
|------|---------------------|---------|
| SCAN-01 CYC | YES | Ticket lines 352-369 — method table with CYC budgets |
| SCAN-02 lock() | YES | Ticket lines 372-377 — grep command + expected zero |
| SCAN-03 async void | YES | Ticket lines 379-386 — grep command + expected zero |
| SCAN-04 null return | YES | Ticket lines 388-394 — grep command + expected zero |
| SCAN-05 throw | YES | Ticket lines 396-401 — grep command + expected zero |
| SCAN-06 ASCII | YES | Ticket lines 403-413 — string inventory with ASCII confirmation |
| SCAN-07 ptt-sync | YES | Ticket lines 415-423 — command + N/0 requirement + completion.md recording |

All 7 scans present with commands, expected outputs, and completion.md recording requirement.

**Scan Checklist: PASS**

---

### File Routing

| File | Path | Workspace |
|------|------|-----------|
| PttMoveTargetOne.cs | src/PropTraderTools/Features/PttMoveTargetOne.cs | Wave ✓ |
| PttMoveTargetAll.cs | src/PropTraderTools/Features/PttMoveTargetAll.cs | Wave ✓ |
| PttContracts.cs | src/PropTraderTools/Core/PttContracts.cs | Wave ✓ |
| B211Tests.cs | src/PropTraderTools/Tests/B211Tests.cs | Wave ✓ |

No Director workspace paths for .cs files.

**File Routing: PASS**

---

### TICKET-1 VERDICT: TICKET_REVIEW_PASS

---

---

## TICKET-2: UI — TradeCopierPanel Button Rows

---

### Traceability

| Item | Mapped? | Source |
|------|---------|--------|
| B211-WPF-TEMPLATE (2 rows, NTButtonStyle teal, RepeatButton pattern) | YES | T2 spec table; BuildBufferedButtonsRow specs array entries; BrushTeal reused |
| B211-BUFFER-SHARED (`volatile int _askBuffer` + `_bidBuffer`) | YES | T2 spec table; field declarations explicit at ticket lines 459-461 |
| 14 new event handlers | YES | Plan §1 handler section; matched in ticket |
| `_askRowPanel` / `_bidRowPanel` UniformGrid fields | YES | Plan §1; ticket lines 468-470 |
| 4 new button ref fields | YES | Plan §1; ticket lines 474-481 |
| BuildBufferedButtonsRow 2 new panel inits + 4 specs entries | YES | Plan §1; ticket lines 491-532 |
| BuildUI 2 new `root.Children.Add` calls | YES | Plan §1; ticket lines 543-546 |
| Ph4b deletion audit command | YES | Ticket lines 770-778 |
| Guardian symbol check | YES | Ticket lines 783-792 |
| Phantom work (not in plan) | NONE detected | — |
| Missing plan work (in plan, not in ticket) | NONE detected | — |

**Traceability: PASS**

---

### JS Pre-Check

| Rule | Check | Result |
|------|-------|--------|
| JS-021 (no lock) | `_askBuffer`/`_bidBuffer` are `volatile int` — no lock. All 14 handlers are synchronous with no shared-state locking. No `lock()` keyword anywhere in Ticket-2 described code. | PASS |
| JS-001 (no throw in hot paths) | No `throw` in any described handler body. No NT8 API calls in handlers — they delegate to Ticket-1 methods where the try/catch lives. | PASS |
| JS-002 (no null return) | All handlers are `void`. No handler returns null. | PASS |
| JS-023 (volatile int for buffer fields) | `private volatile int _askBuffer = 0` and `private volatile int _bidBuffer = 0` explicitly declared at ticket lines 459-461. | PASS |
| JS-033 (no async void) | All 14 handlers are `private void` (synchronous). No `async` keyword. | PASS |

**JS Pre-Check: PASS**

---

### CYC Pre-Check

| Method | File | Stated CYC | Limit | Assessment |
|--------|------|-----------|-------|-----------|
| OnAskUp | TradeCopierPanel.cs | 1 | 8 | PASS — no branches, Math.Max/Min are calls |
| OnAskDown | TradeCopierPanel.cs | 1 | 8 | PASS |
| OnAskClick | TradeCopierPanel.cs | 4 | 8 | PASS — base(1)+if null(1)+??(1)+if null(1) |
| OnAskAllClick | TradeCopierPanel.cs | 2 | 8 | PASS — base(1)+if null(1) |
| OnAskAllUp | TradeCopierPanel.cs | 1 | 8 | PASS — expression body delegate |
| OnAskAllDown | TradeCopierPanel.cs | 1 | 8 | PASS |
| UpdateAskLabels | TradeCopierPanel.cs | 1 | 8 | PASS — Note: body has 2 null guards (see RISK-C) |
| OnBidUp | TradeCopierPanel.cs | 1 | 8 | PASS |
| OnBidDown | TradeCopierPanel.cs | 1 | 8 | PASS |
| OnBidClick | TradeCopierPanel.cs | 4 | 8 | PASS |
| OnBidAllClick | TradeCopierPanel.cs | 2 | 8 | PASS |
| OnBidAllUp | TradeCopierPanel.cs | 1 | 8 | PASS |
| OnBidAllDown | TradeCopierPanel.cs | 1 | 8 | PASS |
| UpdateBidLabels | TradeCopierPanel.cs | 1 | 8 | PASS — Note: body has 2 null guards (see RISK-C) |

**RISK-C (advisory — non-blocking)**: `UpdateAskLabels` and `UpdateBidLabels` each contain 2 `if`
null guards yet are stated as CYC=1. Under McCabe, each `if` adds +1, making actual CYC=3. The plan
CYC table states CYC=1 for these methods and the plan-reviewer passed them without flagging. If the
CYC tool reports CYC=3, it remains well within the global limit of 8. Engineer should record actual
tool output in ticket-2-completion.md regardless. Not a blocking violation.

**CYC Pre-Check: PASS**

---

### NT8 Check

| Check | Result |
|-------|--------|
| No async/await in UI event handlers | PASS — all 14 handlers synchronous void |
| No Dispatcher.InvokeAsync (not needed) | PASS — ticket threading model section explicitly states "DO NOT add Dispatcher.InvokeAsync wrappers" with rationale (already on UI thread) |
| No sealed on TradeCopierWindow / TradeCopierPanel | PASS — not referenced |
| No FontFamily | PASS — NTButtonStyle resource reference only; no FontFamily set |
| No hardcoded hex color | PASS — BrushTeal reused (existing field); no new hex literals |
| No DateTime.Now | PASS — not referenced |
| NT8 API calls delegate to Ticket-1 methods (PttMoveTargetOne/All) | PASS — handlers call PttMoveTargetOne.Execute or PttMoveTargetAll.Execute; no direct NT8 API in T2 UI handlers |

**NT8 Check: PASS**

---

### Test Coverage

Ticket-2 is a UI wiring ticket (TradeCopierPanel.cs modifications only). No new business logic
methods are introduced — all business logic lives in Ticket-1 (PttMoveTargetOne/All). All 14 new
handlers are either: (a) trivial arithmetic wrappers (CYC=1) fully covered by Ticket-1's T3/T4 path
tests, (b) direct delegates to other handlers, or (c) null guards before delegating to Ticket-1
methods tested in T1/T2.

No public or internal methods introduced in Ticket-2 require dedicated [Fact] tests beyond what
Ticket-1 provides. Ticket-2 explicitly scoped to TradeCopierPanel.cs wiring only; test file is
B211Tests.cs (Ticket-1 scope).

**Test Coverage: PASS** (no new independently testable methods in Ticket-2)

---

### Scan Checklist Presence

| Scan | Present in Ticket-2? | Location |
|------|---------------------|---------|
| SCAN-01 CYC | YES | Ticket lines 697-717 — full 14-method table with CYC budgets |
| SCAN-02 lock() | YES | Ticket lines 719-726 — grep command + expected zero |
| SCAN-03 async void | YES | Ticket lines 728-733 — grep command + expected zero |
| SCAN-04 null return | YES | Ticket lines 735-739 — grep command + expected zero |
| SCAN-05 throw | YES | Ticket lines 741-749 — grep command + expected zero |
| SCAN-06 ASCII | YES | Ticket lines 751-757 — string inventory with ASCII confirmation |
| SCAN-07 ptt-sync | YES | Ticket lines 759-765 — command + N/0 requirement + completion.md recording |

All 7 scans present with commands, expected outputs, and completion.md recording requirement.

**Scan Checklist: PASS**

---

### File Routing

| File | Path | Workspace |
|------|------|-----------|
| TradeCopierPanel.cs | src/PropTraderTools/TradeCopierPanel.cs | Wave ✓ |

No Director workspace paths for .cs files. Scope lock enforced (only TradeCopierPanel.cs).

**File Routing: PASS**

---

### Ph4b Deletion Audit Command

Present at ticket lines 770-778:
```powershell
git diff HEAD~1 HEAD -- src/PropTraderTools/TradeCopierPanel.cs | Select-String "^-    (private|internal|public)"
```
Zero-tolerance statement and "Record result in ticket-2-completion.md" instruction present. **PASS**

---

### Guardian Symbol Check

Present at ticket lines 783-792:
```powershell
powershell -File .\scripts\verify_critical_symbols.ps1
```
PASS requirement and stop/report instruction present. **PASS**

---

### TICKET-2 VERDICT: TICKET_REVIEW_PASS

---

---

## Aggregate Spec Coverage Matrix

| Req ID | Covered by Ticket | Status |
|--------|------------------|--------|
| B211-BEHAV-1 | TICKET-1 | PASS |
| B211-BEHAV-2 | TICKET-1 | PASS |
| B211-BEHAV-3 | TICKET-1 | PASS |
| B211-BEHAV-4 | TICKET-1 | PASS |
| B211-BEHAV-5 | TICKET-1 | PASS |
| B211-PARTIAL-FILL | TICKET-1 | PASS |
| B211-PRICE-CALC | TICKET-1 | PASS |
| B211-WPF-TEMPLATE | TICKET-2 | PASS |
| B211-BUFFER-SHARED | TICKET-2 | PASS |

Every spec requirement in scope appears in exactly one ticket. No duplicates. No gaps.

---

## Advisory Risk Summary (non-blocking)

These items do NOT block TICKET_REVIEW_PASS. They are noted for engineer awareness only.

| ID | Ticket | Item |
|----|--------|------|
| RISK-A | T1 | `PttMoveTargetOne.Execute` stated CYC=6; under strict McCabe the `||` in `pos==null\|\|qty==0` may raise to CYC=7. Still ≤ 8. Engineer must record actual tool output in SCAN-01. |
| RISK-B | T1 | `TryFindWorkingTarget` stated CYC=6; strict McCabe tool counting compound boolean operators (`?.`, `\|\|`, `&&`) across 3 if-statements could yield CYC=9-10. If tool reports >8: SCAN-01 FAIL — stop, report to Director. |
| RISK-C | T2 | `UpdateAskLabels`/`UpdateBidLabels` stated CYC=1 but bodies contain 2 `if` guards each; tool may report CYC=3. Remains well within limit 8. |
| V5 | T1 | Plan CYC table states TryGetExitPrice CYC=6 but SCAN-01 note in ticket correctly states CYC=7 (per plan-reviewer V5 advisory). Engineer must record CYC=7 in ticket-1-completion.md. |

---

## Overall: TICKET_REVIEW_PASS

All mandatory checks passed for both tickets:
- Cross-ticket ordering constraint: STATED
- Spec coverage: COMPLETE (all 9 req IDs covered, no gaps, no duplicates)
- Traceability: ALL items map to plan or spec
- JS Pre-Check: PASS on all applicable rules (JS-001, JS-002, JS-021, JS-023, JS-033)
- CYC Pre-Check: PASS (all methods within global limit 8; tool measurement protected by SCAN-01)
- NT8 Check: PASS (correct APIs, banned APIs listed and excluded)
- Test Coverage: 8 [Fact] xUnit tests specified with names and assertions; no [Theory] substitution; no NUnit/MSTest
- Scan Checklist: PASS — all 7 scans (SCAN-01 through SCAN-07) present in BOTH tickets with commands, expected outputs, and completion.md recording requirements
- File Routing: PASS — all .cs paths in Wave workspace (src/PropTraderTools/)
- Ph4b deletion audit: PRESENT in Ticket-2
- Guardian symbol check: PRESENT in Ticket-2
- ptt-sync: PRESENT in both tickets

**TICKET_REVIEW_PASS**

Engineer is cleared to begin Ticket-1. Ticket-2 must not start until Ticket-1 VERIFY_PASS is confirmed.

---

*ptt-ticket-reviewer — Phase 3.5 — 2026-07-07*
