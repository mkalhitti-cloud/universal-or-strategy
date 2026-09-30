# Wave 2 Local Execution Strategy

**Date**: 2026-06-12T05:12 UTC
**Decision**: Pivot from GCP VMs to local Windows execution
**Reason**: Bob Shell cannot be automated on cloud VMs (permissions paradox)

## Executive Summary

After 7 VM iterations and extensive debugging, we've determined that **Bob Shell is not designed for cloud automation**. The installer has a permissions paradox that makes automated VM provisioning impossible.

**New Strategy**: Execute Wave 2 locally on the Windows development machine using PowerShell orchestration (Antigravity).

## Why GCP VMs Failed

### The Permissions Paradox
- Bob Shell installer runs as user → Permission denied (can't write to `/usr/lib/node_modules/`)
- Bob Shell installer runs as root → Installs for root only (user can't access `bob` command)
- No middle ground exists in the current installer design

### Complete Failure Timeline
| Attempt | Issue | Root Cause |
|---------|-------|------------|
| v1-v3 | Setup issues | Script/URL problems |
| v4 | Package not found | `@ibm/bob-shell` doesn't exist on npm |
| v6 | Node.js missing | Installer requires Node.js 22.15+ |
| v7 | **Permission denied** | **Installer design flaw** |

**Conclusion**: Bob Shell is a **desktop development tool**, not a cloud-automatable service.

## New Architecture: Local Execution

### Overview
```
┌─────────────────────────────────────────────────────────┐
│ Windows Development Machine (Local)                     │
├─────────────────────────────────────────────────────────┤
│                                                          │
│  ┌──────────────────────────────────────────────────┐  │
│  │ Antigravity (PowerShell Orchestrator)            │  │
│  │ - Manages epic queue                             │  │
│  │ - Dispatches to Bob Shell                        │  │
│  │ - Monitors progress                              │  │
│  │ - Handles failures                               │  │
│  └──────────────────────────────────────────────────┘  │
│                          │                              │
│                          ▼                              │
│  ┌──────────────────────────────────────────────────┐  │
│  │ Bob Shell (v12-engineer mode)                    │  │
│  │ - Executes epic phases                           │  │
│  │ - Performs surgical refactoring                  │  │
│  │ - Runs build/test validation                     │  │
│  └──────────────────────────────────────────────────┘  │
│                          │                              │
│                          ▼                              │
│  ┌──────────────────────────────────────────────────┐  │
│  │ Local File System                                │  │
│  │ - Repository: C:\WSGTA\universal-or-strategy     │  │
│  │ - Manifests: docs/brain/EPIC-*/                  │  │
│  │ - Logs: wave_execution.log                       │  │
│  └──────────────────────────────────────────────────┘  │
│                                                          │
└─────────────────────────────────────────────────────────┘
```

### Key Components

**1. Antigravity Orchestrator** (PowerShell)
- Reads epic queue from roadmap
- Dispatches epics to Bob Shell sequentially
- Monitors execution via log files
- Handles errors and retries
- Tracks API key usage and costs

**2. Bob Shell** (Existing Installation)
- Already installed and working on Windows
- Runs in `v12-engineer` mode
- Executes all epic phases (0-6)
- Performs surgical refactoring
- Validates builds and tests

**3. Local Storage**
- Repository: `C:\WSGTA\universal-or-strategy`
- Manifests: `docs/brain/EPIC-*/manifest.json`
- Logs: `wave_execution.log`
- Results: Per-epic completion reports

## Advantages Over GCP VMs

### 1. No Installation Issues ✅
- Bob Shell already installed and working
- No permission problems
- No network dependencies
- No VM startup time

### 2. Lower Cost ✅
- **GCP VM**: $0.093/hour × 10 hours = $0.93
- **Local**: $0.00 (electricity negligible)
- **Savings**: 100%

### 3. Faster Execution ✅
- No VM provisioning time (saves 8 minutes per VM)
- No SSH overhead
- Direct file system access
- Faster build times (local SSD vs network storage)

### 4. Simpler Architecture ✅
- No cloud infrastructure to manage
- No networking configuration
- No image management
- No multi-VM coordination

### 5. Better Debugging ✅
- Direct access to logs
- Can inspect state in real-time
- Can manually intervene if needed
- Familiar Windows environment

## Implementation Plan

### Phase 1: Orchestrator Enhancement (1 hour)
**Goal**: Enhance Antigravity to handle sequential epic execution

**Tasks**:
1. Read epic queue from `docs/roadmap/WAVE_2_EPICS.md`
2. For each epic:
   - Check if already complete (manifest exists)
   - Dispatch to Bob Shell via PowerShell
   - Monitor execution via log tailing
   - Detect completion or failure
   - Update tracking spreadsheet
3. Handle errors:
   - Retry logic (max 2 retries)
   - Skip to next epic on persistent failure
   - Log all failures for review

**Deliverable**: `scripts/antigravity_wave2_orchestrator.ps1`

### Phase 2: Bob Shell Integration (30 minutes)
**Goal**: Ensure Bob Shell can be called programmatically

**Tasks**:
1. Test Bob Shell CLI invocation from PowerShell
2. Verify epic commands work non-interactively
3. Confirm log output is parseable
4. Test error detection

**Deliverable**: Verified Bob Shell integration

### Phase 3: Monitoring & Logging (30 minutes)
**Goal**: Real-time visibility into execution progress

**Tasks**:
1. Implement log tailing in PowerShell
2. Parse Bob Shell output for progress indicators
3. Display current epic status
4. Track API key usage
5. Estimate completion time

**Deliverable**: `scripts/monitor_wave2.ps1`

### Phase 4: Dry Run (1 hour)
**Goal**: Test with 2 epics before full Wave 2

**Tasks**:
1. Select 2 simple epics (EPIC-CCN-164, EPIC-CCN-107)
2. Run orchestrator
3. Verify completion
4. Check manifests created correctly
5. Validate build passes

**Deliverable**: 2 completed epics, validated workflow

### Phase 5: Full Wave 2 Execution (8-10 hours)
**Goal**: Complete all 10 Wave 2 epics

**Tasks**:
1. Start orchestrator with full epic queue
2. Monitor progress
3. Handle any failures
4. Validate all completions
5. Generate final report

**Deliverable**: 10 completed epics, Wave 2 complete

## Execution Timeline

| Phase | Duration | Start | End |
|-------|----------|-------|-----|
| 1. Orchestrator | 1 hour | Now | +1h |
| 2. Integration | 30 min | +1h | +1.5h |
| 3. Monitoring | 30 min | +1.5h | +2h |
| 4. Dry Run | 1 hour | +2h | +3h |
| 5. Full Wave 2 | 8-10 hours | +3h | +13h |
| **Total** | **11-13 hours** | **Now** | **+13h** |

**Estimated Completion**: Within 24 hours of starting Phase 1

## Cost Comparison

### GCP VM Approach (Abandoned)
- **Setup**: $0.09 (7 failed VMs)
- **Execution**: $0.93 (10 hours × $0.093/hour)
- **Total**: $1.02

### Local Execution Approach (Adopted)
- **Setup**: $0.00
- **Execution**: $0.00
- **Total**: $0.00

**Savings**: $1.02 (100%)

## Risk Mitigation

### Risk 1: Local Machine Failure
- **Mitigation**: Regular git commits after each epic
- **Recovery**: Resume from last completed epic

### Risk 2: Power Outage
- **Mitigation**: Laptop battery backup
- **Recovery**: Orchestrator resumes from checkpoint

### Risk 3: Bob Shell Crash
- **Mitigation**: Retry logic (max 2 retries)
- **Recovery**: Skip to next epic, log failure

### Risk 4: API Key Exhaustion
- **Mitigation**: Monitor usage, rotate keys
- **Recovery**: Pause execution, add more keys

## Success Criteria

### Phase 1-3 Complete ✅
- [ ] Orchestrator can read epic queue
- [ ] Bob Shell responds to programmatic calls
- [ ] Monitoring displays real-time progress
- [ ] Logs are parseable and informative

### Phase 4 Complete ✅
- [ ] 2 epics completed successfully
- [ ] Manifests created correctly
- [ ] Build passes for both epics
- [ ] No manual intervention needed

### Phase 5 Complete ✅
- [ ] All 10 Wave 2 epics completed
- [ ] All manifests valid
- [ ] All builds pass
- [ ] Final report generated
- [ ] Ready for Wave 3

## Next Steps

1. **Immediate**: Start Phase 1 (Orchestrator Enhancement)
2. **Today**: Complete Phases 1-4 (setup and dry run)
3. **Tomorrow**: Start Phase 5 (full Wave 2 execution)
4. **Within 24h**: Wave 2 complete

## References

- **Failure Analysis**: `docs/workflow/VM_SETUP_FAILURE_ANALYSIS.md`
- **Original Plan**: `docs/workflow/WAVE_2_CONTINUATION_PROMPT.md`
- **Epic List**: `docs/roadmap/WAVE_2_EPICS.md`
- **Antigravity Docs**: `docs/workflow/ANTIGRAVITY_QUICK_REFERENCE.md`

---

**Status**: Ready to begin Phase 1
**Confidence**: HIGH - leverages existing, working tools
**Timeline**: 11-13 hours total execution time