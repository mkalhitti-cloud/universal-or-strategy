# PTT-BUGS-LIVE-0911 Lane E — Plan Review
## Phase 2: ptt-plan-reviewer

**Reviewer**: ptt-plan-reviewer  
**Date**: 2026-09-11  
**Plan reviewed**: `docs/brain/PTT-BUGS-LIVE-0911/02-architecture-plan-lane-e.md`  
**Rules source**: `docs/standards/RULES_CATALOG.md`  
**Lane D Section 11 (wiring reference)**: `docs/brain/PTT-BUGS-LIVE-0911/02-architecture-plan-lane-d.md#section-11`

---

## Final Verdict: REVIEW_FAIL

**1 violation found.** The plan must be revised and resubmitted before Phase 3 is unlocked.

---

## Per-Check Verdicts

### R-1: Lane-Split Gate Compliance — PASS

- "LANE-SPLIT GATE RESULT: SINGLE-PIPELINE" present in Section 2. ✅
- Q1=YES (same method / within 50 lines). Q2=YES (single fix, no Fix A/B split). Both YES → SINGLE-PIPELINE is the correct result. ✅
- Gate logic is internally consistent. ✅

---

### R-2: CYC Budget — FAIL

**VIOLATION V-01 (JS-013, P1):**

The plan adds the following line to **both** `CancelQxBrackets` overloads (Sections 11.3 and 11.4):

```csharp
_laneEDispatched.TryRemove(acc.Name + "#LANE-E#" + (instr?.FullName ?? string.Empty), out _);
```

The plan claims this is "+0 CYC impact (method call, not a branch)" in both Section 11.3, Section 11.4, and Appendix B.

**This claim is incorrect.**

Per RULES_CATALOG.md JS-013 (McCabe counting standard):

> "Add +1 for each: `if`, `else if`, `case`, `while`, `for`, `foreach`, `catch`, `?:`, `??`, `&&`, `||`."

The expression `instr?.FullName ?? string.Empty` contains the `??` null-coalescing operator, which is **explicitly listed as +1 CYC** in the authoritative counting standard.

**Impact per overload:**

| Overload | Documented CYC Before | `??` Adds | CYC After | Budget | Status |
|---|---|---|---|---|---|
| `CancelQxBrackets` 2-param | 4 | +1 | 5 | ≤ 8 | PASS |
| `CancelQxBrackets` 3-param | **8** | +1 | **9** | ≤ 8 | **FAIL** |

The 3-param overload is **already at the maximum CYC=8**. Adding any decision-point expression — including a `??` operator — exceeds the limit. CYC=9 is a hard P1 failure.

**Cited rule**: JS-013 (P1) — CYC > 8 per method.  
**Plan location**: Section 11.4 ("CYC impact: +0"), Appendix B ("Adding `TryRemove` is a method call (McCabe: +0). Final CYC=8. ✓").

**Correction required**: The architect must either:
1. Replace `(instr?.FullName ?? string.Empty)` with a pre-computed local variable assigned BEFORE the branch count reaches 8 (local variable assignment is not a decision point), **or**
2. Extract a private helper `GetLaneEDedupKey(Account acc, Instrument instr)` that returns the pre-built key string, and call `TryRemove(GetLaneEDedupKey(acc, instr), out _)` — a helper call adds 0 CYC to the caller.

---

### R-3: Thread Safety — PASS

- `_laneEDispatched` declared as `ConcurrentDictionary<string, byte>(StringComparer.Ordinal)`. ✅
- No `lock()`, `Monitor`, `Mutex`, or `SemaphoreSlim` anywhere in the plan. ✅
- No WPF element mutations in new code paths. `StatusUpdate?.Invoke` is inside the existing `SendCopyWithAtm` (unchanged). ✅

---

### R-4: Gate Integrity — PASS

- `TrySetFollowerSlateReady`: plan states "logic unchanged, signature unchanged" (Section 1 and Section 8). ✅
- `TryMarkFollowerSlateNotReady`: plan states "logic unchanged, signature unchanged" (Section 1 and Section 8). ✅
- `_followerSlateReady` semantics unchanged (Section 1): Lane E reads via `TryGetValue` and writes the identical `[acc.Name] = true` indexer pattern already used by existing code. ✅
- Lane B `FindFollowerBracketOrder` gate check (line 4118) unaffected. Section 8 gate state machine trace is correct. ✅

---

### R-5: Non-Clone No-Op — PASS

- `TryLaneEHook` branch (1) is the **first guard**: `if (GetCopyMode() != CopyMode.Clone) return;` ✅
- Section 10 provides explicit confirmation matrix for Signal/Mirror/Market modes (all no-op). ✅
- Zero state reads, dictionary lookups, or allocations on non-Clone paths. ✅

---

### R-6: Double-Arm Prevention — PASS

- `_laneEDispatched` (`ConcurrentDictionary<string, byte>`) defined in Section 11.1. ✅
- Dedup key: `acc.Name + "#LANE-E#" + instrFullName` — uniquely identifies the (account, instrument) pair. Separator `#LANE-E#` cannot appear in NT8 account names or instrument names (Section 7). ✅
- Key committed via `TryAdd` on successful `SendCopyWithAtm` (Section 11.6, Branch D). ✅
- Key reset via `TryRemove` in both `CancelQxBrackets` overloads (Sections 11.3 and 11.4). ✅
- Test T_LANE_E_06 covers double-fire prevention. ✅
- Test T_LANE_E_07 covers dedup reset after CancelQxBrackets. ✅

---

### R-7: NT8 API Correctness — PASS

- `StartAtmStrategy`: static form only, delegated entirely to existing `SendCopyWithAtm` (Section 3.4, Appendix A). ✅
- Order name "Entry": hardcoded inside `SendCopyWithAtm` (Section 3.4). ✅
- `AtmStrategyCreate`: explicitly BANNED in Appendix A, not used anywhere. ✅
- `AtmStrategyChangeStopTarget`: explicitly BANNED in Appendix A, not used anywhere. ✅
- No `async`/`await` in any method signature in the plan. ✅
- Event handler `OnFollowerPositionUpdate` is synchronous `void`. ✅

---

### R-8: JS Rule Compliance — PASS (with V-01 noted above)

| Rule | Check | Result |
|---|---|---|
| JS-021 | No `lock()`, `Monitor`, `Mutex`, `SemaphoreSlim` | PASS — ConcurrentDictionary throughout |
| JS-001 | No `throw` in dispatch/event handlers | PASS — all paths use early `return` or `return false`; `SendCopyWithAtm` wraps in its own `try/catch` |
| JS-002 | No unexpected null return | PASS — `TryLaneEHook` is `void`; `TryLaneEDispatchFollower` returns `bool`, never null |
| JS-023 | No WPF update off-thread without Dispatcher | PASS — no WPF element writes in new code; `StatusUpdate?.Invoke` is inside existing `SendCopyWithAtm` |
| JS-025 | ConcurrentDictionary for shared state | PASS — `_laneEDispatched` uses `ConcurrentDictionary<string, byte>` |
| ASCII-001 | ASCII-only | PASS — all string literals use ASCII characters; key separator `#LANE-E#` is ASCII |
| JS-013 | CYC ≤ 8 | **FAIL — see V-01** (`CancelQxBrackets` 3-param reaches CYC=9) |

---

### R-9: 7-Scan Checklist — PASS

Section 13 contains the complete 7-scan table.

| Scan | Present | Required Command/Method |
|---|---|---|
| SCAN-01 | ✅ | `grep -rn "lock("` — 0 matches |
| SCAN-02 | ✅ | Manual McCabe: OnFollowerPositionUpdate=3, TryLaneEHook=7, TryLaneEDispatchFollower=4 |
| SCAN-03 | ✅ | `grep -n "async\|await"` — 0 matches |
| SCAN-04 | ✅ | Non-ASCII byte scan — 0 bytes > 127 |
| SCAN-05 | ✅ | `grep -n "AtmStrategyCreate\|AtmStrategyChangeStopTarget"` — 0 matches |
| SCAN-06 | ✅ | `powershell -File .\scripts\build_readiness.ps1` — 0 errors, 0 warnings, 0 test failures |
| SCAN-07 | ✅ | Structural: wiring, field declaration, CancelQxBrackets presence, non-Clone guard, gate integrity |

All 7 scans present. ✅

**Note for engineer**: After V-01 is resolved, SCAN-02 for `CancelQxBrackets` 3-param must report CYC ≤ 8 (whatever value results from the corrected implementation). The 7-scan table in the revised plan must be updated to reflect the corrected CYC value.

---

### R-10: Test Strategy — PASS

10 concrete xUnit `[Fact]` test names specified in Section 12 covering all required scenarios:

| Scenario | Test(s) |
|---|---|
| Non-Clone no-op | T_LANE_E_01 |
| Gate already open (not post-QX/BE) | T_LANE_E_02 |
| Zero quantity (flat/post-full-exit) | T_LANE_E_03 |
| No ATM template configured | T_LANE_E_04 |
| Correct dispatch (Clone + gate closed + position + template) | T_LANE_E_05 |
| Double-arm prevention (second position event) | T_LANE_E_06 |
| Dedup reset on CancelQxBrackets | T_LANE_E_07 |
| Null account guard | T_LANE_E_08 |
| Daily cap exceeded | T_LANE_E_09 |
| Structural wiring confirmation | T_LANE_E_10 |

All required coverage areas satisfied. ✅

---

### R-11: Deferred Items — PASS

- **DW-LD-03**: Section 15 — "Status: CLOSED by this plan." Explicitly closed with full rationale. ✅
- **DW-PTT0911B-01**: Section 15 — "Status: ADDRESSED as post-compile SIM validation step." Full 8-step SIM verification protocol specified with expected NT8 Output tab messages and regression scenarios. ✅

---

### R-12: Spec Requirement Traceability — PASS

Section 14 contains a full traceability table with 15 rows mapping spec requirements to plan sections and JS rule confirmations. All spec requirements are accounted for with explicit CLOSED/CONFIRMED status. ✅

---

## Violation Summary

| ID | Rule | Severity | Location in Plan | Description |
|---|---|---|---|---|
| V-01 | JS-013 | P1 | Sections 11.3, 11.4, Appendix B | `CancelQxBrackets` 3-param (CYC=8 before) gains +1 CYC from `??` operator in `instr?.FullName ?? string.Empty`. Reaches CYC=9. Plan incorrectly claims "+0 CYC impact." |

---

## Required Correction for V-01

The architect must resolve the `??` operator CYC overrun in `CancelQxBrackets` 3-param.

**Recommended fix (Option A — pre-computed local, zero branches):**
```csharp
// In CancelQxBrackets 3-param, BEFORE any existing branches:
string laneEKey = acc.Name + "#LANE-E#" + (instr == null ? string.Empty : instr.FullName);
// ... existing body ...
TryMarkFollowerSlateNotReady(acc);
_laneEDispatched.TryRemove(laneEKey, out _);
```
Wait — `? :` is also +1 CYC. This does not help.

**Correct fix (Option B — extracted helper, zero CYC to caller):**

Extract a `private static string BuildLaneEKey(Account acc, Instrument instr)` helper that builds the key. A void/value helper call adds 0 CYC to the caller.

```csharp
// New private static helper (CYC=2: entry + 1 null branch):
// ASCII-only. No lock (JS-021).
private static string BuildLaneEKey(Account acc, Instrument instr)
{
    return acc.Name + "#LANE-E#" + (instr != null ? instr.FullName : string.Empty);
}

// In CancelQxBrackets 2-param and 3-param:
TryMarkFollowerSlateNotReady(acc);
_laneEDispatched.TryRemove(BuildLaneEKey(acc, instr), out _);
```

This adds 0 CYC to either `CancelQxBrackets` overload and satisfies JS-013. The helper itself has CYC=2 (well within budget).

**Alternative fix (Option C — guarantee instr is non-null at call sites):**

If the architect can confirm that `instr` is always non-null at both `CancelQxBrackets` call sites, the `?.` and `??` operators are unnecessary and can be removed. Replace with `instr.FullName` directly (0 CYC delta). The plan would need to document the non-null invariant.

---

## Decision

**REVIEW_FAIL**

One violation (V-01, JS-013, P1): `CancelQxBrackets` 3-param reaches CYC=9 after the `??` operator in the `TryRemove` argument expression. Plan incorrectly documents this as "+0 CYC impact."

All other checks (R-1, R-3 through R-12) pass without issue. The architecture is fundamentally sound. The violation is narrow and isolated to the key-building expression in `CancelQxBrackets`. Correction via an extracted `BuildLaneEKey` helper (Option B above) or confirmed non-null instr (Option C) will resolve the violation.

The plan is **not approved for Phase 3** until V-01 is corrected and re-submitted for Phase 2 review.

---

# Phase 2 Cycle 2 — Plan Review

**Reviewer**: ptt-plan-reviewer  
**Date**: 2026-09-11  
**Cycle**: 2 (re-review after V-01 fix)  
**Plan reviewed**: `docs/brain/PTT-BUGS-LIVE-0911/02-architecture-plan-lane-e.md` (Status: PLAN_COMPLETE Cycle 2)  
**Rules source**: `docs/standards/RULES_CATALOG.md`

---

## Final Verdict: REVIEW_FAIL

**1 new violation found (V-02).** V-01 is confirmed fixed. The plan must be revised and resubmitted before Phase 3 is unlocked.

---

## V-01 Fix Confirmation

**V-01 (JS-013) — FIXED ✅**

The architect applied Option B from the Cycle 1 correction guidance: extracted `private static string BuildLaneEKey(Account acc, NinjaTrader.Cbi.Instrument instr)` as a new helper method (Section 11.7, Appendix B).

- `BuildLaneEKey` returns `acc.Name + "#LANE-E#" + (instr != null ? instr.FullName : string.Empty)` — CYC=2 (entry + 1 ternary). ✅
- Both `CancelQxBrackets` overloads now call `_laneEDispatched.TryRemove(BuildLaneEKey(acc, instr), out _)` — a plain method call, +0 CYC to callers. ✅
- `CancelQxBrackets` 2-param: CYC=4 (unchanged). ✅
- `CancelQxBrackets` 3-param: CYC=8 (unchanged). ✅
- SCAN-07 structural checklist updated to include `BuildLaneEKey` verification item. ✅
- Appendix B rewritten to correctly document the helper extraction. ✅

V-01 is fully resolved.

---

## New Violation Found in Cycle 2

### V-02 (JS-013, P1): `TryLaneEHook` — CYC understated; actual CYC=10, exceeds limit of 8

**Location in plan:** Section 9 (`TryLaneEHook` McCabe Branch-Count table), Section 11.5 (full method body).

**The plan claims CYC=7.** The authoritative JS-013 counting standard states:

> "Add +1 for each: `if`, `else if`, `case`, `while`, `for`, `foreach`, `catch`, `?:`, `??`, `&&`, `||`."

`||` is explicitly an independent +1 decision point. The plan's McCabe table counts each `if (...||...)` compound guard as a single "+1" — this is incorrect. Each `||` inside a guard condition must be counted separately.

**Correct McCabe count for `TryLaneEHook` (from method body in Section 11.5):**

```
method entry                                                    +1  (baseline)
(1)  if (GetCopyMode() != CopyMode.Clone)                       +1  [if]
(2)  if (pos == null || pos.Instrument == null)                 +1  [if]  +1  [||]
(3)  if (pos.Quantity <= 0)                                     +1  [if]
(4)  if (!_followerSlateReady.TryGetValue(...) || ready)        +1  [if]  +1  [||]
(5)  if (namedMode == null)                                     +1  [if]
(6)  if (_laneEDispatched.ContainsKey(key))                     +1  [if]
     if (ok)                                                    +1  [if]   ← UNLISTED in plan's table
                                                         Total = 10  CYC > 8  FAIL
```

**Two separate counting errors in the plan:**

| Error | Description | Missed CYC |
|---|---|---|
| Error A | Guard (2): `||` in `pos == null \|\| pos.Instrument == null` counted as zero (+0 instead of +1) | +1 |
| Error B | Guard (4): `||` in `!TryGetValue(...) \|\| ready` counted as zero (+0 instead of +1) | +1 |
| Error C | Terminal `if (ok)` gate-open check (last branch in method body) absent from count table entirely | +1 |

Summing errors: plan claims 7, correct total is 7 + 3 = **10**. CYC=10 > 8. Hard P1 failure.

**Cited rule:** JS-013 (P1) — CYC > 8 per method.  
**Plan locations:** Section 9 McCabe table for `TryLaneEHook` ("Total = 7 CYC ≤ 8 ✓"), Section 11.5 comment `// CYC=7`, Section 11.5 closing note "CYC = 7 (entry + 6 branches). Within budget."

**Correction required:** `TryLaneEHook` must be split or simplified so that no single method exceeds CYC=8.

**Required correction options:**

**Option A — Null-guard consolidation (reduce errors A and C):**  
Extract the two compound null guards into a single `IsValidLaneEPosition(pos)` private helper method. That helper contains the `||` operators (adding to its own CYC), and `TryLaneEHook` calls it as a single expression-body check. This eliminates the `||` charges from `TryLaneEHook`.

Example:
```csharp
// New helper (CYC=3: entry + null(1) + ||(1) + qty(1)):
private static bool IsValidLaneEPosition(NinjaTrader.Cbi.Position pos)
{
    return pos != null && pos.Instrument != null && pos.Quantity > 0;
}

// TryLaneEHook: replace guards (2) and (3) with one call:
if (!IsValidLaneEPosition(pos))
    return;
```

After this extraction, `TryLaneEHook` loses 3 decision points (guard-2-if, guard-2-||, guard-3-if) and replaces them with 1 (single if). Net delta: -2. Recounted CYC = 10 - 2 = 8. Also eliminates Error C if `if (ok)` was always present but counted — must verify final count is ≤ 8.

**Option B — Extract dispatch + gate-open into a single inline helper:**  
The terminal `if (ok)` gate-open is a one-liner that can be folded into `TryLaneEDispatchFollower` by having it return a status and having the caller open the gate inside, or by having the dispatch helper open the gate itself (passing `_followerSlateReady` reference or using a callback). This removes the terminal `if (ok)` from `TryLaneEHook` (-1 CYC).

**Minimum viable fix:** Apply Option A (extract null-guard helper). The resulting `TryLaneEHook` will have at most CYC=8 if `if (ok)` remains and the `||` in guard (4) is still present. The architect must re-count after any restructuring and confirm CYC ≤ 8 with all `||`, `&&`, `?:`, `??` counted.

---

## Per-Check Verdicts (Cycle 2)

### R-1: Lane-Split Gate — PASS (unchanged)

No change from Cycle 1. "LANE-SPLIT GATE RESULT: SINGLE-PIPELINE" present. ✅

---

### R-2: CYC Budget — FAIL

**V-02 (JS-013, P1):** `TryLaneEHook` actual CYC=10. See violation detail above.

| Method | Claimed CYC | Correct CYC | Budget | Status |
|---|---|---|---|---|
| `OnFollowerPositionUpdate` | 3 | 3 | ≤ 8 | PASS |
| `TryLaneEHook` | 7 | **10** | ≤ 8 | **FAIL** |
| `TryLaneEDispatchFollower` | 4 | 4 | ≤ 8 | PASS |
| `BuildLaneEKey` | 2 | 2 | ≤ 8 | PASS |
| `CancelQxBrackets` 2-param | 4 | 4 | ≤ 8 | PASS (V-01 fixed) |
| `CancelQxBrackets` 3-param | 8 | 8 | ≤ 8 | PASS (V-01 fixed) |

---

### R-3: Thread Safety — PASS (unchanged)

`_laneEDispatched` is `ConcurrentDictionary<string, byte>(StringComparer.Ordinal)`. No `lock()`, `Monitor`, `Mutex`, `SemaphoreSlim`. No off-thread WPF writes. ✅

---

### R-4: Gate Integrity — PASS (unchanged)

`TrySetFollowerSlateReady` and `TryMarkFollowerSlateNotReady` signatures and bodies are documented as byte-for-byte unchanged. Gate state machine trace in Section 8 is correct. ✅

---

### R-5: Non-Clone No-Op — PASS (unchanged)

Branch (1) is the first guard in `TryLaneEHook`. Section 10 confirmation matrix present. ✅

---

### R-6: Double-Arm Prevention — PASS (unchanged)

`_laneEDispatched` dedup, `TryAdd` on success, `TryRemove` via `BuildLaneEKey` in both `CancelQxBrackets` overloads. Tests T_LANE_E_06 and T_LANE_E_07. ✅

---

### R-7: NT8 API Correctness — PASS (unchanged)

`StartAtmStrategy` static form only, delegated to `SendCopyWithAtm`. `AtmStrategyCreate` and `AtmStrategyChangeStopTarget` banned and not used. Order name "Entry" hardcoded. No `async`/`await`. ✅

---

### R-8: JS Rule Compliance — FAIL (V-02 propagates)

| Rule | Result |
|---|---|
| JS-001 no throw | PASS |
| JS-002 no null return | PASS |
| JS-003 no magic string discriminated state | PASS |
| JS-009 no plain Dictionary for shared state | PASS |
| JS-021 no lock | PASS |
| JS-023 no WPF off-thread | PASS |
| JS-025 ConcurrentDictionary | PASS |
| ASCII-001 ASCII-only | PASS |
| JS-013 CYC ≤ 8 | **FAIL — V-02** (`TryLaneEHook` CYC=10) |

---

### R-9: 7-Scan Checklist — PASS (structurally)

Section 13 contains all 7 scans. SCAN-07 now includes `BuildLaneEKey` structural verification. ✅

**Note for engineer:** SCAN-02 in Section 13 documents `TryLaneEHook` CYC=7 — this value is incorrect (actual CYC=10 per V-02). After V-02 is resolved, SCAN-02 must report the corrected post-fix CYC value for `TryLaneEHook` (must be ≤ 8).

---

### R-10: Test Strategy — PASS (unchanged)

10 concrete xUnit `[Fact]` tests covering all required scenarios. ✅

---

### R-11: Deferred Items — PASS (unchanged)

DW-LD-03 CLOSED by this plan. DW-PTT0911B-01 ADDRESSED with full 8-step SIM protocol. ✅

---

### R-12: Spec Requirement Traceability — PASS (unchanged)

Section 14: 15-row traceability table, all requirements CLOSED/CONFIRMED. ✅

---

## Violation Summary (Cycle 2)

| ID | Rule | Severity | Location in Plan | Description |
|---|---|---|---|---|
| V-01 | JS-013 | P1 | (Cycle 1) | ~~`CancelQxBrackets` 3-param reached CYC=9 from `??` operator.~~ **FIXED in Cycle 2.** |
| V-02 | JS-013 | P1 | Section 9 table, Section 11.5 comment/closing note | `TryLaneEHook` actual CYC=10. Plan claims CYC=7. Two `||` operators in guards (2) and (4) are uncounted, and the terminal `if (ok)` branch is absent from the count table. CYC exceeds the ≤ 8 limit. |

---

## Required Correction for V-02

The architect must restructure `TryLaneEHook` so its McCabe CYC ≤ 8 under the authoritative JS-013 counting standard (every `if`, `||`, `&&`, `?:`, `??` = +1 each, independently).

**Minimum fix:** Extract the two compound null guards (guard 2: `pos == null || pos.Instrument == null`) and/or the compound gate check (guard 4: `!TryGetValue(...) || ready`) into a private helper method, moving the `||` operators out of `TryLaneEHook`. This reduces `TryLaneEHook`'s CYC by at least 2, bringing it to 8 or below.

The updated plan MUST include a re-counted McCabe table for `TryLaneEHook` that explicitly enumerates every `if`, `||`, and `?:` as separate +1 items. The claimed total must be ≤ 8.

The SCAN-02 entry in Section 13 must also be updated to reflect the corrected CYC.

---

## Decision

**REVIEW_FAIL**

One violation (V-02, JS-013, P1): `TryLaneEHook` actual McCabe CYC=10, exceeding the hard limit of 8. The plan's count table omits the `||` operators in guards (2) and (4) and the terminal `if (ok)` branch.

V-01 is fully resolved. All other checks (R-1, R-3 through R-12) pass without issue. The architecture remains fundamentally sound. V-02 is also narrow and isolated — it requires restructuring `TryLaneEHook` to reduce its CYC to ≤ 8. No other methods are affected.

The plan is **not approved for Phase 3** until V-02 is corrected and resubmitted for Phase 2 Cycle 3 review.

---

# Phase 2 Cycle 3 — Plan Review

**Reviewer**: ptt-plan-reviewer  
**Date**: 2026-09-11  
**Cycle**: 3 (FINAL — force-pass only if genuinely clean; max 2 cycles already exceeded)  
**Plan reviewed**: `docs/brain/PTT-BUGS-LIVE-0911/02-architecture-plan-lane-e.md` (Status: PLAN_COMPLETE Cycle 3)  
**Rules source**: `docs/standards/RULES_CATALOG.md`

---

## V-01 and V-02 Fix Confirmation

**V-01 (JS-013) — FIXED ✅** (confirmed from Cycle 2, unchanged)

`BuildLaneEKey` private static helper extracts the null-coalescing guard. Both `CancelQxBrackets` overloads call `BuildLaneEKey(acc, instr)` — a plain method call (+0 CYC to callers). `CancelQxBrackets` 2-param remains CYC=4. `CancelQxBrackets` 3-param remains CYC=8. Appendix B confirms this.

**V-02 (JS-013) — FIXED ✅**

The architect extracted `IsLaneEEligible(Account acc, NinjaTrader.Cbi.Position pos)` as a new `private bool` helper (Section 11.5a). All compound boolean guards (`||` operators) have been moved out of `TryLaneEHook` and into this helper.

---

## Independent CYC Recount (mandatory per Cycle 3 instructions)

### `TryLaneEHook` — Reviewer's independent count

Source: Section 11.5 method body.

```
method entry                                                  +1 (baseline)
if (!IsLaneEEligible(acc, pos))                              +1  [if]
if (_laneEDispatched.ContainsKey(key))                       +1  [if]
if (ok)                                                      +1  [if]
No ||, &&, ?:, ?? anywhere in TryLaneEHook body              +0
                                                    Total  =  4  CYC
```

**Plan's stated CYC = 5.** Reviewer's independent count = **4**. There is a 1-point arithmetic discrepancy: the plan's branch table enumerates exactly 4 decision points (entry + 3 ifs) yet labels the sum as 5. The discrepancy is in the plan's arithmetic, not in the presence of any hidden decision point.

**Ruling**: Either value (4 or 5) is strictly ≤ 8. The overclaim of +1 by the plan is conservative (self-penalising) and does not create a false budget-pass scenario. CYC budget is satisfied. No FAIL.

### `IsLaneEEligible` — Reviewer's independent count

Source: Section 11.5a method body.

```
method entry                                                           +1 (baseline)
(1)  if (GetCopyMode() != CopyMode.Clone)                              +1  [if]
(2)  if (pos == null || pos.Instrument == null)       if               +1  [if]
     ||                                                                +1  [||]
(3)  if (pos.Quantity <= 0)                                            +1  [if]
(4)  if (!_followerSlateReady.TryGetValue(...) || ready)  if           +1  [if]
     ||                                                                +1  [||]
(5)  if (!(GetCloneAtmMode(pos.Instrument.FullName) is FollowerAtmMode.Named))  +1  [if]
No &&, ?:, ?? anywhere in IsLaneEEligible body                         +0
                                                                Total = 8  CYC
```

**Plan's stated CYC = 8.** Reviewer's independent count = **8**. Counts agree exactly. CYC=8 ≤ 8. ✅

---

## Per-Check Verdicts (Cycle 3 — full re-verification from scratch)

### R-1: Lane-Split Gate Compliance — PASS

"LANE-SPLIT GATE RESULT: SINGLE-PIPELINE" present in Section 2. Q1=YES, Q2=YES. Gate logic is internally consistent. No change from prior cycles. ✅

---

### R-2: CYC Budget — PASS

All methods verified independently:

| Method | Plan Claim | Reviewer Count | Budget | Status |
|---|---|---|---|---|
| `OnFollowerPositionUpdate` | CYC=3 | 3 (entry + 2 ifs) | ≤ 8 | PASS |
| `TryLaneEHook` | CYC=5 | 4 (conservative: plan overclaims +1) | ≤ 8 | PASS |
| `IsLaneEEligible` | CYC=8 | **8** (independently verified) | ≤ 8 | PASS ✅ |
| `TryLaneEDispatchFollower` | CYC=4 | 4 (entry + 3 ifs: A, B, C) | ≤ 8 | PASS |
| `BuildLaneEKey` | CYC=2 | 2 (entry + 1 ternary) | ≤ 8 | PASS |
| `CancelQxBrackets` 2-param | CYC=4 | 4 (V-01 fixed) | ≤ 8 | PASS |
| `CancelQxBrackets` 3-param | CYC=8 | 8 (V-01 fixed) | ≤ 8 | PASS |

Note on `TryLaneEDispatchFollower` Section 11.6 comment: the comment says "CYC=4: (A) acc null guard, (B) daily cap check, (C) SendCopyWithAtm result, (D) TryAdd." But Branch (D) is `_laneEDispatched.TryAdd(dedupKey, 0)` inside an `if (ok)` block. The `if (ok)` is Branch (C), and `TryAdd` is a call — no additional branch. The closing note "CYC = 4 (entry + 3 branches + 1 for TryAdd conditional)" is inconsistent: the `if (ok)` block IS Branch C; `TryAdd` itself is just a call (+0). Actual: entry(1) + if(A)(1) + if(B)(1) + if(ok)=if(C)(1) = CYC=4. The count=4 is correct regardless of the note's imprecise wording. ✅

No method exceeds CYC=8. V-02 is fully resolved. **R-2: PASS.**

---

### R-3: Thread Safety — PASS

- `_laneEDispatched` declared as `ConcurrentDictionary<string, byte>(StringComparer.Ordinal)`. ✅
- No `lock()`, `Monitor`, `Mutex`, or `SemaphoreSlim` anywhere in new or modified code. ✅
- No WPF element mutations in new code paths. `StatusUpdate?.Invoke` is inside the existing `SendCopyWithAtm` (unchanged). ✅
- `_followerSlateReady[acc.Name] = true` write in `TryLaneEHook`: `_followerSlateReady` is a `ConcurrentDictionary` (confirmed by existing usage pattern in Section 1 / Section 8). The indexer write is atomic for `ConcurrentDictionary` (equivalent to `AddOrUpdate`). ✅

---

### R-4: Gate Integrity — PASS

- `TrySetFollowerSlateReady`: Section 1 "logic unchanged, signature unchanged." Section 8 gate machine confirms call sequence. ✅
- `TryMarkFollowerSlateNotReady`: Section 1 "logic unchanged, signature unchanged." ✅
- `_followerSlateReady` semantics unchanged. ✅
- Lane B `FindFollowerBracketOrder` gate check (line 4118) unaffected. Gate opened by Lane E via `_followerSlateReady[acc.Name] = true` after successful re-arm. ✅
- SCAN-07 structural checklist includes byte-for-byte gate method integrity verification. ✅

---

### R-5: Non-Clone No-Op — PASS

- `IsLaneEEligible` check (1) is `if (GetCopyMode() != CopyMode.Clone) return false;` — this is the FIRST guard inside `IsLaneEEligible`, which is itself called as the FIRST statement in `TryLaneEHook`. Non-Clone path executes: `IsLaneEEligible` → one `GetCopyMode()` call + compare + `return false` → `TryLaneEHook` `if (result)` returns. Zero state reads, zero allocations, zero dictionary lookups. ✅
- Section 10 confirmation matrix covers Clone/Signal/Mirror/Market modes explicitly. ✅

---

### R-6: Double-Arm Prevention — PASS

- `_laneEDispatched` (`ConcurrentDictionary<string, byte>`) field in Section 11.1. ✅
- Dedup key: `acc.Name + "#LANE-E#" + instrFullName`. Separator uniqueness argument documented in Section 7. ✅
- Key committed via `TryAdd` on successful dispatch (Section 11.6, inside `if (ok)` block). ✅
- Key reset via `TryRemove(BuildLaneEKey(acc, instr), out _)` in both `CancelQxBrackets` overloads (Sections 11.3 and 11.4). ✅
- Tests T_LANE_E_06 (double-fire prevention) and T_LANE_E_07 (dedup reset) present. ✅

---

### R-7: NT8 API Correctness — PASS

- `StartAtmStrategy`: static form only, delegated entirely to existing `SendCopyWithAtm`. Section 3.4 and Appendix A confirm. ✅
- Order name "Entry": hardcoded inside `SendCopyWithAtm`. ✅
- `AtmStrategyCreate`: explicitly BANNED in Appendix A, not used anywhere in new code. ✅
- `AtmStrategyChangeStopTarget`: explicitly BANNED in Appendix A, not used anywhere. ✅
- No `async`/`await` in any new method signature. ✅
- `OnFollowerPositionUpdate` remains synchronous `void`. ✅
- SCAN-05 entry in Section 13 covers AtmStrategyCreate and AtmStrategyChangeStopTarget. ✅

---

### R-8: JS Rule Compliance — PASS

| Rule | Check | Result |
|---|---|---|
| JS-001 | No `throw` in dispatch/event handlers | PASS — all paths use early `return` or `return false`; `SendCopyWithAtm` wraps in its own `try/catch` |
| JS-002 | No unexpected null return | PASS — `TryLaneEHook` void; `IsLaneEEligible` returns `bool`; `TryLaneEDispatchFollower` returns `bool`; `BuildLaneEKey` returns `string` (never null: string concatenation) |
| JS-003 | No magic string for discriminated state | PASS — key string is a dictionary key, not a state discriminator |
| JS-008 | No mutable struct fields / unfrozen brushes | PASS — no struct fields or brushes introduced |
| JS-009 | No plain `Dictionary` for shared state | PASS — `ConcurrentDictionary` throughout |
| JS-010 | No public constructor on singleton/signal struct | PASS — no new constructors introduced |
| JS-013 | CYC ≤ 8 | PASS — all methods verified above |
| JS-021 | No `lock()`, `Monitor`, `Mutex`, `SemaphoreSlim` | PASS |
| JS-023 | No WPF update off-thread without Dispatcher | PASS |
| JS-025 | ConcurrentDictionary for shared state | PASS |
| JS-100 | No magic numbers | PASS — no numeric literals with domain meaning in new code |
| ASCII-001 | ASCII-only | PASS — all new string literals use ASCII; `#LANE-E#` separator is ASCII |
| NT8-001 | No `async`/`await` in event handlers | PASS |
| NT8-002 | No `Account.All` in constructor | PASS — not applicable |
| NT8-007 | `StartAtmStrategy` static form only | PASS — delegated to `SendCopyWithAtm` |
| NT8-008 | `AtmStrategyCreate` banned | PASS — not used |
| NT8-009 | `AtmStrategyChangeStopTarget` banned | PASS — not used |
| SCAN-01 | No `lock(` | PASS |
| SCAN-03 | No `async`/`await` | PASS |
| SCAN-04 | No non-ASCII bytes | PASS |
| SCAN-05 | No `AtmStrategyCreate`/`AtmStrategyChangeStopTarget` calls | PASS |

No violations found.

---

### R-9: 7-Scan Checklist — PASS

Section 13 contains all 7 scans. Updated from Cycle 2 to include:
- `IsLaneEEligible` in SCAN-02 McCabe entries. ✅
- `BuildLaneEKey` structural verification in SCAN-07. ✅
- `IsLaneEEligible` structural verification in SCAN-07 (declared `private bool`, called as first guard in `TryLaneEHook`). ✅
- SCAN-02 now documents corrected CYC values: `TryLaneEHook`=5 (plan claim), `IsLaneEEligible`=8. ✅

All 7 scans present with required commands and expected results. ✅

---

### R-10: Test Strategy — PASS

10 concrete xUnit `[Fact]` tests specified in Section 12. All required scenarios covered (non-Clone no-op, gate open, zero qty, no template, correct dispatch, double-arm prevention, dedup reset, null account, daily cap, structural wiring). No existing tests modified. ✅

---

### R-11: Deferred Items — PASS

- **DW-LD-03**: Section 15 — "Status: CLOSED by this plan." ✅
- **DW-PTT0911B-01**: Section 15 — "Status: ADDRESSED as post-compile SIM validation step." Full 8-step SIM verification protocol specified. ✅

---

### R-12: Spec Requirement Traceability — PASS

Section 14: 15-row traceability table. All requirements map to explicit plan sections with CLOSED/CONFIRMED status. NT8 "PTT-" prefix exception for ATM entry orders (uses "Entry" per NT8-007) is documented and correct. ✅

---

## Violation Summary (Cycle 3)

| ID | Rule | Severity | Location | Description |
|---|---|---|---|---|
| V-01 | JS-013 | P1 | ~~Cycle 1~~ | FIXED in Cycle 2. |
| V-02 | JS-013 | P1 | ~~Cycle 2~~ | FIXED in Cycle 3. `IsLaneEEligible` extracted; CYC independently verified = 8. |

**Zero new violations found in Cycle 3.**

---

## Final Verdict: REVIEW_PASS

All 12 checks pass. Both prior violations (V-01, V-02) are confirmed resolved. No new violations found in Cycle 3. The architecture is sound, all Jane Street DNA rules are satisfied, all NT8 API constraints are met, CYC ≤ 8 for every method, and spec coverage is complete.

**Phase 3 is UNLOCKED.** The plan is approved for ticket generation by `ptt-architect`.
