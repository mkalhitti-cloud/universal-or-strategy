# TRAYCER EPIC BRIEF — COMPLETE (v2, UltraThink)
## Phase 7: Complexity Extraction & Concurrency Hardening
**Project**: V12 Universal OR Strategy
**Branch**: main
**Current BUILD_TAG**: `1111.007-phase7-t16`
**Protocol**: V12 DNA — Lock-Free, ASCII-Only, Zero-Allocation
**Prior completed**: T2, T3, T4, T13, T14, T15, T16 (Sprint 5)

---

## Epic Description

Systematic reduction of cyclomatic complexity across the V12 Photon Kernel.
Full codebase audit (2026-05-13) identified 45 C# symbols exceeding CYC > 20.
This epic tracks all CRITICAL (CYC >= 40) and HIGH (CYC >= 30) targets plus
two DNA compliance housekeeping tickets.

**V12 DNA — all tickets must comply:**
- Zero executable lock() statements
- ASCII-only in all string literals and Print() calls
- Zero new heap allocations on hot path
- deploy-sync.ps1 + F5 NinjaTrader verification per ticket
- BUILD_TAG bumped per ticket

**Audit**: `docs/brain/complexity_audit_cyc20_report.md`
**Registry**: `docs/brain/Living_Document_Registry.md`

---

## METRIC NOTE: ExecuteSmartDispatchEntry (T-G)

The T03 acceptance doc (Sprint 5) records CYC=22 post-extraction (tool: v12_split.py).
The Bob complexity audit (2026-05-13) records CYC=33 (tool: complexity_audit.py).
Both measurements agree the method EXCEEDS the CYC=20 threshold.
DEVIATION-T3-B accepted CYC=22 as "acceptable per D-S2" — this Epic reopens it
because complexity_audit.py is the authoritative measurement tool going forward.
Establish ground truth via complexity_audit.py before starting T-G.

---

## TICKET SEQUENCE

---

### T-Q1 + T-Q2: DNA Compliance Housekeeping
**Priority**: P0 — Execute First (before any extraction)
**Agent**: Bob (v12-engineer)
**Sessions**: 1 combined

**T-Q1: Empty Catch Logging**
12 empty `catch {}` blocks silently swallow exceptions across 4 production files.
Observability Protocol violation (Section 9: never silent swallow).

Exempt (DO NOT TOUCH):
- `src/V12_002.MetadataGuard.cs` — 6x `catch { return true; }` = intentional fail-open guards
- `src/V12_002.Photon.MmioMirror.cs` — 2x Dispose pattern catches

Files to fix:
- `src/V12_002.Orders.Callbacks.AccountOrders.cs` — 5 instances
- `src/V12_002.SIMA.Lifecycle.cs` — 3 instances
- `src/V12_002.SIMA.Dispatch.cs` — 2 instances (TriggerCustomEvent + MMIO publish)
- `src/V12_002.SIMA.Fleet.cs` — 2 instances

Fix pattern:
```csharp
// BEFORE:
try { TriggerCustomEvent(o => ProcessAccountOrderQueue(), null); } catch { }

// AFTER:
try { TriggerCustomEvent(o => ProcessAccountOrderQueue(), null); }
catch (Exception ex) { Print("[AOQ] TriggerCustomEvent failed: " + ex.Message); }
```

**T-Q2: IPC Server Polling Comment**
`src/V12_002.UI.IPC.Server.cs` lines 85 and 214 have Thread.Sleep calls.
Forensic confirmed: runs on dedicated background thread (ipcThread, IsBackground=true).
No code change — add comment only to prevent future false alarms.

```csharp
// Background thread (ipcThread) only. Sleep(100) is a polling interval, not UI-thread block.
Thread.Sleep(100);
```

Acceptance:
- [ ] Zero unlogged `catch {}` in src/ — grep returns 0 C# hits
- [ ] Comments added at IPC.Server.cs lines 85 and 214
- [ ] deploy-sync.ps1 PASS
- [ ] BUILD_TAG: `1111.007-phase7-tQ1`

---

### T-C: AttachPanelHandlers Extraction
**Priority**: HIGH | **CYC**: 39 -> <= 5
**File**: `src/V12_002.UI.Panel.Handlers.cs:17`
**Agent**: Bob | **Sessions**: 1
**Dependency**: T-Q1 complete

Split into per-control attachment helper methods. Residual becomes a pure
coordinator (CYC=2). Validate in NinjaTrader BEFORE opening T-D and T-F —
this ticket must be blame-isolated.

Target structure:
- `AttachPanelHandlers()` [CYC=2 residual coordinator]
- `AttachExecutionPanelHandlers()`, `AttachSizingPanelHandlers()`, etc.

Acceptance:
- [ ] Residual CYC <= 5, all helpers CYC <= 19
- [ ] Zero behavioral change (same handlers, same controls)
- [ ] 15-LOC extraction floor respected
- [ ] F5 NinjaTrader: UI panel renders identically, all controls respond
- [ ] BUILD_TAG: `1111.007-phase7-tC`

---

### T-D + T-F: OnSyncAllClick + UpdateContextualUI (same session)
**Priority**: HIGH | **CYC**: 37 + 36 -> <= 5 each
**File**: `src/V12_002.UI.Panel.Handlers.cs` (lines 238, 427)
**Agent**: Bob | **Sessions**: 1
**Dependency**: T-C complete and F5 validated

Bundle both in one Bob session — same file, shared UI state context.
Single deploy-sync + single F5 validation pass.

T-D (`OnSyncAllClick`): Extract per-pathway sync helpers (SyncOrchestrator set).
T-F (`UpdateContextualUI`): State Pattern — extract per-mode update methods.

Acceptance:
- [ ] Both residuals CYC <= 5, all helpers CYC <= 19
- [ ] Zero behavioral change
- [ ] F5: All panel modes render correctly, Sync All button functions
- [ ] BUILD_TAG: `1111.007-phase7-tDF`

---

### T-E: ManageTrail_RunPerTradeBranches Extraction
**Priority**: HIGH | **CYC**: 36, max_nesting=7 | **LOC**: 111
**File**: `src/V12_002.Trailing.cs:193`
**Agent**: Bob | **Sessions**: 1
**Dependency**: T-D + T-F complete

**Cluster context**: Part of the Trailing subgraph cluster. The related methods
`ManageTrail_RunFleetSymmetrySync` (CYC=24) and `MoveStopsToBreakevenWithOffset`
(CYC=25) are in the MEDIUM tier but are architecturally coupled — consider grouping
them with T-E as a trailing sub-sprint rather than treating T-E in isolation.

Extraction approach: Extract per-strategy trail handlers (one method per trade
strategy type — RMA trail, OR trail, TREND trail, FFMA trail, etc.).

Acceptance:
- [ ] Residual CYC <= 5, all per-strategy handlers CYC <= 19
- [ ] No trail logic change (same stop levels produced for same inputs)
- [ ] Zero new heap allocations (trailing is hot path)
- [ ] F5: Live trailing stops update correctly during session
- [ ] BUILD_TAG: `1111.007-phase7-tE`

---

### T-H: ValidateStopPrice Extraction
**Priority**: HIGH | **CYC**: 33, max_nesting=7 | **LOC**: 73
**File**: `src/V12_002.Orders.Management.StopSync.cs:551`
**Agent**: Bob | **Sessions**: 1

**Cluster context**: Part of the StopSync subgraph cluster. Related methods
`ValidateStopOrderPreconditions` (CYC=24), `UpdateStopQuantity` (CYC=24),
`SyncLimitTarget` (CYC=21), and `RestoreCascadedTargets` (CYC=23) are in the
same file. Consider a StopSync sub-sprint (T-H + MEDIUM cluster) as a unit.

Extraction approach: Extract validation rule objects / guard clauses.
The recursive logic (level parameter) should become an explicit rule chain,
not a recursive method with 7 nesting levels.

Acceptance:
- [ ] Residual CYC <= 5, validation rule methods CYC <= 15
- [ ] No stop price change for any input combination (zero logic change)
- [ ] Recursive pattern replaced with iterative or rule-chain pattern
- [ ] F5: Stop orders placed at correct prices in live session
- [ ] BUILD_TAG: `1111.007-phase7-tH`

---

### T-G: ExecuteSmartDispatchEntry Further Reduction
**Priority**: HIGH | **CYC**: 22 (T03 doc) / 33 (audit) — GROUND TRUTH NEEDED
**File**: `src/V12_002.SIMA.Dispatch.cs:45`
**Agent**: Bob | **Sessions**: 1

**Pre-condition**: Run `python scripts/complexity_audit.py` on current src/ BEFORE
starting this ticket to establish the authoritative CYC baseline. If CYC < 20,
this ticket is CLOSED and no work needed. If CYC >= 20, proceed.

T03 (Sprint 5) accepted DEVIATION-T3-B: residual CYC=22 acceptable. This ticket
re-examines that deviation against the complexity_audit.py measurement of CYC=33.
The subgraph complexity context:
- `Dispatch_PublishMarketBracketToPhoton` (CYC=26) — helper
- `Dispatch_BuildFollowerOrders` (CYC=21) — helper

The residual orchestrator can be further decomposed by splitting the per-iteration
catch/rollback block and the final pump-prime + forensic-report block into helpers.

Acceptance:
- [ ] complexity_audit.py baseline established first
- [ ] If CYC >= 20: reduce to <= 19 without changing SIMA dispatch behavior
- [ ] Photon publish triple (sideband → MemoryBarrier → TryEnqueue) preserved
- [ ] Increment-before-enqueue invariant preserved
- [ ] F5: SIMA dispatch functions correctly for Market and Limit entries
- [ ] BUILD_TAG: `1111.007-phase7-tG`

---

### T-A + T-B: Unified Command Dispatcher (P3 Design + Bob Execution)
**Priority**: HIGH | **CYC**: 49 each -> <= 5 each
**Files**: `src/V12_002.UI.Callbacks.cs:337` (T-A), `src/V12_002.UI.IPC.cs:325` (T-B)
**Agent**: Claude ARCHITECT (1 joint design session) → Bob (2 execution sessions)
**Dependency**: T-D + T-F complete

CRITICAL: Design T-A and T-B TOGETHER in one P3 session. They are both command
routers (keyboard events and IPC messages). A unified Command Pattern prevents
two incompatible dispatch architectures from emerging.

The P3 Claude session must produce one implementation_plan.md covering both.
Bob executes T-A first (validate), then T-B (validate).

Unified architecture target:
```csharp
// Initialized once at startup:
Dictionary<Key, Action> _keyCommands;
Dictionary<string, Action<string[]>> _ipcCommands;

// OnKeyDown residual (CYC <= 3):
private void OnKeyDown(object sender, KeyEventArgs e)
{
    if (_keyCommands.TryGetValue(e.Key, out var cmd)) cmd();
}

// ProcessIpc_MatchSymbol residual (CYC <= 3):
private bool ProcessIpc_MatchSymbol(string action, string[] parts)
{
    if (_ipcCommands.TryGetValue(action, out var cmd)) { cmd(parts); return true; }
    return false;
}
```

T-A Acceptance:
- [ ] `OnKeyDown` residual CYC <= 5
- [ ] All key handlers registered in `InitKeyCommandRegistry()`
- [ ] New shortcuts addable in one line
- [ ] F5: All keyboard shortcuts function identically

T-B Acceptance:
- [ ] `ProcessIpc_MatchSymbol` residual CYC <= 5
- [ ] All IPC handlers registered in `InitIpcCommandRegistry()`
- [ ] New IPC commands addable in one line
- [ ] F5: All IPC commands function identically

Combined BUILD_TAGs: `1111.007-phase7-tA` then `1111.007-phase7-tB`

---

## PARKED — MEDIUM TIER (CYC 21-29)

**Status**: Explicitly deferred. No re-triage until all HIGH tickets above complete.
**Count**: ~30 C# symbols
**Details**: `docs/brain/complexity_audit_cyc20_report.md` sections 15-54

**HOT-PATH EXCEPTIONS — Watch list (may be promoted):**
- `ShouldSkipFleetAccount` (CYC=25) — called in ESDE fleet loop (per-dispatch)
- `TryFindOrderInPosition` (CYC=25) — called on every OnAccountOrderUpdate

---

## Cluster Map (for future sub-sprints)

| Cluster | Methods | Total CYC |
|:---|:---|:---:|
| **Trailing** | ManageTrail_RunPerTradeBranches (36) + ManageTrail_RunFleetSymmetrySync (24) + MoveStopsToBreakevenWithOffset (25) | 85 |
| **StopSync** | ValidateStopPrice (33) + ValidateStopOrderPreconditions (24) + UpdateStopQuantity (24) + SyncLimitTarget (21) + RestoreCascadedTargets (23) | 125 |
| **SIMA Dispatch** | ExecuteSmartDispatchEntry (22/33) + Dispatch_PublishMarketBracketToPhoton (26) + Dispatch_BuildFollowerOrders (21) | 69-80 |

---

## Epic Completion Criteria

- [ ] T-Q1, T-Q2, T-C, T-D, T-E, T-F, T-G, T-H, T-A, T-B — all Director-accepted
- [ ] Zero C# symbols in src/ with CYC >= 30 (complexity_audit.py verification)
- [ ] All changes live in NinjaTrader (F5 verified per ticket)
- [ ] Living Document Registry updated with all ticket entries
- [ ] master_roadmap.md Phase 7 section updated to COMPLETE

---

## Pre-Epic Admin (complete before first ticket)

1. Update `docs/brain/task.md` — current is T16, not T03
2. Add T16 (`CreateNewStopOrder`, CYC 21→6) to Living Document Registry
3. Create this Epic in Traycer

## Agent Assignments

| Agent | Tickets |
|:---|:---|
| Bob CLI (v12-engineer) | T-Q1, T-Q2, T-C, T-D, T-E, T-F, T-G, T-H, T-A (exec), T-B (exec) |
| Claude ARCHITECT (plan-only) | T-A + T-B joint design session |
| Antigravity (P1 Orchestrator) | handoffs, acceptance verification, registry updates |
