# Parallel Execution Workflow (V12.22)

## Overview
Execute 2 EPICs simultaneously using Bob IDE (orchestrator) + 2 Bob Shell workers (executors). Achieves 40-50% time savings through subgraph isolation and independent review cycles.

---

## Architecture

### Components

**Bob IDE (VS Code)**: Orchestrator
- **Role**: Coordination and monitoring
- **Responsibilities**:
  - Launch parallel workers
  - Monitor progress across both windows
  - Handle F5 gates sequentially
  - Detect conflicts
  - Coordinate PR merges
- **Does NOT**: Execute code directly, run commands, or modify files

**Bob Shell Window 1**: Worker A
- **Role**: Autonomous executor
- **Responsibilities**:
  - Execute `epic-run` or `epic-tdd` for EPIC-X
  - Run all 9 independent review gates
  - Report status to orchestrator
  - Handle Jane Street compliance checks
- **Isolation**: Works in subgraph S1 only

**Bob Shell Window 2**: Worker B
- **Role**: Autonomous executor
- **Responsibilities**:
  - Execute `epic-run` or `epic-tdd` for EPIC-Y
  - Run all 9 independent review gates
  - Report status to orchestrator
  - Handle Jane Street compliance checks
- **Isolation**: Works in subgraph S2 only

---

## Subgraph Isolation Matrix

From `docs/architecture.md`, the V12 codebase is divided into 8 subgraphs:

| Subgraph | Primary File | Methods | Safe Parallel With |
|----------|--------------|---------|-------------------|
| **S1: SIMA Core** | `V12_002.cs` | SIMA dispatch, state machine | S2, S3, S4, S5, S6, S7, S8 |
| **S2: Execution Engine** | `V12_002.cs` | Order placement, fills | S1, S3, S4, S5, S6, S7, S8 |
| **S3: UI & Photon IO** | `V12_002.cs` | OnKeyDown, UI handlers | S1, S2, S4, S5, S6, S7, S8 |
| **S4: ATM Logic** | `V12_002.Atm.cs` | ATM calculations | S1, S2, S3, S5, S6, S7, S8 |
| **S5: Drawing Helpers** | `V12_002.DrawingHelpers.cs` | Chart rendering | S1, S2, S3, S4, S6, S7, S8 |
| **S6: Utilities** | `V12_002.Utilities.cs` | Helper functions | S1, S2, S3, S4, S5, S7, S8 |
| **S7: State Management** | `V12_002.StateManagement.cs` | State tracking | S1, S2, S3, S4, S5, S6, S8 |
| **S8: Configuration** | `V12_002.Configuration.cs` | Config loading | S1, S2, S3, S4, S5, S6, S7 |

### Conflict Rules

**SAFE** ✅:
- EPIC-8 (S1) + EPIC-9 (S3) → Different subgraphs, no file overlap
- EPIC-10 (S4) + EPIC-11 (S5) → Different subgraphs, no file overlap

**UNSAFE** ❌:
- EPIC-8 (S1) + EPIC-12 (S1) → Same subgraph, file conflict
- EPIC-9 (S3) + EPIC-13 (S3) → Same subgraph, file conflict

**VERIFICATION REQUIRED** ⚠️:
- EPIC-8 (S1) + EPIC-9 (S2) → Different subgraphs, but both touch `V12_002.cs`
  - Check: Do they modify different methods? (SAFE)
  - Check: Do they modify the same method? (UNSAFE)

---

## Pre-Flight Checklist

Before launching parallel execution, verify:

### 1. Subgraph Isolation
```
Bob IDE (Orchestrator):
> "Verify subgraph isolation for EPIC-8 and EPIC-9"

Check:
- EPIC-8 targets which subgraph? (e.g., S1)
- EPIC-9 targets which subgraph? (e.g., S3)
- Are they in the isolation matrix as safe? (YES/NO)
```

### 2. File Overlap Detection
```
Bob IDE (Orchestrator):
> "Check file overlap between EPIC-8 and EPIC-9"

Use jCodemunch:
- get_file_outline for EPIC-8 target files
- get_file_outline for EPIC-9 target files
- Compare: Any shared files? (YES/NO)
```

### 3. Dependency Check
```
Bob IDE (Orchestrator):
> "Check if EPIC-9 depends on EPIC-8"

Read:
- docs/brain/epic-8/00-scope.md
- docs/brain/epic-9/00-scope.md
- Does EPIC-9 reference EPIC-8 changes? (YES/NO)
```

### 4. Resource Availability
```
Check:
- 2 Bob Shell windows available? (YES/NO)
- Sufficient RAM for 2 parallel sessions? (YES/NO)
- Director available for F5 gates? (YES/NO)
```

**If ALL checks pass**: Proceed to execution
**If ANY check fails**: Execute sequentially

---

## Execution Protocol

### Phase 1: Setup (Bob IDE)

**Step 1: Create Branches**
```
Bob IDE delegates to Advanced mode:

Window 1 branch:
```
git checkout main
git pull origin main
git checkout -b feature/src-epic-8-s1-extract-sima
```

Window 2 branch:
```
git checkout main
git pull origin main
git checkout -b feature/src-epic-9-s3-extract-ui
```
```

**Step 2: Launch Workers**
```
Bob Shell Window 1:
$ epic-run epic-8 "OnKeyDown + ProcessIpc extraction"

Bob Shell Window 2:
$ epic-run epic-9 "AttachPanelHandlers + OnSyncAllClick extraction"
```

### Phase 2: Autonomous Execution (Workers)

Both workers execute independently with 9 review gates each:

**Worker A (Window 1) - EPIC-8**:
1. Phase 0: Jane Street Baseline Audit
2. Phase 1: Intake
3. Phase 2: Plan
4. Phase 2.3: Sentinel Audit
5. Phase 2.5: Architectural Review
6. Phase 3: Validate
7. Phase 4: Tickets
8. Phase 4.5: Ticket Quality Review
9. Execution Pipeline (per ticket):
   - Step B.5: Implementation Review
   - Step C: Verification + Jane Street Audit
   - Step D: F5 Gate (Director)
10. Phase 6.5: Final Epic Validation

**Worker B (Window 2) - EPIC-9**:
(Same 9 gates, independent execution)

### Phase 3: Orchestrator Monitoring (Bob IDE)

**Status Tracking**:
```
Bob IDE displays:

[PARALLEL-EXECUTION] Status
============================================================
Window 1 (EPIC-8):
  Current Phase : Phase 2.5 (Architectural Review)
  Progress      : 3/7 tickets
  Status        : RUNNING
  Last Update   : 2 minutes ago

Window 2 (EPIC-9):
  Current Phase : Phase 4 (Tickets)
  Progress      : 0/5 tickets
  Status        : RUNNING
  Last Update   : 1 minute ago

Conflicts Detected: NONE
Next F5 Gate      : Window 1, ticket-03 (ETA 5 minutes)
============================================================
```

**Conflict Detection**:
```
Bob IDE monitors:
- File modifications in both windows
- Git status for merge conflicts
- Build failures
- Test failures

If conflict detected:
  1. HALT both workers
  2. Report to Director
  3. Resolve conflict manually
  4. Resume with rebased branches
```

### Phase 4: F5 Gates (Sequential)

**Rule**: F5 gates are handled sequentially, not in parallel.

**Scenario 1: Window 1 reaches F5 first**
```
Window 1:
[F5-GATE] EPIC-8 ticket-01 -- All automated gates PASSED
ACTION REQUIRED: Press F5 in NinjaTrader IDE.

Director:
> Press F5
> Type: F5 done [BUILD_TAG_001]

Window 1 continues to next ticket.
Window 2 continues independently.
```

**Scenario 2: Both windows reach F5 simultaneously**
```
Bob IDE (Orchestrator):
> "Both windows at F5 gate. Processing Window 1 first."

Window 1:
[F5-GATE] EPIC-8 ticket-02
Director: F5 done [BUILD_TAG_002]

Window 2:
[F5-GATE] EPIC-9 ticket-01
Director: F5 done [BUILD_TAG_003]
```

**Rationale**: Sequential F5 prevents confusion and ensures clean BUILD_TAG tracking.

### Phase 5: PR Submission (Workers)

**Worker A completes first**:
```
Window 1:
- All tickets complete
- Creates PR #8
- Runs /pr-loop to 100/100 PHS
- Reports: [EPIC-COMPLETE] EPIC-8

Bob IDE:
> "EPIC-8 complete. PR #8 ready for merge."
```

**Worker B completes second**:
```
Window 2:
- All tickets complete
- Creates PR #9
- Runs /pr-loop to 100/100 PHS
- Reports: [EPIC-COMPLETE] EPIC-9

Bob IDE:
> "EPIC-9 complete. PR #9 ready for merge."
```

### Phase 6: PR Coordination (Bob IDE)

**Merge Order Decision**:

**Case 1: No Dependencies**
```
Bob IDE:
> "EPIC-8 and EPIC-9 are independent. Merge in completion order."

Merge sequence:
1. Merge PR #8 (EPIC-8) - completed first
2. Merge PR #9 (EPIC-9) - completed second
```

**Case 2: EPIC-9 depends on EPIC-8**
```
Bob IDE:
> "EPIC-9 depends on EPIC-8. Merge EPIC-8 first, then rebase EPIC-9."

Merge sequence:
1. Merge PR #8 (EPIC-8)
2. Rebase PR #9 on updated main
3. Re-run /pr-loop for PR #9
4. Merge PR #9 (EPIC-9)
```

**Case 3: Merge Conflict Detected**
```
Bob IDE:
> "Merge conflict detected between PR #8 and PR #9. Manual resolution required."

Resolution:
1. Merge PR #8 (EPIC-8)
2. Checkout EPIC-9 branch
3. git rebase main (conflict appears)
4. Resolve conflict manually
5. git rebase --continue
6. git push --force-with-lease
7. Re-run /pr-loop for PR #9
8. Merge PR #9 (EPIC-9)
```

---

## Performance Metrics

### Time Savings

**Sequential Execution**:
```
EPIC-8: 2 hours (planning + execution + review)
EPIC-9: 2 hours (planning + execution + review)
Total: 4 hours
```

**Parallel Execution**:
```
EPIC-8: 2 hours (Window 1)
EPIC-9: 2 hours (Window 2, simultaneous)
Overhead: 15 minutes (coordination + F5 sequencing)
Total: 2 hours 15 minutes

Time Savings: 1 hour 45 minutes (44% reduction)
```

### Token Efficiency

**Sequential**:
```
EPIC-8: 50,000 tokens (9 review gates)
EPIC-9: 50,000 tokens (9 review gates)
Total: 100,000 tokens
```

**Parallel**:
```
EPIC-8: 50,000 tokens (Window 1)
EPIC-9: 50,000 tokens (Window 2)
Orchestration: 5,000 tokens (monitoring + coordination)
Total: 105,000 tokens

Token Overhead: 5% (acceptable for 44% time savings)
```

### Cognitive Load

**Director Involvement**:
- Sequential: 4-6 F5 gates (2-3 per epic)
- Parallel: 4-6 F5 gates (same total, but faster)
- Overhead: Minimal (orchestrator handles coordination)

**Recommendation**: Parallel execution is optimal for 2 EPICs. Beyond 2, cognitive load increases exponentially.

---

## Error Handling

### Scenario 1: Worker Failure

**Problem**: Window 1 fails at Phase 2.5 (Architectural Review)

**Response**:
```
Bob IDE:
> "Window 1 failed at Phase 2.5. Halting Window 1, continuing Window 2."

Actions:
1. HALT Window 1
2. Report failure to Director
3. Continue Window 2 independently
4. Fix Window 1 issue
5. Restart Window 1 from Phase 2.5
```

### Scenario 2: Conflict Detection

**Problem**: Both windows modify the same file unexpectedly

**Response**:
```
Bob IDE:
> "Conflict detected: Both windows modified V12_002.cs. Halting both workers."

Actions:
1. HALT both workers
2. Save current state
3. Report to Director
4. Manual conflict resolution required
5. Resume with corrected isolation
```

### Scenario 3: F5 Failure

**Problem**: F5 test fails for EPIC-8 ticket-02

**Response**:
```
Window 1:
> "F5 test failed. BUILD_TAG banner not visible."

Bob IDE:
> "Window 1 F5 failure. Halting Window 1, continuing Window 2."

Actions:
1. HALT Window 1
2. Debug F5 failure
3. Fix issue
4. Re-run ticket-02
5. Continue Window 2 independently
```

### Scenario 4: Dependency Violation

**Problem**: EPIC-9 unexpectedly depends on EPIC-8 changes

**Response**:
```
Bob IDE:
> "Dependency violation detected. EPIC-9 references EPIC-8 changes."

Actions:
1. HALT Window 2
2. Wait for Window 1 to complete
3. Merge PR #8
4. Rebase Window 2 on updated main
5. Resume Window 2
```

---

## Best Practices

### 1. Start Small
- First parallel run: 2 simple EPICs (e.g., EPIC-15 + EPIC-16 test coverage)
- Build confidence before tackling complex EPICs

### 2. Verify Isolation
- Always run pre-flight checklist
- Use jCodemunch to verify file overlap
- Document subgraph assignments in EXECUTION_GUIDE.md

### 3. Monitor Actively
- Bob IDE should display real-time status
- Check for conflicts every 15 minutes
- Be ready to halt if issues arise

### 4. Sequential F5
- Never attempt parallel F5 gates
- Process F5 gates in completion order
- Clear BUILD_TAG tracking prevents confusion

### 5. Document Dependencies
- Mark dependencies in epic scope documents
- Update EXECUTION_GUIDE.md with dependency order
- Merge dependent EPICs in correct sequence

### 6. Limit Parallelism
- Maximum 2 EPICs in parallel
- 3+ EPICs = cognitive overload
- Complex EPICs = sequential execution

---

## Troubleshooting

### Issue: "Subgraph conflict detected"
**Cause**: Both EPICs target the same subgraph
**Fix**: Execute sequentially or reassign one EPIC to different subgraph

### Issue: "File overlap detected"
**Cause**: Both EPICs modify the same file
**Fix**: Execute sequentially or split EPICs to avoid overlap

### Issue: "F5 gate timeout"
**Cause**: Director unavailable for F5 verification
**Fix**: Pause workers, resume when Director available

### Issue: "Merge conflict on PR submission"
**Cause**: Both EPICs modified overlapping code
**Fix**: Merge first PR, rebase second PR, resolve conflicts

### Issue: "Worker unresponsive"
**Cause**: Bob Shell crashed or hung
**Fix**: Kill worker process, restart from last checkpoint

---

## Future Enhancements

### Automated Conflict Detection
- Real-time file monitoring
- Automatic HALT on overlap
- Predictive conflict analysis

### Orchestrator Dashboard
- Visual progress tracking
- Real-time conflict alerts
- Performance metrics

### 3-Way Parallelism
- Support for 3 simultaneous EPICs
- Advanced cognitive load management
- Automated dependency resolution

### CI/CD Integration
- Parallel execution in GitHub Actions
- Automated PR coordination
- Zero-touch merge workflow

---

## Summary

**Parallel Execution Workflow** enables 40-50% time savings for independent EPICs through:
- ✅ Subgraph isolation (8 independent subsystems)
- ✅ Autonomous workers (9 review gates each)
- ✅ Sequential F5 gates (prevents confusion)
- ✅ Intelligent PR coordination (dependency-aware merging)
- ✅ Real-time conflict detection (Bob IDE monitoring)

**Use when**: 2 independent EPICs in different subgraphs
**Avoid when**: Dependent EPICs, same subgraph, or 3+ EPICs

**Effective Date:** 2026-05-31 (V12.22)