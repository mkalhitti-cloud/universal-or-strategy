#!/usr/bin/env python3
"""
PTT Lamport Clock — Deterministic Phase Tracking for /nt-builder Workflow
V1.0 (V12.36)

Wraps DeterministicWorkflow from scripts/lamport_clock.py with PTT-specific
phase names, dependency graph, and manifest sync.

PTT Phase Dependency Graph:
  architect (1)
      --> plan_review (2)
            --> tickets (3)
                  --> ticket_review (3.5)
                        --> engineer_T1 (4a.1)
                              --> verifier_T1 (4b.1)
                        --> engineer_T2 (4a.2) [after T1 verify_pass]
                              --> verifier_T2 (4b.2)
                        --> engineer_T3 (4a.3) [after T2 verify_pass]
                              --> verifier_T3 (4b.3)
                                    --> final_review (5)

Usage:
  # Record phase start:
  python scripts/ptt_lamport.py start PTT-COPIER-B9 architect

  # Record phase complete (with result token):
  python scripts/ptt_lamport.py complete PTT-COPIER-B9 architect PLAN_COMPLETE

  # Record phase failed:
  python scripts/ptt_lamport.py fail PTT-COPIER-B9 architect "Spec missing REQ-007"

  # Check if next phase is allowed to start:
  python scripts/ptt_lamport.py gate PTT-COPIER-B9 plan_review

  # Show full event log for an epic:
  python scripts/ptt_lamport.py log PTT-COPIER-B9

  # Show current pipeline status:
  python scripts/ptt_lamport.py status PTT-COPIER-B9

Exit codes (gate command):
  0 = GATE OPEN  — prerequisite phases are complete, proceed
  1 = GATE CLOSED — prerequisite not met, print reason, stop
  2 = USAGE ERROR
"""

import json
import sys
import os
import hashlib
from datetime import datetime, timezone
from pathlib import Path

# ---------------------------------------------------------------------------
# PTT Phase Definitions
# ---------------------------------------------------------------------------

PTT_PHASES = {
    "architect":        {"label": "Phase 1 — Architect",          "output": "02-architecture-plan.md"},
    "plan_review":      {"label": "Phase 2 — Plan Reviewer",       "output": "02-plan-review.md"},
    "tickets":          {"label": "Phase 3 — Ticket Generation",   "output": "04-tickets.md"},
    "ticket_review":    {"label": "Phase 3.5 — Ticket Reviewer",   "output": "04-ticket-review.md"},
    "engineer_T1":      {"label": "Phase 4a T1 — Engineer",        "output": "ticket-1-completion.md"},
    "verifier_T1":      {"label": "Phase 4b T1 — Verifier",        "output": "ticket-1-verification.md"},
    "engineer_T2":      {"label": "Phase 4a T2 — Engineer",        "output": "ticket-2-completion.md"},
    "verifier_T2":      {"label": "Phase 4b T2 — Verifier",        "output": "ticket-2-verification.md"},
    "engineer_T3":      {"label": "Phase 4a T3 — Engineer",        "output": "ticket-3-completion.md"},
    "verifier_T3":      {"label": "Phase 4b T3 — Verifier",        "output": "ticket-3-verification.md"},
    "final_review":     {"label": "Phase 5 — Final Review",        "output": "05-final-review.md"},
}

# Dependency graph: phase -> list of phases that MUST be complete first
PTT_DEPENDENCIES = {
    "architect":     [],
    "plan_review":   ["architect"],
    "tickets":       ["plan_review"],
    "ticket_review": ["tickets"],
    "engineer_T1":   ["ticket_review"],
    "verifier_T1":   ["engineer_T1"],
    "engineer_T2":   ["verifier_T1"],
    "verifier_T2":   ["engineer_T2"],
    "engineer_T3":   ["verifier_T2"],
    "verifier_T3":   ["engineer_T3"],
    "final_review":  ["verifier_T1", "verifier_T2", "verifier_T3"],
}

# RESULT TOKENS: which result token marks a phase as successfully complete
PASS_TOKENS = {
    "architect":     "PLAN_COMPLETE",
    "plan_review":   "REVIEW_PASS",
    "tickets":       "TICKETS_COMPLETE",
    "ticket_review": "TICKET_REVIEW_PASS",
    "engineer_T1":   "BUILD_PASS",
    "verifier_T1":   "VERIFY_PASS",
    "engineer_T2":   "BUILD_PASS",
    "verifier_T2":   "VERIFY_PASS",
    "engineer_T3":   "BUILD_PASS",
    "verifier_T3":   "VERIFY_PASS",
    "final_review":  "FINAL_PASS",
}

# ---------------------------------------------------------------------------
# Event log helpers
# ---------------------------------------------------------------------------

LAMPORT_DIR = Path(".lamport/ptt")


def _log_file(epic_id: str) -> Path:
    d = LAMPORT_DIR / epic_id
    d.mkdir(parents=True, exist_ok=True)
    return d / "event_log.jsonl"


def _clock_file(epic_id: str) -> Path:
    return LAMPORT_DIR / epic_id / "global_clock.json"


def _load_clock(epic_id: str) -> int:
    cf = _clock_file(epic_id)
    if cf.exists():
        return json.loads(cf.read_text(encoding="utf-8")).get("clock", 0)
    return 0


def _save_clock(epic_id: str, value: int):
    cf = _clock_file(epic_id)
    cf.write_text(
        json.dumps({"clock": value, "updated_at": datetime.now(timezone.utc).isoformat()}, indent=2),
        encoding="utf-8"
    )


def _tick(epic_id: str) -> int:
    c = _load_clock(epic_id) + 1
    _save_clock(epic_id, c)
    return c


def _append_event(epic_id: str, event: dict):
    lf = _log_file(epic_id)
    with open(lf, "a", encoding="utf-8") as f:
        f.write(json.dumps(event) + "\n")


def _load_events(epic_id: str) -> list:
    lf = _log_file(epic_id)
    if not lf.exists():
        return []
    events = []
    for line in lf.read_text(encoding="utf-8").splitlines():
        line = line.strip()
        if line:
            events.append(json.loads(line))
    return sorted(events, key=lambda e: e.get("clock", 0))


def _state_hash(epic_id: str, phase: str) -> str:
    """SHA256 of brain dir artifacts for this epic."""
    parts = []
    brain = Path(f"docs/brain/{epic_id}")
    if brain.exists():
        for f in sorted(brain.glob("*.md")) + sorted(brain.glob("*.json")):
            try:
                parts.append(f"{f.name}:{f.read_text(encoding='utf-8', errors='replace')}")
            except OSError:
                pass
    parts.append(f"phase:{phase}")
    return hashlib.sha256("\n".join(parts).encode()).hexdigest()[:16]


# ---------------------------------------------------------------------------
# Core commands
# ---------------------------------------------------------------------------

def cmd_start(epic_id: str, phase: str):
    """Record phase_start event."""
    _assert_phase(phase)
    clock = _tick(epic_id)
    event = {
        "clock": clock,
        "event_type": "phase_start",
        "epic_id": epic_id,
        "phase": phase,
        "status": "running",
        "state_hash": _state_hash(epic_id, phase),
        "timestamp": datetime.now(timezone.utc).isoformat(),
    }
    _append_event(epic_id, event)
    _sync_manifest(epic_id, phase, "running", clock)
    print(f"[ptt-lamport] clock={clock} phase_start {phase} for {epic_id}")


def cmd_complete(epic_id: str, phase: str, result_token: str):
    """Record phase_complete event. Verifies result_token matches expected PASS token."""
    _assert_phase(phase)
    expected = PASS_TOKENS[phase]
    if result_token != expected:
        print(f"[ptt-lamport] WARN: result_token '{result_token}' != expected '{expected}' for {phase}")
        # Not a hard error — Orchestrator may use variants; we log what was given.
    clock = _tick(epic_id)
    event = {
        "clock": clock,
        "event_type": "phase_complete",
        "epic_id": epic_id,
        "phase": phase,
        "status": "completed",
        "result": result_token,
        "state_hash": _state_hash(epic_id, phase),
        "timestamp": datetime.now(timezone.utc).isoformat(),
    }
    _append_event(epic_id, event)
    _sync_manifest(epic_id, phase, "completed", clock, result_token)
    print(f"[ptt-lamport] clock={clock} phase_complete {phase} result={result_token} for {epic_id}")


def cmd_fail(epic_id: str, phase: str, reason: str):
    """Record phase_fail event."""
    _assert_phase(phase)
    clock = _tick(epic_id)
    event = {
        "clock": clock,
        "event_type": "phase_fail",
        "epic_id": epic_id,
        "phase": phase,
        "status": "failed",
        "reason": reason,
        "state_hash": _state_hash(epic_id, phase),
        "timestamp": datetime.now(timezone.utc).isoformat(),
    }
    _append_event(epic_id, event)
    _sync_manifest(epic_id, phase, "failed", clock, reason=reason)
    print(f"[ptt-lamport] clock={clock} phase_fail {phase} reason='{reason}' for {epic_id}")


def cmd_gate(epic_id: str, phase: str) -> int:
    """
    Gate check: exits 0 if all prerequisites are complete, exits 1 if not.
    Used by hooks and orchestrators before spawning a subtask.
    """
    _assert_phase(phase)
    deps = PTT_DEPENDENCIES[phase]
    if not deps:
        print(f"[ptt-lamport] GATE OPEN: {phase} has no dependencies")
        return 0

    events = _load_events(epic_id)
    completed_phases = {
        e["phase"]
        for e in events
        if e.get("event_type") == "phase_complete" and e.get("status") == "completed"
    }

    missing = [d for d in deps if d not in completed_phases]
    if missing:
        print(f"[ptt-lamport] GATE CLOSED: {phase} requires {missing} — not complete for {epic_id}")
        return 1

    clock = _load_clock(epic_id)
    print(f"[ptt-lamport] GATE OPEN: {phase} prerequisites satisfied (clock={clock}) for {epic_id}")
    return 0


def cmd_log(epic_id: str):
    """Print full event log for an epic."""
    events = _load_events(epic_id)
    if not events:
        print(f"[ptt-lamport] No events for {epic_id}")
        return
    print(f"[ptt-lamport] Event log for {epic_id} ({len(events)} events):")
    for e in events:
        ts = e.get("timestamp", "?")[:19]
        clock = e.get("clock", "?")
        etype = e.get("event_type", "?")
        phase = e.get("phase", "?")
        status = e.get("status", "?")
        result = e.get("result", e.get("reason", ""))
        suffix = f" [{result}]" if result else ""
        print(f"  T={clock:>4}  {ts}  {etype:<20}  {phase:<15}  {status}{suffix}")


def cmd_status(epic_id: str):
    """Print current pipeline status — which phases done, which pending, what's next."""
    events = _load_events(epic_id)
    completed = {
        e["phase"]: e
        for e in events
        if e.get("event_type") == "phase_complete" and e.get("status") == "completed"
    }
    failed = {
        e["phase"]: e
        for e in events
        if e.get("event_type") == "phase_fail"
    }
    running = {
        e["phase"]: e
        for e in events
        if e.get("status") == "running"
    }

    print(f"\n[ptt-lamport] Pipeline status: {epic_id}")
    print(f"  Clock: {_load_clock(epic_id)}")
    print()
    for phase, info in PTT_PHASES.items():
        label = info["label"]
        if phase in completed:
            result = completed[phase].get("result", "")
            ts = completed[phase].get("timestamp", "")[:10]
            print(f"  [DONE]    {label:<45}  {result} ({ts})")
        elif phase in failed:
            reason = failed[phase].get("reason", "")[:50]
            print(f"  [FAIL]    {label:<45}  {reason}")
        elif phase in running:
            print(f"  [RUN]     {label}")
        else:
            deps = PTT_DEPENDENCIES[phase]
            missing = [d for d in deps if d not in completed]
            if missing:
                print(f"  [BLOCKED] {label:<45}  waiting on: {', '.join(missing)}")
            else:
                print(f"  [READY]   {label}")
    print()

    # Show next runnable phases
    next_phases = [
        p for p, deps in PTT_DEPENDENCIES.items()
        if p not in completed and p not in running and p not in failed
        and all(d in completed for d in deps)
    ]
    if next_phases:
        print(f"  Next runnable: {', '.join(next_phases)}")
    else:
        if all(p in completed for p in PTT_PHASES):
            print(f"  PIPELINE_COMPLETE")
        else:
            print(f"  No runnable phases — check for failures above")


# ---------------------------------------------------------------------------
# Manifest sync
# ---------------------------------------------------------------------------

def _sync_manifest(epic_id: str, phase: str, status: str, clock: int, result: str = "", reason: str = ""):
    """Keep manifest.json in sync with Lamport events."""
    manifest_path = Path(f"docs/brain/{epic_id}/manifest.json")
    if not manifest_path.exists():
        return
    try:
        manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    except Exception:
        return

    # Ensure lamport_events array exists in manifest
    if "lamport_events" not in manifest:
        manifest["lamport_events"] = []

    entry = {
        "clock": clock,
        "phase": phase,
        "event_type": f"phase_{status}" if status in ("running", "failed") else "phase_complete",
        "status": status,
        "timestamp": datetime.now(timezone.utc).isoformat(),
    }
    if result:
        entry["result"] = result
    if reason:
        entry["reason"] = reason

    manifest["lamport_events"].append(entry)

    # Also update top-level phase field for backward compat
    manifest["phase"] = phase if status == "running" else manifest.get("phase", phase)

    try:
        manifest_path.write_text(
            json.dumps(manifest, indent=2) + "\n",
            encoding="utf-8"
        )
    except Exception as e:
        print(f"[ptt-lamport] WARN: could not sync manifest: {e}", file=sys.stderr)


# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

def _assert_phase(phase: str):
    if phase not in PTT_PHASES:
        print(f"[ptt-lamport] ERROR: unknown phase '{phase}'. Valid: {', '.join(PTT_PHASES)}", file=sys.stderr)
        sys.exit(2)


# ---------------------------------------------------------------------------
# CLI entry point
# ---------------------------------------------------------------------------

USAGE = """
Usage:
  python scripts/ptt_lamport.py start    <epic_id> <phase>
  python scripts/ptt_lamport.py complete <epic_id> <phase> <result_token>
  python scripts/ptt_lamport.py fail     <epic_id> <phase> "<reason>"
  python scripts/ptt_lamport.py gate     <epic_id> <phase>
  python scripts/ptt_lamport.py log      <epic_id>
  python scripts/ptt_lamport.py status   <epic_id>

Phases: architect | plan_review | tickets | ticket_review |
        engineer_T1 | verifier_T1 | engineer_T2 | verifier_T2 |
        engineer_T3 | verifier_T3 | final_review

Gate exit codes: 0=OPEN (proceed), 1=CLOSED (blocked), 2=usage error
"""


def main():
    args = sys.argv[1:]
    if not args:
        print(USAGE)
        sys.exit(2)

    cmd = args[0]

    if cmd == "start" and len(args) == 3:
        cmd_start(args[1], args[2])
    elif cmd == "complete" and len(args) == 4:
        cmd_complete(args[1], args[2], args[3])
    elif cmd == "fail" and len(args) >= 4:
        cmd_fail(args[1], args[2], " ".join(args[3:]))
    elif cmd == "gate" and len(args) == 3:
        sys.exit(cmd_gate(args[1], args[2]))
    elif cmd == "log" and len(args) == 2:
        cmd_log(args[1])
    elif cmd == "status" and len(args) == 2:
        cmd_status(args[1])
    else:
        print(USAGE)
        sys.exit(2)


if __name__ == "__main__":
    main()
