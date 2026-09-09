---
name: phase7
description: Execute Phase 7 Concurrency Hardening on a target file.
metadata:
  user-invocable: true
  disable-model-invocation: true
  argument-hint: <target-file-path>
---

# MISSION: Phase 7 Concurrency Hardening
**Target File:** $1
**Build Tag:** 1111.006-phase-6-t0
**Protocol:** V12 Photon Kernel DNA (Lock-Free Actor / Zero-Allocation Hot Path)

---

## STEP 1 -- MANDATORY ANALYSIS (DO NOT SKIP OR REORDER)

Run the following analysis tools IN ORDER before writing any code:

### 1a. jCodemunch Structural Scan
Using jCodemunch MCP tools:
- `get_file_outline` on `$1` -- map every symbol, its signature, complexity score
- `get_blast_radius` on the highest-complexity method -- identify all downstream callers
- `find_references` on any dictionary or collection field accessed in the hot path

### 1b. Context7 Doc Load
Using the Context7 tool defined in settings.json:
- Load docs for: `System.Threading.Channels`
- Load docs for: `System.Threading.Interlocked`
- Load docs for: `System.Threading.Volatile`
- Confirm which .NET 4.8 primitives are available (NinjaTrader 8 target)

### 1c. Graphify Caller Map
Run: `graphify update .`
Then read `graphify-out/GRAPH_REPORT.md` to identify:
- Which files import or call the target method
- Whether any callers hold state that must be migrated to the lock-free model

---

## STEP 2 -- WRITE THE LOCK-FREE IMPLEMENTATION PLAN

Produce a written plan with the following structure:

```
## Phase 7 Plan: [target file name]
### Bottlenecks Found
| Method | Issue | Lock/Dict/Sequential? |
|--------|-------|----------------------|
| ...    | ...   | ...                   |

### Proposed Refactoring
| Before (Banned Pattern) | After (Approved Primitive) |
|-------------------------|---------------------------|
| lock(stateLock) { ... } | Interlocked.CompareExchange / Enqueue FSM |
| Dictionary<K,V> in hot path | Channel<T> or SPSC ring buffer |
| blocking wait / Thread.Sleep | Volatile.Read spin-check + MemoryBarrier |

### Surgical Edit Plan
1. [File] [Method] -- [exact change described]
2. [File] [Method] -- [exact change described]
```

### !!! DIRECTOR APPROVAL GATE !!!
**STOP HERE. Do NOT proceed to Step 3 until the Director explicitly types: APPROVED**

If the Director has not typed APPROVED, output:
"[PHASE7-GATE] Plan complete. Awaiting Director approval before surgical execution."

---

## STEP 3 -- SURGICAL EXECUTION (Only after APPROVED)

Apply the approved plan using surgical edits:
- Use `replace_file_content` with exact `TargetContent` matching the current file
- Touch ONLY the methods identified in Step 2
- NEVER mutate whitespace, indentation, or adjacent unrelated code
- After each file edit, pause and confirm the change is syntactically valid C# 8.0

### APPROVED PRIMITIVES WHITELIST
The following are the ONLY lock-free constructs permitted:
- `System.Threading.Volatile.Read<T>()` / `Volatile.Write<T>()`
- `System.Threading.Interlocked.CompareExchange()` / `.Increment()` / `.Add()`
- `System.Threading.Channels.Channel<T>` (unbounded or bounded)
- `Thread.MemoryBarrier()` -- ONLY at ring buffer head/tail transitions
- Cache-line padding: `[StructLayout(LayoutKind.Explicit)]` with `[FieldOffset(64)]`

### BANNED PATTERNS (immediate halt if you are about to write these)
- `lock(anything)` -- BANNED
- `Monitor.Enter` / `Monitor.Exit` -- BANNED
- `Mutex` / `SemaphoreSlim` (blocking Wait) -- BANNED
- `Dictionary<K,V>` writes without Interlocked guard -- BANNED
- `Thread.Sleep()` in hot path -- BANNED
- Unicode / emoji / curly quotes in any string literal -- BANNED
- Diff markers (`<<<<<<<`, `=======`, `>>>>>>>`) in tool calls -- BANNED

---

## STEP 4 -- POST-EDIT DNA AUDIT (Mandatory after every src/ change)

Run these commands in sequence and report ALL results to Director:

```powershell
# Step 4a: Re-establish hard links and run ASCII gate
powershell -File .\deploy-sync.ps1

# Step 4b: Lock regression audit (must return ZERO matches)
grep -r "lock(" src/

# Step 4c: Unicode regression audit (must return ZERO matches)
grep -Prn "[^\x00-\x7F]" src/
```

Report format to Director:
```
[PHASE7-AUDIT]
Target: $1
deploy-sync.ps1: PASS / FAIL
lock() audit: [N matches -- list them] / CLEAN
Unicode audit: [N matches -- list them] / CLEAN
BUILD_TAG (from NinjaTrader banner): [value]
```

**If ANY audit fails: HALT. Report failure. Do NOT notify Director of completion.**

---

## STEP 5 -- HANDOFF TO DIRECTOR

Only after all Step 4 audits PASS, output:

```
[PHASE7-COMPLETE]
File: $1
Status: READY FOR F5 COMPILE
Action: Press F5 in NinjaTrader IDE to compile and verify BUILD_TAG banner.
Next Target: [suggest next file from hotspot map if applicable]
```
