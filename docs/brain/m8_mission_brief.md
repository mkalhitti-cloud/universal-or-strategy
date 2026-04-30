# Project Morpheus -- M8 Mission Brief: Live Measurement & Validation

## 1. Mission Overview
**Mission:** M8 - Validation of <100 ns Dispatch Budget (Hybrid Arena Phase 2)
**Context:** Following the successful integration of M5 (CPU Affinity), M6 (JIT Pre-compilation), and M7 (Dispatch Telemetry) on the `1112.001-v29.0` substrate, M8 is the definitive measurement phase. It operates under live Rithmic Direct Connection load to prove the architecture meets its institutional-grade sub-100 ns latency targets.
**Goal:** Prove thread determinism, validate telemetry metrics against the latency budget, and perform a final logic audit (Cases 10-12) to clear the system for production cutover.

## 2. Validation Objectives

### A. Telemetry Measurement (<100 ns Budget)
- **Objective:** Measure `_lastDispatchTicks` and `_dispatchPeakElapsedTicks` under extreme live market conditions (e.g., market open or high volatility data feed).
- **Success Criteria:** Peak-Ticks budget must translate to `<100 ns` consistently. (Using the cached `_stopwatchFrequency`, typical 10MHz tick = 100ns. We are looking for 1 tick or 0 ticks average overhead within the core execution block).

### B. Thread Affinity Lifecycle & Survival
- **Objective:** Confirm the CPU affinity bind we established in `State.DataLoaded` survives NinjaTrader's internal thread management, including:
  - Broker reconnects
  - Transition from `State.Historical` to `State.Realtime`
- **Crucial Verification:** We must mathematically confirm that the thread executing `OnBarUpdate` in real-time is the exact same OS thread that was bound in `State.DataLoaded`.
- **Contingency:** If we discover NinjaTrader spawns a *different* thread for real-time market data handling, we must deploy a follow-up surgical patch routing the `BindCpuAffinity()` call to trigger on the very first invocation of `OnBarUpdate` instead.

### C. Logic Audit (Cases 10-12)
- **Objective:** Run the final tier of Risk Logic Audits (Cases 10, 11, and 12) against the new `v30.0` substrate.
- **Success Criteria:** No logic isolation breaches, zero non-deterministic state mutations, and a complete Pass on all symmetry checks.

## 3. Execution Sequence for the Director

1. **Environment Setup:** 
   - Connect to **Rithmic Direct Connection** (Live Market Data).
   - Ensure `EnableSIMA = True`.
   - Ensure `EnablePhotonAffinityBind = True` and `CpuAffinityMask = 4` (or whatever specific core you have dedicated for Morpheus).
2. **Launch & Monitor:**
   - Enable the strategy on the target chart.
   - Monitor the output window carefully for the OS Thread ID printed during `State.DataLoaded`.
3. **Execution & Load Testing:**
   - Observe live ticks coming in.
   - Fire manual trades to stress the queue, observing the `FORENSIC PULSE REPORT` timings.
4. **Shutdown & Telemetry Harvest:**
   - Flatten and Disable the strategy.
   - Capture the `[MORPHEUS TELEMETRY]` block printed at the end.
   - Calculate if `Peak Ticks` stayed within the 100ns budget.

## 4. Contingency / Follow-Up Actions

- **If Latency Misses Budget:** We must perform a forensic teardown of the execution tree to identify any remaining allocation spikes or branch mispredictions blocking the hot path.
- **If Thread ID Mismatches:** We will immediately implement the "Late Bind Patch", moving `BindCpuAffinity()` from `State.DataLoaded` to the top of `OnBarUpdate` (guarded by an `Interlocked.CompareExchange` boolean to ensure it only binds once).

---
*End of Brief. Director: Please execute the M8 live market measurement protocol outlined above.*
