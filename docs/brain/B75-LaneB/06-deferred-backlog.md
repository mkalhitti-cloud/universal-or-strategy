# B75-LaneB Deferred Backlog

**Block**: B75-LaneB
**Date written**: 2026-08-17
**Source**: Phase 5 final review (ptt-plan-reviewer)

---

## Block: B75-LaneB

| ID | Source | Item | Priority | Status |
|----|--------|------|----------|--------|
| DW-B75-B-01 | B75-LaneB Ph4b | T_B66OBJ_P01/T_B67_01: NT8-HOST-REQUIRED — integration tests for primary `SetCloneAtmObjectCache` (non-null AtmStrategy object path → `FollowerAtmMode.Named.AtmObject != null`) and `GetSavedFollowerNames` (matching-rule positive path, requires `NinjaTrader.Cbi.Account` to call `AddRule`) require NT8 host; skip skeletons documented in test file. T_B67_03 positive predicate path (`Assert.True` after seeding matching rule) also unverifiable without NT8 Account. Full integration intent documented in T_B67_01 skip skeleton. | P3 | OPEN |

---

## Carry-Forward Open Items (from prior blocks)

Copied from `docs/brain/NO-PIPELINE-REPAIRS.md` §Consolidated Carry-Forward OPEN Items (post B72/B73/B74), unchanged by B75-LaneB:

| ID | Item | Priority | Status |
|----|------|----------|--------|
| DW-B63-FLATTEN-MULTWAVE-01 | PTT-Flatten multi-wave on follower accounts after ATM target fills — followers go Short instead of Flat. **FIXED by HOTFIX-B63-FLATTEN-01** (PTT-prefix guard added to `TryDispatchLeaderFlat` gate 2.5). | P1 | APPLIED — awaiting live test |
| DW-B66-BE-01 | `CancelQxBrackets` cancels `PTT-BE-Stop` orders during Quick Exit — Director confirmation required | P1 | OPEN |
| DW-B66-C-02 | `DispatchCopy` Gate 5 dedup key = 0.0 for all StopLimit entries | P1 | OPEN |
| DW-B63-01 | Spurious `PTT-Copy` bracket orders on Sim102 after ATM fill | P1 | OPEN |
| DW-B54-01 | ATM auto-inject — blocked, requires `StrategyBase` API unavailable in `AddOnBase` | P1 | OPEN (blocked) |
| DW-B72-01 | `IsAtmBracketName("Stop10")` returns true — acceptable-known edge | P3 | OPEN |
| DW-B73-B-01 | `RaiseBeAllDisarmed` fires on every flat regardless of per-account slot ownership — redundant broadcasts, no correctness impact | P2 | OPEN |
| DW-B73-B-02 | `UpdateBeAllVisuals` creates unfrozen `SolidColorBrush` instances on every call — allocation on WPF UI thread, not a hot path | P2 | OPEN |
| DW-B58-01 | `SnapshotTargetsPublic` hardcoded order-name prefixes | P2 | OPEN |
| DW-B58-02 | `GlobalBe` non-atomic lazy init | P2 | OPEN |
| DW-B58-03 | `RelayBe` `OcoGroup` not forwarded | P2 | OPEN |
| PRE-EXISTING-01 | Non-ASCII em-dash `CopyEngine.cs` lines 398, 499 | P2 | OPEN |
| PRE-EXISTING-02 | Non-ASCII arrow `CopyEngine.cs` lines ~1449-1450 | P2 | OPEN |
| PRE-EXISTING-03 | `deploy-sync.ps1` archived; PropTraderTools sync is manual | P2 | OPEN |
