# Ticket Review -- BWAVE-NEXT LaneBRepair-R2 (Round 2)

**Epic**: BWAVE-NEXT LaneBRepair-R2
**Phase**: 3.5 (Ticket Review)
**Reviewer**: ptt-ticket-reviewer
**Date**: 2026-09-05
**Input tickets**: `docs/brain/BWAVE-NEXT/LaneBRepair-R2/04-tickets.md`
**Input plan**: `docs/brain/BWAVE-NEXT/LaneBRepair-R2/02-architecture-plan.md` (REVIEW_PASS)
**Input plan review**: `docs/brain/BWAVE-NEXT/LaneBRepair-R2/02-plan-review.md` (REVIEW_PASS)
**Rules source**: `docs/standards/jane-street/RULES_CATALOG.md`

---

## T1 -- R2-F1 + R2-F2: AbortDrainOnFill helper + Clone Entry order filter

### 1. Traceability

**Spec requirement IDs cited**: R2-F1, R2-F2 -- both present at ticket header.
**Plan mapping**:
- R2-F1 → plan §2 R2-F1 problem statement + §3 Fix T1 + §4 CYC table + §7 T1 acceptance criteria. MAPPED.
- R2-F2 → plan §2 R2-F2 problem statement + §3 Fix T2 + §4 CYC table + §7 T2 acceptance criteria. MAPPED.

**Reviewer approval line**: `REVIEW_PASS (ptt-plan-reviewer, 2026-09-05)` -- present.

**Phantom work check**: All three changes described in the ticket (`AbortDrainOnFill` helper, `OnOrderUpdate` call-site swap, `DrainThenDispatch` predicate widening) map directly to plan §3 Fix T1 and Fix T2. No items present in ticket that are absent from the plan.

**Missing work check**: Plan §3 specifies both fixes entirely. Both are covered. Nothing in the plan is absent from the ticket.

**NOTE (non-failing)**: Plan §1 describes LANES-APPROVED with "Two tickets: T1 (R2-F1) and T2 (R2-F2)." The architect consolidated both into a single T1 because both fixes target the same source file (`CopyEngine.cs`) and the ticket provides explicit rationale (lines 15-17 of tickets file). The plan itself states the fixes "may be committed together" with no blocking dependency. This consolidation is consistent with plan intent and introduces no violation.

**Traceability: PASS**

---

### 2. Scope Lock Header

Ticket line 23: `> **SCOPE LOCK -- TICKET T1 ONLY. Do NOT implement any other ticket.**`
Header present, correctly formatted, placed immediately after the ticket title.

**Scope Lock: PASS**

---

### 3. Files Table

| File | Ticket Access | Expected | Match? |
|------|--------------|----------|--------|
| `src/PropTraderTools/CopyEngine.cs` | WRITE | WRITE (plan Component List -- only modified file) | YES |
| `src/PropTraderTools/Tests/BwaveNextLaneBTests.cs` | WRITE (append only) | WRITE (new tests required) | YES |
| `docs/brain/BWAVE-NEXT/LaneBRepair-R2/ticket-1-completion.md` | WRITE (create new) | WRITE (completion report) | YES |
| All other files | READ ONLY | READ ONLY | YES |

File routing: all `.cs` paths point to `src/PropTraderTools/` (Wave workspace `c:\WSGTA\universal-or-strategy\src\PropTraderTools\`). No Director workspace paths present.

**Files Table: PASS**
**File Routing: PASS**

---

### 4. Exact Code (Before/After)

**R2-F1 -- AbortDrainOnFill body contract**:
- Body contract block provided with exact code (ticket lines 57-59). PRESENT.

**R2-F1 -- OnOrderUpdate Filled branch call-site**:
- "Current" block (exact code before, lines 67-72) and "Replace with" block (exact code after, lines 76-81) both present. PRESENT.

**R2-F2 -- DrainThenDispatch entryCandidates predicate**:
- "Current last line of predicate" (ticket line 90) and "Replace with" (ticket lines 94-97) with exact code both present.
- Complete updated block post-fix also provided (ticket lines 100-109). PRESENT.

No vague descriptions substituting for exact code found anywhere.

**Exact Code: PASS**

---

### 5. CYC Pre-Check

| Method | Before | After | Budget | Ticket Claim | Plan Claim | Consistent? | Pass? |
|--------|--------|-------|--------|-------------|-----------|-------------|-------|
| `OnOrderUpdate` | 8 | 8 | <=8 | "statement, not a branch -- CYC unchanged at 8" | Plan §4: Before=8, After=8 | YES | YES |
| `AbortDrainOnFill` (new) | n/a | 3 | <=8 | "CYC=3: (1) base, (2) TryRemove guard, (3) foreach" | Plan §4 narrative: "Base=1 + TryRemove if +1 + foreach +1 = 3" | YES | YES |
| `DrainThenDispatch` | 3 | 3 | <=8 | "lambda `||` does not count toward method body -- CYC=3 unchanged" | Plan §4: Before=3, After=3 | YES | YES |

All three methods within CYC <=8 budget. CYC analysis present in both the CYC Contract table and inline method signature comment.

**CYC Pre-Check: PASS**

---

### 6. JS Rule Constraints

Reviewed against `docs/standards/jane-street/RULES_CATALOG.md`:

| Rule ID | Check | Finding in Ticket | Pass? |
|---------|-------|------------------|-------|
| JS-021 (P0) | `lock()` in new/modified code? | Ticket JS table: "No `lock()` -- ConcurrentDictionary.TryRemove is atomic" for all three change sites. No `lock()` in any code block. | PASS |
| JS-023/025 (P0) | Dictionary<K,V> for shared state? | `_pendingDispatchDrains` and `_drainOwnedOrderIds` are `ConcurrentDictionary` (existing fields -- ticket does not change their type). LINQ predicate uses no shared-state dictionary. | PASS |
| JS-033 (P0) | `async void` in new methods? | `AbortDrainOnFill` declared as `private void` (synchronous). Ticket JS table explicitly states "Synchronous `private void`, NOT `async void`". | PASS |
| JS-002 (P0) | `return null` in new/modified methods? | `AbortDrainOnFill` returns `void` (physically impossible to `return null`). `DrainThenDispatch` predicate is a LINQ expression (no return statement). Ticket JS table confirms "Returns void -- no `return null` possible". | PASS |
| JS-001 (P0) | `throw new XxxException` in hot paths? | No exception throwing in any code block provided. | PASS |
| JS-008/009 | Mutable struct fields / SolidColorBrush? | No structs, no WPF brush usage in either fix. | PASS |
| ASCII-only | Non-ASCII in identifiers or string literals? | All code blocks use pure ASCII identifiers and string literals (`"Entry"`, `"PTT-Copy"`, etc.). Explicitly listed in JS table. SCAN-04 enforces at engineer time. | PASS |
| CYC <=8 | All methods within budget? | As verified in §5 above. | PASS |

**JS Pre-Check: PASS**

---

### 7. NT8 API Constraints

| Constraint | Check | Finding | Pass? |
|------------|-------|---------|-------|
| No `Account.Change()` | Present in new code? | Ticket NT8 table: "NOT USED (banned for AddOnBase)". No code block uses it. SCAN-05 enforces. | PASS |
| No `AtmStrategyCreate()` | Present in new code? | Ticket NT8 table: "NOT USED (StrategyBase-only, banned)". No code block uses it. | PASS |
| No `AtmStrategyChangeStopTarget()` | Present in new code? | Ticket NT8 table: "NOT USED (StrategyBase-only, banned)". No code block uses it. | PASS |
| No `DateTime.Now` | Introduced? | Ticket NT8 table: "NOT USED -- existing TickCount pattern unchanged". | PASS |
| `(long)(int)Environment.TickCount` preserved | Baseline Preservation table? | Ticket Baseline Preservation section (~line 6544): "DO NOT change to `TickCount64`". Acceptance criteria checkbox also present. | PASS |
| `.ToList()` preserved | Baseline Preservation table? | Ticket Baseline Preservation section (~line 6535): "DO NOT remove `.ToList()` (DW-NEXT-A-07 thread-safety lock)". Acceptance criteria checkbox also present. | PASS |
| `CreateOrder` name prefix "PTT-" | Any `CreateOrder` in new code? | Neither fix adds a `CreateOrder` call. | PASS |
| No `FontFamily` / hardcoded hex color | Any WPF changes? | No UI changes in either fix. | PASS |
| `TradeCopierWindow` not sealed | Any window class changes? | Neither fix touches any window class. | PASS |

**NT8 Check: PASS**

---

### 8. 7-Scan Checklist Presence

Reviewed ticket §7-Scan Checklist section (ticket lines 198-210):

| Scan ID | Present? | Command | Pass Condition | JS Rule Citation |
|---------|----------|---------|----------------|-----------------|
| SCAN-01 | YES | `grep -n "lock(" src/PropTraderTools/CopyEngine.cs` | "0 actual code statements (comments OK)" | JS-021 |
| SCAN-02 | YES | `grep -n "async void " src/PropTraderTools/CopyEngine.cs` | "0 actual declarations (comments OK)" | JS-033 |
| SCAN-03 | YES | `grep -n "return null;" src/PropTraderTools/CopyEngine.cs` | "0 hits in `AbortDrainOnFill` and `DrainThenDispatch`" | JS-002 |
| SCAN-04 | YES | `Get-Content ... Where-Object {$_ -match '[^\x00-\x7F]'} \| Measure-Object` | `Count=0` | ASCII-only |
| SCAN-05 | YES | `grep -n "Account\.Change\|AtmStrategyCreate\|AtmStrategyChangeStopTarget" ...` | "0 code hits" | NT8 AddOnBase |
| SCAN-06 | YES | `lizard ...` or `python scripts/complexity_audit.py ...` | `OnOrderUpdate`<=8, `AbortDrainOnFill`<=8, `DrainThenDispatch`<=8 | CYC<=8 |
| SCAN-07 | YES | `dotnet build src/PropTraderTools/PropTraderTools.csproj` | "0 errors, 0 relevant warnings" | Build |

All 7 scans present with exact commands and explicit pass conditions. Post-build gates (`ptt-sync-and-verify.ps1` + F5) also present in ticket.

**Scan Checklist: PASS**

---

### 9. Test Coverage

**Framework**: `xUnit ONLY. NEVER NUnit, NEVER MSTest.` -- explicit in ticket §xUnit Tests header.

| Test | Method | [Fact] present? | Asserts what? | Covers which req? |
|------|--------|----------------|---------------|------------------|
| `AbortDrainOnFill_RemovesDrainedOrderIds_FromConcurrentDict` | `AbortDrainOnFill` | YES | DrainedOrderIds removed from `_drainOwnedOrderIds` after call; preferred behavioral / structural fallback described | R2-F1 |
| `DrainThenDispatch_EntryPredicate_IncludesCloneModeEntry` | `DrainThenDispatch` (entryCandidates predicate) | YES | Predicate accepts order named `"Entry"`; preferred behavioral / structural fallback described | R2-F2 |

Both new methods/changes have at least one [Fact] test specified. Both tests include preferred (behavioral) and fallback (structural) paths given NT8 test-seam constraints. Behavioral tests preferred per Jane Street DNA.

`OnOrderUpdate` call-site swap (statement substitution only) is covered implicitly by Test A which validates `AbortDrainOnFill`'s effect.

**Test Coverage: PASS**

---

### 10. Completion Report Template

Ticket §Completion Report Template (lines 270-325) provides:
- Full markdown structure for `ticket-1-completion.md`
- `## Changes Made` section with R2-F1 and R2-F2 sub-sections and line placeholders
- `## 7-Scan Results` table mirroring the 7 scan commands with PASTE OUTPUT slots
- `## Post-Build Results` with sync + F5 attestation slots
- `## Acceptance Criteria` with 18 checkboxes (all acceptance criteria items)

Template complete and directly usable by engineer without interpretation.

**Completion Report Template: PASS**

---

## Overall Summary

| Check | Result |
|-------|--------|
| 1. Traceability (R2-F1, R2-F2 → plan + spec) | PASS |
| 2. Scope Lock header present | PASS |
| 3. Files table (CopyEngine WRITE, completion WRITE, others READ ONLY) | PASS |
| 4. Exact before/after code for all three changes | PASS |
| 5. CYC pre-check (OnOrderUpdate=8, AbortDrainOnFill=3, DrainThenDispatch=3) | PASS |
| 6. JS rule constraints (JS-021, JS-033, JS-002, ASCII-only, CYC<=8) | PASS |
| 7. NT8 API constraints (banned APIs absent, TickCount preserved, .ToList() preserved) | PASS |
| 8. 7-scan checklist SCAN-01 through SCAN-07 all present | PASS |
| 9. Test coverage (2 xUnit [Fact] tests, no NUnit/MSTest) | PASS |
| 10. Completion report template present | PASS |

**No violations found across all 10 checks.**

---

## Overall: TICKET_REVIEW_PASS

The ticket is approved for engineering. Phase 4a (ptt-engineer) is unlocked.

---

*Review written: ptt-ticket-reviewer | BWAVE-NEXT LaneBRepair-R2 Round 2 | Phase 3.5*
