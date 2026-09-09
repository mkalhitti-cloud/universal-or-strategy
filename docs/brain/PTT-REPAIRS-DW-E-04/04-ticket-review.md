# 04-ticket-review.md — PTT-REPAIRS-DW-E-04

**Status**: TICKET_REVIEW_PASS
**Phase**: 3.5 (Ticket Review)
**Reviewer**: ptt-ticket-reviewer
**Epic**: PTT-REPAIRS-DW-E-04
**Source tickets**: `docs/brain/PTT-REPAIRS-DW-E-04/04-tickets.md`
**Source plan**: `docs/brain/PTT-REPAIRS-DW-E-04/02-architecture-plan.md` (REVIEW_PASS)
**Plan review**: `docs/brain/PTT-REPAIRS-DW-E-04/02-plan-review.md` (REVIEW_PASS, 0 violations)
**Source read**: `src/PropTraderTools/CopyEngine.cs` lines 1538–1545, 4325–4370, 5840–5910

---

## Ticket Review: PTT-REPAIRS-DW-E-04

### T1 — EvictDedup CYC Reduction via Extraction (CopyEngine + Test)

---

#### Traceability

**Result**: PASS

| Check | Finding |
|-------|---------|
| Spec requirement IDs JS-013, JS-021, JS-001, JS-002, JS-042 all listed | T1 §1 table — all 5 present ✓ |
| Each acceptance criterion traceable to spec rule or plan section | AC-01 → SCAN-03/04; AC-02 → SCAN-05; AC-03 → SCAN-05; AC-04 → §4 + §5.1; AC-05 → §4 + §5.2; AC-06 → §4 + §5.3; AC-07 → §6; AC-08 → §7; AC-09 → SCAN-01; AC-10 → SCAN-02; AC-11 → SCAN-06; AC-12 → §11 — all traced ✓ |
| Ticket scope matches plan §10 (single ticket) | T1 §1–12 = plan §1–11; SINGLE-PIPELINE confirmed in both ✓ |
| No phantom work (items not in plan/spec) | All ticket items (method bodies §5, BUG-E §6, call-site §7, test §8, scans §9, ACs §10, risks §12) are grounded in plan §§5–11 ✓ |
| No missing work (plan items absent from ticket) | Plan §§1–11 fully covered in ticket §§1–12 ✓ |

**Traceability: PASS**

---

#### JS Pre-Check

**Result**: PASS

| Rule | Check | Finding |
|------|-------|---------|
| JS-013 (CYC ≤ 8) | CYC targets stated for all three methods with arithmetic | T1 §4 table: EvictDedup=7, EvictCancelledEntry=4, EvictFilledEntry=3; arithmetic column present ✓ |
| JS-021 (no lock()) | lock() prohibition stated for all three methods | T1 §1 table row "JS-021: No lock() statement — All three methods"; method bodies in §5 contain no lock() ✓ |
| JS-001 (no throw) | throw prohibition stated for all three methods | T1 §1 table row "JS-001: No throw statement — All three methods"; method bodies in §5 contain no throw ✓ |
| JS-002 (no null return) | null return prohibition stated | T1 §1 table row "JS-002: No null return — All three methods (all void)"; trivially satisfied ✓ |
| JS-042 (ASCII-only) | ASCII-only constraint stated for all three methods and comments | T1 §1 table row "JS-042: ASCII-only identifiers and string literals — All three methods, all comments"; enforced by SCAN-02 ✓ |

No described pattern violates JS-013, JS-021, JS-001, JS-002, or JS-042.

**JS Pre-Check: PASS**

---

#### CYC Pre-Check

**Result**: PASS

| Method | Ticket Target | Limit | Arithmetic Provided | Arithmetic Correct |
|--------|--------------|-------|--------------------|--------------------|
| `EvictDedup` | 7 | 8 ✓ | Base(1)+&&x2(+2)+if Cancelled(+1)+if TryRemove cancelledInstrKey(+1)+if Filled(+1)+if TryRemove filledInstrKey(+1)=7 | Verified against §5.1 body: 6 decision points + base = 7 ✓ |
| `EvictCancelledEntry` | 4 | 8 ✓ | Base(1)+if(TryGetValue&&storedId==orderId) if(+1)+&&(+1)+if(pipeIdx>0)(+1)=4 | Verified against §5.2 body: 3 decision points + base = 4 ✓ |
| `EvictFilledEntry` | 3 | 8 ✓ | Base(1)+if(TryGetValue&&storedId==orderId) if(+1)+&&(+1)=3 | Verified against §5.3 body: 2 decision points + base = 3 ✓ |

All three CYC targets ≤ 8. All three arithmetic derivations present and correct.

**CYC Pre-Check: PASS**

---

#### NT8 Check

**Result**: PASS

| Check | Finding |
|-------|---------|
| `EvictDedup` signature preserved exactly: `(string orderId, OrderState state)` | T1 §3 shows `internal void EvictDedup(string orderId, OrderState state)`. Verified against source line 5851 — exact match ✓ |
| Call site at line 1541 marked DO NOT TOUCH | T1 §2 "Do NOT touch" list item 1: "Line 1541: `EvictDedup(e.Order.OrderId.ToString(), e.Order.OrderState);` — must remain UNCHANGED." T1 §7 repeats this with verbatim content and verification grep command ✓ |
| All three methods: internal/private void, lock-free, no throw, ASCII-only | T1 §3: `internal void EvictDedup`, `private void EvictCancelledEntry`, `private void EvictFilledEntry`. T1 §5 bodies: all ConcurrentDictionary ops (lock-free), no throw, ASCII-only comments ✓ |
| InternalsVisibleTo noted as already in place (no new attribute needed) | T1 §8 "Helper method locations (already in source — do NOT add, do NOT modify)". Plan review §3f confirms `// InternalsVisibleTo("PropTraderTools.Tests") granted at L46` confirmed at source line 4340 ✓ |
| No async/await in lifecycle method | No async/await in any described method body ✓ |
| No Account.All outside Loaded handler | No Account.All reference ✓ |
| No sealed on TradeCopierWindow | Not applicable to this ticket ✓ |
| No FontFamily or hardcoded hex color | No UI elements in scope ✓ |
| No CreateOrder with non-PTT- name | No order creation in scope ✓ |
| No DateTime.Now usage | No DateTime.Now in any described body ✓ |

**NT8 Check: PASS**

---

#### BUG-E Fix Preservation Check

**Result**: PASS

T1 §6 contains the verbatim four-line code block:

```
// PTT-REPAIRS-04 BUG-E: entry cancelled without fill -- direction record is stale.
// Clear _lastLeaderDirection so next entry in any direction is not reversal-blocked.
var pipeIdx = cancelledInstrKey.IndexOf('|');
if (pipeIdx > 0)
    _lastLeaderDirection.TryRemove(cancelledInstrKey.Substring(0, pipeIdx), out _);
```

Cross-checked against source lines 5878–5882 — verbatim match confirmed.

T1 §6 "Preservation rules" items 1–6:
- Rule 1: both comment lines verbatim (exact wording, exact dashes, ASCII-only) ✓
- Rule 2: `cancelledInstrKey.IndexOf('|')` — exact method, character literal `'|'` (pipe) ✓
- Rule 3: guard is `if (pipeIdx > 0)` — not `>= 0`, not `!= -1` ✓
- Rule 4: `_lastLeaderDirection.TryRemove(cancelledInstrKey.Substring(0, pipeIdx), out _)` — exact call, no intermediate variable ✓
- Rule 5: code must NOT be moved to EvictDedup or anywhere else — belongs only in EvictCancelledEntry ✓
- Rule 6: engineer must diff extracted body against original source lines 5878–5882 ✓

T1 §5.2 method body shows the BUG-E block verbatim at its correct position (after `TryRemove(cancelledInstrKey, out _)`). No ticket language permits any reformulation. The placement is pinned: §5.2 position 4 lines, §6 contract 6 rules.

**BUG-E Fix Preservation Check: PASS**

---

#### Test Coverage

**Result**: PASS

| Check | Finding |
|-------|---------|
| `[Fact]` `EvictDedup_CancelledEntry_ClearsLastLeaderDirection` specified | T1 §8 — present with full body ✓ |
| File: `CopyEngineTests.cs` | T1 §8 header: `src/PropTraderTools.Tests/CopyEngineTests.cs` ✓ |
| Arrange/Act/Assert pattern explicit | T1 §8 shows `// Arrange:`, `// Act:`, `// Assert:` comment lines ✓ |
| Assert calls `HasLeaderDirection` (not `HasLeaderDirection_ForTest`) | T1 §8 uses `Assert.False(_engine.HasLeaderDirection("MGC DEC26"))`. T1 §8 CRITICAL note explicitly flags the distinction. Source line 4330 confirms `HasLeaderDirection` has no `_ForTest` suffix ✓ |
| `EvictDedup_ForTest` confirmed at source line 4360 | Source read confirmed: `internal void EvictDedup_ForTest(string orderId, NinjaTrader.Cbi.OrderState state) => EvictDedup(orderId, state);` ✓ |
| `SetLeaderDirection_ForTest` confirmed at source line 4333 | Source read confirmed: `internal void SetLeaderDirection_ForTest(string instrFullName, OrderAction action) => _lastLeaderDirection[instrFullName] = action;` ✓ |
| `IsLiveEntryBlocked_ForTest` confirmed at source line 4348 | Source read confirmed: `internal bool IsLiveEntryBlocked_ForTest(string instrKey, string orderId, double limitPrice)` ✓ |
| Baseline: passed >= 19 stated | T1 §9 SCAN-05: "Baseline: 19 passed / 449 failed / 31 skipped (pre-epic)" and "passed count >= 19 (baseline was 19; new test adds 1, so ≥ 20 after this change)" ✓ |
| Test logic sound | `"MGC DEC26|Buy"` stripped at `|` → `"MGC DEC26"`; `EvictCancelledEntry` calls `_lastLeaderDirection.TryRemove("MGC DEC26", out _)`; `HasLeaderDirection("MGC DEC26")` returns false ✓ |

**Test Coverage: PASS**

---

#### Scan Checklist

**Result**: PASS

| Scan | Command Present | Expected Result Stated |
|------|----------------|----------------------|
| SCAN-01 lock() audit | `grep -n "lock(" src/PropTraderTools/CopyEngine.cs \| grep -E "EvictDedup\|EvictCancelledEntry\|EvictFilledEntry"` | 0 matches (empty output) ✓ |
| SCAN-02 Non-ASCII audit | `grep -Pn "[^\x00-\x7F]" src/PropTraderTools/CopyEngine.cs \| grep -E "EvictDedup\|EvictCancelledEntry\|EvictFilledEntry"` | 0 matches (empty output) ✓ |
| SCAN-03 Build errors | `dotnet build 2>&1 \| grep " Error"` | 0 Error(s) ✓ |
| SCAN-04 Build summary | `dotnet build 2>&1 \| Select-String "Error\(s\)"` | `Build succeeded.` with `0 Error(s)` ✓ |
| SCAN-05 Test counts | `dotnet test 2>&1 \| Select-String "passed\|failed\|skipped"` | passed ≥ 19; skipped ≥ 31; new test PASS ✓ |
| SCAN-06 deploy-sync | `powershell -File .\deploy-sync.ps1` | `SYNC COMPLETE` ✓ |
| SCAN-07 Hard-link count | `(Get-Item "src/PropTraderTools/CopyEngine.cs").LinkType` + `fsutil hardlink list src\PropTraderTools\CopyEngine.cs` | `HardLink` + 1 NinjaTrader DLL target ✓ |

All 7 scans present with commands and expected results. Defense-in-depth contract intact (Layer 1 of 3).

**Scan Checklist: PASS**

---

#### File Routing

**Result**: PASS

| File | Path in Ticket | Correct Wave Workspace Path |
|------|---------------|----------------------------|
| CopyEngine.cs | `src/PropTraderTools/CopyEngine.cs` | ✓ Wave workspace |
| CopyEngineTests.cs | `src/PropTraderTools.Tests/CopyEngineTests.cs` | ✓ Wave workspace |

No Director workspace paths (`c:\WSGTA\universal-or-strategy-director\`) referenced for .cs files.

**File Routing: PASS**

---

#### Completeness

**Result**: PASS

| Check | Finding |
|-------|---------|
| Files to modify listed | T1 §2: `CopyEngine.cs` and `CopyEngineTests.cs` both listed with wave workspace paths ✓ |
| Output artifact named | T1 §11: `docs/brain/PTT-REPAIRS-DW-E-04/ticket-1-completion.md` ✓ |
| Engineer scope locked to T1 only | T1 §2 "Do NOT touch" list; T1 §10 AC-12; T1 §12 risks all scoped to the three target methods. No second ticket issued ✓ |

**Completeness: PASS**

---

### T1 VERDICT: TICKET_REVIEW_PASS

All 8 checks PASS. No violations. No warnings.

---

## Violations

**None.**

---

## Overall: TICKET_REVIEW_PASS

| Check | T1 |
|-------|----|
| Traceability | PASS |
| JS Pre-Check | PASS |
| CYC Pre-Check | PASS |
| NT8 Check | PASS |
| BUG-E Fix Preservation | PASS |
| Test Coverage | PASS |
| Scan Checklist | PASS |
| File Routing | PASS |
| Completeness | PASS |

**Verdict: TICKET_REVIEW_PASS**

The engineer is cleared to implement T1. The ticket supplies all required contracts:
- Spec requirement IDs (JS-013, JS-021, JS-001, JS-002, JS-042)
- Exact method signatures for all three methods
- Verbatim method bodies with CYC arithmetic
- BUG-E fix verbatim block with 6-rule preservation contract
- `[Fact]` test name, file, Arrange/Act/Assert body, and helper name disambiguation
- All 7 scan commands with expected results (Layer 1 engineer contract)
- Delivery artifact `ticket-1-completion.md` named

*Review complete. Returning: TICKET_REVIEW_PASS*
