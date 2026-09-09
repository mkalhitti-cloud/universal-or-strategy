# PTT-REPAIRS-04-POST-BUG-E Final Review

**Epic:** PTT-REPAIRS-04-POST-BUG-E
**Phase:** 5 -- Final Review
**Reviewer:** ptt-plan-reviewer
**Date:** 2026-09-06
**Scope:** SOURCE VERIFICATION ONLY -- no .cs edits. BUG-E fix confirmed present in CopyEngine.cs.

---

## Section A -- Gate Chain Confirmation

| Gate | Document | Verdict | Date |
|------|----------|---------|------|
| REVIEW_PASS (Ph2) | docs/brain/PTT-REPAIRS-04-POST-BUG-E/02-plan-review.md | CONFIRMED | 2026-09-06 |
| TICKET_REVIEW_PASS (Ph3.5) | docs/brain/PTT-REPAIRS-04-POST-BUG-E/04-ticket-review.md | CONFIRMED (all 15 checklist items) | 2026-09-06 |
| BUILD_PASS (Ph4a) | docs/brain/PTT-REPAIRS-04-POST-BUG-E/ticket-1-completion.md | CONFIRMED (zero CopyEngine errors from Scan 4) | 2026-09-06 |
| VERIFY_PASS (Ph4b) | docs/brain/PTT-REPAIRS-04-POST-BUG-E/ticket-1-verification.md | CONFIRMED (all 8 steps, all 7 scans, zero discrepancies) | 2026-09-06 |

All four gates confirmed. Pipeline is coherent end-to-end.

---

## Section B -- Cross-File Coherence Check

### B.1 -- TryClearLeaderDirectionOnFlat vs EvictDedup TryRemove

**TryClearLeaderDirectionOnFlat** is located at [`CopyEngine.cs:4309`](src/PropTraderTools/CopyEngine.cs:4309).

```csharp
private void TryClearLeaderDirectionOnFlat(Account acc, string instrFullName)
{
    // ... leader account check ...
    if (isLeaderAcct)
    {
        _lastLeaderDirection.TryRemove(instrFullName, out _);        // line 4322
        ClearLiveEntryForInstrument(instrFullName);
    }
}
```

**EvictDedup** BUG-E TryRemove is at [`CopyEngine.cs:5882`](src/PropTraderTools/CopyEngine.cs:5882):

```csharp
_lastLeaderDirection.TryRemove(cancelledInstrKey.Substring(0, pipeIdx), out _);
```

**Conflict assessment:**

- Both callers use `ConcurrentDictionary.TryRemove`, which is documented thread-safe and idempotent. A double-remove (both called for the same key) returns `true` then `false`; neither throws, neither corrupts state.
- `TryClearLeaderDirectionOnFlat` is called from the position-state event path (line 4271) when the leader goes flat.
- `EvictDedup` Cancelled branch is called from `OnOrderUpdate` when an entry order is cancelled.
- These are two independent cleanup paths triggered by different events. There is no ordering dependency between them: if both fire for the same instrument (leader order cancelled AND leader goes flat simultaneously), the result is `_lastLeaderDirection` having no key for that instrument -- which is the correct post-state for both paths.
- **NO CONFLICT.** Both are safe TryRemove callers. ConcurrentDictionary.TryRemove is idempotent.

### B.2 -- DispatchCopy unconditional write: sole production writer

**Grep result for `_lastLeaderDirection[`:**

| Line | Content |
|------|---------|
| 2555 | `_lastLeaderDirection[instr.FullName] = currentAction;` |
| 4334 | `_lastLeaderDirection[instrFullName] = action;` (test accessor only) |

Line 4334 is [`SetLeaderDirection_ForTest`](src/PropTraderTools/CopyEngine.cs:4333) -- a DW-B135 test seam accessor (`internal void SetLeaderDirection_ForTest(...)`). It is annotated `// DW-B135 test accessors -- no logic, thin shims only.` This is test-infrastructure code, not a production write path.

**Production write sites: 1.** Line 2555 inside `DispatchCopy` is the only production writer. The plan claim (Section 2) is **correct**.

### B.3 -- ShouldSkipForReversalGuard reads _lastLeaderDirection via TryGetValue (read-only)

**`ShouldSkipForReversalGuard`** signature is at [`CopyEngine.cs:2604`](src/PropTraderTools/CopyEngine.cs:2604). The method takes `hasLastDirection bool` and `lastAction OrderAction` as parameters -- both are already resolved by the time `ShouldSkipForReversalGuard` is called.

The **actual TryGetValue read** occurs upstream at [`CopyEngine.cs:2530`](src/PropTraderTools/CopyEngine.cs:2530):

```csharp
bool hasLastDirection = _lastLeaderDirection.TryGetValue(
    instr.FullName,
    out OrderAction lastAction
);
```

`ShouldSkipForReversalGuard` itself receives `hasLastDirection` and `lastAction` as caller-resolved values; it does not access `_lastLeaderDirection` directly. The read site at line 2530 is a pure `TryGetValue` -- **read-only**. No write to `_lastLeaderDirection` occurs in `ShouldSkipForReversalGuard` or in `DispatchCopy` before the unconditional write at line 2555. **Confirmed read-only.**

### B.4 -- BUG-E addition is the only modification to EvictDedup

Lines 5878-5882 are the entirety of the BUG-E change. Verification confirms:

- No other method in CopyEngine.cs was modified by this pipeline (ticket scope = SOURCE VERIFICATION ONLY; the fix was applied in the prior PTT-REPAIRS-04 session).
- The Cancelled branch structure outside lines 5878-5882 is unchanged from the pre-BUG-E state.
- The Filled branch (lines 5886-5899) is unchanged.
- The terminal-state guard (lines 5853-5858) and `_dedupCache.TryRemove` (line 5860) are unchanged.

**Confirmed: lines 5878-5882 are the only BUG-E addition. No other method was modified.**

---

## Section C -- JS Compliance Summary Table (Post-Fix State of EvictDedup)

| Rule | Description | Status | Notes |
|------|-------------|--------|-------|
| JS-021 | No lock() | PASS | Zero lock() in EvictDedup. Scan 1: 63 matches, all comment lines. |
| JS-001 | No unguarded throw | PASS | No throw statements. TryRemove returns false on absent key; Substring protected by pipeIdx > 0 guard. |
| JS-013 | CYC <= 8 | DEFERRED | CYC=13 post-fix (D=12). Pre-existing violation: CYC=12 (D=11) before BUG-E. BUG-E adds +1. Extraction to EvictCancelledEntry/EvictFilledEntry planned in DEFERRED-4. |
| JS-042 | ASCII-only new lines | PASS | Lines 5878-5882: all 7-bit ASCII. "--" = two U+002D hyphens. Scan 2: zero non-ASCII matches file-wide. |
| JS-025 | ConcurrentDictionary lock-free | PASS | TryRemove is lock-free. No monitor, mutex, or semaphore anywhere in EvictDedup. |
| JS-002 | No blocking I/O | PASS | EvictDedup returns void; no I/O, no null return site. |

---

## Section D -- Post-Fix Evidence (from docs/output.md)

**Lines 14-16 (first MGC Sell dispatch after initial cancel cycle):**
```
[PTT-COPY] dispatch: Sell x2 MGC DEC26 -> Sim102 mult=1 mode=Named name=Entry
[PTT-COPY] dispatch: Sell x2 MGC DEC26 -> Sim103 mult=1 mode=Named name=Entry
[PTT-COPY] dispatch: Sell x2 MGC DEC26 -> Sim104 mult=1 mode=Named name=Entry
```
MGC Sell dispatched on first attempt -- no `[PTT-COPY-GUARD]` skip line. **CONFIRMED.**

**Lines 43-46 (second MGC Sell dispatch after second cancel cycle):**
```
[PTT-COPY] dispatch: Sell x2 MGC DEC26 -> Sim102 mult=1 mode=Named name=Entry
[PTT-COPY] dispatch: Sell x2 MGC DEC26 -> Sim103 mult=1 mode=Named name=Entry
[PTT-COPY] dispatch: Sell x2 MGC DEC26 -> Sim104 mult=1 mode=Named name=Entry
```
MGC Sell dispatched on first attempt -- no skip. **CONFIRMED.**

**Lines 164-166 (MGC Sell after MES SEP26 trades at lines 104-136):**
```
[PTT-COPY] dispatch: Sell x2 MGC DEC26 -> Sim102 mult=1 mode=Named name=Entry
[PTT-COPY] dispatch: Sell x2 MGC DEC26 -> Sim103 mult=1 mode=Named name=Entry
[PTT-COPY] dispatch: Sell x2 MGC DEC26 -> Sim104 mult=1 mode=Named name=Entry
```
MGC Sell dispatched on first attempt after MES trades -- no skip. **CONFIRMED.**

**Zero `[PTT-COPY-GUARD]` skip entries:** A full scan of docs/output.md (211 lines) finds zero occurrences of `[PTT-COPY-GUARD]`. Every MGC Sell entry dispatches immediately. **CONFIRMED.**

The BUG-E symptom -- reversal-block on new Sell entry after cancelled Sell entry -- is absent from the entire post-fix log.

---

## Section E -- Spec Requirements Satisfied

| Requirement | Resolution | Evidence |
|-------------|-----------|----------|
| PTT-REPAIRS-04-BUG-E: `_lastLeaderDirection` stale on entry cancel causes spurious reversal-block skip on next entry | RESOLVED | Fix at CopyEngine.cs:5878-5882 confirmed present by Ph4a (ticket-1-completion.md) and Ph4b (ticket-1-verification.md, VERIFY_PASS). Post-fix log (docs/output.md) shows zero [PTT-COPY-GUARD] skip events across 211 log lines including 4+ cancel-and-retry cycles. |

---

## Section F -- Section K: Deferred Work

All items carried forward from this pipeline to 06-deferred-backlog.md.

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-REPAIRS-04-BUG-E-01 | Option A test runner wiring: PropTraderTools.Tests.csproj setup + EvictDedup_CancelledEntry_ClearsLastLeaderDirection [Fact] test | P1 | B6/future | OPEN |
| DW-REPAIRS-04-BUG-E-02 | TOCTOU window in value-guarded TryRemove (carried from DW-REPAIRS-03-POST-01). Acceptable under NT8 single-threaded OnOrderUpdate model. | P2 | future | OPEN |
| DW-REPAIRS-04-BUG-E-03 | BUG-F pipeline (reference). Covered by parallel PTT-REPAIRS-04-POST-BUG-F. | P1 | Active (parallel) | OPEN |
| DW-REPAIRS-04-BUG-E-04 | EvictDedup CYC refactoring. CYC=13 post-fix violates JS-013 (limit 8). Extraction: EvictCancelledEntry [CYC~5] + EvictFilledEntry [CYC~4] + residual EvictDedup [CYC~6]. | P1 | B6/future | OPEN |

---

## Final Assessment

All pipeline gates confirmed. All spec requirements satisfied. Cross-file coherence confirmed (two independent TryRemove callers on ConcurrentDictionary -- no conflict; sole production writer at line 2555 confirmed; ShouldSkipForReversalGuard is read-only via parameters resolved from TryGetValue at line 2530; BUG-E addition is the only modification). Post-fix evidence log confirms zero reversal-block skips across 4+ cancel-and-retry cycles. Section K present. 06-deferred-backlog.md required.

**FINAL_PASS**
