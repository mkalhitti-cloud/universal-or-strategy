# B115 Plan Review

**Date**: 2026-08-27
**Reviewer**: ptt-plan-reviewer (Phase 2)
**Block**: B115 — Formalize DW-B119 + DW-B121 + DW-B122 Hotfixes
**Input**: docs/brain/B115/02-architecture-plan.md
**Verdict**: REVIEW_PASS

---

## Summary Table

| Section | Item | Status |
|---------|------|--------|
| A — Spec Traceability | A1: All three DW items in Fix Inventory | PASS |
| A — Spec Traceability | A2: Each fix maps to a ticket | PASS |
| A — Spec Traceability | A3: DW-B119 noted as already handled by B114-T1 | PASS |
| B — Fix Accuracy | B1: DW-B121 TTL AddSeconds(10) confirmed in source L165 | PASS |
| B — Fix Accuracy | B2: DW-B122 Accepted guard confirmed in source L2397-2398 | PASS |
| B — Fix Accuracy | B3: T_B113_01 stale AddSeconds(2)/AddSeconds(3) identified; update plan correct | PASS |
| C — Operator Precedence | C1: C# && binds tighter than || correctly stated | PASS |
| C — Operator Precedence | C2: Guard evaluation as (NotWorking && NotAccepted) \|\| ... correctly proven | PASS |
| C — Operator Precedence | C3: T3 verdict explicitly stated (INCLUDED) | PASS |
| C — Operator Precedence | C4: T3 clarity-only, CYC unchanged stated | PASS |
| C — Operator Precedence | C5: N/A (T3 included, not omitted) | N/A |
| D — Test Coverage | D1: T1 targets T_B113_01 TTL constant update | PASS |
| D — Test Coverage | D2: T2 new [Fact] for Accepted-state path | PASS |
| D — Test Coverage | D3: T2 uses _qxPendingFollowerCleanup seam, no live NT8 objects | PASS |
| D — Test Coverage | D4: T2 scenario clearly described (map armed, tChar paths, dict assertions) | PASS |
| E — JS Rules | E1: No lock() introduced | PASS |
| E — JS Rules | E2: No async void introduced | PASS |
| E — JS Rules | E3: No throw new XxxException | PASS |
| E — JS Rules | E4: No return null in hot paths | PASS |
| E — JS Rules | E5: CYC <= 8 for all modified methods | PASS |
| E — JS Rules | E6: xUnit [Fact] mandated | PASS |
| E — JS Rules | E7: ASCII-only string literals mandated | PASS |
| F — 7-Scan Pre-Assessment | F1: Section 8 (7-scan pre-assessment) present | PASS |
| F — 7-Scan Pre-Assessment | F2: All 7 scans addressed (SCAN-05 marked N/A with rationale) | PASS |
| G — Seam Analysis | G1: InternalsVisibleTo seam for _qxPendingFollowerCleanup explained | PASS |
| G — Seam Analysis | G2: NT8 sealed types limitation for direct TryCleanupReArmedAtmBracket call acknowledged | PASS |

---

## Detailed Findings

### No violations found.

All 26 items (A1–A3, B1–B3, C1–C4, D1–D4, E1–E7, F1–F2, G1–G2) return PASS.

---

## Evidence Trail

### B1 — DW-B121 TTL Verified in Source

`src/PropTraderTools/Features/PttGlobalQuickExit.cs` L163-165 (read 2026-08-27):

```csharp
CopyEngine.Instance?._qxPendingFollowerCleanup.TryAdd(
    acc.Name,
    (instr, DateTime.UtcNow.AddSeconds(10))
```

Plan §2 claims `AddSeconds(10)` at L165. Source confirms. PASS.

### B2 — DW-B122 Accepted Guard Verified in Source

`src/PropTraderTools/CopyEngine.cs` L2397-2398 (read 2026-08-27):

```csharp
e.Order.OrderState != OrderState.Working
&& e.Order.OrderState != OrderState.Accepted
```

Plan §2 claims two-line state guard present at L2397-2398. Source confirms. PASS.

### B3 — B113Tests.cs Stale Constants Verified via Grep

`src/PropTraderTools/Tests/B113Tests.cs` (grep result):

- L32: `DateTime.UtcNow.AddSeconds(2)` — matches plan claim (arrange constant).
- L42: `Assert.True(entry.Expiry <= DateTime.UtcNow.AddSeconds(3))` — matches plan claim (upper bound).

Plan §4 and §5 T1 specify update to `AddSeconds(10)` and `AddSeconds(11)` respectively. Correct. PASS.

### C — Operator Precedence Correctness Confirmed

Source `CopyEngine.cs` L2396-2408 guard (read 2026-08-27) confirms the unparenthesized form:

```
e.Order.OrderState != OrderState.Working
&& e.Order.OrderState != OrderState.Accepted
|| e.Order.Name == null
|| ...
```

Per ECMA-334 §12.4.2 (cited in plan §3), C# `&&` binds tighter than `||`, so natural evaluation
is `(NotWorking && NotAccepted) || (Name == null) || ...`. This matches the author's intent.
Plan §3 Correctness Verdict ("Parentheses NOT REQUIRED") is accurate.
Plan §3 Clarity Verdict (T3 INCLUDED) is rational and clearly stated. PASS.

### D3 — Seam Independence from Live NT8 Objects Confirmed

Plan §5 T2 and §6 explicitly list what cannot be tested (sealed `Account`, `OrderEventArgs`,
`Instrument`) and what CAN be tested via the `_qxPendingFollowerCleanup` seam (guard
sub-expression value, `TryAdd`/`TryRemove` dict behavior, TTL expiry simulation).
Test design is NT8-runtime-independent. PASS.

### E5 — CYC Compliance Confirmed by Annotation

Source `CopyEngine.cs` L2383: `// CYC=5: (1) outer guard, (2) foreach, (3) if found, (4) if shouldRemove.`
Source comment at the top of `TryCleanupReArmedAtmBracket` confirms CYC=5. Plan §2 and §8 SCAN-06
both cite CYC=5 unchanged. All methods remain <= 8. PASS.

---

## Final Verdict

**REVIEW_PASS**

No JS rule violations. No spec gaps. All 26 checklist items PASS. No violations to cite.

B115 is cleared for Phase 3 (ticket generation).
