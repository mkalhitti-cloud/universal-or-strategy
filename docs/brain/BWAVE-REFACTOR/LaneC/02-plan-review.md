# BWAVE-REFACTOR LaneC -- Plan Review

**Reviewer**: ptt-plan-reviewer
**Phase**: 2 (Plan Review)
**Status**: REVIEW_FAIL
**Date**: 2026-09-06
**Plan reviewed**: `docs/brain/BWAVE-REFACTOR/LaneC/02-architecture-plan.md`
**Sources read**:
- `C:\WSGTA\ptt-lane-c\docs\brain\BWAVE-REFACTOR\LaneC\02-architecture-plan.md`
- `C:\WSGTA\ptt-lane-c\docs\standards\jane-street\RULES_CATALOG.md`
- `C:\WSGTA\ptt-lane-c\src\PropTraderTools\Features\PttQuickExit.cs`
- `C:\WSGTA\ptt-lane-c\src\PropTraderTools\Features\PttBreakEven.cs`
- `C:\WSGTA\ptt-lane-c\src\PropTraderTools\Features\PttBreakEvenSwap.cs`
- `C:\WSGTA\ptt-lane-c\src\PropTraderTools\Features\PttGlobalQuickExit.cs`
- `C:\WSGTA\ptt-lane-c\src\PropTraderTools\Features\PttCancel.cs`
- `C:\WSGTA\ptt-lane-c\src\PropTraderTools\Features\PttTrim.cs`

---

## Result: REVIEW_FAIL

**Violation count**: 1
**Blocking rule**: JS-002

---

## Violations

| # | Rule ID | Severity | Location in Plan | Description |
|---|---------|----------|-----------------|-------------|
| 1 | JS-002 | P0 (CRITICAL) | §3.1, helper `FindLeaderPosition` | New helper returns `Position` (a reference type) with an implicit `return null` path for the not-found case. This is a null return where a value is expected — prohibited by JS-002. |

### Violation Detail

**JS-002 — `FindLeaderPosition` returns null (§3.1)**

Plan §3.1 proposes:
```
private static Position FindLeaderPosition(Account leader, Instrument instr)  CYC=2
```
This helper wraps the foreach loop that finds the leader's open position. The `Execute` method caller pattern is:
```csharp
Position pos = FindLeaderPosition(leader, instr);
if (pos == null || pos.Quantity == 0) return;
```
There is no nullable-free path: when the account has no position on `instr`, the helper must return `null`. This violates JS-002 (null return where value expected = P0 FAIL).

The spec's own extraction rule §2 item 3 states: "No return null." The plan acknowledges this rule in §2 but violates it in the very first helper it proposes in §3.1.

**Note**: The existing `FindPositionLocal` in `PttBreakEven.cs` also returns null (pre-existing code, out of scope for this review). The issue here is the NEW helper introduced by this plan.

**Remediation options** (for architect — do not fix here):
1. Keep the position-finding loop **inline** in `Execute`. The foreach+if is only 2 CCN points; removing it from the extracted helper budget and keeping it in the caller costs nothing architecturally, since the caller needs the null check anyway.
2. Use `bool` return with an `out Position pos` parameter: `private static bool TryFindLeaderPosition(Account leader, Instrument instr, out Position pos)` — returns false when not found, never returns null.
3. Return a sentinel value (`Position` with `Quantity == 0`) — not applicable here since `Position` is an NT8 type with no controllable constructor.

**Recommended fix**: Option 1 (keep the foreach inline). The foreach+if contributes only 2 CCN points to `Execute`; the remaining 4 helpers (`BuildQxOcoId`, `SubmitStopOrder`, `SubmitTargetOrder`) still get `Execute` to CCN ≤ 8. No new helper, no JS-002 exposure.

---

## Spec Coverage Matrix

| Requirement | Addressed? | Plan Section |
|-------------|-----------|-------------|
| LANE-SPLIT GATE result stated | YES | Header (§LANE-SPLIT GATE RESULT: SINGLE-PIPELINE) |
| All 14 CCN>8 violations have extraction plans | YES | §3.1–§3.14 |
| PttQuickExit::Execute (32→≤8) | YES | §3.1 |
| PttGlobalQuickExit::SnapshotTargetOrders (20→≤8) | YES | §3.2 |
| PttBreakEven::CancelStaleBracketsLocal (16→≤8) | YES | §3.3 |
| PttBreakEven::SubmitBeTargetsLocal (15→≤8) | YES | §3.4 |
| PttBreakEvenSwap::Execute (15→≤8) | YES | §3.5 |
| PttBreakEven::SnapshotTargetsLocal (13→≤8) | YES | §3.6 |
| PttTrim::TrimPositionLocal (13→≤8) | YES | §3.7 |
| PttFlatten::FlattenPositionLocal (13→≤8) | YES | §3.8 |
| PttBreakEven::IsPttQxTarget (12→≤8) | YES | §3.9 |
| PttGlobalQuickExit::WaitForPttBeCancelled (10→≤8) | YES | §3.10 |
| PttCancel::CancelWorkingEntriesLocal (10→≤8) | YES | §3.11 |
| PttGlobalQuickExit::Execute() (9→≤8) | YES | §3.12 |
| PttGlobalQuickExit::CancelPttBeOrders (9→≤8) | YES | §3.13 |
| PttBreakEven::SubmitBeStopLocal (9→≤8) | YES | §3.14 |
| All predicted post-extraction CCN ≤ 8 | YES | §3.1–§3.14 (all show CCN ≤ 8 after) |
| No lock() in new helpers | YES | §2 rule 3, confirmed throughout |
| No async void in new helpers | YES | §2 rule 3, confirmed throughout |
| No return null in new helpers | **PARTIAL** — **FAIL at FindLeaderPosition (§3.1)** | §3.1 |
| No throw in new helpers | YES | §2 rule 3, all helpers use try/catch |
| Public/internal method signatures unchanged | YES | §5 Non-Goals |
| ASCII-only helper names | YES | All 19 proposed names are ASCII-only |
| 1 [Fact] per extracted helper | YES | §4 states "1 structural [Fact] per extracted helper" |
| New test file created | YES | §4 — BwaveRefactorLaneCTests.cs |
| 2-ticket split coherent (no cross-file conflicts) | YES | §4 — C-1 (3 files), C-2 (4 files), disjoint |
| CopyEngine.cs / TradeCopierPanel.cs excluded | YES | §1 Non-Goals, §5 Non-Goals |
| Build + 7-scan + lizard CCN ≤ 8 verification plan | YES | §6 |
| NT8-049 arg order preserved in extracted helpers | YES | Helpers pass through existing CreateOrder calls |
| NT8-007 (CustomOrder)null preserved | YES | Extracted helpers preserve cast |
| NT8-013 DateTime.MaxValue preserved | YES | All extracted submit helpers retain DateTime.MaxValue |
| NT8-014 PTT- signal name prefix preserved | YES | Signal names unchanged in extracted helpers |

---

## Per-Checklist Confirmation

### 1. LANE-SPLIT GATE: PASS
Plan header states `LANE-SPLIT GATE RESULT: SINGLE-PIPELINE` with Q1/Q2 rationale. Gate result present and coherent.

### 2. Every CCN>8 violation covered: PASS
All 14 violations from the spec's lizard scan appear in §3.1–§3.14 with extraction plans. One-to-one match confirmed against source files.

### 3. Post-extraction CCN ≤ 8 for all methods: PASS (plan claims)
All 14 methods are projected to reach CCN ≤ 8 after extraction. Spot-checked against actual source:
- `PttBreakEvenSwap::Execute`: actual source docs it at CYC=8 already; plan's extraction still achieves ≤8.
- `TrimPositionLocal`: actual source docs it at CYC=5 despite lizard=13; plan correctly extracts useLimitOrder branch, projected CCN≤5.
- `SubmitBeStopLocal`: actual source docs it at CYC=3 despite lizard=9; plan's IsInvalidInput extraction projects CCN=7.

The doc-comment CYC vs lizard discrepancy is acknowledged in plan §1 and consistent with lizard counting `&&`, `||`, and ternaries that the original authors did not count.

### 4. P0 Rules (JS-021, JS-033, JS-002, JS-001): **FAIL — JS-002**
- **JS-021** (no lock): PASS — no lock in any proposed helper.
- **JS-033** (no async void): PASS — all helpers are synchronous.
- **JS-002** (no return null): **FAIL** — `FindLeaderPosition` (§3.1) returns `Position`, requiring a null return for the not-found path. See Violation #1 above.
- **JS-001** (no throw in hot paths): PASS — all submit helpers use try/catch, no throw statements proposed.

### 5. NT8 API constraints preserved: PASS
All helpers extract code blocks that already use correct NT8-049 arg order, NT8-007 cast, NT8-013 `DateTime.MaxValue`, and NT8-014 "PTT-" prefix. No API call signatures are modified by the proposed extractions.

### 6. 2-ticket split coherent: PASS
- **C-1**: `PttQuickExit.cs`, `PttGlobalQuickExit.cs`, `PttBreakEven.cs` — disjoint files, no conflicts.
- **C-2**: `PttBreakEvenSwap.cs`, `PttTrim.cs`, `PttFlatten.cs`, `PttCancel.cs` — disjoint files, no conflicts.
- `IsNonTerminalForInstr` is both defined (§3.10) and reused (§3.13) within `PttGlobalQuickExit.cs` — both in C-1. No cross-ticket dependency.

### 7. Test plan ≥1 [Fact] per extracted helper: PASS
Plan §4 commits to "1 structural [Fact] per NEW extracted helper". §3.9 correctly identifies that no new helper is extracted (StartsWith simplification is in-place), so no test is required for that method. Ticket-level enumeration will detail per-helper test stubs.

### 8. ASCII-only helper names: PASS
All 19 proposed helper names verified as ASCII-only: `FindLeaderPosition`, `BuildQxOcoId`, `SubmitStopOrder`, `SubmitTargetOrder`, `IsTargetOrder`, `DeduplicateByPrice`, `IsCancellableState`, `IsStaleOrder`, `SubmitBareStop`, `SubmitBePair`, `SubmitBareStopSwap`, `SubmitSwapPair`, `IsSnapshotEligibleState`, `ResolveOrderParams`, `IsNonTerminalForInstr`, `IsWorkingEntryOrder`, `LogLeaderDiag`, `SafeName`, `IsInvalidInput`.

---

## Additional Observations (Non-Blocking)

These are informational — they do not change the REVIEW_FAIL verdict but the architect should be aware:

1. **§3.1 plan inconsistency re: `&&` vs `||`**: The plan describes the null guard in `SubmitBeStopLocal` (§3.14) as "(1, &&)" but the actual code uses `||` (`acc == null || instr == null`). Both operators count +1 in lizard so the CCN math is unaffected. Mention in tickets to avoid engineer confusion.

2. **§3.11 stateOk over-description**: The plan describes `IsWorkingEntryOrder` as capturing a "5-state" check analogous to `CancelStaleBracketsLocal`. The actual `CancelWorkingEntriesLocal` in `PttCancel.cs` only uses 2 states (`Working || Initialized`) — a simpler extraction. The proposed helper CYC=4 may be a slight overestimate, but this does not affect safety or correctness.

3. **§3.4 `SubmitBareStop` signature**: The 0-targets path in `SubmitBeTargetsLocal` calls `FindPositionLocal(acc, instr)` inside the block to get `barePos.Quantity`. The proposed signature `SubmitBareStop(Account acc, Instrument instr, OrderAction stopDirection, double bePrice)` omits this. Engineer must either include the position lookup inside the helper or add a `qty` parameter. Either is acceptable but must not introduce a new null-returning helper (JS-002).

---

## Action Required

Send plan back to `ptt-architect` for revision.

**Required fix**: Remove `FindLeaderPosition` from §3.1. Replace with either:
- Option A (recommended): Keep the position-finding foreach inline in `Execute`'s guard. The remaining helpers (`BuildQxOcoId`, `SubmitStopOrder`, `SubmitTargetOrder`) achieve the CCN ≤ 8 target without extracting the null-returning finder.
- Option B: Change signature to `private static bool TryFindLeaderPosition(Account leader, Instrument instr, out Position pos)` (returns bool, assigns out param, never returns null).

No other changes required.

---

**REVIEW_FAIL**
