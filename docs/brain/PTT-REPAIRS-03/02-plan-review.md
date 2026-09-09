# PTT-REPAIRS-03 Plan Review — Cycle 2 (Revision 2 — FINAL)

**Reviewer**: ptt-plan-reviewer
**Block**: PTT-REPAIRS-03
**Plan file**: `docs/brain/PTT-REPAIRS-03/02-architecture-plan.md` (Revision 2 — V-02 fix)
**Source verified**: `src/PropTraderTools/CopyEngine.cs`
**Cycle**: 2 (FINAL — max 2 allowed)
**Date**: 2026-09-08
**Result**: **REVIEW_PASS**

---

## VIOLATIONS

None. Zero violations found across all 10 checklist items.

---

## CHECKLIST RESULTS

### 1. V-01 (DispatchCopy CYC) — PASS

Independent branch count from source lines 2428–2537 (unmodified source, confirmed this cycle):

| Branch | Line | Running CYC |
|--------|------|-------------|
| base | — | 1 |
| `if (IsExitSignalName(order.Name))` | 2431 | 2 |
| `if (!IsDispatchTriggerState(...))` | 2444 | 3 |
| `if (!IsDispatchableOrderType(...))` | 2457 | 4 |
| `if (IsLiveEntryBlocked(...))` | 2474 | 5 |
| `foreach (var acc in rule.FollowerAccounts)` | 2508 | 6 |
| `if (ShouldSkipFollowerDispatch(acc))` | 2510 | 7 |
| `if (ShouldSkipForReversalGuard(...))` | 2516 | **8** |

**Confirmed: current CYC = 8.**

Post-T2 arithmetic (plan Section D):
- Extract two if-blocks (lines 2510, 2516) into `ShouldSkipFollower` → collapses to one branch → CYC = 7 ✓
- Add `if (dispatched > 0)` guard → +1 → CYC = 8 ✓
- **Final DispatchCopy CYC = 8 ≤ 8 ✓**

All new method CYC values:

| Method | Plan Claim | Independent Verification | Result |
|--------|-----------|--------------------------|--------|
| `ShouldSkipFollower` (new) | 3 | base(1) + if-ShouldSkipFollowerDispatch(+1) + if-ShouldSkipForReversalGuard(+1) = **3** | PASS ✓ |
| `IsLiveEntryBlocked_Check` (new) | 4 | base(1) + ContainsKey-instrKey(+1) + IsDedup(+1) + ContainsKey-orderId(+1) = **4** | PASS ✓ |
| `SetLiveEntryDispatched` (new) | 1 | Three sequential TryAdd, no branches = **1** | PASS ✓ |
| `IsEntryDispatched` (simplified) | 1 | Single return ContainsKey, no branches = **1** | PASS ✓ |
| `ShouldSkipForReversalGuard` post-T1 | 4 | base(1) + if(!hasLastDirection)(+1) + if(!IsReversalToFlatFollower)(+1) + `&&` on followerIsFlat(+1) = **4** | PASS ✓ |
| `HasWorkingEntries` | 5 | base(1) + foreach(+1) + instrument check(+1) + OrderState check(+1) + !IsBracketLeg(+1) = **5** | PASS ✓ |

All methods ≤ 8 ✓ (JS-066)

---

### 2. V-02 Fix Verification — PASS

**Source state at line 4842** (read this cycle):
```csharp
foreach (var order in acc.Orders) // (1) branch
```
No `.ToList()` — confirmed as the current unmodified state.

**Plan response** (Section C.3):
- Exact change specified: `foreach (var order in acc.Orders)` → `foreach (var order in acc.Orders.ToList())` at line 4842. ✓
- CYC impact stated as **0** (`.ToList()` is a method call on the iterator source, not a decision branch). ✓ Independent verification: `.ToList()` adds no conditional branching — CYC unchanged at 5. ✓
- **DW-REPAIRS-03-01 promoted from deferred to T1 in-scope**: Section C.3 header states "Promoted from DW-REPAIRS-03-01 (deferred) to T1 in-scope." Section I deferred table confirms DW-REPAIRS-03-01 is absent from the open items. Section H component summary includes `HasWorkingEntries (line 4840) | .ToList() fix — V-02 remediation`. ✓
- `HasWorkingEntries` CYC after fix stated as **5** (stale source comment of 3 corrected). Source-verified CYC = 5. ✓
- Comment update at line 4839 specifies corrected CYC=5 annotation and JS-001 `.ToList()` rationale mirroring `HasWorkingPttCopy` line 4861 pattern. ✓

`HasWorkingPttCopy` (source lines 4861–4876) confirmed to use `.ToList()` — the mirror pattern is correctly cited. ✓

**V-02 RESOLVED. All sub-checks PASS.**

---

### 3. T1 Design Complete — PASS

| Check | Source Evidence / Plan Section | Result |
|-------|-------------------------------|--------|
| `ShouldSkipForReversalGuard` extended with `&& !HasWorkingEntries(acc, instr)` | Section C exact change; source line 2594 currently reads `bool followerIsFlat = IsFlat(FindPosition(acc, instr));` — plan mandates `&& !HasWorkingEntries(acc, instr)` append | PASS ✓ |
| `HasWorkingEntries` `.ToList()` fix included in T1 scope | Section C.3, Section H, Section I | PASS ✓ |
| Comment update at `HasWorkingEntries` (line 4839) | Section C.3 specifies exact before/after comment text | PASS ✓ |
| DW-B128 guard intent preserved | Section C: guard narrowed (tighter flat definition), not removed; `IsReversalToFlatFollower` line 5916 unchanged | PASS ✓ |
| `ShouldSkipForReversalGuard` CYC post-T1 = 4 ≤ 8 | Verified in Item 1 above | PASS ✓ |
| Comment block update (lines 2579–2580) specified | Section C exact before/after comment text: CCN updated to 4, semantic description updated to include "no working entries" | PASS ✓ |

---

### 4. T2 Design Complete — PASS

| Check | Plan Section | Result |
|-------|-------------|--------|
| B1 chosen: split `IsLiveEntryBlocked` into `IsLiveEntryBlocked_Check` + `SetLiveEntryDispatched` | Section D | PASS ✓ |
| B2 rejection documented (B2a: race window; B2b: double-dispatch safety break) | Section D | PASS ✓ |
| `ShouldSkipFollower` extraction specified with signature, CYC=3, zero new logic | Section D | PASS ✓ |
| DispatchCopy four changes enumerated (Change 0 extraction + gate5 rename + dispatched counter + post-loop setter) | Section D | PASS ✓ |
| DispatchCopy final CYC = 8 ≤ 8 | Verified in Item 1 above | PASS ✓ |
| `SetLiveEntryDispatched` called only when `dispatched > 0` | Section D Change 3: `if (dispatched > 0) SetLiveEntryDispatched(instrKey, orderId);` | PASS ✓ |
| MGC guard (DW-B142-MGC-02) verified at all three gate levels (5a, 5b, 5c) | Section D MGC guard verification subsection | PASS ✓ |
| `EvictDedup` unchanged | Section D + Section H | PASS ✓ |
| Stale CYC comment at line 2427 updated to reflect CYC=8 | Section D DispatchCopy comment update | PASS ✓ |
| `IsEntryDispatched` TryAdd side effect removed; simplified to ContainsKey-only | Section D; source lines 5731–5737 confirmed | PASS ✓ |

Source lines 5731–5737 (read this cycle) confirm current `IsEntryDispatched` has TryAdd side effect — plan correctly identifies and removes it. ✓

Source lines 5748–5758 (read this cycle) confirm current `IsLiveEntryBlocked` writes `_liveEntryInstruments` and `_entryInstrKeyByOrderId` before any dispatch. Plan B1 split eliminates this. ✓

---

### 5. Lane-Split Gate — PASS

| Q | Answer | Rationale (plan Section A) | Result |
|---|--------|---------------------------|--------|
| Q1 — same method or within 50 lines? | NO | BUG-A target ~line 2584 vs BUG-B primary ~5748 (~3164 lines); vs DispatchCopy ~2428 (~156 lines) | PASS ✓ |
| Q2 — Fix B design depends on Fix A final design? | NO | T2 dispatched counter works regardless of which skip reason causes dispatched==0 | PASS ✓ |
| Q3 — each fix has standalone value if other is blocked? | YES | T1: restores reversal entry after working entries. T2: prevents phantom lock for ANY zero-dispatch scenario | PASS ✓ |
| Q4 — each fix has independent SIM verification path? | YES | T1 SIM and T2 SIM have no shared preconditions | PASS ✓ |

Gate result stated: LANES-APPROVED ✓

---

### 6. Test Coverage — PASS

| Check | Plan Section | Result |
|-------|-------------|--------|
| T1 test: `ShouldSkipForReversalGuard_AllowsEntryWhenFollowerHasWorkingOrders` | Section E | PASS ✓ |
| T2 test: `DispatchCopy_DoesNotSetPhantomInstrKey_WhenAllFollowersSkipped` | Section E | PASS ✓ |
| `[Fact]` baseline: 475 (measured via STEP 0 Select-String command) | Step 0 | PASS ✓ |
| Delta: +2 | Step 0 / Section E | PASS ✓ |
| Final target: 477 | Step 0 / Section E | PASS ✓ |
| DW-REPAIRS-02-02 closed by fresh measurement | Section I / Step 0 | PASS ✓ |
| `ShouldSkipFollower` omitted from dedicated test (pure extraction, no new logic) | Section E justification | PASS ✓ |

---

### 7. PTT-DIAG Log Lines PERMANENT — PASS

All five log points verified in source this cycle and marked PERMANENT in plan Section F:

| Log prefix | Source lines (confirmed) | Plan Section F | Result |
|-----------|--------------------------|---------------|--------|
| `[PTT-COPY-DIAG] gate0.5 exit:` | 2433–2440 | ✓ PERMANENT | PASS ✓ |
| `[PTT-COPY-DIAG] gate3 exit:` | 2446–2453 | ✓ PERMANENT | PASS ✓ |
| `[PTT-COPY-DIAG] gate4 exit:` | 2459–2466 | ✓ PERMANENT | PASS ✓ |
| `[PTT-COPY-DIAG] gate5 exit:` | 2476–2482 | ✓ PERMANENT | PASS ✓ |
| `[PTT-COPY-GUARD] skip reversal entry:` | 2597–2607 | ✓ PERMANENT | PASS ✓ |

`ShouldSkipFollower` extraction does not move any log-emitting code — all five logs remain inside the original methods (gate logs in `DispatchCopy`; guard log in `ShouldSkipForReversalGuard` body). ✓

Gate5 log fires on `IsLiveEntryBlocked_Check` returning true — same observable condition as before the rename. ✓

---

### 8. JS Rule Compliance — PASS

| Rule | Check | Source Evidence | Result |
|------|-------|----------------|--------|
| JS-001 | No throw in gate chain; `.ToList()` on `HasWorkingEntries` | V-02 fix in T1 scope (Section C.3). `.ToList()` snapshots the live collection before enumeration. Pattern mirrors `HasWorkingPttCopy` line 4863. ✓ | PASS ✓ |
| JS-002 | No null return where value expected | All predicates return bool. No null return path in any proposed method. ✓ | PASS ✓ |
| JS-003 | No magic string for discriminated state | No new magic-string state discrimination introduced. ✓ | PASS ✓ |
| JS-009 | No `Dictionary<K,V>` for shared/thread-touched collections | All proposed writes use existing ConcurrentDictionary fields (`_liveEntryInstruments`, `_entryInstrKeyByOrderId`, `_entryDispatchedOrders`). ✓ | PASS ✓ |
| JS-021 | No `lock()` — use lock-free structures | No lock() in any proposed change. ConcurrentDictionary TryAdd/ContainsKey throughout. ✓ | PASS ✓ |
| JS-023 | Immutable where possible (no side effects in check path) | `IsLiveEntryBlocked_Check` and `ShouldSkipFollower` are pure predicates. `SetLiveEntryDispatched` is the explicit commit-only method. ✓ | PASS ✓ |
| JS-025 | ConcurrentDictionary TryRemove lock-free | No new TryRemove introduced. ✓ | PASS ✓ |
| JS-033 | No `DateTime.Now` | Not applicable to proposed changes. ✓ | PASS ✓ |
| JS-042 | ASCII-only identifiers and string literals | All proposed identifiers ASCII: `ShouldSkipFollower`, `IsLiveEntryBlocked_Check`, `SetLiveEntryDispatched`, `IsEntryDispatched`, `dispatched`, `instrKey`, `orderId`. All comment text ASCII. ✓ | PASS ✓ |
| JS-066 | CYC ≤ 8 per method | All methods verified ≤ 8 (see Item 1). ✓ | PASS ✓ |

---

### 9. No `lock()` Anywhere — PASS

No `lock()` statement appears in any proposed pseudo-code, method body, or comment pattern in the plan. All state mutations use ConcurrentDictionary primitive operations (`TryAdd`, `ContainsKey`, `TryRemove`). This satisfies the Lock-Free Actor Pattern mandate and JS-021.

Plan Section G constraint table confirms for both T1 and T2: "No `lock()` anywhere = ✓ (no new locks)".

**PASS ✓**

---

### 10. ASCII-Only in Proposed Changes — PASS

Verified: all new identifiers and string literals in proposed changes are ASCII-only. Specifically: method names `ShouldSkipFollower`, `IsLiveEntryBlocked_Check`, `SetLiveEntryDispatched`; variable names `dispatched`, `instrKey`, `orderId`; comment text in Sections C, C.3, D, and F use only printable ASCII characters. No Unicode, no curly quotes, no em-dashes, no emoji.

**PASS ✓**

---

## SPEC COVERAGE MATRIX

| Requirement | Addressed? | Plan Section | Result |
|-------------|-----------|--------------|--------|
| LANE-SPLIT GATE result present | ✓ | Section A | PASS |
| Q1=NO | ✓ | Section A | PASS |
| Q2=NO | ✓ | Section A | PASS |
| Q3=YES | ✓ | Section A | PASS |
| Q4=YES | ✓ | Section A | PASS |
| Gate result: LANES-APPROVED | ✓ | Section A | PASS |
| BUG-A: A1 option chosen | ✓ | Section C | PASS |
| `followerIsFlat` extended with `&& !HasWorkingEntries(acc, instr)` | ✓ | Section C | PASS |
| DW-B128 guard intent preserved | ✓ | Section C | PASS |
| `HasWorkingEntries` called safely in hot path — `.ToList()` in T1 scope | ✓ | Section C.3 | PASS |
| `HasWorkingEntries` CYC after fix = 5 ≤ 8 | ✓ | Section C.3 | PASS |
| DW-REPAIRS-03-01 promoted from deferred to T1 in-scope | ✓ | Section C.3 / Section I | PASS |
| `ShouldSkipForReversalGuard` CYC post-T1 = 4 ≤ 8 | ✓ | Section C | PASS |
| `ShouldSkipForReversalGuard` comment block updated | ✓ | Section C | PASS |
| BUG-B: B1 chosen over B2 | ✓ | Section D | PASS |
| B2 rejection documented (both B2a and B2b variants) | ✓ | Section D | PASS |
| `ShouldSkipFollower` extraction resolves V-01 (CYC 8→7→8) | ✓ | Section D | PASS |
| `DispatchCopy` CYC = 8 ≤ 8 after all T2 changes | ✓ | Section D | PASS |
| `IsLiveEntryBlocked_Check` CYC = 4 ≤ 8 | ✓ | Section D | PASS |
| `SetLiveEntryDispatched` CYC = 1 ≤ 8 | ✓ | Section D | PASS |
| `SetLiveEntryDispatched` called only when `dispatched > 0` | ✓ | Section D | PASS |
| `IsEntryDispatched` TryAdd side effect removed | ✓ | Section D | PASS |
| Gate 5(a) `_liveEntryInstruments` guard preserved | ✓ | Section D | PASS |
| Gate 5(b) `IsDedup` / `_dedupCache` side effect preserved | ✓ | Section D | PASS |
| Gate 5(c) `_entryDispatchedOrders` ContainsKey-only in check path | ✓ | Section D | PASS |
| MGC guard (DW-B142-MGC-02) verified at all three gate levels | ✓ | Section D | PASS |
| `EvictDedup` unchanged | ✓ | Section D / H | PASS |
| Stale CYC comment at line 2427 updated | ✓ | Section D | PASS |
| T1 test name correct | ✓ | Section E | PASS |
| T2 test name correct | ✓ | Section E | PASS |
| `[Fact]` baseline 475 measured | ✓ | Step 0 | PASS |
| `[Fact]` delta +2 (475 → 477) | ✓ | Step 0 / E | PASS |
| PTT-DIAG logs marked PERMANENT (all five) | ✓ | Section F | PASS |
| No new log lines introduced | ✓ | Section F | PASS |
| JS-001: no throw in hot path | ✓ | Section G / C.3 | PASS |
| JS-021: no lock() | ✓ | Section G | PASS |
| JS-066: all methods CYC ≤ 8 | ✓ | Sections C / D | PASS |
| No lock() anywhere | ✓ | Section G | PASS |
| ASCII-only identifiers and string literals | ✓ | Section G | PASS |

---

## INCIDENTAL OBSERVATIONS (non-blocking, informational only — carried from Cycle 1)

**B2a CYC rejection arithmetic**: The plan's rejection of B2a cites post-extraction CYC as "7+1+1=9". The correct rejection reason is the race window / impure intermediate state. B2a is correctly rejected; the arithmetic in the secondary justification remains inaccurate. **Non-blocking** — the primary rejection reason (race window) is sound and sufficient.

---

## REVIEW VERDICT

**REVIEW_PASS**

**Violation count**: 0

V-02 (JS-001 — `.ToList()` missing from `HasWorkingEntries` in gate chain) is **RESOLVED** in Revision 2.
V-01 (DispatchCopy CYC) was resolved in Revision 1 and remains resolved.

All 10 checklist items PASS. All spec requirements addressed. All CYC budgets ≤ 8. All Jane Street DNA rules compliant.

**Phase 3 (ticket generation) is UNLOCKED.**

---

*ptt-plan-reviewer · PTT-REPAIRS-03 · 2026-09-08 (Cycle 2 — FINAL)*
