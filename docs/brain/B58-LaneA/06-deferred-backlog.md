# B58-LaneA Deferred Backlog

**Block**: B58-LaneA (copy-engine-missing-members, 2026-08-10)
**Status**: OPEN items carried forward

---

## Block: B58-LaneA

### DW-B58-01: `SnapshotTargetsPublic` hardcoded order-name prefixes

- **Description**: `SnapshotTargetsPublic` filters `Account.Orders` using two hardcoded ASCII
  prefix strings: `"PTT-QX-T"` and `"PTT-TGT-"`. These match the naming conventions established
  in blocks B12 and B41.
- **Reason**: The B58 mandate scoped this method as a thin wrapper for the current panel needs.
  Prefix constants were not centralised at the time of B58 implementation.
- **Impact**: Low — only affects UI display (order count shown in TradeCopierPanel). Functional
  correctness is not impaired; incorrect counts would only manifest if new PTT-prefixed order
  name conventions were introduced without updating this method.
- **Suggested epic**: B59 or later — add a centralised `PttOrderNames` static class and replace
  the inline string literals with named constants. Update `SnapshotTargetsPublic` to reference
  the constants.

---

### DW-B58-02: `GlobalBe` non-atomic lazy init

- **Description**: The `GlobalBe` property getter uses a non-atomic lazy-init pattern:
  `if (_globalBe == null) _globalBe = new PttGlobalBreakEven();`. This is safe under the
  current access pattern (both callers, `TradeCopierPanel` and `TradeCopierWindow`, access
  `GlobalBe` exclusively from the WPF Dispatcher thread). CLR reference assignment is atomic on
  64-bit, so at worst two `PttGlobalBreakEven` instances are briefly created — the last writer
  wins and the extra instance is GC'd.
- **Reason**: B58 scope is restricted to restoring missing members. Converting to
  `Interlocked.CompareExchange` was out of scope and would add complexity with no current
  benefit (single-threaded access pattern).
- **Impact**: Low — thread-safe by access-pattern guarantee. No race condition under current usage.
  A future block adding a non-UI-thread caller (e.g., a background monitoring task) would trigger
  the latent risk.
- **Suggested epic**: future (only needed if multi-thread `GlobalBe` access is introduced) —
  replace with `Interlocked.CompareExchange(ref _globalBe, new PttGlobalBreakEven(), null)`.

---

### DW-B58-03: `RelayBe` does not forward `OcoGroup` from `BeEventArgs`

- **Description**: `RelayBe(BeEventArgs e)` fans out the pre-calculated BE price to all follower
  accounts via `SubmitBeStop(acc, e.Instrument, e.BePrice)`. The `BeEventArgs.OcoGroup` field
  (if present) is not forwarded. `SubmitBeStop` generates its own `OcoId` via `NextQxOcoId()`,
  so each fan-out account's BE stop order receives an independent OcoId rather than a correlated
  group OcoId.
- **Reason**: `SubmitBeStop` does not accept an explicit OcoGroup parameter. Adding an overload
  is beyond B58 scope. The pre-existing `SubmitBeStop` behavior (auto-generate OcoId) was the
  contract in B58's ICopyEngine relay specification.
- **Impact**: Low — correlated OcoId fan-out is not currently required by any panel feature.
  All follower accounts correctly receive independent BE stop orders. Impact would be Medium if
  a future block introduces cross-account OcoGroup correlation for coordinated BE stop placement.
- **Suggested epic**: future — add `SubmitBeStop(Account, Instrument, double, string ocoGroup)`
  overload; update `RelayBe` to pass `e.OcoGroup` if non-empty.

---

## Prior Blocks

No prior deferred backlog found.
(`docs/brain/B57-LaneA/06-deferred-backlog.md` does not exist — B57-LaneA produced no deferred
backlog or it was not written.)

---

*ptt-plan-reviewer | Phase 5 (Final Review) | B58-LaneA | 2026-08-10*
