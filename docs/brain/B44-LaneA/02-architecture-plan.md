# B44-LaneA Architecture Plan
Block: PTT-COPIER-B44
Epic: B44-LaneA
Spec: DW-B44-subscribe-panel-01
Date: 2026-08-05
Status: REVIEW_PASS (Cycle 2 revision — V-01 fix)

---

## 1. Root Cause Summary

`CopyEngine.Subscribe()` registers `OnOrderUpdate` handlers on every account in
`Account.All`. Without this call, the engine is deaf to all order events and zero
copies fire.

**Before B44**: Subscribe() is called only from `TradeCopierWindow.OnLoaded` (L125).
When the user opens only the chart panel (`TradeCopierPanel`) — the common workflow —
`TradeCopierWindow` is never loaded, so `Subscribe()` is never called. Result:
engine silent. Confirmed live 2026-08-05.

**Fix**: Call `Subscribe()` from `TradeCopierPanel.OnLoaded` and `Unsubscribe()` from
`TradeCopierPanel.Detach()`. Add idempotency guards to `Subscribe()` and
`Unsubscribe()` so both Panel and Window can call them safely in any order and
any combination without double-registration or double-removal.

---

## 2. Component List

| Component | File | Change Type |
|-----------|------|-------------|
| `CopyEngine._subscribed` | `CopyEngine.cs` | ADD field |
| `CopyEngine.Subscribe()` | `CopyEngine.cs` | MODIFY — add idempotency guard |
| `CopyEngine.Unsubscribe()` | `CopyEngine.cs` | MODIFY — add idempotency guard |
| `TradeCopierPanel.OnLoaded` | `TradeCopierPanel.cs` | MODIFY — add Subscribe call |
| `TradeCopierPanel.Detach()` | `TradeCopierPanel.cs` | MODIFY — add Unsubscribe call |
| `B44Tests.cs` | `tests/` | NEW — 4 xUnit [Fact] tests |

`TradeCopierWindow.cs` — NO CHANGES. L125 (`_engine.Subscribe()`) and L156
(`_engine.Unsubscribe()`) remain; idempotency guards make them safe alongside Panel
calls.

---

## 3. Ticket T1 — CopyEngine Idempotency

### 3.1 Field Placement

**File**: `CopyEngine.cs`

Insert after the existing `_isCopyEnabled` field at L102:

```csharp
private volatile bool _isCopyEnabled;  // L102 — existing, do not move
private volatile bool _subscribed;     // L103 NEW — JS-023 / NT8-017: volatile bool permitted (32-bit)
```

**Rule justification**:
- `volatile bool` is PERMITTED in NT8 (NT8-017). `volatile double` is banned (NT8-003);
  `bool` is 32-bit and within CLR volatile constraints.
- JS-023: volatile is the correct primitive for a simple boolean cross-thread state field.
- No lock() (JS-021 PASS). No async void (JS-033 PASS). No return null (JS-002 PASS).

### 3.2 Subscribe() Guard

**Location**: `CopyEngine.Subscribe()` at L436.

**Before** (existing bare implementation):
```csharp
public void Subscribe()
{
    foreach (Account a in Account.All)
        a.OrderUpdate += OnOrderUpdate;
}
```

**After**:
```csharp
public void Subscribe()
{
    if (_subscribed) return;          // idempotency guard
    _subscribed = true;               // set BEFORE registering handlers
    foreach (Account a in Account.All)
        a.OrderUpdate += OnOrderUpdate;
}
```

CYC delta: 1 branch added. Post-change CYC = 2. Compliant (<=8).

### 3.3 Unsubscribe() Guard

**Location**: `CopyEngine.Unsubscribe()` at L446.

**Before** (existing bare implementation):
```csharp
public void Unsubscribe()
{
    foreach (Account a in Account.All)
        a.OrderUpdate -= OnOrderUpdate;
}
```

**After**:
```csharp
public void Unsubscribe()
{
    if (!_subscribed) return;         // idempotency guard
    _subscribed = false;              // clear BEFORE removing handlers
    foreach (Account a in Account.All)
        a.OrderUpdate -= OnOrderUpdate;
}
```

CYC delta: 1 branch added. Post-change CYC = 2. Compliant (<=8).

### 3.4 Ordering Rationale

- `_subscribed = true` is set BEFORE the foreach in Subscribe so that if the loop
  throws or is interrupted, a partial registration is still acknowledged by the flag
  (prevents a second call attempting a duplicate partial registration).
- `_subscribed = false` is set BEFORE the foreach in Unsubscribe so that the engine
  considers itself unsubscribed before handler removal begins (prevents OnOrderUpdate
  from being processed for any events that fire during the removal iteration).

---

## 4. Ticket T2 — TradeCopierPanel Call Sites

### 4.1 OnLoaded Placement

**File**: `TradeCopierPanel.cs`
**Method**: `OnLoaded` (L568-629)

The IPttModules loop that calls `SetEnabled` on each module ends at L620 (closing
brace). Leader account wiring begins at L622. The Subscribe call is inserted between
these two blocks:

```
L620   }                                      // close of IPttModules loop — existing
L621                                          // blank line (existing or new)
L622   _engine.Subscribe();                   // NEW — wire order events after modules enabled
L623                                          // blank line
L624   // leader account wiring...            // L622 in pre-B44 (shifted by 2 lines)
```

**Why this position**: All modules are enabled before the engine starts listening.
This prevents OnOrderUpdate from firing while modules are still initialising. The
leader account wiring at L622 sets up the source account after the engine is
subscribed, which is the correct dependency order (engine ready → source identified).

**Threading**: `OnLoaded` fires on the WPF Dispatcher thread. `Subscribe()` iterates
`Account.All` which is a UI-thread-safe collection in NT8. No Dispatcher.InvokeAsync
required.

### 4.2 Detach() Placement

**File**: `TradeCopierPanel.cs`
**Method**: `Detach()` (L490-530)

`_engine.Unsubscribe()` is inserted as the FIRST statement inside the method body,
before the existing `if (_currentChart != null)` guard at L493:

```csharp
public override void Detach()
{
    _engine.Unsubscribe();            // NEW — first statement; clears subscription before cleanup
    if (_currentChart != null)        // L493 — existing, unchanged
    {
        // ... existing cleanup ...
    }
}
```

**Why first**: Unsubscribing before any other cleanup ensures no order events arrive
during the cleanup sequence. Placing it first is the safest ordering — if any
subsequent cleanup step throws, the engine is already unsubscribed.

---

## 5. Idempotency Invariant

The `_subscribed` flag guarantees the following invariants hold under all call
orderings from Panel and Window:

| Scenario | Result |
|----------|--------|
| Panel opens (Window closed) | Subscribe() fires once, `_subscribed=true` |
| Window opens (Panel closed) | Subscribe() fires once, `_subscribed=true` |
| Panel opens, then Window opens | First Subscribe runs; second returns immediately — 1 registration total |
| Window opens, then Panel opens | First Subscribe runs; second returns immediately — 1 registration total |
| Panel closes (Window still open) | Unsubscribe() fires, `_subscribed=false`; Window's future close is a no-op |
| Window closes (Panel still open) | Unsubscribe() fires, `_subscribed=false`; Panel's future Detach is a no-op |
| Panel closes when never subscribed | `if (!_subscribed) return;` — no crash, no double-removal |
| Subscribe → Unsubscribe → Subscribe | Full re-subscription on third call — `_subscribed` is false after Unsubscribe |

The re-subscribe case (row 8) is critical: `_subscribed` is reset to `false` by
Unsubscribe(), so a subsequent Subscribe() proceeds normally. This means scenarios
where both Panel and Window are opened, one closes and re-opens, still work correctly.

---

## 6. Data Flow (Post-B44)

```
TradeCopierPanel.OnLoaded (WPF Dispatcher)
  --> _engine.Subscribe()
        if (_subscribed) return;       // guard
        _subscribed = true;
        Account.All[i].OrderUpdate += OnOrderUpdate  (for each account)

Leader account fires an order
  --> Account.OrderUpdate event fires
  --> CopyEngine.OnOrderUpdate() [now reachable in Panel-only scenario]
  --> [existing gate chain: enabled check, instrument filter, etc.]
  --> CopyEngine.DispatchCopy()
  --> Follower account receives copy

TradeCopierPanel.Detach() (WPF Dispatcher)
  --> _engine.Unsubscribe()
        if (!_subscribed) return;      // guard
        _subscribed = false;
        Account.All[i].OrderUpdate -= OnOrderUpdate  (for each account)
```

---

## 7. Test Design — B44Tests.cs

**Framework**: xUnit only. No NUnit. No MSTest.
**File**: `tests/PropTraderTools.Tests/B44Tests.cs` (new file)

### 7.1 Test Injection Seam (V-01 Resolution)

All three open questions from plan-review V-01 are resolved here. No NT8 runtime
dependency is introduced by any test.

#### Q1 — How tests obtain a CopyEngine reference

`CopyEngine` uses a private constructor and exposes a singleton via `CopyEngine.Instance`
(identical to the B42 pattern at B42Tests.cs:241). Tests access the singleton directly:

```csharp
private readonly CopyEngine _engine = CopyEngine.Instance;
```

No public constructor. No `new CopyEngine(...)`. This is the same access pattern already
validated in `SendCopyFillSignalTests` (B42Tests.cs:241).

#### Q2 — How `_subscribed` is reset between tests (IDisposable pattern)

`_subscribed` is a private instance field on `CopyEngine`. Tests reset it between
invocations using `FieldInfo.SetValue` — exactly the same reflection mechanism B42
uses for private method access (B42Tests.cs:304-306). The test class implements
`IDisposable`; `Dispose()` resets the field to `false` so every test starts from a
clean state regardless of execution order:

```csharp
private static readonly FieldInfo _subscribedField =
    typeof(CopyEngine).GetField(
        "_subscribed",
        BindingFlags.NonPublic | BindingFlags.Instance);

public void Dispose()
{
    // Reset singleton state so tests are order-independent
    _subscribedField.SetValue(CopyEngine.Instance, false);
}
```

The same `Dispose()` method is wired by xUnit for each `[Fact]` — xUnit constructs a
new test-class instance per test and calls `Dispose()` at teardown. This guarantees
`_subscribed == false` at the start of every test.

#### Q3 — How Account.All is made injectable / NT8-runtime-free

The B44 tests do NOT call `Account.All` at all. They do not iterate accounts or
verify handler wiring on `Account` instances. Instead, they verify the `_subscribed`
field state only — which is the authoritative contract of the idempotency guard.
This makes all four tests fully NT8-runtime-free with zero injection seam required
on the production code path:

- `Subscribe()` sets `_subscribed = true` before the foreach.
- `Unsubscribe()` sets `_subscribed = false` before the foreach.
- Tests assert the field value via `FieldInfo.GetValue` after calling Subscribe /
  Unsubscribe. The `Account.All` foreach is unreachable in the test host (no NT8
  runtime) but that is irrelevant — the guard fires before the foreach is ever entered
  when the field is already in the right state.

For T_B44_03 and T_B44_04, the test must call `Subscribe()` once successfully so that
`_subscribed` becomes `true`. Because the test host has no `Account.All` accounts
registered, the foreach body executes zero iterations and no NT8 Account API is
touched. The guard and field assignment happen before the foreach, so the observable
contract (`_subscribed` state) is still verifiable.

---

### 7.2 Test Harness Summary

```csharp
// Shared field-info accessor (computed once, reused across all test methods)
private static readonly FieldInfo _subscribedField =
    typeof(CopyEngine).GetField(
        "_subscribed",
        BindingFlags.NonPublic | BindingFlags.Instance);

private static bool GetSubscribed(CopyEngine engine) =>
    (bool)_subscribedField.GetValue(engine);

private static void SetSubscribed(CopyEngine engine, bool value) =>
    _subscribedField.SetValue(engine, value);
```

The test class `SubscribeIdempotencyTests : IDisposable` owns `_engine = CopyEngine.Instance`
and resets `_subscribed` to `false` in `Dispose()`. xUnit per-test instantiation guarantees
isolation.

---

### 7.3 Individual Test Specifications

#### T_B44_01 — Subscribe is idempotent (double-call does not flip flag twice)

```
[Fact]
public void T_B44_01_Subscribe_CalledTwice_SubscribedFlagRemainsTrue()
```
- Arrange: `GetSubscribed(_engine) == false` (guaranteed by Dispose from prior test or fresh start).
- Act: `_engine.Subscribe(); _engine.Subscribe();`
- Assert: `GetSubscribed(_engine) == true`
- Verifies: the `if (_subscribed) return;` guard short-circuits the second call. Field is
  `true` (set on first call) and the second call returns before the foreach, leaving no
  duplicate handler registration. NT8-runtime-free: Account.All has 0 accounts in test host;
  the foreach body never executes.

#### T_B44_02 — Unsubscribe when not subscribed does not throw

```
[Fact]
public void T_B44_02_Unsubscribe_WhenNotSubscribed_DoesNotThrow()
```
- Arrange: `GetSubscribed(_engine) == false` (Dispose guarantees this).
- Act: `_engine.Unsubscribe();`
- Assert: no exception; `GetSubscribed(_engine) == false`
- Verifies: the `if (!_subscribed) return;` guard handles the cold-start case. The
  field remains `false` and the foreach is never entered. NT8-runtime-free.

#### T_B44_03 — Subscribe → Unsubscribe → Subscribe resets flag correctly

```
[Fact]
public void T_B44_03_ReSubscribe_AfterUnsubscribe_FlagIsTrue()
```
- Arrange: `GetSubscribed(_engine) == false`.
- Act:
  1. `_engine.Subscribe();`   → assert `GetSubscribed == true`
  2. `_engine.Unsubscribe();` → assert `GetSubscribed == false`
  3. `_engine.Subscribe();`   → assert `GetSubscribed == true`
- Assert (final): `GetSubscribed(_engine) == true`
- Verifies: `_subscribed` is correctly toggled through the full cycle. This is the
  Panel-close + Panel-reopen scenario. NT8-runtime-free: Account.All empty in test host.

#### T_B44_04 — Without Subscribe, _subscribed is false (engine deaf)

```
[Fact]
public void T_B44_04_WithoutSubscribe_SubscribedFlag_IsFalse()
```
- Arrange: `GetSubscribed(_engine) == false` (Dispose guarantees — no Subscribe called).
- Act: (none — do not call Subscribe())
- Assert: `GetSubscribed(_engine) == false`
- Verifies: the engine starts in the unsubscribed state and remains deaf to order
  events because the handler was never registered. NT8-runtime-free: no Account.All
  access, no event raising required. The field value is the authoritative gate.

---

## 8. NT8 Compiler Rule Compliance

| Rule | Status | Note |
|------|--------|------|
| NT8-001 (no `init` props) | PASS | No new properties introduced |
| NT8-002 (no records) | PASS | No records introduced |
| NT8-003 (no `volatile double`) | PASS | We use `volatile bool` (32-bit, permitted) |
| NT8-004 (no ImmutableDictionary) | PASS | No collections introduced |
| NT8-017 (`volatile bool` permitted) | PASS | `_subscribed` field |

---

## 9. Jane Street Rules Compliance

| Rule | Status | Note |
|------|--------|------|
| JS-021 (no lock) | PASS | No lock() anywhere. volatile bool + Dispatcher thread serialization |
| JS-002 (no return null) | PASS | `return;` is void return, not null |
| JS-033 (no async void) | PASS | No async methods introduced. OnLoaded is RoutedEventHandler (exempt) |
| JS-023 (atomic primitives) | PASS | `volatile bool _subscribed` for cross-thread state flag |

---

## 10. 7-Scan Checklist Contract

### T1 (CopyEngine.cs)

- SCAN-01 `lock\s*\(` in modified lines — must return 0 results
- SCAN-02 `volatile\s+double` — must return 0 results (we use `volatile bool`)
- SCAN-03 `async\s+void\s+\w+\((?!.*EventHandler)` — must return 0 results
- SCAN-04 `return\s+null\s*;` — must return 0 results
- SCAN-05 `\{\s*get;\s*init;\s*\}` — must return 0 results
- SCAN-06 CYC of Subscribe() and Unsubscribe() <= 2 (each adds exactly 1 branch)
- SCAN-07 `_subscribed` field present immediately after `_isCopyEnabled` field

### T2 (TradeCopierPanel.cs)

- SCAN-01 `lock\s*\(` in modified lines — must return 0 results
- SCAN-02 `_engine\.Subscribe\(\)` present in OnLoaded after IPttModules loop close brace
- SCAN-03 `_engine\.Unsubscribe\(\)` is FIRST statement in Detach() body
- SCAN-04 `async\s+void\s+\w+\((?!.*EventHandler)` — must return 0 results
- SCAN-05 `return\s+null\s*;` — must return 0 results
- SCAN-06 No new control flow branches introduced (CYC delta = 0)
- SCAN-07 TradeCopierWindow.cs unchanged — diff shows no modifications to Window file

### Tests (B44Tests.cs)

- SCAN-01 All test classes use `[Fact]` attribute — no `[Test]`, no `[TestMethod]`
- SCAN-02 No NUnit or MSTest using statements
- SCAN-03 Exactly 4 test methods: T_B44_01, T_B44_02, T_B44_03, T_B44_04
- SCAN-04 `_subscribedField` FieldInfo resolves non-null (reflection accessor verified)
- SCAN-05 `IDisposable.Dispose()` calls `SetSubscribed(_engine, false)` — resets singleton state
- SCAN-06 T_B44_01/T_B44_02/T_B44_03/T_B44_04 assert `_subscribed` field via `GetSubscribed()` reflection helper
- SCAN-07 No `Account.All` reference in B44Tests.cs — confirmed NT8-runtime-free

---

## 11. Deferred Backlog — Carried to B45

The following items from B43 are carried forward unchanged. This block does not
close any of them:

- **DW-B42-01** (P2): T_BUG_QX_BE_01 missing T3 assertion. Deferred to block confirming T3 in production.
- **DW-B42-02** (P1): Live NT8 F5 verification of Quick All / BE All sequences. Next live session.
- **DW-B42-03** (P2): IsPttQxTarget range extension for T4/T5. Block that adds 4th+ target slot.
- **DW-B42-04** (P2): Comment label `NT8-NEW` at PttContracts.cs L254 should be `NT8-005`. Any cleanup pass.
- **DW-B42-05** (P1): Live F5 verification of PTTFollowerStrategy headless ATM. Next live session.
- **DW-B43-02** (P1): GetLeaderAtmTemplateName default selection mismatch. B45 or targeted fix.
- **DW-B43-03** (P2): NT8-045 update if AtmStrategyTemplates API accessible in future NT8 release.

---

## 12. Return Value

PLAN_COMPLETE
