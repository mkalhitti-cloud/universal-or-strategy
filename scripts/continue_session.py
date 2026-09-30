#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Continue Session Manager - State management for /continue command workflow

This module provides utilities for managing continuous session state across
multiple task executions. Each task runs in a fresh session with minimal
context loaded from .continue/state.json.

Usage:
    # Initialize new session
    python scripts/continue_session.py init "Fix MCP configuration"
    
    # Get minimal context for next session
    python scripts/continue_session.py context
    
    # Complete current task
    python scripts/continue_session.py complete "Removed Greptile MCP" .mcp.json .mcp.json.vm
    
    # Show current state
    python scripts/continue_session.py status
"""

import json
import os
import sys
from datetime import datetime, timezone
from pathlib import Path
from typing import Dict, List, Optional

# Constants
STATE_DIR = Path(".continue")
STATE_FILE = STATE_DIR / "state.json"
REPO_ROOT = Path(__file__).parent.parent


def ensure_state_dir():
    """Create .continue directory if it doesn't exist."""
    STATE_DIR.mkdir(exist_ok=True)


def get_git_info() -> Dict[str, str]:
    """Get current git branch and commit hash."""
    import subprocess
    
    try:
        branch = subprocess.run(
            ["git", "branch", "--show-current"],
            capture_output=True,
            text=True,
            check=True
        ).stdout.strip()
        
        commit = subprocess.run(
            ["git", "rev-parse", "--short", "HEAD"],
            capture_output=True,
            text=True,
            check=True
        ).stdout.strip()
        
        return {"branch": branch, "commit": commit}
    except subprocess.CalledProcessError:
        return {"branch": "unknown", "commit": "unknown"}


def load_state() -> Optional[Dict]:
    """Load state from .continue/state.json."""
    if not STATE_FILE.exists():
        return None
    
    try:
        with open(STATE_FILE, 'r', encoding='utf-8') as f:
            return json.load(f)
    except (json.JSONDecodeError, IOError) as e:
        print(f"Error loading state: {e}", file=sys.stderr)
        return None


def save_state(state: Dict) -> None:
    """Save state to .continue/state.json."""
    ensure_state_dir()
    
    try:
        with open(STATE_FILE, 'w', encoding='utf-8') as f:
            json.dump(state, f, indent=2, ensure_ascii=False)
    except IOError as e:
        print(f"Error saving state: {e}", file=sys.stderr)
        sys.exit(1)


def init_session(task_description: str) -> Dict:
    """
    Initialize new /continue session.
    
    Args:
        task_description: Description of the task to execute
        
    Returns:
        Updated state dict
    """
    state = load_state()
    git_info = get_git_info()
    now = datetime.now(timezone.utc).isoformat().replace('+00:00', 'Z')
    
    if state is None:
        # First session - create new state
        state = {
            "session_id": f"continue-{datetime.now(timezone.utc).strftime('%Y-%m-%d-%H%M')}",
            "created_at": now,
            "updated_at": now,
            "current_task": {
                "id": 1,
                "description": task_description,
                "status": "in_progress",
                "started_at": now
            },
            "completed_tasks": [],
            "context": {
                "wave": 7,
                "branch": git_info["branch"],
                "vm_status": "unknown",
                "vm_ip": "34.121.187.241",
                "last_commit": git_info["commit"]
            },
            "next_tasks": []
        }
    else:
        # Continuing session - start new task
        next_id = len(state["completed_tasks"]) + 1
        state["current_task"] = {
            "id": next_id,
            "description": task_description,
            "status": "in_progress",
            "started_at": now
        }
        state["updated_at"] = now
        state["context"]["last_commit"] = git_info["commit"]
    
    save_state(state)
    return state


def complete_task(summary: str, artifacts: List[str]) -> Dict:
    """
    Mark current task as completed.
    
    Args:
        summary: One-line summary of what was accomplished
        artifacts: List of files created/modified
        
    Returns:
        Updated state dict
    """
    state = load_state()
    if state is None:
        print("Error: No active session. Run 'init' first.", file=sys.stderr)
        sys.exit(1)
    
    if state["current_task"]["status"] != "in_progress":
        print("Error: No task in progress.", file=sys.stderr)
        sys.exit(1)
    
    now = datetime.now(timezone.utc).isoformat().replace('+00:00', 'Z')
    
    # Move current task to completed
    completed_task = state["current_task"].copy()
    completed_task["status"] = "completed"
    completed_task["completed_at"] = now
    completed_task["summary"] = summary
    completed_task["artifacts"] = artifacts
    
    state["completed_tasks"].append(completed_task)
    state["current_task"] = {"status": "none"}
    state["updated_at"] = now
    
    save_state(state)
    return state


def get_minimal_context() -> str:
    """
    Generate minimal context block for next session.
    
    Returns:
        Markdown-formatted context string (~500 tokens)
    """
    state = load_state()
    if state is None:
        return "## Session Context\n\nNo previous session found. Starting fresh.\n"
    
    context = state["context"]
    completed = state["completed_tasks"]
    current = state["current_task"]
    
    # Build context block
    lines = [
        "## Session Context (from /continue)",
        "",
        f"**Wave**: {context['wave']} (180 epics, CYC > 8 -> CYC <= 8)",
        f"**Branch**: {context['branch']}",
        f"**VM**: {context['vm_status']} ({context['vm_ip']})",
        f"**Last Commit**: {context['last_commit']}",
        "",
        "**Completed Tasks**:"
    ]
    
    if not completed:
        lines.append("(none)")
    else:
        for task in completed:
            lines.append(f"{task['id']}. [DONE] {task['description']} ({task['summary']})")
    
    lines.append("")
    
    if current.get("status") == "in_progress":
        lines.append(f"**Current Task**: {current['description']}")
    else:
        lines.append("**Current Task**: (none - awaiting /continue)")
    
    return "\n".join(lines)


def show_status() -> None:
    """Display current session status."""
    state = load_state()
    if state is None:
        print("No active session.")
        return
    
    print(f"Session ID: {state['session_id']}")
    print(f"Created: {state['created_at']}")
    print(f"Updated: {state['updated_at']}")
    print()
    
    print(f"Wave: {state['context']['wave']}")
    print(f"Branch: {state['context']['branch']}")
    print(f"VM: {state['context']['vm_status']} ({state['context']['vm_ip']})")
    print(f"Last Commit: {state['context']['last_commit']}")
    print()
    
    print(f"Completed Tasks: {len(state['completed_tasks'])}")
    for task in state['completed_tasks']:
        print(f"  {task['id']}. [DONE] {task['description']}")
        print(f"     {task['summary']}")
    
    print()
    current = state['current_task']
    if current.get('status') == 'in_progress':
        print(f"Current Task: {current['description']}")
        print(f"  Started: {current['started_at']}")
    else:
        print("Current Task: (none)")


def main():
    """CLI entry point."""
    # Force UTF-8 output on Windows
    import sys
    if sys.platform == 'win32':
        import codecs
        sys.stdout = codecs.getwriter('utf-8')(sys.stdout.buffer, 'strict')
        sys.stderr = codecs.getwriter('utf-8')(sys.stderr.buffer, 'strict')
    
    if len(sys.argv) < 2:
        print("Usage: continue_session.py <command> [args...]")
        print()
        print("Commands:")
        print("  init <description>     Initialize new task")
        print("  complete <summary> [artifacts...]  Complete current task")
        print("  context                Get minimal context for next session")
        print("  status                 Show current session status")
        sys.exit(1)
    
    command = sys.argv[1]
    
    if command == "init":
        if len(sys.argv) < 3:
            print("Error: Task description required", file=sys.stderr)
            sys.exit(1)
        
        task_description = sys.argv[2]
        state = init_session(task_description)
        print(f"[OK] Initialized task {state['current_task']['id']}: {task_description}")
    
    elif command == "complete":
        if len(sys.argv) < 3:
            print("Error: Summary required", file=sys.stderr)
            sys.exit(1)
        
        summary = sys.argv[2]
        artifacts = sys.argv[3:] if len(sys.argv) > 3 else []
        state = complete_task(summary, artifacts)
        
        completed = state['completed_tasks'][-1]
        print(f"[OK] Task {completed['id']} Complete: {completed['description']}")
        print(f"   Summary: {summary}")
        if artifacts:
            print(f"   Artifacts: {', '.join(artifacts)}")
    
    elif command == "context":
        context = get_minimal_context()
        print(context)
    
    elif command == "status":
        show_status()
    
    else:
        print(f"Error: Unknown command '{command}'", file=sys.stderr)
        sys.exit(1)


if __name__ == "__main__":
    main()

# Made with Bob
