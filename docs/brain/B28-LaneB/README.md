# B28 Lane B — DW-B28-02 Leader-Account Overloads

Block: B28 | Lane: B | Defect: DW-B28-02 (P1)
Status: PIPELINE IN PROGRESS

## Defect
Cancel, Trim, and Flatten buttons are all silent no-ops.
Root-cause: AllAccounts(instrument) returns 0 accounts because _rules is empty
("Apply Rule" was never clicked). Panel passes only Instrument — no Account.
AllAccounts searches _rules ConcurrentBag -> FindRule -> empty -> yield break.

## Fix
Add leader-account overloads for Trim, Flatten, CancelPendingEntries.
Panel passes _leaderAccount directly. No dependency on _rules.
Pattern mirrors BreakEven(Account, Instrument, int) already at CopyEngine.cs ~L1216.

## Files
- 02-architecture-plan.md  (ptt-architect output — pending)
- 04-tickets.md            (ptt-architect output — pending)
- ticket-1-completion.md   (ptt-engineer output — pending)
- ticket-1-verification.md (ptt-verifier output — pending)

## Target
[Fact] baseline: 135 (after Lane A) | target: 138 (+3 reflection tests)
Tests: T_B28_01, T_B28_02, T_B28_03 (overload existence via reflection)
Files changed: CopyEngine.cs (3 helpers + 5 overloads) + TradeCopierPanel.cs (3 call sites)

## Dependency
Lane B Phase 5 (engineer) MUST WAIT for Lane A BUILD_PASS.
Phases 2 and 4 may run in parallel with Lane A.
