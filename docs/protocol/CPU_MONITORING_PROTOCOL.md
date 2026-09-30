# CPU Monitoring Protocol

**Version**: 1.0
**Effective**: 2026-06-14
**Status**: MANDATORY
**Authority**: Wave 4 Phase 0 Completion (2026-06-14)

## Purpose

Track VM resource utilization during autonomous epic execution to:
1. Validate capacity planning assumptions
2. Detect performance bottlenecks early
3. Optimize concurrent agent limits
4. Prevent resource exhaustion
5. Guide VM scaling decisions

## When to Monitor

**MANDATORY - Post-Execution**: At the END of each phase execution (after all agents complete)

**MANDATORY - During Execution**: Real-time monitoring for phases with >10 concurrent agents

**Why Both?**
- **Post-execution**: Validates VM returned to idle state, no resource leaks
- **During execution**: Captures ACTUAL peak utilization (critical for capacity planning)

## Collection Commands

### Post-Execution Monitoring (After Phase Complete)

```bash
gcloud compute ssh v12-test-golden-v2 --zone=us-central1-a \
  --command="echo '=== VM Resource Usage ===' && \
    echo 'CPU Info:' && nproc && echo '' && \
    echo 'Load Average (1/5/15 min):' && uptime | grep -oE 'load average: [0-9., ]+' && echo '' && \
    echo 'Memory Usage:' && free -h | grep Mem && echo '' && \
    echo 'Disk Usage:' && df -h /home/malhitticrypto/universal-or-strategy | tail -1"
```

**Expected Output**:
```
=== VM Resource Usage ===
CPU Info:
8

Load Average (1/5/15 min):
load average: 0.00, 0.06, 0.10

Memory Usage:
Mem:            31Gi       337Mi        30Gi       0.0Ki       848Mi        30Gi

Disk Usage:
/dev/root        97G  4.3G   93G   5% /
```

### Real-Time Monitoring (During Phase Execution)

**Purpose**: Capture ACTUAL peak CPU/memory/disk usage during agent execution

**Method 1: Manual Polling** (every 2-5 minutes during execution)
```bash
# Run this command repeatedly while agents are running
gcloud compute ssh v12-test-golden-v2 --zone=us-central1-a \
  --command="uptime && free -h | grep Mem && screen -ls | grep -c 'p[0-9]'"
```

**Method 2: Automated Monitoring Script** (recommended for long phases)
```bash
# Create monitoring script on VM
cat > /tmp/monitor_phase.sh << 'EOF'
#!/bin/bash
PHASE=$1
LOG="/tmp/phase${PHASE}_monitor.log"
echo "Timestamp,LoadAvg1,LoadAvg5,LoadAvg15,MemUsedMB,ActiveAgents" > $LOG

while true; do
    TIMESTAMP=$(date -u +"%Y-%m-%dT%H:%M:%SZ")
    LOAD=$(uptime | grep -oE 'load average: [0-9., ]+' | sed 's/load average: //')
    MEM=$(free -m | grep Mem | awk '{print $3}')
    AGENTS=$(screen -ls | grep -c "p${PHASE}-" || echo 0)
    
    echo "$TIMESTAMP,$LOAD,$MEM,$AGENTS" >> $LOG
    
    # Exit if no agents running
    if [ "$AGENTS" -eq 0 ]; then
        echo "No agents running, monitoring complete"
        break
    fi
    
    sleep 120  # Poll every 2 minutes
done
EOF

chmod +x /tmp/monitor_phase.sh

# Launch monitoring in background
gcloud compute ssh v12-test-golden-v2 --zone=us-central1-a \
  --command="nohup /tmp/monitor_phase.sh 0 > /tmp/monitor_phase0.out 2>&1 &"

# Retrieve results after phase completes
gcloud compute ssh v12-test-golden-v2 --zone=us-central1-a \
  --command="cat /tmp/phase0_monitor.log"
```

**Method 3: GCP Monitoring** (best for production)
- Enable Cloud Monitoring on VM
- View metrics in GCP Console: Compute Engine → VM instances → Monitoring tab
- Metrics available: CPU utilization, memory usage, disk I/O
- Historical data retained for 6 weeks

## Metrics to Record

### Post-Execution Metrics

| Metric | Source | Calculation | Example |
|--------|--------|-------------|---------|
| **CPU Cores** | `nproc` | Direct output | 8 |
| **Load Average (1 min)** | `uptime` | First value | 0.00 |
| **Load Average (5 min)** | `uptime` | Second value | 0.06 |
| **Load Average (15 min)** | `uptime` | Third value | 0.10 |
| **CPU Utilization %** | Calculated | (Load avg ÷ cores) × 100 | 1.25% |
| **Memory Total** | `free -h` | Mem: column 2 | 31 GB |
| **Memory Used** | `free -h` | Mem: column 3 | 337 MB |
| **Memory %** | Calculated | (Used ÷ Total) × 100 | 1.1% |
| **Disk Total** | `df -h` | Column 2 | 97 GB |
| **Disk Used** | `df -h` | Column 3 | 4.3 GB |
| **Disk %** | `df -h` | Column 5 | 5% |
| **Concurrent Agents** | Manual | Peak from monitoring | ~50 |

### Real-Time Metrics (During Execution)

| Metric | Source | Calculation | Example |
|--------|--------|-------------|---------|
| **Peak Load Average** | Monitoring log | Max value during execution | 2.5 |
| **Peak CPU %** | Calculated | (Peak load ÷ cores) × 100 | 31.25% |
| **Peak Memory MB** | Monitoring log | Max value during execution | 8,500 MB |
| **Peak Memory %** | Calculated | (Peak MB ÷ Total MB) × 100 | 27.4% |
| **Active Agents Timeline** | Monitoring log | Agent count over time | 0→50→0 |
| **Execution Duration** | Monitoring log | First to last timestamp | 45 minutes |

## Documentation Template

Add to each phase completion report:

```markdown
### VM Resource Usage (Phase X)

| Resource | Capacity | Peak Usage | Utilization | Status |
|----------|----------|------------|-------------|--------|
| **CPU Cores** | 8 vCPU | 0.10 load avg | 1.25% | ✅ Minimal |
| **Memory** | 31 GB | 337 MB | 1.1% | ✅ Minimal |
| **Disk** | 97 GB | 4.3 GB | 4.4% | ✅ Minimal |
| **Concurrent Agents** | 80 max | ~50 peak | 62.5% | ✅ Optimal |

**Analysis**: VM resources were barely utilized during Phase X. Load average peaked at 0.10 (1.25% of 8 cores), indicating the VM can easily handle 2-3x more concurrent agents. Memory usage remained under 2%, and disk usage is negligible.

**Recommendation**: Current VM capacity is sufficient for all phases. No scaling required.
```

## Status Thresholds

### CPU Utilization
- ✅ **Optimal**: <50% (load avg < 4.0 on 8-core VM)
- ⚠️ **Warning**: 50-80% (load avg 4.0-6.4)
- ❌ **Critical**: >80% (load avg >6.4)

### Memory Utilization
- ✅ **Optimal**: <50% (<15.5 GB used on 31 GB VM)
- ⚠️ **Warning**: 50-80% (15.5-24.8 GB used)
- ❌ **Critical**: >80% (>24.8 GB used)

### Disk Utilization
- ✅ **Optimal**: <50% (<48.5 GB used on 97 GB disk)
- ⚠️ **Warning**: 50-80% (48.5-77.6 GB used)
- ❌ **Critical**: >80% (>77.6 GB used)

### Concurrent Agents
- ✅ **Optimal**: 50-70% of theoretical max
- ⚠️ **Warning**: 70-90% of theoretical max
- ❌ **Critical**: >90% of theoretical max

## Interpretation Guide

### Load Average
- **0.00-1.00**: Idle or very light load
- **1.00-4.00**: Light load (on 8-core VM)
- **4.00-6.00**: Moderate load
- **6.00-8.00**: Heavy load
- **>8.00**: Overloaded (queuing)

**Rule of Thumb**: Load average should stay below number of cores for optimal performance.

### Memory Usage
- **<1 GB**: Minimal (typical for Phase 0-4)
- **1-10 GB**: Light (typical for Phase 5)
- **10-20 GB**: Moderate (heavy Phase 5 with many agents)
- **>20 GB**: Heavy (approaching capacity)

### Disk Usage
- **<5 GB**: Minimal (typical for all phases)
- **5-20 GB**: Light (after multiple waves)
- **20-50 GB**: Moderate (long-term accumulation)
- **>50 GB**: Heavy (cleanup recommended)

## Scaling Decisions

### When to Scale UP (larger VM)
- ❌ CPU utilization >80% for >10 minutes
- ❌ Memory utilization >80%
- ❌ Agents frequently timeout or fail
- ❌ Load average consistently >cores

### When to Scale DOWN (smaller VM)
- ✅ CPU utilization <20% consistently
- ✅ Memory utilization <20% consistently
- ✅ Cost optimization needed
- ✅ Fewer concurrent agents planned

### Current VM (n2-standard-8)
- **Capacity**: 8 vCPU, 31 GB RAM, 97 GB disk
- **Cost**: ~$0.39/hour (~$280/month)
- **Status**: OVER-PROVISIONED for Phase 0-4
- **Recommendation**: Keep for Phase 5 (higher load expected)

## Enforcement

### Phase Completion Reports
- ❌ **INCOMPLETE**: Reports without CPU metrics
- ✅ **COMPLETE**: Reports with full CPU metrics table

### Wave Execution
- MUST collect CPU metrics after each phase
- MUST document in phase completion report
- MUST use metrics to validate capacity for next phase
- MUST update scaling recommendations if thresholds exceeded

### Skill Updates
- `gcp-vm-wave-execution` skill MUST reference this protocol
- Post-use audit MUST verify CPU metrics collected
- Skill gaps related to monitoring MUST be documented

## Historical Baseline

### Wave 4 Phase 0 (2026-06-14)

**⚠️ CRITICAL LIMITATION**: Only post-execution metrics collected. Peak utilization during execution is UNKNOWN.

**Post-Execution Metrics** (after all agents finished):
- **Epics**: 80
- **Concurrent Agents**: ~50 peak (estimated from launch pattern)
- **CPU**: 8 vCPU, 0.10 load avg (1.25% utilization) - IDLE STATE
- **Memory**: 31 GB total, 337 MB used (1.1% utilization) - IDLE STATE
- **Disk**: 97 GB total, 4.3 GB used (4.4% utilization)
- **Status**: ✅ Returned to idle successfully

**Peak Utilization During Execution**: UNKNOWN
- No real-time monitoring was performed
- 15-minute load average (0.10) suggests peak was also minimal
- No OOM events in logs indicates memory was sufficient
- **Action Required**: Implement real-time monitoring for Phase 1

**Conclusion**: VM likely over-provisioned for Phase 0, but needs validation with real-time monitoring

**Reference**: `WAVE4_PHASE0_COMPLETION_REPORT.md`

### Lessons Learned
1. **Post-execution metrics are insufficient** - they only show idle state
2. **Real-time monitoring is MANDATORY** - captures actual peak utilization
3. **15-minute load average is useful** - provides rough peak estimate
4. **GCP Cloud Monitoring recommended** - automatic historical data retention

## Related Documents

- **10-Phase SOP**: [`docs/workflow/V12_EPIC_WORKFLOW_10_PHASE_SOP.md`](../workflow/V12_EPIC_WORKFLOW_10_PHASE_SOP.md)
- **GCP VM Skill**: [`.bob/skills/gcp-vm-wave-execution/skill.md`](../../.bob/skills/gcp-vm-wave-execution/skill.md)
- **Wave 4 Handoff**: [`WAVE4_HANDOFF_CORRECTED.md`](../../WAVE4_HANDOFF_CORRECTED.md)

---

**Document Version**: 1.0
**Last Updated**: 2026-06-14T18:05:00Z
**Next Review**: After Wave 4 Phase 1 completion
**Maintainer**: V12 Orchestration Team