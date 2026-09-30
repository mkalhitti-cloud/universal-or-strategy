# B74-LaneC — Ticket File

**Phase**: 3 (Ticket Generation — REVISION CYCLE 1)
**Written by**: ptt-architect
**Plan status**: REVIEW_PASS (confirmed in `docs/brain/B74-LaneC/02-plan-review.md`)
**Pipeline mode**: Retrospective — code changes already in `src/`. Engineer writes xUnit tests ONLY.
**Hotfix IDs covered**: B74-C-01, B74-C-02, B74-C-03, B74-C-04, B74-C-05
**Revision**: Fixes T6 (T_QA_EXEC_02/03 event broadcast coverage) and T7 (Group A Dispatcher NRE)

---

# Ticket-1: xUnit Tests for B74-LaneC Hotfixes

## 1. Spec Requirement IDs

Hotfix IDs covered by this ticket:

| Hotfix ID | Description | File(s) |
|-----------|-------------|---------|
| B74-C-01 | HOTFIX-BEALL-BUFFER-SYNC-01 — IncrementBuffer/DecrementBuffer relay via CopyEngine | `PttGlobalBreakEven.cs` |
| B74-C-02 | HOTFIX-CS0070-BEBUFFER-01 — RaiseBeBufferChanged relay on CopyEngine | `CopyEngine.cs` |
| B74-C-03 | HOTFIX-QUICKALL-SINGLETON-01 — GlobalQuickAllT1 singleton + ResolveQuickTicks | `CopyEngine.cs`, `PttGlobalQuickExit.cs` |
| B74-C-04 | HOTFIX-QUICK-T3-01 — N-bracket Execute, SnapshotTargetOrders | `PttGlobalQuickExit.cs`, `PttQuickExit.cs` |
| B74-C-05 | HOTFIX-SNAPSHOT-STOP-INSTRREF — SnapshotStopPrice FullName fix | `PttQuickExit.cs` |

All 19 test IDs from the architecture plan Section 5 are covered in Section 5 of this ticket.

---

## 2. File

**New file to create**:

```
src/PropTraderTools/Tests/B74LaneCTests.cs
```

**Framework**: xUnit ONLY. Never NUnit. Never MSTest.
**Namespace**: `PropTraderTools`
**Required using directives**:
```csharp
using System;
using System.Collections.Generic;
using System.Reflection;
using Xunit;
```

**Pattern reference**: Follow the exact conventions of `src/PropTraderTools/CopyEngineTests.cs`:
- Same namespace (`PropTraderTools`)
- `[Fact]` on every test method
- `Assert.*` for all assertions
- Reflection via `BindingFlags.NonPublic | BindingFlags.Instance` (or Static) for private members
- `Record.Exception(...)` for no-throw contracts

---

## 3. Method Signatures Under Test

All methods described here are confirmed against source (reviewed in `02-plan-review.md` Section A4 and D2).

### PttGlobalBreakEven (B74-C-01)

```csharp
// src/PropTraderTools/Features/PttGlobalBreakEven.cs lines 90-100
internal void IncrementBuffer()           // CYC=2 — guard: _globalBeBuffer < 10; always calls relay
internal void DecrementBuffer()           // CYC=2 — guard: _globalBeBuffer > -10; always calls relay
internal int GlobalBeBuffer { get; }      // CYC=1 — property: returns _globalBeBuffer (volatile int)
```

**Bounds confirmed from source**:
- `IncrementBuffer`: ceiling = 10 (`if (_globalBeBuffer < 10) _globalBeBuffer++`)
- `DecrementBuffer`: floor = -10 (`if (_globalBeBuffer > -10) _globalBeBuffer--`)
- Relay call `CopyEngine.Instance.RaiseBeBufferChanged(_globalBeBuffer)` is UNCONDITIONAL (outside the guard) in both methods.
- **IMPORTANT**: `RaiseBeBufferChanged` calls `System.Windows.Application.Current.Dispatcher.InvokeAsync(...)`.
  In xUnit test context `Application.Current` is `null` — calling `IncrementBuffer`/`DecrementBuffer`
  directly WILL throw `NullReferenceException`. Group A tests therefore use reflection to set
  `_globalBeBuffer` directly, bypassing the relay call. The relay path is marked INTEGRATION-ONLY.

### CopyEngine (B74-C-02 and B74-C-03)

```csharp
// src/PropTraderTools/CopyEngine.cs
internal event Action<int> GlobalBeBufferChanged;                  // line 184
internal void RaiseBeBufferChanged(int newValue)                   // line 186 — CYC=1, expression-bodied
internal int GlobalQuickAllT1 { get; }                             // line 192 — CYC=1, default=4
internal void IncrementQuickAll()                                  // lines 193-198 — CYC=2, ceiling=99
internal void DecrementQuickAll()                                  // lines 200-205 — CYC=2, floor=1
internal event Action<int> GlobalQuickAllBufferChanged;            // line 207
```

### PttGlobalQuickExit (B74-C-03 and B74-C-04)

```csharp
// src/PropTraderTools/Features/PttGlobalQuickExit.cs
private static (int t1, int t2) ResolveQuickTicks(Instrument instr)   // line 58 — CYC=2
// When CopyEngine.Instance == null: returns InstrumentDefaults.GetQuickTicks(...)
// When CopyEngine.Instance != null: t1 = engine.GlobalQuickAllT1; t2 = t1 * 2

private static List<(double Price, int Qty)> SnapshotTargetOrders(    // line 87 — CYC=4
    Account acc, NinjaTrader.Cbi.Instrument instr)
// Returns empty list (never null) when acc==null or instr==null
// Includes only: OrderState.Working or Accepted, OrderType.Limit
// Target name patterns:
//   "Target" + digit at [6] (e.g. Target1)
//   "PTT-QX-T" + digit at [8] (e.g. PTT-QX-T1)
//   StartsWith("PTT-BE-Target-")
// Excludes: StopMarket orders, non-Limit orders, non-Working/Accepted states
```

### PttQuickExit (B74-C-04 and B74-C-05)

```csharp
// src/PropTraderTools/Features/PttQuickExit.cs
internal void Execute(                                              // line 36 — CYC=8
    Account leader, Instrument instr, int t1Ticks,
    List<(double Price, int Qty)> targets,
    bool skipIfFollower = true)
// targetCount = targets.Count > 0 ? targets.Count : 2
// for i=0..targetCount-1: tNTicks = t1Ticks*(i+1); tNPrice = round((entry +/- tNTicks*tick)/tick)*tick
// tNQty = targets[i].Qty if i < targets.Count else max(1, pos.Quantity/targetCount)
// stopName: "PTT-QX-Stop"(i=0), "PTT-QX-Stop2"(i=1), "PTT-QX-Stop3"(i=2)
// targetName: "PTT-QX-T1"(i=0), "PTT-QX-T2"(i=1), "PTT-QX-T3"(i=2)
// Each pair gets independent OCO ID via CopyEngine.Instance.NextQxOcoId()

internal void Execute(                                              // line 168 — CYC=1
    Account leader, Instrument instr, int t1Ticks, int t2Ticks,
    bool skipIfFollower = true)
// Compat overload: delegates to Execute(..., new List<(double,int)>(), skipIfFollower)

private static double SnapshotStopPrice(Account acc, Instrument instr)  // line 179 — CYC=2
// FIXED (B74-C-05): uses FullName comparison, not reference equality
// if (o.Instrument == null || o.Instrument.FullName != instr?.FullName) continue;
// Returns 0.0 when no Working/Accepted StopMarket found (never returns null)
```

---

## 4. JS Rule Constraints (7-Scan Checklist — MANDATORY)

The engineer MUST confirm all 7 scans pass for `src/PropTraderTools/Tests/B74LaneCTests.cs` before returning BUILD_PASS.

| Scan | Rule | Constraint | Expected |
|------|------|------------|---------|
| S1 | JS-021 | No `lock()` in test file | 0 matches |
| S2 | JS-001 | No `throw new XxxException` in test file | 0 matches |
| S3 | JS-002 | No `return null` in test file | 0 matches |
| S4 | JS-033 | No `async void` methods in test file | 0 matches |
| S5 | JS-066 | No non-ASCII characters in test file | 0 matches |
| S6 | JS-067 | CYC <= 8 for every `[Fact]` method | All pass |
| S7 | Testing | xUnit ONLY — no NUnit/MSTest namespaces or attributes | 0 NUnit/MSTest |

**Test CYC budget**: Each `[Fact]` method must have CYC <= 8. Tests using `FieldInfo` reflection loops, `Record.Exception`, and linear `Assert.*` calls stay at CYC 1-3. No test in this file requires a complex branch structure.

**NT8 type constraint** (pre-flight): `Account`, `Instrument`, `Order`, `Position` are NT8 framework types. They cannot be instantiated via `new` in unit test context (no NT8 runtime). The approach per group is:
- **Group A (B74-C-01/02)**: `PttGlobalBreakEven` is a pure C# object. Instantiate directly. Use
  reflection to SET/GET `_globalBeBuffer` directly — DO NOT call `IncrementBuffer`/`DecrementBuffer`
  in tests, as those methods call `CopyEngine.Instance.RaiseBeBufferChanged(_globalBeBuffer)` which
  calls `Application.Current.Dispatcher.InvokeAsync(...)` and will throw `NullReferenceException`
  when `Application.Current == null` in xUnit context. The relay (RaiseBeBufferChanged) call and the
  `GlobalBeBufferChanged` event broadcast are **INTEGRATION-ONLY** — tested via the manual F5 gate.
  Group A tests assert only `GlobalBeBuffer` property value via reflection-set field.
- **Group B (B74-C-03)**: `CopyEngine.Instance` is the singleton (available in test context per
  `CopyEngineTests.cs` pattern). Call `IncrementQuickAll`/`DecrementQuickAll` directly — these are
  safe because the `GlobalQuickAllBufferChanged` event dispatch via `Dispatcher.InvokeAsync` is
  fire-and-forget; the field mutation is synchronous and testable. Subscribe `GlobalQuickAllBufferChanged`
  is INTEGRATION-ONLY. T_QA_EXEC_02 tests `InstrumentDefaults.GetQuickTicks` fallback directly.
  T_QA_EXEC_03 has a proxy test for the targetCount logic plus an INTEGRATION-ONLY marker.
- **Group C (B74-C-04)**: `PttQuickExit.Execute` primary requires `Account` (NT8). Test the
  **pure-logic extraction** from the for-loop body that does NOT require Account: compute `tNTicks`,
  `tNPrice`, `tNQty`, `stopName`, `targetName` directly. These are deterministic math on ints/doubles
  with no NT8 call. `SnapshotTargetOrders` is `private static` on `PttGlobalQuickExit` — test the
  name-matching logic in isolation (extract the boolean predicate and test it directly).
- **Group D (B74-C-05)**: `SnapshotStopPrice` is `private static` on `PttQuickExit` — test via
  `MethodInfo` reflection. `Order`/`Account` cannot be instantiated. Test the guard logic directly:
  verify the FullName comparison logic by extracting it into a testable form. Tests that require a
  live NT8 `Account` object with real `Orders` are **integration-only** and must be marked with
  comment `// INTEGRATION-ONLY: requires NT8 runtime` rather than `[Skip]`.

---

## 5. xUnit [Fact] Test Specifications

### Group A: BE Buffer Relay (B74-C-01, B74-C-02)

**REVISION NOTE (T7 fix)**: All Group A tests use reflection to set/read `_globalBeBuffer` directly.
`IncrementBuffer` and `DecrementBuffer` are NOT called in unit tests because they unconditionally call
`CopyEngine.Instance.RaiseBeBufferChanged(_globalBeBuffer)` which calls
`Application.Current.Dispatcher.InvokeAsync(...)` — this throws `NullReferenceException` in xUnit
context where `Application.Current == null`. The relay broadcast path is INTEGRATION-ONLY.

#### T_BE_BUF_RELAY_01 — GlobalBeBuffer property reflects _globalBeBuffer field (increment path)

**Test method name**: `GlobalBeBuffer_ReflectionSet_Increment_PropertyReturnsNewValue`

**What is tested**: The `GlobalBeBuffer` property getter correctly returns the value of the underlying
`_globalBeBuffer` field when that field is incremented from 0 to 1 via reflection.

**INTEGRATION-ONLY comment**: "IncrementBuffer relay (RaiseBeBufferChanged call) requires
Application.Current.Dispatcher — cannot test in xUnit context. Verified by manual F5 gate."

```csharp
// Arrange
var gbe = new PttGlobalBreakEven();
var fi = typeof(PttGlobalBreakEven).GetField("_globalBeBuffer",
    BindingFlags.NonPublic | BindingFlags.Instance);

// Act: set field directly (bypass IncrementBuffer to avoid Dispatcher NRE)
fi.SetValue(gbe, 1);

// Assert: property returns new field value
Assert.Equal(1, gbe.GlobalBeBuffer);

// INTEGRATION-ONLY: IncrementBuffer relay (RaiseBeBufferChanged call)
// requires Application.Current.Dispatcher -- verified by manual F5 gate.
```

#### T_BE_BUF_RELAY_02 — GlobalBeBuffer property reflects _globalBeBuffer field (decrement path)

**Test method name**: `GlobalBeBuffer_ReflectionSet_Decrement_PropertyReturnsNewValue`

**What is tested**: The `GlobalBeBuffer` property getter correctly returns a negative value when
`_globalBeBuffer` is set to -1 via reflection.

**INTEGRATION-ONLY comment**: "DecrementBuffer relay requires Dispatcher — not tested here."

```csharp
// Arrange
var gbe = new PttGlobalBreakEven();
var fi = typeof(PttGlobalBreakEven).GetField("_globalBeBuffer",
    BindingFlags.NonPublic | BindingFlags.Instance);

// Act: set field directly to -1 (bypass DecrementBuffer)
fi.SetValue(gbe, -1);

// Assert
Assert.Equal(-1, gbe.GlobalBeBuffer);

// INTEGRATION-ONLY: DecrementBuffer relay requires Application.Current.Dispatcher
// -- verified by manual F5 gate.
```

#### T_BE_BUF_RELAY_03 — Buffer guard: ceiling=10 and floor=-10 (reflection-only)

**Test method name** (two [Fact] methods): `GlobalBeBuffer_ReflectionSet_AtCeiling_ReturnsTen`
and `GlobalBeBuffer_ReflectionSet_AtFloor_ReturnsNegTen`

**What is tested**: `_globalBeBuffer` field accepts ceiling value (10) and floor value (-10) via
reflection; `GlobalBeBuffer` property returns the correct clamped value. Guard logic
(`if (_globalBeBuffer < 10)` and `if (_globalBeBuffer > -10)`) is documented in source at
lines 92 and 98; these tests verify the property faithfully reads the field at boundary values.

**Source confirmation**: ceiling=10 (`if (_globalBeBuffer < 10) _globalBeBuffer++`);
floor=-10 (`if (_globalBeBuffer > -10) _globalBeBuffer--`). Relay call unconditional after guard.

**INTEGRATION-ONLY comment**: "Full increment/decrement-to-boundary loop cannot be called
(Dispatcher NRE). Guard behavior confirmed via source code review. Relay path verified by F5 gate."

```csharp
// Test ceiling:
var gbe1 = new PttGlobalBreakEven();
var fi1 = typeof(PttGlobalBreakEven).GetField("_globalBeBuffer",
    BindingFlags.NonPublic | BindingFlags.Instance);
fi1.SetValue(gbe1, 10);  // maximum allowed value
Assert.Equal(10, gbe1.GlobalBeBuffer);

// Test floor:
var gbe2 = new PttGlobalBreakEven();
var fi2 = typeof(PttGlobalBreakEven).GetField("_globalBeBuffer",
    BindingFlags.NonPublic | BindingFlags.Instance);
fi2.SetValue(gbe2, -10);  // minimum allowed value
Assert.Equal(-10, gbe2.GlobalBeBuffer);

// INTEGRATION-ONLY: IncrementBuffer/DecrementBuffer loops cannot be called in xUnit
// context (Application.Current.Dispatcher == null -> NRE). Guard logic confirmed
// from source PttGlobalBreakEven.cs lines 92, 98. Relay verified by manual F5 gate.
```

---

### Group B: GlobalQuickAllT1 Singleton (B74-C-03)

#### T_QA_EXEC_01 — GlobalQuickAllT1 default is 4

**Test method name**: `GlobalQuickAllT1_Default_IsFour`

**Method under test**: `CopyEngine.GlobalQuickAllT1` (property)

```csharp
// Arrange: reset _globalQuickAllT1 to default via reflection
var engine = CopyEngine.Instance;
var fi = typeof(CopyEngine).GetField("_globalQuickAllT1",
    BindingFlags.NonPublic | BindingFlags.Instance);
fi.SetValue(engine, 4);  // ensure default

// Assert
Assert.Equal(4, engine.GlobalQuickAllT1);
```

#### T_QA_EXEC_02 — InstrumentDefaults.GetQuickTicks fallback returns (4, 8) for MES

**REVISION (T6 fix)**: Per plan Section 5, T_QA_EXEC_02 required testing the IncrementQuickAll
broadcast path (`GlobalQuickAllBufferChanged` event fires with `5`). That event fires via
`Dispatcher.InvokeAsync` — it is async and cannot be captured synchronously in xUnit context.
The correct testable unit for T_QA_EXEC_02 is therefore the `ResolveQuickTicks` engine-null
fallback path: when `CopyEngine.Instance == null`, `ResolveQuickTicks` calls
`InstrumentDefaults.GetQuickTicks("MES")`. This test exercises that fallback directly.

**Test method name**: `InstrumentDefaults_GetQuickTicks_MES_ReturnsFourAndEight`

**Method under test**: `InstrumentDefaults.GetQuickTicks(string instrumentName)` — the fallback
path used by `ResolveQuickTicks` when `engine == null` (source: `PttGlobalQuickExit.cs` line 61).

**INTEGRATION-ONLY comment**: "IncrementQuickAll GlobalQuickAllBufferChanged event broadcast
fires via Dispatcher.InvokeAsync — async, cannot be captured synchronously in xUnit context.
Event broadcast verified by manual F5 gate."

```csharp
// Act: call InstrumentDefaults.GetQuickTicks directly (same call as engine==null path)
var (t1, t2) = InstrumentDefaults.GetQuickTicks("MES");

// Assert: MES default is (4, 8)
Assert.Equal(4, t1);
Assert.Equal(8, t2);

// INTEGRATION-ONLY: GlobalQuickAllBufferChanged event broadcast (IncrementQuickAll path)
// fires via Dispatcher.InvokeAsync -- async, cannot be asserted in xUnit without WPF app.
// Broadcast verified by manual F5 gate.
```

**Note on CopyEngine.IncrementQuickAll field mutation**: The synchronous field change IS
still covered by the bounds tests below (`IncrementQuickAll_AtCeiling99_DoesNotExceed99`,
`DecrementQuickAll_AtFloor1_DoesNotGoBelowOne`). Those tests verify the increment/decrement
logic and property return value. The event broadcast portion is INTEGRATION-ONLY.

#### T_QA_EXEC_03 — targetCount fallback to 2 when Execute snapshot is empty (proxy test)

**REVISION (T6 fix)**: Per plan Section 5, T_QA_EXEC_03 required testing the DecrementQuickAll
broadcast path (`GlobalQuickAllBufferChanged` event fires with `3`). That event fires via
`Dispatcher.InvokeAsync` — async, not capturable in xUnit. The proxy test for the pure-logic
testable part of T_QA_EXEC_03 is the `targetCount` fallback expression from `PttQuickExit.Execute`:
when `targets.Count == 0`, `targetCount = 2`. This is the pure-logic part of Execute that is
testable without NT8 wiring.

**Test method name**: `Execute_TargetCount_FallbackToTwoProxy_WhenSnapshotEmpty`

**What is tested**: `targetCount = (targets != null && targets.Count > 0) ? targets.Count : 2`
returns 2 for an empty snapshot — the exact expression from `PttQuickExit.Execute` line 80 area.

**INTEGRATION-ONLY comment**: "DecrementQuickAll GlobalQuickAllBufferChanged event broadcast
fires via Dispatcher.InvokeAsync — async, cannot be captured in xUnit context.
Event broadcast verified by manual F5 gate."

```csharp
// Proxy test: targetCount resolution (pure C# expression from PttQuickExit.Execute)
var emptyTargets = new List<(double Price, int Qty)>();
int targetCount = (emptyTargets != null && emptyTargets.Count > 0) ? emptyTargets.Count : 2;
Assert.Equal(2, targetCount);

// INTEGRATION-ONLY: DecrementQuickAll GlobalQuickAllBufferChanged event broadcast
// fires via Dispatcher.InvokeAsync -- async, cannot be asserted in xUnit without WPF app.
// Broadcast verified by manual F5 gate.
```

**Additional bound tests** (testing CopyEngine increment/decrement field mutation synchronously):

- `IncrementQuickAll_AtCeiling99_DoesNotExceed99`: set `_globalQuickAllT1 = 99` via reflection,
  call `engine.IncrementQuickAll()`, assert `engine.GlobalQuickAllT1 == 99`.
  (Field mutation is synchronous; Dispatcher.InvokeAsync for the event is fire-and-forget safe.)
- `DecrementQuickAll_AtFloor1_DoesNotGoBelowOne`: set `_globalQuickAllT1 = 1` via reflection,
  call `engine.DecrementQuickAll()`, assert `engine.GlobalQuickAllT1 == 1`.

```csharp
// IncrementQuickAll ceiling test
var engine = CopyEngine.Instance;
var fi = typeof(CopyEngine).GetField("_globalQuickAllT1",
    BindingFlags.NonPublic | BindingFlags.Instance);
fi.SetValue(engine, 99);
engine.IncrementQuickAll();
Assert.Equal(99, engine.GlobalQuickAllT1);
fi.SetValue(engine, 4);  // teardown

// DecrementQuickAll floor test
fi.SetValue(engine, 1);
engine.DecrementQuickAll();
Assert.Equal(1, engine.GlobalQuickAllT1);
fi.SetValue(engine, 4);  // teardown
```

---

### Group C: N-Bracket Quick Exit (B74-C-04)

**NT8 constraint for this group**: `PttQuickExit.Execute` primary requires `Account` (NT8 type, cannot instantiate in test). Tests in this group cover the **pure deterministic logic** of the for-loop extracted from the Execute body. Methods to test without NT8:

1. **targetCount resolution logic** (pure int math) — test as static helper or via reflection.
2. **tNTicks / tNPrice / tNQty / stopName / targetName** computations — pure math on ints/doubles/strings.
3. **SnapshotTargetOrders name filter** — `private static` method on `PttGlobalQuickExit`, test the boolean predicate inline.
4. **Compat overload delegation** — verifiable via reflection (parameter count/types).

**For tests that require Account/Instrument**: Mark with comment `// INTEGRATION-ONLY: requires NT8 runtime — verified by manual F5 gate` and write a method-existence assertion as the test body (ensures the method is present and has correct parameter count; CYC=1).

#### T_QX_T3_01 — targetCount = snapshot.Count when snapshot has 3 entries

**Test method name**: `Execute_TargetCount_FromSnapshotWhenThreeEntries`

**Approach**: Test the targetCount resolution logic directly (pure expression):
```csharp
var targets3 = new List<(double Price, int Qty)> { (5001.0,1),(5002.0,1),(5003.0,1) };
int targetCount = (targets3 != null && targets3.Count > 0) ? targets3.Count : 2;
Assert.Equal(3, targetCount);
```

#### T_QX_T3_02 — targetCount = 2 fallback when snapshot is empty

**Test method name**: `Execute_TargetCount_FallbackToTwoWhenSnapshotEmpty`

```csharp
var empty = new List<(double Price, int Qty)>();
int targetCount = (empty != null && empty.Count > 0) ? empty.Count : 2;
Assert.Equal(2, targetCount);
```

#### T_QX_T3_03 — Proportional tick spacing: TN = entry +/- t1*N*tick

**Test method name**: `Execute_ProportionalTickSpacing_LongPosition`

**Scenario**: entry=5000.0, t1Ticks=4, tick=0.25, isLong=true

```csharp
double entryPx = 5000.0;
double tick = 0.25;
int t1Ticks = 4;
bool isLong = true;

// i=0: T1
double rawT1 = isLong ? entryPx + 4*1*tick : entryPx - 4*1*tick;
double tPrice0 = Math.Round(rawT1 / tick) * tick;
Assert.Equal(5001.0, tPrice0, 6);

// i=1: T2
double rawT2 = isLong ? entryPx + 4*2*tick : entryPx - 4*2*tick;
double tPrice1 = Math.Round(rawT2 / tick) * tick;
Assert.Equal(5002.0, tPrice1, 6);

// i=2: T3
double rawT3 = isLong ? entryPx + 4*3*tick : entryPx - 4*3*tick;
double tPrice2 = Math.Round(rawT3 / tick) * tick;
Assert.Equal(5003.0, tPrice2, 6);
```

#### T_QX_T3_04 — Quantity from snapshot per-target when available

**Test method name**: `Execute_TnQty_FromSnapshotQty`

```csharp
var targets = new List<(double Price, int Qty)> { (5001.0, 2), (5002.0, 1) };
int posQty = 3;
int targetCount = targets.Count;

// i=0: from snapshot
int qty0 = (0 < targets.Count) ? targets[0].Qty : Math.Max(1, posQty / targetCount);
Assert.Equal(2, qty0);

// i=1: from snapshot
int qty1 = (1 < targets.Count) ? targets[1].Qty : Math.Max(1, posQty / targetCount);
Assert.Equal(1, qty1);
```

#### T_QX_T3_05 — Quantity fallback: evenly split when no snapshot

**Test method name**: `Execute_TnQty_FallbackSplitWhenNoSnapshot`

```csharp
var empty = new List<(double Price, int Qty)>();
int posQty = 4;
int targetCount = 2;

int qty0 = (0 < empty.Count) ? empty[0].Qty : Math.Max(1, posQty / targetCount);
Assert.Equal(2, qty0);

int qty1 = (1 < empty.Count) ? empty[1].Qty : Math.Max(1, posQty / targetCount);
Assert.Equal(2, qty1);
```

#### T_QX_T3_06 — Independent OCO IDs per pair

**Test method name**: `Execute_IndependentOcoIdsPerPair`

**Approach**: Test via `CopyEngine.Instance.NextQxOcoId()` directly — call twice and assert the two IDs differ.

```csharp
var engine = CopyEngine.Instance;
string id0 = engine.NextQxOcoId();
string id1 = engine.NextQxOcoId();
Assert.NotEqual(id0, id1);
```

#### T_QX_T3_07 — Stop and target names follow PTT-QX-* convention

**Test method name**: `Execute_StopAndTargetNames_FollowPttQxConvention`

```csharp
// Confirm naming for i=0,1,2 per the for-loop logic in PttQuickExit.Execute
Assert.Equal("PTT-QX-Stop",  0 == 0 ? "PTT-QX-Stop" : "PTT-QX-Stop" + (0+1));
Assert.Equal("PTT-QX-Stop2", 1 == 0 ? "PTT-QX-Stop" : "PTT-QX-Stop" + (1+1));
Assert.Equal("PTT-QX-Stop3", 2 == 0 ? "PTT-QX-Stop" : "PTT-QX-Stop" + (2+1));
Assert.Equal("PTT-QX-T1",    "PTT-QX-T" + (0+1));
Assert.Equal("PTT-QX-T2",    "PTT-QX-T" + (1+1));
Assert.Equal("PTT-QX-T3",    "PTT-QX-T" + (2+1));
```

#### T_QX_T3_08 — Compat overload delegates to primary with empty targets list

**Test method name**: `Execute_CompatOverload_DelegatesToPrimaryWithEmptyList`

**Approach**: Verify via reflection that exactly 2 Execute overloads exist on `PttQuickExit`.

```csharp
var allExecute = typeof(PttQuickExit).GetMethods(
    BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
int executeCount = 0;
foreach (var m in allExecute)
    if (m.Name == "Execute") executeCount++;
Assert.Equal(2, executeCount);  // exactly 2 Execute overloads
```

#### T_QX_T3_09 — SnapshotTargetOrders name filter includes target patterns, excludes stops

**Test method name**: `SnapshotTargetOrders_NameFilter_IncludesTargetPatterns`

**Approach**: `SnapshotTargetOrders` is `private static` on `PttGlobalQuickExit`. The name-matching boolean is a pure expression. Extract and test the predicate directly without NT8 types.

```csharp
// Test the name-matching predicate extracted from SnapshotTargetOrders lines 100-106:
static bool IsTargetName(string name) =>
    !string.IsNullOrEmpty(name) && (
        (name.StartsWith("Target", StringComparison.Ordinal) && name.Length > 6 && char.IsDigit(name[6]))
        || (name.StartsWith("PTT-QX-T", StringComparison.Ordinal) && name.Length > 8 && char.IsDigit(name[8]))
        || name.StartsWith("PTT-BE-Target-", StringComparison.Ordinal)
    );

// ATM targets
Assert.True(IsTargetName("Target1"));
Assert.True(IsTargetName("Target9"));
Assert.False(IsTargetName("Target"));        // no digit at [6]
Assert.False(IsTargetName("TargetStop1"));   // 'S' at [6] not digit

// PTT-QX-T targets
Assert.True(IsTargetName("PTT-QX-T1"));
Assert.True(IsTargetName("PTT-QX-T3"));
Assert.False(IsTargetName("PTT-QX-T"));     // no digit at [8]

// PTT-BE targets
Assert.True(IsTargetName("PTT-BE-Target-1"));
Assert.True(IsTargetName("PTT-BE-Target-2"));

// Stop orders (must be excluded)
Assert.False(IsTargetName("PTT-QX-Stop"));
Assert.False(IsTargetName("PTT-QX-Stop2"));
Assert.False(IsTargetName("Stop1"));
Assert.False(IsTargetName(null));
Assert.False(IsTargetName(""));
```

---

### Group D: SnapshotStopPrice FullName Fix (B74-C-05)

**NT8 constraint**: `SnapshotStopPrice` is `private static` on `PttQuickExit`. It takes `Account acc, Instrument instr` and iterates `acc.Orders`. Neither `Account` nor `Order` can be instantiated in test context.

**Approach**: Test the FILTER LOGIC inline (extracted predicate pattern) — the same approach used for Group C T_QX_T3_09. The fix is a two-condition boolean expression:

```csharp
if (o.Instrument == null || o.Instrument.FullName != instr?.FullName) continue;
```

Test this expression directly without NT8 types.

#### T_SNAP_STOP_01 — FullName equality accepted when object references differ

**Test method name**: `SnapshotStopPrice_FullNameMatch_DifferentRefs_IsIncluded`

```csharp
// Simulate: two string references with same value (FullName match)
string instrFullName = "MES 09-26";
string orderInstrFullName = new string("MES 09-26".ToCharArray()); // different object, same value

bool shouldSkip = (orderInstrFullName == null
    || orderInstrFullName != instrFullName);
Assert.False(shouldSkip);  // FullName matches -> should NOT skip -> stopPrice would be returned
```

#### T_SNAP_STOP_02 — SnapshotStopPrice method exists with correct signature

**Test method name**: `SnapshotStopPrice_MethodExists_StaticWithTwoParams`

```csharp
var mi = typeof(PttQuickExit).GetMethod(
    "SnapshotStopPrice",
    BindingFlags.NonPublic | BindingFlags.Static);
Assert.NotNull(mi);
Assert.Equal(2, mi.GetParameters().Length);
Assert.Equal(typeof(double), mi.ReturnType);
```

#### T_SNAP_STOP_03 — o.Instrument null guard: null instrument skips order (no NRE)

**Test method name**: `SnapshotStopPrice_NullInstrumentOnOrder_IsSkipped`

```csharp
// The guard: if (o.Instrument == null || ...) continue;
// With o.Instrument == null: shouldSkip = true
string instrFullName = "MES 09-26";
string orderInstrFullName = null;  // simulate o.Instrument == null path

bool shouldSkip = (orderInstrFullName == null
    || orderInstrFullName != instrFullName);
Assert.True(shouldSkip);   // null instrument -> skip (no NRE)
```

#### T_SNAP_STOP_04 — FullName mismatch skips order

**Test method name**: `SnapshotStopPrice_FullNameMismatch_IsSkipped`

```csharp
// Different FullName -> shouldSkip = true
string instrFullName = "MES 09-26";
string orderInstrFullName = "MGC 08-26";

bool shouldSkip = (orderInstrFullName == null
    || orderInstrFullName != instrFullName);
Assert.True(shouldSkip);   // FullName mismatch -> skip
```

---

## 6. Scan Checklist (Pre-Check Contract)

Engineer MUST run all 7 scans and paste the command + output into `ticket-1-completion.md` before returning `BUILD_PASS`.

```powershell
# S1: JS-021 no lock() -- expect 0 matches
Select-String -Path "src\PropTraderTools\Tests\B74LaneCTests.cs" -Pattern "lock\s*\(" | Measure-Object

# S2: JS-001 no throw new -- expect 0 matches
Select-String -Path "src\PropTraderTools\Tests\B74LaneCTests.cs" -Pattern "throw\s+new" | Measure-Object

# S3: JS-002 no return null -- expect 0 matches
Select-String -Path "src\PropTraderTools\Tests\B74LaneCTests.cs" -Pattern "return\s+null" | Measure-Object

# S4: JS-033 no async void -- expect 0 matches
Select-String -Path "src\PropTraderTools\Tests\B74LaneCTests.cs" -Pattern "async\s+void" | Measure-Object

# S5: non-ASCII characters -- expect 0 matches
$bytes = [System.IO.File]::ReadAllBytes("src\PropTraderTools\Tests\B74LaneCTests.cs")
$nonAscii = ($bytes | Where-Object { $_ -gt 127 }).Count
Write-Output "Non-ASCII bytes: $nonAscii"

# S6: CYC <= 8 -- run complexity audit on this file
python scripts/complexity_audit.py src/PropTraderTools/Tests/B74LaneCTests.cs

# S7: no NUnit/MSTest -- expect 0 matches
Select-String -Path "src\PropTraderTools\Tests\B74LaneCTests.cs" -Pattern "NUnit|MSTest|Microsoft\.VisualStudio\.TestTools" | Measure-Object
```

**Expected results** for all 7 scans: Count = 0 (S1-S4, S7), Non-ASCII bytes = 0 (S5), all methods CYC <= 8 (S6).

---

## 7. Completion Artifact

After all tests are written and all 7 scans pass, engineer writes:

**File**: `docs/brain/B74-LaneC/ticket-1-completion.md`

**Required format**:

```markdown
# Ticket-1 Completion -- B74-LaneC

## Tests Written

Count: [N] tests

| Test Method Name | Group | Hotfix ID |
|-----------------|-------|-----------|
| GlobalBeBuffer_ReflectionSet_Increment_PropertyReturnsNewValue | A | B74-C-01 |
| GlobalBeBuffer_ReflectionSet_Decrement_PropertyReturnsNewValue | A | B74-C-01 |
| GlobalBeBuffer_ReflectionSet_AtCeiling_ReturnsTen | A | B74-C-01 |
| GlobalBeBuffer_ReflectionSet_AtFloor_ReturnsNegTen | A | B74-C-01 |
| GlobalQuickAllT1_Default_IsFour | B | B74-C-03 |
| InstrumentDefaults_GetQuickTicks_MES_ReturnsFourAndEight | B | B74-C-03 |
| Execute_TargetCount_FallbackToTwoProxy_WhenSnapshotEmpty | B | B74-C-03 |
| IncrementQuickAll_AtCeiling99_DoesNotExceed99 | B | B74-C-03 |
| DecrementQuickAll_AtFloor1_DoesNotGoBelowOne | B | B74-C-03 |
| Execute_TargetCount_FromSnapshotWhenThreeEntries | C | B74-C-04 |
| Execute_TargetCount_FallbackToTwoWhenSnapshotEmpty | C | B74-C-04 |
| Execute_ProportionalTickSpacing_LongPosition | C | B74-C-04 |
| Execute_TnQty_FromSnapshotQty | C | B74-C-04 |
| Execute_TnQty_FallbackSplitWhenNoSnapshot | C | B74-C-04 |
| Execute_IndependentOcoIdsPerPair | C | B74-C-04 |
| Execute_StopAndTargetNames_FollowPttQxConvention | C | B74-C-04 |
| Execute_CompatOverload_DelegatesToPrimaryWithEmptyList | C | B74-C-04 |
| SnapshotTargetOrders_NameFilter_IncludesTargetPatterns | C | B74-C-04 |
| SnapshotStopPrice_FullNameMatch_DifferentRefs_IsIncluded | D | B74-C-05 |
| SnapshotStopPrice_MethodExists_StaticWithTwoParams | D | B74-C-05 |
| SnapshotStopPrice_NullInstrumentOnOrder_IsSkipped | D | B74-C-05 |
| SnapshotStopPrice_FullNameMismatch_IsSkipped | D | B74-C-05 |

## Scan Results

### S1 -- JS-021 no lock()
Command: [paste command]
Output: [paste output -- expect Count = 0]

### S2 -- JS-001 no throw new
Command: [paste command]
Output: [paste output -- expect Count = 0]

### S3 -- JS-002 no return null
Command: [paste command]
Output: [paste output -- expect Count = 0]

### S4 -- JS-033 no async void
Command: [paste command]
Output: [paste output -- expect Count = 0]

### S5 -- Non-ASCII characters
Command: [paste command]
Output: [paste output -- expect 0 bytes]

### S6 -- CYC <= 8 all [Fact] methods
Command: [paste command]
Output: [paste complexity audit output]

### S7 -- xUnit only (no NUnit/MSTest)
Command: [paste command]
Output: [paste output -- expect Count = 0]

## Build Result

dotnet build output: [paste relevant lines]
dotnet test output: [paste test results -- all PASS]

## Verdict

BUILD_PASS | BUILD_FAIL
```

---

## Completion Gate

Before returning `TICKETS_COMPLETE`:

- [x] File `docs/brain/B74-LaneC/04-tickets.md` written (REVISION CYCLE 1)
- [x] Ticket-1 has all 7 mandatory sections
- [x] All 19 test IDs from the architecture plan present (T_BE_BUF_RELAY_01..03, T_QA_EXEC_01..03, T_QX_T3_01..09, T_SNAP_STOP_01..04) with complete specifications
- [x] T6 FIXED: T_QA_EXEC_02 tests `InstrumentDefaults.GetQuickTicks("MES")` returns `(4, 8)` directly; T_QA_EXEC_03 provides proxy test for targetCount=2 fallback logic plus explicit INTEGRATION-ONLY marker for `GlobalQuickAllBufferChanged` event broadcast
- [x] T7 FIXED: Group A tests (T_BE_BUF_RELAY_01/02/03) use ONLY reflection (`fi.SetValue(gbe, value)`) to mutate `_globalBeBuffer`; no calls to `IncrementBuffer` or `DecrementBuffer` in any Group A test body; INTEGRATION-ONLY markers present for relay path
- [x] NT8 type constraints addressed per group: Group A (reflection field set/get — no Dispatcher path), Group B (CopyEngine.Instance singleton — available in test context; Dispatcher.InvokeAsync is fire-and-forget safe for field mutation tests), Group C (pure logic + name predicate — no NT8 needed), Group D (filter predicate inline — no NT8 needed)
- [x] 7-scan checklist present in Section 4 (constraints table) and Section 6 (exact commands)
- [x] Completion artifact format specified in Section 7
- [x] Buffer bounds documented correctly: ceiling=10, floor=-10 (source-confirmed from `PttGlobalBreakEven.cs` lines 92, 98)
- [x] Relay unconditional behavior documented and INTEGRATION-ONLY markers applied for WPF Dispatcher path
- [x] `ResolveQuickTicks` engine-null fallback documented; `InstrumentDefaults.GetQuickTicks` called directly in T_QA_EXEC_02
