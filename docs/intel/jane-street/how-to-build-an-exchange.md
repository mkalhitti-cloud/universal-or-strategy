---
type: KnowledgeIntel
title: How to Build an Exchange
description: ECN matching engine architecture from Jane Street. FSM determinism, sidecar lifecycle, SMR replayability. Primary patterns for V12 state machine design.
tags: [exchange, fsm, determinism, sidecar, smr, matching-engine]
timestamp: 2026-06-25T00:00:00Z
---

# How to Build an Exchange

**Source**: Jane Street engineering talk — ECN matching engine architecture

## Key Takeaways

- ECN matching engines operate as **deterministic single-threaded state machines** on commodity x86 hardware
- UDP multicast for simultaneous, fair distribution of market data to all participants
- **State Machine Replication (SMR)**: any component rebuilt rapidly by replaying the transaction log
- Decouple core matching logic from timing-based events using **helper sidecars** (e.g., Cancel Fairy)
- Pointers and index dereferencing to locate order records = primary memory/cache bottleneck

## V12 C# Patterns

### `determinism`
Use tick timestamps instead of system clocks to ensure history replayability.
```csharp
// CORRECT — tick-based, replayable
_lastUpdateTick = Context.CurrentBar;

// BANNED — system clock, non-deterministic
_lastUpdateTime = DateTime.Now;
```

### `sidecar_lifecycle`
Segregate lifecycle and temporal order rules from core order book updates.
```csharp
// Core matching: pure state transitions only
private void ProcessFill(Fill fill) { /* state machine only */ }

// Sidecar: temporal rules handled separately
private void CancelFairySidecar() { /* time-based cancel logic */ }
```

### `one_in_flight`
Implement a two-phase order replacement FSM to avoid ghost-order states.
```csharp
// State machine prevents overlapping orders
private enum OrderState { Idle, PendingNew, Live, PendingCancel, PendingReplace }
// Only ONE order in flight per instrument at any time
```

### `cache_optimization`
Use fixed-size struct arrays with direct index lookups to eliminate pointer-chasing.
```csharp
// Struct array — contiguous memory, no GC pressure
private readonly OrderSlot[] _orderSlots = new OrderSlot[MaxOrders];
// Direct index access — O(1), cache-friendly
ref var slot = ref _orderSlots[orderId];
```

## Complexity Impact

These patterns directly reduce CYC:
- `one_in_flight` FSM: replaces complex conditional chains (CYC -5 to -10 typical)
- `sidecar_lifecycle`: splits one god-method into 2 focused methods (CYC halved)
- `cache_optimization`: replaces nested null-checks with direct struct access

## Cross-References
- [complexity-reduction.md](complexity-reduction.md) — FSM decomposition strategy
- [lock-free-patterns.md](lock-free-patterns.md) — lock-free FSM transitions
- [production-engineering-billions.md](production-engineering-billions.md) — `one_in_flight` safety
