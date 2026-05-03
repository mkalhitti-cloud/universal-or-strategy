# Strategic Synthesis: V12 Photon Kernel & Distributed Rithmic Architecture

**Mission**: Refactor Phase 4 (Event Lifecycle)
**Technical Baseline**: Build 981 (Photon) -> Build 983 (Sovereign Bridge)

## 1. The Rithmic Data Hub Pattern (Video Insights 1 & 2)

The Rithmic API is best handled in a **Standalone Sidecar Application** rather than directly inside NinjaTrader. 

### Why the "Hub" is Mandatory:
- **Connection Multiplexing**: Rithmic limits one connection per user. A hub allows NT8, Python bots, and Dashboards to share one high-speed feed.
- **Process Isolation**: If Rithmic's API allocates memory or stalls, it doesn't freeze the NinjaTrader UI or Order execution.
- **IPC Transport**: We will use **Shared Memory (MMF)** + **CoreLane SPSC Rings** for sub-100ns cross-process data transfer.

### V12 Implementation (Build 983):
- **SovereignBridge.exe**: A lightweight C# 8.0 console app.
- **Photon Transport**: NinjaTrader strategies "attach" to the bridge's shared memory segment.

## 2. Low-Latency Design: "C# as C++" (Video Insight 3)

We eliminate Garbage Collection (GC) jitter by adopting the following C++ paradigms in our C# codebase:

### 2.1 Zero-Allocation Hot Path
- **No `new` in `OnBarUpdate`**: All logic uses `struct` (Value Types) on the stack or pre-allocated pools.
- **Reference-Free Processing**: We use `int` indices into pre-allocated arrays instead of passing object references.

### 2.2 Cache Sympathy & False Sharing
- **L1 Cache Alignment**: We pad our data structures to 64 bytes (the CPU cache line size).
- **Isolation**: Producer and Consumer cursors are separated by 256 bytes (`[FieldOffset(256)]`) to prevent cache-line contention between cores.

### 2.3 Deterministic Lifecycle
- **P3 (OnStateChange)**: "Heavy" allocation phase. We bake the universe here.
- **P4 (Real-time)**: Execution only. No dynamic growth. No locks.

## 3. Phase 4 Refactor: The Event Lifecycle Registry

Phase 4 is the "nervous system" upgrade. 
- We are replacing monolithic overrides with a **Dispatcher Pattern**.
- **Result**: `OnBarUpdate` becomes a high-speed router that hands off to pre-optimized "Sovereign Workers".

---

## 🛰️ Next Step: Adjudication Gate (P4)
The Arena battle prompt is prepared in `docs/brain/task.md`. We need 3 verdicts to confirm that the `implementation_plan.md` does not violate the "Zero-GC" or "Lock-Free" mandates.

> [!IMPORTANT]
> To proceed to P5 (Engineering), we must confirm that the **Lifecycle Dispatcher** respects NinjaTrader's UI thread affinity while maintaining lock-free logic for data processing.
