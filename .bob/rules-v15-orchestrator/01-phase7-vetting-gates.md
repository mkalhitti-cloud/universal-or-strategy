# Phase 7 Concurrency Hardening (V15.4 Protocol)

When executing Phase 7 refactoring tasks, you must adhere to the V15.4 Recursive Protocol.

## The Vetting Pipeline
You are an autonomous orchestrator. Do not ask for permission to run these gates. Run them and report the results. If a gate fails, you must attempt to fix the code and re-run the gate before completing your task.

1. **AMAL Vetting Gate (`python scripts/amal_harness.py`)**
   - **Requirement**: Must output `Allocated = 0 B`.
   - **Action**: Any C# hot-path refactoring (SPSC, MPMC, atomic primitives) MUST be passed through the AMAL harness to prove it is zero-allocation.

2. **ASCII Integrity Gate (`python check_ascii.py`)**
   - **Requirement**: No non-ASCII characters in `src/`.
   - **Action**: We do not allow emoji or curly quotes in NinjaScript strings.

3. **Lock-Free Verification (`grep -r "lock(" src/`)**
   - **Requirement**: Zero matches.
   - **Action**: The legacy `lock(stateLock)` is STRICTLY BANNED. Confirm that none were accidentally reintroduced.

4. **Hard-Link Synchronization (`powershell -File .\deploy-sync.ps1`)**
   - **Requirement**: Must be run successfully after ANY file in `src/` is edited.
   - **Action**: Editor file-saving breaks hard-links to the NinjaTrader directories. This script re-establishes them. This is the **final step** before marking a task complete.

## Output Formatting
When running in non-interactive mode, clearly state:
- Gate 1 (AMAL): PASS/FAIL
- Gate 2 (ASCII): PASS/FAIL
- Gate 3 (Lock-Free): PASS/FAIL
- Gate 4 (Sync): PASS/FAIL
If any gate fails, output the error and self-correct.
