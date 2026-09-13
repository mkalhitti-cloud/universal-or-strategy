# PTT-DEFERRED-0911 Lane G — Architecture Plan (Revision 3)

**Epic:** PTT-DEFERRED-0911
**Lane:** G
**Phase:** 1 (Architecture)
**Status:** REVIEW_PASS (Revision 3)
**Author:** ptt-architect
**Date:** 2026-09-12

---

## Revision Log

| Rev | Verdict | Violation | Resolution |
|-----|---------|-----------|------------|
| 1 | REVIEW_FAIL | V-001: ICopyEngine did not include BreakEven; plan omitted the interface addition | Added P1: `void BreakEven(Account, Instrument, int)` to ICopyEngine |
| 2 | REVIEW_FAIL | V-002: Plan Section 4 P2 changed `_engine` field type from `CopyEngine` to `ICopyEngine` — would produce ~20 compile errors (Subscribe, Unsubscribe, LoadRules, ArmPendingBe, SetEnabled, Trim, Flatten, CancelPendingEntries, event subscriptions, etc. are NOT on ICopyEngine) | DROP P2 entirely. DROP P3 (injection constructor). `_engine` field type STAYS `CopyEngine`. Full testability achieved via P4 `ExecuteBreakEven` static helper which takes `ICopyEngine` parameter. |
| 3 | REVIEW_FAIL | V-003: LANE-SPLIT GATE section missing. V-004: Old [Fact(Skip=...)] test disposition ambiguous; Section 10 baseline wrong. V-005: CopyEngine.BreakEven line number and CYC incorrect. | Restored LANE-SPLIT GATE (Section 0). Deleted old Skip test explicitly; corrected Section 10 counts (baseline: 178p/364s/542t → after: 179p/363s/542t). Corrected BreakEven line to 6871 and CYC to 5. |

---

## 0. Lane-Split Gate

**Q1 — Same method or within 50 lines?**
This mission involves ONE fix: upgrade one test method (`TradeCopierPanel_BeBufferBox_FieldDoesNotExist`).
All changes touch a single behavioral area (Break-Even dispatch + its test).
Q1 answer: **YES** (one test method, one static helper, one access-modifier change — all tightly coupled within the same dispatch path).
→ **SINGLE-PIPELINE. STOP.**

**LANE-SPLIT GATE RESULT: SINGLE-PIPELINE**
One ticket (T-G1). No lane split required.

---

## 1. Objective

Close deferred item **DW-B26-01** (Option B → Option A test upgrade): replace the
reflection-based absence assertion `TradeCopierPanel_BeBufferBox_FieldDoesNotExist`
with a real behavioral test that exercises the Break-Even dispatch path by passing a stub.

This is achieved WITHOUT changing the `_engine` field type in `TradeCopierPanel`:

- `ExecuteBreakEven` is a static helper with `ICopyEngine` as its first parameter.
- Tests pass a stub that implements only ICopyEngine (5 relay methods + BreakEven).
- `DispatchShortcut` Key.B delegates to `ExecuteBreakEven(_engine, ...)`.
- `_engine` (typed `CopyEngine`) satisfies `ICopyEngine` parameter implicitly — no cast, no new field.

---

## 2. Deferred Item Addressed

| ID | Item | Status |
|----|------|--------|
| DW-B26-01 | Reflection test upgrade (Option B → Option A): `TradeCopierPanel_BeBufferBox_FieldDoesNotExist` reflection-based absence assertion. Upgrade when NT8 WPF test host available. | **CLOSED by Lane G** |

---

## 3. Files Changed (Final List)

| # | File | Change |
|---|------|--------|
| 1 | `src/PropTraderTools/Core/PttContracts.cs` | Add `void BreakEven(Account, Instrument, int)` to `ICopyEngine` |
| 2 | `src/PropTraderTools/CopyEngine.cs` | `BreakEven(Account, Instrument, int)`: `internal` → `public` |
| 3 | `src/PropTraderTools/TradeCopierPanel.cs` | Add `ExecuteBreakEven` static method; update `DispatchShortcut` Key.B case |
| 4 | `src/PropTraderTools/CopyEngineTests.cs` | Add `LaneGTests` class with `StubEngine` + 1 `[Fact]` test |

---

## 4. Component Plan

### P1 — ICopyEngine: Add BreakEven

**File:** `src/PropTraderTools/Core/PttContracts.cs`

**Change:** Add one method signature to the `ICopyEngine` interface body (after `RelayCancel`):

```csharp
/// <summary>Move stop to break-even for leader + all follower accounts.</summary>
void BreakEven(Account leader, Instrument instrument, int beBuffer);
```

**Current ICopyEngine body (lines 79–92):**
```csharp
public interface ICopyEngine
{
    void RelayBe(BeEventArgs e);
    void RelayTrim(TrimEventArgs e);
    void RelayFlatten(FlatEventArgs e);
    void RelayCancel(CancelEventArgs e);
}
```

**After Lane G:**
```csharp
public interface ICopyEngine
{
    void RelayBe(BeEventArgs e);
    void RelayTrim(TrimEventArgs e);
    void RelayFlatten(FlatEventArgs e);
    void RelayCancel(CancelEventArgs e);
    /// <summary>Move stop to break-even for leader + all follower accounts.</summary>
    void BreakEven(Account leader, Instrument instrument, int beBuffer);
}
```

**Why no compile errors:** The only production implementor of `ICopyEngine` is `CopyEngine`
(confirmed by codebase search — `MockCopyEngineRelay` exists only in comments, not as a class).
`CopyEngine` already has `BreakEven(Account, Instrument, int)` at line 6871. After the
access-modifier change in P2, that method is `public`, satisfying the new interface member.

**CYC:** N/A (interface declaration).  
**JS rules:** Interface members are void — no LINQ, no async, no init accessors (NT8 Roslyn constraint met).

---

### P2 — CopyEngine: Access Modifier Change

**File:** `src/PropTraderTools/CopyEngine.cs`

**Change:** Change `BreakEven(Account, Instrument, int)` at line 6871 from `internal` to `public`.

**Before:**
```csharp
internal void BreakEven(Account leader, Instrument instrument, int bufferTicks)
```

**After:**
```csharp
public void BreakEven(Account leader, Instrument instrument, int bufferTicks)
```

**Method body is unchanged:**
```csharp
// B24 T1 -- BreakEven(Account,Instrument,int): fires leader directly, no rule needed.
// CYC=5: _flags.BreakEven guard(1), null guard(2), MoveStop leader(no branch), foreach acc(3), acc==leader skip(4), MoveStop follower(5).
// JS-021: no lock. JS-002: null leader fires StatusUpdate + early return.
public void BreakEven(Account leader, Instrument instrument, int bufferTicks)
{
    if (!_flags.BreakEven) return;
    if (leader == null)
    {
        StatusUpdate?.Invoke("PTT-BE: leader null -- BE skipped");
        return;
    }
    MoveStopToBreakEven(leader, instrument, bufferTicks);
    foreach (var acc in AllAccounts(instrument))
    {
        if (acc == leader) continue;
        MoveStopToBreakEven(acc, instrument, bufferTicks);
    }
}
```

**CYC:** 5 (`_flags.BreakEven` guard + null guard + foreach + skip-leader + MoveStop leader = 5).
**JS rules:** JS-021 no lock (unchanged). JS-002 null return (unchanged).

> **NOTE:** `CopyEngine` is `internal sealed` — the `public` modifier on `BreakEven` is
> visible within the assembly only (NT8 AddOn assembly boundary). This does not expose
> the method to external callers. It satisfies the `ICopyEngine` interface contract.

---

### P3 (DROPPED) — No _engine Field Type Change

**DROPPED per V-002 resolution.**

`TradeCopierPanel._engine` remains typed as `CopyEngine` (line 118).  
Rationale: `TradeCopierPanel` calls ~20+ methods on `_engine` that are NOT on `ICopyEngine`
(Subscribe, Unsubscribe, LoadRules, ArmPendingBe, DisarmPendingBe, SetEnabled, Trim, Flatten,
CancelPendingEntries, event subscriptions, etc.). Changing the field type would cause ~20 compile
errors with zero benefit — testability is fully achieved by the static helper in P4.

---

### P4 (formerly P3 in Rev 1) — TradeCopierPanel: Static Helper + DispatchShortcut Update

**File:** `src/PropTraderTools/TradeCopierPanel.cs`

#### P4.1 — Add `ExecuteBreakEven` Static Method

Add immediately after the `DispatchShortcut` method (after the closing brace of `DispatchShortcut`
at line 3067, before the next method `BuildRiskAtrRow` at line 3069).

**Method signature:**
```csharp
// LANE-G -- ExecuteBreakEven: testable static helper for DispatchShortcut Key.B path.
// Accepts ICopyEngine so tests can pass a stub without NT8 runtime.
// CYC=1: single statement, no branches.
// JS-021: no lock. JS-002: void, no return null.
internal static void ExecuteBreakEven(
    ICopyEngine engine,
    Account leader,
    Instrument instrument,
    int beBuffer)
{
    engine.BreakEven(leader, instrument, beBuffer);
}
```

**Access:** `internal static` — accessible from `CopyEngineTests.cs` in the same assembly
(via `[assembly: InternalsVisibleTo("...")]` which must already be in place for other internal
test targets in this project).

**CYC:** 1 (single statement, no branches).  
**JS-021:** No lock. **JS-002:** void, no return null. **ASCII-only:** All identifiers ASCII.

#### P4.2 — Update `DispatchShortcut` Key.B Case

**Current Key.B case (line 3063–3065):**
```csharp
case Key.B:
    _engine.BreakEven(_leaderAccount, _instrument, _beBuffer);
    break;
```

**After Lane G:**
```csharp
case Key.B:
    ExecuteBreakEven(_engine, _leaderAccount, _instrument, _beBuffer);
    break;
```

**Compile correctness:** `_engine` is typed as `CopyEngine`. `CopyEngine : ICopyEngine`.
`ExecuteBreakEven` takes `ICopyEngine engine`. `CopyEngine` is implicitly assignable to
`ICopyEngine` — this compiles without cast. PASS.

**CYC of DispatchShortcut:** Unchanged at 5 (switch entry + 4 case arms). PASS.

**Behavioral change:** Zero — `ExecuteBreakEven` calls `engine.BreakEven(leader, instrument, beBuffer)`
which is exactly what the inline call did. Same arguments, same method, same result.

---

### T — Test: LaneGTests Class

**File:** `src/PropTraderTools/CopyEngineTests.cs`

**Location:** New additive class at end of file (after `LaneETests`).

#### T.1 — StubEngine (local private stub)

```csharp
private sealed class StubEngine : ICopyEngine
{
    public Account     LastLeader;
    public Instrument  LastInstrument;
    public int         LastBuffer = -1;

    public void RelayBe(BeEventArgs e) { }
    public void RelayTrim(TrimEventArgs e) { }
    public void RelayFlatten(FlatEventArgs e) { }
    public void RelayCancel(CancelEventArgs e) { }

    public void BreakEven(Account leader, Instrument instrument, int beBuffer)
    {
        LastLeader     = leader;
        LastInstrument = instrument;
        LastBuffer     = beBuffer;
    }
}
```

**Why no NT8 runtime needed:** `StubEngine` does NOT call any NinjaTrader type constructor or
method. `Account` and `Instrument` fields are just reference holders. `null` is passed in the
test — no NT8 type instantiation occurs. xUnit can JIT-resolve and run this without NT8 host.

#### T.2 — Test Method

```csharp
// LANE-G-TEST-01: TradeCopierPanel.ExecuteBreakEven passes beBuffer to ICopyEngine.BreakEven.
// No [Skip] — no NT8 runtime required: StubEngine.BreakEven records call without NT8 dispatch.
[Fact]
public void TradeCopierPanel_ExecuteBreakEven_PassesBufferToEngine()
{
    // Arrange
    var stub = new StubEngine();
    // Act
    TradeCopierPanel.ExecuteBreakEven(stub, null, null, 3);
    // Assert
    Assert.Equal(3, stub.LastBuffer);
}
```

**No `[Fact(Skip=...)]`** — this test runs without NT8 runtime.  
**Assert:** `Assert.Equal(3, stub.LastBuffer)` verifies the buffer value was forwarded unchanged.

#### T.3 — LaneGTests Class Wrapper

```csharp
/// <summary>
/// LANE-G: TradeCopierPanel.ExecuteBreakEven testability tests.
/// DW-B26-01 resolution: behavioral test replaces reflection-based absence assertion.
/// </summary>
public class LaneGTests
{
    private sealed class StubEngine : ICopyEngine
    {
        public Account     LastLeader;
        public Instrument  LastInstrument;
        public int         LastBuffer = -1;

        public void RelayBe(BeEventArgs e)     { }
        public void RelayTrim(TrimEventArgs e) { }
        public void RelayFlatten(FlatEventArgs e) { }
        public void RelayCancel(CancelEventArgs e) { }

        public void BreakEven(Account leader, Instrument instrument, int beBuffer)
        {
            LastLeader     = leader;
            LastInstrument = instrument;
            LastBuffer     = beBuffer;
        }
    }

    [Fact]
    public void TradeCopierPanel_ExecuteBreakEven_PassesBufferToEngine()
    {
        // Arrange
        var stub = new StubEngine();
        // Act
        TradeCopierPanel.ExecuteBreakEven(stub, null, null, 3);
        // Assert
        Assert.Equal(3, stub.LastBuffer);
    }
}
```

---

## 5. Method Signatures (Engineer Contract)

| File | Method | Signature | Access | CYC |
|------|--------|-----------|--------|-----|
| PttContracts.cs | ICopyEngine.BreakEven | `void BreakEven(Account leader, Instrument instrument, int beBuffer)` | interface member | N/A |
| CopyEngine.cs | CopyEngine.BreakEven | `public void BreakEven(Account leader, Instrument instrument, int bufferTicks)` (line 6871) | public | 5 (unchanged body; access modifier only) |
| TradeCopierPanel.cs | ExecuteBreakEven | `internal static void ExecuteBreakEven(ICopyEngine engine, Account leader, Instrument instrument, int beBuffer)` | internal static | 1 |
| CopyEngineTests.cs | LaneGTests.StubEngine.BreakEven | `public void BreakEven(Account leader, Instrument instrument, int beBuffer)` | public (interface impl) | 1 |
| CopyEngineTests.cs | LaneGTests.TradeCopierPanel_ExecuteBreakEven_PassesBufferToEngine | `[Fact] public void TradeCopierPanel_ExecuteBreakEven_PassesBufferToEngine()` | public | 1 |

---

## 6. Compile Safety Analysis

| Risk | Analysis | Verdict |
|------|----------|---------|
| Adding BreakEven to ICopyEngine breaks CopyEngine compilation | CopyEngine already has `BreakEven(Account,Instrument,int)` at line 6871; after P2 access change it is `public`, satisfying the interface member | SAFE |
| Adding BreakEven to ICopyEngine breaks MockCopyEngineRelay | MockCopyEngineRelay does not exist as a class (comments-only reference) | SAFE |
| ExecuteBreakEven(ICopyEngine) called with CopyEngine _engine | CopyEngine : ICopyEngine — implicit assignment; no cast needed | SAFE |
| _engine field type stays CopyEngine — all 20+ non-ICopyEngine calls still compile | _engine field type is unchanged; no compile impact | SAFE |
| StubEngine in LaneGTests: Account/Instrument fields hold null | Test passes null; no NT8 type constructor called; xUnit JIT safe | SAFE |

---

## 7. Behavioral Change

**Zero behavioral change** in production paths.

- `DispatchShortcut` Key.B previously called `_engine.BreakEven(_leaderAccount, _instrument, _beBuffer)`.
- After Lane G it calls `ExecuteBreakEven(_engine, _leaderAccount, _instrument, _beBuffer)`.
- `ExecuteBreakEven` calls `engine.BreakEven(leader, instrument, beBuffer)`.
- Net effect: identical NT8 dispatch chain.

---

## 8. JS Rule Constraints (All Methods)

| Method | JS-001 (ASCII) | JS-002 (void) | JS-013 (CYC≤8) | JS-021 (no lock) | JS-023 (Dispatcher) |
|--------|----------------|---------------|----------------|-----------------|---------------------|
| ICopyEngine.BreakEven | PASS | PASS (void) | N/A | N/A | N/A |
| CopyEngine.BreakEven | PASS | PASS (void) | PASS (5) | PASS | N/A (body unchanged) |
| ExecuteBreakEven | PASS | PASS (void) | PASS (1) | PASS | N/A (not UI method) |
| StubEngine.BreakEven | PASS | PASS (void) | PASS (1) | PASS | N/A (test only) |
| Test [Fact] | PASS | PASS (void) | PASS (1) | PASS | N/A |

---

## 9. Seven-Scan Checklist (Pre-Engineering Gate)

| Scan | Description | Expected Result |
|------|-------------|-----------------|
| SCAN-01 | `grep -r "lock(" src/PropTraderTools/` | 0 matches in changed files |
| SCAN-02 | `grep -r "throw " src/PropTraderTools/TradeCopierPanel.cs` | 0 new throw statements |
| SCAN-03 | `grep -r "DateTime.Now" src/PropTraderTools/` | 0 matches in changed files |
| SCAN-04 | `grep -r "FontFamily\|#[0-9A-Fa-f]\{6\}" src/PropTraderTools/` | 0 matches in changed files |
| SCAN-05 | `grep -rP "\?\.(.*)\s*-=" src/PropTraderTools/` | 0 null-conditional event unsubscriptions in changed files |
| SCAN-06 | CYC audit of all new/modified methods | All ≤ 8: ExecuteBreakEven=1, CopyEngine.BreakEven=5 (body unchanged; only access modifier changed), StubEngine.BreakEven=1, test=1 |
| SCAN-07 | Unicode/non-ASCII char scan of changed file sections | 0 non-ASCII characters in any new code |

---

## 10. Test Count Impact

| Metric | Value |
|--------|-------|
| Baseline (Lane F final) | 178 passed, 364 skipped, 0 failed, 542 total |
| Lane G deletions | `TradeCopierPanel_BeBufferBox_FieldDoesNotExist` [Fact(Skip=...)] DELETED from CopyEngineTests.cs (was counted in Skipped=364) |
| Lane G additions | +1 [Fact] (LaneGTests.TradeCopierPanel_ExecuteBreakEven_PassesBufferToEngine, no Skip) |
| Net change | Skipped: 364 − 1 = 363. Passed: 178 + 1 = 179. Total: 542 − 1 + 1 = 542 (unchanged). |
| **Lane G expected** | **179 passed, 363 skipped, 0 failed, 542 total** |

**Explicit test disposition:**
- `TradeCopierPanel_BeBufferBox_FieldDoesNotExist` `[Fact(Skip=...)]` — **DELETED** (removed from CopyEngineTests.cs). This was the reflection-based absence assertion (DW-B26-01 Option B). It is replaced entirely by the new behavioral test below.
- `TradeCopierPanel_ExecuteBreakEven_PassesBufferToEngine` `[Fact]` — **ADDED** (new; no Skip). This is the DW-B26-01 Option A behavioral test.
- Net Skip delta: −1. Net Pass delta: +1. Net Total delta: 0.

---

## 11. Deferred Items Closed

| ID | Item | This Lane |
|----|------|-----------|
| DW-B26-01 | Reflection test upgrade (Option B → Option A): `TradeCopierPanel_BeBufferBox_FieldDoesNotExist` upgraded to behavioral `TradeCopierPanel_ExecuteBreakEven_PassesBufferToEngine` via static helper extraction. | **CLOSED** |

---

## 12. New Deferred Items

None. Lane G is minimal and complete. No new deferred items introduced.

---

## 13. Data Flow Diagram

```
User presses B on chart
         |
         v
chart.PreviewKeyDown (UI thread)
         |
         v
OnChartKeyDown (TradeCopierPanel)
         |
         v
DispatchShortcut(Key.B)                         [unchanged entry point]
         |
         v
ExecuteBreakEven(_engine, _leaderAccount,       [NEW: static helper]
                 _instrument, _beBuffer)
         |
         v
engine.BreakEven(leader, instrument, beBuffer)  [via ICopyEngine interface]
         |
         v (production: _engine is CopyEngine)
CopyEngine.BreakEven(leader, instrument, bufferTicks)  [public after P2]
         |
         v
MoveStopToBreakEven(leader, ...) + foreach AllAccounts
```

```
Test path:
LaneGTests.TradeCopierPanel_ExecuteBreakEven_PassesBufferToEngine()
         |
         v
TradeCopierPanel.ExecuteBreakEven(stub, null, null, 3)
         |
         v
stub.BreakEven(null, null, 3)                  [StubEngine records call]
         |
         v
Assert.Equal(3, stub.LastBuffer)               [PASS]
```

---

*ptt-architect · PTT-DEFERRED-0911 Lane G · Revision 3 · 2026-09-12*
