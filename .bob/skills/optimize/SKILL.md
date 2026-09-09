---
name: optimize
description: >-
  M5 Branch Elimination -- replace if/switch chains with dictionary dispatch
  tables (Jane Street style).
metadata:
  user-invocable: true
  disable-model-invocation: true
  argument-hint: <file-path>
---

# MISSION: M5 Branch Elimination Pass
**Target File:** $1
**Build Tag:** 1111.007-phase7-t1
**Protocol:** V12 M5 Zero-Allocation / Jane Street Dispatch Pattern

---

## WHAT THIS OPTIMIZES

Replace dense `if`/`switch` chains in hot-path methods with pre-built
`Dictionary<string, Action>` or `Dictionary<string, Func<T,T>>` dispatch tables.

**BEFORE (branch-heavy):**
```csharp
if (action == "market") ExecuteTarget_Market(ctx);
else if (action == "onepoint") ExecuteTarget_OnePoint(ctx);
else if (action == "twopoint") ExecuteTarget_TwoPoint(ctx);
// ... 6+ branches
```

**AFTER (dispatch table):**
```csharp
private static readonly Dictionary<string, Action<ExecContext>> _targetHandlers
    = new Dictionary<string, Action<ExecContext>>
    {
        { "market",   ctx => ctx.ExecuteTarget_Market()    },
        { "onepoint", ctx => ctx.ExecuteTarget_OnePoint()  },
        { "twopoint", ctx => ctx.ExecuteTarget_TwoPoint()  },
    };
// Hot path: single dictionary lookup, zero branches
if (_targetHandlers.TryGetValue(action, out var handler)) handler(ctx);
```

**Benefits:**
- Zero branch misprediction on CPU hot path
- O(1) dispatch regardless of case count
- Easier to extend (add new case = add one dictionary entry)
- Aligns with Jane Street / HFT dispatch patterns

---

## STEP 1 -- SCAN FOR DISPATCH CANDIDATES

Use read_file on $1 to identify all switch/if-else chains where:
- The branching variable is a string, enum, or int action code
- Each branch calls a distinct named method (not inline logic)
- The method is called on the trading hot path (callbacks, OnBarUpdate, order handlers)
- The chain has >= 4 branches (below 4, a switch is fine)

Produce a candidate table:

```
| Method | Branch Type | Branch Count | Hot Path? | Candidate? |
|--------|-------------|--------------|-----------|------------|
| RouteTargetActionToHandler | string switch | 6 | YES | YES |
| DispatchRunnerAction | string switch | 5 | YES | YES |
```

---

## STEP 2 -- DISPATCH TABLE DESIGN

For each confirmed candidate, design the replacement:

```
| Method | Dictionary Key Type | Value Type | Static? | Init Location |
```

Rules:
- ALWAYS `private static readonly` for the dictionary (allocated once, zero GC)
- Key must be the EXACT type of the switch variable (string/enum/int)
- Value is `Action<TContext>` or `Func<TContext, TResult>` as appropriate
- Init in the class static initializer or field initializer (never in OnStateChange)
- NEVER allocate new delegates in the hot path -- delegates must be pre-stored

### !!! DIRECTOR APPROVAL GATE !!!
**STOP HERE. Do NOT change any code until the Director types: APPROVED**

Output: "[M5-GATE] Dispatch table design complete. Awaiting Director approval."

---

## STEP 3 -- SURGICAL REPLACEMENT (Only after APPROVED)

For each candidate:
1. Add `private static readonly Dictionary<...> _[name]Handlers = new Dictionary<...> { ... };`
   immediately above the method in the source file
2. Replace the switch/if body with a single `TryGetValue` + invocation
3. Keep the method signature IDENTICAL -- only the body changes
4. Add a fallback log for unknown keys: `Print("M5-WARN: unknown action: " + key);`
   (ASCII-only -- no Unicode in the Print string)

DNA rules during replacement:
- ZERO new lock() statements
- ZERO non-ASCII in string literals
- ZERO inline new() allocations in the hot-path method body
- Dictionary itself MUST be static readonly (not instance, not lazy)

---

## STEP 4 -- POST-EDIT DNA AUDIT (mandatory)

```powershell
# 4a: Re-establish hard links
powershell -File .\deploy-sync.ps1

# 4b: Lock regression
grep -r "lock(" src/

# 4c: Unicode regression
grep -Prn "[^\x00-\x7F]" src/

# 4d: Allocation regression (verify no new() in hot-path methods)
grep -n "new " src/$1
```

Report:
```
[M5-AUDIT]
Target: $1
deploy-sync.ps1: PASS / FAIL
lock() audit: CLEAN / [N]
Unicode audit: CLEAN / [N]
Dispatch tables added: [list]
Branches eliminated: [total count]
```

---

## STEP 5 -- HANDOFF

```
[M5-COMPLETE]
File: $1
Dispatch tables created: [list with key type + entry count]
Branches eliminated: [N total]
Status: READY FOR F5 COMPILE
```
