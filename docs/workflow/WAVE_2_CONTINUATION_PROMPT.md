# Wave 2 Continuation Prompt - 10x Parallel Phase Execution

**Copy and paste this entire prompt into your new session:**

---

# 🎯 Wave 2 Execution - 10 High-Complexity Epics (CYC 18-36 → 8)

## Context Summary

I am continuing Wave 2 execution using the **10x parallel phase workflow**. This means processing all 10 epics through each phase simultaneously (not one epic at a time).

### Previous Session Discoveries
- ✅ Wave 1 (EPIC-CCN-21 to 30) already complete in worker worktrees
- ✅ Skipping Wave 1 to avoid duplicate work
- ✅ Starting Wave 2 with 10 highest-complexity epics
- ✅ Target: CYC ≤ 8 (not 15 - that's stale documentation)

### Wave 2 Architecture
```
Orchestrator (You - Single Session)
    ↓
Sequential Phases (0 through 6)
    ↓
Each Phase: Process ALL 10 epics in parallel
    ↓
Phase 0: 10 MCP calls → Phase 1: 10 MCP calls → ... → Phase 6: 10 MCP calls
```

## Wave 2 Epics (10 total, sorted by complexity)

| # | Epic ID | Method | File | CYC | Target | Reduction |
|---|---------|--------|------|-----|--------|-----------|
| 1 | EPIC-CCN-164 | IsCommandForThisInstrument | src/V12_002.UI.IPC.cs | 36 | 8 | 78% |
| 2 | EPIC-CCN-107 | HydrateFromOpenPositions | src/V12_002.SIMA.Lifecycle.cs | 31 | 8 | 74% |
| 3 | EPIC-CCN-108 | SweepBrokerOrders | src/V12_002.SIMA.Lifecycle.cs | 24 | 8 | 67% |
| 4 | EPIC-CCN-32 | HandleTerminated | src/V12_002.Lifecycle.cs | 23 | 8 | 65% |
| 5 | EPIC-CCN-109 | HydrateWorkingOrdersFromBroker | src/V12_002.SIMA.Lifecycle.cs | 19 | 8 | 58% |
| 6 | EPIC-CCN-110 | AdoptMasterOrders | src/V12_002.SIMA.Lifecycle.cs | 19 | 8 | 58% |
| 7 | EPIC-CCN-155 | TryHandleFleetCommand | src/V12_002.UI.IPC.Commands.Fleet.cs | 19 | 8 | 58% |
| 8 | EPIC-CCN-98 | ProcessFlattenWorkItem_CancelOrders | src/V12_002.SIMA.Flatten.cs | 18 | 8 | 56% |
| 9 | EPIC-CCN-128 | SymmetryGuardReplaceExistingFollowerTarget | src/V12_002.Symmetry.Replace.cs | 18 | 8 | 56% |
| 10 | EPIC-CCN-129 | SymmetryGuardTryResolveFollowersForDispatch | src/V12_002.Symmetry.Replace.cs | 18 | 8 | 56% |

**Average**: CYC 23.2 → 8 (65% reduction)

## Execution Plan

### Phase 0: Hotspot Analysis (10 MCP calls)
Execute Phase 0 for all 10 epics:

```
use_mcp_tool("phase-0-hotspot", "execute_phase_0", {
  "epic_id": "EPIC-CCN-164",
  "method": "IsCommandForThisInstrument",
  "file": "src/V12_002.UI.IPC.cs",
  "cyc": 36,
  "jcodemunch_data": {}
})
```

Repeat for EPIC-CCN-107, 108, 32, 109, 110, 155, 98, 128, 129.

### Phase 1: Scope Definition (10 MCP calls)
After Phase 0 completes for all 10 epics:

```
use_mcp_tool("phase-1-scope", "execute_phase_1", {
  "epic_id": "EPIC-CCN-164"
})
```

Repeat for all 10 epics.

### Phase 1.5: Scope Boundary Validation (10 MCP calls)
### Phase 2: Architecture Planning (10 MCP calls)
### Phase 3: DNA & PR Audit (10 MCP calls)
### Phase 4: Ticket Generation (10 MCP calls)
### Phase 5: Ticket Execution (10 MCP calls)
### Phase 5.5: Verification (10 MCP calls)
### Phase 6: Final Review (10 MCP calls)

**Total**: 90 MCP calls (10 epics × 9 phases)

## Available MCP Servers

All Phase MCP servers are FastMCP (no timeouts, <1 second response):

1. `phase-0-hotspot` - Hotspot analysis
2. `phase-1-scope` - Scope definition
3. `phase-1-5-boundary` - Scope boundary validation
4. `phase-2-architecture` - Architecture planning
5. `phase-3-audit` - DNA & PR audit
6. `phase-4-tickets` - Ticket generation
7. `phase-5-execute` - Ticket execution (delegates to Bob CLI)
8. `phase-5-verify` - Verification
9. `phase-6-review` - Final review

## Helper Scripts Available

- `scripts/wave_coordinator.py` - Wave coordinator (not needed for manual execution)
- `scripts/find_high_complexity_epics.py` - Find high-complexity epics
- `scripts/check_completed_epics_in_workers.py` - Check worker completion

## Estimated Metrics

### Time
- **Phase 0**: ~2 minutes (10 epics)
- **Phase 1**: ~2 minutes (10 epics)
- **Phase 1.5**: ~2 minutes (10 epics)
- **Phase 2**: ~3 minutes (10 epics)
- **Phase 3**: ~3 minutes (10 epics)
- **Phase 4**: ~2 minutes (10 epics)
- **Phase 5**: ~10 minutes (10 epics, includes Bob CLI)
- **Phase 5.5**: ~3 minutes (10 epics)
- **Phase 6**: ~2 minutes (10 epics)
- **Total**: ~29 minutes

### Cost
- **Per Epic**: ~45 BobCoins
- **Total**: ~450 BobCoins (10 epics)
- **Current Budget**: Check BobCoin balance

### MCP Calls
- **Total**: 90 calls (10 epics × 9 phases)

## Important Notes

### Target CYC = 8 (Not 15)
- The 15 threshold is stale documentation
- Ultimate goal: CYC ≤ 8 across entire codebase
- This is the first iteration to reach 8

### CodeScene Primary
- Use CodeScene for complexity metrics (not jCodemunch)
- jCodemunch is supplemental for code navigation, blast radius

### Sequential Phases, Parallel Epics
- Process all 10 epics through Phase 0
- Then all 10 epics through Phase 1
- Continue through all 9 phases
- **NOT** one epic through all phases, then next epic

## Success Criteria

### Per Phase
- ✅ All 10 epics processed through the phase
- ✅ Manifests updated for each epic
- ✅ Output artifacts created

### Wave 2 Overall
- ✅ All 10 epics reduced to CYC ≤ 8
- ✅ Build passes (no compilation errors)
- ✅ All tests pass
- ✅ Quality gates passed
- ✅ 90 MCP calls completed

## Your Task

**Start with Phase 0**: Execute Phase 0 for all 10 epics (10 MCP tool calls).

After Phase 0 completes, I will guide you through Phase 1, then Phase 1.5, etc.

**When you're ready, respond**: "Ready to execute Wave 2 Phase 0 for 10 epics. Starting with EPIC-CCN-164."

---

**Reference Documents**:
- `docs/workflow/WAVE_EXECUTION_GUIDE.md` - Complete wave execution guide
- `docs/workflow/WAVE_2_EXECUTION_PLAN.md` - Wave 2 plan
- Previous session cost: $22.77

**Ready to start Wave 2!** 🚀