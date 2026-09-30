# Final Review -- BWAVE-NEXT LaneBRepair-R2 (Round 2)

**Epic**: BWAVE-NEXT LaneBRepair-R2
**Phase**: 5 (Final Review)
**Reviewer**: ptt-plan-reviewer
**Date**: 2026-09-05
**Branch**: bwave-next-lane-b
**Verdict**: FINAL_PASS

---

## 1. Spec Coverage Matrix

| Requirement | Source Confirmation | Addressed? |
|-------------|-------------------|------------|
| R2-F1: `AbortDrainOnFill(string acctKey)` private helper added | Line 6656 (L3 verified) | YES |
| R2-F1: Filled branch in `OnOrderUpdate` calls `AbortDrainOnFill(e.Order.Account.Name)` | Line 1434 (L3 verified) | YES |
| R2-F1: Helper body iterates `payload.DrainedOrderIds`, removes each from `_drainOwnedOrderIds` via `TryRemove` | Lines 6658-6660 (L3 verified) | YES |
| R2-F1: `OnOrderUpdate` CYC unchanged (statement swap, no new branch) | Lizard CCN=12 pre+post R2 (git stash confirmed) | YES |
| R2-F2: `entryCandidates` predicate includes `|| o.Name == "Entry"` | Line 6535 (L3 + independent grep confirmed) | YES |
| R2-F2: Exact equality `== "Entry"` used (not `StartsWith`) | Line 6535 -- `o.Name == "Entry"` (grep confirmed) | YES |
| R2-F2: `StartsWith("PTT-Copy", StringComparison.Ordinal)` preserved | Line 6534 (L3 verified) | YES |
| xUnit [Fact] test for R2-F1 (AbortDrainOnFill structural reflection) | `BwaveNextLaneBTests.cs` -- `AbortDrainOnFill_MethodExists_WithCorrectSignature` (L3 confirmed) | YES |
| xUnit [Fact] test for R2-F2 (Entry predicate coverage) | `BwaveNextLaneBTests.cs` -- `FindFollowerEntryOrder_AcceptsEntryName_ForCloneMode` (L3 confirmed) | YES |
| All baseline fixes F1-F9 from prior rounds preserved | Baseline preservation section (L3 verified) | YES |

All 10 spec requirements addressed. **Coverage: 10/10 PASS**

---

## 2. Coherent System Check (R2-F1 + R2-F2 Interaction)

**Question**: Do R2-F1 (AbortDrainOnFill cleanup) and R2-F2 (Clone mode Entry predicate) form a
correct, non-conflicting system?

**Analysis**:

| Dimension | R2-F1 | R2-F2 | Interaction |
|-----------|-------|-------|-------------|
| Execution trigger | Entry order fills during active drain | New leader entry arrives; drain setup path executes | F2 runs first (setup); F1 runs later (fill-abort teardown) |
| Operation on `_drainOwnedOrderIds` | `TryRemove` (cleanup) | `TryAdd` via existing loop (setup) | Complementary: F2 adds, F1 removes. No conflict. |
| Operation on `_pendingDispatchDrains` | `TryRemove` (cleanup) | `TryAdd` (setup, existing line 6560) | Complementary: F2 adds, F1 removes. No conflict. |
| Dependency | F1 requires IDs to be present in `_drainOwnedOrderIds` to clean | F2 ensures "Entry"-named orders are included in the drain so their IDs enter `_drainOwnedOrderIds` | F2 is prerequisite for F1 to be relevant for Clone mode. Without F2, F1 still works for PTT-Copy mode. |
| Non-fill path unchanged | F1 not triggered on non-fill | F2 does not affect the drain completion path (`TryDrainWatchdog` / `OnDrainCancelAck`) | Non-fill drain lifecycle unchanged. |

**Verdict**: R2-F1 + R2-F2 together close the full lifecycle for Clone-mode "Entry" orders:
F2 allows them into the drain, F1 cleans them up if the entry fills before draining completes.
No new interaction bug created. System is coherent.

---

## 3. Cross-File Jane Street DNA Violations

Independent grep verification performed on committed source.

### CopyEngine.cs

| Rule | Scan | Finding | Verdict |
|------|------|---------|---------|
| JS-021 lock() ban | `grep -n "lock\s*(" src/PropTraderTools/CopyEngine.cs` | 22 matches — ALL comment lines (// prefix). Zero actual code `lock(` statements. | **PASS** |
| JS-033 async void | L3 SCAN-02 (method-decl regex) | 0 method declarations matching `async void`. | **PASS** |
| JS-002 return null | L3 SCAN-03 (ranged scan for AbortDrainOnFill + DrainThenDispatch) | 0 `return null` in either new/modified method. AbortDrainOnFill is `void` (physically impossible). | **PASS** |
| ASCII-only | L3 SCAN-04 (byte-level check) | Count = 0 non-ASCII bytes in file. | **PASS** |
| NT8 AddOnBase banned APIs | L3 SCAN-05 | 4 matches — ALL comment lines. Zero code calls. | **PASS** |
| CYC <= 8 (new code) | L3 SCAN-06 lizard | `AbortDrainOnFill` CCN=2 (new method, within budget). Pre-existing debt documented separately (Section K). | **PASS** |
| Build gate | L3 SCAN-07 | 0 errors. 1 pre-existing warning (B131Tests.cs xUnit2004, not in R2 scope). | **PASS** |

### BwaveNextLaneBTests.cs

| Rule | Finding | Verdict |
|------|---------|---------|
| xUnit only (no NUnit, no MSTest) | `grep` for `using NUnit`, `using Microsoft.VisualStudio.TestTools`, `[Test]`, `[TestCase]`, `[TestMethod]` — **zero matches**. `using Xunit;` confirmed (L3 Task 6). | **PASS** |
| [Fact] attribute on new tests | Both new tests (`AbortDrainOnFill_MethodExists_WithCorrectSignature`, `FindFollowerEntryOrder_AcceptsEntryName_ForCloneMode`) use `[Fact]`. | **PASS** |
| Existing tests preserved | All 6 pre-existing tests confirmed present (L3 Task 6). | **PASS** |

---

## 4. 7-Scan Aggregate Results (src/PropTraderTools/)

All 7 scans run independently by Layer 3 verifier.

| Scan ID | Scan | L3 Result | Basis |
|---------|------|-----------|-------|
| SCAN-01 | `lock()` ban | **PASS** | 22 comment-only matches, 0 actual code statements |
| SCAN-02 | `async void` ban | **PASS** | 0 method declarations matching strict regex |
| SCAN-03 | `return null` in new methods | **PASS** | 0 hits in AbortDrainOnFill (void) or DrainThenDispatch ranges |
| SCAN-04 | ASCII-only | **PASS** | Count = 0 non-ASCII bytes |
| SCAN-05 | NT8 banned APIs | **PASS** | 4 comment-only matches, 0 actual code calls |
| SCAN-06 | CYC <= 8 | **PASS** | AbortDrainOnFill=2 (new, within budget); pre-existing debt documented |
| SCAN-07 | Build gate | **PASS** | 0 errors, 0 errors in R2 scope |

**All 7 scans: PASS**

---

## 5. Missing Wiring Analysis

| Question | Finding | Verdict |
|----------|---------|---------|
| Does `AbortDrainOnFill` clean ALL `DrainedOrderIds`? | Method body: `foreach (var id in payload.DrainedOrderIds) _drainOwnedOrderIds.TryRemove(id, out _)` — iterates entire `DrainedOrderIds` list. | YES — full cleanup |
| What if `TryRemove` on `_pendingDispatchDrains` returns false? | `foreach` is inside the `if` guard — never executes if no drain found. No orphan created, no crash. | CORRECT — safe no-op |
| Does R2-F2 change affect the drain lifecycle on the non-fill path? | `entryCandidates` filter only controls drain _setup_ (which orders enter the drain). The drain completion path (`TryDrainWatchdog`, `OnDrainCancelAck`) is unchanged. Clone mode "Entry" orders that enter the drain still follow the same completion lifecycle. | NO NEW SIDE EFFECT |
| Is there a double-remove risk? | `TryRemove` on `ConcurrentDictionary` is idempotent — returns false if key absent, never throws. | SAFE |

No missing wiring found.

---

## 6. Pre-Existing CCN Debt Assessment

| Method | Pre-R2 CCN (git stash) | Post-R2 CCN | Delta | R2 Contribution |
|--------|----------------------|------------|-------|-----------------|
| `OnOrderUpdate` | 12 | 12 | 0 | None — statement swap adds no branches |
| `DrainThenDispatch` | 10 | 11 | +1 | R2-F2 lambda `||` counted by lizard as boolean branch |
| `AbortDrainOnFill` (new) | n/a | 2 | +2 | New method — within budget |

**Assessment**: The `OnOrderUpdate` CCN=12 and the pre-existing `DrainThenDispatch` CCN=10 both
pre-date this R2 pipeline. The +1 lizard delta in `DrainThenDispatch` from R2-F2 is attributable
to lizard's counting of lambda boolean operators (`||` inside `.Where()` predicate), which the
architect explicitly excluded from the method-body McCabe count (plan §4 note). Both pre-existing
methods exceed the CYC <=8 budget. This is **pre-existing technical debt**, not a new violation
introduced by R2. Documented in Section K as DW-NEXT-B-04.

**This debt does NOT constitute a FINAL_FAIL condition.** The new code (`AbortDrainOnFill`)
is fully within budget (CCN=2). No new violation was introduced.

---

## 7. NT8 API Compliance

| API | Status |
|-----|--------|
| `Account.Change()` | NOT USED — banned for AddOnBase. 0 code calls. |
| `AtmStrategyCreate()` | NOT USED — StrategyBase-only, banned. 0 code calls. |
| `AtmStrategyChangeStopTarget()` | NOT USED — StrategyBase-only, banned. 0 code calls. |
| `DateTime.Now` | NOT USED — `(long)(int)Environment.TickCount` pattern preserved at line 6545. |
| `Account.Cancel()` + `Account.CreateOrder()` + `Submit()` | Existing pattern unchanged. |

**NT8 API compliance: PASS**

---

## 8. Pending Item (Non-Blocking)

**F5 NinjaTrader 8 compile**: Marked pending manual attestation in completion report.
The engineer attested `ptt-sync-and-verify.ps1` passed (18/18 files, 0 MISMATCH). F5 compile
requires a local NT8 environment and is outside the automated verification scope.
This is acknowledged as a process step, not a FINAL_FAIL condition.

---

## Section K — Deferred Work Register

*Required for FINAL_PASS. All items tracked in `06-deferred-backlog.md`.*

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-NEXT-B-01 | Drain key is acct-only. Second instrument on same account overwrites first drain intent. Extend key to `acct.Name + "\|" + instrument.FullName` when multi-instrument trading is added. | P2 | Future dedicated epic | OPEN |
| DW-NEXT-B-02 | GTC/Day TIF and native-ATM Entry name not preserved in `SubmitEntryDirect` replacement. Carry original TIF + name in `PendingDispatchDrain` payload and use when creating replacement order. | P2 | Future dedicated epic | OPEN |
| DW-NEXT-B-03 | Test behavioral coverage gap. The R2 tests are structural (reflection-based). They do not exercise guard behavior — specifically: (a) `TryAdd` rejection preventing concurrent drain overwrite, (b) `ContainsKey` guard suppressing `TryReplaceOnAtmCancel` when drain is active. A future ticket should add behavioral tests using NT8 test-seam helpers or mock objects. | P2 | Future dedicated epic | OPEN |
| DW-NEXT-B-04 | Pre-existing CCN debt. `OnOrderUpdate` lizard CCN=12 (budget <=8), `DrainThenDispatch` lizard CCN=11 (budget <=8). Both methods pre-date this R2 pipeline (confirmed via independent git stash, L3 verifier). Require extraction to reduce complexity per Jane Street strict standard. The R2 block added zero new branches to `OnOrderUpdate`; +1 to `DrainThenDispatch` from R2-F2 lambda `||` (lizard counting artifact). | P2 | Dedicated complexity reduction epic | OPEN |

---

## Final Verdict

### FINAL_PASS

**Rationale**:

- **R2-F1 confirmed**: `AbortDrainOnFill(string acctKey)` added at line 6656. `OnOrderUpdate` Filled branch at line 1434 calls it. Body iterates `payload.DrainedOrderIds` and removes each ID from `_drainOwnedOrderIds`. L3 verifier independently confirmed all three sub-checks.
- **R2-F2 confirmed**: `entryCandidates` predicate at lines 6534-6535 includes `|| o.Name == "Entry"`. L3 verifier independently confirmed. Independent final grep: `o.Name == "Entry"` confirmed at line 6535.
- **All 7 scans pass**: SCAN-01 through SCAN-07 all PASS per L3 independent runs. Zero new violations introduced.
- **Pre-existing CCN debt documented**: `OnOrderUpdate` CCN=12 and `DrainThenDispatch` CCN=11 pre-date R2. Documented as DW-NEXT-B-04 (P2, future extraction epic). Not a FINAL_FAIL.
- **Section K written**: 4 deferred items (DW-NEXT-B-01 through DW-NEXT-B-04) documented.
- **06-deferred-backlog.md updated**: New block for LaneBRepair-R2 appended (Round 2).
- **xUnit-only confirmed**: `BwaveNextLaneBTests.cs` uses `using Xunit;` exclusively. Zero NUnit or MSTest.
- **Build: 0 errors**. Sync: 18/18 files OK. Coherent system with no missing wiring.

---

*Final review written: ptt-plan-reviewer | BWAVE-NEXT LaneBRepair-R2 Round 2 | Phase 5*
