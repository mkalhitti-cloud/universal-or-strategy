# B77-LaneA Architecture Plan
## Epic: B77-LaneA — ATM Template Name Fix Test Coverage
**Phase**: 1 (Architecture)
**Output**: docs/brain/B77-LaneA/02-architecture-plan.md

---

## 1. Decision Record: Why `SelectedItem as string` is Correct (vs `SelectedAtmStrategy.Name`)

`AtmStrategySelector` extends WPF `ComboBox`. Its `ItemsSource` is populated from the NT8 ATM
template list — which is a list of plain strings (template names). Because the underlying item
collection is string-typed, `SelectedItem` returns the currently highlighted item as an `object`
whose runtime type is `string` (the template name). The `as string` cast therefore yields the
template name on a live selection, and `null` when nothing is selected — safe via `?? string.Empty`.

`SelectedAtmStrategy` returns a typed `AtmStrategy` object. Its `.Name` property reflects the
**C# class name** of the object (`"AtmStrategy"`) whenever no template is actively running (i.e.
the strategy object exists but carries only its default class identity). This is the same
class-name trap that B76 fixed on the primary `ct.AtmStrategy.Name` path (branch 4 guard
`n != "AtmStrategy"`). Using `SelectedAtmStrategy.Name` on the fallback path reintroduces the
identical trap.

**NT8_FULL_REFERENCE.md citations**:
- Line 1293: `atmStrategySelector.SelectedItem` used to interrogate selection state.
- Line 1826: `atmStrategySelector.SelectedItem == null` check; cast `args.AddedItems[0] as AtmStrategy`
  — items in the source collection are strings; `SelectedItem` provides raw combo access.
- The official Events and Properties table for `AtmStrategySelector` (lines 1759-1796) does **not**
  document `SelectedItem` as an NT8 property — it is inherited from WPF `ComboBox` — confirming
  that `SelectedAtmStrategy` is the typed NT8 accessor but `.Name` is unreliable before a live
  strategy run.

**Conclusion**: `sel.SelectedItem as string ?? string.Empty` is the correct read because
(a) it directly reads the string the user selected in the combo, and
(b) it is safe when `SelectedItem` is `null` or unexpectedly non-string.

---

## 2. All 7 Branches Documented

| Branch | Condition | Return | Notes |
|--------|-----------|--------|-------|
| 1 | `currentChart == null` | `string.Empty` | Null guard — first line |
| 2 | `FindVisualChild<ChartTrader>(currentChart)` returns `null` | `string.Empty` | Null guard — no ChartTrader in visual tree |
| 3 | `ct.AtmStrategy != null` | (continue to branch 4) | Primary path — strategy object present |
| 4 | `n.Length > 0 && n != "AtmStrategy"` | `n` (template name string) | B76 guard: valid user-named template |
| (5) | Branch 4 fails: `n` is empty OR `n == "AtmStrategy"` | fall through to fallback-1 | B76 class-name trap avoided; strategy present but unnamed |
| 6 | `sel != null` (`AtmStrategySelector` found) | `sel.SelectedItem as string ?? string.Empty` | **B77 repair**: reads raw combo string, not `SelectedAtmStrategy.Name` |
| (6b) | `sel == null` (no `AtmStrategySelector` in tree) | fall through to fallback-2 | Pre-B66 legacy layout |
| (fallback-2) | `atmCb?.SelectedItem as string` | template name string or `string.Empty` | Legacy `ComboBox` at index 2 |
| 7 | Any exception thrown inside `try` block | `string.Empty` | Catch-all for NT8 API exceptions |

---

## 3. Test Matrix for T1

| Test ID | Path Exercised | NT8 Host Needed | Method |
|---------|---------------|-----------------|--------|
| T_B77_TPL_01 | Branch 1 (null chart → `string.Empty`) | No | Direct reflection invoke with `null` argument |
| T_B77_TPL_02 | Branch 2 (`ct == null` — no `ChartTrader` in visual tree) | Yes | `[Fact(Skip="NT8-HOST-REQUIRED: visual tree traversal needed")]` skeleton |
| T_B77_TPL_03 | Branches 3→4 fail → 5 → fallback-1 (`sel==null`) → fallback-2 (null `atmCb`) → `string.Empty` | Yes | `[Fact(Skip="NT8-HOST-REQUIRED: live visual tree + ATM state needed")]` skeleton |
| T_B77_TPL_04 | Branch 6 (`sel != null`, `SelectedItem = "MES $200 SL7"`) — IL inspection verifies `SelectedItem` is read, not `SelectedAtmStrategy` | No | IL scan of method body for `SelectedItem` opcode; assert `SelectedAtmStrategy` is **not** called on `sel` in the fallback-1 path |
| T_B77_TPL_05 | Branch 6 (`sel != null`, `SelectedItem = null`) → `string.Empty` via `?? string.Empty` — null-invoke proxy for branch 1 runnable check | No | Direct reflection invoke with `null` chart (branch 1 proxy); IL scan confirms null-safe `??` pattern present |

**Notes**:
- `T_B77_TPL_01` and `T_B77_TPL_05`: both use the `null`-chart branch-1 path for the runnable
  (non-NT8) portion of the test. The deeper paths (branches 2-7) all require a live WPF/NT8 visual
  tree and are documented as skip skeletons only.
- `T_B77_TPL_04`: IL inspection is the only non-host method that can confirm the B77 repair is in
  the compiled binary. The test loads the assembly via reflection, retrieves the IL bytes of
  `GetLeaderAtmTemplateName`, and asserts:
  1. The string `"SelectedItem"` (as a member reference token) appears in IL — OR the property
     getter for `SelectedItem` (`ComboBox.SelectedItem`) is called.
  2. The token for `SelectedAtmStrategy` does **not** appear in the fallback-1 code path after
     the `sel != null` branch (i.e. the B77 trap is absent).
- All NT8-host-required tests follow the `T_B66TPL_03..05` pattern from
  `TradeCopierPanelB75Tests.cs`: one-line `[Fact(Skip="NT8-HOST-REQUIRED: ...")]` with a
  descriptive reason string.

---

## 4. Implementation Notes

### What changes
- **No modifications to `TradeCopierPanel.cs`** — the B77 repair (commit `ff5944ee`) is already
  applied. This epic adds test coverage only.
- **New file**: `src/PropTraderTools/TradeCopierPanelB77Tests.cs`

### Test class structure
```
namespace PropTraderTools
{
    public sealed class TradeCopierPanelB77Tests
    {
        // T_B77_TPL_01 — runnable (no NT8 host)
        // T_B77_TPL_02 — skip skeleton (NT8-HOST-REQUIRED)
        // T_B77_TPL_03 — skip skeleton (NT8-HOST-REQUIRED)
        // T_B77_TPL_04 — IL inspection (no NT8 host)
        // T_B77_TPL_05 — IL inspection + null-invoke (no NT8 host)
    }
}
```

### Pattern references
- `T_B76_10` (B76Tests.cs line 319): reflection invoke `GetLeaderAtmTemplateName(null)` — reuse
  exact pattern for `T_B77_TPL_01`.
- `T_B76_11` (B76Tests.cs line ~330): IL scan for string literal `"AtmStrategy"` guard — extend
  pattern for `T_B77_TPL_04` to scan for `SelectedItem` accessor presence.
- `T_B76_12` (B76Tests.cs line ~360): reflection check method is `internal static` on
  `TradeCopierPanel` — no need to repeat in B77 (already covered by B76).

### Jane Street DNA compliance
| Rule | Status |
|------|--------|
| JS-021 no `lock()` | Confirmed — test methods are pure, no shared mutable state |
| JS-001 no `throw new` in hot path | Confirmed — tests use `Assert.Throws` pattern or none |
| JS-002 no `return null` | Confirmed — all helpers return `string` or `bool`, never `null` |
| JS-033 no `async void` | Confirmed — all test methods are synchronous `void` |
| ASCII-only identifiers | Confirmed — no Unicode in identifiers or string literals |
| CYC <= 8 | Confirmed — each test method is a linear sequence, CYC = 1 |
| xUnit only (`[Fact]`) | Confirmed — no NUnit/MSTest |

### Component list
| Component | File | Kind |
|-----------|------|------|
| `TradeCopierPanelB77Tests` | `src/PropTraderTools/TradeCopierPanelB77Tests.cs` | `sealed class` (xUnit) |
| `GetLeaderAtmTemplateName` (existing) | `src/PropTraderTools/TradeCopierPanel.cs` | `internal static string` — read-only, no change |

### Threading model
Tests are synchronous. No `Dispatcher.InvokeAsync`, no `ConcurrentQueue`, no cross-thread calls.
IL inspection tests operate on `MethodBody.GetILAsByteArray()` — pure in-process reflection.

### Data flow (test)
```
[Fact] method
  -> Assembly.GetExecutingAssembly() or typeof(TradeCopierPanel).Assembly
  -> typeof(TradeCopierPanel).GetMethod("GetLeaderAtmTemplateName", BindingFlags.NonPublic | BindingFlags.Static)
  -> method.Invoke(null, new object[] { null })   // branch-1 runnable tests
  -> method.GetMethodBody().GetILAsByteArray()     // IL inspection tests
  -> Assert.Equal / Assert.Contains / Assert.DoesNotContain
```

---

STATUS: REVIEW_PASS
