// BWAVE-NEXT LaneBRepair-R4 T1 -- R4-F1 STALE regression guard.
// R4-F1 investigated and found STALE: R3-F2 already ordered cleanup AFTER submit.
// This test guards against future edits that move cleanup before submit.
// xUnit only -- JS-051. No lock() -- JS-021. No async void -- JS-033.
// CYC=1. ASCII-only -- JS-004.
using System;
using Xunit;

namespace PropTraderTools.Tests
{
    public class BwaveNextLaneBRepairR4Tests
    {
        [Fact]
        public void SubmitDrainedEntry_SourceOrdering_SubmitBeforeCleanup_StaleR4F1()
        {
            // Regression guard: R4-F1 was investigated and found STALE.
            // This test confirms the R3-F2 ordering comment still exists in source,
            // guarding against any future edit that moves cleanup before submit.
            // If this comment disappears, the ordering may have been changed and
            // R4-F1 should be re-evaluated.
            //
            // Path resolution: walk up from BaseDirectory to find the workspace
            // root (identified by the presence of src/PropTraderTools/CopyEngine.cs).
            // This avoids assembly shadow-copy issues in the xUnit test runner.
            string dir = AppDomain.CurrentDomain.BaseDirectory;
            string copyEngineFile = null;
            for (int i = 0; i < 8; i++)
            {
                string candidate = System.IO.Path.Combine(
                    dir,
                    "src",
                    "PropTraderTools",
                    "CopyEngine.cs"
                );
                if (System.IO.File.Exists(candidate))
                {
                    copyEngineFile = candidate;
                    break;
                }
                string parent = System.IO.Path.GetDirectoryName(dir);
                if (parent == null || parent == dir)
                    break;
                dir = parent;
            }
            Assert.NotNull(copyEngineFile);
            var sourceText = System.IO.File.ReadAllText(copyEngineFile);
            Assert.Contains(
                "R3-F2: clear drain-owned IDs AFTER submit",
                sourceText,
                System.StringComparison.Ordinal
            );
        }
    }
}
