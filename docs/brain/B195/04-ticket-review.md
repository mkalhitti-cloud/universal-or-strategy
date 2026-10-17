# B195 Ticket Review

**Epic**: B195 -- Clone Mode ATM-Aware Drain Dispatch (DW-B195-01)
**Reviewer**: ptt-ticket-reviewer (Phase 3.5)
**Date**: 2026-10-11
**Tickets reviewed**: 1 (T1 only -- T2 dropped per plan §1, DW-B195-03 opened)
**Source documents read**:
- docs/brain/B195/04-tickets.md
- docs/brain/B195/02-architecture-plan.md
- docs/brain/B194/06-deferred-backlog.md
- docs/standards/jane-street/RULES_CATALOG.md
- src/PropTraderTools/CopyEngine.cs (lines 600-615, 743-755, 4658-4685, 6713-6739, 6779-6800)

---

## Source Citation Verification (Pre-Review)

All ticket line references were checked against the live source before the checklist was evaluated.

| Citation | Ticket Claims | Source Confirmed | Result |
|----------|---------------|-----------------|--------|
| `SubmitDrainedEntry` body | Lines 6718-6739 | EXACT MATCH -- comment block 6713-6717 and full body 6718-6739 match ticket's "current body" verbatim | PASS |
| `GetCloneAtmMode()` | Line 745, `internal FollowerAtmMode GetCloneAtmMode()`, volatile read, no lock, never null | CONFIRMED at lines 743-754. Returns `Inherit()` as fallback (never null). Uses `volatile _cloneAtmObject` field. | PASS |
| `CopySignal.Create` param order | `(OrderAction action, OrderType type, int qty, double limitPrice, string orderId)` lines 605-611 | CONFIRMED at lines 605-611. Ticket body passes `(payload.Action, payload.OrderType, payload.Qty, payload.Price, <orderId>)` -- ORDER MATCHES. | PASS |
| `SendCopyWithAtm` | Line 4661, `private bool SendCopyWithAtm(Account follower, Instrument instrument, in CopySignal signal, FollowerAtmMode.Named namedMode)` | CONFIRMED at lines 4661-4666. "Entry" name at line 4680. | PASS |
| `PendingDispatchDrain` fields | `FollowerAcctKey` (string, 6783), `TimestampTicks` (long, 6795), `FollowerAccount` (Account, 6793) | ALL CONFIRMED. No ATM mode field -- confirms Option A re-fetch is the only approach without payload changes. | PASS |

---

## Ticket 1 -- DW-B195-01: Clone Mode SubmitDrainedEntry Must Call SendCopyWithAtm

---

### Traceability

| Item | Check | Result |
|------|-------|--------|
| DW-B195-01 spec req ID present | Ticket header: "Spec Requirements: DW-B195-01 (primary): Clone Mode SubmitDrainedEntry Not ATM-Aware. Opened in docs/brain/B194/06-deferred-backlog.md. Closed by this ticket." | PASS |
| B167-DEFER-02 historical reference present | Ticket: "B167-DEFER-02 (historical): Clone+ATM follower bracket dispatch deferred from B167. This ticket completes the drain-completion path for that deferred work." | PASS |
| Plan §3 trace | Ticket Step 1 (comment update) and Step 2 (body replacement) map directly to plan §3 "Modified Method: SubmitDrainedEntry" Before/After blocks. | PASS |
| Phantom work check | Ticket modifies only `SubmitDrainedEntry` + creates `B195Tests.cs`. Plan §4 lists exactly these two changes. No scope outside plan. | PASS |
| Missing work check | Plan §7 specifies 4 test cases. Ticket §xUnit Test Names specifies all 4 (T1_SubmitDrainedEntry_CloneAtmMode_CallsSendCopyWithAtm, T1_SubmitDrainedEntry_NoAtmMode_CallsSubmitEntryDirect, T1_SubmitDrainedEntry_AtmObjectNull_TemplatePresent_UsesNamedMode, T1_SubmitDrainedEntry_MissingPayload_ReturnsEarly). Full coverage. | PASS |

**Traceability: PASS**

---

### Implementation Correctness

| Item | Check | Result |
|------|-------|--------|
| Option A (re-fetch at dispatch time) | Ticket Step 2: `var atmMode = GetCloneAtmMode();` inside `SubmitDrainedEntry`. Plan §3: "Selected: Option A -- call `GetCloneAtmMode()` inside `SubmitDrainedEntry`." | PASS |
| `GetCloneAtmMode()` called inside `SubmitDrainedEntry` (not DrainThenDispatch) | Ticket Step 2 body: `GetCloneAtmMode()` called at branch (3) inside `SubmitDrainedEntry`. `DrainThenDispatch` not mentioned as modified. | PASS |
| No `PendingDispatchDrain` field additions | Ticket method signature table: `PendingDispatchDrain (class)` row shows "NO -- no field or constructor change". Ticket Engineer Instructions: "Do not touch DrainThenDispatch, PendingDispatchDrain, SendCopyWithAtm, or any other method." | PASS |
| ATM branch: Named → SendCopyWithAtm, else → SubmitEntryDirect | Ticket Step 2: `if (atmMode is FollowerAtmMode.Named namedAtm) { ... SendCopyWithAtm(...) } else { SubmitEntryDirect(...) }` | PASS |
| `CopySignal.Create` parameter order matches source | Ticket: `CopySignal.Create(payload.Action, payload.OrderType, payload.Qty, payload.Price, payload.FollowerAcctKey + "-drain-atm-" + payload.TimestampTicks)`. Source (lines 605-611): `(OrderAction action, OrderType type, int qty, double limitPrice, string orderId)`. Mapping: Action→action ✓, OrderType→type ✓, Qty→qty ✓, Price→limitPrice ✓, orderId string→orderId ✓. | PASS |
| `orderId` construction uses `payload.FollowerAcctKey + "-drain-atm-" + payload.TimestampTicks` | Ticket Step 2 and Key Implementation Notes: confirmed exact pattern. Fields verified in source (FollowerAcctKey=string, TimestampTicks=long). | PASS |
| `SendCopyWithAtm` called with `(Account, Instrument, in CopySignal, FollowerAtmMode.Named)` | Ticket: `SendCopyWithAtm(follower, payload.Instrument, in signal, namedAtm)`. Source signature: `(Account follower, Instrument instrument, in CopySignal signal, FollowerAtmMode.Named namedMode)`. | PASS |
| Else branch calls `SubmitEntryDirect` with same parameters as before | Ticket else branch: `SubmitEntryDirect(follower, payload.Instrument, payload.Qty, payload.Price, payload.Action, payload.OrderType)`. Matches the current (pre-B195) call at lines 6727-6734. | PASS |

**Implementation Correctness: PASS**

---

### JS Pre-Check

| Rule | Check | Result |
|------|-------|--------|
| JS-021: No lock() | New body uses `GetCloneAtmMode()` (volatile read, no lock). No lock() in any new or modified code. Ticket comment block explicitly states "JS-021: no lock()". | PASS |
| JS-033: No async void | `SubmitDrainedEntry` is `private void` (not async). No new async methods introduced. | PASS |
| JS-001: No throw new Exception | Ticket Step 2 body uses early returns only. `SendCopyWithAtm` has its own internal try/catch (source line 4668). No throw introduced. | PASS |
| JS-002: No return null for optional values | `SubmitDrainedEntry` returns void (no return value). `GetCloneAtmMode()` source-confirmed to return `Inherit()` (never null). No new null-return patterns. | PASS |
| ASCII-only identifiers | New identifiers: `atmMode`, `namedAtm`, `signal`. New literal: `"-drain-atm-"`. All pure ASCII. `TimestampTicks` is `long` -- string concatenation produces digits only. | PASS |
| DateTime.Now ban | No `DateTime` usage anywhere in the new code. `TimestampTicks` uses existing `long` field from `Environment.TickCount` (plan §2, line 6613). | PASS |
| Dictionary<K,V> shared state | No new Dictionary fields. Existing `_pendingDispatchDrains` is `ConcurrentDictionary<string,PendingDispatchDrain>` (plan Appendix A, line 388-389). No mutation. | PASS |

**JS Pre-Check: PASS**

---

### CYC Pre-Check

| Method | Pre-CYC | Post-CYC | Limit | Ticket Claims | Assessment |
|--------|---------|----------|-------|---------------|------------|
| `SubmitDrainedEntry` | 4 | 5 | 8 | CYC 4→5 (+1 branch: `if atmMode is Named`) | PASS -- branch count verified: (1) TryRemove guard, (2) follower null guard, (3) atmMode is Named, (4) foreach DrainedOrderIds = 4 branches + base 1 = CYC 5. |
| `DrainThenDispatch` | 4 | 4 | 8 | Unchanged | PASS |
| `PendingDispatchDrain` constructor | 1 | 1 | 8 | Unchanged | PASS |
| `GetCloneAtmMode` | 2 | 2 | 8 | Called only | PASS |
| `SendCopyWithAtm` | 4 | 4 | 8 | Called only | PASS |

CYC impact table present: YES (ticket section "CYC Impact Table"). CYC counting convention documented: YES. No method approaches or exceeds CYC=8.

**CYC Pre-Check: PASS**

---

### NT8 Constraints

| Constraint | Check | Result |
|------------|-------|--------|
| `SendCopyWithAtm` is AddOnBase-available | Ticket comment: "NT8: SendCopyWithAtm uses StartAtmStrategy (AddOnBase-available, NT8_FULL_REFERENCE.md line 17)." Plan §6 confirms: "StartAtmStrategy: confirmed AddOnBase-available (NT8_FULL_REFERENCE.md line 17-34). No StrategyBase-only API used." | PASS |
| "Entry" order name constraint respected | Source line 4680 hardcodes `"Entry"` inside `SendCopyWithAtm`. The `orderId` passed to `CopySignal.Create` (`payload.FollowerAcctKey + "-drain-atm-" + payload.TimestampTicks`) is for PTT internal tracking only and does NOT reach `Account.CreateOrder` -- the order name is independently set inside `SendCopyWithAtm`. | PASS |
| orderId vs order name confusion | Ticket explicitly states in Key Implementation Notes: "SendCopyWithAtm return value (bool) is intentionally discarded." NT8 order name `"Entry"` is internal to `SendCopyWithAtm` (line 4680). `CopySignal.OrderId` is the PTT tracking key used in `RecordFollowerCopy` (separate from `CreateOrder` name). Ticket does NOT conflate these. | PASS |
| async/await in lifecycle method | No async/await in any modified or new code. | PASS |
| Account.All call outside Loaded handler | No `Account.All` usage introduced. | PASS |
| sealed on TradeCopierWindow | Not applicable -- no window class modified. | N/A |
| FontFamily / hardcoded hex color | No UI code modified. | N/A |
| CreateOrder name not starting "PTT-" | Non-ATM path calls `SubmitEntryDirect` which already uses "PTT-Copy" (existing). ATM path delegates order name to `SendCopyWithAtm` which uses "Entry" (line 4680) -- NT8 required. No new order names introduced by this ticket. | PASS |
| `DateTime.Now` | None introduced. | PASS |

**NT8 Check: PASS**

---

### Test Coverage

| Test Method | Scenario | Required | Present | Asserts Documented |
|-------------|----------|----------|---------|-------------------|
| `T1_SubmitDrainedEntry_CloneAtmMode_CallsSendCopyWithAtm` | ATM-aware path (Clone mode, Named ATM) | YES | YES | GetCloneAtmMode() returns Named; "Entry"-named order created; no "PTT-Copy" order. |
| `T1_SubmitDrainedEntry_NoAtmMode_CallsSubmitEntryDirect` | Non-ATM path (no ATM configured) | YES | YES | GetCloneAtmMode() returns Inherit; "PTT-Copy" order via CreateOrder; no StartAtmStrategy. |
| `T1_SubmitDrainedEntry_AtmObjectNull_TemplatePresent_UsesNamedMode` | String-overload ATM path (_cloneAtmObject null, _cloneAtmCache set) | YES | YES | GetCloneAtmMode() returns Named with AtmObject==null, TemplateName=="MyTemplate"; SendCopyWithAtm executes. |
| `T1_SubmitDrainedEntry_MissingPayload_ReturnsEarly` | Missing payload early return (CYC branch 1) | YES | YES | No exception; no Account API calls; clean return. |

- All 4 methods are [Fact] decorated (xUnit-only -- no NUnit, no MSTest). ✅
- Minimum required scenarios (ATM-aware Named path + non-ATM path) both present. ✅
- Test file path specified: `src/PropTraderTools/Tests/B195Tests.cs`. ✅
- Class name specified: `B195Tests`. ✅
- Each test is documented with setup steps and assertions. ✅

**Test Coverage: PASS**

---

### 7-Scan Checklist Presence

| Scan | Present in Ticket | Grep Pattern Provided | Expected Result Stated |
|------|------------------|----------------------|----------------------|
| SCAN-01: lock() ban (JS-021) | YES | `grep -n "lock(" src/PropTraderTools/CopyEngine.cs` | YES -- zero matches in modified region |
| SCAN-02: async void ban (JS-033) | YES | `grep -n "async void " src/PropTraderTools/CopyEngine.cs` | YES -- zero new matches |
| SCAN-03: throw new ban (JS-001) | YES | `grep -n "throw new " src/PropTraderTools/CopyEngine.cs` | YES -- zero new throw new in lines 6713-6739 |
| SCAN-04: CYC <= 8 (JS-066) | YES | Manual branch count listed (4 branches enumerated) | YES -- CYC=5, <= 8 ceiling |
| SCAN-05: ASCII-only | YES | `grep -Pn "[^\x00-\x7F]" src/PropTraderTools/CopyEngine.cs` | YES -- zero matches in new code |
| SCAN-06: DateTime.Now ban | YES | `grep -n "DateTime\.Now" src/PropTraderTools/CopyEngine.cs` | YES -- zero matches in new code |
| SCAN-07: return null ban (JS-002) | YES | Manual check: GetCloneAtmMode() returns Inherit (never null); void method | YES -- N/A for void return; GetCloneAtmMode() confirmed unchanged |

All 7 scans present: YES. All have explicit grep patterns or manual check instructions: YES. All have expected results stated: YES. All reference the correct file path: YES.

**Scan Checklist: PASS**

---

### File Routing

| Item | Ticket Claims | Check |
|------|---------------|-------|
| C# source file | `src/PropTraderTools/CopyEngine.cs` | Points to Wave workspace (`c:\WSGTA\universal-or-strategy\src\PropTraderTools\`). NOT Director workspace. |
| Test file | `src/PropTraderTools/Tests/B195Tests.cs` | Points to Wave workspace. |
| Completion artifact | `docs/brain/B195/ticket-1-completion.md` | Correct. |

**File Routing: PASS**

---

### Completeness

| Item | Check | Result |
|------|-------|--------|
| 7-scan checklist present with all 7 scans | YES (see Scan Checklist section) | PASS |
| Each scan has explicit grep pattern or manual check | YES | PASS |
| Expected result for each scan stated | YES | PASS |
| xUnit [Fact] test names listed (minimum 3) | 4 [Fact] methods listed | PASS |
| Completion artifact path specified | `docs/brain/B195/ticket-1-completion.md` | PASS |
| Completion artifact required sections listed | YES -- Scan Results table, CYC Verification, Test Results, BUILD_PASS Declaration, with expected values prefilled | PASS |
| Method signatures table | YES -- all 7 affected/called methods listed with file, line, signature, modified flag | PASS |

**Completeness: PASS**

---

### Ticket 1 Verdict

| Check Category | Result |
|----------------|--------|
| Traceability | PASS |
| Implementation Correctness | PASS |
| JS Pre-Check (JS-021, JS-033, JS-001, JS-002, ASCII-only, DateTime.Now) | PASS |
| CYC Pre-Check (JS-066) | PASS |
| NT8 Constraints | PASS |
| Test Coverage | PASS |
| Scan Checklist (SCAN-01 through SCAN-07) | PASS |
| File Routing | PASS |
| Completeness | PASS |

**VERDICT: TICKET_REVIEW_PASS**

---

## Overall

**Total tickets**: 1
**Tickets passing all checks**: 1
**Tickets failing any check**: 0

**Violations**: NONE

---

## Overall: TICKET_REVIEW_PASS

The single ticket in B195 passes all review criteria. The engineer may proceed to implementation.

Engineer entry point: `docs/brain/B195/04-tickets.md` -- Ticket 1.
Engineer contract: implement Step 1 (comment update, lines 6713-6717), Step 2 (body replacement, lines 6718-6739), and Step 3 (create `src/PropTraderTools/Tests/B195Tests.cs` with 4 [Fact] methods).
Post-edit mandatory: run `powershell -File .\scripts\ptt-sync.ps1` and all 7 scans before reporting BUILD_PASS.

---

*Written by: ptt-ticket-reviewer*
*Epic: B195*
*Phase: 3.5 (Ticket Review)*
*Return: TICKET_REVIEW_PASS*
