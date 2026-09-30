# Ticket 2 — B162A-VERIFY Verification Report

**Ticket ID:** B162A-VERIFY
**Verifier:** ptt-verifier (independent — Layer 3)
**Method:** `TryFireBePriceRetry`
**File:** `src/PropTraderTools/CopyEngine.cs` (L7218–L7261)
**Date:** 2026-09-15
**Status:** VERIFY_PASS

---

## 1. Source Read (Independent)

Lines L7213–L7261 read directly from `src/PropTraderTools/CopyEngine.cs`.
The verifier independently read the file; no reliance on engineer's completion report for source content.

Full method body observed (L7218–L7261):

```
L7218  internal void TryFireBePriceRetry(Order o)
L7219  {
L7220      if (o == null || o.Name == null || o.Account == null)                      // (2)
L7221          return;
L7222      if (!o.Name.StartsWith("PTT-BE-Stop-", StringComparison.Ordinal))         // (3)
L7223          return;
L7224      if (!_pendingBePriceRetry.TryRemove(o.Account.Name, out double bePrice))  // (4) B162 atomic claim
L7225          return;
L7226      // B166: counter NOT cleared here -- preserved as Signal 6 sentinel until path-specific clear below.
L7227
L7228      // V-06 fix (Option a): Working = stop placed -- slot already consumed above.
L7229      if (o.OrderState == OrderState.Working)                                    // (5)
L7230      {
L7231          _bePriceRetryAttempts.TryRemove(o.Account.Name, out _);               // safe: position live, no Entry-cancel race
L7232          NinjaTrader.Code.Output.Process(
L7233              "[PTT-BE-RETRY] working-cleanup: " + o.Account.Name,
L7234              NinjaTrader.NinjaScript.PrintTo.OutputTab1
L7235          );
L7236          return;
L7237      }
L7238
L7239      if (o.OrderState != OrderState.Cancelled)                                  // (6)
L7240          return;
L7241
L7242      // B166: flat-cancel path -- position already gone, do NOT clear sentinel here.
L7243      // IsBeRetryArmedFlat = flat AND counter present; sentinel must remain until
L7244      // RunFollowerEvictPath clears it on confirmed position close. Clearing here
L7245      // drops Signal 6 before TryReplaceOnAtmCancel runs, allowing ghost ATM bracket.
L7246      if (!StillAtNonBePrice(o.Account, o.Instrument, bePrice)                  // (7)
L7247          || IsFlat(FindPosition(o.Account, o.Instrument)))                      // (8) B166: flat = no position to protect
L7248      {
L7249          return;
L7250      }
L7251
L7252      // Fire: submit BE stop at stored price. Counter cleared now -- retry will re-arm.
L7253      _bePriceRetryAttempts.TryRemove(o.Account.Name, out _);
L7254      var pos = FindPosition(o.Account, o.Instrument);
L7255      bool isLong = pos?.MarketPosition == MarketPosition.Long;
L7256      SubmitBeStop(o.Account, o.Instrument, bePrice, isLong);
L7257      NinjaTrader.Code.Output.Process(
L7258          "[PTT-BE-RETRY] fired: " + o.Account.Name + " bePrice=" + bePrice,
L7259          NinjaTrader.NinjaScript.PrintTo.OutputTab1
L7260      );
L7261  }
```

---

## 2. TryRemove Presence at Guard (4) — CONFIRMED

**L7224:** `if (!_pendingBePriceRetry.TryRemove(o.Account.Name, out double bePrice))  // (4) B162 atomic claim`

- `TryRemove` is the gate at guard position (4). ✅
- The `bePrice` out-variable is declared inline and consumed in subsequent paths.
- The comment `// (4) B162 atomic claim` is present, confirming intentional design marker.

---

## 3. TryGetValue Absence from _pendingBePriceRetry — CONFIRMED

**Independent scan:** `Select-String -Pattern "_pendingBePriceRetry\.TryGetValue"` → **0 matches** (entire file).

`TryGetValue` does not appear anywhere in conjunction with `_pendingBePriceRetry`. The old
TOCTOU-vulnerable pattern is fully absent from the file. ✅

Cross-check vs. engineer Layer 2: engineer reported "0 hits" — **matches**. ✅

---

## 4. _bePriceRetryAttempts.TryRemove Placement — CONFIRMED

**Independent scan:** `Select-String -Pattern "_bePriceRetryAttempts\.TryRemove"` → **3 matches**:

| Line | Location | Path | Correct? |
|------|----------|------|----------|
| L2022 | `RunFollowerEvictPath` | Confirmed position close (B166 sentinel clear) | ✅ YES |
| L7231 | `TryFireBePriceRetry` → Working block (L7229-L7237) | Working path — stop placed, position live | ✅ YES |
| L7253 | `TryFireBePriceRetry` → Fire path (after L7250 flat-cancel guard) | Fire path — BE stop submitted | ✅ YES |

**Flat-cancel block (L7246-L7250):** Contains ONLY `return;` — no `TryRemove` call present. ✅

Sentinel preservation per B166 design: the flat-cancel block returns without clearing
`_bePriceRetryAttempts`, so `IsBeRetryArmedFlat` (Signal 6) remains armed for
`IsFollowerPositionBlocked` to detect on the subsequent ATM-cancel path.

Cross-check vs. engineer Layer 2 table (L7231 Working, L7253 Fire, NOT in flat-cancel, L2022 RunFollowerEvictPath): **matches exactly**. ✅

---

## 5. Five-Line Context Check — NO SIDE EFFECTS

Lines L7219-L7228 (5 before + line + 4 after guard (4) at L7224):

| Line | Content | Side effects? |
|------|---------|---------------|
| L7219 | `{` — method open brace | None |
| L7220 | `if (o == null || ...)` — null guard (2) | Pure guard, no mutation |
| L7221 | `return;` | Early exit only |
| L7222 | `if (!o.Name.StartsWith(...))` — prefix guard (3) | Pure guard, no mutation |
| L7223 | `return;` | Early exit only |
| **L7224** | **`_pendingBePriceRetry.TryRemove(...)`** | **Intended removal from ConcurrentDictionary** |
| L7225 | `return;` | Early exit only |
| L7226 | `// B166: counter NOT cleared here ...` | Comment only |
| L7227 | (blank) | None |
| L7228 | `// V-06 fix (Option a): ...` | Comment only |
| L7229 | `if (o.OrderState == OrderState.Working)` | Guard only |

No adjacent code is affected. The guard (4) TryRemove is surrounded only by guard returns
and comments. ✅

---

## 6. Jane Street Compliance

### SCAN-01: JS-001 — No throw (void method)
`Select-String -Pattern "\bthrow\b"` in L7218-L7261 → **0 matches**. ✅

### SCAN-02: JS-002 — No null return
Method is `void`. Not applicable. ✅

### SCAN-03: JS-021 — No lock()
`Select-String -Pattern "\block\s*\("` in L7218-L7261 → **0 matches**. ✅
`ConcurrentDictionary.TryRemove` is internally lock-free at the per-key level (no managed `lock()` required by the caller).

### SCAN-04: JS-042 — ASCII-only
Non-ASCII character scan of L7218-L7261 → **0 matches**. All string literals and comments are pure ASCII. ✅

### SCAN-05: No DateTime.Now (NT8 constraint)
`Select-String -Pattern "DateTime\.Now[^U]"` in L7218-L7261 → **0 matches**. ✅

### SCAN-06: No hex color strings (NT8 SCAN-04)
`Select-String -Pattern "#[0-9A-Fa-f]{6}"` in L7218-L7261 → **0 matches**. ✅

### SCAN-07: No FontFamily (NT8 SCAN-03)
Method contains no WPF elements. Not applicable. ✅

### Summary: All applicable JS/NT8 rules pass with 0 violations.

---

## 7. McCabe CYC Count (Independent)

Counting decision points in `TryFireBePriceRetry` (L7218-L7261):

| # | Line | Decision point | Delta |
|---|------|----------------|-------|
| Base | — | method entry | +1 |
| 1 | L7220 | `if (o == null || o.Name == null || o.Account == null)` | +1 |
| 2 | L7222 | `if (!o.Name.StartsWith(...))` | +1 |
| 3 | L7224 | `if (!_pendingBePriceRetry.TryRemove(...))` | +1 |
| 4 | L7229 | `if (o.OrderState == OrderState.Working)` | +1 |
| 5 | L7239 | `if (o.OrderState != OrderState.Cancelled)` | +1 |
| 6 | L7246-7247 | `if (!StillAtNonBePrice(...) || IsFlat(...))` | +1 |
| 7 | L7255 | `pos?.MarketPosition` (null-conditional) | +1 |
| **Total** | | | **8** |

**Independent CYC = 8** ✅

Counting convention: each `if`-statement header (regardless of internal `||`/`&&` operators)
counts as one decision point, plus null-conditional `?.` as one. This is consistent with the
ticket spec's stated convention and yields CYC=8 ≤ 8 budget. ✅

Cross-check vs. engineer Layer 2 (CYC=8): **matches**. ✅

---

## 8. Concurrency Safety Argument

`ConcurrentDictionary<string, double>.TryRemove(key, out value)` provides an atomic
**claim-and-remove** operation. The .NET runtime implements this using internal bucket-level
locking that is invisible to callers — no managed `lock()` is needed or permitted (JS-021).

**Why it is race-free:**

Suppose two `OnOrderUpdate` dispatch threads (Thread A and Thread B) both receive a fill/cancel
event for the same `o.Account.Name` and both reach L7224 concurrently:

1. **Thread A** calls `TryRemove("Acct1", out double bePrice)`.
   - The runtime acquires the internal bucket lock for "Acct1", finds the key, atomically
     removes the entry, releases the lock, returns `true` with the stored `bePrice` value.
   - Thread A proceeds into the method body with a valid `bePrice`.

2. **Thread B** calls `TryRemove("Acct1", out double bePrice)` (same key).
   - The runtime acquires the same bucket lock, finds the key **absent** (already removed),
     releases the lock, returns `false` with `bePrice = default(double) = 0.0`.
   - Thread B takes the `return` at L7225. It does not proceed.

There is **no window** between a read and a remove in which a second thread can observe
the key and also enter the body. The old `TryGetValue → TryRemove` two-step had exactly
this TOCTOU (Time-Of-Check/Time-Of-Use) race: Thread A reads the key, Thread B reads the
key, Thread A removes it and fires — Thread B also removes it and fires a duplicate BE stop.

With `TryRemove` as the sole gate, exactly one thread per key per event can enter the
execution body. All subsequent concurrent calls for the same key return `false` immediately.
This is the correct lock-free atomic claim pattern per JS-021/JS-023. ✅

---

## 9. Cross-Check vs. Engineer Layer 2

| Check | Engineer Report | Independent Verification | Match? |
|-------|----------------|--------------------------|--------|
| TryRemove at L7224 | CONFIRMED | CONFIRMED | ✅ |
| TryGetValue absent | 0 hits | 0 hits (full-file scan) | ✅ |
| _bePriceRetryAttempts.TryRemove at L7231 | CONFIRMED | L7231 in Working block | ✅ |
| _bePriceRetryAttempts.TryRemove at L7253 | CONFIRMED | L7253 in fire path | ✅ |
| Flat-cancel block (L7246-L7250) has no TryRemove | CONFIRMED | L7248-L7249: only return; | ✅ |
| _bePriceRetryAttempts.TryRemove at L2022 | CONFIRMED | L2022 in RunFollowerEvictPath | ✅ |
| CYC = 8 | 8 | 8 | ✅ |
| JS-001 no-throw | PASS | 0 hits | ✅ |
| JS-021 no-lock | PASS | 0 hits | ✅ |
| JS-042 ASCII-only | PASS | 0 non-ASCII chars | ✅ |

No discrepancies found between engineer Layer 2 report and independent Layer 3 verification.

---

## 10. 7-Scan Checklist (Layer 3 — Independent)

| Scan | Rule | Command | Result |
|------|------|---------|--------|
| SCAN-01 | JS-001 no-throw | `Select-String -Pattern "\bthrow\b"` L7218–L7261 | ✅ 0 hits |
| SCAN-02 | JS-002 no-null-return | `void` method — not applicable | ✅ N/A |
| SCAN-03 | JS-021 no-lock | `Select-String -Pattern "\block\s*\("` L7218–L7261 | ✅ 0 hits |
| SCAN-04 | JS-042 ASCII-only | Non-ASCII scan L7218–L7261 | ✅ 0 hits |
| SCAN-05 | JS-CYC ≤ 8 | Branch count (independent) | ✅ CYC=8 |
| SCAN-06 | NT8 API / atomic gate | `_pendingBePriceRetry.TryGetValue` full-file scan; L7224 read | ✅ 0 hits / TryRemove confirmed |
| SCAN-07 | Signal 6 sentinel preserved | `_bePriceRetryAttempts.TryRemove` full-file scan; L7246-7250 read | ✅ Absent from flat-cancel block |

---

## Final Status

**VERIFY_PASS**

All verification criteria are independently satisfied:

- `_pendingBePriceRetry.TryRemove` is the atomic gate at guard (4) — **L7224** ✅
- `_pendingBePriceRetry.TryGetValue` is **absent** from the entire file (0 matches) ✅
- `_bePriceRetryAttempts.TryRemove` is present on both non-flat exit paths:
  - Working path — **L7231** ✅
  - Fire path — **L7253** ✅
- `_bePriceRetryAttempts.TryRemove` is **absent** from the flat-cancel block (L7246-L7250) — Signal 6 sentinel preserved per B166 design ✅
- `_bePriceRetryAttempts.TryRemove` present in `RunFollowerEvictPath` at **L2022** ✅
- CYC = **8** (within JS-CYC ≤ 8 budget) ✅
- All 7 scans: 0 violations ✅
- Concurrency safety: `TryRemove` is an atomic claim operation — race-free ✅
- No discrepancies between engineer Layer 2 self-report and independent Layer 3 scan ✅
