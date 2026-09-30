# PTT-COPIER Deferred Backlog
# Block: PTT-COPIER-B21-LANE-D
# Date: 2026-07-07
# Author: ptt-plan-reviewer (Phase 5 Final Review)
# Spec: DW-B17-NT8-041

---

## PTT-COPIER-B21-LANE-D

### Block Summary

Lane type: DOC-ONLY  
Spec requirement: DW-B17-NT8-041  
Files modified: `docs/standards/NT8_COMPILER_RULES.md`, `docs/standards/NT8_ADDON_KNOWLEDGE.md`  
Files in `src/PropTraderTools/`: ZERO  
Final review verdict: FINAL_PASS  

### DW-B17-NT8-041 Status

**Status: CLOSED**

Documentation complete. The NT8-041 rule for `ChartControl.Charts` reflection failure has been:

1. Catalogued in `NT8_COMPILER_RULES.md` with a full rule block (lines 757-778) and INDEX TABLE
   row (line 832), first added in B20.
2. Formalised in `NT8_ADDON_KNOWLEDGE.md` with the `## B21 Discoveries` section (lines 1405-1425)
   documenting: discovery origin (B17), reflection attempt, failure cause (NT8 .NET 4.8 does not
   expose `Charts` as a reflection-visible property), safe alternative (`FindVisualChild<Chart>`),
   rule reference (NT8-041 P2), and scan pattern (`GetProperty.*Charts`).
3. Version header updated: `NT8_COMPILER_RULES.md` now reads `Version: 1.4, Source: B1-B21`.

All 5 scans passed (V-SCAN-01 through V-SCAN-05). Zero new `lock(` introduced. Zero `.cs` files
modified. Zero DNA violations.

---

## Deferred Items

None. No work was deferred out of scope in this lane. No documentation gaps remain for NT8-041.

---

## Open Items From Prior Blocks (if any)

No prior open items tracked in this lane's backlog. This is the initial deferred-backlog file
for PTT-COPIER-B21-LANE-D.

---

## Notes for Future Blocks

If a future block attempts to use `ChartControl.Charts` via reflection:
- The agent MUST read NT8-041 in `NT8_COMPILER_RULES.md` (SCAN: `GetProperty.*Charts`)
- The approved safe alternative is `FindVisualChild<Chart>(visualTreeRoot)` as implemented
  in `TradeCopierAddOn.cs`
- No further documentation updates are required for NT8-041 unless new failure modes are discovered
