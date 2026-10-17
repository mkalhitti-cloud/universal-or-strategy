# B192 Engineer Tickets
# GlobalQuickAll MGC/GC Tick Default Bug Fix

**Epic**: B192
**Author**: ptt-architect (Phase 3)
**Date**: 2026-07-15
**Source plan**: docs/brain/B192/02-architecture-plan.md (REVIEW_PASS, cycle 2)
**Plan review**: docs/brain/B192/02-plan-review.md (REVIEW_PASS)

---

## Execution Order

```
Lane A (independent -- implement first or in parallel with Lane B):
  T1 -- PttQuickExit.cs: modify GetQuickTicks
      -> xUnit tests pass
      -> SCAN-01..07 pass on changed lines
      -> Commit: "B192-T1: add GC->10 and explicit ES/NQ/MNQ->4 in InstrumentDefaults"

Lane B (coupled -- T2 before T3 in ticket order, but T2=CopyEngine MUST be implemented first):
  T2 -- CopyEngine.cs: add ResetGlobalQuickAll (compile prerequisite for T3)
      -> xUnit tests pass
      -> SCAN-01..07 pass on changed lines
  T3 -- TradeCopierPanel.cs: add ResetGlobalQuickAll call in SetInstrument
      -> SIM verification pass
      -> SCAN-01..07 pass on changed lines
      -> Commit: "B192-T2T3: sync GlobalQuickAllT1 to instrument default on SetInstrument"
```

> **Lane B compile dependency**: T2 (`CopyEngine.ResetGlobalQuickAll`) MUST be written
> before T3 (`TradeCopierPanel.SetInstrument`) or the project will not compile.
> Implement T2 first, then T3.

---

## T1 — InstrumentDefaults.GetQuickTicks (PttQuickExit.cs)

### Ticket Header

| Field | Value |
|-------|-------|
| Ticket ID | T1 |
| Title | Add GC->(10,20) guard and explicit ES/NQ/MNQ->(4,8) guards in GetQuickTicks |
| File | `src/PropTraderTools/Features/PttQuickExit.cs` |
| Lane | A (independent) |
| Spec Requirements | SR-B192-01, SR-B192-02 |
| Dependency | None |

### Spec Requirements Satisfied

- **SR-B192-01**: `GetQuickTicks("GC")` and `GetQuickTicks("GC 06-26")` must return `(10, 20)`.
  GC full-size Gold shares the same tick size as MGC ($0.10/tick; 10 ticks = 1 point).
- **SR-B192-02**: Explicit entries for ES, NQ, MNQ returning `(4, 8)` must be present.
  Adds documentation-clarity guards for instruments that previously relied on the default fallback.

### Source Baseline (BEFORE)

File: [`src/PropTraderTools/Features/PttQuickExit.cs`](src/PropTraderTools/Features/PttQuickExit.cs:374), lines 374-395.

```csharp
// BEFORE (lines 374-395):
    /// InstrumentDefaults: per-instrument Quick Exit tick defaults.
    /// Called from CopyEngine.GetDefaultQuickTicks() and PttQuickExit fallback paths.
    /// </summary>
    internal static class InstrumentDefaults
    {
        /// <summary>
        /// GetQuickTicks: returns (T1 ticks, T2 ticks) for the given master instrument name.
        /// MES: (4, 8) = 1pt/2pt at 0.25 tick. MGC: (10, 20) = 1pt/2pt at 0.1 tick.
        /// Default: MES ticks (4, 8). CYC=3 (null/empty guard + MES check + MGC check).
        /// JS-002: returns tuple (never returns null). ASCII-only strings.
        /// </summary>
        internal static (int t1, int t2) GetQuickTicks(string masterName)
        {
            if (string.IsNullOrEmpty(masterName))
                return (4, 8); // (1)
            if (masterName.StartsWith("MES"))
                return (4, 8); // (2)
            if (masterName.StartsWith("MGC"))
                return (10, 20); // B190-T1: 1pt/2pt at 0.1 tick
            return (4, 8);
        }
    }
}
```

### Target Implementation (AFTER)

Replace the XML doc comment and entire method body (lines 379-394) with:

```csharp
        /// <summary>
        /// GetQuickTicks: returns (T1 ticks, T2 ticks) for the given master instrument name.
        /// MES/ES: (4, 8) = 1pt/2pt at 0.25/tick.
        /// NQ/MNQ: (4, 8) = 1pt/2pt at 0.25/tick.
        /// MGC/GC: (10, 20) = 1pt/2pt at 0.1/tick.
        /// Default: (4, 8).
        /// CYC=7: null(1) + MES(2) + ES(3) + NQ(4) + MNQ(5) + MGC(6) + GC(7).
        /// JS-002: returns tuple (never null). ASCII-only strings.
        /// </summary>
        internal static (int t1, int t2) GetQuickTicks(string masterName)
        {
            if (string.IsNullOrEmpty(masterName))
                return (4, 8); // (1) null/empty guard
            if (masterName.StartsWith("MES"))
                return (4, 8); // (2) Micro E-mini S&P 500
            if (masterName.StartsWith("ES"))
                return (4, 8); // (3) B192: explicit E-mini S&P 500
            if (masterName.StartsWith("NQ"))
                return (4, 8); // (4) B192: explicit E-mini Nasdaq 100
            if (masterName.StartsWith("MNQ"))
                return (4, 8); // (5) B192: explicit Micro E-mini Nasdaq 100
            if (masterName.StartsWith("MGC"))
                return (10, 20); // (6) Micro Gold: 1pt/2pt at 0.1/tick
            if (masterName.StartsWith("GC"))
                return (10, 20); // (7) B192: Gold: 1pt/2pt at 0.1/tick
            return (4, 8); // default
        }
```

### Ordering Safety Proof

| Candidate | StartsWith("NQ") | StartsWith("MNQ") | Conclusion |
|-----------|-----------------|-------------------|------------|
| "NQ 06-26" | true | false | Caught at NQ guard ✅ |
| "MNQ 06-26" | false (starts 'M') | true | Passes NQ, caught at MNQ ✅ |
| "MGC 06-26" | false | false | Passes NQ/MNQ, caught at MGC ✅ |
| "GC 06-26" | false | false | Passes MGC (starts 'G' not 'M'), caught at GC ✅ |

All guards are prefix-safe and mutually non-overlapping. Ordering is correct.

### Method Signature

```csharp
internal static (int t1, int t2) GetQuickTicks(string masterName)
```

Signature is **unchanged** from BEFORE. No callers require updates.

### CYC Budget

| | Value |
|---|---|
| CYC before | 3 (null + MES + MGC) |
| CYC after | 7 (null + MES + ES + NQ + MNQ + MGC + GC) |
| JS-066 ceiling | 8 |
| Headroom | 1 branch remaining |

### JS Rule Constraints

| Rule | Requirement | Status |
|------|-------------|--------|
| JS-001 | No `throw new XxxException` | No throw in method |
| JS-002 | No `return null` — returns `(int, int)` tuple | Tuple only, never null |
| JS-021 | No `lock()` | Static method, no shared state |
| JS-033 | No `async void` | Synchronous static method |
| JS-066 | CYC <= 8 | CYC=7 |
| JS-081 | ASCII-only identifiers and string literals | All literals are ASCII instrument prefixes |

### xUnit [Fact] Tests

Test class location: test project, file matching `*PttQuickExit*` or `*InstrumentDefaults*`.
No NT8 objects required. All tests are pure static method calls.

| Test Name | Input | Expected Output | Purpose |
|-----------|-------|-----------------|---------|
| `Fact_GetQuickTicks_GC_ReturnsTen` | `"GC"` | `(10, 20)` | SR-B192-01: bare GC name |
| `Fact_GetQuickTicks_GC_WithExpiryDate_ReturnsTen` | `"GC 06-26"` | `(10, 20)` | SR-B192-01: GC with expiry suffix (prefix match) |
| `Fact_GetQuickTicks_ES_ReturnsFour` | `"ES 06-26"` | `(4, 8)` | SR-B192-02: explicit ES guard |
| `Fact_GetQuickTicks_NQ_ReturnsFour` | `"NQ 06-26"` | `(4, 8)` | SR-B192-02: explicit NQ guard |
| `Fact_GetQuickTicks_MNQ_ReturnsFour` | `"MNQ 06-26"` | `(4, 8)` | SR-B192-02: explicit MNQ guard |
| `Fact_GetQuickTicks_MGC_StillReturnsTen` | `"MGC 06-26"` | `(10, 20)` | Regression: existing MGC behaviour unchanged |
| `Fact_GetQuickTicks_MES_StillReturnsFour` | `"MES 06-26"` | `(4, 8)` | Regression: existing MES behaviour unchanged |
| `Fact_GetQuickTicks_EmptyString_ReturnsFour` | `""` | `(4, 8)` | Edge: empty string guard |
| `Fact_GetQuickTicks_Null_ReturnsFour` | `null` | `(4, 8)` | Edge: null guard |
| `Fact_GetQuickTicks_Unknown_ReturnsFour` | `"ZB 06-26"` | `(4, 8)` | Edge: unknown instrument falls to default |

**Test pattern example** (engineer may adapt to existing test project conventions):

```csharp
[Fact]
public void Fact_GetQuickTicks_GC_ReturnsTen()
{
    var result = InstrumentDefaults.GetQuickTicks("GC");
    Assert.Equal((10, 20), result);
}

[Fact]
public void Fact_GetQuickTicks_GC_WithExpiryDate_ReturnsTen()
{
    var result = InstrumentDefaults.GetQuickTicks("GC 06-26");
    Assert.Equal((10, 20), result);
}

[Fact]
public void Fact_GetQuickTicks_Null_ReturnsFour()
{
    var result = InstrumentDefaults.GetQuickTicks(null);
    Assert.Equal((4, 8), result);
}
```

### 7-Scan Checklist (T1)

Engineer MUST run all 7 scans on the changed lines before issuing VERIFY_PASS.
Expected result: **0 matches** on all scans in all changed lines.

| Scan | Pattern | Expected |
|------|---------|----------|
| SCAN-01 | `lock\s*\(` | 0 matches |
| SCAN-02 | `throw\s+new\s+\w+Exception\(` | 0 matches |
| SCAN-03 | `return\s+null\s*;` | 0 matches |
| SCAN-04 | `DateTime\.Now` (not UtcNow) | 0 matches |
| SCAN-05 | `#[0-9A-Fa-f]{6}` or `Color\.FromRgb\(` | 0 matches |
| SCAN-06 | `FontFamily` | 0 matches |
| SCAN-07 | Non-ASCII characters in string literals or identifiers | 0 matches |

Scope: changed lines in `src/PropTraderTools/Features/PttQuickExit.cs` only.

### Commit Message

```
B192-T1: add GC->10 and explicit ES/NQ/MNQ->4 in InstrumentDefaults

GetQuickTicks: add GC->(10,20) guard (SR-B192-01). GC full-size Gold
shares the 0.1/tick increment of MGC. Add explicit ES/NQ/MNQ->(4,8)
guards (SR-B192-02) for documentation clarity. CYC 3->7 (ceiling=8).
10 xUnit Fact tests added. SCAN-01..07 pass.
```

---

## T2 — CopyEngine.ResetGlobalQuickAll (CopyEngine.cs)

> **IMPLEMENT THIS TICKET BEFORE T3.** T3 (TradeCopierPanel.cs) calls
> `CopyEngine.Instance?.ResetGlobalQuickAll(...)` which will not compile until this method exists.

### Ticket Header

| Field | Value |
|-------|-------|
| Ticket ID | T2 |
| Title | Add ResetGlobalQuickAll method to CopyEngine |
| File | `src/PropTraderTools/CopyEngine.cs` |
| Lane | B, Step 1 |
| Spec Requirements | SR-B192-04 |
| Dependency | None (but T3 depends on T2) |

### Spec Requirements Satisfied

- **SR-B192-04**: `CopyEngine.ResetGlobalQuickAll(int t1)` method must be created.
  It resets `_globalQuickAllT1` to the supplied instrument default and fires
  `GlobalQuickAllBufferChanged` via `Dispatcher.InvokeAsync` to notify any subscribed UI.

### Source Baseline (BEFORE)

File: [`src/PropTraderTools/CopyEngine.cs`](src/PropTraderTools/CopyEngine.cs:437), lines 437-460.

```csharp
// BEFORE (lines 437-460):
        private volatile int _globalQuickAllT1 = 4; // default 4 ticks
        internal int GlobalQuickAllT1 => _globalQuickAllT1;

        internal void IncrementQuickAll()
        {
            if (_globalQuickAllT1 < 99)
                _globalQuickAllT1++;
            int v = _globalQuickAllT1;
            System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                GlobalQuickAllBufferChanged?.Invoke(v)
            ); // HOTFIX-DISPATCH-FIX-01
        }

        internal void DecrementQuickAll()
        {
            if (_globalQuickAllT1 > 1)
                _globalQuickAllT1--;
            int v = _globalQuickAllT1;
            System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                GlobalQuickAllBufferChanged?.Invoke(v)
            ); // HOTFIX-DISPATCH-FIX-01
        }

        internal event Action<int> GlobalQuickAllBufferChanged; // HOTFIX-QUICKALL-SINGLETON-01
```

### Target Implementation (AFTER)

Insert `ResetGlobalQuickAll` between `DecrementQuickAll` and `GlobalQuickAllBufferChanged`
(after the closing brace of `DecrementQuickAll` at line ~458, before the event declaration
at line ~460). Do **not** remove or modify any existing lines.

```csharp
        internal void DecrementQuickAll()
        {
            if (_globalQuickAllT1 > 1)
                _globalQuickAllT1--;
            int v = _globalQuickAllT1;
            System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                GlobalQuickAllBufferChanged?.Invoke(v)
            ); // HOTFIX-DISPATCH-FIX-01
        }

        /// <summary>
        /// ResetGlobalQuickAll: resets GlobalQuickAllT1 to the instrument default.
        /// Called from TradeCopierPanel.SetInstrument when the panel's instrument changes.
        /// Fires GlobalQuickAllBufferChanged via Dispatcher to update subscribed UI labels.
        /// B192-T2: instrument-default sync for Quick ALL / QAll2t buttons.
        /// CYC=1: single straight-line path.
        /// JS-021: no lock. Volatile int write is atomic. Dispatcher marshals event to UI thread.
        /// JS-001: no throw. JS-002: void. JS-033: synchronous void. ASCII-only.
        /// </summary>
        internal void ResetGlobalQuickAll(int t1)
        {
            _globalQuickAllT1 = t1;
            int v = _globalQuickAllT1;
            System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                GlobalQuickAllBufferChanged?.Invoke(v)
            ); // B192: sync singleton to instrument default on instrument change
        }

        internal event Action<int> GlobalQuickAllBufferChanged; // HOTFIX-QUICKALL-SINGLETON-01
```

### Design Notes for Engineer

1. **Atomic write**: `_globalQuickAllT1` is `private volatile int`. A single volatile int
   assignment is atomic on .NET. No lock is required or permitted (JS-021).

2. **Snapshot pattern** (`int v = _globalQuickAllT1`): Captures the value after assignment.
   Prevents a race where another thread calls `IncrementQuickAll` or `DecrementQuickAll`
   between the field write and the lambda execution. This is the identical pattern used at
   lines 444 and 454 (`IncrementQuickAll` and `DecrementQuickAll`).

3. **Dispatcher.InvokeAsync**: Marshals the `GlobalQuickAllBufferChanged` callback to the
   WPF UI thread. Required because the event may update UI elements. This is the established
   pattern annotated `HOTFIX-DISPATCH-FIX-01` in `IncrementQuickAll` and `DecrementQuickAll`.

4. **No bounds guard**: The value of `t1` comes exclusively from
   `InstrumentDefaults.GetQuickTicks`, which returns only validated constants (4 or 10).
   Adding a bounds check (e.g., `t1 > 0 && t1 < 99`) would add CYC without value and
   violate the minimal-change principle.

5. **No new using directives** required. `System.Windows.Application` is already used
   in `IncrementQuickAll` / `DecrementQuickAll` in this file.

### Method Signature (NEW)

```csharp
internal void ResetGlobalQuickAll(int t1)
```

### CYC Budget

| | Value |
|---|---|
| CYC | 1 (single straight-line path, no decision points) |
| JS-066 ceiling | 8 |

### JS Rule Constraints

| Rule | Requirement | Status |
|------|-------------|--------|
| JS-001 | No `throw new XxxException` | No throw |
| JS-002 | void return, no null | void method |
| JS-021 | No `lock()` | volatile int write, no lock |
| JS-023 | UI update via Dispatcher.InvokeAsync | Event dispatch uses InvokeAsync |
| JS-033 | No `async void` | Method signature is synchronous `internal void`; lambda inside InvokeAsync is the pre-existing pattern |
| JS-066 | CYC <= 8 | CYC=1 |
| JS-081 | ASCII-only | Comment and identifier ASCII-only |

### xUnit [Fact] Tests

Test class location: test project, file matching `*CopyEngine*`.
Access `CopyEngine.Instance` (singleton) and `GlobalQuickAllT1` property.

| Test Name | Setup | Action | Assert |
|-----------|-------|--------|--------|
| `Fact_ResetGlobalQuickAll_SetsToFour` | — | `CopyEngine.Instance.ResetGlobalQuickAll(4)` | `CopyEngine.Instance.GlobalQuickAllT1 == 4` |
| `Fact_ResetGlobalQuickAll_SetsToTen` | — | `CopyEngine.Instance.ResetGlobalQuickAll(10)` | `CopyEngine.Instance.GlobalQuickAllT1 == 10` |
| `Fact_ResetGlobalQuickAll_OverwritesPreviousIncrement` | `IncrementQuickAll()` x3 so value becomes 7 | `CopyEngine.Instance.ResetGlobalQuickAll(4)` | `CopyEngine.Instance.GlobalQuickAllT1 == 4` |

**Test pattern example**:

```csharp
[Fact]
public void Fact_ResetGlobalQuickAll_SetsToFour()
{
    CopyEngine.Instance.ResetGlobalQuickAll(4);
    Assert.Equal(4, CopyEngine.Instance.GlobalQuickAllT1);
}

[Fact]
public void Fact_ResetGlobalQuickAll_SetsToTen()
{
    CopyEngine.Instance.ResetGlobalQuickAll(10);
    Assert.Equal(10, CopyEngine.Instance.GlobalQuickAllT1);
}

[Fact]
public void Fact_ResetGlobalQuickAll_OverwritesPreviousIncrement()
{
    // Arrange: increment three times to move value away from default
    CopyEngine.Instance.IncrementQuickAll();
    CopyEngine.Instance.IncrementQuickAll();
    CopyEngine.Instance.IncrementQuickAll();

    // Act: reset to instrument default
    CopyEngine.Instance.ResetGlobalQuickAll(4);

    // Assert: value is back to 4 regardless of prior increments
    Assert.Equal(4, CopyEngine.Instance.GlobalQuickAllT1);
}
```

### 7-Scan Checklist (T2)

Engineer MUST run all 7 scans on the inserted lines before issuing VERIFY_PASS.
Expected result: **0 matches** on all scans in all changed lines.

| Scan | Pattern | Expected |
|------|---------|----------|
| SCAN-01 | `lock\s*\(` | 0 matches |
| SCAN-02 | `throw\s+new\s+\w+Exception\(` | 0 matches |
| SCAN-03 | `return\s+null\s*;` | 0 matches |
| SCAN-04 | `DateTime\.Now` (not UtcNow) | 0 matches |
| SCAN-05 | `#[0-9A-Fa-f]{6}` or `Color\.FromRgb\(` | 0 matches |
| SCAN-06 | `FontFamily` | 0 matches |
| SCAN-07 | Non-ASCII characters in string literals or identifiers | 0 matches |

Scope: inserted lines in `src/PropTraderTools/CopyEngine.cs` only.

---

## T3 — TradeCopierPanel.SetInstrument (TradeCopierPanel.cs)

> **IMPLEMENT AFTER T2.** This ticket calls `CopyEngine.Instance?.ResetGlobalQuickAll(...)`.
> The project will not compile until T2 (`CopyEngine.ResetGlobalQuickAll`) exists.

### Ticket Header

| Field | Value |
|-------|-------|
| Ticket ID | T3 |
| Title | Sync GlobalQuickAllT1 to instrument default in SetInstrument |
| File | `src/PropTraderTools/TradeCopierPanel.cs` |
| Lane | B, Step 2 |
| Spec Requirements | SR-B192-03 |
| Dependency | **T2 must be implemented first** |

### Spec Requirements Satisfied

- **SR-B192-03**: `SetInstrument` must sync `GlobalQuickAllT1` via
  `CopyEngine.Instance?.ResetGlobalQuickAll(ticks.t1)` when the instrument changes.
  Both the per-panel fields `_quickT1`/`_quickT2` AND the global singleton are reset.
  (B192 spec amendment, supersedes B190 note at lines 41621 and 41958.)

### Source Baseline (BEFORE)

File: [`src/PropTraderTools/TradeCopierPanel.cs`](src/PropTraderTools/TradeCopierPanel.cs:501), lines 501-516.

```csharp
// BEFORE (lines 501-516):
        public void SetInstrument(Instrument instrument)
        {
            _instrument = instrument;
            if (_statusText != null && instrument != null)
                _statusText.Text = "Ready: " + instrument.FullName + " -- select followers to copy";

            // B190-T1: reset tick defaults when instrument changes
            var ticks = InstrumentDefaults.GetQuickTicks(
                instrument?.MasterInstrument?.Name ?? "");
            _quickT1 = ticks.t1;
            _quickT2 = ticks.t2;
            if (_quickBtn != null)
                _quickBtn.Content = FormatBuffer("Quick", _quickT1);
            if (_instr2tBtn != null)
                _instr2tBtn.Content = FormatBuffer("Quick2t", _quickT1);
        }
```

### Target Implementation (AFTER)

Add **one line** at the end of `SetInstrument`, inside the closing brace:

```csharp
        public void SetInstrument(Instrument instrument)
        {
            _instrument = instrument;
            if (_statusText != null && instrument != null)
                _statusText.Text = "Ready: " + instrument.FullName + " -- select followers to copy";

            // B190-T1: reset tick defaults when instrument changes
            var ticks = InstrumentDefaults.GetQuickTicks(
                instrument?.MasterInstrument?.Name ?? "");
            _quickT1 = ticks.t1;
            _quickT2 = ticks.t2;
            if (_quickBtn != null)
                _quickBtn.Content = FormatBuffer("Quick", _quickT1);
            if (_instr2tBtn != null)
                _instr2tBtn.Content = FormatBuffer("Quick2t", _quickT1);
            // B192-T3: sync global singleton so Quick ALL uses instrument-correct tick count
            CopyEngine.Instance?.ResetGlobalQuickAll(ticks.t1);
        }
```

The **only change** is the final comment line and `CopyEngine.Instance?.ResetGlobalQuickAll(ticks.t1);`.
No other lines are touched.

### Design Notes for Engineer

1. **Null-conditional operator** (`?.`): If `CopyEngine.Instance` is null (pathological case
   where the engine has not been initialized), the call is a no-op. This matches the defensive
   null-conditional pattern used in `PttGlobalQuickExit.ResolveQuickTicks` (line 289).

2. **Thread safety**: `SetInstrument` runs on the UI thread (it directly assigns
   `_statusText.Text`). Calling `ResetGlobalQuickAll` from the UI thread is safe.
   `ResetGlobalQuickAll` internally dispatches its event callback to the UI thread via
   `Dispatcher.InvokeAsync`.

3. **No new using directives** required. `CopyEngine` is already in scope in
   `TradeCopierPanel.cs`.

4. **CYC unchanged**: `?.` is a null-conditional operator, not a cyclomatic branch under
   the project's CYC counting convention. It is a null-safe dereference equivalent to an
   existing null pattern already present in the method body.

5. **B192 spec amendment**: This call was previously prohibited by B190 spec line 41621.
   The Director's amendment at lines 41621 and 41958 (2026-07-15) explicitly authorises it.
   See `docs/brain/B192/02-architecture-plan.md` Section 0 for full amendment text.

### Method Signature (unchanged)

```csharp
public void SetInstrument(Instrument instrument)
```

### CYC Budget

| | Value |
|---|---|
| CYC before | Unchanged from pre-B192 |
| CYC after | Unchanged (null-conditional `?.` is not a new cyclomatic branch) |
| JS-066 ceiling | 8 |

### Tests — SIM Verification (no xUnit possible)

`SetInstrument` requires a live NT8 `Instrument` object. Pure xUnit testing is not possible
without a running NinjaTrader 8 process. Verify in SIM environment:

| Step | Action | Expected Result |
|------|--------|-----------------|
| SIM-1 | Load PTT AddOn in NT8 SIM environment | AddOn loads without error |
| SIM-2 | Add a panel; set instrument to **MGC** (Micro Gold) | Panel loads |
| SIM-3 | Observe Quick ALL label or click Quick ALL | Label shows "10"; order targets 10-tick profit |
| SIM-4 | Switch instrument to **GC** (full-size Gold) on same or new panel | Panel reloads |
| SIM-5 | Observe Quick ALL label or click Quick ALL | Label shows "10"; order targets 10-tick profit |
| SIM-6 | Switch instrument to **ES** | Panel reloads |
| SIM-7 | Observe Quick ALL label or click Quick ALL | Label shows "4"; order targets 4-tick profit |
| SIM-8 | Switch instrument to **NQ** | Panel reloads |
| SIM-9 | Observe Quick ALL label | Label shows "4" |
| SIM-10 | Switch instrument to **MNQ** | Panel reloads |
| SIM-11 | Observe Quick ALL label | Label shows "4" |

All SIM steps must PASS before `VERIFY_PASS` may be issued for Lane B.

### 7-Scan Checklist (T3)

Engineer MUST run all 7 scans on the changed lines before issuing VERIFY_PASS.
Expected result: **0 matches** on all scans in all changed lines.

| Scan | Pattern | Expected |
|------|---------|----------|
| SCAN-01 | `lock\s*\(` | 0 matches |
| SCAN-02 | `throw\s+new\s+\w+Exception\(` | 0 matches |
| SCAN-03 | `return\s+null\s*;` | 0 matches |
| SCAN-04 | `DateTime\.Now` (not UtcNow) | 0 matches |
| SCAN-05 | `#[0-9A-Fa-f]{6}` or `Color\.FromRgb\(` | 0 matches |
| SCAN-06 | `FontFamily` | 0 matches |
| SCAN-07 | Non-ASCII characters in string literals or identifiers | 0 matches |

Scope: changed lines in `src/PropTraderTools/TradeCopierPanel.cs` only.

### Commit Message

```
B192-T2T3: sync GlobalQuickAllT1 to instrument default on SetInstrument

CopyEngine.ResetGlobalQuickAll(int t1): new method; resets volatile
_globalQuickAllT1 to instrument default, fires GlobalQuickAllBufferChanged
via Dispatcher.InvokeAsync (pattern: HOTFIX-DISPATCH-FIX-01). CYC=1.

TradeCopierPanel.SetInstrument: add
  CopyEngine.Instance?.ResetGlobalQuickAll(ticks.t1)
as final statement. Global Quick ALL singleton now tracks instrument
on panel load. B190 stomp-risk accepted (Director 2026-07-15).

SR-B192-03, SR-B192-04. SCAN-01..07 pass. 3 xUnit Fact tests added.
SIM: MGC/GC Quick ALL fires 10-tick target. ES/NQ fires 4-tick target.
```

---

## Summary Matrix

| Ticket | File | Change | Spec IDs | CYC Before | CYC After | Tests |
|--------|------|--------|----------|-----------|-----------|-------|
| T1 | `PttQuickExit.cs` | Modify `GetQuickTicks`: add ES/NQ/MNQ/GC guards | SR-B192-01, SR-B192-02 | 3 | 7 | 10 xUnit [Fact] |
| T2 | `CopyEngine.cs` | Add `ResetGlobalQuickAll` method | SR-B192-04 | N/A (new) | 1 | 3 xUnit [Fact] |
| T3 | `TradeCopierPanel.cs` | Add 1 line in `SetInstrument` | SR-B192-03 | unchanged | unchanged | SIM only (11 steps) |

**Total files changed**: 3
**Total new methods**: 1 (`ResetGlobalQuickAll`)
**Total methods modified**: 2 (`GetQuickTicks`, `SetInstrument`)
**New xUnit [Fact] tests**: 13 (10 + 3)
**SIM verification steps**: 11

---

## Deferred Backlog

| DW ID | Priority | Status | Notes |
|-------|----------|--------|-------|
| DW-B183-03 | P1 | OPEN (unchanged) | IsLaneEEligible CYC=8 freeze. B192 does NOT touch IsLaneEEligible. No action required. |

No new deferred items introduced by B192.

---

*Written by: ptt-architect*
*Epic: B192*
*Phase: 3 (Ticket Generation)*
*Source plan: docs/brain/B192/02-architecture-plan.md (REVIEW_PASS cycle 2)*
*Status: TICKETS_COMPLETE*
