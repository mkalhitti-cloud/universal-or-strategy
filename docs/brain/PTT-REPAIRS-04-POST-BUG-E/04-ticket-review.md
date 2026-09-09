# Ticket Review: PTT-REPAIRS-04-POST-BUG-E

**Epic:** PTT-REPAIRS-04-POST-BUG-E
**Phase:** 3.5 -- Ticket Review
**Reviewer:** ptt-ticket-reviewer
**Date:** 2026-09-06
**Ticket reviewed:** docs/brain/PTT-REPAIRS-04-POST-BUG-E/04-tickets.md
**Plan reviewed:** docs/brain/PTT-REPAIRS-04-POST-BUG-E/02-architecture-plan.md
**Plan status gate:** REVIEW_PASS confirmed (02-plan-review.md, all 10 items, 2026-09-06)

---

## TICKET-1 -- BUG-E: EvictDedup Cancelled branch clears _lastLeaderDirection

### Traceability

**1. Spec requirement ID PTT-REPAIRS-04-BUG-E cited:**
PASS
Ticket line 14: `**Spec requirement IDs:** PTT-REPAIRS-04-BUG-E`

**2. Architecture plan Section 3 and Section 4 referenced:**
PASS
Ticket line 15: `**Architecture plan ref:** Section 3 (fix description), Section 4 (CYC accounting), Section 5 (JS compliance table)`
Both Section 3 and Section 4 explicitly cited.

**3. Scope bounded to SOURCE VERIFICATION ONLY (no .cs edits proposed):**
PASS
Ticket lines 17-19: `**Scope:** SOURCE VERIFICATION ONLY -- the fix is already applied. No .cs edits are required unless a verification step shows the fix is absent (in which case the engineer must report to the Director before making any edit).`
No edit instructions, no `apply_diff` blocks, no line rewrites appear anywhere in the ticket body.

---

### 7-Scan Checklist Presence

**4. All 7 scans present with specific commands or descriptions:**
PASS

| Scan | Ticket Lines | Command / Description Present | Result |
|------|-------------|-------------------------------|--------|
| Scan 1 -- lock() | 84-87 | `Select-String -Path "src\PropTraderTools\CopyEngine.cs" -Pattern "\block\s*\("` | PASS |
| Scan 2 -- non-ASCII | 88-91 | `Select-String -Path "src\PropTraderTools\CopyEngine.cs" -Pattern "[^\x00-\x7F]"` | PASS |
| Scan 3 -- CYC check | 92-99 | Decision-token count instructions with method scope (5851-5895), expected D=12/CYC=13 | PASS |
| Scan 4 -- build | 101-104 | `dotnet build Linting.csproj /nologo 2>&1 \| Select-String "CopyEngine"` | PASS |
| Scan 5 -- fix presence | 106-114 | Lines 5878-5882 with verbatim expected content per line | PASS |
| Scan 6 -- comment presence | 116-119 | "PTT-REPAIRS-04 BUG-E" at line 5878 | PASS |
| Scan 7 -- hard-link | 121-124 | `fsutil hardlink list "src\PropTraderTools\CopyEngine.cs"` | PASS |

All 7 scans are present. The per-ticket 7-scan checklist (Layer 1 engineer contract) is intact.

---

### Verification Steps

**5. All 8 verification steps present with specific line numbers:**
PASS

| Step | Ticket Lines | Line Numbers Cited | Pass/Fail Criterion Present |
|------|-------------|-------------------|----------------------------|
| Step 1 | 131-137 | 5845-5895, 5851, 5862-5884, 5886+ | Yes: "Confirm method signature at line 5851", "Confirm...are present" |
| Step 2 | 140-143 | 5872, 5862 | Yes: "Cite the exact line number" |
| Step 3 | 144-155 | 5878-5882 individually | Yes: "Confirm lines 5878-5882 contain exactly the following" |
| Step 4 | 157-160 | (Scan 1 ref) | Yes: "Result must be zero matches...halt and report" |
| Step 5 | 161-163 | (Scan 2 ref) | Yes: "Result must be zero matches...halt and report" |
| Step 6 | 164-168 | 5851-5901, 5878, 5881 | Yes: "Expected: D=12, CYC=13. Report any unexpected additions." |
| Step 7 | 169-172 | (Scan 4 ref) | Yes: "Result must be zero CopyEngine errors...halt and report" |
| Step 8 | 173-178 | (Scan 7 ref) | Yes: "Result must show exactly 2 paths" |

**6. Step 3 quotes exact expected code from lines 5878-5882:**
PASS
Ticket lines 147-151 quote:
```
line 5878: // PTT-REPAIRS-04 BUG-E: entry cancelled without fill -- direction record is stale.
line 5879: // Clear _lastLeaderDirection so next entry in any direction is not reversal-blocked.
line 5880:     var pipeIdx = cancelledInstrKey.IndexOf('|');
line 5881:     if (pipeIdx > 0)
line 5882:         _lastLeaderDirection.TryRemove(cancelledInstrKey.Substring(0, pipeIdx), out _);
```
Matches plan Section 3 "After" block (plan lines 105-116) and plan review Item 5 table exactly.

**7. Each step has a clear pass/fail criterion:**
PASS
Every step specifies "Required result:" and "Failure action:" (Steps 1-3, 4-8 equivalently structured via required result + halt instruction). Criteria are unambiguous.

---

### JS Pre-Check

**8. JS-021 (no lock) listed:**
PASS
Ticket line 66: `JS-021 | EvictDedup | No lock() anywhere in method or fix lines | ENFORCED -- PASS`

**9. JS-013 (CYC) listed with DEFERRED classification and correct rationale:**
PASS
Ticket line 67: `JS-013 | EvictDedup | CYC <= 8 per method. Post-fix CYC=13 (D=12). Pre-fix CYC=12 (D=11). BUG-E adds +1 to pre-existing violation. | DEFERRED (pre-existing, see DEFERRED-4)`
Ticket lines 72-76 further explain: "The pre-existing CYC=12 (D=11) violated JS-013 before the BUG-E fix was written. BUG-E adds one decision token `if (pipeIdx > 0)` at line 5881, yielding CYC=13 (D=12). BUG-E did NOT introduce the violation. DEFERRED-4 extraction is planned for a future session but is OUT OF SCOPE for this pipeline. The DEFERRED classification is binding per Director scoping decision."
Rationale is complete, accurate, and consistent with plan Section 4 and plan review Item 7.

**10. JS-042 (ASCII-only) listed:**
PASS
Ticket line 69: `JS-042 | EvictDedup | ASCII-only in all new/changed lines (5878-5882) | ENFORCED -- PASS`

---

### Completeness

**11. Acceptance criteria includes BUILD_PASS and VERIFY_READY declarations:**
PASS
Ticket lines 184-185:
- `BUILD_PASS declared (zero CopyEngine errors from Scan 4)`
- `VERIFY_READY declared (all steps pass)`
Both declarations are explicitly required in the acceptance criteria block.

**12. Deferred test spec present with name, Arrange, Act, Assert, and DEFERRED-1 note:**
PASS

| Element | Ticket Lines | Content |
|---------|-------------|---------|
| Test name | 197 | `EvictDedup_CancelledEntry_ClearsLastLeaderDirection` |
| Arrange | 202-203 | `SetLeaderDirection_ForTest(...)`, `IsLiveEntryBlocked_ForTest(...)` |
| Act | 208 | `EvictDedup("ord-1", OrderState.Cancelled)` |
| Assert | 213 | `HasLeaderDirection_ForTest("MGC DEC26") == false` |
| DEFERRED-1 note | 222-224 | "**Status:** DEFERRED. CopyEngineTests.cs is NOT currently executable. This test must not be added to the test file until the Option A InternalsVisibleTo session is completed..." |

DEFERRED-1 is the label assigned in plan Section 6. The test spec satisfies all required elements.

**13. No .cs edits proposed anywhere in ticket body:**
PASS
Ticket is strictly read/verify/scan. No write instructions, no diff blocks, no line edits anywhere.

**14. NT8 constraints respected (no lock() references in proposed code):**
PASS
No `lock()` statement appears in any described or proposed code path. Scan 1 (lock() scan) is
explicitly included as a required verification step with `zero matches` as the required result.
The JS-021 row is classified ENFORCED--PASS.

---

### CYC Pre-Check

**15. Ticket's CYC claim (D=12, CYC=13 post-fix, pre-existing violation) consistent with plan review Section 7:**
PASS

| Source | D | CYC | Pre-fix |
|--------|---|-----|---------|
| Plan Section 4 (plan lines 166, 172-173) | 12 | 13 | D=11, CYC=12 |
| Plan review Section 7 (plan-review lines 146-148) | 12 | 13 | D=11, CYC=12 |
| Ticket line 67 | 12 | 13 | D=11, CYC=12 |

All three sources agree exactly. The plan reviewer performed an independent count and confirmed.

---

### File Routing

File: `src/PropTraderTools/CopyEngine.cs` (ticket line 16).
This is the Wave workspace source path (c:\WSGTA\universal-or-strategy\src\PropTraderTools\).
No Director-workspace .cs path referenced. PASS.

---

## Checklist Summary

| # | Item | Result |
|---|------|--------|
| 1 | Spec req ID PTT-REPAIRS-04-BUG-E cited | PASS |
| 2 | Plan Section 3 and Section 4 referenced | PASS |
| 3 | Scope bounded to SOURCE VERIFICATION ONLY | PASS |
| 4 | All 7 scans present with commands/descriptions | PASS |
| 5 | All 8 verification steps with line numbers | PASS |
| 6 | Step 3 quotes exact lines 5878-5882 | PASS |
| 7 | Each step has clear pass/fail criterion | PASS |
| 8 | JS-021 (no lock) listed | PASS |
| 9 | JS-013 DEFERRED with pre-existing rationale and Director scoping | PASS |
| 10 | JS-042 (ASCII-only) listed | PASS |
| 11 | BUILD_PASS and VERIFY_READY in acceptance criteria | PASS |
| 12 | Deferred test spec: name, Arrange, Act, Assert, DEFERRED-1 note | PASS |
| 13 | No .cs edits proposed anywhere in ticket | PASS |
| 14 | NT8 constraints respected (no lock() in proposed code) | PASS |
| 15 | CYC claim consistent with plan review Section 7 | PASS |

---

## Overall: TICKET_REVIEW_PASS

All 15 checklist items PASS. Zero violations found.

**TICKET_REVIEW_PASS**

Ph4a engineer is cleared to proceed.
