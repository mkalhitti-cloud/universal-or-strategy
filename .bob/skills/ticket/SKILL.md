---
name: ticket
description: Execute a single V12 refactoring ticket in a new isolated Bob session.
metadata:
  user-invocable: true
  disable-model-invocation: true
  argument-hint: <path-to-ticket-file>
---

# TICKET EXECUTION
**Ticket File:** $1
**Mode:** v12-engineer (Plan-Then-Execute Protocol)
**Protocol:** V12 Photon Kernel DNA -- Zero Logic Drift

> This command is designed to run in a NEW Bob session for each ticket.
> Read the ticket file completely before writing a single line of code.
> STOP after the plan for Director approval. Do not execute autonomously.

---

## STEP 1 -- READ THE TICKET (mandatory, do not skip)

Read the full ticket file at $1. Extract:
- Objective and scope boundaries
- Sub-methods to extract (names, responsibilities, LOC estimates)
- Context references (analysis and approach docs)
- V12 DNA guardrails
- Acceptance criteria

If any field is ambiguous or missing, STOP and ask the Director before proceeding.

---

## STEP 2 -- FORENSIC ANALYSIS (verify live code matches the ticket)

Using jCodemunch MCP tools:

### 2a. File Outline
`get_file_outline` on each target file from the ticket -- verify actual symbol names and line numbers match the ticket's references. If there is a mismatch, report it to the Director.

### 2b. Blast Radius Check
`get_blast_radius` on the target method -- confirm caller count matches the analysis doc.
If new callers are found that were not in the analysis, STOP and report.

### 2c. Complexity Confirmation
Run `python scripts/complexity_audit.py` and note the BEFORE CYC score for the target method.
This becomes the baseline for the AFTER comparison in Step 5.

---

## STEP 3 -- WRITE THE EXTRACTION PLAN

Produce a written plan with the following structure:

```
## Extraction Plan: [target method from ticket]
### Baseline
- Current CYC: [from audit]
- Current LOC: [from file outline]
- Extraction targets: [N sub-methods]

### Sub-Methods to Create
| New Method | Responsibility | LOC Estimate | Source Lines |
|------------|---------------|--------------|-------------|
| Handle...  | ...           | ~XX          | L### - L### |

### Residual [Target Method] After Extraction
- Estimated CYC: [target < 20]
- Role: Pure dispatcher -- reads state, calls sub-methods, no inline logic > 5 lines

### Caller Impact
- Files affected: [list]
- Signature changes: [YES/NO -- if YES, list them]

### File Placement Decision
- New sub-methods go in: [same file / new partial file name]
- Reason: [LOC threshold / concern separation]
```

---

## !! DIRECTOR APPROVAL GATE !!
**STOP HERE. Do NOT write any code until the Director types: APPROVED**

If the Director has not typed APPROVED, output:
"[TICKET-GATE] Plan complete for [ticket name]. Awaiting Director approval before surgical execution."

---

## STEP 4 -- SURGICAL EXECUTION (only after APPROVED)

### Split Size Decision
- If total extracted LOC <= 50 lines: use replace_file_content directly in the target file
- If total extracted LOC > 50 lines: MANDATORY -- use Python extractor script:
  `python scripts/v12_split.py --source [file] --method [method]`
  Manual copy-paste for splits > 50 lines is BANNED per V12 DNA.

### Extraction Rules
- New sub-methods go in the SAME partial class file UNLESS it would exceed 1200 LOC
- If file would exceed 1200 LOC: create a new partial file (e.g., V12_002.UI.Callbacks.KeyHandlers.cs)
- Sub-methods are `private void` unless state return is required, then `private [type]`
- Target method becomes a pure dispatcher: reads state, calls sub-methods, no inline logic > 5 lines
- PascalCase method names, camelCase locals -- no dense one-liners
- NEVER mutate whitespace or indentation in untouched lines
- Touch ONLY the lines being extracted + the new sub-method bodies + the call sites

### DNA Compliance During Execution
- ZERO new lock() statements
- ZERO non-ASCII characters in string literals
- ZERO diff markers in tool calls (no <<<<<<, =======, >>>>>>>)
- All state mutations must use existing FSM/Enqueue patterns
- DO NOT optimize or improve logic during extraction -- pure structural movement only

---

## STEP 5 -- POST-EDIT DNA AUDIT (mandatory after every src/ edit)

Run these commands in sequence and report ALL results:

```powershell
# 5a: Re-establish hard links and run ASCII gate (MANDATORY)
powershell -File .\deploy-sync.ps1

# 5b: Complexity verification
python scripts/complexity_audit.py

# 5c: Lock regression (must return ZERO matches)
grep -r "lock(" src/

# 5d: Unicode regression (must return ZERO matches)
grep -Prn "[^\x00-\x7F]" src/
```

Report format to Director:
```
[TICKET-AUDIT]
Ticket: $1
Target method: [name]
CYC before: [N]
CYC after (estimated): [N]
Sub-methods created: [list]
deploy-sync.ps1: PASS / FAIL
lock() audit: CLEAN / [N matches]
Unicode audit: CLEAN / [N matches]
```

**If ANY audit fails: HALT. Report failure. Do NOT report completion.**

---

## STEP 6 -- HANDOFF TO DIRECTOR

Only after ALL Step 5 audits PASS, output:

```
[TICKET-COMPLETE]
Ticket: $1
Status: READY FOR F5 COMPILE
Files modified: [list]
Sub-methods created: [list with LOC]
CYC reduction: [before] -> [after]
Action required: Press F5 in NinjaTrader IDE to compile and verify BUILD_TAG banner.
Next ticket: [suggest next ticket from EXECUTION_GUIDE.md if applicable]
```
