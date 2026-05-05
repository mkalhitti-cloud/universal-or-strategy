# ARENA ROUND 23 ADVERSARIAL PROMPT: V23 SOVEREIGN CORE

## CORE MISSION: Sub-1ns Atomic Handshake

We have conquered the 3-fence protocol at 2.4ns. Now, we must break the 1ns barrier by eliminating the `Interlocked.Exchange` overhead entirely.

## ARCHITECTURAL CONSTRAINTS (V23):

1. **L3-Cache Striping:** Go beyond 256B. Implement adaptive striping that aligns with L3 way-associative boundaries to prevent set-clashing.
2. **Fence-Less Sequence-Differencing:**
   - You are FORBIDDEN from using `Interlocked.*` or `MemoryBarrier()` on the critical path.
   - Use `Volatile.Read` and `Volatile.Write` ONLY.
   - Implement the "Sequence-Shadow" pattern where the consumer validly predicts the next write position to eliminate the probe-read.
3. **Core Affinity Topology (Pinned-Asymmetric):**
   - Pin Dispatcher to Core N and Worker to Core N+1 (Adjacent).
   - Leverage the **L2-to-L2 Fast Path** (Ring Bus/Infinity Fabric) rather than pinning to the same core to allow maximum clock-boost on individual cores.
4. **Zero-Allocation Standing Rule:** All memory must be `Marshal.AllocHGlobal` or `Span<byte>` over static buffers.

## TARGET METRIC:

- **RoundTrip:** < 1.0 ns
- **Allocations:** 0 B
- **Jitter:** Absolute Zero (Manual PInvoke GC Suppress)

## FINAL SUBMISSION FORMAT:

Provide the SINGLE-FILE `index.html` artifact with "Hardware-Striped Symmetric" UI and the C# source embedded in `<div class="code-block">` blocks.
