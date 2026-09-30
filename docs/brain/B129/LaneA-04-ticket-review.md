# B129 LaneA Ticket Review — DW-B135

**Block**: B129 LaneA
**Reviewer**: ptt-ticket-reviewer
**Phase**: 3.5
**Date**: 2026-08-31
**Ticket Reviewed**: `docs/brain/B129/LaneA-04-tickets.md`
**Plan Reviewed**: `docs/brain/B129/LaneA-02-architecture-plan.md`
**Plan Review Confirmed**: `docs/brain/B129/LaneA-02-plan-review.md` — REVIEW_PASS (R-01..R-10 all PASS)

---

## T-1 — Clear _lastLeaderDirection on Leader Flat

### TR-01 — Traceability

| Ticket Section | Required Plan Reference | Present | Notes |
|----------------|------------------------|---------|-------|
| T-01 requirements table | plan Section A (root cause DW-B135) | YES | T-01 maps DW-B135 defect to plan Section A root cause analysis |
| T-01 non-regression contract | plan Section B/F (DW-B128 preservation) | YES | T-01 explicitly states non-regression contract referencing plan Section B |
| T-02 files to edit | plan Component Summary / Section B | YES | T-02 lists CopyEngine.cs + B129Tests.cs matching plan Component Summary table exactly |
| T-03 insertion point | plan Section B (L2382-2383 CAS guard) | YES | T-03 cites "after `if (prior == newVal) return;` approx L2383" matching plan Section B precisely |
| T-03 code block | plan Section B pseudocode | YES | Ticket code block matches plan Section B pseudocode verbatim |
| T-04 tests | plan Section G (test contracts) | YES | All 3 test names, setups, and asserts match plan Section G verbatim |
| T-05 7-scan checklist | plan Pre-flight checklist (SCAN-01..07) | YES | SCAN-01..SCAN-07 all present in T-05 |
| T-06 CYC count | plan Section D (CYC=6) | YES | T-06 table matches plan Section D branch-count table exactly |
| T-07 ASCII check | AGENTS.md Section 2 ASCII mandate | YES | No phantom work; ASCII mandate correctly cited |

**Traceability: PASS**
No phantom work detected. No plan section without a corresponding ticket section.

---

### TR-02 — Insertion Point Accuracy

**Verification source**: `src/PropTraderTools/CopyEngine.cs` L2361-2387 (read directly).

| Claim in Ticket | Actual Code | Match |
|----------------|-------------|-------|
| Method: `TryFirePositionState` | L2361: `private void TryFirePositionState(OrderEventArgs e)` | EXACT |
| Approx location: L2361-L2387 | Method spans L2361-L2387 | EXACT |
| CAS guard ends: `if (prior == newVal) return;` approx L2383 | L2382: `if (prior == newVal)`, L2383: `return;` | EXACT |
| Insertion BEFORE: `bool hasEntries = ...` | L2385: `bool hasEntries = HasWorkingEntries(...)` | EXACT |
| Variable `instr` in scope | L2371: `string instr = e.Order.Instrument.FullName;` — confirmed in scope at L2383 | CONFIRMED |

**Insertion Point Accuracy: PASS**

---

### TR-03 — Code Block Completeness

| Required Element | Present in T-03 | Notes |
|-----------------|-----------------|-------|
| `if (!hasPos)` guard | YES | T-03 code block line 56 |
| `foreach (var r in _rules)` | YES | T-03 code block line 59 |
| `e.Order.Account.Name == r.MasterAccount?.Name` predicate (no lock) | YES | T-03 code block line 61 |
| `_lastLeaderDirection.TryRemove(instr, out _)` | YES | T-03 code block line 68 |
| Comment citing DW-B135 | YES | T-03 comment: `// DW-B135: clear direction key when leader position goes flat.` |
| Comment citing DW-B128 preservation | YES | T-03 comment: `// DW-B128 preserved: during race window, hasPos=True, so this path not taken.` |

**Code Block Completeness: PASS**

---

### TR-04 — Test Completeness

| Test | Name Present | Assert(s) Present | Concrete (not vague) |
|------|-------------|-------------------|----------------------|
| Test 1 | `B129_DW135_GuardClearedAfterLeaderFlat` | `Assert.False(engine.HasLeaderDirection("ES 09-26"))` + `Assert.False(engine.TestOnly_LastLeaderDirection.TryGetValue("ES 09-26", out _))` | YES |
| Test 2 | `B129_DW135_DW128ProtectionPreservedDuringRaceWindow` | `Assert.True(CopyEngine.IsReversalToFlatFollower(OrderAction.Sell, OrderAction.Buy, followerIsFlat: true))` | YES |
| Test 3 | `B129_DW135_FirstEntryAfterRestartNotBlocked` | `Assert.False(engine.HasLeaderDirection("ES 09-26"))` | YES |

All 3 tests are `[Fact]` xUnit. No NUnit, no MSTest.
Framework mandate: xUnit-only (AGENTS.md Section 2, Test Framework Mandate V12.32). COMPLIANT.

**Test Completeness: PASS**

---

### TR-05 — 7-Scan Checklist Presence

| Scan | Present in T-05 | Exact Command | Expected Output | Pass Criterion |
|------|----------------|---------------|-----------------|----------------|
| SCAN-01 (JS-021 lock) | YES | `grep -n "lock(" src/PropTraderTools/CopyEngine.cs` | 0 new hits | Zero `lock(` in new/modified code |
| SCAN-02 (JS-033 async void) | YES | `grep -n "async void " src/PropTraderTools/CopyEngine.cs` | 0 new hits | Zero `async void` in new code |
| SCAN-03 (JS-002 return null) | YES | `grep -n "return null;" src/PropTraderTools/CopyEngine.cs` | 0 new hits | Zero `return null;` in new code |
| SCAN-04 (JS-001 throw new) | YES | `grep -n "throw new " src/PropTraderTools/CopyEngine.cs` | 0 new hits | Zero `throw new` in new code |
| SCAN-05 (_lastLeaderDirection refs) | YES | `grep -n "_lastLeaderDirection" src/PropTraderTools/CopyEngine.cs` | Min 4 total refs | Count matches lines added |
| SCAN-06 (LaneB range overlap) | YES | Manual check: TryFirePositionState >= L2300 | First line >= L2300 | No overlap with LaneB range (~L2160 end) |
| SCAN-07 (build + test gate) | YES | `dotnet build` + `dotnet test --filter B129` | 0 errors, 6/6 tests green | Build succeeded, all B129 tests pass |

Defense-in-depth Layer 1 contract: COMPLETE. All 7 scans present with exact commands, expected outputs, and pass criteria.

**Scan Checklist Presence: PASS**

---

### TR-06 — CYC Pre-Check

| Item | Ticket Claim | Plan Section D | Match |
|------|-------------|---------------|-------|
| CYC BEFORE | 3 (state filter, null guard, Interlocked CAS) | 3 — same three branches at L2365, L2368, L2382 | EXACT |
| New branches | 3: `if (!hasPos)`, `foreach`, `if (isLeaderAcct)` | 3: hasPos guard, foreach loop, leader check | EXACT |
| CYC AFTER | 6 | 5 or 6 depending on counting convention; both <= 8 | CONSISTENT |
| JS-080 compliance | CYC=6 <= 8 COMPLIANT | COMPLIANT — no extraction required | CONFIRMED |

Note: JS-080 is cited in both ticket and plan but is not defined in the current RULES_CATALOG.md (catalog ends at JS-041). This is a known catalog incompleteness. The CYC <= 8 threshold is a project mandate enforced by AGENTS.md Section 2, Section 3.5, and CODACY configuration. The citation is an architectural cross-reference, not a fabrication.

**CYC Pre-Check: PASS**

---

### TR-07 — NT8 Constraint Check

| Check | Result |
|-------|--------|
| New NT8 API calls in inserted block | NONE — `TryRemove` is BCL (ConcurrentDictionary), `foreach` is standard C# |
| NT8 lifecycle method async/await in new code | NONE — `TryFirePositionState` is synchronous `void` |
| `sealed` on `TradeCopierWindow` | N/A — ticket does not touch TradeCopierWindow |
| `FontFamily` set on WPF element | N/A — no UI code in this fix |
| Hardcoded hex color | N/A — no UI code in this fix |
| `CreateOrder` with non-PTT- prefix | N/A — no order creation in this fix |
| `DateTime.Now` | N/A — no time operations |
| `Account.All` outside Loaded handler | N/A — no Account.All usage |

**NT8 Constraint Check: PASS**

---

### TR-08 — P0 Rule Scan

| P0 Rule | Scan Present | Coverage |
|---------|-------------|----------|
| JS-021 No `lock()` | SCAN-01 | Grep over CopyEngine.cs — covers new code |
| JS-033 No `async void` | SCAN-02 | Grep over CopyEngine.cs — covers new code |
| JS-002 No `return null;` | SCAN-03 | Grep over CopyEngine.cs — covers new code (`void` method, no return path) |
| JS-001 No `throw new` | SCAN-04 | Grep over CopyEngine.cs — covers new code |

No P0 violations described in the ticket. The inserted code block uses only:
- `ConcurrentDictionary.TryRemove` (lock-free, JS-021 compliant)
- `bool` flag + `foreach` + early `break` (no exceptions, JS-001 compliant)
- Method returns `void` (no null return, JS-002 compliant)
- No async keyword (JS-033 compliant)

**P0 Rule Scan: PASS**

---

### TR-09 — File Scope

| File | Action | Path | Workspace |
|------|--------|------|-----------|
| `src/PropTraderTools/CopyEngine.cs` | EDIT | `c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs` | Wave workspace CORRECT |
| `src/PropTraderTools/Tests/B129Tests.cs` | APPEND | `c:\WSGTA\universal-or-strategy\src\PropTraderTools\Tests\B129Tests.cs` | Wave workspace CORRECT |

T-02 explicitly lists files NOT to touch: `TradeCopierWindow.cs`, `TradeCopierPanel.cs`, `PttCopier.cs`, `CopyEngineTests.cs`, `B76Tests.cs`, all other `.cs` files.

No Director workspace paths (c:\WSGTA\universal-or-strategy-director\) for .cs files. File routing is correct.

**File Routing: PASS**

---

### TR-10 — Internal Accessor Requirement

| Required Accessor | Present in Ticket | Location |
|-------------------|------------------|----------|
| `internal ConcurrentDictionary<string, OrderAction> TestOnly_LastLeaderDirection => _lastLeaderDirection;` | YES | T-04 Test 1 section (lines 135-139): "The accessor `engine.TestOnly_LastLeaderDirection` exposes `_lastLeaderDirection` directly and may be added alongside the other test shims" with exact code block |
| `internal void TryFirePositionState_ForTest(OrderEventArgs e)` | YES | T-03 Section 3.2 |
| `internal bool HasLeaderDirection(string instrFullName)` | YES | T-03 Section 3.2 |
| `internal void SetLeaderDirection_ForTest(string instrFullName, OrderAction action)` | YES | T-03 Section 3.2 |
| `[assembly: InternalsVisibleTo("PropTraderTools.Tests")]` pre-check | YES | T-03 Section 3.2 requires engineer to confirm presence and add only if absent |

All 4 accessors (3 shims + `TestOnly_LastLeaderDirection`) are specified. The `TestOnly_LastLeaderDirection` accessor is required by Test 1's secondary assert and is explicitly provided in T-04 with exact code.

**Internal Accessor Requirement: PASS**

---

## Summary Table

| Check | Result | Notes |
|-------|--------|-------|
| TR-01 Traceability | **PASS** | All ticket sections trace to plan sections; no phantom work |
| TR-02 Insertion Point Accuracy | **PASS** | L2383 confirmed as Interlocked CAS return; `bool hasEntries` at L2385 confirmed as next statement |
| TR-03 Code Block Completeness | **PASS** | All 5 required elements present in verbatim code block |
| TR-04 Test Completeness | **PASS** | All 3 named [Fact] tests with concrete Assert statements |
| TR-05 7-Scan Checklist Presence | **PASS** | All 7 scans with exact commands, expected outputs, pass criteria |
| TR-06 CYC Pre-Check | **PASS** | CYC=6 stated, count explained (3+3), <= 8 confirmed |
| TR-07 NT8 Constraint Check | **PASS** | No NT8 API calls; only BCL + standard C# in new code |
| TR-08 P0 Rule Scan | **PASS** | JS-001, JS-002, JS-021, JS-033 all covered by SCAN-01..SCAN-04 |
| TR-09 File Routing | **PASS** | Both files in Wave workspace src/PropTraderTools/ |
| TR-10 Internal Accessor Requirement | **PASS** | All 4 accessors specified with exact signatures |

---

## Overall: TICKET_REVIEW_PASS

**Violation count**: 0
**All 10 checks (TR-01 through TR-10)**: PASS

The ticket is a complete and correct implementation contract. The engineer may proceed.

**Next phase**: ptt-engineer executes T-1 against `src/PropTraderTools/CopyEngine.cs` and `src/PropTraderTools/Tests/B129Tests.cs`.
