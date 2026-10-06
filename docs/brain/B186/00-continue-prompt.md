# B186 $continue Prompt
# Paste this entire block into a new session to resume.

$continue | PTT-COPIER B186 | Quick ALL + QAll2t Instrument Scope

## Situation

B186 is spec-designed, PIPELINE_READY. No code changes yet.
Your job: execute the pipeline (Ph1-Ph5), then validate in NT8.

## State at handoff

- **Baseline commit**: 0c27cd88 (B185-POST -- NT8 compile fix, last clean commit)
- **Working tree**: clean
- **Spec**: c:/WSGTA/universal-or-strategy-director/specs/002-trade-copier-spec.html  (B186 section present)
- **Brain dir**: docs/brain/B186/

## B186 Ticket Scope

### T1 -- Quick ALL instrument scope (DW-B186-QX-ALL-SCOPE-01)
- File: `src/PropTraderTools/Features/PttGlobalQuickExit.cs`
- `Execute()` -> `Execute(Instrument instrument)`
- Combined gate: `if (!CopyEngine.Instance.Flags.QxGlobalExit || instrument == null) return;`
- Instrument filter inside pos loop: `if (pos.Instrument?.FullName != instrument.FullName) continue;`
- CYC: 7 -> 8 (filter adds +1; combined gate holds at 1). At ceiling -- acceptable.
- Log line: `[PTT-QX-ALL] GlobalQuickExit fired instr=` + instrument.FullName
- Tests: T_B186_01 (Execute signature has 1 Instrument param), T_B186_02 (null instrument returns immediately)

### T2 -- QAll2t instrument scope (DW-B186-QX-ALL-SCOPE-02, closes DW-B185-QX-ALL-SCOPE-01)
- File: `src/PropTraderTools/Features/PttGlobalQuickExit.cs`
- `Execute(List<...> forcedTargets)` -> `Execute(List<...> forcedTargets, Instrument instrument)`
- Combined gate: `if (!CopyEngine.Instance.Flags.QxGlobalExit || instrument == null) return;`
- CYC hold: fold instrument filter into existing null/flat guard:
  `if (pos == null || pos.Quantity == 0 || pos.Instrument?.FullName != instrument.FullName) continue;`
  CYC stays at 8 (filter folds into existing branch (6)).
- Tests: T_B186_04 (signature has 2 params), T_B186_05 (null instrument returns immediately)

### T3 -- TradeCopierPanel.cs call sites (DW-B186-PANEL-CALLSITES-01)
- File: `src/PropTraderTools/TradeCopierPanel.cs`
- `OnQuickAllClick`: `gqx.Execute()` -> `gqx.Execute(_instrument)`
- `OnInstrQAll2tClick`: `gqx.Execute(targets)` -> `gqx.Execute(targets, _instrument)`
- No null guard needed at call site -- Execute() combined gate handles null instrument.
- Tests: T_B186_03 (OnQuickAllClick passes _instrument -- reflection), T_B186_06 (OnInstrQAll2tClick passes _instrument)

## Pipeline (all phases mandatory)

Ph1  ptt-architect       -> 02-architecture-plan.md
Ph2  ptt-plan-reviewer   -> 02-plan-review.md       (REVIEW_PASS gate)
Ph3  ptt-architect       -> 04-tickets.md
Ph3.5 ptt-ticket-reviewer -> 04-ticket-review.md    (TICKET_REVIEW_PASS gate)
Ph4a ptt-engineer        -> src .cs edits + ticket-N-completion.md
Ph4b ptt-verifier        -> ticket-N-verification.md (VERIFY_PASS gate)
Ph5  ptt-plan-reviewer   -> 05-final-review.md + 06-deferred-backlog.md

## Acceptance Criteria

| Check | Pass condition |
|-------|---------------|
| AC1 | PttGlobalQuickExit.Execute(Instrument) present, CYC=8 |
| AC2 | PttGlobalQuickExit.Execute(List, Instrument) present, CYC=8 |
| AC3 | OnQuickAllClick passes _instrument |
| AC4 | OnInstrQAll2tClick passes _instrument |
| AC5 | DW-B185-QX-ALL-SCOPE-01 eliminated (wrong-instrument qty split gone) |
| AC6 | dotnet build 0 errors; NT8 compile PASS; ptt-sync 19/0; 6 new tests |
| Build | 0 errors (dotnet + NT8) |
| DNA SCAN-01..06 | 0 new violations |
| Guardian | Exit 0 or exit 1 DELETION-AUDIT only with authorised DW-cards |

## Pattern reference

B185 PttBeSwapAll.Execute is the exact building block:
- Combined gate: `if (!flags.BreakEven || instrument == null) return;`
- Filter:         `if (pos.Instrument?.FullName != instrument.FullName) continue;`
- Call site:       `PttBeSwapAll.Execute(CopyEngine.Instance.GlobalBe.GlobalBeBuffer, _instrument)`

## Deferred (NOT in B186 scope)

- DW-B186-DIAG-CLEANUP-01: Remove [DW-B115-DIAG] log blocks from PttGlobalQuickExit (B187+)
- DW-B186-WARN-01: CS0067 PendingBeArmed never raised warning (B187+)

---
*Handoff written: 2026-07-08*
*Baseline: 0c27cd88 (B185-POST)*
*B186 spec: 002-trade-copier-spec.html -- B186 PIPELINE_READY section*
