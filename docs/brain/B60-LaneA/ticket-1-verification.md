# B60-LaneA Ticket-1 Verification Report

**Phase**: 4b -- ptt-verifier
**Date**: 2026-08-10
**Verifying**: ticket-1-completion.md (commit 57b10313)
**Verifier**: ptt-verifier (independent Layer 3 scan)

---

## Independent Scan Results

| Scan | Description | Expected | Actual | Pass? |
|------|-------------|----------|--------|-------|
| SCAN-01 | TryDispatchLeaderFlat present (comment + call + def) | >=2 hits | 4 hits (lines 645, 646, 969, 974) | PASS |
| SCAN-02 | IsFollowerAccount guard in TryDispatchLeaderFlat | >=2 hits | 4 hits (lines 397, 400, 482, 976) | PASS |
| SCAN-03 | StartsWith("Rev") present | >=1 hit | 1 hit (line 733) | PASS |
| SCAN-04 | name=="Rev" GONE | 0 hits | 0 hits | PASS |
| SCAN-05 | No lock() (executable) | 0 hits | 0 executable hits (4 comment hits only) | PASS |
| SCAN-06 | No throw new | 0 hits | 0 hits | PASS |
| SCAN-07 | T_B60_ tests present | >=3 | 3 hits (lines 2816, 2823, 2830) | PASS |
| SCAN-08 | TryDispatchLeaderFlat CYC <= 8 | <=8 | CYC=3 (McCabe: 1 + 2 decisions) | PASS |
| SCAN-09 | verify_links.ps1 DESYNC=0 | 0 | DESYNC=0, OK=5, FIXED=0 | PASS |

---

## Architecture Compliance

### Wire-up location (after Cancelled block, before Gate B)
CONFIRMED. Source lines 643-648:

```
643 |             }                                         <-- Cancelled block closes
644 |
645 |             // DW-B60-01: leader went flat -- propagate close to followers
646 |             if (TryDispatchLeaderFlat(e.Order.Account, e.Order.Instrument)) return;
647 |
648 |             // Gate B: bracket drag detection -- divert to HandleBracketChange path
```

The call at line 646 is:
- AFTER Gate 1 (copy enabled, line ~606), Gate 2 (rule match, line ~611-621), Gate 2.5 (rule enabled, line ~624-625)
- AFTER Cancelled block (which already returned at line 642-643 if applicable)
- BEFORE Gate B (bracket drag, line 648)
- BEFORE DispatchCopy (line 658)
This placement is exactly as specified in architecture plan Section C and Section D-2b.

### IsExitSignalName prefix match
CONFIRMED. Source line 733:
```
733 |   if (name.StartsWith("Rev", StringComparison.Ordinal))         return true;
```
`name == "Rev"` is ABSENT (SCAN-04: 0 hits). Full method body at lines 727-736 confirmed correct.

### Git commit present
CONFIRMED. `57b10313` is HEAD.
Message: `fix(ptt): B60 -- leader-close propagation + Rev prefix fix [3 tests]`
Exactly matches commit message specified in 04-tickets.md.

### TryDispatchLeaderFlat method body
CONFIRMED at lines 974-980:
```
974 |         private bool TryDispatchLeaderFlat(Account account, Instrument instrument)
975 |         {
976 |             if (IsFollowerAccount(account)) return false;           // (1) guard: not a follower
977 |             if (HasOpenPosition(account, instrument)) return false; // (2) guard: leader is flat
978 |             Flatten(account, instrument);
979 |             return true;
980 |         }
```
- No throw (JS-001): PASS
- Returns bool not null (JS-002): PASS
- No lock() (JS-021): PASS
- No async void (JS-033): PASS
- ASCII-only: PASS

### Test framework compliance
All 3 tests in CopyEngineTests.cs use xUnit [Fact] ONLY.
No NUnit, no MSTest, no [Theory] found.

---

## DNA Rule Check (JS-XXX)

| Rule | Checked | Result |
|------|---------|--------|
| JS-001 (no throw in hot path) | TryDispatchLeaderFlat body, IsExitSignalName body, OnOrderUpdate wire-up | PASS -- 0 throw new found (SCAN-06) |
| JS-002 (no return null) | TryDispatchLeaderFlat returns bool | PASS -- no null return in new code |
| JS-021 (no lock()) | Full file scan | PASS -- 0 executable lock() calls (4 comment-only hits confirmed) |
| JS-033 (no async void) | All new code | PASS -- no async void added |
| CYC<=8 | TryDispatchLeaderFlat (new method) | PASS -- CYC=3 (verifier count) |
| ASCII-only | All new comments and string literals | PASS -- "Rev", "--", StringComparison.Ordinal all ASCII |
| NT8 API (CreateOrder PTT- prefix) | CopyEngine.cs -- no new CreateOrder calls in B60 | PASS -- B60 does not add CreateOrder |
| FontFamily | Not scanned for B60 (unchanged panel code) | N/A -- not in scope |
| #RRGGBB hex | Not scanned for B60 (no UI changes) | N/A -- not in scope |
| DateTime.Now | Not in new code | N/A -- not in scope |

---

## Layer 2 Comparison (Engineer Self-Report vs Layer 3 Independent Scans)

| Item | Layer 2 (engineer) | Layer 3 (verifier) | Discrepancy? |
|------|--------------------|--------------------|--------------|
| lock() hits | 0 executable (line 837 is comment) | 0 executable (4 comment hits, 0 code) | NO -- both agree |
| throw new | 0 hits | 0 hits | NO |
| name=="Rev" (gone) | 0 hits | 0 hits | NO |
| StartsWith("Rev") | 1 hit at line 733 | 1 hit at line 733 | NO -- exact match |
| T_B60_ tests | 3 hits at lines 2816, 2823, 2830 | 3 hits at lines 2816, 2823, 2830 | NO -- exact match |
| IsFollowerAccount | 4 hits: 397, 400, 482, 976 | 4 hits: 397, 400, 482, 976 | NO -- exact match |
| verify_links DESYNC | DESYNC=0, FIXED=1 | DESYNC=0, FIXED=0 | MINOR -- FIXED=1 during engineer run (first repair), FIXED=0 in verifier run (already repaired). Both report DESYNC=0. Not a violation. |
| TryDispatchLeaderFlat CYC | Engineer claims CYC=2 | Verifier counts CYC=3 (McCabe: 1+2 decisions) | MINOR -- counting methodology difference. Engineer counts decision points only (2). McCabe adds baseline +1 (=3). Both are <=8. No violation. |
| Git commit hash | 57b10313 | 57b10313 (HEAD) | NO -- confirmed |
| Wire-up line | 645 | 645-646 | NO -- line 645 is comment, 646 is call. Engineer cited 645 (comment line). Both present and correct. |

**Summary**: No substantive discrepancies. Two minor annotation differences (CYC counting methodology, FIXED count in verify_links) -- neither represents a code defect or rule violation.

---

## Spec Coverage

| Defect | Requirement | Satisfied? |
|--------|-------------|------------|
| DW-B59-02 | IsExitSignalName must use StartsWith("Rev", Ordinal) instead of exact == "Rev" | YES -- line 733 confirmed |
| DW-B59-02 | 3 new xUnit [Fact] tests covering "Reversal", "RevLong", "RevShort" | YES -- T_B60_Rev_01/02/03 at lines 2816/2823/2830 |
| DW-B60-01 | New private method TryDispatchLeaderFlat(Account, Instrument) returns bool | YES -- lines 974-980 |
| DW-B60-01 | IsFollowerAccount guard as first check in new method | YES -- line 976 |
| DW-B60-01 | HasOpenPosition guard as second check in new method | YES -- line 977 |
| DW-B60-01 | Wire-up call in OnOrderUpdate after Cancelled block, before Gate B | YES -- lines 645-646 |
| DW-B60-01 | Calls Flatten(account, instrument) when leader is flat | YES -- line 978 |
| DW-B60-01 | Returns true if flat (skips DispatchCopy), false if not flat | YES -- lines 979, 976-977 |

All spec requirements covered.

---

## Failures

None.

---

## Verdict

**VERIFY_PASS**

All 9 independent scans passed. Architecture compliance confirmed. Spec requirements fully satisfied.
Git commit 57b10313 verified. No DNA rule violations. No substantive Layer 2 discrepancies.

CYC note (informational only): TryDispatchLeaderFlat is CYC=3 by McCabe standard (not CYC=2 as
engineer stated). This is a counting methodology difference -- method is well within CYC<=8 threshold.
Not a violation.