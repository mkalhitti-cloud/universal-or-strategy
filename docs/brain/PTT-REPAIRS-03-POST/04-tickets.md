# PTT-REPAIRS-03-POST Tickets

**Epic:** PTT-REPAIRS-03-POST
**Phase:** 4 (Ticket Generation)
**Author:** ptt-architect
**Date:** 2026-09-06
**Plan reviewed:** `docs/brain/PTT-REPAIRS-03-POST/02-architecture-plan.md`
**Plan verdict:** REVIEW_PASS (02-plan-review.md, Section F)
**Source of truth:** `docs/brain/PTT-REPAIRS-03/direct-edits.md`

---

## TICKET-1: BUG-C — gate5 instrKey false-block (DW-B142-MGC-03)

**DW Reference:** DW-B142-MGC-03
**Spec requirements satisfied:** direct-edits.md EDIT 1–5 (BUG-C), plan Sections 2a–2g, DW-REPAIRS-03-POST-02
**Epic:** PTT-REPAIRS-03-POST
**Scope:** VERIFICATION-ONLY (code already in source) + ONE new [Fact] test
**Files touched:**
- Verify: `src/PropTraderTools/CopyEngine.cs`
- Add test: `src/PropTraderTools/CopyEngineTests.cs`

---

### 1.1 Spec Summary

BUG-C: NT8 does not guarantee `OrderState.Cancelled` delivery before the next order on the same
instrument+direction reaches `Accepted`. The original `ContainsKey`-only gate5 check blocked any
order sharing an instrKey with a previously dispatched order, regardless of orderId. Fixed by:
- Changing `_liveEntryInstruments` value type from `byte` to `string` (stores orderId)
- Changing the gate predicate to `TryGetValue + equality` (orderId-scoped check)
- Changing `TryAdd` to indexer overwrite in `SetLiveEntryDispatched` (always stores latest orderId)
- Adding value-guarded `TryRemove` in `EvictDedup` both terminal branches (prevents stale cancel
  from wiping a newer order's guard)

Deferred item DW-REPAIRS-03-POST-02 (P1, OPEN): the regression test for this fix is missing.
This ticket adds it.

---

### 1.2 Methods to Verify

All methods listed below are **already in source**. The engineer must read each method body and
confirm it matches the specification below. If drift is found, the engineer must report it and
NOT proceed to the test addition until the drift is resolved.

#### 1.2.1 `_liveEntryInstruments` field
**File:** `src/PropTraderTools/CopyEngine.cs`
**Lines:** 203–204

**Required declaration:**
```csharp
private readonly ConcurrentDictionary<string, string> _liveEntryInstruments =
    new ConcurrentDictionary<string, string>();
```

**Verify:**
- Type is `ConcurrentDictionary<string, string>` (not `byte`)
- Field is `private readonly`
- Comment on lines 195–202 documents the orderId-scoped semantics and PTT-REPAIRS-03-POST fix

#### 1.2.2 `IsLiveEntryBlocked_Check`
**File:** `src/PropTraderTools/CopyEngine.cs`
**Lines:** 5802–5811
**Signature:** `private bool IsLiveEntryBlocked_Check(string instrKey, string orderId, double limitPrice)`

**Required predicate (line 5804):**
```csharp
if (_liveEntryInstruments.TryGetValue(instrKey, out var liveOrderId) && liveOrderId == orderId)
    return true;
```

**Verify:**
- No `ContainsKey(instrKey)` call in this method
- Predicate uses `TryGetValue` + equality check (not `ContainsKey`)
- CYC = 4 branches: `TryGetValue+equality` (2 nodes, short-circuit AND), `IsDedup` (1), `ContainsKey(_entryDispatchedOrders)` (1)
- JS-021: no `lock(` statement
- JS-001: no `throw` statement
- JS-002: method returns only `true`/`false`
- ASCII-only string literals

#### 1.2.3 `SetLiveEntryDispatched`
**File:** `src/PropTraderTools/CopyEngine.cs`
**Lines:** 5821–5826
**Signature:** `private void SetLiveEntryDispatched(string instrKey, string orderId)`

**Required store (line 5823):**
```csharp
_liveEntryInstruments[instrKey] = orderId;
```

**Verify:**
- No `TryAdd(instrKey, 0)` or `TryAdd(instrKey, ...)` call in this method
- Uses indexer overwrite `[instrKey] = orderId`
- CYC = 1 (no decision branches)
- JS-021: no `lock(` statement

#### 1.2.4 `EvictDedup` Cancelled branch
**File:** `src/PropTraderTools/CopyEngine.cs`
**Lines:** 5866–5872

**Required pattern:**
```csharp
if (_entryInstrKeyByOrderId.TryRemove(orderId, out var cancelledInstrKey))
{
    string storedId;
    if (_liveEntryInstruments.TryGetValue(cancelledInstrKey, out storedId)
        && storedId == orderId)
        _liveEntryInstruments.TryRemove(cancelledInstrKey, out _);
}
```

**Verify:**
- Outer `if`: `_entryInstrKeyByOrderId.TryRemove(orderId, out var cancelledInstrKey)` — produces `cancelledInstrKey`
- Inner guard: `TryGetValue(cancelledInstrKey, out storedId) && storedId == orderId` — value check before remove
- `TryRemove` only executes if stored orderId matches the cancelling orderId
- No unconditional `TryRemove` on `_liveEntryInstruments` at this site

#### 1.2.5 `EvictDedup` Filled branch
**File:** `src/PropTraderTools/CopyEngine.cs`
**Lines:** 5881–5887

**Required pattern (mirrors Cancelled branch):**
```csharp
if (_entryInstrKeyByOrderId.TryRemove(orderId, out var filledInstrKey))
{
    string storedId;
    if (_liveEntryInstruments.TryGetValue(filledInstrKey, out storedId)
        && storedId == orderId)
        _liveEntryInstruments.TryRemove(filledInstrKey, out _);
}
```

**Verify:**
- Same value-guard pattern as Cancelled branch (storedId == orderId before TryRemove)
- No unconditional `TryRemove` on `_liveEntryInstruments` at this site
- Combined `EvictDedup` CYC = 6 (confirmed in source comment at line 5843)

---

### 1.3 Test to Add

This is the ONLY new code required for TICKET-1.

**File:** `src/PropTraderTools/CopyEngineTests.cs`
**Insert location:** After line 7922 (end of `DispatchCopy_DoesNotSetPhantomInstrKey_WhenAllFollowersSkipped`
body) and before the closing `}` of the test class at line 7923.
**Class:** The existing test class (same file — do not create a new class)
**Method name:** `IsLiveEntryBlocked_DifferentOrderId_SameInstrKey_NotBlocked`

**Full method body to add:**
```csharp
        // PTT-REPAIRS-03-POST BUG-C regression test -- DW-REPAIRS-03-POST-02.
        // Verifies that a new orderId with the same instrKey as an already-dispatched order
        // is NOT blocked at gate5. Simulates NT8 late-cancel scenario: orderId-A was dispatched
        // (instrKey set in _liveEntryInstruments) but OrderState.Cancelled has not yet arrived
        // when orderId-B reaches Accepted on the same instrument+direction.
        // Pre-fix behaviour (ContainsKey-only): blocked == true (regression).
        // Post-fix behaviour (TryGetValue+equality): blocked == false (correct).
        [Fact]
        public void IsLiveEntryBlocked_DifferentOrderId_SameInstrKey_NotBlocked()
        {
            // Arrange: clear any residual state for this instrKey
            _engine.ClearLiveEntryForInstrument_ForTest("MES SEP26");

            // Arrange: prime _liveEntryInstruments with orderId-A for instrKey
            // IsLiveEntryBlocked_ForTest: if not blocked, calls SetLiveEntryDispatched internally.
            // This replicates DispatchCopy check+commit for orderId-A.
            bool firstResult = _engine.IsLiveEntryBlocked_ForTest("MES SEP26|Sell", "orderId-A", 0.0);
            // Verify the arrange step: orderId-A must pass (instrKey was clean)
            Assert.False(firstResult);
            // _liveEntryInstruments["MES SEP26|Sell"] is now "orderId-A"

            // Act: orderId-B arrives on the same instrKey (NT8 Cancelled for orderId-A not yet delivered)
            bool blocked = _engine.IsLiveEntryBlocked_ForTest("MES SEP26|Sell", "orderId-B", 0.0);

            // Assert: different orderId on same instrKey must NOT be blocked (BUG-C fix)
            // Simulates NT8 late-cancel scenario: orderId-A set but not yet evicted
            // when orderId-B arrives on same instrKey. New order must pass gate5.
            Assert.False(blocked);
        }
```

**What the test asserts:**
1. `firstResult == false`: orderId-A passes gate5 on a clean instrKey, and `_liveEntryInstruments["MES SEP26|Sell"]` is set to `"orderId-A"` via `IsLiveEntryBlocked_ForTest`'s internal `SetLiveEntryDispatched` call.
2. `blocked == false`: orderId-B, arriving on the same instrKey while orderId-A's guard is still present, is NOT blocked — because `TryGetValue("MES SEP26|Sell") == "orderId-A"` which is `!= "orderId-B"`, so the gate predicate evaluates to `false`.

**Pre-fix regression proof:** Under the original `ContainsKey`-only check, `blocked` would be `true` (any orderId on an occupied instrKey was blocked indefinitely).

**Shim dependency:** Uses `ClearLiveEntryForInstrument_ForTest` (already at `CopyEngine.cs:4357`) and `IsLiveEntryBlocked_ForTest` (already at `CopyEngine.cs:4342`). No new shim required.

---

### 1.4 Method Signatures (complete)

| Symbol | Kind | File | Lines | Signature |
|--------|------|------|-------|-----------|
| `_liveEntryInstruments` | field | CopyEngine.cs | 203–204 | `private readonly ConcurrentDictionary<string, string>` |
| `IsLiveEntryBlocked_Check` | method | CopyEngine.cs | 5802–5811 | `private bool IsLiveEntryBlocked_Check(string instrKey, string orderId, double limitPrice)` |
| `SetLiveEntryDispatched` | method | CopyEngine.cs | 5821–5826 | `private void SetLiveEntryDispatched(string instrKey, string orderId)` |
| `EvictDedup` (Cancelled) | method branch | CopyEngine.cs | 5866–5872 | part of `internal void EvictDedup(string orderId, OrderState state)` |
| `EvictDedup` (Filled) | method branch | CopyEngine.cs | 5881–5887 | part of `internal void EvictDedup(string orderId, OrderState state)` |
| `IsLiveEntryBlocked_DifferentOrderId_SameInstrKey_NotBlocked` | test | CopyEngineTests.cs | (insert after 7922) | `public void IsLiveEntryBlocked_DifferentOrderId_SameInstrKey_NotBlocked()` |

---

### 1.5 Jane Street Rule Constraints

| Method | JS-001 no throw | JS-002 bool safety | JS-021 no lock | JS-013 CYC ≤ 8 | ASCII-only |
|--------|-----------------|--------------------|----------------|----------------|------------|
| `_liveEntryInstruments` field | N/A | N/A | PASS (ConcurrentDictionary) | N/A | PASS |
| `IsLiveEntryBlocked_Check` | PASS | PASS | PASS | PASS (CYC=4) | PASS |
| `SetLiveEntryDispatched` | PASS | N/A (void) | PASS | PASS (CYC=1) | PASS |
| `EvictDedup` (both branches) | PASS | N/A (void) | PASS | PASS (CYC=6) | PASS |
| New test method | N/A | N/A | N/A | N/A | PASS |

---

### 1.6 Acceptance Criteria

All criteria must be verifiable by inspection or test run. No criterion requires rebuilding the
logic — only confirming what is already in source plus the new test.

| # | Criterion | How to verify |
|---|-----------|---------------|
| AC-1 | `_liveEntryInstruments` is `ConcurrentDictionary<string,string>` (not `byte`) | Read `CopyEngine.cs:203` |
| AC-2 | `IsLiveEntryBlocked_Check` uses `TryGetValue + equality`, NOT `ContainsKey` | Read `CopyEngine.cs:5804` — no `ContainsKey(instrKey)` present |
| AC-3 | `SetLiveEntryDispatched` uses indexer assignment `[instrKey]=orderId`, NOT `TryAdd` | Read `CopyEngine.cs:5823` — no `TryAdd` call on `_liveEntryInstruments` |
| AC-4 | `EvictDedup` Cancelled branch: `storedId==orderId` value-guard before `TryRemove` | Read `CopyEngine.cs:5869-5871` |
| AC-5 | `EvictDedup` Filled branch: `storedId==orderId` value-guard before `TryRemove` | Read `CopyEngine.cs:5884-5886` |
| AC-6 | New test `IsLiveEntryBlocked_DifferentOrderId_SameInstrKey_NotBlocked` compiles with `[Fact]` annotation | Build + test run |
| AC-7 | New test asserts `blocked==false` for orderId-B on an instrKey occupied by orderId-A | Test run: `Assert.False(blocked)` passes |

---

### 1.7 xUnit Tests

| Test name | File | Assertion | Status |
|-----------|------|-----------|--------|
| `IsLiveEntryBlocked_DifferentOrderId_SameInstrKey_NotBlocked` | CopyEngineTests.cs (insert after line 7922) | `Assert.False(firstResult)` + `Assert.False(blocked)` | ADD (missing — DW-REPAIRS-03-POST-02) |
| `IsLiveEntryBlocked_ClearsOnFill_AllowsReentry` | CopyEngineTests.cs:7859 | `Assert.False(blocked2)` after fill | EXISTING — verify unmodified |
| `DispatchCopy_DoesNotSetPhantomInstrKey_WhenAllFollowersSkipped` | CopyEngineTests.cs:7894 | `Assert.False(blocked)` + `Assert.False(LiveEntryInstrumentsContains_ForTest)` | EXISTING — verify unmodified |

---

### 1.8 7-Scan Checklist (Engineer Contract)

The engineer signs off each item before closing TICKET-1.

- [ ] **SCAN-01 — lock() scan:** `grep "lock(" src/PropTraderTools/CopyEngine.cs` → zero matches in `IsLiveEntryBlocked_Check`, `SetLiveEntryDispatched`, `EvictDedup`
- [ ] **SCAN-02 — Unicode/non-ASCII:** `grep` for non-ASCII characters in changed/added lines → zero matches; all string literals are ASCII-only
- [ ] **SCAN-03 — CYC:** `IsLiveEntryBlocked_Check` body confirms 4 branches (`TryGetValue` node 1, `&&liveOrderId==orderId` node 2, `IsDedup` node 3, `ContainsKey(_entryDispatchedOrders)` node 4); `SetLiveEntryDispatched` CYC=1; `EvictDedup` CYC=6 per source comment line 5843
- [ ] **SCAN-04 — [Fact]:** new test method has `[Fact]` attribute on the line immediately preceding `public void`
- [ ] **SCAN-05 — Build:** `dotnet build Linting.csproj` → zero errors attributable to `CopyEngine.cs` or `CopyEngineTests.cs` (pre-existing `V12_002.*` NT8 SDK ref errors are exempt)
- [ ] **SCAN-06 — N/A** (no deploy-sync required for test-only addition; verify shim accessibility via `InternalsVisibleTo` at `CopyEngine.cs:46`)
- [ ] **SCAN-07 — N/A** (verification-only ticket; no new production logic paths introduced)

---

## TICKET-2: BUG-D — empty-name Limit entry orders blocked at gate0.5 (DW-LB-FL-01-V7)

**DW Reference:** DW-LB-FL-01-V7
**Spec requirements satisfied:** direct-edits.md EDIT 6–9 (BUG-D), plan Sections 3a–3f
**Epic:** PTT-REPAIRS-03-POST
**Scope:** VERIFICATION-ONLY (code already in source, tests already added)
**Files touched:**
- Verify: `src/PropTraderTools/CopyEngine.cs`
- Verify: `src/PropTraderTools/CopyEngineTests.cs`

---

### 2.1 Spec Summary

BUG-D: `DW-LB-FL-01 V6` added `if (name.Length == 0) return true;` to `IsExitSignalName`, which
over-broadened the check to block valid empty-name Limit entry orders. Fixed by:
- Removing the `name.Length == 0` branch from `IsExitSignalName` (CYC 8→7)
- Adding `IsExitSignalNameOrAnonClose(string name, OrderType orderType)` as a type-aware wrapper
  (CYC=3) that allows empty-name Limit orders and blocks empty-name non-Limit orders
- Updating `DispatchCopy` gate0.5 to call `IsExitSignalNameOrAnonClose` instead of `IsExitSignalName`
- Restoring `T_B59_07` contract (`Assert.False(IsExitSignalName(""))`) and adding 6 new [Fact] tests

No new code is required for this ticket unless drift is found during verification.

---

### 2.2 Methods to Verify

All methods listed below are **already in source**. The engineer must confirm each matches the
specification below.

#### 2.2.1 `IsExitSignalName`
**File:** `src/PropTraderTools/CopyEngine.cs`
**Lines:** 2360–2379
**Signature:** `internal static bool IsExitSignalName(string name)`

**Verify:**
- The branch `if (name.Length == 0) return true;` is **absent** from the method body
- The branch `if (name == null) return false;` is present at line 2362 (null guard)
- CYC = 7: base(1) + `null`(1) + `PTT-`(1) + `IsNativeCloseOrFlattenSignal`(1) + `Rev`(1) + `Exit`(1) + `IsAtmTargetSignalName`(1)
- Comment at line 2358 reads: `// empty("") returns false -- see IsExitSignalNameOrAnonClose for the type-aware empty guard.`
- JS-021: no `lock(`; JS-001: no `throw`; JS-002: returns bool; ASCII-only

#### 2.2.2 `IsExitSignalNameOrAnonClose`
**File:** `src/PropTraderTools/CopyEngine.cs`
**Lines:** 2439–2444
**Signature:** `internal static bool IsExitSignalNameOrAnonClose(string name, OrderType orderType)`

**Required body:**
```csharp
internal static bool IsExitSignalNameOrAnonClose(string name, OrderType orderType)
{
    if (name != null && name.Length == 0)
        return orderType != OrderType.Limit; // (1)+(2): empty-name Limit=allow, others=block
    return IsExitSignalName(name); // (3): named order -- delegate to normal check
}
```

**Verify:**
- Method exists as `internal static bool`
- Empty-name guard: `name != null && name.Length == 0` (compound, handles null safely without throwing)
- For empty-name Limit: `orderType != OrderType.Limit` evaluates to `false` → allow through
- For empty-name non-Limit (Market, StopMarket): `orderType != OrderType.Limit` evaluates to `true` → block
- Null name: `name != null` is false → falls through to `IsExitSignalName(null)` → returns `false`
- Non-empty name: `name.Length == 0` is false → delegates to `IsExitSignalName(name)`
- CYC = 3: `name != null` guard (1), `name.Length == 0 + orderType != Limit` short-circuit (2), `IsExitSignalName` delegation (3 via CYC+=0 tail)
- JS-021: no `lock(`; JS-001: no `throw`; JS-002: returns bool; ASCII-only

**Decision table (verify all rows against source):**

| `name` | `orderType` | Expected return | Source path |
|--------|-------------|-----------------|-------------|
| `""` | `Limit` | `false` | `name != null && name.Length == 0` → `orderType != Limit` = false |
| `""` | `Market` | `true` | `name != null && name.Length == 0` → `orderType != Limit` = true |
| `""` | `StopMarket` | `true` | `name != null && name.Length == 0` → `orderType != Limit` = true |
| `null` | any | `false` | `name != null` = false → `IsExitSignalName(null)` = false |
| `"PTT-Copy"` | any | `true` | `name.Length == 0` = false → `IsExitSignalName("PTT-Copy")` = true |
| `"Entry"` | `Limit` | `false` | `name.Length == 0` = false → `IsExitSignalName("Entry")` = false |

#### 2.2.3 `DispatchCopy` gate0.5 call site
**File:** `src/PropTraderTools/CopyEngine.cs`
**Lines:** 2450–2464
**Signature:** `private void DispatchCopy(Order order, CopyRule rule)`

**Verify (line 2454):**
```csharp
if (IsExitSignalNameOrAnonClose(order.Name, order.OrderType))
```

**Verify:**
- The call is `IsExitSignalNameOrAnonClose(order.Name, order.OrderType)` — NOT `IsExitSignalName(order.Name)`
- No other gate0.5 exit call on `IsExitSignalName` precedes or replaces this line
- CYC of `DispatchCopy` is unchanged at 8 (one call replaces one call — no new branches in `DispatchCopy` itself)
- Diagnostic log on lines 2456–2462 is ASCII-only (uses only `[`, `]`, `-`, `=`, space characters)

---

### 2.3 Tests to Verify

All tests listed below are **already in source**. The engineer confirms each is present and
unmodified. No new tests are required unless drift is found.

#### 2.3.1 `T_B59_07_IsExitSignalName_ArbitrarySignal_ReturnsFalse`
**File:** `src/PropTraderTools/CopyEngineTests.cs`
**Lines:** 3125–3132

**Verify:**
- `[Fact]` annotation present at line 3125
- Method name is `T_B59_07_IsExitSignalName_ArbitrarySignal_ReturnsFalse`
- Line 3131: `Assert.False(CopyEngine.IsExitSignalName(""));` — this assertion is present and UNMODIFIED
- The assertion is valid (no longer contradicted by source): `IsExitSignalName("")` now returns `false`

#### 2.3.2 `T_B59_AnonClose_01` through `T_B59_AnonClose_06`
**File:** `src/PropTraderTools/CopyEngineTests.cs`
**Lines:** 3141–3182

**Verify each test is present with [Fact], correct method name, and correct assertion:**

| Test | Lines | Method name | Key assertion |
|------|-------|-------------|---------------|
| T_B59_AnonClose_01 | 3141–3146 | `T_B59_AnonClose_01_EmptyName_LimitType_ReturnsFalse` | `Assert.False(CopyEngine.IsExitSignalNameOrAnonClose("", OrderType.Limit))` |
| T_B59_AnonClose_02 | 3148–3153 | `T_B59_AnonClose_02_EmptyName_MarketType_ReturnsTrue` | `Assert.True(CopyEngine.IsExitSignalNameOrAnonClose("", OrderType.Market))` |
| T_B59_AnonClose_03 | 3155–3160 | `T_B59_AnonClose_03_EmptyName_StopMarketType_ReturnsTrue` | `Assert.True(CopyEngine.IsExitSignalNameOrAnonClose("", OrderType.StopMarket))` |
| T_B59_AnonClose_04 | 3162–3168 | `T_B59_AnonClose_04_NamedPttPrefix_AnyType_ReturnsTrue` | `Assert.True(...("PTT-Copy", Limit))` and `Assert.True(...("PTT-Copy", Market))` |
| T_B59_AnonClose_05 | 3170–3175 | `T_B59_AnonClose_05_NamedEntry_LimitType_ReturnsFalse` | `Assert.False(CopyEngine.IsExitSignalNameOrAnonClose("Entry", OrderType.Limit))` |
| T_B59_AnonClose_06 | 3177–3182 | `T_B59_AnonClose_06_NullName_ReturnsFalse` | `Assert.False(CopyEngine.IsExitSignalNameOrAnonClose(null, OrderType.Market))` |

---

### 2.4 Method Signatures (complete)

| Symbol | Kind | File | Lines | Signature |
|--------|------|------|-------|-----------|
| `IsExitSignalName` | method | CopyEngine.cs | 2360–2379 | `internal static bool IsExitSignalName(string name)` |
| `IsExitSignalNameOrAnonClose` | method | CopyEngine.cs | 2439–2444 | `internal static bool IsExitSignalNameOrAnonClose(string name, OrderType orderType)` |
| `DispatchCopy` (gate0.5 call site) | method | CopyEngine.cs | 2450–2464 | `private void DispatchCopy(Order order, CopyRule rule)` (gate0.5 at line 2454) |
| `T_B59_07_IsExitSignalName_ArbitrarySignal_ReturnsFalse` | test | CopyEngineTests.cs | 3125–3132 | `public void T_B59_07_IsExitSignalName_ArbitrarySignal_ReturnsFalse()` |
| `T_B59_AnonClose_01..06` | tests | CopyEngineTests.cs | 3141–3182 | 6 `[Fact]` methods (see Section 2.3.2) |

---

### 2.5 Jane Street Rule Constraints

| Method | JS-001 no throw | JS-002 bool safety | JS-021 no lock | JS-013 CYC ≤ 8 | ASCII-only |
|--------|-----------------|--------------------|----------------|----------------|------------|
| `IsExitSignalName` | PASS | PASS | PASS | PASS (CYC=7) | PASS |
| `IsExitSignalNameOrAnonClose` | PASS | PASS | PASS | PASS (CYC=3) | PASS |
| `DispatchCopy` (gate0.5 change) | PASS | N/A (void) | PASS | PASS (CYC=8) | PASS |

---

### 2.6 Acceptance Criteria

| # | Criterion | How to verify |
|---|-----------|---------------|
| AC-1 | `IsExitSignalName` does NOT contain `if (name.Length == 0) return true;` | Read `CopyEngine.cs:2360–2379` — no `name.Length` check present |
| AC-2 | `IsExitSignalName` CYC = 7 (not 8) | Count 7 decision branches in source: null, PTT-, IsNativeCloseOrFlattenSignal, Rev, Exit, IsAtmTargetSignalName (+ base = 7) |
| AC-3 | `IsExitSignalNameOrAnonClose` exists as `internal static bool`, handles all 6 decision-table rows correctly | Read `CopyEngine.cs:2439–2444`; trace each decision-table row |
| AC-4 | `DispatchCopy` gate0.5 calls `IsExitSignalNameOrAnonClose(order.Name, order.OrderType)` at line 2454 | Read `CopyEngine.cs:2454` — no `IsExitSignalName(order.Name)` at gate0.5 |
| AC-5 | `T_B59_07` line 3131 assertion `Assert.False(CopyEngine.IsExitSignalName(""))` is present and correct | Read `CopyEngineTests.cs:3131` |
| AC-6 | All 6 `T_B59_AnonClose_*` tests present, each has `[Fact]`, assertions match Section 2.3.2 | Read `CopyEngineTests.cs:3141–3182` |
| AC-7 | `T_B59_07` passes: `IsExitSignalName("")` returns `false` (old V6 branch removed) | Test run |

---

### 2.7 xUnit Tests

| Test name | File | Assertion | Status |
|-----------|------|-----------|--------|
| `T_B59_07_IsExitSignalName_ArbitrarySignal_ReturnsFalse` | CopyEngineTests.cs:3125 | `Assert.False(IsExitSignalName(""))` | EXISTING — verify present and unmodified |
| `T_B59_AnonClose_01_EmptyName_LimitType_ReturnsFalse` | CopyEngineTests.cs:3141 | `Assert.False(...("", Limit))` | EXISTING — verify present |
| `T_B59_AnonClose_02_EmptyName_MarketType_ReturnsTrue` | CopyEngineTests.cs:3148 | `Assert.True(...("", Market))` | EXISTING — verify present |
| `T_B59_AnonClose_03_EmptyName_StopMarketType_ReturnsTrue` | CopyEngineTests.cs:3155 | `Assert.True(...("", StopMarket))` | EXISTING — verify present |
| `T_B59_AnonClose_04_NamedPttPrefix_AnyType_ReturnsTrue` | CopyEngineTests.cs:3162 | `Assert.True` for Limit and Market | EXISTING — verify present |
| `T_B59_AnonClose_05_NamedEntry_LimitType_ReturnsFalse` | CopyEngineTests.cs:3170 | `Assert.False(...("Entry", Limit))` | EXISTING — verify present |
| `T_B59_AnonClose_06_NullName_ReturnsFalse` | CopyEngineTests.cs:3177 | `Assert.False(...(null, Market))` | EXISTING — verify present |

---

### 2.8 7-Scan Checklist (Engineer Contract)

The engineer signs off each item before closing TICKET-2.

- [ ] **SCAN-01 — lock() scan:** `grep "lock(" src/PropTraderTools/CopyEngine.cs` → zero matches in `IsExitSignalName`, `IsExitSignalNameOrAnonClose`, `DispatchCopy`
- [ ] **SCAN-02 — Unicode/non-ASCII:** all string literals in changed/verified methods are ASCII-only; diagnostic log at `DispatchCopy:2456–2462` uses only ASCII characters
- [ ] **SCAN-03 — CYC:** `IsExitSignalName` = 7 (no `name.Length==0` branch); `IsExitSignalNameOrAnonClose` = 3; `DispatchCopy` = 8 (unchanged — one call replaced one call, no new branches)
- [ ] **SCAN-04 — [Fact]:** all 6 `T_B59_AnonClose_*` methods have `[Fact]`; `T_B59_07` has `[Fact]`
- [ ] **SCAN-05 — Build:** `dotnet build Linting.csproj` → zero errors attributable to `CopyEngine.cs` or `CopyEngineTests.cs` (pre-existing `V12_002.*` NT8 SDK ref errors are exempt)
- [ ] **SCAN-06 — N/A** (verification-only ticket; no new production logic added)
- [ ] **SCAN-07 — N/A** (verification-only ticket; no deploy-sync required)

---

## Deferred Items Carried Forward

These items are tracked in `docs/brain/PTT-REPAIRS-03-POST/02-architecture-plan.md` Section 5
and are NOT addressed by TICKET-1 or TICKET-2.

| ID | Priority | Description | Status |
|----|----------|-------------|--------|
| DW-REPAIRS-03-POST-01 | P3 | TOCTOU window in value-guarded `TryRemove` in `EvictDedup` (theoretically non-exploitable in NT8 single-threaded callback) | OPEN |
| DW-B24-02 | P1 (elevated) | Manual E2E runtime verification — now includes BUG-C and BUG-D scenarios | OPEN |
| DW-REPAIRS-02-01 | P2 | `_entryDispatchedOrders` NOT cleared in Filled branch of `EvictDedup` | OPEN |
| DW-B25-01 | P2 | Companion field race (`_pendingBeAccount` etc.) | OPEN |
| DW-B24-01 | P3 | NT8-043 null-conditional event unsubscription rule watch | OPEN |
| DW-B24-03 | P3 | Skip-duplicate guard `[Fact]` | OPEN |
| DW-REPAIRS-01-01 | P3 | R5 `Account.All` constructor-path risk | OPEN |
| DW-REPAIRS-01-02 | P3 | `TryCancelBeOrders` `-1`-path `[Fact]` test | OPEN |
| DW-B26-01 | P3 | Reflection test upgrade Option B→A | OPEN |

---

*ptt-architect · PTT-REPAIRS-03-POST · 04-tickets.md · 2026-09-06*
