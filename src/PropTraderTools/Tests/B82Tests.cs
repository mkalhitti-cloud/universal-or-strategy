// src/PropTraderTools/Tests/B82Tests.cs
// DW-B82-01: _beReplaceAttempts reset on slot consumption.
// 2 xUnit [Fact] tests: T_DW_B82_01_01, T_DW_B82_01_02.
// Root cause: _beReplaceAttempts never reset on slot consumption -- only on position close.
// After first BE-QX cycle counter reached 3 and never cleared, blocking all subsequent
// TryReplacePttBeBrackets calls (prevAttempts >= 3 => return) until position fully closed.
// Fix: TryRemove(_beReplaceAttempts) added immediately after each _pendingFollowerBeSlots.TryRemove
// in both TryFireFollowerBeRetry and the QueueBeRetryFallback timer Tick lambda.
// JS-021: no lock. JS-001: no throw. JS-002: no return null. JS-033: no async void.
// xUnit only. ASCII identifiers. NT8 sealed types not instantiated -- IL token scan pattern.
using System;
using System.Linq;
using System.Reflection;
using Xunit;

namespace PropTraderTools
{
    public sealed class B82Tests
    {
        // -----------------------------------------------------------------------
        // T_DW_B82_01_01: TryFireFollowerBeRetry IL contains exactly 2 TryRemove calls.
        // Before DW-B82-01 there was 1 TryRemove (slot only).
        // After DW-B82-01 there must be 2 TryRemove calls:
        //   (1) _pendingFollowerBeSlots.TryRemove -- atomic slot claim
        //   (2) _beReplaceAttempts.TryRemove      -- counter reset (DW-B82-01)
        // Mechanism: scan IL for callvirt (0x6F) opcodes that resolve to a method named
        // "TryRemove" declared on a generic ConcurrentDictionary type.
        // Assert: TryRemove callvirt count == 2.
        // -----------------------------------------------------------------------
        [Fact]
        public void T_DW_B82_01_01_TryFireFollowerBeRetry_ILContains_TwoTryRemoveCalls()
        {
            // Arrange: locate private TryFireFollowerBeRetry method via reflection
            var method = typeof(CopyEngine).GetMethod(
                "TryFireFollowerBeRetry",
                BindingFlags.NonPublic | BindingFlags.Instance
            );
            Assert.NotNull(method);

            var body = method.GetMethodBody();
            Assert.NotNull(body);
            var il = body.GetILAsByteArray();
            Assert.NotNull(il);

            // Act: scan IL for callvirt (0x6F) resolving to a method named "TryRemove"
            var module = typeof(CopyEngine).Module;
            int tryRemoveCount = 0;
            for (int i = 0; i < il.Length - 4; i++)
            {
                if (il[i] != 0x6F)
                    continue; // callvirt opcode
                int token = System.BitConverter.ToInt32(il, i + 1);
                try
                {
                    var mi = module.ResolveMethod(token) as MethodInfo;
                    if (mi != null && mi.Name == "TryRemove")
                        tryRemoveCount++;
                }
                catch { }
            }

            // Assert: exactly 2 TryRemove calls (slot + counter reset)
            Assert.Equal(2, tryRemoveCount);
        }

        // -----------------------------------------------------------------------
        // T_DW_B82_01_02: The compiler-generated Tick lambda inside QueueBeRetryFallback
        // contains at least 2 TryRemove callvirt instructions.
        // The timer Tick delegate is compiled into a nested <>c__DisplayClass* type on CopyEngine.
        // Before DW-B82-01: 1 TryRemove (slot claim only).
        // After DW-B82-01:  2 TryRemove calls (slot claim + counter reset).
        // Mechanism: find all nested types on CopyEngine, then scan all methods for
        // "TryRemove" callvirts; identify the one that contains the fallback path by
        // also looking for the string literal "fallback timer fired" (unique to the lambda).
        // Assert: the method containing "fallback timer fired" also has >= 2 TryRemove calls.
        // -----------------------------------------------------------------------
        [Fact]
        public void T_DW_B82_01_02_QueueBeRetryFallback_TimerLambda_ILContains_TwoTryRemoveCalls()
        {
            // Arrange: find all nested types declared on CopyEngine (compiler-generated lambdas)
            var nestedTypes = typeof(CopyEngine).GetNestedTypes(
                BindingFlags.NonPublic | BindingFlags.Public
            );
            Assert.NotNull(nestedTypes);
            Assert.True(
                nestedTypes.Length > 0,
                "Expected compiler-generated nested types on CopyEngine."
            );

            var module = typeof(CopyEngine).Module;
            System.Reflection.MethodBase lambdaMethod = null;
            int tryRemoveCount = 0;

            foreach (var nestedType in nestedTypes)
            {
                foreach (
                    var m in nestedType.GetMethods(
                        BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public
                    )
                )
                {
                    var mBody = m.GetMethodBody();
                    if (mBody == null)
                        continue;
                    var mIl = mBody.GetILAsByteArray();
                    if (mIl == null)
                        continue;

                    bool hasFallbackMarker = false;
                    int localTryRemoveCount = 0;

                    for (int i = 0; i < mIl.Length - 4; i++)
                    {
                        if (mIl[i] == 0x72) // ldstr opcode
                        {
                            int token = System.BitConverter.ToInt32(mIl, i + 1);
                            try
                            {
                                string s = module.ResolveString(token);
                                if (s != null && s.Contains("fallback timer fired"))
                                    hasFallbackMarker = true;
                            }
                            catch { }
                        }
                        if (mIl[i] == 0x6F) // callvirt opcode
                        {
                            int token = System.BitConverter.ToInt32(mIl, i + 1);
                            try
                            {
                                var mi = module.ResolveMethod(token) as MethodInfo;
                                if (mi != null && mi.Name == "TryRemove")
                                    localTryRemoveCount++;
                            }
                            catch { }
                        }
                    }

                    if (hasFallbackMarker)
                    {
                        lambdaMethod = m;
                        tryRemoveCount = localTryRemoveCount;
                        break;
                    }
                }
                if (lambdaMethod != null)
                    break;
            }

            // Assert: found the lambda
            Assert.NotNull(lambdaMethod);

            // Assert: >= 2 TryRemove calls (slot + counter reset)
            Assert.True(
                tryRemoveCount >= 2,
                "Expected >= 2 TryRemove calls in QueueBeRetryFallback timer lambda, found "
                    + tryRemoveCount
                    + ". "
                    + "DW-B82-01: _beReplaceAttempts reset missing from fallback path."
            );
        }
    }
}
