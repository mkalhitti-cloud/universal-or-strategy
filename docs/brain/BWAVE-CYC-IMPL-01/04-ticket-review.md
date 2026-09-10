# BWAVE-CYC-IMPL-01 Ticket Review
**Epic:** BWAVE-CYC-IMPL-01
**Phase:** 3.5 — Ticket Review
**Reviewer:** ptt-ticket-reviewer
**Input:** docs/brain/BWAVE-CYC-IMPL-01/04-tickets.md
**Input:** docs/brain/BWAVE-CYC-IMPL-01/02-architecture-plan.md
**Input:** docs/brain/BWAVE-CYC-IMPL-01/02-plan-review.md (REVIEW_PASS, Cycle 2)
**Source checked:** src/PropTraderTools/CopyEngine.cs
**Tests checked:** src/PropTraderTools/CopyEngineTests.cs (lines 5829–7274)

---

## Traceability: PASS

Every ticket carries:
- `**Epic:** BWAVE-CYC-IMPL-01` ✓
- `**Spec Req IDs:** DW-09-01 (BWAVE-CYC-IMPL-01 Group [A|B|C|D|E])` ✓
- `**Source Test Class(es):**` referencing one of the 5 approved test classes ✓

Method-to-test-class traceability:

| Ticket | Methods | Test Class | Plan § |
|--------|---------|-----------|--------|
| T1 | #1–#24 (24) | B79CancelRaceGuardTests | Plan §B79CancelRaceGuardTests ✓ |
| T2 | #25–#36 (12) | BwaveCycT1R1BeHelperTests | Plan §BwaveCycT1R1BeHelperTests ✓ |
| T3 | #37–#41 (5) | BwaveCycTaR2HelperTests | Plan §BwaveCycTaR2HelperTests ✓ |
| T4 | #42–#65 (24) | BwaveCycTaR3HelperTests | Plan §BwaveCycTaR3HelperTests ✓ |
| T5 | #66–#70 (5) | BwaveCycTaR6HelperTests | Plan §BwaveCycTaR6HelperTests ✓ |

Total: 24+12+5+24+5 = **70 methods** ✓ (matches plan requirement exactly)
No method appears in more than one ticket ✓
No phantom work (all 70 methods trace to plan §Method Inventory) ✓
No missing work (all 70 plan methods are assigned to a ticket) ✓

---

## Method Completeness: PASS

Checks per ticket:

**Signatures** — every method carries: access modifier (`private`), static/instance qualifier where applicable, return type, parameter list with types and names. Spot-checked: all 70 insertion-block entries confirm full signatures ✓

**ObfuscationAttribute** — every method description and every insertion block carries
`[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]` on the line immediately above the method signature ✓

**No duplicate methods across tickets:**
- No method name appears in more than one ticket group ✓
- Plan §Method Inventory row cross-check confirms uniqueness ✓

**No conflicts with existing CopyEngine.cs methods:**
- grep for all 6 spot-check names (`IsPositionFlatOrMissing`, `IsBracketOrderLiveState`, `SelectBeRefPriceByDirection`, `TryFireImmediateBeIfAlreadyAtLevel`, `LogHbcDiag`, `ExecuteStopDragOrder`) → 0 matches in current CopyEngine.cs ✓
- Insertion anchor `private class PendingDispatchDrain` confirmed present at L7881 ✓
- Excluded pre-existing methods (`LogBeSlotEviction` L1779, `GetSenderAccountName` L6970, `LogDiagOrderCount` L6424) are correctly absent from the 70-method list ✓

---

## JS Pre-Check (CYC): PASS

| Rule | Check | Result |
|------|-------|--------|
| CYC stated per method | Every of 70 method descriptions includes `- CYC estimate:` | PASS ✓ |
| CYC <= 8 maximum | Max is CYC=4 (SelectBeRefPriceByDirection #28); all 69 others CYC=1 | PASS ✓ |
| SelectBeRefPriceByDirection logic | Ticket states: `return isLong ? (bid > 0 ? bid : ask) : (ask > 0 ? ask : bid);` | Verified ✓ |
| Test case (true, 100.25, 100.50) → 100.25 | isLong=true, bid=100.25>0 → bid → 100.25 | ✓ |
| Test case (true, 0.0, 100.50) → 100.50 | isLong=true, bid=0 → ask → 100.50 | ✓ |
| Test case (false, 100.25, 100.50) → 100.50 | isLong=false, ask=100.50>0 → ask → 100.50 | ✓ |
| Test case (false, 100.25, 0.0) → 100.25 | isLong=false, ask=0 → bid → 100.25 | ✓ |
| No lock() in any ticket description | Zero lock() calls across all 70 stubs | PASS ✓ |
| No throw in any ticket description | All stubs: return false/null/0.0/string.Empty, void, or out+return | PASS ✓ |
| No DateTime.Now | No date/time access in any stub | PASS ✓ |
| No Unicode | All identifiers and literals are 7-bit ASCII | PASS ✓ |
| No .NET 5+ syntax | No switch expressions, no record types, no init accessors, no C# 8+ features. SelectBeRefPriceByDirection uses classic nested ternary (valid C# 3+) | PASS ✓ |

---

## Parameter Count Hard Checks: PASS

Cross-referenced against test source in CopyEngineTests.cs:

| Method | Ticket | Plan Params | Test Assertion | Ticket Signature Params | Match? |
|--------|--------|-------------|----------------|------------------------|--------|
| LogHbcDiag | T5 | 5 | L7202: `Assert.Equal(5, m.GetParameters().Length)` | `(Order leaderOrder, Order followerOrder, CopyRule rule, double price, string tag)` = 5 | ✓ PASS |
| ExecuteStopDragOrder | T5 | 5 | L7219: `Assert.Equal(5, m.GetParameters().Length)` | `(Account acc, Instrument instr, Order leaderOrder, double stopPrice, CopyRule rule)` = 5 | ✓ PASS |
| IsBracketOrderLiveState | T5 | 1, private static | L7139: `Assert.Equal(1, m.GetParameters().Length)` + GetStaticMethod | `private static bool IsBracketOrderLiveState(Order order)` = 1 + static | ✓ PASS |
| MatchesPttReplacementName | T5 | 3, private static | L7185: `Assert.Equal(3, m.GetParameters().Length)` + GetStaticMethod | `private static bool MatchesPttReplacementName(string leaderName, string suffix, string followerName)` = 3 + static | ✓ PASS |
| IsOrderEventProcessable | T5 | 1, private static | L7272: `Assert.Equal(1, m.GetParameters().Length)` + GetStaticMethod | `private static bool IsOrderEventProcessable(NinjaTrader.Cbi.OrderEventArgs e)` = 1 + static | ✓ PASS |
| IsPositionFlatOrMissing | T1 | 1, private static | GetMethod with BindingFlags.NonPublic\|Static (plan L79) | `private static bool IsPositionFlatOrMissing(NinjaTrader.Cbi.Position pos)` = 1 + static | ✓ PASS |

---

## 7-Scan Checklist: PASS

This check failed Phase 2 Cycle 1 and was the blocking violation. Verified present and correct in all 5 tickets:

| Ticket | Section | SCAN-01 | SCAN-02 | SCAN-03 | SCAN-04 | SCAN-05 | SCAN-06 | SCAN-07 |
|--------|---------|---------|---------|---------|---------|---------|---------|---------|
| T1 | "7-Scan Checklist (Ticket 1)" | grep lock( ✓ | grep DateTime.Now ✓ | grep non-ASCII ✓ | grep FontFamily ✓ | grep hex-color ✓ | grep \bthrow\b ✓ | deploy-sync.ps1 ✓ |
| T2 | "7-Scan Checklist (Ticket 2)" | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| T3 | "7-Scan Checklist (Ticket 3)" | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| T4 | "7-Scan Checklist (Ticket 4)" | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| T5 | "7-Scan Checklist (Ticket 5)" | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |

Each checklist carries the exact grep/powershell command the engineer must run. SCAN-07 correctly marked as a post-edit action (not a static pass). All 5 tickets × 7 scans = 35 checklist entries confirmed present ✓

---

## NT8 Constraints: PASS

| Constraint | Check | Result |
|------------|-------|--------|
| No System.Windows.Media.FontFamily | No FontFamily reference in any ticket description or insertion block | PASS ✓ |
| No System.Windows.Media.Color hex literals | No hex color strings in any ticket description or insertion block | PASS ✓ |
| .NET 4.8 only | No switch expressions, no record types, no init-only setters, no C# 8+ constructs. All confirmed in each ticket's "V12 DNA Constraints" section | PASS ✓ |
| No async/await in stubs | All stubs are synchronous single-expression or empty bodies | PASS ✓ |
| No NT8 type instantiation in method bodies | Method bodies are stubs: `{ return false; }`, `{ return null; }`, `{ }`, `{ return 0.0; }`, `{ return string.Empty; }`, `{ return new System.Collections.Generic.List<Order>(); }`, and out-param assignments. None instantiate NT8 types | PASS ✓ |
| Method bodies that accept NT8 parameter types | NT8 types appear only as parameter types (Account, Instrument, Order, NinjaTrader.Cbi.Position, NinjaTrader.Cbi.OrderEventArgs), never instantiated inside bodies | PASS ✓ |
| No sealed on CopyEngine or TradeCopierWindow | No sealed modifier added anywhere | PASS ✓ |
| No FontFamily set on WPF element | N/A — no WPF code in stubs | PASS ✓ |
| No CreateOrder with non-PTT- name | N/A — no order creation in stubs | PASS ✓ |
| No DateTime.Now | Confirmed absent from all stubs | PASS ✓ |
| CopyRule inner class usage | Tickets T1, T2, T4, T5 reference CopyRule — confirmed as inner class of CopyEngine, in scope within CopyEngine.cs without any using directive. Tickets correctly note this | PASS ✓ |

---

## Build Isolation: PASS

| Check | Result |
|-------|--------|
| Each ticket is independently buildable | All 5 tickets target the same insertion anchor; no ticket's methods call any method from another ticket | PASS ✓ |
| No cross-ticket method references | All 70 stubs have empty or single-expression bodies — no calls to any other new method | PASS ✓ |
| Insertion anchor is unambiguous | All 5 tickets use identical search-text anchor: `private class PendingDispatchDrain`. Confirmed at L7881 in CopyEngine.cs ✓ | PASS ✓ |
| Sequential insertion order described | Tickets specify each group inserts "after any previously-inserted group blocks" — prevents anchor collision between sequential tickets | PASS ✓ |
| No new .cs files | All tickets carry "Do NOT create any new files" in Scope Lock | PASS ✓ |
| No modification to existing methods | All tickets carry "Do NOT modify any existing method" in Scope Lock | PASS ✓ |
| Build command per ticket | Each ticket specifies `dotnet build src/PropTraderTools/PropTraderTools.csproj` with expected "0 Error(s)" | PASS ✓ |

---

## Scope Lock: PASS

| Ticket | Scope Lock Present | Completion Artifact Path |
|--------|--------------------|--------------------------|
| T1 | "TICKET 1 ONLY. Do NOT read, reference, or implement any other ticket. Do NOT modify any existing method. Do NOT create any new files." | `docs/brain/BWAVE-CYC-IMPL-01/ticket-1-completion.md` ✓ |
| T2 | "TICKET 2 ONLY." + same prohibitions | `docs/brain/BWAVE-CYC-IMPL-01/ticket-2-completion.md` ✓ |
| T3 | "TICKET 3 ONLY." + same prohibitions | `docs/brain/BWAVE-CYC-IMPL-01/ticket-3-completion.md` ✓ |
| T4 | "TICKET 4 ONLY." + same prohibitions | `docs/brain/BWAVE-CYC-IMPL-01/ticket-4-completion.md` ✓ |
| T5 | "TICKET 5 ONLY." + same prohibitions | `docs/brain/BWAVE-CYC-IMPL-01/ticket-5-completion.md` ✓ |

All scope locks are present, unambiguous, and specify per-ticket completion artifact paths ✓

---

## Rules Catalog: N/A

`docs/protocol/RULES_CATALOG.md` does not exist (glob confirms zero results). Jane Street DNA rules applied directly from the reviewer role definition. All applicable rules (JS-001, JS-002, JS-009, JS-013, JS-021, JS-023, JS-025) were verified inline in the JS Pre-Check and NT8 Constraints sections above.

---

## Test Coverage Note (Advisory — Not FAIL)

Per the PTT role definition, every new public or internal method must have a `[Fact]` test method name specified in the ticket. All 70 methods in this epic are **private** — they are tested by existing `[Fact(Skip = "obfuscation:...")]` tests already present in the test file (verified: B79CancelRaceGuardTests, BwaveCycT1R1BeHelperTests, BwaveCycTaR2HelperTests, BwaveCycTaR3HelperTests, BwaveCycTaR6HelperTests). The `[Fact]` tests exist; skip removal is out of scope (DW-09-04). The mandatory rule applies to public/internal methods — private methods tested via reflection stubs are correctly handled by referencing the existing test class rather than defining new test methods. No new `[Fact]` methods are required. **Advisory: PASS (no new test methods needed).**

---

## Violations: NONE

| # | Check | Status |
|---|-------|--------|
| V-01 | 7-scan checklist per ticket | CLEARED (was Cycle 1 blocker — all 5 tickets now carry SCAN-01 through SCAN-07) |
| V-02 | Static method count typo | CLEARED in architecture plan (prior cycle) |

No new violations found.

---

## Per-Ticket Verdicts

### T1 — Group A: B79CancelRaceGuard Helpers — 24 Methods
- Traceability: PASS
- JS Pre-Check: PASS
- CYC Pre-Check: PASS (all CYC=1)
- NT8 Check: PASS
- Parameter Count: PASS (IsPositionFlatOrMissing 1-param static confirmed)
- Scan Checklist: PASS (SCAN-01 through SCAN-07 present)
- File Routing: PASS (`src/PropTraderTools/CopyEngine.cs` Wave workspace)
- Scope Lock: PASS
- **VERDICT: TICKET_REVIEW_PASS**

### T2 — Group B: T1R1 BE Trigger/Arming Helpers — 12 Methods
- Traceability: PASS
- JS Pre-Check: PASS
- CYC Pre-Check: PASS (max CYC=4 for SelectBeRefPriceByDirection — confirmed <= 8)
- NT8 Check: PASS
- Parameter Count: PASS (SelectBeRefPriceByDirection 3-param confirmed against test invocations at L6511/6521/6531/6541)
- Scan Checklist: PASS (SCAN-01 through SCAN-07 present)
- File Routing: PASS
- Scope Lock: PASS
- **VERDICT: TICKET_REVIEW_PASS**

### T3 — Group C: TaR2 Target-Selection Helpers — 5 Methods
- Traceability: PASS
- JS Pre-Check: PASS
- CYC Pre-Check: PASS (all CYC=1)
- NT8 Check: PASS
- Parameter Count: PASS
- Scan Checklist: PASS (SCAN-01 through SCAN-07 present)
- File Routing: PASS
- Scope Lock: PASS
- **VERDICT: TICKET_REVIEW_PASS**

### T4 — Group D: TaR3 Sync/Drag/Bracket Helpers — 24 Methods
- Traceability: PASS
- JS Pre-Check: PASS
- CYC Pre-Check: PASS (all CYC=1)
- NT8 Check: PASS
- Parameter Count: PASS (out-parameter methods TryGetCleanupEntryForFollower, TryMatchFollowerInRule confirmed: tests assert NotNull only — no parameter-count assertion; stub uses `out object` and `out int` placeholders correctly)
- Scan Checklist: PASS (SCAN-01 through SCAN-07 present)
- File Routing: PASS
- Scope Lock: PASS
- **VERDICT: TICKET_REVIEW_PASS**

### T5 — Group E: TaR6 Static/Instance Predicate Helpers — 5 Methods
- Traceability: PASS
- JS Pre-Check: PASS
- CYC Pre-Check: PASS (all CYC=1)
- NT8 Check: PASS
- Parameter Count: PASS (all 5 critical counts verified against test source: IsBracketOrderLiveState=1, MatchesPttReplacementName=3, LogHbcDiag=5, ExecuteStopDragOrder=5, IsOrderEventProcessable=1)
- Scan Checklist: PASS (SCAN-01 through SCAN-07 present)
- File Routing: PASS
- Scope Lock: PASS
- **VERDICT: TICKET_REVIEW_PASS**

---

## Overall: TICKET_REVIEW_PASS

All 5 tickets pass all 9 review checks with zero violations. The prior Phase 2 Cycle 1 blocker (missing per-ticket 7-scan checklists) is confirmed cleared in all tickets. The engineer contract is complete: every ticket contains spec requirement IDs, exact method signatures, ObfuscationAttribute declarations, 7-scan checklist (SCAN-01 through SCAN-07), Scope Lock, and completion artifact path.

**Gate: TICKET_REVIEW_PASS. Phase 4 (ptt-engineer implementation) is UNLOCKED.**
