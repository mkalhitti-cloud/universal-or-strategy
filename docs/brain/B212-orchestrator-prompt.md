# B212 Orchestrator Prompt (UPDATED 2026-08-10)
# PTT-COPIER-B212: BE-ALL Follower Bracket Sync — 2 Code Bugs
# Re-validated in fresh session: all code paths read directly from CopyEngine.cs
# Director approved — pipeline ready

---

## VALIDATED DIAGNOSIS (DO NOT RE-INVESTIGATE)

### CORRECTED: Gate 2 Blocks Are Expected — NOT a Config Bug

Previous diagnosis incorrectly concluded Gate 2 blocks meant "no copy rule for MGC DEC26."

**Corrected finding (code read 2026-08-10):**
- `FindMatchingRule` (CopyEngine.cs L2336): checks `order.Account.Name == rule.MasterAccount?.Name`
- Gate 2 ALWAYS blocks follower accounts (Sim102/103/104 are not MasterAccount) — by design
- Proof rule IS loaded: `[PTT-ATM-SEND] qty=10 obj=set` at log lines 9–11 proves Gate 2 passed for Sim101
- No config action needed. The rule for MGC DEC26 is in `_rules`.

### DW-B212-01: HandleBracketChange PTT-BE-* guard missing (P1)

**Code confirmed:**
- `HandleBracketChange` (L4480): no guard for `leaderOrder.Name.StartsWith("PTT-BE-")`
- `IsBracketLegStatic` (L6460): returns true for any `order.Name.StartsWith("PTT-")` → `IsWorkingBracket=true` for PTT-BE-Stop-1 → TryHandleBracketDrag fires
- When Sim101's PTT-BE-Stop-1 reaches Accepted, TP3-HBC fires → `SyncFollowerBracket(Sim102, null, true, "PTT-BE-Stop-1")` → `FindFollowerBracketOrder` → fo=NULL (Sim102 doesn't have PTT-BE-Stop-1 yet, its swap starts concurrently at line 190)
- `MatchesLeaderName` (L4653): only matches ATM names + PTT-STP-Drag-N / PTT-TGT-Drag-N → PTT-BE-Stop-1 matches nothing
- **Architectural fact**: PTT-BE prices are set by each account's own swap. Cross-account sync is wrong and impossible at this tick.

**Fix design:**
```csharp
// In HandleBracketChange (L4480), after instrument == null guard, before foreach:
// B212-DW01: PTT-BE-* brackets managed independently by PttBeSwapAll per account.
// Cross-account price-sync via TP3-HBC is architecturally incorrect.
if (leaderOrder.Name != null
    && leaderOrder.Name.StartsWith("PTT-BE-", StringComparison.Ordinal))
    return; // +1 branch: CYC 6->7, within JS-028 limit of 8
```

**McCabe:** CYC 6 → 7. Still ≤ 8 per JS-028. No extraction required.
**Test:** 1 `[Fact]` in `B212Tests.cs`: leaderOrder.Name="PTT-BE-Stop-1" → HandleBracketChange returns early, SyncFollowerBracket never called.

### DW-B212-03: SubmitDrainedEntry fires during BE swap (P0 — MOST URGENT)

**Evidence:** `[PTT-ATM-SEND] acct=Sim102 qty=9 tmpl= obj=set` at output.md line 391 (also lines 432, 449 for Sim103/104).

**Root cause (suspected — confirm in pipeline):**
- `OnDrainCancelAck` trigger (L1673–1674): fires when ANY Cancelled order arrives for account in `_pendingDispatchDrains`
- During PttBeSwapAll, Sim102's ATM brackets (SimStop1, SimStop2, Target1, Target2) are cancelled
- If Sim102 has an active `_pendingDispatchDrains` entry at that moment, those bracket cancels trigger drain completion
- → `SubmitDrainedEntry` → `SendCopyWithAtm(Sim102, qty=9)` — spurious re-entry mid-trade
- qty=9 = original qty=10 minus 1 partial fill on PTT-BE-Target-1 on Sim101

**Proposed fix options (architect to choose):**
Option A: In `SubmitDrainedEntry` (L7726), add guard: `if (IsBeSwapInProgress(follower.Name)) return;`
Option B: In PttBeSwapAll, before cancelling brackets for account X: `_pendingDispatchDrains.TryRemove(acct.Name, out _);`

**Impact:** Followers get new entry orders (qty=9) mid-trade during BE swap. Position counts wrong, bracket tracking confused.

---

## TASK: FULL B212 PIPELINE

Two tickets, all 7 phases mandatory, none skippable, none combinable.

### PIPELINE (copy verbatim — no shortcuts):

Ph1  ptt-architect       → docs/brain/B212/02-architecture-plan.md
                            (2 tickets: DW-B212-01 HandleBracketChange guard + DW-B212-03 drain guard)
Ph2  ptt-plan-reviewer   → docs/brain/B212/02-plan-review.md
                            Gate: REVIEW_PASS required before Ph3
Ph3  ptt-architect       → docs/brain/B212/04-tickets.md
                            (T1: DW-B212-01, T2: DW-B212-03)
Ph3.5 ptt-ticket-reviewer → docs/brain/B212/04-ticket-review.md
                            Gate: TICKET_REVIEW_PASS required before Ph4a
Ph4a ptt-engineer        → src .cs edits
                            + docs/brain/B212/ticket-1-completion.md (DW-B212-01)
                            + docs/brain/B212/ticket-2-completion.md (DW-B212-03)
Ph4b ptt-verifier        → docs/brain/B212/ticket-1-verification.md (VERIFY_PASS)
                            + docs/brain/B212/ticket-2-verification.md (VERIFY_PASS)
Ph5  ptt-plan-reviewer   → docs/brain/B212/05-final-review.md
                            + docs/brain/B212/06-deferred-backlog.md

NO SHORTCUTS. NO DIRECT .cs EDITS. NO PHASE SKIPPING.
SRC CODE BAN: all .cs edits go through this full pipeline only.

---

## FILES TO READ IN EACH PHASE

### Ph1 + Ph3 (architect):
- `src/PropTraderTools/CopyEngine.cs`
  - `HandleBracketChange` (L4480–4513) — DW-B212-01 target
  - `SyncFollowerBracket` (L3557–3660) — called by HandleBracketChange
  - `SubmitDrainedEntry` (L7726–7760) — DW-B212-03 Option A target
  - `OnDrainCancelAck` (L1673–1674) — DW-B212-03 Option B target
  - `IsBeSwapInProgress` — confirm it's available as guard predicate
  - `IsBracketLegStatic` (L6460) — explains why PTT-BE-Stop-1 fires TryHandleBracketDrag
- `docs/brain/B212-orchestrator-prompt.md` — this file (diagnosis)
- `docs/brain/output.md` — evidence log

### Ph4a (engineer):
- Read HandleBracketChange current source before editing
- Read SubmitDrainedEntry current source before editing
- Verify CYC of HandleBracketChange stays ≤ 8 after +1 branch
- Write B212Tests.cs (1+ [Fact])

### Ph4b (verifier):
- Run ptt-sync: must be 21/0
- Run tests: zero new regressions
- Verify `[TP4-SFB-NULL]` no longer appears for PTT-BE-* leaderName in SIM
- Verify `[PTT-ATM-SEND] qty=9` no longer fires during BE swap

---

## CONSTRAINTS (Jane Street / project rules)

- JS-021: no lock() — all state via ConcurrentDictionary/Interlocked
- JS-028: CYC ≤ 8 per method
- JS-001: no throw in hot path
- JS-002: no return null for missing values (existing pattern preserved)
- ASCII-only: no Unicode in string literals
- xUnit only: [Fact], no NUnit/MSTest

---

## OUTPUT DIRECTORY

All pipeline documents → `docs/brain/B212/`

Required files:
- 02-architecture-plan.md
- 02-plan-review.md
- 04-tickets.md
- 04-ticket-review.md
- ticket-1-completion.md
- ticket-1-verification.md
- ticket-2-completion.md
- ticket-2-verification.md
- 05-final-review.md
- 06-deferred-backlog.md

---

## SPEC REFERENCE

Spec: file:///C:/WSGTA/universal-or-strategy-director/specs/002-trade-copier-spec.html#b212
DW-B212-01: HandleBracketChange PTT-BE-* early-return guard (P1)
DW-B212-02: Gate 2 diagnostic text improvement (P3, deferred)
DW-B212-03: SubmitDrainedEntry BE-swap guard (P0 CRITICAL, most urgent)
