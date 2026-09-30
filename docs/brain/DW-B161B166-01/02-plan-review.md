# Plan Review — DW-B161B166-01 (VIOLATION-01-FIX)

**Epic:** DW-B161B166-01
**Phase:** 2 — Plan Review
**Reviewer:** ptt-plan-reviewer
**Date:** 2026-09-15
**Input plan:** `docs/brain/DW-B161B166-01/02-architecture-plan.md`
**Source verified:** `src/PropTraderTools/CopyEngine.cs` (L3178–L3254 read directly)

---

## A. CYC Counts

### A.1 `IsFollowerPositionBlocked` — CYC transition

Verified against source (L3182–L3241). McCabe decision points (project convention: each `if` = +1, each `&&`/`||` = +1):

| # | Line | Branch | Before fix | After fix |
|---|------|--------|-----------|-----------|
| 1 | — | base | 1 | 1 |
| 2 | L3184 | `if (acc == null)` | +1 = 2 | +1 = 2 |
| 3 | L3185 | `if (instr == null)` | +1 = 3 | +1 = 3 |
| 4 | L3186 | `if (HasOpenPosition)` Signal 1 | +1 = 4 | +1 = 4 |
| 5 | L3195 | `if (HasActivePttBeExits)` Signal 2 | +1 = 5 | +1 = 5 |
| 6 | L3204 | `if (HasSettlingBeTargets)` Signal 3 | +1 = 6 | +1 = 6 |
| 7 | L3213 | `if (HasActivePttBeTargets)` Signal 4 | +1 = 7 | +1 = 7 |
| 8 | L3222 | `if (IsBeSlotNonTerminal)` Signal 5 | +1 = 8 | — extracted |
| 9 | L3231 | `if (IsBeRetryArmedFlat)` Signal 6 | +1 = 9 | — extracted |
| 8 | — | `if (IsBeSettleOrRetrySignalActive)` | — | +1 = 8 |
| **Total** | | | **9** | **8** |

**Source-confirmed CYC: 9 → 8. Budget restored.** PASS ✓

No `||` or `&&` exist in the parent's `if` predicates — each is a single method call. Parent CYC arithmetic is exact.

### A.2 `IsBeSettleOrRetrySignalActive` — new helper

**Adjudication — spec says CYC=4, architect's final answer is CYC=5:**

The project convention counts `&&` and `||` short-circuit operators as +1 each. This is confirmed by
[`IsBeRetryArmedFlat`](../../src/PropTraderTools/CopyEngine.cs:3252):
> `// CYC=2: base(1) + && short-circuit(1)`

That method has exactly one `&&` in its expression body. The project **explicitly counts** the `&&` operator.
Therefore `||` in the new helper's null-guard `if (acc == null || instr == null)` is also +1.

| Branch | Count |
|--------|-------|
| base | 1 |
| `if (acc == null \|\| instr == null)` — the `if` node | +1 = 2 |
| `\|\|` short-circuit node | +1 = 3 |
| `if (IsBeSlotNonTerminal(acc.Name))` | +1 = 4 |
| `if (IsBeRetryArmedFlat(acc, instr))` | +1 = 5 |
| **Final CYC** | **5** |

**Authoritative answer: CYC=5.** The task description's CYC=4 used the looser convention; the plan correctly
self-corrects in §6.2 and settles on CYC=5. The inline comment in the helper must read CYC=5.

**Helper CYC=5 ≤ 8. Budget compliant.** PASS ✓

---

## B. Behavioural Equivalence

### B.1 Signal 5 — `IsBeSlotNonTerminal`

**Before (L3222–L3229, source-confirmed):**
- `if (IsBeSlotNonTerminal(acc.Name))` → log `"[PTT-COPY-GUARD] ghost-entry blocked (be-settle-pending): " + acc.Name + "\n" + instr.FullName` → `return true`

**After (helper body, plan §5.1):**
- Parent calls `IsBeSettleOrRetrySignalActive(acc, instr)`
- Null guard passes (parent's own guards at L3184-L3185 fire first — null args never reach helper)
- `if (IsBeSlotNonTerminal(acc.Name))` with identical argument `acc.Name` → identical log string → `return true` → parent `return true`

**Verdict: Behaviourally identical for Signal 5.** PASS ✓

### B.2 Signal 6 — `IsBeRetryArmedFlat`

**Before (L3231–L3238, source-confirmed):**
- `if (IsBeRetryArmedFlat(acc, instr))` → log `"[PTT-COPY-GUARD] ghost-entry blocked (be-retry-armed-flat): " + acc.Name + "\n" + instr.FullName` → `return true`

**After (helper body, plan §5.1):**
- Signal 5 check in helper returned `false` (otherwise already returned)
- `if (IsBeRetryArmedFlat(acc, instr))` with identical arguments → identical log string → `return true` → parent `return true`

**Verdict: Behaviourally identical for Signal 6.** PASS ✓

### B.3 Null guard interaction

The helper's null guard `if (acc == null || instr == null) return false` is reached only after
the parent's own null guards at L3184-L3185. In normal runtime flow the helper never sees null args.
The guard is belt-and-suspenders for test harness safety and does not alter observable output
for any existing caller. PASS ✓

---

## C. Signal Evaluation Order

**Before:** Signal 5 (`IsBeSlotNonTerminal`) at L3222 precedes Signal 6 (`IsBeRetryArmedFlat`) at L3231.
Short-circuit: if Signal 5 true, Signal 6 not evaluated.

**After (plan §5.1 helper body):**
- `if (IsBeSlotNonTerminal(acc.Name))` labelled `// (3) Signal 5` — first
- `if (IsBeRetryArmedFlat(acc, instr))` labelled `// (4) Signal 6` — second
- Same short-circuit: if Signal 5 returns true, Signal 6 is never called.

**Verdict: Order preserved. Signal 5 evaluated before Signal 6.** PASS ✓

---

## D. No New lock / Dictionary / Field

- Helper body (plan §5.1): no `lock(...)`, no `new Dictionary`, no `new ConcurrentDictionary`, no instance field declarations.
- Plan §11 (Threading Model): reads are via existing `_pendingFollowerBeSlots` and `_bePriceRetryAttempts` (both pre-existing `ConcurrentDictionary`) accessed through existing callees. No new collections.
- Plan §14 (Summary): three edits, one file. No field additions listed.

**Verdict: No new lock, no new Dictionary, no new field.** PASS ✓

---

## E. Stale Comment Fix Accuracy (L3178)

**Current source (L3178, confirmed):**
```
// CYC=8: base(1) + acc-null(2) + instr-null(3) + Signal1-branch(4) + Signal2-branch(5) + Signal3-branch(6) + Signal4-branch(7) + Signal5-branch(8). Signal 6 delegated.
```

This is inaccurate post-B166: Signal 6 is inline (L3231), not delegated; CYC is 9, not 8.

**Proposed replacement (plan §5.2 Change 1):**
```
// CYC=8: base(1) + acc-null(2) + instr-null(3) + Signal1(4) + Signal2(5)
//        + Signal3(6) + Signal4(7) + IsBeSettleOrRetrySignalActive(8). Signals 5+6 delegated.
//        DW-B161B166-01: was CYC=9 after B166 direct Signal6 add. Extraction restores budget.
```

- Post-fix CYC=8 with all 8 decision points correctly enumerated ✓
- "Signals 5+6 delegated" accurately describes the new state ✓
- DW traceability note present ✓

**Verdict: Stale comment fix is accurate and correctly describes the new state.** PASS ✓

---

## F. Lane-Split Gate

Plan §3 provides an explicit 6-row gate table, all answers NO, with explicit result:

> **Result: SINGLE-PIPELINE** — all changes in one file, one class, one extraction.

Repeated in §14: "Three edits, one file, one class. SINGLE-PIPELINE."

**Verdict: LANE-SPLIT GATE RESULT is present. SINGLE-PIPELINE.** PASS ✓

---

## G. Jane Street Rule Compliance

| Rule ID | Rule | Check Result |
|---------|------|-------------|
| JS-001 | No `throw` in non-exceptional paths | PASS — helper uses `return false` / `return true` only; no `throw` anywhere in scope |
| JS-002 | No null return from non-void | PASS — helper returns `bool`; base case is `return false`, never `return null` |
| JS-021 | No `lock()` | PASS — no `lock(...)` in helper or parent changes; `ConcurrentDictionary` reads are lock-free |
| JS-042 | ASCII-only string literals | PASS — both log strings copied verbatim from current source (already ASCII); plan §8 confirms; no Unicode, no curly quotes |
| JS-CYC | CYC ≤ 8 per method | PASS — parent 9→8; helper CYC=5; both ≤ 8 |
| JS-008 | No mutable struct fields; brushes Freeze()d | PASS — no struct, no brush in scope |
| JS-009 | No `Dictionary<K,V>` for shared state | PASS — no new Dictionary introduced |
| JS-010 | No public ctor on singleton/signal struct | PASS — no singleton/struct ctor touched |
| JS-023 | UI update from off-thread via Dispatcher.InvokeAsync | PASS — no UI interaction; no Dispatcher needed |
| NT8-ASYNC | No async/await in OnInitialize/OnDestroyed/OnWindowCreated | PASS — no async/await in any touched method |
| NT8-DATETIME | No DateTime.Now (must use UtcNow) | PASS — not used |
| NT8-CREATEORDER | CreateOrder must use PTT- prefix | PASS — no CreateOrder call |
| NT8-HEX | No hardcoded #RRGGBB hex | PASS — not present |
| NT8-FONT | No FontFamily override | PASS — not present |

**All Jane Street and NT8 rules: PASS.** No violations found.

---

## H. Violation Log

| # | Rule ID | Description | Location in Plan | Status |
|---|---------|-------------|-----------------|--------|
| — | — | No violations found | — | — |

---

## I. Spec Coverage Matrix

| Requirement | Addressed? | Plan Section |
|-------------|-----------|--------------|
| `IsFollowerPositionBlocked` CYC reduced from 9 to 8 | YES | §6.1 |
| Extract Signals 5+6 into private helper | YES | §5.1, §5.2 |
| New helper CYC ≤ 8 (CYC=5 per project convention) | YES | §6.2 |
| Log strings preserved verbatim (Signal 5 and Signal 6) | YES | §8 |
| Signal evaluation order unchanged (5 before 6) | YES | §5.1 body ordering |
| Null guard in helper (acc, instr) | YES | §7 |
| Stale CYC=8 comment at L3178 corrected | YES | §5.2 Change 1 |
| No new fields, no new lock, no new Dictionary | YES | §4, §11 |
| Caller signatures unchanged (ShouldSkipFollower, TryReplaceOnAtmCancel) | YES | §9 |
| Jane Street rules (JS-001, JS-002, JS-021, JS-042) | YES | §10 |
| Lane-split gate result present | YES | §3 |
| DW-B161B166-01 deferred item closed | YES | §2 |

All spec requirements addressed. No gaps. ✓

---

## J. Final Determination

Zero violations found across all check categories (A through I).

All CYC counts verified against live source.
Behavioural equivalence confirmed by direct call-path trace.
Signal order preserved.
No new lock, Dictionary, or field.
Stale comment fix is accurate.
Lane-split gate result is present (SINGLE-PIPELINE).
All Jane Street and NT8 rules satisfied.

---

## REVIEW_PASS
