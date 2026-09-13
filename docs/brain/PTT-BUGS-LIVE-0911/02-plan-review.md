# PTT-BUGS-LIVE-0911 -- Lane A Plan Review
# BUG-1: PTT-Flatten Reverse Entry Guard
# ptt-plan-reviewer | 2026-09-11 | Phase 2 Output -- Cycle 6 (Director-Authorized)

---

## VERDICT: REVIEW_PASS

**Violation count: 0**

V-01 CLOSED. V-02 CLOSED. V-03 CLOSED. V-04 CLOSED.
All violations resolved. Plan (Revision 6) is internally consistent and fully compilable.

---

## V-04 Final Resolution Check -- Revision 6

| Question | Finding | Result |
|----------|---------|--------|
| Any remaining `acc.Name` in a prescriptive code snippet? | Zero. The single `acc.Name` occurrence (line 179) is in explanatory commentary prose: `"use acc.ToString(), not acc.Name"`. It is a negative-instruction clause explicitly directing the engineer away from `acc.Name`. It is not a call site specification. | PASS |
| §2.5 prose log template uses `acc.ToString()`? | Line 176: `[FLAT-GUARD] skip flat, already flat on {acc.ToString()}` | PASS |
| §2.5 C# Output snippet uses `acc.ToString()`? | Line 182: `"[FLAT-GUARD] skip flat, already flat on " + acc.ToString()` | PASS |
| §3.2 `FlatGuardLoop` body log line uses `acc.ToString()`? | Line 281: `"[FLAT-GUARD] skip flat, already flat on " + acc.ToString()` | PASS |
| §2.5 and §3.2 internally consistent? | Both sections specify `acc.ToString()` at every call site. The plan now unambiguously specifies exactly one form. | PASS |
| No new constraints on `TAccount` introduced by change? | `FlatGuardLoop<TAccount>` remains unconstrained. `acc.ToString()` is available on `object`. No CS1061. | PASS |
| No new violations introduced by the §2.5 update? | No lock, Monitor, throw, magic string, public constructor, immutability, NT8, or CYC violation introduced. | PASS |

**V-04 STATUS: CLOSED.**

---

## Closed Violation Summary

| # | Rule | Violation Summary | Closed In |
|---|------|-------------------|-----------|
| V-01 | SPEC COMPLETENESS (P0) | Test targeted `FlattenFollower` (Path A) instead of `FlatGuardLoop` (Path B new guard). | Cycle 2 |
| V-02 | SPEC COMPLETENESS (P0) | Dual implementation forms in plan: both inline guard and helper-extraction specified; §2.2/§2.3 showed inline form while §3.2 implied helper. Plan was ambiguous on which the engineer should implement. | Cycle 3 |
| V-03 | SPEC COMPLETENESS (P0) | `FlatGuardLoop<TAccount, TPosition>` had two generic parameters; `pos.Quantity` emitted CS1061 because `TPosition` was unconstrained. Fix: remove `TPosition`, type `findPosition` as `Func<TAccount, NinjaTrader.Cbi.Position?>` so `pos.Quantity` compiles against the concrete NT8 type. | Cycle 4 |
| V-04 | SPEC COMPLETENESS (P0) | §2.5 retained `acc.Name` in prose template and C# snippet; §3.2 correctly used `acc.ToString()`. Plan internally contradictory -- engineer reading §2.5 first would implement non-compilable form. | Cycle 6 |

---

## V-01, V-02, V-03 Regression Check -- Cycle 6

| Violation | Status | Evidence in Revision 6 |
|-----------|--------|------------------------|
| V-01 (test targets Path B guard, not Path A) | CLOSED -- no regression | §3.1 test invokes `FlatGuardLoop` directly via reflection; `FlattenFollower` (Path A) untouched. |
| V-02 (helper extraction canonical; §2.2/§2.3/§3.2 consistent on helper-call form) | CLOSED -- no regression | §2.2, §2.3, §3.2 all show `FlatGuardLoop(...)` call form; consistent. |
| V-03 (`TPosition` removed; `findPosition` typed as `Func<TAccount, Position?>`) | CLOSED -- no regression | §3.2 signature: `Func<TAccount, NinjaTrader.Cbi.Position?> findPosition`. `TPosition` absent throughout. |

---

## Full Spec Coverage Matrix

| Requirement | Addressed? | Plan Section |
|-------------|-----------|--------------|
| Eliminate reverse entry on flat follower via Path B (PttBus fan-out) | YES | §1.2 root cause, §2.1 summary, §2.2, §2.3, §3.2 |
| Guard applied at outer fan-out loop level (before FlattenOneAccount) | YES | §2.2, §2.3, §3.2 |
| Guard checks `pos == null` (no position entry) | YES | §2.4, §3.2 body |
| Guard checks `pos.Quantity == 0` (zero-qty retained entry) | YES | §2.4, §3.2 body |
| Log `[FLAT-GUARD]` on skip -- ASCII-only | YES | §2.5 (SCAN-04 PASS) |
| `FlatGuardLoop` is `private static` (no new public surface) | YES | §3.2, SCAN-05 |
| `FlatGuardLoop<TAccount>` -- one generic param, compilable | YES | §3.2, §5.2 |
| Unit test exercises Path B guard directly (no NT8 runtime) | YES | §3.1 |
| Test uses `[Fact]` (no Skip) | YES | §3.1 |
| Path A (`TryDispatchLeaderFlat` / `FlattenFollower`) unchanged | YES | §2.6, §5.3 |
| `PttFlatten.cs` read-only | YES | §0, §2.6, §5.1 |
| CYC ≤ 8 for all modified/new methods | YES | `Flatten(Instrument)` 3→4, `Flatten(Account,Instrument)` 5→6, `FlatGuardLoop` CYC=3 |
| No `lock()` introduced | YES | SCAN-01 |
| No `DateTime.Now` introduced | YES | SCAN-02 |
| No non-ASCII string literals | YES | SCAN-04 |
| No new public API surface | YES | SCAN-05 |
| No `?.Event -=` changes | YES | SCAN-07 |
| Seven-scan pre-assessment present | YES | §4 |
| Deferred work documented | YES | §7.1 (carry-forward), §7.2 (new items) |

---

## All Previously-Approved Sections -- Final Carry-Forward

Per Director mandate, all sections outside V-04 are not re-litigated.
LANE-SPLIT GATE PASS, §1 root cause analysis, §2.1 summary, §2.2/§2.3 helper-call form,
§2.4 guard logic, §2.6 out-of-scope list, §3.1 test spec, §3.2 helper signature and body,
§4 seven-scan checklist, §5 component map, §6 data flow, §7 deferred work, §8 out-of-scope,
§9 NT8 key facts, §10 summary -- all carry forward as APPROVED.

---

*ptt-plan-reviewer | PTT-BUGS-LIVE-0911 Lane A | Phase 2 Cycle 6 (Director-Authorized) | 2026-09-11*

**REVIEW_PASS -- 0 violations. All four violations (V-01, V-02, V-03, V-04) CLOSED. Plan (Revision 6) is approved. Phase 3 (ticket generation) is unlocked.**
