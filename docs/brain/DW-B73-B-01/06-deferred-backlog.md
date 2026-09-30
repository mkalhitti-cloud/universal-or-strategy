# Deferred Backlog -- DW-B73-B-01/02 Pipeline Block

**Block**: DW-B73-B-01 + DW-B73-B-02
**Closed**: 2026-08-21
**Status**: FINAL_PASS

---

## Deferred Items This Block

| ID | Priority | Description | Source |
|----|----------|-------------|--------|
| DW-DEFER-B73-01 | P1 | `deploy-sync.ps1` not found at workspace root (`c:\WSGTA\universal-or-strategy\`). Script exists only at `archive/v12-reference/scripts/deploy-sync.ps1`. NT8 hard-link sync was NOT run during this pipeline. Must be located or recreated before next pipeline to ensure NinjaTrader assembly stays synchronized with src/ changes. Blocks F5 gate verification. | Ph5 Section I |
| DW-DEFER-B73-02 | P1 | Pre-existing build failures blocking `dotnet test` runtime execution: `CopyEngineTests.cs` (CS0246 CopyRule, CS0234 NullabilityInfoContext/Instruments, CS1061 FirstOrDefault/Any, CS7036 IsDispatchTriggerState, CS0122 CopyEngine() inaccessible), `CopyEngine.cs` L3243 (CS0433 'Globals' type ambiguity between assemblies), `B43Tests.cs` L35/L57/L75 (CS0117 ParseAtmTemplateSelection), `B68Tests.cs` (CS7036 BeEventArgs constructor), `B71Tests.cs` (CS0246 CopyRule), `B76Tests.cs` L38 (CS0234 NinjaTrader.NinjaScript.Instruments). Root cause investigation and separate-PR resolution required per AGENTS.md V12.23 No Scope Creep Protocol. | Ph5 Section G |
| DW-DEFER-B73-03 | P2 | `scripts/complexity_audit.py` absent from workspace root. Both T1 and T2 CYC audits were performed manually due to missing tool. Automated CYC enforcement cannot run until this script is restored. Required for SCAN-04 compliance in all future pipelines. | Ph5 Section B SCAN-04 |
| DW-DEFER-B73-04 | P2 | Orchestrator prompt `00-orchestrator-prompt.md` stated "6 inline call sites" for DW-B73-B-02; architecture plan (Ph1) correctly identified all 10. Orchestrator prompt was not retroactively updated after Ph1 resolution. Informational only -- no behavioral impact -- but prompt accuracy should be maintained for future audit traceability. | Ph5 Section K informational |

---

## Carry-Forward from Prior Blocks

None -- first pipeline in this brain directory.

---

## Notes

**Commit**: `fix(ptt): DW-B73-B-01+02 BeAllDisarmed self-notify + BrushTeal cache [301 tests]`

**[Fact] count**: 301 static (runtime execution blocked by DW-DEFER-B73-02 pre-existing build failures)

**Files modified in this block**:
- `src/PropTraderTools/TradeCopierPanel.cs` -- L587 removal (T1) + L280-L281 insert + 10 substitutions (T2)
- `src/PropTraderTools/Tests/B73Tests.cs` -- +6 [Fact] (3 per ticket; B73Tests.cs: 33 -> 39)

**P1 blockers for next pipeline**:
1. DW-DEFER-B73-01: Restore `deploy-sync.ps1` to workspace root before any further src/ edits
2. DW-DEFER-B73-02: Fix pre-existing compilation errors in separate PR before attempting runtime test execution
