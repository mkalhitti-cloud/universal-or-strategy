hotfix: Remove readonly from PendingStopReplacement struct for .NET F…
#14
Open
backtothefutures83-oss
wants to merge 1 commit into
main
from
src/epic-13-extraction-v2
+91
-10
Lines changed: 91 additions & 10 deletions
Conversation21 (21)
Commits1 (1)
Checks28 (28)
Files changed2 (2)
Conversation
@backtothefutures83-oss
Owner
backtothefutures83-oss
commented
37 minutes ago
• 
User description
…ramework 4.8 compatibility (CS0518+CS8341)

Replaced C# 9.0 init accessors with C# 7.3 set accessors
Removed readonly modifier from struct declaration
Fixes 8x CS0518 (IsExternalInit not defined) + 8x CS8341 (readonly struct property errors)
Zero logic impact, maintains zero-allocation benefit
F5 verified: Build 1111.010-epic5-perf loads successfully in NinjaTrader 8
Mission Context
Build Tag:
Mission:

Files Changed
src/... —
Pre-Flight Checklist
Mandatory Gates (ALL must pass before merge)
 ASCII Gate: python check_ascii.py src/ — zero non-ASCII in C# strings
 Lock-Free Audit: grep -r "lock(" src/ — zero matches in strategy files
 Lint Pass: powershell -File .\scripts\lint.ps1 — LINT PASS confirmed
 Build Readiness: powershell -File .\scripts\build_readiness.ps1 — Build PASS
 AMAL Gate: python scripts/amal_harness.py — PASSED (Allocated = 0 B)
 Bob Shell Audit: Used v12-engineer mode with checkpointing: true
 Deploy Sync: powershell -File .\deploy-sync.ps1 — hard links re-established
 BUILD_TAG Banner: Verified in NinjaTrader Output window after F5 compile
Architecture Review
 No new lock() statements introduced
 All state mutations use Enqueue() actor model or Interlocked primitives
 _simaToggleSem released in finally blocks (if touched)
 No emoji, curly quotes, or em-dashes in Print() or string literals
Test Results
AMAL Benchmark Summary:
[paste AMAL output: Allocated = 0 B, Mean Latency < Baseline]
Agent Audit & Checkpoint
Bob Checkpoint ID:

 Gemini Standards Auditor review posted
 SonarCloud quality gate: PASSED
 No new P0/P1 SonarCloud issues introduced
Summary by Sourcery
Restore .NET Framework 4.8 compatibility by adjusting the PendingStopReplacement value type and documenting the hotfix.

Bug Fixes:

Resolve CS0518 IsExternalInit errors by replacing C# 9.0 init accessors with standard set accessors on PendingStopReplacement properties.
Resolve CS8341 readonly struct property errors by making PendingStopReplacement a non-readonly struct with mutable properties while preserving zero-allocation behavior.
Documentation:

Add a brain doc describing the IsExternalInit/CS8341 compilation issues, the applied fix, verification steps, and future C# language-version constraints.
Summary by cubic
Fixes .NET Framework 4.8 compile errors in the stop replacement struct and keeps the zero-allocation path. No behavior changes; NinjaTrader 8 build loads successfully.

Bug Fixes
Replaced C# 9 init accessors with set in PendingStopReplacement to remove the IsExternalInit dependency.
Changed readonly struct to struct to allow auto-properties under .NET Framework 4.8.
Clears 8x CS0518 and 8x CS8341 in V12_002.PositionInfo.cs.
Written for commit ac77254. Summary will update on new commits.

Review in cubic

Summary by CodeRabbit
Release Notes
Refactor
Internal optimization to memory allocation handling. No user-visible changes.
CodeAnt-AI Description
Fix .NET Framework 4.8 build errors in pending stop replacement handling

What Changed
Replaced unsupported init-style property setup with standard property assignment so the project compiles on NinjaTrader 8 / .NET Framework 4.8
Removed the readonly restriction from the pending stop replacement data so its fields can be set during creation without compiler errors
Added a write-up explaining the compilation failure, the fix, and the verification steps
Impact
✅ Successful NinjaTrader 8 builds on .NET Framework 4.8
✅ Fewer compile-time failures during deployment
✅ Clearer troubleshooting notes for future build issues

💡 Usage Guide
hotfix: Remove readonly from PendingStopReplacement struct for .NET F… 
ac77254
@codeant-ai
codeant-ai Bot
commented
37 minutes ago
CodeAnt AI is reviewing your PR.

@qodo-code-review
qodo-code-review Bot
commented
37 minutes ago
Qodo reviews are paused for this user.
Troubleshooting steps vary by plan Learn more →

On a Teams plan?
Reviews resume once this user has a paid seat and their Git account is linked in Qodo.
Link Git account →

Using GitHub Enterprise Server, GitLab Self-Managed, or Bitbucket Data Center?
These require an Enterprise plan - Contact us
Contact us →

@sourcery-ai
sourcery-ai Bot
commented
37 minutes ago
• 
Reviewer's Guide
This hotfix makes the PendingStopReplacement struct compatible with .NET Framework 4.8 by reverting C# 9.0-only features (init accessors and readonly struct) to C# 7.3-compatible constructs, and documents the IsExternalInit/CS8341 issue and resolution.

File-Level Changes
Change	Details	Files
Make PendingStopReplacement struct C# 7.3/.NET Framework 4.8 compatible while preserving zero-allocation behavior.	
Change PendingStopReplacement from readonly struct to struct so it can have mutable auto-properties without CS8341 errors.
Replace all init accessors on PendingStopReplacement properties with set accessors to remove the IsExternalInit dependency.
Keep the struct private and value-type to maintain zero-allocation behavior in the hot path.
src/V12_002.PositionInfo.cs
Document the IsExternalInit and readonly struct compilation issues and the applied hotfix.	
Add a brain doc describing the root cause (use of init and readonly struct on .NET 4.8), the exact compiler errors, and the fix steps.
Include before/after code snippets and verification notes to show no logic or allocation-impact changes.
Capture prevention steps and future protocol for staying within C# 7.3 feature set for NinjaTrader 8 targets.
docs/brain/pr_13_hotfix_isexternalinit.md
Tips and commands
greptile-apps[bot]
greptile-apps Bot reviewed 37 minutes ago
greptile-apps Bot
left a comment
backtothefutures83-oss has reached the 50-review limit for trial accounts. To continue receiving code reviews, upgrade your plan.

@coderabbitai
coderabbitai Bot
commented
37 minutes ago
• 
Review Change Stack

Walkthrough
PendingStopReplacement struct is converted from a readonly struct with init-only properties to a mutable struct with set-assignable properties. All eight properties change from get; init; to get; set;, and the struct declaration removes the readonly modifier.

Changes
Struct Mutability Conversion

Layer / File(s)	Summary
PendingStopReplacement mutability conversion
src/V12_002.PositionInfo.cs	PendingStopReplacement removes the readonly modifier and converts all property accessors from get; init; to get; set; for eight properties (EntryName, Quantity, StopPrice, Direction, OldOrder, CreatedTime, CapturedTargets, BracketRestorationNeeded). The comment is updated to reflect the change for heap-allocation avoidance.
Estimated code review effort
🎯 2 (Simple) | ⏱️ ~8 minutes

🚥 Pre-merge checks | ✅ 5
✨ Finishing Touches
Comment @coderabbitai help to get the list of available commands and usage tips.

@codacy-production
codacy-production Bot
commented
36 minutes ago
Up to standards ✅
🟢 Issues 0 issues
🟢 Metrics 0 complexity · 0 duplication
AI Reviewer: first review requested successfully. AI can make mistakes. Always validate suggestions.

Run reviewer

TIP This summary will be updated as you push new changes.

@github-actions
github-actions Bot
commented
36 minutes ago
Failed to generate code suggestions for PR

amazon-q-developer[bot]
amazon-q-developer Bot reviewed 36 minutes ago
amazon-q-developer Bot
left a comment
Review Summary
This PR successfully addresses the .NET Framework 4.8 compatibility issue by replacing C# 9.0 language features with C# 7.3 equivalents. The code changes are correct and necessary for the target platform.

Critical Finding
Documentation inaccuracy: The impact analysis incorrectly claims "no runtime behavior difference" between init and set accessors. The removal of readonly + init→set change makes the struct mutable after construction, which differs significantly from the immutable behavior of readonly struct with init accessors.
Recommendation
Address the documentation error to ensure developers understand the behavioral change. The code fix itself is appropriate for the .NET Framework 4.8 target environment.

You can now have the agent implement changes and create commits directly on your pull request's source branch. Simply comment with /q followed by your request in natural language to ask the agent to make changes.

docs/brain/pr_13_hotfix_isexternalinit.md
- ✅ CSharpier Format - PASS

## Impact Analysis
**Zero Logic Impact**: The change from `init` to `set` has no runtime behavior difference for `readonly struct`. Both prevent modification after construction. The struct is only instantiated via object initializers in `V12_002.Trailing.StopUpdate.cs`, which works identically with both accessor types.
@amazon-q-developer
amazon-q-developer Bot
36 minutes ago
🛑 Documentation Error: The claim "no runtime behavior difference" between init and set is incorrect. Unlike init accessors (which only allow setting during object initialization), set accessors permit modification after construction. This means PendingStopReplacement instances can now be mutated post-initialization, creating potential for bugs if the struct is accidentally modified elsewhere in the codebase. Update the documentation to accurately reflect this behavioral change and note that the code must rely on discipline rather than compiler enforcement to prevent post-construction modification.

@backtothefutures83-oss	Reply...
@codeant-ai codeant-ai Bot added the size:M label 36 minutes ago
@gitar-bot
gitar-bot Bot
commented
36 minutes ago
• 
CI failed: The CI build failed due to a policy violation where source code changes and documentation files were submitted in the same pull request.
Code Review ✅ Approved
Tip

Comment Gitar fix CI or enable auto-apply: gitar auto-apply:on

Options
Was this helpful? React with 👍 / 👎 | Gitar

gemini-code-assist[bot]
gemini-code-assist Bot reviewed 36 minutes ago
gemini-code-assist Bot
left a comment
Code Review
This pull request resolves .NET Framework 4.8 compilation errors (CS0518 and CS8341) by converting the PendingStopReplacement struct from a readonly struct with C# 9.0 init properties to a mutable struct with C# 7.3 set properties, alongside adding documentation for this hotfix. The reviewer raised a valid concern that changing to a mutable struct introduces risks of silent copy-by-value mutation bugs, and suggested adding a warning comment to alert developers against mutating retrieved copies.

Important

The consumer version of Gemini Code Assist on GitHub is being sunset. Starting June 18, 2026, new organization installations will be blocked, and all code review activity will officially cease on July 17, 2026.
For more details on the timeline and next steps, please review the Help Documentation.

src/V12_002.PositionInfo.cs
Comment on lines +406 to +407
        // V12 Round 11: Converted to struct to eliminate heap allocation in hot path (Jane Street principle)
        private struct PendingStopReplacement
@gemini-code-assist
gemini-code-assist Bot
36 minutes ago
medium

Converting PendingStopReplacement from a readonly struct to a mutable struct with { get; set; } properties introduces a risk of silent copy-by-value mutation bugs. In C#, when a mutable struct is retrieved from a collection (like ConcurrentDictionary) or passed to a method, it is copied by value. Modifying any property on the retrieved struct (e.g., pending.Quantity = 10;) will only modify the local copy, leaving the original instance in the dictionary unchanged. To mitigate this risk and prevent future developers from accidentally mutating copies, consider adding a clear warning comment above the struct definition.

        // V12 Round 11: Converted to struct to eliminate heap allocation in hot path (Jane Street principle)
        // WARNING: This is a mutable struct. Copy-by-value semantics apply. Do NOT mutate properties directly
        // after retrieving from collections; instead, re-insert a new instance to update.
        private struct PendingStopReplacement
@backtothefutures83-oss	Reply...
codacy-production[bot]
codacy-production Bot reviewed 36 minutes ago
codacy-production Bot
left a comment
Pull Request Overview
The PR successfully addresses the C# 9.0 compatibility issues (CS0518 and CS8341) for .NET Framework 4.8 by removing init accessors and the readonly struct modifier. However, while the code is 'Up to Standards' according to Codacy, the implementation introduces two significant risks: mutability pitfalls and performance degradation.

Changing the struct to be mutable and removing the readonly modifier increases the risk of 'copy-by-value' bugs. Furthermore, the lack of IEquatable<T> on this struct means any equality comparison (common in hot paths) will result in boxing and reflection, contradicting the zero-allocation objective. The documentation also contains a contradiction regarding the runtime behavior of the modified struct. It is recommended to refactor the struct to use a constructor for initialization, allowing it to remain readonly in C# 7.3.

About this PR
This hotfix lacks automated unit or integration tests to verify that the accessor changes and the switch to a mutable struct do not alter runtime behavior or object initialization logic.
The PR documentation mentions a Roslyn analyzer rule to enforce C# 7.3, but no .csproj changes or linting configurations were added. Without automated enforcement, there is a high risk of future regressions where C# 8+ features are reintroduced.
Test suggestions
 Verify successful compilation in a .NET Framework 4.8 environment
 Verify zero-allocation behavior using the AMAL harness
 Verify that 'set' accessors correctly support object initializer syntax used in Trailing.StopUpdate.cs
 Verify that equality checks on PendingStopReplacement do not cause heap allocations/boxing
Prompt proposal for missing tests
TIP Improve review quality by adding custom instructions
TIP How was this review? Give us feedback

src/V12_002.PositionInfo.cs
        // V12 Round 11: Converted to readonly struct to eliminate heap allocation in hot path (Jane Street principle)
        private readonly struct PendingStopReplacement
        // V12 Round 11: Converted to struct to eliminate heap allocation in hot path (Jane Street principle)
        private struct PendingStopReplacement
@codacy-production
codacy-production Bot
36 minutes ago
🔴 HIGH RISK

This struct modification introduces two critical issues for a 'hot path' component:

Mutability Risk: Removing readonly and using set accessors makes this a mutable struct. In C#, it is easy to accidentally modify a copy of a struct rather than the instance. To maintain immutability in C# 7.3, use a constructor for initialization and keep properties as get-only.
Performance (Zero-Allocation): Without implementing IEquatable<PendingStopReplacement>, the runtime uses reflection-based equality and boxing. This negates the zero-allocation performance benefits intended by using a struct.
Recommended Action: Implement IEquatable<PendingStopReplacement> and refactor to a readonly struct using a constructor for initialization.

See Issue in Codacy

@backtothefutures83-oss	Reply...
docs/brain/pr_13_hotfix_isexternalinit.md
- ✅ CSharpier Format - PASS

## Impact Analysis
**Zero Logic Impact**: The change from `init` to `set` has no runtime behavior difference for `readonly struct`. Both prevent modification after construction. The struct is only instantiated via object initializers in `V12_002.Trailing.StopUpdate.cs`, which works identically with both accessor types.
@codacy-production
codacy-production Bot
36 minutes ago
⚪ LOW RISK

Nitpick: This section claims there is no runtime behavior difference for a readonly struct, but the implementation actually removes the readonly modifier. A mutable struct has significantly different runtime risks (copy-by-value mutations) compared to an immutable one.

@backtothefutures83-oss	Reply...
sourcery-ai[bot]
sourcery-ai Bot reviewed 35 minutes ago
sourcery-ai Bot
left a comment
Hey - I've left some high level feedback:

Removing the readonly modifier from PendingStopReplacement changes its semantics (mutable value type); if possible, consider retaining immutability (e.g., with a constructor and readonly fields, or a shim IsExternalInit type for .NET 4.8) to preserve the original design intent while fixing compilation.
The new brain doc states that switching from init to set has no runtime behavior difference for a readonly struct, but the struct is no longer readonly; it would be good to update that analysis to reflect the current, mutable struct behavior.
Prompt for AI Agents
Sourcery is free for open source - if you like our reviews please consider sharing them ✨
Help me be more useful! Please click 👍 or 👎 on each comment and I'll use the feedback to improve your reviews.
codescene-delta-analysis[bot]
codescene-delta-analysis Bot approved these changes 35 minutes ago
codescene-delta-analysis Bot
left a comment
Our agent can fix these. Install it.

Gates Passed
 6 Quality Gates Passed

Quality Gate Profile: Pay Down Tech Debt
Install CodeScene MCP: safeguard and uplift AI-generated code. Catch issues early with our IDE extension and CLI tool.

@sonarqubecloud
sonarqubecloud Bot
commented
35 minutes ago
Quality Gate Passed Quality Gate passed
Issues
 0 New issues
 0 Accepted issues

Measures
 0 Security Hotspots
 0.0% Coverage on New Code
 0.0% Duplication on New Code

See analysis details on SonarQube Cloud

@codeant-ai
codeant-ai Bot
commented
35 minutes ago
CodeAnt AI finished reviewing your PR.

coderabbitai[bot]
coderabbitai Bot requested changes 33 minutes ago
coderabbitai Bot
left a comment
Actionable comments posted: 1

🤖 Prompt for all review comments with AI agents
🪄 Autofix (Beta)
ℹ️ Review info
src/V12_002.PositionInfo.cs
Comment on lines +407 to 425
        private struct PendingStopReplacement
        {
            public string EntryName { get; init; }
            public string EntryName { get; set; }

            public int Quantity { get; init; }
            public int Quantity { get; set; }

            public double StopPrice { get; init; }
            public double StopPrice { get; set; }

            public MarketPosition Direction { get; init; }
            public MarketPosition Direction { get; set; }

            public Order OldOrder { get; init; } // Track the old order being cancelled
            public Order OldOrder { get; set; } // Track the old order being cancelled

            public DateTime CreatedTime { get; init; } // V8.30: Timeout support - clean up stale replacements
            public DateTime CreatedTime { get; set; } // V8.30: Timeout support - clean up stale replacements

            // Build 950: Bracket restoration -- populated before stop cancel is sent.
            public TargetSnapshot[] CapturedTargets { get; init; } // null if no Working targets at cancel time
            public TargetSnapshot[] CapturedTargets { get; set; } // null if no Working targets at cancel time

            public bool BracketRestorationNeeded { get; init; } // true when CapturedTargets is non-null
            public bool BracketRestorationNeeded { get; set; } // true when CapturedTargets is non-null
        }
@coderabbitai
coderabbitai Bot
33 minutes ago
🧹 Nitpick | 🔵 Trivial | ⚡ Quick win

🧩 Analysis chain
Remove the silent-mutation-loss concern: callers already reassign whole PendingStopReplacement values
Dropping readonly/init to set fixes the compile-time constraints, and the update logic around pendingStopReplacements builds a new PendingStopReplacement and writes it back via indexer assignment or AddOrUpdate return factories—no in-place mutation of a retrieved struct value is used, so the latent copy-mutate-discard bug class is not evident here.
Optional: if immutability is still desired, adding an IsExternalInit polyfill to retain readonly + init would be a cleaner approach than mutable setters.

🤖 Prompt for AI Agents