# Verify G-01+G-02 -- DateTime.UtcNow unification across trailing cluster
# Branch: wave7/pr20-deferred-repairs
# Findings: G-01 (Greptile P1 -- circuit breaker frozen), G-02 (Greptile P1 -- latency mixing)

## Fix Description
Standardized all DateTime comparisons in the trailing/stop-sync cluster to use
DateTime.UtcNow, eliminating the clock-mixing bug where circuitBreakerActivatedTime
was stamped with UtcNow but compared against DateTime.Now in the consumer (always
negative on machines west of UTC -- circuit breaker would never reset).

## Verification

### Code Changes

File: src/V12_002.Trailing.cs
- Line 215: `DateTime now = DateTime.Now;` -> `DateTime now = DateTime.UtcNow;`
  Fixes G-01: ManageTrail_AdaptiveThrottleTick now uses consistent UTC for all
  comparisons including circuit breaker check at line 246.

File: src/V12_002.Trailing.StopUpdate.cs
- Line 39: `DateTime now = DateTime.Now;` -> `DateTime now = DateTime.UtcNow;`
  CleanupStalePendingReplacements uses UTC; consistent with CreatedTime at line 176.
- Line 96: `DateTime.Now - existingPending.CreatedTime` -> `DateTime.UtcNow - ...`
  Fixes G-02: UpdateStopOrder stale-pending age calculation uses UTC.
- Line 142: same fix in HandleStalePendingReplacement
- Line 316: `CreatedTime = DateTime.Now` -> `CreatedTime = DateTime.UtcNow`
  Fixes G-02: CreateNewPendingForEmergencyStop now stamps CreatedTime consistently
  with the UpdateExistingPendingReplacement path (line 176 already used UtcNow).

Remaining DateTime.Now at line 393 (`DateTime.Now.Ticks % 100000000`) is for order
name suffix uniqueness -- not a time comparison, not a bug.

### Gates
- dotnet build Linting.csproj: 0 errors, 0 warnings -- PASS
- wave7_prepush_gate.py: GATE PASSED (6/6 checks)
  Check 2 (DateTime.Now none introduced): PASS -- we removed DateTime.Now, did not add any
- lock() check: none found -- PASS
- ASCII check: PASS

### Commit
SHA: 7c9221dd
Message: fix(wave7/pr20-deferred): G-01+G-02 -- unify DateTime.UtcNow across trailing cluster (circuit breaker + CreatedTime)
Files: src/V12_002.Trailing.cs, src/V12_002.Trailing.StopUpdate.cs

### OKF Alignment
- Rule 3 (FSM determinism): "All time comparisons must use the SAME clock source (UTC only)"
  All 5 DateTime.Now -> DateTime.UtcNow changes enforce this rule.
- Production impact: G-01 fix means circuit breaker now correctly resets after 2 seconds
  on any machine regardless of timezone. G-02 fix means stale-pending detection latency
  (5s threshold at line 44, lines 96/142) is accurate regardless of UTC offset.

verification_verdict: PASS
fix_confirmed: true
build_passed: true
gate_passed: true
