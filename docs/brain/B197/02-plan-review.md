# B197 Plan Review (Re-Review)
# Gap Restore: TrySetSlateReadyOnDrainFill + TryAbortLeaderFlatDrains + TryCancelStaleGtcExitOnEntry + IsPttDragOrphanCancellable suffix fix

**Reviewer**: ptt-plan-reviewer
**Phase**: 2 (Plan Review — Re-Review after architect revision)
**Epic**: B197
**Plan document**: docs/brain/B197/02-architecture-plan.md
**Prior review**: docs/brain/B197/02-plan-review.md (original, verdict: REVIEW_FAIL)
**Date**: 2026-10-12

---

## VERDICT: REVIEW_PASS

**Prior violations resolved**: 2 / 2 (V-01: JS-056; V-02: JS-066 annotation)
**Prior warnings resolved**: 2 / 3 (W-02: IsStaleGtcExitOrder CYC; W-03: Is... naming)
**Residual warning**: W-01 (line-1041 write direction) — carried forward from prior review, not blocking
**New violations**: 0
**New warnings**: 0

---

## Re-Review: Prior Violation Resolution

### V-01 — JS-056 (One Assert per Test) — RESOLVED

**Prior state**: T1, T2, and T3 source-text [Fact]s each contained 2 `Assert.Contains` calls in a
single test, violating JS-056 (one assert per test).

**Required fix**: Split each multi-assert [Fact] into two separate [Fact]s. Total count rises from 7
to 10.

**Revised plan §6 check** — per-test assert count:

| Test | Ticket | Assert Count | Result |
|------|--------|--------------|--------|
| `TrySetSlateReadyOnDrainFill_SourceContainsFollowerSlateReady` | T1 | 1 (`Assert.Contains("_followerSlateReady["...)`) | PASS |
| `TrySetSlateReadyOnDrainFill_SourceContainsSlateReadyDrainMarker` | T1 | 1 (`Assert.Contains("[SLATE-READY-DRAIN]"...)`) | PASS |
| `TryEvictFollowerBeSlot_SourceContainsDrainAbortLeaderFlat` | T2 | 1 (`Assert.Contains("[DRAIN-ABORT-LEADER-FLAT]"...)`) | PASS |
| `TryEvictFollowerBeSlot_SourceContainsTryAbortLeaderFlatDrains` | T2 | 1 (`Assert.Contains("TryAbortLeaderFlatDrains"...)`) | PASS |
| `TryCancelStaleGtcExitOnEntry_SourceContainsMethodName` | T3 | 1 (`Assert.Contains("TryCancelStaleGtcExitOnEntry"...)`) | PASS |
| `TryCancelStaleGtcExitOnEntry_SourceContainsGtcSweepMarker` | T3 | 1 (`Assert.Contains("[GTC-SWEEP]"...)`) | PASS |
| `IsPttDragOrphanCancellableTestable_SuffixedTgtName_ReturnsTrue` | T4 | 1 (`Assert.True(result)`) | PASS |
| `IsPttDragOrphanCancellableTestable_SuffixedStpName_ReturnsTrue` | T4 | 1 (`Assert.True(result)`) | PASS |
| `IsPttDragOrphanCancellableTestable_UnsuffixedTgtName_StillReturnsTrue` | T4 | 1 (`Assert.True(result)`) | PASS |
| `IsPttDragOrphanCancellableTestable_UnsuffixedStpName_StillReturnsTrue` | T4 | 1 (`Assert.True(result)`) | PASS |

Total: **10 [Fact]s, each with exactly 1 assert.** Distribution: T1×2, T2×2, T3×2, T4×4. Matches
required count precisely.

**V-01 resolution**: CONFIRMED. JS-056 PASS.

---

### V-02 — JS-066 Annotation (TryCancelStaleGtcExitOnEntry CYC ceiling) — RESOLVED

**Prior state**: Plan annotated `TryCancelStaleGtcExitOnEntry` CYC as **7**. Correct count is **8**
(guard3 contains if + OR + AND = 3 branches by McCabe/Lizard convention). Method was silently at the
JS-066 ceiling with no ceiling notation.

**Required fix**: Correct annotation to CYC=8 and document as ceiling method with future-branch
extraction requirement.

**Revised plan §3 T3 check**:

| Location | Prior Value | Revised Value | Correct? |
|----------|-------------|---------------|----------|
| §3 T3 comment (method header) | `CYC = 7` | `// TryCancelStaleGtcExitOnEntry: CYC=8` | YES |
| §3 T3 CYC derivation | `CYC = 7` | `CYC = 8` with all 8 branches enumerated | YES |
| §3 T3 ceiling NOTE | absent | `TryCancelStaleGtcExitOnEntry is a CYC=8 ceiling method. Future branch additions require helper extraction first (same constraint as IsLaneEEligible — DW-B183-03).` | YES |
| §5 T3 JS-066 table row | absent / implied | `TryCancelStaleGtcExitOnEntry CYC = 8 (at ceiling). IsStaleGtcExitOrder CYC = 5.` | YES |
| §7 CYC Impact Summary row | CYC=7 | `**8** | +8 (new) | **CYC=8 CEILING**` | YES |
| §7 Notes | absent | `TryCancelStaleGtcExitOnEntry (CYC=8): NEW ceiling method in B197. Future branch additions require helper extraction first.` | YES |
| Component Summary table | absent / `7` | `new private method | **8 (ceiling)** | CopyEngine.cs | T3` | YES |

CYC derivation in revised plan verified:

| Branch | Description | Count |
|--------|-------------|-------|
| base | | 1 |
| guard1 | `if (e?.Order == null)` | 1 |
| guard2 | `if (e.Order.OrderState != OrderState.Filled)` | 1 |
| guard3-if | `if (e.Order.Name == null \|\| ...)` | 1 |
| guard3-OR | `\|\|` operator in guard3 | 1 |
| guard3-AND | `&&` operator in guard3 | 1 |
| guard4 | `if (e.Order.Account == null)` | 1 |
| foreach | `foreach (var o in acc.Orders.ToList())` | 1 |
| **Total** | | **8** |

CYC = 8. At JS-066 ceiling. PASS (not exceeded). Ceiling notation is now correct.

**V-02 resolution**: CONFIRMED. Annotation correct. Ceiling documented. JS-066 PASS.

---

### W-02 — IsStaleGtcExitOrder CYC Annotation — RESOLVED (was warning)

**Prior state**: Plan annotated `IsStaleGtcExitOrder` CYC as **4**. Correct count is **5**
(null-check `||` adds 1 branch per McCabe convention).

**Revised plan §3 T3 check**:
- Comment: `// Helper predicate: CYC=5: base(1) + null-if(1) + null-or(1) + Working-check(1) + return-or(1)`
- CYC derivation: `base(1) + null-if(1) + null-or(1) + Working-check(1) + return-or(1) = CYC = 5`
- §7 table: `IsStaleGtcExitOrder` row — updated per plan (CYC=5 in §3; §7 table not shown separately but §3 and §5 are consistent)

**W-02 resolution**: CONFIRMED. CYC=5 annotation correct.

---

### W-03 — Naming Convention (Is... prefix on void method) — RESOLVED (was warning)

**Prior state**: Method named `IsLeaderFlatDrainAbort` (void). The `Is...` prefix violates CopyEngine.cs
convention where `Is...` is reserved for boolean predicates.

**Required action**: Rename to `TryAbortLeaderFlatDrains` (architect decision, as per prior review).

**Revised plan check** — grep for all occurrences of both names:

| Name | Count in revised plan | Result |
|------|----------------------|--------|
| `TryAbortLeaderFlatDrains` | 23 occurrences (title, §1, §2 T2 Baseline, §3 T2, §4, §5 T2, §6 T2, §7, §8, §9, Component Summary) | CORRECT |
| `IsLeaderFlatDrainAbort` | 1 occurrence — §2 T2 Baseline NOTE (historical reference only: "The method was originally named `IsLeaderFlatDrainAbort` in the DW-B156-DEFER-1 backlog.") | ACCEPTABLE |

The single residual reference to `IsLeaderFlatDrainAbort` is explicitly historical (explaining the DW
card origin and why the rename was made). It does not propose using the old name. All active design
references use `TryAbortLeaderFlatDrains`.

**W-03 resolution**: CONFIRMED. No residual `Is...` prefix on void method in active design.

---

## Regression Checks (Full Re-Run)

### Check 1: Lane-Split Gate Compliance

| Item | Result |
|------|--------|
| Gate result stated explicitly | YES — "LANE-SPLIT GATE RESULT: SINGLE-PIPELINE" at line 12 and line 24 |
| Q1 answered | YES — T1 and T3 share the same `else if (OrderState.Filled)` block; same method, same 5-line block |
| SINGLE-PIPELINE confirmed | PASS |
| Ordering note (Q2 for record) | Present — T3 call site ordered after T1 in Filled branch noted |

**Gate result**: SINGLE-PIPELINE. PASS.

---

### Check 2: Source Accuracy

Verified against `src/PropTraderTools/CopyEngine.cs`.

#### T1 Baseline
| Claim | Actual | Result |
|-------|--------|--------|
| `_drainOwnedOrderIds` at line 394 as ConcurrentDictionary<string,byte> | Line 394: `private readonly ConcurrentDictionary<string, byte> _drainOwnedOrderIds =` | PASS |
| `.ContainsKey(order.OrderId)` at line 916 | Line 916: `if (_drainOwnedOrderIds.ContainsKey(order.OrderId))` | PASS |
| `_followerSlateReady` at line 401 as ConcurrentDictionary<string,bool> | Line 401: `private readonly ConcurrentDictionary<string, bool> _followerSlateReady =` | PASS |
| `AbortDrainOnFill` at line 7029 | Line 7029: `private void AbortDrainOnFill(string acctKey)` | PASS |
| Write sites 1041, 1060, 2735 | Confirmed in source | PASS (W-01 residual — see below) |

#### T2 Baseline
| Claim | Actual | Result |
|-------|--------|--------|
| `TryEvictFollowerBeSlot` at lines 1718-1737 | Line 1718: `private void TryEvictFollowerBeSlot(OrderEventArgs e)` | PASS |
| CYC=6 per comment line 1696 | Line 1696: `// TryEvictFollowerBeSlot: CYC=6.` | PASS |
| `IsFollowerAccount` at line 813 | Line 813: `internal bool IsFollowerAccount(Account acc)` | PASS |
| `IsFlat` at line 5730 | Line 5730: `private static bool IsFlat(NinjaTrader.Cbi.Position pos)` | PASS |
| `FindPosition` at line 5792 | Line 5792: `private Position FindPosition(Account acc, Instrument instrument)` | PASS |
| `_pendingDispatchDrains` at line 388 | Line 388: `private readonly ConcurrentDictionary<string, PendingDispatchDrain> _pendingDispatchDrains =` | PASS |
| `IsLeaderFlatDrainAbort` ABSENT | No match in source | PASS |
| `TryAbortLeaderFlatDrains` ABSENT (pre-B197) | No match in source | PASS |

#### T3 Baseline
| Claim | Actual | Result |
|-------|--------|--------|
| `IsPttCopyEntryFilled` REMOVED | No method exists | PASS |
| Stub `IsPttCopyEntryFilledTestable` at line 837 returns `false` | Line 837: `internal bool IsPttCopyEntryFilledTestable(NinjaTrader.Cbi.Order o) => false;` | PASS |
| `TryCancelStaleGtcExitOnEntry` ABSENT | No match | PASS |
| `acc.Orders.ToList()` pattern at line 1941 | Confirmed pattern exists | PASS |
| `acc.Cancel(new Order[] { o })` pattern at line 1947 | Confirmed pattern exists | PASS |

#### T4 Baseline
| Claim | Actual | Result |
|-------|--------|--------|
| `IsPttDragOrphanCancellable` at lines 1905-1914 | Line 1905: `private static bool IsPttDragOrphanCancellable(Order o, Instrument instr)` | PASS |
| Guard(3) exact-match at line 1911 | Line 1911: `if (o.Name != "PTT-TGT-Drag" && o.Name != "PTT-STP-Drag")` | PASS |
| `IsPttDragOrphanCancellableTestable` at lines 1918-1932 | Line 1918: `internal static bool IsPttDragOrphanCancellableTestable(...)` | PASS |
| Guard(3) exact-match at line 1929 | Line 1929: `if (orderName != "PTT-TGT-Drag" && orderName != "PTT-STP-Drag")` | PASS |

**Source accuracy overall**: PASS.

---

### Check 3: JS Rule Compliance

#### JS-021 (No lock()) — P0

| Ticket | Check | Result |
|--------|-------|--------|
| T1 | `_drainOwnedOrderIds.ContainsKey()` and `_followerSlateReady[key] = true` are ConcurrentDictionary ops. Lock-free. | PASS |
| T2 | `_pendingDispatchDrains` foreach is ConcurrentDictionary enumeration (lock-free). `AbortDrainOnFill` uses TryRemove (lock-free). | PASS |
| T3 | `acc.Orders.ToList()` + `acc.Cancel()` — no lock(). Pattern mirrors CancelPttDragOrphansForAccount at lines 1939-1954. | PASS |
| T4 | Pure static predicate. No state. No lock(). | PASS |

**JS-021 verdict**: PASS.

#### JS-066 (CYC ≤ 8) — P1

| Method | Annotated CYC | Verified CYC | Result |
|--------|---------------|--------------|--------|
| `TrySetSlateReadyOnDrainFill` (new) | 5 | 5 (base+4 guards) | PASS |
| `TryAbortLeaderFlatDrains` (new) | 5 | 5 (base+3 guards+foreach) | PASS |
| `TryCancelStaleGtcExitOnEntry` (new) | **8 (ceiling)** | **8** (base+guard1+guard2+guard3-if+guard3-OR+guard3-AND+guard4+foreach) | PASS |
| `IsStaleGtcExitOrder` (new helper) | **5** | **5** (base+null-if+null-or+Working+return-or) | PASS |
| `TryEvictFollowerBeSlot` (call added) | 6 | 6 (method call +0 CYC delta) | PASS |
| `IsPttDragOrphanCancellable` (guard3 change) | 4 | 4 (unchanged) | PASS |
| `IsPttDragOrphanCancellableTestable` (guard3 change) | 4 | 4 (unchanged) | PASS |

**JS-066 verdict**: PASS. All annotations now correct. TryCancelStaleGtcExitOnEntry correctly
documented as CYC=8 ceiling with future-branch extraction requirement.

#### JS-001 (No throw in hot path) — P0

| Ticket | Check | Result |
|--------|-------|--------|
| T1 | No exception-throwing code. Only early returns and ConcurrentDictionary write. | PASS |
| T2 | No exception-throwing code. Only early returns and method call. | PASS |
| T3 | `try { acc.Cancel(...); } catch (Exception ex) { StatusUpdate?.Invoke(...); }` — catch absorbs, does NOT rethrow. Mirrors CancelPttDragOrphansForAccount at lines 1945-1953. | PASS |
| T4 | Static predicate, no throw. | PASS |

**JS-001 verdict**: PASS.

#### JS-056 (One Assert per Test) — P1

As verified in V-01 re-check above: 10 [Fact]s, each with exactly 1 assert. **PASS.**

#### ASCII-only (JS-081)

| Ticket | Log markers / strings | Result |
|--------|-----------------------|--------|
| T1 | `"[SLATE-READY-DRAIN] acct="` — all ASCII | PASS |
| T2 | `"[DRAIN-ABORT-LEADER-FLAT] triggered by "` — all ASCII | PASS |
| T3 | `"[GTC-SWEEP] cancelled "`, `"[GTC-SWEEP] cancel error: "` — all ASCII | PASS |
| T4 | `"PTT-TGT-Drag"`, `"PTT-STP-Drag"` — all ASCII | PASS |

**ASCII-only verdict**: PASS.

---

### Check 4: Plan Completeness (Required Sections)

| Section | Present | Notes |
|---------|---------|-------|
| §1 Epic Summary | YES | 4-ticket table with DW IDs, priorities, origins |
| §2 Source Baseline | YES | T1-T4 subsections with field/method state |
| §3 Per-ticket design | YES | T1-T4 with signatures, pseudocode, CYC, NT8 API |
| §4 Cross-ticket sequencing | YES | OnOrderUpdate Filled branch, TryEvictFollowerBeSlot, T4 independence, recommended order |
| §5 JS compliance | YES | Per-ticket tables covering JS-021, JS-001, JS-002, JS-066, ASCII |
| §6 Test strategy | YES | 10 [Fact]s across T1-T4 (V-01 resolved) |
| §7 CYC impact summary | YES | 7-method table; CYC=8 ceiling correct (V-02 resolved) |
| §8 SIM gate plan | YES | SIM-1 through SIM-4 with PASS/FAIL criteria |
| §9 Deferred backlog | YES | DW IDs closed; B196 carry-forwards; new DW items (none) |

**Plan completeness**: All 9 required sections present. PASS.

---

### Check 5: T1 Ordering Constraint (before AbortDrainOnFill)

Documented in two locations:

1. **§3 T1** (lines 144-147): "TrySetSlateReadyOnDrainFill MUST be called BEFORE AbortDrainOnFill.
   AbortDrainOnFill (line 7029) removes the order ID from `_drainOwnedOrderIds`. Guard (3) checks
   ContainsKey on that same dict. If AbortDrainOnFill runs first, the ID is gone and Guard (3)
   always fails."

2. **§4 Cross-ticket sequencing** code snippet: `TrySetSlateReadyOnDrainFill(e); // T1: FIRST — needs
   _drainOwnedOrderIds intact`

Ordering invariant rationale verified against CopyEngine.cs: `AbortDrainOnFill` at line 7029 calls
`_drainOwnedOrderIds.TryRemove(id, out _)` at line 7033 (confirmed). Invariant is technically correct.

**T1 ordering constraint check**: PASS.

---

### Check 6: T3 Design Deviation (IsPttCopyEntryFilled stub workaround)

The plan:
1. Correctly identifies `IsPttCopyEntryFilled` as removed in B180 — confirmed absent in CopyEngine.cs.
2. Correctly identifies `IsPttCopyEntryFilledTestable` at line 837 as stub always returning `false`.
3. Explicitly warns: using the stub would make TryCancelStaleGtcExitOnEntry a SILENT BUG (always returns early).
4. Provides inline name-check Guard(3): `StartsWith("PTT-Copy")` OR `Name == "Entry"`.
5. Does NOT propose calling the stub.

**T3 design deviation check**: PASS.

---

### Check 7: Cross-Ticket Sequencing

**OnOrderUpdate Filled branch** (§4):
```csharp
else if (e.Order.OrderState == OrderState.Filled)
{
    TrySetSlateReadyOnDrainFill(e);          // T1: FIRST
    AbortDrainOnFill(e.Order.Account.Name);  // existing
    TryCancelStaleGtcExitOnEntry(e);         // T3: after drain cleanup
}
```
T3's independence from drain state is stated explicitly.

**TryEvictFollowerBeSlot** (T2 call site — §4):
- TryAbortLeaderFlatDrains inserted AFTER null guard, BEFORE IsEvictTriggerState.
- Rationale: e.Order must be non-null before TryAbortLeaderFlatDrains can dereference `e.Order.Account`.
- Placement confirmed correct.

**T4 independence**: Explicitly stated — no ordering dependency, no shared state with T1-T3.

**Cross-ticket sequencing check**: PASS.

---

### Check 8: NT8 API Validity

| Claim | Verification | Result |
|-------|-------------|--------|
| No `AtmStrategyCreate` used (StrategyBase-only API) | Not present in plan | PASS |
| No `async/await` in OnInitialize/OnDestroyed | Not present | PASS |
| `acc.Orders.ToList()` | Confirmed at line 1941 | PASS |
| `acc.Cancel(new Order[] { o })` | Confirmed at line 1947 | PASS |
| `_drainOwnedOrderIds.ContainsKey()` | ConcurrentDictionary<string,byte> at line 394 | PASS |
| `_followerSlateReady` indexer write | ConcurrentDictionary<string,bool> at line 401 | PASS |
| `IsFollowerAccount(Account)` | At line 813 | PASS |
| `IsFlat(FindPosition(...))` | IsFlat at line 5730, FindPosition at line 5792 | PASS |
| `AbortDrainOnFill(string)` | At line 7029 | PASS |
| No `DateTime.Now` | Not present in new methods | PASS |
| No hardcoded `#RRGGBB` hex | Not present | PASS |

**NT8 API check**: PASS.

---

## Residual Warning (Carry-Forward from Prior Review)

### W-01 — Baseline Description Inaccuracy at Line 1041 (NOT BLOCKING)

**Status**: Unresolved in the revised plan. The prior review raised this as a warning (not a
violation). The revised plan still describes lines 1041, 1060, 2735 as "`_followerSlateReady[acc.Name]
= true` write sites" without distinguishing write direction.

**Fact**: Line 1041 (`TrySetFollowerSlateUnready`) writes `false`, not `true`. Lines 1060 and 2735
write `true`.

**Impact**: The new method body (T1) correctly proposes `_followerSlateReady[e.Order.Account.Name] =
true`, which is accurate. The inaccurate baseline description could mislead an engineer into believing
there are 3 pre-existing `true`-write sites when there are 2. This is cosmetic — the proposed body is
correct regardless.

**Action**: Engineer should note line 1041 writes `false` (TrySetFollowerSlateUnready). No plan
revision required for REVIEW_PASS.

---

## Spec Coverage Matrix

B197 is a deferred-work restoration block. Requirements carried from B154/B156/B157 DW cards.

| Requirement / DW ID | Addressed | Plan Section |
|---------------------|-----------|--------------|
| DW-B157-01: TrySetSlateReadyOnDrainFill absent from megacommit | YES | §2 T1, §3 T1, §4, §7 |
| DW-B156-01: TryAbortLeaderFlatDrains wiring | YES | §2 T2, §3 T2, §4, §7 |
| DW-B156-DEFER-1: TryEvictFollowerBeSlot leader-flat branch | YES | §3 T2 (call site), §4 |
| DW-B154-01: TryCancelStaleGtcExitOnEntry (HIGH) | YES | §2 T3, §3 T3, §4, §7 |
| DW-B154-02: TryCancelStaleGtcExitOnEntry (MEDIUM) | YES | §2 T3, §3 T3 |
| DW-B154-03: IsPttDragOrphanCancellable suffix fix | YES | §2 T4, §3 T4, §7 |
| B196 carry-forward items (non-B197 scope) | Acknowledged | §9 carry-forward table |

**Spec coverage**: All 6 DW IDs addressed. PASS.

---

## Violations Summary

| ID | Rule | Description | Location | Severity | Status |
|----|------|-------------|----------|----------|--------|
| V-01 | JS-056 | (PRIOR) Multi-assert [Fact]s in T1/T2/T3 | §6 | P1 | **RESOLVED** |
| V-02 | JS-066 annotation | (PRIOR) TryCancelStaleGtcExitOnEntry CYC annotated as 7; correct is 8 (ceiling) | §3 T3, §7 | P1 | **RESOLVED** |

**Current violations**: 0

## Warnings Summary

| ID | Description | Status |
|----|-------------|--------|
| W-01 | Line 1041 is `_followerSlateReady[acc.Name] = false`, not `= true`. Plan baseline description does not distinguish write direction. | OPEN (carry-forward, not blocking) |
| W-02 | (PRIOR) IsStaleGtcExitOrder CYC annotated as 4; correct is 5. | **RESOLVED** |
| W-03 | (PRIOR) `IsLeaderFlatDrainAbort` void method with `Is...` prefix. | **RESOLVED** — renamed to `TryAbortLeaderFlatDrains` throughout. |

---

## Decision: REVIEW_PASS

All prior violations (V-01: JS-056; V-02: JS-066 annotation) are resolved in the revised plan.
All prior warnings (W-02, W-03) are resolved. W-01 remains open as a cosmetic baseline
inaccuracy — not blocking.

The plan is **cleared for Phase 3 (ticket generation)**.

---

*Written by: ptt-plan-reviewer*
*Epic: B197*
*Phase: 2 (Plan Review — Re-Review)*
*Prior verdict: REVIEW_FAIL (2 violations)*
*Current verdict: REVIEW_PASS (0 violations, 1 residual warning W-01)*
*Max review cycles remaining: 1*
