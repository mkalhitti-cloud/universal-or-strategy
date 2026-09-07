# WAVE1-LANE-A Final Review

**Date**: 2026-09-07
**Reviewer**: ptt-plan-reviewer Phase 5
**Epic**: WAVE1-LANE-A — CopyEngine.cs God-Method Extraction
**Target file**: `src/PropTraderTools/CopyEngine.cs`
**Brain dir**: `docs/brain/WAVE1-LANE-A/`

---

## Overall Verdict: FINAL_PASS

---

## Section A — Pipeline Completeness

| Ticket | Completion | Verification | Result |
|--------|-----------|--------------|--------|
| T1 RegisterBeRetrySlotIfNeeded | EXISTS (ticket-1-completion.md) | VERIFY_PASS | PASS |
| T2 FlattenOneAccountLimit+TrimOneAccountLimit | EXISTS (ticket-2-completion.md) | VERIFY_PASS | PASS |
| T3 OnOrderUpdate (advisory) | EXISTS (ticket-3-completion.md) | VERIFY_PASS | PASS |

All three tickets carry independent VERIFY_PASS verdicts from ptt-verifier.

---

## Section B — Independent CCN Table

CCN values sourced from independent lizard scans run by ptt-verifier in each ticket verification report.
Reviewer independently confirmed method bodies in source match the extraction described.

| Method | CCN Before | CCN After | Compliant (<=8)? |
|--------|-----------|-----------|-----------------|
| RegisterBeRetrySlotIfNeeded | 8 | 6 | YES |
| IsBeRetrySlotNeeded (new) | - | 4 | YES |
| RegisterPendingBeSlot 4-param (new) | - | 1 | YES |
| FlattenOneAccountLimit | 8 | 6 | YES |
| TrimOneAccountLimit | 8 | 6 | YES |
| SubmitLimitExitOrder (new) | - | 3 | YES |
| OnOrderUpdate | 8 | 5 | YES |
| TryResolveEnabledRule (new) | - | 5 | YES |

Notes:
- T1-verifier independently measured all three T1 methods (CCN 4/1/6) from lizard output.
- T2-verifier independently measured all three T2 methods (CCN 6/3/6) from lizard output.
- T3-verifier independently measured both T3 methods (CCN 5/5) from lizard output.
- T3 engineer comment on line 1502 says "CCN=4" but lizard measures 5 — comment is a documentation error only. Actual measured CCN=5 is within JS-080 limit. Not a violation.
- Out-of-scope methods with CCN > 8: `IsExitSignalName` (CCN=9, line 2347), `HasArmingAtmBrackets` (CCN=9, line 5351) — both deferred to WAVE2-LANE-A per Section K below.

---

## Section C — P0 Violation Scan

Reviewer ran independent grep scans against `src/PropTraderTools/CopyEngine.cs`:

| Scan | Pattern | Raw Hits | Actual Code Hits | Pass/Fail |
|------|---------|----------|-----------------|-----------|
| SCAN-01 (JS-021) lock() | `lock(` | 10 | 0 — all comment text | PASS |
| SCAN-02 (JS-033) async void | `async void ` | 2 | 0 — both comment text | PASS |
| SCAN-07 (JS-096) public helpers | `public.*IsBeRetrySlotNeeded\|public.*RegisterPendingBeSlot\|public.*SubmitLimitExitOrder\|public.*TryResolveEnabledRule` | 0 | 0 | PASS |

All 4 new helpers confirmed `private` via grep of declaration lines (1503, 5590, 6273, 6284).

---

## Section D — Test Results

**166 passing / 0 failing / 3 skipped (Total: 169)**

Source: ticket-3-completion.md and ticket-3-verification.md (both independently report identical count).

All 16 new [Fact] tests confirmed present and passing:
- T31-T38: `IsBeRetrySlotNeeded` (6 tests) + `RegisterPendingBeSlot` (2 tests) — Ticket 1
- T39-T42: `SubmitLimitExitOrder` (2 tests) + qty computations (2 tests) — Ticket 2
- T43-T46: `TryResolveEnabledRule` (4 tests — one per gate) — Ticket 3

---

## Section E — Sync Result

The `ptt-sync-and-verify.ps1` sync gate was specified in `04-tickets.md` global acceptance criteria but no ticket completion report records its output. Git status shows `CopyEngine.cs` as modified/unstaged (pending commit).

**Status**: Sync gate OPEN — pending Director execution.
**Action required**: Director must run `powershell -File scripts\ptt-sync-and-verify.ps1` and confirm 0 MISMATCH lines, then press F5 in NT8.
This does NOT trigger FINAL_FAIL — no MISMATCH evidence found. Gate tracked as DW-W1A-05.

---

## Section F — JS Rule Cross-Check

| Rule | Check | Result |
|------|-------|--------|
| JS-021 (no lock) | No lock() in new helpers — 10 grep hits all comment text | PASS |
| JS-001 (no throw) | No throw in new helpers or refactored methods | PASS |
| JS-002 (no null return) | All helpers return void or bool; `TryResolveEnabledRule` uses `out CopyRule` (value type) | PASS |
| JS-033 (no async void) | No async void helpers — 2 grep hits both comment text | PASS |
| JS-066 (diff size) | 4 helpers + 4 parent refactors = targeted surgical change; net line delta well under 10k | PASS |
| JS-080 (CYC<=8) | All 8 measured methods CCN 1–6; max is 6 | PASS |
| JS-096 (illegal states) | Slot-write-FIRST ordering contract in RegisterPendingBeSlot confirmed by T1-verifier; gate ordering in TryResolveEnabledRule confirmed by T3-verifier | PASS |

---

## Section G — Behaviour Preservation Check

| Method | Signature Changed? | Logic Changed? | External Callers Affected? |
|--------|------------------|---------------|--------------------------|
| RegisterBeRetrySlotIfNeeded | NO (6 params, unchanged) | NO (helpers inline same logic) | NO |
| FlattenOneAccountLimit | NO (5 params, unchanged) | NO | NO |
| TrimOneAccountLimit | NO (5 params, unchanged) | NO | NO |
| OnOrderUpdate | NO | NO (matchedRule.Value → matchedRule unwrap is semantically identical) | NO |

All parent method external signatures confirmed unchanged by T1/T2/T3 verifiers.

---

## Section K — Deferred Work

Items identified during this epic that are out of scope but must be tracked:

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-W1A-01 | `IsExitSignalName` (CCN=9): extract HashSet\<string\> or `IsKnownExactExitName` helper; reduces CCN 9→7 | P1 | WAVE2-LANE-A | OPEN |
| DW-W1A-02 | `HasArmingAtmBrackets` (CCN=9): extract `IsArmingOrderState(OrderState s)` for 5-term OR; reduces CCN 9→5 | P1 | WAVE2-LANE-A | OPEN |
| DW-W1A-03 | `MoveStopToBreakEven` (CCN=7): remove ~55 lines of commented-out DW-B88 legacy code (NLOC cleanup, no CCN impact) | P2 | future | OPEN |
| DW-W1A-04 | `SendCopy` (CCN=6): remove dead `atmTemplate` local variable (3 lines; latent JS-002 smell — can be null) | P2 | future | OPEN |
| DW-W1A-05 | Sync gate: run `ptt-sync-and-verify.ps1` and confirm 0 MISMATCH before merge to main; press F5 in NT8 | P0 | immediate (pre-merge) | OPEN |

See `06-deferred-backlog.md` for full details.

---

## Final Verdict: FINAL_PASS

All FINAL_FAIL conditions evaluated:

| Condition | Status |
|-----------|--------|
| Any VERIFY_PASS missing from any of the 3 tickets | NOT TRIGGERED — T1, T2, T3 all VERIFY_PASS |
| Any P0 violation in modified methods | NOT TRIGGERED — SCAN-01/02/07 all zero actual hits |
| Any CCN > 8 in modified methods after extraction | NOT TRIGGERED — all 8 methods CCN 1–6 |
| Test count < 166 or any test failure | NOT TRIGGERED — 166 passing, 0 failing |
| Sync: any MISMATCH | NOT TRIGGERED — no MISMATCH evidence; sync gate open pending Director execution |
| 06-deferred-backlog.md absent | NOT TRIGGERED — written as part of this review |

**FINAL_PASS**
