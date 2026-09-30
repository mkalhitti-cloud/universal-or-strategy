# Deferred Backlog — B104-LaneA
## Written by: Ph5 (ptt-plan-reviewer)
## Block: B104  Lane: A
## FINAL_PASS date: post F5 green confirm

---

## B104-LaneA Block Entry

### Completed This Block
- **DW-B104** — QX Bracket Fallback Loses Remainder Unit  
  Fixed in `PttQuickExit.cs` via `CalcTNQty` extract-method. Last OCO pair now absorbs remainder.  
  All 7 scans green. NT8 F5 compile green. `ptt-sync-and-verify.ps1` PASS.

### Deferred from This Block

| ID | Severity | Description | File | Reason Deferred |
|----|----------|-------------|------|-----------------|
| DW-B104-FOLLOWUP-01 | LOW | Pre-existing non-ASCII character at L222 (compat overload XML doc contains Unicode arrow "→") | `PttQuickExit.cs` | Zero-other-scope mandate: B104-LaneA restricted to DW-B104 fix only. Candidate for a dedicated ASCII-cleanup epic. |

### Carry-Forward from Prior Blocks
_(None known at time of writing. Prior deferred backlog not present for this epic path.)_

---

## Notes
- DW-B104-FOLLOWUP-01 does not affect compilation or correctness — it is a cosmetic doc-comment issue.
- The pre-existing character was present before B104-LaneA. It was observed during Scan 5 (ASCII check) and correctly classified as out-of-scope.
