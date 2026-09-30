# PR #22 Forensics Report
Generated: 2026-06-02 12:31:10

## Summary

| Metric | Count |
|--------|-------|
| Total Findings | 8 |
| VALID Issues | 8 |
| HALLUCINATIONS | 0 |
| INFRA-NOISE | 0 |
| P0 (Critical) | 2 |
| P1 (High) | 5 |
| P2 (Medium) |  |

## VALID Issues (Priority Order)

### [P0] CRITICAL - amazon-q-developer
**Source:** review  
**Timestamp:** 2026-06-02T19:20:39Z  
**URL:** https://github.com/backtothefutures83-oss/universal-or-strategy/pull/22

**Excerpt:**
```
## Summary

This PR successfully achieves its complexity reduction goals through well-structured helper extraction. The refactoring demonstrates solid engineering practices with dependency injection, comprehensive logging, and lock-free concurrency patterns.

**Key Achievements:**
- Ô£à 70% complexity reduction (CYC 20ÔåÆ6)
- Ô£à Four helpers extracted with clean DI signatures
- Ô£à Lock-free implementation maintained
- Ô£à Diagnostic logging added for runtime verification
- Ô£à InternalsVisible
```

### [P0] CRITICAL - coderabbitai
**Source:** review  
**Timestamp:** 2026-06-02T19:28:31Z  
**URL:** https://github.com/backtothefutures83-oss/universal-or-strategy/pull/22

**Excerpt:**
```
**Actionable comments posted: 3**

> [!CAUTION]
> Some comments are outside the diff and canÔÇÖt be posted inline due to platform limitations.
> 
> 
> 
> <details>
> <summary>ÔÜá´©Å Outside diff range comments (1)</summary><blockquote>
> 
> <details>
> <summary>src/V12_002.SIMA.Shadow.cs (1)</summary><blockquote>
> 
> `33-57`: _ÔÜá´©Å Potential issue_ | _­ƒö┤ Critical_ | _ÔÜí Quick win_
> 
> **Add the missing `System.Collections.Concurrent` using in `src/V12_002.SIMA.Shadow.cs`**
> `ConcurrentDi
```

### [P1] REVIEW - gemini-code-assist
**Source:** review  
**Timestamp:** 2026-06-02T19:21:27Z  
**URL:** https://github.com/backtothefutures83-oss/universal-or-strategy/pull/22

**Excerpt:**
```
## Code Review

This pull request refactors the `ShadowPropagateStopMoves` method in `src/V12_002.SIMA.Shadow.cs` by extracting several helper methods to reduce cyclomatic complexity and enable unit testing, supported by test project updates. The review feedback correctly identifies that the newly introduced `Print` logging statements in these hot-path helper methods will cause severe log flooding and garbage collection pressure, violating the repository's performance mandate. It is recommended 
```

### [P1] REVIEW - codacy-production
**Source:** review  
**Timestamp:** 2026-06-02T19:26:54Z  
**URL:** https://github.com/backtothefutures83-oss/universal-or-strategy/pull/22

**Excerpt:**
```
### Pull Request Overview

While the PR meets the technical goal of reducing complexity in the main method, it introduces several high-risk issues that should prevent merging in its current state. Most notably, the unit tests are 'placeholders' that assert local constants rather than logic, failing to verify the 17 identified test scenarios. Additionally, the new `src/V12_002.SIMA.Shadow.cs` remains high-risk as the extracted methods themselves are overly complex and lack functional verification
```

### [P1] REVIEW - sourcery-ai
**Source:** review  
**Timestamp:** 2026-06-02T19:21:21Z  
**URL:** https://github.com/backtothefutures83-oss/universal-or-strategy/pull/22

**Excerpt:**
```
Hey - I've found 2 issues, and left some high level feedback:

- The extracted helpers are mostly DI-friendly, but `ValidateLeaderPosition` still reaches into the instance-level `stopOrders` field instead of taking it as a parameter like the other helpers; consider passing `stopOrders` in to keep helper purity and the dependency style consistent across the set.
- All four helpers now call `Print` in the hot path, which could generate a lot of logging under load; consider adding a configurable di
```

### [P1] REVIEW - greptile-apps
**Source:** review  
**Timestamp:** 2026-06-02T19:20:15Z  
**URL:** https://github.com/backtothefutures83-oss/universal-or-strategy/pull/22

**Excerpt:**
```
`backtothefutures83-oss` has reached the 50-review limit for trial accounts. To continue receiving code reviews, [upgrade your plan](https://app.greptile.com/review/github).
```

### [P1] REVIEW - codescene-delta-analysis
**Source:** review  
**Timestamp:** 2026-06-02T19:21:02Z  
**URL:** https://github.com/backtothefutures83-oss/universal-or-strategy/pull/22

**Excerpt:**
```

[//]: # (cs-code-health)

**Gates Failed**
[![](https://codescene.io/imgs/svg/x1.svg)](#) Enforce advisory code health rules `(1 file with Complex Method, Primitive Obsession, Excess Number of Function Arguments)`

Our agent can fix these. [Install it.](https://codescene.io/docs/developer-tools/pr-refactoring-agent.html)

**Gates Passed**
[![](https://codescene.io/imgs/svg/pass1.svg)](#) 5 Quality Gates Passed

<details><summary>Reason for failure</summary>

| Enforce advisory code health rules
```

### [P2] PERFORMANCE - gitar-bot
**Source:** comment  
**Timestamp:** 2026-06-02T19:21:02Z  
**URL:** https://github.com/backtothefutures83-oss/universal-or-strategy/pull/22#issuecomment-4606249887

**Excerpt:**
```
<details>
<summary><b>CI failed</b>: The CI build failed because the PR violates the repository's 'PR_SEPARATION_ENFORCEMENT' policy, which prohibits mixing source code changes with non-source configuration or test project modifications.</summary>

### Overview
This PR failed due to a repository policy enforcement error. The CI logic identified a mix of `src/` and `non-src/` file changes, which triggers an automated rejection as per defined project contribution standards.

### Failures

#### PR 
```

