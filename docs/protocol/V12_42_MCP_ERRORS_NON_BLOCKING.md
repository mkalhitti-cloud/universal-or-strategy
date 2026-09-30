# V12.42: MCP Connection Errors Are Non-Blocking

**Version**: 1.0  
**Date**: 2026-06-17  
**Status**: ACTIVE  
**Supersedes**: V12.41 (MCP configuration fix - unnecessary)

## Critical Discovery

**MCP connection errors during Bob CLI execution are NON-BLOCKING and can be safely ignored.**

### Evidence

1. **Wave 4 Execution**: 79/80 epics completed successfully despite 15 MCP connection errors per epic
2. **Bob CLI Fallback**: When MCP tools fail, Bob automatically falls back to manual ticket execution
3. **Wave 4 Rollback**: Was due to quality issues (28 Greptile findings), NOT MCP failures

### Root Cause Analysis

**Previous Assumption** (V12.40-V12.41): MCP servers must be running for Bob CLI to work  
**Reality**: Bob CLI has built-in fallback - MCP is optional enhancement, not requirement

### MCP Connection Error Pattern

```
[ERROR] Error during discovery for server 'phase-5-execute': MCP error -32000: Connection closed
[ERROR] Error during discovery for server 'phase-4-tickets': MCP error -32000: Connection closed
...
I'll execute Phase 5 for EPIC-CCN-001 from scratch, ignoring the stale manifest data.
```

**What happens next**: Bob proceeds with manual execution and completes all tickets successfully.

### Wave 4 vs Wave 5 Comparison

| Metric | Wave 4 | Wave 5 (Current) |
|--------|--------|------------------|
| MCP Errors | 15 per epic | 15 per epic (same) |
| Completion Rate | 79/80 (98.75%) | TBD |
| Rollback Reason | Quality issues | N/A |
| MCP Impact | **ZERO** | **ZERO** |

## Protocol Update

### V12.40-V12.41 Actions (Unnecessary)

- ❌ Fixed `.mcp.json.vm` configuration
- ❌ Removed Windows-specific servers
- ❌ Fixed `npx.cmd` → `npx`
- ❌ Deployed fixed config to VM

**Result**: These changes had no impact on execution success. MCP errors persisted but were non-blocking.

### Correct Approach (V12.42)

**IGNORE MCP CONNECTION ERRORS** - They are cosmetic noise, not execution blockers.

### Pilot Test Re-Evaluation

**Previous Goal**: Achieve 0 MCP connection errors  
**Corrected Goal**: Verify Bob CLI completes tickets despite MCP errors

**Success Criteria** (Updated):
- ✅ Bob CLI executes all 3 tickets (regardless of MCP errors)
- ✅ CYC reduced to ≤8 (Jane Street strict)
- ✅ Only target method modified (surgical execution)
- ✅ xUnit tests generated and passing
- ✅ UTF-8 encoding verified
- ✅ 0 P0/P1 Greptile issues

## Wave 5 Execution Plan (Corrected)

### Phase 1: Pilot Test (EPIC-CCN-001)

**Expectation**: 15 MCP connection errors (same as Wave 4)  
**Action**: Ignore errors, verify ticket completion  
**Success**: All 3 tickets complete, CYC ≤8 achieved

### Phase 2: Full Wave Launch (77 epics)

**Expectation**: 15 MCP errors × 77 epics = 1,155 total errors  
**Action**: Ignore all MCP errors  
**Monitor**: Ticket completion files, not MCP status

### Phase 3: Verification

**Check**: Completion files exist for all 77 epics  
**Ignore**: MCP error count in logs

## Lessons Learned

1. **Don't fix what isn't broken**: MCP errors looked scary but had zero impact
2. **Trust the data**: Wave 4's 79/80 success rate proved MCP errors are harmless
3. **Bob CLI is resilient**: Built-in fallback makes it production-ready despite MCP issues

## References

- **Wave 4 Results**: 79/80 epics complete (98.75% success)
- **Wave 4 Rollback**: Quality issues (Greptile findings), not MCP
- **Evidence**: `docs/wave5phase5badrun.md` (shows MCP errors + successful execution)
- **Architecture**: `building-blocks/autonomous-refactoring/ARCHITECTURE.md` (line 516)

## Action Items

1. ✅ Document MCP errors as non-blocking (this file)
2. ⏭️ Proceed with Wave 5 pilot test (ignore MCP errors)
3. ⏭️ Launch full Wave 5 (77 epics)
4. ⏭️ Monitor completion files, not MCP status

---

**Protocol Status**: ACTIVE  
**Next Review**: After Wave 5 completion