# B75-LaneB Ticket Review

**Reviewer**: ptt-ticket-reviewer (Phase 3.5)
**Date**: 2026-08-17
**Tickets source**: `docs/brain/B75-LaneB/04-tickets.md`
**Plan source**: `docs/brain/B75-LaneB/02-architecture-plan.md` (REVIEW_PASS)
**Rules source**: `docs/standards/jane-street/RULES_CATALOG.md`

---

## Check 1: Traceability — FAIL

### Hotfix Reference Coverage

All 10 ticket IDs are present in the ticket index. Each ticket carries a `HOTFIX-B66-ATM-TPL`,
`HOTFIX-B66-ATM-OBJ`, or `HOTFIX-B67-CHECKBOX-RESTORE` reference. The prefix "HOTFIX-" vs the
plan's bare "HOTFIX-B66-..." is consistent in meaning. ✓

### T_B67_03 — FATAL DIVERGENCE FROM PLAN

**Plan (Section 6, T_B67 table, row T_B67_03)**:

> `T_B67_03 | OnLoaded restore block: after LoadFollowers() with two followers in saved rule |
> Both matching _followerItems have IsSelected = true; non-matching items remain IsSelected = false`

The plan specifies T_B67_03 as an **integration-level restore test** on `OnLoaded` — verifying
that `_followerItems` UI state is correctly written after the restore sequence fires.

**Ticket T_B67_03 (04-tickets.md) actually implements**:

> `GetSavedFollowerNames(null, "Sim101")` null-instrument defensive contract — verifies the method
> returns an empty `HashSet<string>` without throwing when instrument is `null`.

This is:
1. **Phantom work**: A null-instrument defensive contract test does not appear anywhere in the
   plan. It is not listed in the T_B67 test scope table, nor is it described in any section of
   02-architecture-plan.md.
2. **Missing plan work**: The OnLoaded restore integration test (the actual T_B67_03 per the plan)
   is entirely absent from 04-tickets.md. No ticket covers: "Both matching `_followerItems` have
   `IsSelected = true`; non-matching items remain `IsSelected = false`."

**Verdict**: FAIL — phantom ticket replaces a required plan test.

**Required fix**: The architect must replace the current T_B67_03 with a ticket that tests the
OnLoaded restore block, consistent with the plan. The null-instrument defensive test is welcome
but must be added as T_B67_04 (or equivalent new ID) with explicit plan backing.

### Remaining Traceability

| Ticket | Plan Reference | Spec Requirement Sentence | Maps to Plan Test Scope | Result |
|--------|---------------|--------------------------|------------------------|--------|
| T_B66TPL_01 | Plan §6 T_B66TPL row 1 | Present | `GetLeaderAtmTemplateName(null) → string.Empty` | ✓ |
| T_B66TPL_02 | Plan §6 T_B66TPL row 2 | Present | Chart no ChartTrader → string.Empty | ✓ |
| T_B66TPL_03 | Plan §6 T_B66TPL row 3 | Present | Primary path ct.AtmStrategy non-null | ✓ |
| T_B66TPL_04 | Plan §6 T_B66TPL row 4 | Present | Fallback-1 AtmStrategySelector | ✓ |
| T_B66TPL_05 | Plan §6 T_B66TPL row 5 | Present | All paths null → string.Empty | ✓ |
| T_B66OBJ_P01 | Plan §6 T_B66OBJ_P row 1 | Present | SetCloneAtmObjectCache(nonNull) → Named | ✓ |
| T_B66OBJ_P02 | Plan §6 T_B66OBJ_P row 2 | Present | SetCloneAtmObjectCache(null) → Inherit | ✓ |
| T_B67_01 | Plan §6 T_B67 row 1 | Present | GetSavedFollowerNames matching rule | ✓ |
| T_B67_02 | Plan §6 T_B67 row 2 | Present | GetSavedFollowerNames no matching rule | ✓ |
| T_B67_03 | Plan §6 T_B67 row 3 | **MISMATCH** | Null-instrument test ≠ OnLoaded restore test | ✗ FAIL |

**All test IDs present?** Yes — T_B66TPL_01..05, T_B66OBJ_P01..P02, T_B67_01..03 are all listed
in the index. However the *content* of T_B67_03 diverges from the plan. The 10th required test
(the OnLoaded restore block test) is effectively absent.

---

## Check 2: 7-Scan Checklist Presence — PASS

Every ticket contains a 7-scan checklist with all seven items. Verified per-ticket:

| Ticket | lock() | throw new | return null | async void | CYC<=8 | ASCII | NT8 constraint |
|--------|--------|-----------|-------------|------------|--------|-------|---------------|
| T_B66TPL_01 | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| T_B66TPL_02 | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| T_B66TPL_03 | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| T_B66TPL_04 | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| T_B66TPL_05 | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| T_B66OBJ_P01 | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| T_B66OBJ_P02 | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| T_B67_01 | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| T_B67_02 | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| T_B67_03 | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |

All 10 tickets × 7 items = 70 checklist items present. **PASS.**

---

## Check 3: JS Pre-Check — PASS

Reviewed all Arrange/Act/Assert blocks across all 10 tickets.

| Rule | Check | Result |
|------|-------|--------|
| JS-021 | `lock(` in any test code block | 0 occurrences — PASS |
| JS-001 | `throw new` in any test code block | 0 occurrences — PASS |
| JS-002 | `return null` in any test method | 0 occurrences — PASS |
| JS-033 | `async void` in any test method | 0 occurrences — PASS |
| ASCII | Non-ASCII characters in string literals | 0 occurrences — all literals are ASCII-only |

String literal audit:
- `"MES $200 SL6"` — ASCII (dollar sign U+0024, ASCII 36) ✓
- `"ATM1"` — ASCII ✓
- `"MES SEP26"`, `"Sim101"`, `"Sim102"`, `"Sim103"` — ASCII ✓
- `"T_B67_02_PHANTOM_INSTRUMENT"` — ASCII ✓
- `string.Empty`, `null` — keywords, not string literals ✓

No ConcurrentDictionary or Dictionary described in test code for shared state. ✓

**PASS.**

---

## Check 4: CYC Pre-Check — PASS

All test methods are described as straight-line Arrange/Act/Assert with **zero branches**.
Skip-annotated skeletons (T_B66TPL_02 skip portion, T_B66TPL_03, T_B66TPL_04, T_B66TPL_05)
are also straight-line.

Highest estimated test CYC: 1 (straight-line, no branches). All well below 8.

NT8-HOST-REQUIRED skip annotations:
- T_B66TPL_02 (skip skeleton): `[Fact(Skip="NT8-HOST-REQUIRED")]` — clearly marked ✓
- T_B66TPL_03: `[Fact(Skip="NT8-HOST-REQUIRED")]` — clearly marked ✓
- T_B66TPL_04: `[Fact(Skip="NT8-HOST-REQUIRED")]` — clearly marked ✓
- T_B66TPL_05: `[Fact(Skip="NT8-HOST-REQUIRED")]` — clearly marked ✓

No skip tests are marked without the exact string `"NT8-HOST-REQUIRED"`. ✓

**PASS.**

---

## Check 5: NT8 Constraints — PASS

### GetLeaderAtmTemplateName visual-tree tests (T_B66TPL_03..05)

Correctly annotated `[Fact(Skip="NT8-HOST-REQUIRED")]`. These require `FindVisualChild<ChartTrader>`,
`FindVisualChild<AtmStrategySelector>`, and `FindVisualChildByIndex<ComboBox>` — all of which
traverse a live WPF visual tree that requires NT8 host. Confirmed by reading TradeCopierPanel.cs
lines 2218-2238. ✓

### GetLeaderAtmTemplateName null-input tests (T_B66TPL_01, T_B66TPL_02 unit portion)

Correctly identified as NT8-HOST-NOT-REQUIRED. The null guard at line 2220
(`if (currentChart == null) return string.Empty`) fires before any visual tree traversal.
No NT8 API called on the Guard-1 path. ✓

### SetCloneAtmObjectCache / GetCloneAtmMode (T_B66OBJ_P01, T_B66OBJ_P02)

Correctly identified as NT8-HOST-NOT-REQUIRED for volatile field mechanics. Confirmed by reading
CopyEngine.cs lines 443-463: `_cloneAtmObject = atmObj` is a single volatile field write; both
branches of `GetCloneAtmMode` are pure C# field reads with no NT8 API calls. ✓

T_B66OBJ_P01 correctly documents Options A/B/C for `AtmStrategy` stub construction with fallback
annotation `[Fact(Skip="NT8-HOST-REQUIRED")]` if stub cannot be created without NT8 host. ✓

### FollowerAtmMode type visibility (T_B66OBJ_P01, T_B66OBJ_P02)

`FollowerAtmMode` is declared `public abstract class` at CopyEngine.cs line 75. It is `public` —
accessible from any assembly without `InternalsVisibleTo`. The cross-ticket note at line 498 of
04-tickets.md correctly notes to confirm accessibility from `PropTraderTools.Tests`. Since the
class is `public`, no `InternalsVisibleTo` entry is required for it. ✓

### GetSavedFollowerNames (T_B67_01..03)

Correctly identified as NT8-HOST-NOT-REQUIRED. CopyEngine.cs lines 479-489 confirm: the method
iterates `_rules` (a `ConcurrentBag<CopyRule>`) and builds a local `HashSet<string>`. No NT8 API
is called. T_B67_01 correctly notes that if `Account` requires NT8 host to construct, a skip
annotation fallback is documented. ✓

**PASS.**

---

## Check 6: Completeness — PASS

Each ticket was checked for the full required structure.

| Field | T_B66TPL_01 | T_B66TPL_02 | T_B66TPL_03 | T_B66TPL_04 | T_B66TPL_05 | T_B66OBJ_P01 | T_B66OBJ_P02 | T_B67_01 | T_B67_02 | T_B67_03 |
|-------|-------------|-------------|-------------|-------------|-------------|--------------|--------------|----------|----------|----------|
| ID | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| Hotfix ref | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| Spec requirement | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| Method under test | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| Signature + file + line | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| Test type ([Fact] or Skip) | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| Test class | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| Test name | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| Arrange | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| Act | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| Assert | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| NT8 note | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| 7-scan checklist | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |

Test class consistency: All tickets use `TradeCopierPanelB75Tests`. ✓

xUnit framework: All tickets use `[Fact]` or `[Fact(Skip=...)]`. No NUnit `[Test]`, no MSTest
`[TestMethod]` referenced anywhere. The cross-ticket engineer note explicitly mandates xUnit only
and cites JS-051..065. ✓

Test file routing: `tests/PropTraderTools.Tests/TradeCopierPanelB75Tests.cs` — under the Wave
workspace (`c:\WSGTA\universal-or-strategy\`) not the Director workspace. ✓

Source file paths: All `.cs` source files point to `src/PropTraderTools/TradeCopierPanel.cs` and
`src/PropTraderTools/CopyEngine.cs` — correct Wave workspace routing. ✓

**PASS** (structural completeness; traceability failure for T_B67_03 is captured in Check 1).

---

## Check 7: CopyEngine Singleton Isolation — PASS (informational)

The cross-ticket "Engineer Notes" section (04-tickets.md lines 490-492) explicitly states:

> `CopyEngine.Instance` is a singleton. Tests T_B66OBJ_P01, T_B66OBJ_P02, T_B67_01, T_B67_02,
> T_B67_03 all access the same instance. Engineers MUST ensure test isolation by resetting volatile
> fields and rule state between tests.

Per-ticket arrange sections further specify:
- T_B66OBJ_P01: reset via `SetCloneAtmObjectCache(null)` and `SetCloneAtmCache(string.Empty)` ✓
- T_B67_01: explicitly notes use of `ClearRules()` teardown or `IDisposable` pattern ✓
- T_B67_02: explicitly specifies unique phantom instrument key to avoid state pollution ✓

The singleton isolation concern is acknowledged and mitigated. This is **non-blocking**.

---

## Per-Ticket Verdicts

| Ticket | Traceability | JS Pre-Check | CYC | NT8 | Completeness | 7-Scan | Verdict |
|--------|-------------|-------------|-----|-----|-------------|--------|---------|
| T_B66TPL_01 | PASS | PASS | PASS | PASS | PASS | PASS | **TICKET_REVIEW_PASS** |
| T_B66TPL_02 | PASS | PASS | PASS | PASS | PASS | PASS | **TICKET_REVIEW_PASS** |
| T_B66TPL_03 | PASS | PASS | PASS | PASS | PASS | PASS | **TICKET_REVIEW_PASS** |
| T_B66TPL_04 | PASS | PASS | PASS | PASS | PASS | PASS | **TICKET_REVIEW_PASS** |
| T_B66TPL_05 | PASS | PASS | PASS | PASS | PASS | PASS | **TICKET_REVIEW_PASS** |
| T_B66OBJ_P01 | PASS | PASS | PASS | PASS | PASS | PASS | **TICKET_REVIEW_PASS** |
| T_B66OBJ_P02 | PASS | PASS | PASS | PASS | PASS | PASS | **TICKET_REVIEW_PASS** |
| T_B67_01 | PASS | PASS | PASS | PASS | PASS | PASS | **TICKET_REVIEW_PASS** |
| T_B67_02 | PASS | PASS | PASS | PASS | PASS | PASS | **TICKET_REVIEW_PASS** |
| T_B67_03 | **FAIL** | PASS | PASS | PASS | PASS | PASS | **TICKET_REVIEW_FAIL** |

---

## Violations Summary

| # | Ticket | Check | Violation | Severity |
|---|--------|-------|-----------|----------|
| 1 | T_B67_03 | Check 1: Traceability | Ticket content diverges from plan: plan specifies OnLoaded restore-block integration test ("Both matching `_followerItems` have `IsSelected = true`"); ticket implements null-instrument defensive contract test (`GetSavedFollowerNames(null, ...)`). This constitutes (a) phantom work not in the plan and (b) a missing plan-required test. | BLOCKING |

**No other violations found across all 7 checks for the remaining 9 tickets.**

---

## Required Fix (architect action before re-submission)

**T_B67_03 must be rewritten** to match the plan specification:

- **Method under test**: `OnLoaded` restore block (specifically the `foreach (_followerItems)` step
  that sets `item.IsSelected = true` on name match)
- **Test type**: Must be determined by the architect (likely `[Fact(Skip="NT8-HOST-REQUIRED")]`
  if `_followerItems` requires a running NT8/WPF host, OR a unit test using a stub
  `ObservableCollection<FollowerItem>` if `FollowerItem.IsSelected` is a plain C# property)
- **Assert**: Both matching `_followerItems` entries have `IsSelected = true`; non-matching entries
  have `IsSelected = false`
- **Plan reference**: 02-architecture-plan.md §6 T_B67 row 3

The null-instrument defensive contract (current T_B67_03 content) may optionally be added as a
**new ticket T_B67_04** after it is confirmed with the architect that this test is desired and in
plan scope.

---

## Overall Gate: TICKET_REVIEW_FAIL

**Blocking violation**: T_B67_03 traceability failure — ticket content diverges from the
architecture plan. The plan-required OnLoaded restore test is missing.

**Gate status**: TICKET_REVIEW_FAIL — return to ptt-architect for T_B67_03 rewrite.
Re-submit 04-tickets.md after fix for Phase 3.5 re-review.

**Non-blocking observations** (informational, no re-review needed once T_B67_03 is fixed):
1. Source comment at TradeCopierPanel.cs line 640 cites incorrect CYC (says +0, actual is +4).
   The ticket cross-ticket notes acknowledge this. Engineer to fix during implementation.
2. Source comment at CopyEngine.cs line 478 cites CYC=2 (actual is 5). Acknowledged in plan §7.
   Engineer to fix during implementation.
3. FollowerAtmMode is `public class` — no InternalsVisibleTo entry is required for it
   (the cross-ticket note suggests confirming this, which is correct due diligence).

---

## SECOND PASS (Revision Cycle 1)

**Reviewer**: ptt-ticket-reviewer (Phase 3.5)
**Date**: 2026-08-17
**Trigger**: T_B67_03 was rewritten by architect to fix the single blocking violation from Pass 1.
**Mandate**: Re-run ALL 7 checks across all 10 tickets. No shortcuts.

---

### T_B67_03 Violation — RESOLVED

**Pass 1 violation**: T_B67_03 described a null-instrument defensive test
(`GetSavedFollowerNames(null, "Sim101")`) — phantom work not in the plan, and the
plan-required OnLoaded restore-block predicate test was absent.

**Revised T_B67_03 content** (04-tickets.md lines 437–499):

| Field | Revised value | Plan §6 T_B67 row 3 requirement | Match? |
|-------|--------------|--------------------------------|--------|
| Spec requirement | "restore block in `OnLoaded` MUST correctly pre-check only the follower items that appear in the saved rule" | "Both matching `_followerItems` have `IsSelected = true`; non-matching items remain `IsSelected = false`" | ✓ |
| Method under test | `GetSavedFollowerNames` + `_followerItems` `IsSelected` state logic (restore predicate at TradeCopierPanel.cs lines 648-650) | OnLoaded restore block | ✓ |
| Test name | `T_B67_03_RestoreBlock_OnlyMatchingItemsChecked` | Restore-block test | ✓ |
| Assert | `Assert.True(sim102Selected)` / `Assert.False(sim103Selected)` | Matching items true; non-matching false | ✓ |
| Test type | `[Fact]` (no skip) | Pure logic; no NT8 host required | ✓ |
| 7-scan checklist | All 7 items present | Required | ✓ |

**Approach validation**: The revised ticket uses a predicate-isolation pattern — it calls
`GetSavedFollowerNames` to obtain the real `saved` HashSet, then simulates the restore
predicate (`saved.Contains(name)`) inline using two string stand-ins, bypassing the private
`FollowerItem` class that is not directly instantiable from the test assembly. This mirrors
exactly the logic at TradeCopierPanel.cs lines 648-650 and fulfills the plan's intent that
"the test verifies the same predicate in isolation."

**RESOLVED** — T_B67_03 now correctly maps to plan §6 T_B67 row 3.

---

### All Checks Re-Run

#### Check 1: Traceability — PASS

All 10 tickets re-verified against plan §6.

| Ticket | Plan Reference | Content Match | Result |
|--------|---------------|---------------|--------|
| T_B66TPL_01 | Plan §6 T_B66TPL row 1 | `GetLeaderAtmTemplateName(null) → string.Empty` | ✓ PASS |
| T_B66TPL_02 | Plan §6 T_B66TPL row 2 | Chart no ChartTrader → string.Empty | ✓ PASS |
| T_B66TPL_03 | Plan §6 T_B66TPL row 3 | Primary path ct.AtmStrategy non-null | ✓ PASS |
| T_B66TPL_04 | Plan §6 T_B66TPL row 4 | Fallback-1 AtmStrategySelector | ✓ PASS |
| T_B66TPL_05 | Plan §6 T_B66TPL row 5 | All paths null → string.Empty | ✓ PASS |
| T_B66OBJ_P01 | Plan §6 T_B66OBJ_P row 1 | SetCloneAtmObjectCache(nonNull) → Named | ✓ PASS |
| T_B66OBJ_P02 | Plan §6 T_B66OBJ_P row 2 | SetCloneAtmObjectCache(null) → Inherit | ✓ PASS |
| T_B67_01 | Plan §6 T_B67 row 1 | GetSavedFollowerNames matching rule | ✓ PASS |
| T_B67_02 | Plan §6 T_B67 row 2 | GetSavedFollowerNames no matching rule | ✓ PASS |
| T_B67_03 | Plan §6 T_B67 row 3 | **Restore-block predicate: Sim102 selected, Sim103 not selected** | ✓ PASS (FIXED) |

No phantom work found. No plan-required test missing. All 10 plan test rows have exactly one
corresponding ticket. **PASS.**

---

#### Check 2: 7-Scan Checklist Presence — PASS

T_B67_03 re-verified (04-tickets.md lines 491–499). All 7 items present:

| Item | Present | Notes |
|------|---------|-------|
| `lock()` scan | ✓ | "no new `lock(` in test code" |
| `throw new` scan | ✓ | "no `throw new` in test code" |
| `return null` scan | ✓ | "no `return null` in test method" |
| `async void` scan | ✓ | "no `async void` test method" |
| CYC<=8 | ✓ | "straight-line Arrange/Act/Assert — zero branches; `saved.Contains` calls are expressions" |
| ASCII | ✓ | `"MES SEP26"`, `"Sim101"`, `"Sim102"`, `"Sim103"`, assertion message strings — all ASCII |
| NT8 constraint | ✓ | NT8-HOST-NOT-REQUIRED confirmed; `[Fact]` annotation (no skip) correct |

Full 10-ticket checklist matrix unchanged from Pass 1 (all 9 other tickets confirmed unmodified):

| Ticket | lock() | throw new | return null | async void | CYC<=8 | ASCII | NT8 constraint |
|--------|--------|-----------|-------------|------------|--------|-------|---------------|
| T_B66TPL_01 | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| T_B66TPL_02 | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| T_B66TPL_03 | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| T_B66TPL_04 | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| T_B66TPL_05 | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| T_B66OBJ_P01 | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| T_B66OBJ_P02 | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| T_B67_01 | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| T_B67_02 | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| T_B67_03 | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |

All 70 checklist items present. **PASS.**

---

#### Check 3: JS Pre-Check — PASS

T_B67_03 Arrange/Act/Assert blocks reviewed:

- **Act** (lines 471–474):
  ```
  HashSet<string> saved = CopyEngine.Instance.GetSavedFollowerNames("MES SEP26", "Sim101");
  bool sim102Selected = saved.Contains("Sim102");
  bool sim103Selected = saved.Contains("Sim103");
  ```
- **Assert** (lines 477–480):
  ```
  Assert.True(sim102Selected,  "Sim102 is in the saved rule -- must be selected after restore");
  Assert.False(sim103Selected, "Sim103 is NOT in the saved rule -- must remain unselected");
  ```

| Rule | Check | Result |
|------|-------|--------|
| JS-021 | `lock(` in T_B67_03 code | 0 occurrences — PASS |
| JS-001 | `throw new` in T_B67_03 code | 0 occurrences — PASS |
| JS-002 | `return null` in T_B67_03 code | 0 occurrences — PASS |
| JS-033 | `async void` in T_B67_03 code | 0 occurrences — PASS |
| ASCII | Non-ASCII chars in T_B67_03 strings | 0 — all string literals ASCII-only |

All 9 other tickets: unchanged from Pass 1 — all JS Pre-Checks confirmed PASS. **PASS.**

---

#### Check 4: CYC Pre-Check — PASS

T_B67_03 test body: straight-line — zero control-flow branches. `saved.Contains(...)` are
boolean expressions assigned to local variables, not `if`/`while`/`for` branch points.
CYC = 1. Well below 8. **PASS.**

All other tickets: unchanged — all CYC Pre-Checks confirmed PASS. **PASS.**

---

#### Check 5: NT8 Constraints — PASS

**T_B67_03 NT8 analysis** (confirmed against CopyEngine.cs lines 479–489):

`GetSavedFollowerNames` (CopyEngine.cs line 479): iterates `_rules` (ConcurrentBag<CopyRule>),
builds a `HashSet<string>` from matching rule's `FollowerAccounts`. No NT8 API called in the
method body. Source line 484: `rule.MasterAccount?.Name` — property access on data model class,
not an NT8 API call. Source line 486: `f?.Name` — same. **NT8-HOST-NOT-REQUIRED confirmed.**

The predicate simulation in T_B67_03 (`saved.Contains("Sim102")`) is a `HashSet<string>.Contains`
call — pure BCL. No NT8 dependency. `[Fact]` annotation (no skip) is correct.

All other NT8 annotations unchanged from Pass 1 — confirmed PASS. **PASS.**

---

#### Check 6: Completeness — PASS

T_B67_03 full structure verified:

| Field | Present | Value |
|-------|---------|-------|
| Ticket ID | ✓ | T_B67_03 |
| Hotfix ref | ✓ | HOTFIX-B67-CHECKBOX-RESTORE |
| Spec requirement | ✓ | OnLoaded restore block pre-check predicate |
| Method under test | ✓ | GetSavedFollowerNames + _followerItems IsSelected logic |
| Signatures + file + line | ✓ | CopyEngine.cs line 479, TradeCopierPanel.cs lines 649-650 |
| Test type | ✓ | xUnit `[Fact]` (no skip) |
| Test class | ✓ | TradeCopierPanelB75Tests |
| Test name | ✓ | T_B67_03_RestoreBlock_OnlyMatchingItemsChecked |
| Arrange | ✓ | CopyEngine.Instance reset + rule add + GetSavedFollowerNames call |
| Act | ✓ | `saved.Contains("Sim102")` / `saved.Contains("Sim103")` |
| Assert | ✓ | Assert.True(sim102Selected) / Assert.False(sim103Selected) |
| NT8 constraint note | ✓ | NT8-HOST-NOT-REQUIRED; pure ConcurrentBag + HashSet |
| 7-scan checklist | ✓ | All 7 items present |

All 9 other tickets: unchanged — all Completeness checks confirmed PASS. **PASS.**

---

#### Check 7: No Inadvertent Changes to Other Tickets — PASS

Verified all 9 other tickets are identical to their Pass 1 versions:
- T_B66TPL_01 through T_B66TPL_05: test names, signatures, line references, scan checklists —
  all unchanged. ✓
- T_B66OBJ_P01 and T_B66OBJ_P02: volatile field mechanics, FollowerAtmMode type assertions,
  Options A/B/C for stub construction — all unchanged. ✓
- T_B67_01 and T_B67_02: GetSavedFollowerNames arrange/act/assert, phantom instrument pattern
  for T_B67_02, ConcurrentBag iteration notes — all unchanged. ✓
- Engineer Notes (cross-ticket) section: InternalsVisibleTo note, singleton isolation warning,
  source comment correction note, xUnit mandate — all unchanged. ✓

**PASS.**

---

### Per-Ticket Verdicts (Second Pass)

| Ticket | Traceability | JS Pre-Check | CYC | NT8 | Completeness | 7-Scan | No-Change | Verdict |
|--------|-------------|-------------|-----|-----|-------------|--------|-----------|---------|
| T_B66TPL_01 | PASS | PASS | PASS | PASS | PASS | PASS | N/A | **TICKET_REVIEW_PASS** |
| T_B66TPL_02 | PASS | PASS | PASS | PASS | PASS | PASS | N/A | **TICKET_REVIEW_PASS** |
| T_B66TPL_03 | PASS | PASS | PASS | PASS | PASS | PASS | N/A | **TICKET_REVIEW_PASS** |
| T_B66TPL_04 | PASS | PASS | PASS | PASS | PASS | PASS | N/A | **TICKET_REVIEW_PASS** |
| T_B66TPL_05 | PASS | PASS | PASS | PASS | PASS | PASS | N/A | **TICKET_REVIEW_PASS** |
| T_B66OBJ_P01 | PASS | PASS | PASS | PASS | PASS | PASS | N/A | **TICKET_REVIEW_PASS** |
| T_B66OBJ_P02 | PASS | PASS | PASS | PASS | PASS | PASS | N/A | **TICKET_REVIEW_PASS** |
| T_B67_01 | PASS | PASS | PASS | PASS | PASS | PASS | N/A | **TICKET_REVIEW_PASS** |
| T_B67_02 | PASS | PASS | PASS | PASS | PASS | PASS | N/A | **TICKET_REVIEW_PASS** |
| T_B67_03 | **PASS (FIXED)** | PASS | PASS | PASS | PASS | PASS | ✓ | **TICKET_REVIEW_PASS** |

---

### Violations Summary (Second Pass)

No violations found. Zero blocking issues. Zero warnings requiring re-review.

**All violations from Pass 1 are resolved.**

---

## Overall Gate (Second Pass): TICKET_REVIEW_PASS

The single blocking violation from Pass 1 (T_B67_03 traceability mismatch) has been
correctly resolved. T_B67_03 now implements the plan-required OnLoaded restore-block
predicate test (`T_B67_03_RestoreBlock_OnlyMatchingItemsChecked`), using a predicate-isolation
approach that is pure C# / NT8-host-independent / correctly annotated `[Fact]`.

All 7 checks pass across all 10 tickets. No other tickets were inadvertently modified.

**Gate status**: TICKET_REVIEW_PASS — cleared for Phase 4a (ptt-engineer).
