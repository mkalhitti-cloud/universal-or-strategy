# T4 — Final Acceptance: Verbatim Print + CYC Gates + architecture.md + implementation_plan.md

## Scope & Objective

**Single sentence**: Run the cross-cutting verification gates (verbatim Print fidelity + CYC verification) across all touched files from T1.A through T3.D, then update file:docs/architecture.md (heatmap CYC refresh + `OnOrderUpdate` placement bug fix) and overwrite file:docs/brain/implementation_plan.md with the Phase 6 plan + final close-out, and stamp Phase 6 as DONE in file:docs/brain/master_roadmap.md.

**In scope**:

- Run `python scripts/csharp_hotspots.py` and capture output; ASSERT each new sub-handler < 20 CYC, each parent (`ManageTrailingStops`, `ProcessOnExecutionUpdate`, `ExecuteSmartDispatchEntry`) < 30 CYC.
- Run `python check_ascii.py` on every src/ file touched in T1.A-T3.D; ASSERT PASS.
- Run `grep -rn "(?` AND add a separate row for `OnOrderUpdate (cluster)` correctly placed at `V12_002.Orders.Callbacks.cs`.
  - Refresh CYC numbers for `ManageTrailingStops`, `ExecuteSmartDispatchEntry`, `ProcessOnExecutionUpdate` based on T4 measurement output.
  - Update the `Phase 5/6 Status` heading and the "Status" column for the affected rows.
  - Refresh the mermaid diagram subgraph LOC/CYC labels for `Trailing_Main`, `Exec_Logic`, `SIMA_Disp`.
- Overwrite file:docs/brain/implementation_plan.md with the Phase 6 plan: copy the Approach §1-5 outline + the 11 ticket summaries (T0..T4), per the Phase 5 implementation_plan.md format precedent.
- In file:docs/brain/master_roadmap.md:
  - Flip the Phase 6 row in "THE 5 REFACTORING PHASES -- STATUS" table from `🟡 IN PROGRESS` to `✅ DONE`.
  - Update the Hotspot Map status column for the 3 targets to `✅ Phase 6 Complete`.
  - Bump `Last Synced` and `Current Build` headers.

**Out of scope**: Any new src/ extraction — this ticket is purely the verification gate + docs sync. Build-984 work, M4 Rithmic sidecar, M5 zero-alloc deeper work.

## References

- **Analysis** §3 Test Coverage; risk hotspots **H11** (verbatim Print), **H12** (architecture.md placement bug).
- **Approach** §5 Test Strategy (verification gates); §1.6 (H11/H12 mitigations); §2 Target State (final post-state).
- **Brief** §5 Constraints C6 (verbatim Print) and C7 (CYC gate).

## Guardrails

- DO NOT touch any src/ file in this ticket (read-only verification + docs only).
- ASCII gate on all updated docs.
- Diff < 30 KB total (mostly docs).
- The CYC report output is captured into `docs/brain/phase6_cyc_report.md` as evidence.
- Verbatim Print verification is a per-file `grep -cn` checklist; the PR description includes the full table of (target Print string, expected count, actual count).
- If ANY gate fails, this ticket BLOCKS the Phase 6 close-out — re-open the failing T1/T2/T3 ticket as a follow-up before merging T4.

## Acceptance Criteria

- `python scripts/csharp_hotspots.py` output shows:
  - `ManageTrailingStops` < 30 CYC.
  - `ManageTrail_AdaptiveThrottleTick`, `ManageTrail_RunPerTradeBranches`, `ManageTrail_RunPointBasedTrailing`, `ManageTrail_RunFleetSymmetrySync` each < 20 CYC.
  - `ProcessOnExecutionUpdate` ≤ 12 CYC.
  - `ProcessOnExecution_FinalizeFullClose`, `HasPendingEntryForAcct`, `HasUnfilledActivePositionForAcct` each < 10 CYC.
  - `ExecuteSmartDispatchEntry` < 30 CYC.
  - `Dispatch_ResolveFleetSnapshot`, `Dispatch_BuildFollowerOrders`, `Dispatch_PublishMarketBracketToPhoton`, `Dispatch_PublishLimitEntryToPhoton` each < 20 CYC.
- `python check_ascii.py src/V12_002.Trailing.cs src/V12_002.Orders.Callbacks.Execution.cs src/V12_002.SIMA.Dispatch.cs` PASS.
- `grep -rn "(?` (significantly lower than 151) with status `🟢 Phase 6 Optimized`.
- file:docs/brain/master_roadmap.md shows Phase 6 ✅ DONE.
- file:docs/brain/implementation_plan.md is overwritten with the Phase 6 ticket summary.
- file:docs/brain/phase6_cyc_report.md exists with the captured `csharp_hotspots.py` output.

## Verification Steps

1. Run `python scripts/csharp_hotspots.py > docs/brain/phase6_cyc_report.md` -- inspect.
2. Run `python check_ascii.py src/V12_002.Trailing.cs src/V12_002.Orders.Callbacks.Execution.cs src/V12_002.SIMA.Dispatch.cs docs/architecture.md docs/brain/master_roadmap.md docs/brain/implementation_plan.md docs/brain/phase6_cyc_report.md` -- PASS.
3. Run the verbatim Print grep checklist (manual or scripted).
4. Run `powershell -File .\scripts\lint.ps1` -- Codacy/DeepSource regression delta = 0 vs T3.D baseline.
5. Run `powershell -File .\scripts\test_stress.ps1` -- Risk Audit Cases 1-7 PASS.
6. Run `powershell -File .\deploy-sync.ps1` -- EXIT 0 (in case any docs are hard-linked).
7. Director runs 4-session live NT8 replay (Apr 29 - May 5 reference) and confirms no behavioral drift before approving merge to main.
8. BUILD_TAG bumped to `1111.006-phase-6-complete`.
9. PR title: `phase-6-t4-final-acceptance-and-docs`. Merge after Director approval.