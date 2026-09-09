# Ticket Review: PTT-REPAIRS-03

**Reviewer**: ptt-ticket-reviewer
**Block**: PTT-REPAIRS-03
**Tickets file**: `docs/brain/PTT-REPAIRS-03/04-tickets.md`
**Revision reviewed**: Ticket Revision Cycle 1 (T2-TRACE-01, T2-TRACE-02, T2-TEST-01 corrections)
**Plan basis**: `docs/brain/PTT-REPAIRS-03/02-architecture-plan.md` (Revision 2 — V-02 fix)
**Plan review**: `docs/brain/PTT-REPAIRS-03/02-plan-review.md` (REVIEW_PASS — Cycle 2 FINAL)
**Source verified**: `src/PropTraderTools/CopyEngine.cs` and `src/PropTraderTools/CopyEngineTests.cs`
**Date**: 2026-09-08 (Cycle 1 re-review)

---

## PTT-REPAIRS-03-T1

### T1 STATUS: Unchanged since Cycle 0 — confirmation only.

### Traceability: PASS

- Block reference: PTT-REPAIRS-03 ✓
- Bug reference: BUG-A in Section B; DW-REPAIRS-03-01 explicitly promoted in-scope ✓
- Plan reference: header references "Revision 2 — V-02 corrected" REVIEW_PASS ✓
- Method names and line numbers verified against source:
  - `ShouldSkipForReversalGuard` at line 2584 ✓; change at line 2594
    (`bool followerIsFlat = IsFlat(FindPosition(acc, instr));` confirmed at source) ✓
  - Comment lines 2579–2580 (`// Returns true when...` / `// CCN<=3...`) confirmed at source ✓
  - `HasWorkingEntries` at line 4840 ✓; change at line 4842
    (`foreach (var order in acc.Orders)` — no `.ToList()`, confirmed) ✓
  - Comment at line 4839 (`// CYC=3`) confirmed as stale — correction accurately prescribed ✓
- All work traces to BUG-A spec requirement or DW-REPAIRS-03-01 (promoted in-scope) ✓
- No phantom work detected ✓
- No plan items for T1 omitted from ticket ✓

### JS Pre-Check: PASS

| Rule | Check | Result |
|------|-------|--------|
| JS-001 | `acc.Orders.ToList()` mandated at `HasWorkingEntries` line 4842; rationale cited | PASS ✓ |
| JS-002 | Both methods return `bool`; no null return path described | PASS ✓ |
| JS-021 | No `lock()` in any described change; `acc.Orders.ToList()` explicitly noted as lock-free | PASS ✓ |
| JS-023 | Both methods described as pure predicates (no side effects) | PASS ✓ |
| JS-042 | ASCII-only requirement stated in Section I | PASS ✓ |
| JS-066 | CYC budget explicitly stated in Section J and Section F for both methods | PASS ✓ |

### CYC Pre-Check: PASS

| Method | Before | After | Budget | Ticket Section |
|--------|--------|-------|--------|----------------|
| `ShouldSkipForReversalGuard` | 3 | 4 | ≤8 ✓ | Section J, Section E.1 comment |
| `HasWorkingEntries` | 5 (stale comment corrected) | 5 (no delta from `.ToList()`) | ≤8 ✓ | Section J, Section E.2 comment |

Both within budget. Comment correction for stale `// CYC=3` → `// CYC=5` accurately scoped ✓

### NT8 Check: PASS

- No `lock()` in any change ✓
- ASCII-only stated and enumerated for all changed lines ✓
- No `DateTime.Now`, `FontFamily`, hex color, `PTT-` order name, or `Dispatcher.InvokeAsync` applicable — each correctly noted N/A ✓
- 5 PTT-DIAG log lines listed as PERMANENT in Section K; `[PTT-COPY-GUARD]` log preservation correctly analyzed for T1 ✓

### Test Coverage: PASS

- Test method `ShouldSkipForReversalGuard_AllowsEntryWhenFollowerHasWorkingOrders` specified ✓
- Scenario accurately described: `followerIsFlat = IsFlat(true) && !HasWorkingEntries(true) = false` → guard returns `false` ✓
- Arrange/Act/Assert outline present with both Option A (preferred) and Option B (fallback) patterns ✓
- Instructs engineer to use existing test infrastructure; no new mocking framework ✓
- Both assert conditions stated (guard returns false; dispatch allowed) ✓

### Scan Checklist: PASS

| Scan | Present | Command | Pass Condition |
|------|---------|---------|----------------|
| SCAN-01 `lock()` grep | ✓ | Range-scoped grep on lines 2578–2610 and 4839–4852 | 0 matches |
| SCAN-02 Unicode/curly-quote grep | ✓ | `[^\x00-\x7F]` on same ranges | 0 matches |
| SCAN-03 CYC check | ✓ | Manual branch count with table | All ≤8 |
| SCAN-04 `[Fact]` count | ✓ | `Select-String ... Measure-Object` before/after | 475 → 476 (+1) |
| SCAN-05 Build | ✓ | `build_readiness.ps1` or `dotnet build` | 0 errors |
| SCAN-06 NT8-043 event handler | ✓ | Explicitly marked N/A with PR statement | N/A — no event handlers changed |
| SCAN-07 `.ToList()` check | ✓ | `acc\.Orders\.ToList\(\)` on lines 4839–4852 | 1 match |

All 7 scans present ✓

### File Routing: PASS

- Both modified files (`src/PropTraderTools/CopyEngine.cs` and `src/PropTraderTools/CopyEngineTests.cs`)
  correctly route to Wave workspace ✓
- No Director workspace paths for `.cs` files ✓

### VERDICT: TICKET_REVIEW_PASS

---

## PTT-REPAIRS-03-T2

### Traceability: PASS

**T2-TRACE-01 RESOLVED** — verified:

- Section C Scope Lock now lists `IsLiveEntryBlocked_ForTest` shim as item #7:
  "1 test seam shim redirected" ✓
- Section D Files to Modify updated to "7 targeted locations" including shim redirect ✓
- Section E.3 provides full shim redirect body (method-body form, no longer expression-bodied
  single-line delegation to the deleted method) ✓
- Caller count correction verified: Section E.3 states "two callers exist" — (1) the shim at
  line 4306, (2) stale comment at line 201. Both explicitly identified and addressed ✓
  Source grep confirms: `IsLiveEntryBlocked` call at line 4306 and comment reference at line 201
  are the only two non-definition occurrences. Count = 2. Accurate ✓
- Gate5 check behavior preservation verified: shim calls `IsLiveEntryBlocked_Check` (same three
  guards as original: instrKey ContainsKey, IsDedup, _entryDispatchedOrders ContainsKey) then
  `SetLiveEntryDispatched` on pass. Semantics identical to original `IsLiveEntryBlocked` ✓

**T2-TRACE-02 RESOLVED** — verified:

- Section C Scope Lock now explicitly states: "Existing `[Fact]` `IsLiveEntryBlocked_ClearsOnFill_AllowsReentry`
  (line 7784): Updated via `IsLiveEntryBlocked_ForTest` shim redirection. The test is **NOT deleted**." ✓
- Section G.1 provides Approach X (preserve test, update shim only) with full justification ✓
- Disposition of existing test is unambiguous: test body unchanged, shim redirected ✓

All work traces to BUG-B, V-01 (DispatchCopy CYC), and the three prior violation items ✓
No phantom work detected ✓
No plan items omitted ✓

**WARN (non-blocking)**: Lines 2470–2471 in `DispatchCopy` are stale comments after T2. Line 2471
references `IsLiveEntryBlocked` (the deleted method); line 2470 references `IsEntryDispatched`
which will have zero call sites post-T2. These two comment lines are NOT listed in any of the four
`DispatchCopy` changes (Change 0 through Change 4). Compare: the ticket explicitly addresses the
analogous stale comment at line 201 (Section E.3). The inconsistency is noted. This is a code
quality warning — not a compile, behavioral, or safety failure. No FAIL triggered. Engineer should
update lines 2470–2471 as part of Change 1 (renaming line 2474 from `IsLiveEntryBlocked` to
`IsLiveEntryBlocked_Check`).

### JS Pre-Check: PASS

| Rule | Check | Result |
|------|-------|--------|
| JS-001 | No throw in any new method; `ContainsKey`/`TryAdd` are no-throw; explicitly stated per-method | PASS ✓ |
| JS-002 | `ShouldSkipFollower` and `IsLiveEntryBlocked_Check` return `bool`; no null returns | PASS ✓ |
| JS-021 | No `lock()` in any described change; all writes use `ConcurrentDictionary` | PASS ✓ |
| JS-023 | `IsLiveEntryBlocked_Check` and `ShouldSkipFollower` described as pure predicates; `SetLiveEntryDispatched` isolated as commit | PASS ✓ |
| JS-025 | No new `TryRemove` introduced; `EvictDedup` unchanged | PASS ✓ |
| JS-042 | ASCII-only requirement stated in Section I; all new identifiers verified ASCII | PASS ✓ |
| JS-066 | CYC budget explicitly stated in Section J per-method; all values ≤8 | PASS ✓ |

### CYC Pre-Check: PASS

| Method | Before | After | Budget | Ticket Section |
|--------|--------|-------|--------|----------------|
| `ShouldSkipFollower` (new) | — | 3 | ≤8 ✓ | Section J, E.1 |
| `IsLiveEntryBlocked_Check` (new) | — | 4 | ≤8 ✓ | Section J, E.4 |
| `SetLiveEntryDispatched` (new) | — | 1 | ≤8 ✓ | Section J, E.5 |
| `IsEntryDispatched` (simplified) | 2 | 1 | ≤8 ✓ | Section J, E.6 |
| `IsLiveEntryBlocked` (deleted) | 4 | deleted | — | Section J |
| `IsLiveEntryBlocked_ForTest` (redirected) | 1 (expr-bodied) | 2 (+1 if-branch) | ≤8 ✓ | Section J |
| `DispatchCopy` | 8 (measured) | 7 (post-extraction) → 8 (post-guard) | ≤8 ✓ | Section J, E.2 |

CYC arithmetic for `DispatchCopy` verified: 8 (pre-T2) → 7 (ShouldSkipFollower extraction collapses
two separate if-blocks to one) → 8 (if(dispatched > 0) post-loop guard +1). Final = 8 ≤ 8 ✓
All values within budget ✓

### NT8 Check: PASS

- No `lock()` in any described change ✓
- ASCII-only stated in Section I and per-method in Section E comments ✓
- All N/A NT8 items (DateTime.Now, FontFamily, hex color, PTT- prefix, Dispatcher) correctly noted ✓
- MGC guard preservation (DW-B142-MGC-02) explicitly verified: `SetLiveEntryDispatched` writes all
  three maps (`_liveEntryInstruments`, `_entryInstrKeyByOrderId`, `_entryDispatchedOrders`) that
  `EvictDedup` reads on fill/cancel. Gate5(a/b/c) semantics preserved end-to-end ✓
- 5 PTT-DIAG log lines listed as PERMANENT in Section K; gate5 log preservation correctly analyzed:
  log block at lines 2476–2482 is unchanged; only line 2474 call expression is renamed ✓

### Test Coverage: PASS

**T2-TEST-01 RESOLVED** — verified:

**Existing test `IsLiveEntryBlocked_ClearsOnFill_AllowsReentry` — assertion path verified against source**:

The test body (CopyEngineTests.cs lines 7784–7811) is unchanged. Assertion correctness after T2
verified by tracing the full execution path through the new shim and `EvictDedup`:

| Test line | Assertion | Chain after T2 | Status |
|-----------|-----------|----------------|--------|
| 7796 | `blocked1 = IsLiveEntryBlocked_ForTest(instrKey, orderId1, 0.0)` → `Assert.False` | `IsLiveEntryBlocked_Check`: `_liveEntryInstruments.ContainsKey(instrKey)` = false; `IsDedup(orderId1)` TryAdds → returns false; `_entryDispatchedOrders.ContainsKey(orderId1)` = false → returns false. Then `SetLiveEntryDispatched` TryAdds instrKey, orderId1, orderId1 to all three maps. | PASS ✓ |
| 7800 | `Assert.True(LiveEntryInstrumentsContains_ForTest(instrKey))` | `_liveEntryInstruments.ContainsKey(instrKey)` = true (written by `SetLiveEntryDispatched` in shim's commit path) | PASS ✓ |
| 7803 | `EvictDedup_ForTest(orderId1, Filled)` | Source lines 5800–5806: `_dedupCache.TryRemove(orderId1)` → `_entryInstrKeyByOrderId.TryRemove(orderId1, out filledInstrKey)` succeeds (was written) → `_liveEntryInstruments.TryRemove(filledInstrKey)` succeeds | PASS ✓ |
| 7806 | `Assert.False(LiveEntryInstrumentsContains_ForTest(instrKey))` | `_liveEntryInstruments.ContainsKey(instrKey)` = false (cleared by EvictDedup at 7803) | PASS ✓ |
| 7809 | `blocked2 = IsLiveEntryBlocked_ForTest(instrKey, orderId2, 0.0)` → `Assert.False` | `IsLiveEntryBlocked_Check`: instrKey not in `_liveEntryInstruments` (cleared); `IsDedup(orderId2)` TryAdds → false; `_entryDispatchedOrders.ContainsKey(orderId2)` = false. Returns false. | PASS ✓ |

All 5 assertions in the existing test pass through the redirected shim. Test is preserved,
not modified, not deleted ✓

**New test `DispatchCopy_DoesNotSetPhantomInstrKey_WhenAllFollowersSkipped`**:
- Section G.2 specifies the test method name ✓
- Scenario: all followers skipped (`dispatched == 0`) → `SetLiveEntryDispatched` NOT called →
  `_liveEntryInstruments` does NOT contain instrKey ✓
- Arrange/Act/Assert outline present with skip mechanism options ✓
- Two assertions specified: (1) instrKey not in `_liveEntryInstruments`; (2) subsequent
  `IsLiveEntryBlocked_Check` returns false ✓
- Uses existing test infrastructure (reflection, InternalsVisibleTo at L46) ✓
- No new mocking framework required ✓

**[Fact] delta arithmetic**: 475 baseline → +1 (T1: `ShouldSkipForReversalGuard_AllowsEntryWhenFollowerHasWorkingOrders`) = 476 → +1 (T2 new: `DispatchCopy_DoesNotSetPhantomInstrKey_WhenAllFollowersSkipped`) = 477. Existing test preserved (delta = 0). Final target = 477 ✓

### Scan Checklist: PASS

| Scan | Present | Command | Pass Condition |
|------|---------|---------|----------------|
| SCAN-01 `lock()` grep | ✓ | Whole-file grep; 0 matches project invariant | 0 matches in `src/PropTraderTools/` |
| SCAN-02 Unicode/curly-quote grep | ✓ | `[^\x00-\x7F]` on changed lines | 0 matches in T2-changed lines |
| SCAN-03 CYC check | ✓ | Manual branch count table (6 methods: ShouldSkipFollower=3, IsLiveEntryBlocked_Check=4, SetLiveEntryDispatched=1, IsEntryDispatched=1, IsLiveEntryBlocked_ForTest=2, DispatchCopy=8) | All ≤8 |
| SCAN-04 `[Fact]` count | ✓ | `Select-String ... Measure-Object` 476 → 477 | +1 delta (existing test preserved) |
| SCAN-05 Build | ✓ | `build_readiness.ps1` or `dotnet build`; note: redirect shim BEFORE delete is mandatory order | 0 errors |
| SCAN-06 NT8-043 event handler | ✓ | Explicitly marked N/A with PR statement | N/A — no event handlers changed |
| SCAN-07 `.ToList()` scope | ✓ | Explicitly marked N/A (T2 does not touch `HasWorkingEntries`) | N/A (T2) |

All 7 scans present ✓

SCAN-05 note: Section H correctly documents the implementation order constraint —
"IsLiveEntryBlocked_ForTest shim must be redirected (E.3) before IsLiveEntryBlocked is deleted.
Deleting IsLiveEntryBlocked without redirecting the shim = compile error = SCAN-05 FAIL." ✓

### File Routing: PASS

- All modified and new files correctly route to Wave workspace `src/PropTraderTools/` ✓
- No Director workspace paths for `.cs` files ✓

### VERDICT: TICKET_REVIEW_PASS

---

## Overall: TICKET_REVIEW_PASS

**T1**: TICKET_REVIEW_PASS — all checks pass. No changes since Cycle 0.

**T2**: TICKET_REVIEW_PASS — all three Cycle 0 violations resolved:

| Prior Violation | Resolution | Status |
|-----------------|------------|--------|
| T2-TRACE-01 | `IsLiveEntryBlocked_ForTest` added to Section C (item #7) and Section D; Section E.3 provides full redirect body; caller count corrected to 2 (shim + stale comment) | RESOLVED ✓ |
| T2-TRACE-02 | Existing `[Fact]` `IsLiveEntryBlocked_ClearsOnFill_AllowsReentry` explicitly identified in Section C as "NOT deleted"; Section G.1 provides full Approach X disposition with assertion-by-line analysis | RESOLVED ✓ |
| T2-TEST-01 | Assertion path verified: `SetLiveEntryDispatched` writes `_liveEntryInstruments` → `EvictDedup` on fill clears via `_entryInstrKeyByOrderId` → second `IsLiveEntryBlocked_Check` returns false. All 5 assertions pass. `[Fact]` delta arithmetic correct (476 → 477) | RESOLVED ✓ |

**Block gate**: TICKET_REVIEW_PASS.

Engineer handoff is **APPROVED**. Apply T1 first, then T2. Run hard-link sync once after both
tickets are implemented and SCAN-05 build passes (per Section L of each ticket).

---

*ptt-ticket-reviewer · PTT-REPAIRS-03 · 04-ticket-review.md · Cycle 1 re-review · 2026-09-08*
*Source verified: CopyEngine.cs lines 175–5809 · CopyEngineTests.cs lines 7780–7813*
*WARN (non-blocking, T2): stale comments at CopyEngine.cs lines 2470–2471 not explicitly scoped;*
*engineer should update during Change 1 (renaming line 2474 IsLiveEntryBlocked → IsLiveEntryBlocked_Check)*
*Cycle 0 violations: T2-TRACE-01, T2-TRACE-02, T2-TEST-01 — all RESOLVED in Cycle 1 revision*
