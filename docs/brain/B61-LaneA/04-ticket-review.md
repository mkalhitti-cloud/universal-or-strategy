# B61-LaneA Ticket Review

**Block**: B61-LaneA
**Phase**: 3.5 (Ticket Review)
**Written by**: ptt-ticket-reviewer
**Date**: 2026-08-10
**Tickets reviewed**: docs/brain/B61-LaneA/04-tickets.md
**Plan reviewed**: docs/brain/B61-LaneA/02-architecture-plan.md
**Live source read**: src/PropTraderTools/CopyEngine.cs lines 620-660, 966-985
**Rules gate**: docs/standards/jane-street/RULES_CATALOG.md (JS-001, JS-002, JS-021)

---

## TICKET-1 Review

### T-01: Traceability — PASS
TICKET-1 header declares `**Spec requirement ID**: DW-B61-01 (P0 regression)`.
Plan Section 2 defines DW-B61-01 with three enumerated sub-bugs.
Not phantom work. Every change in the ticket traces to a documented defect.

### T-02: Both changes required by DW-B61-01 — PASS
Bug 1 (no OrderState guard) → necessitates the state guard in Change 1.
Bug 2 (leader phantom order via Flatten overload) → necessitates removing `Flatten(account, instrument)` call.
Bug 3 (no CopyRule parameter, no follower scoping) → necessitates the `CopyRule rule` param in Change 1 and the `matchedRule.Value` arg in Change 2.
Both Change 1 and Change 2 are required; neither is surplus.

### T-03: Change 1 OLD text matches live CopyEngine.cs lines 969-980 — PASS
Live file read (lines 969-980) confirmed exact byte-level match:
- Line 969: `// DW-B60-01: Detect leader-flat and fan out PTT-Flatten to followers.`
- Line 970: `// CYC=2: (1) follower guard, (2) position guard.`
- Line 971: `// Only called from OnOrderUpdate after Gates 1+2+2.5 (copy enabled, rule matched).`
- Line 972: `// JS-001: no throw. JS-002: returns bool. JS-021: no lock.`
- Line 973: `// TESTABILITY: private instance -- testable via CopyEngine harness.`
- Line 974: `private bool TryDispatchLeaderFlat(Account account, Instrument instrument)`
- Lines 975-980: 2-guard body + `Flatten(account, instrument);` + `return true;` + closing brace
Ticket OLD block is verbatim match to all 12 lines. PASS.

### T-04: Change 2 OLD text matches live CopyEngine.cs line 646 — PASS
Live file line 646:
`if (TryDispatchLeaderFlat(e.Order.Account, e.Order.Instrument)) return;`
Ticket OLD text is identical. PASS.

### T-05: Change 1 NEW adds OrderState state parameter and state guard — PASS
New signature includes `OrderState state` as the 3rd parameter.
Guard at first branch: `if (state != OrderState.Filled && state != OrderState.Cancelled) return false; // (1)`
Requirement satisfied.

### T-06: Change 1 NEW does NOT call Flatten(account, instrument) — PASS
New method body contains only `flattenOne(acc, instrument)` inside the foreach loop.
`Flatten(account, instrument)` (the leader-account overload) does not appear in the new method body.
SCAN-04 in the 7-scan checklist explicitly verifies this.

### T-07: Change 1 NEW includes CopyRule rule parameter and iterates rule.FollowerAccounts — PASS
Signature: `CopyRule rule` as 4th parameter.
Body: `foreach (var acc in rule.FollowerAccounts) { if (acc == null) continue; flattenOne(acc, instrument); }`
Follower scoping is correct.

### T-08: Change 2 NEW passes e.Order.OrderState and matchedRule.Value — PASS
New call site:
```
if (TryDispatchLeaderFlat(
        e.Order.Account, e.Order.Instrument, e.Order.OrderState, matchedRule.Value,
        IsFollowerAccount, HasOpenPosition, FlattenOneAccount)) return;
```
Both `e.Order.OrderState` (3rd arg) and `matchedRule.Value` (4th arg) are present.

### T-09: No lock() in NEW code (JS-021) — PASS
New `TryDispatchLeaderFlat` is `internal static` with delegate parameters only.
No `lock()` statement in any new code block. JS-021 satisfied.

### T-10: No throw new in NEW code (JS-001) — PASS
No `throw` keyword appears in the new method body or at the updated call site.
JS-001 satisfied.

### T-11: No return null in NEW code (JS-002) — PASS
Method return type is `bool`. All exit paths return `false` or `true`.
`return null` is structurally impossible. JS-002 satisfied.

### T-12: All four tests use xUnit [Fact] — PASS
T_B61_01: `[Fact]` ✅
T_B61_02: `[Fact]` ✅
T_B61_03: `[Fact]` ✅
T_B61_04: `[Fact]` ✅
No `[Test]` (NUnit) or `[TestMethod]` (MSTest) attributes present.

### T-13: All test method names carry T_B61_ prefix — PASS
T_B61_01_LeaderHasOpenPosition_ReturnsFalse ✅
T_B61_02_WrongState_Working_ReturnsFalse ✅
T_B61_03_AccountIsFollower_ReturnsFalse ✅
T_B61_04_HappyPath_FlattenOnlyFollowers_ReturnsTrue ✅

### T-14: T_B61_04 verifies leader account is NOT passed to flattenOne — PASS
T_B61_04 passes `account: null` (the leader) and tracks flattened accounts in a List.
Assert: `Assert.DoesNotContain(null, flattenedAccounts); // leader (null) never flattened`
Assert: `Assert.Equal(2, flattenedAccounts.Count)` with `follower1` and `follower2` only.
Leader is structurally excluded because the new method iterates `rule.FollowerAccounts`
(which contains only `follower1` and `follower2`), never the `account` argument.

### T-15: 7-scan checklist present with all 7 scans — PASS
Section "7-scan checklist (engineer must run ALL before BUILD_PASS)" contains a table
with SCAN-01 through SCAN-07, each specifying the exact command and expected result.
All 7 scans are present. Engineer contract is complete.

### T-16: Commit message correct — PASS
Ticket "Commit message" section:
`fix(ptt): B61 -- TryDispatchLeaderFlat state guard + follower-only flatten [4 tests]`
Exact match to required string.

### T-17: verify_links.ps1 -Fix present in deploy steps — PASS
Deploy Step 2: `powershell -File .\scripts\verify_links.ps1 -Fix`
Present and correct.

### T-18: NT8 manual copy path correct — PASS
Deploy Step 1 destination path:
`C:\Users\Mohammed Khalid\Documents\NinjaTrader 8\bin\Custom\AddOns\PropTraderTools\CopyEngine.cs`
Matches the required path exactly.

### T-19: File routing — PASS
Change 1 file: `src/PropTraderTools/CopyEngine.cs` (Wave workspace) ✅
Change 2 file: `src/PropTraderTools/CopyEngine.cs` (Wave workspace) ✅
Test additions: `tests/PropTraderTools.Tests/CopyEngineTests.cs` (test project) ✅
No director workspace paths. No out-of-scope `.cs` files. No phantom file references.

---

## Violations

None.

---

## CYC Pre-Check

New `TryDispatchLeaderFlat` strict McCabe CYC = 6 (5 decision points + 1 base).
Decision points: state guard (1), follower guard (1), open-position guard (1),
foreach loop entry (1), null guard inside loop (1).
Limit: ≤ 8. PASS.

---

## TICKET-1 Verdict

**TICKET_REVIEW_PASS**

All 19 checks passed. Old text verified against live source. New text is JS-compliant
(JS-001, JS-002, JS-021), CYC=6 ≤ 8, xUnit-only tests, 7-scan checklist complete,
file routing correct. Safe to spawn ptt-engineer.
