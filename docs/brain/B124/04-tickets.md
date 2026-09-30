# B124 Tickets — BE Button Active-State Brush + Arm Guard

**Block**: B124  
**Phase**: 4 — Ticket Generation  
**Plan status**: REVIEW_PASS (`02-architecture-plan.md`)  
**Engineer contract**: This file is the sole implementation contract. Do not deviate.  
**Ticket count**: 1 (T1 covers both source fixes + new test file)

---

## SPEC REQUIREMENT TRACEABILITY

| Req ID | Description | Satisfied by |
|--------|-------------|--------------|
| B124-REQ-1 | `_globalBeBtn2` background = `BrushActive` when BE-ALL armed | T1 Change A |
| B124-REQ-2 | `_globalBeBtn2` background = `Transparent` when BE-ALL idle | Existing code — unchanged, no edit required |
| B124-REQ-3 | Second click on armed `_globalBeBtn2` logs `[PTT-BE-ALL] already armed, ignoring double-press` and returns without calling `Execute()` | T1 Change B |
| B124-REQ-4 | xUnit Test 1 PASS — guard is no-op when already armed | T1 Test File |
| B124-REQ-5 | xUnit Test 2 PASS — Execute fires when idle (first press) | T1 Test File |

---

## T1 — BE Button Brush Fix + Double-Press Guard + Tests

**Satisfies**: B124-REQ-1, B124-REQ-2, B124-REQ-3, B124-REQ-4, B124-REQ-5

### Files

| File | Action |
|------|--------|
| `src/PropTraderTools/TradeCopierPanel.cs` | Modify — 2 surgical locations |
| `src/PropTraderTools/Tests/B124Tests.cs` | Create new — 2 xUnit `[Fact]` tests |

**Files MUST NOT be touched** (zero changes permitted):
- `src/PropTraderTools/CopyEngine.cs`
- `src/PropTraderTools/TradeCopierAddOn.cs`
- `src/PropTraderTools/TradeCopierWindow.cs`

---

### Method Signatures

#### Change A target

```csharp
// File: src/PropTraderTools/TradeCopierPanel.cs
// Signature (do not change the signature — only change internal body, 1 line):
private void UpdateBeAllVisuals(BeState state)
```

#### Change B target

```csharp
// File: src/PropTraderTools/TradeCopierPanel.cs
// Signature (do not change the signature — only change the else-branch body):
private void OnGlobalBeClick(object sender, RoutedEventArgs e)
```

---

### Change A — `UpdateBeAllVisuals` — BrushCaution → BrushActive

**Location**: `src/PropTraderTools/TradeCopierPanel.cs` line ~1061  
**Scope**: One line replacement inside the else-branch of `UpdateBeAllVisuals`.  
**CYC before**: 3. **CYC after**: 3 (no branch added or removed).

**BEFORE** (exact text to find — do not change any surrounding lines):
```csharp
            _globalBeBtn2.Background = BrushCaution;
```

**AFTER** (exact replacement):
```csharp
            _globalBeBtn2.Background = BrushActive;
```

> `BrushActive` is already defined at line ~314 as:
> ```csharp
> private static readonly SolidColorBrush BrushActive = MakeBrush(34, 197, 94);
> ```
> No new field required.

---

### Change B — `OnGlobalBeClick` — Replace Disarm Else-Body with Guard

**Location**: `src/PropTraderTools/TradeCopierPanel.cs` lines ~1389–1400  
**Scope**: Replace the entire body of the `else` block (lines after `else {` through the closing `}`).  
**CYC before**: 4. **CYC after**: 2. Complexity decreases.

**BEFORE** (exact block to locate and replace — includes the brace-delimited else body):
```csharp
        else
        {
            // Currently Armed -- disarm
            NinjaTrader.Code.Output.Process(
                "[BE-ALL] button: disarm all",
                NinjaTrader.NinjaScript.PrintTo.OutputTab1
            );
            if (Account.All != null)
                foreach (var acc in Account.All)
                    CopyEngine.Instance.DisarmPendingBe(acc);
            UpdateBeAllVisuals(BeState.Idle);
        }
```

**AFTER** (exact replacement block):
```csharp
        else
        {
            // Already armed -- guard: log and return (no disarm, no re-arm)
            NinjaTrader.Code.Output.Process(
                "[PTT-BE-ALL] already armed, ignoring double-press",
                NinjaTrader.NinjaScript.PrintTo.OutputTab1
            );
            return;
        }
```

> **What is removed**: `Account.All` iteration, `CopyEngine.Instance.DisarmPendingBe(acc)` call,
> `UpdateBeAllVisuals(BeState.Idle)` from this path.  
> **What is added**: Guard log (ASCII-only string) + early `return;`.  
> **BREAKING CHANGE**: Double-click no longer disarms. This is intentional per B124 spec.

---

### New File — `src/PropTraderTools/Tests/B124Tests.cs`

**Framework**: xUnit only. NUnit and MSTest are BANNED per project mandate.  
**Namespace**: `PropTraderTools.Tests`  
**Class**: `B124Tests`  
**Test injection pattern**: Use `PttGlobalBreakEven(Action<Account, Instrument, double, bool>)`
injection constructor to count `Execute` invocations without any NT8 dispatcher dependency.

#### [Fact] Test 1 — `GuardReturnsWithoutRearmingWhenAlreadyArmed`

```csharp
[Fact]
public void GuardReturnsWithoutRearmingWhenAlreadyArmed()
```

**What it asserts**: When `IsPendingSlotsEmpty()` returns `false` (slots already occupied —
meaning BE is already armed), calling the guard-path equivalent must NOT invoke the Execute
delegate a second time.

**Arrange**:
- Create an `int executeCallCount = 0` counter.
- Instantiate `PttGlobalBreakEven` with the injection constructor, passing an `Action` that
  increments `executeCallCount`.
- Simulate the "already armed" condition: pre-populate the pending-slots state so that
  `IsPendingSlotsEmpty()` would return `false`.

**Act**:
- Call `Execute()` via the injection path in the condition where `IsPendingSlotsEmpty()` is `false`.
  Because the guard checks `IsPendingSlotsEmpty()` before invoking the delegate, the delegate
  must not fire.

**Assert**:
```csharp
Assert.Equal(0, executeCallCount);
```

#### [Fact] Test 2 — `FirstPressArmsWhenNotYetArmed`

```csharp
[Fact]
public void FirstPressArmsWhenNotYetArmed()
```

**What it asserts**: When `IsPendingSlotsEmpty()` returns `true` (no slots occupied — idle/not
armed), calling Execute must invoke the delegate exactly once.

**Arrange**:
- Create `int executeCallCount = 0`.
- Instantiate `PttGlobalBreakEven` with injection constructor.
- Ensure slots are empty (`IsPendingSlotsEmpty()` returns `true`).

**Act**:
- Call `Execute()` via the injection path in the condition where `IsPendingSlotsEmpty()` is `true`.

**Assert**:
```csharp
Assert.Equal(1, executeCallCount);
```

---

> **NOTE for engineer on test implementation**:  
> `OnGlobalBeClick` is a private WPF event handler and cannot be invoked directly in unit tests
> without an NT8 Dispatcher. Test the guard LOGIC UNIT via the `PttGlobalBreakEven` injection
> constructor seam. The injected `Action<Account, Instrument, double, bool>` delegate is the
> unit-observable proxy for "Execute was called". No NT8 Dispatcher required.  
> Do NOT attempt to reflect into `OnGlobalBeClick` — that would couple the test to the private
> WPF handler and require NT8 context initialization.

---

## 7-Scan Checklist (T1 — Engineer Signs Off Each Before BUILD_PASS)

The engineer MUST run every scan below and confirm the expected result before reporting
`BUILD_PASS`. Signing off a scan without running it = protocol violation.

---

### SCAN-01 — JS-021: `lock()` ban

**Rule**: JS-021 — No `lock()` anywhere in `src/`.  
**Command (PowerShell)**:
```powershell
Select-String -Path "src/PropTraderTools/TradeCopierPanel.cs" -Pattern "lock\("
```
**Expected**: 0 matches  
**Engineer sign-off**: `[ ]` SCAN-01 PASS — 0 matches confirmed

---

### SCAN-02 — JS-033: `async void` ban

**Rule**: JS-033 — `async void` banned except for event handlers.  
**Command (PowerShell)**:
```powershell
Select-String -Path "src/PropTraderTools/TradeCopierPanel.cs" -Pattern "async void"
```
**Expected**: 0 matches (event handlers in scope use `void` without `async` — exempt per
B124-REQ-NOTE-1; any pre-existing `async void` elsewhere is not introduced by T1 and must
not be changed by this ticket)  
**Engineer sign-off**: `[ ]` SCAN-02 PASS — 0 matches confirmed in modified methods

---

### SCAN-03 — CYC check all modified methods

**Rule**: CYC ≤ 8 (Jane Street strict standard).  
**Methods to check**:
- `UpdateBeAllVisuals` — expected CYC = **3** (unchanged from pre-B124; no branch delta)
- `OnGlobalBeClick` — expected CYC = **2** (reduced from 4; two decision branches removed)

**Command**:
```powershell
python scripts/complexity_audit.py
```
Or manual count per method (base=1, each `if`/`else if`/`foreach`/`&&`/`||` = +1).

**Expected**: `UpdateBeAllVisuals` ≤ 8, `OnGlobalBeClick` ≤ 8 (both well under threshold)  
**Engineer sign-off**: `[ ]` SCAN-03 PASS — UpdateBeAllVisuals=3, OnGlobalBeClick=2 confirmed

---

### SCAN-04 — ASCII-only (no Unicode in modified string literals)

**Rule**: ASCII-only identifiers and strings — no Unicode, no emoji, no curly quotes.  
**Command (PowerShell)**:
```powershell
Select-String -Path "src/PropTraderTools/TradeCopierPanel.cs" -Pattern "[^\x00-\x7F]"
```
**Expected**: 0 matches in lines added or modified by T1  
**String to verify as ASCII-safe**: `"[PTT-BE-ALL] already armed, ignoring double-press"`  
**Engineer sign-off**: `[ ]` SCAN-04 PASS — 0 non-ASCII characters in modified lines

---

### SCAN-05 — `return null` check in modified methods

**Rule**: JS-002 — no `return null` for missing values.  
**Scope**: Manually inspect `UpdateBeAllVisuals` and `OnGlobalBeClick` only.  
**Command (manual or)**:
```powershell
Select-String -Path "src/PropTraderTools/TradeCopierPanel.cs" -Pattern "return null"
```
**Expected**: 0 `return null` statements inside either modified method  
**Engineer sign-off**: `[ ]` SCAN-05 PASS — 0 return null in scope

---

### SCAN-06 — Build

**Rule**: Zero new errors, zero new warnings after T1 edits.  
**Command**:
```powershell
dotnet build src/PropTraderTools/PropTraderTools.csproj
```
**Expected**: `Build succeeded.` with 0 errors and 0 new warnings  
**Engineer sign-off**: `[ ]` SCAN-06 PASS — build succeeded

---

### SCAN-07 — xUnit tests pass

**Rule**: B124-REQ-4, B124-REQ-5 — both `[Fact]` tests green.  
**Command**:
```powershell
dotnet test
```
or, targeting B124Tests.cs specifically:
```powershell
dotnet test --filter "FullyQualifiedName~B124Tests"
```
**Expected**:
- `GuardReturnsWithoutRearmingWhenAlreadyArmed` → PASS
- `FirstPressArmsWhenNotYetArmed` → PASS

**Engineer sign-off**: `[ ]` SCAN-07 PASS — Test 1 PASS, Test 2 PASS

---

## Summary Checklist

```
T1 Change A  [ ] BrushCaution → BrushActive in UpdateBeAllVisuals (~line 1061)
T1 Change B  [ ] Replace else-body in OnGlobalBeClick with guard log + return (~lines 1389-1400)
T1 Test File [ ] B124Tests.cs created with GuardReturnsWithoutRearmingWhenAlreadyArmed + FirstPressArmsWhenNotYetArmed
SCAN-01      [ ] lock() = 0 matches
SCAN-02      [ ] async void = 0 matches in modified methods
SCAN-03      [ ] UpdateBeAllVisuals CYC=3, OnGlobalBeClick CYC=2
SCAN-04      [ ] ASCII-only = 0 non-ASCII in modified lines
SCAN-05      [ ] return null = 0 in scope
SCAN-06      [ ] dotnet build = 0 errors
SCAN-07      [ ] dotnet test B124Tests = 2/2 PASS
```

All boxes checked = **BUILD_PASS**. Report `TICKETS_COMPLETE` to orchestrator.
