# Building Blocks Index - Autonomous Refactor Scripts

**Purpose**: Track which scripts are APPROVED templates for future waves.

**Protocol**: Before using ANY script as a template, check this index first.

---

## Phase 5 Scripts (Ticket Execution)

### ✅ APPROVED TEMPLATES

| Script | Purpose | Pattern | Notes |
|--------|---------|---------|-------|
| `launch_remaining_epics.sh` | Multi-epic orchestrator | PARALLEL (gated per epic) | Proven in Wave 2 |
| `complete_epic_108_proper.sh` | Single epic orchestrator | GATED (sequential tickets) | Proven in Wave 2 |
| `_p5_*.sh` | Individual ticket execution | Single ticket | Generated per ticket |
| `_p5v_*.sh` | Individual ticket validation | Single ticket | Generated per ticket |

**Key Pattern**: Each epic runs tickets sequentially (gated), but multiple epics can run in parallel.

### ❌ DO NOT USE
- None (Phase 5 scripts are all correct)

---

## Phase 6 Scripts (Epic Reviews)

### ✅ APPROVED TEMPLATES

| Script | Purpose | Pattern | Notes |
|--------|---------|---------|-------|
| `launch_phase6_all_epics_PARALLEL.sh` | Multi-epic orchestrator | PARALLEL (3 workers) | **USE THIS VERSION** |
| `_p6_*.sh` | Individual epic review | Single epic | Generated per epic |

**Key Pattern**: All epics run in parallel with ThreadPoolExecutor (max_workers=3).

### ❌ DO NOT USE

| Script | Reason | Impact | Archived Date |
|--------|--------|--------|---------------|
| `launch_phase6_all_epics.sh.SEQUENTIAL_ERROR` | Sequential execution | 2-3x slower | 2026-06-13 |

**Error Details**:
- **Problem**: Uses `wait_for_completion()` loop instead of `ThreadPoolExecutor`
- **Impact**: 21 minutes vs ~7-10 minutes (2-3x slower)
- **Why Archived**: Forensic evidence for Wave 2 pilot
- **Replacement**: `launch_phase6_all_epics_PARALLEL.sh`

---

## Script Selection Rules

When creating scripts for future waves:

### 1. Check This Index First
- ✅ Verify script is in APPROVED list
- ❌ Avoid scripts in DO NOT USE list

### 2. Use Correct Suffix
- If multiple versions exist, use `_PARALLEL.sh` suffix
- If only one version exists, use as-is

### 3. Verify Pattern
```python
# ✅ CORRECT - Parallel execution
from concurrent.futures import ThreadPoolExecutor, as_completed

with ThreadPoolExecutor(max_workers=3) as executor:
    futures = {executor.submit(run_task, item): item for item in items}
    for future in as_completed(futures):
        result = future.result()
```

```python
# ❌ WRONG - Sequential execution
for item in items:
    run_task(item)
    wait_for_completion(item)  # Blocks!
```

### 4. Smoke Test Before Deployment
- Test with 2 items (epics/tickets) first
- Verify parallel execution (check CPU usage ~75% with 3 workers)
- Verify logs show concurrent execution

---

## Pattern Recognition Guide

### Parallel Execution Indicators (✅ GOOD)
- `ThreadPoolExecutor(max_workers=N)`
- `as_completed(futures)`
- `parallel -j N` (GNU parallel)
- `xargs -P N` (parallel xargs)
- Multiple screen sessions launched without waiting

### Sequential Execution Indicators (❌ BAD)
- `wait_for_completion()` inside loop
- `for item in items: run(item); wait(item)`
- Single screen session at a time
- No ThreadPoolExecutor or parallel tools

---

## Wave-Specific Notes

### Wave 2 (Pilot)
- **Phase 5**: ✅ Correct (gated per epic, parallel across epics)
- **Phase 6**: ❌ Error (sequential instead of parallel)
- **Remediation**: Created `_PARALLEL.sh` version, archived sequential script
- **Lesson**: Always smoke test with 2 items before full deployment

### Wave 3+ (Future)
- **Requirement**: Use Building Blocks Index for ALL script selection
- **Pre-Wave Checklist**: Verify scripts against this index
- **Smoke Test**: Mandatory for any new orchestrator script

---

## Pre-Wave Checklist

Before starting any wave:

- [ ] Review Building Blocks Index
- [ ] Identify which scripts to use as templates
- [ ] Verify scripts are in APPROVED list
- [ ] Check for `_PARALLEL.sh` suffix if multiple versions exist
- [ ] Smoke test orchestrator with 2 items
- [ ] Verify parallel execution (CPU usage, concurrent logs)
- [ ] Document any new patterns discovered

---

## Adding New Scripts to Index

When a new script is created and proven:

1. **Test thoroughly** (smoke test + full deployment)
2. **Document pattern** (parallel/sequential/gated)
3. **Add to APPROVED list** with notes
4. **Update this index** via PR
5. **Reference in SOP** (e.g., PARALLEL_EXECUTION_SOP.md)

---

## References

- **Parallel Execution SOP**: `docs/protocol/PARALLEL_EXECUTION_SOP.md`
- **Wave 2 Remediation**: `docs/protocol/WAVE2_SCRIPT_POLLUTION_REMEDIATION.md`
- **Autonomous Refactor Command**: `.bob/commands/autonomous-refactor.md`
- **Building Blocks Method**: Reuse proven scripts, modify only what's needed

---

## Quick Reference

**Q**: Which Phase 6 orchestrator should I use?  
**A**: `launch_phase6_all_epics_PARALLEL.sh` (NOT the `.SEQUENTIAL_ERROR` version)

**Q**: Can I use `launch_remaining_epics.sh` for Phase 5?  
**A**: Yes, it's APPROVED and proven in Wave 2

**Q**: How do I know if a script is parallel or sequential?  
**A**: Check for `ThreadPoolExecutor` or `parallel -j N` (parallel) vs `wait_for_completion()` in loop (sequential)

**Q**: What if I need to modify a script?  
**A**: Start with APPROVED template, modify only what's needed, smoke test before deployment

---

**Version**: V12.25  
**Last Updated**: 2026-06-13  
**Maintainer**: Orchestrator (Bob)  
**Status**: ACTIVE