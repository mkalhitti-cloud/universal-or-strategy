# PTT Phase 4a/4b Execution Protocol — One Agent Per Ticket

**Version**: 1.0
**Created**: 2026-09-07
**Authority**: Director mandate — embedded in ptt-orchestrator roleDefinition
**Scope**: All Ph4a (ptt-engineer) and Ph4b (ptt-verifier) ticket executions

---

## The Rule (non-negotiable)

**ONE AGENT SESSION = ONE TICKET. Always. No exceptions.**

Every Ph4a (engineer) ticket execution is a separate `start_subtask` call.
Every Ph4b (verifier) ticket verification is a separate `start_subtask` call.

A single ptt-engineer session MUST NOT implement more than one ticket.
A single ptt-verifier session MUST NOT verify more than one ticket.

---

## Why This Rule Exists

When one agent handles multiple tickets in sequence:

1. **Context cross-contamination**: The agent carries state from Ticket 1 into
   Ticket 2. Scope decisions, CYC estimates, and naming conventions drift.

2. **Incomplete per-ticket scans**: The 7-scan checklist is per-ticket by design.
   A combined session produces one scan over both tickets, masking violations in
   whichever ticket received less attention.

3. **Verification gaps**: A ptt-verifier reading two completion files simultaneously
   cannot reliably cross-reference each against its own ticket spec. Findings are
   attributed to the wrong ticket or dropped entirely.

4. **Irreversible compaction**: If the combined session hits the context limit mid-way
   through Ticket 2, Ticket 1 state is lost and the session cannot be restored cleanly.

5. **Pipeline traceability breaks**: Each ticket has its own `ticket-N-completion.md`
   and `ticket-N-verification.md`. These must each be written by a dedicated agent
   with full attention on exactly that ticket. Mixed sessions produce mixed artifacts.

---

## Correct Execution Pattern

For a pipeline with N tickets:

```
Ticket 1:
  start_subtask(mode="ptt-engineer",  message="TICKET 1 ONLY. Do NOT read ticket 2.")
  -- returns BUILD_PASS --
  start_subtask(mode="ptt-verifier",  message="VERIFY TICKET 1 ONLY.")
  -- returns VERIFY_PASS --

Ticket 2:
  start_subtask(mode="ptt-engineer",  message="TICKET 2 ONLY. Do NOT read ticket 1.")
  -- returns BUILD_PASS --
  start_subtask(mode="ptt-verifier",  message="VERIFY TICKET 2 ONLY.")
  -- returns VERIFY_PASS --

... (repeat for tickets 3..N)

Phase 5:
  start_subtask(mode="ptt-plan-reviewer") reads ALL completion + verification files.
```

**Ticket N is never started until Ticket N-1 reaches VERIFY_PASS.**
(Tickets are sequential, not parallel — they share the same .cs file.)

---

## Mandatory ptt-orchestrator Message Format

Every `start_subtask` to `ptt-engineer` MUST include this header verbatim:

```
SCOPE LOCK — TICKET [N] ONLY.
Do NOT read, reference, or implement any other ticket in this session.
Files in scope: 04-tickets.md (ticket [N] section only), 04-ticket-review.md,
  02-architecture-plan.md, RULES_CATALOG.md, [specific .cs file].
Write: docs/brain/{epic}/ticket-[N]-completion.md
Return: BUILD_PASS | BUILD_FAIL
```

Every `start_subtask` to `ptt-verifier` MUST include this header verbatim:

```
SCOPE LOCK — VERIFY TICKET [N] ONLY.
Do NOT read ticket-[N-1]-completion.md or ticket-[N+1] files in this session.
Files in scope: ticket-[N]-completion.md, 04-tickets.md (ticket [N] only),
  02-architecture-plan.md, RULES_CATALOG.md, spec (read-only).
Write: docs/brain/{epic}/ticket-[N]-verification.md
Return: VERIFY_PASS | VERIFY_FAIL
```

---

## Retry Rules (unchanged from original protocol)

- BUILD_FAIL: re-spawn ptt-engineer with error log. Max 2 retries.
  After 2 retries: STOP. Escalate to Director.

- VERIFY_FAIL: re-spawn ptt-engineer with verification report. Max 3 cycles.
  After 3 VERIFY_FAIL: STOP. Escalate to Director.

Each retry is ALSO a fresh independent agent session (same scope lock applies).

---

## Gate: ptt-orchestrator Self-Check Before Ph5

Before spawning Ph5 (ptt-plan-reviewer), the orchestrator MUST verify:

```
For each ticket N (1..total):
  [ ] ticket-N-completion.md exists
  [ ] ticket-N-verification.md exists and contains VERIFY_PASS
  [ ] Each file was written by a dedicated single-ticket session
      (check: completion.md references only ticket N scope)
```

If any ticket is missing its completion or verification artifact: STOP.
Do not spawn Ph5. Re-run the missing Ph4a or Ph4b session.

---

## Reference

- `docs/brain/COPIER-SESSION-LOOP.md` — Rule 1: One Agent Per Ticket
- `docs/protocol/PTT_PIPELINE_LANE_SPLIT_PROTOCOL.md` — lane-split gate
- `.bob/custom_modes.yaml` ptt-orchestrator — enforces this rule in Ph4 loop
