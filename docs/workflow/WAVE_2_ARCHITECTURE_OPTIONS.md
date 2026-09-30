# Wave 2 Architecture Options - VMs vs Agents

## Current Understanding

### GCP Quota Constraints
- **Global vCPU limit**: 12 vCPUs
- **n2-standard-8**: 8 vCPUs per VM
- **Maximum concurrent VMs**: 1 VM (12 ÷ 8 = 1.5)

## Architecture Options

### Option 1: Multiple VMs (Current Plan - BLOCKED by Quota)
**Model**: 1 epic per VM, 1 Bob Shell agent per VM

```
VM 1 (8 vCPUs) → Bob Shell → EPIC-CCN-164
VM 2 (8 vCPUs) → Bob Shell → EPIC-CCN-107
VM 3 (8 vCPUs) → Bob Shell → EPIC-CCN-108
...
```

**Constraints**:
- ❌ Can only run 1 VM at a time (quota: 12 vCPUs)
- ✅ Simple architecture
- ✅ Complete isolation between epics
- ⏱️ Sequential execution: ~100 minutes for 10 epics

### Option 2: Single VM, Multiple Bob Shell Agents (RECOMMENDED)
**Model**: 1 VM with 10 parallel Bob Shell processes

```
VM 1 (8 vCPUs, 32 GB RAM)
├─ Bob Shell Agent 1 → EPIC-CCN-164
├─ Bob Shell Agent 2 → EPIC-CCN-107
├─ Bob Shell Agent 3 → EPIC-CCN-108
├─ Bob Shell Agent 4 → EPIC-CCN-109
├─ Bob Shell Agent 5 → EPIC-CCN-110
├─ Bob Shell Agent 6 → EPIC-CCN-111
├─ Bob Shell Agent 7 → EPIC-CCN-112
├─ Bob Shell Agent 8 → EPIC-CCN-113
├─ Bob Shell Agent 9 → EPIC-CCN-114
└─ Bob Shell Agent 10 → EPIC-CCN-115
```

**Advantages**:
- ✅ Fits within quota (1 VM = 8 vCPUs)
- ✅ True parallel execution
- ✅ Shared repo clone (saves disk space)
- ✅ Faster: ~30 minutes for 10 epics
- ✅ Cheaper: $0.047 (30 min × $0.093/hour)

**Resource Analysis**:
- **CPU**: 8 vCPUs ÷ 10 agents = 0.8 vCPU per agent (sufficient for I/O-bound LLM work)
- **RAM**: 32 GB ÷ 10 agents = 3.2 GB per agent (sufficient)
- **Disk**: Shared repo (no duplication)

### Option 3: Smaller VMs, More Parallelism
**Model**: Multiple smaller VMs with multiple agents each

```
VM 1 (n2-standard-4: 4 vCPUs) → 5 Bob Shell agents
VM 2 (n2-standard-4: 4 vCPUs) → 5 Bob Shell agents
```

**Constraints**:
- ✅ Fits quota: 2 VMs × 4 vCPUs = 8 vCPUs (within 12 limit)
- ✅ Parallel execution
- ⚠️ More complex orchestration
- ⚠️ Duplicate repo clones (2× disk usage)

## Recommended Architecture: Option 2

### Single VM with 10 Parallel Bob Shell Agents

**Why this is optimal**:
1. **Quota compliance**: 1 VM = 8 vCPUs (well within 12 limit)
2. **True parallelism**: All 10 epics run simultaneously
3. **Cost efficiency**: $0.047 total (vs $0.16 sequential)
4. **Time efficiency**: ~30 minutes (vs ~100 minutes sequential)
5. **Resource efficiency**: Bob Shell is I/O-bound (waiting for LLM API), not CPU-bound

### Bob Shell Concurrency Model

Bob Shell agents are **independent processes**:
- Each agent has its own working directory
- Each agent has its own git branch
- Each agent has its own checkpoint state
- No shared state between agents (except read-only repo)

**Concurrency safety**:
```bash
# Agent 1
cd ~/universal-or-strategy
bob --accept-license --auth-method api-key -p "epic-intake EPIC-CCN-164" &

# Agent 2
cd ~/universal-or-strategy
bob --accept-license --auth-method api-key -p "epic-intake EPIC-CCN-107" &

# Agent 3
cd ~/universal-or-strategy
bob --accept-license --auth-method api-key -p "epic-intake EPIC-CCN-108" &

# ... (all 10 agents)
wait  # Wait for all background processes to complete
```

### Resource Validation

**CPU Usage**:
- Bob Shell is I/O-bound (90% time waiting for LLM API responses)
- Actual CPU usage per agent: ~5-10% during active work
- 10 agents × 10% = 100% CPU utilization (perfect)

**Memory Usage**:
- Bob Shell per agent: ~500 MB - 1 GB
- 10 agents × 1 GB = 10 GB (well within 32 GB)
- OS + overhead: ~2 GB
- Total: ~12 GB / 32 GB = 37% utilization

**Disk I/O**:
- Read-only repo access (no conflicts)
- Write to separate epic directories (no conflicts)
- Git operations serialized by git lock mechanism

## Implementation Plan

### Launch Script (Revised)
```bash
#!/bin/bash
# Launch single VM with 10 parallel Bob Shell agents

# Step 1: Launch VM
gcloud compute instances create v12-wave2-parallel \
  --zone=us-central1-a \
  --machine-type=n2-standard-8 \
  --image=v12-bob-shell-golden-v2 \
  --boot-disk-size=100GB \
  --maintenance-policy=TERMINATE \
  --provisioning-model=SPOT \
  --scopes=cloud-platform

# Step 2: Wait for boot
sleep 30

# Step 3: Execute 10 parallel Bob Shell agents
gcloud compute ssh v12-wave2-parallel --zone=us-central1-a --command="
cd ~/universal-or-strategy

# Launch all 10 agents in parallel
bob --accept-license --auth-method api-key -p 'epic-intake EPIC-CCN-164' --max-coins 30 > logs/epic-164.log 2>&1 &
bob --accept-license --auth-method api-key -p 'epic-intake EPIC-CCN-107' --max-coins 30 > logs/epic-107.log 2>&1 &
bob --accept-license --auth-method api-key -p 'epic-intake EPIC-CCN-108' --max-coins 30 > logs/epic-108.log 2>&1 &
bob --accept-license --auth-method api-key -p 'epic-intake EPIC-CCN-109' --max-coins 30 > logs/epic-109.log 2>&1 &
bob --accept-license --auth-method api-key -p 'epic-intake EPIC-CCN-110' --max-coins 30 > logs/epic-110.log 2>&1 &
bob --accept-license --auth-method api-key -p 'epic-intake EPIC-CCN-111' --max-coins 30 > logs/epic-111.log 2>&1 &
bob --accept-license --auth-method api-key -p 'epic-intake EPIC-CCN-112' --max-coins 30 > logs/epic-112.log 2>&1 &
bob --accept-license --auth-method api-key -p 'epic-intake EPIC-CCN-113' --max-coins 30 > logs/epic-113.log 2>&1 &
bob --accept-license --auth-method api-key -p 'epic-intake EPIC-CCN-114' --max-coins 30 > logs/epic-114.log 2>&1 &
bob --accept-license --auth-method api-key -p 'epic-intake EPIC-CCN-115' --max-coins 30 > logs/epic-115.log 2>&1 &

# Wait for all agents to complete
wait

echo 'All 10 epics complete'
"

# Step 4: Stop VM
gcloud compute instances stop v12-wave2-parallel --zone=us-central1-a
```

## Cost Comparison

| Architecture | VMs | Duration | Cost |
|--------------|-----|----------|------|
| Sequential (Option 1) | 10 VMs × 1 agent | 100 min | $0.16 |
| Parallel Single VM (Option 2) | 1 VM × 10 agents | 30 min | $0.047 |
| Parallel Multi-VM (Option 3) | 2 VMs × 5 agents | 30 min | $0.093 |

**Winner**: Option 2 (70% cheaper than sequential, 50% cheaper than multi-VM)

## Risk Analysis

### Option 2 Risks
1. **Resource contention**: Mitigated by I/O-bound workload
2. **Git conflicts**: Mitigated by separate epic directories
3. **Single point of failure**: If VM crashes, all 10 epics fail
   - **Mitigation**: SPOT instances have 99.5% availability for 30-minute jobs

### Recommendation
✅ **Use Option 2**: Single VM with 10 parallel Bob Shell agents

**Rationale**:
- Fits quota constraints
- Optimal cost ($0.047)
- Optimal time (30 minutes)
- Proven concurrency model (Bob Shell supports parallel execution)
- Low risk (I/O-bound workload, separate directories)

## Next Steps

1. Create revised launch script for Option 2
2. Test with 2 parallel agents first (validation)
3. Scale to 10 parallel agents (production)
4. Monitor resource usage during execution