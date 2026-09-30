
# Phase MCP Quick Start Guide

**Version**: V12.25
**Last Updated**: 2026-06-10

## TL;DR

The Phase MCP system enables parallel execution of V12 epic phases using independent MCP servers. Each phase runs as a separate session with clear input/output contracts.

## Prerequisites

```bash
# 1. Install Python dependencies
pip install -r scripts/requirements_worker_mcp.txt

# 2. Verify MCP configuration
python scripts/test_phase_mcp_servers.py

# Expected output: 9/9 servers PASS
```

## Basic Usage

### Execute Single Epic (Sequential)

```bash
# Use orchestration script
python scripts/orchestrate_phase_execution.py --epic EPIC-CCN-22 --phase 0
python scripts/orchestrate_phase_execution.py --epic EPIC-CCN-22 --phase 1
python scripts/orchestrate_phase_execution.py --epic EPIC-CCN-22 --phase 1.5
# ... continue through phases
```

### Execute Wave (Parallel)

```bash
# Run Phase 0 for all pending epics
python scripts/orchestrate_phase_execution.py --wave 0

# Run Phase 1 for all ready epics
python scripts/orchestrate_phase_execution.py --wave 1
```

### Check Epic Status

```bash
# Show execution plan
python scripts/orchestrate_phase_execution.py --epic EPIC-CCN-22 --plan

# Output shows which phases are complete/pending/ready
```

## MCP Tool Invocation (via Bob CLI)

### Phase 0: Hotspot Analysis

```python
use_mcp_tool(
  server='phase-0-hotspot',
  tool='execute_phase_0',
  args={
    'epic_id': 'EPIC-CCN-22',
    'method': 'ProcessBarUpdate',
    'file': 'src/V12_002.BarUpdate.cs',
    'cyc': 28
  }
)
```

### Phase 1-6: Other Phases

See full documentation in PHASE_MCP_ARCHITECTURE.md for complete tool invocation examples.

## Common Workflows

### New Epic (Full Workflow)

```bash
# Execute phases sequentially
for phase in 0 1 1.5 2 3 4 5 6; do
  python scripts/orchestrate_phase_execution.py --epic EPIC-CCN-22 --phase $phase
done
```

### Batch Processing (Wave Execution)

```bash
# Process all pending epics through Phase 0
python scripts/orchestrate_phase_execution.py --wave 0
```

## Troubleshooting

### Test Suite Fails

```bash
# Run verbose tests
python scripts/test_phase_mcp_servers.py --verbose
```

### MCP Server Not Found

```bash
# Verify configuration
cat .bob/mcp.json | grep phase-0-hotspot
```

## Next Steps

- Read full architecture: `PHASE_MCP_ARCHITECTURE.md`
- Review workflow design: `V12_EPIC_WORKFLOW_REFACTORING_DESIGN.md`