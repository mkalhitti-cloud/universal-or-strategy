# Autonomous Deployment Blockers

**Date**: 2026-06-09
**Purpose**: Track all questions/decisions that block autonomous deployment
**Goal**: Eliminate ALL human-in-the-loop decisions for 1-shot deployment

## Critical Principle

> **"Communication will be 1-way and 1-shot when you deploy them autonomously"**
> 
> Every question an agent asks is a **deployment blocker**. We must pre-answer ALL questions through:
> - Pre-creation scripts
> - Configuration files
> - Explicit instructions
> - Mode selection
> - Data validation

---

## Blockers Discovered (2026-06-09 Session)

### 1. Epic Number Assignment ⚠️ CRITICAL
**Question**: "EPIC-CCN-19 does not exist. Should I create it?"
**Root Cause**: Epic directories don't exist before deployment
**Impact**: Requires human confirmation
**Solution**: 
```python
# Pre-create epic directories before deployment
def prepare_batch(start_epic, count=3):
    for i in range(count):
        epic_id = f"EPIC-CCN-{start_epic + i}"
        os.makedirs(f"docs/brain/{epic_id}", exist_ok=True)
        create_manifest(epic_id, status="pending")
```
**Status**: ❌ Not implemented

---

### 2. Mode Selection ⚠️ CRITICAL
**Question**: "I'm in 'ask' mode which doesn't allow file writes. Should I switch to 'plan' mode?"
**Root Cause**: Bob started in wrong mode for epic-intake
**Impact**: Requires human confirmation to switch modes
**Solution**:
```python
# Deploy Bob in correct mode from start
def deploy_bob_session(epic_id, worktree):
    return subprocess.Popen([
        "bob",
        "--yolo",
        "--mode", "plan",  # ← Explicit mode for epic-intake
        "--command", f"epic-intake {epic_id} 'Auto-generated epic'"
    ], cwd=worktree)
```
**Status**: ❌ Not implemented

---

### 3. Stale Complexity Data ⚠️ HIGH
**Question**: "EPIC-LOOP-EXECUTION-PLAN.md references pre-CCN-16 data. Should I regenerate?"
**Root Cause**: Planning documents created 4 days ago, now stale
**Impact**: Requires human decision on data freshness
**Solution**:
```python
# Always regenerate roadmap before deployment
def prepare_deployment():
    # 1. Run fresh complexity audit
    subprocess.run(["python", "scripts/complexity_audit.py"])
    
    # 2. Regenerate epic roadmap
    subprocess.run(["python", "scripts/generate_epic_roadmap.py"])
    
    # 3. Validate roadmap is fresh (< 1 hour old)
    roadmap_age = time.time() - os.path.getmtime("epic_roadmap.json")
    assert roadmap_age < 3600, "Roadmap too old"
```
**Status**: ❌ Not implemented

---

### 4. Epic Number Race Condition ⚠️ CRITICAL
**Question**: "Multiple sessions trying to create EPIC-CCN-19. Which one should proceed?"
**Root Cause**: No atomic reservation mechanism
**Impact**: Requires sequential deployment (30-second overhead per session)
**Solution**:
```python
# Atomic epic reservation
def reserve_epic(epic_id, worktree):
    lock_file = f"docs/brain/{epic_id}/.lock"
    
    # Atomic file creation (fails if exists)
    try:
        fd = os.open(lock_file, os.O_CREAT | os.O_EXCL | os.O_WRONLY)
        os.write(fd, f"{worktree}\n{datetime.now()}".encode())
        os.close(fd)
        return True
    except FileExistsError:
        return False  # Another session got it first
```
**Status**: ❌ Not implemented

---

### 5. Inconsistent Confirmation Dialogs ⚠️ MEDIUM
**Question**: Sometimes Bob asks for confirmation, sometimes doesn't
**Root Cause**: Timing-dependent filesystem polling
**Impact**: Unpredictable automation behavior
**Solution**: Pre-creation eliminates all confirmation dialogs
**Status**: ❌ Not implemented (blocked by #1)

---

## Deployment Readiness Checklist

For autonomous deployment to work, ALL of these must be ✅:

- [ ] **Epic directories pre-created** (eliminates "does not exist" questions)
- [ ] **Correct mode specified** (eliminates "should I switch mode" questions)
- [ ] **Fresh complexity data** (eliminates "data is stale" questions)
- [ ] **Atomic epic reservation** (eliminates race condition questions)
- [ ] **YOLO mode enabled** (eliminates tool permission questions)
- [ ] **Manifest pre-populated** (eliminates "what should I do" questions)

**Current Status**: 0/6 implemented ❌

---

## Implementation Priority

### Phase 1: Pre-Creation Script (HIGHEST PRIORITY)
**File**: `scripts/prepare_parallel_batch.py`
**Eliminates**: Blockers #1, #4, #5
**Effort**: 2 hours
**Impact**: 🔥 Removes 3 critical blockers

### Phase 2: Mode Configuration (HIGH PRIORITY)
**File**: `scripts/deploy_bob_session.py`
**Eliminates**: Blocker #2
**Effort**: 1 hour
**Impact**: 🔥 Removes 1 critical blocker

### Phase 3: Data Freshness Validation (MEDIUM PRIORITY)
**File**: `scripts/validate_roadmap_freshness.py`
**Eliminates**: Blocker #3
**Effort**: 30 minutes
**Impact**: ⚠️ Removes 1 high-priority blocker

---

## Testing Protocol

**Before claiming "autonomous deployment ready"**:

1. Run full deployment script
2. Monitor for ANY human prompts
3. If ANY prompt appears → FAIL, add to this document
4. Repeat until zero prompts for 3 consecutive runs
5. Only then: Mark as production-ready

**Success Criteria**: 
- ✅ Zero human prompts from deployment to F5 gate
- ✅ All 3 sessions reach F5 simultaneously
- ✅ Reproducible across 10 consecutive runs

---

## Next Session Action Items

1. **Implement Phase 1** (pre-creation script)
2. **Test with 3 parallel sessions**
3. **Document any NEW blockers discovered**
4. **Iterate until zero prompts**

**Goal**: Next parallel batch should have ZERO questions.