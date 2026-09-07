# Complexity Reduction Protocol (V12.PTT)

## ⚠️ LIZARD MEASUREMENT — READ THIS FIRST

The lizard CSV column order is NLOC first, CCN second.
Using the wrong header mapping reports NLOC as CCN, producing
numbers like 103, 87, 80 that look like CCN but are line counts.
This caused the entire Wave 1 planning to target the wrong methods.

**Canonical lizard command — use verbatim, never alter header order:**
```powershell
lizard src/PropTraderTools/ -x "*/bin/*" -x "*/obj/*" -x "*Tests*" --csv |
ConvertFrom-Csv -Header NLOC,CCN,Token,Params,Length,Location,File,Function,Sig,Start,End |
Where-Object {[int]$_.CCN -gt 8} |
Sort-Object {[int]$_.CCN} -Descending |
Select-Object CCN, Function, @{L="File";E={[IO.Path]::GetFileName($_.File)}} |
Format-Table -AutoSize
```
Full incident record and sanity checks: `docs/protocol/LIZARD_CCN_PROTOCOL.md`

---

## Mandatory Thresholds (No Questions Asked)

### Primary Gate: CYC ≤ 8 (BLOCKING — JS-080)
**Rule**: ALL methods MUST be CYC ≤ 8 before merge.
- **Enforcement**: Pre-push validation — BLOCKING
- **Tool**: lizard (canonical command above, NOT complexity_audit.py which uses wrong threshold)
- **Rationale**: Jane Street JS-080 strict standard. CYC ≤ 8 = fits CPU DSB cache, fully testable paths

### There is no stretch goal or aspirational tier
CYC ≤ 8 is the single hard gate. Anything above 8 must be extracted.
Anything already ≤ 8 must not be touched.

---

## Decision Tree (No Director Approval Needed)

```
Current CYC > 20?
├─ YES → Extract to CYC ≤ 15 (REQUIRED)
│  └─ Natural result ≤ 10? → DONE (bonus achieved)
│  └─ Natural result 11-15? → DONE (gate passed)
│  └─ Natural result 16-20? → Extract further (gate failed)
└─ NO (CYC 15-20) → Extract to CYC ≤ 15 (REQUIRED)
   └─ Same decision tree as above
```

**Key Principle**: Extract until ≤15. If you land at ≤10 naturally, great. If you land at 11-15, stop.

---

## Practical Examples

### Example 1: T-W1-Perf (CYC 31 → ?)
**Current**: 31 (CRITICAL-REFACTOR)
**Action**: Extract helpers until parent ≤ 15
**Likely Outcome**: 
- Parent: 12-15 (PASS - stop here)
- Helpers: 8-10 each (natural result)

**Don't**: Force parent to 10 if it naturally lands at 13 after clean extraction.

### Example 2: OnKeyDown (CYC 49 → ?)
**Current**: 49 (CRITICAL-REFACTOR)
**Action**: Command Pattern extraction
**Likely Outcome**:
- Dispatcher: 5-8 (routing only)
- Handlers: 8-12 each (business logic)

**Don't**: Split handlers further if they land at 11-12.

### Example 3: Method at CYC 17
**Current**: 17 (above gate)
**Action**: Extract ONE helper to drop parent to ≤15
**Likely Outcome**: Parent 14, Helper 8

**Don't**: Extract multiple helpers to force parent to 8.

---

## Parallel Execution Strategy

### When to Parallelize
**Rule**: Parallelize when files DO NOT overlap.

**Safe Parallel Pairs** (from current queue):
- ✅ T-W1-Perf (SIMA.Fleet.cs) + GAP-2 (Photon.Ring.cs) - different files
- ❌ T-W1-Perf (SIMA.Fleet.cs) + GAP-5 (SIMA.Fleet.cs) - SAME FILE

### Parallel Execution Protocol
1. **Check file overlap** using jCodemunch `search_symbols`
2. **If no overlap**: Use 2 Bob Shell windows (different branches)
3. **If overlap**: Sequential execution (first-complete, first-merge)

### Coordination
- **Window 1**: Bob Shell #1 (`bob --mode v12-engineer`)
- **Window 2**: Bob Shell #2 (`bob --mode v12-engineer`)
- **Orchestrator**: Bob IDE (Advanced mode) - monitors both, coordinates F5 gates

---

## Bob Shell Mode Clarification

### Standard Bob Modes (Built-in)
- `orchestrator` - Multi-agent coordination (YES, this exists)
- `architect` - Planning only
- `code` - General coding

### V12 Custom Modes (`.bob/custom_modes.yaml`)
- `v12-epic-planner` - Epic planning (docs/ only)
- `v12-engineer` - Architect + Engineer unified (src/ access)
- `v12-phase7-lead` - Legacy Phase 7 mode

### Correct Usage for T-W1-Perf
**Option A: Single-agent execution**
```bash
bob --mode v12-engineer
```

**Option B: Multi-agent coordination (parallel execution)**
```bash
# Terminal 1
bob --mode orchestrator
# Then delegate to v12-engineer agents in separate windows
```

---

## Master Roadmap Parallel Execution Plan

### Current Queue (Post-Phase-7)
| Task | File | CYC | Parallel Group |
|------|------|-----|----------------|
| T-W1-Perf | SIMA.Fleet.cs:407 | 31 | Group A |
| GAP-5 | SIMA.Fleet.cs:300 | N/A | Group A (CONFLICT) |
| GAP-2 | Photon.Ring.cs | N/A | Group B |

**Conflict Detected**: T-W1-Perf and GAP-5 both touch SIMA.Fleet.cs

**Resolution**:
1. **Clarify GAP-5 status** (roadmap says CLOSED, task.md says NEXT)
2. **If GAP-5 is truly needed**: Sequential (T-W1-Perf → GAP-5)
3. **If GAP-5 is obsolete**: Parallel (T-W1-Perf + GAP-2)

### Parallel Execution Template
```bash
# Window 1: Bob Orchestrator (monitoring)
bob --mode orchestrator

# Window 2: Agent 1 (T-W1-Perf)
bob --mode v12-engineer
# Branch: feature/src-tw1-perf-linq-optimization

# Window 3: Agent 2 (GAP-2)
bob --mode v12-engineer
# Branch: feature/src-gap2-spsc-integration
```

---

## Summary: No More Questions Protocol

### For ANY complexity reduction task:
1. ✅ **Target**: CYC ≤ 15 (BLOCKING gate)
2. ✅ **Bonus**: CYC ≤ 10 if achieved naturally
3. ✅ **Stop**: When gate is passed (don't over-optimize)
4. ✅ **Parallel**: Check file overlap, parallelize if safe
5. ✅ **Mode**: Use `orchestrator` for multi-agent, `v12-engineer` for single-agent

### Agents NEVER need to ask:
- ❌ "Should I target 10 or 15?" → **Answer: 15 (stop if you hit 10 naturally)**
- ❌ "Can I parallelize?" → **Answer: Check file overlap, parallelize if no conflict**
- ❌ "Which mode?" → **Answer: `orchestrator` for coordination, `v12-engineer` for execution**

---

**Version**: V12.22  
**Effective Date**: 2026-05-31  
**Supersedes**: All previous ad-hoc complexity guidance