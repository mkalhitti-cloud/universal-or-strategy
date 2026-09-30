# Linear Integration Protocol (V12.22)

## Overview
Automatically sync all V12 EPICs, tickets, and progress to Linear for centralized project tracking. Integrates with `epic-run` and `epic-tdd` workflows to create Linear issues, update status, and track completion.

---

## Linear Workspace Configuration

### Current Setup
- **Primary Account**: `backtothefutures83@gmail.com`
- **Collaborator**: `mkalhitti` (invited by primary account)
- **Team**: "Universal Or Strategy" (UNI)
- **Team ID**: Extract from Linear API
- **Current Projects**:
  - "V12 Universal OR Strategy - Build 1111.007-phase7-11" (15 issues)
  - "Morpheus OS Sovereign Substrate" (2 issues)

### API Access
- **API Key**: Generate from `backtothefutures83@gmail.com` account
- **API Endpoint**: `https://api.linear.app/graphql`
- **Documentation**: https://developers.linear.app/docs/graphql/working-with-the-graphql-api
- **Storage**: Store in `.env` as `LINEAR_API_KEY`

### Account Migration Notes
- Old account (`mkalhitti`) still has access as collaborator
- All new work tracked under `backtothefutures83@gmail.com` workspace
- API key must be generated from primary account for full permissions

---

## Epic-to-Linear Mapping

### Epic Structure in Linear

**Project** (Linear) = **Epic** (V12)
```
Project: "EPIC-8: OnKeyDown + ProcessIpc Extraction"
├── Issue: "EPIC-8-ticket-01: Extract OnKeyDown command handlers"
├── Issue: "EPIC-8-ticket-02: Extract ProcessIpc command handlers"
└── Issue: "EPIC-8-ticket-03: Integration testing"
```

### Status Mapping

| V12 Phase | Linear Status | Linear State |
|-----------|---------------|--------------|
| Phase 0: Jane Street Baseline | Backlog | `backlog` |
| Phase 1: Intake | In Progress | `started` |
| Phase 2: Plan | In Progress | `started` |
| Phase 2.3: Sentinel Audit | In Review | `in_review` |
| Phase 2.5: Architectural Review | In Review | `in_review` |
| Phase 3: Validate | In Progress | `started` |
| Phase 4: Tickets | In Progress | `started` |
| Phase 4.5: Ticket Quality Review | In Review | `in_review` |
| Execution: Ticket Loop | In Progress | `started` |
| Step B.5: Implementation Review | In Review | `in_review` |
| Step C: Verification | In Review | `in_review` |
| Step D: F5 Gate | Blocked | `blocked` (waiting for Director) |
| Phase 6: PR Submission | In Review | `in_review` |
| Phase 6.5: Final Validation | In Review | `in_review` |
| Phase 7: Perfection Loop | In Progress | `started` |
| Complete | Done | `completed` |

### Priority Mapping

| V12 Epic Priority | Linear Priority |
|-------------------|-----------------|
| EPIC-8 through EPIC-14 (Complexity) | High (P1) |
| EPIC-15 through EPIC-17 (Tests) | Medium (P2) |
| EPIC-18, EPIC-19 (Codacy) | Medium (P2) |
| EPIC-20, EPIC-21 (Quality) | Low (P3) |

---

## Integration Points

### 1. Epic Creation (Phase 0)

**Trigger**: `epic-run` or `epic-tdd` starts Phase 0

**Action**: Create Linear Project + Parent Issue
```graphql
mutation CreateEpicProject {
  projectCreate(input: {
    name: "EPIC-8: OnKeyDown + ProcessIpc Extraction"
    description: "Extract high-complexity methods from V12_002.cs to reduce CYC"
    teamId: "UNI_TEAM_ID"
    state: "planned"
    priority: 1
    targetDate: "2026-06-15"
  }) {
    project {
      id
      url
    }
  }
}
```

**Store**: Save `project.id` in `docs/brain/epic-8/00-linear-project.json`

---

### 2. Ticket Creation (Phase 4)

**Trigger**: Phase 4 completes, tickets generated

**Action**: Create Linear Issues for each ticket
```graphql
mutation CreateTicketIssue {
  issueCreate(input: {
    title: "EPIC-8-ticket-01: Extract OnKeyDown command handlers"
    description: "Extract OnKeyDown command handlers to reduce CYC from 45 to 12"
    teamId: "UNI_TEAM_ID"
    projectId: "EPIC_8_PROJECT_ID"
    state: "backlog"
    priority: 1
    estimate: 2  # Story points (hours / 2)
    labels: ["epic-8", "complexity-extraction", "s1-sima-core"]
  }) {
    issue {
      id
      identifier  # e.g., "UNI-123"
      url
    }
  }
}
```

**Store**: Save `issue.id` and `issue.identifier` in `docs/brain/epic-8/ticket-01-linear.json`

---

### 3. Status Updates (Per Phase)

**Trigger**: Each phase completes

**Action**: Update Linear Issue status
```graphql
mutation UpdateTicketStatus {
  issueUpdate(
    id: "TICKET_ISSUE_ID"
    input: {
      state: "started"  # or "in_review", "completed"
    }
  ) {
    issue {
      id
      state
    }
  }
}
```

**Frequency**: After each gate (9 times per ticket)

---

### 4. Progress Tracking (Real-Time)

**Trigger**: Ticket loop advances

**Action**: Add comment to Linear Issue
```graphql
mutation AddProgressComment {
  commentCreate(input: {
    issueId: "TICKET_ISSUE_ID"
    body: "✅ Phase 2.5: Architectural Review PASSED\n- CYC target: 45 → 12\n- Jane Street compliance: VERIFIED\n- Next: Phase 3 (Validate)"
  }) {
    comment {
      id
    }
  }
}
```

**Frequency**: After each autonomous gate

---

### 5. F5 Gate Notification (Manual)

**Trigger**: Step D (F5 Gate) reached

**Action**: Update Linear Issue + notify Director
```graphql
mutation NotifyF5Gate {
  issueUpdate(
    id: "TICKET_ISSUE_ID"
    input: {
      state: "blocked"
    }
  ) {
    issue {
      id
    }
  }
  
  commentCreate(input: {
    issueId: "TICKET_ISSUE_ID"
    body: "⏸️ F5 GATE: Waiting for Director verification\n- All automated gates: PASSED\n- CYC: 45 → 12\n- Jane Street DNA: PASS\n- Action: Press F5 in NinjaTrader"
  }) {
    comment {
      id
    }
  }
}
```

**Notification**: Linear sends notification to Director (backtothefutures83@gmail.com)

---

### 6. Ticket Completion (Step I)

**Trigger**: Ticket complete, PR at 100/100 PHS

**Action**: Mark Linear Issue as Done
```graphql
mutation CompleteTicket {
  issueUpdate(
    id: "TICKET_ISSUE_ID"
    input: {
      state: "completed"
      completedAt: "2026-05-31T01:30:00Z"
    }
  ) {
    issue {
      id
      state
    }
  }
  
  commentCreate(input: {
    issueId: "TICKET_ISSUE_ID"
    body: "✅ TICKET COMPLETE\n- PR #8: 100/100 PHS\n- CYC: 45 → 12 (achieved)\n- Jane Street: COMPLIANT\n- Build Tag: BUILD_TAG_001"
  }) {
    comment {
      id
    }
  }
}
```

---

### 7. Epic Completion (Phase 7)

**Trigger**: All tickets complete, epic done

**Action**: Mark Linear Project as Complete
```graphql
mutation CompleteEpic {
  projectUpdate(
    id: "EPIC_PROJECT_ID"
    input: {
      state: "completed"
      completedAt: "2026-05-31T02:00:00Z"
    }
  ) {
    project {
      id
      state
    }
  }
}
```

**Summary Comment**:
```graphql
mutation AddEpicSummary {
  commentCreate(input: {
    issueId: "EPIC_PARENT_ISSUE_ID"
    body: "🎉 EPIC-8 COMPLETE\n\n**Tickets**: 3/3 (100%)\n**CYC Reduction**: 135 → 36 (73% reduction)\n**PRs**: #8 (100/100 PHS)\n**Jane Street**: COMPLIANT\n**Time**: 2 hours 15 minutes\n\n**Independent Reviews**: 9 gates per ticket (27 total)\n**F5 Gates**: 3 (all passed)\n**Build Tags**: BUILD_TAG_001, BUILD_TAG_002, BUILD_TAG_003"
  }) {
    comment {
      id
    }
  }
}
```

---

## Automation Script

### Linear Sync Script (`scripts/linear_sync.py`)

```python
#!/usr/bin/env python3
"""
Linear Integration Script for V12 Epic Workflow
Syncs EPICs, tickets, and progress to Linear workspace
Primary Account: backtothefutures83@gmail.com
"""

import os
import json
import requests
from datetime import datetime
from pathlib import Path

LINEAR_API_URL = "https://api.linear.app/graphql"
LINEAR_API_KEY = os.getenv("LINEAR_API_KEY")
TEAM_ID = os.getenv("LINEAR_TEAM_ID", "UNI")  # Universal Or Strategy team

class LinearSync:
    def __init__(self):
        if not LINEAR_API_KEY:
            raise ValueError("LINEAR_API_KEY not found in environment. Generate from backtothefutures83@gmail.com account.")
        
        self.headers = {
            "Authorization": LINEAR_API_KEY,
            "Content-Type": "application/json"
        }
    
    def graphql_request(self, query, variables=None):
        """Execute GraphQL request to Linear API"""
        response = requests.post(
            LINEAR_API_URL,
            headers=self.headers,
            json={"query": query, "variables": variables or {}}
        )
        response.raise_for_status()
        return response.json()
    
    def create_epic_project(self, epic_slug, title, description, priority=1):
        """Create Linear Project for Epic"""
        query = """
        mutation CreateProject($input: ProjectCreateInput!) {
          projectCreate(input: $input) {
            project {
              id
              url
              identifier
            }
          }
        }
        """
        variables = {
            "input": {
                "name": f"{epic_slug.upper()}: {title}",
                "description": description,
                "teamId": TEAM_ID,
                "state": "planned",
                "priority": priority,
                "targetDate": self.calculate_target_date(epic_slug)
            }
        }
        result = self.graphql_request(query, variables)
        project = result["data"]["projectCreate"]["project"]
        
        # Save project ID
        self.save_linear_metadata(epic_slug, "project", project)
        return project
    
    def create_ticket_issue(self, epic_slug, ticket_num, title, description, estimate=2):
        """Create Linear Issue for Ticket"""
        # Load project ID
        project_id = self.load_linear_metadata(epic_slug, "project")["id"]
        
        query = """
        mutation CreateIssue($input: IssueCreateInput!) {
          issueCreate(input: $input) {
            issue {
              id
              identifier
              url
            }
          }
        }
        """
        variables = {
            "input": {
                "title": f"{epic_slug.upper()}-ticket-{ticket_num:02d}: {title}",
                "description": description,
                "teamId": TEAM_ID,
                "projectId": project_id,
                "state": "backlog",
                "priority": 1,
                "estimate": estimate,
                "labels": [epic_slug, "complexity-extraction"]
            }
        }
        result = self.graphql_request(query, variables)
        issue = result["data"]["issueCreate"]["issue"]
        
        # Save issue ID
        self.save_linear_metadata(epic_slug, f"ticket-{ticket_num:02d}", issue)
        return issue
    
    def update_ticket_status(self, epic_slug, ticket_num, state, comment=None):
        """Update Linear Issue status"""
        issue_id = self.load_linear_metadata(epic_slug, f"ticket-{ticket_num:02d}")["id"]
        
        query = """
        mutation UpdateIssue($id: String!, $input: IssueUpdateInput!) {
          issueUpdate(id: $id, input: $input) {
            issue {
              id
              state
            }
          }
        }
        """
        variables = {
            "id": issue_id,
            "input": {"state": state}
        }
        self.graphql_request(query, variables)
        
        # Add comment if provided
        if comment:
            self.add_comment(issue_id, comment)
    
    def add_comment(self, issue_id, body):
        """Add comment to Linear Issue"""
        query = """
        mutation CreateComment($input: CommentCreateInput!) {
          commentCreate(input: $input) {
            comment {
              id
            }
          }
        }
        """
        variables = {
            "input": {
                "issueId": issue_id,
                "body": body
            }
        }
        self.graphql_request(query, variables)
    
    def save_linear_metadata(self, epic_slug, key, data):
        """Save Linear metadata to docs/brain/{epic}/linear-{key}.json"""
        brain_dir = Path(f"docs/brain/{epic_slug}")
        brain_dir.mkdir(parents=True, exist_ok=True)
        
        metadata_file = brain_dir / f"linear-{key}.json"
        with open(metadata_file, "w") as f:
            json.dump(data, f, indent=2)
    
    def load_linear_metadata(self, epic_slug, key):
        """Load Linear metadata from docs/brain/{epic}/linear-{key}.json"""
        metadata_file = Path(f"docs/brain/{epic_slug}/linear-{key}.json")
        with open(metadata_file, "r") as f:
            return json.load(f)
    
    def calculate_target_date(self, epic_slug):
        """Calculate target date based on epic number"""
        # EPIC-8 through EPIC-14: 2 weeks from now
        # EPIC-15 through EPIC-21: 4 weeks from now
        from datetime import timedelta
        epic_num = int(epic_slug.split("-")[1])
        weeks = 2 if epic_num <= 14 else 4
        target = datetime.now() + timedelta(weeks=weeks)
        return target.strftime("%Y-%m-%d")

# CLI Interface
if __name__ == "__main__":
    import sys
    
    sync = LinearSync()
    command = sys.argv[1] if len(sys.argv) > 1 else "help"
    
    if command == "create-epic":
        epic_slug = sys.argv[2]
        title = sys.argv[3]
        description = sys.argv[4] if len(sys.argv) > 4 else ""
        project = sync.create_epic_project(epic_slug, title, description)
        print(f"Created project: {project['url']}")
    
    elif command == "create-ticket":
        epic_slug = sys.argv[2]
        ticket_num = int(sys.argv[3])
        title = sys.argv[4]
        description = sys.argv[5] if len(sys.argv) > 5 else ""
        issue = sync.create_ticket_issue(epic_slug, ticket_num, title, description)
        print(f"Created issue: {issue['identifier']} - {issue['url']}")
    
    elif command == "update-status":
        epic_slug = sys.argv[2]
        ticket_num = int(sys.argv[3])
        state = sys.argv[4]
        comment = sys.argv[5] if len(sys.argv) > 5 else None
        sync.update_ticket_status(epic_slug, ticket_num, state, comment)
        print(f"Updated {epic_slug}-ticket-{ticket_num:02d} to {state}")
    
    else:
        print("Usage:")
        print("  python scripts/linear_sync.py create-epic <epic-slug> <title> [description]")
        print("  python scripts/linear_sync.py create-ticket <epic-slug> <ticket-num> <title> [description]")
        print("  python scripts/linear_sync.py update-status <epic-slug> <ticket-num> <state> [comment]")
```

---

## Integration with Epic Commands

### Update `epic-run.md` and `epic-tdd.md`

Add Linear sync calls at key phases:

**Phase 0: Create Epic Project**
```markdown
**Step 0.5 -- Switch to: Advanced mode (Linear sync)**

After Jane Street baseline audit, create Linear project:
```bash
python scripts/linear_sync.py create-epic $1 "$2" "$(cat docs/brain/$1/00-scope.md)"
```
```

**Phase 4: Create Ticket Issues**
```markdown
**Step 4.5 -- Switch to: Advanced mode (Linear sync)**

After tickets generated, create Linear issues:
```bash
for ticket in docs/brain/$1/ticket-*.md; do
  ticket_num=$(echo $ticket | grep -oP 'ticket-\K\d+')
  title=$(grep "^# " $ticket | head -1 | sed 's/^# //')
  python scripts/linear_sync.py create-ticket $1 $ticket_num "$title" "$(cat $ticket)"
done
```
```

**Per Ticket: Update Status**
```markdown
**Step B.5 -- After implementation review**
```bash
python scripts/linear_sync.py update-status $1 XX "in_review" "✅ Implementation review PASSED"
```

**Step D -- F5 Gate**
```bash
python scripts/linear_sync.py update-status $1 XX "blocked" "⏸️ F5 GATE: Waiting for Director"
```

**Step I -- Ticket Complete**
```bash
python scripts/linear_sync.py update-status $1 XX "completed" "✅ TICKET COMPLETE - PR #X at 100/100 PHS"
```
```

---

## Environment Setup

### 1. Get Linear API Key (Primary Account)

**IMPORTANT**: Generate API key from `backtothefutures83@gmail.com` account (NOT `mkalhitti`)

1. Log in to Linear as `backtothefutures83@gmail.com`
2. Go to https://linear.app/settings/api
3. Click "Create new API key"
4. Name: "V12 Epic Automation"
5. Scope: `write` (full access)
6. Copy key to `.env`:

```bash
# .env
LINEAR_API_KEY=lin_api_xxxxxxxxxxxxxxxxxxxxx
LINEAR_TEAM_ID=UNI  # Universal Or Strategy team
```

### 2. Install Dependencies

```bash
pip install requests python-dotenv
```

### 3. Test Connection

```bash
python scripts/linear_sync.py create-epic epic-test "Test Epic" "Testing Linear integration"
```

**Expected Output**:
```
Created project: https://linear.app/backtothefutures83/project/epic-test-xxxxx
```

---

## Dashboard View

### Linear Workspace View (backtothefutures83@gmail.com)

**Projects Tab**:
```
📊 V12 Universal OR Strategy - Phase 7 Complexity Extraction
├── EPIC-8: OnKeyDown + ProcessIpc Extraction (3 issues, 100%)
├── EPIC-9: AttachPanelHandlers + OnSyncAllClick Extraction (2 issues, 50%)
├── EPIC-10: UpdateContextualUI + ManageTrail Extraction (2 issues, 0%)
└── ...
```

**Issues Tab** (Filtered by Epic):
```
UNI-123 ✅ EPIC-8-ticket-01: Extract OnKeyDown command handlers
UNI-124 ✅ EPIC-8-ticket-02: Extract ProcessIpc command handlers
UNI-125 ✅ EPIC-8-ticket-03: Integration testing
UNI-126 🔄 EPIC-9-ticket-01: Extract AttachPanelHandlers
UNI-127 ⏸️ EPIC-9-ticket-02: Extract OnSyncAllClick (F5 Gate)
```

**Roadmap View**:
```
Week 1-2: EPIC-8, EPIC-9, EPIC-10 (Complexity Extraction)
Week 3-4: EPIC-11, EPIC-12, EPIC-13 (Complexity Extraction)
Week 5-6: EPIC-14, EPIC-15, EPIC-16 (Tests + Cleanup)
Week 7-8: EPIC-17, EPIC-18, EPIC-19 (Tests + Codacy)
Week 9-10: EPIC-20, EPIC-21 (Quality Convergence)
```

---

## Benefits

### 1. Centralized Tracking
- ✅ All EPICs and tickets visible in one place
- ✅ Real-time progress updates
- ✅ Historical record of all work

### 2. Stakeholder Visibility
- ✅ Non-technical stakeholders can track progress
- ✅ No need to understand Git/GitHub
- ✅ Clean, visual interface

### 3. Metrics & Reporting
- ✅ Velocity tracking (story points per week)
- ✅ Cycle time (ticket start → complete)
- ✅ Burndown charts
- ✅ Epic completion forecasting

### 4. Notifications
- ✅ Director gets notified at F5 gates (backtothefutures83@gmail.com)
- ✅ Collaborator can view progress (mkalhitti)
- ✅ Slack/Discord integration available

---

## Migration from Existing Issues

### Current Linear Issues (15 in Build 1111.007-phase7-11)

**Action**: Map existing issues to new EPIC structure

```bash
# Export existing issues
python scripts/linear_sync.py export-issues > existing_issues.json

# Map to EPICs
python scripts/linear_sync.py map-issues existing_issues.json

# Update project structure
python scripts/linear_sync.py migrate-to-epics
```

---

## Future Enhancements

### 1. Bi-Directional Sync
- Linear status changes → Update local `docs/brain/` files
- Enable manual status updates in Linear

### 2. Time Tracking
- Automatic time tracking per ticket
- Compare estimated vs actual time
- Improve future estimates

### 3. Dependencies
- Link dependent tickets in Linear
- Visualize dependency graph
- Auto-block tickets waiting on dependencies

### 4. GitHub Integration
- Link Linear issues to GitHub PRs
- Auto-update Linear when PR merges
- Show PR status in Linear

---

## Summary

**Linear Integration** provides centralized project tracking for all V12 work:
- ✅ Automatic epic/ticket creation
- ✅ Real-time status updates (9 gates per ticket)
- ✅ F5 gate notifications to primary account
- ✅ Completion tracking
- ✅ Metrics & reporting
- ✅ Stakeholder visibility

**Primary Account**: `backtothefutures83@gmail.com` (API key owner)
**Collaborator**: `mkalhitti` (view/comment access)

**Setup Time**: 15 minutes (API key + script installation)
**Maintenance**: Zero (fully automated)

**Effective Date:** 2026-05-31 (V12.22)