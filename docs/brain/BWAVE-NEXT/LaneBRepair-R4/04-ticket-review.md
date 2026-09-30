# Ticket Review -- BWAVE-NEXT LaneBRepair-R4

**Phase**: 3.5 (Ticket Review)
**Reviewer**: ptt-ticket-reviewer
**Date**: 2026-09-05
**Branch**: bwave-next-lane-b
**Tickets reviewed**: docs/brain/BWAVE-NEXT/LaneBRepair-R4/04-tickets.md
**Plan reviewed**: docs/brain/BWAVE-NEXT/LaneBRepair-R4/02-architecture-plan.md
**Rules catalog**: docs/standards/jane-street/RULES_CATALOG.md

---

## Ticket Review: BWAVE-NEXT LaneBRepair-R4

### T1 -- R4-F1 STALE: Regression Guard Test

---

#### Traceability: PASS

| Item | Mapped To | Status |
|------|-----------|--------|
| R4-F1 STALE investigation | Plan Section 3 + Plan Section 8 | TRACED |
| R4-T1 regression guard test | Plan Section 8 (T1 spec requirement) | TRACED |
| All 11 dismissed findings | Plan Section 5 (exact match) | PRESENT |
| DW-NEXT-B-01 through B-04 | Plan Section 7 (carried forward) | PRESENT |

No phantom work identified. No plan items missing from ticket.
`SubmitDrainedEntry_SourceOrdering_SubmitBeforeCleanup_StaleR4F1` is the exact name
prescribed by Plan Section 8 -- perfect traceability.

---

#### JS Pre-Check: PASS

| Rule ID | Description | Ticket Section | Status |
|---------|-------------|----------------|--------|
| JS-021 (P0) | No `lock()` usage | "JS Rule Constraints" table row 1; SCAN-01 | No lock() in new code. PASS |
| JS-001 (P0) | No `throw new XxxException` in hot paths | "JS Rule Constraints" table row 2 | Test uses Assert.Contains, not manual throw. PASS |
| JS-002 (P0) | No `return null` | "JS Rule Constraints" table row 3 | Test returns void. PASS |
| JS-033 (P0) | No `async void` | "JS Rule Constraints" table row 4 | Test is synchronous void [Fact]. PASS |
| JS-004 (P1) | ASCII-only identifiers and literals | "JS Rule Constraints" table row 7; SCAN-04 | All identifiers and string literals ASCII. PASS |
| JS-051 (Test Framework Mandate) | xUnit [Fact] only | "JS Rule Constraints" table row 5 | [Fact] used; no NUnit/MSTest. PASS |
| JS-066 (CYC <= 8 per method) | Cyclomatic complexity | "JS Rule Constraints" table row 6; SCAN-06 | Test CYC=2, SubmitDrainedEntry CYC=4 (unchanged). PASS |

Note: JS-051 and JS-066 are cited by the ticket as project standards (AGENTS.md Test Framework
Mandate + Jane Street strict standard). These identifiers do not appear in the current
RULES_CATALOG.md (which ends at JS-050). The underlying requirements are valid and correctly
described. No violation.

No concurrency violation described. No type-safety violation described. No immutability violation
described in new code.

---

#### CYC Pre-Check: PASS

| Method | File | Estimated CYC | Within Budget? |
|--------|------|--------------|----------------|
| `SubmitDrainedEntry_SourceOrdering_SubmitBeforeCleanup_StaleR4F1` | CopyEngineTests.cs | 2 (base 1 + Assert branch 1) | YES (<= 8) |
| `SubmitDrainedEntry(string acctKey)` | CopyEngine.cs | 4 (unchanged) | YES (<= 8) |

No method at risk. Zero regressions prescribed.

---

#### NT8 Check: PASS

| Constraint | Status |
|------------|--------|
| No async/await in lifecycle method | Not applicable -- no production code change |
| No `Account.All` outside Loaded handler | Not applicable -- no production code change |
| No `sealed` on TradeCopierWindow | Not applicable |
| No `FontFamily` on WPF element | Not applicable |
| No hardcoded hex color | Not applicable |
| No `CreateOrder` without "PTT-" prefix | Not applicable -- no CreateOrder call |
| No `DateTime.Now` | Explicitly prohibited in test rules (ticket line 97) |
| No `AtmStrategyChangeStopTarget` in AddOnBase | SCAN-05 present with grep command |

NT8 Sync Gate is present: `ptt-sync-and-verify.ps1` command specified.
F5 recompile instruction is present.

---

#### Test Coverage: PASS

Every new method described has a [Fact] test specified:

| Method | [Fact] Test | Present in Ticket? |
|--------|-------------|-------------------|
| `SubmitDrainedEntry_SourceOrdering_SubmitBeforeCleanup_StaleR4F1` | Self (IS the test) | YES |

No new production methods are added. No production methods are modified.
The single deliverable IS the test method. [Fact] attribute is specified.
xUnit `Assert.Contains` with `StringComparison.Ordinal` is specified.
Test is deterministic (file read + substring match, no randomness, no timing).

---

#### Scan Checklist: PASS

All 7 scans are present in the ticket's "7-SCAN CHECKLIST (Engineer Contract)" table
(ticket lines 134-146):

| Scan | Description | grep Command Present | Expected Result Stated |
|------|-------------|---------------------|------------------------|
| SCAN-01 | JS-021 lock() ban | `grep "lock(" src/ --include="*.cs" -r` | YES |
| SCAN-02 | JS-033 async void ban | `grep "async void " src/ --include="*.cs" -r` | YES |
| SCAN-03 | JS-002 return null ban | `grep "return null;" src/ --include="*.cs" -r` | YES |
| SCAN-04 | JS-004 ASCII-only | Manual inspect instruction | YES |
| SCAN-05 | NT8 API AtmStrategyChangeStopTarget | `grep "AtmStrategyChangeStopTarget" src/ --include="*.cs" -r` | YES |
| SCAN-06 | CYC <= 8 | `python scripts/complexity_audit.py` + named values (SubmitDrainedEntry=4, test=2) | YES |
| SCAN-07 | Zero build errors | `dotnet build src/PropTraderTools/ --no-incremental` | YES |

All 7 scans present. Engineer contract is complete. Verifier anchor is established.

---

#### File Routing: PASS

| File | Path | Routing |
|------|------|---------|
| Test file (modify) | `src/PropTraderTools/CopyEngineTests.cs` | Wave workspace -- CORRECT |
| Production file (no change) | `src/PropTraderTools/CopyEngine.cs` | Wave workspace -- CORRECT, NO CHANGE |

No Director workspace (.cs) path found.

---

#### Scope Lock Compliance: PASS

| Lock | Verified in Ticket? |
|------|---------------------|
| No production `.cs` changes | YES -- "NO CHANGE" stated in File Path table and Scope Lock table |
| `(long)(int)Environment.TickCount` preserved | YES -- Scope Lock table |
| `.ToList()` on ActiveOrders preserved | YES -- Scope Lock table |
| try/finally NOT applied | YES -- Scope Lock table: "R4-F1 is STALE -- current ordering is already correct" |
| Watchdog drop-on-timeout locked | YES -- Scope Lock table |

---

#### Stale Finding Documentation: PASS

| Requirement | Present? |
|-------------|----------|
| Explicit STALE declaration with "no production code change" | YES -- ticket header section and Scope Lock table |
| Line-number evidence (submit at 6641, cleanup at 6650) | YES -- "Finding" section cites lines 6641 and 6650-6651 |
| R3-F2 prior fix referenced | YES -- "Finding" section and test body comment |
| R4-F1 STALE explained in test comment | YES -- test body documents rationale |

---

#### Dismissed Findings: PASS

All 11 items from prior rounds are present with DISMISSED status:

| ID | Present in T1? |
|----|---------------|
| CR5-outside-1 | YES |
| CR5-outside-2 | YES |
| CR5-outside-3 | YES |
| CR5-dup-1 | YES |
| CR5-dup-2 | YES |
| CR5-dup-3 | YES |
| CR5-dup-4 | YES |
| CR5-test-1 | YES |
| CR5-test-2 | YES |
| DW-lock-1 | YES |
| DW-net-1 | YES |

Zero dismissed findings re-opened.

---

#### Deferred Items: PASS

DW-NEXT-B-01 through B-04 all present as OPEN (carried forward).
No new DW- items generated (correct for a STALE finding).

---

### VERDICT: TICKET_REVIEW_PASS

---

## Overall: TICKET_REVIEW_PASS

**All checks passed. Zero violations found.**

The single ticket T1 is correctly structured:
- Traceability to spec requirements is complete.
- JS Pre-Check passes on all cited rules (JS-021, JS-001, JS-002, JS-033, JS-004, JS-051, JS-066).
- CYC pre-check passes for all methods (max CYC=4, well within budget of 8).
- NT8 constraints are respected; NT8 Sync Gate with ptt-sync-and-verify.ps1 and F5 instruction present.
- Every new method (the test itself) has a [Fact] specification.
- All 7 scans (SCAN-01 through SCAN-07) are present with grep commands and expected results.
- File routing is correct (Wave workspace src/PropTraderTools/).
- No production code change is prescribed; STALE finding is documented with line-number evidence.
- All 11 dismissed findings carried forward. DW-NEXT-B-01 through B-04 carried forward.
- Scope locks are complete: TickCount, .ToList(), try/finally NOT applied, watchdog drop-on-timeout.

**Engineer is cleared to execute T1.**

---

*Review written: 2026-09-05 | ptt-ticket-reviewer | Phase 3.5 | BWAVE-NEXT LaneBRepair-R4*

---

**TICKET_REVIEW_PASS**
