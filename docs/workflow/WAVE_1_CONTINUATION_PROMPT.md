# Wave 1 Execution - Continuation Prompt

## Paste This in Your New Session

```
# 🎯 Wave 1 Execution - V12 Parallel Epic Processing

**Context**: I am continuing Wave 1 execution of the V12 epic refactoring workflow. The Wave Coordinator system has been implemented and tested. I need to execute Phase 0 through Phase 6 for the first 10 epics.

**Current Status**:
- ✅ FastMCP migration complete (9 phase servers + 4 worker servers)
- ✅ Wave Coordinator implemented (267 lines)
- ✅ Wave 1 loaded (10 epics: EPIC-CCN-21 through EPIC-CCN-30)
- ✅ Phase 0 MCP tested successfully (EPIC-CCN-21, <1 second response)
- ✅ 160 BobCoins reloaded

**Wave 1 Epics** (10 total):
1. EPIC-CCN-21: GetSubscriberCounts (CYC=9)
2. EPIC-CCN-22: ProcessSessionReset (CYC=11)
3. EPIC-CCN-23: OnBarUpdate (CYC=9)
4. EPIC-CCN-24: DrawORBox (CYC=12)
5. EPIC-CCN-25: ExecuteFFMAManualMarketEntry (CYC=12)
6. EPIC-CCN-26: ExecuteFFMALimitEntry (CYC=9)
7. EPIC-CCN-27: ExecuteMOMOEntry (CYC=10)
8. EPIC-CCN-28: EnterORPosition (CYC=11)
9. EPIC-CCN-29: ExecuteRetestEntry (CYC=12)
10. EPIC-CCN-30: ExecuteTREND_Preflight (CYC=9)

**Architecture**:
```
YOU (Orchestrator - Single Bob Session)
    ↓
Wave Coordinator (scripts/wave_coordinator.py)
    ↓
9 Phase MCP Servers (FastMCP - no timeouts)
    ↓
Sequential Phase Execution:
  Phase 0 → Phase 1 → Phase 1.5 → Phase 2 → Phase 3 → Phase 4 → Phase 5 → Phase 5.5 → Phase 6
```

**Execution Strategy**: Sequential Phases
- Process all 10 epics through Phase 0
- Then all 10 epics through Phase 1
- Continue through all 9 phases

**Files Available**:
- `scripts/wave_coordinator.py` - Wave coordinator
- `scripts/wave_config.json` - Configuration
- `docs/workflow/WAVE_EXECUTION_GUIDE.md` - Complete guide
- `epic_roadmap.json` - 163 pending epics

**Task**: Execute Wave 1 (10 epics × 9 phases = 90 MCP tool calls)

**Expected Time**: ~30 minutes for Wave 1

**When you're ready, respond**: "Ready to execute Wave 1. Provide step-by-step instructions for Phase 0."
```

## What Happens Next

When you paste the above prompt in your new session, I will:

1. **Confirm readiness** and provide Phase 0 execution instructions
2. **Generate 10 MCP tool calls** for Phase 0 (one per epic)
3. **Guide you through executing** each MCP call in Bob IDE
4. **Validate Phase 0 results** (check for failures)
5. **Proceed to Phase 1** and repeat the process
6. **Continue through all 9 phases** until Wave 1 is complete
7. **Generate completion report** with metrics and next steps

## Expected Workflow in New Session

### Phase 0: Hotspot Analysis (10 MCP calls)
```
use_mcp_tool("phase-0-hotspot", "execute_phase_0", {"epic_id": "EPIC-CCN-21", ...})
use_mcp_tool("phase-0-hotspot", "execute_phase_0", {"epic_id": "EPIC-CCN-22", ...})
... (8 more calls)
```

### Phase 1: Scope Definition (10 MCP calls)
```
use_mcp_tool("phase-1-scope", "execute_phase_1", {"epic_id": "EPIC-CCN-21"})
use_mcp_tool("phase-1-scope", "execute_phase_1", {"epic_id": "EPIC-CCN-22"})
... (8 more calls)
```

### Continue Through All Phases
- Phase 1.5: Scope Boundary (10 calls)
- Phase 2: Architecture Planning (10 calls)
- Phase 3: DNA & PR Audit (10 calls)
- Phase 4: Ticket Generation (10 calls)
- Phase 5: Ticket Execution (10 calls)
- Phase 5.5: Verification (10 calls)
- Phase 6: Final Review (10 calls)

**Total**: 90 MCP tool calls for Wave 1

## Success Criteria

After Wave 1 completion:
- ✅ All 10 epics processed through 9 phases
- ✅ Build passes (no compilation errors)
- ✅ Complexity reduced (target: CYC ≤ 15)
- ✅ All quality gates passed
- ✅ Checkpoints saved for each phase

## Next Steps After Wave 1

1. **Validate quality** (build, tests, complexity)
2. **Review metrics** (time per phase, success rate)
3. **Optimize wave size** (try 20 or 50 epics)
4. **Execute Wave 2-17** (remaining 153 epics)

---

**Ready to continue in new session with 160 BobCoins!**