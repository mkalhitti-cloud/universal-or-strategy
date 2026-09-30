# DW-B160 Ticket Review
<!-- block: DW-B160 -->
<!-- wave: B160 -->
<!-- phase: 3.5 -->
<!-- date: 2026-09-15 -->
<!-- produced-by: ptt-ticket-reviewer (Phase 3.5) -->

---

## Ticket Review: DW-B160

### T1 — FM-A Ghost Re-entry Guard Race Repair (B160-T1)

---

#### Traceability: PASS

All 10 spec requirements (REQ-1 through REQ-10) are explicitly listed in ticket Section 2
with descriptions. Every ticket item maps to a plan section:

| Ticket Item | Plan Section | Result |
|-------------|--------------|--------|
| IsPttBeStopActiveState (new) | Plan §4, §5.0.1 | PASS |
| IsPttBeTargetSettlingState (new) | Plan §4, §5.0.2 | PASS |
| HasActivePttBeExits (new) | Plan §4, §5.1 | PASS |
| HasSettlingBeTargets (new) | Plan §4, §5.2 | PASS |
| IsFollowerPositionBlocked (modified) | Plan §4, §5.3 | PASS |
| ShouldSkipFollower log removal | Plan §4, §5.4 | PASS |
| [PTT-COPY-GUARD] reason-tagged logs | Plan §5.3 | PASS |
| Stale comment DW-B159-K5 removal | Plan §5.3 note, §11 | PASS |
| Deferred items K1/K5 close criteria | Plan §11 | PASS |
| 6 xUnit [Fact] test stubs | Plan §10 | PASS |

No phantom work (items in ticket not in plan). No missing plan work (all plan components
appear in ticket). Scope lock statement present in ticket Section 3:
> "Implement changes in `CopyEngine.cs` ONLY. Do NOT modify any other file."

---

#### JS Pre-Check: PASS

| Rule | Description | Ticket Section | Result |
|------|-------------|----------------|--------|
| JS-001 | No throw in execution paths | §6.8, §6.5 — all null inputs return false | PASS |
| JS-013 | CYC <= 8 all methods | §5 CYC table — all within limit | PASS |
| JS-021 | No lock() | §6.1 "no lock() anywhere", §6.8 "NO lock()" | PASS |
| JS-023 | No off-thread WPF update | Pure predicates, no WPF elements | PASS |
| JS-042 / ASCII-001 | ASCII-only string literals | §6.5 "ASCII-only log strings. All characters 0x20-0x7E. No Unicode. No curly quotes." §6.8 "NO Unicode in string literals" | PASS |
| JS-PUSH-01 | No new instance fields | §6.8 "NO new instance fields, ConcurrentDictionary, or cached state" | PASS |
| NT8-001 | No async void | §6.8 "NO async void methods" | PASS |
| NT8-006 | No DateTime.Now | §6.8 "NO DateTime.Now — not used; no DateTime in any new/modified path" | PASS |

No violations found in ticket descriptions.

---

#### CYC Pre-Check: PASS

Ticket Section 5 records authoritative CYC values consistent with the plan reviewer's
corrections (02-plan-review.md §2.3, §2.4). The ticket explicitly corrects the plan's
erroneous CYC=7 claim for `HasActivePttBeExits` and `HasSettlingBeTargets`, recording
CYC=8 per the plan reviewer's strict McCabe recount:

| Method | Ticket CYC | Hard Limit | Status |
|--------|-----------|------------|--------|
| `IsPttBeStopActiveState` | 3 | 8 | PASS |
| `IsPttBeTargetSettlingState` | 2 | 8 | PASS |
| `HasActivePttBeExits` | **8** | 8 | PASS — at limit, not over; comment must record 8 not 7 per plan review |
| `HasSettlingBeTargets` | **8** | 8 | PASS — at limit, not over; comment must record 8 not 7 per plan review |
| `IsFollowerPositionBlocked` | 6 | 8 | PASS |
| `ShouldSkipFollower` | 4 | 8 | PASS — log removal is not a branch |

No method exceeds CYC=8.

---

#### NT8 Check: PASS

| Constraint | Ticket Evidence | Result |
|------------|----------------|--------|
| `acc.Orders.ToList()` snapshot pattern | §6.1 explicitly requires `foreach (var o in acc.Orders.ToList())` with "snapshot live collection, no lock" note | PASS |
| OrderState enum values (Working, Accepted, Submitted, CancelPending, CancelSubmitted) | §6.2 name prefix table; §5 SCAN-03 lists all 5 states by name; §9 acceptance criteria reference CancelPending/CancelSubmitted | PASS |
| Instrument comparison via `FullName` | §6.3 `if (o.Instrument?.FullName != instr.FullName) continue;` — confirmed against L7021 | PASS |
| No async/await in lifecycle methods | Not used — all new/modified methods are synchronous | PASS |
| No Account.All outside Loaded handler | Not used | PASS |
| No sealed on TradeCopierWindow | Not applicable to this ticket | PASS |
| No FontFamily on WPF element | No WPF elements | PASS |
| No hardcoded hex color | No WPF elements | PASS |
| CreateOrder names start with "PTT-" | No CreateOrder calls — pure predicates | PASS |

---

#### Test Coverage: PASS

All 6 required [Fact] method names present in ticket Section 7 with explicit assertion criteria:

| # | [Fact] Name | Asserts Column Present | Result |
|---|------------|----------------------|--------|
| T1 | `HasActivePttBeExits_ReturnsTrue_WhenBeStopWorking` | Returns true when order "PTT-BE-Stop-1" with OrderState.Working on matching instrument | PASS |
| T2 | `HasActivePttBeExits_ReturnsFalse_WhenBeStopFilled` | Returns false when "PTT-BE-Stop-1" has OrderState.Filled (terminal) | PASS |
| T3 | `HasSettlingBeTargets_ReturnsTrue_WhenTargetCancelPending` | Returns true when order "PTT-BE-Target-1" with OrderState.CancelPending on matching instrument | PASS |
| T4 | `HasSettlingBeTargets_ReturnsFalse_WhenTargetCancelled` | Returns false when "PTT-BE-Target-1" has OrderState.Cancelled (terminal) | PASS |
| T5 | `IsFollowerPositionBlocked_ReturnsTrue_WhenSettlingTargetsPresent` | Returns true when HasOpenPosition=false, HasActivePttBeExits=false, HasSettlingBeTargets=true (the confirmed B160 race scenario) | PASS |
| T6 | `IsFollowerPositionBlocked_ReturnsFalse_WhenAllTerminal` | Returns false when all three signals return false | PASS |

All new `internal` methods accessible via InternalsVisibleTo (established in B159) — confirmed in ticket Section 7 note.

---

#### Scan Checklist: PASS

Ticket Section 8 "Seven-Scan Checklist (Engineer Contract)" contains all 7 scans with grep-based pass criteria:

| Scan | Description | Pass Criteria Present | Result |
|------|-------------|----------------------|--------|
| SCAN-01 | No new fields or ConcurrentDictionary | grep command targeting private fields and properties | PASS |
| SCAN-02 | No lock() usage | `grep -c "lock("` against CopyEngine.cs | PASS |
| SCAN-03 | All new/modified methods CYC <= 8 | Explicit per-method CYC values with hard limit comparison | PASS |
| SCAN-04 | No DateTime.Now in executable paths | `grep -n "DateTime\.Now"` against CopyEngine.cs | PASS |
| SCAN-05 | ASCII-only string literals | Explicit inspection list of all new log strings | PASS |
| SCAN-06 | No async void methods | `grep -n "async void"` against CopyEngine.cs | PASS |
| SCAN-07 | All 6 [Fact] test methods present and passing | All 6 method names listed with passing requirement | PASS |

All 7 scans present with actionable pass criteria. Defense-in-depth contract intact.

---

#### File Routing: PASS

Ticket Section 10 File Location Summary:

| File | Action | Path |
|------|--------|------|
| `src/PropTraderTools/CopyEngine.cs` | MODIFY — all changes in this file only | Wave workspace ✅ |

No Director workspace paths for .cs files. Single file. Correct.

---

#### Acceptance Criteria Check: PASS

| Criterion | Location | Result |
|-----------|----------|--------|
| Ghost re-entry block scenario described | §9 P0 item 1 — "When PTT-BE-Stop-1/2/3 fill and acc.Positions.Quantity drains to 0 while PTT-BE-Target-1/2/3 are in CancelPending/CancelSubmitted..." | PASS |
| [PTT-COPY-GUARD] reason-tagged log output specified | §9 P0 item 2 — "Exactly one of (has-position), (active-be-exits), or (settling-be-targets) must appear in the log for each blocked copy dispatch" | PASS |
| SIM verification path present | §9 SIM Verification Criteria table — 3 scenarios with expected Output Tab 1 strings | PASS |

---

### VERDICT: TICKET_REVIEW_PASS

All checks passed. Zero violations found.

---

## Overall: TICKET_REVIEW_PASS

| Check | Result |
|-------|--------|
| Traceability | PASS |
| Spec Coverage (REQ-1 through REQ-10) | PASS |
| Scope Lock Statement | PASS |
| Method Signatures (all 6) | PASS |
| Access Modifiers | PASS |
| CYC Pre-Check (all methods <= 8) | PASS |
| Jane Street Rules (JS-001, JS-013, JS-021, JS-023, JS-042, JS-PUSH-01) | PASS |
| NT8 API Constraints | PASS |
| 7-Scan Checklist (SCAN-01 through SCAN-07) | PASS |
| Test Coverage (all 6 [Fact] names with assertions) | PASS |
| Acceptance Criteria | PASS |
| File Routing (Wave workspace only) | PASS |

**No violations found. TICKET_REVIEW_PASS. Safe to spawn Phase 4 engineer.**

---

*Reviewed by: ptt-ticket-reviewer (Phase 3.5). Wave B160. Date: 2026-09-15.*
*Source ticket: docs/brain/B160/04-tickets.md*
*Source plan: docs/brain/B160/02-architecture-plan.md (REVIEW_PASS)*
*Source plan review: docs/brain/B160/02-plan-review.md (REVIEW_PASS)*
