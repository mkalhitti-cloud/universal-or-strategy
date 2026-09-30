# Jane Street P0 Violation Categories Explained

**Generated**: 2026-06-18
**Total P0 Violations**: 299 across 52 files

## Overview

The 299 Jane Street P0 violations in V12 fall into 4 categories based on Jane Street's high-frequency trading (HFT) principles. These rules come from analyzing Jane Street's public GitHub repositories and technical talks.

---

## 1. Philosophy (223 violations, 74.6%)

**What it is**: Core design principles that make code predictable and maintainable under microsecond latency constraints.

### Primary Violation: Magic Numbers (JS-100)

**Rule**: No magic numbers - use named constants

**Why Jane Street cares**:
- In HFT, every number has trading significance (tick sizes, lot sizes, risk limits)
- Magic numbers make it impossible to audit risk parameters
- Constants enable compile-time verification and easy parameter sweeps

**Example from V12**:
```csharp
// ❌ VIOLATION
if (price > 0.25) { ... }

// ✅ FIXED
private const decimal MIN_TICK_SIZE = 0.25m;
if (price > MIN_TICK_SIZE) { ... }
```

**Impact**: 223 violations means ~223 hardcoded numbers that should be named constants. This is the bulk of our work.

**Fix Strategy**: During refactoring, extract every magic number to a named constant with clear intent.

---

## 2. Type Safety (69 violations, 23.1%)

**What it is**: Using the type system to make illegal states unrepresentable (Jane Street's core philosophy).

### Primary Violation: Exceptions in Hot Paths (JS-001)

**Rule**: Use `Result<T,E>` instead of exceptions in hot paths

**Why Jane Street cares**:
- Exception throwing is ~1000x slower than returning a Result
- Exceptions break predictable latency (unpredictable stack unwinding)
- Result types force explicit error handling at compile time

**Example from V12**:
```csharp
// ❌ VIOLATION
public Order ValidateOrder(OrderRequest req) {
    if (req.Quantity <= 0)
        throw new ArgumentException("Invalid quantity");
    return new Order(req);
}

// ✅ FIXED
public Result<Order, ValidationError> ValidateOrder(OrderRequest req) {
    if (req.Quantity <= 0)
        return Result.Err(ValidationError.InvalidQuantity);
    return Result.Ok(new Order(req));
}
```

**Other Type Safety Violations**:
- JS-002: Use `Option<T>` instead of null (prevents NullReferenceException)
- JS-003: Use sealed record hierarchies for sum types (exhaustive matching)
- JS-005: Enable nullable reference types (compile-time null safety)

**Impact**: 69 violations means ~69 places where we throw exceptions or use null unsafely.

**Fix Strategy**: Introduce `Result<T,E>` and `Option<T>` types, replace exceptions with Result.Err().

---

## 3. Concurrency (5 violations, 1.7%)

**What it is**: Lock-free concurrency patterns for predictable latency.

### Primary Violation: Lock Usage (JS-021)

**Rule**: Lock usage is BANNED - use Actor pattern or atomic primitives

**Why Jane Street cares**:
- `lock()` causes unpredictable latency (thread contention, priority inversion)
- Locks don't compose (deadlock risk)
- Actor pattern provides deterministic message ordering

**Example from V12**:
```csharp
// ❌ VIOLATION
lock(stateLock) {
    _position += delta;
}

// ✅ FIXED (Actor Pattern)
_stateActor.Enqueue(new UpdatePosition(delta));

// ✅ FIXED (Atomic)
Interlocked.Add(ref _position, delta);
```

**Impact**: 5 violations means we still have 5 `lock()` statements in V12 (should be zero).

**Fix Strategy**: Replace with FSM/Actor `Enqueue` pattern or `Interlocked` primitives.

---

## 4. Performance (2 violations, 0.7%)

**What it is**: Zero-allocation patterns for hot paths.

### Primary Violations:

**JS-036: Use Span<T> for zero-allocation**
```csharp
// ❌ VIOLATION
byte[] buffer = new byte[1024]; // heap allocation

// ✅ FIXED
Span<byte> buffer = stackalloc byte[1024]; // stack allocation
```

**JS-037: Use ArrayPool<T> for reusable buffers**
```csharp
// ❌ VIOLATION
var buffer = new byte[8192]; // GC pressure

// ✅ FIXED
var buffer = ArrayPool<byte>.Shared.Rent(8192);
try {
    // use buffer
} finally {
    ArrayPool<byte>.Shared.Return(buffer);
}
```

**Why Jane Street cares**:
- Heap allocations trigger GC pauses (unpredictable latency)
- Stack allocation is deterministic and fast
- ArrayPool eliminates allocation entirely for reusable buffers

**Impact**: Only 2 violations, but these are in hot paths (likely order processing).

**Fix Strategy**: Replace `new[]` with `stackalloc` or `ArrayPool` in hot paths.

---

## Distribution Summary

| Category | Count | % | Severity | Fix Complexity |
|----------|-------|---|----------|----------------|
| **Philosophy** | 223 | 74.6% | P0 | LOW (extract constants) |
| **Type Safety** | 69 | 23.1% | P0 | MEDIUM (add Result/Option types) |
| **Concurrency** | 5 | 1.7% | P0 | HIGH (replace locks with actors) |
| **Performance** | 2 | 0.7% | P0 | MEDIUM (use Span/ArrayPool) |

---

## Why These Matter for V12

V12 is a **high-frequency trading strategy** running inside NinjaTrader 8. Jane Street's rules apply because:

1. **Microsecond Latency**: Order execution must be predictable (no GC pauses, no lock contention)
2. **Risk Management**: Magic numbers hide risk parameters (tick sizes, position limits)
3. **Correctness**: Type safety prevents runtime errors during live trading
4. **Auditability**: Named constants and explicit error handling enable compliance audits

---

## Wave 8 Strategy

**69.8% of complexity files ALSO have Jane Street violations** - this is why your original plan is optimal:

> "Refactor and while refactoring fix the Jane Street issues in every file we touch"

**Execution**:
1. **37 files (6 days)**: Fix BOTH complexity (CYC > 8) AND Jane Street violations
2. **15 files (1 day)**: Fix ONLY Jane Street violations
3. **16 files (1 day)**: Fix ONLY complexity methods

**Per-File Checklist**:
- [ ] Extract all magic numbers to named constants (Philosophy)
- [ ] Replace exceptions with Result<T,E> in hot paths (Type Safety)
- [ ] Replace any remaining locks with Actor pattern (Concurrency)
- [ ] Use Span/ArrayPool in hot paths (Performance)
- [ ] Reduce method complexity to CYC ≤ 8

---

## References

- **Violations File**: `jane_street_p0_violations.json` (299 violations)
- **Rules Catalog**: `docs/standards/jane-street/RULES_CATALOG.md` (100+ rules)
- **Overlap Analysis**: `compare_files_normalized.ps1` (69.8% overlap)
- **Jane Street KB**: Query via `python scripts/query_kb.py "topic"`

---

**Next Step**: Start VM, fix Phase 1.5 scripts, run 3-epic pilot, then execute Wave 8 with this checklist.