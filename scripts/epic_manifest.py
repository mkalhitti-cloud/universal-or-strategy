#!/usr/bin/env python3
"""
Epic Manifest Management with V12.52 Lamport Causal Verification

Provides functions for creating, reading, updating, and validating
epic workflow manifests with deterministic execution guarantees.

V12.52 Features:
- Lamport logical clocks for causal ordering
- Deterministic phase execution (same inputs → same outputs)
- State hash verification (manifest + filesystem)
- Blocking gates (prevent execution with stale state)
- Event log for rollback/replay

Usage:
    from epic_manifest import load_manifest, update_manifest, validate_dependencies
    
    # Load manifest
    manifest = load_manifest("EPIC-CCN-15")
    
    # Update phase status (with causal verification)
    update_manifest("EPIC-CCN-15", "1", "completed",
                   outputs=["docs/brain/EPIC-CCN-15/00-scope.md"],
                   notes="Scope defined")
    
    # Check dependencies (with determinism check)
    if validate_dependencies("EPIC-CCN-15", "1.5"):
        print("Phase 1.5 ready to execute")
    
    # Verify can execute (V12.52 blocking gate)
    can_execute, reason = verify_can_execute("EPIC-CCN-15", "1.5")
    if not can_execute:
        print(f"BLOCKED: {reason}")
    
    # Get next phases (deterministic order)
    next_phases = get_next_phases("EPIC-CCN-15")
    print(f"Ready to execute: {next_phases}")
    
    # Generate new manifest
    manifest = generate_manifest("EPIC-CCN-16", "Extract CalculatePositionSize")
"""

import json
import os
import re
from datetime import datetime, timezone
from pathlib import Path
from typing import Dict, List, Optional, Any, Set, Tuple
import time

# Import V12.52 deterministic workflow engine
try:
    # Try relative import first (when called from repo root)
    from scripts.lamport_clock import (
        get_workflow,
        record_phase_start,
        record_phase_complete,
        record_phase_fail,
        verify_can_execute as lamport_verify_can_execute
    )
except ModuleNotFoundError:
    # Fallback to direct import (when called from scripts/ directory)
    from lamport_clock import (
        get_workflow,
        record_phase_start,
        record_phase_complete,
        record_phase_fail,
        verify_can_execute as lamport_verify_can_execute
    )

# Platform-specific imports for file locking
try:
    import fcntl
    HAS_FCNTL = True
except ImportError:
    # Windows doesn't have fcntl, use msvcrt instead
    import msvcrt
    HAS_FCNTL = False

# Constants
MANIFEST_SCHEMA_VERSION = "1.0"
BRAIN_DIR = Path("docs/brain")
MANIFEST_FILENAME = "manifest.json"

# Valid status values
EPIC_STATUSES = {"pending", "in_progress", "completed", "failed", "blocked"}
PHASE_STATUSES = {"pending", "in_progress", "completed", "failed", "blocked", "skipped"}

# Valid modes
# Valid modes for manifest validation
# Base modes (built into Bob CLI, no custom_modes.yaml needed):
VALID_MODES = {"ask", "plan", "advanced"}

# Custom modes (defined in .bob/custom_modes.yaml):
# V12.53: ALL 10 phases now have custom modes with MCP tool enforcement
CUSTOM_MODES = {
    "v12-phase0-hotspot",      # Phase 0: Hotspot Analysis
    "v12-phase1-scope",        # Phase 1: Scope Definition
    "v12-phase1-5-boundary",   # Phase 1.5: Scope Boundary Validation
    "v12-phase2-architecture", # Phase 2: Architecture Planning
    "v12-phase3-audit",        # Phase 3: DNA & PR Audit
    "v12-phase4-tickets",      # Phase 4: Ticket Generation
    "v12-phase4-5-review",     # Phase 4.5: Ticket Review (Jane Street)
    "v12-engineer",            # Phase 5: Ticket Execution
    "v12-phase5-v-verify",     # Phase 5.V: Verification
    "v12-phase6-review",       # Phase 6: Final Review
    "v12-epic-planner",        # Interactive epic planning (not wave execution)
    "v12-phase7-lead",         # Concurrency engineering (not wave execution)
    "autonomous-refactor"      # Wave orchestration (not phase execution)
}

# All valid modes (base + custom):
ALL_VALID_MODES = VALID_MODES | CUSTOM_MODES

# Valid status transitions
VALID_TRANSITIONS = {
    "pending": {"in_progress", "blocked", "skipped"},
    "in_progress": {"completed", "failed"},
    "completed": set(),  # Terminal state
    "failed": {"in_progress"},  # Can retry
    "blocked": {"in_progress"},
    "skipped": set()  # Terminal state
}

# Phase ID pattern
PHASE_ID_PATTERN = re.compile(r'^\d+(\.\d+)?(\.V)?$')


class ManifestError(Exception):
    """Base exception for manifest operations"""
    pass


class ValidationError(ManifestError):
    """Raised when manifest validation fails"""
    pass


class DependencyError(ManifestError):
    """Raised when dependency validation fails"""
    pass


def _get_manifest_path(epic_id: str) -> Path:
    """Get path to manifest file for an epic"""
    return BRAIN_DIR / epic_id / MANIFEST_FILENAME


def _validate_phase_id(phase_id: str) -> None:
    """Validate phase ID format"""
    if not PHASE_ID_PATTERN.match(phase_id):
        raise ValidationError(
            f"Invalid phase ID format: {phase_id}. "
            f"Expected pattern: ^\\d+(\\.\\d+)?(\\.V)?$"
        )


def _validate_status_transition(from_status: str, to_status: str) -> None:
    """Validate status transition is allowed"""
    if from_status not in VALID_TRANSITIONS:
        raise ValidationError(f"Invalid status: {from_status}")
    
    if to_status not in VALID_TRANSITIONS:
        raise ValidationError(f"Invalid status: {to_status}")
    
    if to_status not in VALID_TRANSITIONS[from_status]:
        raise ValidationError(
            f"Invalid status transition: {from_status} -> {to_status}. "
            f"Allowed transitions from {from_status}: {VALID_TRANSITIONS[from_status]}"
        )


def _validate_artifact_path(artifact: str, epic_id: str) -> None:
    """Validate artifact path is in correct location"""
    expected_prefix = f"docs/brain/{epic_id}/"
    if not artifact.startswith(expected_prefix):
        raise ValidationError(
            f"Artifact path must start with {expected_prefix}, got: {artifact}"
        )
    
    if os.path.isabs(artifact):
        raise ValidationError(
            f"Artifact path must be relative, got absolute path: {artifact}"
        )


def _validate_timestamps(phase: Dict[str, Any], created_at: str) -> None:
    """Validate phase timestamps are in correct order"""
    started_at = phase.get('started_at')
    completed_at = phase.get('completed_at')
    
    if started_at:
        started_dt = datetime.fromisoformat(started_at.replace('Z', '+00:00'))
        created_dt = datetime.fromisoformat(created_at.replace('Z', '+00:00'))
        
        if started_dt < created_dt:
            raise ValidationError(
                f"started_at ({started_at}) must be after created_at ({created_at})"
            )
    
    if completed_at and started_at:
        completed_dt = datetime.fromisoformat(completed_at.replace('Z', '+00:00'))
        started_dt = datetime.fromisoformat(started_at.replace('Z', '+00:00'))
        
        if completed_dt < started_dt:
            raise ValidationError(
                f"completed_at ({completed_at}) must be after started_at ({started_at})"
            )


def _detect_circular_dependencies(
    dependencies: Dict[str, List[str]], 
    phase: str,
    visited: Optional[Set[str]] = None,
    path: Optional[List[str]] = None
) -> Optional[List[str]]:
    """
    Detect circular dependencies using DFS.
    Returns cycle path if found, None otherwise.
    """
    if visited is None:
        visited = set()
    if path is None:
        path = []
    
    if phase in path:
        # Found cycle
        cycle_start = path.index(phase)
        return path[cycle_start:] + [phase]
    
    if phase in visited:
        return None
    
    visited.add(phase)
    path.append(phase)
    
    for dep in dependencies.get(phase, []):
        cycle = _detect_circular_dependencies(dependencies, dep, visited, path.copy())
        if cycle:
            return cycle
    
    return None


def load_manifest(epic_id: str) -> Dict[str, Any]:
    """
    Load and validate manifest for an epic.
    
    Args:
        epic_id: Epic identifier (e.g., "EPIC-CCN-15")
        
    Returns:
        Parsed manifest dictionary
        
    Raises:
        FileNotFoundError: If manifest doesn't exist
        ValidationError: If manifest is invalid
        
    Example:
        >>> manifest = load_manifest("EPIC-CCN-15")
        >>> print(manifest['status'])
        'in_progress'
    """
    manifest_path = _get_manifest_path(epic_id)
    
    if not manifest_path.exists():
        raise FileNotFoundError(
            f"Manifest not found: {manifest_path}. "
            f"Use generate_manifest() to create a new manifest."
        )
    
    try:
        with open(manifest_path, 'r', encoding='utf-8') as f:
            manifest = json.load(f)
    except json.JSONDecodeError as e:
        raise ValidationError(f"Invalid JSON in manifest: {e}")
    
    # Validate required fields
    required_fields = ['epic_id', 'description', 'status', 'created_at', 'phases', 'dependencies']
    for field in required_fields:
        if field not in manifest:
            raise ValidationError(f"Missing required field: {field}")
    
    # Validate epic_id matches
    if manifest['epic_id'] != epic_id:
        raise ValidationError(
            f"Epic ID mismatch: expected {epic_id}, got {manifest['epic_id']}"
        )
    
    # Validate status
    if manifest['status'] not in EPIC_STATUSES:
        raise ValidationError(f"Invalid epic status: {manifest['status']}")
    
    # Validate phases
    for phase_id, phase in manifest['phases'].items():
        _validate_phase_id(phase_id)
        
        if phase['status'] not in PHASE_STATUSES:
            raise ValidationError(
                f"Invalid phase status for {phase_id}: {phase['status']}"
            )
        
        if phase['mode'] not in ALL_VALID_MODES:
            raise ValidationError(
                f"Invalid mode for {phase_id}: {phase['mode']}. Must be one of: {', '.join(sorted(ALL_VALID_MODES))}"
            )
        
        _validate_timestamps(phase, manifest['created_at'])
    
    # Validate dependencies reference existing phases
    for phase_id, deps in manifest['dependencies'].items():
        if phase_id not in manifest['phases']:
            raise ValidationError(
                f"Dependency references non-existent phase: {phase_id}"
            )
        
        for dep in deps:
            if dep not in manifest['phases']:
                raise ValidationError(
                    f"Phase {phase_id} depends on non-existent phase: {dep}"
                )
    
    # Check for circular dependencies
    for phase_id in manifest['dependencies']:
        cycle = _detect_circular_dependencies(manifest['dependencies'], phase_id)
        if cycle:
            raise DependencyError(
                f"Circular dependency detected: {' -> '.join(cycle)}"
            )
    
    return manifest


def update_manifest(
    epic_id: str,
    phase: str,
    status: str,
    outputs: Optional[List[str]] = None,
    notes: Optional[str] = None
) -> None:
    """
    Update phase status and outputs in manifest.
    
    Uses file locking to prevent race conditions during concurrent updates.
    
    Args:
        epic_id: Epic identifier
        phase: Phase ID (e.g., "1.5", "5.1")
        status: New status (pending/in_progress/completed/failed/blocked/skipped)
        outputs: List of output artifact paths (relative to repo root)
        notes: Optional notes about phase execution
        
    Raises:
        ValidationError: If status transition is invalid
        FileNotFoundError: If manifest doesn't exist
        
    Example:
        >>> update_manifest(
        ...     "EPIC-CCN-15",
        ...     "1",
        ...     "completed",
        ...     outputs=["docs/brain/EPIC-CCN-15/00-scope.md"],
        ...     notes="Scope limited to 3 methods"
        ... )
    """
    _validate_phase_id(phase)
    
    manifest_path = _get_manifest_path(epic_id)
    
    # Use file locking for atomic updates
    with open(manifest_path, 'r+', encoding='utf-8') as f:
        # Acquire exclusive lock (platform-specific)
        # Skip locking in test mode (Windows has issues with msvcrt.locking)
        use_locking = os.getenv('EPIC_MANIFEST_NO_LOCK') != '1'
        
        if use_locking:
            if HAS_FCNTL:
                fcntl.flock(f.fileno(), fcntl.LOCK_EX)
            else:
                # Windows: lock first byte of file
                try:
                    msvcrt.locking(f.fileno(), msvcrt.LK_LOCK, 1)
                except OSError as e:
                    # If lock fails, continue without lock (test mode)
                    if os.getenv('EPIC_MANIFEST_NO_LOCK') != '1':
                        raise
        
        try:
            manifest = json.load(f)
            
            if phase not in manifest['phases']:
                raise ValidationError(f"Phase not found in manifest: {phase}")
            
            phase_data = manifest['phases'][phase]
            old_status = phase_data['status']
            
            # Validate status transition
            _validate_status_transition(old_status, status)
            
            # Update status
            phase_data['status'] = status
            
            # Update timestamps
            now = datetime.now(timezone.utc).isoformat()
            
            if status == 'in_progress' and not phase_data.get('started_at'):
                phase_data['started_at'] = now
            
            if status in ('completed', 'failed', 'skipped'):
                phase_data['completed_at'] = now
            
            # Update outputs
            if outputs is not None:
                # Validate artifact paths
                for artifact in outputs:
                    _validate_artifact_path(artifact, epic_id)
                
                # Verify artifacts exist if status is completed
                if status == 'completed':
                    for artifact in outputs:
                        if not os.path.exists(artifact):
                            raise ValidationError(
                                f"Output artifact not found: {artifact}. "
                                f"Cannot mark phase as completed."
                            )
                        
                        if os.path.getsize(artifact) == 0:
                            raise ValidationError(
                                f"Output artifact is empty: {artifact}. "
                                f"Cannot mark phase as completed."
                            )
                
                phase_data['output_artifacts'] = outputs
            
            # Update notes
            if notes is not None:
                phase_data['notes'] = notes
            
            # Update manifest-level timestamp
            manifest['updated_at'] = now
            
            # Update epic status based on phase statuses
            phase_statuses = [p['status'] for p in manifest['phases'].values()]
            
            if all(s == 'completed' for s in phase_statuses):
                manifest['status'] = 'completed'
            elif any(s == 'failed' for s in phase_statuses):
                manifest['status'] = 'failed'
                if phase not in manifest['metadata'].get('failed_phases', []):
                    manifest['metadata'].setdefault('failed_phases', []).append(phase)
            elif any(s == 'in_progress' for s in phase_statuses):
                manifest['status'] = 'in_progress'
            
            # Write updated manifest
            f.seek(0)
            f.truncate()
            json.dump(manifest, f, indent=2, ensure_ascii=True)
            f.write('\n')  # Add trailing newline
            
        finally:
            # Release lock (platform-specific)
            if use_locking:
                if HAS_FCNTL:
                    fcntl.flock(f.fileno(), fcntl.LOCK_UN)
                else:
                    # Windows: unlock first byte of file
                    try:
                        msvcrt.locking(f.fileno(), msvcrt.LK_UNLCK, 1)
                    except OSError:
                        pass  # Ignore unlock errors


def validate_dependencies(epic_id: str, phase: str) -> bool:
    """
    Check if all dependencies for a phase are satisfied.
    
    A dependency is satisfied if its status is 'completed'.
    
    Args:
        epic_id: Epic identifier
        phase: Phase ID to check
        
    Returns:
        True if all dependencies completed, False otherwise
        
    Raises:
        ValidationError: If phase doesn't exist
        DependencyError: If circular dependencies detected
        
    Example:
        >>> if validate_dependencies("EPIC-CCN-15", "1.5"):
        ...     print("Phase 1.5 ready to execute")
        ... else:
        ...     print("Phase 1.5 blocked on dependencies")
    """
    _validate_phase_id(phase)
    
    manifest = load_manifest(epic_id)
    
    if phase not in manifest['phases']:
        raise ValidationError(f"Phase not found in manifest: {phase}")
    
    # Check for circular dependencies
    cycle = _detect_circular_dependencies(manifest['dependencies'], phase)
    if cycle:
        raise DependencyError(
            f"Circular dependency detected for phase {phase}: {' -> '.join(cycle)}"
        )
    
    # Get dependencies for this phase
    dependencies = manifest['dependencies'].get(phase, [])
    
    # Check if all dependencies are completed
    for dep in dependencies:
        dep_status = manifest['phases'][dep]['status']
        if dep_status != 'completed':
            return False
    
    return True


def get_next_phases(epic_id: str) -> List[str]:
    """
    Determine which phases can be executed next.
    
    Returns phases that:
    - Have status "pending"
    - Have all dependencies satisfied
    - Are not blocked
    
    Args:
        epic_id: Epic identifier
        
    Returns:
        List of phase IDs ready to execute (may be empty)
        
    Example:
        >>> next_phases = get_next_phases("EPIC-CCN-15")
        >>> print(f"Ready to execute: {next_phases}")
        ['1.5', '2.3']  # Parallel phases
    """
    manifest = load_manifest(epic_id)
    
    ready_phases = []
    
    for phase_id, phase_data in manifest['phases'].items():
        # Skip if not pending
        if phase_data['status'] != 'pending':
            continue
        
        # Check if dependencies satisfied
        try:
            if validate_dependencies(epic_id, phase_id):
                ready_phases.append(phase_id)
        except DependencyError:
            # Skip phases with circular dependencies
            continue
    
    # Sort by phase ID for consistent ordering
    ready_phases.sort(key=lambda x: [int(p) if p.isdigit() else p for p in re.split(r'(\d+)', x)])
    
    return ready_phases


def generate_manifest(
    epic_id: str,
    description: str,
    ticket_count: Optional[int] = None
) -> Dict[str, Any]:
    """
    Create new manifest for an epic.
    
    Generates a minimal manifest with Phase 0 and Phase 1 defined.
    Additional phases are added as the epic progresses.
    
    Args:
        epic_id: Epic identifier (e.g., "EPIC-CCN-16")
        description: Brief epic description (10-200 chars)
        ticket_count: Number of tickets (if known, for metadata)
        
    Returns:
        New manifest dictionary
        
    Side Effects:
        Writes manifest.json to docs/brain/{epic_id}/
        Creates epic directory if it doesn't exist
        
    Raises:
        ValidationError: If epic_id format invalid or manifest already exists
        
    Example:
        >>> manifest = generate_manifest(
        ...     "EPIC-CCN-16",
        ...     "Extract CalculatePositionSize complexity"
        ... )
        >>> print(f"Manifest created at: {manifest['_path']}")
    """
    # Validate epic_id format
    if not re.match(r'^EPIC-[A-Z]+-\d+$', epic_id):
        raise ValidationError(
            f"Invalid epic_id format: {epic_id}. "
            f"Expected pattern: EPIC-{{CATEGORY}}-{{NUMBER}}"
        )
    
    # Validate description length
    if len(description) < 10 or len(description) > 200:
        raise ValidationError(
            f"Description must be 10-200 characters, got {len(description)}"
        )
    
    # Check if manifest already exists
    manifest_path = _get_manifest_path(epic_id)
    if manifest_path.exists():
        raise ValidationError(
            f"Manifest already exists: {manifest_path}. "
            f"Use load_manifest() to load existing manifest."
        )
    
    # Create epic directory
    epic_dir = BRAIN_DIR / epic_id
    epic_dir.mkdir(parents=True, exist_ok=True)
    
    # Generate manifest
    now = datetime.now(timezone.utc).isoformat()
    
    manifest = {
        "epic_id": epic_id,
        "description": description,
        "status": "pending",
        "created_at": now,
        "updated_at": now,
        "phases": {
            "0": {
                "name": "Hotspot Analysis",
                "status": "pending",
                "mode": "ask",
                "started_at": None,
                "completed_at": None,
                "input_artifacts": [],
                "output_artifacts": []
            },
            "1": {
                "name": "Scope Definition",
                "status": "pending",
                "mode": "plan",
                "started_at": None,
                "completed_at": None,
                "input_artifacts": [],
                "output_artifacts": []
            },
            "1.5": {
                "name": "Scope Boundary",
                "status": "pending",
                "mode": "plan",
                "started_at": None,
                "completed_at": None,
                "input_artifacts": [],
                "output_artifacts": []
            },
            "2": {
                "name": "Architecture Planning",
                "status": "pending",
                "mode": "plan",
                "started_at": None,
                "completed_at": None,
                "input_artifacts": [],
                "output_artifacts": []
            },
            "3": {
                "name": "DNA & PR Audit",
                "status": "pending",
                "mode": "advanced",
                "started_at": None,
                "completed_at": None,
                "input_artifacts": [],
                "output_artifacts": []
            },
            "4": {
                "name": "Ticket Generation",
                "status": "pending",
                "mode": "plan",
                "started_at": None,
                "completed_at": None,
                "input_artifacts": [],
                "output_artifacts": []
            }
        },
        "dependencies": {
            "1": ["0"],
            "1.5": ["1"],
            "2": ["1.5"],
            "3": ["2"],
            "4": ["3"]
        },
        "parallel_groups": [],
        "metadata": {
            "total_tickets": ticket_count,
            "completed_tickets": 0,
            "failed_phases": [],
            "retry_count": 0,
            "estimated_duration_minutes": None,
            "actual_duration_minutes": None
        }
    }
    
    # Write manifest
    with open(manifest_path, 'w', encoding='utf-8') as f:
        json.dump(manifest, f, indent=2, ensure_ascii=True)
        f.write('\n')  # Add trailing newline
    
    # Add path to manifest for convenience
    manifest['_path'] = str(manifest_path)
    
    return manifest


def add_ticket_phases(
    epic_id: str,
    ticket_count: int,
    ticket_names: Optional[List[str]] = None
) -> None:
    """
    Add ticket execution and verification phases to manifest.
    
    Called after Phase 4 (Ticket Generation) completes.
    
    Args:
        epic_id: Epic identifier
        ticket_count: Number of tickets to add
        ticket_names: Optional list of ticket names (must match ticket_count)
        
    Raises:
        ValidationError: If ticket_count invalid or names mismatch
        
    Example:
        >>> add_ticket_phases(
        ...     "EPIC-CCN-15",
        ...     2,
        ...     ["Extract ValidateOrderState", "Extract CalculateRiskMetrics"]
        ... )
    """
    if ticket_count < 1:
        raise ValidationError(f"ticket_count must be >= 1, got {ticket_count}")
    
    if ticket_names and len(ticket_names) != ticket_count:
        raise ValidationError(
            f"ticket_names length ({len(ticket_names)}) must match "
            f"ticket_count ({ticket_count})"
        )
    
    manifest_path = _get_manifest_path(epic_id)
    
    with open(manifest_path, 'r+', encoding='utf-8') as f:
        # Acquire exclusive lock (platform-specific)
        if HAS_FCNTL:
            fcntl.flock(f.fileno(), fcntl.LOCK_EX)
        else:
            # Windows: lock first byte of file
            msvcrt.locking(f.fileno(), msvcrt.LK_LOCK, 1)
        
        try:
            manifest = json.load(f)
            
            # Add ticket phases
            for i in range(1, ticket_count + 1):
                ticket_id = f"5.{i}"
                verify_id = f"5.{i}.V"
                
                ticket_name = (
                    ticket_names[i-1] if ticket_names 
                    else f"Ticket {i}"
                )
                
                # Add ticket execution phase
                manifest['phases'][ticket_id] = {
                    "name": ticket_name,
                    "status": "pending",
                    "mode": "v12-engineer",
                    "started_at": None,
                    "completed_at": None,
                    "input_artifacts": [],
                    "output_artifacts": []
                }
                
                # Add verification phase
                manifest['phases'][verify_id] = {
                    "name": f"Verify {ticket_name}",
                    "status": "pending",
                    "mode": "advanced",
                    "started_at": None,
                    "completed_at": None,
                    "input_artifacts": [],
                    "output_artifacts": []
                }
                
                # Add dependencies
                manifest['dependencies'][ticket_id] = ["4"]
                manifest['dependencies'][verify_id] = [ticket_id]
            
            # Add Phase 6 (Final Review)
            manifest['phases']['6'] = {
                "name": "Final Review",
                "status": "pending",
                "mode": "advanced",
                "started_at": None,
                "completed_at": None,
                "input_artifacts": [],
                "output_artifacts": []
            }
            
            # Phase 6 depends on all verifications
            manifest['dependencies']['6'] = [
                f"5.{i}.V" for i in range(1, ticket_count + 1)
            ]
            
            # Add parallel groups
            if ticket_count > 1:
                manifest['parallel_groups'].append({
                    "group_id": "ticket_execution",
                    "phases": [f"5.{i}" for i in range(1, ticket_count + 1)],
                    "description": "Independent tickets can run concurrently"
                })
                
                manifest['parallel_groups'].append({
                    "group_id": "verification",
                    "phases": [f"5.{i}.V" for i in range(1, ticket_count + 1)],
                    "description": "Verifications can run concurrently"
                })
            
            # Update metadata
            manifest['metadata']['total_tickets'] = ticket_count
            manifest['updated_at'] = datetime.now(timezone.utc).isoformat()
            
            # Write updated manifest
            f.seek(0)
            f.truncate()
            json.dump(manifest, f, indent=2, ensure_ascii=True)
            f.write('\n')
            
        finally:
            fcntl.flock(f.fileno(), fcntl.LOCK_UN)

# ============================================================================
# V12.52 Lamport Causal Verification Functions
# ============================================================================

def verify_can_execute(epic_id: str, phase: str, agent_id: str = "unknown") -> Tuple[bool, str]:
    """
    V12.52 Blocking Gate: Verify phase can execute with deterministic guarantees.
    
    Checks:
    1. Dependencies satisfied (manifest)
    2. Workflow determinism (Lamport clocks)
    3. State consistency (manifest + filesystem)
    
    Args:
        epic_id: Epic identifier
        phase: Phase ID to check
        agent_id: Agent identifier for event logging
        
    Returns:
        (can_execute, reason) tuple
        
    Example:
        >>> can_execute, reason = verify_can_execute("EPIC-CCN-15", "1.5", "wave6-phase1-015")
        >>> if not can_execute:
        ...     print(f"BLOCKED: {reason}")
        ...     exit(1)
    """
    _validate_phase_id(phase)
    
    # 1. Check manifest dependencies
    try:
        if not validate_dependencies(epic_id, phase):
            return False, "Dependencies not satisfied (manifest)"
    except (ValidationError, DependencyError) as e:
        return False, f"Dependency validation failed: {e}"
    
    # 2. Check Lamport causal verification
    try:
        lamport_ok, lamport_reason = lamport_verify_can_execute(epic_id, phase)
        if not lamport_ok:
            return False, f"Causal verification failed: {lamport_reason}"
    except Exception as e:
        return False, f"Lamport verification error: {e}"
    
    # 3. Verify filesystem state matches manifest
    try:
        state_ok, state_reason = verify_filesystem_state(epic_id, phase)
        if not state_ok:
            return False, f"State mismatch: {state_reason}"
    except Exception as e:
        return False, f"State verification error: {e}"
    
    return True, "Ready to execute (all gates passed)"


def verify_filesystem_state(epic_id: str, phase: str) -> Tuple[bool, str]:
    """
    Verify filesystem state matches manifest expectations.
    
    Dual verification (manifest + filesystem):
    - Check expected input artifacts exist
    - Check no unexpected output artifacts present (stale state)
    - Verify file sizes are non-zero
    
    Args:
        epic_id: Epic identifier
        phase: Phase ID to check
        
    Returns:
        (state_ok, reason) tuple
    """
    try:
        manifest = load_manifest(epic_id)
    except FileNotFoundError:
        return False, "Manifest not found"
    
    if phase not in manifest['phases']:
        return False, f"Phase {phase} not in manifest"
    
    phase_data = manifest['phases'][phase]
    brain_dir = BRAIN_DIR / epic_id
    
    # Check input artifacts exist
    for artifact in phase_data.get('input_artifacts', []):
        artifact_path = Path(artifact)
        if not artifact_path.exists():
            return False, f"Input artifact missing: {artifact}"
        if artifact_path.stat().st_size == 0:
            return False, f"Input artifact empty: {artifact}"
    
    # Check for stale output artifacts (shouldn't exist yet)
    expected_outputs = phase_data.get('output_artifacts', [])
    if phase_data['status'] == 'pending' and expected_outputs:
        for artifact in expected_outputs:
            artifact_path = Path(artifact)
            if artifact_path.exists():
                return False, f"Stale output artifact detected: {artifact} (phase is pending but file exists)"
    
    # Check for unexpected files in brain directory
    if brain_dir.exists():
        # Get all markdown files
        existing_files = set(f.name for f in brain_dir.glob("*.md"))
        
        # Get expected files from all completed phases
        # If current phase is completed or in_progress, also include its outputs
        expected_files = set()
        for p_id, p_data in manifest['phases'].items():
            # Include files from completed phases
            if p_data['status'] == 'completed':
                for artifact in p_data.get('output_artifacts', []):
                    expected_files.add(Path(artifact).name)
            # Also include files from current phase if it's in_progress or completed
            elif p_id == phase and p_data['status'] in ('in_progress', 'completed'):
                for artifact in p_data.get('output_artifacts', []):
                    expected_files.add(Path(artifact).name)
        
        # Check for unexpected files
        unexpected = existing_files - expected_files
        if unexpected:
            # Filter out known files (manifest.json, etc.)
            unexpected = {f for f in unexpected if not f.startswith('manifest')}
            if unexpected:
                return False, f"Unexpected files in brain directory: {unexpected}"
    
    return True, "Filesystem state matches manifest"


def start_phase_execution(epic_id: str, phase: str, agent_id: str) -> Tuple[bool, str]:
    """
    Start phase execution with V12.52 verification and event logging.
    
    Workflow:
    1. Verify can execute (blocking gate)
    2. Record phase_start event (Lamport clock)
    3. Update manifest status to in_progress
    
    Args:
        epic_id: Epic identifier
        phase: Phase ID to start
        agent_id: Agent identifier
        
    Returns:
        (started, reason) tuple
        
    Example:
        >>> started, reason = start_phase_execution("EPIC-CCN-15", "1.5", "wave6-phase1-015")
        >>> if not started:
        ...     print(f"Failed to start: {reason}")
        ...     exit(1)
    """
    # 1. Verify can execute
    can_execute, reason = verify_can_execute(epic_id, phase, agent_id)
    if not can_execute:
        return False, f"Blocked: {reason}"
    
    # 2. Record phase_start event
    try:
        event = record_phase_start(epic_id, phase, agent_id)
    except Exception as e:
        return False, f"Failed to record phase_start event: {e}"
    
    # 3. Update manifest
    try:
        update_manifest(epic_id, phase, 'in_progress')
    except Exception as e:
        return False, f"Failed to update manifest: {e}"
    
    return True, f"Phase started (clock={event['clock']})"


def complete_phase_execution(
    epic_id: str,
    phase: str,
    agent_id: str,
    outputs: List[str],
    notes: Optional[str] = None
) -> Tuple[bool, str]:
    """
    Complete phase execution with V12.52 verification and event logging.
    
    Workflow:
    1. Verify outputs exist and are non-empty
    2. Record phase_complete event (Lamport clock)
    3. Update manifest status to completed
    
    Args:
        epic_id: Epic identifier
        phase: Phase ID to complete
        agent_id: Agent identifier
        outputs: List of output artifact paths
        notes: Optional completion notes
        
    Returns:
        (completed, reason) tuple
        
    Example:
        >>> completed, reason = complete_phase_execution(
        ...     "EPIC-CCN-15",
        ...     "1.5",
        ...     "wave6-phase1-015",
        ...     ["docs/brain/EPIC-CCN-15/01-scope-boundary.md"],
        ...     "Scope validated - single method only"
        ... )
    """
    # 1. Verify outputs exist
    for artifact in outputs:
        artifact_path = Path(artifact)
        if not artifact_path.exists():
            return False, f"Output artifact not found: {artifact}"
        if artifact_path.stat().st_size == 0:
            return False, f"Output artifact is empty: {artifact}"
    
    # 2. Record phase_complete event
    try:
        event = record_phase_complete(epic_id, phase, agent_id, {
            'outputs': outputs,
            'notes': notes
        })
    except Exception as e:
        return False, f"Failed to record phase_complete event: {e}"
    
    # 3. Update manifest
    try:
        update_manifest(epic_id, phase, 'completed', outputs=outputs, notes=notes)
    except Exception as e:
        return False, f"Failed to update manifest: {e}"
    
    return True, f"Phase completed (clock={event['clock']})"


def fail_phase_execution(
    epic_id: str,
    phase: str,
    agent_id: str,
    error: str
) -> Tuple[bool, str]:
    """
    Fail phase execution with V12.52 event logging.
    
    Workflow:
    1. Record phase_fail event (Lamport clock)
    2. Update manifest status to failed
    
    Args:
        epic_id: Epic identifier
        phase: Phase ID that failed
        agent_id: Agent identifier
        error: Error message
        
    Returns:
        (recorded, reason) tuple
    """
    # 1. Record phase_fail event
    try:
        event = record_phase_fail(epic_id, phase, agent_id, error)
    except Exception as e:
        return False, f"Failed to record phase_fail event: {e}"
    
    # 2. Update manifest
    try:
        update_manifest(epic_id, phase, 'failed', notes=f"Error: {error}")
    except Exception as e:
        return False, f"Failed to update manifest: {e}"
    
    return True, f"Phase failure recorded (clock={event['clock']})"


def get_event_log(epic_id: str, phase: Optional[str] = None) -> List[Dict]:
    """
    Get Lamport event log for an epic.
    
    Args:
        epic_id: Epic identifier
        phase: Optional phase filter
        
    Returns:
        List of events in causal order (sorted by clock)
        
    Example:
        >>> events = get_event_log("EPIC-CCN-15")
        >>> for event in events:
        ...     print(f"Clock {event['clock']}: {event['event_type']} - {event['phase']}")
    """
    workflow = get_workflow()
    return workflow.get_event_log(epic_id, phase)


def replay_workflow(epic_id: str) -> List[Dict]:
    """
    Replay workflow from event log (for debugging/recovery).
    
    Args:
        epic_id: Epic identifier
        
    Returns:
        List of events in causal order
        
    Example:
        >>> events = replay_workflow("EPIC-CCN-15")
        >>> print(f"Total events: {len(events)}")
        >>> print(f"Last event: {events[-1]['event_type']} at clock {events[-1]['clock']}")
    """
    workflow = get_workflow()
    return workflow.replay_workflow(epic_id)



if __name__ == '__main__':
    # CLI interface for testing
    import sys
    
    if len(sys.argv) < 2:
        print("Usage:")
        print("  python epic_manifest.py generate EPIC-ID 'Description'")
        print("  python epic_manifest.py load EPIC-ID")
        print("  python epic_manifest.py update EPIC-ID PHASE STATUS")
        print("  python epic_manifest.py validate EPIC-ID PHASE")
        print("  python epic_manifest.py next EPIC-ID")
        sys.exit(1)
    
    command = sys.argv[1]
    
    try:
        if command == 'generate':
            epic_id = sys.argv[2]
            description = sys.argv[3]
            manifest = generate_manifest(epic_id, description)
            print(f"Manifest created: {manifest['_path']}")
        
        elif command == 'load':
            epic_id = sys.argv[2]
            manifest = load_manifest(epic_id)
            print(json.dumps(manifest, indent=2))
        
        elif command == 'update':
            epic_id = sys.argv[2]
            phase = sys.argv[3]
            status = sys.argv[4]
            update_manifest(epic_id, phase, status)
            print(f"Updated {epic_id} phase {phase} to {status}")
        
        elif command == 'validate':
            epic_id = sys.argv[2]
            phase = sys.argv[3]
            result = validate_dependencies(epic_id, phase)
            print(f"Dependencies satisfied: {result}")
        
        elif command == 'next':
            epic_id = sys.argv[2]
            phases = get_next_phases(epic_id)
            print(f"Next phases: {phases}")
        
        else:
            print(f"Unknown command: {command}")
            sys.exit(1)
    
    except Exception as e:
        print(f"Error: {e}", file=sys.stderr)
        sys.exit(1)

# Made with Bob
