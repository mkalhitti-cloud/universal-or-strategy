# Wave 2 Execution Plan - High-Complexity Epics

**Date**: 2026-06-11  
**Status**: READY TO START  
**Goal**: Execute all 9 phases for 10 high-complexity epics (CYC > 15)

---

## Wave 1 Summary (Completed)

**Result**: All 10 epics were NO-ACTION (CYC ≤ 15)
- EPIC-CCN-21 through EPIC-CCN-30
- Average complexity: 10.4
- No refactoring needed (already meet Jane Street threshold)

**Lesson Learned**: Pre-filter for CYC > 15 before starting wave

---

## Wave 2 Target Epics (Top 10 by Complexity)

| # | Epic ID | Method | File | Complexity | Priority |
|---|---------|--------|------|------------|----------|
| 1 | EPIC-CCN-164 | IsCommandForThisInstrument | src/V12_002.UI.IPC.cs | 36 | 🔴 CRITICAL |
| 2 | EPIC-CCN-107 | HydrateFromOpenPositions | src/V12_002.SIMA.Lifecycle.cs | 31 | 🔴 CRITICAL |
| 3 | EPIC-CCN-108 | SweepBrokerOrders | src/V12_002.SIMA.Lifecycle.cs | 24 | 🔴 HIGH |
| 4 | EPIC-CCN-32 | HandleTerminated | src/V12_002.Lifecycle.cs | 23 | 🔴 HIGH |
| 5 | EPIC-CCN-109 | HydrateWorkingOrdersFromBroker | src/V12_002.SIMA.Lifecycle.cs | 19 | 🟡 MEDIUM |
| 6 | EPIC-CCN-110 | AdoptMasterOrders | src/V12_002.SIMA.Lifecycle.cs | 19 | 🟡 MEDIUM |
| 7 | EPIC-CCN-155 | TryHandleFleetCommand | src/V12_002.UI.IPC.Commands.Fleet.cs | 19 | 🟡 MEDIUM |
| 8 | EPIC-CCN-98 | ProcessFlattenWorkItem_CancelOrders | src/V12_002.SIMA.Flatten.cs | 18 | 🟡 MEDIUM |
| 9 | EPIC-CCN-128 | SymmetryGuardReplaceExistingFollowerTarget | src/V12_002.Symmetry.Replace.cs | 18 | 🟡 MEDIUM |
| 10 | EPIC-CCN-129 | SymmetryGuardTryResolveFollowersForDispatch | src/V12_002.Symmetry.Replace.cs | 18 | 🟡 MEDIUM |

**Total Epics**: 10  
**Average Complexity**: 23.2 (2.3x higher than Wave 1)  
**Complexity Range**: 18-36

---

## Full 9-Phase Pipeline

### Phase 0: Hotspot Analysis
- **Tool**: `phase-0-hotspot` MCP server
- **Output**: `00-hotspots.md` + `manifest.json`
- **Time**: ~2 minutes (10 epics)

### Phase 1: Scope Definition
- **Tool**: `phase-1-scope` MCP server
- **Output**: `00-scope.md`
- **Decision**: Action required (YES/NO)
- **Time**: ~2 minutes (10 epics)

### Phase 1.5: Scope Boundary Validation
- **Tool**: `phase-1-5-boundary` MCP server
- **Output**: `01-scope-boundary.md`
- **Validation**: Single-method rule enforcement
- **Time**: ~2 minutes (10 epics)

### Phase 2: Architecture Planning
- **Tool**: `phase-2-architecture` MCP server
- **Output**: `02-architecture-plan.md` + `02-diagrams.mmd`
- **Content**: Method signatures, call graphs, Jane Street compliance
- **Time**: ~3 minutes (10 epics)

### Phase 3: DNA & PR Audit
- **Tool**: `phase-3-audit` MCP server
- **Output**: `03-audit-report.md`
- **Checks**: V12 DNA compliance, PR hygiene, pre-flight safety
- **Time**: ~3 minutes (10 epics)

### Phase 4: Ticket Generation
- **Tool**: `phase-4-tickets` MCP server
- **Output**: `04-tickets.md`
- **Content**: Surgical extraction tickets (1-4 per epic)
- **Time**: ~2 minutes (10 epics)

### Phase 5: Ticket Execution
- **Tool**: `phase-5-execute` MCP server
- **Delegation**: Bob CLI (`v12-engineer`) for actual code work
- **Output**: `ticket-X-completion.md` per ticket
- **Time**: ~5-10 minutes (10 epics, ~20-40 tickets)

### Phase 5.5: Verification
- **Tool**: `phase-5-verify` MCP server
- **Output**: `ticket-X-verification.md` per ticket
- **Checks**: Complexity targets met, quality gates passed
- **Time**: ~3 minutes (10 epics)

### Phase 6: Final Review
- **Tool**: `phase-6-review` MCP server
- **Output**: `05-completion-report.md`
- **Content**: Final review, completion report, roadmap update
- **Time**: ~2 minutes (10 epics)

---

## Estimated Metrics

### Time Projection
- **Phase 0**: 2 min
- **Phase 1**: 2 min
- **Phase 1.5**: 2 min
- **Phase 2**: 3 min
- **Phase 3**: 3 min
- **Phase 4**: 2 min
- **Phase 5**: 10 min (includes Bob CLI execution)
- **Phase 5.5**: 3 min
- **Phase 6**: 2 min
- **Total**: ~29 minutes for 10 epics

### Cost Projection
- **Phases 0-1**: 8.6 BobCoins (validated in Wave 1)
- **Phases 1.5-6**: ~30 BobCoins (estimated)
- **Total**: ~38.6 BobCoins for 10 epics
- **Current Balance**: 149.45 BobCoins
- **Remaining After Wave 2**: ~110.85 BobCoins

### Artifact Projection
- **Per Epic**: 15 files (9 phase outputs + 6 manifests)
- **Total**: 150 files for 10 epics
- **Plus**: ~20-40 ticket completion/verification docs

---

## Success Criteria

### Per Epic
- ✅ All 9 phases completed
- ✅ Complexity reduced to ≤ 15 (Jane Street threshold)
- ✅ Build passes (no compilation errors)
- ✅ All tests pass
- ✅ Quality gates passed
- ✅ Manifest updated for each phase

### Wave 2 Overall
- ✅ 10 high-complexity epics processed
- ✅ Average complexity reduced from 23.2 to ≤ 15
- ✅ Full pipeline validated end-to-end
- ✅ Performance metrics collected
- ✅ Scalability validated

---

## Risk Assessment

### High-Risk Epics
1. **EPIC-CCN-164** (CYC=36): IsCommandForThisInstrument
   - Risk: Very high complexity, likely needs 3-4 tickets
   - Mitigation: Extra validation in Phase 3

2. **EPIC-CCN-107** (CYC=31): HydrateFromOpenPositions
   - Risk: High complexity, SIMA lifecycle critical path
   - Mitigation: Careful blast radius analysis

3. **EPIC-CCN-108** (CYC=24): SweepBrokerOrders
   - Risk: High complexity, broker integration
   - Mitigation: Extra testing in Phase 5.5

### Medium-Risk Epics
- EPIC-CCN-32, 109, 110, 155, 98, 128, 129
- Risk: Moderate complexity (18-23)
- Mitigation: Standard validation workflow

---

## Execution Strategy

### Sequential Phases (Recommended)
Process all 10 epics through each phase before moving to next phase:
1. Phase 0 for all 10 epics
2. Phase 1 for all 10 epics
3. Phase 1.5 for all 10 epics
4. Continue through Phase 6

**Advantages**:
- Clear progress tracking
- Easy to pause/resume
- Consistent artifact generation
- No race conditions

### Parallel Execution (Not Recommended)
Process multiple epics through all phases simultaneously.

**Disadvantages**:
- Complex coordination
- Harder to track progress
- Potential race conditions
- Context switching overhead

---

## Next Steps

### 1. Start Wave 2 Phase 0
Execute Phase 0 (Hotspot Analysis) for all 10 epics:

```
use_mcp_tool("phase-0-hotspot", "execute_phase_0", {
  "epic_id": "EPIC-CCN-164",
  "method": "IsCommandForThisInstrument",
  "file": "src/V12_002.UI.IPC.cs",
  "cyc": 36,
  "jcodemunch_data": {...}
})
```

Repeat for EPIC-CCN-107, 108, 32, 109, 110, 155, 98, 128, 129.

### 2. Continue Through All Phases
After Phase 0 completes for all 10 epics, proceed to Phase 1, then Phase 1.5, etc.

### 3. Generate Completion Report
After Phase 6, generate comprehensive Wave 2 completion report with:
- Performance metrics
- Complexity reduction achieved
- Quality gate results
- Lessons learned
- Recommendations for Wave 3

---

## Wave 3+ Planning

After Wave 2 completes, we have 12 more high-complexity epics (CYC > 15):
- EPIC-CCN-72, 111, 112, 152, 170, 67, 73, 99, 144, and 3 more

**Strategy**: Continue with waves of 10-20 epics until all 22 high-complexity epics are complete.

---

**Ready to start Wave 2 Phase 0!** 🚀