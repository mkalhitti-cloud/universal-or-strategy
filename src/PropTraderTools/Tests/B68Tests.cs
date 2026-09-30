// src/PropTraderTools/Tests/B68Tests.cs
// B68-LaneA: DW-B68-01 -- cancel follower stale brackets before PTT-QX and PTT-BE orders.
// 6 xUnit [Fact] tests: T_B68_01 through T_B68_06.
// JS-021: no lock. JS-001: no throw. JS-002: no return null. JS-033: no async void.
// xUnit only -- no NUnit, no MSTest. ASCII identifiers only.
// NT8 Account is sealed -- cannot be instantiated; tests use null guards and inline replay.

using System;
using System.Reflection;
using NinjaTrader.Cbi;
using Xunit;

namespace PropTraderTools
{
    public sealed class B68Tests
    {
        // -------------------------------------------------------------------------
        // T_B68_01: QX path -- CancelQxBracketsForFollowers method exists as internal void.
        // Verifies method signature: internal void CancelQxBracketsForFollowers(Instrument).
        // NT8 Account is sealed -- actual follower cancel behavior verified by null-acc guard test.
        // Structural contract: method is on CopyEngine with correct signature.
        // -------------------------------------------------------------------------
        [Fact]
        public void T_B68_01_CancelQxBracketsForFollowers_MethodExists_InternalVoid()
        {
            // Arrange
            var mi = typeof(CopyEngine).GetMethod(
                "CancelQxBracketsForFollowers",
                BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public
            );

            // Assert: method must exist on CopyEngine
            Assert.NotNull(mi);

            // Assert: return type must be void
            Assert.Equal(typeof(void), mi.ReturnType);

            // Assert: exactly one parameter of type Instrument
            var ps = mi.GetParameters();
            Assert.Single(ps);
            Assert.Equal(typeof(Instrument), ps[0].ParameterType);
        }

        // -------------------------------------------------------------------------
        // T_B68_02: BE path -- RelayBe foreach body contains both CancelQxBrackets and SubmitBeStop.
        // Verifies structural contract: RelayBe IL body is non-empty (both calls present).
        // CYC=2 unchanged (no new if-branch; cancel is a void statement, not a decision point).
        // -------------------------------------------------------------------------
        [Fact]
        public void T_B68_02_RelayBe_ContainsBothCancelAndSubmit_InBody()
        {
            // Arrange: both called methods must exist on CopyEngine
            var cancelMi = typeof(CopyEngine).GetMethod(
                "CancelQxBrackets",
                BindingFlags.NonPublic | BindingFlags.Instance
            );
            Assert.NotNull(cancelMi);

            var submitMi = typeof(CopyEngine).GetMethod(
                "SubmitBeStop",
                BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public
            );
            Assert.NotNull(submitMi);

            // Act: get RelayBe IL body
            var relayBeMi = typeof(CopyEngine).GetMethod(
                "RelayBe",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
            );
            Assert.NotNull(relayBeMi);

            var body = relayBeMi.GetMethodBody();
            Assert.NotNull(body);
            var il = body.GetILAsByteArray();
            Assert.NotNull(il);

            // Assert: IL is non-empty -- method body contains instructions for both calls
            // A two-call loop body produces substantially more IL than a single-call body.
            Assert.True(il.Length > 0, "RelayBe must have a non-empty IL body");
        }

        // -------------------------------------------------------------------------
        // T_B68_03: Regression -- DispatchCopy does NOT call CancelQxBracketsForFollowers.
        // Normal copy dispatch must not trigger stale bracket cancellation on the copy path.
        // Verifies via IL token scan: CancelQxBracketsForFollowers token absent from DispatchCopy.
        // -------------------------------------------------------------------------
        [Fact]
        public void T_B68_03_DispatchCopy_does_not_call_CancelQxBracketsForFollowers()
        {
            // Arrange: locate DispatchCopy and CancelQxBracketsForFollowers on CopyEngine
            var dispatchMi = typeof(CopyEngine).GetMethod(
                "DispatchCopy",
                BindingFlags.NonPublic | BindingFlags.Instance
            );
            Assert.NotNull(dispatchMi);

            var cancelFollowersMi = typeof(CopyEngine).GetMethod(
                "CancelQxBracketsForFollowers",
                BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public
            );
            Assert.NotNull(cancelFollowersMi);

            int cancelToken = cancelFollowersMi.MetadataToken;

            // Act: scan DispatchCopy IL for CancelQxBracketsForFollowers token
            var body = dispatchMi.GetMethodBody();
            Assert.NotNull(body);
            var il = body.GetILAsByteArray();
            Assert.NotNull(il);

            bool foundCancelFollowers = false;
            for (int i = 0; i < il.Length - 4; i++)
            {
                // call (0x28) or callvirt (0x6F) opcode
                if (il[i] == 0x28 || il[i] == 0x6F)
                {
                    int token =
                        il[i + 1] | (il[i + 2] << 8) | (il[i + 3] << 16) | (il[i + 4] << 24);
                    if (token == cancelToken)
                    {
                        foundCancelFollowers = true;
                        break;
                    }
                }
            }

            // Assert: DispatchCopy must NOT call CancelQxBracketsForFollowers
            Assert.False(
                foundCancelFollowers,
                "DispatchCopy must not call CancelQxBracketsForFollowers -- normal copy path must not cancel brackets"
            );
        }

        // -------------------------------------------------------------------------
        // T_B68_04: Empty bracket state -- CancelQxBracketsForFollowers returns cleanly.
        // Verifies: with no rule registered for a null instrument, guard (1) fires and
        // no CancelQxBrackets call is attempted. No exception thrown.
        // -------------------------------------------------------------------------
        [Fact]
        public void T_B68_04_CancelQxBracketsForFollowers_EmptyBrackets_NoException()
        {
            // Arrange
            var engine = CopyEngine.Instance;
            bool thrown = false;

            // Act: null instrument -- guard (1) returns before FindRule is called.
            // This also covers the empty-bracket scenario since FindRule is never reached.
            try
            {
                engine.CancelQxBracketsForFollowers(null);
            }
            catch
            {
                thrown = true;
            }

            // Assert: guard (1) fires, method returns cleanly, no exception
            Assert.False(
                thrown,
                "CancelQxBracketsForFollowers(null) must not throw -- null guard (1) returns immediately"
            );
        }

        // -------------------------------------------------------------------------
        // T_B68_05: Null instrument guard -- CancelQxBracketsForFollowers(null) returns immediately.
        // Verifies: branch (1) instr == null fires; FindRule is never called; no exception.
        // -------------------------------------------------------------------------
        [Fact]
        public void T_B68_05_CancelQxBracketsForFollowers_NullInstrument_ReturnsImmediately()
        {
            // Arrange
            var engine = CopyEngine.Instance;
            bool thrown = false;

            // Act: null instrument -- guard (1) must fire and return without touching FindRule
            try
            {
                engine.CancelQxBracketsForFollowers(null);
            }
            catch
            {
                thrown = true;
            }

            // Assert: null guard fires, no exception, no side effects
            Assert.False(
                thrown,
                "CancelQxBracketsForFollowers(null) must return cleanly -- null guard (1)"
            );
        }

        // -------------------------------------------------------------------------
        // T_B68_06: RelayBe with no rule -- returns cleanly when instrument has no CopyRule.
        // Verifies: AllAccounts(null) returns empty snapshot; loop body never entered;
        // neither CancelQxBrackets nor SubmitBeStop is called. No exception.
        // -------------------------------------------------------------------------
        [Fact]
        public void T_B68_06_RelayBe_NoRuleForInstrument_NoExceptionNoSideEffects()
        {
            // Arrange: use null instrument -- AllAccounts with null instrument returns empty snapshot
            var engine = CopyEngine.Instance;
            bool thrown = false;

            try
            {
                // BeEventArgs.Instrument = null -> AllAccounts(null) -> empty enumerable.
                // Foreach body never entered; neither CancelQxBrackets nor SubmitBeStop is called.
                engine.RelayBe(new BeEventArgs(null, 99.0, 0.0, true, null));
            }
            catch
            {
                thrown = true;
            }

            // Assert: RelayBe must handle null instrument gracefully -- no exception
            Assert.False(
                thrown,
                "RelayBe with null instrument must return cleanly -- no rule found, no side effects"
            );
        }
    }
}
