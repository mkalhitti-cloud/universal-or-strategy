# Ticket Review — Block B154
# GTC Exit Order Survival — BE/QX Stale Order Sweep + Drag Orphan Suffix Fix

**Reviewer:** PTT Ticket Reviewer (ptt-ticket-reviewer mode)
**Phase:** 3.5 — Ticket Review
**Source tickets:** `docs/brain/B154/04-tickets.md`
**Source plan:** `docs/brain/B154/02-architecture-plan.md`
**Plan gate:** REVIEW_PASS (confirmed in `docs/brain/B154/02-plan-review.md`)

---

## TICKET 1 — Sweep stale PTT-BE-* and PTT-QX-* GTC exit orders on new entry fill

**Spec Requirements covered:** DW-B154-01 (HIGH), DW-B154-02 (MEDIUM)

### Traceability
**PASS.**
All five atomic changes (a)–(e) map directly to plan §2 TICKET 1:
- (a) `IsPttCopyEntryFilled` predicate → plan §2 "New predicate: `IsPttCopyEntryFilled`"
- (b) `IsPttCopyEntryFilledTestable` seam → plan §2 "Test seam" block
- (c) `TryCancelStaleGtcExitOnEntry` helper → plan §2 "New helper: `TryCancelStaleGtcExitOnEntry`"
- (d) Call site in `OnOrderUpdate` → plan §2 "Call site: `OnOrderUpdate`"
- (e) 6 tests in `CopyEngineTests.cs` → plan §3 "TICKET 1 — New Tests" table (6 rows, exact name match)

No phantom work. No plan items missing from the ticket.

### Atomic Change Completeness
**PASS.**
No "and related changes" shorthand. Each change is fully specified:
- (a) includes exact method signature, full body (both CYC=4 and CYC=3 variants), and required comment header.
- (b) includes exact single-line expression body.
- (c) includes exact method signature, full body with line-level CYC branch annotations, JS rule constraints, state filter table, and log format.
- (d) includes verbatim 4-line insertion, exact before/after anchors.
- (e) includes exact `[Fact]` signature, setup, and assert for all 6 tests.

### Verify Criteria (SIM-testable / unit-testable)
**PASS.**
- Unit gate: 6 named `[Fact]` methods with exact `Assert` predicates. Automated.
- SIM gate: 5 numbered steps with specific observables (log line format, order state transition, Output Tab 1 timing, zero exceptions in Tab 2). No vague "test this" phrases.

### Independence / Sequencing Note
**PASS.**
Header states: "TICKET 1 MUST be fully committed before TICKET 2 begins. Both tickets share `src/PropTraderTools/CopyEngine.cs`. No parallel execution." Architecture plan confirms Q2=NO (tickets are independent in design) and enforces sequential execution because of the shared `.cs` file.

### DW-B154-04 Exclusion
**PASS.**
DW-B154-04 does not appear in TICKET 1's atomic changes, JS rules, CYC table, scan checklist, or verify criteria. The block-level "OUT OF SCOPE — EXPLICIT EXCLUSION" section (lines 545–548) re-states the exclusion explicitly.

### Test Seam Name Consistency
**PASS.**
Atomic change (b) defines: `internal bool IsPttCopyEntryFilledTestable(Order o) => IsPttCopyEntryFilled(o);`
All 5 predicate tests in atomic change (e) call `IsPttCopyEntryFilledTestable(order)` — exact name match. Seam name is consistent throughout.

### JS Rule Constraints
**PASS.**
JS Rule Constraints table (lines 300–305) lists all four required rules with enforcement evidence:
| Rule | Constraint stated? |
|------|--------------------|
| JS-001 (no throw in hot path) | YES — `try { acc.Cancel(stale); } catch { }` |
| JS-002 (void helper, no nullable return) | YES — `TryCancelStaleGtcExitOnEntry` is `void`; `IsPttCopyEntryFilled` returns `bool` |
| JS-021 (no lock / Monitor / Mutex) | YES — `acc.Orders.ToList()` snapshot; no lock |
| JS-033 (no async void) | YES — both new methods are synchronous |

### CYC Constraints
**PASS.**
CYC Constraints table states:
| Method | Required CYC |
|--------|-------------|
| `IsPttCopyEntryFilled` | <= 4 |
| `TryCancelStaleGtcExitOnEntry` | = 8 (max) |
| `OnOrderUpdate` | delta = 0 |
All three rows present with SCAN-7 lizard enforcement specified.

### TriggerPending in State Filter
**PASS.**
Atomic change (c) state filter (lines 152–156) includes `OrderState.TriggerPending` in the cancellable set: `{Working, Accepted, Submitted, Initialized, TriggerPending}`.
Inline comment references HOTFIX-QX-DOUBLE-01 at CopyEngine.cs line 1039 with rationale: "Do NOT use IsCancelAllStateOk (omits TriggerPending). Use IsQxCancelEligible2 pattern."

### Call Site Placement vs. Architecture Plan
**PASS.**
Atomic change (d) states: "after `TryNakedDetect(e)` at line ~1712, before `HandleDrainTerminalState` at line ~1717."
Architecture plan §2 TICKET 1 "Call site: `OnOrderUpdate`" states: "Insert after `TryNakedDetect(e)` at line 1712, before `HandleDrainTerminalState` at line 1717."
Exact match.

### 7-Scan Checklist Presence
**PASS.**
All 7 scans present in TICKET 1 (lines 319–332):
| Scan ID | Present? |
|---------|---------|
| SCAN-7 (`lizard --CCN 8`) | YES |
| SCAN-8 (`grep -rn "lock\s*("`) | YES |
| SCAN-9 (byte-scan CopyEngine.cs > 127) | YES |
| SCAN-9b (byte-scan CopyEngineTests.cs > 127) | YES |
| SCAN-10 (`dotnet build`) | YES |
| SCAN-L (`grep -rn "lock("` secondary) | YES |
| SCAN-B (`dotnet test` — all 6 new tests pass) | YES |
Engineer instructed to report pass/fail per scan in `ticket-1-completion.md`. Verifier anchor is established.

### File Routing
**PASS.**
All `.cs` paths point to Wave workspace (`src/PropTraderTools/CopyEngine.cs`, `src/PropTraderTools/CopyEngineTests.cs`). No Director workspace `.cs` paths present.

### No StrategyBase-only APIs
**PASS.**
All APIs in ticket code snippets are `Account`/`Order`/static utility: `order.Account`, `acc.Orders`, `acc.Cancel()`, `Order.OrderState`, `Order.Instrument`, `Order.Name`, `NinjaTrader.Code.Output.Process`. None require `StrategyBase` context.

### No lock() in Implementation Spec
**PASS.**
No `lock()`, `Monitor.Enter`, `Mutex`, or `SemaphoreSlim` appears in any code block. JS-021 is listed as enforced. SCAN-8 and SCAN-L both mandate 0 matches as completion gates.

### VERDICT: TICKET_REVIEW_PASS

---

## TICKET 2 — Fix `IsPttDragOrphanCancellable` to match suffixed Drag order names (B142)

**Spec Requirements covered:** DW-B154-03 (LOW)

### Traceability
**PASS.**
All four atomic changes (a)–(d) map directly to plan §2 TICKET 2:
- (a) `IsPttDragOrphanCancellable` equality → StartsWith → plan §2 "Change 1: `IsPttDragOrphanCancellable` at line 1998"
- (b) `IsPttDragOrphanCancellableTestable` equality → StartsWith → plan §2 "Change 2: `IsPttDragOrphanCancellableTestable` at line 2016"
- (c) Comment update (add StartsWith rationale, remove "must not be generalized") → plan §2 "Comment update on `IsPttDragOrphanCancellable`"
- (d) 6 tests in `CopyEngineTests.cs` → plan §3 "TICKET 2 — New Tests" table (6 rows, exact name match)

No phantom work. No plan items missing from the ticket.

### Atomic Change Completeness
**PASS.**
No "and related changes" shorthand:
- (a) provides exact SEARCH and REPLACE code blocks for `IsPttDragOrphanCancellable`.
- (b) provides exact SEARCH and REPLACE code blocks for `IsPttDragOrphanCancellableTestable`.
- (c) specifies the exact line to add and the exact phrase to remove ("must not be generalized"), with guidance for XML doc format.
- (d) includes exact `[Fact]` signature, setup, and assert for all 6 tests.

Both `IsPttDragOrphanCancellable` AND `IsPttDragOrphanCancellableTestable` are listed as separate atomic changes — requirement confirmed.

### Verify Criteria (SIM-testable / unit-testable)
**PASS.**
- Unit gate: 6 named `[Fact]` methods with exact `Assert` predicates. Automated.
- SCAN-7 gate: `lizard --CCN 8 src/PropTraderTools/CopyEngine.cs` = 0 warnings. Specific pass criterion stated.

### Independence / Sequencing Note
**PASS.**
TICKET 2 header states: "Prerequisite: TICKET 1 MUST be committed before TICKET 2 begins." Architecture plan confirms Q2=NO (TICKET 2 design is independent of TICKET 1 design). Sequencing note is present.

### DW-B154-04 Exclusion
**PASS.**
DW-B154-04 does not appear anywhere in TICKET 2's atomic changes, JS rules, CYC table, scan checklist, or verify criteria.

### Both Predicates Listed as Atomic Changes
**PASS.**
- Atomic change (a): `IsPttDragOrphanCancellable` at line ~1998 — SEARCH/REPLACE provided.
- Atomic change (b): `IsPttDragOrphanCancellableTestable` at line ~2016 — SEARCH/REPLACE provided.
Both are explicitly listed, not combined or abbreviated.

### Comment Update
**PASS.**
Atomic change (c) specifies:
- **Add:** `// StartsWith: matches suffixed variants PTT-TGT-Drag-N / PTT-STP-Drag-N (B142).`
- **Remove:** whichever comment line contains "must not be generalized" (exact phrase cited).

### CYC Delta = 0
**PASS.**
CYC Constraints table states delta = 0 for both `IsPttDragOrphanCancellable` and `IsPttDragOrphanCancellableTestable` with rationale: "equality replaced by prefix check, same two conditions." SCAN-7 enforcement specified.

### StartsWith Test Coverage — All 6 Test Names
**PASS.**
All four required categories are covered, and all 6 exact method signatures are present:

| Category | Test Name | Present? |
|----------|-----------|---------|
| Exact (PTT-TGT-Drag) | `IsPttDragOrphanCancellableTestable_ShouldReturnTrue_WhenNameIsPttTgtDragExact` | YES |
| Suffixed (PTT-TGT-Drag-2) | `IsPttDragOrphanCancellableTestable_ShouldReturnTrue_WhenNameIsPttTgtDragSuffixed` | YES |
| Exact (PTT-STP-Drag) | `IsPttDragOrphanCancellableTestable_ShouldReturnTrue_WhenNameIsPttStpDragExact` | YES |
| Suffixed (PTT-STP-Drag-1) | `IsPttDragOrphanCancellableTestable_ShouldReturnTrue_WhenNameIsPttStpDragSuffixed` | YES |
| Unrelated | `IsPttDragOrphanCancellableTestable_ShouldReturnFalse_WhenNameIsUnrelated` | YES |
| State-fail | `IsPttDragOrphanCancellableTestable_ShouldReturnFalse_WhenStateIsNotWorking` | YES |

All 6 present. All 4 required categories (exact, suffixed, unrelated, state-fail) covered. Verify criteria section also lists all 6 names.

### JS Rule Constraints
**PASS.**
JS Rule Constraints table lists JS-021 (no lock) with enforcement note. No new locking introduced. All string literals are 7-bit ASCII (`"PTT-TGT-Drag"`, `"PTT-STP-Drag"`, `StringComparison.Ordinal`). ASCII-only constraint is explicitly listed.

### 7-Scan Checklist Presence
**PASS.**
All 7 scans present in TICKET 2 (lines 514–527):
| Scan ID | Present? |
|---------|---------|
| SCAN-7 (`lizard --CCN 8`) | YES |
| SCAN-8 (`grep -rn "lock\s*("`) | YES |
| SCAN-9 (byte-scan CopyEngine.cs > 127) | YES |
| SCAN-9b (byte-scan CopyEngineTests.cs > 127) | YES |
| SCAN-10 (`dotnet build`) | YES |
| SCAN-L (`grep -rn "lock("` secondary) | YES |
| SCAN-B (`dotnet test` — all 6 new tests pass) | YES |
Engineer instructed to report pass/fail per scan in `ticket-2-completion.md`. Verifier anchor is established.

### File Routing
**PASS.**
All `.cs` paths point to Wave workspace (`src/PropTraderTools/CopyEngine.cs`, `src/PropTraderTools/CopyEngineTests.cs`). No Director workspace `.cs` paths present.

### No StrategyBase-only APIs
**PASS.**
Atomic changes (a) and (b) operate on `o.Name` (string) and `orderName` (string) — no NT8 API calls at all. No StrategyBase-only APIs referenced.

### No lock() in Implementation Spec
**PASS.**
No `lock()`, `Monitor`, `Mutex`, or `SemaphoreSlim` in any code block. SCAN-8 and SCAN-L mandate 0 matches.

### VERDICT: TICKET_REVIEW_PASS

---

## Overall Summary

| Check | T1 | T2 |
|-------|----|----|
| Ticket count (exactly 2, no creep) | PASS | PASS |
| Atomic changes explicit (no shorthand) | PASS | PASS |
| Verify criteria SIM/unit testable | PASS | PASS |
| Independent design, sequencing note present | PASS | PASS |
| DW-B154-04 excluded | PASS | PASS |
| Test seam name consistency | PASS | N/A |
| 7-scan checklist (SCAN-7 through SCAN-B) | PASS | PASS |
| TriggerPending in state filter | PASS | N/A |
| JS-001, JS-002, JS-021, JS-033 listed | PASS | N/A (JS-021 listed) |
| Call site placement matches plan | PASS | N/A |
| CYC constraints stated | PASS | PASS |
| Both predicates listed (T2) | N/A | PASS |
| Comment update (add + remove) | N/A | PASS |
| CYC delta=0 (T2) | N/A | PASS |
| StartsWith tests all 6 present (T2) | N/A | PASS |
| No StrategyBase-only APIs | PASS | PASS |
| No lock() in implementation spec | PASS | PASS |

**Violations found:** 0
**Tickets reviewed:** 2
**All 17 checklist items:** PASS

---

## TICKET_REVIEW_PASS
