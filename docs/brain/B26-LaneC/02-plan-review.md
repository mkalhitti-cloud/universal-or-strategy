# B26 Lane C — Plan Review

**Epic**: B26-LaneC
**Phase**: 2 (Plan Review)
**Reviewer**: ptt-plan-reviewer
**Date**: 2026-07-17
**Verdict**: REVIEW_FAIL

---

## Violations Summary

| # | Rule | Severity | Location in Plan | Description |
|---|------|----------|-----------------|-------------|
| V1 | SPEC-DEV | P0 | Part A, Section A2 (UpdateBeVisuals NEW block) | Plan retains BorderBrush + BorderThickness in Armed/Connected cases. Spec L10202 mandates "Keep BorderBrush null; drop BorderThickness." Architecture Decision (spec L10247) locks: "BorderBrush approach dropped entirely." |
| V2 | SPEC-DEV | P0 | Part A, Section A2 (Idle case — "unchanged") | Plan omits the mandatory Idle Background reset. Spec L10206 (Fix step 3) requires: "Idle case in UpdateBeVisuals: reset Background to BrushInactive explicitly so returning to Idle always clears amber/blue." Plan states "Idle case: unchanged — no Background assignment." |
| V3 | SPEC-SCOPE | P1 | Part B, Section B1 deletion table | Plan deletes 3 extra methods (OnTrim L1278-1281, OnFlatten L1283-1286, OnCancel L1288-1291) not in the spec DEAD-B26 register. Spec lists only OnToggle and OnBreakEven as dead methods. This is unauthorized scope expansion. |
| V4 | SPEC-DEV | P1 | Part A, Section A3 CYC note | Plan reports UpdateButtonColors CYC 5→6. Spec L10207 (locked architecture) states "UpdateButtonColors CYC unchanged (guard replaces unconditional write — same branch count)." Plan contradicts locked spec value. |

---

## Per-Check Results

### R1 — DW-B26-03 Exact Code (UpdateBeVisuals + UpdateButtonColors)

**R1(a) — UpdateBeVisuals: Background set in Armed and Connected cases**
FAIL — V1, V2

The plan provides exact OLD/NEW code for Armed and Connected cases adding
`_beBtn2.Background = BrushCaution` and `_beBtn2.Background = BrushConnected`. That part is
correct and matches spec L10201-10202.

However:

**V1**: Plan NEW block retains BorderBrush and BorderThickness assignments in both cases:
```
// Plan NEW (Armed):
_beBtn2.Background      = BrushCaution;
_beBtn2.BorderBrush     = BrushCaution;      // ← retained by plan
_beBtn2.BorderThickness = new Thickness(2);  // ← retained by plan
```
Spec L10202 explicitly states: "Keep BorderBrush null; drop BorderThickness."
Spec Architecture Decision L10247 states: "BorderBrush approach dropped entirely."
Plan contradicts both.

**V2**: Plan states for the Idle case: "Idle case (L832-836): **unchanged** — no Background
assignment."
Spec L10206 mandates a third fix step: "Idle case in UpdateBeVisuals: reset Background to
`BrushInactive` explicitly so returning to Idle always clears amber/blue."
Plan explicitly omits this step.

**R1(b) — UpdateButtonColors L418: `_beState == BeState.Idle` guard**
PASS

Plan Section A3 provides exact OLD→NEW:
```
OLD: if (_beBtn2         != null) _beBtn2.Background         = hasPosition  ? BrushActive   : BrushInactive;
NEW: if (_beBtn2         != null && _beState == BeState.Idle) _beBtn2.Background = hasPosition  ? BrushActive   : BrushInactive;
```
This matches spec L10204-10205 exactly. No violation.

---

### R2 — CYC: UpdateButtonColors 5→6, is 6 ≤ 8?

FAIL — V4 (spec conflict; absolute threshold passes)

Plan claims UpdateButtonColors CYC 5→6 (+1 for the boolean predicate `&&`).
Spec locked architecture L10207 says: "UpdateButtonColors CYC unchanged (guard replaces
unconditional write — same branch count)."

The spec value is locked in the architecture decisions block (L10243-L10250). The plan must
reflect the locked spec value or explicitly resolve the conflict with a spec amendment. It does
neither. This is a plan-vs-spec contradiction.

Absolute threshold check: 6 ≤ 8. Would PASS if spec conflict were resolved.

---

### R3 — Dead Code Scope: 10 items with line numbers + zero-ref evidence

FAIL — V3

**Spec DEAD-B26 register (L10216-10233):**
- 5 dead field declarations: L121 `_copyToggleBtn`, L122 `_flattenBtn`, L123 `_cancelBtn`,
  L124 `_trimBtn`, L125 `_beBtn`
- 2 dead methods: L1270 `OnToggle`, L1293 `OnBreakEven`

**Spec Architecture Decision (L10248):** "Delete 4 dead field declarations (L121-124), 2 dead
methods (OnToggle, OnBreakEven). Retain `_beBtn` L125 deletion safe." This confirms L125 `_beBtn`
is also deleted → 5 fields + 2 methods = **7 authorized deletions**.

**Plan claims 10 items** — 5 fields + **5 methods**, adding:
- `OnTrim` L1278-1281 (not in spec DEAD-B26 register)
- `OnFlatten` L1283-1286 (not in spec DEAD-B26 register)
- `OnCancel` L1288-1291 (not in spec DEAD-B26 register)

These 3 methods are not listed in the spec's DEAD-B26 defect register. Deleting them is
unauthorized scope expansion violating the No Scope Creep Protocol (V12.23).

Note on evidence quality: For the 7 spec-authorized items, the plan provides line numbers and
zero-ref grep evidence in Section B1. That evidence is adequate for the authorized items. The
violation is scope, not evidence quality.

---

### R4 — KEEP List: `_beBufferBox` (L128) and `_statusText` (L126)

PASS

- `_beBufferBox` (L128): Plan Section B2 explicitly confirms KEEP, citing live reference at
  L1417 in `DispatchShortcut`. Matches spec L10226 and L10233.
- `_statusText` (L126): Plan Section B4 explicitly confirms KEEP ("multiple live references").

---

### R5 — [Fact] Count: 131 confirmed unchanged

PASS

Plan Section C: "Baseline [Fact] count: **131**. Count after Lane C: **131** (unchanged)."
Lane C owns zero new [Fact] tests. The 2 new tests in B26 (target 133) belong to Lanes A and B.
No [Fact] tests reference the deleted methods. Confirmed correct.

---

### R6 — NT8/JS P0 Rule Violations

PASS

Plan Section D provides a full rule table. All applicable rules verified:

| Rule | Plan Verdict | Reviewer Verdict |
|------|-------------|-----------------|
| JS-021 lock() | N/A — no lock introduced | PASS |
| JS-033 async void | N/A — no async touched | PASS |
| JS-001 throw in hot paths | N/A — no throws added | PASS |
| JS-002 return null | N/A — no new returns | PASS |
| NT8-001 init accessor | N/A — no new properties | PASS |
| NT8-002 records | N/A — no records | PASS |
| NT8-003 volatile double | N/A — no new volatile | PASS |
| NT8-004 ImmutableDictionary | N/A — not used | PASS |

Lane C is deletion + one-line guard + two property assignments. No new concurrency, no new types,
no new async code. Zero P0 violations.

---

### R7 — Spec Traceability: DW-B26-03 and DEAD-B26 by name

PASS

- DW-B26-03: Named in Summary, Part A title, Sections A1/A2/A3/A4, and Deferred Items.
- DEAD-B26: Named in Summary, Part B title, Section B1 table, and Deferred Items.

Both spec requirement IDs are explicitly referenced throughout the plan.

---

## Spec Coverage Matrix

| Spec Requirement | Addressed in Plan? | Plan Section | Status |
|------------------|--------------------|-------------|--------|
| DW-B26-03 background fix (UpdateBeVisuals Armed/Connected) | YES — partial | A2 | FAIL: BorderBrush/BorderThickness not dropped |
| DW-B26-03 Idle Background reset | NO | A2 (missing) | FAIL: step 3 omitted |
| DW-B26-03 UpdateButtonColors guard L418 | YES — exact | A3 | PASS |
| DEAD-B26 delete _copyToggleBtn L121 | YES | B1 | PASS |
| DEAD-B26 delete _flattenBtn L122 | YES | B1 | PASS |
| DEAD-B26 delete _cancelBtn L123 | YES | B1 | PASS |
| DEAD-B26 delete _trimBtn L124 | YES | B1 | PASS |
| DEAD-B26 delete _beBtn L125 | YES | B1 | PASS |
| DEAD-B26 delete OnToggle L1270-1276 | YES | B1 | PASS |
| DEAD-B26 delete OnBreakEven L1293-1300 | YES | B1 | PASS |
| DEAD-B26 retain _beBufferBox L128 | YES | B2 | PASS |
| DEAD-B26 retain _statusText L126 | YES | B4 | PASS |
| [Fact] count 131 unchanged (Lane C) | YES | C | PASS |
| No P0 NT8/JS violations | YES | D | PASS |

---

## Required Fixes Before Re-Review

The architect must address all 4 violations before resubmission:

**Fix V1** (UpdateBeVisuals Armed/Connected): Remove `_beBtn2.BorderBrush` and
`_beBtn2.BorderThickness` assignments from both Armed and Connected cases in the plan's NEW code
block. Spec locked: "BorderBrush approach dropped entirely."

**Fix V2** (UpdateBeVisuals Idle): Add the Idle case fix to the plan: set
`_beBtn2.Background = BrushInactive` (or `null`) explicitly in the Idle case to clear amber/blue
when returning to Idle. This is spec L10206 Fix step 3, mandatory.

**Fix V3** (DEAD-B26 scope): Remove OnTrim, OnFlatten, and OnCancel from the deletion table.
These 3 methods are not in the spec DEAD-B26 register and their deletion is not authorized.
If the architect believes they are dead, raise a separate deferred item (DW-B26 backlog) for
Director approval, do not include in Lane C scope.

**Fix V4** (CYC note): Align with the locked spec value. Either: (a) adopt spec's stated
"unchanged" and justify why the `&&` does not add a branch in this context, or (b) raise a
spec correction request to the Director before proceeding. The plan cannot silently contradict
a locked architecture decision.

---

REVIEW_FAIL

---

---

# SECOND PASS — B26 Lane C Plan Review (Revision 2)

**Reviewer**: ptt-plan-reviewer
**Plan Version**: Revision 2 (post REVIEW_FAIL — all 4 violations corrected)
**Review Date**: 2026-07-17
**Prior Verdict**: REVIEW_FAIL (V1, V2, V3, V4)

---

## Second Pass Violations Summary

| # | Rule | Severity | Status | Finding |
|---|------|----------|--------|---------|
| V1 | SPEC-DEV | P0 | **RESOLVED** | BorderBrush/BorderThickness absent from Armed and Connected cases in revised NEW block |
| V2 | SPEC-DEV | P0 | **RESOLVED** | Idle case now sets `_beBtn2.Background = BrushInactive`; old BorderBrush=null and BorderThickness lines removed |
| V3 | SPEC-SCOPE | P1 | **RESOLVED** | Deletion scope is exactly 7 items (5 fields + 2 methods); OnTrim/OnFlatten/OnCancel moved to Deferred Items as DW-B26-backlog-01 |
| V4 | SPEC-DEV | P1 | **RESOLVED** | CYC reported as unchanged: UpdateButtonColors=5, UpdateBeVisuals=3; justification provided and consistent with locked spec |

**New violations found**: 0

---

## Per-Check Results (Second Pass)

### R1 — V1 fixed: BorderBrush/BorderThickness absent from Armed and Connected

**PASS**

Revised NEW code block:
```csharp
case BeState.Armed:
    _beBtn2.Content    = "BE Armed";
    _beBtn2.Background = BrushCaution;     // amber
    break;                                 // BorderBrush/Thickness: NOT SET
case BeState.Connected:
    _beBtn2.Content    = "BE Live";
    _beBtn2.Background = BrushConnected;   // blue
    break;                                 // BorderBrush/Thickness: NOT SET
```

No `BorderBrush` assignment present in Armed or Connected. No `BorderThickness` assignment present
in Armed or Connected. Plan explicitly annotates "BorderBrush/Thickness: NOT SET" on both break
lines. Matches spec L10202 ("Keep BorderBrush null; drop BorderThickness") and Architecture
Decision L10247 ("BorderBrush approach dropped entirely").

---

### R2 — V2 fixed: Idle has `Background = BrushInactive`; old BorderBrush/BorderThickness lines gone

**PASS**

Revised Idle case:
```csharp
case BeState.Idle:
    _beBtn2.Content    = FormatBuffer("BE", _beBuffer);
    _beBtn2.Background = BrushInactive;    // explicit reset — clears amber/blue
    break;
```

`_beBtn2.Background = BrushInactive` is present. Plan confirms: "The old `BorderBrush = null`
and `BorderThickness = new Thickness(0)` lines in Idle are also removed." Final Idle case is
exactly 2 lines (Content + Background). Matches spec L10206 Fix step 3.

---

### R3 — V3 fixed: Deletion scope exactly 7 items; OnTrim/OnFlatten/OnCancel absent

**PASS**

Section B1 authorized deletion table (7 items):

| # | Item | Lines |
|---|------|-------|
| 1 | `_copyToggleBtn` | L121 |
| 2 | `_flattenBtn` | L122 |
| 3 | `_cancelBtn` | L123 |
| 4 | `_trimBtn` | L124 |
| 5 | `_beBtn` | L125 |
| 6 | `OnToggle` | L1270-1276 |
| 7 | `OnBreakEven` | L1293-1300 |

Count = 7. Matches spec DEAD-B26 register exactly (5 fields L121-125 + 2 methods).

Section B3 explicitly states: "OnTrim / OnFlatten / OnCancel are out of scope for Lane C. Do not
delete." Deferred Items section raises them as `DW-B26-backlog-01` for Director review. No scope
creep in Lane C.

---

### R4 — V4 fixed: CYC unchanged for both methods

**PASS**

Section A3 (labelled "V4 fix applied"):
- `UpdateButtonColors`: CYC = **5** (unchanged). Justification: "The guard `&& _beState ==
  BeState.Idle` adds a boolean condition to an existing `if` statement — it replaces the
  unconditional assignment with a conditional one. It does not introduce a new branch in the
  logical control flow count. Same 5 branches as before."
- `UpdateBeVisuals`: CYC = **3** (unchanged). Justification: "The 3-case switch shape is
  identical; only the property assignments inside each case change. No new cases, no new guards."

Both values consistent with locked spec Architecture Decision L10247/L10207. Both values ≤ 8.

---

### R5 — Brush definitions present with line numbers

**PASS**

Section A4:
| Brush | Line | Color |
|-------|------|-------|
| `BrushConnected` | L181 | blue `#3b82f6` — `MakeBrush(59, 130, 246)` |
| `BrushCaution` | L195 | amber `#f59e0b` — `MakeBrush(245, 158, 11)` |
| `BrushInactive` | existing | grey |

Both primary brushes cited by line number. Both confirmed frozen via `MakeBrush` (`.Freeze()`).
No new brush definitions required. No JS-008 violation (SolidColorBrush not Freeze()d).

---

### R6 — KEEP list: `_beBufferBox` L128 and `_statusText` L126 confirmed

**PASS**

- Section B2: `_beBufferBox` L128 — LIVE (referenced at L1417 in `DispatchShortcut`, active
  `Key.B` handler). Must not be deleted. Matches spec L10226 and L10233.
- Section B4: `_statusText` L126 — LIVE (multiple live references throughout file). Must not
  be deleted.

---

### R7 — [Fact] count 131 unchanged

**PASS**

Section C: "Baseline [Fact] count: 131. Count after Lane C: 131 (unchanged)."
Lane C owns zero new [Fact] tests. The 2 new B26 tests (Lane A and Lane B) are in separate lanes
targeting `CopyEngine.cs`. No [Fact] tests reference any of the 7 deleted items (confirmed in
plan).

---

### R8 — NT8/JS P0 checklist: all PASS

**PASS**

Section D full compliance table:

| Rule | Status | Evidence |
|------|--------|---------|
| JS-021 `lock()` | PASS | No concurrency changes; no lock introduced |
| JS-033 `async void` | PASS | No async methods added or touched |
| JS-001 `throw` in hot paths | PASS | No throws added |
| JS-002 `return null` | PASS | No new return values |
| NT8-001 `{ get; init; }` | PASS | No new properties |
| NT8-002 `abstract/sealed record` | PASS | No records |
| NT8-003 `volatile double` | PASS | No new volatile fields |
| NT8-004 `ImmutableDictionary` | PASS | Not used |
| NT8-007 `CreateOrder` | PASS | Not used |

Change set is deletion + one-line guard + two property-assignment replacements only. Zero new
concurrency, zero new types, zero new async code. No P0 or P1 violations.

---

## Second Pass Spec Coverage Matrix

| Spec Requirement | Addressed? | Plan Section | Status |
|------------------|-----------|-------------|--------|
| DW-B26-03: UpdateBeVisuals Armed — Background=BrushCaution, no BorderBrush/Thickness | YES | A2 | PASS |
| DW-B26-03: UpdateBeVisuals Connected — Background=BrushConnected, no BorderBrush/Thickness | YES | A2 | PASS |
| DW-B26-03: UpdateBeVisuals Idle — Background=BrushInactive (explicit reset) | YES | A2 | PASS |
| DW-B26-03: UpdateButtonColors L418 — `_beState == BeState.Idle` guard | YES | A2 | PASS |
| DEAD-B26: delete `_copyToggleBtn` L121 | YES | B1 | PASS |
| DEAD-B26: delete `_flattenBtn` L122 | YES | B1 | PASS |
| DEAD-B26: delete `_cancelBtn` L123 | YES | B1 | PASS |
| DEAD-B26: delete `_trimBtn` L124 | YES | B1 | PASS |
| DEAD-B26: delete `_beBtn` L125 | YES | B1 | PASS |
| DEAD-B26: delete `OnToggle` L1270-1276 | YES | B1 | PASS |
| DEAD-B26: delete `OnBreakEven` L1293-1300 | YES | B1 | PASS |
| DEAD-B26: retain `_beBufferBox` L128 | YES | B2 | PASS |
| DEAD-B26: retain `_statusText` L126 | YES | B4 | PASS |
| CYC ≤ 8 for all touched methods | YES | A3 | PASS (5, 3) |
| [Fact] count 131 unchanged | YES | C | PASS |
| No P0 NT8/JS violations | YES | D | PASS |
| OnTrim/OnFlatten/OnCancel out of scope | YES | B3 + Deferred | PASS |

All 17 requirements: PASS.

---

REVIEW_PASS
