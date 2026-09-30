# Snapshot Testing Gap Analysis

**Date**: 2026-06-08  
**Status**: CRITICAL GAP IDENTIFIED  
**Priority**: P0 (Blocks autonomous refactoring)

---

## Executive Summary

The Jane Street snapshot testing system is **DOCUMENTED BUT NOT IMPLEMENTED**. All references point to non-existent scripts.

---

## What Was Found

### 1. Documentation (Extensive)

✅ **Exists**: Comprehensive documentation across 71 files
- `docs/standards/jane-street/JANE_STREET_TESTING_PATTERNS.md` - Verify framework patterns
- `docs/training/JANE_STREET_AGENT_GUIDE.md` - Session snapshot workflow
- `docs/brain/SOURCE_CODE_CONTEXT_INTEGRATION.md` - Integration guide
- Multiple workflow documents referencing session snapshots

### 2. Implementation (Missing)

❌ **Missing**: Core Python scripts
- `scripts/session_snapshot.py` - **DOES NOT EXIST**
- `scripts/session_continuity.py` - **DOES NOT EXIST**
- `scripts/negative_evidence_check.py` - **DOES NOT EXIST**

### 3. Test Infrastructure (Missing)

❌ **Missing**: Verify framework integration
- No `.verified.txt` files in tests/
- No `[UsesVerify]` attributes in test files
- No FsCheck property-based tests
- No Verify NuGet package references

---

## Impact Analysis

### Blocked Workflows

1. **Epic Run** (`/epic-run`)
   - Phase 0: No session initialization
   - Phase 5: No before/after snapshots
   - Phase 6: No verification snapshots

2. **PR Loop** (`/pr-loop`)
   - No session tracking
   - No budget management
   - No negative evidence cache

3. **TDD Workflow**
   - No test session tracking
   - No iteration snapshots
   - No budget warnings

4. **Autonomous Refactoring**
   - No cross-epic session continuity
   - No checkpoint/restore capability
   - No token budget tracking

### Documentation Debt

**71 files** reference non-existent infrastructure:
- Agent guides instruct calling missing scripts
- Workflow documents assume snapshot capability
- Training materials teach non-existent patterns

---

## What Needs Implementation

### Phase 1: Session Snapshot Infrastructure (P0)

**File**: `scripts/session_snapshot.py` (~300 lines)

**Commands**:
```bash
# Initialize session
python scripts/session_snapshot.py init <session_id> <agent_name> <task_description>

# Check if file already read
python scripts/session_snapshot.py check-read <session_id> <file_path>

# Record file read
python scripts/session_snapshot.py record-read <session_id> <file_path> <read_type>

# Record symbol exploration
python scripts/session_snapshot.py record-symbol <session_id> <symbol_id> <symbol_name> <file_path>

# Record search query
python scripts/session_snapshot.py record-search <session_id> <query> <tool> <result_count>

# Update token budget
python scripts/session_snapshot.py update-budget <session_id> <tokens_consumed>

# Get session state
python scripts/session_snapshot.py get <session_id> [--json]
```

**Storage**: `docs/brain/session_<id>.json`

**Schema**:
```json
{
  "session_id": "string",
  "agent_name": "string",
  "task_description": "string",
  "start_time": "ISO8601",
  "files_accessed": [
    {
      "path": "string",
      "read_type": "outline|full|symbol",
      "timestamp": "ISO8601",
      "tokens_estimated": "number"
    }
  ],
  "symbols_explored": [
    {
      "symbol_id": "string",
      "symbol_name": "string",
      "file_path": "string",
      "timestamp": "ISO8601"
    }
  ],
  "searches_performed": [
    {
      "query": "string",
      "tool": "search_symbols|search_text|find_references",
      "result_count": "number",
      "timestamp": "ISO8601"
    }
  ],
  "token_budget": {
    "total": 200000,
    "consumed": "number",
    "remaining": "number",
    "last_update": "ISO8601"
  },
  "negative_evidence": [
    {
      "query": "string",
      "verdict": "no_implementation_found",
      "timestamp": "ISO8601"
    }
  ]
}
```

### Phase 2: Session Continuity (P0)

**File**: `scripts/session_continuity.py` (~200 lines)

**Commands**:
```bash
# Auto-snapshot at token threshold
python scripts/session_continuity.py auto-snapshot <session_id> <current_tokens>

# Restore from checkpoint
python scripts/session_continuity.py restore <session_id> [checkpoint_number]

# List checkpoints
python scripts/session_continuity.py list <session_id>

# Merge checkpoints
python scripts/session_continuity.py merge <session_id> <checkpoint_ids...>

# Prune old checkpoints
python scripts/session_continuity.py prune <session_id> --keep <count>
```

**Storage**: `docs/brain/session_<id>_checkpoint_NNN.json`

**Trigger**: Auto-snapshot every 50k tokens

### Phase 3: Negative Evidence Cache (P1)

**File**: `scripts/negative_evidence_check.py` (~100 lines)

**Commands**:
```bash
# Check if query already failed
python scripts/negative_evidence_check.py check <query>

# Record failed search
python scripts/negative_evidence_check.py record <query> <verdict>

# List all negative evidence
python scripts/negative_evidence_check.py list
```

**Storage**: `docs/brain/negative_evidence.json`

### Phase 4: Verify Framework Integration (P1)

**NuGet Packages**:
```xml
<PackageReference Include="Verify.Xunit" Version="24.0.0" />
<PackageReference Include="FsCheck.Xunit" Version="2.16.6" />
```

**Test Pattern**:
```csharp
[UsesVerify]
public class ExtractionSnapshotTests
{
    [Fact]
    public Task CaptureBeforeState()
    {
        var state = new ExtractionState
        {
            MethodName = "HydrateFSMsFromWorkingOrders",
            CYC = 71,
            LOC = 450,
            Callers = new[] { "OnStateChange", "OnExecutionUpdate" }
        };
        
        return Verify(state);
    }
}
```

**Storage**: `tests/**/*.verified.txt`

### Phase 5: Workflow Integration (P0)

**Epic Run Integration**:
```markdown
## Phase 0: Session Initialization
python scripts/session_snapshot.py init "EPIC-CCN-X" "v12-engineer" "Extract MethodName"

## Phase 5: Before Snapshot (per ticket)
python scripts/session_snapshot.py record-read "EPIC-CCN-X" "src/V12_002.cs" "full"
# Capture before state via Verify test

## Phase 5: After Snapshot (per ticket)
# Capture after state via Verify test
# Compare .verified.txt diffs

## Phase 6: Auto-Snapshot
python scripts/session_continuity.py auto-snapshot "EPIC-CCN-X" <tokens>
```

**PR Loop Integration**:
```markdown
## Step 1: Initialize Session
python scripts/session_snapshot.py init "PR-X-REPAIR" "advanced" "Fix PR #X issues"

## Step 2: Budget Tracking
python scripts/session_snapshot.py update-budget "PR-X-REPAIR" <tokens>

## Step 3: Auto-Checkpoint
python scripts/session_continuity.py auto-snapshot "PR-X-REPAIR" <tokens>
```

---

## Implementation Priority

### Immediate (P0) - Blocks Autonomous Refactoring
1. ✅ `scripts/session_snapshot.py` - Core session tracking
2. ✅ `scripts/session_continuity.py` - Checkpoint/restore
3. ✅ Integration into `/epic-run` workflow
4. ✅ Integration into `/pr-loop` workflow

### High (P1) - Enhances Quality
5. ⬜ `scripts/negative_evidence_check.py` - Prevents redundant searches
6. ⬜ Verify framework NuGet packages
7. ⬜ Example snapshot tests in `tests/`
8. ⬜ Integration into TDD workflow

### Medium (P2) - Documentation Cleanup
9. ⬜ Update all 71 files to reflect actual implementation
10. ⬜ Add troubleshooting guide
11. ⬜ Add performance benchmarks

---

## Estimated Implementation Time

- **Phase 1**: 2 hours (session_snapshot.py)
- **Phase 2**: 1.5 hours (session_continuity.py)
- **Phase 3**: 1 hour (negative_evidence_check.py)
- **Phase 4**: 2 hours (Verify framework + example tests)
- **Phase 5**: 1 hour (workflow integration)

**Total**: ~7.5 hours

---

## Recommendation

**IMPLEMENT IMMEDIATELY** before resuming autonomous refactoring. The 168-epic queue cannot proceed without:
1. Session tracking (prevent redundant file reads)
2. Token budget management (prevent mid-epic exhaustion)
3. Checkpoint/restore (recover from failures)
4. Negative evidence cache (prevent infinite search loops)

**Alternative**: Proceed with EPIC-CCN-3 manually without snapshot infrastructure, but this will:
- Waste tokens on redundant reads
- Risk mid-epic token exhaustion
- Lose progress on failures
- Repeat failed searches

**Director Decision Required**: Implement infrastructure first (7.5 hours) or proceed without it (higher risk)?