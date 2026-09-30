# B179 PTT-Orchestrator Prompt
# BLOCK: B179 -- DW-B179-BE-ALL-SILENT-CANCEL
# Produced by: ptt-orchestrator (copier-spec mode)
# Status: AWAITING DIRECTOR GO

---

## MISSION STATEMENT

Block B179 closes one confirmed live-trading UX failure: BE All is silently cancelled
when the user triggers a manual Positions-tab flatten while BE All is armed (PTT-BE-*
orders pending). The timeline observed on 2026-09-28:

- L801-815 output.md: BE All clicked -- 4 accounts armed, PTT-BE-RETRY armed for all 4
- L816-887 output.md: PositionStateChanged hasPos=False x36 -- manual flatten won the race
- L832-899 output.md: [BE-DIAG-CANCEL] for every PTT-BE-Target on all 4 accounts
- Zero PTT-BE-Stop fills. Zero PTT-BE-Target fills. BE All = complete no-op.
- Director received zero feedback that BE All was overridden.

The fix detects the flat+armed race in the LogBeCancelDiag call site
(TryTriggerBeRecovery) and adds:
1. A new [BE-WARN] Output log line when a PTT-BE-* order is cancelled while the
   account is already flat AND a pending BE slot is still armed.
2. A StatusUpdate panel message (same content) so the BE ALL button feedback path
   carries the warning to the panel UI.
3. A DisarmPendingBe(o.Account) call to clean up the armed slot atomically when
   flat is confirmed, preventing stale slot state.

Files under surgery: `src/PropTraderTools/CopyEngine.cs`
Test file:           `src/PropTraderTools/CopyEngineTests.cs`
Pipeline: FULL 5-PHASE (all phases mandatory, none skippable, none combinable)

---

## BUG DESCRIPTION

### DW-B179-BE-ALL-SILENT-CANCEL (P1)

**Trigger:** Director clicks BE All while holding positions across 4 accounts.
Before any PTT-BE-Stop order fires, the Director triggers a manual Positions-tab
Flatten All (or NinjaTrader flattens the account via a different path). The
positions go flat before any PTT-BE-* orders execute. Result: BE All was a complete
no-op but no warning or status message was emitted.

**Evidence (output.md):**
```
L801:  [BE-ALL] button: arm buf=0
L802:  [BE-ALL] GlobalBreakEven: ArmAllPendingBe buf=0t
L803:  [BE] ArmPendingBe: PAAPEX4221360000010 MES DEC26 buf=0t -- ARMED
L804:  [BE] ArmPendingBe: PAAPEX4221360000011 MES DEC26 buf=0t -- ARMED
L805:  [BE] ArmPendingBe: PA-APEX-422136-09 MES DEC26 buf=0t -- ARMED
L806:  [BE] ArmPendingBe: PA-APEX-422136-08 MES DEC26 buf=0t -- ARMED
L807:  [BE] MoveStopToBreakEven: PA-APEX-422136-08 buf=0t
L809:  [PTT-BE-RETRY] armed: PA-APEX-422136-08 bePrice=7769
L810:  [BE] MoveStopToBreakEven: PAAPEX4221360000010 buf=0t
L812:  [PTT-BE-RETRY] armed: PAAPEX4221360000010 bePrice=7769.25
L813:  [BE] MoveStopToBreakEven: PAAPEX4221360000011 buf=0t
L815:  [PTT-BE-RETRY] armed: PAAPEX4221360000011 bePrice=7769.25
L816-L887: PositionStateChanged instr=MES DEC26 hasPos=False x36 (all 4 accounts)
L832+: [BE-DIAG-CANCEL] PAAPEX4221360000011 PTT-BE order cancelled: PTT-BE-Target-1
L836+: [BE-DIAG-CANCEL] PAAPEX4221360000010 PTT-BE order cancelled: PTT-BE-Target-2
L888+: [BE-DIAG-CANCEL] PA-APEX-422136-08 PTT-BE order cancelled: PTT-BE-Target-3
       (... and 20+ more BE-DIAG-CANCEL lines, no PTT-BE-Stop fills, no PTT-BE-Target fills)
```
Zero [BE-WARN], zero StatusUpdate panel messages, zero DisarmPendingBe calls.

**Root Cause:**
`TryTriggerBeRecovery` (CopyEngine.cs line 5222) calls `LogBeCancelDiag` (line 5232)
unconditionally on any PTT-BE-* cancel, then returns early at line 5233-5234 for
PTT-BE-Target-* orders (only Stop-* triggers bracket recovery). For the flat+armed
case:
- The pending BE slot is still present in `_pendingBeSlots` when the cancel arrives
  (DisarmPendingBe is not called until ArmPendingBe fires its price trigger or the
  user explicitly disarms -- neither happened here)
- `IsFlat(FindPosition(o.Account, o.Instrument))` is true at this point
- `LogBeCancelDiag` only logs `[BE-DIAG-CANCEL]` with no flat+armed awareness
- No code path calls `DisarmPendingBe` or `StatusUpdate` on this branch

**Call path (confirmed from source reads):**
```
OnOrderUpdate (CopyEngine.cs line ~1735)
  -> TryTriggerBeRecovery(e.Order)           line 5222
       guard: o == null                      line 5224   (1)
       guard: OrderState != Cancelled        line 5226   (2)
       guard: o.Name == null                 line 5228   (3)
       guard: !StartsWith("PTT-BE-")         line 5230   (4)
       LogBeCancelDiag(o)                    line 5232   <-- logs [BE-DIAG-CANCEL]
       guard: !StartsWith("PTT-BE-Stop-")    line 5233   (5)  <-- returns here for Target-*
       guard: HasFilledBeTargetFast          line 5240   (6)
       TryReplacePttBeBrackets(o)            line 5242
```

The fix is injected at or just before the (5) guard return. When control reaches
line 5233, the order IS a PTT-BE-* cancel and account/instrument are non-null (passed
guards 1-4). The flat+armed detection is safe here.

---

## CONFIRMED SOURCE EVIDENCE (do not re-read from files -- embed directly)

```
CopyEngine.cs line 5222-5243 (TryTriggerBeRecovery -- current):

    private void TryTriggerBeRecovery(Order o)
    {
        if (o == null)
            return; // (1)
        if (o.OrderState != OrderState.Cancelled)
            return; // (2)
        if (o.Name == null)
            return; // (3)
        if (!o.Name.StartsWith("PTT-BE-", StringComparison.Ordinal))
            return; // (4)
        LogBeCancelDiag(o);
        if (!o.Name.StartsWith("PTT-BE-Stop-", StringComparison.Ordinal))
            return; // (5)
        if (HasFilledBeTargetFast(o.Account))
            return; // (6)
        TryReplacePttBeBrackets(o);
    }

CopyEngine.cs line 5310-5326 (LogBeCancelDiag -- current):

    private static void LogBeCancelDiag(Order o)
    {
        string accName = o.Account != null ? o.Account.Name : "?";
        string instrName = o.Instrument != null ? o.Instrument.FullName : "?";
        NinjaTrader.Code.Output.Process(
            "[BE-DIAG-CANCEL] "
                + accName
                + " PTT-BE order cancelled: "
                + o.Name
                + " instr="
                + instrName,
            NinjaTrader.NinjaScript.PrintTo.OutputTab1
        );
    }

CopyEngine.cs line 7684-7695 (DisarmPendingBe -- current):

    internal void DisarmPendingBe(Account leader)
    {
        if (leader == null) // (1)
        {
            StatusUpdate?.Invoke("DisarmPendingBe: leader null -- no-op");
            return;
        }
        if (!_pendingBeSlots.TryRemove(leader.Name, out var slot)) // (2)
            return;
        if (slot.Account != null) // (3)
            slot.Account.AccountItemUpdate -= OnPendingBeAccountUpdate;
    }

CopyEngine.cs line 7699 (IsPendingSlotsEmpty):
    internal bool IsPendingSlotsEmpty() => _pendingBeSlots.IsEmpty;

CopyEngine.cs line 7975-7979 (IsBeArmedForAccount -- test seam):
    _pendingBeSlots.ContainsKey(accountName);

Key field:
    ConcurrentDictionary<string, PendingBeSlot> _pendingBeSlots   line 270
```

---

## REPAIR DESIGN

### Method: LogBeCancelDiag -- CYC 1 -> 1 (unchanged, body rewritten for clarity only)

No change to `LogBeCancelDiag`. The [BE-DIAG-CANCEL] log is preserved verbatim.

### Method: TryTriggerBeRecovery -- CYC 6 -> 8 (add 2 branches: flat guard + armed guard)

The flat+armed detection is extracted to a new helper `TryEmitBeWarnIfFlatArmed`
(CYC=4) to keep TryTriggerBeRecovery's CYC within the JS-013 limit of 8.

**CYC accounting:**
- Current TryTriggerBeRecovery: CYC=6 (6 guards, all return-only)
- After: TryTriggerBeRecovery gains zero new branches (TryEmitBeWarnIfFlatArmed is
  a void call, no conditional wrapping it in parent) -- CYC stays at 6.
  (The "call" branch is NOT counted as a CYC branch in Jane Street rules unless
  the call itself is wrapped in an if. There is no if-wrapper here.)
- TryEmitBeWarnIfFlatArmed (NEW): CYC=4 (null guard + ContainsKey + IsFlat + 
  StatusUpdate?.). Well within <=8.

**BEFORE (TryTriggerBeRecovery lines 5222-5243):**
```csharp
private void TryTriggerBeRecovery(Order o)
{
    if (o == null)
        return; // (1)
    if (o.OrderState != OrderState.Cancelled)
        return; // (2)
    if (o.Name == null)
        return; // (3)
    if (!o.Name.StartsWith("PTT-BE-", StringComparison.Ordinal))
        return; // (4)
    LogBeCancelDiag(o);
    if (!o.Name.StartsWith("PTT-BE-Stop-", StringComparison.Ordinal))
        return; // (5)
    if (HasFilledBeTargetFast(o.Account))
        return; // (6)
    TryReplacePttBeBrackets(o);
}
```

**AFTER (TryTriggerBeRecovery -- CYC=6 unchanged):**
```csharp
// B179 DW-B179-BE-ALL-SILENT-CANCEL: TryEmitBeWarnIfFlatArmed call added after
// LogBeCancelDiag -- emits [BE-WARN] + StatusUpdate + DisarmPendingBe when account
// is flat and a pending BE slot is still armed. CYC=6 (unchanged -- helper absorbs branches).
// JS-021: no lock. JS-001: no throw. JS-002: void. ASCII-only.
private void TryTriggerBeRecovery(Order o)
{
    if (o == null)
        return; // (1)
    if (o.OrderState != OrderState.Cancelled)
        return; // (2)
    if (o.Name == null)
        return; // (3)
    if (!o.Name.StartsWith("PTT-BE-", StringComparison.Ordinal))
        return; // (4)
    LogBeCancelDiag(o);
    TryEmitBeWarnIfFlatArmed(o); // B179: warn + disarm when flat overtook BE All
    if (!o.Name.StartsWith("PTT-BE-Stop-", StringComparison.Ordinal))
        return; // (5)
    if (HasFilledBeTargetFast(o.Account))
        return; // (6)
    TryReplacePttBeBrackets(o);
}
```

**Lines changed:** +1 (`TryEmitBeWarnIfFlatArmed(o);` call). CYC: 6 -> 6.

---

### NEW Method: TryEmitBeWarnIfFlatArmed -- CYC=4 (new method, 0->4)

**ADD after LogBeCancelDiag (after line 5326):**
```csharp
// B179 DW-B179-BE-ALL-SILENT-CANCEL: emit [BE-WARN] + StatusUpdate + DisarmPendingBe
// when a PTT-BE-* cancel arrives while account is flat and a pending BE slot is still armed.
// This is the "manual flatten won the race against BE All" path.
// Called from TryTriggerBeRecovery immediately after LogBeCancelDiag.
// Preconditions (guaranteed by caller guards 1-4): o != null, o.Account != null,
//   o.Instrument != null, o.Name starts with "PTT-BE-".
// CYC=4: (1) Account null guard; (2) ContainsKey armed check;
//        (3) IsFlat guard; (4) StatusUpdate?. operator.
// JS-021: ConcurrentDictionary.ContainsKey is lock-free. JS-001: no throw.
// JS-002: void. ASCII-only. No lock().
private void TryEmitBeWarnIfFlatArmed(Order o)
{
    if (o.Account == null) // (1)
        return;
    if (!_pendingBeSlots.ContainsKey(o.Account.Name)) // (2) not armed -- nothing to warn
        return;
    if (!IsFlat(FindPosition(o.Account, o.Instrument))) // (3) position open -- not this race
        return;
    string msg = "[BE-WARN] BE All overridden by manual flatten: "
        + o.Account.Name
        + " -- BE order cancelled while flat, armed slot disarmed";
    NinjaTrader.Code.Output.Process(msg, NinjaTrader.NinjaScript.PrintTo.OutputTab1);
    StatusUpdate?.Invoke(msg); // (4) ?. operator
    DisarmPendingBe(o.Account); // clean up stale armed slot
}
```

**CYC = 4.** No new lock(). ASCII-only. All NT8 calls are safe on NT8 event thread.
`DisarmPendingBe` is idempotent (TryRemove -- second call is a no-op).

---

### CYC Summary

| Method | File | Before | After | Delta | Compliant |
|--------|------|--------|-------|-------|-----------|
| TryTriggerBeRecovery | CopyEngine.cs | 6 | 6 | 0 | YES (<=8) |
| TryEmitBeWarnIfFlatArmed (NEW) | CopyEngine.cs | -- | 4 | +4 | YES (<=8) |
| LogBeCancelDiag | CopyEngine.cs | 1 | 1 | 0 | YES (<=8) |
| DisarmPendingBe | CopyEngine.cs | 3 | 3 | 0 | YES (<=8) (called, not modified) |

---

## TICKETS

### B179-1 -- TryTriggerBeRecovery + TryEmitBeWarnIfFlatArmed

**File:** `src/PropTraderTools/CopyEngine.cs`

**Change A -- TryTriggerBeRecovery (line ~5232):**
Insert `TryEmitBeWarnIfFlatArmed(o);` on the line immediately after `LogBeCancelDiag(o);`.
The AFTER block above is the exact expected result.

**Change B -- TryEmitBeWarnIfFlatArmed (new private method):**
Insert the new method immediately after the closing brace of `LogBeCancelDiag` (line ~5326).
The AFTER block above is the exact expected result.

**Implementation constraints:**
- Use `apply_diff` ONLY (no `write_file` on .cs files)
- Two `apply_diff` hunks in one call: one for Change A, one for Change B
- CYC: TryTriggerBeRecovery stays at 6. TryEmitBeWarnIfFlatArmed new at 4.
- No new lock(). ASCII-only. No Unicode. No DateTime.Now. No curly quotes.
- Every new string literal uses straight double quotes and ASCII only.

**Verify:**
- `grep -c "TryEmitBeWarnIfFlatArmed" src/PropTraderTools/CopyEngine.cs` must return 2
  (one call site in TryTriggerBeRecovery, one definition)
- `grep -c "BE-WARN" src/PropTraderTools/CopyEngine.cs` must return 1
- `grep -c "lock(" src/PropTraderTools/CopyEngine.cs` must return 0 (no new lock)
- TryTriggerBeRecovery body matches AFTER exactly (one new line added)
- TryEmitBeWarnIfFlatArmed body matches AFTER exactly (new method present)

---

### B179-2 -- Tests

**File:** `src/PropTraderTools/CopyEngineTests.cs`

**Change:** Append new xUnit test class `B179BeWarnTests` with [Fact] tests.
The class uses the existing test seams:
- `ArmPendingBeTestable(acc, instr, bufferTicks)` (line 9193 of CopyEngine.cs)
- `IsBeArmedForAccount(accountName)` (line 7975, ContainsKey test seam)
- `TryTriggerBeRecoveryNullTestable()` (line 8014, null guard seam)
- `StatusUpdate` event (public event, subscribable)
- Direct engine method calls via `CopyEngineTests` helper setup pattern

**Tests to write:**

**T-B179-01 -- flat+armed: [BE-WARN] emitted and slot disarmed**
Setup: create engine, arm pending BE for account X (via test seam or mock order path),
confirm `IsBeArmedForAccount("X")` is true.
Action: call `TryTriggerBeRecovery` with a mock Cancelled order:
  - Name = "PTT-BE-Target-1" (starts with "PTT-BE-")
  - OrderState = Cancelled
  - Account = X (flat position -- no open position)
  - Instrument = any
Subscribe to `StatusUpdate` event; collect messages.
Expected:
  - StatusUpdate fires with a message containing "BE-WARN" and account name
  - `IsBeArmedForAccount("X")` is false after the call (slot disarmed)

**T-B179-02 -- armed but NOT flat: no [BE-WARN], slot preserved**
Setup: arm pending BE for account Y. Simulate a non-flat position (position open).
Action: call `TryTriggerBeRecovery` with Cancelled PTT-BE-Target-1 order on Y.
Expected:
  - StatusUpdate does NOT fire with "BE-WARN"
  - `IsBeArmedForAccount("Y")` is still true (slot not disarmed)
Note: this tests guard (3) in TryEmitBeWarnIfFlatArmed (IsFlat = false -> return).

**T-B179-03 -- not armed: no [BE-WARN]**
Setup: engine with NO pending BE slot for account Z.
Action: call `TryTriggerBeRecovery` with Cancelled PTT-BE-Target-1 order on Z (flat).
Expected:
  - StatusUpdate does NOT fire with "BE-WARN"
Note: this tests guard (2) in TryEmitBeWarnIfFlatArmed (ContainsKey = false -> return).

**T-B179-04 -- DisarmPendingBe idempotency**
Action: call `DisarmPendingBe(account)` twice on same account with no slot present.
Expected: no exception, no crash. (Confirms TryRemove no-op on second call.)

**No direct calls to private methods.**
**`dotnet build` and `dotnet test` must pass with 0 new failures.**
**No NT8 runtime required (pure unit tests against the engine API).**

---

## MANDATORY PIPELINE (7-STEP -- ALL PHASES MANDATORY -- NONE SKIPPABLE -- NONE COMBINABLE)

```
Ph1  ptt-architect       Input: this prompt
                         Output: docs/brain/B179/02-architecture-plan.md
                         Contract: architectural plan for DW-B179-BE-ALL-SILENT-CANCEL:
                           - exact before/after for TryTriggerBeRecovery
                           - exact body for TryEmitBeWarnIfFlatArmed (new method)
                           - CYC table (TryTriggerBeRecovery 6->6, helper new at 4)
                           - rationale for helper extraction vs inline branching
                           - precondition analysis (guards 1-4 guarantee in caller)
                           - idempotency argument for DisarmPendingBe
                           - confirmation that LogBeCancelDiag is unchanged
                         Gate: none -- proceed to Ph2.

Ph2  ptt-plan-reviewer   Input: 02-architecture-plan.md
                         Output: docs/brain/B179/02-plan-review.md
                         Gate: REVIEW_PASS required. Any REVIEW_FAIL sends back to Ph1.

Ph3  ptt-architect       Input: 02-plan-review.md (REVIEW_PASS)
                         Output: docs/brain/B179/04-tickets.md
                         Contract: 2 tickets (B179-1, B179-2) with exact line numbers,
                           exact before/after, CYC verification column per ticket.

Ph3.5 ptt-ticket-reviewer Input: 04-tickets.md
                          Output: docs/brain/B179/04-ticket-review.md
                          Gate: TICKET_REVIEW_PASS required. Any TICKET_REVIEW_FAIL sends
                                back to Ph3.

Ph4a ptt-engineer         Input: 04-ticket-review.md (TICKET_REVIEW_PASS) + 04-tickets.md
                          Output: surgical apply_diff edits to CopyEngine.cs +
                                  CopyEngineTests.cs
                                  + docs/brain/B179/ticket-N-completion.md for each ticket
                          Constraints:
                            - Use apply_diff ONLY (no write_file on .cs files)
                            - Complete tickets IN ORDER: B179-1, then B179-2
                            - After ALL tickets: run powershell -File .\scripts\ptt-sync.ps1

Ph4b ptt-verifier         Input: ticket-N-completion.md files
                          Output: docs/brain/B179/ticket-N-verification.md for each ticket
                          Checks per ticket:
                            - Exact diff matches the AFTER blocks in 04-tickets.md
                            - CYC per method matches the CYC table
                            - No lock() introduced:
                              grep -c "lock(" src/PropTraderTools/CopyEngine.cs must return 0
                            - No Unicode/curly quotes in changed lines
                            - grep -c "TryEmitBeWarnIfFlatArmed" src/PropTraderTools/CopyEngine.cs
                              must return 2 (call site + definition)
                            - grep -c "BE-WARN" src/PropTraderTools/CopyEngine.cs must return 1
                            - ptt-sync.ps1 was run after all edits
                            - dotnet build 0 errors, dotnet test 0 new failures
                          Gate: VERIFY_PASS required per ticket. Any VERIFY_FAIL triggers
                                a targeted fix loop (NOT a full Ph1 restart unless root
                                cause is architectural).

Ph5  ptt-plan-reviewer    Input: all ticket-N-verification.md (all VERIFY_PASS)
                          Output: docs/brain/B179/05-final-review.md
                                  docs/brain/B179/06-deferred-backlog.md
                          Contract for 06-deferred-backlog.md:
                            - DW-B179-BE-ALL-SILENT-CANCEL: CLOSED if VERIFY_PASS
                            - DW-B179-SIM: NEW -- Director must run SIM gate (see below)
                            - DW-B177-CYC: carry forward (CYC=13 OnAtrBoxPreviewKeyDown)
                            - All prior open items from B177/06-deferred-backlog.md
                              carried forward with updated status
                          Gate: FINAL_PASS or FINAL_FAIL.
```

---

## SIM GATE (post-B179 implementation)

After B179 is implemented and verified (VERIFY_PASS all tickets), the Director runs:

1. Open NinjaTrader. Confirm PTT copier is loaded. Take a position on one account.
2. Click BE All. Confirm "[BE] ArmPendingBe: <account> ... -- ARMED" appears in Output.
3. While BE is armed and before the stop fires, flatten the position manually via
   NinjaTrader Positions tab -> Flatten All (or close the position via DOM).
4. In NT8 Output tab:
   - Expected: `[BE-WARN] BE All overridden by manual flatten: <account> -- BE order
     cancelled while flat, armed slot disarmed`
   - Expected: PTT panel status bar shows the same BE-WARN message.
   - Expected: `[BE-DIAG-CANCEL]` line for PTT-BE-Target or PTT-BE-Stop cancel still
     present (unchanged from existing behavior).
5. After flatten: `IsBeArmedForAccount` should be false (slot disarmed).
   Confirm by clicking BE All again -- it should re-arm cleanly, not show a stale state.

Pass criteria: Step 4 BE-WARN message visible in Output and panel status, and step 5
re-arm works correctly.
Close DW-B179-SIM upon Director SIM confirmation.

---

## JANE STREET COMPLIANCE CHECKLIST (apply to all tickets)

- [ ] No `lock()` statement introduced
      (grep -c "lock(" src/PropTraderTools/CopyEngine.cs == 0)
- [ ] No Unicode, curly quotes, or non-ASCII characters in any changed string literal
- [ ] No `DateTime.Now` or `FontFamily` introduced
- [ ] CYC per changed method <= 8 (verified against CYC table above)
      TryTriggerBeRecovery: 6 -> 6. TryEmitBeWarnIfFlatArmed: new at 4.
- [ ] `apply_diff` used for all .cs file edits (NO `write_file`)
- [ ] `ptt-sync.ps1` run after all edits
- [ ] `TryEmitBeWarnIfFlatArmed` appears exactly twice in CopyEngine.cs
      (call site line ~5233a + definition after LogBeCancelDiag)
- [ ] `BE-WARN` tag appears exactly once in CopyEngine.cs (in TryEmitBeWarnIfFlatArmed)
- [ ] Every changed line traces to DW-B179-BE-ALL-SILENT-CANCEL
- [ ] `dotnet build` 0 errors, `dotnet test` 0 new failures

---

## DEFERRED BACKLOG CARRY-FORWARD (from B177/06-deferred-backlog.md)

The following items from B177 are carried into B179 unchanged unless a ticket
in B179 explicitly closes them.

| ID | Block | Description | Priority | Status |
|----|-------|-------------|----------|--------|
| DW-B179-BE-ALL-SILENT-CANCEL | B179 | BE All silently cancelled when manual flatten races ahead. Fix: TryEmitBeWarnIfFlatArmed helper in TryTriggerBeRecovery. | P1 | OPEN -- target of B179 |
| DW-B179-SIM | B179 | SIM GATE: Director runs flatten-races-BE-All scenario; confirms [BE-WARN] in Output and panel status. Close upon Director SIM confirmation. | P1 | OPEN -- SIM pending |
| DW-B177-CYC | B177 | OnAtrBoxPreviewKeyDown CYC=13 exceeds JS-013 limit of 8. Extract InjectAtrChar(char c) helper. | P2 | OPEN -- carry forward |
| DW-B170-SIM | B170 | SIM GATE: Director drags target on leader -- confirm follower stop preserved after drag. B171 fix deployed. | P0 | OPEN (PENDING SIM GATE) |
| DW-B169-K1 | B169 | SIM GATE: 3-stop simultaneous drag -- 0 missing follower stops | P0 | OPEN (partially verified; Sim102 blocked by B167-DEFER-02) |
| DW-B169-K2 | B169 | SIM GATE: single-stop drag regression | P0 | OPEN |
| DW-B168-K1 | B168 | Superseded by DW-B169-K1/K2 | P0 | OPEN (superseded) |
| B167-DEFER-02 | B167 | fo=NULL: follower ATM brackets never placed after leader fill. Blocks DW-B169-K1 Sim102. | P1 | OPEN |
| B167-DEFER-01 | B167 | ECA-1 double-position race. Monitor at SIM. | P2 | OPEN |
| B167-DEFER-03 | B167 | Combined B161-B166 SIM gate pending | P1 | OPEN |
| B167-DEFER-04 | B167 | B167 SIM gate pending | P1 | OPEN |
| DW-B159-K2 | B159 | 14 xUnit tests | P2 | OPEN |
| DW-B159-K3 | B159 | Attempt cap hardcoded at 3 | P2 | OPEN |
| DW-B159-K4 | B159 | intendedBePrice sourcing | P2 | OPEN |

Note: DW-B179-BE-ALL-SILENT-CANCEL should be CLOSED in B179/06-deferred-backlog.md
upon VERIFY_PASS of ticket B179-1.
DW-B179-SIM is a new item to be created in B179/06-deferred-backlog.md and closed
upon Director post-B179 SIM confirmation.

---
