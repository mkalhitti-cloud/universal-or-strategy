# B161-B166 Ghost-Entry Repair Block — Plan Review

**Epic:** B161-B166  
**Reviewer:** ptt-plan-reviewer  
**Phase:** 2 — Plan Review  
**Plan reviewed:** `docs/brain/B161-B166/02-architecture-plan.md`  
**Date:** 2026-09-15  

---

## 1. Review Summary

This plan documents 7 surgical repairs to `CopyEngine.cs` that close ghost-entry race windows in the BE-All burst path. The plan is a **FORMAL RECORD** — all repairs are already present in `src/PropTraderTools/CopyEngine.cs`. The review evaluates plan completeness, root-cause accuracy, rule compliance, and structural correctness against the mandatory criteria.

**One known violation is present:** VIOLATION-01 (`IsFollowerPositionBlocked` CYC=9). The plan acknowledges it fully, provides the exact branch-by-branch calculation, designates it P1, and proposes a concrete follow-on remediation ticket. Per review criteria, this is treated as a conditional non-blocking finding.

**No blocking issues found.**

---

## 2. Per-Repair Review

### B161 — `TryReplaceOnAtmCancel` guard

| Item | Result | Notes |
|------|--------|-------|
| Present in plan | ✅ PASS | §3, L947–L960 |
| Root cause correct | ✅ PASS | Correctly identifies the missing `IsFollowerPositionBlocked` guard on this code path, distinct from the `DispatchCopy` path wired in B160 |
| CYC | ✅ PASS | 4→5, budget ≤8 |
| JS-001 | ✅ PASS | `void` method, no throw |
| JS-002 | ✅ PASS | `void`, not applicable |
| JS-021 | ✅ PASS | No `lock()`. `IsFollowerPositionBlocked` uses `ToList()` snapshot |
| JS-042 | ✅ PASS | `"BE-All race -- skip re-place (4)"` is ASCII-only |

**Verdict: PASS**

---

### B162-A — `TryFireBePriceRetry` atomic claim

| Item | Result | Notes |
|------|--------|-------|
| Present in plan | ✅ PASS | §3, L7218–L7261 |
| Root cause correct | ✅ PASS | Correctly identifies non-consuming `TryGetValue` on concurrent threads; contrasts with the existing `TryRemove` pattern already used in `TryFireFollowerBeRetry` (L1885) |
| CYC | ✅ PASS | 7→8 (`||` in flat-cancel guard at L7246–7247 adds one branch), budget ≤8 |
| JS-001 | ✅ PASS | `void`, no throw |
| JS-002 | ✅ PASS | `void`, not applicable |
| JS-021 | ✅ PASS | `ConcurrentDictionary.TryRemove` is lock-free atomic; no `lock()` |
| JS-042 | ✅ PASS | All string literals ASCII-only |

**Verdict: PASS**

---

### B162-B — `HasActivePttBeExits` + `StillAtNonBePrice` prefix fix

| Item | Result | Notes |
|------|--------|-------|
| Present in plan | ✅ PASS | §3, L3110, L7187–L7188 |
| Root cause correct | ✅ PASS | Correctly identifies `SubmitBeStopOrder` submits bare `"PTT-BE-Stop"` (no suffix); prior prefix `"PTT-BE-Stop-"` (with dash) missed bare orders during burst |
| CYC | ✅ PASS | Both methods unchanged at 8 and 7 (string literal change, no branches added) |
| JS-001 | ✅ PASS | Both `bool`-returning; no throw |
| JS-002 | ✅ PASS | `false` on all guard exits; no null returns |
| JS-021 | ✅ PASS | `acc.Orders.ToList()` snapshot; no `lock()` |
| JS-042 | ✅ PASS | `"PTT-BE-Stop"` is ASCII-only |
| Intentional asymmetry | ✅ PASS | Plan explicitly states `TryTriggerBeRecovery` retains `"PTT-BE-Stop-"` WITH dash intentionally (documented in §3 repair section AND §6 CONCERN-01). NOT flagged as a discrepancy. |

**Verdict: PASS**

---

### B163 — `IsPttEntryOrderCancelTrigger` AverageFillPrice guard

| Item | Result | Notes |
|------|--------|-------|
| Present in plan | ✅ PASS | §3, L924–L935 |
| Root cause correct | ✅ PASS | Correctly distinguishes NT8 lifecycle teardown cancel of a filled Entry (`AverageFillPrice > 0`) from ATM-sweep cancel of never-filled Entry (`AverageFillPrice == 0`) |
| Guard retained (NOT superseded) | ✅ PASS | Plan does not mark B163 as superseded or recommend reverting it. B164 explicitly states it "adds Signal 4 on top of B163's infrastructure." Both coexist. |
| CYC | ✅ PASS | 3→4, budget ≤8 |
| JS-001 | ✅ PASS | `bool`-returning `static` method; no throw |
| JS-002 | ✅ PASS | `false` on all guard exits; no null returns |
| JS-021 | ✅ PASS | `static` method; no instance state; no `lock()` |
| JS-042 | ✅ PASS | Comment `"(4) B163: lifecycle cancel..."` is ASCII-only |

**Verdict: PASS**

---

### B164 — `HasActivePttBeTargets` + Signal 4

| Item | Result | Notes |
|------|--------|-------|
| Present in plan | ✅ PASS | §3, L3147–L3158, L3213–L3220 |
| Root cause correct | ✅ PASS | Correctly identifies the 1–3 event-loop tick gap where Signal 2 = false (stop filled) and Signal 3 = false (target still Working, not yet CancelPending) |
| CYC — `IsFollowerPositionBlocked` at B164 stage | ✅ PASS | 6→7 (B164 stage only); budget ≤8 at this stage |
| CYC — `HasActivePttBeTargets` (new helper) | ✅ PASS | 8 ≤ 8 |
| JS-001 | ✅ PASS | Both `bool`-returning; no throw |
| JS-002 | ✅ PASS | `false` on all null/empty-loop exits; no null |
| JS-021 | ✅ PASS | `ToList()` snapshot; no `lock()` |
| JS-042 | ✅ PASS | `"[PTT-COPY-GUARD] ghost-entry blocked (active-be-targets): "` is ASCII-only |
| Non-blocking concern | ⚠️ CONCERN-02 | `IsPttBeStopActiveState` reused for Target orders — naming is misleading but functionally correct. Plan acknowledges this. Deferred rename. |

**Verdict: PASS (CONCERN-02 non-blocking)**

---

### B165 — `IsFollowerPositionBlocked` Signal 5 (`IsBeSlotNonTerminal`)

| Item | Result | Notes |
|------|--------|-------|
| Present in plan | ✅ PASS | §3, L3222–L3229, L9382–L9386 |
| Root cause correct | ✅ PASS | Correctly identifies that `_pendingFollowerBeSlots` holds the slot after Signals 1–4 all clear; Sim104 gap confirmed in output.md 2026-09-14 |
| CYC — `IsFollowerPositionBlocked` at B165 stage | ✅ PASS | 7→8 (B165 stage only); budget ≤8 at this stage |
| CYC — `IsBeSlotNonTerminal` | ✅ PASS | CYC=1 |
| JS-001 | ✅ PASS | `bool`-returning; no throw |
| JS-002 | ✅ PASS | `false` on empty-slot case; no null |
| JS-021 | ✅ PASS | `_pendingFollowerBeSlots.ContainsKey` is O(1) lock-free read |
| JS-042 | ✅ PASS | `"[PTT-COPY-GUARD] ghost-entry blocked (be-settle-pending): "` is ASCII-only |

**Verdict: PASS**

---

### B166 — `TryFireBePriceRetry` sentinel preservation + `RunFollowerEvictPath` clear

| Item | Result | Notes |
|------|--------|-------|
| Present in plan | ✅ PASS | §3, L7242–L7249, L2022 |
| Root cause correct | ✅ PASS | Correctly identifies 3-step race: timer re-arms sentinel → flat-cancel path cleared sentinel via `TryRemove` before `DispatchCopy` evaluated it → ghost fired. |
| CYC — `TryFireBePriceRetry` | ✅ PASS | 8→8 (removing `TryRemove` call inside `if` body does not change branch count) |
| CYC — `RunFollowerEvictPath` | ✅ PASS | 4→4 (`TryRemove` statement added, not an `if` branch) |
| JS-001 | ✅ PASS | Both `void`; no throw |
| JS-002 | ✅ PASS | `void`, not applicable |
| JS-021 | ✅ PASS | `ConcurrentDictionary.TryRemove` is lock-free; no `lock()` |
| JS-042 | ✅ PASS | `"B166: sentinel stays alive..."` is ASCII-only |

**Verdict: PASS**

---

## 3. LANE-SPLIT GATE Compliance

**Required:** Plan must state `LANE-SPLIT GATE RESULT: SINGLE-PIPELINE`

**Found:** §2 is titled `## 2. LANE-SPLIT GATE RESULT: SINGLE-PIPELINE` and closes with bold `**GATE RESULT: SINGLE-PIPELINE**`. Gate table with Q1–Q4 analysis is present. Combined SIM gate timestamp recorded (2026-09-15 4:33 PM).

**Verdict: PASS** ✅

---

## 4. CYC Budget Review

| Method | Before | After | Budget | Status |
|--------|--------|-------|--------|--------|
| `TryReplaceOnAtmCancel` | 4 | 5 | ≤8 | ✅ PASS |
| `TryFireBePriceRetry` | 7 | 8 | ≤8 | ✅ PASS |
| `HasActivePttBeExits` | 8 | 8 | ≤8 | ✅ PASS |
| `StillAtNonBePrice` | 7 | 7 | ≤8 | ✅ PASS |
| `IsPttEntryOrderCancelTrigger` | 3 | 4 | ≤8 | ✅ PASS |
| `IsFollowerPositionBlocked` | 6→7→8 (incremental) | **9** (final) | ≤8 | ⚠️ VIOLATION-01 |
| `HasActivePttBeTargets` (new) | — | 8 | ≤8 | ✅ PASS |
| `IsBeSlotNonTerminal` | pre-existing | 1 | ≤8 | ✅ PASS |
| `RunFollowerEvictPath` | 4 | 4 | ≤8 | ✅ PASS |

### VIOLATION-01 Assessment

`IsFollowerPositionBlocked` reaches CYC=9 after B166 adds Signal 6 (1 base + 8 `if`-branches = 9). This exceeds the JS-CYC ≤8 budget.

**Disposition (per review criteria §e):** The plan fully acknowledges VIOLATION-01 in §6:
- The branch-by-branch calculation is correct and fully enumerated (9 rows, line-cited).
- Severity is correctly rated P1.
- A concrete remediation is specified: extract Signals 5+6 into `IsBeSettleOrRetrySignalActive(Account, Instrument)` — with the proposed signature, CYC calculation (parent reduces 9→8, helper CYC=4), and a designated follow-on ticket `VIOLATION-01-FIX` in §8.

This meets the conditional pass bar: acknowledged, calculated correctly, remediated concretely. **Non-blocking for REVIEW_PASS.**

The follow-on ticket VIOLATION-01-FIX is mandatory and must be the first ticket generated in Phase 3.

---

## 5. Jane Street Rules Assessment

| Rule | All Repairs | Status |
|------|-------------|--------|
| JS-001 — no throw from non-void | All bool-returning methods: no throw in any repair. All void methods: not applicable. | ✅ PASS |
| JS-002 — no null return from non-void | All bool-returning methods return `false` on guard exits; no `null` return found in any repair. | ✅ PASS |
| JS-021 — no lock() | Plan §1 executive summary: "No lock() anywhere." Each repair section confirms: `ConcurrentDictionary.TryRemove`/`ContainsKey` (lock-free), `ToList()` snapshot (no lock), `static` method (no instance state). | ✅ PASS |
| JS-042 — ASCII-only string literals | All comment strings and log strings verified as ASCII-only (no Unicode, no curly quotes, no emoji) across all 7 repairs. | ✅ PASS |
| JS-003 — no magic string for discriminated state | All string comparisons use named constants (`"PTT-BE-Stop"`, `"PTT-BE-Target-"`) with `StringComparison.Ordinal`. No empty-string discriminated state. | ✅ PASS |
| JS-008 — no mutable fields on struct / Freeze SolidColorBrush | Not applicable to this set of repairs. | N/A |
| JS-009 — no Dictionary for thread-touched collections | Not applicable — all collections are `ConcurrentDictionary`. | ✅ PASS |
| JS-010 — no public constructor on singleton/signal struct | Not applicable. | N/A |

**Verdict: ALL APPLICABLE JS RULES PASS**

---

## 6. Signal Numbering Check

Plan §4 signal map evaluated against required specification (Signals 1–6):

| # | Required | Plan States | Match |
|---|----------|-------------|-------|
| 1 | pre-B161 position check | `HasOpenPosition` — `Quantity > 0` — B159 | ✅ |
| 2 | `HasActivePttBeExits` (B162-B fixes prefix) | `HasActivePttBeExits` — `PTT-BE-Stop*` Working/Accepted/Submitted — B160 (B162-B fixed prefix) | ✅ |
| 3 | `HasSettlingBeTargets` (pre-existing) | `HasSettlingBeTargets` — `PTT-BE-Target-*` CancelPending/CancelSubmitted — B160 | ✅ |
| 4 | `HasActivePttBeTargets` (B164 new helper) | `HasActivePttBeTargets` — `PTT-BE-Target-*` Working/Accepted/Submitted — B164 | ✅ |
| 5 | `IsBeSlotNonTerminal` (B165) | `IsBeSlotNonTerminal` — `_pendingFollowerBeSlots.ContainsKey` — B165 | ✅ |
| 6 | `IsBeRetryArmedFlat` (B166 sentinel) | `IsBeRetryArmedFlat` — `IsFlat && _bePriceRetryAttempts.ContainsKey` — B166 | ✅ |

Time-ordered coverage diagram in §4 correctly shows no gap windows between consecutive signals. All 6 signals present, correctly ordered, no duplicates.

**Verdict: PASS** ✅

---

## 7. Blocking Issues

**NONE.**

All mandatory criteria pass. VIOLATION-01 is non-blocking per the conditional pass rule (acknowledged + concrete follow-on ticket). No JS rules violated at the plan level. No missing repairs. No missing gate result. No superseded B163. No incorrect asymmetry treatment.

---

## 8. Non-Blocking Concerns (for tickets / deferred backlog)

| ID | Item | Priority | Disposition |
|----|------|----------|-------------|
| VIOLATION-01 | `IsFollowerPositionBlocked` CYC=9 — extract Signals 5+6 into `IsBeSettleOrRetrySignalActive` helper | P1 | **Mandatory follow-on ticket.** Must be Ticket-1 in Phase 3. |
| CONCERN-02 | Rename `IsPttBeStopActiveState` → `IsPttBeOrderActiveState` (3 call sites) | P3 | Deferred cleanup. |

---

## 9. Final Verdict

```
REVIEW_PASS
```

**Conditions:**
- VIOLATION-01-FIX ticket is mandatory. It must be issued as Ticket-1 in Phase 3 before any other tickets. The CYC=9 in `IsFollowerPositionBlocked` is a real rule violation (JS-CYC); it is accepted here solely because the plan provides a complete and correct remediation plan.
- CONCERN-02 (naming) is deferred with no timeline requirement.
- All 7 repairs are correctly documented, root causes are accurate, B163 is not superseded, signal numbering is correct, LANE-SPLIT gate is present, all applicable Jane Street rules pass, and `TryTriggerBeRecovery` intentional asymmetry is correctly documented.

Phase 3 (ticket generation) is **UNLOCKED**.
