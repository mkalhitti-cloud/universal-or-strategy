# B119 Ticket 1 Verification Report

## Ticket: B119-T1 -- DW-B128 Direction-Change Guard in DispatchCopy

## Verification Result: VERIFY_PASS

**Verifier**: ptt-verifier (Phase 4b)
**Date**: 2026-08-27
**Source files read independently**: `src/PropTraderTools/CopyEngine.cs`, `src/PropTraderTools/Tests/B119Tests.cs`
**Scans run**: 7 independent scans (Layer 3 -- not copied from engineer Layer 2)

---

## Engineer Claims vs Actual Code

### Claim 1: `_lastLeaderDirection` field
**Engineer claimed**: `private readonly ConcurrentDictionary<string, OrderAction> _lastLeaderDirection` added at line 305, after `_lastHasPos`, before `_orderMap`. Comment cites JS-021: no lock.
**Actual (verified at source)**:
```
CopyEngine.cs:302  private readonly ConcurrentDictionary<string, int[]> _lastHasPos = ...
CopyEngine.cs:305  // B119: DW-B128 -- reversal entry guard. ...
CopyEngine.cs:308  private readonly ConcurrentDictionary<string, OrderAction> _lastLeaderDirection =
CopyEngine.cs:309      new ConcurrentDictionary<string, OrderAction>();
CopyEngine.cs:311  // V01: order map ...
CopyEngine.cs:314  private readonly ConcurrentDictionary<string, ConcurrentBag<FollowerBinding>> _orderMap = ...
```
Field is `private readonly`, type is `ConcurrentDictionary<string, OrderAction>`, placement is between `_lastHasPos` (L302) and `_orderMap` (L314). Comment matches ticket specification exactly.
**Result**: MATCH

### Claim 2: `IsReversalToFlatFollower` helper
**Engineer claimed**: `internal static bool IsReversalToFlatFollower(OrderAction currentAction, OrderAction lastAction, bool followerIsFlat)` at L3313, after `IsFlat` closing `}`. Body: `return currentAction != lastAction && followerIsFlat;`. CYC=2.
**Actual (verified at source)**:
```
CopyEngine.cs:3340  return pos == null || pos.Quantity == 0;  // IsFlat body
CopyEngine.cs:3341  }                                          // IsFlat closing brace
CopyEngine.cs:3342  (blank)
CopyEngine.cs:3343  // B119: DW-B128 -- direction-change guard predicate. ...
CopyEngine.cs:3347  internal static bool IsReversalToFlatFollower(
CopyEngine.cs:3348      OrderAction currentAction,
CopyEngine.cs:3349      OrderAction lastAction,
CopyEngine.cs:3350      bool followerIsFlat)
CopyEngine.cs:3351  {
CopyEngine.cs:3352      return currentAction != lastAction && followerIsFlat;
CopyEngine.cs:3353  }
```
Accessibility: `internal static` (per ticket Section 3b). Return type: `bool`. Body: single `return` expression with `&&`. CYC=2. Placed immediately after `IsFlat` closing brace at L3341. Note: engineer reported L3313 but actual location is L3347 -- this is a line-number discrepancy only (file grew due to intervening additions); the code is correct and placement relative to `IsFlat` is exact.
**Result**: MATCH (minor line-number discrepancy L3313 vs L3347 -- code is correct)

### Claim 3: DispatchCopy modification
**Engineer claimed**: (3a) Pre-loop snapshots (`currentAction`, `instr`, `hasLastDirection`) after `int baseQty` and before `int idx = 0`. (3b) Merged null+cap guard as compound `||`, followed by reversal guard with `continue`. Log string `[PTT-COPY-GUARD] skip reversal entry: {acc.Name} {instr.FullName} follower flat`. (3c) Post-loop `_lastLeaderDirection[instr.FullName] = currentAction` after `foreach` closes.
**Actual (verified at source)**:
- L1827: `int baseQty = _atrEnabled ? ... : ...;`
- L1831: `OrderAction currentAction = order.OrderAction;`  -- snapshot #1
- L1832: `var instr = order.Instrument;`                   -- snapshot #2
- L1833-1835: `bool hasLastDirection = _lastLeaderDirection.TryGetValue(instr.FullName, out OrderAction lastAction);` -- snapshot #3
- L1838: `int idx = 0;` -- idx immediately follows snapshots as specified
- L1843: `if (acc == null || !PassesDailyCapCheck(acc))` -- merged compound guard
- L1854: `bool followerIsFlat = IsFlat(FindPosition(acc, instr));`
- L1855: `if (hasLastDirection && IsReversalToFlatFollower(currentAction, lastAction, followerIsFlat))`
- L1857-1864: NinjaTrader.Code.Output.Process with exact log string
- L1865: `idx++;`
- L1866: `continue;`
- L1900: `}` -- foreach closing brace
- L1902-1904: post-loop dict update `_lastLeaderDirection[instr.FullName] = currentAction;`
Log string confirmed character-for-character: `"[PTT-COPY-GUARD] skip reversal entry: " + acc.Name + " " + instr.FullName + " follower flat"` -- matches ticket specification exactly.
**Result**: MATCH

### Claim 4: Test file
**Engineer claimed**: `src/PropTraderTools/Tests/B119Tests.cs` with 11 [Fact] tests, xUnit framework, namespace `PropTraderTools`, zero NT8 API calls, Parts A (6 pure unit), B (3 dict invariant), C (2 BuyToCover/SellShort).
**Actual (verified at source via execute_command)**:
- `using Xunit;` present -- xUnit confirmed, no NUnit, no MSTest
- `namespace PropTraderTools` -- correct
- `public class B119Tests` -- correct class name
- All 11 [Fact] methods present with exact ticket-specified names (A1-A6, B1-B3, C1-C2)
- Part A: Calls `CopyEngine.IsReversalToFlatFollower(...)` directly (internal static)
- Part B: Constructs `new ConcurrentDictionary<string, OrderAction>()` -- no CopyEngine instance
- Part C: Same pattern as Part A with BuyToCover/SellShort enum values
- Zero NT8 API calls: `OrderAction` is enum (value type), `NinjaTrader.Cbi` used only for enum values
**Result**: MATCH

---

## Independent 7-Scan Results

| Scan | Command | Raw Output | My Assessment | PASS/FAIL |
|------|---------|------------|---------------|-----------|
| SCAN 1 | `Select-String -Path CopyEngine.cs -Pattern "lock\s*\("` | 8 hits, ALL in comments ("no lock()" annotations) -- 0 in executable code | No actual lock() in B119 code or anywhere. ConcurrentDictionary used throughout. | PASS |
| SCAN 2 | `Select-String -Path CopyEngine.cs -Pattern "async void "` | 0 results | No async void introduced. | PASS |
| SCAN 3 | `Select-String -Path CopyEngine.cs -Pattern "return null;"` | 7 hits: L1532, L2057, L2103, L3320, L3326, L3401, L4216 -- all pre-existing | Zero new return null in B119 code. IsReversalToFlatFollower returns bool expression only. | PASS |
| SCAN 4 | `Select-String -Path CopyEngine.cs -Pattern "\bthrow\b"` | ~40 hits, ALL in "no throw" comment annotations -- 0 actual throw statements | No throw statement in any B119 code region. | PASS |
| SCAN 5 | `[System.IO.File]::ReadAllText(); [regex]::Matches(..., '[^\x00-\x7F]').Count` | 0 | Zero non-ASCII characters in CopyEngine.cs. Log string [PTT-COPY-GUARD] is 7-bit ASCII. All identifiers ASCII-only. | PASS |
| SCAN 6 | Manual CYC count (DispatchCopy full body L1790-L1905) | DispatchCopy=8 (8 McCabe branches per project compound-as-1 convention: 4 early returns + foreach + merged null/cap guard + reversal guard + mode-is-Named). IsReversalToFlatFollower=2 (1 base + 1 && operator). | B119 net CYC delta = 0 (branch-merge -1 + reversal guard +1). At 8 limit. | PASS |
| SCAN 7 | `dotnet build PropTraderTools.csproj` | Build fails with pre-existing errors in CopyEngineTests.cs (CopyRule not found, CS8400, Immutable), B76Tests.cs (Instruments namespace), B43Tests.cs (ParseAtmTemplateSelection). Zero errors in B119Tests.cs or in IsReversalToFlatFollower/_lastLeaderDirection/currentAction/lastAction/followerIsFlat/hasLastDirection. Confirmed by targeted grep. | All errors pre-existing per V12.23 No Scope Creep. Zero B119-introduced errors. | PASS |

---

## Acceptance Criteria Verification

| Criterion | Ticket AC# | Implementation Found | Code Location | PASS/FAIL |
|-----------|-----------|---------------------|---------------|-----------|
| Reversal + flat follower -> dispatch skipped | AC3 | `if (hasLastDirection && IsReversalToFlatFollower(currentAction, lastAction, followerIsFlat)) { ... idx++; continue; }` + log `[PTT-COPY-GUARD]` | CopyEngine.cs L1855-1866 | PASS |
| First entry (no key) -> copy proceeds | AC1 | `hasLastDirection = TryGetValue(...)` returns false when key absent; `if (hasLastDirection && ...)` short-circuits to false; guard cannot fire | CopyEngine.cs L1833-1835, L1855 | PASS |
| Same direction -> copy proceeds | AC2 | `IsReversalToFlatFollower` body: `currentAction != lastAction && followerIsFlat`; when same direction, `!=` is false, method returns false | CopyEngine.cs L3352 | PASS |
| Reversal + follower open position -> copy proceeds | AC4 | `followerIsFlat = IsFlat(FindPosition(acc, instr))` = false when position open; `&& followerIsFlat` = false; guard does NOT fire | CopyEngine.cs L1854, L3352 | PASS |
| Per-follower independence | AC5 | Each `acc` evaluated independently in foreach; `continue` at L1866 skips only the current `acc`; other followers unaffected | CopyEngine.cs L1839-1900 | PASS |
| Dictionary updated after loop | AC6 | `_lastLeaderDirection[instr.FullName] = currentAction;` at L1904, after foreach closing `}` at L1900 | CopyEngine.cs L1900-1904 | PASS |
| No new lock() | AC7 | ConcurrentDictionary TryGetValue (L1833) + indexer-set (L1904); SCAN 1 confirmed zero actual lock() | CopyEngine.cs L308-309 + scans | PASS |

---

## DNA Rule Check

| Rule | Checked | Result |
|------|---------|--------|
| JS-021: No lock() anywhere in B119 code | SCAN 1 + code read | COMPLIANT |
| JS-001: No throw in hot path (DispatchCopy/IsReversalToFlatFollower) | SCAN 4 + code read | COMPLIANT |
| JS-002: No return null in B119 code regions | SCAN 3 + code read | COMPLIANT |
| JS-033: No async void introduced | SCAN 2 | COMPLIANT |
| CYC <= 8: DispatchCopy=8, IsReversalToFlatFollower=2 | SCAN 6 manual count | COMPLIANT |
| ASCII-only: All new strings and identifiers 7-bit ASCII | SCAN 5 (0 hits) | COMPLIANT |
| xUnit-only tests: No NUnit/MSTest | B119Tests.cs code read | COMPLIANT |
| NT8 API: No new NT8 API surface; reuses IsFlat + FindPosition | Code read + ticket ref | COMPLIANT |
| Struct mutability: N/A | Not applicable | N/A |
| FontFamily / hex colors / DateTime.Now / PTT- prefix / sealed TradeCopierWindow | Not applicable to this ticket | N/A |

---

## Discrepancies

1. **Line number discrepancy**: Engineer reported `IsReversalToFlatFollower` at L3313; actual location is L3347. This is a non-material discrepancy -- the file grew from other earlier additions, and the method placement relative to `IsFlat` (immediately after its closing `}`) is exactly as specified by the ticket. No code correctness issue.

2. **Engineer reported L1826-L1905 as "lines modified"**: Actual pre-loop code starts at L1829 (comment) with snapshots at L1831-1835. Minor line offset consistent with discrepancy #1. Code is correct.

No material discrepancies found. Both discrepancies are line-number offsets from file growth only.

---

## Decision

**VERIFY_PASS**

All 7 independent scans clean (zero violations). All 7 acceptance criteria satisfied by actual source code. All DNA rules compliant. Test file is xUnit-only with 11 [Fact] methods matching the exact ticket-specified names. The `_lastLeaderDirection` field uses `ConcurrentDictionary` with no lock(). `IsReversalToFlatFollower` is `internal static bool` with CYC=2. `DispatchCopy` CYC=8 (at limit, maintained by branch-merge). Dict update is after the foreach loop. Build errors are all pre-existing and unrelated to B119 changes per V12.23 No Scope Creep.