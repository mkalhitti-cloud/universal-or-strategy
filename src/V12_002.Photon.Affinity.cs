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
