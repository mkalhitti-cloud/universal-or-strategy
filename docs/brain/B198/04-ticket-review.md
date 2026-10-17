# B198 Ticket Review
**Author**: ptt-ticket-reviewer (Phase 3.5)
**Date**: 2026-10-13
**Epic**: B198 -- SimStop N-bracket generalisation + QTY instrument + fo=NULL drain + ATM dir fix
**Tickets source**: docs/brain/B198/04-tickets.md
**Plan source**: docs/brain/B198/02-architecture-plan.md (REVIEW_PASS -- Cycle 2)
**Plan review source**: docs/brain/B198/02-plan-review.md (REVIEW_PASS -- Cycle 2)
**Spec source**: docs/brain/B198-orchestrator-prompt.md
**Rules source**: docs/standards/jane-street/RULES_CATALOG.md

---

## Ticket 1 — SimStop N-General Bracket Support (DW-B198-SIMSTP-01)

### Traceability
- Spec requirement IDs present: B198-orchestrator-prompt.md §TICKET 1 Part A + Part B ✅
- DW Card IDs: DW-B198-SIMSTP-01, DW-B198-FINDLDR-01, DW-B198-CAPTURELEG-01 ✅
- Plan section references: §3 Part A (6 predicates), §3 B1-B5 ✅
- All 6 Part A predicates (A1-A6) have exact line + exact code change ✅
- All Part B methods (B1 TryParseStopSuffix, B2a-d CaptureOtherLegTargetPrices + ResubmitCollateralLegs, B3 FindLeaderCollateralOrder, B5 FindLeaderStopPrice) have full replacement / body ✅
- MaxBracketLegs = 9 const declared with placement instruction ✅
- B4 auto-coverage via B1 documented ✅
- No phantom work (all items trace to plan §3 or spec) ✅
- No plan items missing (plan §3 fully covered) ✅

**Traceability: PASS**

### JS Pre-Check
- SCAN-01 (JS-021): No lock() in any new/modified code. All changes are expression-level OR additions or method body replacements. No lock() described. ✅
- SCAN-02 (JS-033): No new async void methods. All changes synchronous. ✅
- SCAN-03 (JS-001/JS-002): No throw statements. FindLeaderCollateralOrder returns null as documented NT8 absence signal (plan-review-approved exempt). FindLeaderStopPrice returns 0.0 (not null). ✅
- SCAN-04 (JS-066): CYC table present. Max after: TryParseStopSuffix=5, IsNativeExitName=5. All ≤ 8. ✅
- SCAN-05 (ASCII-only): All string literals ASCII ("SimStop", "Stop", "Target"). No Unicode. ✅
- SCAN-06 (xUnit): All 16 test cases use [Fact]. No [Test] / [TestMethod] / NUnit / MSTest references. ✅
- SCAN-07: MaxBracketLegs is `private const int` (not a shared mutable field). No lock. ✅

**JS Pre-Check: PASS**

### CYC Pre-Check
- 11 modified methods; CYC table present covering all. Maximum: TryParseStopSuffix 3→5, IsNativeExitName 4→5.
- No method exceeds CYC=8. ✅

**CYC Pre-Check: PASS**

### NT8 Check
- No async/await in lifecycle methods ✅
- No lock() on any shared state ✅
- No Account.All calls ✅
- No sealed on TradeCopierWindow ✅
- No FontFamily, no hardcoded hex colors ✅
- No DateTime.Now ✅
- No CreateOrder with non-PTT- name ✅
- All event handler changes: none (T1 does not touch event handlers) ✅

**NT8 Check: PASS**

### Test Coverage
- 16 [Fact] tests (T1-01..T1-16) present ✅
- IsAtmBracketName: T1-01/T1-02/T1-03 (direct call, internal static) ✅
- IsNativeExitName: T1-04/T1-05 (direct call, internal static) ✅
- TryParseStopSuffix: T1-06..T1-11 (source-text; justified: private static, TypeInitializationException constraint) ✅
- MaxBracketLegs const: T1-12 (source-text) ✅
- IsBracketLeg: T1-13 (source-text; justified: private instance, NT8 runtime required) ✅
- IsBracketLegStatic: T1-14 (source-text; justified: private static) ✅
- IsAtmSTPOrder: T1-15 (source-text; justified: internal static, requires NT8 Order param) ✅
- IsStopLeg: T1-16 (source-text; justified: private static) ✅
- All public/internal methods in T1 scope have [Fact] tests ✅

**Test Coverage: PASS**

### Scan Checklist Presence
- SCAN-01 through SCAN-07 all present with PowerShell commands and expected results ✅
- SCAN-04 row includes before/after CYC for every modified method (11 methods in CYC table) ✅
- No blank or placeholder scan rows ✅

**Scan Checklist: PASS**

### File Routing
- CopyEngine.cs path: `src/PropTraderTools/CopyEngine.cs` (Wave workspace) ✅
- Tests path: `src/PropTraderTools/Tests/B198Tests.cs` (Wave workspace) ✅
- No Director workspace paths for .cs files ✅

**File Routing: PASS**

### VERDICT: TICKET_REVIEW_PASS

---

## Ticket 2a — QTY Bug Instrumentation (DW-B198-QTY-01 Phase A)

### Traceability
- Spec requirement IDs present: B198-orchestrator-prompt.md §TICKET 2, 02-architecture-plan.md §4 T2a ✅
- DW Card ID: DW-B198-QTY-01 (Phase A) ✅
- Plan section reference: §4 T2a ✅

**FAIL ITEM — Log string mismatch with spec and approved plan**:

The ticket's Change T2a-1 log string is:
```
follower.Name + ": ATM-entry-qty=" + signal.Quantity + " tmpl=" + namedMode.TemplateName
```

The spec (B198-orchestrator-prompt.md §TICKET 2, line 277) mandates:
```
acc.Name + ": ATM-send qty=" + signal.Quantity + " tmpl=" + namedMode.TemplateName
```

The REVIEW_PASS plan (02-architecture-plan.md §4 T2a) specifies:
```
follower.Name + ": ATM-send qty=" + signal.Quantity + " tmpl=" + namedMode.TemplateName
```

Ticket uses `"ATM-entry-qty="` — spec and plan both say `"ATM-send qty="`. **MISMATCH.**

The ticket's Change T2a-2 log string is:
```
follower.Name + ": ATM-order-created qty=" + order.Quantity
```

The spec (line 279) mandates:
```
if (order != null) StatusUpdate?.Invoke(acc.Name + ": ATM-order qty=" + order.Quantity);
```

The REVIEW_PASS plan (§4 T2a) specifies:
```
if (order != null) StatusUpdate?.Invoke(follower.Name + ": ATM-order qty=" + order.Quantity);
```

Ticket uses `"ATM-order-created qty="` — spec and plan both say `"ATM-order qty="`. **MISMATCH.**

**Impact cascade**: These mismatches propagate into:
1. T2a-01 [Fact] test asserts `"ATM-entry-qty=" + signal.Quantity` (ticket string, NOT plan string) — will PASS for ticket implementation but would fail against a spec-compliant implementation.
2. T2a-02 [Fact] test asserts `"ATM-order-created qty=" + order.Quantity` (ticket string, NOT plan string) — same issue.
3. T2a SIM protocol uses `ATM-entry-qty=X` and `ATM-order-created qty=Y` — deviates from plan-approved strings.
4. T2a acceptance criteria items 2 and 3 use the ticket's strings — will reject plan-compliant code.
5. T2b SIM evidence template uses the ticket's strings for hypothesis determination.

The engineer builds from the ticket. If the ticket mandates `"ATM-entry-qty="` the engineer writes that string. The verifier's source-text scan then validates `"ATM-entry-qty="` not `"ATM-send qty="`. The T2b hypothesis strings (H1/H2/H3 examples in the SIM evidence template) reference `ATM-entry-qty=` for H1/H2 comparison, which would also be wrong. This is a **spec requirement not satisfied** — the ticket has drifted from both the spec mandate and the REVIEW_PASS plan.

**Traceability: FAIL**
- VIO-1: T2a Change T2a-1 log string `"ATM-entry-qty="` deviates from spec `"ATM-send qty="` and plan §4 `"ATM-send qty="` — ticket does not implement the spec-approved design.
- VIO-2: T2a Change T2a-2 log string `"ATM-order-created qty="` deviates from spec `"ATM-order qty="` and plan §4 `"ATM-order qty="` — same issue.

### JS Pre-Check
- SCAN-01 (JS-021): No lock() added. ✅
- SCAN-02 (JS-033): No new async void. ✅
- SCAN-03 (JS-001/JS-002): No throw, no new return null. ✅
- SCAN-04 (JS-066): SendCopyWithAtm CYC 4→4 (0 McCabe delta on StatusUpdate?.Invoke). ✅
- SCAN-05 (ASCII-only): Ticket's log strings are ASCII (the wrong strings are still ASCII). ✅
- SCAN-06 (xUnit): T2a-01 and T2a-02 use [Fact]. ✅
- SCAN-07: No new fields. ✅

**JS Pre-Check: PASS** (no JS rule violations, only spec/plan traceability violations)

### CYC Pre-Check
- SendCopyWithAtm CYC: 4→4. StatusUpdate?.Invoke is not a McCabe branch. ✅

**CYC Pre-Check: PASS**

### NT8 Check
- No lock(), no async void, no DateTime.Now ✅
- StatusUpdate?.Invoke is a null-conditional event invocation — NT8-safe ✅

**NT8 Check: PASS**

### Test Coverage
- T2a-01 and T2a-02: both [Fact] source-text assertions present ✅
- Tests cover both log lines ✅
- NOTE: tests assert the TICKET's strings (not plan strings) — tests are internally consistent with the (faulty) ticket but the underlying spec string mismatch is a traceability issue, not a missing test issue.

**Test Coverage: PASS** (tests present for all new code; underlying issue is traceability, not test coverage)

### Scan Checklist Presence
- SCAN-01 through SCAN-07 all present with commands and expected results ✅
- SCAN-04 row confirms CYC=4 unchanged ✅
- No blank or placeholder rows ✅

**Scan Checklist: PASS**

### File Routing
- CopyEngine.cs: `src/PropTraderTools/CopyEngine.cs` ✅
- Tests: `src/PropTraderTools/Tests/B198Tests.cs` ✅
- SIM evidence artifact: `docs/brain/B198/ticket-2a-sim-evidence.md` ✅

**File Routing: PASS**

### VERDICT: TICKET_REVIEW_FAIL
Violations: VIO-1, VIO-2 (log strings deviate from spec + plan — see Traceability section above)

---

## Ticket 2b — QTY Bug Fix (DW-B198-QTY-01 Phase B)

### Traceability
- Spec requirement IDs present: B198-orchestrator-prompt.md §TICKET 2, 02-architecture-plan.md §4 T2b ✅
- DW Card ID: DW-B198-QTY-01 (Phase B) ✅
- Plan section reference: §4 T2b ✅
- Contingency clause present: H1/H2/H3 decision tree with explicit outcome actions ✅
- T2a pre-condition gate explicit and mandatory ✅
- H2 path fully designed: field, CheckAtmTemplatesForReverse extension, TryGetAtmTemplateGroupCount helper, SendCopyWithAtm qty division ✅
- H1 closure path stated: "CANNOT-REPRODUCE", write cancelled.md, no code changes ✅
- H3 closure path stated: DEFERRED, write deferred.md ✅
- No phantom work ✅
- No plan items missing from T2b scope ✅

**Traceability: PASS**

### JS Pre-Check
- SCAN-01 (JS-021): `_atmTemplateGroupQty` is `ConcurrentDictionary<string, int>`. TryGetValue, TryAdd are lock-free. No lock(). ✅
- SCAN-02 (JS-033): No new async void. ✅
- SCAN-03 (JS-001/JS-002): TryGetAtmTemplateGroupCount returns bool with out param. No throw. No return null. ✅
- SCAN-04 (JS-066): CYC table present. CheckAtmTemplatesForReverse(string): 6→7, SendCopyWithAtm: 4→5, TryGetAtmTemplateGroupCount: N/A→2. All ≤ 8. ✅
- SCAN-05 (ASCII-only): All string literals ASCII (XML path `//Quantity`, comment strings). ✅
- SCAN-06 (xUnit): T2b-01 and T2b-02 use [Fact]. ✅
- SCAN-07 (sync field): `_atmTemplateGroupQty` is `private readonly ConcurrentDictionary` — no lock around it. SCAN-07 command present. ✅

**JS Pre-Check: PASS**

### CYC Pre-Check
- CYC table present with 3 rows (CheckAtmTemplatesForReverse(string), SendCopyWithAtm, TryGetAtmTemplateGroupCount)
- Max after: CheckAtmTemplatesForReverse(string)=7, SendCopyWithAtm=5. All ≤ 8. ✅

**CYC Pre-Check: PASS**

### NT8 Check
- No lock(), no async void ✅
- XML node path `//Quantity` noted as requiring verification against actual template XML — engineer instructed to verify before applying ✅

**NT8 Check: PASS**

### Test Coverage
- T2b-01: [Fact] asserts `_atmTemplateGroupQty` field present in source ✅
- T2b-02: [Fact] asserts `signal.Quantity / groupCount` division logic present in source ✅
- NOTE: Tests are marked SKIPPED if H1 confirmed (no code changes) — correctly handled ✅
- All new public/internal methods (TryGetAtmTemplateGroupCount is `private`) covered via source-text assertions ✅

**Test Coverage: PASS**

### Scan Checklist Presence
- SCAN-01 through SCAN-07 all present with commands and expected results ✅
- SCAN-04 includes before/after CYC ✅
- No blank or placeholder rows ✅

**Scan Checklist: PASS**

### File Routing
- CopyEngine.cs: `src/PropTraderTools/CopyEngine.cs` ✅
- Tests: `src/PropTraderTools/Tests/B198Tests.cs` ✅

**File Routing: PASS**

### VERDICT: TICKET_REVIEW_PASS

---

## Ticket 3 — fo=NULL Drain Path Fix (DW-B195-01)

### Traceability
- Spec requirement IDs present: B198-orchestrator-prompt.md §TICKET 3, DW-B195-01, 02-architecture-plan.md §5 ✅
- Plan review VIO-1 (RESOLVED in cycle 2) referenced: ✅
- New field `_drainFollowerEntryOrders`: ✅ (T3-1)
- SendCopyWithAtm return type change bool→Order?: ✅ (T3-2)
- All 6 call sites enumerated: ✅ (table in T3-2: lines 2759, 2870, 2987, 4522, 6966, 7104)
- SubmitDrainedEntry stores returned Order: ✅ (T3-3)
- TrySetSlateReadyOnDrainFill: TryGetValue + TryRemove + drainFo passed to SyncFollowerBracket: ✅ (T3-4) — VIO-1 resolution confirmed
- AbortDrainOnFill cleanup: ✅ (T3-5)
- TryDrainWatchdog cleanup: ✅ (T3-6)
- JS-008 rationale documented (ConcurrentDictionary vs struct field): ✅
- No phantom work ✅
- No plan §5 items missing ✅

**Traceability: PASS**

### JS Pre-Check
- SCAN-01 (JS-021): `_drainFollowerEntryOrders` is `private readonly ConcurrentDictionary<string, Order>`. TryAdd, TryGetValue, TryRemove are lock-free. No lock(). ✅
- SCAN-02 (JS-033): No new async void. ✅
- SCAN-03 (JS-001/JS-002): SendCopyWithAtm returns null on failure — NT8 established absence signal, plan-review-approved exempt. ✅
- SCAN-04 (JS-066): CYC table present. Max: SubmitDrainedEntry 5→6. All ≤ 8. ✅
- SCAN-05 (ASCII-only): All new log strings and comment strings ASCII. ✅
- SCAN-06 (xUnit): T3-01..T3-04 use [Fact]. ✅
- SCAN-07 (sync field): SCAN-07 command checks for lock blocks around `_drainFollowerEntryOrders`. ✅

**JS Pre-Check: PASS**

### CYC Pre-Check
- CYC table present: SendCopyWithAtm=4 (return type change only, no new branches), SubmitDrainedEntry=6 (+1 null check), TrySetSlateReadyOnDrainFill=5 (unchanged, TryGetValue/TryRemove are not branches), AbortDrainOnFill=3, TryDrainWatchdog=4. All ≤ 8. ✅

**CYC Pre-Check: PASS**

### NT8 Check
- No lock() ✅
- ConcurrentDictionary TryAdd/TryGetValue/TryRemove: lock-free NT8 dispatch thread safe ✅
- No async void ✅

**NT8 Check: PASS**

### Test Coverage
- T3-01: [Fact] source-text confirms `_drainFollowerEntryOrders` field ✅
- T3-02: [Fact] source-text confirms `Order? SendCopyWithAtm` return type ✅
- T3-03: [Fact] source-text confirms `_drainFollowerEntryOrders.TryAdd` in SubmitDrainedEntry ✅
- T3-04: [Fact] source-text confirms `_drainFollowerEntryOrders.TryGetValue` in TrySetSlateReadyOnDrainFill ✅
- 4 tests for 4 key structural changes — all new fields and wiring covered ✅

**Test Coverage: PASS**

### Scan Checklist Presence
- SCAN-01 through SCAN-07 all present with commands and expected results ✅
- SCAN-04 includes before/after CYC for all 5 modified methods ✅
- No blank or placeholder rows ✅

**Scan Checklist: PASS**

### File Routing
- CopyEngine.cs: `src/PropTraderTools/CopyEngine.cs` ✅
- Tests: `src/PropTraderTools/Tests/B198Tests.cs` ✅

**File Routing: PASS**

### VERDICT: TICKET_REVIEW_PASS

---

## Ticket 4 — ATM Template Dir Startup Fix (DW-B198-ATMDIR-01)

### Traceability
- Spec requirement IDs present: B198-orchestrator-prompt.md §TICKET 4, DW-B198-ATMDIR-01, 02-architecture-plan.md §6 ✅
- DW Card ID: DW-B198-ATMDIR-01 ✅
- Plan section reference: §6 ✅
- Old path (`"Templates", "ATM"`) and new path (`"templates", "AtmStrategy"`) exact strings shown ✅
- Dir-not-found warning log line upgraded to WARN with path appended ✅
- Existing Reverse=true warning preserved (not modified) ✅
- Two acceptance criteria verify the exact log strings ✅
- No phantom work ✅
- No plan §6 items missing ✅

**Traceability: PASS**

### JS Pre-Check
- SCAN-01 (JS-021): No lock() added. Pure string changes. ✅
- SCAN-02 (JS-033): No new async void. ✅
- SCAN-03 (JS-001/JS-002): No throw. Methods are void. ✅
- SCAN-04 (JS-066): CYC table present. CheckAtmTemplatesForReverse(): 1→1, CheckAtmTemplatesForReverse(string): 6→6. All ≤ 8. ✅
- SCAN-05 (ASCII-only): All string literals ASCII ("templates", "AtmStrategy", "[PTT-STARTUP-WARN]"). ✅
- SCAN-06 (xUnit): T4-01 and T4-02 use [Fact]. ✅
- SCAN-07: No new fields introduced. ✅

**JS Pre-Check: PASS**

### CYC Pre-Check
- CYC table present: both overloads unchanged (1 and 6). ✅

**CYC Pre-Check: PASS**

### NT8 Check
- No lock(), no async void ✅
- Path change uses `System.IO.Path.Combine(NinjaTrader.Core.Globals.UserDataDir, ...)` — correct NT8 API ✅

**NT8 Check: PASS**

### Test Coverage
- T4-01: [Fact] source-text confirms `"templates", "AtmStrategy"` correct path ✅
- T4-02: [Fact] source-text confirms `[PTT-STARTUP-WARN] ATM template dir not found:` upgraded warning ✅
- All changed strings have corresponding test coverage ✅

**Test Coverage: PASS**

### Scan Checklist Presence
- SCAN-01 through SCAN-07 all present with commands and expected results ✅
- SCAN-04 row confirms CYC unchanged for both overloads ✅
- No blank or placeholder rows ✅

**Scan Checklist: PASS**

### File Routing
- CopyEngine.cs: `src/PropTraderTools/CopyEngine.cs` ✅
- Tests: `src/PropTraderTools/Tests/B198Tests.cs` ✅

**File Routing: PASS**

### VERDICT: TICKET_REVIEW_PASS

---

## Violations Summary

| VIO # | Ticket | Rule / Section | Description | Required Fix |
|-------|--------|----------------|-------------|--------------|
| VIO-1 | T2a | TRACEABILITY — spec §TICKET 2 line 277 + plan §4 T2a log line 1 | Ticket Change T2a-1 uses `"ATM-entry-qty="` but spec mandates `"ATM-send qty="` and plan §4 specifies `"ATM-send qty="`. Propagates to: T2a-01 [Fact] test assertion, T2a SIM protocol log check, T2a acceptance criterion item 2. | Replace `"ATM-entry-qty="` with `"ATM-send qty="` everywhere in T2a (Change T2a-1, T2a-01 assertion, SIM protocol, acceptance criteria item 2). |
| VIO-2 | T2a | TRACEABILITY — spec §TICKET 2 line 279 + plan §4 T2a log line 2 | Ticket Change T2a-2 uses `"ATM-order-created qty="` but spec mandates `"ATM-order qty="` and plan §4 specifies `"ATM-order qty="`. Propagates to: T2a-02 [Fact] test assertion, T2a SIM protocol log check, T2a acceptance criterion item 3, T2b SIM evidence template H1/H2 examples. | Replace `"ATM-order-created qty="` with `"ATM-order qty="` everywhere in T2a (Change T2a-2, T2a-02 assertion, SIM protocol, acceptance criteria item 3). Fix T2b SIM evidence template accordingly. |

---

## Overall: TICKET_REVIEW_FAIL

T2a (Ticket 2a) fails on two traceability violations (log strings deviate from spec and REVIEW_PASS plan). All other tickets (T1, T2b, T3, T4) pass all checks.

**This is Revision Cycle 1.** Maximum 2 revision cycles. After 2 TICKET_REVIEW_FAIL: STOP and escalate to Director.

**Corrective action required**: ptt-architect must revise `docs/brain/B198/04-tickets.md` Ticket 2a only. Fix the two log string mismatches (VIO-1 and VIO-2). No other changes required. Return for Cycle 2 review.

---

*Written by: ptt-ticket-reviewer*
*Epic: B198*
*Phase: 3.5 (Ticket Review — Cycle 1)*
*Returns: TICKET_REVIEW_FAIL*

---

## Cycle 2 Review

**Reviewer**: ptt-ticket-reviewer (Phase 3.5)
**Date**: 2026-10-13
**Cycle**: 2 of 2 maximum
**Source**: docs/brain/B198/04-tickets.md (revised after Cycle 1 TICKET_REVIEW_FAIL)
**Prior violations**: VIO-1 (T2a log string 1: `"ATM-entry-qty="` → must be `"ATM-send qty="`), VIO-2 (T2a log string 2: `"ATM-order-created qty="` → must be `"ATM-order qty="`)

---

### VIO-1 RESOLUTION CHECK

**Change T2a-1 log string** (04-tickets.md line ~531):
```
follower.Name + ": ATM-send qty=" + signal.Quantity + " tmpl=" + namedMode.TemplateName
```
Spec mandate (B198-orchestrator-prompt.md line 277): `": ATM-send qty=" + signal.Quantity + " tmpl="` ✅ **STRING MATCHES SPEC.**

Downstream propagation check:
- T2a-01 [Fact] assertion (line ~596): `"ATM-send qty=" + signal.Quantity + " tmpl=" + namedMode.TemplateName` ✅ FIXED
- T2a SIM protocol (line ~557): `": ATM-send qty=10 tmpl=MGC $400 SL4"` ✅ FIXED
- T2a acceptance criteria item 2 (line ~641): `Select-String ... -Pattern "ATM-send qty="` ✅ FIXED

**VIO-1: RESOLVED ✅**

---

### VIO-2 RESOLUTION CHECK

**Change T2a-2 log string** (04-tickets.md line ~544):
```
follower.Name + ": ATM-order qty=" + order.Quantity
```
Spec mandate (B198-orchestrator-prompt.md line 279): `": ATM-order qty=" + order.Quantity` ✅ **STRING MATCHES SPEC.**

Downstream propagation check:
- T2a-02 [Fact] assertion (line ~601): `"ATM-order qty=" + order.Quantity` ✅ FIXED
- T2a SIM protocol (line ~558): `": ATM-order qty="` ✅ FIXED
- T2a acceptance criteria item 3 (line ~642): `Select-String ... -Pattern "ATM-order qty="` ✅ FIXED
- T2b SIM evidence template H1/H2/H3 (lines ~573-576): All three hypotheses now use `ATM-order qty=10` and `ATM-order qty=5` ✅ FIXED

**VIO-2: RESOLVED ✅**

---

### Full Re-Scan — Ticket 1 (SimStop N-General Bracket Support)

No changes from Cycle 1 (T1 was not revised). All prior checks hold.

- Traceability: PASS (all 6 Part A predicates + 5 Part B methods + DW cards present) ✅
- Scope lock: PASS (touches only CopyEngine.cs + B198Tests.cs) ✅
- JS Pre-Check: PASS (no lock, no async void, no throw, all CYC ≤8) ✅
- CYC Pre-Check: PASS (max: TryParseStopSuffix=5, IsNativeExitName=5) ✅
- NT8 Check: PASS ✅
- Test Coverage: PASS (16 [Fact] tests T1-01..T1-16) ✅
- Scan Checklist: PASS (SCAN-01 through SCAN-07 all present with PowerShell commands and expected results) ✅
- File Routing: PASS (Wave workspace paths only) ✅

**T1 VERDICT: TICKET_REVIEW_PASS**

---

### Full Re-Scan — Ticket 2a (QTY Bug Instrumentation)

- Traceability: PASS — VIO-1 and VIO-2 both resolved. Spec req IDs present. DW-B198-QTY-01 (Phase A) present. Plan §4 T2a referenced. All log strings now match spec and REVIEW_PASS plan. ✅
- Scope lock: PASS (touches only SendCopyWithAtm method + B198Tests.cs T2a section + sim-evidence.md) ✅
- JS Pre-Check: PASS (no lock, no async void, no throw; `"ATM-send qty="` and `"ATM-order qty="` are ASCII-only; `?.Invoke` is not McCabe branch; CYC 4→4) ✅
- CYC Pre-Check: PASS (SendCopyWithAtm CYC 4→4; 0 McCabe delta confirmed) ✅
- NT8 Check: PASS (StatusUpdate?.Invoke is null-conditional event invocation — NT8 safe; no lock) ✅
- Test Coverage: PASS (T2a-01 and T2a-02 both [Fact] source-text assertions present, both now assert correct spec-compliant strings) ✅
- Scan Checklist: PASS (SCAN-01 through SCAN-07 all present with commands and expected results) ✅
- File Routing: PASS (CopyEngine.cs + B198Tests.cs + ticket-2a-sim-evidence.md — all Wave workspace or docs/brain paths) ✅

**T2a VERDICT: TICKET_REVIEW_PASS**

---

### Full Re-Scan — Ticket 2b (QTY Bug Fix)

No changes from Cycle 1 (T2b was not revised). All prior checks hold.

- Traceability: PASS (DW-B198-QTY-01 Phase B, H1/H2/H3 decision tree, T2a pre-condition gate, no phantom work) ✅
- Scope lock: PASS (contingency clause present; H1/H2/H3 closure paths explicit) ✅
- JS Pre-Check: PASS (ConcurrentDictionary, no lock, no async void, CYC all ≤8) ✅
- CYC Pre-Check: PASS (CheckAtmTemplatesForReverse(string)=7, SendCopyWithAtm=5, TryGetAtmTemplateGroupCount=2; max=7 ≤8) ✅
- NT8 Check: PASS ✅
- Test Coverage: PASS (T2b-01 and T2b-02 [Fact] tests present; SKIPPED path for H1 documented) ✅
- Scan Checklist: PASS (SCAN-01 through SCAN-07 all present) ✅
- File Routing: PASS ✅

**T2b VERDICT: TICKET_REVIEW_PASS**

---

### Full Re-Scan — Ticket 3 (fo=NULL Drain Path Fix)

No changes from Cycle 1 (T3 was not revised). All prior checks hold. Specific checklist items re-confirmed:

- SendCopyWithAtm return type change `bool→Order?`: ✅ (Change T3-2 specifies: `private Order? SendCopyWithAtm(...)`)
- 6 call sites enumerated: ✅ (table at lines ~949-955: lines 2759, 2870, 2987, 4522, 6966, 7104)
- `_drainFollowerEntryOrders` field: ✅ (Change T3-1; `private readonly ConcurrentDictionary<string, Order>`)
- TryGetValue+TryRemove+drainFo wiring: ✅ (Change T3-4: `TryGetValue(acctKey, out Order drainFo)` + `TryRemove(acctKey, out _)` + `SyncFollowerBracket(acc, drainFo, ...)`)
- Traceability: PASS ✅
- Scope lock: PASS ✅
- JS Pre-Check: PASS ✅
- CYC Pre-Check: PASS (max: SubmitDrainedEntry=6; all ≤8) ✅
- NT8 Check: PASS ✅
- Test Coverage: PASS (4 [Fact] tests T3-01..T3-04) ✅
- Scan Checklist: PASS (SCAN-01 through SCAN-07 all present) ✅
- File Routing: PASS ✅

**T3 VERDICT: TICKET_REVIEW_PASS**

---

### Full Re-Scan — Ticket 4 (ATM Template Dir Startup Fix)

No changes from Cycle 1 (T4 was not revised). All prior checks hold. Specific checklist items re-confirmed:

- Old path strings shown: ✅ (`"Templates", "ATM"` in Change T4-1 CURRENT CODE block)
- New path strings shown: ✅ (`"templates", "AtmStrategy"` in Change T4-1 REPLACEMENT block)
- Dir-not-found INFO→WARN upgrade with path appended: ✅ (Change T4-2; `[PTT-STARTUP-WARN] ATM template dir not found: " + atmDir`)
- Existing Reverse=true warning preserved (not modified): ✅ (explicitly noted in Change T4-2)
- Acceptance criteria verify exact log strings: ✅ (items 4-5 use Select-String for `[PTT-STARTUP-WARN]` and confirm ZERO matches for old `[PTT-STARTUP-INFO]` string)
- Traceability: PASS ✅
- Scope lock: PASS ✅
- JS Pre-Check: PASS ✅
- CYC Pre-Check: PASS (both overloads unchanged: 1 and 6) ✅
- NT8 Check: PASS ✅
- Test Coverage: PASS (2 [Fact] tests T4-01 and T4-02) ✅
- Scan Checklist: PASS (SCAN-01 through SCAN-07 all present) ✅
- File Routing: PASS ✅

**T4 VERDICT: TICKET_REVIEW_PASS**

---

### Aggregate Test Count Verification

| Ticket | Mandated | Actual | Match |
|--------|----------|--------|-------|
| T1 | 16 | 16 (T1-01..T1-16) | ✅ |
| T2a | 2 | 2 (T2a-01, T2a-02) | ✅ |
| T2b | 2 | 2 (T2b-01, T2b-02) | ✅ |
| T3 | 4 | 4 (T3-01..T3-04) | ✅ |
| T4 | 2 | 2 (T4-01, T4-02) | ✅ |
| **Total** | **26** | **26** | **✅** |

---

### Violations Summary — Cycle 2

| VIO # | Status | Description |
|-------|--------|-------------|
| VIO-1 | **RESOLVED** | T2a log string 1 now reads `"ATM-send qty="` (spec-compliant). All downstream references (test T2a-01, SIM protocol, acceptance criteria item 2) updated. |
| VIO-2 | **RESOLVED** | T2a log string 2 now reads `"ATM-order qty="` (spec-compliant). All downstream references (test T2a-02, SIM protocol, acceptance criteria item 3, T2b SIM evidence template H1/H2/H3) updated. |

**No new violations found in Cycle 2.**

---

## Overall Cycle 2: TICKET_REVIEW_PASS

All 5 tickets (T1, T2a, T2b, T3, T4) pass all checks:
- Traceability: PASS on all tickets
- JS Pre-Check: PASS on all tickets
- CYC Pre-Check: PASS on all tickets (maximum CYC = 7, ceiling = 8)
- NT8 Check: PASS on all tickets
- Test Coverage: PASS on all tickets (26 total [Fact] tests)
- Scan Checklist (SCAN-01..SCAN-07): PASS on all tickets
- File Routing: PASS on all tickets

Both prior violations (VIO-1, VIO-2) are confirmed RESOLVED. No new violations introduced.

**This is the final cycle. TICKET_REVIEW_PASS is issued. Safe to spawn ptt-engineer.**

---

*Written by: ptt-ticket-reviewer*
*Epic: B198*
*Phase: 3.5 (Ticket Review — Cycle 2)*
*Returns: TICKET_REVIEW_PASS*
