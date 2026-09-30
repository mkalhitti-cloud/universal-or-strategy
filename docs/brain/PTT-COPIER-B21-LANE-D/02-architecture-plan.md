# PTT-COPIER-B21-LANE-D — Architecture Plan
# Phase: 1 (Architecture)
# Status: REVIEW_PASS
# Author: ptt-architect
# Spec requirement: DW-B17-NT8-041

---

## 1. Scope Confirmation

**Lane type**: DOC-ONLY — zero `.cs` files are in scope.

**Files modified** (2 total, both in Director workspace `c:\WSGTA\universal-or-strategy-director`):

| # | File | Change type |
|---|------|-------------|
| 1 | `docs/standards/NT8_COMPILER_RULES.md` | Version header update only (2 lines) |
| 2 | `docs/standards/NT8_ADDON_KNOWLEDGE.md` | Append-only — new `## B21 Discoveries` section |

**No files in `src/PropTraderTools/` are touched.**
**No `.cs` files are touched.**
**No existing rows, blocks, or sections are modified or reformatted.**

---

## 2. Pre-Flight: NT8-041 Already Present

The NT8-041 rule block and INDEX TABLE row were added in B20 work and are **already correct**.
No changes are required to them.

| Artifact | Location | Status |
|----------|----------|--------|
| NT8-041 rule block | `NT8_COMPILER_RULES.md` line 757 | PRESENT — do not touch |
| NT8-041 INDEX TABLE row | `NT8_COMPILER_RULES.md` line 832 | PRESENT — do not touch |
| B20 Discoveries stub | `NT8_ADDON_KNOWLEDGE.md` lines 1393–1402 | PRESENT — do not touch |

---

## 3. Change 1 — NT8_COMPILER_RULES.md: Version Header Update

**File**: `docs/standards/NT8_COMPILER_RULES.md`
**Scope**: Lines 2–3 only. All other content is untouched.

### Before (current state)
```
# Version: 1.3
# Source: PTT Trade Copier blocks B1-B20 (hard compiler errors, runtime crashes, confirmed workarounds)
```

### After (target state)
```
# Version: 1.4
# Source: PTT Trade Copier blocks B1-B21 (hard compiler errors, runtime crashes, confirmed workarounds)
```

**Constraint**: Only the version number (`1.3` → `1.4`) and the block range (`B1-B20` → `B1-B21`)
change. The remainder of the header line is identical character-for-character.

---

## 4. Change 2 — NT8_ADDON_KNOWLEDGE.md: Append B21 Discoveries Section

**File**: `docs/standards/NT8_ADDON_KNOWLEDGE.md`
**Scope**: Append after the final line (currently line 1402). No existing content is modified.

### Text to append (verbatim)

```markdown
## B21 Discoveries
### NT8-041 (documentation hardening pass — B21-LANE-D)

**Discovery origin**: B17 runtime diagnostic. First documented in B20.
**Block**: B21-LANE-D formalised this entry in the standards catalog.

**What was attempted**: Enumerating open NT8 Chart windows from AddOnBase context
via Reflection: `chartControl.GetType().GetProperty("Charts")`.

**What failed**: `GetProperty("Charts")` returns null at runtime in the NT8 .NET 4.8
AddOnBase compilation context. The Charts property is not exposed as a public
reflection-visible property on ChartControl. Calling `.GetValue(chartControl)` on a
null PropertyInfo throws NullReferenceException.

**Safe alternative**: Visual tree walk via `FindVisualChild<Chart>(visualTreeRoot)`.
This is compile-safe, reflection-free, and available in all AddOnBase lifecycle phases.
To enumerate ALL open chart windows: iterate all top-level NT8 windows and cast each to
`NinjaTrader.Gui.Chart.Chart`.

**Rule added**: NT8-041 (P2) in NT8_COMPILER_RULES.md.
**Scan pattern**: grep for `GetProperty.*Charts` or `"Charts"` as a reflection argument.
```

**Constraint**: This is a pure append — no existing lines are altered, reordered, or deleted.

---

## 5. Data Flow (Doc-Only)

```
NT8_COMPILER_RULES.md  (lines 2-3 only)
  └─ version: 1.3 → 1.4
  └─ source: B1-B20 → B1-B21

NT8_ADDON_KNOWLEDGE.md  (append at EOF, after line 1402)
  └─ ## B21 Discoveries
       └─ ### NT8-041 (documentation hardening pass — B21-LANE-D)
           └─ origin, failure analysis, safe alternative, rule ref, scan pattern
```

---

## 6. Threading Model

Not applicable — doc-only lane. No C# code is written or modified.

---

## 7. NT8 API Surface

Not applicable — doc-only lane. The NT8 API types referenced in the appended text
(`ChartControl`, `FindVisualChild<Chart>`, `NinjaTrader.Gui.Chart.Chart`) are documentation
references only; no new code uses them.

---

## 8. 5-Scan Checklist (SCAN-01 through SCAN-05)

Engineer MUST run each grep after applying changes and confirm expected result before
marking the ticket complete.

| SCAN | Command | Expected result | Notes |
|------|---------|-----------------|-------|
| SCAN-01 | `grep -n "NT8-041" docs/standards/NT8_COMPILER_RULES.md` | ≥ 1 match | Row already present — confirms no regression |
| SCAN-02 | `grep -n "ChartControl.Charts" docs/standards/NT8_COMPILER_RULES.md` | ≥ 1 match | Block already present — confirms no regression |
| SCAN-03 | `grep -n "FindVisualChild" docs/standards/NT8_COMPILER_RULES.md` | ≥ 1 match | Safe alternative already documented |
| SCAN-04 | `grep -n "B21" docs/standards/NT8_ADDON_KNOWLEDGE.md` | ≥ 1 match | Confirms new `## B21 Discoveries` section was appended |
| SCAN-05 | `grep -rn "lock(" docs/standards/` | 0 matches | Trivially satisfied — doc-only lane, no code added |

---

## 9. Constraints Checklist

- [x] APPEND ONLY — no existing rows, blocks, or sections modified in either file
- [x] NT8-041 rule block already correct — not touched
- [x] NT8-041 INDEX TABLE row already correct — not touched
- [x] B20 Discoveries stub already present — not touched
- [x] Version header: only lines 2–3 changed (`1.3` → `1.4`, `B1-B20` → `B1-B21`)
- [x] No `.cs` files in scope
- [x] No `lock()` introduced (doc-only)
- [x] No `DateTime.Now` introduced (doc-only)
- [x] No FontFamily, no hex colors, no Unicode identifiers (doc-only)
- [x] All changes are in Director workspace (`c:\WSGTA\universal-or-strategy-director`)

---

## 10. Return Value

**PLAN_COMPLETE**
