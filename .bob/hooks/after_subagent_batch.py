#!/usr/bin/env python3
"""
After-Subagent-Batch Hook (V2.8)

Fires automatically after every batch of subagents returns in a wave-orch-phaseN
or v12-phaseN-* session. Runs wave7_batch_audit.py deterministically and:

  - EXIT 0  → batch is fully compliant; orchestrator may proceed
  - EXIT 1  → one or more epics failed; orchestrator MUST retry failed IDs
              before moving to next batch (redo list written to /tmp/wave7_redo.txt)
  - EXIT 2  → hook invocation error (missing args, script not found, etc.)

Environment variables consumed (set by orchestrator before yielding):
  WAVE7_BATCH_PHASE    Phase being audited: "0", "1", "1.5", "2", etc.
  WAVE7_BATCH_EPICS    Space-separated epic IDs in the just-completed batch
                       e.g. "EPIC-W7-001 EPIC-W7-002 EPIC-W7-003"
  WAVE7_BATCH_TICKET   (optional) Ticket ID for phase 5/5v

Output files:
  /tmp/wave7_redo.txt          — epic IDs that must be re-spawned (one per line)
  /tmp/wave7_audit_result.json — full JSON audit for this batch

The orchestrator reads /tmp/wave7_redo.txt after the hook exits:
  - If empty (or not created): all passed, proceed
  - If non-empty: spawn exactly those epics again before next batch

Integration:
  This hook is registered in .bob/hooks.json under "after_subagent_batch".
  Bob IDE calls it automatically when a session that includes wave7_batch_context
  completes a batch (i.e. all spawn_subagent calls in the batch have returned).
  Orchestrators running in wave-orch-phase* modes also call it explicitly via:
    python .bob/hooks/after_subagent_batch.py
  after collecting all worker returns.
"""

import json
import os
import subprocess
import sys
from pathlib import Path
from datetime import datetime, timezone

AUDIT_SCRIPT  = Path(__file__).parent.parent.parent / "scripts" / "wave7_batch_audit.py"
REDO_FILE     = Path("/tmp/wave7_redo.txt")
RESULT_FILE   = Path("/tmp/wave7_audit_result.json")
LAMPORT_LOG   = Path(".lamport/wave7/event_log.jsonl")


def get_lamport_clock() -> int:
    """Read current max Lamport clock from event log."""
    if not LAMPORT_LOG.exists():
        return 0
    max_clock = 0
    try:
        for line in LAMPORT_LOG.read_text(encoding="utf-8").splitlines():
            line = line.strip()
            if not line:
                continue
            ev = json.loads(line)
            max_clock = max(max_clock, ev.get("lamport_clock", 0))
    except Exception:
        pass
    return max_clock


def log_lamport(event_type: str, status: str, note: str, phase: str, extras: dict = None):
    """Append a Lamport-clocked event to the wave 7 event log."""
    clock = get_lamport_clock() + 1
    event = {
        "timestamp":     datetime.now(timezone.utc).isoformat(),
        "lamport_clock": clock,
        "epic_id":       "WAVE-7",
        "phase":         phase,
        "tier":          "hook",
        "event_type":    event_type,
        "status":        status,
        "note":          note,
    }
    if extras:
        event.update(extras)
    try:
        with open(LAMPORT_LOG, "a", encoding="utf-8") as f:
            f.write(json.dumps(event) + "\n")
    except Exception as e:
        print(f"[hook] WARNING: Could not write Lamport event: {e}", file=sys.stderr)


def main() -> int:
    # ------------------------------------------------------------------
    # 1. Read configuration from environment
    # ------------------------------------------------------------------
    phase   = os.environ.get("WAVE7_BATCH_PHASE", "").strip()
    epics_s = os.environ.get("WAVE7_BATCH_EPICS", "").strip()
    ticket  = os.environ.get("WAVE7_BATCH_TICKET", "").strip() or None

    # Allow CLI override for manual runs:
    #   python .bob/hooks/after_subagent_batch.py --phase 0 --epics EPIC-W7-001 ...
    if not phase and len(sys.argv) > 1:
        for i, arg in enumerate(sys.argv[1:], 1):
            if arg == "--phase" and i + 1 < len(sys.argv):
                phase = sys.argv[i + 1]
            elif arg == "--epics":
                epics_s = " ".join(sys.argv[i + 1:])
                break

    if not phase:
        print("[hook] ERROR: WAVE7_BATCH_PHASE not set. "
              "Set env var or pass --phase <N>.", file=sys.stderr)
        return 2

    epic_ids = [e for e in epics_s.split() if e.startswith("EPIC-W7-")]
    if not epic_ids:
        print("[hook] ERROR: No EPIC-W7-* IDs found in WAVE7_BATCH_EPICS.", file=sys.stderr)
        return 2

    if not AUDIT_SCRIPT.exists():
        print(f"[hook] ERROR: Audit script not found: {AUDIT_SCRIPT}", file=sys.stderr)
        return 2

    # ------------------------------------------------------------------
    # 2. Run the deterministic audit script
    # ------------------------------------------------------------------
    print(f"\n[hook] ── after_subagent_batch ──────────────────────────────")
    print(f"[hook] Phase: {phase} | Batch size: {len(epic_ids)}")
    print(f"[hook] Epics: {', '.join(epic_ids[:6])}{'...' if len(epic_ids) > 6 else ''}")
    print(f"[hook] Running: wave7_batch_audit.py --phase {phase} ...")

    cmd = [
        sys.executable,
        str(AUDIT_SCRIPT),
        "--phase", phase,
        "--epics", *epic_ids,
        "--json",
        "--fail-file", str(REDO_FILE),
    ]
    if ticket:
        cmd += ["--ticket", ticket]

    try:
        proc = subprocess.run(
            cmd,
            capture_output=True,
            text=True,
            encoding="utf-8",
            timeout=120,   # 2-minute hard timeout for 40-epic batch
        )
    except subprocess.TimeoutExpired:
        print("[hook] ERROR: Audit script timed out (120s).", file=sys.stderr)
        log_lamport("batch_audit_timeout", "error",
                    f"Phase {phase} batch audit timed out after 120s. {len(epic_ids)} epics.", phase)
        return 1
    except Exception as e:
        print(f"[hook] ERROR: Failed to run audit script: {e}", file=sys.stderr)
        return 2

    # ------------------------------------------------------------------
    # 3. Parse and persist JSON result
    # ------------------------------------------------------------------
    audit_result = None
    try:
        audit_result = json.loads(proc.stdout)
        RESULT_FILE.write_text(json.dumps(audit_result, indent=2), encoding="utf-8")
    except json.JSONDecodeError:
        print("[hook] WARNING: Could not parse audit JSON output.", file=sys.stderr)
        print(proc.stdout[:500], file=sys.stderr)

    # ------------------------------------------------------------------
    # 4. Human-readable summary
    # ------------------------------------------------------------------
    if audit_result:
        total   = audit_result.get("total", len(epic_ids))
        passed  = audit_result.get("passed", 0)
        failed  = audit_result.get("failed", 0)
        failed_epics = audit_result.get("failed_epics", [])

        print(f"\n[hook] Audit result: {passed}/{total} PASS, {failed} FAIL")
        if failed_epics:
            print(f"[hook] Failed epics that need redo:")
            for eid in failed_epics:
                # Find the first failure reason for quick triage
                for r in audit_result.get("results", []):
                    if r["epic_id"] == eid and r["failures"]:
                        print(f"  ❌ {eid}: {r['failures'][0]}")
                        break
            print(f"[hook] Redo list written to: {REDO_FILE}")
        else:
            print(f"[hook] ✅ All {total} epics in batch PASSED Phase {phase} audit.")
    else:
        # Script ran but no JSON — check return code
        if proc.returncode == 0:
            print("[hook] Script exited 0 (pass) but no JSON. Treating as PASS.")
        else:
            print(f"[hook] Script stderr:\n{proc.stderr[:300]}", file=sys.stderr)

    # ------------------------------------------------------------------
    # 5. Log to Lamport event log
    # ------------------------------------------------------------------
    if audit_result:
        status    = "pass" if audit_result["status"] == "ALL_PASS" else "fail"
        fail_list = audit_result.get("failed_epics", [])
        log_lamport(
            event_type  = "batch_audit_complete",
            status      = status,
            phase       = phase,
            note        = (
                f"Phase {phase} batch audit: {audit_result['passed']}/{audit_result['total']} pass. "
                + (f"Redo required: {fail_list}" if fail_list else "All pass.")
            ),
            extras = {
                "passed":       audit_result.get("passed", 0),
                "failed":       audit_result.get("failed", 0),
                "failed_epics": fail_list,
                "batch_size":   len(epic_ids),
            }
        )

    # ------------------------------------------------------------------
    # 6. Exit code drives orchestrator behaviour
    # ------------------------------------------------------------------
    return proc.returncode


if __name__ == "__main__":
    sys.exit(main())
