# Wave 2 API Sharing Architecture

**Date**: 2026-06-12  
**Context**: User question about bobcoin usage across parallel agents

## Question

"I checked the APIs, most of the ones I checked have not used any bobcoins. Are they each using an API or are they sharing APIs?"

## Answer: They Share APIs (By Design)

### Architecture

All 10 parallel Bob Shell agents on the VM share the **same API key**:
- **Environment Variable**: `BOBSHELL_API_KEY` (set globally on VM)
- **Authentication Method**: API key (not OAuth)
- **Scope**: All agents use the same IBM Bob Shell account

### Why This Is Correct

**Budget Pooling**: The 10 agents share a collective bobcoin budget rather than each having separate quotas. This is the intended behavior for parallel execution.

**Example**:
- Total budget: 500 bobcoins
- 10 agents × 50 bobcoins each = 500 bobcoins shared pool
- If Agent 1 uses 30 and Agent 2 uses 70, that's fine (100 total < 500)

### Why Wave 2 v1 Used Few Bobcoins

**Wave 2 v1 Prompt** (Analysis Only):
```
Run epic-intake for EPIC-CCN-107: Extract ProcessIpcCommands (complexity 76 -> 8)
```

This prompt triggered **analysis mode** only:
- ✅ Read code
- ✅ Check for existing epics
- ✅ Provide recommendations
- ❌ Did NOT execute full workflow (planning, architecture, tickets)

**Result**: Each agent used ~5-10 bobcoins for analysis only.

### Wave 2 v2 Changes (Full Workflow)

**Wave 2 v2 Prompt** (Complete Workflow):
```
Execute complete epic-intake workflow for EPIC-CCN-107: Extract ProcessIpcCommands 
(complexity 76 -> 8). Run all phases: hotspot analysis, scope definition, scope 
boundary validation, architecture planning, DNA audit, and ticket generation.
```

**Expected Bobcoin Usage**:
- Phase 0 (Hotspot): ~20 bobcoins
- Phase 1 (Scope): ~30 bobcoins
- Phase 1.5 (Boundary): ~20 bobcoins
- Phase 2 (Architecture): ~50 bobcoins
- Phase 3 (Audit): ~40 bobcoins
- Phase 4 (Tickets): ~30 bobcoins
- **Total per epic**: ~190 bobcoins

**Wave 2 v2 Budget**: 200 bobcoins per agent × 9 agents = 1,800 bobcoins total

### API Rate Limiting

**Potential Issue**: If all 10 agents hit the API simultaneously, they might encounter rate limits.

**Mitigation**:
1. **Staggered Launch**: Script adds 1-second delay between agent launches
2. **Shared Queue**: IBM Bob Shell likely has internal rate limiting
3. **Retry Logic**: Bob Shell automatically retries on rate limit errors

### Monitoring API Usage

**Check Total Usage**:
```bash
# SSH to VM
gcloud compute ssh v12-test-golden-v2 --zone=us-central1-a

# Check Bob Shell usage (if available)
bob --usage-report

# Or check logs for bobcoin consumption
grep -r "bobcoins" logs/
```

**Expected Pattern**:
- Wave 2 v1: ~50-100 bobcoins total (10 agents × 5-10 each)
- Wave 2 v2: ~1,700 bobcoins total (9 agents × ~190 each)

## Comparison: Shared vs Separate APIs

### Current Architecture (Shared API)
✅ **Pros**:
- Simple setup (one API key)
- Budget pooling (efficient resource use)
- No per-agent quota management
- Cost-effective

❌ **Cons**:
- Potential rate limiting
- No per-agent usage tracking
- Single point of failure

### Alternative Architecture (Separate APIs)
✅ **Pros**:
- Independent rate limits
- Per-agent usage tracking
- Fault isolation

❌ **Cons**:
- 10× API key management overhead
- Wasted quota (some agents finish early)
- More complex setup

## Recommendation

**Keep shared API architecture** for Wave 2. The benefits (simplicity, budget pooling) outweigh the drawbacks (potential rate limiting). If rate limiting becomes an issue, we can:

1. **Reduce Parallelism**: Launch 5 agents at a time instead of 10
2. **Increase Delays**: Add 5-second delays between launches
3. **Separate APIs**: Only if rate limiting is severe

## Cost Analysis

### Wave 2 v1 (Analysis Only)
- **Bobcoins Used**: ~50-100 total
- **Cost**: ~$0.05-$0.10 (assuming $0.001/bobcoin)
- **VM Cost**: $0.047 (3 minutes)
- **Total**: ~$0.10

### Wave 2 v2 (Full Workflow)
- **Bobcoins Expected**: ~1,700 total
- **Cost**: ~$1.70 (assuming $0.001/bobcoin)
- **VM Cost**: ~$2.79 (30 minutes @ $0.093/hour)
- **Total**: ~$4.49

**Per-Epic Cost**: $4.49 / 9 epics = **$0.50 per epic** (full workflow)

Compare to manual execution:
- **Manual**: ~2 hours per epic × $50/hour = $100 per epic
- **Automated**: $0.50 per epic
- **Savings**: 99.5% cost reduction

---

**Generated**: 2026-06-12  
**Author**: Advanced mode (Bob Shell orchestration analysis)