# B26 Lane A+B — Architecture Plan

**Epic**: B26-LaneAB  
**Spec**: specs/002-trade-copier-spec.html § block-b26  
**Status**: REVIEW_PENDING  
**Requirement IDs**: PTT-COPIER-B26/DW-B26-01, PTT-COPIER-B26/DW-B26-02  
**Scope**: Lane A (DW-B26-01) + Lane B (DW-B26-02). Lane C and DEAD-B26 are OUT OF SCOPE.  
**[Fact] Baseline**: 131 | **Target**: 133 (+2 new tests)

---

## Rules Catalog Gate — PASS

| Rule | Check | Result |
|------|-------|--------|
| JS-021 (lock()) | No `lock(` introduced in 5 changed lines | PASS |
| JS-001 (throw in hot path) | No `throw` introduced | PASS |
| JS-002 (return null) | No `return null` introduced | PASS |
| JS-033 (async void) | No `async void` introduced | PASS |
| JS-036/037 (heap alloc) | No new array allocations | PASS |
| ASCII-only | All new identifiers: `accountName`, `acc` — ASCII | PASS |
| DateTime.Now | Not referenced | PASS |
| FontFamily | Not referenced | PASS |

---

## A. Exact Change Set — 5 Lines

### Change 1 — CopyEngine.cs L130 (DW-B26-02)

**File**: `src/PropTraderTools/CopyEngine.cs`  
**Line**: 130  
**Defect**: Event carries instrument name only; no account identity — causes all matching-instrument panels to receive the broadcast.

```
OLD:  internal event Action<string> PendingBeFired;
NEW:  internal event Action<string, string> PendingBeFired;
```

The delegate type changes from `Action<string>` (instrName only) to `Action<string, string>` (instrName, accountName). The `+=` and `-=` subscriber lines at TradeCopierPanel.cs L435 and L398 reference the method group `OnPendingBeFiredDispatch` by name — the compiler resolves the delegate type from the event declaration. Those lines do not require textual edits; they compile correctly after Change 4 updates the method signature.

---

### Change 2 — CopyEngine.cs L1422 (DW-B26-01)

**File**: `src/PropTraderTools/CopyEngine.cs`  
**Line**: 1422  
**Defect**: `OnTrailBeAccountUpdate` calls 2-arg `BreakEven(instr, newBuffer)` which routes through `AllAccounts(instrument)` → `FindRule` → `yield break` when no copy rule exists. Zero accounts iterated. `MoveStopToBreakEven` never called. Stop does not move.

```
OLD:      if (instr != null)
              BreakEven(instr, newBuffer);
NEW:      if (instr != null)
              BreakEven(acc, instr, newBuffer);
```

The 2-arg overload `BreakEven(Instrument, int)` at CopyEngine.cs ~L1192 is NOT deleted — it is also the copy-fan-out path called from TradeCopierWindow.cs L691 `OnRuleBreakEven`. This fix is at the call site only. Minimal: one argument added.

---

### Change 3 — CopyEngine.cs L1463 (DW-B26-02)

**File**: `src/PropTraderTools/CopyEngine.cs`  
**Line**: 1463  
**Defect**: `PendingBeFired` invoke passes only `instr?.FullName`. The account `acc` is in scope at this point (captured from `_pendingBeAccount` at method entry, used in the `BreakEven(acc, instr, buf)` call on L1461) but is not forwarded to subscribers.

```
OLD:      PendingBeFired?.Invoke(instr?.FullName ?? string.Empty);
NEW:      PendingBeFired?.Invoke(instr?.FullName ?? string.Empty, acc?.Name ?? string.Empty);
```

`acc` is a local variable in `OnPendingBeAccountUpdate`, captured before the account subscription is unregistered (L1456-1459). `string` is immutable — safe to pass across thread boundaries to the subscriber chain.

---

### Change 4 — TradeCopierPanel.cs L612 (DW-B26-02)

**File**: `src/PropTraderTools/TradeCopierPanel.cs`  
**Line**: 612 (signature) + body (lambda argument)  
**Defect**: Dispatcher signatures must match the new 2-arg event. The `accountName` string must be captured by the lambda closure and forwarded to the UI-thread callback.

```
OLD:  private void OnPendingBeFiredDispatch(string instr)
      {
          Dispatcher.InvokeAsync(() => OnBeConnected(instr));
      }

NEW:  private void OnPendingBeFiredDispatch(string instr, string accountName)
      {
          Dispatcher.InvokeAsync(() => OnBeConnected(instr, accountName));
      }
```

`accountName` is an immutable string captured by the lambda closure. `Dispatcher.InvokeAsync` marshals to the WPF/NT8 UI thread before `OnBeConnected` executes. Thread-safety: MAINTAINED.

---

### Change 5 — TradeCopierPanel.cs L852 (DW-B26-02)

**File**: `src/PropTraderTools/TradeCopierPanel.cs`  
**Line**: 852 (signature) + first guard in body  
**Defect**: `OnBeConnected(string instr)` has no account identity check. After the `_beBtn2 != null` guard, it unconditionally sets `_beState = BeState.Connected` and calls `UpdateBeVisuals(Connected)`. All panels subscribed to the same instrument execute this path — causing both panels to flip visual state simultaneously.

```
OLD:  private void OnBeConnected(string instr)
      {
          if (_beBtn2 == null) return;
          _beState = BeState.Connected;
          ...

NEW:  private void OnBeConnected(string instr, string accountName)
      {
          if (_beBtn2 == null) return;
          if (_leaderAccount == null || _leaderAccount.Name != accountName) return;
          // DW-B26-02: only update state for the panel whose account fired BE
          _beState = BeState.Connected;
          ...
```

The new guard is inserted **after** the existing `_beBtn2 == null` check and **before** the `_beState = BeState.Connected` assignment. The `_leaderAccount` field is set from the panel constructor and UI-thread callbacks — safe to read on the UI thread (this method runs via `Dispatcher.InvokeAsync`). The guard short-circuits for any panel whose leader account name does not match the account that fired the event.

---

## B. CYC Analysis

| Method | File | Current CYC | Change | New CYC | Limit | Status |
|--------|------|-------------|--------|---------|-------|--------|
| `OnTrailBeAccountUpdate` | CopyEngine.cs | 5 | Change 2 is inside existing `if (instr != null)` — no new branch | 5 | 8 | PASS |
| `OnPendingBeFiredDispatch` | TradeCopierPanel.cs | 1 | Signature change + lambda arg; no new branch | 1 | 8 | PASS |
| `OnBeConnected` | TradeCopierPanel.cs | 3 | New guard: `if (_leaderAccount == null \|\| _leaderAccount.Name != accountName)` adds 1–2 branches depending on `&&` counting | 4–5 | 8 | PASS |

**OnBeConnected detail**: Current body has 3 decision points (`_beBtn2 == null`, `_instrument != null`, `_leaderAccount != null`). The new guard `_leaderAccount == null || _leaderAccount.Name != accountName` adds 1 branch for the `||` short-circuit plus potentially 1 for the condition itself. Worst-case CYC = 5. This is well within the Jane Street limit of 8.

---

## C. Event Subscriber Impact

| File | Location | Type | Action Required |
|------|----------|------|----------------|
| `TradeCopierPanel.cs` | L435 | `_engine.PendingBeFired += OnPendingBeFiredDispatch` | **None** — method group ref resolves from new delegate type automatically |
| `TradeCopierPanel.cs` | L398 | `_engine.PendingBeFired -= OnPendingBeFiredDispatch` | **None** — same as above |
| `TradeCopierWindow.cs` | — | Zero subscriptions (confirmed by orchestrator grep) | **None** |

Only TradeCopierPanel.cs subscribes to `PendingBeFired`. The 4-line fix (Changes 1, 3, 4, 5) is the complete fix set. No additional subscriber files need updating.

---

## D. [Fact] Tests

**Baseline**: 131 confirmed-live tests.  
**Target**: 133 (+2 new tests, per spec PTT-COPIER-B26 architecture decisions).

### Test 1: `T_B26_01_TrailBe_WithNoRule_StillMovesStop`

**Covers**: DW-B26-01  
**File**: `src/PropTraderTools.Tests/CopyEngineTests.cs` (or existing test project)

**Scenario**:
1. Create a `CopyEngine` instance with zero copy rules registered (simulating COPY OFF — no follower accounts).
2. Arm trail-BE on a stub `Account` (`_trailBeAccount`) and a stub `Instrument` (`_trailBeInstrument`).
3. Set `_trailBeArmed = true` (or use `ArmTrailBe` if it sets the flag).
4. Fire `OnTrailBeAccountUpdate` by raising `AccountItemUpdate` on the stub account with:
   - `AccountItem = AccountItem.UnrealizedProfitLoss`
   - A `Value` higher than the current `_trailBeLastPnl` (so the HWM advance branch executes).
5. **Assert**: `MoveStopToBreakEven` was called with the leader account (confirmed via a `StatusUpdate` event emission that carries the account name, or via a mock/stub capturing the call).

**What this verifies**: After the fix at L1422, `BreakEven(acc, instr, newBuffer)` is called (3-arg). The 3-arg overload does not route through `AllAccounts` — it calls `MoveStopToBreakEven(acc, instr, buf)` directly for the leader account. The test fails on the pre-fix code (stop never moves) and passes after the fix.

---

### Test 2: `T_B26_02_PendingBeFired_CarriesAccountName`

**Covers**: DW-B26-02  
**File**: `src/PropTraderTools.Tests/CopyEngineTests.cs`

**Scenario**:
1. Create a `CopyEngine` instance.
2. Arm pending-BE on a stub `Account` (`acc`) and stub `Instrument` (`instr`).
3. Subscribe to `PendingBeFired` with a local handler: `(instrName, accountName) => { ... }`.
4. Fire `OnPendingBeAccountUpdate` by raising `AccountItemUpdate` on the stub account with a value that triggers the BE condition (i.e. `acc.Get(AccountItem.UnrealizedProfitLoss)` >= the threshold that fires the pending BE).
5. **Assert**: The `PendingBeFired` event was raised with:
   - First argument = `instr.FullName` (instrument name string)
   - Second argument = `acc.Name` (account name string, non-empty)

**What this verifies**: After the fix at L1463, the invoke passes `acc?.Name ?? string.Empty` as the second argument. The test fails on pre-fix code (second argument is missing — compilation error or empty string) and passes after the fix. Also verifies that `acc.Name` is non-empty, confirming the account is properly in scope at the call site.

---

## E. NT8 Compiler Checklist

| Rule | Description | Check | Result |
|------|-------------|-------|--------|
| NT8-001 | No `{ get; init; }` | Not introduced | PASS |
| NT8-002 | No `abstract record` / `sealed record` | Not introduced | PASS |
| NT8-003 | No `volatile double` | Not introduced | PASS |
| NT8-004 | No `ImmutableDictionary` / `System.Collections.Immutable` | Not introduced | PASS |
| NT8-007 | No `CreateOrder` arg 12 as `string` | Not touched | PASS |
| Custom | No `async void` non-event-handler | Not introduced | PASS |
| Custom | No `lock()` | Not introduced | PASS |
| Custom | No `DateTime.Now` | Not introduced | PASS |
| Custom | ASCII-only identifiers | `accountName`, `instr`, `acc` — all ASCII | PASS |
| Custom | No `FontFamily` | Not referenced | PASS |
| Custom | No hardcoded hex colors | Not referenced | PASS |

---

## F. Dead Code Check

| Symbol | File | Location | Status |
|--------|------|----------|--------|
| `BreakEven(Instrument, int)` — 2-arg overload | `CopyEngine.cs` | ~L1192 | **NOT DEAD** — called from `TradeCopierWindow.cs` L691 `OnRuleBreakEven`. After the DW-B26-01 fix (L1422 uses 3-arg), the 2-arg overload is still live via TradeCopierWindow. **Do not delete.** |

The DW-B26-01 fix removes the only *incorrect* use of the 2-arg overload from `OnTrailBeAccountUpdate`. The 2-arg overload's legitimate use on the copy-fan-out path from TradeCopierWindow remains unaffected.

---

## G. Component Summary

```
Lane A — CopyEngine.cs (1 line)
  OnTrailBeAccountUpdate [L1403-1423, CYC=5]
    └─ L1422: BreakEven(instr, newBuffer) → BreakEven(acc, instr, newBuffer)

Lane B — CopyEngine.cs (2 lines) + TradeCopierPanel.cs (2 lines)
  CopyEngine.cs
    L130:  event Action<string> → Action<string, string>
    L1463: PendingBeFired?.Invoke(instr?.FullName ?? "")
           → PendingBeFired?.Invoke(instr?.FullName ?? "", acc?.Name ?? "")

  TradeCopierPanel.cs
    L612:  OnPendingBeFiredDispatch(string instr)
           → OnPendingBeFiredDispatch(string instr, string accountName)
           body: OnBeConnected(instr) → OnBeConnected(instr, accountName)
    L852:  OnBeConnected(string instr) → OnBeConnected(string instr, string accountName)
           + guard: if (_leaderAccount == null || _leaderAccount.Name != accountName) return;

Tests (new)
  T_B26_01_TrailBe_WithNoRule_StillMovesStop  — DW-B26-01
  T_B26_02_PendingBeFired_CarriesAccountName   — DW-B26-02

No lock() introduced. No async void introduced. No FontFamily. No hex color.
No DateTime.Now. ASCII-only. All CYC <= 8.
```

---

*Plan written by ptt-architect. Awaiting ptt-plan-reviewer for REVIEW_PASS before ticket generation.*
