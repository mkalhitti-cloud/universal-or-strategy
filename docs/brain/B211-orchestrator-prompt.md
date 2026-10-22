# B211 Orchestrator Prompt
# PTT-COPIER-B211: Move Target to Ask / Move Target to Bid (PTT-MA)
# Generated: 2026-07-07 | Director approved

---

## CONTEXT (do not re-investigate)

New feature: two rows of buttons — Ask / Ask-ALL and Bid / Bid-ALL — that move
the current open position's profit target order to the current live Ask or Bid
price (plus a tick buffer). Each button is an identical copy of the BE row WPF
template: NTButtonStyle teal, 2-column UniformGrid, own ▲▼ RepeatButtons,
volatile int buffer, FormatBuffer() label. The Ask/Bid rows sit directly below
the BE / BE-ALL row in the panel.

Full spec: specs/002-trade-copier-spec.html, section B211.
Key spec sections to read before Ph1:
  - Architecture (BE/BE-ALL as building blocks)
  - Trading Intent (Bid = primary long exit, Ask = primary short exit)
  - WPF Implementation Template (buffer fields, price calc, handlers)
  - Partial Fill Behaviour (Quantity - Filled formula — P0 if missed)
  - Behaviour Rules (5 rules: no-op conditions, no BeSwapInProgress guard, GTC sweep)
  - NT8 API Pattern (Account.Cancel + Account.CreateOrder + Submit — AddOnBase)
  - Deferred Work DW-B211-01 through DW-B211-05

Git SHA at time of writing: main HEAD (B209 pipeline complete, B210 SIM pending)

---

## PIPELINE — MANDATORY ALL 5 PHASES, NONE SKIPPABLE, NONE COMBINABLE

Ph1  ptt-architect       -> docs/brain/B211/02-architecture-plan.md
Ph2  ptt-plan-reviewer   -> docs/brain/B211/02-plan-review.md       (REVIEW_PASS gate)
Ph3  ptt-architect       -> docs/brain/B211/04-tickets.md
Ph3.5 ptt-ticket-reviewer -> docs/brain/B211/04-ticket-review.md   (TICKET_REVIEW_PASS gate)
Ph4a ptt-engineer        -> src .cs edits + docs/brain/B211/ticket-N-completion.md
Ph4b ptt-verifier        -> docs/brain/B211/ticket-N-verification.md (VERIFY_PASS gate)
Ph5  ptt-plan-reviewer   -> docs/brain/B211/05-final-review.md + docs/brain/B211/06-deferred-backlog.md

No shortcuts. No direct edits. No phase skipping. No phase combining.

---

## TASK BRIEF FOR ptt-architect (Ph1)

### Block number
B211

### Problem statement
Add two new button rows to the TradeCopierPanel ChartTrader row:
  Row 1 (Ask): [ Ask +0 [▲][▼] ]  [ Ask-ALL +0 [▲][▼] ]
  Row 2 (Bid): [ Bid +0 [▲][▼] ]  [ Bid-ALL +0 [▲][▼] ]

Each button cancels the current Working target (Limit exit order) and submits
a new Limit order at the current Ask or Bid price plus buffer ticks.

Primary intent: "better price than a market order with near-certain fill."
  - Long exit: Bid is primary (fills at Bid or better, buyer in book now)
  - Short exit: Ask is primary (fills at Ask or better, seller in book now)
  - Buffer = 0 default (snap to ask/bid exactly); ▲▼ adjusts in ticks

### Files in scope
- src/PropTraderTools/TradeCopierPanel.cs
    Add 2 new RowDefinitions. Build 2 UniformGrid rows (Columns=2 each).
    Wire 8 handlers: OnAskClick, OnAskUp, OnAskDown, OnAskAllClick, OnAskAllUp,
    OnAskAllDown, OnBidClick, OnBidUp, OnBidDown, OnBidAllClick, OnBidAllUp,
    OnBidAllDown.
    Add volatile int _askBuffer = 0, _bidBuffer = 0 (JS-023).
    Use NTButtonStyle for all buttons (same teal as existing rows).
    Use RepeatButton Delay=400ms Interval=100ms (same as existing spinners).
    Use FormatBuffer() to label buttons (same as BE row).

- src/PropTraderTools/Features/PttMoveTargetOne.cs   (NEW FILE)
    Mirrors PttBeSwapOne.cs structure.
    Execute(Account acc, Instrument instr, bool useAsk, int bufferTicks):
      1. Read price: Ask or Bid + (bufferTicks * tickSize)
      2. Find existing Working Limit exit order on acc/instr
         (scan acc.Orders for OrderState.Working, OrderType.Limit, matching instr)
      3. No-op if no Working target found (log [PTT-MA] no target found)
      4. No-op if no open position (log [PTT-MA] no position)
      5. qty = existingTarget.Quantity - existingTarget.Filled  (partial fill safe)
      6. Cancel existing: acc.Cancel(new Order[] { existingTarget })
      7. Submit new Limit: acc.CreateOrder(...) + Submit()
         Order name: "PTT-MA-Ask-{N}" or "PTT-MA-Bid-{N}"
    CYC target <= 6. JS-021: no lock. JS-001: no throw. ASCII-only.

- src/PropTraderTools/Features/PttMoveTargetAll.cs   (NEW FILE)
    Mirrors PttBeSwapAll.cs structure.
    Execute(Instrument instr, bool useAsk, int bufferTicks):
      Iterates AllAccounts() (reused — do NOT re-implement).
      Calls PttMoveTargetOne.Execute(acc, instr, useAsk, bufferTicks) per account.
    CYC target <= 4. Same JS rules.

- src/PropTraderTools/PttContracts.cs
    Add constants: PttOrderNames.MaAskPrefix = "PTT-MA-Ask-"
                   PttOrderNames.MaBidPrefix = "PTT-MA-Bid-"

- src/PropTraderTools/Tests/B211Tests.cs   (NEW FILE)
    xUnit [Fact] only. Testable seams. Minimum 8 tests covering:
    1. PttMoveTargetOne: no target -> no-op (returns without submitting)
    2. PttMoveTargetOne: no position -> no-op
    3. PttMoveTargetOne: partial fill -> qty = Quantity - Filled (not Quantity)
    4. PttMoveTargetOne: zero fill -> qty = Quantity (Quantity - 0)
    5. Price calc Ask + 0 buffer -> Ask exactly
    6. Price calc Ask + 2 buffer -> Ask + 2 ticks
    7. Price calc Bid - 1 buffer -> Bid - 1 tick
    8. PTT-MA-* name NOT protected by FIX-G-2 guard (IsBeSwapGtcSweepGuard returns false for "PTT-MA-Ask-1")

### Ph1 architect decisions required
- DW-B211-03: canonical target order identification when multiple Working
  Limit exit orders exist simultaneously (e.g. PTT-BE-Target-N from active
  BE swap + prior PTT-MA-Ask-N). Recommend: take the most recently submitted
  Working Limit order with an exit action on the instrument. Confirm approach.
- DW-B211-05: shared vs independent buffers for Ask/Ask-ALL and Bid/Bid-ALL.
  Default spec: shared (_askBuffer, _bidBuffer). Confirm or change.

### NT8 API constraint (confirmed — do not re-investigate)
Account.Cancel() + Account.CreateOrder() + Submit() — AddOnBase-available. CORRECT.
AtmStrategyChangeStopTarget() — StrategyBase-only. BANNED in AddOnBase. Do NOT use.

### Jane Street compliance checklist
- CYC <= 8 for all new methods
- JS-021: no lock() — volatile int buffers (JS-023)
- JS-001: no throw in new methods
- JS-033: no async void
- ASCII-only: PTT-MA-Ask-N, PTT-MA-Bid-N, all log strings
- [Fact] only — no NUnit, no MSTest
- ptt-sync: 19/0 required after all .cs edits
- Guardian symbol check: powershell -File .\scripts\verify_critical_symbols.ps1 PASS
- Partial fill: existingTarget.Quantity - existingTarget.Filled (MANDATORY — see spec B211 Partial Fill Behaviour)

### ptt-sync requirement
After Ph4a (.cs edits), engineer MUST run:
  powershell -File .\scripts\ptt-sync.ps1
Confirm output: N/0. Include in ticket-N-completion.md.

### Ph4b deletion audit (MANDATORY)
Before VERIFY_PASS, run:
  git diff HEAD~1 HEAD -- src/PropTraderTools/TradeCopierPanel.cs | Select-String "^-    (private|internal|public)"
Every deleted symbol must have an authorising DW-card reference. Zero tolerance.

### Spec update required (Ph5)
ptt-plan-reviewer must update specs/002-trade-copier-spec.html B211 block:
- Pill: "FEATURE DESIGN — SPEC COMPLETE — AWAITING PIPELINE" -> "APPLIED — AWAITING SIM"
- Build tag: add commit SHAs and ptt-sync result
- Any DW items resolved in pipeline: mark CLOSED with commit reference

---

## OUTPUT DIRECTORY
All pipeline artifacts go in: docs/brain/B211/

Files to produce:
- 02-architecture-plan.md  (Ph1)
- 02-plan-review.md        (Ph2)
- 04-tickets.md            (Ph3)
- 04-ticket-review.md      (Ph3.5)
- ticket-1-completion.md   (Ph4a — PttMoveTargetOne + PttMoveTargetAll + PttContracts + B211Tests)
- ticket-1-verification.md (Ph4b)
- ticket-2-completion.md   (Ph4a — TradeCopierPanel button rows)
- ticket-2-verification.md (Ph4b)
- 05-final-review.md       (Ph5)
- 06-deferred-backlog.md   (Ph5)

---

## PIPELINE START INSTRUCTION

Paste this entire prompt into a ptt-architect mode session.
ptt-architect produces 02-architecture-plan.md.
Do NOT skip to Ph3. Do NOT combine Ph1+Ph2. Do NOT edit .cs files in Ph1 or Ph2.
