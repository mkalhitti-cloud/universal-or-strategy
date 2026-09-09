# PTT-REPAIRS-DW-F-R06 -- Plan Review (Phase 2)

**Reviewer**: PTT Plan Reviewer (ptt-plan-reviewer mode)
**Epic**: PTT-REPAIRS-DW-F-R06
**Plan file**: `docs/brain/PTT-REPAIRS-DW-F-R06/02-architecture-plan.md`
**RULES_CATALOG**: `docs/standards/jane-street/RULES_CATALOG.md` (DNA block in role definition)
**Source reads performed**:
- `src/PropTraderTools/CopyEngine.cs:727` -- F1 current comment text
- `src/PropTraderTools/CopyEngine.cs:738` -- F2 current comment text
- `src/PropTraderTools/CopyEngine.cs:4325-4370` -- seam signatures (HasLeaderDirection, SetLeaderDirection_ForTest, IsLiveEntryBlocked_ForTest, EvictDedup_ForTest)
- `src/PropTraderTools/CopyEngine.cs:5855-5890` -- EvictDedup Cancelled branch production path
- `src/PropTraderTools/CopyEngine.cs:46` -- InternalsVisibleTo attribute
- `src/PropTraderTools/CopyEngineTests.cs:8030-8040` -- insert position (last test body + class/namespace closers)

---

## RESULT

**REVIEW_PASS**

Zero violations found. All source claims verified. All spec requirements addressed. All Jane Street DNA rules satisfied. Lane-split gate correctly stated and result correctly computed.

---

## CHECK-1: LANE-SPLIT GATE RESULT -- PASS

Plan Section 0 (lines 10-24) contains:

```
Q1. F1 and F2 within 50 lines of each other? YES (lines 727 and 738, delta=11)
Q2. Fix B design depends on Fix A final design?  NO
Q3. Each fix has standalone value if the other is blocked? YES
Q4. Each fix has an independent SIM verification path? YES
Gate rule: LANES require NO on Q1+Q2 AND YES on Q3+Q4.
Q1=YES -> lanes NOT approved.

LANE-SPLIT GATE RESULT: SINGLE-PIPELINE
```

Gate logic is correctly applied: Q1=YES disqualifies a lane split per the gate rule (lanes require NO on Q1). Result `SINGLE-PIPELINE` is correct. Gate result is explicitly stated. No gate violations.

---

## CHECK-2: F1 Spec Requirement (doc-only comment, CYC=1 -> CYC=2) -- PASS

**Spec**: Correct stale comment at `CopyEngine.cs:727` from `CYC=1` to `CYC=2`.

**Source verification**: `CopyEngine.cs:727` reads:
```
// PTT-REPAIRS-04 BUG-F: SetCloneAtmObjectCache -- per-instrument. CYC=1.
```
Plan states current text = `CYC=1` (CONFIRMED) and correct text = `CYC=2` (CONFIRMED via prior review: `SetCloneAtmObjectCache` lines 730-736 has base=1 + one `if` branch = CYC=2).

**Change type**: doc-only comment. Zero logic change. No production behavior change. PASS.

---

## CHECK-3: F2 Spec Requirement (doc-only comment, CYC=2 -> CYC=4) -- PASS

**Spec**: Correct stale comment at `CopyEngine.cs:738` from `CYC=2` to `CYC=4`.

**Source verification**: `CopyEngine.cs:738` reads:
```
// PTT-REPAIRS-04 BUG-F: GetCloneAtmMode -- per-instrument. CYC=2.
```
Plan states current text = `CYC=2` (CONFIRMED) and correct text = `CYC=4`. CYC=4 is verified by prior REVIEW_PASS cycle (PTT-REPAIRS-04-POST-BUG-F): base=1 + if(744)+1 + ternary(746)+1 + if(750)+1 = 4. CYC=4 <= 8, so JS-013 is satisfied.

**Change type**: doc-only comment. Zero logic change. No production behavior change. PASS.

---

## CHECK-4: F3 Spec Requirement (new [Fact] test, no production code change) -- PASS

**Spec**: Add new `[Fact]` test `EvictDedup_CancelledEntry_ClearsLastLeaderDirection` to `CopyEngineTests.cs`.

### 4a. No production code change
Plan explicitly states: "No production logic change for F3." T2 ticket touches only `CopyEngineTests.cs`. `CopyEngine.cs` is not in F3 change scope. PASS.

### 4b. Insert position correct
Source confirms `CopyEngineTests.cs:8036` = `}` (last test body closing brace). Lines 8038-8039 are class `}` and namespace `}`. Plan insert position "after line 8036, before lines 8038-8039" is structurally correct. PASS.

### 4c. Test seam signatures verified against source

| Seam | Plan line | Source line | Signature match |
|------|-----------|-------------|-----------------|
| `HasLeaderDirection` | 4330 | 4330 | `internal bool HasLeaderDirection(string instrFullName)` -- CONFIRMED |
| `SetLeaderDirection_ForTest` | 4333 | 4333 | `internal void SetLeaderDirection_ForTest(string instrFullName, OrderAction action)` -- CONFIRMED |
| `IsLiveEntryBlocked_ForTest` | 4348 | 4348 | `internal bool IsLiveEntryBlocked_ForTest(string instrKey, string orderId, double limitPrice)` -- CONFIRMED |
| `EvictDedup_ForTest` | 4360 | 4360 | `internal void EvictDedup_ForTest(string orderId, NinjaTrader.Cbi.OrderState state)` -- CONFIRMED |

### 4d. DISAMBIGUATION NOTE verified
Plan states `HasLeaderDirection` (no `_ForTest` suffix) at line 4330. Source confirms method name is `HasLeaderDirection` (no suffix). The note is accurate. Test body correctly uses `engine.HasLeaderDirection(...)`. PASS.

### 4e. InternalsVisibleTo verified
`CopyEngine.cs:46`: `[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("PropTraderTools.Tests")]` -- CONFIRMED. All four seams are `internal`. Access from test assembly is granted. PASS.

### 4f. Data flow proof verified against source
`CopyEngine.cs:5862-5882` (EvictDedup Cancelled branch) confirmed:
1. Line 5862: `if (state == OrderState.Cancelled)` -- Cancelled branch entered.
2. Line 5872: `if (_entryInstrKeyByOrderId.TryRemove(orderId, out var cancelledInstrKey))` -- removes `"MGC DEC26|Buy"`.
3. Lines 5875-5877: value-guarded removal of `_liveEntryInstruments["MGC DEC26|Buy"]`.
4. Line 5880: `pipeIdx = cancelledInstrKey.IndexOf('|')` = 9 (valid > 0).
5. Line 5882: `_lastLeaderDirection.TryRemove("MGC DEC26", out _)` -- removes direction.
6. `HasLeaderDirection("MGC DEC26")` returns `false`. `Assert.False` is correct.

Plan data flow proof is accurate. PASS.

### 4g. No NT8 type construction
Test uses only enum values (`OrderAction.Buy`, `OrderState.Cancelled`) and string keys. No NT8 Account, Instrument, or AtmStrategy objects constructed. NT8 runtime risk assessment is accurate. PASS.

---

## CHECK-5: JS Rule Compliance -- PASS

| Rule | Requirement | F1 | F2 | F3 | Verdict |
|------|-------------|----|----|-----|---------|
| JS-021 | No `lock()` | N/A (comment-only) | N/A (comment-only) | No `lock()` in test body -- seams use ConcurrentDictionary | PASS |
| JS-042 | ASCII-only | "CYC=2" is pure ASCII | "CYC=4" is pure ASCII | All strings/identifiers ASCII | PASS |
| JS-013 | CYC per helper | N/A (no new helpers) | N/A (no new helpers) | New test method CYC=1 (straight-line, no branches) | PASS |
| JS-001 | No throw in dispatch chain | N/A | N/A | N/A (test, not production dispatch) | PASS |
| JS-002 | No null return | N/A | N/A | N/A (void test method) | PASS |
| JS-008 | Mutable struct / unFrozen brush | N/A | N/A | N/A (no struct/brush) | PASS |
| JS-009 | Dictionary for thread-touched | N/A | N/A | N/A (no new collections) | PASS |
| JS-010 | Public constructor on singleton | N/A | N/A | N/A (no new class) | PASS |
| JS-023 | UI update off-thread | N/A | N/A | N/A (test thread, no UI) | PASS |
| NT8: async/await in lifecycle | Not introduced | Not introduced | Not introduced | PASS |
| NT8: Account.All in constructor | Not introduced | Not introduced | Not introduced | PASS |
| NT8: SCAN-03 FontFamily | Not introduced | Not introduced | Not introduced | PASS |
| NT8: SCAN-04 #RRGGBB hex | Not introduced | Not introduced | Not introduced | PASS |
| NT8: SCAN-05 CreateOrder without PTT- | Not introduced | Not introduced | Not introduced | PASS |
| NT8: SCAN-06 DateTime.Now | Not introduced | Not introduced | Not introduced | PASS |

No JS violations introduced.

---

## CHECK-6: 7-Scan Checklist Presence -- PASS

Plan Section 9 (lines 242-256) contains all 7 scans:

| Scan | Command | Zero-match target |
|------|---------|-------------------|
| SCAN-1 | `grep -r "lock(" src/PropTraderTools/CopyEngine.cs` | 0 matches |
| SCAN-2 | `grep -rP "[^\x00-\x7F]" src/PropTraderTools/` | 0 matches in changed files |
| SCAN-3 | `dotnet build 2>&1 \| grep " error "` | 0 matches |
| SCAN-4 | `dotnet build 2>&1 \| Select-String "Error\(s\)"` | "0 Error(s)" |
| SCAN-5 | `dotnet test --filter "FullyQualifiedName~CopyEngineTests"` | passed >= 19 |
| SCAN-6 | `powershell -File .\deploy-sync.ps1` | "SYNC COMPLETE" |
| SCAN-7 | `(Get-Item ...).LinkType` for both files | "HardLink" or count=1 |

All 7 scans are present with explicit commands and zero-match / success criteria. PASS.

---

## CHECK-7: Ticket Structure Addressability -- PASS

| Ticket | Change | Verify criteria addressable |
|--------|--------|-----------------------------|
| T1 | 2 doc-only line edits in `CopyEngine.cs` (lines 727, 738) | `grep "CYC=2" src/.../CopyEngine.cs` line 727 and `grep "CYC=4"` line 738 -- binary, measurable |
| T2 | Insert `[Fact]` test in `CopyEngineTests.cs` after line 8036 | `grep "EvictDedup_CancelledEntry_ClearsLastLeaderDirection"` and `dotnet test` count delta -- measurable |

Both verify criteria in Section 10 are binary and measurable. No vague "should work" criteria. PASS.

---

## CHECK-8: NinjaTrader 8 API Usage -- PASS

Plan Section 6 enumerates two NT8 API usages:

| API | Usage | AddOn-safe? |
|-----|-------|-------------|
| `OrderAction.Buy` | Enum value argument to `SetLeaderDirection_ForTest` | Yes -- enum value only, no StrategyBase required |
| `OrderState.Cancelled` | Enum value argument to `EvictDedup_ForTest` | Yes -- enum value only, no StrategyBase required |

No `AtmStrategyCreate`, no `Account.CreateOrder`, no `Dispatcher.InvokeAsync` introduced. No AddOn-unsafe API usage. PASS.

---

## SPEC COVERAGE MATRIX

| Requirement | Addressed? | Plan Section |
|-------------|------------|--------------|
| F1: correct `CopyEngine.cs:727` comment from CYC=1 to CYC=2 (doc-only) | YES | Section 2 (F1), Section 4, Section 5, Section 9 |
| F2: correct `CopyEngine.cs:738` comment from CYC=2 to CYC=4 (doc-only) | YES | Section 2 (F2), Section 4, Section 5, Section 9 |
| F3: new `[Fact]` test in `CopyEngineTests.cs` (no production code change) | YES | Section 2 (F3), Section 3 (T2), Section 4, Section 5 |
| F3: test must not require NT8 type construction | YES | Section 2 (NT8 Runtime Risk), Section 6 |
| F3: test uses `InternalsVisibleTo` seams only | YES | Section 2 (Helper Verification, InternalsVisibleTo) |
| F3: data flow proof that Assert.False is correct | YES | Section 2 (Data Flow Proof) |
| All fixes in single pipeline (no lane split) | YES | Section 0 (LANE-SPLIT GATE RESULT) |
| JS rule compliance table | YES | Section 8 |
| 7-scan checklist (both tickets) | YES | Section 9 |
| Measurable verify criteria | YES | Section 10 |
| Baseline test counts documented | YES | Section 11 |
| Deferred items disposition | YES | Section 12 (None) |

All spec requirements addressed. No unaddressed requirement found.

---

## VIOLATIONS

**None.**

---

## SUMMARY

All 8 checks pass with zero violations.

- Lane-split gate is correctly stated, correctly computed (Q1=YES disqualifies lanes), result is SINGLE-PIPELINE.
- F1 and F2 source comment texts verified at lines 727 and 738; plan correctly identifies current and correct text.
- F3 seam signatures verified at exact source lines (4330, 4333, 4348, 4360). Insert position confirmed as structurally correct (after line 8036, before class/namespace closers at 8038-8039).
- EvictDedup Cancelled branch (5862-5882) verified: `Assert.False` is mathematically guaranteed.
- `InternalsVisibleTo("PropTraderTools.Tests")` at `CopyEngine.cs:46` confirmed.
- All 7 scans present with explicit zero-match criteria.
- No `lock()`, no non-ASCII, no NT8-unsafe API, no production code in F3. All JS DNA rules satisfied.
- Maximum CYC introduced: 1 (new test method, straight-line). Well within JS-013 limit of 8.

**REVIEW_PASS**

---

*Reviewed against: RULES_CATALOG.md DNA block (role definition); source reads at CopyEngine.cs:727, 738, 4325-4370, 5855-5890, L46; CopyEngineTests.cs:8030-8040; prior REVIEW_PASS for PTT-REPAIRS-04-POST-BUG-F (CYC=4 for GetCloneAtmMode confirmed).*
