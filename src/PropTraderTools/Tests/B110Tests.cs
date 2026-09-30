// src/PropTraderTools/Tests/B110Tests.cs
// B110: DW-B110 -- Remove CancelQxBracketsForFollowers from PttQuickExit leader path.
// 2 xUnit [Fact] tests: T_B110_01, T_B110_02.
// JS-021: no lock. JS-001: no throw. JS-002: no return null. JS-033: no async void.
// xUnit only -- no NUnit, no MSTest. ASCII identifiers only.

using System;
using System.Reflection;
using NinjaTrader.Cbi;
using Xunit;

namespace PropTraderTools
{
    public sealed class B110Tests
    {
        // -------------------------------------------------------------------------
        // T_B110_01: IL token scan -- PttQuickExit.Execute does NOT call
        // CancelQxBracketsForFollowers. Mirrors T_B68_03 pattern on CopyEngine.DispatchCopy.
        // -------------------------------------------------------------------------
        [Fact]
        public void T_B110_01_Execute_DoesNotCallCancelQxBracketsForFollowers()
        {
            // Arrange: locate Execute on PttQuickExit
            var executeMi = typeof(PttQuickExit).GetMethod(
                "Execute",
                BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public
            );
            Assert.NotNull(executeMi);

            // Arrange: locate CancelQxBracketsForFollowers on CopyEngine
            var cancelFollowersMi = typeof(CopyEngine).GetMethod(
                "CancelQxBracketsForFollowers",
                BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public
            );
            Assert.NotNull(cancelFollowersMi);

            int cancelToken = cancelFollowersMi.MetadataToken;

            // Act: scan Execute IL for CancelQxBracketsForFollowers token
            var body = executeMi.GetMethodBody();
            Assert.NotNull(body);
            var il = body.GetILAsByteArray();
            Assert.NotNull(il);

            bool foundCancelFollowers = false;
            for (int i = 0; i < il.Length - 4; i++)
            {
                // call (0x28) or callvirt (0x6F) opcode
                if (il[i] == 0x28 || il[i] == 0x6F)
                {
                    int token =
                        il[i + 1] | (il[i + 2] << 8) | (il[i + 3] << 16) | (il[i + 4] << 24);
                    if (token == cancelToken)
                    {
                        foundCancelFollowers = true;
                        break;
                    }
                }
            }

            // Assert: Execute must NOT call CancelQxBracketsForFollowers -- DW-B110 fix
            Assert.False(
                foundCancelFollowers,
                "PttQuickExit.Execute must NOT call CancelQxBracketsForFollowers -- DW-B110 fix"
            );
        }

        // -------------------------------------------------------------------------
        // T_B110_02: IL branch count scan -- PttQuickExit.Execute has exactly 6 branch
        // instructions after the DW-B110 fix, confirming CYC=7 (CYC = branch_count + 1).
        // -------------------------------------------------------------------------
        [Fact]
        public void T_B110_02_Execute_CycIs7_BranchCountIs6()
        {
            // Arrange: locate Execute on PttQuickExit
            var executeMi = typeof(PttQuickExit).GetMethod(
                "Execute",
                BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public
            );
            Assert.NotNull(executeMi);

            // Act: count branch instructions in Execute IL
            var body = executeMi.GetMethodBody();
            Assert.NotNull(body);
            var il = body.GetILAsByteArray();
            Assert.NotNull(il);

            // Branch opcodes (short and long forms):
            // brfalse.s=0x2C, brtrue.s=0x2D, br.s=0x2B
            // brfalse=0x39, brtrue=0x3A, br=0x38
            // beq.s=0x2E, bge.s=0x2F, bgt.s=0x30, ble.s=0x31, blt.s=0x32, bne.un.s=0x33
            // beq=0x3B, bge=0x3C, bgt=0x3D, ble=0x3E, blt=0x3F, bne.un=0x40
            // bge.un.s=0x34, bgt.un.s=0x35, ble.un.s=0x36, blt.un.s=0x37
            // bge.un=0x41, bgt.un=0x42, ble.un=0x43, blt.un=0x44
            int branchCount = 0;
            for (int i = 0; i < il.Length; i++)
            {
                byte op = il[i];
                if (
                    op == 0x2B
                    || op == 0x2C
                    || op == 0x2D
                    || // br.s, brfalse.s, brtrue.s
                    op == 0x2E
                    || op == 0x2F
                    || op == 0x30
                    || op == 0x31
                    || op == 0x32
                    || op == 0x33
                    || // beq.s..bne.un.s
                    op == 0x34
                    || op == 0x35
                    || op == 0x36
                    || op == 0x37
                    || // bge.un.s..blt.un.s
                    op == 0x38
                    || op == 0x39
                    || op == 0x3A
                    || // br, brfalse, brtrue
                    op == 0x3B
                    || op == 0x3C
                    || op == 0x3D
                    || op == 0x3E
                    || op == 0x3F
                    || op == 0x40
                    || // beq..bne.un
                    op == 0x41
                    || op == 0x42
                    || op == 0x43
                    || op == 0x44 // bge.un..blt.un
                )
                {
                    branchCount++;
                }
            }

            // Assert: CYC=7 means branchCount=6 (CYC = branch_count + 1)
            Assert.Equal(6, branchCount);
        }
    }
}
