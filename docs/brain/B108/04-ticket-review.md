# B108 Ticket Review — ptt-ticket-reviewer
**Phase**: 3.5 (Ticket Review)
**Epic**: B108-T1 (DW-B107 fix)
**Ticket under review**: `docs/brain/B108/04-tickets.md`
**Source plan**: `docs/brain/B108/02-architecture-plan.md` (REVIEW_PASS confirmed)
**Plan review**: `docs/brain/B108/02-plan-review.md` (RC-01..12 all PASS)
**Reviewer**: ptt-ticket-reviewer
**Date**: 2026-08-11

---

## Ticket Review: B108

### T1 — SnapshotBeTargets Extraction + Cap-at-3 (B108-T1)

---

#### TC-01: Single File Gate — PASS

- Ticket Section 2 declares: `"EXACTLY ONE FILE: src/PropTraderTools/CopyEngine.cs"`.
- Explicit DO NOT TOUCH table names all three required out-of-scope files:
  - `src/PropTraderTools/Features/PttGlobalQuickExit.cs` — "Fixed in B107 (DW-B106). DO NOT TOUCH."
  - `src/PropTraderTools/Features/PttQuickExit.cs` — "Fixed in B107 (DW-B106). DO NOT TOUCH."
  - `src/PropTraderTools/Features/PttBreakEvenSwap.cs` — "Cap applied upstream (before `Execute`). DO NOT TOUCH."
- No additional files listed as in-scope.

---

#### TC-02: Traceability — PASS

- Ticket header: `"Defect closed: DW-B107"`.
- Section 1 traceability table: all three rows (CHANGE A, CHANGE B, CHANGE C) cite `DW-B107` in the "Closes" column.
- No changes reference any other defect ID (DW-B79, DW-B106, etc. are cited as *preserved* prior fixes — not as new changes introduced by this ticket).
- Zero phantom work items: all changes map directly to the DW-B107 plan (confirmed against `02-architecture-plan.md` Section 2 and Section 6).

---

#### TC-03: CHANGE A Verbatim Completeness — PASS

All sub-criteria confirmed present in ticket Section 4, CHANGE A (lines 74–127):

| Sub-criterion | Evidence |
|---------------|---------|
| Full method body present (not just described) | Complete verbatim C# code block at ticket lines 80–127 |
| Return type `List<(double Price, int Qty, OrderAction Action)>` | Ticket line 86: `private List<(double Price, int Qty, OrderAction Action)> SnapshotBeTargets(` and Section 3 return type declaration |
| Parameters `(Account acc, Instrument instrument)` | Ticket lines 86–87 |
| CYC=7 annotation present | Ticket line 81: `// CYC=7: null guard(1) + foreach(2) + o==null continue(3) + stateOk(4) + instrOk+type(5)` |
| stateOk has all 7 states | Ticket lines 98–104: `Working`, `Accepted`, `Submitted`, `Initialized`, `TriggerPending`, `ChangeSubmitted`, `CancelSubmitted` |
| isNative includes `&& o.Name[6] != '0'` | Ticket line 114: `&& o.Name[6] != '0';` |
| isPtt covers `PTT-QX-T*` AND `PTT-BE-Target-*` | Ticket lines 115–119: both OR branches present |
| Return: `nativeTargets.Count > 0 ? nativeTargets : pttTargets` | Ticket line 125 |
| Null guard returns `nativeTargets` (empty list), not null | Ticket line 92: `return nativeTargets; // (1) JS-002: empty list, never null` |

---

#### TC-04: CHANGE B Verbatim Completeness — PASS

All sub-criteria confirmed present in ticket Section 4, CHANGE B (lines 131–164):

| Sub-criterion | Evidence |
|---------------|---------|
| Step A replacement code: `var targets = SnapshotBeTargets(acc, instrument); // (3)` | Ticket line 160 (exact, including `// (3)` suffix) |
| DW-B107 comment block above the call | Ticket lines 156–160: multi-line comment block with DW-B107 extraction rationale |
| CYC annotation update CYC=8 → CYC=7 specified | Ticket lines 137–148: B.1 sub-change replaces old CYC=8 annotation with new CYC=7 annotation |

---

#### TC-05: CHANGE C Verbatim Completeness — PASS

All sub-criteria confirmed present in ticket Section 4, CHANGE C (lines 168–180):

| Sub-criterion | Evidence |
|---------------|---------|
| `while (targets.Count > 3) targets.RemoveAt(targets.Count - 1);` | Ticket lines 178–179 |
| DW-B107 cap comment present | Ticket line 175: `// DW-B107: hard cap -- BE/QX contract is always exactly 3 targets max.` |
| No LINQ alternative mentioned as acceptable | Ticket line 177: `// No LINQ -- while-loop trim per JS zero-alloc mandate.` — LINQ alternatives explicitly prohibited, not offered as acceptable |

---

#### TC-06: JS Pre-Check (P0 Rules) — PASS

Ticket Section 5 (JS Rule Constraints table) explicitly confirms all P0 constraints for each change:

| P0 Rule | Evidence in ticket |
|---------|-------------------|
| NO `lock()` (JS-021) | Section 5: "JS-021 (no `lock()`, local list operations only)" for CHANGE A; "JS-021 (no `lock()`)" for CHANGE C |
| NO `return null` (JS-002) | Section 5: "JS-002 (returns empty list, not null — never `return null`)" for CHANGE A; SCAN-03 explicitly requires zero `return null` in new code |
| NO `async void` (JS-033) | Section 5: "JS-033 (synchronous — no `async` keyword)" for CHANGE A |
| ASCII-only | Section 5: "ASCII-only identifiers and string literals" for all three changes |
| No LINQ | Section 5: "no LINQ" for CHANGE A; "no LINQ (`while + RemoveAt` only — not `.Take()`, `.GetRange()`, `.Where()`, `.Select()`)" for CHANGE C |

The verbatim method body in CHANGE A was cross-checked: no `lock()`, no `return null`, no `async`, no non-ASCII characters, no LINQ calls present.

---

#### TC-07: CYC Pre-Check — PASS

Ticket Section 8 (CYC Before/After Table, lines 383–388) provides the required table:

| Method | CYC Before | CYC After | Delta | Limit | Status |
|--------|-----------|-----------|-------|-------|--------|
| `MoveStopToBreakEven` | 8 | 7 | -1 | 8 | PASS |
| `SnapshotBeTargets` | n/a (new) | 7 | n/a | 8 | PASS |

Both methods are ≤ 8. Closing statement at ticket line 388: "No existing method exceeds CYC=8 after B108. No other methods are touched."

Cross-check against plan Section 3 CYC analysis: consistent. The -1 delta for `MoveStopToBreakEven` is derived from old branches 3/4/5 collapsing into the extracted method call (0 CYC contribution at call site) with +1 for the new `while` cap, and the pre-existing `partial-retry branch` correctly counted at position 7.

---

#### TC-08: 7-Scan Checklist MANDATORY Presence — PASS

Ticket Section 6 contains all 7 scans. Per-scan verification:

| Scan | Present | Command/Method |
|------|---------|---------------|
| SCAN-01: `lock()` check (JS-021 P0 BLOCKER) | YES | `Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "lock\("` |
| SCAN-02: `async void` check (JS-033 P0 BLOCKER) | YES | `Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "async void "` |
| SCAN-03: `return null` check (JS-002 P0 BLOCKER) | YES | `Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "return null;"` |
| SCAN-04: Non-ASCII check | YES | `Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "[^\x00-\x7F]"` |
| SCAN-05: CYC check | YES | `python scripts/complexity_audit.py` or manual count; table with expected CYC=7 for both methods |
| SCAN-06: LINQ check | YES | `Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "\.Take\(|\.GetRange\(|\.Where\(|\.Select\("` |
| SCAN-07: `stateOk` 7-state completeness check (domain correctness gate) | YES | Manual inspection with table of all 7 required states; FAIL condition: any state missing |

All 7 scans are present, each with explicit command or method, expected outcome, and FAIL condition. Defense-in-depth contract is intact.

---

#### TC-09: T1-T15 Acceptance Criteria Present — PASS

Ticket Section 7 (lines 296–376) contains all 15 criteria. Key domain-specific criteria verified:

| Criterion | Present | Domain Gate |
|-----------|---------|-------------|
| T1 — `SnapshotBeTargets` method exists with correct sig | YES | Signature + location |
| T2 — null guard returns empty list (JS-002) | YES | JS-002 correctness |
| T3 — two-pass structure present | YES | DW-B107 architecture |
| T4 — `stateOk` 7-state (regression guard DW-B79) | YES | "stateOk includes all of: Working, Accepted, Submitted, Initialized, TriggerPending, ChangeSubmitted, CancelSubmitted" |
| T5 — `isNative` `[6] != '0'` guard | YES | "All four sub-conditions present" including `o.Name[6] != '0'` |
| T6 — `isPtt` covers both `PTT-QX-T*` and `PTT-BE-Target-*` | YES | "Both branches of the OR present" |
| T7 — `SnapshotBeTargets` CYC annotation CYC=7 | YES | Annotation content specified |
| T8 — Step A loop replaced | YES | Old lines L3373-3422 replaced by single call |
| T9 — Step A comment updated | YES | DW-B107 extraction comment replaces old block |
| T10 — `while` cap present with correct position | YES | After `SnapshotBeTargets(...)`, before `PttBreakEvenSwap.Execute(...)` |
| T11 — No LINQ at cap site | YES | Specific banned patterns listed |
| T12 — `MoveStopToBreakEven` CYC annotation updated | YES | Old CYC=8 annotation removed, new CYC=7 annotation specified |
| T13 — No `lock()` in new code | YES | grep command specified |
| T14 — No `return null` in new code | YES | Both method and cap block explicitly checked |
| T15 — 3 out-of-scope files unchanged | YES | File timestamps and content identical to pre-B108 |

---

#### TC-10: Build Verification Steps — PASS

Ticket Section 9 (Build Verification Steps, lines 392–411) specifies all 5 required steps in order:

| Step | Required | Present |
|------|---------|---------|
| Step 1: All 7 scans to zero | YES | "Run all 7 scans (Section 6 above) — every scan must return zero new findings." |
| Step 2: `ptt-sync-and-verify.ps1` (0 MISMATCH) | YES | Exact PowerShell command with `0 MISMATCH` requirement stated |
| Step 3: F5 in NinjaTrader 8 (compile green) | YES | "must compile green (zero errors). Do NOT merge without a green F5." |
| Step 4: T1-T15 code inspection | YES | "Confirm T1-T15 by code inspection of `src/PropTraderTools/CopyEngine.cs`. All 15 criteria must be PASS." |
| Step 5: git commit with correct message | YES | Exact commit message: `"feat(ptt): B108 DW-B107 SnapshotBeTargets extraction + cap-at-3"` with staged files specified |

---

#### TC-11: NT8 Constraints — PASS

| Constraint | Evidence |
|-----------|---------|
| No LINQ (NT8-006): stated and scanned | Section 5 CHANGE A: "no LINQ"; Section 5 CHANGE C: "no LINQ (`while + RemoveAt` only...)"; SCAN-06 verifies with PowerShell command |
| No `lock()` (JS-021): stated and scanned | Section 5 all changes: JS-021 cited; SCAN-01 verifies |
| Method `SnapshotBeTargets` is private | Ticket line 86 + Section 3: `private List<...> SnapshotBeTargets(...)` — no public API surface change |

---

#### TC-12: No Scope Creep — PASS

| Scope Check | Evidence |
|-------------|---------|
| No test file changes | Section 2 specifies one file only: `CopyEngine.cs`; Section 9 Step 5 stages `src/PropTraderTools/CopyEngine.cs` and `docs/brain/B108/` only |
| No interface file changes | Consistent with plan Section 5: "Interface files changed: 0" |
| `PttGlobalQuickExit.cs` not touched | Explicit DO NOT TOUCH in Section 2; T15 enforces this |
| `PttQuickExit.cs` not touched | Explicit DO NOT TOUCH in Section 2; T15 enforces this |
| `PttBreakEvenSwap.cs` not touched | Explicit DO NOT TOUCH in Section 2; T15 enforces this |
| No new features beyond 3 changes | Changes A/B/C cover extraction, replacement, and cap only — no additional behaviour introduced |

---

### Line Reference Verification (against `src/PropTraderTools/CopyEngine.cs` L3271-3430)

The following ticket line claims were verified against the actual source:

| Ticket Claim | Source Verification | Status |
|-------------|-------------------|--------|
| CYC annotation at L3271-3272 | L3271: `// CYC=8: IsFlat(1) + tickSize/pos guard(2) + snapshot-foreach(3)...` L3272: `//        + cancel-try(6) + 0-targets branch(7) + targets-for-loop(8).` | VERIFIED ✓ |
| `MoveStopToBreakEven` at ~L3335 | L3335: `private void MoveStopToBreakEven(` | VERIFIED ✓ |
| Step A loop at L3373-3422 | L3373: `// -- Step A: snapshot ATM target orders...` L3379: `var targets = new List<...>()` L3380: `foreach (Order o in acc.Orders)` L3422: closing brace of foreach | VERIFIED ✓ |
| `PttBreakEvenSwap.Execute(...)` at L3427 | L3427: `PttBreakEvenSwap.Execute(acc, instrument, newStop, targets);` | VERIFIED ✓ |

All line references are accurate. The CHANGE B replacement and CHANGE C insertion points are precisely described and match the actual source layout.

---

## Overall: TICKET_REVIEW_PASS

| Criterion | Verdict |
|-----------|---------|
| TC-01: Single File Gate | **PASS** |
| TC-02: Traceability | **PASS** |
| TC-03: CHANGE A Verbatim Completeness | **PASS** |
| TC-04: CHANGE B Verbatim Completeness | **PASS** |
| TC-05: CHANGE C Verbatim Completeness | **PASS** |
| TC-06: JS Pre-Check (P0 Rules) | **PASS** |
| TC-07: CYC Pre-Check | **PASS** |
| TC-08: 7-Scan Checklist MANDATORY Presence | **PASS** |
| TC-09: T1-T15 Acceptance Criteria Present | **PASS** |
| TC-10: Build Verification Steps | **PASS** |
| TC-11: NT8 Constraints | **PASS** |
| TC-12: No Scope Creep | **PASS** |

**Violations found**: None.

**Engineer clearance**: B108-T1 is cleared for Phase 4a (ptt-engineer). The engineer reads this
file first, then `docs/brain/B108/04-tickets.md`. The 7-scan checklist in Section 6 of the ticket
is the engineer's implementation contract and the verifier's cross-check anchor.
