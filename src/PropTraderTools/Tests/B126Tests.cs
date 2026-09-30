// src/PropTraderTools/Tests/B126Tests.cs
// B126 -- DW-B58-01: SnapshotTargetsPublic hardcoded prefix constantification.
// xUnit [Fact] tests -- no NT8 runtime dependency.
// JS-066: CYC <= 8 per test method. JS-021: no lock. ASCII-only.

using Xunit;

namespace PropTraderTools.Tests
{
    public class B126Tests
    {
        // B126-T1: Verify all three constants have the exact compile-time values
        //          required by DW-B58-01. Primary regression guard -- if any
        //          constant value drifts in a future refactor this test fails.
        [Fact]
        public void B126_T1_Constants_PttBeTargetPrefix_EqualsExpected()
        {
            Assert.Equal("PTT-BE-Target-", PttOrderNames.PttBeTargetPrefix);
            Assert.Equal("PTT-QX-T", PttOrderNames.PttQxTargetPrefix);
            Assert.Equal("PTT-TGT-", PttOrderNames.PttTgtPrefix);
        }

        // B126-T2: PttQxTargetPrefix correctly matches a QX target order name
        //          and does NOT match a TGT order name.
        //          Verifies the constant's semantic correctness in the predicate
        //          used by SnapshotTargetsPublic (string.StartsWith -- same call).
        [Fact]
        public void B126_T2_PttQxTargetPrefix_MatchesPttQxOrder()
        {
            Assert.True(
                "PTT-QX-T1".StartsWith(
                    PttOrderNames.PttQxTargetPrefix,
                    System.StringComparison.Ordinal
                )
            );
            Assert.False(
                "PTT-TGT-1".StartsWith(
                    PttOrderNames.PttQxTargetPrefix,
                    System.StringComparison.Ordinal
                )
            );
        }

        // B126-T3: PttTgtPrefix correctly matches a TGT order name and does NOT
        //          match a QX order name.
        [Fact]
        public void B126_T3_PttQxTargetPrefix_DoesNotMatchNativeTarget()
        {
            Assert.True(
                "PTT-TGT-1".StartsWith(PttOrderNames.PttTgtPrefix, System.StringComparison.Ordinal)
            );
            Assert.False(
                "PTT-QX-T1".StartsWith(PttOrderNames.PttTgtPrefix, System.StringComparison.Ordinal)
            );
        }
    }
}
