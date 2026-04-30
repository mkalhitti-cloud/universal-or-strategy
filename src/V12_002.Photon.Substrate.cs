using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using NinjaTrader.Cbi;

// Hybrid Arena v29.0: Management Membrane substrate.
//
// Structure-of-Arrays view of the fleet that is frozen at State.DataLoaded by
// FreezeManagementMembrane(). After freeze, the hot path indexes these arrays by
// AccountIndex and never enumerates Account.All, never allocates HashSet<string>,
// and never performs string-keyed lookups in the dispatch loop.

namespace NinjaTrader.NinjaScript.Strategies
{
    public partial class V12_002 : Strategy
    {

        private const int ACCT_ACTIVE             = 1 << 0;
        private const int ACCT_CONSISTENCY_LOCKED = 1 << 1;
        private const int ACCT_HAS_OPEN_POSITION  = 1 << 2;
        private const int ACCT_FLAT_PENDING       = 1 << 3;

        private const int TIER_MASTER   = 0;
        private const int TIER_ANCHOR   = 1;
        private const int TIER_FOLLOWER = 2;

        private sealed class FlattenedSubstrateState
        {
            public int       AccountCount;
            public Account[] AccountByIndex;
            public int[]     AccountState;
            public int[]     AccountFleetTier;
            public double[]  AccountTickSize;
            public double[]  AccountDailyPnLSnap;
            public int[]     AccountQtyMultiplier;
            public ulong[]   AccountNameHashByIndex;
            public long      FreezeStamp;

            public int[] ExpectedPositionByIndex;
            public int[] DispatchSyncPendingByIndex;

            public Dictionary<string, int> AccountIndexByName;
            public Dictionary<ulong, int>  AccountIndexByNameHash;

            public double InstrumentTickSize;
            public int    InstrumentDigits;
            public ulong  InstrumentHash;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool IsActive(int accountIndex)
            {
                int s = AccountState[accountIndex];
                return (s & ACCT_ACTIVE) != 0 && (s & ACCT_CONSISTENCY_LOCKED) == 0;
            }
        }

        private FlattenedSubstrateState _membrane;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong XxHash64FleetEntry(string s)
        {
            if (string.IsNullOrEmpty(s))
                return 0UL;

            const ulong FnvOffset = 0xCBF29CE484222325UL;
            const ulong FnvPrime = 0x00000100000001B3UL;

            ulong h = FnvOffset;
            for (int i = 0; i < s.Length; i++)
            {
                h ^= (byte)s[i];
                h *= FnvPrime;
            }

            return h;
        }



        private void FreezeManagementMembrane()
        {
            try
            {
                var fleetList = new List<Account>(16);
                foreach (Account acct in Account.All)
                {
                    if (acct == null)
                        continue;
                    if (!IsFleetAccount(acct))
                        continue;
                    fleetList.Add(acct);
                }

                int n = fleetList.Count;
                var membrane = new FlattenedSubstrateState
                {
                    AccountCount = n,
                    AccountByIndex = new Account[n],
                    AccountState = new int[n],
                    AccountFleetTier = new int[n],
                    AccountTickSize = new double[n],
                    AccountDailyPnLSnap = new double[n],
                    AccountQtyMultiplier = new int[n],
                    AccountNameHashByIndex = new ulong[n],
                    ExpectedPositionByIndex = new int[n],
                    DispatchSyncPendingByIndex = new int[n],
                    AccountIndexByName = new Dictionary<string, int>(n, StringComparer.OrdinalIgnoreCase),
                    AccountIndexByNameHash = new Dictionary<ulong, int>(n)
                };

                double instrumentTickSize = Instrument != null && Instrument.MasterInstrument != null
                    ? Instrument.MasterInstrument.TickSize
                    : 0.0;
                int instrumentDigits = Instrument != null && Instrument.MasterInstrument != null
                    ? (Instrument.MasterInstrument.PointValue > 0 ? 2 : 0)
                    : 0;
                ulong instrumentHash = Instrument != null
                    ? XxHash64FleetEntry(Instrument.FullName)
                    : 0UL;

                membrane.InstrumentTickSize = instrumentTickSize;
                membrane.InstrumentDigits = instrumentDigits;
                membrane.InstrumentHash = instrumentHash;

                int parityMult = Math.Max(1, FleetParityMultiplier);

                for (int i = 0; i < n; i++)
                {
                    Account acct = fleetList[i];
                    membrane.AccountByIndex[i] = acct;
                    membrane.AccountIndexByName[acct.Name] = i;

                    ulong nameHash = XxHash64FleetEntry(acct.Name);
                    membrane.AccountNameHashByIndex[i] = nameHash;
                    membrane.AccountIndexByNameHash[nameHash] = i;

                    int state = 0;
                    bool isActive;
                    if (activeFleetAccounts.TryGetValue(acct.Name, out isActive) && isActive)
                        state |= ACCT_ACTIVE;

                    string expectedKey = ExpKey(acct.Name);
                    double dailyPnL = 0.0;
                    try
                    {
                        dailyPnL = acct.Get(AccountItem.RealizedProfitLoss, Currency.UsDollar);
                    }
                    catch (Exception pnlEx)
                    {
                        Print("[FREEZE] WARN: RealizedProfitLoss snapshot failed for " + acct.Name + ": " + pnlEx.Message);
                    }

                    membrane.AccountDailyPnLSnap[i] = dailyPnL;

                    if (EnableConsistencyLock && dailyPnL >= MaxDailyProfitCap)
                        state |= ACCT_CONSISTENCY_LOCKED;

                    bool hasOpen = false;
                    try
                    {
                        var positions = acct.Positions;
                        if (positions != null)
                        {
                            for (int p = 0; p < positions.Count; p++)
                            {
                                var pos = positions[p];
                                if (pos != null && pos.Quantity != 0)
                                {
                                    hasOpen = true;
                                    break;
                                }
                            }
                        }
                    }
                    catch (Exception posEx)
                    {
                        Print("[FREEZE] WARN: Position snapshot failed for " + acct.Name + ": " + posEx.Message);
                    }

                    if (hasOpen)
                        state |= ACCT_HAS_OPEN_POSITION;

                    membrane.AccountState[i] = state;
                    membrane.AccountFleetTier[i] = ResolveFleetTier(acct);
                    membrane.AccountTickSize[i] = instrumentTickSize;
                    membrane.AccountQtyMultiplier[i] = parityMult;

                    int prevExpected = 0;
                    expectedPositions.TryGetValue(expectedKey, out prevExpected);
                    membrane.ExpectedPositionByIndex[i] = prevExpected;
                    membrane.DispatchSyncPendingByIndex[i] =
                        _dispatchSyncPendingExpKeys.ContainsKey(expectedKey) ? 1 : 0;
                }

                membrane.FreezeStamp = DateTime.UtcNow.Ticks;
                Volatile.Write(ref _membrane, membrane);

                Print(string.Format(
                    "[FREEZE] Management Membrane frozen: {0} fleet accounts, tick={1}, parity={2}, freezeStamp={3}",
                    n,
                    instrumentTickSize.ToString("F4"),
                    parityMult,
                    membrane.FreezeStamp));
            }
            catch (Exception ex)
            {
                Print("[FREEZE] FATAL: FreezeManagementMembrane threw -- hot dispatch will continue using prior membrane. " + ex.Message);
            }
        }

        private int ResolveFleetTier(Account acct)
        {
            if (acct == null)
                return TIER_FOLLOWER;
            if (this.Account != null && acct == this.Account)
                return TIER_MASTER;
            return TIER_FOLLOWER;
        }
    }
}
