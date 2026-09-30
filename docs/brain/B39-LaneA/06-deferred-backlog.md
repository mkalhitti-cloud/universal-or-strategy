# B39-LaneA Deferred Backlog

**Block**: B39-LaneA — Global BE All
**Date Closed**: 2026-07-30
**Engineer**: ptt-engineer
**Reviewer**: ptt-plan-reviewer

---

## Features Delivered This Block

| Feature | Files | Lines |
|---------|-------|-------|
| **NEW** `PttGlobalBreakEven.cs` — `Execute(int)`, `Execute(IEnumerable<Account>, int)`, `ExecuteOne()`, `IncrementBuffer()`, `DecrementBuffer()`, `GlobalBeBuffer` property, injection test seam | `src/PropTraderTools/Features/PttGlobalBreakEven.cs` | 88 |
| **CopyEngine.cs** — build tag B38→B39, `SubmitBeStop` private→internal, `GlobalBe` singleton property | `src/PropTraderTools/CopyEngine.cs` | 3 surgical edits |
| **TradeCopierPanel.cs** — Row 2 right: BE ALL button (purple) + ▲▼; Row 3: `UniformGrid` Cancel + COPY ON/OFF; `BrushPurple` field; `OnGlobalBeClick/Up/Down` handlers; `FormatGlobalBeBuffer` helper | `src/PropTraderTools/TradeCopierPanel.cs` | ~45 net new lines |
| **TradeCopierWindow.cs** — Global toolbar row above rulesScroll: BE ALL button (purple) + ▲▼; `WBrushPurple`, `WBrushFlash` fields; `OnWindowGlobalBeClick/Up/Down` handlers; `FormatWindowGlobalBe` helper | `src/PropTraderTools/TradeCopierWindow.cs` | ~55 net new lines |
| **CopyEngineTests.cs** — 8 new `[Fact]` tests (T_B39_01..T_B39_08) + 6 private static stub helpers; total `[Fact]` count = 202 | `src/PropTraderTools/CopyEngineTests.cs` | +209 lines (3693–3901) |
| **PropTraderTools.csproj** — `<Compile Include="Features\PttGlobalBreakEven.cs" />` | `src/PropTraderTools/PropTraderTools.csproj` | 1 line |

**Build tag**: `"PTT-COPIER B39 | global-be-all | 2026-07-30"`
**Baseline tag**: `"PTT-COPIER B38 | trim-anchor-be-tif | 2026-07-28"`
**[Fact] delta**: +8 (194 → 202)
**New errors introduced**: 0

---

## Deferred Items

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-B39-OOS-01 | Keyboard shortcut for BE ALL (e.g. Shift+G via `PreviewKeyDown` on AddOn window) | P2 | B40+ | OPEN |
| DW-B39-OOS-02 | `PttBus.GlobalBeFired` pub-sub event — not needed in B39; each `SubmitBeStop` call handles its own fan-out. Deferred if future orchestration requires a single-fire notification | P2 | future | OPEN |
| DW-B39-OOS-03 | Armed state / `ArmPendingGlobalBe` state machine — spec §armed explicitly says "fires immediately, no armed state." If armed mode ever required for global BE, new block | P2 | future | OPEN |
| DW-B39-OOS-04 | BE-target limit order handling for global BE — `SubmitBeStop` submits a stop order; limit order variant for global BE is a separate architectural concern | P2 | future | OPEN |
| DW-B38-OOS-01 | `TimeInForce.Day` in PTT-Click **entry** order (`TradeCopierPanel.cs:1397`) — intentionally Day TIF for entry orders; correct current behaviour. Out of scope unless spec changes entry-order TIF policy | P2 | future | OPEN (inherited from B38) |
| DW-B39-OOS-05 | Visual buffer sync between Panel and Window — best-effort per plan §7. Buffer is shared (Option A), but each surface only updates its own label when its own spinner is clicked. Auto-label refresh across surfaces would require a `GlobalBeBufferChanged` event on CopyEngine | P2 | B40+ | OPEN |
| DW-B39-INFO-01 | `AtrSizingEngine.cs` pre-existing CS0234/CS0246 compile errors in standalone MSBuild — structural to build environment (NT8 Indicator base class requires NT8 runtime assemblies). Should be resolved in a dedicated infrastructure block | P1 | future | OPEN |

---

## Closed Items (inherited open items resolved this block)

None — B38 had zero open deferred items entering B39.

---

## Verification Summary

| Layer | Verdict |
|-------|---------|
| Layer 1 (plan review, 02-architecture-plan.md Rev 2) | REVIEW_PASS |
| Layer 2 (ticket review, 04-ticket-review.md Rev 3) | TICKET_REVIEW_PASS |
| Layer 3 (T1 engineer, ticket-1-completion.md) | BUILD_PASS |
| Layer 4 (T1 verifier, ticket-1-verification.md) | VERIFY_PASS |
| Layer 5 (T2 engineer, ticket-2-completion.md) | BUILD_PASS |
| Layer 6 (T2 verifier, ticket-2-verification.md) | VERIFY_PASS |
| Layer 7 (final review, 05-final-review.md) | FINAL_PASS |

---

## 7-Scan Aggregate Summary (B39 scope)

| Scan | Result |
|------|--------|
| SCAN-01 `lock()` | 0 actual lock() statements across all B39 files |
| SCAN-02 `async void` | 0 hits |
| SCAN-03 `return null` (new code) | 0 actual — comments only |
| SCAN-04 `throw new` (new code) | 0 hits |
| SCAN-05 CYC ≤ 8 | Max CYC = 5; all methods within budget |
| SCAN-06 dotnet build | 0 B39-introduced errors; 2 pre-existing AtrSizingEngine errors (out of scope) |
| SCAN-07 [Fact] count | 202 (was 194; +8) |

---

## Next Block Guidance

Candidates for B40+, in approximate priority:

1. **Keyboard shortcut for BE ALL** (`DW-B39-OOS-01`): `PreviewKeyDown` handler on the AddOn window mapping Shift+G (or user-defined) to `CopyEngine.Instance.GlobalBe.Execute(...)`. Low risk, small scope.
2. **Visual buffer sync across surfaces** (`DW-B39-OOS-05`): Add `GlobalBeBufferChanged` event on `CopyEngine`; Panel and Window subscribe and refresh labels. Requires CopyEngine modification and new event plumbing.
3. **Entry-order TIF policy** (`DW-B38-OOS-01`): If spec ever extends to entry-order TIF, scope as independent block. Not urgent — `TimeInForce.Day` is correct for entry orders.
4. **AtrSizingEngine infrastructure fix** (`DW-B39-INFO-01`): Resolve missing NT8 assembly references in standalone MSBuild. Requires environment configuration, not source code change. Should be isolated in its own infrastructure block.

---

*Generated by ptt-plan-reviewer | Phase 5 Final Review | B39-LaneA | 2026-07-30*
