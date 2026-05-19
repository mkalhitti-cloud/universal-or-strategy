# MISSION: ROUND 28 KERNEL ADJUDICATION (MMIO-SPSC-RING)

# TARGET: Antigravity OS V12.15 Platinum Standard

# CONTEXT: High-Speed Algorithmic Trading (Low-Latency C#)

This is the ZERO-KNOWLEDGE version of the prompt for use in the Arena AI Text Section (where the model cannot read local files).

---

ACT AS: Principal Kernel Architect.
TASK: Design a standalone, compilable C# kernel primitive: MmioSpscRing<T>.

## 1. PERFORMANCE CONSTRAINTS (HARD)

- ENVIRONMENT: .NET 9 / NinjaTrader 8 compatible.
- THROUGHPUT TARGET: < 14 ns/op.
- ALLOCATION: ZERO heap allocation on the hot path (Dequeue/Enqueue).
- SYNC: LOCK-FREE. Use Volatile.Read/Write and manual memory barriers. No Interlocked.\*
- MEMORY: Backed by a raw byte\* region (Memory-Mapped I/O).

## 2. STRUCT SPECIFICATION (ADR-001)

Implement two structs with [StructLayout(LayoutKind.Sequential, Pack = 8, Size = 64)]:

1. OrderSlot: long Seq, OrderId, StrategyId, InstrumentId, PriceTicks, Quantity, TimestampNs, ulong XorShadow (FINAL FIELD).
2. FillSlot: long Seq, OrderId, FillId, InstrumentId, FillPriceTicks, FillQuantity, TimestampNs, ulong XorShadow (FINAL FIELD).

## 3. INTEGRITY PROTOCOL (XOR-SHADOW / ADR-016)

Implement a branch-free XorShadow validator:

- Salt: 0xDEADBEEFCAFEBABEUL.
- Compute: XOR salt with successive 8-byte blocks of the struct (excluding the final XorShadow field).
- Validate: Compare computed hash against the stored XorShadow field in the slot.

## 4. RING ARCHITECTURE (MMIO)

- Header (128 bytes):
  - Offset 0: producerCursor (long)
  - Offset 8-63: Padding (Cache line isolation)
  - Offset 64: consumerCursor (long)
  - Offset 72-127: Padding (Cache line isolation)
- Slots: Occupy memory starting at Offset 128.

## 5. REQUIRED API (UNSAFE)

- public MmioSpscRing(byte\* region, int capacity) // capacity must be power of 2
- public bool TryEnqueue(in T item)
- public bool TryDequeue(out T item) // MUST validate XorShadow before returning data.

## 6. TEST HARNESS (8/8 AMAL BATTERY)

Include a static Program.Main with an automated battery that MAINTAINS A COUNTER and prints:

- 1. Single round-trip validity.
- 2. Sequential burst (10 items).
- 3. Corruption Detection: Manually flip a bit in a slot (e.g., slotPtr + 16 ^= 0xFF) and verify TryDequeue returns false.
- 4. Ring Full behavior (Capacity 64).
- 5. Ring Empty behavior.
- 6. Generation wrap-around (Fill 64, Drain 64, Refill 64).
- 7. Throughput Benchmark (10M iterations, report ns/op).
- 8. Generic Multi-type Support (Test with both OrderSlot and FillSlot).

OUTPUT FORMAT: Single markdown code block containing a complete, verbatim, compilable C# file. No pseudocode. No stubs. High-performance "unsafe" implementation only.
