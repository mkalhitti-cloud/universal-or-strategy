# PTT-COPIER-B20-LANE-B — Tickets
# Status: TICKETS_COMPLETE
# Written by: ptt-architect (Phase 3)
# Plan: docs/brain/PTT-COPIER-B20-LANE-B/02-architecture-plan.md (REVIEW_PASS)

---

## Epic Classification

**Type**: DOCUMENTATION-ONLY
**Source ticket**: DW-B17-NT8-041 (P2)
**Wave workspace**: c:\WSGTA\universal-or-strategy
**Director workspace**: c:\WSGTA\universal-or-strategy-director

**Zero .cs files. Zero compilation. Zero test step. Zero 7-scan chain.**

---

## T1 — Append NT8-041 to NT8_COMPILER_RULES.md and NT8_ADDON_KNOWLEDGE.md

**Ticket ID**: T1
**Source**: DW-B17-NT8-041 (P2)
**Type**: DOCUMENTATION-ONLY
**Spec requirement**: Record confirmed B17 runtime constraint — ChartControl.Charts
  NOT accessible via Reflection in NT8 .NET 4.8 AddOn context.

---

### WRITE-SET (complete — no other files touched)

| File | Absolute path in wave workspace | Action |
|------|---------------------------------|--------|
| `NT8_COMPILER_RULES.md` | `c:\WSGTA\universal-or-strategy\docs\standards\NT8_COMPILER_RULES.md` | Append rule block + index row + version bump |
| `NT8_ADDON_KNOWLEDGE.md` | `c:\WSGTA\universal-or-strategy\docs\standards\NT8_ADDON_KNOWLEDGE.md` | Append B20 Discoveries section |

**NO .cs files. NO manifest changes beyond B20-LANE-B. NO test files. NO build artifacts.**

---

### Change 1 — NT8_COMPILER_RULES.md

#### 1a. Append rule block

Locate the closing `---` separator that ends the NT8-032 rule block (before the
`## CATEGORY: AGENT UPDATE PROTOCOL` section). Insert the following block immediately
before that section:

```
### NT8-041 | P2 | `ChartControl.Charts` NOT ACCESSIBLE VIA REFLECTION IN NT8
CONFIRMED: B17 (runtime — reflection returns null)
ERROR: ChartControl.Charts property NOT FOUND via Reflection at runtime.
       GetType().GetProperty("Charts") returns null.
CAUSE: NT8 .NET 4.8 does not expose Charts as a public reflection-visible property on
       ChartControl in the AddOn compilation context.
       GetType().GetProperty("Charts") returns null.
BANNED: ChartControl reflection for Charts enumeration
SAFE: Use FindVisualChild<Chart>(parent) visual tree walk instead.
      The visual tree is always available in AddOnBase context.
SCAN: GetProperty.*Charts

---
```

#### 1b. Append INDEX TABLE row

Locate the INDEX TABLE. After the last existing row (NT8-032), append exactly:

```
| NT8-041 | P2 | `ChartControl.Charts` NOT accessible via Reflection — use FindVisualChild<Chart> | B17 |
```

#### 1c. Increment version header

Find the version line near the top of the file (currently `Version: 1.2`).
Change it to:

```
Version: 1.3
```

---

### Change 2 — NT8_ADDON_KNOWLEDGE.md

#### 2a. Append B20 Discoveries section

Navigate to the end of the file (after the last existing section, which ends with
`DW-B18-CANCEL-01 — CLOSED (B18 T3)` or equivalent last line). Append exactly:

```markdown
---

## B20 Discoveries

### NT8-041: ChartControl.Charts NOT accessible via Reflection

- **Context**: B17 diagnostic work -- attempted to enumerate open Chart windows via
  ChartControl.GetType().GetProperty("Charts").GetValue(...).
- **Result**: GetProperty("Charts") returns null at runtime in AddOnBase context.
- **Root cause**: NT8 .NET 4.8 does not expose this property publicly via reflection.
- **Safe pattern**: Use FindVisualChild<Chart>(visualTreeRoot) to enumerate charts.
  This is compile-safe, reflection-free, and works in all AddOnBase phases.
- **Added to NT8_COMPILER_RULES.md**: NT8-041.
```

---

### NOT required for this ticket

- No 7-scan checklist (SCAN-01 through SCAN-07) — documentation ticket, not a code ticket
- No compilation step
- No `deploy-sync.ps1` run
- No xUnit `[Fact]` tests
- No NT8 build gate (F5)
- No `lock()` check (no C# written)
- No `async void` check (no C# written)

---

### SUCCESS CRITERIA

Both of the following must be true after T1 is executed:

1. `NT8_COMPILER_RULES.md` contains:
   - Rule block `NT8-041 | P2 | ChartControl.Charts NOT ACCESSIBLE VIA REFLECTION IN NT8`
     with CONFIRMED, ERROR, CAUSE, BANNED, SAFE, and SCAN fields present
   - INDEX TABLE row for NT8-041 present after the NT8-032 row
   - Version header reads `1.3`

2. `NT8_ADDON_KNOWLEDGE.md` contains:
   - Section `## B20 Discoveries` at or near end of file
   - Subsection `### NT8-041: ChartControl.Charts NOT accessible via Reflection`
   - All five bullet points present (Context, Result, Root cause, Safe pattern, Added to rules)

All appended text is ASCII-only. No Unicode. No emoji. No curly quotes.

---

**T1 STATUS: READY FOR ENGINEER**
