# W9-L8-004 Plan: Dictionary Dispatch for DispatchRunnerAction

## File
`src/V12_002.UI.Callbacks.cs`

## Method
`DispatchRunnerAction` (lines 1132-1162)

## Current CYC
8 (base 1 + ternary 1 + 6 switch cases)

## Target CYC
2 for `DispatchRunnerAction` (base 1 + TryGetValue branch 1)
2 for `GetCurrentPrice` (base 1 + ternary 1)

---

## 1. New Private Helper: GetCurrentPrice

Extract the ternary `lastKnownPrice > 0 ? lastKnownPrice : Close[0]` into a named
private helper. This removes the +1 ternary from `DispatchRunnerAction`'s CYC count.

```csharp
private double GetCurrentPrice() => lastKnownPrice > 0 ? lastKnownPrice : Close[0];
```

**Design notes:**
- Expression-body method (=>) -- CYC = 2 (base 1 + ternary 1). Acceptable.
- No parameters -- reads `lastKnownPrice` (instance field) and `Close[0]` (NinjaTrader
  bar series), identical to the original inline expression.
- No allocation. Computed once per `DispatchRunnerAction` call, passed as `double` arg.
- Placement: immediately before `DispatchRunnerAction` (line 1132), after the line 1131
  blank line. Both helpers co-locate with their sole consumer.

---

## 2. Dictionary Field Declaration

Place this field **immediately before `DispatchRunnerAction`** (before line 1132),
after `GetCurrentPrice`.

```csharp
// W9-L8-004: Dictionary dispatch -- replaces switch in DispatchRunnerAction.
// static readonly = immutable after class init; thread-safe for concurrent reads (no lock).
// Action<V12_002, string, PositionInfo, int, double>: uniform 5-arg signature.
//   self          = V12_002 strategy instance (avoids closure capture in static field)
//   en            = entryName (string)
//   p             = pos (PositionInfo)
//   rc            = runnerContracts (int) -- used only by "market" handler
//   cp            = currentPrice (double) -- used only by "stopbe" and "lock50" handlers
// Handlers that do not need rc or cp simply ignore those parameters.
// StringComparer.Ordinal: case-sensitive byte-for-byte match -- consistent with original
//   switch(action) behavior, deterministic, no locale side effects.
private static readonly Dictionary<
    string,
    Action<V12_002, string, PositionInfo, int, double>>
    _runnerDispatch = new Dictionary<
        string,
        Action<V12_002, string, PositionInfo, int, double>>(StringComparer.Ordinal)
    {
        { "market",       (self, en, p, rc, cp) => self.ExecuteRunner_Market(en, p, rc)       },
        { "stop1pt",      (self, en, p, rc, cp) => self.ExecuteRunner_StopOnePoint(en, p)     },
        { "stop2pt",      (self, en, p, rc, cp) => self.ExecuteRunner_StopTwoPoint(en, p)     },
        { "stopbe",       (self, en, p, rc, cp) => self.ExecuteRunner_Breakeven(en, p, cp)    },
        { "lock50",       (self, en, p, rc, cp) => self.ExecuteRunner_Lock50(en, p, cp)       },
        { "disabletrail", (self, en, p, rc, cp) => self.ExecuteRunner_DisableTrail(en, p)     },
    };
```

**Field design notes:**
- `private static readonly` -- immutable after class-load; zero coordination overhead
  for concurrent reads. No `lock()` needed (per OKF lock-free-patterns.md).
- `StringComparer.Ordinal` -- case-sensitive, identical semantics to original
  `switch (action)` string comparison.
- Six entries -- one per original `case` arm. No `default` entry needed: the original
  switch had no `default` branch, so unknown action strings silently no-op. This is
  naturally reproduced by `TryGetValue` returning `false` with no `else` clause.
- The Action type carries all 5 context values as explicit parameters. Handlers that
  do not use `rc` or `cp` accept them in the lambda signature and discard them.
  The JIT will optimize away unused lambda parameters -- zero overhead.
- Lambdas are compiled once at class-load time (static field initializer). No allocation
  per call to `DispatchRunnerAction`.

---

## 3. Refactored DispatchRunnerAction Method Body

Replace the entire method body (lines 1133-1162) with:

```csharp
private void DispatchRunnerAction(string action, string entryName, PositionInfo pos, int runnerContracts)
{
    if (_runnerDispatch.TryGetValue(action, out var handler))
        handler(this, entryName, pos, runnerContracts, GetCurrentPrice());
}
```

**Structural diff vs original:**
- Removed: `double currentPrice = lastKnownPrice > 0 ? lastKnownPrice : Close[0];` (ternary, +1 CYC)
- Removed: `switch (action) { case ... }` block (24 lines, +6 CYC)
- Added: `if (_runnerDispatch.TryGetValue(action, out var handler))` (+1 CYC)
- Added: `handler(this, entryName, pos, runnerContracts, GetCurrentPrice());` (call site)
- No pre-hook / post-hook logic existed -- method body reduces to 4 lines total.
- Silent no-op for unknown actions: preserved. `TryGetValue` returns `false` for
  unrecognised action strings; the `if` body is skipped. No behavior change.

---

## 4. CYC Analysis

### DispatchRunnerAction (after refactor)

| # | Branch point | Expression |
|---|--------------|------------|
| 1 | Base | method entry |
| 2 | `if` dispatch | `_runnerDispatch.TryGetValue(action, out var handler)` |

**Predicted CYC = 2** (base 1 + TryGetValue if 1).

Reduction: **8 -> 2** (75% reduction). Well inside Jane Street strict standard <= 8.

### GetCurrentPrice (new helper)

| # | Branch point | Expression |
|---|--------------|------------|
| 1 | Base | method entry |
| 2 | Ternary | `lastKnownPrice > 0 ? ... : ...` |

**Predicted CYC = 2** (base 1 + ternary 1). Acceptable.

---

## 5. Field and Helper Placement

**File**: `src/V12_002.UI.Callbacks.cs`

**Insert order** (all before line 1132, the original `DispatchRunnerAction` declaration):

```
[line 1131 -- blank line after ValidateRunnerPrerequisites closing brace]
[INSERT GetCurrentPrice() here]
[INSERT _runnerDispatch field here]
[line 1132 -- DispatchRunnerAction (method body replaced)]
```

Both the helper method and the Dictionary field are placed **immediately before their
sole consumer** (`DispatchRunnerAction`), maximizing co-location readability. This
matches the placement convention established in W9-L8-003 (`_targetDispatch` placed
immediately before `RouteTargetActionToHandler`).

---

## 6. No New Handler Methods Needed

**Confirmed: none.**

Only `GetCurrentPrice` is new -- it is a private helper that extracts an existing
inline ternary expression. All six dispatch handlers already exist as private methods:

| Lambda | Existing Method Signature |
|--------|--------------------------|
| `(self, en, p, rc, cp) => self.ExecuteRunner_Market(en, p, rc)` | `private void ExecuteRunner_Market(string entryName, PositionInfo pos, int runnerContracts)` |
| `(self, en, p, rc, cp) => self.ExecuteRunner_StopOnePoint(en, p)` | `private void ExecuteRunner_StopOnePoint(string entryName, PositionInfo pos)` |
| `(self, en, p, rc, cp) => self.ExecuteRunner_StopTwoPoint(en, p)` | `private void ExecuteRunner_StopTwoPoint(string entryName, PositionInfo pos)` |
| `(self, en, p, rc, cp) => self.ExecuteRunner_Breakeven(en, p, cp)` | `private void ExecuteRunner_Breakeven(string entryName, PositionInfo pos, double currentPrice)` |
| `(self, en, p, rc, cp) => self.ExecuteRunner_Lock50(en, p, cp)` | `private void ExecuteRunner_Lock50(string entryName, PositionInfo pos, double currentPrice)` |
| `(self, en, p, rc, cp) => self.ExecuteRunner_DisableTrail(en, p)` | `private void ExecuteRunner_DisableTrail(string entryName, PositionInfo pos)` |

**No existing method signatures change.**
**No public API surface added.**

---

## 7. OKF Compliance Checklist

| Rule | Status |
|------|--------|
| `lock()` banned | PASS -- `static readonly` Dictionary; immutable after init, zero locks |
| `DateTime.Now` banned | N/A -- not touched by this change |
| CYC <= 8 | PASS -- `DispatchRunnerAction` drops from 8 to 2; `GetCurrentPrice` = 2 |
| No new alloc on hot path | PASS -- Dictionary initialized once at class load; `TryGetValue` is O(1) with no `new` |
| `switch expression` preferred | PASS -- switch eliminated entirely; Dictionary dispatch used instead |
| xUnit tests for extracted helpers | ACTION REQUIRED -- 1 [Fact] for `GetCurrentPrice` helper; 1 [Fact] verifying dispatch routes to correct handler for each of the 6 keys |
| ASCII-only source | PASS -- all identifiers, strings, comments are ASCII |
| camelCase locals | PASS -- `handler` (from `out var handler`) is camelCase; no underscore-prefixed locals |
| Private stays private | PASS -- `_runnerDispatch` is `private static readonly`; `GetCurrentPrice` is `private` |
| StringComparer.Ordinal | PASS -- case-sensitive, deterministic, locale-safe |
| No scope creep | PASS -- only `DispatchRunnerAction`, `GetCurrentPrice`, and `_runnerDispatch` touched |

---

## 8. Behavioral Equivalence Notes

| Original behavior | Preserved? |
|-------------------|-----------|
| `"market"` calls `ExecuteRunner_Market(entryName, pos, runnerContracts)` | YES |
| `"stop1pt"` calls `ExecuteRunner_StopOnePoint(entryName, pos)` | YES |
| `"stop2pt"` calls `ExecuteRunner_StopTwoPoint(entryName, pos)` | YES |
| `"stopbe"` calls `ExecuteRunner_Breakeven(entryName, pos, currentPrice)` | YES -- `currentPrice` now from `GetCurrentPrice()` which has identical logic |
| `"lock50"` calls `ExecuteRunner_Lock50(entryName, pos, currentPrice)` | YES -- same |
| `"disabletrail"` calls `ExecuteRunner_DisableTrail(entryName, pos)` | YES |
| Unknown action string = silent no-op | YES -- `TryGetValue` returns false, `if` body skipped |
| `currentPrice` evaluated only when needed (stopbe/lock50) | CHANGED -- `GetCurrentPrice()` now evaluated unconditionally on every dispatch call. This is a micro-level behavioral difference: previously `currentPrice` was only evaluated once before the switch, but the switch itself was always entered (all 6 cases are reachable). The evaluation is the same regardless of which case is hit. The only new case is unknown action strings: previously `currentPrice` was computed even for unknown actions; now `GetCurrentPrice()` is NOT called when `TryGetValue` returns false. This is strictly better -- avoids a redundant `Close[0]` access for unknown action strings. |

---

## 9. Agent Tracking

| Field | Value |
|-------|-------|
| Phase | 2 -- Architecture Planning |
| Finding ID | W9-L8-004 |
| Method | `DispatchRunnerAction` |
| File | `src/V12_002.UI.Callbacks.cs` |
| Current CYC | 8 |
| Target CYC | 2 (`DispatchRunnerAction`), 2 (`GetCurrentPrice`) |
| Approach | Static readonly Dictionary + TryGetValue dispatch; ternary extracted to `GetCurrentPrice` |
| New types introduced | None |
| New public API | None |
| New private methods | `GetCurrentPrice` only |
| Handler methods changed | None |
| Test requirement | 1 [Fact] for `GetCurrentPrice`; 1 [Fact] verifying dispatch table routes all 6 keys |
