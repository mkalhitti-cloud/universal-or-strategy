# Epic Locking Protocol - Preventing Duplicate Work

**Date**: 2026-06-10
**Issue**: Workers 2 & 4 both started EPIC-CCN-21 simultaneously, causing duplicate work
**Root Cause**: No real-time epic locking mechanism

---

## Problem Analysis

### What Happened
1. **Worker 2** started EPIC-CCN-21 first (completed in 33 minutes)
2. **Worker 4** started EPIC-CCN-21 independently (still in Phase 1.5)
3. **No coordination** - Workers couldn't see each other's work in real-time
4. **Result**: Duplicate work, wasted effort

### Root Causes

#### 1. No Atomic Epic Claiming
**Problem**: `epic_roadmap.json` is a static file, not a real-time database
- Workers read roadmap at different times
- No atomic "claim and lock" operation
- Changes not visible until git push/pull

#### 2. Worktree Isolation
**Problem**: Each worktree has independent working directory
- Worker 2's changes in `epic-cluster-2` not visible to Worker 4 in `epic-cluster-4`
- Manifest files (`docs/brain/EPIC-CCN-21/manifest.json`) are local to each worktree
- Git commits not pushed until epic completes

#### 3. No Central Coordination Service
**Problem**: No shared state between workers
- Each worker operates independently
- No pub/sub or message queue
- No real-time status updates

---

## Solution: Multi-Layer Locking Protocol

### Layer 1: Git-Based Locking (Immediate Fix)

**Mechanism**: Use git branches as locks

```bash
# Worker claims epic by creating lock branch
git checkout -b lock/EPIC-CCN-21/worker-2
git push origin lock/EPIC-CCN-21/worker-2

# Other workers check for lock before starting
git fetch origin
if git branch -r | grep "lock/EPIC-CCN-21"; then
  echo "Epic locked by another worker"
  exit 1
fi
```

**Pros**: 
- ✅ Atomic (git push is atomic)
- ✅ Visible across all worktrees
- ✅ No external dependencies

**Cons**:
- ❌ Requires network (git push/fetch)
- ❌ Branch pollution

### Layer 2: Shared Lock File (Better)

**Mechanism**: Use shared file in main workspace

```bash
# Worker claims epic
echo "worker-2:$(date -u +%Y-%m-%dT%H:%M:%SZ)" > .epic-locks/EPIC-CCN-21.lock
git add .epic-locks/EPIC-CCN-21.lock
git commit -m "lock: Claim EPIC-CCN-21 for worker-2"
git push origin gitbutler/workspace

# Other workers check lock
if [ -f .epic-locks/EPIC-CCN-21.lock ]; then
  owner=$(cat .epic-locks/EPIC-CCN-21.lock | cut -d: -f1)
  echo "Epic locked by $owner"
  exit 1
fi
```

**Pros**:
- ✅ Atomic (git commit + push)
- ✅ Visible across worktrees after pull
- ✅ Clean (no branch pollution)

**Cons**:
- ❌ Requires git pull before each epic
- ❌ Race condition if two workers push simultaneously

### Layer 3: Firebase Realtime Database (Best)

**Mechanism**: Use Firebase for real-time locking

```javascript
// Worker claims epic
const lockRef = db.ref(`epic-locks/EPIC-CCN-21`);
await lockRef.transaction((current) => {
  if (current === null) {
    return {
      worker: 'worker-2',
      timestamp: Date.now(),
      status: 'in_progress'
    };
  }
  return; // Abort if already locked
});

// Other workers listen for locks
lockRef.on('value', (snapshot) => {
  if (snapshot.exists()) {
    console.log(`Epic locked by ${snapshot.val().worker}`);
  }
});
```

**Pros**:
- ✅ Real-time (sub-second updates)
- ✅ Atomic (Firebase transactions)
- ✅ No git pollution
- ✅ Pub/sub for status updates

**Cons**:
- ❌ Requires Firebase setup
- ❌ External dependency

---

## Recommended Implementation: Layer 2 (Shared Lock File)

### Step 1: Create Lock Directory

```bash
mkdir -p .epic-locks
echo "*.lock" > .epic-locks/.gitignore
```

### Step 2: Update `scripts/validate_epic.py`

Add atomic locking functions:

```python
def claim_epic_atomic(epic_id: str, worker_id: str) -> bool:
    """Atomically claim an epic using git."""
    lock_file = f".epic-locks/{epic_id}.lock"
    
    # Pull latest locks
    subprocess.run(["git", "pull", "origin", "gitbutler/workspace"], check=True)
    
    # Check if already locked
    if os.path.exists(lock_file):
        with open(lock_file) as f:
            owner = f.read().split(":")[0]
        print(f"Epic {epic_id} already locked by {owner}")
        return False
    
    # Claim lock
    timestamp = datetime.utcnow().isoformat() + "Z"
    with open(lock_file, "w") as f:
        f.write(f"{worker_id}:{timestamp}")
    
    # Commit and push atomically
    subprocess.run(["git", "add", lock_file], check=True)
    subprocess.run(["git", "commit", "-m", f"lock: Claim {epic_id} for {worker_id}"], check=True)
    subprocess.run(["git", "push", "origin", "gitbutler/workspace"], check=True)
    
    return True

def release_epic_atomic(epic_id: str, worker_id: str):
    """Release epic lock."""
    lock_file = f".epic-locks/{epic_id}.lock"
    
    if os.path.exists(lock_file):
        os.remove(lock_file)
        subprocess.run(["git", "add", lock_file], check=True)
        subprocess.run(["git", "commit", "-m", f"unlock: Release {epic_id} by {worker_id}"], check=True)
        subprocess.run(["git", "push", "origin", "gitbutler/workspace"], check=True)
```

### Step 3: Update Worker Prompts

Add lock checking to all worker prompts:

```
Before starting any epic:
1. Pull latest: git pull origin gitbutler/workspace
2. Claim epic: python scripts/validate_epic.py --claim EPIC-CCN-X worker-N
3. If claim fails, find next pending epic
4. After completion: python scripts/validate_epic.py --release EPIC-CCN-X worker-N
```

---

## Immediate Action: Resolve Worker 2 & 4 Conflict

### Current Status
- **Worker 2**: EPIC-CCN-21 COMPLETE (production-ready, 33 minutes)
- **Worker 4**: EPIC-CCN-21 Phase 1.5 (waiting for Director)

### Resolution

**Worker 4 Prompt**:
```
ABORT EPIC-CCN-21. Worker 2 has already completed this epic (production-ready in 33 minutes). Your work is duplicate.

Mark your session as "aborted - duplicate work" and move to next pending epic (EPIC-CCN-22 or later).

Before starting next epic, pull latest changes and check for locks:
git pull origin gitbutler/workspace
python C:/WSGTA/universal-or-strategy/scripts/validate_epic.py --claim EPIC-CCN-22 worker-4
```

**Worker 2 Prompt**:
```
EPIC-CCN-21 complete. Excellent work (33 minutes, production-ready).

Move to next pending epic. Before starting, pull latest and claim:
git pull origin gitbutler/workspace
python C:/WSGTA/universal-or-strategy/scripts/validate_epic.py --claim EPIC-CCN-22 worker-2
```

---

## Prevention Checklist

- [ ] Create `.epic-locks/` directory
- [ ] Update `scripts/validate_epic.py` with atomic locking
- [ ] Update all worker prompts to include lock checking
- [ ] Test locking with 2 workers on same epic
- [ ] Document locking protocol in worker instructions

---

## Future: Firebase Real-Time Locking

For true real-time coordination, implement Firebase:
- Sub-second lock updates
- Pub/sub for epic status
- Worker heartbeat monitoring
- Automatic lock release on worker crash

**Reference**: `docs/workflow/FIREBASE_EPIC_LOCKING.md` (to be created)