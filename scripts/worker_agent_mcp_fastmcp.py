#!/usr/bin/env python3
"""
Worker Agent MCP Server (FastMCP) - V12 Universal OR Strategy
Exposes epic execution capabilities as MCP tools for parallel execution

Migrated from old mcp library to FastMCP for Bob IDE compatibility
"""

import json
import subprocess
import sys
from pathlib import Path
from datetime import datetime
from typing import Dict, Any, List, Optional
from fastmcp import FastMCP

# Worker configuration
WORKER_ID = sys.argv[1] if len(sys.argv) > 1 else "worker-1"
WORKTREE_PATH = Path(sys.argv[2]) if len(sys.argv) > 2 else Path(f"C:/WSGTA/universal-or-epic-cluster-{WORKER_ID[-1]}")
REPO_PATH = Path("C:/WSGTA/universal-or-strategy")
ROADMAP_PATH = REPO_PATH / "epic_roadmap.json"

# Initialize FastMCP server
mcp = FastMCP(f"V12 Worker Agent - {WORKER_ID}")


def load_roadmap() -> List[Dict]:
    """Load epic roadmap"""
    with open(ROADMAP_PATH, 'r', encoding='utf-8') as f:
        return json.load(f)


def save_roadmap(roadmap: List[Dict]) -> None:
    """Save updated roadmap"""
    with open(ROADMAP_PATH, 'w', encoding='utf-8') as f:
        json.dump(roadmap, f, indent=2, ensure_ascii=False)


def run_command(cmd: List[str], cwd: Optional[Path] = None) -> Dict[str, Any]:
    """Execute shell command and return result"""
    try:
        result = subprocess.run(
            cmd,
            cwd=cwd or WORKTREE_PATH,
            capture_output=True,
            text=True,
            check=True
        )
        return {
            'success': True,
            'stdout': result.stdout,
            'stderr': result.stderr
        }
    except subprocess.CalledProcessError as e:
        return {
            'success': False,
            'error': str(e),
            'stdout': e.stdout,
            'stderr': e.stderr
        }


@mcp.tool()
def claim_epic(epic_id: str) -> dict:
    """
    Atomically claim an epic for this worker using git-based locking.
    
    Args:
        epic_id: Epic ID (e.g., EPIC-CCN-21)
    
    Returns:
        Success status with epic details or error
    """
    try:
        # 1. Pull latest roadmap
        pull_result = run_command(
            ["git", "pull", "origin", "gitbutler/workspace", "--rebase"],
            cwd=REPO_PATH
        )
        
        if not pull_result['success']:
            return {
                "success": False,
                "error": "Git pull failed",
                "details": pull_result['stderr']
            }
        
        # 2. Load roadmap and check if epic exists
        roadmap = load_roadmap()
        epic = next((e for e in roadmap if e['epic_number'] == epic_id), None)
        
        if not epic:
            return {
                "success": False,
                "error": f"Epic {epic_id} not found in roadmap"
            }
        
        # 3. Check if already assigned
        if epic.get('assigned_to'):
            return {
                "success": False,
                "error": f"Epic {epic_id} already assigned to {epic['assigned_to']}",
                "lock_timestamp": epic.get('lock_timestamp')
            }
        
        # 4. Claim epic
        epic['assigned_to'] = WORKER_ID
        epic['lock_timestamp'] = datetime.utcnow().isoformat() + 'Z'
        save_roadmap(roadmap)
        
        # 5. Commit and push atomically
        run_command(["git", "add", "epic_roadmap.json"], cwd=REPO_PATH)
        run_command(
            ["git", "commit", "-m", f"lock: Claim {epic_id} for {WORKER_ID}"],
            cwd=REPO_PATH
        )
        push_result = run_command(
            ["git", "push", "origin", "gitbutler/workspace"],
            cwd=REPO_PATH
        )
        
        if not push_result['success']:
            return {
                "success": False,
                "error": "Failed to push claim to git",
                "details": push_result['stderr']
            }
        
        return {
            "success": True,
            "epic_id": epic_id,
            "worker_id": WORKER_ID,
            "method": epic['method'],
            "file": epic['file'],
            "cyclomatic": epic['cyclomatic'],
            "lock_timestamp": epic['lock_timestamp']
        }
    
    except Exception as e:
        return {
            "success": False,
            "error": str(e)
        }


@mcp.tool()
def execute_epic(epic_id: str) -> dict:
    """
    Execute all phases of a claimed epic (intake, scope, plan, scan, tickets, validate).
    
    Args:
        epic_id: Epic ID to execute
    
    Returns:
        Success status with phase results or error
    """
    try:
        phases = [
            'epic-intake',
            'epic-scope-boundary',
            'epic-plan',
            'epic-scan',
            'epic-tickets',
            'epic-validate'
        ]
        
        results = []
        
        for phase in phases:
            # Execute phase command
            cmd = ["powershell", "-Command", f"{phase} {epic_id}"]
            result = run_command(cmd, cwd=WORKTREE_PATH)
            
            phase_result = {
                "phase": phase,
                "success": result['success'],
                "timestamp": datetime.utcnow().isoformat() + 'Z'
            }
            
            if not result['success']:
                phase_result['error'] = result['stderr']
                results.append(phase_result)
                
                # Phase failed, return early
                return {
                    "success": False,
                    "epic_id": epic_id,
                    "failed_phase": phase,
                    "results": results
                }
            
            results.append(phase_result)
        
        return {
            "success": True,
            "epic_id": epic_id,
            "worker_id": WORKER_ID,
            "phases_completed": len(results),
            "results": results
        }
    
    except Exception as e:
        return {
            "success": False,
            "error": str(e)
        }


@mcp.tool()
def release_epic(epic_id: str) -> dict:
    """
    Release epic lock after completion or failure.
    
    Args:
        epic_id: Epic ID to release
    
    Returns:
        Success status or error
    """
    try:
        roadmap = load_roadmap()
        epic = next((e for e in roadmap if e['epic_number'] == epic_id), None)
        
        if not epic:
            return {
                "success": False,
                "error": f"Epic {epic_id} not found"
            }
        
        # Remove lock fields
        epic.pop('assigned_to', None)
        epic.pop('lock_timestamp', None)
        save_roadmap(roadmap)
        
        # Commit and push
        run_command(["git", "add", "epic_roadmap.json"], cwd=REPO_PATH)
        run_command(
            ["git", "commit", "-m", f"lock: Release {epic_id} from {WORKER_ID}"],
            cwd=REPO_PATH
        )
        run_command(["git", "push", "origin", "gitbutler/workspace"], cwd=REPO_PATH)
        
        return {
            "success": True,
            "epic_id": epic_id,
            "worker_id": WORKER_ID
        }
    
    except Exception as e:
        return {
            "success": False,
            "error": str(e)
        }


@mcp.tool()
def get_worker_status() -> dict:
    """
    Get current worker status (assigned epic, progress, health).
    
    Returns:
        Worker status information
    """
    try:
        roadmap = load_roadmap()
        assigned_epic = next(
            (e for e in roadmap if e.get('assigned_to') == WORKER_ID),
            None
        )
        
        return {
            "worker_id": WORKER_ID,
            "worktree_path": str(WORKTREE_PATH),
            "assigned_epic": assigned_epic['epic_number'] if assigned_epic else None,
            "health": "healthy"
        }
    
    except Exception as e:
        return {
            "worker_id": WORKER_ID,
            "health": "error",
            "error": str(e)
        }


@mcp.tool()
def get_next_pending_epic() -> dict:
    """
    Get next pending epic from roadmap (not complete, not assigned).
    
    Returns:
        Next epic details or error if none available
    """
    try:
        roadmap = load_roadmap()
        next_epic = next(
            (e for e in roadmap 
             if e.get('status') != 'complete' and not e.get('assigned_to')),
            None
        )
        
        if not next_epic:
            return {
                "success": False,
                "message": "No pending epics found"
            }
        
        return {
            "success": True,
            "epic_id": next_epic['epic_number'],
            "method": next_epic['method'],
            "file": next_epic['file'],
            "cyclomatic": next_epic['cyclomatic']
        }
    
    except Exception as e:
        return {
            "success": False,
            "error": str(e)
        }


if __name__ == "__main__":
    mcp.run()

# Made with Bob
