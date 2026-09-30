# Special Case Detection Protocol

**Version**: 1.0  
**Date**: 2026-06-16  
**Purpose**: Automatically detect and handle special cases in epic execution

---

## Overview

Some epics require special handling due to file characteristics, not epic numbers. This protocol defines **file-based patterns** for automatic detection and routing.

**Key Principle**: Detection is based on **file attributes** (encoding, complexity, test requirements), NOT epic numbers.

---

## Special Case Categories

### 1. Encoding-Sensitive Files (LOCAL EXECUTION REQUIRED)

**Pattern**: Files with non-ASCII characters or non-UTF-8 encoding

**Detection Method**:
```python
def detect_encoding_issues(file_path: str) -> bool:
    """Detect if file requires local execution due to encoding."""
    try:
        with open(file_path, 'r', encoding='utf-8') as f:
            content = f.read()
            # Check for non-ASCII characters
            if any(ord(c) > 127 for c in content):
                return True
    except UnicodeDecodeError:
        return True
    
    # Check file encoding
    import chardet
    with open(file_path, 'rb') as f:
        result = chardet.detect(f.read())
        if result['encoding'] not in ['utf-8', 'ascii']:
            return True
    
    return False
```

**Known Files** (as of 2026-06-16):
- `src/V12_002.DrawingHelpers.cs` - Contains UTF-16 LE characters
- `src/V12_002.ChartControl.cs` - Unicode in comments (if exists)

**Routing**:
- ❌ **DO NOT** execute on VM
- ✅ **DO** execute locally with manual verification
- ✅ **DO** run `verify_ascii_only.py` after extraction

**Protocol Reference**: `docs/protocol/LOCAL_EXECUTION_PROTOCOL.md`

**Label**: `encoding-sensitive`

---

### 2. Invalid Target Methods (SKIP EPIC)

**Pattern**: Target method doesn't exist in codebase

**Detection Method**:
```python
def detect_invalid_target(method_name: str, file_path: str) -> bool:
    """Detect if target method exists in specified file."""
    # Search file for method signature
    with open(file_path, 'r') as f:
        content = f.read()
        
    # Check for method declaration patterns
    patterns = [
        f"void {method_name}(",
        f"bool {method_name}(",
        f"int {method_name}(",
        f"string {method_name}(",
        f"Task {method_name}(",
        f"async Task {method_name}(",
    ]
    
    return not any(pattern in content for pattern in patterns)
```

**Known Cases** (as of 2026-06-16):
- `Dispatch_PublishMarketBracketToPhoton` in `src/V12_002.SIMA.Dispatch.cs` - Method doesn't exist

**Routing**:
- ❌ **DO NOT** execute any phases
- ✅ **DO** mark epic as INVALID in roadmap
- ✅ **DO** document in forensic report
- ✅ **DO** update jCodemunch index

**Protocol Reference**: `docs/protocol/PHASE_MINUS_1_PREFLIGHT.md` (to be created)

**Label**: `invalid-target`

---

### 3. Test-Heavy Methods (EXTENDED EXECUTION TIME)

**Pattern**: Methods requiring extensive test generation

**Detection Method**:
```python
def detect_test_requirements(method_name: str, file_path: str, cyc: int) -> dict:
    """Detect if method requires extensive test generation."""
    # High complexity = more test cases
    if cyc > 30:
        return {
            "requires_extended_time": True,
            "estimated_test_cases": cyc * 2,  # 2 tests per CYC point
            "framework": "xUnit",
            "coverage_target": 90
        }
    
    # Check if method is in critical path
    critical_files = [
        'V12_002.Atm.cs',
        'V12_002.Execution.cs',
        'V12_002.SIMA.cs',
    ]
    
    if any(f in file_path for f in critical_files):
        return {
            "requires_extended_time": True,
            "estimated_test_cases": cyc * 1.5,
            "framework": "xUnit",
            "coverage_target": 85
        }
    
    return {
        "requires_extended_time": False,
        "estimated_test_cases": cyc,
        "framework": "xUnit",
        "coverage_target": 80
    }
```

**Known Patterns**:
- Any method with CYC > 30
- Methods in `V12_002.Atm.cs` (critical execution path)
- Methods in `V12_002.SIMA.cs` (state machine core)

**Routing**:
- ✅ **DO** execute on VM (normal)
- ✅ **DO** allocate 2× normal time for Phase 5
- ✅ **DO** generate comprehensive xUnit tests
- ✅ **DO** verify coverage ≥ target

**Protocol Reference**: `docs/protocol/TEST_GENERATION_PROTOCOL.md`

**Label**: `test-heavy`

---

### 4. Already-Completed Epics (SKIP)

**Pattern**: Epic already has clean Phase 6 completion

**Detection Method**:
```python
def detect_already_complete(epic_id: str) -> bool:
    """Detect if epic is already complete with clean execution."""
    completion_file = f"docs/brain/{epic_id}/06-completion-report.md"
    
    if not os.path.exists(completion_file):
        return False
    
    # Check if completion was clean (no issues)
    with open(completion_file, 'r') as f:
        content = f.read()
        
    # Look for issue indicators
    issue_indicators = [
        "P0 issue",
        "P1 issue",
        "compilation error",
        "behavioral change",
        "Jane Street violation",
    ]
    
    return not any(indicator in content.lower() for indicator in issue_indicators)
```

**Known Cases** (as of 2026-06-16):
- EPIC-CCN-075 (Infrastructure) - Clean execution, 0 issues

**Routing**:
- ❌ **DO NOT** re-execute
- ✅ **DO** keep existing Phase 5-6 outputs
- ✅ **DO** merge PR if not already merged

**Label**: `already-complete`

---

## Detection Workflow

### Phase -1: Pre-Flight Validation (NEW)

**Run BEFORE Phase 0 for every epic**

```python
def preflight_validation(epic_id: str, method_name: str, file_path: str, cyc: int) -> dict:
    """Run all special case detections before starting epic."""
    
    results = {
        "epic_id": epic_id,
        "method_name": method_name,
        "file_path": file_path,
        "special_cases": [],
        "routing": "normal",  # normal, local, skip
        "labels": []
    }
    
    # Check 1: Invalid target
    if detect_invalid_target(method_name, file_path):
        results["special_cases"].append("invalid-target")
        results["routing"] = "skip"
        results["labels"].append("invalid-target")
        return results  # Early exit - don't check other cases
    
    # Check 2: Already complete
    if detect_already_complete(epic_id):
        results["special_cases"].append("already-complete")
        results["routing"] = "skip"
        results["labels"].append("already-complete")
        return results  # Early exit
    
    # Check 3: Encoding issues
    if detect_encoding_issues(file_path):
        results["special_cases"].append("encoding-sensitive")
        results["routing"] = "local"
        results["labels"].append("encoding-sensitive")
    
    # Check 4: Test requirements
    test_req = detect_test_requirements(method_name, file_path, cyc)
    if test_req["requires_extended_time"]:
        results["special_cases"].append("test-heavy")
        results["labels"].append("test-heavy")
        results["test_requirements"] = test_req
    
    return results
```

### Integration Points

#### 1. Epic Planner (`scripts/epic_planner.py`)
```python
# Add to epic generation
for epic in epic_candidates:
    preflight = preflight_validation(
        epic_id=epic["epic_number"],
        method_name=epic["method"],
        file_path=epic["file"],
        cyc=epic["cyclomatic"]
    )
    
    epic["special_cases"] = preflight["special_cases"]
    epic["routing"] = preflight["routing"]
    epic["labels"] = preflight["labels"]
```

#### 2. Wave Launcher (`scripts/launch_wave.py`)
```python
# Filter epics by routing
normal_epics = [e for e in epics if e["routing"] == "normal"]
local_epics = [e for e in epics if e["routing"] == "local"]
skip_epics = [e for e in epics if e["routing"] == "skip"]

# Launch normal epics on VM
launch_vm_execution(normal_epics)

# Report local epics for manual execution
print(f"Local execution required: {len(local_epics)} epics")
for epic in local_epics:
    print(f"  {epic['epic_id']}: {epic['file']} ({', '.join(epic['labels'])})")

# Report skipped epics
print(f"Skipped: {len(skip_epics)} epics")
for epic in skip_epics:
    print(f"  {epic['epic_id']}: {', '.join(epic['special_cases'])}")
```

#### 3. Autonomous Refactor Mode (`.bob/commands/autonomous-refactor.md`)
```markdown
## PHASE -1: PRE-FLIGHT VALIDATION (NEW)

Before starting any epic, run special case detection:

```bash
python scripts/preflight_validation.py --epic EPIC-CCN-X
```

**Routing Decision**:
- `normal`: Execute on VM as usual
- `local`: Execute locally, not on VM
- `skip`: Do not execute, mark as INVALID or already-complete

**Labels Applied**:
- `encoding-sensitive`: Requires local execution
- `invalid-target`: Target method doesn't exist
- `test-heavy`: Requires extended time + comprehensive tests
- `already-complete`: Skip, already done cleanly
```

---

## File-Based Pattern Registry

### Encoding-Sensitive Files

| File Pattern | Reason | Detection |
|--------------|--------|-----------|
| `*DrawingHelpers.cs` | UTF-16 LE encoding | `chardet.detect()` |
| `*ChartControl.cs` | Unicode in comments | Non-ASCII chars |
| `*Localization*.cs` | Translated strings | Non-ASCII chars |

### High-Complexity Files (Test-Heavy)

| File Pattern | Reason | Detection |
|--------------|--------|-----------|
| `*Atm.cs` | Critical execution path | File name match |
| `*SIMA*.cs` | State machine core | File name match |
| `*Execution*.cs` | Order execution logic | File name match |
| Any file with CYC > 30 | Complex logic | Complexity audit |

### Known Invalid Targets

| Method Name | File | Reason | Date Discovered |
|-------------|------|--------|-----------------|
| `Dispatch_PublishMarketBracketToPhoton` | `V12_002.SIMA.Dispatch.cs` | Method doesn't exist | 2026-06-16 |

---

## Labeling System

### Label Format
`<category>-<subcategory>`

### Standard Labels

| Label | Category | Meaning | Routing |
|-------|----------|---------|---------|
| `encoding-sensitive` | Execution | Non-UTF-8 encoding | Local |
| `invalid-target` | Validation | Method doesn't exist | Skip |
| `test-heavy` | Testing | Requires extensive tests | Normal (extended time) |
| `already-complete` | Status | Clean completion exists | Skip |
| `high-complexity` | Complexity | CYC > 30 | Normal (extended time) |
| `critical-path` | Priority | Hot path method | Normal (extra verification) |

### Label Application

Labels are stored in:
1. `epic_roadmap.json` - `labels` field
2. Epic manifest - `docs/brain/EPIC-X/manifest.json`
3. GitHub issue - Applied as issue labels

---

## Reporting

### Pre-Flight Report Format

```markdown
# Wave X Pre-Flight Validation Report

## Summary
- Total epics: 80
- Normal execution: 76
- Local execution: 1
- Skipped: 3

## Special Cases Detected

### Local Execution Required (1)
- EPIC-CCN-24: DrawORBox in V12_002.DrawingHelpers.cs
  - Labels: encoding-sensitive
  - Reason: UTF-16 LE encoding detected

### Skipped - Invalid Target (1)
- EPIC-CCN-27: Dispatch_PublishMarketBracketToPhoton in V12_002.SIMA.Dispatch.cs
  - Labels: invalid-target
  - Reason: Method not found in codebase

### Skipped - Already Complete (1)
- EPIC-CCN-75: Infrastructure extraction
  - Labels: already-complete
  - Reason: Clean Phase 6 completion exists

### Test-Heavy (12)
- EPIC-CCN-01: Method X (CYC 35)
- EPIC-CCN-05: Method Y (CYC 42)
- ... (10 more)
```

---

## Maintenance

### Adding New Special Cases

1. **Identify Pattern**: What file/method characteristic triggers special handling?
2. **Create Detection Function**: Add to `scripts/preflight_validation.py`
3. **Define Label**: Add to labeling system
4. **Update Registry**: Document in this file
5. **Test Detection**: Run on known cases
6. **Update Workflows**: Integrate into epic planner, wave launcher, autonomous mode

### Updating Existing Patterns

1. **Document Change**: Update pattern registry
2. **Update Detection Logic**: Modify detection function
3. **Re-validate**: Run on all epics in roadmap
4. **Update Labels**: Re-label affected epics

---

## Success Criteria

### Pre-Flight Validation Complete When:
- ✅ All epics scanned for special cases
- ✅ Labels applied to epic roadmap
- ✅ Routing decisions made (normal/local/skip)
- ✅ Pre-flight report generated
- ✅ Local execution list provided
- ✅ Skip list documented with reasons

### Special Case Handling Complete When:
- ✅ Normal epics execute on VM
- ✅ Local epics execute locally with verification
- ✅ Skipped epics marked in roadmap
- ✅ All labels accurate and up-to-date

---

## References

- **Local Execution Protocol**: `docs/protocol/LOCAL_EXECUTION_PROTOCOL.md`
- **Test Generation Protocol**: `docs/protocol/TEST_GENERATION_PROTOCOL.md`
- **Phase -1 Pre-Flight**: `docs/protocol/PHASE_MINUS_1_PREFLIGHT.md` (to be created)
- **Epic Planner**: `scripts/epic_planner.py`
- **Pre-Flight Validator**: `scripts/preflight_validation.py` (to be created)

---

**Version**: 1.0  
**Status**: 🟢 READY FOR IMPLEMENTATION  
**Next Action**: Create `scripts/preflight_validation.py` and integrate into workflows