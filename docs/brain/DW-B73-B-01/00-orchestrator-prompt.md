# DW-B73-B-01/02 -- Orchestrator Prompt

**Pipeline ID**: DW-B73-B-01 + DW-B73-B-02 (combined single-pipeline run)
**Brain dir**: `docs/brain/DW-B73-B-01/`
**Date**: 2026-08-21
**Architecture plan**: `docs/brain/DW-B73-B-01/02-architecture-plan.md` (complete -- read before starting)

---

## Scope

Two P2 defects, combined into one pipeline run. Both defects are in
`src/PropTraderTools/TradeCopierPanel.cs`. No changes to `CopyEngine.cs` or any other file.

**DW-B73-B-01**: `RaiseBeAllDisarmed` redundant self-notification. The panel that calls
`UpdateBeAllVisuals(BeState.Idle)` directly (L587) also calls `RaiseBeAllDisarmed()` (L588),
which fires `OnGlobalBeAllDisarmed` back onto the SAME panel via `Dispatcher.InvokeAsync`,
causing a second redundant `UpdateBeAllVisuals(BeState.Idle)`. Fix: unsubscribe self before
raising, then re-subscribe after. OR (preferred -- simpler) just remove the unconditional
raise and use the existing `OnGlobalBeAllDisarmed` path correctly by NOT raising if this panel
is the only subscriber. See architecture plan for chosen approach.

**DW-B73-B-02**: `UpdateBeAllVisuals` and button construction call `MakeBrush(13, 148, 136)`
inline on every invocation. `MakeBrush` allocates a new `SolidColorBrush` and freezes it
each call. The teal color (13, 148, 136) has no cached `static readonly` field, unlike all
other semantic colors in the panel. Fix: add `BrushTeal` field and replace all 6 inline
`MakeBrush(13, 148, 136)` call sites.

---

## MANDATORY: Full 5-Phase PTT Pipeline

ALL phases are mandatory. None may be combined or skipped.

```
Ph1  ptt-architect        ->  02-architecture-plan.md        (ALREADY COMPLETE -- do not redo)
Ph2  ptt-plan-reviewer    ->  02-plan-review.md              (REVIEW_PASS gate)
Ph3  ptt-architect        ->  04-tickets.md
Ph3.5 ptt-ticket-reviewer ->  04-ticket-review.md            (TICKET_REVIEW_PASS gate)
Ph4a ptt-engineer         ->  src .cs edits + ticket-N-completion.md
Ph4b ptt-verifier         ->  ticket-N-verification.md       (VERIFY_PASS gate)
Ph5  ptt-plan-reviewer    ->  05-final-review.md + 06-deferred-backlog.md
```

**Phase 1 (architecture plan) is already written** at `docs/brain/DW-B73-B-01/02-architecture-plan.md`.
The pipeline begins at **Phase 2 (ptt-plan-reviewer)**.

---

## SRC CODE BAN

You are **BANNED** from editing any `.cs` file except via Phase 4a (ptt-engineer).
Phase 4a requires explicit `TICKET_REVIEW_PASS` from Phase 3.5 before any `.cs` edit.

---

## Artifacts expected at pipeline end

```
docs/brain/DW-B73-B-01/
  00-orchestrator-prompt.md       (this file)
  02-architecture-plan.md         (Phase 1 -- complete)
  02-plan-review.md               (Phase 2 output)
  04-tickets.md                   (Phase 3 output)
  04-ticket-review.md             (Phase 3.5 output)
  ticket-1-completion.md          (Phase 4a T1 output)
  ticket-1-verification.md        (Phase 4b T1 output)
  ticket-2-completion.md          (Phase 4a T2 output)
  ticket-2-verification.md        (Phase 4b T2 output)
  05-final-review.md              (Phase 5 output)
  06-deferred-backlog.md          (Phase 5 output)
```

---

## Commit message template

```
fix(ptt): DW-B73-B-01+02 BeAllDisarmed self-notify + BrushTeal cache [NNN tests]
```

where NNN is the actual [Fact] count after all edits (expected 295 -> 298).

---

## Key source locations (HEAD d15709be baseline)

```
TradeCopierPanel.cs
  L267-272   MakeBrush(byte r, byte g, byte b) -- allocates + freezes
  L243       BrushPurple   static readonly (existing pattern to follow)
  L264       BrushConnected static readonly (existing pattern)
  L276-279   BrushActive/Danger/Caution/Inactive static readonly (existing pattern)
  L587-588   UpdateButtonColors -- Idle update + RaiseBeAllDisarmed (DW-B73-B-01 site)
  L944-946   OnGlobalBeAllDisarmed -- Dispatcher.InvokeAsync -> UpdateBeAllVisuals Idle
  L952-964   UpdateBeAllVisuals -- Idle branch: MakeBrush(13,148,136) x2 (DW-B73-B-02 site)
  L957       _globalBeBtn2.BorderBrush = MakeBrush(13,148,136)  -- T2 fix site
  L958       _globalBeBtn2.Foreground  = MakeBrush(13,148,136)  -- T2 fix site
  L1049      _beBtn2 BorderBrush       = MakeBrush(13,148,136)  -- T2 fix site
  L1050      _beBtn2 Foreground        = MakeBrush(13,148,136)  -- T2 fix site
  L1078      _globalBeBtn2 BorderBrush = MakeBrush(13,148,136)  -- T2 fix site
  L1079      _globalBeBtn2 Foreground  = MakeBrush(13,148,136)  -- T2 fix site
  L1111      (TBD -- verify in Ph4a read)
  L1140      (TBD -- verify in Ph4a read)
```

---

## Test count baseline

295 [Fact] at HEAD d15709be. Expected after pipeline: 298 (3 new [Fact] for DW-B73-B-01/02).
Actual count confirmed by ptt-verifier after edits.
