# B26 Lane C — Tickets

**Epic**: B26-LaneC
**Phase**: 4 (Ticket Generation)
**Author**: ptt-architect
**Source plan**: `docs/brain/B26-LaneC/02-architecture-plan.md` (REVIEW_PASS, Revision 2)
**Spec**: `specs/002-trade-copier-spec.html` L10190-10252
**File in scope**: `src/PropTraderTools/TradeCopierPanel.cs` (only)
**[Fact] baseline**: 131  |  **[Fact] after Lane C**: 131 (unchanged)

---

## TICKET B26-C-T1 — DW-B26-03: BE Armed/Connected visual fix

**Spec ID**: DW-B26-03 (spec L10191-10210)
**Plan ref**: Part A (plan L27-113)
**File**: `c:\WSGTA\universal-or-strategy\src\PropTraderTools\TradeCopierPanel.cs`
**Wave workspace root**: `c:\WSGTA\universal-or-strategy`

### Problem

`UpdateBeVisuals(Armed)` sets `_beBtn2.BorderBrush = BrushCaution` (amber) +
`BorderThickness = 2`.  
`UpdateButtonColors` at L418 runs on every position-state tick and unconditionally writes
`_beBtn2.Background = hasPosition ? BrushActive : BrushInactive`, overwriting the background.  
The NT8 WPF button template renders `Background` over the interior — `BorderBrush`-only changes are
invisible. Armed and Connected states are therefore visually indistinguishable from Idle.

### Method signatures (existing — do not rename)

```csharp
private void UpdateBeVisuals()          // L826 — CYC = 3 (3-case switch, unchanged)
private void UpdateButtonColors(bool hasPosition)   // L412 — CYC = 5 (unchanged)
```

### Changes — read exact current lines first, then apply

> **Engineer mandate**: Before editing, read the exact current content of each target section
> using `read_file` or equivalent. If the actual lines differ from the OLD block shown below,
> STOP and report the discrepancy. Do NOT guess or apply the diff blindly.

---

#### CHANGE 1 — `UpdateBeVisuals` Idle case

**Locate**: The `case BeState.Idle:` block inside `UpdateBeVisuals` (approx. L832-836).

**OLD** (3 property-assignment lines — exact match required):
```csharp
    _beBtn2.Content         = FormatBuffer("BE", _beBuffer);
    _beBtn2.BorderBrush     = null;
    _beBtn2.BorderThickness = new Thickness(0);
```

**NEW** (2 lines — replaces all 3 above):
```csharp
    _beBtn2.Content    = FormatBuffer("BE", _beBuffer);
    _beBtn2.Background = BrushInactive;
```

*Rationale*: Explicit reset so returning to Idle always clears amber/blue (spec L10206, Fix step 3).
`BorderBrush = null` and `BorderThickness = new Thickness(0)` are artefacts of the old approach —
remove them entirely.

---

#### CHANGE 2 — `UpdateBeVisuals` Armed case

**Locate**: The `case BeState.Armed:` block inside `UpdateBeVisuals` (approx. L837-841).

**OLD** (3 property-assignment lines — exact match required):
```csharp
    _beBtn2.Content         = "BE Armed";
    _beBtn2.BorderBrush     = BrushCaution;
    _beBtn2.BorderThickness = new Thickness(2);
```

**NEW** (2 lines — replaces all 3 above):
```csharp
    _beBtn2.Content    = "BE Armed";
    _beBtn2.Background = BrushCaution;
```

*Rationale*: Background is the primary visual signal (Architecture Decision spec L10247).
`BorderBrush` and `BorderThickness` assignments dropped entirely.

---

#### CHANGE 3 — `UpdateBeVisuals` Connected case

**Locate**: The `case BeState.Connected:` block inside `UpdateBeVisuals` (approx. L842-846).

**OLD** (3 property-assignment lines — exact match required):
```csharp
    _beBtn2.Content         = "BE Live";
    _beBtn2.BorderBrush     = BrushConnected;
    _beBtn2.BorderThickness = new Thickness(2);
```

**NEW** (2 lines — replaces all 3 above):
```csharp
    _beBtn2.Content    = "BE Live";
    _beBtn2.Background = BrushConnected;
```

*Rationale*: Same as Armed — Background is the primary visual signal.
`BorderBrush` and `BorderThickness` assignments dropped entirely.

---

#### CHANGE 4 — `UpdateButtonColors` L418 guard

**Locate**: The single-line BE background write inside `UpdateButtonColors` (approx. L418).

**OLD** (exact line — exact match required):
```csharp
if (_beBtn2 != null) _beBtn2.Background = hasPosition ? BrushActive : BrushInactive;
```

**NEW** (same line with added guard):
```csharp
if (_beBtn2 != null && _beState == BeState.Idle) _beBtn2.Background = hasPosition ? BrushActive : BrushInactive;
```

*Rationale*: Prevents position-tick writes from overwriting amber/blue when state is Armed or
Connected. Spec L10203-10205, Architecture Decision spec L10247.

---

### Brush definitions (no new brushes needed)

| Brush           | Line  | Value                                  |
|-----------------|-------|----------------------------------------|
| `BrushConnected` | L181 | `MakeBrush(59, 130, 246)` — blue       |
| `BrushCaution`   | L195 | `MakeBrush(245, 158, 11)` — amber      |
| `BrushInactive`  | existing | already defined — grey             |

Both brushes are frozen via `MakeBrush` (calls `.Freeze()`). Do not redefine.

---

### Acceptance criteria

1. Click BE button once (Armed) → button shows **amber** background. Hover/focus changes do not
   override amber.
2. A position-state tick fires while Armed → button does **NOT** reset to green/grey; amber persists.
3. Click BE again (Connected) → button shows **blue** background.
4. Click BE to reset (Idle) → button returns to position-aware green/grey from `UpdateButtonColors`.
5. `UpdateBeVisuals` CYC = **3** (unchanged — 3-case switch, same shape).
6. `UpdateButtonColors` CYC = **5** (unchanged — guard replaces unconditional write, same branch count).
7. Zero `BorderBrush` or `BorderThickness` references remain inside the 3 switch cases of
   `UpdateBeVisuals`.

---

### 7-SCAN CHECKLIST — T1

| Scan | Check | Command | Expected result |
|------|-------|---------|-----------------|
| SCAN-01 | JS-021 `lock()` | `grep -n "lock(" TradeCopierPanel.cs` | Zero new occurrences in changed lines |
| SCAN-02 | JS-001 `throw new` | `grep -n "throw new" TradeCopierPanel.cs` | No new throws in changed lines |
| SCAN-03 | JS-002 `return null` | `grep -n "return null" TradeCopierPanel.cs` | No new `return null` in changed lines |
| SCAN-04 | NT8-001 `init` accessor | Inspect changed lines | No `{ get; init; }` introduced |
| SCAN-05 | NT8-002 `record` keyword | Inspect changed lines | No `record` keyword introduced |
| SCAN-06 | NT8-003 `volatile` | Inspect changed lines | No new `volatile` field introduced |
| SCAN-07 | CYC gate | Manual count or complexity tool | `UpdateBeVisuals` = 3, `UpdateButtonColors` = 5 — both ≤ 8 |

Engineer signs off: all 7 scans pass before marking T1 complete.

---

## TICKET B26-C-T2 — DEAD-B26: Remove 5 dead fields + 2 dead methods

**Spec ID**: DEAD-B26 (spec L10213-10236)
**Plan ref**: Part B (plan L116-144)
**File**: `c:\WSGTA\universal-or-strategy\src\PropTraderTools\TradeCopierPanel.cs`
**Wave workspace root**: `c:\WSGTA\universal-or-strategy`

### Problem

B12 restructured the panel to buffered V2 buttons (`_beBtn2`, `_trimBtn2`, etc.) but left V1 field
declarations intact at L121-125. Two V1 event handlers (`OnToggle`, `OnBreakEven`) remain in the
file but are not wired to any button after B12. They are dead code confirmed by grep.

### Method signatures being deleted (dead — do not retain)

```csharp
private void OnToggle(object sender, RoutedEventArgs e)     // L1270-1276 — dead V1 handler
private void OnBreakEven(object sender, RoutedEventArgs e)  // L1293-1300 — dead V1 handler
```

### Changes — ALWAYS re-read exact lines before each deletion

> **Engineer mandate**: Before deleting any block, read the exact current lines using `read_file`.
> If the actual content differs from the description below, STOP and report the discrepancy.
> Do NOT delete by line number alone — confirm content matches first.
> Perform deletions in reverse line order (highest line first) to keep earlier line numbers stable.

---

#### DELETION 1 — `OnBreakEven` method (approx. L1293-1300, 8 lines)

**Delete in full** the method that begins:
```csharp
private void OnBreakEven(object sender, RoutedEventArgs e)
```
and ends at its closing `}`.

Evidence of dead code:
- Zero `Click+=` wiring to `OnBreakEven` anywhere in the file (grep confirmed).
- Superseded by `OnBeClick` introduced in B12.
- The only live use of `_beBufferBox` inside this method is at `DispatchShortcut` L1417 — that
  reference is in a **different** method and is preserved. Deleting `OnBreakEven` does not remove
  `_beBufferBox` from the file.

---

#### DELETION 2 — `OnToggle` method (approx. L1270-1276, 7 lines)

**Delete in full** the method that begins:
```csharp
private void OnToggle(object sender, RoutedEventArgs e)
```
and ends at its closing `}`.

Evidence of dead code:
- Zero `Click+=` wiring to `OnToggle` anywhere in the file (grep confirmed).
- Superseded by `OnCopyToggle` at L713 introduced in B12.
- `_copyToggleBtn` referenced inside this method (approx. L1274-1275) is itself a dead field
  (DELETION 3). Both references vanish together with the method body.

---

#### DELETION 3 — dead field declarations (approx. L121-125, 5 lines)

**Delete exactly these 5 lines** (verify content before deleting):

```csharp
private Button      _copyToggleBtn;
private Button      _flattenBtn;
private Button      _cancelBtn;
private Button      _trimBtn;
private Button      _beBtn;
```

Evidence of dead code:
- `_copyToggleBtn`: never assigned; only referenced inside `OnToggle` (itself deleted above).
  Comment at L472 confirms "old 4-column actionGrid and `_copyToggleBtn` removed."
- `_flattenBtn`: declaration only; referenced only inside `OnFlatten` (out of scope — not deleted).
- `_cancelBtn`: declaration only; referenced only inside `OnCancel` V1 (out of scope — not deleted).
- `_trimBtn`: declaration only; referenced only inside `OnTrim` (out of scope — not deleted).
- `_beBtn`: declaration only; zero references anywhere in the file.

---

### Lines explicitly NOT in scope — do NOT touch

| Line | Symbol | Reason |
|------|--------|--------|
| L126 | `private TextBlock _statusText;` | LIVE — many refs throughout file |
| L127 | `private bool _copyEnabled;` | LIVE |
| L128 | `private TextBox _beBufferBox;` | LIVE — `DispatchShortcut` Key.B at L1417 reads it |
| L1278-1281 | `OnTrim` | Out of scope — not authorized by DEAD-B26 |
| L1283-1286 | `OnFlatten` | Out of scope — not authorized by DEAD-B26 |
| L1288-1291 | `OnCancel` | Out of scope — not authorized by DEAD-B26 |

If `OnTrim`, `OnFlatten`, or `OnCancel` are believed dead, raise a new deferred item
(`DW-B26-backlog-01`) for Director approval. Do NOT delete them as part of this ticket.

---

### Post-deletion verification (engineer must run all three)

```powershell
# 1. Zero remaining refs to any deleted symbol
Select-String -Path TradeCopierPanel.cs -Pattern "_copyToggleBtn|_flattenBtn|_cancelBtn|_trimBtn|_beBtn\b"
# Expected: zero results

# 2. Zero remaining refs to deleted methods
Select-String -Path TradeCopierPanel.cs -Pattern "OnToggle|OnBreakEven"
# Expected: zero results

# 3. _beBufferBox still present (LIVE ref)
Select-String -Path TradeCopierPanel.cs -Pattern "_beBufferBox"
# Expected: at least 1 result (L1417 DispatchShortcut)
```

---

### Acceptance criteria

1. File compiles with zero errors (no CS0103 / CS0246 for deleted symbols).
2. Zero references to any of the 7 deleted items remain in the file.
3. `_beBufferBox` field (L128) and its `DispatchShortcut` reference (L1417) are still present.
4. `_statusText`, `_copyEnabled`, `OnTrim`, `OnFlatten`, `OnCancel` are untouched.
5. `[Fact]` count = **131** (unchanged — no test references any deleted symbol).

---

### 7-SCAN CHECKLIST — T2

| Scan | Check | Command | Expected result |
|------|-------|---------|-----------------|
| SCAN-01 | JS-021 `lock()` | `grep -n "lock(" TradeCopierPanel.cs` | Zero — deletion adds nothing |
| SCAN-02 | JS-001 `throw new` | `grep -n "throw new" TradeCopierPanel.cs` | No new throws — deletion only |
| SCAN-03 | JS-002 `return null` | `grep -n "return null" TradeCopierPanel.cs` | No new return null — deletion only |
| SCAN-04 | NT8-001/002/003 | Inspect change | No `init`/`record`/`volatile` introduced — deletion only |
| SCAN-05 | Dead symbol refs | See post-deletion verification above | Zero refs to `_copyToggleBtn`, `_flattenBtn`, `_cancelBtn`, `_trimBtn`, `_beBtn`, `OnToggle`, `OnBreakEven` |
| SCAN-06 | `_beBufferBox` live | `grep -n "_beBufferBox" TradeCopierPanel.cs` | At least 1 result (L1417) — field retained |
| SCAN-07 | `[Fact]` count | `grep -c "\[Fact\]" *.Tests.cs` | 131 — unchanged |

Engineer signs off: all 7 scans pass before marking T2 complete.

---

## Execution order

1. **T1 first** (visual fix — additive changes only, no line-number shift for T2).
2. **T2 second** (deletions — use reverse-order deletion to preserve line number stability within T2
   itself: delete `OnBreakEven` first, then `OnToggle`, then the field block at L121-125).

Both tickets touch only `TradeCopierPanel.cs`. No other files. No new tests. No cross-file deps.
