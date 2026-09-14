# B156/B157 Ticket Review
# Epic: Post-Fix Documentation Pipeline — CopyEngine Three-Bug Repair
# Reviewer: ptt-ticket-reviewer (Phase 3.5)
# Input tickets: docs/brain/B156/04-tickets.md
# Input plan:    docs/brain/B156/02-architecture-plan.md (REVIEW_PASS)
# Plan review:   docs/brain/B156/02-plan-review.md (REVIEW_PASS)
# Source ref:    src/PropTraderTools/CopyEngine.cs (committed; read-only)
# Rules:         docs/standards/RULES_CATALOG.md

---

## T1 — DW-B156-01: Ghost Re-Entry After Leader-Flat Drain

### Traceability
PASS
- Spec req ID DW-B156-01 cited in §a.
- Plan section 3.1 fully covers the inline block approach and the log marker.
- No phantom work: all items in this ticket map to plan §2.1 + §3.1.
- No missing work: plan §3.1 specifies exactly what T1 records.

### JS Pre-Check (JS-001 / JS-002 / JS-021)
PASS
- **JS-001** (no throw): §h SCAN-04 — block uses if/foreach/if with early-return only; no throw. Source lines 1934-1957 confirm. PASS.
- **JS-002** (no null return): §h SCAN-05 — void method, no return value expression. PASS.
- **JS-021** (no lock): §h SCAN-02 — `_pendingDispatchDrains` is `ConcurrentDictionary`; `AbortDrainOnFill` uses `TryRemove` (lock-free). Source confirmed. PASS.

### CYC Pre-Check
PASS (pre-existing debt, DEFER-1 documented)
- Inserted block adds 7 decision points to a pre-existing method.
- Method total: ~13 (exceeds CYC=8).
- **Exemption criteria met**: DEFER-1 annotation is present in both the ticket (§e) and the plan (§7 + §5/JS-013). Decomposition targets (`IsLeaderFlatDrainAbort()`, `RunFollowerEvictPath()`) are named. Tracked in `06-deferred-backlog.md`.
- RULES_CATALOG JS-013: "Pre-existing CYC debt is exempt only if explicitly documented with DEFER annotation." Condition satisfied.
- Accepted per DEFER-1. Does NOT constitute a JS-013 waiver for future epics.

### NT8 Constraints
PASS
- §h SCAN-06: `o.Account`, `o.OrderState`, `o.Instrument`, `OrderState.Filled`, `NinjaTrader.Code.Output.Process` — all AddOnBase-safe, no UI-thread requirement. Source lines 1938-1956 confirm no Dispatcher usage, no async/await.

### Completeness (signatures, log markers, call sites)
PASS
- Method signature: `private void TryEvictFollowerBeSlot(OrderEventArgs e)` — unchanged. Correctly stated as §d "no new methods".
- Log marker: `[DRAIN-ABORT-LEADER-FLAT] leader=X instr=Y aborted=N` — specified with emission condition (abortCount > 0 only). Source line 1952 confirms.
- No call-site change required (method already called at line 1712 via existing routing).

### Test Coverage
PASS
- 4 [Fact] test names listed in §g table:
  1. `Assert.Contains("DRAIN-ABORT-FLAT", src)` (line 9364) — log marker substring check.
  2. `TryEvictFollowerBeSlot_ShouldCallAbortDrain_When...` (line 9382) — structural reflection.
  3. `AbortDrainOnFill_ShouldExist_AsPrivateInstanceHelper` (line 9412) — existence contract.
  4. `TryEvictFollowerBeSlot_LeaderFlatDrainAbort_LogMarkerExists` — assertion upgrade (structural).
- No new public/internal methods introduced; all four tests cover the inline block and the log marker.

### Scan Checklist Presence
PASS — all 7 scans present (SCAN-01 through SCAN-07).

### File Routing
PASS — `src/PropTraderTools/CopyEngine.cs` (Wave workspace path).

### LANE-SPLIT Alignment
PASS — T1 targets `TryEvictFollowerBeSlot` (lines 1934-1957), a distinct code region from T2 (lines 992-1010) and T3 (lines 6927-6930). Consistent with plan LANES-APPROVED gate result.

### JS-CONV-01 (Convergence Analysis)
PASS — fix targets the convergence point: the single block before the `IsFollowerAccount` early-return is the correct ancestor that covers all drain-abort paths for the leader-flat case. No common ancestor exists with T2 or T3 paths.

**VERDICT: TICKET_REVIEW_PASS**

---

## T2 — DW-B157-01: fo=NULL for Drain Entries (Stop/Target Drag Broken)

### Traceability
PASS
- Spec req ID DW-B157-01 cited in §a.
- Plan section 3.2 fully covers `TrySetSlateReadyOnDrainFill` design and call site.
- No phantom work: all items map to plan §2.2 + §3.2.
- No missing work: plan §6 "Required New Tests" are all reflected in §g.

### JS Pre-Check (JS-001 / JS-002 / JS-021)
PASS
- **JS-001** (no throw): §h SCAN-04 — 5 early returns, no throw in method body. Source lines 992-1010 confirm. PASS.
- **JS-002** (no null return): §h SCAN-05 — void return type, no return value expression. PASS.
- **JS-021** (no lock): §h SCAN-02 — `_followerSlateReady[order.Account.Name] = true` is `ConcurrentDictionary` indexer assignment (lock-free). Source line 1004 confirms. PASS.

### CYC Pre-Check
PASS
- `TrySetSlateReadyOnDrainFill` CYC = 6 (strict McCabe: base=1 + 5 ifs = 6). Well within CYC≤8.
- Call site in `OnOrderUpdate` adds 0 branches (unconditional call).
- **INFO (non-blocking, inherited from plan review INFO-1)**: Source comment at line 991 reads `// CYC<=5` (simplified decision-points-only count, omits base=1). Ticket §e correctly documents this discrepancy and confirms both values are ≤8. No gate is affected.

### NT8 Constraints
PASS
- §h SCAN-06: `order.OrderState`, `order.Name`, `order.Account`, `order.Instrument`, `OrderState.Filled`, `NinjaTrader.Code.Output.Process` — all AddOnBase-safe. No Dispatcher, no async/await, no UI-thread calls.
- Call site at line 1756 is inside `OnOrderUpdate` — synchronous void context. Source confirmed.

### Completeness (signatures, log markers, call sites)
PASS
- New method signature: `private void TrySetSlateReadyOnDrainFill(Order order)` — stated in §d. Source line 992 confirms exact match.
- Call site: `OnOrderUpdate` line 1756, after `HandleDrainTerminalState(e.Order)` at line 1748 — stated in §d and §c. Source lines 1748-1756 confirm exact placement.
- Log marker: `[SLATE-READY-DRAIN] acct=X instr=Y` — specified with emission condition (all 5 guards pass). Source line 1006 confirms.

### Test Coverage
PASS
- 6 entries in §g test table:
  1. `TrySetSlateReadyOnDrainFill_MethodExists_StructuralCheck` — [Fact] structural.
  2. `TrySetSlateReadyOnDrainFill_NullOrder_NoSideEffect` — [Fact] BCL.
  3. `TrySetSlateReadyOnDrainFill_NonFilledOrder_NoSideEffect` — [Fact] BCL.
  4. `TrySetSlateReadyOnDrainFill_NonPttCopyName_NoSideEffect` — [Fact] BCL.
  5. `TrySetSlateReadyOnDrainFill_CalledAfterHandleDrainTerminal` — [Fact] structural.
  6. `Bug2FollowerSlateReadyTests` suite (line 8503+) — [Fact] / [Fact(Skip="NT8-runtime")] mixed, correctly annotated.
- NT8-runtime tests (guards 4+5 requiring `IsFollowerAccount`) are correctly annotated `[Fact(Skip = "NT8-runtime: ...")]` with structural fallback. BCL tests (guards 1-3) run without NT8. Strategy is sound.

### Scan Checklist Presence
PASS — all 7 scans present (SCAN-01 through SCAN-07).

### File Routing
PASS — `src/PropTraderTools/CopyEngine.cs` (Wave workspace path).

### LANE-SPLIT Alignment
PASS — T2 targets a new method at lines 992-1010 and a call-site hook at line 1756, independent of T1 (lines 1934-1957) and T3 (lines 6927-6930). Consistent with plan LANES-APPROVED gate result.

### JS-CONV-01 (Convergence Analysis)
PASS — `TrySetSlateReadyOnDrainFill` called from `OnOrderUpdate` is the correct single convergence point for the drain-fill slate-restoration path. No shared ancestor with T1 or T3 paths (separate state: `_followerSlateReady` vs `_pendingDispatchDrains` vs `diagTotal`).

**VERDICT: TICKET_REVIEW_PASS**

---

## T3 — DW-B157-02: BE Retry 500ms Delay from Inflated Order Count

### Traceability
PASS
- Spec req ID DW-B157-02 cited in §a.
- Plan section 3.3 fully covers the terminal-state filter approach.
- No phantom work: all items map to plan §2.3 + §3.3.
- No missing work: plan §6 "For DW-B157-02" tests are reflected in §g.

### JS Pre-Check (JS-001 / JS-002 / JS-021)
PASS
- **JS-001** (no throw): §h SCAN-04 — `continue` + counter increment only; no throw. Source lines 6931-6934 confirm. PASS.
- **JS-002** (no null return): §h SCAN-05 — void method, no return value expression. PASS.
- **JS-021** (no lock): §h SCAN-02 — `acc.Orders` is read-only enumeration; no shared state written; no lock(). Source confirmed. PASS.

### CYC Pre-Check
PASS
- `LogDiagOrderCount` CYC = 6 (strict McCabe: base=1 + foreach=1 + if(terminal)=1 + ||Cancelled=1 + ||Rejected=1 + if(instrument)=1 = 6). Within CYC≤8.
- **INFO (non-blocking, inherited from plan review)**: Source comment at line 6923 reads `// CYC<=3` (simplified block-counting style: 3 logical gates). Ticket §e correctly documents both values are ≤8. No gate is affected.

### NT8 Constraints
PASS
- §h SCAN-06: `acc.Orders`, `o.OrderState`, `OrderState.Filled/Cancelled/Rejected`, `o.Instrument.FullName`, `NinjaTrader.Code.Output.Process` — all AddOnBase-safe. No UI-thread calls, no async/await.
- **[ObfuscationAttribute]** at source line 6925 is pre-existing (noted in plan review INFO-2). Not added or modified by this fix. No constraint violation.

### Completeness (signatures, log markers, call sites)
PASS
- Method signature: `[System.Reflection.ObfuscationAttribute(...)] private void LogDiagOrderCount(Account acc, Instrument instrument)` — correctly stated as existing and unchanged in §d. Pre-existing `[ObfuscationAttribute]` explicitly noted per INFO-2.
- Log marker: `[BE-DIAG] <account-name> orders-for-instr=<N>` — pre-existing, correctly documented as "changes the value of N" (not introducing a new marker). Source line 6939 confirms.
- No new call site required: `LogDiagOrderCount` is already called at line 6841 (existing routing).

### Test Coverage
PASS
- 2 entries in §g test table:
  1. `LogDiagOrderCount_ShouldLogCorrectCount_When...` (line 6267) — `[Fact(Skip="NT8-runtime: ...")]` (requires `acc.Orders` stub). Correct annotation.
  2. `LogDiagOrderCount_FiltersTerminalOrders_SkipsFilledCancRej` — structural: source body contains `OrderState.Filled` as terminal-filter guard. Fallback to structural inspection is sound given NT8 runtime constraint.
- Only 2 tests for a 4-line loop filter is adequate: the filter is not testable without an `Account.Orders` stub, and the structural fallback confirms presence. No additional public/internal methods introduced.

### Scan Checklist Presence
PASS — all 7 scans present (SCAN-01 through SCAN-07).

### File Routing
PASS — `src/PropTraderTools/CopyEngine.cs` (Wave workspace path).

### LANE-SPLIT Alignment
PASS — T3 targets `LogDiagOrderCount` lines 6927-6930, far from T1 (lines 1934-1957) and T2 (lines 992-1010). Consistent with plan LANES-APPROVED gate result.

### JS-CONV-01 (Convergence Analysis)
PASS — `LogDiagOrderCount` is the only caller of `acc.Orders` in the BE diagnostic path. The terminal-state filter at the foreach loop is the correct convergence point. No shared ancestor with T1 or T2 paths.

**VERDICT: TICKET_REVIEW_PASS**

---

## Cross-Ticket Summary

| Check                          | T1              | T2         | T3         |
|-------------------------------|-----------------|------------|------------|
| Spec req ID traceability       | PASS            | PASS       | PASS       |
| JS-001 (no throw)              | PASS            | PASS       | PASS       |
| JS-002 (no null return)        | PASS            | PASS       | PASS       |
| JS-021 (no lock)               | PASS            | PASS       | PASS       |
| CYC pre-check                  | ACCEPTED/DEFER-1| PASS (6)   | PASS (6)   |
| NT8 constraints                | PASS            | PASS       | PASS       |
| Method signatures              | PASS            | PASS       | PASS       |
| Log markers                    | PASS            | PASS       | PASS       |
| Call sites                     | PASS (n/a)      | PASS       | PASS (n/a) |
| Test coverage ([Fact])         | PASS (4 tests)  | PASS (6 entries) | PASS (2 tests) |
| 7-scan checklist (SCAN-01…07)  | PASS            | PASS       | PASS       |
| File routing                   | PASS            | PASS       | PASS       |
| LANE-SPLIT alignment           | PASS            | PASS       | PASS       |
| JS-CONV-01                     | PASS            | PASS       | PASS       |

**Deferred item acknowledged**: DEFER-1 — `TryEvictFollowerBeSlot` CYC ~13 decomposition.
Tracked in `docs/brain/B156/06-deferred-backlog.md`. Must be resolved in a future epic.
Does NOT constitute a JS-013 waiver going forward.

---

## Overall

**TICKET_REVIEW_PASS**

Zero rule violations. All three tickets satisfy every gate:
- Spec requirement IDs present and correctly mapped.
- 7-scan checklists complete (SCAN-01 through SCAN-07) on all three tickets.
- CYC ≤ 8 on all new/modified methods (T1 pre-existing exceedance accepted under DEFER-1
  with explicit documented annotation in both plan and ticket).
- JS-001, JS-002, JS-021 compliance explicitly stated and source-verified in all three tickets.
- No new NT8 UI-thread API calls.
- Method signatures, log markers, and call sites fully specified.
- [Fact] test names present per ticket.
- File routing to Wave workspace correct.
- LANE-SPLIT alignment consistent with plan LANES-APPROVED gate.
- JS-CONV-01 convergence analysis: all three fixes target the correct convergence points
  with no shared ancestors; independent paths confirmed.

Pipeline is cleared for Phase 4a (engineer confirmation).

---

_Review authored by ptt-ticket-reviewer (Phase 3.5). READ-ONLY — no .cs or ticket files modified._
_Input files read: 04-tickets.md, 02-architecture-plan.md, 02-plan-review.md, CopyEngine.cs (source lines 986-1010, 1748-1756, 1926-1970, 6919-6940), docs/standards/RULES_CATALOG.md._
