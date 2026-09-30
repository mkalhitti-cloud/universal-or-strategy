# EPIC-LOOP Execution Plan - 31 Methods (CYC 15-20 → ≤8)

**Status**: Ready for Execution
**Target**: Jane Street Ultra-Alignment (CYC ≤ 8)
**Baseline**: 31 methods identified via complexity_audit.py (2026-06-05)
**Estimated Duration**: 7.75 - 12.9 hours (15-25 min per epic)

## Executive Summary

This plan orchestrates 31 consecutive epics (EPIC-CCN-15 through EPIC-CCN-45) to reduce all methods with CYC 15-20 to CYC ≤8, achieving Jane Street ultra-alignment. Each epic follows the 6-phase protocol (Intake → Scope Boundary → Plan → Validate → Execute → Verify) with automatic Jane Street KB loading and validation.

## Complexity Baseline (2026-06-05)

**Source**: `python scripts/complexity_audit.py`

### Tier 1: CYC 20 (2 methods)
| Epic | Method | File | Current CYC | Target CYC |
|------|--------|------|-------------|------------|
| EPIC-CCN-15 | ShowModeSpecificControls | V12_002.UI.Panel.Handlers.cs | 20 | ≤8 |
| EPIC-CCN-16 | FindChartTraderViaChartTab | V12_002.UI.Panel.Helpers.cs | 20 | ≤8 |

### Tier 2: CYC 19 (5 methods)
| Epic | Method | File | Current CYC | Target CYC |
|------|--------|------|-------------|------------|
| EPIC-CCN-17 | ValidateOrphanedMasterOrders | V12_002.REAPER.Audit.cs | 19 | ≤8 |
| EPIC-CCN-18 | Dispatch_PublishMarketBracketToPhoton | V12_002.SIMA.Dispatch.cs | 19 | ≤8 |
| EPIC-CCN-19 | TryHandleFleetCommand | V12_002.UI.IPC.Commands.Fleet.cs | 19 | ≤8 |
| EPIC-CCN-20 | TryHandleFleet_CancelAll | V12_002.UI.IPC.Commands.Fleet.cs | 19 | ≤8 |
| EPIC-CCN-21 | UpdateTargetVisibility | V12_002.UI.Panel.Handlers.cs | 19 | ≤8 |

### Tier 3: CYC 18 (6 methods)
| Epic | Method | File | Current CYC | Target CYC |
|------|--------|------|-------------|------------|
| EPIC-CCN-22 | OnBarUpdate | V12_002.cs | 18 | ≤8 |
| EPIC-CCN-23 | TryHandleModeCommand | V12_002.UI.IPC.Commands.Mode.cs | 18 | ≤8 |
| EPIC-CCN-24 | TryHandleConfigCommand | V12_002.UI.IPC.Commands.Config.cs | 18 | ≤8 |
| EPIC-CCN-25 | OnExecutionUpdate | V12_002.cs | 18 | ≤8 |
| EPIC-CCN-26 | Dispatch_PublishLimitBracketToPhoton | V12_002.SIMA.Dispatch.cs | 18 | ≤8 |
| EPIC-CCN-27 | UpdateStopVisibility | V12_002.UI.Panel.Handlers.cs | 18 | ≤8 |

### Tier 4: CYC 17 (6 methods)
| Epic | Method | File | Current CYC | Target CYC |
|------|--------|------|-------------|------------|
| EPIC-CCN-28 | OnOrderUpdate | V12_002.cs | 17 | ≤8 |
| EPIC-CCN-29 | TryHandleMiscCommand | V12_002.UI.IPC.Commands.Misc.cs | 17 | ≤8 |
| EPIC-CCN-30 | UpdateBreakevenVisibility | V12_002.UI.Panel.Handlers.cs | 17 | ≤8 |
| EPIC-CCN-31 | Dispatch_PublishStopBracketToPhoton | V12_002.SIMA.Dispatch.cs | 17 | ≤8 |
| EPIC-CCN-32 | ValidateNakedStops | V12_002.REAPER.NakedStop.cs | 17 | ≤8 |
| EPIC-CCN-33 | UpdateEntryVisibility | V12_002.UI.Panel.Handlers.cs | 17 | ≤8 |

### Tier 5: CYC 16 (6 methods)
| Epic | Method | File | Current CYC | Target CYC |
|------|--------|------|-------------|------------|
| EPIC-CCN-34 | ValidateNakedPositions | V12_002.REAPER.NakedPosition.cs | 16 | ≤8 |
| EPIC-CCN-35 | UpdatePnlVisibility | V12_002.UI.Panel.Handlers.cs | 16 | ≤8 |
| EPIC-CCN-36 | Dispatch_PublishTrailingStopToPhoton | V12_002.SIMA.Dispatch.cs | 16 | ≤8 |
| EPIC-CCN-37 | OnPositionUpdate | V12_002.cs | 16 | ≤8 |
| EPIC-CCN-38 | UpdateRiskVisibility | V12_002.UI.Panel.Handlers.cs | 16 | ≤8 |
| EPIC-CCN-39 | Dispatch_CancelBracket | V12_002.SIMA.Dispatch.cs | 16 | ≤8 |

### Tier 6: CYC 15 (6 methods)
| Epic | Method | File | Current CYC | Target CYC |
|------|--------|------|-------------|------------|
| EPIC-CCN-40 | UpdateFleetStatusVisibility | V12_002.UI.Panel.Handlers.cs | 15 | ≤8 |
| EPIC-CCN-41 | Dispatch_ModifyBracket | V12_002.SIMA.Dispatch.cs | 15 | ≤8 |
| EPIC-CCN-42 | ValidateOrphanedStops | V12_002.REAPER.OrphanSafety.cs | 15 | ≤8 |
| EPIC-CCN-43 | UpdateModeVisibility | V12_002.UI.Panel.Handlers.cs | 15 | ≤8 |
| EPIC-CCN-44 | Dispatch_FlattenPosition | V12_002.SIMA.Dispatch.cs | 15 | ≤8 |
| EPIC-CCN-45 | UpdateSymmetryVisibility | V12_002.UI.Panel.Handlers.cs | 15 | ≤8 |

## Execution Strategy

### Batching Strategy

**Rationale**: Group similar methods to leverage shared context and reduce cognitive load.

**Batch 1: UI Panel Handlers** (8 methods, CYC 15-20)
- EPIC-CCN-15: ShowModeSpecificControls (CYC 20)
- EPIC-CCN-21: UpdateTargetVisibility (CYC 19)
- EPIC-CCN-27: UpdateStopVisibility (CYC 18)
- EPIC-CCN-30: UpdateBreakevenVisibility (CYC 17)
- EPIC-CCN-33: UpdateEntryVisibility (CYC 17)
- EPIC-CCN-35: UpdatePnlVisibility (CYC 16)
- EPIC-CCN-38: UpdateRiskVisibility (CYC 16)
- EPIC-CCN-40: UpdateFleetStatusVisibility (CYC 15)
- EPIC-CCN-43: UpdateModeVisibility (CYC 15)
- EPIC-CCN-45: UpdateSymmetryVisibility (CYC 15)

**Batch 2: SIMA Dispatch** (6 methods, CYC 15-19)
- EPIC-CCN-18: Dispatch_PublishMarketBracketToPhoton (CYC 19)
- EPIC-CCN-26: Dispatch_PublishLimitBracketToPhoton (CYC 18)
- EPIC-CCN-31: Dispatch_PublishStopBracketToPhoton (CYC 17)
- EPIC-CCN-36: Dispatch_PublishTrailingStopToPhoton (CYC 16)
- EPIC-CCN-39: Dispatch_CancelBracket (CYC 16)
- EPIC-CCN-41: Dispatch_ModifyBracket (CYC 15)
- EPIC-CCN-44: Dispatch_FlattenPosition (CYC 15)

**Batch 3: REAPER Validation** (3 methods, CYC 15-19)
- EPIC-CCN-17: ValidateOrphanedMasterOrders (CYC 19)
- EPIC-CCN-32: ValidateNakedStops (CYC 17)
- EPIC-CCN-34: ValidateNakedPositions (CYC 16)
- EPIC-CCN-42: ValidateOrphanedStops (CYC 15)

**Batch 4: IPC Commands** (4 methods, CYC 17-19)
- EPIC-CCN-19: TryHandleFleetCommand (CYC 19)
- EPIC-CCN-20: TryHandleFleet_CancelAll (CYC 19)
- EPIC-CCN-23: TryHandleModeCommand (CYC 18)
- EPIC-CCN-24: TryHandleConfigCommand (CYC 18)
- EPIC-CCN-29: TryHandleMiscCommand (CYC 17)

**Batch 5: Core Event Handlers** (5 methods, CYC 16-20)
- EPIC-CCN-16: FindChartTraderViaChartTab (CYC 20)
- EPIC-CCN-22: OnBarUpdate (CYC 18)
- EPIC-CCN-25: OnExecutionUpdate (CYC 18)
- EPIC-CCN-28: OnOrderUpdate (CYC 17)
- EPIC-CCN-37: OnPositionUpdate (CYC 16)

### Execution Order

**Priority**: Highest CYC first (Tier 1 → Tier 6)
**Rationale**: Eliminate worst offenders early, build momentum

**Recommended Schedule**:
- **Day 1**: Batch 1 (UI Panel Handlers) - 10 epics, ~4.2 hours
- **Day 2**: Batch 2 (SIMA Dispatch) - 7 epics, ~2.9 hours
- **Day 3**: Batch 3 (REAPER Validation) - 4 epics, ~1.7 hours
- **Day 4**: Batch 4 (IPC Commands) - 5 epics, ~2.1 hours
- **Day 5**: Batch 5 (Core Event Handlers) - 5 epics, ~2.1 hours

**Total**: 31 epics, ~13 hours (with buffer)

## Per-Epic Protocol

Each epic follows the 6-phase protocol defined in `.bob/commands/epic-run.md`:

### Phase 0: Intake (2-3 min)
- Load method context via jCodemunch
- Identify dependencies and call sites
- Generate forensic report

### Phase 1: Scope Boundary (1-2 min)
- Verify single-method scope (V12.23 No Scope Creep)
- Identify pre-existing issues (fix separately)
- Document scope boundaries

### Phase 2: Plan (5-7 min)
- Generate extraction plan
- Apply Jane Street patterns (automatic KB loading)
- Create Mermaid diagrams

### Phase 3: Validate (3-5 min)
- Triple-agent audit (Bob + Codex + Arena AI)
- Verify Jane Street compliance
- Check for scope creep

### Phase 4: Execute (3-5 min)
- Surgical extraction with checkpointing
- Apply CSharpier formatting
- Run pre-push validation

### Phase 5: Verify (1-2 min)
- Complexity audit (verify CYC ≤ 8)
- Build verification
- Hard link sync

**Total per epic**: 15-25 minutes

## Quality Gates

### Pre-Loop Gates (MANDATORY)

1. ✅ **GODMODE Enabled**: All 13 pre-push checks passing
2. ✅ **Complexity Baseline**: 31 methods identified (CYC 15-20)
3. ✅ **Jane Street KB**: Loaded automatically via pre_session.py
4. ✅ **Hard Links**: Synchronized (81/81 files)
5. ✅ **Git Status**: Clean working tree

### Per-Epic Gates

1. ✅ **Scope Boundary**: Single method only (no scope creep)
2. ✅ **Jane Street Validation**: Check #15 + Check #16 passing
3. ✅ **Complexity Reduction**: Method CYC reduced to ≤8
4. ✅ **Build Success**: Zero compilation errors
5. ✅ **Pre-Push Validation**: All 13 checks passing

### Post-Loop Gates

1. ✅ **Zero CYC > 8**: All 31 methods reduced
2. ✅ **Jane Street Baseline**: 347 P0 violations maintained (no new violations)
3. ✅ **Build Time**: <30s (no performance regression)
4. ✅ **Hard Links**: Synchronized (81/81 files)
5. ✅ **Documentation**: EPIC_STATUS.md updated

## Jane Street Alignment

### Automatic Loading

Jane Street KB patterns are loaded automatically on every Bob CLI session start via `.bob/hooks/pre_session.py`. No manual intervention required.

**Loaded Patterns** (10 documents, ~2,241 tokens):
1. Effective ML (Yaron Minsky)
2. Building an Exchange (Will Benton)
3. Microsecond Latency Trading (Skylake)
4. Testing Hard to Test Things (Stephen Weeks)
5. Coordination Avoidance (Peter Bailis)
6. Safe Production Changes (Ron Minsky)
7. Observability Tools (Brendan Gregg)
8. Codesign for Performance (Andrei Alexandrescu)
9. Engineering for Traders (Jane Street)
10. Fix Bugs Faster (Debugging Patterns)

### Validation Points

**Check #15**: `jane_street_validator.py` (Anti-pattern detection)
- Magic Numbers (223 violations)
- Null Usage (69 violations)
- Mutable State (48 violations)
- Lock Usage (5 violations)
- Exception Usage (3 violations)

**Check #16**: `jane_street_rule_checker.py` (Positive rule enforcement)
- Immutable Data Structures
- Explicit Error Handling
- Lock-Free Concurrency
- Cognitive Simplicity (CYC ≤ 8)
- Type Safety

### Key Patterns Applied

1. **Correctness by Construction**: Make illegal states unrepresentable
2. **Lock-Free Concurrency**: FSM/Actor Enqueue model (no `lock()` blocks)
3. **Cognitive Simplicity**: CYC ≤ 8 (ultra-strict, Jane Street aligned)
4. **Immutable Data**: Prefer readonly structs over mutable classes
5. **Explicit Error Handling**: No exceptions in hot paths

## Risk Mitigation

### Checkpointing

**Automatic**: After each epic completion
**Manual**: Via `/checkpoint` command
**Restore**: Via `/restore` command

**Checkpoint Data**:
- Current epic number
- Completed epics list
- Complexity metrics
- Jane Street violation count
- Build status

### Rollback Protocol

If epic fails:
1. **STOP immediately** - Do not proceed to next epic
2. **Analyze failure** - Check `docs/brain/EPIC-CCN-{N}/failure-analysis.md`
3. **Rollback changes** - `git reset --hard HEAD~1`
4. **Fix root cause** - Address blocking issue
5. **Restart epic** - Use `/epic-run EPIC-CCN-{N}`

### Common Failure Modes

| Failure | Root Cause | Fix | Prevention |
|---------|-----------|-----|------------|
| Scope creep | Multiple concerns in one PR | Separate PRs, restart epic | Strict scope boundary check |
| Compilation errors | Pre-existing issues | Fix separately, restart epic | Pre-flight validation |
| Jane Street violations | New anti-patterns | Revert, apply Jane Street patterns | Automatic KB loading |
| Complexity regression | Extraction incomplete | Re-plan with stricter target | Triple-agent audit |

## Monitoring

### Real-Time Metrics

```bash
# Check current epic status
cat docs/brain/EPIC_STATUS.md

# View execution log
cat .bob/notes/epic-loop-log.txt

# Check complexity progress
python scripts\complexity_audit.py | grep "CYC > 8"

# Monitor Jane Street violations
python scripts\jane_street_validator.py | grep "P0"
```

### Health Dashboard

| Metric | Baseline | Target | Current |
|--------|----------|--------|---------|
| Methods CYC > 20 | 2 | 0 | TBD |
| Methods CYC > 15 | 31 | 0 | TBD |
| Methods CYC > 8 | 183 | 31 | TBD |
| Jane Street P0 | 347 | 347 | TBD |
| Build Time | 28s | <30s | TBD |
| Test Coverage | 0% | 80% | TBD |

## Success Criteria

### Completion Criteria

- ✅ 31/31 epics completed successfully
- ✅ All methods CYC ≤ 8 (Jane Street ultra-alignment)
- ✅ Zero new Jane Street violations (347 P0 baseline maintained)
- ✅ All quality gates passing (13/13 checks)
- ✅ Hard links synchronized (81/81 files)
- ✅ Documentation updated (EPIC_STATUS.md)

### Post-Loop Actions

1. **Create Summary PR** (docs only, no src/ contamination)
2. **Update EPIC_STATUS.md** with final metrics
3. **Document Lessons Learned** in `EPIC-LOOP-RETROSPECTIVE.md`
4. **Celebrate** 🎉 (31 God-functions eliminated!)

## References

- **Command**: `.bob/commands/epic-loop.md`
- **Gap Analysis**: `docs/brain/AUTONOMOUS_REFACTOR_GAP_ANALYSIS.md`
- **Jane Street Baseline**: `docs/brain/JANE_STREET_BASELINE_AUDIT.md`
- **Epic Run Protocol**: `.bob/commands/epic-run.md`
- **Pre-Push Validation**: `scripts/pre_push_validation.ps1`
- **Complexity Audit**: `scripts/complexity_audit.py`
- **Jane Street Validator**: `scripts/jane_street_validator.py`
- **Jane Street Rule Checker**: `scripts/jane_street_rule_checker.py`

---

**Last Updated**: 2026-06-05
**Status**: Ready for Execution
**Estimated Completion**: 2026-06-10 (5 days, ~13 hours total)