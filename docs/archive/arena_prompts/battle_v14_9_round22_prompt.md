Do not use any web search. Answer from memory only.

1. **Prior Breakthroughs (V14.9 Baseline)**:
   | Component | Protocol | Latency |
   | :--- | :--- | :--- |
   | **SpscRing** | 3-Fence Roundtrip (Relaxed Read + Barrier) | 6.42ns |
   | **Topology** | SPSC per-lane ring, slot per 256B stripe | 4.1ns |
   | **Memory** | Unmanaged (Marshal.AllocHGlobal) | Zero GC |

2. **Phase 22 Design Challenge: Hardware-Striped Affinity**:
   Provide a single-file `index.html` referencing the following C# 8.0 logic:

- **Constraint 1**: Implement a **Hardware-Striped CoreLane Array** where each CPU core has a dedicated cache-aligned SPSC lane.
- **Constraint 2**: Integrate **CPU Thread Affinity Pinning** for the Dispatcher thread and Worker threads to ensure they stay on the same physical core as their assigned SPSC stripe. Use `PInvoke` to `SetThreadAffinityMask`.
- **Constraint 3**: Implement the **SpscRingV149** (3-fence) protocol within this striped topology.

3. **Required Output Headers**:

- **Title**: Model Name + Version
- **Design Name**: e.g., "Affinity-Pinned Stripe-Ring"
- **Mechanism**: One-sentence core logic summary.
- **Latency Est**: Target < 3ns.

In the page <title> tag and in a visible <h2> heading, write your model name and version.
