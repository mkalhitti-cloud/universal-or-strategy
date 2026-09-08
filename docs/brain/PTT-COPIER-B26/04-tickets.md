# PTT-COPIER-B26 Tickets

**Epic**: PTT-COPIER-B26  
**Phase**: 3 — Ticket Generation  
**Source plan**: `docs/brain/PTT-COPIER-B26/02-architecture-plan.md` (REVIEW_PASS, Cycle 1)  
**Author**: ptt-architect  
**Date**: 2026-07-07  
**Ticket count**: 1 (SINGLE-PIPELINE)

---

## T1 — Fix `_beBufferBox` NullRef in `DispatchShortcut`

### Spec Requirement IDs

- **DW-PR120-GREPTILE-B** — confirmed NullReferenceException on Ctrl+Shift+B: `_beBufferBox` field at
  `TradeCopierPanel.cs:202` is declared but never assigned; `DispatchShortcut` reads it at line 3065.

### File Path

```
src/PropTraderTools/TradeCopierPanel.cs
```

Wave workspace: `C:\WSGTA\universal-or-strategy`

### Method Signatures Affected

| Method | Return | Visibility | Change |
|--------|--------|------------|--------|
| `DispatchShortcut(Key key)` | `void` | `private` | Key.B case body replaced; signature unchanged |

No new methods. No overloads. `BreakEven(Account account, Instrument instrument, int bufferTicks)`
in `CopyEngine` is called as before — signature established in B24, not changed in B26.

### Exact Changes — 3 Surgical Edits

#### Edit 1 — Delete dead field (line 202)

**File**: `src/PropTraderTools/TradeCopierPanel.cs`

```
OLD (line 202):
        private TextBox _beBufferBox;

NEW:
(line deleted entirely — remove the line, do not replace with blank)
```

Rationale: `_beBufferBox` is never assigned anywhere in the file. After Edit 2 removes its only
usage at line 3065, this declaration is dead code. Removing it eliminates the risk of any future
caller accidentally reading a null reference.

#### Edit 2 — Replace Key.B case body (lines 3063–3067)

**File**: `src/PropTraderTools/TradeCopierPanel.cs`

```
OLD (lines 3063–3067):
            case Key.B:
                int buf = 2;
                int.TryParse(_beBufferBox.Text, out buf);
                _engine.BreakEven(_leaderAccount, _instrument, buf);
                break;

NEW (lines 3063–3065):
            case Key.B:
                _engine.BreakEven(_leaderAccount, _instrument, _beBuffer);
                break;
```

Changes made:
- DELETE `int buf = 2;` — scaffolding for the old TextBox parse; no longer needed
- DELETE `int.TryParse(_beBufferBox.Text, out buf);` — crash site; `_beBufferBox` is null at runtime
- CHANGE third argument of `BreakEven` call: `buf` → `_beBuffer`

`_beBuffer` is `private int _beBuffer = 1;` at line 245 (existing field). It is written exclusively
by `OnBeUp()` and `OnBeDown()` (UI-thread button click handlers). Reading it in `DispatchShortcut`
is safe: `DispatchShortcut` runs on the WPF UI thread (invoked from `OnChartKeyDown`, a
`chart.PreviewKeyDown` handler). No Dispatcher.InvokeAsync needed. No lock needed.

Pattern conformance: Key.T reads `_trimBuffer` (int, line 243) directly. Key.F reads
`_flattenBuffer` (int, line 244) directly. Key.B now reads `_beBuffer` (int, line 245) directly.
All three shortcut cases now follow the same plain-int field pattern.

#### Edit 3 — Update stale comment (line 3041)

**File**: `src/PropTraderTools/TradeCopierPanel.cs`

```
OLD (line 3041):
        // BE path reads _beBufferBox.Text for buffer ticks (UI-thread-safe; PreviewKeyDown is on UI thread).

NEW:
        // BE path uses _beBuffer (int field, maintained by OnBeUp/OnBeDown) for break-even tick count.
```

Rationale: The comment referenced the now-removed `_beBufferBox` TextBox. After Edit 2, the
comment is factually wrong and would mislead future readers. The updated text correctly names
`_beBuffer` and its maintenance pattern.

### Test Requirement

**File**: `tests/PropTraderTools.Tests/CopyEngineTests.cs` (or appropriate panel test file —
use the file that already holds panel-level xUnit tests; do NOT create a new test file)

**Framework**: xUnit ([Fact] only — NOT NUnit, NOT MSTest)

**New test count**: 128 → 129

#### [Fact] `DispatchShortcut_KeyB_CallsBreakEvenWithBeBuffer`

**Asserts**:
1. When `DispatchShortcut` is invoked with `Key.B` on a panel instance where `_beBuffer` has been
   set to a known value (e.g. `3`), `CopyEngine.BreakEven` is called with that exact int value.
2. `_beBufferBox` field no longer exists on `TradeCopierPanel` — confirmed by the build succeeding
   with zero `CS0103` errors.
3. The call does NOT use a hardcoded fallback of `2`.
4. No exception is thrown.

**Implementation options** (engineer selects the most feasible):

Option A (preferred — mock ICopyEngine):
```csharp
[Fact]
public void DispatchShortcut_KeyB_CallsBreakEvenWithBeBuffer()
{
    // Arrange
    var mockEngine = new Mock<ICopyEngine>();
    var panel = CreatePanelUnderTest(mockEngine.Object);
    // Set _beBuffer = 3 via reflection
    typeof(TradeCopierPanel)
        .GetField("_beBuffer", BindingFlags.NonPublic | BindingFlags.Instance)
        .SetValue(panel, 3);

    // Act — invoke DispatchShortcut(Key.B) via reflection
    typeof(TradeCopierPanel)
        .GetMethod("DispatchShortcut", BindingFlags.NonPublic | BindingFlags.Instance)
        .Invoke(panel, new object[] { Key.B });

    // Assert
    mockEngine.Verify(e => e.BreakEven(It.IsAny<Account>(), It.IsAny<Instrument>(), 3), Times.Once);
}
```

Option B (fallback — compile-time assertion if WPF panel is not instantiable in xUnit):
```csharp
[Fact]
public void TradeCopierPanel_BeBufferBox_FieldDoesNotExist()
{
    // Asserts that _beBufferBox has been removed from TradeCopierPanel
    // (if this field existed, the type system would allow the null crash to recur)
    var field = typeof(TradeCopierPanel)
        .GetField("_beBufferBox", BindingFlags.NonPublic | BindingFlags.Instance);
    Assert.Null(field); // field must not exist after B26
}
```

Minimum acceptance bar: the [Fact] passes without exception and confirms that `_beBufferBox` is
absent OR that `BreakEven` receives the `_beBuffer` value (not a hardcoded 2).

### JS Rule Constraints

All P0 rules verified against plan §10 (REVIEW_PASS confirmed by plan-reviewer §3):

| Rule ID | Severity | Description | Status | Grep Pattern |
|---------|----------|-------------|--------|--------------|
| JS-021 | P0 | No `lock()` | **PASS** | `lock\s*\(` — zero results in change set |
| JS-001 | P0 | No `throw new XxxException` in hot paths | **PASS** | `throw\s+new\s+\w+Exception\(` — fix removes crash, introduces no throws |
| JS-002 | P0 | No `return null` | **PASS** | `return\s+null\s*;` — no return statements in change set |
| JS-033 | P0 | No `async void` (non-handler) | **PASS** | `async\s+void\s+\w+\(` — `DispatchShortcut` is `private void`, not async |
| JS-036 | P1 | No `new byte[]` heap allocation in hot path | **PASS** | Not applicable — no allocations in change set |
| JS-037 | P1 | No `new T[]` without ArrayPool in hot path | **PASS** | Not applicable — no array allocations in change set |
| CYC <= 8 | Arch | Cyclomatic complexity per method | **PASS** | `DispatchShortcut` CYC = 5 before and after (4 switch cases + base 1) |

**ASCII compliance**: `_beBuffer`, `BreakEven`, `DispatchShortcut`, `OnBeUp`, `OnBeDown` — all ASCII identifiers.  
**No `DateTime.Now`**: Not applicable to this change set.  
**No hex color literals**: Not applicable.  
**No FontFamily references**: Not applicable.

### 7-Scan Checklist (Engineer Contract)

All scans MUST reach zero before BUILD_PASS is reported. Run from `C:\WSGTA\universal-or-strategy`.

- [ ] **SCAN-01** `grep -r "lock(" src/ --include="*.cs"` → **0 results**  
      Verify no lock() introduced in DispatchShortcut or anywhere in the change set.

- [ ] **SCAN-02** `grep -rn "async void " src/ --include="*.cs"` → **0 non-handler results**  
      Verify DispatchShortcut remains `private void` (not async void).

- [ ] **SCAN-03** `grep -rn "throw new " src/ --include="*.cs"` → **0 hot-path results**  
      Verify the fix introduces no new throw statements.

- [ ] **SCAN-04** `grep -rn "return null;" src/ --include="*.cs"` → **0 new results**  
      Verify no return null introduced in the change set.

- [ ] **SCAN-05** `grep -rn "_beBufferBox" src/ --include="*.cs"` → **0 results**  
      Verify `_beBufferBox` field (line 202) and its usage (line 3065) are both fully removed.
      This scan is the primary correctness gate for B26: any remaining reference = incomplete fix.

- [ ] **SCAN-06** `python scripts/complexity_audit.py` → **DispatchShortcut CYC = 5**  
      Verify switch arm count is unchanged (4 cases + base 1 = CYC 5). Edit 2 removes 2 lines
      inside an existing arm; it does not add or remove arms.

- [ ] **SCAN-07** `grep -rn "\?\." src/PropTraderTools/TradeCopierPanel.cs` → **0 null-conditional event unsubscriptions**  
      Verify `DispatchShortcut` and the surrounding Key.B region have no `?.Event -=` patterns
      (NT8-043 compliance; `DispatchShortcut` performs no event operations).

### Build Verification

```powershell
dotnet build src/PropTraderTools/PropTraderTools.csproj
```

Expected: **0 errors, 0 warnings**

Critical: if `CS0103` appears for `_beBuffer` or `_beBufferBox`, the field edit sequence was
applied out of order. Apply Edit 2 before Edit 1 (remove usage before removing declaration).

Post-build sync (mandatory before F5 in NinjaTrader 8):

```powershell
powershell -File scripts\ptt-sync-and-verify.ps1
```

Expected: **0 MISMATCH** lines in output.

Then press **F5** in NinjaTrader 8 to recompile. GREEN = ticket complete.

### Deferred Backlog Carry-Forward

These items are NOT in scope for T1. Carry forward to B27.

| ID | Description | Priority | Status |
|----|-------------|----------|--------|
| DW-B24-01 | NT8-043 formal rule entry: runtime crash confirmation for null-conditional unsubscription | P2 | OPEN |
| DW-B24-02 | Manual E2E runtime verification: press Ctrl+Shift+B in live NT8 session — now unblocked by B26 | P1 | OPEN — execute immediately after B26 merges |
| DW-B24-03 | Skip-duplicate guard [Fact] for `if (acc == leader) continue` at CopyEngine.cs:~1195 | P2 | OPEN |
| DW-B25-01 | Companion plain-ref field race (`_pendingBeAccount` etc. singleton refs) | P3 | OPEN |

---

*ptt-architect · PTT-COPIER-B26 · 2026-07-07 · TICKETS_COMPLETE*
