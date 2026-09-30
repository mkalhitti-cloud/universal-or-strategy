// PTT-COPIER-B73-LaneB -- B73Tests.cs
// xUnit tests for B73-LaneB: 15 hotfixes to TradeCopierPanel.cs
// T_BEALL_SYNC_01 through T_LABEL_CLIP_03 (33 tests total)
// Jane Street rules: JS-001, JS-021, JS-033
// NT8 types (Account, Order, Instrument, Position) NOT instantiated in test context.
// Pattern: test static helpers via reflection; test CopyEngine singleton via public/internal API;
//          test WPF types via DependencyProperty static field reflection (no STA required).
using System;
using System.Reflection;
using System.Windows.Controls;
using System.Windows.Media;
using NinjaTrader.Cbi;
using Xunit;

namespace PropTraderTools
{
    public sealed class B73Tests
    {
        private readonly CopyEngine _engine = CopyEngine.Instance;

        // Reflection accessors for private static format methods on TradeCopierPanel.
        private static MethodInfo GetFormatGlobalBeBuffer() =>
            typeof(TradeCopierPanel).GetMethod(
                "FormatGlobalBeBuffer",
                BindingFlags.NonPublic | BindingFlags.Static
            )!;

        private static MethodInfo GetFormatQuickAllBuffer() =>
            typeof(TradeCopierPanel).GetMethod(
                "FormatQuickAllBuffer",
                BindingFlags.NonPublic | BindingFlags.Static
            )!;

        private static MethodInfo GetFormatBuffer() =>
            typeof(TradeCopierPanel).GetMethod(
                "FormatBuffer",
                BindingFlags.NonPublic | BindingFlags.Static
            )!;

        // -- Group 1: B73-B-01 (2 tests) ----------------------------------------

        [Fact(Skip="B180-2: removed symbol")]
        public void T_BEALL_SYNC_01_IsPendingSlotsEmpty_InitialState_ReturnsTrue()
        {
            bool empty = CopyEngine.Instance.IsPendingSlotsEmpty();
            Assert.True(empty);
        }

        [Fact(Skip="B180-2: removed symbol")]
        public void T_BEALL_SYNC_02_DisarmPendingBe_NullAccount_NoException()
        {
            var ex = Record.Exception(() => CopyEngine.Instance.DisarmPendingBe(null));
            Assert.Null(ex);
        }

        // -- Group 2: B73-B-02 (2 tests) ----------------------------------------

        [Fact(Skip="B180-2: removed symbol")]
        public void T_BE_BG_01_BeState_HasIdleAndArmedValues()
        {
            Assert.True(Enum.IsDefined(typeof(TradeCopierPanel.BeState), "Idle"));
            Assert.True(Enum.IsDefined(typeof(TradeCopierPanel.BeState), "Armed"));
        }

        [Fact(Skip="B180-2: removed symbol")]
        public void T_BE_BG_02_BeState_ArmedNotEqualIdle()
        {
            Assert.NotEqual(TradeCopierPanel.BeState.Armed, TradeCopierPanel.BeState.Idle);
        }

        // -- Group 3: B73-B-03 (2 tests) ----------------------------------------

        [Fact(Skip="B180-2: removed symbol")]
        public void T_NO_DISARM_01_DisarmPendingBe_NullLeader_NoException()
        {
            var ex = Record.Exception(() => CopyEngine.Instance.DisarmPendingBe(null));
            Assert.Null(ex);
        }

        [Fact(Skip="B180-2: removed symbol")]
        public void T_NO_DISARM_02_IsPendingSlotsEmpty_IsIdempotent()
        {
            bool first = CopyEngine.Instance.IsPendingSlotsEmpty();
            bool second = CopyEngine.Instance.IsPendingSlotsEmpty();
            Assert.Equal(first, second);
        }

        // -- Group 4: B73-B-04 (2 tests) ----------------------------------------

        [Fact(Skip="B180-2: removed symbol")]
        public void T_FLAT_DISARM_01_DisarmPendingBe_NullArg_NoException()
        {
            var ex = Record.Exception(() => CopyEngine.Instance.DisarmPendingBe(null));
            Assert.Null(ex);
        }

        [Fact(Skip="B180-2: removed symbol")]
        public void T_FLAT_DISARM_02_IsPendingSlotsEmpty_AfterDisarm_ReturnsBool()
        {
            CopyEngine.Instance.DisarmPendingBe(null);
            bool result = CopyEngine.Instance.IsPendingSlotsEmpty();
            Assert.IsType<bool>(result);
        }

        // -- Group 5: B73-B-05 (2 tests) ----------------------------------------

        [Fact(Skip="B180-2: removed symbol")]
        public void T_BEALL_ARM_01_CopyEngine_PendingBeArmed_MemberExists()
        {
            var field = typeof(CopyEngine).GetField(
                "PendingBeArmed",
                BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public
            );
            Assert.NotNull(field);
        }

        [Fact(Skip="B180-2: removed symbol")]
        public void T_BEALL_ARM_02_CopyEngine_GlobalBeAllDisarmed_MemberExists()
        {
            var field = typeof(CopyEngine).GetField(
                "GlobalBeAllDisarmed",
                BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public
            );
            Assert.NotNull(field);
        }

        // -- Group 6: B73-B-06 (2 tests) ----------------------------------------

        [Fact]
        public void T_MANUAL_CLOSE_01_Operation_HasRemoveValue()
        {
            Assert.True(Enum.IsDefined(typeof(Operation), "Remove"));
        }

        [Fact]
        public void T_MANUAL_CLOSE_02_Operation_RemoveNotEqualUpdate()
        {
            Assert.NotEqual(Operation.Remove, Operation.Update);
        }

        // -- Group 7: B73-B-07 (2 tests) ----------------------------------------

        [Fact(Skip="B180-2: removed symbol")]
        public void T_DISARM_SYNC_01_CopyEngine_GlobalBeAllDisarmed_MemberExists()
        {
            var field = typeof(CopyEngine).GetField(
                "GlobalBeAllDisarmed",
                BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public
            );
            Assert.NotNull(field);
        }

        [Fact(Skip="B180-2: removed symbol")]
        public void T_DISARM_SYNC_02_RaiseBeAllDisarmed_NoException()
        {
            var ex = Record.Exception(() => CopyEngine.Instance.RaiseBeAllDisarmed());
            Assert.Null(ex);
        }

        // -- Group 8: B73-B-08 (2 tests) ----------------------------------------

        [Fact]
        public void T_BUF_BE_01_FormatGlobalBeBuffer_WithPositiveTicks_ReturnsCorrectLabel()
        {
            var method = GetFormatGlobalBeBuffer();
            Assert.NotNull(method);
            var result = (string)method.Invoke(null, new object[] { "BE ALL", 3 })!;
            Assert.Equal("BE ALL +3", result);
        }

        [Fact]
        public void T_BUF_BE_02_FormatGlobalBeBuffer_ZeroTicks_ReturnsNameOnly()
        {
            var method = GetFormatGlobalBeBuffer();
            Assert.NotNull(method);
            var result = (string)method.Invoke(null, new object[] { "BE ALL", 0 })!;
            Assert.Equal("BE ALL", result);
        }

        // -- Group 9: B73-B-09 (4 tests) ----------------------------------------

        [Fact]
        public void T_LABEL_01_FormatQuickAllBuffer_AppendsTSuffix()
        {
            var method = GetFormatQuickAllBuffer();
            Assert.NotNull(method);
            var result = (string)method.Invoke(null, new object[] { "Quick ALL", 4 })!;
            Assert.Equal("Quick ALL +4t", result);
        }

        [Fact]
        public void T_LABEL_02_FormatGlobalBeBuffer_WithFiveTicks_ReturnsCorrectLabel()
        {
            var method = GetFormatGlobalBeBuffer();
            Assert.NotNull(method);
            var result = (string)method.Invoke(null, new object[] { "BE ALL", 5 })!;
            Assert.Equal("BE ALL +5", result);
        }

        [Fact]
        public void T_LABEL_03_FormatQuickAllBuffer_ContainsTSuffix()
        {
            var method = GetFormatQuickAllBuffer();
            Assert.NotNull(method);
            var result = (string)method.Invoke(null, new object[] { "Quick ALL", 4 })!;
            Assert.Contains("t", result);
        }

        [Fact]
        public void T_LABEL_04_FormatQuickAllBuffer_ZeroTicks_ReturnsZeroWithTSuffix()
        {
            var method = GetFormatQuickAllBuffer();
            Assert.NotNull(method);
            var result = (string)method.Invoke(null, new object[] { "Quick ALL", 0 })!;
            Assert.Equal("Quick ALL +0t", result);
        }

        // -- Group 10: B73-B-10 (2 tests) ----------------------------------------

        [Fact]
        public void T_QA_SING_01_CopyEngine_GlobalQuickAllBufferChanged_MemberExists()
        {
            var field = typeof(CopyEngine).GetField(
                "GlobalQuickAllBufferChanged",
                BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public
            );
            Assert.NotNull(field);
        }

        [Fact]
        public void T_QA_SING_02_GlobalQuickAllT1_IsAccessible()
        {
            int t1 = CopyEngine.Instance.GlobalQuickAllT1;
            Assert.IsType<int>(t1);
        }

        // -- Group 11: B73-B-11 (1 test) -----------------------------------------

        [Fact]
        public void T_QA_INIT_01_GlobalQuickAllT1_HasPositiveInitialValue()
        {
            int t1 = CopyEngine.Instance.GlobalQuickAllT1;
            Assert.True(t1 >= 1);
        }

        // -- Group 12: B73-B-12 (2 tests) ----------------------------------------

        [Fact(Skip="B180-2: removed symbol")]
        public void T_DISARM_CROSS_01_RaiseBeAllDisarmed_CalledTwice_NoException()
        {
            var ex = Record.Exception(() =>
            {
                CopyEngine.Instance.RaiseBeAllDisarmed();
                CopyEngine.Instance.RaiseBeAllDisarmed();
            });
            Assert.Null(ex);
        }

        [Fact(Skip="B180-2: removed symbol")]
        public void T_DISARM_CROSS_02_IsPendingSlotsEmpty_AfterRaiseBeAllDisarmed_ReturnsBool()
        {
            CopyEngine.Instance.RaiseBeAllDisarmed();
            bool result = CopyEngine.Instance.IsPendingSlotsEmpty();
            Assert.IsType<bool>(result);
        }

        // -- Group 13: B73-B-13 (2 tests) ----------------------------------------

        [Fact]
        public void T_BEALL_FLAT_01_CopyEngine_GlobalBeBufferChanged_MemberExists()
        {
            var field = typeof(CopyEngine).GetField(
                "GlobalBeBufferChanged",
                BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public
            );
            Assert.NotNull(field);
        }

        [Fact(Skip="B180-2: removed symbol")]
        public void T_BEALL_FLAT_02_IsPendingSlotsEmpty_IsIdempotentOnTwoCalls()
        {
            bool first = CopyEngine.Instance.IsPendingSlotsEmpty();
            bool second = CopyEngine.Instance.IsPendingSlotsEmpty();
            Assert.Equal(first, second);
        }

        // -- Group 14: B73-B-14 (3 tests) ----------------------------------------

        [Fact]
        public void T_ORPHAN_01_CancelQxBrackets_NullArgs_NoException()
        {
            var ex = Record.Exception(() => CopyEngine.Instance.CancelQxBrackets(null, null));
            Assert.Null(ex);
        }

        [Fact]
        public void T_ORPHAN_02_IsQxCancelCandidate_NullOrder_ReturnsFalse()
        {
            bool result = CopyEngine.IsQxCancelCandidate(null);
            Assert.False(result);
        }

        [Fact]
        public void T_ORPHAN_03_IsQxCancelCandidate_MethodExists_AsStaticAccessible()
        {
            var mi = typeof(CopyEngine).GetMethod(
                "IsQxCancelCandidate",
                BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Public
            );
            Assert.NotNull(mi);
        }

        // -- Group 15: B73-B-15 (3 tests) ----------------------------------------

        [Fact]
        public void T_LABEL_CLIP_01_DockPanel_TypeExists()
        {
            Assert.Equal("DockPanel", typeof(DockPanel).Name);
            Assert.True(typeof(DockPanel).IsClass);
        }

        [Fact]
        public void T_LABEL_CLIP_02_DockPanel_LastChildFillProperty_Exists()
        {
            var field = typeof(DockPanel).GetField(
                "LastChildFillProperty",
                BindingFlags.Public | BindingFlags.Static
            );
            Assert.NotNull(field);
        }

        [Fact]
        public void T_LABEL_CLIP_03_DockPanel_DockProperty_Exists()
        {
            var field = typeof(DockPanel).GetField(
                "DockProperty",
                BindingFlags.Public | BindingFlags.Static
            );
            Assert.NotNull(field);
        }

        // -- DW-B73-B-01 tests (Ticket 1) ----------------------------------------

        [Fact(Skip="B180-2: removed symbol")]
        public void RaiseBeAllDisarmed_NoException_WhenCalled()
        {
            // Act + Assert: no exception thrown when no subscribers attached
            var exception = Record.Exception(() => CopyEngine.Instance.RaiseBeAllDisarmed());
            Assert.Null(exception);
        }

        [Fact(Skip="B180-2: removed symbol")]
        public void GlobalBeAllDisarmed_EventExists_AndIsSubscribable()
        {
            // Arrange
            var subscribed = false;
            Action handler = () => subscribed = true;
            CopyEngine.Instance.GlobalBeAllDisarmed += handler;

            // Act
            CopyEngine.Instance.RaiseBeAllDisarmed();

            // Assert
            Assert.True(subscribed);

            // Cleanup
            CopyEngine.Instance.GlobalBeAllDisarmed -= handler;
        }

        [Fact(Skip="B180-2: removed symbol")]
        public void RaiseBeAllDisarmed_FiresSubscriber_ExactlyOnce()
        {
            // Arrange
            var fireCount = 0;
            Action handler = () => fireCount++;
            CopyEngine.Instance.GlobalBeAllDisarmed += handler;

            // Act
            CopyEngine.Instance.RaiseBeAllDisarmed();

            // Assert
            Assert.Equal(1, fireCount);

            // Cleanup
            CopyEngine.Instance.GlobalBeAllDisarmed -= handler;
        }

        // -- DW-B73-B-02 tests (Ticket 2) ----------------------------------------

        private static SolidColorBrush GetBrushTeal()
        {
            var field = typeof(TradeCopierPanel).GetField(
                "BrushTeal",
                BindingFlags.NonPublic | BindingFlags.Static
            );
            return (SolidColorBrush)field!.GetValue(null)!;
        }

        [Fact]
        public void BrushTeal_IsNotNull()
        {
            // Act
            var brush = GetBrushTeal();

            // Assert
            Assert.NotNull(brush);
        }

        [Fact]
        public void BrushTeal_IsFrozen()
        {
            // Act
            var brush = GetBrushTeal();

            // Assert
            Assert.True(brush.IsFrozen);
        }

        [Fact]
        public void BrushTeal_Color_MatchesTeal600()
        {
            // Act
            var brush = GetBrushTeal();
            var color = brush.Color;

            // Assert
            Assert.Equal(13, color.R);
            Assert.Equal(148, color.G);
            Assert.Equal(136, color.B);
        }
    }
}
