# NinjaTrader 8 Strategy Development: Strategic Patterns

This document provides architectural guidance for NinjaTrader 8 strategy development, focusing on design decisions and systemic patterns.

## Core Architectural Principle: Event-Driven Price Processing

### Design Decision: OnMarketData vs. OnBarClose

The fundamental architectural choice in NT8 strategy development is **when to react to price changes**.

| Approach | Use Case | Trade-offs |
|----------|----------|------------|
| `OnBarUpdate` | Signal generation, indicator calculation | Lower CPU, batched processing |
| `OnMarketData` | Order management, trailing stops | Higher CPU, real-time precision |

**Strategic Recommendation:** Use a hybrid approach—generate signals on `OnBarUpdate`, but manage open positions via `OnMarketData`.

### The Close[0] Anti-Pattern

**Problem Statement:** `Close[0]` represents the *last completed bar's close*, not the current live price. This creates a fundamental timing mismatch for order management.

**Architectural Impact:**
- Trailing stops can be seconds behind the market
- Stop losses may execute at significantly worse prices
- Potential for missed profits of 5+ points per trade

**Solution Pattern:**
```csharp
protected override void OnMarketData(MarketDataEventArgs e)
{
    if (e.MarketDataType != MarketDataType.Last) return;
    if (e.Instrument != Instrument) return;
    
    // This is the true live price
    ProcessLivePrice(e.Price);
}
```

## Order Management Architecture

### IsUnmanaged Mode

For strategies requiring precise order control, `IsUnmanaged = true` provides:
- Direct order submission without NT8's internal order management
- Full control over order lifecycle
- Compatibility with Rithmic's low-latency feeds

**Trade-off Analysis:**
| Factor | Managed | Unmanaged |
|--------|---------|-----------|
| Simplicity | ✅ Higher | ❌ Lower |
| Control | ❌ Limited | ✅ Full |
| Error Proneness | ✅ Lower | ⚠️ Higher |
| Required for Apex | ⚠️ Possible | ✅ Recommended |

### Rate Limiting Strategy

**Constraint:** Apex imposes implicit order modification limits.

**Design Pattern:** Implement a time-based throttle:
```csharp
private DateTime lastModTime = DateTime.MinValue;
private const int ThrottleMs = 1000;

private bool CanModifyOrder() =>
    (DateTime.Now - lastModTime).TotalMilliseconds >= ThrottleMs;
```

**Strategic Consideration:** The 1-second throttle balances responsiveness with compliance. For volatile instruments, consider dynamic throttling based on ATR.

## Memory Management Strategy

### Problem Context
System operates on memory-constrained hardware (80%+ RAM usage reported).

### Architectural Approaches

1. **StringBuilder Pooling:** Reuse buffers for logging
2. **Event Batching:** Consolidate updates where possible
3. **Explicit Nulling:** Clear references when positions close
4. **Indicator Caching:** Avoid recalculating on every tick

### Memory Budget Guidelines
| Component | Target | Rationale |
|-----------|--------|-----------|
| Per-strategy overhead | < 50 MB | Allow 20+ charts |
| Per-position tracking | < 5 MB | Support multiple strategies |
| Logging buffer | < 1 MB | Prevent string allocation bloat |

## Testing Strategy

### Multi-Timeframe Validation
Test across 1-min, 5-min, and 15-min charts to verify:
- OnMarketData fires consistently
- Rate limiting behaves correctly
- Memory remains stable

### Long-Duration Testing
- 1-hour smoke test: Memory stable?
- 12-hour session test: No crashes?
- 24/5 simulation: Graceful Rithmic disconnect handling?

## Strategic Checklist for Code Reviews

- [ ] **Architecture:** Does it use OnMarketData for live price decisions?
- [ ] **Compliance:** Are order modifications rate-limited?
- [ ] **Resilience:** Is Rithmic disconnect handling implemented?
- [ ] **Efficiency:** Are memory-intensive operations pooled?
- [ ] **Maintainability:** Is the position lifecycle clearly managed?
- [ ] **Testability:** Can this be validated without live trading?

## Related Documents
- `live-price-tracking.md`: Detailed implementation of the OnMarketData fix
- `universal-strategy-v6-context.md`: Project-specific context
