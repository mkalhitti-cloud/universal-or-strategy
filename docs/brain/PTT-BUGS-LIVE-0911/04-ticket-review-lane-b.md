# Ticket Review — PTT-BUGS-LIVE-0911 Lane B
**BUG-2: Post-Exit Clean Slate (follower fo=NULL bracket failure)**
**Reviewer: ptt-ticket-reviewer**
**Date: 2026-09-11**
**Source ticket:** `docs/brain/PTT-BUGS-LIVE-0911/04-tickets-lane-b.md`
**Source plan:** `docs/brain/PTT-BUGS-LIVE-0911/02-architecture-plan-lane-b.md` (REVIEW_PASS Cycle 3 FINAL)
**Plan review:** `docs/brain/PTT-BUGS-LIVE-0911/02-plan-review-lane-b.md` (REVIEW_PASS Cycle 3)
**Spec evidence:** `docs/brain/live-trading-analysis-2026-09-11.md`

---

## T1 — BUG-2: Post-Exit Clean Slate Gate

### Check 1 — TRACEABILITY

Every change in the ticket traces to a BUG2-REQ-N requirement. Verification:

| Ticket item | Maps to requirement | Spec evidence |
|---|---|---|
| `_followerSlateReady` field | BUG2-REQ-1 | spec §BUG-2: "Block bracket propagation until clean slate confirmed" |
| `TryMarkFollowerSlateNotReady` in CancelQxBrackets (2-param, 3-param) + FlattenOneAccount | BUG2-REQ-2 | spec §BUG-2: "Any exit that replaces standard named bracket orders…leaves stale order name state" |
| `Subscribe()` / `Unsubscribe()` PositionUpdate wiring | BUG2-REQ-3 | spec §BUG-2: "when PositionStateChanged hasPos=False fires…run a cleanup pass" |
| `OnFollowerPositionUpdate` + `TrySetFollowerSlateReady` + `IsAccountFlat` + `HasAnyActiveOrders` | BUG2-REQ-4 | spec §BUG-2: "verify order slate is empty, only then mark follower ready" |
| Gate in `FindFollowerBracketOrder(Account, ...)` | BUG2-REQ-5 | spec §BUG-2: "Block bracket propagation until clean slate confirmed" |
| Absent-key = gate open | BUG2-REQ-6 | spec §Trade 2: "First trade of day was clean because no prior state existed" |

All 6 BUG2-REQ-N requirements are covered. No undocumented scope additions found. All methods and fields listed in ticket Section 4 match identically to the plan's Affected Methods Summary (plan Section 4). No phantom work items detected.

**Traceability: PASS**

---

### Check 2 — COMPLETENESS vs PLAN

#### Field
- `_followerSlateReady: ConcurrentDictionary<string, bool>` with `StringComparer.Ordinal` — present in ticket Section 3, matches plan Section 3.1 exactly. ✅

#### New methods (5 required, 5 present)
| Method | In ticket | Signature matches plan |
|---|---|---|
| `TryMarkFollowerSlateNotReady(Account)` | Section 4.1.1 | ✅ |
| `TrySetFollowerSlateReady(Account)` | Section 4.1.2 | ✅ |
| `IsAccountFlat(Account)` — `private static bool` | Section 4.1.3 | ✅ |
| `HasAnyActiveOrders(Account)` — `private static bool` | Section 4.1.4 | ✅ |
| `OnFollowerPositionUpdate(object, PositionEventArgs)` | Section 4.1.5 | ✅ |

#### Modified methods (5+ required, 6 present — plan lists exactly these 6)
| Method | In ticket | Matches plan |
|---|---|---|
| `FindFollowerBracketOrder(Account, string?, bool, string?)` | Section 4.2.1 | ✅ |
| `Subscribe()` | Section 4.2.2 | ✅ |
| `Unsubscribe()` | Section 4.2.3 | ✅ |
| `CancelQxBrackets(Account, Instrument)` 2-param | Section 4.2.4 | ✅ |
| `CancelQxBrackets(Account, Instrument, HashSet<Order>)` 3-param | Section 4.2.5 | ✅ |
| `FlattenOneAccount(Account, Instrument)` | Section 4.2.6 | ✅ |

#### CYC bounds
| Method | Ticket states | Plan limit | Status |
|---|---|---|---|
| `TryMarkFollowerSlateNotReady` | CYC = 2 | ≤ 8 | PASS |
| `TrySetFollowerSlateReady` | CYC = 4 | ≤ 8 | PASS |
| `IsAccountFlat` | CYC = 3 | ≤ 8 | PASS |
| `HasAnyActiveOrders` | CYC = 5 | ≤ 8 | PASS |
| `OnFollowerPositionUpdate` | CYC = 3 | ≤ 8 | PASS |
| `FindFollowerBracketOrder(Account,...)` | CYC = 2 | ≤ 8 | PASS |

All CYC values match plan Section 3.2 and plan-review CYC table exactly.

**Completeness vs Plan: PASS**

---

### Check 3 — 7-SCAN CHECKLIST

All 7 scans present in ticket Section 7. Each verified:

| Scan | Present | Full pass criteria stated | Notes |
|---|---|---|---|
| SCAN-01 (lock grep) | ✅ | ✅ — ZERO matches, scope stated | Command: `grep -r "lock(" src/PropTraderTools/CopyEngine.cs` |
| SCAN-02 (CYC check) | ✅ | ✅ — all 6 methods listed with individual CYC values and LIMIT=8 | |
| SCAN-03 (async/await grep) | ✅ | ✅ — ZERO in new methods; `OnFollowerPositionUpdate` must be sync void explicitly stated | |
| SCAN-04 (ASCII grep) | ✅ | ✅ — ZERO matches in new code; command specified | |
| SCAN-05 (NT8 API compliance) | ✅ | ✅ — 3 forbidden grep commands + AddOnBase confirmation list | |
| SCAN-06 (build clean) | ✅ | ✅ — ZERO errors/warnings; common failure modes documented | |
| SCAN-07 (SIM test) | ✅ | ✅ — 5-step procedure + explicit PASS/FAIL criteria | |

**SCAN-07 fo=NULL semantics check (non-negotiable):**
Ticket SCAN-07 Step 3 states: *"Expected: fo=NULL log entries MAY appear during this window. This is correct behavior…These fo=NULL entries during gate-closed window are EXPECTED and do NOT constitute a scan failure."*
SCAN-07 PASS CRITERIA states: *"fo=NULL log entries during the gate-closed window are EXPECTED and do NOT constitute a scan failure."*
SCAN-07 FAIL CRITERIA correctly focuses on `Account.CreateOrder` calls while gate is closed and regression on Trade 2. ✅

The stale procedure step 7 noted as Non-Blocking in the plan review (plan-review §Non-Blocking Finding) is correctly resolved in the ticket: Section 7 SCAN-07 step 3 replaces the stale "verify no fo=NULL" language with explicit expected-behavior language. ✅

**Scan Checklist: PASS**

---

### Check 4 — NT8 API COMPLIANCE

| Check | Ticket section | Result |
|---|---|---|
| No `AtmStrategyCreate()` | Section 6 NT8 compliance table; Section 9 prohibited list | PASS — explicitly listed as prohibited |
| No `StartAtmStrategy()` | Section 6 NT8 compliance table; SCAN-05 step 2 | PASS — explicitly prohibited |
| No `AtmStrategyChangeStopTarget()` | Section 6 NT8 compliance table; SCAN-05 step 3 | PASS — explicitly prohibited |
| `account.PositionUpdate` is AddOnBase-available | Section 6: "TradeCopierPanel.cs line 861 (existing usage)" | PASS |
| `PositionEventArgs.Position.Account` is AddOnBase-available | Section 6: "TradeCopierPanel.cs line 2109 (existing handler)" | PASS |
| `Account.Positions` is AddOnBase-available | Section 6 | PASS |
| `Account.Orders` + `.ToList()` snapshot | Section 4.1.4: "standard pattern in CopyEngine (line 4898)" | PASS |
| No `Account.All` in constructor | `Account.All` used only in `Subscribe()`/`Unsubscribe()` (internal instance methods) | PASS |
| No `lock()` anywhere in implementation specs | All new methods use `ConcurrentDictionary` only; Section 9 explicitly prohibits lock() | PASS |
| No `async`/`await` in event handler or lifecycle methods | Section 4.1.5 and SCAN-03 both explicitly require synchronous void | PASS |
| No `DateTime.Now` | Section 9 prohibited list: "Must use DateTime.UtcNow if datetime needed (not needed in this fix)" | PASS |
| `OnFollowerPositionUpdate` threading note | Section 4.1.5: "Fires on NT8 account background thread. No UI access permitted." | PASS |

**NT8 API Compliance: PASS**

---

### Check 5 — JANE STREET RULES

| Rule | Check | Ticket location | Result |
|---|---|---|---|
| JS-021 | No `lock()` blocks — `ConcurrentDictionary` only | Sections 4.1.1–4.1.5, 4.2.x comments all cite JS-021; field declaration uses `ConcurrentDictionary` | PASS |
| JS-001 | No `throw` in hot path | All new methods use early-return pattern; no throw in any implementation block | PASS |
| JS-002 | No null return on non-nullable contracts | `Order?` null contract unchanged; bool/void helpers explicit; ticket Section 4.2.1: "Order? null contract unchanged" | PASS |
| JS-008 | No `DateTime.Now` | Section 9: explicitly prohibited; not used in fix | PASS |
| JS-009 | No `Dictionary<K,V>` for shared state | Section 9 prohibited: "Must be `ConcurrentDictionary`"; field uses `ConcurrentDictionary` | PASS |
| JS-023 | No UI update from non-UI thread without Dispatcher | Section 4.1.5 threading note: "No UI operations"; SCAN-01 scope; no Dispatcher needed because no UI access | PASS |
| JS-025 | ConcurrentDictionary as canonical lock-free pattern | `_followerSlateReady` is `ConcurrentDictionary`; JS-025 cited in field declaration comment | PASS |
| CYC ≤ 8 | All methods within limit | Max CYC = 5 (`HasAnyActiveOrders`), all ≤ 8 | PASS |
| No async/await in lifecycle methods | `OnFollowerPositionUpdate` is `void`, not `async void` | PASS |
| ASCII-only string literals | All string literals and comments in ticket use ASCII only; SCAN-04 command present | PASS |

**Jane Street Rules: PASS**

---

### Check 6 — SCOPE LOCK

SCOPE LOCK statement present at ticket top (lines 10–16):

> *"This file contains exactly one ticket (T1). Do not implement any other ticket, any other bug fix, or any change not listed in this document. Every changed line must trace to a requirement in BUG2-REQ-1 through BUG2-REQ-6 below. Changes to TradeCopierAddOn.cs, TradeCopierPanel.cs, TradeCopierWindow.cs, PttQuickExit.cs, or any file outside CopyEngine.cs (tests excepted) are **explicitly prohibited**."*

- Correctly limits scope to T1 only. ✅
- Lists all out-of-scope files by name. ✅
- Section 9 (Prohibited Changes) table reinforces the same constraints with additional detail. ✅
- No cross-ticket references found. ✅

**Scope Lock: PASS**

---

### Check 7 — TEST COVERAGE

7 `[Fact]` tests present in ticket Section 8. Verified against all 3 required categories:

| # | Test method name | REQ coverage | Category satisfied |
|---|---|---|---|
| 1 | `FollowerSlateReady_AbsentKey_GateOpen_FindFollowerBracketOrderProceedsNormally` | BUG2-REQ-1 + BUG2-REQ-6 | (a) gate-open on absent key ✅ |
| 2 | `FollowerSlateReady_AfterNotReady_GateClosed_FindFollowerBracketOrderReturnsNull` | BUG2-REQ-2 + BUG2-REQ-5 | (a) gate blocks on slate=false ✅ |
| 3 | `FollowerSlateReady_AfterFlatAndCleanOrders_GateOpen_FindFollowerBracketOrderFindsOrders` | BUG2-REQ-4 + BUG2-REQ-5 | (b) slate ready after flat+clean ✅ |
| 4 | `TrySetFollowerSlateReady_WithOpenPosition_DoesNotSetReady` | BUG2-REQ-4 | (b) IsAccountFlat guard ✅ |
| 5 | `TrySetFollowerSlateReady_WithActiveOrders_DoesNotSetReady` | BUG2-REQ-4 | (b) HasAnyActiveOrders guard ✅ |
| 6 | `TryMarkFollowerSlateNotReady_OnNonFollowerAccount_NoStateChange` | BUG2-REQ-6 | (a) non-follower no-op ✅ |
| 7 | `FollowerSlateReady_GateClosed_BracketSyncBlocked_FoNullLogPreserved` | BUG2-REQ-2 + BUG2-REQ-5 | (c) bracket-propagation-blocked (not log-absent) ✅ |

Category verification:
- **(a) Gate blocks on slate=false:** Tests 1, 2, 6 cover the gate-closed, gate-open-absent, and non-follower guard scenarios. ✅
- **(b) Slate ready after flat+clean:** Tests 3, 4, 5 cover the affirmative ready path and both negative guards (open position, active orders). ✅
- **(c) Bracket-propagation-blocked (not log-absent):** Test 7 explicitly asserts "no `CreateOrder` calls" and states "The test does NOT assert log absence." ✅

**Satisfiability check for Test 7:** The criterion (no `Account.CreateOrder` calls on follower while gate is closed) is achievable under the proposed `FindFollowerBracketOrder` gate design — when the gate returns `null`, `SyncFollowerBracket` returns at `if (fo == null) return` before any `CreateOrder` call. The test description correctly identifies this causal chain. ✅

All 7 tests have names, descriptions, and clearly stated assert criteria. No shared mutable state between tests is described.

**Test Coverage: PASS**

---

### Check 8 — LINE NUMBER ACCURACY (spot-check, ≥3 required)

Verified against `src/PropTraderTools/CopyEngine.cs`:

| Ticket claim | Actual source | Accuracy |
|---|---|---|
| Line 394: `_drainOwnedOrderIds` declaration; insert field after this line | Line 394: `new ConcurrentDictionary<string, byte>(StringComparer.Ordinal);` ✅ | **EXACT** |
| Line 396: `// --- Status event ---` comment follows | Line 396: `// --- Status event ---` ✅ | **EXACT** |
| Lines 938–941: `CancelQxBrackets(2-param)` starts line 938, null guard at 940–941 | Lines 938–941 exactly match ticket's quoted current code ✅ | **EXACT** |
| Lines 1052–1059: `CancelQxBrackets(3-param)` signature starts 1052, null guard at 1058–1059 | Lines 1052–1059 exactly match ✅ | **EXACT** |
| Lines 1499–1503: `Subscribe()` current code | Lines 1499–1503 exactly match quoted current code ✅ | **EXACT** |
| Lines 1505–1509: `Unsubscribe()` current code | Lines 1505–1509 exactly match ✅ | **EXACT** |
| Lines 3902–3913: `FindFollowerBracketOrder(Account,...)` expression-body form | Lines 3902–3913 exactly match quoted current code ✅ | **EXACT** |
| Lines 5417–5425: `FlattenOneAccount` with `IsAccountFlattenable` guard and `CancelAllAccountOrders` | Lines 5417–5425 exactly match ✅ | **EXACT** |

All 8 spot-checked locations are exact. No line number is off by even 1 line.

**Line Number Accuracy: PASS**

---

### Check 9 — FILE ROUTING

- Primary source file: `src/PropTraderTools/CopyEngine.cs` — Wave workspace path. ✅
- Test file: `src/PropTraderTools/CopyEngineTests.cs` — Wave workspace path. ✅
- No Director workspace paths (e.g., `c:\WSGTA\universal-or-strategy-director\`) referenced for `.cs` files. ✅

**File Routing: PASS**

---

## Aggregate Summary

| Check | Result |
|---|---|
| 1. Traceability | PASS |
| 2. Completeness vs Plan | PASS |
| 3. 7-Scan Checklist | PASS |
| 4. NT8 API Compliance | PASS |
| 5. Jane Street Rules | PASS |
| 6. Scope Lock | PASS |
| 7. Test Coverage | PASS |
| 8. Line Number Accuracy | PASS |
| 9. File Routing | PASS |

**Violations found: ZERO**

---

## Overall: TICKET_REVIEW_PASS

**The ticket is approved for Phase 4a engineering.** All 9 checks pass with zero violations. The ticket provides the engineer with:

- All 6 spec requirement IDs (BUG2-REQ-1 through BUG2-REQ-6) with implementation mapping
- Exact current-code blocks and full replacement blocks for every modified method
- Exact signatures and full implementations for all 5 new methods
- 7 `[Fact]` test names with assert descriptions covering all required categories
- Complete 7-scan checklist (SCAN-01 through SCAN-07) with pass/fail criteria per scan
- SCAN-07 correctly stating fo=NULL is EXPECTED during gate-closed window (not a failure), with pass criterion on no-CreateOrder-while-gate-closed
- All CYC bounds stated and within plan limits
- No lock(), no async/await, no StrategyBase-only APIs, no DateTime.Now, no non-ASCII

---

*TICKET_REVIEW_PASS*
