# Ticket 7 Verification — B166-VERIFY

**Epic:** B161-B166
**Ticket:** 7 (B166-VERIFY — sentinel lifecycle, no src changes)
**Verifier:** ptt-verifier (Phase 4b — independent, READ-ONLY)
**Date:** 2026-09-16
**Source file:** `src/PropTraderTools/CopyEngine.cs` (READ ONLY — not modified)

---

## Layer 3 Independent Verification

All reads and scans performed independently. Engineer Layer 2 report (ticket-7-completion.md) is
cross-checked AFTER independent findings — not used as input to scans.

---

## Change A — TryFireBePriceRetry flat-cancel block

### Source as read (L7238–L7261)

```
7239 |             if (o.OrderState != OrderState.Cancelled)                                  // (6)
7240 |                 return;
7241 |
7242 |             // B166: flat-cancel path -- position already gone, do NOT clear sentinel here.
7243 |             // IsBeRetryArmedFlat = flat AND counter present; sentinel must remain until
7244 |             // RunFollowerEvictPath clears it on confirmed position close. Clearing here
7245 |             // drops Signal 6 before TryReplaceOnAtmCancel runs, allowing ghost ATM bracket.
7246 |             if (!StillAtNonBePrice(o.Account, o.Instrument, bePrice)                  // (7)
7247 |                 || IsFlat(FindPosition(o.Account, o.Instrument)))                      // (8) B166: flat = no position to protect
7248 |             {
7249 |                 return;
7250 |             }
7251 |
7252 |             // Fire: submit BE stop at stored price. Counter cleared now -- retry will re-arm.
7253 |             _bePriceRetryAttempts.TryRemove(o.Account.Name, out _);
```

### Findings

- Flat-cancel guard: `if (!StillAtNonBePrice(...) || IsFlat(FindPosition(...)))` at L7246-L7247.
- Block body L7248-L7250: **exactly one statement: `return;`** — no `_bePriceRetryAttempts.TryRemove`
  inside the block.
- `_bePriceRetryAttempts.TryRemove` at L7253 is OUTSIDE the block (fire path, after guard passes).
- Sentinel is preserved on the flat-cancel path. CORRECT. PASS.

**Cross-check vs engineer report:** Engineer claimed block body is simple `return;`, absent TryRemove inside.
Layer 3 confirms: MATCH. No discrepancy.

---

## Change B — RunFollowerEvictPath (L2013–L2026)

### Source as read

```
2013 |         private void RunFollowerEvictPath(Order o)
2014 |         {
2015 |             bool isFilled = o.OrderState == OrderState.Filled;
2016 |             if (isFilled && !IsFlat(FindPosition(o.Account, o.Instrument)))
2017 |                 return;
2018 |             string accName = o.Account.Name;
2019 |             bool slotEvicted = _pendingFollowerBeSlots.TryRemove(accName, out _);
2020 |             _beReplaceAttempts.TryRemove(accName, out _);
2021 |             _filledBeTargetCount.TryRemove(accName, out _);
2022 |             _bePriceRetryAttempts.TryRemove(accName, out _);   // B166: clear Signal 6 sentinel on confirmed position close
2023 |             AbortDrainOnFill(accName);
2024 |             if (slotEvicted)
2025 |                 LogBeSlotEviction(accName, o.OrderState == OrderState.Rejected);
2026 |         }
```

### Findings

- `_bePriceRetryAttempts.TryRemove(accName, out _)` present at **L2022**. CONFIRMED.
- Comes immediately AFTER `_filledBeTargetCount.TryRemove` at L2021. CORRECT ORDER.
- Comment "B166: clear Signal 6 sentinel on confirmed position close" — ASCII-only. PASS.
- No lock(), no throw. PASS.

**Cross-check vs engineer report:** Engineer claimed L2022 with correct order. Layer 3 confirms: MATCH.

---

## Signal 6 — IsFollowerPositionBlocked / IsBeRetryArmedFlat (L3231, L3253)

### Source as read

```
3231 |             if (IsBeRetryArmedFlat(acc, instr))    // (9) Signal 6 -- post-fallback-BE window (B166)
3232 |             {
3233 |                 NinjaTrader.Code.Output.Process(
3234 |                     "[PTT-COPY-GUARD] ghost-entry blocked (be-retry-armed-flat): "
3235 |                         + acc.Name + "\n" + instr.FullName,
3236 |                     NinjaTrader.NinjaScript.PrintTo.OutputTab1
3237 |                 );
3238 |                 return true;
3239 |             }
3240 |             return false;
3241 |         }
```

```
3253 |         private bool IsBeRetryArmedFlat(Account acc, Instrument instr) =>
3254 |             IsFlat(FindPosition(acc, instr)) && _bePriceRetryAttempts.ContainsKey(acc.Name);
```

### Findings

- Signal 6 at L3231 calls `IsBeRetryArmedFlat(acc, instr)`.
- `IsBeRetryArmedFlat` at L3253-L3254 uses `_bePriceRetryAttempts.ContainsKey(acc.Name)` — O(1)
  lock-free read on the sentinel dictionary. CONFIRMED.
- Guard returns `true` (blocks copy) when flat AND sentinel counter present.
- ASCII-only comment `// (9) Signal 6 -- post-fallback-BE window (B166)`. PASS.

---

## 5-Line Context — No Side Effects

**Around flat-cancel block (L7237-L7262):**
- L7237: close brace of Working path
- L7239-L7240: OrderState.Cancelled guard (exit if not cancelled)
- L7242-L7250: B166 flat-cancel block (return; only)
- L7252-L7260: fire path (TryRemove + SubmitBeStop + log)
- No unexpected mutations, no state writes inside flat-cancel block. CLEAN.

**Around RunFollowerEvictPath L2022 (L2007-L2028):**
- L2009-L2012: method comment (ASCII)
- L2013-L2026: method body — sequential TryRemove calls + AbortDrainOnFill + conditional log
- L2022 is a single lock-free TryRemove, no branching
- No unexpected mutations, no signal-graph side effects. CLEAN.

---

## 7-Scan Results (Independent — Layer 3)

| Scan | Rule | Scope | Layer 3 Result | Match Layer 2 |
|------|------|-------|----------------|--------------|
| SCAN-01 | JS-021 no-lock | L7238-L7261 and L2013-L2026 | 0 hits — PASS | YES |
| SCAN-01 | JS-021 no-lock | L2013-L2026 | 0 hits — PASS | YES |
| SCAN-02 | ASCII-only | L7238-L7261, L2007-L2026 | 0 non-ASCII bytes — PASS | YES |
| SCAN-03 | JS-001 no-throw | L7218-L7261, L2013-L2026 | 0 hits — PASS | YES |
| SCAN-04 | FontFamily | Full file | 3 hits — all in "No FontFamily" compliance comments, pre-existing, NOT in changed lines — PASS | YES (pre-existing) |
| SCAN-05 | Hex color #RRGGBB | L7218-L7261, L2013-L2026 | 0 hits — PASS | YES |
| SCAN-06 | DateTime.Now | L7218-L7261, L2013-L2026 | 0 hits — PASS | YES |
| SCAN-07 | Signal 6 sentinel | RunFollowerEvictPath | TryRemove at L2022, absent from flat-cancel block — PASS | YES |

**Note:** FontFamily 3 hits are in existing code comments stating "No FontFamily" as a compliance
annotation (L4413, L4676, L4698). None are in the B166 changed regions. No WPF FontFamily=
assignments are present. Pre-existing, out-of-scope for this ticket.

**All 7 scans: PASS. No discrepancies between Layer 2 and Layer 3.**

---

## CYC — TryFireBePriceRetry (Independent Count)

Method span: L7218–L7261.

Project McCabe convention: compound null-guard treated as one decision point (source-annotated labels
2-8 used as canonical branch labels).

| Label | Line | Decision |
|-------|------|----------|
| base  | —    | +1 |
| (2)   | 7220 | `if (o == null \|\| o.Name == null \|\| o.Account == null)` — one guard clause |
| (3)   | 7222 | `if (!o.Name.StartsWith("PTT-BE-Stop-", ...))` |
| (4)   | 7224 | `if (!_pendingBePriceRetry.TryRemove(...))` |
| (5)   | 7229 | `if (o.OrderState == OrderState.Working)` |
| (6)   | 7239 | `if (o.OrderState != OrderState.Cancelled)` |
| (7)   | 7246 | `if (!StillAtNonBePrice(...) \|\| ...)` |
| (8)   | 7247 | `\|\| IsFlat(FindPosition(...))` — short-circuit branch |

**Independent CYC = 1 + 7 = 8. Matches expected (<=8). PASS.**

---

## Sentinel Lifecycle Assessment

All `_bePriceRetryAttempts` references in file:

| Line | Operation | Context |
|------|-----------|---------|
| L335 | Declaration | `ConcurrentDictionary<string, int>` field |
| L7165 | GetOrAdd | TryArmBePriceRetry — read current attempts |
| L7168 | AddOrUpdate | TryArmBePriceRetry — **ARM** sentinel (counter incremented) |
| L7231 | TryRemove | Working path in TryFireBePriceRetry — cleared (position live, no race) |
| L7253 | TryRemove | Fire path in TryFireBePriceRetry — cleared after stop submitted |
| L2022 | TryRemove | RunFollowerEvictPath — **cleared on confirmed position close** |
| L3254 | ContainsKey | IsBeRetryArmedFlat — Signal 6 read (O(1) lock-free) |

**Lifecycle verdict:**
- Sentinel ARMED: `TryArmBePriceRetry` (L7168) — called from MoveStopToBreakEven.
- Sentinel NOT cleared in flat-cancel path (L7246-L7250): CORRECT — `return;` only.
- Sentinel IS cleared in working path (L7231): CORRECT — position live, no Entry-cancel race.
- Sentinel IS cleared in fire path (L7253): CORRECT — after stop submitted.
- Sentinel IS cleared in eviction path (L2022): CORRECT — on confirmed position close.
- Signal 6 reads sentinel (L3254): CORRECT — blocks ghost-entry during NT8 event-queue gap.

**Lifecycle is complete and consistent. No leak paths. No premature clear. PASS.**

---

## SIM Gate

**PASSED 2026-09-15 4:33 PM** (as reported by engineer, noted for record).
No src changes in this ticket — SIM gate result applies to the B166 implementation delivered
in preceding tickets of this epic.

---

## Cross-Check Summary (Layer 2 vs Layer 3)

| Claim (Layer 2) | Layer 3 Finding | Match |
|-----------------|-----------------|-------|
| Flat-cancel block body is `return;` only | CONFIRMED at L7249 | YES |
| No TryRemove inside flat-cancel block | CONFIRMED — zero hits | YES |
| TryRemove at L7253 is outside flat-cancel block | CONFIRMED at L7253 (fire path) | YES |
| RunFollowerEvictPath TryRemove at L2022 | CONFIRMED at L2022 | YES |
| L2022 comes after L2021 (_filledBeTargetCount) | CONFIRMED — sequential ordering | YES |
| Signal 6 via IsBeRetryArmedFlat uses _bePriceRetryAttempts | CONFIRMED at L3254 | YES |
| CYC = 8 | CONFIRMED by independent count | YES |
| All 7 scans PASS | CONFIRMED by independent scans | YES |

**No discrepancies found between engineer Layer 2 report and independent Layer 3 verification.**

---

## Final Status

**VERIFY_PASS**

Both B166 changes are correctly implemented in `src/PropTraderTools/CopyEngine.cs`:

- **Change A (L7246-L7250):** Flat-cancel block contains `return;` only — `_bePriceRetryAttempts`
  sentinel is preserved for Signal 6 (`IsBeRetryArmedFlat`).
- **Change B (L2022):** `_bePriceRetryAttempts.TryRemove(accName, out _)` present in
  `RunFollowerEvictPath` immediately after `_filledBeTargetCount.TryRemove` — cleared only on
  confirmed position close.
- Signal 6 wiring (`IsBeRetryArmedFlat` ? `_bePriceRetryAttempts.ContainsKey`) confirmed at L3231/L3254.
- CYC = 8 (within budget).
- Sentinel lifecycle complete and consistent — no leak paths, no premature clears.
- All 7 scans PASS. No DNA violations. No discrepancies with engineer report.
