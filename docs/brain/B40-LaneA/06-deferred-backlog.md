# B40-LaneA Deferred Backlog

**Block**: B40-LaneA — BE ALL Armed/Wait + OCO Collision Fix
**Date Closed**: 2026-07-31
**Engineer**: ptt-engineer
**Reviewer**: ptt-plan-reviewer

---

## Features Delivered This Block

| Feature | Files | Key Details |
|---------|-------|-------------|
| **FIX (P0)** DW-B39-OCO-01: `BuildGlobalBeOcoId` static helper + `SubmitBeStop` optional `ocoOverride` param | `PttGlobalBreakEven.cs`, `CopyEngine.cs` | `"PTT-BEG-" + seq.ToString("D5") + "-" + accIdx + "-" + pairIndex`; ocoOverride conditional in per-pair loop; eliminates Sim101/Sim102 collision |
| **FIX (P1)** DW-B39-BEHAVIOR-01 (engine): `ArmAllPendingBe(int bufferTicks)` — loops Account.All, immediate-fire or arm per account; `IsPendingSlotsEmpty()` — `_pendingBeSlots.IsEmpty`; `ComputeBePrice` (×2 overloads); `IsPriceAlreadyAtBeForAccount` | `CopyEngine.cs` | CYC: ArmAllPendingBe=5, IsPriceAlreadyAtBeForAccount=4, ComputeBePrice=2, IsPendingSlotsEmpty=1; all `internal` for test access |
| **FIX (P1)** DW-B39-BEHAVIOR-01 (PttGlobalBreakEven): `Execute(int)` body rewritten to delegate; `_ocoSeq volatile int` field added | `PttGlobalBreakEven.cs` | Old inner Account.All loop removed; CYC=1; test-seam `Execute(IEnumerable<Account>, int)` unchanged |
| **FIX (P1)** DW-B39-BEHAVIOR-01 (UI — Panel): `_globalBeState` field; `OnGlobalBeClick` FSM rewritten; `OnPendingBeFiredDispatch` auto-reset; `UpdateBeAllVisuals`; `Detach()` cleanup loop; `BeState` promoted to `internal` | `TradeCopierPanel.cs` | CYC: OnGlobalBeClick=4, UpdateBeAllVisuals=2, OnPendingBeFiredDispatch=2; amber armed / purple idle |
| **FIX (P1)** DW-B39-BEHAVIOR-01 (UI — Window): `_windowGlobalBeState` field; `OnWindowGlobalBeClick` FSM; `OnWindowPendingBeFiredDispatch`; `UpdateWindowBeAllVisuals`; `OnWindowClosed` cleanup loop; `PendingBeFired` subscribe/unsubscribe | `TradeCopierWindow.cs` | Mirrors Panel exactly; CYC: OnWindowGlobalBeClick=4, UpdateWindowBeAllVisuals=2, OnWindowPendingBeFiredDispatch=2 |
| **TESTS** T_B40_01–T_B40_15: `BuildGlobalBeOcoId` format + uniqueness (5 tests), `ComputeBePrice` long/short/edge (5 tests), `IsPendingSlotsEmpty` state lifecycle (3 tests), coverage extensions (2 tests) | `CopyEngineTests.cs` | [Fact] count: 201→216 (+15); all CYC=1; xUnit only; all pass |

**Build tag**: `"PTT-COPIER B40 | be-all-armed-oco-fix | 2026-07-30"`
**Baseline tag**: `"PTT-COPIER B39 | global-be-all | 2026-07-30"`
**[Fact] delta**: +15 (201→216)
**New errors introduced**: 0 (2 pre-existing AtrSizingEngine errors exempt per DW-B39-INFO-01)

---

## Deferred Items

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-B40-TEST-01 | Add `CopyEngine.CreateForTest` factory seam (fake account list + `onSubmitBeStop` delegate). Unblocks direct unit tests for `ArmAllPendingBe` all-flat, above-threshold, below-threshold, and mixed-account paths. T_B40_A01–T_B40_A04 to be written once seam is in place. | P1 | B41 | OPEN |
| DW-B40-TEST-02 | `IsPriceAlreadyAtBeForAccount` (private) — testable via `ArmAllPendingBe` with injectable market data once DW-B40-TEST-01 seam exists. Requires `longAccountBid`/`shortAccountAsk` injection. Both long bid ≥ bePrice (returns true, immediate fire) and long bid < bePrice (returns false, armed) paths need coverage. | P1 | B41 | OPEN |
| DW-B40-TEST-03 | `SubmitBeStop ocoOverride` string construction path — the literal code that fixes P0 DW-B39-OCO-01. Requires `onCreateOrderOcoId: Action<string>` hook in `CreateForTest` (or extraction of OCO ID construction into a testable pure helper). Until direct test exists, the P0 fix is verified only by integration testing (F5 + live sim multi-account). | P0 | B41 | OPEN |
| DW-B39-OOS-01 | Keyboard shortcut for BE ALL (e.g. Shift+G via `PreviewKeyDown` on AddOn window) | P2 | B41+ | OPEN |
| DW-B39-OOS-02 | `PttBus.GlobalBeFired` pub-sub event — deferred if future orchestration requires a single-fire notification across multiple panels | P2 | future | OPEN |
| DW-B39-OOS-04 | BE-target limit order handling for global BE — `SubmitBeStop` submits a stop order; limit order variant for global BE is a separate architectural concern | P2 | future | OPEN |
| DW-B38-OOS-01 | `TimeInForce.Day` in PTT-Click entry order (`TradeCopierPanel.cs:1397`) — correct current behaviour; out of scope unless spec changes entry-order TIF policy | P2 | future | OPEN (inherited from B38) |
| DW-B39-OOS-05 | Visual buffer sync between Panel and Window — `GlobalBeBufferChanged` event on CopyEngine needed; each surface currently updates only its own label when its own spinner is clicked | P2 | B41+ | OPEN |
| DW-B39-INFO-01 | `AtrSizingEngine.cs` pre-existing CS0234/CS0246 compile errors in standalone MSBuild — structural to build environment; must be resolved in a dedicated infrastructure block | P1 | future | OPEN |

---

## Closed Items (B39 deferred items resolved this block)

| B39 Deferred ID | Closed by | Resolution |
|-----------------|-----------|-----------|
| DW-B39-OCO-01 (P0) — OCO ID collision (Sim101/Sim102 share `"Sim1"` prefix) | B40 T1 | `PttGlobalBreakEven.BuildGlobalBeOcoId(seq, accIdx, pairIndex)` produces globally unique IDs; `CopyEngine.SubmitBeStop` `ocoOverride` conditional replaces accName-prefix formula on global BE path. T1 verifier confirmed source at CopyEngine.cs lines 1637–1642 and PttGlobalBreakEven.cs line 82. |
| DW-B39-BEHAVIOR-01 (P1) — BE ALL fires immediately; no armed/wait state | B40 T1 + T2 | `CopyEngine.ArmAllPendingBe` delegates to existing `ArmPendingBe` per account (armed/wait) or fires `SubmitBeStop` immediately (price already past threshold). Panel and Window FSM (`_globalBeState`, `_windowGlobalBeState`) track armed state; amber visual on arm, purple on reset. T2 verifier confirmed all 16 source claims. |
| DW-B39-OOS-03 (P2) — Armed state machine for global BE in `PttGlobalBreakEven` itself | B40 T1 (PARTIAL→CLOSED) | The armed/wait behaviour is implemented via delegation to the existing per-account `ArmPendingBe` path. A separate armed-state field inside `PttGlobalBreakEven` is not needed; `_globalBeState` in Panel/Window is the correct home for UI state. Spirit of DW-B39-OOS-03 satisfied. |

---

## Verification Summary

| Layer | Step | Artifact | Verdict |
|-------|------|----------|---------|
| 1 | Plan Review Rev 1 | `02-plan-review.md` | REVIEW_FAIL (V-01 — wrong market data API in IsPriceAlreadyAtBeForAccount) |
| 1 | Plan Review Rev 2 | `02-plan-review.md §Rev 2` | REVIEW_PASS — V-01 resolved; all 24 checks pass |
| 2 | Ticket Review Rev 1 | `04-ticket-review.md` | TICKET_REVIEW_FAIL (T3 missing T_B40_06/07/10; ComputeBePrice visibility; count mismatch) |
| 2 | Ticket Review Rev 2 | `04-ticket-review.md §Rev 2` | TICKET_REVIEW_PASS — all 5 violations cleared; T1/T2/T3 individually pass |
| 3 | T1 Engineer | `ticket-1-completion.md` | BUILD_PASS |
| 4 | T1 Verifier | `ticket-1-verification.md` | VERIFY_PASS — all 7 scans PASS; all source claims confirmed |
| 5 | T2 Engineer | `ticket-2-completion.md` | BUILD_PASS |
| 6 | T2 Verifier | `ticket-2-verification.md` | VERIFY_PASS — all 7 scans PASS; 16/16 claims confirmed; 7/7 spec requirements confirmed |
| 7 | T3 Engineer | `ticket-3-completion.md` | BUILD_PASS — 216 [Fact] |
| 8 | T3 Verifier | `ticket-3-verification.md` | VERIFY_PASS — all 15 T_B40 tests present and meaningful; 216 confirmed |
| 9 | Final Review | `05-final-review.md` | FINAL_PASS |

---

## 7-Scan Aggregate Summary (B40 scope, all 5 files)

| Scan | Pattern | Result |
|------|---------|--------|
| SCAN-01 `lock()` | `lock\s*\(` | **0 actual** — all hits are JS-021 compliance comments |
| SCAN-02 `async void` | `async void ` | **0 actual** — hits at Panel:941 and Window:877 are JS-033 comments |
| SCAN-03 `return null;` (new code) | `return null;` | **0 new** — 4 pre-existing at CopyEngine.cs only; none in B40 new methods |
| SCAN-04 `throw new` (new code) | `throw new ` | **0** |
| SCAN-05 CYC | manual (complexity_audit.py absent) | **0 violations** — max CYC=5 (ArmAllPendingBe); all 9 new methods ≤ 8 |
| SCAN-06 `[Fact]` count | `^\s*\[Fact\]` | **216** standalone [Fact]; 0 new build errors; 2 pre-existing AtrSizingEngine errors exempt |
| SCAN-07 `verify_links.ps1` | hard-link integrity | **OK=12, DESYNC=0** |

---

## Next Block Guidance

Candidates for B41+, in priority order:

1. **`CopyEngine.CreateForTest` seam** (`DW-B40-TEST-01`): The highest-leverage single addition — one factory method unblocks DW-B40-TEST-01, DW-B40-TEST-02, and DW-B40-TEST-03 simultaneously. The seam accepts a fake account list, optional `onSubmitBeStop` delegate, and optional `onCreateOrderOcoId` delegate. Estimated: ~40 lines in CopyEngine.cs + ~30 lines of new tests. Should be a standalone micro-ticket before the next feature block.
2. **`SubmitBeStop ocoOverride` direct test** (`DW-B40-TEST-03`, P0): Once DW-B40-TEST-01 seam exists, write `T_B40_10` asserting `capturedOcoId == ocoOverride + "-0"` for pair 0. The P0 OCO fix has no direct unit coverage today.
3. **Keyboard shortcut for BE ALL** (`DW-B39-OOS-01`, P2): `PreviewKeyDown` on AddOn window; `Shift+G` → `GlobalBe.Execute(...)`. Low risk, small scope, high usability impact.
4. **Visual buffer sync across surfaces** (`DW-B39-OOS-05`, P2): Add `GlobalBeBufferChanged` event on CopyEngine; Panel and Window subscribe and refresh their buffer labels. Requires CopyEngine event plumbing — small scope.
5. **AtrSizingEngine infrastructure fix** (`DW-B39-INFO-01`, P1): Resolve missing NT8 assembly references in standalone MSBuild. Requires environment configuration, not source code change. Should be isolated in its own infrastructure block.

---

*Generated by ptt-plan-reviewer | Phase 5 Final Review | B40-LaneA | 2026-07-31*
