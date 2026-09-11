# Tickets — PTT-REPAIRS-12-SKIP-REMOVAL

**Epic:** PTT-REPAIRS-12-SKIP-REMOVAL  
**Phase:** 3 (Ticket Generation)  
**Author:** PTT Architect  
**Source plan:** `docs/brain/PTT-REPAIRS-12-SKIP-REMOVAL/02-architecture-plan.md` — REVIEW_PASS  
**Closes:** DW-09-04 (all remaining obfuscation-Skip annotations — 137 tests in CopyEngineTests.cs)

---

## Execution Order (HARD)

Tickets are executed **SEQUENTIALLY**. T1 must reach 0 failed before T2 starts.
T2 must reach 0 failed before T3 starts. T3 must reach 0 failed before T4 starts.
No parallel execution. No batching across tickets.

---

## Baseline / Target

| Metric  | Baseline | Target |
|---------|----------|--------|
| Passed  | 26       | 163    |
| Failed  | 0        | 0 (HARD) |
| Skipped | 488      | 351    |
| Total   | 514      | 514    |

---

## Global Invariants (ALL Tickets)

- **File under modification:** `src/PropTraderTools/CopyEngineTests.cs` ONLY.
- **No production `.cs` file is touched.**
- **No `deploy-sync.ps1` run required.**
- **NT8-runtime-skip count must remain 335 at all times.**
  `grep "NT8-runtime:" src/PropTraderTools/CopyEngineTests.cs` — count must stay exactly 335.
- **ASCII-only.** No Unicode, emoji, or curly quotes in any edit.
- **No `lock()` added.**
- **No `throw` statement added.**
- **No `DateTime.Now` reference added.**
- **No cyclomatic complexity (CYC) change.** Operation is attribute substitution only — no method body logic.
- **Do not modify already-passing (plain `[Fact]`) tests.**

---

## Exact Replacement Operation (ALL Tickets)

**SEARCH:** `[Fact(Skip = "obfuscation: AgileDotNetRT renames private members; cannot locate by string name")]`  
**REPLACE:** `[Fact]`

This is the sole edit performed per ticket — at the exact line numbers listed.
Do not edit any adjacent line, whitespace, or method body.

---

## T1 — B79CancelRaceGuardTests (59 obfuscation-skip removals)

**Spec requirement closed:** DW-09-04 (partial — 59 of 137)  
**File:** `src/PropTraderTools/CopyEngineTests.cs`  
**Test class in scope:** `B79CancelRaceGuardTests`  
**Line range:** L5968–L6460  
**Skip removals this ticket:** 59

### Binding Flags Context (Plan §3.1)

Class-level `GetMethod` helper (L5845–L5847):
```csharp
private static System.Reflection.MethodInfo GetMethod(string name) =>
    typeof(CopyEngine).GetMethod(name, System.Reflection.BindingFlags.NonPublic
        | System.Reflection.BindingFlags.Instance);
```
- All 57 obfuscation-skip tests that call `GetMethod` resolve **instance** methods. Correct as-is.
- The 2 `IsPositionFlatOrMissing` tests (L6357, L6367) use **inline** `BindingFlags.NonPublic | BindingFlags.Static` — no class helper involved. Correct as-is.
- **No structural additions required. Zero changes to helpers.**

### Exact Lines to Edit (59 total)

```
5968, 5976, 5984, 5992, 6002, 6010, 6018, 6026, 6036, 6043,
6050, 6059, 6066, 6075, 6082, 6100, 6107, 6114, 6123, 6130,
6139, 6148, 6157, 6164, 6173, 6180, 6189, 6199, 6209, 6219,
6231, 6238, 6247, 6254, 6261, 6270, 6277, 6284, 6293, 6300,
6309, 6316, 6325, 6332, 6341, 6348, 6357, 6367, 6379, 6386,
6393, 6400, 6409, 6416, 6423, 6432, 6439, 6448, 6455
```

### Do NOT Touch

- **NT8-runtime skips anywhere in the file** — `grep "NT8-runtime:"` count must remain 335.
- Any line not in the 59-line list above.

### Post-Implementation Verify Gate

```
dotnet test --filter "FullyQualifiedName~B79CancelRaceGuardTests"
```
**Required result:** 0 failed. Passed count increases by 59 (or fewer if rollback applies — see Rollback section).

### Rollback Protocol

If any test fails after `[Fact(Skip=...)]` removal:
1. Immediately re-add `[Fact(Skip = "obfuscation: AgileDotNetRT renames private members; cannot locate by string name")]` to that specific test.
2. Document the failure in `docs/brain/PTT-REPAIRS-12-SKIP-REMOVAL/06-deferred-backlog.md` as `DW-12-01` (increment suffix for each failing test).
3. Continue processing remaining tests in this ticket — do NOT abort the whole ticket for one failure.
4. Re-run `dotnet test --filter "FullyQualifiedName~B79CancelRaceGuardTests"` after each rollback. Must read 0 failed before proceeding to T2.

### 7-Scan Checklist — T1

Engineer must complete and attest each item before marking T1 done:

| Scan | Command / Check | Required Result |
|------|----------------|-----------------|
| SCAN-01 | `grep "lock(" src/PropTraderTools/CopyEngineTests.cs` | 0 matches |
| SCAN-02 | `grep "throw " src/PropTraderTools/CopyEngineTests.cs` — compare to pre-edit count | Count must NOT increase |
| SCAN-03 | CYC check — test file only; operation is attribute substitution, no method body logic | N/A — confirm no method body was edited |
| SCAN-04 | `grep "obfuscation:" src/PropTraderTools/CopyEngineTests.cs` — compare to pre-T1 baseline | Must decrease by exactly 59 (or 59 minus rollbacks) |
| SCAN-05 | `grep "NT8-runtime:" src/PropTraderTools/CopyEngineTests.cs` | Must remain exactly 335 |
| SCAN-06 | `dotnet build src/PropTraderTools/CopyEngineTests.cs` (or full solution build) | 0 errors, 0 warnings |
| SCAN-07 | `dotnet test --filter "FullyQualifiedName~B79CancelRaceGuardTests"` | 0 failed; passed count increases by N (N = 59 minus rollbacks) |

---

## T2 — BwaveCycT1R1BeHelperTests (23 obfuscation-skip removals)

**Spec requirement closed:** DW-09-04 (partial — 23 of 137)  
**Prerequisite:** T1 complete with 0 failed.  
**File:** `src/PropTraderTools/CopyEngineTests.cs`  
**Test class in scope:** `BwaveCycT1R1BeHelperTests`  
**Line range:** L6482–L6683  
**Skip removals this ticket:** 23

### Binding Flags Context (Plan §3.2)

Class-level `GetMethod` helper (L6477–L6478):
```csharp
private static MethodInfo GetMethod(string name) =>
    typeof(CopyEngine).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance);
```
- All 23 obfuscation-skip tests target **instance** methods:
  `GetMarketBidPrice`, `GetMarketAskPrice`, `GetBeTickSize`, `SelectBeRefPriceByDirection`,
  `FireBeAndNotifyEvent`, `ShouldFireBeImmediately`, `CompleteBeArming`, `TryClaimPendingBeSlot`,
  `GetSlotInstrumentName`, `GetSlotAccountName`, `RaisePendingBeFiredEvent`,
  `SettleAndFirePendingBe`, `TryFireImmediateBeIfAlreadyAtLevel`, `IsPendingBeTriggerMet`.
- Binding flags are correct for all 23. **No structural additions required.**

### Exact Lines to Edit (23 total)

```
6482, 6489, 6496, 6505, 6515, 6525, 6535, 6547, 6554, 6561,
6590, 6597, 6604, 6611, 6618, 6627, 6634, 6641, 6648, 6657,
6664, 6671, 6678
```

### Do NOT Touch

- **L6570 and L6580** — `GetSenderAccountName_ShouldReturnEmpty_*` tests. These carry NT8-runtime skips, not obfuscation skips. Do NOT modify.
- **NT8-runtime skips anywhere in the file** — `grep "NT8-runtime:"` count must remain 335.
- Any line not in the 23-line list above.

### Post-Implementation Verify Gate

```
dotnet test --filter "FullyQualifiedName~BwaveCycT1R1BeHelperTests"
```
**Required result:** 0 failed. Passed count increases by 23 (or fewer if rollback applies).

### Rollback Protocol

If any test fails after `[Fact(Skip=...)]` removal:
1. Immediately re-add `[Fact(Skip = "obfuscation: AgileDotNetRT renames private members; cannot locate by string name")]` to that specific test.
2. Document the failure in `docs/brain/PTT-REPAIRS-12-SKIP-REMOVAL/06-deferred-backlog.md` as `DW-12-02` (increment suffix for each failing test).
3. Continue processing remaining tests in this ticket — do NOT abort the whole ticket for one failure.
4. Re-run `dotnet test --filter "FullyQualifiedName~BwaveCycT1R1BeHelperTests"` after each rollback. Must read 0 failed before proceeding to T3.

### 7-Scan Checklist — T2

Engineer must complete and attest each item before marking T2 done:

| Scan | Command / Check | Required Result |
|------|----------------|-----------------|
| SCAN-01 | `grep "lock(" src/PropTraderTools/CopyEngineTests.cs` | 0 matches |
| SCAN-02 | `grep "throw " src/PropTraderTools/CopyEngineTests.cs` — compare to post-T1 count | Count must NOT increase |
| SCAN-03 | CYC check — test file only; operation is attribute substitution, no method body logic | N/A — confirm no method body was edited |
| SCAN-04 | `grep "obfuscation:" src/PropTraderTools/CopyEngineTests.cs` — compare to post-T1 count | Must decrease by exactly 23 (or 23 minus rollbacks) |
| SCAN-05 | `grep "NT8-runtime:" src/PropTraderTools/CopyEngineTests.cs` | Must remain exactly 335 |
| SCAN-06 | `dotnet build` | 0 errors, 0 warnings |
| SCAN-07 | `dotnet test --filter "FullyQualifiedName~BwaveCycT1R1BeHelperTests"` | 0 failed; passed count increases by N (N = 23 minus rollbacks) |

---

## T3 — BwaveCycTaR2HelperTests + BwaveCycTaR3HelperTests (12 + 33 = 45 obfuscation-skip removals)

**Spec requirement closed:** DW-09-04 (partial — 45 of 137)  
**Prerequisite:** T2 complete with 0 failed.  
**File:** `src/PropTraderTools/CopyEngineTests.cs`  
**Test classes in scope:** `BwaveCycTaR2HelperTests` AND `BwaveCycTaR3HelperTests`  
**Line ranges:**
  - R2 portion: L6701–L6791 (12 removals)
  - R3 portion: L6830–L7115 (33 removals)  
**Skip removals this ticket:** 45 total

### Binding Flags Context (Plan §3.3 and §3.4)

**BwaveCycTaR2HelperTests** class-level helpers (L6694–L6697):
```csharp
private static MethodInfo GetMethod(string name) =>
    typeof(CopyEngine).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance);
private static MethodInfo GetStaticMethod(string name) =>
    typeof(CopyEngine).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static);
```
`GetStaticMethod` **already present** (added by PTT-REPAIRS-11-BINDING-FLAGS-03).  
All 12 R2 obfuscation-skip tests call `GetMethod` (instance): `HasValidTargetNameSuffix`,
`IsLeaderTargetOrder` (×4), `SelectBeTargetList`, `IsBeTargetActiveState`,
`IsBeTargetPendingChangeState`, `IsBeTargetSnapshotState`, `IsEligibleBeTargetOrder` (×3).  
**Zero additions required.**

**BwaveCycTaR3HelperTests** class-level helpers (L6822–L6826):
```csharp
private static MethodInfo GetMethod(string name) =>
    typeof(CopyEngine).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance);
private static MethodInfo GetStaticMethod(string name) =>
    typeof(CopyEngine).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static);
```
`GetStaticMethod` **already present** (added by PTT-REPAIRS-11-BINDING-FLAGS-02).  
All 33 R3 obfuscation-skip tests call `GetMethod` (instance).  
**Zero additions required.**

### Exact Lines to Edit — R2 Portion (12 lines)

```
6701, 6708, 6715, 6722, 6729, 6738, 6747, 6756, 6765, 6772, 6779, 6786
```

### Exact Lines to Edit — R3 Portion (33 lines)

```
6830, 6842, 6851, 6860, 6869, 6878, 6885, 6893, 6900, 6907, 6915, 6922, 6929,
6953, 6960, 6967, 6974, 6983, 6990, 6997, 7004, 7011, 7018, 7025, 7032,
7041, 7050, 7059, 7071, 7083, 7092, 7101, 7110
```

### Do NOT Touch — PROTECTED TESTS (Explicit Mandate)

These tests are already plain `[Fact]` and must not be modified in any way:

| Test Name | Line | Class | Reason |
|-----------|------|-------|--------|
| `GetSenderAccountName_ShouldBeReusedByOnTrailBeAccountUpdate` | L6806 | R2 | Already-fixed plain `[Fact]`; uses `GetStaticMethod` |
| `OnTrailBeAccountUpdate_ShouldExist_AsPrivateMethod` | L6795 | R2 | Already-fixed plain `[Fact]` |
| `LogBeSlotEviction_ShouldExist_AsPrivateVoidMethod` | L6937 | R3 | Already-fixed plain `[Fact]`; uses `GetStaticMethod` |
| `LogBeSlotEviction_ShouldAccept_AccNameAndIsRejectedParameters` | L6944 | R3 | Already-fixed plain `[Fact]`; uses `GetStaticMethod` |

- **NT8-runtime skips anywhere in the file** — `grep "NT8-runtime:"` count must remain 335.
- Any line not in the R2 (12) or R3 (33) edit lists above.

### Post-Implementation Verify Gate

```
dotnet test --filter "FullyQualifiedName~BwaveCycTaR2HelperTests|BwaveCycTaR3HelperTests"
```
**Required result:** 0 failed. Passed count increases by 45 (or fewer if rollback applies).

### Rollback Protocol

If any test fails after `[Fact(Skip=...)]` removal:
1. Immediately re-add `[Fact(Skip = "obfuscation: AgileDotNetRT renames private members; cannot locate by string name")]` to that specific test.
2. Document the failure in `docs/brain/PTT-REPAIRS-12-SKIP-REMOVAL/06-deferred-backlog.md` as `DW-12-03` (increment suffix for each failing test).
3. Continue processing remaining tests in this ticket — do NOT abort the whole ticket for one failure.
4. Re-run the verify gate after each rollback. Must read 0 failed before proceeding to T4.

### 7-Scan Checklist — T3

Engineer must complete and attest each item before marking T3 done:

| Scan | Command / Check | Required Result |
|------|----------------|-----------------|
| SCAN-01 | `grep "lock(" src/PropTraderTools/CopyEngineTests.cs` | 0 matches |
| SCAN-02 | `grep "throw " src/PropTraderTools/CopyEngineTests.cs` — compare to post-T2 count | Count must NOT increase |
| SCAN-03 | CYC check — test file only; operation is attribute substitution, no method body logic | N/A — confirm no method body was edited |
| SCAN-04 | `grep "obfuscation:" src/PropTraderTools/CopyEngineTests.cs` — compare to post-T2 count | Must decrease by exactly 45 (or 45 minus rollbacks) |
| SCAN-05 | `grep "NT8-runtime:" src/PropTraderTools/CopyEngineTests.cs` | Must remain exactly 335 |
| SCAN-06 | `dotnet build` | 0 errors, 0 warnings |
| SCAN-07 | `dotnet test --filter "FullyQualifiedName~BwaveCycTaR2HelperTests\|BwaveCycTaR3HelperTests"` | 0 failed; passed count increases by N (N = 45 minus rollbacks) |

---

## T4 — BwaveCycTaR6HelperTests (10 obfuscation-skip removals)

**Spec requirement closed:** DW-09-04 (final closure — 10 of 137; total 137 after T1+T2+T3+T4)  
**Prerequisite:** T3 complete with 0 failed.  
**File:** `src/PropTraderTools/CopyEngineTests.cs`  
**Test class in scope:** `BwaveCycTaR6HelperTests`  
**Line range:** L7131–L7272  
**Skip removals this ticket:** 10

### Binding Flags Context (Plan §3.5)

Class-level helpers (L7123–L7127):
```csharp
private static MethodInfo GetStaticMethod(string name) =>
    typeof(CopyEngine).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static);
private static MethodInfo GetInstanceMethod(string name) =>
    typeof(CopyEngine).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance);
```
Both helpers **already present**. Note: this class uses `GetStaticMethod` and `GetInstanceMethod` by name — there is NO unnamed `GetMethod` in this class.

Per-test binding for the 10 removals:

| Test (line) | Helper Used | Binding |
|-------------|-------------|---------|
| `IsBracketOrderLiveState_ShouldExist...` (L7131) | `GetStaticMethod` | STATIC |
| `IsBracketOrderLiveState_ShouldReturnTrue...` (L7138) | `GetStaticMethod` | STATIC |
| `MatchesPttReplacementName_ShouldExist...` (L7178) | `GetStaticMethod` | STATIC |
| `MatchesPttReplacementName_ShouldAccept...` (L7185) | `GetStaticMethod` | STATIC |
| `LogHbcDiag_ShouldExist...` (L7195) | `GetInstanceMethod` | INSTANCE |
| `LogHbcDiag_ShouldAccept...` (L7202) | `GetInstanceMethod` | INSTANCE |
| `ExecuteStopDragOrder_ShouldExist...` (L7212) | `GetInstanceMethod` | INSTANCE |
| `ExecuteStopDragOrder_ShouldAccept...` (L7219) | `GetInstanceMethod` | INSTANCE |
| `IsOrderEventProcessable_ShouldExist...` (L7265) | `GetStaticMethod` | STATIC |
| `IsOrderEventProcessable_ShouldAccept...` (L7272) | `GetStaticMethod` | STATIC |

All binding flags match declared production method visibility. **No structural additions required.**

### Exact Lines to Edit (10 total)

```
7131, 7138, 7178, 7185, 7195, 7202, 7212, 7219, 7265, 7272
```

### Do NOT Touch

- **L7149, L7156, L7166** — `ExtractLegSuffix_*` tests. These are plain `[Fact]` and NT8-runtime skips respectively. Do NOT modify.
- **L7229, L7236, L7245, L7254** — `IsPositionStateRelevant_*` tests. These carry NT8-runtime skips. Do NOT modify.
- **NT8-runtime skips anywhere in the file** — `grep "NT8-runtime:"` count must remain 335.
- Any line not in the 10-line list above.

### Post-Implementation Verify Gate

```
dotnet test --filter "FullyQualifiedName~BwaveCycTaR6HelperTests"
```
**Required result:** 0 failed. Passed count increases by 10 (or fewer if rollback applies).

### Final State Verify Gate (after T4 complete)

```
dotnet test src/PropTraderTools/CopyEngineTests.cs
```
**Required result:**
```
Passed:  163
Failed:  0
Skipped: 351
Total:   514
```

Confirm:
- `grep "obfuscation:" src/PropTraderTools/CopyEngineTests.cs` → **0 matches**
- `grep "NT8-runtime:" src/PropTraderTools/CopyEngineTests.cs` → **335 matches**

DW-09-04 is CLOSED.

### Rollback Protocol

If any test fails after `[Fact(Skip=...)]` removal:
1. Immediately re-add `[Fact(Skip = "obfuscation: AgileDotNetRT renames private members; cannot locate by string name")]` to that specific test.
2. Document the failure in `docs/brain/PTT-REPAIRS-12-SKIP-REMOVAL/06-deferred-backlog.md` as `DW-12-04` (increment suffix for each failing test).
3. Continue processing remaining tests in this ticket — do NOT abort the whole ticket for one failure.
4. Re-run `dotnet test --filter "FullyQualifiedName~BwaveCycTaR6HelperTests"` after each rollback. Must read 0 failed before closing the epic.

### 7-Scan Checklist — T4

Engineer must complete and attest each item before marking T4 done:

| Scan | Command / Check | Required Result |
|------|----------------|-----------------|
| SCAN-01 | `grep "lock(" src/PropTraderTools/CopyEngineTests.cs` | 0 matches |
| SCAN-02 | `grep "throw " src/PropTraderTools/CopyEngineTests.cs` — compare to post-T3 count | Count must NOT increase |
| SCAN-03 | CYC check — test file only; operation is attribute substitution, no method body logic | N/A — confirm no method body was edited |
| SCAN-04 | `grep "obfuscation:" src/PropTraderTools/CopyEngineTests.cs` — compare to post-T3 count | Must decrease by exactly 10 (or 10 minus rollbacks); final value must be 0 |
| SCAN-05 | `grep "NT8-runtime:" src/PropTraderTools/CopyEngineTests.cs` | Must remain exactly 335 |
| SCAN-06 | `dotnet build` | 0 errors, 0 warnings |
| SCAN-07 | `dotnet test src/PropTraderTools/CopyEngineTests.cs` (full suite) | 0 failed; Passed=163, Skipped=351, Total=514 |

---

## JS Rule Constraints Summary (All Tickets)

| Rule | Constraint | Applicable | Status |
|------|-----------|------------|--------|
| JS-021 | No `lock()` | No change (SCAN-01 enforces) | PASS |
| JS-001 | No `throw` in dispatch | No change (SCAN-02 enforces) | PASS |
| ASCII | ASCII-only identifiers/strings | No change | PASS |
| CYC | All methods <= 8 branches | No change (SCAN-03 confirms) | PASS |
| DateTime | No `DateTime.Now` | No change | PASS |
| FontFamily | No FontFamily | N/A | PASS |

---

## Deferred Backlog Convention

If any rollback item is created during execution, document it in `docs/brain/PTT-REPAIRS-12-SKIP-REMOVAL/06-deferred-backlog.md` using:

```
| DW-12-0N | <MethodName> test in <ClassName> fails after obfuscation-skip removal | OPEN |
```

Increment `N` per failing test. These items represent obfuscation renames not yet resolved.

---

*Tickets authored by PTT Architect — PTT-REPAIRS-12-SKIP-REMOVAL Phase 3*  
*Source plan: REVIEW_PASS confirmed by PTT Plan Reviewer*  
*Total skip removals: 137 (T1=59 + T2=23 + T3=45 + T4=10)*
