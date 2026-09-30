# B167 — Plan Review
**Reviewer:** ptt-plan-reviewer  
**Phase:** 2 (Plan Review)  
**Plan under review:** `docs/brain/B167/02-architecture-plan.md`  
**Spec cards reviewed:** `docs/brain/DW-B167-01/00-dw-card.md`, `docs/brain/DW-B167-02/00-dw-card.md`, `docs/brain/no-pipeline-repairs.md` (DW-B167-02 entry)  
**RULES_CATALOG.md:** Not present at `docs/protocol/RULES_CATALOG.md` — standard JS rules applied from role definition.  
**Verdict:** **REVIEW_PASS**

---

## Check Results

### LANE-SPLIT GATE COMPLIANCE

| # | Check | Result | Citation |
|---|-------|--------|---------|
| L1 | Plan states `LANE-SPLIT GATE RESULT: SINGLE-PIPELINE` | **PASS** | Plan line 5: `**LANE-SPLIT GATE RESULT:** SINGLE-PIPELINE (sequential tickets, same source file)` |
| L2 | Rationale stated (same write-set, sequential) | **PASS** | Plan §1: "both tickets write to the same `.cs` file. Concurrent lanes would produce a merge conflict." |

---

### TICKET 1 CHECKS (DW-B167-01)

| # | Check | Result | Citation |
|---|-------|--------|---------|
| 1 | Property name is `HadPositionAtDrainTime` (NOT `WasFlat`) | **PASS** | Plan §2 CRITICAL Naming Rule: "`WasFlat` alias **REJECTED**. All artifacts MUST use `HadPositionAtDrainTime`." Property named `HadPositionAtDrainTime` throughout. |
| 2 | Guard change: `if (payload.HadPositionAtDrainTime && IsFlat(FindPosition(follower, payload.Instrument)))` | **PASS** | Plan §2 T3 shows exact guard. Logic table verifies semantics: pre-fill drag `false && true = NO` → resubmit ✅; post-flatten race `true && true = YES` → abort ✅; normal open `true && false = NO` → resubmit ✅. |
| 3 | `HadPositionAtDrainTime` is LAST constructor param of `PendingDispatchDrain` (after `timestampTicks`) | **PASS** | Plan §2 T1 shows `bool hadPositionAtDrainTime` added after `long timestampTicks`. Method signatures section confirms param order: `..., long timestampTicks, bool hadPositionAtDrainTime`. |
| 4 | Capture uses `bool hadPos = !IsFlat(FindPosition(follower, instrument))` (NOT `HasOpenPosition`) | **PASS** | Plan §2 T2 shows exactly `bool hadPos = !IsFlat(FindPosition(follower, instrument));`. |
| 5 | Diagnostic log token `[DRAIN-SUBMIT-PREFILL]` on `HadPositionAtDrainTime=false` path | **PASS** | Plan §2 T4: `if (!payload.HadPositionAtDrainTime)` logs `"[DRAIN-SUBMIT-PREFILL] acct=" + acctKey`. |
| 6 | Zero changes to `AbortDrainOnFill` | **PASS** | Plan §2 and §8 File Map: no mention of `AbortDrainOnFill` in any change prescription. Only T1–T4 changes enumerated, none touching `AbortDrainOnFill`. |
| 7 | CYC proof: `SubmitDrainedEntry` 5→6, one added `if` (T4), ≤8 PASS | **PASS** | Plan §2 CYC Proof table: T3 modifies existing condition (no new vertex, CYC stays 5); T4 adds one `if` branch (CYC 5→6). Final CYC = 6 ≤ 8. |

---

### TICKET 2 CHECKS (DW-B167-02)

| # | Check | Result | Citation |
|---|-------|--------|---------|
| 8 | Described as FORMAL RECORD ONLY (already applied fix) | **PASS** | Plan §3: "**Fix already applied to source.** ptt-engineer for Ticket 2 performs READ-ONLY source confirmation. Zero `.cs` edits permitted." |
| 9 | No re-engineering of already-applied fix | **PASS** | Plan §3 Source Verification Criteria: confirmation-only, 3 read checks. §8 File Map: Ticket 2 row = "READ-ONLY confirmation. Zero edits." |
| 10 | L1032 matches `if (!IsPttCopyEntry(order))` | **PASS** | Plan §3 Source Verification item 1: "L1032 reads `if (!IsPttCopyEntry(order))` — not the old `StartsWith("PTT-Copy")` inline form." |
| 11 | `IsPttCopyEntry` at L8313–L8314 returns correct predicate | **PASS** | Plan §3 shows `private static bool IsPttCopyEntry(Order o) => o.Name.StartsWith("PTT-Copy", StringComparison.Ordinal) \|\| o.Name == "Entry";`. Both clauses present, `StringComparison.Ordinal` present. *(Note: no-pipeline-repairs.md gives line L8309 vs plan L8313 — ~4-line drift, content identical. Not a review failure.)* |
| 12 | SIM gate criteria carried forward from no-pipeline-repairs.md | **PASS** | Plan §3: "Carried forward from `no-pipeline-repairs.md` DW-B167-02 entry (see §7 below)." Plan §7 items 12–16 match no-pipeline-repairs.md SIM criteria verbatim. |

---

### JANE STREET COMPLIANCE CHECKS

| # | Rule | Check | Result | Citation |
|---|------|-------|--------|---------|
| 13 | JS-001 | No new throw paths (both tickets) | **PASS** | Plan §4: "No throws added. `bool hadPos` assignment and condition change cannot throw." / "No code changes." |
| 14 | JS-021 | `IsFlat` and `FindPosition` are lock-free | **PASS** | Plan §4: "`IsFlat` and `FindPosition` are lock-free read-only helpers. `HadPositionAtDrainTime` is written once at construction (immutable after). No `lock()` introduced." |
| 15 | JS-CYC | `SubmitDrainedEntry` ≤8 after change; CYC delta correctly stated | **PASS** | Plan CYC table shows 5→6, limit 8. Delta of +1 from T4 `if` branch is correct. No new branches from T3 (modifies existing condition). |
| 16 | ASCII-only | `[DRAIN-SUBMIT-PREFILL]` is ASCII only | **PASS** | Plan §4: "`"[DRAIN-SUBMIT-PREFILL] acct="` — all ASCII characters. No Unicode, no curly quotes, no emoji." |

---

### SCOPE CHECKS

| # | Check | Result | Citation |
|---|-------|--------|---------|
| 17 | No changes to `IsFollowerPositionBlocked` or its signals | **PASS** | Plan makes zero reference to `IsFollowerPositionBlocked` in any change prescription. §8 File Map touches only `PendingDispatchDrain` (T1), `DrainThenDispatch` (T2), `SubmitDrainedEntry` (T3+T4), and Ticket 2 READ-ONLY confirmation. |
| 18 | No changes to B161–B166 fixes | **PASS** | Plan scope is entirely within the drain pipeline. No B161–B166 methods referenced in any change instruction. |
| 19 | Plan does NOT attempt to fix the big fo=NULL ATM bracket placement issue (DW-B167-02 primary) | **PASS** | Plan §3 correctly scopes Ticket 2 to ONLY the `TrySetSlateReadyOnDrainFill` repair documented in no-pipeline-repairs.md. The IsWorkingBracket=False / IsDispatchTriggerState-StopMarket / TryInitialBracketCopy architecture problem from DW-B167-02 primary is correctly left unaddressed (separate unresolved bug). |

---

### TEST PLAN CHECKS

| # | Check | Result | Citation |
|---|-------|--------|---------|
| 20 | 4 named `[Fact]` unit tests for Ticket 1 | **PASS** | Plan §5: `SubmitDrainedEntry_HadPositionFalse_DoesNotAbort`, `SubmitDrainedEntry_HadPositionTrue_IsFlat_Aborts`, `SubmitDrainedEntry_HadPositionTrue_NotFlat_Submits`, `DrainThenDispatch_CapturesHadPositionAtDrainTime`. All 4 present with setup/assert/confirm structure. |
| 21 | 1 structural confirmation test for Ticket 2 | **PASS** | Plan §5: `TrySetSlateReadyOnDrainFill_EntryFill_SetsSlateReady` — setup, assert, confirms DW-B167-02 extended guard. |

---

### DEFERRED

| # | Check | Result | Citation |
|---|-------|--------|---------|
| 22 | ECA-1 acknowledged as deferred without blocking pipeline | **PASS** | Plan §6: ECA-1 scenario described (pre-fill drag + manual follower fill race within ~100–500ms), mitigation via `[DRAIN-SUBMIT-PREFILL]` diagnostic, disposition: "DO NOT block pipeline. If reviewer determines a DW card is warranted, log as new deferred item in `06-deferred-backlog.md`." |

---

## Violation Summary

**No violations found.**

All 22 mandatory checks PASS. No Jane Street rule violations (JS-001, JS-021, JS-CYC, ASCII-only). No scope violations. No naming violations. No missing test coverage. No missing gate declarations.

---

## Additional Observations (Non-blocking)

1. **ECA-1 DW Card Recommendation:** The pre-fill drag + manual follower fill race described in §6 is real. The `[DRAIN-SUBMIT-PREFILL]` diagnostic will surface it in live use. A DW card tracking this edge case is warranted — recommended for `06-deferred-backlog.md` by ptt-architect in the next phase.

2. **Line number discrepancy on `IsPttCopyEntry`:** no-pipeline-repairs.md cites L8309; plan cites L8313. Both reference identical predicate content. This is an inconsequential 4-line drift from source edits between documents — does not affect correctness.

3. **T3 CYC note:** Plan claims T3 introduces no new CYC vertex (modifying `&&` into an existing condition). This is technically correct by McCabe's edge-minus-node formula — the boolean short-circuit in `&&` adds a predicate evaluation path but the condition replaces (not adds) a branch node. The claim of CYC 5 after T3 and CYC 6 only after T4 is accurate.

---

## Spec Coverage Matrix

| Requirement | Source | Addressed? | Plan Section |
|-------------|--------|-----------|--------------|
| Distinguish pre-fill flat from post-flatten flat in drain guard | DW-B167-01 Root Cause | ✅ Yes | §2 |
| Store position snapshot at drain-initiation time | DW-B167-01 Fix Design | ✅ Yes | §2 T1+T2 |
| Use `HadPositionAtDrainTime` (not `WasFlat`) as field name | Review mandate | ✅ Yes | §2 CRITICAL Naming Rule |
| Guard: `payload.HadPositionAtDrainTime && IsFlat(...)` | Review mandate check 3 | ✅ Yes | §2 T3 |
| `bool hadPos = !IsFlat(...)` at drain-time | Review mandate check 5 | ✅ Yes | §2 T2 |
| `[DRAIN-SUBMIT-PREFILL]` diagnostic on pre-fill path | Review mandate check 6 | ✅ Yes | §2 T4 |
| Zero changes to `AbortDrainOnFill` | Review mandate check 7 | ✅ Yes | §2 scope |
| CYC ≤8 on all modified methods | JS-CYC rule | ✅ Yes | §2 CYC Proof |
| Formal record for DW-B167-02 `TrySetSlateReadyOnDrainFill` repair | DW-B167-02 / no-pipeline-repairs.md | ✅ Yes | §3 |
| READ-ONLY confirmation (no new edits) for Ticket 2 | Review mandate check 9–10 | ✅ Yes | §3 |
| `IsPttCopyEntry` at L8313 verified with both clauses | Review mandate check 12 | ✅ Yes | §3 |
| SIM gate criteria for both tickets | Review mandate check 13 | ✅ Yes | §7 |
| No changes to `IsFollowerPositionBlocked` | Review mandate check 18 | ✅ Yes | §2–§3 scope |
| No changes to B161–B166 fixes | Review mandate check 19 | ✅ Yes | §2–§3 scope |
| No premature fix of ATM bracket architecture (DW-B167-02 primary) | Review mandate check 20 | ✅ Yes | §3 |
| 4 `[Fact]` unit tests for Ticket 1 | Review mandate check 21 | ✅ Yes | §5 |
| 1 structural confirmation test for Ticket 2 | Review mandate check 22 | ✅ Yes | §5 |
| ECA-1 acknowledged and deferred | Review mandate check 23 | ✅ Yes | §6 |

---

## Final Verdict

**REVIEW_PASS**

The plan is correct, complete, and compliant. No violations. ptt-architect may proceed to Phase 3 (ticket generation).
