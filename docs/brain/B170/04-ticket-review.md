# B170 Ticket Review — DW-B170-01 FOLLOWER-STOP-LOST-ON-TGT-DRAG

Reviewer: ptt-ticket-reviewer
Tickets under review: docs/brain/B170/04-tickets.md
Plan under review: docs/brain/B170/02-architecture-plan.md (REVIEW_PASS)
Source spot-check: src/PropTraderTools/CopyEngine.cs lines 4407–4428

---

## TICKET B170-1 — Extend OrderState predicate in FindLeaderStopPrice

### 1. Line number sourced from actual source (not assumed)

**PASS**

Source read confirms:
- Line 4410: `// CYC=5: (1) null account; (2) zero index; (3) foreach; (4) name match; (5) state==Working.` — matches BEFORE block exactly.
- Lines 4421–4424:
  ```csharp
          if (
              order.Name == targetName // (4)
              && order.OrderState == OrderState.Working
          ) // (5)
  ```
  — matches BEFORE block exactly. Line numbers are sourced from actual source, not assumed.

### 2. apply_diff is surgical (ticket must NOT instruct write_file on CopyEngine.cs)

**PASS**

Ticket B170-1 describes exactly two targeted line modifications (line 4423 and line 4410) with explicit BEFORE/AFTER blocks and states net line-count delta = 0. It does not instruct `write_file`. The change description is consistent with `apply_diff` surgical editing. No full-file rewrite is prescribed.

### 3. Both OrderState.Working AND OrderState.Accepted appear in the AFTER predicate

**PASS**

AFTER block (ticket line 42):
```csharp
            && (order.OrderState == OrderState.Working || order.OrderState == OrderState.Accepted)
```
Both states are present. Regression safety is preserved.

### 4. CYC comment updated to CYC=6 with DW-B170-01 reference

**PASS**

Change B (ticket line 59) specifies:
```
// CYC=6: (1) null account; (2) zero index; (3) foreach; (4) name match; (5) state==Working; (6) state==Accepted. DW-B170-01.
```
CYC=6 and DW-B170-01 reference are both present.

### 5. ptt-sync.ps1 listed as required post-edit step

**PASS**

Post-edit step section (ticket lines 68–72) explicitly states:
```
powershell -File .\scripts\ptt-sync.ps1
```
with note: "Must exit 0. Required to re-synchronize NinjaTrader hard links after every `.cs` save."

Also confirmed in SCAN-06.

### 6. 7-scan checklist present and complete (SCAN-01 through SCAN-07)

**PASS**

All seven scans present in the engineer contract table (ticket lines 78–86):
- SCAN-01: grep OrderState.Working in FindLeaderStopPrice region
- SCAN-02: grep OrderState.Accepted in FindLeaderStopPrice region
- SCAN-03: grep lock() in src/
- SCAN-04: CYC count verification (6 branches)
- SCAN-05: ASCII-only inspection
- SCAN-06: ptt-sync.ps1 exit code 0
- SCAN-07: diff scope — only FindLeaderStopPrice region modified

### 7. No other methods touched (ticket explicitly states this)

**PASS**

Ticket line 18–24 states: "Only one method is touched. Signature is unchanged." and "No other methods are modified."
SCAN-07 instructs: "Review full diff: only `FindLeaderStopPrice` region (lines 4410 and 4421–4424) modified — No other method bodies or signatures changed."

**TICKET B170-1 VERDICT: TICKET_REVIEW_PASS**

---

## TICKET B170-2 — Add DwB170Tests class to CopyEngineTests.cs

### 8. Test uses FindLeaderStopPriceTestable seam (no NT8 runtime)

**PASS**

Seam reference section (ticket lines 158–161) identifies:
```
CopyEngine.FindLeaderStopPriceTestable(Account? leaderAccount, int bracketIndex)
```
at CopyEngine.cs line 4835 as `internal static`. Critical note to engineer (ticket lines 147–152) explicitly states: "Use the seam `CopyEngine.FindLeaderStopPriceTestable` at line 4835. No NT8 runtime types in the test body."

SCAN-04 in B170-2 requires: "0 NT8 runtime references" in test body.

### 9. Test names present: FindLeaderStopPrice_AcceptedStop_ReturnsPrice AND FindLeaderStopPrice_WorkingStop_StillReturnsPrice

**PASS**

Both [Fact] method names appear in the template (ticket lines 124 and 135) and are confirmed in SCAN-01 and SCAN-02 of the B170-2 checklist.

### 10. No NT8 runtime references in test body

**PASS**

Template body uses only `CopyEngine.FindLeaderStopPriceTestable(/* stubAccount */, bracketIndex: 1)` and `Assert.Equal(...)`. No `NinjaTrader.Cbi.Account`, `Instrument`, `Session`, or other NT8 runtime types appear in the template. SCAN-04 enforces "0 NT8 runtime references."

### 11. No whitespace mutations outside insert site

**PASS**

Ticket line 111: "No existing lines are removed or modified. All other test classes remain intact."
SCAN-05 instructs: "Diff of CopyEngineTests.cs: confirm no whitespace mutations, line-ending changes, or indentation edits outside the insertion site — 0 mutations outside insert site."

### 12. Existing tests not removed or renamed

**PASS**

Ticket lines 110–113 state it is a pure insertion after line 10257. SCAN-06 enforces `[Fact]` count increases by exactly 2 (only additions, no removals). No instruction to remove or rename existing tests anywhere in B170-2.

### 13. ptt-sync.ps1 explicitly NOT required (test-only file) — stated in ticket

**PASS**

Ticket lines 165–167 (ptt-sync.ps1 section) explicitly states:
> "Not required for test-only file changes. `ptt-sync.ps1` synchronizes NinjaTrader hard links for `.cs` files in `src/PropTraderTools/`; test files are not hard-linked to the NT8 AddOn directory. State this explicitly when closing the ticket."

SCAN-07 further specifies: "Confirm `ptt-sync.ps1` was NOT run for this ticket (test-only change) — Not required; state explicitly in completion note."

### 14. 7-scan checklist present and complete (SCAN-01 through SCAN-07)

**PASS**

All seven scans present in the engineer contract table (ticket lines 173–181):
- SCAN-01: grep FindLeaderStopPrice_AcceptedStop_ReturnsPrice in CopyEngineTests.cs
- SCAN-02: grep FindLeaderStopPrice_WorkingStop_StillReturnsPrice in CopyEngineTests.cs
- SCAN-03: grep FindLeaderStopPriceTestable in DwB170Tests (2 matches)
- SCAN-04: inspect test body for NT8 runtime references
- SCAN-05: diff for whitespace mutations outside insertion site
- SCAN-06: [Fact] count increases by exactly 2
- SCAN-07: ptt-sync.ps1 NOT required — state explicitly

**TICKET B170-2 VERDICT: TICKET_REVIEW_PASS**

---

## Cross-Ticket Checks

### 15. Traceability: both tickets reference DW-B170-01

**PASS**

- B170-1, line 10: `**Spec requirement:** DW-B170-01`
- B170-2, line 103: `**Spec requirement:** DW-B170-01`
- B170-1 SCAN-07 references "lines 4410 and 4421–4424" — consistent with architecture plan DW-B170-01 root-cause item.
- B170-1 Change B CYC comment includes `DW-B170-01` as inline reference.

Both tickets are fully traceable to the single spec requirement. No phantom work (items in ticket not in plan). No missing work (all plan items covered: predicate edit, CYC comment, test class).

### 16. No lock() in any proposed change

**PASS**

Neither ticket describes any `lock()`, `Mutex`, `Monitor`, or `SemaphoreSlim` usage. The changed method `FindLeaderStopPrice` uses `leaderAccount.Orders.ToList()` which is the existing pattern. SCAN-03 in B170-1 explicitly requires `grep -r "lock(" src/` returns 0 new matches.

No JS-021 violation found.

### 17. ASCII-only in all changed lines

**PASS**

BEFORE/AFTER blocks in B170-1:
- All characters in `&& (order.OrderState == OrderState.Working || order.OrderState == OrderState.Accepted)` are 7-bit ASCII.
- CYC comment update uses only ASCII alphanumerics, punctuation, and spaces. No Unicode, curly/smart quotes, or emoji.

B170-2 template:
- All class, method, and assert lines use 7-bit ASCII only. No Unicode or special characters present.

SCAN-05 in B170-1 enforces "ASCII-only (7-bit); 0 violations."

---

## Source Spot-Check Matrix

| Item | Ticket-Stated Line | Source Actual (verified) | Match? |
|------|--------------------|--------------------------|--------|
| CYC comment (BEFORE) | 4410 | 4410 — `// CYC=5: (1) null account; (2) zero index; (3) foreach; (4) name match; (5) state==Working.` | EXACT MATCH |
| Predicate open `if (` | 4421 | 4421 — `if (` | EXACT MATCH |
| Predicate name check | 4422 | 4422 — `order.Name == targetName // (4)` | EXACT MATCH |
| Predicate state check | 4423 | 4423 — `&& order.OrderState == OrderState.Working` | EXACT MATCH |
| Predicate close `) // (5)` | 4424 | 4424 — `) // (5)` | EXACT MATCH |

All BEFORE block lines verified against actual source. No drift detected.

---

## Jane Street DNA Pre-Check Summary

| Rule | Check | Result |
|------|-------|--------|
| JS-021 (no lock) | No lock() described in either ticket | CLEAR |
| JS-021 (no Mutex/Monitor) | None proposed | CLEAR |
| JS-023 (UI off-thread) | No UI touch | N/A |
| JS-001 (throw in hot path) | No throw proposed | CLEAR |
| JS-002 (null return) | Returns 0.0 on all failure paths (unchanged) | CLEAR |
| JS-003 (magic string state) | `"Stop" + bracketIndex` is name lookup, not discriminated state | CLEAR |
| JS-008 (mutable struct) | No struct touched | N/A |
| JS-009 (Dictionary for shared state) | No Dictionary added | CLEAR |
| CYC > 8 | CYC=6 post-change | CLEAR |

---

## NT8 Constraint Check

| Constraint | Check | Result |
|-----------|-------|--------|
| async/await in lifecycle method | Not proposed | N/A |
| Account.All outside Loaded handler | Not proposed | N/A |
| sealed on TradeCopierWindow | Not proposed | N/A |
| FontFamily on WPF element | Not proposed | N/A |
| Hardcoded hex color | Not proposed | N/A |
| CreateOrder without PTT- prefix | No CreateOrder in fix | N/A |
| DateTime.Now | Not proposed | N/A |
| OrderState.Accepted is valid NT8 enum member | Confirmed in 02-plan-review.md | CLEAR |

---

## Overall

**TICKET_REVIEW_PASS**

All 17 checklist items pass. Both tickets carry complete 7-scan checklists. BEFORE blocks verified against actual source at stated line numbers. No Jane Street DNA violations. No NT8 constraint violations. No phantom or missing work. Traceability to DW-B170-01 confirmed in both tickets. Engineer contract is complete and unambiguous.

Safe to spawn Phase 4 engineer.
