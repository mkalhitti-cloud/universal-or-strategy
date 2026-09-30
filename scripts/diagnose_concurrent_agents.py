#!/usr/bin/env python3
"""
Diagnose why concurrent agents are detected.
"""

import json
from pathlib import Path

def diagnose_epic(epic_id: str):
    """Diagnose concurrent agent detection for an epic."""
    manifest_path = Path(f"docs/brain/{epic_id}/manifest.json")
    
    if not manifest_path.exists():
        print(f"[ERROR] {epic_id}: No manifest found")
        return
    
    with open(manifest_path, 'r') as f:
        manifest = json.load(f)
    
    print(f"\n{epic_id}")
    print("=" * 60)
    
    # Collect all events from manifest
    all_events = []
    for phase_id, phase_data in manifest.get('phases', {}).items():
        phase_events = phase_data.get('lamport_events', [])
        for event in phase_events:
            if 'phase' not in event:
                event['phase'] = phase_id
            all_events.append(event)
    
    print(f"Total events in manifest: {len(all_events)}")
    
    # Count by event type
    phase_starts = [e for e in all_events if e.get('event_type') == 'phase_start']
    phase_completes = [e for e in all_events if e.get('event_type') == 'phase_complete']
    
    print(f"Phase start events: {len(phase_starts)}")
    print(f"Phase complete events: {len(phase_completes)}")
    
    # Count by status
    running = [e for e in all_events if e.get('status') == 'running']
    completed = [e for e in all_events if e.get('status') == 'completed']
    
    print(f"Running status: {len(running)}")
    print(f"Completed status: {len(completed)}")
    
    # Show running events
    if running:
        print("\nRunning events:")
        for e in running:
            print(f"  Phase {e.get('phase')}: {e.get('event_type')} status={e.get('status')}")
    
    # The issue: verify_determinism checks for concurrent execution
    # by counting phase_start events with status='running'
    # We have 2 phase_start events (Phase 0 and Phase 1), both with status='running'
    print(f"\n[ISSUE] Concurrent agents detected: {len(running)} phase_start events with status='running'")
    print("[FIX] Phase_complete events should have status='completed', phase_start should have status='running'")
    print("      But for COMPLETED phases, we should only keep the phase_complete event")

def main():
    print("Diagnosing concurrent agent detection...")
    
    pilot_epics = ['EPIC-CCN-001', 'EPIC-CCN-002', 'EPIC-CCN-004']
    
    for epic_id in pilot_epics:
        diagnose_epic(epic_id)

if __name__ == '__main__':
    main()

# Made with Bob
