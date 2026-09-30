# B58-LaneA Ticket Review

**Reviewer**: ptt-ticket-reviewer
**Date**: 2026-08-10
**Ticket file**: docs/brain/B58-LaneA/04-tickets.md
**Plan file**: docs/brain/B58-LaneA/02-architecture-plan.md (REVIEW_PASS, 0 violations)
**Plan review**: docs/brain/B58-LaneA/02-plan-review.md (REVIEW_PASS, 2 prior violations resolved)
**Rules**: docs/standards/jane-street/RULES_CATALOG.md

---

## Review Result: TICKET_REVIEW_PASS

**Violations**: 0

---

## Criteria Results

| ID | Criterion | Result | Notes |
|----|-----------|--------|-------|
| T1 | Traceability | PASS | All 19 changes trace to cited prior brain artifacts. See detail below. |
| T2 | 7-Scan Checklist Completeness | PASS | All 7 scans present with exact commands and expected results. See detail below. |
| T3 | Code Correctness | PASS | Signatures, field names, types, and anchors verified against live source. See detail below. |
| T4 | JS Rule Constraints Embedded | PASS | JS-021, JS-033, JS-001, JS-002, JS-066, JS-023, NT8-001, NT8-003 embedded verbatim in ticket's JS Rule Constraints table. |
| T5 | CYC Pre-Check | PASS | All 13 new/modified members have CYC documented; max is 3 (SnapshotTargetsPublic). All ≤ 8. |
| T6 | NT8 Constraints | PASS | All NT8 types already in scope; no new using directives; no banned NT8 patterns. |
| T7 | Insertion Order | PASS | Fields precede dependent methods; GetCloneAtmMode precedes ResolveAtmMode; DispatchCopy anchor confirmed at line 699. |
| T8 | Definition of Done | PASS | 19-item change checklist + 7 explicit SCAN checks + ticket-1-completion.md output artifact named. |

---

## Detailed Criterion Evidence

### T1 — Traceability

Every ticket item maps to exactly one prior brain artifact or plan section:

| Change # | Member | Source Artifact |
|----------|--------|----------------|
| 1 | `CopyEngine : ICopyEngine` | B58-§1 (PttContracts.cs ICopyEngine line 79) |
| 2 | `_cloneAtmCache` field | B50-§CE (ticket-1-completion.md) |
| 3 | `_globalBe` field | B39-§CopyEngine (ticket-1-completion.md Implementation Note 1) |
| 4 | `IsEnabled` property | B54-§A1 (ticket-1-completion.md) |
| 5 | `GlobalBe` property | B39-§CopyEngine (ticket-1-completion.md) |
| 6 | `RelayBe` method | B58-§1 + PttContracts.cs line 82 |
| 7 | `RelayTrim` method | B58-§1 + PttContracts.cs line 85 |
| 8 | `RelayFlatten` method | B58-§1 + PttContracts.cs line 88 |
| 9 | `RelayCancel` method | B58-§1 + PttContracts.cs line 91 |
| 10 | `SetCloneAtmCache` method | B50-§CE |
| 11 | `GetCloneAtmMode` method | B50-§CE |
| 12 | `ResolveAtmMode` method | B50-§CE (after GetAtmMode ~line 889) |
| 13 | DispatchCopy call-site | B50-§CE (line 699 substitution) |
| 14 | `IsPendingSlotsEmpty` method | B40-§3 |
| 15 | `FindPositionPublic` method | B58-§1 (public wrapper of private FindPosition) |
| 16 | `SnapshotTargetsPublic` method | B58-§1 (panel needs working-order snapshot count) |
| 17 | `CopyRulesContainer.CopyEnabled` | B54-§A2 |
| 18 | `SaveRules` CopyEnabled write | B54-§A3 |
| 19 | `LoadRules` CopyEnabled restore | B54-§A4 |

No phantom work (ticket items absent from plan/spec). No missing work (all plan Section C items
covered). PASS.

---

### T2 — 7-Scan Checklist Completeness

All 7 scans verified present in ticket section "7-Scan Checklist (Engineer Contract)":

| Scan | Command Present | Expected Result Stated | Pre-existing Exemption Noted |
|------|----------------|----------------------|------------------------------|
| SCAN-01 lock() | `grep -n "lock\s*(" src/PropTraderTools/CopyEngine.cs` ✅ | 0 results ✅ | Comments acceptable ✅ |
| SCAN-02 async void | `grep -n "async void " src/PropTraderTools/CopyEngine.cs` ✅ | 0 results ✅ | — |
| SCAN-03 return null | `grep -n "return null;" src/PropTraderTools/CopyEngine.cs` ✅ | 0 new in B58 scope ✅ | FindPositionPublic pre-existing contract documented ✅ |
| SCAN-04 throw new | `grep -n "throw new" src/PropTraderTools/CopyEngine.cs` ✅ | 0 new in B58 scope ✅ | — |
| SCAN-05 CYC ≤ 8 | Table of all 15 new/modified members with CYC values ✅ | Max 3 (SnapshotTargetsPublic) ✅ | DispatchCopy AT LIMIT=8, 0 new branches ✅ |
| SCAN-06 build | `dotnet build src/PropTraderTools/PropTraderTools.csproj` ✅ | 0 new errors ✅ | AtrSizingEngine.cs 2 pre-existing CS0234/CS0246 exempt ✅ |
| SCAN-07 hard-link | `powershell -File scripts\verify_links.ps1 -Fix` ✅ | DESYNC=0, MISSING=0 ✅ | Run AFTER commit noted ✅ |

PASS — all 7 present; all carry exact commands; all carry pass criteria.

---

### T3 — Code Correctness

**Interface signature match** (PttContracts.cs ICopyEngine lines 82/85/88/91):

| Interface declaration | Ticket implementation | Match |
|----------------------|----------------------|-------|
| `void RelayBe(BeEventArgs e)` | `public void RelayBe(BeEventArgs e)` | ✅ |
| `void RelayTrim(TrimEventArgs e)` | `public void RelayTrim(TrimEventArgs e)` | ✅ |
| `void RelayFlatten(FlatEventArgs e)` | `public void RelayFlatten(FlatEventArgs e)` | ✅ |
| `void RelayCancel(CancelEventArgs e)` | `public void RelayCancel(CancelEventArgs e)` | ✅ |

**Backing field verification** (live CopyEngine.cs grep confirmed):

| Member | Backing field | Live line | Correct |
|--------|--------------|-----------|---------|
| `IsEnabled` | `_isCopyEnabled` | line 98: `private volatile bool _isCopyEnabled;` | ✅ |
| `IsPendingSlotsEmpty` | `_pendingBeSlots.IsEmpty` | line 131: `private readonly ConcurrentDictionary<string, PendingBeSlot> _pendingBeSlots` | ✅ |
| `_cloneAtmCache` | `volatile string` | field declaration in ticket matches B50 spec | ✅ |
| `_globalBe` | `PttGlobalBreakEven` | B39 artifact confirms type | ✅ |

**Anchor text verification** (grep confirmed present in live CopyEngine.cs):

| Anchor (ticket) | Live line | Present |
|----------------|-----------|---------|
| `internal sealed class CopyEngine` | line 91 | ✅ |
| `private volatile int _copyModeValue` | line 103 | ✅ |
| closing `}` of `SetEnabled()` | line 273 | ✅ |
| closing `}` of `GetCopyMode()` | line 309 | ✅ |
| closing `}` of `GetAtmMode` static method | line 953–959 | ✅ |
| `var mode = GetAtmMode(rule, acc.Name);` | line 699 | ✅ |
| closing `}` of `DisarmPendingBe` | line 1653–1664 | ✅ |
| closing `}` of `private Position FindPosition(` | line 1424 | ✅ |
| `public List<CopyRuleDto> Rules { get; set; }` inside `CopyRulesContainer` | line 1813 | ✅ |
| `[Serializable]` already on `CopyRulesContainer` | line 1810 | ✅ (no duplicate needed) |
| `container.Rules.Add(RuleToDto(rule));` foreach closing before `var serializer` | lines 1930–1933 | ✅ |
| `_rules.Add(DtoToRule(dto));` foreach inside container null-check | lines 1972–1976 | ✅ |

**SnapshotTargetsPublic**: returns `List<Order>`, guards `acc == null || instr == null`, checks
`PTT-QX-T` and `PTT-TGT-` prefixes with `StringComparison.Ordinal`. Never returns null. ✅

**CopyRulesContainer.CopyEnabled**: `public bool CopyEnabled { get; set; } = false;` inside the
`private sealed class CopyRulesContainer` block. Uses `{ get; set; }` (not `init`) per NT8-001. ✅

**SaveRules/LoadRules wiring**: insertion points exactly match the live file structure at lines
1929–1933 (SaveRules) and lines 1972–1976 (LoadRules). Context code blocks in ticket match live
source verbatim. ✅

PASS.

---

### T4 — JS Rule Constraints Embedded

The ticket's "JS Rule Constraints (embedded)" table at its base contains all required rules
verbatim (not just document references):

| Rule | Present in ticket | Severity stated | Applies-To scope stated |
|------|------------------|----------------|------------------------|
| JS-021 lock() BANNED | ✅ | P0 | All 19 changes |
| JS-033 async void BANNED | ✅ | P0 | All 19 changes |
| JS-002 no return null | ✅ | P0 | Changes 15, 16 |
| JS-001 no throw new | ✅ | P0 | All 19 changes |
| JS-066 CYC ≤ 8 | ✅ | P1 | All new/modified methods |
| JS-023 volatile safe | ✅ | P1 | Changes 2, 4, 10, 11, 18, 19 |
| NT8-001 get; set; | ✅ | P1 | Change 17 |
| NT8-003 volatile double/float BANNED | ✅ | P1 | Change 2 |

Minimum required (JS-021, JS-033, JS-066) all present. PASS.

---

### T5 — CYC Pre-Check

| Member | Ticket CYC | Expected (plan §D SCAN-05) | ≤ 8 |
|--------|-----------|---------------------------|-----|
| `RelayBe` | 2 | 2 | ✅ |
| `RelayTrim` | 1 | 1 | ✅ |
| `RelayFlatten` | 1 | 1 | ✅ |
| `RelayCancel` | 1 | 1 | ✅ |
| `IsEnabled` property | 1 | 1 | ✅ |
| `GlobalBe` property getter | 2 | 2 | ✅ |
| `IsPendingSlotsEmpty` | 1 | 1 | ✅ |
| `SetCloneAtmCache` | 1 | 1 | ✅ |
| `GetCloneAtmMode` | 2 | 2 | ✅ |
| `ResolveAtmMode` | 2 | 2 | ✅ |
| `DispatchCopy` (modified) | 8 (AT LIMIT) | 8 | ✅ |
| `FindPositionPublic` | 1 | 1 | ✅ |
| `SnapshotTargetsPublic` | 3 | 3 | ✅ |
| `SaveRules` (modified) | unchanged | unchanged | ✅ |
| `LoadRules` (modified) | pre+1 | pre+1 | ✅ |

Ticket CYC values match plan exactly. Max is 3. PASS.

---

### T6 — NT8 Constraints

All NT8-specific checks pass:

- `Account.Orders` iteration: read-only NT8 enumeration, no lock required. Used throughout
  CopyEngine.cs already. ✅
- `OrderState.Working`: standard NT8 CBI enum, already in scope. ✅
- `Instrument`, `Position`: NT8 CBI types, already in scope. ✅
- `FollowerAtmMode`, `BeEventArgs`, `TrimEventArgs`, `FlatEventArgs`, `CancelEventArgs`:
  all defined in `PropTraderTools` namespace — `FollowerAtmMode` at CopyEngine.cs line 75,
  event args in PttContracts.cs. No new `using` directives required. ✅
- No `async/await` in any lifecycle or new method. ✅
- No `Account.All` call outside Loaded handler. ✅
- No `sealed` on any Window class. ✅
- No `FontFamily` set. ✅
- No hardcoded hex color. ✅
- No `DateTime.Now` (plan uses `DateTime.UtcNow` in pre-existing code). ✅
- `CopyEnabled { get; set; }` uses `{ set; }` (not `init`) per NT8-001. ✅
- `volatile string` (not `volatile double/float`) per NT8-003. ✅

PASS.

---

### T7 — Insertion Order

Field-before-method dependency chain verified:

| Dependency | Satisfied by | Notes |
|-----------|-------------|-------|
| `_cloneAtmCache` before `SetCloneAtmCache` / `GetCloneAtmMode` / `ResolveAtmMode` | Steps 2→10, 2→11, 2→12 | ✅ |
| `GetCloneAtmMode` before `ResolveAtmMode` (which calls it) | Steps 11→12 | ✅ |
| `IsEnabled` before `GlobalBe` (anchor chaining) | Steps 4→5 | ✅ |
| `RelayBe` before `RelayTrim` before `RelayFlatten` before `RelayCancel` | Steps 6→7→8→9 | ✅ |
| `RelayCancel` before `SetCloneAtmCache` (all relay methods grouped) | Steps 9→10 | ✅ |

Key anchor text confirmed in live CopyEngine.cs:
- `var mode = GetAtmMode(rule, acc.Name);` — **confirmed at line 699** ✅

`_globalBe = null` is a field initializer, not a constructor initialization. The private
constructor at line 264 is `private CopyEngine() { }` — empty. No constructor initialization
required for `_globalBe`. Lazy-init is deferred to the `GlobalBe` property getter. ✅

Ticket step table covers all 19 changes with correct anchor text for each. PASS.

---

### T8 — Definition of Done

The ticket's "Definition of Done (BUILD_PASS)" section contains:

- 19-item checkbox list (Changes 1–19), one line per change ✅
- 7 SCAN result checkboxes (SCAN-01 through SCAN-07), all explicitly required ✅
- "No regressions to existing CopyEngine behavior" ✅
- "`ticket-1-completion.md` written and committed with scan results" ✅

The DoD maps directly to what the engineer will report in `ticket-1-completion.md`. Each scan
is named and pass criteria stated in the checklist. PASS.

---

## Violations

None. Zero violations across all 8 criteria.

---

## Pre-Conditions for Engineer

Before executing Ticket-1, the engineer must observe the following:

1. **Apply all 19 changes in the exact sequence shown in the Insertion Order table.**
   Steps 1–3 are field-level insertions that all other method insertions depend on. Do not
   reorder.

2. **Changes 1 and 6–9 are a single atomic commit unit.** Adding `: ICopyEngine` (Change 1)
   without the 4 relay method bodies (Changes 6–9) will produce 4 CS0535 compile errors.
   Both the interface declaration and its 4 implementations must land in the same commit.

3. **Verify `[Serializable]` at line 1810 before inserting CopyEnabled (Change 17).**
   The live file already has `[Serializable]` on the line immediately above
   `private sealed class CopyRulesContainer`. Do NOT add a second `[Serializable]`.

4. **Change 13 (DispatchCopy) is a 1-line text substitution only.**
   Replace `var mode = GetAtmMode(rule, acc.Name);` with `var mode = ResolveAtmMode(rule, acc.Name);`.
   Do not touch any other line in the `DispatchCopy` method. DispatchCopy CYC is AT LIMIT=8;
   any new branch added would be a JS-066 violation.

5. **Line numbers are approximate; use anchor-text search, not line numbers.**
   B57 was committed after B54 and may have shifted line numbers. All anchors in the ticket
   are designed for text-search use.

6. **SCAN-07 must run AFTER the commit**, not before. The hard-link sync script reads the
   committed file state.

7. **Pre-existing [Fact] count is 278.** After applying all 19 changes, the count must remain
   278 — no new tests are added, none are removed.

---

*ptt-ticket-reviewer | Phase 3.5 | B58-LaneA | 2026-08-10*
