# Ticket Review: B62-LaneA
# Live Entry Drag Sync + Price-Keyed Dedup Fix

**Reviewer role**: ptt-ticket-reviewer (Phase 3.5)
**Date**: 2026-08-11
**Ticket under review**: `docs/brain/B62-LaneA/04-tickets.md` (B62-T1)
**Plan reviewed against**: `docs/brain/B62-LaneA/02-architecture-plan.md` (REVIEW_PASS)
**Source reads**: `CopyEngine.cs` lines 100–120, 600–665, 750–775, 860–935, 1448–1466

---

## Ticket: B62-T1 — Live entry drag sync + price-keyed dedup fix

---

### Category 1 — Traceability

**Check 1.1**: Every change traces to DW-B62-01.
- Ticket Section B explicitly maps to `DW-B62-01: Live entry drag sync`.
- All 7 changes exist in `02-architecture-plan.md` Sections 3 and 7.
- **PASS**

**Check 1.2**: All 7 changes from the spec/plan appear in the ticket.
- Plan Section 3: Change 1 (`_dedupCache` type) ↔ Ticket Change 1 — present.
- Plan Section 3: Change 2 (`IsDedup` body) ↔ Ticket Change 2 — present.
- Plan Section 3: Change 3 (call site update) ↔ Ticket Change 3 — present.
- Plan Section 3: Change 4 (`EvictDedup`) ↔ Ticket Change 4 — present.
- Plan Section 3: Change 5 (`EvictDedup` wire) ↔ Ticket Change 5 — present.
- Plan Section 3: Change 6 (`FindFollowerEntryOrder`) ↔ Ticket Change 6 — present.
- Plan Section 3: Change 7 (`HandleEntryChange` + Gate C) ↔ Ticket Change 7 — present.
- **PASS**

**Check 1.3**: No extra changes added beyond plan/spec.
- Ticket Section I (Out of Scope) explicitly excludes all items not in DW-B62-01.
- No phantom work identified.
- **PASS**

**Category 1 Verdict**: **PASS** (3/3)

---

### Category 2 — Pre-condition Verification

**Check 2.1**: `_dedupCache` field type at line 112.
- Source line 112: `private readonly ConcurrentDictionary<string, long> _dedupCache = new ConcurrentDictionary<string, long>(); // JS-025`
- Ticket pre-condition table: `ConcurrentDictionary<string, long>` — **EXACT MATCH**.
- **PASS**

**Check 2.2**: `IsDedup` signature at line 1448.
- Source line 1448: `private bool IsDedup(string orderId)` — single argument.
- Ticket pre-condition table: `private bool IsDedup(string orderId)` — **EXACT MATCH**.
- **PASS**

**Check 2.3**: Gate C absent at lines 650–660.
- Source lines 659–660: `// No bracket -- normal copy dispatch` followed by `DispatchCopy(e.Order, matchedRule.Value);`
- No `// Gate C` comment or Guard C block present.
- Ticket pre-condition: `MUST NOT EXIST (no Gate C comment)` — **CONFIRMED**.
- **PASS**

**Check 2.4**: `HandleEntryChange` absent.
- Not found in any read range; ticket pre-condition says `MUST NOT EXIST` — **CONFIRMED**.
- **PASS**

**Check 2.5**: `FindFollowerEntryOrder` absent.
- Not found in any read range; ticket pre-condition says `MUST NOT EXIST` — **CONFIRMED**.
- **PASS**

**Category 2 Verdict**: **PASS** (5/5)

---

### Category 3 — Change Correctness

**Check 3.1**: Change 1 — `_dedupCache` type `long` → `double` exact match to line 112.
- Source line 112 is `ConcurrentDictionary<string, long>`.
- Ticket Change 1 "Before" block matches exactly: `ConcurrentDictionary<string, long>`.
- Ticket Change 1 "After" block uses `ConcurrentDictionary<string, double>` — semantically correct.
- **PASS**

**Check 3.2**: Change 2 — `IsDedup` new signature includes `double limitPrice` param; body is TryAdd-only (CYC=2).
- Ticket Change 2 new signature: `private bool IsDedup(string orderId, double limitPrice)` — correct.
- Body contains only `if (!_dedupCache.TryAdd(orderId, limitPrice)) return true; return false;` — CYC=2.
- `DateTime.UtcNow.Ticks` and foreach pruning loop fully deleted — correct.
- **PASS**

**Check 3.3**: Change 3 — call site update passes `order.LimitPrice` as second arg.
- Source line 763: `if (IsDedup(order.OrderId.ToString()))` — single arg.
- Ticket Change 3 "After": `if (IsDedup(order.OrderId.ToString(), order.LimitPrice))` — correct.
- **PASS**

**Check 3.4**: Change 4 — `EvictDedup` is `internal`, guards Filled/Cancelled/Rejected, CYC=2.
- Ticket Change 4 declares `internal void EvictDedup(string orderId, OrderState state)` — `internal` access correct.
- Guard: `if (state != OrderState.Filled && state != OrderState.Cancelled && state != OrderState.Rejected) return;` — exactly three terminal states guarded.
- Followed by `_dedupCache.TryRemove(orderId, out _);` — CYC=2 (one branch + fall-through).
- **PASS**

**Check 3.5**: Change 5 — `EvictDedup` wired AFTER `TryFirePositionState` and BEFORE `// Gate 1` comment.
- Source lines 602–607 match ticket "Before" block exactly:
  ```
  TryFirePositionState(e);
  [blank line]
  // Gate 1: enabled check
  if (!_isCopyEnabled)
      return;
  ```
- Ticket Change 5 "After" inserts `EvictDedup(e.Order.OrderId.ToString(), e.Order.OrderState);` between `TryFirePositionState(e);` and `// Gate 1:` — correct placement.
- **PASS**

**Check 3.6**: Change 6 — `FindFollowerEntryOrder` returns `Order?`, matches Name+Type+State, CYC=3.
- Ticket Change 6: `private static Order? FindFollowerEntryOrder(Account follower, Instrument instrument)` — nullable return type correct.
- Loop body guards: `order.Instrument != instrument` (2), `order.OrderState == OrderState.Working && order.OrderType == OrderType.Limit && order.Name == "PTT-Copy"` (3) — CYC=3 correct.
- Insert position: after `FindFollowerBracketOrder` which ends at source line 931 — correct.
- **PASS**

**Check 3.7a**: Change 7A — `HandleEntryChange` has `try/catch`, no `lock()`, updates `_dedupCache` before looping, CYC ≤ 8.
- Ticket Section D Change 7A: `try/catch` wraps `acc.Change()` — present.
- No `lock()` anywhere in the body — confirmed.
- `_dedupCache[leaderOrder.OrderId.ToString()] = newPrice;` precedes the `foreach` loop — correct.
- CYC comment in ticket says `CYC=6`; engineer contract in Section E also says `CYC=6`; Acceptance Criteria Section H explicitly requires the CYC=6 correction from the plan reviewer note — 6 ≤ 8.
- **PASS**

**Check 3.7b**: Gate C fires only for Limit + (Accepted|Working), uses TryGetValue, price delta >= tickSize.
- Ticket Change 7B Gate C: `if (e.Order.OrderType == OrderType.Limit && (e.Order.OrderState == OrderState.Accepted || e.Order.OrderState == OrderState.Working))` — correct.
- Inner guard uses `_dedupCache.TryGetValue(e.Order.OrderId.ToString(), out double storedPrice)` — TryGetValue not TryAdd — correct.
- Delta guard: `Math.Abs(e.Order.LimitPrice - storedPrice) >= (e.Order.Instrument?.MasterInstrument?.TickSize ?? 0.01)` — >= tickSize correct.
- **PASS**

**Category 3 Verdict**: **PASS** (8/8)

---

### Category 4 — 7-Scan Checklist Presence

All 7 scans appear in Ticket Section F as a complete table with exact commands and required results.

**SCAN-01 — ASCII**
- Command: `grep -Prn "[^\x00-\x7F]" src/PropTraderTools/CopyEngine.cs`
- Present with required result and pre-existing line exemptions.
- **PASS**

**SCAN-02 — Build**
- Command: `dotnet build src/PropTraderTools/ --no-restore`
- Present with required result: `0 errors, 0 warnings`.
- **PASS**

**SCAN-03 — Tests**
- Command: `dotnet test src/PropTraderTools/ --no-build`
- Present with required result: `All pass — 5 new T_B62_xx tests + all prior`.
- **PASS**

**SCAN-04 — Lock**
- Command: `grep -n "lock(" src/PropTraderTools/CopyEngine.cs`
- Present with required result: `0 results in new B62 code`.
- **PASS**

**SCAN-05 — Complexity**
- Command: `python scripts/complexity_audit.py src/PropTraderTools/CopyEngine.cs`
- Present with required result: `All new methods ≤ 8`.
- **PASS**

**SCAN-06 — Throw**
- Command: `grep -n "throw new" src/PropTraderTools/CopyEngine.cs`
- Present with required result: `0 new throw new in B62 changes`.
- **PASS**

**SCAN-07 — Null return**
- Specification: Manual review of `FindFollowerEntryOrder` return type and all call sites.
- Present with required result: `Return type is Order?; all callers null-guard with if (fo == null) continue;`.
- **PASS**

**Category 4 Verdict**: **PASS** (7/7 — all scans present)

---

### Category 5 — Test Coverage

All tests in Ticket Section G.

**Check 5.1**: T_B62_01 — `IsDedup_FirstCall_ReturnsFalse`
- Framework: `[Fact]` — present.
- Arrange: `orderId = "ord-001"`, `limitPrice = 7751.0` — present.
- Act: invoke `IsDedup("ord-001", 7751.0)` via reflection — present.
- Assert: `Returns false` — present. Side-effect note `(_dedupCache now contains ...)` present.
- **PASS**

**Check 5.2**: T_B62_02 — `IsDedup_SecondCallSamePrice_ReturnsTrue`
- Framework: `[Fact]` — present.
- Arrange: seed cache with first call — present.
- Act: second call with same args — present.
- Assert: `Returns true` — present.
- **PASS**

**Check 5.3**: T_B62_03 — `EvictDedup_FilledState_RemovesEntry`
- Framework: `[Fact]` — present.
- Arrange: seed cache; verify seed returns false — present.
- Act: `engine.EvictDedup("ord-003", OrderState.Filled)` — present.
- Assert: `IsDedup("ord-003", 7751.0) returns false` (entry removed) — present.
- **PASS**

**Check 5.4**: T_B62_04 — `EvictDedup_WorkingState_DoesNotRemove`
- Framework: `[Fact]` — present.
- Arrange: seed cache — present.
- Act: `engine.EvictDedup("ord-004", OrderState.Working)` — present.
- Assert: `IsDedup("ord-004", 7751.0) returns true` (entry still present) — present.
- **PASS**

**Check 5.5**: T_B62_05 — `EvictDedup_CancelledState_RemovesEntry`
- Framework: `[Fact]` — present.
- Arrange: seed cache — present.
- Act: `engine.EvictDedup("ord-005", OrderState.Cancelled)` — present.
- Assert: `IsDedup("ord-005", 7751.0) returns false` (entry removed) — present.
- **PASS**

**Check 5.6**: All tests use `[Fact]` — xUnit only, no NUnit or MSTest markers anywhere.
- Section G header: `Framework: xUnit [Fact] ONLY. No NUnit. No MSTest.`
- All 5 tests annotated with `[Fact]` block header.
- **PASS**

**Category 5 Verdict**: **PASS** (6/6)

---

### Category 6 — NT8 Constraints

**Check 6.1**: `acc.Change(new Order[] { fo })` pattern matches line 871 usage.
- Source line 871: `acc.Change(new Order[] { fo });` — exact pattern confirmed.
- Ticket `HandleEntryChange` body: `acc.Change(new Order[] { fo });` — **EXACT MATCH**.
- **PASS**

**Check 6.2**: `FindFollowerEntryOrder` matches by `Instrument` object reference (not string).
- Ticket Change 6 guard: `if (order.Instrument != instrument) continue;` — reference equality (not `FullName` string comparison).
- Ticket notes: "Instrument comparison is object-reference equality (NT8 `Instrument` object identity)."
- **PASS**

**Check 6.3**: Gate C is inserted AFTER Gate B (bracket check) and BEFORE `DispatchCopy` call.
- Ticket Change 7B "After" block: Gate B block preserved intact, Gate C block inserted after Gate B's closing brace (`return;`), `DispatchCopy` call placed after Gate C block.
- Source lines 650–660 confirm current order: Gate B → DispatchCopy (no Gate C). Ticket inserts Gate C between them.
- **PASS**

**Check 6.4**: `EvictDedup` wired at pre-gate level (not inside Gate 1/2/B/C).
- Ticket Change 5 inserts `EvictDedup(...)` after `TryFirePositionState(e)` and before `// Gate 1: enabled check`.
- Source lines 602–607 confirm this is the pre-gate block, outside all conditional guards.
- **PASS**

**Category 6 Verdict**: **PASS** (4/4)

---

### Category 7 — JS Rule Compliance

**Check 7.1 (JS-021)**: No `lock()` in any new method.
- `IsDedup` body: no `lock()`.
- `EvictDedup` body: no `lock()`.
- `FindFollowerEntryOrder` body: no `lock()`.
- `HandleEntryChange` body: no `lock()`.
- Gate C inline code: no `lock()`.
- All new methods annotated `// JS-021: no lock` or `// JS-025: ConcurrentDictionary is lock-free`.
- **PASS**

**Check 7.2 (JS-001)**: `HandleEntryChange` wraps `acc.Change()` in `try/catch`, no `throw new`.
- `HandleEntryChange` body has `try { fo.LimitPrice = newPrice; acc.Change(...); ... } catch (Exception ex) { StatusUpdate?.Invoke(...); }` — exception absorbed, not rethrown.
- No `throw new` anywhere in any new B62 method body.
- **PASS**

**Check 7.3 (JS-002)**: `FindFollowerEntryOrder` returns `Order?`, callers null-guard `fo`.
- Ticket Change 6: return type `Order?` — declared nullable.
- `HandleEntryChange` call site: `var fo = FindFollowerEntryOrder(acc, instrument); if (fo == null) continue;` — null-guard present.
- Ticket notes: "Returns null when not found — callers in HandleEntryChange null-guard with if (fo == null) continue;."
- **PASS**

**Check 7.4 (CYC ≤ 8)**: All new methods within limit.
- `IsDedup`: CYC=2 — ≤ 8 ✓
- `EvictDedup`: CYC=2 — ≤ 8 ✓
- `FindFollowerEntryOrder`: CYC=3 — ≤ 8 ✓
- `HandleEntryChange`: CYC=6 (corrected per reviewer note, Section H) — ≤ 8 ✓
- Gate C inline: two nested `if` blocks = CYC=2 in context of `OnOrderUpdate` — does not push any method above 8.
- **PASS**

**Check 7.5 (ASCII-only)**: All new string literals are ASCII.
- New string literals: `"PTT-Copy"`, `": entry dragged -> "`, `": entry drag error: "`, `"B62: ..."` comments, `"ord-001"` through `"ord-005"` in tests.
- Arrow notation uses ASCII `->` (hyphen 0x2D + greater-than 0x3E), not Unicode arrow character.
- Pre-existing non-ASCII at lines 395, 496, 1256, 1257 explicitly exempt per SCAN-01 note.
- **PASS**

**Check 7.6 (xUnit-only)**: All 5 tests use `[Fact]`.
- Confirmed in Category 5 Check 5.6.
- **PASS**

**Category 7 Verdict**: **PASS** (6/6)

---

### File Routing

**Check FR.1**: C# source path.
- Ticket Section A: `src/PropTraderTools/CopyEngine.cs` — Wave workspace path ✓
- Ticket Section A: `src/PropTraderTools/Tests/B62Tests.cs` — Wave workspace path ✓
- No Director workspace path (`c:\WSGTA\universal-or-strategy-director\`) referenced for any `.cs` file.
- **PASS**

**File Routing Verdict**: **PASS**

---

## Engineer Guidance Notes

These notes are informational. They do NOT constitute failures. Engineer must apply them during implementation.

**NOTE-1 (CYC label correction — mandatory)**
The architecture plan (`02-architecture-plan.md` Section 7, Change 7A) contains a mis-numbered CYC annotation (`CYC=5`) and out-of-sequence branch labels (`(4)`, `(5)`, `(3)` in the plan). The ticket explicitly corrects this with the plan-reviewer note in Section D Change 7A and Section H Acceptance Criteria. Engineer MUST:
- Set `HandleEntryChange` CYC comment to `CYC=6` (not 5).
- Number branch labels `(1)` through `(6)` in sequential code-flow order as specified in Section H.
The ticket body already shows the corrected labels and CYC=6 — implement exactly as shown in the ticket, not the plan.

**NOTE-2 (`InternalsVisibleTo` dependency)**
Test access to `EvictDedup` (internal) requires `[assembly: InternalsVisibleTo("PropTraderTools.Tests")]`. The ticket notes this is "already configured" from prior blocks. Engineer must verify this attribute exists in the production assembly before writing T_B62_03/04/05. If absent, add it — it is not a new test infrastructure change but a prerequisite.

**NOTE-3 (Market order `LimitPrice = 0.0`)**
Gate C guards `OrderType.Limit`, so market orders with `LimitPrice = 0.0` stored in `_dedupCache` never trigger `HandleEntryChange`. This is correct and safe by design. No market-order test is required.

**NOTE-4 (Dependency implementation order)**
Section H Acceptance Criteria specifies implement in order 1 → 2 → 3 → 4 → 5 → 6 → 7. This is a hard dependency chain: Change 1 must precede Change 2 (type compatibility); Change 2 must precede Change 3 (signature); Change 4 must precede Change 5 (method existence); Change 6 must precede Change 7A (method existence). Do not reorder.

**NOTE-5 (SCAN-01 pre-existing exemptions)**
SCAN-01 requires zero NEW non-ASCII occurrences. Pre-existing non-ASCII at `CopyEngine.cs` lines 395, 496, 1256, 1257 are exempt and must not be touched. Record these pre-existing lines as exempt in `ticket-1-completion.md`.

---

## Summary

| Category | Checks | Pass | Fail |
|----------|--------|------|------|
| 1 — Traceability | 3 | 3 | 0 |
| 2 — Pre-condition Verification | 5 | 5 | 0 |
| 3 — Change Correctness | 8 | 8 | 0 |
| 4 — 7-Scan Checklist Presence | 7 | 7 | 0 |
| 5 — Test Coverage | 6 | 6 | 0 |
| 6 — NT8 Constraints | 4 | 4 | 0 |
| 7 — JS Rule Compliance | 6 | 6 | 0 |
| File Routing | 1 | 1 | 0 |
| **TOTAL** | **40** | **40** | **0** |

---

## Overall Verdict

TICKET_REVIEW_PASS
