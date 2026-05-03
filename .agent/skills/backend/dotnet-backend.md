---
name: .NET Backend Patterns
description: >
  High-performance .NET and C# backend design patterns for NinjaTrader and Nexus.
  Ensures zero-allocation hot paths and thread-safe execution.
---

# .NET Backend Patterns (Nexus V12 Standard)

## Core Logic

- **Zero-Allocation Hot Path**: Use `Span<T>`, `Memory<T>`, and `ArrayPool<T>` to eliminate GC pressure.
- **Lock-Free Concurrency**: Favor SPSC/MPMC ring buffers over `lock()` or `Monitor`.
- **Marshalling Safety**: Use `fixed` blocks and direct pointers for high-velocity memory access.

## Instructions

1.  **Direct Writes**: Use direct writes to `stopOrders` during bracket submission (Build 981).
2.  **Lifecycle Management**: Always release semaphores in `finally` blocks.
3.  **FSM Model**: Use state machines for complex order lifecycle transitions.

## Mandatory Self-Improvement Audit

After EVERY skill use, perform this audit:

1. **Instruction Clarity**: Did any step produce an unexpected or ambiguous result?
2. **Trigger Coverage**: Is the description "pushy" enough?
3. **Gap Analysis**: If a gap is found, fix it immediately. Otherwise, state: `skill(dotnet-backend): no gaps identified`.
