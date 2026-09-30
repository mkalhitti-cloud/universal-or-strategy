# Panel Monitoring Protocol (V12.24)

## Objective
Proactively monitor Bob IDE quality panels DURING coding to prevent issues from being committed.

## Shift-Left Quality Strategy
**Principle**: Catch issues at edit-time, not push-time or PR-time.

## Required Panels to Monitor

### 1. Problems Panel (115 issues visible)
**Purpose**: Real-time linting, type errors, CodeScene issues
**Check Frequency**: After every file edit
**Action**: Fix issues BEFORE committing

### 2. Comments Tab
**Purpose**: GitHub PR bot comments, review feedback
**Check Frequency**: Before starting new work
**Action**: Address unresolved comments first

### 3. Bob Findings Panel
**Purpose**: Bob's internal code review findings
**Check Frequency**: After every file edit
**Action**: Fix findings BEFORE committing

### 4. CSharpier (Existing)
**Purpose**: Formatting enforcement
**Check Frequency**: Pre-commit (automated)
**Action**: Auto-fix on save

## Workflow Integration

### Before Editing Code:
1. Check Problems panel - note existing issues in target file
2. Check Comments tab - review any open feedback
3. Check Bob Findings - review any active findings

### During Editing:
1. Monitor Problems panel - watch for NEW issues introduced
2. Fix issues immediately (don't accumulate technical debt)

### Before Committing:
1. Problems panel - ZERO new issues introduced
2. Bob Findings - ZERO new findings
3. CSharpier - Auto-formatted (existing)
4. Pre-push validation - All checks pass (existing)

## API Access Requirements

### Current Status:
- ❌ Problems panel - No programmatic access
- ❌ Comments tab - No programmatic access  
- ❌ Bob Findings - No programmatic access
- ✅ CSharpier - CLI available

### Needed:
Bob IDE needs to expose these panels via:
1. CLI commands (e.g., `bob problems list`)
2. JSON export (e.g., `bob findings export`)
3. MCP tools (e.g., `get_bob_findings`)

## Temporary Workaround

Until API access is available:
1. **Manual Check**: User reports panel status before/after edits
2. **Screenshot**: User shares panel screenshots when issues appear
3. **Export**: User exports panel data to files for parsing

## Long-Term Solution

**Feature Request**: Bob IDE should provide:
```bash
# List current problems
bob problems list --format json

# List Bob Findings
bob findings list --format json

# List PR comments
bob comments list --format json
```

This would enable true shift-left automation.

## Enforcement

This protocol is MANDATORY for all code modifications starting V12.24.

**Violation**: Committing code with unresolved panel issues is a protocol violation.