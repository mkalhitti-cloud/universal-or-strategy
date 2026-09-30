#!/usr/bin/env python3
"""
CodeScene API Integration Script
Fetches code health metrics, hotspots, and refactoring targets from CodeScene API.
"""

import os
import sys
import json
import requests
from pathlib import Path
from typing import Dict, List, Optional

# Load environment variables from .env file
def load_env():
    env_path = Path(__file__).parent.parent / '.env'
    if env_path.exists():
        with open(env_path) as f:
            for line in f:
                line = line.strip()
                if line and not line.startswith('#') and '=' in line:
                    key, value = line.split('=', 1)
                    os.environ[key.strip()] = value.strip()

load_env()

CODESCENE_API_TOKEN = os.getenv('CODESCENE_API_TOKEN')
CODESCENE_BASE_URL = "https://api.codescene.io/v2"

if not CODESCENE_API_TOKEN:
    print("ERROR: CODESCENE_API_TOKEN not found in .env file")
    sys.exit(1)

class CodeSceneClient:
    def __init__(self, api_token: str):
        self.api_token = api_token
        self.headers = {
            "Authorization": f"Bearer {api_token}",
            "Content-Type": "application/json"
        }
    
    def _request(self, method: str, endpoint: str, **kwargs) -> Dict:
        """Make API request to CodeScene"""
        url = f"{CODESCENE_BASE_URL}/{endpoint}"
        try:
            response = requests.request(method, url, headers=self.headers, **kwargs)
            response.raise_for_status()
            return response.json()
        except requests.exceptions.HTTPError as e:
            print(f"HTTP Error: {e}")
            print(f"Response: {e.response.text}")
            sys.exit(1)
        except Exception as e:
            print(f"Error: {e}")
            sys.exit(1)
    
    def list_projects(self) -> List[Dict]:
        """List all CodeScene projects"""
        response = self._request("GET", "projects")
        return response.get('projects', [])
    
    def get_project_id(self, project_name: str) -> Optional[int]:
        """Find project ID by name"""
        projects = self.list_projects()
        for project in projects:
            if project_name.lower() in project.get('name', '').lower():
                return project['id']
        return None
    
    def get_code_health(self, project_id: int) -> Dict:
        """Get code health metrics for a project"""
        return self._request("GET", f"projects/{project_id}/code-health")
    
    def get_hotspots(self, project_id: int) -> List[Dict]:
        """Get hotspot files for a project"""
        return self._request("GET", f"projects/{project_id}/hotspots")
    
    def get_file_health(self, project_id: int, file_path: str) -> Dict:
        """Get code health for a specific file"""
        return self._request("GET", f"projects/{project_id}/files/{file_path}/health")
    
    def get_refactoring_targets(self, project_id: int) -> List[Dict]:
        """Get recommended refactoring targets"""
        return self._request("GET", f"projects/{project_id}/refactoring-targets")

def main():
    if len(sys.argv) < 2:
        print("Usage: python query_codescene.py <command> [args]")
        print("\nCommands:")
        print("  projects              - List all projects")
        print("  health <project>      - Get code health for project")
        print("  hotspots <project>    - Get hotspot files")
        print("  file <project> <path> - Get health for specific file")
        print("  targets <project>     - Get refactoring targets")
        sys.exit(1)
    
    command = sys.argv[1]
    client = CodeSceneClient(CODESCENE_API_TOKEN)
    
    if command == "projects":
        projects = client.list_projects()
        print(json.dumps(projects, indent=2))
    
    elif command == "health":
        if len(sys.argv) < 3:
            print("Usage: python query_codescene.py health <project_name>")
            sys.exit(1)
        project_name = sys.argv[2]
        project_id = client.get_project_id(project_name)
        if not project_id:
            print(f"Project '{project_name}' not found")
            sys.exit(1)
        health = client.get_code_health(project_id)
        print(json.dumps(health, indent=2))
    
    elif command == "hotspots":
        if len(sys.argv) < 3:
            print("Usage: python query_codescene.py hotspots <project_name>")
            sys.exit(1)
        project_name = sys.argv[2]
        project_id = client.get_project_id(project_name)
        if not project_id:
            print(f"Project '{project_name}' not found")
            sys.exit(1)
        hotspots = client.get_hotspots(project_id)
        print(json.dumps(hotspots, indent=2))
    
    elif command == "file":
        if len(sys.argv) < 4:
            print("Usage: python query_codescene.py file <project_name> <file_path>")
            sys.exit(1)
        project_name = sys.argv[2]
        file_path = sys.argv[3]
        project_id = client.get_project_id(project_name)
        if not project_id:
            print(f"Project '{project_name}' not found")
            sys.exit(1)
        file_health = client.get_file_health(project_id, file_path)
        print(json.dumps(file_health, indent=2))
    
    elif command == "targets":
        if len(sys.argv) < 3:
            print("Usage: python query_codescene.py targets <project_name>")
            sys.exit(1)
        project_name = sys.argv[2]
        project_id = client.get_project_id(project_name)
        if not project_id:
            print(f"Project '{project_name}' not found")
            sys.exit(1)
        targets = client.get_refactoring_targets(project_id)
        print(json.dumps(targets, indent=2))
    
    else:
        print(f"Unknown command: {command}")
        sys.exit(1)

if __name__ == "__main__":
    main()

# Made with Bob
