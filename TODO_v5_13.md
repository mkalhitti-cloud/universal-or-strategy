# TODO List - v5.13 Minor Items

**Date Created:** January 16, 2026  
**Version:** v5.13  
**Priority:** Low (cosmetic and testing items)

---

## 🔧 Code Updates Needed

### 1. UI Version String Update
**Status:** Not Done  
**Priority:** Low (cosmetic only)  
**Location:** `CreateUI()` method  
**Issue:** UI shows "v5.12" instead of "v5.13"  
**Fix:** Update the UI creation message to show correct version

```csharp
// Current (line ~XXX):
Print("UI created - v5.12 (Target Management Dropdowns)");

// Should be:
Print("UI created - v5.13 (4-Target System + Critical Fixes)");
```

---

## 🧪 Testing Needed

### 2. Dropdown Actions Testing
**Status:** Not Tested  
**Priority:** Medium  
**What to Test:**
- T1 dropdown actions (Fill at Market, Move to 1pt, Move to 2pt, etc.)
- T2 dropdown actions (same as T1)
- T3 dropdown actions (NEW - needs testing)
- Runner dropdown actions (Close, Move Stop, Lock Profit, etc.)

**Expected Output:**
- Print messages showing action execution
- Orders modified/cancelled correctly
- All active positions affected simultaneously

### 3. Full Trailing Stop Progression
**Status:** Not Tested  
**Priority:** Medium  
**What to Test:**
- Let a trade run through all levels: BE → T1 → T2 → T3
- Verify stop moves correctly at each target fill
- Verify runner continues trailing after T3 fills

**Expected Output:**
```
STOP UPDATED: → Entry+1tick (Level: BE)
T1 FILLED: 1 contracts
STOP UPDATED: → T1 price (Level: T1)
T2 FILLED: 1 contracts
STOP UPDATED: → T2 price (Level: T2)
T3 FILLED: 1 contracts
STOP UPDATED: → T3 price (Level: T3)
[Runner continues trailing...]
```

### 4. Runner to Completion
**Status:** Not Tested  
**Priority:** Low  
**What to Test:**
- Let a runner trade complete naturally (not manually closed)
- Verify trailing stop continues working
- Verify final cleanup when runner exits

**Expected Output:**
- Trailing stop updates as price moves
- Clean exit when stopped out or manually closed
- All orders cleaned up properly

---

## 📝 Notes for Context Transfer

When requesting context transfer or starting a new session, mention:

1. **UI Version String:** Still shows v5.12, needs update to v5.13
2. **Dropdown Testing:** T3 dropdown actions not yet tested
3. **Trailing Progression:** Full BE → T1 → T2 → T3 sequence not yet validated
4. **Runner Completion:** Need to observe a runner trade from entry to natural exit

---

## ✅ Completed Items

- [x] 4-target system implementation
- [x] T3/T4 cleanup bug fix
- [x] Target movement validation
- [x] OR stop calculation change (0.5x ATR)
- [x] OR entry price detection (lastKnownPrice)
- [x] Version banner update in print statements
- [x] Live testing of basic functionality
- [x] Milestone documentation created
- [x] CHANGELOG.md updated
- [x] README.md updated
- [x] **CRITICAL: Stop order null-checking in UpdateStopOrder() - FIXED Jan 16**

---

## 🔴 NEW: Critical Tasks

### 5. Audit All Order Submissions for Null-Checking
**Status:** Not Done  
**Priority:** HIGH (safety)  
**What to Check:**

Verify that ALL `SubmitOrderUnmanaged()` calls have null-checking:
- [ ] `SubmitBracketOrders()` (line ~1470)
- [ ] `EnterORPosition()` (line ~882)
- [ ] `ExecuteRMAEntry()` (line ~1040)
- [ ] Any other order submission locations

**Pattern to Use:**
```csharp
Order newOrder = SubmitOrderUnmanaged(...);
if (newOrder == null) {
    Print("⚠️ CRITICAL ERROR: Order submission returned NULL!");
    // Recovery logic (flatten, cancel, or log)
    return;
}
```

---

**Last Updated:** January 16, 2026 (Post-crash fix)
