# Screen Session Script Protocol

**Version**: 1.0  
**Effective**: 2026-06-22  
**Status**: MANDATORY for all autonomous wave execution

## Critical Rule: No Heredocs in Screen Sessions

### The Problem

**Heredocs are INCOMPATIBLE with screen sessions when scripts contain nested heredocs.**

When a bash script is launched via `screen -dmS name bash -c "script.sh"`, any heredoc syntax in the script can fail with:
```
bash: -c: line N: syntax error: unexpected end of file
bash: -c: line N: warning: here-document at line 1 delimited by end-of-file
```

This occurs because:
1. Screen wraps the script in its own command context
2. Bash heredoc delimiters can be misinterpreted
3. Nested heredocs (heredoc containing heredoc examples) ALWAYS fail
4. The error is silent until runtime (no syntax check catches it)

### Wave 7 Incident

**Date**: 2026-06-22  
**Impact**: 72/161 epics failed (44.7% failure rate)  
**Root Cause**: `generate_phase0_scripts.py` used heredoc to create message files  
**Message Content**: Contained heredoc examples for Bob CLI instructions  
**Result**: Nested heredoc syntax error in screen sessions

### The Solution

**NEVER use heredocs in scripts launched via screen. Use Python/file writing instead.**

#### ❌ WRONG (Heredoc in Screen Script)
```bash
#!/bin/bash
# This WILL FAIL in screen sessions
cat > /tmp/message.txt << 'EOF'
Your message here
EOF
command "$(cat /tmp/message.txt)"
```

#### ✅ CORRECT (Python File Writing)
```python
# generate_scripts.py
message = """Your message here"""
with open(f'/tmp/message_{epic_num}.txt', 'w', encoding='utf-8') as f:
    f.write(message)

# Script template (no heredocs)
script = f"""#!/bin/bash
command "$(cat /tmp/message_{epic_num}.txt)"
"""
```

## Pre-Launch Syntax Validation

### Mandatory Validation Step

**ALL generated scripts MUST pass syntax validation before launch.**

Add this to every script generator:

```python
def validate_script_syntax(script_path: str) -> bool:
    """Validate bash script syntax using bash -n"""
    result = subprocess.run(
        ['bash', '-n', script_path],
        capture_output=True,
        text=True
    )
    if result.returncode != 0:
        print(f"[ERROR] Syntax error in {script_path}:")
        print(result.stderr)
        return False
    return True

# After generating all scripts
print("\n[*] Validating script syntax...")
failed = []
for epic_num in epic_numbers:
    script_path = f"scripts/wave7/_p0_{epic_num:03d}.sh"
    if not validate_script_syntax(script_path):
        failed.append(epic_num)

if failed:
    print(f"\n[ERROR] {len(failed)} scripts failed syntax validation:")
    print(f"  {failed}")
    sys.exit(1)

print(f"[OK] All {len(epic_numbers)} scripts passed syntax validation")
```

### Integration Points

1. **Script Generators**: Add validation after generation, before writing
2. **Launch Scripts**: Validate before launching screen sessions
3. **Recovery Scripts**: Validate regenerated scripts before re-launch
4. **CI/CD**: Add syntax check to pre-commit hooks

## Incremental Rollout Strategy

### Pilot → Batch → Full Wave

**NEVER launch all epics at once without validation.**

#### Phase 1: Pilot (3 epics)
```bash
# Test low/medium/high complexity
./scripts/wave7/launch_phase0_pilot.sh
# Wait 5 minutes, verify all 3 complete
```

#### Phase 2: First Batch (10 epics)
```bash
# Launch first 10 epics
for i in {001..010}; do
    screen -dmS "p0-$i" bash -c "./scripts/wave7/_p0_$i.sh"
    sleep 12
done
# Wait 10 minutes, verify all 10 complete
```

#### Phase 3: Full Wave (remaining epics)
```bash
# Only proceed if pilot + batch succeeded
./scripts/wave7/launch_phase0_all.sh
```

### Rollout Checklist

- [ ] Syntax validation passed for all scripts
- [ ] Pilot test (3 epics) completed successfully
- [ ] First batch (10 epics) completed successfully
- [ ] No syntax errors in logs
- [ ] No heredoc-related failures
- [ ] Proceed to full wave launch

## Screen Session Best Practices

### 1. Simple Script Structure
```bash
#!/bin/bash
set -e
cd /path/to/repo

# Set environment
export VAR=value

# Create directories
mkdir -p logs

# Execute command (reference pre-written files)
command "$(cat /tmp/input.txt)" 2>&1 | tee logs/output.log
```

### 2. Avoid Complex Bash Features
- ❌ Heredocs (`cat > file << EOF`)
- ❌ Process substitution (`<(command)`)
- ❌ Complex parameter expansion (`${var//pattern/replacement}`)
- ✅ Simple variable substitution (`$VAR`, `${VAR}`)
- ✅ Command substitution (`$(cat file)`)
- ✅ Basic redirects (`2>&1`, `| tee`)

### 3. Pre-Write All Input Files
```python
# In Python generator
for epic_num in range(1, 162):
    # Write message file
    with open(f'/tmp/phase0_msg_{epic_num:03d}.txt', 'w') as f:
        f.write(message_content)
    
    # Script only references file
    script = f"""#!/bin/bash
    command "$(cat /tmp/phase0_msg_{epic_num:03d}.txt)"
    """
```

## Wave 7 Template Updates

### All Phase Templates Must Follow This Pattern

**Before** (Wave 4-6, DEPRECATED):
```bash
cat > /tmp/message.txt << 'EOFMSG'
Message content here
EOFMSG
bob --yolo "$(cat /tmp/message.txt)"
```

**After** (Wave 7+, MANDATORY):
```bash
# Message file created by Python generator, not bash heredoc
bob --yolo "$(cat /tmp/phase0_msg_${EPIC_NUM}.txt)"
```

### Template Audit Checklist

- [ ] Phase 0: No heredocs ✅ (fixed)
- [ ] Phase 1: No heredocs (needs update)
- [ ] Phase 1.5: No heredocs (needs update)
- [ ] Phase 2: No heredocs (needs update)
- [ ] Phase 3: No heredocs ✅ (fixed)
- [ ] Phase 4: No heredocs (needs update)
- [ ] Phase 5: No heredocs (needs update)
- [ ] Phase 5.V: No heredocs (needs update)
- [ ] Phase 6: No heredocs (needs update)

## Recovery Protocol

### When Heredoc Failures Occur

1. **Identify Failed Epics**:
   ```bash
   grep -l 'syntax error\|unexpected end of file' logs/phase*/*.log
   ```

2. **Create Fixed Generator**:
   - Copy original generator to `*_fixed.py`
   - Replace ALL heredocs with Python file writing
   - Add syntax validation

3. **Regenerate Scripts**:
   ```bash
   python3 scripts/wave7/generate_phase0_scripts_fixed.py --failed-only
   ```

4. **Validate Before Re-Launch**:
   ```bash
   for script in scripts/wave7/_p0_*.sh; do
       bash -n "$script" || echo "FAILED: $script"
   done
   ```

5. **Clean Lamport Clock**:
   ```bash
   # Remove old events for failed epics
   # (handled by recover_failed_phase0.sh)
   ```

6. **Re-Launch**:
   ```bash
   ./scripts/wave7/recover_failed_phase0.sh
   ```

## Enforcement

### Pre-Commit Hook
```bash
#!/bin/bash
# .git/hooks/pre-commit

# Check for heredocs in wave scripts
if git diff --cached --name-only | grep -q 'scripts/wave.*\.sh$'; then
    if git diff --cached | grep -q '<<.*EOF'; then
        echo "ERROR: Heredoc detected in wave script"
        echo "Use Python file writing instead"
        exit 1
    fi
fi
```

### Code Review Checklist
- [ ] No heredocs in generated scripts
- [ ] Syntax validation included in generator
- [ ] Pilot test before full launch
- [ ] Incremental rollout strategy documented
- [ ] Recovery procedure tested

## References

- **Wave 7 Recovery**: `scripts/wave7/RECOVERY_STATUS.md`
- **Fixed Generator**: `scripts/wave7/generate_phase0_scripts_fixed.py`
- **Recovery Script**: `scripts/wave7/recover_failed_phase0.sh`
- **SOP Update**: `docs/workflow/WAVE_PHASE_SCRIPT_GENERATION_SOP_V3.md` (needs update)

## Success Metrics

### Wave 7 Results
- **Before Fix**: 72/161 failures (44.7%)
- **After Fix**: 0/72 failures (0%) - all recovered successfully
- **Validation**: 100% syntax check pass rate
- **Cost Impact**: ~$1.44 wasted (5.6% of Phase 0 cost)

### Future Waves
- **Target**: 0% heredoc-related failures
- **Validation**: 100% pre-launch syntax check
- **Rollout**: Pilot → Batch → Full (no failures)

---

**Last Updated**: 2026-06-22 00:56 UTC  
**Next Review**: After Wave 7 completion