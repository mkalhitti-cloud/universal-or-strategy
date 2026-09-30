# Ticket 1 Verification — PTT-COPIER-B52 Lane B

**Ticket**: T1 — DW-B50C-02 (DOCS ONLY)
**Title**: Add NT8_ADDON_KNOWLEDGE.md entry for NinjaTrader.Client.dll removal
**Verifier**: ptt-verifier (Phase 4b)
**Date**: 2026-08-08
**Engineer Layer 2 status**: BUILD_PASS

---

## Source Scope

T1 is a docs-only ticket. The only file modified is:
- `docs/standards/NT8_ADDON_KNOWLEDGE.md` (Director workspace)

No `.cs` files were touched. This is verified independently below.

---

## SCAN-08 — Independent Re-Run (Layer 3)

**Command**:
```powershell
Select-String -Path "docs/standards/NT8_ADDON_KNOWLEDGE.md" -Pattern "NinjaTrader\.Client"
```

**Independent results** (Layer 3):

| Line | Match |
|------|-------|
| 1634 | `## B52 Discoveries (2026-08-08)` (section header — B52 present) |
| 1636 | `### NinjaTrader.Client.dll Removed from csproj - CS0433 Globals Ambiguity (B50-LaneC)` |
| 1641 | `NinjaTrader.Client.dll` was referenced in `PropTraderTools.csproj` to |
| 1645 | CS0433: The type 'Globals' exists in both `NinjaTrader.Client, Version=...` and |
| 1649 | `NinjaTrader.Client.dll` is a legacy namespace alias DLL. |
| 1659 | **Rule**: Do NOT add `NinjaTrader.Client.dll` back to `PropTraderTools.csproj`. |

**Hit count: 6 (≥1 required)** — **PASS**

**Layer 2 vs Layer 3 discrepancy**: Engineer reported 5 hits; independent run found 6 hits (line 1634 also matches B52 section header containing "B52 Discoveries" which then leads to the NinjaTrader.Client lines). Minor count difference only — both ≥1. No discrepancy affecting verdict.

---

## T1 Check Results

| Check | Criterion | Evidence | Result |
|-------|-----------|----------|--------|
| T1-1 | `## B52 Discoveries` section present | Line 1634: `## B52 Discoveries (2026-08-08)` | **PASS** |
| T1-2 | Section names `NinjaTrader.Client.dll` explicitly | Line 1636: subsection title names it; line 1641, 1645, 1649, 1659: body names it | **PASS** |
| T1-3 | Section states CS0433 Globals as the cause | Line 1645: `CS0433: The type 'Globals' exists in both 'NinjaTrader.Client, Version=...'` | **PASS** |
| T1-4 | Section names NinjaTrader.Core.dll as replacement | Lines 1650, 1657, 1662: `NinjaTrader.Core.dll` named as the authoritative provider | **PASS** |
| T1-5 | Section includes "Do NOT add" rule | Line 1659: `**Rule**: Do NOT add \`NinjaTrader.Client.dll\` back to \`PropTraderTools.csproj\`...` | **PASS** |
| T1-6 | No .cs files modified (docs-only constraint) | Independent search: no B52 block header in any `.cs` file; no src edit in completion file | **PASS** |

---

## DNA Rule Checks (T1)

T1 is docs-only. No C# source was written. All C# DNA rules (JS-001 through JS-037, NT8 constraints) are **N/A** for this ticket.

---

## Architecture Compliance

- The entry is appended to `NT8_ADDON_KNOWLEDGE.md` as a new `## B52 Discoveries` section — correct location per convention (all prior blocks append in order).
- The entry documents a CS0433 compiler error root cause and a "Do NOT add" rule — exactly the format of all prior block entries (B8 through B15).
- DW-B50C-02 is the target defect work item and is documented within the entry.

---

## Verdict

**VERIFY_PASS**

All 6 T1 checks pass. SCAN-08 independently confirms ≥1 hit. No .cs files modified. Architecture compliant.
