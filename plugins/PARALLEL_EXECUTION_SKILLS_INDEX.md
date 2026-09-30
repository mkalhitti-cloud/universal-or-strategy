# Parallel Execution Skills - Master Index

## Overview

This repository contains **4 complementary skills** for parallel epic execution. Each serves a different use case but they can be combined for maximum efficiency.

## Skills Summary

| Skill | Location | Purpose | Scope |
|-------|----------|---------|-------|
| **GCP VM Wave Execution** | `.bob/skills/gcp-vm-wave-execution/` | Remote VM parallel execution | Phase 0-6 (VM) |
| **Parallel Epic Execution** | `plugins/parallel-epic-execution/` | Local worktree parallelization | Full epic (local) |
| **Multi-Agent Orchestrator** | `plugins/multi-agent-orchestrator/` | Sub-agent phase specialization | Full epic (local) |
| **Wave 2 Shell Workaround** | `plugins/multi-agent-orchestrator/WAVE2_SHELL_WORKAROUND.md` | Tool bug workaround | Phase 0 (VM) |

## Skill Relationships

```
┌─────────────────────────────────────────────────────────────┐
│                    GCP VM Wave Execution                     │
│  (.bob/skills/gcp-vm-wave-execution/skill.md)               │
│                                                              │
│  • Remote VM execution (SSH)                                │
│  • 9 parallel Phase 0 agents                                │
│  • Uses: v12-phase0-hotspot mode                           │
│  • Requires: Wave 2 Shell Workaround ──────────┐           │
└─────────────────────────────────────────────────┼───────────┘
                                                   │
                                                   ▼
┌─────────────────────────────────────────────────────────────┐
│              Wave 2 Shell Workaround                         │
│  (plugins/multi-agent-orchestrator/WAVE2_SHELL_WORKAROUND)  │
│                                                              │
│  • Fixes read_file tool bug in SSH mode                     │
│  • Uses shell commands for file I/O                         │
│  • Template: phase0_message_template_shell.txt              │
└─────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────┐
│              Parallel Epic Execution                         │
│  (plugins/parallel-epic-execution/SKILL.md)                 │
│                                                              │
│  • Local Windows worktrees                                  │
│  • 3 Bob CLI sessions                                       │
│  • File-based clustering (SIMA, Orders, Lifecycle)          │
│  • Batch F5 verification                                    │
└─────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────┐
│            Multi-Agent Orchestrator                          │
│  (plugins/multi-agent-orchestrator/SKILL.md)                │
│                                                              │
│  • Single Bob session spawns sub-agents                     │
│  • Phase specialization (different modes)                   │
│  • Artifact-based communication                             │
│  • Status: POC testing                                      │
└─────────────────────────────────────────────────────────────┘
```

## When to Use Each Skill

### Use GCP VM Wave Execution When:
✅ Need to execute Phase 0-4 for 9+ epics  
✅ Have remote VM with jCodemunch indexed  
✅ Want parallel execution without local resource usage  
✅ Need automatic recovery and bobcoin tracking  

**Current Status**: Active, production-ready with shell workaround

### Use Parallel Epic Execution When:
✅ Working on local Windows machine  
✅ Need full epic execution (Phase 0-6)  
✅ Epics target different files (no conflicts)  
✅ Want maximum time savings (64%)  

**Current Status**: Active, proven workflow

### Use Multi-Agent Orchestrator When:
✅ Working on single epic  
✅ Want phase specialization (different modes per phase)  
✅ Need clear audit trail (artifact handoffs)  

**Current Status**: POC testing (sub-agent support unknown)

### Use Wave 2 Shell Workaround When:
✅ Running Phase 0 on VM via SSH  
✅ Encountering `read_file` tool failures  
✅ Need reliable file I/O in non-interactive mode  

**Current Status**: Active, required for GCP VM Wave Execution

## Integration Scenarios

### Scenario 1: Full Parallel Workflow (Recommended)
```
1. GCP VM Wave Execution (Phase 0-4)
   ├─ 9 epics analyzed in parallel on VM
   ├─ Uses Wave 2 Shell Workaround
   └─ Output: 9 ticket sets ready

2. Parallel Epic Execution (Phase 5-6)
   ├─ 3 epics executed locally (worktrees)
   ├─ Batch F5 verification
   └─ Repeat for remaining 6 epics
```

**Result**: Maximum parallelization at both analysis and execution stages

### Scenario 2: Local Only
```
1. Parallel Epic Execution (Phase 0-6)
   ├─ 3 worktrees
   ├─ Full epic workflow
   └─ Batch F5 testing
```

**Result**: No VM needed, but limited to 3 parallel epics

### Scenario 3: VM Analysis + Sequential Execution
```
1. GCP VM Wave Execution (Phase 0-4)
   └─ 9 epics analyzed in parallel

2. Sequential Execution (Phase 5-6)
   └─ Execute epics one at a time locally
```

**Result**: Informed prioritization without parallel execution complexity

## Critical Dependencies

### GCP VM Wave Execution Requires:
- ✅ Golden image: `v12-bob-shell-golden-v2`
- ✅ jCodemunch-MCP indexed repository
- ✅ 10 Bob Shell API keys (160 bobcoins each)
- ✅ Wave 2 Shell Workaround (for Phase 0)
- ✅ Configuration: `docs/workflow/WAVE_2_CONFIGURATION.md`

### Parallel Epic Execution Requires:
- ✅ Git worktrees setup
- ✅ Bob CLI installed
- ✅ Auto-approval enabled (`.bob/settings.json`)
- ✅ Setup script: `scripts/setup_parallel_epic_workflow.ps1`

### Multi-Agent Orchestrator Requires:
- ✅ Bob Shell sub-agent support (POC testing)
- ✅ Artifact-based communication
- ✅ Phase-specific modes

### Wave 2 Shell Workaround Requires:
- ✅ SSH access to VM
- ✅ Custom mode: `v12-phase0-hotspot`
- ✅ Shell template: `scripts/wave2/phase0_message_template_shell.txt`

## Known Issues & Solutions

### Issue: read_file Tool Fails in SSH Mode
**Skill**: Wave 2 Shell Workaround  
**Solution**: Use shell commands (`cat`, `ls`, `wc -l`)  
**Status**: Working solution deployed  
**Reference**: `plugins/multi-agent-orchestrator/WAVE2_SHELL_WORKAROUND.md`

### Issue: Bob CLI Not Auto-Approving
**Skill**: Parallel Epic Execution  
**Solution**: Verify `.bob/settings.json` exists, restart Bob CLI  
**Status**: Documented in skill  

### Issue: Sub-Agents Don't Spawn
**Skill**: Multi-Agent Orchestrator  
**Solution**: Fall back to Parallel Epic Execution  
**Status**: POC testing required  

## Quick Start Guide

### For Wave 2 Phase 0 (VM):
```bash
# 1. Read configuration
cat docs/workflow/WAVE_2_CONFIGURATION.md

# 2. Generate scripts
python scripts/wave2/launch_phase0_fixed.py

# 3. Upload to VM
gcloud compute scp scripts/wave2/_p0_*.sh v12-test-golden-v2:/home/malhitticrypto/universal-or-strategy/ --zone=us-central1-a

# 4. Execute
gcloud compute ssh v12-test-golden-v2 --zone=us-central1-a --command="chmod +x /home/malhitticrypto/universal-or-strategy/launch_phase0_all.sh && /home/malhitticrypto/universal-or-strategy/launch_phase0_all.sh"
```

### For Local Parallel Execution:
```bash
# 1. Setup worktrees
powershell -File .\scripts\setup_parallel_epic_workflow.ps1

# 2. Launch Bob CLI in each worktree
cd C:\WSGTA\universal-or-epic-cluster-1
bob

# 3. Start epic execution
Execute EPIC-CCN-19 Ticket 1
```

## Documentation

### Primary Documentation
- **GCP VM Wave**: `.bob/skills/gcp-vm-wave-execution/skill.md`
- **Parallel Epic**: `plugins/parallel-epic-execution/SKILL.md`
- **Orchestrator**: `plugins/multi-agent-orchestrator/SKILL.md`
- **Shell Workaround**: `plugins/multi-agent-orchestrator/WAVE2_SHELL_WORKAROUND.md`

### Supporting Documentation
- **Skill Relationships**: `plugins/SKILL_RELATIONSHIPS.md`
- **Wave 2 Config**: `docs/workflow/WAVE_2_CONFIGURATION.md`
- **Epic Workflow**: `docs/workflow/V12_EPIC_WORKFLOW_REFACTORING_DESIGN.md`
- **Final Solution**: `scripts/wave2/FINAL_SOLUTION_SUMMARY.md`

## Version History

- **V1.0** (2026-06-09): Initial skills created
- **V1.1** (2026-06-12): Added GCP VM Wave Execution skill
- **V1.2** (2026-06-13): Added Wave 2 Shell Workaround
- **V1.3** (2026-06-13): Created master index (this file)

## Maintenance

When updating any skill:
1. ✅ Update the skill's own documentation
2. ✅ Update this master index if relationships change
3. ✅ Update `plugins/SKILL_RELATIONSHIPS.md` if needed
4. ✅ Run post-use audit on the skill
5. ✅ Document new failure modes and solutions

## Contact

For questions or issues with these skills, refer to:
- **Technical Issues**: Check skill-specific "Common Issues" sections
- **Architecture Questions**: See `plugins/SKILL_RELATIONSHIPS.md`
- **Wave 2 Specific**: See `docs/workflow/WAVE_2_CONFIGURATION.md`