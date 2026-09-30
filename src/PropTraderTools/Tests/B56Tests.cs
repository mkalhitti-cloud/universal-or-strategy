// PTT-COPIER-B56 -- B56Tests.cs
// xUnit [Fact] tests for B56-LaneB: Rules Refresh + Clone Mode Fix.
// Defects closed: DW-B56-02 (rules not rebuilt after LoadRules) + DW-B56-03 (Clone missing from enum).
// T_B56B_01: GetRuleInstruments_ReturnsEmpty_WhenNoRules -- JS-002 contract.
// T_B56B_02: CopyModeEnum_HasCloneValue2 -- locks the Clone=2 enum contract.
// Jane Street rules: JS-002 (no return null), JS-021 (no lock), JS-033 (no async void).
// xUnit only -- no NUnit, no MSTest. NT8-054: Tests\ subfolder.
// CYC: T_B56B_01 = CYC 1, T_B56B_02 = CYC 1.
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace PropTraderTools
{
    /// <summary>
    /// B56-LaneB: Rules Refresh and Clone Mode Fix.
    /// DW-B56-02: GetRuleInstruments returns empty (not null) when no rules loaded.
    /// DW-B56-03: CopyMode.Clone == 2.
    /// </summary>
    public class B56Tests
    {
        // -------------------------------------------------------------------------
        // T_B56B_01 -- GetRuleInstruments returns empty IEnumerable when no rules
        // -------------------------------------------------------------------------

        /// <summary>
        /// When CopyEngine has no rules loaded, GetRuleInstruments() returns an empty
        /// IEnumerable (not null). JS-002: empty IEnumerable is the null-safe return contract.
        /// </summary>
        [Fact]
        public void T_B56B_01_GetRuleInstruments_ReturnsEmpty_WhenNoRules()
        {
            // Engine singleton -- _rules starts empty (or was reset by prior test cleanup).
            // GetRuleInstruments() must return empty IEnumerable, never null.
            var result = CopyEngine.Instance.GetRuleInstruments().ToList();
            Assert.Equal(0, result.Count);
        }

        // -------------------------------------------------------------------------
        // T_B56B_02 -- CopyMode enum has Clone=2
        // -------------------------------------------------------------------------

        /// <summary>
        /// CopyMode.Clone must equal 2. Documents and locks the B56 enum contract.
        /// Ensures Signal=0 and Mirror=1 are not regressed.
        /// </summary>
        [Fact]
        public void T_B56B_02_CopyModeEnum_HasCloneValue2()
        {
            Assert.Equal(2, (int)CopyMode.Clone);
            Assert.True(System.Enum.IsDefined(typeof(CopyMode), 2));
            Assert.Equal(0, (int)CopyMode.Signal); // no regression
            Assert.Equal(1, (int)CopyMode.Mirror); // no regression
        }
    }
}
