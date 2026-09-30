# PTT-COPIER-B20-LANE-A — Ticket 2 Completion
# Phase 4a output (ptt-engineer)
# Ticket: T2 — Copy ON/OFF State Event (DW-B17-SYNC-01)
# Status: BUILD_PASS
# Date: 2026-07-14
# Engineer: ptt-engineer

---

## Summary

Implemented DW-B17-SYNC-01: added `public event Action<bool> CopyEnabledChanged` to
`CopyEngine` and wired it in `SetEnabled`. Added one xUnit `[Fact]` test that asserts
both boolean states are delivered. [Fact] count advanced 119 → 120.

---

## Files Modified

| File | Workspace | Changes |
|------|-----------|---------|
| `src/PropTraderTools/CopyEngine.cs` | `c:\WSGTA\universal-or-strategy` | CHANGE A (event field) + CHANGE B (invoke site) |
| `src/PropTraderTools/CopyEngineTests.cs` | `c:\WSGTA\universal-or-strategy` | Added `SetEnabled_FiresCopyEnabledChanged` |

Files NOT touched: `TradeCopierPanel.cs`, `TradeCopierWindow.cs`, `TradeCopierAddOn.cs`

---

## CHANGE A — Event Field (CopyEngine.cs, after PendingBeFired)

**Insertion point**: After line 125 (`internal event Action<string> PendingBeFired;`)
**New lines inserted at line 127–130**:

```csharp
// B20-LANE-A T2: Copy ON/OFF sync event (DW-B17-SYNC-01)
// Plain delegate field -- NOT lock-guarded (JS-021). Fired from SetEnabled on every toggle.
// Lane C wires TradeCopierPanel and TradeCopierWindow subscribers.
public event Action<bool> CopyEnabledChanged;
```

**Resulting line in file**: Line 130 — `public event Action<bool> CopyEnabledChanged;`

---

## CHANGE B — Invoke Site (CopyEngine.cs, in SetEnabled)

**Insertion point**: After `StatusUpdate?.Invoke("Copy " + (enabled ? "ON" : "OFF"));` in `SetEnabled`
**New line inserted at line 240**:

```csharp
CopyEnabledChanged?.Invoke(enabled);
```

**Resulting SetEnabled body (lines 236–241)**:
```csharp
internal void SetEnabled(bool enabled)
{
    _isCopyEnabled = enabled;
    StatusUpdate?.Invoke("Copy " + (enabled ? "ON" : "OFF"));
    CopyEnabledChanged?.Invoke(enabled);
}
```

---

## Test Added — SetEnabled_FiresCopyEnabledChanged (CopyEngineTests.cs)

**Insertion point**: Before closing `}` of test class (previously line 2070), now at line 2075
**Method**:

```csharp
// ===================================================================
// B20-LANE-A T2: SetEnabled fires CopyEnabledChanged event
// ===================================================================

[Fact]
public void SetEnabled_FiresCopyEnabledChanged()
{
    _engine.SetEnabled(false);
    bool? received = null;
    Action<bool> handler = v => received = v;
    _engine.CopyEnabledChanged += handler;
    try
    {
        _engine.SetEnabled(true);
        Assert.Equal(true, received);
        _engine.SetEnabled(false);
        Assert.Equal(false, received);
    }
    finally
    {
        _engine.CopyEnabledChanged -= handler;
    }
}
```

**[Fact] count**: 119 → **120**

---

## Layer 2 Scan Results (7 Scans — Engineer Contract)

| # | Scan | Command | Expected | Actual | Result |
|---|------|---------|----------|--------|--------|
| SCAN-1 | Event declaration present | `Select-String CopyEngine.cs -Pattern "public event Action<bool> CopyEnabledChanged"` | 1 match | 1 match (line 130) | ✅ PASS |
| SCAN-2 | Invoke site present | `Select-String CopyEngine.cs -Pattern "CopyEnabledChanged\?\.Invoke\(enabled\)"` | 1 match | 1 match (line 240) | ✅ PASS |
| SCAN-3 | Test method present | `Select-String CopyEngineTests.cs -Pattern "SetEnabled_FiresCopyEnabledChanged"` | 1 match | 1 match (line 2075) | ✅ PASS |
| SCAN-4 | [Fact] count | `(Select-String CopyEngineTests.cs -Pattern "\[Fact\]").Count` | 120 | **120** | ✅ PASS |
| SCAN-5 | No live lock() | `Select-String CopyEngine.cs -Pattern "lock\s*\("` (non-comment lines) | 0 matches | 0 matches | ✅ PASS |
| SCAN-6 | No async void | `Get-ChildItem *.cs \| Select-String -Pattern "async void "` | 0 matches | 0 matches | ✅ PASS |
| SCAN-7 | Build — 0 new errors | `dotnet build PropTraderTools.csproj 2>&1 \| Select-Object -Last 15` | 0 new errors (3 pre-existing NT8 infra errors acceptable) | 3 pre-existing errors (AtrSizingEngine NT8 assembly ×2, CopyEngine.cs:634 nullable C# 7.3 ×1) — 0 new errors | ✅ PASS |

**All 7 scans: PASS**

---

## Pre-Existing Build Errors (Not Introduced by T2)

| Error | File | Line | Root Cause | New? |
|-------|------|------|------------|------|
| CS0234: `NinjaTrader.NinjaScript.Indicators` not found | `AtrSizingEngine.cs` | 20 | NT8 SDK assembly unavailable in `dotnet build` context | NO (pre-existing) |
| CS0246: `Indicator` type not found | `AtrSizingEngine.cs` | 24 | NT8 SDK assembly unavailable in `dotnet build` context | NO (pre-existing) |
| CS8370: Nullable reference types not available in C# 7.3 | `CopyEngine.cs` | 634 | Pre-existing line, C# 7.3 constraint | NO (pre-existing) |

T2 changes are at lines 127–130 and 240. Error at line 634 is entirely unrelated to T2.

---

## CYC Analysis

| Method | CYC Before T2 | CYC After T2 | Delta | Within Limit (<=8)? |
|--------|--------------|-------------|-------|---------------------|
| `SetEnabled` | 1 | 1 | 0 | YES |

`CopyEnabledChanged?.Invoke(enabled)` is a null-conditional expression statement.
The C# compiler atomically snapshots the delegate before the null check — no control-flow
branch is introduced. CYC remains 1.

---

## JS P0 Compliance

| Rule | Check | Result |
|------|-------|--------|
| JS-021 | No `lock()` added | PASS — `?.Invoke` null-conditional is thread-safe; no lock needed or permitted |
| JS-002 | No `return null` | PASS — `SetEnabled` returns void |
| JS-001 | No `throw new XxxException` in hot paths | PASS — no throw added |
| JS-033 | No `async void` non-event-handlers | PASS — no async modifier |
| JS-015 | No unvalidated primitives crossing API boundary | PASS — `bool enabled` is an existing parameter |

---

## NT8 Compiler Compliance

| Rule | Check | Result |
|------|-------|--------|
| NT8-001 | No `{ get; init; }` | PASS — event field declaration, not a property |
| NT8-002 | No `abstract record` / `sealed record` | PASS |
| NT8-003 | No `volatile double` / `volatile long` | PASS |
| NT8-004 | No `ImmutableDictionary` | PASS |
| NT8-007 | No `CreateOrder` call | PASS |
| NT8-031 | `Math.Clamp` not used | PASS |
| `event Action<bool>` syntax | Standard C# delegate event field | PASS — .NET 4.8 / C# 7.x compatible |
| `DateTime.Now` | Not used | PASS |
| Non-ASCII characters | None in new code | PASS |

---

## Thread-Safety Note

`CopyEnabledChanged?.Invoke(enabled)` follows the canonical C# thread-safe delegate invocation
pattern. The `?.` null-conditional operator causes the compiler to capture the delegate reference
atomically before the null check, preventing a TOCTOU race between a concurrent
`-= handler` and the null check. No lock() is required or permitted (JS-021).

---

## Singleton Teardown

The test uses a `try/finally` block to unconditionally unsubscribe the handler:
```csharp
finally { _engine.CopyEnabledChanged -= handler; }
```
`CopyEngine.Instance` persists across the test suite. Without teardown, the lambda would
accumulate subscribers across test runs, producing false positives in future tests.

---

## Return

**BUILD_PASS**
