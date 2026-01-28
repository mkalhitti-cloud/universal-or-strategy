# V9 Multi-Agent Workflow Protocol

This protocol defines how your "Trading Team" of AI agents collaborates to move from an idea to a live trade.

---

## 🏗️ 1. The Planning Phase (Managing the Desk)
**Primary Agent**: `V9_009 Project Director` (Coordination)
*   **The Goal**: Ensure everyone knows their "Position" for the day and check for overlaps.
*   **Action**: The Director reads `MASTER_TASKS.json` and identifies which agents can work in parallel.

---

## 🛠️ 2. The Execution Phase (Independent Specialization)
**Primary Agents**: `V9_003` - `V9_008` (The Specialists)
*   **The Goal**: Deep work on specific features (Trim buttons, Trailing stops, etc.).
*   **Action**: These agents work in their own "WIP" (Work In Progress) folders to avoid "Stepping on toes." 

---

## 🧪 3. The Verification Phase (The Stress Test)
**Primary Agents**: `V9_001` & `V9_002` (Testing & Debugging)
*   **The Goal**: Confirm the "Infrastructure" is solid (TOS Connection).
*   **Action**: Before any new code is moved to the main strategy, we run the TOS RTD test.

---

## 🔗 4. The Integration Phase (Final Assembly)
**Primary Agent**: `V9_010 System Integrator`
*   **The Goal**: Combine all tested features into the Final Production Version.
*   **Action**: Merges code from WIP folders into the main `V9_ExternalRemote.exe`.

---

## 📈 5. Monitoring Phase
**Primary Agent**: `V8_MONITOR` (Safety Net)
*   **The Goal**: Protecting the Capital during live sessions.

## 💰 6. Cost Optimization (Environment Protocol)
> [!IMPORTANT]
> All subagents MUST optimize costs by offloading routine file operations.

**Scenario A: Antigravity IDE (Full Tool Support)**
*   **Protocol**: Use `mcp_delegation_bridge_call_gemini_flash` for all file I/O (Read/Write).
*   **Result**: 90% savings by keeping high-IQ models focused on logic.

**Scenario B: Claude Desktop / CLI (No Bridge Access)**
*   **Protocol**: **"Haiku Delegation"**. The Lead AI (Sonnet/Opus) defines the logic, then **must** instruct the environment to use **Claude 3.5 Haiku** (or a Haiku sub-agent) for the final `write_to_file` operation.
*   **Result**: High-speed, low-cost implementation without requiring a bridge.

---

## 🔄 The Communication Loop
1.  **Spawn**: You (the Human Manager) decide which task is next.
2.  **Implementation**: The subagent implementarion logic (reasoning) happens in a High-IQ model (Opus/Sonnet).
3.  **Delegation**: The subagent calls **Gemini Flash** via `call_gemini_flash` to save the results.
4.  **Handoff**: The subagent updates `CURRENT_SESSION.md` (via delegation).
5.  **Confirm**: You review the result and "Direct" the next subagent.
