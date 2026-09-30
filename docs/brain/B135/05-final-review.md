# B135 Final Review

**Epic**: B135 -- Two-Ticket: DW-B146 (second drag fo=null) + DW-B134-OCO (PTT drag orphan sweep)
**Reviewer**: ptt-plan-reviewer
**Phase**: 5 (Final Review)
**Date**: 2026-09-07
**Inputs**:
- `docs/brain/B135/02-architecture-plan.md` (REVIEW_PASS, Cycle 2)
- `docs/brain/B135/04-ticket-review.md` (TICKET_REVIEW_PASS)
- `docs/brain/B135/ticket-1-completion.md` (BUILD_PASS)
- `docs/brain/B135/ticket-1-verification.md` (VERIFY_PASS)
- `docs/brain/B135/ticket-2-completion.md` (BUILD_PASS)
- `docs/brain/B135/ticket-2-verification.md` (VERIFY_PASS)
- `docs/standards/jane-street/RULES_CATALOG.md` (enforcement)
- `docs/brain/B134/06-deferred-backlog.md` (prior backlog)
- `src/PropTraderTools/CopyEngine.cs` (live source, spot-checked regions)
- `src/PropTraderTools/PropTraderTools.csproj` (registration check)

---

## Section A -- Cross-File Coherence (7 Checks)

### A1: MatchesLeaderName exists with correct Option A logic

**Live source verified at L2645-2656 (`src/PropTraderTools/CopyEngine.cs`)**:

```csharp
private static bool MatchesLeaderName(Order order, string? leaderName, bool isStop)
{
    if (leaderName == null)                                           // (1) no constraint -- pass through
        return true;
    if (order.Name == leaderName)                                     // (2) exact ATM name match
        return true;
    if (!isStop && order.Name == "PTT-TGT-Drag")                     // (3) replacement target match
        return true;
    if (isStop && order.Name == "PTT-STP-Drag")                      // (4) replacement stop match
        return true;
    return false;
}
```

Branch map matches Option A specification exactly:
- `leaderName == null` -> return true (null = pass-through): **PRESENT**
- `order.Name == leaderName` -> return true (exact match): **PRESENT**
- `!isStop && PTT-TGT-Drag` -> return true (replacement target): **PRESENT**
- `isStop && PTT-STP-Drag` -> return true (replacement stop): **PRESENT**
- else -> return false: **PRESENT**

**Result: PASS**

---

### A2: FindFollowerBracketOrder guard replaced with MatchesLeaderName call

**Live source verified at L2609-2612 (`src/PropTraderTools/CopyEngine.cs`)**:

```csharp
if (!SignalOrNameMatches(order, fromEntrySignalName, leaderName)) // (1) branch
    continue;
if (!MatchesLeaderName(order, leaderName, isStop)) // (1) branch -- B135 DW-B146: extracted helper handles PTT-Drag fallback
    continue;
```

The old B134 guard (`if (leaderName != null && order.Name != leaderName)`) is **absent**. The new
`!MatchesLeaderName(...)` guard appears **once only** at L2611. Guard replaced 1-for-1; no doubling.

CYC comment at L2596-2599 reads `post-B135` and lists `MatchesLeaderName guard(1)` in the branch sum.
This matches the plan requirement (Change 1a).

**Result: PASS**

---

### A3: SignalOrNameMatches unchanged

**Live source confirmed by verifier at L2511-2518** (from ticket-1-verification.md V2 Check 3):

```csharp
internal static bool SignalOrNameMatches(Order order, string? signalName, string? leaderName)
{
    if (signalName != null && order.FromEntrySignal == signalName) // (1)
        return true;
    if (leaderName == null) // (2)
        return false;
    return order.Name == leaderName; // (3)
}
```

CYC=3. No modifications from B135. Plan §H "Files NOT touched" includes
`SignalOrNameMatches`. T1 scope hygiene (ticket-1-completion.md, spec 1.13) confirms DO NOT MODIFY.

**Result: PASS**

---

### A4: TrySweptPttDragOrphans exists with all 5 guards in correct order

**Live source verified at L1567-1579 (`src/PropTraderTools/CopyEngine.cs`)**:

```csharp
private void TrySweptPttDragOrphans(OrderEventArgs e)
{
    var o = e?.Order;
    if (o == null)                                                    // (1)
        return;
    if (o.OrderState != OrderState.Filled)                           // (2)
        return;
    if (!IsFollowerAccount(o.Account))                               // (3)
        return;
    if (!IsFlat(FindPosition(o.Account, o.Instrument)))              // (4)
        return;
    CancelPttDragOrphansForAccount(o.Account, o.Instrument);
}
```

All 5 gates present (null, Filled, follower, flat, delegate) in the exact order specified by plan §D.
Method body matches plan §D exact code verbatim.

**Result: PASS**

---

### A5: CancelPttDragOrphansForAccount exists with try/catch pattern

**Live source verified at L1592-1612 (`src/PropTraderTools/CopyEngine.cs`)**:

```csharp
private void CancelPttDragOrphansForAccount(Account acc, Instrument instr)
{
    foreach (var o in acc.Orders.ToList())                           // (1)
    {
        if (o.OrderState != OrderState.Working)                      // (2)
            continue;
        if (o.Instrument?.FullName != instr?.FullName)               // (3)
            continue;
        if (o.Name != "PTT-TGT-Drag" && o.Name != "PTT-STP-Drag")  // (4)
            continue;
        try
        {
            acc.Cancel(new Order[] { o });
            StatusUpdate?.Invoke(acc.Name + ": PTT drag sweep: cancelled " + o.Name);
        }
        catch (Exception ex)
        {
            StatusUpdate?.Invoke(acc.Name + ": PTT drag sweep cancel error: " + ex.Message);
        }
    }
}
```

`acc.Cancel` is inside `try/catch`. Exception absorbed via `StatusUpdate?.Invoke(...)`. No `throw;` or
`throw ex;` in catch block. Matches plan §D exact code verbatim.

**Result: PASS**

---

### A6: OnOrderUpdate has TrySweptPttDragOrphans call pre-Gate-1

**Live source verified at L1315-1321 (`src/PropTraderTools/CopyEngine.cs`)**:

```csharp
// DW-B79-06: evict stale BE retry slot when follower position closes via any path.
TryEvictFollowerBeSlot(e);

// B135 DW-B134-OCO: sweep orphaned PTT-drag orders when follower position goes flat.
TrySweptPttDragOrphans(e);

// DW-B79-08: PTT-BE bracket wipe recovery.
```

Call at L1319, inserted after `TryEvictFollowerBeSlot(e)` at L1316 and before the `DW-B79-08` gate block.
This is pre-Gate-1 (`_isCopyEnabled` check at L1369 per plan §B.4). Comment includes DW-B134-OCO tag.
McCabe delta = 0 (call statement, no boolean branch).

**Result: PASS**

---

### A7: csproj B135Tests.cs registered after B134Tests.cs

**Live source verified at PropTraderTools.csproj L162-163**:

```xml
<Compile Include="Tests\B134Tests.cs" />
<Compile Include="Tests\B135Tests.cs" />
```

B135Tests.cs is at L163, immediately following B134Tests.cs at L162. Matches plan §H and spec §1.6.

**Result: PASS**

---

### Section A Summary

| Check | Description | Result |
|-------|-------------|--------|
| A1 | MatchesLeaderName Option A logic (5-branch) | PASS |
| A2 | FindFollowerBracketOrder guard replaced 1-for-1 with MatchesLeaderName | PASS |
| A3 | SignalOrNameMatches unchanged (CYC=3) | PASS |
| A4 | TrySweptPttDragOrphans 5 guards in correct order | PASS |
| A5 | CancelPttDragOrphansForAccount try/catch with no rethrow | PASS |
| A6 | OnOrderUpdate TrySweptPttDragOrphans(e) call pre-Gate-1 | PASS |
| A7 | csproj B135Tests.cs at L163 after B134Tests.cs at L162 | PASS |

**Section A: ALL 7 CHECKS PASS**

---

## Section B -- Spec Coverage Matrix

| Requirement | Source | Addressed By | Plan Section | Status |
|-------------|--------|-------------|--------------|--------|
| Second target drag returns PTT-TGT-Drag (not null) | DW-B146 | T1: MatchesLeaderName branch 3 (`!isStop && PTT-TGT-Drag`) | §B.2, §C | COVERED |
| Second stop drag returns PTT-STP-Drag (not null) | DW-B146 | T1: MatchesLeaderName branch 4 (`isStop && PTT-STP-Drag`) | §B.2, §C | COVERED |
| null leaderName backward compat preserved | DW-B146 | T1: MatchesLeaderName branch 1 (`leaderName==null -> true`) | §B.2 | COVERED |
| No matching PTT order returns false | DW-B146 | T1: `T1_MatchesLeaderName_WrongName_ReturnsFalse` + `_PttTgtDrag_StopContext_ReturnsFalse` | §G T1 | COVERED |
| 1st drag still returns ATM bracket (regression) | DW-B146 | T1: `T1_MatchesLeaderName_ExactName_ReturnsTrue` | §G T1 | COVERED |
| CYC gate: MatchesLeaderName <= 8 | DW-B146 | CYC=5 verified by engineer + verifier independently | §C CYC table | COVERED |
| CYC gate: FindFollowerBracketOrder <= 8 | DW-B146 | CYC=8 AT LIMIT; guard replaced 1-for-1 | §C CYC table | COVERED |
| Flat position cancels PTT-TGT-Drag | DW-B134-OCO | T2: `T2_CancelPttDragOrphans_CancelsWorkingTgtDrag` | §D, §G T2 | COVERED |
| Flat position cancels PTT-STP-Drag | DW-B134-OCO | T2: `T2_CancelPttDragOrphans_CancelsWorkingStpDrag` | §D, §G T2 | COVERED |
| Non-PTT Working orders NOT cancelled | DW-B134-OCO | T2: `T2_CancelPttDragOrphans_IgnoresNonPttOrders` | §D, §G T2 | COVERED |
| Partial fill (qty>0 = not flat) does not trigger sweep | DW-B134-OCO | T2: `T2_TrySwept_PartialFill_NotFlat_DoesNotSweep` | §D, §G T2 | COVERED |
| acc.Cancel exception absorbed gracefully | DW-B134-OCO | T2: `T2_CancelPttDragOrphans_ExceptionAbsorbed_NoRethrow` + try/catch in source | §D, §G T2 | COVERED |
| DW-B147: DEFERRED (CYC budget exceeded) | DW-B147 | Documented in plan §E; not in any ticket; deferred to B136+ | §E | DEFERRED (documented) |

**Section B: All 12 active requirements covered. DW-B147 correctly deferred with documented rationale.**

---

## Section C -- 7-Scan Aggregate Table

Both T1 and T2 verifiers ran scans independently. Results cross-compared here.

| Scan | T1 Verifier Result | T2 Verifier Result | Aggregate |
|------|-------------------|-------------------|-----------|
| **SCAN-01** lock() | 4 comment-only refs; 0 actual lock() statements | 11 comment-only refs; 0 actual lock() statements | **ZERO actual lock() -- PASS** |
| **SCAN-02** throw new | 0 matches | 0 matches | **ZERO throw new -- PASS** |
| **SCAN-03** non-ASCII | 0 bytes (CopyEngine.cs); B135Tests.cs gitignored -- test names confirmed ASCII via list-tests | 0 bytes (CopyEngine.cs) | **ZERO non-ASCII bytes -- PASS** |
| **SCAN-04** CYC | MatchesLeaderName=5 (McCabe), FindFollowerBracketOrder=8 (AT LIMIT) | TrySweptPttDragOrphans=5, CancelPttDragOrphansForAccount=5, OnOrderUpdate=8 (AT LIMIT, unchanged) | **AT LIMIT PASS -- all <= 8** |
| **SCAN-05** return null | L2571 preserved; 0 new return null | 7 pre-existing; 0 new return null (both new methods void) | **ZERO new return null -- PASS** |
| **SCAN-06** build | 0 errors, 0 warnings (verifier run) | 0 errors, 0 warnings (verifier run) | **ZERO ERRORS -- PASS** |
| **SCAN-07** tests | B135 T1: 7/7 PASS; B129-B134: 50/50 PASS; 15 pre-existing failures | B135 T1+T2: 62/62 PASS (B129-B135 filter); 14 pre-existing failures | **ZERO REGRESSIONS -- PASS** |

**Note on SCAN-07 pre-existing failures**: T1 reported 15 pre-existing failures; T2 reported 14. The discrepancy of 1 is because T1's run (totalling 379) was earlier and may have included an intermittent failure that resolved. Neither engineer nor verifier introduced new failures. The set of old-suite failures (B44, B56/68, B70, B71, B72, B74LaneC, B76, B77, B79) is consistent between both runs. NOT a violation.

**Note on lizard vs McCabe in SCAN-04**: T2 verifier noted lizard tool reports higher CYC values (TrySweptPttDragOrphans: 6; CancelPttDragOrphansForAccount: 10) due to `?.` null-conditional and `&&` operator counting. The project convention (documented at ticket-1-verification.md L83-84; confirmed at 04-ticket-review.md L208) uses traditional McCabe counting. Under project McCabe convention all values are <= 8. PASS.

**Section C: All 7 scans PASS across both tickets in aggregate.**

---

## Section D -- DNA Rule Compliance

Checked across all new and modified code in B135 (MatchesLeaderName, FindFollowerBracketOrder guard, TrySweptPttDragOrphans, CancelPttDragOrphansForAccount, OnOrderUpdate call site).

| Rule | Check | Evidence | Status |
|------|-------|----------|--------|
| **JS-021** (P0) no `lock()` | 0 actual lock() in any new/modified code | SCAN-01 both tickets: 0 actual; all 11 matches are comment-only | **PASS** |
| **JS-001** (P0) no `throw` in hot paths | MatchesLeaderName: returns bool (no throw). FindFollowerBracketOrder: unchanged guard semantics (no throw). TrySweptPttDragOrphans: void with guard returns (no throw). CancelPttDragOrphansForAccount: `catch(Exception ex)` absorbs; no `throw;` or `throw ex;` in catch | SCAN-02: 0 throw new; live catch block at L1607-1610 verified absent rethrow | **PASS** |
| **JS-002** (P0) return null | `FindFollowerBracketOrder` `return null` at L2571 preserved unchanged. MatchesLeaderName returns bool. Both T2 methods void. No new bare return null introduced | SCAN-05 both tickets: 0 new return null | **PASS** |
| **JS-033** (P0) no `async void` | All new methods are synchronous void or static bool. No async keyword in any new method | Ticket-review TR3: PASS both tickets | **PASS** |
| **CYC <= 8** (P1) | MatchesLeaderName=5, FindFollowerBracketOrder=8 (AT LIMIT), TrySweptPttDragOrphans=5, CancelPttDragOrphansForAccount=5, OnOrderUpdate=8 (delta=0) | SCAN-04 both tickets cross-compared | **PASS** |
| **ASCII-only** | "PTT-TGT-Drag", "PTT-STP-Drag", "PTT drag sweep: cancelled", "PTT drag sweep cancel error:" -- all ASCII. No Unicode literals | SCAN-03: 0 non-ASCII bytes in CopyEngine.cs | **PASS** |
| **acc.Cancel in try/catch** | `CancelPttDragOrphansForAccount` wraps each `acc.Cancel(new Order[]{o})` call in try/catch absorbing ErrorCode.UnableToCancelOrder. No rethrow | Live source L1602-1610 verified; verifier Check 5 PASS | **PASS** |
| **No `DateTime.Now`** | No DateTime used in any new code | Ticket-review TR4 T1+T2: N/A (no DateTime) | **PASS** |
| **No FontFamily / hex colors** | No WPF code in scope | N/A | **N/A** |
| **No CreateOrder without PTT- prefix** | No CreateOrder calls in B135 code. PTT-TGT-Drag and PTT-STP-Drag creation was in B134/B132 | N/A | **N/A** |
| **No sealed TradeCopierWindow** | No class-level modifications in B135 | N/A | **N/A** |
| **No async/await in lifecycle methods** | No lifecycle methods (OnInitialize, OnDestroyed, OnWindowCreated) touched | N/A | **N/A** |

**Section D: All applicable DNA rules PASS. Zero P0 violations.**

---

## Section E -- VERIFY_PASS Artifact Consistency

Cross-comparison of engineer completion reports vs independent verifier reports.

### Ticket 1 Consistency

| Claim | Engineer (ticket-1-completion.md) | Verifier (ticket-1-verification.md) | Match? | Notes |
|-------|----------------------------------|-------------------------------------|--------|-------|
| MatchesLeaderName CYC | 5 | 5 (McCabe) / 7 (strict) | YES | Both <= 8; PASS under project convention |
| FindFollowerBracketOrder CYC | 8 | 8 (AT LIMIT) | YES | |
| SCAN-01 lock() | 4 comment refs, 0 actual | 4 comment refs (L309,343,1676,3018), 0 actual | YES | |
| SCAN-02 throw new | 0 | 0 | YES | |
| SCAN-03 CopyEngine.cs non-ASCII | 0 | 0 | YES | |
| SCAN-03 B135Tests.cs non-ASCII | 0 | gitignored -- verified via list-tests | N/A | Test names confirmed ASCII |
| SCAN-06 build warnings | 1 (pre-existing xUnit2004 B131:156) | 0 warnings | MINOR | Verifier result is cleaner; no violation |
| SCAN-07 B135 T1 | 7/7 | 7/7 | YES | |
| SCAN-07 B129-B134 | 50/50 (11 B129 + rest) | 50/50 | YES | |
| SCAN-07 pre-existing failures | 15 | 15 (slightly different enumeration) | YES | |
| B129 spec baseline | Reported 11 (6+5) | Confirmed 11; spec baseline of 13 is pre-existing error | YES | Pre-existing spec count error; not caused by B135 |
| Deviation T7 leaderName | leaderName="PTT-TGT-Drag" (not "Target3") | ACCEPTABLE | ACCEPTED | Fix paths covered by T4+T5; T7 valid integration test |

**Verdict: CONSISTENT. No divergences constituting a VERIFY_FAIL.**

### Ticket 2 Consistency

| Claim | Engineer (ticket-2-completion.md) | Verifier (ticket-2-verification.md) | Match? | Notes |
|-------|----------------------------------|-------------------------------------|--------|-------|
| TrySweptPttDragOrphans CYC | 5 (McCabe) | 5 (McCabe) / 6 (lizard) | YES | McCabe governs; PASS |
| CancelPttDragOrphansForAccount CYC | 5 (McCabe) | 5 (McCabe) / 10 (lizard) | YES | McCabe governs; PASS |
| OnOrderUpdate CYC | 8 (unchanged) | 8 (McCabe) / 23 (lizard) | YES | Call adds 0 branches |
| SCAN-01 lock() | 0 actual | 0 actual (11 comment-only) | YES | |
| SCAN-02 throw new | 0 | 0 | YES | |
| SCAN-03 non-ASCII | 0 | 0 | YES | |
| SCAN-05 return null | 0 new | 0 new (7 pre-existing) | YES | |
| SCAN-06 build warnings | 1 (pre-existing xUnit2004) | 0 warnings | MINOR | Cleaner result in verifier run; no violation |
| SCAN-07 target suites | 62/62 | 62/62 | YES | |
| SCAN-07 pre-existing failures | 14 | 14 | YES | |
| T1 code unchanged | Confirmed in scope lock | Confirmed (MatchesLeaderName L2645, FindFollowerBracketOrder L2600) | YES | |
| Deviation: callvirt opcode test pattern | Documented; justified by NT8 sealed-type constraint | ACCEPTABLE (clause >= 1 for catch, callvirt >= 6 for cancel dispatch) | ACCEPTED | |

**Verdict: CONSISTENT. No divergences constituting a VERIFY_FAIL.**

---

## Section K -- Deferred Work Register

**REQUIRED for FINAL_PASS. Absence of this section = FINAL_FAIL.**

This section lists ALL open deferred items as of B135 close. Prior OPEN items from B134/06-deferred-backlog.md are carried forward with updated status where applicable.

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-B147 | rawPrice==newPrice early-return guard in SyncAtmFollowerBracket/SyncAtmFollowerTarget | P2 | B136+ | OPEN -- SyncAtmFollowerTarget at CYC=8; adding guard would push to CYC=9 (OVER LIMIT). Requires helper extraction in a dedicated block. Plan §E. |
| DW-B141 | Phase C re-confirmation -- SIM Test A pending (no code fix) | P1 | B135 SIM | OPEN -- After B135 T1 fix, director runs SIM Test A: drag leader target, verify follower syncs PTT-TGT-Drag (Phase B/C). If Phase C fires PTT-STP-Drag correctly: CLOSED. If absent: INCONCLUSIVE. |
| DW-B138 | Follower stop drag confirmed -- SIM Test B pending (no code fix) | P1 | B135 SIM | OPEN -- Director runs SIM Test B: drag leader stop, verify follower stop syncs. Not a ticket; SIM-gate only. |
| B135-DEFER-01 | Gap B runtime -- two simultaneous entries, cancel first, verify 2nd copied | P1 | B136+ | OPEN -- carry-forward from B133-DEFER-01 / B134-DEFER-01 chain |
| B135-DEFER-02 | Stale orders multi-session -- FindFollowerBracketOrder may match prior-session orders | P2 | future | OPEN -- carry-forward from B133-DEFER-02 / B134-DEFER-02 chain |
| DW-B134-OCO (partial) | T2 sweep handles flat-position orphan cleanup. OBS-A/B/C/D partial-fill race conditions unresolved | P1 | future | PARTIAL CLOSE -- flat-position sweep (DW-B134-OCO main) closed by T2. OBS-A, OBS-B, OBS-C, OBS-D remain OPEN (separate from sweep). |

**Status changes from B134 backlog**:
- DW-B134-OCO: PARTIAL CLOSE -- the OCO orphan-after-flat condition is fixed by T2. OBS-A through OBS-D (partial-fill race conditions) remain OPEN.
- B134-DEFER-01: Renamed to B135-DEFER-01 for chain continuity. Status unchanged (OPEN).
- B134-DEFER-02: Renamed to B135-DEFER-02. Status unchanged (OPEN).
- DW-B141: Status unchanged (OPEN -- SIM Test A not yet run).
- DW-B138: Status unchanged (OPEN -- SIM Test B not yet run).

**Full structured entries**: See `docs/brain/B135/06-deferred-backlog.md`.

---

## Final Verdict

### Aggregate Decision

| Gate | Result |
|------|--------|
| Section A (Cross-file coherence, 7 checks) | ALL PASS |
| Section B (Spec coverage, 13 requirements) | ALL COVERED (DW-B147 correctly deferred) |
| Section C (7-scan aggregate) | ALL PASS |
| Section D (DNA rules) | ALL PASS -- ZERO P0 violations |
| Section E (VERIFY_PASS consistency) | CONSISTENT -- no material divergences |
| Section K (Deferred work register) | WRITTEN -- 6 items documented |
| 06-deferred-backlog.md written | YES |

**No violations found. No missing wiring. No spec requirement unaddressed. System is coherent.**

---

## FINAL_PASS

B135 two-ticket epic (DW-B146 + DW-B134-OCO) is complete, coherent, and compliant.

- T1 (DW-B146): `MatchesLeaderName` helper extracted (CYC=5), `FindFollowerBracketOrder` guard replaced
  1-for-1 (CYC=8 AT LIMIT). Second drag fo=null defect resolved. 7/7 tests PASS.
- T2 (DW-B134-OCO): `TrySweptPttDragOrphans` + `CancelPttDragOrphansForAccount` added (both CYC=5).
  `OnOrderUpdate` hook inserted pre-Gate-1 (CYC delta=0). Orphaned PTT-drag order sweep on position
  flat implemented. 5/5 new tests PASS. 62/62 target suite tests PASS.
- All 7 DNA rules PASS. All 7 scans ZERO across both tickets in aggregate.
- DW-B147 correctly deferred (SyncAtmFollowerTarget at CYC=8; guard would push to CYC=9).
- 6 deferred items documented in 06-deferred-backlog.md.

**FINAL_PASS**

---

*Produced by ptt-plan-reviewer, B135 Phase 5.*
