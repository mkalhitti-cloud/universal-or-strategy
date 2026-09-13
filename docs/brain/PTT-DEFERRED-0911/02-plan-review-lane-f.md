# PTT-DEFERRED-0911 Lane F — Plan Review (Cycle 2)

**Epic:** PTT-DEFERRED-0911 Lane F
**Phase:** 2 (Plan Review) — Revision 2 (post REVIEW_FAIL)
**Date:** 2026-09-11
**Reviewer:** ptt-plan-reviewer
**Plan reviewed:** docs/brain/PTT-DEFERRED-0911/02-architecture-plan-lane-f.md (Revision 1)
**Prior review:** docs/brain/PTT-DEFERRED-0911/02-plan-review-lane-f.md (Cycle 1 — REVIEW_FAIL, 2 violations)

---

## VERDICT: REVIEW_PASS

**Violations found: 0**

Both prior violations (V-01 and V-02) have been resolved in Revision 1 of the plan.
No new violations detected. All checks pass. Proceed to Phase 3 (ticket generation).

---

## Prior Violation Resolution

| # | Prior Severity | Rule | Status | Evidence |
|---|---------------|------|--------|---------|
| V-01 | P0 — SPEC COMPLETENESS | T2 `GetMethod` ambiguity on overloaded methods | **RESOLVED** | `GetAnyOverload` helper in §4.3 uses `GetMethods(flags).FirstOrDefault(m => m.Name == name)` — cannot throw `AmbiguousMatchException`. Returns `null` on miss; `Assert.NotNull` catches absence. Plan §9 T2 Risks confirms the fix rationale. |
| V-02 | P1 — 7-SCAN PROTOCOL | SCAN-03, SCAN-05, SCAN-06 absent from all ticket checklists | **RESOLVED** | §3.6, §4.6, §5.7 each now carry the canonical SCAN-01 through SCAN-07 exactly as defined in RULES_CATALOG Section 4. All seven scan definitions match the authoritative table. |

---

## Section A — LANE-SPLIT GATE Compliance

**PASS.**

| Check | Result |
|-------|--------|
| Gate result present? | YES — `LANE-SPLIT GATE RESULT: LANES-APPROVED` explicitly stated (§1) |
| Q1 = NO (same method / within 50 lines)? | CORRECT — NO. T1 (FlatGuardLoop ~5497), T2 (LaneFTests ~9000+), T3 (CancelQxBrackets ~1162) — no pair within 50 lines. |
| Q2 = NO (design coupling)? | CORRECT — NO. T1 delegate-type change, T2 new test, T3 extraction — zero design coupling. |
| Q3 = YES (standalone value if other blocked)? | CORRECT — YES. T1 unblocks skipped test; T2 adds leader-guard behavioral coverage; T3 fixes P1 CYC violation. Each independently improves the codebase. |
| Q4 = YES (independent SIM verification)? | CORRECT — YES. T1: `dotnet test` passes FlatGuardLoop test with no Skip. T2: `dotnet test` passes SkipDuplicateGuard test. T3: `dotnet test` + McCabe recount confirms CYC=7. |

All four gate conditions correctly evaluated. Format compliant. ✓

---

## Section B — T1 Review: NT8 Type Elimination + Semantics + Callers

**PASS.**

| Check | Result | Evidence |
|-------|--------|---------|
| NT8 type removed from `FlatGuardLoop` signature | YES | `Func<TAccount, NinjaTrader.Cbi.Position?>` → `Func<TAccount, bool> isFlat` (§3.1, §3.2 A) |
| `pos == null \|\| pos.Quantity == 0` semantics preserved | YES | Both callers: `acc => { var p = FindPosition(acc, instrument); return p == null \|\| p.Quantity == 0; }` (§3.2 B, C) |
| Zero caller behavior change | YES | Callers' external signatures unchanged; only lambda argument updated (§3.4) |
| JIT blocker root cause addressed | YES | Test delegate `Func<object, NinjaTrader.Cbi.Position?>` → `Func<object, bool>`. NT8 type not present in xUnit host (§3.3). Blocker root cause per §2.2 confirmed: delegate type declaration triggered `TypeInitializationException`. After T1 the delegate carries only BCL types. |
| `[Fact(Skip=...)]` removed | YES | Test becomes `[Fact]` (§3.3) |
| `FlatGuardLoop` CYC post-change | CYC=3: base(1)+foreach(1)+if(isFlat)(1). JS-013 satisfied (3 ≤ 8). ✓ |
| `Single(m => m.Name == "FlatGuardLoop" ...)` ambiguity risk | NONE — `FlatGuardLoop` has exactly one overload; `Single` is correct and will not throw. ✓ |
| Risk: 3rd call site | Correctly identified; engineer mitigation prescribed (`grep -r "FlatGuardLoop" src/` before patching) (§9 T1 Risks) |

---

## Section C — T2 Review: [Fact] No-Skip + Reflection Pattern + Guard Verification + V-01

**PASS.**

| Check | Result | Evidence |
|-------|--------|---------|
| `[Fact]` with no Skip | YES | `[Fact] public void SkipDuplicateGuard_LeaderAccountInFollowerFanOut_SkipsDispatchForLeader()` (§4.3) |
| No NT8 types instantiated at runtime | YES | `object` tokens, `List<object>`, pure BCL throughout (§4.3) |
| `acc == leader` guard semantics verified (Part A) | YES | Reference-equality simulation: 3 accounts, leader skipped, dispatched.Count=2, leader excluded, follower1/follower2 present ✓ |
| Structural reflection existence check (Part B) | YES | `GetAnyOverload` confirms Trim, Flatten, CancelPendingEntries all exist. `Assert.NotNull` will fail test clearly if any method is missing. |
| V-01 resolved — `AmbiguousMatchException` eliminated | YES | `GetAnyOverload` uses `GetMethods(BindingFlags.NonPublic \| BindingFlags.Instance \| BindingFlags.Static).FirstOrDefault(m => m.Name == name)`. Returns array first; `FirstOrDefault` selects without ambiguity resolution. Cannot throw. ✓ |
| `BindingFlags` coverage — includes Static flag | YES | `BindingFlags.NonPublic \| BindingFlags.Instance \| BindingFlags.Static` — covers both instance and static methods, ensuring all overloads (regardless of static/instance) are found. ✓ |
| Zero production code changes | YES (§4.2) |
| `GetAnyOverload` CYC | CYC=1 (pure expression body, no branches). ✓ |

---

## Section D — T3 Review: McCabe Count + Fix Validity

**PASS.**

| Check | Result | Evidence |
|-------|--------|---------|
| Independent McCabe count performed and documented | YES | Full line-by-line decision-point trace in §5.1 and §2.4 (both reach CYC=9 independently) |
| Reviewer independent recount | base(1)+\|\|(2 for `acc==null\|\|instr==null`)+foreach(1)+snapshot!=null(1)+!Contains(1)+IsQxCancelEligible3-race(1)+!IsQxCancelEligible3-stale(1)+stale.Count==0(1) = **9**. Confirms plan. ✓ |
| Existing `// CYC<=5` comment identified as wrong | YES (§5.1, §5.3 C); stale comment replaced ✓ |
| Extraction target: `IsRaceSkipCandidate` | YES (§5.2). Absorbs the 3-branch `&&` chain (snapshot!=null, !Contains, IsQxCancelEligible3 with null snapshot) ✓ |
| `CancelQxBrackets` 3-param CYC post-extraction | CYC=7: base(1)+\|\|(2)+foreach(1)+IsRaceSkipCandidate(1)+!IsQxCancelEligible3(1)+stale.Count(1)=7. JS-013 satisfied (7 ≤ 8). ✓ |
| `IsRaceSkipCandidate` CYC | CYC=4: base(1)+snapshot!=null(1)+!Contains(1)+IsQxCancelEligible3(1)=4. JS-013 satisfied (4 ≤ 8). ✓ |
| `GetStaticMethod` in T3 — no ambiguity risk | YES — `IsRaceSkipCandidate` has exactly one overload; `GetMethod(name, NonPublic\|Static)` will not throw. Plan §5.4 note confirms this. ✓ |
| Null-snapshot short-circuit test valid | YES — snapshot==null fires first `&&` operand, returns false immediately; `o` and `instr` null args are never dereferenced; no NRE risk (§9 T3 Risks) ✓ |
| `mi.GetParameters().Length` — NT8 type instantiation risk | NONE — `GetParameters()` is metadata access, not runtime instantiation. Only `.Length` is checked; individual `ParameterType` fields are not accessed. No NT8 type constructed in xUnit host. ✓ |
| CYC comment update in `CancelQxBrackets` planned | YES — stale `// CYC<=5` replaced with `// CYC=7` with full breakdown (§5.3 C) ✓ |
| IsRaceSkipCandidate dependency on IsQxCancelEligible3 | Noted in §9 T3 Risks; mitigation: engineer verifies `IsQxCancelEligible3` existence via grep before implementing ✓ |

---

## Section E — JS Rule Compliance

| Rule | T1 | T2 | T3 | Status |
|------|----|----|----|--------|
| JS-021 (no lock) | `FlatGuardLoop` static, pure iteration, no state | Test-local `List<object>`, single-threaded | `IsRaceSkipCandidate` static pure predicate, no state | **PASS** all tickets |
| JS-001 (no throw in handlers/dispatch) | `FlatGuardLoop` uses `continue` and direct call; no throw | Test not in dispatch path; `Assert` calls are expected | `IsRaceSkipCandidate` returns `bool`, no throw | **PASS** all tickets |
| JS-013 (CYC ≤ 8) | `FlatGuardLoop` CYC=3 ✓ | `GetAnyOverload` CYC=1; test method CYC≈4 (informational) ✓ | `IsRaceSkipCandidate` CYC=4; `CancelQxBrackets` CYC=7 ✓ | **PASS** all tickets |
| JS-002 (no unexpected null return) | `FlatGuardLoop` is `void` — N/A | `GetAnyOverload` returns `MethodInfo?` (nullable; null = method not found, handled by `Assert.NotNull`) ✓ | `IsRaceSkipCandidate` returns `bool` (value type) ✓ | **PASS** |
| JS-003 (no magic string for state) | No string discriminators introduced | No string discriminators introduced | No string discriminators introduced | **PASS** |
| ASCII-001 | Stated; SCAN-04 enforces | Stated; SCAN-04 enforces | Stated; SCAN-04 enforces | **PASS** (stated; enforcement at Ph4b) |

---

## Section F — 7-Scan Protocol Compliance (V-02 Resolution)

**PASS.**

All three ticket checklists (§3.6, §4.6, §5.7) now carry the canonical SCAN-01 through SCAN-07 as defined in RULES_CATALOG Section 4.

| SCAN # (RULES_CATALOG) | Definition | T1 §3.6 | T2 §4.6 | T3 §5.7 |
|------------------------|-----------|---------|---------|---------|
| SCAN-01 | `lock()` grep | ✓ matches | ✓ matches | ✓ matches |
| SCAN-02 | Manual McCabe count | ✓ FlatGuardLoop CYC=3 | ✓ GetAnyOverload CYC=1, test CYC≈4 | ✓ IsRaceSkipCandidate CYC=4, CancelQxBrackets CYC=7 |
| SCAN-03 | `async`/`await` grep | ✓ present | ✓ present | ✓ present |
| SCAN-04 | PowerShell byte scan (non-ASCII) | ✓ present | ✓ present | ✓ present |
| SCAN-05 | `AtmStrategyCreate`/`ChangeStopTarget` grep | ✓ present | ✓ present | ✓ present |
| SCAN-06 | `dotnet build` + `dotnet test` | ✓ present; Skip→PASS stated | ✓ present; PASS stated | ✓ present; PASS stated |
| SCAN-07 | Structural line-number verification | ✓ present | ✓ present | ✓ present |

V-02 is fully resolved. No mismatches against RULES_CATALOG Section 4 authoritative definitions.

---

## Section G — NT8 API Facts

**PASS.** Unchanged from Cycle 1.

| NT8 API | Plan treatment | Status |
|---------|---------------|--------|
| `AtmStrategyCreate()` | StrategyBase-only; not used in this wave (§7) | ✓ |
| `AtmStrategyChangeStopTarget()` | StrategyBase-only; not used in this wave (§7) | ✓ |
| `Account.Cancel()` | AddOnBase available; used in `CommitStaleCancelBatch` (called by T3-modified method, not changed) | ✓ |
| `NinjaTrader.Cbi.Position.Quantity` | After T1, accessed inside callers' lambdas (NT8 process only, not in xUnit host) | ✓ |
| `NinjaTrader.Cbi.Order`, `NinjaTrader.Cbi.Instrument` | T3 `IsRaceSkipCandidate` uses as parameter types (production method). T3 test passes `null` for both — metadata access only in reflection, no instantiation | ✓ |

No banned NT8 APIs (NT8-008, NT8-009) introduced in any ticket.

---

## Section H — No New NT8 Types Instantiated in Tests

**PASS.**

| Ticket | Test delegates / locals | NT8 instantiation? |
|--------|------------------------|--------------------|
| T1 | `Func<IEnumerable<object>>`, `Func<object, bool>`, `Action<object>`, `object` token | NONE |
| T2 | `object` leader/follower tokens, `List<object> dispatched` | NONE |
| T3 | `null` passed for Order and Instrument params; `mi.GetParameters().Length` — metadata only | NONE |

---

## Section I — Spec Coverage Matrix

| Requirement | Addressed? | Plan section |
|-------------|-----------|-------------|
| T1: Remove `[Fact(Skip=...)]` from `Flatten_Instrument_FlatFollower_SkipsDispatch_FlatGuard` | YES | §3.3 |
| T1: Fix JIT blocker — remove `Func<TAccount, NinjaTrader.Cbi.Position?>` from delegate | YES | §3.1–3.2 |
| T1: Preserve `pos==null\|\|pos.Quantity==0` semantics | YES | §3.2 B, C |
| T1: Zero behavior change to callers | YES | §3.4 |
| T2: Add `[Fact]` (no Skip) `SkipDuplicateGuard_LeaderAccountInFollowerFanOut_SkipsDispatchForLeader` | YES (V-01 resolved) | §4.3 |
| T2: Use reflection/delegate injection — no NT8 runtime, no Skip | YES (V-01 resolved) | §4.3 Parts A + B |
| T2: Verify `if(acc==leader)continue` guard at listed sites | YES (behavioral simulation; existence check) | §4.3, §2.3 |
| T3: Architect independently counts McCabe for `CancelQxBrackets` 3-param | YES | §5.1 (CYC=9 confirmed) |
| T3: Count documented in plan | YES | §5.1 |
| T3: Fix valid — extract helper to CYC≤8 | YES | §5.2–5.3 (IsRaceSkipCandidate CYC=4; CancelQxBrackets CYC=7) |
| All JS rules respected (JS-021, JS-001, JS-013) | YES | §3.5, §4.5, §5.6 |
| 7-scan checklist present per ticket (V-02) | YES (V-02 resolved) | §3.6, §4.6, §5.7 |
| ASCII-only compliance stated | YES | Per-ticket JS tables + SCAN-04 entries |
| No new NT8 types instantiated in tests | YES | §3.3, §4.3, §5.4 |

---

## Section J — Summary

| Area | Cycle 1 | Cycle 2 |
|------|---------|---------|
| Lane-split gate | PASS | **PASS** |
| T1 — NT8 type elimination + semantics + callers | PASS | **PASS** |
| T2 — [Fact] no-Skip + reflection + guard + V-01 | FAIL (V-01) | **PASS** |
| T3 — McCabe count + extraction validity | PASS | **PASS** |
| JS-021, JS-001, JS-013 | PASS | **PASS** |
| ASCII-001 | PASS | **PASS** |
| 7-scan protocol compliance (V-02) | FAIL (V-02) | **PASS** |
| NT8 API facts | PASS | **PASS** |
| No banned NT8 APIs introduced | PASS | **PASS** |
| No new NT8 types in tests | PASS | **PASS** |

**REVIEW_PASS. Zero violations. Proceed to Phase 3 (ticket generation).**

---

*ptt-plan-reviewer · PTT-DEFERRED-0911 Lane F · 2026-09-11 · Cycle 2*
