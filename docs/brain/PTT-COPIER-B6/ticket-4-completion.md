# PTT-COPIER-B6 Ticket T4 Completion Report
**Ticket:** T4 -- Spec HTML Update (Documentation Only)
**Block:** B6
**Status:** COMPLETE
**File modified:** specs/002-trade-copier-spec.html (Director workspace)
**Date:** 2026-07-06

---

## Summary

Updated `specs/002-trade-copier-spec.html` to bring it in sync with B3-B6 implementation.
No Wave workspace (`src/`) files were created, modified, or deleted.
Five changes were applied: four new feature sections added and one factual correction made.

---

## Changes Made

### 1. Correction -- JSON to XML (line ~1531 old / line 1827 new)

**Location (new line number):** 1827 (Block 6 phase-detail in the Build Phases section)

**Before:**
```
Serialize/deserialize CopyRule list on NT shutdown/startup (JSON to NT UserDataDir).
Update spec HTML to document BE cluster, Shift+B, ListBox, buffer.
P3 backlog items DW-B5-03 and DW-B5-04.
```

**After:**
```
Serialize/deserialize CopyRule list on NT shutdown/startup using XML (copy_rules.xml)
to NT UserDataDir\PropTraderTools\. XmlSerializer (System.Xml.Serialization -- no NuGet).
SaveRules() on OnDestroyed, LoadRules() on OnInitialize. 3 new xUnit [Fact] persistence
tests (total 22). Spec HTML updated: BE button, Shift+B, ListBox multi-select, stop buffer,
B6 XML persistence. P3 backlog items DW-B5-03 and DW-B5-04 CLOSED.
```

---

### 2. Follower Account Selector row updated in NT-Native UI table (line ~1162)

The existing "Follower account selector" row in the NT-Native UI Specification table was
updated to reflect the B5 change from ComboBox to ListBox + ScrollViewer.

**New line number:** ~1162-1167
**Change:** Cell describes `ListBox` (SelectionMode.Extended) wrapped in `ScrollViewer`
(MaxHeight=80) instead of the old ComboBox-with-CheckBox description.

---

### 3. Runtime configuration table updated (line ~1272)

Two rows updated/added in the Runtime configuration per-rule table:

- "Follower accounts" row: now shows `ListBox` + `ScrollViewer` (B5)
- New "Stop buffer" row added: integer TextBox labeled "Buffer", default 0, tick offset
  added to copied stop distance when `StopBuffer > 0`.

**New line numbers:** ~1272-1274

---

### 4. New card: Break-Even Button (B3/B4 feature)

**Location:** Inserted in `spec-spec-config` tab panel after the Runtime configuration card.
**New element id:** `feature-breakeven`
**New line number:** ~1282

**Documents:**
- UI placement: "BE  S+B" button in TradeCopierWindow rule row and TradeCopierPanel action row
- Handler: `OnBreakEvenClicked` (Panel) / `OnRuleBreakEven` (Window) -> `CopyEngine.Instance.BreakEven(instrument, bufferTicks)`
- Behavior: moves active stop loss to entry + bufferTicks ticks via `order.Change()` on all accounts in the rule
- Buffer field: integer tick count, default 2, live-editable without dialog
- Scope: master + all followers for the active instrument; two instruments never moved simultaneously

---

### 5. New card: Shift+B Keyboard Shortcut (B4/B5 feature)

**Location:** Inserted after Break-Even Button card.
**New element id:** `feature-shiftb`
**New line number:** ~1338

**Documents:**
- Keyboard binding: `Shift+B` registered as WPF `KeyBinding` in `TradeCopierWindow.OnInitialize()`
- Registration: `using System.Windows.Input`, `RelayCommand` nested class wired to `OnWindowBreakEven`
- Handler: `OnWindowBreakEven` reads `_activeRuleInstrument` (set by `MouseEnter` on rule rows)
- Scope: active only when TradeCopierWindow has WPF keyboard focus; identical to clicking [BE]
- Panel equivalent: TradeCopierPanel has its own Shift+B binding wired to `OnBreakEven`

---

### 6. New card: Follower Account Selection -- ListBox / ScrollViewer (B5 feature)

**Location:** Inserted after Shift+B card.
**New element id:** `feature-listbox`
**New line number:** ~1389

**Documents:**
- Previous control (B1-B4): single-select ComboBox with CheckBox items in dropdown
- New control (B5+): `ListBox` with `SelectionMode.Extended`, wrapped in `ScrollViewer` (MaxHeight=80)
- Selection mode: multiple followers selected simultaneously; all receive copied trade
- Engine unchanged: `CopyEngine.AddRule()` already accepted `Account[]` from B1 -- UI-only change in B5

---

### 7. New card: Stop Buffer Field (B5 feature)

**Location:** Inserted after ListBox/ScrollViewer card.
**New element id:** `feature-stopbuffer`
**New line number:** ~1434

**Documents:**
- UI control: integer TextBox labeled "Buffer" adjacent to [BE] button on both Panel and Window
- Purpose: configurable tick offset added to copied stop loss distance; stored as `CopyRule.StopBuffer`
- Effect: when `StopBuffer > 0`, follower stop placed StopBuffer ticks further from entry
- Default: 0 (no offset -- stop copied exactly)
- Live-editable: no properties dialog needed during trading (Control-from-outside pillar)

---

### 8. New card: B6 Rule Persistence (XML)

**Location:** Inserted after Stop Buffer card; closes the spec-spec-config tab panel additions.
**New element id:** `feature-b6-persistence`
**New line number:** ~1486

**Documents:**
- Format: XML via `System.Xml.Serialization.XmlSerializer` (NOT JSON)
- File location: `{UserDataDir}\PropTraderTools\copy_rules.xml` (via `Path.Combine()`)
- Save trigger: `TradeCopierWindow.OnDestroyed()` -> `CopyEngine.Instance.SaveRules()` (first statement)
- Load trigger: `TradeCopierWindow.OnInitialize()` -> `CopyEngine.Instance.LoadRules()` (after init logic)
- Fields persisted: SourceAccountName, FollowerAccountNames[], LotRatio, TickOffset, StopBuffer, IsEnabled
- No-file guard: LoadRules() is no-op when file absent (first run)
- Thread safety: no lock() required; both methods called on NT main thread at lifecycle boundaries

---

## 7-Scan Results (N/A for HTML -- retained for format consistency)

| Scan | Pattern | Result |
|------|---------|--------|
| SCAN-01 | `lock(` in .cs files | N/A -- HTML file only |
| SCAN-02 | Non-ASCII chars in .cs files | N/A -- HTML file only |
| SCAN-03 | `FontFamily` | N/A -- HTML file only |
| SCAN-04 | `#RRGGBB` hex color literals | N/A -- HTML file only |
| SCAN-05 | `CreateOrder` without `PTT-` prefix | N/A -- HTML file only |
| SCAN-06 | `DateTime.Now` | N/A -- HTML file only |
| SCAN-07 | `\block\s*\(` | N/A -- HTML file only |

---

## Definition of Done Checklist

- [x] Break-Even button (B3/B4) documented: UI placement, handler, behavior, buffer, scope
- [x] Shift+B shortcut (B4/B5) documented: KeyBinding registration, handler, scope, panel equivalent
- [x] ListBox/ScrollViewer follower select (B5) documented: control change, selection mode, rationale
- [x] Stop Buffer field (B5) documented: control, purpose, effect, default, live-editable
- [x] B6 persistence section documents XML (copy_rules.xml) -- NOT JSON
- [x] Pre-existing JSON reference at line ~1531 corrected to "XML (copy_rules.xml)"
- [x] HTML structurally valid: no unclosed tags, no broken nesting
- [x] All new content uses ASCII-only text (no Unicode, curly quotes, em-dashes)
- [x] No Wave workspace (src/) files created, modified, or deleted

---

BUILD_PASS
