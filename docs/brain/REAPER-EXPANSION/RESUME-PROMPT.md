# REAPER-EXPANSION Phase 2.3 - Resume Prompt (Post-VSCode Restart)

**Copy and paste this entire prompt into a new Advanced mode session after restarting VSCode:**

---

## Context Resume

**Epic**: REAPER-EXPANSION Phase 2.3 - Greptile Sentinel Audit  
**Previous Session**: Greptile MCP configuration fixed (wrong file location)  
**Status**: VSCode restarted, ready to validate Greptile and execute Sentinel Audit

## What Was Fixed

The Greptile MCP server wasn't loading because the configuration was in the project root `.mcp.json` instead of the VS Code user directory. Fixed by creating:
```
C:\Users\Mohammed Khalid\AppData\Roaming\Code\User\mcp.json
```

Configuration now includes both jCodemunch (stdio) and Greptile (HTTP) servers.

## Your Task

**Phase 2.3: Greptile Sentinel Audit Execution**

1. **Verify Greptile MCP is available**:
   - Check that `greptile` appears in available MCP servers (alongside jcodemunch and sequential-thinking)
   - Confirm all 11 Greptile tools are enabled

2. **Execute 4 Sentinel Audit Queries** using Greptile MCP:

   **Query 1 - SIMA Queue Safety**:
   ```
   What are the current safety gaps in SIMA dispatch queue management? Focus on unbounded growth and OOM risks.
   ```

   **Query 2 - Counter Synchronization** (P0 BLOCKING BUG):
   ```
   Find all usages of _pendingFleetDispatches and _pendingFleetDispatchCount. Does any code path dequeue without updating the count?
   ```
   
   **Critical Bug Context**: [`src/V12_002.SIMA.Fleet.cs:240`](src/V12_002.SIMA.Fleet.cs:240) - The `TryDequeue` at line 240 does NOT decrement `_pendingFleetDispatchCount`, causing counter drift.

   **Query 3 - IPC Circuit Breaker Patterns**:
   ```
   Review V12_002.UI.IPC.cs for existing (but unused) circuit breaker or rate-limiting patterns.
   ```

   **Query 4 - Entry Quantity Clamping Surface**:
   ```
   Locate all entry methods in src/Entries.*.cs that accept a 'contracts' or 'quantity' parameter to verify our clamping surface.
   ```

3. **Compare Greptile findings with jCodemunch results**:
   - jCodemunch already found 9 usages of `_pendingFleetDispatches` across 4 files
   - Validate if Greptile provides additional context or catches issues jCodemunch missed

4. **Update the report**:
   - Add Greptile validation results to [`docs/brain/REAPER-EXPANSION/02-greptile-report.md`](docs/brain/REAPER-EXPANSION/02-greptile-report.md)
   - Document any discrepancies between Greptile and jCodemunch findings

5. **Proceed to Phase 4 (TICKETS)**:
   - Once Sentinel Audit is complete, generate implementation tickets
   - Priority: P0 counter synchronization bug MUST be fixed first

## Reference Documents

- [`docs/brain/REAPER-EXPANSION/00-scope.md`](docs/brain/REAPER-EXPANSION/00-scope.md) - Original scope (12 safety gaps)
- [`docs/brain/REAPER-EXPANSION/01-analysis.md`](docs/brain/REAPER-EXPANSION/01-analysis.md) - Initial analysis
- [`docs/brain/REAPER-EXPANSION/02-approach.md`](docs/brain/REAPER-EXPANSION/02-approach.md) - Implementation approach (3 modules)
- [`docs/brain/REAPER-EXPANSION/02-greptile-report.md`](docs/brain/REAPER-EXPANSION/02-greptile-report.md) - Greptile fix log + jCodemunch findings
- [`docs/brain/REAPER-EXPANSION/GREPTILE-FIX-SUMMARY.md`](docs/brain/REAPER-EXPANSION/GREPTILE-FIX-SUMMARY.md) - Configuration fix summary

## Critical Bug Alert

**P0 BLOCKING**: [`src/V12_002.SIMA.Fleet.cs:240`](src/V12_002.SIMA.Fleet.cs:240)

```csharp
// Line 240: TryDequeue WITHOUT counter decrement
if (!_pendingFleetDispatches.TryDequeue(out req))
    return;
```

This causes `_pendingFleetDispatchCount` to drift, leading to:
- Infinite pump cycles (queue appears non-empty when it's actually empty)
- OOM risk from unbounded queue growth
- Performance degradation from unnecessary pump invocations

**Must be fixed before any other REAPER-EXPANSION work.**

## Expected Outcome

After this session:
- ✅ Greptile MCP validated and working
- ✅ All 4 Sentinel Audit queries executed
- ✅ Findings documented in 02-greptile-report.md
- ✅ Ready to generate Phase 4 implementation tickets
- ✅ P0 counter bug prioritized for immediate fix

---

**Start by verifying Greptile MCP is available, then execute Query 1.**