"""
ARCHIVE PIPELINE — TIER 2: PIPELINE ORCHESTRATOR
==================================================
Operated by: Bob accounts 02-20 (one per batch)

Responsibilities:
  - Reads its own worker_assignments/account_XX.md
  - Assigns Tier 3 stage workers for each session in its batch
  - Monitors stage completion
  - Updates archive_manifest.json with progress
  - Reports completion to Tier 1

Usage:
    python scripts/archive_agent/02_pipeline_orchestrator.py --account account_02
    python scripts/archive_agent/02_pipeline_orchestrator.py --account account_02 --status
    python scripts/archive_agent/02_pipeline_orchestrator.py --account account_02 --assign-tier3
"""

import json
import argparse
from pathlib import Path
from datetime import datetime

MANIFEST_PATH = Path("archive/archive_manifest.json")
ASSIGNMENTS_DIR = Path("worker_assignments")

TIER3_STAGE_ACCOUNTS = {
    "download":   list(range(21, 31)),   # accounts 21-30
    "transcribe": list(range(31, 46)),   # accounts 31-45
    "analyze":    list(range(46, 61)),   # accounts 46-60
    "extract":    list(range(61, 76)),   # accounts 61-75
    "metadata":   list(range(76, 91)),   # accounts 76-90
}

STAGE_SCRIPTS = {
    "download":   "scripts/archive_agent/03_download_worker.py",
    "transcribe": "scripts/archive_agent/04_transcribe_worker.py",
    "analyze":    "scripts/archive_agent/05_analyze_worker.py",
    "extract":    "scripts/archive_agent/06_extract_worker.py",
    "metadata":   "scripts/archive_agent/07_metadata_worker.py",
}


def load_manifest() -> dict:
    with open(MANIFEST_PATH, encoding="utf-8") as f:
        return json.load(f)


def save_manifest(manifest: dict):
    manifest["updated_at"] = datetime.utcnow().isoformat()
    with open(MANIFEST_PATH, "w", encoding="utf-8") as f:
        json.dump(manifest, f, indent=2)


def get_my_sessions(manifest: dict, account: str) -> list[dict]:
    return [s for s in manifest["sessions"]
            if s.get("orchestrator_account") == account]


def assign_tier3_workers(account: str, sessions: list[dict]):
    """
    Tier 2 writes assignment files for each Tier 3 stage worker.
    Each worker gets one session and one stage.
    """
    print(f"\n{account} — Assigning Tier 3 workers for {len(sessions)} sessions...\n")

    stage_account_idx = {stage: 0 for stage in TIER3_STAGE_ACCOUNTS}

    for session in sessions:
        sid = session["id"]
        title_short = session["title"][:60]

        for stage in ["download", "transcribe", "analyze", "extract", "metadata"]:
            accounts = TIER3_STAGE_ACCOUNTS[stage]
            idx = stage_account_idx[stage] % len(accounts)
            worker_account = f"account_{accounts[idx]:02d}"
            stage_account_idx[stage] += 1

            assignment_file = ASSIGNMENTS_DIR / f"{worker_account}_{stage}_{sid}.md"

            # Determine input/output paths per stage
            input_path, output_path = _get_stage_paths(stage, sid)

            content = f"""# Worker Assignment — {worker_account}
## Role: Tier 3 Stage Worker — {stage.upper()}
## Session: {sid}
## Assigned by: {account} (Tier 2 Orchestrator)
## Assigned at: {datetime.utcnow().isoformat()}

---

## Session Details
- **ID**: {sid}
- **Title**: {title_short}
- **Priority**: P{session['priority']}
- **Source**: {session['source']}
- **URL**: {session.get('url', 'TBD — set before running')}

---

## Your Single Task: {stage.upper()}

### Input
```
{input_path}
```

### Output
```
{output_path}
```

### Command to Run
```powershell
python {STAGE_SCRIPTS[stage]} --session {sid}
```

---

## Your 4-Step Protocol

### Step 1 — git pull
```powershell
git pull origin main
```

### Step 2 — Execute your stage
```powershell
python {STAGE_SCRIPTS[stage]} --session {sid}
```

### Step 3 — Verify output exists
```powershell
Test-Path "{output_path}"
```

### Step 4 — Commit and push
```powershell
git add archive/
git commit -m "feat(archive): {stage} complete for {sid}"
git push
```

---

## Success Criteria
- [ ] Output file exists at `{output_path}`
- [ ] No errors in script output
- [ ] Session status updated in manifest
- [ ] git push completed
"""
            assignment_file.write_text(content, encoding="utf-8")

        print(f"  ✅ {sid} — all 5 stage assignments written")

    print(f"\nTier 3 assignments complete for {account}")


def _get_stage_paths(stage: str, sid: str) -> tuple[str, str]:
    paths = {
        "download":   (f"URL or local path in manifest",
                       f"archive/raw/{sid}.mp4"),
        "transcribe": (f"archive/raw/{sid}.mp4",
                       f"archive/transcripts/{sid}.json"),
        "analyze":    (f"archive/transcripts/{sid}.json",
                       f"archive/transcripts/{sid}_clips.json"),
        "extract":    (f"archive/raw/{sid}.mp4 + archive/transcripts/{sid}_clips.json",
                       f"archive/clips/[shorts|medium|full]/{sid}_*.mp4"),
        "metadata":   (f"archive/transcripts/{sid}_clips.json",
                       f"archive/metadata/{sid}_metadata.json"),
    }
    return paths[stage]


def print_batch_status(account: str, sessions: list[dict]):
    total = len(sessions)
    from collections import defaultdict
    counts = defaultdict(int)
    for s in sessions:
        counts[s["status"]] += 1

    print(f"\n{'='*50}")
    print(f"  {account} — BATCH STATUS")
    print(f"{'='*50}")
    print(f"  Sessions: {total}")
    for status, count in counts.items():
        pct = count / total * 100
        print(f"  {status:<14} {count:>4} ({pct:>5.1f}%)")
    complete = counts.get("complete", 0)
    print(f"\n  Progress: {complete}/{total} ({complete/total*100:.1f}%)")
    print(f"{'='*50}\n")


def main():
    parser = argparse.ArgumentParser(description="Tier 2 Pipeline Orchestrator")
    parser.add_argument("--account", required=True,
                        help="This orchestrator's account ID (e.g. account_02)")
    parser.add_argument("--assign-tier3", action="store_true",
                        help="Write Tier 3 worker assignment files")
    parser.add_argument("--status", action="store_true",
                        help="Print batch status")
    args = parser.parse_args()

    manifest = load_manifest()
    sessions = get_my_sessions(manifest, args.account)

    if not sessions:
        print(f"No sessions assigned to {args.account}. Run director --assign first.")
        return

    if args.assign_tier3:
        assign_tier3_workers(args.account, sessions)
    elif args.status:
        print_batch_status(args.account, sessions)
    else:
        print_batch_status(args.account, sessions)


if __name__ == "__main__":
    main()
