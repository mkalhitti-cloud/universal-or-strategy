using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace NinjaTrader.NinjaScript.Strategies
{
    /// <summary>
    /// Concrete IAccountIndexResolver. Reads AccountIndexByName from the
    /// membrane snapshot (which is frozen -- no mutation after FreezeStamp set).
    ///
    /// NO-LOCKS: Dictionary is read-only after membrane freeze.
    /// HOT-PATH SAFETY: Inlined because called once per fleet account.
    /// </summary>
    internal sealed class AccountIndexResolver : IAccountIndexResolver
    {
        private readonly Action<string> _print;

        public AccountIndexResolver(Action<string> print)
        {
            _print = print;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryResolve(
            FlattenedSubstrateState membrane,
            string accountName,
            out int accountIndex)
        {
            accountIndex = -1;

            if (membrane.AccountIndexByName == null)
            {
                _print(string.Format("[DISPATCH] Membrane AccountIndexByName null for {0} -- skipped",
                    accountName));
                return false;
            }

            if (!membrane.AccountIndexByName.TryGetValue(accountName, out accountIndex))
            {
                _print(string.Format("[DISPATCH] Membrane index missing for {0} -- skipped",
                    accountName));
                return false;
            }

            if (accountIndex < 0 || membrane.AccountByIndex == null
                || accountIndex >= membrane.AccountByIndex.Length)
            {
                _print(string.Format("[DISPATCH] Membrane index out of range for {0} index={1} -- skipped",
                    accountName, accountIndex));
                accountIndex = -1;
                return false;
            }

            return true;
        }
    }
}
