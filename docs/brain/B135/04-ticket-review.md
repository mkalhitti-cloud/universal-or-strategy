# B135 Ticket Review

**Epic**: B135 -- Two-Ticket: DW-B146 (second drag fo=null) + DW-B134-OCO (PTT drag orphan sweep)
**Reviewer**: ptt-ticket-reviewer
**Review Phase**: 3.5
**Date**: 2026-09-07
**Input tickets**: `docs/brain/B135/04-tickets.md` (TICKETS_COMPLETE)
**Plan**: `docs/brain/B135/02-architecture-plan.md` (REVIEW_PASS, Cycle 2)

---

## Rules Catalog Gate (Pre-Review)

P0 rules applicable to this epic (checked against ticket descriptions):

| Rule | Severity | Applicability |
|------|----------|---------------|
| JS-001 (no throw in hot path) | P0 | `MatchesLeaderName`, `FindFollowerBracketOrder`, `TrySweptPttDragOrphans`, `CancelPttDragOrphansForAccount` |
| JS-002 (no bare return null) | P0 | `FindFollowerBracketOrder` nullable contract |
| JS-021 (no lock()) | P0 | All new and modified methods |
| JS-033 (no async void) | P0 | All new void methods |

No P0 violations found during pre-review scan of ticket descriptions.

**Rules Catalog Gate: PASS**

---

## Ticket 1 -- DW-B146: MatchesLeaderName helper + FindFollowerBracketOrder second-drag fix

### TR1 -- 7-Scan Checklist Presence

| Scan | Present? | Command Specified? | Threshold Stated? |
|------|----------|--------------------|-------------------|
| SCAN-01 (lock() ban) | YES | `grep -n "lock(" src/PropTraderTools/CopyEngine.cs` | 0 matches |
| SCAN-02 (throw new ban) | YES | `grep -n "throw new" src/PropTraderTools/CopyEngine.cs` | 0 matches in modified scope |
| SCAN-03 (non-ASCII bytes) | YES | PowerShell `ReadAllBytes` on both CopyEngine.cs and B135Tests.cs | Count = 0 for both |
| SCAN-04 (CYC verification) | YES | Manual count specified for `MatchesLeaderName` (=5) and `FindFollowerBracketOrder` (=8) | Required values stated |
| SCAN-05 (return null documentation) | YES | Existing `return null` at L2571 identified as unchanged; no new return null | No new return null introduced |
| SCAN-06 (build) | YES | `dotnet build src/PropTraderTools/PropTraderTools.csproj` | 0 errors, 0 warnings introduced |
| SCAN-07 (prior test regression guard) | YES | `dotnet test` with per-suite PASS counts: B134:8, B133:10, B132:6, B131:7, B130:8, B129:13, B135 T1:7 = 59 total | All counts stated |

**Scan Checklist T1: PASS** (SCAN-01 through SCAN-07 all present with commands and thresholds)

### TR2 -- CYC Pre-Check (Ticket 1)

| Method | Pre-B135 CYC | Post-B135 CYC | Limit | Claimed in Ticket | Match Plan? | Pass? |
|--------|-------------|---------------|-------|-------------------|-------------|-------|
| `FindFollowerBracketOrder` list overload | 8 | 8 | 8 | 8 (AT LIMIT, 1-for-1 guard replacement) | YES (plan §C CYC table) | YES |
| `MatchesLeaderName` (new) | -- | 5 | 8 | 5 (base+null+exact+!isStop&&TGT+isStop&&STP) | YES (plan §B.2) | YES |
| `SignalOrNameMatches` | 3 | 3 | 8 | Unchanged, not modified | YES | YES |

CYC budget for `FindFollowerBracketOrder` post-B135: foreach(1) + SignalOrNameMatches(1) + MatchesLeaderName(1) + state×3(3) + isStop(1) + type(1) = **8**. AT LIMIT; confirmed PASS.

**CYC Pre-Check T1: PASS**

### TR3 -- JS Rule Compliance (Ticket 1)

| Rule | Applies To | Ticket Claim | Verified? |
|------|-----------|--------------|-----------|
| JS-021 (P0) no lock() | `MatchesLeaderName` | Static pure predicate, no shared state, no lock() | PASS |
| JS-021 (P0) no lock() | `FindFollowerBracketOrder` (modified) | Guard replaced in-kind, no state mutation, no lock() | PASS |
| JS-001 (P0) no throw | `MatchesLeaderName` | Returns bool; all 5 paths return a value, no throw | PASS |
| JS-001 (P0) no throw | `FindFollowerBracketOrder` (modified) | Guard replaced in-kind; no throw added | PASS |
| JS-002 (P0) return null contract | `FindFollowerBracketOrder` (modified) | `return null` at L2571 explicitly preserved; Order? nullable contract unchanged | PASS |
| JS-033 (P0) no async void | `MatchesLeaderName`, `MatchesLeaderNameTestable` | Both are synchronous static methods, no async | PASS |
| ASCII-only | "PTT-TGT-Drag", "PTT-STP-Drag" string literals | Confirmed ASCII in ticket §1.7 and SCAN-03 | PASS |

**JS Rule Compliance T1: PASS**

### TR4 -- NT8 Constraint Verification (Ticket 1)

| Constraint | Ticket Claim | Verdict |
|------------|--------------|---------|
| No NT8 API calls in `MatchesLeaderName` | Confirmed in §1.8: pure predicate on `Order.Name` (read-only string property) | PASS |
| `Order.Name` accessible from AddOnBase | Confirmed in §1.8 via NT8_FULL_REFERENCE.md | PASS |
| No async/await in lifecycle methods | Not applicable -- no lifecycle methods modified | N/A |
| No `DateTime.Now` | No DateTime in Ticket 1 code | PASS |
| No CreateOrder without PTT- prefix | No CreateOrder in Ticket 1 | N/A |
| No sealed class on TradeCopierWindow | Not in scope | N/A |
| No hardcoded hex colors | Not in scope | N/A |

**NT8 Constraint T1: PASS**

### TR5 -- Completeness (Ticket 1)

| Check | Status |
|-------|--------|
| Test file B135Tests.cs creation specified (§1.2, NEW) | PASS |
| csproj registration specified (§1.6, after B134Tests.cs entry at L162) | PASS |
| `MatchesLeaderNameTestable` internal seam specified (§1.2, §1.3, §1.c) | PASS |
| `FindFollowerBracketOrderTestable` list-injection seam reused (no new seam needed -- §1.2 "NO CHANGE") | PASS |
| All 7 [Fact] names explicitly stated (§1.10) | PASS |
| 1st drag ATM regression scenario covered | `T1_MatchesLeaderName_ExactName_ReturnsTrue` -- PASS |
| 2nd drag PTT-TGT-Drag scenario covered | `T1_MatchesLeaderName_PttTgtDrag_Target_ReturnsTrue` + `T1_FindFollower_SecondDrag_ReturnsReplacementTarget` -- PASS |
| 2nd stop drag PTT-STP-Drag scenario covered | `T1_MatchesLeaderName_PttStpDrag_Stop_ReturnsTrue` -- PASS |
| null leaderName compatibility scenario covered | `T1_MatchesLeaderName_NullLeaderName_ReturnsTrue` -- PASS |
| No matching PTT order returns false scenario covered | `T1_MatchesLeaderName_WrongName_ReturnsFalse` + `T1_MatchesLeaderName_PttTgtDrag_StopContext_ReturnsFalse` -- PASS |
| T1 minimum count: ≥5 [Fact] | 7 [Fact] -- PASS |
| xUnit only (no NUnit, no MSTest) | Explicitly stated in §1.10 -- PASS |
| Test access via internal seam confirmed (InternalsVisibleTo at L46) | Stated in §1.c comment block -- PASS |

**Completeness T1: PASS**

### TR6 -- BEFORE/AFTER Code Block Verification (Ticket 1)

**Change 1a (CYC comment block L2536-2539)**:

Ticket BEFORE states:
```
// CYC=8 (post-B134). AT LIMIT; PASS.
// foreach(1) + SignalOrNameMatches guard(1) + leaderName exact guard(1) + state filter(3) + isStop(1) + type match(1) = 8.
// DW-B143: Accepted added. DW-B144: Submitted added. DW-B145: leaderName exact guard added.
// JS-021: no lock. JS-001: no throw. JS-002: Order? null contract unchanged.
```

Actual source at L2536-2539 (verified):
```
// CYC=8 (post-B134). AT LIMIT; PASS.
// foreach(1) + SignalOrNameMatches guard(1) + leaderName exact guard(1) + state filter(3) + isStop(1) + type match(1) = 8.
// DW-B143: Accepted added. DW-B144: Submitted added. DW-B145: leaderName exact guard added.
// JS-021: no lock. JS-001: no throw. JS-002: Order? null contract unchanged.
```

**MATCH** -- exact character-for-character correspondence confirmed.

**Change 1b (guard at L2551-2552)**:

Ticket BEFORE states:
```csharp
                if (leaderName != null && order.Name != leaderName) // (1) branch -- B134 DW-B145: require exact name when leaderName provided
                    continue;
```

Actual source at L2551-2552 (verified):
```csharp
                if (leaderName != null && order.Name != leaderName) // (1) branch -- B134 DW-B145: require exact name when leaderName provided
                    continue;
```

**MATCH** -- exact correspondence confirmed.

**Change 1c (insertion point after L2577)**:

Ticket states: insert after L2577 (`=> SignalOrNameMatches(order, signalName, leaderName);`), before L2579 (`internal Order? FindFollowerBracketOrderTestable(`).

Actual source: L2577 = `=> SignalOrNameMatches(order, signalName, leaderName);` and L2579 = `internal Order? FindFollowerBracketOrderTestable(` (Account overload). **MATCH** -- insertion point is correctly identified.

**BEFORE/AFTER T1: PASS**

### TR7 -- Scope Hygiene (Ticket 1)

| Out-of-Scope Item | Explicitly Listed in §1.13? |
|-------------------|-----------------------------|
| `SignalOrNameMatches` (L2511-2518) | YES -- "DO NOT modify" |
| `SyncAtmFollowerTarget` | YES -- "DO NOT touch" |
| `SyncAtmFollowerBracket` | YES -- "DO NOT touch" |
| `OnOrderUpdate` | YES -- "DO NOT touch (T2 only)" |
| `TrySweptPttDragOrphans` | YES -- "DO NOT create (T2 only)" |
| `CancelPttDragOrphansForAccount` | YES -- "DO NOT create (T2 only)" |
| `_diagnosticMode` field (L412) | YES -- "DO NOT touch" |
| B129-B134 test files | YES |

**Scope Hygiene T1: PASS**

### Ticket 1 Verdict

| Check | Result |
|-------|--------|
| Traceability | PASS (DW-B146, B135-T1 cited in §1.1) |
| JS Pre-Check | PASS |
| CYC Pre-Check | PASS |
| NT8 Check | PASS |
| Test Coverage | PASS (7 [Fact] with explicit names + test seam specified) |
| Scan Checklist | PASS (SCAN-01 through SCAN-07 all present) |
| File Routing | PASS (src/PropTraderTools/ throughout) |
| BEFORE/AFTER Blocks | PASS (verified against actual source) |
| Scope Hygiene | PASS |

**T1 VERDICT: TICKET_REVIEW_PASS**

---

## Ticket 2 -- DW-B134-OCO: Orphaned PTT-Drag sweep on position flat

### TR1 -- 7-Scan Checklist Presence

| Scan | Present? | Command Specified? | Threshold Stated? |
|------|----------|--------------------|-------------------|
| SCAN-01 (lock() ban) | YES | `grep -n "lock(" src/PropTraderTools/CopyEngine.cs` | 0 matches |
| SCAN-02 (throw new ban) | YES | `grep -n "throw new" src/PropTraderTools/CopyEngine.cs` | 0 matches in modified scope |
| SCAN-03 (non-ASCII bytes) | YES | PowerShell `ReadAllBytes` on both CopyEngine.cs and B135Tests.cs | Count = 0 for both |
| SCAN-04 (CYC verification) | YES | Manual counts for `TrySweptPttDragOrphans` (=5), `CancelPttDragOrphansForAccount` (=5), `OnOrderUpdate` (=8 unchanged) | Required values stated |
| SCAN-05 (return null documentation) | YES | Both new methods are void; no new return null; OnOrderUpdate existing paths unchanged | No new return null introduced |
| SCAN-06 (build) | YES | `dotnet build src/PropTraderTools/PropTraderTools.csproj` | 0 errors, 0 warnings introduced |
| SCAN-07 (prior test regression guard) | YES | `dotnet test` with per-suite PASS counts: B134:8, B133:10, B132:6, B131:7, B130:8, B129:13, B135 T1:7, B135 T2:5 = 64 total | All counts stated |

**Scan Checklist T2: PASS** (SCAN-01 through SCAN-07 all present with commands and thresholds)

### TR2 -- CYC Pre-Check (Ticket 2)

| Method | CYC | Limit | Branch Count | Match Plan? | Pass? |
|--------|-----|-------|-------------|-------------|-------|
| `TrySweptPttDragOrphans` (new) | 5 | 8 | base(1)+null(1)+Filled(1)+follower(1)+flat(1)=5 | YES (plan §D CYC table) | YES |
| `CancelPttDragOrphansForAccount` (new) | 5 | 8 | base(1)+foreach(1)+state(1)+instr(1)+name(1)=5 | YES (plan §D CYC table) | YES |
| `OnOrderUpdate` (call added) | 8 | 8 | Call adds 0 McCabe branches | YES (plan §B.4, §D) | YES |

Note on `catch (Exception ex)` in `CancelPttDragOrphansForAccount`: correctly stated as adding 0 McCabe branches per standard McCabe counting (exception handlers are not conditional branches in the normal control flow). CYC=5 confirmed.

**CYC Pre-Check T2: PASS**

### TR3 -- JS Rule Compliance (Ticket 2)

| Rule | Applies To | Ticket Claim | Verified? |
|------|-----------|--------------|-----------|
| JS-021 (P0) no lock() | `TrySweptPttDragOrphans` | Guard returns void with early exits, no lock() | PASS |
| JS-021 (P0) no lock() | `CancelPttDragOrphansForAccount` | `acc.Orders.ToList()` is NT8-thread-safe established pattern, no lock() | PASS |
| JS-021 (P0) no lock() | `OnOrderUpdate` (modified) | Call statement only, no lock() added | PASS |
| JS-001 (P0) no throw | `TrySweptPttDragOrphans` | void with guard returns, no throw | PASS |
| JS-001 (P0) no throw | `CancelPttDragOrphansForAccount` | try/catch absorbs `UnableToCancelOrder`; no rethrow | PASS |
| JS-002 (P0) return null | `TrySweptPttDragOrphans` | void return -- no null return | PASS |
| JS-002 (P0) return null | `CancelPttDragOrphansForAccount` | void return -- no null return | PASS |
| JS-033 (P0) no async void | Both new void methods | Synchronous void, no async keyword | PASS |
| ASCII-only | "PTT-TGT-Drag", "PTT-STP-Drag", "PTT drag sweep" literals | Confirmed ASCII in §2.5 | PASS |

**JS Rule Compliance T2: PASS**

### TR4 -- NT8 Constraint Verification (Ticket 2)

| API / Constraint | Ticket Claim | Source | Verdict |
|------------------|--------------|--------|---------|
| `acc.Cancel(new Order[]{o})` -- correct AddOnBase syntax | Confirmed §2.6 via NT8_FULL_REFERENCE.md L2408-2452 and NT8_ADDON_KNOWLEDGE.md L222; wrapped in try/catch | CONFIRMED -- existing pattern SyncAtmFollowerBracket L2259-2266 | PASS |
| `acc.Orders.ToList()` -- correct snapshot pattern | Confirmed §2.6 via NT8_ADDON_KNOWLEDGE.md L219; safe in OnOrderUpdate callback; existing pattern L2322 | CONFIRMED | PASS |
| `IsFollowerAccount(acc)` -- existing method, not new | Confirmed §2.6: "CopyEngine internal: YES, L1536 existing usage" | Existing method, no new creation needed | PASS |
| `IsFlat(FindPosition(...))` -- existing methods, not new | Confirmed §2.6: "CopyEngine private: YES, L4002-4070"; established pattern: TryEvictFollowerBeSlot L1538 | Existing methods, no new creation needed | PASS |
| `o.Instrument?.FullName` -- null-safe property access | AddOnBase: YES (NT8_FULL_REFERENCE.md); `?.` pattern used consistently in CopyEngine | PASS |
| No async/await in lifecycle methods | No lifecycle methods touched by T2 | N/A |
| No `DateTime.Now` | No DateTime in T2 code | PASS |
| No new PositionUpdate subscriptions (Subscribe/Unsubscribe untouched) | Explicitly deferred in §2.11 and explained in §2.6 "Why NOT PositionUpdate" | PASS |
| `OnOrderUpdate` pre-Gate-1 insertion (after TryEvictFollowerBeSlot, before `_isCopyEnabled` check) | Hook at L1316, before Gate-1 at L1369; same rationale as TryEvictFollowerBeSlot | PASS |

**NT8 Constraint T2: PASS**

### TR5 -- Completeness (Ticket 2)

| Check | Status |
|-------|--------|
| Test file B135Tests.cs reused (same file, add B135Ticket2Tests class -- §2.2) | PASS |
| `TrySweptPttDragOrphansTestable` internal seam specified (§2.2, §2.3, §2.b) | PASS |
| `CancelPttDragOrphansForAccountTestable` internal seam specified (§2.2, §2.3, §2.b) | PASS |
| All 5 [Fact] names explicitly stated (§2.8) | PASS |
| Scenario (a): flat cancels PTT-TGT-Drag | `T2_CancelPttDragOrphans_CancelsWorkingTgtDrag` -- PASS |
| Scenario (b): flat cancels PTT-STP-Drag | `T2_CancelPttDragOrphans_CancelsWorkingStpDrag` -- PASS |
| Scenario (c): non-PTT Working orders NOT cancelled | `T2_CancelPttDragOrphans_IgnoresNonPttOrders` -- PASS |
| Scenario (d): partial fill does NOT trigger sweep | `T2_TrySwept_PartialFill_NotFlat_DoesNotSweep` -- PASS |
| Scenario (e): acc.Cancel exception absorbed | `T2_CancelPttDragOrphans_ExceptionAbsorbed_NoRethrow` -- PASS |
| T2 minimum count: ≥5 [Fact] | 5 [Fact] -- PASS |
| xUnit only (no NUnit, no MSTest) | Explicitly stated in §2.8 -- PASS |
| Precondition (T1 BUILD_PASS + VERIFY_PASS) stated | YES -- header of T2 -- PASS |

**Completeness T2: PASS**

### TR6 -- BEFORE/AFTER Code Block Verification (Ticket 2)

**Change 2a (OnOrderUpdate call insertion at L1316)**:

Ticket context BEFORE (L1315-1318):
```csharp
            // DW-B79-06: evict stale BE retry slot when follower position closes via any path.
            TryEvictFollowerBeSlot(e);

            // DW-B79-08: PTT-BE bracket wipe recovery.
```

Actual source at L1315-1318 (verified):
```
            // DW-B79-06: evict stale BE retry slot when follower position closes via any path.
            TryEvictFollowerBeSlot(e);

            // DW-B79-08: PTT-BE bracket wipe recovery.
```

**MATCH** -- exact correspondence confirmed. The insertion point is correctly identified.

Ticket AFTER adds `TrySweptPttDragOrphans(e)` with `// B135 DW-B134-OCO:` comment between `TryEvictFollowerBeSlot(e)` and the `// DW-B79-08:` comment. This is a call statement only; zero McCabe branches added to `OnOrderUpdate`. CYC delta = 0. **CORRECT**.

**Change 2b (TrySweptPttDragOrphans + CancelPttDragOrphansForAccount definition)**:

Insertion point stated as "after `TryEvictFollowerBeSlot` method definition (~L1557)". No BEFORE block needed (pure addition). Method bodies in §2.4 are consistent with plan §D exact code. **CONSISTENT**.

**BEFORE/AFTER T2: PASS**

### TR7 -- Scope Hygiene (Ticket 2)

| Out-of-Scope Item | Explicitly Listed in §2.11? |
|-------------------|-----------------------------|
| `FindFollowerBracketOrder` | YES -- "DO NOT touch (T1 only)" |
| `MatchesLeaderName` | YES -- "DO NOT touch (T1 only)" |
| `SyncAtmFollowerTarget` | YES -- "DO NOT touch" |
| `SyncAtmFollowerBracket` | YES -- "DO NOT touch" |
| `_diagnosticMode` field (L412) | YES -- "DO NOT touch" |
| DW-B134-OCO OBS-A through OBS-D (deferred items) | YES -- explicitly listed |
| DW-B147 rawPrice==newPrice guard | YES -- "DEFERRED, not in scope" |
| `Subscribe()`/`Unsubscribe()` (no PositionUpdate subscriptions) | YES -- "DO NOT add PositionUpdate subscriptions" |
| B129-B134 test files | YES |

**Scope Hygiene T2: PASS**

### Ticket 2 Verdict

| Check | Result |
|-------|--------|
| Traceability | PASS (DW-B134-OCO, B135-T2 cited in §2.1) |
| JS Pre-Check | PASS |
| CYC Pre-Check | PASS |
| NT8 Check | PASS |
| Test Coverage | PASS (5 [Fact] with explicit names + both test seams specified) |
| Scan Checklist | PASS (SCAN-01 through SCAN-07 all present) |
| File Routing | PASS (src/PropTraderTools/ throughout) |
| BEFORE/AFTER Blocks | PASS (verified against actual source) |
| Scope Hygiene | PASS |

**T2 VERDICT: TICKET_REVIEW_PASS**

---

## TR8 -- Violations Found

**None.**

All checks across both tickets passed. No JS-XXX rule violations, no NT8 constraint violations, no missing scan entries, no traceability gaps, no file routing errors, and no scope creep detected.

---

## Aggregate Checks

| Aggregate Check | Result |
|----------------|--------|
| Execution order: SEQUENTIAL (T1 BUILD_PASS + VERIFY_PASS before T2) | PASS -- stated in ticket header |
| Spec coverage (DW-B146 + DW-B134-OCO): both in exactly one ticket each | PASS |
| Total [Fact] count: 7 T1 + 5 T2 = 12 | PASS -- matches ticket header |
| Regression baseline: 52 prior tests (B129:13 + B130:8 + B131:7 + B132:6 + B133:10 + B134:8) | PASS -- per-suite counts stated in SCAN-07 of each ticket |
| File routing: all .cs paths in `src/PropTraderTools/` | PASS -- no director workspace references |
| Plan alignment: tickets match REVIEW_PASS plan exactly | PASS -- code bodies match plan §B.2 and §D verbatim |
| DW-B147 deferred (not in any ticket): documented in Appendix B | PASS |

---

## Overall Verdict

**TICKET_REVIEW_PASS**

Both tickets are cleared for engineering execution. The engineer should proceed with Ticket 1 first, achieving BUILD_PASS + VERIFY_PASS (all 7 T1 [Fact] + 52 prior = 59 total PASS), then proceed to Ticket 2.
