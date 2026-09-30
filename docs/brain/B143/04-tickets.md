# B143 Tickets

**Block**: B143
**Phase**: 3 (Ticket Generation)
**Produced by**: ptt-architect
**Plan**: `docs/brain/B143/02-architecture-plan.md` (REVIEW_PASS — cycle 1)
**Plan review**: `docs/brain/B143/02-plan-review.md` (REVIEW_PASS)
**Pipeline**: SINGLE-PIPELINE (all changes mutually dependent — DW-B142-MGC-02)
**Ticket count**: 1

---

## Ticket 1 — Add test seam shims + B143 xUnit test suite

### Spec Requirement IDs

- **DW-B142-MGC-02** — Instrument-level entry guard blocks duplicate dispatches for MGC cancel+resubmit pattern (CLOSED by commit `3f709a91`; this ticket verifies closure with 7 tests)
- **DW-B142-MGC-01** — Root cause: MGC cancel+resubmit produces duplicate entry dispatch (CLOSED; root cause resolved by MGC-02 guard)

---

### Files Modified

| File | Change Type | Description |
|------|-------------|-------------|
| `src/PropTraderTools/CopyEngine.cs` | Test seam only — **no logic changes** | Add 5 thin forwarding shims in a new `#region B143 test seam` block adjacent to the existing DW-B135 test accessors at line 3501 |
| `src/PropTraderTools/Tests/B143Tests.cs` | New file | 7 xUnit `[Fact]` tests for the MGC instrument-level entry guard |

**Also required**: Add `<Compile Include="Tests\B143Tests.cs" />` to `src/PropTraderTools/PropTraderTools.csproj` after the existing `<Compile Include="Tests\B139Tests.cs" />` line.

**InternalsVisibleTo check**: `[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("PropTraderTools.Tests")]` is already present at line 46 of `CopyEngine.cs`. **No new attribute needed.**

---

### Method Signatures — Test Seam (add to CopyEngine.cs)

Insert the following `#region` block in `CopyEngine.cs` immediately after the DW-B135 test accessors block that ends at line 3511 (after `TestOnly_LastLeaderDirection`). The shims are thin expression-body forwarders — **zero logic, zero branches**.

```csharp
        // B143 test seam -- no logic, thin shims only.
        // InternalsVisibleTo("PropTraderTools.Tests") granted at L46.
        #region B143 test seam

        internal bool IsLiveEntryBlocked_ForTest(string instrKey, string orderId, double limitPrice)
            => IsLiveEntryBlocked(instrKey, orderId, limitPrice);

        internal void EvictDedup_ForTest(string orderId, NinjaTrader.Cbi.OrderState state)
            => EvictDedup(orderId, state);

        internal void ClearLiveEntryForInstrument_ForTest(string instrFullName)
            => ClearLiveEntryForInstrument(instrFullName);

        internal bool LiveEntryInstrumentsContains_ForTest(string key)
            => _liveEntryInstruments.ContainsKey(key);

        internal bool EntryInstrKeyByOrderIdContains_ForTest(string orderId)
            => _entryInstrKeyByOrderId.ContainsKey(orderId);

        #endregion
```

**Note**: `EvictDedup` is already `internal` (line 4643). The `EvictDedup_ForTest` shim is included for symmetry and to avoid tests calling production internal methods directly by name; it is a thin forwarder and carries zero risk.

---

### xUnit [Fact] Method Names

All 7 tests live in `class B143Tests` in `src/PropTraderTools/Tests/B143Tests.cs`.

Each test uses a unique `instrKey` prefix (`"TEST-B143-0N|..."`) to prevent cross-test dictionary contamination. The `CopyEngine` instance is obtained via the existing singleton accessor used by prior B-block test files.

| # | Test Name | Contract Verified |
|---|-----------|-------------------|
| 1 | `T_B143_01_IsLiveEntryBlocked_FirstCall_ReturnsFalse_AllowsDispatch` | Fresh instrKey + orderId → returns false; dispatch allowed |
| 2 | `T_B143_02_IsLiveEntryBlocked_SecondCall_SameInstrKey_ReturnsTrue_BlocksDuplicate` | Same instrKey, different orderId → returns true; instrument-level guard fires |
| 3 | `T_B143_03_EvictDedup_Cancelled_ClearsInstrKey_FutureEntryUnblocked` | `EvictDedup(orderId, Cancelled)` → clears companion map + live entry; new call on same instrKey returns false |
| 4 | `T_B143_04_EvictDedup_Filled_DoesNotClear_TradeStillLive` | `EvictDedup(orderId, Filled)` → companion map cleaned, `_liveEntryInstruments` key preserved; new call on same instrKey returns true |
| 5 | `T_B143_05_ClearLiveEntryForInstrument_RemovesAllKeysWithPrefix` | `ClearLiveEntryForInstrument` removes both `"TEST-B143-05|Sell"` and `"TEST-B143-05|Buy"` in one call |
| 6 | `T_B143_06_ClearLiveEntryForInstrument_IsNoOp_WhenNoMatchingKey` | `ClearLiveEntryForInstrument("INSTRUMENT_NOT_PRESENT")` throws no exception; unrelated key survives |
| 7 | `T_B143_07_EvictDedup_BracketCancelOrderId_DoesNotClearLiveEntryGuard` | `EvictDedup(bracketOrderId, Cancelled)` where bracketOrderId is NOT in `_entryInstrKeyByOrderId` → `_liveEntryInstruments` key for the original entry survives; scoped-removal contract verified |

---

### Test Specifications (arrange / act / assert per test)

#### T_B143_01 — First Call Returns False (Dispatch Allowed)

| Field | Value |
|-------|-------|
| **Method under test** | `IsLiveEntryBlocked_ForTest` |
| **Arrange** | Obtain `CopyEngine` singleton |
| **Act** | `engine.IsLiveEntryBlocked_ForTest("TEST-B143-01|Sell", "ORD-B143-01", 2000.0)` |
| **Assert** | `Assert.False(result)` — new instrKey and orderId, dispatch allowed |
| **CYC** | 1 (single path, no branches in test body) |

---

#### T_B143_02 — Second Call Same instrKey Returns True (Duplicate Blocked)

| Field | Value |
|-------|-------|
| **Method under test** | `IsLiveEntryBlocked_ForTest` |
| **Arrange** | `engine.IsLiveEntryBlocked_ForTest("TEST-B143-02|Sell", "ORD-B143-02A", 2000.0)` — first call records key |
| **Act** | `engine.IsLiveEntryBlocked_ForTest("TEST-B143-02|Sell", "ORD-B143-02B", 2000.0)` — different orderId, same instrKey |
| **Assert** | First call: `Assert.False(firstResult)`. Second call: `Assert.True(secondResult)` |
| **CYC** | 1 |

---

#### T_B143_03 — EvictDedup Cancelled Clears Guard (Entry Unblocked)

| Field | Value |
|-------|-------|
| **Method under test** | `EvictDedup_ForTest` (Cancelled path) |
| **Arrange** | `engine.IsLiveEntryBlocked_ForTest("TEST-B143-03|Sell", "ORD-B143-03", 2000.0)` → false (entry recorded) |
| **Act** | `engine.EvictDedup_ForTest("ORD-B143-03", NinjaTrader.Cbi.OrderState.Cancelled)` |
| **Assert** | `Assert.False(engine.IsLiveEntryBlocked_ForTest("TEST-B143-03|Sell", "ORD-B143-03C", 2000.0))` — instrument slot unblocked |
| **CYC** | 1 |

---

#### T_B143_04 — EvictDedup Filled Does NOT Clear Guard (Trade Still Live)

| Field | Value |
|-------|-------|
| **Method under test** | `EvictDedup_ForTest` (Filled path) |
| **Arrange** | `engine.IsLiveEntryBlocked_ForTest("TEST-B143-04|Sell", "ORD-B143-04", 2000.0)` → false (entry recorded) |
| **Act** | `engine.EvictDedup_ForTest("ORD-B143-04", NinjaTrader.Cbi.OrderState.Filled)` |
| **Assert** | `Assert.True(engine.IsLiveEntryBlocked_ForTest("TEST-B143-04|Sell", "ORD-B143-04F", 2000.0))` — instrument slot still blocked (trade live) |
| **CYC** | 1 |

---

#### T_B143_05 — ClearLiveEntryForInstrument Removes All Keys With Prefix

| Field | Value |
|-------|-------|
| **Method under test** | `ClearLiveEntryForInstrument_ForTest` |
| **Arrange** | Record two keys: `IsLiveEntryBlocked_ForTest("TEST-B143-05|Sell", "ORD-B143-05A", 2000.0)` → false; `IsLiveEntryBlocked_ForTest("TEST-B143-05|Buy", "ORD-B143-05B", 2000.0)` → false |
| **Act** | `engine.ClearLiveEntryForInstrument_ForTest("TEST-B143-05")` |
| **Assert** | `Assert.False(engine.IsLiveEntryBlocked_ForTest("TEST-B143-05|Sell", "ORD-B143-05C", 0.0))` AND `Assert.False(engine.IsLiveEntryBlocked_ForTest("TEST-B143-05|Buy", "ORD-B143-05D", 0.0))` |
| **CYC** | 1 |

---

#### T_B143_06 — ClearLiveEntryForInstrument Is No-Op When No Matching Key

| Field | Value |
|-------|-------|
| **Method under test** | `ClearLiveEntryForInstrument_ForTest` |
| **Arrange** | Record unrelated key: `IsLiveEntryBlocked_ForTest("UNRELATED-INSTR|Sell", "ORD-B143-06U", 0.0)` → false |
| **Act** | `engine.ClearLiveEntryForInstrument_ForTest("INSTRUMENT_NOT_PRESENT")` — no exception |
| **Assert** | `Assert.True(engine.IsLiveEntryBlocked_ForTest("UNRELATED-INSTR|Sell", "ORD-B143-06X", 0.0))` — unrelated key still blocks; unrelated-instr guard is unaffected |
| **CYC** | 1 |

---

#### T_B143_07 — EvictDedup BracketOrderId Cancelled Does NOT Clear Live Entry Guard

| Field | Value |
|-------|-------|
| **Method under test** | `EvictDedup_ForTest` (Cancelled path, non-entry orderId) |
| **Arrange** | Record entry: `IsLiveEntryBlocked_ForTest("TEST-B143-07|Sell", "ORD-B143-07A", 2000.0)` → false (key stored in both `_liveEntryInstruments` and `_entryInstrKeyByOrderId`) |
| **Act** | `engine.EvictDedup_ForTest("BRACKET-ORD-B143-07", NinjaTrader.Cbi.OrderState.Cancelled)` — `"BRACKET-ORD-B143-07"` was **never** written to `_entryInstrKeyByOrderId` |
| **Assert** | `Assert.True(engine.IsLiveEntryBlocked_ForTest("TEST-B143-07|Sell", "ORD-B143-07B", 2000.0))` — live entry guard for the original entry survives; bracket cancel did not wipe it |
| **Rationale** | Verifies scoped-removal contract (plan §4.4): `TryRemove` on `_entryInstrKeyByOrderId` returns false for a non-entry orderId → `_liveEntryInstruments` is untouched. ATM bracket cancels, drag cancels, and other non-Gate-5 cancels must not wipe the instrument guard. |
| **CYC** | 1 |

---

### JS Rule Constraints

| Rule | Description | Application to This Ticket |
|------|-------------|---------------------------|
| **JS-021** | No `lock()` anywhere | Production code uses `ConcurrentDictionary.TryAdd`/`TryRemove`/`ContainsKey` exclusively. Test seam shims are read-only expression bodies. **Zero `lock()` calls.** Verify: `grep -n "lock(" src/PropTraderTools/CopyEngine.cs` → 0 hits |
| **JS-001** | No `throw` in hot paths | Test assertions use `Assert.False` / `Assert.True` / `Assert.Equal` only. No `throw` in test bodies or shim methods. |
| **JS-033** | No `async void` | All 7 `[Fact]` tests are synchronous. No `async` keyword anywhere in test bodies or shim methods. |
| **JS-066** | CYC <= 8 | All 7 test methods must have CYC=1 (single linear path, no branches). All 5 shim methods have CYC=1 (expression-body forwarders). Production method CYC values are unchanged from commit `3f709a91`: `IsLiveEntryBlocked`=4, `ClearLiveEntryForInstrument`=2, `EvictDedup`=5, `TryFirePositionState`=8 (AT LIMIT, no change), `DispatchCopy`=8 (AT LIMIT, no change). |
| **ASCII-only** | No Unicode in identifiers or string literals | All instrKey strings (`"TEST-B143-0N|Sell"`, `"BRACKET-ORD-B143-07"`, etc.), method names, and comments are ASCII-only. The `"|"` pipe separator is ASCII 0x7C. |

---

### 7-Scan Checklist

The engineer MUST run ALL 7 scans in order and record results in the completion artifact before declaring BUILD_PASS. A failing scan is a hard blocker — do not proceed to the next scan until the failure is resolved.

---

**SCAN-01: ASCII scan**

```powershell
grep -P "[^\x00-\x7F]" src/PropTraderTools/CopyEngine.cs
grep -P "[^\x00-\x7F]" src/PropTraderTools/Tests/B143Tests.cs
```

Expected: **0 hits in both files**

Failure action: Identify and remove any non-ASCII character (Unicode, curly quotes, em-dash). All string literals, comments, and identifiers must be ASCII-only.

---

**SCAN-02: lock() ban (JS-021)**

```powershell
grep -n "lock(" src/PropTraderTools/CopyEngine.cs
```

Expected: **0 hits**

Failure action: Any `lock()` found is a P0 JS-021 violation — STOP. Replace with `ConcurrentDictionary` operation per JS-025.

---

**SCAN-03: CYC audit**

```powershell
python scripts/complexity_audit.py src/PropTraderTools/CopyEngine.cs
```

Expected: **All methods report CYC <= 8**

Specific spot-checks (must match plan §5 exactly):

| Method | Expected CYC | Status |
|--------|-------------|--------|
| `IsLiveEntryBlocked` | 4 | Unchanged |
| `ClearLiveEntryForInstrument` | 2 | Unchanged |
| `EvictDedup` | 5 | Unchanged |
| `TryFirePositionState` | 8 (AT LIMIT) | Must not have increased — straight-line shim addition adds 0 branches |
| `DispatchCopy` | 8 (AT LIMIT, unchanged) | No touch |

Failure action: If any method reports CYC > 8, do NOT merge. Extract a branch to a helper. If `TryFirePositionState` reports CYC > 8, a branch was accidentally added — remove it. The 5 new test seam shims must each report CYC=1.

---

**SCAN-04: JS P0 gate**

```powershell
grep -n "throw new" src/PropTraderTools/CopyEngine.cs
grep -n "async void " src/PropTraderTools/CopyEngine.cs
grep -n "return null;" src/PropTraderTools/CopyEngine.cs
```

Scope: check **new and changed lines only** (test seam shims + B143Tests.cs). New shims return `bool` or `void` — `return null` is not applicable. Tests return `void` from `[Fact]` — `return null` not applicable.

Expected: **0 hits in new/changed code for all three patterns**

Failure action: Any `async void` (non-event-handler) is a P0 JS-033 violation — STOP. Any `throw new` in new test or shim code is a P0 JS-001 violation — STOP.

---

**SCAN-05: dotnet build**

```powershell
dotnet build src/PropTraderTools/PropTraderTools.csproj
```

Expected: **0 errors, 0 new warnings**

Pre-requisite: `<Compile Include="Tests\B143Tests.cs" />` must be present in `PropTraderTools.csproj` before running this scan.

Failure action: Fix all compilation errors. Do not suppress warnings without Director approval.

---

**SCAN-06: dotnet test**

```powershell
dotnet test tests/PropTraderTools.Tests/ --filter "FullyQualifiedName~B143"
```

Expected: **7/7 PASS** (`T_B143_01` through `T_B143_07`), **0 failures, 0 skipped**

Failure action: For each failed test, read the failure message, identify whether the production code or the test assertion is wrong, and fix only what is wrong. Do not comment out or skip a failing test.

---

**SCAN-07: ptt-sync-and-verify**

```powershell
powershell -File scripts\ptt-sync-and-verify.ps1
```

Expected: **0 MISMATCH lines**

Failure action: If any MISMATCH is reported, the sync failed — re-run and confirm file was copied correctly to the NT8 directory before reporting completion.

---

### Completion Artifact

**File**: `docs/brain/B143/ticket-1-completion.md`

The engineer MUST create this file upon completing all 7 scans. It must contain:

```
# B143 Ticket 1 Completion

**Status**: BUILD_PASS | BUILD_FAIL  (delete whichever does not apply)
**Engineer**: [name]
**Date**: [date]

## SCAN-01 (ASCII): PASS | FAIL
[paste grep output or "0 hits"]

## SCAN-02 (lock ban): PASS | FAIL
[paste grep output or "0 hits"]

## SCAN-03 (CYC): PASS | FAIL
[paste complexity_audit.py output, confirm TryFirePositionState==8]

## SCAN-04 (JS P0): PASS | FAIL
[paste grep output for all three patterns]

## SCAN-05 (dotnet build): PASS | FAIL
[paste build output last 20 lines]

## SCAN-06 (dotnet test): PASS | FAIL
[paste test output — must show 7 passed, 0 failed]

## SCAN-07 (ptt-sync-and-verify): PASS | FAIL
[paste sync output — must show 0 MISMATCH]

## DW Items Closed
- DW-B142-MGC-02: CLOSED (T_B143_01, T_B143_02 verify first-pass allow + duplicate block)
- DW-B142-MGC-01: CLOSED (root cause resolved by MGC-02 instrument-level guard)
```

---

*Produced by ptt-architect, B143 Phase 3. This ticket is the engineer's contract. Missing any section = TICKET_REVIEW_FAIL.*
