name: docs-manager
description: Portability-focused documentation protocol for maintaining CHANGELOG.md, milestones, and README. Uses Gemini Flash delegation for zero-cost doc updates and consistent state across IDEs.

# Documentation Manager Sub-Agent

## Purpose
Fast, cost-effective documentation management using Haiku model for routine doc updates.

## When to Auto-Trigger
Automatically spawn a Haiku sub-agent when user requests:
- "Update changelog"
- "Add milestone to changelog"
- "Create milestone summary for V8_2"
- "Document this release"
- "Update README"

## Sub-Agent Configuration
```
Primary Executor: Gemini 3 Flash (via delegation_bridge MCP)
Protocol: Wearable Project Standard
Tools: call_gemini_flash (update_docs), read_file
Type: Documentation & State Maintenance
```

## Operations

### 1. Update CHANGELOG.md

#### Standard Entry Format
```markdown
## [VERSION] - YYYY-MM-DD

### Added
- **Feature Name**: Description
  - Bullet point details
  - More details

### Changed
- Description of changes

### Fixed
- Bug fix description

### Files Modified
- `Filename.cs` - What changed

---
```

#### Milestone Entry Format
```markdown
## [VERSION] - YYYY-MM-DD - MILESTONE "Name"

### Added
- Feature details

### Test Results
- ✅ Test 1 passed
- ✅ Test 2 passed

### Production Status
- ✅ **READY FOR LIVE TRADING**

### Files
- `Filename.cs` - Main file
- See `MILESTONE_VERSION_SUMMARY.md` for details

---
```

#### Workflow
1. Read CHANGELOG.md
2. Find insertion point (after header, before first entry)
3. Format new entry with date, version, changes
4. Insert at top of changelog
5. Save file

### 2. Create Milestone Summary

#### File Naming
`MILESTONE_V[VERSION]_[NAME]_SUMMARY.md`

Example: `MILESTONE_V8_2_UI_HORIZONTAL_SUMMARY.md`

#### Content Structure
```markdown
# Milestone: V8.2 UI Horizontal Layout

**Date**: YYYY-MM-DD
**Status**: ✅ Complete / 🚧 In Progress / ❌ Failed

## Summary
Brief description of what this milestone achieved.

## Changes Made
- Detailed bullet points
- File-by-file breakdown

## Test Results
- ✅ What was tested
- Results and verification

## Files Modified
1. `File1.cs` - Changes
2. `File2.cs` - Changes

## Next Steps
- What comes next (if applicable)
```

### 3. Update README.md

#### Operations
- Update version number in header
- Add new features to feature list
- Update "Current Version" section
- Add notes about recent changes

#### Safety
- NEVER rewrite entire README
- Only update specific sections
- Preserve existing content

## Locations
- **CHANGELOG.md**: `c:\Users\Mohammed Khalid\OneDrive\Desktop\WSGTA\Github\universal-or-strategy\CHANGELOG.md`
- **Milestones**: Same directory as CHANGELOG
- **README.md**: Same directory as CHANGELOG

## Workflow Examples

### After File Creation
**Sonnet**: Creates V8_2_UI_HORIZONTAL via file-manager
**Sonnet**: Calls docs-manager to update changelog
**Haiku agent**:
1. Read CHANGELOG.md
2. Create entry:
```markdown
## [8.2_UI_HORIZONTAL] - 2026-01-18

### Added
- **Horizontal Button Layout**: Redesigned UI with buttons in rows
  - Row 1: Long | Short (2 buttons)
  - Row 2: RMA | TREND (2 buttons)
  - Row 3: T1 | T2 | T3 | Runner | BE (5 buttons)
- **Resizable Panel**: Drag corner to resize (280-600px width)
- **Proportional Scaling**: Buttons scale with panel width

### Changed
- Grid structure from 18 rows × 1 column to 8 rows × multi-column
- Button layout from vertical stack to horizontal rows

### Files Modified
- `UniversalORStrategyV8_2_UI_HORIZONTAL.cs` - Complete UI redesign

---
```
3. Insert at top
4. Save
5. Report: "✓ Changelog updated with V8_2_UI_HORIZONTAL entry"

### Creating Milestone
**User**: "Create milestone summary for V8_2 UI redesign"
**Haiku agent**:
1. Create `MILESTONE_V8_2_UI_HORIZONTAL_SUMMARY.md`
2. Fill with structure above
3. Add to changelog as milestone entry
4. Report: "✓ Milestone summary created and changelog updated"

## Integration with Other Skills
- **file-manager**: After creating new files, docs-manager updates changelog
- **version-safety**: Documentation reflects safe versioning practices
- **version-manager**: Changelog shows version history for reference

## Input from Sonnet
When Sonnet calls docs-manager, it provides:
- Version number
- Change type (Added/Changed/Fixed)
- Description of changes
- Files modified
- (Optional) Test results for milestones

Haiku agent formats this into proper changelog entry.

## Safety Rules
- NEVER delete existing changelog entries
- ALWAYS insert new entries at top (after header)
- ALWAYS preserve formatting
- NEVER modify milestone summaries after creation (unless explicitly requested)

## Example Interactions

**Sonnet to docs-manager**: "Add changelog entry for V8_2_BUGFIX"
**Input**:
```
Version: 8.2_BUGFIX
Type: Fixed
Changes: Stop validation bug causing rejections
Files: UniversalORStrategyV8_2_BUGFIX.cs
```

**Haiku agent output**:
```markdown
## [8.2_BUGFIX] - 2026-01-18

### Fixed
- **Stop Validation Bug**: Corrected stop price calculation
  - Stops were being placed too close to market price
  - Added 4-tick buffer to prevent rejection
  - Validated against Rithmic data feed requirements

### Files Modified
- `UniversalORStrategyV8_2_BUGFIX.cs` - Stop validation logic

---
```

## Related Skills
- [delegation-bridge](../delegation-bridge/SKILL.md) - Execution of doc updates
- [wearable-project](../antigravity-core/wearable-project.md) - Portability standards
- [file-manager](../file-manager/SKILL.md) - File deployment relationship

---
*Optimized for Gemini 3 Flash delegation via bridge (2026-01-21)*
