#!/usr/bin/env python3
"""
PTT Phase Gate Hook (V12.36)
run_order: 2  (runs after utf8_repair, before task execution)

Reads PTT_EPIC_ID and PTT_PHASE env vars.
If set, verifies via ptt_lamport.py gate that all prerequisite phases
are complete before allowing the current phase to run.

This makes the /nt-builder pipeline DETERMINISTIC:
- No phase can start before its prerequisite
- Prevents plan_review running before architect finishes
- Prevents final_review running before all verifiers pass

Exit codes:
  0 = GATE OPEN or env vars not set (not a PTT task -- skip silently)
  1 = GATE CLOSED -- prerequisite phases incomplete, work stops
  2 = usage/config error
"""

import os
import sys
import subprocess
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent


def main():
    epic_id = os.environ.get("PTT_EPIC_ID", "").strip()
    phase = os.environ.get("PTT_PHASE", "").strip()

    # Not a PTT task — skip silently (non-blocking for non-PTT workflows)
    if not epic_id or not phase:
        sys.exit(0)

    # Run gate check via ptt_lamport.py
    script = REPO_ROOT / "scripts" / "ptt_lamport.py"
    if not script.exists():
        print(f"[ptt-phase-gate] ERROR: {script} not found", file=sys.stderr)
        sys.exit(2)

    result = subprocess.run(
        [sys.executable, str(script), "gate", epic_id, phase],
        capture_output=False  # let stdout print directly so orchestrator sees it
    )

    if result.returncode == 0:
        # Gate open — also log phase_start
        subprocess.run(
            [sys.executable, str(script), "start", epic_id, phase],
            capture_output=False
        )
        sys.exit(0)
    elif result.returncode == 1:
        print(
            f"\n[ptt-phase-gate] BLOCKED: Phase '{phase}' for '{epic_id}' cannot start.\n"
            f"  Prerequisite phase(s) not complete.\n"
            f"  Run: python scripts/ptt_lamport.py status {epic_id}\n"
            f"  to see current pipeline state.\n",
            file=sys.stderr
        )
        sys.exit(1)
    else:
        print(f"[ptt-phase-gate] ERROR: gate script returned {result.returncode}", file=sys.stderr)
        sys.exit(2)


if __name__ == "__main__":
    main()
