# B158 Final Review
# ptt-plan-reviewer Phase 5 output
# Block: B158
# Reviewed: 2026-09-15
# Reviewer role: ptt-plan-reviewer
# Status: FINAL_PASS

---

## VERDICT: FINAL_PASS

Zero cross-file violations. All spec requirements satisfied at source + unit-test level.
Both output files written (05-final-review.md + 06-deferred-backlog.md).
SIM gate for DW-B156-01 is documented and PENDING Director execution — this is expected
and does not block FINAL_PASS (implementation is confirmed; SIM is a Director-run gate).

---

## 1. Coherent System Check

### 1.1 T1 — DW-B156-01 Leader-Flat Drain Abort (Verification-Only)

**Fix3 Implementation Status**: CONFIRMED PRESENT.

Source read at `src/PropTraderTools/CopyEngine.cs` L1929-1974:

| Item | Expected | Source Confirms | Result |
|------|----------|-----------------|--------|
| Method signature | `private void IsLeaderFlatDrainAbort(Order o, string instrFull)` at L1950 | EXACT MATCH | PASS |
| Call site | Unconditional call at L1938 within TryEvictFollowerBeSlot | `IsLeaderFlatDrainAbort(o, instrFull);` at L1938 | PASS |
| Pre-Gate-1 placement | Called at OnOrderUpdate L1712, before Gate 1 at L1763 | TryEvictFollowerBeSlot confirmed pre-Gate-1 hook | PASS |
| Guard 1 | `if (IsFollowerAccount(o.Account)) return;` at L1952-1953 | PRESENT | PASS |
| Guard 2 | `if (o.OrderState != OrderState.Filled) return;` at L1954-1955 | PRESENT | PASS |
| Guard 3 | `if (!IsFlat(FindPosition(o.Account, o.Instrument))) return;` at L1956-1957 | PRESENT | PASS |
| Loop | `foreach (var kv in _pendingDispatchDrains)` at L1959 | PRESENT — ConcurrentDictionary lock-free | PASS |
| Instrument match | `kv.Value.Instrument?.FullName == instrFull` at L1961 | PRESENT | PASS |
| Abort call | `AbortDrainOnFill(kv.Key)` at L1963 | PRESENT | PASS |
| Counter | `abortCount++` at L1964 | PRESENT | PASS |
| Log marker | `[DRAIN-ABORT-LEADER-FLAT]` when `abortCount > 0` at L1967-1973 | EXACT MARKER CONFIRMED | PASS |

**All 8 structural items PASS.** Engineer and verifier independently confirmed all 8 items
with zero discrepancies (ticket-1-completion.md §3 / ticket-1-verification.md §3.2).

**Unit test**: `TryEvictFollowerBeSlot_LeaderFlatDrainAbort_LogMarkerIsExactString` at
CopyEngineTests.cs:9481. `[Fact]`, no `[Skip]`. Passed 153–155 ms. Independently
confirmed by both engineer and verifier.

**SIM gate**: PENDING Director execution. Documentation complete (5-step scenario,
PASS/FAIL criteria, INCONCLUSIVE scenario, DW-B158-DRAIN-RACE-01 escalation path).
This is not a FINAL_FAIL condition — the SIM gate is a Director-run operational verification.
Block disposition: **IMPLEMENTATION_CONFIRMED / SIM_PENDING_DIRECTOR**.

### 1.2 T2 — DW-B157-COMMENT-01 Stale Comment (Comment-Only)

**Comment Status**: CORRECTED.

Source read at `src/PropTraderTools/CopyEngine.cs` L6514-6528:

| Item | Expected | Source Confirms | Result |
|------|----------|-----------------|--------|
| L6517 new text | `// Returns true iff the current dispatch reverses the last direction AND the follower is NOT flat (open position + reversal = protection path).` | EXACT MATCH | PASS |
| Old text absent | `// Returns true iff the current dispatch reverses the last direction AND the follower is flat.` must NOT be present | NOT PRESENT | PASS |
| Return expression unchanged | `return currentAction != lastAction && !followerIsFlat;` at L6526 | CONFIRMED UNCHANGED | PASS |
| CYC comment adjacent | `// CYC=2 (one && expression in a single return)...` at L6518 | PRESENT AND UNCHANGED | PASS |
| Method body L6520-6527 | Identical to pre-edit state | CONFIRMED | PASS |

**git diff**: Exactly 1 line changed (L6517). No whitespace changes. No executable code changes.
Engineer BUILD_PASS + verifier VERIFY_PASS, zero discrepancies between Layer 2 and Layer 3.
ptt-sync.ps1: 18 linked, 0 errors.

### 1.3 Cross-Ticket Interference Check

| Q | Check | Result |
|---|-------|--------|
| T1 region (L1929-1974) modified by T2? | git diff shows only L6517 changed. T1 region has 0 diff entries (confirmed by verifier SCAN-07). | PASS — NO interference |
| T2 region (L6516-6527) modified by T1? | T1 is zero-diff (empty git diff confirmed by both engineer and verifier). | PASS — NO interference |
| T2 scope isolation | ticket-2-verification.md §1 confirms T1 region was NOT read or analysed by T2 session. | PASS |
| T1 scope isolation | ticket-1-verification.md §1 confirms T2 region was NOT examined by T1 session. | PASS |

**Lane independence: CONFIRMED. Zero cross-ticket interference.**

---

## 2. Cross-File JS Violations Check

Both tickets scanned file-wide. Results independently confirmed by both engineer and verifier.

| Rule | Scan | T1 Result | T2 Result | Combined |
|------|------|-----------|-----------|---------|
| JS-021: no lock() | SCAN-02 `^\s+lock\s*\(` | 0 matches | 0 matches | PASS |
| NT8: no async void | SCAN-05 `\basync void\b` | 0 executable matches | 0 executable matches | PASS |
| NT8: no DateTime.Now | SCAN-03 `\bDateTime\.Now\b` | 0 executable matches (7 comment-only, pre-existing) | 0 executable matches (same 7 comment-only) | PASS |
| ASCII-001: no non-ASCII | SCAN-01 byte scan | Count=0 | Count=0 | PASS |
| JS-001: no throw in hot paths | Source inspection | No throw in IsLeaderFlatDrainAbort / TryEvictFollowerBeSlot / AbortDrainOnFill | N/A (comment-only) | PASS |
| JS-013: CYC<=8 | SCAN-04 | IsLeaderFlatDrainAbort CYC=8 AT LIMIT, no changes. TryEvictFollowerBeSlot CYC=7. AbortDrainOnFill CYC=3. | IsReversalToFlatFollower CYC=2, unchanged. | PASS |

**No Jane Street DNA violations. No NT8 constraint violations. All scans ZERO.**

**Note (pre-existing, not a violation)**: Stale CYC=6 comment at ~L1904 for TryEvictFollowerBeSlot
predates DW-B156-DEFER-1 decomposition. Authoritative CYC=7 comment at L1926. Neither B158
ticket is responsible for this pre-existing inconsistency. Flagged for future cleanup.

---

## 3. Spec Requirements Coverage

| Requirement | Source | Addressed? | Block Outcome |
|-------------|--------|------------|---------------|
| DW-B156-01: Leader-flat drain abort (ghost re-entry fix) | B156 deferred backlog | YES — T1 source verification COMPLETE; unit test PASS; SIM gate documented, PENDING Director | IMPLEMENTATION_CONFIRMED / SIM_PENDING |
| DW-B157-COMMENT-01: Stale comment at IsReversalToFlatFollower | B157 deferred backlog | YES — T2 corrected L6517 to correct semantics | CLOSED |
| Correct log marker [DRAIN-ABORT-LEADER-FLAT] | DW-B156-01 spec | YES — confirmed exact string at L1969 | CONFIRMED |
| SIM FAIL escalation path | Protocol | YES — DW-B158-DRAIN-RACE-01 defect card path documented in ticket | DOCUMENTED |
| Deferred items carried forward (4 OPEN items) | B157 backlog | YES — 4 items unchanged and carried forward | CARRIED FORWARD |
| NT8 API compliance | RULES_CATALOG.md | YES — no new API calls; banned APIs (AtmStrategyCreate, Account.All in ctor, etc.) confirmed absent | PASS |

**All spec requirements addressed. No gaps.**

---

## 4. All 7 Scans Zero (Aggregate)

### T1 (Zero-diff — all scans are read-only baseline confirmations)

| Scan | Command | Result | Status |
|------|---------|--------|--------|
| SCAN-01 | Non-ASCII byte scan | Count=0 | PASS |
| SCAN-02 | `Select-String lock\s*\(` | 0 matches | PASS |
| SCAN-03 | `Select-String DateTime\.Now` | 0 executable matches | PASS |
| SCAN-04 | Source comment CYC read | CYC=8/7/3 (all <=8) | PASS |
| SCAN-05 | `Select-String async void` | 0 executable matches | PASS |
| SCAN-06 | `dotnet test --filter "TryEvictFollowerBeSlot_LeaderFlatDrainAbort_LogMarkerIsExactString"` | 1 PASSED, 0 failures | PASS |
| SCAN-07 | `git diff CopyEngine.cs` | Empty diff (zero changes) | PASS |

Engineer and verifier ran all 7 scans independently. Zero discrepancies. **T1: 7/7 PASS.**

### T2 (Comment-only — 1 line changed at L6517)

| Scan | Command | Result | Status |
|------|---------|--------|--------|
| SCAN-01 | `Select-String lock\s*\(` | 0 matches | PASS |
| SCAN-02 | `Select-String async void` | 0 executable matches | PASS |
| SCAN-03 | `Select-String DateTime\.Now` | 0 executable matches | PASS |
| SCAN-04 | Non-ASCII byte scan | Count=0 | PASS |
| SCAN-05 | Source comment CYC=2 adjacent line | CYC=2 unchanged | PASS |
| SCAN-06 | `dotnet test --filter "IsReversalToFlatFollower"` + full suite | 3 pre-existing skips, 0 new failures | PASS |
| SCAN-07 | `git diff CopyEngine.cs` + ptt-sync.ps1 | 1 line changed (L6517); 18 linked, 0 errors | PASS |

Engineer and verifier ran all 7 scans independently. Zero discrepancies. **T2: 7/7 PASS.**

**Pre-existing test suite failure** (DwB15701SlateReadyTests.TrySetSlateReadyOnDrainFill_NullOrder_NoSideEffect):
Pre-existing at B157 baseline. Unrelated to any B158 ticket. Zero NEW failures introduced by B158. **Not a B158 violation.**

**Aggregate 7-scan result across src/PropTraderTools/: ALL ZERO.**

---

## 5. Missing Wiring Check

| Item | Check | Result |
|------|-------|--------|
| IsLeaderFlatDrainAbort wiring | Called unconditionally from TryEvictFollowerBeSlot at L1938 | PRESENT — no additional wiring needed |
| Pre-Gate-1 placement | TryEvictFollowerBeSlot called at OnOrderUpdate L1712, before Gate 1 at L1763 | CONFIRMED |
| No new methods added | Both tickets are read-only / annotation-only | CONFIRMED — no new method registrations required |
| No new event subscriptions | Neither ticket adds or removes event handlers | N/A |
| _pendingDispatchDrains key format | follower acc.Name, StringComparer.Ordinal (L387-388) | CONFIRMED |

**No missing wiring. No registration gaps.**

---

## 6. Additional Cross-File Coherence Observations

### 6.1 AbortDrainOnFill Lock-Free Confirmation

Source at L8343-8348 (referenced in architecture plan, confirmed by both engineer and verifier):
```csharp
// CYC=3: base(1) + TryRemove guard(1) + foreach(1). JS-021: no lock().
private void AbortDrainOnFill(string acctKey)
{
    if (_pendingDispatchDrains.TryRemove(acctKey, out var payload))
        foreach (var id in payload.DrainedOrderIds)
            _drainOwnedOrderIds.TryRemove(id, out _);
}
```
CYC=3. No lock(). JS-021 compliant. **PASS.**

### 6.2 _pendingDispatchDrains Immutability

Field at L387-388: `private readonly ConcurrentDictionary<string, PendingDispatchDrain>` with `StringComparer.Ordinal`.
`readonly` prevents reassignment. `ConcurrentDictionary` provides lock-free mutation. JS-009 compliant.
**PASS.**

### 6.3 SIM Marker Integrity

The exact marker `[DRAIN-ABORT-LEADER-FLAT]` at L1969 is distinct from `[DRAIN-ABORT-FLAT]`
(Fix1 marker at SubmitDrainedEntry L8319). Both engineer and verifier independently confirmed
the correct marker. The SIM step documentation in ticket-1-completion.md calls this out in a
prominently labelled "CRITICAL MARKER DISTINCTION" section. No risk of marker confusion.

---

## Section K — Deferred Work

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-B154-05 | IsPttCopyEntryFilled delegation refactor (CYC improvement) | P3 | Future CYC block | OPEN |
| DW-B154-06 | TryTriggerBeRecovery post-deployment monitor (no new evidence) | P2 | Post-deployment | OPEN |
| DW-B155-01 | B155 [DRAIN-ABORT-FLAT] SIM gate (Fix1 marker) — separate from B158 T1 SIM | P2 | Post-deployment | OPEN |
| DW-BUG-0911-A-01 | Mid-close race condition in OnOrderUpdate | P2 | Future drain-path block | OPEN |
| DW-B157-COMMENT-01 | Stale comment at CopyEngine.cs:6517 | P3 | B158 T2 | **CLOSED** — corrected by B158 T2 |
| DW-B156-01 | Leader-flat drain abort (ghost re-entry fix) — implementation confirmed; SIM pending | P0 | B158 T1 / Director | IMPLEMENTATION_CONFIRMED / SIM_PENDING_DIRECTOR |
| DW-B158-SIM-01 | B158 T1 SIM gate — Director to execute 5-step SIM scenario. See ticket-1-completion.md SIM Gate Documentation. PASS = DW-B156-01 formally CLOSED. FAIL = open DW-B158-DRAIN-RACE-01. | P0 | Director (next SIM session) | OPEN — PENDING DIRECTOR EXECUTION |
| DW-B158-CYC6-STALE-01 | Stale CYC=6 comment at ~L1904 for TryEvictFollowerBeSlot (pre-DW-B156-DEFER-1, authoritative CYC=7 at L1926). Annotation-only cleanup. | P3 | Future annotation block | OPEN |
| DW-B158-DRAIN-RACE-01 | Leader-flat drain abort timing race: cancel-ack pre-consumes drain before IsLeaderFlatDrainAbort fires. CONDITIONAL — open only if DW-B158-SIM-01 produces FAIL. | P1 (conditional) | Future drain-path block | CONDITIONAL — NOT YET OPENED |

### Section K Notes

1. **DW-B157-COMMENT-01 CLOSED**: B158 T2 corrected the stale comment at CopyEngine.cs:6517.
   New text: `// Returns true iff the current dispatch reverses the last direction AND the follower is NOT flat (open position + reversal = protection path).`
   Old text absent. VERIFY_PASS confirmed. This item is fully retired.

2. **DW-B156-01 disposition**: Implementation is confirmed at source and unit-test level.
   Formal closure requires Director to run the 5-step SIM gate (documented in ticket-1-completion.md).
   If SIM PASS → DW-B156-01 CLOSED, DW-B158-SIM-01 CLOSED.
   If SIM FAIL → DW-B158-DRAIN-RACE-01 opened, DW-B156-01 remains OPEN pending architectural fix.
   If SIM INCONCLUSIVE → escalate to Director; DW-B156-01 remains OPEN.

3. **DW-B158-DRAIN-RACE-01 is conditional**: Do NOT open this defect card unless the SIM gate
   confirms the timing-race scenario (Step 3 log absent AND Step 4 ghost entry present).

4. **DW-B155-01 is NOT closed by B158 T1**: The two SIM gates are entirely separate.
   DW-B155-01 = SubmitDrainedEntry follower-flat guard (Fix1, marker `[DRAIN-ABORT-FLAT]`).
   DW-B158-SIM-01 = IsLeaderFlatDrainAbort (Fix3, marker `[DRAIN-ABORT-LEADER-FLAT]`).
   Do not conflate them.

---

## Summary

| Phase | Document | Verdict |
|-------|----------|---------|
| Phase 2 | docs/brain/B158/02-plan-review.md | REVIEW_PASS |
| Phase 3.5 | docs/brain/B158/04-ticket-review.md | TICKET_REVIEW_PASS |
| Phase 4a T1 | docs/brain/B158/ticket-1-completion.md | BUILD_PASS |
| Phase 4b T1 | docs/brain/B158/ticket-1-verification.md | VERIFY_PASS |
| Phase 4a T2 | docs/brain/B158/ticket-2-completion.md | BUILD_PASS |
| Phase 4b T2 | docs/brain/B158/ticket-2-verification.md | VERIFY_PASS |
| Phase 5 | docs/brain/B158/05-final-review.md (this file) | **FINAL_PASS** |

**Block B158 FINAL_PASS.**
DW-B157-COMMENT-01 CLOSED. DW-B156-01 IMPLEMENTATION_CONFIRMED / SIM_PENDING_DIRECTOR.
All 7 scans ZERO across src/PropTraderTools/. Zero new JS/NT8 violations. Zero missing wiring.
Section K written. 06-deferred-backlog.md written.

---

_Final review authored by ptt-plan-reviewer (Phase 5). Block B158. 2026-09-15._
_Source cross-referenced: CopyEngine.cs L1929-1974 (T1 region), L6514-6528 (T2 region)._
_Prior backlog read: docs/brain/B157/06-deferred-backlog.md._
_Input files read: 02-architecture-plan.md, 02-plan-review.md, 04-tickets.md, 04-ticket-review.md,_
_ticket-1-completion.md, ticket-1-verification.md, ticket-2-completion.md, ticket-2-verification.md._
