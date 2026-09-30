#!/usr/bin/env python3
"""
GitButler Integration - Before New Task Hook

Auto-creates a GitButler virtual branch when Bob CLI starts a new task.
Branch naming follows V12 three-tier model:
- Tier 1: src/ (source code only)
- Tier 2: infra/ (docs, scripts, workflows)
- Tier 3: protocol/ (agent rules, MCP configs)
"""

import subprocess
import sys
import re
import json
from pathlib import Path


def sanitize_branch_name(name: str) -> str:
    """Convert task description to valid git branch name."""
    # Remove special characters, convert to lowercase
    name = re.sub(r'[^\w\s-]', '', name.lower())
    # Replace spaces with hyphens
    name = re.sub(r'\s+', '-', name)
    # Remove consecutive hyphens
    name = re.sub(r'-+', '-', name)
    # Trim hyphens from ends
    return name.strip('-')


def detect_task_tier(task_description: str) -> str:
    """
    Detect which tier the task belongs to based on keywords.
    
    Returns: 'src', 'docs', 'infra', or 'protocol'
    """
    task_lower = task_description.lower()
    
    # Tier 1: Source code changes
    src_keywords = ['fix', 'feat', 'refactor', 'epic', 'ticket', 'build', 'compile']
    if any(kw in task_lower for kw in src_keywords):
        return 'src'
    
    # Tier 3: Protocol/agent changes
    protocol_keywords = ['agent', 'mode', 'command', 'mcp', 'skill', 'workflow']
    if any(kw in task_lower for kw in protocol_keywords):
        return 'protocol'
    
    # Tier 2: Infrastructure (docs, scripts, CI)
    infra_keywords = ['docs', 'script', 'ci', 'github', 'workflow', 'readme']
    if any(kw in task_lower for kw in infra_keywords):
        return 'infra'
    
    # Default to docs for documentation-only changes
    if 'doc' in task_lower or 'readme' in task_lower:
        return 'docs'
    
    # Default to src for ambiguous tasks
    return 'src'


def run_command(cmd: list[str]) -> tuple[int, str, str]:
    """Run shell command and return (exit_code, stdout, stderr)."""
    try:
        result = subprocess.run(
            cmd,
            capture_output=True,
            text=True,
            timeout=10
        )
        return result.returncode, result.stdout, result.stderr
    except subprocess.TimeoutExpired:
        return 1, "", "Command timed out"
    except Exception as e:
        return 1, "", str(e)


def main():
    """Main hook entry point."""
    # Read task description from stdin or args
    if len(sys.argv) > 1:
        task_description = ' '.join(sys.argv[1:])
    else:
        task_description = sys.stdin.read().strip()
    
    if not task_description:
        print("[HOOK] No task description provided, skipping branch creation", file=sys.stderr)
        sys.exit(0)
    
    # Detect tier and sanitize name
    tier = detect_task_tier(task_description)
    sanitized = sanitize_branch_name(task_description)
    branch_name = f"{tier}/{sanitized}"
    
    # Check if GitButler CLI is available
    exit_code, _, _ = run_command(['but', '--version'])
    if exit_code != 0:
        print(f"[HOOK] GitButler CLI not found, skipping branch creation", file=sys.stderr)
        print(f"[HOOK] Would have created: {branch_name}", file=sys.stderr)
        sys.exit(0)
    
    # Check if we're in gitbutler/workspace
    exit_code, current_branch, _ = run_command(['git', 'branch', '--show-current'])
    if exit_code != 0 or 'gitbutler/workspace' not in current_branch:
        print(f"[HOOK] Not in gitbutler/workspace, skipping branch creation", file=sys.stderr)
        sys.exit(0)
    
    # Create virtual branch
    print(f"[HOOK] Creating GitButler virtual branch: {branch_name}", file=sys.stderr)
    exit_code, stdout, stderr = run_command(['but', 'branch', 'new', branch_name])
    
    if exit_code == 0:
        print(f"[HOOK] ✓ Virtual branch created: {branch_name}", file=sys.stderr)
        print(f"[HOOK] Task tier: {tier}", file=sys.stderr)
    else:
        print(f"[HOOK] ✗ Failed to create branch: {stderr}", file=sys.stderr)
        # Don't fail the task if branch creation fails
        sys.exit(0)


if __name__ == '__main__':
    main()

# Made with Bob
