# B44-LaneA Tickets
Block: PTT-COPIER-B44
Epic: B44-LaneA
Plan: 02-architecture-plan.md (REVIEW_PASS — Cycle 2)
Date: 2026-08-05
Author: ptt-architect

---

## Ticket Index

| Ticket | Title | File(s) | Spec IDs |
|--------|-------|---------|----------|
| T1 | CopyEngine Idempotency Guards | `CopyEngine.cs` | DW-B44-T1-01, T1-02, T1-03 |
| T2 | TradeCopierPanel Wiring + B44Tests | `TradeCopierPanel.cs`, `B44Tests.cs` (NEW) | DW-B44-T2-01 through T2-06 |

---

---

# T1 — CopyEngine Idempotency Guards

## File Target

```
c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs
```

## Spec Requirement IDs

- DW-B44-T1-01: Add `_subscribed` field after `_isCopyEnabled` at L102
- DW-B44-T1-02: Subscribe() — insert idempotency guard as first two statements
- DW-B44-T1-03: Unsubscribe() — insert idempotency guard as first two statements

## Method Signatures

| Method | Visibility | Return | Change |
|--------|-----------|--------|--------|
| `Subscribe()` | `internal` | `void` | MODIFY — add 2-line guard at top |
| `Unsubscribe()` | `internal` | `void` | MODIFY — add 2-line guard at top |

No signature change. Same access modifier, same return type, same parameter list (none).

## CYC Before / After

| Method | CYC Before | CYC After | Limit | Status |
|--------|-----------|-----------|-------|--------|
| `Subscribe()` | 1 | 2 | 8 | PASS |
| `Unsubscribe()` | 1 | 2 | 8 | PASS |

One `if` branch added to each method — delta is +1 per method.

## Jane Street Rule Constraints

| Rule | Constraint | Verification |
|------|-----------|-------------|
| JS-021 | No `lock()` anywhere | grep `lock\s*(` → 0 results |
| JS-023 | Use atomic primitives for cross-thread state | `volatile bool _subscribed` is correct primitive |
| JS-002 | No `return null` | `return;` is void return — NOT a null return |
| JS-033 | No `async void` (non-event-handler) | No async methods introduced |

## NT8 Compiler Rule Constraints

| Rule | Constraint | Verification |
|------|-----------|-------------|
| NT8-003 | `volatile double` is banned | Field uses `volatile bool` (32-bit, permitted) |
| NT8-017 | `volatile bool` is explicitly permitted | `private volatile bool _subscribed;` is safe |
| NT8-001 | No `{ get; init; }` properties | No new properties introduced |
| NT8-002 | No abstract/sealed records | No records introduced |

---

## Change 1 — Add `_subscribed` Field

**Location**: `CopyEngine.cs`, line immediately after L102.

**Existing line L102** (do not modify):
```csharp
private volatile bool _isCopyEnabled;
```

**Insert as new line L103**:
```csharp
private volatile bool _subscribed;     // B44: idempotency guard — JS-023 / NT8-017
```

**Rule**: `volatile bool` is 32-bit and within CLR volatile constraints. `volatile double`
is banned (NT8-003); `volatile bool` is explicitly permitted (NT8-017). No lock() required.

---

## Change 2 — Subscribe() Idempotency Guard

**Location**: `CopyEngine.cs`, `Subscribe()` method body at L436.

**BEFORE** (existing — do not keep):
```csharp
internal void Subscribe()
{
    foreach (Account acc in Account.All)
        acc.OrderUpdate += OnOrderUpdate;
}
```

**AFTER** (exact replacement — copy verbatim):
```csharp
internal void Subscribe()
{
    if (_subscribed) return;
    _subscribed = true;
    foreach (Account acc in Account.All)
        acc.OrderUpdate += OnOrderUpdate;
}
```

**Ordering rule**: `_subscribed = true` is set BEFORE the foreach. If the loop is
interrupted, the flag already reflects the intent to subscribe, preventing a second
call from attempting a duplicate partial registration.

---

## Change 3 — Unsubscribe() Idempotency Guard

**Location**: `CopyEngine.cs`, `Unsubscribe()` method body at L446.

**BEFORE** (existing — do not keep):
```csharp
internal void Unsubscribe()
{
    foreach (Account acc in Account.All)
        acc.OrderUpdate -= OnOrderUpdate;
}
```

**AFTER** (exact replacement — copy verbatim):
```csharp
internal void Unsubscribe()
{
    if (!_subscribed) return;
    _subscribed = false;
    foreach (Account acc in Account.All)
        acc.OrderUpdate -= OnOrderUpdate;
}
```

**Ordering rule**: `_subscribed = false` is set BEFORE the foreach so that the engine
considers itself unsubscribed before handler removal begins. This prevents OnOrderUpdate
from being processed for any events that fire during the removal iteration.

---

## xUnit Tests (from T2 — see T2 for full implementations)

- `T_B44_01_Subscribe_CalledTwice_SubscribedFlagRemainsTrue` — double Subscribe, flag stays true
- `T_B44_02_Unsubscribe_WhenNotSubscribed_DoesNotThrow` — cold Unsubscribe, no exception, flag false

---

## 7-Scan Checklist — T1

The engineer MUST run all 7 scans and confirm every result before marking T1 complete.

| # | Scan | Command | Expected Result |
|---|------|---------|----------------|
| SCAN-01 | No lock() | `grep -n "lock\s*(" CopyEngine.cs` | 0 matches |
| SCAN-02 | No async void | `grep -n "async void" CopyEngine.cs` | 0 matches |
| SCAN-03 | No return null | `grep -n "return null" CopyEngine.cs` | 0 matches in Subscribe/Unsubscribe |
| SCAN-04 | No volatile double | `grep -n "volatile double" CopyEngine.cs` | 0 matches |
| SCAN-05 | _subscribed field present | `grep -n "_subscribed" CopyEngine.cs` | >= 3 lines (field decl + guard in Subscribe + guard in Unsubscribe) |
| SCAN-06 | CYC compliance | Run complexity_audit.py on CopyEngine.cs | Subscribe CYC=2, Unsubscribe CYC=2, both <= 8 |
| SCAN-07 | Idempotency proof | T_B44_01 passes: Subscribe() called twice, _subscribed=true, no second-call side effect | xUnit green |

## T1 BUILD PASS Criteria

- [ ] `dotnet build` exits 0, zero errors, zero new warnings
- [ ] `grep -n "_subscribed" CopyEngine.cs` returns >= 3 results
- [ ] SCAN-01 through SCAN-04 return 0 matches each
- [ ] SCAN-06: CYC <= 2 for both Subscribe and Unsubscribe
- [ ] T_B44_01 and T_B44_02 xUnit [Fact] tests green

---

---

# T2 — TradeCopierPanel Wiring + B44Tests.cs

## File Targets

```
FILE A: c:\WSGTA\universal-or-strategy\src\PropTraderTools\TradeCopierPanel.cs
FILE B: c:\WSGTA\universal-or-strategy\src\PropTraderTools\B44Tests.cs  (NEW FILE)
```

## Spec Requirement IDs

- DW-B44-T2-01: TradeCopierPanel.Detach() — insert `_engine.Unsubscribe()` as FIRST statement
- DW-B44-T2-02: TradeCopierPanel.OnLoaded — insert `_engine.Subscribe()` after IPttModules loop close brace
- DW-B44-T2-03: T_B44_01 — Subscribe idempotency (double-call)
- DW-B44-T2-04: T_B44_02 — Unsubscribe cold-start safety
- DW-B44-T2-05: T_B44_03 — Subscribe/Unsubscribe/Subscribe full cycle
- DW-B44-T2-06: T_B44_04 — Fresh engine starts unsubscribed

## Method Signatures (TradeCopierPanel.cs)

| Method | Visibility | Return | Change |
|--------|-----------|--------|--------|
| `Detach()` | `public override` | `void` | MODIFY — insert 1 line as first statement |
| `OnLoaded(object, RoutedEventArgs)` | `private` | `void` | MODIFY — insert 1 line after IPttModules loop |

No signature change. CYC delta = 0 for both methods (no new branches, only straight-line calls).

## Jane Street Rule Constraints

| Rule | Constraint | Verification |
|------|-----------|-------------|
| JS-021 | No `lock()` | grep `lock\s*(` TradeCopierPanel.cs → 0 results |
| JS-002 | No `return null` | New code adds no return statements |
| JS-033 | No `async void` (non-event-handler) | No async methods added; `OnLoaded` is RoutedEventHandler (exempt) |

---

## Change 1 — Detach() First Statement

**File**: `TradeCopierPanel.cs`
**Method**: `Detach()` at L490.

**BEFORE** (existing method opening — do not keep):
```csharp
public void Detach()
{
    // B9 T2: unregister click trader before clearing state
    if (_currentChart != null)
        TradeCopierAddOn.UnregisterClickTrader(_currentChart);
```

**AFTER** (exact replacement of method opening — copy verbatim):
```csharp
public void Detach()
{
    _engine.Unsubscribe();
    // B9 T2: unregister click trader before clearing state
    if (_currentChart != null)
        TradeCopierAddOn.UnregisterClickTrader(_currentChart);
```

**Rule**: `_engine.Unsubscribe()` MUST be the FIRST statement in the method body —
before any null checks, before any other cleanup. This ensures no order events arrive
during the cleanup sequence. If any subsequent cleanup step throws, the engine is
already unsubscribed.

---

## Change 2 — OnLoaded Subscribe Call

**File**: `TradeCopierPanel.cs`
**Method**: `OnLoaded` at L568-629.

**Context** (existing code around insertion point at L620-622):
```csharp
            foreach (IPttModule m in _modules)
            {
                switch (m.ModuleId)
                {
                    case "BE":     m.SetEnabled(IsBeLicensed);      break;
                    case "TRIM":   m.SetEnabled(IsTrimLicensed);     break;
                    case "FLAT":   m.SetEnabled(IsFlattenLicensed);  break;
                    case "CANCEL": m.SetEnabled(IsCancelLicensed);   break;
                    case "COPY":   m.SetEnabled(IsCopierLicensed);   break;
                }
            }    // <- closing brace of IPttModules SetEnabled loop (L620)

            // B41: Site 3 -- initial display sync after panel wires up.
            if (_leaderAccount != null)
```

**Insert AFTER the closing brace of the IPttModules loop** (after L620, before the
`// B41: Site 3` comment):

```csharp
            _engine.Subscribe();
```

**Full context after insertion** (exact — copy verbatim):
```csharp
            foreach (IPttModule m in _modules)
            {
                switch (m.ModuleId)
                {
                    case "BE":     m.SetEnabled(IsBeLicensed);      break;
                    case "TRIM":   m.SetEnabled(IsTrimLicensed);     break;
                    case "FLAT":   m.SetEnabled(IsFlattenLicensed);  break;
                    case "CANCEL": m.SetEnabled(IsCancelLicensed);   break;
                    case "COPY":   m.SetEnabled(IsCopierLicensed);   break;
                }
            }
            _engine.Subscribe();

            // B41: Site 3 -- initial display sync after panel wires up.
            if (_leaderAccount != null)
```

**Rule**: All modules are enabled BEFORE the engine starts listening. This prevents
`OnOrderUpdate` from firing while modules are still initialising. Threading: `OnLoaded`
fires on the WPF Dispatcher thread. `Subscribe()` iterates `Account.All` which is
UI-thread-safe in NT8. No `Dispatcher.InvokeAsync` wrapper required.

---

## Change 3 — B44Tests.cs (NEW FILE)

**File**: `c:\WSGTA\universal-or-strategy\src\PropTraderTools\B44Tests.cs`

**Framework**: xUnit ONLY. No NUnit. No MSTest.

**Design**:
- Singleton access via `CopyEngine.Instance` (same as B42Tests.cs:241 pattern).
- Private field reset via reflection `FieldInfo.SetValue` (same as B42Tests.cs:304-306 pattern).
- `IDisposable.Dispose()` resets `_subscribed = false` — xUnit constructs a new class
  instance per `[Fact]` and calls `Dispose()` at teardown, guaranteeing isolation.
- Zero NT8 runtime dependency — no `Account.All` reference anywhere in test file.
  Tests assert `_subscribed` field state only (authoritative contract of the guard).

**Complete file content** (copy verbatim — no modifications):

```csharp
// B44Tests.cs
// Block: PTT-COPIER-B44
// Spec: DW-B44-subscribe-panel-01
// Tests: T_B44_01 through T_B44_04
// Framework: xUnit only (no NUnit, no MSTest)
// NT8-runtime-free: no Account.All reference, no event raising

using System;
using System.Reflection;
using Xunit;

namespace PropTraderTools.Tests
{
    public sealed class SubscribeIdempotencyTests : IDisposable
    {
        // Singleton access — identical to B42Tests.cs:241 pattern
        private readonly CopyEngine _engine = CopyEngine.Instance;

        // Reflection accessor for private _subscribed field (B42Tests.cs:304-306 pattern)
        private static readonly FieldInfo _subscribedField =
            typeof(CopyEngine).GetField(
                "_subscribed",
                BindingFlags.NonPublic | BindingFlags.Instance);

        private bool GetSubscribed() =>
            (bool)_subscribedField.GetValue(_engine);

        private void SetSubscribed(bool value) =>
            _subscribedField.SetValue(_engine, value);

        // IDisposable: xUnit calls Dispose() after each [Fact] — resets singleton state
        public void Dispose()
        {
            SetSubscribed(false);
        }

        // T_B44_01 — Subscribe is idempotent: calling twice leaves _subscribed=true, no double-registration
        // Spec: DW-B44-T1-02, DW-B44-T2-03
        [Fact]
        public void T_B44_01_Subscribe_CalledTwice_SubscribedFlagRemainsTrue()
        {
            // Arrange — Dispose() guarantees _subscribed=false at start
            Assert.False(GetSubscribed());

            // Act — call Subscribe() twice
            _engine.Subscribe();
            _engine.Subscribe();

            // Assert — flag is true; second call was a no-op (guard short-circuited)
            Assert.True(GetSubscribed());
        }

        // T_B44_02 — Unsubscribe when not subscribed does not throw and leaves flag false
        // Spec: DW-B44-T1-03, DW-B44-T2-04
        [Fact]
        public void T_B44_02_Unsubscribe_WhenNotSubscribed_DoesNotThrow()
        {
            // Arrange — Dispose() guarantees _subscribed=false at start
            Assert.False(GetSubscribed());

            // Act — call Unsubscribe() on cold engine (never subscribed)
            _engine.Unsubscribe();

            // Assert — no exception thrown; flag remains false
            Assert.False(GetSubscribed());
        }

        // T_B44_03 — Subscribe -> Unsubscribe -> Subscribe cycle resets flag correctly
        // Spec: DW-B44-T2-05
        [Fact]
        public void T_B44_03_ReSubscribe_AfterUnsubscribe_FlagIsTrue()
        {
            // Arrange — Dispose() guarantees _subscribed=false at start
            Assert.False(GetSubscribed());

            // Act + intermediate asserts through full cycle
            _engine.Subscribe();
            Assert.True(GetSubscribed());   // after first Subscribe

            _engine.Unsubscribe();
            Assert.False(GetSubscribed());  // after Unsubscribe

            _engine.Subscribe();
            Assert.True(GetSubscribed());   // after re-Subscribe

            // Final assert — flag is true after full cycle
            Assert.True(GetSubscribed());
        }

        // T_B44_04 — Fresh engine (no Subscribe called) has _subscribed=false
        // Spec: DW-B44-T2-06
        [Fact]
        public void T_B44_04_WithoutSubscribe_SubscribedFlag_IsFalse()
        {
            // Arrange — Dispose() guarantees _subscribed=false at start; no Subscribe called

            // Assert — engine starts in unsubscribed (deaf) state
            Assert.False(GetSubscribed());
        }
    }
}
```

---

## 7-Scan Checklist — T2

The engineer MUST run all 7 scans and confirm every result before marking T2 complete.

### FILE A — TradeCopierPanel.cs

| # | Scan | Command | Expected Result |
|---|------|---------|----------------|
| SCAN-01 | No lock() | `grep -n "lock\s*(" TradeCopierPanel.cs` | 0 matches |
| SCAN-02 | No async void | `grep -n "async void" TradeCopierPanel.cs` | 0 matches |
| SCAN-03 | No return null in new code | Manual review of inserted lines | 0 new return statements |
| SCAN-04 | Subscribe call in OnLoaded | `grep -n "_engine.Subscribe" TradeCopierPanel.cs` | >= 1 result, inside OnLoaded method |
| SCAN-05 | Unsubscribe call in Detach | `grep -n "_engine.Unsubscribe" TradeCopierPanel.cs` | >= 1 result; FIRST statement in Detach body |
| SCAN-06 | CYC delta = 0 | Run complexity_audit.py on TradeCopierPanel.cs | Detach and OnLoaded CYC unchanged |
| SCAN-07 | TradeCopierWindow unchanged | `git diff TradeCopierWindow.cs` | No modifications — 0 lines changed |

### FILE B — B44Tests.cs

| # | Scan | Command | Expected Result |
|---|------|---------|----------------|
| SCAN-01 | xUnit only | `grep -n "using Xunit" B44Tests.cs` | Present (>= 1 line) |
| SCAN-02 | No NUnit/MSTest | `grep -n "NUnit\|MSTest" B44Tests.cs` | 0 matches |
| SCAN-03 | Exactly 4 [Fact] tests | `grep -c "\[Fact\]" B44Tests.cs` | 4 |
| SCAN-04 | FieldInfo resolves non-null | T_B44_01 passes (FieldInfo.GetValue succeeds) | xUnit green |
| SCAN-05 | IDisposable.Dispose present | `grep -n "IDisposable\|Dispose" B44Tests.cs` | Both present |
| SCAN-06 | All 4 tests assert _subscribed | `grep -n "GetSubscribed\|Assert" B44Tests.cs` | >= 8 lines (2 asserts per test minimum) |
| SCAN-07 | NT8-runtime-free | `grep -n "Account.All" B44Tests.cs` | 0 matches |

## T2 BUILD PASS Criteria

- [ ] `dotnet build` exits 0, zero errors, zero new warnings
- [ ] `_engine.Unsubscribe();` appears as the FIRST statement inside `Detach()` body
- [ ] `_engine.Subscribe();` appears in `OnLoaded` after closing brace of IPttModules loop
- [ ] `git diff TradeCopierWindow.cs` shows 0 lines changed
- [ ] All 4 xUnit [Fact] tests in B44Tests.cs pass green
- [ ] SCAN-07 (FILE B): `grep "Account.All" B44Tests.cs` returns 0 — confirmed NT8-runtime-free
- [ ] SCAN-01 and SCAN-02 (FILE A): 0 `lock()` and 0 `async void` matches

---

---

# Cross-Ticket Notes

## TradeCopierWindow.cs — UNTOUCHED

`TradeCopierWindow.cs` L125 (`_engine.Subscribe()`) and L156 (`_engine.Unsubscribe()`)
remain exactly as-is. The idempotency guards in T1 make them safe alongside the Panel
calls. Any engineer who modifies TradeCopierWindow.cs in this block has violated scope.

## Execution Order

T1 MUST be complete and building before T2 begins. T2 depends on:
- `_engine.Subscribe()` existing with the guard (T1 Change 2)
- `_engine.Unsubscribe()` existing with the guard (T1 Change 3)

## Singleton State Warning

`CopyEngine.Instance` is a process-wide singleton in the NT8 runtime. Tests in
`B44Tests.cs` MUST use `Dispose()` to reset `_subscribed = false` after every `[Fact]`.
Failure to reset causes test order dependency and flaky results in CI. The `IDisposable`
pattern on the test class is mandatory, not optional.

---

# Return Value

TICKETS_COMPLETE
