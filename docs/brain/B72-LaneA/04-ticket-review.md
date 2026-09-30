# Ticket Review: B72-LaneA
**Phase**: 3.5 — Ticket Review
**Reviewer**: ptt-ticket-reviewer
**Pass**: 4 (FINAL)
**Date**: 2026-08-16
**Source files reviewed**:
- `docs/brain/B72-LaneA/04-tickets.md` (8 tickets, 65 canonical test IDs)
- `docs/brain/B72-LaneA/02-architecture-plan.md` (§7 Test ID Mapping Table)
**Corrections confirmed in this pass**:
- T_ATM_T3_09: `T_ATM_T3_09_CancelStaleBracketsLocal_PttBeTarget1_IsExcluded_StartsWith`
  — input `"PTT-BE-Target-1"`, `Assert.False(notBe)` [IS excluded] ✅
- T_ATM_T3_10: `T_ATM_T3_10_CancelStaleBracketsLocal_Stop3_IncludedInStaleList`
  — input `"Stop3"`, `Assert.True(notBe)` [included in stale list] ✅
Both corrections match plan §7 (B72-A-20 / CancelStaleBracketsLocal / notBe prefix guard).

---

## Ticket 1 — CopyEngine: ArmAllPendingBe + TryFirePositionState + FollowerFlatDisarm

**Hotfix IDs**: B72-A-01, B72-A-04, B72-A-07, B72-A-21
**Spec IDs**: T_BEALL_01–04, T_BE_RESET_01–02, T_TRYFIRE_01–03, T_FOLLOWER_FLAT_01–04 (13 IDs)

**TR-01 Traceability**: PASS
- B72-A-01 → plan §3 B72-A-01 ✅; §6 row B72-A-01 ✅
- B72-A-04 → plan §3 B72-A-04 ✅; §6 row B72-A-04 ✅
- B72-A-07 → plan §3 B72-A-07 ✅; §6 row B72-A-07 ✅
- B72-A-21 → plan §3 B72-A-21 ✅; §6 row B72-A-21 ✅
- All 13 test IDs appear in plan §7 mapping table ✅

**TR-02 Spec Coverage**: PASS
- 13 spec IDs covered; no duplicates with other tickets ✅

**TR-03 JS Concurrency (JS-021/023/025)**: PASS
- No `lock()` described ✅
- `_pendingBeSlots` specified as `ConcurrentDictionary` ✅
- No Dictionary<K,V> for shared state ✅

**TR-04 Type Safety (JS-001/002/003)**: PASS
- No `throw exception` in hot paths ✅
- No `return null` ✅
- Null guards: `IsFollowerAccount(null)` → `Assert.False(result)` ✅

**TR-05 Immutability (JS-008/009)**: PASS — no structs, no brushes in scope ✅

**TR-06 NT8 Constraints**: PASS
- No async/await in lifecycle ✅
- `ArmAllPendingBe` iterates `Account.All` within method body (not in test) ✅
- No `DateTime.Now` ✅

**TR-07 CYC Pre-Check**: PASS — all test methods CYC=1 (straight-line [Fact]) ✅

**TR-08 Test Coverage**: PASS — 13 [Fact] tests specified for 13 spec IDs ✅

**TR-09 Scan Checklist**: PASS
- S1 lock() ban ✅ | S2 async void ban ✅ | S3 return null ban ✅ | S4 throw ban ✅
- S5 non-ASCII ✅ | S6 CYC ≤ 8 ✅ | S7 xUnit-only ✅
- All 7 scans present for `src/PropTraderTools/Tests/CopyEngineB72Tests.cs` ✅

**TR-10 File Routing**: PASS
- `src/PropTraderTools/Tests/CopyEngineB72Tests.cs` → Wave workspace ✅

**VERDICT: TICKET_REVIEW_PASS**

---

## Ticket 2 — CopyEngine: QX Dedup + HandleEntryChange + IsDispatchTriggerState

**Hotfix IDs**: B72-A-02, B72-A-06, B72-A-22
**Spec IDs**: T_QX_DOUBLE_01–03, T_DRAG_DEDUP_02–04, T_DEDUP_MARKET_01–02, T_DEDUP_LIMIT_01–02 (10 IDs)

**TR-01 Traceability**: PASS
- B72-A-02 → plan §3 B72-A-02 ✅; §6 row B72-A-02 ✅
- B72-A-06 → plan §3 B72-A-06 ✅; §6 row B72-A-06 ✅
- B72-A-22 → plan §3 B72-A-22 ✅; §6 row B72-A-22 ✅
- All 10 test IDs appear in plan §7 mapping table ✅

**TR-02 Spec Coverage**: PASS
- 10 spec IDs covered; no duplicates ✅

**TR-03 JS Concurrency (JS-021/023/025)**: PASS
- No `lock()` described ✅
- `_dedupCache` specified as `ConcurrentDictionary<string, double>` ✅
- T_DRAG_DEDUP_02 verifies upsert (`cache[key] = value`) not `TryRemove` ✅

**TR-04 Type Safety (JS-001/002/003)**: PASS
- T_QX_DOUBLE_02 exercises null-guard path (`Record.Exception`) ✅
- No throw, no return null ✅

**TR-05 Immutability (JS-008/009)**: PASS ✅

**TR-06 NT8 Constraints**: PASS
- `OrderState.TriggerPending` enum value confirmed via T_QX_DOUBLE_01 ✅
- `IsDispatchTriggerState` is `internal static` — directly callable ✅
- No `DateTime.Now` ✅

**TR-07 CYC Pre-Check**: PASS — all test methods CYC=1 (straight-line [Fact]) ✅

**TR-08 Test Coverage**: PASS — 10 [Fact] tests for 10 spec IDs ✅

**TR-09 Scan Checklist**: PASS
- S1 lock() ban ✅ | S2 async void ban ✅ | S3 return null ban ✅ | S4 throw ban ✅
- S5 non-ASCII ✅ | S6 CYC ≤ 8 ✅ | S7 xUnit-only ✅
- All 7 scans present for `src/PropTraderTools/Tests/CopyEngineB72Tests.cs` ✅

**TR-10 File Routing**: PASS
- `src/PropTraderTools/Tests/CopyEngineB72Tests.cs` → Wave workspace ✅

**VERDICT: TICKET_REVIEW_PASS**

---

## Ticket 3 — CopyEngine: BE Instrument FullName + Sign + StateOk + Immediate Fire

**Hotfix IDs**: B72-A-08, B72-A-09, B72-A-10, B72-A-11
**Spec IDs**: T_BE_MOVE_01–02, T_BE_SIGN_LONG_01, T_BE_SIGN_SHORT_01, T_BE_SIGN_ZERO, T_BE_IMM_01–04, T_BE_MOVE_03–05 (12 IDs)

**TR-01 Traceability**: PASS
- B72-A-08 → plan §3 B72-A-08 ✅; §6 row B72-A-08 ✅
- B72-A-09 → plan §3 B72-A-09 ✅; §6 row B72-A-09 ✅
- B72-A-10 → plan §3 B72-A-10 ✅; §6 row B72-A-10 ✅
- B72-A-11 → plan §3 B72-A-11 ✅; §6 row B72-A-11 ✅
- All 12 test IDs appear in plan §7 mapping table ✅

**TR-02 Spec Coverage**: PASS
- 12 spec IDs covered; no duplicates ✅

**TR-03 JS Concurrency (JS-021/023/025)**: PASS
- No `lock()` described ✅
- No shared mutable state in test expressions ✅

**TR-04 Type Safety (JS-001/002/003)**: PASS
- T_BE_MOVE_03 exercises null-guard: `ArmPendingBe(null, null, 2)` → `Assert.Null(ex)` ✅
- Sign formulas are pure arithmetic expressions — no throw, no return null ✅

**TR-05 Immutability (JS-008/009)**: PASS ✅

**TR-06 NT8 Constraints**: PASS
- `FullName` string equality pattern used (not reference equality) ✅
- `OrderState.TriggerPending` in Step B stateOk confirmed via T_BE_MOVE_04 ✅
- Immediate-fire path correctly described ✅
- No `DateTime.Now` ✅

**TR-07 CYC Pre-Check**: PASS — all test methods CYC=1 ✅

**TR-08 Test Coverage**: PASS — 12 [Fact] tests for 12 spec IDs ✅

**TR-09 Scan Checklist**: PASS
- S1 lock() ban ✅ | S2 async void ban ✅ | S3 return null ban ✅ | S4 throw ban ✅
- S5 non-ASCII ✅ | S6 CYC ≤ 8 ✅ | S7 xUnit-only ✅
- All 7 scans present for `src/PropTraderTools/Tests/CopyEngineB72Tests.cs` ✅

**TR-10 File Routing**: PASS
- `src/PropTraderTools/Tests/CopyEngineB72Tests.cs` → Wave workspace ✅

**VERDICT: TICKET_REVIEW_PASS**

---

## Ticket 4 — CopyEngine: BE Cancel+Resubmit + OCO Seed + Target Filter

**Hotfix IDs**: B72-A-12, B72-A-13, B72-A-14, B72-A-23
**Spec IDs**: T_MSTBE_CR_01–03, T_OCO_SEED_01–03, T_OCO_SEQ_01, T_OCO_SEQ_04, T_QX_TARGETS_01–04 (12 IDs)

**TR-01 Traceability**: PASS
- B72-A-12 → plan §3 B72-A-12 ✅; §6 row B72-A-12 ✅
- B72-A-13/14 → plan §3 B72-A-13/14 ✅; §6 row B72-A-13/14 ✅
- B72-A-23 → plan §3 B72-A-23 ✅; §6 row B72-A-23 ✅
- All 12 test IDs appear in plan §7 mapping table ✅

**TR-02 Spec Coverage**: PASS
- 12 spec IDs covered; no duplicates ✅

**TR-03 JS Concurrency (JS-021/023/025)**: PASS
- `_mstbeOcoSeq` is `volatile int` + `Interlocked.Increment` (JS-023 compliant) ✅
- T_OCO_SEQ_04 tests concurrent uniqueness via `Task.Run` (10 concurrent calls) ✅
- No `lock()` described ✅

**TR-04 Type Safety (JS-001/002/003)**: PASS
- T_MSTBE_CR_02 exercises null-guard via reflection invoke with null args ✅
- T_MSTBE_CR_03 asserts signal names start "PTT-" (NT8 CreateOrder naming rule) ✅
- No throw, no return null ✅

**TR-05 Immutability (JS-008/009)**: PASS ✅

**TR-06 NT8 Constraints**: PASS
- Signal names start "PTT-" confirmed by T_MSTBE_CR_03 ✅
- `Environment.TickCount` seed (not `DateTime.Now`) ✅
- `isAtmTarget` widened to include PTT-QX-T* and PTT-BE-Target-* ✅

**TR-07 CYC Pre-Check**: PASS
- T_OCO_SEQ_04 has a for-loop = CYC 2 (explicitly noted; well within CYC ≤ 8) ✅
- All other test methods CYC=1 ✅

**TR-08 Test Coverage**: PASS — 12 [Fact] tests for 12 spec IDs ✅

**TR-09 Scan Checklist**: PASS
- S1 lock() ban ✅ | S2 async void ban ✅ | S3 return null ban ✅ | S4 throw ban ✅
- S5 non-ASCII ✅ | S6 CYC ≤ 8 (noted CYC=2 for T_OCO_SEQ_04) ✅ | S7 xUnit-only ✅
- All 7 scans present for `src/PropTraderTools/Tests/CopyEngineB72Tests.cs` ✅

**TR-10 File Routing**: PASS
- `src/PropTraderTools/Tests/CopyEngineB72Tests.cs` → Wave workspace ✅

**VERDICT: TICKET_REVIEW_PASS**

---

## Ticket 5 — CopyEngine: IsAtmBracketName

**Hotfix IDs**: B72-A-19
**Spec IDs**: T_ATM_T3_01–03, T_ATM_T3_06–08 (6 IDs)

**TR-01 Traceability**: PASS
- B72-A-19 → plan §3 B72-A-19 ✅; §6 row B72-A-19 ✅
- All 6 test IDs appear in plan §7 mapping table ✅

**TR-02 Spec Coverage**: PASS
- 6 spec IDs covered; T_ATM_T3_04/05/09/10 correctly deferred to Ticket 6 ✅

**TR-03 JS Concurrency (JS-021/023/025)**: PASS
- `IsAtmBracketName` is `internal static` — pure, no shared state ✅

**TR-04 Type Safety (JS-001/002/003)**: PASS
- T_ATM_T3_08 asserts `Assert.False(IsAtmBracketName(""))` — empty string handled ✅
- No throw, no return null ✅

**TR-05 Immutability (JS-008/009)**: PASS ✅

**TR-06 NT8 Constraints**: PASS
- `IsAtmBracketName` is pure static method, no NT8 API calls ✅

**TR-07 CYC Pre-Check**: PASS — all test methods CYC=1 (single Assert) ✅

**TR-08 Test Coverage**: PASS — 6 [Fact] tests for 6 spec IDs ✅

**TR-09 Scan Checklist**: PASS
- S1 lock() ban ✅ | S2 async void ban ✅ | S3 return null ban ✅ | S4 throw ban ✅
- S5 non-ASCII ✅ | S6 CYC ≤ 8 ✅ | S7 xUnit-only ✅
- All 7 scans present for `src/PropTraderTools/Tests/CopyEngineB72Tests.cs` ✅

**TR-10 File Routing**: PASS
- `src/PropTraderTools/Tests/CopyEngineB72Tests.cs` → Wave workspace ✅

**VERDICT: TICKET_REVIEW_PASS**

---

## Ticket 6 — PttBreakEven: Stale Brackets + notBe Filter

**Hotfix IDs**: B72-A-03, B72-A-20
**Spec IDs**: T_BE_CANCEL_01–03, T_ATM_T3_04–05, T_ATM_T3_09–10 (7 IDs)

**TR-01 Traceability**: PASS
- B72-A-03 → plan §3 B72-A-03 ✅; §6 row B72-A-03 ✅; maps to CancelStaleBracketsLocal stateOk
- B72-A-20 → plan §3 B72-A-20 ✅; §6 row B72-A-20 ✅; maps to CancelStaleBracketsLocal notBe prefix
- T_ATM_T3_04/05: in-ticket extension of IsAtmBracketName coverage; note in Ticket 6 provides rationale ✅
- T_ATM_T3_09/10: B72-A-20 / CancelStaleBracketsLocal / notBe — confirmed in plan §7 ✅

**KEY VERIFICATION (T_ATM_T3_09)**:
- Test name: `T_ATM_T3_09_CancelStaleBracketsLocal_PttBeTarget1_IsExcluded_StartsWith` ✅
- Contains "PttBeTarget1" ✅
- Input: `"PTT-BE-Target-1"` ✅
- Assert: `Assert.False(notBe)` — "PTT-BE-Target-1" IS excluded (notBe=false) ✅
- Plan §7 B72-A-20: prefix guard excludes entire PTT-BE-* family ✅ CONFIRMED

**KEY VERIFICATION (T_ATM_T3_10)**:
- Test name: `T_ATM_T3_10_CancelStaleBracketsLocal_Stop3_IncludedInStaleList` ✅
- Contains "Stop3" ✅
- Input: `"Stop3"` ✅
- Assert: `Assert.True(notBe)` — "Stop3" passes notBe filter (IS included in stale list) ✅
- Plan §7 B72-A-20: non-PTT-BE- names pass filter (will be cancelled) ✅ CONFIRMED

**TR-02 Spec Coverage**: PASS
- 7 spec IDs covered; no duplicates with Ticket 5 (T_ATM_T3_04/05 extension justified in-ticket) ✅

**TR-03 JS Concurrency (JS-021/023/025)**: PASS
- `CancelStaleBracketsLocal` is `private static`; all tests use pure proxy expressions ✅
- No `lock()` described ✅

**TR-04 Type Safety (JS-001/002/003)**: PASS
- T_ATM_T3_05 asserts `Assert.False(IsAtmBracketName(null))` — null guard verified ✅
- No throw, no return null ✅

**TR-05 Immutability (JS-008/009)**: PASS ✅

**TR-06 NT8 Constraints**: PASS
- `OrderState.TriggerPending` enum used for stateOk proxy assertions ✅
- No `DateTime.Now` ✅

**TR-07 CYC Pre-Check**: PASS — all test methods CYC=1 ✅

**TR-08 Test Coverage**: PASS — 7 [Fact] tests for 7 spec IDs ✅

**TR-09 Scan Checklist**: PASS
- S1 lock() ban ✅ | S2 async void ban ✅ | S3 return null ban ✅ | S4 throw ban ✅
- S5 non-ASCII ✅ | S6 CYC ≤ 8 ✅ | S7 xUnit-only ✅
- All 7 scans present for `src/PropTraderTools/Tests/PttBreakEvenB72Tests.cs` ✅

**TR-10 File Routing**: PASS
- `src/PropTraderTools/Tests/PttBreakEvenB72Tests.cs` → Wave workspace ✅

**VERDICT: TICKET_REVIEW_PASS**

---

## Ticket 7 — PttBreakEven: OCO Shared Counter + Prefix

**Hotfix IDs**: B72-A-15, B72-A-16
**Spec IDs**: T_OCO_SHARED_01–02, T_OCO_ID_01–03 (5 IDs)

**TR-01 Traceability**: PASS — B72-A-15/16 in plan §3/§6 ✅; 5 IDs in plan §7 ✅

**TR-02 Spec Coverage**: PASS — 5 spec IDs covered; no duplicates ✅

**TR-03 JS Concurrency**: PASS — `NextBeOcoSeq` uses `Interlocked.Increment`; no lock ✅

**TR-04 Type Safety**: PASS — T_OCO_SHARED_02 asserts `_beOcoSeq` field is absent via reflection ✅

**TR-05 Immutability**: PASS ✅

**TR-06 NT8 Constraints**: PASS — `BuildBeOcoId` is private static pure computation; no NT8 API ✅

**TR-07 CYC Pre-Check**: PASS — all test methods CYC=1 ✅

**TR-08 Test Coverage**: PASS — 5 [Fact] tests for 5 spec IDs ✅

**TR-09 Scan Checklist**: PASS
- S1 lock() ban ✅ | S2 async void ban ✅ | S3 return null ban ✅ | S4 throw ban ✅
- S5 non-ASCII ✅ | S6 CYC ≤ 8 ✅ | S7 xUnit-only ✅
- All 7 scans present for `src/PropTraderTools/Tests/PttBreakEvenB72Tests.cs` ✅

**TR-10 File Routing**: PASS — `src/PropTraderTools/Tests/PttBreakEvenB72Tests.cs` ✅

**VERDICT: TICKET_REVIEW_PASS**

---

## Ticket 8 — PttBreakEven: Sign Fixes + RaiseBeNotify

**Hotfix IDs**: B72-A-17, B72-A-18
**Spec IDs**: T_BE_PRICE_LONG_01–02, T_BE_PRICE_SHORT_01–02, T_BE_PRICE_VALID_SHORT, T_NOTIFY_01–02 (7 IDs)

**TR-01 Traceability**: PASS — B72-A-17/18 in plan §3/§6 ✅; 7 IDs in plan §7 ✅

**TR-02 Spec Coverage**: PASS — 7 spec IDs covered; no duplicates ✅

**TR-03 JS Concurrency**: PASS — pure arithmetic expressions; no shared state ✅

**TR-04 Type Safety**: PASS — `Record.Exception(() => Execute(null))` used for null-guard path ✅

**TR-05 Immutability**: PASS ✅

**TR-06 NT8 Constraints**: PASS — sign formula tests use plain double arithmetic; no NT8 API ✅

**TR-07 CYC Pre-Check**: PASS — all test methods CYC=1 ✅

**TR-08 Test Coverage**: PASS — 7 [Fact] tests for 7 spec IDs ✅

**TR-09 Scan Checklist**: PASS
- S1 lock() ban ✅ | S2 async void ban ✅ | S3 return null ban ✅ | S4 throw ban ✅
- S5 non-ASCII ✅ | S6 CYC ≤ 8 ✅ | S7 xUnit-only ✅
- All 7 scans present for `src/PropTraderTools/Tests/PttBreakEvenB72Tests.cs` ✅

**TR-10 File Routing**: PASS — `src/PropTraderTools/Tests/PttBreakEvenB72Tests.cs` ✅

**VERDICT: TICKET_REVIEW_PASS**

---

## Overall Summary

| Ticket | TR-01 | TR-02 | TR-03 | TR-04 | TR-05 | TR-06 | TR-07 | TR-08 | TR-09 | TR-10 | VERDICT |
|--------|-------|-------|-------|-------|-------|-------|-------|-------|-------|-------|---------|
| T1 | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS |
| T2 | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS |
| T3 | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS |
| T4 | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS |
| T5 | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS |
| T6 | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS |
| T7 | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS |
| T8 | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS |

**Violations**: NONE

## Overall: TICKET_REVIEW_PASS
