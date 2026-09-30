#!/usr/bin/env python3
"""Capture Lesson Learned to Firebase Knowledge Base.

Automatically extracts and stores lessons learned from epic failures,
forensic reports, and refactoring sessions to the Firebase `learnings` collection.

Usage:
    python scripts/capture_lesson.py <epic_id> <category> <insight> <confidence>
    python scripts/capture_lesson.py --from-forensic <forensic_report_path>

Categories:
    - architecture: System design, correctness patterns
    - refactoring: Lock-free, FSM, actor patterns
    - performance: Latency, microsecond optimization
    - testing: Verification, testing patterns
    - debugging: Observability, debugging patterns
    - workflow: Process, automation, tooling

Examples:
    python scripts/capture_lesson.py EPIC-CCN-1 refactoring \
        "Always refresh jCodemunch index before epic planning" 0.95
    
    python scripts/capture_lesson.py --from-forensic \
        docs/brain/EPIC-CCN-1/FORENSIC_REPORT.md
"""

import os
import sys
import json
import re
from pathlib import Path
from datetime import datetime, timezone
from typing import Dict, List, Optional

try:
    from query_kb import init_firestore
except ImportError:
    print("[-] Error: query_kb module not found. Cannot connect to Firebase.")
    sys.exit(1)


def capture_lesson(
    epic_id: str,
    category: str,
    insight: str,
    confidence: float,
    source: str = "manual",
    metadata: Optional[Dict] = None
) -> bool:
    """Capture a lesson learned to Firebase.
    
    Args:
        epic_id: Epic identifier (e.g., EPIC-CCN-1)
        category: Lesson category (architecture, refactoring, etc.)
        insight: The lesson learned (clear, actionable statement)
        confidence: Confidence score 0.0-1.0
        source: Source of the lesson (manual, forensic, automated)
        metadata: Additional context (file paths, CYC deltas, etc.)
        
    Returns:
        True if successful, False otherwise
    """
    try:
        db = init_firestore()
        learnings_ref = db.collection('learnings')
        
        lesson_data = {
            'epic_id': epic_id,
            'category': category,
            'insight': insight,
            'confidence': confidence,
            'source': source,
            'timestamp': datetime.now(timezone.utc),
            'metadata': metadata or {}
        }
        
        # Add to Firebase
        doc_ref = learnings_ref.add(lesson_data)
        
        print(f"[+] Lesson captured successfully!")
        print(f"    Epic: {epic_id}")
        print(f"    Category: {category}")
        print(f"    Confidence: {confidence:.2f}")
        print(f"    Document ID: {doc_ref[1].id}")
        
        return True
        
    except Exception as e:
        print(f"[-] Error capturing lesson: {e}")
        return False


def extract_lessons_from_forensic(forensic_path: str) -> List[Dict]:
    """Extract lessons learned from a forensic report.
    
    Args:
        forensic_path: Path to forensic report markdown file
        
    Returns:
        List of lesson dictionaries
    """
    lessons = []
    
    try:
        with open(forensic_path, 'r', encoding='utf-8') as f:
            content = f.read()
        
        # Extract epic ID from path or content
        epic_id_match = re.search(r'EPIC-[A-Z]+-\d+', forensic_path)
        if not epic_id_match:
            epic_id_match = re.search(r'EPIC-[A-Z]+-\d+', content)
        
        epic_id = epic_id_match.group(0) if epic_id_match else "UNKNOWN"
        
        # Extract root cause section
        root_cause_match = re.search(
            r'## Root Cause[:\s]+(.*?)(?=\n##|\Z)',
            content,
            re.DOTALL | re.IGNORECASE
        )
        
        if root_cause_match:
            root_cause = root_cause_match.group(1).strip()
            
            # Extract key insights
            if "SYSTEMIC" in root_cause.upper():
                lessons.append({
                    'epic_id': epic_id,
                    'category': 'workflow',
                    'insight': 'Systemic workflow failure detected - requires protocol-level fixes',
                    'confidence': 0.95,
                    'source': 'forensic',
                    'metadata': {'root_cause': root_cause[:200]}
                })
        
        # Extract protocol gaps
        gaps_match = re.search(
            r'## Protocol Gaps[:\s]+(.*?)(?=\n##|\Z)',
            content,
            re.DOTALL | re.IGNORECASE
        )
        
        if gaps_match:
            gaps_text = gaps_match.group(1).strip()
            
            # Extract numbered gaps
            gap_pattern = r'\d+\.\s+\*\*([^*]+)\*\*[:\s]+([^\n]+)'
            for match in re.finditer(gap_pattern, gaps_text):
                gap_name = match.group(1).strip()
                gap_desc = match.group(2).strip()
                
                lessons.append({
                    'epic_id': epic_id,
                    'category': 'workflow',
                    'insight': f"{gap_name}: {gap_desc}",
                    'confidence': 0.90,
                    'source': 'forensic',
                    'metadata': {'gap_type': gap_name}
                })
        
        # Extract corrective actions
        actions_match = re.search(
            r'## Corrective Actions[:\s]+(.*?)(?=\n##|\Z)',
            content,
            re.DOTALL | re.IGNORECASE
        )
        
        if actions_match:
            actions_text = actions_match.group(1).strip()
            
            # Extract action items
            action_pattern = r'\d+\.\s+\*\*([^*]+)\*\*[:\s]+([^\n]+)'
            for match in re.finditer(action_pattern, actions_text):
                action_name = match.group(1).strip()
                action_desc = match.group(2).strip()
                
                lessons.append({
                    'epic_id': epic_id,
                    'category': 'workflow',
                    'insight': f"Corrective action: {action_name} - {action_desc}",
                    'confidence': 0.85,
                    'source': 'forensic',
                    'metadata': {'action_type': action_name}
                })
        
        print(f"[+] Extracted {len(lessons)} lessons from forensic report")
        return lessons
        
    except Exception as e:
        print(f"[-] Error extracting lessons from forensic report: {e}")
        return []


def main():
    """Main entry point."""
    if len(sys.argv) < 2:
        print(__doc__)
        sys.exit(1)
    
    # Check for --from-forensic flag
    if sys.argv[1] == '--from-forensic':
        if len(sys.argv) < 3:
            print("[-] Error: Missing forensic report path")
            print("Usage: python scripts/capture_lesson.py --from-forensic <path>")
            sys.exit(1)
        
        forensic_path = sys.argv[2]
        
        if not os.path.exists(forensic_path):
            print(f"[-] Error: Forensic report not found: {forensic_path}")
            sys.exit(1)
        
        # Extract lessons
        lessons = extract_lessons_from_forensic(forensic_path)
        
        if not lessons:
            print("[-] No lessons extracted from forensic report")
            sys.exit(1)
        
        # Capture each lesson
        success_count = 0
        for lesson in lessons:
            if capture_lesson(**lesson):
                success_count += 1
        
        print(f"\n[+] Captured {success_count}/{len(lessons)} lessons successfully")
        sys.exit(0 if success_count == len(lessons) else 1)
    
    # Manual lesson capture
    if len(sys.argv) < 5:
        print("[-] Error: Missing required arguments")
        print("Usage: python scripts/capture_lesson.py <epic_id> <category> <insight> <confidence>")
        sys.exit(1)
    
    epic_id = sys.argv[1]
    category = sys.argv[2]
    insight = sys.argv[3]
    confidence = float(sys.argv[4])
    
    # Validate category
    valid_categories = ['architecture', 'refactoring', 'performance', 'testing', 'debugging', 'workflow']
    if category not in valid_categories:
        print(f"[-] Error: Invalid category '{category}'")
        print(f"    Valid categories: {', '.join(valid_categories)}")
        sys.exit(1)
    
    # Validate confidence
    if not 0.0 <= confidence <= 1.0:
        print(f"[-] Error: Confidence must be between 0.0 and 1.0 (got {confidence})")
        sys.exit(1)
    
    # Capture lesson
    success = capture_lesson(epic_id, category, insight, confidence, source='manual')
    sys.exit(0 if success else 1)


if __name__ == "__main__":
    main()

# Made with Bob
