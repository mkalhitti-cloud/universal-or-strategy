# Option 3: Sub-Orchestrator Setup Guide

**Version**: 1.0  
**Date**: 2026-06-10  
**Status**: Design Complete, Implementation Pending

## Overview

Option 3 enables Bob to spawn 4 independent sub-orchestrators that autonomously manage their assigned workers. This provides delegation and fault isolation while maintaining the flexibility to execute as Option 1 (direct orchestration) when needed.

## What's Needed for Option 3

### 1. Sub-Orchestrator Agent Definition

Create 4 specialized orchestrator agents (one per worker):

**File**: `sub_orchestrator_worker_1.yaml`

```yaml
name: v12_worker_1_orchestrator
description: Autonomous orchestrator for Worker 1 epic execution
model: gpt-4o

tools:
  - v12_worker_1.claim_epic
  - v12_worker_1.execute_epic
  - v12_worker_1.release_epic
  - v12_worker_1.get_worker_status
  - v12_worker_1.get_next_pending_epic

instructions: |
  You are the autonomous orchestrator for Worker 1.
  
  Your job:
  1. Get next pending epic
  2. Claim epic for Worker 1
  3. Execute epic (all 6 phases)
  4. Release lock
  5. Repeat until no pending epics
  
  Rules:
  - Always release lock after completion/failure
  - Report progress every 5 minutes
  - Handle errors gracefully
  - Stop if no pending epics
```

**Repeat for Workers 2, 3, 4** (same structure, different worker tools)

### 2. Bob's Spawning Capability

Bob needs a `new_task` tool call to spawn sub-orchestrators:

```python
# Bob spawns 4 sub-orchestrators
new_task(
    mode="advanced",
    message="You are Worker 1 Orchestrator. Execute all pending epics for Worker 1 autonomously.",
    todos=[
        "[ ] Get next pending epic",
        "[ ] Claim epic for Worker 1",
        "[ ] Execute epic phases",
        "[ ] Release lock",
        "[ ] Repeat until no pending epics"
    ]
)
```

### 3. Inter-Orchestrator Communication

Sub-orchestrators need to coordinate via shared state:

**File**: `docs/brain/orchestrator_state.json`

```json
{
  "orchestrator_1": {
    "worker_id": "worker-1",
    "status": "active",
    "current_epic": "EPIC-CCN-21",
    "phase": "epic-plan",
    "last_heartbeat": "2026-06-10T19:00:00Z"
  },
  "orchestrator_2": {
    "worker_id": "worker-2",
    "status": "active",
    "current_epic": "EPIC-CCN-22",
    "phase": "epic-scan",
    "last_heartbeat": "2026-06-10T19:00:00Z"
  },
  "orchestrator_3": {
    "worker_id": "worker-3",
    "status": "idle",
    "current_epic": null,
    "phase": null,
    "last_heartbeat": "2026-06-10T19:00:00Z"
  },
  "orchestrator_4": {
    "worker_id": "worker-4",
    "status": "active",
    "current_epic": "EPIC-CCN-24",
    "phase": "epic-validate",
    "last_heartbeat": "2026-06-10T19:00:00Z"
  }
}
```

### 4. Heartbeat Monitoring

Bob monitors sub-orchestrators via heartbeat:

```python
# Bob checks orchestrator health every 5 minutes
def monitor_orchestrators():
    state = load_orchestrator_state()
    
    for orch_id, orch_data in state.items():
        last_heartbeat = parse_timestamp(orch_data['last_heartbeat'])
        
        if (now() - last_heartbeat) > timedelta(minutes=10):
            # Orchestrator crashed or stuck
            print(f"⚠️ {orch_id} not responding (last heartbeat: {last_heartbeat})")
            
            # Release orphaned epic lock
            if orch_data['current_epic']:
                release_epic(orch_data['current_epic'])
            
            # Respawn orchestrator
            respawn_orchestrator(orch_id)
```

### 5. Graceful Shutdown

Sub-orchestrators must handle shutdown signals:

```python
# Sub-orchestrator shutdown handler
def shutdown_handler():
    # Release current epic lock
    if current_epic:
        release_epic(current_epic)
    
    # Update state to "stopped"
    update_orchestrator_state(orchestrator_id, status="stopped")
    
    # Exit gracefully
    sys.exit(0)
```

## Implementation Checklist

### Phase 1: Infrastructure (30 minutes)
- [ ] Create `sub_orchestrator_worker_1.yaml`
- [ ] Create `sub_orchestrator_worker_2.yaml`
- [ ] Create `sub_orchestrator_worker_3.yaml`
- [ ] Create `sub_orchestrator_worker_4.yaml`
- [ ] Create `docs/brain/orchestrator_state.json`
- [ ] Create `scripts/orchestrator_monitor.py` (heartbeat checker)

### Phase 2: Bob Integration (15 minutes)
- [ ] Add `spawn_orchestrator()` function to Bob
- [ ] Add `monitor_orchestrators()` function to Bob
- [ ] Add `respawn_orchestrator()` function to Bob
- [ ] Test spawning single orchestrator

### Phase 3: Testing (30 minutes)
- [ ] Test spawning 4 orchestrators
- [ ] Test heartbeat monitoring
- [ ] Test orchestrator crash recovery
- [ ] Test graceful shutdown
- [ ] Test epic completion and reassignment

### Phase 4: Production Deployment (15 minutes)
- [ ] Deploy sub-orchestrator agents to Watsonx Orchestrate
- [ ] Configure Bob to spawn orchestrators on demand
- [ ] Set up monitoring dashboard
- [ ] Document operational procedures

## Execution Modes

### Mode 1: Direct Orchestration (Option 1)
**When to use**: Testing, debugging, learning workflow

```
Bob (this session):
  ├─ Call worker-1.claim_epic("EPIC-CCN-21")
  ├─ Call worker-1.execute_epic("EPIC-CCN-21")
  ├─ Call worker-1.release_epic("EPIC-CCN-21")
  └─ Repeat for all epics
```

**Command**: "Execute next 4 epics directly"

### Mode 2: Delegated Orchestration (Option 3)
**When to use**: Production, long-running execution, fault tolerance

```
Bob (this session):
  ├─ Spawn Orchestrator 1 → manages Worker 1 autonomously
  ├─ Spawn Orchestrator 2 → manages Worker 2 autonomously
  ├─ Spawn Orchestrator 3 → manages Worker 3 autonomously
  ├─ Spawn Orchestrator 4 → manages Worker 4 autonomously
  └─ Monitor orchestrators (heartbeat check every 5 min)
```

**Command**: "Spawn 4 orchestrators and execute all 173 epics"

## Benefits of Having Both

### Option 1 (Direct) Benefits
- ✅ Full visibility (see every step)
- ✅ Easy debugging
- ✅ Lower cost
- ✅ Simple coordination

### Option 3 (Delegated) Benefits
- ✅ Fault isolation (one fails, others continue)
- ✅ Parallel decision-making
- ✅ Lower cognitive load for Bob
- ✅ Autonomous execution

### Flexibility
- Start with Option 1 to perfect workflow
- Switch to Option 3 for production scale
- Fall back to Option 1 for debugging
- Mix modes (e.g., 3 delegated + 1 direct)

## Cost Comparison

### Option 1 (Direct)
- **Orchestration**: Bob monitors every phase
- **Cost**: ~$50-100 (Bob's API calls)
- **Time**: Bob must stay active

### Option 3 (Delegated)
- **Orchestration**: 4 sub-orchestrators monitor phases
- **Cost**: ~$100-150 (4x orchestration overhead)
- **Time**: Bob can close session after spawning

## Migration Path

### Week 1: Option 1 (Testing)
- Execute 10-20 epics directly
- Perfect workflow
- Identify edge cases
- Document lessons learned

### Week 2: Option 3 (Production)
- Implement sub-orchestrator infrastructure
- Test with 4 epics (one per orchestrator)
- Validate fault tolerance
- Deploy for remaining 153 epics

## Success Criteria

### Option 1 Success
- ✅ 10 epics completed successfully
- ✅ Zero duplicate work
- ✅ All locks released
- ✅ Average time <30 minutes per epic

### Option 3 Success
- ✅ 4 orchestrators running autonomously
- ✅ Heartbeat monitoring working
- ✅ Crash recovery tested
- ✅ 153 epics completed without intervention

## Next Steps

1. **Now**: Start with Option 1 (direct orchestration)
2. **After 10 epics**: Evaluate workflow, identify improvements
3. **Week 2**: Implement Option 3 infrastructure
4. **Production**: Switch to Option 3 for remaining epics

---

**Status**: Design complete, ready for Option 1 execution  
**Recommendation**: Start with Option 1 now, build Option 3 after workflow is perfected