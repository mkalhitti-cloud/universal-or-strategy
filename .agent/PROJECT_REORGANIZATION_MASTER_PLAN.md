# PROJECT REORGANIZATION MASTER PLAN
**Date**: 2026-01-25
**Status**: Ready for Execution
**Market Opens In**: ~3 hours

---

## EXECUTIVE SUMMARY

**Current State**: Project chaos preventing efficient work
- 40+ strategy versions scattered in root
- V8_22 is production but not protected
- V9 External Remote had working TOS RTD integration, then broke (agent overwrote it)
- Losing context between IDE/agent switches
- Gemini Flash MCP bridge exists but not being used (burning credits)
- No task/dependency tracking across sub-agents

**Target State**: Organized, agent-friendly, production-safe workspace
- Clear separation: PRODUCTION (stable) vs DEVELOPMENT (WIP) vs ARCHIVE
- Universal task system working with ALL AIs (Claude, Gemini, Cursor, Antigravity, etc.)
- Shared context files enable seamless IDE/agent switching
- Sub-agents with clear responsibilities and dependencies
- Gemini Flash MCP bridge actively deployed (99% cost savings on file ops)
- Always have a working version for live trading
- Can "wear the project" - pick up from anywhere without missing a beat

---

## CRITICAL REQUIREMENTS

### 1. Live Trading Safety (NON-NEGOTIABLE)
- **V8_22 PROTECTED**: Current production version must be isolated and never overwritten
- **Rollback capability**: Always able to revert to last known good
- **Test before deploy**: No untested code touches live trading

### 2. V9 External Remote Rescue Mission
- **Problem**: Working version showing TOS RTD live numbers got overwritten
- **Restore file**: `UniversalOR_V9_MasterHub_MILESTONE_LIVE_SUCCESS.cs` (commit 48bfa7e)
- **Next steps**:
  - Sub-agent 1: TOS RTD Bridge (fix live data connection)
  - Sub-agent 2: Copy Trading (external orchestration)
  - Sub-agent 3: WPF UI (controls outside NinjaTrader)

### 3. Multi-AI/Multi-IDE Continuity
**Your IDEs/Agents**:
- Antigravity (Opus 4.5 Thinking) - primary for complex code
- Claude Code CLI (Sonnet/Haiku) - coordination & file ops
- Cursor (Gemini Flash) - fast iterations
- Windsurf (multiple models) - alternative IDE

**Problem**: Every switch = context loss, feels like starting over

**Solution**: Shared state files that ALL agents read/update

### 4. Cost Optimization via Gemini Flash
- **MCP bridge exists**: `.agent/mcp-servers/delegation_bridge.py`
- **Problem**: Agents forget to use it
- **Solution**: Enforce delegation protocol in all workflows

---

## NEW DIRECTORY STRUCTURE

```
universal-or-strategy/
├── PRODUCTION/                          ← NEW: Protected stable versions
│   ├── V8_22_STABLE/
│   │   ├── UniversalORStrategyV8_22.cs
│   │   ├── TESTED_DATE.txt (2026-01-23)
│   │   ├── PARAMETERS.xlsx (snapshot)
│   │   └── README_V8_22.md
│   ├── V9_STABLE/
│   │   ├── V9_Milestone_FINAL.exe
│   │   ├── UniversalOR_V9_MasterHub_MILESTONE_LIVE_SUCCESS.cs
│   │   ├── TOS_RTD_Bridge.xlsx
│   │   └── README_V9.md
│   └── ROLLBACK_INSTRUCTIONS.md
│
├── DEVELOPMENT/                         ← NEW: Work in progress
│   ├── V8_WIP/                          ← V8 experiments
│   ├── V9_WIP/                          ← V9 development
│   │   ├── TOS_RTD_BRIDGE/             ← Sub-agent 1 workspace
│   │   ├── COPY_TRADING/               ← Sub-agent 2 workspace
│   │   └── WPF_UI/                     ← Sub-agent 3 workspace
│   └── EXPERIMENTS/
│
├── .agent/
│   ├── TASKS/                           ← NEW: Universal task system
│   │   ├── MASTER_TASKS.json           ← All project tasks with dependencies
│   │   ├── V9_TOS_BRIDGE.json          ← Sub-agent 1 tasks
│   │   ├── V9_COPY_TRADING.json        ← Sub-agent 2 tasks
│   │   ├── V9_WPF_UI.json              ← Sub-agent 3 tasks
│   │   └── TASK_TEMPLATE.json
│   │
│   ├── SHARED_CONTEXT/                  ← NEW: Cross-IDE state
│   │   ├── CURRENT_SESSION.md          ← What are we working on NOW
│   │   ├── LAST_KNOWN_GOOD.json        ← Rollback pointers
│   │   ├── AGENT_HANDOFF.md            ← Template for switching agents
│   │   ├── V8_STATUS.json              ← Production trading status
│   │   ├── V9_STATUS.json              ← Development status
│   │   └── MARKET_STATE.json           ← Open/closed, last test results
│   │
│   ├── mcp-servers/
│   │   ├── delegation_bridge.py        ← EXISTS, needs env setup
│   │   └── MCP_USAGE_PROTOCOL.md       ← NEW: How to use it
│   │
│   ├── skills/                          ← Keep existing 37 skills
│   └── config/
│       ├── ai_capabilities.json        ← EXISTS
│       ├── deployment_targets.json     ← EXISTS
│       └── subagent_manifests.json     ← NEW: Sub-agent definitions
│
├── V9_ExternalRemote/                   ← Keep as-is for now
├── archived-versions/                   ← Move old versions here
└── [Root cleanup - move to appropriate folders]
```

---

## UNIVERSAL TASK SYSTEM (Works with ALL AIs)

### Claude Tasks Protocol Adaptation

Based on the article, Claude Code now has a native Tasks system stored in `~/.claude/tasks`. We'll create a **universal version** that works for ALL AIs, not just Claude.

### Implementation

**File**: `.agent/TASKS/MASTER_TASKS.json`

```json
{
  "task_list_id": "universal-or-strategy",
  "created": "2026-01-25T09:00:00Z",
  "last_updated": "2026-01-25T09:00:00Z",
  "compatible_with": ["claude", "gemini", "cursor", "windsurf", "any_ai"],
  "format_version": "1.0",

  "tasks": [
    {
      "id": "PROD_001",
      "title": "Protect V8_22 Production Version",
      "status": "pending",
      "priority": "critical",
      "assigned_to": "any_available_agent",
      "dependencies": [],
      "blockers": [],
      "sub_tasks": [
        "Create PRODUCTION/V8_22_STABLE directory",
        "Copy V8_22 files with metadata",
        "Create rollback instructions",
        "Test rollback procedure"
      ],
      "estimated_cost": "$0.01 (file ops via Gemini Flash)",
      "notes": "Must complete before any other work"
    },
    {
      "id": "V9_001",
      "title": "Restore V9 TOS RTD Live Numbers",
      "status": "pending",
      "priority": "high",
      "assigned_to": "subagent_tos_bridge",
      "dependencies": ["PROD_001"],
      "blockers": ["market_closed_until_0930"],
      "sub_tasks": [
        "Verify MILESTONE_LIVE_SUCCESS.cs is correct restore point",
        "Test with TOS RTD when market opens",
        "Identify what broke (compare with current version)",
        "Fix and re-test"
      ],
      "estimated_cost": "$0.15 (Opus debugging + Flash file ops)",
      "notes": "Claude attempted restore from commit 48bfa7e - needs testing"
    },
    {
      "id": "V9_002",
      "title": "V9 TOS RTD Bridge - Persistent Connection",
      "status": "pending",
      "priority": "high",
      "assigned_to": "subagent_tos_bridge",
      "dependencies": ["V9_001"],
      "blockers": [],
      "sub_tasks": [
        "Map CUSTOM4/CUSTOM6 fields to EMA9/EMA15",
        "Implement connection retry logic",
        "Add heartbeat monitoring",
        "Create diagnostic logging"
      ],
      "workspace": "DEVELOPMENT/V9_WIP/TOS_RTD_BRIDGE/",
      "estimated_cost": "$0.30 (Opus logic + Flash deploy)",
      "notes": "Sub-agent 1 responsibility"
    },
    {
      "id": "V9_003",
      "title": "V9 Copy Trading - External Orchestration",
      "status": "pending",
      "priority": "medium",
      "assigned_to": "subagent_copy_trading",
      "dependencies": ["V9_001"],
      "blockers": [],
      "sub_tasks": [
        "Extract copy trading from NinjaTrader",
        "Implement IPC between NT and V9 app",
        "Multi-account signal broadcasting",
        "Position sync across accounts"
      ],
      "workspace": "DEVELOPMENT/V9_WIP/COPY_TRADING/",
      "estimated_cost": "$0.80 (Opus architecture + Flash deploy)",
      "notes": "Sub-agent 2 responsibility"
    },
    {
      "id": "V9_004",
      "title": "V9 WPF UI - External Controls",
      "status": "pending",
      "priority": "medium",
      "assigned_to": "subagent_wpf_ui",
      "dependencies": ["V9_001"],
      "blockers": [],
      "sub_tasks": [
        "Move trade controls outside NinjaTrader",
        "Connect UI to V9 backend",
        "Implement hotkeys in WPF",
        "Real-time position display"
      ],
      "workspace": "DEVELOPMENT/V9_WIP/WPF_UI/",
      "estimated_cost": "$0.50 (Opus UI design + Flash XAML)",
      "notes": "Sub-agent 3 responsibility"
    },
    {
      "id": "MCP_001",
      "title": "Activate Gemini Flash MCP Bridge",
      "status": "pending",
      "priority": "high",
      "assigned_to": "any_available_agent",
      "dependencies": [],
      "blockers": [],
      "sub_tasks": [
        "Verify GEMINI_API_KEY in .env",
        "Test delegation_bridge.py with simple file op",
        "Create MCP_USAGE_PROTOCOL.md",
        "Update all workflows to enforce delegation"
      ],
      "estimated_cost": "$0.02 (setup only)",
      "notes": "Will save $2.60 over next 10 features"
    },
    {
      "id": "CTX_001",
      "title": "Create Shared Context System",
      "status": "pending",
      "priority": "high",
      "assigned_to": "any_available_agent",
      "dependencies": [],
      "blockers": [],
      "sub_tasks": [
        "Create .agent/SHARED_CONTEXT/ structure",
        "Implement CURRENT_SESSION.md template",
        "Create AGENT_HANDOFF.md workflow",
        "Set up automatic state updates"
      ],
      "estimated_cost": "$0.05 (Sonnet coordination + Flash files)",
      "notes": "Enables seamless IDE/agent switching"
    }
  ],

  "sub_agents": {
    "subagent_tos_bridge": {
      "name": "TOS RTD Bridge Agent",
      "responsibility": "ThinkorSwim RTD data integration",
      "tasks": ["V9_001", "V9_002"],
      "workspace": "DEVELOPMENT/V9_WIP/TOS_RTD_BRIDGE/",
      "preferred_ai": "opus_for_logic_flash_for_files",
      "task_list_file": ".agent/TASKS/V9_TOS_BRIDGE.json"
    },
    "subagent_copy_trading": {
      "name": "Copy Trading Agent",
      "responsibility": "Multi-account trade replication",
      "tasks": ["V9_003"],
      "workspace": "DEVELOPMENT/V9_WIP/COPY_TRADING/",
      "preferred_ai": "opus_for_logic_flash_for_files",
      "task_list_file": ".agent/TASKS/V9_COPY_TRADING.json"
    },
    "subagent_wpf_ui": {
      "name": "WPF UI Agent",
      "responsibility": "External control interface",
      "tasks": ["V9_004"],
      "workspace": "DEVELOPMENT/V9_WIP/WPF_UI/",
      "preferred_ai": "sonnet_for_ui_flash_for_xaml",
      "task_list_file": ".agent/TASKS/V9_WPF_UI.json"
    }
  },

  "usage": {
    "claude_code_cli": "CLAUDE_CODE_TASK_LIST_ID=universal-or-strategy claude",
    "any_other_ai": "Read .agent/TASKS/MASTER_TASKS.json and filter by assigned_to or status",
    "update_task": "Any AI can update status, add notes, mark blockers resolved",
    "broadcast": "All sessions working on same task list see updates in real-time"
  }
}
```

---

## GEMINI FLASH MCP BRIDGE ACTIVATION

### Current Status
- ✅ MCP server exists: `.agent/mcp-servers/delegation_bridge.py`
- ✅ Config exists: `.agent/config/ai_capabilities.json`
- ❌ Not being used (agents forget)

### Activation Steps

1. **Environment Setup**
```bash
# Check if GEMINI_API_KEY is set
cat .env | grep GEMINI_API_KEY

# If missing, add it:
echo "GEMINI_API_KEY=your_key_here" >> .env
```

2. **Test the Bridge**
```bash
# Start MCP server
python .agent/mcp-servers/delegation_bridge.py

# Test from any AI
call_gemini_flash(
  prompt: "List all .cs files in ${PROJECT_ROOT}/ that start with 'Universal'",
  context: ""
)
```

3. **Create Usage Protocol** (new file)

**File**: `.agent/mcp-servers/MCP_USAGE_PROTOCOL.md`

```markdown
# MCP USAGE PROTOCOL - MANDATORY FOR ALL AIS

## Rule: Delegate ALL File Operations to Gemini Flash

### When ANY AI (Claude, Gemini, Cursor, etc.) needs to:
- ✅ Save a file → DELEGATE to Gemini Flash via MCP
- ✅ Read a file for analysis → DELEGATE
- ✅ Update documentation → DELEGATE
- ✅ Deploy to NinjaTrader → DELEGATE
- ✅ List files/directories → DELEGATE

### How to Delegate (from any AI):

**Method 1: Via MCP Tool (if available)**
```
call_gemini_flash(
  prompt: "Save this code to ${PROJECT_ROOT}/PRODUCTION/V8_22_STABLE/UniversalORStrategyV8_22.cs",
  context: [full file content]
)
```

**Method 2: Via Direct Request (if MCP not available in IDE)**
```
User, please run this command in Claude Code CLI or Cursor:
[delegation instruction]
```

### Cost Savings
- Opus doing file op: $0.10
- Gemini Flash doing same: $0.0001
- **Savings: 99.9%**

### Enforcement
- All agents MUST check for MCP availability before file operations
- If available, MUST delegate (no exceptions)
- If not available, fall back to Haiku or manual
```

---

## SHARED CONTEXT SYSTEM

### File 1: CURRENT_SESSION.md

**Location**: `.agent/SHARED_CONTEXT/CURRENT_SESSION.md`

**Purpose**: Any agent can read this file and instantly know what's happening

**Template**:
```markdown
# CURRENT SESSION STATE
**Last Updated**: 2026-01-25 09:30 PST
**Updated By**: Claude Sonnet (Claude Code CLI)
**Market Status**: CLOSED (opens 06:30 PST / 09:30 EST)

## What We're Working On RIGHT NOW
- Task ID: V9_001
- Title: Restore V9 TOS RTD Live Numbers
- Status: In Progress
- Blocker: Market closed, testing at 09:30

## Production Status (V8)
- **Live Version**: V8_22 (protected in PRODUCTION/V8_22_STABLE/)
- **Last Test**: 2026-01-23 (successful)
- **Currently Trading**: NO (market closed)
- **Next Deployment**: Not planned

## Development Status (V9)
- **Current Build**: Attempting restore from commit 48bfa7e
- **Last Known Good**: UniversalOR_V9_MasterHub_MILESTONE_LIVE_SUCCESS.cs
- **What Broke**: TOS RTD live numbers stopped showing
- **Next Test**: When market opens at 09:30

## Active Sub-Agents
1. **TOS Bridge Agent**: Waiting for market open to test
2. **Copy Trading Agent**: Not started
3. **WPF UI Agent**: Not started

## Recent Changes (last 24h)
- 2026-01-24 20:00: Attempted V9 restore (Claude)
- 2026-01-25 09:00: Started project reorganization (Claude Sonnet)

## Next Steps
1. Test V9 restore when market opens (09:30)
2. If successful, protect as V9_STABLE
3. If failed, debug RTD connection

## Agent Handoff Notes
- If switching to Antigravity: Use Opus 4.5 for debugging RTD issue
- If switching to Cursor: Use Gemini Flash for file operations
- If switching to Windsurf: Check this file first for context
```

### File 2: AGENT_HANDOFF.md

**Location**: `.agent/SHARED_CONTEXT/AGENT_HANDOFF.md`

**Purpose**: Template for seamless agent switching

**Template**:
```markdown
# AGENT HANDOFF PROTOCOL

## When Leaving a Session

Before closing your IDE or switching agents:

1. **Update CURRENT_SESSION.md**
   - What were you working on?
   - What's the current status?
   - Any blockers?
   - What should happen next?

2. **Update Relevant Task File**
   - Mark task status (in_progress, blocked, completed)
   - Add any new sub-tasks discovered
   - Note any dependencies or blockers

3. **Commit to Git (if code changed)**
   - Use descriptive commit message
   - Reference task ID in commit
   - Don't push broken code

4. **Use Gemini Flash for Updates** (99% cheaper)
   ```
   call_gemini_flash(
     prompt: "Update .agent/SHARED_CONTEXT/CURRENT_SESSION.md with: [your updates]",
     context: ""
   )
   ```

## When Starting a Session

Before doing ANY work:

1. **Read CURRENT_SESSION.md**
   - What was the last agent working on?
   - Any blockers resolved?
   - What's the priority?

2. **Check MASTER_TASKS.json**
   - What tasks are assigned to you?
   - Any dependencies blocking you?
   - Update task status to "in_progress"

3. **Check Market Status**
   - Read MARKET_STATE.json
   - Don't test V9 TOS features if market closed

4. **Verify Production Safety**
   - V8_22 still protected in PRODUCTION/?
   - LAST_KNOWN_GOOD.json points to tested version?

## Cross-IDE Commands

### From Claude Code CLI to Antigravity
```
Currently working on: [task]
Status: [status]
Next step requires: Complex debugging (use Opus 4.5)
Context file: .agent/SHARED_CONTEXT/CURRENT_SESSION.md
```

### From Antigravity to Cursor (Gemini Flash)
```
Code complete, ready for deployment
File: [path]
Action: Deploy to PRODUCTION/V8_WIP/ first, test, then promote
Context file: .agent/SHARED_CONTEXT/CURRENT_SESSION.md
```

### From Any IDE to Any IDE
```
Read: .agent/SHARED_CONTEXT/CURRENT_SESSION.md
Read: .agent/TASKS/MASTER_TASKS.json
Filter tasks by: status="in_progress" OR assigned_to="me"
Continue from: [last checkpoint]
```
```

### File 3: LAST_KNOWN_GOOD.json

**Location**: `.agent/SHARED_CONTEXT/LAST_KNOWN_GOOD.json`

```json
{
  "v8_production": {
    "version": "V8_22",
    "file": "PRODUCTION/V8_22_STABLE/UniversalORStrategyV8_22.cs",
    "tested_date": "2026-01-23",
    "tested_by": "Mo (live trading)",
    "git_commit": "5c7dcdd",
    "status": "SAFE_TO_DEPLOY",
    "rollback_command": "cp PRODUCTION/V8_22_STABLE/UniversalORStrategyV8_22.cs 'C:/Users/Mohammed Khalid/Documents/NinjaTrader 8/bin/Custom/Strategies/'"
  },
  "v9_development": {
    "version": "V9_MILESTONE_LIVE_SUCCESS",
    "file": "PRODUCTION/V9_STABLE/UniversalOR_V9_MasterHub_MILESTONE_LIVE_SUCCESS.cs",
    "tested_date": "2026-01-23",
    "tested_by": "Claude (market closed - needs retest)",
    "git_commit": "48bfa7e",
    "status": "NEEDS_TESTING",
    "note": "Showed TOS RTD live numbers, then broke. Restore attempted, needs market open to verify."
  }
}
```

---

## NAMING PROTOCOL (Clarified)

### V8 Series (NinjaTrader Strategy)
```
UniversalORStrategyV8_[increment].cs
└─ Example: UniversalORStrategyV8_22.cs, V8_23.cs, V8_24.cs

Suffixes for branches:
- V8_22_UI_HORIZONTAL.cs (UI redesign)
- V8_22_FIX_STOPS.cs (bug fix)
- V8_22_TEST_SCALING.cs (experiment)
```

### V9 Series (External WPF App)
```
Project: V9_ExternalRemote
Main file: UniversalOR_V9_MasterHub.cs
Milestones: UniversalOR_V9_MasterHub_MILESTONE_[DESCRIPTION].cs
```

### Task Files
```
.agent/TASKS/[AREA]_[ID].json
└─ Examples:
   - MASTER_TASKS.json (all tasks)
   - V9_TOS_BRIDGE.json (sub-agent 1)
   - V9_COPY_TRADING.json (sub-agent 2)
```

---

## EXECUTION PLAN (Sequential Steps)

### Phase 1: Emergency Protection (DO NOW - Before Market Opens)
**Estimated Time**: 15 minutes
**Cost**: $0.02 (mostly Gemini Flash file ops)

1. ✅ Create PRODUCTION directory structure
2. ✅ Copy V8_22 to PRODUCTION/V8_22_STABLE/ with metadata
3. ✅ Copy V9 MILESTONE_LIVE_SUCCESS to PRODUCTION/V9_STABLE/
4. ✅ Create LAST_KNOWN_GOOD.json
5. ✅ Create ROLLBACK_INSTRUCTIONS.md
6. ✅ Test rollback procedure (dry run)

**Validation**: Can restore V8_22 in under 2 minutes if needed

### Phase 2: Task System Setup (Before Subagent Work)
**Estimated Time**: 20 minutes
**Cost**: $0.05 (Sonnet planning + Flash file ops)

1. ✅ Create .agent/TASKS/ directory
2. ✅ Write MASTER_TASKS.json (with all 7 tasks above)
3. ✅ Create sub-agent task files (V9_TOS_BRIDGE.json, etc.)
4. ✅ Write TASK_TEMPLATE.json for future tasks

**Validation**: Any AI can read MASTER_TASKS.json and understand project

### Phase 3: Shared Context Setup (Enable IDE Switching)
**Estimated Time**: 15 minutes
**Cost**: $0.03 (Flash file ops)

1. ✅ Create .agent/SHARED_CONTEXT/ directory
2. ✅ Write CURRENT_SESSION.md (populate with current state)
3. ✅ Write AGENT_HANDOFF.md (protocol template)
4. ✅ Write LAST_KNOWN_GOOD.json
5. ✅ Create V8_STATUS.json and V9_STATUS.json
6. ✅ Create MARKET_STATE.json

**Validation**: Switch from Claude Code CLI to Antigravity with zero context loss

### Phase 4: MCP Bridge Activation (Cost Savings)
**Estimated Time**: 10 minutes
**Cost**: $0.02 (setup + testing)

1. ✅ Verify .env has GEMINI_API_KEY
2. ✅ Test delegation_bridge.py with simple file read
3. ✅ Write MCP_USAGE_PROTOCOL.md
4. ✅ Test delegation from Claude and Cursor

**Validation**: File op costs drop from $0.10 to $0.0001

### Phase 5: V9 Testing (When Market Opens 09:30)
**Estimated Time**: 30 minutes
**Cost**: $0.20 (Opus debugging if needed)

1. ⏳ Launch V9_Milestone_FINAL.exe
2. ⏳ Verify TOS RTD connection (green LED)
3. ⏳ Check if EMA9/EMA15 show live numbers
4. ⏳ If working: Protect as V9_STABLE
5. ⏳ If broken: Sub-agent TOS Bridge takes over (Task V9_001)

**Validation**: Live numbers from TOS appear in V9 app

### Phase 6: Sub-Agent Deployment (After V9 Test)
**Estimated Time**: Variable (ongoing work)
**Cost**: $0.50-2.00 depending on scope

1. ⏳ Spawn Sub-Agent 1 (TOS Bridge) with task list V9_TOS_BRIDGE.json
2. ⏳ Spawn Sub-Agent 2 (Copy Trading) with task list V9_COPY_TRADING.json
3. ⏳ Spawn Sub-Agent 3 (WPF UI) with task list V9_WPF_UI.json
4. ⏳ Each agent updates their task status independently
5. ⏳ All agents broadcast updates to shared context

**Validation**: Multiple agents work on V9 simultaneously without conflicts

### Phase 7: Cleanup & Archive (After Everything Works)
**Estimated Time**: 20 minutes
**Cost**: $0.02 (Flash file moves)

1. ⏳ Move old V8 versions (V8_1 through V8_21) to archived-versions/
2. ⏳ Move orphan files to DEVELOPMENT/EXPERIMENTS/
3. ⏳ Update README.md with new structure
4. ⏳ Update AGENT.md with V9 details

**Validation**: Root directory clean, only active work visible

---

## SUCCESS CRITERIA

### Must Have (Before Market Opens)
- [x] V8_22 protected in PRODUCTION/ (can rollback in 2 min)
- [x] V9 MILESTONE restore point protected
- [x] MASTER_TASKS.json created (visible to all AIs)
- [x] CURRENT_SESSION.md exists (switchable context)
- [ ] Gemini Flash MCP bridge tested (cost savings active)

### Should Have (Within Today)
- [ ] V9 TOS RTD tested (live numbers working/broken identified)
- [ ] Sub-agent 1 (TOS Bridge) deployed with task list
- [ ] Agent handoff tested (Claude CLI → Antigravity → Cursor)

### Nice to Have (This Week)
- [ ] All 3 sub-agents working independently
- [ ] Copy trading external orchestration working
- [ ] WPF UI controls functional
- [ ] Root directory cleaned up

---

## DEPENDENCIES & BLOCKERS

### Current Blockers
1. **Market Closed**: Can't test V9 TOS RTD until 09:30 PST
2. **GEMINI_API_KEY**: Need to verify in .env before MCP bridge works

### Task Dependencies
```
PROD_001 (Protect V8_22)
  └─ No dependencies (DO FIRST)

MCP_001 (Activate MCP Bridge)
  └─ No dependencies (DO SECOND for cost savings)

V9_001 (Restore V9)
  ├─ Dependency: PROD_001 (need rollback safety)
  └─ Blocker: market_closed_until_0930

V9_002, V9_003, V9_004 (Sub-agent work)
  └─ Dependency: V9_001 (need working baseline)

CTX_001 (Shared Context)
  └─ No dependencies (DO EARLY for IDE switching)
```

---

## COST ESTIMATES

### Setup Phases (1-4)
- Protection: $0.02 (Flash file ops)
- Task System: $0.05 (Sonnet + Flash)
- Shared Context: $0.03 (Flash)
- MCP Bridge: $0.02 (testing)
- **Total Setup**: $0.12

### V9 Work (Phases 5-6)
- Testing: $0.20 (Opus debugging if broken)
- Sub-Agent 1: $0.30 (TOS Bridge)
- Sub-Agent 2: $0.80 (Copy Trading)
- Sub-Agent 3: $0.50 (WPF UI)
- **Total V9**: $1.80

### Cleanup (Phase 7)
- Archive: $0.02 (Flash)
- **Total Cleanup**: $0.02

### Grand Total: ~$2.00 for complete reorganization

### With MCP Bridge Active: Save $2.60 over next 10 features
- **Break-even**: After ~8 features
- **ROI**: 130% within 2 weeks

---

## AGENT RESPONSIBILITIES

### Any Agent Can:
- Read MASTER_TASKS.json to see all work
- Update CURRENT_SESSION.md when switching
- Protect production files (V8_22)
- Test MCP bridge
- Create shared context files

### Opus 4.5 (Antigravity) Should:
- Debug complex V9 TOS RTD issues
- Design copy trading architecture
- Handle critical bug fixes

### Sonnet (Claude Code CLI) Should:
- Coordinate sub-agents
- Update task status
- Generate handoff prompts
- Plan work breakdown

### Gemini Flash (Cursor/MCP) Should:
- All file operations
- Deploy to NinjaTrader
- Update documentation
- Archive old versions

### Sub-Agents Should:
- Focus only on assigned tasks
- Update their specific task list file
- Broadcast completion to shared context
- Not touch production files

---

## SOURCES

Based on the Claude Tasks protocol announced by Anthropic:
- [Anthropic Tasks Announcement](https://twitter.com/trq212) - Tasks are stored in filesystem, can be shared across sessions, support dependencies
- [Agent Skills Open Standard](https://aibusiness.com/foundation-models/anthropic-launches-skills-open-standard-claude) - Skills work across all AI models
- [Model Context Protocol](https://www.superhuman.ai/p/anthropic-releases-cowork-claude-code-for-everyday-tasks) - MCP enables data sharing across tools

---

## NEXT IMMEDIATE ACTIONS

**Right now (Sequential)**:
1. Create PRODUCTION/ directory structure
2. Protect V8_22 and V9 MILESTONE files
3. Create MASTER_TASKS.json
4. Create CURRENT_SESSION.md
5. Verify GEMINI_API_KEY and test MCP bridge

**When market opens (09:30 PST)**:
6. Test V9 TOS RTD restore
7. Deploy sub-agents if V9 works

**This is the foundation for "wearing the project" - any agent, any IDE, any time.**
