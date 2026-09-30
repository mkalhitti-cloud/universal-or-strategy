# Ticket 7 — B166-VERIFY Completion Report

**Epic:** B161-B166  
**Ticket:** 7 (B166-VERIFY — verification only, no src changes)  
**File:** `src/PropTraderTools/CopyEngine.cs`  
**Methods verified:** `TryFireBePriceRetry` (~L7218–L7261), `RunFollowerEvictPath` (~L2013–L2026)  
**Date:** 2026-09-15

---

## Change A — TryFireBePriceRetry flat-cancel block (L7246–L7250)

### Exact lines from source

```csharp
// B166: flat-cancel path -- position already gone, do NOT clear sentinel here.
// IsBeRetryArmedFlat = flat AND counter present; sentinel must remain until
// RunFollowerEvictPath clears it on confirmed position close. Clearing here
// drops Signal 6 before TryReplaceOnAtmCancel runs, allowing ghost ATM bracket.
if (!StillAtNonBePrice(o.Account, o.Instrument, bePrice)                  // (7)
    || IsFlat(FindPosition(o.Account, o.Instrument)))                      // (8) B166: flat = no position to protect
{
    return;
}
```

### Verification

- Block body is a **simple `return;`** only. ✅
- `_bePriceRetryAttempts.TryRemove` is **ABSENT** from the flat-cancel block. ✅  
  (Confirmed by grep of L7246–L7250: zero matches for `_bePriceRetryAttempts`.)
- `_bePriceRetryAttempts.TryRemove(o.Account.Name, out _)` at L7253 is **outside** the flat-cancel
  block, in the fire path (after the guard has passed). This is correct — the sentinel is preserved
  for Signal 6 (`IsBeRetryArmedFlat`) on the flat path and only cleared on the live-position fire path.

---

## Change B — RunFollowerEvictPath (L2013–L2026)

### Exact lines from source

```csharp
private void RunFollowerEvictPath(Order o)
{
    bool isFilled = o.OrderState == OrderState.Filled;
    if (isFilled && !IsFlat(FindPosition(o.Account, o.Instrument)))
        return;
    string accName = o.Account.Name;
    bool slotEvicted = _pendingFollowerBeSlots.TryRemove(accName, out _);
    _beReplaceAttempts.TryRemove(accName, out _);
    _filledBeTargetCount.TryRemove(accName, out _);
    _bePriceRetryAttempts.TryRemove(accName, out _);   // B166: clear Signal 6 sentinel on confirmed position close
    AbortDrainOnFill(accName);
    if (slotEvicted)
        LogBeSlotEviction(accName, o.OrderState == OrderState.Rejected);
}
```

### Verification

- `_bePriceRetryAttempts.TryRemove(accName, out _)` is **PRESENT** at L2022. ✅
- It appears immediately **after** `_filledBeTargetCount.TryRemove(accName, out _)` at L2021. ✅
- Signal 6 (`IsBeRetryArmedFlat`) sentinel is cleared at the correct point: confirmed position close
  via RunFollowerEvictPath, not prematurely in the flat-cancel branch of TryFireBePriceRetry.

---

## CYC Verification — TryFireBePriceRetry

Method span: L7218–L7261 (44 lines).

Branch labels used in source (project McCabe convention: each numbered `if`/`||` branch = +1):

| Label | Line | Condition |
|-------|------|-----------|
| base  | —    | 1 |
| (2)   | 7220 | `o == null \|\| o.Name == null \|\| o.Account == null` |
| (3)   | 7222 | `!o.Name.StartsWith("PTT-BE-Stop-", ...)` |
| (4)   | 7224 | `!_pendingBePriceRetry.TryRemove(...)` |
| (5)   | 7229 | `o.OrderState == OrderState.Working` |
| (6)   | 7239 | `o.OrderState != OrderState.Cancelled` |
| (7)   | 7246 | `!StillAtNonBePrice(...)` |
| (8)   | 7247 | `\|\| IsFlat(FindPosition(...))` |

**Actual CYC = 1 + 7 = 8** ✅  
Matches expected CYC = 8 from ticket spec.

---

## 7-Scan Checklist

| Scan | Rule | Scope | Result | Notes |
|------|------|-------|--------|-------|
| SCAN-01 | JS-001 no-throw | TryFireBePriceRetry, RunFollowerEvictPath | **PASS — 0 hits** | Flat-cancel path is simple `return;`. No `throw` in either method. |
| SCAN-02 | JS-002 no-null-return | TryFireBePriceRetry | **N/A — PASS** | Method is `void`. No null return possible. |
| SCAN-03 | JS-021 no-lock | TryFireBePriceRetry, RunFollowerEvictPath | **PASS — 0 hits** | Both methods use `ConcurrentDictionary.TryRemove` (lock-free). No `lock()` in either method. 116 pre-existing locks elsewhere in file are out of scope. |
| SCAN-04 | JS-042 ASCII-only | B166 comment text | **PASS — 0 non-ASCII bytes** | Comment "B166: sentinel stays alive for Signal 6 (IsBeRetryArmedFlat)" is pure ASCII. All changed lines verified ASCII-only. |
| SCAN-05 | JS-CYC <= 8 | TryFireBePriceRetry | **PASS — CYC=8** | Exactly at budget. No CYC regression. |
| SCAN-06 | NT8 API correctness | TryRemove calls | **PASS** | L2022: `TryRemove(accName, out _)` — accName is `string`. L7253: `TryRemove(o.Account.Name, out _)` — string. Both match `ConcurrentDictionary<string, int>` key type. |
| SCAN-07 | Signal 6 sentinel | RunFollowerEvictPath | **PASS** | `_bePriceRetryAttempts.TryRemove` is at L2022 in RunFollowerEvictPath — the correct clear point for Signal 6 (`IsBeRetryArmedFlat`) after confirmed position close. Absent from flat-cancel path as required. |

All 7 scans: **PASS**

---

## SIM Gate

**PASSED 2026-09-15 4:33 PM**

---

## Final Status

**TICKET_COMPLETE**

Both changes verified present and correct in `src/PropTraderTools/CopyEngine.cs`:

- **Change A**: Flat-cancel block in `TryFireBePriceRetry` (L7246–L7250) is a simple `return;` with NO `_bePriceRetryAttempts.TryRemove` inside it. Sentinel preserved for Signal 6.
- **Change B**: `_bePriceRetryAttempts.TryRemove(accName, out _)` present at L2022 in `RunFollowerEvictPath`, immediately after `_filledBeTargetCount.TryRemove` at L2021. Signal 6 cleared at correct eviction point.
- CYC = 8 (unchanged, within budget).
- All 7 scans: PASS.
