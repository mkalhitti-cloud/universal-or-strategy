# DW-B159 Plan Review — Final Pass (Pass 3)
## Ghost Re-entry Guard + BE-All Silent Rollback Repair
**Wave:** B159  
**Epic:** DW-B159  
**Phase:** 2 — Plan Review (Final Pass — verify all 9 violations resolved)  
**Reviewer:** ptt-plan-reviewer  
**Input:** docs/brain/B159/02-architecture-plan.md (triply-revised — status: CORRECTED V-07/V-08/V-09)  
**Prior review:** docs/brain/B159/02-plan-review.md (second pass — 3 violations V-07/V-08/V-09)  
**Source truth:** src/PropTraderTools/CopyEngine.cs (READ ONLY)  
**Verdict:** **REVIEW_PASS**

---

## Prior Violation Resolution Status — All 9 Violations

| ID | Prior Description | Plan Fix | Source Verified | Status |
|----|-------------------|----------|----------------|--------|
| V-01 | `SubmitBeStop` cited at line 451 | Now cites line **1389** | grep: line 1389 ✓ | **RESOLVED** |
| V-02 | `ArmAllPendingBe` cited at line 478 | Now cites line **1482** | grep: line 1482 ✓ | **RESOLVED** |
| V-03 | `MoveStopToBreakEven` cited at line 1542 | Now cites line **6816** | grep: line 6816 ✓ | **RESOLVED** |
| V-04 | Insertion anchor "near line 139" | Now cites **after line 322** (`_beReplaceAttempts` ends at 322) | read_file 318–328 confirmed ✓ | **RESOLVED** |
| V-05 | `StillAtNonBePrice` CYC stated as 4 | Now states **CYC=7** with corrected branch table | Budget table updated to 7 ✓ | **RESOLVED** |
| V-06 | No Working-state cleanup path | `TryFireBePriceRetry` branch (5) handles `OrderState.Working`: `TryRemove` both dicts, log, return | FM-B-T4 test references branch (5) ✓ | **RESOLVED** |
| V-07 | LANES-APPROVED table had Q1=YES, Q3=NO, Q4=NO (wrong) | Q values corrected: Q1=No, Q2=No, Q3=Yes, Q4=Yes; LANES-APPROVED stated | Plan LANE-SPLIT GATE section ✓ | **RESOLVED** |
| V-08 | `TryFireBePriceRetry` hook placed AFTER `TryTriggerBeRecovery` (after line 1726) | Hook now at line 1725a — AFTER `TryRecordBeTargetFill` (1725), BEFORE `TryTriggerBeRecovery` (1726) | Plan Hook 2 + call order table ✓ | **RESOLVED** |
| V-09 | JS-021: `StillAtNonBePrice` iterated `acc.Orders` without `.ToList()` snapshot | Pseudo-code now: `foreach (var o in acc.Orders.ToList())` with comment `JS-021: snapshot live collection` | Plan line 220 ✓ | **RESOLVED** |

All 9 prior violations are confirmed resolved in the triply-revised plan.

---

## Check 1: LANE-SPLIT GATE

**Result: PASS**

| Q | Question | Answer in Plan | Required | Verdict |
|---|----------|----------------|----------|---------|
| Q1 | Are the two feature modules within 50 lines of each other? | **NO** — FM-A at line 3056, FM-B at lines 1697/6816 (1359+ lines apart) | No | ✅ |
| Q2 | Does FM-B depend on FM-A's final design? | **NO** — Independent data structures and call paths | No | ✅ |
| Q3 | Does each fix have standalone value if the other is blocked? | **YES** — FM-A (ghost guard) and FM-B (BE-retry) each independently shippable | Yes | ✅ |
| Q4 | Does each fix have an independent SIM verification path? | **YES** — `[PTT-COPY-GUARD]` for FM-A; `[PTT-BE-RETRY]` for FM-B | Yes | ✅ |

**Result: LANES-APPROVED.** All new code stays in `src/PropTraderTools/CopyEngine.cs`. No new files.

---

## Check 2: Source Line Accuracy

**Result: PASS**

All spot-check symbols verified against `src/PropTraderTools/CopyEngine.cs`:

| Symbol | Plan Line | Source Line | Delta | Status |
|--------|-----------|-------------|-------|--------|
| `ShouldSkipFollower` | 3056 | 3056 | 0 | ✅ PASS |
| `ShouldSkipFollowerDispatch` | 3008 | 3008 | 0 | ✅ PASS |
| `HasOpenPosition` | 5273 | 5273 | 0 | ✅ PASS |
| `DispatchCopy` | 2679 | 2679 | 0 | ✅ PASS |
| `OnOrderUpdate` | 1697 | 1697 | 0 | ✅ PASS |
| `TryTriggerBeRecovery` | 4974 | 4974 | 0 | ✅ PASS |
| `TryRecordBeTargetFill` | 4951 | 4951 | 0 | ✅ PASS |
| `SubmitBeStop` | 1389 | 1389 | 0 | ✅ PASS (V-01 resolved) |
| `ArmAllPendingBe` | 1482 | 1482 | 0 | ✅ PASS (V-02 resolved) |
| `MoveStopToBreakEven` | 6816 | 6816 | 0 | ✅ PASS (V-03 resolved) |
| `_beReplaceAttempts` anchor (after 322) | after 322 | line 321–322, next field `_qxCancelInProgress` at 327 | 0 | ✅ PASS (V-04 resolved) |
| `TryRecordBeTargetFill` in `OnOrderUpdate` | 1725 | 1725 | 0 | ✅ PASS |
| `TryTriggerBeRecovery` in `OnOrderUpdate` | 1726 | 1726 | 0 | ✅ PASS |
| `PttBreakEvenSwap.Execute` in `MoveStopToBreakEven` | 6856 | 6856 | 0 | ✅ PASS |
| `StatusUpdate?.Invoke` after Execute | 6857 | 6857 | 0 | ✅ PASS |

Zero delta across all spot-check symbols.

**Confirmed absent from source (new — no pre-existing collision):** `IsFollowerPositionBlocked`, `TryArmBePriceRetry`, `TryFireBePriceRetry`, `StillAtNonBePrice`, `_pendingBePriceRetry`, `_bePriceRetryAttempts` — all confirmed absent ✓.

---

## Check 3: FM-A Design Integrity

**Result: PASS**

| Sub-check | Result | Evidence |
|-----------|--------|----------|
| `IsFollowerPositionBlocked` is new (absent from source) | PASS | grep: 0 matches in CopyEngine.cs |
| Hook at `ShouldSkipFollower` as **first** statement | PASS | Plan inserts `IsFollowerPositionBlocked` guard before `ShouldSkipFollowerDispatch` call |
| Delegates to `HasOpenPosition` (line 5273) — no duplicate logic | PASS | Pseudo-code: `return HasOpenPosition(acc, instr);` |
| CYC=3 for `IsFollowerPositionBlocked` | PASS | base(1) + acc-null(1) + instr-null(1) = 3 ✓ |
| `ShouldSkipFollower` rises to CYC=4 after hook | PASS | Adds 1 branch to existing CYC=3 → 4, within ≤8 ✓ |
| Log marker `[PTT-COPY-GUARD] ghost-entry blocked: {acc.Name} {instr.FullName}` ASCII-only | PASS | No Unicode, no emoji, no curly quotes ✓ |
| No `lock()` | PASS | No lock in `IsFollowerPositionBlocked` or hook |
| No `throw` in gate chain | PASS | All branches use early `return true` or delegate |
| No `DateTime.Now` | PASS | No date usage in any FM-A code |
| No Unicode in string literals | PASS | Log string verified ASCII-only |
| `internal` visibility for xUnit via `InternalsVisibleTo` | PASS | Declared `internal bool IsFollowerPositionBlocked(...)` |

---

## Check 4: FM-B Design Integrity

**Result: PASS**

| Sub-check | Result | Evidence |
|-----------|--------|----------|
| Two new `ConcurrentDictionary` fields with `StringComparer.Ordinal` | PASS | `_pendingBePriceRetry` + `_bePriceRetryAttempts` both declare `StringComparer.Ordinal` ✓ |
| JS-021: `ConcurrentDictionary` not `Dictionary` | PASS | Both fields typed as `ConcurrentDictionary<string, T>` ✓ |
| Field insertion anchor correct (after line 322) | PASS | V-04 resolved; `_beReplaceAttempts` ends line 322, next field at 327 ✓ |
| `TryFireBePriceRetry` placed AFTER `TryRecordBeTargetFill` BEFORE `TryTriggerBeRecovery` | PASS | V-08 resolved: call order table shows line 1725 → 1725a → 1726 ✓ |
| `TryArmBePriceRetry` hook in `MoveStopToBreakEven` after `StatusUpdate?.Invoke` (line 6857) | PASS | Hook 1 inserts after line 6857, source confirms `StatusUpdate?.Invoke` at line 6857 ✓ |
| `StillAtNonBePrice` uses `acc.Orders.ToList()` snapshot | PASS | V-09 resolved: pseudo-code line 220 `foreach (var o in acc.Orders.ToList())` with JS-021 comment ✓ |
| CYC `TryArmBePriceRetry` = 5 | PASS | base(1)+acc-null(1)+instr-null(1)+bePrice-guard(1)+attempts-ceiling(1) = 5 ✓ |
| CYC `TryFireBePriceRetry` = 7 | PASS | base(1)+compound-null(1)+name-prefix(1)+no-slot(1)+Working(1)+not-Cancelled(1)+StillAtNonBePrice(1) = 7 ✓ |
| CYC `StillAtNonBePrice` = 7 (V-05 corrected) | PASS | base(1)+acc-null(1)+instr-null(1)+foreach(1)+name-guard(1)+instr-match(1)+state-guard(1) = 7 ✓ |
| All new/modified methods CYC ≤ 8 | PASS | CYC budget table: max is 7 across all methods ✓ |
| Attempt cap = 3 per account per trade | PASS | `if (attempts >= 3) return;` in `TryArmBePriceRetry` branch (5) ✓ |
| Working-state cleanup via `TryFireBePriceRetry` branch (5) | PASS | V-06 resolved: `if (o.OrderState == OrderState.Working)` → TryRemove both dicts, log, return ✓ |
| All log markers ASCII-only | PASS | `[PTT-BE-RETRY]` prefix and all markers verified ASCII ✓ |
| No `lock()` anywhere in new methods | PASS | All state via `ConcurrentDictionary` TryAdd/TryGetValue/TryRemove ✓ |
| No `throw` in any new method | PASS | All branches early `return` ✓ |
| No `DateTime.Now` | PASS | No date usage in any FM-B code ✓ |
| `internal` visibility for xUnit | PASS | All new methods declared `internal` ✓ |

---

## Check 5: Test Coverage

**Result: PASS**

**FM-A — 4 tests with concrete paths:**

| Test | Method | Stimulus | Expected | Concrete Path |
|------|--------|----------|----------|---------------|
| FM-A-T1 | `IsFollowerPositionBlocked` | acc has no open positions (Qty=0) | returns `false` | `HasOpenPosition` returns false → method returns false |
| FM-A-T2 | `IsFollowerPositionBlocked` | acc has open position Qty>0 | returns `true` | `HasOpenPosition` returns true → method returns true |
| FM-A-T3 | `ShouldSkipFollower` | follower has open position on instrument | returns `true` (blocked) | First branch fires, `IsFollowerPositionBlocked` returns true → skip |
| FM-A-T4 | `ShouldSkipFollower` | follower is flat on instrument | first guard passes, falls through to existing logic | `IsFollowerPositionBlocked` returns false → existing `ShouldSkipFollowerDispatch` path continues |

**FM-B — 5 tests with concrete paths:**

| Test | Method | Stimulus | Expected | Concrete Path |
|------|--------|----------|----------|---------------|
| FM-B-T1 | `TryArmBePriceRetry` | acc=valid, instr=valid, bePrice=4825.0 | `_pendingBePriceRetry[acc.Name] == 4825.0` | All guards pass → dict assignment executes |
| FM-B-T2 | `TryFireBePriceRetry` | PTT-BE-Stop-* Cancelled, entry in dict, StillAtNonBePrice=true | `SubmitBeStop` called, dict entries removed | Branch (6) Cancelled + branch (7) StillAtNonBePrice passes → fire path |
| FM-B-T3 | `TryFireBePriceRetry` | PTT-BE-Stop-* Cancelled, no entry in `_pendingBePriceRetry` | returns without action | Branch (4) TryGetValue fails → early return |
| FM-B-T4 | `TryFireBePriceRetry` | PTT-BE-Stop-* **Working**, entry exists in dict | `TryRemove` both dicts, no `SubmitBeStop` called | Branch (5) Working → cleanup + return (V-06 fix) |
| FM-B-T5 | `StillAtNonBePrice` | working PTT-BE-Stop-* order found with price == bePrice | returns `false` (already at BE) | Inner `return Math.Abs(...)` evaluates ≤ tick*0.5 → returns false |

---

## Check 6: NT8 Constraints

**Result: PASS**

| Constraint | Status |
|-----------|--------|
| No `lock()` anywhere in new methods | PASS — All state via `ConcurrentDictionary` ✓ |
| No `async void` | PASS — No async in new methods ✓ |
| No `DateTime.Now` | PASS — No date usage ✓ |
| No `async`/`await` in lifecycle hooks | PASS — No lifecycle hooks touched by new methods ✓ |
| `ConcurrentDictionary<K,V>` with `StringComparer.Ordinal` (JS-021) | PASS — Both fields declared with `StringComparer.Ordinal` ✓ |
| `acc.Orders` enumeration uses `.ToList()` snapshot (JS-021) | PASS — V-09 resolved: `foreach (var o in acc.Orders.ToList())` in `StillAtNonBePrice` ✓ |
| `AtmStrategyCreate` — StrategyBase-only, NOT used | PASS — Not used in any new method ✓ |
| `AtmStrategyChangeStopTarget` — StrategyBase-only, NOT used | PASS — Not used ✓ |
| `Account.Change()` — silent no-op on ATM-owned brackets, NOT used | PASS — Not used ✓ |
| `Account.Cancel()` + `Account.CreateOrder()` + `Submit()` — AddOnBase available | PASS — Via existing `SubmitBeStop` at line 1389 ✓ |
| `acc.Positions` (NT8 read-only) | PASS — Read in `HasOpenPosition` (line 5273); no lock needed ✓ |
| `Dispatcher.InvokeAsync` for UI updates | PASS — No UI calls in any new method ✓ |
| `PTT-` prefixed order names | PASS — `SubmitBeStop` creates `PTT-BE-Stop-*` ✓ |
| No Unicode / emoji in string literals | PASS — All log strings verified ASCII-only ✓ |
| No `FontFamily` override | PASS — No UI changes ✓ |
| No hardcoded `#RRGGBB` hex | PASS — No UI changes ✓ |
| No `CreateOrder` without `PTT-` prefix | PASS — No new `CreateOrder` calls ✓ |
| No `throw` in `OnOrderUpdate` path | PASS — All branches early `return` ✓ |
| No `sealed` on `TradeCopierWindow` | PASS — N/A, no window changes ✓ |
| Thread safety: `TryArmBePriceRetry` called from UI thread via `MoveStopToBreakEven` | PASS — `ConcurrentDictionary` write is lock-free, safe from any thread ✓ |
| Thread safety: `TryFireBePriceRetry` called on NT8 order-update thread | PASS — `ConcurrentDictionary` TryGetValue/TryRemove lock-free ✓ |

---

## Check 7: Single File Scope

**Result: PASS**

Plan Scope section states: "Single file: `src/PropTraderTools/CopyEngine.cs`". Tests go to existing `src/PropTraderTools/CopyEngineTests.cs`. No new source files created. Component Summary table lists only `CopyEngine.cs` for all production changes.

---

## Check 8: Spec Traceability

**Result: PASS**

| Requirement | Addressed? | Plan Section | Status |
|-------------|-----------|--------------|--------|
| FM-A: Ghost re-entry — follower already has open position | YES | FM-A Problem + `IsFollowerPositionBlocked` design | ✅ PASS |
| FM-A: Guard via `ShouldSkipFollower` as first gate | YES | FM-A Hook (first statement before existing guards) | ✅ PASS |
| FM-A: Uses `HasOpenPosition` (live NT8 truth, not duplicate logic) | YES | `IsFollowerPositionBlocked` delegates directly | ✅ PASS |
| FM-A: Log `[PTT-COPY-GUARD]` verifiable in SIM | YES | Log Markers table + Q4 of LANE gate | ✅ PASS |
| FM-B: PTT-BE-Stop bulk cancel silent rollback | YES | FM-B Problem | ✅ PASS |
| FM-B: Arm retry at `MoveStopToBreakEven` after line 6857 | YES | Hook 1 (`TryArmBePriceRetry` after `StatusUpdate?.Invoke`) | ✅ PASS |
| FM-B: Fire retry on cancel via `OnOrderUpdate` hook | YES | Hook 2 at line 1725a (AFTER 1725, BEFORE 1726) | ✅ PASS |
| FM-B: Attempt cap = 3 per account per trade | YES | `TryArmBePriceRetry` branch (5) `if (attempts >= 3)` | ✅ PASS |
| FM-B: Idempotency — no retry when stop already at BE price | YES | `StillAtNonBePrice` returns false when at price → `TryFireBePriceRetry` returns | ✅ PASS |
| FM-B: Working-state cleanup — evict slot when stop goes Working | YES | `TryFireBePriceRetry` branch (5) V-06 fix | ✅ PASS |
| FM-B: Log `[PTT-BE-RETRY]` verifiable in SIM | YES | Log Markers table (armed/fired/working-cleanup markers) | ✅ PASS |
| Single file scope | YES | Scope section | ✅ PASS |
| ASCII-only log markers | YES | Log Markers table; all 4 markers verified ASCII | ✅ PASS |

---

## Violations Summary (Final Pass)

| # | Rule ID | Violation | Location | Severity | Status |
|---|---------|-----------|----------|----------|--------|
| V-01 | LINE-ACCURACY | `SubmitBeStop` at line 451 | — | P0 | **RESOLVED** |
| V-02 | LINE-ACCURACY | `ArmAllPendingBe` at line 478 | — | P0 | **RESOLVED** |
| V-03 | LINE-ACCURACY | `MoveStopToBreakEven` at line 1542 | — | P0 | **RESOLVED** |
| V-04 | LINE-ACCURACY | Field anchor "near line 139" | — | P0 | **RESOLVED** |
| V-05 | CYC-BUDGET | `StillAtNonBePrice` CYC=4 stated (actual=7) | — | P1 | **RESOLVED** |
| V-06 | DESIGN-GAP | No Working-state cleanup path in `TryFireBePriceRetry` | — | P1 | **RESOLVED** |
| V-07 | LANE-GATE | LANES-APPROVED Q values Q1=YES/Q3=NO/Q4=NO (required Q1=No/Q3=Yes/Q4=Yes) | — | P0 | **RESOLVED** |
| V-08 | HOOK-PLACEMENT | `TryFireBePriceRetry` hook after `TryTriggerBeRecovery` instead of before | — | P0 | **RESOLVED** |
| V-09 | JS-021 | `StillAtNonBePrice` iterated `acc.Orders` without `.ToList()` | — | P0 | **RESOLVED** |

**New violations in final pass: NONE.**

---

## Overall Verdict

**REVIEW_PASS**

All 9 prior violations (V-01 through V-09) are confirmed resolved in the triply-revised plan. All 8 review checks pass. No new violations found.

### Gate Summary

| Check | Result |
|-------|--------|
| 1. LANE-SPLIT GATE (Q1=No, Q2=No, Q3=Yes, Q4=Yes, LANES-APPROVED) | ✅ PASS |
| 2. Source line accuracy (8 symbols, zero delta) | ✅ PASS |
| 3. FM-A design integrity (CYC=3/4, hook order, ASCII log) | ✅ PASS |
| 4. FM-B design integrity (2 fields Ordinal, 3 methods, hook order, cap=3, CYC≤8) | ✅ PASS |
| 5. Test coverage (4 FM-A + 5 FM-B, concrete paths) | ✅ PASS |
| 6. NT8 constraints (acc.Orders.ToList, StringComparer.Ordinal, JS-021, no lock) | ✅ PASS |
| 7. Single file scope (CopyEngine.cs only) | ✅ PASS |
| 8. Spec traceability (FM-A ghost entry, FM-B bulk cancel) | ✅ PASS |

**This plan is approved for Phase 3 (ticket generation).**

---

*Review produced by ptt-plan-reviewer. Status: REVIEW_PASS. Cycle 3 of 2-cycle limit (extended for triply-revised submission). All 9 violations resolved. Zero new violations. Phase 3 gate is OPEN.*
