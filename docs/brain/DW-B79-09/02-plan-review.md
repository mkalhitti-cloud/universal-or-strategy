# DW-B79-09 — Plan Review (Phase 2)

**Reviewer**: ptt-plan-reviewer  
**Date**: 2026-08-21  
**Plan reviewed**: `docs/brain/DW-B79-09/02-architecture-plan.md`  
**Spec section**: `specs/002-trade-copier-spec.html` lines 24436–24500  
**Source HEAD**: 5925b618  

---

## VERDICT: REVIEW_PASS

No violations found. All checks below PASS.

---

## Section A — Spec Coverage Matrix

| Spec Requirement | Addressed? | Plan Section |
|------------------|-----------|--------------|
| Guard `CancelQxBrackets` 2-param (`CopyEngine.cs`) | ✓ YES | §3a, §6 |
| Guard `CancelQxBrackets` 3-param (`CopyEngine.cs`) | ✓ YES | §3b, §6 |
| Guard `CancelStaleBracketsLocal` (`PttBreakEven.cs`) | ✓ YES | §3c, §6 |
| `CancelAllAccountOrders` already guarded — not in scope | ✓ YES (noted §1, §2) | §2 Not-in-scope note |
| Fix: identical `RemoveAll(Filled \|\| Cancelled)` one-liner | ✓ YES | §3a–§3c |
| CYC impact: +0 per method | ✓ YES | §4 |
| Test delta: 292 → 295 (+3 [Fact]) | ✓ YES | §5 |
| 2 source files touched (CopyEngine.cs, PttBreakEven.cs) | ✓ YES | §6 (3 entries: 2 src + 1 test) |
| Full 5-phase PTT pipeline required | ✓ YES | Implied by plan structure |

**Coverage: 9/9 — COMPLETE**

---

## Section B — Source Line Number Verification

All BEFORE/AFTER code blocks verified against HEAD 5925b618.

| Method | Plan location claim | Actual source | Match |
|--------|---------------------|--------------|-------|
| `CancelQxBrackets` 2-param — method start | `CopyEngine.cs:613` | L613 ✓ | ✓ PASS |
| `CancelQxBrackets` 2-param — `acc.Cancel` | `CopyEngine.cs:630` | L630 ✓ | ✓ PASS |
| `CancelQxBrackets` 3-param — method start | `CopyEngine.cs:677` | L677 ✓ | ✓ PASS |
| `CancelQxBrackets` 3-param — `acc.Cancel` | `CopyEngine.cs:702` | L702 ✓ | ✓ PASS |
| `CancelStaleBracketsLocal` — method start | `PttBreakEven.cs:171` | L171 ✓ | ✓ PASS |
| `CancelStaleBracketsLocal` — `acc.Cancel` | `PttBreakEven.cs:193` | L193 ✓ | ✓ PASS |

**BEFORE block accuracy:**

- §3a: Plan shows `try { acc.Cancel(stale.ToArray()); } catch { }` at L630–631. Source L630–631 identical. ✓  
- §3b: Plan shows `if (stale.Count == 0) return; // (7)` (L701) then `try { acc.Cancel(stale.ToArray()); } catch { }` (L702–703). Source L701–703 identical. ✓  
- §3c: Plan shows `if (stale.Count == 0) return; // (3)` (L190) then `try { acc.Cancel(stale.ToArray()); ... } catch { }` (L191–198). Source L190–198 identical. ✓  

**AFTER placement correctness:**

- §3a: `RemoveAll` inserted between `if (stale.Count == 0) return;` (L629) and `try { acc.Cancel... }` (L630). ✓  
- §3b: `RemoveAll` inserted after `if (stale.Count == 0) return;` (L701) and before `try { acc.Cancel... }` (L702). ✓  
- §3c: `RemoveAll` inserted as first statement inside the `try` block at L192, before `acc.Cancel` at L193. Placement inside `try` is safe — `RemoveAll` with a valid lambda predicate cannot throw; any NT8 edge case is caught by the existing `catch { }`. ✓  

---

## Section C — CYC Analysis Verification

`RemoveAll(predicate)` is a single `List<T>` method call. It introduces no branch point in the calling method's control flow graph. Cyclomatic complexity is unchanged.

| Method | Branch points counted from source | CYC before | CYC after | Budget |
|--------|-----------------------------------|-----------|-----------|--------|
| `CancelQxBrackets` 2-param | (1) null-guard `(2) foreach (3) !stateOk continue (4) instrument-filter continue (5) IsQxCancelCandidate (6) stale.Count==0` | 6 | 6 | ≤8 ✓ |
| `CancelQxBrackets` 3-param | Stated CYC=7 in source comment L673; verified: (1) null-guard (2) foreach (3) !stateOk (4) instrument-filter (5) snapshot-filter (6) IsQxCancelCandidate (7) stale.Count==0 | 7 | 7 | ≤8 ✓ |
| `CancelStaleBracketsLocal` | (1) null-guard (2) foreach (3) o==null continue (4) stateOk&&instrOk&&notBe (5) stale.Count==0 — catch is not a decision point | 6 | 6 | ≤8 ✓ |

**CYC budget: all three methods remain ≤8. No extraction required. PASS.**

---

## Section D — Jane Street DNA Compliance

| Rule | Check | Result |
|------|-------|--------|
| **JS-021** (no lock) | No `lock(...)` added. `RemoveAll` operates on a local `List<T>` allocated in the method frame — no shared state mutation. | ✓ PASS |
| **JS-001** (no throw in hot path) | `RemoveAll` with a valid `o => ...` lambda predicate does not throw. All three methods retain their existing `catch { }` wrapper around `acc.Cancel`. No `throw` statement introduced. | ✓ PASS |
| **JS-002** (no return null) | All three methods are `void` — no return value. Rule is not applicable. No `return null` risk. | ✓ N/A |
| **JS-080** (CYC ≤ 8) | All methods stay at CYC 6/7/6 (see Section C). | ✓ PASS |
| **ASCII-only** | Inserted line: `stale.RemoveAll(o => o.OrderState == OrderState.Filled` / `|| o.OrderState == OrderState.Cancelled);` — 100% ASCII characters, no Unicode, no curly quotes, no emoji. | ✓ PASS |
| **No async/await** | Insertions are synchronous. No async/await introduced in any of the three methods. | ✓ PASS |
| **No new lock / Monitor / Mutex / SemaphoreSlim** | None introduced. | ✓ PASS |
| **No `throw` in OnOrderUpdate / gate chain** | Not applicable — `CancelQxBrackets` and `CancelStaleBracketsLocal` are cancel helpers, not event handlers. | ✓ N/A |
| **No magic string** | No string literals introduced. | ✓ PASS |

---

## Section E — Architecture Coherence

| Check | Finding | Result |
|-------|---------|--------|
| All 3 unguarded methods addressed | §2 table lists all 3; §3a–3c each contain a concrete BEFORE/AFTER code block | ✓ PASS |
| 4th method (`CancelAllAccountOrders`) correctly excluded | Noted as already guarded at §2 Not-in-scope and §1 Context. Consistent with spec. | ✓ PASS |
| No interface changes, no CopyRule fields, no signature changes | §6 explicitly states this. No new params in any method. | ✓ PASS |
| Test plan: 3 tests map 1-to-1 to the 3 methods | §5 table: T_DW_B79_09_01/02/03 — one per method | ✓ PASS |
| IL scan approach valid for public methods | `CancelQxBrackets` (2-param and 3-param) are `internal` — accessible via reflection from same assembly's test project. IL scan established pattern from B79 tests. | ✓ PASS |
| Private method fallback plan specified | §5: `BindingFlags.NonPublic | BindingFlags.Static` for `CancelStaleBracketsLocal` — consistent with existing `PttBreakEvenB72Tests.cs` pattern. | ✓ PASS |
| Single ticket justified | 3 independent one-line insertions in 2 files — no ordering constraint between edits. Single ticket is correct. | ✓ PASS |
| `deploy-sync.ps1` in acceptance criteria | §10 acceptance criteria item 9. ✓ | ✓ PASS |
| F5 gate in acceptance criteria | §10 acceptance criteria item 10. ✓ | ✓ PASS |
| 7-scan zero listed in acceptance criteria | §10 item 8 explicitly lists all 7 scans. ✓ | ✓ PASS |

---

## Section F — NT8 API Validity

| Claim | Verification |
|-------|-------------|
| `List<T>.RemoveAll(predicate)` | Standard .NET BCL method on `System.Collections.Generic.List<T>`. Not NT8-specific. No NT8 API constraints apply. ✓ |
| `acc.Cancel(Order[])` | Already used in the existing code at the same sites. Not a new API call. ✓ |
| No `AtmStrategyCreate` or StrategyBase-only APIs introduced | Confirmed — only `List.RemoveAll` is added. ✓ |

---

## Section G — Risk and Mitigation Review

| Risk in plan §8 | Assessment |
|-----------------|-----------|
| Second race window between `RemoveAll` and `acc.Cancel` | Acknowledged and correctly characterised as defence-in-depth, not guarantee. The `catch { }` handles broker rejection. Mitigated. ✓ |
| `RemoveAll` mutating list while NT8 iterates internally | Correctly noted as N/A: `acc.Cancel` receives `ToArray()` snapshot. Mutation of the original `List<T>` before `ToArray()` is safe. ✓ |
| Reflection for private `CancelStaleBracketsLocal` | Low-risk fallback with existing pattern citation. ✓ |

---

## Section H — Violation Log

| # | Rule ID | Severity | Description | Location | Status |
|---|---------|----------|-------------|----------|--------|
| — | — | — | No violations found | — | — |

**Total violations: 0**

---

## Summary

The plan is minimal, precise, and complete. All three unguarded cancel methods are addressed with identical one-line insertions that are source-verified, CYC-neutral, and fully compliant with the Jane Street DNA rules. The test plan is sound and consistent with established patterns. No spec requirements are unaddressed. No rule violations are present.

**REVIEW_PASS — proceed to Phase 3 (ticket generation by ptt-architect).**
