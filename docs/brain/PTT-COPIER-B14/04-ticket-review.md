# PTT-COPIER-B14 Ticket Review
# Phase: 3.5 (ptt-ticket-reviewer)
# Date: 2026-07-14
# Reviewer: ptt-ticket-reviewer
# Input tickets: docs/brain/PTT-COPIER-B14/04-tickets.md
# Input plan:    docs/brain/PTT-COPIER-B14/02-architecture-plan.md (Status: REVIEW_PASS)
# Reference:     docs/brain/PTT-COPIER-B13/06-deferred-backlog.md
# Rules:         docs/standards/jane-street/RULES_CATALOG.md
#                docs/standards/NT8_COMPILER_RULES.md

---

## Ticket Review: PTT-COPIER-B14

---

### T1 — DW-B12-DEFER-02: Auto-Trail Stop from BE CONNECTED State

**Files:** `CopyEngine.cs`, `TradeCopierPanel.cs`, `CopyEngineTests.cs`
**Spec req:** DW-B12-DEFER-02 (original)

---

#### Traceability: PASS

- DW-B12-DEFER-02 (original) confirmed in B13 backlog §Open Items for B14 with priority P3 and source
  "B12 arch plan §1 Shelved". Ticket target is correct.
- All 5 new fields specified with types, modifiers, and comment rationale (§1.1).
- ArmTrailBe: CYC=4, null guards ×2, IsFlat guard, volatile release fence ordering,
  AccountItemUpdate subscription — all present (§1.2). PASS.
- DisarmTrailBe: CYC=2, CAS disarm, idempotent comment — all present (§1.3). PASS.
- OnTrailBeAccountUpdate: CYC=5, state check first, item filter, PnL improvement check,
  CAS update _trailBeLastPnl, Interlocked.Increment + BreakEven — all present (§1.4). PASS.
- OnBeConnected: modified to call ArmTrailBe after BreakEven — present (§1.5). PASS.
- OnBeClick Connected→Idle: modified to call DisarmTrailBe alongside DisarmPendingBe — present (§1.6). PASS.
- Cleanup path: DisarmTrailBe wired in Detach() with exact BEFORE/AFTER blocks (§1.7). PASS.
  Note: Architecture plan §2.6 said "the engineer must locate" the cleanup method; the ticket
  specifies Detach() precisely with BEFORE/AFTER source — this is MORE precise than the plan and
  is a correct improvement. No phantom work.
- 6 [Fact] xUnit tests specified (§1.8, T-B14-T1-A through T-B14-T1-F). PASS.
- No phantom work (all items trace to plan §2.x or spec DW-B12-DEFER-02). PASS.
- No missing plan items (plan §2.1–§2.7 fully covered by ticket §1.1–§1.8). PASS.

---

#### JS Pre-Check: PASS

| Rule | Check | Result |
|------|-------|--------|
| JS-021 (P0) | No `lock()` in any new or modified method description | PASS — Interlocked.CompareExchange, Interlocked.Read, Interlocked.Increment only |
| JS-001 (P0) | No `throw` in hot path | PASS — OnTrailBeAccountUpdate has no throw; comment at §1.4 confirms BreakEven wraps acc.Change() internally |
| JS-002 (P0) | No `return null` | PASS — all guard exits use bare `return;` in all new methods |
| JS-023 (P1) | Cross-thread fields are volatile | PASS — _trailBeState (volatile int), _trailBeBufferTicks (volatile int), _trailBeLastPnl (volatile long); plain refs _trailBeAccount / _trailBeInstrument are single-writer UI thread with volatile release fence from _trailBeState = 1 |
| JS-033 (P0) | No `async void` (non-event-handler) | PASS — OnTrailBeAccountUpdate is plain `void`; OnBeConnected is plain `void` (ticket §1.5 AFTER block shows `private void`, not `async void`) |

**Note on OnBeConnected:** The architecture plan §2.4 contained a draft that showed `async void` with
`await Task.CompletedTask`. The ticket's BEFORE block (§1.5) correctly reflects the existing plain
`void` source from B12. The ticket does NOT introduce `async void`. The plan draft error does not
propagate to the ticket. This is not a ticket violation.

---

#### CYC Pre-Check: PASS

| Method | File | CYC | Limit | Status |
|--------|------|-----|-------|--------|
| ArmTrailBe | CopyEngine.cs | 4 | 8 | PASS |
| DisarmTrailBe | CopyEngine.cs | 2 | 8 | PASS |
| OnTrailBeAccountUpdate | CopyEngine.cs | 5 | 8 | PASS |
| OnBeConnected | TradeCopierPanel.cs | 3 (was 2) | 8 | PASS |
| OnBeClick (Connected case) | TradeCopierPanel.cs | 5 (unchanged) | 8 | PASS |
| Detach() | TradeCopierPanel.cs | unchanged | 8 | PASS |

No at-risk methods. All new/modified methods are well within the CYC ≤ 8 gate.

---

#### NT8 Check: PASS

| Rule | Check | Result |
|------|-------|--------|
| NT8-003 | `_trailBeLastPnl` is `volatile long`, not `volatile double` | PASS — declared as `private volatile long _trailBeLastPnl = 0L;` in §1.1 |
| NT8-003 | BitConverter.DoubleToInt64Bits / BitConverter.Int64BitsToDouble used | PASS — present in ArmTrailBe (§1.2) and OnTrailBeAccountUpdate (§1.4) |
| NT8-018 / JS-021 | No `lock()` in any new method | PASS — Interlocked only |
| NT8-019 / JS-033 | OnTrailBeAccountUpdate is plain `void`, not `async void` | PASS — explicitly stated "plain void" in §1.4 header comment |
| NT8-026 | No `order.TrailPrice` reference | PASS — not present anywhere in T1 |
| NT8-031 | `using System.Threading` confirmed present | PASS — §1.1 states "already present in CopyEngine.cs (added B10 T2). No new using directive needed." |
| NT8-034 | No `Math.Clamp` in new/modified code | PASS — NT8 constraints table at end of tickets confirms "Math.Clamp not used" |
| NT8-007 | No new CreateOrder calls | PASS — trail uses acc.Change() via BreakEven; no CreateOrder |
| NT8-013 | No DateTime.Now | PASS — not present |
| NT8-014 | No new signal names | PASS — no new CreateOrder calls |
| NT8-020 | No new SolidColorBrush | PASS — no new brushes in T1 |

---

#### Test Coverage: PASS

All new public/internal methods have [Fact] tests specified:

| Method | Test | CYC | Framework |
|--------|------|-----|-----------|
| ArmTrailBe | T-B14-T1-A: ArmTrailBe_MethodExists_WithCorrectSignature | 1 | [Fact] xUnit |
| ArmTrailBe (null instr guard) | T-B14-T1-B: ArmTrailBe_NullInstrument_NoException | 1 | [Fact] xUnit |
| DisarmTrailBe (not armed) | T-B14-T1-C: DisarmTrailBe_WhenNotArmed_NoException | 1 | [Fact] xUnit |
| DisarmTrailBe (idempotent) | T-B14-T1-D: DisarmTrailBe_Idempotent_NoExceptionOnDoubleCall | 1 | [Fact] xUnit |
| _trailBeLastPnl encoding pattern | T-B14-T1-E: TrailBe_BitConverter_PnlEncoding_RoundTrip | 1 | [Fact] xUnit |
| OnTrailBeAccountUpdate CAS logic | T-B14-T1-F: TrailBe_CasLogic_NewBitsGreaterThanOld_CasSucceeds | 1 | [Fact] xUnit |

All 6 tests use `[Fact]` (xUnit). No NUnit `[Test]` or MSTest `[TestMethod]` attributes. PASS.

---

#### Scan Checklist: PASS

Ticket §1.9 contains the full 7-scan table:

| Scan | Present | Expected Result | Rule |
|------|---------|-----------------|------|
| SCAN-01 | YES | 0 results — no `lock(` in new/modified methods | JS-021 P0 |
| SCAN-02 | YES | 0 results — OnTrailBeAccountUpdate is plain void | JS-033 P0 |
| SCAN-03 | YES | 0 results — bare `return;` only | JS-002 P0 |
| SCAN-04 | YES | CYC audit: ArmTrailBe(4), DisarmTrailBe(2), OnTrailBeAccountUpdate(5), OnBeConnected(3) — all ≤ 8 | CYC gate |
| SCAN-05 | YES | `_trailBeLastPnl` is `volatile long`; no `volatile double` | NT8-003 |
| SCAN-06 | YES | 0 results — Math.Clamp not present | NT8-034 |
| SCAN-07 | YES | BitConverter.Int64BitsToDouble / BitConverter.DoubleToInt64Bits present in ArmTrailBe and OnTrailBeAccountUpdate | NT8-003 compliance |

All 7 scans present. PASS.

---

#### File Routing: PASS

All C# source paths reference `c:\WSGTA\universal-or-strategy\src\PropTraderTools\`. No paths point to
the Director workspace for .cs files. PASS.

---

### T1 VERDICT: TICKET_REVIEW_PASS

---

### T2 — DW-B12-DEFER-04: Test Name Alignment

**File:** `CopyEngineTests.cs`
**Spec req:** DW-B12-DEFER-04

---

#### Traceability: PASS

- DW-B12-DEFER-04 confirmed in B13 backlog §Open Items for B14 with priority P3 and source
  "B12 T1 verification WARN-03". Ticket target is correct.
- All 5 B12 §T1 §1.10 contract names specified in the mapping table (§2.1). PASS.
- 4 exact BEFORE/AFTER rename declarations provided with approximate source line numbers (§2.2). PASS.
- 1 new test body provided for the missing short-direction Trim path (§2.3). PASS.
- Body preservation rule is explicitly stated: "Only the `public void <MethodName>()` declaration
  line changes. Test bodies, comments, assertions, and spacing are PRESERVED EXACTLY." PASS.
- No phantom work (all 5 items trace to DW-B12-DEFER-04 and plan §3.2). PASS.
- No missing plan items (plan §3.1–§3.4 fully covered by ticket §2.1–§2.3). PASS.

---

#### JS Pre-Check: PASS

| Rule | Check | Result |
|------|-------|--------|
| JS-021 (P0) | No `lock(` introduced in test renames or new test body | PASS — no lock() in any test method |
| JS-001 (P0) | No `throw` in new test body | PASS — test body uses Record.Exception() pattern only |
| JS-002 (P0) | No `return null` in new test body | PASS — no return null; test uses Assert.Null(ex) |
| JS-033 (P0) | No `async void` | PASS — all renamed and new tests are plain `public void` |

---

#### CYC Pre-Check: PASS

| Action | CYC | Status |
|--------|-----|--------|
| 4 renamed tests (declaration-only change) | 1 each (unchanged) | PASS |
| 1 new test body (§2.3) | 1 (no branch logic) | PASS |

---

#### NT8 Check: PASS

| Rule | Check | Result |
|------|-------|--------|
| NT8-014 | PTT- signal name check in new test | PASS — `const string signalName = "PTT-TrimLimit"` verified with Assert.True(signalName.StartsWith("PTT-",...)) |
| NT8-034 | No Math.Clamp in test code | PASS — not present |
| NT8-018 | No lock() | PASS |

---

#### Test Coverage: PASS

T2 covers exactly the 4 renames and 1 new test required by DW-B12-DEFER-04:

| # | Contract Name | Action | [Fact] Attribute |
|---|---------------|--------|-----------------|
| 1 | Trim_LimitOverload_LongPosition_EmitsSellLimitAtRefPlusTick | RENAME (declaration only) | [Fact] (carried from existing test) |
| 2 | Trim_LimitOverload_ShortPosition_EmitsBuyToCoverLimitAtRefMinusTick | ADD NEW (full body in §2.3) | [Fact] xUnit |
| 3 | Flatten_LimitOverload_LongPosition_EmitsSellLimitFullQty | RENAME (declaration only) | [Fact] (carried) |
| 4 | Flatten_LimitOverload_ShortPosition_EmitsBuyToCoverLimitFullQty | RENAME (declaration only) | [Fact] (carried) |
| 5 | DispatchCopy_PttPrefixGate_SkipsOrderNamedPttTrimLimit | RENAME (declaration only) | [Fact] (carried) |

New test at §2.3 uses `[Fact]` (xUnit). PASS.

---

#### Scan Checklist: PASS

Ticket §2.4 contains the full 7-scan table:

| Scan | Present | Expected Result | Rule |
|------|---------|-----------------|------|
| SCAN-01 | YES | 0 new `lock(` in test file | JS-021 P0 |
| SCAN-02 | YES | 0 new `async void` in test file | JS-033 P0 |
| SCAN-03 | YES | 0 `return null;` in new test body | JS-002 P0 |
| SCAN-04 | YES | All renamed tests CYC=1 (unchanged), new test CYC=1 | CYC gate |
| SCAN-05 | YES | grep confirms 5 contract names now exist in file | DW-B12-DEFER-04 contract |
| SCAN-06 | YES | grep confirms old names are gone (0 results) | Audit trail integrity |
| SCAN-07 | YES | New test uses `[Fact]` (xUnit), not `[Test]` or `[TestMethod]` | Test framework mandate |

All 7 scans present. PASS.

---

#### File Routing: PASS

Single file `c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngineTests.cs` — Wave workspace. PASS.

---

### T2 VERDICT: TICKET_REVIEW_PASS

---

## Spec Coverage Summary

| Spec Req | Description | Ticket | Covered? |
|----------|-------------|--------|----------|
| DW-B12-DEFER-02 (original) | Auto-trail stop from BE CONNECTED state | T1 | YES — exactly once |
| DW-B12-DEFER-04 | Align CopyEngineTests.cs test names to B12 §T1 §1.10 contract | T2 | YES — exactly once |

No uncovered requirements. No duplicate coverage. PASS.

---

## Plan Discrepancy Note (Informational — Not a Ticket Violation)

Architecture plan §2.4 showed `OnBeConnected` with `async void` signature and
`await System.Threading.Tasks.Task.CompletedTask;`. The ticket §1.5 BEFORE block correctly
reflects the existing B12 source (plain `void`). The ticket does not introduce `async void`.
The plan's draft error was not propagated. The ticket is correct. Flagged for architect awareness
only — no action required before engineering proceeds.

---

## Overall: TICKET_REVIEW_PASS

Both tickets pass all checks:
- Traceability: PASS (both specs traced; no phantom/missing work)
- JS Pre-Check: PASS (JS-021, JS-001, JS-002, JS-023, JS-033 — zero violations)
- CYC Pre-Check: PASS (all new/modified methods ≤ 8)
- NT8 Check: PASS (NT8-003, NT8-018, NT8-019, NT8-026, NT8-031, NT8-034 — zero violations)
- Test Coverage: PASS (all new public/internal methods have [Fact] xUnit tests)
- Scan Checklist: PASS (SCAN-01 through SCAN-07 present in both T1 §1.9 and T2 §2.4)
- File Routing: PASS (all .cs paths in Wave workspace c:\WSGTA\universal-or-strategy\src\PropTraderTools\)
