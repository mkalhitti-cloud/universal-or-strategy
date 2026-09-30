# Loop Orchestration - How All Loops Work Together

**Purpose**: Document how `/pr-loop`, `/epic-loop`, and Greptile integration work together in the V12 workflow.

---

## Available Loops

### 1. `/pr-loop` - PR Perfection Loop ✅
**Purpose**: Drive a single PR to 100/100 Project Health Score  
**Location**: `.bob/commands/pr-loop.md`  
**Trigger**: Manual (`/pr-loop <PR_NUMBER>`)

**What It Does**:
1. Extracts bot findings from GitHub PR
2. Extracts VS Code Problems panel (117 issues)
3. Extracts VS Code Comments panel (105 comments)
4. Categorizes: VALID-FIX / VALID-SUPPRESS / HALLUCINATION / INFRA-NOISE
5. Deduplicates across all sources
6. Applies fixes in priority order (P0 → P1 → P2)
7. Runs pre-push validation
8. Pushes and monitors bot checks
9. Repeats until PHS = 100/100

**Integration Points**:
- Uses Greptile MCP for code search
- Uses CodeAnt MCP for review
- Uses jCodemunch MCP for symbol analysis
- Reads Problems panel via `extract_vscode_problems.ps1`
- Reads Comments panel via `extract_vscode_comments.ps1`

---

### 2. `/epic-loop` - Multi-Epic Orchestration ✅
**Purpose**: Execute multiple consecutive epics autonomously  
**Location**: `.bob/commands/epic-loop.md`  
**Trigger**: Manual (`/epic-loop [start] [end]`)

**What It Does**:
1. Runs pre-flight validation (GODMODE checks)
2. For each epic in range:
   - Delegates to `/epic-run` (6-phase protocol)
   - Runs `/pr-loop` after each ticket
   - Verifies complexity reduction
3. Runs post-loop verification

**Integration Points**:
- Calls `/epic-run` for each epic
- Calls `/pr-loop` after each ticket commit
- Uses jCodemunch for complexity analysis
- Uses Jane Street KB for validation

---

### 3. `/epic-run` - Single Epic Orchestration ✅
**Purpose**: Execute one epic end-to-end (planning → execution → verification)  
**Location**: `.bob/commands/epic-run.md`  
**Trigger**: Manual (`/epic-run <epic-slug> <description>`) or via `/epic-loop`

**What It Does**:
1. **Phase 0**: Hotspot analysis (CodeScene)
2. **Phase 1**: Intake (scope definition)
3. **Phase 2**: Plan (approach design)
4. **Phase 2.3**: Scan (Sentinel audit via Greptile)
5. **Phase 3**: Validate (triple-agent review)
6. **Phase 4**: Tickets (execution plan)
7. **Phase 5**: Execution (ticket loop with `/pr-loop`)
8. **Phase 6**: PR submission + perfection

**Integration Points**:
- Uses Greptile MCP in Phase 2.3 (Sentinel audit)
- Calls `/pr-loop` after each ticket (Phase 5, Step F)
- Uses jCodemunch for symbol analysis
- Uses Jane Street KB for validation

---

## Greptile Integration

### Greptile MCP Server
**Status**: ⚠️ Authentication error (token expired)  
**Location**: `.bob/mcp.json`  
**Tools Available**:
- `list_custom_context` - List organization custom context
- `search_custom_context` - Search custom context by content
- `list_merge_requests` - List PRs/MRs
- `get_merge_request` - Get detailed PR info
- `list_merge_request_comments` - Get all PR comments
- `list_code_reviews` - List code reviews
- `get_code_review` - Get detailed review
- `trigger_code_review` - Trigger new review
- `search_greptile_comments` - Search Greptile review comments

### Greptile CLI
**Status**: ✅ Available as fallback  
**Installation**: `npm install -g @greptile/cli`  
**Usage**:
```bash
# Search codebase
greptile search "ProcessBracketEvent"

# Review PR
greptile review --pr 6

# List reviews
greptile list-reviews
```

### Integration in Workflows

#### In `/epic-run` Phase 2.3 (Sentinel Audit)
```markdown
**Switch to: v12-epic-planner mode**

Hand off:
```
EPIC: $1
TASK: Run /epic-scan
INPUT: @docs/brain/$1/01-analysis.md @docs/brain/$1/02-approach.md
OUTPUT: Write docs/brain/$1/02-greptile-report.md

PROTOCOL:
  1. Use Greptile MCP to search for similar patterns:
     - use_mcp_tool: greptile / search_custom_context
     - Query: "Extract method pattern for {target_method}"
  
  2. Use Greptile MCP to find related code reviews:
     - use_mcp_tool: greptile / search_greptile_comments
     - Query: "Complexity reduction {target_method}"
  
  3. Analyze findings for semantic gaps
  4. Generate Sentinel verdict
  5. STOP at [SENTINEL-GATE]
```
```

#### In `/pr-loop` Step 1 (Bot Forensics)
```markdown
**Switch to: Advanced mode**

Hand off:
```
TASK: Extract and Categorize Bot Findings with Greptile Integration
PR: $1

PROTOCOL:
  1. Extract GitHub PR comments:
     powershell -File .\scripts\extract_pr_forensics.ps1 -PrNumber $1
  
  2. Extract Greptile review comments via MCP:
     use_mcp_tool: greptile / list_merge_request_comments
     Args: { name: "owner/repo", remote: "github", prNumber: $1 }
  
  3. Extract VS Code Problems panel:
     powershell -File .\scripts\extract_vscode_problems.ps1
  
  4. Extract VS Code Comments panel:
     powershell -File .\scripts\extract_vscode_comments.ps1
  
  5. Merge all sources and deduplicate:
     python scripts\deduplicate_findings.py
  
  6. Categorize with Jane Street alignment:
     python scripts\categorize_problems.py
  
  7. Generate unified fix queue
  8. Emit: [FORENSICS-READY]
```
```

---

## Loop Execution Flow

### Scenario 1: Single PR Review
```
User: /pr-loop 6

┌─────────────────────────────────────┐
│ /pr-loop                            │
├─────────────────────────────────────┤
│ Step 1: Extract Findings            │
│ ├─ GitHub PR comments               │
│ ├─ Greptile MCP comments            │
│ ├─ VS Code Problems (117)           │
│ ├─ VS Code Comments (105)           │
│ └─ Deduplicate + Categorize         │
│                                     │
│ Step 2: Apply Fixes                 │
│ ├─ P0 issues (critical)             │
│ ├─ P1 issues (high)                 │
│ └─ P2 issues (medium)               │
│                                     │
│ Step 3: Push + Monitor              │
│ ├─ Run pre-push validation          │
│ ├─ Push to GitHub                   │
│ └─ Wait for bot checks              │
│                                     │
│ Step 4: Check PHS                   │
│ ├─ If < 100: GOTO Step 1            │
│ └─ If = 100: DONE                   │
└─────────────────────────────────────┘
```

### Scenario 2: Single Epic Execution
```
User: /epic-run EPIC-CCN-15 "Extract ProcessBracketEvent"

┌─────────────────────────────────────┐
│ /epic-run                           │
├─────────────────────────────────────┤
│ Phase 0: Hotspot Analysis           │
│ Phase 1: Intake                     │
│ Phase 2: Plan                       │
│ Phase 2.3: Sentinel Audit           │
│ ├─ Greptile MCP: search patterns    │
│ ├─ Greptile MCP: find reviews       │
│ └─ Generate verdict                 │
│ Phase 3: Validate                   │
│ Phase 4: Tickets                    │
│                                     │
│ Phase 5: Execution (Ticket Loop)    │
│ ├─ For each ticket:                 │
│ │  ├─ Plan extraction               │
│ │  ├─ Execute extraction            │
│ │  ├─ Verify (pre-push)             │
│ │  ├─ F5 Gate (manual)              │
│ │  ├─ Commit                        │
│ │  └─ /pr-loop <PR> ← INTEGRATION   │
│ └─ All tickets complete             │
│                                     │
│ Phase 6: PR Submission              │
│ └─ /pr-loop <PR> ← INTEGRATION      │
└─────────────────────────────────────┘
```

### Scenario 3: Multi-Epic Autonomous Execution
```
User: /epic-loop 15 45

┌─────────────────────────────────────┐
│ /epic-loop                          │
├─────────────────────────────────────┤
│ Pre-Flight: GODMODE validation      │
│                                     │
│ For epic in [15..45]:               │
│ ├─ /epic-run EPIC-CCN-{N}           │
│ │  ├─ Phase 0-4: Planning           │
│ │  ├─ Phase 5: Execution            │
│ │  │  └─ /pr-loop per ticket        │
│ │  └─ Phase 6: PR submission        │
│ │     └─ /pr-loop final             │
│ └─ Checkpoint                       │
│                                     │
│ Post-Loop: Verification             │
│ ├─ Complexity audit                 │
│ ├─ Jane Street audit                │
│ └─ Hard link sync                   │
└─────────────────────────────────────┘
```

---

## Greptile Loop Integration (Proposed)

### `/greptile-loop` - Continuous Code Review Loop

**Purpose**: Continuously monitor and review code changes using Greptile  
**Status**: ⬜ Not yet implemented  
**Proposed Location**: `.bob/commands/greptile-loop.md`

**Proposed Functionality**:
```markdown
# /greptile-loop - Continuous Greptile Review

**Purpose**: Monitor codebase and trigger Greptile reviews automatically

## Usage
```
/greptile-loop [--watch] [--interval <minutes>]
```

## Protocol

1. **Watch Mode** (if --watch):
   - Monitor file changes in src/
   - On change detected:
     - Wait for file save
     - Trigger Greptile review via MCP
     - Extract findings
     - Add to Problems panel
     - Notify user

2. **Interval Mode** (if --interval):
   - Every N minutes:
     - Check for uncommitted changes
     - If changes exist:
       - Trigger Greptile review
       - Extract findings
       - Update Problems panel

3. **Integration with /pr-loop**:
   - Greptile findings automatically included in Step 1
   - No manual extraction needed
   - Real-time feedback loop

## Example

```bash
# Start continuous monitoring
/greptile-loop --watch

# Or periodic reviews every 15 minutes
/greptile-loop --interval 15
```

## Benefits

✅ Real-time Greptile feedback while coding  
✅ Automatic integration with /pr-loop  
✅ No manual review triggering  
✅ Continuous quality monitoring  
```

---

## Current Loop Integration Status

| Loop | Status | Greptile Integration | Problems/Comments Integration |
|------|--------|---------------------|------------------------------|
| `/pr-loop` | ✅ Exists | ⚠️ Manual (MCP auth issue) | ✅ Documented |
| `/epic-loop` | ✅ Exists | ✅ Via `/epic-run` Phase 2.3 | ✅ Via `/pr-loop` |
| `/epic-run` | ✅ Exists | ✅ Phase 2.3 Sentinel audit | ✅ Via `/pr-loop` |
| `/greptile-loop` | ⬜ Proposed | ✅ Core functionality | ✅ Would integrate |

---

## Recommended Implementation Order

1. ✅ **Fix Greptile MCP authentication** (regenerate token)
2. ✅ **Test `/pr-loop` with Greptile MCP** (verify Step 1 extraction)
3. ✅ **Test `/epic-run` Phase 2.3** (verify Sentinel audit)
4. ⬜ **Implement `/greptile-loop`** (continuous monitoring)
5. ⬜ **Integrate `/greptile-loop` into `/pr-loop`** (automatic findings)

---

## Summary

**Current State**:
- `/pr-loop` ✅ - Integrates GitHub + VS Code Problems/Comments
- `/epic-loop` ✅ - Calls `/epic-run` + `/pr-loop`
- `/epic-run` ✅ - Uses Greptile in Phase 2.3 + calls `/pr-loop`
- Greptile MCP ⚠️ - Available but needs auth fix

**Proposed Enhancement**:
- `/greptile-loop` ⬜ - Continuous monitoring + automatic integration

**Integration Flow**:
```
Typing → On-Save → Pre-Commit → Pre-Push → /pr-loop → GitHub PR
   ↓         ↓          ↓           ↓           ↓
Problems  Problems   Problems    Problems   Greptile
 Panel     Panel      Panel       Panel      Comments
   ↓         ↓          ↓           ↓           ↓
   └─────────┴──────────┴───────────┴───────────┘
                        ↓
              Unified Fix Queue
                        ↓
                  /pr-loop Step 2
                   (Apply Fixes)
```

All loops are designed to work together, with `/pr-loop` as the central integration point for all findings (GitHub, VS Code, Greptile).