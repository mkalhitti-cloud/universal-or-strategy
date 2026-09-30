# PTT Pipeline Lane-Split Decision Protocol

**Version**: 1.0
**Created**: 2026-09-07
**Authority**: Director mandate — embedded in COPIER-SESSION-LOOP.md V2.2
**Scope**: All PTT pipeline planning decisions in ptt-architect (Ph1)

---

## Purpose

This protocol governs when a set of fixes is split into parallel lanes (LaneA + LaneB)
versus kept as a single pipeline with multiple tickets. Incorrect splitting has caused
repeated SIM failures and architecture review delays (see B131, B133 retrospective).

**Default: single pipeline. Lanes are the exception, not the rule.**

---

## The Decision Gate (mandatory — answer before ANY lane decision)

Answer all four questions in order. Stop as soon as the condition is met.

### Q1 — Proximity
Do all fixes touch the same method OR are they within 50 lines of each other?

  YES → **SINGLE PIPELINE**. Co-located changes interact at the diff level.
         The plan-reviewer and ticket-reviewer cannot evaluate them in isolation.
         STOP. Do not evaluate Q2-Q4.

### Q2 — Design Dependency
Does the architect need to know Fix A's final design to correctly design Fix B?

  YES → **SINGLE PIPELINE**. Sequential design dependency means Fix B cannot be
         correctly specified before Fix A is resolved. Parallel lanes would produce
         an incomplete or incorrect architecture plan for Fix B.
         STOP. Do not evaluate Q3-Q4.

### Q3 — Standalone Value
If one fix is blocked at review (REVIEW_FAIL), does the other still have
standalone value that can be shipped independently?

  NO  → **SINGLE PIPELINE**. If neither fix delivers value alone, they must travel
         together. Splitting only creates coordination overhead with no benefit.
         STOP. Do not evaluate Q4.

### Q4 — SIM Independence
Can a meaningful SIM gate be run on each fix independently, without the other?

  NO  → **SINGLE PIPELINE**. If both fixes must be present for the SIM gate to be
         meaningful, run them together under one pipeline so the SIM gate is clean.

### LANES ALLOWED only when:
  - Q1: NO (fixes are in different methods, more than 50 lines apart)
  - Q2: NO (Fix B can be fully specified without knowing Fix A's final form)
  - Q3: YES (each fix ships useful value independently if the other is blocked)
  - Q4: YES (each fix has its own verifiable SIM path)

---

## Correct Split Patterns

### SINGLE PIPELINE — Use When:
- Fixes are in the same method or call chain
- One fix sets up state the other fix reads
- Both fixes are required before any SIM gate is meaningful
- CYC impact must be assessed across both changes together
- Either fix is diagnostic only (no .cs logic change)

### LANE SPLIT — Use When:
- Fixes are in genuinely different subsystems (e.g. entry copy vs bracket sync)
- Each fix has its own independent SIM verification path
- Each fix can be reviewed and approved without knowledge of the other
- Both fixes are P0 urgency and blocking each other in a single queue is harmful

---

## Empirical Calibration (B129-B133)

| Block | Decision | Correct? | Root Cause if Wrong |
|-------|----------|----------|---------------------|
| B129  | Lanes    | YES      | Different methods, different call trees, independent SIM paths |
| B130  | Lanes    | YES      | LaneB was diagnostic only — zero .cs logic change, independent |
| B131  | Lanes    | NO       | Same method (SyncAtmFollowerTarget), neither fix useful alone, Q3=NO |
| B132  | Lanes    | YES      | LaneB diagnostic/read-only, zero .cs change, independent observation |
| B133  | Lanes    | NO       | Both edits in FindFollowerBracketOrder, 36 lines apart (Q1=YES), CYC must be assessed together, both required before SIM (Q4=NO) |

**B131 and B133 were avoidable.** Applying this gate before Ph1 would have
identified both as single-pipeline cases immediately.

---

## ptt-architect Instruction (embed in every Ph1 prompt)

Before producing the architecture plan, apply the Lane-Split Decision Gate:

```
LANE-SPLIT GATE (mandatory — answer before structuring the plan):
  Q1. Same method or within 50 lines? YES -> single pipeline.
  Q2. Fix B design depends on Fix A final design? YES -> single pipeline.
  Q3. Each fix has standalone value if the other is blocked? NO -> single pipeline.
  Q4. Each fix has an independent SIM path? NO -> single pipeline.
  Default: single pipeline.
  State result: SINGLE-PIPELINE or LANES-APPROVED (with Q1-Q4 answers).
```

The architect MUST state the gate result explicitly in 02-architecture-plan.md
before any design content. Format:

```
LANE-SPLIT GATE RESULT: [SINGLE-PIPELINE | LANES-APPROVED]
  Q1 (proximity):        [YES/NO] — [reason]
  Q2 (design dep):       [YES/NO] — [reason]
  Q3 (standalone value): [YES/NO] — [reason]
  Q4 (SIM independence): [YES/NO] — [reason]
Decision: [explanation]
```

---

## ptt-plan-reviewer Enforcement

The plan-reviewer (Ph2) MUST reject the plan (REVIEW_FAIL) if:
- The plan uses lanes but the LANE-SPLIT GATE RESULT is missing from the plan
- The plan uses lanes but Q1 or Q2 was YES (gate violation)
- The plan uses lanes but Q3 or Q4 was NO (gate violation)
- The plan is single-pipeline but no gate result is stated

Gate violations are REVIEW_FAIL, not warnings.

---

## Reference

- `docs/brain/COPIER-SESSION-LOOP.md` — Rule 2: Lane-Split Decision Gate
- `docs/protocol/PHASE5_EXECUTION_PROTOCOL.md` — Rule 1: 1-agent-per-ticket
- `.bob/custom_modes.yaml` ptt-orchestrator — enforces gate at Ph1 spawn
