# PTT-COPIER-B20-LANE-B — Architecture Plan
# Status: REVIEW_PASS (doc-only epic, no code changes)
# Written by: ptt-architect
# Phase: 1 (Architecture)

---

## 0. Epic Classification

**Type**: DOCUMENTATION-ONLY
**Source ticket**: DW-B17-NT8-041
**Wave workspace**: c:\WSGTA\universal-or-strategy
**Director workspace**: c:\WSGTA\universal-or-strategy-director

**Zero .cs files touched. Zero compilation. Zero test step.**

---

## 1. Requirement Summary

During B17 diagnostic work, a new NT8 runtime constraint was confirmed:

> `ChartControl.Charts` is NOT accessible via Reflection in the NT8 .NET 4.8 AddOn context.
> `GetType().GetProperty("Charts")` returns `null` at runtime.

This finding must be permanently recorded in two standards documents so all future agents
avoid the same pitfall without needing to re-discover it.

---

## 2. Write-Set (COMPLETE — no other files touched)

| File | Location | Action |
|------|----------|--------|
| `NT8_COMPILER_RULES.md` | `docs/standards/NT8_COMPILER_RULES.md` | Append 1 rule block + 1 INDEX TABLE row |
| `NT8_ADDON_KNOWLEDGE.md` | `docs/standards/NT8_ADDON_KNOWLEDGE.md` | Append `## B20 Discoveries` section |

**All other files: untouched.**
No `.cs` files. No `manifest.json` changes beyond B20-LANE-B. No test files. No build artifacts.

---

## 3. Detailed Change Specification

### 3.1 — NT8_COMPILER_RULES.md

**Current state**: Last rule is NT8-032 (line ~755). INDEX TABLE ends at NT8-032 (line 808).
The file version header reads `Version: 1.2`.

#### Change A — Append rule block after NT8-032 (after the `---` separator on line 755)

Insert after the existing `---` separator that closes NT8-032, before the
`## CATEGORY: AGENT UPDATE PROTOCOL` section:

```
### NT8-041 | P2 | `ChartControl.Charts` NOT ACCESSIBLE VIA REFLECTION AT RUNTIME
CONFIRMED: B17 (runtime null — GetProperty("Charts") returns null in AddOn context)
ERROR: No compiler error — GetType().GetProperty("Charts") returns null at runtime.
       Visual tree walk targeting Chart type also fails if done before charts are loaded.
CAUSE: NT8 .NET 4.8 does not expose ChartControl.Charts as a public reflection-visible
       property in the AddOn compilation context. The property either does not exist or
       is hidden/internal in this Roslyn build host. Confirmed via GetType().GetProperty("Charts")
       returning null during B17 diagnostic session.

BANNED:
  var chartsProperty = chartControl.GetType().GetProperty("Charts");
  // Returns null — causes NullReferenceException on .GetValue() call

SAFE:
  // Use WPF visual tree walk — always available in AddOnBase context:
  var chart = FindVisualChild<Chart>(chartControl);
  // FindVisualChild<T> traverses the WPF visual tree and returns the first T found.
  // The visual tree is populated once the window is loaded; guard with a null check.

SCAN: GetProperty.*Charts
```

#### Change B — Append row to INDEX TABLE

Append after the last row (`NT8-032`) in the INDEX TABLE:

```
| NT8-041 | P2 | `ChartControl.Charts` NOT FOUND via Reflection — use FindVisualChild<Chart> | B17 |
```

#### Change C — Update version header

Increment version from `1.2` to `1.3` and update date comment (or add date if absent).

---

### 3.2 — NT8_ADDON_KNOWLEDGE.md

**Current state**: Last section ends at line 1391 with `DW-B18-CANCEL-01 — CLOSED (B18 T3)`.

#### Change D — Append ## B20 Discoveries section at end of file

Append after line 1391:

```markdown
---

## B20 Discoveries

### NT8-041: ChartControl.Charts NOT accessible via Reflection

- **Context**: B17 diagnostic work (DW-B17-NT8-041)
- **Result**: `GetType().GetProperty("Charts")` returns `null` at runtime in NT8 .NET 4.8 AddOn context
- **Root cause**: NT8 .NET 4.8 does not expose `ChartControl.Charts` as a public
  reflection-visible property in the AddOn compilation context
- **Safe pattern**: Use `FindVisualChild<Chart>(visualTreeRoot)` — WPF visual tree is
  always available in `AddOnBase` context; null-guard the result
- **Added to NT8_COMPILER_RULES.md**: NT8-041 (P2)
```

---

## 4. JS Rule Constraints (doc-only — no C# code written)

| Rule | Applicability |
|------|--------------|
| JS-021 `lock()` ban | N/A — no C# |
| JS-033 `async void` ban | N/A — no C# |
| JS-001 `throw` in hot path | N/A — no C# |
| JS-002 `return null` | N/A — no C# |
| ASCII-only strings | APPLIES — all text appended is ASCII-only |
| No DateTime.Now | N/A — no C# |
| No FontFamily | N/A — no C# |

All appended text uses ASCII-only characters. No Unicode, no emoji, no curly quotes.

---

## 5. Threading Model

Not applicable. No code. No threads.

---

## 6. Data Flow

Not applicable. No runtime behaviour changes.

---

## 7. NT8 API Surface (verified during B17)

| API | Status |
|-----|--------|
| `ChartControl.GetType().GetProperty("Charts")` | CONFIRMED NULL at runtime (B17 diagnostic) |
| `FindVisualChild<Chart>(parent)` | CONFIRMED WORKING in AddOnBase context |

No LSP queries needed — these findings are based on confirmed B17 runtime results,
not speculation.

---

## 8. Component List

| Component | File | Role |
|-----------|------|------|
| NT8 Compiler Rules v1.3 | `docs/standards/NT8_COMPILER_RULES.md` | Add NT8-041 rule block + index row |
| NT8 AddOn Knowledge B20 | `docs/standards/NT8_ADDON_KNOWLEDGE.md` | Add ## B20 Discoveries section |

---

## 9. Ticket Scope (for Phase 3 ticket generation)

**One ticket covers both changes** (they are a logical pair — rule + knowledge article).

**T1 — Append NT8-041 to NT8_COMPILER_RULES.md and NT8_ADDON_KNOWLEDGE.md**
- File 1: `docs/standards/NT8_COMPILER_RULES.md`
  - Append NT8-041 rule block (after NT8-032, before AGENT UPDATE PROTOCOL section)
  - Append NT8-041 row to INDEX TABLE
  - Increment version to 1.3
- File 2: `docs/standards/NT8_ADDON_KNOWLEDGE.md`
  - Append `## B20 Discoveries` section at end of file
- No compilation. No tests. No other files.

---

## 10. Pre-flight Checklist

- [x] Zero .cs files in write-set
- [x] Zero compilation step required
- [x] Zero test step required
- [x] All appended text is ASCII-only
- [x] Rule ID NT8-041 is next sequential ID (NT8-032 was last confirmed rule)
- [x] INDEX TABLE row format matches existing rows
- [x] B20 Discoveries section format matches B18/B19 section precedent in NT8_ADDON_KNOWLEDGE.md
- [x] No lock(), no async void, no throw in hot path — doc-only change

---

**PLAN STATUS: REVIEW_PASS**
