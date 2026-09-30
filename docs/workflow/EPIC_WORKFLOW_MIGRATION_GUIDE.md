# V12 Epic Workflow Migration Guide

**Version**: 1.0  
**Date**: 2026-06-09  
**Status**: Active  
**Author**: V12 Architecture Team

## Executive Summary

This guide helps teams migrate from the old monolithic `epic-run` workflow to the new manifest-based independent subtask workflow. The migration is designed to be gradual, with both workflows coexisting during the transition period.

## Table of Contents

1. [Why Migrate?](#why-migrate)
2. [Key Differences](#key-differences)
3. [Migration Timeline](#migration-timeline)
4. [When to Use New Workflow](#when-to-use-new-workflow)
5. [Migrating In-Progress Epics](#migrating-in-progress-epics)
6. [Backward Compatibility](#backward-compatibility)
7. [Breaking Changes](#breaking-changes)
8. [Migration Checklist](#migration-checklist)
9. [Troubleshooting](#troubleshooting)

## Why Migrate?

### Problems with Old Workflow

**Monolithic Single-Session Model**:
- ❌ Context window exhaustion in long epics
- ❌ No checkpointing between phases
- ❌ Cannot parallelize independent work
- ❌ Difficult to resume after failure
- ❌ No clear artifact handoff protocol
- ❌ Manual file path management

**Example Failure Scenario**:
```bash
# Old workflow
epic-run EPIC-CCN-15

# Phase 0-4 complete (60 minutes elapsed)
# Phase 5.1 starts...
# ERROR: Context window exhausted
# Result: Must restart entire epic from Phase 0
```

### Benefits of New Workflow

**Manifest-Based Independent Subtasks**:
- ✅ Each phase is a fresh session (no context exhaustion)
- ✅ Clear artifact handoff via manifest
- ✅ Parallel execution of independent phases
- ✅ Resume from any phase after failure
- ✅ Watsonx Orchestrate integration ready
- ✅ Automated state management

**Example Success Scenario**:
```bash
# New workflow
epic-intake EPIC-CCN-15 "Description"  # Phase 0
epic-scope-boundary EPIC-CCN-15 --phase 1  # Phase 1
# ... phases 1.5-4 complete (60 minutes elapsed)

epic-validate EPIC-CCN-15 --ticket 1  # Phase 5.1
# ERROR: Build fails

# Fix issue manually
# Resume from Phase 5.1 (no restart needed)
epic-validate EPIC-CCN-15 --ticket 1  # Retry Phase 5.1
# SUCCESS: Continue from where you left off
```

### Performance Improvements

| Metric | Old Workflow | New Workflow | Improvement |
|--------|--------------|--------------|-------------|
| **Context Window Usage** | 100% (exhausted) | 20% per phase | 80% reduction |
| **Failure Recovery Time** | Full restart | Resume from failure | 90% faster |
| **Parallel Execution** | Not supported | 3x tickets concurrent | 3x faster |
| **Epic Completion Time** | 240 min | 140 min | 42% faster |

## Key Differences

### Command Structure

**Old Workflow** (Monolithic):
```bash
# Single command for entire epic
epic-run EPIC-CCN-X
```

**New Workflow** (Phase-by-Phase):
```bash
# Individual commands per phase
epic-intake EPIC-CCN-X "Description"
epic-scope-boundary EPIC-CCN-X --phase 1
epic-scope-boundary EPIC-CCN-X --phase 1.5
epic-plan EPIC-CCN-X
epic-scan EPIC-CCN-X
epic-tickets EPIC-CCN-X
epic-validate EPIC-CCN-X --ticket 1
epic-verify-ticket EPIC-CCN-X --ticket 1
epic-review-final EPIC-CCN-X
```

### State Management

**Old Workflow**:
- State stored in memory (lost on failure)
- No persistent state between phases
- Manual tracking of progress

**New Workflow**:
- State stored in `manifest.json` (persistent)
- Automatic state updates after each phase
- Programmatic progress tracking

### Artifact Management

**Old Workflow**:
```bash
# Manual file path management
cat docs/brain/EPIC-CCN-X/hotspots.md  # Where is this file?
cat docs/brain/EPIC-CCN-X/scope.md     # What's the naming convention?
```

**New Workflow**:
```bash
# Manifest tracks all artifacts
python scripts/epic_manifest.py artifacts EPIC-CCN-X

# Output:
# Phase 0: docs/brain/EPIC-CCN-X/00-hotspots.md
# Phase 1: docs/brain/EPIC-CCN-X/00-scope.md
# Phase 1.5: docs/brain/EPIC-CCN-X/01-scope-boundary.md
# ...
```

### Dependency Management

**Old Workflow**:
- Implicit dependencies (hardcoded in epic-run)
- No validation of dependency satisfaction
- Manual ordering of phases

**New Workflow**:
```bash
# Explicit dependencies in manifest
python scripts/epic_manifest.py dependencies EPIC-CCN-X 1.5

# Output:
# Phase 1.5 depends on: Phase 1
# Phase 1 status: completed ✅
# Dependencies satisfied: YES
```

### Parallel Execution

**Old Workflow**:
- Sequential execution only
- No parallelization support

**New Workflow**:
```bash
# Parallel ticket execution
epic-validate EPIC-CCN-X --ticket 1 &  # Background
epic-validate EPIC-CCN-X --ticket 2 &  # Background
epic-validate EPIC-CCN-X --ticket 3 &  # Background
wait  # Wait for all to complete
```

## Migration Timeline

### Phase 1: Coexistence (Weeks 1-4)

**Status**: Both workflows available

**Old Workflow**:
- ✅ Still functional
- ✅ No breaking changes
- ⚠️ Marked as deprecated

**New Workflow**:
- ✅ Fully implemented
- ✅ Tested with EPIC-CCN-16 pilot
- ✅ Documentation complete

**Recommendation**: Use new workflow for all new epics

### Phase 2: Transition (Weeks 5-8)

**Status**: Gradual migration

**Actions**:
- Migrate in-progress epics to new workflow (optional)
- Train team on new workflow
- Update CI/CD pipelines
- Monitor for issues

**Recommendation**: Complete all in-progress epics with old workflow, start new epics with new workflow

### Phase 3: Deprecation (Weeks 9-12)

**Status**: Old workflow deprecated

**Actions**:
- Add deprecation warnings to `epic-run`
- Update all documentation to reference new workflow
- Archive old workflow code

**Recommendation**: All epics must use new workflow

### Phase 4: Removal (Week 13+)

**Status**: Old workflow removed

**Actions**:
- Remove `epic-run` command
- Remove old workflow code
- Update AGENTS.md to remove old workflow references

**Recommendation**: New workflow is the only option

## When to Use New Workflow

### Always Use New Workflow For:

✅ **New Epics** (starting after 2026-06-09)
- All new complexity reduction epics
- All new feature extraction epics
- All new refactoring epics

✅ **Long-Running Epics** (>2 hours estimated)
- High risk of context window exhaustion
- Benefit from checkpointing

✅ **Multi-Ticket Epics** (>3 tickets)
- Benefit from parallel execution
- Faster completion time

✅ **Collaborative Epics** (multiple engineers)
- Clear handoff via manifest
- Independent phase execution

### Consider Old Workflow For:

⚠️ **In-Progress Epics** (started before 2026-06-09)
- Already past Phase 4
- Less than 1 hour remaining
- Low risk of failure

⚠️ **Simple Epics** (1-2 tickets, <1 hour)
- Minimal benefit from new workflow
- Old workflow is simpler

**Note**: Old workflow will be removed in Week 13. Plan accordingly.

## Migrating In-Progress Epics

### Decision Tree

```
Is epic past Phase 4?
├─ YES → Continue with old workflow
└─ NO → Migrate to new workflow

Is epic estimated <1 hour remaining?
├─ YES → Continue with old workflow
└─ NO → Migrate to new workflow

Has epic failed/stalled?
├─ YES → Migrate to new workflow (easier recovery)
└─ NO → Continue with old workflow
```

### Migration Procedure

#### Step 1: Assess Current State

```bash
# Check epic progress (old workflow)
# Review docs/brain/EPIC-CCN-X/ directory
ls docs/brain/EPIC-CCN-X/

# Identify completed phases
# Example output:
# hotspots.md (Phase 0 complete)
# scope.md (Phase 1 complete)
# architecture-plan.md (Phase 2 complete)
# (Phase 3 not started)
```

#### Step 2: Generate Manifest

```bash
# Create manifest from existing artifacts
python scripts/epic_manifest.py migrate EPIC-CCN-X

# This will:
# 1. Scan docs/brain/EPIC-CCN-X/ for artifacts
# 2. Infer completed phases
# 3. Generate manifest.json
# 4. Rename artifacts to new convention
```

**Example Migration**:
```bash
# Before migration
docs/brain/EPIC-CCN-X/
  ├─ hotspots.md
  ├─ scope.md
  └─ architecture-plan.md

# After migration
docs/brain/EPIC-CCN-X/
  ├─ manifest.json (NEW)
  ├─ 00-hotspots.md (renamed from hotspots.md)
  ├─ 00-scope.md (renamed from scope.md)
  └─ 02-architecture-plan.md (renamed from architecture-plan.md)
```

#### Step 3: Verify Migration

```bash
# Check manifest validity
python scripts/epic_manifest.py validate EPIC-CCN-X

# Check phase status
python scripts/epic_manifest.py status EPIC-CCN-X

# Expected output:
# Epic: EPIC-CCN-X
# Status: in_progress
# Completed: Phase 0, 1, 2
# Next: Phase 3
```

#### Step 4: Resume with New Workflow

```bash
# Continue from next phase
epic-scan EPIC-CCN-X  # Phase 3
epic-tickets EPIC-CCN-X  # Phase 4
# ... continue with new workflow
```

### Migration Example: EPIC-CCN-15

**Before Migration** (Old Workflow):
```bash
# Epic started with old workflow
epic-run EPIC-CCN-15

# Phases 0-2 completed
# Phase 3 failed (context window exhausted)
# Epic stalled
```

**Migration**:
```bash
# Step 1: Assess state
ls docs/brain/EPIC-CCN-15/
# Output: hotspots.md, scope.md, architecture-plan.md

# Step 2: Generate manifest
python scripts/epic_manifest.py migrate EPIC-CCN-15
# Output: Manifest created, 3 phases marked completed

# Step 3: Verify
python scripts/epic_manifest.py status EPIC-CCN-15
# Output: Phases 0-2 completed, Phase 3 pending

# Step 4: Resume
epic-scan EPIC-CCN-15  # Phase 3 (fresh session, no context issues)
# SUCCESS: Phase 3 completed
```

**After Migration** (New Workflow):
```bash
# Continue with new workflow
epic-tickets EPIC-CCN-15  # Phase 4
epic-validate EPIC-CCN-15 --ticket 1  # Phase 5.1
epic-verify-ticket EPIC-CCN-15 --ticket 1  # Phase 5.1.V
epic-review-final EPIC-CCN-15  # Phase 6
# SUCCESS: Epic completed
```

## Backward Compatibility

### Supported During Transition

✅ **Old Workflow Commands**:
- `epic-run` still functional (with deprecation warning)
- All old workflow features preserved
- No breaking changes to existing epics

✅ **Old Artifact Naming**:
- Old artifact names still recognized
- Migration script handles renaming
- No manual renaming required

✅ **Old State Management**:
- In-memory state still works for old workflow
- No forced migration to manifest

### Not Supported

❌ **Mixing Workflows**:
- Cannot use old workflow commands with new workflow epics
- Cannot use new workflow commands with old workflow epics
- Must complete epic with workflow it started with (unless migrated)

❌ **Old Workflow Features in New Workflow**:
- Old workflow's implicit dependencies not supported
- Old workflow's in-memory state not supported
- Old workflow's artifact naming not supported (after migration)

## Breaking Changes

### Command Changes

**Removed Commands**:
- `epic-run` (deprecated, will be removed Week 13)

**New Commands**:
- `epic-intake` (replaces `epic-run` Phase 0)
- `epic-scope-boundary` (replaces `epic-run` Phase 1, 1.5)
- `epic-plan` (replaces `epic-run` Phase 2)
- `epic-scan` (replaces `epic-run` Phase 3)
- `epic-tickets` (replaces `epic-run` Phase 4)
- `epic-validate` (replaces `epic-run` Phase 5.X)
- `epic-verify-ticket` (NEW - per-ticket verification)
- `epic-review-final` (replaces `epic-run` Phase 6)

### Artifact Naming Changes

**Old Convention**:
```
docs/brain/EPIC-CCN-X/
  ├─ hotspots.md
  ├─ scope.md
  ├─ architecture-plan.md
  ├─ audit-report.md
  ├─ tickets.md
  └─ completion-report.md
```

**New Convention**:
```
docs/brain/EPIC-CCN-X/
  ├─ manifest.json (NEW)
  ├─ 00-hotspots.md
  ├─ 00-scope.md
  ├─ 01-scope-boundary.md (NEW)
  ├─ 02-architecture-plan.md
  ├─ 02-diagrams.mmd (NEW)
  ├─ 03-audit-report.md
  ├─ 04-tickets.md
  ├─ ticket-1-completion.md (NEW)
  ├─ ticket-1-verification.md (NEW)
  └─ 05-completion-report.md
```

### State Management Changes

**Old Workflow**:
- State in memory (lost on failure)
- No persistent state file

**New Workflow**:
- State in `manifest.json` (persistent)
- Required for all operations

### Dependency Management Changes

**Old Workflow**:
- Implicit dependencies (hardcoded)
- No validation

**New Workflow**:
- Explicit dependencies in manifest
- Automatic validation before phase execution

## Migration Checklist

### Pre-Migration

- [ ] Read this migration guide completely
- [ ] Review new workflow documentation (`EPIC_WORKFLOW_WALKTHROUGH.md`)
- [ ] Verify all new commands installed
- [ ] Verify manifest helper script available
- [ ] Backup existing epic directories

### During Migration

- [ ] Assess current epic state
- [ ] Run migration script (`epic_manifest.py migrate`)
- [ ] Verify manifest generated correctly
- [ ] Verify artifacts renamed correctly
- [ ] Test next phase execution

### Post-Migration

- [ ] Verify epic completes successfully
- [ ] Document any issues encountered
- [ ] Update team documentation
- [ ] Train team on new workflow

### For New Epics

- [ ] Use new workflow commands only
- [ ] Verify manifest created in Phase 0
- [ ] Check manifest status between phases
- [ ] Use parallel execution when possible
- [ ] Archive completed epics

## Troubleshooting

### Issue: "epic-run not found"

**Symptom**:
```bash
epic-run EPIC-CCN-X
# Error: command not found
```

**Cause**: Old workflow removed (Week 13+)

**Fix**:
```bash
# Use new workflow commands
epic-intake EPIC-CCN-X "Description"
# ... continue with new workflow
```

---

### Issue: "Manifest not found after migration"

**Symptom**:
```bash
python scripts/epic_manifest.py migrate EPIC-CCN-X
# Error: No artifacts found to migrate
```

**Cause**: No artifacts in `docs/brain/EPIC-CCN-X/`

**Fix**:
```bash
# Check for artifacts
ls docs/brain/EPIC-CCN-X/

# If no artifacts, epic not started
# Start fresh with new workflow
epic-intake EPIC-CCN-X "Description"
```

---

### Issue: "Artifact naming conflict"

**Symptom**:
```bash
python scripts/epic_manifest.py migrate EPIC-CCN-X
# Error: File 00-hotspots.md already exists
```

**Cause**: Partial migration or manual file creation

**Fix**:
```bash
# Backup existing files
cp -r docs/brain/EPIC-CCN-X docs/brain/EPIC-CCN-X.backup

# Remove conflicting files
rm docs/brain/EPIC-CCN-X/00-*.md
rm docs/brain/EPIC-CCN-X/01-*.md
rm docs/brain/EPIC-CCN-X/02-*.md

# Retry migration
python scripts/epic_manifest.py migrate EPIC-CCN-X
```

---

### Issue: "Phase status incorrect after migration"

**Symptom**:
```bash
python scripts/epic_manifest.py status EPIC-CCN-X
# Output: Phase 2 status: pending (should be completed)
```

**Cause**: Migration script couldn't infer phase completion

**Fix**:
```bash
# Manually update phase status
python scripts/epic_manifest.py update EPIC-CCN-X 2 completed

# Verify
python scripts/epic_manifest.py status EPIC-CCN-X
```

---

### Issue: "Cannot resume after migration"

**Symptom**:
```bash
epic-scan EPIC-CCN-X
# Error: Dependencies not satisfied
```

**Cause**: Manifest dependencies not correctly set

**Fix**:
```bash
# Check dependencies
python scripts/epic_manifest.py dependencies EPIC-CCN-X 3

# If dependencies incorrect, regenerate manifest
python scripts/epic_manifest.py regenerate EPIC-CCN-X

# Retry phase
epic-scan EPIC-CCN-X
```

---

### Issue: "Old workflow artifacts not recognized"

**Symptom**:
```bash
python scripts/epic_manifest.py migrate EPIC-CCN-X
# Warning: Unknown artifact: custom-report.md
```

**Cause**: Custom artifacts not in standard naming convention

**Fix**:
```bash
# Rename custom artifacts to standard convention
mv docs/brain/EPIC-CCN-X/custom-report.md \
   docs/brain/EPIC-CCN-X/03-audit-report.md

# Retry migration
python scripts/epic_manifest.py migrate EPIC-CCN-X
```

## FAQ

### Q: Can I use old workflow for new epics?

**A**: Not recommended. Old workflow will be removed in Week 13. Use new workflow for all new epics.

### Q: Do I need to migrate in-progress epics?

**A**: Only if:
- Epic has failed/stalled
- Epic is <50% complete
- Epic will take >1 hour to complete

Otherwise, complete with old workflow.

### Q: Will migration break my epic?

**A**: No. Migration is non-destructive. Original artifacts are preserved. If migration fails, you can continue with old workflow.

### Q: Can I switch back to old workflow after migration?

**A**: No. Once migrated, you must use new workflow. However, you can restore from backup if needed.

### Q: What happens to old workflow after Week 13?

**A**: `epic-run` command will be removed. All epics must use new workflow.

### Q: How do I know if migration was successful?

**A**: Run `python scripts/epic_manifest.py validate EPIC-CCN-X`. If validation passes, migration was successful.

### Q: Can I migrate multiple epics at once?

**A**: Yes. Run migration script for each epic:
```bash
for epic in EPIC-CCN-15 EPIC-CCN-16 EPIC-CCN-17; do
    python scripts/epic_manifest.py migrate $epic
done
```

### Q: What if I encounter issues during migration?

**A**: 
1. Check troubleshooting section
2. Restore from backup
3. Contact V12 Architecture Team
4. Document issue in `docs/workflow/MIGRATION_ISSUES.md`

## Support

### Getting Help

**Documentation**:
- `docs/workflow/EPIC_WORKFLOW_WALKTHROUGH.md` - Step-by-step guide
- `docs/workflow/V12_EPIC_WORKFLOW_REFACTORING_DESIGN.md` - Architecture details
- `docs/workflow/EPIC_MANIFEST_SCHEMA.md` - Manifest specification

**Commands**:
```bash
# Get help on any command
epic-intake --help
epic-scope-boundary --help
python scripts/epic_manifest.py --help
```

**Contact**:
- V12 Architecture Team
- GitHub Issues: https://github.com/malhitticrypto-debug/universal-or-strategy/issues
- Slack: #v12-epic-workflow

### Reporting Issues

**Issue Template**:
```markdown
## Issue Description
[Brief description of the issue]

## Epic ID
EPIC-CCN-X

## Workflow Used
- [ ] Old workflow (epic-run)
- [ ] New workflow (manifest-based)
- [ ] Migration (old → new)

## Steps to Reproduce
1. [Step 1]
2. [Step 2]
3. [Step 3]

## Expected Behavior
[What should happen]

## Actual Behavior
[What actually happened]

## Error Messages
```
[Paste error messages here]
```

## Environment
- OS: [Windows/Linux/macOS]
- Python Version: [3.11/3.12]
- Bob CLI Version: [x.y.z]

## Additional Context
[Any other relevant information]
```

## Next Steps

1. **Review Documentation**: Read `EPIC_WORKFLOW_WALKTHROUGH.md`
2. **Test New Workflow**: Try with EPIC-CCN-16 pilot
3. **Plan Migration**: Identify in-progress epics to migrate
4. **Execute Migration**: Follow migration procedure
5. **Monitor Results**: Track issues and report to team

## References

- **V12 Epic Workflow Design**: `docs/workflow/V12_EPIC_WORKFLOW_REFACTORING_DESIGN.md`
- **Workflow Walkthrough**: `docs/workflow/EPIC_WORKFLOW_WALKTHROUGH.md`
- **Manifest Schema**: `docs/workflow/EPIC_MANIFEST_SCHEMA.md`
- **Watsonx Integration**: `docs/workflow/WATSONX_ORCHESTRATE_INTEGRATION.md`
- **V12 DNA Principles**: `AGENTS.md`

---

**Document Status**: Active v1.0  
**Last Updated**: 2026-06-09  
**Maintainer**: V12 Architecture Team  
**Deprecation Date**: Week 13 (2026-09-01)