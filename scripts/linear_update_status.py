#!/usr/bin/env python3
"""
Linear Status Update Script
Creates status updates and new issues in Linear.
"""

import os
import sys
import json
import requests
from datetime import datetime

# Linear API configuration
LINEAR_API_URL = "https://api.linear.app/graphql"

def get_api_key():
    """Get LINEAR_API_KEY from environment."""
    api_key = os.getenv("LINEAR_API_KEY")
    if not api_key:
        print("ERROR: LINEAR_API_KEY not set in environment")
        print("Run: . .\\scripts\\setup_linear_env.ps1")
        sys.exit(1)
    return api_key

def get_team_id(api_key):
    """Get the team ID from Linear."""
    query = """
    query {
        teams {
            nodes {
                id
                name
                key
            }
        }
    }
    """
    
    headers = {
        "Authorization": api_key,
        "Content-Type": "application/json"
    }
    
    try:
        response = requests.post(
            LINEAR_API_URL,
            headers=headers,
            json={"query": query},
            timeout=30
        )
        
        if response.status_code == 200:
            data = response.json()
            teams = data.get("data", {}).get("teams", {}).get("nodes", [])
            if teams:
                team = teams[0]  # Use first team
                print(f"[OK] Using team: {team['name']} ({team['key']})")
                return team['id']
            else:
                print("ERROR: No teams found")
                sys.exit(1)
        else:
            print(f"ERROR: HTTP {response.status_code}")
            print(response.text)
            sys.exit(1)
    except Exception as e:
        print(f"ERROR: {e}")
        sys.exit(1)

def create_issue(api_key, team_id, title, description, priority=2, labels=None):
    """Create a new Linear issue."""
    mutation = """
    mutation CreateIssue($teamId: String!, $title: String!, $description: String!, $priority: Int!) {
        issueCreate(input: {
            teamId: $teamId
            title: $title
            description: $description
            priority: $priority
        }) {
            success
            issue {
                id
                identifier
                title
                url
            }
        }
    }
    """
    
    variables = {
        "teamId": team_id,
        "title": title,
        "description": description,
        "priority": priority
    }
    
    headers = {
        "Authorization": api_key,
        "Content-Type": "application/json"
    }
    
    try:
        response = requests.post(
            LINEAR_API_URL,
            headers=headers,
            json={"query": mutation, "variables": variables},
            timeout=30
        )
        
        if response.status_code == 200:
            data = response.json()
            if data.get("data", {}).get("issueCreate", {}).get("success"):
                issue = data["data"]["issueCreate"]["issue"]
                print(f"\n[SUCCESS] Created issue: {issue['identifier']}")
                print(f"  Title: {issue['title']}")
                print(f"  URL: {issue['url']}")
                return issue
            elif "errors" in data:
                print(f"ERROR: {data['errors']}")
                return None
        else:
            print(f"ERROR: HTTP {response.status_code}")
            print(response.text)
            return None
    except Exception as e:
        print(f"ERROR: {e}")
        return None

def list_issues(api_key, team_id, states=None):
    """List issues in Linear."""
    query = """
    query ListIssues($teamId: String!) {
        team(id: $teamId) {
            issues(first: 50) {
                nodes {
                    id
                    identifier
                    title
                    state {
                        name
                    }
                    priority
                    url
                }
            }
        }
    }
    """
    
    variables = {"teamId": team_id}
    
    headers = {
        "Authorization": api_key,
        "Content-Type": "application/json"
    }
    
    try:
        response = requests.post(
            LINEAR_API_URL,
            headers=headers,
            json={"query": query, "variables": variables},
            timeout=30
        )
        
        if response.status_code == 200:
            data = response.json()
            issues = data.get("data", {}).get("team", {}).get("issues", {}).get("nodes", [])
            return issues
        else:
            print(f"ERROR: HTTP {response.status_code}")
            return []
    except Exception as e:
        print(f"ERROR: {e}")
        return []

def main():
    print("=" * 60)
    print("Linear Status Update")
    print("=" * 60)
    
    # Get API credentials
    api_key = get_api_key()
    team_id = get_team_id(api_key)
    
    # Create status update issue
    print("\n[1/3] Creating status update...")
    status_desc = """**Completed Work:**

- ✅ PR #14: .NET Framework 4.8 compatibility (MERGED)
- ✅ PR #15: Infrastructure documentation (MERGED)  
- ✅ Branch cleanup: 14 stale branches deleted
- ✅ Status: Ready for EPIC-POSINFO refactoring

**Next Steps:**
- Begin EPIC-POSINFO: Refactor PositionInfo.cs to CodeScene 10/10
- Target: CYC ≤10, eliminate switch-based duplication
- Maintain zero-allocation hot paths

**Timestamp:** {timestamp}
""".format(timestamp=datetime.utcnow().isoformat())
    
    status_issue = create_issue(
        api_key, 
        team_id,
        "Status Update: PR #14 & #15 Merged, Branch Cleanup Complete",
        status_desc,
        priority=2
    )
    
    # Create EPIC-POSINFO issue
    print("\n[2/3] Creating EPIC-POSINFO issue...")
    epic_desc = """**Objective:**
Refactor PositionInfo.cs to achieve CodeScene 10/10 health score while maintaining V12 DNA principles.

**Current State:**
- High duplication in switch-based property accessors
- Cyclomatic complexity exceeds threshold
- CodeScene hotspot detected

**Target State:**
- CYC ≤ 10 (Jane Street aligned)
- CodeScene score: 10/10
- Zero-allocation hot paths maintained
- Lock-free Actor pattern compliance

**Approach:**
1. Extract switch logic into lookup tables
2. Consolidate duplicate accessor patterns
3. Maintain performance characteristics
4. Add TDD tests for extracted methods

**Labels:** epic, refactoring, jane-street-aligned
**Priority:** High
"""
    
    epic_issue = create_issue(
        api_key,
        team_id,
        "EPIC-POSINFO: Refactor PositionInfo.cs to CodeScene 10/10",
        epic_desc,
        priority=1  # High priority
    )
    
    # List current open issues
    print("\n[3/3] Listing current open issues...")
    issues = list_issues(api_key, team_id)
    
    print(f"\n[INFO] Total issues: {len(issues)}")
    print("\nRecent issues:")
    for issue in issues[:10]:
        state = issue.get("state", {}).get("name", "Unknown")
        print(f"  {issue['identifier']}: {issue['title']} [{state}]")
    
    # Summary
    print("\n" + "=" * 60)
    print("Summary")
    print("=" * 60)
    if status_issue:
        print(f"[OK] Status update created: {status_issue['identifier']}")
    if epic_issue:
        print(f"[OK] EPIC-POSINFO created: {epic_issue['identifier']}")
    print(f"[INFO] Total open issues: {len(issues)}")
    print("\n[DONE] Linear sync complete!")

if __name__ == "__main__":
    main()

# Made with Bob
