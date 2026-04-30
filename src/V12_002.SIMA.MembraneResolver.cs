using System;
using System.Threading;
using System.Runtime.CompilerServices;

namespace NinjaTrader.NinjaScript.Strategies
{
    /// <summary>
    /// Concrete IMembraneResolver. Reads the membrane via Volatile.Read and
    /// triggers FreezeManagementMembrane() when the membrane is absent.
    ///
    /// NO-LOCKS: Volatile.Read provides acquire semantics without a lock.
    /// HOT-PATH SAFETY: IsAccountIndexValid() is inlined; it does NOT call
    ///   IsActive() -- that remains on FlattenedSubstrateState where it is
    ///   already AggressiveInlining-decorated. This class only checks bounds.
    /// </summary>
    internal sealed class MembraneResolver : IMembraneResolver
    {
        private readonly Func<FlattenedSubstrateState> _volatileRead;
        private readonly Action                        _freeze;
        private readonly Action<string>                _print;

        public MembraneResolver(
            Func<FlattenedSubstrateState> volatileRead,
            Action freeze,
            Action<string> print)
        {
            _volatileRead = volatileRead ?? throw new ArgumentNullException("volatileRead");
            _freeze       = freeze       ?? throw new ArgumentNullException("freeze");
            _print        = print        ?? throw new ArgumentNullException("print");
        }

        /// <inheritdoc/>
        public bool TryResolve(out FlattenedSubstrateState membrane)
        {
            membrane = _volatileRead();

            if (membrane == null || membrane.FreezeStamp == 0)
            {
                _freeze();
                membrane = _volatileRead();
            }

            if (membrane == null || membrane.FreezeStamp == 0)
            {
                _print("[DISPATCH] [ERR] Management Membrane unavailable - dispatch aborted");
                return false;
            }

            return true;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsAccountIndexValid(FlattenedSubstrateState membrane, int accountIndex)
        {
            // Bounds check only -- does not call IsActive() (that is the caller's choice).
            return accountIndex >= 0
                && membrane.AccountByIndex != null
                && accountIndex < membrane.AccountByIndex.Length;
        }
    }
}
