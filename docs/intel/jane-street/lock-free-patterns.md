---
type: KnowledgeRule
title: Lock-Free Patterns (V12 DNA Mandate)
description: lock() blocks are STRICTLY BANNED in V12. All state mutations use FSM/Actor Enqueue model or atomic primitives. Primary reference for Phase 3 DNA audit and Phase 5 execution.
tags: [lock-free, concurrency, actor, fsm, enqueue, dna]
resource: docs/intel/jane-street/ocaml-performance-engineering.md
timestamp: 2026-06-25T00:00:00Z
---

# Lock-Free Patterns (V12 DNA Mandate)

**Rule**: `lock(stateLock)` blocks are **STRICTLY BANNED**. Zero-match requirement.
**Scan**: `grep -r "lock(" src/` must return 0 results.
**Why**: Jane Street's Data Race Freedom via Contention/Portability modes — prevent shared mutable state access at the type-system level, not runtime locks.

## The Actor/Enqueue Model

All state mutations MUST use the Enqueue pattern:

```csharp
// BANNED
lock (_stateLock) {
    _position += fill.Quantity;
}

// CORRECT — Actor Enqueue model
_stateActor.Enqueue(new PositionUpdateMessage(fill.Quantity));

// OR: Interlocked atomic primitive
Interlocked.Add(ref _position, fill.Quantity);
```

## Allowed Primitives

| Pattern | Usage |
|---------|-------|
| `Interlocked.Add/Exchange/CompareExchange` | Scalar state counters |
| `volatile` field | Single-reader/single-writer flags |
| `ConcurrentQueue<T>` | Message passing between actors |
| `ConcurrentDictionary<K,V>` | Shared read-heavy lookup tables |
| `ImmutableArray<T>` | Snapshot-style read-only state |
| FSM `Enqueue(message)` | All complex state transitions |

## FSM Transition Pattern (Jane Street Exchange)

From [how-to-build-an-exchange.md](how-to-build-an-exchange.md) — `one_in_flight` pattern:

```csharp
// Two-phase order replacement FSM — no lock() needed
private enum OrderFsmState { Idle, PendingNew, Live, PendingCancel }

private void TransitionTo(OrderFsmState next) {
    // Atomic CAS — no lock
    var prev = (OrderFsmState)Interlocked.Exchange(ref _fsmState, (int)next);
    _stateActor.Enqueue(new FsmTransition(prev, next));
}
```

## DNA Check (Phase 3 + Phase 5.V)

Phase 3 DNA audit MUST verify:
```bash
# Zero lock() blocks in any modified file
jcodemunch search_ast("lock(", source_file) → must return 0 matches
```

Phase 5.V independent verification MUST verify:
```bash
grep -r "lock(" src/<modified_file> → must return 0 lines
```

## V12 Specific Notes

- `OnStateChange` method: NEVER use lock() — use `_actor.Enqueue(StateChangeMsg)`
- ATM management: use `Interlocked.CompareExchange` for ATM state flags
- Fleet/Symmetry coordination: use `ConcurrentQueue` message passing
- All `static readonly` dictionaries are safe (immutable after init — no lock needed)

## Cross-References
- [ocaml-performance-engineering.md](ocaml-performance-engineering.md) — data race freedom modes
- [how-to-build-an-exchange.md](how-to-build-an-exchange.md) — one_in_flight FSM
- [complexity-reduction.md](complexity-reduction.md) — FSM decomposition reduces CYC too
