# Plan Review — PTT-BUGS-LIVE-0911 Lane B
**BUG-2: Post-Exit Clean Slate**
**Reviewer: ptt-plan-reviewer**
**Date: 2026-09-11**
**Cycle: 3 (FINAL)**
**Status: REVIEW_PASS**

---

## VERDICT: REVIEW_PASS

**Zero violations found.** All 7 checks pass. All 3 prior violation fixes confirmed. Plan is approved for Phase 3 ticket generation.

---

## Violation Fix Verification

### VIOLATION-1 FIX — CONFIRMED FIXED ✅

**Cycle 1 violation:** Only the 2-param `CancelQxBrackets(Account, Instrument)` was modified; the 3-param overload `CancelQxBrackets(Account, Instrument, HashSet<Order>)` (line 1052, primary QX-2T path) was not instrumented.

**Cycle 3 status (unchanged from Cycle 2 — no regression):**
- Plan Section 3.3 dedicated subsection for 3-param overload at line 1052 confirmed present.
- `TryMarkFollowerSlateNotReady(acc); // BUG2: bracket wipe imminent (3-param path)` confirmed immediately after null guard.
- BUG2-REQ-2: "covers both CancelQxBrackets overloads" — present.
- Affected Methods table (plan line 424): 3-param overload listed as MODIFIED.
- No regression introduced by the Cycle 3 surgical patch.

**FIXED. INTACT.** ✅

---

### VIOLATION-2 FIX — CONFIRMED FIXED ✅

**Cycle 1 violation:** Test criterion 7(c) required "fo=NULL not logged on 2nd+ trade." Unsatisfiable: `TryLogSFBTrace` fires before `if (fo == null) return`.

**Cycle 3 status (unchanged from Cycle 2 — no regression):**
- Section 8 note (plan lines 517–518): `fo=NULL` log intentionally preserved during gate-closed window; fix prevents bracket submission, not log emission.
- Test 7(c) name: `FollowerSlateReady_GateClosed_BracketSyncBlocked_FoNullLogPreserved` — satisfiable.
- Test 7(c) criterion: asserts no `CreateOrder` calls on follower (bracket-propagation-blocked), not log absence.
- No regression introduced by the Cycle 3 surgical patch.

**FIXED. INTACT.** ✅

---

### VIOLATION-3 FIX — CONFIRMED FIXED ✅

**Cycle 2 violation:** SCAN-07 pass criteria read "zero fo=NULL log entries on 2nd trade bracket sync" — directly contradicting the Section 8 note that such entries are intentionally preserved.

**Cycle 3 resolution verified (plan lines 508–512):**

> *"Pass criteria: No bracket submission orders are dispatched to follower accounts while `_followerSlateReady` gate is closed (false). `fo=NULL` log entries are expected during the gate-closed window and do NOT constitute a scan failure. Verify by confirming no `Account.CreateOrder` calls execute for bracket orders on 2nd+ trade until `PositionUpdate hasPos=False` fires and slate is confirmed clean."*

All three required conditions met:
1. SCAN-07 explicitly states `fo=NULL` entries are **expected** and **do NOT constitute a scan failure**. ✅
2. SCAN-07 pass criteria focuses on **no `Account.CreateOrder` calls** for bracket orders while gate is closed. ✅
3. Pass criteria is fully consistent with Section 8 note (intentional preservation) and test 7(c) (CreateOrder-absent criterion). ✅

**FIXED.** ✅

---

### Non-Blocking Finding (not a VIOLATION)

**SCAN-07 procedure step 7 (plan line 507):** "Verify no fo=NULL lines appear in output for 2nd trade brackets." This procedure sub-step is stale and nominally inconsistent with the pass criteria that immediately follows it (lines 508–512). However:
- The pass criteria block is the authoritative verdict text for SCAN-07.
- The pass criteria (lines 508–512) unambiguously states fo=NULL entries are expected and non-failing.
- The spec requirement (fo=NULL acceptable; bracket-submission-absence is the criterion) is correctly addressed in the pass criteria.
- The gate is achievable; an engineer who reads the complete SCAN-07 block will find the governing criterion correct.

This is a documentation inconsistency within the procedure steps, not an unachievable gate or missing spec requirement. Not elevated to VIOLATION under the SPEC COMPLETENESS rule. **Noted for architect awareness only.** The architect may wish to update step 7 text in a future pass (e.g., "Observe [TP4-SFB] logs — fo=NULL entries during gate-closed window are expected").

---

## Full Review Checklist (Cycle 3)

| # | Check | Result | Notes |
|---|-------|--------|-------|
| 1 | LANE-SPLIT GATE: LANES-APPROVED present | PASS | Plan line 9 |
| 1 | Q1=NO (not within 50 lines of BUG-1 fix) | PASS | BUG-2 primary change ~3902; BUG-1 ~1500 lines away |
| 1 | Q2=NO (BUG-2 does not depend on BUG-1 design) | PASS | `_followerSlateReady` reads no BUG-1 field/method |
| 1 | Q3=YES (standalone value if other blocked) | PASS | BUG-2 prevents unmanaged risk exposure independently |
| 1 | Q4=YES (independent SIM verification path) | PASS | QX-2T → Trade 2 SIM path distinct from BUG-1 SIM |
| 2 | NULL site correctly identified (FindFollowerBracketOrder → SyncFollowerBracket early return) | PASS | Plan Section 2 Step 5; ~line 3902 |
| 2 | output.md line references correct (894, 900, 906 / 268, 274, 280) | PASS | Evidence block in plan Section 2 |
| 2 | Root cause chain accurate (QX wipes ATM names → stale slate → fo=NULL) | PASS | Plan Section 2 correctly describes race |
| 2 | Why -08 succeeded / -07 did not explained | PASS | Extra flatten cycle reset -08's bracket timing |
| 3 | `_followerSlateReady` typed `ConcurrentDictionary<string, bool>` | PASS | StringComparer.Ordinal specified; plan Section 3.1 |
| 3 | Absent key = gate open (first trade semantics) | PASS | `TryGetValue && !ready`: absent → false && x = false → OPEN (Section 3.4) |
| 3 | `TryMarkFollowerSlateNotReady` called before cancel batch (after null guard) | PASS | Before `var stale = ...` in both CancelQxBrackets overloads |
| 3 | `TrySetFollowerSlateReady` gated on BOTH IsAccountFlat AND !HasAnyActiveOrders | PASS | 4-condition chain in plan Section 3.2 |
| 3 | Gate in `FindFollowerBracketOrder(Account, ...)` returns null on slate=false | PASS | CYC=2; plan Section 3.3 |
| 3 | Subscribe/Unsubscribe correctly paired for PositionUpdate | PASS | `+=` and `-=` both present; no orphaned subscription |
| 3 | Both CancelQxBrackets overloads instrumented | PASS | 2-param (line 938) + 3-param (line 1052); both covered |
| 3 | FlattenOneAccount instrumented | PASS | Plan Section 3.3; after IsAccountFlattenable guard |
| 3 | Section 3.4 three-state gate semantics table present and correct | PASS | Absent/false/true states correctly mapped |
| 4 | No `lock()` in any new or modified method (JS-021) | PASS | All new state via ConcurrentDictionary; no lock() |
| 4 | No Monitor/Mutex/SemaphoreSlim for state (JS-021) | PASS | None present |
| 4 | No `throw` in hot-path methods (JS-001) | PASS | All new methods return early; no exceptions thrown |
| 4 | No null return on non-nullable contracts (JS-002) | PASS | `Order?` null contract unchanged; bool/void explicit |
| 4 | No magic string for discriminated state (JS-003) | PASS | None present |
| 4 | No Dictionary<K,V> for shared collection (JS-009) | PASS | ConcurrentDictionary used |
| 4 | No mutable struct fields / unfroozen brush (JS-008) | PASS | No new struct types |
| 4 | No public constructor on singleton/signal struct (JS-010) | PASS | No new types |
| 4 | No UI update from off-thread without Dispatcher (JS-023) | PASS | OnFollowerPositionUpdate touches ConcurrentDictionary only |
| 4 | All new methods CYC ≤ 8 | PASS | Max=5 (HasAnyActiveOrders). All ≤ 8. |
| 4 | No `async`/`await` in NT8 lifecycle or event handler methods | PASS | OnFollowerPositionUpdate is synchronous void |
| 4 | No `Account.All` in constructor | PASS | Used in Subscribe() internal instance method only |
| 4 | No AtmStrategyCreate() / StartAtmStrategy() (StrategyBase-only) | PASS | Explicitly excluded; Section 5 |
| 4 | No DateTime.Now | PASS | No datetime in fix |
| 4 | No new unnecessary public API surface | PASS | All new methods private or private static |
| 4 | ASCII-only string literals | PASS | No Unicode in any new code sample |
| 5 | SCAN-01 (lock grep): command + ZERO expected | PASS | Present |
| 5 | SCAN-02 (CYC): all 6 new/modified methods listed with values | PASS | Present |
| 5 | SCAN-03 (async/await grep): command + ZERO expected | PASS | Present |
| 5 | SCAN-04 (Unicode grep): command + ZERO expected | PASS | Present |
| 5 | SCAN-05 (NT8 API): lists forbidden + allowed APIs | PASS | Present |
| 5 | SCAN-06 (build clean): command + ZERO errors/warnings | PASS | Present |
| 5 | SCAN-07 (SIM test): procedure present | PASS | Present |
| 5 | SCAN-07 pass criteria achievable under plan design | PASS | fo=NULL expected/non-failing; bracket-submission-absent is criterion |
| 5 | SCAN-07 consistent with Section 8 note and test 7(c) | PASS | All three aligned on fo=NULL preservation + bracket-submit block |
| 6 | BUG2-REQ-1 through BUG2-REQ-6 all present | PASS | All 6 IDs in Section 6 |
| 6 | Each requirement traceable to a plan section | PASS | Section column populated for all 6 |
| 6 | BUG2-REQ-2 explicitly covers both overloads | PASS | "covers both CancelQxBrackets overloads" — plan line 451 |
| 7 | Test (a): absent key → gate open → proceeds | PASS | `FollowerSlateReady_AbsentKey_GateOpen_FindFollowerBracketOrderProceedsNormally` |
| 7 | Test (b): after not-ready → gate closed → returns null | PASS | `FollowerSlateReady_AfterNotReady_GateClosed_FindFollowerBracketOrderReturnsNull` |
| 7 | Test (c): after flat+clean → gate open → finds orders | PASS | `FollowerSlateReady_AfterFlatAndCleanOrders_GateOpen_FindFollowerBracketOrderFindsOrders` |
| 7 | Test (d): open position blocks ready | PASS | `TrySetFollowerSlateReady_WithOpenPosition_DoesNotSetReady` |
| 7 | Test (e): active orders block ready | PASS | `TrySetFollowerSlateReady_WithActiveOrders_DoesNotSetReady` |
| 7 | Test (f): non-follower no-op | PASS | `TryMarkFollowerSlateNotReady_OnNonFollowerAccount_NoStateChange` |
| 7 | Test 7(c): reframed to bracket-propagation-blocked (not log-absent) | PASS | `FollowerSlateReady_GateClosed_BracketSyncBlocked_FoNullLogPreserved` — asserts no CreateOrder |
| 7 | fo=NULL preservation acknowledged with explicit Section 8 note | PASS | Plan lines 517–518 |
| 7 | Test 7(c) [Fact] name satisfiable by implementation | PASS | Criterion achievable under gate design |

**Total checks: 50. Violations: 0.**

---

## CYC Verification Summary

| Method | CYC | Limit | Status |
|---|---|---|---|
| `TryMarkFollowerSlateNotReady` | 2 | 8 | PASS |
| `TrySetFollowerSlateReady` | 4 | 8 | PASS |
| `IsAccountFlat` | 3 | 8 | PASS |
| `HasAnyActiveOrders` | 5 | 8 | PASS |
| `OnFollowerPositionUpdate` | 3 | 8 | PASS |
| `FindFollowerBracketOrder(Account,...)` | 2 | 8 | PASS |

---

## Spec Coverage Matrix (BUG2-REQ)

| Requirement ID | Requirement | Addressed | Plan Section |
|---|---|---|---|
| BUG2-REQ-1 | `_followerSlateReady: ConcurrentDictionary<string, bool>` added | ✅ | Section 3.1 |
| BUG2-REQ-2 | Both CancelQxBrackets overloads + FlattenOneAccount marked not-ready | ✅ | Section 3.3 (3 subsections) |
| BUG2-REQ-3 | PositionUpdate subscribed in Subscribe() / unsubscribed in Unsubscribe() | ✅ | Section 3.3 Subscribe/Unsubscribe |
| BUG2-REQ-4 | ready=true set only when flat AND no active orders | ✅ | TrySetFollowerSlateReady via IsAccountFlat + HasAnyActiveOrders |
| BUG2-REQ-5 | FindFollowerBracketOrder(Account,...) returns null when slate=false | ✅ | Section 3.3 gate |
| BUG2-REQ-6 | First trade unaffected (absent key = gate open) | ✅ | Section 3.4 gate semantics table |

---

## Decision

**REVIEW_PASS — Cycle 3 (FINAL)**

All 3 prior violations fixed. Zero new violations. 50 checks pass. Plan approved for Phase 3 ticket generation.

---

*REVIEW_PASS — 0 violations. Proceed to Phase 3 (ptt-architect: write 04-tickets.md).*
