# PTT-REPAIRS-02 Final Review
**Reviewer**: ptt-plan-reviewer
**Phase**: 5 (Final Review)
**Epic**: PTT-REPAIRS-02
**Date**: 2026-09-07
**Verdict**: FINAL_PASS

---

## Documents Read

| Document | Path | Status |
|----------|------|--------|
| Architecture Plan | `docs/brain/PTT-REPAIRS-02/02-architecture-plan.md` | READ |
| Ticket Review | `docs/brain/PTT-REPAIRS-02/04-ticket-review.md` | READ |
| Ticket 1 Completion | `docs/brain/PTT-REPAIRS-02/ticket-1-completion.md` | READ |
| Ticket 1 Verification | `docs/brain/PTT-REPAIRS-02/ticket-1-verification.md` | READ |
| RULES_CATALOG | `docs/protocol/RULES_CATALOG.md` | FILE NOT FOUND — rules applied from hardcoded role DNA block (same authoritative source) |
| Prior Backlog | `docs/brain/PTT-REPAIRS-01/06-deferred-backlog.md` | READ |
| CopyEngine.cs (final state) | `src/PropTraderTools/CopyEngine.cs` | READ (lines 4260-4282, 5735-5780) |
| CopyEngineTests.cs (final state) | `src/PropTraderTools/CopyEngineTests.cs` | READ (lines 7775-7814) |

---

## Section A: Coherent System Check

### A1. Does the implemented fix correctly resolve the alternating-dispatch defect?

**CONFIRMED. PASS.**

Root cause: `_liveEntryInstruments[instrKey]` was never cleared on `OrderState.Filled`, causing every alternate `DispatchCopy` call for the same instrument+direction to be blocked at Gate 5 check (a) (`_liveEntryInstruments.ContainsKey(instrKey)`).

Fix verified in source at `CopyEngine.cs` lines 5762-5769:
```csharp
if (state == OrderState.Filled)
{
    // PTT-REPAIRS-02: clear instrKey on fill -- entry lifecycle complete, followers dispatched.
    // Mirrors Cancelled branch. ClearLiveEntryForInstrument remains as secondary guard.
    // MGC cancel+resubmit guard provided by _entryDispatchedOrders (DW-B91-A) -- not instrKey.
    if (_entryInstrKeyByOrderId.TryRemove(orderId, out var filledInstrKey))
        _liveEntryInstruments.TryRemove(filledInstrKey, out _);
}
```

The old "Do NOT remove _liveEntryInstruments key" comment is confirmed gone (grep returned 0 matches).

### A2. Is the fix internally consistent with the architecture plan and ticket design?

**CONFIRMED. PASS.**

Verifier Section 7c states: "NO DEVIATIONS FOUND. Implementation is an exact match to plan Section 5 and plan Section 9."

All 7 plan-specified changes are present in source:
- Header comment updated to CYC=6 at line 5738 ✓
- Old comments ("Do NOT remove", "trade is live", "authoritative cleanup") removed ✓
- Old single `TryRemove(orderId, out _)` (discarding key) replaced ✓
- New `if (_entryInstrKeyByOrderId.TryRemove(orderId, out var filledInstrKey))` at L5767 ✓
- New `_liveEntryInstruments.TryRemove(filledInstrKey, out _)` at L5768 ✓

Minor cosmetic deviation: plan says "filledInstrKey-TryRemove(5)" in the header comment; implementation says "filledInstrKey-remove(5)". Functionally equivalent. Acceptable per verifier.

### A3. Cross-file consistency (CopyEngine.cs ↔ CopyEngineTests.cs)?

**CONFIRMED. PASS.**

Test `IsLiveEntryBlocked_ClearsOnFill_AllowsReentry` confirmed present at `CopyEngineTests.cs` line 7785 with `[Fact]` attribute at line 7784. All 4 seam methods used by the test are confirmed present in `CopyEngine.cs` at lines 4264-4280 (independently verified by verifier Section 5d). The test exercises exactly the fixed code path: `EvictDedup_ForTest(orderId1, OrderState.Filled)` (Step 3) → `LiveEntryInstrumentsContains_ForTest(instrKey) == false` (Step 4). Cross-file seam contract is intact.

---

## Section B: Cross-File JS Violations

### B1. Any new JS violations introduced?

**NONE. PASS.**

Change is confined to `EvictDedup` Filled block (2 lines of executable code) and 1 test method appended to `CopyEngineTests.cs`.

### B2. JS-021: New ConcurrentDictionary race via stale reference?

**NO NEW RACE. PASS.**

`_entryInstrKeyByOrderId.TryRemove` is atomic — the captured `out var filledInstrKey` is the value removed from the map in a single lock-free operation. There is no window between the TryRemove returning `true` and the `filledInstrKey` variable being set. The pattern mirrors the Cancelled branch (lines 5758-5759), which has been in production and reviewed across multiple prior blocks. Whole-file grep for `lock\s*(` confirms all 70+ matches are in comment strings — zero `lock()` in executable code.

### B3. JS-025: New silent exception swallow?

**NO SWALLOW. PASS.**

`ConcurrentDictionary.TryRemove` does not throw. No `try/catch` is introduced. No exception path exists to swallow. Both TryRemove calls are lock-free and guaranteed non-throwing per .NET contract.

---

## Section C: Spec Requirements Satisfied

| Requirement | Status | Evidence |
|-------------|--------|----------|
| PTT-REPAIRS-02-T1: Alternating-dispatch defect fixed (rapid re-entry after fill now permitted) | **SATISFIED** | `_liveEntryInstruments.TryRemove(filledInstrKey)` confirmed at CopyEngine.cs L5768; verifier manual trace confirms second dispatch passes Gate 5 check (a) |
| MGC cancel+resubmit guard intact (DW-B142-MGC-02 preserved) | **SATISFIED** | Cancelled branch at L5758-5759 confirmed untouched; `_entryDispatchedOrders` guard at L5755 confirmed untouched; verifier Section 3 full scenario table confirmed |
| `[Fact]` test present and behavioral (`IsLiveEntryBlocked_ClearsOnFill_AllowsReentry`) | **SATISFIED** | Test at CopyEngineTests.cs L7784-7811; 5-step assertion confirmed; delta +1 confirmed by engineer (474→475) and verifier independent count (475) |

---

## Section D: All 7 Scans Zero

### D1. Layer 2 (engineer) vs Layer 3 (verifier) agreement

| Scan | Engineer Result | Verifier Result | Match |
|------|----------------|-----------------|-------|
| SCAN-01: `lock\s*(` in modified region (5735-5775) | 0 matches | 0 actual lock() in executable code (all comment-only hits whole-file) | **MATCH ✓** |
| SCAN-02: `DateTime\.Now` in modified region (5735-5775) | 0 matches | 0 in modified region (all comment-only hits whole-file) | **MATCH ✓** |
| SCAN-03: `return null` in modified region (5710-5775) | 0 matches | 0 in region | **MATCH ✓** |
| SCAN-04: `async void` in modified region (5710-5775) | 0 matches | 0 (all comment-only hits whole-file) | **MATCH ✓** |
| SCAN-05: Non-ASCII chars in modified region (5735-5775) | 0 matches | 0 matches | **MATCH ✓** |
| SCAN-06: `?.\w+ -=` in modified region (5710-5775) | 0 matches | 0 matches | **MATCH ✓** |
| SCAN-07: CYC (manual count) | EvictDedup=6 (≤7), IsLiveEntryBlocked=4 (≤5) | EvictDedup=6, IsLiveEntryBlocked=4 (independent count) | **MATCH ✓** |

**No discrepancies between Layer 2 and Layer 3. All 7 scans zero in the modified region.**

### D2. Independent verification of whole-file scan status

Final reviewer confirms via direct grep of the source files:
- `lock\s*(` in `CopyEngine.cs`: 70+ matches, all in comment strings. Zero in executable code. **PASS.**
- `DateTime\.Now[^U]` in `CopyEngine.cs`: 7 matches, all in comment strings ("No DateTime.Now"). Zero in executable code. **PASS.**

---

## Section E: DNA Rule Compliance Cross-Check

| Rule | Description | Status |
|------|-------------|--------|
| JS-001 | No throw in hot paths (OnOrderUpdate/gate chain) | PASS — TryRemove is non-throwing; no try/catch introduced |
| JS-002 | No return null where value expected | PASS — EvictDedup is void; no nullable return path |
| JS-003 | No magic string for discriminated state | PASS — no magic strings introduced |
| JS-008 | No mutable struct fields / unfrozen brushes | N/A — no struct or brush usage |
| JS-009 | No Dictionary<K,V> for shared/thread-touched collection | PASS — ConcurrentDictionary pattern used throughout; unchanged |
| JS-010 | No public constructor on singleton | N/A — no constructor changes |
| JS-021 | No lock() | PASS — zero lock() in executable code (whole-file verified) |
| JS-023 | No UI update from off-thread without Dispatcher.InvokeAsync | N/A — no UI changes |
| JS-025 | ConcurrentDictionary is lock-free canonical | PASS — TryRemove pattern mirrors existing Cancelled branch |
| JS-066 (CYC ≤ 8) | Complexity budget | PASS — EvictDedup: 6 (≤7 budget); IsLiveEntryBlocked: 4 (≤5 budget) |
| NT8: async/await in lifecycle methods | No async introduced | PASS |
| NT8: Account.All in constructor | Not used | N/A |
| NT8: sealed TradeCopierWindow | Not touched | N/A |
| NT8: FontFamily override | Not introduced | PASS |
| NT8: Hardcoded #RRGGBB hex | Not introduced | PASS |
| NT8: CreateOrder without PTT- prefix | Not introduced | N/A |
| NT8: DateTime.Now (not UtcNow) | 0 in executable code | PASS |
| ASCII-only string literals | 0 non-ASCII in modified region | PASS |

---

## Section F: Build and Sync Status

| Check | Result |
|-------|--------|
| `build_readiness.ps1` | BUILD_PASS — no new errors in CopyEngine.cs or CopyEngineTests.cs |
| `deploy-sync.ps1` | SYNC COMPLETE — ASCII GATE PASS, DIFF GUARD PASS (9691 chars, within limit), SOVEREIGN AUDIT PASS |
| Pre-existing errors | Linting.csproj V12_002.*.cs NT8 assembly reference errors — confirmed pre-existing, not introduced by this ticket |

---

## Section G: [Fact] Count Reconciliation

### G1. Observed discrepancy

The architecture plan (Section 3 / Section 9) and ticket document (Section H) both state baseline = **300** and post-ticket count = **301**.

Engineer (Layer 2) actual scan: **474** before → **475** after (+1).
Verifier (Layer 3) independent scan: **475** confirmed.

### G2. Root cause of discrepancy

The plan was authored using the PTT-REPAIRS-01 architecture plan's stated baseline of 300 (from the PTT-COPIER-B26 era). However, the actual `CopyEngineTests.cs` file accumulated additional tests across multiple prior blocks (B24 through PTT-REPAIRS-01) that were merged into the working file before the PTT-REPAIRS-02 plan was written. The PTT-REPAIRS-01 deferred backlog (line 21) states "Final CopyEngineTests.cs [Fact] count: 483 (was 477)" — this count is inconsistent with the engineer's measured baseline of 474 before PTT-REPAIRS-02-T1. The discrepancy in the PTT-REPAIRS-01 backlog's 483 figure vs. the actual 474 is an unresolved measurement inconsistency from a prior block.

### G3. What matters for correctness

The delta of **+1** is confirmed by two independent measurements. The delta is correct. The baseline discrepancy between plan and file does not affect the correctness of the fix or the test.

### G4. Disposition

- The plan's 300/301 figures were written against a stale baseline. This is a **documentation artifact**, not a defect.
- The actual test file state (475 [Fact] attributes) and delta (+1) are confirmed correct.
- See Section K item DW-REPAIRS-02-02 for the prior-block count inconsistency to be investigated.

---

## Section H: MGC Cancel+Resubmit Guard End-to-End Trace

Verifier Section 3 independently traces the MGC scenario:

| Scenario | Guard | Status |
|----------|-------|--------|
| First dispatch: instrKey set in `_liveEntryInstruments` | TryAdd at Gate 5 | INTACT |
| MGC cancel: `EvictDedup(Cancelled)` clears instrKey | L5758-5759 Cancelled path | INTACT (untouched) |
| MGC resubmit NEW orderId: instrKey cleared → TryAdd succeeds → dispatched | Cancelled path clears, new orderId unblocked | INTACT |
| MGC resubmit BEFORE cancel: instrKey still set → blocked | instrKey not yet cleared | INTACT |
| `_entryDispatchedOrders` primary double-dispatch guard | L5755 / IsEntryDispatched at L5716 | INTACT (untouched) |
| Leader fill: instrKey cleared by fix | NEW: L5767-5768 Filled path | FIXED ✓ |

**No regression to MGC guard. DW-B142-MGC-02 preserved.**

---

## Section I: Scope Adherence

Engineer attestation confirmed: no changes to any file outside `src/PropTraderTools/CopyEngine.cs` and `src/PropTraderTools/CopyEngineTests.cs`. No changes to Cancelled branch (L5758-5759). No changes to `_entryDispatchedOrders` (L5755). No changes to `IsLiveEntryBlocked` or `ClearLiveEntryForInstrument`. Scope lock honored.

---

## Section J: Prior Backlog Items — Closed or Carried?

Review of all open items from `docs/brain/PTT-REPAIRS-01/06-deferred-backlog.md`:

| ID | Item | Action in PTT-REPAIRS-02 | Status |
|----|------|--------------------------|--------|
| DW-B24-01 | NT8-043 null-conditional unsubscription runtime crash confirmation | Out of scope. No event subscription changes in this block. SCAN-06 returned 0. | **OPEN — carried** |
| DW-B24-02 | Manual E2E runtime verification | Out of scope for automated test. PTT-REPAIRS-02 fix MUST be included in E2E session. | **OPEN — scope expanded (see Section K)** |
| DW-B24-03 | Skip-duplicate guard [Fact] | Out of scope. | **OPEN — carried** |
| DW-B25-01 | Companion field race on plain singleton refs | Out of scope. | **OPEN — carried** |
| DW-B26-01 | Reflection test upgrade Option B to Option A | Out of scope. | **OPEN — carried** |
| DW-REPAIRS-01-01 | R5 Account.All constructor-path risk | Out of scope. | **OPEN — carried** |
| DW-REPAIRS-01-02 | TryCancelBeOrders wrapper -1 path [Fact] | Out of scope. | **OPEN — carried** |

No items from prior blocks were addressed or closed in PTT-REPAIRS-02. All remain OPEN.

---

## Section K — Deferred Work (MANDATORY)

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-B24-01 | NT8-043 null-conditional unsubscription runtime crash confirmation. SCAN-07 zero across all PTT blocks to date. No new evidence this block. | P2 | B27 or future | OPEN |
| DW-B24-02 | Manual E2E runtime verification. **Scope expanded**: E2E session MUST verify PTT-REPAIRS-02 fix: (a) dispatch trade, fill the leader order, then dispatch same instrument+direction again — confirm followers receive second copy. (b) MGC cancel+resubmit scenario remains unaffected. Include alongside R1–R5 verification targets from PTT-REPAIRS-01 scope. | P1 | ASAP post-merge | OPEN |
| DW-B24-03 | Skip-duplicate guard [Fact] for `if (acc == leader) continue` guard. | P2 | B27 | OPEN |
| DW-B25-01 | Companion field race on `_pendingBeAccount` / `_pendingBeInstrument` plain singleton refs. | P3 | B28 or future | OPEN |
| DW-B26-01 | Reflection test upgrade Option B to Option A for `TradeCopierPanel_BeBufferBox_FieldDoesNotExist`. | P2 | B28 or future | OPEN |
| DW-REPAIRS-01-01 | R5 Account.All constructor-path risk in BuildRuleRow. | P2 | B28 or future | OPEN |
| DW-REPAIRS-01-02 | TryCancelBeOrders -1 path [Fact] test requires mock Account. | P2 | B28 or future | OPEN |
| DW-REPAIRS-02-01 | `_entryDispatchedOrders` NOT cleared in Filled branch of EvictDedup. Comment at L5770 defers to `TryEvictFollowerBeSlot`. If `TryEvictFollowerBeSlot` does not fire (no BE slot), orderId lingers. Pre-existing; not introduced by this fix. Runtime E2E verification should confirm orderId is released correctly across full fill lifecycle. Include in DW-B24-02 E2E session. | P2 | B28+ / DW-B24-02 E2E | OPEN |
| DW-REPAIRS-02-02 | [Fact] count baseline inconsistency: PTT-REPAIRS-01 backlog (line 21) states "483 (was 477)" but engineer and verifier both independently measured 474 before PTT-REPAIRS-02-T1. Discrepancy of 9 tests unexplained. May reflect a rollback or rebase between PTT-REPAIRS-01 close and PTT-REPAIRS-02 start. Recommend: at next block start, run `Select-String -Pattern '^\s*\[Fact\]' \| Measure-Object` on `CopyEngineTests.cs` and record the authoritative baseline before writing the architecture plan. | P3 | Next block start | OPEN |

**Items closed this block**: None. No prior backlog items were in scope for PTT-REPAIRS-02.

**E2E verification note**: DW-B24-02 now covers the following scenarios for the next E2E session:
1. Ctrl+Shift+B BreakEven fire (original DW-B24-02 scope)
2. PTT-REPAIRS-01 R1–R5 runtime behaviors
3. **PTT-REPAIRS-02**: dispatch → fill → dispatch same instrument+direction (confirm second copy fires)
4. **DW-REPAIRS-02-01**: confirm orderId released via TryEvictFollowerBeSlot across full fill lifecycle

---

## Final Verdict

| Check | Result |
|-------|--------|
| A: Coherent system — fix resolves alternating-dispatch defect | **PASS** |
| A: Consistent with plan and ticket design | **PASS** |
| A: Cross-file consistency CopyEngine.cs ↔ CopyEngineTests.cs | **PASS** |
| B: No new JS-021 ConcurrentDictionary race | **PASS** |
| B: No new JS-025 silent exception swallow | **PASS** |
| C: Alternating-dispatch defect fixed | **PASS** |
| C: MGC cancel+resubmit guard intact | **PASS** |
| C: [Fact] test present and behavioral | **PASS** |
| D: All 7 scans zero — Layer 2 and Layer 3 in agreement | **PASS** |
| E: All applicable DNA rules pass | **PASS** |
| F: Build and sync pass | **PASS** |
| K: Section K present with all deferred items | **PASS** |
| 06-deferred-backlog.md written | **PASS** |

## FINAL_PASS

All spec requirements satisfied. No rule violations found. No cross-file consistency issues. All 7 scans zero with no discrepancies between engineer and verifier. Section K complete. `06-deferred-backlog.md` required — see companion output.

---

*ptt-plan-reviewer · PTT-REPAIRS-02 · Phase 5 Final Review · 2026-09-07*
