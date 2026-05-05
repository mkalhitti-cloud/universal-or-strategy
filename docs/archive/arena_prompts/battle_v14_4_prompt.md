# C# High-Performance Concurrency Implementation Request

We have successfully compiled the foundational data structures for our C# 12 high-throughput data pipeline (specifically, our lock-free `MPMCRingBuffer<T>` and the `TaggedPointer` struct using explicit 64-byte padded struct layouts).

For the final stage of our performance optimization sequence, we need you to design the integration layer. We require a highly optimized, fully thread-safe `SovereignDispatchPipeline` class that orchestrates these primitives.

## Functional Requirements:

1. **Producer Gateway**: Expose a highly concurrent `TryPublish(PhotonContext context)` method that uses the `TaggedPointer` mechanics to rapidly push messages onto the underlying `MPMCRingBuffer` without blocking.
2. **Deterministic Consumer Threading**: Design the consumer logic as a dedicated, long-running background thread that polls the ring buffer continuously. It must utilize `SpinWait` optimally when the buffer is empty to yield CPU cycles without introducing context-switching latency.
3. **Graceful Pipeline Teardown**: Implement an atomic, deterministic shutdown sequence. When `Dispose()` is called, the pipeline must flush all remaining queued entries to completion, flag the shutdown definitively using volatile memory barriers, and strictly prevent any "ghost" publishes from successfully enqueuing post-teardown.
4. **Lock-Free Synchronization**: Do not use any `System.Threading.Monitor` locks (`lock(obj)`), Mutexes, or operating system blocking primitives. All internal signaling and synchronization must remain entirely lock-free.

## Output Format:

Please provide the full C# implementation for the `SovereignDispatchPipeline` class. Ensure that all memory barrier directives (`Interlocked`, `Volatile.Read/Write`, `Thread.MemoryBarrier`) and processor spin-waits are explicitly implemented. Emphasize strict adherence to C# weak memory models to guarantee cross-core state coherency.
