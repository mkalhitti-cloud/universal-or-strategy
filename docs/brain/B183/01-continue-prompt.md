# B183 $continue Prompt
# Paste this into the copier-spec session AFTER ptt-orchestrator issues FINAL_PASS

---

$continue B183 complete. FINAL_PASS issued.

Run the following validation sequence and report results:

## Step 1: Exact-16 Symbol Validation

```powershell
$symbols = @(
    "_followerSlateReady",
    "TryMarkFollowerSlateNotReady",
    "TrySetFollowerSlateReady",
    "IsAccountFlat",
    "HasAnyActiveOrders",
    "OnFollowerPositionUpdate",
    "FlatGuardLoop",
    "TryLaneDHook",
    "_laneDDispatched",
    "TryLaneDDispatchFollower",
    "TryLaneEHook",
    "_laneEDispatched",
    "TryLaneEDispatchFollower",
    "BuildLaneEKey",
    "IsLaneEEligible",
    "PositionUpdate"
)
$head = Get-Content src/PropTraderTools/CopyEngine.cs -Raw
$missing = $symbols | Where-Object { $head -notmatch [regex]::Escape($_) }
if ($missing) {
    Write-Error "STILL MISSING: $($missing -join ', ')"
} else {
    Write-Host "ALL 16 PRESENT -- regression resolved"
}
```

Expected result: `ALL 16 PRESENT -- regression resolved`

## Step 2: Guardian Script

```powershell
powershell -File .\scripts\verify_critical_symbols.ps1
```

Expected: exit 0 with `GUARDIAN PASS -- all 16 critical symbols present`

## Step 3: Hard-link Sync

Confirm `ptt-sync.ps1` was run after the last .cs edit:

```powershell
powershell -File .\scripts\ptt-sync.ps1
```

## Step 4: Build Verification

Confirm the solution compiles with 0 errors and 0 new warnings.

## Step 5: BUG1 Call Trace Verification

Trace the cancel+reorder path through current HEAD:

1. `CancelQxBrackets(acc, instr)` line ~918 — confirm `TryMarkFollowerSlateNotReady(acc)` call exists
2. `CancelQxBrackets(acc, instr, snapshot)` line ~998 — confirm `TryMarkFollowerSlateNotReady(acc)` call exists
3. `FindFollowerBracketOrder(Account follower, ...)` — confirm it is now a block-body method
   with `if (_followerSlateReady.TryGetValue(follower.Name, out ready) && !ready) return null;` as gate (1)
4. `Subscribe()` — confirm `acc.PositionUpdate += OnFollowerPositionUpdate;` is present
5. `Unsubscribe()` — confirm `acc.PositionUpdate -= OnFollowerPositionUpdate;` is present
6. `DispatchCopy(Order, CopyRule)` — confirm `TryLaneDHook(order, rule, baseSignal, baseQty);` call after `baseQty` line
7. `DispatchToFollower(Account, ...)` — confirm Lane D skip gate `if (_laneDDispatched.ContainsKey(laneD_key)) return;` is present

**Expected BUG1 flow after fix**:
- Leader cancel -> `CancelQxBrackets` -> `TryMarkFollowerSlateNotReady` (gate=false)
- `FindFollowerBracketOrder` returns null immediately (gate closed, no stale bracket sync)
- NT8 fires `PositionUpdate` when flat -> `OnFollowerPositionUpdate` -> `TrySetFollowerSlateReady`
  (if flat + no active orders -> gate=true)
- Leader places new order -> `DispatchCopy` -> `TryLaneDHook` (Named-ATM) or follower loop
  -> copy dispatched to followers (BUG1 FIXED)

## Step 6: BUG2 Call Trace Verification

Trace the re-entry-after-close path:

1. `FlattenOneAccount(acc, instrument)` — confirm `TryMarkFollowerSlateNotReady(acc)` call before `CancelAllAccountOrders`
2. `Flatten(Instrument)` — confirm `FlatGuardLoop(...)` replaces raw `foreach`
3. `Flatten(Account, Instrument)` — confirm `FlatGuardLoop(...)` replaces follower `foreach`
4. `OnFollowerPositionUpdate` — confirm it calls `TrySetFollowerSlateReady` then `TryLaneEHook`
5. `TryLaneEHook` — confirm it calls `IsLaneEEligible` and gates on `_followerSlateReady`

**Expected BUG2 flow after fix (full close)**:
- Flatten called -> `FlattenOneAccount` -> `TryMarkFollowerSlateNotReady` (gate=false)
- `FindFollowerBracketOrder` returns null -> no bracket sync -> no ghost re-entry (BUG2 path 1 FIXED)

**Expected BUG2 flow after fix (partial QX/BE)**:
- Partial exit -> follower has remaining position, gate=false (from prior cancel)
- `PositionUpdate` fires -> `OnFollowerPositionUpdate` -> `TrySetFollowerSlateReady`
  (position > 0, not flat -> returns early, gate stays false)
- `TryLaneEHook` -> `IsLaneEEligible` (gate=false, eligible) -> dedup check -> `TryLaneEDispatchFollower`
  -> re-arms ATM bracket at remaining position (BUG2 FIXED)

## Step 7: Final Artifacts Check

Confirm these files exist:
- `docs/brain/B183/02-architecture-plan.md`
- `docs/brain/B183/02-plan-review.md` (contains `REVIEW_PASS: YES`)
- `docs/brain/B183/04-tickets.md`
- `docs/brain/B183/04-ticket-review.md` (contains `TICKET_REVIEW_PASS: YES`)
- `docs/brain/B183/ticket-1-completion.md` through `ticket-6-completion.md`
- `docs/brain/B183/ticket-1-verification.md` through `ticket-6-verification.md` (all `VERIFY_PASS: YES`)
- `scripts/verify_critical_symbols.ps1`
- `docs/brain/B183/05-final-review.md` (contains `FINAL_PASS: YES`)
- `docs/brain/B183/06-deferred-backlog.md`

## Step 8: Documentation Updates

1. Update `docs/brain/regression-investigation-B179-B180.md` status line to:
   `STATUS: RESOLVED -- B183 FINAL_PASS (date). All 16 symbols re-introduced.`

2. Confirm spec HTML `DW-B183-ROOT` card status is updated to CLOSED/RESOLVED.

3. Confirm `AGENTS.md` Section 2 has the 4 protocol hardening items added.

---

## Reporting Format

Report as a table:

| Check | Result | Notes |
|-------|--------|-------|
| All 16 symbols present | PASS/FAIL | list any missing |
| verify_critical_symbols.ps1 | PASS/FAIL | exit code |
| ptt-sync.ps1 ran | YES/NO | |
| Build 0E/0W | PASS/FAIL | |
| BUG1 call trace | PASS/FAIL | |
| BUG2 call trace (close) | PASS/FAIL | |
| BUG2 call trace (partial QX) | PASS/FAIL | |
| All 9 artifact files exist | PASS/FAIL | list any missing |
| docs updated | YES/NO | |

If ALL checks PASS: confirm B183 is complete and both regressions are architecturally resolved.
If ANY check FAILS: report exactly which check failed and what the engineer must fix before B183 can close.
