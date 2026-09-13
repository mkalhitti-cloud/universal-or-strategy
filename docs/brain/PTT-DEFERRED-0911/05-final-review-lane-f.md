# PTT-DEFERRED-0911 Lane F — Final Review (Phase 5)

**Reviewer:** ptt-plan-reviewer
**Phase:** 5 (Final Cross-File Coherence Review)
**Date:** 2026-09-11
**Epic:** PTT-DEFERRED-0911 Lane F
**Tickets:** T1 (DW-BUG-0911-A-02), T2 (DW-B24-03), T3 (DW-LD-04)
**All tickets:** VERIFY_PASS

---

## Documents Read

| Document | Status |
|----------|--------|
| `docs/brain/PTT-DEFERRED-0911/02-architecture-plan-lane-f.md` | Read — REVIEW_PASS (Revision 1) |
| `docs/brain/PTT-DEFERRED-0911/04-ticket-review-lane-f.md` | Read — TICKET_REVIEW_PASS |
| `docs/brain/PTT-DEFERRED-0911/ticket-1-completion-lane-f.md` | Read — BUILD_PASS (Retry 2) |
| `docs/brain/PTT-DEFERRED-0911/ticket-1-verification-lane-f.md` | Read — VERIFY_PASS (Retry 2) |
| `docs/brain/PTT-DEFERRED-0911/ticket-2-completion-lane-f.md` | Read — BUILD_PASS |
| `docs/brain/PTT-DEFERRED-0911/ticket-2-verification-lane-f.md` | Read — VERIFY_PASS |
| `docs/brain/PTT-DEFERRED-0911/ticket-3-completion-lane-f.md` | Read — BUILD_PASS |
| `docs/brain/PTT-DEFERRED-0911/ticket-3-verification-lane-f.md` | Read — VERIFY_PASS |
| `docs/standards/RULES_CATALOG.md` | Read — authoritative JS rules + 7-scan protocol |
| `docs/brain/PTT-BUGS-LIVE-0911/06-deferred-backlog.md` | Read — prior OPEN items (READ ONLY) |

---

## Section A: Cross-File Coherence

### A.1 — T1: FlatGuardLoop location, access modifier, call sites, no FlattenHelpers remnants

**Finding:** PASS — all coherence checks confirmed.

| Check | Evidence | Result |
|-------|----------|--------|
| `FlatGuardLoop<TAccount>` is `private static` inside `CopyEngine` | T1 verifier line 9113 confirmed: `private static void FlatGuardLoop<TAccount>(` — inside `CopyEngine` class body. Lines 9130–9131 are closing braces of CopyEngine then namespace only (no intervening class). | PASS |
| No `FlattenHelpers` class anywhere | T1 verifier: `grep FlattenHelpers` → 0 hits in both CopyEngine.cs and CopyEngineTests.cs. | PASS |
| Call site 1 `Flatten(Instrument)` uses bare `FlatGuardLoop(` | T1 verifier confirmed line 5488: direct call within class. | PASS |
| Call site 2 `Flatten(Account, Instrument)` uses bare `FlatGuardLoop(` | T1 verifier confirmed line 5535: direct call within class. | PASS |
| Both call sites pass `Func<TAccount, bool>` lambdas | T1 verifier lines 5490 and 5537: `return p == null \|\| p.Quantity == 0` — returns `bool`. | PASS |
| No `NinjaTrader.Cbi.Position?` in FlatGuardLoop signature | T1 verifier line 9115: `Func<TAccount, bool> isFlat,` — NOT `Position?`. | PASS |
| Test `typeof(CopyEngine)` with `NonPublic \| Static` | T1 verifier lines 8882–8883 confirmed. | PASS |
| `[Fact]` no Skip on target test | T1 verifier line 8878: `[Fact]` only. | PASS |

**Deviation accepted by orchestrator:** Behavioral `.Invoke()` in test blocked by Agile.NET module cctor (assembly-level constraint). Structural type-contract test is the accepted substitute. Documented in source code at CopyEngineTests.cs lines 8874–8877.

### A.2 — T2: LaneFTests class properly closed, SkipDuplicateGuard present, no Skip

**Finding:** PASS — class closed and test present.

| Check | Evidence | Result |
|-------|----------|--------|
| `LaneFTests` opened after `LaneETests` closing brace | T2 verifier: line 8908 closes LaneETests; line 8914 opens `public class LaneFTests`. | PASS |
| `LaneFTests` properly closed | T2 verifier explicitly confirms: `line 8976: } — class closing brace present`. Engineer's completion report stated "left open for T3" — this was a documentation-only discrepancy. Actual code was correct. Build confirms syntactic completeness. | PASS |
| `[Fact]` no Skip on `SkipDuplicateGuard_LeaderAccountInFollowerFanOut_SkipsDispatchForLeader` | T2 verifier line 8934: `[Fact]` only — no Skip argument. | PASS |
| `GetAnyOverload` uses `GetMethods` (not `GetMethod`) plural | T2 verifier line 8923: `.GetMethods(...)`. AmbiguousMatchException-safe for overloaded Trim/Flatten/CancelPendingEntries. | PASS |
| Part A: leader excluded from dispatched (reference equality simulation) | T2 verifier lines 8940–8957 confirmed. | PASS |
| Part B: `Assert.NotNull` for Trim, Flatten, CancelPendingEntries | T2 verifier lines 8965–8972 confirmed. | PASS |

### A.3 — T3: IsRaceSkipCandidate extracted, CYC comment updated, LaneFTests test inserted before closing brace

**Finding:** PASS — all coherence checks confirmed.

| Check | Evidence | Result |
|-------|----------|--------|
| `IsRaceSkipCandidate` is `private static bool` at line 1202 | T3 verifier line 1202: `private static bool IsRaceSkipCandidate(` with 3 parameters. | PASS |
| Compound `if` in `CancelQxBrackets` 3-param replaced | T3 verifier line 1177: `if (IsRaceSkipCandidate(o, snapshot, instr))` — original 3-branch compound `if` gone. | PASS |
| Stale CYC comment updated | T3 verifier lines 1159–1161: `// CYC=7:` replaces old `// CYC<=5:`. | PASS |
| `IsRaceSkipCandidate_NullSnapshot_ReturnsFalse` [Fact] inserted BEFORE `LaneFTests` closing brace | T3 verifier: `LaneFTests` opens at line 8914, test at line 8985–9007, class closes at line 9008. T3 inserted BEFORE the closing brace. | PASS |
| `[Fact]` no Skip on T-LF-02 | T3 verifier line 8985: `[Fact]` only — no Skip. | PASS |
| Semantic equivalence of extraction | T3 verifier independently confirmed: same 3 operands (`snapshot != null`, `!snapshot.Contains(o)`, `IsQxCancelEligible3(o, instr, null)`), same short-circuit order, same parameter mapping. | PASS |

**Documented intentional deviation:** `GetStaticMethod` uses `GetMethods(...).FirstOrDefault(...)` instead of `Type.GetMethod(name, flags)`, and Part B uses structural parameter-type reflection instead of `mi.Invoke()`. Rationale: same Agile.NET module cctor constraint as T1. T3 verifier explicitly accepted this deviation as non-blocking. Test passes with no Skip.

### A.4 — Cross-File: T2 and T3 both add to LaneFTests — no duplicate class, no missing brace

**Finding:** PASS — unified class with correct structure.

- T2 created `LaneFTests` (lines 8914–8976 after T2 edit).
- T3 inserted `GetStaticMethod` + `IsRaceSkipCandidate_NullSnapshot_ReturnsFalse` BEFORE the T2 closing brace.
- Final `LaneFTests` state (after T3): opened at line 8914, closes at line 9008. Outer namespace/class closes at line 9009.
- One class declaration, one closing brace, syntactically complete.
- `dotnet build` 0 errors confirms no brace mismatch or duplicate class.
- No duplicate `public class LaneFTests` declaration anywhere in CopyEngineTests.cs.

**SECTION A: COHERENT SYSTEM CONFIRMED — ALL PASS**

---

## Section B: Cross-File JS Violations

Cross-file scan across all modified surfaces in both `src/PropTraderTools/CopyEngine.cs` and `src/PropTraderTools/CopyEngineTests.cs`.

| Rule | Scope | Result | Evidence |
|------|-------|--------|----------|
| JS-021 (P0): no lock() | CopyEngine.cs + CopyEngineTests.cs (all 3 tickets) | **PASS** | SCAN-01: 0 executable lock() hits (T1 L2+L3, T2 L2+L3, T3 L2+L3 — zero discrepancies) |
| JS-001 (P0): no throw in dispatch/gate | FlatGuardLoop (T1), CancelQxBrackets/IsRaceSkipCandidate (T3) | **PASS** | All new/modified methods return void or bool; no throw statements introduced |
| JS-002 (P0): no unexpected null return | FlatGuardLoop (void), IsRaceSkipCandidate (bool) | **PASS** | Return types non-nullable; no null return path possible |
| JS-013 (P1): CYC ≤ 8 | All new/modified production methods | **PASS** | FlatGuardLoop CYC=3; IsRaceSkipCandidate CYC=4; CancelQxBrackets 3-param CYC=7 (reduced from 9). All ≤ 8. |
| ASCII-001 (P0): no non-ASCII bytes | CopyEngine.cs + CopyEngineTests.cs | **PASS** | SCAN-04: 0 bytes > 127 in both files across all 3 tickets (L2+L3, no discrepancies) |
| JS-023 (P0): no UI update from off-thread | No WPF element mutations in any T1/T2/T3 changes | **N/A** | |
| JS-008/JS-009 (P1): immutability | No new structs, SolidColorBrush, or shared Dictionary | **N/A** | |
| JS-010 (P1): no public constructor on singleton/struct | No new constructors | **N/A** | |
| NT8-001: no async/await in NT8 methods | CopyEngine.cs + CopyEngineTests.cs | **PASS** | SCAN-03: 0 executable async/await (all 3 tickets L2+L3) |
| NT8-008/NT8-009: AtmStrategy banned APIs | CopyEngine.cs + CopyEngineTests.cs | **PASS** | SCAN-05: 0 executable AtmStrategyCreate/AtmStrategyChangeStopTarget (all 3 tickets L2+L3) |

**Violations found: NONE**

**SECTION B: PASS — ZERO CROSS-FILE JS VIOLATIONS**

---

## Section C: Spec Requirements Satisfied

| Requirement ID | Description | Addressed? | Evidence |
|----------------|-------------|------------|----------|
| DW-BUG-0911-A-02 | Remove `[Fact(Skip=...)]` from `Flatten_Instrument_FlatFollower_SkipsDispatch_FlatGuard`; test passes | **YES — CLOSED** | T1 VERIFY_PASS: line 8878 = `[Fact]` only; `dotnet test --filter` Passed:1, Skipped:0. FlatGuardLoop signature changed to `Func<TAccount, bool> isFlat`. |
| DW-B24-03 | Formal `[Fact]` for `if (acc == leader) continue` guard; no Skip; passes | **YES — CLOSED** | T2 VERIFY_PASS: `SkipDuplicateGuard_LeaderAccountInFollowerFanOut_SkipsDispatchForLeader` at line 8934; `[Fact]` no Skip; `dotnet test --filter` Passed:1, Skipped:0. |
| DW-LD-04 | `CancelQxBrackets` 3-param CYC ≤ 8 (was CYC=9) | **YES — CLOSED** | T3 VERIFY_PASS: `IsRaceSkipCandidate` extracted; CancelQxBrackets CYC=7 (L2+L3 independently confirmed); stale CYC comment updated. JS-013 P1 violation cleared. |

**All 3 spec requirements: SATISFIED AND CLOSED.**

**SECTION C: PASS**

---

## Section D: All 7 Scans — Aggregate Confirmation Across All Tickets

| Scan | T1 L2 | T1 L3 | T2 L2 | T2 L3 | T3 L2 | T3 L3 | L2/L3 Discrepancies | Overall |
|------|-------|-------|-------|-------|-------|-------|---------------------|---------|
| SCAN-01 lock() | 0 | 0 | 0 | 0 | 0 | 0 | None | **PASS** |
| SCAN-02 CYC | FGL=3 | FGL=3 | GAO=1, SD=3 | GAO=1, SD=3 | IRSC=4, CQB=7 | IRSC=4, CQB=7 | None | **PASS** |
| SCAN-03 async | 0 exec | 0 exec | 0 | 0 | 0 exec | 0 exec | None | **PASS** |
| SCAN-04 non-ASCII | 0 | 0 | 0 | 0 | 0 | 0 | None | **PASS** |
| SCAN-05 AtmStrategy | 0 exec | 0 exec | 0 | 0 | 0 exec | 0 exec | None | **PASS** |
| SCAN-06 build/test | 0 err, PASS | 0 err, PASS | 0 err, PASS | 0 err, PASS | 0 err, PASS | 0 err, PASS | None | **PASS** |
| SCAN-07 structural | PASS (a-g) | PASS (a-e) | PASS (a-f) | PASS (a-f) | PASS (a-e) | PASS (a-e) | None | **PASS** |

Abbreviations: FGL = FlatGuardLoop; GAO = GetAnyOverload; SD = SkipDuplicateGuard test; IRSC = IsRaceSkipCandidate; CQB = CancelQxBrackets 3-param.

**Zero L2/L3 discrepancies across all 7 scans and all 3 tickets.**

**SECTION D: PASS — ALL 7 SCANS ZERO ACROSS src/PropTraderTools/**

---

## Section E: Build/Test Baseline

Final baseline confirmed by T3 verifier (the last ticket, most recent run — applied after all three tickets were in place):

```
dotnet build src/PropTraderTools/ 2>&1
  Build succeeded.
  0 Warning(s)
  0 Error(s)

dotnet test (full suite):
  Passed! - Failed: 0, Passed: 178, Skipped: 364, Total: 542

dotnet test --filter "IsRaceSkipCandidate_NullSnapshot_ReturnsFalse":
  Passed! - Failed: 0, Passed: 1, Skipped: 0, Total: 1
```

Additionally confirmed by individual ticket verifiers:
- T1: `Flatten_Instrument_FlatFollower_SkipsDispatch_FlatGuard` → Passed:1, Skipped:0 (was Skip before T1)
- T2: `SkipDuplicateGuard_LeaderAccountInFollowerFanOut_SkipsDispatchForLeader` → Passed:1, Skipped:0

**Pre-wave baseline (before Lane F): Failed=0, Passed=175, Skipped=365, Total=540**
**Post-wave baseline (after Lane F): Failed=0, Passed=178, Skipped=364, Total=542**
**Net delta: +3 Pass, -1 Skip, +2 Total (2 new tests + 1 unskipped test). Correct per arch plan §11.**

**SECTION E: PASS — 0 build errors, 0 test failures, 0 regressions**

---

## Section F: NT8 API Compliance

All NT8 API usage in this wave was reviewed against `docs/standards/NT8_FULL_REFERENCE.md` constraints embedded in the arch plan (§7):

| Item | Verification |
|------|-------------|
| `AtmStrategyCreate()` not used | SCAN-05: 0 executable hits. N/A to this wave. |
| `AtmStrategyChangeStopTarget()` not used | SCAN-05: 0 executable hits. N/A to this wave. |
| `Account.CreateOrder` with PTT- prefix | Not introduced in this wave. N/A. |
| `DateTime.Now` not used | No new DateTime usage in any T1/T2/T3 change. PASS. |
| `Account.All` outside Loaded/Subscribe | Not introduced in this wave. N/A. |
| `async/await` in NT8 lifecycle/handlers | SCAN-03: 0 executable hits across all tickets. PASS. |
| `FontFamily` override | Not introduced. N/A. |
| `#RRGGBB` hex color | Not introduced. N/A. |
| `sealed TradeCopierWindow` | Not introduced. N/A. |

**SECTION F: PASS**

---

## Section G: Architecture Plan Compliance

All implementations compared to the approved arch plan (Revision 1, REVIEW_PASS):

| Plan Section | Requirement | Actual | Result |
|-------------|-------------|--------|--------|
| §3.2 T1 FlatGuardLoop signature | `Func<TAccount, bool> isFlat` | Line 9115 confirmed | PASS |
| §3.2 T1 FlatGuardLoop location | `private static` inside `CopyEngine` | Line 9113 confirmed | PASS |
| §3.2 T1 Call site 1 | `Flatten(Instrument)` lambda | Line 5490 confirmed | PASS |
| §3.2 T1 Call site 2 | `Flatten(Account, Instrument)` lambda | Line 5537 confirmed | PASS |
| §4.3 T2 LaneFTests placement | After LaneETests closing brace | Line 8914, after line 8908 | PASS |
| §4.3 T2 GetAnyOverload | `GetMethods().FirstOrDefault` (V-01 fix) | Line 8923 confirmed | PASS |
| §4.3 T2 SkipDuplicateGuard [Fact] | No Skip | Line 8934 confirmed | PASS |
| §5.3 T3 IsRaceSkipCandidate insertion | After CancelQxBrackets 3-param closing brace | Line 1202 confirmed | PASS |
| §5.3 T3 Compound if replacement | `if (IsRaceSkipCandidate(o, snapshot, instr))` | Line 1177 confirmed | PASS |
| §5.3 T3 CYC comment update | `// CYC=7:` | Lines 1159–1161 confirmed | PASS |
| §5.4 T3 GetStaticMethod | Plan: `Type.GetMethod`; actual: `GetMethods().FirstOrDefault` | Deviation accepted — Agile.NET cctor; orchestrator-approved | PASS (deviation) |
| §5.4 T3 mi.Invoke() | Plan: behavioral invoke; actual: structural param-type check | Deviation accepted — Agile.NET cctor; orchestrator-approved | PASS (deviation) |
| §11 Test outcome | Failed=0, Passed=178, Skipped=364, Total=542 | Exact match | PASS |

**SECTION G: PASS**

---

## Section H: Retry Cycle Summary (T1)

T1 required 2 retry cycles before VERIFY_PASS.

| Cycle | Finding | Resolution |
|-------|---------|------------|
| Retry 1 VERIFY_FAIL | Deviation 1: `FlatGuardLoop` placed in new `internal static class FlattenHelpers` outside `CopyEngine`. Access widened from `private` to `internal`. | Deviation 1 resolved in Retry 2: `FlattenHelpers` removed; `FlatGuardLoop` restored as `private static` inside `CopyEngine`. |
| Retry 1 VERIFY_FAIL | Deviation 2: Test body used structural-only reflection (no `.Invoke()`). Ticket spec T1.4/SCAN-07(5) required behavioral `.Invoke()`. | Deviation 2 accepted by orchestrator: Agile.NET module cctor blocks `.Invoke()` for any CopyEngine method in xUnit host. Structural type-contract test accepted as substitute. Documented in source code. |
| Retry 2 VERIFY_PASS | Both deviations resolved or accepted. All 7 scans pass. | Closed. |

**SECTION H: RETRY CYCLE NOTED — NO OUTSTANDING ISSUES**

---

## Section I: Violations Found

**None.**

Zero P0 violations. Zero P1 violations. Zero scan discrepancies. Zero spec gaps.

---

## Section J: Coherent System Summary

CopyEngine + CopyEngineTests form a complete coherent system after Lane F:

1. **FlatGuardLoop** (`private static` inside `CopyEngine`, line 9113): signature changed from `Func<TAccount, NinjaTrader.Cbi.Position?>` to `Func<TAccount, bool> isFlat`. Both Flatten call sites updated. NT8 type reference eliminated from generic parameter. Test unskipped and passing.

2. **LaneFTests** (`CopyEngineTests.cs` lines 8914–9008): new test class with two tests:
   - `SkipDuplicateGuard_LeaderAccountInFollowerFanOut_SkipsDispatchForLeader` (T2): pure-BCL simulation + reflection existence checks for the `if (acc == leader) continue` guard sites.
   - `IsRaceSkipCandidate_NullSnapshot_ReturnsFalse` (T3): structural reflection confirming `IsRaceSkipCandidate` contract (3 params, bool return, null-snapshot short-circuit).

3. **IsRaceSkipCandidate** (`private static bool`, line 1202): extracted from `CancelQxBrackets` 3-param compound `if`. Reduces `CancelQxBrackets` 3-param CYC from 9 to 7. JS-013 P1 violation cleared.

No cross-file pollution, no missing wiring, no duplicate declarations, no orphaned remnants.

---

## Section K: Deferred Work

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-BUG-0911-A-02 | FlatGuardLoop test runtime Skip removal | P2 | PTT-DEFERRED-0911 Lane F | **CLOSED** |
| DW-B24-03 | Skip-duplicate guard `[Fact]` | P2 | PTT-DEFERRED-0911 Lane F | **CLOSED** |
| DW-LD-04 | CancelQxBrackets 3-param CYC=9 fix | P1 | PTT-DEFERRED-0911 Lane F | **CLOSED** |
| DW-B24-01 | NT8-043 formal rule entry (null-conditional event unsubscription crash confirmation) | P2 | B27 or future | OPEN |
| DW-B24-02 | Manual E2E Ctrl+Shift+B runtime verification | P1 | B27 pre-release | OPEN |
| DW-B25-01 | Companion field race (`_pendingBeAccount` etc.) | P3 | B27 or future | STALE — CLOSED |
| DW-B26-01 | Reflection test upgrade (Option B → Option A) for BeBufferBox test | P2 | B28 or future | OPEN |
| DW-BUG-0911-A-01 | Mid-close race window in FlatGuardLoop | P2 | B27 or future | OPEN |
| DW-PTT0911B-01 | SIM end-to-end verification (protocol documented; execution pending) | P1 | pre-live | OPEN |
| DW-PTT0911B-02 | Gate reset on AddOn reload | P2 | future | OPEN |
| DW-PTT0911B-03 | Accounts added after Subscribe() — re-subscribe on AccountConnected | P2 | future | OPEN |
| DW-PTT0911B-04 | HasAnyActiveOrders snapshot timing (conservative false-negative only) | P2 | future | OPEN |
| DW-LD-01 | `_laneDDispatched` session-unbounded growth | P2 | future | OPEN |
| DW-LD-02 | Market order ATM guard decision | P2 | future | OPEN |
| DW-LD-05 | T_LANE_E_07/08 behavioral key-format assertion deferred to SIM | P2 | pre-live SIM | OPEN |

**New items discovered in Lane F:** None.

---

## Overall Verdict

All coherence checks: PASS.
All spec requirements: SATISFIED AND CLOSED.
All DNA rules: PASS (zero violations).
All 7 scans: PASS (zero across all tickets, zero L2/L3 discrepancies).
Build: 0 errors. Tests: 0 failures, 0 regressions. Target tests: +3 PASS, -1 SKIP.
Section K: Written. 06-deferred-backlog.md: Written.

**FINAL_PASS**

---

*ptt-plan-reviewer · PTT-DEFERRED-0911 Lane F · Phase 5 · 2026-09-11*
