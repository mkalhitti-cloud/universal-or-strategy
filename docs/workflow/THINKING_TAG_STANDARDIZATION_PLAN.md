# Thinking Tag Standardization Plan

**Status**: Queued for implementation after Wave 4 Phase 6 completion
**Priority**: Low (documentation improvement, non-blocking)
**Effort**: 1.5-2.5 hours (7 steps total)
**Risk**: Minimal (documentation + template updates, no src/ changes)

---

## Background

Analysis of Anthropic's Claude Fable 5 system prompt revealed a standardized thinking tag format that improves readability and agent-to-agent handoffs:

```markdown
<thinking>**Brief Summary (5-10 words)**
Detailed reasoning here...
</thinking>
```

**Current State**: V12 epic documentation uses thinking tags inconsistently (some with summaries, some without).

**Desired State**: All epic documentation uses standardized format with mandatory summary line.

---

## Implementation Steps

### 1. Document Standard (5 min)
Create `docs/workflow/THINKING_TAG_STANDARD.md`:
- Format specification
- Examples (good vs bad)
- Rationale (readability, handoffs)

### 2. Update Templates (15 min)
Modify `building-blocks/autonomous-refactoring/templates/`:
- `00-hotspots.md.template`
- `01-scope-boundary.md.template`
- `02-architecture-plan.md.template`
- `03-audit-report.md.template`
- `04-tickets.md.template`
- `05-completion-report.md.template`

Add thinking tag examples with summary format.

### 3. Update SOP (10 min)
Modify `docs/workflow/WAVE_PHASE_SCRIPT_GENERATION_SOP_V3.md`:
- Add thinking tag format requirement to each phase
- Include in verification checklist

### 4. Update Mode Definition (10 min)
Modify `.bob/custom_modes.yaml` → `autonomous-refactor` mode:
- Add thinking tag format to roleDefinition
- Add to customRules section

### 5. Retroactive Update (Optional, 30 min)
Update existing Wave 4 epic documentation:
- Add summaries to existing thinking tags
- Only if time permits, not blocking

### 6. File Creation Strategy (10 min)
Add to `docs/workflow/WAVE_PHASE_SCRIPT_GENERATION_SOP_V3.md`:
- **SHORT files (<100 lines)**: Create in single write_to_file call
- **LONG files (>100 lines)**: Build iteratively (outline → sections → review → refine)
- Apply to Phase 4 (ticket generation) to prevent truncated files
- Add verification step: read_file after write to confirm completeness

### 7. Error Handling Template (15 min)
Update `building-blocks/autonomous-refactoring/templates/phase5-execution-template.sh`:
- Wrap Bob CLI calls in error handlers
- Pattern:
  ```bash
  if ! bob --mode v12-engineer --yolo "Execute ticket"; then
      echo "ERROR: Ticket execution failed" >&2
      exit 1
  fi
  ```
- Log errors to phase-specific log files
- Exit with non-zero code on failure (enables recovery loop detection)

---

## Format Specification

### Required Format
```markdown
<thinking>**Brief Summary (5-10 words)**
Detailed reasoning with multiple lines...
Can include bullet points, code snippets, etc.
</thinking>
```

### Examples

**Good**:
```markdown
<thinking>**Validate extraction scope boundary**
The proposed extraction targets `ProcessIpcCommands` (CYC 61).
Blast radius analysis shows 3 direct callers, all in same file.
No cross-file dependencies detected.
Extraction is safe and contained.
</thinking>
```

**Bad** (no summary):
```markdown
<thinking>
The proposed extraction targets ProcessIpcCommands with CYC 61.
Blast radius shows 3 callers...
</thinking>
```

---

## Benefits

1. **Improved Readability**: Summary line provides quick context
2. **Better Handoffs**: Agents can scan summaries without reading full reasoning
3. **Consistency**: Matches Anthropic's production pattern
4. **Searchability**: Summaries make grep/search more effective

---

## Risks & Mitigations

| Risk | Mitigation |
|------|------------|
| Agents forget format | Add to mode roleDefinition (enforced) |
| Inconsistent summaries | Provide examples in templates |
| Retroactive work | Mark as optional, focus on new epics |

---

## Success Criteria

- [ ] `THINKING_TAG_STANDARD.md` created
- [ ] All 6 templates updated with format examples
- [ ] SOP updated with format requirement
- [ ] Mode definition updated with format mandate
- [ ] Retroactive update of Wave 4 docs (optional)
- [ ] File creation strategy added to SOP
- [ ] Error handling template updated
- [ ] Format used in all new epics (Wave 5+)

---

## Timeline

**Trigger**: After Wave 4 Phase 6 completion
**Duration**: 1.5-2.5 hours (7 steps)
**Blocking**: No (can be done anytime)

### Step Breakdown
1. **Document Standard** (5 min) - Create format specification
2. **Update Templates** (15 min) - Add thinking tag examples to 6 templates
3. **Update SOP** (10 min) - Add format requirement to wave execution SOP
4. **Update Mode Definition** (10 min) - Enforce format in autonomous-refactor mode
5. **Retroactive Update** (30 min, optional) - Update Wave 4 docs with summaries
6. **File Creation Strategy** (10 min) - Add SHORT (<100 lines) vs LONG (>100 lines) rules
7. **Error Handling Template** (15 min) - Add try-catch pattern to Phase 5 scripts

---

## Implementation Details

### Step 6: File Creation Strategy (Detailed)

**File to Update**: `docs/workflow/WAVE_PHASE_SCRIPT_GENERATION_SOP_V3.md`

**Add Section**: "File Size Strategy" (after "Building-Blocks Method")

```markdown
### File Size Strategy (Anthropic Pattern)

**SHORT Files (<100 lines)**:
- Create in single write_to_file call
- Verify with read_file immediately after
- Example: ticket files, scope documents, manifests

**LONG Files (>100 lines)**:
- Build iteratively to prevent truncation
- Pattern: outline → sections → review → refine
- Verify each section with read_file before proceeding
- Example: architecture plans, completion reports

**Phase 4 Application**:
- Ticket files typically <100 lines → single write
- EXECUTION_GUIDE.md may exceed 100 lines → iterative build
- Always verify with read_file after write_to_file
```

**Rationale**: Prevents truncated files in Phase 4 ticket generation (observed in Wave 3).

---

### Step 7: Error Handling Template (Detailed)

**File to Update**: `building-blocks/autonomous-refactoring/templates/phase5-execution-template.sh`

**Current Pattern** (no error handling):
```bash
bob --mode v12-engineer --yolo "Execute ticket-01"
```

**New Pattern** (with error handling):
```bash
#!/bin/bash
set -e  # Exit on any error

EPIC_ID="EPIC-CCN-XXX"
TICKET_ID="ticket-01"
LOG_FILE="logs/phase5/${EPIC_ID}_${TICKET_ID}.log"

echo "Starting ticket execution: ${TICKET_ID}" | tee -a "${LOG_FILE}"

if ! bob --mode v12-engineer --yolo "Execute ${TICKET_ID}" 2>&1 | tee -a "${LOG_FILE}"; then
    echo "ERROR: Ticket execution failed for ${TICKET_ID}" >&2 | tee -a "${LOG_FILE}"
    echo "Check log: ${LOG_FILE}" >&2
    exit 1
fi

echo "SUCCESS: Ticket ${TICKET_ID} completed" | tee -a "${LOG_FILE}"
exit 0
```

**Benefits**:
- Non-zero exit code enables recovery loop detection
- Logs capture full output for debugging
- Clear success/failure messages
- Matches Anthropic's error handling pattern

**Rationale**: Improves robustness of Phase 5 execution scripts, enables automatic failure detection in recovery loop protocol.

---

## Related Documents

- Source: `docs/Claude Fable 5 System Prompt.md` (Anthropic's format)
- Templates: `building-blocks/autonomous-refactoring/templates/`
- SOP: `docs/workflow/WAVE_PHASE_SCRIPT_GENERATION_SOP_V3.md`
- Mode: `.bob/custom_modes.yaml` → `autonomous-refactor`
- Recovery Loop: `docs/protocol/RECOVERY_LOOP_PROTOCOL.md`

---

## Anthropic Patterns Applied

1. **Thinking Tag Format**: `<thinking>**Summary**\ndetails</thinking>`
2. **File Creation Strategy**: SHORT (<100) vs LONG (>100) lines
3. **Error Handling**: try-catch with logging and exit codes

**Patterns NOT Applied** (with rationale):
- ❌ Copyright Compliance: We create original code, not copying
- ✅ Tool Use Protocol: Already enforced in all modes
- ✅ Sequential Thinking MCP: Already mandatory in 9/10 phases
- ✅ Artifact Storage: Already using manifest.json

---

**Created**: 2026-06-15
**Author**: Autonomous Refactor Mode
**Status**: Queued for post-Wave 4 implementation
**Source Analysis**: Claude Fable 5 System Prompt comparison
