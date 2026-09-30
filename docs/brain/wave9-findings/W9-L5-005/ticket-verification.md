# W9-L5-005 Ticket Verification

**Epic**: W9-L5-005
**File**: `src/V12_002.UI.Panel.Helpers.cs`
**Category**: Lane 5 -- Magic Numbers (L5) -- UI dimensions and offsets
**Resolved commit**: aa9222b4
**Verifier**: V12 Verifier (Phase 5.V)
**Date verified**: 2026-07-06

---

## verification_verdict: PASS

---

## Check 1: All 14 const declarations present, grouped by domain

**Result**: PASS

All 14 consts present at lines 16-38, grouped into 5 named domain groups:

| Line | Const Name | Value | Group |
|------|-----------|-------|-------|
| 17 | `BUTTON_HEIGHT_PX` | 22 | GROUP 1 -- Button/Row Heights |
| 18 | `TEXTBOX_HEIGHT_PX` | 20 | GROUP 1 -- Button/Row Heights |
| 21 | `TARGET_LABEL_COL_WIDTH_PX` | 22 | GROUP 2 -- Column Widths |
| 22 | `CLOSE_BTN_COL_WIDTH_PX` | 22 | GROUP 2 -- Column Widths |
| 23 | `CLOSE_BTN_WIDTH_PX` | 20 | GROUP 2 -- Column Widths |
| 26 | `FONT_SIZE_BTN` | 10 | GROUP 3 -- Font Sizes |
| 27 | `FONT_SIZE_TEXTBOX` | 9 | GROUP 3 -- Font Sizes |
| 28 | `FONT_SIZE_SUBLABEL` | 8 | GROUP 3 -- Font Sizes |
| 29 | `FONT_SIZE_EMA_LABEL` | 11 | GROUP 3 -- Font Sizes |
| 32 | `BTN_PADDING_H` | 2 | GROUP 4 -- Padding/Margin |
| 33 | `CHIP_MARGIN_BOTTOM` | 2 | GROUP 4 -- Padding/Margin |
| 34 | `TARGET_ROW_MARGIN_TOP` | 2 | GROUP 4 -- Padding/Margin |
| 35 | `EMA_LABEL_MARGIN_RIGHT` | 10 | GROUP 4 -- Padding/Margin |
| 38 | `MAX_LIVE_TARGETS` | 5 | GROUP 5 -- Domain Cardinality |

---

## Check 2: All 27 planned substitutions applied

**Result**: PASS

Grep on const name references: 41 lines total (14 declarations + 27 usage lines = 41). Exact match.

Usage-site lines confirmed substituted (non-declaration lines):

| Source Line | Substitution |
|-------------|-------------|
| 60 | `FONT_SIZE_BTN` |
| 62 | `BUTTON_HEIGHT_PX` |
| 63 | `BTN_PADDING_H` (x2 in Thickness args) |
| 82 | `FONT_SIZE_BTN` |
| 84 | `BUTTON_HEIGHT_PX` |
| 108 | `FONT_SIZE_TEXTBOX` |
| 109 | `TEXTBOX_HEIGHT_PX` |
| 237 | `FONT_SIZE_BTN` |
| 259 | `FONT_SIZE_TEXTBOX` |
| 260 | `BUTTON_HEIGHT_PX` |
| 261 | `BTN_PADDING_H` (x2 in Thickness args) |
| 262 | `CHIP_MARGIN_BOTTOM` |
| 271 | `BUTTON_HEIGHT_PX` |
| 272 | `FONT_SIZE_TEXTBOX` |
| 273 | `CHIP_MARGIN_BOTTOM` |
| 305 | `BUTTON_HEIGHT_PX` |
| 307 | `TARGET_ROW_MARGIN_TOP` |
| 308 | `TARGET_LABEL_COL_WIDTH_PX` |
| 311 | `CLOSE_BTN_COL_WIDTH_PX` |
| 318 | `FONT_SIZE_TEXTBOX` |
| 326 | `FONT_SIZE_BTN` |
| 327 | `BTN_PADDING_H` (x2 in Thickness args) |
| 337 | `FONT_SIZE_SUBLABEL` |
| 345 | `CLOSE_BTN_WIDTH_PX` |
| 346 | `FONT_SIZE_SUBLABEL` |
| 363 | `FONT_SIZE_EMA_LABEL` |
| 366 | `EMA_LABEL_MARGIN_RIGHT` |

---

## Check 3: No magic numeric literals from scan table remain

**Result**: PASS

Scan of lines 40-837 (post-declaration section) for bare Height=22/20, FontSize=10/9/8/11,
GridLength(22/20), Thickness with target values: **zero findings**.

One instance of `Thickness(2, 0, 2, 0)` at line 339 (`ctsBlock` margin) remains intentionally
as a trivial value `2` per register rule (line 154): "Trivial values (0, 1, -1, 2) that have
no domain meaning: leave as-is."

No `lock()` in file: confirmed (grep exit 1 = zero matches).

---

## Check 4: dotnet build 0 errors

**Result**: PASS

```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

---

## Check 5: No unintended changes outside planned lines

**Result**: PASS

Commit aa9222b4 touched exactly 1 file: `src/V12_002.UI.Panel.Helpers.cs`.
`git show aa9222b4 --stat` confirms: "1 file changed, 51 insertions(+), 27 deletions(-)"
27 deletions = 27 bare-literal substitutions. 51 insertions = 23 const-block lines + 28 const references.
No other files modified.

---

## CYC Gate

```
CYC_GATE: NOT_FOUND  W9-L5-005  BUTTON_HEIGHT_PX  (not in CYC>8 list -- assumed PASS)
```

**cyc_gate_run**: CYC_GATE: NOT_FOUND  W9-L5-005  BUTTON_HEIGHT_PX  EXIT=0
**cyc_verified**: N/A (const extraction -- no CYC-bearing method)
**build_verified**: true

---

## OKF Rule Compliance

- **Rule 11 (ASCII-only)**: PASS -- all const names are ASCII, SCREAMING_SNAKE_CASE
- **Rule 12 (Naming)**: PASS -- consts use `SCREAMING_SNAKE_CASE` as required
- **Rule 1 (lock-free)**: PASS -- no `lock()` in file
- **Rule 6 (CYC <= 8)**: N/A -- no method CYC changed by this extraction
- **Rule 7 (no allocations)**: N/A -- const declarations have zero runtime cost
