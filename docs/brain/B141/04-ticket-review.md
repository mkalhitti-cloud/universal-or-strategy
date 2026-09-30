# Ticket Review: B141 — OCO Cascade Dual-Resubmit

**Reviewer**: ptt-ticket-reviewer (Phase 3.5)
**Block**: B141
**Tickets file**: `docs/brain/B141/04-tickets.md`
**Plan file**: `docs/brain/B141/02-architecture-plan.md`
**Reviewed**: 2026-09-01
**Rules source**: `docs/standards/jane-street/RULES_CATALOG.md`

---

## T1 — OCO Cascade Dual-Resubmit (single ticket)

### Traceability

| Item | Status | Notes |
|------|--------|-------|
| Ticket references DW-B153 | PASS | Cited in Spec Requirements Closed table and inline comment |
| Ticket references DW-B154 | PASS | Cited in Spec Requirements Closed table and Change 5 inline comment |
| All 5 code changes trace to architecture plan | PASS | Changes 1-5 map to Plan Sections 4.1, 4.2, 4.2 (helpers), 4.2 (helper), 4.3 respectively |
| SyncFollowerBracket BEFORE/AFTER exact match to plan | PASS | Plan Section 4.1 BEFORE/AFTER verbatim — matches ticket Change 1 verbatim (comment text differs trivially: plan uses "+ B141", ticket uses "+ DW-B153"; semantically identical) |
| CaptureLinkedTargetPrice signature matches plan | PASS | `private double? CaptureLinkedTargetPrice(Account acc, string stopName)` — exact match |
| TryParseStopSuffix signature matches plan | PASS | `private static bool TryParseStopSuffix(string stopName, out string suffix)` — exact match |
| IsTargetOrderLive signature matches plan | PASS | `private static bool IsTargetOrderLive(Order o)` — exact match |
| ResubmitTargetAfterCascade signature matches plan | PASS | `private void ResubmitTargetAfterCascade(Account acc, Order stpOrder, double targetPrice, Order leaderOrder)` — exact match |
| No phantom work (in ticket but not in plan) | PASS | Every item in T1 traces to Plan Sections 3, 4.1, 4.2, 4.3, 6, K |
| No missing plan work (in plan but not in ticket) | PASS | All 5 method changes, 7 tests, DW updates, SIM gates, and Method Placement from plan are represented in T1 |

**Traceability: PASS**

---

### JS Pre-Check

| Rule | Check | Status | Notes |
|------|-------|--------|-------|
| JS-021 | No `lock()` in any new/modified method | PASS | No `lock()` in any of the 5 code changes; ticket explicitly calls out JS-021 per-method |
| JS-033 | No `async void` in any new/modified method | PASS | No async keywords anywhere; all methods are synchronous; JS-033 cited per-method |
| JS-001 | No `throw` in hot path — absorbed via try/catch | PASS | Change 5 wraps CreateOrder in try/catch + StatusUpdate; all helpers use early returns only; JS-001 cited explicitly |
| JS-002 | No reference null return for missing optional value | PASS (with documented exception) | `double?` return from CaptureLinkedTargetPrice is a nullable VALUE type (Nullable<double>), not a reference null — plan Section 4.2 and ticket Change 2 both document this explicitly per JS-002 note. The `suffix = null` in TryParseStopSuffix is an `out` parameter initialisation pattern standard in NT8 .NET 4.8 context, not a missing-value null return. Both cases are documented and architecturally sound. |
| JS-041 | CYC <= 8 for all methods | PASS | See CYC Pre-Check section below |

**JS Pre-Check: PASS**

---

### CYC Pre-Check

All CYC counts use the project-wide convention (confirmed via existing codebase comments at
[`CopyEngine.cs:2250`](src/PropTraderTools/CopyEngine.cs:2250) and
[`CopyEngine.cs:2327`](src/PropTraderTools/CopyEngine.cs:2327)):
base=1, each `if`/`foreach`/`for`/`while`/`?:`=+1, `&&`/`||`=0, `catch`=0.

| Method | Baseline CYC | B141 delta | Post-B141 CYC | Limit | Status |
|--------|-------------|------------|---------------|-------|--------|
| `SyncFollowerBracket` (modified) | 7 (confirmed: matches L2250 comment) | +1 (HasValue check) | **8** | 8 | **PASS — at limit** |
| `CaptureLinkedTargetPrice` (new) | — | — | **4** (base+if+foreach+if) | 8 | **PASS** |
| `TryParseStopSuffix` (new) | — | — | **3** (base+if+if) | 8 | **PASS** |
| `IsTargetOrderLive` (new) | — | — | **1** (base only; pure bool expression) | 8 | **PASS** |
| `ResubmitTargetAfterCascade` (new) | — | — | **4** (base+foreach+if+if) | 8 | **PASS** |

**Baseline CYC-7 for `SyncFollowerBracket` verified** against source at
[`CopyEngine.cs:2281-2285`](src/PropTraderTools/CopyEngine.cs:2281). The 7 branches are:
fo-null(1), price-delta(2), ATM-STP-branch-3(3), ATM-TGT-branch-3b(4), IsTrailingStop-branch-4(5),
isStop-inner-branch-5(6 — confirmed at lines 2292, 2300), plus base=1. Total = CYC 7. Adding the
`capturedTargetPrice.HasValue` check (+1) brings it to CYC 8 — at the JS-041 limit.

**Engineer MUST NOT add any further branch to `SyncFollowerBracket`. DW-B141-STP-CYC8-WALL open.**

**CYC Pre-Check: PASS**

---

### NT8 Constraint Check

| Constraint | Status | Notes |
|------------|--------|-------|
| CreateOrder 12-arg signature with arg12 = `(NinjaTrader.Cbi.CustomOrder)null` | PASS | Change 5 explicitly casts arg12; cross-checked against existing pattern at [`CopyEngine.cs:1096-1109`](src/PropTraderTools/CopyEngine.cs:1096) and [`CopyEngine.cs:2505-2517`](src/PropTraderTools/CopyEngine.cs:2505) |
| oco="" for PTT-TGT-Drag (not in ATM OCO group) | PASS | Change 5 passes `""` as arg9; note confirmed vs existing `SyncAtmFollowerTarget` Block B (line 2514) |
| `acc.Submit(new[]{order})` called after CreateOrder | PASS | `acc.Submit(new[] { newTarget })` present in Change 5, Block B |
| `acc.Orders` enumeration (not Position.Orders or other) | PASS | Both Change 2 (CaptureLinkedTargetPrice) and Change 5 (ResubmitTargetAfterCascade Block A-Prime) use `acc.Orders.ToList()` |
| `OrderState.Working` AND `OrderState.Accepted` both checked | PASS | `IsTargetOrderLive` (Change 4) checks both states; T_B141_02 specifically validates Accepted state coverage |
| No `DateTime.Now` | PASS | `NinjaTrader.Core.Globals.MaxDate` used in Change 5; explicitly called out in NT8 API constraints |
| No `async void` in lifecycle methods | PASS | All 5 methods are synchronous |
| No `Account.All` outside Loaded handler | PASS | No `Account.All` usage in any new/modified method |
| PTT- prefix on new order name | PASS | `"PTT-TGT-Drag"` — compliant |
| No `FontFamily` set on WPF element | PASS | No WPF UI elements in scope |
| No hardcoded hex colors | PASS | Not applicable |

**NT8 Constraint Check: PASS**

---

### 7-Scan Checklist Presence (Defense in Depth — Non-Negotiable)

Per role contract: each ticket MUST carry its own full 7-scan checklist. Missing any scan = TICKET_REVIEW_FAIL.

| Scan | Present | Command specified | Expected result stated |
|------|---------|-------------------|----------------------|
| SCAN-01 — No `lock()` | PASS | `grep -n "lock(" ... \| Select-String -Pattern "..."` | "0 hits" stated |
| SCAN-02 — No `async void` | PASS | `grep -n "async void" ... \| Select-String -Pattern "..."` | "0 hits" stated |
| SCAN-03 — No `throw new` in hot paths | PASS | `grep -n "throw new" ... \| Select-String -Pattern "..."` | "0 hits" stated |
| SCAN-04 — CYC verification | PASS | Manual line-by-line table with expected CYC and PASS/FAIL for all 5 methods | Table with "PASS" for all rows |
| SCAN-05 — ASCII-only | PASS | Regex scan for non-ASCII in new string literals; specific literals listed | "0 new non-ASCII" stated |
| SCAN-06 — Build clean | PASS | `dotnet build src/PropTraderTools/PropTraderTools.csproj` | "0 errors, 0 CS1503, 0 CS0246" |
| SCAN-07 — Tests pass | PASS | `dotnet test ... --filter "B141"` | "7/7 pass" |

**Scan Checklist Presence: PASS**

---

### Test Coverage

| Item | Status | Notes |
|------|--------|-------|
| Test file path stated | PASS | `tests/PropTraderTools.Tests/B141Tests.cs` |
| Framework is xUnit [Fact] | PASS | "xUnit only — NEVER NUnit or MSTest" explicitly stated; all 7 tests are `[Fact]` |
| T_B141_01 present with Arrange/Act/Assert | PASS | CaptureLinkedTargetPrice Stop1 -> Target1 LimitPrice |
| T_B141_02 present with Arrange/Act/Assert | PASS | Stop2 -> Target2, Accepted state coverage |
| T_B141_03 present with Arrange/Act/Assert | PASS | Stop3 -> Target3 coverage |
| T_B141_04 present with Arrange/Act/Assert | PASS | Cancelled target -> null return |
| T_B141_05 present with Arrange/Act/Assert | PASS | End-to-end: SyncFollowerBracket -> CreateOrder + Submit called when target found |
| T_B141_06 present with Arrange/Act/Assert | PASS | Guard: no CreateOrder when target absent |
| T_B141_07 present with Arrange/Act/Assert | PASS | Regression: SyncAtmFollowerBracket unconditional in BOTH scenarios (target found + absent) |
| T_B141_07 covers regression (always-called invariant) | PASS | Explicitly verifies both scenario A and scenario B |
| All public/internal methods have [Fact] coverage | PASS | CaptureLinkedTargetPrice: T01-T04; full SyncFollowerBracket branch-3 path: T05-T07; TryParseStopSuffix covered via T01-T04 call chain; IsTargetOrderLive covered via T02 (Accepted) and T04 (Cancelled) |

**Test Coverage: PASS**

---

### Completeness

| Item | Status | Notes |
|------|--------|-------|
| All 5 code changes fully specified with verbatim code | PASS | Changes 1-5 each contain full implementation block with "engineer MUST match verbatim" instruction |
| Insertion location for new helpers stated | PASS | "immediately after the closing brace of SyncFollowerBracket (approximately after line 2317)" |
| Ordering of new helper methods stated | PASS | Explicit ordering: CaptureLinkedTargetPrice -> TryParseStopSuffix -> IsTargetOrderLive -> ResubmitTargetAfterCascade |
| OrderAction inversion handling | PASS | Plan Section 4.3 and ticket Change 5 comment both explicitly justify NO inversion (ATM brackets share OrderAction direction; cross-confirmed by SyncAtmFollowerTarget Block B at line 2507 using `fo.OrderAction` directly) |
| `leaderOrder` scope in SyncFollowerBracket branch (3) | PASS | Confirmed by source read: line 2288 (`SyncAtmFollowerTarget(acc, fo, newPrice, leaderOrder)`) uses `leaderOrder` within branch 3b in the same method scope |
| CaptureLinkedTargetPrice called BEFORE SyncAtmFollowerBracket | PASS | Change 1 replacement code: capture fires first (line 1 of replacement block), SyncAtmFollowerBracket fires second |
| SyncAtmFollowerBracket call is UNCONDITIONAL | PASS | In Change 1, `SyncAtmFollowerBracket` is NOT inside the `if (capturedTargetPrice.HasValue)` guard — it executes unconditionally. The HasValue guard gates only `ResubmitTargetAfterCascade`. |
| DW updates after T1 | PASS | All 6 DW items (B153, B154, B140-01, B140-02, B140-03, B141-STP-CYC8-WALL) listed with post-T1 status |
| SIM verification gates defined | PASS | Gates 1, 2, 3 defined with procedure, pass criteria, and FAIL protocol |
| File routing: .cs to Wave workspace | PASS | `src/PropTraderTools/CopyEngine.cs` and `tests/PropTraderTools.Tests/B141Tests.cs` — both in `C:\WSGTA\universal-or-strategy\` (Wave workspace only) |

**Completeness: PASS**

---

### File Routing

| Check | Status |
|-------|--------|
| .cs source paths point to `src/PropTraderTools/` (Wave workspace) | PASS |
| Test file path points to `tests/PropTraderTools.Tests/` (Wave workspace) | PASS |
| No Director workspace paths (`universal-or-strategy-director`) referenced | PASS |

**File Routing: PASS**

---

### Observations (Non-Blocking)

1. **`IsTargetOrderLive` null guard additive**: The ticket's Change 4 implementation adds `o != null &&` to the expression body, which is absent from the plan's Section 4.2 version. The ticket version is strictly safer. CYC impact: expression body with `&&` not counted per project convention — CYC remains 1. No violation.

2. **`leaderOrder` parameter unused in ResubmitTargetAfterCascade Block B**: Noted in ticket as "not used in Block B — included for forward compatibility (Phase C pattern)". This may generate a compiler warning (CS0168 or IDE0060 unused parameter). Engineer should suppress with `#pragma warning disable IDE0060` or `_ = leaderOrder;` if build gate requires zero warnings. This is not a ticket failure — it is an implementation-time concern that the engineer must handle.

3. **`SyncFollowerBracket` CYC 8 wall**: DW-B141-STP-CYC8-WALL correctly created. Any future PR touching `SyncFollowerBracket` MUST be flagged to the reviewer — no further branching permitted without prior extraction.

4. **`suffix = null` in TryParseStopSuffix**: Standard `out` parameter initialisation in .NET 4.8 context. Not a JS-002 violation (out parameter, not a missing-value null return). Documented correctly in ticket.

---

### Verdict

| Check Category | Result |
|----------------|--------|
| Traceability | PASS |
| JS Pre-Check (JS-021, JS-033, JS-001, JS-002, JS-041) | PASS |
| CYC Pre-Check (all 5 methods <= 8) | PASS |
| NT8 Constraints | PASS |
| 7-Scan Checklist Presence (SCAN-01 through SCAN-07) | PASS |
| Test Coverage (7 xUnit [Fact] tests T_B141_01 through T_B141_07) | PASS |
| Completeness (verbatim code, placement, ordering, invariants) | PASS |
| File Routing (Wave workspace only) | PASS |

---

## Overall: TICKET_REVIEW_PASS

**Zero violations. Zero rule citations required.**

T1 is cleared for ptt-engineer. The engineer is bound by:
- Change 1 verbatim replacement code (SyncFollowerBracket branch 3)
- Changes 2-5 verbatim implementation blocks
- Method insertion location: after SyncFollowerBracket closing brace (~line 2317)
- Method ordering: CaptureLinkedTargetPrice, TryParseStopSuffix, IsTargetOrderLive, ResubmitTargetAfterCascade
- 7-scan checklist MUST be executed and reported in ticket-1-completion.md before BUILD_PASS is declared
- 7 xUnit [Fact] tests in `tests/PropTraderTools.Tests/B141Tests.cs` following B140Tests.cs pattern
- SyncFollowerBracket CYC MUST remain 8 — no further branching

**TICKET_REVIEW_PASS**
