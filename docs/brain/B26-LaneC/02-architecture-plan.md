# B26 Lane C — Architecture Plan (Revision 2)

**Epic**: B26-LaneC
**Phase**: 2 (Architecture)
**Author**: ptt-architect
**Revision**: 2 (post REVIEW_FAIL — all 4 violations corrected)
**Status**: PLAN_COMPLETE (pending reviewer sign-off)
**Spec**: specs/002-trade-copier-spec.html L10190-10252
**File in scope**: `src/PropTraderTools/TradeCopierPanel.cs` (only)

---

## Summary

Lane C closes two spec defects in `TradeCopierPanel.cs`:

| Ticket | Spec ID    | Description                                                      |
|--------|------------|------------------------------------------------------------------|
| T1     | DW-B26-03  | BE Armed/Connected states invisible — fix `UpdateBeVisuals` + guard `UpdateButtonColors` |
| T2     | DEAD-B26   | Delete 5 dead field declarations + 2 dead methods (7 items total) |

Lane C is independent of Lanes A and B (those touch `CopyEngine.cs`). No cross-file changes. No new types, no new async code, no new concurrency.

---

## Part A — DW-B26-03: BE State Visual Fix

### Problem (spec L10194-10199)

`UpdateBeVisuals(Armed)` sets `_beBtn2.BorderBrush = BrushCaution` (amber) and `BorderThickness = 2`. However, `UpdateButtonColors` at L418 runs on every position-state tick and unconditionally writes `_beBtn2.Background = hasPosition ? BrushActive : BrushInactive`, overwriting the background. The NT8 WPF button template renders `Background` over the interior — `BorderBrush`-only changes are invisible. Result: Armed and Connected states are visually indistinguishable from Idle.

### Fix (spec L10200-10207, Architecture Decision L10247)

Three coordinated changes:

#### A1. `UpdateBeVisuals` — complete new switch body

**Architecture Decision (spec L10247):** "Background is the primary visual signal — BorderBrush approach dropped entirely."

`UpdateBeVisuals` is at `TradeCopierPanel.cs` L826-848 (CYC=3, 3-case switch).

**Current code (L832-848):**
```csharp
case BeState.Idle:                                                    // (1)
    _beBtn2.Content         = FormatBuffer("BE", _beBuffer);
    _beBtn2.BorderBrush     = null;
    _beBtn2.BorderThickness = new Thickness(0);
    break;
case BeState.Armed:                                                   // (2)
    _beBtn2.Content         = "BE Armed";
    _beBtn2.BorderBrush     = BrushCaution;
    _beBtn2.BorderThickness = new Thickness(2);
    break;
case BeState.Connected:                                               // (3)
    _beBtn2.Content         = "BE Live";
    _beBtn2.BorderBrush     = BrushConnected;
    _beBtn2.BorderThickness = new Thickness(2);
    break;
```

**New code (all 3 cases, no BorderBrush or BorderThickness anywhere):**
```csharp
case BeState.Idle:                                                    // (1)
    _beBtn2.Content    = FormatBuffer("BE", _beBuffer);
    _beBtn2.Background = BrushInactive;               // explicit reset — clears amber/blue
    break;
case BeState.Armed:                                                   // (2)
    _beBtn2.Content    = "BE Armed";
    _beBtn2.Background = BrushCaution;                // amber
    break;                                            // BorderBrush/Thickness: NOT SET
case BeState.Connected:                               // (3)
    _beBtn2.Content    = "BE Live";
    _beBtn2.Background = BrushConnected;              // blue
    break;                                            // BorderBrush/Thickness: NOT SET
```

**V1 fix applied:** `BorderBrush` and `BorderThickness` assignments removed from Armed and Connected cases entirely — spec L10202 "Keep BorderBrush null; drop BorderThickness"; Architecture Decision L10247 "BorderBrush approach dropped entirely."

**V2 fix applied:** Idle case now explicitly sets `_beBtn2.Background = BrushInactive` — spec L10206 Fix step 3: "reset Background to BrushInactive explicitly so returning to Idle always clears amber/blue." The old `BorderBrush = null` and `BorderThickness = new Thickness(0)` lines in Idle are also removed (artefacts of the old approach). Final Idle case: exactly 2 lines (`Content` + `Background`).

#### A2. `UpdateButtonColors` L418 — add `_beState == BeState.Idle` guard

`UpdateButtonColors` is at `TradeCopierPanel.cs` L412-419 (CYC=5).

**Current L418 (spec L10204, marked OLD):**
```csharp
if (_beBtn2 != null) _beBtn2.Background = hasPosition ? BrushActive : BrushInactive;
```

**New L418 (spec L10205, marked NEW):**
```csharp
if (_beBtn2 != null && _beState == BeState.Idle) _beBtn2.Background = hasPosition ? BrushActive : BrushInactive;
```

This prevents `UpdateButtonColors`'s position-tick write from overwriting the amber/blue `Background` set by `UpdateBeVisuals` when state is Armed or Connected. When Idle, the position-aware green/grey from `UpdateButtonColors` still applies.

#### A3. CYC Analysis (V4 fix applied)

Both methods: **CYC UNCHANGED** — per spec Architecture Decision L10247 and spec L10207.

- **`UpdateButtonColors`**: CYC stays at **5**. The guard `&& _beState == BeState.Idle` adds a boolean condition to an existing `if` statement — it replaces the unconditional assignment with a conditional one. It does not introduce a new branch in the logical control flow count. Same 5 branches as before.
- **`UpdateBeVisuals`**: CYC stays at **3**. The 3-case switch shape is identical; only the property assignments inside each case change. No new cases, no new guards.

#### A4. Brush Definitions (existing — no new brushes required)

| Brush         | Line | Definition                                              | Color  |
|---------------|------|---------------------------------------------------------|--------|
| `BrushConnected` | L181 | `static readonly SolidColorBrush MakeBrush(59, 130, 246)` | blue `#3b82f6` |
| `BrushCaution`   | L195 | `static readonly SolidColorBrush MakeBrush(245, 158, 11)` | amber `#f59e0b` |
| `BrushInactive`  | existing | already defined | grey |

Both brushes are frozen via `MakeBrush` (calls `.Freeze()`). No new brush definitions needed.

---

## Part B — DEAD-B26: Delete 7 Dead Items

### Problem (spec L10216-10233)

B12 restructured the panel to buffered V2 buttons (`_beBtn2`, `_trimBtn2`, etc.) but left V1 field declarations intact. None are assigned after construction. Two V1 event handlers remain wired to nothing.

### B1. Authorized Deletion Table — exactly 7 items (V3 fix applied)

**Spec DEAD-B26 register (L10220-10233) + Architecture Decision (L10248) authorizes exactly:**

| # | Item             | Lines       | Evidence                                                          |
|---|------------------|-------------|-------------------------------------------------------------------|
| 1 | `_copyToggleBtn` | L121        | Never assigned; only referenced inside `OnToggle` (dead method). Comment at L472 confirms "old 4-column actionGrid and `_copyToggleBtn` removed." |
| 2 | `_flattenBtn`    | L122        | Declaration only; referenced only in dead `OnFlatten` (retained per spec — not a deletion target). |
| 3 | `_cancelBtn`     | L123        | Declaration only; referenced only in dead `OnCancel` (V1 version, retained per spec). |
| 4 | `_trimBtn`       | L124        | Declaration only; referenced only in dead `OnTrim` (retained per spec). |
| 5 | `_beBtn`         | L125        | Declaration only; zero references anywhere. Architecture Decision L10248: "Retain `_beBtn` L125 deletion safe (zero references)." |
| 6 | `OnToggle`       | L1270-1276  | V1 toggle handler. Updates dead `_copyToggleBtn`. Not wired to any button after B12 (V2 uses `OnCopyToggle` at L713). Zero `Click+=` wiring. |
| 7 | `OnBreakEven`    | L1293-1300  | V1 BE handler. Reads `_beBufferBox` but not wired to any button after B12 (V2 uses `OnBeClick`). Zero `Click+=` wiring. |

**V3 fix applied:** `OnTrim` (L1278-1281), `OnFlatten` (L1283-1286), and `OnCancel` (L1288-1291) are **NOT** in this table. They are not listed in the spec DEAD-B26 register. Their deletion is not authorized by DEAD-B26. They are out of scope for Lane C per the No Scope Creep Protocol (V12.23). If dead, they require a separate Director-approved deferred item.

### B2. Retain: `_beBufferBox` (L128)

`private TextBox _beBufferBox` at L128 is **LIVE** — referenced in `DispatchShortcut` at L1417 (active `Key.B` shortcut handler). Must not be deleted. Spec L10226 and L10233 both explicitly exclude it.

### B3. Note on `OnTrim` / `OnFlatten` / `OnCancel`

These methods are **out of scope for Lane C**. They are not listed in spec DEAD-B26. Do not delete. Do not reference as deletion targets. If the engineer believes they are dead, report to Director for a separate authorization.

### B4. Retain: `_statusText` (L126)

`private TextBlock _statusText` at L126 has multiple live references throughout the file. Must not be deleted.

---

## Part C — [Fact] Test Count

**Baseline [Fact] count: 131**
**Count after Lane C: 131 (unchanged)**

Lane C owns zero new [Fact] tests. The 2 new tests for B26 (`T_B26_01_TrailBe_WithNoRule_StillMovesStop` and `T_B26_02_PendingBeFired_CarriesAccountName`) belong to Lanes A and B respectively — they test `CopyEngine.cs` logic, not `TradeCopierPanel.cs` UI state. No [Fact] tests reference the 7 deleted items (confirmed: none of the deleted fields/methods are called from any test file).

---

## Part D — NT8 / JS Rule Compliance

All applicable rules verified for the Lane C change set (deletion + one-line guard + two property-assignment replacements):

| Rule    | Status | Justification                                                        |
|---------|--------|----------------------------------------------------------------------|
| JS-021  | PASS   | No `lock()` introduced. No concurrency changes.                      |
| JS-033  | PASS   | No `async void` method added or touched.                             |
| JS-001  | PASS   | No `throw new XxxException` in hot paths.                            |
| JS-002  | PASS   | No `return null` for missing values.                                 |
| NT8-001 | PASS   | No new `{ get; init; }` accessors.                                   |
| NT8-002 | PASS   | No `abstract record` or `sealed record`.                             |
| NT8-003 | PASS   | No new `volatile` fields.                                            |
| NT8-004 | PASS   | No `ImmutableDictionary` or `System.Collections.Immutable` usage.   |
| NT8-007 | PASS   | No `CreateOrder` calls.                                              |

**Gate result: ALL PASS — no P0 or P1 violations.**

---

## Component & File Summary

| File                                    | Change Type        | Lines Changed |
|-----------------------------------------|--------------------|---------------|
| `src/PropTraderTools/TradeCopierPanel.cs` | Edit + Delete     | ~15 lines net |

No new files. No new classes. No new interfaces. No cross-file changes.

---

## Deferred Items

- **OnTrim / OnFlatten / OnCancel** (L1278-1291): Potentially dead V1 handlers. Not authorized by DEAD-B26. Raise as `DW-B26-backlog-01` for Director review before any future deletion.

---

## Revision History

| Revision | Date       | Change                                                                 |
|----------|------------|------------------------------------------------------------------------|
| 1        | 2026-07-17 | Initial plan — REVIEW_FAIL (V1 BorderBrush retained, V2 Idle missing, V3 scope exceeded, V4 CYC wrong) |
| 2        | 2026-07-17 | V1: removed BorderBrush/BorderThickness from Armed+Connected. V2: added Idle Background reset + removed old BorderBrush null lines. V3: removed OnTrim/OnFlatten/OnCancel from deletion scope. V4: CYC unchanged for both methods per locked spec. |
