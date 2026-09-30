---
type: KnowledgeIntel
title: Making OCaml Safe for Performance Engineering
description: OCaml type system innovations applied to C# — struct cache locality, ref struct escape prevention, data race freedom via type modes. Applied to V12 hot-path optimization.
tags: [performance, structs, ref-struct, data-race-freedom, cache-locality, gc]
timestamp: 2026-06-25T00:00:00Z
---

# Making OCaml Safe for Performance Engineering

**Source**: Jane Street engineering talk on OCaml perf + type system innovations

## Key Takeaways

- Uniform value representation eases GC and polymorphism but forces boxing of floats/records on the heap
- **Kinds (layouts)** track type shapes to specialized generics once per layout, minimizing binary bloat
- **Modes (Global vs Local)** track escape behavior, enabling safe, compiler-checked stack allocation of closures
- **Statically enforcing Data Race Freedom** uses Contention and Portability modes to prevent shared mutable state access

## V12 C# Patterns

### `struct_cache_locality`
Favor value types (structs) over reference types (classes) to eliminate GC scans and leverage contiguous layout caches.
```csharp
// PREFERRED — struct, stack/array allocated, no GC pressure
private struct OrderSlot {
    public int OrderId;
    public double Price;
    public int Quantity;
    public OrderState State;
}

// Fixed-size struct array — contiguous, cache-line friendly
private readonly OrderSlot[] _slots = new OrderSlot[1024];
```

### `ref_struct_escape_prevention`
Using C# `ref struct` definitions to enforce stack-only lifetimes and prevent heap escaping.
```csharp
// ref struct cannot be boxed or stored in fields — compiler-enforced stack lifetime
private ref struct MarketSnapshot {
    public ref readonly double Bid;
    public ref readonly double Ask;
    // Cannot escape to heap — safe for hot-path use
}
```

## Data Race Freedom (Applied to V12)

OCaml's Contention/Portability modes map to V12's lock-free mandate:
- **Contention mode** → C# equivalent: `Interlocked` operations only
- **Portability mode** → C# equivalent: `readonly` fields + immutable snapshots
- **Global mode** → C# equivalent: `static readonly` (immutable after init)

This is WHY `lock()` is banned: data race freedom is enforced architecturally via types and patterns, not runtime mutual exclusion.

## Performance Impact

| Pattern | GC Impact | Cache Impact |
|---------|-----------|--------------|
| `struct` arrays | Zero GC scans | Contiguous — L1/L2 friendly |
| `ref struct` | Zero heap allocation | Stack-only |
| `class` arrays | Full GC scan per object | Pointer-chased — cache miss |

## Cross-References
- [lock-free-patterns.md](lock-free-patterns.md) — data race freedom → no lock()
- [how-to-build-an-exchange.md](how-to-build-an-exchange.md) — cache_optimization struct arrays
