# Local Execution Protocol (V12.35)

**Version**: 1.0  
**Effective**: 2026-06-16  
**Status**: MANDATORY for encoding-sensitive files  
**Supersedes**: Standard VM execution for special cases

## Purpose

This protocol defines when and how to execute epics **locally** instead of on the VM, specifically for **encoding-sensitive files** that are prone to UTF-16 corruption during remote execution.

## Core Principle: Encoding Preservation

Some files are **encoding-sensitive** and MUST be edited locally to prevent UTF-16 corruption that occurs during VM execution.

## When to Use Local Execution

### Pattern Detection

Use local execution when the target file matches ANY of these patterns:

1. **DrawingHelpers Pattern**: `*DrawingHelpers.cs`
   - Example: `V12_002.DrawingHelpers.cs`
   - Reason: High UTF-16 corruption rate on VM

2. **Future Patterns** (to be added as discovered):
   - `*GraphicsHelpers.cs`
   - `*RenderingHelpers.cs`
   - Any file with known encoding issues

### Pre-Flight Detection

Run pre-flight validation to detect encoding-sensitive epics:

```bash
python scripts/preflight_validation.py
```

**Output Example**:
```
Special Cases Detected:
- EPIC-CCN-024: encoding-sensitive (V12_002.DrawingHelpers.cs)
  → Execute locally with encoding verification
```

## Local Execution Process

### Step 1: Environment Setup

**Prerequisites**:
- Bob CLI installed locally (`bob --version`)
- Git status clean (no uncommitted changes)
- GitButler virtual branch active
- Encoding verification script available

**Verify Environment**:
```powershell
# Check Bob CLI
bob --version

# Check git status
git status

# Check encoding script
Test-Path .\scripts\check_encoding.ps1
```

---

### Step 2: Execute Epic Locally

**Command Pattern**:
```bash
bob --yolo --mode v12-engineer "Execute EPIC-CCN-XXX Phase 5 Ticket 1"
```

**Key Flags**:
- `--yolo`: Enable file persistence in SSH mode
- `--mode v12-engineer`: Use V12 Photon Engineer mode
- Include full ticket instructions in prompt

**Example**:
```bash
bob --yolo --mode v12-engineer "Execute EPIC-CCN-024 Phase 5 Ticket 1:
Extract DrawingHelpers complexity from DrawLine method.
Target: V12_002.DrawingHelpers.cs::DrawLine
CYC: 15 → ≤8
SURGICAL ONLY - touch only target method."
```

---

### Step 3: Encoding Verification (MANDATORY)

**After EVERY change**, verify encoding:

```powershell
powershell -File .\scripts\check_encoding.ps1
```

**Success Criteria**:
- Exit code: 0
- All files: UTF-8 without BOM
- Zero UTF-16 violations

**If Violations Found**:
```powershell
# Auto-fix encoding
powershell -File .\scripts\check_encoding.ps1 -Fix

# Re-verify
powershell -File .\scripts\check_encoding.ps1
```

---

### Step 4: Manual Commit

**DO NOT use automated commit**. Manually commit with verification:

```bash
# Stage changes
git add src/V12_002.DrawingHelpers.cs

# Verify encoding one more time
powershell -File .\scripts\check_encoding.ps1

# Commit with detailed message
git commit -m "feat(EPIC-CCN-024): Extract DrawLine complexity

- Extracted: ValidateCoordinates, CalculateSlope, DrawSegment
- CYC: 15 → 6 (60% reduction)
- Encoding: UTF-8 without BOM verified
- Tests: 3 xUnit tests generated and passing

Ticket: EPIC-CCN-024-T1
Execution: Local (encoding-sensitive file)"
```

---

### Step 5: Verification

Run Phase 5.V verification locally:

```powershell
# Check 1: Compilation
dotnet build

# Check 2: Complexity
python scripts/complexity_audit.py

# Check 3: Scope
git diff HEAD~1 --stat

# Check 4: Tests
dotnet test

# Check 5: Encoding
powershell -File .\scripts\check_encoding.ps1
```

**Document Results**:
Create `docs/brain/EPIC-CCN-XXX/06-verification-report.md` with all 5 check results.

---

### Step 6: Sync to VM (Optional)

If VM needs to be updated:

```powershell
# Sync local changes to VM
powershell -File .\deploy-sync.ps1
```

**Note**: This is optional for local-only epics. VM sync is only needed if other epics depend on this change.

## Checklist: Local Execution

### Pre-Execution
- [ ] Pre-flight validation identified encoding-sensitive epic
- [ ] Bob CLI available locally
- [ ] Git status clean
- [ ] GitButler virtual branch active
- [ ] Encoding verification script available

### During Execution
- [ ] Execute with `bob --yolo --mode v12-engineer`
- [ ] Verify encoding after EVERY change
- [ ] Fix encoding violations immediately
- [ ] Manual commit (not automated)

### Post-Execution
- [ ] Run all 5 verification checks
- [ ] Document results in verification report
- [ ] Update epic status in roadmap
- [ ] Sync to VM if needed

## Comparison: VM vs Local Execution

| Aspect | VM Execution | Local Execution |
|--------|--------------|-----------------|
| **Speed** | Parallel (80 epics) | Sequential (1 at a time) |
| **Encoding** | UTF-16 risk | UTF-8 preserved |
| **Automation** | Fully automated | Manual commit required |
| **Use Case** | Standard epics | Encoding-sensitive only |
| **Cost** | $0.05/epic | $0.05/epic (same) |
| **Verification** | Automated | Manual |

## Known Encoding-Sensitive Files

### Current List (V12.35)

1. **V12_002.DrawingHelpers.cs** (EPIC-CCN-024)
   - Pattern: `*DrawingHelpers.cs`
   - Issue: UTF-16 corruption during VM execution
   - Solution: Local execution with encoding verification

### Future Additions

Add files here as encoding issues are discovered:

```
# Template:
# - File: <filename>
# - Pattern: <glob pattern>
# - Issue: <description>
# - Epic: EPIC-CCN-XXX
```

## Failure Scenarios

### Scenario 1: UTF-16 Corruption Detected

**Symptoms**: `check_encoding.ps1` exits with code 1

**Recovery**:
```powershell
# Auto-fix
powershell -File .\scripts\check_encoding.ps1 -Fix

# Re-verify
powershell -File .\scripts\check_encoding.ps1

# If still fails, manual inspection required
```

---

### Scenario 2: Bob CLI Not Available Locally

**Symptoms**: `bob: command not found`

**Recovery**:
1. Install Bob CLI: Follow installation instructions
2. Verify: `bob --version`
3. Retry execution

---

### Scenario 3: Git Status Not Clean

**Symptoms**: Uncommitted changes in working directory

**Recovery**:
1. Stash changes: `git stash`
2. Execute epic
3. Restore stash: `git stash pop`

## Integration with Other Protocols

- **Pre-Flight Validation**: Detects encoding-sensitive epics
- **Phase 5 Execution**: Applies SURGICAL ONLY mandate
- **Phase 5.V Verification**: Runs 5-check protocol
- **Building-Blocks Method**: Uses local execution templates

## Success Metrics

**Wave 4 Baseline** (no local execution):
- EPIC-CCN-024: UTF-16 corruption on VM
- Required manual fix and re-execution
- Wasted time: ~30 minutes

**Wave 5 Target** (with local execution):
- EPIC-CCN-024: Execute locally from start
- Zero encoding violations
- Zero re-execution needed

## Cost Analysis

**Local vs VM Execution**:
- **Bobcoin Cost**: Same ($0.05/epic)
- **Time Cost**: +10 minutes (manual commit overhead)
- **Quality Gain**: 100% encoding compliance
- **Net Benefit**: Positive (prevents re-execution)

## References

- **Root Cause**: Wave 4 EPIC-CCN-024 UTF-16 corruption
- **Detection**: `scripts/preflight_validation.py`
- **Pattern System**: `docs/protocol/SPECIAL_CASE_DETECTION_PROTOCOL.md`
- **Encoding Protocol**: `docs/protocol/FILE_ENCODING_PROTOCOL.md` (V12.33)

## Version History

- **V1.0** (2026-06-16): Initial protocol based on Wave 4 EPIC-CCN-024 encoding issue