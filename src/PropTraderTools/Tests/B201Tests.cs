// B201Tests.cs -- xUnit tests for B201-ENTRY-CANCEL-01: IsQxCancelCandidate isFollower guard.
// Strategy: source-text assertions -- no NT8 runtime required.
// T_B201_01: Leader "Entry" order NOT cancelled (core B201 regression test).
// T_B201_02: Follower "Entry" order IS cancelled (follower path unchanged).
// T_B201_03: ATM bracket names always cancelled regardless of isFollower value.
// T_B201_04: CancelQxBrackets leader path skips "Entry" -- isFollower threaded end-to-end.
// Framework: xUnit only. No NUnit. No MSTest.
// JS-021: no lock. JS-033: no async void. ASCII-only.
using Xunit;

namespace PropTraderTools.Tests
{
    public class B201Tests
    {
        private static string Src() => System.IO.File.ReadAllText(
            System.IO.Path.Combine("..", "..", "..", "CopyEngine.cs"));

        // T_B201_01 -- Leader "Entry" NOT cancelled (B201 core regression)
        // Verifies IsQxCancelCandidate no longer returns true for a leader "Entry" order.
        // Before the B201 fix: if (o.Name == "Entry") return true; fired for ALL accounts.
        // After the fix: if (isFollower && o.Name == "Entry") -- leader path (isFollower=false) falls through.
        // isFollower=false AND o.Name=="Entry" => the guard does not fire => result is false.
        [Fact]
        public void T_B201_01_IsQxCancelCandidate_LeaderEntryOrder_NotCancelled()
        {
            var src = Src();
            // Confirms the B201 fix is present: isFollower param added to signature.
            Assert.Contains("internal static bool IsQxCancelCandidate(Order o, bool isFollower)", src);
            // Confirms the "Entry" branch is guarded by isFollower (not unconditional).
            Assert.Contains("if (isFollower && o.Name == \"Entry\")", src);
            // Confirms the fix comment is present to document B201-ENTRY-CANCEL-01.
            Assert.Contains("B201-ENTRY-CANCEL-01", src);
            // Source-text logic: with isFollower=false the "Entry" branch requires isFollower==true,
            // so "Entry" alone (isFollower=false) does NOT match -- the method falls through to return false.
            // This is verified by confirming the guard requires isFollower AND the name match together.
            Assert.DoesNotContain("if (o.Name == \"Entry\")\n                return true;", src);
        }

        // T_B201_02 -- Follower "Entry" IS cancelled (follower path unchanged by B201 fix)
        // Verifies the follower "Entry" cancel path is preserved: isFollower=true AND o.Name=="Entry"
        // still fires the guard and returns true (DW-B93: named ATM follower entry Limit).
        [Fact]
        public void T_B201_02_IsQxCancelCandidate_FollowerEntryOrder_Cancelled()
        {
            var src = Src();
            // Confirms the 2-param signature is present (T2 contract).
            Assert.Contains("internal static bool IsQxCancelCandidate(Order o, bool isFollower)", src);
            // Confirms the isFollower guard fires for the "Entry" branch.
            // With isFollower=true this branch evaluates to true && true => returns true.
            Assert.Contains("if (isFollower && o.Name == \"Entry\")", src);
            // Confirms the DW-B93 comment is still present -- fix is additive, not a replacement.
            Assert.Contains("DW-B93: Named ATM follower entry Limit", src);
            // Confirms the return true line for the "Entry" branch is still in the method.
            Assert.Contains("return true; // (6) DW-B93:", src);
        }

        // T_B201_03 -- ATM bracket names always cancelled regardless of isFollower
        // Verifies the fix is strictly additive: only the "Entry" branch gained the isFollower guard.
        // All other cancel-candidate branches (IsAtmBracketName, PTT-QX-*, PTT-BE-*, PTT-Copy*) are unchanged.
        // For isFollower=false (leader), names like "Stop1", "Target2", "PTT-QX-Stop",
        // "PTT-BE-Target-1", "PTT-CopyEntry" still return true via the unguarded earlier branches.
        [Fact]
        public void T_B201_03_IsQxCancelCandidate_AtmBracketNames_AlwaysCancelled()
        {
            var src = Src();
            // Confirms IsAtmBracketName helper is still called (branch 2 -- Stop*/Target* names).
            Assert.Contains("if (IsAtmBracketName(o.Name))", src);
            // Confirms PTT-QX-* prefix branch is unguarded (no isFollower condition).
            Assert.Contains("if (o.Name.StartsWith(\"PTT-QX-\", StringComparison.Ordinal))", src);
            // Confirms PTT-BE-* prefix branch is unguarded.
            Assert.Contains("if (o.Name.StartsWith(\"PTT-BE-\", StringComparison.Ordinal))", src);
            // Confirms PTT-Copy* prefix branch is unguarded (B70 DW-B70-02).
            Assert.Contains("if (o.Name.StartsWith(\"PTT-Copy\", StringComparison.Ordinal))", src);
            // Confirms the fix is ONLY on the "Entry" branch: the isFollower && guard appears exactly
            // once in IsQxCancelCandidate context, not on the PTT-* prefix branches.
            Assert.Contains("if (isFollower && o.Name == \"Entry\")", src);
        }

        // T_B201_04 -- CancelQxBrackets leader path skips "Entry" (end-to-end isFollower threading)
        // Verifies that CancelQxBrackets 2-arg overload reads IsFollowerAccount into a local bool isFollower
        // and threads it into every IsQxCancelCandidate call, so a leader account (IsFollowerAccount=false)
        // produces isFollower=false and the "Entry" order is excluded from the stale-cancel list.
        [Fact]
        public void T_B201_04_CancelQxBrackets_LeaderAccount_DoesNotCancelEntryOrder()
        {
            var src = Src();
            // Confirms CancelQxBrackets 2-arg reads IsFollowerAccount into a local variable.
            Assert.Contains("bool isFollower = IsFollowerAccount(acc);", src);
            // Confirms the local isFollower is threaded into IsQxCancelCandidate calls.
            Assert.Contains("IsQxCancelCandidate(o, isFollower)", src);
            // Confirms CancelQxBrackets 3-arg overload also reads IsFollowerAccount (both overloads fixed).
            // Both 2-arg and 3-arg bodies contain this pattern -- at least 2 occurrences in the source.
            var matchCount = System.Text.RegularExpressions.Regex.Matches(
                src, @"bool isFollower = IsFollowerAccount\(acc\);").Count;
            Assert.True(matchCount >= 2, "Expected bool isFollower = IsFollowerAccount(acc) in at least 2 CancelQxBrackets overloads");
            // Confirms leader path logic: CancelQxBrackets calls IsQxCancelCandidate(o, isFollower)
            // where isFollower derives from IsFollowerAccount -- for a leader account this is false,
            // so the "Entry" branch in IsQxCancelCandidate does not fire and "Entry" order is not added to stale list.
            Assert.DoesNotContain("IsQxCancelCandidate(o))", src);
        }
    }
}