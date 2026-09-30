# PTT-COPIER-B21-LANE-D — Tickets
# Phase: 3 (Ticket Generation)
# Status: TICKETS_COMPLETE
# Author: ptt-architect
# Source plan: docs/brain/PTT-COPIER-B21-LANE-D/02-architecture-plan.md (REVIEW_PASS)
# Spec: DW-B17-NT8-041

---

## Ticket T1 — NT8-041 Documentation Hardening

### Overview

Harden the NT8 standards knowledge base with the B21 documentation pass for NT8-041
(`ChartControl.Charts` reflection null-return). This is a **DOC-ONLY** ticket — no `.cs`
files are touched, no build or test changes are required.

---

### Spec Requirement IDs

- **DW-B17-NT8-041** — NT8-041 documentation hardening: version header update + B21 Discoveries append

---

### Write-Set (2 files, Director workspace only)

| # | File | Workspace | Change type |
|---|------|-----------|-------------|
| 1 | `docs/standards/NT8_COMPILER_RULES.md` | `c:\WSGTA\universal-or-strategy-director` | Version header lines 2-3 ONLY |
| 2 | `docs/standards/NT8_ADDON_KNOWLEDGE.md` | `c:\WSGTA\universal-or-strategy-director` | Append-only — new `## B21 Discoveries` section after EOF |

**Zero `.cs` files are in scope. Zero `src/PropTraderTools/` files are touched.**

---

### Change A — NT8_COMPILER_RULES.md: Version Header Update

**File**: `docs/standards/NT8_COMPILER_RULES.md`
**Scope**: Lines 2–3 only. All other content is untouched — no reformatting, no reordering.

#### BEFORE (lines 2–3, current state)

```
# Version: 1.3
# Source: PTT Trade Copier blocks B1-B20 (hard compiler errors, runtime crashes, confirmed workarounds)
```

#### AFTER (lines 2–3, target state)

```
# Version: 1.4
# Source: PTT Trade Copier blocks B1-B21 (hard compiler errors, runtime crashes, confirmed workarounds)
```

**Constraint**: Only `1.3` → `1.4` and `B1-B20` → `B1-B21` change.
The remainder of each line is identical character-for-character.

**Do NOT touch**:
- NT8-041 rule block (already correct at line 757) — leave untouched
- NT8-041 INDEX TABLE row (already correct at line 832) — leave untouched
- Any other content in the file

---

### Change B — NT8_ADDON_KNOWLEDGE.md: Append B21 Discoveries Section

**File**: `docs/standards/NT8_ADDON_KNOWLEDGE.md`
**Scope**: Append after the final line (currently line 1402). No existing content is modified.

**Implementation note**: Verify the current EOF state before appending. If the last line has no
trailing newline, add one before the section header to avoid a formatting gap.

#### Text to append (verbatim, after current EOF)

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

**Constraint**: Pure append — no existing lines are altered, reordered, or deleted.

---

### Method Signatures

N/A — doc-only ticket. No methods are implemented or modified.

---

### NT8 Compiler Constraints

N/A — no `.cs` files are in scope. NT8 compiler gate does not apply.

---

### Jane Street DNA Constraints

N/A — no C# code written or modified. All JS-XXX rules are trivially satisfied.
Confirmed: no `lock()`, no `async void`, no `DateTime.Now`, no Unicode, no FontFamily,
no hex color literals, no `CreateOrder` calls.

---

### xUnit [Fact] Tests

N/A — doc-only ticket. No test changes required or permitted.

---

### Build Scan

N/A — doc-only ticket. No compilation step required.

---

### 5-Scan Checklist (SCAN-01 through SCAN-05)

Engineer MUST run each command after applying changes and confirm the expected result
before marking the ticket complete. All paths are relative to
`c:\WSGTA\universal-or-strategy-director`.

| SCAN | Command | Expected result | Pass condition |
|------|---------|-----------------|----------------|
| SCAN-01 | `grep -n "NT8-041" docs/standards/NT8_COMPILER_RULES.md` | >= 1 match | Rule block already present — confirms no regression from Change A |
| SCAN-02 | `grep -n "ChartControl.Charts" docs/standards/NT8_COMPILER_RULES.md` | >= 1 match | Rule block content intact — confirms no regression from Change A |
| SCAN-03 | `grep -n "FindVisualChild" docs/standards/NT8_COMPILER_RULES.md` | >= 1 match | Safe alternative documented — confirms no regression from Change A |
| SCAN-04 | `grep -n "B21" docs/standards/NT8_ADDON_KNOWLEDGE.md` | >= 1 match | New `## B21 Discoveries` section appended — confirms Change B applied |
| SCAN-05 | `grep -rn "lock(" docs/standards/NT8_COMPILER_RULES.md docs/standards/NT8_ADDON_KNOWLEDGE.md` | 0 NEW matches | Any existing `lock(` hits in NT8-018 are pre-existing and expected; verify no new `lock(` was introduced by this ticket |

All 5 scans must pass before the ticket is marked complete.

---

### Completion Criteria

- [ ] Change A applied: lines 2–3 of `NT8_COMPILER_RULES.md` updated (`1.3` → `1.4`, `B1-B20` → `B1-B21`)
- [ ] Change B applied: `## B21 Discoveries` section appended to `NT8_ADDON_KNOWLEDGE.md` after line 1402
- [ ] SCAN-01 passes (>= 1 match for `NT8-041`)
- [ ] SCAN-02 passes (>= 1 match for `ChartControl.Charts`)
- [ ] SCAN-03 passes (>= 1 match for `FindVisualChild`)
- [ ] SCAN-04 passes (>= 1 match for `B21` in `NT8_ADDON_KNOWLEDGE.md`)
- [ ] SCAN-05 passes (0 new `lock(` matches)
- [ ] No `.cs` files were modified
- [ ] NT8-041 rule block at line 757 is unchanged
- [ ] NT8-041 INDEX TABLE row at line 832 is unchanged
- [ ] B20 Discoveries section (lines 1393–1402) is unchanged
