# REAPER-EXPANSION Phase 3 Handoff - EPIC-REAPER-CORE

**Generated**: 2026-05-22T20:20:00Z  
**Previous Phase**: Phase 2.3 (EPIC-QUALITY-DEBT) - ✅ COMPLETE  
**Current Phase**: Phase 3 (EPIC-REAPER-CORE) - 🔜 READY TO START

---

## Phase 2.3 Completion Summary

### ✅ Achievements

**P0 Circuit Breaker Fix** (commit 5322d67):
- Fixed Fleet.cs:240 counter synchronization bug
- Removed double-decrement on legacy path
- Verified by Cubic AI (0 issues on latest review)

**Technical Debt Baseline Established**:
- 2,891 Codacy issues documented
- 155 CodeFactor issues documented
- 138 code duplications tracked
- Boy Scout Rule enforcement strategy defined

**CI Infrastructure Hardened**:
- Codacy integration configured (.codacy.yml)
- Complexity threshold: 15 (Jane Street alignment)
- StyleCop failure documented (missing NinjaTrader DLLs)
- Codacy Coverage disabled (no test project)
- 2 workflows deferred to EPIC-CI-COMPILATION

**Documentation Created**:
- `docs/brain/EPIC-QUALITY-DEBT.md` - Technical debt tracking
- `docs/brain/REAPER-EXPANSION/03-pr-status-final.md` - PR status report
- `docs/protocol/CODEFACTOR_PROTOCOL.md` - CodeFactor safety protocol
- `docs/protocol/CODACY_COVERAGE_WORKFLOW.md` - Coverage workflow guide

**PR #1 Merged**: 
- PHS: 80% (20/25 checks passing with documented exceptions)
- Zero new technical debt introduced
- All security scans passing
- Squash merge to main (commit 09ecb58)

---

## Phase 3 Scope - EPIC-REAPER-CORE

### Objective
Implement comprehensive circuit breaker hardening across all SIMA dispatch entry points.

### Key Deliverables

1. **Complete Circuit Breaker Implementation**
   - Extend REAPER_MAX_PENDING_DISPATCHES checks to ALL dispatch entry points
   - Implement consistent rollback logic across all rejection paths
   - Add circuit breaker reset in `DrainAllDispatchQueuesOnAbort`

2. **State Rollback Hardening**
   - Fix incomplete rollback in `Dispatch_ProcessFleetLoop` (Cubic AI P1)
   - Ensure all dictionary registrations are cleaned up on rejection:
     - `activePositions`
     - `entryOrders` / `stopOrders`
     - Target dictionaries
     - `_followerBrackets`
   - Reset `registeredForCleanup` ref on rejection

3. **Entry Point Clamping**
   - Add `contracts` parameter validation in all `Entries.*.cs` methods
   - Clamp to REAPER_MAX_CONTRACTS_PER_ENTRY (default: 10)
   - Prevent unbounded position sizing at source

4. **IPC Circuit Breaker Integration**
   - Review `V12_002.UI.IPC.cs` for existing rate-limiting patterns
   - Integrate REAPER circuit breaker with IPC command handlers
   - Add backpressure signaling to UI clients

### Reference Documents

**Phase 2 Analysis**:
- `docs/brain/REAPER-EXPANSION/00-scope.md` - Original scope definition
- `docs/brain/REAPER-EXPANSION/01-analysis.md` - Sentinel Audit findings
- `docs/brain/REAPER-EXPANSION/02-approach.md` - Implementation approach
- `docs/brain/REAPER-EXPANSION/02-approach-sima.md` - SIMA-specific approach
- `docs/brain/REAPER-EXPANSION/02-approach-entries.md` - Entry point approach
- `docs/brain/REAPER-EXPANSION/02-approach-ipc.md` - IPC integration approach

**Phase 2 Validation**:
- `docs/brain/REAPER-EXPANSION/03-validation.md` - Validation criteria
- `docs/brain/REAPER-EXPANSION/03-pr-status-final.md` - PR #1 final status

**Technical Debt**:
- `docs/brain/EPIC-QUALITY-DEBT.md` - Baseline and strategy

### Known Issues to Address

**From Cubic AI Review** (commit a6d8ba3):
```
P1: Circuit breaker rollback is incomplete: dictionary registrations 
(activePositions, entryOrders, stopOrders, target dicts, _followerBrackets) 
committed before the check are not cleaned up on rejection. This leaves 
phantom tracked positions that REAPER will observe and attempt to repair. 
Additionally, registeredForCleanup ref is not reset, so the caller 
increments rmaCount as if the dispatch succeeded.

Location: src/V12_002.SIMA.Dispatch.cs:802
```

**From Codacy AI Review**:
1. Missing automated tests for circuit breaker trip/reset thresholds
2. Potential zero-allocation violation (string interpolation in hot path)
3. Clarify `DrainAllDispatchQueuesOnAbort` circuit breaker reset implementation

### Implementation Strategy

**Stage 1: Rollback Hardening** (P1 - BLOCKING)
1. Create helper method: `RollbackDispatchRegistrations()`
2. Call from both circuit breaker rejection branches
3. Ensure complete state cleanup:
   - Remove from `activePositions`
   - Remove from `entryOrders` / `stopOrders`
   - Remove from target dictionaries
   - Remove from `_followerBrackets`
   - Release `_poolSlotIndex`
   - Reset `_photonSideband`
   - Call `ClearDispatchSyncPending`
   - Rollback `AddExpectedPositionDeltaLocked`
   - Reset `registeredForCleanup` ref

**Stage 2: Entry Point Clamping** (P2)
1. Add `REAPER_MAX_CONTRACTS_PER_ENTRY` constant (default: 10)
2. Clamp `contracts` parameter in all entry methods:
   - `Entries.FFMA.cs`
   - `Entries.MOMO.cs`
   - `Entries.OR.cs`
   - `Entries.Retest.cs`
   - `Entries.RMA.cs`
   - `Entries.Trend.cs`
3. Log clamping events for audit trail

**Stage 3: Circuit Breaker Reset** (P3)
1. Add circuit breaker reset in `DrainAllDispatchQueuesOnAbort`
2. Ensure reset happens AFTER queue drain completes
3. Add structured log event for reset

**Stage 4: IPC Integration** (P4)
1. Review existing rate-limiting patterns in `V12_002.UI.IPC.cs`
2. Integrate REAPER circuit breaker state with IPC handlers
3. Add backpressure signaling to UI clients when tripped

### Testing Strategy (Deferred to Phase 4)

Phase 3 focuses on implementation. Automated tests will be added in Phase 4 (EPIC-REAPER-TESTS):
- Circuit breaker trip at exactly 1000 pending dispatches
- Circuit breaker reset at 800 pending dispatches
- Counter decrement on both Photon and legacy paths
- Full state rollback on rejection
- Circuit breaker reset after queue drain

### Success Criteria

**Code Quality**:
- ✅ Zero new Codacy issues introduced
- ✅ Zero new CodeFactor issues introduced
- ✅ Local lint passes with `-warnaserror`
- ✅ All security scans passing

**Functional**:
- ✅ P1 rollback issue resolved (Cubic AI verification)
- ✅ All entry points clamped to max contracts
- ✅ Circuit breaker reset implemented in drain path
- ✅ IPC integration complete

**Documentation**:
- ✅ Implementation plan documented
- ✅ Code changes documented with structured comments
- ✅ PR description includes validation checklist

**CI/CD**:
- ✅ PHS ≥ 80% with documented exceptions
- ✅ All bot reviews approved
- ✅ Build and tests passing

---

## Strategic Context

### 5-Epic REAPER-EXPANSION Sequence

1. ✅ **EPIC-QUALITY-DEBT** (Phase 2.3) - Baseline established, P0 fix verified
2. 🔜 **EPIC-REAPER-CORE** (Phase 3) - Circuit breaker hardening ← **YOU ARE HERE**
3. 🔜 **EPIC-REAPER-TESTS** (Phase 4) - Automated circuit breaker tests
4. 🔜 **EPIC-REAPER-PERF** (Phase 5) - Zero-allocation hot path optimization
5. 🔜 **EPIC-CI-COMPILATION** (Phase 6) - NinjaTrader in GitHub Actions

### V12 DNA Alignment

**Correctness by Construction**:
- Make illegal states unrepresentable
- Circuit breaker prevents unbounded queue growth at compile-time structure level

**Lock-Free Actor Pattern**:
- All state mutations via FSM/Actor `Enqueue` model
- No `lock(stateLock)` blocks (STRICTLY BANNED)

**ASCII-Only Compliance**:
- No Unicode, emoji, or curly quotes in C# string literals

**Jane Street Alignment**:
- Complexity threshold: 15 (cognitive simplicity)
- Functions with cyclomatic complexity >15 must be refactored

---

## Recommended Workflow

### Opening Move
1. Read this handoff document
2. Review Phase 2 analysis documents (00-scope.md, 01-analysis.md, 02-approach-*.md)
3. Use jCodemunch MCP to explore current implementation:
   ```
   resolve_repo { "path": "." }
   search_symbols { "repo": "universal-or-strategy", "query": "REAPER circuit breaker" }
   get_symbol_source { "repo": "universal-or-strategy", "symbol_id": "<from search>" }
   ```

### Implementation
1. Create feature branch: `feat/reaper-core-phase3`
2. Implement Stage 1 (Rollback Hardening) - P1 BLOCKING
3. Commit and push after each stage
4. Run local validation: `dotnet build Linting.csproj -warnaserror`
5. Verify hard-link integrity: `powershell -File .\deploy-sync.ps1`

### PR Creation
1. Use `generate_description_from_diff` to create PR description
2. Target PHS ≥ 80% with documented exceptions
3. Ensure all bot reviews approved before merge
4. Squash merge to main with descriptive commit message

---

## Key Files to Modify

**Primary**:
- `src/V12_002.SIMA.Dispatch.cs` - Rollback hardening, circuit breaker logic
- `src/V12_002.SIMA.Fleet.cs` - Circuit breaker reset in drain path
- `src/V12_002.Entries.*.cs` (6 files) - Entry point clamping
- `src/V12_002.UI.IPC.cs` - IPC integration

**Supporting**:
- `src/V12_002.Constants.cs` - Add REAPER_MAX_CONTRACTS_PER_ENTRY constant
- `src/V12_002.StructuredLog.cs` - Add log events for clamping and reset

---

## Deferred Items

**To Phase 4 (EPIC-REAPER-TESTS)**:
- Automated circuit breaker tests
- Trip/reset threshold validation
- State rollback verification tests

**To Phase 5 (EPIC-REAPER-PERF)**:
- Zero-allocation hot path optimization
- Replace string interpolation with non-allocating alternatives

**To Phase 6 (EPIC-CI-COMPILATION)**:
- Install NinjaTrader in GitHub Actions
- Enable StyleCop CI workflow
- Enable Codacy Coverage workflow

**To Backlog**:
- Rotate Greptile API token (exposed in git history)
- Remove exposed secrets from git history

---

## Contact Points

**Previous Phase Lead**: Orchestrator (Phase 2.3)  
**Current Phase Lead**: TBD (Phase 3)  
**Technical Debt Tracker**: `docs/brain/EPIC-QUALITY-DEBT.md`  
**PR Template**: Use `generate_description_from_diff` tool

---

## Final Notes

Phase 2.3 established a solid foundation:
- P0 bug fixed and verified
- Technical debt baseline documented
- CI infrastructure hardened
- Zero new debt introduced

Phase 3 builds on this foundation by completing the circuit breaker implementation across all entry points. Focus on correctness and completeness - performance optimization comes in Phase 5.

**Remember**: Boy Scout Rule applies - leave the code better than you found it, but don't introduce new technical debt.

Good luck with Phase 3! 🚀