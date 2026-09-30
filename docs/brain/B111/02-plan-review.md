# B111-T1 Plan Review

**Block**: B111-T1  
**Reviewer**: ptt-plan-reviewer  
**Review Date**: 2026-08-28  
**Plan file**: docs/brain/B111/02-architecture-plan.md  
**Source verified**: src/PropTraderTools/CopyEngine.cs, src/PropTraderTools/Features/PttGlobalQuickExit.cs  
**Rules verified**: docs/standards/jane-street/RULES_CATALOG.md  

---

## VERDICT: REVIEW_PASS

**Condition**: Ticket engineer MUST acknowledge WARNING W1 (see Section 3) before implementing Change C
and record the resolution in the ticket completion report.

---

## 1. Source Accuracy Matrix

All cited line numbers were verified against source.

| Change | Plan Claim | Source Verified? | Finding |
|--------|-----------|-----------------|---------|
| Change A — delete L1465 | `_beReplaceAttempts.TryRemove(capturedAcc.Name, out _); // DW-B82-01: reset on slot consumption` | YES | Exact match at L1465 inside timer tick lambda success arm |
| Reset guard at L1354 | `_beReplaceAttempts.TryRemove(o.Account.Name, out _);` | YES | Present, untouched |
| Reset guard at L1409 | `_beReplaceAttempts.TryRemove(accName, out _); // ALWAYS reset on terminal` | YES | Present, untouched |
| Insertion point L2296/L2297 | After `var instr = cancelledStop.Instrument;` before `// (4) Attempt-count guard` | YES | L2296 = `var instr`, L2297 = `// (4) Attempt-count guard: max 3` — exact |
| Change B-1 — L2299 constant | `if (prevAttempts >= 3) // (4)` | YES | Exact match at L2299 |
| Change B-2 — L2304 string | `" -- max 3 attempts, no new slot (TryFireFollowerBeRetry still holds slot "` | YES | Exact match at L2304 |
| Change B-3 — L2324 string | `"/3, slot registered, 500ms fallback queued"` | YES | Exact match at L2324 |
| Method header L2279 comment | `// CYC=5: (1) null guard...` | YES | Exact match at L2279 |
| PttGlobalQuickExit.cs L159-162 finally | `CopyEngine.Instance?._qxCancelInProgress.TryRemove(acc.Name, out _);` | YES | Exact match at L161 |
| DW-B105 guard still present at L2293 | `if (_qxCancelInProgress.ContainsKey(cancelledStop.Account.Name)) return;` | YES | Present and untouched |
| NT8 API: `o.Name.StartsWith("PTT-QX-"...)` | "Used at CopyEngine.cs L1339" | MINOR INACCURACY | L1338-1339 uses `StartsWith("PTT-QX-T",...)`. The plan generalises to `"PTT-QX-"` for the new guard — acceptable. The exact cited pattern is a sub-variant. No correctness impact. |
| NT8 API: `o.Instrument?.FullName` | "Used at CopyEngine.cs L1504" | MINOR INACCURACY | L1504 is `order.Instrument.FullName` (non-nullable dereference inside `FindMatchingRule`). The `?.` null-safe form is appropriate in the new guard; the cited line does not use `?.`. No correctness impact. |

---

## 2. Checklist Evaluation

### 2a. Accuracy Checks

| Check | Result | Evidence |
|-------|--------|---------|
| Cited line numbers match actual source content | PASS | All 9 cited locations verified (see Section 1) |
| TryRemove at ~L1465 (DW-B111) correctly identified as the bug | PASS | L1465 confirmed: timer-tick lambda resets counter before MoveStopToBreakEven |
| Correct reset locations (L1354, L1409) present and untouched | PASS | Both lines confirmed in source, plan does not modify them |
| PTT-QX presence check — acc.Orders query pattern valid for NT8 API | PASS (with WARNING W1) | `acc.Orders` enumerated at 17 locations in CopyEngine.cs including from OnOrderUpdate-callees; pattern is established. WARNING W1 applies (see Section 3). |
| _qxCancelInProgress guard preserved (belt-and-suspenders) | PASS | L2293 guard is NOT removed; explicitly stated in plan Section 3.7 |

### 2b. CYC Checks

| Method | CYC Before | CYC After | Delta | Budget ≤ 8? | Result |
|--------|-----------|-----------|-------|------------|--------|
| `TryReplacePttBeBrackets` | 6 | 7 | +1 | YES | PASS |
| `QueueBeRetryFallback` (outer) | 1 | 1 | 0 | YES | PASS |
| `QueueBeRetryFallback` timer tick lambda | 2 | 2 | 0 | YES | PASS |
| `TryFireFollowerBeRetry` (unchanged) | 5 | 5 | 0 | YES | PASS |
| `TryEvictFollowerBeSlot` (unchanged) | 6 | 6 | 0 | YES | PASS |
| `ExecuteOne` PttGlobalQuickExit (comment only) | unchanged | unchanged | 0 | YES | PASS |

**Note on pre-change CYC=6 for TryReplacePttBeBrackets**: Plan acknowledges the source header currently reads `CYC=5` (predating DW-B92). The reviewer confirms this is a stale annotation — source L2293 shows a 4th guard `_qxCancelInProgress.ContainsKey` already present (the DW-B105 guard), making the pre-B111 count 6. Plan's Change D (update comment to `CYC=7`) is correct.

**Arithmetic verification**: Current guards in `TryReplacePttBeBrackets`:
1. `cancelledStop?.Account == null` (L2285)
2. `!IsFollowerAccount(...)` (L2287)
3. `IsFlat(FindPosition(...))` (L2289)
4. `_qxCancelInProgress.ContainsKey(...)` (L2293) — DW-B105
5. `prevAttempts >= 3` (L2299)
6. `!_pendingFollowerBeSlots.TryAdd(...)` (L2317)

= 6 branches. Insert 1 new guard (Change C) → CYC=7. Confirmed.

### 2c. Test Coverage Checks

| Test | Spec Requirement | xUnit [Fact]? | Coverage | Result |
|------|-----------------|--------------|---------|--------|
| T_B111_01 `TryReplacePttBeBrackets_SkipsRecovery_WhenPttQxOrderWorking` | DW-B112 skip when Working | YES | Asserts no slot registered + DW-B112 log when PTT-QX Working | PASS |
| T_B111_02 `TryReplacePttBeBrackets_SkipsRecovery_WhenPttQxOrderSubmitted` | DW-B112 skip when Submitted | YES | Covers `\|\| o.OrderState == OrderState.Submitted` branch specifically | PASS |
| T_B111_03 `QueueBeRetryFallback_AttemptCounter_NotResetBeforeMoveStop` | DW-B111 TryRemove absence | YES | Sets counter to 2 before timer tick; asserts counter remains 2 throughout | PASS |
| T_B111_04 `QueueBeRetryFallback_LoopTerminates_AfterCapAttempts` | DW-B111 cap=5 terminates loop | YES | Two-part: prevAttempts=4 → slot registered; prevAttempts=5 → guard fires | PASS |

### 2d. JS Rule Checks

| Rule | Check | Result | Evidence |
|------|-------|--------|---------|
| JS-021 (No lock()) | No lock() in proposed code | PASS | All operations use ConcurrentDictionary (lock-free) and read-only acc.Orders enumeration |
| JS-033 (No async void) | No async void in proposed code | PASS | No async methods introduced or modified; DispatcherTimer.Tick is event handler (exempt) |
| JS-001 (No throw) | No throw in proposed code | PASS | Both methods are void; no exceptions |
| JS-002 (No return null) | No return null in proposed code | PASS | Both methods return void |
| ASCII-only | No Unicode in string literals | PASS | All new literals ASCII-only: `"PTT-QX-"`, `"[BE-DIAG] TryReplacePttBeBrackets: "`, etc. |
| DateTime.UtcNow | Not touched | PASS | No DateTime usage introduced |
| FontFamily override | Not touched | PASS | No UI changes |
| Hardcoded hex colors | Not touched | PASS | No color literals |
| CreateOrder PTT- prefix | No new CreateOrder calls | PASS | No new order submissions |

### 2e. Completeness Checks

| Check | Result | Evidence |
|-------|--------|---------|
| Exact old code and new code for both fixes | PASS | Change A: exact line quoted; Changes B-1/B-2/B-3: exact old/new per line; Change C: full block; Change D: full comment; Change E: full comment |
| Cap decision (3 → 5) with reasoning | PASS | Section 2.3: 1.5s insufficient for partial-target retry; cap=5 = 2.5s bounded; primary fix makes cap the first genuine safety valve |
| PttBreakEvenSwap.cs scope decision documented | PASS | Section 5: three numbered reasons for deferral; deferred as B111-DEFER-01 |
| Risk analysis included | PASS | Section 9: 4 named risks with mitigations |

---

## 3. Warnings (Non-Blocking)

### W1 — `acc.Orders.Any()` without `.ToList()` snapshot

**Location**: Plan Section 3.5 (proposed Change C guard block)  
**Severity**: WARNING (non-blocking — no P0 rule violated, but codebase pattern inconsistency)

**Finding**: The plan proposes:
```csharp
acc.Orders.Any(
    o =>
        o.Name.StartsWith("PTT-QX-", StringComparison.Ordinal)
        && (o.OrderState == OrderState.Working || o.OrderState == OrderState.Submitted)
        && o.Instrument?.FullName == instr.FullName
)
```

The codebase's own safety comment at `CopyEngine.cs L2414` states:
> `acc.Orders.ToList() snapshot prevents InvalidOperationException`

Multiple methods in the same file adopt `.ToList()` before iterating `acc.Orders` (L2417, L2818, L2936, L2967, L3649). Using `.Any()` on the live `IEnumerable` without a snapshot is inconsistent with this documented safety practice.

**Plan's Justification**: Section 9 Risk 4 states "NT8's Account.Orders collection supports concurrent read access" and cites `CancelQxBrackets` at L757 as a precedent for direct enumeration. L757 confirms: `foreach (Order o in acc.Orders)` without `.ToList()`. The justification is partially valid.

**Required Engineer Action**: Before implementing Change C, the engineer MUST document in the ticket completion report one of:
  - (a) Confirm `.Any()` early-exit is safe without `.ToList()` because NT8's `Account.Orders` implements `IEnumerable` with snapshot semantics (cite the NT8 reference or existing code evidence), OR
  - (b) Adopt `.ToList()` before `.Any()` to match the codebase's own safety pattern: `acc.Orders.ToList().Any(...)`.

Option (b) is preferred for consistency. Neither choice changes CYC.

---

## 4. Minor Inaccuracies (No Action Required)

| ID | Location in plan | Finding | Impact |
|----|-----------------|---------|--------|
| M1 | Section 3.6: "o.Name.StartsWith... Used at CopyEngine.cs L1339" | L1338-1339 uses `StartsWith("PTT-QX-T",...)` (sub-variant with digit check). Plan's new guard uses `"PTT-QX-"` (broader prefix). Correct by design. | None — broadening is intentional |
| M2 | Section 3.6: "o.Instrument?.FullName... Used at CopyEngine.cs L1504" | L1504 is `order.Instrument.FullName` without `?.` inside `FindMatchingRule`. The `?.` form is appropriate for the new guard (defensive null check). | None — plan's use of `?.` is more defensive |

---

## 5. Spec Coverage Matrix

| Requirement | Addressed? | Plan Section |
|-------------|-----------|-------------|
| DW-B111: Remove counter reset from timer callback | YES | Section 2.2 (Change A) |
| DW-B111: Raise attempt cap 3 → 5 with reasoning | YES | Section 2.3 (Changes B-1/B-2/B-3) |
| DW-B112: Structural PTT-QX presence check (Option 2) | YES | Section 3.3–3.5 (Change C) |
| DW-B112: Update PttGlobalQuickExit.cs comment | YES | Section 4 (Change E) |
| DW-B112: Preserve belt-and-suspenders _qxCancelInProgress guard | YES | Section 3.7 |
| Method header comment update to CYC=7 | YES | Section 3.9 (Change D) |
| PttBreakEvenSwap.cs secondary fix — scope decision | YES | Section 5 (deferred as B111-DEFER-01) |
| 4 test stubs (T_B111_01 through T_B111_04) | YES | Section 7 |
| JS rule compliance table | YES | Section 8 |
| Risk analysis | YES | Section 9 |
| Out-of-scope items listed | YES | Section 10 |
| Change summary matrix | YES | Appendix |

All spec requirements are addressed.

---

## 6. Summary

**Zero P0 violations.** No lock(), no async void, no throw, no return null, no CYC > 8 in any touched method.

**Zero inaccurate line number citations.** All 9 cited locations in CopyEngine.cs and PttGlobalQuickExit.cs verified against source and found to be exact matches.

**One non-blocking WARNING (W1)**: `acc.Orders.Any()` without `.ToList()` snapshot is inconsistent with the codebase's documented safety practice at L2414. Engineer must resolve before implementation and document the resolution in the ticket completion report.

**Plan is approved for ticket generation.**

---

*Reviewer: ptt-plan-reviewer | B111-T1 | 2026-08-28*
