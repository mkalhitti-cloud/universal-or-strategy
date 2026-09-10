# BWAVE-CYC-IMPL-01 Plan Review
**Epic:** BWAVE-CYC-IMPL-01
**Phase:** 2 — Plan Review (Cycle 2)
**Reviewer:** ptt-plan-reviewer
**Date:** 2026-08
**Input:** docs/brain/BWAVE-CYC-IMPL-01/02-architecture-plan.md (updated after Cycle 1)
**Prior review:** docs/brain/BWAVE-CYC-IMPL-01/02-plan-review.md (Cycle 1 — REVIEW_FAIL on V-01)
**References:**
  - docs/brain/PTT-REPAIRS-09-OBFUSC-ATTR/02-architecture-plan.md (§STEP 1 method inventory)
  - docs/brain/PTT-REPAIRS-09-OBFUSC-ATTR/06-deferred-backlog.md (DW-09-01 scope)
  - docs/protocol/RULES_CATALOG.md — FILE NOT FOUND

---

## LANE-SPLIT GATE: PASS

The plan states `LANE-SPLIT GATE RESULT: SINGLE-PIPELINE` with a complete gate table.
- Q1: All 70 methods in a single insertion block in one file — no split warranted. Not a YES-trigger.
- Q2: N/A — one mission, no cross-ticket dependency. Not a YES-trigger.
- Q3: YES — each ticket is independently mergeable and testable. ✓
- Q4: YES — each ticket has an independent `dotnet test --filter` verification path. ✓

Result: Gate correctly determined as SINGLE-PIPELINE. No violations.

---

## V-01 Fix Verified: PASS

Cycle 1 blocking violation: plan did not carry per-ticket 7-scan checklists.

The updated plan now contains a `7-Scan Checklist` table (SCAN-01 through SCAN-07) for each of the five tickets:

| Ticket | Checklist Section Present | SCAN-01 | SCAN-02 | SCAN-03 | SCAN-04 | SCAN-05 | SCAN-06 | SCAN-07 |
|--------|--------------------------|---------|---------|---------|---------|---------|---------|---------|
| T1 | Lines 698–708 ✓ | PASS ✓ | PASS ✓ | PASS ✓ | PASS ✓ | PASS ✓ | PASS ✓ | REQUIRED ✓ |
| T2 | Lines 710–720 ✓ | PASS ✓ | PASS ✓ | PASS ✓ | PASS ✓ | PASS ✓ | PASS ✓ | REQUIRED ✓ |
| T3 | Lines 722–732 ✓ | PASS ✓ | PASS ✓ | PASS ✓ | PASS ✓ | PASS ✓ | PASS ✓ | REQUIRED ✓ |
| T4 | Lines 734–744 ✓ | PASS ✓ | PASS ✓ | PASS ✓ | PASS ✓ | PASS ✓ | PASS ✓ | REQUIRED ✓ |
| T5 | Lines 746–756 ✓ | PASS ✓ | PASS ✓ | PASS ✓ | PASS ✓ | PASS ✓ | PASS ✓ | REQUIRED ✓ |

SCAN-07 (hard-link sync) is marked REQUIRED (not PASS), which is the correct treatment — it is a post-edit action, not a pre-check. Content of each scan is correct and consistent with the V12 DNA block. V-01 is CLEARED.

---

## V-02 Fix Verified: PASS

Cycle 1 advisory defect: plan line 297 labelled 4 static methods as "(5)" and duplicated entry #20.

Updated plan line 297 now reads:
> `**Static methods (4):** IsPositionFlatOrMissing (#20), IsBracketOrderLiveState (#66), MatchesPttReplacementName (#67), IsOrderEventProcessable (#70).`

Count "(4)" is correct. Duplicate #20 reference is removed. V-02 is CLEARED.

---

## Method Inventory: PASS

Cross-check against PTT-REPAIRS-09-OBFUSC-ATTR §STEP 1 (73 unique names):
- 73 unique names minus 3 already-existing (`LogBeSlotEviction`, `GetSenderAccountName`, `LogDiagOrderCount`) = **70 new methods**.
- Plan inventory table: rows #1–#70. Count = 70 exactly. ✓
- Group sizes: A=24 (#1–24), B=12 (#25–36), C=5 (#37–41), D=24 (#42–65), E=5 (#66–70). Sum = 70. ✓
- `LogBeSlotEviction` excluded, correctly scoped to DW-09-02. ✓
- `GetSenderAccountName` excluded, correctly scoped to DW-09-03. ✓
- `LogDiagOrderCount` excluded (existing method, not in obfuscation-skip category). ✓
- No method appears in the inventory table more than once. ✓

---

## Binding Flags: PASS

| Method / Group | Test Binding Flags | Plan Access | Correct? |
|---|---|---|---|
| Group A (all except #20) | NonPublic \| Instance | private instance | YES ✓ |
| IsPositionFlatOrMissing (#20) | Explicit NonPublic \| **Static** | private **static** | YES ✓ |
| Group B (#25–36) | NonPublic \| Instance | private instance | YES ✓ |
| Group C (#37–41) | NonPublic \| Instance | private instance | YES ✓ |
| Group D (#42–65) | NonPublic \| Instance | private instance | YES ✓ |
| IsBracketOrderLiveState (#66) | GetStaticMethod → NonPublic \| **Static** | private **static** | YES ✓ |
| MatchesPttReplacementName (#67) | GetStaticMethod → NonPublic \| **Static** | private **static** | YES ✓ |
| LogHbcDiag (#68) | GetInstanceMethod → NonPublic \| Instance | private instance | YES ✓ |
| ExecuteStopDragOrder (#69) | GetInstanceMethod → NonPublic \| Instance | private instance | YES ✓ |
| IsOrderEventProcessable (#70) | GetStaticMethod → NonPublic \| **Static** | private **static** | YES ✓ |
| LogBeSlotEviction (NOT in 70) | NonPublic \| Instance (WRONG — DW-09-02) | existing private static | Out of scope — correctly noted ✓ |
| GetSenderAccountName (NOT in 70) | NonPublic \| Instance (WRONG — DW-09-03) | existing internal static | Out of scope — correctly noted ✓ |

No binding-flag mismatch in any of the 70 planned methods.

---

## Signature Completeness: PASS

| Method | Required | Plan Provides | Verdict |
|---|---|---|---|
| SelectBeRefPriceByDirection (#28) | 3 params `(bool isLong, double bid, double ask) → double`; logic verified by 4 test assertions | `(bool isLong, double bid, double ask)` returning `isLong ? (bid > 0 ? bid : ask) : (ask > 0 ? ask : bid)` | ✓ |
| LogHbcDiag (#68) | 5 params, void, private instance | `(Order leaderOrder, Order followerOrder, CopyRule rule, double price, string tag)` — 5 params ✓ | ✓ |
| ExecuteStopDragOrder (#69) | 5 params, void, private instance | `(Account acc, Instrument instr, Order leaderOrder, double stopPrice, CopyRule rule)` — 5 params ✓ | ✓ |
| MatchesPttReplacementName (#67) | 3 params, bool, private static | `(string leaderName, string suffix, string followerName)` — 3 params ✓ | ✓ |
| IsBracketOrderLiveState (#66) | 1 param (Order), bool, private static | `(Order order)` — 1 param ✓ | ✓ |
| IsOrderEventProcessable (#70) | 1 param (OrderEventArgs), bool, private static | `(NinjaTrader.Cbi.OrderEventArgs e)` — 1 param ✓ | ✓ |
| All other 64 stubs | Return type + access + defined parameter list | All have defined return type, access modifier, and parameter list | ✓ |

---

## V12 DNA: PASS

| Rule | Check | Result |
|---|---|---|
| JS-001: No throw | All 70 stubs use `return false`, `return null`, `return 0.0`, `return string.Empty`, empty void body, or `out` param assignment + return. Zero throw statements in all Group A–E code samples. | PASS ✓ |
| JS-002: Return-type discipline | `FindMatchingNativeAtmBracket` and `CreateAndSubmitReplacementTarget` return `Order` (reference type; `return null` is valid stub return for reference types). All predicates return bool. All void methods have void signatures. | PASS ✓ |
| JS-009/JS-021: No lock() | Zero `lock()` calls in all 70 stubs. | PASS ✓ |
| JS-013: CYC <= 8 | Max CYC=4 (`SelectBeRefPriceByDirection`, two nested ternaries). All 69 other stubs CYC=1. | PASS ✓ |
| ASCII-only | All identifiers, string literals, and comments in all 70 stubs are ASCII. No Unicode, no curly quotes. | PASS ✓ |
| No DateTime.Now | No date/time access in any stub. | PASS ✓ |
| No FontFamily override | Not applicable (no UI code in stubs). | PASS ✓ |
| No hardcoded #RRGGBB hex | Not applicable (no UI code in stubs). | PASS ✓ |
| ObfuscationAttribute on every new method | Every method in all 5 code sample blocks carries `[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]`. All 70 covered. | PASS ✓ |
| Static vs Instance correctness | IsPositionFlatOrMissing (#20), IsBracketOrderLiveState (#66), MatchesPttReplacementName (#67), IsOrderEventProcessable (#70) are `private static`. All 66 others are `private instance`. | PASS ✓ |
| .NET 4.8 compatible | No switch expressions, no record types, no `init` accessors, no C# 8+ features in any stub. `SelectBeRefPriceByDirection` uses nested ternaries (valid in all C# versions). | PASS ✓ |
| No existing method modified | Plan explicitly states "Pure insertion only" and "Does not modify any existing CopyEngine.cs method." | PASS ✓ |
| No test Skip removal | Plan explicitly defers all Skip removal to DW-09-04 (out of scope). | PASS ✓ |
| `SelectBeTargetList` returns `new List<Order>()` | New List instantiation per call — not a shared/thread-touched Dictionary. JS-009 does not apply. | PASS ✓ |

---

## Ticket Coverage: PASS

### Count and Assignment

| Ticket | Methods | Test Class | Size <= 25? | Independent? | Verification Command |
|---|---|---|---|---|---|
| T1 | 24 (#1–#24) | B79CancelRaceGuardTests | YES ✓ | YES ✓ | `dotnet test --filter "FullyQualifiedName~B79CancelRaceGuardTests"` ✓ |
| T2 | 12 (#25–#36) | BwaveCycT1R1BeHelperTests | YES ✓ | YES ✓ | `dotnet test --filter "FullyQualifiedName~BwaveCycT1R1BeHelperTests"` ✓ |
| T3 | 5 (#37–#41) | BwaveCycTaR2HelperTests | YES ✓ | YES ✓ | `dotnet test --filter "FullyQualifiedName~BwaveCycTaR2HelperTests"` ✓ |
| T4 | 24 (#42–#65) | BwaveCycTaR3HelperTests | YES ✓ | YES ✓ | `dotnet test --filter "FullyQualifiedName~BwaveCycTaR3HelperTests"` ✓ |
| T5 | 5 (#66–#70) | BwaveCycTaR6HelperTests | YES ✓ | YES ✓ | `dotnet test --filter "FullyQualifiedName~BwaveCycTaR6HelperTests"` ✓ |
| **Total** | **70** | — | — | — | — |

All 70 methods assigned to exactly one ticket. No method appears in two tickets. All tickets target the same insertion region; no inter-ticket build dependency. Build verification via `powershell -File .\scripts\build_readiness.ps1` present.

### 7-Scan Checklist per Ticket: PASS

All 5 tickets now carry complete SCAN-01 through SCAN-07 tables. V-01 is cleared. See V-01 Fix Verified section above for per-ticket verification matrix.

---

## Insertion Plan: PASS

- **Anchor validity:** Plan identifies `PendingDispatchDrain` inner class as the insertion boundary. Confirmed in source: `private sealed class PendingDispatchDrain` begins at L7881. The stated anchor (before the L7876 comment block that precedes the class) is valid. ✓
- **Engineer instruction:** Plan correctly instructs "insert by searching for the PendingDispatchDrain class declaration text, not a fixed line number" (Risk R8) — accommodates line drift from concurrent edits. ✓
- **No existing method modified:** Pure insertion confirmed. Plan states "Does not modify any existing CopyEngine.cs method." ✓
- **No new .cs files created:** Plan states "Does not create any new .cs files." ✓
- **No Skip removed:** Explicitly deferred to DW-09-04. ✓
- **Indentation:** 8 spaces specified, matching surrounding code. ✓
- **Block ordering:** Group A → B → C → D → E. Contiguous single block. ✓

---

## Rules Catalog: N/A

`docs/protocol/RULES_CATALOG.md` does not exist in the workspace. No rules catalog found at any path (glob `**/RULES_CATALOG.md` — zero results). Jane Street DNA block applied directly from role definition.

---

## Violations

None. All prior blocking violations are cleared:

| # | Prior Violation | Cycle 1 Status | Cycle 2 Status |
|---|---|---|---|
| V-01 | 7-scan checklist not planned per ticket (T1–T5) | REVIEW_FAIL trigger | **CLEARED** — all 5 tickets now carry SCAN-01 through SCAN-07 |
| V-02 | Text note at plan line 297 labelled 4 static methods as "(5)" and duplicated #20 | Advisory only | **CLEARED** — corrected to "(4)", duplicate removed |

No new violations found in Cycle 2 review.

---

## Spec Coverage Matrix

| Requirement (from DW-09-01 scope) | Addressed? | Plan Section |
|---|---|---|
| Implement 70 missing CopyEngine helper methods | YES | §Method Inventory, §Implementation Design |
| Each method carries ObfuscationAttribute (rename/Exclude=true) | YES | §V12 DNA Checklist, all code samples |
| Methods are findable by reflection (correct binding flags) | YES | §Binding Flags Analysis, §Test Class Analysis |
| Do not remove any [Fact(Skip)] annotations | YES | §Mission Summary, §Ticket Grouping note |
| Do not modify any existing method | YES | §Mission Summary, §Insertion Plan |
| Do not create any new .cs files | YES | §Mission Summary |
| SelectBeRefPriceByDirection: working implementation with correct logic | YES | §BwaveCycT1R1BeHelperTests analysis, Group B code sample |
| LogHbcDiag: 5-param void instance | YES | §BwaveCycTaR6HelperTests analysis, Group E code sample |
| ExecuteStopDragOrder: 5-param void instance | YES | §BwaveCycTaR6HelperTests analysis, Group E code sample |
| LogBeSlotEviction excluded (already exists, DW-09-02) | YES | §Source File Analysis, §Binding Flags Analysis |
| GetSenderAccountName excluded (already exists, DW-09-03) | YES | §Source File Analysis, §Binding Flags Analysis |
| .NET 4.8 compatible | YES | §V12 DNA Checklist |
| Per-ticket verification commands | YES | §Ticket Grouping |
| Per-ticket 7-scan checklist (SCAN-01 through SCAN-07) | YES | §7-Scan Checklist T1–T5 (V-01 cleared) |

All spec requirements satisfied.

---

## Overall: REVIEW_PASS

All checks pass. Both Cycle 1 violations are cleared. No new violations introduced. The plan is complete, accurate, and ready to proceed to Phase 3 (ticket generation).

**Gate: REVIEW_PASS. Phase 3 (ptt-architect ticket generation) is UNLOCKED.**
