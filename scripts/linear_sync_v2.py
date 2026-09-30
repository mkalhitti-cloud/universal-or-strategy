#!/usr/bin/env python3
"""
V12 Master Roadmap → Linear Integration Script (V2 - Update Mode)
Syncs V12 development roadmap to Linear issue tracking system.
This version updates existing projects instead of creating duplicates.

Usage:
    python scripts/linear_sync_v2.py
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
        
        try:
            response = requests.post(
                self.base_url,
                headers=self.headers,
                json={"query": query, "variables": variables},
                timeout=30
            )
            
            if response.status_code == 200:
                data = response.json()
                if not data:
                    print(f"[WARN] Empty response when finding project: {name}")
                    return None
                projects = data.get("data", {}).get("team", {}).get("projects", {}).get("nodes", [])
                for project in projects:
                    if project["name"] == name:
                        print(f"[INFO] Found existing project: {project['id']}")
                        return project["id"]
            else:
                print(f"[ERROR] HTTP {response.status_code} when finding project")
        except Exception as e:
            print(f"[ERROR] Exception finding project: {e}")
        
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
        
        try:
            response = requests.post(
                self.base_url,
                headers=self.headers,
                json={"query": mutation, "variables": variables},
                timeout=30
            )
            
            if response.status_code == 200:
                data = response.json()
                if not data:
                    print(f"[WARN] Empty response when updating project")
                    return False
                if data.get("data", {}).get("projectUpdate", {}).get("success"):
                    return True
                elif "errors" in data:
                    print(f"[ERROR] GraphQL errors updating project: {data['errors']}")
            else:
                print(f"[ERROR] HTTP {response.status_code} when updating project")
        except Exception as e:
            print(f"[ERROR] Exception updating project: {e}")
        
        return False
    
    def get_or_create_project(self, title: str, description: str) -> str:
        """Get existing project or create new one."""
        # Try to find existing project first
        existing_id = self.find_project_by_name(title)
        if existing_id:
            print(f"[OK] Using existing project: {existing_id}")
            # Update the description
            if self.update_project(existing_id, description):
                print(f"[OK] Updated project description")
            return existing_id
        
        # Project doesn't exist, create it
        print(f"[INFO] Project not found, creating new one...")
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
        
        try:
            response = requests.post(
                self.base_url,
                headers=self.headers,
                json={"query": mutation, "variables": variables},
                timeout=30
            )
            
            if response.status_code == 200:
                data = response.json()
                if not data:
                    raise Exception("Empty response from Linear API")
                if data.get("data", {}).get("projectCreate", {}).get("success"):
                    project_id = data["data"]["projectCreate"]["project"]["id"]
                    print(f"[OK] Created new project: {project_id}")
                    return project_id
                elif "errors" in data:
                    raise Exception(f"GraphQL errors: {data['errors']}")
                else:
                    raise Exception(f"Unexpected response: {data}")
            else:
                raise Exception(f"HTTP {response.status_code}: {response.text}")
        except Exception as e:
            raise Exception(f"Failed to create project: {e}")
    
    def parse_roadmap(self, roadmap_path: str) -> Dict:
        """Parse master_roadmap.md into structured data."""
        with open(roadmap_path, 'r', encoding='utf-8') as f:
            content = f.read()
        
        # Extract build tag
        build_tag_match = re.search(r'\*\*Current Build\*\*: (.+)', content)
        build_tag = build_tag_match.group(1) if build_tag_match else "Unknown"
        
        return {
            "build_tag": build_tag,
            "parsed_at": datetime.now(timezone.utc).isoformat()
        }
    
    def sync_to_linear(self, roadmap_data: Dict):
        """Sync parsed roadmap to Linear."""
        print(f"Syncing V12 Roadmap (Build: {roadmap_data['build_tag']}) to Linear...")
        
        # Get or create main project
        project_id = self.get_or_create_project(
            title=f"V12 Universal OR Strategy - Build {roadmap_data['build_tag']}",
            description=f"""
# V12 Development Roadmap

**Current Build**: {roadmap_data['build_tag']}
**Last Synced**: {roadmap_data['parsed_at']}

---
*Auto-synced from master_roadmap.md*
            """
        )
        
        print(f"\n[SUCCESS] Sync complete! Project ID: {project_id}")


def main():
    # Get values from environment variables
    api_key = os.getenv("LINEAR_API_KEY")
    team_id = os.getenv("LINEAR_TEAM_ID")
    
    # Validate required parameters
    if not api_key:
        print("ERROR: LINEAR_API_KEY env var not set")
        exit(1)
    if not team_id:
        print("ERROR: LINEAR_TEAM_ID env var not set")
        exit(1)
    
    roadmap_path = "docs/brain/master_roadmap.md"
    if not os.path.exists(roadmap_path):
        print(f"ERROR: Roadmap file not found: {roadmap_path}")
        exit(1)
    
    # Initialize sync
    sync = LinearSync(api_key, team_id)
    
    # Parse roadmap
    print(f"[PARSE] Parsing roadmap: {roadmap_path}")
    roadmap_data = sync.parse_roadmap(roadmap_path)
    print(f"Current build: {roadmap_data['build_tag']}")
    
    # Sync to Linear
    sync.sync_to_linear(roadmap_data)


if __name__ == "__main__":
    main()

# Made with Bob
