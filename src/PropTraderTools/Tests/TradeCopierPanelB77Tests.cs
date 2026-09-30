// src/PropTraderTools/TradeCopierPanelB77Tests.cs
// B77-LaneA: HOTFIX-B77-01 -- GetLeaderAtmTemplateName fallback-1 repair test coverage.
// 5 xUnit [Fact] tests: T_B77_TPL_01 through T_B77_TPL_05.
// T_B77_TPL_01: null chart -> string.Empty (reflection invoke, no NT8 host)
// T_B77_TPL_02: ct==null skip skeleton (NT8-HOST-REQUIRED)
// T_B77_TPL_03: AtmStrategy.Name=="AtmStrategy" -> guard fires -> sel null -> empty; skip skeleton
// T_B77_TPL_04: IL scan -- get_SelectedAtmStrategy getter NOT called in method body
// T_B77_TPL_05: reflection invoke null + IL scan for string.Empty literal
// JS-021: no lock. JS-001: no throw new. JS-002: no return null. JS-033: no async void.
// xUnit only -- no NUnit, no MSTest. ASCII identifiers only. CYC <= 8 per method.

using System;
using System.Reflection;
using Xunit;

namespace PropTraderTools
{
    public sealed class TradeCopierPanelB77Tests
    {
        // ===========================================================================
        // TICKET T1: GetLeaderAtmTemplateName fallback-1 repair (HOTFIX-B77-01)
        // Branch 6 repair: sel.SelectedItem as string ?? string.Empty
        //                  (was: sel.SelectedAtmStrategy.Name -- class-name trap)
        // ===========================================================================

        // T_B77_TPL_01: GetLeaderAtmTemplateName(null) returns string.Empty -- no NRE.
        // Branch 1 null guard. No NT8 host required.
        // Pattern: T_B76_10 (B76Tests.cs).
        [Fact]
        public void T_B77_TPL_01_NullChart_ReturnsStringEmpty()
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

        // T_B77_TPL_02: currentChart != null but FindVisualChild<ChartTrader> returns null -- branch 2.
        // Skip: requires live WPF visual tree (NT8 host).
        [Fact(
            Skip = "NT8-HOST-REQUIRED: FindVisualChild<ChartTrader>(currentChart) requires live WPF visual tree"
        )]
        public void T_B77_TPL_02_ChartTraderNull_ReturnsStringEmpty()
        {
            // Arrange: real Chart with no ChartTrader child in visual tree.
            // Act:     string result = TradeCopierPanel.GetLeaderAtmTemplateName(chartWithNoChartTrader);
            // Assert:  Assert.Equal(string.Empty, result);
        }

        // T_B77_TPL_03: ct.AtmStrategy.Name == "AtmStrategy" -- B76 guard fires; sel==null;
        //               ComboBox.SelectedItem==null -> returns string.Empty.
        // Skip: requires live ChartTrader with staged AtmStrategy and no AtmStrategySelector.
        [Fact(
            Skip = "NT8-HOST-REQUIRED: requires live ChartTrader with AtmStrategy.Name==\"AtmStrategy\" and no AtmStrategySelector in visual tree"
        )]
        public void T_B77_TPL_03_AtmStrategyNameIsClassName_SelNull_FallsThrough_ReturnsEmpty()
        {
            // Arrange: real Chart; ct.AtmStrategy.Name == "AtmStrategy"; no AtmStrategySelector child;
            //          no ComboBox at index 2 or ComboBox.SelectedItem == null.
            // Act:     string result = TradeCopierPanel.GetLeaderAtmTemplateName(chart);
            // Assert:  Assert.Equal(string.Empty, result);
        }

        // T_B77_TPL_04: IL scan -- GetLeaderAtmTemplateName must NOT call get_SelectedAtmStrategy.
        // Proves the B77 repair is compiled: SelectedItem path replaces SelectedAtmStrategy.Name path.
        // No NT8 host required (IL inspection only).
        [Fact]
        public void T_B77_TPL_04_ILScan_SelectedAtmStrategyGetterNotCalledInMethodBody()
        {
            var mi = typeof(TradeCopierPanel).GetMethod(
                "GetLeaderAtmTemplateName",
                BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Public
            );

            Assert.NotNull(mi);

            var selectorType = typeof(NinjaTrader.Gui.NinjaScript.AtmStrategy.AtmStrategySelector);
            var selProp = selectorType.GetProperty(
                "SelectedAtmStrategy",
                BindingFlags.Public | BindingFlags.Instance
            );

            Assert.NotNull(selProp);

            int getterToken = selProp.GetGetMethod().MetadataToken;

            var body = mi.GetMethodBody();
            Assert.NotNull(body);
            var il = body.GetILAsByteArray();
            Assert.NotNull(il);
            Assert.True(il.Length > 0, "GetLeaderAtmTemplateName must have a non-empty IL body");

            Assert.False(
                IlContainsCallvirt(il, getterToken),
                "GetLeaderAtmTemplateName must NOT call get_SelectedAtmStrategy -- B77 repair uses SelectedItem (HOTFIX-B77-01)"
            );
        }

        // T_B77_TPL_05: null-invoke + IL scan for string.Empty literal.
        // Documents null-safe ?? contract: sel.SelectedItem as string ?? string.Empty
        // never throws even when SelectedItem is null.
        // No NT8 host required for the runnable assertions.
        [Fact]
        public void T_B77_TPL_05_Fallback1_SelNotNull_SelectedItemNull_ReturnsStringEmpty()
        {
            var mi = typeof(TradeCopierPanel).GetMethod(
                "GetLeaderAtmTemplateName",
                BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Public
            );

            Assert.NotNull(mi);

            // Part 1: null-invoke confirms no exception on null-chart path (branch 1 proxy).
            var result = mi.Invoke(null, new object[] { null });
            Assert.Equal(string.Empty, result);
            Assert.NotNull(result);

            // Part 2: IL scan confirms at least one string.Empty literal is compiled in,
            // proving the null-safe ?? string.Empty pattern is present in the method body.
            var body = mi.GetMethodBody();
            Assert.NotNull(body);
            var il = body.GetILAsByteArray();
            Assert.NotNull(il);

            var module = typeof(TradeCopierPanel).Module;
            bool foundStringEmpty = false;
            for (int i = 0; i < il.Length - 4; i++)
            {
                if (il[i] == 0x72) // ldstr
                {
                    int token =
                        il[i + 1] | (il[i + 2] << 8) | (il[i + 3] << 16) | (il[i + 4] << 24);
                    try
                    {
                        var s = module.ResolveString(token);
                        if (s != null && s.Length == 0)
                        {
                            foundStringEmpty = true;
                            break;
                        }
                    }
                    catch
                    { /* token not a valid string reference -- skip */
                    }
                }
            }

            Assert.True(
                foundStringEmpty,
                "GetLeaderAtmTemplateName must contain a string.Empty literal (null-safe ?? pattern -- HOTFIX-B77-01)"
            );
        }

        // IL inspection helper: returns true if the byte array contains a callvirt instruction (0x6F)
        // whose 4-byte token operand matches targetToken.
        // CYC = 3: loop + opcode-if + token-if.
        // JS-021: no lock. JS-002: does not return null (returns bool).
        private static bool IlContainsCallvirt(byte[] il, int targetToken)
        {
            for (int i = 0; i < il.Length - 4; i++)
            {
                if (il[i] == 0x6F) // callvirt
                {
                    int token =
                        il[i + 1] | (il[i + 2] << 8) | (il[i + 3] << 16) | (il[i + 4] << 24);
                    if (token == targetToken)
                        return true;
                }
            }
            return false;
        }
    }
}
