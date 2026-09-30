# B47-LaneB -- Plan Review
**Reviewer**: ptt-plan-reviewer (Phase 2)
**Block**: PTT-COPIER-B47 -- Panel UX Redesign
**Plan reviewed**: `docs/brain/B47-LaneB/02-architecture-plan.md`
**Date**: 2026-08-07
**Result**: **REVIEW_FAIL**

---

## VIOLATIONS

### VIOLATION-1 (P0 -- SPEC COMPLETENESS)
| Field | Value |
|-------|-------|
| Rule | SPEC_COMPLETENESS |
| Severity | P0 -- auto-FAIL |
| Ticket | T1-B (DW-B47-INLINE-FOLLOWERS-02) |
| Plan location | §4.3 `BuildInlineFollowerRow()` |
| Description | **P&L TextBlock column is missing from the inline follower row design.** |

**Spec requirement** (DW-B47-INLINE-FOLLOWERS-02):
> Each row: **[CheckBox] [account name TextBlock] [P&L TextBlock] [ATM template ComboBox]**

**Plan implementation** (§4.3 `BuildInlineFollowerRow`):
Row contains only **3 columns**: `[CheckBox][account TextBlock][ATM ComboBox]`.
The `[P&L TextBlock]` column is entirely absent from the plan. No field, no binding, no
placeholder, no deferred-item entry covers this omission.

**Evidence**: §4.3 body constructs `chk` (CheckBox), `nameLabel` (TextBlock), `atmCombo`
(ComboBox) and adds them to `row.Children` in that order. There is no P&L TextBlock anywhere
in §4.3, §4.4, §4.5, §4.6, or §14 (scope exclusions).

**Action required**: Architect must add a P&L TextBlock (Col 2) to `BuildInlineFollowerRow()`.
Source: `Account.UnrealizedPnL` or `Account.GetAccountItem(AccountItem.UnrealizedPnL).Value`.
Update §4.3 code, §4.6 CYC table (still CYC=1 -- straight-line adds), and §12 AC-matrix.

---

### VIOLATION-2 (P0 -- SPEC COMPLETENESS)
| Field | Value |
|-------|-------|
| Rule | SPEC_COMPLETENESS |
| Severity | P0 -- auto-FAIL |
| Ticket | T6-B (DW-B47-PANEL-ORDER-04) cross-cutting T1-B |
| Plan location | §4.4 vs §6.2 vs §9.4 |
| Description | **`_followerScrollViewer` is added to root.Children twice** -- in §4.4 T1-B BuildUI snippet AND in §6.2 `BuildCopierSection()`. WPF throws at runtime when the same UIElement is assigned a second parent. |

**Evidence**:

*§4.4 "After" BuildUI() code snippet* (T1-B):
```csharp
root.Children.Add(_followerScrollViewer);
```

*§6.2 `BuildCopierSection(StackPanel root)` body*:
```csharp
root.Children.Add(_copierCollapseBtn);
root.Children.Add(_followerScrollViewer);  // already constructed in BuildUI()
```

*§9.4 end-of-BuildUI()* (T6-B canonical order -- the correct design):
```csharp
BuildCopierSection(root);   // adds _copierCollapseBtn + _followerScrollViewer
```

There is no statement in the plan that T6-B **removes** the standalone
`root.Children.Add(_followerScrollViewer)` from the T1-B BuildUI() snippet. An engineer
following the plan ticket-by-ticket (T7-B → T1-B → T4-B → T3-B → T2-B → T5-B → T6-B per §11)
will implement T1-B first (adding `_followerScrollViewer` to root directly), then implement T6-B
which calls `BuildCopierSection` -- resulting in two `root.Children.Add(_followerScrollViewer)`
calls, causing a WPF `InvalidOperationException: "Element is already the child of another element"`.

**Action required**: Architect must explicitly state in §4.4 (T1-B) that the standalone
`root.Children.Add(_followerScrollViewer)` call is **omitted** from the T1-B implementation --
the ScrollViewer is added to root exclusively through `BuildCopierSection(root)` called from
T6-B. Alternatively, add a "T1-B implementation note: do NOT call root.Children.Add on
_followerScrollViewer in BuildUI(); that call is deferred to T6-B via BuildCopierSection."

---

## SPEC COVERAGE MATRIX

| Req ID | Requirement | Addressed? | Plan section |
|--------|-------------|-----------|-------------|
| T1-B | Replace ComboBox with ScrollViewer+StackPanel | ✅ YES | §4.4 |
| T1-B | Each row: [CheckBox][account name][P&L][ATM ComboBox] | ❌ **NO -- P&L missing** | §4.3 (VIOLATION-1) |
| T1-B | MaxHeight=66px | ✅ YES | §4.4 |
| T1-B | VerticalScrollBarVisibility=Auto | ✅ YES | §4.4 |
| T1-B | ATM ComboBox IsEnabled=false when unchecked | ✅ YES | §4.3 lambdas |
| T1-B | Apply button Visibility.Collapsed (hidden, not deleted) | ✅ YES | §4.4 |
| T1-B | Populate from NT8 AtmStrategy xml directory | ✅ YES | §4.3 OnFollowerAtmTemplateComboLoaded |
| T1-B | Checked rows float to top (T4-B sort) | ✅ YES | §4.3 lambdas call SortFollowerRows() |
| T2-B | TryAutoApply() CYC≤3 | ✅ YES (CYC=3) | §5.2, §5.6 |
| T2-B | Wire from OnFollowerChecked | ✅ YES | §4.3 chk.Checked lambda |
| T2-B | Wire from OnFollowerAtmTemplateComboChanged | ✅ YES | §5.5 |
| T2-B | Apply button remains hidden (wired to OnApplyRule) | ✅ YES | §4.4 |
| T2-B | BuildAtmMap helper extracted | ✅ YES | §5.3 |
| T2-B | BuildMultipliers helper extracted | ✅ YES | §5.4 |
| T3-B | "▼ Copier"/"▶ Copier (N active)" header | ✅ YES | §6.4 UpdateCopierHeader() |
| T3-B | N = checked count | ✅ YES | §6.4 → §6.5 CountActiveFollowers() |
| T3-B | Toggle ScrollViewer Visibility | ✅ YES | §6.3 OnCopierCollapseClick() |
| T3-B | Default: Expanded | ✅ YES | §2 `_copierCollapsed = false` |
| T4-B | SortFollowerRows() CYC≤8 | ✅ YES (CYC=3) | §7.2, §7.3 |
| T4-B | Checked first, alpha secondary | ✅ YES | §7.2 Sort lambda |
| T4-B | Called from OnFollowerChecked | ✅ YES | §4.3 chk.Checked/Unchecked lambdas |
| T4-B | Called from initial load | ✅ YES | §4.2 LoadFollowers() end |
| T4-B | Private method | ✅ YES | §7.2 `private void SortFollowerRows()` |
| T5-B | Trim button + spinner hidden (Visibility.Collapsed) | ✅ YES | §8.2 row1 Visibility.Collapsed |
| T5-B | Flatten button + spinner hidden | ✅ YES | §8.2 row1 Visibility.Collapsed |
| T5-B | Cancel button hidden | ✅ YES | §8.4 ClickTrader row hidden (contains Cancel) |
| T5-B | Tighten row (entire) hidden | ✅ YES | §8.4 tightenRow.Visibility = Collapsed |
| T5-B | Click Trader row (entire) hidden | ✅ YES | §8.4 clickTraderRow hidden |
| T5-B | Event handlers PRESERVED (not deleted) | ✅ YES | §8.2 "HIDE NOT DELETE" comment; wired handlers remain |
| T5-B | Row1=[BE▲▼][BE ALL▲▼] (2-col 50/50) | ✅ YES | §8.1 _beRowPanel UniformGrid 2-col |
| T5-B | Row2=[Quick▲▼][Quick ALL▲▼] (2-col 50/50) | ✅ YES | §8.1 _quickRowPanel UniformGrid 2-col |
| T5-B | Quick ALL gets ▲▼ spinner | ✅ YES | §8.2 quickAllCluster DockPanel + RepeatButtons |
| T6-B | Final order: COPY ON+Mode at top | ✅ YES | §9.3 step 2 BuildModeRow(root) |
| T6-B | ▼ Position Tools above Copier | ✅ YES | §9.3 steps 3-6 |
| T6-B | ▼ Copier section below Position Tools | ✅ YES | §9.3 step 7 |
| T6-B | Status bar below Copier | ✅ YES | §9.3 step 8 |
| T6-B | BE/BE ALL below status | ✅ YES | §9.3 step 9 |
| T6-B | Quick/Quick ALL at bottom | ✅ YES | §9.3 step 10 |
| T6-B | _followerScrollViewer double-add resolved | ❌ **NO -- ambiguous** | §4.4 vs §6.2 (VIOLATION-2) |

---

## JANE STREET P0 RULES CHECK

| Rule | Check | Result |
|------|-------|--------|
| JS-021 (no lock()) | All new methods UI-thread-only. No `lock(` in any code snippet. | ✅ PASS |
| JS-001 (no throw in hot path) | TryAutoApply/BuildAtmMap/BuildMultipliers use `return;` not throw. | ✅ PASS |
| JS-002 (no return null) | BuildAtmMap returns init'd Dictionary. BuildMultipliers returns init'd int[]. TryAutoApply is void. | ✅ PASS |
| JS-033 (no async void) | All handlers are synchronous `private void`. No `async void` anywhere in new code. | ✅ PASS |

---

## NT8 P0 RULES CHECK

| Rule | Check | Result |
|------|-------|--------|
| NT8-001 (no init setter) | No `{ get; init; }` in new fields or properties. | ✅ PASS |
| NT8-003 (no volatile double) | New fields: ScrollViewer, StackPanel, Button, bool, UniformGrid, int -- none volatile double. | ✅ PASS |
| NT8-019 (no async void) | No async void in any new method. | ✅ PASS |
| NT8-042 (no Dispatcher.InvokeAsync) | No Dispatcher calls added in B47. All new methods on UI thread. | ✅ PASS |
| NT8-043 (no null-conditional compound assignment) | No `?.` compound-assignment patterns in new code. | ✅ PASS |
| NT8-044 (StringComparison requires using System) | `SortFollowerRows` uses `StringComparison.OrdinalIgnoreCase`. Plan §16 confirms `using System;` at file line 2. | ✅ PASS |
| NT8-004 (ImmutableDictionary banned) | BuildAtmMap returns `Dictionary<string, FollowerAtmMode>` (mutable). | ✅ PASS |

---

## CYC VERIFICATION

| Method | CYC | Limit | Result |
|--------|-----|-------|--------|
| LoadFollowers() | 2 | ≤ 8 | ✅ PASS |
| BuildInlineFollowerRow() | 1 | ≤ 8 | ✅ PASS |
| BuildUI() | 1 | ≤ 8 | ✅ PASS |
| OnLoaded() | 5 | ≤ 8 | ✅ PASS |
| TryAutoApply() | 3 | ≤ 3 (spec) | ✅ PASS |
| BuildAtmMap() | 2 | ≤ 8 | ✅ PASS |
| BuildMultipliers() | 3 | ≤ 8 | ✅ PASS |
| BuildCopierSection() | 1 | ≤ 8 | ✅ PASS |
| OnCopierCollapseClick() | 2 | ≤ 8 | ✅ PASS |
| UpdateCopierHeader() | 2 | ≤ 8 | ✅ PASS |
| CountActiveFollowers() | 1 | ≤ 8 | ✅ PASS |
| SortFollowerRows() | 3 | ≤ 8 (spec) | ✅ PASS |
| Sort comparison lambda | 3 | ≤ 8 | ✅ PASS |
| BuildBufferedButtonsRow() | 1 | ≤ 8 | ✅ PASS |
| OnQuickAllUp() | 1 | ≤ 8 | ✅ PASS |
| OnQuickAllDown() | 1 | ≤ 8 | ✅ PASS |

---

## VERDICT

**REVIEW_FAIL**

Two P0 violations prevent REVIEW_PASS:

| # | ID | Rule | Description |
|---|----|------|-------------|
| 1 | VIOLATION-1 | SPEC_COMPLETENESS | T1-B row missing `[P&L TextBlock]` column (§4.3 BuildInlineFollowerRow has 3 cols; spec requires 4) |
| 2 | VIOLATION-2 | SPEC_COMPLETENESS | `_followerScrollViewer` double-add ambiguity: §4.4 T1-B snippet adds it to root; §6.2 BuildCopierSection adds it again. Plan does not reconcile. Will crash WPF at runtime. |

**Required fixes before REVIEW_PASS**:
1. Add P&L TextBlock as Col 2 in `BuildInlineFollowerRow()` (§4.3). Update §4.6 CYC (still 1), §12 AC matrix.
2. Remove or explicitly annotate the `root.Children.Add(_followerScrollViewer)` call in §4.4's T1-B code snippet. State that T6-B is the sole point where `_followerScrollViewer` enters the visual tree (via `BuildCopierSection`). Update §9.3 description accordingly.

All other checks (JS P0, NT8 P0, CYC, T2-B through T5-B coverage, T6-B ordering) PASS.

---
*Reviewed by ptt-plan-reviewer. Cycle 1 of 2 allowed.*

---

## CYCLE 2 REVIEW — 2026-08-07

**Reviewer**: ptt-plan-reviewer (Phase 2, Cycle 2)
**Plan version reviewed**: Cycle 2 re-arch (Status: PLAN_COMPLETE — Cycle 2)
**Violations re-checked**: VIOLATION-1 (P&L column), VIOLATION-2 (_followerScrollViewer double-add)

---

### VIOLATION-1 RESOLUTION CHECK

**Claimed fix**: P&L TextBlock added as Col 2 in `BuildInlineFollowerRow()`.

**Evidence in revised plan §4.3**:
- Col 2 `pnlLabel` TextBlock explicitly defined with `Text = item.DailyPnlText`, `Width = 64`, `Foreground = item.DailyPnlColor`.
- `row.Children.Add(pnlLabel)` appears between `row.Children.Add(nameLabel)` and `row.Children.Add(atmCombo)`.
- Comment on that line: `// Col 2: P&L -- added between name and ATM ComboBox`.
- §12 AC-1 updated: "each row shows [CheckBox][account name][P&L TextBlock][ATM ComboBox]".
- §4.6 CYC table: `BuildInlineFollowerRow()` remains CYC=1 (straight-line; no new branches from adding pnlLabel).

**Result**: **FIXED ✓** — 4-column row now fully specified per spec.

---

### VIOLATION-2 RESOLUTION CHECK

**Claimed fix**: `_followerScrollViewer` double-add eliminated. T1-B constructs only; visual tree insertion via T6-B `BuildCopierSection` exclusively.

**Evidence in revised plan §4.4**:
```
// *** T1-B IMPLEMENTATION NOTE -- DO NOT ADD _followerScrollViewer TO root HERE ***
// _followerScrollViewer enters the visual tree ONLY via BuildCopierSection(root) called
// from T6-B's rebuilt BuildUI(). Adding root.Children.Add(_followerScrollViewer) here
// would cause a WPF InvalidOperationException ("Element is already the child of another
// element") when T6-B subsequently calls BuildCopierSection which adds it a second time.
// T1-B scope: construct + populate only. Visual tree insertion: T6-B exclusively.
```

The standalone `root.Children.Add(_followerScrollViewer)` line that was present in the Cycle 1 plan §4.4 is **absent** from the Cycle 2 plan. The `applyBtn` (hidden) is still added to `root.Children` (preserving `OnApplyRule` wiring), but `_followerScrollViewer` itself is NOT added in T1-B scope.

**§6.2 `BuildCopierSection`**: `root.Children.Add(_followerScrollViewer)` — **sole** insertion point.

**§9.4 canonical BuildUI() tail**:
```csharp
root.Children.Add(_contentPanel);
BuildCopierSection(root);   // adds copier header + _followerScrollViewer
root.Children.Add(_statusText);
root.Children.Add(_beRowPanel);
root.Children.Add(_quickRowPanel);
Content = root;
```

Execution order is unambiguous. No double-add path exists.

**Result**: **FIXED ✓** — Single visual tree insertion path confirmed.

---

### DOCUMENTATION NOTE (non-blocking)

§9.3 "After" list item 1 reads: `_followerScrollViewer (T1-B; applyBtn hidden just above it)`.
This is a carry-over artefact from the "Before" description. The **`_followerScrollViewer`** in the
After layout is correctly placed at step 7 (inside `BuildCopierSection`), not at step 1. The `applyBtn`
(hidden) does appear early (before ModeRow) because it is added in T1-B's BuildUI fragment, which is
consistent with spec ("preserved but hidden"). Only the `_followerScrollViewer` label in item 1 is
stale. This is a documentation inconsistency, **not a code violation**. §9.4's concrete code is
authoritative and correct. No FAIL trigger.

---

### FULL SPEC COVERAGE RE-VERIFICATION (Cycle 2)

| Req | Requirement | Addressed? | Plan section |
|-----|-------------|-----------|-------------|
| T1-B | ScrollViewer MaxHeight=66 | ✅ YES | §4.4 |
| T1-B | VerticalScrollBarVisibility=Auto | ✅ YES | §4.4 |
| T1-B | 4-column rows [CB][name][P&L][ATM] | ✅ YES | §4.3 |
| T1-B | ATM IsEnabled=false when unchecked | ✅ YES | §4.3 chk.Unchecked lambda |
| T1-B | Apply button Visibility.Collapsed (preserved) | ✅ YES | §4.4 |
| T1-B | Populated from NT8 AtmStrategy filesystem | ✅ YES | §4.3 OnFollowerAtmTemplateComboLoaded |
| T2-B | TryAutoApply() CYC≤3 | ✅ YES (CYC=3) | §5.2, §5.6 |
| T2-B | BuildAtmMap helper | ✅ YES | §5.3 |
| T2-B | BuildMultipliers helper | ✅ YES | §5.4 |
| T2-B | Wired from OnFollowerChecked | ✅ YES | §4.3 lambdas |
| T2-B | Wired from OnFollowerAtmTemplateComboChanged | ✅ YES | §5.5 |
| T2-B | OnApplyRule preserved | ✅ YES | §4.4 applyBtn.Click += OnApplyRule |
| T3-B | "▼ Copier" / "▶ Copier (N active)" header | ✅ YES | §6.4 |
| T3-B | N = checked count | ✅ YES | §6.5 CountActiveFollowers() |
| T3-B | Toggle visibility | ✅ YES | §6.3 |
| T3-B | Default expanded | ✅ YES | §2 _copierCollapsed=false |
| T4-B | SortFollowerRows() CYC≤8 | ✅ YES (CYC=3) | §7.2 |
| T4-B | Checked first, alpha secondary | ✅ YES | §7.2 sort lambda |
| T4-B | Called from OnFollowerChecked | ✅ YES | §4.3 lambdas |
| T4-B | Called from initial load | ✅ YES | §4.2 LoadFollowers() |
| T5-B | Trim/Flatten/ClickTrader/Tighten HIDDEN (Visibility.Collapsed) | ✅ YES | §8.2, §8.4 |
| T5-B | Event handlers PRESERVED (not deleted) | ✅ YES | §8.4 "HIDE NOT DELETE" |
| T5-B | Row1=[BE|BE ALL] 2-col | ✅ YES | §8.1 _beRowPanel UniformGrid 2-col |
| T5-B | Row2=[Quick|Quick ALL] 2-col | ✅ YES | §8.1 _quickRowPanel UniformGrid 2-col |
| T5-B | Quick ALL spinner ▲▼ | ✅ YES | §8.2 quickAllCluster + §8.3 handlers |
| T6-B | Order: 1=ModeRow | ✅ YES | §9.3 step 2, §9.4 BuildModeRow(root) |
| T6-B | Order: 2=Position Tools collapsible | ✅ YES | §9.3 steps 3-6 |
| T6-B | Order: 3=Copier section | ✅ YES | §9.3 step 7, §9.4 BuildCopierSection(root) |
| T6-B | Order: 4=Status bar | ✅ YES | §9.3 step 8 |
| T6-B | Order: 5=BE/BE ALL row | ✅ YES | §9.3 step 9 |
| T6-B | Order: 6=Quick/Quick ALL row | ✅ YES | §9.3 step 10 |
| T6-B | _followerScrollViewer single insertion (no double-add) | ✅ YES | §4.4 NOTE + §6.2 + §9.4 |

**All 33 spec requirements: SATISFIED ✓**

---

### JS P0 RULES CHECK (Cycle 2)

| Rule | Check | Result |
|------|-------|--------|
| JS-001 (no throw in hot path) | No `throw` in any new method. TryAutoApply uses `return;`. | ✅ PASS |
| JS-002 (no return null) | BuildAtmMap returns init'd Dictionary; BuildMultipliers returns init'd int[]. | ✅ PASS |
| JS-021 (no lock()) | No `lock(` anywhere in new code. All methods UI-thread-only. | ✅ PASS |
| JS-033 (no async void) | All new methods are synchronous `private void`. | ✅ PASS |
| JS-009 (no Dictionary for shared/thread-touched) | BuildAtmMap/BuildMultipliers return locals, not stored fields. | ✅ PASS |
| JS-008 (SolidColorBrush Freeze) | Only via MakeBrush() helper which calls .Freeze() internally. | ✅ PASS |
| JS-023 (UI updates on UI thread only) | All new event handlers fire on UI thread. No cross-thread dispatch. | ✅ PASS |

---

### NT8 P0 RULES CHECK (Cycle 2)

| Rule | Check | Result |
|------|-------|--------|
| NT8-001 (`init` setter banned) | No `{ get; init; }` in any new field or property. | ✅ PASS |
| NT8-002 (record banned) | No records of any kind. | ✅ PASS |
| NT8-003 (`volatile double` banned) | New fields are plain bool, int, reference types. None volatile double. | ✅ PASS |
| NT8-004 (ImmutableDictionary banned) | Only `Dictionary<K,V>` used (local return value). | ✅ PASS |
| NT8-007 (CreateOrder arg 12) | No CreateOrder calls in B47. | ✅ N/A |
| NT8-013 (DateTime.Now) | No DateTime usage in B47. | ✅ PASS |
| NT8-016 (TradeCopierWindow not sealed) | Not touched in B47. | ✅ N/A |
| NT8-019 (async void banned) | No async void anywhere. | ✅ PASS |
| NT8-020 (SolidColorBrush Freeze) | Only via MakeBrush() helper. | ✅ PASS |
| NT8-042 (Dispatcher.InvokeAsync banned) | No Dispatcher calls. | ✅ PASS |
| NT8-043 (null-conditional compound assignment) | No `?.` compound assignment patterns. | ✅ PASS |
| NT8-044 (StringComparison needs using System) | `using System;` confirmed at file line 2 (§16). | ✅ PASS |
| NT8-045 (AtmStrategy filesystem) | OnFollowerAtmTemplateComboLoaded (existing handler) reused. | ✅ PASS |

---

### CYC VERIFICATION (Cycle 2)

| Method | CYC | Limit | Result |
|--------|-----|-------|--------|
| LoadFollowers() | 2 | ≤ 8 | ✅ PASS |
| BuildInlineFollowerRow() | 1 | ≤ 8 | ✅ PASS |
| BuildUI() | 1 | ≤ 8 | ✅ PASS |
| OnLoaded() | 5 | ≤ 8 | ✅ PASS |
| TryAutoApply() | 3 | ≤ 3 (spec) | ✅ PASS |
| BuildAtmMap() | 2 | ≤ 8 | ✅ PASS |
| BuildMultipliers() | 3 | ≤ 8 | ✅ PASS |
| BuildCopierSection() | 1 | ≤ 8 | ✅ PASS |
| OnCopierCollapseClick() | 2 | ≤ 8 | ✅ PASS |
| UpdateCopierHeader() | 2 | ≤ 8 | ✅ PASS |
| CountActiveFollowers() | 1 | ≤ 8 | ✅ PASS |
| SortFollowerRows() | 3 | ≤ 8 | ✅ PASS |
| Sort comparison lambda | 3 | ≤ 8 | ✅ PASS |
| BuildBufferedButtonsRow() | 1 | ≤ 8 | ✅ PASS |
| OnQuickAllUp() | 1 | ≤ 8 | ✅ PASS |
| OnQuickAllDown() | 1 | ≤ 8 | ✅ PASS |

**All 16 methods: CYC ≤ 8 ✓. Max observed: 3.**

---

### CYCLE 2 VERDICT

**REVIEW_PASS**

Both P0 violations from Cycle 1 are fully resolved:

| Violation | Cycle 1 status | Cycle 2 status |
|-----------|---------------|---------------|
| VIOLATION-1: P&L column missing from BuildInlineFollowerRow | ❌ FAIL | ✅ FIXED |
| VIOLATION-2: _followerScrollViewer double-add to visual tree | ❌ FAIL | ✅ FIXED |

Zero new violations found in Cycle 2 review.
All spec requirements satisfied (33/33).
All JS P0 rules: PASS.
All NT8 P0 rules: PASS.
All CYC limits: PASS (max CYC = 3 across all new methods).

**Phase 3 (ticket generation) is UNLOCKED.**

---
*Reviewed by ptt-plan-reviewer. Cycle 2 of 2. Gate: REVIEW_PASS.*
