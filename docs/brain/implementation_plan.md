# M5-M7 Performance Integration Plan
## Project Morpheus -- Hybrid Arena Phase 2

**Mission**: CPU Affinity (M5) + JIT Warm-up (M6) + Dispatch Telemetry (M7)
**Build Tag**: `1112.001-v29.0` (BUILD_TAG remains; M5-M7 ride the v29.0 substrate)
**Date Drafted**: 2026-04-29
**Architect**: Claude P3 (Plan-Only Mode)
**Engineer (Worker)**: Codex P4
**Validator**: Codex P4 self-audit + Antigravity P1 orchestration
**Final Plan Path**: `docs/brain/implementation_plan.md`
**Workflow**: Orchestrator-Worker-Validator. ONE step at a time. Each step is independently verifiable.

> **STEP 0 (Codex must run first)**: Copy this entire file from `.claude/plans/godmode-use-ultrathink-for-snuggly-wave.md` to `docs/brain/implementation_plan.md` so the canonical mission record lives at the agreed path.
> ```bash
> cp ".claude/plans/godmode-use-ultrathink-for-snuggly-wave.md" "docs/brain/implementation_plan.md"
> ```
> Self-audit: `test -f docs/brain/implementation_plan.md && echo OK`

---

## 1. Mission Summary

M5-M7 layers determinism, JIT pre-compilation, and ultra-low-overhead per-cycle telemetry on top of the v29.0 Hybrid Arena substrate. CPU affinity pins the strategy execution thread to a configurable core to eliminate scheduler jitter; JIT warm-up forces the .NET CLR to compile the dispatch pipeline (including every method tagged `[MethodImpl(MethodImplOptions.AggressiveInlining)]`) before the first market signal lands; the new telemetry partial captures `LastDispatchTicks` on top of the existing `PeakDispatchTicks` / `DispatchTicksAccumulator` / `TotalDispatches` counters and emits a structured ASCII report at `State.Terminated`. All three additions ship as new partial-class files (`V12_002.Photon.Affinity.cs`, `V12_002.Photon.JitWarmup.cs`, `V12_002.Photon.Telemetry.cs`); the hot-path memory contract, zero-allocation dispatch, and ASCII gate are untouched.

This work is a SAFE addition: no `lock(stateLock)` is introduced, no allocation is added inside `OnBarUpdate` or the dispatch loop, all counters live as plain `long` fields on the strategy class with `Volatile.Read`/`Volatile.Write` and `Interlocked` barriers, and the only dispatch hot-path edit is a single `Volatile.Write` call after the existing accumulator updates.

---

## 2. File Change Manifest

| # | Path | Action | Purpose |
|---|------|--------|---------|
| 1 | `src/V12_002.Properties.cs` | MODIFY | Add `EnablePhotonAffinityBind` (bool, default false) + `CpuAffinityMask` (int, default 4 = Core 2) NinjaScriptProperties |
| 2 | `src/V12_002.Photon.Affinity.cs` | CREATE | P/Invoke `SetThreadAffinityMask` + `BindCpuAffinity()` cold-path entry + thread priority bump |
| 3 | `src/V12_002.Photon.JitWarmup.cs` | CREATE | `WarmupJit()` via `RuntimeHelpers.PrepareMethod` + reflection scan + dry-run synthetic loop |
| 4 | `src/V12_002.Photon.Telemetry.cs` | CREATE | `_lastDispatchTicks`, cached `_stopwatchFrequency`, recycled `_telemetryReportBuilder`, `GetPhotonTelemetryReport()`, `PrintPhotonTelemetryReport()` |
| 5 | `src/V12_002.Lifecycle.cs` | MODIFY | Call `BindCpuAffinity()` + `WarmupJit()` in `State.DataLoaded` AFTER `FreezeManagementMembrane()` |
| 6 | `src/V12_002.Lifecycle.cs` | MODIFY | Call `PrintPhotonTelemetryReport()` in `State.Terminated` AFTER `EmitMetricsSummary()` |
| 7 | `src/V12_002.SIMA.Dispatch.cs` | MODIFY | Insert `Volatile.Write(ref _lastDispatchTicks, tFinalTicks);` after `sw.Stop()` |
| 8 | `deploy-sync.ps1` | MODIFY | Append three new `.cs` files to the file-sync map |

**Note on field reuse (M7)**: `_dispatchPeakElapsedTicks`, `_dispatchTotalElapsedTicks`, and `_dispatchInvocationCount` already exist on the strategy class (`src/V12_002.cs:574-578`). They map exactly to the brief's `PeakDispatchTicks`, `DispatchTicksAccumulator`, and `TotalDispatches` counters. M7 reuses these and adds only `_lastDispatchTicks`. There is NO new heap-allocated telemetry object; every counter is a plain `long` field on the strategy class with explicit memory-barrier access.

**Note on `DispatchSingleAccount` (M6)**: The brief lists `DispatchSingleAccount` as a JIT warm-up target. The codebase has no method by that name; the per-account dispatch is inlined inside `ExecuteSmartDispatchEntry`'s for-loop, and the consumer-side per-account submit is `ProcessFleetSlot` in `src/V12_002.SIMA.Fleet.cs`. The plan substitutes `ProcessFleetSlot` and `PumpFleetDispatch` (the consumer driver), and adds a reflection-driven scan that prepares every method on `V12_002` decorated with `[MethodImpl(MethodImplOptions.AggressiveInlining)]`.

---

## 3. Permanent DNA Constraints (Codex must verify per step)

- [ ] No `lock(stateLock)` is added or relied upon. Use Volatile primitives, Interlocked, or `Enqueue(ctx => ...)`.
- [ ] No allocation, LINQ, closures, or string concatenation inside `OnBarUpdate` or any method reachable from the dispatch loop. (Cold-path code in M5/M6 cold init MAY allocate; M7 hot-path write is a single `Volatile.Write` and zero allocation.)
- [ ] All `Print()` string literals are ASCII-only. NEVER use emoji, curly quotes, em-dashes, Unicode arrows, or box-drawing. Allowed substitutions: `(!)` not emoji, `--` not em-dash, `->` not arrow, straight `"` and `'` only.
- [ ] All new files use `public partial class V12_002 : Strategy` inside `namespace NinjaTrader.NinjaScript.Strategies`.
- [ ] PascalCase methods, camelCase locals. No dense one-liners.
- [ ] After every edit, run the self-audit grep specified in the step. PASS gates progression.
- [ ] After ALL 8 steps, run `python C:\tmp\byte_purge.py` (or the ASCII gate inside `deploy-sync.ps1`) before deployment.

---

## 4. Step-by-Step Implementation

### STEP 1 -- Add Photon Kernel NinjaScriptProperties

**Target**: `src/V12_002.Properties.cs`
**Action**: MODIFY
**Anchor**: existing `EnablePhotonMmioMirror` block (lines 293-295) is the unique insertion point.

**OLD** (exact match required):
```csharp
        [NinjaScriptProperty]
        [Display(Name = "Enable Photon MMIO Mirror", GroupName = "Photon Kernel", Order = 5)]
        public bool EnablePhotonMmioMirror { get; set; } = false;

        [NinjaScriptProperty]
        [Display(Name = "Auto Flatten Desync", GroupName = "12. SIMA", Order = 5)]
        public bool AutoFlattenDesync { get; set; }
```

**NEW**:
```csharp
        [NinjaScriptProperty]
        [Display(Name = "Enable Photon MMIO Mirror", GroupName = "Photon Kernel", Order = 5)]
        public bool EnablePhotonMmioMirror { get; set; } = false;

        // M5 (Build 1112.001-v29.0): CPU affinity bind for the strategy execution thread.
        // Defaults to OFF -- safe in shared-host environments. When enabled, the cold path
        // calls BindCpuAffinity() once during State.DataLoaded after FreezeManagementMembrane().
        [NinjaScriptProperty]
        [Display(Name = "Enable Photon CPU Affinity", Description = "Bind hot-path thread to CpuAffinityMask. OFF by default. Boost ThreadPriority to Highest when enabled.", GroupName = "Photon Kernel", Order = 6)]
        public bool EnablePhotonAffinityBind { get; set; } = false;

        // M5: CPU affinity bitmask. 4 = Core 2 (default). 8 = Core 3. 12 = Cores 2+3.
        // Bit position = core index (0-based). Range guards 1..int.MaxValue.
        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "Photon CPU Affinity Mask", Description = "Bitmask of CPU cores for the hot-path thread. 4 = Core 2 (default). 8 = Core 3. 12 = Cores 2+3.", GroupName = "Photon Kernel", Order = 7)]
        public int CpuAffinityMask { get; set; } = 4;

        [NinjaScriptProperty]
        [Display(Name = "Auto Flatten Desync", GroupName = "12. SIMA", Order = 5)]
        public bool AutoFlattenDesync { get; set; }
```

**Self-Audit (MUST PASS before STEP 2)**:
```bash
grep -n "EnablePhotonAffinityBind\|CpuAffinityMask" src/V12_002.Properties.cs
```
Expected: 4 lines -- 2 attribute decorations + 2 property declarations for each new property.

```bash
grep -c "NinjaScriptProperty" src/V12_002.Properties.cs
```
Expected: prior count + 2 (verify the additions land cleanly).

---

### STEP 2 -- Create V12_002.Photon.Affinity.cs (M5)

**Target**: `src/V12_002.Photon.Affinity.cs`
**Action**: CREATE (new file)

**FULL FILE CONTENT**:
```csharp
// V12_002.Photon.Affinity.cs -- M5 CPU Affinity & Thread Priority (Hybrid Arena Phase 2)
// Build 1112.001-v29.0
// Bind the strategy execution thread to a configurable CPU core via Win32 P/Invoke
// to eliminate OS scheduler jitter on the hot dispatch path.
//
// Threading contract: BindCpuAffinity() runs ONCE during State.DataLoaded, AFTER
// FreezeManagementMembrane() has completed. It binds the calling thread, which is
// the strategy initialization thread for that NinjaTrader instance. If the bind
// fails (insufficient privileges, invalid mask, etc.) the call returns gracefully
// with a [WARN] log and the strategy continues unbound. NEVER throws.

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace NinjaTrader.NinjaScript.Strategies
{
    public partial class V12_002 : Strategy
    {
        #region M5 P/Invoke (kernel32)

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetCurrentThread();

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern UIntPtr SetThreadAffinityMask(IntPtr hThread, UIntPtr dwThreadAffinityMask);

        // Idempotent guard -- BindCpuAffinity() is wired only into the cold path,
        // but a defensive flag prevents accidental double-bind if a future caller
        // adds a re-init wrapper.
        private int _affinityBindAttempted = 0;

        #endregion

        #region M5 Cold-Path Entry

        /// <summary>
        /// Bind the calling thread (strategy DataLoaded thread) to the configured CPU mask
        /// and bump priority to Highest. Best-effort; never throws.
        /// Called from State.DataLoaded immediately after FreezeManagementMembrane().
        /// </summary>
        private void BindCpuAffinity()
        {
            if (Interlocked.CompareExchange(ref _affinityBindAttempted, 1, 0) != 0)
            {
                Print("[MORPHEUS] CPU affinity bind already attempted -- skip.");
                return;
            }

            if (!EnablePhotonAffinityBind)
            {
                Print("[MORPHEUS] CPU affinity disabled (EnablePhotonAffinityBind=false) -- thread runs unbound.");
                return;
            }

            int mask = CpuAffinityMask;
            if (mask <= 0)
            {
                Print(string.Format("[WARN] CPU affinity mask invalid (mask={0}) -- running unbound.", mask));
                return;
            }

            try
            {
                // Lock the managed thread to its current OS thread so the kernel32 bind sticks.
                Thread.BeginThreadAffinity();

                IntPtr threadHandle = GetCurrentThread();
                UIntPtr maskPtr = new UIntPtr((uint)mask);
                UIntPtr previous = SetThreadAffinityMask(threadHandle, maskPtr);

                if (previous == UIntPtr.Zero)
                {
                    int err = Marshal.GetLastWin32Error();
                    Thread.EndThreadAffinity();
                    Print(string.Format("[WARN] CPU affinity bind failed (Win32 error {0}) -- running unbound.", err));
                    return;
                }

                // NinjaTrader forbids ThreadPriority.Realtime; Highest is the sanctioned ceiling.
                try { Thread.CurrentThread.Priority = ThreadPriority.Highest; }
                catch (Exception priEx) { Print("[WARN] ThreadPriority bump failed: " + priEx.Message); }

                int tid = Thread.CurrentThread.ManagedThreadId;
                bool isBg = Thread.CurrentThread.IsBackground;
                Print(string.Format("[MORPHEUS] CPU affinity bound: mask=0x{0:X} prevMask=0x{1:X} threadId={2} isBackground={3} priority={4}",
                    mask, previous.ToUInt64(), tid, isBg, Thread.CurrentThread.Priority));
            }
            catch (Exception ex)
            {
                // Defensive: never propagate. Strategy must continue even if affinity is unavailable.
                try { Thread.EndThreadAffinity(); } catch { }
                Print("[WARN] CPU affinity bind threw -- running unbound. " + ex.Message);
            }
        }

        #endregion
    }
}
```

**Self-Audit (MUST PASS before STEP 3)**:
```bash
test -f src/V12_002.Photon.Affinity.cs && echo FILE_EXISTS
grep -n "private void BindCpuAffinity" src/V12_002.Photon.Affinity.cs
grep -n "SetThreadAffinityMask" src/V12_002.Photon.Affinity.cs
grep -nE "lock\s*\(\s*stateLock\s*\)" src/V12_002.Photon.Affinity.cs
```
Expected:
- `FILE_EXISTS`
- 1 hit for the BindCpuAffinity definition
- 2 hits for SetThreadAffinityMask (DllImport + the call site)
- 0 hits for `lock(stateLock)` (DNA gate)

ASCII gate (per-file):
```bash
python -c "import sys; b=open('src/V12_002.Photon.Affinity.cs','rb').read(); bad=[i for i,c in enumerate(b) if c>127]; print('ASCII_OK' if not bad else 'NON_ASCII_AT '+str(bad[:5]))"
```
Expected: `ASCII_OK`

---

### STEP 3 -- Create V12_002.Photon.JitWarmup.cs (M6)

**Target**: `src/V12_002.Photon.JitWarmup.cs`
**Action**: CREATE (new file)

**FULL FILE CONTENT**:
```csharp
// V12_002.Photon.JitWarmup.cs -- M6 JIT Warm-up Protocol (Hybrid Arena Phase 2)
// Build 1112.001-v29.0
// Force the .NET CLR to JIT-compile the dispatch pipeline before the first
// OnBarUpdate is processed, eliminating cold-IL latency spikes when the market
// opens. Targets: ExecuteSmartDispatchEntry, ProcessFleetSlot, PumpFleetDispatch,
// and every method on V12_002 decorated with [MethodImpl(AggressiveInlining)].
//
// Cold path: invoked once from State.DataLoaded AFTER BindCpuAffinity().
// Allocates during init only -- not reachable from any hot path.

using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;

namespace NinjaTrader.NinjaScript.Strategies
{
    public partial class V12_002 : Strategy
    {
        #region M6 Cold-Path Entry

        // Idempotent guard.
        private int _jitWarmupAttempted = 0;

        // Methods named explicitly per the M5-M7 brief. Reflection scan covers
        // additional [MethodImpl(AggressiveInlining)] methods at runtime.
        private static readonly string[] _jitWarmupTargetNames = new[]
        {
            "ExecuteSmartDispatchEntry",
            "ProcessFleetSlot",
            "PumpFleetDispatch",
            "ComputeFleetDispatchShadow",
            "RecordPhotonDispatchSample"
        };

        /// <summary>
        /// Force JIT compilation of the dispatch pipeline. Runs once during cold init.
        /// </summary>
        private void WarmupJit()
        {
            if (Interlocked.CompareExchange(ref _jitWarmupAttempted, 1, 0) != 0)
            {
                Print("[MORPHEUS] JIT warm-up already attempted -- skip.");
                return;
            }

            int prepared = 0;
            int failed = 0;
            Type t = typeof(V12_002);
            const BindingFlags scanFlags =
                BindingFlags.Public | BindingFlags.NonPublic |
                BindingFlags.Instance | BindingFlags.Static |
                BindingFlags.DeclaredOnly;

            MethodInfo[] all = t.GetMethods(scanFlags);

            // Pass 1: explicit name list (brief targets + dispatch pipeline core).
            for (int i = 0; i < _jitWarmupTargetNames.Length; i++)
            {
                string targetName = _jitWarmupTargetNames[i];
                for (int j = 0; j < all.Length; j++)
                {
                    MethodInfo m = all[j];
                    if (m.Name != targetName) continue;
                    if (TryPrepare(m)) prepared++;
                    else failed++;
                }
            }

            // Pass 2: every method decorated with [MethodImpl(AggressiveInlining)].
            for (int j = 0; j < all.Length; j++)
            {
                MethodInfo m = all[j];
                MethodImplAttributes impl = m.GetMethodImplementationFlags();
                if ((impl & MethodImplAttributes.AggressiveInlining) == 0) continue;
                if (TryPrepare(m)) prepared++;
                else failed++;
            }

            // Dry run synthetic loop -- warms branch predictor + L1 cache without
            // touching brokers, orders, or expectedPositions. Reads membrane arrays
            // by index only; XOR checksum is discarded.
            int dryRunIterations = RunJitDryLoop();

            Print(string.Format("[MORPHEUS] JIT warm-up complete. {0} methods prepared.", prepared));
            if (failed > 0)
                Print(string.Format("[MORPHEUS] JIT warm-up: {0} method(s) skipped (generic/abstract/unavailable).", failed));
            Print(string.Format("[MORPHEUS] JIT dry-run loop iterated {0} membrane slots (no orders submitted).", dryRunIterations));
        }

        /// <summary>
        /// Attempt to JIT-prepare a single method. Skips abstract, generic, and
        /// generic-parameter-bearing methods which RuntimeHelpers.PrepareMethod
        /// cannot handle without a concrete generic instantiation.
        /// </summary>
        private static bool TryPrepare(MethodInfo m)
        {
            try
            {
                if (m == null) return false;
                if (m.IsAbstract) return false;
                if (m.IsGenericMethodDefinition) return false;
                if (m.ContainsGenericParameters) return false;
                RuntimeHelpers.PrepareMethod(m.MethodHandle);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Iterate every AccountIndex in the frozen membrane, reading slot data and
        /// XORing into a discarded checksum. Warms the branch predictor and L1 lines
        /// for the dispatch loop. NO orders submitted, NO state mutated.
        /// </summary>
        private int RunJitDryLoop()
        {
            FlattenedSubstrateState membrane = Volatile.Read(ref _membrane);
            if (membrane == null || membrane.AccountByIndex == null)
            {
                Print("[MORPHEUS] JIT dry-run skipped -- membrane not yet frozen.");
                return 0;
            }

            int accountCount = membrane.AccountCount;
            long checksum = 0;
            for (int i = 0; i < accountCount; i++)
            {
                if (i < membrane.AccountNameHashByIndex.Length)
                    checksum ^= unchecked((long)membrane.AccountNameHashByIndex[i]);
                if (i < membrane.ExpectedPositionByIndex.Length)
                    checksum ^= Volatile.Read(ref membrane.ExpectedPositionByIndex[i]);
                if (i < membrane.DispatchSyncPendingByIndex.Length)
                    checksum ^= Volatile.Read(ref membrane.DispatchSyncPendingByIndex[i]);
            }

            // Discarded sink -- prevents the JIT dead-code-elimination pass from removing the loop.
            Volatile.Write(ref _jitDryRunSink, checksum);
            return accountCount;
        }

        // Discard sink for the dry-run XOR checksum. Write-only; never read by hot path.
        private long _jitDryRunSink = 0;

        #endregion
    }
}
```

**Self-Audit (MUST PASS before STEP 4)**:
```bash
test -f src/V12_002.Photon.JitWarmup.cs && echo FILE_EXISTS
grep -n "private void WarmupJit" src/V12_002.Photon.JitWarmup.cs
grep -n "RuntimeHelpers.PrepareMethod" src/V12_002.Photon.JitWarmup.cs
grep -n "JIT warm-up complete" src/V12_002.Photon.JitWarmup.cs
grep -nE "lock\s*\(\s*stateLock\s*\)" src/V12_002.Photon.JitWarmup.cs
```
Expected:
- `FILE_EXISTS`
- 1 hit for `WarmupJit` definition
- 1 hit for `PrepareMethod`
- 1 hit for the exact warm-up banner string
- 0 hits for `lock(stateLock)`

ASCII gate (per-file):
```bash
python -c "import sys; b=open('src/V12_002.Photon.JitWarmup.cs','rb').read(); bad=[i for i,c in enumerate(b) if c>127]; print('ASCII_OK' if not bad else 'NON_ASCII_AT '+str(bad[:5]))"
```
Expected: `ASCII_OK`

---

### STEP 4 -- Create V12_002.Photon.Telemetry.cs (M7)

**Target**: `src/V12_002.Photon.Telemetry.cs`
**Action**: CREATE (new file)

**FULL FILE CONTENT**:
```csharp
// V12_002.Photon.Telemetry.cs -- M7 Dispatch Telemetry (Hybrid Arena Phase 2)
// Build 1112.001-v29.0
// Ultra-low-overhead, zero-allocation per-cycle dispatch counters. Reuses the
// existing _dispatchInvocationCount / _dispatchPeakElapsedTicks /
// _dispatchTotalElapsedTicks fields declared on V12_002 (V12_002.cs:574-578)
// and adds _lastDispatchTicks for per-call sampling. Stopwatch.Frequency is
// cached at static-class init to avoid repeated property dispatch on the hot path.
//
// Hot-path write (in V12_002.SIMA.Dispatch.cs after sw.Stop()):
//     Volatile.Write(ref _lastDispatchTicks, tFinalTicks);
//
// Read-side (cold path -- State.Terminated):
//     PrintPhotonTelemetryReport() -> StringBuilder with zero LINQ, zero $-interpolation.

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;

namespace NinjaTrader.NinjaScript.Strategies
{
    public partial class V12_002 : Strategy
    {
        #region M7 State

        // Frequency snapshot taken once at class init. Stopwatch.Frequency is a static
        // readonly property today, but caching avoids any future JIT-load overhead and
        // makes the conversion arithmetic auditable.
        private static readonly long _stopwatchFrequency = Stopwatch.Frequency;

        // Per-cycle sample. Written by the dispatch thread at the end of
        // ExecuteSmartDispatchEntry. Read by the terminate-thread report emitter.
        // Plain long; cross-thread visibility enforced via Volatile.Read/Write.
        private long _lastDispatchTicks = 0;

        // Recycled StringBuilder. 1 KiB initial capacity covers the 4-line report
        // with headroom. Reused across calls -- never replaced, never grown beyond
        // the report size.
        private readonly StringBuilder _telemetryReportBuilder = new StringBuilder(1024);

        #endregion

        #region M7 Hot-Path Sample Helper

        /// <summary>
        /// Single inlined accessor that the dispatch path uses to publish the
        /// final tick sample. Provided as a helper for clarity; the dispatch site
        /// may call Volatile.Write directly (see STEP 7).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void RecordPhotonDispatchSample(long elapsedTicks)
        {
            Volatile.Write(ref _lastDispatchTicks, elapsedTicks);
        }

        #endregion

        #region M7 Report Emitter

        /// <summary>
        /// Build the [MORPHEUS TELEMETRY] report into the recycled StringBuilder
        /// and return the formatted string. Cold path only -- never invoked from
        /// OnBarUpdate or the dispatch loop.
        /// Format (exact per M5-M7 brief):
        ///   [MORPHEUS TELEMETRY] BUILD 1112.001-v29.0
        ///     Total Dispatches : {N}
        ///     Peak Ticks       : {peak} ({peak_us} us)
        ///     Avg Ticks        : {avg} ({avg_us} us)
        /// </summary>
        public string GetPhotonTelemetryReport()
        {
            long total = Interlocked.Read(ref _dispatchInvocationCount);
            long peak  = Volatile.Read(ref _dispatchPeakElapsedTicks);
            long acc   = Interlocked.Read(ref _dispatchTotalElapsedTicks);
            long last  = Volatile.Read(ref _lastDispatchTicks);

            long avg = total > 0 ? acc / total : 0;

            // Microsecond conversion: ticks * 1_000_000 / Stopwatch.Frequency.
            // Stopwatch.Frequency on Windows is typically 10_000_000 (100ns ticks),
            // which yields integer-precise microseconds. On platforms with
            // non-10MHz frequencies the result is integer-truncated.
            long freq = _stopwatchFrequency;
            long peakUs = freq > 0 ? (peak * 1000000L) / freq : 0;
            long avgUs  = freq > 0 ? (avg  * 1000000L) / freq : 0;
            long lastUs = freq > 0 ? (last * 1000000L) / freq : 0;

            StringBuilder sb = _telemetryReportBuilder;
            sb.Length = 0;
            sb.Append("[MORPHEUS TELEMETRY] BUILD ");
            sb.Append(BUILD_TAG);
            sb.Append('\n');
            sb.Append("  Total Dispatches : ");
            sb.Append(total);
            sb.Append('\n');
            sb.Append("  Peak Ticks       : ");
            sb.Append(peak);
            sb.Append(" (");
            sb.Append(peakUs);
            sb.Append(" us)");
            sb.Append('\n');
            sb.Append("  Avg Ticks        : ");
            sb.Append(avg);
            sb.Append(" (");
            sb.Append(avgUs);
            sb.Append(" us)");
            sb.Append('\n');
            sb.Append("  Last Ticks       : ");
            sb.Append(last);
            sb.Append(" (");
            sb.Append(lastUs);
            sb.Append(" us)");
            return sb.ToString();
        }

        /// <summary>
        /// Print the telemetry report to the NinjaTrader output window. Called from
        /// State.Terminated AFTER EmitMetricsSummary so the two reports do not
        /// interleave.
        /// </summary>
        private void PrintPhotonTelemetryReport()
        {
            try
            {
                string report = GetPhotonTelemetryReport();
                Print(report);
            }
            catch (Exception ex)
            {
                // Telemetry emit must NEVER throw from terminate teardown.
                Print("[MORPHEUS TELEMETRY] emit failed: " + ex.Message);
            }
        }

        #endregion
    }
}
```

**Self-Audit (MUST PASS before STEP 5)**:
```bash
test -f src/V12_002.Photon.Telemetry.cs && echo FILE_EXISTS
grep -n "GetPhotonTelemetryReport\|PrintPhotonTelemetryReport\|RecordPhotonDispatchSample" src/V12_002.Photon.Telemetry.cs
grep -n "_stopwatchFrequency = Stopwatch.Frequency" src/V12_002.Photon.Telemetry.cs
grep -n "_lastDispatchTicks" src/V12_002.Photon.Telemetry.cs
grep -nE "lock\s*\(\s*stateLock\s*\)" src/V12_002.Photon.Telemetry.cs
grep -nE "string\.Format|\\\$\"" src/V12_002.Photon.Telemetry.cs
```
Expected:
- `FILE_EXISTS`
- 3+ hits for the three method names
- 1 hit for the cached frequency
- 3+ hits for `_lastDispatchTicks` (declaration + helper write + report read)
- 0 hits for `lock(stateLock)`
- 0 hits for `string.Format` or `$"` in the report builder (StringBuilder only -- DNA gate for hot-side helpers)

Verify field reuse against existing strategy declarations:
```bash
grep -n "_dispatchInvocationCount\|_dispatchPeakElapsedTicks\|_dispatchTotalElapsedTicks" src/V12_002.cs
```
Expected: 3 declarations at lines ~574-578 (must already exist from M1-M4).

ASCII gate (per-file):
```bash
python -c "import sys; b=open('src/V12_002.Photon.Telemetry.cs','rb').read(); bad=[i for i,c in enumerate(b) if c>127]; print('ASCII_OK' if not bad else 'NON_ASCII_AT '+str(bad[:5]))"
```
Expected: `ASCII_OK`

---

### STEP 5 -- Wire BindCpuAffinity + WarmupJit into State.DataLoaded

**Target**: `src/V12_002.Lifecycle.cs`
**Action**: MODIFY
**Anchor**: the `FreezeManagementMembrane()` call at the end of `State.DataLoaded` (line ~372).

**OLD** (exact match required):
```csharp
                // V12.2 HEADLESS SAFETY: Start core services even if ChartControl is null (for background execution)
                // [Build 932]: Start IPC in DataLoaded so Control Surface connects even if market is closed/offline.
                StartIpcServer();
                TouchStrategyHeartbeat();
                PublishUiSnapshot();
                FreezeManagementMembrane();
            }
            else if (state == State.Realtime)
```

**NEW**:
```csharp
                // V12.2 HEADLESS SAFETY: Start core services even if ChartControl is null (for background execution)
                // [Build 932]: Start IPC in DataLoaded so Control Surface connects even if market is closed/offline.
                StartIpcServer();
                TouchStrategyHeartbeat();
                PublishUiSnapshot();
                FreezeManagementMembrane();

                // M5-M7 (Build 1112.001-v29.0): cold-path performance pivot.
                // Order matters: bind CPU first so the JIT and dry-run loop both
                // execute on the bound core, populating its L1/L2 with the dispatch
                // pipeline machine code before the first OnBarUpdate fires.
                BindCpuAffinity();
                WarmupJit();
            }
            else if (state == State.Realtime)
```

**Self-Audit (MUST PASS before STEP 6)**:
```bash
grep -nB1 -A1 "BindCpuAffinity()" src/V12_002.Lifecycle.cs
grep -nB1 -A1 "WarmupJit()" src/V12_002.Lifecycle.cs
```
Expected: each call appears exactly once, ordered AFTER `FreezeManagementMembrane()` and BEFORE the `else if (state == State.Realtime)` branch.

```bash
awk '/State.DataLoaded/{flag=1} /State.Realtime/{flag=0} flag && /FreezeManagementMembrane|BindCpuAffinity|WarmupJit/{print NR": "$0}' src/V12_002.Lifecycle.cs
```
Expected: three lines in this exact order: `FreezeManagementMembrane`, `BindCpuAffinity`, `WarmupJit`.

---

### STEP 6 -- Wire PrintPhotonTelemetryReport into State.Terminated

**Target**: `src/V12_002.Lifecycle.cs`
**Action**: MODIFY
**Anchor**: the `EmitMetricsSummary();` call inside `State.Terminated` (line ~447).

**OLD** (exact match required):
```csharp
                // [BUILD 948] GTC Cancel Sweep -- cancel all tracked/broker V12 orders before teardown.
                // Must run while dicts are still populated and accounts still subscribed.
                // force=false: soft terminate, protects brackets for open positions.
                CancelAllV12GtcOrders(false);

                DrainQueuesForShutdown();
                EmitMetricsSummary();

                // Stop IPC Server
                StopIpcServer();
```

**NEW**:
```csharp
                // [BUILD 948] GTC Cancel Sweep -- cancel all tracked/broker V12 orders before teardown.
                // Must run while dicts are still populated and accounts still subscribed.
                // force=false: soft terminate, protects brackets for open positions.
                CancelAllV12GtcOrders(false);

                DrainQueuesForShutdown();
                EmitMetricsSummary();

                // M7 (Build 1112.001-v29.0): emit Photon dispatch telemetry AFTER
                // the legacy FSM metrics summary so the two reports do not interleave.
                PrintPhotonTelemetryReport();

                // Stop IPC Server
                StopIpcServer();
```

**Self-Audit (MUST PASS before STEP 7)**:
```bash
grep -nB1 -A1 "PrintPhotonTelemetryReport" src/V12_002.Lifecycle.cs
```
Expected: exactly one call site, appearing AFTER `EmitMetricsSummary()` and BEFORE `StopIpcServer()`.

```bash
awk '/State.Terminated/{flag=1} /State.Terminated/&&/}/{flag=0} flag && /EmitMetricsSummary|PrintPhotonTelemetryReport|StopIpcServer/{print NR": "$0}' src/V12_002.Lifecycle.cs | head -10
```
Expected order: `EmitMetricsSummary`, `PrintPhotonTelemetryReport`, `StopIpcServer`.

---

### STEP 7 -- Capture _lastDispatchTicks at end of dispatch

**Target**: `src/V12_002.SIMA.Dispatch.cs`
**Action**: MODIFY
**Anchor**: the existing `sw.Stop(); long tFinalTicks = sw.ElapsedTicks;` block (line ~626) at the end of `ExecuteSmartDispatchEntry`.

**OLD** (exact match required):
```csharp
                // [Phase 7.2 LATENCY] T_Final: Fleet loop complete (setup+enqueue only; no blocking Submit) -- stop clock, flush forensic report.
                sw.Stop();
                long tFinalTicks = sw.ElapsedTicks;
                Interlocked.Add(ref _dispatchTotalElapsedTicks, tFinalTicks);
                long peakTicks = Volatile.Read(ref _dispatchPeakElapsedTicks);
                while (tFinalTicks > peakTicks
                    && Interlocked.CompareExchange(ref _dispatchPeakElapsedTicks, tFinalTicks, peakTicks) != peakTicks)
                {
                    peakTicks = Volatile.Read(ref _dispatchPeakElapsedTicks);
                }
```

**NEW**:
```csharp
                // [Phase 7.2 LATENCY] T_Final: Fleet loop complete (setup+enqueue only; no blocking Submit) -- stop clock, flush forensic report.
                sw.Stop();
                long tFinalTicks = sw.ElapsedTicks;
                // M7 (Build 1112.001-v29.0): publish per-cycle sample for Photon telemetry.
                // Single Volatile.Write -- zero allocation, no LINQ, no closure.
                Volatile.Write(ref _lastDispatchTicks, tFinalTicks);
                Interlocked.Add(ref _dispatchTotalElapsedTicks, tFinalTicks);
                long peakTicks = Volatile.Read(ref _dispatchPeakElapsedTicks);
                while (tFinalTicks > peakTicks
                    && Interlocked.CompareExchange(ref _dispatchPeakElapsedTicks, tFinalTicks, peakTicks) != peakTicks)
                {
                    peakTicks = Volatile.Read(ref _dispatchPeakElapsedTicks);
                }
```

**Self-Audit (MUST PASS before STEP 8)**:
```bash
grep -n "_lastDispatchTicks" src/V12_002.SIMA.Dispatch.cs
```
Expected: exactly 1 hit (the new `Volatile.Write`).

```bash
grep -nE "ExecuteSmartDispatchEntry|sw\.Stop\(\)|_lastDispatchTicks|_dispatchTotalElapsedTicks" src/V12_002.SIMA.Dispatch.cs | head
```
Expected order: `sw.Stop()` -> `_lastDispatchTicks` -> `_dispatchTotalElapsedTicks` (new write must precede the accumulator add).

Confirm DNA: zero allocation in the new line.
```bash
grep -nE "new\s+\w+\(|string\.Format" src/V12_002.SIMA.Dispatch.cs | grep -i "lastdispatch"
```
Expected: 0 hits.

---

### STEP 8 -- Update deploy-sync.ps1 file map

**Target**: `deploy-sync.ps1`
**Action**: MODIFY
**Anchor**: the existing Photon block at lines 52-55. Insert the three new entries immediately after `V12_002.Photon.Substrate.cs`.

**OLD** (exact match required):
```powershell
    @{ src = "V12_002.Photon.MmioMirror.cs"; dst = Join-Path $NtStrategyDir "V12_002.Photon.MmioMirror.cs" },
    @{ src = "V12_002.Photon.Pool.cs"; dst = Join-Path $NtStrategyDir "V12_002.Photon.Pool.cs" },
    @{ src = "V12_002.Photon.Ring.cs"; dst = Join-Path $NtStrategyDir "V12_002.Photon.Ring.cs" },
    @{ src = "V12_002.Photon.Substrate.cs"; dst = Join-Path $NtStrategyDir "V12_002.Photon.Substrate.cs" },
    @{ src = "V12_002.Safety.Watchdog.cs"; dst = Join-Path $NtStrategyDir "V12_002.Safety.Watchdog.cs" },
```

**NEW**:
```powershell
    @{ src = "V12_002.Photon.MmioMirror.cs"; dst = Join-Path $NtStrategyDir "V12_002.Photon.MmioMirror.cs" },
    @{ src = "V12_002.Photon.Pool.cs"; dst = Join-Path $NtStrategyDir "V12_002.Photon.Pool.cs" },
    @{ src = "V12_002.Photon.Ring.cs"; dst = Join-Path $NtStrategyDir "V12_002.Photon.Ring.cs" },
    @{ src = "V12_002.Photon.Substrate.cs"; dst = Join-Path $NtStrategyDir "V12_002.Photon.Substrate.cs" },
    # M5-M7 (Build 1112.001-v29.0) Performance Integration
    @{ src = "V12_002.Photon.Affinity.cs"; dst = Join-Path $NtStrategyDir "V12_002.Photon.Affinity.cs" },
    @{ src = "V12_002.Photon.JitWarmup.cs"; dst = Join-Path $NtStrategyDir "V12_002.Photon.JitWarmup.cs" },
    @{ src = "V12_002.Photon.Telemetry.cs"; dst = Join-Path $NtStrategyDir "V12_002.Photon.Telemetry.cs" },
    @{ src = "V12_002.Safety.Watchdog.cs"; dst = Join-Path $NtStrategyDir "V12_002.Safety.Watchdog.cs" },
```

**Self-Audit (MUST PASS before VALIDATION GATE)**:
```bash
grep -n "V12_002.Photon.Affinity.cs\|V12_002.Photon.JitWarmup.cs\|V12_002.Photon.Telemetry.cs" deploy-sync.ps1
```
Expected: 6 hits (each filename appears twice -- src + dst path).

```bash
powershell -NoProfile -Command "(Get-Content deploy-sync.ps1) -join \"`n\" | ForEach-Object { if ($_ -match 'V12_002.Photon.(Affinity|JitWarmup|Telemetry).cs') { Write-Host $_ } }"
```
Expected: lines listing the three new file mappings.

---

## 5. Validation Gate (after all 8 steps)

Codex P4 must run ALL of these in order before declaring M5-M7 done. Any FAIL halts the rollout and routes back to the failing step.

### 5.1 ASCII Gate (project-wide)
```bash
python C:\tmp\byte_purge.py
```
Or, if using the in-repo gate:
```powershell
powershell -NoProfile -File deploy-sync.ps1
```
Expected: `ASCII GATE PASS - all source files are clean`. Any non-ASCII byte aborts.

### 5.2 DNA Gates (project-wide)
```bash
grep -rnE "lock\s*\(\s*stateLock\s*\)" src/ | grep -v "private readonly object stateLock" | grep -v "private readonly object dailySummaryLock"
```
Expected: 0 hits. The dummy `stateLock` declaration in `V12_002.cs:226` is allowed; ANY `lock(stateLock) { ... }` block is BANNED.

```bash
grep -rn "ThreadPriority.Realtime" src/
```
Expected: 0 hits. NinjaTrader forbids Realtime.

### 5.3 Method Wiring
```bash
grep -rn "BindCpuAffinity\|WarmupJit\|PrintPhotonTelemetryReport\|RecordPhotonDispatchSample\|GetPhotonTelemetryReport" src/
```
Expected lines (canonical wiring):
- `src/V12_002.Photon.Affinity.cs`: 1 BindCpuAffinity definition
- `src/V12_002.Photon.JitWarmup.cs`: 1 WarmupJit definition
- `src/V12_002.Photon.Telemetry.cs`: 1 each for the three telemetry methods
- `src/V12_002.Lifecycle.cs`: 1 BindCpuAffinity call, 1 WarmupJit call, 1 PrintPhotonTelemetryReport call
- `src/V12_002.SIMA.Dispatch.cs`: 1 `Volatile.Write(ref _lastDispatchTicks ...)` line (no method call needed, the assignment is inline)

### 5.4 Compile in NinjaTrader 8
1. Run `powershell -NoProfile -File deploy-sync.ps1` (must report `SYNC COMPLETE` with the 3 new files linked).
2. In NinjaTrader 8: `Tools -> Edit NinjaScript -> Strategy -> V12_002.cs -> Compile (F5)`.
3. Expected: `Compile succeeded`. ZERO errors. ZERO warnings related to the new partials.
4. If 300+ errors appear: ASCII gate failed; run `python C:\tmp\byte_purge.py` and re-deploy.

### 5.5 Live Output Window (Sim101 minimum)
1. Apply the V12_002 strategy to a chart with `EnableSIMA = false` and `EnablePhotonAffinityBind = true`, `CpuAffinityMask = 4`.
2. On strategy enable, the output window MUST contain (in this order):
   - `[MORPHEUS] CPU affinity bound: mask=0x4 prevMask=0x... threadId=... isBackground=... priority=Highest`
   - `[MORPHEUS] JIT warm-up complete. N methods prepared.` (N >= 5)
   - `[MORPHEUS] JIT dry-run loop iterated K membrane slots (no orders submitted).` (K >= 0)
3. Disable the strategy. The output window MUST contain (after the existing SESSION METRICS REPORT):
   - `[MORPHEUS TELEMETRY] BUILD 1112.001-v29.0`
   - `  Total Dispatches : ...`
   - `  Peak Ticks       : ... (... us)`
   - `  Avg Ticks        : ... (... us)`
   - `  Last Ticks       : ... (... us)`
4. With `EnablePhotonAffinityBind = false`: instead of the "CPU affinity bound" line, the output MUST contain `[MORPHEUS] CPU affinity disabled (EnablePhotonAffinityBind=false) -- thread runs unbound.`

### 5.6 Regression Sanity (Sim101)
1. With `EnableSIMA = true` and at least one Apex follower, fire a manual entry. Expect the existing `+========+` FORENSIC PULSE REPORT to print exactly as before -- the Volatile.Write injection at STEP 7 must not perturb the existing accumulator/peak logic.
2. Confirm `[PUMP] Submitted N orders for Fleet_... | <account>` lines appear as before.
3. Flatten and disable. Confirm both `EmitMetricsSummary` and `PrintPhotonTelemetryReport` print without exception.

PASS = all 6 gates green. Anything else = halt and route the failing step back to Codex.

---

## 6. M8 Handoff Note

M5-M7 deliver the three cold-path scaffolds (deterministic core binding, hot-method JIT pre-compilation, and per-cycle dispatch telemetry) needed to certify M8's <100 ns dispatch budget. M8 will (a) measure `_lastDispatchTicks` and `_dispatchPeakElapsedTicks` under live Rithmic Direct Connection load, (b) confirm the affinity bind survives the NinjaTrader thread lifecycle through reconnects and Realtime entry, (c) run the Logic Audit Cases 10-12 against the v30.0 substrate, and (d) gate the production cutover on a sustained Peak-Ticks budget that translates to <100 ns under the cached `_stopwatchFrequency`. Any M8 finding that the bound thread is NOT the one executing `OnBarUpdate` will trigger a follow-up patch routing the bind through `OnBarUpdate`'s first-call site instead of `State.DataLoaded`.

---

## 7. Orchestrator-Worker-Validator Cheat Sheet

| Phase | Actor | Action |
|-------|-------|--------|
| Brief | Antigravity (P1) | Hand Step N to Codex, attach the OLD/NEW block from this plan |
| Worker | Codex (P4) | Execute Step N exactly. No stepping ahead. Run the self-audit. |
| Validator | Codex (P4) | Report PASS or FAIL with self-audit output. |
| Gate | Antigravity (P1) | If PASS -> assign Step N+1. If FAIL -> route correction to Codex. |
| Final | Antigravity (P1) | After Step 8 PASS, run Validation Gate 5.1-5.6. |
| Sign-off | Director (P5) | Approves live cutover after 5.6 regression sanity. |

Sequential. No parallel execution. Each step lands and is verified before the next is dispatched.

---

## 8. Director Approval Checklist

- [ ] Plan reviewed and approved by Director (P5)
- [ ] Codex (P4) confirms Step 0 (file copy) and acknowledges the 8-step sequence
- [ ] Antigravity (P1) opens the orchestration thread and dispatches Step 1
- [ ] Validation Gate 5.1-5.6 all green before declaring M5-M7 complete
- [ ] M8 mission brief drafted (separate document) before live cutover
