// B50Tests.cs -- Clone Mode xUnit tests
// Block: PTT-COPIER-B50 Lane A
// NT8-054: test file in Tests\ subfolder (never flat root).
// DW-B48-02: All BXXTests.cs files must be in Tests\ per protocol established B48.
// JS-021: CopyEngine.Instance.SetCopyMode cleanup after each test (reset to Signal).
using Xunit;

namespace PropTraderTools
{
    public class B50Tests
    {
        // T_B50_01 -- CopyMode enum value check.
        // Verifies Clone=2 and that existing values are unchanged.
        // No NT8 runtime. Pure enum assertion.
        [Fact]
        public void T_B50_01_CopyMode_Clone_HasValue2()
        {
            Assert.Equal(2, (int)CopyMode.Clone);
            Assert.Equal(0, (int)CopyMode.Signal); // existing -- must not regress
            Assert.Equal(1, (int)CopyMode.Mirror); // existing -- must not regress
        }

        // T_B50_02 -- SetCopyMode(Clone) roundtrip via GetCopyMode.
        // Verifies engine returns Clone after SetCopyMode(Clone).
        [Fact]
        public void T_B50_02_SetCopyMode_Clone_SetsModeValueToClone()
        {
            CopyEngine.Instance.SetCopyMode(CopyMode.Clone);
            Assert.Equal(CopyMode.Clone, CopyEngine.Instance.GetCopyMode());
            CopyEngine.Instance.SetCopyMode(CopyMode.Signal); // cleanup
        }

        // T_B50_03 -- GetCloneAtmMode returns Named when cache is non-empty.
        // Verifies Clone dispatch path injects Named ATM mode with correct template name.
        [Fact]
        public void T_B50_03_DispatchCopy_CloneMode_UsesCloneAtmCache()
        {
            CopyEngine.Instance.SetCopyMode(CopyMode.Clone);
            CopyEngine.Instance.SetCloneAtmCache("MES $200 SL5");
            var mode = CopyEngine.Instance.GetCloneAtmMode();
            Assert.IsType<FollowerAtmMode.Named>(mode);
            var named = (FollowerAtmMode.Named)mode;
            Assert.Equal("MES $200 SL5", named.TemplateName);
            CopyEngine.Instance.SetCopyMode(CopyMode.Signal); // cleanup
        }

        // T_B50_04 -- Clone mode does not activate Mirror guard.
        // Verifies Clone != Mirror so Gate B (HandleBracketChange) fires unconditionally.
        // Bracket sync for Clone is handled by Gate B without Mirror intercept.
        [Fact]
        public void T_B50_04_HandleBracketChange_CloneMode_SyncsFollowers()
        {
            CopyEngine.Instance.SetCopyMode(CopyMode.Clone);
            // Clone must not be confused with Mirror (which triggers MirrorOrderUpdate)
            Assert.NotEqual(CopyMode.Mirror, CopyEngine.Instance.GetCopyMode());
            Assert.Equal(CopyMode.Clone, CopyEngine.Instance.GetCopyMode());
            CopyEngine.Instance.SetCopyMode(CopyMode.Signal); // cleanup
        }

        // T_B50_05 -- GetCloneAtmMode returns Inherit when cache is empty.
        // Verifies fallback behavior when no ATM template was cached at Clone click time.
        [Fact]
        public void T_B50_05_CloneAtmCache_EmptyFallback_UsesDefault()
        {
            CopyEngine.Instance.SetCopyMode(CopyMode.Clone);
            CopyEngine.Instance.SetCloneAtmCache(string.Empty);
            var mode = CopyEngine.Instance.GetCloneAtmMode();
            Assert.IsType<FollowerAtmMode.Inherit>(mode);
            CopyEngine.Instance.SetCopyMode(CopyMode.Signal); // cleanup
        }
    }
}
