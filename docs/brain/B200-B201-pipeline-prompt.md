You are the ptt-orchestrator for **Epic B200+B201 — Direct Repair Formalisation + Entry-Order Cancel Fix**.

### Mission

B200 has four direct repairs already applied to `src/PropTraderTools/CopyEngine.cs` and SIM confirmed.
Promote them into the formal pipeline with tests. Also fix one new P1 bug (B201) diagnosed from code.

### Work Directory

`docs/brain/B200/`

### Inputs

- `docs/brain/B200-orchestrator-prompt.md` — full B200 repair detail and prior ticket specs
- `docs/brain/B200-B201-orchestrator-prompt.md` — ticket definitions for all 4 tickets
- `src/PropTraderTools/CopyEngine.cs` — current state (B200 repairs already in code; B201 fix NOT yet applied)
- `src/PropTraderTools/Features/PttBreakEvenSwap.cs` — B201 call site (read only)
- `docs/standards/jane-street/RULES_CATALOG.md` — JS-001..JS-110 (mandatory pre-read for Ph1)
- `specs/002-trade-copier-spec.html` — B200 and B201 blocks already written (update to PIPELINE_COMPLETE on Ph5 pass)

### The 4 Tickets

**T1 — B200Tests.cs: xUnit tests for 4 already-applied B200 repairs (no src changes)**

All 4 repairs are already in CopyEngine.cs and SIM confirmed. This ticket adds formal test coverage only.

- `T_B200_01` — QTY-01: follower entry order Quantity=5 Filled=5 → drain payload qty=10 (Quantity+Filled). Asserts `SendCopyWithAtm` called with qty=10.
- `T_B200_02` — SFB-NULL-01: `_followerSlateReady[acc.Name]` is false before TryLaneDDispatchFollower → true after successful SendCopyWithAtm. `FindFollowerBracketOrder` returns non-null for Working bracket.
- `T_B200_03` — NAKED-01: TryNakedDetect receives Filled event for order.Name=="Entry" on follower → PTT-Flatten NOT submitted.
- `T_B200_04` — CLOSE-LANED-01 V13: `_flattenInFlightOrderId` = "acc#pending" (sentinel) set before `_flattenInFlight=true`. Stale Filled event with different OrderId does NOT clear flag. Matching Filled event does clear flag.

File: `src/PropTraderTools/Tests/B200Tests.cs` (new)

**T2 — CopyEngine.cs: B201-ENTRY-CANCEL-01 fix (3 touch points, CopyEngine.cs only)**

Root cause: `IsQxCancelCandidate` (~L994) `if (o.Name == "Entry") return true` is NOT scoped to follower accounts. NT8 names all ATM pending limit entries "Entry" — including the leader's own add-to-position limit. Every QX/BE button calls `CancelQxBrackets` on the leader account, cancelling it.

Fix — 3 touch points, CopyEngine.cs only:

1. `IsQxCancelCandidate` signature (~L982): add `bool isFollower` param.
2. `IsQxCancelCandidate` body (~L994): `if (o.Name == "Entry") return true` → `if (isFollower && o.Name == "Entry") return true`.
3. All callers of `IsQxCancelCandidate` within CopyEngine.cs (`CancelQxBrackets` 2-arg ~L1007, `CancelQxBrackets` 3-arg ~L1201, `BuildQxSnapshot` ~L1050): add `bool isFollower = IsFollowerAccount(acc);` at top of each, thread into `IsQxCancelCandidate(o, isFollower)` call.

Do NOT touch `PttBreakEvenSwap.cs` or any other Feature file — they call the 2-arg overload of `CancelQxBrackets(acc, instr)` which already has `acc`; no signature change propagates there.
CYC impact: +0 on all touched methods. No new branches. Roslyn counts `&&`-extended existing branch as 0 delta.

**T3 — B201Tests.cs: xUnit tests for B201 fix (no further src changes)**

- `T_B201_01` — leader "Entry" order (isFollower=false) → `IsQxCancelCandidate(o, false)` returns false.
- `T_B201_02` — follower "Entry" order (isFollower=true) → `IsQxCancelCandidate(o, true)` returns true.
- `T_B201_03` — ATM bracket names (Stop1, Target2, PTT-QX-Stop, PTT-BE-Target-1, PTT-CopyEntry) with isFollower=false → all return true (non-"Entry" names unaffected by isFollower).
- `T_B201_04` — `CancelQxBrackets(leaderAcc, instr)` with leader having "Entry" Working + "Stop1" TriggerPending → cancel list contains "Stop1" only, "Entry" NOT cancelled.

File: `src/PropTraderTools/Tests/B201Tests.cs` (new)

**T4 — Diagnostic log cleanup + deferred backlog**

Remove from `CopyEngine.cs` (temporary B200 diagnostic tags — confirm each line present before removing):
- `[PTT-ATM-CALLER]` log lines (5 occurrences: ~L2921 LaneD, ~L3080 LaneE, ~L4767 ReplaceOnAtmCancel, ~L7285 DrainFastPath, ~L7426 SubmitDrainedEntry)
- `[PTT-FLAT-OPEN]` log line (1 occurrence: inside IsFlattenInFlightOrRecent call site)

Do NOT remove: `[PTT-ATM-SEND]`, `[PTT-GATE5-BLOCKED]`, `[PTT-GATE2-BLOCKED]`, `[PTT-DR4-CLEAR]`, `[PTT-DR5-EVICT]`, `[PTT-ATM-ERROR]` — these are permanent operational telemetry.

Create `docs/brain/B200/06-deferred-backlog.md` with 3 deferred DW items:
- DW-B200-LANE-E-COPY-ENABLED: TryLaneEHook not gated on _isCopyEnabled. Low risk. Defer B202+.
- DW-B201-OPTION-B: Alternative rename follower "Entry" dispatch name to "PTT-Copy-Entry". Deferred pending Director decision.
- DW-B199-H2-CLEANUP-01: If B199 H2 divide remnant still in SendCopyWithAtm, remove. If already gone, close N/A.

### The Pipeline (MANDATORY — ALL 7 STEPS — NO EXCEPTIONS — NONE SKIPPABLE — NONE COMBINABLE)

```
Ph1   ptt-architect       → docs/brain/B200/02-architecture-plan.md
Ph2   ptt-plan-reviewer   → docs/brain/B200/02-plan-review.md          (REVIEW_PASS gate)
Ph3   ptt-architect       → docs/brain/B200/04-tickets.md
Ph3.5 ptt-ticket-reviewer → docs/brain/B200/04-ticket-review.md        (TICKET_REVIEW_PASS gate)
Ph4a  ptt-engineer        → src .cs edits + docs/brain/B200/ticket-N-completion.md (one per ticket)
Ph4b  ptt-verifier        → docs/brain/B200/ticket-N-verification.md   (VERIFY_PASS gate, one per ticket)
Ph5   ptt-plan-reviewer   → docs/brain/B200/05-final-review.md
                              docs/brain/B200/06-deferred-backlog.md
```

No phase may be skipped, combined, or abbreviated.
No direct .cs edits outside Ph4a. Small one-off edits require explicit Director approval.
Ph4a MUST read CopyEngine.cs before editing. B200 repairs are already in code — do NOT re-apply.
Ph4b MUST run `powershell -File .\scripts\ptt-sync.ps1` and confirm 19 linked 0 errors.
Ph4b MUST run `powershell -File .\scripts\verify_critical_symbols.ps1`.
Ph4b deletion audit: run `git diff HEAD~1 HEAD -- src/PropTraderTools/CopyEngine.cs | Select-String "^-    (private|internal|public)"` — every deleted symbol needs an authorising DW-card.
Single-hunk ban: `(git show HEAD -- CopyEngine.cs | Select-String "^@@").Count -eq 1` = VERIFY_FAIL always.
Ph5 MUST produce `06-deferred-backlog.md` with at minimum the 3 deferred items listed in T4.

### Jane Street Constraints (P0 — non-negotiable)

- JS-021: No `lock()` in any new or modified code. ConcurrentDictionary atomic ops only.
- JS-033: No `async void` (except NT8 event handler exemption).
- JS-066: All modified methods must have McCabe CYC ≤ 8 after edit.
- JS-001: No `throw new XxxException` in hot paths.
- xUnit only: All tests use `[Fact]`. No NUnit. No MSTest.
- ASCII only: No Unicode, emoji, or curly quotes in string literals.

Pre-edit gate (run before any .cs edit):
```powershell
Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "^\s*lock\s*\(" | Measure-Object
# Must be 0
Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "async\s+void\s+\w+\(" | Measure-Object
# Must be 0
powershell -File .\scripts\ptt-sync.ps1
# Must be 19/0
```

### Acceptance Criteria

- T1: 4 [Fact] tests GREEN. No src file modified.
- T2: `grep "isFollower && o.Name" CopyEngine.cs` = 1 match. `grep "bool isFollower = IsFollowerAccount" CopyEngine.cs` = 3 matches. CYC delta = 0 on all touched methods. All existing tests pass.
- T3: 4 [Fact] tests GREEN. No src file modified.
- T4: `grep "\[PTT-ATM-CALLER\]" CopyEngine.cs` = 0. `grep "\[PTT-FLAT-OPEN\]" CopyEngine.cs` = 0. `docs/brain/B200/06-deferred-backlog.md` exists with 3 deferred items.
- All: lock=0, async void=0, CYC≤8 all modified methods, ASCII-only, xUnit-only.
- PTT sync: 19 linked 0 errors after every Ph4a commit.
- `specs/002-trade-copier-spec.html` B200+B201 blocks updated to PIPELINE_COMPLETE on Ph5 pass.

### Start

Begin with Ph1 (ptt-architect). Read `docs/standards/jane-street/RULES_CATALOG.md` and `docs/brain/B200-orchestrator-prompt.md` before writing the architecture plan. B200 repairs are already in code and SIM confirmed — Ph1 reviews them for compliance only, does not redesign them. B201 fix is the only new code change.
