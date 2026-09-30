# DW-B160 Plan Review (Phase 2 Re-Run)
<!-- block: DW-B160 -->
<!-- phase: 2 -->
<!-- date: 2026-09-15 -->
<!-- produced-by: ptt-plan-reviewer (Phase 2 re-run) -->
<!-- verdict: REVIEW_PASS -->

---

## Verdict

**REVIEW_PASS**

Zero violations found. V-01 and V-02 resolutions are correct. The revised plan
satisfies all Jane Street rules, NT8 API constraints, CYC limits, spec coverage,
and structural requirements.

---

## 1. LANE-SPLIT GATE Compliance

| Check | Result |
|-------|--------|
| Section "LANE-SPLIT GATE RESULT" present | PASS -- Section 1, plan L24 |
| Gate questions Q1-Q4 answered in table | PASS |
| Result stated explicitly | PASS -- "SINGLE-PIPELINE" |

---

## 2. CYC Compliance -- All 6 Methods

Re-verified using strict Manual McCabe per JS-013 (RULES_CATALOG.md Section 1).
Rule: +1 for each `if`, `else if`, `foreach`, `catch`, `?:`, `??`, `&&`, `||`.

### 2.1 `IsPttBeStopActiveState` (NEW)

```
Expression body: s == OrderState.Working || s == OrderState.Accepted || s == OrderState.Submitted
  base(1) + ||(+1) + ||(+1) = CYC=3
```

**CYC=3 <= 8. PASS.** (Plan claims 3. Correct.)

### 2.2 `IsPttBeTargetSettlingState` (NEW)

```
Expression body: s == OrderState.CancelPending || s == OrderState.CancelSubmitted
  base(1) + ||(+1) = CYC=2
```

**CYC=2 <= 8. PASS.** (Plan claims 2. Correct.)

### 2.3 `HasActivePttBeExits` (NEW)

```
base(1)
+ if (acc == null)                                          +1  = (2)
+ if (instr == null)                                        +1  = (3)
+ foreach (var o in acc.Orders.ToList())                    +1  = (4)
+ if (o.Name == null [if-clause]                            +1  = (5)
+    || !o.Name.StartsWith(...) [|| inside same if]         +1  = (6)
+ if (o.Instrument?.FullName != instr.FullName) continue;  +1  = (7)
+ if (IsPttBeStopActiveState(o.OrderState)) return true;   +1  = (8)
```

**Strict McCabe CYC=8 <= 8. PASS.**

NOTE: The plan comments claim CYC=7 for HasActivePttBeExits, treating the `||`
in `if (o.Name == null || !o.Name.StartsWith(...))` as a single decision node.
Per JS-013, each `||` is independently +1. The true strict count is CYC=8.
CYC=8 is exactly at the hard limit -- NOT a violation. However, the engineer's
SCAN-02 compliance comment MUST record CYC=8 (not 7) in source to match the
authoritative gate value per RULES_CATALOG Section 4.

### 2.4 `HasSettlingBeTargets` (NEW)

```
base(1)
+ if (acc == null)                                          +1  = (2)
+ if (instr == null)                                        +1  = (3)
+ foreach (var o in acc.Orders.ToList())                    +1  = (4)
+ if (o.Name == null [if-clause]                            +1  = (5)
+    || !o.Name.StartsWith(...) [|| inside same if]         +1  = (6)
+ if (o.Instrument?.FullName != instr.FullName) continue;  +1  = (7)
+ if (IsPttBeTargetSettlingState(o.OrderState)) return true; +1 = (8)
```

**Strict McCabe CYC=8 <= 8. PASS.**

Same note as 2.3 above: plan claims CYC=7, strict count is CYC=8. At the
limit, not over. Engineer must record CYC=8 in source compliance comment.

### 2.5 `IsFollowerPositionBlocked` (MODIFIED)

```
base(1)
+ if (acc == null)                        +1 = (2)
+ if (instr == null)                      +1 = (3)
+ if (HasOpenPosition(...))               +1 = (4)
+ if (HasActivePttBeExits(...))           +1 = (5)
+ if (HasSettlingBeTargets(...))          +1 = (6)
```

**Strict McCabe CYC=6 <= 8. PASS.** (Plan self-corrects to 6 in the note at
plan line 227. Correct.)

### 2.6 `ShouldSkipFollower` (MODIFIED -- log removal only)

No branches added or removed. Log block at L3097-3100 is three statements
(not a branch). CYC unchanged at 4. **PASS.**

---

## 3. Root Cause Analysis

| Check | Result |
|-------|--------|
| Race window identified from output.md evidence | PASS -- L452-525 |
| `Quantity=0` during OCO-settle burst explained | PASS |
| Confirmed PTT-BE-Target-* CancelPending/CancelSubmitted from output.md L456-507 | PASS |
| Zero `[PTT-COPY-GUARD]` hits in pre-B160 log cited | PASS |
| Signal 3 correctly identified as the closing guard | PASS |

---

## 4. Jane Street Rules

| Rule | Check | Result |
|------|-------|--------|
| JS-001 | No `throw` in any new/modified method | PASS -- all null inputs return false |
| JS-013 | CYC <= 8 all methods | PASS -- see Section 2 |
| JS-021 | No `lock()`, Monitor, Mutex | PASS -- `acc.Orders.ToList()` snapshot, no lock anywhere |
| JS-023 | No off-thread WPF update | PASS -- pure predicates, no WPF elements |
| ASCII-001 | ASCII-only string literals | PASS -- all log strings ASCII: "[PTT-COPY-GUARD] ghost-entry blocked (has-position): ", "[PTT-COPY-GUARD] ghost-entry blocked (active-be-exits): ", "[PTT-COPY-GUARD] ghost-entry blocked (settling-be-targets): " -- all printable ASCII 0x20-0x7E |
| NT8-006 | No DateTime.Now | PASS -- no DateTime usage |
| NT8-004 | No FontFamily | PASS -- no WPF elements |
| NT8-005 | No hex colors | PASS -- no WPF elements |
| NT8-001 | No async void | PASS -- all methods are synchronous bool or void |
| JS-PUSH-01 | No new shared mutable fields | PASS -- confirmed zero new fields |

---

## 5. NT8 API Compliance

| API | Used In | Status |
|-----|---------|--------|
| `acc.Orders.ToList()` | HasActivePttBeExits, HasSettlingBeTargets | PASS -- confirmed pattern at CopyEngine.cs L7016 |
| `o.Name.StartsWith(string, StringComparison.Ordinal)` | Both new methods | PASS -- confirmed at L7019 |
| `o.Instrument?.FullName` | Both new methods | PASS -- confirmed at L7021 |
| `o.OrderState` / `OrderState` enum | Both new methods | PASS -- confirmed at L7022 |
| `OrderState.Working`, `Accepted`, `Submitted` | HasActivePttBeExits | PASS -- standard NT8 states; Accepted confirmed output.md L508+ |
| `OrderState.CancelPending`, `CancelSubmitted` | HasSettlingBeTargets | PASS -- confirmed output.md L456+, L460+ |
| `NinjaTrader.Code.Output.Process` | IsFollowerPositionBlocked (moved from ShouldSkipFollower) | PASS -- confirmed at L3097-3100 |
| `AtmStrategyCreate` | NOT used | PASS -- correctly noted as StrategyBase-only (NT8-008) |
| `AtmStrategyChangeStopTarget` | NOT used | PASS -- correctly noted as StrategyBase-only (NT8-009) |

All NT8 API calls are AddOnBase-legal and confirmed against existing CopyEngine.cs patterns.

---

## 6. `IsFollowerPositionBlocked` Signal Chain Order

| Position | Signal | Method | Race Window |
|----------|--------|--------|-------------|
| First | Signal 1 | `HasOpenPosition` | Normal case (B159) |
| Second | Signal 2 | `HasActivePttBeExits` | Pre-fill window |
| Third | Signal 3 | `HasSettlingBeTargets` | Confirmed OCO-settle race |

HasOpenPosition checked first per approved design. **PASS.**

---

## 7. `ShouldSkipFollower` Log Change

| Check | Result |
|-------|--------|
| Existing untagged log at L3097-3100 removed | PASS -- plan Section 5.4 |
| Reason-tagged logs moved to `IsFollowerPositionBlocked` return-true sites | PASS |
| Log strings ASCII-only | PASS |
| CYC unchanged (log removal is not a branch) | PASS |

---

## 8. Test Coverage -- 6 Required [Fact] Names

| # | [Fact] Name | Present in Plan |
|---|------------|-----------------|
| T1 | `HasActivePttBeExits_ReturnsTrue_WhenBeStopWorking` | PASS -- Section 10 |
| T2 | `HasActivePttBeExits_ReturnsFalse_WhenBeStopFilled` | PASS -- Section 10 |
| T3 | `HasSettlingBeTargets_ReturnsTrue_WhenTargetCancelPending` | PASS -- Section 10 |
| T4 | `HasSettlingBeTargets_ReturnsFalse_WhenTargetCancelled` | PASS -- Section 10 |
| T5 | `IsFollowerPositionBlocked_ReturnsTrue_WhenSettlingTargetsPresent` | PASS -- Section 10 |
| T6 | `IsFollowerPositionBlocked_ReturnsFalse_WhenAllTerminal` | PASS -- Section 10 |

All 6 required [Fact] names present with explicit assertion descriptions. **PASS.**

---

## 9. SCAN-01 through SCAN-07 Checklist

Plan Section 12 contains all 7 scans:

| Scan | Present | Content |
|------|---------|---------|
| SCAN-01 | PASS | Spec coverage |
| SCAN-02 | PASS | NT8 API patterns confirmed |
| SCAN-03 | PASS | CYC compliance per method |
| SCAN-04 | PASS | Threading / ToList snapshot |
| SCAN-05 | PASS | Jane Street rules |
| SCAN-06 | PASS | Zero new state/fields |
| SCAN-07 | PASS | Test coverage / InternalsVisibleTo |

**PASS.**

---

## 10. Scope -- CopyEngine.cs Only

Plan Section 4 explicitly lists all components in `src/PropTraderTools/CopyEngine.cs`.
No other files proposed for modification. **PASS.**

---

## Spec Coverage Matrix

| Requirement | Addressed | Plan Section |
|-------------|-----------|--------------|
| REQ-1: Three-signal guard in IsFollowerPositionBlocked | YES | Sections 3, 5.3 |
| REQ-2: Signal 2 HasActivePttBeExits (pre-fill race) | YES | Sections 3.1, 5.1 |
| REQ-3: Signal 3 HasSettlingBeTargets (OCO-settle race) | YES | Sections 3.1, 5.2 |
| REQ-4: Zero new fields / no cached state | YES | Sections 4, 9 |
| REQ-5: Reason-tagged log at each guard return-true site | YES | Sections 5.3, 5.4 |
| REQ-6: V-01 fix (HasActivePttBeExits CYC<=8) | YES | Section 5.0.1, 5.1 |
| REQ-7: V-02 fix (HasSettlingBeTargets CYC<=8) | YES | Section 5.0.2, 5.2 |
| REQ-8: 6 test stubs specified | YES | Section 10 |
| REQ-9: Stale comment DW-B159-K5 addressed | YES | Section 5.3 note |
| REQ-10: Deferred items DW-B159-K1 and K5 close criteria stated | YES | Section 11 |

---

## Engineer Instructions (Non-negotiable)

1. **CYC compliance comments**: Record CYC=8 (not 7) in source compliance
   comments for `HasActivePttBeExits` and `HasSettlingBeTargets`. The plan's
   CYC=7 claim is a counting error (the `||` in the name-null branch is +1
   per JS-013). CYC=8 is still within the hard limit. SCAN-02 must record
   the accurate value.

2. **Stale comment**: Per plan Section 5.3, remove the "line 5273/5294"
   reference from the existing `IsFollowerPositionBlocked` header comment
   (CopyEngine.cs L3069-3073) when implementing the B160 modification.

3. **ShouldSkipFollower log**: Remove the three-line `Output.Process` block
   at L3097-3100 exactly. No other changes to `ShouldSkipFollower`.

---

## Prior Violations -- Resolution Status

| Violation ID | Original Violation | Resolution in Revised Plan | Status |
|-------------|-------------------|---------------------------|--------|
| V-01 | `HasActivePttBeExits` CYC=10 (was 10, limit 8) -- JS-013 | Extracted `IsPttBeStopActiveState` private static predicate (CYC=3); `HasActivePttBeExits` reduced to CYC=8 | RESOLVED |
| V-02 | `HasSettlingBeTargets` CYC=9 (was 9, limit 8) -- JS-013 | Extracted `IsPttBeTargetSettlingState` private static predicate (CYC=2); `HasSettlingBeTargets` reduced to CYC=8 | RESOLVED |

---

## Summary

The revised plan correctly resolves both V-01 and V-02 CYC violations by
extracting two pure static predicate methods (`IsPttBeStopActiveState`,
`IsPttBeTargetSettlingState`). All 6 methods are within the CYC<=8 hard
limit under strict Manual McCabe counting. All Jane Street rules, NT8 API
constraints, threading model, test coverage, and scope restrictions are
satisfied. The engineer must record CYC=8 (not 7) in compliance comments
for the two helper methods; this is a documentation correction and not a
plan defect.

**REVIEW_PASS. Proceed to Phase 3 (ticket generation).**

---

*Reviewed by: ptt-plan-reviewer. Phase 2 re-run. Wave B160. Date: 2026-09-15.*
