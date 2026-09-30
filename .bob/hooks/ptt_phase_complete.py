#!/usr/bin/env python3
"""
PTT Phase Complete Hook (V12.36)
Trigger: after_task_complete (run by orchestrator after each phase subtask)

Reads PTT_EPIC_ID, PTT_PHASE, PTT_RESULT env vars.
Records the phase outcome (complete or fail) into the Lamport event log
and syncs manifest.json.

Usage by orchestrator:
  $env:PTT_EPIC_ID = "PTT-COPIER-B9"
  $env:PTT_PHASE   = "plan_review"
  $env:PTT_RESULT  = "REVIEW_PASS"    # or REVIEW_FAIL / BUILD_PASS / VERIFY_PASS / etc.
  python .bob/hooks/ptt_phase_complete.py

Exit codes:
  0 = recorded successfully (or not a PTT task -- skipped)
  1 = recorded as FAIL (PTT_RESULT is a FAIL token)
  2 = usage/config error
"""

import os
import sys
import subprocess
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent

# FAIL tokens — any result containing these substrings is treated as a failure
FAIL_SUFFIXES = ("_FAIL", "_FAILED", "_ERROR")
PASS_SUFFIXES = ("_PASS", "_COMPLETE", "FINAL_PASS")


def main():
    epic_id = os.environ.get("PTT_EPIC_ID", "").strip()
    phase = os.environ.get("PTT_PHASE", "").strip()
    result = os.environ.get("PTT_RESULT", "").strip()

    # Not a PTT task — skip silently
    if not epic_id or not phase or not result:
        sys.exit(0)

    script = REPO_ROOT / "scripts" / "ptt_lamport.py"
    if not script.exists():
        print(f"[ptt-phase-complete] ERROR: {script} not found", file=sys.stderr)
        sys.exit(2)

    is_fail = any(result.endswith(s) for s in FAIL_SUFFIXES)
    is_pass = any(result.endswith(s) or result == s for s in PASS_SUFFIXES) or "PASS" in result or "COMPLETE" in result

    if is_fail:
        subprocess.run(
            [sys.executable, str(script), "fail", epic_id, phase, result],
            capture_output=False
        )
        print(f"[ptt-phase-complete] Recorded FAIL: {phase} -> {result} for {epic_id}")
        sys.exit(1)
    elif is_pass:
        subprocess.run(
            [sys.executable, str(script), "complete", epic_id, phase, result],
            capture_output=False
        )
        print(f"[ptt-phase-complete] Recorded COMPLETE: {phase} -> {result} for {epic_id}")
        sys.exit(0)
    else:
        # Unknown token — log as complete with a warning
        subprocess.run(
            [sys.executable, str(script), "complete", epic_id, phase, result],
            capture_output=False
        )
        print(f"[ptt-phase-complete] WARN: unknown result token '{result}' logged as complete for {phase}")
        sys.exit(0)


if __name__ == "__main__":
    main()
