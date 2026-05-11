# T0 — Pre-Merge: Register Phase 6 in master_roadmap.md

## Scope & Objective

**Single sentence**: Add a "Phase 6: Hot Path Execution Hardening" row to file:docs/brain/master_roadmap.md and stamp it as IN PROGRESS, **before** any code PR for T1-T3 lands.

**In scope**:

- Edit file:docs/brain/master_roadmap.md — add a new section "## CURRENT MISSION: PHASE 6 -- HOT PATH EXECUTION HARDENING" right above the "ADR-020 PHASE GATE STATUS" section.
- Add a row in "THE 4 REFACTORING PHASES -- STATUS" table renaming it to "THE 5 REFACTORING PHASES -- STATUS" and adding `Phase 6 | Hot Path Execution Hardening (T1/T2/T3 god-function extraction) | 🟡 IN PROGRESS`.
- Update the "HOTSPOT MAP" rows for `ManageTrailingStops`, `ExecuteSmartDispatchEntry`, and `ProcessOnExecutionUpdate (Orders.Callbacks.Execution.cs)` with status `Phase 6` / `IN PROGRESS`.
- Reference the new Epic + spec IDs (epic:d897fcf5-7eec-48e1-87cc-43d34a8ca7b7, the Brief and Approach spec IDs).
- Bump `Last Synced` and `Current Build` timestamps per existing pattern.

**Out of scope**:

- Any `src/*.cs` change.
- `docs/architecture.md` heatmap refresh (deferred to T4 per A5=C — heatmap CYC numbers update only after extractions are verified).
- `docs/brain/implementation_plan.md` overwrite (deferred to T4).

## References

- **Analysis** spec:d897fcf5-7eec-48e1-87cc-43d34a8ca7b7/1088e442-9ac4-475d-9901-216ec5528e94 §4 Change Surface Area (`docs/brain/master_roadmap.md` listed UPDATE).
- **Approach** §1.2 Transition (T0 lands FIRST per A5=C); §2 Target State (post-Phase-6 master_roadmap shows Phase 6 row registered + then marked complete in T4).
- Risk hotspot **none** (DOC-only, zero src/ touch).

## Guardrails

- ASCII gate on all added markdown (use `--` not em-dash; no curly quotes).
- Diff < 5 KB (DOC-only, narrow surgical insert).
- Do NOT touch any other section of the roadmap (e.g., the Build-984 status, the M3-M9 milestone table, the agent role table — these stay as-is).

## Acceptance Criteria

- `git diff docs/brain/master_roadmap.md` shows ONLY: (a) new "PHASE 6" mission section, (b) new Phase 6 row in the phases status table, (c) updated status column on the 3 Hotspot Map rows, (d) refreshed `Last Synced` and `Current Build` headers.
- `git diff --stat HEAD` shows zero src/ files touched.
- `python check_ascii.py docs/brain/master_roadmap.md` returns PASS.

## Verification Steps

1. `git diff docs/brain/master_roadmap.md` -- visual review.
2. `python check_ascii.py docs/brain/master_roadmap.md` -- ASCII PASS.
3. PR opens with title `phase-6-t0-roadmap-registration` and merges to `main` BEFORE any T1.x branch is opened.