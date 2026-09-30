# V12 Epic Workflow Phase Consolidation Analysis

**Date**: 2026-06-12
**Context**: Review of 9-phase workflow vs Traycer's 6-phase workflow
**Goal**: Identify redundant phases and optimize for autonomous execution

## Current V12 Workflow (9 Phases)

### Phase Breakdown

| Phase | Command | Mode | Purpose | Duration | Artifacts |
|-------|---------|------|---------|----------|-----------|
| **0** | `epic-intake` | ask | Hotspot analysis via jCodemunch | 5 min | `00-hotspots.md` |
| **1** | `epic-scope-boundary --phase 1` | plan | Define scope from hotspots | 10 min | `00-scope.md` |
| **1.5** | `epic-scope-boundary --phase 1.5` | plan | Validate scope boundaries (V12.23 mandate) | 10 min | `01-scope-boundary.md` |
| **2** | `epic-plan` | plan | Architecture design + call graphs | 30 min | `02-architecture-plan.md`, `02-diagrams.mmd` |
| **3** | `epic-scan` | advanced | DNA compliance + PR hygiene audit | 15 min | `03-audit-report.md` |
| **4** | `epic-tickets` | plan | Generate surgical tickets from plan | 15 min | `04-tickets.md` |
| **5.X** | `epic-validate --ticket X` | v12-engineer | Execute ticket (Bob CLI) | 30 min/ticket | `ticket-X-completion.md` |
| **5.X.V** | `epic-verify-ticket --ticket X` | advanced | Verify ticket (build, tests, complexity) | 10 min/ticket | `ticket-X-verification.md` |
| **6** | `epic-review-final` | advanced | Final review + completion report | 20 min | `05-completion-report.md` |

**Total**: 9 distinct phases (11+ including per-ticket phases)

## Traycer Workflow (6 Phases)

From the image, Traycer's workflow:

| Phase | Purpose |
|-------|---------|
| **trigger-workflow** | Entry point - understand code area |
| **plan-refactor** | Build shared understanding before planning |
| **architecture-validation** | Validate architectural approach |
| **ticket-breakdown** | Break into executable tickets |
| **execute** | Implement changes |
| **verification** | Verify implementation |

**Total**: 6 phases

## Redundancy Analysis

### Potential Consolidations

#### 1. **Phase 0 + Phase 1** → **Phase 0: Intake & Scope**
**Current**:
- Phase 0: Hotspot analysis (jCodemunch)
- Phase 1: Scope definition from hotspots

**Proposed**: Merge into single phase
- **Rationale**: Both are analysis/planning, no code changes
- **Mode**: `plan` (can use jCodemunch MCP in plan mode)
- **Output**: `00-intake-and-scope.md` (combined)
- **Duration**: 15 min (saves 5 min)

**Verdict**: ✅ **MERGE** - Natural handoff, same mode, sequential dependency

#### 2. **Phase 1.5: Scope Boundary** → Keep Separate
**Current**: Standalone validation gate (V12.23 Protocol)

**Analysis**:
- Added after EPIC-13 PR #12 failure (scope creep)
- MANDATORY gate to prevent multi-concern PRs
- Critical for autonomous execution (prevents runaway scope)

**Verdict**: ❌ **KEEP SEPARATE** - Critical safety gate, prevents $0.47 wasted VM costs

#### 3. **Phase 2 + Phase 3** → **Phase 2: Architecture & Validation**
**Current**:
- Phase 2: Architecture planning (plan mode)
- Phase 3: DNA & PR audit (advanced mode)

**Analysis**:
- Phase 2 creates plan
- Phase 3 validates plan against DNA principles
- Different modes (plan vs advanced)
- Phase 3 requires MCP tools (jCodemunch, graphify)

**Proposed**: Keep separate BUT streamline
- Phase 2: Architecture design only
- Phase 3: Automated validation (no manual review needed)

**Verdict**: ❌ **KEEP SEPARATE** - Different modes, MCP dependency, but automate Phase 3

#### 4. **Phase 4: Ticket Generation** → Keep Separate
**Current**: Standalone ticket breakdown

**Analysis**:
- Critical output for Phase 5 parallelization
- Determines ticket dependencies
- Enables autonomous ticket execution

**Verdict**: ❌ **KEEP SEPARATE** - Enables parallel execution

#### 5. **Phase 5.X.V: Per-Ticket Verification** → **Redundant?**
**Current**: Verify each ticket individually before proceeding

**Analysis**:
- **Pro**: Catches issues early (fail fast)
- **Pro**: Prevents cascading failures
- **Con**: Adds 10 min per ticket overhead
- **Con**: Phase 6 (Final Review) does comprehensive verification anyway

**Question**: Is per-ticket verification redundant with final review?

**Traycer Approach**: Single verification phase at end

**Verdict**: ⚠️ **CONDITIONAL** - Keep for multi-ticket epics (>3 tickets), skip for single-ticket epics

#### 6. **Phase 6: Final Review** → Keep Separate
**Current**: Comprehensive final verification

**Analysis**:
- Verifies all tickets together
- Checks for integration issues
- Generates completion report
- Required for autonomous execution

**Verdict**: ❌ **KEEP SEPARATE** - Essential final gate

## Proposed Consolidated Workflow (7 Phases)

### Option A: Aggressive Consolidation (7 Phases)

| Phase | Command | Mode | Purpose | Duration | Change |
|-------|---------|------|---------|----------|--------|
| **0** | `epic-intake` | plan | Hotspot analysis + scope definition | 15 min | ✅ Merged 0+1 |
| **1** | `epic-scope-boundary` | plan | Scope boundary validation (V12.23) | 10 min | Renumbered |
| **2** | `epic-plan` | plan | Architecture design + call graphs | 30 min | Unchanged |
| **3** | `epic-scan` | advanced | Automated DNA/PR audit | 10 min | ✅ Automated |
| **4** | `epic-tickets` | plan | Generate surgical tickets | 15 min | Unchanged |
| **5.X** | `epic-validate --ticket X` | v12-engineer | Execute ticket | 30 min/ticket | Unchanged |
| **6** | `epic-review-final` | advanced | Final verification + report | 20 min | ✅ Absorbs 5.X.V |

**Total**: 7 phases (9 including per-ticket)
**Savings**: 15 min per epic (10 min per ticket for verification)

### Option B: Conservative Consolidation (8 Phases)

Keep per-ticket verification for multi-ticket epics:

| Phase | Command | Mode | Purpose | Duration | Change |
|-------|---------|------|---------|----------|--------|
| **0** | `epic-intake` | plan | Hotspot analysis + scope definition | 15 min | ✅ Merged 0+1 |
| **1** | `epic-scope-boundary` | plan | Scope boundary validation (V12.23) | 10 min | Renumbered |
| **2** | `epic-plan` | plan | Architecture design + call graphs | 30 min | Unchanged |
| **3** | `epic-scan` | advanced | Automated DNA/PR audit | 10 min | ✅ Automated |
| **4** | `epic-tickets` | plan | Generate surgical tickets | 15 min | Unchanged |
| **5.X** | `epic-validate --ticket X` | v12-engineer | Execute ticket | 30 min/ticket | Unchanged |
| **5.X.V** | `epic-verify-ticket --ticket X` | advanced | Verify ticket (if >3 tickets) | 10 min/ticket | ⚠️ Conditional |
| **6** | `epic-review-final` | advanced | Final verification + report | 20 min | Unchanged |

**Total**: 8 phases (10+ including per-ticket)
**Savings**: 5 min per epic

## Comparison: Traycer vs V12

### Traycer Workflow (6 phases)
```
trigger-workflow (intake)
  ↓
plan-refactor (scope + architecture)
  ↓
architecture-validation (DNA audit)
  ↓
ticket-breakdown (tickets)
  ↓
execute (implementation)
  ↓
verification (final review)
```

### V12 Current (9 phases)
```
epic-intake (hotspots)
  ↓
epic-scope-boundary --phase 1 (scope)
  ↓
epic-scope-boundary --phase 1.5 (boundary validation) ← V12.23 addition
  ↓
epic-plan (architecture)
  ↓
epic-scan (DNA audit)
  ↓
epic-tickets (tickets)
  ↓
epic-validate (execute)
  ↓
epic-verify-ticket (per-ticket verification) ← V12 addition
  ↓
epic-review-final (final review)
```

### V12 Proposed Option A (7 phases)
```
epic-intake (hotspots + scope) ← MERGED
  ↓
epic-scope-boundary (boundary validation)
  ↓
epic-plan (architecture)
  ↓
epic-scan (automated DNA audit) ← AUTOMATED
  ↓
epic-tickets (tickets)
  ↓
epic-validate (execute)
  ↓
epic-review-final (final review + per-ticket verification) ← ABSORBED
```

## Key Differences: V12 vs Traycer

### V12 Additions (Not in Traycer)

1. **Phase 1.5: Scope Boundary Validation**
   - **Why**: Prevents scope creep (V12.23 Protocol)
   - **Trigger**: EPIC-13 PR #12 failure (320 compilation errors)
   - **Value**: Critical for autonomous execution
   - **Keep**: ✅ YES

2. **Phase 5.X.V: Per-Ticket Verification**
   - **Why**: Fail-fast for multi-ticket epics
   - **Trigger**: Wanted early detection of ticket issues
   - **Value**: Debatable (Phase 6 does comprehensive verification)
   - **Keep**: ⚠️ CONDITIONAL (only for >3 tickets)

### Traycer Simplifications (Not in V12)

1. **Combined Intake + Scope**
   - Traycer: Single "plan-refactor" phase
   - V12: Separate hotspot analysis + scope definition
   - **Recommendation**: ✅ MERGE (saves 5 min, same mode)

2. **Single Verification Phase**
   - Traycer: One verification at end
   - V12: Per-ticket + final verification
   - **Recommendation**: ✅ ADOPT (saves 10 min per ticket)

## Recommendations

### Short-Term (Wave 2 Launch)

**Use Current 9-Phase Workflow**
- ✅ Already implemented and tested
- ✅ EPIC-CCN-164 has Phase 1 complete
- ✅ No refactoring needed for immediate launch
- ✅ Proven with EPIC-13 (despite failure, workflow worked)

**Rationale**: Don't optimize prematurely. Launch Wave 2 with current workflow, collect data, then optimize.

### Medium-Term (After Wave 2 Data)

**Implement Option A (7 Phases)**
- ✅ Merge Phase 0 + Phase 1 (saves 5 min per epic)
- ✅ Automate Phase 3 (DNA audit) - no manual review
- ✅ Absorb Phase 5.X.V into Phase 6 (saves 10 min per ticket)
- ✅ Keep Phase 1.5 (scope boundary) - critical safety gate

**Expected Savings**:
- Single-ticket epic: 15 min (5 min intake + 10 min verification)
- 3-ticket epic: 45 min (5 min intake + 30 min verification)
- 165-epic roadmap: 41 hours → 36 hours (12% faster)

### Long-Term (Production Optimization)

**Adaptive Verification Strategy**
```python
if ticket_count == 1:
    skip_per_ticket_verification = True
elif ticket_count <= 3:
    per_ticket_verification = "build_only"  # Fast check
else:
    per_ticket_verification = "full"  # Comprehensive
```

**Parallel Phase Execution**
- Phase 0 (intake) can run in parallel with Phase 2.3 (test strategy) if test strategy doesn't depend on hotspots
- Phase 5.1, 5.2, ..., 5.N (tickets) can run in parallel if independent
- Phase 5.1.V, 5.2.V, ..., 5.N.V (verifications) can run in parallel

## Big Picture: Long-Term Workflow Strategy

### Current State (V12.25)
- **Phases**: 9 (11+ with per-ticket)
- **Parallelization**: Tickets only (Phase 5.X)
- **Automation**: Partial (manual gates at Phase 1.5, 2, 3)
- **Verification**: Redundant (per-ticket + final)

### Target State (V12.30)
- **Phases**: 7 (9 with per-ticket)
- **Parallelization**: Tickets + verifications
- **Automation**: Full (automated gates, manual override only)
- **Verification**: Adaptive (conditional per-ticket)

### Alignment with Traycer
- ✅ **Intake + Scope**: Merged (like Traycer's "plan-refactor")
- ✅ **Architecture + Validation**: Separate but automated
- ✅ **Ticket Breakdown**: Identical
- ✅ **Execute**: Identical (Bob CLI)
- ⚠️ **Verification**: V12 has extra per-ticket gate (conditional)
- ✅ **Scope Boundary**: V12-specific (prevents scope creep)

### V12-Specific Innovations
1. **Scope Boundary Gate** (Phase 1.5) - Prevents scope creep
2. **Manifest-Based State** - Enables resume from any phase
3. **Parallel Ticket Execution** - Faster than Traycer's sequential
4. **Adaptive Verification** - Conditional per-ticket checks

## Decision Matrix

| Criterion | Current (9) | Option A (7) | Option B (8) |
|-----------|-------------|--------------|--------------|
| **Simplicity** | ❌ Complex | ✅ Simpler | ⚠️ Moderate |
| **Safety** | ✅ Redundant | ⚠️ Less redundant | ✅ Redundant |
| **Speed** | ❌ Slower | ✅ Faster | ⚠️ Moderate |
| **Traycer Alignment** | ❌ Divergent | ✅ Aligned | ⚠️ Partial |
| **Autonomous Ready** | ✅ Yes | ✅ Yes | ✅ Yes |
| **Wave 2 Ready** | ✅ Yes | ❌ Needs refactoring | ❌ Needs refactoring |

## Final Recommendation

### For Wave 2 Launch (Immediate)
**Use Current 9-Phase Workflow** ✅
- Already implemented
- EPIC-CCN-164 ready to test
- No refactoring risk
- Collect performance data

### For Wave 3+ (After Data Collection)
**Implement Option A (7 Phases)** ✅
- Merge Phase 0 + Phase 1
- Automate Phase 3 (DNA audit)
- Absorb Phase 5.X.V into Phase 6
- Keep Phase 1.5 (scope boundary)

### For Production (Long-Term)
**Adaptive Workflow** ✅
- Conditional per-ticket verification (based on ticket count)
- Parallel ticket execution + verification
- Automated gates with manual override
- Manifest-based resume from any phase

## Action Items

### Immediate (Before Wave 2)
1. ✅ Use current 9-phase workflow
2. ✅ Test full epic on VM (EPIC-CCN-164)
3. ✅ Launch Wave 2 with current workflow
4. ✅ Collect performance metrics

### Short-Term (After Wave 2)
1. Analyze Wave 2 performance data
2. Identify bottleneck phases
3. Prototype Option A (7 phases)
4. Test with EPIC-CCN-165

### Long-Term (Production)
1. Implement adaptive verification
2. Enable parallel phase execution
3. Automate all gates (manual override only)
4. Integrate with Watsonx Orchestrate

---

**Status**: Analysis Complete
**Recommendation**: Launch Wave 2 with current 9-phase workflow, optimize to 7 phases after data collection
**Next Action**: Run full epic test on VM to validate current workflow