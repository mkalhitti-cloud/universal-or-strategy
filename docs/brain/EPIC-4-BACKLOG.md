# Epic 4: Sticky State & IPC Hardening - BACKLOG

**Status**: ⏳ PENDING  
**Prerequisites**: Epic 3 PR #1 merged

## Inherited from Epic 3

### P1 Issues (High Priority)

1. **IPC Queue Observability**
   - **Issue**: REAPER audit monitors legacy queue, not _photonDispatchRing
   - **Location**: V12_002.UI.IPC.cs
   - **Action**: Add _photonDispatchRing.Count monitoring
   - **Acceptance**: Queue depth alerts at 80% threshold

2. **Entries Quantity Validation**
   - **Issue**: Secondary dispatch methods lack quantity clamping
   - **Locations**:
     - ExecuteTREND_DispatchSima (Entries.Trend.cs)
     - ExecuteTRENDManual_DispatchSima (Entries.Trend.cs)
   - **Action**: Add PositionSize clamping before dispatch
   - **Acceptance**: No orders exceed PositionSize limit

## New Epic 4 Scope

### Sticky State (Persistence Layer)
- [ ] Cross-session state recovery
- [ ] Atomic state snapshots
- [ ] Rollback on corruption detection

### IPC Hardening (External Command Plane)
- [ ] Command validation layer
- [ ] Rate limiting (backpressure NACK @1600)
- [ ] Malformed payload circuit breaker
- [ ] Allowlist bypass anomaly detection

## Success Criteria

- [ ] All Epic 3 deferred items addressed
- [ ] Persistence layer operational
- [ ] IPC command plane hardened
- [ ] PHS: 100/100 maintained
- [ ] Greptile: 5/5 maintained
- [ ] Zero P0/P1 issues

## Session Transition Checklist

**Before Starting Epic 4**:
- [x] Epic 3 PR #1 merged
- [x] Epic 3 completion summary created
- [x] Epic 4 backlog reviewed
- [x] V12 roadmap updated
- [ ] New session started with handoff context