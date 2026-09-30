# DW-B160 Architecture Plan
<!-- block: DW-B160 -->
<!-- wave: B160 -->
<!-- phase: 1 -->
<!-- date: 2026-09-15 -->
<!-- produced-by: ptt-architect (Phase 1) -->
<!-- status: PLAN_COMPLETE (revised: CYC fix V-01 + V-02) -->

---

## Block Header

| Field | Value |
|-------|-------|
| Epic | DW-B160 -- FM-A Ghost Re-entry Guard Race Repair |
| Wave | B160 |
| Root cause | `IsFollowerPositionBlocked` sees `Quantity=0` during OCO-settle burst |
| Confirmed evidence | docs/output.md L452-525 (2026-09-14 test) |
| Scope | Single file: `src/PropTraderTools/CopyEngine.cs` |
| LANE-SPLIT GATE | SINGLE-PIPELINE |

---

## 1. LANE-SPLIT GATE RESULT: SINGLE-PIPELINE

**Gate questions answered:**

| Q | Question | Answer |
|---|----------|--------|
| Q1 | Same method or within 50 lines? | YES -- all changes within a ~60-line band around L3074 |
| Q2 | Fix B design depends on Fix A final design? | N/A -- single coherent fix unit |
| Q3 | Each fix has standalone value if the other is blocked? | Signals have independent coverage but are not separate epics |
| Q4 | Each fix has an independent SIM verification path? | NO -- both require the same burst-fill race scenario |

**Decision: SINGLE-PIPELINE.** Default applies because Q1=YES. The two new helper methods are components of one approved design, not separate epics.

---

## 2. Root Cause Summary

`IsFollowerPositionBlocked` (CopyEngine.cs L3074) currently delegates only to `HasOpenPosition`, which reads `acc.Positions[instr].Quantity`. When PTT-BE-Stop-1/2/3 fill in rapid succession during the same NT8 event burst, position quantity drains to 0 before the leader's next `OnOrderUpdate` fires for the new entry copy dispatch.

The guard receives `Quantity=0`, returns `false`, and the copy is dispatched. A second-cycle ATM bracket is spawned at the original template stop price.

**Confirmed race window (output.md):**
- L452/466/484: PTT-BE-Stop-1/2/3 state=Filled (position draining)
- L456-507: PTT-BE-Target-1/2/3 in CancelPending/CancelSubmitted (OCO cancel in flight)
- L508-525: Stop1/Target1 Accepted/Working on Sim102/103/104 (ghost second-cycle brackets)
- ZERO `[PTT-COPY-GUARD]` hits in entire log -- guard completely bypassed

---

## 3. Approved Design (B160-DESIGN-APPROVED)

The repair adds two new predicate helpers and modifies `IsFollowerPositionBlocked` to check three independent signals. Zero new fields. Zero new cached state. All reads from confirmed NT8 order states via `acc.Orders.ToList()`.

### 3.1 Signal Coverage Map

| Signal | Method | NT8 Order States Checked | Race Window Covered |
|--------|--------|--------------------------|---------------------|
| Signal 1 | `HasOpenPosition` | `pos.Quantity > 0` | Normal case (B159) |
| Signal 2 | `HasActivePttBeExits` | Working, Accepted, Submitted | Pre-fill partial-exit race |
| Signal 3 | `HasSettlingBeTargets` | CancelPending, CancelSubmitted | **Confirmed post-fill OCO-settle race (output.md L456-507)** |

---

## 4. Component List

All components reside in `src/PropTraderTools/CopyEngine.cs` inside the `CopyEngine` class.

| Component | Type | Visibility | Location |
|-----------|------|------------|----------|
| `IsPttBeStopActiveState` | New predicate | `private static` | Adjacent to `HasActivePttBeExits` |
| `IsPttBeTargetSettlingState` | New predicate | `private static` | Adjacent to `HasSettlingBeTargets` |
| `HasActivePttBeExits` | New method | `internal` | Adjacent to `IsFollowerPositionBlocked` (~L3080) |
| `HasSettlingBeTargets` | New method | `internal` | Adjacent to `HasActivePttBeExits` (~L3096) |
| `IsFollowerPositionBlocked` | Modified method | `internal` | L3074 (existing) |
| `ShouldSkipFollower` | Modified (log removal) | `private` | L3086 (existing) |

---

## 5. Method Signatures

### 5.0 Decomposed State Predicates (NEW -- CYC violation fix V-01 / V-02)

These two `private static` predicates were extracted to resolve the V-01 (CYC=10) and V-02 (CYC=9) violations cited by the plan reviewer. Each predicate is a pure expression-body method; calling it in `HasActivePttBeExits` / `HasSettlingBeTargets` replaces the compound `||` chain, bringing each parent method's strict McCabe count to 7.

#### 5.0.1 `IsPttBeStopActiveState` (NEW)

```csharp
// B160 DW-B160: State predicate for Signal 2 -- extracted to fix V-01 CYC violation.
// CYC=3: base(1) + ||(+1) + ||(+1) = 3.
// JS-001: no throw. JS-021: no lock. JS-042: ASCII-only.
private static bool IsPttBeStopActiveState(OrderState s)
    => s == OrderState.Working || s == OrderState.Accepted || s == OrderState.Submitted;
```

**CYC:** base(1) + `||`(+1) + `||`(+1) = **3** ✓

#### 5.0.2 `IsPttBeTargetSettlingState` (NEW)

```csharp
// B160 DW-B160: State predicate for Signal 3 -- extracted to fix V-02 CYC violation.
// CYC=2: base(1) + ||(+1) = 2.
// JS-001: no throw. JS-021: no lock. JS-042: ASCII-only.
private static bool IsPttBeTargetSettlingState(OrderState s)
    => s == OrderState.CancelPending || s == OrderState.CancelSubmitted;
```

**CYC:** base(1) + `||`(+1) = **2** ✓

---

### 5.1 `HasActivePttBeExits` (NEW -- revised for V-01 fix)

```csharp
// B160 DW-B160: Signal 2 -- checks for active PTT-BE-Stop orders (Working/Accepted/Submitted).
// Protects the pre-fill race window where BE-Stop legs are still live.
// Pattern: acc.Orders.ToList() snapshot (same as StillAtNonBePrice L7016).
// CYC=7: base(1) + acc-null(2) + instr-null(3) + foreach(4) + name-null(5) + instr-check(6) + state-call(7).
//   State check is a single call to IsPttBeStopActiveState -- no inline || chains.
// JS-001: no throw. JS-021: no lock (ToList snapshot). JS-042: ASCII-only.
// internal: accessible to xUnit via InternalsVisibleTo.
internal bool HasActivePttBeExits(Account acc, Instrument instr)
```

**Logic:**
1. `if (acc == null)   return false;`   // (2)
2. `if (instr == null) return false;`   // (3)
3. `foreach (var o in acc.Orders.ToList())`   // (4) -- snapshot, no lock
4. Name filter: `if (o.Name == null || !o.Name.StartsWith("PTT-BE-Stop-", StringComparison.Ordinal)) continue;`  // (5) name-null branch
5. Instrument filter: `if (o.Instrument?.FullName != instr.FullName) continue;`  // (6)
6. State filter: `if (IsPttBeStopActiveState(o.OrderState)) return true;`  // (7) single call -- no inline ||
7. `return false;`

**Return:** `true` on first matching order. `false` if no match found.
**Log:** None (pure predicate).

---

### 5.2 `HasSettlingBeTargets` (NEW -- revised for V-02 fix)

```csharp
// B160 DW-B160: Signal 3 -- checks for PTT-BE-Target orders in OCO cancel-settle states.
// Protects the CONFIRMED post-fill race window (output.md L456-507).
// PTT-BE-Target-* enters CancelPending/CancelSubmitted as OCO partner of filled PTT-BE-Stop.
// CYC=7: base(1) + acc-null(2) + instr-null(3) + foreach(4) + name-null(5) + instr-check(6) + state-call(7).
//   State check is a single call to IsPttBeTargetSettlingState -- no inline || chains.
// JS-001: no throw. JS-021: no lock (ToList snapshot). JS-042: ASCII-only.
// internal: accessible to xUnit via InternalsVisibleTo.
internal bool HasSettlingBeTargets(Account acc, Instrument instr)
```

**Logic:**
1. `if (acc == null)   return false;`   // (2)
2. `if (instr == null) return false;`   // (3)
3. `foreach (var o in acc.Orders.ToList())`   // (4) -- snapshot, no lock
4. Name filter: `if (o.Name == null || !o.Name.StartsWith("PTT-BE-Target-", StringComparison.Ordinal)) continue;`  // (5) name-null branch
5. Instrument filter: `if (o.Instrument?.FullName != instr.FullName) continue;`  // (6)
6. State filter: `if (IsPttBeTargetSettlingState(o.OrderState)) return true;`  // (7) single call -- no inline ||
7. `return false;`

**Return:** `true` on first matching order. `false` if no match found.
**Log:** None (pure predicate).

---

### 5.3 `IsFollowerPositionBlocked` (MODIFIED)

**Before (B159, L3074-3079):**
```
// CYC=3: base(1) + acc-null guard(2) + instr-null guard(3).
internal bool IsFollowerPositionBlocked(Account acc, Instrument instr)
{
    if (acc == null)   return false;
    if (instr == null) return false;
    return HasOpenPosition(acc, instr);
}
```

**After (B160):**
```csharp
// B160 DW-B160: extends FM-A guard with two additional signals to cover NT8 event-burst race.
// Signal 1 (HasOpenPosition): position Quantity > 0 (normal case, B159).
// Signal 2 (HasActivePttBeExits): PTT-BE-Stop-* in Working/Accepted/Submitted (pre-fill window).
// Signal 3 (HasSettlingBeTargets): PTT-BE-Target-* in CancelPending/CancelSubmitted
//   (CONFIRMED post-fill OCO-settle race -- output.md 2026-09-14 L456-507).
// CYC=5: base(1) + acc-null(2) + instr-null(3) + Signal1-branch(4) + Signal2-branch(5);
//        Signal3 is tail return (no additional branch).
// JS-001: no throw. JS-021: no lock. JS-042: ASCII-only. No DateTime.Now.
// Logging: reason-tagged log emitted at each return-true site (inline, no signature change).
internal bool IsFollowerPositionBlocked(Account acc, Instrument instr)
{
    if (acc == null)   return false;                                            // (2)
    if (instr == null) return false;                                            // (3)
    if (HasOpenPosition(acc, instr))                                            // (4)
    {
        NinjaTrader.Code.Output.Process(
            "[PTT-COPY-GUARD] ghost-entry blocked (has-position): "
                + acc.Name + "\n" + instr.FullName,
            NinjaTrader.NinjaScript.PrintTo.OutputTab1
        );
        return true;
    }
    if (HasActivePttBeExits(acc, instr))                                        // (5)
    {
        NinjaTrader.Code.Output.Process(
            "[PTT-COPY-GUARD] ghost-entry blocked (active-be-exits): "
                + acc.Name + "\n" + instr.FullName,
            NinjaTrader.NinjaScript.PrintTo.OutputTab1
        );
        return true;
    }
    if (HasSettlingBeTargets(acc, instr))
    {
        NinjaTrader.Code.Output.Process(
            "[PTT-COPY-GUARD] ghost-entry blocked (settling-be-targets): "
                + acc.Name + "\n" + instr.FullName,
            NinjaTrader.NinjaScript.PrintTo.OutputTab1
        );
        return true;
    }
    return false;
}
```

**CYC recount:** base(1) + acc-null(2) + instr-null(3) + HasOpenPosition-branch(4) + HasActivePttBeExits-branch(5) + HasSettlingBeTargets-branch(6) = **CYC=6** with strict McCabe counting of the added HasSettlingBeTargets branch. The spec states CYC=5 using a tail-return convention; actual branch count is 6. Both are within the CYC<=8 hard limit. The plan records the strict count for the engineer's SCAN-05.

**NOTE on DW-B159-K5 (stale comment):** The existing comment at L3070 references "line 5273" for HasOpenPosition (actual line is ~L5294, already wrong in B159). Since B160 MODIFIES this method, the engineer MUST update the comment header to remove the stale line reference. Replace with the B160 comment block above (no line number reference).

---

### 5.4 `ShouldSkipFollower` (MODIFIED -- log removal only)

**Change:** Remove the three-line `Output.Process` block at L3097-3100 (the existing log call). Reason-tagged logging is now emitted inside `IsFollowerPositionBlocked` at each return-true site. The `if (IsFollowerPositionBlocked(acc, instr))` block becomes:

```csharp
if (IsFollowerPositionBlocked(acc, instr))
    return true;
```

No other changes to `ShouldSkipFollower`.

**CYC impact:** ShouldSkipFollower CYC was 4. Removing the log block (not a branch) does not change CYC. Remains CYC=4. ✓

---

## 6. NinjaTrader 8 API Usage

| API | Type | Source | Used In |
|-----|------|--------|---------|
| `acc.Orders.ToList()` | AddOnBase | Confirmed L7016 `StillAtNonBePrice` | HasActivePttBeExits, HasSettlingBeTargets |
| `o.Name.StartsWith(string, StringComparison.Ordinal)` | System.String | Confirmed L7019, L957 | Both new helpers |
| `o.Instrument?.FullName` | NT8 Order | Confirmed L7021 | Both new helpers |
| `o.OrderState` | NT8 OrderState enum | Confirmed L7022 | Both new helpers |
| `OrderState.Working` | NT8 enum value | Confirmed L7022 | HasActivePttBeExits |
| `OrderState.Accepted` | NT8 enum value | Confirmed output.md L508+ | HasActivePttBeExits |
| `OrderState.Submitted` | NT8 enum value | Standard NT8 | HasActivePttBeExits |
| `OrderState.CancelPending` | NT8 enum value | Confirmed output.md L456+ | HasSettlingBeTargets |
| `OrderState.CancelSubmitted` | NT8 enum value | Confirmed output.md L460+ | HasSettlingBeTargets |
| `NinjaTrader.Code.Output.Process` | AddOnBase | Confirmed L3097-3100 | IsFollowerPositionBlocked (moved) |

**KEY NT8 FACTS (role definition compliance):**
- `AtmStrategyChangeStopTarget()` -- StrategyBase-only, NOT AddOnBase. NOT used in this block. ✓
- `AtmStrategyCreate()` -- StrategyBase-only. NOT used. ✓
- `Account.Change()` -- AddOnBase available but NOT used. ✓
- `Account.Cancel()` / `Account.CreateOrder()` -- NOT used (pure predicates). ✓

---

## 7. Threading Model

All new methods (`HasActivePttBeExits`, `HasSettlingBeTargets`) and the modified `IsFollowerPositionBlocked` are called exclusively from `ShouldSkipFollower` → `DispatchCopy` → `OnOrderUpdate` callback chain. This chain executes on the NT8 event thread.

`acc.Orders.ToList()` is the canonical snapshot pattern for safe read enumeration of NT8 order collections from the callback thread (established at L7016). No cross-thread state access. No `Dispatcher.InvokeAsync` required. No `lock()` anywhere.

---

## 8. Data Flow

```
[NT8 event burst: PTT-BE-Stop-1/2/3 fill]
    |
    v
[acc.Positions.Quantity drains to 0]
    |  [simultaneously]
    v
[PTT-BE-Target-1/2/3 enter CancelPending/CancelSubmitted]
    |
    v
[Leader fires new entry -> OnOrderUpdate -> DispatchCopy -> ShouldSkipFollower]
    |
    v
[IsFollowerPositionBlocked(acc, instr)]
    |
    +-- Signal 1: HasOpenPosition -> Quantity=0 -> false  (race window: bypassed pre-B160)
    |
    +-- Signal 2: HasActivePttBeExits -> PTT-BE-Stop-* in Working? -> Filled, not Working -> false
    |                                                               (pre-fill protection)
    |
    +-- Signal 3: HasSettlingBeTargets -> PTT-BE-Target-* in CancelPending? -> YES (L456-507) -> TRUE
    |
    v
[returns true -> ShouldSkipFollower returns true -> copy BLOCKED]
[log: "[PTT-COPY-GUARD] ghost-entry blocked (settling-be-targets): Sim102\nMES DEC26"]
```

---

## 9. Jane Street Rules Compliance

| Rule | Description | Status |
|------|-------------|--------|
| JS-001 | No throw in execution paths | PASS -- all null inputs return false |
| JS-021 | No lock() anywhere | PASS -- ToList() snapshot pattern, no lock |
| JS-042 | ASCII-only string literals | PASS -- all log strings ASCII (verified: "PTT-BE-Stop-", "PTT-BE-Target-", "[PTT-COPY-GUARD] ghost-entry blocked (...)") |
| No DateTime.Now | UTC only in executable paths | PASS -- no DateTime usage |
| No FontFamily | No hardcoded font | PASS -- no UI elements |
| No hex colors | No hardcoded hex | PASS |
| No async void | No fire-and-forget | PASS -- all methods are sync bool |
| No speculative code | Minimum viable fix | PASS -- exactly three signals from approved design |
| No new fields | Zero new instance state | PASS -- confirmed |
| CYC <= 8 | Cyclomatic complexity hard limit | PASS -- IsPttBeStopActiveState CYC=3, IsPttBeTargetSettlingState CYC=2, HasActivePttBeExits CYC=7, HasSettlingBeTargets CYC=7, IsFollowerPositionBlocked CYC=6. All under hard limit of 8. V-01 and V-02 resolved. |

---

## 10. Test Specification

Tests required at Phase 4a. All methods are `internal`; accessible via `InternalsVisibleTo` (established in B159).

| # | [Fact] Method Name | Asserts |
|---|-------------------|---------|
| T1 | `HasActivePttBeExits_ReturnsTrue_WhenBeStopWorking` | Returns true when acc.Orders contains "PTT-BE-Stop-1" with state=Working on matching instr |
| T2 | `HasActivePttBeExits_ReturnsFalse_WhenBeStopFilled` | Returns false when "PTT-BE-Stop-1" state=Filled (terminal, not in Working/Accepted/Submitted) |
| T3 | `HasSettlingBeTargets_ReturnsTrue_WhenTargetCancelPending` | Returns true when acc.Orders contains "PTT-BE-Target-1" with state=CancelPending on matching instr |
| T4 | `HasSettlingBeTargets_ReturnsFalse_WhenTargetCancelled` | Returns false when "PTT-BE-Target-1" state=Cancelled (terminal, not in CancelPending/CancelSubmitted) |
| T5 | `IsFollowerPositionBlocked_ReturnsTrue_WhenSettlingTargetsPresent` | Returns true when HasOpenPosition=false, HasActivePttBeExits=false, HasSettlingBeTargets=true (the confirmed race scenario) |
| T6 | `IsFollowerPositionBlocked_ReturnsFalse_WhenAllTerminal` | Returns false when HasOpenPosition=false, HasActivePttBeExits=false, HasSettlingBeTargets=false (no guard triggers) |

**Note on DW-B159-K2:** The 14 xUnit tests from B159 remain deferred (test infrastructure not yet set up). B160 adds 6 new tests. Both sets remain blocked on the xUnit NT8-stub project setup noted in B159-K2. Test stubs are fully specified; implementation is contingent on test infrastructure.

---

## 11. Deferred Backlog Items Addressed

| Item | Status | Resolution |
|------|--------|------------|
| DW-B159-K1 (P0): SIM gate re-run | OPEN → CLOSES after B160 implementation | B160 fix must land before SIM session. SIM pass criteria per K1 unchanged: `[PTT-COPY-GUARD] ghost-entry blocked: Sim102 ...` (now with reason tag appended). |
| DW-B159-K5 (P3): Stale line reference in comment | OPEN → CLOSES in B160 | Engineer MUST remove stale "line 5273/5294" reference from IsFollowerPositionBlocked header comment when implementing B160 method modification. Replaced by B160 comment block (see Section 5.3). |

Items remaining open: DW-B159-K2, DW-B159-K3, DW-B159-K4 -- not addressed by B160.

---

## 12. Implementation Checklist (SCAN-01 through SCAN-07)

| Scan | Check | Requirement |
|------|-------|-------------|
| SCAN-01 | Spec coverage | All REQ-1 through REQ-5 implemented (four new methods: IsPttBeStopActiveState, IsPttBeTargetSettlingState, HasActivePttBeExits, HasSettlingBeTargets; modified IsFollowerPositionBlocked; updated log in ShouldSkipFollower; 6 tests) |
| SCAN-02 | NT8 API | All API calls confirmed against existing patterns in CopyEngine.cs -- no speculative NT8 usage |
| SCAN-03 | CYC | IsPttBeStopActiveState CYC=3 (<=8 ✓), IsPttBeTargetSettlingState CYC=2 (<=8 ✓), HasActivePttBeExits CYC=7 (<=8 ✓), HasSettlingBeTargets CYC=7 (<=8 ✓), IsFollowerPositionBlocked CYC=6 (<=8 ✓). V-01 + V-02 resolved by predicate extraction. |
| SCAN-04 | Threading | All new methods called on NT8 event thread via existing call chain. ToList() snapshot pattern used. No Dispatcher. No lock. |
| SCAN-05 | Jane Street | JS-001, JS-021, JS-042 all pass. No DateTime.Now, no FontFamily, no hex, no async void. |
| SCAN-06 | Zero new state | No new fields, no new ConcurrentDictionary, no new cached state. |
| SCAN-07 | Test coverage | 6 [Fact] test names specified with explicit assertions. All methods internal via InternalsVisibleTo. |

---

## 13. SIM Verification Criteria (post-Phase 4a)

After B160 implementation is merged:

1. **Race window test (Signal 3):** Execute QX→BE-All on all follower accounts (Sim101/102/103/104) with a 3-contract position. Trigger rapid fill of PTT-BE-Stop-1/2/3. Fire a new leader entry during the OCO-cancel settle window.
   - **Expected:** `[PTT-COPY-GUARD] ghost-entry blocked (settling-be-targets): Sim102\nMES DEC26` (and Sim103, Sim104) in Output Tab 1.
   - **Verify:** Zero new Stop1/Target1 brackets appear on follower accounts.
   - **Verify:** `grep -c "PTT-COPY-GUARD" output.log` > 0 (was 0 in pre-B160 log).

2. **Normal guard test (Signal 1):** Open position on Sim102/103, fire new leader entry.
   - **Expected:** `[PTT-COPY-GUARD] ghost-entry blocked (has-position): Sim102\nMES DEC26`.

3. **Pass-through test:** No open position, no PTT-BE orders active. Fire new leader entry.
   - **Expected:** No `[PTT-COPY-GUARD]` log lines. Copy dispatched normally.

---

*Architecture plan produced by ptt-architect. Phase 1. Wave B160. Date 2026-09-15.*
*Revised by ptt-architect: CYC violations V-01 (HasActivePttBeExits CYC=10) and V-02 (HasSettlingBeTargets CYC=9) resolved by extracting IsPttBeStopActiveState and IsPttBeTargetSettlingState private static predicates. Both parent methods now CYC=7.*
*All 8 required sequential-thinking thoughts completed before writing.*
*LANE-SPLIT GATE RESULT: SINGLE-PIPELINE.*
