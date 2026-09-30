# B162 Investigation — Two Bugs Found During B161 SIM Test (2026-09-14 9:29 PM)

## Status
INVESTIGATION COMPLETE — ready for fix design.

## Context
B161 SIM gate: guard IS working (no ghost entries from `TryReplaceOnAtmCancel`).
Two NEW bugs observed in the same test run. Neither is caused by B161.

Evidence files:
- `docs/output.md` L242-617
- `docs/NinjaTrader Grid 2026-09-14 09-32 PM.csv`

---

## Issue A — `TryFireBePriceRetry` Double-Fire on Leader (Sim101)

### Observed Symptom
`output.md` L509-518:
```
[BE] SubmitBeStop: Sell 7 @ 7674.50 on Sim101    (first fire)
[PTT-BE-RETRY] fired: Sim101 bePrice=7674.5
[BE-DIAG-CANCEL] Sim101 PTT-BE order cancelled: PTT-BE-Stop-1 instr=MES DEC26
...
[BE] SubmitBeStop: Sell 7 @ 7674.50 on Sim101    (second fire -- WRONG)
[PTT-BE-RETRY] fired: Sim101 bePrice=7674.5
[BE-DIAG-CANCEL] Sim101 PTT-BE order cancelled: PTT-BE-Stop-2 instr=MES DEC26
```
Two 7-contract Sell orders submitted for Sim101 at the same price. One flattened the
position correctly; the second created a 7-contract short reverse position.

### Root Cause

**Function**: `TryFireBePriceRetry` at `CopyEngine.cs` L7122.

**Mechanism**: `TryFireBePriceRetry` uses `TryGetValue` (a non-consuming read) at guard (4)
to check `_pendingBePriceRetry`, then fires `SubmitBeStop`, then calls `TryRemove` at L7152.
The read and the remove are two separate operations with no atomicity guarantee between them.

When BE-All fires for Sim101, `MoveStopToBreakEven` creates PTT-BE-Stop-1/2/3 via
`PttBreakEvenSwap.Execute` and arms the `_pendingBePriceRetry` slot via `TryArmBePriceRetry`.
When the BE-All bracket wipe cancels PTT-BE-Stop-1 and PTT-BE-Stop-2:

- PTT-BE-Stop-1 Cancelled: `TryGetValue` succeeds (slot present), `SubmitBeStop` fires,
  BEFORE `TryRemove` at L7152 executes: PTT-BE-Stop-2 Cancelled event arrives on
  a concurrent NT8 callback thread, `TryGetValue` ALSO succeeds (slot still present),
  SECOND `SubmitBeStop` fires. Both threads then reach `TryRemove` (no-op for the second).

**Contrast with `TryFireFollowerBeRetry`** (L1876): uses `TryRemove` as the atomic claim
at guard (4). Only one caller wins per slot. `TryFireBePriceRetry` does not follow this pattern.

**Why `TryFireFollowerBeRetry` does not double-fire**: ConcurrentDictionary.TryRemove is
atomic -- exactly one caller wins. `TryGetValue` + separate `TryRemove` is NOT atomic.

### Source Locations
- `TryFireBePriceRetry`:        L7122  (CYC=7, bug is at guard (4))
- `TryArmBePriceRetry`:         L7077  (arms `_pendingBePriceRetry`)
- `TryFireFollowerBeRetry`:      L1876  (correct pattern: TryRemove at guard (4))
- `_pendingBePriceRetry`:        L328   (ConcurrentDictionary<string, double>)
- `_bePriceRetryAttempts`:       L335   (paired counter)

### Proposed Fix (Issue A)
Replace `TryGetValue` with `TryRemove` as the atomic claim gate in `TryFireBePriceRetry`.
Mirrors the exact pattern of `TryFireFollowerBeRetry` L1885.

```csharp
// BEFORE (non-atomic -- bug):
if (!_pendingBePriceRetry.TryGetValue(o.Account.Name, out double bePrice)) // (4)
    return;
...
SubmitBeStop(o.Account, o.Instrument, bePrice, isLong);
_pendingBePriceRetry.TryRemove(o.Account.Name, out _);   // L7152
_bePriceRetryAttempts.TryRemove(o.Account.Name, out _);  // L7153

// AFTER (atomic claim -- fix):
if (!_pendingBePriceRetry.TryRemove(o.Account.Name, out double bePrice)) // (4) atomic claim
    return;
_bePriceRetryAttempts.TryRemove(o.Account.Name, out _);  // clear counter immediately
...
SubmitBeStop(o.Account, o.Instrument, bePrice, isLong);
// TryRemove and TryRemove(attempts) moved to before SubmitBeStop -- no post-fire removal needed
```

Working-cleanup guard (5) also needs updating: currently does `TryRemove` on Working state.
With TryRemove as claim gate, the Working-cleanup path must also change to TryRemove.
The Working path should TryRemove ONLY the price slot (not the attempts counter, which
is separate). Actually both TryRemoves should move together before the fire.

CYC impact: guard (4) changes from `TryGetValue` to `TryRemove` -- no new branch, CYC unchanged (=7).
Zero new fields. Zero new helpers. Two line changes (guard 4 + remove post-fire lines).

---

## Issue B — `HasActivePttBeExits` Misses Bare `"PTT-BE-Stop"` Name

### Observed Symptom
`output.md` L545-560:
```
[TP1-OOU] name=Entry state=ChangeSubmitted signal=null acct=Sim102
[TP1-OOU] name=Entry state=ChangeSubmitted signal=null acct=Sim103
...
Stop1 Accepted acct=Sim102
Target1 Accepted acct=Sim102
Stop1 Working acct=Sim102
Target1 Working acct=Sim102
```
During the BE burst, `DispatchCopy` fired for followers Sim102/Sim103 -- new ATM brackets
(Stop1/Target1) armed on followers while BE-stop was still in flight. This created
duplicate bracket coverage that interfered with the BE outcome.

### Root Cause

**Function**: `HasActivePttBeExits` at `CopyEngine.cs` L3097.

**Mechanism**: `HasActivePttBeExits` checks `o.Name.StartsWith("PTT-BE-Stop-", ...)` at L3103.
The trailing dash (`-`) means it matches `PTT-BE-Stop-1`, `PTT-BE-Stop-2`, `PTT-BE-Stop-3`
but NOT the bare `"PTT-BE-Stop"` order submitted by `SubmitBeStopOrder` (L1475).

`RelayBe` (L725) calls `SubmitBeStop` for all accounts, which calls `SubmitBeStopOrder`
with name `"PTT-BE-Stop"` (no suffix). This bare order name is NOT covered by Signal 2
of `IsFollowerPositionBlocked`.

When the leader's `Entry` order transitions to `ChangeSubmitted` during the BE burst,
`DispatchCopy` is triggered. `ShouldSkipFollower` calls `IsFollowerPositionBlocked`.
Signal 2 (`HasActivePttBeExits`) checks for `PTT-BE-Stop-` (with dash) -- bare name FAILS.
Signal 1 (HasOpenPosition) and Signal 3 (HasSettlingBeTargets) may also pass if the
follower is flat (position cleared by BE-stop fill). Guard passes, copy dispatch fires.

**Note**: The bare `PTT-BE-Stop` is used by `RelayBe` (non-OCO path, older accounts).
The `PTT-BE-Stop-{N}` names are used by `PttBreakEvenSwap.Execute` (OCO path, modern).
Both naming patterns must be covered by Signal 2.

### Source Locations
- `HasActivePttBeExits`:         L3097  (Signal 2 -- only matches `PTT-BE-Stop-` with dash)
- `SubmitBeStopOrder`:           L1455  (submits bare `"PTT-BE-Stop"` name at L1475)
- `RelayBe`:                     L725   (calls SubmitBeStop for all accounts)
- `IsFollowerPositionBlocked`:   L3139  (3-signal composite, calls HasActivePttBeExits)
- `ShouldSkipFollower`:          L3178  (calls IsFollowerPositionBlocked for DispatchCopy)

### Proposed Fix (Issue B)
Change `HasActivePttBeExits` line check from `StartsWith("PTT-BE-Stop-")` (with dash) to
`StartsWith("PTT-BE-Stop", ...)` (without dash). This matches both:
- `"PTT-BE-Stop"` (bare, RelayBe path)
- `"PTT-BE-Stop-1"`, `"PTT-BE-Stop-2"`, etc. (PttBreakEvenSwap path)

```csharp
// BEFORE:
if (o.Name == null || !o.Name.StartsWith("PTT-BE-Stop-", StringComparison.Ordinal)) continue;

// AFTER:
if (o.Name == null || !o.Name.StartsWith("PTT-BE-Stop", StringComparison.Ordinal)) continue;
```

One character removed. CYC unchanged (=8). Zero new fields. Zero new helpers.

**False-positive risk**: Could a non-BE order name start with `"PTT-BE-Stop"`? The order
naming convention uses `"PTT-BE-Stop"` as the BE stop prefix exclusively. No other PTT
order uses this prefix. The false-positive risk is zero given the naming standard.

**Must also fix `StillAtNonBePrice`** (L7098): uses same `StartsWith("PTT-BE-Stop-")`
pattern at L7105. This helper looks for a Working PTT-BE-Stop to determine if the retry
is still needed. If Sim101 has a Working bare `"PTT-BE-Stop"` (from `RelayBe`), it would
not be found by `StillAtNonBePrice` → returns `true` (still needs BE) → spuriously fires
retry. Same fix applies: drop the trailing dash.

---

## Fix Classification

Both Issue A and Issue B qualify as **direct repairs** under the no-pipeline rule:
- Issue A: single guard change in `TryFireBePriceRetry` (TryGetValue -> TryRemove),
  moves two TryRemove calls before SubmitBeStop. Zero new fields. Zero new helpers.
  CYC unchanged (=7). Directly reuses ConcurrentDictionary atomic semantics.
- Issue B: single character removed from StartsWith predicate in `HasActivePttBeExits`
  (and `StillAtNonBePrice`). Zero new fields. Zero new helpers. CYC unchanged.

Both must be logged in `docs/brain/no-pipeline-repairs.md` before SIM gate.
After SIM gate pass, combined B161+B162 formal pipeline runs.

---

## Constraints Verified
- No lock() anywhere in proposed changes
- No async void
- No Unicode, ASCII-only literals
- No DateTime.Now
- CYC unchanged for both methods
- No new fields, no new helpers
- Must not block re-place for genuine post-flat new leader entries:
  Issue B fix: bare `PTT-BE-Stop` is only in active state (Working/Accepted/Submitted)
  during the BE burst. Once it fills or is terminal, Signal 2 returns false. The guard
  clears correctly. No false-positive risk for new entries after BE completes.
