# Wave Execution Guide - V12 Parallel Epic Processing

## Overview

The Wave Coordinator enables parallel processing of multiple epics through sequential phases. This guide explains how to use the wave-based execution system.

## Architecture

```
YOU (Orchestrator - Single Bob Session)
    ↓
Wave Coordinator (Python script)
    ↓
┌─────────────────────────────────────────────────┐
│ WAVE 1: Phase 0 (10 epics in parallel)         │
│   ├─ phase-0 MCP for EPIC-CCN-26               │
│   ├─ phase-0 MCP for EPIC-CCN-27               │
│   └─ ... (8 more epics)                        │
└─────────────────────────────────────────────────┘
    ↓ Wait for all Phase 0 to complete
┌─────────────────────────────────────────────────┐
│ WAVE 2: Phase 1 (10 epics in parallel)         │
│   ├─ phase-1 MCP for EPIC-CCN-26               │
│   ├─ phase-1 MCP for EPIC-CCN-27               │
│   └─ ... (8 more epics)                        │
└─────────────────────────────────────────────────┘
    ↓ Continue through all 9 phases
```

## Quick Start

### 1. Initialize Wave Coordinator

```python
from scripts.wave_coordinator import WaveCoordinator

# Create coordinator with default settings (wave_size=10)
coordinator = WaveCoordinator(wave_size=10)
```

### 2. Get Next Wave of Epics

```python
# Get first 10 pending epics
epic_ids = coordinator.get_next_wave(wave_number=1)

# Output:
# ============================================================
# WAVE 1: 10 epics
# ============================================================
#   - EPIC-CCN-26: ExecuteFFMALimitEntry (CYC=45)
#   - EPIC-CCN-27: ExecuteMOMOEntry (CYC=42)
#   - ... (8 more)
```

### 3. Execute Phase 0 for All Epics

```python
# Generate MCP tool calls for Phase 0
wave_result = coordinator.execute_wave(epic_ids, phase_id=0)

# This returns instructions for calling MCP tools
# YOU must execute these in Bob IDE
```

### 4. Execute MCP Tool Calls in Bob IDE

The coordinator generates instructions like:

```
# Execute Phase 0: Hotspot Analysis

## MCP Tool Calls (Execute in Bob IDE)

### Call 1: EPIC-CCN-26
use_mcp_tool(
    server_name="phase-0-hotspot",
    tool_name="execute_phase_0",
    arguments={"epic_id": "EPIC-CCN-26"}
)

### Call 2: EPIC-CCN-27
use_mcp_tool(
    server_name="phase-0-hotspot",
    tool_name="execute_phase_0",
    arguments={"epic_id": "EPIC-CCN-27"}
)

... (8 more calls)
```

### 5. Proceed to Next Phase

After all Phase 0 calls complete:

```python
# Execute Phase 1 for same epics
wave_result = coordinator.execute_wave(epic_ids, phase_id=1)

# Execute MCP calls in Bob IDE (same pattern as Phase 0)
```

### 6. Complete All Phases

```python
# Run all phases for a batch of epics
results = coordinator.run_wave_batch(epic_ids)

# This generates instructions for all 9 phases
# YOU execute each phase's MCP calls in Bob IDE
```

## Configuration

### Wave Size

Control how many epics are processed per wave:

```python
# Small batch (safer, easier to monitor)
coordinator = WaveCoordinator(wave_size=10)

# Medium batch (faster)
coordinator = WaveCoordinator(wave_size=20)

# Large batch (much faster)
coordinator = WaveCoordinator(wave_size=50)

# Process ALL pending epics in one wave
pending = coordinator.load_roadmap()
coordinator = WaveCoordinator(wave_size=len(pending))
```

### Phase Range

Execute only specific phases:

```python
# Only Phase 0 and Phase 1
coordinator = WaveCoordinator(start_phase=0, end_phase=1)

# Skip Phase 0, start from Phase 1
coordinator = WaveCoordinator(start_phase=1, end_phase=6)
```

### Configuration File

Edit `scripts/wave_config.json`:

```json
{
  "execution_mode": "wave",
  "wave_size": 10,
  "start_phase": 0,
  "end_phase": 6,
  "batch_strategy": "sequential_phases",
  "checkpoint_enabled": true
}
```

## Execution Patterns

### Pattern 1: Single Wave, All Phases (Recommended)

Process 10 epics through all 9 phases:

```python
coordinator = WaveCoordinator(wave_size=10)
epic_ids = coordinator.get_next_wave(1)
results = coordinator.run_wave_batch(epic_ids)

# Execute all MCP calls in Bob IDE
```

**Best for**: Initial testing, quality control

### Pattern 2: Multiple Waves, All Phases

Process 30 epics (3 waves of 10):

```python
coordinator = WaveCoordinator(wave_size=10)

for wave_num in range(1, 4):  # Waves 1, 2, 3
    epic_ids = coordinator.get_next_wave(wave_num)
    results = coordinator.run_wave_batch(epic_ids)
    # Execute MCP calls in Bob IDE
```

**Best for**: Production execution, large batches

### Pattern 3: Single Phase, Multiple Waves

Process Phase 0 for 50 epics (5 waves of 10):

```python
coordinator = WaveCoordinator(wave_size=10, start_phase=0, end_phase=0)

for wave_num in range(1, 6):  # Waves 1-5
    epic_ids = coordinator.get_next_wave(wave_num)
    wave_result = coordinator.execute_wave(epic_ids, phase_id=0)
    # Execute MCP calls in Bob IDE
```

**Best for**: Hotspot analysis only, pre-filtering

## Checkpointing

The coordinator automatically saves checkpoints after each phase:

```python
# Checkpoints saved to: docs/brain/wave_checkpoints.json
{
  "timestamp": "2026-06-11T01:00:00Z",
  "phase_id": 0,
  "epic_ids": ["EPIC-CCN-26", "EPIC-CCN-27", ...],
  "result": {...}
}
```

**Resume from checkpoint**:

```python
import json
from pathlib import Path

# Load last checkpoint
checkpoints = json.loads(Path("docs/brain/wave_checkpoints.json").read_text())
last_checkpoint = checkpoints[-1]

# Resume from next phase
coordinator = WaveCoordinator(start_phase=last_checkpoint["phase_id"] + 1)
epic_ids = last_checkpoint["epic_ids"]
results = coordinator.run_wave_batch(epic_ids)
```

## Monitoring Progress

### Check Roadmap Status

```python
pending = coordinator.load_roadmap()
print(f"Total pending epics: {len(pending)}")
print(f"Total waves needed: {(len(pending) + 9) // 10}")
```

### Generate Execution Plan

```python
# Generate plan for 3 waves (30 epics)
plan = coordinator.generate_execution_plan(num_waves=3)

# Output:
{
  "total_epics": 163,
  "total_waves": 17,
  "wave_size": 10,
  "waves_to_execute": 3,
  "waves": [
    {
      "wave_number": 1,
      "epic_ids": ["EPIC-CCN-26", ...],
      "phases": [...]
    },
    ...
  ]
}
```

## Performance Expectations

| Wave Size | Epics/Hour | 173 Epics Total | Speedup vs Sequential |
|-----------|------------|-----------------|----------------------|
| 10 | 20 | 8.6 hours | 10x |
| 20 | 40 | 4.3 hours | 20x |
| 50 | 100 | 1.7 hours | 50x |
| ALL (173) | 346 | 0.5 hours | 173x |

**Note**: Actual speedup depends on:
- MCP tool response time (<1 second with FastMCP)
- Your BobCoin budget
- Network latency
- Epic complexity

## Troubleshooting

### Issue: MCP Tool Timeout

**Symptom**: Phase MCP tool takes >5 minutes

**Solution**: Verify FastMCP migration
```bash
# Check .mcp.json uses FastMCP versions
grep "fastmcp" .mcp.json

# Restart Bob IDE to reload MCP servers
```

### Issue: Epic Not Found

**Symptom**: `execute_phase_X` returns "Epic not found"

**Solution**: Verify epic exists in roadmap
```python
pending = coordinator.load_roadmap()
epic_ids = [e["epic_number"] for e in pending]
print(epic_ids[:10])  # First 10 pending epics
```

### Issue: Phase Dependency Failure

**Symptom**: Phase 2 fails because Phase 1 didn't complete

**Solution**: Always execute phases sequentially
```python
# WRONG: Skip Phase 1
coordinator = WaveCoordinator(start_phase=2)

# RIGHT: Execute all phases in order
coordinator = WaveCoordinator(start_phase=0, end_phase=6)
```

## Migration to Multi-Orchestrator (Future)

After perfecting single orchestrator, migrate to multi-orchestrator:

1. **Test single orchestrator** with 10 epics (1 wave)
2. **Scale to 50 epics** (5 waves)
3. **Validate quality** (build passes, complexity reduced)
4. **Configure multi-orchestrator** (4 workers)
5. **Test parallel execution** (4 epics simultaneously)
6. **Scale to 173 epics** (full roadmap)

See `docs/workflow/MULTI_ORCHESTRATOR_MIGRATION.md` for details.

## CLI Usage

```bash
# Generate execution plan for 3 waves
python scripts/wave_coordinator.py --wave-size 10 --num-waves 3 --plan-only

# Execute 1 wave (interactive)
python scripts/wave_coordinator.py --wave-size 10 --num-waves 1

# Execute Phase 0 only for 5 waves
python scripts/wave_coordinator.py --wave-size 10 --num-waves 5 --start-phase 0 --end-phase 0
```

## Next Steps

1. **Test with 10 epics** (1 wave, all phases)
2. **Review results** (check build, complexity, quality)
3. **Scale to 50 epics** (5 waves)
4. **Optimize wave size** (based on performance)
5. **Execute full roadmap** (173 epics, 17 waves)

---

**Status**: ✅ Wave Coordinator implemented and ready for testing
**Version**: V12.25
**Last Updated**: 2026-06-11