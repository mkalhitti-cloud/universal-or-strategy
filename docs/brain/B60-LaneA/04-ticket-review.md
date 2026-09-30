# B60-LaneA Ticket Review (Phase 3.5)

**Written by**: ptt-ticket-reviewer
**Date**: 2026-08-10
**Input**: docs/brain/B60-LaneA/04-tickets.md
**Plan source**: docs/brain/B60-LaneA/02-architecture-plan.md (REVIEW_PASS)
**Spec source**: docs/brain/B60-LaneA/orchestrator-prompt.md

---

## Rules Catalog Gate: PASS

RULES_CATALOG.md confirmed UTF-8 clean and fully readable (JS-001..JS-110).
P0 rules in scope for this ticket:

| Rule | Description | In-scope check |
|------|-------------|----------------|
| JS-001 | No `throw new` in hot path | TryDispatchLeaderFlat + OnOrderUpdate insertion |
| JS-002 | No `return null` | TryDispatchLeaderFlat returns bool |
| JS-021 | No `lock()` | All new code |
| JS-033 | No `async void` (non-event) | All new code |

No P0 violations found in live source files. Gate: PASS.

---

## Ticket-1 Review

### Traceability

**T-01: PASS** -- "Spec requirement IDs: DW-B60-01, DW-B59-02" present in ticket header. Ticket
traces to DW-B60-01 (Change 2a + 2b + wire-up) exactly as required by architecture plan Section D-2.

**T-02: PASS** -- "Spec requirement IDs: DW-B60-01, DW-B59-02" also covers DW-B59-02 (Change 1 +
T_B60_Rev_01..03 tests) exactly as required by architecture plan Section D-1.

**T-03: PASS** -- No phantom work detected. All three changes (Change 1, Change 2a, Change 2b)
and all three test methods map directly to DW-B59-02 or DW-B60-01 as authorized by the plan.
No work item present in ticket that is absent from the architecture plan.

---

### OLD Text Verification

**T-04: PASS** -- Change 1 OLD text (ticket lines 37-40) verified line-by-line against live
`src/PropTraderTools/CopyEngine.cs:729-731`:

Live source:
```
            if (name == "Flatten")                                         return true;
            if (name == "Rev")                                             return true;
            if (name.StartsWith("Exit", StringComparison.Ordinal))        return true;
```
Ticket OLD text: identical match including whitespace alignment. Confirmed.

**T-05: PASS** -- Change 2b OLD context (ticket lines 111-120) verified against live
`src/PropTraderTools/CopyEngine.cs:640-647`:

Live source:
```
                    CancelOneAccount(acc, e.Order.Instrument);
                }
                return;
            }

            // Gate B: bracket drag detection -- divert to HandleBracketChange path
            if (IsWorkingBracket(e.Order))
            {
```
Ticket OLD text: identical match including blank line between `}` and Gate B comment. Confirmed.

**T-06: PASS** -- Change 2a insertion anchor (ticket lines 63-71) verified against live
`src/PropTraderTools/CopyEngine.cs:960-966`:

Live source:
```
            var pos = FindPosition(acc, instrument);                        // (1) branch
            if (pos == null)
                return false;
            return pos.Quantity > 0;
        }

        // CYC=3. Returns true if any working non-bracket order exists for the instrument.
```
Ticket OLD text: identical match. HasOpenPosition close-brace at line 964 confirmed.
HasWorkingEntries comment at line 966 confirmed as post-insertion anchor. Confirmed.

**T-07: PASS** -- Test insertion anchor (ticket lines 152-160) verified against live
`src/PropTraderTools/CopyEngineTests.cs:2808-2813`:

Live source:
```
            Assert.False(CopyEngine.IsExitSignalName(""));
        }


    }
}
```
Ticket OLD text: identical match including two blank lines between `}` and class/namespace close.
Insertion before line 2812 (`    }`) is valid. Confirmed.

---

### NEW Text Correctness

**T-08: PASS** -- Change 1 NEW text is exactly:
```
            if (name.StartsWith("Rev", StringComparison.Ordinal))         return true;
```
`StringComparison.Ordinal` is present. No looser comparison (e.g. `OrdinalIgnoreCase`) used.
Matches architecture plan Section D-1 verbatim.

**T-09: PASS** -- `TryDispatchLeaderFlat` new method body:
```csharp
private bool TryDispatchLeaderFlat(Account account, Instrument instrument)
{
    if (IsFollowerAccount(account)) return false;           // (1) guard: not a follower
    if (HasOpenPosition(account, instrument)) return false; // (2) guard: leader is flat
    Flatten(account, instrument);
    return true;
}
```
Contains: zero `lock(`, zero `throw new`, zero `return null`. Returns `bool` only. PASS.

**T-10: PASS** -- Wire-up call `if (TryDispatchLeaderFlat(e.Order.Account, e.Order.Instrument)) return;`
is inserted after line 643 (Cancelled block `return;`) and before line 645 (`// Gate B:`).
This is after Gates 1 (line 606), 2 (line 620), and 2.5 (line 624) and before Gate B and
`DispatchCopy`. Placement is correct per architecture plan Section C and D-2b. PASS.

---

### Test Quality

**T-11: PASS** -- All 3 test methods use xUnit `[Fact]` exclusively.
No `[Theory]`, no NUnit `[Test]`, no MSTest `[TestMethod]` anywhere in the ticket's test section.

**T-12: PASS** -- All 3 test method names contain `T_B60_`:
- `T_B60_Rev_01_IsExitSignalName_Reversal_ReturnsTrue`
- `T_B60_Rev_02_IsExitSignalName_RevLong_ReturnsTrue`
- `T_B60_Rev_03_IsExitSignalName_RevShort_ReturnsTrue`

**T-13: PASS** -- All assertions are logically correct after the Change 1 fix:
- `Assert.True(CopyEngine.IsExitSignalName("Reversal"))` -- "Reversal".StartsWith("Rev") == true
- `Assert.True(CopyEngine.IsExitSignalName("RevLong"))` -- "RevLong".StartsWith("Rev") == true
- `Assert.True(CopyEngine.IsExitSignalName("RevShort"))` -- "RevShort".StartsWith("Rev") == true
All three would return `false` under the old `name == "Rev"` exact match, confirming the regression
these tests catch. Assertions are correct.

---

### Compliance

**T-14: PASS** -- No `lock(` in any new code (TryDispatchLeaderFlat, insertion line, test bodies).
JS-021 satisfied. Rule table in ticket explicitly confirms PASS.

**T-15: PASS** -- No `throw new` in any new code. JS-001 satisfied. Rule table in ticket
explicitly confirms PASS.

**T-16: PASS** -- No `return null` in any new code. `TryDispatchLeaderFlat` returns `bool` only.
JS-002 satisfied. Rule table in ticket explicitly confirms PASS.

**T-17: PASS** -- All new string literals are ASCII-only: "Rev", "PTT-Flatten" (existing),
comment text uses "--" (two ASCII hyphens, not em-dash). Rule table in ticket confirms PASS.

**T-18: PASS** -- CYC for all new methods:
- `TryDispatchLeaderFlat` (new): CYC=2. <= 8. PASS.
- `IsExitSignalName` (modified): CYC=6 (unchanged -- one `==` replaced by `StartsWith`,
  same branch count). <= 8. PASS.
- `OnOrderUpdate` pre-existing CYC violation (11->12) is acknowledged but NOT introduced by B60.
  The CYC constraint applies only to new methods. No new method exceeds CYC=8.

---

### Infrastructure

**T-19: PASS** -- `powershell -File .\scripts\verify_links.ps1 -Fix` appears in:
- Verification Steps (item 4)
- SCAN-08 in the 7-scan table
- Commit Steps (item 4)
Three independent references ensure engineer cannot miss it.

**T-20: PASS** -- Commit message in ticket header is exactly:
`fix(ptt): B60 -- leader-close propagation + Rev prefix fix [3 tests]`
Test count `[3 tests]` matches the 3 automated [Fact] tests (T_B60_Rev_01..03).
Commit Steps section mirrors the same message. Consistent.

**T-21: PASS** -- Files touched are:
- `src/PropTraderTools/CopyEngine.cs` (Wave workspace)
- `src/PropTraderTools/CopyEngineTests.cs` (Wave workspace)
No Director workspace (`universal-or-strategy-director`) paths. No `.cs` path outside
`src/PropTraderTools/`. File routing correct.

**T-22: PASS** -- 7-scan checklist is present. All 8 scans (SCAN-01 through SCAN-08) are present
with exact shell commands and required result per scan. Defense-in-depth contract is complete.

| Scan | Present | Command | Required Result |
|------|---------|---------|----------------|
| SCAN-01 | YES | `grep -n "lock(" CopyEngine.cs` | 0 matches |
| SCAN-02 | YES | `grep -n "throw new" CopyEngine.cs` | 0 matches |
| SCAN-03 | YES | `grep -n "return null" CopyEngine.cs` | 0 matches |
| SCAN-04 | YES | `grep -n 'name == "Rev"' CopyEngine.cs` | 0 matches |
| SCAN-05 | YES | `grep -n 'StartsWith.*"Rev"' CopyEngine.cs` | >= 1 match |
| SCAN-06 | YES | `grep -n "T_B60_" CopyEngineTests.cs` | >= 3 matches |
| SCAN-07 | YES | `grep -n "IsFollowerAccount" CopyEngine.cs` | >= 2 matches |
| SCAN-08 | YES | `powershell -File .\scripts\verify_links.ps1 -Fix` | DESYNC=0, exit 0 |

---

## Violations

None.

---

## Verdict: Ticket-1

**TICKET_REVIEW_PASS**

All 22 checklist items PASS. No JS rule violations. No OLD text mismatches. No phantom work.
No missing scan entries. File routing correct. Commit message exact. xUnit [Fact] only.

The engineer may proceed to Phase 4a.
