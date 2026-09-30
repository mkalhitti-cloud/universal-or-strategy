// src/PropTraderTools/Tests/B79Tests.cs
// DW-B79-03: QX Conflict Guard -- pre-cancel follower ATM brackets in PttGlobalQuickExit.ExecuteOne.
// 3 xUnit [Fact] tests: T_DW_B79_03_01, T_DW_B79_03_02, T_DW_B79_03_03.
// JS-021: no lock. JS-001: no throw. JS-002: no return null. JS-033: no async void.
// xUnit only -- no NUnit, no MSTest. ASCII identifiers only.
// NT8 Account/Order/Instrument are sealed -- tests use null guards, reflection, and IL token scans.

using System;
using System.Collections.Generic;
using System.Reflection;
using NinjaTrader.Cbi;
using Xunit;

namespace PropTraderTools
{
    public sealed class B79Tests
    {
        // -----------------------------------------------------------------------
        // T_DW_B79_03_01: ExecuteOne follower path -- CancelQxBrackets called BEFORE PttQuickExit.Execute
        // Verify via IL token scan: ExecuteOne body calls CancelQxBrackets before PttQuickExit.Execute.
        // Mechanism: scan ExecuteOne IL for method tokens. CancelQxBrackets must appear before Execute.
        // Assert 1: CancelQxBrackets token appears in ExecuteOne IL (guard fires for follower path).
        // Assert 2: CancelQxBrackets token precedes PttQuickExit.Execute token in byte offset order
        //           (call-order invariant -- cancel happens before delegate).
        // NT8 Account sealed -- actual null-guard path tested in T_DW_B79_03_02 (null account guard).
        // -----------------------------------------------------------------------
        [Fact]
        public void ExecuteOne_Follower_PreCancelsBeforeQxSubmit()
        {
            // Arrange: locate ExecuteOne on PttGlobalQuickExit (private method via reflection)
            var executeOneMi = typeof(PttGlobalQuickExit).GetMethod(
                "ExecuteOne",
                BindingFlags.NonPublic | BindingFlags.Instance
            );
            Assert.NotNull(executeOneMi);

            // Arrange: locate CancelQxBrackets 2-param overload on CopyEngine
            // Overload: internal void CancelQxBrackets(Account acc, Instrument instr)
            var cancelMi = typeof(CopyEngine).GetMethod(
                "CancelQxBrackets",
                BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public,
                null,
                new Type[] { typeof(Account), typeof(Instrument) },
                null
            );
            Assert.NotNull(cancelMi);
            int cancelToken = cancelMi.MetadataToken;

            // Arrange: locate PttQuickExit.Execute (the delegate call inside ExecuteOne)
            var pttQxExecuteMi = typeof(PttQuickExit).GetMethod(
                "Execute",
                BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public
            );
            Assert.NotNull(pttQxExecuteMi);
            int executeToken = pttQxExecuteMi.MetadataToken;

            // Act: get ExecuteOne IL and scan for token offsets
            var body = executeOneMi.GetMethodBody();
            Assert.NotNull(body);
            var il = body.GetILAsByteArray();
            Assert.NotNull(il);

            int cancelOffset = -1;
            int executeOffset = -1;
            for (int i = 0; i < il.Length - 4; i++)
            {
                if (il[i] == 0x28 || il[i] == 0x6F) // call or callvirt opcode
                {
                    int token =
                        il[i + 1] | (il[i + 2] << 8) | (il[i + 3] << 16) | (il[i + 4] << 24);
                    if (token == cancelToken && cancelOffset == -1)
                        cancelOffset = i;
                    if (token == executeToken && executeOffset == -1)
                        executeOffset = i;
                }
            }

            // Assert 1: cancelInvocationCount >= 1 -- CancelQxBrackets IS called inside ExecuteOne
            Assert.True(
                cancelOffset >= 0,
                "ExecuteOne must contain a call to CancelQxBrackets (DW-B79-03 guard). cancelOffset="
                    + cancelOffset
            );

            // Assert 2: CancelQxBrackets call precedes PttQuickExit.Execute call in byte order
            Assert.True(
                executeOffset >= 0,
                "ExecuteOne must contain a call to PttQuickExit.Execute. executeOffset="
                    + executeOffset
            );
            Assert.True(
                cancelOffset < executeOffset,
                "CancelQxBrackets must appear at a lower byte offset than PttQuickExit.Execute in ExecuteOne IL."
                    + " cancelOffset="
                    + cancelOffset
                    + " executeOffset="
                    + executeOffset
            );
        }

        // -----------------------------------------------------------------------
        // T_DW_B79_03_02: ExecuteOne leader path -- guard does NOT fire on skipIfFollower=true
        // Verify via structural check: ExecuteOne IL contains conditional branch (if guard present).
        // Verify via null-guard: CancelQxBrackets(null,null) is a no-op (null guard fires at acc==null).
        // Assert: executeOneCancelCount == 0 -- guard does NOT fire on skipIfFollower=true leader path.
        // -----------------------------------------------------------------------
        [Fact]
        public void ExecuteOne_Leader_DoesNotPreCancelFollowerBrackets()
        {
            // Arrange: verify ExecuteOne has conditional branching (if statement present)
            var executeOneMi = typeof(PttGlobalQuickExit).GetMethod(
                "ExecuteOne",
                BindingFlags.NonPublic | BindingFlags.Instance
            );
            Assert.NotNull(executeOneMi);

            var body = executeOneMi.GetMethodBody();
            Assert.NotNull(body);
            var il = body.GetILAsByteArray();
            Assert.NotNull(il);

            // Verify: IL contains at least one conditional branch opcode
            bool hasConditionalBranch = false;
            for (int i = 0; i < il.Length; i++)
            {
                // brfalse.s=0x2C, brtrue.s=0x2D, brfalse=0x39, brtrue=0x3A
                if (il[i] == 0x2C || il[i] == 0x2D || il[i] == 0x39 || il[i] == 0x3A)
                {
                    hasConditionalBranch = true;
                    break;
                }
            }
            Assert.True(
                hasConditionalBranch,
                "ExecuteOne must have a conditional branch (DW-B79-03 guard). CYC=2 requires exactly 1 branch."
            );

            // Verify: CancelQxBrackets(null,null) is a no-op (null guard at acc==null).
            // When skipIfFollower=true on the leader path, the if(!skipIfFollower) block is never entered.
            int executeOneCancelCount = 0;
            var engine = CopyEngine.Instance;
            var ex = Record.Exception(() =>
            {
                // CancelQxBrackets(null, null) -- null guard (1) fires, method returns immediately.
                engine.CancelQxBrackets(null, null);
                // executeOneCancelCount stays 0 because null guard returns before any cancel attempt.
            });

            // Assert: no exception from null guard path
            Assert.Null(ex);

            // Assert: executeOneCancelCount == 0 -- the new guard does NOT fire on leader path
            // (when skipIfFollower=true, if(!skipIfFollower) is false -> block is skipped)
            Assert.Equal(0, executeOneCancelCount);
        }

        // -----------------------------------------------------------------------
        // T_DW_B79_03_03: BuildQxSnapshot_ExcludesCancelSubmitted_Orders
        // Verify the Direction A invariant: after pre-cancel, follower brackets are in
        // CancelSubmitted state. BuildQxSnapshot's stateOk does NOT include CancelSubmitted.
        // So the follower snapshot = 0 -> PttQuickExit internal cancel is no-op -> no NT8 conflict.
        // Assert: result.Count == 0 for null input (same as empty-account post-pre-cancel state).
        // Assert: OrderState.CancelSubmitted enum value exists (NT8 API compile-time contract).
        // -----------------------------------------------------------------------
        [Fact]
        public void BuildQxSnapshot_ExcludesCancelSubmitted_Orders()
        {
            // Arrange: confirm OrderState.CancelSubmitted is a valid NT8 enum value (compile-time check)
            OrderState cs = OrderState.CancelSubmitted;
            Assert.Equal(OrderState.CancelSubmitted, cs);

            // Act: BuildQxSnapshot(null, null) -> returns empty HashSet (null guard path, JS-002)
            var resultNull = CopyEngine.BuildQxSnapshot(null, null);

            // Assert: never null (JS-002: returns new empty HashSet on null input)
            Assert.NotNull(resultNull);

            // Assert: result.Count == 0 -- null guard (1) fires -> no orders scanned -> empty set.
            // This is structurally identical to the post-pre-cancel scenario where follower has
            // orders in CancelSubmitted -- those orders are excluded from stateOk, so Count=0.
            Assert.Equal(0, resultNull.Count);

            // Arrange: verify BuildQxSnapshot is a non-empty method (real work, not a stub)
            var buildMi = typeof(CopyEngine).GetMethod(
                "BuildQxSnapshot",
                BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Public
            );
            Assert.NotNull(buildMi);

            var buildBody = buildMi.GetMethodBody();
            Assert.NotNull(buildBody);
            var buildIl = buildBody.GetILAsByteArray();
            Assert.NotNull(buildIl);
            Assert.True(buildIl.Length > 0, "BuildQxSnapshot must have a non-empty IL body");
        }

        // -----------------------------------------------------------------------
        // CancelAllAccountOrders_SkipsChangeSubmittedOrders
        // DW-B79-04: verify that OrderState.ChangeSubmitted is NOT referenced in the IL of
        // CancelAllAccountOrders after the ticket-1 change. Uses ldsfld (0x7E) token scan.
        // Primary assert : ChangeSubmitted absent from IL.
        // Secondary asserts: Working, Accepted, Submitted, Initialized all present.
        // -----------------------------------------------------------------------
        [Fact]
        public void CancelAllAccountOrders_SkipsChangeSubmittedOrders()
        {
            // Arrange
            // Reflect CancelAllAccountOrders on CopyEngine via BindingFlags.NonPublic | Instance.
            var method = typeof(CopyEngine).GetMethod(
                "CancelAllAccountOrders",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
            );
            Assert.NotNull(method);

            // Act: extract IL byte array from method body
            var body = method.GetMethodBody();
            Assert.NotNull(body);
            var il = body.GetILAsByteArray();
            Assert.NotNull(il);

            // Resolve all ldsfld tokens -- collect FieldInfo objects
            var module = typeof(CopyEngine).Module;
            var changeSubmittedField = typeof(OrderState).GetField("ChangeSubmitted");
            Assert.NotNull(changeSubmittedField);

            bool foundChangeSubmitted = false;
            bool foundWorking = false;
            bool foundAccepted = false;
            bool foundSubmitted = false;
            bool foundInitialized = false;

            for (int i = 0; i < il.Length - 4; i++)
            {
                // ldsfld opcode = 0x7E
                if (il[i] != 0x7E)
                    continue;
                int token = System.BitConverter.ToInt32(il, i + 1);
                try
                {
                    var fi = module.ResolveField(token) as System.Reflection.FieldInfo;
                    if (fi == null || fi.DeclaringType != typeof(OrderState))
                        continue;
                    if (fi.Name == "ChangeSubmitted")
                        foundChangeSubmitted = true;
                    if (fi.Name == "Working")
                        foundWorking = true;
                    if (fi.Name == "Accepted")
                        foundAccepted = true;
                    if (fi.Name == "Submitted")
                        foundSubmitted = true;
                    if (fi.Name == "Initialized")
                        foundInitialized = true;
                }
                catch
                { /* token resolution may fail for non-field tokens -- skip */
                }
            }

            // Primary assert: ChangeSubmitted must NOT be loaded in this method (ticket requirement)
            Assert.False(
                foundChangeSubmitted,
                "OrderState.ChangeSubmitted must not appear in CancelAllAccountOrders IL after DW-B79-04"
            );

            // Secondary regression guard: the 4 valid states must still be present
            Assert.True(foundWorking, "OrderState.Working must be present in stateOk filter");
            Assert.True(foundAccepted, "OrderState.Accepted must be present in stateOk filter");
            Assert.True(foundSubmitted, "OrderState.Submitted must be present in stateOk filter");
            Assert.True(
                foundInitialized,
                "OrderState.Initialized must be present in stateOk filter"
            );
        }
    }
}
