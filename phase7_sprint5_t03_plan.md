# Implementation Plan: T03 - ExecuteSmartDispatchEntry Refactoring
**Ticket**: T03 (Phase 7 Sprint 5)
**Status**: DRAFT (P3 Audit Pending)
**Target**: `src/V12_002.SIMA.Dispatch.cs`

## Observations
Forensic read of `src/V12_002.SIMA.Dispatch.cs` confirms a **partial-prior-execution state**: three of the four sub-helpers from Phase 6 already exist with the `Dispatch_` prefix:
- `Dispatch_ResolveFleetSnapshot` (lines 337–379)
- `Dispatch_BuildFollowerOrders` (lines 381–492)
- `Dispatch_PublishMarketBracketToPhoton` (lines 494–717)

**Only the Limit branch remains inlined** in the per-account loop's `else` block (lines 156–263), keeping the parent at CYC=29, LOC=183.

## Approach
Single surgical extraction: lift the inlined Limit branch (lines 156–263) into a new `Dispatch_PublishLimitEntryToPhoton` private helper. Collapse the parent residual to a thin orchestrator at CYC ≤19, LOC ≤80.

## Implementation Details

### Step 1 — Forensic Read
- Re-confirm inlined Limit branch dependencies.
- Verify `ocoId` is unused in the Limit branch (Forensic check: confirmed unused, will be dropped from signature).

### Step 2 — EXTRACT-GATE Proposal
| Sub-Helper Name | Status | Responsibility |
|---|---|---|
| `Dispatch_ResolveFleetSnapshot` | EXISTS | Fleet enumeration + snapshot. |
| `Dispatch_BuildFollowerOrders` | EXISTS | Per-follower order construction. |
| `Dispatch_PublishMarketBracketToPhoton` | EXISTS | Market-entry bracket publication. |
| `Dispatch_PublishLimitEntryToPhoton` | **NEW** | Limit-entry publication (entry-only). |

**Proposed Signature (DEVIATION-T3-A applied):**
```csharp
private void Dispatch_PublishLimitEntryToPhoton(
    Account acct,
    OrderAction action,
    PositionInfo fleetPos,
    Order entry,
    string fleetEntryName,
    string expectedKey,
    int followerQty,
    StringBuilder dispatchLog,
    ref bool syncPending,
    ref int reservedDelta,
    ref bool registeredForCleanup)
```

### Step 3 — Surgical Split
1. **Insert** `Dispatch_PublishLimitEntryToPhoton` after line 717.
2. **Delete** inlined block (lines 156–263) and replace with helper call.
3. **Preserve** Photon publish triple (sideband-write -> MemoryBarrier -> TryEnqueue) as contiguous statements.

### Step 4 — Verification Gates
1. CYC residual ≤ 19.
2. INV-2.2 (MemoryBarrier count = 2).
3. INV-2.4 (Interlocked.Increment precedes TryEnqueue).
4. Verbatim Print assertions (all 11 counts = 1).
5. Build clean & `deploy-sync.ps1` PASS.

## Next Step
Trigger **P3 Audit (Arena AI)** to verify DNA compliance before Stage P3.5 Annotation.
