# Epic Workflow Testing Plan

**Version**: 1.0  
**Date**: 2026-06-09  
**Status**: Phase 1 Testing  
**Target**: Validate manifest-based workflow foundation

## Overview

This document outlines the testing strategy for the V12 Epic Workflow Refactoring. Testing is organized into three tiers:
1. **Unit Tests**: Test individual helper functions in isolation
2. **Integration Tests**: Test phase handoffs and workflow orchestration
3. **Pilot Test**: End-to-end validation with EPIC-CCN-16

## Test Environment Setup

### Prerequisites

```bash
# Python 3.8+ required
python --version

# Install test dependencies
pip install pytest pytest-cov

# Verify scripts directory accessible
ls scripts/epic_manifest.py

# Verify docs/brain directory exists
ls docs/brain/
```

### Test Data Setup

```bash
# Create test epic directory
mkdir -p docs/brain/EPIC-TEST-1

# Create sample artifacts for testing
echo "# Test Hotspots" > docs/brain/EPIC-TEST-1/00-hotspots.md
echo "# Test Scope" > docs/brain/EPIC-TEST-1/00-scope.md
```

## Unit Tests

### Test File: `tests/test_epic_manifest.py`

```python
"""
Unit tests for epic_manifest.py helper functions.

Run with: pytest tests/test_epic_manifest.py -v
"""

import pytest
import json
import os
import sys
from pathlib import Path
from datetime import datetime

# Add scripts to path
sys.path.insert(0, 'scripts')
from epic_manifest import (
    generate_manifest,
    load_manifest,
    update_manifest,
    validate_dependencies,
    get_next_phases,
    add_ticket_phases,
    ManifestError,
    ValidationError,
    DependencyError
)

# Test fixtures
@pytest.fixture
def test_epic_id():
    return "EPIC-TEST-1"

@pytest.fixture
def test_description():
    return "Test epic for unit testing"

@pytest.fixture
def cleanup_manifest(test_epic_id):
    """Clean up test manifest after test"""
    yield
    manifest_path = Path(f"docs/brain/{test_epic_id}/manifest.json")
    if manifest_path.exists():
        manifest_path.unlink()

@pytest.fixture
def sample_manifest(test_epic_id, test_description, cleanup_manifest):
    """Create a sample manifest for testing"""
    manifest = generate_manifest(test_epic_id, test_description)
    return manifest


class TestGenerateManifest:
    """Tests for generate_manifest function"""
    
    def test_generate_manifest_valid(self, test_epic_id, test_description, cleanup_manifest):
        """Test generating a valid manifest"""
        manifest = generate_manifest(test_epic_id, test_description)
        
        assert manifest['epic_id'] == test_epic_id
        assert manifest['description'] == test_description
        assert manifest['status'] == 'pending'
        assert 'created_at' in manifest
        assert 'phases' in manifest
        assert '0' in manifest['phases']
        assert '1' in manifest['phases']
        assert 'dependencies' in manifest
        
    def test_generate_manifest_creates_file(self, test_epic_id, test_description, cleanup_manifest):
        """Test that manifest file is created on disk"""
        manifest = generate_manifest(test_epic_id, test_description)
        manifest_path = Path(f"docs/brain/{test_epic_id}/manifest.json")
        
        assert manifest_path.exists()
        assert manifest_path.is_file()
        
    def test_generate_manifest_invalid_epic_id(self, cleanup_manifest):
        """Test that invalid epic_id raises error"""
        with pytest.raises(ValidationError, match="Invalid epic_id format"):
            generate_manifest("invalid-id", "Test description")
    
    def test_generate_manifest_description_too_short(self, cleanup_manifest):
        """Test that short description raises error"""
        with pytest.raises(ValidationError, match="Description must be 10-200 characters"):
            generate_manifest("EPIC-TEST-2", "Short")
    
    def test_generate_manifest_description_too_long(self, cleanup_manifest):
        """Test that long description raises error"""
        long_desc = "x" * 201
        with pytest.raises(ValidationError, match="Description must be 10-200 characters"):
            generate_manifest("EPIC-TEST-2", long_desc)
    
    def test_generate_manifest_already_exists(self, test_epic_id, test_description, sample_manifest):
        """Test that generating duplicate manifest raises error"""
        with pytest.raises(ValidationError, match="Manifest already exists"):
            generate_manifest(test_epic_id, test_description)
    
    def test_generate_manifest_with_ticket_count(self, cleanup_manifest):
        """Test generating manifest with ticket count"""
        manifest = generate_manifest("EPIC-TEST-3", "Test with tickets", ticket_count=5)
        
        assert manifest['metadata']['total_tickets'] == 5
        
        # Cleanup
        Path("docs/brain/EPIC-TEST-3/manifest.json").unlink()


class TestLoadManifest:
    """Tests for load_manifest function"""
    
    def test_load_manifest_valid(self, test_epic_id, sample_manifest):
        """Test loading a valid manifest"""
        manifest = load_manifest(test_epic_id)
        
        assert manifest['epic_id'] == test_epic_id
        assert 'phases' in manifest
        assert 'dependencies' in manifest
    
    def test_load_manifest_missing(self):
        """Test loading non-existent manifest raises error"""
        with pytest.raises(FileNotFoundError, match="Manifest not found"):
            load_manifest("EPIC-NONEXISTENT-999")
    
    def test_load_manifest_invalid_json(self, test_epic_id, cleanup_manifest):
        """Test loading corrupted manifest raises error"""
        # Create invalid JSON
        manifest_path = Path(f"docs/brain/{test_epic_id}")
        manifest_path.mkdir(parents=True, exist_ok=True)
        (manifest_path / "manifest.json").write_text("{ invalid json }")
        
        with pytest.raises(ValidationError, match="Invalid JSON"):
            load_manifest(test_epic_id)
    
    def test_load_manifest_missing_required_field(self, test_epic_id, cleanup_manifest):
        """Test loading manifest with missing field raises error"""
        # Create manifest missing required field
        manifest_path = Path(f"docs/brain/{test_epic_id}")
        manifest_path.mkdir(parents=True, exist_ok=True)
        (manifest_path / "manifest.json").write_text('{"epic_id": "EPIC-TEST-1"}')
        
        with pytest.raises(ValidationError, match="Missing required field"):
            load_manifest(test_epic_id)


class TestUpdateManifest:
    """Tests for update_manifest function"""
    
    def test_update_manifest_status_transition(self, test_epic_id, sample_manifest):
        """Test valid status transition"""
        update_manifest(test_epic_id, "0", "in_progress")
        
        manifest = load_manifest(test_epic_id)
        assert manifest['phases']['0']['status'] == 'in_progress'
        assert manifest['phases']['0']['started_at'] is not None
    
    def test_update_manifest_invalid_transition(self, test_epic_id, sample_manifest):
        """Test invalid status transition raises error"""
        # First complete the phase
        update_manifest(test_epic_id, "0", "in_progress")
        update_manifest(test_epic_id, "0", "completed", outputs=["docs/brain/EPIC-TEST-1/00-hotspots.md"])
        
        # Try to transition back to in_progress
        with pytest.raises(ValidationError, match="Invalid status transition"):
            update_manifest(test_epic_id, "0", "in_progress")
    
    def test_update_manifest_with_outputs(self, test_epic_id, sample_manifest):
        """Test updating with output artifacts"""
        update_manifest(test_epic_id, "0", "in_progress")
        update_manifest(
            test_epic_id, 
            "0", 
            "completed",
            outputs=["docs/brain/EPIC-TEST-1/00-hotspots.md"]
        )
        
        manifest = load_manifest(test_epic_id)
        assert manifest['phases']['0']['status'] == 'completed'
        assert manifest['phases']['0']['completed_at'] is not None
        assert "docs/brain/EPIC-TEST-1/00-hotspots.md" in manifest['phases']['0']['output_artifacts']
    
    def test_update_manifest_with_notes(self, test_epic_id, sample_manifest):
        """Test updating with notes"""
        update_manifest(
            test_epic_id,
            "0",
            "in_progress",
            notes="Starting hotspot analysis"
        )
        
        manifest = load_manifest(test_epic_id)
        assert manifest['phases']['0']['notes'] == "Starting hotspot analysis"
    
    def test_update_manifest_missing_artifact(self, test_epic_id, sample_manifest):
        """Test that completing with missing artifact raises error"""
        update_manifest(test_epic_id, "0", "in_progress")
        
        with pytest.raises(ValidationError, match="Output artifact not found"):
            update_manifest(
                test_epic_id,
                "0",
                "completed",
                outputs=["docs/brain/EPIC-TEST-1/nonexistent.md"]
            )
    
    def test_update_manifest_invalid_phase(self, test_epic_id, sample_manifest):
        """Test updating non-existent phase raises error"""
        with pytest.raises(ValidationError, match="Phase not found"):
            update_manifest(test_epic_id, "99", "in_progress")


class TestValidateDependencies:
    """Tests for validate_dependencies function"""
    
    def test_validate_dependencies_satisfied(self, test_epic_id, sample_manifest):
        """Test dependencies satisfied returns True"""
        # Complete Phase 0
        update_manifest(test_epic_id, "0", "in_progress")
        update_manifest(
            test_epic_id,
            "0",
            "completed",
            outputs=["docs/brain/EPIC-TEST-1/00-hotspots.md"]
        )
        
        # Phase 1 depends on Phase 0
        result = validate_dependencies(test_epic_id, "1")
        assert result is True
    
    def test_validate_dependencies_unsatisfied(self, test_epic_id, sample_manifest):
        """Test dependencies unsatisfied returns False"""
        # Phase 1 depends on Phase 0, which is still pending
        result = validate_dependencies(test_epic_id, "1")
        assert result is False
    
    def test_validate_dependencies_circular(self, test_epic_id, sample_manifest):
        """Test circular dependencies raises error"""
        # Manually create circular dependency
        manifest_path = Path(f"docs/brain/{test_epic_id}/manifest.json")
        with open(manifest_path, 'r') as f:
            manifest = json.load(f)
        
        # Create cycle: 1 -> 2 -> 1
        manifest['phases']['2'] = {
            "name": "Test Phase 2",
            "status": "pending",
            "mode": "plan",
            "started_at": None,
            "completed_at": None,
            "input_artifacts": [],
            "output_artifacts": []
        }
        manifest['dependencies']['2'] = ["1"]
        manifest['dependencies']['1'] = ["2"]
        
        with open(manifest_path, 'w') as f:
            json.dump(manifest, f)
        
        with pytest.raises(DependencyError, match="Circular dependency detected"):
            validate_dependencies(test_epic_id, "1")
    
    def test_validate_dependencies_invalid_phase(self, test_epic_id, sample_manifest):
        """Test validating non-existent phase raises error"""
        with pytest.raises(ValidationError, match="Phase not found"):
            validate_dependencies(test_epic_id, "99")


class TestGetNextPhases:
    """Tests for get_next_phases function"""
    
    def test_get_next_phases_empty(self, test_epic_id, sample_manifest):
        """Test no phases ready returns empty list"""
        # Phase 0 has no dependencies, so it should be ready
        # But we'll mark it as in_progress to test empty case
        update_manifest(test_epic_id, "0", "in_progress")
        
        next_phases = get_next_phases(test_epic_id)
        assert next_phases == []
    
    def test_get_next_phases_single(self, test_epic_id, sample_manifest):
        """Test single phase ready"""
        # Phase 0 has no dependencies
        next_phases = get_next_phases(test_epic_id)
        assert "0" in next_phases
    
    def test_get_next_phases_multiple(self, test_epic_id, sample_manifest):
        """Test multiple phases ready (parallel execution)"""
        # Complete Phase 0
        update_manifest(test_epic_id, "0", "in_progress")
        update_manifest(
            test_epic_id,
            "0",
            "completed",
            outputs=["docs/brain/EPIC-TEST-1/00-hotspots.md"]
        )
        
        # Complete Phase 1
        update_manifest(test_epic_id, "1", "in_progress")
        update_manifest(
            test_epic_id,
            "1",
            "completed",
            outputs=["docs/brain/EPIC-TEST-1/00-scope.md"]
        )
        
        # Complete Phase 1.5
        update_manifest(test_epic_id, "1.5", "in_progress")
        update_manifest(
            test_epic_id,
            "1.5",
            "completed",
            outputs=["docs/brain/EPIC-TEST-1/01-scope-boundary.md"]
        )
        
        # Complete Phase 2
        update_manifest(test_epic_id, "2", "in_progress")
        update_manifest(
            test_epic_id,
            "2",
            "completed",
            outputs=["docs/brain/EPIC-TEST-1/02-architecture-plan.md"]
        )
        
        # Complete Phase 3
        update_manifest(test_epic_id, "3", "in_progress")
        update_manifest(
            test_epic_id,
            "3",
            "completed",
            outputs=["docs/brain/EPIC-TEST-1/03-audit-report.md"]
        )
        
        # Complete Phase 4
        update_manifest(test_epic_id, "4", "in_progress")
        update_manifest(
            test_epic_id,
            "4",
            "completed",
            outputs=["docs/brain/EPIC-TEST-1/04-tickets.md"]
        )
        
        # Add ticket phases
        add_ticket_phases(test_epic_id, 2)
        
        # Now Phase 5.1 and 5.2 should be ready (parallel)
        next_phases = get_next_phases(test_epic_id)
        assert "5.1" in next_phases
        assert "5.2" in next_phases


class TestAddTicketPhases:
    """Tests for add_ticket_phases function"""
    
    def test_add_ticket_phases_valid(self, test_epic_id, sample_manifest):
        """Test adding ticket phases"""
        add_ticket_phases(test_epic_id, 3)
        
        manifest = load_manifest(test_epic_id)
        assert "5.1" in manifest['phases']
        assert "5.2" in manifest['phases']
        assert "5.3" in manifest['phases']
        assert "5.1.V" in manifest['phases']
        assert "5.2.V" in manifest['phases']
        assert "5.3.V" in manifest['phases']
        assert "6" in manifest['phases']
    
    def test_add_ticket_phases_with_names(self, test_epic_id, sample_manifest):
        """Test adding ticket phases with custom names"""
        ticket_names = ["Extract ValidateOrder", "Extract CalculateRisk"]
        add_ticket_phases(test_epic_id, 2, ticket_names)
        
        manifest = load_manifest(test_epic_id)
        assert manifest['phases']['5.1']['name'] == "Extract ValidateOrder"
        assert manifest['phases']['5.2']['name'] == "Extract CalculateRisk"
    
    def test_add_ticket_phases_invalid_count(self, test_epic_id, sample_manifest):
        """Test adding zero tickets raises error"""
        with pytest.raises(ValidationError, match="ticket_count must be >= 1"):
            add_ticket_phases(test_epic_id, 0)
    
    def test_add_ticket_phases_name_mismatch(self, test_epic_id, sample_manifest):
        """Test name count mismatch raises error"""
        with pytest.raises(ValidationError, match="ticket_names length"):
            add_ticket_phases(test_epic_id, 3, ["Name1", "Name2"])


# Run tests
if __name__ == '__main__':
    pytest.main([__file__, '-v', '--cov=epic_manifest', '--cov-report=term-missing'])
```

## Integration Tests

### Test File: `tests/test_epic_workflow_integration.py`

```python
"""
Integration tests for epic workflow phase handoffs.

Run with: pytest tests/test_epic_workflow_integration.py -v
"""

import pytest
import sys
from pathlib import Path

sys.path.insert(0, 'scripts')
from epic_manifest import (
    generate_manifest,
    load_manifest,
    update_manifest,
    validate_dependencies,
    get_next_phases,
    add_ticket_phases
)


@pytest.fixture
def integration_epic_id():
    return "EPIC-INT-1"

@pytest.fixture
def cleanup_integration(integration_epic_id):
    """Clean up integration test artifacts"""
    yield
    epic_dir = Path(f"docs/brain/{integration_epic_id}")
    if epic_dir.exists():
        for file in epic_dir.glob("*"):
            file.unlink()
        epic_dir.rmdir()


class TestPhaseHandoffs:
    """Test artifact handoff between phases"""
    
    def test_phase_0_to_phase_1_handoff(self, integration_epic_id, cleanup_integration):
        """Test Phase 0 -> Phase 1 artifact handoff"""
        # Generate manifest
        manifest = generate_manifest(integration_epic_id, "Integration test epic")
        
        # Execute Phase 0
        update_manifest(integration_epic_id, "0", "in_progress")
        
        # Create Phase 0 output
        hotspots_path = Path(f"docs/brain/{integration_epic_id}/00-hotspots.md")
        hotspots_path.write_text("# Hotspots\n- Method1: CYC 25\n")
        
        update_manifest(
            integration_epic_id,
            "0",
            "completed",
            outputs=[str(hotspots_path)],
            notes="Found 1 hotspot"
        )
        
        # Verify Phase 1 can start
        assert validate_dependencies(integration_epic_id, "1")
        next_phases = get_next_phases(integration_epic_id)
        assert "1" in next_phases
        
        # Execute Phase 1
        update_manifest(integration_epic_id, "1", "in_progress")
        
        # Phase 1 should read Phase 0 output
        manifest = load_manifest(integration_epic_id)
        phase_0_outputs = manifest['phases']['0']['output_artifacts']
        assert str(hotspots_path) in phase_0_outputs
        
        # Create Phase 1 output
        scope_path = Path(f"docs/brain/{integration_epic_id}/00-scope.md")
        scope_path.write_text("# Scope\n- File: test.cs\n- Method: Method1\n")
        
        update_manifest(
            integration_epic_id,
            "1",
            "completed",
            outputs=[str(scope_path)],
            notes="Scope defined"
        )
        
        # Verify handoff successful
        manifest = load_manifest(integration_epic_id)
        assert manifest['phases']['0']['status'] == 'completed'
        assert manifest['phases']['1']['status'] == 'completed'
        assert str(scope_path) in manifest['phases']['1']['output_artifacts']
    
    def test_phase_1_to_phase_1_5_handoff(self, integration_epic_id, cleanup_integration):
        """Test Phase 1 -> Phase 1.5 artifact handoff"""
        # Setup: Complete Phase 0 and Phase 1
        generate_manifest(integration_epic_id, "Integration test epic")
        
        # Phase 0
        update_manifest(integration_epic_id, "0", "in_progress")
        hotspots_path = Path(f"docs/brain/{integration_epic_id}/00-hotspots.md")
        hotspots_path.write_text("# Hotspots\n")
        update_manifest(integration_epic_id, "0", "completed", outputs=[str(hotspots_path)])
        
        # Phase 1
        update_manifest(integration_epic_id, "1", "in_progress")
        scope_path = Path(f"docs/brain/{integration_epic_id}/00-scope.md")
        scope_path.write_text("# Scope\n")
        update_manifest(integration_epic_id, "1", "completed", outputs=[str(scope_path)])
        
        # Test Phase 1.5
        assert validate_dependencies(integration_epic_id, "1.5")
        update_manifest(integration_epic_id, "1.5", "in_progress")
        
        # Phase 1.5 should read Phase 1 output
        manifest = load_manifest(integration_epic_id)
        assert str(scope_path) in manifest['phases']['1']['output_artifacts']
        
        # Complete Phase 1.5
        boundary_path = Path(f"docs/brain/{integration_epic_id}/01-scope-boundary.md")
        boundary_path.write_text("# Scope Boundary\n")
        update_manifest(integration_epic_id, "1.5", "completed", outputs=[str(boundary_path)])
        
        # Verify
        manifest = load_manifest(integration_epic_id)
        assert manifest['phases']['1.5']['status'] == 'completed'


class TestParallelExecution:
    """Test parallel phase execution"""
    
    def test_parallel_ticket_execution(self, integration_epic_id, cleanup_integration):
        """Test multiple tickets can execute in parallel"""
        # Setup: Complete phases up to Phase 4
        generate_manifest(integration_epic_id, "Parallel execution test")
        
        # Complete Phase 0-4
        for phase in ["0", "1", "1.5", "2", "3", "4"]:
            update_manifest(integration_epic_id, phase, "in_progress")
            artifact_path = Path(f"docs/brain/{integration_epic_id}/phase-{phase}.md")
            artifact_path.write_text(f"# Phase {phase}\n")
            update_manifest(integration_epic_id, phase, "completed", outputs=[str(artifact_path)])
        
        # Add ticket phases
        add_ticket_phases(integration_epic_id, 3)
        
        # Verify all tickets ready simultaneously
        next_phases = get_next_phases(integration_epic_id)
        assert "5.1" in next_phases
        assert "5.2" in next_phases
        assert "5.3" in next_phases
        
        # Start all tickets
        for ticket in ["5.1", "5.2", "5.3"]:
            update_manifest(integration_epic_id, ticket, "in_progress")
        
        # Verify all in progress
        manifest = load_manifest(integration_epic_id)
        assert manifest['phases']['5.1']['status'] == 'in_progress'
        assert manifest['phases']['5.2']['status'] == 'in_progress'
        assert manifest['phases']['5.3']['status'] == 'in_progress'


class TestFailureRecovery:
    """Test recovery from failed phases"""
    
    def test_failed_phase_recovery(self, integration_epic_id, cleanup_integration):
        """Test retrying a failed phase"""
        generate_manifest(integration_epic_id, "Failure recovery test")
        
        # Start Phase 0
        update_manifest(integration_epic_id, "0", "in_progress")
        
        # Fail Phase 0
        update_manifest(
            integration_epic_id,
            "0",
            "failed",
            notes="Build error: CS0103"
        )
        
        # Verify epic status is failed
        manifest = load_manifest(integration_epic_id)
        assert manifest['status'] == 'failed'
        assert "0" in manifest['metadata']['failed_phases']
        
        # Retry Phase 0
        update_manifest(integration_epic_id, "0", "in_progress")
        
        # Complete Phase 0
        hotspots_path = Path(f"docs/brain/{integration_epic_id}/00-hotspots.md")
        hotspots_path.write_text("# Hotspots\n")
        update_manifest(integration_epic_id, "0", "completed", outputs=[str(hotspots_path)])
        
        # Verify recovery
        manifest = load_manifest(integration_epic_id)
        assert manifest['phases']['0']['status'] == 'completed'
        assert manifest['metadata']['retry_count'] == 0  # Not tracking retries yet


class TestEndToEnd:
    """End-to-end workflow tests"""
    
    def test_end_to_end_epic_ccn_16(self, cleanup_integration):
        """Test complete epic workflow (simplified EPIC-CCN-16)"""
        epic_id = "EPIC-CCN-16"
        
        # Phase 0: Generate manifest
        manifest = generate_manifest(epic_id, "Extract CalculatePositionSize complexity")
        assert manifest['epic_id'] == epic_id
        
        # Phase 0: Hotspot analysis
        update_manifest(epic_id, "0", "in_progress")
        hotspots_path = Path(f"docs/brain/{epic_id}/00-hotspots.md")
        hotspots_path.write_text("# Hotspots\n- CalculatePositionSize: CYC 22\n")
        update_manifest(epic_id, "0", "completed", outputs=[str(hotspots_path)])
        
        # Phase 1: Scope definition
        assert validate_dependencies(epic_id, "1")
        update_manifest(epic_id, "1", "in_progress")
        scope_path = Path(f"docs/brain/{epic_id}/00-scope.md")
        scope_path.write_text("# Scope\n- File: V12_002.cs\n- Method: CalculatePositionSize\n")
        update_manifest(epic_id, "1", "completed", outputs=[str(scope_path)])
        
        # Phase 1.5: Scope boundary
        assert validate_dependencies(epic_id, "1.5")
        update_manifest(epic_id, "1.5", "in_progress")
        boundary_path = Path(f"docs/brain/{epic_id}/01-scope-boundary.md")
        boundary_path.write_text("# Scope Boundary\n- IN: CalculatePositionSize\n- OUT: Other methods\n")
        update_manifest(epic_id, "1.5", "completed", outputs=[str(boundary_path)])
        
        # Verify workflow progressing
        manifest = load_manifest(epic_id)
        assert manifest['phases']['0']['status'] == 'completed'
        assert manifest['phases']['1']['status'] == 'completed'
        assert manifest['phases']['1.5']['status'] == 'completed'
        assert manifest['status'] == 'in_progress'
        
        # Cleanup
        epic_dir = Path(f"docs/brain/{epic_id}")
        for file in epic_dir.glob("*"):
            file.unlink()
        epic_dir.rmdir()


# Run tests
if __name__ == '__main__':
    pytest.main([__file__, '-v'])
```

## Pilot Test: EPIC-CCN-16

### Objective
Validate Phase 1 implementation with a real epic workflow.

### Prerequisites

```bash
# Ensure EPIC-CCN-16 directory doesn't exist
rm -rf docs/brain/EPIC-CCN-16

# Verify epic_manifest.py is executable
python scripts/epic_manifest.py --help
```

### Test Steps

#### Step 1: Generate Manifest

```bash
# Run epic-intake command
bob /epic-intake EPIC-CCN-16 "Extract CalculatePositionSize complexity"
```

**Expected Output**:
- `docs/brain/EPIC-CCN-16/manifest.json` created
- Manifest contains Phase 0 and Phase 1 definitions
- Phase 0 status: `in_progress`

**Verification**:
```python
from epic_manifest import load_manifest
import json

manifest = load_manifest("EPIC-CCN-16")
print(json.dumps(manifest, indent=2))

# Check required fields
assert manifest['epic_id'] == "EPIC-CCN-16"
assert manifest['status'] == 'in_progress'
assert '0' in manifest['phases']
assert '1' in manifest['phases']
```

#### Step 2: Complete Phase 0

**Expected Output**:
- `docs/brain/EPIC-CCN-16/00-hotspots.md` created
- Phase 0 status updated to `completed`
- Phase 0 `completed_at` timestamp set

**Verification**:
```python
manifest = load_manifest("EPIC-CCN-16")
assert manifest['phases']['0']['status'] == 'completed'
assert manifest['phases']['0']['completed_at'] is not None
assert "docs/brain/EPIC-CCN-16/00-hotspots.md" in manifest['phases']['0']['output_artifacts']
```

#### Step 3: Verify Phase 1 Dependencies

```python
from epic_manifest import validate_dependencies, get_next_phases

# Check Phase 1 dependencies satisfied
assert validate_dependencies("EPIC-CCN-16", "1") == True

# Check Phase 1 is next
next_phases = get_next_phases("EPIC-CCN-16")
assert "1" in next_phases
```

#### Step 4: Complete Phase 1

**Expected Output**:
- `docs/brain/EPIC-CCN-16/00-scope.md` created
- Phase 1 status updated to `completed`
- Phase 1 reads Phase 0 outputs from manifest

**Verification**:
```python
manifest = load_manifest("EPIC-CCN-16")
assert manifest['phases']['1']['status'] == 'completed'
assert "docs/brain/EPIC-CCN-16/00-scope.md" in manifest['phases']['1']['output_artifacts']

# Verify Phase 1 input references Phase 0 output
phase_0_output = manifest['phases']['0']['output_artifacts'][0]
assert phase_0_output in manifest['phases']['1']['input_artifacts'] or \
       "00-hotspots.md" in str(manifest['phases']['1'])
```

#### Step 5: Verify Phase 1.5 Ready

```python
# Check Phase 1.5 dependencies satisfied
assert validate_dependencies("EPIC-CCN-16", "1.5") == True

# Check Phase 1.5 is next
next_phases = get_next_phases("EPIC-CCN-16")
assert "1.5" in next_phases
```

### Success Criteria

- ✅ Manifest generated correctly
- ✅ Phase 0 completed and updated manifest
- ✅ Phase 1 read Phase 0 outputs from manifest
- ✅ Phase 1 completed and updated manifest
- ✅ Phase 1.5 dependencies satisfied
- ✅ No manual file path editing required
- ✅ All artifacts exist on disk
- ✅ Timestamps are valid and ordered

### Failure Scenarios

| Scenario | Expected Behavior | Recovery |
|----------|-------------------|----------|
| Manifest generation fails | Error message with details | Fix epic_id format or description |
| Phase 0 fails | Status set to `failed`, error in manifest | Retry Phase 0 after fixing issue |
| Artifact missing | Error on status update to `completed` | Create artifact before completing |
| Invalid status transition | Error with allowed transitions | Check current status, use valid transition |
| Circular dependency | Error on validation | Fix dependency graph in manifest |

## Test Execution

### Running Unit Tests

```bash
# Run all unit tests
pytest tests/test_epic_manifest.py -v

# Run with coverage
pytest tests/test_epic_manifest.py -v --cov=epic_manifest --cov-report=html

# Run specific test class
pytest tests/test_epic_manifest.py::TestGenerateManifest -v

# Run specific test
pytest tests/test_epic_manifest.py::TestGenerateManifest::test_generate_manifest_valid -v
```

### Running Integration Tests

```bash
# Run all integration tests
pytest tests/test_epic_workflow_integration.py -v

# Run specific test class
pytest tests/test_epic_workflow_integration.py::TestPhaseHandoffs -v
```

### Running Pilot Test

```bash
# Manual execution
bob /epic-intake EPIC-CCN-16 "Extract CalculatePositionSize complexity"

# Verify with Python
python -c "
from epic_manifest import load_manifest
import json
manifest = load_manifest('EPIC-CCN-16')
print(json.dumps(manifest, indent=2))
"
```

## Test Metrics

### Coverage Targets

| Component | Target Coverage | Current |
|-----------|----------------|---------|
| `generate_manifest` | 100% | TBD |
| `load_manifest` | 100% | TBD |
| `update_manifest` | 100% | TBD |
| `validate_dependencies` | 100% | TBD |
| `get_next_phases` | 100% | TBD |
| `add_ticket_phases` | 100% | TBD |
| **Overall** | **95%+** | **TBD** |

### Performance Targets

| Operation | Target Time | Acceptable |
|-----------|-------------|------------|
| `generate_manifest` | < 100ms | < 500ms |
| `load_manifest` | < 50ms | < 200ms |
| `update_manifest` | < 100ms | < 500ms |
| `validate_dependencies` | < 50ms | < 200ms |
| `get_next_phases` | < 100ms | < 500ms |

## Test Reporting

### Test Report Template

```markdown
# Epic Workflow Test Report

**Date**: YYYY-MM-DD
**Tester**: [Name]
**Phase**: Phase 1 - Foundation

## Unit Tests
- Total: X
- Passed: Y
- Failed: Z
- Coverage: XX%

## Integration Tests
- Total: X
- Passed: Y
- Failed: Z

## Pilot Test (EPIC-CCN-16)
- Status: [Pass/Fail]
- Issues Found: X
- Time Taken: XX minutes

## Issues
1. [Issue description]
   - Severity: [Critical/High/Medium/Low]
   - Status: [Open/Fixed]

## Recommendations
1. [Recommendation]

## Sign-off
- [ ] All unit tests pass
- [ ] All integration tests pass
- [ ] Pilot test successful
- [ ] Coverage >= 95%
- [ ] No critical issues
```

## Next Steps

After Phase 1 testing completes:

1. **Phase 2 Testing** (Week 3-4):
   - Test all phase commands with manifest integration
   - Test Phase 2-6 artifact handoffs
   - Test ticket execution workflow

2. **Phase 3 Testing** (Week 5-6):
   - Test orchestrator command
   - Test parallel execution
   - Test failure recovery
   - Load testing with multiple epics

3. **Phase 4 Testing** (Week 7-8):
   - Test Watsonx Orchestrate integration
   - Test webhook endpoints
   - Test authentication
   - End-to-end integration test

## References

- **Design Document**: `docs/workflow/V12_EPIC_WORKFLOW_REFACTORING_DESIGN.md`
- **Manifest Schema**: `docs/workflow/EPIC_MANIFEST_SCHEMA.md`
- **Helper Functions**: `scripts/epic_manifest.py`
- **pytest Documentation**: https://docs.pytest.org/

---

**Test Plan Status**: Active  
**Next Review**: After Phase 1 pilot completion  
**Maintainer**: V12 Architecture Team