# B130-LaneA Final Review

**Epic**: B130-LaneA
**Defect**: DW-B137 — IsAtmSTPOrder Wrong Name Format
**Phase**: 5 (Final Cross-File Coherence Review)
**Reviewer**: ptt-plan-reviewer
**Date**: 2026-09-01

---

## Final Review Result: FINAL_PASS

---

## A. Spec Requirements Satisfaction

| Requirement | Status | Evidence |
|-------------|--------|---------|
| DW-B137 root cause fixed: `IsAtmSTPOrder` predicate extended to cover ATM Stop1/Stop2/Stop3 format | PASS | `CopyEngine.cs` L2107–2113: `StartsWith("Stop")` + `StartsWith("Target")` clauses added |
| Stop1/Stop2/Stop3 drag → cancel+resubmit on follower accounts | PASS | `IsAtmSTPOrder` returns `true` for Stop1 → branch (3) `isStop && IsAtmSTPOrder` at L2151 → `SyncAtmFollowerBracket` |
| Target1/Target2/Target3 drag → cancel+resubmit on follower accounts | PASS | `IsAtmSTPOrder` returns `true` for Target1 → branch (3b) `!isStop && IsAtmSTPOrder` at L2156 → `SyncAtmFollowerTarget` |
| "Buy STP"/"Sell STP" backward compatibility preserved | PASS | `EndsWith("STP", OrdinalIgnoreCase)` clause retained at L2110; tested in `B130_DW137_Stop1NameRoutesToCancelResubmit` |
| OQ-03 safety preserved for target cancel+resubmit | PASS | Architecture plan section B confirms Gate 2 `FindMatchingRule` (L1609) returns null for follower orders, blocking `TryCancelFollowerEntries` unconditionally — same guarantee applies to SyncAtmFollowerTarget |
| PTT- prefix on new order name | PASS | `"PTT-TGT-Drag"` at L2292; SCAN-06 confirmed 3 hits including L2292 |
| `IsTrailingStop` guard not hit for ATM stop brackets | PASS | Branch (3) at L2151 fires and returns before reaching IsTrailingStop guard at L2162 |
| Layer 1 `IsBracketLegStatic` already passes Stop1/Target1 (no change required) | PASS | Pre-existing `StartsWith("Stop")` and `StartsWith("Target")` clauses in IsBracketLegStatic (L3639); no modification required |
| 2 new [Fact] tests: `B130_DW137_Stop1NameRoutesToCancelResubmit` + `B130_DW137_Target1NameRoutesCorrectly` | PASS | V-CHECK-04: both tests present with all required assertions; BUILD_PASS confirmed |
| All 6 B129Tests updated for DW-B137 behavior change (3 Assert.False → Assert.True for Stop1) | PASS | V-CHECK-05: all 3 DW-B134 group tests updated; all 6 B129Tests present and passing |

---

## B. Rules Catalog Compliance (cross-file)

| Rule ID | Rule | Check | Result |
|---------|------|-------|--------|
| JS-021 | No `lock()` — P0 CRITICAL | `grep -r "lock\s*\(" src/PropTraderTools/CopyEngine.cs` → 8 results, all in comments (lines 309, 343, 1199, 1228, 1670, 2758, 3096, 4027). Zero actual `lock()` calls. Modified region L2051–L2308: 0 lock statements. | PASS |
| JS-001 | No throw in hot path | `SyncAtmFollowerTarget`: exceptions caught in two independent try/catch blocks. `acc.Cancel` and `acc.CreateOrder` both wrapped. No `throw` statement in method body. | PASS |
| JS-002 | No null return where value expected | Both new methods are `void`. Null guards use `return;` (void method — not null return from value-expected method). | PASS |
| JS-033 | No `async void` | `grep "async void " CopyEngine.cs` → 0 actual async void methods (only a comment at L1567). | PASS |
| JS-036 | No `new byte[]` heap alloc in hot path | N/A — not applicable to this change. `new Order[] { fo }` is pre-existing NT8 array pattern (both SyncAtmFollowerBracket and SyncAtmFollowerTarget). Verifier acknowledged as accepted pre-existing pattern. | PASS |
| JS-066 | CYC ≤ 8 | IsAtmSTPOrder=1, SyncFollowerBracket=7, SyncAtmFollowerTarget=4. All ≤ 8. See Section C. | PASS |
| NT8-014 | CreateOrder name starts with "PTT-" | `"PTT-TGT-Drag"` at L2292. `"PTT-STP-Drag"` at L2232 (unchanged). Both confirmed by SCAN-06. | PASS |
| NT8-007 | `(CustomOrder)null` as last arg | `(NinjaTrader.Cbi.CustomOrder)null` at L2294 (SyncAtmFollowerTarget) and L2234 (SyncAtmFollowerBracket). | PASS |
| NT8-013 | `Core.Globals.MaxDate` for GTC | `NinjaTrader.Core.Globals.MaxDate` at L2293 and L2233. | PASS |
| ASCII-only | No non-ASCII in source | `grep "[^\x00-\x7F]" CopyEngine.cs` → 0 results. | PASS |
| DateTime.Now ban | `DateTime.UtcNow` not `.Now` | `grep "DateTime\.Now" CopyEngine.cs` → 0 results. | PASS |

**No JS violations found across all modified and new code.**

---

## C. CYC Budget (all modified/new methods)

| Method | File:Line | Old CYC | New CYC | ≤ 8? | Comment in Source |
|--------|-----------|---------|---------|------|-------------------|
| `IsAtmSTPOrder` | `CopyEngine.cs:2107` | 1 | 1 | **PASS** | "CYC=1: expression body. JS-021: no lock." (L2106) — compound OR clauses are not McCabe decision nodes |
| `SyncFollowerBracket` | `CopyEngine.cs:2131` | 6 | 7 | **PASS** | "CYC=7: fo null(1), price delta(2), ATM STP(3), ATM TGT(3b), IsTrailingStop(4), isStop branch(5)" (L2127). Comment consistent with source. |
| `SyncAtmFollowerTarget` | `CopyEngine.cs:2262` | — (new) | 4 | **PASS** | "CYC=4: (1) acc null, (2) fo null, (3) Block A, (4) newTarget null in Block B" (L2253). Actual McCabe nodes: acc null (1), fo null (2), newTarget null (3) + base = 4. try/catch blocks add 0 McCabe branches. Count is correct. |

> Minor observation (non-blocking, pre-identified by plan reviewer and ticket reviewer): `SyncFollowerBracket` source labels the inner-try branch as `(4)` at L2170, but the CYC comment enumerates it as branch (5). The numeric total CYC=7 is correct and the method is comfortably under budget (≤8). This is a label/comment inconsistency only — no violation.

---

## D. NT8 API Coherence

| Attribute | `SyncAtmFollowerBracket` (L2202, DW-B134) | `SyncAtmFollowerTarget` (L2262, DW-B137) |
|-----------|------------------------------------------|------------------------------------------|
| Order type | `OrderType.StopMarket` | `OrderType.Limit` |
| arg6 (limitPrice) | `0` | `newPrice` |
| arg7 (stopPrice) | `newPrice` | `0` |
| Order name | `"PTT-STP-Drag"` | `"PTT-TGT-Drag"` |
| PTT- prefix | Yes | Yes |
| `Core.Globals.MaxDate` | Yes | Yes |
| `(CustomOrder)null` | Yes | Yes |
| Independent try/catch | Yes (Block A: Cancel, Block B: Create+Submit) | Yes (Block A: Cancel, Block B: Create+Submit) |
| Null guards | acc null (1), fo null (2), newStop null (3) | acc null (1), fo null (2), newTarget null (3) |
| CYC | 4 | 4 |
| Placement | After `SyncFollowerBracket` | Immediately after `SyncAtmFollowerBracket` (L2249+) |

The two methods are symmetric counterparts. `SyncAtmFollowerTarget` is a correct mirror image of `SyncAtmFollowerBracket` with only the order-type-specific arg positions swapped (limitPrice/stopPrice), the order name changed to reflect target semantics, and the StatusUpdate messages updated.

NT8 API validation: `AtmStrategyChangeStopTarget()` is StrategyBase-only and NOT used. `AtmStrategyCreate()` is StrategyBase-only and NOT used. The cancel+resubmit pattern via `acc.Cancel()` + `acc.CreateOrder()` + `acc.Submit()` is the confirmed AddOn-context path.

---

## E. Test Coverage

| Test | Assertions | Result |
|------|------------|--------|
| `B130_DW137_Stop1NameRoutesToCancelResubmit` | Stop1→true, Stop2→true, Stop3→true (new); "Buy STP"→true, "Sell STP"→true (backward compat); "Entry"→false, "PTT-Copy"→false | PASS — V-CHECK-04 confirmed all 7 assertions present |
| `B130_DW137_Target1NameRoutesCorrectly` | Target1→true, Target2→true, Target3→true (new); "PTT-Copy"→false, "PTT-TGT-Drag"→false (PTT orders excluded) | PASS — V-CHECK-04 confirmed all 5 assertions present |
| `B129_DW134_STPSuffixDetectedByIsBracketLegStatic` | Updated: Stop1 assertion changed Assert.False→Assert.True (DW-B137 behavior) | PASS — V-CHECK-05 confirmed |
| `B129_DW134_SyncFollowerBracketCancelResubmitFiredForAtmBracket` | Updated: Stop1 assertion changed Assert.False→Assert.True (DW-B137 behavior) | PASS — V-CHECK-05 confirmed |
| `B129_DW134_OQ03_CancelledBracketDoesNotTriggerFollowerEntryCancel` | Updated: Stop1 assertion changed Assert.False→Assert.True (DW-B137 behavior) | PASS — V-CHECK-05 confirmed |
| `B129_DW135_GuardClearedAfterLeaderFlat` | Unchanged — DW-B135 logic unaffected by this block | PASS |
| `B129_DW135_DW128ProtectionPreservedDuringRaceWindow` | Unchanged | PASS |
| `B129_DW135_FirstEntryAfterRestartNotBlocked` | Unchanged | PASS |

Build result: 0 errors, 0 warnings (engineer SCAN-07 + verifier SCAN-07 both confirm). All 8 tests compile and pass.

---

## F. Cross-File Coherence

| Check | Result |
|-------|--------|
| `IsAtmSTPOrder` gates both branch (3) stop path and branch (3b) target path | PASS — L2151: `isStop && IsAtmSTPOrder(fo)` → stop path; L2156: `!isStop && IsAtmSTPOrder(fo)` → target path |
| `SyncFollowerBracket` calls `SyncAtmFollowerTarget` which exists in file | PASS — method confirmed at CopyEngine.cs:2262 |
| `SyncAtmFollowerTarget` placed immediately after `SyncAtmFollowerBracket` | PASS — SyncAtmFollowerBracket ends ~L2248; SyncAtmFollowerTarget starts L2250 (comment) / L2262 (signature) |
| Branch (3b) placement: after branch (3), before `IsTrailingStop` guard | PASS — L2151→L2156→L2162 ordering confirmed in source |
| `PropTraderTools.csproj` compiles `B130Tests.cs` | PASS — V-CHECK-06: L158 `<Compile Include="Tests\B130Tests.cs" />` confirmed |
| Build: 0 errors, 0 warnings | PASS — engineer SCAN-07 + independent verifier SCAN-07 both confirm |
| Option A safety: 0 `CreateOrder` calls use "Stop*"/"Target*" name prefix | PASS — `grep "CreateOrder.*[Ss]top\|CreateOrder.*[Tt]arget" CopyEngine.cs` → 1 result at L2105 (comment only) |
| 7 scans all returned expected results (per verifier Layer 3) | PASS — all 7 scans match engineer Layer 2 report exactly; no discrepancies |

---

## Section K — Deferred Work (REQUIRED)

### Items CLOSED This Block (B130-LaneA)

| ID | Item | Priority | Closed By |
|----|------|----------|-----------|
| DW-B137 | `IsAtmSTPOrder` extended to cover Stop1/Stop2/Stop3 and Target1/Target2/Target3 ATM name formats. `SyncAtmFollowerTarget` added for ATM Limit target bracket cancel+resubmit. Branch (3b) added to `SyncFollowerBracket`. B129Tests.cs updated (3 assertions corrected). B130Tests.cs created with 2 new [Fact] tests. | P1 | B130 LaneA T1 — BUILD_PASS, VERIFY_PASS |

### New Deferred Items (B130-LaneA)

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-B137-SIM | Director SIM Gate: Stop1/Target1 drag sync verification in live NT8 session. Code fix is BUILD_PASS + VERIFY_PASS. Director must enter a position using `MES $200 SL 6` ATM template, drag Stop1 on leader (Sim101), and confirm follower Stop1 updated via `PTT-STP-Drag` cancel+resubmit. Same verification applies to Target1 drag → `SyncAtmFollowerTarget` path (`PTT-TGT-Drag` order appears on followers at correct price). | P1 | Next Director SIM gate session after F5 compilation confirms no errors | OPEN |
| DW-B134-OCO | OCO Orphan Risk After ATM Cancel+Resubmit — carry-forward from B129 LaneB. Risk now applies to both `SyncAtmFollowerBracket` (stop) and `SyncAtmFollowerTarget` (target): cancelling one leg of an ATM OCO pair may affect the partner leg. NT8 ATM OCO behavior on `acc.Cancel` of a single bracket is unverified. Fix direction gated on Director SIM observation. | P2 | B131 or first SIM gate session | OPEN — carry-forward |

---

## Violations Found

None.

---

## Final Recommendation

**FINAL_PASS** — all 6 coherence checks passed. The B130-LaneA implementation is correct, coherent, and complete.

- DW-B137 root cause fully addressed: `IsAtmSTPOrder` predicate extended with `StartsWith("Stop")` and `StartsWith("Target")` clauses covering the MES $200 SL 6 ATM template naming format.
- Stop drag path (Stop1/Stop2/Stop3) correctly routed to existing `SyncAtmFollowerBracket` cancel+resubmit.
- Target drag path (Target1/Target2/Target3) correctly routed to new `SyncAtmFollowerTarget` cancel+resubmit (Limit order, `PTT-TGT-Drag`).
- Backward compatibility ("Buy STP"/"Sell STP") preserved and tested.
- CYC budget: all methods at or below threshold (IsAtmSTPOrder=1, SyncFollowerBracket=7, SyncAtmFollowerTarget=4; all ≤ 8).
- Zero JS rule violations. Zero NT8 API violations. Zero non-ASCII. Zero lock(). Zero DateTime.Now. Zero async void.
- Build: 0 errors, 0 warnings. All 8 tests pass (2 new B130 + 6 B129 backward compat).
- 1 deferred item (DW-B137-SIM): Director SIM gate required for live validation. Code correctness confirmed by independent verification. Pipeline complete for B130-LaneA.
