# Epic 2 YOLO: Medium-Complexity Methods (CYC 15-20)

**Strategy**: Automated `/epic-run` orchestration
**Target**: 33 methods → CYC ≤ 15
**PR Strategy**: 1 PR for entire YOLO phase

## Overview

These methods have **moderate complexity** (CYC 15-20) and are suitable for automated refactoring:
- Lower risk than P0 methods (CYC 20-31)
- Well-defined extraction patterns
- Minimal cross-file dependencies
- Suitable for YOLO-mode orchestration

## Method List (33 Methods)

### Order Management & Callbacks (9 methods)

1. **ProcessQueuedAccountOrder** - `V12_002.Orders.Callbacks.AccountOrders.cs`
   - Current CYC: 15, LOC: 34
   - Risk: Medium - Account order queue processing

2. **ProcessOnOrderUpdate** - `V12_002.Orders.Callbacks.cs`
   - Current CYC: 19, LOC: 48
   - Risk: Medium - Order update event routing

3. **HandleFlatPosition_CleanupActivePositions** - `V12_002.Orders.Callbacks.Execution.cs`
   - Current CYC: 17, LOC: 30
   - Risk: Medium - Position cleanup after flatten

4. **PropagateMaster_IdentifyMove** - `V12_002.Orders.Callbacks.Propagation.cs`
   - Current CYC: 18, LOC: 40
   - Risk: Medium - Master order move detection

5. **ValidateOrphanedMasterOrders** - `V12_002.Orders.Management.Cleanup.cs`
   - Current CYC: 19, LOC: 32
   - Risk: Medium - Orphaned order validation

6. **ManageCIT** - `V12_002.Orders.Management.Flatten.cs`
   - Current CYC: 19, LOC: 77
   - Risk: Medium - Cancel-If-Touched management

7. **FlattenSinglePosition** - `V12_002.Orders.Management.Flatten.cs`
   - Current CYC: 16, LOC: 76
   - Risk: Medium - Single position flatten logic

8. **SyncLimitTarget** - `V12_002.Orders.Management.StopSync.cs`
   - Current CYC: 17, LOC: 128 (LARGE)
   - Risk: Medium - Limit target synchronization

9. **RestoreCascadedTargets** - `V12_002.Orders.Management.StopSync.cs`
   - Current CYC: 16, LOC: 90 (LARGE)
   - Risk: Medium - Cascaded target restoration

### SIMA & Fleet Operations (8 methods)

10. **ProcessFlattenWorkItem_CancelOrders** - `V12_002.SIMA.Flatten.cs`
    - Current CYC: 18, LOC: 36
    - Risk: Medium - Flatten work item order cancellation

11. **SweepBrokerOrders** - `V12_002.SIMA.Lifecycle.cs`
    - Current CYC: 18, LOC: 49
    - Risk: Medium - Broker order sweep logic

12. **AdoptFleetWorkingOrders** - `V12_002.SIMA.Lifecycle.cs`
    - Current CYC: 17, LOC: 46
    - Risk: Medium - Fleet order adoption

13. **ClassifyAndRouteFleetOrder** - `V12_002.SIMA.Lifecycle.cs`
    - Current CYC: 16, LOC: 42
    - Risk: Medium - Fleet order classification

14. **ShadowPropagateStopMoves** - `V12_002.SIMA.Shadow.cs`
    - Current CYC: 20, LOC: 32
    - Risk: Medium-High - Shadow stop propagation

15. **SymmetryGuardReplaceExistingFollowerTarget** - `V12_002.Symmetry.Replace.cs`
    - Current CYC: 18, LOC: 49
    - Risk: Medium - Follower target replacement

16. **SymmetryGuardTryResolveFollowersForDispatch** - `V12_002.Symmetry.Replace.cs`
    - Current CYC: 18, LOC: 33
    - Risk: Medium - Follower resolution for dispatch

17. **AuditMaster_HandleNakedPosition** - `V12_002.REAPER.Audit.cs`
    - Current CYC: 15, LOC: 38
    - Risk: Medium - Naked position audit handling

### UI & Compliance (10 methods)

18. **IsOrderAllowed** - `V12_002.UI.Compliance.cs`
    - Current CYC: 16, LOC: 43
    - Risk: Medium - Order compliance validation

19. **HandleFleetTargetFill** - `V12_002.UI.Compliance.cs`
    - Current CYC: 16, LOC: 58
    - Risk: Medium - Fleet target fill handling

20. **TryApplyConfigTarget_Value** - `V12_002.UI.IPC.Commands.Config.cs`
    - Current CYC: 17, LOC: 45
    - Risk: Medium - Config target value application

21. **TryHandleFleetCommand** - `V12_002.UI.IPC.Commands.Fleet.cs`
    - Current CYC: 19, LOC: 42
    - Risk: Medium - Fleet command handling

22. **TryHandleFleet_CancelAll** - `V12_002.UI.IPC.Commands.Fleet.cs`
    - Current CYC: 19, LOC: 41
    - Risk: Medium - Fleet cancel-all command

23. **CancelAll_ProcessSingleFleetAccount** - `V12_002.UI.IPC.Commands.Fleet.cs`
    - Current CYC: 18, LOC: 31
    - Risk: Medium - Single fleet account cancellation

24. **TryHandleFleet_MoveTarget** - `V12_002.UI.IPC.Commands.Fleet.cs`
    - Current CYC: 15, LOC: 33
    - Risk: Medium - Fleet target move command

25. **IsSymbolMatch** - `V12_002.UI.IPC.cs`
    - Current CYC: 18, LOC: 19
    - Risk: Low - Symbol matching logic

26. **DestroyPanel** - `V12_002.UI.Panel.Construction.cs`
    - Current CYC: 17, LOC: 149 (LARGE)
    - Risk: Medium - Panel destruction logic

27. **ShowModeSpecificControls** - `V12_002.UI.Panel.Handlers.cs`
    - Current CYC: 20, LOC: 42
    - Risk: Medium-High - Mode-specific control visibility

### UI Panel & Chart (6 methods)

28. **UpdateTargetVisibility** - `V12_002.UI.Panel.Handlers.cs`
    - Current CYC: 19, LOC: 36
    - Risk: Medium - Target visibility updates

29. **FindChartTraderViaChartTab** - `V12_002.UI.Panel.Helpers.cs`
    - Current CYC: 20, LOC: 54
    - Risk: Medium-High - ChartTrader lookup logic

30. **UpdatePanelState** - `V12_002.UI.Panel.StateSync.cs`
    - Current CYC: 16, LOC: 51
    - Risk: Medium - Panel state synchronization

31. **SyncPanelConfigFromSnapshot** - `V12_002.UI.Panel.StateSync.cs`
    - Current CYC: 15, LOC: 37
    - Risk: Medium - Panel config snapshot sync

### Entry Logic (2 methods)

32. **CheckFFMAConditions** - `V12_002.Entries.FFMA.cs`
    - Current CYC: 16, LOC: 50
    - Risk: Medium - FFMA entry condition validation

33. **MonitorRmaProximity** - `V12_002.Entries.RMA.cs`
    - Current CYC: 17, LOC: 67
    - Risk: Medium - RMA proximity monitoring

## Execution Plan

### Phase 1: Automated Orchestration
Run the `/epic-run` command to trigger full YOLO-mode execution:

```
/epic-run epic2-yolo "Refactor 33 medium-complexity methods (CYC 15-20 → ≤15)"
```

### Phase 2: Orchestrator Workflow
The orchestrator will:
1. **Intake**: Generate `00-scope.md` (this file)
2. **Plan**: Create `01-analysis.md` and `02-approach.md`
3. **Scan**: Run Sentinel audit via `02-greptile-report.md`
4. **Validate**: DNA compliance check
5. **Tickets**: Generate individual ticket files for each method
6. **Execute**: Sequential ticket execution via `v12-engineer`
7. **Verify**: F5 gate + `/pr-loop` to 100/100 PHS

### Phase 3: Ticket Grouping Strategy
Methods will be grouped into tickets by **file and subsystem**:
- **Ticket Group A**: Order Callbacks (methods 1-9)
- **Ticket Group B**: SIMA/Fleet (methods 10-17)
- **Ticket Group C**: UI/Compliance (methods 18-27)
- **Ticket Group D**: UI Panel/Chart (methods 28-31)
- **Ticket Group E**: Entry Logic (methods 32-33)

**Estimated**: 5-8 tickets total (4-6 methods per ticket)

### Phase 4: PR Submission
Single PR for entire YOLO phase:
- Title: `[EPIC2-YOLO] Reduce 33 methods from CYC 15-20 to ≤15`
- Body: Reference all tickets, include aggregate CYC reduction
- Labels: `epic2`, `yolo`, `complexity-reduction`

## Success Criteria

- [ ] All 33 methods: CYC ≤ 15
- [ ] Zero locks introduced (verified by `grep -r "lock(" src/`)
- [ ] ASCII-only maintained (verified by `python check_ascii.py`)
- [ ] FSM/Actor pattern preserved (no new stateful classes without Enqueue)
- [ ] All tests pass (`powershell -File .\scripts\pre_push_validation.ps1`)
- [ ] F5 verification passed (BUILD_TAG visible in NinjaTrader)
- [ ] `/pr-loop` achieves 100/100 PHS
- [ ] Single PR merged successfully

## Risk Mitigation

**Medium-High Risk Methods** (CYC 20, requires extra attention):
- `ShadowPropagateStopMoves` (CYC 20)
- `ShowModeSpecificControls` (CYC 20)
- `FindChartTraderViaChartTab` (CYC 20)

**Large LOC Methods** (>80 lines, may need extra extraction):
- `SyncLimitTarget` (LOC 128)
- `RestoreCascadedTargets` (LOC 90)
- `DestroyPanel` (LOC 149)

**Orchestrator Guidance**:
- Prioritize CYC 20 methods first (highest risk in YOLO batch)
- Split large LOC methods into 2+ sub-methods
- Verify no cross-file dependencies before extraction

## Jane Street Alignment

All refactorings MUST maintain:
- ✅ Zero locks (FSM/Actor Enqueue model only)
- ✅ ASCII-only (no Unicode in strings)
- ✅ Deterministic behavior (no DateTime.Now, use UTC)
- ✅ Explicit error handling (no silent catches)
- ✅ Cognitive simplicity (CYC ≤15 per function)

## Next Steps

1. **Director**: Review this scope document
2. **Director**: Approve YOLO execution: `YES` to proceed
3. **Orchestrator**: Run `/epic-run epic2-yolo "Refactor 33 medium-complexity methods"`
4. **Orchestrator**: Monitor progress through ticket execution
5. **Orchestrator**: Drive `/pr-loop` to 100/100 PHS
6. **Director**: F5 verification and merge approval

---

**Note**: This YOLO phase should ONLY be executed AFTER all TDD tickets (Epic 2 TDD) are complete and merged. The 3 critical methods (CYC 21-31) must be stable before touching these 33 medium-complexity methods.