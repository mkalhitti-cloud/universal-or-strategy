# PTT-COPIER-B7 -- $continue prompt
# Save this file. Paste the block below into a new Director session to continue.

---

PTT Trade Copier — Director session context for new window.
Use Director workspace: c:\WSGTA\universal-or-strategy-director

--- PROJECT STATE ---
Building a NinjaTrader 8 Trade Copier Add-On (PropTraderTools namespace) using a
3-tier orchestrator-worker-validator pipeline. Blocks are additive only — no rewrites
of prior logic. Each block runs as a fresh ptt-orchestrator session (start_subtask only,
never spawn_subagent for pipeline work).

--- BLOCK STATUS ---
B1  COMPLETE  31/31  CopyEngine singleton, 4-gate copy chain, Trim/Flatten/Cancel/dedup
B2  COMPLETE  50/50  5 P0/P1 defect repairs (Subscribe, AddRule, ConcurrentBag, buttons, BorderBrush)
B3  COMPLETE  34/34  CopyRule.Enabled gate 2.5, SetRuleEnabled, PassesDailyCapCheck (real P&L),
                     +Add Rule rows, OnRuleToggle, 17 xUnit [Fact] tests
B4  COMPLETE  24/24  IsFlat+IsStopLeg+MoveStopToBreakEven+BreakEven; BE cluster (button +
                     inline TextBox default "2" + "tks") on both Panel and Window; Shift+B on Panel
B5  COMPLETE  FINAL_PASS  Follower ComboBox -> ListBox (SelectionMode.Extended + ScrollViewer
                     MaxHeight=80) on both Panel and Window; _activeRuleInstrument + MouseEnter
                     tracking on both row builders; Shift+B KeyBinding -> OnWindowBreakEven in
                     Window; RelayCommand nested class in Window; using System.Windows.Input added;
                     2 new [Fact] BreakEven tests (total 19); IDisposable + Dispose() on
                     CopyEngineTests; CopyEngine.cs UNCHANGED
B6  COMPLETE  FINAL_PASS  Rule persistence (SaveRules/LoadRules via XmlSerializer, copy_rules.xml
                     to NT UserDataDir\PropTraderTools\); lifecycle hooks in TradeCopierWindow;
                     3 new xUnit [Fact] persistence tests (total 22); Spec HTML updated (B3-B6);
                     Runtime fixes: NTWindow->Window base class, AddOn wiring, AccountComboBoxStyle
                     guard, Account.All deferred to Loaded handler, duplicate menu guard.
                     DEFERRED BACKLOG: EMPTY after B6 (all DW-B5-03, DW-B5-04 CLOSED).
B7  NEXT      Open items: F5 gate (ChartTrader panel injection not yet tested), then B7 features

--- ACTUAL WAVE SOURCE FILES (c:\WSGTA\universal-or-strategy\src\PropTraderTools\) ---
CopyEngine.cs        534 lines  B6-complete (SaveRules/LoadRules added)
TradeCopierPanel.cs  225 lines  B6-FIX6 (UserControl, SetInstrument/Detach, KeyBinding removed)
TradeCopierWindow.cs 392 lines  B6-FIX4 (plain Window, Account.All in Loaded, LoadRules/SaveRules)
TradeCopierAddOn.cs  135 lines  B6-FIX7 (ControlCenter menu + ChartTrader injection wired)
CopyEngineTests.cs   345 lines  B6-complete (22 xUnit [Fact], IDisposable, Dispose)

--- 7 MANDATORY SCANS (all must be 0 before accepting any block) ---
SCAN-01: lock(           -> 0
SCAN-02: non-ASCII       -> 0
SCAN-03: FontFamily      -> 0
SCAN-04: #RRGGBB         -> 0
SCAN-05: CreateOrder without PTT- prefix -> 0 (all 3 calls carry "PTT-Copy","PTT-Trim","PTT-Flatten")
         NOTE: scan script must look across multi-line span — name arg is on the line AFTER CreateOrder(
SCAN-06: DateTime.Now    -> 0
SCAN-07: sealed class TradeCopierWindow -> 0  (TradeCopierWindow must NOT be sealed — NT8 NTWindow rule)
All 7 confirmed PASS on B6 source.

--- JANE STREET RULES (P0 blocking) ---
JS-021: no lock() anywhere
JS-023: volatile bool _isCopyEnabled
JS-025: ConcurrentDictionary + ConcurrentBag (lock-free)
JS-010: private CopyEngine() constructor (singleton)
JS-003: TrimSignal has NO qty field — correctness by construction
CYC <= 8 on all methods

--- NT8 CONSTRAINTS (hard-won — DO NOT VIOLATE) ---
No async/await in lifecycle methods (OnInitialize, OnDestroyed)
Dispatcher.InvokeAsync for all UI callbacks from off-thread
TradeCopierWindow must NOT be sealed (NTWindow subclass rule)
order.Change(0, newStop, qty) to move stops
Math.Round(raw / tickSize) * tickSize mandatory for stop prices
WPF KeyGesture rejects ALL Shift+letter combos in NT8 host — NEVER use KeyBinding with letter keys
Account.All must NEVER be called in constructors — only in Loaded event handlers
NTWindow cannot be embedded — use UserControl for injectable panels
TradeCopierWindow must extend System.Windows.Window (not NTWindow)
TradeCopierAddOn.OnWindowCreated fires for EVERY NT8 window — use live menu scan for idempotency
ChartTrader injection: use chartTrader.Rows (StackPanel) to append UserControl
  IF "Rows" throws a compile error, try "RowsPanel" — property name varies by NT8 version
AccountComboBoxStyle resource ref: safe in NT8-hosted NTWindow; may throw in plain Window/UserControl
  — guard with try/catch or remove if it causes BuildUI abort

--- DIRECTOR ARTIFACTS (c:\WSGTA\universal-or-strategy-director\) ---
specs/002-trade-copier-spec.html          2263 lines  B1-B6 COMPLETE · B7 next
docs/brain/PTT-COPIER-B1/manifest.json    complete
docs/brain/PTT-COPIER-B2/manifest.json    complete
docs/brain/PTT-COPIER-B3/manifest.json    complete
docs/brain/PTT-COPIER-B4/manifest.json    complete
docs/brain/PTT-COPIER-B5/manifest.json    complete
docs/brain/PTT-COPIER-B6/manifest.json    complete  534/225/392/345  22 tests  openItems:0
docs/brain/PTT-COPIER-B6/06-deferred-backlog.md  B1->B6 full ledger — ALL ITEMS CLOSED
docs/brain/PTT-COPIER-B7/00-continue-prompt.md   THIS FILE
docs/standards/jane-street/RULES_CATALOG.md       reference
docs/protocol/PTT_WORKSPACE_PROTOCOL.md           reference
specs/assets/competitor-reference/ai-enhanced-chart-trader-full-panel.md   full panel map
specs/assets/competitor-reference/ai-enhanced-full-feature-inventory.md    24-feature inventory
specs/assets/roadmap/B7-B9-feature-roadmap.md     ATR sizing, click trader, two modes, ATR box
specs/assets/zoom-reference/scotty-zoom-session-notes.md  screen layout corrected
specs/assets/screenshots/bob-debug-console.jpg           saved (unrelated to trade copier)
specs/assets/screenshots/docker-desktop-containers.jpg   saved (unrelated to trade copier)
specs/assets/screenshots/greptile-settings-1.jpg         saved (unrelated to trade copier)
specs/assets/screenshots/greptile-settings-2.jpg         saved (unrelated to trade copier)
NOTE: No trade copier screenshots exist as image files. The Scotty Zoom competitor analysis
      is fully encoded in the markdown reference files above (text-based — no image file needed).
.bob/commands/nt-builder.md                       Tier 1 command (Check 6 auto-discovers backlog)
.bob/custom_modes.yaml                            ptt-orchestrator/ptt-architect/ptt-plan-reviewer/
                                                  ptt-engineer/ptt-verifier all configured

--- DEFERRED BACKLOG PROTOCOL (mandatory) ---
06-deferred-backlog.md is the single source of truth for all deferred work.
Written by ptt-plan-reviewer in Phase 5 of EVERY block (FINAL_PASS blocked if missing).
Read by ptt-architect in Phase 1 of the NEXT block.
nt-builder Check 6 auto-discovers the prior block's backlog and passes it to the prompt.
PIPELINE_COMPLETE is blocked if 06-deferred-backlog.md is absent after FINAL_PASS.

--- OPEN B7 BACKLOG (from docs/brain/PTT-COPIER-B6/06-deferred-backlog.md) ---
NO deferred items from B6. Backlog is EMPTY.
B7 features come from the roadmap (specs/assets/roadmap/B7-B9-feature-roadmap.md):

  PRIORITY 1 — F5 GATE (blocking before any B7 code):
    Verify ChartTrader panel injection compiles and appears in a live chart.
    Known risk: chartTrader.Rows property name may be "RowsPanel" in some NT8 builds.
    Known risk: AccountComboBoxStyle SetResourceReference in TradeCopierPanel.cs line 64
                may throw in UserControl context (was safe in NTWindow context).
    If "PTT ChartTrader inject error:" MessageBox appears — fix before B7.

  PRIORITY 2 — B7 FEATURES (after F5 gate is green):
    B7-F1  Button color coding                P2  Low complexity
           [Copy ON] = green Background, [Copy OFF] = dark grey
           [Flatten] = red, [Cancel] = red, [Trim] = amber, [BE] = green
           WPF SolidColorBrush on Button.Background — NO NTBrushes refs
           Apply on both TradeCopierPanel and TradeCopierWindow rule rows

    B7-F2  Position + P&L status strip        P2  Low complexity
           Single TextBlock per rule row: "MES: 4 long | +$120"
           Reads from account.GetAccountValue(AccountItem.RealizedPnL)
           and account position data already tracked in CopyEngine
           Updates via existing StatusUpdate event + Dispatcher.InvokeAsync

    B7-F3  Per-account qty multiplier         P2  Medium complexity
           In each follower row: TextBox "1.0x" beside account name
           CopyEngine.CopyRule gets a Dictionary<string,double> multipliers
           SendCopy multiplies signal.Quantity by multiplier before CreateOrder
           Default multiplier = 1.0 (no change to existing behavior)

    B7-F4  ATR dynamic sizing engine          P1  Medium complexity
           New class: AtrSizingEngine (separate file, CYC<=8)
           Methods: UpdateTrueRange(), CalcAtr(), CalcContracts(), ApplyResize()
           Subscribes to MarketData for rolling N-bar true range (N=14 default)
           Watches pending orders via Account.OrderUpdate
           Calls order.Change() when calculated contracts != current order qty
           Safety: ATR=0 guard (use max_contracts), min=1, max=operator cap
           Only adjusts PENDING (Submitted state) orders — never filled
           Configurable: max_risk_dollars, atr_period, max_contracts, min_contracts

--- COMPETITOR INTELLIGENCE (confirmed from Scotty Zoom session) ---
Competitor: affordableindicators.com / aiEnhancedChartTrader
Two surfaces: (A) ChartTrader injection panel + (B) separate spreadsheet-style copier window
Their BE button: [BE +1] hardcoded — PTT advantage: our TextBox is live-editable
Their Copy toggle: full-width, dark when OFF, bright when ON — PTT matches
Two copy modes: unconfirmed — research at affordableindicators.com before B8
AI Assistant feature: floating chat window for contextual help — B8+ candidate
Attach Orders: snap order to indicator price — B8+ candidate
ATR sizing display in chart header — B8 candidate

--- NEXT ACTIONS (in priority order) ---
1. F5 in NT8 — compile and test ChartTrader panel injection
   If error: paste "PTT ChartTrader inject error: ..." message
   Fix 1: try "RowsPanel" if "Rows" doesn't compile
   Fix 2: remove SetResourceReference(AccountComboBoxStyle) from TradeCopierPanel.cs line 64
            replace with: _leaderCombo = new ComboBox { ItemsSource = Account.All, ... }
2. Once F5 green: run /nt-builder 002-trade-copier-spec.html PTT-COPIER-B7
   This outputs the ptt-orchestrator prompt. Paste into new ptt-orchestrator session.
3. B7 features above (F1 color coding first — lowest complexity, highest visual impact)

--- LIVE SPEC ---
file:///C:/WSGTA/universal-or-strategy-director/specs/002-trade-copier-spec.html
