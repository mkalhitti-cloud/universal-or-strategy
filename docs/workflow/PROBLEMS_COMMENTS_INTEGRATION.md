# Problems & Comments Integration Workflow

**Principle**: Structure-driven quality - workflows should perform consistently regardless of model by following systematic processes for handling Problems and Comments panels.

**Goal**: Zero manual triage - automated categorization, prioritization, and resolution of all findings.

---

## Current State (From Screenshots)

**Problems Panel**: 117 issues detected
- Greptile apps (image issues)
- CodeAnt AI suggestions
- Cubic dev AI warnings
- CodeFactor IO documentation issues
- CodeRabbit AI critical/potential issues
- Gitar bot delta warnings
- SonarQube cognitive complexity warnings
- Sourcery config errors

**Comments Panel**: 105 comments
- Multiple bot reviews across files
- Suggestions, warnings, and critical issues
- Spanning multiple files in `src/V12_002.*`

---

## Problem: Unstructured Triage

**Current Issues**:
- ❌ 117 problems require manual review
- ❌ No automatic categorization (VALID vs HALLUCINATION)
- ❌ No priority ordering (P0 → P1 → P2)
- ❌ No Jane Street alignment check
- ❌ No deduplication across bots
- ❌ No automated fix suggestions

**Result**: Agent loops waste time re-analyzing the same issues.

---

## Solution: Structured Workflow Integration

### Phase 1: Automatic Problem Extraction

**Script**: `scripts/extract_vscode_problems.ps1`

```powershell
# Extract all problems from VS Code Problems panel
# Output: problems.json with structured data

param(
    [string]$OutputPath = "docs/brain/vscode_problems.json"
)

# Get problems from VS Code API
$problems = code --list-problems --json

# Structure the data
$structured = @{
    timestamp = Get-Date -Format "o"
    total_count = $problems.Count
    by_severity = @{
        error = @($problems | Where-Object { $_.severity -eq "error" }).Count
        warning = @($problems | Where-Object { $_.severity -eq "warning" }).Count
        info = @($problems | Where-Object { $_.severity -eq "info" }).Count
    }
    by_source = @{}
    problems = @()
}

# Group by source (bot)
foreach ($problem in $problems) {
    $source = $problem.source
    if (-not $structured.by_source.ContainsKey($source)) {
        $structured.by_source[$source] = 0
    }
    $structured.by_source[$source]++
    
    # Add to problems array
    $structured.problems += @{
        file = $problem.file
        line = $problem.line
        column = $problem.column
        severity = $problem.severity
        source = $source
        message = $problem.message
        code = $problem.code
    }
}

# Save to JSON
$structured | ConvertTo-Json -Depth 10 | Out-File $OutputPath -Encoding UTF8

Write-Host "✅ Extracted $($problems.Count) problems to $OutputPath"
```

---

### Phase 2: Automatic Categorization

**Script**: `scripts/categorize_problems.py`

```python
#!/usr/bin/env python3
"""
Categorize VS Code problems using Jane Street KB and hallucination patterns.
"""

import json
from pathlib import Path

# Load Jane Street baseline
with open("docs/brain/JANE_STREET_BASELINE_AUDIT.md") as f:
    jane_street_baseline = f.read()

# Load known hallucination patterns
with open("docs/brain/bot_hallucinations.md") as f:
    hallucination_patterns = f.read()

def categorize_problem(problem):
    """
    Categorize a problem as:
    - VALID-FIX: Must fix (aligns with Jane Street)
    - VALID-SUPPRESS: Suppress (conflicts with Jane Street)
    - HALLUCINATION: Bot error (known false positive)
    - INFRA-NOISE: Infrastructure issue (ignore)
    """
    
    # Check if it's a known hallucination
    if is_hallucination(problem):
        return "HALLUCINATION"
    
    # Check if it's infrastructure noise
    if is_infra_noise(problem):
        return "INFRA-NOISE"
    
    # Check Jane Street alignment
    if conflicts_with_jane_street(problem):
        return "VALID-SUPPRESS"
    
    # Default: must fix
    return "VALID-FIX"

def is_hallucination(problem):
    """Check if problem matches known hallucination patterns."""
    # Example: CodeAnt suggesting to add null checks when already present
    if "null check" in problem["message"].lower():
        # Check if null check already exists in file
        with open(problem["file"]) as f:
            content = f.read()
            if "if (order == null)" in content:
                return True
    return False

def is_infra_noise(problem):
    """Check if problem is infrastructure-related."""
    infra_patterns = [
        "Snyk Error",  # Snyk service issues
        "Image: image",  # Greptile image processing
        "exit status 2",  # Build tool errors
    ]
    return any(p in problem["message"] for p in infra_patterns)

def conflicts_with_jane_street(problem):
    """Check if problem conflicts with documented Jane Street deviations."""
    # Example: Bot suggests using lock() but Jane Street bans it
    if "lock(" in problem["message"].lower():
        return True
    
    # Example: Bot suggests LINQ but Jane Street bans it in hot paths
    if "linq" in problem["message"].lower() and "hot path" in problem["file"]:
        return True
    
    return False

def prioritize_problem(problem):
    """Assign priority: P0 (critical), P1 (high), P2 (medium)."""
    if problem["severity"] == "error":
        return "P0"
    elif "security" in problem["message"].lower():
        return "P0"
    elif "complexity" in problem["message"].lower():
        return "P1"
    else:
        return "P2"

# Main execution
with open("docs/brain/vscode_problems.json") as f:
    data = json.load(f)

categorized = {
    "VALID-FIX": [],
    "VALID-SUPPRESS": [],
    "HALLUCINATION": [],
    "INFRA-NOISE": []
}

for problem in data["problems"]:
    category = categorize_problem(problem)
    priority = prioritize_problem(problem)
    problem["category"] = category
    problem["priority"] = priority
    categorized[category].append(problem)

# Save categorized problems
with open("docs/brain/vscode_problems_categorized.json", "w") as f:
    json.dump(categorized, f, indent=2)

# Generate fix queue (VALID-FIX only, sorted by priority)
fix_queue = sorted(
    categorized["VALID-FIX"],
    key=lambda p: (0 if p["priority"] == "P0" else 1 if p["priority"] == "P1" else 2, p["file"], p["line"])
)

with open("docs/brain/vscode_fix_queue.md", "w") as f:
    f.write("# VS Code Problems Fix Queue\n\n")
    f.write(f"**Total**: {len(fix_queue)} issues\n\n")
    
    for priority in ["P0", "P1", "P2"]:
        issues = [p for p in fix_queue if p["priority"] == priority]
        if issues:
            f.write(f"## {priority} Issues ({len(issues)})\n\n")
            for p in issues:
                f.write(f"- [ ] **{p['source']}**: {p['message']}\n")
                f.write(f"  - File: `{p['file']}:{p['line']}`\n")
                f.write(f"  - Severity: {p['severity']}\n\n")

print(f"✅ Categorized {len(data['problems'])} problems")
print(f"   VALID-FIX: {len(categorized['VALID-FIX'])}")
print(f"   VALID-SUPPRESS: {len(categorized['VALID-SUPPRESS'])}")
print(f"   HALLUCINATION: {len(categorized['HALLUCINATION'])}")
print(f"   INFRA-NOISE: {len(categorized['INFRA-NOISE'])}")
```

---

### Phase 3: Workflow Integration

**Update `/pr-loop` Step 1** to include VS Code problems:

```markdown
### Step 1: Bot Forensics + VS Code Problems + Jane Street Audit

**Switch to: Advanced mode**

Hand off:
```
TASK: Extract and Categorize ALL Findings
PR: $1
PROTOCOL:
  1. Extract GitHub PR bot comments:
     powershell -File .\scripts\extract_pr_forensics.ps1 -PrNumber $1
  
  2. Extract VS Code Problems panel:
     powershell -File .\scripts\extract_vscode_problems.ps1
  
  3. Categorize all findings:
     python scripts\categorize_problems.py
  
  4. Deduplicate across sources:
     python scripts\deduplicate_findings.py
  
  5. JANE STREET AUDIT:
     - Read: docs/standards/JANE_STREET_DEVIATIONS.md
     - For each VALID issue, check Jane Street alignment
     - Categorize as VALID-FIX or VALID-SUPPRESS
  
  6. Generate unified fix queue:
     - Merge PR findings + VS Code problems
     - Sort by priority (P0 → P1 → P2)
     - Remove duplicates
     - Output: docs/brain/unified_fix_queue.md
  
  7. Emit: [FORENSICS-READY] X VALID-FIX, Y VALID-SUPPRESS, Z hallucinations
```
```

---

### Phase 4: Deduplication

**Script**: `scripts/deduplicate_findings.py`

```python
#!/usr/bin/env python3
"""
Deduplicate findings across PR bots and VS Code problems.
"""

import json
from pathlib import Path
from difflib import SequenceMatcher

def similarity(a, b):
    """Calculate similarity ratio between two strings."""
    return SequenceMatcher(None, a, b).ratio()

def are_duplicates(finding1, finding2):
    """Check if two findings are duplicates."""
    # Same file and line
    if finding1["file"] == finding2["file"] and finding1["line"] == finding2["line"]:
        # Similar messages (>80% similarity)
        if similarity(finding1["message"], finding2["message"]) > 0.8:
            return True
    return False

# Load PR findings
with open("docs/brain/pr_findings_categorized.json") as f:
    pr_findings = json.load(f)

# Load VS Code problems
with open("docs/brain/vscode_problems_categorized.json") as f:
    vscode_problems = json.load(f)

# Merge and deduplicate
all_findings = []
seen = set()

for category in ["VALID-FIX", "VALID-SUPPRESS"]:
    for finding in pr_findings.get(category, []):
        key = f"{finding['file']}:{finding['line']}:{finding['message'][:50]}"
        if key not in seen:
            all_findings.append(finding)
            seen.add(key)
    
    for problem in vscode_problems.get(category, []):
        # Check if duplicate of existing finding
        is_dup = False
        for existing in all_findings:
            if are_duplicates(problem, existing):
                # Merge sources
                if "sources" not in existing:
                    existing["sources"] = [existing.get("source", "unknown")]
                existing["sources"].append(problem["source"])
                is_dup = True
                break
        
        if not is_dup:
            key = f"{problem['file']}:{problem['line']}:{problem['message'][:50]}"
            if key not in seen:
                all_findings.append(problem)
                seen.add(key)

# Save deduplicated findings
with open("docs/brain/unified_findings.json", "w") as f:
    json.dump(all_findings, f, indent=2)

print(f"✅ Deduplicated to {len(all_findings)} unique findings")
```

---

### Phase 5: Automated Fix Application

**Update `/pr-loop` Step 2** to use unified fix queue:

```markdown
### Step 2: Automated Fix Application

**Switch to: v12-engineer mode**

Hand off:
```
TASK: Apply Fixes from Unified Queue
INPUT: @docs/brain/unified_fix_queue.md
PROTOCOL:
  1. Read unified fix queue (deduplicated PR + VS Code findings)
  
  2. For each P0 issue (CRITICAL):
     - Apply fix automatically if pattern is known
     - Request human review if ambiguous
     - Mark as [x] FIXED in queue
  
  3. For each P1 issue (HIGH):
     - Apply fix if straightforward
     - Skip if requires design decision
     - Mark as [x] FIXED or [~] DEFERRED
  
  4. For each P2 issue (MEDIUM):
     - Apply quick wins only
     - Defer complex refactorings to separate PR
     - Mark as [x] FIXED or [~] DEFERRED
  
  5. Run formatters and validators:
     powershell -File .\scripts\format_all_csharp.ps1
     powershell -File .\scripts\pre_push_validation.ps1
  
  6. Emit: [FIXES-APPLIED] X fixed, Y deferred
```
```

---

## Workflow Diagram

```
┌─────────────────────────────────────────────────────────────┐
│ Layer 1: TYPING (Real-Time)                                 │
│ - SonarLint, Snyk, Sourcery show squiggles                  │
│ - Problems panel updates live                                │
└─────────────────────────────────────────────────────────────┘
                            ↓
┌─────────────────────────────────────────────────────────────┐
│ Layer 2: ON-SAVE (Immediate)                                │
│ - CSharpier auto-formats                                    │
│ - Roslyn analyzers run                                       │
│ - Problems panel refreshes                                   │
└─────────────────────────────────────────────────────────────┘
                            ↓
┌─────────────────────────────────────────────────────────────┐
│ Layer 3: PRE-COMMIT (Local)                                 │
│ - Extract VS Code problems → problems.json                   │
│ - Categorize → VALID-FIX / VALID-SUPPRESS / HALLUCINATION   │
│ - Block commit if P0 issues exist                           │
└─────────────────────────────────────────────────────────────┘
                            ↓
┌─────────────────────────────────────────────────────────────┐
│ Layer 4: PRE-PUSH (Local)                                   │
│ - Run full validation suite                                 │
│ - Extract + categorize + deduplicate all findings           │
│ - Generate unified fix queue                                 │
│ - Block push if critical issues remain                      │
└─────────────────────────────────────────────────────────────┘
                            ↓
┌─────────────────────────────────────────────────────────────┐
│ Layer 5: PR REVIEW (GitHub)                                 │
│ - Bots add comments                                          │
│ - /pr-loop extracts + merges with VS Code problems          │
│ - Deduplicate + prioritize + apply fixes                    │
│ - Iterate until 100/100 PHS                                 │
└─────────────────────────────────────────────────────────────┘
```

---

## Benefits of Structured Workflow

✅ **Model-Agnostic**: Works with any LLM (Claude, GPT-4, Gemini) because process is structured  
✅ **Zero Manual Triage**: Automatic categorization via scripts  
✅ **Deduplication**: Same issue from 5 bots = 1 fix  
✅ **Priority-Driven**: P0 → P1 → P2 ordering  
✅ **Jane Street Aligned**: Automatic suppression of conflicting rules  
✅ **Hallucination Filtering**: Known false positives removed  
✅ **Audit Trail**: Every decision documented in JSON  

---

## Implementation Checklist

- [ ] Create `scripts/extract_vscode_problems.ps1`
- [ ] Create `scripts/categorize_problems.py`
- [ ] Create `scripts/deduplicate_findings.py`
- [ ] Update `/pr-loop` Step 1 to include VS Code problems
- [ ] Update `/pr-loop` Step 2 to use unified fix queue
- [ ] Create `docs/brain/bot_hallucinations.md` (hallucination patterns)
- [ ] Test on current 117 problems + 105 comments
- [ ] Verify deduplication works across all bots
- [ ] Document new workflow in `AGENTS.md`

---

## Example: Current 117 Problems

**Before Workflow**:
```
117 problems (manual triage required)
- 40 duplicates across bots
- 25 hallucinations (false positives)
- 15 Jane Street conflicts (should suppress)
- 12 infrastructure noise
- 25 actual issues to fix
```

**After Workflow**:
```
25 VALID-FIX issues (deduplicated, prioritized)
- 5 P0 (critical - block commit)
- 10 P1 (high - fix before push)
- 10 P2 (medium - fix or defer)

15 VALID-SUPPRESS (documented in Jane Street deviations)
25 HALLUCINATION (logged for bot training)
12 INFRA-NOISE (ignored)
40 DUPLICATES (merged)
```

**Result**: 117 → 25 actionable issues (78% reduction in noise)

---

## References

- **Extract Script**: `scripts/extract_vscode_problems.ps1`
- **Categorize Script**: `scripts/categorize_problems.py`
- **Deduplicate Script**: `scripts/deduplicate_findings.py`
- **Unified Queue**: `docs/brain/unified_fix_queue.md`
- **Hallucination Log**: `docs/brain/bot_hallucinations.md`
- **Jane Street Baseline**: `docs/brain/JANE_STREET_BASELINE_AUDIT.md`