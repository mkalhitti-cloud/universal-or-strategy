# Antigravity: Wave 2 Script Execution Failure - Need Fix

## Success So Far

✅ **Your SCP + execute pattern WORKED!**
- SSH key cache cleared successfully
- Script uploaded via `gcloud compute scp` (no quote issues)
- Script executed via `gcloud compute ssh --command="bash /tmp/script.sh"` (no quote issues)
- 10 screen sessions launched

**Quote escaping problem is SOLVED!** 🎉

## New Problem

❌ **The Bob Shell agents failed immediately** - no log files created

## Evidence

**Expected**: 10 log files in `/home/malhitticrypto/universal-or-strategy/logs/`
- EPIC-CCN-109.log
- EPIC-CCN-129.log
- EPIC-CCN-1.log
- ... (7 more)

**Actual**: Only old logs exist
```
-rw-rw-r-- 1 malhitticrypto malhitticrypto  56 Jun 12 08:07 EPIC-CCN-107.log
-rw-rw-r-- 1 malhitticrypto malhitticrypto 13K Jun 12 09:16 EPIC-CCN-16.log
-rw-rw-r-- 1 malhitticrypto malhitticrypto  55 Jun 12 08:07 EPIC-CCN-164.log
```

**Screen sessions**: All exited immediately (no sockets found)

## The Generated Script

**Python code** (Windows, Python 3.12):
```python
def build_wave_script(epics: list[str]) -> str:
    lines = [
        "#!/bin/bash",
        "set -eu",
        "",
        f"REPO={REPO}",  # REPO = "/home/malhitticrypto/universal-or-strategy"
        "mkdir -p $REPO/logs",
        "",
        "git config --global user.email 'malhitticrypto@gmail.com'",
        "git config --global user.name 'malhitticrypto'",
        "",
        "cd $REPO && git pull --ff-only origin main || true",
        "",
        "echo '[WAVE2] Launching parallel Bob Shell agents...'",
    ]

    for epic in epics:
        session = f"v12-{epic}"
        log = f"$REPO/logs/{epic}.log"
        lines.append(
            f"screen -dmS {session} "
            f"bash -l -c 'cd $REPO && "
            f"bob --accept-license --max-coins {MAX_COINS} "
            f"-p \"Run epic-intake for {epic}\" "
            f"> {log} 2>&1; "
            f"echo DONE_EXIT=$? >> {log}'"
        )
        lines.append(f"echo '[WAVE2] Launched: {epic}'")
        lines.append("sleep 1")

    return "\n".join(lines)
```

**Saved to**: `scripts/wave2/_wave2_launch_generated.sh` (Windows file system)

## Errors Observed

From the SSH output:
```
/tmp/wave2_launch.sh: line 4: set: -: invalid option name
/tmp/wave2_launch.sh: line 5: $'\r': command not found
/tmp/wave2_launch.sh: line 8: $'\r': command not found
/tmp/wave2_launch.sh: line 12: $'\r': command not found
fatal: not a git repository (or any of the parent directories): .git
/tmp/wave2_launch.sh: line 14: $'true\r': command not found
sleep: invalid time interval '1\r'
```

## Root Causes Identified

1. **CRLF Line Endings** (`\r\n` instead of `\n`)
   - Windows Python writes CRLF by default
   - Linux bash chokes on `\r` characters
   - Every line has `$'\r': command not found`

2. **Git Repo Path Issue**
   - `fatal: not a git repository`
   - Script tried to `cd $REPO && git pull` but failed
   - Possible: `$REPO` variable not expanded correctly due to CRLF

3. **Screen Sessions Exited Immediately**
   - No logs created = Bob never ran
   - Likely: `bash -l` didn't load PATH or `BOBSHELL_API_KEY`
   - Or: Command syntax broken by CRLF

## Your Smoke Test (That Worked)

**Your script** (`smoke_test.sh`):
```bash
#!/bin/bash
cd ~/universal-or-strategy || exit 1
mkdir -p logs
screen -dmS v12-EPIC-CCN-16 bash -l -c "
  bob --accept-license --max-coins 30 \
    -p 'Run epic-intake for EPIC-CCN-16' \
    > logs/EPIC-CCN-16.log 2>&1
"
sleep 2
screen -ls
```

**Result**: ✅ Worked perfectly - Bob ran, log created, epic completed

## The Question

**How do we fix the Python-generated script to work like your smoke test?**

## Specific Issues to Solve

1. **Line Endings**: How to force Python on Windows to write LF (`\n`) only?
2. **Variable Expansion**: Why did `$REPO` fail but `~/universal-or-strategy` worked?
3. **Screen Command**: Your syntax used `\` for line continuation - should we do that?
4. **Path Loading**: Your `bash -l` worked - why didn't ours?

## Constraints

- Must run from Windows laptop (Python 3.12)
- Must generate script dynamically (10 epics, not hardcoded)
- Must work with `gcloud compute scp` + `gcloud compute ssh --command`
- Must be repeatable for Wave 3, 4, 5...

## Your Mission

**Provide the corrected Python code** that:
1. Writes LF line endings (not CRLF)
2. Uses paths that work (like your `~/universal-or-strategy`)
3. Uses screen syntax that works (like your smoke test)
4. Generates a script that will actually run Bob Shell successfully

**Bonus**: Explain WHY your smoke test worked but our generated script failed.

## Current Status

- VM: `v12-test-golden-v2` (still running, $0.093/hour)
- Cost so far: $62.42
- Ready to test your fix immediately

---

**What changes should we make to `launch_wave_now.py` to fix these issues?**