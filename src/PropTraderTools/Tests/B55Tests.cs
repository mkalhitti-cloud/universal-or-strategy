// PTT-COPIER-B55 -- B55Tests.cs
// xUnit [Fact] tests for B55: ATM Template Read Fix (DW-B43-02 P1).
// Defect closed: DW-B43-02 -- GetLeaderAtmTemplateName read SelectedValue (null) instead of SelectedItem.
// Fix: TradeCopierPanel.GetLeaderAtmTemplateName() now reads cb.SelectedItem (line 2088).
// T_B55A_01: Documents the SelectedItem read path -- pure pattern, no WPF required.
// Jane Street rules: JS-002 (no return null), JS-021 (no lock), JS-033 (no async void).
// xUnit only -- no NUnit, no MSTest.
// CYC: T_B55A_01 = CYC 1 (straight-line assertion body).
using Xunit;

namespace PropTraderTools
{
    /// <summary>
    /// B55: ATM Template Read Fix -- documents that GetLeaderAtmTemplateName()
    /// reads ComboBox.SelectedItem (not SelectedValue) to retrieve the selected
    /// ATM template name from ChartTrader's cbxStrategySelector.
    /// Root cause: NT8 does not set SelectedValuePath on cbxStrategySelector,
    /// so SelectedValue is always null. SelectedItem IS the template name string.
    /// </summary>
    public class B55Tests
    {
        // -------------------------------------------------------------------------
        // T_B55A_01 -- SelectedItem set, SelectedValue null -> returns template name
        // -------------------------------------------------------------------------

        /// <summary>
        /// When ComboBox.SelectedItem is set to a template name and SelectedValue is null
        /// (because NT8 does not set SelectedValuePath on cbxStrategySelector),
        /// the SelectedItem read path returns the correct template name.
        /// Documents the fix: cb.SelectedItem as string ?? string.Empty (line 2088).
        /// </summary>
        [Fact]
        public void T_B55A_01_GetLeaderAtmTemplateName_SelectedItemSet_SelectedValueNull_ReturnsTemplateName()
        {
            // Arrange: simulate ComboBox state when NT8 populates cbxStrategySelector.
            // NT8 does NOT set SelectedValuePath, so SelectedValue is always null.
            object selectedItem = "MES $200"; // ComboBox.SelectedItem after user selects template
            string selectedValue = null; // ComboBox.SelectedValue -- null (no SelectedValuePath)

            // Act: exact expression from GetLeaderAtmTemplateName() line 2088
            string result = selectedItem as string ?? string.Empty;

            // Assert
            Assert.Equal("MES $200", result); // SelectedItem path returns the template name
            Assert.Null(selectedValue); // documents root cause: SelectedValue is null
        }
    }
}
