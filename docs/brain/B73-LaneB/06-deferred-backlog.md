# B73-LaneB Deferred Backlog

**Block**: B73-LaneB
**Written by**: ptt-plan-reviewer (Ph5)
**Date**: 2026-08-17

---

## Closed This Block

### NO-PIPELINE-REPAIRS.md Hotfixes Formally Pipelined

**Status**: CLOSED — B73-LaneB Ticket-1

B73-LaneB formally pipelined 15 hotfixes that had previously existed in `NO-PIPELINE-REPAIRS.md`
status APPLIED (applied directly to the working tree without a plan/ticket/verify pipeline pass).
This block retroactively closed the pipeline gap for all 15 by:

1. Writing a retrospective architecture plan (`02-architecture-plan.md`) documenting each hotfix's
   rationale, change description, threading model, and JS-DNA compliance
2. Completing ticket review (3-violation -> TICKET_REVIEW_PASS after architect fixes)
3. Writing 33 xUnit [Fact] tests (`src/PropTraderTools/Tests/B73Tests.cs`) covering all 15 hotfixes
4. Independent verification (VERIFY_PASS) confirming 7/7 scans zero and 33/33 test names present

| Hotfix | Status before B73-LaneB | Status after B73-LaneB |
|--------|------------------------|------------------------|
| B73-B-01 (HOTFIX-DW-B72-02) | APPLIED, no pipeline | PIPELINE-CLOSED |
| B73-B-02 (HOTFIX-FIX-A-BE-BACKGROUND) | APPLIED, no pipeline | PIPELINE-CLOSED |
| B73-B-03 (HOTFIX-FIX-C-NO-DISARM-IN-UPDATEBUTTONCOLORS) | APPLIED, no pipeline | PIPELINE-CLOSED |
| B73-B-04 (HOTFIX-FLAT-DISARM) | APPLIED, no pipeline | PIPELINE-CLOSED |
| B73-B-05 (HOTFIX-BEALL-SYNC-01) | APPLIED, no pipeline | PIPELINE-CLOSED |
| B73-B-06 (HOTFIX-FLAT-MANUAL-CLOSE-01) | APPLIED, no pipeline | PIPELINE-CLOSED |
| B73-B-07 (HOTFIX-BEALL-DISARM-SYNC-01) | APPLIED, no pipeline | PIPELINE-CLOSED |
| B73-B-08 (HOTFIX-BEALL-BUFFER-SYNC-01) | APPLIED, no pipeline | PIPELINE-CLOSED |
| B73-B-09 (HOTFIX-BUFLABEL-02) | APPLIED, no pipeline | PIPELINE-CLOSED |
| B73-B-10 (HOTFIX-QUICKALL-SINGLETON-01) | APPLIED, no pipeline | PIPELINE-CLOSED |
| B73-B-11 (HOTFIX-QUICKALL-COMPILE-01) | APPLIED, no pipeline | PIPELINE-CLOSED |
| B73-B-12 (HOTFIX-BEALL-DISARM-CROSS-01) | APPLIED, no pipeline | PIPELINE-CLOSED |
| B73-B-13 (HOTFIX-BEALL-FLAT-RESET) | APPLIED, no pipeline | PIPELINE-CLOSED |
| B73-B-14 (HOTFIX-ORPHAN-STOP-CLEANUP) | APPLIED, no pipeline | PIPELINE-CLOSED |
| B73-B-15 (HOTFIX-FOLLOWER-LABEL-CLIP-01) | APPLIED, no pipeline | PIPELINE-CLOSED |

**Deferred items closed by B73-LaneB code changes**: 0
(B73-LaneB is a TradeCopierPanel.cs hotfix block only. No deferred items from prior blocks
targeted TradeCopierPanel.cs. All 15 hotfixes address new bugs introduced by multi-panel
BE ALL state divergence, manual close flat signal gaps, and layout overflow issues.)

---

## New Deferred Items — B73-LaneB

### DW-B73-B-01 — RaiseBeAllDisarmed fires on every flat regardless of per-account slot ownership

**Priority**: P2
**Target block**: B75+
**Status**: OPEN

**Description**: `UpdateButtonColors` HOTFIX-BEALL-FLAT-RESET block calls `RaiseBeAllDisarmed()`
whenever `!hasPosition && !IsPendingSlotsEmpty()`. This is correct and intentional — it fires
the broadcast to sync all panels. However, if `_leaderAccount` had no pending slot in CopyEngine
(the trader armed BE ALL from a different panel), this panel still fires the broadcast. The
broadcast is idempotent (`UpdateBeAllVisuals(Idle)` is safe to call multiple times), so there
is no correctness issue. The concern is redundant event fires across many open panels.

**Future optimization**: Gate `RaiseBeAllDisarmed` on `_leaderAccount`'s slot state before
raising. Requires adding per-account slot tracking to the BE ALL reset path, increasing CYC
of `UpdateButtonColors`.

**Defer rationale**: No correctness impact. Scope creep risk. The current behavior is safe.
Optimization increases `UpdateButtonColors` CYC beyond the current CYC=6. Not worth adding
complexity in this hotfix block.

**Impact**: Performance only — redundant UI update callbacks on multi-panel sessions during
flat events when BE ALL was not armed on this panel's leader account.

---

### DW-B73-B-02 — UpdateBeAllVisuals uses MakeBrush on every call (no freeze/cache)

**Priority**: P2
**Target block**: future
**Status**: OPEN

**Description**: `UpdateBeAllVisuals` calls `MakeBrush(13, 148, 136)` on every invocation for
both `BorderBrush` and `Foreground` assignments. `MakeBrush` allocates a new `SolidColorBrush`
instance each call. WPF best practice: static-color brushes should be `Freeze()`d and cached
as `static readonly` fields to avoid repeated allocations on the WPF UI thread.

**Future fix pattern**:
```csharp
private static readonly SolidColorBrush _beTealBrush =
    new SolidColorBrush(Color.FromRgb(13, 148, 136)).Frozen();
```

**Defer rationale**: Pre-existing pattern used by multiple methods in the panel. A correct fix
requires auditing all `MakeBrush` call sites and deciding which are static-color eligible for
caching. Out of scope for B73-LaneB hotfix block.

**Impact**: Performance only. Each call allocates 2 brush objects. In normal trading this is
called at most a few times per session (arm/disarm/flat cycles). Not a hot path.

---

## Carry-Forward Items (OPEN, unchanged from B66-LaneC)

### DW-B66-BE-01 — CancelQxBrackets cancels PTT-BE-Stop orders during Quick Exit

**Priority**: P1
**Target block**: B67+ (Director confirmation required)
**Status**: OPEN — opened by B66-LaneA; no change in B73-LaneB.

**Description**: The widened predicate in `IsQxCancelCandidate` (branch 4,
`StartsWith("PTT-BE-", StringComparison.Ordinal)`) means that pressing Quick Exit will cancel
any live `PTT-BE-Stop`, `PTT-BE-Stop-{i+1}`, or `PTT-BE-Target-{i+1}` orders on the account
for the instrument. This ensures a clean position exit but removes breakeven stop protection
at the moment of Quick Exit.

**Action required**: Director must confirm that cancelling PTT-BE-* orders on Quick Exit is
the intended behavior.

---

### DW-B66-C-02 — DispatchCopy dedup key = 0.0 for all StopLimit entries

**Priority**: P1
**Target block**: B67+
**Status**: OPEN — no change in B73-LaneB.

**Description**: `DispatchCopy` Gate 5 passes `order.LimitPrice` as the dedup key. Since
`StopLimit.LimitPrice == 0` always, every StopLimit entry shares dedup key 0.0. The second
concurrent StopLimit entry dispatch is wrongly rejected as a duplicate.

**Fix approach**: Replace `order.LimitPrice` with `order.OrderType == OrderType.StopLimit ? order.StopPrice : order.LimitPrice` at Gate 5.

---

### DW-B63-01 — Spurious PTT-Copy bracket orders on Sim102 after ATM fill

**Priority**: P1
**Target block**: B67+
**Status**: OPEN — no change in B73-LaneB.

**Description**: After an ATM fill on the leader account, spurious PTT-Copy bracket orders
appear on the follower Sim102 account. Investigation starting point: `DispatchCopy` Gate 0.5
(`IsExitSignalName` check) and Gate A (`IsFollowerAccount` check) for the bracket order
dispatch path.

---

### DW-B54-01 — ATM auto-inject (blocked)

**Priority**: P1
**Target block**: future (blocked — requires StrategyBase-level NT8 API unavailable in AddOnBase)
**Status**: OPEN (blocked) — no change in B73-LaneB.

**Description**: `AtmStrategyCreate()` is confirmed `StrategyBase`-only per
`NT8_FULL_REFERENCE.md`. The `AddOnBase` (`TradeCopierAddOn`) cannot call this API. A
companion `StrategyBase` add-in would be required. Deferred indefinitely pending Director
architectural decision.

---

### DW-B58-01 — SnapshotTargetsPublic hardcoded order-name prefixes

**Priority**: P2
**Target block**: future
**Status**: OPEN — no change in B73-LaneB.

**Description**: `SnapshotTargetsPublic` checks for hardcoded prefixes `PTT-QX-T` and
`PTT-TGT-`. Future blocks adding new PTT-prefixed target order names must update this method.

---

### DW-B58-02 — GlobalBe non-atomic lazy init

**Priority**: P2
**Target block**: future
**Status**: OPEN — no change in B73-LaneB.

**Description**: `GlobalBe` lazy init uses `if (_globalBe == null) _globalBe = new ...`
(non-atomic). Currently safe — both callers access exclusively from the WPF UI thread. If a
future block introduces a non-UI-thread caller, `Interlocked.CompareExchange` will be required.

---

### DW-B58-03 — RelayBe does not forward OcoGroup from BeEventArgs

**Priority**: P2
**Target block**: future
**Status**: OPEN — no change in B73-LaneB.

**Description**: `RelayBe` does not forward `OcoGroup` from `BeEventArgs` to `SubmitBeStop`.
If a future block requires correlated OcoId fan-out across accounts for a single BE event, a
new `SubmitBeStop` overload accepting an explicit `OcoGroup` will be needed.

---

### PRE-EXISTING-01 — Non-ASCII characters at CopyEngine.cs lines 398, 499

**Priority**: P2
**Status**: OPEN — pre-existing. Not introduced by B73-LaneB.

**Description**: Em-dash Unicode characters in B56 BUILD-FIX stub markers (comment lines only).
Not in the B73-LaneB modification region (TradeCopierPanel.cs only).

---

### PRE-EXISTING-02 — Non-ASCII characters at CopyEngine.cs lines ~1449-1450

**Priority**: P2
**Status**: OPEN — pre-existing. Not introduced by B73-LaneB.

**Description**: Unicode arrow characters in exit-order direction comments. B73-LaneB does not
touch CopyEngine.cs, so line numbers are unchanged from B66-LaneC estimate.

---

### PRE-EXISTING-03 — deploy-sync.ps1 archived; PropTraderTools sync is manual

**Priority**: P2
**Status**: OPEN — pre-existing infrastructure state. No change in B73-LaneB.

**Description**: `deploy-sync.ps1` is archived and maps V12_002 strategy files, not
PropTraderTools AddOn files. Manual SHA-256 copy + `verify_links.ps1 -Fix` is the current
PropTraderTools deploy workflow.

---

## B72-LaneA Tracking Gap Note

`docs/brain/B72-LaneA/ticket-1-completion.md` is absent. This is a **parallel-lane pipeline
tracking gap**, not a B73-LaneB defect. The CopyEngine.cs source is present and functional;
B73Tests.cs compiles and passes independently. Director should ensure B72-LaneA
ticket-1-completion.md is written retroactively.

---

## Summary Table

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-B73-B-01 | `RaiseBeAllDisarmed` fires on every flat regardless of per-account slot ownership — redundant broadcasts, no correctness impact | P2 | B75+ | OPEN |
| DW-B73-B-02 | `UpdateBeAllVisuals` creates unfrozen `SolidColorBrush` instances on every call | P2 | future | OPEN |
| DW-B66-BE-01 | `CancelQxBrackets` cancels `PTT-BE-Stop` on Quick Exit — Director confirmation required | P1 | B67+ | OPEN |
| DW-B66-C-02 | `DispatchCopy` dedup key = 0.0 for all StopLimit entries (Gate 5) | P1 | B67+ | OPEN |
| DW-B63-01 | Spurious `PTT-Copy` bracket orders on Sim102 after ATM fill | P1 | B67+ | OPEN |
| DW-B54-01 | ATM auto-inject — blocked, `StrategyBase`-level API required | P1 | future (blocked) | OPEN |
| DW-B58-01 | `SnapshotTargetsPublic` hardcoded prefixes | P2 | future | OPEN |
| DW-B58-02 | `GlobalBe` non-atomic lazy init | P2 | future | OPEN |
| DW-B58-03 | `RelayBe` `OcoGroup` not forwarded | P2 | future | OPEN |
| PRE-EXISTING-01 | Non-ASCII em-dash `CopyEngine.cs` lines 398, 499 | P2 | future | OPEN |
| PRE-EXISTING-02 | Non-ASCII arrow `CopyEngine.cs` lines ~1449-1450 | P2 | future | OPEN |
| PRE-EXISTING-03 | `deploy-sync.ps1` archived; PropTraderTools sync is manual | P2 | future | OPEN |

**Closed this block**: 0 (plus 15 NO-PIPELINE-REPAIRS.md hotfixes formally pipelined)
**New items this block**: 2 (DW-B73-B-01, DW-B73-B-02)
**Carry-forward OPEN from B66-LaneC**: 10 items
**Total OPEN**: 12 items
