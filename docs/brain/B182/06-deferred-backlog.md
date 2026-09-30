# B182 Deferred Backlog
## Ph5 Output -- ptt-plan-reviewer
### Date: 2026-09-28

---

## Deferred from B182 (Explicit Out-of-Scope)

### DW-B181-G3 -- Concurrent Flatten Test
**What:** Add a concurrent flatten test that verifies BE-ALL + Flatten-All happening within the same
event cycle produces the expected output (warn + no orphaned PTT-BE orders).
**Why deferred:** Requires NT8 event simulation harness. No unit test seam available yet.
**Priority:** P1. Include in B183.

### DW-B181-G1 -- Event Burst Unit Test
**What:** Unit test that simulates rapid consecutive order events (burst) to verify no state corruption
under high-frequency update conditions.
**Why deferred:** Requires event orchestration infrastructure beyond current seam.
**Priority:** P2. Include in B183 or B184.

### DW-B181-G2 -- Price Divergence Test
**What:** Verify that when leader and follower prices diverge (e.g., different fill prices), the
QX-pattern snapshot correctly uses each account's own stop, not the leader's.
**Why deferred:** Requires multi-account simulation seam.
**Priority:** P2. Include in B183 or B184.

---

## New Items Observed During B182

### DW-B182-WARN-DEDUP -- TryEmitBeFlattenWarn may fire multiple times per flatten event
**What:** When a position goes flat, multiple PTT-BE-* and PTT-QX-* orders cancel in the same event
cycle. Each one triggers `TryEmitBeFlattenWarn` independently, producing repeated `[BE-WARN]` log
lines for the same flatten event.
**Impact:** Log noise only. No trading behavior affected.
**Proposed fix:** Add a per-instrument-per-session warn dedup (similar to `_warnedAtmTemplates`
pattern). Throttle: emit once per account+instrument per T+5 second window.
**Priority:** P3 (cosmetic log noise). Include in B183 deferred cleanup.

### DW-B182-STARTUP-ORDER -- CheckAtmTemplatesForReverse runs before NT8 Output tab is ready
**What:** `State.Configure` fires very early in NT8 startup. The OutputTab1 panel may not yet be
visible to the user. The `[PTT-STARTUP-WARN]` messages will appear in the log but the user may
need to scroll back.
**Impact:** Low. The messages are logged correctly; the user must scroll back in Output tab.
**Proposed fix:** Consider also writing to NinjaTrader.Code.Output.Process(... OutputTab2) or
a PTT-specific startup summary panel. B183 backlog.
**Priority:** P3 (UX cosmetic). Deferred.

---

## Notes for B183 Planning

1. **B183 primary scope:** DW-B181-G3 (concurrent flatten test -- seam required).
2. **Secondary scope:** DW-B182-WARN-DEDUP (log dedup for TryEmitBeFlattenWarn).
3. **NT8-runtime integration tests:** The deferred `[Fact(Skip="DEFERRED:...")]` tests from B180 and
   B182 require an NT8 Account/Position mock infrastructure. This is a cross-cutting investment.
   Consider a B183 lane specifically for building the NT8 test seam.
