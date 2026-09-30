# DW-B167-02 — Follower ATM brackets not placed after leader fill (fo=NULL on all drags)

**ID:** DW-B167-02
**Block origin:** B167 live-test session 2026-09-18
**Priority:** P1 — naked follower positions. Stop/target missing = unprotected risk.
**Status:** OPEN — needs second output.md to confirm exact gate that blocks bracket copy.
**Source file:** `src/PropTraderTools/CopyEngine.cs`

---

## Symptom

After leader entry fills and ATM brackets (`Stop1/2/3`, `Target1/2/3`) appear on
the leader account, the follower accounts (Sim102, Sim103, Sim104) do not receive
matching bracket orders. Consequence: when the leader stop/target is dragged,
`FindFollowerBracketOrder` returns null for all followers (`fo=NULL` in TP4-SFB logs)
and the drag has no effect on followers.

The user sees: stops "sometimes work, sometimes disappear" -- the drag appears to work
when a prior session left brackets in place, and fails when the brackets were never
copied in the first place.

---

## Evidence from output.md (2026-09-18)

**Every TP4-SFB log line shows `fo=NULL`:**
```
[TP4-SFB] acc=Sim102 leaderName=Stop1 isStop=True fo=NULL followerOrders=[Entry:Cancelled]
[TP4-SFB] acc=Sim103 leaderName=Stop1 isStop=True fo=NULL followerOrders=[Entry:Cancelled]
[TP4-SFB] acc=Sim104 leaderName=Stop1 isStop=True fo=NULL followerOrders=[Entry:Cancelled]
```

followerOrders shows only `Entry:Cancelled` -- no Stop or Target orders exist on any
follower account. The TP3-HBC log confirms the drag path fires correctly (`followerCount=3`)
but has nothing to sync.

**ATM bracket orders exit gate3 immediately after leader fill (output.md L72-L119):**
```
[TP2-DRAG] IsWorkingBracket=False name=Stop1 state=Initialized
[PTT-COPY-DIAG] gate3 exit: name=Stop1 act=Sell state=Initialized type=StopMarket

[TP2-DRAG] IsWorkingBracket=False name=Target1 state=Initialized
[PTT-COPY-DIAG] gate0.5 exit: name=Target1 act=Sell state=Initialized type=Limit
```

- `Stop1` at Initialized: rejected at gate3 (not a dispatch-trigger state)
- `Target1` at Initialized: rejected at gate0.5 (blocked by `IsExitSignalNameOrAnonClose`)
- Same pattern for Stop2/3, Target2/3, Submitted state

The brackets pass through `DispatchCopy` at state Initialized and Submitted, but
both states fail the gate before the copy is dispatched to followers.

---

## Root Cause (Hypothesis -- needs second output.md to confirm)

`IsDispatchTriggerState` at L2581:
```csharp
internal static bool IsDispatchTriggerState(OrderState state, OrderType type) =>
    (type == OrderType.Market && state == OrderState.Submitted)
    || (type == OrderType.Limit && (state == OrderState.Accepted || state == OrderState.Working));
```

This predicate passes ONLY for:
- Market orders at Submitted
- Limit orders at Accepted or Working

ATM StopMarket brackets (`Stop1/2/3`) have `OrderType=StopMarket` -- they never
satisfy `IsDispatchTriggerState` at any state. Gate3 always rejects them.

ATM Target brackets (`Target1/2/3`) have `OrderType=Limit` but `name="Target1"` --
gate0.5 (`IsExitSignalNameOrAnonClose`) blocks them because the name "Target1" matches
the NT8 ATM bracket naming pattern (DW-LB-FL-01, B78 DW-B78-01).

**Consequence:** the leader's ATM brackets never reach follower dispatch. Followers
are never given Stop or Target orders after the fill. The copier only dispatches the
entry, not the brackets.

This also explains the grid CSV (2026-09-18 01-18 PM): follower exits are via
`PTT-Flatten` and `PTT-TGT-Drag` (manually dragged targets that succeeded when
brackets happened to exist from a prior session), not from ATM bracket fills.

---

## What the second output.md is for

The existing output.md CONFIRMS brackets are not being placed but does NOT show
what happens when brackets reach Accepted/Working state on the leader (the state
where `IsDispatchTriggerState` would pass for Limit orders -- but Target brackets
would still be blocked by gate0.5). Need to confirm:

1. Do Stop brackets (`StopMarket`) ever appear in the output with state=Accepted
   or Working? If yes, do they hit gate3 at that state too?
2. Does `gate0.5` block Target1/2/3 or does gate3 block them first?

This tells us whether the fix is:
- (A) Extend `IsDispatchTriggerState` to pass StopMarket at Accepted/Working
  AND suppress gate0.5 for bracket-type names
- (B) A completely separate bracket-copy path (not through `DispatchCopy` at all)

Option B is architecturally correct: `DispatchCopy` was designed for entry orders.
Bracket placement after a leader ATM fill should go through a dedicated path that
detects the leader-fill event and copies the bracket prices to followers -- this is
what the `HandleBracketChange` / `SyncFollowerBracket` path does for drag, but
there appears to be no equivalent "initial placement" path.

---

## Second output.md Instructions (for the Director)

To get the data needed for this card, do the following test:

**Test procedure:**
1. Clear the NT8 output tab.
2. Place a limit entry on the leader. Wait for it to fill.
3. Wait 2-3 seconds for all bracket state transitions to complete.
4. Save output tab to `docs/output2.md`.
5. Do NOT drag anything. Do NOT exit. Just capture the bracket placement sequence.

**What to look for in output2.md:**
- Any `Stop1/2/3` or `Target1/2/3` lines with `state=Accepted` or `state=Working`.
- If they exist: what gate blocks them (gate3 or gate0.5)?
- If they do NOT exist (brackets go Initialized -> Submitted -> Accepted -> Working
  but are never logged in TP2-DRAG after Submitted): then the issue is that
  `IsWorkingBracket` returns false, not `IsDispatchTriggerState` gating them out.

**Specifically needed:**
- Is there a `[TP2-DRAG] IsWorkingBracket=True name=Stop1 state=Accepted` line?
  If YES: the drag path IS reached for Stop orders. Gate3 is the blocker.
  If NO: `IsWorkingBracket` returns false and the bracket never enters the drag check.

---

## Preliminary Fix Direction (pending output2.md)

**If IsWorkingBracket=False for Stop1/2/3 at Working/Accepted:**
The root is in `IsWorkingBracket` (L3365). It returns false for ATM Stop brackets
in the initial placement window. The TP2-DRAG log at L72-L119 shows
`IsWorkingBracket=False name=Stop1 state=Accepted` -- which confirms this.
Fix would be: extend `IsWorkingBracket` or add a separate initial-bracket-copy
path triggered by the leader entry fill event (leader Filled + has ATM brackets
-> copy bracket prices to followers).

**Architecture note:** the cleanest fix is a dedicated `TryInitialBracketCopy`
called from the leader-fill path that reads the leader's working bracket prices
and submits matching bracket orders on followers via `CreateOrder+Submit` --
analogous to `SyncFollowerBracket` but for the initial placement rather than drag.

---

## Rules (when fix is designed)

- JS-CYC: new methods must be CYC <= 8.
- JS-021: no new locks.
- ASCII-only.
- No `AtmStrategyCreate` from AddOnBase (StrategyBase-only per NT8_FULL_REFERENCE.md).
- Follower brackets must be standalone (oco="") -- NT8 AddOnBase cannot add orders
  to an existing ATM OCO group (confirmed B142 design notes L2083).

---

## Dependencies

- DW-B167-01 (DRAIN-ABORT-FLAT fix) should go first -- once pre-fill entry drags
  work, there will be more follower entries filling and more bracket-copy events.
  Fixing B167-02 first while B167-01 is open could cause double-fix confusion.

---

*Written by copier-spec mode — 2026-09-18 — Evidence: output.md L72-L227 (all fo=NULL); grid CSV 01-18 PM.*
