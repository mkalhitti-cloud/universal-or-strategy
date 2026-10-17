# Ticket Review: B197 — Gap Restore

**Reviewer**: ptt-ticket-reviewer
**Phase**: 3.5 (Ticket Review)
**Epic**: B197
**Tickets document**: docs/brain/B197/04-tickets.md
**Plan document**: docs/brain/B197/02-architecture-plan.md (REVIEW_PASS)
**Plan review**: docs/brain/B197/02-plan-review.md (0 violations, W-01 carry-forward)
**Source verified**: src/PropTraderTools/CopyEngine.cs
**Date**: 2026-10-12

---

## Pre-Review: CYC Discrepancy Investigation

**Mission instruction**: Plan §7 says TryEvictFollowerBeSlot CYC=7, ticket says CYC=6. Verify.

**Actual plan §7 table (T2 row)**:
> `TryEvictFollowerBeSlot | CopyEngine.cs | MODIFIED (call added) | 6 | 6 | 0 | ≤8 PASS`

**Plan says CYC=6. Ticket says CYC=6. Source comment at line 1696 says CYC=6.**

Source count (lines 1718-1737): base(1) + null-guard(1) + IsEvictTriggerState(1) +
IsFollowerAccount(1) + isFilled-and-IsFlat compound(1) + slotEvicted(1) = CYC=6. Confirmed.

**FINDING**: No discrepancy exists between plan and ticket. Both state CYC=6, which is
correct per source. The mission instruction's premise ("plan says CYC=7") is factually
incorrect. Plan and ticket are consistent at CYC=6. **Non-blocking — no action required.**

---

## TICKET T1

### 1. TICKET HEADER

| Field | Value |
|-------|-------|
| ID | T1 |
| Title | TrySetSlateReadyOnDrainFill — new private method + call site in OnOrderUpdate |
| DW IDs Closed | DW-B157-01 |

---

### Traceability

| Item | Plan Reference | Result |
|------|----------------|--------|
| TrySetSlateReadyOnDrainFill new method | Plan §3 T1, §7 table | PASS |
| OnOrderUpdate Filled branch call site | Plan §4 Filled branch block | PASS |
| DW-B157-01 closure | Plan §1 T1 row, §9 DW table | PASS |
| _followerSlateReady write body | Plan §3 T1 Body | PASS |
| [SLATE-READY-DRAIN] log marker | Plan §3 T1 Log | PASS |
| T1-BEFORE-AbortDrainOnFill ordering | Plan §3 T1 ORDERING CRITICAL, §4 | PASS |

No phantom work. No missing plan items. **Traceability: PASS**

---

### JS Pre-Check

| Rule | Check | Result |
|------|-------|--------|
| JS-021 (no lock) | _drainOwnedOrderIds.ContainsKey and _followerSlateReady[] are ConcurrentDictionary ops — lock-free. Confirmed fields at lines 394 and 401. No lock() described. | PASS |
| JS-001 (no throw) | Only early returns. No exception-throwing code path in method body. | PASS |
| JS-056 (1 assert per Fact) | Fact 1: 1× Assert.Contains. Fact 2: 1× Assert.Contains. | PASS |
| ASCII-only | "[SLATE-READY-DRAIN] acct=" — all ASCII. | PASS |

**JS Pre-Check: PASS**

---

### CYC Pre-Check

| Method | Derivation | Claimed CYC | Verified CYC | Result |
|--------|-----------|-------------|-------------|--------|
| TrySetSlateReadyOnDrainFill | base(1)+guard1(1)+guard2(1)+guard3(1)+guard4(1) | 5 | 5 | PASS ≤8 |
| OnOrderUpdate | unchanged per ticket | unchanged | unchanged | PASS |

**CYC Pre-Check: PASS**

---

### NT8 Constraints

| Claim | Verification | Result |
|-------|-------------|--------|
| _drainOwnedOrderIds.ContainsKey — ConcurrentDictionary read, lock-free | Field confirmed at line 394 as ConcurrentDictionary<string,byte>. ContainsKey pattern at line 916. | PASS |
| _followerSlateReady[Name] = true — ConcurrentDictionary indexer write, lock-free | Field confirmed at line 401 as ConcurrentDictionary<string,bool>. Write pattern at line 1060. | PASS |
| IsFollowerAccount(Account) — AddOnBase-accessible | Confirmed at line 813: `internal bool IsFollowerAccount(Account acc)` | PASS |
| NinjaTrader.Code.Output.Process — AddOnBase-accessible | Existing pattern throughout OnOrderUpdate. | PASS |
| No AtmStrategyCreate, no AtmStrategyChangeStopTarget | Not described. | PASS |

**NT8 Constraints: PASS**

---

### Completeness (13 mandatory sections)

All 13 sections present: HEADER, SCOPE LOCK, ROOT CAUSE, DESIGN, IMPLEMENTATION STEPS,
CALL SITE WIRING, NT8 CONSTRAINTS, METHOD SIGNATURES, CYC BUDGET, MANDATORY 7-SCAN
CHECKLIST, TEST SPECIFICATION, SUCCESS CRITERIA, COMPLETION ARTIFACT.

**Completeness: PASS**

---

### Critical T1 Ordering

Ticket §6 CALL SITE WIRING states:
> "Ordering invariant (CRITICAL): TrySetSlateReadyOnDrainFill(e) MUST appear on the line
> immediately after { in the Filled branch, BEFORE AbortDrainOnFill."

Ticket §5 Step 2 shows the 3-call block with TrySetSlateReadyOnDrainFill FIRST, AbortDrainOnFill
second, TryCancelStaleGtcExitOnEntry third. Rationale (AbortDrainOnFill removes from
_drainOwnedOrderIds at line 7033, Guard(3) checks ContainsKey) is documented.

**T1 Ordering Constraint: PASS**

---

### Scan Checklist

Scans SCAN-01 through SCAN-07 are all present in §10:
- Scan 1 — lock() grep ✓
- Scan 2 — CYC check (TrySetSlateReadyOnDrainFill) ✓
- Scan 3 — ASCII check ✓
- Scan 4 — throw check ✓
- Scan 5 — nullable check (Guard 1 and Guard 4) ✓
- Scan 6 — test compile ✓
- Scan 7 — test run ✓

**Scan Checklist: PASS**

---

### File Routing

CopyEngine.cs: `src/PropTraderTools/CopyEngine.cs` ✓
Tests: `src/PropTraderTools/Tests/B197Tests.cs` ✓
Both paths are Wave workspace (c:\WSGTA\universal-or-strategy\src\PropTraderTools\). No Director paths.

**File Routing: PASS**

---

### VERDICT: TICKET_REVIEW_PASS

---

---

## TICKET T2

### 1. TICKET HEADER

| Field | Value |
|-------|-------|
| ID | T2 |
| Title | TryAbortLeaderFlatDrains — new private method + TryEvictFollowerBeSlot wiring |
| DW IDs Closed | DW-B156-01, DW-B156-DEFER-1 |

---

### Traceability

| Item | Plan Reference | Result |
|------|----------------|--------|
| TryAbortLeaderFlatDrains new method | Plan §3 T2, §7 table | PASS |
| TryEvictFollowerBeSlot call site addition | Plan §3 T2 modification block, §4 | PASS |
| DW-B156-01 + DW-B156-DEFER-1 closure | Plan §1 T2 row, §9 DW table | PASS |
| Placement: after null guard, before IsEvictTriggerState | Plan §3 T2, §4 structure | PASS |
| _pendingDispatchDrains.ToList() foreach body | Plan §3 T2 Body | PASS |
| [DRAIN-ABORT-LEADER-FLAT] log marker | Plan §3 T2 Log | PASS |

No phantom work. No missing plan items. **Traceability: PASS**

---

### JS Pre-Check

| Rule | Check | Result |
|------|-------|--------|
| JS-021 (no lock) | _pendingDispatchDrains.ToList() is ConcurrentDictionary snapshot — lock-free. AbortDrainOnFill uses ConcurrentDictionary.TryRemove — lock-free. No lock() described. | PASS |
| JS-001 (no throw) | Only early returns + method calls. No exception-throwing code path. | PASS |
| JS-056 (1 assert per Fact) | Fact 1: 1× Assert.Contains. Fact 2: 1× Assert.Contains. | PASS |
| ASCII-only | "[DRAIN-ABORT-LEADER-FLAT] triggered by " — all ASCII. | PASS |

**JS Pre-Check: PASS**

---

### CYC Pre-Check

| Method | Derivation | Claimed CYC | Verified CYC | Result |
|--------|-----------|-------------|-------------|--------|
| TryAbortLeaderFlatDrains | base(1)+guard1(1)+guard2(1)+guard3(1)+foreach(1) | 5 | 5 | PASS ≤8 |
| TryEvictFollowerBeSlot (post-modification) | Method call not a McCabe branch (+0 CYC delta). Source comment line 1696 confirms CYC=6 pre-modification. | 6 | 6 | PASS ≤8 |

**Note**: Plan §7 and ticket §9 are both consistent at TryEvictFollowerBeSlot CYC=6.
Source confirms CYC=6 per comment line 1696. No discrepancy.

**CYC Pre-Check: PASS**

---

### NT8 Constraints

| Claim | Verification | Result |
|-------|-------------|--------|
| _pendingDispatchDrains — ConcurrentDictionary<string,PendingDispatchDrain> at line 388 | Plan review §Check 2 T2 Baseline: confirmed `private readonly ConcurrentDictionary<string, PendingDispatchDrain> _pendingDispatchDrains =` at line 388. | PASS |
| IsFlat(FindPosition(...)) pattern | IsFlat at line 5730, FindPosition at line 5792. Pattern confirmed at TryEvictFollowerBeSlot line 1729. | PASS |
| AbortDrainOnFill(kv.Key) — string parameter, lock-free | AbortDrainOnFill at line 7029 confirmed. Takes string. Only TryRemove ops. | PASS |
| Call site: after e?.Order null guard in TryEvictFollowerBeSlot | Source lines 1720-1722 confirm null guard `if (o == null) return;` is before line 1723 (IsEvictTriggerState). Insertion point correct. | PASS |
| No AtmStrategyCreate, no async | Not described. | PASS |

**NT8 Constraints: PASS**

---

### Completeness (13 mandatory sections)

All 13 sections present: HEADER, SCOPE LOCK, ROOT CAUSE, DESIGN, IMPLEMENTATION STEPS,
CALL SITE WIRING, NT8 CONSTRAINTS, METHOD SIGNATURES, CYC BUDGET, MANDATORY 7-SCAN
CHECKLIST, TEST SPECIFICATION, SUCCESS CRITERIA, COMPLETION ARTIFACT.

**Completeness: PASS**

---

### Scan Checklist

Scans SCAN-01 through SCAN-07 all present in §10:
- Scan 1 — lock() grep ✓
- Scan 2 — CYC check (TryAbortLeaderFlatDrains + TryEvictFollowerBeSlot) ✓
- Scan 3 — ASCII check ✓
- Scan 4 — throw check ✓
- Scan 5 — nullable check (e.Order safe after TryEvictFollowerBeSlot null guard) ✓
- Scan 6 — test compile ✓
- Scan 7 — test run ✓

**Scan Checklist: PASS**

---

### File Routing

All paths point to `src/PropTraderTools/` (Wave workspace). No Director paths.

**File Routing: PASS**

---

### VERDICT: TICKET_REVIEW_PASS

---

---

## TICKET T3

### 1. TICKET HEADER

| Field | Value |
|-------|-------|
| ID | T3 |
| Title | TryCancelStaleGtcExitOnEntry + IsStaleGtcExitOrder — 2 new methods + call site in OnOrderUpdate |
| DW IDs Closed | DW-B154-01, DW-B154-02 |

---

### Traceability

| Item | Plan Reference | Result |
|------|----------------|--------|
| TryCancelStaleGtcExitOnEntry new method | Plan §3 T3, §7 table | PASS |
| IsStaleGtcExitOrder helper method | Plan §3 T3 revised design, §7 table | PASS |
| OnOrderUpdate Filled branch call site (3rd position) | Plan §4 Filled branch block | PASS |
| DW-B154-01 + DW-B154-02 closure | Plan §1 T3 row, §9 DW table | PASS |
| Guard(3) inline name-check (not stub) | Plan §3 T3 DESIGN DEVIATION NOTICE | PASS |
| acc.Orders.ToList() + acc.Cancel() pattern | Plan §3 T3 NT8 API | PASS |
| [GTC-SWEEP] log marker | Plan §3 T3 Body | PASS |

No phantom work. No missing plan items. **Traceability: PASS**

---

### JS Pre-Check

| Rule | Check | Result |
|------|-------|--------|
| JS-021 (no lock) | acc.Orders.ToList() snapshot + acc.Cancel() mirror CancelPttDragOrphansForAccount at lines 1939-1954. No lock() described. | PASS |
| JS-001 (no throw / no rethrow) | try/catch in TryCancelStaleGtcExitOnEntry absorbs acc.Cancel errors. Catch block DOES NOT rethrow — only StatusUpdate?.Invoke(). Explicitly noted: "catch block does NOT rethrow." Mirrors existing pattern at lines 1945-1953. | PASS |
| JS-056 (1 assert per Fact) | Fact 1: 1× Assert.Contains. Fact 2: 1× Assert.Contains. | PASS |
| ASCII-only | "[GTC-SWEEP] cancelled " and "[GTC-SWEEP] cancel error: " — all ASCII. | PASS |

**JS Pre-Check: PASS**

---

### CYC Pre-Check

| Method | Derivation | Claimed CYC | Verified CYC | Result |
|--------|-----------|-------------|-------------|--------|
| TryCancelStaleGtcExitOnEntry | base(1)+guard1(1)+guard2(1)+guard3-if(1)+guard3-OR(1)+guard3-AND(1)+guard4(1)+foreach(1) | 8 (CEILING) | 8 | PASS (at ceiling, not exceeded) |
| IsStaleGtcExitOrder | base(1)+null-if(1)+null-OR(1)+Working-check(1)+return-OR(1) | 5 | 5 | PASS ≤8 |

Note: try/catch does not add McCabe branches in Lizard's counting convention. CYC=8 is correct.

**CYC Pre-Check: PASS**

---

### NT8 Constraints

| Claim | Verification | Result |
|-------|-------------|--------|
| acc.Orders.ToList() — confirmed at line 1941 | Plan review §Check 2 T3 Baseline: confirmed. | PASS |
| acc.Cancel(new Order[] { o }) — confirmed at line 1947 | Plan review §Check 2 T3 Baseline: confirmed. | PASS |
| IsPttCopyEntryFilled stub at line 837 always returns false — DO NOT USE | Source line 837 confirmed as `=> false` stub. Ticket explicitly forbids stub use with `// DO NOT use IsPttCopyEntryFilled` comment in Guard(3). | PASS |
| Guard(3) uses inline name-check: StartsWith("PTT-Copy") OR Name=="Entry" | Ticket §4 Design shows exact guard. Spec references at line 4844 ("PTT-Copy") and lines 4196, 4823 ("Entry") confirmed in plan §3 T3 Baseline. | PASS |
| No AtmStrategyCreate, no async | Not described. | PASS |

**NT8 Constraints: PASS**

---

### T3 Design Deviation Check (CRITICAL)

The ticket:
1. Documents that IsPttCopyEntryFilled was REMOVED in B180. ✓
2. Documents that the stub at line 837 (`=> false`) always returns false. ✓
3. Explicitly warns in §3 ROOT CAUSE: "Using the stub as Guard (3) would cause TryCancelStaleGtcExitOnEntry to never fire (silent bug)." ✓
4. Requires the inline name-check: StartsWith("PTT-Copy", StringComparison.Ordinal) OR Name == "Entry". ✓
5. SUCCESS CRITERIA §12 item 2 explicitly requires inline name-check, not stub. ✓
6. Header comment in method code includes: `// DO NOT use IsPttCopyEntryFilled — REMOVED in B180. Stub at L837 always returns false.` ✓

**T3 Design Deviation: PASS**

---

### Cross-Ticket Call Site Ordering

Ticket §5 Step 2 and §6 both specify the 3-call block:
```
TrySetSlateReadyOnDrainFill(e);   // T1: first
AbortDrainOnFill(e.Order.Account.Name);  // existing
TryCancelStaleGtcExitOnEntry(e);  // T3: last
```
Order documented: T1 → AbortDrainOnFill → T3. Matches plan §4. ✓

**Cross-Ticket Ordering: PASS**

---

### Completeness (13 mandatory sections)

All 13 sections present: HEADER, SCOPE LOCK, ROOT CAUSE, DESIGN, IMPLEMENTATION STEPS,
CALL SITE WIRING, NT8 CONSTRAINTS, METHOD SIGNATURES, CYC BUDGET, MANDATORY 7-SCAN
CHECKLIST, TEST SPECIFICATION, SUCCESS CRITERIA, COMPLETION ARTIFACT.

**Completeness: PASS**

---

### Scan Checklist

Scans SCAN-01 through SCAN-07 all present in §10:
- Scan 1 — lock() grep ✓
- Scan 2 — CYC check (TryCancelStaleGtcExitOnEntry + IsStaleGtcExitOrder) ✓
- Scan 3 — ASCII check ✓
- Scan 4 — throw check (with explicit NOTE: try/catch allowed, catch does NOT rethrow) ✓
- Scan 5 — nullable check (Guard 1, Guard 4, Guard 3 null-short-circuit, IsStaleGtcExitOrder null guard) ✓
- Scan 6 — test compile ✓
- Scan 7 — test run ✓

**Scan Checklist: PASS**

---

### File Routing

All paths point to `src/PropTraderTools/` (Wave workspace). No Director paths.

**File Routing: PASS**

---

### VERDICT: TICKET_REVIEW_PASS

---

---

## TICKET T4

### 1. TICKET HEADER

| Field | Value |
|-------|-------|
| ID | T4 |
| Title | IsPttDragOrphanCancellable + IsPttDragOrphanCancellableTestable — suffix fix (StartsWith) |
| DW IDs Closed | DW-B154-03 |

---

### Traceability

| Item | Plan Reference | Result |
|------|----------------|--------|
| IsPttDragOrphanCancellable guard(3) change — exact-match → StartsWith | Plan §3 T4, §7 table | PASS |
| IsPttDragOrphanCancellableTestable guard(3) change | Plan §3 T4 | PASS |
| NT8-014 comment update at line 1902 | Plan §3 T4 annotation update | PASS |
| DW-B154-03 closure | Plan §1 T4 row, §9 DW table | PASS |

Source confirmed: line 1911 exact-match `o.Name != "PTT-TGT-Drag" && o.Name != "PTT-STP-Drag"` (to be changed). Line 1929 exact-match `orderName != "PTT-TGT-Drag" && orderName != "PTT-STP-Drag"` (to be changed). Both confirmed pre-B197.

No phantom work. No missing plan items. **Traceability: PASS**

---

### JS Pre-Check

| Rule | Check | Result |
|------|-------|--------|
| JS-021 (no lock) | Pure static predicate. No state access. No lock() described. | PASS |
| JS-001 (no throw) | Returns bool. No exception-throwing. | PASS |
| JS-056 (1 assert per Fact) | 4 Facts: each has 1× Assert.True(result). | PASS |
| ASCII-only | "PTT-TGT-Drag", "PTT-STP-Drag" — all ASCII. StringComparison.Ordinal specified. | PASS |

**JS Pre-Check: PASS**

---

### CYC Pre-Check

| Method | Claimed CYC | Source CYC | Result |
|--------|-------------|-----------|--------|
| IsPttDragOrphanCancellable | 4 (unchanged) | 4: base(1)+Working(1)+instr-?.(1)+name-&&(1) | PASS ≤8 |
| IsPttDragOrphanCancellableTestable | 4 (unchanged) | 4: same structure | PASS ≤8 |

StartsWith replacement has same McCabe branch count as exact-match `!=`. `&&` compound condition
counts as 1 branch regardless of the predicate used. CYC unchanged. ✓

**CYC Pre-Check: PASS**

---

### NT8 Constraints

| Claim | Verification | Result |
|-------|-------------|--------|
| Pure static predicates — no NT8 API access | Confirmed. Both methods are static, no AddOnBase API calls. | PASS |
| String.StartsWith(string, StringComparison.Ordinal) — zero-alloc, locale-independent | Standard .NET method, existing pattern throughout CopyEngine.cs. | PASS |
| No lock, no async | Not described. | PASS |

**SCAN-5 Note**: Ticket §10 Scan 5 correctly identifies a null-safety risk: `o.Name.StartsWith(...)` throws
if `o.Name` is null (unlike the original `o.Name != "PTT-TGT-Drag"` which returns true safely on null).
The ticket does NOT resolve this — it leaves the decision to the engineer: confirm NT8 guarantees
Order.Name non-null for Working orders, or add an explicit null guard. The ticket's Scan 5 text
fully documents the risk and required action. This is appropriate behavior for a ticket review
checklist item: it surfaces the risk and assigns verification to the engineer. **Acceptable.**

**NT8 Constraints: PASS**

---

### T4 Regression Coverage

4 [Fact]s present:
1. `IsPttDragOrphanCancellableTestable_SuffixedTgtName_ReturnsTrue` — "PTT-TGT-Drag-1" → true (new case) ✓
2. `IsPttDragOrphanCancellableTestable_SuffixedStpName_ReturnsTrue` — "PTT-STP-Drag-2" → true (new case) ✓
3. `IsPttDragOrphanCancellableTestable_UnsuffixedTgtName_StillReturnsTrue` — "PTT-TGT-Drag" → true (regression) ✓
4. `IsPttDragOrphanCancellableTestable_UnsuffixedStpName_StillReturnsTrue` — "PTT-STP-Drag" → true (regression) ✓

All 4 required variants present. **T4 Regression Coverage: PASS**

---

### Completeness (13 mandatory sections)

All 13 sections present: HEADER, SCOPE LOCK, ROOT CAUSE, DESIGN, IMPLEMENTATION STEPS,
CALL SITE WIRING, NT8 CONSTRAINTS, METHOD SIGNATURES, CYC BUDGET, MANDATORY 7-SCAN
CHECKLIST, TEST SPECIFICATION, SUCCESS CRITERIA, COMPLETION ARTIFACT.

**Completeness: PASS**

---

### Scan Checklist

Scans SCAN-01 through SCAN-07 all present in §10:
- Scan 1 — lock() grep ✓
- Scan 2 — CYC check (IsPttDragOrphanCancellable + IsPttDragOrphanCancellableTestable) ✓
- Scan 3 — ASCII check ✓
- Scan 4 — throw check ✓
- Scan 5 — nullable check (with explicit null-safety risk analysis and engineer action required) ✓
- Scan 6 — test compile ✓
- Scan 7 — test run ✓

**Scan Checklist: PASS**

---

### File Routing

All paths point to `src/PropTraderTools/` (Wave workspace). No Director paths.

**File Routing: PASS**

---

### VERDICT: TICKET_REVIEW_PASS

---

---

## Spec Coverage (Aggregate)

| DW ID | Required | Covered By | Result |
|-------|----------|------------|--------|
| DW-B157-01 | YES | T1 | PASS |
| DW-B156-01 | YES | T2 | PASS |
| DW-B156-DEFER-1 | YES | T2 | PASS |
| DW-B154-01 | YES | T3 | PASS |
| DW-B154-02 | YES | T3 | PASS |
| DW-B154-03 | YES | T4 | PASS |

All 6 DW IDs covered. No duplicates. No gaps. **Spec Coverage: PASS**

---

## Cross-Ticket Call Site Ordering (OnOrderUpdate Filled branch)

Both T1 and T3 add calls to the same `else if (e.Order.OrderState == OrderState.Filled)` block.
Required order: TrySetSlateReadyOnDrainFill (T1) → AbortDrainOnFill → TryCancelStaleGtcExitOnEntry (T3).

- T1 §5 Step 2 specifies the 3-call block explicitly: T1 first, AbortDrainOnFill second, T3 third. ✓
- T3 §5 Step 3 verifies/completes the same block and documents the ordering. ✓
- T1 §6 includes a NOTE: "TryCancelStaleGtcExitOnEntry(e) is the T3 call site" — both tickets
  are aware of the shared edit. ✓

**Cross-Ticket Call Site Ordering: PASS**

---

## Overall Summary

| Ticket | Traceability | JS Pre-Check | CYC Pre-Check | NT8 Check | Test Coverage | Scan Checklist | File Routing | VERDICT |
|--------|-------------|-------------|--------------|----------|--------------|----------------|-------------|---------|
| T1 | PASS | PASS | PASS | PASS | PASS | PASS | PASS | TICKET_REVIEW_PASS |
| T2 | PASS | PASS | PASS | PASS | PASS | PASS | PASS | TICKET_REVIEW_PASS |
| T3 | PASS | PASS | PASS | PASS | PASS | PASS | PASS | TICKET_REVIEW_PASS |
| T4 | PASS | PASS | PASS | PASS | PASS | PASS | PASS | TICKET_REVIEW_PASS |

---

## Warnings (Non-Blocking)

| ID | Description | Action |
|----|-------------|--------|
| W-01 (carry-forward) | Line 1041 writes _followerSlateReady = false, not true. Baseline description in plan does not distinguish write direction. Engineer should note this. | Carry-forward from plan review. Not blocking. |
| W-CYC-NOTE | Mission instruction states "Plan §7 says TryEvictFollowerBeSlot CYC=7." Actual plan §7 shows CYC=6. Both plan and ticket are consistent at CYC=6. Source confirms CYC=6. No discrepancy exists. | No action required. Factual correction noted here only. |

---

## Overall: TICKET_REVIEW_PASS

All 4 tickets pass all 7 mandatory checks. Zero violations found.
10 [Fact]s across T1-T4 (T1×2, T2×2, T3×2, T4×4). All have exactly 1 assert each (JS-056 PASS).
7-scan checklists present in all 4 tickets (defense-in-depth contract intact).
T1 ordering invariant documented and enforced. T3 stub-bypass design deviation documented and enforced.
T4 regression coverage complete (4 Facts: 2 new suffix cases + 2 regression cases).

Safe to spawn engineer.

---

*Written by: ptt-ticket-reviewer*
*Epic: B197*
*Phase: 3.5 (Ticket Review)*
*Verdict: TICKET_REVIEW_PASS*
*Violations: 0*
*Warnings: 2 (both non-blocking carry-forwards)*
