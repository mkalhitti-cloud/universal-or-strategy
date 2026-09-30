# Immediate Implementation Plan
**Session**: 2026-06-08T22:37:00Z
**Goal**: Implement missing infrastructure before resuming refactoring

## Status: PROMPT CACHING NEVER IMPLEMENTED

**Discovery**: The 2026-05-23 audit identified ~42,500 tokens of cacheable content but it was never activated in Bob settings.

---

## Implementation Tasks (Sequential)

### Task 1: Enable Prompt Caching (15 minutes)
**File**: `.bob/settings.json`

**Add**:
```json
{
  "general": {
    "checkpointing": {
      "enabled": true
    },
    "prompt_caching": {
      "enabled": true,
      "provider": "anthropic",
      "cache_ttl": "5m",
      "cacheable_content": [
        "AGENTS.md",
        "CLAUDE.md",
        "BOB.md",
        ".bob/rules-v12-engineer/dna.md",
        "docs/standards/jane-street/RULES_CATALOG.md"
      ]
    },
    "hooks": {
      "pre_task": [
        "python .bob/hooks/pre_task_jane_street_kb.py"
      ],
      "after_task": [
        "python .bob/hooks/after_task_complete.py"
      ]
    }
  }
}
```

**Benefit**: 75% reduction in input token costs for repeated context

---

### Task 2: Implement Stale Data Detection (30 minutes)
**File**: `scripts/verify_index_freshness.py`

**Purpose**: Prevent EPIC-CCN-1 failure mode (19-day-old stale index)

**Logic**:
```python
def verify_index_freshness(repo_path: str, max_age_days: int = 7) -> dict:
    """
    Verify jCodemunch index is fresh.
    
    Returns:
        {
            "fresh": bool,
            "index_age_days": int,
            "last_indexed": str (ISO timestamp),
            "stale_files": list[str],
            "action_required": str
        }
    """
    # 1. Check graphify-out/graph.json timestamp
    # 2. Compare to git HEAD commit timestamp
    # 3. If delta > max_age_days: return fresh=False
    # 4. List files modified since last index
    # 5. Return action: "reindex" or "ok"
```

**Integration**: Add to `/epic-run` Phase 0 (before forensic analysis)

---

### Task 3: Add Index Freshness Gate to /epic-run (15 minutes)
**File**: `.bob/commands/epic-run.md`

**Add to Phase 0** (before HOTSPOT ANALYSIS):
```markdown
## PHASE -1: INDEX FRESHNESS VERIFICATION

**Switch to: Advanced mode**

Hand off this exact task:
```
TASK: Verify Index Freshness
PROTOCOL:
  1. Run: python scripts/verify_index_freshness.py
  2. If fresh=False:
     - Run: graphify update .
     - Run: jcodemunch index_folder { "path": "." }
     - Emit: [INDEX-REFRESHED]
  3. If fresh=True:
     - Emit: [INDEX-FRESH]
```

**GATE -1:**
> "Index status: [FRESH/STALE]. Age: [N days]. Reply GO to proceed."

- GO: advance to Phase 0
- STALE: index was refreshed, verify and GO
```

---

### Task 4: Implement Automated Lesson Capture Hook (20 minutes)
**File**: `.bob/hooks/after_epic_failure.py`

**Purpose**: Auto-capture lessons when epic fails

**Logic**:
```python
def after_epic_failure(epic_id: str, failure_reason: str):
    """
    Automatically capture lesson from epic failure.
    
    Triggered when:
    - /epic-run outputs [EPIC-FAILED]
    - Forensic report exists in docs/brain/{epic_id}/
    """
    # 1. Read forensic report
    # 2. Extract root cause
    # 3. Call scripts/capture_lesson.py
    # 4. Update autonomous_refactor_session.json
```

**Integration**: Add to `.bob/settings.json` hooks

---

### Task 5: Add Jane Street KB Query to /epic-plan (10 minutes)
**File**: `.bob/commands/epic-run.md`

**Modify Phase 2** (PLAN):
```markdown
## PHASE 2: PLAN

**Switch to: v12-epic-planner mode**

Hand off this exact task:
```
EPIC: $1
TASK: Run /epic-plan with Jane Street KB integration
INPUT: @docs/brain/$1/00-scope.md
PROTOCOL:
  1. Query Jane Street KB for relevant patterns:
     python scripts/query_kb.py "complexity reduction"
     python scripts/query_kb.py "FSM extraction"
  2. Apply patterns to analysis
  3. Write docs/brain/$1/01-analysis.md and 02-approach.md
OUTPUT: Write docs/brain/$1/01-analysis.md and docs/brain/$1/02-approach.md
STOP at [PLAN-GATE] and do not proceed.
```
```

---

## Implementation Order

1. ✅ **Prompt Caching** (Task 1) - 15 min - IMMEDIATE COST SAVINGS
2. ✅ **Stale Data Detection** (Task 2) - 30 min - PREVENTS EPIC-CCN-1 FAILURE
3. ✅ **Index Freshness Gate** (Task 3) - 15 min - INTEGRATES DETECTION
4. ✅ **Lesson Capture Hook** (Task 4) - 20 min - AUTO-LEARNING
5. ✅ **Jane Street KB Integration** (Task 5) - 10 min - PATTERN REUSE

**Total Time**: 90 minutes (1.5 hours)

---

## Success Criteria

- [ ] Bob sessions show cache hits in token usage
- [ ] `/epic-run` verifies index freshness before Phase 0
- [ ] Stale index triggers automatic refresh
- [ ] Epic failures auto-capture lessons to Firebase
- [ ] `/epic-plan` queries Jane Street KB for patterns

---

## After Implementation

**Resume**: EPIC-CCN-2 execution with full safeguards active

**Expected**: Zero stale data failures, 75% token cost reduction, automatic lesson learning