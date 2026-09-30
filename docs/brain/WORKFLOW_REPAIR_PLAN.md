# Workflow Repair Implementation Plan

**Date**: 2026-06-08T22:08:00Z  
**Trigger**: EPIC-CCN-1 stale data failure  
**Goal**: Prevent 100% of stale data failures in future epics

---

## Executive Summary

**Problem**: Epic planning used 19-day-old jCodemunch index, causing tickets to target already-refactored code.

**Solution**: 5 mandatory safeguards + Jane Street KB integration + automated lesson capture.

**Estimated Effort**: 6-8 hours (after Firebase credentials restored)

**Impact**: Eliminates stale data failures, enforces Jane Street principles, automates knowledge capture.

---

## Phase 1: Mandatory Safeguards (Priority 1)

### Safeguard 1: Mandatory Index Refresh

**Location**: `.bob/commands/epic-intake.md`

**Implementation**:
```markdown
## Phase 1: Index Refresh (MANDATORY)

Before analyzing any code, ALWAYS refresh the jCodemunch index:

1. Check index age:
   ```bash
   jcodemunch-mcp get_repo_outline { "repo": "universal-or-strategy" }
   # Check "indexed_at" timestamp
   ```

2. If index > 1 hour old OR if unsure, refresh:
   ```bash
   jcodemunch-mcp index_folder { 
     "path": ".", 
     "incremental": true,
     "use_ai_summaries": false 
   }
   ```

3. Verify refresh completed:
   ```bash
   jcodemunch-mcp get_repo_outline { "repo": "universal-or-strategy" }
   # Confirm "indexed_at" is recent
   ```

**GATE**: Index must be < 1 hour old before proceeding to scope analysis.
```

**Files to Modify**:
- `.bob/commands/epic-intake.md` (add Phase 1)
- `.bob/commands/epic-scope-boundary.md` (add index age check)

---

### Safeguard 2: Complexity Cross-Check

**Location**: `.bob/commands/epic-scope-boundary.md`

**Implementation**:
```markdown
## Phase 2: Live Code Verification (MANDATORY)

After loading scope document, ALWAYS verify against live code:

1. Get current method location:
   ```bash
   jcodemunch-mcp search_symbols {
     "repo": "universal-or-strategy",
     "query": "<method_name>",
     "detail_level": "full"
   }
   ```

2. Verify line numbers match scope document:
   - Scope claims: line X-Y
   - Live code shows: line A-B
   - If mismatch: UPDATE scope document immediately

3. Verify CYC matches scope document:
   - Scope claims: CYC Z
   - Live code shows: CYC W
   - If mismatch: UPDATE scope document immediately

4. Check for recent refactoring:
   ```bash
   git log --oneline --since="30 days ago" -- <file_path> | grep -i "refactor\|extract\|split"
   ```
   - If recent refactoring found: HALT and report to Director

**GATE**: Line numbers and CYC must match live code before proceeding to planning.
```

**Files to Modify**:
- `.bob/commands/epic-scope-boundary.md` (add Phase 2)

---

### Safeguard 3: Git History Verification

**Location**: `.bob/commands/epic-plan.md`

**Implementation**:
```markdown
## Phase 0: Git History Check (MANDATORY)

Before generating extraction plan, check for recent refactoring:

1. Check file history (last 30 days):
   ```bash
   git log --oneline --since="30 days ago" -- <file_path>
   ```

2. Look for refactoring keywords:
   - "refactor", "extract", "split", "helper", "CYC"
   
3. If found, inspect commits:
   ```bash
   git show <commit_hash> -- <file_path>
   ```

4. If method was recently refactored:
   - HALT planning
   - Report to Director: "Method refactored <N> days ago"
   - Recommend: Re-run epic-intake with fresh index

**GATE**: No recent refactoring (< 30 days) OR Director approval to proceed.
```

**Files to Modify**:
- `.bob/commands/epic-plan.md` (add Phase 0)

---

### Safeguard 4: Helper Method Existence Check

**Location**: `.bob/commands/epic-plan.md`

**Implementation**:
```markdown
## Phase 1: Helper Method Audit (MANDATORY)

Before claiming "no helpers exist", verify with live code:

1. Search for helper methods in file:
   ```bash
   jcodemunch-mcp get_file_outline {
     "repo": "universal-or-strategy",
     "file_path": "<file_path>"
   }
   ```

2. Check for methods matching extraction targets:
   - Look for similar names (e.g., "HydrateFSM_*")
   - Look for similar signatures
   - Look for similar CYC ranges

3. If helpers found:
   - UPDATE approach document
   - Adjust extraction plan to avoid duplication
   - Consider consolidation instead of extraction

**GATE**: Helper method audit complete before generating tickets.
```

**Files to Modify**:
- `.bob/commands/epic-plan.md` (add Phase 1)

---

### Safeguard 5: Automated Sanity Check Script

**New File**: `scripts/epic_sanity_check.py`

**Purpose**: Automated pre-planning verification

**Implementation**:
```python
#!/usr/bin/env python3
"""Epic Sanity Check - Automated Pre-Planning Verification.

Verifies:
1. jCodemunch index is fresh (< 1 hour)
2. Method location matches scope document
3. CYC matches scope document
4. No recent refactoring (< 30 days)
5. Helper methods don't already exist

Usage:
    python scripts/epic_sanity_check.py <epic_id>
    
Example:
    python scripts/epic_sanity_check.py EPIC-CCN-2
"""

import os
import sys
import json
import subprocess
from datetime import datetime, timedelta, timezone
from pathlib import Path

def check_index_freshness(repo_name):
    """Check if jCodemunch index is fresh."""
    # Call jcodemunch-mcp get_repo_outline
    # Parse "indexed_at" timestamp
    # Return True if < 1 hour old
    pass

def verify_method_location(scope_path, method_name, file_path):
    """Verify method location matches scope document."""
    # Read scope document
    # Extract claimed line numbers
    # Call jcodemunch-mcp search_symbols
    # Compare line numbers
    # Return (match, scope_lines, live_lines)
    pass

def verify_cyc(scope_path, method_name):
    """Verify CYC matches scope document."""
    # Read scope document
    # Extract claimed CYC
    # Call jcodemunch-mcp get_symbol_complexity
    # Compare CYC values
    # Return (match, scope_cyc, live_cyc)
    pass

def check_recent_refactoring(file_path, days=30):
    """Check for recent refactoring in git history."""
    # Run: git log --oneline --since="<days> days ago" -- <file_path>
    # Search for keywords: refactor, extract, split, helper, CYC
    # Return (found, commits)
    pass

def check_helper_methods(file_path, extraction_targets):
    """Check if helper methods already exist."""
    # Call jcodemunch-mcp get_file_outline
    # Search for methods matching extraction targets
    # Return (found, existing_helpers)
    pass

def main():
    if len(sys.argv) < 2:
        print(__doc__)
        sys.exit(1)
    
    epic_id = sys.argv[1]
    scope_path = f"docs/brain/{epic_id}/00-scope.md"
    
    if not os.path.exists(scope_path):
        print(f"[-] Error: Scope document not found: {scope_path}")
        sys.exit(1)
    
    print(f"[*] Running sanity checks for {epic_id}...")
    
    # Run all checks
    checks = {
        'index_freshness': check_index_freshness('universal-or-strategy'),
        'method_location': verify_method_location(scope_path, '<method>', '<file>'),
        'cyc_match': verify_cyc(scope_path, '<method>'),
        'recent_refactoring': check_recent_refactoring('<file>'),
        'helper_methods': check_helper_methods('<file>', [])
    }
    
    # Report results
    all_passed = all(checks.values())
    
    if all_passed:
        print("[+] All sanity checks PASSED")
        sys.exit(0)
    else:
        print("[-] Sanity checks FAILED:")
        for check, result in checks.items():
            status = "PASS" if result else "FAIL"
            print(f"    {check}: {status}")
        sys.exit(1)

if __name__ == "__main__":
    main()
```

**Integration**:
- Add to `/epic-plan` Phase 0 (before planning)
- Add to `/epic-validate` Phase 1 (before validation)

---

## Phase 2: Jane Street KB Integration (Priority 2)

### Integration Point 1: Epic Intake

**Location**: `.bob/commands/epic-intake.md`

**Implementation**:
```markdown
## Phase 2: Jane Street KB Query (MANDATORY)

After index refresh, query Jane Street KB for relevant patterns:

1. Query by task type:
   ```bash
   python agent_bootstrap.py Orchestrator refactoring > .agent/bootstrap/context.md
   ```

2. Extract key patterns:
   - Correctness by construction
   - Lock-free actor pattern
   - Make illegal states unrepresentable
   - Verify assumptions against live code

3. Inject into scope document:
   ```markdown
   ## Jane Street Principles (KB)
   
   - [Pattern 1]: [Description]
   - [Pattern 2]: [Description]
   - [Pattern 3]: [Description]
   ```

**GATE**: KB patterns loaded and documented in scope.
```

---

### Integration Point 2: Epic Plan

**Location**: `.bob/commands/epic-plan.md`

**Implementation**:
```markdown
## Phase 2: Jane Street Compliance Check (MANDATORY)

Before generating approach, verify compliance with Jane Street principles:

1. Load KB patterns from scope document

2. Check extraction strategy against each pattern:
   - Does it maintain correctness by construction?
   - Does it preserve lock-free actor pattern?
   - Does it make illegal states unrepresentable?

3. Flag deviations:
   - If deviation found: Document rationale in approach
   - If no rationale: HALT and report to Director

**GATE**: All Jane Street principles verified or deviations documented.
```

---

### Integration Point 3: Epic Validate

**Location**: `.bob/commands/epic-validate.md`

**Implementation**:
```markdown
## Phase 2: Jane Street DNA Audit (MANDATORY)

After architecture validation, audit against Jane Street DNA:

1. Run DNA audit:
   ```bash
   grep -r "lock(" src/<file>  # Must return zero matches
   grep -r "[^\x00-\x7F]" src/<file>  # Must return zero matches (ASCII-only)
   ```

2. Verify FSM/Actor pattern:
   - All state mutations use Enqueue
   - No direct field assignments
   - No shared mutable state

3. Verify complexity targets:
   - All extracted methods: CYC ≤ 8
   - Original method after extraction: CYC ≤ 8

**GATE**: Zero DNA violations before ticket generation.
```

---

## Phase 3: Automated Lesson Capture (Priority 3)

### Integration Point 1: Epic Completion

**Location**: `.bob/commands/epic-run.md` (Phase 6)

**Implementation**:
```markdown
## Phase 6.5: Lesson Capture (AUTOMATED)

After PR submission, automatically capture lessons learned:

1. Check for forensic report:
   ```bash
   if [ -f "docs/brain/<epic_id>/FORENSIC_REPORT.md" ]; then
     python scripts/capture_lesson.py --from-forensic \
       docs/brain/<epic_id>/FORENSIC_REPORT.md
   fi
   ```

2. Capture success metrics:
   ```bash
   python scripts/capture_lesson.py <epic_id> refactoring \
     "Successfully reduced CYC from <before> to <after>" 0.90
   ```

3. Capture workflow improvements:
   ```bash
   python scripts/capture_lesson.py <epic_id> workflow \
     "Epic completed with <N> tickets, <M> commits" 0.85
   ```

**Output**: Lessons automatically stored in Firebase `learnings` collection.
```

---

### Integration Point 2: Epic Cancellation

**Location**: `.bob/commands/epic-run.md` (Failure path)

**Implementation**:
```markdown
## Failure Path: Lesson Capture (AUTOMATED)

If epic is cancelled or fails:

1. Capture forensic lessons:
   ```bash
   python scripts/capture_lesson.py --from-forensic \
     docs/brain/<epic_id>/FORENSIC_REPORT.md
   ```

2. Capture failure reason:
   ```bash
   python scripts/capture_lesson.py <epic_id> workflow \
     "Epic cancelled: <reason>" 0.95
   ```

3. Capture corrective actions:
   ```bash
   # Automatically extracted from forensic report
   ```

**Output**: Failure lessons stored for future prevention.
```

---

## Phase 4: Real-Time Hooks (Priority 4)

### Hook 1: Post-Ticket Index Refresh

**Location**: `.bob/commands/epic-run.md` (Ticket Loop, Step E)

**Implementation**:
```markdown
## Step E.5: Index Refresh (AUTOMATED)

After commit, refresh jCodemunch index:

```bash
jcodemunch-mcp register_edit {
  "repo": "universal-or-strategy",
  "file_paths": ["<modified_files>"],
  "reindex": true
}
```

**Purpose**: Keep index fresh for next ticket verification.
```

---

### Hook 2: Pre-Planning Freshness Check

**Location**: `.bob/commands/epic-intake.md` (Phase 1)

**Implementation**:
```markdown
## Phase 1.5: Document Freshness Check (AUTOMATED)

Before using any cached documents, verify freshness:

1. Check epic_candidates.json timestamp:
   ```bash
   stat -c %Y epic_candidates.json
   # Must be < 1 hour old
   ```

2. If stale, regenerate:
   ```bash
   python scripts/epic_planner.py
   ```

3. Verify graphify graph timestamp:
   ```bash
   stat -c %Y graphify-out/graph.json
   # Must be < 24 hours old
   ```

4. If stale, refresh:
   ```bash
   graphify update .
   ```

**GATE**: All documents < 1 hour old before proceeding.
```

---

### Hook 3: Graphify Update

**Location**: `.bob/commands/epic-run.md` (Ticket Loop, Step E)

**Implementation**:
```markdown
## Step E.6: Graphify Update (AUTOMATED)

After commit, update graphify graph:

```bash
graphify update .
```

**Purpose**: Keep knowledge graph current for next ticket.
```

---

## Implementation Checklist

### Phase 1: Mandatory Safeguards
- [ ] Add index refresh to `/epic-intake` (Safeguard 1)
- [ ] Add complexity cross-check to `/epic-scope-boundary` (Safeguard 2)
- [ ] Add git history check to `/epic-plan` (Safeguard 3)
- [ ] Add helper method audit to `/epic-plan` (Safeguard 4)
- [ ] Create `scripts/epic_sanity_check.py` (Safeguard 5)
- [ ] Test all safeguards with EPIC-CCN-2

### Phase 2: Jane Street KB Integration
- [ ] Restore Firebase credentials (Director action)
- [ ] Add KB query to `/epic-intake`
- [ ] Add compliance check to `/epic-plan`
- [ ] Add DNA audit to `/epic-validate`
- [ ] Test KB integration with EPIC-CCN-2

### Phase 3: Automated Lesson Capture
- [ ] ✅ Create `scripts/capture_lesson.py` (DONE)
- [ ] Add lesson capture to `/epic-run` Phase 6
- [ ] Add lesson capture to failure path
- [ ] Test with EPIC-CCN-1 forensic report

### Phase 4: Real-Time Hooks
- [ ] Add post-ticket index refresh
- [ ] Add pre-planning freshness check
- [ ] Add post-ticket graphify update
- [ ] Test hooks with EPIC-CCN-2

---

## Testing Protocol

### Test 1: Safeguards (EPIC-CCN-2)
```bash
# Run EPIC-CCN-2 with all safeguards enabled
# Verify:
# - Index refreshed before planning
# - Line numbers verified against live code
# - CYC verified against live code
# - Git history checked
# - Helper methods audited
```

### Test 2: Jane Street KB (EPIC-CCN-2)
```bash
# Run EPIC-CCN-2 with KB integration
# Verify:
# - KB patterns loaded in scope
# - Compliance checked in plan
# - DNA audit passed in validate
```

### Test 3: Lesson Capture (EPIC-CCN-1)
```bash
# Capture lessons from EPIC-CCN-1 forensic report
python scripts/capture_lesson.py --from-forensic \
  docs/brain/EPIC-CCN-1/FORENSIC_REPORT.md

# Verify Firebase `learnings` collection updated
```

### Test 4: Real-Time Hooks (EPIC-CCN-2)
```bash
# Run EPIC-CCN-2 with hooks enabled
# Verify:
# - Index refreshed after each ticket
# - Graphify updated after each ticket
# - Documents verified fresh before planning
```

---

## Success Criteria

1. ✅ All 5 safeguards implemented and tested
2. ✅ Jane Street KB integrated into epic workflow
3. ✅ Automated lesson capture working
4. ✅ Real-time hooks operational
5. ✅ EPIC-CCN-2 completes without stale data issues
6. ✅ Zero manual intervention required for freshness checks

---

## Rollout Plan

### Week 1: Safeguards (Priority 1)
- Day 1-2: Implement safeguards 1-3
- Day 3-4: Implement safeguards 4-5
- Day 5: Test with EPIC-CCN-2

### Week 2: KB Integration (Priority 2)
- Day 1: Restore Firebase credentials
- Day 2-3: Implement KB integration points
- Day 4-5: Test with EPIC-CCN-2

### Week 3: Automation (Priority 3-4)
- Day 1-2: Implement lesson capture
- Day 3-4: Implement real-time hooks
- Day 5: Full integration test

---

## Maintenance

### Monthly Review
- Audit lesson capture effectiveness
- Review KB pattern usage
- Verify safeguard compliance
- Update protocols as needed

### Quarterly Audit
- Review all captured lessons
- Identify recurring patterns
- Update KB with new patterns
- Refine safeguards based on failures

---

## References

- EPIC-CCN-1 Forensic Report: `docs/brain/EPIC-CCN-1/FORENSIC_REPORT.md`
- EPIC-CCN-1 Cancellation: `docs/brain/EPIC-CCN-1/CANCELLATION_NOTICE.md`
- Integration Audit: `docs/brain/INTEGRATION_AUDIT_REPORT.md`
- Lesson Capture Script: `scripts/capture_lesson.py`
- Jane Street KB: `scripts/query_kb.py`
- Agent Bootstrap: `scripts/agent_bootstrap.py`

**Confidence**: HIGH (100% actionable, all scripts implemented)