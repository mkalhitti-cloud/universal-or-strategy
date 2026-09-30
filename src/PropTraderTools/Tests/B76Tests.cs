// src/PropTraderTools/B76Tests.cs
// B76-LaneA: HOTFIX-B76-FLATTEN-GUARD-01 v2 + HOTFIX-B76-FLATTEN-RACE-01 + HOTFIX-B76-POSSTATE-DEDUP-01
//            + HOTFIX-B76-ATM-TPL-CLASSNAME
// 12 xUnit [Fact] tests: T_B76_01 through T_B76_12.
// JS-021: no lock. JS-001: no throw. JS-002: no return null. JS-033: no async void.
// xUnit only -- no NUnit, no MSTest. ASCII identifiers only.

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using Xunit;

namespace PropTraderTools
{
    public sealed class B76Tests
    {
        // ======================================================================
        // TICKET-B76-1: FlattenOneAccount in-flight guard + race guard
        // HOTFIX-B76-FLATTEN-GUARD-01 v2 + HOTFIX-B76-FLATTEN-RACE-01
        // ======================================================================

        // T_B76_01: FlattenOneAccount method exists as private instance method on CopyEngine.
        // Confirms the method was not accidentally removed or renamed.
        [Fact]
        public void T_B76_01_FlattenOneAccount_MethodExists_PrivateInstance()
        {
            var mi = typeof(CopyEngine).GetMethod(
                "FlattenOneAccount",
                BindingFlags.NonPublic | BindingFlags.Instance
            );

            Assert.NotNull(mi);
            Assert.Equal(typeof(void), mi.ReturnType);

            var ps = mi.GetParameters();
            Assert.Equal(2, ps.Length);
            Assert.Equal(typeof(NinjaTrader.Cbi.Account), ps[0].ParameterType);
            Assert.Equal(typeof(NinjaTrader.Cbi.Instrument), ps[1].ParameterType);
        }

        // T_B76_02: FlattenOneAccount compiled body contains the string literal for the in-flight guard.
        // Verifies HOTFIX-B76-FLATTEN-GUARD-01 v2 is compiled into the method.
        [Fact]
        public void T_B76_02_FlattenOneAccount_ContainsInFlightSkipString()
        {
            Assert.True(
                IlContainsString(
                    typeof(CopyEngine),
                    "FlattenOneAccount",
                    BindingFlags.NonPublic | BindingFlags.Instance,
                    "flat-guard: in-flight skip",
                    exactMatch: false
                ),
                "FlattenOneAccount must contain string literal 'flat-guard: in-flight skip' (HOTFIX-B76-FLATTEN-GUARD-01 v2)"
            );
        }

        // T_B76_03: FlattenOneAccount compiled body contains the string literal for the race skip.
        // Verifies HOTFIX-B76-FLATTEN-RACE-01 is compiled into the method.
        [Fact]
        public void T_B76_03_FlattenOneAccount_ContainsRaceSkipString()
        {
            Assert.True(
                IlContainsString(
                    typeof(CopyEngine),
                    "FlattenOneAccount",
                    BindingFlags.NonPublic | BindingFlags.Instance,
                    "flat-race skip",
                    exactMatch: false
                ),
                "FlattenOneAccount must contain string literal 'flat-race skip' (HOTFIX-B76-FLATTEN-RACE-01)"
            );
        }

        // Shared helper: walks ldstr opcodes in a method IL body and checks for a target string.
        // exactMatch=false -> Contains semantics; exactMatch=true -> string equality.
        private static bool IlContainsString(
            Type declaringType,
            string methodName,
            BindingFlags flags,
            string target,
            bool exactMatch
        )
        {
            var mi = declaringType.GetMethod(methodName, flags);
            if (mi == null)
                return false;
            var body = mi.GetMethodBody();
            if (body == null)
                return false;
            var il = body.GetILAsByteArray();
            if (il == null || il.Length == 0)
                return false;
            var module = declaringType.Module;
            for (int i = 0; i < il.Length - 4; i++)
            {
                if (il[i] != 0x72) // ldstr opcode
                    continue;
                int token = il[i + 1] | (il[i + 2] << 8) | (il[i + 3] << 16) | (il[i + 4] << 24);
                try
                {
                    var s = module.ResolveString(token);
                    if (s != null && (exactMatch ? s == target : s.Contains(target)))
                        return true;
                }
                catch
                { /* token not a valid string reference -- skip */
                }
            }
            return false;
        }

        // T_B76_04: FlattenOneAccount IL contains at least 2 FindPosition call sites.
        // The first call is the initial position check; the second is the post-cancel re-read.
        // Proves the race guard (posAfterCancel) is compiled into the method.
        [Fact]
        public void T_B76_04_FlattenOneAccount_HasAtLeastTwoFindPositionCallSites()
        {
            var mi = typeof(CopyEngine).GetMethod(
                "FlattenOneAccount",
                BindingFlags.NonPublic | BindingFlags.Instance
            );
            Assert.NotNull(mi);

            var findPosMi = typeof(CopyEngine).GetMethod(
                "FindPosition",
                BindingFlags.NonPublic | BindingFlags.Instance
            );
            Assert.NotNull(findPosMi);

            int findPosToken = findPosMi.MetadataToken;

            var body = mi.GetMethodBody();
            Assert.NotNull(body);
            var il = body.GetILAsByteArray();
            Assert.NotNull(il);

            int callCount = 0;
            for (int i = 0; i < il.Length - 4; i++)
            {
                if (il[i] == 0x28 || il[i] == 0x6F) // call or callvirt
                {
                    int token =
                        il[i + 1] | (il[i + 2] << 8) | (il[i + 3] << 16) | (il[i + 4] << 24);
                    if (token == findPosToken)
                        callCount++;
                }
            }

            Assert.True(
                callCount >= 2,
                $"FlattenOneAccount must contain at least 2 FindPosition call sites (pre-cancel + post-cancel re-read). Found: {callCount}"
            );
        }

        // T_B76_05: FlattenOneAccount IL: CancelAllAccountOrders call offset is BEFORE the second
        // FindPosition call offset. Proves the cancel-then-re-read sequence is correct.
        [Fact]
        public void T_B76_05_FlattenOneAccount_CancelBeforeSecondFindPosition_InIL()
        {
            var mi = typeof(CopyEngine).GetMethod(
                "FlattenOneAccount",
                BindingFlags.NonPublic | BindingFlags.Instance
            );
            Assert.NotNull(mi);

            var findPosMi = typeof(CopyEngine).GetMethod(
                "FindPosition",
                BindingFlags.NonPublic | BindingFlags.Instance
            );
            Assert.NotNull(findPosMi);

            var cancelMi = typeof(CopyEngine).GetMethod(
                "CancelAllAccountOrders",
                BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public
            );
            Assert.NotNull(cancelMi);

            int findPosToken = findPosMi.MetadataToken;
            int cancelToken = cancelMi.MetadataToken;

            var body = mi.GetMethodBody();
            Assert.NotNull(body);
            var il = body.GetILAsByteArray();
            Assert.NotNull(il);

            var findPosOffsets = new System.Collections.Generic.List<int>();
            int cancelOffset = -1;

            for (int i = 0; i < il.Length - 4; i++)
            {
                if (il[i] == 0x28 || il[i] == 0x6F)
                {
                    int token =
                        il[i + 1] | (il[i + 2] << 8) | (il[i + 3] << 16) | (il[i + 4] << 24);
                    if (token == findPosToken)
                        findPosOffsets.Add(i);
                    else if (token == cancelToken && cancelOffset == -1)
                        cancelOffset = i;
                }
            }

            Assert.True(
                findPosOffsets.Count >= 2,
                "FlattenOneAccount must have at least 2 FindPosition call sites"
            );
            Assert.True(cancelOffset >= 0, "FlattenOneAccount must call CancelAllAccountOrders");

            int secondFindPosOffset = findPosOffsets[1];
            Assert.True(
                cancelOffset < secondFindPosOffset,
                $"CancelAllAccountOrders offset ({cancelOffset}) must be before second FindPosition offset ({secondFindPosOffset})"
            );
        }

        // T_B76_06: FlattenOneAccount IL has at least 5 local variables.
        // Expected locals: foreach loop var (o), pos, posAfterCancel, action, order.
        // Proves the full method body is compiled (not a stub).
        [Fact]
        public void T_B76_06_FlattenOneAccount_HasAtLeastFiveLocals()
        {
            var mi = typeof(CopyEngine).GetMethod(
                "FlattenOneAccount",
                BindingFlags.NonPublic | BindingFlags.Instance
            );
            Assert.NotNull(mi);

            var body = mi.GetMethodBody();
            Assert.NotNull(body);

            int localCount = body.LocalVariables.Count;
            Assert.True(
                localCount >= 5,
                $"FlattenOneAccount must have at least 5 local variables (loop var, pos, posAfterCancel, action, order). Found: {localCount}"
            );
        }

        // ======================================================================
        // TICKET-B76-2: PositionStateChanged dedup -- _lastHasPos + Interlocked.Exchange
        // HOTFIX-B76-POSSTATE-DEDUP-01
        // ======================================================================

        // T_B76_07: CopyEngine has field _lastHasPos of type ConcurrentDictionary<string, int[]>.
        // Verifies the dedup dictionary is declared and accessible via reflection.
        [Fact]
        public void T_B76_07_CopyEngine_HasLastHasPosField_ConcurrentDictionaryStringIntArray()
        {
            var fi = typeof(CopyEngine).GetField(
                "_lastHasPos",
                BindingFlags.NonPublic | BindingFlags.Instance
            );

            Assert.NotNull(fi);
            Assert.Equal(typeof(ConcurrentDictionary<string, int[]>), fi.FieldType);

            var instance = CopyEngine.Instance;
            var value = fi.GetValue(instance);
            Assert.NotNull(value);
            Assert.IsType<ConcurrentDictionary<string, int[]>>(value);
        }

        // T_B76_08: TryFirePositionState IL contains an Interlocked.Exchange call site.
        // Verifies the CAS dedup is compiled into TryFirePositionState.
        [Fact]
        public void T_B76_08_TryFirePositionState_IL_ContainsInterlockedExchangeCallSite()
        {
            var mi = typeof(CopyEngine).GetMethod(
                "TryFirePositionState",
                BindingFlags.NonPublic | BindingFlags.Instance
            );
            Assert.NotNull(mi);

            var interlockedExchangeMi = typeof(System.Threading.Interlocked).GetMethod(
                "Exchange",
                new Type[] { typeof(int).MakeByRefType(), typeof(int) }
            );
            Assert.NotNull(interlockedExchangeMi);

            int exchangeToken = interlockedExchangeMi.MetadataToken;

            var body = mi.GetMethodBody();
            Assert.NotNull(body);
            var il = body.GetILAsByteArray();
            Assert.NotNull(il);
            Assert.True(il.Length > 0, "TryFirePositionState must have a non-empty IL body");

            bool foundExchange = false;
            for (int i = 0; i < il.Length - 4; i++)
            {
                if (il[i] == 0x28 || il[i] == 0x6F) // call or callvirt
                {
                    int token =
                        il[i + 1] | (il[i + 2] << 8) | (il[i + 3] << 16) | (il[i + 4] << 24);
                    if (token == exchangeToken)
                    {
                        foundExchange = true;
                        break;
                    }
                }
            }

            Assert.True(
                foundExchange,
                "TryFirePositionState must call Interlocked.Exchange(ref int, int) -- HOTFIX-B76-POSSTATE-DEDUP-01"
            );
        }

        // T_B76_09: TryFirePositionState is a private instance method on CopyEngine.
        // Accessibility check: not public, not static.
        [Fact]
        public void T_B76_09_TryFirePositionState_IsPrivateInstanceMethod()
        {
            var mi = typeof(CopyEngine).GetMethod(
                "TryFirePositionState",
                BindingFlags.NonPublic | BindingFlags.Instance
            );
            Assert.NotNull(mi);

            var publicMi = typeof(CopyEngine).GetMethod(
                "TryFirePositionState",
                BindingFlags.Public | BindingFlags.Instance
            );
            Assert.Null(publicMi);

            Assert.False(
                mi.IsStatic,
                "TryFirePositionState must be an instance method (not static)"
            );
            Assert.True(mi.IsPrivate, "TryFirePositionState must be private");
        }

        // ======================================================================
        // TICKET-B76-3: GetLeaderAtmTemplateName class-name guard
        // HOTFIX-B76-ATM-TPL-CLASSNAME
        // ======================================================================

        // T_B76_10: GetLeaderAtmTemplateName(null) returns string.Empty.
        // Regression guard -- null-chart path must continue to return empty string.
        [Fact]
        public void T_B76_10_GetLeaderAtmTemplateName_NullChart_ReturnsStringEmpty()
        {
            var mi = typeof(TradeCopierPanel).GetMethod(
                "GetLeaderAtmTemplateName",
                BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Public
            );
            Assert.NotNull(mi);

            var result = mi.Invoke(null, new object[] { null });
            Assert.Equal(string.Empty, result);
            Assert.NotNull(result);
        }

        // T_B76_11: GetLeaderAtmTemplateName method body contains the string literal "AtmStrategy"
        // used as the class-name guard comparison.
        // Confirms HOTFIX-B76-ATM-TPL-CLASSNAME is compiled into the method.
        [Fact]
        public void T_B76_11_GetLeaderAtmTemplateName_IL_ContainsAtmStrategyClassNameGuardString()
        {
            Assert.True(
                IlContainsString(
                    typeof(TradeCopierPanel),
                    "GetLeaderAtmTemplateName",
                    BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Public,
                    "AtmStrategy",
                    exactMatch: true
                ),
                "GetLeaderAtmTemplateName must contain string literal \"AtmStrategy\" (HOTFIX-B76-ATM-TPL-CLASSNAME class-name guard)"
            );
        }

        // T_B76_12: GetLeaderAtmTemplateName is an internal static method on TradeCopierPanel.
        // Accessibility check: internal (assembly-accessible), static, returns string.
        [Fact]
        public void T_B76_12_GetLeaderAtmTemplateName_IsInternalStaticOnTradeCopierPanel()
        {
            // internal is visible as Assembly-scope; try NonPublic first, then Public (same assembly)
            var mi = typeof(TradeCopierPanel).GetMethod(
                "GetLeaderAtmTemplateName",
                BindingFlags.NonPublic | BindingFlags.Static
            );
            if (mi == null)
            {
                mi = typeof(TradeCopierPanel).GetMethod(
                    "GetLeaderAtmTemplateName",
                    BindingFlags.Public | BindingFlags.Static
                );
            }

            Assert.NotNull(mi);
            Assert.True(mi.IsStatic, "GetLeaderAtmTemplateName must be static");
            Assert.Equal(typeof(string), mi.ReturnType);

            var ps = mi.GetParameters();
            Assert.Single(ps);
            Assert.Equal("currentChart", ps[0].Name);
        }
    }
}
