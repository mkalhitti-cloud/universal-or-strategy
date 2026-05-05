# C# Hard-Core Performance: Cache-Line Padding & NUMA Isolation

We are pushing our C# 12 / .NET 8.0 Sovereign Engine (running on Windows Server 2022) to its absolute physical limits. While our lock-free MPMC ring buffer is functionally correct, we are hitting a "performance wall" at roughly 12M operations per second due to **Cache-Line Invalidation Storms (L1/L2 ping-ponging)** between CPU cores.

We need your expert help to refactor our foundational types to be "Hardware-Aware."

## Technical Requirements:

1.  **Explicit Memory Alignment**: Please refactor the `DispatchSlot` and `TaggedPointer` structs using `[StructLayout(LayoutKind.Explicit)]`. Ensure each shared state field (specifically the `ProducerCursor`, `ConsumerCursor`, and `Epoch` counters) are isolated by 64 bytes of padding to prevent false sharing.
2.  **NUMA-Local Affinity**: Provide a helper method to pin the background consumer thread to a specific CPU core using `SetThreadAffinityMask`. We need to ensure the worker thread stays physically close to its L2 cache and doesn't get migrated by the Windows scheduler.
3.  **Instruction-Level Optimization**: In the high-frequency polling loop, use `Thread.SpinWait(1)` but also provide a mechanism to detect "sustained idle" and back off to a more power-efficient state without losing sub-microsecond responsiveness.
4.  **Zero-Allocation Invariants**: The solution must remain entirely GC-free. Do not use `Task.Run` or standard thread pools. We require a dedicated `Thread` with `ThreadPriority.Highest`.

## Objective:

Generate the full C# implementation for the padded `TaggedPointer` and the `AffinitiveDispatchWorker` class. Your code will be audited for cache-line alignment and instruction-level efficiency.

Please deliver the code in a single, production-ready block.
