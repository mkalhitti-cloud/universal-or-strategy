# Git Hooks Consolidation Analysis

**Date**: 2026-06-13
**Purpose**: Analyze current git hooks and design consolidated system for auto-updating PROJECT_DIRECTORY.md, graphify, and jCodemunch

## Current State

### Existing Git Hooks

#### 1. `.git/hooks/post-commit` (158 lines)
**Purpose**: Bob Shell notes attachment + remote sync

**What it does**:
- Attaches Bob Shell notes from `.bob/notes/pending-notes.txt` to commits
- Validates Bob's changes are still present in commit
- Syncs notes with remote (origin/upstream/fork) for collaboration
- Handles merge conflicts with `cat_sort_uniq` strategy
- Cleans up stuck merge states

**Key Functions**:
- `sync_notes_with_remote()` - Pushes/pulls notes to remote
- Note filtering by committed files
- Diff validation (Bob's changes vs actual commit)

#### 2. `.git/hooks/pre-commit` (120 lines)
**Purpose**: V12 src-only protection + Bob notes cleanup

**What it does**:
- **V12 Protection** (lines 1-59):
  - Enforces `.cs`-only rule on non-main branches
  - Allows all files on `main` and `gitbutler/workspace`
  - Checks branch sync with main (prevents divergence)
  - Blocks commit if branch is behind main
- **Bob Notes Cleanup** (lines 61-120):
  - Cleans up stale notes before commit
  - Filters notes by modified files (staged + unstaged + untracked)
  - Shared function: `filter_notes_by_modified_files()`

#### 3. `.git/hooks/post-checkout` (67 lines)
**Purpose**: Bob Shell notes cleanup on branch switch

**What it does**:
- Cleans up notes when switching branches
- Filters notes by modified files in new branch

#### 4. `.git/hooks/post-merge` (59 lines)
**Purpose**: Bob Shell notes cleanup after merge

**What it does**:
- Cleans up notes after merge operations
- Filters notes by modified files post-merge

### Current Update Patterns

#### Graphify Updates

**Current Locations**:
1. **Manual Updates** (51 occurrences in docs):
   - `graphify update .` - CLI command
   - Recommended after "major structural changes to src/"
   - V12.19 mandate: MUST run after ANY structural src/ changes
   - Takes 30-60 seconds (slow)

**Current Triggers**:
- ❌ **NOT automated** - Manual only
- ✅ **After epic completion** (recommended in MCP_UPDATE_PROTOCOL.md)
- ✅ **After src/ modifications** (V12.19 mandate)
- ❌ **NOT in git hooks** (no automation)

**Output**:
- `graphify-out/graph.json` - Knowledge graph
- `graphify-out/GRAPH_REPORT.md` - Analysis report
- `graphify-out/wiki/` - Documentation

**Performance**:
- 30-60 seconds per update
- AST-only (no API cost)
- Static output files

#### jCodemunch Updates

**Current Locations**:
1. **Session Start** (recommended):
   - `resolve_repo { "path": "." }` - Check if indexed
   - `index_folder { "path": "." }` - Full re-index if needed
   - `suggest_queries` - When repo unfamiliar

2. **After Edits** (recommended):
   - `register_edit(repo, file_paths)` - Invalidate caches
   - `index_file(path)` - Re-index single file
   - `index_folder(path, incremental=true)` - Incremental update

**Current Triggers**:
- ❌ **NOT automated** - Manual only
- ✅ **After every edit** (recommended via `register_edit`)
- ✅ **Before epic planning** (freshness check)
- ❌ **NOT in git hooks** (no automation)

**Known Issues**:
- **Stale Index Problem**: Index dated 2026-05-19, actual code refactored since
  - Example: MonitorRmaProximity reported CYC 32, actual is CYC 7
  - Caused EPIC-CCN-1 failure (wrong complexity data)
- **Manual Updates**: Agents forget to call `register_edit` or `index_file`

**Performance**:
- `register_edit`: Fast (cache invalidation only)
- `index_file`: Fast (single file)
- `index_folder`: Slow (full re-index)

#### PROJECT_DIRECTORY.md Updates

**Current State**:
- ❌ **NOT automated** - Manual only
- ❌ **No update mechanism** - Becomes stale
- ✅ **Critical for navigation** - "Mall directory" for codebase

**Current Content**:
- Commands and what they use
- Skills and what they implement
- SOPs and who follows them
- Tools and what they update
- Complete dependency maps

## Proposed Consolidated Architecture

### Design Principles

1. **Modular**: Each concern in separate module
2. **Composable**: Hooks orchestrate modules
3. **Fast**: Skip expensive operations when possible
4. **Safe**: Never block commits on failures
5. **Informative**: Clear output for debugging

### Module Structure

```
.git/hooks/
├── modules/
│   ├── bob-notes.sh           # Extract from existing hooks
│   ├── v12-protection.sh      # Extract from pre-commit
│   ├── project-directory.sh   # NEW - Auto-update directory
│   ├── graphify-update.sh     # NEW - Knowledge graph
│   └── jcodemunch-index.sh    # NEW - Code index
├── pre-commit                 # Orchestrator
├── post-commit                # Orchestrator
├── post-checkout              # Orchestrator
└── post-merge                 # Orchestrator
```

### Module Specifications

#### 1. `bob-notes.sh` (Extract from existing)
**Purpose**: Bob Shell notes management

**Functions**:
- `sync_notes_with_remote()` - Push/pull notes
- `filter_notes_by_modified_files()` - Clean stale notes
- `attach_notes_to_commit()` - Add notes to commit

**Called by**:
- `pre-commit` - Cleanup
- `post-commit` - Attach + sync
- `post-checkout` - Cleanup
- `post-merge` - Cleanup

#### 2. `v12-protection.sh` (Extract from pre-commit)
**Purpose**: V12 src-only enforcement

**Functions**:
- `check_branch_sync()` - Verify not behind main
- `check_file_types()` - Enforce .cs-only on PR branches

**Called by**:
- `pre-commit` - Block non-.cs files

#### 3. `project-directory.sh` (NEW)
**Purpose**: Auto-update PROJECT_DIRECTORY.md

**Functions**:
- `scan_commands()` - Find all .bob/commands/*.yaml
- `scan_skills()` - Find all .bob/skills/*/skill.md
- `scan_sops()` - Find all docs/workflow/*_SOP.md
- `scan_tools()` - Find all scripts/*.py, scripts/*.ps1
- `generate_directory()` - Create PROJECT_DIRECTORY.md
- `should_update()` - Check if update needed

**Triggers**:
- File created/deleted/moved in:
  - `.bob/commands/`
  - `.bob/skills/`
  - `docs/workflow/`
  - `scripts/`

**Called by**:
- `post-commit` - After structural changes
- `post-merge` - After merging changes

**Performance**:
- Fast: <1 second (file scanning only)
- Skip if no structural changes

#### 4. `graphify-update.sh` (NEW)
**Purpose**: Auto-update knowledge graph

**Functions**:
- `should_update_graphify()` - Check if src/ changed
- `run_graphify_update()` - Execute `graphify update .`
- `verify_graphify_output()` - Check output files

**Triggers**:
- ANY file changed in `src/` (V12.19 mandate)
- Structural changes: new files, deleted files, moved files

**Called by**:
- `post-commit` - After src/ changes
- `post-merge` - After merging src/ changes

**Performance**:
- Slow: 30-60 seconds
- Skip if no src/ changes
- Run in background (non-blocking)

**V12.19 Compliance**:
- MANDATORY after ANY src/ structural change
- No skip rules allowed
- Always update regardless of change size

#### 5. `jcodemunch-index.sh` (NEW)
**Purpose**: Auto-update code index

**Functions**:
- `should_update_jcodemunch()` - Check if src/ changed
- `run_jcodemunch_index()` - Execute index update
- `get_changed_files()` - List modified src/ files

**Triggers**:
- ANY file changed in `src/`
- Use `register_edit` for cache invalidation
- Use `index_file` for single-file updates

**Called by**:
- `post-commit` - After src/ changes
- `post-merge` - After merging src/ changes

**Performance**:
- Fast: `register_edit` (cache invalidation)
- Medium: `index_file` (single file)
- Skip if no src/ changes

**Strategy**:
1. **Per-file updates**: Use `index_file` for each changed file
2. **Cache invalidation**: Always call `register_edit` with all changed files
3. **Batch updates**: If >10 files changed, use `index_folder(incremental=true)`

### Hook Orchestration

#### `pre-commit`
```bash
#!/bin/bash
source .git/hooks/modules/v12-protection.sh
source .git/hooks/modules/bob-notes.sh

# V12 Protection (blocking)
check_branch_sync || exit 1
check_file_types || exit 1

# Bob Notes Cleanup (non-blocking)
cleanup_stale_notes || true

exit 0
```

#### `post-commit`
```bash
#!/bin/bash
source .git/hooks/modules/bob-notes.sh
source .git/hooks/modules/project-directory.sh
source .git/hooks/modules/graphify-update.sh
source .git/hooks/modules/jcodemunch-index.sh

# Bob Notes (blocking - critical for collaboration)
attach_notes_to_commit || exit 1
sync_notes_with_remote || true

# PROJECT_DIRECTORY.md (non-blocking)
if should_update_directory; then
    generate_directory &
fi

# Graphify (non-blocking, background)
if should_update_graphify; then
    run_graphify_update &
fi

# jCodemunch (non-blocking, background)
if should_update_jcodemunch; then
    run_jcodemunch_index &
fi

exit 0
```

#### `post-checkout`
```bash
#!/bin/bash
source .git/hooks/modules/bob-notes.sh

# Bob Notes Cleanup (non-blocking)
cleanup_stale_notes || true

exit 0
```

#### `post-merge`
```bash
#!/bin/bash
source .git/hooks/modules/bob-notes.sh
source .git/hooks/modules/project-directory.sh
source .git/hooks/modules/graphify-update.sh
source .git/hooks/modules/jcodemunch-index.sh

# Bob Notes Cleanup (non-blocking)
cleanup_stale_notes || true

# PROJECT_DIRECTORY.md (non-blocking)
if should_update_directory; then
    generate_directory &
fi

# Graphify (non-blocking, background)
if should_update_graphify; then
    run_graphify_update &
fi

# jCodemunch (non-blocking, background)
if should_update_jcodemunch; then
    run_jcodemunch_index &
fi

exit 0
```

## Change Detection Strategy

### PROJECT_DIRECTORY.md Triggers

**Track**:
- ✅ File creates in: `.bob/commands/`, `.bob/skills/`, `docs/workflow/`, `scripts/`
- ✅ File deletes in same directories
- ✅ File moves/renames in same directories

**Skip**:
- ❌ File edits (too noisy)
- ❌ Changes outside tracked directories

**Implementation**:
```bash
should_update_directory() {
    local changed_files=$(git diff-tree --no-commit-id --name-status -r HEAD)
    
    # Check for A (added), D (deleted), R (renamed) in tracked dirs
    echo "$changed_files" | grep -E '^[ADR].*\.(bob/commands|bob/skills|docs/workflow|scripts)/' >/dev/null
}
```

### Graphify Triggers

**Track**:
- ✅ ANY change in `src/` (V12.19 mandate)
- ✅ New files, deleted files, modified files, moved files

**Skip**:
- ❌ Changes outside `src/`

**Implementation**:
```bash
should_update_graphify() {
    local changed_files=$(git diff-tree --no-commit-id --name-only -r HEAD)
    
    # Check if any src/ files changed
    echo "$changed_files" | grep '^src/' >/dev/null
}
```

### jCodemunch Triggers

**Track**:
- ✅ ANY change in `src/` (same as graphify)
- ✅ Use `register_edit` for all changed files
- ✅ Use `index_file` for individual files

**Skip**:
- ❌ Changes outside `src/`

**Implementation**:
```bash
should_update_jcodemunch() {
    local changed_files=$(git diff-tree --no-commit-id --name-only -r HEAD)
    
    # Check if any src/ files changed
    echo "$changed_files" | grep '^src/' >/dev/null
}

run_jcodemunch_index() {
    local changed_files=$(git diff-tree --no-commit-id --name-only -r HEAD | grep '^src/')
    local file_count=$(echo "$changed_files" | wc -l)
    
    if [ "$file_count" -gt 10 ]; then
        # Batch update for many files
        jcodemunch-mcp index_folder --path=. --incremental=true
    else
        # Per-file updates
        echo "$changed_files" | while read file; do
            jcodemunch-mcp index_file --path="$file"
        done
        
        # Always invalidate caches
        jcodemunch-mcp register_edit --repo=. --file-paths="$changed_files"
    fi
}
```

## Performance Considerations

### Blocking vs Non-Blocking

**Blocking** (must succeed):
- ✅ V12 protection checks
- ✅ Bob notes attachment

**Non-Blocking** (failures logged, don't block commit):
- ✅ PROJECT_DIRECTORY.md updates
- ✅ Graphify updates
- ✅ jCodemunch updates
- ✅ Remote sync operations

### Background Execution

**Run in background** (don't wait):
- ✅ Graphify updates (30-60s)
- ✅ jCodemunch batch updates (>10 files)
- ✅ PROJECT_DIRECTORY.md generation

**Run synchronously** (fast):
- ✅ V12 protection checks (<1s)
- ✅ Bob notes operations (<1s)
- ✅ jCodemunch per-file updates (<1s each)

### Skip Conditions

**PROJECT_DIRECTORY.md**:
- Skip if no structural changes in tracked directories
- Skip if file already up-to-date (timestamp check)

**Graphify**:
- Skip if no src/ changes
- Skip if graph timestamp < 5 minutes old (rate limiting)

**jCodemunch**:
- Skip if no src/ changes
- Always run `register_edit` (fast cache invalidation)

## Installation Plan

### Phase 1: Extract Existing Modules
1. Create `.git/hooks/modules/` directory
2. Extract `bob-notes.sh` from existing hooks
3. Extract `v12-protection.sh` from pre-commit
4. Test existing hooks still work

### Phase 2: Create New Modules
1. Implement `project-directory.sh`
2. Implement `graphify-update.sh`
3. Implement `jcodemunch-index.sh`
4. Test each module independently

### Phase 3: Update Orchestrators
1. Update `pre-commit` to use modules
2. Update `post-commit` to use modules
3. Update `post-checkout` to use modules
4. Update `post-merge` to use modules
5. Test full workflow

### Phase 4: Documentation
1. Create `docs/workflow/GIT_HOOKS_GUIDE.md`
2. Update `PROJECT_DIRECTORY.md` with hook info
3. Add troubleshooting section
4. Document performance characteristics

## Testing Strategy

### Unit Tests (Per Module)
- Test `should_update_*()` functions with various file changes
- Test module functions in isolation
- Verify skip conditions work correctly

### Integration Tests (Full Hooks)
- Test commit with src/ changes (should trigger graphify + jcodemunch)
- Test commit with .bob/commands/ changes (should trigger directory)
- Test commit with no relevant changes (should skip all)
- Test merge scenarios
- Test branch switch scenarios

### Performance Tests
- Measure hook execution time with various change sizes
- Verify background processes don't block commits
- Test with 1, 10, 100 file changes

## Rollback Plan

### If Issues Arise
1. Keep backups of original hooks in `.git/hooks/backups/`
2. Restore originals: `cp .git/hooks/backups/* .git/hooks/`
3. Document issue in `docs/workflow/GIT_HOOKS_ISSUES.md`
4. Fix module and re-deploy

### Gradual Rollout
1. Deploy to test branch first
2. Monitor for 1 week
3. Deploy to main if stable
4. Document lessons learned

## Success Criteria

### Functional
- ✅ All existing hook functionality preserved
- ✅ PROJECT_DIRECTORY.md auto-updates on structural changes
- ✅ Graphify auto-updates on src/ changes (V12.19 compliance)
- ✅ jCodemunch auto-updates on src/ changes
- ✅ No false positives (unnecessary updates)
- ✅ No false negatives (missed updates)

### Performance
- ✅ Commit time <2 seconds (excluding background tasks)
- ✅ Background tasks don't interfere with workflow
- ✅ Skip conditions work correctly

### Reliability
- ✅ Failures don't block commits (except V12 protection)
- ✅ Clear error messages for debugging
- ✅ Graceful degradation if tools unavailable

## Open Questions

1. **jCodemunch MCP Integration**: How to call jCodemunch-MCP tools from bash?
   - Option A: Use `bob` CLI with MCP tool calls
   - Option B: Direct API calls (if available)
   - Option C: Python wrapper script

2. **Rate Limiting**: Should we rate-limit graphify updates?
   - Current: No rate limiting
   - Proposed: Skip if last update <5 minutes ago

3. **Conflict Resolution**: What if multiple hooks try to update same file?
   - PROJECT_DIRECTORY.md could be updated by multiple hooks
   - Need locking mechanism or merge strategy

4. **Windows Compatibility**: Do bash hooks work on Windows?
   - Git for Windows includes bash
   - Test on Windows before deployment

## Next Steps

1. ✅ **Analysis Complete** - This document
2. ⏳ **User Review** - Get feedback on design
3. ⏳ **Phase 1 Implementation** - Extract existing modules
4. ⏳ **Phase 2 Implementation** - Create new modules
5. ⏳ **Phase 3 Implementation** - Update orchestrators
6. ⏳ **Testing** - Unit + integration tests
7. ⏳ **Documentation** - Complete guide
8. ⏳ **Deployment** - Gradual rollout

## References

- **Existing Hooks**: `.git/hooks/pre-commit`, `post-commit`, `post-checkout`, `post-merge`
- **Graphify Protocol**: `docs/workflow/MCP_UPDATE_PROTOCOL.md`
- **jCodemunch Protocol**: `AGENTS.md` lines 322-324
- **V12.19 Mandate**: `docs/brain/v12_19_protocol_changes.md`
- **PROJECT_DIRECTORY.md**: Root directory (current state)