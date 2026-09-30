# PR Review Cluster Strategy - Wave 4 Complexity Reduction

**Version**: 1.1
**Date**: 2026-06-16
**Status**: 🟢 READY FOR IMPLEMENTATION

## Overview

Wave 4 completed 79/80 epics (98.75%) with surgical complexity reduction across the V12 Photon Kernel. This document defines a cluster-based PR review strategy that groups related changes by architectural subsystem to enable focused, context-rich reviews.

## Architecture-Based Clustering

Based on [`docs/architecture.md`](../architecture.md), the V12 Photon Kernel is organized into 8 subsystems (S1-S8). Wave 4 epics targeted 7 subsystems (S1-S7). We'll create **one PR per modified subsystem** to maintain architectural coherence and enable domain-expert reviews.

**Note**: S8 (Photon Substrate IO) had no Wave 4 epics because all files were already optimized (<15 CYC). It is excluded from this PR strategy.

### Cluster Mapping

| Cluster | Subsystem | Files | Complexity (CYC) | Epic Count | Review Focus |
|---------|-----------|-------|------------------|------------|--------------|
| **PR-1** | S1: SIMA Core | 9 files | ~143 CYC | ~12 epics | Dispatch logic, fleet management, lifecycle |
| **PR-2** | S2: Execution Engine | 18 files | ~280 CYC | ~20 epics | Order callbacks, trailing stops, symmetry FSM |
| **PR-3** | S3: UI & Photon IO | 16 files | ~329 CYC | ~18 epics | IPC commands, panel handlers, UI callbacks |
| **PR-4** | S4: REAPER Defense | 7 files | ~99 CYC | ~8 epics | Audit, repair, naked stop detection |
| **PR-5** | S5: Kernel State | 8 files | ~72 CYC | ~7 epics | Sticky state, telemetry, lifecycle |
| **PR-6** | S6: Signals & Entries | 9 files | ~131 CYC | ~10 epics | Trend, OR, RMA, FFMA entry logic |
| **PR-7** | S7: Kernel Infrastructure | 12 files | ~45 CYC | ~4 epics | Main kernel, drawing helpers, ATM |

**Total**: 7 PRs covering 79 files, ~1,099 CYC reduced, 79 epics

**Excluded**: S8 (Photon Substrate IO) - No Wave 4 changes (already optimized)

## Cluster Definitions

### PR-1: S1 SIMA Core (~12 Epics)

**Files**:
- `V12_002.SIMA.cs`
- `V12_002.SIMA.Lifecycle.cs`
- `V12_002.SIMA.Dispatch.cs`
- `V12_002.SIMA.Fleet.cs`
- `V12_002.SIMA.Execution.cs`
- `V12_002.SIMA.Flatten.cs`
- `V12_002.SIMA.Shadow.cs`
- `V12_002.SIMA.Init.cs`
- `V12_002.SIMA.Constants.cs`

**Review Focus**:
- ✅ Dispatch logic correctness (entry routing)
- ✅ Fleet account filtering (multi-account safety)
- ✅ Lifecycle state transitions (FSM compliance)
- ✅ Shadow order synchronization
- ✅ Flatten logic (position closure)

**Complexity Targets**:
- `ExecuteSmartDispatchEntry`: 48 → <15 CYC
- `ShouldSkipFleetAccount`: 25 → <15 CYC
- `HydrateWorkingOrdersFromBroker`: 96 → <15 CYC

**Epic Examples**: EPIC-CCN-001, 002, 008, 011, 012, 019, 020, 028, 032, 043, 064, 077

---

### PR-2: S2 Execution Engine (~20 Epics)

**Files**:
- `V12_002.Orders.Callbacks.Execution.cs`
- `V12_002.Orders.Callbacks.AccountOrders.cs`
- `V12_002.Orders.Callbacks.Propagation.cs`
- `V12_002.Trailing.cs`
- `V12_002.Trailing.Breakeven.cs`
- `V12_002.Trailing.StopUpdate.cs`
- `V12_002.Symmetry.cs`
- `V12_002.Symmetry.BracketFSM.cs`
- `V12_002.Symmetry.Follower.cs`
- `V12_002.Symmetry.Replace.cs`
- `V12_002.Orders.Metadata.cs`
- `V12_002.Orders.Utils.cs`
- `V12_002.Orders.Callbacks.cs`
- `V12_002.Orders.CancelGateway.cs`
- `V12_002.Orders.Management.cs`
- `V12_002.Orders.Management.Cleanup.cs`
- `V12_002.Orders.Management.Flatten.cs`
- `V12_002.Orders.Management.StopSync.cs`

**Review Focus**:
- ✅ Order execution callbacks (fill handling)
- ✅ Trailing stop logic (breakeven, profit targets)
- ✅ Symmetry FSM (bracket state machine)
- ✅ Order propagation (fleet synchronization)
- ✅ Stop price validation (slippage protection)

**Complexity Targets**:
- `ProcessOnExecutionUpdate`: 35 → <15 CYC
- `ManageTrailingStops`: 42 → <15 CYC
- `ValidateStopPrice`: 33 → <15 CYC
- `TryFindOrderInPosition`: 25 → <15 CYC

**Epic Examples**: EPIC-CCN-003, 004, 005, 006, 007, 009, 010, 013, 014, 015, 016, 017, 018, 021, 022, 023, 024, 025, 026, 027

---

### PR-3: S3 UI & Photon IO (~18 Epics)

**Files**:
- `V12_002.UI.Callbacks.cs`
- `V12_002.UI.Compliance.cs`
- `V12_002.UI.IPC.cs`
- `V12_002.UI.IPC.Commands.Config.cs`
- `V12_002.UI.IPC.Commands.Fleet.cs`
- `V12_002.UI.IPC.Commands.Misc.cs`
- `V12_002.UI.IPC.Commands.Mode.cs`
- `V12_002.UI.IPC.Server.cs`
- `V12_002.UI.Panel.Construction.cs`
- `V12_002.UI.Panel.Handlers.cs`
- `V12_002.UI.Panel.Helpers.cs`
- `V12_002.UI.Panel.Lifecycle.cs`
- `V12_002.UI.Panel.StateSync.cs`
- `V12_002.UI.Sizing.cs`
- `V12_002.UI.Snapshot.cs`
- `V12_002.UI.Panel.Brushes.cs`

**Review Focus**:
- ✅ Keyboard callback handling (OnKeyDown)
- ✅ IPC command routing (config, fleet, mode)
- ✅ Panel event handlers (button clicks, state sync)
- ✅ UI compliance checks (parameter validation)
- ✅ Photon IO integration (ring buffer, MMIO)

**Complexity Targets**:
- `OnKeyDown`: 48 → <15 CYC
- `AttachPanelHandlers`: 39 → <15 CYC
- `ProcessIpc_MatchSymbol`: 38 → <15 CYC
- `UpdateContextualUI`: 32 → <15 CYC

**Epic Examples**: EPIC-CCN-029, 030, 031, 033, 034, 035, 036, 037, 038, 039, 040, 041, 042, 044, 045, 046, 047, 048

---

### PR-4: S4 REAPER Defense (~8 Epics)

**Files**:
- `V12_002.REAPER.Audit.cs`
- `V12_002.REAPER.Repair.cs`
- `V12_002.REAPER.cs`
- `V12_002.REAPER.NakedStop.cs`
- `V12_002.Safety.Watchdog.cs`
- `V12_002.Safety.Auth.cs`
- `V12_002.Safety.Limits.cs`

**Review Focus**:
- ✅ Naked stop detection (orphaned stops)
- ✅ Repair logic (auto-recovery)
- ✅ Audit trail (compliance logging)
- ✅ Safety limits (position size, drawdown)
- ✅ Watchdog timers (deadlock detection)

**Complexity Targets**:
- `AuditPositionIntegrity`: 28 → <15 CYC
- `RepairOrphanedStops`: 22 → <15 CYC

**Epic Examples**: EPIC-CCN-049, 050, 051, 052, 053, 054, 055, 056

---

### PR-5: S5 Kernel State (~7 Epics)

**Files**:
- `V12_002.StickyState.cs`
- `V12_002.Lifecycle.cs`
- `V12_002.Telemetry.cs`
- `V12_002.StructuredLog.cs`
- `V12_002.Properties.cs`
- `V12_002.Fields.cs`
- `V12_002.Methods.cs`
- `V12_002.Variables.cs`

**Review Focus**:
- ✅ Sticky state persistence (session recovery)
- ✅ Lifecycle hooks (OnStateChange, OnTermination)
- ✅ Telemetry emission (metrics, traces)
- ✅ Structured logging (JSON events)

**Complexity Targets**:
- `RestoreStickyState`: 20 → <15 CYC
- `EmitTelemetrySnapshot`: 18 → <15 CYC

**Epic Examples**: EPIC-CCN-057, 058, 059, 060, 061, 062, 063

---

### PR-6: S6 Signals & Entries (~10 Epics)

**Files**:
- `V12_002.Entries.Trend.cs`
- `V12_002.Entries.OR.cs`
- `V12_002.Entries.RMA.cs`
- `V12_002.Entries.FFMA.cs`
- `V12_002.Entries.Retest.cs`
- `V12_002.Entries.MOMO.cs`
- `V12_002.Signals.Indicators.cs`
- `V12_002.Signals.LogicFSM.cs`
- `V12_002.Signals.Utils.cs`

**Review Focus**:
- ✅ Entry signal generation (TREND, OR, RMA, FFMA)
- ✅ Indicator calculations (EMA, SMA, ATR)
- ✅ Logic FSM (signal state machine)
- ✅ Retest logic (pullback entries)
- ✅ MOMO entries (momentum breakouts)

**Complexity Targets**:
- `ExecuteTRENDEntry`: 120 → 10 CYC (already optimized)
- `ExecuteRMAEntry`: 35 → <15 CYC
- `ExecuteFFMAEntry`: 32 → <15 CYC

**Epic Examples**: EPIC-CCN-065, 066, 067, 068, 069, 070, 071, 072, 073, 074

---

### PR-7: S7 Kernel Infrastructure (~4 Epics)

**Files**:
- `V12_002.cs`
- `V12_002.Constants.cs`
- `V12_002.LogicAudit.cs`
- `V12_002.DrawingHelpers.cs`
- `V12_002.AccountUpdate.cs`
- `V12_002.BarUpdate.cs`
- `V12_002.Atm.cs`
- `V12_002.PureLogic.cs`
- `V12_002.Data.cs`
- `V12_002.PositionInfo.cs`
- `V12_002.Entries.cs`
- `SignalBroadcaster.cs`

**Review Focus**:
- ✅ Main kernel initialization
- ✅ Bar update logic (OnBarUpdate)
- ✅ Account update handling (OnAccountItemUpdate)
- ✅ Drawing helpers (chart annotations)
- ✅ ATM strategy integration

**Complexity Targets**:
- `OnBarUpdate`: 25 → <15 CYC
- `DrawingHelpers`: 28 → <15 CYC

**Epic Examples**: EPIC-CCN-075, 076, 078, 080

---


## PR Creation Workflow

### Step 1: Generate PR Branch per Cluster

```bash
# Example for PR-1 (S1 SIMA Core)
git checkout -b pr/wave4-s1-sima-core

# Cherry-pick commits for epics in this cluster
git cherry-pick <commit-hash-epic-001>
git cherry-pick <commit-hash-epic-002>
# ... (all S1 epics)

# Push branch
git push origin pr/wave4-s1-sima-core
```

### Step 2: Create GitHub PR with Cluster Template

**PR Title**: `refactor(S1): Wave 4 SIMA Core complexity reduction (12 epics)`

**PR Description Template**:
```markdown
## Cluster: S1 SIMA Core

**Epic Count**: 12  
**Files Changed**: 9  
**Complexity Reduced**: 143 CYC → <90 CYC  
**Wave**: 4 (79/80 complete)

### Epics Included
- EPIC-CCN-001: `ExecuteSmartDispatchEntry` extraction
- EPIC-CCN-002: `ShouldSkipFleetAccount` simplification
- EPIC-CCN-008: `HydrateWorkingOrdersFromBroker` decomposition
- ... (list all 12)

### Architectural Focus
This PR refactors the **SIMA Core** subsystem, which handles:
- Smart dispatch routing (entry signal → order execution)
- Fleet account management (multi-account filtering)
- Lifecycle state transitions (initialization, termination)
- Shadow order synchronization (broker state mirroring)

### Complexity Targets Met
| Method | Before | After | Status |
|--------|--------|-------|--------|
| `ExecuteSmartDispatchEntry` | 48 CYC | 12 CYC | ✅ |
| `ShouldSkipFleetAccount` | 25 CYC | 8 CYC | ✅ |
| `HydrateWorkingOrdersFromBroker` | 96 CYC | 3 CYC | ✅ |

### Testing
- ✅ All unit tests pass
- ✅ Build passes (zero errors)
- ✅ No behavioral changes (verified via Phase 5.V)
- ✅ Complexity audit: all methods ≤15 CYC

### Review Checklist
- [ ] Dispatch logic correctness
- [ ] Fleet filtering safety
- [ ] FSM state transitions
- [ ] Shadow sync integrity
- [ ] No lock() usage (lock-free mandate)
- [ ] ASCII-only strings
- [ ] Jane Street compliance

### Related Documentation
- Architecture: [`docs/architecture.md`](../architecture.md#s1-sima-core)
- Epic Roadmap: [`epic_roadmap.json`](../epic_roadmap.json)
- Wave 4 Report: [`WAVE4_FINAL_STATUS_SUMMARY.md`](../WAVE4_FINAL_STATUS_SUMMARY.md)
```

### Step 3: Assign Domain Experts

| Cluster | Primary Reviewer | Secondary Reviewer |
|---------|------------------|-------------------|
| PR-1 (S1) | SIMA/Dispatch expert | Architect |
| PR-2 (S2) | Order execution expert | Risk manager |
| PR-3 (S3) | UI/UX expert | IPC specialist |
| PR-4 (S4) | Safety/compliance expert | Architect |
| PR-5 (S5) | Telemetry expert | DevOps |
| PR-6 (S6) | Signal logic expert | Quant analyst |
| PR-7 (S7) | Kernel expert | Architect |

### Step 4: Review Sequence

**Parallel Review** (all 7 PRs can be reviewed simultaneously):
- Each PR is architecturally isolated
- No cross-cluster dependencies
- Reviewers can focus on domain expertise

**Merge Order** (after all reviews complete):
1. PR-7 (S7) - Kernel core (foundation)
2. PR-5 (S5) - State management
3. PR-1 (S1) - SIMA dispatch
4. PR-6 (S6) - Signal generation
5. PR-2 (S2) - Order execution
6. PR-4 (S4) - Safety layer
7. PR-3 (S3) - UI/IPC (depends on all others)

---

## Benefits of Cluster-Based Review

### 1. **Architectural Coherence**
- Each PR represents a single subsystem
- Reviewers can focus on domain-specific logic
- Easier to reason about changes in context

### 2. **Parallel Review**
- 8 PRs can be reviewed simultaneously
- No blocking dependencies between clusters
- Faster time-to-merge

### 3. **Domain Expertise**
- Assign reviewers based on subsystem knowledge
- Higher quality feedback
- Reduced review fatigue

### 4. **Focused Scope**
- 7 PRs instead of 1 monolithic or 79 individual PRs
- Optimal balance between granularity and manageability
- Each PR represents a cohesive architectural unit

### 5. **Rollback Granularity**
- If a cluster has issues, rollback only that PR
- Other clusters remain unaffected
- Surgical incident response

### 6. **Documentation Alignment**
- Each PR maps directly to architecture diagram
- Clear traceability: epic → cluster → subsystem
- Easier onboarding for new developers

---

## Alternative Strategies (Not Recommended)

### ❌ Single Monolithic PR
- **Problem**: 79 epics, 79 files, 1,099 CYC changes in one PR
- **Issue**: Impossible to review thoroughly
- **Risk**: High chance of missing critical issues

### ❌ One PR per Epic
- **Problem**: 79 separate PRs
- **Issue**: Review fatigue, context switching overhead
- **Risk**: Inconsistent review quality

### ❌ Random File Grouping
- **Problem**: Files grouped arbitrarily (e.g., alphabetically)
- **Issue**: No architectural coherence
- **Risk**: Reviewers lack domain context

---

## Success Metrics

### Per-PR Metrics
- ✅ Review time <2 hours per PR
- ✅ Zero P0 findings (all caught in Phase 3 audit)
- ✅ Build passes on first attempt
- ✅ All tests green
- ✅ Complexity targets met (all methods ≤15 CYC)

### Wave-Level Metrics
- ✅ All 7 PRs merged within 2 weeks
- ✅ Zero production incidents
- ✅ Zero rollbacks
- ✅ 100% test coverage maintained
- ✅ CodeScene score improvement (8 → 6 target)

---

## Next Steps

1. **Generate PR branches** (one per cluster)
2. **Create GitHub PRs** (use template above)
3. **Assign reviewers** (domain experts)
4. **Parallel review** (all 7 PRs simultaneously)
5. **Sequential merge** (S7 → S5 → S1 → S6 → S2 → S4 → S3)
6. **Celebrate** 🎉 (Wave 4 complete!)

---

**Maintainer**: Wave 4 Completion Lead  
**Last Updated**: 2026-06-16T17:58:00Z  
**Status**: 🟢 READY FOR PR CREATION