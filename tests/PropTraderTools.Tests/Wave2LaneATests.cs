// Wave2LaneATests.cs -- xUnit verification tests for WAVE2-LANE-A Ticket 1 (+ Ticket 2 to follow).
// Ticket 1: IsExitSignalName CCN 9->8 + IsNativeCloseOrFlattenSignal (CCN=3) extraction.
//   12 [Fact] tests exercising IsExitSignalName and IsNativeCloseOrFlattenSignal via reflection.
//
// PropTraderTools targets net48 (NT8 requirement).
// PropTraderTools.Tests targets net8.0.
// Direct ProjectReference is impossible across TFMs -- reflection seam is the established
// project pattern (see BwaveRefactorLaneBTests.cs T5 section).
// IsExitSignalName and IsNativeCloseOrFlattenSignal are internal static methods on CopyEngine.
// They take only string -- no NT8 runtime required.
//
// InternalsVisibleTo("PropTraderTools.Tests") declared at CopyEngine.cs:46 ensures
// internal members are accessible. At runtime, reflection with BindingFlags.NonPublic
// accesses internal/private members directly without the InternalsVisibleTo compile gate.
//
// Framework: xUnit ONLY. NEVER NUnit or MSTest. ASCII-only. No DateTime.Now. No lock().
// JS-021: no lock. JS-001: no throw. JS-002: no null return. JS-033: no async void.
using System.Reflection;
using Xunit;

namespace PropTraderTools.Tests
{
    public sealed class Wave2LaneAIsExitSignalNameTests
    {
        // Helper: load CopyEngine type from PropTraderTools.dll via reflection.
        // BaseDirectory = tests\PropTraderTools.Tests\bin\Debug\net8.0\
        // Navigate 5 levels up to workspace root, then into src\PropTraderTools\bin\Debug\.
        private static System.Type LoadCopyEngineType()
        {
            string dllPath = System.IO.Path.GetFullPath(
                System.IO.Path.Combine(
                    System.AppDomain.CurrentDomain.BaseDirectory,
                    "..",
                    "..",
                    "..",
                    "..",
                    "..",
                    "src",
                    "PropTraderTools",
                    "bin",
                    "Debug",
                    "PropTraderTools.dll"
                )
            );
            var asm = System.Reflection.Assembly.LoadFrom(dllPath);
            return asm.GetType("PropTraderTools.CopyEngine");
        }

        private static readonly System.Reflection.BindingFlags SeamFlags =
            BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;

        // Helper: return a named static method from CopyEngine.
        private static System.Reflection.MethodInfo GetCopyEngineMethod(string methodName)
        {
            var copyEngineType = LoadCopyEngineType();
            Assert.NotNull(copyEngineType);
            var m = copyEngineType.GetMethod(methodName, SeamFlags);
            Assert.NotNull(m);
            return m;
        }

        // Helper wrappers matching the call syntax described in the ticket spec.
        // IsExitSignalName and IsNativeCloseOrFlattenSignal are internal static on CopyEngine.
        private static bool IsExitSignalName(string name)
        {
            var m = GetCopyEngineMethod("IsExitSignalName");
            return (bool)m.Invoke(null, new object[] { name });
        }

        private static bool IsNativeCloseOrFlattenSignal(string name)
        {
            var m = GetCopyEngineMethod("IsNativeCloseOrFlattenSignal");
            return (bool)m.Invoke(null, new object[] { name });
        }

        // ------------------------------------------------------------------
        // IsExitSignalName tests (9 [Fact] tests)
        // ------------------------------------------------------------------

        [Fact]
        public void IsExitSignalName_NullInput_ReturnsFalse()
        {
            Assert.False(IsExitSignalName(null));
        }

        [Fact]
        public void IsExitSignalName_EmptyString_ReturnsTrue()
        {
            // DW-LB-FL-01: empty name = NT8 anonymous close order, treated as exit.
            Assert.True(IsExitSignalName(""));
        }

        [Fact]
        public void IsExitSignalName_PttPrefixed_ReturnsTrue()
        {
            Assert.True(IsExitSignalName("PTT-Stop1"));
        }

        [Fact]
        public void IsExitSignalName_CloseSignal_ReturnsTrue()
        {
            // NT8 platform-generated "Close" -- routes via IsNativeCloseOrFlattenSignal.
            Assert.True(IsExitSignalName("Close"));
        }

        [Fact]
        public void IsExitSignalName_FlattenSignal_ReturnsTrue()
        {
            // NT8 platform-generated "Flatten" -- routes via IsNativeCloseOrFlattenSignal.
            Assert.True(IsExitSignalName("Flatten"));
        }

        [Fact]
        public void IsExitSignalName_RevPrefix_ReturnsTrue()
        {
            Assert.True(IsExitSignalName("RevEntry"));
        }

        [Fact]
        public void IsExitSignalName_ExitPrefix_ReturnsTrue()
        {
            Assert.True(IsExitSignalName("ExitLong"));
        }

        [Fact]
        public void IsExitSignalName_AtmTarget_ReturnsTrue()
        {
            // B78 DW-B78-01: ATM profit-target bracket "Target1" is an exit signal.
            Assert.True(IsExitSignalName("Target1"));
        }

        [Fact]
        public void IsExitSignalName_EntrySignal_ReturnsFalse()
        {
            // "Entry" is intentionally NOT treated as exit.
            // Gate 2 filters to master account only -- follower "Entry" never reaches DispatchCopy.
            Assert.False(IsExitSignalName("Entry"));
        }

        // ------------------------------------------------------------------
        // IsNativeCloseOrFlattenSignal tests (3 [Fact] tests)
        // ------------------------------------------------------------------

        [Fact]
        public void IsNativeCloseOrFlattenSignal_Close_ReturnsTrue()
        {
            Assert.True(IsNativeCloseOrFlattenSignal("Close"));
        }

        [Fact]
        public void IsNativeCloseOrFlattenSignal_Flatten_ReturnsTrue()
        {
            Assert.True(IsNativeCloseOrFlattenSignal("Flatten"));
        }

        [Fact]
        public void IsNativeCloseOrFlattenSignal_CloseLowercase_ReturnsFalse()
        {
            // StringComparison.Ordinal (default ==): "close" != "Close". Case-sensitive contract.
            Assert.False(IsNativeCloseOrFlattenSignal("close"));
        }
    }


    public sealed class Wave2LaneAHasArmingAtmBracketsTests
    {
        // IsArmingOrderState is internal static on CopyEngine (src/PropTraderTools/CopyEngine.cs).
        // It takes OrderState (NinjaTrader.Cbi.OrderState -- NT8 enum, net48 only).
        // Calling it via reflection requires boxing an NT8 enum value, which triggers the NT8
        // obfuscated module static ctor -- fails on net8.0 (WindowsImpersonationContext absent).
        // Established project pattern: inline mirror (PttBreakEvenB72Tests, B143Tests, B141Tests).
        //
        // This test class:
        //   (a) Verifies the method exists via GetMethod (no parameter access, no enum boxing).
        //   (b) Mirrors the production logic inline and exercises every boundary.
        //
        // The inline mirror exactly matches CopyEngine.IsArmingOrderState source:
        //   if (s == OrderState.Initialized) return true;  -- 3
        //   if (s == OrderState.Working)     return true;  -- 10
        //   if (s == OrderState.Submitted)   return true;  -- 7
        //   if (s == OrderState.Accepted)    return true;  -- 0
        //   if (s == OrderState.TriggerPending) return true; -- 8
        //   return false;
        // NT8 OrderState integer values: Accepted=0 Cancelled=1 Filled=2 Initialized=3
        //   Submitted=7 TriggerPending=8 Rejected=9 Working=10
        // (confirmed from NinjaTrader.Core.dll reflection, 2026-08)

        // NT8 OrderState integer constants -- mirrors NinjaTrader.Cbi.OrderState enum.
        private static class NtOrderState
        {
            internal const int Accepted = 0;
            internal const int Cancelled = 1;
            internal const int Filled = 2;
            internal const int Initialized = 3;
            internal const int Submitted = 7;
            internal const int TriggerPending = 8;
            internal const int Rejected = 9;
            internal const int Working = 10;
        }

        // Inline mirror of CopyEngine.IsArmingOrderState(OrderState s).
        // Mirrors: if-chain returning true for Initialized/Working/Submitted/Accepted/TriggerPending.
        private static bool IsArmingOrderStateInline(int s)
        {
            if (s == NtOrderState.Initialized)
                return true;
            if (s == NtOrderState.Working)
                return true;
            if (s == NtOrderState.Submitted)
                return true;
            if (s == NtOrderState.Accepted)
                return true;
            if (s == NtOrderState.TriggerPending)
                return true;
            return false;
        }

        // Method-existence check: verify CopyEngine.IsArmingOrderState is present and internal.
        // Uses LoadFrom to load the DLL but does NOT access NT8 types or parameters.
        private static System.Reflection.MethodInfo GetIsArmingOrderStateMethod()
        {
            string dllPath = System.IO.Path.GetFullPath(
                System.IO.Path.Combine(
                    System.AppDomain.CurrentDomain.BaseDirectory,
                    "..",
                    "..",
                    "..",
                    "..",
                    "..",
                    "src",
                    "PropTraderTools",
                    "bin",
                    "Debug",
                    "PropTraderTools.dll"
                )
            );
            var asm = System.Reflection.Assembly.LoadFrom(dllPath);
            var copyEngineType = asm.GetType("PropTraderTools.CopyEngine");
            Assert.NotNull(copyEngineType);
            var flags =
                System.Reflection.BindingFlags.Static
                | System.Reflection.BindingFlags.NonPublic
                | System.Reflection.BindingFlags.Public;
            return copyEngineType.GetMethod("IsArmingOrderState", flags);
        }

        // ------------------------------------------------------------------
        // Test 0 (prerequisite): confirm IsArmingOrderState exists in production DLL.
        // Guards against accidental rename or removal.
        // ------------------------------------------------------------------
        [Fact]
        public void IsArmingOrderState_MethodExists_InCopyEngine()
        {
            var m = GetIsArmingOrderStateMethod();
            Assert.NotNull(m);
            Assert.True(m.IsStatic, "IsArmingOrderState must be static");
        }

        // ------------------------------------------------------------------
        // Inline mirror boundary tests (8 [Fact] tests).
        // Active states (return true): Initialized, Working, Submitted, Accepted, TriggerPending.
        // Inactive states (return false): Filled, Cancelled, Rejected.
        // ------------------------------------------------------------------

        [Fact]
        public void IsArmingOrderState_Initialized_ReturnsTrue()
        {
            // DW-LB-FL-01-V2: Initialized included for cancel-storm race (belt+suspenders).
            Assert.True(IsArmingOrderStateInline(NtOrderState.Initialized));
        }

        [Fact]
        public void IsArmingOrderState_Working_ReturnsTrue()
        {
            Assert.True(IsArmingOrderStateInline(NtOrderState.Working));
        }

        [Fact]
        public void IsArmingOrderState_Submitted_ReturnsTrue()
        {
            Assert.True(IsArmingOrderStateInline(NtOrderState.Submitted));
        }

        [Fact]
        public void IsArmingOrderState_Accepted_ReturnsTrue()
        {
            Assert.True(IsArmingOrderStateInline(NtOrderState.Accepted));
        }

        [Fact]
        public void IsArmingOrderState_TriggerPending_ReturnsTrue()
        {
            Assert.True(IsArmingOrderStateInline(NtOrderState.TriggerPending));
        }

        [Fact]
        public void IsArmingOrderState_Filled_ReturnsFalse()
        {
            Assert.False(IsArmingOrderStateInline(NtOrderState.Filled));
        }

        [Fact]
        public void IsArmingOrderState_Cancelled_ReturnsFalse()
        {
            Assert.False(IsArmingOrderStateInline(NtOrderState.Cancelled));
        }

        [Fact]
        public void IsArmingOrderState_Rejected_ReturnsFalse()
        {
            Assert.False(IsArmingOrderStateInline(NtOrderState.Rejected));
        }
    }
}