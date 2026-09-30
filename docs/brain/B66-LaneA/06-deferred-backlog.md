# B66-LaneA Deferred Backlog

**Block**: B66-LaneA
**Written by**: ptt-plan-reviewer (Ph5)
**Date**: 2026-08-13

---

## Closed This Block

### DW-B66-01 — CancelQxBrackets missed ATM bracket order names

**Priority**: P0 (live trading correctness)
**Status**: CLOSED — B66-LaneA Ticket-1
**Commit**: d6002b95

**Resolution**: Two new `internal static` helpers were added to `CopyEngine.cs` immediately
before `CancelQxBrackets`:

- `IsAtmBracketName(string name)` (lines 423-428, CYC=1, expression body) — exact equality
  match for standard NT8 ATM bracket order names `"Stop1"`, `"Stop2"`, `"Target1"`,
  `"Target2"`. Authority: `NT8_FULL_REFERENCE.md` line 1631:
  > "The order name such as 'Stop1' or 'Target2'"

- `IsQxCancelCandidate(Order o)` (lines 430-441, CYC=5) — null-guards then delegates:
  (1) null guard, (2) `IsAtmBracketName`, (3) `StartsWith("PTT-QX-", Ordinal)`,
  (4) `StartsWith("PTT-BE-", Ordinal)`, (5) default `return false`.

`CancelQxBrackets` line 458 predicate was changed from:
```csharp
if (o.Name != null && o.Name.StartsWith("PTT-QX-"))  // OLD — missed ATM bracket names
```
to:
```csharp
if (IsQxCancelCandidate(o))                           // B66 — widened via helper
```

The CancelQxBrackets CYC comment was also corrected from the inaccurate "CYC=4" to the correct
"CYC=6" with all 6 branches enumerated.

7 xUnit [Fact] tests (T_B66_01..T_B66_07 in `CopyEngineTests.cs` lines 3287-3348) exercise all
positive branches and the default false path. All 7 scans (S1-S7) returned 0 violations.

**Root cause**: The 2026-08-13 ~07:50 UTC production incident: double-bracket orders remained
live on 4 follower accounts after Quick Exit with an active ATM strategy. The old predicate
`StartsWith("PTT-QX-")` did not match ATM bracket order names.

---

## New Deferred Items — B66

### DW-B66-BE-01 — CancelQxBrackets now cancels PTT-BE-Stop during Quick Exit

**Priority**: P1
**Target block**: B67+ (Director confirmation required)
**Status**: OPEN — NEW

**Description**: The widened predicate in `IsQxCancelCandidate` (branch 4,
`StartsWith("PTT-BE-", StringComparison.Ordinal)`) means that pressing Quick Exit will now
cancel any live `PTT-BE-Stop`, `PTT-BE-Stop-{i+1}`, or `PTT-BE-Target-{i+1}` orders on the
account for the instrument. This ensures a clean position exit but removes breakeven stop
protection at the moment of Quick Exit.

**PTT-BE-* order name variants in production** (per NT8-VERIFY-03 in ticket-1-verification.md):

| Variant | Source |
|---------|--------|
| `"PTT-BE-Stop"` | PttBreakEven.cs:217, :374; CopyEngine.cs:496 |
| `"PTT-BE-Stop-1"`, `"PTT-BE-Stop-2"`, ... | PttBreakEven.cs:407 |
| `"PTT-BE-Target-1"`, `"PTT-BE-Target-2"`, ... | PttBreakEven.cs:446 |
| `"PTT-BE-XXXX-00001-0"` (OCO group ID) | PttBreakEven.cs:328 |

All variants are correctly covered by `StartsWith("PTT-BE-")`.

**Action required**: Director must confirm that cancelling PTT-BE-* orders on Quick Exit is the
intended behavior. If NOT intended, branch (4) should be removed from `IsQxCancelCandidate`,
retaining only: (1) null guard, (2) `IsAtmBracketName`, (3) `PTT-QX-` prefix.

---

## Carry-Forward Items (OPEN, unchanged from B65)

### DW-B64-01 — B62 drag sync not working (HandleEntryChange not firing)

**Priority**: P0
**Target block**: B67+ (next available)
**Status**: OPEN — no change in B66.

**Description**: From Director live testing after B62 deployment: `HandleEntryChange` is not
firing when a stop-limit entry is dragged on the leader account. The B62 implementation added
Gate C in `OnOrderUpdate` to detect entry price changes and call `HandleEntryChange` to propagate
drags to follower `PTT-Copy` orders. The mechanism is present in source but not activating in
live testing.

**Investigation starting point**: Verify Gate C conditions in `OnOrderUpdate` — check whether the
price-change detection condition (`limitPrice != storedPrice`) is being evaluated correctly for
the order type being dragged. Verify `_dedupCache` has an entry for the order being dragged
(otherwise Gate C short-circuits). Check `CopyEngineTests.cs` T_B62_04 for the expected price
comparison logic.

---

### DW-B63-01 — Spurious PTT-Copy bracket orders on Sim102 after ATM fill

**Priority**: P1
**Target block**: B67+ (next available)
**Status**: OPEN — no change in B66.

**Description**: After an ATM fill on the leader account, spurious PTT-Copy bracket orders appear
on the follower Sim102 account. These orders are not part of the intended copy cascade.

**Investigation starting point**: Review `DispatchCopy` Gate 0.5 (`IsExitSignalName` check) and
Gate A (`IsFollowerAccount` check) for the bracket order dispatch path. Verify `IsWorkingBracket`
(B63 T1) is correctly widened to `Accepted` state so bracket orders are detected before they
transition to Working. Check the `_dedupCache` for double-dispatch via ConcurrentDictionary
TryAdd semantics vs. the prior timestamp dedup.

---

### DW-B58-01 — SnapshotTargetsPublic hardcoded order-name prefixes

**Priority**: P2
**Target block**: future
**Status**: OPEN — no change in B66.

**Description**: `SnapshotTargetsPublic` checks for hardcoded prefixes `PTT-QX-T` and `PTT-TGT-`.
Future blocks adding new PTT-prefixed target order names must update this method or the snapshot
will miss them.

---

### DW-B58-02 — GlobalBe non-atomic lazy init

**Priority**: P2
**Target block**: future
**Status**: OPEN — no change in B66.

**Description**: `GlobalBe` lazy init uses `if (_globalBe == null) _globalBe = new ...`
(non-atomic). Currently safe — both callers (TradeCopierPanel, TradeCopierWindow) access
exclusively from the WPF UI thread. If a future block introduces a non-UI-thread caller,
`Interlocked.CompareExchange` will be required.

---

### DW-B58-03 — RelayBe does not forward OcoGroup from BeEventArgs

**Priority**: P2
**Target block**: future
**Status**: OPEN — no change in B66.

**Description**: `RelayBe` does not forward `OcoGroup` from `BeEventArgs` to `SubmitBeStop`.
`SubmitBeStop` generates its own `OcoId` via `NextQxOcoId()`. If a future block requires
correlated OcoId fan-out across accounts for a single BE event, a new `SubmitBeStop` overload
accepting an explicit `OcoGroup` will be needed.

---

### DW-B54-01 — ATM auto-inject (blocked)

**Priority**: P1
**Target block**: future (blocked — requires StrategyBase-level NT8 API unavailable in AddOnBase)
**Status**: OPEN — blocked. No change in B66.

**Description**: `AtmStrategyCreate()` is confirmed `StrategyBase`-only per
`NT8_FULL_REFERENCE.md`. The `AddOnBase` (`TradeCopierAddOn`) cannot call this API. A companion
`StrategyBase` add-in would be required. Deferred indefinitely pending Director architectural
decision.

---

### PRE-EXISTING-01 — Non-ASCII characters at CopyEngine.cs lines 398, 499

**Priority**: P2
**Status**: OPEN — pre-existing. Not introduced by B66.

**Description**: Em-dash Unicode characters in B56 BUILD-FIX stub markers (comment lines only).
Line numbers 398 and 499 are unchanged from B65 baseline (B66 inserts `IsAtmBracketName` +
`IsQxCancelCandidate` at lines 423-441; no shift to lines above 420).

---

### PRE-EXISTING-02 — Non-ASCII characters at CopyEngine.cs lines 1401-1402

**Priority**: P2
**Status**: OPEN — pre-existing. Not introduced by B66.

**Description**: Unicode arrow characters in exit-order direction comments. Line numbers shifted
to **approximately 1415-1416** due to B66 inserting ~21 lines for `IsAtmBracketName` +
`IsQxCancelCandidate` (lines 423-441 + surrounding blank lines + updated CancelQxBrackets comment).
Same physical comment blocks; no new non-ASCII introduced.

**Note**: Line numbers for this item should be re-confirmed in the next block that touches
CopyEngine.cs; the delta from B65 insertion (+~21 lines) is an estimate.

---

### PRE-EXISTING-03 — deploy-sync.ps1 archived; PropTraderTools sync is manual

**Priority**: P2
**Status**: OPEN — pre-existing infrastructure state. No change in B66.

**Description**: `deploy-sync.ps1` is archived to `archive/v12-reference/scripts/deploy-sync.ps1`
and maps V12_002 strategy files, not PropTraderTools AddOn files. Manual SHA-256 copy +
`verify_links.ps1 -Fix` is the current PropTraderTools deploy workflow.

---

## Summary Table

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-B66-01 | CancelQxBrackets missed ATM bracket names (Stop1/Stop2/Target1/Target2) | P0 | B66 | **CLOSED** |
| DW-B66-BE-01 | CancelQxBrackets now cancels PTT-BE-Stop during Quick Exit -- Director confirm | P1 | B67+ | OPEN |
| DW-B64-01 | B62 drag sync -- HandleEntryChange not firing | P0 | B67+ | OPEN |
| DW-B63-01 | Spurious PTT-Copy bracket orders on Sim102 after ATM fill | P1 | B67+ | OPEN |
| DW-B54-01 | ATM auto-inject (blocked -- StrategyBase required) | P1 | future (blocked) | OPEN |
| DW-B58-01 | SnapshotTargetsPublic hardcoded prefixes | P2 | future | OPEN |
| DW-B58-02 | GlobalBe non-atomic lazy init | P2 | future | OPEN |
| DW-B58-03 | RelayBe OcoGroup not forwarded | P2 | future | OPEN |
| PRE-EXISTING-01 | Non-ASCII em-dash CopyEngine.cs lines 398, 499 | P2 | future | OPEN |
| PRE-EXISTING-02 | Non-ASCII arrow CopyEngine.cs lines ~1415-1416 | P2 | future | OPEN |
| PRE-EXISTING-03 | deploy-sync.ps1 archived; PropTraderTools sync is manual | P2 | future | OPEN |

**Closed this block**: 1 (DW-B66-01)
**Opened this block**: 1 (DW-B66-BE-01)
**Carry-forward OPEN**: 9 items (1xP0 + 2xP1 + 1xP1-blocked + 5xP2)
