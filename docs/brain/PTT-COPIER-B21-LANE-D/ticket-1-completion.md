# PTT-COPIER-B21-LANE-D -- Ticket T1 Completion Report
# Ticket: T1 -- NT8-041 Documentation Hardening (doc-only)
# Engineer: ptt-engineer
# Status: BUILD_PASS
# Spec: DW-B17-NT8-041

---

## 1. Summary of Changes

This ticket is DOC-ONLY. Zero `.cs` files were touched. Zero `src/PropTraderTools/` files
were touched. Two Markdown files in the Director workspace were updated.

| # | File | Workspace | Change type |
|---|------|-----------|-------------|
| 1 | `docs/standards/NT8_COMPILER_RULES.md` | `c:\WSGTA\universal-or-strategy-director` | Version header lines 2-3 ONLY |
| 2 | `docs/standards/NT8_ADDON_KNOWLEDGE.md` | `c:\WSGTA\universal-or-strategy-director` | Append-only -- new `## B21 Discoveries` section after EOF |

---

## 2. Change A Evidence -- NT8_COMPILER_RULES.md Lines 2-3

**File**: `docs/standards/NT8_COMPILER_RULES.md`

### BEFORE (lines 2-3)
```
# Version: 1.3
# Source: PTT Trade Copier blocks B1-B20 (hard compiler errors, runtime crashes, confirmed workarounds)
```

### AFTER (lines 2-3)
```
# Version: 1.4
# Source: PTT Trade Copier blocks B1-B21 (hard compiler errors, runtime crashes, confirmed workarounds)
```

Only `1.3` -> `1.4` and `B1-B20` -> `B1-B21` changed.
All other content in the file is untouched -- including:
- NT8-041 rule block at line 757 (unchanged)
- NT8-041 INDEX TABLE row at line 832 (unchanged)
- All other rules and content

---

## 3. Change B Evidence -- NT8_ADDON_KNOWLEDGE.md Append

**File**: `docs/standards/NT8_ADDON_KNOWLEDGE.md`
**Appended after line 1402 (previous EOF)**

New content at lines 1403-1425 (confirmed by SCAN-04 hitting line 1405):

```markdown

---

## B21 Discoveries
### NT8-041 (documentation hardening pass -- B21-LANE-D)

**Discovery origin**: B17 runtime diagnostic. First documented in B20 stub.
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

No existing lines were altered, reordered, or deleted.

---

## 4. Scan Results (verbatim output)

### SCAN-01: NT8-041 present in NT8_COMPILER_RULES.md

Command: `Select-String -Path docs/standards/NT8_COMPILER_RULES.md -Pattern "NT8-041"`

```
docs\standards\NT8_COMPILER_RULES.md:757:### NT8-041 | P2 | `ChartControl.Charts` NOT ACCESSIBLE VIA REFLECTION IN NT8
docs\standards\NT8_COMPILER_RULES.md:832:| NT8-041 | P2 | `ChartControl.Charts` NOT accessible via Reflection -- use FindVisualChild<Chart> | B17 |
```

**RESULT: PASS -- 2 matches (>= 1 required)**

---

### SCAN-02: ChartControl.Charts present in NT8_COMPILER_RULES.md

Command: `Select-String -Path docs/standards/NT8_COMPILER_RULES.md -Pattern "ChartControl\.Charts"`

```
docs\standards\NT8_COMPILER_RULES.md:757:### NT8-041 | P2 | `ChartControl.Charts` NOT ACCESSIBLE VIA REFLECTION IN NT8
docs\standards\NT8_COMPILER_RULES.md:759:ERROR: ChartControl.Charts property NOT FOUND via Reflection at runtime.
docs\standards\NT8_COMPILER_RULES.md:832:| NT8-041 | P2 | `ChartControl.Charts` NOT accessible via Reflection -- use FindVisualChild<Chart> | B17 |
```

**RESULT: PASS -- 3 matches (>= 1 required)**

---

### SCAN-03: FindVisualChild present in NT8_COMPILER_RULES.md

Command: `Select-String -Path docs/standards/NT8_COMPILER_RULES.md -Pattern "FindVisualChild"`

```
docs\standards\NT8_COMPILER_RULES.md:237:  var cc = FindVisualChild<ChartControl>(chart);
docs\standards\NT8_COMPILER_RULES.md:238:  // FindVisualChild<T> is the depth-first helper already in TradeCopierAddOn.cs
docs\standards\NT8_COMPILER_RULES.md:772:  var chart = FindVisualChild<Chart>(visualTreeRoot);
docs\standards\NT8_COMPILER_RULES.md:774:  // FindVisualChild<T> is in TradeCopierAddOn.cs (the depth-first helper).
docs\standards\NT8_COMPILER_RULES.md:807:| NT8-008 | P0 | `Chart.ChartControl` property does not exist - use FindVisualChild | B8 |
docs\standards\NT8_COMPILER_RULES.md:832:| NT8-041 | P2 | `ChartControl.Charts` NOT accessible via Reflection -- use FindVisualChild<Chart> | B17 |
```

**RESULT: PASS -- 6 matches (>= 1 required)**

---

### SCAN-04: B21 present in NT8_ADDON_KNOWLEDGE.md

Command: `Select-String -Path docs/standards/NT8_ADDON_KNOWLEDGE.md -Pattern "B21"`

```
docs\standards\NT8_ADDON_KNOWLEDGE.md:1405:## B21 Discoveries
docs\standards\NT8_ADDON_KNOWLEDGE.md:1406:### NT8-041 (documentation hardening pass -- B21-LANE-D)
docs\standards\NT8_ADDON_KNOWLEDGE.md:1409:**Block**: B21-LANE-D formalised this entry in the standards catalog.
```

**RESULT: PASS -- 3 matches (>= 1 required)**

---

### SCAN-05: Zero new lock( introduced

Command: `Select-String -Path docs/standards/NT8_COMPILER_RULES.md,docs/standards/NT8_ADDON_KNOWLEDGE.md -Pattern "lock\("`

```
docs\standards\NT8_COMPILER_RULES.md:434:### NT8-018 | P1 | `lock()` IS BANNED - USE `volatile` + `ConcurrentDictionary`/`ConcurrentBag`
docs\standards\NT8_COMPILER_RULES.md:817:| NT8-018 | P1 | `lock()` banned - use volatile + ConcurrentDictionary/ConcurrentBag | B1 |
```

**RESULT: PASS -- 2 hits, both pre-existing from NT8-018 rule content (lines 434 and 817
in NT8_COMPILER_RULES.md). Zero hits in NT8_ADDON_KNOWLEDGE.md. Zero new lock( introduced
by this ticket.**

---

## 5. Completion Checklist

- [x] Change A applied: lines 2-3 of NT8_COMPILER_RULES.md updated (1.3 -> 1.4, B1-B20 -> B1-B21)
- [x] Change B applied: ## B21 Discoveries section appended to NT8_ADDON_KNOWLEDGE.md after line 1402
- [x] SCAN-01 passes (2 matches for NT8-041)
- [x] SCAN-02 passes (3 matches for ChartControl.Charts)
- [x] SCAN-03 passes (6 matches for FindVisualChild)
- [x] SCAN-04 passes (3 matches for B21 in NT8_ADDON_KNOWLEDGE.md)
- [x] SCAN-05 passes (0 new lock( matches; 2 pre-existing NT8-018 hits)
- [x] No .cs files were modified
- [x] NT8-041 rule block at line 757 is unchanged
- [x] NT8-041 INDEX TABLE row at line 832 is unchanged
- [x] B20 Discoveries section (lines 1393-1402) is unchanged

---

## BUILD_PASS
