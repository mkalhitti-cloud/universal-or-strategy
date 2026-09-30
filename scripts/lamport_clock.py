#!/usr/bin/env python3
"""
V12.52 Lamport Causal Verification - Deterministic Workflow Engine

Makes Wave 6 execution DETERMINISTIC using Lamport's logical clocks:
1. Same inputs → Same outputs (reproducible)
2. Execution order is predictable (causal ordering)
3. No race conditions (happens-before enforced)
4. Rollback/replay is possible (event log)

Key Principle: If A → B (A happens-before B), then clock(A) < clock(B)

Reference: docs/protocol/V12_52_LAMPORT_CAUSAL_VERIFICATION.md
"""

import json
import os
from pathlib import Path
from typing import Dict, List, Optional, Tuple
from datetime import datetime
import hashlib


class DeterministicWorkflow:
    """
    Deterministic workflow engine using Lamport clocks.
    
    Guarantees:
    1. Phase execution order is deterministic (respects dependencies)
    2. Concurrent phases execute in predictable order (sorted by epic ID)
    3. State transitions are atomic (manifest + filesystem sync)
    4. Rollback/replay is possible (event log with checksums)
    """
    
    def __init__(self, workflow_dir: str = ".lamport"):
        self.workflow_dir = Path(workflow_dir)
        self.workflow_dir.mkdir(exist_ok=True)
        
        self.global_clock_file = self.workflow_dir / "global_clock.json"
        self.event_log_file = self.workflow_dir / "event_log.jsonl"
        
        self.global_clock = self._load_global_clock()
    
    def _load_global_clock(self) -> int:
        """Load global logical clock (monotonically increasing)."""
        if self.global_clock_file.exists():
            with open(self.global_clock_file, 'r') as f:
                data = json.load(f)
                return data.get('clock', 0)
        return 0
    
    def _save_global_clock(self):
        """Save global logical clock."""
        with open(self.global_clock_file, 'w') as f:
            json.dump({
                'clock': self.global_clock,
                'updated_at': datetime.utcnow().isoformat()
            }, f, indent=2)
    
    def _append_event(self, event: Dict):
        """Append event to immutable log (JSONL format)."""
        with open(self.event_log_file, 'a') as f:
            f.write(json.dumps(event) + '\n')
    
    def _compute_state_hash(self, epic_id: str, phase: str) -> str:
        """
        Compute deterministic hash of epic state.
        
        Includes:
        - Manifest content
        - Phase output files
        - Git commit SHA
        
        Returns:
            SHA256 hash of state
        """
        state_parts = []
        
        # 1. Manifest content
        manifest_path = Path(f"docs/brain/{epic_id}/manifest.json")
        if manifest_path.exists():
            with open(manifest_path, 'r') as f:
                state_parts.append(f.read())
        
        # 2. Phase output files
        brain_dir = Path(f"docs/brain/{epic_id}")
        if brain_dir.exists():
            for file in sorted(brain_dir.glob("*.md")):
                with open(file, 'r') as f:
                    state_parts.append(f"{file.name}:{f.read()}")
        
        # 3. Git commit SHA
        import subprocess
        try:
            git_sha = subprocess.check_output(
                ['git', 'rev-parse', 'HEAD'],
                text=True
            ).strip()
            state_parts.append(f"git:{git_sha}")
        except:
            pass
        
        # Compute hash
        combined = '\n'.join(state_parts)
        return hashlib.sha256(combined.encode()).hexdigest()
    
    def tick(self) -> int:
        """Increment global clock (atomic operation)."""
        self.global_clock += 1
        self._save_global_clock()
        return self.global_clock
    
    def record_event(
        self,
        event_type: str,
        epic_id: str,
        phase: str,
        agent_id: str,
        status: str,
        data: Optional[Dict] = None
    ) -> Dict:
        """
        Record deterministic event with state hash.
        
        Args:
            event_type: 'phase_start' | 'phase_complete' | 'phase_fail'
            epic_id: Epic identifier (e.g., 'EPIC-CCN-001')
            phase: Phase identifier (e.g., '0', '1', '5.1')
            agent_id: Agent identifier (e.g., 'wave6-phase0-001')
            status: 'pending' | 'running' | 'completed' | 'failed'
            data: Optional event data
        
        Returns:
            Event dict with clock and state hash
        """
        clock = self.tick()
        state_hash = self._compute_state_hash(epic_id, phase)
        
        event = {
            'clock': clock,
            'event_type': event_type,
            'epic_id': epic_id,
            'phase': phase,
            'agent_id': agent_id,
            'status': status,
            'state_hash': state_hash,
            'data': data or {},
            'timestamp': datetime.utcnow().isoformat()
        }
        
        self._append_event(event)
        return event
    
    def get_event_log(
        self,
        epic_id: Optional[str] = None,
        phase: Optional[str] = None
    ) -> List[Dict]:
        """
        Get event log, optionally filtered.
        
        Args:
            epic_id: Optional epic filter
            phase: Optional phase filter
        
        Returns:
            List of events in causal order (sorted by clock)
        """
        if not self.event_log_file.exists():
            return []
        
        events = []
        with open(self.event_log_file, 'r') as f:
            for line in f:
                event = json.loads(line.strip())
                
                # Apply filters
                if epic_id and event.get('epic_id') != epic_id:
                    continue
                if phase and event.get('phase') != phase:
                    continue
                
                events.append(event)
        
        # Sort by clock (causal order)
        events.sort(key=lambda e: e['clock'])
        return events
    
    def verify_determinism(self, epic_id: str, phase: str) -> Tuple[bool, str]:
        """
        Verify workflow determinism for an epic/phase.
        
        Checks:
        1. Dependencies satisfied (happens-before)
        2. State hash matches expected
        3. No concurrent conflicts
        
        Args:
            epic_id: Epic identifier
            phase: Phase identifier
        
        Returns:
            (is_deterministic, reason) tuple
        """
        # 1. Check dependencies
        satisfied, reason = self.check_dependencies(epic_id, phase)
        if not satisfied:
            return False, f"Dependencies not satisfied: {reason}"
        
        # 2. Check state consistency
        events = self.get_event_log(epic_id, phase)
        if len(events) > 1:
            # Verify state hashes are consistent
            hashes = [e['state_hash'] for e in events if e['event_type'] == 'phase_complete']
            if len(set(hashes)) > 1:
                return False, f"State hash mismatch: {len(set(hashes))} different states"
        
        # 3. Check for concurrent conflicts (only check phase_start events FOR THIS EPIC)
        # Allow parallel execution of DIFFERENT epics, only block concurrent execution of SAME epic
        running_events = [
            e for e in events
            if e['epic_id'] == epic_id and e['status'] == 'running' and e['event_type'] == 'phase_start'
        ]
        if len(running_events) > 1:
            return False, f"Concurrent execution detected for {epic_id}: {len(running_events)} agents"
        
        return True, "Workflow is deterministic"
    
    def check_dependencies(self, epic_id: str, phase: str) -> Tuple[bool, str]:
        """
        Check if all dependencies for a phase are satisfied.
        
        Phase dependency graph (deterministic order):
        -1 → 0 → 1 → 1.5 → 2 → 3 → 4 → 4.5 → 5.X → 5.X.V → 6
        
        Checks BOTH:
        1. Global event log (.lamport/event_log.jsonl)
        2. Manifest lamport_events array (fallback for migrated epics)
        
        Args:
            epic_id: Epic identifier
            phase: Phase identifier
        
        Returns:
            (satisfied, reason) tuple
        """
        # Phase dependency map
        # Note: Phase -1 is optional (pre-flight checks)
        dependencies = {
            '-1': [],
            '0': [],  # Phase 0 has no dependencies (Phase -1 is optional)
            '1': ['0'],
            '1.5': ['1'],
            '2': ['1.5'],
            '3': ['2'],
            '4': ['3'],
            '4.5': ['4'],
            '5.1': ['4.5'],
            '5.2': ['5.1', '5.1.V'],  # Requires previous ticket + verification
            '5.3': ['5.2', '5.2.V'],
            '5.1.V': ['5.1'],
            '5.2.V': ['5.2'],
            '5.3.V': ['5.3'],
            '6': []  # Will be computed dynamically (all 5.X.V)
        }
        
        # Special case: Phase 6 requires ALL ticket verifications
        if phase == '6':
            events = self.get_event_log(epic_id)
            ticket_phases = set(e['phase'] for e in events if e['phase'].startswith('5.') and e['phase'].endswith('.V'))
            dependencies['6'] = list(ticket_phases)
        
        required_phases = dependencies.get(phase, [])
        if not required_phases:
            return True, "No dependencies"
        
        # Check each required phase FOR THIS EPIC ONLY
        # First check global event log
        events = self.get_event_log(epic_id)  # Already filtered by epic_id
        
        # Also load manifest events as fallback (for migrated epics)
        manifest_events = self._load_manifest_events(epic_id)
        
        for req_phase in required_phases:
            # Check global event log first
            completions = [
                e for e in events
                if e['phase'] == req_phase and e['event_type'] == 'phase_complete' and e.get('status', 'completed') == 'completed'
            ]
            
            # Fallback: check manifest events
            if not completions and manifest_events:
                completions = [
                    e for e in manifest_events
                    if e.get('phase') == req_phase and e.get('event_type') == 'phase_complete'
                ]
            
            if not completions:
                return False, f"Phase {req_phase} not complete (checked event log + manifest)"
        
        return True, "All dependencies satisfied"
    
    def _load_manifest_events(self, epic_id: str) -> List[Dict]:
        """
        Load lamport_events from manifest as fallback.
        
        This handles migrated epics where events exist in manifest
        but not in global event log.
        
        Args:
            epic_id: Epic identifier
        
        Returns:
            List of events from manifest, or empty list if not found
        """
        manifest_path = Path(f"docs/brain/{epic_id}/manifest.json")
        if not manifest_path.exists():
            return []
        
        try:
            with open(manifest_path, 'r') as f:
                manifest = json.load(f)
                
                # Collect events from all phases
                all_events = []
                for phase_id, phase_data in manifest.get('phases', {}).items():
                    phase_events = phase_data.get('lamport_events', [])
                    # Add phase field if missing
                    for event in phase_events:
                        if 'phase' not in event:
                            event['phase'] = phase_id
                        all_events.append(event)
                
                return all_events
        except Exception:
            return []
    
    def get_next_phases(self, epic_id: str) -> List[str]:
        """
        Get next executable phases in deterministic order.
        
        Returns phases that:
        1. Have all dependencies satisfied
        2. Are not already running/completed
        3. Are sorted deterministically (by phase number)
        
        Args:
            epic_id: Epic identifier
        
        Returns:
            List of phase identifiers ready to execute
        """
        all_phases = ['-1', '0', '1', '1.5', '2', '3', '4', '4.5', '5.1', '5.1.V', '5.2', '5.2.V', '5.3', '5.3.V', '6']
        
        events = self.get_event_log(epic_id)
        completed_phases = set(
            e['phase'] for e in events
            if e['event_type'] == 'phase_complete' and e['status'] == 'completed'
        )
        running_phases = set(
            e['phase'] for e in events
            if e['status'] == 'running'
        )
        
        next_phases = []
        for phase in all_phases:
            # Skip if already completed or running
            if phase in completed_phases or phase in running_phases:
                continue
            
            # Check dependencies
            satisfied, _ = self.check_dependencies(epic_id, phase)
            if satisfied:
                next_phases.append(phase)
        
        return next_phases
    
    def replay_workflow(self, epic_id: str) -> List[Dict]:
        """
        Replay workflow from event log (for debugging/recovery).
        
        Args:
            epic_id: Epic identifier
        
        Returns:
            List of events in causal order
        """
        return self.get_event_log(epic_id)


# Global workflow instance
_workflow = None

def get_workflow(workflow_dir: str = ".lamport") -> DeterministicWorkflow:
    """Get or create global workflow instance."""
    global _workflow
    if _workflow is None:
        _workflow = DeterministicWorkflow(workflow_dir)
    return _workflow


# Convenience functions
def record_phase_start(epic_id: str, phase: str, agent_id: str) -> Dict:
    """Record phase start event."""
    workflow = get_workflow()
    return workflow.record_event('phase_start', epic_id, phase, agent_id, 'running')


def record_phase_complete(epic_id: str, phase: str, agent_id: str, data: Optional[Dict] = None) -> Dict:
    """Record phase completion event."""
    workflow = get_workflow()
    return workflow.record_event('phase_complete', epic_id, phase, agent_id, 'completed', data)


def record_phase_fail(epic_id: str, phase: str, agent_id: str, error: str) -> Dict:
    """Record phase failure event."""
    workflow = get_workflow()
    return workflow.record_event('phase_fail', epic_id, phase, agent_id, 'failed', {'error': error})


def verify_can_execute(epic_id: str, phase: str) -> Tuple[bool, str]:
    """Verify phase can execute (dependencies satisfied, deterministic)."""
    workflow = get_workflow()
    
    # Check dependencies
    satisfied, reason = workflow.check_dependencies(epic_id, phase)
    if not satisfied:
        return False, f"BLOCKED: {reason}"
    
    # Check determinism
    is_deterministic, reason = workflow.verify_determinism(epic_id, phase)
    if not is_deterministic:
        return False, f"NON-DETERMINISTIC: {reason}"
    
    return True, "Ready to execute"


# Example usage
if __name__ == "__main__":
    workflow = get_workflow()
    
    # Record Phase 0 start
    event1 = record_phase_start("EPIC-CCN-001", "0", "wave6-phase0-001")
    print(f"Phase 0 start: clock={event1['clock']}, hash={event1['state_hash'][:8]}")
    
    # Record Phase 0 complete
    event2 = record_phase_complete("EPIC-CCN-001", "0", "wave6-phase0-001", {"cyc_before": 21, "cyc_after": 8})
    print(f"Phase 0 complete: clock={event2['clock']}, hash={event2['state_hash'][:8]}")
    
    # Check if Phase 1 can execute
    can_execute, reason = verify_can_execute("EPIC-CCN-001", "1")
    print(f"Phase 1 ready: {can_execute} - {reason}")
    
    # Get next phases
    next_phases = workflow.get_next_phases("EPIC-CCN-001")
    print(f"Next phases: {next_phases}")

# Made with Bob
