# B26 Lane A+B — Tickets

**Epic**: B26-LaneAB  
**Phase**: 3 (Ticket Generation)  
**Architecture Plan**: `docs/brain/B26-LaneAB/02-architecture-plan.md` (REVIEW_PASS)  
**Spec**: `specs/002-trade-copier-spec.html` § block-b26  
**Wave Workspace**: `c:\WSGTA\universal-or-strategy\`  
**[Fact] Baseline**: 131 | **Target after T1**: 133  
**Ticket Count**: 2 (T1 → T2 sequential — T2 depends on T1)

---

## TICKET B26-AB-T1

**ID**: B26-AB-T1  
**Title**: DW-B26-01 wrong BreakEven overload + DW-B26-02 event signature (CopyEngine.cs)  
**Spec Requirement IDs**: PTT-COPIER-B26/DW-B26-01, PTT-COPIER-B26/DW-B26-02 (engine side)  
**File**: `src/PropTraderTools/CopyEngine.cs` (source changes)  
**Test File**: `src/PropTraderTools/CopyEngineTests.cs` (new [Fact] tests)  
**Dependency**: None — implement first.

---

### Exact Changes — CopyEngine.cs

#### Change 1 — L130 (DW-B26-02 event declaration)

**What**: Widen `PendingBeFired` event delegate from `Action<string>` to `Action<string, string>` so that `accountName` is carried alongside the instrument name. Subscriber method groups (`OnPendingBeFiredDispatch`) at `TradeCopierPanel.cs` L435 and L398 do NOT require textual edits — the compiler resolves the delegate type from the event declaration automatically after Change 4 updates the method signature.

```
OLD:  internal event Action<string> PendingBeFired;
NEW:  internal event Action<string, string> PendingBeFired;
```

#### Change 2 — L1422 (DW-B26-01 call-site fix)

**What**: Inside `OnTrailBeAccountUpdate`, within the existing `if (instr != null)` block, replace the 2-arg `BreakEven` call with the 3-arg overload that passes `acc` explicitly. The 2-arg overload (`BreakEven(Instrument, int)` at ~L1192) is **not** deleted — it remains live on the copy-fan-out path from `TradeCopierWindow.cs` L691.

```
OLD:      if (instr != null)
              BreakEven(instr, newBuffer);
NEW:      if (instr != null)
              BreakEven(acc, instr, newBuffer);
```

> CYC impact: Change 2 is inside an **existing** `if (instr != null)` branch — no new decision point. `OnTrailBeAccountUpdate` CYC stays 5.

#### Change 3 — L1463 (DW-B26-02 invoke fix)

**What**: Inside `OnPendingBeAccountUpdate`, update the `PendingBeFired?.Invoke(...)` call to pass `acc?.Name ?? string.Empty` as the second argument. `acc` is a local variable already in scope at this line (captured from `_pendingBeAccount` at method entry, used earlier in the same method at the `BreakEven(acc, instr, buf)` call). `string` is immutable — safe to pass across thread boundaries.

```
OLD:      PendingBeFired?.Invoke(instr?.FullName ?? string.Empty);
NEW:      PendingBeFired?.Invoke(instr?.FullName ?? string.Empty, acc?.Name ?? string.Empty);
```

> CYC impact: Change 3 is a call-site argument change — no new decision point. `OnPendingBeAccountUpdate` CYC stays 8.

---

### New [Fact] Tests — CopyEngineTests.cs

Both tests must be added to `src/PropTraderTools/CopyEngineTests.cs`. The [Fact] count increases from 131 to 133.

#### Test 1: `T_B26_01_TrailBe_WithNoRule_StillMovesStop`

**Covers**: DW-B26-01  
**Purpose**: Verify that after the Change 2 fix, `BreakEven(acc, instr, newBuffer)` (3-arg) is called from `OnTrailBeAccountUpdate`, routing directly to `MoveStopToBreakEven` for the leader account — even when zero copy rules are registered. The 2-arg overload routes through `AllAccounts(instrument)` → `FindRule` → `yield break`, so pre-fix code calls `MoveStopToBreakEven` zero times.

**Scenario**:
1. Create a `CopyEngine` instance with **zero** copy rules registered (simulating COPY OFF).
2. Arm trail-BE: set `_trailBeAccount` to a stub `Account` and `_trailBeInstrument` to a stub `Instrument`.
3. Set `_trailBeArmed = true` (or call `ArmTrailBe` if that method sets the flag).
4. Fire `OnTrailBeAccountUpdate` by raising `AccountItemUpdate` on the stub account with:
   - `AccountItem = AccountItem.UnrealizedProfitLoss`
   - A `Value` higher than the current `_trailBeLastPnl` (so the HWM advance branch executes).
5. **Assert**: `StatusUpdate` was raised and contains the leader account name — confirming `MoveStopToBreakEven` was called via the 3-arg `BreakEven(acc, instr, newBuffer)` path.

**Failure contract**: Test **fails** on pre-fix code (stop never moves, no `StatusUpdate`). Test **passes** after Change 2.

```csharp
[Fact]
public void T_B26_01_TrailBe_WithNoRule_StillMovesStop()
{
    // arrange: engine with zero copy rules
    // arm trail-BE on stub account + instrument
    // set _trailBeLastPnl < trigger value
    // subscribe to StatusUpdate, capture emitted strings

    // act: raise AccountItemUpdate(UnrealizedProfitLoss, highValue) on stub account

    // assert: StatusUpdate was raised; emitted string contains stub account name
    //         (proves MoveStopToBreakEven reached the 3-arg BreakEven path)
}
```

#### Test 2: `T_B26_02_PendingBeFired_CarriesAccountName`

**Covers**: DW-B26-02  
**Purpose**: Verify that after Change 3, `PendingBeFired` carries `acc.Name` as its second argument. Pre-fix code either fails to compile (delegate mismatch) or passes an empty/wrong second argument.

**Scenario**:
1. Create a `CopyEngine` instance.
2. Arm pending-BE on a stub `Account` (`acc`) and stub `Instrument` (`instr`).
3. Subscribe to `PendingBeFired` with a local handler: `(instrName, accountName) => { /* capture both */ }`.
4. Fire `OnPendingBeAccountUpdate` by raising `AccountItemUpdate` on the stub account with a value that satisfies the pending-BE trigger condition (unrealized profit >= threshold that fires the pending BE).
5. **Assert**:
   - `PendingBeFired` was raised (handler invoked).
   - Second argument (`accountName`) == `acc.Name` (non-empty string).

**Failure contract**: Test **fails** on pre-fix code (delegate type mismatch = compile error, or second arg is absent/empty). Test **passes** after Changes 1 + 3.

```csharp
[Fact]
public void T_B26_02_PendingBeFired_CarriesAccountName()
{
    // arrange: engine, stub account, stub instrument
    // arm pending-BE on stub account + instrument
    // subscribe: (instrName, accountName) => capture both

    // act: raise AccountItemUpdate to trigger pending-BE condition

    // assert: event was fired; second argument == stub account name (non-empty)
}
```

---

### Acceptance Criteria — T1

| # | Criterion | Verification Command |
|---|-----------|----------------------|
| AC-1 | [Fact] count increases from 131 to 133 | `Select-String -Pattern "\[Fact\]" src/PropTraderTools/CopyEngineTests.cs \| Measure-Object` → **133** |
| AC-2 | `OnTrailBeAccountUpdate` CYC stays 5 | Complexity audit: Change 2 is inside existing branch, no new decision point |
| AC-3 | `OnPendingBeAccountUpdate` CYC stays 8 | Complexity audit: Change 3 is a call-site argument change, no new branch |
| AC-4 | F5 compile clean in NinjaTrader 8 | Zero errors in NT8 compiler output |
| AC-5 | No NT8 banned patterns introduced | SCAN-01 through SCAN-05 all pass (see checklist) |

---

### 7-Scan Checklist — T1 (Engineer Contract)

| Scan | Command | Required Result |
|------|---------|----------------|
| **SCAN-01** lock() | `grep -n "lock(" src/PropTraderTools/CopyEngine.cs` | **0 results** |
| **SCAN-02** async void | `grep -n "async void " src/PropTraderTools/CopyEngine.cs` | **0 results** |
| **SCAN-03** return null | `grep -n "return null;" src/PropTraderTools/CopyEngine.cs` | Count **same as baseline** (no new occurrences) |
| **SCAN-04** throw new | `grep -n "throw new " src/PropTraderTools/CopyEngine.cs` | Count **same as baseline** (no new occurrences) |
| **SCAN-05** PTT- prefix | `grep -n "CreateOrder" src/PropTraderTools/CopyEngine.cs` | All `CreateOrder` calls must use **"PTT-"** prefixed order names |
| **SCAN-06** [Fact] count | `Select-String -Pattern "\[Fact\]" src/PropTraderTools/CopyEngineTests.cs \| Measure-Object` | **133** |
| **SCAN-07** CYC | Complexity audit on `OnTrailBeAccountUpdate` and `OnPendingBeAccountUpdate` | `OnTrailBeAccountUpdate` CYC = **5**, `OnPendingBeAccountUpdate` CYC = **8** (both unchanged) |

---

---

## TICKET B26-AB-T2

**ID**: B26-AB-T2  
**Title**: DW-B26-02 OnPendingBeFiredDispatch + OnBeConnected account guard (TradeCopierPanel.cs)  
**Spec Requirement IDs**: PTT-COPIER-B26/DW-B26-02 (panel side)  
**File**: `src/PropTraderTools/TradeCopierPanel.cs` ONLY  
**Test File**: None — UI dispatch path verified by F5 compile + visual integration test.  
**Dependency**: **T1 must be complete first.** The `PendingBeFired` event must already be declared as `Action<string, string>` (Change 1 from T1) before the compiler will accept the updated `OnPendingBeFiredDispatch` signature in this ticket.

---

### Exact Changes — TradeCopierPanel.cs

#### Change 4 — L612 (OnPendingBeFiredDispatch — full method replacement)

**What**: Update `OnPendingBeFiredDispatch` to accept the new 2-arg event signature. The `accountName` string is captured by the lambda closure and forwarded to `OnBeConnected` on the UI thread via `Dispatcher.InvokeAsync`. Thread-safety is maintained: `accountName` is an immutable `string` captured by value.

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

> CYC impact: Signature change + lambda argument update — no new decision point. `OnPendingBeFiredDispatch` CYC stays 1.

> Subscriber wiring: `_engine.PendingBeFired += OnPendingBeFiredDispatch` (L435) and `_engine.PendingBeFired -= OnPendingBeFiredDispatch` (L398) reference the method group by name. After Change 4 updates the signature, the compiler resolves the method group to the new `Action<string, string>` delegate type automatically. **No textual changes required at L435 or L398.**

#### Change 5 — L852 (OnBeConnected — signature + account guard)

**What**: Update `OnBeConnected` to accept `accountName` as a second parameter. Insert a new guard **after** the existing `_beBtn2 == null` check and **before** `_beState = BeState.Connected`. The guard short-circuits for any panel whose `_leaderAccount.Name` does not match the `accountName` that fired the event — preventing all subscribed panels from simultaneously flipping visual state.

The `_leaderAccount` field is set from the panel constructor and UI-thread callbacks. `OnBeConnected` always runs on the WPF/NT8 UI thread (marshaled by `Dispatcher.InvokeAsync` in Change 4). Reading `_leaderAccount.Name` on the UI thread is safe.

```
OLD:  private void OnBeConnected(string instr)
      {
          if (_beBtn2 == null) return;
          _beState = BeState.Connected;

NEW:  private void OnBeConnected(string instr, string accountName)
      {
          if (_beBtn2 == null) return;
          if (_leaderAccount == null || _leaderAccount.Name != accountName) return;
          // DW-B26-02: only update state for the panel whose account fired BE
          _beState = BeState.Connected;
```

> The `...` trailing body is unchanged. Only the signature line and the two lines after the `_beBtn2` guard are modified. The closing brace and all subsequent statements remain in place.

> CYC impact: `OnBeConnected` current CYC = 3. The new guard `if (_leaderAccount == null || _leaderAccount.Name != accountName)` adds 1 branch for the `||` short-circuit operator plus the condition itself = worst-case +2 branches. New CYC ≤ 5. Well within JS limit of 8.

---

### Acceptance Criteria — T2

| # | Criterion | Verification Command |
|---|-----------|----------------------|
| AC-1 | F5 compile clean in NinjaTrader 8 | Zero "no overload takes 1 argument" errors on `OnBeConnected` or `OnPendingBeFiredDispatch` |
| AC-2 | No 1-arg forms remain | `grep -n "OnBeConnected\|OnPendingBeFiredDispatch" src/PropTraderTools/TradeCopierPanel.cs` — all occurrences show 2-arg signatures; zero 1-arg call sites remain |
| AC-3 | Mandatory comment present | `grep -n "DW-B26-02: only update state for the panel whose account fired BE" src/PropTraderTools/TradeCopierPanel.cs` → **1 result** |
| AC-4 | `OnBeConnected` CYC ≤ 5 | Complexity audit: new guard adds ≤ 2 branches; CYC was 3, now ≤ 5 ≤ 8 |
| AC-5 | `OnPendingBeFiredDispatch` CYC = 1 | Complexity audit: no new decision point introduced |

---

### 7-Scan Checklist — T2 (Engineer Contract)

| Scan | Command | Required Result |
|------|---------|----------------|
| **SCAN-01** lock() | `grep -n "lock(" src/PropTraderTools/TradeCopierPanel.cs` | **0 results** |
| **SCAN-02** async void | `grep -n "async void " src/PropTraderTools/TradeCopierPanel.cs` | **0 results** |
| **SCAN-03** return null | `grep -n "return null;" src/PropTraderTools/TradeCopierPanel.cs` | Count **same as baseline** (no new occurrences) |
| **SCAN-04** throw new | `grep -n "throw new " src/PropTraderTools/TradeCopierPanel.cs` | Count **same as baseline** (no new occurrences) |
| **SCAN-05** PTT- prefix | `grep -n "CreateOrder" src/PropTraderTools/TradeCopierPanel.cs` | All `CreateOrder` calls must use **"PTT-"** prefixed order names |
| **SCAN-06** signature clean | `grep -n "OnBeConnected\|OnPendingBeFiredDispatch" src/PropTraderTools/TradeCopierPanel.cs` | **No 1-arg forms remain** — all occurrences are 2-arg |
| **SCAN-07** CYC | Complexity audit on `OnBeConnected` and `OnPendingBeFiredDispatch` | `OnBeConnected` CYC ≤ **5**, `OnPendingBeFiredDispatch` CYC = **1** |

---

## Execution Order

```
T1 (CopyEngine.cs + CopyEngineTests.cs)
  Change 1: L130  — event Action<string> → Action<string, string>
  Change 2: L1422 — BreakEven(instr, newBuffer) → BreakEven(acc, instr, newBuffer)
  Change 3: L1463 — PendingBeFired?.Invoke(instr?) → PendingBeFired?.Invoke(instr?, acc?)
  Tests:    T_B26_01_TrailBe_WithNoRule_StillMovesStop
            T_B26_02_PendingBeFired_CarriesAccountName
  Verify:   [Fact] count = 133; F5 green; SCAN-01..07 pass

T2 (TradeCopierPanel.cs) — after T1 is complete
  Change 4: L612  — OnPendingBeFiredDispatch 1-arg → 2-arg (full method replacement)
  Change 5: L852  — OnBeConnected 1-arg + account guard insertion
  Verify:   F5 green; no 1-arg forms; comment present; SCAN-01..07 pass
```

---

## Rules Catalog Gate — PASS (all 5 changes)

| Rule | Check | Result |
|------|-------|--------|
| JS-021 (lock()) | No `lock(` in any of the 5 changed lines | PASS |
| JS-001 (throw in hot path) | No `throw` introduced | PASS |
| JS-002 (return null) | No `return null` introduced | PASS |
| JS-033 (async void) | No `async void` introduced | PASS |
| JS-036/037 (heap alloc) | No new array allocations | PASS |
| ASCII-only | All new identifiers: `accountName`, `acc`, `instr` — ASCII | PASS |
| NT8-001 | No `{ get; init; }` introduced | PASS |
| NT8-003 | No `volatile double` introduced | PASS |
| DateTime.Now | Not referenced | PASS |
| FontFamily | Not referenced | PASS |
| Hex colors | Not referenced | PASS |

---

*Tickets written by ptt-architect from REVIEW_PASS plan. Hand off to ptt-engineer for T1, then T2.*
