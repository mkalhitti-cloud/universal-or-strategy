# B209 Orchestrator Prompt
# PTT-COPIER-B209 — BE-ALL Permanent Lock Fix (FIX-G-1 + FIX-G-2)
# Generated: 2026-10-16 | Director approved

---

## CONTEXT (do not re-investigate)

B208 SIM Round 6 diagnosed a permanent `_beSwapInProgress` flag lock.
Root cause: `TryClearBeSwapOnStopWorking` only clears on `Working`,
but when PTT-BE-Target-N fills (scratch exit), the OCO partner
PTT-BE-Stop-N transitions `CancelSubmitted -> Cancelled` (NEVER Working).
Flag stays true permanently. On the next trade NakedPositionDetector
is permanently suppressed for all follower accounts — any naked position
will go unprotected with no PTT-Flatten safety net.

Two fixes required — both in `CopyEngine.cs`, no other files.

FIX-G-1: `TryClearBeSwapOnStopWorking` — extend clear from Working-only
to `Working | Cancelled | Rejected | Accepted`. CYC 3->5.

FIX-G-2: `TryCancelSingleStaleGtcExit` — add `IsBeSwapInProgress` guard
before cancelling PTT-BE-* orders. Prevents stale GTC sweep from wiping
a Working PTT-BE-Stop-N during a rapid re-entry that overlaps the swap
window. CYC 1->2.

Spec reference: specs/002-trade-copier-spec.html, section B208,
"B208 SIM ROUND 6 DIAGNOSIS" and "B208-FIX-G" design cards.

Git SHA at time of writing: efd3b92e (branch: main, 0 uncommitted files)

---

## PIPELINE — MANDATORY ALL 5 PHASES, NONE SKIPPABLE, NONE COMBINABLE

Ph1  ptt-architect       -> docs/brain/B209/02-architecture-plan.md
Ph2  ptt-plan-reviewer   -> docs/brain/B209/02-plan-review.md       (REVIEW_PASS gate)
Ph3  ptt-architect       -> docs/brain/B209/04-tickets.md
Ph3.5 ptt-ticket-reviewer -> docs/brain/B209/04-ticket-review.md   (TICKET_REVIEW_PASS gate)
Ph4a ptt-engineer        -> src .cs edits + docs/brain/B209/ticket-N-completion.md
Ph4b ptt-verifier        -> docs/brain/B209/ticket-N-verification.md (VERIFY_PASS gate)
Ph5  ptt-plan-reviewer   -> docs/brain/B209/05-final-review.md + docs/brain/B209/06-deferred-backlog.md

No shortcuts. No direct edits. No phase skipping. No phase combining.

---

## TASK BRIEF FOR ptt-architect (Ph1)

### Block number
B209

### Problem statement
`TryClearBeSwapOnStopWorking` only clears `_beSwapInProgress` when
PTT-BE-Stop-N reaches `OrderState.Working`. In the OCO scratch-exit path,
PTT-BE-Stop-N is cancelled by NT8 (OCO partner filled) and transitions
directly to `Cancelled`, never reaching `Working`. The flag therefore
stays `true` permanently. On any subsequent trade on that follower account,
`NakedPositionDetector` returns early at the `IsBeSwapInProgress` guard and
never fires PTT-Flatten — leaving open positions unprotected indefinitely.

A second latent risk: `IsStaleGtcExitOrder` matches Working `PTT-BE-*`
orders. If a follower re-enters quickly via PTT-Copy/Entry fill while the
prior `PTT-BE-Stop-N` is still Working, `TryCancelStaleGtcExitOnEntry`
cancels the live stop — follower left unprotected.

### Files in scope
- `src/PropTraderTools/CopyEngine.cs` — two methods:
  - `TryClearBeSwapOnStopWorking` (~line 3159)
  - `TryCancelSingleStaleGtcExit` (~line 2177)
- `src/PropTraderTools/Tests/B209Tests.cs` — NEW test file (xUnit only)

### Fix designs (implement exactly as specified)

**FIX-G-1: TryClearBeSwapOnStopWorking**

BEFORE:
```csharp
private void TryClearBeSwapOnStopWorking(OrderEventArgs e)
{
    if (e.Order.Name == null
        || !e.Order.Name.StartsWith("PTT-BE-Stop", StringComparison.Ordinal)) // (1)
        return;
    if (e.Order.OrderState != OrderState.Working) // (2) BUG: misses Cancelled
        return;
    if (!IsBeSwapInProgress(e.Order.Account?.Name)) // (3)
        return;
    SetBeSwapInProgress(e.Order.Account.Name, false);
}
```

AFTER (FIX-G-1):
```csharp
// B208-FIX-G-1: clear on Working (normal: stop live), Cancelled (OCO: target filled),
// Rejected (bad price: retry handles), Accepted (SIM belt+suspenders: mirrors FIX-F-A).
// All four are terminal or confirmed-live outcomes for PTT-BE-Stop-N.
// Permanent lock eliminated: flag cannot leak across trade cycles.
// CYC=5: (1) name check, (2a-d) four-state isClearState, (3) IsBeSwapInProgress.
// JS-021: no lock. JS-001: no throw. JS-002: void. ASCII-only.
private void TryClearBeSwapOnStopWorking(OrderEventArgs e)
{
    if (e.Order.Name == null
        || !e.Order.Name.StartsWith("PTT-BE-Stop", StringComparison.Ordinal)) // (1)
        return;
    bool isClearState = e.Order.OrderState == OrderState.Working    // (2a) normal: stop live
        || e.Order.OrderState == OrderState.Cancelled               // (2b) OCO: target filled
        || e.Order.OrderState == OrderState.Rejected                // (2c) bad price: evict+retry
        || e.Order.OrderState == OrderState.Accepted;               // (2d) SIM: mirrors FIX-F-A
    if (!isClearState)
        return;
    if (!IsBeSwapInProgress(e.Order.Account?.Name)) // (3)
        return;
    SetBeSwapInProgress(e.Order.Account.Name, false);
}
```

CYC delta: +2 branches (2b, 2c, 2d add 3 new states; bool expression
with || counts as +2 McCabe from the original single check). Final CYC=5.
JS-021: no lock. ASCII-only. Comment references B208-FIX-G-1.

---

**FIX-G-2: TryCancelSingleStaleGtcExit**

BEFORE:
```csharp
private void TryCancelSingleStaleGtcExit(Account acc, Order o)
{
    if (!IsStaleGtcExitOrder(o)) return;
    try
    {
        acc.Cancel(new Order[] { o });
        if (StatusUpdate != null)
            StatusUpdate(acc.Name + ": [GTC-SWEEP] cancelled " + o.Name);
    }
    catch (Exception ex)
    {
        if (StatusUpdate != null)
            StatusUpdate("[GTC-SWEEP] cancel error: " + ex.Message);
    }
}
```

AFTER (FIX-G-2):
```csharp
// B208-FIX-G-2: do not cancel PTT-BE-* orders while BE-swap is in progress.
// IsBeSwapInProgress stays true until PTT-BE-Stop-N reaches a terminal state (FIX-G-1).
// Without this guard, a rapid PTT-Copy/Entry fill can sweep a Working PTT-BE-Stop-N
// via TryCancelStaleGtcExitOnEntry, leaving the follower unprotected.
// CYC: +1 branch. ASCII-only. JS-021: IsBeSwapInProgress is lock-free. JS-001: no throw.
private void TryCancelSingleStaleGtcExit(Account acc, Order o)
{
    if (!IsStaleGtcExitOrder(o)) return;
    // B208-FIX-G-2: protect live PTT-BE-Stop-N during active swap window.
    if (o.Name != null
        && o.Name.StartsWith("PTT-BE-", StringComparison.Ordinal)
        && IsBeSwapInProgress(acc.Name))
        return;
    try
    {
        acc.Cancel(new Order[] { o });
        if (StatusUpdate != null)
            StatusUpdate(acc.Name + ": [GTC-SWEEP] cancelled " + o.Name);
    }
    catch (Exception ex)
    {
        if (StatusUpdate != null)
            StatusUpdate("[GTC-SWEEP] cancel error: " + ex.Message);
    }
}
```

CYC delta: +1 branch (the PTT-BE- + IsBeSwapInProgress compound guard).
Final CYC=2. JS-021: lock-free. ASCII-only. Comment references B208-FIX-G-2.

---

### Testable seam requirement

`TryClearBeSwapOnStopWorking` is private — it is called from `OnOrderUpdate`
and cannot be directly tested without a testable seam. Add an `internal`
testable delegate that extracts the state-check logic:

```csharp
// B209: testable seam for TryClearBeSwapOnStopWorking state-check logic.
// Extracts isClearState predicate so xUnit can verify all four states without NT8 OrderEventArgs.
// CYC=1 (expression body). JS-021: no lock. JS-001: no throw. ASCII-only.
internal static bool IsBeClearStateTrigger(string orderName, OrderState state)
{
    if (orderName == null || !orderName.StartsWith("PTT-BE-Stop", StringComparison.Ordinal))
        return false;
    return state == OrderState.Working
        || state == OrderState.Cancelled
        || state == OrderState.Rejected
        || state == OrderState.Accepted;
}
```

Similarly for FIX-G-2:
```csharp
// B209: testable seam for FIX-G-2 guard in TryCancelSingleStaleGtcExit.
// Extracts the BE-swap skip predicate for xUnit testing.
// CYC=1 (expression body). JS-021: no lock. JS-001: no throw. ASCII-only.
internal static bool IsBeSwapGtcSweepGuard(string orderName, bool beSwapInProgress)
    => orderName != null
        && orderName.StartsWith("PTT-BE-", StringComparison.Ordinal)
        && beSwapInProgress;
```

### xUnit tests required (B209Tests.cs)

Minimum 6 tests. xUnit only (no NUnit/MSTest). ASCII-only. [Fact] only.

1. `IsBeClearStateTrigger_Working_ReturnsTrue` — PTT-BE-Stop-1, Working -> true
2. `IsBeClearStateTrigger_Cancelled_ReturnsTrue` — PTT-BE-Stop-1, Cancelled -> true
3. `IsBeClearStateTrigger_Rejected_ReturnsTrue` — PTT-BE-Stop-1, Rejected -> true
4. `IsBeClearStateTrigger_Accepted_ReturnsTrue` — PTT-BE-Stop-1, Accepted -> true
5. `IsBeClearStateTrigger_Submitted_ReturnsFalse` — PTT-BE-Stop-1, Submitted -> false (not a clear trigger)
6. `IsBeClearStateTrigger_WrongName_ReturnsFalse` — PTT-BE-Target-1, Working -> false (name filter)
7. `IsBeSwapGtcSweepGuard_PttBeName_SwapActive_ReturnsTrue` — "PTT-BE-Stop-1", true -> true
8. `IsBeSwapGtcSweepGuard_PttBeName_SwapInactive_ReturnsFalse` — "PTT-BE-Stop-1", false -> false
9. `IsBeSwapGtcSweepGuard_NonPttBeName_SwapActive_ReturnsFalse` — "Stop1", true -> false
10. `IsBeSwapGtcSweepGuard_NullName_ReturnsFalse` — null, true -> false

### Jane Street compliance checklist
- CYC <= 8 for all touched methods (TryClearBeSwapOnStopWorking: 5, TryCancelSingleStaleGtcExit: 2)
- JS-021: no lock() in any new or modified code
- JS-001: no throw — all exceptions caught in existing try/catch
- JS-033: synchronous void — no async void
- ASCII-only: all string literals must be ASCII
- [Fact] only — no NUnit, no MSTest
- No null returns (JS-002) in new methods

### ptt-sync requirement
After Ph4a (engineer edits .cs files), engineer MUST run:
`powershell -File .\scripts\ptt-sync.ps1`
Confirm output: N/0 (zero errors). Include in ticket-N-completion.md.

### Guardian symbol check
After any CopyEngine.cs edit, run:
`powershell -File .\scripts\verify_critical_symbols.ps1`
PASS required before VERIFY_PASS.

### Ph4b deletion audit (MANDATORY)
Before VERIFY_PASS, run:
`git diff HEAD~1 HEAD -- src/PropTraderTools/CopyEngine.cs | Select-String "^-    (private|internal|public)"`
Every deleted symbol must have an authorising DW-card reference.
Zero tolerance. Single-hunk ban: if `(git show HEAD -- CopyEngine.cs | Select-String "^@@").Count -eq 1`
this is a full-file replace — automatic VERIFY_FAIL.

### Spec update required (Ph5)
ptt-plan-reviewer must update `specs/002-trade-copier-spec.html`:
- B208 block: mark FIX-G-1 + FIX-G-2 as APPLIED with ptt-sync result
- B208 card title: update from "FIX-G DESIGNED — AWAITING DIRECTOR APPROVAL"
  to "FIX-G APPLIED — AWAITING SIM ROUND 7"
- Bug table row for B208: update status column
- 06-deferred-backlog.md: record anything deferred from this block

---

## OUTPUT DIRECTORY
All pipeline artifacts go in: `docs/brain/B209/`

Files to produce:
- `02-architecture-plan.md` (Ph1)
- `02-plan-review.md` (Ph2)
- `04-tickets.md` (Ph3)
- `04-ticket-review.md` (Ph3.5)
- `ticket-1-completion.md` (Ph4a — FIX-G-1)
- `ticket-1-verification.md` (Ph4b)
- `ticket-2-completion.md` (Ph4a — FIX-G-2 + testable seams + B209Tests.cs)
- `ticket-2-verification.md` (Ph4b)
- `05-final-review.md` (Ph5)
- `06-deferred-backlog.md` (Ph5)

---

## PIPELINE START INSTRUCTION

Paste this entire prompt into a `ptt-architect` mode session.
ptt-architect produces `02-architecture-plan.md`.
Do NOT skip to Ph3. Do NOT combine Ph1+Ph2. Do NOT edit .cs files in Ph1 or Ph2.
