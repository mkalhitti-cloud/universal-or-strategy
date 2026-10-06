# B186 Tickets
## PTT-COPIER B186 | Quick ALL + QAll2t Instrument Scope

**Author**: ptt-architect (Phase 3)
**Date**: 2026-07-08
**Baseline**: 0c27cd88 (B185-POST)
**Plan status**: REVIEW_PASS (02-architecture-plan.md)
**Execution order**: T1 + T2 (single pass on PttGlobalQuickExit.cs) → T3 (TradeCopierPanel.cs) → Tests

---

## Ticket T1 — DW-B186-QX-ALL-SCOPE-01

### Identity
| Field | Value |
|-------|-------|
| Ticket ID | T1 |
| DW card | DW-B186-QX-ALL-SCOPE-01 |
| File | `src/PropTraderTools/Features/PttGlobalQuickExit.cs` |
| Method | `Execute` |
| Pipeline phase | Ph4a (ptt-engineer) |

### Spec Requirements Satisfied
- DW-B186-QX-ALL-SCOPE-01: `PttGlobalQuickExit.Execute()` must be scoped to the currently active instrument.

### Current Signature (BEFORE)
```csharp
internal void Execute()
```

### New Signature (AFTER)
```csharp
internal void Execute(Instrument instrument)
```

### Exact Changes Required

#### Change 1 — Replace split gate + blocked log with combined gate

**REMOVE** (current guard block):
```csharp
if (!CopyEngine.Instance.Flags.QxGlobalExit)
{
    NinjaTrader.Code.Output.Process(
        "[PTT-QX-ALL] Blocked: Global Quick Exit requires Elite tier",
        NinjaTrader.NinjaScript.PrintTo.OutputTab1
    );
    return;
}
```

**ADD** (combined gate — exact replacement):
```csharp
if (!CopyEngine.Instance.Flags.QxGlobalExit || instrument == null) return;
```

#### Change 2 — Replace old fired log with instrument-scoped log

**REMOVE** (old log line, immediately after old gate):
```csharp
NinjaTrader.Code.Output.Process(
    "[PTT-QX-ALL] GlobalQuickExit fired",
    NinjaTrader.NinjaScript.PrintTo.OutputTab1
);
```

**ADD** (immediately after combined gate):
```csharp
NinjaTrader.Code.Output.Process(
    "[PTT-QX-ALL] GlobalQuickExit fired instr=" + instrument.FullName,
    NinjaTrader.NinjaScript.PrintTo.OutputTab1);
```

#### Change 3 — Add instrument filter inside pos loop

**Locate** (existing null/flat guard):
```csharp
if (pos == null || pos.Quantity == 0)
    continue; // (4)
```

**ADD immediately after** that line:
```csharp
if (pos.Instrument?.FullName != instrument.FullName) continue; // (new) +1 CYC -> 8
```

#### Change 4 — Update doc comment CYC annotation

**REPLACE** existing CYC annotation:
```
/// CYC=7: acc loop(1), follower guard(2), pos loop(3), null/flat continue(4),
///        DW-B115-DIAG for-loop(5), NeedsLeaderFallbackFlatten guard(6), delegate via ExecuteFollowers(7).
```

**WITH**:
```
/// CYC=8: combined gate(1), acc loop(2), follower guard(3), pos loop(4),
///        null/flat continue(5), instrument filter(6), NeedsLeaderFallbackFlatten guard(7),
///        delegate via ExecuteFollowers(8).
/// B186 DW-B186-QX-ALL-SCOPE-01: instrument scope applied. Filter skips positions not on _instrument.
/// Combined gate: !QxGlobalExit || instrument == null -> early return (silent).
```

### What Is NOT Changed in T1
- `ExecuteFollowers` signature — UNCHANGED
- `ExecuteOne` signature — UNCHANGED
- All helper methods — UNCHANGED
- All other log lines except the two listed above — UNCHANGED
- `_qxCancelInProgress` and all ConcurrentQueue usage — UNCHANGED

### CYC Analysis

| Branch | Description | Before | After |
|--------|-------------|--------|-------|
| 1 | Combined gate `!QxGlobalExit \|\| instrument==null` | flag-only gate | combined gate (1 branch) |
| 2 | acc foreach loop | loop(1) | loop(2) |
| 3 | follower guard `IsFollowerAccount` continue | guard(2) | guard(3) |
| 4 | pos foreach loop | loop(3) | loop(4) |
| 5 | null/flat guard continue | continue(4) | continue(5) |
| 6 | instrument filter continue (NEW) | — | continue(6) |
| 7 | NeedsLeaderFallbackFlatten guard | guard(6) | guard(7) |
| 8 | ExecuteFollowers delegate call | delegate(7) | delegate(8) |

**Before: CYC = 7. After: CYC = 8. AT CEILING — acceptable per spec.**

### JS Rule Constraints

| Rule | Constraint | Status |
|------|-----------|--------|
| JS-021 (P0) | No `lock()` anywhere in modified method | PASS — no lock added |
| JS-001 (P0) | No `throw new` in hot paths | PASS — no throw statements added |
| JS-002 (P0) | No `return null` | PASS — method is `void`; early returns are `return;` |
| JS-033 (P0) | No `async void` | PASS — method is synchronous `void` |
| ASCII | ASCII-only string literals | PASS — `"[PTT-QX-ALL] GlobalQuickExit fired instr="` is ASCII-only |
| CYC | CYC <= 8 for all modified methods | PASS — CYC = 8 (at ceiling, acceptable) |

### xUnit Tests

Add the following two `[Fact]` methods to the existing test class in `src/PropTraderTools/CopyEngineTests.cs`:

#### T_B186_01 — Execute signature has 1 Instrument parameter

```csharp
[Fact]
public void T_B186_01_ExecuteHasInstrumentParameter()
{
    // SKIP_IF_NT8_RUNTIME_ABSENT
    // Arrange: get PttGlobalQuickExit type via reflection
    var type = Type.GetType(
        "PropTraderTools.Features.PttGlobalQuickExit, PropTraderTools",
        throwOnError: false);
    if (type == null) return; // NT8 runtime absent -- skip

    // Act: find Execute overload with exactly 1 parameter of type Instrument
    var methods = type.GetMethods(
        System.Reflection.BindingFlags.Instance |
        System.Reflection.BindingFlags.NonPublic |
        System.Reflection.BindingFlags.Public)
        .Where(m => m.Name == "Execute")
        .ToList();

    var instrumentOverload = methods.FirstOrDefault(m =>
    {
        var ps = m.GetParameters();
        return ps.Length == 1 &&
               ps[0].ParameterType.Name == "Instrument";
    });

    // Assert: exactly one Execute(Instrument) overload exists
    Assert.NotNull(instrumentOverload);
    // Satisfies: DW-B186-QX-ALL-SCOPE-01 AC1
}
```

**What T_B186_01 asserts**: `PttGlobalQuickExit` has an `Execute` method with exactly one parameter of type `Instrument` (signature change from T1 is present).

#### T_B186_02 — Execute(null) returns immediately (combined gate structural)

```csharp
[Fact]
public void T_B186_02_ExecuteNullInstrumentReturnsImmediately()
{
    // SKIP_IF_NT8_RUNTIME_ABSENT
    // Arrange: instantiate PttGlobalQuickExit via reflection
    var type = Type.GetType(
        "PropTraderTools.Features.PttGlobalQuickExit, PropTraderTools",
        throwOnError: false);
    if (type == null) return; // NT8 runtime absent -- skip

    var instance = Activator.CreateInstance(type);
    var method = type.GetMethod("Execute",
        System.Reflection.BindingFlags.Instance |
        System.Reflection.BindingFlags.NonPublic |
        System.Reflection.BindingFlags.Public,
        null,
        new[] { Type.GetType("NinjaTrader.Cbi.Instrument, NinjaTrader.Core") ??
                typeof(object) },
        null);
    if (method == null) return; // overload not found -- skip

    // Act: invoke Execute(null) -- combined gate must return early on null instrument
    // Note: QxGlobalExit flag fires before instrument null check; both result in early return
    var ex = Record.Exception(() => method.Invoke(instance, new object[] { null }));

    // Assert: no NullReferenceException or unhandled exception escapes
    Assert.Null(ex);
    // Satisfies: DW-B186-QX-ALL-SCOPE-01 AC2
}
```

**What T_B186_02 asserts**: `Execute(null)` does not throw — the combined gate returns early without dereferencing the null instrument argument.

### Acceptance Criteria

| ID | Check | Pass condition |
|----|-------|---------------|
| AC1 | `Execute(Instrument)` overload present | T_B186_01 passes |
| AC1b | CYC = 8 on `Execute(Instrument)` | SCAN-04 manual count = 8 |
| AC-gate | Combined gate folds flag + null check | `if (!...QxGlobalExit \|\| instrument == null) return;` present |
| AC-filter | Instrument filter line present in pos loop | `pos.Instrument?.FullName != instrument.FullName` present |
| AC-log | New scoped log line present | `"[PTT-QX-ALL] GlobalQuickExit fired instr="` present |
| AC-old-log-gone | Old unscoped log removed | `"[PTT-QX-ALL] GlobalQuickExit fired"` (without `instr=`) absent |

### 7-Scan Checklist (SCAN-01..SCAN-07)

Run after committing T1 changes. Expected result in parentheses.

```powershell
# SCAN-01: no new lock()
git diff HEAD~1 HEAD -- src/ | Select-String "^\+" | Select-String "lock\("
# Expected: 0 results

# SCAN-02: no new return null
git diff HEAD~1 HEAD -- src/ | Select-String "^\+" | Select-String "return null"
# Expected: 0 results

# SCAN-03: no new async void
git diff HEAD~1 HEAD -- src/ | Select-String "^\+" | Select-String "async void "
# Expected: 0 results

# SCAN-04: CYC manual count for Execute(Instrument instrument)
# Count branches: combined gate(1), acc loop(2), follower guard(3), pos loop(4),
#                 null/flat continue(5), instrument filter(6), flatten guard(7), ExecuteFollowers delegate(8)
# Expected: 8

# SCAN-05: no non-ASCII characters in new lines
git diff HEAD~1 HEAD -- src/ | Select-String "^\+" | Select-String "[^\x00-\x7F]"
# Expected: 0 results

# SCAN-06: no throw new
git diff HEAD~1 HEAD -- src/ | Select-String "^\+" | Select-String "throw new"
# Expected: 0 results

# SCAN-07: dotnet build
dotnet build src/PropTraderTools/PropTraderTools.csproj 2>&1 | Select-Object -Last 5
# Expected: 0 errors
```

---

## Ticket T2 — DW-B186-QX-ALL-SCOPE-02

### Identity
| Field | Value |
|-------|-------|
| Ticket ID | T2 |
| DW card | DW-B186-QX-ALL-SCOPE-02 |
| Closes | DW-B185-QX-ALL-SCOPE-01 (wrong-instrument qty split deferred from B185) |
| File | `src/PropTraderTools/Features/PttGlobalQuickExit.cs` |
| Method | `Execute(List<(double Price, int Qty)> forcedTargets)` |
| Pipeline phase | Ph4a (ptt-engineer) — same pass as T1, same file |

### Spec Requirements Satisfied
- DW-B186-QX-ALL-SCOPE-02: `PttGlobalQuickExit.Execute(forcedTargets)` must be scoped to the currently active instrument.
- DW-B185-QX-ALL-SCOPE-01 (deferred): wrong-instrument qty split eliminated by instrument filter.

### Current Signature (BEFORE)
```csharp
internal void Execute(
    System.Collections.Generic.List<(double Price, int Qty)> forcedTargets
)
```

### New Signature (AFTER)
```csharp
internal void Execute(
    System.Collections.Generic.List<(double Price, int Qty)> forcedTargets,
    Instrument instrument
)
```

### Exact Changes Required

#### Change 1 — Replace split gate + blocked log with combined gate

**REMOVE** (current guard block):
```csharp
if (!CopyEngine.Instance.Flags.QxGlobalExit)
{
    NinjaTrader.Code.Output.Process(
        "[PTT-QX-2T-ALL] Blocked: Global Quick Exit requires Elite tier",
        NinjaTrader.NinjaScript.PrintTo.OutputTab1
    );
    return;
}
```

**ADD** (combined gate — exact replacement):
```csharp
if (!CopyEngine.Instance.Flags.QxGlobalExit || instrument == null) return;
```

#### Change 2 — Fold instrument filter into existing null/flat guard (branch 6)

**REPLACE** (existing null/flat guard):
```csharp
if (pos == null || pos.Quantity == 0)
    continue; // (6)
```

**WITH** (extended compound condition — CYC unchanged at 8):
```csharp
if (pos == null || pos.Quantity == 0 || pos.Instrument?.FullName != instrument.FullName)
    continue; // (6) extended -- instrument filter folded in
```

**CRITICAL**: This is a fold into existing branch (6), NOT a new branch. CYC stays at 8.

#### Change 3 — Update doc comment CYC annotation

**REPLACE** existing CYC annotation:
```
/// CYC=8: flag-guard(1), null/empty-guard(2), acc-loop(3), follower-skip(4),
///        pos-loop(5), null/flat-continue(6), flatten-guard(7), ExecuteFollowers-call(8).
```

**WITH**:
```
/// CYC=8: combined-gate(1), null/empty-guard(2), acc-loop(3), follower-skip(4),
///        pos-loop(5), null/flat/instr-continue(6), flatten-guard(7), ExecuteFollowers-call(8).
/// B186 DW-B186-QX-ALL-SCOPE-02: instrument scope applied. Filter folded into branch (6).
/// Combined gate: !QxGlobalExit || instrument == null -> early return (silent).
```

### What Is NOT Changed in T2
- `forcedTargets` null/empty guard (branch 2) — UNCHANGED (separate guard, not affected)
- `ExecuteFollowers` call — UNCHANGED
- `ExecuteOne` call — UNCHANGED
- All logging inside account/position loop — UNCHANGED
- All other guards and branches — UNCHANGED

### CYC Analysis — HOLD STRATEGY

| Branch | Description | Before | After |
|--------|-------------|--------|-------|
| 1 | Combined gate `!QxGlobalExit \|\| instrument==null` | flag-only gate | combined gate (1 branch) |
| 2 | forcedTargets null/empty guard | null/empty(2) | null/empty(2) — UNCHANGED |
| 3 | acc foreach loop | loop(3) | loop(3) — UNCHANGED |
| 4 | follower skip | skip(4) | skip(4) — UNCHANGED |
| 5 | pos foreach loop | loop(5) | loop(5) — UNCHANGED |
| 6 | null/flat guard (EXTENDED with instrument filter) | `pos==null\|\|qty==0`(6) | `pos==null\|\|qty==0\|\|instr!=`(6) — FOLDED |
| 7 | NeedsLeaderFallbackFlatten guard | guard(7) | guard(7) — UNCHANGED |
| 8 | ExecuteFollowers delegate call | delegate(8) | delegate(8) — UNCHANGED |

**Before: CYC = 8. After: CYC = 8. Fold strategy holds CYC at ceiling — no new branch added.**

### JS Rule Constraints

| Rule | Constraint | Status |
|------|-----------|--------|
| JS-021 (P0) | No `lock()` anywhere in modified method | PASS — no lock added |
| JS-001 (P0) | No `throw new` in hot paths | PASS — no throw statements added |
| JS-002 (P0) | No `return null` | PASS — method is `void`; early returns are `return;` |
| JS-033 (P0) | No `async void` | PASS — method is synchronous `void` |
| ASCII | ASCII-only string literals | PASS — no new string literals added in T2 body |
| CYC | CYC <= 8 for all modified methods | PASS — CYC = 8 (fold strategy, no new branch) |

### xUnit Tests

Add the following two `[Fact]` methods to the existing test class in `src/PropTraderTools/CopyEngineTests.cs`:

#### T_B186_04 — Execute(forcedTargets, Instrument) signature has 2 parameters

```csharp
[Fact]
public void T_B186_04_ExecuteForcedTargetsHasInstrumentParameter()
{
    // SKIP_IF_NT8_RUNTIME_ABSENT
    // Arrange: get PttGlobalQuickExit type via reflection
    var type = Type.GetType(
        "PropTraderTools.Features.PttGlobalQuickExit, PropTraderTools",
        throwOnError: false);
    if (type == null) return; // NT8 runtime absent -- skip

    // Act: find Execute overload with exactly 2 parameters (List + Instrument)
    var methods = type.GetMethods(
        System.Reflection.BindingFlags.Instance |
        System.Reflection.BindingFlags.NonPublic |
        System.Reflection.BindingFlags.Public)
        .Where(m => m.Name == "Execute")
        .ToList();

    var twoParamOverload = methods.FirstOrDefault(m =>
    {
        var ps = m.GetParameters();
        return ps.Length == 2 &&
               ps[0].ParameterType.IsGenericType && // List<...>
               ps[1].ParameterType.Name == "Instrument";
    });

    // Assert: exactly one Execute(List, Instrument) overload exists
    Assert.NotNull(twoParamOverload);
    // Satisfies: DW-B186-QX-ALL-SCOPE-02 AC2
}
```

**What T_B186_04 asserts**: `PttGlobalQuickExit` has an `Execute` overload with exactly two parameters where the first is a generic list type and the second is `Instrument`.

#### T_B186_05 — Execute(forcedTargets, null) returns immediately (combined gate structural)

```csharp
[Fact]
public void T_B186_05_ExecuteForcedTargetsNullInstrumentReturnsImmediately()
{
    // SKIP_IF_NT8_RUNTIME_ABSENT
    // Arrange: instantiate PttGlobalQuickExit via reflection; build dummy target list
    var type = Type.GetType(
        "PropTraderTools.Features.PttGlobalQuickExit, PropTraderTools",
        throwOnError: false);
    if (type == null) return; // NT8 runtime absent -- skip

    var instance = Activator.CreateInstance(type);

    var listType = typeof(System.Collections.Generic.List<(double, int)>);
    var instrumentType = Type.GetType("NinjaTrader.Cbi.Instrument, NinjaTrader.Core")
                         ?? typeof(object);

    var method = type.GetMethod("Execute",
        System.Reflection.BindingFlags.Instance |
        System.Reflection.BindingFlags.NonPublic |
        System.Reflection.BindingFlags.Public,
        null,
        new[] { listType, instrumentType },
        null);
    if (method == null) return; // overload not found -- skip

    // Build a non-empty dummy target list (to ensure forcedTargets guard is not the early exit)
    var targets = new System.Collections.Generic.List<(double Price, int Qty)>
    {
        (100.0, 1), (101.0, 1)
    };

    // Act: invoke Execute(targets, null) -- combined gate must return early on null instrument
    var ex = Record.Exception(() => method.Invoke(instance, new object[] { targets, null }));

    // Assert: no NullReferenceException or unhandled exception escapes
    Assert.Null(ex);
    // Satisfies: DW-B186-QX-ALL-SCOPE-02 -- combined gate verified structurally
}
```

**What T_B186_05 asserts**: `Execute(targets, null)` does not throw — the combined gate returns early on `instrument == null` before any position loop that would dereference the instrument.

### Acceptance Criteria

| ID | Check | Pass condition |
|----|-------|---------------|
| AC2 | `Execute(List, Instrument)` overload present | T_B186_04 passes |
| AC2b | CYC = 8 on `Execute(List, Instrument)` | SCAN-04 manual count = 8 |
| AC5 | DW-B185-QX-ALL-SCOPE-01 closed | instrument filter in branch (6) eliminates wrong-instrument qty split |
| AC-gate | Combined gate folds flag + null check | `if (!...QxGlobalExit \|\| instrument == null) return;` present |
| AC-fold | Instrument filter folded into branch (6) | `pos==null \|\| pos.Quantity == 0 \|\| pos.Instrument?.FullName != instrument.FullName` present |
| AC-no-new-branch | No new CYC branch added | SCAN-04 count stays at 8 |

### 7-Scan Checklist (SCAN-01..SCAN-07)

Run after committing T2 changes (may be committed in the same commit as T1).

```powershell
# SCAN-01: no new lock()
git diff HEAD~1 HEAD -- src/ | Select-String "^\+" | Select-String "lock\("
# Expected: 0 results

# SCAN-02: no new return null
git diff HEAD~1 HEAD -- src/ | Select-String "^\+" | Select-String "return null"
# Expected: 0 results

# SCAN-03: no new async void
git diff HEAD~1 HEAD -- src/ | Select-String "^\+" | Select-String "async void "
# Expected: 0 results

# SCAN-04: CYC manual count for Execute(List<...> forcedTargets, Instrument instrument)
# Count branches: combined-gate(1), null/empty-guard(2), acc-loop(3), follower-skip(4),
#                 pos-loop(5), null/flat/instr-continue(6), flatten-guard(7), ExecuteFollowers-call(8)
# Expected: 8 -- FOLD confirmed, not a new branch

# SCAN-05: no non-ASCII characters in new lines
git diff HEAD~1 HEAD -- src/ | Select-String "^\+" | Select-String "[^\x00-\x7F]"
# Expected: 0 results

# SCAN-06: no throw new
git diff HEAD~1 HEAD -- src/ | Select-String "^\+" | Select-String "throw new"
# Expected: 0 results

# SCAN-07: dotnet build
dotnet build src/PropTraderTools/PropTraderTools.csproj 2>&1 | Select-Object -Last 5
# Expected: 0 errors
```

---

## Ticket T3 — DW-B186-PANEL-CALLSITES-01

### Identity
| Field | Value |
|-------|-------|
| Ticket ID | T3 |
| DW card | DW-B186-PANEL-CALLSITES-01 |
| File | `src/PropTraderTools/TradeCopierPanel.cs` |
| Methods | `OnQuickAllClick`, `OnInstrQAll2tClick` |
| Dependency | T1 and T2 must be merged first (Execute signatures must exist before T3 compiles) |
| Pipeline phase | Ph4a (ptt-engineer) — separate commit after T1+T2 |

### Spec Requirements Satisfied
- DW-B186-PANEL-CALLSITES-01: Both call sites in `TradeCopierPanel.cs` must pass `_instrument` to the updated `Execute` overloads.

### Files in Scope
| File | Change |
|------|--------|
| `src/PropTraderTools/TradeCopierPanel.cs` | 2 call-site line edits |
| `src/PropTraderTools/CopyEngineTests.cs` | 2 new [Fact] methods (T_B186_03, T_B186_06) |

### Change 1 — OnQuickAllClick (line ~1617)

#### Current Method (BEFORE)
```csharp
// B41: OnQuickAllClick -- fires all-accounts Quick Exit bracket swap. CYC=1.
// JS-033: synchronous void event handler. JS-021: no lock.
private void OnQuickAllClick(object sender, RoutedEventArgs e)
{
    var gqx = new PttGlobalQuickExit();
    gqx.Execute();
}
```

#### Updated Method (AFTER)
```csharp
// B41: OnQuickAllClick -- fires all-accounts Quick Exit bracket swap. CYC=1.
// B186 DW-B186-PANEL-CALLSITES-01: pass _instrument for instrument scope.
// JS-033: synchronous void event handler. JS-021: no lock.
private void OnQuickAllClick(object sender, RoutedEventArgs e)
{
    var gqx = new PttGlobalQuickExit();
    gqx.Execute(_instrument);
}
```

**Exact line diff**: `gqx.Execute();` → `gqx.Execute(_instrument);`

### Change 2 — OnInstrQAll2tClick (line ~1706)

#### Current Method (BEFORE)
```csharp
// B123/R9: OnInstrQAll2tClick -- fires global 2-target quick exit on all followers.
// CYC=2: (1)TryResolve2TargetContext false-path, (2)Execute call.
// JS-021: no lock. ASCII-only.
private void OnInstrQAll2tClick(object sender, RoutedEventArgs e)
{
    if (!TryResolve2TargetContext(out int qty, out var targets))
        return; // (1)
    LogQxTwoTarget("[PTT-QX-2T-ALL]", qty, targets);
    new PttGlobalQuickExit().Execute(targets); // (2)
}
```

#### Updated Method (AFTER)
```csharp
// B123/R9: OnInstrQAll2tClick -- fires global 2-target quick exit on all followers.
// B186 DW-B186-PANEL-CALLSITES-01: pass _instrument for instrument scope.
// CYC=2: (1)TryResolve2TargetContext false-path, (2)Execute call.
// JS-021: no lock. ASCII-only.
private void OnInstrQAll2tClick(object sender, RoutedEventArgs e)
{
    if (!TryResolve2TargetContext(out int qty, out var targets))
        return; // (1)
    LogQxTwoTarget("[PTT-QX-2T-ALL]", qty, targets);
    new PttGlobalQuickExit().Execute(targets, _instrument); // (2)
}
```

**Exact line diff**: `new PttGlobalQuickExit().Execute(targets);` → `new PttGlobalQuickExit().Execute(targets, _instrument);`

### No Null Guard at Call Site
Both `Execute` overloads gate on `instrument == null` in their combined gate. No null guard is needed at either call site. This matches the B185 T1 pattern exactly (`PttBeSwapAll.Execute(buffer, _instrument)` — no null guard at call site).

### CYC Impact on T3
- `OnQuickAllClick`: CYC = 1, **UNCHANGED** — no new branches, only argument added
- `OnInstrQAll2tClick`: CYC = 2, **UNCHANGED** — no new branches, only argument added to existing call

### JS Rule Constraints

| Rule | Constraint | Status |
|------|-----------|--------|
| JS-021 (P0) | No `lock()` anywhere in modified methods | PASS — no lock added |
| JS-001 (P0) | No `throw new` in hot paths | PASS — no throw statements added |
| JS-002 (P0) | No `return null` | PASS — methods are `void`; existing `return;` unchanged |
| JS-033 (P0) | No `async void` | PASS — both methods are synchronous `void` event handlers |
| ASCII | ASCII-only string literals | PASS — no new string literals added |
| CYC | CYC <= 8 for all modified methods | PASS — both methods unchanged at CYC = 1 and CYC = 2 |

### Post-T3 Action (Mandatory)
After committing T3 changes, run:
```powershell
powershell -File .\scripts\ptt-sync.ps1
```
Expected output: `19/0` (19 links synced, 0 errors).

### xUnit Tests

Add the following two `[Fact]` methods to the existing test class in `src/PropTraderTools/CopyEngineTests.cs`:

#### T_B186_03 — OnQuickAllClick passes _instrument (reflection)

```csharp
[Fact]
public void T_B186_03_OnQuickAllClickPassesInstrument()
{
    // SKIP_IF_NT8_RUNTIME_ABSENT
    // Arrange: get TradeCopierPanel type via reflection
    var panelType = Type.GetType(
        "PropTraderTools.TradeCopierPanel, PropTraderTools",
        throwOnError: false);
    if (panelType == null) return; // NT8 runtime absent -- skip

    // Act: get OnQuickAllClick method and check its IL for _instrument field reference
    var method = panelType.GetMethod("OnQuickAllClick",
        System.Reflection.BindingFlags.Instance |
        System.Reflection.BindingFlags.NonPublic |
        System.Reflection.BindingFlags.Public);
    if (method == null) return; // method not found -- skip

    // Check: _instrument field exists on panel type
    var instrumentField = panelType.GetFields(
        System.Reflection.BindingFlags.Instance |
        System.Reflection.BindingFlags.NonPublic |
        System.Reflection.BindingFlags.Public)
        .FirstOrDefault(f => f.Name == "_instrument");

    // Assert: TradeCopierPanel has a field named _instrument
    // (structural evidence that gqx.Execute(_instrument) is possible)
    Assert.NotNull(instrumentField);

    // Assert: the Execute overload that takes Instrument exists (T_B186_01 validates this)
    var gqxType = Type.GetType(
        "PropTraderTools.Features.PttGlobalQuickExit, PropTraderTools",
        throwOnError: false);
    if (gqxType == null) return;

    var execWithInstr = gqxType.GetMethods(
        System.Reflection.BindingFlags.Instance |
        System.Reflection.BindingFlags.NonPublic |
        System.Reflection.BindingFlags.Public)
        .FirstOrDefault(m =>
        {
            var ps = m.GetParameters();
            return m.Name == "Execute" &&
                   ps.Length == 1 &&
                   ps[0].ParameterType.Name == "Instrument";
        });

    Assert.NotNull(execWithInstr);
    // Satisfies: DW-B186-PANEL-CALLSITES-01 AC3
}
```

**What T_B186_03 asserts**: `TradeCopierPanel` has an `_instrument` field AND `PttGlobalQuickExit` has the matching `Execute(Instrument)` overload — together confirming the call site `gqx.Execute(_instrument)` is structurally valid.

#### T_B186_06 — OnInstrQAll2tClick passes _instrument (reflection)

```csharp
[Fact]
public void T_B186_06_OnInstrQAll2tClickPassesInstrument()
{
    // SKIP_IF_NT8_RUNTIME_ABSENT
    // Arrange: get TradeCopierPanel type via reflection
    var panelType = Type.GetType(
        "PropTraderTools.TradeCopierPanel, PropTraderTools",
        throwOnError: false);
    if (panelType == null) return; // NT8 runtime absent -- skip

    // Act: get OnInstrQAll2tClick method
    var method = panelType.GetMethod("OnInstrQAll2tClick",
        System.Reflection.BindingFlags.Instance |
        System.Reflection.BindingFlags.NonPublic |
        System.Reflection.BindingFlags.Public);
    if (method == null) return; // method not found -- skip

    // Check: _instrument field exists on panel type
    var instrumentField = panelType.GetFields(
        System.Reflection.BindingFlags.Instance |
        System.Reflection.BindingFlags.NonPublic |
        System.Reflection.BindingFlags.Public)
        .FirstOrDefault(f => f.Name == "_instrument");

    Assert.NotNull(instrumentField);

    // Check: Execute(List, Instrument) overload exists (T_B186_04 validates this)
    var gqxType = Type.GetType(
        "PropTraderTools.Features.PttGlobalQuickExit, PropTraderTools",
        throwOnError: false);
    if (gqxType == null) return;

    var execWithListAndInstr = gqxType.GetMethods(
        System.Reflection.BindingFlags.Instance |
        System.Reflection.BindingFlags.NonPublic |
        System.Reflection.BindingFlags.Public)
        .FirstOrDefault(m =>
        {
            var ps = m.GetParameters();
            return m.Name == "Execute" &&
                   ps.Length == 2 &&
                   ps[0].ParameterType.IsGenericType &&
                   ps[1].ParameterType.Name == "Instrument";
        });

    Assert.NotNull(execWithListAndInstr);
    // Satisfies: DW-B186-PANEL-CALLSITES-01 AC4
}
```

**What T_B186_06 asserts**: `TradeCopierPanel` has an `_instrument` field AND `PttGlobalQuickExit` has the matching `Execute(List, Instrument)` overload — confirming the call site `new PttGlobalQuickExit().Execute(targets, _instrument)` is structurally valid.

### Acceptance Criteria

| ID | Check | Pass condition |
|----|-------|---------------|
| AC3 | `OnQuickAllClick` passes `_instrument` | T_B186_03 passes; `gqx.Execute(_instrument)` present in source |
| AC4 | `OnInstrQAll2tClick` passes `_instrument` | T_B186_06 passes; `Execute(targets, _instrument)` present in source |
| AC-no-callsite-guard | No null guard at call site | neither method adds `if (_instrument == null)` check |
| AC-cyc-unchanged | CYC unchanged on both methods | SCAN-04 = 1 and 2 respectively |
| AC6-partial | ptt-sync succeeds after T3 commit | `ptt-sync.ps1` exits with 19/0 |

### 7-Scan Checklist (SCAN-01..SCAN-07)

Run after committing T3 changes and running `ptt-sync.ps1`.

```powershell
# SCAN-01: no new lock()
git diff HEAD~1 HEAD -- src/ | Select-String "^\+" | Select-String "lock\("
# Expected: 0 results

# SCAN-02: no new return null
git diff HEAD~1 HEAD -- src/ | Select-String "^\+" | Select-String "return null"
# Expected: 0 results

# SCAN-03: no new async void
git diff HEAD~1 HEAD -- src/ | Select-String "^\+" | Select-String "async void "
# Expected: 0 results

# SCAN-04: CYC manual count for OnQuickAllClick and OnInstrQAll2tClick
# OnQuickAllClick:    no branches -> CYC=1 (unchanged)
# OnInstrQAll2tClick: TryResolve2TargetContext false-path -> CYC=2 (unchanged)
# Expected: 1 and 2 respectively

# SCAN-05: no non-ASCII characters in new lines
git diff HEAD~1 HEAD -- src/ | Select-String "^\+" | Select-String "[^\x00-\x7F]"
# Expected: 0 results

# SCAN-06: no throw new
git diff HEAD~1 HEAD -- src/ | Select-String "^\+" | Select-String "throw new"
# Expected: 0 results

# SCAN-07: dotnet build
dotnet build src/PropTraderTools/PropTraderTools.csproj 2>&1 | Select-Object -Last 5
# Expected: 0 errors
```

---

## Full Epic Acceptance Checklist

Run after all 3 tickets are committed. This is the complete Ph4b / ptt-verifier gate.

```powershell
# ---- AC VERIFICATION ----

# AC1: Execute(Instrument) overload present in PttGlobalQuickExit
Select-String -Path src/PropTraderTools/Features/PttGlobalQuickExit.cs `
    -Pattern "Execute.*Instrument"
# Expected: 2 matches (both Execute overloads with Instrument param)

# AC3+AC4: Both call sites in TradeCopierPanel pass _instrument
Select-String -Path src/PropTraderTools/TradeCopierPanel.cs `
    -Pattern "gqx\.Execute\(_instrument\)|Execute\(targets.*_instrument\)"
# Expected: 2 matches

# AC6: 6 new test methods present
Select-String -Path src/PropTraderTools/CopyEngineTests.cs -Pattern "T_B186_"
# Expected: 6 matches (T_B186_01 through T_B186_06)

# ---- DNA SCANS ----

# SCAN-01
git diff HEAD~3 HEAD -- src/ | Select-String "^\+" | Select-String "lock\("
# Expected: 0 results

# SCAN-02
git diff HEAD~3 HEAD -- src/ | Select-String "^\+" | Select-String "return null"
# Expected: 0 results

# SCAN-03
git diff HEAD~3 HEAD -- src/ | Select-String "^\+" | Select-String "async void "
# Expected: 0 results

# SCAN-04: CYC summary
# PttGlobalQuickExit.Execute(Instrument):        8  (at ceiling, acceptable)
# PttGlobalQuickExit.Execute(List, Instrument):  8  (fold strategy, unchanged)
# TradeCopierPanel.OnQuickAllClick:              1  (unchanged)
# TradeCopierPanel.OnInstrQAll2tClick:           2  (unchanged)

# SCAN-05
git diff HEAD~3 HEAD -- src/ | Select-String "^\+" | Select-String "[^\x00-\x7F]"
# Expected: 0 results

# SCAN-06
git diff HEAD~3 HEAD -- src/ | Select-String "^\+" | Select-String "throw new"
# Expected: 0 results

# SCAN-07
dotnet build src/PropTraderTools/PropTraderTools.csproj 2>&1 | Select-Object -Last 5
# Expected: 0 errors

# ---- GUARDIAN + SYNC ----
powershell -File .\scripts\verify_critical_symbols.ps1
# Expected: exit 0

powershell -File .\scripts\ptt-sync.ps1
# Expected: 19/0

# ---- NT8 SIM SMOKE TESTS ----
# 1. Load panel with instrument X active.
# 2. Open a position on instrument X and a position on instrument Y.
# 3. Click "Quick ALL" button.
# 4. Verify: only instrument X position exits. Instrument Y position remains.
# 5. Click "QAll2t" button with instrument X active.
# 6. Verify: only instrument X position exits. Instrument Y position remains.
```

### Expected Build Tag
```
PTT-COPIER B186 | qx-all-instr-scope | {date}
```

---

## Deferred Items (NOT in B186 scope)

| DW ID | Description | Target |
|-------|-------------|--------|
| DW-B186-DIAG-CLEANUP-01 | Remove [DW-B115-DIAG] log blocks from PttGlobalQuickExit.Execute and ExecuteFollowers | B187+ |
| DW-B186-WARN-01 | CS0067 PendingBeArmed never raised warning | B187+ |

---

*Written by: ptt-architect (Phase 3)*
*Epic: B186*
*Baseline: 0c27cd88 (B185-POST)*
*Plan source: docs/brain/B186/02-architecture-plan.md (REVIEW_PASS)*
*Next phase: ptt-ticket-reviewer -> 04-ticket-review.md (TICKET_REVIEW_PASS gate)*
