#!/usr/bin/env python3
"""
Pre-Task Graphify Staleness Hook

Runs graphify update ONLY when the graph is stale (git HEAD SHA differs from
the SHA stored in .graphify/graph.json). No-ops in ~0.1s when graph is fresh.

Registered in .bob/hooks.json under "pre_task_graphify".
Triggered before every task starts.

Exit codes:
  0 - graph was fresh (no-op) or was stale and update succeeded
  1 - update failed (non-blocking — task proceeds regardless)
"""

import json
import subprocess
import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent.parent
GRAPH_JSON = REPO_ROOT / ".graphify" / "graph.json"


def get_git_head_sha() -> str | None:
    """Return current git HEAD SHA."""
    try:
        result = subprocess.run(
            ["git", "rev-parse", "HEAD"],
            capture_output=True, text=True, cwd=str(REPO_ROOT), timeout=5
        )
        if result.returncode == 0:
            return result.stdout.strip()
    except Exception:
        pass
    return None


def get_graph_sha() -> str | None:
    """Return the git SHA stored in .graphify/graph.json."""
    try:
        if not GRAPH_JSON.exists():
            return None
        with open(GRAPH_JSON, "r", encoding="utf-8") as f:
            data = json.load(f)
        # graphify stores the SHA under metadata.git_sha or commit_sha
        meta = data.get("metadata", data)
        return (
            meta.get("git_sha")
            or meta.get("commit_sha")
            or meta.get("head_sha")
        )
    except Exception:
        return None


def run_graphify_update() -> bool:
    """Run graphify update --no-cluster --no-description. Returns True on success."""
    try:
        result = subprocess.run(
            ["graphify", "update", ".", "--no-cluster", "--no-description"],
            cwd=str(REPO_ROOT), timeout=120
        )
        return result.returncode == 0
    except FileNotFoundError:
        print("[graphify-hook] graphify not found in PATH — skipping", file=sys.stderr)
        return False
    except subprocess.TimeoutExpired:
        print("[graphify-hook] graphify update timed out after 120s", file=sys.stderr)
        return False
    except Exception as e:
        print(f"[graphify-hook] unexpected error: {e}", file=sys.stderr)
        return False


def main() -> int:
    head_sha = get_git_head_sha()
    graph_sha = get_graph_sha()

    if head_sha is None:
        # Not a git repo or git unavailable — skip silently
        return 0

    if graph_sha is None:
        # No graph yet — build it
        print("[graphify-hook] No graph found — building initial graph...")
        run_graphify_update()
        return 0

    if head_sha == graph_sha:
        # Fresh — no-op
        return 0

    # Stale — update
    print(f"[graphify-hook] Graph stale (graph={graph_sha[:8]} HEAD={head_sha[:8]}) — updating...")
    success = run_graphify_update()
    if success:
        print("[graphify-hook] Graph updated.")
    else:
        print("[graphify-hook] Update failed — proceeding with stale graph.", file=sys.stderr)

    return 0  # always exit 0 — never block a task due to graphify


if __name__ == "__main__":
    sys.exit(main())
