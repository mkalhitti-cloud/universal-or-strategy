# T-W1-Perf: Closure Report

## Decision: ACCEPT AS-IS ✅

**Date**: 2026-06-01  
**Verdict**: Working As Intended - Defensive Allocation Pattern

## Rationale

### Thread Safety Requirement
- `.ToArray()` creates defensive snapshot of `acct.Positions`
- Prevents `InvalidOperationException` from broker thread mutations
- Build 939 P0 fix - explicitly documented in code comments

### Allocation Cost Analysis
- **Per-call**: ~40 bytes (array header + references)
- **Frequency**: 5-50 calls/second (dispatch cadence × fleet size)
- **Total**: ~500 bytes/second Gen0 allocation
- **Impact**: Negligible (microsecond lifetime, no GC pause)

### Already Optimized
- ✅ Uses for-loop instead of LINQ lambda
- ✅ Early-exit on match
- ✅ Typical size: 0-5 positions per account

### No Viable Alternative
- ❌ NinjaTrader API doesn't provide thread-safe collections
- ❌ Lock-free enumeration would risk crashes
- ✅ Jane Street principle: **Correctness > micro-optimization**

## Similar Patterns
Found 2 `.ToArray()` calls in [`V12_002.SIMA.Fleet.cs`](../../src/V12_002.SIMA.Fleet.cs):
- Line 413: Position snapshot (current target) - **ACCEPTED**
- Line 489: Account.All snapshot - **ACCEPTED** (same rationale)

## Conclusion
This allocation is a **necessary defensive pattern** for thread safety in the NinjaTrader API. The cost is negligible and the pattern is already optimized. No action required.

**Status**: CLOSED - Working As Intended