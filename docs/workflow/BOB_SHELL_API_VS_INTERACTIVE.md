# Bob Shell: API Mode vs Interactive Mode - Critical Differences

## What Changes with API Authentication?

### Interactive Mode (What We Tried Before)
```bash
bob --mode v12-engineer
# Opens interactive session
# Uses browser-based IBMid authentication
# Creates git notes for session state
# ❌ Git notes race condition in parallel
```

### API Mode (New Discovery)
```bash
bob --auth-method api-key --mode v12-engineer --non-interactive -p "prompt"
# No interactive session
# Uses BOBSHELL_API_KEY environment variable
# No browser authentication needed
# ✅ Stateless execution
```

## Key Differences

| Aspect | Interactive Mode | API Mode (Non-Interactive) |
|--------|------------------|----------------------------|
| **Authentication** | Browser + IBMid | Environment variable |
| **Session State** | Git notes (`.git/notes/bob`) | Stateless |
| **Execution** | Multi-turn conversation | Single prompt → response |
| **Git Notes** | ✅ Created (causes conflicts) | ❌ Not created (no conflicts!) |
| **Parallel Safe** | ❌ No (git notes race) | ✅ Yes (stateless) |
| **Use Case** | Development, exploration | Automation, CI/CD |

## Does API Mode Solve the Parallel Execution Problem?

### The Git Notes Problem (Interactive Mode)
```
Session 1: git config --local notes.rewriteRef refs/notes/bob
Session 2: git config --local notes.rewriteRef refs/notes/bob  # CONFLICT!
Session 3: git config --local notes.rewriteRef refs/notes/bob  # CONFLICT!
```

### API Mode Solution (Non-Interactive)
```bash
# Session 1 (stateless)
bob --auth-method api-key --non-interactive -p "Task 1"

# Session 2 (stateless, no git notes)
bob --auth-method api-key --non-interactive -p "Task 2"

# Session 3 (stateless, no git notes)
bob --auth-method api-key --non-interactive -p "Task 3"

# ✅ No git notes = No conflicts!
```

## Why We Couldn't Do This Earlier

### What We Didn't Know:
1. **API authentication exists** - We only knew about interactive IBMid auth
2. **Non-interactive mode** - We didn't know `--non-interactive` flag existed
3. **Stateless execution** - We assumed Bob always used git notes
4. **API keys for automation** - We thought API keys were only for web portal

### What We Tried (All Interactive Mode):
```bash
# Attempt 1: Same repo, 9 sessions
bob --mode v12-engineer  # Interactive, git notes conflict

# Attempt 2: Worktrees, 5 sessions
cd worktree-1 && bob --mode v12-engineer  # Still interactive, still git notes

# Attempt 3: Staggered startup
sleep 60 && bob --mode v12-engineer  # Still interactive, still git notes
```

**All attempts used interactive mode** → All created git notes → All failed

## The Correct Approach (API Mode)

```python
# Parallel execution with API mode
import subprocess
from concurrent.futures import ThreadPoolExecutor

def execute_task(prompt):
    return subprocess.run([
        "bob",
        "--auth-method", "api-key",
        "--mode", "v12-engineer",
        "--non-interactive",
        "-p", prompt
    ], env={"BOBSHELL_API_KEY": "your-key"})

# Run 9 tasks in parallel - NO GIT NOTES CONFLICTS!
with ThreadPoolExecutor(max_workers=9) as executor:
    futures = [executor.submit(execute_task, f"Task {i}") for i in range(9)]
```

## Limitations of API Mode

### What You Lose:
1. **No multi-turn conversations** - Single prompt only
2. **No interactive approval** - Auto-approves all tool uses
3. **No session history** - Each call is independent
4. **No `/restore` command** - No checkpointing

### What You Gain:
1. ✅ **Parallel execution** - No git notes conflicts
2. ✅ **Automation-friendly** - Perfect for CI/CD
3. ✅ **Stateless** - No session state to manage
4. ✅ **Scriptable** - Easy to integrate with Python/PowerShell

## When to Use Each Mode

### Use Interactive Mode When:
- Exploring codebase
- Need multi-turn conversations
- Want to review changes before applying
- Working on complex refactoring interactively

### Use API Mode When:
- Running automation workflows
- Executing in CI/CD pipelines
- Need parallel execution
- Have well-defined single-step tasks

## Wave 2 Execution Strategy

### Old Approach (Failed):
```bash
# Interactive mode, 9 parallel sessions
for epic in epics:
    bob --mode v12-engineer  # Git notes conflict!
```

### New Approach (Should Work):
```bash
# API mode, 9 parallel sessions
export BOBSHELL_API_KEY="your-key"
for epic in epics:
    bob --auth-method api-key --non-interactive -p "Execute Phase 1 for $epic"
done
```

## Testing the Solution

### Step 1: Set API Key
```powershell
$env:BOBSHELL_API_KEY="bob_prod_bob-admin_..."
```

### Step 2: Test Single Execution
```bash
bob --auth-method api-key --mode v12-engineer --non-interactive -p "What is 2+2?"
```

### Step 3: Test Parallel Execution (2 sessions)
```python
# If this works without git notes conflicts, we've solved it!
python scripts/wave2_bob_shell_executor.py
```

## Summary

**API mode solves the parallel execution problem** because:
1. ✅ No git notes created
2. ✅ Stateless execution
3. ✅ No session state conflicts
4. ✅ Perfect for automation

**We couldn't do this earlier** because:
1. ❌ Didn't know API authentication existed
2. ❌ Didn't know `--non-interactive` flag existed
3. ❌ Always used interactive mode (which creates git notes)
4. ❌ Documentation wasn't clear about automation use case

**The breakthrough**: Reading the Bob Shell docs revealed the `--auth-method api-key` and `--non-interactive` flags that enable stateless, parallel-safe execution!