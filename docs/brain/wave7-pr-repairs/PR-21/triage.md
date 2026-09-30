# PR #21 Triage — wave7/pr2-s3-ui-ipc
# S3 UI & IPC Cluster
# Phase 7 Lane Orchestrator — Lane L2
# Triage Date: 2026-07-03

## Bot Poll Summary (initial)
- coderabbitai: CHANGES_REQUESTED (11 actionable comments)
- gemini-code-assist: ACTION_REQUIRED (1 high / 4 medium)
- greptile-apps: INFORMATIONAL (trial ended)
- cubic-dev-ai: ACTION_REQUIRED (2 high / 1 medium)
- sourcery-ai: INFORMATIONAL (1 suggestion)
- codeant-ai: ACTION_REQUIRED (2 high / 8 medium)
- amazon-q-developer: INFORMATIONAL (positive overall review)

## Triage Note
All 30 inline comments are anchored to commit a80ae6cc (original CYC reduction).
Commits 2546a835 (REPAIR-04+05) and ffca2a1a (REPAIR-09) post-date all bot reviews.
Bots have NOT re-reviewed the post-repair state. F01/F02/F03 are ALREADY-FIXED.

---

## ALREADY-FIXED (pre-existing repairs, confirmed in source)

### F01 -- ALREADY-FIXED
Gemini/Cubic: `UI.Compliance.cs:347` `this.Account` used instead of fleet account lookup.
FIX: REPAIR-04 in commit 2546a835 -- `Account.All.FirstOrDefault(a => a.Name == acctName) ?? this.Account`

### F02 -- ALREADY-FIXED
CodeAnt/Cubic: `UI.IPC.cs:464` allowlist check after validator (ordering reversed).
FIX: REPAIR-05 in commit 2546a835 -- `IsAllowedIpcAction` at line 455 before `ValidateIpcCommand` at 463.

### F03 -- ALREADY-FIXED
REPAIR-09 in commit ffca2a1a -- DateTime.UtcNow in compliance log throttle.

---

## VALID-LOGIC-BUG

### F09 -- VALID-LOGIC-BUG
**File**: `src/V12_002.UI.Compliance.cs`
**Method**: `TryClearFlatExpectedPosition` (line 838)
**Finding**: `fleetAcct.Positions.FirstOrDefault(p => p.Instrument.FullName == ...)` -- `p.Instrument` can be null during
broker init/disconnect. NullReferenceException possible.
**Source**: Gemini (medium)
**OKF**: production-engineering-billions.md -- staleness_guard / defensive init

### F10 -- VALID-LOGIC-BUG
**File**: `src/V12_002.UI.Compliance.cs`
**Method**: `BuildAccountJsonEntry` (line 949)
**Finding**: Same pattern -- `acct.Positions.FirstOrDefault(p => p.Instrument.FullName == ...)` null dereference on p.Instrument.
**Source**: Gemini (medium)
**OKF**: production-engineering-billions.md -- defensive init

### F11 -- VALID-LOGIC-BUG
**File**: `src/V12_002.UI.IPC.Commands.Fleet.cs`
**Method**: `CancelAll_IsBracketOrder` called with `order.Name` (lines 238, 345)
**Finding**: `order.Name` can be null in NinjaTrader. `oName.StartsWith(...)` throws NullReferenceException.
Method signature is `private static bool CancelAll_IsBracketOrder(string oName)` -- no null guard inside.
**Source**: Gemini (medium)
**OKF**: production-engineering-billions.md -- defensive init

### F12 -- VALID-LOGIC-BUG
**File**: `src/V12_002.UI.IPC.Commands.Fleet.cs`
**Method**: `TryExecuteRmaEntry` (line 503-504)
**Finding**: `stopDist = CalculateATRStopDistance(RMAStopATRMultiplier)` passed directly to `CalculatePositionSize(stopDist)`.
If stopDist <= 0 (ATR lag, zero ATR), division by zero or degenerate position size. Other callers apply a MinimumStop fallback.
**Source**: Gemini (medium)
**OKF**: production-engineering-billions.md -- defense in depth / independent enforcement

### F13 -- VALID-LOGIC-BUG
**File**: `src/V12_002.UI.IPC.Commands.Mode.cs`
**Method**: `SetMode_ActivateModeFlags` (line 138-166)
**Finding**: switch has no `default` branch. If newMode is unknown/garbled, all mode flags are cleared and nothing is set.
The caller still proceeds to `SetMode_HydrateAndPublish`. Silently puts system in undefined mode-flag state.
**Source**: CodeRabbit (major)
**OKF**: how-to-build-an-exchange.md -- FSM determinism / sidecar_lifecycle: unknown commands rejected

---

## VALID-MECHANICAL

### F04 -- VALID-MECHANICAL
**File**: `src/V12_002.IPC.Hardening.cs`
**Method**: `IsActionSqlInjection` (line 363-371)
**Finding**: Returns true on SQL injection match but emits no Print log. All three peer methods
(IsPartsSqlInjection, IsActionPathTraversal, IsPartsPathTraversal) DO emit a detection log. Asymmetric logging.
**Source**: CodeRabbit (minor)

### F05 -- VALID-MECHANICAL
**File**: `src/V12_002.UI.Compliance.cs`
**Method**: `BuildAccountJsonEntry` (line 938)
**Finding**: `int count` parameter is never used inside the method body. Dead parameter.
**Source**: CodeRabbit (trivial)

### F06 -- VALID-MECHANICAL
**File**: `src/V12_002.UI.Compliance.cs`
**Method**: `IsTargetOrderPrefix` (line 590-594)
**Finding**: StartsWith calls without StringComparison.Ordinal. Culture-agnostic intent requires explicit ordinal comparison.
**Source**: Sourcery

### F07 -- VALID-MECHANICAL
**Files**: multiple (IPC.Hardening.cs, Compliance.cs, IPC.Commands.Mode.cs, IPC.Commands.Misc.cs)
**Finding**: Missing braces on single-line if bodies (SA1503 / CSharpier mandate).
Run: dotnet csharpier format src/
**Source**: CodeRabbit (trivial)

---

## HALLUCINATION / INFRA-NOISE

### CodeRabbit Commands.Mode.cs:353 (Breakeven_CalcOffset)
HALLUCINATION: CodeRabbit implies BE_CUSTOM returns for all actions. Source shows correct guard
`if (action == "BE_CUSTOM" && parts.Length >= 2)` -- only executes for BE_CUSTOM with params. Logic is correct.

### Cubic IPC.cs:425 (empty catch around TriggerCustomEvent)
INFRA-NOISE: Pre-existing pattern. Not introduced by this PR's diff (present in base commit).

### CodeAnt IPC REJECT log marker suggestions (x8)
INFRA-NOISE: custom_rule suggestions about log format string ("V12 IPC REJECT" marker presence).
Project-specific convention, not an OKF rule. Low priority -- deferred.

### CodeAnt raw account name in JSON (Compliance.cs:962)
INFRA-NOISE: custom_rule_security suggestion to use "BMad alias placeholders." Project-specific
external-contract detail not defined in OKF. Deferred -- requires product-level decision on alias mapping.

### CodeRabbit SnapshotPool.cs:224 (duplicate logic)
INFRA-NOISE: Informational refactor suggestion. Duplication is in separate update path intentionally.

---

## TRIAGE TOTALS
TRIAGE_DONE PR#21 logic=5 mech=4 dna=0 hall=1 noise=5 fixed=3
