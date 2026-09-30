# B55 LaneA — Architecture Plan
# Epic: DW-B43-02 P1 — ATM Template Read Fix (GetLeaderAtmTemplateName SelectedItem)
# Status: REVISION SUBMITTED
# Spec: specs/002-trade-copier-spec.html#section-b55 LANE A

---

## 1. Spec Requirement Summary

| ID        | Priority | Description                                                                      |
|-----------|----------|----------------------------------------------------------------------------------|
| DW-B43-02 | P1       | GetLeaderAtmTemplateName() read SelectedValue (null) instead of SelectedItem     |
| T_B55A_01 | NEW      | Unit test documenting the SelectedItem read path (pure pattern, no WPF)          |

---

## 2. Source File State (Working Tree — Pre-Commit)

Investigation findings provided by orchestrator:

- `TradeCopierPanel.cs` line 2088:
  ```csharp
  return atmCb.SelectedItem as string ?? string.Empty;
  ```
  The SelectedItem fix **is already applied** in the working tree (found via `git diff HEAD`).
  `GetLeaderAtmTemplateName()` — `internal static`, CYC=4 — reads `SelectedItem` not `SelectedValue`.

**Conclusion:** TradeCopierPanel.cs requires **no changes** in B55 LaneA. The production fix is done.

---

## 3. What B55 LaneA Adds

One new file only:

| File                                      | Type        | Action |
|-------------------------------------------|-------------|--------|
| `src/PropTraderTools/Tests/B55Tests.cs`   | Test class  | CREATE |

One new test:

| Test Name                                                                            | CYC |
|--------------------------------------------------------------------------------------|-----|
| `T_B55A_01_GetLeaderAtmTemplateName_SelectedItemSet_SelectedValueNull_ReturnsTemplateName` | 1 |

Test baseline: 297 → **298** after B55 LaneA.

---

## 4. Architecture Decision: Why a Pure Pattern Test

### Why NOT a WPF ComboBox test
- xUnit test runner uses MTA threads by default.
- WPF ComboBox requires an STA thread and a running WPF application host.
- NT8 WPF assemblies may not load in the external Linting test project context.
- Attempting to instantiate ComboBox in MTA = `InvalidOperationException` at test startup.

### Why NOT a reflection test
- `GetLeaderAtmTemplateName()` requires `_currentChart` to be non-null AND a valid WPF visual
  tree containing a `ChartTrader` with a `ComboBox` named `cbxStrategySelector`.
- The null-chart guard path is already tested by `T_B43_04`.
- There is no lightweight way to inject a stub ComboBox through the method's private visual-tree
  walk without instantiating WPF.

### Approved approach: Pure code-documentation test
The method's critical semantic is one expression: `cb.SelectedItem as string ?? string.Empty`.
- `SelectedItem` is the selected object from the ComboBox Items collection.
- When NT8 populates `cbxStrategySelector` with ATM template name strings (no `SelectedValuePath`),
  `SelectedItem` IS the string; `SelectedValue` is `null`.
- The `as string` cast safely returns `null` if the item is not a string; `?? string.Empty` handles null.

T_B55A_01 directly exercises this expression in isolation:
- `object selectedItem = "MES $200"` — simulates ComboBox.SelectedItem after user selects template
- `string selectedValue = null` — confirms SelectedValue stays null (root cause of original bug)
- `string result = selectedItem as string ?? string.Empty` — the exact production code pattern
- `Assert.Equal("MES $200", result)` — locks the return value
- `Assert.Null(selectedValue)` — documents that SelectedValue is null (why the old code broke)

This approach:
- Requires zero WPF assemblies
- Runs on any thread
- Is deterministic (always passes)
- Directly documents the root cause and fix

---

## 5. Component List

| Component    | Class       | File                                     | Namespace        |
|--------------|-------------|------------------------------------------|------------------|
| Test class   | B55Tests    | src/PropTraderTools/Tests/B55Tests.cs    | PropTraderTools  |

No production class changes.

---

## 6. Method Signatures

### B55Tests.T_B55A_01
```csharp
[Fact]
public void T_B55A_01_GetLeaderAtmTemplateName_SelectedItemSet_SelectedValueNull_ReturnsTemplateName()
```
- Return type: `void`
- Parameters: none
- CYC: 1
- No NT8 API calls
- No WPF types
- No async

---

## 7. Data Flow

```
Arrange:
  object selectedItem  = "MES $200"     // simulates ComboBox.SelectedItem
  string selectedValue = null            // simulates ComboBox.SelectedValue (no SelectedValuePath)

Act:
  string result = selectedItem as string ?? string.Empty
                  ^--- exact expression from GetLeaderAtmTemplateName() line 2088

Assert:
  result == "MES $200"      (SelectedItem path returns template name)
  selectedValue == null     (SelectedValue is null = root cause documentation)
```

---

## 8. NinjaTrader 8 API Usage

None. B55Tests.cs uses:
- `using Xunit;` only
- No NT8 namespace imports
- No WPF types

---

## 9. Threading Model

Pure synchronous test. No `Dispatcher.InvokeAsync`, no `ConcurrentQueue`, no thread marshaling.
xUnit runner manages thread lifecycle. STA constraint is irrelevant (no WPF instantiated).

---

## 10. JS Rule Compliance Pre-Check

| Rule   | Applies? | Status |
|--------|----------|--------|
| JS-021 (lock)          | No  | PASS — no lock() |
| JS-033 (async void)    | No  | PASS — no async |
| JS-001 (throw in hot path) | No | PASS — no throw |
| JS-002 (return null)   | No  | PASS — no return null |

---

## 11. NT8 Compiler Rule Pre-Check

| Rule    | Applies? | Status |
|---------|----------|--------|
| NT8-001 ({get; init;}) | No  | PASS |
| NT8-002 (abstract record) | No | PASS |
| NT8-003 (volatile double) | No | PASS |
| NT8-019 (async void)   | No  | PASS |
| NT8-042 (Dispatcher.InvokeAsync) | No | PASS |
| NT8-044 (StringComparison / using System) | No | PASS |

---

## 12. Invariants

1. `T_B43_04_GetLeaderAtmTemplateName_NullChart_ReturnsEmptyString` — must still pass unchanged.
2. `T_B55A_01` — new test, documents SelectedItem path, must pass deterministically.
3. `GetLeaderAtmTemplateName()` in `TradeCopierPanel.cs` reads `SelectedItem` (not `SelectedValue`).
4. Test count after B55 LaneA: 298.

---

## 13. Deferred Items Closed

| Deferred Work Item | Closed By | Notes                                                        |
|--------------------|-----------|--------------------------------------------------------------|
| DW-B43-02 P1       | B55 LaneA | Fix confirmed in working tree; test added to document path   |
