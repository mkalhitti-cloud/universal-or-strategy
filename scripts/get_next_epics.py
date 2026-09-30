#!/usr/bin/env python3
"""Extract next N pending epics from roadmap."""
import json
import sys

def get_next_pending_epics(count=3):
    """Get next N pending epics from epic_roadmap.json."""
    with open('epic_roadmap.json', 'r') as f:
        roadmap = json.load(f)
    
    # Filter pending epics (no status or status != 'complete')
    pending = [e for e in roadmap if e.get('status') != 'complete']
    
    # Get first N
    next_epics = pending[:count]
    
    print(f"Next {count} pending epics:\n")
    for epic in next_epics:
        print(f"  {epic['epic_number']}: {epic['method']}")
        print(f"    File: {epic['file']}")
        print(f"    Line: {epic['line']}")
        print(f"    CYC: {epic['cyclomatic']}")
        print()
    
    return next_epics

if __name__ == '__main__':
    count = int(sys.argv[1]) if len(sys.argv) > 1 else 3
    get_next_pending_epics(count)

# Made with Bob
