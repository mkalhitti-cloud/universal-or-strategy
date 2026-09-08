# PTT-REPAIRS-01 Ticket Review
**Status**: TICKET_REVIEW_PASS
**Phase**: 3.5 (Ticket Review)
**Epic**: PTT-REPAIRS-01
**Reviewer**: ptt-ticket-reviewer
**Cycle**: 2 of 2 (final)
**Date**: 2026-09-07
**Plan**: `docs/brain/PTT-REPAIRS-01/02-architecture-plan.md` (REVIEW_PASS)
**Tickets**: `docs/brain/PTT-REPAIRS-01/04-tickets.md`
**Prior Review**: `docs/brain/PTT-REPAIRS-01/04-ticket-review.md` (Cycle 1 — TICKET_REVIEW_FAIL)

---

## Cycle 2 Re-Review Scope

Cycle 1 returned TICKET_REVIEW_FAIL with one blocking violation:

> **TICKET-VIOLATION [3.1]**: `Execute(forcedTargets)` CYC=9 permitted by Ticket 3 — JS-066 (CYC <= 8) violation. SCAN-06 gate language `expected CYC = 9 (acknowledged, see note)` and Engineer Return Gate `Execute(forcedTargets) CYC documented (9 or reduced via further extraction)` both permitted a CYC=9 BUILD_PASS. Required fix: mandate extraction of the inner `foreach (Position pos in acc.Positions)` body to a private helper.

This cycle reviews only the Ticket 3 changes required to resolve [3.1]. Tickets 1 and 2 are confirmed unchanged.

---

## Blocking Violation Resolution Check — TICKET-VIOLATION [3.1]

### Fix Checklist

**[1] Step R6.0 or equivalent extraction step present in Ticket 3**
RESULT: YES. `04-tickets.md` lines 1218-1333 contain a new `Step R6.0 — Extract Inner Loop Body of Execute(forcedTargets)` that adds a new private method `ProcessForcedTargetPosition(Account acc, Position pos, List<(double Price, int Qty)> forcedTargets, CopyEngine engine)` and replaces the `foreach (Position pos in acc.Positions)` body in `Execute(forcedTargets)` with a single call `ProcessForcedTargetPosition(acc, pos, forcedTargets, engine)`. PASS.

**[2] Extraction reduces `Execute(forcedTargets)` CYC so adding the R6 guard keeps it <= 8**
RESULT: YES. The extraction removes branches (5) pos-null/flat guard, (6) flatten guard, and (7) ExecuteFollowers-call from `Execute(forcedTargets)`, moving them to `ProcessForcedTargetPosition`. The R6 guard (`if (_beCancelCount < 0) return;`) is placed INSIDE `ProcessForcedTargetPosition`, NOT added to `Execute(forcedTargets)`. Net `Execute(forcedTargets)` CYC = 5 (branches 1 flag-guard + 2 IsInvalidForcedTargets + 3 acc-loop + 4 follower-skip + 5 pos-loop). No additional branch is added to the caller. CYC summary table in Ticket 3 correctly records `Execute(forcedTargets)` before=8, after=5. PASS.

**[3] SCAN-06 now requires `Execute(forcedTargets)` CYC <= 8 — no "CYC=9 acknowledged" language**
RESULT: YES. Updated SCAN-06 reads: `Execute(forcedTargets) expected CYC = 5 (was 8, extraction reduces -- PASS)`. The `expected CYC = 9 (acknowledged, see note -- REVIEW WITH CI)` language from cycle 1 is gone. PASS.

**[4] Engineer Return Gate no longer permits CYC=9**
RESULT: YES. Updated Engineer Return Gate SCAN-06 item reads: `Execute(forcedTargets) CYC=5 (required)`. The old `Execute(forcedTargets) CYC documented (9 or reduced via further extraction)` language is gone. CYC=9 is not mentioned as an acceptable BUILD_PASS condition anywhere in the gate. PASS.

**[5] New helper method `ProcessForcedTargetPosition` has CYC <= 8**
RESULT: YES. Ticket claims CYC=4. Manual recount: base(1) + compound null guard `pos==null || pos.Quantity==0`(1 compound branch) + R6 return guard `if (_beCancelCount < 0)`(1) + `NeedsLeaderFallbackFlatten` guard(1) = CYC=4 to CYC=5 depending on Lizard's `||` counting. Either value passes JS-066 (<=8). PASS.

**[6] DW-REPAIRS-01-03 removed from deferred section**
RESULT: YES. The deferred table at `04-tickets.md` lines 1511-1524 contains DW-REPAIRS-01-01 and DW-REPAIRS-01-02 only. DW-REPAIRS-01-03 (CYC=9 deferred item) is removed. PASS.

**[7] R6 catch→return -1 fix still present**
RESULT: YES. `CancelPttBeOrders` AFTER code (lines 1100-1155) retains the full try/catch with `return -1;` in the catch block. PASS.

**[8] All call sites of `CancelPttBeOrders` still updated**
RESULT: YES. Three call sites are addressed:
- Step R6-C: `Execute()` no-arg (line ~60) — replaces `CancelPttBeOrders` + `WaitForPttBeCancelled` with `TryCancelBeOrders` + guard. PASS.
- Step R6.0: `Execute(forcedTargets)` — inner loop body extracted to `ProcessForcedTargetPosition`, which calls `TryCancelBeOrders` internally. PASS.
- Step R6-E: `ExecuteFollowers()` (line ~214) — replaces `CancelPttBeOrders` + `WaitForPttBeCancelled` with `TryCancelBeOrders` + guard. PASS.
Step R6-D is superseded by R6.0 (noted explicitly in ticket). PASS.

**[9] T_R6 test still present with correct body**
RESULT: YES. `T_R6_CancelPttBeOrders_ReturnsNegativeOneOnException` at lines 1385-1415 is unchanged from cycle 1: verifies `CancelPttBeOrders` is internal static, returns int, null args return 0, and `TryCancelBeOrders` exists as private instance with correct (Account, Instrument) -> int signature. PASS.

**[10] SCAN-01 through SCAN-07 complete**
RESULT: YES. All 7 scans present at lines 1422-1477 with correct file paths, expected counts, and explicit CYC gate for all 6 changed methods. PASS.

### TICKET-VIOLATION [3.1] — RESOLVED

---

## Cycle 2 — Ticket 3 Full Re-Review

### T3 — Traceability

| Spec Item | Covered in Ticket 3? | Plan Reference |
|-----------|----------------------|----------------|
| R6 | YES — CancelPttBeOrders try/catch + 3 call site updates | Plan §3/R6 |
| TryCancelBeOrders helper | YES — Step R6-B | Plan §6 |
| ProcessForcedTargetPosition (NEW in cycle 2) | YES — Step R6.0 | Plan §6 ("extraction MANDATORY") |
| All 3 call sites | YES — R6-C (Execute no-arg), R6.0 (forcedTargets), R6-E (ExecuteFollowers) | Plan §6 |
| No phantom items | YES — only R6 scope | — |

DW-REPAIRS-01-03 (deferred CYC=9 item) removed — confirms the extraction is now mandatory, not deferred.

**Traceability**: PASS

### T3 — JS Pre-Check

- `CancelPttBeOrders` AFTER: catch swallows, logs, returns -1. No `throw new`. No `lock()`. No `return null`. JS-001 PASS.
- `TryCancelBeOrders`: returns int (-1/0/>0). No `return null`. No `lock()`. No `throw new`. PASS.
- `ProcessForcedTargetPosition`: `void` return. No `return null`. No `lock()`. No `throw new`. PASS.
- `Execute()` no-arg call site: `if (_beCancelCount < 0) continue;` — boolean int check, no JS violations. PASS.
- `ExecuteFollowers()` call site: same pattern. PASS.
- `Execute(forcedTargets)` AFTER: straight-line call `ProcessForcedTargetPosition(acc, pos, forcedTargets, engine)` — no JS violations. PASS.
- All log strings are ASCII-only. PASS.
- JS-033: No async void in changed methods. PASS.

**JS Pre-Check**: PASS

### T3 — CYC Pre-Check

| Method | Before | After | Status |
|--------|--------|-------|--------|
| `CancelPttBeOrders` | 7 | 8 | PASS (at limit, <=8) |
| `TryCancelBeOrders` (NEW) | — | 2 | PASS |
| `ProcessForcedTargetPosition` (NEW) | — | 4 | PASS |
| `Execute()` no-arg | 7 | 8 | PASS (at limit, <=8) |
| `Execute(forcedTargets)` | 8 | **5** | PASS (extraction reduces from 8 to 5) |
| `ExecuteFollowers()` | 7 | 8 | PASS (at limit, <=8) |

Source verification: `Execute(forcedTargets)` in `PttGlobalQuickExit.cs` (lines 110-111) carries existing XML comment `CYC=8: flag-guard(1), IsInvalidForcedTargets(2), acc-loop(3), follower-skip(4), pos-loop(5), null/flat-continue(6), flatten-guard(7), ExecuteFollowers-call(8).` This matches the ticket's BEFORE=8 claim exactly. The Step R6.0 extraction removes branches 5/6/7/8 from the caller and places them (plus the R6 guard) inside `ProcessForcedTargetPosition`. Resulting caller CYC=5 is architecturally sound and correctly computed.

**CYC Pre-Check**: PASS

### T3 — NT8 Check

- `acc.Orders.ToList()` in try block: safe snapshot copy pattern. PASS.
- `acc.Cancel(toCancel)` in try block: AddOnBase-available pattern. PASS.
- `acc?.Name ?? "null"` in catch log: null-conditional safe. PASS.
- `ProcessForcedTargetPosition`: `acc.Flatten(new[] { pos.Instrument })` — standard NT8 flatten pattern. PASS.
- No `async/await` in lifecycle methods. PASS.
- No `FontFamily` on WPF elements. PASS.
- No hardcoded hex colors. PASS.
- No `Account.All` call outside Loaded/UI path (Execute methods are called from UI thread). PASS.

**NT8 Check**: PASS

### T3 — Test Coverage

| Method | Visibility | [Fact] Test |
|--------|-----------|-------------|
| `CancelPttBeOrders` | internal static | `T_R6_CancelPttBeOrders_ReturnsNegativeOneOnException` — exists, is static, returns int, null args return 0. PASS. |
| `TryCancelBeOrders` | private | `T_R6_CancelPttBeOrders_ReturnsNegativeOneOnException` — exists, is non-static, signature (Account, Instrument)->int. PASS. |
| `ProcessForcedTargetPosition` | private void | No dedicated [Fact]. ADVISORY only — private method; role definition FAIL threshold applies to public or internal only. |
| `Execute()` no-arg (modified) | internal | No new test — modification is a 3-line call-site change. Structural coverage via T_R6 confirms method exists. PASS (structural). |
| `ExecuteFollowers()` (modified) | private | No new test — modification is a 3-line call-site change. PASS (private + structural). |

**Test Coverage**: PASS (all public/internal methods have [Fact] coverage; private method gap is advisory)

### T3 — Scan Checklist

| Scan | Present? | Quality |
|------|----------|---------|
| SCAN-01 | YES | Correct file path, 0 expected |
| SCAN-02 | YES | Correct file path, 0 expected |
| SCAN-03 | YES | Lists `CancelPttBeOrders`, `TryCancelBeOrders`, `Execute()`, `Execute(forcedTargets)`, `ExecuteFollowers()` — catch swallows, no re-throw |
| SCAN-04 | YES | All changed methods return int or void — 0 expected |
| SCAN-05 | YES | 3 specific verification patterns: `return -1` >= 1; `TryCancelBeOrders` >= 4; `WaitForPttBeCancelled` check at call sites |
| SCAN-06 | YES | All 6 changed/new methods listed with expected CYC values; `Execute(forcedTargets)` gate = `CYC = 5 (required)` — enforces JS-066 |
| SCAN-07 | YES | Pattern covers `*.cs` glob, 0 expected |

**Scan Checklist**: PASS

### T3 — File Routing

All `.cs` source paths point to `src/PropTraderTools/Features/PttGlobalQuickExit.cs` and `src/PropTraderTools/CopyEngineTests.cs`. No Director workspace paths. PASS.

**File Routing**: PASS

### T3 — Engineer Return Gate

Gate contains 10 conditions:
- SCAN-01 through SCAN-07: all present with concrete expected values.
- SCAN-06 explicitly: `Execute(forcedTargets) CYC=5 (required)` — this is a blocking condition that enforces JS-066. PASS.
- Build: 0 errors, 0 warnings.
- Tests: exactly 306 `[Fact]` methods (305 after Ticket 2 + T_R6).
- `ptt-sync-and-verify.ps1` = 0 MISMATCH lines.

**Engineer Return Gate**: PASS

### VERDICT — Ticket 3 (Cycle 2): TICKET_REVIEW_PASS

---

## Tickets 1 and 2 — Unchanged Confirmation

Ticket 1 (lines 18-527) and Ticket 2 (lines 529-1025) of `04-tickets.md` are confirmed identical to their cycle 1 content. No accidental edits detected.

| Check | Result |
|-------|--------|
| Ticket 1 covers G1, R1, R2 exactly | YES — unchanged |
| Ticket 2 covers R3, R4, R5 exactly | YES — unchanged |
| Ticket 1 VERDICT remains TICKET_REVIEW_PASS | YES |
| Ticket 2 VERDICT remains TICKET_REVIEW_PASS | YES |

---

## Advisory Violations (non-blocking, cycle 2)

**ADVISORY [3.3]: Stale CYC=9 comment in `TryCancelBeOrders` comment block.**
- Location: `04-tickets.md` lines 1168-1175 (Step R6-B comment block).
- The comment states `"CYC=9 is acceptable per reviewer (extraction is the correct architectural pattern even if the +1 guard cannot be eliminated). Verify with 'python scripts/complexity_audit.py' before commit."` This text predates Step R6.0 and no longer applies — the extraction is now mandatory and `Execute(forcedTargets)` CYC=5 is required by the gate.
- Impact: misleading to a future maintainer reading the helper comment in isolation. No impact on engineer execution because SCAN-06 and the Engineer Return Gate override it with `Execute(forcedTargets) CYC=5 (required)`.
- Severity: ADVISORY. No blocking impact. Engineer follows the gate, not the helper comment.

**ADVISORY [3.4]: No [Fact] test for `ProcessForcedTargetPosition` (private void helper).**
- Per role definition, the FAIL threshold applies to public or internal methods only. `ProcessForcedTargetPosition` is `private void`. Its behavior is exercised transitively through T_R6 (which confirms `TryCancelBeOrders` exists and is callable) and the build/test gate.
- Severity: ADVISORY. Non-blocking.

**Carried forward advisories from cycle 1:**
- **ADVISORY [1.1]**: Test name `T_R1_IsNakedConditionMet_FullNameEquality` does not name the methods actually tested. Test body is correct. Non-blocking.
- **ADVISORY [2.1]**: Plan states `ReissueDrainCancels` CYC=4; ticket claims CYC<=7 (counts `||` as extra branch). Both pass JS-066. Verifier should run Lizard to confirm.

---

## Spec Coverage (Aggregate — unchanged)

| Spec Item | Ticket | Status |
|-----------|--------|--------|
| G1 | Ticket 1 | PASS |
| R1 | Ticket 1 | PASS |
| R2 | Ticket 1 | PASS |
| R3 | Ticket 2 | PASS |
| R4 | Ticket 2 | PASS |
| R5 | Ticket 2 | PASS |
| R6 | Ticket 3 | PASS (CYC violation resolved in cycle 2) |

All 7 spec items (G1 + R1–R6) covered across 3 tickets. No uncovered items. No duplicate coverage.

---

## Per-Ticket Summary

| Ticket | Traceability | JS Pre-Check | CYC Pre-Check | NT8 Check | Test Coverage | Scan Checklist | File Routing | VERDICT |
|--------|-------------|--------------|---------------|-----------|---------------|----------------|--------------|---------|
| T1 | PASS | PASS | PASS | PASS | PASS | PASS | PASS | TICKET_REVIEW_PASS |
| T2 | PASS | PASS | PASS | PASS | PASS | PASS | PASS | TICKET_REVIEW_PASS |
| T3 | PASS | PASS | PASS | PASS | PASS | PASS | PASS | TICKET_REVIEW_PASS |

---

## Overall: TICKET_REVIEW_PASS

**Cycle 2 outcome**: The sole blocking violation from cycle 1 (TICKET-VIOLATION [3.1] — JS-066 / `Execute(forcedTargets)` CYC=9 permitted) is fully resolved. Step R6.0 mandates extraction of the `foreach (Position pos in acc.Positions)` body to `ProcessForcedTargetPosition`, reducing `Execute(forcedTargets)` from CYC=8 to CYC=5. The R6 guard is placed inside the helper (not the caller). SCAN-06 and the Engineer Return Gate now enforce `Execute(forcedTargets) CYC=5 (required)`. No new blocking violations are introduced.

**Green light**: Safe to spawn ptt-engineer on all three tickets in sequential order (Ticket 1 → Ticket 2 → Ticket 3).
