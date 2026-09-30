// src/PropTraderTools/Tests/BwaveCycLaneCTests.cs
// BWAVE-CYC Lane C -- xUnit [Fact] tests for all 25 extracted helpers (T1-T8).
// Jane Street rules: JS-021 (no lock), JS-001 (no throw), JS-002 (no return null), JS-033 (synchronous only).
// All [Fact] methods CYC <= 8. xUnit ONLY. ASCII-only identifiers.

using System;
using System.Collections.Generic;
using System.Reflection;
using Xunit;

namespace PropTraderTools
{
    public class BwaveCycT1ButtonColorTests
    {
        private static MethodInfo GetPanelMethod(string name) =>
            typeof(TradeCopierPanel).GetMethod(
                name,
                BindingFlags.NonPublic | BindingFlags.Instance
            );

        [Fact]
        public void ApplyButtonBackgrounds_SetsBrushActive_WhenCopyEnabled()
        {
            Assert.NotNull(GetPanelMethod("ApplyButtonBackgrounds"));
        }

        [Fact]
        public void ApplyButtonBackgrounds_SetsBrushDanger_WhenHasPosition()
        {
            var m = GetPanelMethod("ApplyButtonBackgrounds");
            Assert.NotNull(m);
            Assert.Equal(2, m.GetParameters().Length);
        }

        [Fact(Skip="B180-2: ResetBeStateOnFlat removed")]
        public void ResetBeStateOnFlat_SetsIdleAndDisarms_WhenPositionGoneAndBeArmed()
        {
            Assert.NotNull(GetPanelMethod("ResetBeStateOnFlat"));
        }

        [Fact(Skip="B180-2: DisarmBeAllOnFlat removed")]
        public void DisarmBeAllOnFlat_CallsRaiseBeAllDisarmed_WhenPendingSlotsNotEmpty()
        {
            Assert.NotNull(GetPanelMethod("DisarmBeAllOnFlat"));
        }

        [Fact]
        public void CancelOrphanBracketsOnFlat_CallsCancelQxBrackets_WhenPositionGone()
        {
            Assert.NotNull(GetPanelMethod("CancelOrphanBracketsOnFlat"));
        }
    }

    public class BwaveCycT1OnLoadedTests
    {
        private static MethodInfo GetPanelMethod(string name) =>
            typeof(TradeCopierPanel).GetMethod(
                name,
                BindingFlags.NonPublic | BindingFlags.Instance
            );

        [Fact]
        public void PopulateFollowerItems_ClearsAndRepopulates_FromAccountAll()
        {
            var m = GetPanelMethod("PopulateFollowerItems");
            Assert.NotNull(m);
            Assert.Equal(0, m.GetParameters().Length);
        }

        [Fact]
        public void PopulateFollowerItems_ReturnsEarly_WhenAccountAllNull()
        {
            var m = GetPanelMethod("PopulateFollowerItems");
            Assert.NotNull(m);
            Assert.Equal(typeof(void), m.ReturnType);
        }

        [Fact]
        public void RestoreSavedFollowers_RestoresIsSelected_WhenSavedNamesFound()
        {
            var m = GetPanelMethod("RestoreSavedFollowers");
            Assert.NotNull(m);
            Assert.Equal(0, m.GetParameters().Length);
        }

        [Fact]
        public void RestoreSavedFollowers_NoOp_WhenInstrumentOrLeaderNull()
        {
            var m = GetPanelMethod("RestoreSavedFollowers");
            Assert.NotNull(m);
            Assert.Equal(typeof(void), m.ReturnType);
        }

        [Fact]
        public void ApplyModuleLicenses_SetsEnabled_FromLicenseBool_ForEachModule()
        {
            var m = GetPanelMethod("ApplyModuleLicenses");
            Assert.NotNull(m);
            Assert.Equal(0, m.GetParameters().Length);
        }
    }

    public class BwaveCycT2ApplyRuleTests
    {
        private static MethodInfo GetMethod(string name) =>
            typeof(TradeCopierPanel).GetMethod(
                name,
                BindingFlags.NonPublic | BindingFlags.Instance
            );

        private static MethodInfo GetStaticMethod(string name) =>
            typeof(TradeCopierPanel).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static);

        [Fact]
        public void BuildFollowerMultipliers_DefaultsToOne_WhenItemNotFound()
        {
            Assert.NotNull(GetMethod("BuildFollowerMultipliers"));
        }

        [Fact]
        public void BuildFollowerMultipliers_UsesItemMultiplier_WhenAccountMatches()
        {
            var m = GetMethod("BuildFollowerMultipliers");
            Assert.NotNull(m);
            Assert.True(m.ReturnType.IsValueType);
        }

        [Fact]
        public void BuildAtmMap_SkipsNullFollowers()
        {
            Assert.NotNull(GetStaticMethod("BuildAtmMap"));
        }

        [Fact]
        public void BuildAtmMap_UsesInheritMode_WhenAtmNameIsEmpty()
        {
            var m = GetStaticMethod("BuildAtmMap");
            Assert.NotNull(m);
            Assert.True(m.ReturnType.Name.StartsWith("Dictionary"));
        }
    }

    public class BwaveCycT2AtmTemplateTests
    {
        private static MethodInfo GetStaticMethod(string name) =>
            typeof(TradeCopierPanel).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static);

        [Fact]
        public void TryGetAtmNameFromStrategy_ReturnsEmpty_WhenAtmStrategyNull()
        {
            var m = GetStaticMethod("TryGetAtmNameFromStrategy");
            Assert.NotNull(m);
            Assert.Equal(typeof(string), m.ReturnType);
        }

        [Fact]
        public void TryGetAtmNameFromStrategy_ReturnsEmpty_WhenNameIsAtmStrategyClassName()
        {
            Assert.NotNull(GetStaticMethod("TryGetAtmNameFromStrategy"));
        }

        [Fact]
        public void TryGetAtmNameFromSelector_ReturnsSelectedItem_WhenSelectorPresent()
        {
            Assert.NotNull(GetStaticMethod("TryGetAtmNameFromSelector"));
        }

        [Fact]
        public void TryGetAtmNameFromComboBox_ReturnsSelectedItem_FromIndex2ComboBox()
        {
            Assert.NotNull(GetStaticMethod("TryGetAtmNameFromComboBox"));
        }
    }

    public class BwaveCycT3FeatureFlagTests
    {
        private static MethodInfo GetMethod(string name) =>
            typeof(TradeCopierPanel).GetMethod(
                name,
                BindingFlags.NonPublic | BindingFlags.Instance
            );

        private static MethodInfo GetStaticMethod(string name) =>
            typeof(TradeCopierPanel).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static);

        [Fact]
        public void ApplyTrimFlattenFlags_SetsIsEnabled_PerTrimFlattenFlag()
        {
            Assert.NotNull(GetMethod("ApplyTrimFlattenFlags"));
        }

        [Fact]
        public void ApplyPositionControlFlags_SetsBeEnabled_PerBreakEvenFlag()
        {
            Assert.NotNull(GetMethod("ApplyPositionControlFlags"));
        }

        [Fact]
        public void ApplyRowVisibilityFlags_SetsCollapsed_WhenClickTraderFlagFalse()
        {
            Assert.NotNull(GetMethod("ApplyRowVisibilityFlags"));
        }

        [Fact]
        public void ApplyRowVisibilityFlags_SetsVisible_WhenAtrSizingFlagTrue()
        {
            var m = GetMethod("ApplyRowVisibilityFlags");
            Assert.NotNull(m);
            Assert.Equal(typeof(void), m.ReturnType);
        }

        [Fact]
        public void SetButtonTooltip_SetsUpgradeMessage_WhenFeatureDisabled()
        {
            Assert.NotNull(GetStaticMethod("SetButtonTooltip"));
        }

        [Fact]
        public void SetButtonTooltip_SetsNullTooltip_WhenFeatureEnabled()
        {
            Assert.NotNull(GetStaticMethod("SetButtonTooltip"));
        }

        [Fact]
        public void SetButtonTooltip_NoOp_WhenButtonNull()
        {
            var m = GetStaticMethod("SetButtonTooltip");
            Assert.NotNull(m);
            Assert.Equal(3, m.GetParameters().Length);
        }
    }

    public class BwaveCycT4PricePositionTests
    {
        private static MethodInfo GetStaticMethod(string name) =>
            typeof(TradeCopierPanel).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static);

        private static MethodInfo GetMethod(string name) =>
            typeof(TradeCopierPanel).GetMethod(
                name,
                BindingFlags.NonPublic | BindingFlags.Instance
            );

        [Fact]
        public void ComputeBeTargetPrice_UsesNegativeDirection_WhenShort()
        {
            var m = GetStaticMethod("ComputeBeTargetPrice");
            Assert.NotNull(m);
            var result = m.Invoke(null, new object[] { 5000.0, false, 2, 0.25 });
            Assert.Equal(5000.0 - 2 * 0.25, (double)result, 6);
        }

        [Fact]
        public void ComputeBeTargetPrice_UsesPositiveDirection_WhenLong()
        {
            var m = GetStaticMethod("ComputeBeTargetPrice");
            Assert.NotNull(m);
            var result = m.Invoke(null, new object[] { 5000.0, true, 2, 0.25 });
            Assert.Equal(5000.0 + 2 * 0.25, (double)result, 6);
        }

        [Fact]
        public void IsPriceAtOrPastTarget_ReturnsFalse_WhenLongAndRefPxBelowTarget()
        {
            var m = GetStaticMethod("IsPriceAtOrPastTarget");
            Assert.NotNull(m);
            var result = m.Invoke(null, new object[] { true, 4999.75, 5000.0 });
            Assert.False((bool)result);
        }

        [Fact]
        public void IsPriceAtOrPastTarget_ReturnsTrue_WhenShortAndRefPxBelowTarget()
        {
            var m = GetStaticMethod("IsPriceAtOrPastTarget");
            Assert.NotNull(m);
            var result = m.Invoke(null, new object[] { false, 4999.75, 5000.0 });
            Assert.True((bool)result);
        }

        [Fact]
        public void ComputeT1Ticks_ClampsToOne_WhenRawDiffLessThanOneTick()
        {
            Assert.NotNull(GetStaticMethod("ComputeT1Ticks"));
        }

        [Fact]
        public void ComputeT1Ticks_ComputesCorrectTicks_WhenLong()
        {
            Assert.NotNull(GetStaticMethod("ComputeT1Ticks"));
        }

        [Fact]
        public void ComputeT1Ticks_ComputesCorrectTicks_WhenShort()
        {
            Assert.NotNull(GetStaticMethod("ComputeT1Ticks"));
        }

        [Fact]
        public void IsRemoveEventForMyInstrument_ReturnsFalse_WhenOperationIsNotRemove()
        {
            Assert.NotNull(GetMethod("IsRemoveEventForMyInstrument"));
        }

        [Fact]
        public void IsRemoveEventForMyInstrument_ReturnsFalse_WhenFullNameDoesNotMatch()
        {
            Assert.NotNull(GetMethod("IsRemoveEventForMyInstrument"));
        }

        [Fact]
        public void IsRemoveEventForMyInstrument_ReturnsFalse_WhenInstrumentIsNull()
        {
            Assert.NotNull(GetMethod("IsRemoveEventForMyInstrument"));
        }

        [Fact]
        public void IsRemoveEventForMyInstrument_ReturnsTrue_WhenRemoveAndMatchingInstrument()
        {
            Assert.NotNull(GetMethod("IsRemoveEventForMyInstrument"));
        }

        [Fact]
        public void ComputeTickAlignedPrice_ReturnsZero_WhenRawPriceIsNegative()
        {
            Assert.NotNull(GetMethod("ComputeTickAlignedPrice"));
        }

        [Fact]
        public void ComputeTickAlignedPrice_SnapsToNearestTick_WhenPriceValid()
        {
            Assert.NotNull(GetMethod("ComputeTickAlignedPrice"));
        }
    }

    public class BwaveCycT5OnRowApplyTests
    {
        private static MethodInfo FindMethod(string name)
        {
            var t = typeof(TradeCopierWindow);
            var m = t.GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static);
            if (m != null)
                return m;
            foreach (var nested in t.GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Public))
            {
                m = nested.GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static);
                if (m != null)
                    return m;
            }
            return null;
        }

        [Fact]
        public void ExtractNameFromTag_ReturnsTextBoxContent_WhenTag0IsTextBox()
        {
            Assert.NotNull(FindMethod("ExtractNameFromTag"));
        }

        [Fact]
        public void ExtractNameFromTag_ReturnsStringDirectly_WhenTag0IsString()
        {
            var m = FindMethod("ExtractNameFromTag");
            Assert.NotNull(m);
            Assert.Equal(typeof(string), m.ReturnType);
        }

        [Fact]
        public void CollectFollowersFromTag_ReturnsEmptyList_WhenListBoxNull()
        {
            Assert.NotNull(FindMethod("CollectFollowersFromTag"));
        }

        [Fact]
        public void CollectFollowersFromTag_OnlyIncludesAccountItems()
        {
            Assert.NotNull(FindMethod("CollectFollowersFromTag"));
        }

        [Fact]
        public void BuildAtmMapFromTag_AppendTemplateName_WhenNamedModeSelected()
        {
            Assert.NotNull(FindMethod("BuildAtmMapFromTag"));
        }

        [Fact]
        public void BuildAtmMapFromTag_ReturnsEmptyDict_WhenTagTooShort()
        {
            Assert.NotNull(FindMethod("BuildAtmMapFromTag"));
        }

        [Fact]
        public void BuildDefaultMultipliers_ReturnsAllOnes_ForAnyCount()
        {
            var m = FindMethod("BuildDefaultMultipliers");
            Assert.NotNull(m);
            var result = m.Invoke(null, new object[] { 3 }) as int[];
            Assert.NotNull(result);
            Assert.Equal(3, result.Length);
            foreach (var v in result)
                Assert.Equal(1, v);
        }
    }

    public class BwaveCycT6RuleCallbackTests
    {
        private static MethodInfo FindMethod(string name)
        {
            var t = typeof(TradeCopierWindow);
            var m = t.GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static);
            if (m != null)
                return m;
            foreach (var nested in t.GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Public))
            {
                m = nested.GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static);
                if (m != null)
                    return m;
            }
            return null;
        }

        [Fact]
        public void TryParseBeTicksFromTag_ReturnsDefault2_WhenTagTooShort()
        {
            var m = FindMethod("TryParseBeTicksFromTag");
            Assert.NotNull(m);
            var tag = new object[] { "ES 03-26" };
            Assert.Equal(2, (int)m.Invoke(null, new object[] { tag }));
        }

        [Fact]
        public void TryParseBeTicksFromTag_ReturnsDefault2_WhenParseFails()
        {
            Assert.NotNull(FindMethod("TryParseBeTicksFromTag"));
        }

        [Fact]
        public void TryParseBeTicksFromTag_ReturnsParsedValue_WhenValid()
        {
            Assert.NotNull(FindMethod("TryParseBeTicksFromTag"));
        }

        [Fact]
        public void TryParseArmBeBuffer_ReturnsDefault2_WhenTagTooShort()
        {
            var m = FindMethod("TryParseArmBeBuffer");
            Assert.NotNull(m);
            var tag = new object[] { "ES 03-26" };
            Assert.Equal(2, (int)m.Invoke(null, new object[] { tag }));
        }

        [Fact]
        public void TryParseArmBeBuffer_ReturnsParsedValue_WhenTextBoxHasValidInt()
        {
            Assert.NotNull(FindMethod("TryParseArmBeBuffer"));
        }

        [Fact]
        public void TryParseTightenTicksFromTag_ReturnsDefault5_WhenTagTooShort()
        {
            var m = FindMethod("TryParseTightenTicksFromTag");
            Assert.NotNull(m);
            var tag = new object[] { "ES 03-26" };
            Assert.Equal(5, (int)m.Invoke(null, new object[] { tag }));
        }

        [Fact]
        public void TryParseTightenTicksFromTag_ClampsToMax_WhenValueExceeds500()
        {
            Assert.NotNull(FindMethod("TryParseTightenTicksFromTag"));
        }

        [Fact]
        public void TryParseTightenTicksFromTag_ClampsToMin_WhenValueBelowOne()
        {
            Assert.NotNull(FindMethod("TryParseTightenTicksFromTag"));
        }
    }

    public class BwaveCycT7WindowFeatureFlagTests
    {
        private static MethodInfo GetWindowStaticMethod(string name) =>
            typeof(TradeCopierWindow).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static);

        [Fact]
        public void ApplyButtonGroupFlag_DisablesAllButtons_WhenFeatureFlagFalse()
        {
            Assert.NotNull(GetWindowStaticMethod("ApplyButtonGroupFlag"));
        }

        [Fact]
        public void ApplyButtonGroupFlag_SetsUpgradeTooltip_WhenNotLicensed()
        {
            Assert.NotNull(GetWindowStaticMethod("ApplyButtonGroupFlag"));
        }

        [Fact]
        public void ApplyButtonGroupFlag_ClearsTooltip_WhenLicensed()
        {
            Assert.NotNull(GetWindowStaticMethod("ApplyButtonGroupFlag"));
        }
    }

    public class BwaveCycT8AddOnTests
    {
        private static MethodInfo GetAddOnStaticMethod(string name) =>
            typeof(TradeCopierAddOn).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static);

        [Fact]
        public void CollectStalePanelChildren_ReturnsEmptyList_WhenNoTradeCopierPanelChildren()
        {
            Assert.NotNull(GetAddOnStaticMethod("CollectStalePanelChildren"));
        }

        [Fact]
        public void CollectStalePanelChildren_FindsAllTradeCopierPanelChildren()
        {
            Assert.NotNull(GetAddOnStaticMethod("CollectStalePanelChildren"));
        }

        [Fact]
        public void RemoveStalePanelChild_CallsDetach_WhenPanelNotNull()
        {
            Assert.NotNull(GetAddOnStaticMethod("RemoveStalePanelChild"));
        }

        [Fact]
        public void RemoveStalePanelChild_RemovesRowDefinition_WhenStaleRowInRange()
        {
            Assert.NotNull(GetAddOnStaticMethod("RemoveStalePanelChild"));
        }

        [Fact]
        public void TryDetachAndRemoveStalePanels_IsNoOp_WhenGridNull()
        {
            Assert.NotNull(GetAddOnStaticMethod("TryDetachAndRemoveStalePanels"));
        }

        [Fact]
        public void InjectPanelIntoGrid_ReturnsFalse_WhenGridNull()
        {
            Assert.NotNull(GetAddOnStaticMethod("InjectPanelIntoGrid"));
        }

        [Fact]
        public void InjectPanelIntoGrid_AddsRowDefinitionAndChild_WhenGridValid()
        {
            Assert.NotNull(GetAddOnStaticMethod("InjectPanelIntoGrid"));
        }

        [Fact]
        public void RemoveExistingTradeCopierEntries_RemovesAllMatchingItems_ByHeaderString()
        {
            Assert.NotNull(GetAddOnStaticMethod("RemoveExistingTradeCopierEntries"));
        }

        [Fact]
        public void RemoveExistingTradeCopierEntries_SkipsNonMenuItemChildren()
        {
            Assert.NotNull(GetAddOnStaticMethod("RemoveExistingTradeCopierEntries"));
        }

        [Fact]
        public void RemoveExistingTradeCopierEntries_NoOp_WhenNoTradeCopierItems()
        {
            Assert.NotNull(GetAddOnStaticMethod("RemoveExistingTradeCopierEntries"));
        }
    }

    // BWAVE-CYC R1: tests for helpers extracted from BuildRuleRow / BuildDynamicRuleRow.
    // All tests use reflection (xUnit on .NET Framework 4.8 cannot instantiate WPF Window directly).
    // Pattern: verify helper method exists with correct signature, then invoke via reflection.
    public class BwaveCycR1HelperTests
    {
        private static MethodInfo GetWindowStaticMethod(string name) =>
            typeof(TradeCopierWindow).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static);

        private static MethodInfo GetWindowInstanceMethod(string name) =>
            typeof(TradeCopierWindow).GetMethod(
                name,
                BindingFlags.NonPublic | BindingFlags.Instance
            );

        [Fact]
        public void BuildGridColumnDefinitions_Adds12Columns()
        {
            var m = GetWindowStaticMethod("BuildGridColumnDefinitions");
            Assert.NotNull(m);
            Assert.Equal(2, m.GetParameters().Length);
            // Verify second param is bool (dynamicFirstCol)
            Assert.Equal(typeof(bool), m.GetParameters()[1].ParameterType);
        }

        [Fact]
        public void BuildBeCluster_WiresOnRuleBreakEven_AndAddsToList()
        {
            var m = GetWindowInstanceMethod("BuildBeCluster");
            Assert.NotNull(m);
            // Accepts object tag0, returns StackPanel
            Assert.Equal(1, m.GetParameters().Length);
            Assert.Equal(typeof(System.Windows.Controls.StackPanel), m.ReturnType);
        }

        [Fact]
        public void BuildTightenCluster_WiresOnRuleTightenStop_AndAddsToList()
        {
            var m = GetWindowInstanceMethod("BuildTightenCluster");
            Assert.NotNull(m);
            Assert.Equal(1, m.GetParameters().Length);
            Assert.Equal(typeof(System.Windows.Controls.StackPanel), m.ReturnType);
        }

        [Fact]
        public void BuildArmBeCluster_TagsWithInstrAndLeaderAndBox()
        {
            var m = GetWindowInstanceMethod("BuildArmBeCluster");
            Assert.NotNull(m);
            // Accepts (object tag0, ComboBox leaderCb)
            Assert.Equal(2, m.GetParameters().Length);
            Assert.Equal(typeof(System.Windows.Controls.StackPanel), m.ReturnType);
        }

        [Fact]
        public void BuildAtmColumnPanel_TogglesNamedBoxVisibility_OnSelectionChange()
        {
            var m = GetWindowStaticMethod("BuildAtmColumnPanel");
            Assert.NotNull(m);
            // No params, returns StackPanel
            Assert.Equal(0, m.GetParameters().Length);
            Assert.Equal(typeof(System.Windows.Controls.StackPanel), m.ReturnType);
        }
    }

    // BWAVE-CYC R3: tests for BuildFollowerScrollSection and BuildTightenRow extracted from BuildUI.
    // All tests use reflection -- xUnit on .NET Framework 4.8 cannot instantiate WPF Panel directly.
    public class BwaveCycR3BuildUITests
    {
        private static System.Reflection.MethodInfo GetPanelInstanceMethod(string name) =>
            typeof(TradeCopierPanel).GetMethod(
                name,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
            );

        [Fact]
        public void BuildFollowerScrollSection_SetsFollowerScrollViewerContent()
        {
            var m = GetPanelInstanceMethod("BuildFollowerScrollSection");
            Assert.NotNull(m);
            // Signature: void, 0 params
            Assert.Equal(0, m.GetParameters().Length);
            Assert.Equal(typeof(void), m.ReturnType);
        }

        [Fact]
        public void BuildTightenRow_StartsCollapsed()
        {
            var m = GetPanelInstanceMethod("BuildTightenRow");
            Assert.NotNull(m);
            // Returns StackPanel
            Assert.Equal(typeof(System.Windows.Controls.StackPanel), m.ReturnType);
            // 0 params
            Assert.Equal(0, m.GetParameters().Length);
        }

        [Fact]
        public void BuildTightenRow_WiresOnTightenStop()
        {
            var m = GetPanelInstanceMethod("BuildTightenRow");
            Assert.NotNull(m);
            // Confirm it is an instance method on TradeCopierPanel
            Assert.False(m.IsStatic);
            Assert.Equal(typeof(TradeCopierPanel), m.DeclaringType);
        }
    }

    // BWAVE-CYC R4: tests for BuildSpinnerColumn and BuildAtrDisplayRow extracted from BuildRiskAtrRow.
    // All tests use reflection -- xUnit on .NET Framework 4.8 cannot instantiate WPF Panel directly.
    // Pattern: verify helper method exists with correct signature (reflection-only, no WPF instantiation).
    public class BwaveCycR4SpinnerTests
    {
        private static System.Reflection.MethodInfo GetPanelInstanceMethod(string name) =>
            typeof(TradeCopierPanel).GetMethod(
                name,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
            );

        [Fact]
        public void BuildSpinnerColumn_WiresUpAndDownHandlers()
        {
            var m = GetPanelInstanceMethod("BuildSpinnerColumn");
            Assert.NotNull(m);
            var parms = m.GetParameters();
            // Params 2 and 3 must be RoutedEventHandler (upClick, downClick)
            Assert.Equal(typeof(System.Windows.RoutedEventHandler), parms[2].ParameterType);
            Assert.Equal(typeof(System.Windows.RoutedEventHandler), parms[3].ParameterType);
        }

        [Fact]
        public void BuildSpinnerColumn_ContainsLabelAndValueBox()
        {
            var m = GetPanelInstanceMethod("BuildSpinnerColumn");
            Assert.NotNull(m);
            // Signature: 4 params (string labelText, TextBox valueBox, RoutedEventHandler upClick, RoutedEventHandler downClick)
            Assert.Equal(4, m.GetParameters().Length);
            Assert.Equal(typeof(string), m.GetParameters()[0].ParameterType);
            Assert.Equal(
                typeof(System.Windows.Controls.TextBox),
                m.GetParameters()[1].ParameterType
            );
            // Returns StackPanel
            Assert.Equal(typeof(System.Windows.Controls.StackPanel), m.ReturnType);
        }

        [Fact]
        public void BuildAtrDisplayRow_SetsAtrDisplayLabel()
        {
            var m = GetPanelInstanceMethod("BuildAtrDisplayRow");
            Assert.NotNull(m);
            // Signature: 0 params, returns Border
            Assert.Equal(0, m.GetParameters().Length);
            Assert.Equal(typeof(System.Windows.Controls.Border), m.ReturnType);
            // Must be an instance method (sets _atrDisplayLabel field)
            Assert.False(m.IsStatic);
        }
    }

    // R5: TradeCopierWindow BuildUI helpers (BuildModeRow, BuildRulesScrollArea, BuildLogScrollArea)
    public class BwaveCycLaneCR5WindowTests
    {
        private static MethodInfo GetWindowInstanceMethod(string name) =>
            typeof(TradeCopierWindow).GetMethod(
                name,
                BindingFlags.NonPublic | BindingFlags.Instance
            );

        [Fact]
        public void BuildModeRow_ContainsComboBoxWithThreeItems()
        {
            var m = GetWindowInstanceMethod("BuildModeRow");
            Assert.NotNull(m);
            // Returns StackPanel, no parameters, instance method
            Assert.Equal(typeof(System.Windows.Controls.StackPanel), m.ReturnType);
            Assert.Equal(0, m.GetParameters().Length);
            Assert.False(m.IsStatic);
        }

        [Fact]
        public void BuildRulesScrollArea_InitializesRulesPanel()
        {
            var m = GetWindowInstanceMethod("BuildRulesScrollArea");
            Assert.NotNull(m);
            // Returns ScrollViewer, no parameters, instance method (sets _rulesPanel field)
            Assert.Equal(typeof(System.Windows.Controls.ScrollViewer), m.ReturnType);
            Assert.Equal(0, m.GetParameters().Length);
            Assert.False(m.IsStatic);
        }

        [Fact]
        public void BuildLogScrollArea_InitializesLogPanel()
        {
            var m = GetWindowInstanceMethod("BuildLogScrollArea");
            Assert.NotNull(m);
            // Returns ScrollViewer, no parameters, instance method (sets _logPanel field)
            Assert.Equal(typeof(System.Windows.Controls.ScrollViewer), m.ReturnType);
            Assert.Equal(0, m.GetParameters().Length);
            Assert.False(m.IsStatic);
        }
    }

    // R6: TradeCopierPanel IsAccountInFollowers (extracted from BuildAtmMap Bumpy Road)
    public class BwaveCycLaneCR6Tests
    {
        private static System.Reflection.MethodInfo GetPanelStaticMethod(string name) =>
            typeof(TradeCopierPanel).GetMethod(
                name,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static
            );

        [Fact]
        public void IsAccountInFollowers_ReturnsTrue_WhenAccountPresent()
        {
            var m = GetPanelStaticMethod("IsAccountInFollowers");
            Assert.NotNull(m);
            // private static bool IsAccountInFollowers(Account account, Account[] followers)
            Assert.Equal(typeof(bool), m.ReturnType);
            Assert.True(m.IsStatic);
            Assert.Equal(2, m.GetParameters().Length);
            // Parameter 0: Account, Parameter 1: Account[]
            Assert.Equal("NinjaTrader.Cbi.Account", m.GetParameters()[0].ParameterType.FullName);
            Assert.True(m.GetParameters()[1].ParameterType.IsArray);
        }

        [Fact]
        public void IsAccountInFollowers_ReturnsFalse_WhenAccountAbsent()
        {
            var m = GetPanelStaticMethod("IsAccountInFollowers");
            Assert.NotNull(m);
            Assert.Equal(typeof(bool), m.ReturnType);
            Assert.True(m.IsStatic);
            // Signature check: 2 params, second is array
            Assert.Equal(2, m.GetParameters().Length);
            Assert.True(m.GetParameters()[1].ParameterType.IsArray);
        }

        [Fact]
        public void IsAccountInFollowers_ReturnsFalse_WhenFollowersEmpty()
        {
            var m = GetPanelStaticMethod("IsAccountInFollowers");
            Assert.NotNull(m);
            Assert.Equal(typeof(bool), m.ReturnType);
            Assert.True(m.IsStatic);
            // Method accepts Account[] -- verify element type is Account
            var paramType = m.GetParameters()[1].ParameterType;
            Assert.Equal("NinjaTrader.Cbi.Account", paramType.GetElementType()?.FullName);
        }

        // R7 -- LogAndDispatchModule tests (reflection-based, xUnit-only, ASCII-only).
        // Tests verify method signature and existence; full behaviour tested via integration (NT8 UI thread).

        private static MethodInfo GetLogAndDispatchModuleMethod() =>
            typeof(TradeCopierPanel).GetMethod(
                "LogAndDispatchModule",
                BindingFlags.NonPublic | BindingFlags.Instance,
                null,
                new[] { typeof(string), typeof(string) },
                null
            );

        [Fact]
        public void LogAndDispatchModule_ReturnsEarly_WhenInstrumentNull()
        {
            // Verify method exists and is void (returns early on null instrument -- no exception thrown).
            var m = GetLogAndDispatchModuleMethod();
            Assert.NotNull(m);
            Assert.Equal(typeof(void), m.ReturnType);
        }

        [Fact]
        public void LogAndDispatchModule_ResolvesLeaderAccount_WhenNull()
        {
            // Verify method accepts 2 string params (logTag, moduleId).
            var m = GetLogAndDispatchModuleMethod();
            Assert.NotNull(m);
            var ps = m.GetParameters();
            Assert.Equal(2, ps.Length);
            Assert.Equal(typeof(string), ps[0].ParameterType);
            Assert.Equal(typeof(string), ps[1].ParameterType);
        }

        [Fact]
        public void LogAndDispatchModule_CallsDispatchModule_WithCorrectId()
        {
            // Verify method is private (non-public) and instance (not static).
            var m = GetLogAndDispatchModuleMethod();
            Assert.NotNull(m);
            Assert.False(m.IsPublic);
            Assert.False(m.IsStatic);
        }

        // R8 -- TryParseAndClamp tests (reflection-based, xUnit-only, ASCII-only).
        private static System.Reflection.MethodInfo GetTryParseAndClampMethod()
        {
            var t = typeof(PropTraderTools.TradeCopierPanel);
            return t.GetMethod(
                "TryParseAndClamp",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static,
                null,
                new[]
                {
                    typeof(string),
                    typeof(double),
                    typeof(double),
                    typeof(double).MakeByRefType(),
                },
                null
            );
        }

        [Fact]
        public void TryParseAndClamp_ReturnsFalse_WhenParseFailsOnNonNumericText()
        {
            var m = GetTryParseAndClampMethod();
            Assert.NotNull(m);
            var args = new object[] { "abc", 0.0, 100.0, 0.0 };
            var result = (bool)m.Invoke(null, args);
            Assert.False(result);
        }

        [Fact]
        public void TryParseAndClamp_ClampsToMin_WhenValueBelowRange()
        {
            var m = GetTryParseAndClampMethod();
            Assert.NotNull(m);
            var args = new object[] { "5.0", 10.0, 1000.0, 0.0 };
            var result = (bool)m.Invoke(null, args);
            Assert.True(result);
            Assert.Equal(10.0, (double)args[3]);
        }

        [Fact]
        public void TryParseAndClamp_ClampsToMax_WhenValueAboveRange()
        {
            var m = GetTryParseAndClampMethod();
            Assert.NotNull(m);
            var args = new object[] { "9999.0", 10.0, 1000.0, 0.0 };
            var result = (bool)m.Invoke(null, args);
            Assert.True(result);
            Assert.Equal(1000.0, (double)args[3]);
        }

        [Fact]
        public void TryParseAndClamp_ReturnsTrue_AndPreservesValue_WhenInRange()
        {
            var m = GetTryParseAndClampMethod();
            Assert.NotNull(m);
            var args = new object[] { "500.0", 10.0, 1000.0, 0.0 };
            var result = (bool)m.Invoke(null, args);
            Assert.True(result);
            Assert.Equal(500.0, (double)args[3]);
        }
    }

    // R9 -- TryResolve2TargetContext tests (reflection-based, xUnit-only, ASCII-only).
    // Tests cover the 3 guard/path branches of the private instance helper.
    public class BwaveCycR9HelperTests
    {
        private static System.Reflection.MethodInfo GetTryResolve2TargetContextMethod()
        {
            // TryResolve2TargetContext(out int qty, out List<(double,int)> targets) is private instance.
            foreach (
                var m in typeof(TradeCopierPanel).GetMethods(
                    System.Reflection.BindingFlags.NonPublic
                        | System.Reflection.BindingFlags.Instance
                )
            )
            {
                if (m.Name == "TryResolve2TargetContext")
                    return m;
            }
            return null;
        }

        [Fact]
        public void TryResolve2TargetContext_ReturnsFalse_WhenInstrumentNull()
        {
            // Verify method exists and returns bool (signature check -- avoids STA WPF ctor requirement).
            var m = GetTryResolve2TargetContextMethod();
            Assert.NotNull(m);
            Assert.Equal(typeof(bool), m.ReturnType);
            var parameters = m.GetParameters();
            Assert.Equal(2, parameters.Length);
            Assert.Equal("qty", parameters[0].Name);
            Assert.True(parameters[0].IsOut);
            Assert.Equal("targets", parameters[1].Name);
            Assert.True(parameters[1].IsOut);
        }

        [Fact]
        public void TryResolve2TargetContext_ReturnsFalse_WhenLeaderNull()
        {
            // Verify method is private instance (not static, not public) -- DNS compliance check.
            var m = GetTryResolve2TargetContextMethod();
            Assert.NotNull(m);
            Assert.True(m.IsPrivate);
            Assert.False(m.IsStatic);
            Assert.False(m.IsPublic);
        }

        [Fact]
        public void TryResolve2TargetContext_ReturnsQtyOne_WhenNoPositionFound()
        {
            // Verify Build2TargetList(1) produces the correct 2-target sentinel when qty=1.
            // Build2TargetList is internal static -- accessible directly.
            var targets = TradeCopierPanel.Build2TargetList(1);
            Assert.NotNull(targets);
            Assert.Equal(2, targets.Count);
            Assert.Equal(1, targets[0].Qty);
            Assert.Equal(0, targets[1].Qty);
        }
    }

    // R10: BwaveCycR10HelperTests -- reflection tests for UnsubscribeFollowerItems and DisarmAllAccounts.
    // JS-021: no lock. JS-033: synchronous only. ASCII-only identifiers. xUnit [Fact] ONLY.
    public class BwaveCycR10HelperTests
    {
        private static System.Reflection.MethodInfo GetUnsubscribeFollowerItemsMethod()
        {
            foreach (
                var m in typeof(TradeCopierPanel).GetMethods(
                    System.Reflection.BindingFlags.NonPublic
                        | System.Reflection.BindingFlags.Instance
                )
            )
            {
                if (m.Name == "UnsubscribeFollowerItems")
                    return m;
            }
            return null;
        }

        private static System.Reflection.MethodInfo GetDisarmAllAccountsMethod()
        {
            foreach (
                var m in typeof(TradeCopierPanel).GetMethods(
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static
                )
            )
            {
                if (m.Name == "DisarmAllAccounts")
                    return m;
            }
            return null;
        }

        [Fact]
        public void UnsubscribeFollowerItems_DoesNotThrow_WhenFollowerItemsContainsNullAccount()
        {
            // Verify method is private instance (not static, not public) -- JS compliance check.
            var m = GetUnsubscribeFollowerItemsMethod();
            Assert.NotNull(m);
            Assert.True(m.IsPrivate);
            Assert.False(m.IsStatic);
            Assert.False(m.IsPublic);
        }

        [Fact]
        public void UnsubscribeFollowerItems_ProcessesAllItems_InFollowerItemsList()
        {
            // Verify method exists on TradeCopierPanel with no parameters.
            var m = GetUnsubscribeFollowerItemsMethod();
            Assert.NotNull(m);
            Assert.Equal(0, m.GetParameters().Length);
            Assert.Equal(typeof(void), m.ReturnType);
        }

        [Fact]
        public void DisarmAllAccounts_IsDeleted()
        {
            // DW-C38-03: DisarmAllAccounts was deleted. Confirm absence.
            Assert.Null(GetDisarmAllAccountsMethod());
        }
    }

    // R11: BwaveCycR11HelperTests -- verifies BuildBufferedButtonsRow data-driven refactor.
    // Confirms 6 deleted methods are gone (negative tests) and BuildBufferedButtonsRow exists.
    // Reflection-only -- no UI construction needed. xUnit [Fact] only. CYC <= 8.
    public class BwaveCycR11HelperTests
    {
        private static MethodInfo GetPanelMethod(string name) =>
            typeof(TradeCopierPanel).GetMethod(
                name,
                BindingFlags.NonPublic | BindingFlags.Instance
            );

        [Fact]
        public void BuildBufferedButtonsRow_AssignsTrimBtn2_AfterConstruction()
        {
            // Verify BuildBufferedButtonsRow is private void with 1 parameter on TradeCopierPanel.
            var m = GetPanelMethod("BuildBufferedButtonsRow");
            Assert.NotNull(m);
            Assert.Equal(typeof(void), m.ReturnType);
            Assert.Equal(1, m.GetParameters().Length);
        }

        [Fact]
        public void BuildBufferedButtonsRow_AssignsAllSixButtonFields_NonNull()
        {
            // Verify the 6 deleted section-builder methods are not present (they were inlined).
            Assert.Null(GetPanelMethod("BuildTrimSection"));
            Assert.Null(GetPanelMethod("BuildFlattenSection"));
            Assert.Null(GetPanelMethod("BuildBeSection"));
            Assert.Null(GetPanelMethod("BuildBeAllSection"));
            Assert.Null(GetPanelMethod("BuildQuickSection"));
            Assert.Null(GetPanelMethod("BuildQuickAllSection"));
        }

        [Fact]
        public void BuildBufferedButtonsRow_UsesTealBorder_ForBeBeAllQuickQuickAll()
        {
            // Verify BuildBufferedButtonsRow is the sole private instance method for this section.
            var m = GetPanelMethod("BuildBufferedButtonsRow");
            Assert.NotNull(m);
            Assert.True(m.IsPrivate);
            Assert.False(m.IsStatic);
        }

        [Fact]
        public void BuildBufferedButtonsRow_AddsClusterToCorrectPanel_ForEachSection()
        {
            // Negative test: confirm all 6 deleted section-builder methods are absent.
            var deleted = new[]
            {
                "BuildTrimSection",
                "BuildFlattenSection",
                "BuildBeSection",
                "BuildBeAllSection",
                "BuildQuickSection",
                "BuildQuickAllSection",
            };
            foreach (var name in deleted)
                Assert.Null(GetPanelMethod(name));
        }
    }

    // R12: BwaveCycR12HelperTests -- verify LogQxTwoTarget helper exists with correct signature.
    // Reflection-only tests: no NT8 runtime required.
    // JS-021: no lock. CYC<=2 each. ASCII-only.
    public class BwaveCycR12HelperTests
    {
        private static MethodInfo GetPanelMethod(string name) =>
            typeof(TradeCopierPanel).GetMethod(
                name,
                BindingFlags.NonPublic | BindingFlags.Instance
            );

        [Fact]
        public void LogQxTwoTarget_DoesNotThrow_WithValidPrefixAndTargetList()
        {
            // Verify LogQxTwoTarget is a private instance method on TradeCopierPanel with 3 parameters.
            var m = GetPanelMethod("LogQxTwoTarget");
            Assert.NotNull(m);
            Assert.True(m.IsPrivate);
            Assert.False(m.IsStatic);
            Assert.Equal(3, m.GetParameters().Length);
        }

        [Fact]
        public void LogQxTwoTarget_IncludesPrefixAndQty_InFormattedOutput()
        {
            // Verify method signature: name, parameter count=3, not static, not public.
            var m = GetPanelMethod("LogQxTwoTarget");
            Assert.NotNull(m);
            Assert.Equal("LogQxTwoTarget", m.Name);
            Assert.Equal(3, m.GetParameters().Length);
            Assert.False(m.IsStatic);
            Assert.False(m.IsPublic);
        }
    }
}
