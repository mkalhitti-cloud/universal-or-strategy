# BWAVE-CYC Lane C -- Mission Brief

**Produced by**: ptt-orchestrator (Stage 1)
**Date**: 2025-01-30
**Wave**: BWAVE-CYC -- Complexity Reduction
**Lane**: C -- Panel / Window / AddOn

---

## 1. Independence Confirmation

Lane C is **fully independent of Lanes A and B**.

| Lane | Files touched |
|------|--------------|
| A | CopyEngine.cs only |
| B | CopyEngine.cs only |
| **C** | **TradeCopierPanel.cs, TradeCopierWindow.cs, TradeCopierAddOn.cs** |

Zero file overlap. Lane C proceeds from t=0 with no coordination needed.
No blocking dependency on Lane A or Lane B status.

---

## 2. Baseline Code Health (CodeScene)

| File | Baseline Score | Target After Lane C |
|------|---------------|---------------------|
| TradeCopierPanel.cs | **3.45 / 10** | >= 7.0 |
| TradeCopierWindow.cs | **5.81 / 10** | >= 8.0 |
| TradeCopierAddOn.cs | **7.91 / 10** | >= 9.0 |

Panel is the highest-risk file -- lowest health score and most tickets (T1-T4).
Window has 4 tickets (T5-T7) focused on AccountDisplayConverter callbacks.
AddOn has 1 ticket (T8) targeting the NT8 visual tree injection methods.

---

## 3. Ticket Summary -- 8 Tickets, 25 Methods

| Ticket | File | Methods | CCN Before | Risk |
|--------|------|---------|-----------|------|
| T1 | Panel | FollowerItem::UpdateButtonColors + FollowerItem::OnLoaded | 18, 17 | WPF RoutedEvent handlers |
| T2 | Panel | OnApplyRule + FollowerItem::GetLeaderAtmTemplateName | 15, 12 | WPF event handler + CopyEngine call |
| T3 | Panel | TradeCopierPanel::ApplyFeatureFlags + ApplyFeatureFlagTooltips | 10, 11 | FeatureFlags enum switches |
| T4 | Panel | IsPriceAlreadyAtBe + RefreshQuickDisplay + OnLeaderPositionUpdate + OnChartMouseDown | 10, 10, 10, 9 | Position/price callbacks |
| T5 | Window | AccountDisplayConverter::OnRowApply | 18 | NT8 dispatcher callback (outer switch) |
| T6 | Window | AccountDisplayConverter::OnRuleBreakEven + OnRuleArmBe + OnRuleTightenStop | 11, 10, 10 | NT8 dispatcher callbacks |
| T7 | Window | TradeCopierWindow::ApplyFeatureFlags | 9 | Feature flag switches |
| T8 | AddOn | TradeCopierAddOn::DoInject + WireControlCenterMenu | 15, 9 | NT8 visual tree walk |

**Total methods**: 25
**Total target**: All 25 at CCN <= 8 after extraction.

---

## 4. NT8 UI Thread Contract (Critical Safety Gate)

These rules govern every extraction in this lane. Architect MUST apply them ticket by ticket.

### SAFE to extract:
- Pure decision logic (if/else trees, guard clauses, value computation)
- Named predicate helpers (bool methods answering a single question)
- Value-building helpers that return a computed value
- Per-flag visibility blocks (enum switch branches)
- Visual tree "finder" helpers that return a found element

### FORBIDDEN to extract:
- Code that calls `Dispatcher.InvokeAsync` / `Dispatcher.Invoke` -- keep in original method
- Code that accesses NT8 `Account`, `Order`, or `Position` objects into a helper that could be called off UI thread
- NT8 lifecycle callback signatures (`OnStateChange`, `OnWindowCreated`, `OnWindowDestroyed`) -- entry points must stay intact
- `AccountDisplayConverter` callbacks (`OnRowApply`, `OnRuleBreakEven`, etc.) -- outer callback signature stays, only inner decision logic extracted
- `VisualTreeHelper.GetChild` / `DependencyProperty` calls should NOT be moved into helpers that break WPF binding expectations

### Per-ticket special notes:
- **T1**: WPF RoutedEvent handlers -- extract decision logic only, never VisualTreeHelper or DependencyProperty
- **T2**: `OnApplyRule` -- extract validation/guard logic only; `CopyEngine.Instance` call MUST remain in `OnApplyRule`
- **T5/T6**: `AccountDisplayConverter` callbacks -- outer switch/dispatch stays; only per-rule-type decision blocks extracted
- **T8**: `DoInject` -- extract "find specific control" blocks into named private helpers; main injection sequence stays in `DoInject`

---

## 5. CYC Extraction Rules (Non-Negotiable)

1. **Private helpers only** -- no new public or internal surface
2. **Semantic helper names** -- describe the decision being made (e.g. `IsBreakEvenAlreadySet`, `ApplyDragFeatureFlags`)
3. **Each helper: CCN <= 4**
4. **Parent method after extraction: CCN <= 8**
5. **Identical behaviour** -- no logic changes, no reordering, no early returns added
6. **One `[Fact]` test per extracted helper minimum**

---

## 6. Mandatory Scans (ptt-verifier runs all 7 before VERIFY_PASS)

| Scan | Command | Required Result |
|------|---------|----------------|
| SCAN-01 | `Select-String "lock(" src/PropTraderTools -Recurse -Include *.cs` | 0 results |
| SCAN-02 | `Select-String "async void " src/PropTraderTools -Recurse -Include *.cs` | 0 results |
| SCAN-03 | `Select-String "return null" src/PropTraderTools -Recurse -Include *.cs` | 0 new instances |
| SCAN-04 | `Select-String "throw new " src/PropTraderTools -Recurse -Include *.cs` | 0 new instances |
| SCAN-05a | `lizard src/PropTraderTools/TradeCopier{Panel,Window,AddOn}.cs --CCN 8` | 0 warnings for all 25 modified methods |
| SCAN-05b | `$env:CS_ACCESS_TOKEN=...; cs delta` | Code Health does NOT decrease for any modified file |
| SCAN-06 | `dotnet build` | 0 errors, 0 warnings |
| SCAN-07 | `dotnet test` | 370 pass, 22 pre-existing IL-reflection (ACCEPT), 0 new failures |

---

## 7. Known Baseline Failures (Not Regressions)

22 IL-reflection test failures in `archive/v12-reference` linting DLL -- pre-existing since B87.
ptt-verifier MUST state: "22 pre-existing IL-reflection failures -- accepted, baseline confirmed."

---

## 8. Execution Sequence

```
Stage 2: ptt-architect reads all 25 method bodies, designs 8 extraction tickets
         Writes: LaneC-02-architect-plan.md
         STOP -- hand off to Stage 3

Stage 3: ptt-engineer executes T1 -> T2 -> T3 -> T4 -> T5 -> T6 -> T7 -> T8 (sequential)
         Each ticket: edit .cs, dotnet build PASS, cs delta PASS, log result
         Writes: LaneC-03-engineer-report.md
         STOP -- hand off to Stage 4

Stage 4: ptt-verifier runs all 7 scans independently
         Reads all modified methods, runs lizard directly
         Writes: LaneC-04-verify-report.md
         Output: VERIFY_PASS or VERIFY_FAIL with blocker details
```

---

## 9. FINAL_PASS Criteria

- [ ] ptt-verifier VERIFY_PASS on all 7 scans
- [ ] All 25 Lane-C target methods: CCN <= 8 confirmed by lizard
- [ ] CodeScene scores: Panel >= 7.0, Window >= 8.0, AddOn >= 9.0
- [ ] NT8 UI THREAD CONTRACT not violated (ptt-verifier explicitly confirms)
- [ ] New [Fact] tests: minimum 1 per extracted helper, all passing
- [ ] No new `lock()`, no new `async void`, no new `return null`
- [ ] `verify_links.ps1 -Fix` run after all 7 scans pass
- [ ] `docs/brain/BWAVE-CYC/LaneC-04-verify-report.md` written

---

**Build Tag**: PTT-COPIER BWAVE-CYC Lane-C | 2025-01-30

STAGE 1 COMPLETE -- handing off to ptt-architect.
