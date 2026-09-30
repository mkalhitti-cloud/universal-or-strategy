#!/usr/bin/env python3
"""
GitButler Integration - After Task Complete Hook

Auto-commits changes to the current GitButler virtual branch with V12-compliant message format.
Optionally pushes the branch to create a GitHub PR.
"""

import subprocess
import sys
import re
from pathlib import Path
from datetime import datetime


def run_command(cmd: list[str]) -> tuple[int, str, str]:
    """Run shell command and return (exit_code, stdout, stderr)."""
    try:
        result = subprocess.run(
            cmd,
            capture_output=True,
            text=True,
            timeout=30
        )
        return result.returncode, result.stdout, result.stderr
    except subprocess.TimeoutExpired:
        return 1, "", "Command timed out"
    except Exception as e:
        return 1, "", str(e)


def get_build_tag() -> str:
    """Extract BUILD_TAG from src/V12_002.cs if it exists."""
    v12_file = Path('src/V12_002.cs')
    if not v12_file.exists():
        return ""
    
    try:
        content = v12_file.read_text(encoding='utf-8')
        # Look for BUILD_TAG pattern: [BUILD_XXXX]
        match = re.search(r'\[BUILD_(\d+)\]', content)
        if match:
            return f"[BUILD_{match.group(1)}]"
    except Exception:
        pass
    
    return ""


def get_current_branch() -> str:
    """Get current GitButler virtual branch name."""
    exit_code, stdout, _ = run_command(['but', 'status'])
    if exit_code != 0:
        return ""
    
    # Parse output to find current branch (marked with ●)
    for line in stdout.split('\n'):
        if '●' in line and '[' in line and ']' in line:
            # Extract branch name from format: "┊╭┄bu [build/1105-monolith]"
            match = re.search(r'\[([^\]]+)\]', line)
            if match:
                return match.group(1)
    
    return ""


def generate_commit_message(task_summary: str, branch_name: str) -> str:
    """
    Generate V12-compliant commit message.
    
    Format: <type>(<scope>): <description> [BUILD_TAG]
    
    Types: feat, fix, refactor, docs, chore, test, ci
    """
    # Detect commit type from branch name or task
    if branch_name.startswith('src/epic-'):
        commit_type = 'refactor'
        scope = 'epic'
    elif branch_name.startswith('src/fix-'):
        commit_type = 'fix'
        scope = 'src'
    elif branch_name.startswith('docs/'):
        commit_type = 'docs'
        scope = 'docs'
    elif branch_name.startswith('infra/'):
        commit_type = 'chore'
        scope = 'infra'
    elif branch_name.startswith('protocol/'):
        commit_type = 'feat'
        scope = 'protocol'
    else:
        commit_type = 'chore'
        scope = 'general'
    
    # Clean up task summary
    summary = task_summary.strip()
    if len(summary) > 72:
        summary = summary[:69] + '...'
    
    # Add BUILD_TAG if available
    build_tag = get_build_tag()
    if build_tag:
        return f"{commit_type}({scope}): {summary} {build_tag}"
    else:
        return f"{commit_type}({scope}): {summary}"


def main():
    """Main hook entry point."""
    # Read task summary from stdin or args
    if len(sys.argv) > 1:
        task_summary = ' '.join(sys.argv[1:])
    else:
        task_summary = sys.stdin.read().strip()
    
    if not task_summary:
        task_summary = "Task complete"
    
    # Check if GitButler CLI is available
    exit_code, _, _ = run_command(['but', '--version'])
    if exit_code != 0:
        print(f"[HOOK] GitButler CLI not found, skipping auto-commit", file=sys.stderr)
        sys.exit(0)
    
    # Check if we're in gitbutler/workspace
    exit_code, current_branch, _ = run_command(['git', 'branch', '--show-current'])
    if exit_code != 0 or 'gitbutler/workspace' not in current_branch:
        print(f"[HOOK] Not in gitbutler/workspace, skipping auto-commit", file=sys.stderr)
        sys.exit(0)
    
    # Get current virtual branch
    branch_name = get_current_branch()
    if not branch_name:
        print(f"[HOOK] Could not detect current virtual branch", file=sys.stderr)
        sys.exit(0)
    
    # Check if there are staged changes
    exit_code, stdout, _ = run_command(['but', 'diff'])
    if exit_code != 0 or not stdout.strip():
        print(f"[HOOK] No changes to commit", file=sys.stderr)
        sys.exit(0)
    
    # Generate commit message
    commit_msg = generate_commit_message(task_summary, branch_name)
    
    # Commit to virtual branch
    print(f"[HOOK] Committing to virtual branch: {branch_name}", file=sys.stderr)
    print(f"[HOOK] Message: {commit_msg}", file=sys.stderr)
    
    exit_code, stdout, stderr = run_command(['but', 'commit', '-m', commit_msg])
    
    if exit_code == 0:
        print(f"[HOOK] ✓ Changes committed to {branch_name}", file=sys.stderr)
        
        # Ask if user wants to push (create PR)
        # For now, don't auto-push - let user decide via `but push`
        print(f"[HOOK] To create PR, run: but push", file=sys.stderr)
    else:
        print(f"[HOOK] ✗ Failed to commit: {stderr}", file=sys.stderr)
        # Don't fail the task if commit fails
        sys.exit(0)


if __name__ == '__main__':
    main()

# Made with Bob
