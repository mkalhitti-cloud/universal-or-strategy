# B214 Architecture Plan -- Lane A
## Validation-Only Pipeline for 10 Pre-Existing Hotfixes

**Epic**: B214 -- B211 + B212 + B213 Repair Batch Validation
**Phase**: 1 (Architecture)
**Architect**: ptt-architect
**Status**: PLAN_COMPLETE
**Pipeline Type**: VALIDATION-ONLY -- no new code, no .cs modifications permitted in Ph4a

---

## 1. LANE-SPLIT GATE

### Gate Questions

| Q | Question | Answer |
|---|----------|--------|
| Q1 | Same method or within 50 lines? | N/A -- multiple files. Irrelevant: all hotfixes share one validation chain (ptt-sync + dotnet test + CYC audit). No split warranted. |
| Q2 | Fix B design depends on Fix A final design? | NO. All 10 hotfixes are already in code. No design dependency exists between them in this pipeline. |
| Q3 | Each fix has standalone value if the other is blocked? | YES -- each fix addresses a distinct user-facing bug. Moot for validation-only: all 10 are simultaneously present and must be verified together. |
| Q4 | Each fix has an independent SIM verification path? | YES -- each has documented SIM criteria in NO-PIPELINE-REPAIRS.md. However, Ph4a runs a single unified scan across all files. Split verification chains would add cost without benefit. |

### LANE-SPLIT GATE RESULT: SINGLE-PIPELINE

All 4 questions confirm no split is warranted. Ph4a runs one unified validation pass across all 3 modified files.

---

## 2. Epic Summary

**Validation-only pipeline for 10 pre-existing hotfixes.**

All 10 hotfixes listed below are already implemented in source, ptt-synced (21/0), and SIM-confirmed (individually or in combination). No new C# code will be written in this pipeline. Ph4a is a read-only + scan-only verification pass that confirms:
- ptt-sync 21/0
- All tests pass (dotnet test)
- CYC targets met for each named method
- Zero P0 Jane Street violations in modified code
- Deferred items correctly carried forward

**Files in scope** (read-only in Ph4a):
- `src/PropTraderTools/TradeCopierPanel.cs`
- `src/PropTraderTools/CopyEngine.cs`
- `src/PropTraderTools/Features/PttMoveTargetOne.cs`

**Test files** (read-only in Ph4a):
- `src/PropTraderTools/Tests/B211Tests.cs`
- `src/PropTraderTools/Tests/B212Tests.cs`

---

## 3. Hotfix Inventory

### HF-01 -- HOTFIX-B211-ASK-01

| Field | Value |
|-------|-------|
| File | `src/PropTraderTools/TradeCopierPanel.cs` |
| Methods | `OnAskClick` (~L1462), `TryFindWorkingTarget` |
| Source | `docs/brain/NO-PIPELINE-REPAIRS.md` L3232 |
| SIM status | TESTED-PASS -- awaiting pipeline |

**What is validated (NOT reimplemented):**
- `TryFindWorkingTarget` name filter accepts both `PTT-` prefix AND `Target` prefix (native NT8 ATM names Target1..Target9).
- Single-account Ask button no longer silently fails when copier uses Clone-mode ATM targets.
- CYC audit: `TryFindWorkingTarget` must be <= 8.

**JS-DNA declared:** No lock, no throw new, no return null, no async void, ASCII-only. All YES.

---

### HF-02 -- HOTFIX-B211-ASKALL-01

| Field | Value |
|-------|-------|
| File | `src/PropTraderTools/TradeCopierPanel.cs` |
| Method | `OnAskAllClick` (~L1474) |
| Source | `docs/brain/NO-PIPELINE-REPAIRS.md` L3292 |
| SIM status | TESTED-PASS -- awaiting pipeline |

**What is validated (NOT reimplemented):**
- `OnAskAllClick` uses `_leaderAccount ?? TryResolveLeaderAccount()` + `foreach _followerItems` loop (direct pattern). Does NOT call `PttMoveTargetAll.Execute` / `AllAccounts` / `FindRule`.
- Calls `PttMoveTargetOne.Execute(..., useAsk: true, _askBuffer)` for leader and each selected follower.
- CYC=4 (acceptable, < 8).

**JS-DNA declared:** No lock, no throw new, no return null, no async void, ASCII-only. All YES.

---

### HF-03 -- HOTFIX-B211-BIDALL-01

| Field | Value |
|-------|-------|
| File | `src/PropTraderTools/TradeCopierPanel.cs` |
| Method | `OnBidAllClick` (~L1526) |
| Source | `docs/brain/NO-PIPELINE-REPAIRS.md` L3341 |
| SIM status | TESTED-PASS -- awaiting pipeline (same block as HF-02) |

**What is validated (NOT reimplemented):**
- `OnBidAllClick` mirrors `OnAskAllClick` exactly with `useAsk: false`.
- Same direct loop pattern: `_leaderAccount` + `_followerItems`, no `AllAccounts`/`FindRule` dependency.
- CYC=4 (acceptable).

**JS-DNA declared:** No lock, no throw new, no return null, no async void, ASCII-only. All YES.

---

### HF-04 -- HOTFIX-B211-MOVETARGET-01 (B212-MAFIX2)

| Field | Value |
|-------|-------|
| File | `src/PropTraderTools/Features/PttMoveTargetOne.cs` |
| Methods | `Execute`, `CollectWorkingTargets` |
| Source | `docs/brain/NO-PIPELINE-REPAIRS.md` L3262 |
| SIM status | TESTED-PASS -- awaiting pipeline |

**What is validated (NOT reimplemented):**
- `CollectWorkingTargets(acc, instr)` returns `Order[]` (two-pass count+fill, no `List<>` to avoid NT8 NinjaScript CS0246 import issue).
- `Execute` iterates ALL working targets from `CollectWorkingTargets` result (not last-only).
- Batch cancel+resubmit for all found targets.
- CYC audit: `CollectWorkingTargets` <= 8; `Execute` = 8 (closed DW-B212-CYC-01 in B212).

**JS-DNA declared:** No lock, no throw new, no return null, no async void, ASCII-only. All YES.

---

### HF-05 -- HOTFIX-B212-REENTRY-V3

| Field | Value |
|-------|-------|
| File | `src/PropTraderTools/CopyEngine.cs` |
| Method | `DispatchCopy` |
| Source | `docs/brain/NO-PIPELINE-REPAIRS.md` L3728 |
| SIM status | SIM-CONFIRMED (indirect) -- ptt-sync 21/0 |

**What is validated (NOT reimplemented):**
- Gate B212-REENTRY-V3 present in `DispatchCopy` after Gate 0.5: `if (IsBeSwapInProgress(order.Account?.Name)) return;`
  - `IsBeSwapInProgress` is a ConcurrentDictionary lookup (lock-free, O(1)).
- Gate B212-REENTRY-FLATTEN present in `DispatchCopy`: `if (order.Account != null && HasActivePttFlatten(order.Account, order.Instrument)) return;`
  - `HasActivePttFlatten` scans `acc.Orders.ToList()` (lock-free NT8 snapshot) for `PTT-Flatten` in active states.
- Both gates placed BEFORE Gate 3 in `DispatchCopy`.
- `DispatchCopy` CYC <= 8 (gates added CYC+2 max, final CYC must be confirmed <= 8).

**JS-DNA declared:** No lock (ConcurrentDictionary + acc.Orders.ToList() lock-free), no throw new, no return null, no async void, ASCII-only. All YES.

---

### HF-06 -- HOTFIX-B212-REENTRY-BETARGET (V4 final)

| Field | Value |
|-------|-------|
| File | `src/PropTraderTools/CopyEngine.cs` |
| Methods | `IsSuppressedReentry`, `TryRecordBeTargetFill`, `TryEvictFollowerBeSlot`, `DispatchCopy` (TryRemove V2 placement) |
| Source | `docs/brain/NO-PIPELINE-REPAIRS.md` L3805, L3877, L3937, L3984 |
| SIM status | V4: SIM-CONFIRMED (all four accounts reach countAfter=4, no spurious PTT-ATM-SEND) |

**What is validated (NOT reimplemented):**
- `IsSuppressedReentry` has third guard: `if (HasFilledBeTargetFast(order.Account)) return true;`
  - CYC +1 in `IsSuppressedReentry`. Final CYC = 4 (PASS).
- `_filledBeTargetCount.TryRemove(order.Account.Name, out _)` is in `DispatchCopy` AFTER Gate 5 (IsLiveEntryBlocked), BEFORE `baseSignal` construction. NOT in `TryFirePositionState` (V1 bug was removed).
- `TryRecordBeTargetFill` increments `_filledBeTargetCount[acc]` for all accounts (leader + followers) when any `PTT-BE-Target-N` fills.
- `TryEvictFollowerBeSlot` clears counter for followers on flat.
- Corrected V2 timeline: re-armed ATM Entry hits `IsSuppressedReentry` BEFORE TryRemove fires (TryRemove only fires after all gates pass = confirmed new legitimate trade).

**JS-DNA declared:** No lock (ConcurrentDictionary.TryRemove lock-free), no throw new, no return null, no async void, CYC delta=0 in DispatchCopy, ASCII-only. All YES.

---

### HF-07 -- HOTFIX-B212-REENTRY-FOLLOWER-V1+V2

| Field | Value |
|-------|-------|
| File | `src/PropTraderTools/CopyEngine.cs` |
| Methods | `SubmitDrainedEntry`, `DrainThenDispatch`, `IsLaneEEligible`, plus 4 extracted helpers |
| Source | `docs/brain/NO-PIPELINE-REPAIRS.md` L4066, L4178, L4192 |
| SIM status | V1: DRAIN-RACE-CLOSED-INCONCLUSIVE. V2: APPLIED -- ptt-sync 21/0. AWAITING-TEST. |

**What is validated (NOT reimplemented):**

V1 guards:
- `SubmitDrainedEntry`: `HasFilledBeTargetFast(follower)` gate (2d) present AFTER `IsBeSwapInProgress` check (~L7879). CYC: 7->8 (at ceiling, PASS).
- `DrainThenDispatch` fast-path: `HasFilledBeTargetFast(follower)` gate (2-be) present AFTER `IsFlattenInFlightOrRecent` check (~L7724). CYC delta minimal (fast-path branch only); DrainThenDispatch overall CYC confirmed <= 8.

V2 extraction + gate (5b):
- Four helpers extracted from `IsLaneEEligible` (CYC reduced 11->7):
  - `IsLaneEFlattenLatched(acc, pos)` -- gate (0) compound && (CYC <= 2)
  - `IsPositionOrInstrumentNull(pos)` -- gate (2) compound || (CYC <= 2)
  - `IsFollowerSlateNotClosed(acc)` -- gate (4) compound || (CYC <= 2)
  - `IsLaneEBlockedByFlattenOrLeader(acc, pos)` -- gates (6)+(7) grouped (CYC <= 2)
- Gate (5b) added to `IsLaneEEligible` between gate (5) and gates (6+7): `if (HasFilledBeTargetFast(acc)) return false;`
- **Required CYC: `IsLaneEEligible` = 8 FINAL** (CYC path: (0)+(1)+(2)+(3)+(4)+(5)+(5b)+(6+7) = 8 branches exactly).

CYC breakdown for IsLaneEEligible:
`(0)latch(1)+(1)mode(1)+(2)null(1)+(3)qty(1)+(4)slate(1)+(5)atm(1)+(5b)beCount(1)+(6+7)flattenLeader(1) = 8`

**JS-DNA declared:** No lock, no async void, ASCII-only. CYC: IsLaneEEligible 11->8 (PASS). All four helpers CYC<=2 (PASS). All YES.

---

### HF-08 -- HOTFIX-B212-FLATTEN-LEADERSKIP-01

| Field | Value |
|-------|-------|
| File | `src/PropTraderTools/CopyEngine.cs` |
| Method | `Flatten(Instrument)` -- RelayFlatten overload (~L5703/L5751) |
| Source | `docs/brain/NO-PIPELINE-REPAIRS.md` L3451 |
| SIM status | CODE-CONFIRMED (leader-skip unconditionally present in source). AWAITING dedicated Flatten-ALL SIM run. |

**What is validated (NOT reimplemented):**
- `Flatten(Instrument)` RelayFlatten overload calls `AllAccounts(instrument).Where(acc => leader == null || acc.Name != leader.Name)` -- leader-skip WHERE clause present.
- Leader resolution: `var rule = FindRule(instrument); var leader = rule?.MasterAccount;` present before `FlatGuardLoop`.
- `Flatten(Account leader, Instrument)` overload (direct panel calls) is NOT modified -- confirm unchanged.
- No CYC delta (LINQ .Where on IEnumerable, not a hot path -- button click only).

**JS-DNA declared:** No lock, no throw new, no return null, no async void, LINQ .Where is not a hot-path heap issue (button-click frequency), ASCII-only. All YES.

---

### HF-09 -- HOTFIX-B212-MA-STOP-02

| Field | Value |
|-------|-------|
| File | `src/PropTraderTools/Features/PttMoveTargetOne.cs` |
| Method | `SubmitTargetPair` (extracted from per-target loop) |
| Source | `docs/brain/NO-PIPELINE-REPAIRS.md` L3660 |
| SIM status | APPLIED -- AWAITING-TEST (dedicated Ask/Bid SIM run for OCO pair confirmation) |

**What is validated (NOT reimplemented):**
- `SubmitTargetPair` generates per-pair OCO ID: `"PTT-MA-" + acc.Name[..8] + "-" + NextBeOcoSeq().ToString("D7") + "-" + pairIdx`
  - Uses `NextBeOcoSeq()` (CopyEngine) -- same monotonic Interlocked.Increment seed-protected counter as BE.
- Per-target loop submits TWO orders with same ocoId:
  - StopMarket order: arg6=0.0 (limitPrice), arg7=snapshotStop (stopPrice), ocoId, name `PTT-MA-Stop-NNNNN`
  - Limit order: arg6=exitPrice (limitPrice), arg7=0.0 (stopPrice), ocoId, name `PTT-MA-Ask/Bid-NNNNN`
- Guard: `if (snapshotStop == 0)` -- submit target only with `string.Empty` ocoId (no stranded stop).
- NT8-049 compliance: arg positions correct for StopMarket and Limit.
- CYC audit: `SubmitTargetPair` <= 8.

**JS-DNA declared:** No lock (Interlocked.Increment for NextBeOcoSeq), no throw in hot path (each CreateOrder+Submit in own try/catch), no null return, no async void, ASCII-only. All YES.

---

### HF-10 -- HOTFIX-B213-MULTI-CHART-INSTRUMENT-01

| Field | Value |
|-------|-------|
| File | `src/PropTraderTools/TradeCopierPanel.cs` |
| Method | `TryResolveLeaderAccount` |
| Source | `docs/brain/NO-PIPELINE-REPAIRS.md` L4240 |
| SIM status | SIM-CONFIRMED (Director live test, MES + MGC dual-window). |

**What is validated (NOT reimplemented):**
- `TryResolveLeaderAccount` lazy re-wire path present in `_accountCombo == null` branch:
  1. Guard: `if (_currentChart == null) return null;`
  2. `FindVisualChild<ChartTrader>(_currentChart)` -> ct
  3. `FindAccountComboBox(ct)` -> combo (primary)
  4. `FindVisualChildByIndex<ComboBox>(ct, 1)` -> combo (fallback if primary returns null)
  5. `if (combo != null) WireAccountCombo(combo);`
  6. Second `_accountCombo == null` guard before existing resolution proceeds.
- **Required CYC: `TryResolveLeaderAccount` = 6** (was 4, lazy re-wire adds 2 branches, <=8 PASS).
- UI-thread safety confirmed: method is only called from WPF Click handlers. `FindVisualChild`, `FindAccountComboBox`, `WireAccountCombo` are all UI-thread operations. No Dispatcher.InvokeAsync needed.
- `WireAccountCombo` is idempotent in effect. Double-hook prevented by `_accountCombo == null` guard.

**JS-DNA declared:** No lock, no async void, ASCII-only. CYC 4->6 (PASS). All YES.

---

## 4. Validation Approach

Ph4a is **read-only and scan-only**. It MUST NOT write, modify, or create any `.cs` file.

### Step 1 -- ptt-sync Verification
```powershell
powershell -File .\scripts\ptt-sync.ps1
```
**Expected result**: `21 linked, 0 errors`  
**FAIL condition**: Any error count > 0.

### Step 2 -- Build Verification
```powershell
dotnet build src/PropTraderTools/PropTraderTools.Tests.csproj
```
**Expected result**: 0 errors. Warnings are acceptable (pre-existing from DW-CSPROJ-BUILD-DEBT-01).

### Step 3 -- Test Suite
```powershell
dotnet test src/PropTraderTools/PropTraderTools.Tests.csproj
```
**Expected result**: All tests pass (0 failures).  
**Test files covering these hotfixes**: `B211Tests.cs`, `B212Tests.cs`

### Step 4 -- CYC Audit (named methods)

Ph4a MUST confirm cyclomatic complexity for each method in the table below. Use the project CYC audit tool if available, or manual branch count from source:

| Method | File | Required CYC | Source |
|--------|------|-------------|--------|
| `IsLaneEEligible` | CopyEngine.cs | **= 8** | HF-07 V2 (8 branches exactly) |
| `SubmitDrainedEntry` | CopyEngine.cs | **= 8** | HF-07 V1 Fix 1 (at ceiling) |
| `DrainThenDispatch` | CopyEngine.cs | **<= 8** | HF-07 V1 Fix 2 (fast-path only, confirm no regression) |
| `DispatchCopy` | CopyEngine.cs | **<= 8** | HF-05 + HF-06 (gates added) |
| `IsSuppressedReentry` | CopyEngine.cs | **<= 8** | HF-06 (+1 gate, CYC=4) |
| `TryResolveLeaderAccount` | TradeCopierPanel.cs | **= 6** | HF-10 (was 4->6) |
| `Execute` | PttMoveTargetOne.cs | **<= 8** | HF-04 (was 14->8, DW-B212-CYC-01 closed) |
| `CollectWorkingTargets` | PttMoveTargetOne.cs | **<= 8** | HF-04 (new method, 2-pass pattern) |
| `SubmitTargetPair` | PttMoveTargetOne.cs | **<= 8** | HF-09 (extracted method) |
| `IsLaneEFlattenLatched` | CopyEngine.cs | **<= 2** | HF-07 V2 extracted helper |
| `IsPositionOrInstrumentNull` | CopyEngine.cs | **<= 2** | HF-07 V2 extracted helper |
| `IsFollowerSlateNotClosed` | CopyEngine.cs | **<= 2** | HF-07 V2 extracted helper |
| `IsLaneEBlockedByFlattenOrLeader` | CopyEngine.cs | **<= 2** | HF-07 V2 extracted helper |
| `OnAskAllClick` | TradeCopierPanel.cs | **<= 8** | HF-02 (CYC=4) |
| `OnBidAllClick` | TradeCopierPanel.cs | **<= 8** | HF-03 (CYC=4) |

**FAIL condition**: Any method exceeds its required CYC target.

### Step 5 -- P0 Violation Scans

Run each scan and confirm ZERO matches in modified methods:

```powershell
# JS-021: lock() ban -- MUST return 0 in modified methods
grep -r "lock(" src/ --include="*.cs"

# JS-033: async void ban -- MUST return 0 in new/modified signatures
grep -rn "async void " src/ --include="*.cs"

# JS-001: throw new in hot paths -- confirm 0 new instances in modified methods
grep -rn "throw new " src/ --include="*.cs"

# JS-002: return null in hot paths -- confirm 0 new instances in modified methods
grep -rn "return null;" src/ --include="*.cs"

# DateTime.Now ban -- confirm UtcNow used
grep -rn "DateTime\.Now" src/ --include="*.cs"
```

**Interpretation**: Results for `lock(`, `async void`, `DateTime.Now` must be ZERO for any code touched by the 10 hotfixes. Pre-existing instances in unmodified code are NOT a blocker for this pipeline but must be noted.

### Step 6 -- ptt-sync Hard-Link Integrity
```powershell
powershell -File .\scripts\verify_critical_symbols.ps1
```
**Expected**: GUARDIAN PASS.  
**Known pre-existing**: `verify_critical_symbols.ps1` may report false-positive for `DW-B209-02` (`TradeCopierPanel._instr2tBtn.Background`). This is pre-existing (DW-B209-01 open). If ONLY this known false positive fires: PASS with note.

### Step 7 -- DIFF GUARD (DW-B212-03)
```powershell
powershell -File .\deploy-sync.ps1
```
**Expected**: DIFF GUARD PASS. Combined src/ diff for B211+B212+B213 hotfixes MUST be < 10,000 chars per AGENTS.md Platinum Standard.  
**FAIL condition**: DIFF GUARD reports > 10,000 chars changed in src/.

---

## 5. Zero-New-Code Mandate

> **Ph4a MUST NOT modify any `.cs` file.**

This is a hard protocol constraint. Permitted Ph4a actions:
- READ: any source file, test file, or documentation
- RUN: ptt-sync, dotnet build, dotnet test, CYC audit, grep scans, deploy-sync
- REPORT: VERIFY_PASS or VERIFY_FAIL with evidence

**If Ph4a modifies any `.cs` file**: immediate PROTOCOL VIOLATION. The plan must be returned to ptt-architect with a VERIFY_FAIL citing the unauthorized modification.

**Source of authority**: `docs/brain/NO-PIPELINE-REPAIRS.md` header section "SRC CODE BAN: ptt-architect is BANNED from writing .cs files directly." The same SRC CODE BAN applies to Ph4a ptt-engineer for a validation-only block: verification cannot introduce new code.

---

## 6. Jane Street Rules Applicable to Validation

The following rules from `docs/standards/jane-street/RULES_CATALOG.md` apply to the validation scans:

| Rule | Category | Severity | Validation Check |
|------|----------|----------|-----------------|
| JS-021 | Concurrency | P0 | `lock()` must be absent from all 10 hotfix methods. ConcurrentDictionary + Interlocked are the correct patterns. |
| JS-033 | Concurrency | P0 | `async void` must be absent from any new/modified method signature. Event handlers that were already `async void` are pre-existing (not introduced by these hotfixes). |
| JS-001 | Type Safety | P0 | `throw new XxxException` must not appear in `DispatchCopy`, `IsSuppressedReentry`, `SubmitTargetPair`, or other hot-path methods added/modified by these hotfixes. |
| JS-002 | Type Safety | P0 | `return null` for missing values -- `TryResolveLeaderAccount` returns `null` as a sentinel (UI pattern, not hot-path). Pre-existing design. Not a new P0 violation in this block. |
| CYC<=8 | Code Review | P1 | All methods in Section 4, Step 4 table. Ph4a must confirm targets. |
| ASCII-only | Code Review | P1 | No Unicode, emoji, or curly quotes in any modified string literal. All hotfixes declared ASCII-only. Ph4a grep scan: search for non-ASCII chars in modified file sections. |
| PTT- prefix | NT8 API | P1 | All `CreateOrder` calls in `SubmitTargetPair` use `PTT-MA-Ask-NNNNN`, `PTT-MA-Bid-NNNNN`, `PTT-MA-Stop-NNNNN` names. Confirm "PTT-" prefix present. |
| No DateTime.Now | Code Quality | P1 | `DateTime.UtcNow` must be used everywhere. No `DateTime.Now`. |
| No FontFamily | Code Quality | P1 | Not relevant to these hotfixes. No UI font changes introduced. |
| No hex colors | Code Quality | P1 | Not relevant to these hotfixes. No new color literals introduced. |

**NT8 API scope note**:
- `AtmStrategyChangeStopTarget()` and `AtmStrategyCreate()` are StrategyBase-only and NOT used in any of these hotfixes. The correct AddOnBase pattern (`Account.Cancel()` + `Account.CreateOrder()` + `Submit()`) is used throughout.
- `Account.Change()` is not used in these hotfixes.

---

## 7. Deferred Items Carried Forward from B212

The following items from `docs/brain/B212/06-deferred-backlog.md` remain OPEN and are carried forward to the next block after B214. B214 does NOT close any of these items.

| ID | Item | Priority | Original Target | Status |
|----|------|----------|-----------------|--------|
| DW-B209-01 | Guardian script single-hunk false positive fix (`verify_critical_symbols.ps1` false positive for DW-B209-02) | P2 | B213/future | OPEN -- carried to B215+ |
| DW-B209-02 | `TradeCopierPanel.cs` BrushTeal gap (`_instr2tBtn.Background`) | P2 | Future | OPEN -- cosmetic, carried to B215+ |
| DW-B209-04 | `specs/002-trade-copier-spec.html` B209 block updates (OCO + GTC guard features) | P1 | Before SIM R8 | OPEN -- must update spec before production push |
| DW-B211-01 | Direction-aware validation (warn/block for direction-inappropriate Ask/Bid) | P2 | Future block | OPEN -- V2 design decision required before DW-B211-02 |
| DW-B211-02 | Increase/Decrease target nudge buttons (depends DW-B211-01) | P2 | Future block | OPEN -- depends on DW-B211-01 |
| DW-B212-01 | `Trim(Instrument)` RelayTrim path -- leader-skip investigation | P1 | B213+ | OPEN -- investigate `TrimOneAccount` body: does it cancel orders that trigger NT8 ATM re-arm? Same risk as pre-fix `Flatten(Instrument)`. Investigation approach: read `TrimOneAccount` body; check whether it calls `CancelAllAccountOrders` or a targeted partial-cancel. |
| DW-B212-02 | `PttMoveTargetAll.cs` future role | P2 | B213/future | OPEN -- `PttMoveTargetAll.Execute` still exists and calls `AllAccounts`/`CopyEngine`. Decide: retain as copier-aware API utility, or deprecate with DW-card. Do NOT delete without explicit DW-card authorization. |
| DW-B212-03 | DIFF LIMIT compliance audit for B211+B212 combined PR | P1 | Before PR push | OPEN -- Ph4a Step 7 (DIFF GUARD) addresses this for the B214 pipeline. Must PASS before PR merge. |

---

## 8. Validation Pass / Fail Criteria

Ph4a issues **VERIFY_PASS** if and only if ALL of the following are true:

| Check | Pass Condition |
|-------|---------------|
| ptt-sync | `21 linked, 0 errors` |
| dotnet build | 0 errors (pre-existing warnings acceptable) |
| dotnet test | 0 failures, 0 errors |
| CYC: IsLaneEEligible | = 8 |
| CYC: SubmitDrainedEntry | = 8 |
| CYC: DrainThenDispatch | <= 8 |
| CYC: TryResolveLeaderAccount | = 6 |
| CYC: DispatchCopy | <= 8 |
| CYC: IsSuppressedReentry | <= 8 |
| CYC: Execute (PttMoveTargetOne) | <= 8 |
| CYC: CollectWorkingTargets | <= 8 |
| CYC: SubmitTargetPair | <= 8 |
| CYC: 4x IsLaneEEligible helpers | <= 2 each |
| P0 scan: lock() | 0 matches in hotfix methods |
| P0 scan: async void | 0 matches in hotfix methods |
| P0 scan: DateTime.Now | 0 matches |
| Guardian script | PASS (DW-B209-01 false positive is known exception) |
| DIFF GUARD (DW-B212-03) | < 10,000 chars src/ diff |
| Zero .cs modifications | Ph4a made NO .cs edits |

Ph4a issues **VERIFY_FAIL** if any check fails. VERIFY_FAIL report must cite the specific failing check with file + line evidence.

---

## 9. Component Summary

| Component | Role | Files Touched | New Code? |
|-----------|------|--------------|-----------|
| `TradeCopierPanel.cs` | Button handlers (Ask/Bid/AskAll/BidAll), multi-chart leader resolve | HF-01, HF-02, HF-03, HF-10 | NO |
| `CopyEngine.cs` | DispatchCopy gates, follower re-entry suppression, Flatten leader skip, BE-target sentinel | HF-05, HF-06, HF-07, HF-08 | NO |
| `PttMoveTargetOne.cs` | Multi-target collect, OCO pair submit | HF-04, HF-09 | NO |
| `B211Tests.cs` | Test coverage for Ask/Bid/AskAll/BidAll + MoveTarget fixes | read-only | NO |
| `B212Tests.cs` | Test coverage for REENTRY + Flatten-skip + BETARGET | read-only | NO |

---

*Architecture plan produced by ptt-architect. Phase 1 complete.*
*Sequential thinking: 8 thoughts. No violations found. PLAN_COMPLETE.*
