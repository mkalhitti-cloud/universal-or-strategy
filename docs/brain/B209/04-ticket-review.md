# B209 Ticket Review
# PTT-COPIER-B209: BE-ALL Permanent Lock Fix (FIX-G-1 + FIX-G-2)

**Reviewer**: ptt-ticket-reviewer
**Date**: 2026-07-07
**Tickets source**: docs/brain/B209/04-tickets.md
**Plan source**: docs/brain/B209/02-architecture-plan.md (REVIEW_PASS)
**Spec source**: docs/brain/B209-orchestrator-prompt.md
**Rules source**: docs/standards/jane-street/RULES_CATALOG.md

---

## TICKET-1 — FIX-G-1: TryClearBeSwapOnStopWorking + IsBeClearStateTrigger Seam

### 1. Traceability

Spec requirement IDs cited: B209-FIX-G-1 and B208 SIM Round 6 root-cause. Both present.
Architecture plan mapping: Ticket-1 deliverables match plan section "Ticket-1: FIX-G-1" exactly
(MODIFY TryClearBeSwapOnStopWorking, ADD IsBeClearStateTrigger after it).
No phantom work identified. No plan work missing from Ticket-1's scope.

**Traceability**: PASS

---

### 2. Scope Isolation

Scope lock statement is explicit:
- "Only TryClearBeSwapOnStopWorking is modified. Only IsBeClearStateTrigger is added."
- "Do NOT touch TryCancelSingleStaleGtcExit. Do NOT touch IsBeSwapGtcSweepGuard."
- "Do NOT create B209Tests.cs in this ticket."

FIX-G-2 work is absent from Ticket-1. B209Tests.cs creation is absent from Ticket-1.

**Scope Isolation**: PASS

---

### 3. JS Pre-Check (P0 Rules — JS-001, JS-002, JS-021, JS-033)

| Rule | Check | Result |
|------|-------|--------|
| JS-021 | No `lock()` in TryClearBeSwapOnStopWorking or IsBeClearStateTrigger bodies | PASS — uses ConcurrentDictionary |
| JS-001 | No `throw new XxxException` in either method | PASS — no throw present |
| JS-002 | No `return null` in new methods | PASS — TryClearBeSwapOnStopWorking is void; IsBeClearStateTrigger returns bool |
| JS-033 | No `async void` in modified/added code | PASS — both methods are synchronous |
| JS-015 | StringComparison.Ordinal on all StartsWith calls | PASS — present in both methods |

**JS Pre-Check**: PASS

---

### 4. CYC Pre-Check

| Method | Spec CYC | Ticket CYC Target | Max Allowed | Status |
|--------|----------|-------------------|-------------|--------|
| TryClearBeSwapOnStopWorking | 5 (spec-declared) / 7 (strict Lizard) | <= 8 | 8 | PASS |
| IsBeClearStateTrigger | ~1 expression-body / ~5 strict | <= 8 | 8 | PASS |

Both methods are well within the CYC <= 8 ceiling. Strict Lizard max of 7 noted for
TryClearBeSwapOnStopWorking. IsBeClearStateTrigger is a pure expression-body seam.

**CYC Pre-Check**: PASS

---

### 5. NT8 Constraint Check

| Check | Result |
|-------|--------|
| No async/await in lifecycle method | PASS — no async present |
| No write_file on CopyEngine.cs | PASS — ticket specifies apply_diff/search_and_replace ONLY |
| No hardcoded hex color | PASS — no UI code |
| No sealed on TradeCopierWindow | PASS — not applicable |
| No FontFamily on WPF element | PASS — not applicable |
| OrderState enum values (Working, Cancelled, Rejected, Accepted) are valid NT8 members | PASS — confirmed in plan NT8 API Surface Verification table |
| No DateTime.Now usage | PASS — not present |

**NT8 Check**: PASS

---

### 6. Test Coverage

Ticket-1 explicitly defers all test creation to Ticket-2:
"Ticket-1 does not include test creation. Tests are in Ticket-2 only."
"The IsBeClearStateTrigger seam is added here so that Ticket-2 can reference it in B209Tests.cs."

The IsBeClearStateTrigger seam is `internal static` — it is the testable surface added in Ticket-1.
Its tests are correctly scoped to Ticket-2. This is the designed decomposition per the architecture
plan. No [Fact] tests are required in Ticket-1 itself; no new public/internal methods are
introduced in Ticket-1 that go untested — IsBeClearStateTrigger's tests are in Ticket-2.

**Test Coverage**: PASS (tests scoped to Ticket-2 per design)

---

### 7. Scan Checklist Presence (DEFENSE IN DEPTH — NON-NEGOTIABLE)

All 7 scans present in Ticket-1, each with explicit command and pass criterion:

| Scan | Command | Pass Criterion | Present |
|------|---------|----------------|---------|
| SCAN-01 | `grep -rn "lock(" src/PropTraderTools/ --include="*.cs"` | zero matches in added/modified lines | YES |
| SCAN-02 | `grep -rn "async void " src/PropTraderTools/ --include="*.cs"` | zero matches in added/modified lines | YES |
| SCAN-03 | `grep -rn "return null;" src/PropTraderTools/ --include="*.cs"` | zero matches in added/modified lines | YES |
| SCAN-04 | CYC via lizard `--CCN 8` | TryClearBeSwapOnStopWorking <= 8; IsBeClearStateTrigger <= 8 | YES |
| SCAN-05 | `grep -rn "throw new" src/PropTraderTools/ --include="*.cs"` | zero matches in added/modified lines | YES |
| SCAN-06 | ASCII-only visual inspect of string literals | zero non-ASCII in new/modified lines | YES |
| SCAN-07 | `powershell -File .\scripts\ptt-sync.ps1` | N/0 (zero sync errors) | YES |

**Scan Checklist**: PASS

---

### 8. File Routing

| File | Path in Ticket | Correct Wave Workspace? |
|------|---------------|-------------------------|
| CopyEngine.cs (MODIFY) | `src/PropTraderTools/CopyEngine.cs` | PASS — Wave workspace |
| CopyEngine.cs (ADD seam) | `src/PropTraderTools/CopyEngine.cs` | PASS — Wave workspace |

No Director workspace `.cs` paths referenced.

**File Routing**: PASS

---

### 9. Post-Edit Checks Presence

| Check | Present | Command |
|-------|---------|---------|
| Guardian symbol check | YES | `powershell -File .\scripts\verify_critical_symbols.ps1` |
| Ph4b deletion audit | YES | `git diff HEAD~1 HEAD -- src/PropTraderTools/CopyEngine.cs \| Select-String "^-    (private\|internal\|public)"` |
| Single-hunk ban check | YES | `(git show HEAD -- CopyEngine.cs \| Select-String "^@@").Count -eq 1` must be False |
| Build pass requirement | YES | `dotnet build src/PropTraderTools/ --no-incremental` — zero errors, zero new warnings |

**Post-Edit Checks**: PASS

---

### TICKET-1 VERDICT: TICKET_REVIEW_PASS

---
---

## TICKET-2 — FIX-G-2: TryCancelSingleStaleGtcExit + IsBeSwapGtcSweepGuard + B209Tests.cs

### 1. Traceability

Spec requirement IDs cited: B209-FIX-G-2 and B208 SIM Round 6 root-cause. Both present.
Architecture plan mapping: Ticket-2 deliverables match plan section "Ticket-2: FIX-G-2" exactly
(MODIFY TryCancelSingleStaleGtcExit, ADD IsBeSwapGtcSweepGuard, CREATE B209Tests.cs with 10 tests).
Prerequisite gate stated: "Ticket-2 MUST NOT begin until Ticket-1 VERIFY_PASS is confirmed."
No phantom work. No plan work missing.

**Traceability**: PASS

---

### 2. Prerequisite Gate

Ticket-2 states: "The engineer must have the ticket-1-verification.md VERIFY_PASS document before starting."
This satisfies the review checklist item 3.

**Prerequisite Gate**: PASS

---

### 3. Scope Isolation

Scope lock statement is explicit:
- "Only TryCancelSingleStaleGtcExit is modified in CopyEngine.cs."
- "Only IsBeSwapGtcSweepGuard is added to CopyEngine.cs."
- "B209Tests.cs is created as a new file."
- "Do NOT modify TryClearBeSwapOnStopWorking. Do NOT modify IsBeClearStateTrigger."

FIX-G-1 work is absent from Ticket-2.

**Scope Isolation**: PASS

---

### 4. JS Pre-Check (P0 Rules — JS-001, JS-002, JS-021, JS-033)

| Rule | Check | Result |
|------|-------|--------|
| JS-021 | No `lock()` in TryCancelSingleStaleGtcExit guard block or IsBeSwapGtcSweepGuard | PASS — IsBeSwapInProgress reads ConcurrentDictionary |
| JS-001 | No `throw new XxxException` in guard or seam; existing catch block inherited unchanged | PASS — no new throw |
| JS-002 | No `return null` in modified/new methods | PASS — TryCancelSingleStaleGtcExit is void; IsBeSwapGtcSweepGuard returns bool |
| JS-033 | No `async void` in modified/added code | PASS — all methods synchronous |
| JS-015 | StringComparison.Ordinal on StartsWith("PTT-BE-", ...) in guard | PASS — present |

**JS Pre-Check**: PASS

---

### 5. CYC Pre-Check

| Method | Spec CYC | Ticket CYC Target | Max Allowed | Status |
|--------|----------|-------------------|-------------|--------|
| TryCancelSingleStaleGtcExit | delta=+1 / strict total <= 7 | <= 8 | 8 | PASS |
| IsBeSwapGtcSweepGuard | ~1 expression-body / ~3 strict | <= 8 | 8 | PASS |

Strict Lizard max of 7 total for TryCancelSingleStaleGtcExit. IsBeSwapGtcSweepGuard is
a pure expression-body seam.

**CYC Pre-Check**: PASS

---

### 6. NT8 Constraint Check

| Check | Result |
|-------|--------|
| No async/await in lifecycle method | PASS |
| No write_file on CopyEngine.cs | PASS — ticket specifies apply_diff/search_and_replace ONLY for CopyEngine.cs |
| write_file for B209Tests.cs (new file) | PASS — new file creation with write_file is explicitly permitted |
| OrderState enum values (Submitted, Working, Cancelled, etc.) valid NT8 members | PASS — confirmed in plan |
| No DateTime.Now usage | PASS |
| No hardcoded hex color / FontFamily | PASS — not applicable |

**NT8 Check**: PASS

---

### 7. Test Coverage

All 10 [Fact] tests specified with exact names, method under test, inputs, and expected outputs.

| # | Test Name | Matches Spec? | Matches Plan? |
|---|-----------|--------------|---------------|
| 1 | IsBeClearStateTrigger_Working_ReturnsTrue | YES | YES |
| 2 | IsBeClearStateTrigger_Cancelled_ReturnsTrue | YES | YES |
| 3 | IsBeClearStateTrigger_Rejected_ReturnsTrue | YES | YES |
| 4 | IsBeClearStateTrigger_Accepted_ReturnsTrue | YES | YES |
| 5 | IsBeClearStateTrigger_Submitted_ReturnsFalse | YES | YES |
| 6 | IsBeClearStateTrigger_WrongName_ReturnsFalse — input "PTT-BE-Target-1" | YES (matches spec) | NOTE: plan uses "PTT-QX-Stop-1" |
| 7 | IsBeSwapGtcSweepGuard_PttBeName_SwapActive_ReturnsTrue | YES | YES |
| 8 | IsBeSwapGtcSweepGuard_PttBeName_SwapInactive_ReturnsFalse | YES | YES |
| 9 | IsBeSwapGtcSweepGuard_NonPttBeName_SwapActive_ReturnsFalse — input "Stop1" | YES (matches spec) | NOTE: plan uses "PTT-QX-Stop-1" |
| 10 | IsBeSwapGtcSweepGuard_NullName_ReturnsFalse | YES | YES |

**Notes on tests 6 and 9 — plan vs. spec input divergence (WARN, not FAIL):**

- Test 6: Spec uses `"PTT-BE-Target-1"`; architecture plan uses `"PTT-QX-Stop-1"`.
  Both are valid wrong-name inputs (neither starts with "PTT-BE-Stop"). Ticket follows spec. Functionally equivalent.
- Test 9: Spec uses `"Stop1"`; architecture plan uses `"PTT-QX-Stop-1"`.
  Both are valid non-PTT-BE- inputs. Ticket follows spec. Functionally equivalent.

These are minor input-data divergences between plan and spec. The ticket correctly defers
to the spec (authoritative source). No behavioral difference; both inputs correctly exercise
the name-prefix guard. Flagged as WARN for transparency; not a FAIL.

xUnit only. `[Fact]` only. `using Xunit;` present. No NUnit/MSTest imports. ASCII-only. PASS.

IsBeClearStateTrigger (Ticket-1) and IsBeSwapGtcSweepGuard (Ticket-2) are both covered by tests.
Every internal static seam has corresponding [Fact] tests. PASS.

**Test Coverage**: PASS (with WARN: test input data for items 6 and 9 differ between plan and spec;
ticket follows spec, which is correct)

---

### 8. Scan Checklist Presence (DEFENSE IN DEPTH — NON-NEGOTIABLE)

All 7 scans present in Ticket-2, each with explicit command and pass criterion:

| Scan | Command | Pass Criterion | Present |
|------|---------|----------------|---------|
| SCAN-01 | `grep -rn "lock(" src/PropTraderTools/ --include="*.cs"` | zero matches in added/modified lines | YES |
| SCAN-02 | `grep -rn "async void " src/PropTraderTools/ --include="*.cs"` | zero matches in added/modified lines | YES |
| SCAN-03 | `grep -rn "return null;" src/PropTraderTools/ --include="*.cs"` | zero matches in added/modified lines | YES |
| SCAN-04 | CYC via lizard `--CCN 8` | TryCancelSingleStaleGtcExit <= 8; IsBeSwapGtcSweepGuard <= 8 | YES |
| SCAN-05 | `grep -rn "throw new" src/PropTraderTools/ --include="*.cs"` | zero matches in added/modified lines | YES |
| SCAN-06 | ASCII-only visual inspect of string literals | zero non-ASCII in new/modified lines | YES |
| SCAN-07 | `powershell -File .\scripts\ptt-sync.ps1` | N/0 (zero sync errors) | YES |

**Scan Checklist**: PASS

---

### 9. File Routing

| File | Path in Ticket | Correct Wave Workspace? |
|------|---------------|-------------------------|
| CopyEngine.cs (MODIFY) | `src/PropTraderTools/CopyEngine.cs` | PASS — Wave workspace |
| CopyEngine.cs (ADD seam) | `src/PropTraderTools/CopyEngine.cs` | PASS — Wave workspace |
| B209Tests.cs (CREATE) | `src/PropTraderTools/Tests/B209Tests.cs` | PASS — Wave workspace |

No Director workspace `.cs` paths referenced.

**File Routing**: PASS

---

### 10. Post-Edit Checks Presence

| Check | Present | Command |
|-------|---------|---------|
| Guardian symbol check | YES | `powershell -File .\scripts\verify_critical_symbols.ps1` |
| Ph4b deletion audit | YES | `git diff HEAD~1 HEAD -- src/PropTraderTools/CopyEngine.cs \| Select-String "^-    (private\|internal\|public)"` |
| Single-hunk ban check | YES | `(git show HEAD -- CopyEngine.cs \| Select-String "^@@").Count -eq 1` must be False |
| Build pass requirement | YES | `dotnet build src/PropTraderTools/ --no-incremental` — zero errors, zero new warnings |

**Post-Edit Checks**: PASS

---

### TICKET-2 VERDICT: TICKET_REVIEW_PASS

---
---

## Cross-Ticket Compliance Summary

| Check | Ticket-1 | Ticket-2 |
|-------|----------|----------|
| Spec requirement IDs | PASS | PASS |
| Architecture plan traceability | PASS | PASS |
| Prerequisite gate (T1 before T2) | Stated in EXECUTION ORDER MANDATE | PASS — explicit in Ticket-2 |
| Scope isolation | PASS | PASS |
| FIX-G-1 body matches spec exactly | PASS | N/A |
| FIX-G-2 body matches spec exactly | N/A | PASS |
| IsBeClearStateTrigger signature correct | PASS | N/A |
| IsBeSwapGtcSweepGuard signature correct | N/A | PASS |
| All 10 [Fact] test names match spec | N/A | PASS |
| SCAN-01 through SCAN-07 all present | PASS | PASS |
| NT8 OrderState enum values valid | PASS | PASS |
| apply_diff/search_and_replace for .cs edits | PASS | PASS |
| TryClearBeSwapOnStopWorking CYC target (spec=5, max=8) | PASS | N/A |
| TryCancelSingleStaleGtcExit CYC target (spec=2 delta, max=8) | N/A | PASS |
| IsBeClearStateTrigger CYC ~1 expression-body noted | PASS | N/A |
| IsBeSwapGtcSweepGuard CYC ~1 expression-body noted | N/A | PASS |
| JS-021 no lock | PASS | PASS |
| JS-001 no throw | PASS | PASS |
| JS-002 no null return | PASS | PASS |
| JS-033 no async void | PASS | PASS |
| ASCII-only string literals | PASS | PASS |
| [Fact]-only (no NUnit/MSTest) | N/A | PASS |
| Guardian symbol check present | PASS | PASS |
| Ph4b deletion audit present | PASS | PASS |
| Single-hunk ban check present | PASS | PASS |
| ptt-sync in SCAN-07 | PASS | PASS |
| Build pass requirement stated | PASS | PASS |
| B209Tests.cs location correct (Tests/B209Tests.cs) | N/A (no test file in T1) | PASS — `src/PropTraderTools/Tests/B209Tests.cs` |

---

## Warnings (Non-Blocking)

**WARN-01 — Test input data divergence (tests 6 and 9):**
The architecture plan and the spec specify different input strings for two tests:
- Plan test 6: `"PTT-QX-Stop-1"` vs. Spec/Ticket test 6: `"PTT-BE-Target-1"`
- Plan test 9: `"PTT-QX-Stop-1"` vs. Spec/Ticket test 9: `"Stop1"`

Both pairs exercise the same guard condition (name does not start with the required prefix)
and produce identical results. Ticket correctly defers to spec (the authoritative source).
No behavioral regression. Flagged for architect awareness only.

---

## Overall

All 30 review checklist items: PASS.
Zero TICKET_REVIEW_FAIL conditions found.
Both tickets are complete, correctly scoped, correctly traced to spec and plan,
carry all 7-scan checklists (defense-in-depth contract), use correct file paths,
contain no JS P0 violations, and specify all required post-edit checks.

The engineer may proceed to Ticket-1. Ticket-2 is gated on Ticket-1 VERIFY_PASS.

## Overall: TICKET_REVIEW_PASS
