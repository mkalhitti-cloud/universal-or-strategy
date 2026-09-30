# DW-B91 Plan Review

**Epic**: DW-B91 -- Entry dedup survivor guard + flat-follower re-entry guard  
**Phase**: 2 (Plan Review)  
**Status**: REVIEW_PASS  
**Date**: 2026-08-24  
**Reviewer**: ptt-plan-reviewer  

---

## Inputs Read

| File | Range | Purpose |
|------|-------|---------|
| `docs/brain/DW-B91/02-architecture-plan.md` | full | Plan under review |
| `docs/standards/jane-street/RULES_CATALOG.md` | L1-120 | JS-001, JS-002, JS-021 rule text |
| `src/PropTraderTools/CopyEngine.cs` | L1380-1440 | DispatchCopy live source (Gate 5 hook site) |
| `src/PropTraderTools/CopyEngine.cs` | L1882-1910 | TryDispatchLeaderFlat live source (CYC=8 confirmed) |
| `src/PropTraderTools/CopyEngine.cs` | L2468-2494 | IsDedup + EvictDedup live source (eviction hook site) |
| `src/PropTraderTools/CopyEngine.cs` | L120-135 | Existing ConcurrentDictionary field block |

---

## Check Results

| # | Check | Status | Evidence |
|---|-------|--------|----------|
| 1 | DW-B91-A addressed with `ConcurrentDictionary` field `_entryDispatchedOrders` | **PASS** | Plan §3 Fix A declares `private readonly ConcurrentDictionary<string, byte> _entryDispatchedOrders`. Keyed by orderId, byte value as presence-only marker. Matches pattern of existing `_dedupCache` at L128. |
| 2 | `IsEntryDispatched` helper exists in method signatures with CYC=2 | **PASS** | Plan §3 defines `private bool IsEntryDispatched(string orderId)`. CYC breakdown explicit: 1 base + 1 `if (ContainsKey)` = CYC=2. Plan §4 method table confirms. |
| 3 | `DispatchCopy` CYC stated as <=8 after fix | **PASS** | Plan §3 states compound `\|\|` in single `if` = one McCabe point. "CYC=8 unchanged." Plan §4 table confirms DispatchCopy CYC=8 MODIFIED. Source L1381 confirms current method exists and is the target. |
| 4 | `EvictDedup` gains `_entryDispatchedOrders` eviction | **PASS** | Plan §3 Fix A shows modified EvictDedup body with `_entryDispatchedOrders.TryRemove(orderId, out _)`. Plan §7 change inventory confirms hook site "After L2493". Source L2493 confirms `_dedupCache.TryRemove` exists as the co-location anchor. |
| 5 | DW-B91-B addressed with `FlattenFollower` helper | **PASS** | Plan §3 Fix B defines `private static void FlattenFollower(Account acc, Instrument instrument, Func<Account, Instrument, bool> hasOpenPosition, Action<Account, Instrument> flattenOne)`. Problem and fix both described in plan §1 and §2. |
| 6 | `TryDispatchLeaderFlat` CYC stated as <=8 after fix | **PASS** | Plan §3 states "Net change: -1 branch. CYC = 8->7." Plan §4 confirms CYC=7 MODIFIED. Source L1882 comment confirms current CYC=8 as the baseline. |
| 7 | `FlattenFollower` absorbs both null guard and `hasOpenPosition` guard | **PASS** | Plan §3 Fix B body shows `if (acc == null) return;` (null guard) and `if (!hasOpenPosition(acc, instrument)) return;` (open-position guard). CYC=3 breakdown accounts for both. |
| 8 | Fix only touches `src/PropTraderTools/CopyEngine.cs` for production code | **PASS** | Plan §7 Files Changed: production = CopyEngine.cs only. Test file CopyEngineB91Tests.cs is additive (required by Check 13). Plan §7 states "No other files touched. Zero cross-contamination." |
| 9 | JS-021 compliance (no `lock()`) in all new/modified methods | **PASS** | Plan §6 compliance table: all methods use ConcurrentDictionary atomic ops (ContainsKey, TryAdd, TryRemove). Plan §6 notes: "Grep for `lock(` in modified lines: zero results." No `lock()` in any code snippet in the plan. |
| 10 | JS-001 compliance (no `throw`) in all new/modified methods | **PASS** | Plan §6 compliance table column "JS-001 (no throw)": checkmark for all six rows. No `throw` statement appears in any code snippet in the plan. New methods are `void` or `bool` return -- no exception path required. |
| 11 | ASCII-only constraint called out for new string literals | **PASS** | Plan §6 notes: "ASCII-only identifiers: `_entryDispatchedOrders`, `IsEntryDispatched`, `FlattenFollower`, `orderId` -- all 7-bit ASCII." No non-ASCII characters in any plan code block. |
| 12 | All 6 xUnit `[Fact]` tests (T_B91A_01-03, T_B91B_01-03) present | **PASS** | Plan §5 lists all 6 tests with IDs, `[Fact]` names, and assertion descriptions. T_B91A_01/02/03 for Fix A, T_B91B_01/02/03 for Fix B. |
| 13 | Test file is `CopyEngineB91Tests.cs` (xUnit, NOT NUnit/MSTest) | **PASS** | Plan §5 header: "`src/PropTraderTools/Tests/CopyEngineB91Tests.cs` (NEW -- xUnit only, no NUnit, no MSTest)." Test structure shows `using Xunit;` and `[Fact]` only. |
| 14 | Deferred backlog note present (DW-B89 items still open) | **PASS** | Plan §8 lists all 12 open items (DW-B89-DEFERRED-01..06, DW-B42-01..03, DW-PTT-BE-FIX-01..03) with Status=Open. Explicitly states "DW-B91 does not close any DW-B89 deferred items." |

---

## Violations

**None.** All 14 checks passed. No JS-XXX rule violations found in the plan.

---

## Spec Coverage Matrix

| Requirement | Addressed? | Plan Section |
|-------------|------------|-------------|
| DW-B91-A: double dispatch on re-submitted orderId after EvictDedup | YES | §1, §2, §3 Fix A |
| Per-orderId dispatch guard survives EvictDedup terminal-state eviction | YES | §3 Fix A (`_entryDispatchedOrders`) |
| DispatchCopy CYC must not increase beyond 8 | YES | §3 Fix A (compound `\|\|` = one branch) |
| EvictDedup must co-evict new guard with `_dedupCache` | YES | §3 Fix A (modified EvictDedup body) |
| DW-B91-B: spurious flattenOne on already-flat follower | YES | §1, §2, §3 Fix B |
| FlattenFollower absorbs null guard + open-position guard | YES | §3 Fix B (CYC=3 body) |
| TryDispatchLeaderFlat CYC must not exceed 8 | YES | §3 Fix B (CYC=7 after extraction) |
| Production changes confined to CopyEngine.cs | YES | §7 Files Changed |
| 6 xUnit [Fact] tests covering both fixes | YES | §5 Test Plan |
| JS-021 (no lock()) across all changes | YES | §6 Compliance Checklist |
| JS-001 (no throw) across all changes | YES | §6 Compliance Checklist |
| ASCII-only string literals | YES | §6 Compliance Notes |
| DW-B89 deferred items acknowledged as still open | YES | §8 Deferred Items Addressed |

---

## Summary

**Verdict: REVIEW_PASS**

The DW-B91 architecture plan is complete, internally consistent, and compliant with all Jane Street
DNA rules checked. Specific observations:

1. **Root cause analysis is correct**: The plan correctly traces the double-dispatch bug to the
   gap between `EvictDedup` (terminal-state eviction) and a second Submitted event — confirmed
   against the live source at L2490 and L1398.

2. **CYC discipline is maintained**: Both fixes are designed to leave their respective methods at
   or below CYC=8. The compound `||` in DispatchCopy is a standard single-branch construct.
   The extraction of `FlattenFollower` reduces `TryDispatchLeaderFlat` from CYC=8 to CYC=7.

3. **Lock-free discipline is maintained**: All new state (`_entryDispatchedOrders`) uses
   `ConcurrentDictionary<string, byte>` with atomic `ContainsKey`/`TryAdd`/`TryRemove` — the same
   pattern as the existing `_dedupCache` at L128. No `lock()` anywhere.

4. **Test plan is complete**: 6 `[Fact]` tests cover both fixes with boundary conditions
   (null account, no open position, independent tracking, eviction path). xUnit-only. No NUnit/MSTest.

5. **Scope discipline**: Production changes limited to `CopyEngine.cs`. No cross-contamination.
   DW-B89 deferred items explicitly acknowledged.

Proceed to Phase 3 (ticket generation).
