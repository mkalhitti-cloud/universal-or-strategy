---
name: epic-review-tickets
description: '# /epic-review-tickets - Phase 4.5 Ticket Review (Automated)'
metadata:
  user-invocable: true
  disable-model-invocation: true
---

# /epic-review-tickets - Phase 4.5 Ticket Review (Automated)

**Version**: 1.0  
**Date**: 2026-06-21  
**Phase**: 4.5 (Ticket Review - Jane Street Validation Gate)  
**Mode**: `v12-phase4-5-review`  
**Status**: Automated (no manual review required)

## Purpose

Automate Phase 4.5 (Ticket Review) to validate generated tickets against Jane Street KB rules and V12 DNA principles. This eliminates the manual review gate and enables fully autonomous epic execution.

## Command Syntax

```bash
/epic-review-tickets EPIC-ID
```

**Example**:
```bash
/epic-review-tickets EPIC-CCN-027
```

## Prerequisites

- ✅ Phase 4 complete (`04-tickets.md` exists)
- ✅ Jane Street KB accessible (`scripts/query_kb.py`)
- ✅ Sequential Thinking MCP available

## Workflow

### Step 1: Load Tickets

**Mode**: `v12-phase4-5-review`

**Task**: Load and parse tickets from Phase 4

```markdown
TASK: Load Tickets for Review
PROTOCOL:
  1. Read: docs/brain/EPIC-{ID}/04-tickets.md
  2. Parse ticket structure:
     - Ticket count
     - Each ticket's scope
     - Each ticket's complexity target
     - Each ticket's dependencies
  3. Emit: [TICKETS-LOADED] N tickets parsed
```

### Step 2: Jane Street KB Validation

**Mode**: `v12-phase4-5-review`

**Task**: Query Jane Street KB for ticket validation rules

```markdown
TASK: Query Jane Street KB
PROTOCOL:
  1. Query topics:
     - "ticket generation best practices"
     - "complexity reduction patterns"
     - "scope boundary validation"
     - "FSM extraction patterns"
  2. Load rules:
     - Single-method scope mandate
     - CYC ≤ 8 target
     - No scope creep patterns
     - Test coverage requirements
  3. Emit: [KB-LOADED] X rules retrieved
```

### Step 3: Automated Validation Checks

**Mode**: `v12-phase4-5-review`

**Task**: Run automated validation checks using Sequential Thinking MCP

```markdown
TASK: Validate Tickets Against Rules
PROTOCOL:
  Use Sequential Thinking MCP to validate each ticket:
  
  CHECK 1: Single-Method Scope
    - Verify each ticket targets exactly ONE method
    - BLOCKER if ticket spans multiple methods
    - Emit: [SCOPE-CHECK] Pass/Fail per ticket
  
  CHECK 2: Complexity Target
    - Verify each ticket targets CYC ≤ 8
    - BLOCKER if target exceeds threshold
    - Emit: [COMPLEXITY-CHECK] Pass/Fail per ticket
  
  CHECK 3: No Scope Creep
    - Verify no "while we're here" improvements
    - Verify no pre-existing error fixes
    - BLOCKER if scope creep detected
    - Emit: [SCOPE-CREEP-CHECK] Pass/Fail per ticket
  
  CHECK 4: Ticket Independence
    - Verify tickets can execute in parallel
    - Verify no circular dependencies
    - WARNING if dependencies detected
    - Emit: [INDEPENDENCE-CHECK] Pass/Warn per ticket
  
  CHECK 5: Test Coverage
    - Verify each ticket includes xUnit test generation
    - BLOCKER if test generation missing
    - Emit: [TEST-CHECK] Pass/Fail per ticket
  
  CHECK 6: UTF-8 Encoding
    - Verify each ticket includes encoding verification
    - BLOCKER if encoding check missing
    - Emit: [ENCODING-CHECK] Pass/Fail per ticket
```

### Step 4: Generate Review Report

**Mode**: `v12-phase4-5-review`

**Task**: Write validation report

```markdown
TASK: Generate Review Report
PROTOCOL:
  1. Create: docs/brain/EPIC-{ID}/04-5-ticket-review.md
  2. Include sections:
     - Validation Summary (Pass/Fail/Warn counts)
     - Per-Ticket Results (all 6 checks)
     - Blocker Issues (if any)
     - Recommendations (if warnings)
     - Jane Street KB References (rules applied)
     - Agent Tracking (bobcoins, API key, duration)
  3. Emit: [REVIEW-COMPLETE]
```

### Step 5: Decision Gate

**Mode**: `v12-phase4-5-review`

**Task**: Determine if tickets are approved

```markdown
TASK: Approval Decision
PROTOCOL:
  IF all checks PASS:
    - Status: APPROVED
    - Emit: [TICKETS-APPROVED] Ready for Phase 5
    - Exit: Success
  
  IF any check BLOCKER:
    - Status: REJECTED
    - Emit: [TICKETS-REJECTED] Fix required
    - List: Blocker issues
    - Exit: Failure (epic halts)
  
  IF only WARNINGS:
    - Status: APPROVED WITH WARNINGS
    - Emit: [TICKETS-APPROVED-WARN] Proceed with caution
    - List: Warning issues
    - Exit: Success
```

## Output Files

### Primary Output

**File**: `docs/brain/EPIC-{ID}/04-5-ticket-review.md`

**Structure**:
```markdown
# Phase 4.5: Ticket Review Report

**Epic**: EPIC-{ID}
**Date**: {timestamp}
**Status**: APPROVED | REJECTED | APPROVED WITH WARNINGS

## Validation Summary

- Total Tickets: N
- Passed: X
- Failed: Y
- Warnings: Z

## Per-Ticket Results

### Ticket 1: {Title}
- ✅ Single-Method Scope: PASS
- ✅ Complexity Target: PASS (CYC ≤ 8)
- ✅ No Scope Creep: PASS
- ⚠️ Ticket Independence: WARN (depends on Ticket 2)
- ✅ Test Coverage: PASS
- ✅ UTF-8 Encoding: PASS

### Ticket 2: {Title}
...

## Blocker Issues

(If any)

## Recommendations

(If warnings)

## Jane Street KB References

- Rule 1: Single-method extraction mandate
- Rule 2: CYC ≤ 8 strict threshold
- Rule 3: No scope creep protocol

## Agent Tracking

- Agent Name: v12-phase4-5-review
- Bobcoins Used: {amount}
- API Key: {key}
- Execution Time: {duration}
```

## Integration with /epic-run

**Current**: Phase 4 → (Manual Review) → Phase 5

**New**: Phase 4 → `/epic-review-tickets` → Phase 5

**Update Required**: `.bob/commands/epic-run.md`

Add after Phase 4 (Ticket Generation):

```markdown
### Step E: Ticket Review (Phase 4.5)

**Switch to: v12-phase4-5-review mode**

Hand off:
```
TASK: Review Generated Tickets
EPIC: EPIC-{ID}
PROTOCOL:
  1. Run: /epic-review-tickets EPIC-{ID}
  2. Wait for: [TICKETS-APPROVED] or [TICKETS-REJECTED]
  3. If APPROVED: Continue to Phase 5
  4. If REJECTED: HALT epic, report blockers to Director
```
```

## Validation Criteria

### PASS Criteria

- ✅ All 6 checks pass for all tickets
- ✅ No blocker issues
- ✅ Review report generated
- ✅ Agent tracking complete

### FAIL Criteria

- ❌ Any ticket fails single-method scope check
- ❌ Any ticket targets CYC > 8
- ❌ Scope creep detected in any ticket
- ❌ Test coverage missing in any ticket
- ❌ UTF-8 encoding check missing in any ticket

### WARNING Criteria

- ⚠️ Ticket dependencies detected (not blocker, but noted)
- ⚠️ Complexity reduction strategy unclear (not blocker, but noted)

## Error Handling

### Scenario 1: Tickets File Missing

```
ERROR: docs/brain/EPIC-{ID}/04-tickets.md not found
ACTION: HALT - Phase 4 must complete first
```

### Scenario 2: Jane Street KB Unavailable

```
ERROR: scripts/query_kb.py failed
ACTION: HALT - KB required for validation
```

### Scenario 3: Sequential Thinking MCP Unavailable

```
ERROR: Sequential Thinking MCP not available
ACTION: HALT - MCP required for validation logic
```

### Scenario 4: Validation Fails

```
ERROR: Ticket X failed CHECK Y
ACTION: HALT - Report blockers, require ticket regeneration
```

## Success Criteria

- ✅ All tickets validated against Jane Street KB
- ✅ All 6 checks pass (or warnings only)
- ✅ Review report generated
- ✅ Status: APPROVED or APPROVED WITH WARNINGS
- ✅ Agent tracking complete
- ✅ Ready for Phase 5 execution

## Example Execution

```bash
# Run ticket review
/epic-review-tickets EPIC-CCN-027

# Expected output:
[TICKETS-LOADED] 3 tickets parsed
[KB-LOADED] 12 rules retrieved
[SCOPE-CHECK] 3/3 PASS
[COMPLEXITY-CHECK] 3/3 PASS
[SCOPE-CREEP-CHECK] 3/3 PASS
[INDEPENDENCE-CHECK] 2/3 PASS, 1 WARN
[TEST-CHECK] 3/3 PASS
[ENCODING-CHECK] 3/3 PASS
[REVIEW-COMPLETE]
[TICKETS-APPROVED-WARN] Ready for Phase 5 (1 warning)

# Review report saved: docs/brain/EPIC-CCN-027/04-5-ticket-review.md
```

## Benefits of Automation

1. **Eliminates Manual Gate**: No Director review required
2. **Consistent Validation**: Same rules applied every time
3. **Faster Execution**: No waiting for human review
4. **Audit Trail**: All validation logic documented
5. **Jane Street Compliance**: KB rules enforced automatically
6. **Fully Autonomous**: Enables true autonomous refactoring

## Related Documentation

- **Custom Mode**: `.bob/custom_modes.yaml` (v12-phase4-5-review)
- **Epic Run**: `.bob/commands/epic-run.md`
- **Jane Street KB**: `scripts/query_kb.py`
- **Phase 4**: `.bob/commands/epic-tickets.md`
- **Phase 5**: `.bob/commands/epic-validate.md`

---

**Command Status**: ✅ Ready for Integration  
**Next Step**: Update `/epic-run` to call `/epic-review-tickets` after Phase 4
