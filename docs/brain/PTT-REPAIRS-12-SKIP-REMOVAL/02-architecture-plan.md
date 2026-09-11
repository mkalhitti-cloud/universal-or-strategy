# Architecture Plan — PTT-REPAIRS-12-SKIP-REMOVAL

**Epic:** PTT-REPAIRS-12-SKIP-REMOVAL  
**Phase:** 1 (Architecture)  
**Author:** PTT Architect  
**Status:** PLAN_COMPLETE  
**Closes:** DW-09-04 (all remaining obfuscation-Skip annotations — ≈137 tests in CopyEngineTests.cs)

---

## LANE-SPLIT GATE RESULT: SINGLE-PIPELINE

All 4 tickets modify the same file (`CopyEngineTests.cs`) in disjoint line ranges. A serial
T1 → T2 → T3 → T4 pipeline with a `dotnet test` gate after each ticket is the correct execution
model. Parallel lanes would require same-file merge coordination and add no throughput benefit
for mechanical attribute removal. LANES not approved.

---

## 1. Baseline & Target

| Metric        | Baseline | Target |
|---------------|----------|--------|
| Passed        | 26       | 163    |
| Failed        | 0        | 0 (HARD) |
| Skipped       | 488      | 351    |
| Total         | 514      | 514    |

Delta: 137 obfuscation-skipped tests become plain `[Fact]` and are expected to pass.

---

## 2. File Under Modification

**Single file:** `src/PropTraderTools/CopyEngineTests.cs`

No production `.cs` files are modified. No `deploy-sync.ps1` run required.

---

## 3. Static Method Binding Flags Analysis — Per Class

### 3.1 B79CancelRaceGuardTests (L5829–L6461)

**Class-level GetMethod helper** (L5845–L5847):
```csharp
private static System.Reflection.MethodInfo GetMethod(string name) =>
    typeof(CopyEngine).GetMethod(name, System.Reflection.BindingFlags.NonPublic
        | System.Reflection.BindingFlags.Instance);
```

**Static method in this class:** `IsPositionFlatOrMissing`

**Verification:** Tests for `IsPositionFlatOrMissing` at L6357–L6374 do NOT use the class-level
`GetMethod` helper. They use an inline call with `BindingFlags.NonPublic | BindingFlags.Static`
directly:
```csharp
var m = typeof(CopyEngine).GetMethod(
    "IsPositionFlatOrMissing",
    BindingFlags.NonPublic | BindingFlags.Static
);
```
This is already correct. **No `GetStaticMethod` helper addition required.**

All remaining 57 obfuscation-skip tests in this class call the instance `GetMethod` helper
for instance methods. Binding flags are correct for all.

**Verdict:** SAFE. Zero structural additions needed.

---

### 3.2 BwaveCycT1R1BeHelperTests (L6475–L6684)

**Class-level GetMethod helper** (L6477–L6478):
```csharp
private static MethodInfo GetMethod(string name) =>
    typeof(CopyEngine).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance);
```

**Static methods in this class:** None. All methods under test are instance methods:
`GetMarketBidPrice`, `GetMarketAskPrice`, `GetBeTickSize`, `SelectBeRefPriceByDirection`,
`FireBeAndNotifyEvent`, `ShouldFireBeImmediately`, `CompleteBeArming`, `TryClaimPendingBeSlot`,
`GetSlotInstrumentName`, `GetSlotAccountName`, `RaisePendingBeFiredEvent`,
`SettleAndFirePendingBe`, `TryFireImmediateBeIfAlreadyAtLevel`, `IsPendingBeTriggerMet`.

All 23 obfuscation-skip tests use the instance `GetMethod` helper. Binding flags correct.

**Verdict:** SAFE. No `GetStaticMethod` helper needed.

---

### 3.3 BwaveCycTaR2HelperTests (L6692–L6813)

**Class-level helpers** (L6694–L6697):
```csharp
private static MethodInfo GetMethod(string name) =>
    typeof(CopyEngine).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance);
private static MethodInfo GetStaticMethod(string name) =>
    typeof(CopyEngine).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static);
```
`GetStaticMethod` **already present** (added by PTT-REPAIRS-11-BINDING-FLAGS-03).

**Static methods in this class under obfuscation-skip tests:** None.
All 12 obfuscation-skip tests call `GetMethod` (instance): `HasValidTargetNameSuffix`,
`IsLeaderTargetOrder` (×4), `SelectBeTargetList`, `IsBeTargetActiveState`,
`IsBeTargetPendingChangeState`, `IsBeTargetSnapshotState`, `IsEligibleBeTargetOrder` (×3).

The already-fixed `GetSenderAccountName_ShouldBeReusedByOnTrailBeAccountUpdate` at L6806
uses `GetStaticMethod` and is already plain `[Fact]`. **DO NOT TOUCH.**

**Verdict:** SAFE. `GetStaticMethod` already exists. All 12 use instance binding. Zero additions.

---

### 3.4 BwaveCycTaR3HelperTests (L6820–L7116)

**Class-level helpers** (L6822–L6826):
```csharp
private static MethodInfo GetMethod(string name) =>
    typeof(CopyEngine).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance);
private static MethodInfo GetStaticMethod(string name) =>
    typeof(CopyEngine).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static);
```
`GetStaticMethod` **already present** (added by PTT-REPAIRS-11-BINDING-FLAGS-02).

**Already-fixed tests (DO NOT TOUCH):**
- `LogBeSlotEviction_ShouldExist_AsPrivateVoidMethod` (L6937) — plain `[Fact]`, uses `GetStaticMethod`.
- `LogBeSlotEviction_ShouldAccept_AccNameAndIsRejectedParameters` (L6944) — plain `[Fact]`, uses `GetStaticMethod`.

**Static methods under obfuscation-skip tests in this class:** None.
All 33 obfuscation-skip tests call `GetMethod` (instance). The complete method list:
`TrySyncAtmBrackets`, `TrySkipTrailingStop`, `SyncStandardBracket`, `IsPttTgtDragOrder`,
`IsAtmTgtOrder`, `SyncAtmFollowerStopBracket` (×2), `IsBePendingTargetOrder` (×3),
`IsPttBeStopRejected` (×3), `IsPttDragOrderCancellable` (×4), `IsPttQxTargetOrder`,
`IsNativeAtmBeRetryTarget`, `IsBeRetryEligibleOrderState`, `IsBeRetryOrderInvalid`,
`IsBeSlotNonTerminal`, `IsBeFilledWithOpenPosition`, `IsPttDragOrderName`,
`IsDragInstrumentMatch`, `IsQxTOrderStateValid`, `IsQxTBracketNameValid`,
`TryGetCleanupEntryForFollower`, `IsCleanupEntryCurrentAndMatching`, `SendAtmCancelReplace`,
`TryMatchFollowerInRule`, `IsBeReplaceTargetValid`, `TryIncrementBeReplaceAttempt`.

**Verdict:** SAFE. All 33 use instance binding. Both helpers already present.

---

### 3.5 BwaveCycTaR6HelperTests (L7121–L7279)

**Class-level helpers** (L7123–L7127):
```csharp
private static MethodInfo GetStaticMethod(string name) =>
    typeof(CopyEngine).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static);
private static MethodInfo GetInstanceMethod(string name) =>
    typeof(CopyEngine).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance);
```
Both helpers **already present**. Note: this class has NO `GetMethod` (unnamed); it uses
`GetStaticMethod` and `GetInstanceMethod` explicitly.

**10 obfuscation-skip tests and their binding:**

| Test (line)                                           | Helper Used        | Binding  |
|-------------------------------------------------------|--------------------|----------|
| `IsBracketOrderLiveState_ShouldExist...` (L7131)      | `GetStaticMethod`  | STATIC   |
| `IsBracketOrderLiveState_ShouldReturnTrue...` (L7138) | `GetStaticMethod`  | STATIC   |
| `MatchesPttReplacementName_ShouldExist...` (L7178)    | `GetStaticMethod`  | STATIC   |
| `MatchesPttReplacementName_ShouldAccept...` (L7185)   | `GetStaticMethod`  | STATIC   |
| `LogHbcDiag_ShouldExist...` (L7195)                   | `GetInstanceMethod`| INSTANCE |
| `LogHbcDiag_ShouldAccept...` (L7202)                  | `GetInstanceMethod`| INSTANCE |
| `ExecuteStopDragOrder_ShouldExist...` (L7212)         | `GetInstanceMethod`| INSTANCE |
| `ExecuteStopDragOrder_ShouldAccept...` (L7219)        | `GetInstanceMethod`| INSTANCE |
| `IsOrderEventProcessable_ShouldExist...` (L7265)      | `GetStaticMethod`  | STATIC   |
| `IsOrderEventProcessable_ShouldAccept...` (L7272)     | `GetStaticMethod`  | STATIC   |

All binding flags match the declared method visibility in production. **Correct as-is.**

**Verdict:** SAFE. Both helpers already present. All 10 use correct binding.

---

## 4. GetStaticMethod Helper Requirements Summary

| Class                       | GetStaticMethod Needed? | Action Required          |
|-----------------------------|-------------------------|--------------------------|
| B79CancelRaceGuardTests     | NO                      | Tests use inline Static  |
| BwaveCycT1R1BeHelperTests   | NO                      | All methods are instance |
| BwaveCycTaR2HelperTests     | Already present         | None                     |
| BwaveCycTaR3HelperTests     | Already present         | None                     |
| BwaveCycTaR6HelperTests     | Already present         | None                     |

**GLOBAL VERDICT: Zero structural additions to CopyEngineTests.cs. Pure attribute removal.**

---

## 5. Ticket Breakdown

### T1 — B79CancelRaceGuardTests (59 removals)

- **File:** `src/PropTraderTools/CopyEngineTests.cs`
- **Line range:** L5968–L6460
- **Skip count:** 59 obfuscation-skips
- **Operation:** Replace `[Fact(Skip = "obfuscation: AgileDotNetRT renames private members; cannot locate by string name")]` → `[Fact]` for all 59 occurrences in this range.
- **Exact lines of obfuscation-skip in this range:**
  5968, 5976, 5984, 5992, 6002, 6010, 6018, 6026, 6036, 6043, 6050, 6059, 6066, 6075, 6082,
  6100, 6107, 6114, 6123, 6130, 6139, 6148, 6157, 6164, 6173, 6180, 6189, 6199, 6209, 6219,
  6231, 6238, 6247, 6254, 6261, 6270, 6277, 6284, 6293, 6300, 6309, 6316, 6325, 6332, 6341,
  6348, 6357, 6367, 6379, 6386, 6393, 6400, 6409, 6416, 6423, 6432, 6439, 6448, 6455
- **Special handling:** IsPositionFlatOrMissing tests (L6357, L6367) use inline Static binding — correct as-is, no helper change.
- **Verify gate:** `dotnet test --filter "FullyQualifiedName~B79CancelRaceGuardTests"` — 0 failed.

---

### T2 — BwaveCycT1R1BeHelperTests (23 removals)

- **File:** `src/PropTraderTools/CopyEngineTests.cs`
- **Line range:** L6482–L6683
- **Skip count:** 23 obfuscation-skips
- **Operation:** Replace the obfuscation-skip string → `[Fact]` for all 23 occurrences.
- **Exact lines of obfuscation-skip in this range:**
  6482, 6489, 6496, 6505, 6515, 6525, 6535, 6547, 6554, 6561,
  6590, 6597, 6604, 6611, 6618, 6627, 6634, 6641, 6648, 6657, 6664, 6671, 6678
- **DO NOT TOUCH:** `GetSenderAccountName_ShouldReturnEmpty_*` tests at L6570 and L6580 — these are NT8-runtime skips.
- **Verify gate:** `dotnet test --filter "FullyQualifiedName~BwaveCycT1R1BeHelperTests"` — 0 failed.

---

### T3 — BwaveCycTaR2HelperTests + BwaveCycTaR3HelperTests (12 + 33 = 45 removals)

- **File:** `src/PropTraderTools/CopyEngineTests.cs`
- **Line ranges:**
  - R2: L6701–L6791 (12 removals)
  - R3: L6830–L7115 (33 removals)
- **Skip count:** 45 obfuscation-skips total
- **Operation:** Replace the obfuscation-skip string → `[Fact]` for all 45 occurrences.
- **Exact lines — R2 portion:**
  6701, 6708, 6715, 6722, 6729, 6738, 6747, 6756, 6765, 6772, 6779, 6786
- **Exact lines — R3 portion:**
  6830, 6842, 6851, 6860, 6869, 6878, 6885, 6893, 6900, 6907, 6915, 6922, 6929,
  6953, 6960, 6967, 6974, 6983, 6990, 6997, 7004, 7011, 7018, 7025, 7032,
  7041, 7050, 7059, 7071, 7083, 7092, 7101, 7110
- **DO NOT TOUCH:**
  - R2: `GetSenderAccountName_ShouldBeReusedByOnTrailBeAccountUpdate` (L6806) — plain `[Fact]`.
  - R2: `OnTrailBeAccountUpdate_ShouldExist_AsPrivateMethod` (L6795) — plain `[Fact]`.
  - R3: `LogBeSlotEviction_ShouldExist_AsPrivateVoidMethod` (L6937) — plain `[Fact]`.
  - R3: `LogBeSlotEviction_ShouldAccept_AccNameAndIsRejectedParameters` (L6944) — plain `[Fact]`.
- **Verify gate:** `dotnet test --filter "FullyQualifiedName~BwaveCycTaR2HelperTests|BwaveCycTaR3HelperTests"` — 0 failed.

---

### T4 — BwaveCycTaR6HelperTests (10 removals)

- **File:** `src/PropTraderTools/CopyEngineTests.cs`
- **Line range:** L7131–L7272
- **Skip count:** 10 obfuscation-skips
- **Operation:** Replace the obfuscation-skip string → `[Fact]` for all 10 occurrences.
- **Exact lines of obfuscation-skip in this range:**
  7131, 7138, 7178, 7185, 7195, 7202, 7212, 7219, 7265, 7272
- **DO NOT TOUCH:**
  - `ExtractLegSuffix_*` tests at L7149, L7156, L7166 — plain `[Fact]` and NT8-runtime skips.
  - `IsPositionStateRelevant_*` tests at L7229, L7236, L7245, L7254 — NT8-runtime skips.
- **Verify gate:** `dotnet test --filter "FullyQualifiedName~BwaveCycTaR6HelperTests"` — 0 failed.

---

## 6. 7-Scan Checklist Template (Per Ticket)

Each ticket MUST complete this checklist before marking complete:

```
SCAN-01  ASCII-only: No Unicode, emoji, or curly quotes added.
SCAN-02  No lock(): no lock() added in CopyEngineTests.cs.
SCAN-03  No new throw: no throw statement added.
SCAN-04  No DateTime.Now: no DateTime.Now reference added.
SCAN-05  NT8-runtime skips untouched: grep 'NT8-runtime' count unchanged (335).
SCAN-06  Protected tests untouched: GetSenderAccountName / LogBeSlotEviction [Fact] count unchanged.
SCAN-07  dotnet test gate: 0 failed after ticket. Obfuscation-skip count decreased by expected delta.
```

---

## 7. Rollback Strategy

If ANY test fails after `[Fact(Skip=...)]` removal:

1. Immediately re-add the `Skip` string to the failing test: `[Fact(Skip = "obfuscation: AgileDotNetRT renames private members; cannot locate by string name")]`
2. Document the failure as a new deferred item: `DW-12-XX` in `06-deferred-backlog.md`.
3. Proceed with remaining tests in the ticket (do not abort the whole ticket for one failure).
4. Run `dotnet test` again after each rollback to confirm 0 failed.

The rollback operation is surgical: one test at a time, always maintaining the failed=0 invariant.

---

## 8. JS Rule Constraints

| Rule     | Constraint                          | Applicable? | Status |
|----------|-------------------------------------|-------------|--------|
| JS-021   | No lock()                           | No change   | PASS   |
| JS-001   | No throw in dispatch                | No change   | PASS   |
| ASCII    | ASCII-only identifiers/strings      | No change   | PASS   |
| CYC      | All methods <= 8 branches           | No change   | PASS   |
| DateTime | No DateTime.Now                     | No change   | PASS   |
| FontFamily | No FontFamily                     | N/A         | PASS   |

---

## 9. NT8 API Surface

Not applicable. No NT8 API is called in the modified tests. All test bodies call
`typeof(CopyEngine).GetMethod(...)` and `Assert.NotNull(m)`. No NT8 host required for
any of the 137 tests being un-skipped.

---

## 10. Backlog Closure

| Deferred Item | Description                                    | Status         |
|---------------|------------------------------------------------|----------------|
| DW-09-04      | Remove all remaining obfuscation-Skip (137 tests) | CLOSED by this epic |

---

## 11. Expected Final State

After T1+T2+T3+T4 complete with 0 failures:

```
dotnet test CopyEngineTests.cs
  Passed:  163
  Failed:  0
  Skipped: 351
  Total:   514
```

Obfuscation-skip count in CopyEngineTests.cs: **0**  
NT8-runtime-skip count in CopyEngineTests.cs: **335** (unchanged)

---

*Plan authored by PTT Architect — PTT-REPAIRS-12-SKIP-REMOVAL Phase 1*  
*Sequential thinking: 9 thoughts completed, all checks PASS.*
