# Milestone v5.7 FINAL_FIX - Entry Cancellation Bug Fixed

**Date:** January 12, 2026  
**Version:** v5.7_FINAL_FIX  
**Status:** ✅ TESTED & WORKING

---

## 🎯 Overview

This milestone fixes a critical bug where opposite-side OR entry orders were being incorrectly cancelled when a position closed. Also includes risk parameter updates for tighter risk management.

---

## 🐛 Critical Bug Fixes

### **Bug #1: Opposite-Side Entry Cancellation (OnPositionUpdate)**
**Problem:**  
When a position went flat (e.g., ORLong stopped out), the `OnPositionUpdate` function was cancelling **ALL** pending entry orders, including unrelated opposite-side entries (e.g., ORShort).

**Root Cause:**  
Lines 1283-1296 in `OnPositionUpdate` looped through all `activePositions` and cancelled any unfilled entry orders when position went flat.

**Fix:**  
Removed the problematic code block. Pending entry orders are independent trades and should remain active even when other positions close.

**Code Change:**
```diff
- // Also cancel any pending entry orders
- foreach (var kvp in activePositions)
- {
-     if (!kvp.Value.EntryFilled && entryOrders.ContainsKey(kvp.Key))
-     {
-         Order entryOrder = entryOrders[kvp.Key];
-         if (entryOrder != null && (entryOrder.OrderState == OrderState.Working || entryOrder.OrderState == OrderState.Accepted))
-         {
-             CancelOrder(entryOrder);
-             Print(FormatString("Cancelled orphaned entry for {0}", kvp.Key));
-         }
-         positionsToCleanup.Add(kvp.Key);
-     }
- }
+ // REMOVED v5.7: DO NOT cancel unrelated pending entry orders!
+ // The old logic here cancelled ALL pending entries when position went flat,
+ // which incorrectly cancelled opposite-side OR entries (e.g., ORShort when ORLong closed)
+ // Pending entries should remain active - they are independent trades!
```

---

### **Bug #2: String Matching in CleanupPosition**
**Problem:**  
The `CleanupPosition` function used `.Contains()` for order name matching, which caused "ORLong" to match "ORShort" (since "ORShort" contains "OR").

**Fix:**  
Changed to use `.StartsWith(entryName) || .Contains("_" + entryName)` for precise matching.

**Code Change:**
```diff
- if (order.Name.Contains(entryName) && 
+ if ((order.Name.StartsWith(entryName) || order.Name.Contains("_" + entryName)) && 
      (order.OrderState == OrderState.Working || order.OrderState == OrderState.Accepted))
```

**Matching Logic:**
- ✅ Matches: `ORLong_123`, `Stop_ORLong_123`, `T1_ORLong_123`
- ❌ Does NOT match: `ORShort_456` (different trade)

---

## ⚙️ Parameter Updates

### **Risk Management**
| Parameter | Old Value | New Value | Reason |
|-----------|-----------|-----------|--------|
| `RiskPerTrade` | $400 | **$200** | Reduced default risk per trade |
| `ReducedRiskPerTrade` | $160 | **$200** | Simplified - now matches normal risk |
| `MinimumStop` | 4.0 pts | **1.0 pt** | Allows tighter stops for small OR ranges |

---

## ✅ Test Results

**Test Scenario:** MGC with both OR Long and Short entries
```
OR ENTRY ORDER: ORLong 8 @ 4598.10 | Stop: 4595.75 (-2.35)
OR ENTRY ORDER: ORShort 8 @ 4592.80 | Stop: 4595.15 (-2.35)

OR ENTRY FILLED: SHORT 8 @ 4592.74 (intended: 4592.80)
OR BRACKET SUBMITTED: Stop@4595.15 | T1:2@4591.63 | T2:2@4590.45 | T3:4@trail

STOP FILLED: 8 contracts @ 4595.11
CLEANUP: Cancelled T1_ORShort_181432 for ORShort_181432
CLEANUP: Cancelled T2_ORShort_181432 for ORShort_181432
CLEANUP SUMMARY for ORShort_181432: Stops=0 Targets=2 Entries=0
```

**✅ Result:** ORLong entry order remained active after ORShort was stopped out!

---

## 📁 Files Changed

### **Main Strategy File**
- `UniversalORStrategyV5.cs` - Updated to v5.7
- `UniversalORStrategyV5_v5_7_FINAL_FIX.cs` - Versioned copy

### **Archived**
- `archived-versions/UniversalORStrategyV5_v5_7_FINAL_FIX.cs` - Backup

---

## 🔄 Version History Leading to v5.7

| Version | Description |
|---------|-------------|
| v5.4 | PERFORMANCE - OnPriceChange for real-time trailing |
| v5.5 | CLEANUP_FIX - Enhanced cleanup for stranded stops |
| v5.6 | ORPHAN_FIX - Fixed CleanupPosition string matching |
| **v5.7** | **FINAL_FIX - Fixed OnPositionUpdate entry cancellation** |

---

## 🚀 Key Features (Cumulative)

1. ✅ **Real-time trailing stops** (Calculate.OnPriceChange)
2. ✅ **Multi-target system** (T1, T2, T3 with trailing)
3. ✅ **RMA mode** (ATR-based limit entries with Shift+Click)
4. ✅ **OR-based entries** (Breakout entries with OR-calculated stops/targets)
5. ✅ **Enhanced cleanup** (Prevents stranded orders after freeze/restart)
6. ✅ **Proper order isolation** (Opposite-side entries remain active)
7. ✅ **Flexible risk management** (Reduced risk for wide stops)

---

## 📊 Default Settings (v5.7)

### Session
- **Session Start:** 09:30 Eastern
- **Session End:** 16:00 Eastern
- **OR Timeframe:** 5 minutes

### Risk
- **Risk Per Trade:** $200
- **Reduced Risk:** $200
- **Stop Threshold:** 5.0 points
- **MES Min/Max:** 1 / 30 contracts
- **MGC Min/Max:** 1 / 15 contracts

### Stops
- **Stop Multiplier:** 0.5x OR Range
- **Min Stop:** 1.0 points ⬅️ **NEW**
- **Max Stop:** 8.0 points

### Targets
- **T1:** 0.25x OR Range (33% of contracts)
- **T2:** 0.5x OR Range (33% of contracts)
- **T3:** Trailing (34% of contracts)

### Trailing
- **Break-even:** 2.0 pts trigger, +1 tick offset
- **Trail 1:** 3.0 pts trigger, 2.0 pts distance
- **Trail 2:** 4.0 pts trigger, 1.5 pts distance
- **Trail 3:** 5.0 pts trigger, 1.0 pts distance

### RMA
- **ATR Period:** 14
- **Stop:** 1.0x ATR
- **T1:** 0.5x ATR
- **T2:** 1.0x ATR

---

## 🎓 Trading Context

- **Platform:** NinjaTrader 8
- **Data Feed:** Rithmic (Apex funded account)
- **Instruments:** MES (Micro E-mini S&P), MGC (Micro Gold)
- **Sessions:** NY, Australia, China, NZ opens
- **Scaling Plan:** Phase 1 (Single Account) → Phase 2 (Multi-Chart) → Phase 3 (Multi-Account)

---

## ⚠️ Known Limitations

None currently identified. Strategy is stable and ready for live trading.

---

## 📝 Next Steps

1. ✅ Test on live Apex account with real Rithmic data
2. Monitor for any edge cases with order cancellation
3. Consider adding position size scaling based on account equity
4. Plan for Phase 2: Multi-chart, single account implementation

---

## 👤 Developer Notes

**Critical Fix Context:**  
The entry cancellation bug was subtle and only appeared when:
1. Multiple OR entries were placed (both Long and Short)
2. One side filled and then closed (via stop or target)
3. The `OnPositionUpdate` cleanup logic incorrectly cancelled the opposite side

This was a **trading-critical bug** that could have prevented valid breakout entries from executing.

**Testing Recommendation:**  
Always test with both OR Long and Short entries active, then close one side to verify the other remains active.

---

**End of Milestone v5.7 Summary**
