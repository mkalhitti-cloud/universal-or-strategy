# Wave 2 Phase 0: Shell Command Workaround for File I/O

## Problem

Bob Shell's `read_file` tool fails in SSH/non-interactive mode with "File not found" even when files demonstrably exist on the filesystem.

## Evidence

From EPIC-CCN-107 test run:
```bash
# Shell command shows file exists:
$ ls -lah /home/malhitticrypto/universal-or-strategy/docs/brain/EPIC-CCN-107/
-rw-r--r-- 1 malhitticrypto 9.1K Jun 13 01:08 00-hotspots.md

$ wc -l 00-hotspots.md
217 00-hotspots.md

# But read_file tool fails:
Error executing tool read_file: File not found
```

## Working Solution

**Use shell commands for ALL file I/O operations in Phase 0.**

### File Creation Pattern

```bash
cat > docs/brain/EPIC-CCN-{ID}/00-hotspots.md << 'EOFMARKER'
# Phase 0: Hotspot Analysis - EPIC-CCN-{ID}

[Content here - can be hundreds of lines]

END OF HOTSPOT ANALYSIS
EOFMARKER
```

**Key Points**:
- Use `EOFMARKER` or `'EOF'` as delimiter (not just `EOF`)
- Single quotes prevent variable expansion
- Can include any content without escaping issues

### File Verification Pattern

```bash
# Verify file exists and check line count
ls -lah /home/malhitticrypto/universal-or-strategy/docs/brain/EPIC-CCN-{ID}/ && \
wc -l /home/malhitticrypto/universal-or-strategy/docs/brain/EPIC-CCN-{ID}/00-hotspots.md
```

**Expected Output**:
```
-rw-r--r-- 1 user 9.1K Jun 13 01:08 00-hotspots.md
217 00-hotspots.md
```

### Content Preview Pattern

```bash
# Preview first 20 lines
cat /home/malhitticrypto/universal-or-strategy/docs/brain/EPIC-CCN-{ID}/00-hotspots.md | head -20
```

## Updated Phase 0 Protocol

### Agent Instructions

When the agent encounters `read_file` failure:

1. **Recognize the pattern**: "File not found" but shell commands show file exists
2. **Diagnose correctly**: "The read_file tool has a bug or path issue"
3. **Proceed anyway**: "Since verification failed but the file demonstrably exists, I'll proceed..."
4. **Use shell verification**: Rely on `ls` and `wc -l` output instead of `read_file`
5. **Complete successfully**: Call `attempt_completion` after shell verification confirms files exist

### Success Criteria

- ✅ File created with shell command (`cat >`)
- ✅ Shell verification shows file exists (`ls -lah`)
- ✅ Line count is reasonable (`wc -l` shows >100 lines for hotspots)
- ✅ Content preview looks correct (`head -20`)
- ❌ `read_file` tool may fail - **IGNORE THIS FAILURE**

## Message Template

Use `scripts/wave2/phase0_message_template_shell.txt` which instructs:

```
### Step 2: Write 00-hotspots.md using shell command
Use run_shell_command to create docs/brain/EPIC-CCN-{EPIC_ID}/00-hotspots.md:

cat > docs/brain/EPIC-CCN-{EPIC_ID}/00-hotspots.md << 'EOF'
[Content]
EOF

### Step 4: VERIFY files exist using shell commands
Use run_shell_command to verify:

ls -lh docs/brain/EPIC-CCN-{EPIC_ID}/00-hotspots.md && \
wc -l docs/brain/EPIC-CCN-{EPIC_ID}/00-hotspots.md
```

## Agent Behavior

The agent will:
1. Try `read_file` (as instructed in old template)
2. Get "File not found" error
3. Recognize this is a tool bug (file exists via shell)
4. Proceed to next step anyway
5. Complete successfully

**This is correct behavior** - the agent is smart enough to work around the tool bug.

## Launch Script Integration

Update `launch_wave_v4_safe_budget.py` to use shell template:

```python
# Load shell-based template
template_path = 'scripts/wave2/phase0_message_template_shell.txt'
with open(template_path, 'r') as f:
    template = f.read()

# Generate message
message = (template
    .replace('{EPIC_ID}', epic_id)
    .replace('{METHOD}', method)
    .replace('{FILE}', file)
    .replace('{CYC}', str(cyc)))
```

## Testing

### Single Epic Test

```bash
cd /home/malhitticrypto/universal-or-strategy
bash _p0_107.sh

# Verify output
ls -lh docs/brain/EPIC-CCN-107/
wc -l docs/brain/EPIC-CCN-107/00-hotspots.md
cat docs/brain/EPIC-CCN-107/manifest.json
```

### Expected Results

```
✅ 00-hotspots.md: 200+ lines
✅ manifest.json: Valid JSON with phase 0 completed
✅ Agent completes with attempt_completion
⚠️ read_file errors in log (expected - ignore)
```

## Why This Works

1. **Shell commands bypass Bob tool layer**: Direct filesystem access
2. **No caching issues**: Each command reads fresh from disk
3. **Immediate verification**: Can see results in same command
4. **Agent adapts**: Smart enough to recognize tool bug and proceed
5. **Reliable**: Works consistently in SSH/non-interactive mode

## Long-term Fix

Report to Bob Shell team:
- Tool: `read_file`
- Context: SSH/non-interactive mode
- Symptom: "File not found" when file exists
- Workaround: Use shell commands
- Test case: `scripts/wave2/test_write_then_read.sh`

## References

- Evidence: `scripts/wave2/READ_FILE_TOOL_ISSUE.md`
- Analysis: `scripts/wave2/TOOL_ISSUE_ANALYSIS.md`
- Shell Template: `scripts/wave2/phase0_message_template_shell.txt`
- Solution Doc: `scripts/wave2/SOLUTION_SHELL_COMMANDS.md`
- Test Log: User's feedback showing actual agent behavior