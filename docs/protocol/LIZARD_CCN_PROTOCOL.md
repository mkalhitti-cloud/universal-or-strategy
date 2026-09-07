<!-- docs/protocol/LIZARD_CCN_PROTOCOL.md -->
# Lizard CCN Measurement Protocol (V1.0)

## Status: P0 MANDATORY — all agents, all phases, all waves

---

## The Incident That Created This Protocol

During Wave 1 (Lanes A, B, C) the planning phase reported methods with
"CCN" values of 103, 87, 80, 79, 66... These were used to select
extraction targets and size the work.

Every single one of those numbers was NLOC (non-comment lines of code),
not CCN (cyclomatic complexity). The lizard CSV column header mapping was
wrong. CCN was being read as column 1 when it is actually column 2.

Result: 3 pipeline lanes ran against targets that were already CCN <=8.
No CCN compliance work was needed. 160 tests were added (real value) but
the refactoring justification was built on bad data.

The correct CCN picture after Wave 1: 2 methods at CCN=9. That is the
entire debt for the whole PropTraderTools codebase.

---

## Root Cause

Lizard CSV output has NO header row. When piped through
`ConvertFrom-Csv` you must supply headers manually. The wrong header
mapping was used:

```powershell
# WRONG -- what was used in Wave 1 planning
-Header CCN,Token,Lines,Params,CCN2,NLOC,...
#         ^--- col 1 is NOT CCN. It is NLOC.
```

The correct lizard CSV column order is:

```
Col 1:  NLOC     (non-comment lines of code)
Col 2:  CCN      (cyclomatic complexity)  <-- this is what you want
Col 3:  Token    (token count)
Col 4:  Params   (parameter count)
Col 5:  Length   (total lines including blanks/comments)
Col 6:  Location (function@startline-endline@file)
Col 7:  File     (full file path)
Col 8:  Function (ClassName::MethodName)
Col 9:  Sig      (full qualified signature)
Col 10: Start    (start line number)
Col 11: End      (end line number)
```

---

## Canonical Commands — Use These Verbatim, Never Modify Headers

### 1. Find all methods with CCN > 8 (the JS-080 gate)

```powershell
lizard src/PropTraderTools/ -x "*/bin/*" -x "*/obj/*" -x "*Tests*" --csv |
ConvertFrom-Csv -Header NLOC,CCN,Token,Params,Length,Location,File,Function,Sig,Start,End |
Where-Object {[int]$_.CCN -gt 8} |
Sort-Object {[int]$_.CCN} -Descending |
Select-Object CCN, Function, @{L="File";E={[IO.Path]::GetFileName($_.File)}} |
Format-Table -AutoSize
```

Expected output when fully compliant: **no rows**.

### 2. Summary by file (violations count + max CCN per file)

```powershell
lizard src/PropTraderTools/ -x "*/bin/*" -x "*/obj/*" -x "*Tests*" --csv |
ConvertFrom-Csv -Header NLOC,CCN,Token,Params,Length,Location,File,Function,Sig,Start,End |
Where-Object {[int]$_.CCN -gt 8} |
Group-Object {[IO.Path]::GetFileName($_.File)} |
ForEach-Object {
    [PSCustomObject]@{
        File           = $_.Name
        ViolationCount = $_.Count
        MaxCCN         = ($_.Group | ForEach-Object {[int]$_.CCN} | Measure-Object -Max).Maximum
    }
} |
Sort-Object MaxCCN -Descending |
Format-Table -AutoSize
```

### 3. Verify a single file (text mode -- most readable)

```powershell
lizard src\PropTraderTools\CopyEngine.cs
```

Text mode output column order is: NLOC, CCN, Token, Param, Length, location
Read left to right: the SECOND number is CCN.

Example line:
```
      20      9    131      2      24 TrimSignal::IsExitSignalName@2347-2370
```
That method has NLOC=20, CCN=9.

### 4. Verify specific method by name

```powershell
lizard src\PropTraderTools\CopyEngine.cs |
Select-String "IsExitSignalName|HasArmingAtmBrackets"
```

---

## Verification Rule for Pipelines

Every ptt-architect Ph1 scope audit MUST:

1. Run command #1 above (CCN > 8 query)
2. Report the output verbatim in 02-architecture-plan.md
3. Label columns explicitly: CCN | Function | File
4. If any number looks implausibly large (>20 for a short method),
   cross-check with command #3 (single file text mode) before using
   the number in any ticket

The Ph2 ptt-plan-reviewer MUST:

1. Verify that the CCN values in 02-architecture-plan.md were produced
   with the correct NLOC,CCN,Token,... header mapping
2. If any value looks like a line count (>30 for methods described as
   "simple"), flag as potential NLOC/CCN mislabel and demand re-run
3. REVIEW_PASS is blocked if CCN values are not independently verified

---

## Sanity Check Table

These relationships always hold. If your output violates them, your
column mapping is wrong:

| Observation | Meaning |
|-------------|---------|
| "CCN" value > 50 for a method under 30 lines | Almost certainly NLOC, not CCN |
| "CCN" value exactly equals line count | Definite NLOC/CCN swap |
| All methods show "CCN" clustered around 50-110 | Reading NLOC column |
| Text-mode CCN (col 2) != CSV "CCN" (col 1) | Wrong CSV header |
| Average "CCN" across file > 20 | Suspect -- real average is usually 3-6 |

---

## Where This Is Enforced

| Location | What it says |
|----------|-------------|
| This file | Canonical commands + incident record |
| `.bob/rules-v12-engineer/dna.md` | CCN section references this file |
| `docs/protocol/COMPLEXITY_REDUCTION_PROTOCOL.md` | Updated to reference this file |
| `.bob/skills/ptt-architect/SKILL.md` | CCN audit step uses canonical command |
| `.bob/skills/ptt-plan-reviewer/SKILL.md` | Review gate checks for mislabel |
| PTT pipeline prompt templates | Canonical command embedded verbatim |

---

## Version History

| Version | Date | Change |
|---------|------|--------|
| V1.0 | 2026-09 | Created after Wave 1 NLOC/CCN mislabel incident |

---

**Effective**: immediately
**Authority**: Director mandate
**Supersedes**: any lizard command not using the NLOC,CCN,... header order
