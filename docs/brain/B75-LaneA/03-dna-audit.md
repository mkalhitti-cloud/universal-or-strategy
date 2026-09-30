# B75-LaneA DNA Audit Report
**Epic**: B75-LaneA
**Phase**: Phase 3 — DNA Audit
**Auditor**: ptt-verifier
**File Audited**: `src/PropTraderTools/CopyEngine.cs`
**Plan Reference**: `docs/brain/B75-LaneA/02-architecture-plan.md`
**Date**: 2026-08-17

---

## Overall Verdict

**DNA_FAIL**

One confirmed violation (pre-existing, not introduced by B75):
- **NON-ASCII-01**: 6 non-ASCII bytes at lines 202, 203, 493, 697, 1856, 1857 (em-dashes, box-drawing, arrows in comments).

One structural gap (engineer incomplete per Section G plan):
- **CYC-01**: `OnOrderUpdate` CYC exceeds 8 — `TryFireFollowerBeDisarm` extraction not implemented (plan Section G.1 required it).

---

## Section 1 — 7-Scan Results

### SCAN 1 — lock() Ban (JS-021)
```
Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "lock\s*\(" | Where-Object { $_.Line -notmatch "//" }
```
**Result**: 0 matches
**Verdict**: PASS

---

### SCAN 2 — async void Ban (JS-033)
```
Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "async\s+void\s+\w+\("
```
**Result**: 0 matches
**Verdict**: PASS

---

### SCAN 3 — throw new Exception Ban (JS-001)
```
Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "throw\s+new\s+\w+Exception"
```
**Result**: 0 matches
**Verdict**: PASS

---

### SCAN 4 — volatile double/float Ban (NT8-003)
```
Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "volatile\s+(double|float)"
```
**Result**: 2 matches — BOTH are comments only, no live declarations.

| Line | Content |
|------|---------|
| 115 | `// NT8-003: volatile double/float BANNED -- string is safe.` |
| 203 | `// JS-023: volatile int allowed. NT8-003: volatile double banned - not used here.` |

**Verdict**: PASS (comment-only hits; no actual `volatile double` or `volatile float` declarations exist)

---

### SCAN 5 — DIAG-Cancel Lines Removed
```
Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "DIAG-Cancel"
```
**Result**: 0 matches
**Verdict**: PASS — all DIAG-CancelAll and DIAG-CancelOne lines confirmed removed. [PTT-CLONE] lines retained per plan authorization.

---

### SCAN 6 — Instrument Reference Equality
```
Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "o\.Instrument\s*==\s*instrument"     → 0 matches
Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "\.Instrument\s*!=\s*instrument"      → 8 matches
```

**Detailed analysis of the 8 hits:**

| Line | Method | instrument type | Assessment |
|------|--------|-----------------|------------|
| 484 | `GetSavedFollowerNames(string instrument, ...)` | `string` | `rule.Instrument` is also `string` field → value equality via `string.!=`. **No violation.** |
| 761 | `SetFollowerMultiplier(string instrument, ...)` | `string` | Same as above — `ConcurrentBag<CopyRule>` rebuild, string compare. **No violation.** |
| 1282 | `FindFollowerEntryOrder(Account, Instrument instrument)` | `Instrument` (NT8 obj) | NT8 singleton pattern — Instrument objects are canonical per session. Reference equality is the standard NT8 AddOn pattern. **Not a violation.** |
| 1499 | `HasWorkingEntries(Account, Instrument instrument)` | `Instrument` (NT8 obj) | Same — canonical NT8 reference compare. **Not a violation.** |
| 1682 | `SetAtmMode(string instrument, ...)` | `string` | `r.Instrument` is `string` — value equality. **No violation.** |
| 1903 | `CancelOneAccount(Account, Instrument instrument)` | `Instrument` (NT8 obj) | Canonical NT8 reference compare. **Not a violation.** |
| 1930 | `CancelStaleExitOrders(Account, Instrument, string)` | `Instrument` (NT8 obj) | Canonical NT8 reference compare. **Not a violation.** |
| 2406 | `ShouldTightenOrder(Order, Instrument instrument)` | `Instrument` (NT8 obj) | Canonical NT8 reference compare. **Not a violation.** |

**Verdict**: PASS — all `string` comparisons use value equality correctly; all `Instrument` object comparisons use NT8 canonical reference identity (correct and standard for AddOn API).

---

### SCAN 7 — CYC Manual Count

#### 7.1 — OnOrderUpdate (lines 801–938)

Counting convention: branch-only McCabe (each `if`, `foreach`, `while`, `case` = 1; compound boolean operators not counted separately — consistent with plan's Section G.3 table).

| # | Branch | Count |
|---|--------|-------|
| 1 | `if (e.Order != null && ... PTT-BE-Stop ... && FullName != null)` | 1 |
| 2 | `foreach (var r in _rules)` (isLeader scan) | 1 |
| 3 | `if (e.Order.Account.Name == r.MasterAccount?.Name)` | 1 |
| 4 | `if (!isLeader)` | 1 |
| 5 | `if (IsPttEntryOrderCancelTrigger(e.Order))` | 1 |
| 6 | `if (!_isCopyEnabled)` | 1 |
| 7 | `foreach (var rule in _rules)` (matchedRule scan) | 1 |
| 8 | `if (e.Order.Instrument.FullName == rule.Instrument && ...)` | 1 |
| 9 | `if (matchedRule == null)` | 1 |
| 10 | `if (!matchedRule.Value.Enabled)` | 1 |
| 11 | `if ((CopyMode)_copyModeValue == CopyMode.Mirror)` | 1 |
| 12 | `if (e.Order.OrderState == OrderState.Cancelled)` | 1 |
| 13 | `if (IsAtmBracketName(e.Order.Name))` | 1 |
| 14 | `foreach (var acc in matchedRule.Value.FollowerAccounts)` | 1 |
| 15 | `if (acc == null)` | 1 |
| 16 | `if (TryDispatchLeaderFlat(...))` | 1 |
| 17 | `if (IsWorkingBracket(e.Order))` | 1 |
| 18 | `if (e.Order.FromEntrySignal != null)` | 1 |
| 19 | `if ((Limit\|\|StopLimit) && (Accepted\|\|Working) && Filled==0)` (Gate C outer) | 1 |
| 20 | `if (_dedupCache.TryGetValue(...) && Math.Abs(...)>=...)` | 1 |

**OnOrderUpdate CYC = 20** (branch-only). Comment at line 800 still says `CYC=9` (stale — from B7-F0 era, before all hotfixes were applied and before B75 extraction).

**Plan target**: Section G.3 requires extraction of BOTH `IsPttManagedEntryName` (→ `IsPttEntryOrderCancelTrigger` implemented) AND `TryFireFollowerBeDisarm` (NOT implemented). Plan's two-step extraction target was CYC = 7.

**Current state after B75**: Only `IsPttEntryOrderCancelTrigger` was extracted (-1 branch from pre-Gate-1 `||`). `TryFireFollowerBeDisarm` was NOT extracted — the HOTFIX-FLAT-DISARM-FOLLOWER block (items 1-4 in table above, ~4 branches) remains inline.

**CYC verdict**: **FAIL** — OnOrderUpdate does not meet CYC <= 8.

Note: The HOTFIX-FLAT-DISARM-FOLLOWER block alone (items 1-4) contributes 4 branches. Extracting it to a helper method (as planned in Section G.1) would reduce the count to ~16 branch-only... still above 8. The plan's claim that two extractions would yield CYC=7 appears to have used a different baseline count (CYC=10 + two extractions = 8, then a bonus -1 = 7). The true current branch count is higher than the plan's stated baseline of 10, suggesting additional branches accumulated between the B7-F0 baseline and B75.

#### 7.2 — TryDispatchLeaderFlat (lines 1481–1491)

| # | Branch | Count |
|---|--------|-------|
| 1 | `if (state != Filled && state != Cancelled)` | 1 |
| 2 | `if (isFollower(account))` | 1 |
| 3 | `if (IsNonFlatDispatchName(orderName))` | 1 |
| 4 | `if (!IsNativeExitName(orderName) && hasOpenPosition(account, instrument))` | 1 |
| 5 | `foreach (var acc in rule.FollowerAccounts)` | 1 |
| 6 | `if (acc == null)` | 1 |

**TryDispatchLeaderFlat CYC = 6** (branch-only). Comment at line 1467 says `CYC=8` — actual post-extraction count is **6**. The comment is conservative. 

**Verdict**: PASS — TryDispatchLeaderFlat CYC = 6 <= 8. `IsNonFlatDispatchName` extraction correctly collapsed gates (2.5) and (2.6) into a single branch.

#### 7.3 — Helper Methods (New)

| Method | Line | CYC (comment) | CYC (verified) | Status |
|--------|------|---------------|----------------|--------|
| `IsPttEntryOrderCancelTrigger` | 535 | 3 | 3 (null + Cancelled + name guards) | PASS |
| `IsNonFlatDispatchName` | 1049 | 2 | 2 (PTT-prefix + "Entry" literal) | PASS |

Both helpers are `internal static`, ASCII-only, no throws, no null returns. ✅

---

## Section 2 — Non-ASCII Scan (Additional Check)

```powershell
Get-Content "src/PropTraderTools/CopyEngine.cs" | Where-Object { $_ -match '[^\x00-\x7F]' }
```

**Result**: 6 lines with non-ASCII bytes:

| Line | Non-ASCII chars | Content (excerpt) |
|------|-----------------|-------------------|
| 202 | em-dash (U+2014) | `// HOTFIX-QUICKALL-SINGLETON-01: Quick ALL tick buffer — singleton...` |
| 203 | em-dash (U+2014) | `// JS-023: volatile int allowed. NT8-003: volatile double banned — not used...` |
| 493 | box-drawing (U+2500) | `// ── B56 BUILD-FIX stubs ...` |
| 697 | box-drawing (U+2500) | `// ── end B56 BUILD-FIX stubs ──` |
| 1856 | right-arrow (U+2192) | `// Long exits (Sell Limit) post at bid - buffer (at/below market → fills...` |
| 1857 | right-arrow (U+2192) | `// Short exits (BuyToCover) post at ask + buffer (at/above market → fills...` |

**Provenance**: `git diff HEAD -- src/PropTraderTools/CopyEngine.cs | Select-String "^\+" | Select-String "[^\x00-\x7F]"` → **0 matches** — confirmed NOT introduced by B75 engineer. All 6 are pre-existing from commit `7e159dc2` (B72/B73/B74 block).

**Verdict**: Pre-existing violation (not a B75 regression). Must be remediated before the file is considered clean. All occurrences are in comments, not string literals or identifiers — no runtime impact, but they violate the ASCII-only mandate.

---

## Section 3 — CYC Summary Table

| Method | Pre-B75 CYC | Plan Target (post-B75) | Actual Post-B75 CYC | Status |
|--------|-------------|----------------------|---------------------|--------|
| `OnOrderUpdate` | 10 (plan baseline) / ~21 (full branch count) | 7 | 20 (branch-only) | **FAIL** |
| `TryDispatchLeaderFlat` | 9 (plan) | 8 | 6 | PASS |
| `IsPttEntryOrderCancelTrigger` | — (new) | 3 | 3 | PASS |
| `IsNonFlatDispatchName` | — (new) | 2 | 2 | PASS |
| `TryFireFollowerBeDisarm` | — (planned) | 3 | NOT IMPLEMENTED | **MISSING** |

---

## Section 4 — Violations Summary

| ID | Severity | Type | Location | Description |
|----|----------|------|----------|-------------|
| NON-ASCII-01 | P1 | ASCII compliance | Lines 202, 203, 493, 697, 1856, 1857 | Pre-existing non-ASCII bytes (em-dash, box-drawing, arrow) in comments. Not introduced by B75 — requires remediation. |
| CYC-01 | P1 | CYC > 8 | `OnOrderUpdate` line 801 | `TryFireFollowerBeDisarm` extraction specified in plan Section G.1 was not implemented. OnOrderUpdate remains CYC > 8 (20 by branch-only count). Engineer must extract the HOTFIX-FLAT-DISARM-FOLLOWER block (lines 813–833) to achieve the plan's target CYC. |

---

## Section 5 — Passing Items

| Check | Result |
|-------|--------|
| SCAN 1: lock() | PASS — 0 occurrences |
| SCAN 2: async void | PASS — 0 occurrences |
| SCAN 3: throw new XxxException | PASS — 0 occurrences |
| SCAN 4: volatile double/float | PASS — comment-only, no declarations |
| SCAN 5: DIAG-Cancel lines | PASS — all removed |
| SCAN 6: Instrument reference equality | PASS — string fields use value equality; Instrument objects use NT8 canonical reference pattern |
| SCAN 7: TryDispatchLeaderFlat CYC | PASS — CYC=6 after IsNonFlatDispatchName extraction |
| SCAN 7: IsPttEntryOrderCancelTrigger | PASS — CYC=3, internal static, correct |
| SCAN 7: IsNonFlatDispatchName | PASS — CYC=2, internal static, correct |
| JS-021 (no lock) | PASS |
| JS-033 (no async void) | PASS |
| JS-001 (no throw in hot path) | PASS |
| JS-002 (no null return) | PASS |
| NT8-003 (no volatile double/float) | PASS |
| DIAG-CancelAll/CancelOne removed | PASS |

---

## Section 6 — Required Engineer Actions Before VERIFY_PASS

1. **CYC-01 (BLOCKING)**: Extract `TryFireFollowerBeDisarm` from `OnOrderUpdate` per plan Section G.1:
   - Extract lines 813–833 (HOTFIX-FLAT-DISARM-FOLLOWER block) to a new `private void TryFireFollowerBeDisarm(OrderEventArgs e)` method.
   - Method signature: `private void TryFireFollowerBeDisarm(OrderEventArgs e)` — CYC=3 (null guard + isLeader loop + `if (!isLeader)` block).
   - Call site: Replace lines 813–833 in `OnOrderUpdate` with `TryFireFollowerBeDisarm(e);` (1 statement, no branch count at call site beyond existing `foreach` for the isLeader scan inside the helper).
   - Update `OnOrderUpdate` header comment from `CYC=9` to the post-extraction value.
   - Add mandatory xUnit `[Fact]` tests: `TryFireFollowerBeDisarm` is NT8-runtime-bound — mark with `[Fact(Skip="NT8-runtime")]` as directed in plan Section G.4.

2. **NON-ASCII-01 (NON-BLOCKING — pre-existing, not B75 regression)**: Replace non-ASCII chars in comments:
   - Lines 202, 203: Replace em-dash `—` with ASCII hyphen `-` or double-dash `--`.
   - Lines 493, 697: Replace box-drawing `──` with ASCII `//---` or plain dashes.
   - Lines 1856, 1857: Replace right-arrow `→` with ASCII `->`.
   - Note: These pre-date B75. The current block is not required to fix them, but the next block touching this file should include the fix.

---

*End of B75-LaneA DNA Audit.*
---

## Re-Verify Results — Ph2 Re-Run (2026-08-17)

**Verifier**: ptt-verifier
**Trigger**: Ph2 re-run performed to fix OnOrderUpdate CYC. Re-verification required.
**Source state**: `src/PropTraderTools/CopyEngine.cs` (post-Ph2 extraction pass)

---

### Re-Verify SCAN 1 — lock() Ban

```
Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "lock\s*\(" | Where-Object { $_.Line -notmatch "//" }
```
**Result**: 0 matches
**Verdict**: PASS

---

### Re-Verify SCAN 2 — async void Ban

```
Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "async\s+void\s+\w+\("
```
**Result**: 0 matches
**Verdict**: PASS

---

### Re-Verify SCAN 3 — throw new Exception Ban

```
Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "throw\s+new\s+\w+Exception"
```
**Result**: 0 matches
**Verdict**: PASS

---

### Re-Verify SCAN 4 — volatile double/float Ban

```
Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "volatile\s+(double|float)"
```
**Result**: 2 matches — BOTH are comments only (lines 115, 203). No live declarations.
**Verdict**: PASS

---

### Re-Verify SCAN 5 — DIAG-Cancel Lines

```
Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "DIAG-Cancel"
```
**Result**: 0 matches
**Verdict**: PASS

---

### Re-Verify SCAN 6 — Instrument Reference Equality

```
Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "\.Instrument\s*!=\s*instrument"
```
**Result**: 8 matches (lines 484, 761, 1290, 1507, 1690, 1911, 1938, 2414)

All 8 hits use object-reference equality (`Instrument` vs `Instrument`) — the correct NT8 AddOn singleton pattern. No string comparison misuse detected.
**Verdict**: PASS

---

### Re-Verify SCAN 7 — CYC Manual Count (Post-Extraction)

Counting convention: branch-only McCabe (each `if`, `foreach`, `while`, `case` = +1; boolean operators in return expressions not counted).

#### OnOrderUpdate (lines 801-856)

| # | Line | Branch |
|---|------|--------|
| 1 | 812 | `if (IsPttEntryOrderCancelTrigger(e.Order))` |
| 2 | 816 | `if (!_isCopyEnabled)` |
| 3 | 823 | `if (matchedRule == null)` |
| 4 | 827 | `if (!matchedRule.Value.Enabled)` |
| 5 | 834 | `if ((CopyMode)_copyModeValue == CopyMode.Mirror)` |
| 6 | 839 | `if (TryCancelFollowerEntries(...))` |
| 7 | 842 | `if (TryDispatchLeaderFlat(...))` |
| 8 | 848 | `if (TryHandleBracketDrag(...))` |
| 9 | 852 | `if (TryHandleEntryDrag(...))` |

**OnOrderUpdate CYC = 1 + 9 = 10**
Engineer comment at line 800 says `CYC=7` — **incorrect**. Actual count is 10.
Note: `TryFireFollowerBeDisarm` has been successfully extracted (the 4+ inline HOTFIX-FLAT-DISARM-FOLLOWER branches are gone), reducing from the original ~20 branch-only count. However 9 remaining `if` gates yield CYC=10, which still exceeds the <=8 limit.

**Verdict: FAIL — CYC=10 > 8**

#### TryFireFollowerBeDisarm (lines 861-882) — NEW METHOD

| # | Line | Branch |
|---|------|--------|
| 1 | 863 | `if (e.Order == null) return;` |
| 2 | 864 | `if (e.Order.OrderState != OrderState.Filled) return;` |
| 3 | 865 | `if (e.Order.Name == null) return;` |
| 4 | 866 | `if (!e.Order.Name.StartsWith("PTT-BE-Stop")) return;` |
| 5 | 867 | `if (e.Order.Instrument?.FullName == null) return;` |
| 6 | 870 | `foreach (var r in _rules)` |
| 7 | 872 | `if (e.Order.Account.Name == r.MasterAccount?.Name)` |
| 8 | 874 | `if (isLeader) return;` |

**TryFireFollowerBeDisarm CYC = 1 + 8 = 9**
Engineer comment at line 858 says `CYC=8` — **incorrect**. Actual count is 9 (the guard sequence lines 863-867 contributes 5 branches, not 4).

**Verdict: FAIL — CYC=9 > 8**

#### TryDispatchLeaderFlat (lines 1483-1500)

| # | Line | Branch |
|---|------|--------|
| 1 | 1490 | `if (state != Filled && state != Cancelled) return false;` |
| 2 | 1491 | `if (isFollower(account)) return false;` |
| 3 | 1492 | `if (IsNonFlatDispatchName(orderName)) return false;` |
| 4 | 1493 | `if (!IsNativeExitName(orderName) && hasOpenPosition(...)) return false;` |
| 5 | 1494 | `foreach (var acc in rule.FollowerAccounts)` |
| 6 | 1496 | `if (acc == null) continue;` |

**TryDispatchLeaderFlat CYC = 1 + 6 = 7**
**Verdict: PASS — CYC=7 <= 8**

#### FindMatchingRule (lines 887-896) — NEW METHOD

| # | Line | Branch |
|---|------|--------|
| 1 | 889 | `foreach (var rule in _rules)` |
| 2 | 891 | `if (instrument.FullName == rule.Instrument && account.Name == rule.MasterAccount?.Name)` |

**FindMatchingRule CYC = 1 + 2 = 3**
**Verdict: PASS — CYC=3 <= 8**

#### IsPttEntryOrderCancelTrigger (lines 535-541) — NEW METHOD

| # | Line | Branch |
|---|------|--------|
| 1 | 537 | `if (order == null) return false;` |
| 2 | 538 | `if (order.OrderState != OrderState.Cancelled) return false;` |
| 3 | 539 | `if (order.Name != "PTT-Copy" && order.Name != "Entry") return false;` |

Line 540 `return order.LimitPrice > 0 && ...` — boolean in return expression, not a control-flow branch (not counted).
**IsPttEntryOrderCancelTrigger CYC = 1 + 3 = 4** (within <=4 spec limit)
**Verdict: PASS — CYC=4 <= 4**

#### IsNonFlatDispatchName (lines 1057-1062) — NEW METHOD

| # | Line | Branch |
|---|------|--------|
| 1 | 1059 | `if (orderName != null && orderName.StartsWith("PTT-", ...)) return true;` |
| 2 | 1060 | `if (orderName == "Entry") return true;` |

**IsNonFlatDispatchName CYC = 1 + 2 = 3**
**Verdict: PASS — CYC=3 <= 8**

---

### Re-Verify CYC Summary Table

| Method | Annotated CYC | Actual CYC (verified) | Limit | Status |
|--------|---------------|----------------------|-------|--------|
| `OnOrderUpdate` | 7 (comment) | **10** | <=8 | **FAIL** |
| `TryFireFollowerBeDisarm` | 8 (comment) | **9** | <=8 | **FAIL** |
| `TryDispatchLeaderFlat` | — | 7 | <=8 | PASS |
| `FindMatchingRule` | 3 (comment) | 3 | <=8 | PASS |
| `IsPttEntryOrderCancelTrigger` | 3 (comment) | 4 | <=4 | PASS |
| `IsNonFlatDispatchName` | 2 (comment) | 3 | <=8 | PASS |

---

### Re-Verify New Method Existence Check

```
Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "TryFireFollowerBeDisarm"
Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "IsNonFlatDispatchName"
Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "IsPttEntryOrderCancelTrigger"
```

| Method | Hits | Definition Line | Status |
|--------|------|-----------------|--------|
| `TryFireFollowerBeDisarm` | 4 | line 861 (`private void TryFireFollowerBeDisarm`) | **CONFIRMED** |
| `IsNonFlatDispatchName` | 3 | line 1057 (`internal static bool IsNonFlatDispatchName`) | **CONFIRMED** |
| `IsPttEntryOrderCancelTrigger` | 4 | line 535 (`internal static bool IsPttEntryOrderCancelTrigger`) | **CONFIRMED** |

All three new methods: **CONFIRMED present in source**.

---

### Re-Verify Violations

| ID | Severity | Type | Location | Description |
|----|----------|------|----------|-------------|
| CYC-01 | P1 BLOCKING | CYC > 8 | `OnOrderUpdate` line 801 | CYC=10 (9 `if` gates). Engineer annotated CYC=7 — incorrect. One more branch must be extracted. Candidate: `if (TryCancelFollowerEntries...)` block or merge `TryHandleBracketDrag`+`TryHandleEntryDrag` paths into a single `TryHandleDrag` dispatcher to eliminate 2 `if` gates, targeting CYC=8. |
| CYC-02 | P1 BLOCKING | CYC > 8 | `TryFireFollowerBeDisarm` line 861 | CYC=9 (8 branches). Engineer annotated CYC=8 — incorrect. The 5-guard preamble (lines 863-867) plus the `foreach`+`if`+`if (isLeader)` = 8 branches, base+8=9. Fix: collapse the 5 null/state guards into a single `ValidateBeDisarmEvent(e)` predicate extraction, reducing to CYC=5. |

---

### Re-Verify Final Verdict

**DNA_FAIL**

- SCANS 1–6: ALL PASS
- New method existence: ALL CONFIRMED
- CYC violations: 2 blocking failures
  - `OnOrderUpdate` CYC=10 (limit=8) — extraction reduced from ~20 to 10, but still 2 branches over limit
  - `TryFireFollowerBeDisarm` CYC=9 (limit=8) — new method introduced but its own guard sequence is 1 branch over limit
- Engineer annotation errors: `OnOrderUpdate` comment says CYC=7 (actual=10); `TryFireFollowerBeDisarm` comment says CYC=8 (actual=9)

**Required fix (retry 2 of 3)**:
1. Extract the `TryFireFollowerBeDisarm` guard preamble (lines 863-867, 5 guards) to a predicate method `IsBeDisarmCandidate(OrderEventArgs e) -> bool`. This collapses CYC-02: TryFireFollowerBeDisarm CYC drops from 9 to 4 (foreach+if match+if isLeader+1 base).
2. For `OnOrderUpdate` CYC=10: two options — (A) merge `TryHandleBracketDrag`+`TryHandleEntryDrag` into a single `TryHandleDrag` method with internal dispatch (eliminates 1 `if`), reducing CYC to 9; then extract Mirror relay into `TryMirrorIfEnabled(e.Order, matchedRule.Value)` with no `if` at call site (caller always calls), reducing to CYC=8. (B) Extract `if ((CopyMode)... == Mirror)` + `MirrorOrderUpdate` into a `TryMirrorOrderUpdate(Order, CopyRule)` guard method — this leaves the `if` at the call site unless the method returns void unconditionally. Recommend option (A).

*End of Re-Verify Results.*

---

## FINAL VERIFY — Round 3 (2026-08-17)

**Verifier**: ptt-verifier (independent, READ-ONLY)
**Trigger**: Engineer applied second targeted CYC fix; all 5 new methods now present.

### 7-Scan Results

| Scan | Pattern | Result |
|------|---------|--------|
| SCAN-01 | lock\s*\( (non-comment lines) | **0 hits — PASS** |
| SCAN-02 | sync\s+void\s+\w+\( | **0 hits — PASS** |
| SCAN-03 | 	hrow\s+new\s+\w+Exception | **0 hits — PASS** |
| SCAN-04 | olatile\s+(double\|float) | **2 hits — both in comments only (lines 115, 203). Actual declarations: olatile string (line 116) and olatile int (line 204). PASS** |
| SCAN-05 | DIAG-Cancel | **0 hits — PASS** |
| SCAN-06 | Instrument equality spot-check | **Consistent FullName string comparisons where string fields used (lines 589, 1445, 2091, 2155). Direct object-ref comparison elsewhere (pattern correct for NT8 AddOn). PASS** |
| SCAN-07 | CYC counts (see table below) | **All within limits — PASS** |

### New Method Existence Check

| Method | Line | Present |
|--------|------|---------|
| TryFireFollowerBeDisarm | 866 | YES |
| IsBeDisarmCandidate | 533 | YES |
| TryHandleDrag | 932 | YES |
| IsNonFlatDispatchName | 1070 | YES |
| IsPttEntryOrderCancelTrigger | 546 | YES |

All 5 required methods confirmed present.

### CYC Table — Round 3

| Method | Decision Points | CYC | Limit | Verdict |
|--------|----------------|-----|-------|---------|
| OnOrderUpdate | 7 | **8** | ≤8 | **PASS (at-limit)** |
| TryFireFollowerBeDisarm | 4 | **5** | ≤8 | **PASS** |
| TryDispatchLeaderFlat | 6 | **7** | ≤8 | **PASS** |
| IsBeDisarmCandidate | 4 | **5** | ≤8 | **PASS** |
| TryHandleDrag | 2 | **3** | ≤8 | **PASS** |
| IsPttEntryOrderCancelTrigger | 3 | **4** | ≤4 | **PASS (at-limit)** |
| IsNonFlatDispatchName | 2 | **3** | ≤4 | **PASS** |

#### OnOrderUpdate Decision Point Detail
1. if (IsPttEntryOrderCancelTrigger(e.Order)) — line 823
2. if (!_isCopyEnabled) — line 827
3. if (matchedRule == null \|\| !matchedRule.Value.Enabled) — compound; 1 point (line 835)
4. if ((CopyMode)_copyModeValue == CopyMode.Mirror) — line 842
5. if (TryCancelFollowerEntries(...)) — line 847
6. if (TryDispatchLeaderFlat(...)) — line 850
7. if (TryHandleDrag(...)) — line 856
Total: 7 decision points + 1 = **CYC 8**

### Pre-existing Non-ASCII Status (NON-ASCII-01)

The non-ASCII bytes (em-dashes, arrows) at lines 202, 203, 493, 697, 1856, 1857 that caused the initial DNA_FAIL are a pre-existing condition not in scope for B75. They were noted in Round 1 as outside B75 ticket scope. B75 ticket work (CYC extraction) is complete and clean.

### Final Verdict

**DNA_PASS**

All B75-LaneA ticket-scope items resolved:
- 7/7 scans clean (SCAN-04 comment-only hits confirmed non-violations)
- All 5 new methods present at correct signatures
- OnOrderUpdate CYC = 8 (at-limit, PASS)
- All other new methods within limits
- No new DNA violations introduced by B75 changes

The pre-existing NON-ASCII-01 issue (not introduced by B75, not in B75 scope) is tracked separately and does not block B75 DNA_PASS.
