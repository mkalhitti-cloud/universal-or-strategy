# V12 Refactoring Anti-Patterns

**Purpose**: Quick reference for "what NOT to do" during refactoring epics.  
**Audience**: All agents (Bob CLI, Codex CLI, Gemini CLI, Claude, etc.)  
**Status**: Living document - update as new anti-patterns are discovered  
**Last Updated**: 2026-06-02

---

## Overview

This guide documents anti-patterns that violate V12 DNA principles, Jane Street alignment, or operational protocols. Use this as a pre-flight checklist before any refactoring epic.

**Golden Rule**: When in doubt, prefer boring code over clever abstractions.

---

## Anti-Pattern Categories

### 1. Allocation Anti-Patterns (Jane Street Violations)

These patterns introduce heap allocation in hot paths, violating the Jane Street zero-allocation principle.

#### 1.1 Arrays in Hot Paths
```csharp
// ❌ BANNED - Array allocation
private int[] _targetContracts = new int[5];

private int GetTargetContracts(int targetNumber)
{
    return _targetContracts[targetNumber - 1]; // Bounds check allocates
}
```

**Why banned**:
- Array indexing requires bounds checking (allocation)
- Array initialization allocates on heap
- PR #14 specifically reverted from arrays for this reason

**Alternative**:
```csharp
// ✅ CORRECT - Switch-based (zero allocation)
private int GetTargetContracts(PositionInfo pos, int targetNumber)
{
    switch (targetNumber)
    {
        case 1: return pos.T1Contracts;
        case 2: return pos.T2Contracts;
        // ... etc
        default: return 0;
    }
}
```

#### 1.2 Dictionary Lookups
```csharp
// ❌ BANNED - Dictionary allocation
private Dictionary<int, Func<PositionInfo, int>> _accessors = new()
{
    { 1, pos => pos.T1Contracts },
    { 2, pos => pos.T2Contracts },
    // ... etc
};

private int GetTargetContracts(PositionInfo pos, int targetNumber)
{
    return _accessors[targetNumber](pos); // Allocates on miss
}
```

**Why banned**:
- Dictionary miss allocates exception
- Func delegates allocate on creation
- Lookup overhead vs direct field access

**Alternative**: Use switch statements (zero allocation).

#### 1.3 Reflection-Based Access
```csharp
// ❌ BANNED - Reflection allocates and is slow
private int GetTargetContracts(PositionInfo pos, int targetNumber)
{
    var propName = $"T{targetNumber}Contracts";
    var prop = typeof(PositionInfo).GetProperty(propName);
    return (int)prop.GetValue(pos); // Allocates + boxes
}
```

**Why banned**:
- `GetProperty()` allocates
- `GetValue()` boxes value types
- 100x slower than direct field access
- String interpolation allocates

**Alternative**: Use switch statements or compile-time code generation.

#### 1.4 LINQ in Performance-Critical Code
```csharp
// ❌ BANNED - LINQ allocates enumerators
private int SumTargetContracts(PositionInfo pos)
{
    return new[] { pos.T1Contracts, pos.T2Contracts, pos.T3Contracts }
        .Where(x => x > 0)
        .Sum(); // Multiple allocations
}
```

**Why banned**:
- Array creation allocates
- LINQ enumerators allocate
- Lambda closures may allocate

**Alternative**:
```csharp
// ✅ CORRECT - Manual loop (zero allocation)
private int SumTargetContracts(PositionInfo pos)
{
    int sum = 0;
    if (pos.T1Contracts > 0) sum += pos.T1Contracts;
    if (pos.T2Contracts > 0) sum += pos.T2Contracts;
    if (pos.T3Contracts > 0) sum += pos.T3Contracts;
    return sum;
}
```

---

### 2. Scope Creep Anti-Patterns (V12.23 Violations)

These patterns violate the "ONE EPIC = ONE CONCERN" principle.

#### 2.1 "While We're Here" Fixes
```csharp
// ❌ SCOPE CREEP - Mixing concerns
// EPIC-POSINFO: Refactor 6 accessor methods
private int GetTargetContracts(...) { /* refactored */ }
private double GetTargetPrice(...) { /* refactored */ }

// ❌ Don't also refactor these unrelated methods:
private TargetMode GetTargetMode(...) { /* also refactored */ } // Different concern!
private bool IsRunnerTarget(...) { /* also refactored */ } // Different concern!
```

**Why banned**:
- Violates V12.23 No Scope Creep Protocol
- Causes PR bloat (>10k char diff limit)
- Mixes multiple concerns in one PR
- Makes review and rollback harder

**Correct approach**:
- Create separate epic for each concern
- One PR per epic
- Document dependencies between epics

#### 2.2 Struct/Class Modification During Refactoring
```csharp
// ❌ SCOPE CREEP - Changing data structures
// EPIC-POSINFO: Refactor accessor methods

// ❌ Don't also change the struct:
public struct PositionInfo
{
    // Replacing 15 individual fields with an array
    public int[] TargetContracts; // ❌ Affects 100+ call sites!
}
```

**Why banned**:
- Requires updating all call sites (100+ in this case)
- Different concern from accessor refactoring
- Requires separate epic (e.g., EPIC-POSINFO-STRUCT)

**Correct approach**:
- Keep struct unchanged
- Refactor only the accessor methods
- Create separate epic for struct changes if needed

#### 2.3 Expanding Scope Mid-Epic
```csharp
// ❌ SCOPE CREEP - Expanding during implementation
// Original scope: Refactor 6 accessor methods

// ❌ Don't expand to:
// - Fix pre-existing compilation errors
// - Add new features
// - Refactor adjacent code
// - Update documentation (unless part of original scope)
```

**Why banned**:
- Causes epic drift
- Makes rollback impossible
- Violates planning phase agreements

**Correct approach**:
- STOP immediately if scope expansion needed
- Report to Director
- Create separate PR for new concern
- Restart original epic cleanly

---

### 3. Complexity Anti-Patterns

These patterns add unnecessary complexity without proven benefit.

#### 3.1 Clever Abstractions Over Boring Code
```csharp
// ❌ OVER-ENGINEERING - Complex abstraction
public interface IFieldAccessor<T>
{
    T GetValue(PositionInfo pos, int index);
    void SetValue(PositionInfo pos, int index, T value);
}

public class ContractsAccessor : IFieldAccessor<int>
{
    public int GetValue(PositionInfo pos, int index) { /* complex logic */ }
    public void SetValue(PositionInfo pos, int index, int value) { /* complex logic */ }
}

// ❌ Factory pattern for simple accessors
public class FieldAccessorFactory
{
    public IFieldAccessor<T> Create<T>(string fieldType) { /* complex logic */ }
}
```

**Why banned**:
- Jane Street principle: "Boring code is good code"
- Adds cognitive overhead
- No performance benefit over simple switch
- Harder to debug and maintain

**Alternative**:
```csharp
// ✅ CORRECT - Boring switch statement
private int GetTargetContracts(PositionInfo pos, int targetNumber)
{
    switch (targetNumber)
    {
        case 1: return pos.T1Contracts;
        case 2: return pos.T2Contracts;
        // ... simple and obvious
    }
}
```

#### 3.2 Premature Optimization
```csharp
// ❌ PREMATURE - Unsafe code without proven need
private unsafe int GetTargetContracts(PositionInfo* pos, int targetNumber)
{
    fixed (int* ptr = &pos->T1Contracts)
    {
        return *(ptr + (targetNumber - 1)); // Pointer arithmetic
    }
}
```

**Why banned**:
- Current switch is already zero-allocation
- Unsafe code adds complexity
- No proven performance benefit
- Harder to maintain and audit

**Correct approach**:
- Measure first (benchmark)
- Optimize only if proven bottleneck
- Document performance justification

#### 3.3 Nested Abstractions
```csharp
// ❌ OVER-ENGINEERING - Multiple abstraction layers
public class TargetAccessorStrategy
{
    private IFieldSelector _selector;
    private IValueConverter _converter;
    private IValidationPolicy _validator;
    
    public T GetValue<T>(PositionInfo pos, int targetNumber)
    {
        var field = _selector.SelectField(pos, targetNumber);
        var value = _converter.Convert<T>(field);
        return _validator.Validate(value) ? value : default;
    }
}
```

**Why banned**:
- Violates YAGNI (You Aren't Gonna Need It)
- Adds cognitive overhead
- No clear benefit over simple switch
- Makes debugging harder

**Alternative**: Keep it simple - use switch statements.

---

### 4. Lock-Based Anti-Patterns (V12 DNA Violations)

These patterns violate the lock-free Actor/FSM mandate.

#### 4.1 Lock Statements
```csharp
// ❌ BANNED - Lock statements forbidden
private readonly object _stateLock = new object();

private void UpdateTarget(int targetNumber, int value)
{
    lock (_stateLock) // ❌ STRICTLY BANNED
    {
        // ... state mutation
    }
}
```

**Why banned**:
- Violates V12 DNA lock-free mandate
- Legacy pattern from pre-V12 code
- Causes contention and deadlocks

**Alternative**:
```csharp
// ✅ CORRECT - FSM/Actor Enqueue model
private void UpdateTarget(int targetNumber, int value)
{
    _actor.Enqueue(() =>
    {
        // ... state mutation (serialized by actor)
    });
}
```

#### 4.2 Mutex/Semaphore Primitives
```csharp
// ❌ BANNED - Synchronization primitives
private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

private async Task UpdateTargetAsync(int targetNumber, int value)
{
    await _semaphore.WaitAsync(); // ❌ BANNED
    try
    {
        // ... state mutation
    }
    finally
    {
        _semaphore.Release();
    }
}
```

**Why banned**:
- Violates lock-free mandate
- Async overhead in hot paths
- Potential deadlocks

**Alternative**: Use Actor/FSM pattern with message queue.

---

### 5. Style Anti-Patterns

These patterns violate V12 coding standards.

#### 5.1 Unicode/Emoji in Code
```csharp
// ❌ BANNED - Unicode characters
private string GetStatus()
{
    return "✅ Success"; // ❌ Unicode emoji
}

private string GetMessage()
{
    return "Target "filled""; // ❌ Curly quotes
}
```

**Why banned**:
- Violates ASCII-only mandate
- Causes encoding issues
- Breaks pre-push validation

**Alternative**:
```csharp
// ✅ CORRECT - ASCII only
private string GetStatus()
{
    return "[OK] Success";
}

private string GetMessage()
{
    return "Target \"filled\""; // Straight quotes
}
```

#### 5.2 Missing Braces
```csharp
// ❌ STYLE VIOLATION - Missing braces
if (targetNumber < 1 || targetNumber > 5)
    return 0; // ❌ No braces

for (int i = 0; i < 5; i++)
    ProcessTarget(i); // ❌ No braces
```

**Why banned**:
- V12 DNA mandates braces for all control structures
- CSharpier auto-fixes this
- Prevents subtle bugs

**Alternative**:
```csharp
// ✅ CORRECT - Always use braces
if (targetNumber < 1 || targetNumber > 5)
{
    return 0;
}

for (int i = 0; i < 5; i++)
{
    ProcessTarget(i);
}
```

---

## Pre-Flight Checklist

Before starting any refactoring epic, verify:

### Allocation Check
- [ ] No arrays in hot paths
- [ ] No dictionaries for field access
- [ ] No reflection-based access
- [ ] No LINQ in performance-critical code

### Scope Check
- [ ] ONE EPIC = ONE CONCERN
- [ ] No "while we're here" fixes
- [ ] No struct/class modifications (unless that's the epic)
- [ ] Scope boundaries documented in `00-scope.md`

### Complexity Check
- [ ] No clever abstractions without justification
- [ ] No premature optimization
- [ ] No nested abstraction layers
- [ ] CYC ≤ 15 per method (Jane Street threshold)

### V12 DNA Check
- [ ] No lock statements
- [ ] No mutex/semaphore primitives
- [ ] ASCII-only strings
- [ ] Braces on all control structures

### Process Check
- [ ] Scope document created (`00-scope.md`)
- [ ] Pattern analysis documented (`01-pattern-analysis.md`)
- [ ] Anti-patterns reviewed (this document)
- [ ] Director confirmation obtained

---

## Recovery Protocol

If anti-pattern detected during epic:

1. **STOP immediately** - Do not proceed with implementation
2. **Document violation** in `docs/brain/EPIC-X/failure-analysis.md`
3. **Close PR** if already created
4. **Report to Director** with violation details
5. **Create separate PR** for each concern if scope creep
6. **Restart epic cleanly** after fixing violation

---

## References

- **V12.23 No Scope Creep Protocol**: [`docs/brain/EPIC-13/09-pr12-failure-analysis.md`](../brain/EPIC-13/09-pr12-failure-analysis.md)
- **Jane Street Intel**: [`docs/intel/jane-street/`](../intel/jane-street/)
- **V12 DNA Constraints**: [`AGENTS.md`](../AGENTS.md#2-architectural-mandates-the-platinum-standard)
- **Lock-Free Actor Pattern**: [`docs/architecture.md`](architecture.md)
- **Pre-Push Validation**: [`scripts/pre_push_validation.ps1`](../scripts/pre_push_validation.ps1)

---

## Version History

- **2026-06-02**: Initial version (EPIC-POSINFO Phase 1.5)
- Future: Add new anti-patterns as discovered