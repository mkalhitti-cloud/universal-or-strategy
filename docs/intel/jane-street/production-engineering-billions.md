---
type: KnowledgeIntel
title: Production Engineering When Trading Billions
description: Production safety patterns from Jane Street — staleness guards, independent tracking, manifest logging, rate limiting. Applied to V12 risk and order management.
tags: [production, safety, staleness, risk, circuit-breaker, logging]
timestamp: 2026-06-25T00:00:00Z
---

# Production Engineering When Trading Billions

**Source**: Jane Street engineering talk on production safety at scale

## Key Takeaways

- Every order and its economic details are critical; **adverse selection punishes bugs immediately**
- Banish average-based SLO alerts for core systems; implement **event-based alerting** for all edge cases
- Implement orthogonal, epistemic alerts like **Feel Too Good** (PnL exceeds expectations) to catch cross-stack issues
- Defense in depth requires distinct enforcement gates with separate codebases, teams, and dependencies
- Support staff must possess business context; engineers collaborate closely with traders using shared terminology during incidents

## V12 C# Patterns

### `staleness_guard`
Track machine time vs last tick time to detect and halt on stale feeds.
```csharp
private void CheckFeedStaleness() {
    var staleness = Environment.TickCount64 - _lastTickMs;
    if (staleness > MaxStalenessMsThreshold) {
        HaltStrategy($"Feed stale: {staleness}ms since last tick");
    }
}
```

### `independent_tracking`
Verify working orders and positions in-memory separately from external API states.
```csharp
// Two independent trackers — discrepancy = alert
private int _inMemoryPosition;
private int _brokerReportedPosition;

private void ReconcilePositions() {
    if (_inMemoryPosition != _brokerReportedPosition)
        RaisePositionDiscrepancyAlert(_inMemoryPosition, _brokerReportedPosition);
}
```

### `manifest_logging`
Log BUILD_TAG and parameters at startup to simplify deployment roll audits.
```csharp
protected override void OnStateChange() {
    if (State == State.Configure) {
        Print($"[MANIFEST] Strategy={Name} Build={BuildTag} " +
              $"Params: MaxLoss={MaxDailyLoss} Size={DefaultSize}");
    }
}
```

### `rate_limiting`
Implement a time-window circuit breaker to catch looping order placement bugs.
```csharp
private int _ordersThisSecond;
private long _windowStartMs;

private bool CanPlaceOrder() {
    var now = Environment.TickCount64;
    if (now - _windowStartMs > 1000) { _ordersThisSecond = 0; _windowStartMs = now; }
    return ++_ordersThisSecond <= MaxOrdersPerSecond;
}
```

## Complexity Impact

These patterns keep `OnStateChange` and risk methods within CYC <= 8 by:
- `staleness_guard` → extracted as single-purpose helper (CYC 2-3)
- `independent_tracking` → extracted `ReconcilePositions()` (CYC 2)
- `rate_limiting` → extracted `CanPlaceOrder()` guard method (CYC 3)

## Cross-References
- [hardware-software-codesign.md](hardware-software-codesign.md) — defensive_initialization
- [how-to-build-an-exchange.md](how-to-build-an-exchange.md) — one_in_flight safety
- [complexity-reduction.md](complexity-reduction.md) — extract guard clauses
