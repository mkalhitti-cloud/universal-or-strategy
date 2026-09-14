# B153 — Hardcoded 3-Leg Ceiling: Convergence Point Analysis

**Block:** B153  
**Status:** Pipeline complete (all 4 tickets implemented + verified)  
**Principle established:** JS-CONV-01 (Convergence Point Analysis)

---

## Problem Statement

`ResolveTargetCount`, `MoveStopToBreakEven`, `CaptureOtherLegTargetPrices`, and `IsPttQxTarget`
each contained a hardcoded `3` as an implicit leg-count ceiling. Four independent code paths
were broken. Initial ticket scoping surfaced 6 candidate fix sites (leaves). After applying
JS-CONV-01 convergence point analysis, 6 collapsed to 4.

---

## Call Path Analysis — 4 Paths, 4 Convergence Points

### Path 1: Quick Exit (QX)

```
QuickExitButton -> QuickExitAllButton
       |                  |
       v                  v
   ResolveTargetCount (PttQuickExit.cs)   <-- CONVERGENCE POINT
```

`ResolveTargetCount` is the single ancestor for both Quick and Quick ALL buttons.
Fixing the ceiling here covers all QX entry paths in one ticket.  
**Ticket:** PTT-B153-T1

---

### Path 2: Break-Even (BE)

```
BreakEvenButton -> BreakEvenAllButton
       |                  |
       v                  v
   MoveStopToBreakEven (CopyEngine.cs)    <-- CONVERGENCE POINT
```

`MoveStopToBreakEven` is the single ancestor for both BE and BE ALL buttons.
One change, all BE paths covered.  
**Ticket:** PTT-B153-T2

---

### Path 3: Stop Drag (all drag modes)

```
StopDragEvent (all modes: single / all / followers)
       |
       v
   CaptureOtherLegTargetPrices            <-- CONVERGENCE POINT (group)
   TryParseStopSuffix
   ResubmitCollateralLegs
   (tightly coupled via shared Dictionary<int,double> type boundary)
```

These 3 functions form a single logical unit sharing a `Dictionary<int,double>` state boundary.
They cannot be split into separate tickets without coordination cost exceeding the benefit.
Treated as one convergence point.  
**Ticket:** PTT-B153-T3

---

### Path 4: BE-after-QX (residual position)

```
QX partial exit -> remaining position in BE state
       |
       v
   IsPttQxTarget (PttBreakEven.cs)        <-- CONVERGENCE POINT
```

Post-QX BE logic routes through `IsPttQxTarget`. Fixing here covers the BE-after-QX
residual case without touching either the QX path or the primary BE path.  
**Ticket:** PTT-B153-T4

---

## Collapsed Ticket Count

| Candidate sites (leaves) | Convergence points (ancestors) | Reduction |
|--------------------------|-------------------------------|-----------|
| 6                        | 4                             | -2 tickets |

The Quick2t / QAll2t behavior (2 targets) was intentionally correct — not a ceiling bug.
Those were excluded during convergence analysis, which is why the leaf count was 6 but
the actual broken paths were only 4.

---

## JS-CONV-01 Canonical Example

This block is the canonical reference for JS-CONV-01 in all future agent prompts:

> "B153 found 4 independent broken paths (QX, BE, stop drag, BE-after-QX).
> Within each path, a single ancestor covered all child call sites.
> 6 leaf candidates collapsed to 4 tickets. Quick2t/QAll2t intentionally fixed at 2 targets -- not a ceiling.
> Always trace to the ancestor before issuing tickets."

See [`docs/standards/RULES_CATALOG.md`](../../standards/RULES_CATALOG.md) for full JS-CONV-01 rule text.

---

## JS-PUSH-01 Canonical Example

B153 / BUG-2 is also the canonical reference for **JS-PUSH-01 (Push over Pull)**:

The `fo == NULL` crash was a **pull bug**. `CopyEngine` stored the follower bracket reference
in `_followerSlateReady` (a boolean flag) rather than passing the bracket object directly to
the consumer at arm-time. The consumer had to query ("pull") whether the bracket existed via
a flag, then reconstruct the bracket reference separately. When the flag was not re-armed after
a partial QX/BE exit, the consumer pulled stale state (flag still `true`, object reference gone)
and crashed on NULL dereference.

The correct push pattern: the method that arms the bracket passes the bracket object directly
as a parameter to the consumer. The consumer never queries a shared flag — it receives the
live value at the moment it is needed.

> "BUG-2 fo=NULL was a pull bug. The bracket was gated behind a boolean flag
> (_followerSlateReady) that was never re-armed after partial exit. The fix is not to
> re-arm the flag better -- it is to eliminate the flag as a data-transfer mechanism
> and push the bracket object directly. See JS-PUSH-01."

See [`docs/standards/RULES_CATALOG.md`](../../standards/RULES_CATALOG.md) for full JS-PUSH-01 rule text.

---

## Files Modified

| File | Change |
|------|--------|
| `src/PropTraderTools/Features/PttQuickExit.cs` | T1: `ResolveTargetCount` ceiling removed |
| `src/PropTraderTools/CopyEngine.cs` | T2: `MoveStopToBreakEven` ceiling removed |
| `src/PropTraderTools/CopyEngine.cs` | T3: `CaptureOtherLegTargetPrices` + helpers ceiling removed |
| `src/PropTraderTools/Features/PttBreakEven.cs` | T4: `IsPttQxTarget` ceiling removed |

---

## Notes

- No shared ancestor exists across all 4 paths. The 4 convergence points are independent.
- The stop drag group (T3) is the only case where 3 functions were treated as one convergence
  point due to shared type boundary. This is the documented exception pattern for JS-CONV-01.
- All 4 tickets passed full 5-phase PTT pipeline verification.
