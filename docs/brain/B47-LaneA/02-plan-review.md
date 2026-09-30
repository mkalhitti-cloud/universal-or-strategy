# B47-LaneA — Plan Review (Cycle 2)
**Phase**: 2 (Plan Review — Cycle 2 of 2 maximum)
**Reviewer**: ptt-plan-reviewer
**Date**: 2026-08-08
**Plan file reviewed**: `docs/brain/B47-LaneA/02-architecture-plan.md` (Revision 1 — CYC fix)
**Spec anchor**: `specs/002-trade-copier-spec.html#dw-b47-be-follower-scope`
**Rules catalog**: `docs/standards/jane-street/RULES_CATALOG.md`
**NT8 rules**: `docs/standards/NT8_COMPILER_RULES.md`
**Source verified**: `src/PropTraderTools/Features/PttBreakEven.cs`, `Features/PttGlobalQuickExit.cs`, `CopyEngine.cs`

---

## VERDICT: REVIEW_PASS

**Violations found**: 0
**Cycle 1 blocker (V-01 CYC undercount)**: RESOLVED — `BuildBeRejectMsg` extraction correctly absorbs the two `!priceOk` ternaries; `ExecuteOneAccount` CYC confirmed ≤ 8.

---

## CYC Verification — Independent Source Audit

All five affected methods verified against actual source at
`c:\WSGTA\universal-or-strategy\src\PropTraderTools\`.

### 1. `CopyEngine.IsFollowerAccount` (new) — CYC = 4

Plan claims **CYC = 4**. Verified.

| Branch | Delta | Running |
|--------|-------|---------|
| Base | +1 | 1 |
| `foreach (CopyRule r in _rules)` | +1 | 2 |
| `if (r.FollowerAccounts == null) continue` | +1 | 3 |
| `if (Array.IndexOf(r.FollowerAccounts, a) >= 0)` | +1 | 4 |

**CYC = 4** ✓ (≤ 8)

### 2. `CopyEngine.ArmAllPendingBe` — CYC before = 5, after guard = 6

Plan claims **CYC = 6 after guard**. Verified against source lines 2107–2132.

Source comment "CYC=5" confirmed: base(1) + `foreach Account.All`(+1) + `foreach acc.Positions`(+1) + `if IsFlat`(+1) + `if IsPriceAlreadyAtBe`(+1) = 5.
Adding `if (IsFollowerAccount(acc)) continue`: +1.

**CYC = 6** ✓ (≤ 8)

### 3. `PttGlobalQuickExit.Execute` — CYC before = 3–5 (counting convention), after ≤ 7

Plan claims **CYC = 5 after guard**. Verified against source lines 25–36.

Source header comment says CYC=3 (acc loop, pos loop, null/flat continue). Under strict Lizard (counts `||` in guard): CYC=5. After adding `if (engine != null && engine.IsFollowerAccount(acc)) continue` (+1 if, +1 &&): plan's CYC=5, strict CYC=7.
**Both ≤ 8** ✓

### 4. `PttBreakEven.Execute` — CYC before = 14, after extraction = 7

Plan claims **CYC = 7 after extraction**. Verified against actual source lines 66–123.

True baseline CYC = 14 (confirmed cycle 1 and in plan's §4c table). After extracting:
- Loop body → `ExecuteOneAccount`
- `leaderIsLong` ternary + bus raise → `RaiseBeNotify`
- Follower guard (`if (engine != null && engine.IsFollowerAccount(acc)) continue`)

Remaining in `Execute()`:

| Branch | Delta | Running |
|--------|-------|---------|
| Base | +1 | 1 |
| `if (!IsEnabled) return` | +1 | 2 |
| `if (leaderPos == null ` | +1 | 3 |
| `\|\|` in leader null guard | +1 | 4 |
| `foreach (Account acc in ctx.AllAccounts)` | +1 | 5 |
| B47 guard `if (engine != null && ...)` | +1 | 6 |
| `&&` in guard | +1 | 7 |

**CYC = 7** ✓ (≤ 8)

### 5. `PttBreakEven.ExecuteOneAccount` (new) — CYC = 7 (plan) or 8 (strict)

Plan claims **CYC = 7**. Verified.

The plan's docblock correctly notes the two `!priceOk` ternaries (lines 97–98) are **delegated to `BuildBeRejectMsg`** and do NOT add CYC here.

| Branch | Delta | Running |
|--------|-------|---------|
| Base | +1 | 1 |
| `if (pos == null ` | +1 | 2 |
| `\|\|` in pos null guard | +1 | 3 |
| `isLong ? +buf : -buf` ternary | +1 | 4 |
| `isLong ? (ask <= 0.0 \|\| ...) : (...)` outer ternary | +1 | 5 |
| `\|\|` in ternary branch (true branch) | +1 | 6 |
| `if (!priceOk)` | +1 | 7 |

Plan's count: **CYC = 7**. Strict Lizard (counting `||` in both ternary branches): 8.
**Both ≤ 8** ✓

### 6. `PttBreakEven.BuildBeRejectMsg` (new) — CYC = 3

Plan claims **CYC = 3**. Verified.

| Branch | Delta | Running |
|--------|-------|---------|
| Base | +1 | 1 |
| `isLong ? "above ask" : "below bid"` ternary | +1 | 2 |
| `isLong ? ask.ToString("F2") : bid.ToString("F2")` ternary | +1 | 3 |

**CYC = 3** ✓ (≤ 8)

### 7. `PttBreakEven.RaiseBeNotify` (new) — CYC = 2

Plan claims **CYC = 2**. Verified.

| Branch | Delta | Running |
|--------|-------|---------|
| Base | +1 | 1 |
| `leaderIsLong ? +buf : -buf` ternary | +1 | 2 |

**CYC = 2** ✓ (≤ 8)

---

## CYC Summary Table

| Method | File | CYC Before | CYC After | Status |
|--------|------|-----------|-----------|--------|
| `CopyEngine.IsFollowerAccount` | CopyEngine.cs | N/A (new) | 4 | ✓ NEW |
| `CopyEngine.ArmAllPendingBe` | CopyEngine.cs | 5 | 6 | ✓ |
| `PttGlobalQuickExit.Execute` | Features/PttGlobalQuickExit.cs | 3–5 | 5–7 | ✓ (worst-case 7) |
| `PttBreakEven.Execute` | Features/PttBreakEven.cs | 14 | 7 | ✓ |
| `PttBreakEven.ExecuteOneAccount` | Features/PttBreakEven.cs | N/A (new) | 7–8 | ✓ (worst-case 8) |
| `PttBreakEven.BuildBeRejectMsg` | Features/PttBreakEven.cs | N/A (new) | 3 | ✓ NEW |
| `PttBreakEven.RaiseBeNotify` | Features/PttBreakEven.cs | N/A (new) | 2 | ✓ NEW |
| `PttGlobalBreakEven.Execute(int)` | Features/PttGlobalBreakEven.cs | 1 | 1 | ✓ NO CHANGE |

**All methods ≤ 8.** ✓

---

## Cycle 1 Violation Re-Check

| # | Prior Violation | Resolution in Revision 1 | Cycle 2 Status |
|---|-----------------|--------------------------|----------------|
| V-01 | `ExecuteOneAccount` CYC = 9; two `!priceOk` ternaries (lines 97–98) not extracted | `BuildBeRejectMsg` static helper extracts both ternaries. `ExecuteOneAccount` CYC = 7 (plan) or 8 (strict). Both ≤ 8. | **RESOLVED** ✓ |

---

## Jane Street DNA Rule Audit

| Rule | Check | Result |
|------|-------|--------|
| **JS-021** no `lock()` | None of the 4 new/modified methods use `lock`. All guards are `if`-based. `ConcurrentBag<CopyRule>` iteration is lock-free by design. | ✓ PASS |
| **JS-001** no `throw` in hot path | No `throw new XxxException` in any new or modified method. Existing `try/catch` blocks in `SubmitBeStopLocal` / `SubmitBeTargetsLocal` are pre-existing. | ✓ PASS |
| **JS-002** no `return null` | `IsFollowerAccount` returns `bool`. `BuildBeRejectMsg` returns `string` (never null — string concatenation always produces a non-null string). `ExecuteOneAccount` and `RaiseBeNotify` return `void`. | ✓ PASS |
| **JS-033** no `async void` | All new and modified methods are synchronous `void` or `bool` or `string`. | ✓ PASS |
| **CYC ≤ 8** | All methods verified above. | ✓ PASS |

---

## NT8 Rule Audit (modified files only)

| Rule | Check | Result |
|------|-------|--------|
| NT8-006 no LINQ | `IsFollowerAccount` uses `foreach` + `Array.IndexOf`; no `.Any()`, `.Contains()`, or LINQ extension methods. | ✓ PASS |
| NT8-003 no `volatile double` | No new `volatile double` fields. | ✓ PASS |
| NT8-014 PTT- prefix | No new `CreateOrder` calls. Existing signal names unchanged. | ✓ N/A |
| NT8-021 `Account.All` in constructor | `ArmAllPendingBe` and `PttGlobalQuickExit.Execute` are called from UI button handlers post-init; no constructor access. | ✓ PASS |
| NT8-013 `DateTime.Now` | No new `CreateOrder` calls. No `DateTime.Now` usage. | ✓ N/A |
| NT8-019 `async void` | None. | ✓ PASS |

---

## Spec Coverage Matrix

| Requirement | Addressed? | Plan Section |
|-------------|-----------|--------------|
| DW-B47-BE-FOLLOWER-SCOPE: follower accounts skipped in BE ALL path | YES — `ArmAllPendingBe` guard at CopyEngine.cs:2113 | §4a |
| DW-B47-BE-FOLLOWER-SCOPE: follower accounts skipped in Quick ALL path | YES — `PttGlobalQuickExit.Execute` guard | §4b |
| DW-B47-BE-FOLLOWER-SCOPE: follower accounts skipped in BE button (single) path | YES — `PttBreakEven.Execute` guard + `ExecuteOneAccount` extraction | §4c |
| `PttGlobalBreakEven.Execute(int)` correctly identified as no-change delegate | YES — §2 D6, §6 no-change table | §2, §6 |
| `IsFollowerAccount` predicate added to `CopyEngine` | YES — `internal bool`, placed after `FindRule` at line 1389 | §3 |
| NT8-safe iteration (no LINQ) | YES — `foreach` + `Array.IndexOf` in `IsFollowerAccount` | §3, §7 |
| All modified method CYC ≤ 8 | YES — verified above | §4, §5 |
| JS-021 no `lock()` | YES | §7 |
| JS-001 no `throw` in hot path | YES | §7 |
| JS-002 no `return null` (new methods) | YES | §7 |
| Thread-safety analysis present | YES — §8 |
| 7-scan checklist present and complete | YES — §12 |
| Exact line numbers provided and verified against source | YES | §4a, §4b, §4c |
| Scope minimal (3 files only) | YES | §6 |

---

## 7-Scan Checklist Verification

| Scan | Check | Status |
|------|-------|--------|
| SCAN-01 | No `lock(` in any modified or new method | ✓ CONFIRMED |
| SCAN-02 | No `throw new XxxException` in any hot path | ✓ CONFIRMED |
| SCAN-03 | No `return null` from any new method returning a value type | ✓ CONFIRMED |
| SCAN-04 | No LINQ on hot path — `IsFollowerAccount` uses `foreach` + `Array.IndexOf` | ✓ CONFIRMED |
| SCAN-05 | All new method identifiers ASCII-only, no FontFamily override, no hex colour literals | ✓ CONFIRMED |
| SCAN-06 | All CYC counts ≤ 8 (see table above) | ✓ CONFIRMED |
| SCAN-07 | All `CreateOrder` signal names start with "PTT-" — no new `CreateOrder` calls in B47-LaneA scope | ✓ CONFIRMED (N/A) |

All 7 scans present and addressed. ✓

---

## Non-Violation Observations (informational — not blocking)

**O-01** *(carried from cycle 1)*: `FindRule` at CopyEngine.cs line 1381 / 1387 contains `return null` — a JS-002 pattern in pre-existing code. Not introduced by B47-LaneA; plan correctly does not modify `FindRule`. Should be tracked in deferred backlog.

**O-02**: The plan's `ExecuteOneAccount` docblock counts the `||` in the `priceOk` ternary's false branch as not counted ("see strict note"). Conservative Lizard counting would yield CYC=8, still ≤ 8. This is informational — not a violation regardless of counting convention.

**O-03**: `BuildBeRejectMsg` is correctly declared `private static` — it uses no instance state (`_beOcoSeq`, `ctx`, etc.) and takes all data as parameters. This is the correct visibility for a pure formatter. ✓

---

## Checklist Result (10 items)

| # | Check | Result |
|---|-------|--------|
| 1 | Plan addresses defect (follower accounts skipped in all three BE/QX paths)? | ✓ PASS |
| 2 | All CYC values ≤ 8 for every modified method? | ✓ PASS |
| 3 | `IsFollowerAccount` uses NT8-safe iteration (no LINQ, `Array.IndexOf` + `foreach`)? | ✓ PASS |
| 4 | All P0 rules respected (no `lock()`, no `async void`, no `return null` in new methods, no `throw` in hot path)? | ✓ PASS |
| 5 | Scope minimal — only `PttBreakEven.cs`, `PttGlobalQuickExit.cs`, `CopyEngine.cs` modified? | ✓ PASS |
| 6 | Plan correctly identifies `PttGlobalBreakEven` as needing NO change? | ✓ PASS |
| 7 | `PttBreakEven.Execute()` CYC ≤ 8 handled with correct extraction (three helpers)? | ✓ PASS — Execute=7, ExecuteOneAccount≤8, BuildBeRejectMsg=3, RaiseBeNotify=2 |
| 8 | NT8-006 respected (no LINQ in NT8 context)? | ✓ PASS |
| 9 | 7-scan checklist section present and complete? | ✓ PASS |
| 10 | Plan includes exact change sites with line numbers verified against actual source? | ✓ PASS |

---

*Reviewed by: ptt-plan-reviewer (Phase 2, Cycle 2, 2026-08-08)*
*Cycle count: **2 of 2 maximum***
*Prior cycle verdict: REVIEW_FAIL (V-01 CYC undercount in ExecuteOneAccount)*
*This cycle verdict: **REVIEW_PASS** — V-01 resolved; zero violations found*
*Next phase: ptt-architect → Phase 3 ticket generation (04-tickets.md)*
