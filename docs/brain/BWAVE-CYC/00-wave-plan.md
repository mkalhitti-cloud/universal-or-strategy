# BWAVE-CYC — Complexity Reduction Wave
## Jane Street CYC <= 8 on all src/PropTraderTools/*.cs methods

**Wave started**: pre-B144
**Baseline commit**: 596ebf41
**Baseline lizard**: 95 warnings total (59 in production .cs files, 36 in test files)
**Goal**: 0 warnings in production .cs files (test files excluded from gate)
**Test baseline**: 370 pass / 22 pre-existing IL-reflection failures (known) / 15 skips

---

## Parallelism Model

Lane A and Lane B BOTH touch CopyEngine.cs -- they are SEQUENTIAL (A completes, then B starts).
Lane C touches only Panel/Window/AddOn -- it runs IN PARALLEL with Lane A and Lane B.

```
Timeline:
  t=0   Lane A starts (CopyEngine lines ~875-1100, ~2279-3793, ~4303-5520)
  t=0   Lane C starts (TradeCopierPanel + TradeCopierWindow + TradeCopierAddOn)

  t=A_done  Lane B starts (CopyEngine lines ~1316-2199 -- no line overlap with Lane A)
            Lane C may still be running (OK -- different files)

  t=all_done  Final wave PR (single PR, all .cs changes)
```

---

## Scope

| File | Warnings | Lane |
|------|----------|------|
| CopyEngine.cs (lines ~875-5520) | 42 | A + B sequential |
| TradeCopierPanel.cs | 10 | C |
| TradeCopierWindow.cs | 5 | C |
| TradeCopierAddOn.cs | 2 | C |
| Total | **59** | |

---

## Lane A — CopyEngine: BE/ATM/bracket cluster (lines 875-1100, 2279-5520)

7 tickets, sequential within lane. All pure C# logic, no NT8 UI thread contracts.

| Ticket | Methods | CCN before |
|--------|---------|-----------|
| A-T1 | OnPendingBeAccountUpdate@5480 + ArmPendingBe@5308 | 32, 27 |
| A-T2 | SnapshotBeTargets@4938 + MoveStopToBreakEven@4993 | 24, 18 |
| A-T3 | ResubmitOneCollateralLeg@2701 | 25 |
| A-T4 | TryCleanupReArmedAtmBracket@3727 + ReplaceFollowerCopyOnAtmCancel@3548 | 23, 18 |
| A-T5 | SyncAtmFollowerTarget@2869 + SyncFollowerBracket@2279 | 21, 20 -- DW-B143-POSSTATE-CYC8 P0 |
| A-T6 | FlattenOneAccount@4303 + TryReplacePttBeBrackets@3644 + CountLeaderTargets@4904 | 19, 14, 13 |
| A-T7 | ResubmitTargetAfterCascade@2588 + HandleEntryChange@3366 + TryFirePositionState@3451 | 13, 13, 13 -- includes DW-B143-POSSTATE-CYC8 P0 |
| A-T8 | CancelQxBrackets@875 + CancelQxBrackets@955 + CancelAllAccountOrders@1013 + BuildQxSnapshot@916 | 14, 16, 12, 11 |

---

## Lane B — CopyEngine: dispatch/entry cluster (lines 1316-2199)

Starts AFTER Lane A FINAL_PASS. No line overlap with Lane A methods.

| Ticket | Methods | CCN before |
|--------|---------|-----------|
| B-T1 | OnOrderUpdate@1316 | 23 -- DW-B143-POSSTATE-CYC8 P0 |
| B-T2 | DispatchCopy@2082 | 13 |
| B-T3 | TryFireFollowerBeRetry@1483 + TryEvictFollowerBeSlot@1542 | 15, 13 |
| B-T4 | TryHandleEntryDrag@1886 + IsExitSignalName@2008 + SyncAtmFollowerBracket@2395 | 11, 10, 11 |
| B-T5 | DtoToRule@5609 | 11 |

---

## Lane C — Panel + Window + AddOn (manual PTT pipeline, NT8 UI tier)

Runs in parallel with Lane A from t=0. Tickets are independent (different files).

| Ticket | Method | File | CCN | Risk |
|--------|--------|------|-----|------|
| C-T1 | FollowerItem::UpdateButtonColors + OnLoaded | Panel | 18, 17 | WPF bindings |
| C-T2 | OnApplyRule + GetLeaderAtmTemplateName | Panel | 15, 12 | WPF event handler |
| C-T3 | ApplyFeatureFlags + ApplyFeatureFlagTooltips | Panel | 10, 11 | Feature flag switches |
| C-T4 | IsPriceAlreadyAtBe + RefreshQuickDisplay + OnLeaderPositionUpdate + OnChartMouseDown | Panel | 10,10,10,9 | Position update callbacks |
| C-T5 | AccountDisplayConverter::OnRowApply | Window | 18 | NT8 dispatcher callback |
| C-T6 | AccountDisplayConverter::OnRuleBreakEven + OnRuleArmBe + OnRuleTightenStop | Window | 11,10,10 | NT8 dispatcher callbacks |
| C-T7 | TradeCopierWindow::ApplyFeatureFlags | Window | 9 | Feature flag switches |
| C-T8 | TradeCopierAddOn::DoInject + WireControlCenterMenu | AddOn | 15, 9 | NT8 visual tree walk |

---

## CYC Extraction Rules (applies to all lanes)

1. Extract to PRIVATE helper methods only. No new public/internal surface.
2. Helper names describe the semantic slice they perform (not "helper1").
3. Each extracted helper must have CYC <= 4 (leave headroom for future growth).
4. The parent method after extraction must be CYC <= 8.
5. Behaviour must be IDENTICAL. No logic changes. No early returns. No reordering.
6. Every extracted helper gets one [Fact] test minimum (or added to existing test class).

---

## Mandatory scans per ticket (ptt-verifier runs all 7)

SCAN-01: Select-String "lock("        src/PropTraderTools -Recurse -Include *.cs  -> 0 results
SCAN-02: Select-String "async void "  src/PropTraderTools -Recurse -Include *.cs  -> 0 results
SCAN-03: Select-String "return null"  src/PropTraderTools -Recurse -Include *.cs  -> 0 new
SCAN-04: Select-String "throw new "   src/PropTraderTools -Recurse -Include *.cs  -> 0 new
SCAN-05: lizard src/PropTraderTools/{File}.cs --CCN 8                             -> 0 warnings for modified methods
SCAN-06: dotnet build                                                              -> 0 errors 0 warnings
SCAN-07: dotnet test                                                               -> 370 pass, 22 pre-existing IL-reflection (accept), 0 new failures

## KNOWN BASELINE FAILURES (not regressions)
22 IL-reflection test failures in archive linting DLL -- pre-existing since B87
ptt-verifier MUST note: "22 pre-existing IL-reflection failures -- accepted, not new"

## Wave PR
Single PR after all lanes FINAL_PASS.
Branch: feature/bwave-cyc
Title: "feat(ptt): BWAVE-CYC -- all 59 methods reduced to CYC <= 8, Jane Street standard"
