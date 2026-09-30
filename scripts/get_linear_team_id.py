#!/usr/bin/env python3
"""
Get Linear Team ID from API
Uses LINEAR_API_KEY from .env file
"""

import os
import requests
from dotenv import load_dotenv

load_dotenv()

api_key = os.getenv("LINEAR_API_KEY")
if not api_key:
    print("ERROR: LINEAR_API_KEY not found in .env file")
    exit(1)

# GraphQL query to get teams
query = """
query {
    teams {
        nodes {
            id
            name
            key
        }
    }
    viewer {
        id
        name
        email
    }
}
"""

headers = {
    "Authorization": api_key,
    "Content-Type": "application/json"
}

response = requests.post(
    "https://api.linear.app/graphql",
    headers=headers,
    json={"query": query}
)

if response.status_code == 200:
    data = response.json()
    
    print("=" * 60)
    print("LINEAR ACCOUNT INFO")
    print("=" * 60)
    
    viewer = data.get("data", {}).get("viewer", {})
    print(f"\nLogged in as: {viewer.get('name')} ({viewer.get('email')})")
    print(f"User ID: {viewer.get('id')}")
    
    teams = data.get("data", {}).get("teams", {}).get("nodes", [])
    
    if teams:
        print(f"\n{len(teams)} team(s) found:")
        print("-" * 60)
        for i, team in enumerate(teams, 1):
            print(f"\n{i}. {team['name']}")
            print(f"   Team ID: {team['id']}")
            print(f"   Team Key: {team['key']}")
        
        print("\n" + "=" * 60)
        print("NEXT STEPS")
        print("=" * 60)
        print("\n1. Copy the Team ID you want to use")
        print("2. Add to .env file:")
        print(f"   LINEAR_TEAM_ID=<paste_team_id_here>")
        print(f"   LINEAR_ASSIGNEE_IDS={viewer.get('id')}")
        print("\n3. Test the sync:")
        print("   python scripts/linear_sync.py")
    else:
        print("\nNo teams found. You may need to create a team in Linear first.")
        print("Go to: https://linear.app/settings/teams")
else:
    print(f"ERROR: HTTP {response.status_code}")
    print(response.text)

# Made with Bob
