# Ticket Review: PTT-DEFERRED-0911 Lane F

**Phase:** 3.5 (Ticket Review)
**Reviewer:** ptt-ticket-reviewer
**Date:** 2026-09-11
**Source tickets:** `docs/brain/PTT-DEFERRED-0911/04-tickets-lane-f.md`
**Source plan:** `docs/brain/PTT-DEFERRED-0911/02-architecture-plan-lane-f.md` (REVIEW_PASS, Revision 1)
**Rules reference:** `docs/standards/RULES_CATALOG.md`

---

## T1 — DW-BUG-0911-A-02: Remove [Fact(Skip=...)] via FlatGuardLoop delegate type change

**Spec Requirement IDs:** DW-BUG-0911-A-02 (§T1.0)

### Traceability
- T1.0 cites DW-BUG-0911-A-02. Present in plan §0 and §12 deferred items table. ✓
- Scope matches plan §3.1–3.3 exactly: FlatGuardLoop signature change, 2 call-site lambda updates, test unskip. ✓
- T1.1 files touched match plan §3.2. No phantom work. No plan item left uncovered by T1. ✓

**Traceability: PASS**

### JS Pre-Check
- **JS-021 (P0):** T1.6 states "Zero new `lock()` calls." `FlatGuardLoop` is static, pure control flow. SCAN-01 required → 0. No `lock()` described. PASS. ✓
- **JS-001 (P0):** T1.6 states "`FlatGuardLoop` has no `throw`." No `throw` added. PASS. ✓
- **JS-013 (P1):** `FlatGuardLoop<TAccount>` revised: CYC=3 stated in T1.6, AFTER code comment, and SCAN-02. Both Flatten call-site lambdas: CYC=1 each (pure expression bodies), stated in SCAN-02. All ≤ 8. PASS. ✓
- **ASCII-001 (P0):** SCAN-04 required → 0 non-ASCII bytes. No new non-ASCII identifiers or literals described. PASS. ✓

**JS Pre-Check: PASS**

### CYC Pre-Check
- `FlatGuardLoop<TAccount>` (modified): CYC=3 (base=1 + foreach=1 + isFlat branch=1) — stated at T1.6, T1.7/SCAN-02, and in AFTER code comment. ≤ 8. ✓
- Both Flatten call-site lambdas: pure expression bodies, CYC=1 each. ✓
- No other new or modified methods with decision points.

**CYC Pre-Check: PASS**

### NT8 Check
- AFTER test body uses `Func<object, bool> isFlat = acc => true;` — pure BCL, zero NT8 type reference. ✓
- Reflection invocation passes `isFlat` as second element; no NT8 type instantiated. ✓
- T1.4 comment explicitly confirms: "no NinjaTrader.Cbi.Position reference." ✓
- Production call-site lambdas reference `NinjaTrader.Cbi.Position.Quantity` — acceptable; they execute inside NT8 process only, not in xUnit host. ✓
- No `async/await` in lifecycle methods. No `Account.All` outside Loaded. No `sealed` on `TradeCopierWindow`. No `FontFamily`. No `DateTime.Now`. No `CreateOrder` with non-"PTT-" prefix. ✓

**NT8 Check: PASS**

### Test Coverage
- New method `FlatGuardLoop<TAccount>` (modified): covered by `[Fact] public void Flatten_Instrument_FlatFollower_SkipsDispatch_FlatGuard()` via reflection (T1.4). ✓
- `[Fact]` attribute present with NO `Skip` argument (§T1.4 AFTER code block). ✓
- Test method name matches spec exactly. ✓
- No new public or internal production methods introduced in T1 that lack a test. ✓

**Test Coverage: PASS**

### Scan Checklist
All 7 scans present in §T1.7:
- **SCAN-01:** ✓ (Select-String lock pattern on CopyEngine.cs → 0)
- **SCAN-02:** ✓ (Manual McCabe: FlatGuardLoop CYC=3, lambdas CYC=1)
- **SCAN-03:** ✓ (Select-String async/await on CopyEngine.cs → 0)
- **SCAN-04:** ✓ (PowerShell byte scan on CopyEngine.cs → 0)
- **SCAN-05:** ✓ (Select-String AtmStrategyCreate|AtmStrategyChangeStopTarget → 0)
- **SCAN-06:** ✓ (dotnet build + dotnet test; target test transitions SKIP→PASS)
- **SCAN-07:** ✓ (5 structural items: signature, 2 call sites, [Fact] no Skip, isFlat type, Invoke args)

**Scan Checklist: PASS**

### File Routing
- `src/PropTraderTools/CopyEngine.cs` — Wave workspace. ✓
- `src/PropTraderTools/CopyEngineTests.cs` — Wave workspace. ✓
- No Director workspace `.cs` paths. ✓

**File Routing: PASS**

### VERDICT: TICKET_REVIEW_PASS

---

## T2 — DW-B24-03: New [Fact] for acc==leader skip guard

**Spec Requirement IDs:** DW-B24-03 (§T2.0)

### Traceability
- T2.0 cites DW-B24-03. Present in plan §0 and §12 deferred items table. ✓
- Scope matches plan §4.1–4.3 exactly: additive test class `LaneFTests`, zero production change. ✓
- T2.1 explicitly states "None" for CopyEngine.cs changes. Matches plan §4.2 "None." ✓
- Plan V-01 fix (`GetAnyOverload` via `GetMethods`) present in T2.2 and T2.3. ✓
- No phantom work. No plan item left uncovered. ✓

**Traceability: PASS**

### JS Pre-Check
- **JS-021 (P0):** T2.4 states "Zero `lock()` calls." `LaneFTests` uses `List<object>`, no shared state. SCAN-01 required → 0. PASS. ✓
- **JS-001 (P0):** T2.4 notes "Test is not a dispatch path. `Assert` throws are xUnit contract, not PTT dispatch." N/A. PASS. ✓
- **JS-013 (P1):** `GetAnyOverload`: CYC=1 (expression body, no branches). `SkipDuplicateGuard_*`: CYC≈4 (foreach+if+Assert calls) — T2.4 documents "test methods are informational for CYC; not subject to JS-013 gates." Consistent with LaneD/LaneE precedent per plan. ✓
- **ASCII-001 (P0):** SCAN-04 required → 0. No new non-ASCII identifiers or literals described. PASS. ✓

**JS Pre-Check: PASS**

### CYC Pre-Check
- `GetAnyOverload`: CYC=1 (pure expression body). ✓
- `SkipDuplicateGuard_LeaderAccountInFollowerFanOut_SkipsDispatchForLeader`: CYC≈4 — informational; test methods exempt from JS-013 gate per T2.4. ✓
- No production code added or modified in T2. ✓

**CYC Pre-Check: PASS**

### NT8 Check
- `LaneFTests` uses `new object()` tokens — pure BCL. ✓
- `GetAnyOverload` uses `typeof(CopyEngine).GetMethods(...)` — pure reflection, no NT8 type instantiation. ✓
- Part B: `GetAnyOverload("Trim")`, `GetAnyOverload("Flatten")`, `GetAnyOverload("CancelPendingEntries")` — only `Assert.NotNull` existence check; methods are NOT invoked; no NT8 types instantiated at runtime. ✓
- T2.3 states "No Skip, no NT8 runtime types instantiated." ✓
- Zero production code changes; no new NT8 lifecycle/handler methods introduced. ✓

**NT8 Check: PASS**

### Test Coverage
- New method `SkipDuplicateGuard_LeaderAccountInFollowerFanOut_SkipsDispatchForLeader`: `[Fact]` present with NO `Skip` argument (§T2.2 code block). ✓
- Test method name matches spec exactly. ✓
- `GetAnyOverload(string name)`: private static helper; no independent [Fact] required. Used inside the [Fact] test. ✓
- No new production methods in T2 requiring test coverage. ✓

**Test Coverage: PASS**

### Scan Checklist
All 7 scans present in §T2.5:
- **SCAN-01:** ✓ (Select-String lock pattern on CopyEngineTests.cs → 0)
- **SCAN-02:** ✓ (Manual McCabe: GetAnyOverload CYC=1, SkipDuplicateGuard CYC≈4 informational)
- **SCAN-03:** ✓ (Select-String async/await on CopyEngineTests.cs → 0)
- **SCAN-04:** ✓ (PowerShell byte scan on CopyEngineTests.cs → 0)
- **SCAN-05:** ✓ (Select-String AtmStrategyCreate|AtmStrategyChangeStopTarget on CopyEngineTests.cs → 0)
- **SCAN-06:** ✓ (dotnet build + dotnet test; target test is PASS, not SKIP, not ERROR)
- **SCAN-07:** ✓ (4 structural items: LaneFTests after LaneETests, GetAnyOverload uses GetMethods, [Fact] no Skip, Part B assertions)

**Scan Checklist: PASS**

### File Routing
- `src/PropTraderTools/CopyEngineTests.cs` — Wave workspace. ✓
- No CopyEngine.cs edits (zero production changes). ✓
- No Director workspace `.cs` paths. ✓

**File Routing: PASS**

### VERDICT: TICKET_REVIEW_PASS

---

## T3 — DW-LD-04: CancelQxBrackets CYC=9 fix via IsRaceSkipCandidate extraction

**Spec Requirement IDs:** DW-LD-04 (§T3.0)

### Traceability
- T3.0 cites DW-LD-04. Present in plan §0 and §12 deferred items table. ✓
- Scope matches plan §5.2–5.4 exactly: IsRaceSkipCandidate extraction, CancelQxBrackets body update, CYC comment update, test. ✓
- T3.1 files touched match plan §5.3–5.4. ✓
- Plan §5.1 CYC=9 violation confirmed; T3.0 and T3.3C recount verify resolution. ✓
- No phantom work. No plan item left uncovered. ✓

**Traceability: PASS**

### JS Pre-Check
- **JS-021 (P0):** T3.6 states "Zero new `lock()` calls." `IsRaceSkipCandidate` is static, pure predicate, no shared state. SCAN-01 required → 0. PASS. ✓
- **JS-001 (P0):** T3.6 states "`IsRaceSkipCandidate` returns `bool`, no `throw`." PASS. ✓
- **JS-002 (P0):** T3.6 states "Returns `bool`, not a reference type → N/A." PASS. ✓
- **JS-013 (P1):** `IsRaceSkipCandidate` new: CYC=4. `CancelQxBrackets` 3-param modified: CYC=7. Both stated in T3.6, T3.7/SCAN-02, code comment. Both ≤ 8. PASS. ✓
- **ASCII-001 (P0):** SCAN-04 required → 0. `IsRaceSkipCandidate` method name and all new identifiers/comments are ASCII. PASS. ✓

**JS Pre-Check: PASS**

### CYC Pre-Check
- `IsRaceSkipCandidate` (new): CYC=4 (base=1 + snapshot!=null=1 + !Contains=1 + IsQxCancelEligible3=1). Stated at T3.6, T3.7/SCAN-02, and AFTER code comment. ≤ 8. ✓
- `CancelQxBrackets` 3-param (modified): CYC=7 post-extraction. Full manual recount provided at T3.3C (base=1, ||=2, foreach=1, IsRaceSkipCandidate=1, !IsQxCancelEligible3=1, stale.Count=1 → Total=7). Stated at T3.6, T3.7/SCAN-02, updated CYC comment. ≤ 8. ✓
- Architect's independent count (plan §5.1) = CYC=9 pre-extraction; post-extraction CYC=7 consistent. ✓

**CYC Pre-Check: PASS**

### NT8 Check
- `IsRaceSkipCandidate` production code uses `NinjaTrader.Cbi.Order`, `HashSet<NinjaTrader.Cbi.Order>`, `NinjaTrader.Cbi.Instrument` — acceptable; private production code executing inside NT8 process only. ✓
- Test Part A: `GetStaticMethod("IsRaceSkipCandidate")` — pure reflection on `MethodInfo`; checks `ReturnType` and `GetParameters().Length` — BCL operations, no NT8 type instantiation. ✓
- Test Part B: `mi.Invoke(null, new object[] { null, null, null })` — passes `null` for all 3 params. First `&&` operand is `snapshot != null`; when `snapshot` = null, short-circuit returns `false` immediately. `o` and `instr` are never dereferenced. Zero NT8 type members accessed at runtime. ✓
- T3.4 comment explicitly confirms: "No NT8 Order or Instrument instantiation needed for this path." ✓
- No `async/await` in new methods. No `Account.All` outside Loaded. No new NT8 lifecycle methods. ✓

**NT8 Check: PASS**

### Test Coverage
- New production method `IsRaceSkipCandidate`: covered by `[Fact] public void IsRaceSkipCandidate_NullSnapshot_ReturnsFalse()` (T3.4). ✓
- `[Fact]` attribute present with NO `Skip` argument (§T3.4 code block). ✓
- Test method name matches spec exactly. ✓
- `GetStaticMethod(string name)`: private static test helper; no independent [Fact] required. ✓
- Modified `CancelQxBrackets` 3-param (body-only change, no signature change): behavioral equivalence is a refactor with no semantic delta; extracted predicate is directly tested by T-LF-02. ✓

**Test Coverage: PASS**

### Scan Checklist
All 7 scans present in §T3.7:
- **SCAN-01:** ✓ (Select-String lock pattern on CopyEngine.cs → 0)
- **SCAN-02:** ✓ (Manual McCabe: IsRaceSkipCandidate CYC=4, CancelQxBrackets CYC=7; both ≤ 8)
- **SCAN-03:** ✓ (Select-String async/await on CopyEngine.cs → 0)
- **SCAN-04:** ✓ (PowerShell byte scan on CopyEngine.cs → 0)
- **SCAN-05:** ✓ (Select-String AtmStrategyCreate|AtmStrategyChangeStopTarget → 0)
- **SCAN-06:** ✓ (dotnet build + dotnet test; target test is PASS, not SKIP, not ERROR; no regressions)
- **SCAN-07:** ✓ (4 structural items: IsRaceSkipCandidate at ~1200, compound if replaced, CYC=7 comment, LaneFTests closing brace)

**Scan Checklist: PASS**

### File Routing
- `src/PropTraderTools/CopyEngine.cs` — Wave workspace. ✓
- `src/PropTraderTools/CopyEngineTests.cs` — Wave workspace. ✓
- No Director workspace `.cs` paths. ✓

**File Routing: PASS**

### VERDICT: TICKET_REVIEW_PASS

---

## Overall: TICKET_REVIEW_PASS

All three tickets pass all checks.

| Ticket | Traceability | JS Pre-Check | CYC Pre-Check | NT8 Check | Test Coverage | Scan Checklist | File Routing | Verdict |
|--------|-------------|--------------|---------------|-----------|---------------|----------------|--------------|---------|
| T1 (DW-BUG-0911-A-02) | PASS | PASS | PASS | PASS | PASS | PASS | PASS | TICKET_REVIEW_PASS |
| T2 (DW-B24-03) | PASS | PASS | PASS | PASS | PASS | PASS | PASS | TICKET_REVIEW_PASS |
| T3 (DW-LD-04) | PASS | PASS | PASS | PASS | PASS | PASS | PASS | TICKET_REVIEW_PASS |

**Violations found:** None.

**Gate: TICKET_REVIEW_PASS.** Safe to spawn ptt-engineer for Phase 4a.

---

*ptt-ticket-reviewer · PTT-DEFERRED-0911 Lane F · Phase 3.5 · 2026-09-11*
