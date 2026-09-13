# PTT-DEFERRED-0911 Lane G — Engineer Tickets

**Epic:** PTT-DEFERRED-0911
**Lane:** G
**Phase:** 3 (Ticket Generation)
**Status:** TICKETS_COMPLETE
**Author:** ptt-architect
**Date:** 2026-09-12
**Plan source:** docs/brain/PTT-DEFERRED-0911/02-architecture-plan-lane-g.md (REVIEW_PASS Rev 3)

---

## TICKET T-G1

**Title:** Upgrade TradeCopierPanel_BeBufferBox_FieldDoesNotExist to Option A-partial (DW-B26-01)

**Spec requirement IDs:** DW-B26-01

---

### Files to change

| # | File | Change |
|---|------|--------|
| 1 | `src/PropTraderTools/Core/PttContracts.cs` | Add `BreakEven` to `ICopyEngine` interface |
| 2 | `src/PropTraderTools/CopyEngine.cs` | Change `BreakEven(Account,Instrument,int)` from `internal` to `public` |
| 3 | `src/PropTraderTools/TradeCopierPanel.cs` | Add `ExecuteBreakEven` static method; update `DispatchShortcut` Key.B case |
| 4 | `src/PropTraderTools/CopyEngineTests.cs` | Delete old `[Fact(Skip=...)]` test; add `LaneGTests` class |

---

### Method signatures (exact, compile-ready)

#### PttContracts.cs — add to `ICopyEngine` interface (after `RelayCancel`)

```csharp
/// <summary>Dispatch break-even to leader and all follower accounts.</summary>
void BreakEven(NinjaTrader.Cbi.Account leader, NinjaTrader.Cbi.Instrument instrument, int bufferTicks);
```

> **Note for engineer:** Check existing `using` directives in `PttContracts.cs`. If the file
> already contains `using NinjaTrader.Cbi;`, use the short names `Account` and `Instrument`
> in the interface signature instead of the fully-qualified forms above.

#### CopyEngine.cs line 6871 — access modifier change only

```csharp
// BEFORE: internal void BreakEven(Account leader, Instrument instrument, int bufferTicks)
// AFTER:  public  void BreakEven(Account leader, Instrument instrument, int bufferTicks)
// Body unchanged. CYC=5 unchanged.
```

**Single-word change only:** `internal` → `public`. Do not touch any other character on that
line or anywhere in the method body.

#### TradeCopierPanel.cs — new `internal static` method

Add immediately after the closing brace of `DispatchShortcut` (line 3067), before
`BuildRiskAtrRow` (line 3069):

```csharp
// T-G1: ExecuteBreakEven -- testable BE dispatch seam (internal static, InternalsVisibleTo granted).
// CYC=1: straight-line call. No NT8 UI deps. JS-001: no throw. JS-021: no lock.
internal static void ExecuteBreakEven(
    ICopyEngine engine, NinjaTrader.Cbi.Account leader, NinjaTrader.Cbi.Instrument instrument, int beBuffer)
{
    engine.BreakEven(leader, instrument, beBuffer);
}
```

> **Note for engineer:** `ExecuteBreakEven` must use the `ICopyEngine` type from the
> `PttContracts` namespace. Confirm the correct `using` directive is present in
> `TradeCopierPanel.cs` (it should already exist from prior lanes).
> `InternalsVisibleTo` is already granted at `CopyEngine.cs:46` — no new attribute needed.

#### TradeCopierPanel.cs `DispatchShortcut` Key.B case change

```csharp
// BEFORE:
case Key.B:
    _engine.BreakEven(_leaderAccount, _instrument, _beBuffer);
    break;

// AFTER:
case Key.B:
    ExecuteBreakEven(_engine, _leaderAccount, _instrument, _beBuffer);
    break;
```

**Compile note:** `_engine` is typed `CopyEngine`. `CopyEngine : ICopyEngine`. The implicit
assignment to the `ICopyEngine` parameter compiles without a cast.

---

### CopyEngineTests.cs changes

#### Step 1 — DELETE the old `[Fact(Skip=...)]` test

Delete lines 7735–7747 (the `TradeCopierPanel_BeBufferBox_FieldDoesNotExist` test and its
leading comment). These lines are the Option B reflection-based absence assertion that Lane G
replaces. Exact lines must be confirmed in the current file before deletion.

#### Step 2 — ADD `LaneGTests` class

Add the following class **after** the `LaneFTests` closing brace (currently line 9008) and
**before** the final namespace closing brace (currently line 9009):

```csharp
public class LaneGTests
{
    // StubEngine: hand-rolled ICopyEngine mock for ExecuteBreakEven test.
    // Captures last BreakEven call args. No NT8 runtime dependencies.
    // JS-021: no lock. JS-001: no throw. ASCII-only.
    private sealed class StubEngine : ICopyEngine
    {
        public int BreakEvenCallCount;
        public NinjaTrader.Cbi.Account LastLeader;
        public NinjaTrader.Cbi.Instrument LastInstrument;
        public int LastBuffer;
        public void BreakEven(NinjaTrader.Cbi.Account leader, NinjaTrader.Cbi.Instrument instrument, int bufferTicks)
        { BreakEvenCallCount++; LastLeader = leader; LastInstrument = instrument; LastBuffer = bufferTicks; }
        public void RelayBe(BeEventArgs e)         { }
        public void RelayTrim(TrimEventArgs e)     { }
        public void RelayFlatten(FlatEventArgs e)  { }
        public void RelayCancel(CancelEventArgs e) { }
    }

    // T-G1 (DW-B26-01): ExecuteBreakEven passes _beBuffer value to ICopyEngine.BreakEven.
    // Option A-partial: static seam called directly from test, no WPF/NT8 instantiation.
    // Replaces TradeCopierPanel_BeBufferBox_FieldDoesNotExist [Fact(Skip=...)] (deleted).
    // No Skip: no NT8 cctor triggered. CYC=1 (straight-line).
    [Fact]
    public void TradeCopierPanel_ExecuteBreakEven_PassesBufferToEngine()
    {
        var stub = new StubEngine();
        TradeCopierPanel.ExecuteBreakEven(stub, null, null, 3);
        Assert.Equal(1, stub.BreakEvenCallCount);
        Assert.Equal(3, stub.LastBuffer);
    }
}
```

---

### xUnit [Fact] names

| Test name | Kind | Skip? |
|-----------|------|-------|
| `TradeCopierPanel_ExecuteBreakEven_PassesBufferToEngine` | `[Fact]` | None — runs without NT8 |

---

### Test count delta

| State | Passed | Skipped | Failed | Total |
|-------|--------|---------|--------|-------|
| Baseline (Lane F final) | 178 | 364 | 0 | 542 |
| Delete old `[Fact(Skip=...)]` | 178 | 363 | 0 | 541 |
| Add new `[Fact]` passing | **179** | **363** | **0** | **542** |
| **Lane G expected** | **179** | **363** | **0** | **542** |

---

### CYC analysis

| Method | CYC | Notes |
|--------|-----|-------|
| `ExecuteBreakEven` | 1 | Straight-line; no branches |
| `DispatchShortcut` | 5 | Unchanged — Key.B body replaced with 1-line call, branch count unaffected |
| `CopyEngine.BreakEven` | 5 | Body unchanged; access modifier only |
| `StubEngine.BreakEven` | 1 | Records call; no branches |
| `TradeCopierPanel_ExecuteBreakEven_PassesBufferToEngine` | 1 | Straight-line test |

All methods: CYC ≤ 8. **JS-013 PASS.**

---

### Jane Street constraints

| Rule | Severity | Applies to | Requirement |
|------|----------|------------|-------------|
| JS-021 | P0 | `ExecuteBreakEven`, `StubEngine`, all changed code | No `lock()` anywhere |
| JS-001 | P0 | `ExecuteBreakEven` | No `throw` (CYC=1, no error paths) |
| JS-013 | P1 | All new/changed methods | CYC ≤ 8 (max=5 for `CopyEngine.BreakEven`) |

---

### 7-scan checklist (engineer contract)

| Scan | Command / Check | Required result |
|------|-----------------|-----------------|
| SCAN-01 | `grep -r "lock(" src/` | 0 matches (no new `lock()` added) |
| SCAN-02 | CYC audit of all new/changed methods | All ≤ 8: max = 5 (`CopyEngine.BreakEven`) |
| SCAN-03 | `grep -r "async\|await" src/PropTraderTools/*.cs` | 0 new `async` in lifecycle handlers |
| SCAN-04 | Non-ASCII byte scan of all changed files | 0 non-ASCII characters in any new code |
| SCAN-05 | No ATM API usage | No `AtmStrategyCreate` / `AtmStrategyChangeStopTarget` in new code |
| SCAN-06 | `dotnet build` + `dotnet test` | Build: 0 errors; Test: **179 passed / 363 skipped / 542 total**; new `[Fact]` PASSES (not Skip) |
| SCAN-07 | Structural | `ExecuteBreakEven` static seam exists; old `[Fact(Skip=...)]` deleted; new `[Fact]` passes; DW-B26-01 CLOSED |

---

### Notes for engineer

1. **CopyEngineTests.cs delete target:** Lines 7735–7747 contain the
   `TradeCopierPanel_BeBufferBox_FieldDoesNotExist` `[Fact(Skip=...)]` test and its leading
   comment. Confirm exact line numbers before deleting (the file may have shifted since
   architecture was written). Delete that block in full — nothing else.

2. **CopyEngineTests.cs add location:** Add `LaneGTests` class after the closing brace of
   `LaneFTests` (currently line 9008) and before the final namespace closing brace (currently
   line 9009). Confirm current line numbers in the file before inserting.

3. **PttContracts.cs using directives:** If `using NinjaTrader.Cbi;` is already present, use
   the short names `Account` and `Instrument` in the interface signature. Use fully-qualified
   names only if the directive is absent.

4. **CopyEngine.cs line 6871:** Single-word change `internal` → `public`. Do not change
   anything else on that line or in the method body.

5. **TradeCopierPanel.cs using directive:** `ExecuteBreakEven` takes `ICopyEngine` — confirm
   the `using` directive for `PttContracts` namespace is already present (it should be from
   prior lanes). Do not add a duplicate.

6. **InternalsVisibleTo:** Already granted at `CopyEngine.cs:46`. No new assembly attribute
   needed for `internal static ExecuteBreakEven` to be visible in `CopyEngineTests.cs`.

7. **StubEngine `BreakEvenCallCount` field:** The `[Fact]` asserts both
   `Assert.Equal(1, stub.BreakEvenCallCount)` and `Assert.Equal(3, stub.LastBuffer)`.
   Ensure the `StubEngine.BreakEven` implementation increments `BreakEvenCallCount` on
   every call (as shown in the class body above).

---

*ptt-architect · PTT-DEFERRED-0911 Lane G · Phase 3 · 2026-09-12*
