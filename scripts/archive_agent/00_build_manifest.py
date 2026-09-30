"""
ARCHIVE PIPELINE — MANIFEST BUILDER
====================================
Run once by Media Architect (Tier 1) to build archive_manifest.json
from docs/sample videos archive.md

Priority system:
  1 = Peter Tuchman Q&As (crown jewel)
  2 = Psychology / losing day sessions
  3 = Apex Trader Funding sessions
  4 = NinjaTrader sessions
  5 = Strategy sessions (FFMA, RMA, Trend, ORB)
  6 = All other sessions

Usage:
    python scripts/archive_agent/00_build_manifest.py
"""

import json
import re
from pathlib import Path
from datetime import datetime

ARCHIVE_MD = Path("docs/sample videos archive.md")
MANIFEST_OUT = Path("archive/archive_manifest.json")

PRIORITY_RULES = [
    (1, ["peter tuchman", "q&a with peter", "peter's market"]),
    (2, ["psychology", "losing day", "losing trade", "blew", "blow", "fear", "fomo",
         "discipline", "bad habits", "urge to close", "huge loss", "losing trades"]),
    (3, ["apex", "topstep", "ftmo", "prop", "funded", "funding"]),
    (4, ["ninjatrader", "ninja trader", "atm strategy"]),
    (5, ["ffma", "far from moving", "rma", "regular moving", "trend trade",
         "base trade", "orb", "opening range"]),
]

def get_priority(title: str) -> int:
    t = title.lower()
    for priority, keywords in PRIORITY_RULES:
        if any(k in t for k in keywords):
            return priority
    return 6

def parse_archive(md_path: Path) -> list[dict]:
    sessions = []
    session_id = 1
    date_pattern = re.compile(
        r"(\d{1,2}[/-]\d{1,2}[/-]\d{2,4}|\d{2}/\d{2}/\d{4})"
    )

    with open(md_path, encoding="utf-8") as f:
        for line in f:
            line = line.strip()
            if not line:
                continue
            # Skip section headers and nav elements
            if line in ["Previous Lecture", "Complete and Continue",
                        "Essential Lessons", "Product"]:
                continue
            if line.startswith("👨") or line.startswith("🆕") or line.startswith("$"):
                continue
            if "%" in line and "COMPLETE" in line:
                continue
            if "Mentorship Class Every" in line or "Zoom Link" in line:
                continue
            if line in ["July 2026", "June 2026", "May 2026", "April 2026",
                        "March 2026", "February 2026", "January 2026",
                        "December 2025", "November 2025", "October 2025",
                        "September 2025", "August 2025", "July 2025",
                        "June 2025", "May 2025", "April 2025", "March 2025",
                        "February 2025", "January 2025"] or re.match(r"^\w+ 202\d$", line) or re.match(r"^\w+ 202\d$", line):
                continue

            # Detect if line looks like a session (has a date or known title)
            has_date = bool(date_pattern.search(line))
            is_lesson = any(k in line for k in [
                "Fibonacci", "Bracket Orders", "Watchlist", "ThinkorSwim",
                "Options Lesson", "Boot Camp", "NinjaTrader Setup",
                "Futures FastTrack", "Introduction to Trading", "Master Class",
                "Truth About Day Trading", "Registration"
            ])

            if has_date or is_lesson:
                priority = get_priority(line)
                # Determine source
                source = "teachable"
                sessions.append({
                    "id": f"session_{session_id:03d}",
                    "title": line,
                    "source": source,
                    "url": None,
                    "local_path": None,
                    "priority": priority,
                    "status": "pending",
                    "batch": None,
                    "orchestrator_account": None,
                    "raw_video_path": None,
                    "transcript_path": None,
                    "analysis_path": None,
                    "clips": [],
                    "metadata_path": None,
                    "created_at": datetime.utcnow().isoformat(),
                    "updated_at": datetime.utcnow().isoformat(),
                })
                session_id += 1

    return sessions

def assign_batches(sessions: list[dict], batch_size: int = 12) -> list[dict]:
    """Assign sessions to Tier 2 orchestrator accounts (02-20 = 19 accounts)."""
    # Sort by priority first
    sessions.sort(key=lambda s: s["priority"])
    for i, session in enumerate(sessions):
        batch_num = (i // batch_size) + 2  # accounts start at 02
        batch_num = min(batch_num, 20)     # cap at account 20
        session["batch"] = f"batch_{batch_num:02d}"
        session["orchestrator_account"] = f"account_{batch_num:02d}"
    return sessions

def main():
    print("Building archive manifest...")
    sessions = parse_archive(ARCHIVE_MD)
    sessions = assign_batches(sessions)

    manifest = {
        "version": "1.0",
        "created_at": datetime.utcnow().isoformat(),
        "media_architect": "account_01",
        "total_sessions": len(sessions),
        "status_summary": {
            "pending": len(sessions),
            "downloading": 0,
            "transcribing": 0,
            "analyzing": 0,
            "extracting": 0,
            "metadata": 0,
            "complete": 0,
            "failed": 0,
        },
        "priority_summary": {
            str(p): sum(1 for s in sessions if s["priority"] == p)
            for p in range(1, 7)
        },
        "sessions": sessions,
    }

    MANIFEST_OUT.parent.mkdir(parents=True, exist_ok=True)
    with open(MANIFEST_OUT, "w", encoding="utf-8") as f:
        json.dump(manifest, f, indent=2)

    print(f"Manifest written: {MANIFEST_OUT}")
    print(f"Total sessions: {len(sessions)}")
    print("Priority breakdown:")
    for p, count in manifest["priority_summary"].items():
        labels = {
            "1": "Peter Tuchman Q&As",
            "2": "Psychology/Losing sessions",
            "3": "Apex/Prop firm sessions",
            "4": "NinjaTrader sessions",
            "5": "Strategy sessions (FFMA/RMA/Trend/ORB)",
            "6": "All other sessions",
        }
        print(f"  P{p} ({labels[p]}): {count}")

if __name__ == "__main__":
    main()
