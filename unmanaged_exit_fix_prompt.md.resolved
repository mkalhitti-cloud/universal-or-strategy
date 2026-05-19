# MASTER DIRECTOR-TO-DIRECTOR HANDOFF: Phase 8 (Unmanaged Fix)

## 🎯 Current Objective: Phase 8 - Unmanaged Exit & Sizing Logic Fix
You are assuming full Project Director authority for **Universal OR Strategy V12.44 (Build 1101E)**. We have just completed Phase 7 (Concurrency Hardening), but a live test revealed two critical "Silent Killers" that need immediate surgical correction.

### 🧠 Shared Context
- **Project Root**: `C:\WSGTA\universal-or-strategy\`
- **Modular Framework**: All code is split into 16 files in `src/`.
- **System Memory**: All historical roadmaps, plans, and walkthroughs are synced in the project-local `docs/brain/` and the system brain directory `C:\Users\Mohammed Khalid\.gemini\antigravity\brain\b3825fff-fc80-4e82-97d2-6543b87f49be`.

---

## 🛑 Critical Issues Detected

### 1. Managed Method Violation (Panic Crash)
- **Problem**: When the `FLATTEN` or `FLATTEN_ONLY` buttons are pressed via IPC, the strategy crashes with an error: 
  `Method 'ExitShort' can't be called on unmanaged strategies.`
- **Root Cause**: `SIMA.cs` → `ClosePositionsOnlyApexAccounts` calls `ExitLong()` / `ExitShort()`. These are managed NinjaTrader methods. Since Build 1101E is marked `IsUnmanaged = true`, these calls are illegal.
- **Fix**: Replace all managed exit calls with `SubmitOrderUnmanaged`.

### 2. 1-Lot "Runner Only" Logic (Rigid Math)
- **Problem**: Entry logic currently treats all 1-contract trades as runners only (no T1 target).
- **Location**: `Orders.Management.cs` → `SubmitBracketOrders` and `UI.Sizing.cs` → `GetTargetDistribution`.
- **Fix**: Update the math to allowed T1 targets even on single-lot entries if the user configuration requests a scalp.

---

## 🛠️ Required Actions (Phase 8)

### Task A: SIMA Unmanaged Audit
- [Modify] [SIMA.cs](file:///C:/WSGTA/universal-or-strategy/src/UniversalORStrategyV12_002_Dev.SIMA.cs): Replace `ExitLong()` and `ExitShort()` with `SubmitOrderUnmanaged` in `ClosePositionsOnlyApexAccounts`.
- [Audit] [UI.IPC.cs](file:///C:/WSGTA/universal-or-strategy/src/UniversalORStrategyV12_002_Dev.UI.IPC.cs): Ensure all IPC-driven market actions use unmanaged order submissions.

### Task B: Sizing Sensitivity Update
- [Modify] [Orders.Management.cs](file:///C:/WSGTA/universal-or-strategy/src/UniversalORStrategyV12_002_Dev.Orders.Management.cs): Remove the hard-coded "Runner Only" skip for 1-contract trades in `SubmitBracketOrders`.
- [Modify] [UI.Sizing.cs](file:///C:/WSGTA/universal-or-strategy/src/UniversalORStrategyV12_002_Dev.UI.Sizing.cs): Update `GetTargetDistribution` to naturally handle 1-contract targets based on user priority % rather than a hard-coded T4 fallback.

---

## 🚦 Verification Plan
1. **Compilation Check**: Press `F5` in NinjaTrader.
2. **IPC Stress Test**: Execute `FLATTEN` during a live trade and verify no "Unmanaged Strategy" exceptions appear in the NinjaScript output tab.
3. **1-Lot Test**: Place a 1-lot trade and verify it generates a T1 Limit order.

**Reference Implementation Plan**: [implementation_plan.md](file:///C:/Users/Mohammed%20Khalid/.gemini/antigravity/brain/b3825fff-fc80-4e82-97d2-6543b87f49be/implementation_plan.md)
