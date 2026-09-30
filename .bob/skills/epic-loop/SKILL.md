---
name: epic-loop
description: '# /epic-loop - Autonomous Multi-Epic Orchestration'
metadata:
  user-invocable: true
  disable-model-invocation: true
---

# /epic-loop - Autonomous Multi-Epic Orchestration

⚠️ **DEPRECATED**: This command is deprecated as of 2026-06-08. Use `/autonomous-refactor` instead.

**Reason**: `/autonomous-refactor` provides more comprehensive orchestration with:
- Prerequisites validation (GODMODE checks)
- Checkpointing and failure recovery
- GitButler workspace integration
- Complete documentation of all phases

**Migration**: Replace `/epic-loop START END` with `/autonomous-refactor` (interactive mode will guide you).

---

# Original Documentation (Preserved for Reference)

**Purpose**: Execute multiple consecutive epics autonomously with minimal human intervention.

**Target**: Process 31 methods (CYC 15-20 → ≤8) via EPIC-CCN-15 through EPIC-CCN-45.

## Prerequisites

1. ✅ GODMODE enabled (all quality gates passing)
2. ✅ Complexity baseline established (31 methods identified)
3. ✅ Jane Street KB loaded (automatic via pre_session.py hook)
4. ✅ Execution plan exists (`docs/brain/EPIC-LOOP-EXECUTION-PLAN.md`)

## Command Syntax

```bash
/epic-loop [start_epic] [end_epic] [--dry-run]
```

**Parameters**:
- `start_epic`: First epic number (default: 15)
- `end_epic`: Last epic number (default: 45)
- `--dry-run`: Preview execution plan without running

**Examples**:
```bash
/epic-loop                    # Run all 31 epics (EPIC-CCN-15 to EPIC-CCN-45)
/epic-loop 15 20              # Run first 6 epics only
/epic-loop 25 30 --dry-run    # Preview epics 25-30
```

## Execution Protocol

### Phase 1: Pre-Flight Validation (MANDATORY)

```powershell
# 1. Verify GODMODE prerequisites
powershell -File .\scripts\pre_push_validation.ps1

# 2. Verify complexity baseline
python scripts\complexity_audit.py

# 3. Verify hard link sync
powershell -File .\deploy-sync.ps1

# 4. Verify Jane Street KB loaded
python scripts\query_kb.py "test"
```

**Gate**: ALL checks must pass. If any fail, STOP and fix before proceeding.

### Phase 2: Epic Loop Execution

For each epic in range [start_epic, end_epic]:

```bash
# Delegate to /epic-run (which handles full 6-phase protocol)
/epic-run EPIC-CCN-{N}
```

**Per-Epic Protocol** (via `/epic-run`):
1. **Intake**: Load method context via jCodemunch
2. **Scope Boundary**: Verify single-method scope (V12.23 No Scope Creep)
3. **Plan**: Generate extraction plan with Jane Street validation
4. **Validate**: Triple-agent audit (Bob + Codex + Arena AI)
5. **Execute**: Surgical extraction with checkpointing
6. **Verify**: Complexity audit + pre-push validation

**Checkpointing**: Automatic after each epic. Restore via `/restore` if interrupted.

### Phase 3: Post-Loop Verification

```powershell
# 1. Run full complexity audit
python scripts\complexity_audit.py

# 2. Verify ZERO methods with CYC > 8
# Expected: All 31 methods now CYC ≤ 8

# 3. Run full pre-push validation
powershell -File .\scripts\pre_push_validation.ps1

# 4. Sync hard links
powershell -File .\deploy-sync.ps1
```

**Success Criteria**:
- ✅ All 31 methods reduced to CYC ≤ 8
- ✅ Zero new Jane Street violations (347 P0 baseline maintained)
- ✅ All 13 pre-push checks passing
- ✅ Hard links synchronized (81/81 files)

## Error Handling

### Epic Failure Protocol

If any epic fails:

1. **STOP immediately** - Do not proceed to next epic
2. **Analyze failure** - Check `docs/brain/EPIC-CCN-{N}/failure-analysis.md`
3. **Fix root cause** - Address blocking issue
4. **Restart from failed epic** - Use `/epic-loop {N} {end_epic}`

### Common Failure Modes

| Failure | Root Cause | Fix |
|---------|-----------|-----|
| Scope creep detected | Multiple concerns in one PR | Separate PRs, restart epic |
| Compilation errors | Pre-existing issues | Fix separately, restart epic |
| Jane Street violations | New anti-patterns introduced | Revert, apply Jane Street patterns |
| Complexity regression | Extraction incomplete | Re-plan with stricter CYC target |

## Monitoring

### Progress Tracking

```bash
# Check current epic status
cat docs/brain/EPIC_STATUS.md

# View execution log
cat .bob/notes/epic-loop-log.txt

# Check complexity progress
python scripts\complexity_audit.py | grep "CYC > 8"
```

### Health Metrics

Monitor these metrics during loop execution:

- **Complexity Trend**: Should decrease monotonically
- **Jane Street Violations**: Should remain at 347 P0 (no new violations)
- **Build Time**: Should remain stable (<30s)
- **Test Coverage**: Should increase (currently 0% → target 80%)

## Jane Street Alignment

**Automatic Loading**: Jane Street KB patterns are loaded automatically via `.bob/hooks/pre_session.py` on every Bob CLI session start.

**Validation Points**:
1. **Pre-Loop**: Baseline audit (347 P0 violations documented)
2. **Per-Epic**: Check #15 (jane_street_validator.py) + Check #16 (jane_street_rule_checker.py)
3. **Post-Loop**: Final audit (verify no new violations)

**Key Patterns Applied**:
- **Correctness by Construction**: Make illegal states unrepresentable
- **Lock-Free Concurrency**: FSM/Actor Enqueue model (no `lock()` blocks)
- **Cognitive Simplicity**: CYC ≤ 8 (ultra-strict, Jane Street aligned)
- **Immutable Data**: Prefer readonly structs over mutable classes
- **Explicit Error Handling**: No exceptions in hot paths

## Recovery Protocol

If orchestration window freezes or crashes:

1. **Check last checkpoint**: `.bob/notes/pending-notes.txt`
2. **Verify last completed epic**: `docs/brain/EPIC_STATUS.md`
3. **Resume from next epic**: `/epic-loop {N+1} {end_epic}`
4. **Verify no partial work**: `git status` should show clean working tree

## Performance Targets

**Per-Epic Duration**: 15-25 minutes (6 phases)
**Total Loop Duration**: 7.75 - 12.9 hours (31 epics)

**Optimization Strategies**:
- Run during off-hours (overnight execution)
- Use `--dry-run` to preview before committing
- Batch similar methods (e.g., all UI methods together)

## Success Metrics

**Completion Criteria**:
- ✅ 31/31 epics completed successfully
- ✅ All methods CYC ≤ 8 (Jane Street ultra-alignment)
- ✅ Zero new Jane Street violations
- ✅ All quality gates passing (13/13 checks)
- ✅ Hard links synchronized
- ✅ Documentation updated (EPIC_STATUS.md)

**Post-Loop Actions**:
1. Create summary PR (docs only, no src/ contamination)
2. Update `docs/brain/EPIC_STATUS.md` with final metrics
3. Document lessons learned in `docs/brain/EPIC-LOOP-RETROSPECTIVE.md`
4. Celebrate 🎉 (31 God-functions eliminated!)

## References

- **Execution Plan**: `docs/brain/EPIC-LOOP-EXECUTION-PLAN.md`
- **Gap Analysis**: `docs/brain/AUTONOMOUS_REFACTOR_GAP_ANALYSIS.md`
- **Jane Street Baseline**: `docs/brain/JANE_STREET_BASELINE_AUDIT.md`
- **Epic Run Protocol**: `.bob/commands/epic-run.md`
- **Pre-Push Validation**: `scripts/pre_push_validation.ps1`
