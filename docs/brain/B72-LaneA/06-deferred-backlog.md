# B72-LaneA Deferred Backlog

**Block**: B72-LaneA
**Written by**: ptt-plan-reviewer (Ph5)
**Date**: 2026-08-17

---

## Closed This Block

None. No items from the B66-LaneC deferred backlog are closed by B72-LaneA.

B72-LaneA is a test-only block (retrospective test coverage for pre-shipped hotfixes). No source
logic was changed in the test-writing phase. The 22 hotfixes in `CopyEngine.cs` and
`PttBreakEven.cs` address live-trading correctness defects but do not resolve any of the open
deferred items. In particular:

- **DW-B66-BE-01**: B72-A-20 added a `PTT-BE-*` prefix guard to `CancelStaleBracketsLocal`
  in `PttBreakEven.cs`. However, `IsQxCancelCandidate` (which drives `CancelQxBrackets` in
  `CopyEngine.cs`) retains branch (4) `StartsWith("PTT-BE-")` — confirmed at CopyEngine.cs
  line 494. Quick Exit still cancels PTT-BE-* orders. DW-B66-BE-01 requires a Director
  decision on intended behavior before it can be closed.

- **DW-B66-C-02**: `DispatchCopy` Gate 5 (`IsDedup` dedup key) is unchanged. No B72 hotfix
  touched this code path.

---

## New Deferred Items — B72-LaneA

### DW-B72-01 — `IsAtmBracketName("Stop10")` returns true: acceptable-known digit-at-[4] edge

**Priority**: P3
**Target block**: future / informational
**Status**: OPEN
**Location**: `src/PropTraderTools/CopyEngine.cs` — `IsAtmBracketName(string name)`

**Description**: The B72-A-19 generic pattern `name.StartsWith("Stop") && name.Length > 4 && char.IsDigit(name[4])` evaluates `"Stop10"` as follows: `name[4] == '1'`; `char.IsDigit('1') == true`; method returns `true`. A hypothetical bracket order named `"Stop10"` (a 10th stop order, which NT8 does not generate) would be classified as an ATM bracket name and cancelled by `CancelQxBrackets`.

**NT8 ground truth**: NT8 ATM strategies generate bracket names `Stop1` through `Stop9` (single-digit suffix only). `"Stop10"` is not a valid NT8 ATM bracket name and does not appear in production trading. The plan notes this edge case explicitly in §3 B72-A-19: "Stop10 does not occur in practice but would be correctly caught if it did."

**Impact**: None in practice. If `"Stop10"` were ever created by a future NT8 version or custom ATM, it would be cancelled during QX/BE operations — conservative (over-cancel) rather than dangerous (under-cancel) behavior.

**Fix approach** (if ever required): Change the check to `name.Length == 5` (exactly one digit) instead of `name.Length > 4`:
```csharp
(name.StartsWith("Stop", StringComparison.Ordinal) && name.Length == 5 && char.IsDigit(name[4]))
```
This would exclude `"Stop10"` while still covering `Stop1`..`Stop9`.

**Defer rationale**: Cosmetic-only risk. NT8 constraint makes the edge case impossible in production. Fixing requires touching a tested predicate for zero practical benefit. Document and carry forward at P3 for awareness only.

---

## Carry-Forward Items (OPEN, unchanged from B66-LaneC)

### DW-B66-BE-01 — CancelQxBrackets cancels PTT-BE-Stop orders during Quick Exit

**Priority**: P1
**Target block**: B73+ (Director confirmation required)
**Status**: OPEN — opened B66-LaneA; no change in B66-LaneC or B72-LaneA.

**Description**: The widened predicate in `IsQxCancelCandidate` (branch 4,
`StartsWith("PTT-BE-", StringComparison.Ordinal)`) means that pressing Quick Exit will now
cancel any live `PTT-BE-Stop`, `PTT-BE-Stop-{i+1}`, or `PTT-BE-Target-{i+1}` orders on the
account for the instrument. This ensures a clean position exit but removes breakeven stop
protection at the moment of Quick Exit.

**B72-LaneA update**: B72-A-20 added a `PTT-BE-*` prefix guard to `CancelStaleBracketsLocal`
in `PttBreakEven.cs` — this correctly prevents BE-armed orders from self-cancelling during a
BE re-arm. However, `IsQxCancelCandidate` (CopyEngine.cs) is a separate method that still
includes the `PTT-BE-*` branch. The behaviors are complementary: BE-arm self-protection (A-20)
and QX-cancels-BE (DW-B66-BE-01) are independent.

**Action required**: Director must confirm that cancelling PTT-BE-* orders on Quick Exit is
the intended behavior. If NOT intended, branch (4) should be removed from
`IsQxCancelCandidate`, retaining only: (1) null guard, (2) `IsAtmBracketName`, (3) `PTT-QX-`
prefix.

**PTT-BE-* order name variants in production**:

| Variant | Source |
|---------|--------|
| `"PTT-BE-Stop"` | PttBreakEven.cs:217, :374; CopyEngine.cs:496 |
| `"PTT-BE-Stop-1"`, `"PTT-BE-Stop-2"`, ... | PttBreakEven.cs:407 |
| `"PTT-BE-Target-1"`, `"PTT-BE-Target-2"`, ... | PttBreakEven.cs:446 |
| `"PTT-BE-XXXX-00001-0"` (OCO group ID) | PttBreakEven.cs:328 |

---

### DW-B66-C-02 — DispatchCopy dedup key = 0.0 for all StopLimit entries

**Priority**: P1
**Target block**: B73+ (next available)
**Status**: OPEN — no change in B72-LaneA.

**Description**: `DispatchCopy` Gate 5 passes `order.LimitPrice` to `IsDedup` as the
dedup key. Since `StopLimit.LimitPrice == 0` always (NT8 confirmed), every StopLimit entry
order on every instrument shares dedup key `0.0`. The first StopLimit entry dispatch on any
instrument succeeds; any subsequent StopLimit entry dispatch (same or different instrument) is
wrongly rejected as a duplicate.

**Root cause** (`CopyEngine.cs` Gate 5):
```csharp
if (IsDedup(order.OrderId.ToString(), order.LimitPrice))
    return;
```
`order.LimitPrice` is always 0 for StopLimit. All StopLimit entries share key 0.0.

**Fix approach** (B73+):
```csharp
double dedupPrice = order.OrderType == OrderType.StopLimit ? order.StopPrice : order.LimitPrice;
if (IsDedup(order.OrderId.ToString(), dedupPrice))
    return;
```

**Defer rationale**: Changing `DispatchCopy` Gate 5 and `IsDedup` signature intersects ALL copy
paths (Market, Limit, StopLimit, StopMarket). A separate PR with dedicated test coverage is
required. Blast radius is larger than single-method scope.

---

### DW-B63-01 — Spurious PTT-Copy bracket orders on Sim102 after ATM fill

**Priority**: P1
**Target block**: B73+ (next available)
**Status**: OPEN — no change in B72-LaneA.

**Description**: After an ATM fill on the leader account, spurious PTT-Copy bracket orders
appear on the follower Sim102 account. These orders are not part of the intended copy cascade.

**Investigation starting point**: Review `DispatchCopy` Gate 0.5 (`IsExitSignalName` check)
and Gate A (`IsFollowerAccount` check) for the bracket order dispatch path. Verify
`IsWorkingBracket` (B63 T1) is correctly widened to `Accepted` state so bracket orders are
detected before they transition to Working. Check the `_dedupCache` for double-dispatch via
ConcurrentDictionary TryAdd semantics vs. the prior timestamp dedup.

---

### DW-B58-01 — SnapshotTargetsPublic hardcoded order-name prefixes

**Priority**: P2
**Target block**: future
**Status**: OPEN — no change in B72-LaneA.

**Description**: `SnapshotTargetsPublic` checks for hardcoded prefixes `PTT-QX-T` and
`PTT-TGT-`. Future blocks adding new PTT-prefixed target order names must update this
method or the snapshot will miss them.

---

### DW-B58-02 — GlobalBe non-atomic lazy init

**Priority**: P2
**Target block**: future
**Status**: OPEN — no change in B72-LaneA.

**Description**: `GlobalBe` lazy init uses `if (_globalBe == null) _globalBe = new ...`
(non-atomic). Currently safe — both callers (TradeCopierPanel, TradeCopierWindow) access
exclusively from the WPF UI thread. If a future block introduces a non-UI-thread caller,
`Interlocked.CompareExchange` will be required.

---

### DW-B58-03 — RelayBe does not forward OcoGroup from BeEventArgs

**Priority**: P2
**Target block**: future
**Status**: OPEN — no change in B72-LaneA.

**Description**: `RelayBe` does not forward `OcoGroup` from `BeEventArgs` to `SubmitBeStop`.
`SubmitBeStop` generates its own `OcoId` via `NextQxOcoId()`. If a future block requires
correlated OcoId fan-out across accounts for a single BE event, a new `SubmitBeStop`
overload accepting an explicit `OcoGroup` will be needed.

---

### DW-B54-01 — ATM auto-inject (blocked)

**Priority**: P1
**Target block**: future (blocked — requires StrategyBase-level NT8 API unavailable in AddOnBase)
**Status**: OPEN — blocked. No change in B72-LaneA.

**Description**: `AtmStrategyCreate()` is confirmed `StrategyBase`-only per
`NT8_FULL_REFERENCE.md`. The `AddOnBase` (`TradeCopierAddOn`) cannot call this API. A
companion `StrategyBase` add-in would be required. Deferred indefinitely pending Director
architectural decision.

---

### PRE-EXISTING-01 — Non-ASCII characters at CopyEngine.cs lines 398, 499

**Priority**: P2
**Status**: OPEN — pre-existing. Not introduced by B72-LaneA.

**Description**: Em-dash Unicode characters in B56 BUILD-FIX stub markers (comment lines
only). Lines 398 and 499 are in the `IsQxCancelCandidate` and `CancelQxBrackets` regions,
which received minor changes in B72 (B72-A-02, A-19). The em-dash characters remain
unchanged in the comment text; B72 did not touch those specific comment lines.

---

### PRE-EXISTING-02 — Non-ASCII characters at CopyEngine.cs — line estimate shifted by B72

**Priority**: P2
**Status**: OPEN — pre-existing. Not introduced by B72-LaneA.

**Description**: Unicode arrow characters in exit-order direction comments. The B66-LaneC
estimate was `~1449-1450`. B72-LaneA inserts net new lines in the 750-2270 region of
CopyEngine.cs (hotfixes A-04, A-06, A-07, A-08, A-09, A-10, A-11, A-12, A-21, A-23 all
affect this zone). The line estimate is now outdated. Re-confirm exact line numbers in the
next block that touches CopyEngine.cs below line 1000.

---

### PRE-EXISTING-03 — deploy-sync.ps1 archived; PropTraderTools sync is manual

**Priority**: P2
**Status**: OPEN — pre-existing infrastructure state. No change in B72-LaneA.

**Description**: `deploy-sync.ps1` is archived to `archive/v12-reference/scripts/deploy-sync.ps1`
and maps V12_002 strategy files, not PropTraderTools AddOn files. Manual SHA-256 copy +
`verify_links.ps1 -Fix` is the current PropTraderTools deploy workflow. The sync script in
use is `scripts\sync-ptt-to-nt8.ps1` (used in B72 completion and verification — copy: 0,
skip in-sync: 15).

---

## Summary Table

| ID | Item | Priority | Target | Status |
|----|------|----------|--------|--------|
| DW-B72-01 | `IsAtmBracketName("Stop10")` returns true — acceptable digit-at-[4] edge case | P3 | future | OPEN |
| DW-B66-BE-01 | `CancelQxBrackets` cancels PTT-BE-Stop on Quick Exit — Director confirmation | P1 | B73+ | OPEN |
| DW-B66-C-02 | DispatchCopy Gate 5 dedup key = 0.0 for all StopLimit entries | P1 | B73+ | OPEN |
| DW-B63-01 | Spurious PTT-Copy bracket orders on Sim102 after ATM fill | P1 | B73+ | OPEN |
| DW-B54-01 | ATM auto-inject (blocked — StrategyBase required) | P1 | future | OPEN (blocked) |
| DW-B58-01 | `SnapshotTargetsPublic` hardcoded prefixes | P2 | future | OPEN |
| DW-B58-02 | `GlobalBe` non-atomic lazy init | P2 | future | OPEN |
| DW-B58-03 | `RelayBe` OcoGroup not forwarded | P2 | future | OPEN |
| PRE-EXISTING-01 | Non-ASCII em-dash CopyEngine.cs lines 398, 499 | P2 | future | OPEN |
| PRE-EXISTING-02 | Non-ASCII arrow CopyEngine.cs — line estimate shifted by B72 insertions | P2 | future | OPEN |
| PRE-EXISTING-03 | deploy-sync.ps1 archived; PropTraderTools sync is manual | P2 | future | OPEN |

**Closed this block**: 0
**Opened this block**: 1 (DW-B72-01)
**Carry-forward OPEN**: 10 items (3×P1 + 1×P1-blocked + 5×P2 + 1×P3)
