# Milestone V8.28 Summary

This release addresses two critical issues identified during testing on live and simulation accounts. These fixes ensure stability when running multiple strategy instances.

## 1. Cross-Instrument Cancellation Bug (V8.27 Fix)

**Problem:**  
A strategy instance running on one instrument (e.g., MGC - Micro Gold) was inadvertently scanning all submittted orders and cancelling those on *other* instruments (e.g., MES - Micro S&P) because they weren't in its local active positions list.

**Impact:**  
Unexplained cancellations of stop or target orders on concurrent charts, leaving the user with unprotected positions.

**Solution:**  
Implemented a strict instrument filter in the `ReconcileOrphanedOrders` logic. The strategy now only manages orders that exactly match its assigned instrument.

## 2. Flatten Race Condition (V8.28 Fix)

**Problem:**  
When the "Flatten" button was pressed, the strategy would calculate how many contracts to close based on its internally tracked (cached) position. However, if a target filled at the *exact same moment*, the actual position would decrease while the flatten command used the old, higher quantity.

**Impact:**  
Overselling the position (e.g., selling 8 contracts when only 7 remained), resulting in a net short position after flattening a long trade.

**Solution:**  
The Flatten logic now checks the **Live Position Quantity** directly from NinjaTrader at the moment of execution. It takes the smaller of the two values to ensure we never sell more than we own.

---

### Deployment Status
- **Strategy Version**: V8.28
- **Status**: Live Testing Authorized
- **Deployment Path**: `bin/Custom/Strategies/UniversalORStrategyV8_28.cs`
- **Backup Path**: `PRODUCTION/V8_28/UniversalORStrategyV8_28.cs`
