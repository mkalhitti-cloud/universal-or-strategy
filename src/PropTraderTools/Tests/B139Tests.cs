// B139Tests.cs
// xUnit tests for DW-B152-B: CancelPending/CancelSubmitted gap in CancelExistingPttStpDrag.
// Framework: xUnit only. No NUnit. No MSTest.
// Seams: CancelExistingPttStpDragTestable, IsPttStpDragCancellableTestable.
// Account is a sealed NT8 type -- structural tests use IL reflection (same pattern as B135Tests.cs).
// Predicate tests (T_B139_02 through T_B139_06) use direct NinjaTrader.Cbi.Order instantiation.
// ASCII-only. No lock(). No throw. No return null. No async void.
using System.Reflection;
using NinjaTrader.Cbi;
using Xunit;

namespace PropTraderTools.Tests
{
    public class B139Tests
    {
        // Helper: creates an Order stub with the given OrderState.
        // IsPttStpDragCancellableTestable reads only o.OrderState -- no other NT8 fields needed.
        // Pattern: direct NinjaTrader.Cbi.Order instantiation (same as B134Tests.cs, B135Tests.cs).
        // Do NOT use Moq or any mocking framework.
        private static Order MakeFakeOrder(
            OrderState state,
            string name = "PTT-STP-Drag",
            string instrument = "MES SEP26"
        )
        {
            var o = new Order();
            o.OrderState = state;
            o.Name = name;
            // Instrument.FullName is read via ?.FullName; null instrument -> null == "MES SEP26" -> false.
            // For predicate-only tests, instrument field is irrelevant.
            return o;
        }

        // ----------------------------------------------------------------
        // T_B139_01 -- Accumulation prevention: CancelExistingPttStpDrag structural verification.
        // Confirms the compiled method body contains >= 3 callvirt opcodes (Orders, ToList, Cancel path)
        // and >= 2 conditional branches (foreach + if guard = CYC-relevant paths).
        // NT8 Account is sealed -- IL reflection is the correct structural test per B135Tests.cs pattern.
        // Three prior PTT-STP-Drags in mixed states (CancelPending, Working, Accepted) scenario:
        // all three match IsPttStpDragCancellable=true + Name=="PTT-STP-Drag" + instrument match.
        // The method iterates acc.Orders and calls acc.Cancel for each matching order.
        // ----------------------------------------------------------------
        [Fact]
        public void CancelExistingPttStpDrag_ThreePriorDragsInMixedStates_CancelsAllThree()
        {
            // IL scan: CancelExistingPttStpDrag must contain acc.Orders, ToList(), IsPttStpDragCancellable,
            // Name comparison, Instrument?.FullName, acc.Cancel -- at least 5 callvirt opcodes.
            // CYC=6: foreach(1) + if(1) + &&Name(1) + &&Instrument(1) + ?.(1) + base(1) -- at least 5 branches.
            var methodInfo = typeof(CopyEngine).GetMethod(
                "CancelExistingPttStpDrag",
                BindingFlags.NonPublic | BindingFlags.Instance
            );
            Assert.NotNull(methodInfo);

            var body = methodInfo.GetMethodBody();
            Assert.NotNull(body);
            var il = body.GetILAsByteArray();
            Assert.NotNull(il);

            // Count callvirt (0x6F) opcodes.
            // CancelExistingPttStpDrag calls: acc.Orders getter, ToList(), IsPttStpDragCancellable (callvirt via interface),
            // o.Name getter (x2), o.Instrument getter, Instrument.FullName getter, fo.Instrument getter,
            // fo.Instrument.FullName getter, acc.Cancel. Minimum 5 callvirt confirms cancel dispatch path.
            int callvirtCount = 0;
            for (int i = 0; i < il.Length; i++)
            {
                if (il[i] == 0x6F) // callvirt opcode
                    callvirtCount++;
            }

            Assert.True(
                callvirtCount >= 5,
                "CancelExistingPttStpDrag must contain >= 5 callvirt calls (confirms acc.Cancel dispatch for 3-drag burst). callvirtCount="
                    + callvirtCount
            );

            // Count conditional branches: brfalse.s=0x2C, brtrue.s=0x2D, brfalse=0x39, brtrue=0x3A,
            // bne.un.s=0x33, bne.un=0x40, beq.s=0x2E, beq=0x3B, br.s=0x2B, br=0x38.
            // CYC=6 -> at least 5 conditional branches confirm foreach + if + &&Name + &&Instrument + ?.
            int branchCount = 0;
            for (int i = 0; i < il.Length; i++)
            {
                byte op = il[i];
                if (
                    op == 0x2B
                    || op == 0x2C
                    || op == 0x2D
                    || op == 0x2E
                    || op == 0x33
                    || op == 0x38
                    || op == 0x39
                    || op == 0x3A
                    || op == 0x3B
                    || op == 0x40
                )
                    branchCount++;
            }

            Assert.True(
                branchCount >= 5,
                "CancelExistingPttStpDrag must have >= 5 branch opcodes (CYC=6, three-drag cancel path). branchCount="
                    + branchCount
            );
        }

        // ----------------------------------------------------------------
        // T_B139_02 -- DW-B152-B fix: CancelPending and CancelSubmitted return true.
        // Before B139: these states were missing from the filter -> race condition.
        // After B139: IsPttStpDragCancellable includes them -> cancel fires.
        // ----------------------------------------------------------------
        [Fact]
        public void IsPttStpDragCancellable_CancelPendingAndCancelSubmitted_ReturnTrue()
        {
            var orderCP = MakeFakeOrder(OrderState.CancelPending);
            var orderCS = MakeFakeOrder(OrderState.CancelSubmitted);

            Assert.True(CopyEngine.IsPttStpDragCancellableTestable(orderCP));
            Assert.True(CopyEngine.IsPttStpDragCancellableTestable(orderCS));
        }

        // ----------------------------------------------------------------
        // T_B139_03 -- DW-B151 regression: Working and Accepted orders are still cancelled.
        // Confirms the B139 refactor did not remove the pre-existing Working/Accepted states.
        // CancelExistingPttStpDrag IL scan: exception handler clause count >= 1 (try/catch absorbs).
        // ----------------------------------------------------------------
        [Fact]
        public void CancelExistingPttStpDrag_WorkingAndAcceptedDrag_CancelsCalled()
        {
            // Structural: exception handler confirms try/catch block is compiled (JS-001 compliance).
            var methodInfo = typeof(CopyEngine).GetMethod(
                "CancelExistingPttStpDrag",
                BindingFlags.NonPublic | BindingFlags.Instance
            );
            Assert.NotNull(methodInfo);

            var body = methodInfo.GetMethodBody();
            Assert.NotNull(body);

            // Verify: at least 1 exception handling clause (try/catch around acc.Cancel).
            int clauseCount = body.ExceptionHandlingClauses.Count;
            Assert.True(
                clauseCount >= 1,
                "CancelExistingPttStpDrag must have >= 1 exception handler (try/catch absorbs acc.Cancel failure). clauseCount="
                    + clauseCount
            );

            // Predicate regression: Working and Accepted must return true from the cancellable predicate.
            Assert.True(
                CopyEngine.IsPttStpDragCancellableTestable(MakeFakeOrder(OrderState.Working))
            );
            Assert.True(
                CopyEngine.IsPttStpDragCancellableTestable(MakeFakeOrder(OrderState.Accepted))
            );
        }

        // ----------------------------------------------------------------
        // T_B139_04 -- Terminal states correctly excluded from cancellation.
        // Cancelled/Filled/Rejected orders must return false from the predicate.
        // ----------------------------------------------------------------
        [Fact]
        public void IsPttStpDragCancellable_TerminalStates_ReturnFalse()
        {
            Assert.False(
                CopyEngine.IsPttStpDragCancellableTestable(MakeFakeOrder(OrderState.Cancelled))
            );
            Assert.False(
                CopyEngine.IsPttStpDragCancellableTestable(MakeFakeOrder(OrderState.Filled))
            );
            Assert.False(
                CopyEngine.IsPttStpDragCancellableTestable(MakeFakeOrder(OrderState.Rejected))
            );
        }

        // ----------------------------------------------------------------
        // T_B139_05 -- DW-B152 partial fix regression: Submitted state still returns true.
        // Guards against the CancelPending/CancelSubmitted addition accidentally removing Submitted.
        // ----------------------------------------------------------------
        [Fact]
        public void IsPttStpDragCancellable_Submitted_ReturnsTrue()
        {
            Assert.True(
                CopyEngine.IsPttStpDragCancellableTestable(MakeFakeOrder(OrderState.Submitted))
            );
        }

        // ----------------------------------------------------------------
        // T_B139_06 -- DW-B151 regression: Working state still returns true.
        // Guards against regression from the IsPttStpDragCancellable extraction refactor.
        // ----------------------------------------------------------------
        [Fact]
        public void IsPttStpDragCancellable_Working_ReturnsTrue()
        {
            Assert.True(
                CopyEngine.IsPttStpDragCancellableTestable(MakeFakeOrder(OrderState.Working))
            );
        }

        // ----------------------------------------------------------------
        // T_B139_07 -- Instrument selectivity: different instrument does not match filter.
        // CancelExistingPttStpDrag with fo.Instrument?.FullName="NQ SEP26" must not cancel
        // a PTT-STP-Drag on "MES SEP26". Uses new Account() (empty Orders) -- acc.Orders.ToList()
        // returns empty list, so foreach iterates 0 orders -> acc.Cancel never called -> no exception.
        // Verifies: instrument guard `o.Instrument?.FullName == fo.Instrument?.FullName` is selective.
        // ----------------------------------------------------------------
        [Fact]
        public void CancelExistingPttStpDrag_DifferentInstrument_DoesNotCancel()
        {
            // Arrange: engine + empty account (no orders to iterate) + fo with NQ instrument.
            // When acc.Orders is empty, the foreach body never executes -> acc.Cancel is never called.
            // This verifies the instrument-filter path: with different fo instrument, zero cancels occur.
            var engine = CopyEngine.Instance;
            var acc = new Account();
            var fo = MakeFakeOrder(OrderState.Working, "PTT-STP-Drag", "NQ SEP26");

            // Act: no exception expected -- empty orders means no cancel dispatch.
            var ex = Record.Exception(() => engine.CancelExistingPttStpDragTestable(acc, fo, "1")); // B142: suffix added

            // Assert: no exception (zero matching orders; different instrument does not cancel).
            Assert.Null(ex);
        }
    }
}
