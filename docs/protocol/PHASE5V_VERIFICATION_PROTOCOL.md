# Phase 5.V Verification Protocol (V12.34)

**Version**: 1.0  
**Effective**: 2026-06-16  
**Status**: MANDATORY for all Phase 5.V verifications  
**Supersedes**: Implicit Phase 5.V verification guidelines

## Purpose

This protocol defines the **5 MANDATORY CHECKS** for Phase 5.V (Verification) to ensure surgical execution quality and prevent buggy PRs.

## Core Principle: 5-Check Gate

Phase 5.V is a **QUALITY GATE**. ALL 5 checks MUST pass before marking an epic as complete.

## The 5 Mandatory Checks

### Check 1: Compilation ✅
**Purpose**: Verify code compiles without errors

**Command**:
```powershell
dotnet build
```

**Success Criteria**:
- Exit code: 0
- Zero compilation errors
- Zero compilation warnings (P0 level)

**Failure Action**:
- Document error in verification report
- Mark epic as FAILED
- Trigger recovery loop

---

### Check 2: Complexity Reduction ✅
**Purpose**: Verify target method CYC reduced to ≤8

**Command**:
```bash
python scripts/complexity_audit.py
```

**Success Criteria**:
- Target method CYC ≤8 (Jane Street strict standard)
- Extracted methods CYC ≤8
- Overall complexity redistributed (not increased)

**Failure Action**:
- Document actual CYC in verification report
- If CYC >8: Mark as FAILED, trigger recovery
- If CYC ≤8: PASS

---

### Check 3: Scope Compliance ✅
**Purpose**: Verify ONLY target method was modified

**Command**:
```bash
git diff HEAD~1 --stat
```

**Success Criteria**:
- ONLY target file appears in diff
- ONLY target method + extracted helpers modified
- No adjacent methods touched
- No unrelated files changed

**Failure Action**:
- Document scope violations in verification report
- List all modified methods
- Mark as FAILED (scope creep violation)
- Trigger recovery loop

---

### Check 4: Test Coverage ✅
**Purpose**: Verify xUnit tests generated and passing

**Command**:
```powershell
dotnet test
```

**Success Criteria**:
- xUnit tests exist for extracted methods
- All tests pass (exit code 0)
- Test framework: xUnit ONLY (NEVER NUnit/MSTest)

**Failure Action**:
- If no tests: Document as technical debt, PASS with warning
- If tests fail: Mark as FAILED, trigger recovery
- If wrong framework: Mark as FAILED, regenerate with xUnit

---

### Check 5: Encoding Compliance ✅
**Purpose**: Verify UTF-8 without BOM (no UTF-16)

**Command**:
```powershell
powershell -File .\scripts\check_encoding.ps1
```

**Success Criteria**:
- Exit code: 0
- All modified files: UTF-8 without BOM
- Zero UTF-16 violations

**Failure Action**:
- Run with `-Fix` flag to auto-convert
- Re-verify encoding
- If still fails: Mark as FAILED

---

## Verification Checklist

### Pre-Verification
- [ ] Phase 5 execution completed
- [ ] Ticket completion file exists
- [ ] Git commit created

### Execute 5 Checks
- [ ] Check 1: Compilation (`dotnet build`)
- [ ] Check 2: Complexity (`complexity_audit.py`)
- [ ] Check 3: Scope (`git diff --stat`)
- [ ] Check 4: Tests (`dotnet test`)
- [ ] Check 5: Encoding (`check_encoding.ps1`)

### Post-Verification
- [ ] Document results in `06-verification-report.md`
- [ ] Update epic status in roadmap
- [ ] If ALL PASS: Mark epic COMPLETE
- [ ] If ANY FAIL: Trigger recovery loop

## Verification Report Template

```markdown
# Phase 5.V Verification Report: EPIC-XXX

## Check Results

### ✅ Check 1: Compilation
- Command: `dotnet build`
- Exit Code: 0
- Errors: 0
- Warnings: 0
- Status: PASS

### ✅ Check 2: Complexity Reduction
- Target Method: `MethodName`
- Before CYC: 15
- After CYC: 6
- Target: ≤8
- Status: PASS (25% under target)

### ✅ Check 3: Scope Compliance
- Files Modified: 1 (target file only)
- Methods Modified: 2 (target + 1 extracted)
- Adjacent Changes: 0
- Status: PASS

### ✅ Check 4: Test Coverage
- Framework: xUnit
- Tests Generated: 3
- Tests Passing: 3
- Status: PASS

### ✅ Check 5: Encoding Compliance
- Files Checked: 1
- UTF-8 without BOM: 1
- UTF-16 Violations: 0
- Status: PASS

## Overall Result
- **Status**: ✅ ALL CHECKS PASS
- **Epic Status**: COMPLETE
- **Ready for PR**: YES
```

## Failure Scenarios

### Scenario 1: Compilation Failure
**Symptoms**: `dotnet build` exits with code 1

**Root Causes**:
- Syntax errors in extracted code
- Missing using statements
- Type mismatches

**Recovery**:
1. Review build errors
2. Fix compilation issues
3. Re-run Phase 5.V
4. Document fix in recovery notes

---

### Scenario 2: Complexity Not Reduced
**Symptoms**: Target method CYC still >8

**Root Causes**:
- Insufficient extraction
- Complex logic not decomposed
- Nested conditionals remain

**Recovery**:
1. Review extraction plan
2. Identify remaining complexity
3. Generate additional tickets
4. Re-execute Phase 5

---

### Scenario 3: Scope Violation
**Symptoms**: `git diff` shows multiple methods changed

**Root Causes**:
- Over-optimization (fixed adjacent code)
- Pre-existing errors fixed
- "While we're here" improvements

**Recovery**:
1. Revert all changes (`git reset --hard HEAD~1`)
2. Document scope violation
3. Re-execute Phase 5 with SURGICAL ONLY mandate
4. Verify scope compliance

---

### Scenario 4: Test Failures
**Symptoms**: `dotnet test` exits with code 1

**Root Causes**:
- Incorrect test assertions
- Logic errors in extracted code
- Missing test setup

**Recovery**:
1. Review failing tests
2. Fix test logic or implementation
3. Re-run tests
4. Document fix

---

### Scenario 5: Encoding Violations
**Symptoms**: `check_encoding.ps1` exits with code 1

**Root Causes**:
- Bob CLI saved as UTF-16
- Windows editor auto-converted
- Copy-paste from UTF-16 source

**Recovery**:
1. Run `check_encoding.ps1 -Fix`
2. Re-verify encoding
3. Commit encoding fix
4. Document in recovery notes

## Integration with Other Protocols

- **Phase 5 Execution**: Validates surgical execution quality
- **Recovery Loop Protocol**: Handles failed verifications
- **Building-Blocks Method**: Ensures consistent verification
- **Pre-Push Validation**: Runs subset of these checks locally

## Success Metrics

**Wave 4 Baseline** (before protocol):
- 28 issues found by Greptile (9 P0, 12 P1, 6 P2)
- 7 PRs closed due to quality issues
- $7.80 wasted on failed executions

**Wave 5 Target** (with protocol):
- 0 P0/P1 issues in PRs
- 100% PR merge rate
- Zero rollbacks

## References

- **Root Cause**: Wave 4 PR #10-16 failures
- **Analysis**: `WAVE4_FULL_PR_AUDIT.md`
- **Hardening Plan**: `WAVE4_PROTOCOL_HARDENING_PLAN.md`
- **Phase 5 Protocol**: `PHASE5_EXECUTION_PROTOCOL.md`

## Version History

- **V1.0** (2026-06-16): Initial protocol based on Wave 4 root cause analysis