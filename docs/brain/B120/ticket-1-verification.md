# B120-T1 Verification Report

**Ticket**: B120-T1
**Title**: DW-B129 -- Leader Fallback Flatten After B118 PTT-BE Cancel (PttGlobalQuickExit.cs)
**Verifier**: ptt-verifier (Phase 4b)
**Date**: 2026-08-28
**Source read**: `src/PropTraderTools/Features/PttGlobalQuickExit.cs` (READ-ONLY, independent)

---

## INDEPENDENT SCAN RESULTS (Layer 3 -- verifier runs)

### SCAN-H: JS-021 -- No lock()

Command: `Select-String -Path "src/PropTraderTools/Features/PttGlobalQuickExit.cs" -Pattern "lock\s*\("`

Result: **0 results** -- PASS

### SCAN-I: JS-033 -- No async void

Command: `Select-String -Path "src/PropTraderTools/Features/PttGlobalQuickExit.cs" -Pattern "async\s+void\s"`

Result: **0 results** -- PASS

### SCAN-K: ptt-sync-and-verify.ps1

Command: `powershell -File scripts\ptt-sync-and-verify.ps1`

Result:
```
=== PTT SYNC: src/PropTraderTools -> NT8 AddOns ===
  Copied:   0  |  In-sync: 16  |  Excluded: 46

=== PTT VERIFY: MD5 check every synced file ===
  OK  Features\PttGlobalQuickExit.cs
  (15 other files: OK)

=== SYNC + VERIFY: PASS (16 files confirmed) ===
```

**0 MISMATCH lines** -- PASS

---

## CYC COUNTS (independent manual count)

| Method | CYC | Decision Points | Limit | Verdict |
|--------|-----|-----------------|-------|---------|
| `Execute()` | 7 | acc-foreach(1), follower-guard-compound(2), pos-foreach(3), null/flat-guard-compound(4), DIAG-for-loop(5), NeedsLeaderFallbackFlatten-guard(6); base=1 | 8 | PASS |
| `ExecuteFollowers()` | 7 | rule-null-guard(1), follower-foreach(2), follower-null-continue(3), DIAG-pos-foreach(4), DIAG-_p-guard-compound(5), DIAG-for-loop(6); base=1 | 8 | PASS |
| `NeedsLeaderFallbackFlatten` | 2 | single && chain (3 predicates, 1 compound decision + 1 base); no explicit branches | 8 | PASS |

**Note on Execute() count**: The `ExecuteFollowers(...)` call at line 108 is a method call (not a branch), so it does not add to the CYC of Execute(). The follower block CYC now lives entirely inside ExecuteFollowers().

---

## CHECK A -- NeedsLeaderFallbackFlatten present

- **Signature found at line 216-219**:
  `internal static bool NeedsLeaderFallbackFlatten(int beCancelCount, int snapshotCount, int posQty)`
- **Body at line 221**: `return beCancelCount > 0 && snapshotCount == 0 && posQty > 0;`
- **Matches spec exactly** (04-tickets.md Step 2)
- **CYC=2**: single && compound expression, no branching constructs
- **Modifiers**: `internal static` -- correct per spec

**PASS**

---

## CHECK B -- acc.Flatten(pos.Instrument) on fallback path with [PTT-QX-FLATTEN] log

- **Guard at line 95**: `if (NeedsLeaderFallbackFlatten(_beCancelCount, targets.Count, pos.Quantity))`
- **Log at lines 97-101**:
  `"[PTT-QX-FLATTEN] leader fallback flatten: " + acc.Name + " " + pos.Instrument.FullName + " qty=" + pos.Quantity`
  - Log prefix: [PTT-QX-FLATTEN] -- correct
  - Log includes: acc.Name -- PASS
  - Log includes: pos.Instrument.FullName -- PASS
  - Log includes: pos.Quantity -- PASS
- **acc.Flatten(pos.Instrument) at line 103** -- correct call
- **Called inside the NeedsLeaderFallbackFlatten if-block** -- correct

**PASS**

---

## CHECK C -- continue present after acc.Flatten; ExecuteOne NOT called on fallback path

- **Line 104**: `continue; // skip ExecuteOne -- flatten handles the exit`
- `continue` is the statement immediately after `acc.Flatten(pos.Instrument)` at line 103
- Control flow: when flatten guard fires, `continue` skips to next `pos` iteration
- `ExecuteOne` at line 106 is outside the `if` block -- only reached when guard returns false

**PASS**

---

## CHECK D -- ExecuteFollowers() extracted as private void

- **Method declaration at lines 121-126**:
  `private void ExecuteFollowers(Account acc, Position pos, System.Collections.Generic.List<(double Price, int Qty)> targets, (int t1, int t2) ticks, double leaderStop)`
- **Signature matches spec exactly** (04-tickets.md Step 1)
- **Call site at line 108** of Execute(): `ExecuteFollowers(acc, pos, targets, ticks, leaderStop);`
- **CopyEngine.Instance captured internally at line 128** (not a parameter -- per spec)
- **Contents confirmed**: rule guard (line 129-130), follower foreach (line 131), follower null continue (lines 133-134), _fBeCancelCount/WaitForPttBeCancelled/SnapshotTargetOrders (lines 136-138), DIAG block (lines 143-176), ResolveFollowerTargets (lines 180-185), follower log (lines 186-195), follower ExecuteOne (lines 197-205)

**PASS**

---

## CHECK E -- Execute() CYC <= 8

Independent McCabe count of Execute() (lines 36-111):

| # | Line | Construct |
|---|------|-----------|
| 1 | 43 | `foreach (Account acc in Account.All)` -- loop |
| 2 | 45 | `if (engine != null && engine.IsFollowerAccount(acc))` -- compound if |
| 3 | 47 | `foreach (Position pos in acc.Positions)` -- loop |
| 4 | 49 | `if (pos == null \|\| pos.Quantity == 0)` -- compound if |
| 5 | 81 | `for (int _i = 0; _i < targets.Count; _i++)` -- DIAG for-loop |
| 6 | 95 | `if (NeedsLeaderFallbackFlatten(...))` -- new B120 guard |

CYC = 6 decision points + 1 base = **7**. Well within limit of 8.

**PASS (CYC=7)**

---

## CHECK F -- Follower path unchanged; _fBeCancelCount separate; NeedsLeaderFallbackFlatten NOT called on follower path

- **_fBeCancelCount** declared at line 136 inside ExecuteFollowers(): `int _fBeCancelCount = CancelPttBeOrders(follower, pos.Instrument);`
- **_fBeCancelCount is distinct** from _beCancelCount (line 52 in Execute()) -- separate local variables in separate methods
- **NeedsLeaderFallbackFlatten is NOT called anywhere in ExecuteFollowers()** -- confirmed by full source read (lines 121-207): no reference to NeedsLeaderFallbackFlatten
- **Follower uses ResolveFollowerTargets** (line 180-185) for empty-snapshot handling, unchanged from pre-B120

**PASS**

---

## CHECK G -- Normal QX path unchanged; ExecuteOne called when NeedsLeaderFallbackFlatten returns false

- **Line 106**: `ExecuteOne(acc, pos.Instrument, ticks.t1, targets);`
- This call is reached only when NeedsLeaderFallbackFlatten returns false (the `if` block at line 95 is skipped)
- When beCancelCount=0 OR snapshotCount>0 OR posQty==0 -- guard returns false, execution falls through to ExecuteOne
- No code has been inserted between the DIAG block and ExecuteOne that would alter normal path

**PASS**

---

## CHECK J -- B120Tests.cs: 3 xUnit [Fact] tests

File read via `Get-Content src/PropTraderTools/Tests/B120Tests.cs`.

**Framework**: `using Xunit;` -- xUnit only. No NUnit. No MSTest. -- PASS

**Test 1 -- True path** (matches spec exactly):
- Method name: `Test_NeedsLeaderFallbackFlatten_True_WhenBECancelledAndSnapshotEmpty`
- `[Fact]` attribute: present
- Call: `PttGlobalQuickExit.NeedsLeaderFallbackFlatten(1, 0, 7)` -- (beCancelCount=1, snapshotCount=0, posQty=7)
- Assert: `Assert.True(...)` -- PASS

**Test 2 -- False: no BE cancel** (matches spec exactly):
- Method name: `Test_NeedsLeaderFallbackFlatten_False_WhenBECancelCountZero`
- `[Fact]` attribute: present
- Call: `PttGlobalQuickExit.NeedsLeaderFallbackFlatten(0, 0, 7)` -- (beCancelCount=0)
- Assert: `Assert.False(...)` -- PASS

**Test 3 -- False: snapshot has targets** (matches spec exactly):
- Method name: `Test_NeedsLeaderFallbackFlatten_False_WhenSnapshotHasTargets`
- `[Fact]` attribute: present
- Call: `PttGlobalQuickExit.NeedsLeaderFallbackFlatten(1, 3, 7)` -- (snapshotCount=3)
- Assert: `Assert.False(...)` -- PASS

All 3 test method names match 04-tickets.md specification verbatim.

**PASS**

---

## COMPARISON vs ENGINEER LAYER 2 SCAN REPORT

Engineer's ticket-1-completion.md reported:

| Scan | Engineer Layer 2 | Verifier Layer 3 | Discrepancy? |
|------|-----------------|-----------------|--------------|
| SCAN-01 JS-021 lock() | 0 results -- PASS | 0 results -- PASS | None |
| SCAN-02 JS-033 async void | 0 code results (1 comment-only in header) | 0 results -- PASS | **Note below** |
| SCAN-03 JS-066 CYC | Execute()=7, ExecuteFollowers()=7, NeedsLeaderFallbackFlatten=2 | Same counts -- PASS | None |
| SCAN-04 JS-001 no throw | 0 results | Not separately scanned (source read confirms no `throw new`) | None |
| SCAN-05 JS-002 no null return | bool return confirmed | Source confirms bool return at line 221 | None |
| SCAN-06 ASCII-only | 0 non-ASCII | Source header-only comment pattern confirmed ASCII | None |
| SCAN-07 NT8 API | acc.Flatten at line 103 | Confirmed at line 103 | None |
| SCAN-K sync verify | 0 MISMATCH (16 files) | 0 MISMATCH (16 files -- independent run) | None |

**SCAN-02 Note**: Engineer reported "1 comment-only match in header rule list" for `async void` pattern.
My independent scan returned **0 results** with pattern `async\s+void\s`. The engineer's match was
likely from the file header comment line 5: `// JS-033 (no async void)`. This comment is not a code
violation -- it is a documentation comment referencing the rule. My scan with `async\s+void\s` found
0 matches because the comment text contains `async void` without a trailing space in the relevant
context. **No discrepancy in substance -- 0 code violations in both cases.** PASS.

No material discrepancies between engineer Layer 2 and verifier Layer 3.

---

## DNA RULES AUDIT

| Rule | Check | Source Evidence | Verdict |
|------|-------|-----------------|---------|
| JS-021 no lock() | Scan-H: 0 results | Confirmed | PASS |
| JS-033 no async void | Scan-I: 0 results | Confirmed | PASS |
| JS-066 CYC <= 8 | Execute()=7, ExecuteFollowers()=7, NeedsLeaderFallbackFlatten=2 | All <= 8 | PASS |
| JS-001 no throw | No `throw new` in new code | Source read lines 95-108, 216-222 | PASS |
| JS-002 no null return | NeedsLeaderFallbackFlatten returns bool; ExecuteFollowers returns void | Lines 216, 121 | PASS |
| JS-010 no public constructors on signals | No new signal structs added | N/A | N/A |
| JS-023 no Mutex/Monitor | Not present | Source read | PASS |
| NT8 -- no sealed on TradeCopierWindow | Class is PttGlobalQuickExit (internal sealed) | Not TradeCopierWindow -- no violation | PASS |
| NT8 -- no FontFamily | Scan: 0 results (not in scope file) | Confirmed | PASS |
| NT8 -- no hex color | Scan: 0 results (not in scope file) | Confirmed | PASS |
| NT8 -- no DateTime.Now (non-UTC) | Line 291: `DateTime.UtcNow.AddSeconds(10)` | UtcNow used -- PASS | PASS |
| ASCII-only | All new string literals checked | [PTT-QX-FLATTEN], qty=, acc.Name, pos.Instrument.FullName, pos.Quantity -- all ASCII | PASS |

---

## ACCEPTANCE CRITERIA SUMMARY

| Criterion | Source Evidence | Verdict |
|-----------|-----------------|---------|
| A. NeedsLeaderFallbackFlatten present, CYC=2, internal static | Line 216-221 | PASS |
| B. acc.Flatten on fallback path with [PTT-QX-FLATTEN] log (acc.Name, FullName, qty) | Lines 95-103 | PASS |
| C. continue after acc.Flatten; ExecuteOne skipped | Line 104 | PASS |
| D. ExecuteFollowers() extracted private void; Execute() calls it | Lines 121, 108 | PASS |
| E. Execute() CYC <= 8 (CYC=7) | Manual count | PASS |
| F. _fBeCancelCount separate in ExecuteFollowers(); NeedsLeaderFallbackFlatten not on follower path | Line 136; lines 121-207 search | PASS |
| G. Normal QX path unchanged; ExecuteOne called when guard false | Line 106 | PASS |
| H. No lock() | SCAN-H: 0 results | PASS |
| I. No async void | SCAN-I: 0 results | PASS |
| J. B120Tests.cs: xUnit, 3 [Fact] tests, correct names and assertions | Source confirmed | PASS |
| K. ptt-sync-and-verify.ps1: 0 MISMATCH (16 files) | Independent run | PASS |

---

## VERDICT

**VERIFY_PASS**

All 11 acceptance criteria (A through K) independently verified against source.
All DNA rule scans clean. Sync verified 0 MISMATCH. Test file confirmed xUnit with 3 [Fact] methods matching spec exactly.
B120-T1 implementation is correct, complete, and compliant.

---

*End of B120-T1 Verification Report*