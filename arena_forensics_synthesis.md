# Arena Forensics Synthesis: ADR-019 Sovereign Substrate Repair

## 1. Executive Summary

After analyzing the forensic data across multiple React/Vite dashboards (`kimi`, `claude`, `chatgpt`) submitted by the Red Team, the current implementation plan for ADR-019 must be **REJECTED**. The proposed plan contains critical structural flaws ("Type 2" errors) where the newly inserted `_isTerminating` early-return guards will bypass essential resource cleanup (semaphore releases, dictionary removals, flag resets), leading to permanent reservation leaks and deadlocks in the V12 codebase.

## 2. Critical Blockers (Type 2 Errors)

The Red Team forensics independently flagged the following 7 sites as structurally unsafe under the current plan. These sites place shared-resource cleanup operations either _after_ the guard or exclusively in `catch` blocks that will not be executed if the lambda short-circuits.

- **Site #5 (AccountOrders.cs:369 - HandleMatchedFollowerOrder)**:
  - **Evidence**: `_followerReplaceSpecs.TryRemove(sigName, out _)` is executed after the primary workload. The current plan inserts the `_isTerminating` guard before this cleanup.
  - **Impact**: Permanent FSM spec leak. The entry never gets removed, creating a permanent reservation leak.
- **Site #11 (REAPER.Audit.cs:136 - AuditAccountState)**:
  - **Evidence**: `_repairInFlight.TryRemove(repairKey, out _)` is currently only in the `catch` block. The plan puts the early-return guard inside the try block's lambda. If the lambda returns early, the primary work is skipped, the generic `catch` is not triggered, and the in-flight flag remains permanently set.
  - **Impact**: Permanent lockout of the reaper repair queue.
- **Sites #12, #13, #14, #15 (REAPER.Audit.cs)**:
  - **Evidence**: Sister sites to #11 handling `ProcessReaperFlattenQueue` and `ProcessReaperNakedStopQueue`.
  - **Impact**: Same permanent lockout/leak issues due to bypassed in-flight cleanup.
- **Site #16 (SIMA.Dispatch.cs:60 - ExecuteSmartDispatchEntry)**:
  - **Evidence**: Deferred dispatch retry handling semaphore contention.
  - **Impact**: Semaphore release would be bypassed, leading to permanent semaphore contention/leaks.

## 3. Verification & CI Hurdles

- **Hardcoded POSIX Checks**: Section F verification steps contain 13/17 tests written for POSIX environments (e.g., using `grep`, `test -f`). These will fail seamlessly without PowerShell equivalents specified.
- **Environment Incompatibilities**: Test #14 requires NinjaTrader installed, rendering it non-automatable on a Linux CI matrix.
- **Linting project Path**: Utilizing `$(UserProfile)` in HintPath is consistent. While it fails on Linux CI (resolving to `$HOME`), NinjaTrader dependencies are skipped in Linux CI anyway, making it an acceptable tradeoff as verified by Kimi.

## 4. Source Tooling Clarification

- Forensic dashboards confirm that the repo-canonical tool for ASCII purity is `check_ascii.py` at the repo root.
- The `byte_purge.py` referenced in the OLD path substitution scripts does not exist in the repository. The proposed plan successfully points to the correct script.

## 5. Architectural Recommendation

**NO-GO**. Do not execute the existing implementation plan. Provide an updated architectural design (rewrite `implementation_plan.md`) that explicitly addresses resource cleanup ordering (e.g. by wrapping the cleanup in `finally` blocks, or placing the early-return guards subsequent to resource release).
