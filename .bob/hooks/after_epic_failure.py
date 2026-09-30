#!/usr/bin/env python3
"""
After Epic Failure Hook

Automatically captures lessons when an epic fails.

Triggered when:
- /epic-run outputs [EPIC-FAILED]
- Forensic report exists in docs/brain/{epic_id}/

Usage:
    python .bob/hooks/after_epic_failure.py <epic_id> <failure_reason>
"""

import json
import sys
from pathlib import Path
from datetime import datetime, timezone
import subprocess


def extract_lesson_from_forensic_report(epic_id: str) -> dict | None:
    """
    Extract lesson from forensic report.
    
    Returns:
        {
            "title": str,
            "category": str,
            "description": str,
            "root_cause": str,
            "prevention": str,
            "confidence": float
        }
        or None if forensic report not found
    """
    forensic_path = Path(f"docs/brain/{epic_id}/FORENSIC_REPORT.md")
    
    if not forensic_path.exists():
        return None
    
    content = forensic_path.read_text(encoding="utf-8")
    
    # Extract key sections
    lesson = {
        "title": f"{epic_id} Failure Analysis",
        "category": "workflow",  # Default, can be refined
        "description": "",
        "root_cause": "",
        "prevention": "",
        "confidence": 0.9  # High confidence from forensic analysis
    }
    
    # Parse sections
    lines = content.split("\n")
    current_section = None
    
    for line in lines:
        if "## Root Cause" in line:
            current_section = "root_cause"
        elif "## Prevention" in line or "## Safeguards" in line:
            current_section = "prevention"
        elif "## Summary" in line or "## Executive Summary" in line:
            current_section = "description"
        elif line.startswith("##"):
            current_section = None
        elif current_section and line.strip():
            lesson[current_section] += line.strip() + " "
    
    # Clean up
    for key in ["description", "root_cause", "prevention"]:
        lesson[key] = lesson[key].strip()
    
    # Determine category from content
    content_lower = content.lower()
    if "stale" in content_lower or "index" in content_lower:
        lesson["category"] = "workflow"
    elif "complexity" in content_lower or "cyc" in content_lower:
        lesson["category"] = "refactoring"
    elif "lock" in content_lower or "race" in content_lower:
        lesson["category"] = "architecture"
    
    return lesson


def capture_lesson_to_firebase(lesson: dict, epic_id: str):
    """
    Capture lesson to Firebase using existing script.
    """
    # Call existing capture_lesson.py script
    result = subprocess.run(
        [
            sys.executable,
            "scripts/capture_lesson.py",
            "--title", lesson["title"],
            "--category", lesson["category"],
            "--description", lesson["description"],
            "--root-cause", lesson["root_cause"],
            "--prevention", lesson["prevention"],
            "--confidence", str(lesson["confidence"]),
            "--source", f"epic_failure:{epic_id}"
        ],
        capture_output=True,
        text=True
    )
    
    if result.returncode != 0:
        print(f"❌ Failed to capture lesson: {result.stderr}", file=sys.stderr)
        return False
    
    print(f"✅ Lesson captured to Firebase: {lesson['title']}")
    return True


def update_session_json(epic_id: str, failure_reason: str):
    """
    Update autonomous_refactor_session.json with failure.
    """
    session_path = Path("docs/brain/autonomous_refactor_session.json")
    
    if not session_path.exists():
        return
    
    with open(session_path, "r", encoding="utf-8") as f:
        session = json.load(f)
    
    # Add to failures list
    if "failures" not in session:
        session["failures"] = []
    
    session["failures"].append({
        "epic_id": epic_id,
        "reason": failure_reason,
        "timestamp": datetime.now(timezone.utc).isoformat(),
        "lesson_captured": True
    })
    
    # Update status
    session["status"] = "PAUSED_AFTER_FAILURE"
    session["current_epic"] = None
    
    with open(session_path, "w", encoding="utf-8") as f:
        json.dump(session, f, indent=2)
    
    print(f"✅ Session updated: {epic_id} marked as failed")


def main():
    if len(sys.argv) < 3:
        print("Usage: python .bob/hooks/after_epic_failure.py <epic_id> <failure_reason>")
        sys.exit(1)
    
    epic_id = sys.argv[1]
    failure_reason = sys.argv[2]
    
    print(f"\n🔍 Processing epic failure: {epic_id}")
    print(f"   Reason: {failure_reason}")
    
    # Extract lesson from forensic report
    lesson = extract_lesson_from_forensic_report(epic_id)
    
    if not lesson:
        print(f"⚠️  No forensic report found for {epic_id}")
        print(f"   Expected: docs/brain/{epic_id}/FORENSIC_REPORT.md")
        sys.exit(1)
    
    print(f"\n📝 Lesson extracted:")
    print(f"   Title: {lesson['title']}")
    print(f"   Category: {lesson['category']}")
    print(f"   Confidence: {lesson['confidence']}")
    
    # Capture to Firebase
    success = capture_lesson_to_firebase(lesson, epic_id)
    
    if not success:
        sys.exit(1)
    
    # Update session JSON
    update_session_json(epic_id, failure_reason)
    
    print(f"\n✅ Epic failure processed successfully")
    print(f"   Lesson captured to Firebase")
    print(f"   Session updated")


if __name__ == "__main__":
    main()

# Made with Bob
