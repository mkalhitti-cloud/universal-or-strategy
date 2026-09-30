# B39-LaneA Final Review — Global BE All
<!-- Phase 5 — ptt-plan-reviewer | 2026-07-30 -->

**Reviewer**: ptt-plan-reviewer (Phase 5)
**Epic**: PTT-COPIER B39 — Global BE All
**Spec**: `specs/002-trade-copier-spec.html` id="section-b39"
**Brain dir**: `docs/brain/B39-LaneA/`
**Prior deferred items entering B39**: 0 (B38 closed all items)

---

## Documents Read

| Document | Status |
|----------|--------|
| `02-architecture-plan.md` | REVIEW_PASS (Rev 2 — JS-008 fix applied) |
| `04-ticket-review.md` | TICKET_REVIEW_PASS (Rev 3 — F4 clamp tests added) |
| `ticket-1-completion.md` | BUILD_PASS |
| `ticket-1-verification.md` | VERIFY_PASS |
| `ticket-2-completion.md` | BUILD_PASS |
| `ticket-2-verification.md` | VERIFY_PASS |
| `docs/brain/B38-LaneA/06-deferred-backlog.md` | Read — zero open deferred items |
| `specs/002-trade-copier-spec.html` id="section-b39" | Read — lines 17297–17869 |
| `docs/standards/jane-street/RULES_CATALOG.md` | Read |

---

## Source Files Verified (Wave workspace — read-only)

| File | Action | Lines verified |
|------|--------|----------------|
| `src/PropTraderTools/Features/PttGlobalBreakEven.cs` | NEW | Full file (88 lines) |
| `src/PropTraderTools/CopyEngine.cs` | MODIFIED | Lines 37–105 |
| `src/PropTraderTools/TradeCopierPanel.cs` | MODIFIED | Lines 207–217, 808–935 |
| `src/PropTraderTools/TradeCopierWindow.cs` | MODIFIED | Lines 60–76, 210–270, 850–891 |
| `src/PropTraderTools/CopyEngineTests.cs` | MODIFIED | Lines 3601–3901 (last 300) |

---

## Section A — Build Tag

| Check | Expected | Actual (source) | Verdict |
|-------|----------|-----------------|---------|
| `CopyEngine.cs` line 41 | `"PTT-COPIER B39 \| global-be-all \| 2026-07-30"` | `"PTT-COPIER B39 \| global-be-all \| 2026-07-30"` | **PASS** |

---

## Section B — Spec Requirements Coverage (12/12)

| # | Requirement | Source Evidence | Verdict |
|---|-------------|-----------------|---------|
| 1 | `PttGlobalBreakEven.cs` exists in `Features/` | File read confirmed at `c:\WSGTA\...\Features\PttGlobalBreakEven.cs` (88 lines) | **PASS** |
| 2 | `Execute(int)` iterates `Account.All × positions` | `PttGlobalBreakEven.cs` lines 36–46: `foreach (var acc in Account.All)` + inner `foreach (var pos in acc.Positions)` | **PASS** |
| 3 | Direction-aware bePrice with tick alignment | `PttGlobalBreakEven.cs` lines 64–73: `isLong ? bufferTicks : -bufferTicks`, `Math.Round(.../ tickSize) * tickSize` | **PASS** |
| 4 | `volatile int _globalBeBuffer = 0` | `PttGlobalBreakEven.cs` line 18: `private volatile int _globalBeBuffer = 0;` | **PASS** |
| 5 | `IncrementBuffer/DecrementBuffer` clamp ±10 | Lines 77–85: `if (_globalBeBuffer < 10) _globalBeBuffer++;` / `if (_globalBeBuffer > -10) _globalBeBuffer--;` | **PASS** |
| 6 | Panel Row 2 right: BE ALL button (purple) replaces Cancel | `TradeCopierPanel.cs` lines 842–869: `_globalBeBtn2` with `BrushPurple`, `BorderThickness=2`, arrows wired | **PASS** |
| 7 | Panel Row 3: `UniformGrid Columns=2` Cancel\|COPY ON/OFF | Lines 873–888: `var row3 = new UniformGrid { Columns = 2, ... }` + `_cancelBtn2` left + `_copyToggleBtn2` right | **PASS** |
| 8 | Window global toolbar row above rulesPanel | `TradeCopierWindow.cs` lines 211–251: `globalBeToolbar` inserted before `rulesScroll` with `DockPanel.SetDock(globalBeToolbar, Dock.Top)` | **PASS** |
| 9 | `CopyEngine.GlobalBe` property (Option A shared instance) | `CopyEngine.cs` line 99: `internal PttGlobalBreakEven GlobalBe { get; } = new PttGlobalBreakEven();` | **PASS** |
| 10 | `SubmitBeStop` made `internal` (was `private`) | Confirmed at `CopyEngine.cs` line 1573 via T1-verification SCAN-07 independent re-run | **PASS** |
| 11 | 8 new `[Fact]` tests (T_B39_01..T_B39_08) | `CopyEngineTests.cs` lines 3752–3898: all 8 test methods present and correctly asserted | **PASS** |
| 12 | `[Fact]` count ≥ 202 (was 194 at B38 baseline, +8) | T2 verification SCAN-07: count = 202 confirmed independently | **PASS** |

**Spec coverage: 12/12 PASS**

> **Note on test count**: The spec requires 6 tests (T_B39_01..T_B39_06). The plan revision (Rev 2) and ticket review (F4 fix) added T_B39_07 and T_B39_08 to cover `IncrementBuffer` / `DecrementBuffer` clamp behaviour. Final count = 8 ≥ 6. The ≥ 202 [Fact] target is met.

---

## Section C — Cross-File JS/NT8 Violations

| Rule | Scan Command | Files Scanned | Result | Verdict |
|------|-------------|---------------|--------|---------|
| JS-021 no `lock()` | `^\s*lock\s*\(` | PttGlobalBreakEven.cs, TradeCopierPanel.cs, TradeCopierWindow.cs, CopyEngineTests.cs | **0 actual lock() statements** — `lock(` appears in a comment on PttGlobalBreakEven.cs line 4 only | **PASS** |
| JS-033 no `async void` | `async\s+void\s+\w` | All 4 new/modified files | **0 hits** — all handlers are synchronous `void` | **PASS** |
| JS-002 no `return null` (new code) | `return null` in new code blocks | PttGlobalBreakEven.cs | **0 actual `return null;`** — lines 4 and 63 are comment-only hits; `ExecuteOne` uses early `return` (void) | **PASS** |
| JS-001 no `throw new` (hot path) | `throw\s+new` | PttGlobalBreakEven.cs | **0 hits** — no exception throwing anywhere in new code | **PASS** |
| JS-008 SolidColorBrush Freeze() | Static readonly via factory | TradeCopierPanel.cs line 214 | `BrushPurple = MakeBrush(168,85,247)` — `static readonly`, Freeze() called internally by `MakeBrush` | **PASS** |
| JS-008 SolidColorBrush Freeze() | Static readonly via factory | TradeCopierWindow.cs line 69 | `WBrushPurple = MakeWinBrush(168,85,247)` — `static readonly`, Freeze() called internally | **PASS** |
| JS-008 SolidColorBrush Freeze() | Static readonly via factory | TradeCopierWindow.cs line 70 | `WBrushFlash = MakeWinBrush(34,197,94)` — `static readonly`, Freeze() called internally | **PASS** |
| NT8-003 no `volatile double` | `volatile.*double` | PttGlobalBreakEven.cs | Only `volatile int _globalBeBuffer` — no volatile double | **PASS** |
| NT8-001 no `{ get; init; }` | `get;\s*init;` | CopyEngine.cs | `GlobalBe { get; }` is a getter-only auto-property (not init) | **PASS** |
| JS-010 singleton constructor | public constructor check | CopyEngine.cs | `private CopyEngine() { }` — constructor is private; `PttGlobalBreakEven` constructors are `internal` (not a singleton) | **PASS** |
| NT8 no `sealed` on TradeCopierWindow | sealed keyword | TradeCopierWindow.cs | `public class TradeCopierWindow : Window` — not sealed | **PASS** |
| NT8 no FontFamily | FontFamily | All B39 files | No FontFamily reference in any new code | **PASS** |
| NT8 no DateTime.Now | DateTime.Now | All B39 files | Not present in any new code | **PASS** |
| NT8 no hardcoded hex | #RRGGBB string literals | All B39 files | All colors use `Color.FromRgb()` via `MakeBrush`/`MakeWinBrush` — no string hex literals | **PASS** |
| ASCII-only | Non-ASCII literals | All B39 files | `\u25B2`/`\u25BC` used as escape sequences — no raw non-ASCII character literals | **PASS** |

**Cross-file violation count: 0. All checks PASS.**

> **Panel green flash brush note**: `OnGlobalBeClick` (Panel) uses `BrushActive` — the existing `static readonly SolidColorBrush BrushActive = MakeBrush(34, 197, 94)` — for the green flash, rather than a dedicated `BrushFlash` alias. `BrushActive` is identical RGB (`#22c55e`) to the plan's specified flash colour. This is **functionally equivalent** and **JS-008 compliant** (frozen static readonly). The plan §5.4 specifies "green flash" without mandating a distinct field name. Non-blocking.

---

## Section D — CYC Coherence

| Method | File | CYC | Budget | Verdict |
|--------|------|-----|--------|---------|
| `Execute(int)` | PttGlobalBreakEven.cs | 5 | ≤ 8 | **PASS** |
| `Execute(IEnumerable<Account>, int)` | PttGlobalBreakEven.cs | 5 | ≤ 8 | **PASS** |
| `ExecuteOne(Account, Position, int)` | PttGlobalBreakEven.cs | 4 | ≤ 8 | **PASS** |
| `GlobalBeBuffer` (property) | PttGlobalBreakEven.cs | 1 | ≤ 8 | **PASS** |
| `IncrementBuffer()` | PttGlobalBreakEven.cs | 2 | ≤ 8 | **PASS** |
| `DecrementBuffer()` | PttGlobalBreakEven.cs | 2 | ≤ 8 | **PASS** |
| `OnGlobalBeClick` | TradeCopierPanel.cs | 3 | ≤ 8 | **PASS** |
| `OnGlobalBeUp` | TradeCopierPanel.cs | 2 | ≤ 8 | **PASS** |
| `OnGlobalBeDown` | TradeCopierPanel.cs | 2 | ≤ 8 | **PASS** |
| `FormatGlobalBeBuffer(string, int)` | TradeCopierPanel.cs | 3 | ≤ 8 | **PASS** |
| `OnWindowGlobalBeClick` | TradeCopierWindow.cs | 3 | ≤ 8 | **PASS** |
| `OnWindowGlobalBeUp` | TradeCopierWindow.cs | 2 | ≤ 8 | **PASS** |
| `OnWindowGlobalBeDown` | TradeCopierWindow.cs | 2 | ≤ 8 | **PASS** |
| `FormatWindowGlobalBe(string, int)` | TradeCopierWindow.cs | 3 | ≤ 8 | **PASS** |

**Maximum CYC across all new methods: 5. All ≤ 8 absolute budget. PASS.**

L2 (engineer) and L3 (verifier) CYC counts are in full agreement. Zero discrepancies.

---

## Section E — Test Coverage Coherence

| ID | Method | Assert | Source Line | Verdict |
|----|--------|--------|-------------|---------|
| T_B39_01 | `GlobalBe_FiresOnAllAccountsAllInstruments` | 3 accs × 2 pos = 6 calls | L3754 `Assert.Equal(6, calls.Count)` | **PASS** |
| T_B39_02 | `GlobalBe_SkipsFlatAccounts` | flat skipped; 1 call | L3794 `Assert.Equal(1, calls.Count)` | **PASS** |
| T_B39_03 | `GlobalBe_WorksWithNoCopyRule` | no rule dep; 1 call | L3809 `Assert.Equal(1, calls.Count)` | **PASS** |
| T_B39_04 | `GlobalBe_B35GuardInherited_UnderwaterSkipped` | extreme buf; no exception; 1 call | L3824 `Assert.Null(ex)` + L3825 `Assert.Equal(1, ...)` | **PASS** |
| T_B39_05 | `GlobalBe_BufferAppliedPerDirectionCorrectly` | long=7500.50, short=7499.50 | L3845 + L3846 `Assert.Equal(..., precision:5)` | **PASS** |
| T_B39_06 | `GlobalBe_AllAccountsFlat_NoCalls` | 3 flat accs; 0 calls; no exception | L3866 `Assert.Null(ex)` + L3867 `Assert.Equal(0, ...)` | **PASS** |
| T_B39_07 | `GlobalBeBuffer_IncrementClampedAt10` | 11 increments → `GlobalBeBuffer == 10` | L3882 `Assert.Equal(10, globalBe.GlobalBeBuffer)` | **PASS** |
| T_B39_08 | `GlobalBeBuffer_DecrementClampedAtMinus10` | 11 decrements → `GlobalBeBuffer == -10` | L3897 `Assert.Equal(-10, globalBe.GlobalBeBuffer)` | **PASS** |

**Test coverage: 8/8 spec IDs present and correctly asserted. [Fact] total = 202. PASS.**

T_B39_05 math independently verified: long `7500.00 + 2 × 0.25 = 7500.50` ✅; short `7500.00 − 2 × 0.25 = 7499.50` ✅.
T_B39_07/T_B39_08 clamp logic: 10 increments → buffer=10; one more call → guard `if (_globalBeBuffer < 10)` is false → stays 10 ✅.

---

## Section F — Scope Creep Audit

| File | B39 scope? | Notes |
|------|-----------|-------|
| `src/PropTraderTools/Features/PttGlobalBreakEven.cs` | YES — NEW | Fully in scope |
| `src/PropTraderTools/CopyEngine.cs` | YES — 3 edits | Build tag, SubmitBeStop access, GlobalBe property |
| `src/PropTraderTools/TradeCopierPanel.cs` | YES — layout + handlers | Row 2/3 + handlers + helper + brush field |
| `src/PropTraderTools/TradeCopierWindow.cs` | YES — toolbar row + handlers | Global toolbar + handlers + helper + brush fields |
| `src/PropTraderTools/CopyEngineTests.cs` | YES — 8 new [Fact] | T_B39_01..T_B39_08 + 6 private static helpers |
| `src/PropTraderTools/PropTraderTools.csproj` | YES — compile entry | `<Compile Include="Features\PttGlobalBreakEven.cs" />` added |

**No files outside the B39 spec scope were modified. Scope creep: none. PASS.**

---

## Section G — Wiring Coherence

| Handler | Surface | Wired to | Verdict |
|---------|---------|----------|---------|
| `OnGlobalBeClick` | TradeCopierPanel | `CopyEngine.Instance.GlobalBe.Execute(GlobalBeBuffer)` + 500ms green flash via `DispatcherTimer` | **PASS** |
| `OnGlobalBeUp` | TradeCopierPanel | `CopyEngine.Instance.GlobalBe.IncrementBuffer()` + label update | **PASS** |
| `OnGlobalBeDown` | TradeCopierPanel | `CopyEngine.Instance.GlobalBe.DecrementBuffer()` + label update | **PASS** |
| `OnWindowGlobalBeClick` | TradeCopierWindow | `CopyEngine.Instance.GlobalBe.Execute(GlobalBeBuffer)` + 500ms green flash via `DispatcherTimer` | **PASS** |
| `OnWindowGlobalBeUp` | TradeCopierWindow | `CopyEngine.Instance.GlobalBe.IncrementBuffer()` + label update | **PASS** |
| `OnWindowGlobalBeDown` | TradeCopierWindow | `CopyEngine.Instance.GlobalBe.DecrementBuffer()` + label update | **PASS** |

Green flash pattern: `button.Background = BrushXxx; t = new DispatcherTimer { Interval = 500ms }; t.Tick += (s,ev) => { button.ClearValue(BackgroundProperty); t.Stop(); }; t.Start()` ✅

Option A (shared instance via `CopyEngine.Instance.GlobalBe`) correctly implemented on both surfaces — buffer stays in sync. ✅

---

## Section H — Test Seam Coherence

| Check | Evidence | Verdict |
|-------|----------|---------|
| Injection constructor `PttGlobalBreakEven(Action<Account, Instrument, double>)` present | `PttGlobalBreakEven.cs` lines 30–33: `internal PttGlobalBreakEven(Action<Account, Instrument, double> submitBeStop)` | **PASS** |
| `Execute(IEnumerable<Account>, int)` test-seam overload present | `PttGlobalBreakEven.cs` lines 50–60 | **PASS** |
| All 8 tests use injection ctor (no `Account.All` in tests) | `CopyEngineTests.cs` — every test calls `new PttGlobalBreakEven((a, i, p) => calls.Add(...))` | **PASS** |
| Tests T_B39_01..T_B39_06 call `Execute(IEnumerable<Account>, int)` overload | Confirmed — bypasses NT8 static collection entirely | **PASS** |
| Tests T_B39_07 / T_B39_08 do not call `Execute` at all (buffer only) | Confirmed — call only `IncrementBuffer()` / `DecrementBuffer()` | **PASS** |

---

## Section I — Prior Deferred Items Review

B38 deferred backlog (`docs/brain/B38-LaneA/06-deferred-backlog.md`) states:

> **"None — all B38 spec requirements are fully satisfied."**

The single informational item (`DW-B38-OOS-01` — `TimeInForce.Day` for PTT-Click **entry** orders) was marked as out-of-scope per V12.23 No Scope Creep Protocol and explicitly **not a defect in its current context**. It carried no obligation into B39.

**Conclusion**: Zero B38 deferred items required addressing in B39. No obligations missed. PASS.

---

## Section J — Pre-existing Baseline Errors

| Error | File | Location | Type | Present before B39? | In B39 scope? |
|-------|------|----------|------|---------------------|---------------|
| CS0234 `'Indicators' not in 'NinjaTrader.NinjaScript'` | AtrSizingEngine.cs | Line 20 | Compile error | YES — in git HEAD before B39 (B38 baseline) | NO |
| CS0246 `'Indicator' type not found` | AtrSizingEngine.cs | Line 24 | Compile error | YES — in git HEAD before B39 (B38 baseline) | NO |
| CS8632 nullable annotation warning | CopyEngine.cs | Line 683 | Warning | YES — pre-existing B38 warning | NO |

**Root cause**: `AtrSizingEngine.cs` inherits NT8's `Indicator` base class which requires NT8 runtime assemblies unavailable in the standalone MSBuild host. NT8 compiles these files via its own internal Roslyn host. The errors are structural to the standalone build environment, not introduced by B39.

**B39 introduced zero new compilation errors. PASS (B39-scope).**

---

## Section K — Deferred Work (REQUIRED)

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-B39-OOS-01 | Keyboard shortcut for BE ALL (e.g. Shift+G via `PreviewKeyDown` on AddOn window) | P2 | B40+ | OPEN |
| DW-B39-OOS-02 | `PttBus.GlobalBeFired` pub-sub event — not needed in B39 since each `SubmitBeStop` call handles its own fan-out. Deferred if future orchestration requires a single-fire notification | P2 | future | OPEN |
| DW-B39-OOS-03 | Armed state / `ArmPendingGlobalBe` state machine — spec §armed explicitly specifies "fires immediately, no armed state" for B39. If armed mode ever required for global BE, new block | P2 | future | OPEN |
| DW-B39-OOS-04 | BE-target limit order handling for global BE — SubmitBeStop submits a stop; limit order variant for global BE is a separate architectural concern | P2 | future | OPEN |
| DW-B38-OOS-01 | `TimeInForce.Day` in PTT-Click **entry** order at `TradeCopierPanel.cs:1397` — intentionally Day TIF for entry orders. Out of scope unless spec changes entry-order TIF policy | P2 | future | OPEN (inherited from B38, still out of scope) |
| DW-B39-OOS-05 | Visual buffer sync between Panel and Window (auto-label refresh when one surface spinner is used) — best-effort per plan §7; buffer is shared but label only updates on the surface where the spinner was clicked | P2 | B40+ | OPEN |
| DW-B39-INFO-01 | `AtrSizingEngine.cs` pre-existing CS0234/CS0246 compile errors in standalone MSBuild — structural to the build environment, not a B39 defect. Should be resolved in a dedicated infrastructure block | P1 | future | OPEN |

---

## 7-Scan Aggregate (across all B39 src/ files)

| Scan | Scope | Result |
|------|-------|--------|
| SCAN-01 `lock()` | All 4 B39 source files | 0 actual lock() statements |
| SCAN-02 `async void` | All 4 B39 source files | 0 hits |
| SCAN-03 `return null` (new code) | PttGlobalBreakEven.cs | 0 actual — 2 comment-only |
| SCAN-04 `throw new` (new code) | PttGlobalBreakEven.cs | 0 hits |
| SCAN-05 CYC ≤ 8 all new methods | All new methods across 4 files | Max CYC = 5; all ≤ 8 |
| SCAN-06 dotnet build | PropTraderTools.csproj | 2 pre-existing AtrSizingEngine errors; 0 B39-introduced errors |
| SCAN-07 [Fact] count | CopyEngineTests.cs | 202 (was 194; +8 B39 tests) |

All 7 scans return zero violations within B39 scope. PASS.

---

## Cross-File Coherence Summary

| Coherence Check | Finding |
|----------------|---------|
| PttGlobalBreakEven ↔ CopyEngine | `CopyEngine.GlobalBe` holds the singleton; production ctor lambda captures `CopyEngine.Instance` at call time (no circular init) |
| CopyEngine.GlobalBe ↔ TradeCopierPanel | Panel reads `CopyEngine.Instance.GlobalBe.*` — shared buffer; no separate instance |
| CopyEngine.GlobalBe ↔ TradeCopierWindow | Window reads `CopyEngine.Instance.GlobalBe.*` — same singleton; full sync |
| Panel Row 2 ↔ Row 3 layout | Cancel moved from Row 2 to Row 3 UniformGrid; COPY ON/OFF narrowed to half-width; Row 4 (Risk/ATR) untouched |
| SubmitBeStop access | Changed `private` → `internal`; no logic change; allows PttGlobalBreakEven production ctor to form the lambda |
| Test seam ↔ Production path | Injection ctor delegates to same `ExecuteOne` logic as production; seam is clean and isolated |

---

## Final Verdict

**FINAL_PASS**

All 12 spec requirements satisfied. Zero JS/NT8 DNA violations across all 5 modified files. CYC max = 5 (well within ≤ 8 budget). 8/8 test IDs present. [Fact] count = 202. No scope creep. All 7 scans returned 0 violations within B39 scope. L2 and L3 scan results in full agreement (0 discrepancies). Cross-file wiring coherent. Test seam sound. Prior deferred items: none from B38. Pre-existing baseline errors documented and out of scope.

Section K (deferred work) written with 7 items (5 new B39 OOS + 1 inherited B38 OOS + 1 infrastructure info item).

---

*Generated by ptt-plan-reviewer | Phase 5 Final Review | B39-LaneA | 2026-07-30*
