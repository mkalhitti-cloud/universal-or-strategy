# Epic Manifest Schema

**Version**: 1.0  
**Date**: 2026-06-09  
**Status**: Active

## Overview

The epic manifest (`manifest.json`) is the central state management file for V12 epic workflows. It tracks phase execution, dependencies, artifacts, and metadata for the entire epic lifecycle.

## JSON Schema Definition

```json
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "title": "V12 Epic Manifest",
  "type": "object",
  "required": ["epic_id", "description", "status", "created_at", "phases", "dependencies"],
  "properties": {
    "epic_id": {
      "type": "string",
      "pattern": "^EPIC-[A-Z]+-\\d+$",
      "description": "Unique epic identifier (e.g., EPIC-CCN-15)",
      "examples": ["EPIC-CCN-15", "EPIC-FSM-3", "EPIC-SIMA-7"]
    },
    "description": {
      "type": "string",
      "minLength": 10,
      "maxLength": 200,
      "description": "Brief description of epic objective"
    },
    "status": {
      "type": "string",
      "enum": ["pending", "in_progress", "completed", "failed", "blocked"],
      "description": "Overall epic status"
    },
    "created_at": {
      "type": "string",
      "format": "date-time",
      "description": "ISO 8601 timestamp of epic creation"
    },
    "updated_at": {
      "type": "string",
      "format": "date-time",
      "description": "ISO 8601 timestamp of last update"
    },
    "phases": {
      "type": "object",
      "description": "Map of phase ID to phase details",
      "patternProperties": {
        "^\\d+(\\.\\d+)?(\\.V)?$": {
          "$ref": "#/definitions/phase"
        }
      }
    },
    "dependencies": {
      "type": "object",
      "description": "Map of phase ID to array of dependency phase IDs",
      "patternProperties": {
        "^\\d+(\\.\\d+)?(\\.V)?$": {
          "type": "array",
          "items": {
            "type": "string",
            "pattern": "^\\d+(\\.\\d+)?(\\.V)?$"
          }
        }
      }
    },
    "parallel_groups": {
      "type": "array",
      "description": "Groups of phases that can execute concurrently",
      "items": {
        "$ref": "#/definitions/parallel_group"
      }
    },
    "metadata": {
      "$ref": "#/definitions/metadata"
    }
  },
  "definitions": {
    "phase": {
      "type": "object",
      "required": ["name", "status", "mode"],
      "properties": {
        "name": {
          "type": "string",
          "description": "Human-readable phase name"
        },
        "status": {
          "type": "string",
          "enum": ["pending", "in_progress", "completed", "failed", "blocked", "skipped"],
          "description": "Current phase status"
        },
        "mode": {
          "type": "string",
          "enum": ["ask", "plan", "advanced", "v12-engineer"],
          "description": "Bob mode to use for this phase"
        },
        "started_at": {
          "type": ["string", "null"],
          "format": "date-time",
          "description": "ISO 8601 timestamp when phase started"
        },
        "completed_at": {
          "type": ["string", "null"],
          "format": "date-time",
          "description": "ISO 8601 timestamp when phase completed"
        },
        "input_artifacts": {
          "type": "array",
          "items": {
            "type": "string"
          },
          "description": "Paths to input artifacts (relative to repo root)"
        },
        "output_artifacts": {
          "type": "array",
          "items": {
            "type": "string"
          },
          "description": "Paths to output artifacts (relative to repo root)"
        },
        "notes": {
          "type": "string",
          "description": "Optional notes about phase execution"
        },
        "error": {
          "type": "string",
          "description": "Error message if phase failed"
        }
      }
    },
    "parallel_group": {
      "type": "object",
      "required": ["group_id", "phases"],
      "properties": {
        "group_id": {
          "type": "string",
          "description": "Unique identifier for parallel group"
        },
        "phases": {
          "type": "array",
          "items": {
            "type": "string",
            "pattern": "^\\d+(\\.\\d+)?(\\.V)?$"
          },
          "minItems": 2,
          "description": "Phase IDs that can run concurrently"
        },
        "description": {
          "type": "string",
          "description": "Explanation of why phases can run in parallel"
        }
      }
    },
    "metadata": {
      "type": "object",
      "properties": {
        "total_tickets": {
          "type": "integer",
          "minimum": 0,
          "description": "Total number of tickets in epic"
        },
        "completed_tickets": {
          "type": "integer",
          "minimum": 0,
          "description": "Number of completed tickets"
        },
        "failed_phases": {
          "type": "array",
          "items": {
            "type": "string"
          },
          "description": "List of phase IDs that failed"
        },
        "retry_count": {
          "type": "integer",
          "minimum": 0,
          "description": "Number of times phases have been retried"
        },
        "estimated_duration_minutes": {
          "type": ["integer", "null"],
          "minimum": 0,
          "description": "Estimated total duration in minutes"
        },
        "actual_duration_minutes": {
          "type": ["integer", "null"],
          "minimum": 0,
          "description": "Actual total duration in minutes"
        }
      }
    }
  }
}
```

## Field Descriptions

### Root Level Fields

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `epic_id` | string | Yes | Unique identifier matching pattern `EPIC-{CATEGORY}-{NUMBER}` |
| `description` | string | Yes | Brief objective (10-200 chars) |
| `status` | enum | Yes | Overall epic status (see Status Values) |
| `created_at` | datetime | Yes | ISO 8601 timestamp of creation |
| `updated_at` | datetime | No | ISO 8601 timestamp of last update |
| `phases` | object | Yes | Map of phase ID to phase details |
| `dependencies` | object | Yes | Map of phase ID to dependency array |
| `parallel_groups` | array | No | Groups of phases that can run concurrently |
| `metadata` | object | No | Additional epic metadata |

### Phase Object Fields

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `name` | string | Yes | Human-readable phase name |
| `status` | enum | Yes | Phase status (see Status Values) |
| `mode` | enum | Yes | Bob mode: `ask`, `plan`, `advanced`, `v12-engineer` |
| `started_at` | datetime | No | ISO 8601 timestamp when started |
| `completed_at` | datetime | No | ISO 8601 timestamp when completed |
| `input_artifacts` | array | No | Paths to input files (relative to repo root) |
| `output_artifacts` | array | No | Paths to output files (relative to repo root) |
| `notes` | string | No | Optional execution notes |
| `error` | string | No | Error message if failed |

### Parallel Group Fields

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `group_id` | string | Yes | Unique group identifier |
| `phases` | array | Yes | Phase IDs that can run concurrently (min 2) |
| `description` | string | No | Explanation of parallelization |

### Metadata Fields

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `total_tickets` | integer | No | Total number of tickets |
| `completed_tickets` | integer | No | Number of completed tickets |
| `failed_phases` | array | No | List of failed phase IDs |
| `retry_count` | integer | No | Number of retry attempts |
| `estimated_duration_minutes` | integer | No | Estimated duration |
| `actual_duration_minutes` | integer | No | Actual duration |

## Status Values

### Epic Status

| Status | Description | Transitions To |
|--------|-------------|----------------|
| `pending` | Epic created, not started | `in_progress` |
| `in_progress` | At least one phase executing | `completed`, `failed`, `blocked` |
| `completed` | All phases completed successfully | N/A (terminal) |
| `failed` | One or more phases failed | `in_progress` (after retry) |
| `blocked` | Waiting on external dependency | `in_progress` |

### Phase Status

| Status | Description | Transitions To |
|--------|-------------|----------------|
| `pending` | Phase not started | `in_progress`, `blocked`, `skipped` |
| `in_progress` | Phase currently executing | `completed`, `failed` |
| `completed` | Phase finished successfully | N/A (terminal) |
| `failed` | Phase encountered error | `in_progress` (after retry) |
| `blocked` | Dependencies not satisfied | `in_progress` |
| `skipped` | Phase intentionally skipped | N/A (terminal) |

## Validation Rules

### 1. Phase ID Format

**Pattern**: `^\d+(\.\d+)?(\\.V)?$`

**Valid Examples**:
- `0` - Phase 0
- `1` - Phase 1
- `1.5` - Phase 1.5 (Scope Boundary)
- `2.3` - Phase 2.3 (Test Strategy)
- `5.1` - Phase 5.1 (Ticket 1)
- `5.1.V` - Phase 5.1.V (Verify Ticket 1)

**Invalid Examples**:
- `phase-1` - Must be numeric
- `1.5.2` - Too many decimal points
- `5.1.VV` - Invalid verification suffix

### 2. Status Transitions

**Valid Transitions**:
```
pending -> in_progress -> completed
pending -> in_progress -> failed
pending -> blocked -> in_progress
pending -> skipped
failed -> in_progress (retry)
```

**Invalid Transitions**:
```
completed -> in_progress (cannot restart completed phase)
skipped -> in_progress (cannot unskip)
completed -> failed (cannot fail after completion)
```

### 3. Dependency Validation

**Rules**:
- All dependency phase IDs must exist in `phases` object
- No circular dependencies allowed
- Dependencies must be completed before dependent phase starts
- Verification phases (`.V`) must depend on their ticket phase

**Example Valid Dependencies**:
```json
{
  "dependencies": {
    "1": ["0"],
    "1.5": ["1"],
    "2": ["1.5"],
    "5.1": ["4"],
    "5.1.V": ["5.1"]
  }
}
```

**Example Invalid Dependencies**:
```json
{
  "dependencies": {
    "1": ["2"],  // Circular: 1 -> 2 -> 1.5 -> 1
    "2": ["1.5"],
    "1.5": ["1"]
  }
}
```

### 4. Artifact Validation

**Rules**:
- Output artifacts must exist on disk when phase status is `completed`
- Output artifacts must be non-empty files
- Input artifacts must be in previous phase outputs
- Artifact paths must be relative to repo root
- Artifact paths must start with `docs/brain/{epic_id}/`

**Example Valid Artifacts**:
```json
{
  "input_artifacts": [
    "docs/brain/EPIC-CCN-15/00-hotspots.md"
  ],
  "output_artifacts": [
    "docs/brain/EPIC-CCN-15/00-scope.md"
  ]
}
```

**Example Invalid Artifacts**:
```json
{
  "input_artifacts": [
    "/absolute/path/file.md",  // Must be relative
    "docs/other/file.md"       // Must be in epic directory
  ]
}
```

### 5. Timestamp Validation

**Rules**:
- `started_at` must be after `created_at`
- `completed_at` must be after `started_at`
- `updated_at` must be the latest timestamp in manifest
- All timestamps must be valid ISO 8601 format
- All timestamps must be in UTC timezone

**Example Valid Timestamps**:
```json
{
  "created_at": "2026-06-09T04:00:00Z",
  "started_at": "2026-06-09T04:05:00Z",
  "completed_at": "2026-06-09T04:15:00Z",
  "updated_at": "2026-06-09T04:15:00Z"
}
```

### 6. Parallel Group Validation

**Rules**:
- Each group must have at least 2 phases
- Phases in a group must have no dependencies on each other
- Phases in a group must have same dependency depth
- Group IDs must be unique

**Example Valid Parallel Group**:
```json
{
  "parallel_groups": [
    {
      "group_id": "ticket_execution",
      "phases": ["5.1", "5.2", "5.3"],
      "description": "Independent tickets"
    }
  ],
  "dependencies": {
    "5.1": ["4"],
    "5.2": ["4"],
    "5.3": ["4"]
  }
}
```

**Example Invalid Parallel Group**:
```json
{
  "parallel_groups": [
    {
      "group_id": "invalid",
      "phases": ["5.1", "5.2"],  // Invalid: 5.2 depends on 5.1
      "description": "Sequential tickets"
    }
  ],
  "dependencies": {
    "5.1": ["4"],
    "5.2": ["5.1"]  // Dependency within group
  }
}
```

## Example Manifests

### Minimal Manifest (New Epic)

```json
{
  "epic_id": "EPIC-CCN-16",
  "description": "Extract CalculatePositionSize complexity",
  "status": "pending",
  "created_at": "2026-06-09T05:00:00Z",
  "updated_at": "2026-06-09T05:00:00Z",
  "phases": {
    "0": {
      "name": "Hotspot Analysis",
      "status": "pending",
      "mode": "ask",
      "started_at": null,
      "completed_at": null,
      "input_artifacts": [],
      "output_artifacts": []
    },
    "1": {
      "name": "Scope Definition",
      "status": "pending",
      "mode": "plan",
      "started_at": null,
      "completed_at": null,
      "input_artifacts": [],
      "output_artifacts": []
    }
  },
  "dependencies": {
    "1": ["0"]
  },
  "metadata": {
    "total_tickets": null,
    "completed_tickets": 0,
    "failed_phases": [],
    "retry_count": 0,
    "estimated_duration_minutes": null,
    "actual_duration_minutes": null
  }
}
```

### Complete Manifest (Finished Epic)

See Appendix A in `V12_EPIC_WORKFLOW_REFACTORING_DESIGN.md` for full EPIC-CCN-15 example.

### Manifest with Parallel Execution

```json
{
  "epic_id": "EPIC-CCN-17",
  "description": "Extract multiple order validation methods",
  "status": "in_progress",
  "created_at": "2026-06-09T06:00:00Z",
  "updated_at": "2026-06-09T07:30:00Z",
  "phases": {
    "4": {
      "name": "Ticket Generation",
      "status": "completed",
      "mode": "plan",
      "started_at": "2026-06-09T06:00:00Z",
      "completed_at": "2026-06-09T06:30:00Z",
      "input_artifacts": ["docs/brain/EPIC-CCN-17/02-architecture-plan.md"],
      "output_artifacts": ["docs/brain/EPIC-CCN-17/04-tickets.md"]
    },
    "5.1": {
      "name": "Ticket 1: Extract ValidatePrice",
      "status": "in_progress",
      "mode": "v12-engineer",
      "started_at": "2026-06-09T06:30:00Z",
      "completed_at": null,
      "input_artifacts": ["docs/brain/EPIC-CCN-17/04-tickets.md"],
      "output_artifacts": []
    },
    "5.2": {
      "name": "Ticket 2: Extract ValidateQuantity",
      "status": "in_progress",
      "mode": "v12-engineer",
      "started_at": "2026-06-09T06:30:00Z",
      "completed_at": null,
      "input_artifacts": ["docs/brain/EPIC-CCN-17/04-tickets.md"],
      "output_artifacts": []
    },
    "5.3": {
      "name": "Ticket 3: Extract ValidateInstrument",
      "status": "in_progress",
      "mode": "v12-engineer",
      "started_at": "2026-06-09T06:30:00Z",
      "completed_at": null,
      "input_artifacts": ["docs/brain/EPIC-CCN-17/04-tickets.md"],
      "output_artifacts": []
    }
  },
  "dependencies": {
    "5.1": ["4"],
    "5.2": ["4"],
    "5.3": ["4"]
  },
  "parallel_groups": [
    {
      "group_id": "ticket_execution",
      "phases": ["5.1", "5.2", "5.3"],
      "description": "Independent validation methods can be extracted concurrently"
    }
  ],
  "metadata": {
    "total_tickets": 3,
    "completed_tickets": 0,
    "failed_phases": [],
    "retry_count": 0,
    "estimated_duration_minutes": 180,
    "actual_duration_minutes": null
  }
}
```

### Manifest with Failed Phase

```json
{
  "epic_id": "EPIC-CCN-18",
  "description": "Extract FSM state transition logic",
  "status": "failed",
  "created_at": "2026-06-09T08:00:00Z",
  "updated_at": "2026-06-09T09:15:00Z",
  "phases": {
    "5.1": {
      "name": "Ticket 1: Extract ValidateTransition",
      "status": "failed",
      "mode": "v12-engineer",
      "started_at": "2026-06-09T08:30:00Z",
      "completed_at": "2026-06-09T09:15:00Z",
      "input_artifacts": ["docs/brain/EPIC-CCN-18/04-tickets.md"],
      "output_artifacts": [],
      "error": "Build failed: CS0103 - Undefined variable 'stateContext'",
      "notes": "Need to pass StateContext as parameter"
    }
  },
  "dependencies": {
    "5.1": ["4"]
  },
  "metadata": {
    "total_tickets": 1,
    "completed_tickets": 0,
    "failed_phases": ["5.1"],
    "retry_count": 1,
    "estimated_duration_minutes": 60,
    "actual_duration_minutes": null
  }
}
```

## Usage Guidelines

### Creating a New Manifest

1. Use `generate_manifest(epic_id, description)` helper function
2. Manifest is created in `docs/brain/{epic_id}/manifest.json`
3. Initial status is `pending`
4. Only Phase 0 and Phase 1 are defined initially
5. Additional phases added as epic progresses

### Updating Phase Status

1. Use `update_manifest(epic_id, phase, status, outputs, notes)` helper
2. Status transitions are validated
3. Timestamps are automatically updated
4. Output artifacts are validated to exist

### Checking Dependencies

1. Use `validate_dependencies(epic_id, phase)` helper
2. Returns `True` if all dependencies completed
3. Returns `False` if any dependency pending/failed
4. Raises error if circular dependencies detected

### Getting Next Phases

1. Use `get_next_phases(epic_id)` helper
2. Returns list of phase IDs ready to execute
3. Considers dependencies and parallel groups
4. Excludes blocked/skipped phases

## Integration with Commands

### epic-intake Command

**Before** (monolithic):
```bash
epic-intake EPIC-CCN-X "Description"
# Runs Phase 0 in single session
```

**After** (manifest-based):
```bash
epic-intake EPIC-CCN-X "Description"
# 1. Generates manifest.json
# 2. Runs Phase 0
# 3. Updates manifest with Phase 0 outputs
# 4. Returns manifest path
```

### epic-orchestrate Command (New)

```bash
epic-orchestrate EPIC-CCN-X
# 1. Loads manifest.json
# 2. Calls get_next_phases()
# 3. Launches phases (parallel if possible)
# 4. Monitors completion
# 5. Updates manifest
# 6. Repeats until all phases complete
```

### Phase Commands (Updated)

Each phase command will:
1. Load manifest to verify dependencies
2. Read input artifacts from manifest
3. Execute phase work
4. Write output artifacts
5. Update manifest with status and outputs

## Error Handling

### Validation Errors

**Invalid Phase ID**:
```json
{
  "error": "Invalid phase ID format",
  "phase": "phase-1",
  "expected_pattern": "^\\d+(\\.\\d+)?(\\.V)?$"
}
```

**Invalid Status Transition**:
```json
{
  "error": "Invalid status transition",
  "phase": "5.1",
  "from_status": "completed",
  "to_status": "in_progress",
  "allowed_transitions": []
}
```

**Circular Dependency**:
```json
{
  "error": "Circular dependency detected",
  "cycle": ["1", "2", "1.5", "1"]
}
```

**Missing Artifact**:
```json
{
  "error": "Output artifact not found",
  "phase": "1",
  "artifact": "docs/brain/EPIC-CCN-X/00-scope.md",
  "status": "completed"
}
```

### Recovery Strategies

**Failed Phase**:
1. Review error message in manifest
2. Fix underlying issue
3. Update phase status to `pending`
4. Re-run phase

**Missing Dependency**:
1. Check dependency phase status
2. If failed, fix and retry dependency
3. If blocked, resolve blocker
4. Dependent phase will auto-unblock

**Corrupted Manifest**:
1. Validate manifest with schema
2. Fix validation errors
3. If unfixable, restore from backup
4. Last resort: regenerate from artifacts

## Best Practices

### 1. Atomic Updates

Always update manifest atomically:
```python
# Good
with manifest_lock:
    manifest = load_manifest(epic_id)
    manifest['phases'][phase]['status'] = 'completed'
    save_manifest(epic_id, manifest)

# Bad (race condition)
manifest = load_manifest(epic_id)
manifest['phases'][phase]['status'] = 'completed'
save_manifest(epic_id, manifest)
```

### 2. Validate Before Update

Always validate before updating:
```python
# Good
if validate_status_transition(old_status, new_status):
    update_manifest(epic_id, phase, new_status)
else:
    raise ValueError(f"Invalid transition: {old_status} -> {new_status}")

# Bad (no validation)
update_manifest(epic_id, phase, new_status)
```

### 3. Verify Artifacts

Always verify artifacts exist:
```python
# Good
for artifact in output_artifacts:
    if not os.path.exists(artifact):
        raise FileNotFoundError(f"Artifact not found: {artifact}")
update_manifest(epic_id, phase, 'completed', output_artifacts)

# Bad (no verification)
update_manifest(epic_id, phase, 'completed', output_artifacts)
```

### 4. Add Descriptive Notes

Always add notes for context:
```python
# Good
update_manifest(
    epic_id, 
    phase, 
    'completed',
    outputs=['docs/brain/EPIC-X/00-scope.md'],
    notes='Scope limited to 3 methods, 2 files. CYC target: 15'
)

# Bad (no context)
update_manifest(epic_id, phase, 'completed', outputs)
```

### 5. Handle Errors Gracefully

Always capture errors in manifest:
```python
# Good
try:
    execute_phase(epic_id, phase)
    update_manifest(epic_id, phase, 'completed', outputs)
except Exception as e:
    update_manifest(
        epic_id, 
        phase, 
        'failed',
        notes=f'Error: {str(e)}'
    )

# Bad (error lost)
execute_phase(epic_id, phase)
update_manifest(epic_id, phase, 'completed', outputs)
```

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 1.0 | 2026-06-09 | Initial schema definition |

## References

- **Design Document**: `docs/workflow/V12_EPIC_WORKFLOW_REFACTORING_DESIGN.md`
- **Helper Functions**: `scripts/epic_manifest.py`
- **Test Plan**: `docs/workflow/EPIC_WORKFLOW_TESTING_PLAN.md`
- **JSON Schema Spec**: https://json-schema.org/draft-07/schema

---

**Schema Status**: Active  
**Next Review**: After Phase 1 pilot (EPIC-CCN-16)  
**Maintainer**: V12 Architecture Team