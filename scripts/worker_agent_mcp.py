#!/usr/bin/env python3
"""
Worker Agent MCP Server - V12 Universal OR Strategy
Exposes epic execution capabilities as MCP tools for Watsonx Orchestrate integration

Based on IBM Bob Watsonx Orchestrate tutorial pattern
"""

import asyncio
import json
import subprocess
import sys
from pathlib import Path
from datetime import datetime
from typing import Dict, Any, List, Optional
from mcp.server import Server
from mcp.server.stdio import stdio_server
from mcp.types import Tool, TextContent

# Worker configuration
WORKER_ID = sys.argv[1] if len(sys.argv) > 1 else "worker-1"
WORKTREE_PATH = Path(sys.argv[2]) if len(sys.argv) > 2 else Path(f"C:/WSGTA/universal-or-epic-cluster-{WORKER_ID[-1]}")
REPO_PATH = Path("C:/WSGTA/universal-or-strategy")
ROADMAP_PATH = REPO_PATH / "epic_roadmap.json"

# Initialize MCP server
app = Server("v12-worker-agent")


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


@app.list_tools()
async def list_tools() -> List[Tool]:
    """List available MCP tools for worker agent"""
    return [
        Tool(
            name="claim_epic",
            description="Atomically claim an epic for this worker using git-based locking",
            inputSchema={
                "type": "object",
                "properties": {
                    "epic_id": {
                        "type": "string",
                        "description": "Epic ID (e.g., EPIC-CCN-21)"
                    }
                },
                "required": ["epic_id"]
            }
        ),
        Tool(
            name="execute_epic",
            description="Execute all phases of a claimed epic (intake, scope, plan, validate)",
            inputSchema={
                "type": "object",
                "properties": {
                    "epic_id": {
                        "type": "string",
                        "description": "Epic ID to execute"
                    }
                },
                "required": ["epic_id"]
            }
        ),
        Tool(
            name="release_epic",
            description="Release epic lock after completion or failure",
            inputSchema={
                "type": "object",
                "properties": {
                    "epic_id": {
                        "type": "string",
                        "description": "Epic ID to release"
                    }
                },
                "required": ["epic_id"]
            }
        ),
        Tool(
            name="get_worker_status",
            description="Get current worker status (assigned epic, progress, health)",
            inputSchema={
                "type": "object",
                "properties": {},
                "required": []
            }
        ),
        Tool(
            name="get_next_pending_epic",
            description="Get next pending epic from roadmap (not complete, not assigned)",
            inputSchema={
                "type": "object",
                "properties": {},
                "required": []
            }
        )
    ]


@app.call_tool()
async def call_tool(name: str, arguments: Dict[str, Any]) -> List[TextContent]:
    """Handle MCP tool calls"""
    
    if name == "claim_epic":
        return await claim_epic_tool(arguments["epic_id"])
    
    elif name == "execute_epic":
        return await execute_epic_tool(arguments["epic_id"])
    
    elif name == "release_epic":
        return await release_epic_tool(arguments["epic_id"])
    
    elif name == "get_worker_status":
        return await get_worker_status_tool()
    
    elif name == "get_next_pending_epic":
        return await get_next_pending_epic_tool()
    
    else:
        return [TextContent(
            type="text",
            text=json.dumps({"error": f"Unknown tool: {name}"})
        )]


async def claim_epic_tool(epic_id: str) -> List[TextContent]:
    """Atomically claim epic using git-based locking"""
    try:
        # 1. Pull latest roadmap
        pull_result = run_command(
            ["git", "pull", "origin", "gitbutler/workspace", "--rebase"],
            cwd=REPO_PATH
        )
        
        if not pull_result['success']:
            return [TextContent(
                type="text",
                text=json.dumps({
                    "success": False,
                    "error": "Git pull failed",
                    "details": pull_result['stderr']
                })
            )]
        
        # 2. Load roadmap and check if epic exists
        roadmap = load_roadmap()
        epic = next((e for e in roadmap if e['epic_number'] == epic_id), None)
        
        if not epic:
            return [TextContent(
                type="text",
                text=json.dumps({
                    "success": False,
                    "error": f"Epic {epic_id} not found in roadmap"
                })
            )]
        
        # 3. Check if already assigned
        if epic.get('assigned_to'):
            return [TextContent(
                type="text",
                text=json.dumps({
                    "success": False,
                    "error": f"Epic {epic_id} already assigned to {epic['assigned_to']}",
                    "lock_timestamp": epic.get('lock_timestamp')
                })
            )]
        
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
            return [TextContent(
                type="text",
                text=json.dumps({
                    "success": False,
                    "error": "Failed to push claim to git",
                    "details": push_result['stderr']
                })
            )]
        
        return [TextContent(
            type="text",
            text=json.dumps({
                "success": True,
                "epic_id": epic_id,
                "worker_id": WORKER_ID,
                "method": epic['method'],
                "file": epic['file'],
                "cyclomatic": epic['cyclomatic'],
                "lock_timestamp": epic['lock_timestamp']
            })
        )]
    
    except Exception as e:
        return [TextContent(
            type="text",
            text=json.dumps({
                "success": False,
                "error": str(e)
            })
        )]


async def execute_epic_tool(epic_id: str) -> List[TextContent]:
    """Execute all phases of epic"""
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
                return [TextContent(
                    type="text",
                    text=json.dumps({
                        "success": False,
                        "epic_id": epic_id,
                        "failed_phase": phase,
                        "results": results
                    })
                )]
            
            results.append(phase_result)
        
        return [TextContent(
            type="text",
            text=json.dumps({
                "success": True,
                "epic_id": epic_id,
                "worker_id": WORKER_ID,
                "phases_completed": len(results),
                "results": results
            })
        )]
    
    except Exception as e:
        return [TextContent(
            type="text",
            text=json.dumps({
                "success": False,
                "error": str(e)
            })
        )]


async def release_epic_tool(epic_id: str) -> List[TextContent]:
    """Release epic lock"""
    try:
        roadmap = load_roadmap()
        epic = next((e for e in roadmap if e['epic_number'] == epic_id), None)
        
        if not epic:
            return [TextContent(
                type="text",
                text=json.dumps({
                    "success": False,
                    "error": f"Epic {epic_id} not found"
                })
            )]
        
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
        
        return [TextContent(
            type="text",
            text=json.dumps({
                "success": True,
                "epic_id": epic_id,
                "worker_id": WORKER_ID
            })
        )]
    
    except Exception as e:
        return [TextContent(
            type="text",
            text=json.dumps({
                "success": False,
                "error": str(e)
            })
        )]


async def get_worker_status_tool() -> List[TextContent]:
    """Get worker status"""
    try:
        roadmap = load_roadmap()
        assigned_epic = next(
            (e for e in roadmap if e.get('assigned_to') == WORKER_ID),
            None
        )
        
        status = {
            "worker_id": WORKER_ID,
            "worktree_path": str(WORKTREE_PATH),
            "assigned_epic": assigned_epic['epic_number'] if assigned_epic else None,
            "health": "healthy"
        }
        
        return [TextContent(
            type="text",
            text=json.dumps(status)
        )]
    
    except Exception as e:
        return [TextContent(
            type="text",
            text=json.dumps({
                "worker_id": WORKER_ID,
                "health": "error",
                "error": str(e)
            })
        )]


async def get_next_pending_epic_tool() -> List[TextContent]:
    """Get next pending epic"""
    try:
        roadmap = load_roadmap()
        next_epic = next(
            (e for e in roadmap 
             if e.get('status') != 'complete' and not e.get('assigned_to')),
            None
        )
        
        if not next_epic:
            return [TextContent(
                type="text",
                text=json.dumps({
                    "success": False,
                    "message": "No pending epics found"
                })
            )]
        
        return [TextContent(
            type="text",
            text=json.dumps({
                "success": True,
                "epic_id": next_epic['epic_number'],
                "method": next_epic['method'],
                "file": next_epic['file'],
                "cyclomatic": next_epic['cyclomatic']
            })
        )]
    
    except Exception as e:
        return [TextContent(
            type="text",
            text=json.dumps({
                "success": False,
                "error": str(e)
            })
        )]


async def main():
    """Run MCP server"""
    async with stdio_server() as (read_stream, write_stream):
        await app.run(
            read_stream,
            write_stream,
            app.create_initialization_options()
        )


if __name__ == "__main__":
    asyncio.run(main())

# Made with Bob
