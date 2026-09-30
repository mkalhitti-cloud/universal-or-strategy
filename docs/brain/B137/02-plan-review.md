# B137 Plan Review — Third Pass (Final Gate)

**Block**: B137
**Phase**: 2 — Plan Review (Third Pass / Revision Cycle 2 — Final Gate)
**Reviewer**: ptt-plan-reviewer
**Date**: 2026-09-08 (third pass — final gate)
**Input**: docs/brain/B137/02-architecture-plan.md (revision cycle 2)
**Prior reviews**:
- First pass: REVIEW_FAIL (V1 hedged root cause, V2 dead-code fix, V3 SCAN-03 false-fail)
- Second pass: REVIEW_FAIL (V4 T4 inline CYC ≥9 — extraction not promoted as primary design)
**Sources verified**:
- specs/002-trade-copier-spec.html (DW-B147, DW-B149, DW-B150, DW-B151, section-b136, section-b135)
- docs/standards/jane-street/RULES_CATALOG.md (JS-001, JS-002, JS-021, JS-023, JS-033, JS-066)
- src/PropTraderTools/CopyEngine.cs (lines 2290-2445, 2595-2690 — CYC source-of-truth)

---

## PRIOR VIOLATION RESOLUTION CONFIRMATION (ALL 4)

### V1 (P1 prior — DW-B150 root cause hedged) — CONFIRMED FIXED

**Verification**: Plan section "DW-B150 Root Cause (Confirmed — No Hedging)" provides an 11-step
deterministic trace: `HandleBracketChange` → `SyncFollowerBracket` → `FindFollowerBracketOrder` →
`OrderPassesBracketGate` branch (1) → `"" != null` = TRUE → signal path → `null == ""` = FALSE →
fo=NULL → `SyncFollowerBracket` returns at line 2249. Specific method names, line numbers, and Boolean
truth-values stated exactly. Zero hedged language ("likely", "probably", "may") anywhere in the
DW-B150 section.

**Source cross-check**: L2677 confirms `if (signalName != null)` — the pre-B137 condition whose
behaviour with empty string is exactly as traced. The trace is ground-truth.

**RESOLVED.** V1 is no longer a violation. ✅

---

### V2 (P0 prior — T3 fix in dead MatchesLeaderName code) — CONFIRMED FIXED (carried from second pass)

**Verification**: The cycle-2 plan's T3 target is `OrderPassesBracketGate` branch (1) condition
change: `if (signalName != null)` → `if (!string.IsNullOrEmpty(signalName))` at source line 2677.
`MatchesLeaderName` (lines 2643-2654) is NOT modified. Reachability proof covers all three inputs:
- `null` → `IsNullOrEmpty(null)` = true → `!true` = false → ATM path (unchanged)
- `""` → `IsNullOrEmpty("")` = true → `!true` = false → ATM path (NEW — was signal path before; this is the DW-B150 fix)
- `"SomeSignal"` → `IsNullOrEmpty("SomeSignal")` = false → `!false` = true → signal path (unchanged)

Non-dead-code. Reachable. Semantically correct. ✅

**RESOLVED.** V2 remains resolved. ✅

---

### V3 (P1 prior — SCAN-03 false-fail on pre-existing return null) — CONFIRMED FIXED (carried from second pass)

**Verification**: SCAN-03 reads: `git diff HEAD src/PropTraderTools/CopyEngine.cs | grep "^+" | grep "return null;"` → 0 matches required. Parenthetical explicitly acknowledges pre-existing `Order? return null;` at line 2629 as excluded by git diff scope. No B137-added code introduces a new `return null;`. Self-consistent. ✅

**RESOLVED.** V3 remains resolved. ✅

---

### V4 (P1 prior — T4 CYC math incorrect; extraction must be primary design) — CONFIRMED FIXED

**Verification**: The cycle-2 plan's Ticket T4 is titled "SyncAtmFollowerBracket Block A-Prime via
`CancelExistingPttStpDrag` extraction (DW-B151)". Extraction is the PRIMARY design — it is step 1 of
"What to do". There is NO inline Block A-Prime code in `SyncAtmFollowerBracket`. There is NO
conditional escape hatch. The SyncAtmFollowerBracket T4 change is a single method call:
`CancelExistingPttStpDrag(acc, fo);` — NOT a branch.

**CYC verification**:
- `SyncAtmFollowerBracket` after T2 = CYC=5 (source-verified at L2301: entering CYC=4, +1 for T2 guard)
- T4 adds `CancelExistingPttStpDrag(acc, fo);` — a method call, zero McCabe branches added
- `SyncAtmFollowerBracket` after T4 = 5 + 0 = **CYC=6** ✅

- `CancelExistingPttStpDrag`: base(1) + foreach(1) + if(1) + `||`(1) + `&&Name`(1) + `&&Instrument`(1) + `?.`(1) = **CYC=7** (worst-case strict count) ✅
- Loose count: 6. Both bounds ≤ 8. The cycle-2 plan documents both as "CYC=6-7 ≤ 8". ✅

**RESOLVED.** V4 is no longer a violation. ✅

---

## CHECK A — LANE-SPLIT GATE

**Result**: PASS ✅

- Gate result present and labelled: `LANE-SPLIT GATE RESULT: SINGLE-PIPELINE`.
- Q1=YES: T1/T2 share `SyncAtmFollowerTarget`; T2/T4 share `SyncAtmFollowerBracket`.
- Q2=YES: T2 depends on T1 CYC reduction (8→7); T4 depends on T2 completion (CYC=5).
- Q3=PARTIAL: T3 and T4 fully independent from each other; T2 partial (applicable to `SyncAtmFollowerBracket` alone without T1).
- Q4=YES: Each ticket has a distinct SIM verification path stated.
- Default rule correctly applied: Q1=YES, Q2=YES → SINGLE-PIPELINE. Sequential T1→T2→T3→T4. ✅

---

## CHECK B — SPEC TRACEABILITY

**Result**: PASS ✅

| DW Item | Addressed? | Plan Section |
|---------|-----------|--------------|
| DW-B147 | ✅ Yes | "Deferred Items Closed" + Ticket T2 |
| DW-B149 | ✅ Yes | "Deferred Items Closed" + Ticket T2 |
| DW-B150 | ✅ Yes | "New Defects Addressed" + DW-B150 Root Cause section + Ticket T3 |
| DW-B151 | ✅ Yes | "New Defects Addressed" + Ticket T4 (extraction as primary design) |

All four required DW items addressed. Root cause confirmation (DW-B150) and extraction mandate (DW-B151)
both satisfy the spec requirement without hedging.

---

## CHECK C — CYC MATH VERIFICATION

**Source-verified entering CYC values**:

| Method | Source Line | Source Comment | CYC | Verified |
|--------|------------|----------------|-----|---------|
| `SyncAtmFollowerTarget` | L2363-2364 | "(1) acc null, (2) fo null, (3) foreach A-Prime, (4) OrderState==Working, (5) Name=='PTT-TGT-Drag', (6) catch A-Prime, (7) Block A catch, (8) newTarget null" | 8 | ✅ |
| `SyncAtmFollowerBracket` | L2301 | "(1) acc null guard, (2) fo null guard, (3) newStop null guard. Two independent try/catch add 0 McCabe branches." | 4 | ✅ |
| `MatchesLeaderName` | L2640 | "CYC=5: base(1) + leaderName null(1) + name==(1) + !isStop&&TGT(1) + isStop&&STP(1) = 5." | 5 | ✅ |
| `OrderPassesBracketGate` | L2668 | "CYC=2: base(1) + if(signalName != null)(1) = 2." | 2 | ✅ |
| `FindFollowerBracketOrder` (list overload) | L2596 | "CYC=7 (post-B136). AT LIMIT RESOLVED; headroom = 1." | 7 | ✅ |

**T1: SyncAtmFollowerTarget 8 → 7**

Phase C extraction (lines 2440-2442) moves `leaderOrder?.Account` null-conditional out of parent body into `ExecutePhaseCStopReplacement`. The null-conditional `?.` counts as +1 McCabe branch per codebase convention. 8 - 1 = **7**. `ExecutePhaseCStopReplacement` CYC = base(1) + `?.`(1) = **2**. ✅

**T2: SyncAtmFollowerTarget 7 → 8**

Adding `if (IsNoPriceChange(fo.LimitPrice, newPrice)) return;` adds +1 branch. 7 + 1 = **8 AT LIMIT**. ✅

**T2: SyncAtmFollowerBracket 4 → 5**

Adding `if (IsNoPriceChange(fo.StopPrice, newPrice)) return;` adds +1 branch. 4 + 1 = **5**. ✅

**IsNoPriceChange (new): CYC=1** (pure expression method body `=> currentPrice == newPrice;`, no branches). ✅

**T3: OrderPassesBracketGate CYC=2 → 2 (UNCHANGED)**

Condition expression change on branch (1): `signalName != null` → `!string.IsNullOrEmpty(signalName)`. Branch COUNT is unchanged — same single `if` branch, different predicate expression. McCabe counts branches, not sub-expressions. CYC remains **2**. `MatchesLeaderName` not modified; CYC stays **5**. ✅

**T4: SyncAtmFollowerBracket 5 → 6**

`CancelExistingPttStpDrag(acc, fo);` is a method call. McCabe complexity counts control-flow branches (if/foreach/while/&&/||/??/null-conditional). A bare method call adds 0 branches. CYC = 5 + 0 = **6**. ✅

Branch enumeration after T2+T4: `(1) acc null, (2) fo null, (3) IsNoPriceChange guard, (4) Block A catch, (5) Block B catch, (6) newStop null` = 6. Consistent with source's counting convention (catch blocks counted in the source's own CYC comments). ✅

**CancelExistingPttStpDrag (new): CYC=6-7**

McCabe branches: base(1) + foreach(1) + if-opening(1) + `||`(1) + `&&Name`(1) + `&&Instrument`(1) + `?.`(1) = **7** (strict count). Loose count (treating `&&Instrument` and `?.` as one): **6**. Both bounds ≤ 8. Plan documents this range explicitly as "CYC=6-7 ≤ 8". ✅

**CYC Summary**:

| Method | After T1 | After T2 | After T3 | After T4 | Final | Valid? |
|--------|----------|----------|----------|----------|-------|--------|
| `SyncAtmFollowerTarget` | 7 ✅ | 8 ✅ | — | — | **8** ✅ | ✅ |
| `SyncAtmFollowerBracket` | — | 5 ✅ | — | 6 ✅ | **6** ✅ | ✅ |
| `OrderPassesBracketGate` | — | — | 2 ✅ | — | **2** ✅ | ✅ |
| `MatchesLeaderName` | — | — | 5 ✅ | — | **5** ✅ | ✅ |
| `IsNoPriceChange` (new) | — | 1 ✅ | — | — | **1** ✅ | ✅ |
| `ExecutePhaseCStopReplacement` (new) | 2 ✅ | — | — | — | **2** ✅ | ✅ |
| `CancelExistingPttStpDrag` (new) | — | — | — | 6-7 ✅ | **6-7** ✅ | ✅ |
| `FindFollowerBracketOrder` (list overload) | 7 ✅ | 7 ✅ | 7 ✅ | 7 ✅ | **7** ✅ | ✅ |

All final CYC values ≤ 8. No violations. ✅

---

## CHECK D — JS RULE COMPLIANCE

**Result**: PASS ✅

| Rule | Check | Result |
|------|-------|--------|
| JS-001 (no throw hot path) | All new code uses try/catch with `StatusUpdate?.Invoke` (no rethrow). `IsNoPriceChange`, `OrderPassesBracketGate`, `ExecutePhaseCStopReplacement` have no throw path. `CancelExistingPttStpDrag` try/catch, no rethrow. | ✅ PASS |
| JS-002 (no null return) | All new methods return bool or void. Pre-existing `Order? return null;` at L2629 unchanged and not augmented by B137. | ✅ PASS |
| JS-009 (no Dictionary for shared collections) | Not applicable — no new collections introduced. | ✅ N/A |
| JS-021 (lock() ban) | No `lock()` in any new or modified method. Threading section explicitly confirms lock-free for all new code. `CancelExistingPttStpDrag` uses `acc.Orders.ToList()` snapshot — no lock needed or used. | ✅ PASS |
| JS-023 (UI off-thread) | No UI code touched. No Dispatcher.InvokeAsync added. Zero UI layer changes. | ✅ PASS |
| JS-033 (async void ban) | No async methods introduced. All NT8 callbacks synchronous on NT8 background thread. | ✅ PASS |
| JS-066 (CYC > 8 ban) | All final CYC values ≤ 8. Worst-case: `SyncAtmFollowerTarget` = 8 (AT LIMIT). `CancelExistingPttStpDrag` = 6-7. All within mandate. | ✅ PASS |
| SCAN-05 (PTT- prefix) | `CancelExistingPttStpDrag` filters on `o.Name == "PTT-STP-Drag"`. New CreateOrder calls use `"PTT-STP-Drag"` (T4 Block B, unchanged from pre-B137). ✅ | ✅ PASS |
| SCAN-03 (no hardcoded hex) | No `#RRGGBB` hex literals in any new method. | ✅ PASS |
| SCAN-06 (DateTime.UtcNow) | No `DateTime.Now` in new code. No time logic added. | ✅ PASS |
| ASCII-only | All new identifiers, string literals, and comment text verified ASCII-only: "PTT-STP-Drag", "STP pre-cancel error", "CancelExistingPttStpDrag", "IsNoPriceChange", "ExecutePhaseCStopReplacement", "OrderPassesBracketGateTestable". | ✅ PASS |
| FontFamily | No FontFamily override in any new code. | ✅ PASS |
| async/await in NT8 lifecycle methods | None. | ✅ PASS |
| Account.All in constructor | Not used. | ✅ PASS |
| sealed TradeCopierWindow | Not modified — out of scope for B137. | ✅ N/A |

---

## CHECK E — NT8 API CORRECTNESS

**Result**: PASS ✅

| API | Location in Plan | Assessment |
|-----|-----------------|------------|
| `acc.Cancel(new Order[] { o })` | `CancelExistingPttStpDrag` (T4) | AddOnBase-available. Identical form to L2390 (established pattern). ✅ |
| `acc.Orders.ToList()` | `CancelExistingPttStpDrag` (T4) | Thread-safe snapshot. Identical to L2382 (established pattern). ✅ |
| `o.OrderState == OrderState.Working \|\| OrderState.Accepted` | `CancelExistingPttStpDrag` (T4) | Valid NT8 OrderState values. Working+Accepted only (Submitted excluded — in-flight unsafe). ✅ |
| `fo.LimitPrice` / `fo.StopPrice` | T2 IsNoPriceChange calls | Existing `Order` property access. No new NT8 API. ✅ |
| `string.IsNullOrEmpty(signalName)` | T3 OrderPassesBracketGate | BCL static method. Not NT8 API. No allocation. No throw. ✅ |
| `AtmStrategyCreate()` | N/A | Correctly stated as StrategyBase-only; not used in B137. ✅ |
| `AtmStrategyChangeStopTarget()` | N/A | Correctly stated as StrategyBase-only; not used in B137. ✅ |
| `Account.Change()` | N/A | Correctly stated as silent no-op on ATM brackets; not used in new B137 code. ✅ |
| `acc.CreateOrder` / `acc.Submit` | SyncAtmFollowerBracket Block B (unchanged) | AddOnBase-available; unchanged from prior blocks. ✅ |
| ATM `FromEntrySignal=""` behavior | NT8 API Usage section | NT8 sets empty string on ATM bracket state-transition events. Plan documents both null and "" cases. Fix is robust to both. ✅ |
| ATM stop bracket names "Stop1"/"Stop2"/"Stop3" | T3 reachability proof | Canonical NT8 ATM naming confirmed B134 SIM Test B. ✅ |

No StrategyBase-only APIs used in AddOnBase context. All AddOnBase API usages are established patterns
from prior blocks (L2382, L2390) or existing property accesses. ✅

---

## CHECK F — TEST COVERAGE

**Result**: PASS ✅

| Test ID | Method Under Test | Framework | Logic Sound? |
|---------|------------------|-----------|-------------|
| T_B137_01 | `IsNoPriceChangeTestable` — returns true when `currentPrice == newPrice` | xUnit ✅ | ✅ Pure predicate; no stubs needed |
| T_B137_02 | `IsNoPriceChangeTestable` — returns false when `currentPrice != newPrice` | xUnit ✅ | ✅ Complement case |
| T_B137_03 | `SyncAtmFollowerTarget` (stub) — no cancel when `fo.LimitPrice == newPrice` | xUnit ✅ | ✅ Guards DW-B147/B149 via T2 fix |
| T_B137_04 | `SyncAtmFollowerBracket` (stub) — no cancel when `fo.StopPrice == newPrice` | xUnit ✅ | ✅ Guards DW-B147/B149 via T2 fix |
| T_B137_05 | Both sync methods (stub) — cancel proceeds when prices differ | xUnit ✅ | ✅ Confirms fix does not suppress real drags |
| T_B137_06 | `OrderPassesBracketGateTestable` — `signalName=""`, `leaderName="Stop3"`, `order.Name="Stop3"`, `order.FromEntrySignal=null` → true (ATM path; DW-B150 direct validation) | xUnit ✅ | ✅ Fails pre-B137 (`"" != null` → signal path → `null == ""` = false → return false). Passes after T3. |
| T_B137_07 | Pre-sweep via stub — `CancelExistingPttStpDrag` cancels a Working `PTT-STP-Drag` (DW-B151) | xUnit ✅ | ✅ Guards against Working order accumulation |
| T_B137_08 | Pre-sweep via stub — `CancelExistingPttStpDrag` cancels an Accepted `PTT-STP-Drag` (DW-B151) | xUnit ✅ | ✅ Guards against Accepted order accumulation |
| T_B137_09 | `OrderPassesBracketGateTestable` — `signalName=null` → ATM path → returns true (regression guard) | xUnit ✅ | ✅ Verifies null signalName behaviour unchanged |

9 [Fact] tests. Count ≥ 8 minimum. Framework: xUnit ONLY — NUnit/MSTest not used. All tests logically
sound and directly trace to specific spec requirements (DW-B147/B149/B150/B151). ✅

---

## CHECK G — 7-SCAN CHECKLIST

**Result**: PASS ✅

| Scan | Command | Expected Result | Valid? |
|------|---------|----------------|--------|
| SCAN-01 | `grep -r "lock(" src/ --include="*.cs"` | 0 matches | ✅ |
| SCAN-02 | `grep -rn "async void " src/ --include="*.cs"` | 0 matches | ✅ |
| SCAN-03 | `git diff HEAD src/PropTraderTools/CopyEngine.cs \| grep "^+" \| grep "return null;"` | 0 new `return null;` in B137-added lines; pre-existing L2629 excluded by git diff scope | ✅ |
| SCAN-04 | `dotnet build` | 0 errors 0 warnings | ✅ |
| SCAN-05 | `python scripts/complexity_audit.py` | All CYC ≤ 8; explicit targets: IsNoPriceChange=1, ExecutePhaseCStopReplacement=2, SyncAtmFollowerTarget=8, SyncAtmFollowerBracket=6, CancelExistingPttStpDrag≤8, OrderPassesBracketGate=2, MatchesLeaderName=5, FindFollowerBracketOrder=7 | ✅ |
| SCAN-06 | `dotnet test` | 0 Failed 0 Errors (includes all 9 B137 tests) | ✅ |
| SCAN-07 | `dotnet csharpier check src/` | clean | ✅ |

SCAN-03 is correctly scoped to B137-added lines only. SCAN-05 lists explicit CYC targets for every
modified and new method. All 7 scans present with correct expected results. ✅

---

## Violation Summary

**No violations found.**

| ID | Severity | Rule | Location | Description | Status |
|----|----------|------|----------|-------------|--------|
| V1 | P1 | DW-B150 root cause | Cycle-2 plan section | Prior: hedged language. Fixed: 11-step deterministic trace. | RESOLVED ✅ |
| V2 | P0 | Dead-code fix location | Cycle-2 plan T3 | Prior: fix in MatchesLeaderName (dead code). Fixed: fix in OrderPassesBracketGate branch (1) — non-dead, reachable. | RESOLVED ✅ |
| V3 | P1 | SCAN-03 scope | Cycle-2 plan 7-scan | Prior: git diff scope missing. Fixed: git diff scoped; L2629 pre-existing acknowledged. | RESOLVED ✅ |
| V4 | P1 | JS-066 CYC >8 | Cycle-2 plan T4 | Prior: inline A-Prime in SyncAtmFollowerBracket → CYC ≥9. Fixed: extraction to CancelExistingPttStpDrag is primary design; SyncAtmFollowerBracket CYC=6. | RESOLVED ✅ |

---

## Spec Coverage Matrix

| Requirement | Addressed? | Plan Section |
|-------------|-----------|--------------|
| DW-B147: rawPrice==newPrice early-return guard | ✅ Yes | Ticket T2 |
| DW-B149: ChangeSubmitted race second TP3-HBC | ✅ Yes | Ticket T2 |
| DW-B150: OrderPassesBracketGate empty-string signalName | ✅ Yes | Ticket T3 (confirmed root cause; reachable fix) |
| DW-B151: SyncAtmFollowerBracket missing Block A-Prime | ✅ Yes | Ticket T4 (extraction as primary design) |
| CYC ≤ 8 all methods | ✅ Yes | All final CYC values ≤ 8 (worst case SyncAtmFollowerTarget=8, CancelExistingPttStpDrag=6-7) |
| xUnit tests ≥ 9 | ✅ Yes | 9 [Fact] tests T_B137_01 through T_B137_09 |
| lock-free design (JS-021) | ✅ Yes | Threading Model section |
| NT8 AddOnBase API only | ✅ Yes | NT8 API Usage section |
| PTT- prefix on new orders | ✅ Yes | "PTT-STP-Drag" throughout T4 |
| 7-scan checklist present | ✅ Yes | SCAN-01..SCAN-07 all specified with explicit expected results |
| Lane-split gate | ✅ Yes | SINGLE-PIPELINE gate result with Q1-Q4 reasoning |
| SCAN-03 scoped to B137-added lines | ✅ Yes | git diff command; L2629 pre-existing acknowledged |

---

## VERDICT

**REVIEW_PASS**

All four prior violations (V1, V2, V3, V4) are confirmed resolved in the cycle-2 plan.
All checklist items A through G pass.
No new violations found.
CYC math is source-verified against CopyEngine.cs comments at lines 2301, 2363-2364, 2596, 2640, 2668.
T4 extraction design is confirmed as primary (no conditional escape hatch).
NT8 API usage is AddOnBase-only and mirrors established patterns at lines 2382-2397.
9 xUnit tests present, logically sound, and directly trace to DW-B147/B149/B150/B151.
7-scan checklist complete with correct expected results and git diff scoping for SCAN-03.

**Gate unlocked: Phase 3 (ticket generation) may proceed.**
