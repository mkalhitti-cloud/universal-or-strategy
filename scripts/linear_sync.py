#!/usr/bin/env python3
"""
V12 Master Roadmap → Linear Integration Script
Syncs V12 development roadmap to Linear issue tracking system.

Usage:
    python scripts/linear_sync.py --api-key YOUR_LINEAR_API_KEY --team-id YOUR_TEAM_ID

Build Number Consolidation:
    - 1111.xxx series: Current active build tags (Phase 6-7, M-Phase)
    - 938/984 series: Legacy build numbers from earlier phases
    - Recommendation: Standardize on 1111.xxx going forward
"""

import argparse
import json
import os
import re
from typing import Dict, List, Optional
from dataclasses import dataclass
from datetime import datetime, timezone

try:
    import requests
except ImportError:
    print("ERROR: requests library not installed. Run: pip install requests")
    exit(1)

try:
    from dotenv import load_dotenv
    load_dotenv()  # Load .env file if it exists
except ImportError:
    pass  # dotenv is optional


@dataclass
class LinearIssue:
    """Represents a Linear issue to be created/updated."""
    title: str
    description: str
    priority: int  # 0=No priority, 1=Urgent, 2=High, 3=Medium, 4=Low
    status: str  # "backlog", "todo", "in_progress", "done", "canceled"
    labels: List[str]
    assignee_ids: List[str]
    parent_id: Optional[str] = None  # For sub-issues
    estimate: Optional[int] = None  # Story points


class LinearSync:
    """Syncs V12 roadmap to Linear."""
    
    def __init__(self, api_key: str, team_id: str):
        self.api_key = api_key
        self.team_id = team_id
        self.base_url = "https://api.linear.app/graphql"
        self.headers = {
            "Authorization": api_key,
            "Content-Type": "application/json"
        }
        
    def find_project_by_name(self, name: str) -> Optional[str]:
        """Find a project by name and return its ID."""
        query = """
        query FindProject($teamId: String!) {
            team(id: $teamId) {
                projects {
                    nodes {
                        id
                        name
                    }
                }
            }
        }
        """
        
        variables = {"teamId": self.team_id}
        
        response = requests.post(
            self.base_url,
            headers=self.headers,
            json={"query": query, "variables": variables}
        )
        
        if response.status_code == 200:
            try:
                data = response.json()
                if not data:
                    print(f"[WARN] Empty response when finding project: {name}")
                    return None
                projects = data.get("data", {}).get("team", {}).get("projects", {}).get("nodes", [])
                for project in projects:
                    if project["name"] == name:
                        return project["id"]
            except ValueError as e:
                print(f"[ERROR] Invalid JSON when finding project: {response.text}")
                return None
        else:
            print(f"[ERROR] HTTP {response.status_code} when finding project: {response.text}")
        
        return None
    
    def update_project(self, project_id: str, description: str) -> bool:
        """Update an existing project's description."""
        mutation = """
        mutation UpdateProject($projectId: String!, $description: String!) {
            projectUpdate(id: $projectId, input: {
                description: $description
            }) {
                success
                project {
                    id
                    name
                }
            }
        }
        """
        
        variables = {
            "projectId": project_id,
            "description": description
        }
        
        response = requests.post(
            self.base_url,
            headers=self.headers,
            json={"query": mutation, "variables": variables}
        )
        
        if response.status_code == 200:
            try:
                data = response.json()
                if not data:
                    print(f"[WARN] Empty response when updating project")
                    return False
                if data.get("data", {}).get("projectUpdate", {}).get("success"):
                    return True
                elif "errors" in data:
                    print(f"[ERROR] GraphQL errors updating project: {data['errors']}")
            except ValueError as e:
                print(f"[ERROR] Invalid JSON when updating project: {response.text}")
        else:
            print(f"[ERROR] HTTP {response.status_code} when updating project: {response.text}")
        
        return False
    
    def create_epic(self, title: str, description: str) -> str:
        """Create or update a Linear epic (project)."""
        # Check if project already exists
        existing_id = self.find_project_by_name(title)
        if existing_id:
            print(f"[INFO] Project already exists: {existing_id}")
            # Update the existing project
            if self.update_project(existing_id, description):
                print(f"[OK] Updated existing project: {existing_id}")
                return existing_id
            else:
                print(f"[WARN] Failed to update project, will use existing: {existing_id}")
                return existing_id
        
        # Create new project
        mutation = """
        mutation CreateProject($teamId: String!, $name: String!, $description: String!) {
            projectCreate(input: {
                teamIds: [$teamId]
                name: $name
                description: $description
            }) {
                success
                project {
                    id
                    name
                }
            }
        }
        """
        
        variables = {
            "teamId": self.team_id,
            "name": title,
            "description": description
        }
        
        response = requests.post(
            self.base_url,
            headers=self.headers,
            json={"query": mutation, "variables": variables}
        )
        
        if response.status_code == 200:
            try:
                data = response.json()
                if not data:
                    print(f"[ERROR] Empty response when creating project")
                    raise Exception("Empty JSON response from Linear API")
                if data.get("data", {}).get("projectCreate", {}).get("success"):
                    return data["data"]["projectCreate"]["project"]["id"]
                elif "errors" in data:
                    # Check if error is about duplicate project
                    error_msg = str(data["errors"])
                    if "already exists" in error_msg.lower() or "duplicate" in error_msg.lower():
                        print(f"[WARN] Project creation failed (likely duplicate): {error_msg}")
                        # Try to find and return existing project
                        existing_id = self.find_project_by_name(title)
                        if existing_id:
                            print(f"[OK] Using existing project: {existing_id}")
                            return existing_id
                    raise Exception(f"GraphQL errors: {data['errors']}")
                else:
                    print(f"[ERROR] Unexpected response structure: {data}")
                    raise Exception(f"Unexpected response: {data}")
            except ValueError as e:
                print(f"[ERROR] Invalid JSON response: {response.text}")
                raise Exception(f"Invalid JSON response: {response.text}")
        else:
            print(f"[ERROR] HTTP {response.status_code} when creating project: {response.text}")
        
        raise Exception(f"Failed to create epic (HTTP {response.status_code}): {response.text}")
    
    def create_issue(self, issue: LinearIssue, project_id: Optional[str] = None) -> str:
        """Create a Linear issue."""
        mutation = """
        mutation CreateIssue(
            $teamId: String!
            $title: String!
            $description: String
            $priority: Int
            $stateId: String
            $labelIds: [String!]
            $assigneeId: String
            $projectId: String
            $parentId: String
            $estimate: Int
        ) {
            issueCreate(input: {
                teamId: $teamId
                title: $title
                description: $description
                priority: $priority
                stateId: $stateId
                labelIds: $labelIds
                assigneeId: $assigneeId
                projectId: $projectId
                parentId: $parentId
                estimate: $estimate
            }) {
                success
                issue {
                    id
                    identifier
                    title
                }
            }
        }
        """
        
        # Map status to Linear state ID (you'll need to fetch these from your workspace)
        state_map = {
            "backlog": None,  # Will use team default
            "todo": None,
            "in_progress": None,
            "done": None,
            "canceled": None
        }
        
        variables = {
            "teamId": self.team_id,
            "title": issue.title,
            "description": issue.description,
            "priority": issue.priority,
            "stateId": state_map.get(issue.status),
            "labelIds": [],  # You'll need to create/fetch label IDs
            "assigneeId": issue.assignee_ids[0] if issue.assignee_ids else None,  # Linear only supports single assignee
            "projectId": project_id,
            "parentId": issue.parent_id,
            "estimate": issue.estimate
        }
        
        response = requests.post(
            self.base_url,
            headers=self.headers,
            json={"query": mutation, "variables": variables}
        )
        
        if response.status_code == 200:
            data = response.json()
            if data.get("data", {}).get("issueCreate", {}).get("success"):
                return data["data"]["issueCreate"]["issue"]["id"]
        
        raise Exception(f"Failed to create issue: {response.text}")
    
    def parse_roadmap(self, roadmap_path: str) -> Dict:
        """Parse master_roadmap.md into structured data."""
        with open(roadmap_path, 'r', encoding='utf-8') as f:
            content = f.read()
        
        # Extract phases
        phases = []
        phase_pattern = r'\| \*\*Phase (\d+)\*\* \| (.+?) \| (.+?) \|'
        for match in re.finditer(phase_pattern, content):
            phases.append({
                "number": int(match.group(1)),
                "title": match.group(2).strip(),
                "status": match.group(3).strip()
            })
        
        # Extract current tasks
        tasks = []
        task_pattern = r'\| \*\*(\d+)\*\* \| (.+?) \| (.+?) \|'
        in_task_section = False
        for line in content.split('\n'):
            if "Current Task List" in line:
                in_task_section = True
                continue
            if in_task_section and '|' in line:
                match = re.match(task_pattern, line)
                if match:
                    tasks.append({
                        "number": int(match.group(1)),
                        "title": match.group(2).strip(),
                        "status": match.group(3).strip()
                    })
        
        # Extract build tag
        build_tag_match = re.search(r'\*\*Current Build\*\*: (.+)', content)
        build_tag = build_tag_match.group(1) if build_tag_match else "Unknown"
        
        return {
            "phases": phases,
            "tasks": tasks,
            "build_tag": build_tag,
            "parsed_at": datetime.now(timezone.utc).isoformat()
        }
    
    def sync_to_linear(self, roadmap_data: Dict, assignee_ids: List[str]):
        """Sync parsed roadmap to Linear."""
        print(f"Syncing V12 Roadmap (Build: {roadmap_data['build_tag']}) to Linear...")
        
        # Create main epic
        epic_id = self.create_epic(
            title=f"V12 Universal OR Strategy - Build {roadmap_data['build_tag']}",
            description=f"""
# V12 Development Roadmap

**Current Build**: {roadmap_data['build_tag']}
**Last Synced**: {roadmap_data['parsed_at']}

## Phases
{len(roadmap_data['phases'])} phases tracked

## Active Tasks
{len(roadmap_data['tasks'])} tasks in current sprint

---
*Auto-synced from master_roadmap.md*
            """
        )
        
        print(f"[OK] Created epic: {epic_id}")
        
        # Create phase issues
        phase_ids = {}
        for phase in roadmap_data['phases']:
            status_map = {
                "DONE": "done",
                "COMPLETE": "done",
                "IN PROGRESS": "in_progress",
                "QUEUED": "todo"
            }
            
            status = "done"
            for key, value in status_map.items():
                if key in phase['status'].upper():
                    status = value
                    break
            
            issue = LinearIssue(
                title=f"Phase {phase['number']}: {phase['title']}",
                description=f"**Status**: {phase['status']}\n\n{phase['title']}",
                priority=2,  # High priority for phases
                status=status,
                labels=["phase", f"phase-{phase['number']}"],
                assignee_ids=assignee_ids,
                estimate=13  # Large epic estimate
            )
            
            phase_id = self.create_issue(issue, project_id=epic_id)
            phase_ids[phase['number']] = phase_id
            print(f"[OK] Created Phase {phase['number']}: {phase_id}")
        
        # Create task issues
        for task in roadmap_data['tasks']:
            status_map = {
                "COMPLETE": "done",
                "NEXT": "in_progress",
                "QUEUED": "todo"
            }
            
            status = "todo"
            for key, value in status_map.items():
                if key in task['status'].upper():
                    status = value
                    break
            
            issue = LinearIssue(
                title=f"T-{task['number']}: {task['title']}",
                description=f"**Status**: {task['status']}\n\n{task['title']}",
                priority=1 if status == "in_progress" else 2,
                status=status,
                labels=["task", "active-sprint"],
                assignee_ids=assignee_ids,
                estimate=5  # Medium task estimate
            )
            
            task_id = self.create_issue(issue, project_id=epic_id)
            print(f"[OK] Created Task {task['number']}: {task_id}")
        
        print(f"\n[SUCCESS] Sync complete! Created {len(phase_ids)} phases and {len(roadmap_data['tasks'])} tasks")


def main():
    parser = argparse.ArgumentParser(description="Sync V12 roadmap to Linear")
    parser.add_argument("--api-key", help="Linear API key (or set LINEAR_API_KEY env var)")
    parser.add_argument("--team-id", help="Linear team ID (or set LINEAR_TEAM_ID env var)")
    parser.add_argument("--assignee-ids", nargs="+", help="Linear user IDs to assign (or set LINEAR_ASSIGNEE_IDS env var)")
    parser.add_argument("--roadmap", default="docs/brain/master_roadmap.md", help="Path to roadmap file")
    
    args = parser.parse_args()
    
    # Get values from args or environment variables
    api_key = args.api_key or os.getenv("LINEAR_API_KEY")
    team_id = args.team_id or os.getenv("LINEAR_TEAM_ID")
    assignee_ids = args.assignee_ids or (os.getenv("LINEAR_ASSIGNEE_IDS", "").split(",") if os.getenv("LINEAR_ASSIGNEE_IDS") else [])
    
    # Validate required parameters
    if not api_key:
        print("ERROR: Linear API key required. Set LINEAR_API_KEY env var or use --api-key")
        exit(1)
    if not team_id:
        print("ERROR: Linear team ID required. Set LINEAR_TEAM_ID env var or use --team-id")
        exit(1)
    
    # Validate roadmap exists
    if not os.path.exists(args.roadmap):
        print(f"ERROR: Roadmap file not found: {args.roadmap}")
        exit(1)
    
    # Initialize sync
    sync = LinearSync(api_key, team_id)
    
    # Parse roadmap
    print(f"[PARSE] Parsing roadmap: {args.roadmap}")
    roadmap_data = sync.parse_roadmap(args.roadmap)
    
    print(f"Found {len(roadmap_data['phases'])} phases and {len(roadmap_data['tasks'])} tasks")
    print(f"Current build: {roadmap_data['build_tag']}")
    
    # Sync to Linear
    sync.sync_to_linear(roadmap_data, assignee_ids)


if __name__ == "__main__":
    main()

# Made with Bob
