# UNIVERSAL OR STRATEGY V12.44 - PHASE 5: SURGICAL MODULARIZATION
## DIRECTOR-LEVEL HANDOFF PROMPT (OPUS 4.6 READY)

### 🚨 INITIALIZATION COMMAND
`claude --model opus-4.6 "ACTING AS PROJECT DIRECTOR. EXECUTE PHASE 5 MODULARIZATION PER THE ATTACHED PROTOCOL."`

---

### [CONTEXT BASELINE]
- **Project Root**: `C:\WSGTA\universal-or-strategy`
- **Current Build**: 1101E (Stability Certified)
- **Protocol**: MASTER HANDOFF PROTOCOL (MHP v1.0)
- **Artifacts Directory**: `C:\Users\Mohammed Khalid\.gemini\antigravity\brain\b3825fff-fc80-4e82-97d2-6543b87f49be`

### [OBJECTIVE: THE SURGICAL SPLIT]
Partition the monolith partial classes into functional nodes to reduce context noise for future forensic audits. **CRITICAL: MAINTAIN 1:1 LOGIC PARITY. DO NOT REFACTOR INTERNAL LOGIC—ONLY SEPARATE INTO NEW PARTIAL CLASS FILES.**

#### 📦 NODE A: UI MONOLITH PARTITION
**Source**: `src/UniversalORStrategyV12_002_Dev.UI.cs` (~2246 lines)
**Target Split**:
1.  `UniversalORStrategyV12_002_Dev.UI.IPC.cs`: TCP Server, Client Handlers (`HandleClient`), and `ProcessIpcCommands`.
2.  `UniversalORStrategyV12_002_Dev.UI.Compliance.cs`: Compliance Hub, P/L tracking, and `LogApexPerformance`.
3.  `UniversalORStrategyV12_002_Dev.UI.Sizing.cs`: ATR Auto-Sizing Engine and `GetTargetDistribution`.
4.  `UniversalORStrategyV12_002_Dev.UI.Callbacks.cs`: Chart Click handlers and Button Logic.

#### 📦 NODE B: ORDERS MONOLITH PARTITION
**Source**: `src/UniversalORStrategyV12_002_Dev.Orders.cs` (~2024 lines)
**Target Split**:
1.  `UniversalORStrategyV12_002_Dev.Orders.Callbacks.cs`: `OnOrderUpdate`, `OnPositionUpdate`, and `OnAccountOrderUpdate`.
2.  `UniversalORStrategyV12_002_Dev.Orders.Management.cs`: `SubmitBracketOrders`, `CleanupPosition`, and `ReconcileOrphanedOrders`.

### [TECHNICAL CONSTRAINTS]
1.  **Partial Class Integrity**: Each new file MUST use `public partial class UniversalORStrategyV12_002_Dev : Strategy`.
2.  **Using Directives**: Duplicate standard using blocks (`System`, `NinjaTrader.Cbi`, etc.) at the top of every new file.
3.  **No Logic Drift**: Use `mcp_delegation_bridge_save_file` or equivalent to ensure exact code replication.
4.  **Verification**: After each split, attempt to locate any missing private variable references and ensure they are accessible (they will be, as they are partials).

### [INSTRUCTIONS FOR AGENTIC EXECUTION]
1.  **Read**: `task.md` and `implementation_plan.md` from the Artifacts Directory above.
2.  **Plan**: Draft a specific mapping of line numbers/methods to new files.
3.  **Execute**: Move the code surgically.
4.  **Audit**: Perform a "Foreclosure Audit" to ensure no methods were lost during the move.
5.  **Summarize**: Update `walkthrough.md` with the new file structure.

---
**DIRECTOR VERDICT**: Proceed with **Phase 1: UI Modularization** immediately. Accuracy is the only metric of success.
