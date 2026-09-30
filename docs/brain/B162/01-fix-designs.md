# B162 Fix Designs

## Status
DESIGN COMPLETE — SRC CODE BAN in effect. No .cs edits until Director approval.

---

## Issue A Fix — `TryFireBePriceRetry` Atomic Claim Gate

### Problem
`TryFireBePriceRetry` (L7122) uses `TryGetValue` (non-consuming read) at guard (4)
then fires `SubmitBeStop` then calls `TryRemove` post-fire (L7152). These three
operations are not atomic. If NT8 delivers two PTT-BE-Stop-N cancel events concurrently
(one per OCO pair being cancelled), both calls pass guard (4) before either removes
the slot, resulting in two `SubmitBeStop` calls on the leader.

### Fix
Replace `TryGetValue` + post-fire `TryRemove` with a single `TryRemove` at guard (4).
This is the identical pattern used by `TryFireFollowerBeRetry` at L1885 which has
never double-fired. ConcurrentDictionary.TryRemove is atomic -- exactly one caller wins.

### Exact Change

File: `src/PropTraderTools/CopyEngine.cs`
Method: `TryFireBePriceRetry` (L7115-L7158)

**BEFORE:**
```
        // B159 DW-B159-FM-B: attempt to fire deferred BE stop when PTT-BE-Stop-* is Cancelled
        // (bracket wipe recovery path). Also cleans up retry slot when PTT-BE-Stop-* goes Working
        // (V-06 fix: Option a -- Working state treated as successful placement, evict slot).
        // CYC=7: base(1) + compound-null-guard(2) + name-prefix(3) + no-slot(4) +
        //        working-cleanup(5) + not-cancelled(6) + still-at-non-be(7).
        // JS-001: no throw. JS-021: no lock (ConcurrentDictionary TryRemove is lock-free).
        // JS-042: ASCII-only. No DateTime.Now. "PTT-" prefix per order naming standard.
        internal void TryFireBePriceRetry(Order o)
        {
            if (o == null || o.Name == null || o.Account == null)                     // (2)
                return;
            if (!o.Name.StartsWith("PTT-BE-Stop-", StringComparison.Ordinal))        // (3)
                return;
            if (!_pendingBePriceRetry.TryGetValue(o.Account.Name, out double bePrice)) // (4)
                return;

            // V-06 fix (Option a): Working state = stop successfully placed -- evict slot.
            if (o.OrderState == OrderState.Working)                                   // (5)
            {
                _pendingBePriceRetry.TryRemove(o.Account.Name, out _);
                _bePriceRetryAttempts.TryRemove(o.Account.Name, out _);
                NinjaTrader.Code.Output.Process(
                    "[PTT-BE-RETRY] working-cleanup: " + o.Account.Name,
                    NinjaTrader.NinjaScript.PrintTo.OutputTab1
                );
                return;
            }

            if (o.OrderState != OrderState.Cancelled)                                 // (6)
                return;
            if (!StillAtNonBePrice(o.Account, o.Instrument, bePrice))                // (7)
                return;

            // Fire: submit BE stop at stored price.
            var pos = FindPosition(o.Account, o.Instrument);
            bool isLong = pos?.MarketPosition == MarketPosition.Long;
            SubmitBeStop(o.Account, o.Instrument, bePrice, isLong);
            _pendingBePriceRetry.TryRemove(o.Account.Name, out _);
            _bePriceRetryAttempts.TryRemove(o.Account.Name, out _);
            NinjaTrader.Code.Output.Process(
                "[PTT-BE-RETRY] fired: " + o.Account.Name + " bePrice=" + bePrice,
                NinjaTrader.NinjaScript.PrintTo.OutputTab1
            );
        }
```

**AFTER:**
```
        // B162 DW-B162-A: TryFireBePriceRetry -- fix non-atomic claim gate.
        // Root: TryGetValue (non-consuming read) at guard (4) followed by post-fire TryRemove
        //   is not atomic. If two PTT-BE-Stop-N cancel events arrive concurrently, both pass
        //   guard (4) before either removes the slot -> double SubmitBeStop -> reverse position.
        // Fix: replace TryGetValue with TryRemove at guard (4). Mirrors TryFireFollowerBeRetry
        //   L1885 which uses TryRemove as atomic claim gate and has never double-fired.
        //   ConcurrentDictionary.TryRemove is atomic -- exactly one caller wins the slot.
        //   Also moves _bePriceRetryAttempts.TryRemove immediately after claim (before fire)
        //   to keep counter consistent regardless of Working/Cancelled path taken.
        // CYC=7: base(1) + compound-null-guard(2) + name-prefix(3) + no-slot(4) +
        //        working-cleanup(5) + not-cancelled(6) + still-at-non-be(7). UNCHANGED.
        // JS-001: no throw. JS-021: no lock (ConcurrentDictionary TryRemove is lock-free).
        // JS-042: ASCII-only. No DateTime.Now. "PTT-" prefix per order naming standard.
        internal void TryFireBePriceRetry(Order o)
        {
            if (o == null || o.Name == null || o.Account == null)                      // (2)
                return;
            if (!o.Name.StartsWith("PTT-BE-Stop-", StringComparison.Ordinal))         // (3)
                return;
            if (!_pendingBePriceRetry.TryRemove(o.Account.Name, out double bePrice))  // (4) B162 atomic claim
                return;
            _bePriceRetryAttempts.TryRemove(o.Account.Name, out _);                   // clear counter

            // V-06 fix (Option a): Working state = stop successfully placed -- slot already evicted above.
            if (o.OrderState == OrderState.Working)                                    // (5)
            {
                NinjaTrader.Code.Output.Process(
                    "[PTT-BE-RETRY] working-cleanup: " + o.Account.Name,
                    NinjaTrader.NinjaScript.PrintTo.OutputTab1
                );
                return;
            }

            if (o.OrderState != OrderState.Cancelled)                                  // (6)
                return;
            if (!StillAtNonBePrice(o.Account, o.Instrument, bePrice))                 // (7)
                return;

            // Fire: submit BE stop at stored price.
            var pos = FindPosition(o.Account, o.Instrument);
            bool isLong = pos?.MarketPosition == MarketPosition.Long;
            SubmitBeStop(o.Account, o.Instrument, bePrice, isLong);
            NinjaTrader.Code.Output.Process(
                "[PTT-BE-RETRY] fired: " + o.Account.Name + " bePrice=" + bePrice,
                NinjaTrader.NinjaScript.PrintTo.OutputTab1
            );
        }
```

### Metrics
| Metric       | Before | After |
|:-------------|:------:|:-----:|
| CYC          | 7      | 7     |
| New fields   | 0      | 0     |
| New helpers  | 0      | 0     |
| Lines changed| --     | ~5    |

### Key Invariants Preserved
1. Working-cleanup still clears slot and counter (now done at guard 4, not duplicated at guard 5)
2. Cancelled-fire still submits and logs
3. StillAtNonBePrice guard still prevents spurious retry when BE stop already in place
4. Only change: TryGetValue → TryRemove at guard (4); removal of post-fire TryRemove calls

---

## Issue B Fix — `HasActivePttBeExits` and `StillAtNonBePrice` Drop Trailing Dash

### Problem
`HasActivePttBeExits` (L3097) checks `o.Name.StartsWith("PTT-BE-Stop-", ...)` at L3103.
The trailing dash matches `PTT-BE-Stop-1/2/3` (PttBreakEvenSwap path) but NOT bare
`"PTT-BE-Stop"` submitted by `SubmitBeStopOrder` via `RelayBe` (non-OCO path).

When `RelayBe`'s bare `PTT-BE-Stop` is in Working/Accepted/Submitted state (BE burst in
flight), `IsFollowerPositionBlocked` Signal 2 returns false → guard passes → `DispatchCopy`
fires → new ATM brackets (Stop1/Target1) placed on followers mid-BE-burst.

`StillAtNonBePrice` (L7098) has the same coverage gap at L7105 -- would miss a working
bare `PTT-BE-Stop` and spuriously report "still needs BE".

### Fix
Drop the trailing dash in `StartsWith` in both methods.

**`HasActivePttBeExits` L3103:**
```
// BEFORE:
if (o.Name == null || !o.Name.StartsWith("PTT-BE-Stop-", StringComparison.Ordinal)) continue;

// AFTER:
if (o.Name == null || !o.Name.StartsWith("PTT-BE-Stop", StringComparison.Ordinal)) continue;
```

**`StillAtNonBePrice` L7104-L7106:**
```
// BEFORE:
if (o.Name == null
    || !o.Name.StartsWith("PTT-BE-Stop-", StringComparison.Ordinal))
    continue;

// AFTER:
if (o.Name == null
    || !o.Name.StartsWith("PTT-BE-Stop", StringComparison.Ordinal))
    continue;
```

### Metrics
| Metric       | Before | After |
|:-------------|:------:|:-----:|
| CYC HasActivePttBeExits | 8 | 8 |
| CYC StillAtNonBePrice   | 7 | 7 |
| New fields   | 0      | 0     |
| New helpers  | 0      | 0     |
| Lines changed| --     | 2     |

### False-Positive Risk Assessment
Q: Could any non-BE order start with `"PTT-BE-Stop"`?
A: No. PTT order naming: `PTT-{module}-{type}[-N]`. The `BE-Stop` prefix is exclusive to
`SubmitBeStopOrder` and `PttBreakEvenSwap`. No other module uses this prefix.
Checked via grep: all `PTT-BE-Stop` references are in BE-related code only.

Q: Could bare `"PTT-BE-Stop"` linger after position is flat and block new entries?
A: No. `IsPttBeStopActiveState` (called at L3105) filters to Working/Accepted/Submitted only.
Once the bare `PTT-BE-Stop` is Filled or Cancelled, it is no longer in active state and
`HasActivePttBeExits` returns false. Guard clears correctly for new entries.

### Correctness Note on TryTriggerBeRecovery
`TryTriggerBeRecovery` (L5099) also uses `StartsWith("PTT-BE-Stop-", ...)` at L5110.
This guard controls whether a cancel triggers OCO-pair bracket recovery for followers.
Bare `"PTT-BE-Stop"` is submitted by `RelayBe` for non-OCO accounts. Non-OCO accounts
do not need OCO-pair bracket recovery. Changing this guard would cause `TryReplacePttBeBrackets`
to fire for non-OCO accounts on bare stop cancel -- WRONG. Leave L5110 unchanged.

---

## Log Markers for no-pipeline-repairs.md

Issue A: `[PTT-BE-RETRY] fired` — confirm appears only ONCE per `[PTT-BE-RETRY] armed`
Issue B: `[PTT-COPY-GUARD] ghost-entry blocked (active-be-exits)` during RelayBe path

---

## Direct Repair Eligibility
Both qualify under the no-pipeline direct repair rule:
- Zero new fields, zero new helpers
- CYC unchanged for all modified methods
- Single callsite change per bug
- Directly reuses existing helpers (ConcurrentDictionary TryRemove, StartsWith)
- Must be logged in no-pipeline-repairs.md before SIM gate
