# B111-T1 Ticket Review

**Block**: B111-T1
**Reviewer**: ptt-ticket-reviewer
**Review Date**: 2026-08-28
**Ticket file**: docs/brain/B111/04-tickets.md
**Plan file**: docs/brain/B111/02-architecture-plan.md
**Plan Review**: docs/brain/B111/02-plan-review.md (REVIEW_PASS)
**Source verified**: src/PropTraderTools/CopyEngine.cs, src/PropTraderTools/Features/PttGlobalQuickExit.cs
**Rules verified**: docs/standards/jane-street/RULES_CATALOG.md

---

## Ticket T1 — B111-T1: Fix DW-B111 (Infinite BE-Retry Loop) + DW-B112 (QX Presence Guard)

---

### Traceability

| Requirement | Present in Ticket? | Citation |
|-------------|-------------------|---------|
| DW-B111 — Remove TryRemove from timer callback (Change A) | PASS | Ticket Section "Change A" |
| DW-B111 — Raise attempt cap 3→5 (Changes B-1/B-2/B-3) | PASS | Ticket Sections "Change B-1", "Change B-2", "Change B-3" |
| DW-B112 — Structural PTT-QX presence check (Change C) | PASS | Ticket Section "Change C" |
| DW-B112 — Update PttGlobalQuickExit.cs comment (Change E) | PASS | Ticket Section "Change E" |
| DW-B112 — Preserve _qxCancelInProgress guard at L2293 | PASS | Change C: "Confirmed preserved at L2293...NOT removed" |
| Method header comment update to CYC=7 (Change D) | PASS | Ticket Section "Change D" |
| PttBreakEvenSwap.cs secondary fix deferred | PASS | CYC Summary Table (OUT OF SCOPE annotation) |
| 4 xUnit tests T_B111_01 through T_B111_04 | PASS | Ticket "Tests" section |
| W1 resolution documented (plan-review conditional) | PASS | Change C: "W1 resolved: .ToList().Any() chosen, reasoning stated"; Acceptance Criterion 15 |

**Verdict**: PASS — all spec requirements in scope appear in the ticket and trace to plan sections.
No phantom work (items in ticket not in plan). No missing work (all plan items covered).

---

### JS Pre-Check (Jane Street Rules — RULES_CATALOG.md)

| Rule | Check | Verdict | Evidence |
|------|-------|---------|---------|
| JS-021 (No lock()) | No lock() in any proposed new code | PASS | Change C guard block uses acc.Orders enumeration (read-only) and ConcurrentDictionary ops. No lock() anywhere. |
| JS-033 (No async void) | No async void in proposed code | PASS | No new async methods. DispatcherTimer.Tick (unchanged) is event handler (exempt). |
| JS-001 (No throw in hot path) | No new exception throws | PASS | TryReplacePttBeBrackets and QueueBeRetryFallback are both void with no throw. No new throw statements. |
| JS-002 (No return null) | No return null in proposed code | PASS | Both methods return void. All new `return` statements are bare `return;`. |
| ASCII-only | All new string literals ASCII-only | PASS | Verified: "PTT-QX-", "[BE-DIAG] TryReplacePttBeBrackets: ", " -- PTT-QX orders Working/Submitted, skipping recovery (DW-B112)", "/5, slot registered, 500ms fallback queued" — all ASCII. |
| JS-036 (No heap alloc in hot path) | .ToList() snapshot acceptable | PASS (ACCEPTABLE) | Ticket correctly documents this at Change C JS Rules table: "acceptable in OnOrderUpdate callback (not a sub-microsecond hot path)". |
| DateTime.Now | Not used | PASS | Not introduced. |
| CreateOrder PTT- prefix | No new CreateOrder calls | PASS (N/A) | No new order submissions in this ticket. |

**Verdict**: PASS — zero JS rule violations in proposed code descriptions.

---

### CYC Pre-Check

| Method | File | Before | After | Delta | <= 8? | Ticket Consistent? |
|--------|------|--------|-------|-------|-------|--------------------|
| TryReplacePttBeBrackets | CopyEngine.cs | 6 | 7 | +1 | YES | PASS — Change C adds exactly one if-branch; plan-reviewer confirmed 6 existing guards before B111. |
| QueueBeRetryFallback (outer) | CopyEngine.cs | 1 | 1 | 0 | YES | PASS — removing a statement from inside an existing branch does not add a branch. |
| QueueBeRetryFallback timer tick lambda | CopyEngine.cs | 2 | 2 | 0 | YES | PASS — CYC delta 0 confirmed. |
| TryFireFollowerBeRetry (unchanged) | CopyEngine.cs | 5 | 5 | 0 | YES | PASS (source confirmed) |
| TryEvictFollowerBeSlot (unchanged) | CopyEngine.cs | 6 | 6 | 0 | YES | PASS (source confirmed) |
| ExecuteOne PttGlobalQuickExit (comment only) | PttGlobalQuickExit.cs | unchanged | unchanged | 0 | YES | PASS |
| Execute PttBreakEvenSwap (OUT OF SCOPE) | PttBreakEvenSwap.cs | 8 | 8 | 0 | YES | PASS |

**Arithmetic for TryReplacePttBeBrackets CYC 6→7**:
Verified from source L2283-2328: 6 existing guards confirmed:
(1) cancelledStop?.Account == null (L2285), (2) !IsFollowerAccount (L2287), (3) IsFlat (L2289),
(3b) _qxCancelInProgress.ContainsKey (L2293), (4) prevAttempts >= 3 (L2299), (5) !TryAdd (L2317).
Change C inserts one additional if-branch (PTT-QX presence check). 6 + 1 = 7. ✓

**Note on CYC=5 stale annotation**: Source header at L2279 reads `CYC=5` — this predates DW-B92.
The plan-reviewer confirmed the true pre-B111 count is 6. Change D updates comment to CYC=7. Correct.

**Verdict**: PASS — no method reaches or exceeds CYC=8 after this ticket.

---

### NT8 Check

| Expression | Verified? | Finding |
|-----------|-----------|---------|
| acc.Orders.ToList() | PASS | Consistent with codebase safety pattern at CopyEngine.cs L2414; documented in Change C W1 resolution. |
| .Any(o => ...) | PASS | LINQ on snapshot — established post-.ToList() pattern (L2417, L2818, L2936, L2967, L3649). |
| o.Name.StartsWith("PTT-QX-", StringComparison.Ordinal) | PASS | CopyEngine.cs L1338-1339 uses sub-variant; broader prefix "PTT-QX-" is correct by design. |
| o.OrderState == OrderState.Working | PASS | NinjaTrader.Cbi.OrderState enum; used at CopyEngine.cs L1348. |
| o.OrderState == OrderState.Submitted | PASS | Same enum; represents cancel-accepted-but-not-confirmed state. Correct. |
| o.Instrument?.FullName == instr.FullName | PASS | Defensive null form; appropriate for new guard context. |
| StringComparison.Ordinal | PASS | Established pattern throughout CopyEngine.cs. |
| No sealed on Window | N/A | No window classes in scope. |
| No FontFamily | N/A | No UI changes. |
| No hardcoded hex color | N/A | No color literals. |
| No async/await in lifecycle | N/A | No lifecycle methods touched. |
| No Account.All outside Loaded | N/A | Not used. |
| No CreateOrder name not starting "PTT-" | N/A | No new CreateOrder calls. |
| No DateTime.Now | PASS | Not introduced. |

**Verdict**: PASS — all NT8 API usages are consistent with existing established patterns.

---

### Test Coverage

| Method Described in Ticket | [Fact] Test Present? | Test Name | Asserts Regression? |
|---------------------------|---------------------|-----------|-------------------|
| TryReplacePttBeBrackets — PTT-QX Working guard | YES | T_B111_01: TryReplacePttBeBrackets_SkipsRecovery_WhenPttQxOrderWorking | YES — Assert.False(_pendingFollowerBeSlots.ContainsKey) + log check |
| TryReplacePttBeBrackets — PTT-QX Submitted guard | YES | T_B111_02: TryReplacePttBeBrackets_SkipsRecovery_WhenPttQxOrderSubmitted | YES — Assert.False + log check; specifically covers Submitted branch |
| QueueBeRetryFallback — counter not reset before MoveStop | YES | T_B111_03: QueueBeRetryFallback_AttemptCounter_NotResetBeforeMoveStop | YES — Assert.Equal(2, capturedCounterAtMoveStop); Assert.Equal(2, counterAfter) |
| TryReplacePttBeBrackets — cap=5 terminates loop | YES | T_B111_04: QueueBeRetryFallback_LoopTerminates_AfterCapAttempts | YES — Part A (4<5 allows), Part B (5>=5 blocks); "attempt 5/5" log |
| Change D (comment update) | N/A | Comment-only change — no [Fact] needed | N/A |
| Change E (comment addition) | N/A | Comment-only change — no [Fact] needed | N/A |

**Test name traceability to spec**:
- T_B111_01 matches plan Section 7 exactly. ✓
- T_B111_02 matches plan Section 7 exactly. ✓
- T_B111_03 matches plan Section 7 exactly. ✓
- T_B111_04: Plan Section 7 Note says name updated from `After3Attempts` → `AfterCapAttempts` because cap was raised. Ticket uses `AfterCapAttempts`. ✓

All 4 tests T_B111_01 through T_B111_04 are present with full Arrange/Act/Assert bodies and regression contracts.
Every new structural method touched by this ticket (TryReplacePttBeBrackets, QueueBeRetryFallback) has [Fact] coverage.

**Verdict**: PASS

---

### Scan Checklist Presence (7-Scan — NON-NEGOTIABLE)

| Scan | Present? | Command Executable? | Files Covered? |
|------|----------|---------------------|---------------|
| SCAN-01: lock() — CopyEngine.cs | YES | `grep -n "lock(" src/PropTraderTools/CopyEngine.cs` — valid grep | CopyEngine.cs ✓ |
| SCAN-02: async void — CopyEngine.cs | YES | `grep -n "async void" src/PropTraderTools/CopyEngine.cs` — valid grep | CopyEngine.cs ✓ |
| SCAN-03: return null — CopyEngine.cs | YES | `grep -n "return null" src/PropTraderTools/CopyEngine.cs` — valid grep | CopyEngine.cs ✓ |
| SCAN-04: lock() — PttGlobalQuickExit.cs | YES | `grep -rn "lock(" src/PropTraderTools/Features/PttGlobalQuickExit.cs` — valid grep | PttGlobalQuickExit.cs ✓ |
| SCAN-05: async void — PttGlobalQuickExit.cs | YES | `grep -rn "async void" src/PropTraderTools/Features/PttGlobalQuickExit.cs` — valid grep | PttGlobalQuickExit.cs ✓ |
| SCAN-06: complexity_audit.py | YES | `python scripts/complexity_audit.py` — valid script path; targets TryReplacePttBeBrackets (CYC<=8, expected 7) and QueueBeRetryFallback (CYC<=8, expected 1) | Both methods ✓ |
| SCAN-07: ASCII-only | YES | `grep -Pn "[^\x00-\x7F]" src/PropTraderTools/CopyEngine.cs src/PropTraderTools/Features/PttGlobalQuickExit.cs src/PropTraderTools/Tests/B111Tests.cs` — valid grep; covers all 3 files including test file | All 3 files ✓ |

All 7 scans present. Commands are executable (correct grep flags, correct script path). Both primary source files covered by lock/async void scans. Complexity scan targets both touched methods. ASCII scan covers all 3 files modified by this ticket.

**Verdict**: PASS

---

### Code Correctness Checks

| Check | Verdict | Finding |
|-------|---------|---------|
| Change A: TryRemove line identified correctly | PASS | Source L1465 confirmed: `_beReplaceAttempts.TryRemove(capturedAcc.Name, out _); // DW-B82-01: reset on slot consumption` — exact match to ticket OLD CODE. Deletion leaves no dangling syntax (it is a standalone statement inside an if-success-arm that still has the MoveStopToBreakEven call). |
| Change A: No dangling syntax after deletion | PASS | After L1465 is removed, L1466 (`bool flat = IsFlat(...)`) becomes the first statement in the if-success arm. No syntax dependency on L1465. |
| Change B: Consistent "3"→"5" across all 3 log strings | PASS | B-1 updates guard constant; B-2 updates max-attempts log string; B-3 updates slot-registered log string. All references to "3" in the guarded path updated. |
| Change C: Guard code is complete compilable C# (not pseudocode) | PASS | Full guard block provided with all brackets, parentheses, dot-notation, method calls, StringComparison.Ordinal, NinjaTrader.Code.Output.Process call, and bare `return;`. No placeholder text or ellipsis in structural code. |
| Change C: Uses .ToList().Any() for W1 safety | PASS | Ticket Change C explicitly uses `acc.Orders.ToList().Any(...)`. W1 resolved in favour of option (b). Comment confirms: "W1 resolved: .ToList() snapshot used for consistency with L2414 safety pattern." |
| Change C: _qxCancelInProgress guard at L2293 preserved | PASS | Ticket explicitly states: "The line...at L2293 (`if (_qxCancelInProgress.ContainsKey(cancelledStop.Account.Name)) return;`) is preserved... NOT removed." Insertion is after L2296, not touching L2293. |
| Change C: Context lines not re-added | PASS | Ticket Note: "The line `var acc = cancelledStop.Account;` and `var instr = cancelledStop.Instrument;` shown above are the existing L2295–L2296 lines for context — they are NOT re-added." Explicit and unambiguous. |
| Change D: Old comment text matches source L2279 | PASS | Source L2279 confirmed: `// CYC=5: (1) null guard, (2) follower guard, (3) flat guard, (4) attempt guard, (5) slot+fallback.` — exact match. |
| Change E: Old finally block matches source L159-162 | PASS | Source L159-162 confirmed: `finally { CopyEngine.Instance?._qxCancelInProgress.TryRemove(acc.Name, out _); }` — matches ticket OLD CODE. |
| Acceptance criteria verifier-checkable | PASS | All 15 criteria are numbered and reference specific file + line content or command output observable from source inspection. |

**Verdict**: PASS

---

### File Routing

| File | Path in Ticket | Workspace | Verdict |
|------|---------------|-----------|---------|
| CopyEngine.cs | src/PropTraderTools/CopyEngine.cs | Wave workspace (c:\WSGTA\universal-or-strategy) | PASS |
| PttGlobalQuickExit.cs | src/PropTraderTools/Features/PttGlobalQuickExit.cs | Wave workspace | PASS |
| B111Tests.cs (new) | src/PropTraderTools/Tests/B111Tests.cs | Wave workspace | PASS |

**Verdict**: PASS — all .cs paths route to Wave workspace. No Director workspace paths for .cs files.

---

### Scope Check

| Check | Verdict | Evidence |
|-------|---------|---------|
| Covers only DW-B111 and DW-B112 | PASS | Spec Requirements table lists exactly these two defects |
| PttBreakEvenSwap.cs secondary fix properly deferred | PASS | CYC Summary Table marks it OUT OF SCOPE; plan Section 5 deferred as B111-DEFER-01 |
| No scope creep (no new features, no unrequested changes) | PASS | All 7 changes (A, B-1, B-2, B-3, C, D, E) trace directly to DW-B111 or DW-B112 |
| DW-B107 explicitly kept out of scope | PASS | Plan Section 10 Out-of-Scope table lists DW-B107 |

**Verdict**: PASS

---

### Completeness Checks

| Check | Verdict | Evidence |
|-------|---------|---------|
| Acceptance criteria present and numbered | PASS | 15 numbered criteria, all verifier-checkable |
| Old code and new code provided for every change | PASS | All changes A, B-1, B-2, B-3, C, D, E have explicit OLD CODE / NEW CODE blocks |
| CYC summary table present | PASS | "CYC Summary Table" section with 7 rows |
| W1 resolution documented | PASS | Change C section and Acceptance Criterion 15 both document the resolution |
| Build gate included in acceptance criteria | PASS | Criterion 14: dotnet build exit zero |
| Test file path specified | PASS | "src/PropTraderTools/Tests/B111Tests.cs" — new file |

**Verdict**: PASS

---

## Overall: TICKET_REVIEW_PASS

**Zero violations found.** All checks passed:

| Check Category | Result |
|---------------|--------|
| Traceability (spec ↔ ticket) | PASS |
| JS Pre-Check (JS-021, JS-033, JS-001, JS-002, ASCII) | PASS |
| CYC Pre-Check (TryReplacePttBeBrackets=7, all others ≤8) | PASS |
| NT8 Check (API usage verified against existing patterns) | PASS |
| Test Coverage (T_B111_01 through T_B111_04 present with Arrange/Act/Assert) | PASS |
| Scan Checklist (all 7 scans present, executable, both files + test file covered) | PASS |
| Code Correctness (old code matches source, new code is complete compilable C#) | PASS |
| File Routing (all .cs paths → Wave workspace) | PASS |
| Scope (DW-B111 + DW-B112 only, PttBreakEvenSwap.cs correctly deferred) | PASS |
| Completeness (15 numbered acceptance criteria, CYC table, W1 resolved) | PASS |

**W1 Resolution Confirmed**: Plan-review WARNING W1 (`acc.Orders.Any()` without `.ToList()`) was resolved in the ticket by adopting option (b) `.ToList().Any(...)`. This is the correct resolution and it is documented in both Change C and Acceptance Criterion 15. The engineer is cleared to implement.

**Ticket approved. Engineer may proceed.**

---

*Reviewer: ptt-ticket-reviewer | Block B111-T1 | Phase 3.5 | 2026-08-28*
*Plan source: docs/brain/B111/02-architecture-plan.md (REVIEW_PASS)*
*Review source: docs/brain/B111/02-plan-review.md*
