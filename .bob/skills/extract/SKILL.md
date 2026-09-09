---
name: extract
description: Phase 6-style god-function extraction on a high-complexity method.
metadata:
  user-invocable: true
  disable-model-invocation: true
  argument-hint: <file-path> <method-name>
---

# MISSION: God-Function Complexity Extraction
**Target File:** $1
**Target Method:** $2
**Build Tag:** 1111.007-phase7-t1
**Protocol:** V12 Phase 6 Extraction — Metabolic Elegance Standard

---

## STEP 1 -- FORENSIC ANALYSIS (mandatory, do not skip)

### 1a. Read the target file
Use read_file on $1 to load the full source. Focus on $2.

### 1b. jCodemunch Structural Scan
- `get_file_outline` on $1 -- map all symbols, identify the complexity hotspot
- `get_blast_radius` on $2 -- find all callers in the codebase
- `find_references` on $2 -- confirm no external callers that would break on signature change

### 1c. Graphify update
Run: `graphify update .`
Read `graphify-out/GRAPH_REPORT.md` to verify $2 is not a god-node with cross-subgraph callers.

---

## STEP 2 -- RESPONSIBILITY DECOMPOSITION PLAN

Analyze $2 for distinct logical responsibilities. Each responsibility block must:
- Have a single, clear purpose (e.g., "handle fill confirmation", "route to fleet update")
- Be nameable with a PascalCase verb-noun method (e.g., HandleFillConfirmed, RouteFleetUpdate)
- Contain >= 15 lines of logic (below this threshold, extraction adds noise, not clarity)
- NOT introduce new cross-method state dependencies

Produce a decomposition table:

```
## Extraction Plan: $2
### Current State
- Complexity: [CYC score]
- LOC: [line count]
- Responsibilities identified: [N]

### Proposed Sub-Methods
| New Method | Responsibility | Estimated LOC | Extracted From Lines |
|------------|---------------|---------------|---------------------|
| Handle...  | ...           | ~XX           | L### - L###         |
| Process... | ...           | ~XX           | L### - L###         |
| Route...   | ...           | ~XX           | L### - L###         |

### Residual $2 After Extraction
- Estimated complexity: [target < 20 CYC]
- Role: Dispatcher/router only -- reads state, delegates to sub-methods
```

### !!! DIRECTOR APPROVAL GATE !!!
**STOP HERE. Do NOT extract any code until the Director types: APPROVED**

Output: "[EXTRACT-GATE] Decomposition plan complete. Awaiting Director approval."

---

## STEP 3 -- SURGICAL EXTRACTION (Only after APPROVED)

### Split Size Decision
- If total extracted LOC <= 50 lines: use replace_file_content directly in $1
- If total extracted LOC > 50 lines: MANDATORY -- use Python extractor script:
  `python scripts/v12_split.py --source $1 --method $2 --output [new file if needed]`
  Manual copy-paste for splits > 50 lines is BANNED per V12 DNA.

### Extraction Rules
- New sub-methods go in the SAME partial class file ($1) unless LOC pushes the file > 1200 LOC
- If file would exceed 1200 LOC: create a new partial file (e.g., V12_002.UI.Callbacks.OrderUpdate.cs)
- Sub-methods are `private void` unless state return is required, then `private [type]`
- $2 becomes a pure dispatcher: reads state, calls sub-methods, no inline logic > 5 lines
- PascalCase method names, camelCase locals -- no dense one-liners
- NEVER mutate whitespace or indentation in untouched lines
- Touch ONLY the lines being extracted + the new sub-method bodies + the call sites in $2

### DNA Compliance During Extraction
- ZERO new lock() statements
- ZERO non-ASCII characters
- ZERO diff markers in tool calls
- All state mutations must use existing FSM/Enqueue patterns

---

## STEP 4 -- POST-EDIT DNA AUDIT (mandatory)

```powershell
# 4a: Re-establish hard links and ASCII gate
powershell -File .\deploy-sync.ps1

# 4b: Lock regression
grep -r "lock(" src/

# 4c: Unicode regression  
grep -Prn "[^\x00-\x7F]" src/
```

Report to Director:
```
[EXTRACT-AUDIT]
Target: $1 :: $2
deploy-sync.ps1: PASS / FAIL
lock() audit: CLEAN / [N matches]
Unicode audit: CLEAN / [N matches]
Original CYC: [before]
Estimated CYC after extraction: [after]
Sub-methods created: [list]
```

---

## STEP 5 -- HANDOFF

Only after all Step 4 audits PASS:
```
[EXTRACT-COMPLETE]
File: $1
Method: $2
Sub-methods created: [list with LOC]
Complexity reduction: [before] -> [estimated after]
Status: READY FOR F5 COMPILE
```
