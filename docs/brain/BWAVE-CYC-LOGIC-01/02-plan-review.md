# BWAVE-CYC-LOGIC-01 — Plan Review (Cycle 2)

**Reviewer:** PTT Plan Reviewer (Bob CLI, ptt-plan-reviewer mode)
**Phase:** 2 — Plan Review (Cycle 2 of 2)
**Date:** 2026-01-01
**Epic:** BWAVE-CYC-LOGIC-01
**Input artifact:** `docs/brain/BWAVE-CYC-LOGIC-01/02-architecture-plan.md` (Cycle 1 Revision)
**Prior review:** Cycle 1 — REVIEW_FAIL (V-001: TryFindRuleAndFollowerIndex CYC=10)

---

## VERDICT: REVIEW_PASS

**Total violations this cycle:** 0
**Prior V-001:** Resolved. ✅
**Prior informational CYC undercounts (7):** All corrected. ✅

---

## Section A — LANE-SPLIT GATE Compliance

| Check | Result |
|-------|--------|
| Gate result stated in plan | ✅ PASS — `LANE-SPLIT GATE RESULT: SINGLE-PIPELINE` at plan line 11 |
| Single-pipeline rationale documented | ✅ Q1–Q4 analysis present |

**LANE-SPLIT GATE: PASS**

---

## Section B — Spec Coverage Matrix

| Requirement | Addressed? | Plan Section |
|-------------|-----------|--------------|
| Fill all 70 stubs in Groups A–E (L7877–8199) | ✅ Yes — 69 stubs filled + 1 excluded as already implemented (B-04) | Deferred Items, Ticket Breakdown |
| A-14b IsFollowerAccountMatch added as NEW method | ✅ Yes — documented "NEW METHOD (ADD, not fill)" with ObfuscationAttribute, insertion point, and engineer note | A-14b, T1 engineer note |
| `SelectBeRefPriceByDirection` at L8005 left untouched | ✅ Yes — explicitly excluded as B-04 "SKIP: already implemented" | B-04, Deferred Items table |
| No `lock()` | ✅ Plan uses only ConcurrentDictionary operations | Threading Model section |
| No `throw` | ✅ All NT8 API calls wrapped in `try/catch {}` | All action method blocks |
| CYC ≤ 8 per method | ✅ All 70 methods (69 fills + A-14b) have CYC ≤ 8 | Full table: Section E |
| ASCII-only string literals | ✅ All literals inspected; no Unicode or curly quotes | All method code blocks |
| .NET 4.8 only (no switch expr, no record, no `??=`) | ✅ No .NET 5+ syntax present | All method code blocks |
| No new fields without justification | ✅ No new fields proposed | All method blocks |
| Method signatures and ObfuscationAttributes unchanged | ✅ Plan fills bodies only; A-14b is a new addition with correct attribute | All method headers |
| Hard-link sync after every src edit | ✅ Stated at plan end | Hard-Link Sync Requirement |
| `dotnet test` failed = 0 after each ticket | ✅ SCAN-07 in 7-scan checklist | 7-Scan Checklist Template |
| Do NOT modify CopyEngineTests.cs | ✅ "READ-ONLY. DO NOT MODIFY." stated | Component Summary |

**SPEC COVERAGE: 13/13 PASS**

---

## Section C — V-001 Resolution Verification

### V-001 — RESOLVED ✅

**Cycle 1 finding:** `TryFindRuleAndFollowerIndex` (A-14) had corrected CYC=10 (plan stated 6).

**Cycle 2 revision:**
- A-14 redesigned to call new helper `IsFollowerAccountMatch` (A-14b).
- A-14 revised logic (plan lines 337–350):

```
followerIndex = -1;
foreach (var rule in _rules)                          // (1) foreach
{
    if (rule.Instrument != instr?.FullName) continue; // (2) if/continue
    for (int i = 0; i < rule.FollowerAccounts.Length; i++)  // (3) for
    {
        if (!IsFollowerAccountMatch(...)) continue;    // (4) if/continue
        followerIndex = i;
        return true;
    }
}
return false;
```

CYC (project McCabe standard — 1 base + 1 per control-flow branch):
```
= 1 (base) + 1 (foreach) + 1 (if/continue) + 1 (for) + 1 (if/continue) = 5
```

**Plan states CYC=5. Verified CYC=5. ≤ 8. V-001 RESOLVED.**

---

## Section D — A-14b Documentation Verification

| Check | Result |
|-------|--------|
| Marked as NEW METHOD (ADD, not fill) | ✅ Section header: "**NEW METHOD (ADD, not fill)**" |
| `[ObfuscationAttribute(Feature="rename", Exclude=true)]` required | ✅ Stated explicitly |
| Insertion point documented | ✅ "Insert immediately after A-14 stub (approximately L7941), before A-15" |
| Engineer note in T1 ticket group | ✅ "This method does NOT have a pre-existing stub. The engineer must insert..." |
| Component summary updated | ✅ "69 stub implementations + 1 new method (A-14b)" |
| CYC verified | ✅ CYC=4 (1 base + 3 if branches) ≤ 8 |
| private static (no instance state) | ✅ Signature: `private static bool IsFollowerAccountMatch(...)` |

**A-14b DOCUMENTATION: PASS**

---

## Section E — CYC Compliance (all 70 methods, revised plan values)

### 7 Informational Corrections from Cycle 1 — All Applied

| Method | C1 Stated | C1 Corrected | Revised Plan CYC | Verified? |
|--------|-----------|--------------|-----------------|-----------|
| A-02 `IsPendingBeTriggerMet` | 6 | 8 | **8** | ✅ |
| A-04 `IsNativeAtmTargetOrder` | 4 | 5 | **5** | ✅ |
| A-05 `IsPttBeOrQxTargetOrder` | 4 | 6 | **6** | ✅ |
| A-22 `ResubmitFollowerEntry` | 4 | 8 | **8** | ✅ |
| A-24 `CancelStaleCascadeTgtDrag` | 5 | 8 | **8** | ✅ |
| C-01 `HasValidTargetNameSuffix` | 4 | 5 | **5** | ✅ |
| D-01 `TrySyncAtmBrackets` | 4 | 6 | **6** | ✅ |

### Full CYC Table (revised plan — all values ≤ 8)

| Method | CYC | ≤ 8? |
|--------|-----|------|
| A-01 `TryFireImmediateBeIfAlreadyAtLevel` | 5 | ✅ |
| A-02 `IsPendingBeTriggerMet` | 8 | ✅ |
| A-03 `IsEligibleBeTargetOrder` | 4 | ✅ |
| A-04 `IsNativeAtmTargetOrder` | 5 | ✅ |
| A-05 `IsPttBeOrQxTargetOrder` | 6 | ✅ |
| A-06 `RegisterBeRetryIfNoTargets` | 1 | ✅ |
| A-07 `RegisterPartialTargetBeRetry` | 1 | ✅ |
| A-08 `CancelExistingStpDragOrders` | 4 | ✅ |
| A-09 `CancelExistingTgtDragOrders` | 4 | ✅ |
| A-10 `SubmitReplacementStopLeg` | 3 | ✅ |
| A-11 `SubmitReplacementTargetLeg` | 3 | ✅ |
| A-12 `IsReArmedAtmBracketCleanupRequired` | 3 | ✅ |
| A-13 `FindMatchingNativeAtmBracket` | 4 | ✅ |
| A-14 `TryFindRuleAndFollowerIndex` (revised) | 5 | ✅ |
| A-14b `IsFollowerAccountMatch` (NEW ADD) | 4 | ✅ |
| A-15 `HasActiveQxOrdersForInstrument` | 3 | ✅ |
| A-16 `SyncAtmFollowerStopBracket` | 3 | ✅ |
| A-17 `CancelStaleTgtDragOrders` | 4 | ✅ |
| A-18 `CreateAndSubmitReplacementTarget` | 3 | ✅ |
| A-19 `HasInFlightFlattenOrder` | 4 | ✅ |
| A-20 `IsPositionFlatOrMissing` | 3 | ✅ |
| A-21 `IsLeaderTargetOrder` | 4 | ✅ |
| A-22 `ResubmitFollowerEntry` | 8 | ✅ |
| A-23 `IsLeaderAccountForInstrument` | 3 | ✅ |
| A-24 `CancelStaleCascadeTgtDrag` | 8 | ✅ |
| B-01 `GetMarketBidPrice` | 1 | ✅ |
| B-02 `GetMarketAskPrice` | 1 | ✅ |
| B-03 `GetBeTickSize` | 1 | ✅ |
| B-05 `FireBeAndNotifyEvent` | 1 | ✅ |
| B-06 `ShouldFireBeImmediately` | 3 | ✅ |
| B-07 `CompleteBeArming` | 1 | ✅ |
| B-08 `TryClaimPendingBeSlot` | 2 | ✅ |
| B-09 `GetSlotInstrumentName` | 1 | ✅ |
| B-10 `GetSlotAccountName` | 2 | ✅ |
| B-11 `RaisePendingBeFiredEvent` | 1 | ✅ |
| B-12 `SettleAndFirePendingBe` | 3 | ✅ |
| C-01 `HasValidTargetNameSuffix` | 5 | ✅ |
| C-02 `SelectBeTargetList` | 3 | ✅ |
| C-03 `IsBeTargetActiveState` | 2 | ✅ |
| C-04 `IsBeTargetPendingChangeState` | 2 | ✅ |
| C-05 `IsBeTargetSnapshotState` | 1 | ✅ |
| D-01 `TrySyncAtmBrackets` | 6 | ✅ |
| D-02 `TrySkipTrailingStop` | 2 | ✅ |
| D-03 `SyncStandardBracket` | 4 | ✅ |
| D-04 `IsPttTgtDragOrder` | 2 | ✅ |
| D-05 `IsAtmTgtOrder` | 4 | ✅ |
| D-06 `IsBePendingTargetOrder` | 1 | ✅ |
| D-07 `IsPttBeStopRejected` | 3 | ✅ |
| D-08 `IsPttDragOrderCancellable` | 5 | ✅ |
| D-09 `IsPttQxTargetOrder` | 4 | ✅ |
| D-10 `IsNativeAtmBeRetryTarget` | 4 | ✅ |
| D-11 `IsBeRetryEligibleOrderState` | 3 | ✅ |
| D-12 `IsBeRetryOrderInvalid` | 3 | ✅ |
| D-13 `IsBeSlotNonTerminal` | 1 | ✅ |
| D-14 `IsBeFilledWithOpenPosition` | 2 | ✅ |
| D-15 `IsPttDragOrderName` | 3 | ✅ |
| D-16 `IsDragInstrumentMatch` | 1 | ✅ |
| D-17 `IsQxTOrderStateValid` | 3 | ✅ |
| D-18 `IsQxTBracketNameValid` | 4 | ✅ |
| D-19 `TryGetCleanupEntryForFollower` | 2 | ✅ |
| D-20 `IsCleanupEntryCurrentAndMatching` | 4 | ✅ |
| D-21 `SendAtmCancelReplace` | 3 | ✅ |
| D-22 `TryMatchFollowerInRule` | 4 | ✅ |
| D-23 `IsBeReplaceTargetValid` | 4 | ✅ |
| D-24 `TryIncrementBeReplaceAttempt` | 2 | ✅ |
| E-01 `IsBracketOrderLiveState` | 4 | ✅ |
| E-02 `MatchesPttReplacementName` | 3 | ✅ |
| E-03 `LogHbcDiag` | 2 | ✅ |
| E-04 `ExecuteStopDragOrder` | 3 | ✅ |
| E-05 `IsOrderEventProcessable` | 4 | ✅ |

**CYC COMPLIANCE: 70/70 PASS (all ≤ 8)**

---

## Section F — NT8 API Check

All NT8 APIs claimed in the plan (`Account.Cancel`, `Account.CreateOrder`, `Account.Submit`, `Account.Orders`, `Account.Positions` via FindPosition, `Instrument.MarketData.Bid/Ask.Price`, `Instrument.MasterInstrument.TickSize`, `NinjaTrader.Code.Output.Process`, `NinjaTrader.Core.Globals.MaxDate`) are confirmed available on `AddOnBase`.

StrategyBase-only APIs (`AtmStrategyCreate`, `AtmStrategyChangeStopTarget`) are correctly excluded. ✅

**NT8 API CHECK: PASS**

---

## Section G — .NET 4.8 Syntax Check

| Pattern | Present? | Compliant? |
|---------|---------|-----------|
| Switch expressions (`x switch { }`) | No | ✅ |
| Record types | No | ✅ |
| `??=` null-coalescing assignment | No | ✅ |
| `ValueTuple<T1,T2>` (C# 7.0 / .NET 4.7+) | Yes — D-20 | ✅ |
| `is` type test (C# 7.0) | Yes — D-20 | ✅ |
| `?.` null-conditional (C# 6.0) | Yes — many methods | ✅ |
| LINQ `.ToList()` / `.Any()` | Yes | ✅ |

**.NET 4.8 SYNTAX CHECK: PASS**

---

## Section H — Threading / Concurrency Check

| Rule | Check | Result |
|------|-------|--------|
| JS-021: No `lock()` | Scanned all 70 method bodies — zero `lock(` found | ✅ PASS |
| JS-021: No Monitor/Mutex/SemaphoreSlim for state | Not present | ✅ PASS |
| JS-023: UI updates from off-thread without Dispatcher.InvokeAsync | No direct UI updates in any stub; event firing via `PendingBeFired?.Invoke(...)` delegates marshaling to subscriber per existing pattern (confirmed L409-412) | ✅ PASS |
| ConcurrentDictionary used for all shared state | `_pendingBeSlots`, `_pendingFollowerBeSlots`, `_beReplaceAttempts`, `_filledBeTargetCount`, `_qxPendingFollowerCleanup` — all ConcurrentDictionary | ✅ PASS |

**THREADING CHECK: PASS**

---

## Section I — Type Safety Check

| Rule | Check | Result |
|------|-------|--------|
| JS-001: No `throw` in stub bodies | Scanned all 70 method bodies — zero `throw` statements; all NT8 calls wrapped in `try/catch {}` | ✅ PASS |
| JS-002: No null return where value expected | All methods returning `Order` return `null` only for nullable return type | ✅ PASS |
| JS-003: No magic string for discriminated state | All string comparisons use literal order names per documented naming conventions table | ✅ PASS |

**TYPE SAFETY CHECK: PASS**

---

## Section J — 7-Scan Checklist Check

Template at plan lines 1454–1463 covers all 7 scans with ticket applicability. Per Phase 2 role scope, per-ticket embedding is the responsibility of Phase 3.5 (ptt-ticket-reviewer). Plan-level template is present and correct. ✅

| Scan | Description | Ticket Scope |
|------|-------------|-------------|
| SCAN-01 | No `lock(` in changed file | All |
| SCAN-02 | No `DateTime.Now`; only `DateTime.UtcNow` | T1 (A-12), T5 (D-20) |
| SCAN-03 | No `throw`; NT8 API calls in `try/catch` | T1,T2,T3,T4,T5 (per plan table) |
| SCAN-04 | ASCII-only string literals | All |
| SCAN-05 | All `acc.CreateOrder` calls use `"PTT-"` prefix | T2, T3, T5 |
| SCAN-06 | Max McCabe CYC ≤ 8 per method | All |
| SCAN-07 | `dotnet test` reports Failed: 0 after ticket | All |

**7-SCAN CHECKLIST CHECK: PASS**

---

## Section K — Deferred Work Items

No deferred work items raised by this review. All prior informational CYC findings (Section D of Cycle 1 review) are resolved by the corrected values in the revised plan. Engineer must use the corrected CYC values from Section E of this review when writing `// CYC=N` source comments.

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| — | No deferred items | — | — | — |

---

## Summary

| Check | Result |
|-------|--------|
| Lane-Split Gate | ✅ PASS |
| Spec coverage (70 stubs + B-04 skip + A-14b new add) | ✅ PASS |
| V-001 resolved (A-14 CYC=5, A-14b CYC=4) | ✅ PASS |
| CYC ≤ 8 (all 70 methods) | ✅ PASS |
| 7 informational CYC corrections applied | ✅ PASS |
| No lock() | ✅ PASS |
| No throw | ✅ PASS |
| No DateTime.Now | ✅ PASS |
| ASCII-only | ✅ PASS |
| .NET 4.8 syntax | ✅ PASS |
| No new fields | ✅ PASS |
| NT8 API (AddOnBase only) | ✅ PASS |
| SelectBeRefPriceByDirection excluded | ✅ PASS |
| 7-scan checklist present | ✅ PASS |
| Ticket boundaries independently buildable | ✅ PASS |

---

**REVIEW_PASS**

Plan is approved for Phase 3 (ticket generation). Engineer must use corrected CYC values from Section E when writing `// CYC=N` source comments, not the original plan values where they differed.
