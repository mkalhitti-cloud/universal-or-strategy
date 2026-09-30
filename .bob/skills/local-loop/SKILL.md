---
name: local-loop
description: >-
  Local Quality Loop (Pre-Push MCP Review). Runs CodeAnt and complexity audit
  locally before pushing to GitHub, achieving 5/5 scores in ~8 minutes with
  zero API cost. Use when the user runs /local-loop, asks to run local quality
  checks, wants to validate code before pushing, or is in Phase 5 ticket execution.
metadata:
  user-invocable: true
  disable-model-invocation: true
---

# /local-loop - Local Quality Loop (Pre-Push MCP Review)

## Purpose
Execute local MCP-based quality checks (CodeAnt + complexity audit) BEFORE pushing to GitHub. This loop runs entirely locally with zero API costs and completes in ~8 minutes.

## Position in Triple-Nested Architecture
```
/autonomous-refactor (Outer - Master Orchestrator)
  └─ /epic-run (Middle - Single epic, 6 phases)
      └─ Phase 5: Ticket Execution
          ├─ /ticket (Engineer implementation)
          ├─ F5 Gate (Human verification - ONLY manual step)
          ├─ /local-loop ← YOU ARE HERE (8 min, local only)
          ├─ /mcp-loop (3 min, MCP servers)
          ├─ Pre-Push Validation (2 min, fast mode)
          ├─ git commit (local only)
          └─ AFTER ALL EPICS: git push → /pr-loop (GitHub bots)
```

**CRITICAL**: Both `/local-loop` and `/mcp-loop` run BEFORE any git push. They are pre-push quality gates.

## What This Loop Does

### 1. CodeAnt Review (C# Only)
**Tool**: CodeAnt CLI + MCP
**Target**: All modified `.cs` files
**Checks**:
- Code quality issues
- Security vulnerabilities
- Style violations
- Dead code detection
- Complexity issues

**Command**:
```bash
codeant review
```

**Output**: Priority-based issues (MINOR, MAJOR, CRITICAL)

### 2. Complexity Audit
**Tool**: `scripts/complexity_audit.py`
**Target**: All modified `.cs` files
**Threshold**: CYC ≤ 15 (Jane Street aligned)

**Command**:
```bash
python scripts/complexity_audit.py
```

**Output**: Methods exceeding CYC 15

### 3. Dead Code Scan
**Tool**: `scripts/dead_code_scan.py`
**Target**: All modified `.cs` files

**Command**:
```bash
python scripts/dead_code_scan.py
```

**Output**: Unreferenced methods/classes

## Loop Protocol

### Entry Conditions
- ✅ F5 Gate passed (BUILD_TAG verified in NinjaTrader)
- ✅ All changes committed locally
- ❌ NOT yet pushed to GitHub

### Exit Conditions
**SUCCESS** (proceed to `/mcp-loop`):
- ✅ CodeAnt: Zero issues (ALL severity levels) OR documented exceptions only
- ✅ Complexity: All methods ≤ 15 CYC (Jane Street aligned)
- ✅ Dead Code: Zero new dead code

**FAILURE** (fix and retry):
- ❌ CodeAnt: ANY issues found (CRITICAL, MAJOR, or MINOR)
- ❌ Complexity: Methods > 15 CYC
- ❌ Dead Code: New dead code detected

**Exception Protocol** (for CodeAnt issues):
1. **Jane Street Suppression**: Query KB, verify pattern is intentional
2. **Director Override**: Manual approval with documentation in `.codeantignore`
3. **Tool Hallucination**: Verified false positive, add to `.codeantignore`

**Target**: 5/5 clean score (zero issues) unless explicitly suppressed

### Loop Logic
```
1. Run CodeAnt review
   ├─ CRITICAL issues? → FIX → Restart loop
   └─ Clean? → Continue

2. Run complexity audit
   ├─ Methods > 15 CYC? → REFACTOR → Restart loop
   └─ Clean? → Continue

3. Run dead code scan
   ├─ Dead code found? → REMOVE → Restart loop
   └─ Clean? → EXIT SUCCESS

4. Emit: [LOCAL-LOOP-PASS] → Proceed to /mcp-loop
```

## Autonomous Execution

### Step 1: CodeAnt Review
```bash
# Run CodeAnt on modified C# files
codeant review

# Parse output
if [[ $? -ne 0 ]]; then
  echo "[LOCAL-LOOP] CodeAnt CRITICAL issues detected"
  # Extract issues
  # Apply fixes autonomously
  # Commit fixes
  # Restart loop
fi
```

### Step 2: Complexity Audit
```bash
# Run complexity audit
python scripts/complexity_audit.py

# Parse output
if grep -q "exceeds threshold" complexity_report.txt; then
  echo "[LOCAL-LOOP] Complexity violations detected"
  # Extract violating methods
  # Delegate to /extract for refactoring
  # Commit fixes
  # Restart loop
fi
```

### Step 3: Dead Code Scan
```bash
# Run dead code scan
python scripts/dead_code_scan.py

# Parse output
if grep -q "unreferenced" dead_code_report.txt; then
  echo "[LOCAL-LOOP] Dead code detected"
  # Extract dead code list
  # Remove dead code
  # Commit fixes
  # Restart loop
fi
```

### Step 4: Success Exit
```bash
echo "[LOCAL-LOOP-PASS] All local checks passed"
echo "Duration: $(($SECONDS / 60)) minutes"
echo "Next: /mcp-loop"
```

## Integration with Batch Commit Strategy

For the CCN run (157 epics), `/local-loop` runs ONCE per epic:

```
Epic N:
  1. /ticket (Engineer execution)
  2. F5 Gate (Human verification)
  3. /local-loop ← Autonomous fix loop (8 min, 5/5 target)
  4. /mcp-loop ← Semantic review (3 min, 5/5 target)
  5. Pre-Push Validation (2 min, fast mode)
  6. git commit (local only, no push)
  7. Continue to Epic N+1

After all 157 epics:
  1. git push (batch push to GitButler virtual branch)
  2. Create batch PRs (~10-15 PRs)
  3. /pr-loop (on each batch PR → 100/100 PHS)
```

## Time Savings

**Per-Epic Cost**:
- `/local-loop`: 8 minutes (local, zero API cost)
- Skip `/mcp-loop` per epic (save 3 min × 157 = 7.85 hours)
- Skip `/pr-loop` per epic (save 15 min × 157 = 39.25 hours)

**Total Savings**: 47.1 hours (vs per-epic PR review)

## Error Recovery

### CodeAnt Failures
**Symptom**: CRITICAL issues persist after 3 fix attempts
**Action**:
1. STOP loop
2. Escalate to Director
3. Manual review required
4. Document in `docs/brain/EPIC-X/local-loop-failure.md`

### Complexity Failures
**Symptom**: Method still > 15 CYC after extraction
**Action**:
1. STOP loop
2. Delegate to `/extract` with stricter target (CYC ≤ 10)
3. Restart loop after extraction

### Dead Code Failures
**Symptom**: Dead code removal breaks tests
**Action**:
1. STOP loop
2. Revert dead code removal
3. Mark as "false positive" in `.deadcodeignore`
4. Continue loop

## Success Metrics

**Target**: 95% first-pass success rate
**Current**: TBD (first run pending)

**Tracking**:
- Success rate per epic
- Average loop iterations
- Most common failure modes
- Time per iteration

## Configuration

### CodeAnt Config (`.codeantignore`)
```
# Scan only C# files
!*.cs

# Exclude generated files
**/obj/**
**/bin/**
```

### Complexity Threshold (`scripts/complexity_audit.py`)
```python
THRESHOLD = 15  # Jane Street aligned
```

### Dead Code Exclusions (`.deadcodeignore`)
```
# False positives (used via reflection)
V12_002.UI.*.cs:*Handler
```

## MCP Integration

**CodeAnt MCP** (`.bob/mcp.json`):
```json
{
  "mcpServers": {
    "codeant": {
      "command": "npx",
      "args": ["-y", "any-cli-mcp-server", "codeant"],
      "disabled": false
    }
  }
}
```

**Usage in Bob IDE**:
```
Ask Bob: "Run /local-loop on current changes"
```

## Next Steps

1. ✅ Document `/local-loop` protocol
2. ⏳ Create `/mcp-loop` command (jcodemunch + Cubic)
3. ⏳ Test on EPIC-51 (first validation)
4. ⏳ Integrate into `/epic-run` Phase 5
5. ⏳ Update `BATCH_COMMIT_STRATEGY.md` with loop details

---

**Status**: Protocol defined, awaiting first execution
**Owner**: V12 Orchestrator
**Last Updated**: 2026-06-08
