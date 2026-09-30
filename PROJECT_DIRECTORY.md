# PROJECT DIRECTORY

**Auto-generated**: This file is automatically updated by git hooks when structural changes occur.
**Last Updated**: 2026-06-13 02:06:44

This directory provides a complete cross-reference of all commands, skills, SOPs, and scripts in the Universal OR Strategy V12 project.

---

## Commands

| Command | File | Description |
|---------|------|-------------|

## Skills

| Skill | File | Description |
|-------|------|-------------|
| `gcp-vm-wave-execution` | [`.bob/skills/gcp-vm-wave-execution/skill.md`](.bob/skills/gcp-vm-wave-execution/skill.md) | GCP VM Wave Execution |

## Standard Operating Procedures (SOPs)

| SOP | File | Description |
|-----|------|-------------|
| `WAVE_PHASE_SCRIPT_GENERATION_SOP` | [`docs/workflow/WAVE_PHASE_SCRIPT_GENERATION_SOP.md`](docs/workflow/WAVE_PHASE_SCRIPT_GENERATION_SOP.md) | Wave Phase Script Generation SOP |

## Scripts

| Script | Type | Description |
|--------|------|-------------|
| `__init__.py` | Python |  |
| `agent_bootstrap.py` | Python |  |
| `amal_harness.py` | Python |  |
| `amal_harness_v25.py` | Python | # Pre-clean |
| `amal_harness_v26.py` | Python | import os, re, json, subprocess, sys, html as _html |
| `analyze_roadmap.py` | Python |  |
| `apply_anthropic_colors.py` | Python |  |
| `apply_final_polish.py` | Python |  |
| `capture_lesson.py` | Python |  |
| `check_complete_epics.py` | Python |  |
| `check_completed_epics_in_workers.py` | Python |  |
| `check_epic_status.py` | Python |  |
| `check_roadmap_status.py` | Python |  |
| `check_wave1_targets.py` | Python |  |
| `cleanup_dashboard_styles.py` | Python |  |
| `complexity_audit.py` | Python | import re |
| `context7_cli.py` | Python | try: |
| `csharp_hotspots.py` | Python |  |
| `dead_code_scan.py` | Python | import os, re, sys |
| `debug_extract.py` | Python |  |
| `diff_fixer.py` | Python |  |
| `enhance_dashboard_layout.py` | Python |  |
| `enhance_dashboard_layout2.py` | Python |  |
| `epic_manifest.py` | Python | Epic Manifest Management |
| `epic_planner.py` | Python |  |
| `find_high_complexity_epics.py` | Python | import json |
| `fix_wave1_targets.py` | Python |  |
| `generate_epic_roadmap.py` | Python | result = subprocess.run( |
| `get_linear_team_id.py` | Python | query { |
| `get_next_epics.py` | Python | with open('epic_roadmap.json', 'r') as f: |
| `harden_agents.py` | Python |  |
| `bump_version.py` | Python |  |
| `master_hook.py` | Python |  |
| `safety_guard.py` | Python |  |
| `sync_settings_doc.py` | Python |  |
| `update_task_status.py` | Python |  |
| `jcodemunch_hook.py` | Python |  |
| `langsmith_bridge.py` | Python | Traces the handoff between two agents in the Sovereign fleet. |
| `linear_setup.py` | Python |  |
| `linear_sync.py` | Python |  |
| `linear_sync_v2.py` | Python |  |
| `linear_update_status.py` | Python | api_key = os.getenv("LINEAR_API_KEY") |
| `load_api_keys.py` | Python |  |
| `measure_kb_size.py` | Python |  |
| `monitor_vm_progress.py` | Python |  |
| `negative_evidence_check.py` | Python |  |
| `nexus_relay.py` | Python | mission_id = "V14.2" # Default to current mission |
| `orchestrate_full_epic_execution.py` | Python | import json |
| `orchestrate_phase_execution.py` | Python |  |
| `orchestrate_phase0_with_prep.py` | Python | import sys |
| `orders_callbacks_split.py` | Python |  |
| `orders_management_split.py` | Python |  |
| `phase_0_hotspot_mcp.py` | Python | import asyncio |
| `phase_0_hotspot_mcp_fastmcp.py` | Python |  |
| `phase_1_5_boundary_mcp.py` | Python | Execute Phase 1.5 (Scope Boundary Validation) for an epic. |
| `phase_1_scope_mcp.py` | Python |  |
| `phase_1_scope_mcp_fastmcp.py` | Python | Execute Phase 1 (Scope Definition) for an epic. |
| `phase_2_architecture_mcp.py` | Python | Execute Phase 2 (Architecture Planning) for an epic. |
| `phase_3_audit_mcp.py` | Python | Execute Phase 3 (DNA & PR Audit) for an epic. |
| `phase_4_tickets_mcp.py` | Python | Execute Phase 4 (Ticket Generation) for an epic. |
| `phase_5_execute_mcp.py` | Python | Execute Phase 5 (Ticket Execution) for an epic. |
| `phase_5_verify_mcp.py` | Python | Execute Phase 5.V (Verification) for an epic. |
| `phase_6_review_mcp.py` | Python | Execute Phase 6 (Final Review) for an epic. |
| `prepare_wave1_phase0.py` | Python |  |
| `query_codescene.py` | Python |  |
| `query_kb.py` | Python | # Resolve absolute path based on the script's location |
| `reaper_split.py` | Python | using System; |
| `round26_stress_harness.py` | Python |  |
| `session_continuity.py` | Python |  |
| `session_snapshot.py` | Python |  |
| `sima_split.py` | Python |  |
| `surgical_fix_agents.py` | Python |  |
| `symmetry_split.py` | Python |  |
| `sync_epic_roadmap_from_worker.py` | Python | try: |
| `temp_load_manifest.py` | Python |  |
| `test_fastmcp_phase0.py` | Python |  |
| `test_parallel_phase0.py` | Python | import sys |
| `test_phase_mcp_integration.py` | Python |  |
| `test_phase_mcp_servers.py` | Python |  |
| `test_worker_mcp_client.py` | Python | print(f"\n{'='*60}") |
| `trailing_split.py` | Python |  |
| `ui_ipc_split.py` | Python |  |
| `update_manifest_phase23.py` | Python |  |
| `v12_main_split.py` | Python |  |
| `v12_split.py` | Python | # Find method signature |
| `validate_epic.py` | Python |  |
| `verify_index_freshness.py` | Python |  |
| `wave_coordinator.py` | Python |  |
| `api_balance_tracker.py` | Python |  |
| `check_api_balances.py` | Python | try: |
| `check_phase2_status.py` | Python | epic_id = f"EPIC-CCN-{epic_num}" |
| `check_phase4_local.py` | Python | print("[STATUS] Phase 4 Progress Check") |
| `generate_phase1_scripts.py` | Python |  |
| `generate_phase2_scripts.py` | Python |  |
| `generate_phase3_scripts.py` | Python |  |
| `generate_phase4_scripts.py` | Python |  |
| `get_wave2_complexity.py` | Python | import json |
| `launch_phase0_v4_shell_commands.py` | Python | import subprocess, sys, json |
| `launch_wave.py` | Python |  |
| `launch_wave_now.py` | Python |  |
| `launch_wave_v2.py` | Python |  |
| `launch_wave_v3_multi_api.py` | Python |  |
| `launch_wave_v4_safe_budget.py` | Python |  |
| `monitor_phase4.py` | Python |  |
| `phase4_with_checkpoints.py` | Python |  |
| `phase4_with_checkpoints_v2.py` | Python |  |
| `phase4_with_checkpoints_v3_fixed.py` | Python |  |
| `remove_gates_final.py` | Python |  |
| `reset_phase4_manifests.py` | Python | manifest_path = BRAIN_DIR / f"EPIC-CCN-{epic_id}" / "manifest.json" |
| `test_single_epic_107.py` | Python | epic_key = f"EPIC-CCN-{epic_id}" |
| `track_api_balances.py` | Python |  |
| `wait_for_phase4.py` | Python | completed = 0 |
| `wave2_bob_shell_executor.py` | Python |  |
| `wave2_direct_executor.py` | Python |  |
| `wave2_parallel_executor.py` | Python |  |
| `wave2_simple_orchestrator.py` | Python |  |
| `worker_agent_mcp.py` | Python |  |
| `worker_agent_mcp_fastmcp.py` | Python |  |
| `zero_caller_trace.py` | Python |  |
| `audit_scan.ps1` | PowerShell | Director's Audit Scan (V1.0) |
| `auto-benchmark.ps1` | PowerShell |  |
| `backup_bob_ide_settings.ps1` | PowerShell | Backup Bob IDE Settings and Conversations |
| `bob_logout.ps1` | PowerShell | Bob Shell Logout Script |
| `build_readiness.ps1` | PowerShell | scripts/build_readiness.ps1 |
| `check_pr_scope.ps1` | PowerShell | check_pr_scope.ps1 |
| `cleanup_branches.ps1` | PowerShell | Branch Cleanup Script - V12 Universal OR Strategy |
| `commit-to-main.ps1` | PowerShell | Commit Non-.cs Files to Main (While Preserving PR Work) |
| `create_golden_image_v3.ps1` | PowerShell | Create Golden Image v3 - Self-Orchestrating Parallel Execution |
| `create_worktree_auto_approval.ps1` | PowerShell | Create Auto-Approval Settings for Worktrees |
| `delete_obsolete_branches.ps1` | PowerShell | GitHub Branch Cleanup Script |
| `diag_fleet.ps1` | PowerShell | Diagnostic: Get fleet state after SIMA toggle stress |
| `extract_battle_results.ps1` | PowerShell | Clear existing contents if any |
| `extract_pr_forensics.ps1` | PowerShell | PR Forensics Extraction Script |
| `format_all_csharp.ps1` | PowerShell | Format All C# Files - Automated CSharpier Runner |
| `get_github_username.ps1` | PowerShell | Get GitHub Username Helper |
| `git_src_only.ps1` | PowerShell | scripts/git_src_only.ps1 |
| `launch_wave2_parallel.ps1` | PowerShell | Wave 2 Parallel Execution Launcher |
| `launch_wave2_parallel_robust.ps1` | PowerShell | Wave 2 Parallel Execution - Robust Solution |
| `launch_wave2_test_2_agents.ps1` | PowerShell | Wave 2 TEST - 2 Parallel Agents |
| `launch_wave2_v3_final.ps1` | PowerShell | Wave 2 Launch - Final Version with Orchestrator Upload |
| `launch_wave2_v3_metadata.ps1` | PowerShell | Wave 2 Launch - Golden Image v3 with Metadata-Driven Orchestration |
| `launch_worker_orchestrators.ps1` | PowerShell | Launch 3 Worker Orchestrator Windows |
| `lint.ps1` | PowerShell | scripts/lint.ps1 |
| `monitor_pr_checks.ps1` | PowerShell | !/usr/bin/env pwsh |
| `nexus_watch.ps1` | PowerShell | Nexus Watchdog (V1.0) - Multi-Agent Orchestrator |
| `patch_path.ps1` | PowerShell |  |
| `pre_battle_hook.ps1` | PowerShell | scripts/pre_battle_hook.ps1 |
| `pre_push_validation.ps1` | PowerShell | V12 Pre-Push Validation Suite |
| `query_codacy_issues.ps1` | PowerShell | Query Codacy API for PR issues |
| `query_codacy_with_env.ps1` | PowerShell | Load .env file and run Codacy query |
| `resolve_comments.ps1` | PowerShell |  |
| `run_linear_update.ps1` | PowerShell | Run Linear Update with proper environment setup |
| `run_semgrep.ps1` | PowerShell | Run Semgrep Scan for V12 DNA Compliance |
| `setup_linear_env.ps1` | PowerShell | Setup Linear Environment Variables Permanently |
| `setup_parallel_epic_workflow.ps1` | PowerShell | Parallel Epic Workflow Setup Script |
| `setup_yolo_mode.ps1` | PowerShell | Setup YOLO Mode for Bob CLI |
| `sima_toggle_stress.ps1` | PowerShell | A-4 SIMA Toggle Stress Test |
| `start-dev-day.ps1` | PowerShell | !/usr/bin/env pwsh |
| `sync_all_worktrees.ps1` | PowerShell | Sync All Worktrees with Latest Infrastructure |
| `test_codacy_api.ps1` | PowerShell | Test Codacy API connectivity and response |
| `test_stress.ps1` | PowerShell | scripts/test_stress.ps1 |
| `verify_links.ps1` | PowerShell |  |
| `verify_pr_hygiene.ps1` | PowerShell | scripts/verify_pr_hygiene.ps1 |
| `verify_reorg.ps1` | PowerShell | verify_reorg.ps1 |
| `accept_ssh_key.ps1` | PowerShell | Accept SSH key for GCP VM |
| `add_yolo_flag.ps1` | PowerShell | Add --yolo flag to all Phase 0 scripts |
| `ask_bob_interactive.ps1` | PowerShell | Ask Bob Shell on VM about file persistence (interactive - you'll see output) |
| `cleanup_bad_scripts.ps1` | PowerShell | Cleanup Bad Wave 2 Scripts (Tool Bug Instructions) |
| `deploy_and_test_tool_fix.ps1` | PowerShell | Deploy Fixed custom_modes.yaml and Test |
| `deploy_fixed_custom_modes_and_test.ps1` | PowerShell | Deploy Fixed custom_modes.yaml and Test Phase 0 |
| `download_wave2_logs.ps1` | PowerShell | Download Wave 2 v4 Logs from GCP VM |
| `fix_phase0_scripts.ps1` | PowerShell | Fix Phase 0 scripts: Replace run_shell_command with execute_command |
| `fix_phase1_apikey_field.ps1` | PowerShell | Fix Phase 1 scripts to use correct JSON field name |
| `fix_phase1_bash_login.ps1` | PowerShell | Fix Phase 1 scripts to use bash -l (login shell) for PATH loading |
| `fix_slash_commands.ps1` | PowerShell | Fix Wave 2 Scripts to Use Slash Commands |
| `fix_threshold_to_8.ps1` | PowerShell | Fix Complexity Threshold: 15 → 8 |
| `generate_fixed_phase0_scripts.ps1` | PowerShell | Generate Fixed Phase 0 Scripts with execute_command |
| `record_wave2_v4_usage.ps1` | PowerShell | Record Wave 2 v4 actual usage (3.23 bobcoins per epic for Phases 0-3) |
| `remove_director_gates.ps1` | PowerShell | Remove Director Approval Gates from Autonomous Refactor Commands |
| `remove_director_gates_v2.ps1` | PowerShell | Remove Director Approval Gates from Autonomous Refactor Commands |
| `launch_wave2_parallel.sh` | Bash | !/bin/bash |
| `launch_wave2_sequential.sh` | Bash | !/bin/bash |
| `launch_wave2_test_2_agents.sh` | Bash | !/bin/bash |
| `test_epic_164.sh` | Bash | !/bin/bash |
| `test_epic_164_phase_1_5.sh` | Bash | !/bin/bash |
| `test_epic_simple.sh` | Bash | !/bin/bash |
| `verify_vm_ready.sh` | Bash | !/bin/bash |
| `vm_install_bob.sh` | Bash | !/bin/bash |
| `vm_manual_setup_commands.sh` | Bash | !/bin/bash |
| `vm_setup_and_run.sh` | Bash | !/bin/bash |
| `vm_setup_fixed.sh` | Bash | !/bin/bash |
| `vm_startup_script.sh` | Bash | !/bin/bash |
| `vm_startup_script_v10_golden_v2_fixed.sh` | Bash | !/bin/bash |
| `vm_startup_script_v11_golden_v3_python_fix.sh` | Bash | !/bin/bash |
| `vm_startup_script_v12_golden_v3_orchestrator.sh` | Bash | !/bin/bash |
| `vm_startup_script_v2.sh` | Bash | !/bin/bash |
| `vm_startup_script_v3.sh` | Bash | !/bin/bash |
| `vm_startup_script_v4.sh` | Bash | !/bin/bash |
| `vm_startup_script_v5_mise.sh` | Bash | !/bin/bash |
| `vm_startup_script_v6.sh` | Bash | !/bin/bash |
| `vm_startup_script_v7.sh` | Bash | !/bin/bash |
| `vm_startup_script_v8.sh` | Bash | !/bin/bash |
| `vm_startup_script_v9_golden_v2.sh` | Bash | !/bin/bash |
| `_debug_write_test.sh` | Bash | !/bin/bash |
| `_wave2_launch_generated.sh` | Bash | !/bin/bash |
| `_wave2_v2_launch_generated.sh` | Bash | !/bin/bash |
| `_wave2_v3_launch_generated.sh` | Bash | !/bin/bash |
| `_wave2_v4_launch_generated.sh` | Bash | !/bin/bash |
| `add_yolo_flag.sh` | Bash | !/bin/bash |
| `apply_execute_command_fix.sh` | Bash | !/bin/bash |
| `ask_bob_about_persistence.sh` | Bash | !/bin/bash |
| `ask_bob_on_vm.sh` | Bash | !/bin/bash |
| `check_all_phase2.sh` | Bash | !/bin/bash |
| `fix_api_key_env.sh` | Bash | !/bin/bash |
| `fix_bob_command.sh` | Bash | !/bin/bash |
| `fix_phase0_scripts.sh` | Bash | !/bin/bash |
| `launch_phase0_all.sh` | Bash | !/bin/bash |
| `launch_phase0_all_screen.sh` | Bash | !/bin/bash |
| `orchestrator.sh` | Bash | !/bin/bash |
| `smoke_test.sh` | Bash | !/bin/bash |
| `stop_wave2.sh` | Bash | !/bin/bash |
| `test_read_file.sh` | Bash | !/bin/bash |
| `test_write_then_read.sh` | Bash | !/bin/bash |
| `update_p0_scripts_with_balance.sh` | Bash | !/bin/bash |


---

## Usage

- **Commands**: Use with Bob CLI (`bob <command>`)
- **Skills**: Activate with `use_skill` tool
- **SOPs**: Follow for standardized workflows
- **Scripts**: Execute directly or via commands

## Maintenance

This file is automatically updated by `.git/hooks/modules/project-directory.sh` when:
- Files are added, deleted, or renamed in tracked directories
- Tracked directories: `.bob/commands/`, `.bob/skills/`, `docs/workflow/`, `scripts/`

To manually regenerate: `bash .git/hooks/modules/project-directory.sh generate`
