#!/usr/bin/env python3
"""
Minimal after_task hook for GitButler workspace model.
Provides basic auto-commit functionality for state preservation.

Version: 1.0 (Minimal)
Status: ACTIVE
"""

import subprocess
import sys
import os
from pathlib import Path
from datetime import datetime

# Force UTF-8 encoding for stdout/stderr on Windows
if sys.platform == 'win32':
    sys.stdout.reconfigure(encoding='utf-8', errors='replace')
    sys.stderr.reconfigure(encoding='utf-8', errors='replace')


def run_command(cmd, cwd=None):
    """Execute shell command and return output."""
    try:
        result = subprocess.run(
            cmd,
            shell=True,
            cwd=cwd,
            capture_output=True,
            text=True,
            encoding='utf-8',
            errors='replace',
            timeout=30
        )
        return result.returncode == 0, result.stdout or "", result.stderr or ""
    except Exception as e:
        return False, "", str(e)


def get_changed_files():
    """Get list of changed files in working directory."""
    success, stdout, _ = run_command("git status --porcelain")
    if not success:
        return []
    
    files = []
    for line in stdout.strip().split('\n'):
        if line:
            # Parse git status output (e.g., " M file.cs", "?? file.md")
            status = line[:2]
            filepath = line[3:].strip()
            files.append((status, filepath))
    
    return files


def categorize_files(files):
    """Categorize files into .cs and non-.cs."""
    cs_files = []
    non_cs_files = []
    
    for status, filepath in files:
        if filepath.endswith('.cs'):
            cs_files.append(filepath)
        else:
            non_cs_files.append(filepath)
    
    return cs_files, non_cs_files


def generate_commit_message(cs_files, non_cs_files):
    """Generate V12-compliant commit message."""
    timestamp = datetime.now().strftime("%Y-%m-%d %H:%M:%S")
    
    if cs_files and non_cs_files:
        return f"chore(workspace): auto-commit mixed changes [{timestamp}]"
    elif cs_files:
        return f"feat(src): auto-commit code changes [{timestamp}]"
    elif non_cs_files:
        return f"docs(workspace): auto-commit non-code changes [{timestamp}]"
    else:
        return f"chore(workspace): auto-commit [{timestamp}]"


def auto_commit():
    """Perform automatic commit of changed files."""
    print("[after_task] Checking for changes...")
    
    # Get changed files
    files = get_changed_files()
    if not files:
        print("[after_task] No changes detected. Skipping auto-commit.")
        return True
    
    # Categorize files
    cs_files, non_cs_files = categorize_files(files)
    
    print(f"[after_task] Found {len(cs_files)} .cs files, {len(non_cs_files)} non-.cs files")
    
    # Stage all changes
    success, _, stderr = run_command("git add -A")
    if not success:
        print(f"[after_task] ERROR: Failed to stage files: {stderr}")
        return False
    
    # Generate commit message
    message = generate_commit_message(cs_files, non_cs_files)
    
    # Commit changes
    success, stdout, stderr = run_command(f'git commit -m "{message}"')
    if not success:
        if "nothing to commit" in stderr:
            print("[after_task] Nothing to commit (already committed).")
            return True
        print(f"[after_task] ERROR: Failed to commit: {stderr}")
        return False
    
    print(f"[after_task] SUCCESS: Auto-commit successful: {message}")
    
    # Show commit info
    success, stdout, _ = run_command("git log -1 --oneline")
    if success:
        print(f"[after_task] Commit: {stdout.strip()}")
    
    return True


def main():
    """Main hook execution."""
    print("\n" + "="*60)
    print("[after_task] GitButler Workspace Hook - Minimal Version")
    print("="*60)
    
    # Verify we're on gitbutler/workspace
    success, stdout, _ = run_command("git branch --show-current")
    if not success or "gitbutler/workspace" not in stdout:
        print("[after_task] WARNING: Not on gitbutler/workspace branch")
        print("[after_task] Skipping auto-commit for safety")
        return 0
    
    print(f"[after_task] Branch: {stdout.strip()}")
    
    # Perform auto-commit
    if auto_commit():
        print("[after_task] SUCCESS: Hook completed successfully")
        return 0
    else:
        print("[after_task] FAILED: Hook failed")
        return 1


if __name__ == "__main__":
    sys.exit(main())

# Made with Bob
