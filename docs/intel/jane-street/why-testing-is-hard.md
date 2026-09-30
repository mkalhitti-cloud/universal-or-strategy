---
type: KnowledgeIntel
title: Why Testing Is Hard and How to Fix It (Will Wilson)
description: Deterministic Simulation Testing (DST), fault injection, state invariants, AI agent architectural enforcement. Applied to V12 test strategy and FSM correctness.
tags: [testing, dst, determinism, fault-injection, invariants, ai-agents, fsm]
timestamp: 2026-06-25T00:00:00Z
---

# Why Testing Is Hard and How to Fix It (Will Wilson)

**Source**: Jane Street engineering talk by Will Wilson on deterministic simulation testing

## Key Takeaways

- Non-determinism (network, clocks, OS thread scheduling) degrades randomized testing into guessing
- **Deterministic Simulation Testing (DST)**: runs execution branches in 100% mocked environment (cooperative multitasking, isolated VM state)
- Hypervisor-level DST uses host-level page deduplication via Copy-on-Write for massive branch exploration
- Exhaustive specification unnecessary; coarse-grained invariants + fault injection catch majority of bugs
- **AI agents act as "evil genies"**: edit code to satisfy tests while destroying architecture — strict enforcement required

## V12 C# Patterns

### `lock_free_scheduler`
Enforce single-threaded FSM actor loop, processing events sequentially from a queue.
```csharp
// Single-threaded FSM — deterministic, no scheduling non-determinism
private readonly ConcurrentQueue<IEvent> _eventQueue = new();

private void ProcessEventLoop() {
    while (_eventQueue.TryDequeue(out var evt)) {
        _fsm.Transition(evt);  // One at a time, always same order
    }
}
```

### `deterministic_time`
Inject `IClock` to bind time strictly to bar/tick timestamps instead of system clocks.
```csharp
// BANNED — system clock = non-deterministic
if (DateTime.Now - _lastOrder > TimeSpan.FromSeconds(30)) { ... }

// CORRECT — bar-based time = deterministic, replayable
if (CurrentBar - _lastOrderBar > BarsPerMinute * 0.5) { ... }
```

### `state_invariants`
Verify global structural conditions at the end of every state transaction.
```csharp
// Invariant check — catches FSM bugs immediately
[Conditional("DEBUG")]
private void AssertInvariants() {
    Debug.Assert(_activeOrders <= Math.Abs(_targetPosition),
        "Active orders exceed position size — FSM state corruption");
    Debug.Assert(_flatPosition || _activeOrders > 0,
        "Non-flat position with no active orders");
}
```

### `fault_injection`
Simulate network latency, broker disconnects, and execution slippage using deterministic PRNGs.
```csharp
// Deterministic fault injection for testing
private readonly Random _faultRng = new Random(42);  // Fixed seed = reproducible

private bool ShouldSimulateLatency() =>
    _testMode && _faultRng.NextDouble() < FaultInjectionRate;
```

## Why This Matters for Wave 7

The "evil genie" warning is directly applicable to Wave 7 agents:
- Phase 5 workers MUST NOT reduce CYC by disabling logic or deleting conditions
- Phase 5.V independent verification exists precisely to catch this
- `state_invariants` should be added to extracted helpers during Phase 5

## Cross-References
- [how-to-build-an-exchange.md](how-to-build-an-exchange.md) — lock_free_scheduler origin
- [testing-strategies.md](testing-strategies.md) — xUnit [Fact] mandate
- [complexity-reduction.md](complexity-reduction.md) — extraction must preserve behavior
