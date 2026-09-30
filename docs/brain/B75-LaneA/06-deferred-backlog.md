# PTT Deferred Work Backlog

This file is maintained by ptt-plan-reviewer (Phase 5). Each block appends its own
Section K entries. Items carry forward until resolved or explicitly closed by Director.

---

## Block B75-LaneA — CopyEngine Clone/Copy Hotfixes

**Block completed**: 2026-08-17
**Pipeline verdict**: FINAL_PASS
**Files in scope**: `src/PropTraderTools/CopyEngine.cs`, `src/PropTraderTools/CopyEngineTests.cs`

### New Items from B75-LaneA

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-B75-01 | Non-ASCII em-dash/box-drawing/arrow in `CopyEngine.cs` at lines 202, 203, 493, 697, 1856, 1857 — pre-existing from B72/B73/B74 commits. Cross-reference: PRE-EXISTING-01/02 in NO-PIPELINE-REPAIRS.md. All occurrences are in comments, no runtime impact. Next block touching this file should replace with ASCII equivalents (em-dash `—` → `--`, box-drawing `──` → `//---`, right-arrow `→` → `->`). | P2 | B76 or future | OPEN |
| DW-B75-02 | `[PTT-CLONE]` diagnostic `Output.Process` lines retained in `CopyEngine.cs` (at `SetCloneAtmCache`, `SetCloneAtmObjectCache`, `GetCloneAtmMode` call sites). Authorized as temporary per plan Section B DIAG-CLEANUP note. Remove only after Clone mode live confirmation and Director sign-off. | P2 | B76 or future | OPEN |
| DW-B75-03 | 14 NT8-runtime-bound tests in `CopyEngineB75Tests` marked `[Fact(Skip="NT8-runtime")]`. Affected groups: HOTFIX-B66-COPY-REPLACE (8), HOTFIX-CLONE-DRAG (1), HOTFIX-B66-ATM-OBJ (1), HOTFIX-B67-CHECKBOX-RESTORE (1), CYC REFACTOR HELPERS (3). Need NT8 host harness or mock `Account`/`Order`/`AtmStrategy` layer to enable full execution outside NT8 host process. | P2 | future | OPEN |
| DW-B75-04 | `HasWorkingPttCopy` has no retry counter guard. Current dedup mechanism uses `cancelledOrder.OrderId + "-R"` suffix to bypass existing dedup cache. If a replacement is itself swept by the ATM bracket-arming process, a second replacement cycle starts with no cycle limit. Risk is bounded in practice by the finite number of ATM sweeps per fill event, but not formally cycle-proof. Recommend adding a per-orderId replacement counter (max 1 re-place per original orderId) in a future block. | P2 | B76 or future | OPEN |

### Carried Items from Prior Blocks (OPEN — no action in B75-LaneA)

| ID | Source Block | Item | Priority | Status |
|----|-------------|------|----------|--------|
| DW-B66-BE-01 | B66/B74 | `CancelQxBrackets` cancels `PTT-BE-Stop` orders during Quick Exit. Director confirmation required before adding `IsAtmBracketName` guard to the QX cancel path to preserve BE stops. | P1 | OPEN |
| DW-B66-C-02 | B66/B74 | `DispatchCopy` Gate 5 dedup key = `0.0` for all StopLimit entries because `LimitPrice == 0` for StopLimit orders in NT8. Duplicate follower StopLimit entries possible on repeated dispatch for the same working StopLimit leader order. | P1 | OPEN |
| DW-B63-01 | B63/B74 | Spurious `PTT-Copy` bracket orders on Sim102 after ATM fill. Root cause not isolated. May be related to `HOTFIX-B66-COPY-REPLACE` firing prematurely on Sim102 when follower already has a working entry. | P1 | OPEN |
| DW-B54-01 | B54 | ATM auto-inject blocked: `AtmStrategyCreate()` is `StrategyBase`-only. Not available from `AddOnBase`. Confirmed by `NT8_FULL_REFERENCE.md`. No workaround without StrategyBase host or separate Strategy plugin. | P1 | OPEN (blocked) |
| DW-B72-01 | B72-LaneA | `IsAtmBracketName("Stop10")` returns true — digit-at-[4] edge case. NT8 ATM order names are Stop1..Stop9 only; over-cancel is conservative, not dangerous in production. | P3 | OPEN |
| DW-B73-B-01 | B73-LaneB | `RaiseBeAllDisarmed` fires on every flat regardless of per-account slot ownership — redundant broadcasts, no correctness impact. | P2 | OPEN |
| DW-B73-B-02 | B73-LaneB | `UpdateBeAllVisuals` creates unfrozen `SolidColorBrush` instances on every call — allocation on WPF UI thread, not a hot path. Should use pre-frozen static brushes per JS-008. | P2 | OPEN |
| DW-B58-01 | B58 | `SnapshotTargetsPublic` hardcoded order-name prefixes — should use constants or enum. | P2 | OPEN |
| DW-B58-02 | B58 | `GlobalBe` non-atomic lazy init — `Interlocked.CompareExchange` pattern required. | P2 | OPEN |
| DW-B58-03 | B58 | `RelayBe` `OcoGroup` not forwarded to follower brackets. | P2 | OPEN |
| PRE-EXISTING-01 | pre-B72 | Non-ASCII em-dash in `CopyEngine.cs` lines 398, 499 (line numbers from pre-B72 era; may have shifted). Superseded by DW-B75-01 which re-identifies all 6 current locations. | P2 | SUPERSEDED by DW-B75-01 |
| PRE-EXISTING-02 | pre-B72 | Non-ASCII arrow in `CopyEngine.cs` lines ~1449-1450 (estimate from pre-B72 era). Superseded by DW-B75-01. | P2 | SUPERSEDED by DW-B75-01 |
| PRE-EXISTING-03 | pre-B72 | `deploy-sync.ps1` archived; PropTraderTools sync is manual. | P2 | OPEN |

---

*This file is append-only. New blocks add a new ## section above this footer.*
*All items remain OPEN until a reviewer explicitly marks them CLOSED with a block citation.*
