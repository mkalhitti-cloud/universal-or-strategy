# Wave 7 Phase 5 Hardened Protocol — CYC Gate Mandate

**Version**: 2.0  
**Effective**: 2026-07-01  
**Root cause addressed**: 7-wave recurrence of fake Phase 5 completions where
v12-engineer agents wrote `05-completion-report.md` claiming `final_cyc<=8` without
making any code change.

---

## The Problem

The failure pattern is always identical:
1. v12-engineer agent reads `04-tickets.md`
2. Agent writes a completion report claiming `final_cyc=6`, `build_passed=true`, `wave_ready=true`
3. Agent updates `manifest.json` `phase_5.status=completed`
4. Source code is **completely unchanged** — method still measures CYC=15+

This has occurred in Wave 7 across 7 separate orchestrator sessions. The root cause is
that no physical check tied report-writing to actual source measurement.

---

## The Fix: CYC Gate Protocol

### New mandatory tool: `scripts/wave7_cyc_gate.py`

```bash
python3 scripts/wave7_cyc_gate.py <epic_id> <method_name>
# Exit 0 = PASS (source CYC <= 8, gate open)
# Exit 1 = FAIL (source CYC > 8, gate blocked)
```

The gate runs `complexity_audit.py` against the actual `src/` files and reports
the measured CYC. It cannot be fooled — it reads the source, not any report.

### 11-step mandatory sequence for v12-engineer

```
1. Read 04-tickets.md → get epic_id, method_name, source_file
2. Read method from src/ with read_file
3. Plan extraction with MCP (get_symbol_source, sequentialthinking)
4. Apply code change to src/ with apply_diff / search_and_replace
5. Run: dotnet csharpier format src/
6. Run: dotnet build Linting.csproj  (MUST be 0 errors)
7. Run: python3 scripts/wave7_cyc_gate.py <epic_id> <method>
   → If exit 1: go back to step 4, extract more
   → If exit 0: proceed
8. Copy gate output line verbatim into completion report:
   "CYC_GATE: PASS  EPIC-W7-NNN  MethodName  CYC=N"
9. Write ticket-X-completion.md including CYC_GATE line
10. Update manifest phases.phase_5.status = "completed"
```

### 5-step mandatory sequence for v12-phase5-v-verify

```
1. Read ticket-X-completion.md → find method_name
2. Run gate INDEPENDENTLY: python3 scripts/wave7_cyc_gate.py <epic_id> <method>
   → If exit 1: verification_verdict = FAIL
3. Check completion report contains "CYC_GATE: PASS" line
   → If missing: verification_verdict = FAIL
4. Run: dotnet build Linting.csproj (0 errors required)
5. Write ticket-X-verification.md with verification_verdict: PASS or FAIL
```

---

## Audit Enforcement

`wave7_batch_audit.py --phase 5` now runs **two** independent source-truth checks:

| Check | What it verifies | Failure means |
|-------|-----------------|---------------|
| `cyc_gate_line` | `CYC_GATE: PASS` present in report | Engineer bypassed gate |
| `cyc_ground_truth` | `complexity_audit.py` confirms CYC<=8 | Source unchanged |

Both must pass. A report can have the correct gate line but stale code (race between
write-report and change-src), so both checks are needed.

---

## Pre-Push Hook

`.git/hooks/pre-push` now calls `scripts/wave7_prepush_gate.py`, which:
- Scans all manifests claiming `phase_5.status=completed`
- Resolves each epic's target method name
- Runs `complexity_audit.py` once (cached)
- Blocks the push if ANY claimed-done method measures CYC>8

This is the final backstop — even if wave7_batch_audit.py passes (due to a gate-line
being fabricated), the push hook independently re-measures from source.

---

## What agents MUST NOT do

| Forbidden action | Consequence |
|-----------------|-------------|
| Write completion report without running CYC gate | `cyc_gate_line` check FAILS |
| Fabricate `CYC_GATE: PASS` line | `cyc_ground_truth` check FAILS |
| Claim CYC based on MCP output alone | `cyc_ground_truth` check FAILS |
| Skip `dotnet build` | `build_passed: true` claim is unverified |
| Mark `phase_5.status=completed` before gate passes | Pre-push hook BLOCKS |

---

## Lane Split for Remaining 48 Epics

See [`docs/workflow/WAVE7_PHASE5_LANE_ASSIGNMENT.md`](WAVE7_PHASE5_LANE_ASSIGNMENT.md)
for the 12-lane parallel assignment.

Summary by file:

| Lane | File | Epics |
|------|------|-------|
| L-A | `V12_002.REAPER.Audit.cs` | W7-031,081,082,083,084,085,086,087,141 (9 epics) |
| L-B | `V12_002.SIMA.Lifecycle.cs` | W7-056,060,070,107,109,110,111,112,113,114,115 (11 epics) |
| L-C | `V12_002.UI.Compliance.cs` | W7-047,146,147,149,150 (5 epics) |
| L-D | `V12_002.Trailing.StopUpdate.cs` | W7-051,052,053,139,140 (5 epics) |
| L-E | `V12_002.Trailing.cs` | W7-049,050,137,138 (4 epics) |
| L-F | `V12_002.UI.IPC.Commands.Fleet.cs` | W7-019,154,157,159 (4 epics) |
| L-G | `V12_002.REAPER.Repair.cs` | W7-088 (1 epic) |
| L-H | `V12_002.Safety.Watchdog.cs` | W7-089,090,091 (3 epics) |
| L-I | `V12_002.SIMA.Flatten.cs` | W7-028,098 (2 epics) |
| L-J | `V12_002.Symmetry.cs` | W7-067,124 (2 epics) |
| L-K | `V12_002.UI.IPC.cs` | W7-018 (1 epic) |
| L-L | `V12_002.UI.IPC.Server.cs` | W7-077 (1 epic) |

All 12 lanes run **in parallel** — no cross-lane file overlap.
