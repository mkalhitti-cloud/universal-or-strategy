---
name: antigravity-doctor
description: Diagnoses and repairs Antigravity IDE hangs, generation failures, and Git config conflicts. Use when Antigravity shows "Working..." indefinitely, generation stops abruptly, or slash commands fail.
---

# Antigravity Doctor

This skill automates the recovery of the Antigravity IDE and Gemini environment.

## When to Use

Trigger this skill if the user reports:
- Antigravity is stuck on "Working..."
- Conversations load but generation stops immediately.
- Slash (`/`) menu is missing in the IDE.
- Errors regarding `worktreeConfig` or `repositoryformatversion`.

## Recovery Workflow

1.  **Run Diagnosis & Repair**: Execute the recovery script to clear stale locks and fix configurations.
    ```powershell
    powershell -File .gemini/skills/antigravity-doctor/scripts/doctor.ps1
    ```

2.  **Verify Skill Directory**: If the slash menu is missing, ensure the skill path at `~\.gemini\antigravity\skills\superpowers` is a **physical directory**, not a junction. Junctions often fail as "untrusted mount points" on Windows.

3.  **Optimize Scan Path**: Ensure `.claudeignore` contains large, non-source directories (e.g., `vendor/`, `ARCHIVE_V12/`) to prevent language server memory crashes.
    
4.  **Heavy Directory Detection**: The script scans for directories with >50,000 files (e.g., untracked T3 projects or massive data folders). These must be relocated outside the repository root or explicitly ignored to maintain IDE performance.

5.  **Restart**: Instruct the user to restart Antigravity and the terminal to pick up the changes.

