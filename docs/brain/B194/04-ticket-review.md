# Ticket Review: B194

**Epic**: B194 -- Fix 4 confirmed NakedPositionDetector + DrainThenDispatch bugs
**Reviewer**: ptt-ticket-reviewer (Phase 3.5, Cycle 2)
**Date**: 2026-10-07
**Input**: docs/brain/B194/04-tickets.md (revised -- T2 HasNakedPosition promoted to internal static)
**Plan authority**: docs/brain/B194/02-architecture-plan.md (REVIEW_PASS)
**Cycle**: 2 (re-review after Cycle 1 V1 violation resolved by architect)

---

## V1 Resolution Check (PRIMARY -- Cycle 2 mandate)

Cycle 1 blocking violation: T2 specified HasNakedPosition as `private static`, blocking direct
unit test access without NT8 runtime. Architect fix required: `internal static` in 3 locations.

### Location (a) -- Method Signature Section (T2 §4)

```
04-tickets.md line 296:  internal static bool IsBracketStopOrder(Order o)   PASS
04-tickets.md line 305:  internal static bool HasNakedPosition(Account acct) PASS
```
Both signatures declare `internal static`. V1(a): **RESOLVED**

### Location (b) -- Full Post-F2 Code Block (T2 §5)

```
04-tickets.md line 332:  internal static bool IsBracketStopOrder(Order o)   PASS
04-tickets.md line 402:  internal static bool HasNakedPosition(Account acct) PASS
```
Both full-method code blocks use `internal static`. V1(b): **RESOLVED**

### Location (c) -- Test Mechanism (T2 §7)

```
04-tickets.md line 477:  "Call CopyEngine.HasNakedPosition(stubAcct) directly
                          (internal visibility via InternalsVisibleTo)"        PASS
04-tickets.md line 488:  "Direct static call CopyEngine.IsBracketStopOrder(order)"
                          -- direct, no reflection                             PASS
```
Test mechanism specifies direct invocation via InternalsVisibleTo, not reflection. V1(c): **RESOLVED**

**V1 VERDICT: RESOLVED. No `private static` remains in T2 for either method.**

---

## WARN-1 (Non-Blocking) -- Plan Internal Inconsistency

The architecture plan (02-architecture-plan.md) is internally inconsistent:
- §6 Component List (line 319): `private static bool IsBracketStopOrder(Order o)`
- §T2 Method signatures (line 468): `private static bool IsBracketStopOrder(Order o)`
- §T2 note (lines 490-492): "*engineer MUST use `internal static`*" (binding instruction)

The ticket correctly follows the binding note. The `private static` references in the plan's
Component List and T2 signatures section are stale -- they predate the Cycle 1 correction.
This inconsistency does not affect the ticket (the ticket is correct) but the plan document
should be updated in a future revision to avoid confusing the engineer.
**Action required**: None on ticket. Architect to clean up plan residual at B194 close.

---

## T1 -- Fix TryNakedDetect PTT-Copy Guard (F1) + DrainThenDispatch Submitted (F3)

### Traceability
- BUG-1 (P0) → F1 guard in TryNakedDetect (T1 §2): MAPPED. Plan §T1 confirms. PASS
- BUG-4 (P0) → F3 LINQ predicate in DrainThenDispatch (T1 §2): MAPPED. Plan §T1 confirms. PASS
- DW-B194-01 → closed by F1 guard (T1 §8): MAPPED. PASS
- DW-B194-03 → closed by F3 LINQ extension (T1 §8): MAPPED. PASS
- All T1 plan items are covered. Zero phantom work. Zero uncovered plan items.
**Traceability: PASS**

### Scope Lock
Explicit scope lock present: "TryNakedDetect and DrainThenDispatch only. No other methods. No other
files." (T1 §3). No method overlap with T2 or T3.
**Scope Lock: PASS**

### JS Pre-Check
- JS-021 (P0): F1 guard reads `e.Order.Name` and `e.Order.OrderState` (read-only). F3 is a LINQ
  predicate with no state mutation. Zero `lock()` statements in either change. PASS
- JS-001 (P0): F1 guard uses `return`. F3 extends a LINQ predicate. Zero `throw new`. PASS
- JS-033 (P0): TryNakedDetect and DrainThenDispatch are synchronous. No `async void`. PASS
- JS-002 (P0): No `return null` in either method. PASS
- ASCII-only: "PTT-Copy" is ASCII (0x50..0x79). No Unicode. PASS
**JS Pre-Check: PASS**

### CYC Pre-Check (JS-066)
- TryNakedDetect: CYC 3 (pre) → 4 (post-F1). Verified: base=1, terminal-check=+1, follower-check=+1,
  PTT-Copy guard=+1 = 4. Well below JS-066 limit 8. PASS
- DrainThenDispatch: CYC unchanged (LINQ lambda predicates do not add McCabe branches to enclosing
  method). PASS
**CYC Pre-Check: PASS**

### NT8 Check
- No async/await in lifecycle methods. PASS
- No Account.All call. PASS
- No sealed on window classes. PASS
- No FontFamily/hardcoded hex color. PASS
- "PTT-Copy" order name confirmed: SubmitEntryDirect at line 6631 always names "PTT-Copy"
  (Director-confirmed in task context). F1 guard is correct. PASS
**NT8 Check: PASS**

### Test Coverage
- `TryNakedDetect_SkipsPTTCopyFill_NoNakedDetectorCalled` [Fact]: covers F1 PTT-Copy guard. PASS
- `DrainThenDispatch_IncludesSubmittedInEntryCandidates` [Fact]: covers F3 Submitted branch. PASS
- Both methods are private void; tests observe behavior indirectly via StatusUpdate event and
  Cancel/Submit call counts. [Fact] names and assertions are specified.
- WARN (non-blocking): T1 §7 states mechanism is "via reflection **or** testable seam." Engineer
  should prefer indirect observation (StatusUpdate event subscription) over reflection for
  TryNakedDetect. The "or" is ambiguous but the [Fact] is present and the assertion is observable.
  Not a FAIL under the stated rules (private methods, not internal/public).
**Test Coverage: PASS**

### Scan Checklist
T1 §9 contains all 7 scans:
- SCAN-01 [JS-021]: lock() grep. PRESENT
- SCAN-02 [JS-033]: async void grep. PRESENT
- SCAN-03 [JS-066]: CYC count. PRESENT
- SCAN-04 [JS-001]: throw new grep. PRESENT
- SCAN-05 [JS-002]: return null grep. PRESENT
- SCAN-06 [ASCII-only / DateTime.UtcNow]: string/identifier checks + DateTime.Now grep. PRESENT
- SCAN-07 [Test coverage]: dotnet test filter command + test names. PRESENT
**Scan Checklist: PASS**

### File Routing
All .cs changes in `src/PropTraderTools/CopyEngine.cs` (Wave workspace). PASS
Test file `src/PropTraderTools/Tests/B194Tests.cs` (Wave workspace). PASS
**File Routing: PASS**

### T1 VERDICT: TICKET_REVIEW_PASS

---

## T2 -- Fix HasNakedPosition TriggerPending Fix + IsBracketStopOrder Helper

### Traceability
- BUG-2 (P1) → F2: extract IsBracketStopOrder + refactor HasNakedPosition inner loop (T2 §2): MAPPED.
  Plan §T2 confirms. PASS
- DW-B194-02 → closed by F2 helper extraction + inner loop refactor (T2 §8): MAPPED. PASS
- All T2 plan items are covered. Zero phantom work.
**Traceability: PASS**

### Scope Lock
Explicit scope lock present: "HasNakedPosition inner loop refactor and new IsBracketStopOrder helper
only. No other methods. No other files." (T2 §3). No overlap with T1 or T3.
**Scope Lock: PASS**

### JS Pre-Check
- JS-021 (P0): IsBracketStopOrder is a pure predicate reading `o.OrderState`, `o.OrderType`,
  `o.StopPrice` (read-only). HasNakedPosition modified body has no lock. Zero `lock()`. PASS
- JS-001 (P0): IsBracketStopOrder uses only `return true`/`return false`. HasNakedPosition uses
  assignment and `continue`. Zero `throw new`. PASS
- JS-033 (P0): Both methods are `static bool`. No `async void`. PASS
- JS-002 (P0): Both methods return bool. No `return null`. PASS
- ASCII-only: Identifier "IsBracketStopOrder" is ASCII. No new string literals. PASS
**JS Pre-Check: PASS**

### CYC Pre-Check (JS-066)
- HasNakedPosition: CYC 8 (pre) → 7 (post-F2). Architect CYC=7 derivation:
  base=1, foreach-pos=+1, qty=+1, no-pos-return=+1, foreach-ord=+1,
  IsBracketStopOrder-if=+1, Limit-if=+1 = 7. PASS
- IsBracketStopOrder (NEW): CYC 3. Architect derivation:
  base=1, TriggerPending-if=+1, WorkingStop-if=+1 = 3. PASS
- Helper extraction was architecturally required: adding TriggerPending recognition directly to
  HasNakedPosition without extraction would have pushed CYC to 9 (JS-066 violation). PASS
**CYC Pre-Check: PASS**

### NT8 Check
- `OrderState.TriggerPending` confirmed available in AddOnBase (plan §9, B194-DIRECT commit
  a434679a). PASS
- `Order.StopPrice` confirmed available in AddOnBase (plan §9). PASS
- `StopPrice > 0` discriminator confirmed: real Market entries have StopPrice=0; ATM bracket stops
  have StopPrice > 0. Correctly discriminates. PASS
- No async/await, no Account.All, no FontFamily, no hardcoded hex color. PASS
**NT8 Check: PASS**

### Test Coverage
- `HasNakedPosition_ReturnsFalse_WhenOnlyTriggerPendingStopExists` [Fact]: covers BUG-2 end-to-end.
  Direct call via InternalsVisibleTo. PASS
- `IsBracketStopOrder_ReturnsTrue_ForTriggerPendingMarketWithStopPrice` [Fact]: branch 1. PASS
- `IsBracketStopOrder_ReturnsTrue_ForWorkingStopMarket` [Fact]: branch 2. PASS
- `IsBracketStopOrder_ReturnsFalse_ForWorkingLimitOrder` [Fact]: false path / anti-regression. PASS
- 4 [Fact] tests cover all paths of IsBracketStopOrder (CYC=3: 2 true branches + false path).
  HasNakedPosition covered end-to-end. xUnit only. No NUnit, no MSTest. PASS
- InternalsVisibleTo note present in T2 §4: "If not already present, engineer MUST add it." PASS
**Test Coverage: PASS**

### Scan Checklist
T2 §9 contains all 7 scans:
- SCAN-01 [JS-021]: lock() grep. PRESENT
- SCAN-02 [JS-033]: async void grep. PRESENT
- SCAN-03 [JS-066]: CYC count for IsBracketStopOrder (CYC=3) and HasNakedPosition (CYC=7). PRESENT
- SCAN-04 [JS-001]: throw new grep. PRESENT
- SCAN-05 [JS-002]: return null grep (bool methods -- cannot return null). PRESENT
- SCAN-06 [ASCII-only / DateTime.UtcNow]: identifier check + no DateTime.Now. PRESENT
- SCAN-07 [Test coverage]: dotnet test filter command + all 4 test names. PRESENT
**Scan Checklist: PASS**

### File Routing
All changes in `src/PropTraderTools/CopyEngine.cs` (Wave workspace). PASS
**File Routing: PASS**

### T2 VERDICT: TICKET_REVIEW_PASS

---

## T3 -- FlattenOneAccount Belt-and-Suspenders Debounce (F4)

### Traceability
- BUG-3 (P0 residual) → F4 debounce at FlattenOneAccount (T3 §2): MAPPED. Plan §T3 confirms.
  Residual path: PTT-Flatten fill → NakedDetect re-entry. F1 (T1) closes primary path;
  F4 (T3) closes residual. Both required for complete BUG-3 closure. PASS
- DW-B194-04 → closed by F4 field + debounce guard + timestamp recording (T3 §8): MAPPED. PASS
- All T3 plan items are covered. Zero phantom work.
**Traceability: PASS**

### Scope Lock
Explicit scope lock present: "FlattenOneAccount debounce insertions and _flattenFilledTicks field
declaration only. No other methods. No other files." (T3 §3). No overlap with T1 or T2.
**Scope Lock: PASS**

### JS Pre-Check
- JS-021 (P0): `_flattenFilledTicks` uses `ConcurrentDictionary.GetOrAdd` (value literal `0L`,
  no delegate, no closure allocation) and `AddOrUpdate`. Both are lock-free atomic operations.
  Zero `lock()` statements. PASS
- JS-001 (P0): F4 Change 2 uses `return`. F4 Change 3 uses `AddOrUpdate`. Zero `throw new`. PASS
- JS-033 (P0): FlattenOneAccount is synchronous void, signature unchanged. No `async void`. PASS
- JS-002 (P0): FlattenOneAccount returns void; new `return` is bare `return;`. No `return null`. PASS
- ASCII-only: "flat-debounce skip" verified ASCII; "_flattenFilledTicks", "flattenNow" ASCII. PASS
- DateTime: Debounce uses `Environment.TickCount`, not `DateTime.Now`. PASS
**JS Pre-Check: PASS**

### CYC Pre-Check (JS-066)
- FlattenOneAccount: CYC 6 (pre) → 7 (post-F4). +1 for debounce if-check. T3 SCAN-03 breakdown:
  (1) active PTT-Flatten guard foreach, (2) debounce if (NEW), (3) pre-cancel pos guard,
  (4) post-cancel pos guard, (5) action ternary, (6) if (order != null), (7) try/catch = 7.
  Verified against actual method body (lines 5004-5074): matches. PASS
- TickCount cast pattern `(long)(int)Environment.TickCount` matches existing pattern at line 6479.
  Intentional int-first cast preserves ~25-day wrap behavior. 500ms window far shorter than wrap
  period. No CYC impact. PASS
**CYC Pre-Check: PASS**

### NT8 Check
- `ConcurrentDictionary<string, long>` lock-free in current use (`_nakedDetectLastQueuedTicks`). PASS
- `Environment.TickCount` in current use (NakedPositionDetector line 6479). PASS
- `acc.Submit(new[] { order })` -- AddOnBase confirmed (plan §9). PASS
- "PTT-Flatten" order name -- matches existing FlattenOneAccount order creation at line 5062. PASS
- No async/await, no Account.All, no FontFamily, no hardcoded hex color. PASS
**NT8 Check: PASS**

### Test Coverage
- `FlattenOneAccount_SkipsWhenFlattenWithin500ms` [Fact]: covers debounce guard (new branch added
  by F4). Two assertions: second call emits "flat-debounce skip" via StatusUpdate AND acc.Submit
  call count remains 1. PASS
- Supplementary `FlattenOneAccount_SubmitsAfterDebounceExpired` [Fact] marked optional at engineer
  discretion. The mandatory test covers the required new branch. PASS
- xUnit only. No NUnit, no MSTest. PASS
**Test Coverage: PASS**

### Scan Checklist
T3 §9 contains all 7 scans:
- SCAN-01 [JS-021]: lock() grep + explicit _flattenFilledTicks must use GetOrAdd/AddOrUpdate only. PRESENT
- SCAN-02 [JS-033]: async void grep + FlattenOneAccount synchronous confirmation. PRESENT
- SCAN-03 [JS-066]: CYC count with 7-item breakdown for FlattenOneAccount post-F4. PRESENT
- SCAN-04 [JS-001]: throw new grep. PRESENT
- SCAN-05 [JS-002]: return null grep (void method, bare returns only). PRESENT
- SCAN-06 [ASCII-only / DateTime.UtcNow]: string + identifier checks + DateTime.Now grep command. PRESENT
- SCAN-07 [Test coverage]: dotnet test filter command + test name. PRESENT
**Scan Checklist: PASS**

### File Routing
All changes in `src/PropTraderTools/CopyEngine.cs` (Wave workspace). PASS
New field in same file. PASS
**File Routing: PASS**

### T3 VERDICT: TICKET_REVIEW_PASS

---

## Aggregate Spec Coverage Check

| Spec Bug | Priority | Ticket | Covered | Notes |
|----------|----------|--------|---------|-------|
| BUG-1 | P0 | T1 | YES | F1 guard in TryNakedDetect |
| BUG-2 | P1 | T2 | YES | F2 IsBracketStopOrder + HasNakedPosition refactor |
| BUG-3 | P0 | T1+T3 | YES | T1 closes primary path; T3 closes residual path |
| BUG-4 | P0 | T1 | YES | F3 LINQ Submitted extension in DrainThenDispatch |

All 4 confirmed bugs covered. No spec requirement uncovered. No duplicate coverage.
**Spec Coverage: PASS**

## DW Card Coverage Check

| DW Card | Ticket | Status |
|---------|--------|--------|
| DW-B194-01 | T1 | CLOSED by F1 |
| DW-B194-02 | T2 | CLOSED by F2 |
| DW-B194-03 | T1 | CLOSED by F3 |
| DW-B194-04 | T3 | CLOSED by F4 |
| DW-B183-03 | -- | OPEN carry-forward (correct) |
| DW-B192-SINST-01 | -- | OPEN carry-forward (correct) |
| DW-B193-01 | -- | OPEN carry-forward (correct) |

**DW Card Coverage: PASS**

## Director Update (Phase 5 Awareness -- No Ticket Change Required)

- Clone mode confirmed: in Clone+drag scenario, filling follower order IS "PTT-Copy"
  (SubmitEntryDirect at line 6631 always names the order "PTT-Copy"). F1 guard is correct as-written.
- B167-DEFER-02 (Clone mode ATM bracket missing after drain) is OUT OF B194 scope.
- DW-B195-01 will be added to 06-deferred-backlog.md at Phase 5. No ticket change needed.

---

## Summary Table

| Ticket | Traceability | JS Pre-Check | CYC | NT8 | Test Coverage | Scan Checklist | File Routing | VERDICT |
|--------|-------------|-------------|-----|-----|--------------|---------------|-------------|---------|
| T1 | PASS | PASS | PASS | PASS | PASS | PASS | PASS | **TICKET_REVIEW_PASS** |
| T2 | PASS | PASS | PASS | PASS | PASS | PASS | PASS | **TICKET_REVIEW_PASS** |
| T3 | PASS | PASS | PASS | PASS | PASS | PASS | PASS | **TICKET_REVIEW_PASS** |

---

## Overall: TICKET_REVIEW_PASS

All three tickets pass all checks. V1 violation (HasNakedPosition/IsBracketStopOrder
`private static`) is fully resolved: `internal static` confirmed in method signature section,
full post-F2 code block, and test mechanism (direct call via InternalsVisibleTo, no reflection)
for both T2 methods.

Non-blocking items noted (no ticket change required):
- WARN-1: Plan §6 Component List retains `private static` for IsBracketStopOrder -- plan document
  inconsistency (ticket is correct; plan should be cleaned up at B194 close).
- WARN-2: T1 test mechanism for TryNakedDetect mentions "reflection or testable seam" --
  engineer should prefer indirect StatusUpdate observation. Not a blocker (private method,
  both options produce equivalent test coverage).

**Safe to spawn Phase 4a engineer.**

---

*Written by: ptt-ticket-reviewer*
*Epic: B194*
*Phase: 3.5 (Ticket Review), Cycle 2*
*Source tickets: docs/brain/B194/04-tickets.md (revised)*
*Plan authority: docs/brain/B194/02-architecture-plan.md*
