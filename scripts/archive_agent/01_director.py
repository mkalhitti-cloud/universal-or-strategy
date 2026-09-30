"""
ARCHIVE PIPELINE — TIER 1: MEDIA ARCHITECT DIRECTOR
====================================================
Operated by: Bob (Media Architect) — account_01

Responsibilities:
  - Reads archive_manifest.json
  - Writes worker_assignments/ for each Tier 2 orchestrator
  - Monitors pipeline progress via git status
  - Reports overall completion status
  - Re-assigns failed batches

Usage:
    python scripts/archive_agent/01_director.py --assign
    python scripts/archive_agent/01_director.py --status
    python scripts/archive_agent/01_director.py --reassign-failed
"""

import json
import argparse
from pathlib import Path
from datetime import datetime
from collections import defaultdict

MANIFEST_PATH = Path("archive/archive_manifest.json")
ASSIGNMENTS_DIR = Path("worker_assignments")
ASSIGNMENTS_DIR.mkdir(exist_ok=True)


def load_manifest() -> dict:
    with open(MANIFEST_PATH, encoding="utf-8") as f:
        return json.load(f)


def save_manifest(manifest: dict):
    manifest["updated_at"] = datetime.utcnow().isoformat()
    with open(MANIFEST_PATH, "w", encoding="utf-8") as f:
        json.dump(manifest, f, indent=2)


def assign_workers(manifest: dict):
    """Tier 1 → Write assignment files for all Tier 2 orchestrators."""
    # Group sessions by batch
    batches = defaultdict(list)
    for session in manifest["sessions"]:
        batches[session["batch"]].append(session)

    print(f"\nMedia Architect assigning {len(batches)} batches to Tier 2 orchestrators...\n")

    for batch_id, sessions in sorted(batches.items()):
        account = sessions[0]["orchestrator_account"]
        p1 = sum(1 for s in sessions if s["priority"] == 1)
        p2 = sum(1 for s in sessions if s["priority"] == 2)
        p3 = sum(1 for s in sessions if s["priority"] == 3)

        assignment_path = ASSIGNMENTS_DIR / f"{account}.md"
        content = f"""# Worker Assignment — {account}
## Role: Tier 2 Pipeline Orchestrator
## Batch: {batch_id}
## Assigned by: Media Architect (account_01)
## Assigned at: TIMESTAMP

---

## Your Responsibility
You are a **Tier 2 Pipeline Orchestrator**. You manage the full archive
processing pipeline for your assigned batch of {len(sessions)} sessions.

You do NOT do the work yourself. You assign Tier 3 workers and monitor their output.

---

## Your Batch Sessions ({len(sessions)} total)

| Priority | Count | Focus |
|----------|-------|-------|
| P1 (Crown Jewel) | {p1} | Peter Tuchman Q&As |
| P2 (High Value) | {p2} | Psychology sessions |
| P3 (High Demand) | {p3} | Apex/Prop firm sessions |
| P4-P6 | {len(sessions) - p1 - p2 - p3} | Other sessions |

### Session List
"""
        for s in sessions:
            content += f"- `[{s['id']}]` P{s['priority']} — {s['title']}\n"

        content += f"""
---

## Pipeline Stages You Orchestrate

```
Stage 1: DOWNLOAD   → Tier 3 workers: accounts 21-30
Stage 2: TRANSCRIBE → Tier 3 workers: accounts 31-45
Stage 3: ANALYZE    → Tier 3 workers: accounts 46-60
Stage 4: EXTRACT    → Tier 3 workers: accounts 61-75
Stage 5: METADATA   → Tier 3 workers: accounts 76-90
```

---

## Your 4-Step Protocol

### Step 1 — git pull
```powershell
git pull origin main
```

### Step 2 — Assign your Tier 3 workers
For each session in your batch, write to `worker_assignments/`:
- `account_2X_download_{batch_id}.md` for download workers
- `account_3X_transcribe_{batch_id}.md` for transcribe workers
- etc.

Use: `python scripts/archive_agent/01_director.py --assign-tier3 {batch_id}`

### Step 3 — Monitor until all stages complete
Check status: `python scripts/archive_agent/01_director.py --batch-status {batch_id}`

### Step 4 — Commit and report
```powershell
git add archive/ worker_assignments/
git commit -m "feat(archive): batch {batch_id} complete — {{N}} sessions processed"
git push
```

---

## Input / Output Paths

| Stage | Input | Output |
|-------|-------|--------|
| Download | URL or local path | `archive/raw/{{session_id}}.mp4` |
| Transcribe | `archive/raw/{{session_id}}.mp4` | `archive/transcripts/{{session_id}}.json` |
| Analyze | `archive/transcripts/{{session_id}}.json` | `archive/transcripts/{{session_id}}_clips.json` |
| Extract | `archive/raw/{{session_id}}.mp4` + clips.json | `archive/clips/shorts/` `archive/clips/medium/` |
| Metadata | clips + transcript | `archive/metadata/{{session_id}}_metadata.json` |

---

## Success Criteria
- [ ] All {len(sessions)} sessions in batch reach status `complete`
- [ ] All clips extracted and named correctly
- [ ] All metadata files written
- [ ] No sessions in status `failed`
- [ ] git push with completion commit done
"""
        assignment_path.write_text(content, encoding="utf-8")
        print(f"  [OK] {account}.md written -- {len(sessions)} sessions, batch {batch_id}")

    print(f"\nAll {len(batches)} assignment files written to worker_assignments/")


def print_status(manifest: dict):
    """Print pipeline progress dashboard."""
    sessions = manifest["sessions"]
    total = len(sessions)
    status_counts = defaultdict(int)
    for s in sessions:
        status_counts[s["status"]] += 1

    print("\n" + "="*55)
    print("  MEDIA ARCHITECT — PIPELINE STATUS DASHBOARD")
    print("="*55)
    print(f"  Total sessions:  {total}")
    print(f"  Last updated:    {manifest.get('updated_at', manifest['created_at'])}")
    print()

    statuses = ["pending", "downloading", "transcribing",
                "analyzing", "extracting", "metadata", "complete", "failed"]
    bars = {
        "pending": "[ ]", "downloading": "[~]", "transcribing": "[~]",
        "analyzing": "[~]", "extracting": "[~]", "metadata": "[~]",
        "complete": "[x]", "failed": "[!]"
    }
    for status in statuses:
        count = status_counts.get(status, 0)
        pct = (count / total * 100) if total > 0 else 0
        bar = bars.get(status, "⬜")
        print(f"  {bar} {status:<14} {count:>4} ({pct:>5.1f}%)")

    complete = status_counts.get("complete", 0)
    print(f"\n  Overall progress: {complete}/{total} ({complete/total*100:.1f}%)")

    failed = status_counts.get("failed", 0)
    if failed > 0:
        print(f"\n  ⚠️  {failed} failed sessions — run --reassign-failed")

    print("="*55 + "\n")


def reassign_failed(manifest: dict):
    """Reset failed sessions back to pending for retry."""
    failed = [s for s in manifest["sessions"] if s["status"] == "failed"]
    if not failed:
        print("No failed sessions found.")
        return
    for s in failed:
        s["status"] = "pending"
        s["updated_at"] = datetime.utcnow().isoformat()
    save_manifest(manifest)
    print(f"Reset {len(failed)} failed sessions to pending.")


def main():
    parser = argparse.ArgumentParser(description="Media Architect — Tier 1 Director")
    parser.add_argument("--assign", action="store_true",
                        help="Write Tier 2 assignment files")
    parser.add_argument("--status", action="store_true",
                        help="Print pipeline status dashboard")
    parser.add_argument("--reassign-failed", action="store_true",
                        help="Reset failed sessions to pending")
    args = parser.parse_args()

    manifest = load_manifest()

    if args.assign:
        assign_workers(manifest)
    elif args.status:
        print_status(manifest)
    elif args.reassign_failed:
        reassign_failed(manifest)
    else:
        print_status(manifest)


if __name__ == "__main__":
    main()
