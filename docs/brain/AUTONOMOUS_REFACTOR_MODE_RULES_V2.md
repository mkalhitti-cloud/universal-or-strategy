# Autonomous Refactor Mode - Custom Instructions V2.0

**Version**: 2.0
**Date**: 2026-06-19
**Purpose**: Paste this into `.bob/custom_modes.yaml` under `autonomous-refactor` → `customInstructions`

---

## Primary Reference Document

**MANDATORY FIRST READ**: `docs/brain/WAVE8_COMPLETE_REFERENCE.md`

This is your single source of truth for:
- Wave history & evolution (Waves 4-8)
- Current status & progress
- Scope & baseline (180 methods)
- Jane Street violations (299 P0 violations, 69.8% overlap)
- Phase 1.5 freeze incident & fix
- Wave 8 merge strategy (6-day timeline)
- Building-blocks method (MANDATORY)
- Execution protocols (Bob CLI pattern, polling, recovery)
- Quality gates (pre-push validation, per-phase criteria)
- Complete reference document index

---

## Core Mission

**Goal**: Orchestrate Wave 8 autonomous refactoring to reduce all 180 methods in V12 Photon Kernel to CYC ≤8 (Jane Street strict standard)

**Current State**:
- ✅ Wave 6 Phase 0-1: 79 epics complete
- ❌ Wave 6 Phase 1.5: FROZEN (script bug - fix ready)
- ⏳ Wave 7 Phase 0-1: 101 epics pending (catch-up)
- ⏳ Wave 8 Phase 1.5-6: 180 epics unified execution

**Timeline**: 6 days total (25% faster than sequential)

---

## Critical Protocols (NEVER FORGET)

### 1. Building-Blocks Method (MANDATORY)
**Golden Rule**: ALWAYS copy working scripts from previous phases/waves, modify only phase-specific parameters, NEVER generate from scratch

**Mandatory Reading**:
- `docs/workflow/WAVE_PHASE_SCRIPT_GENERATION_SOP_V3.md`
- `building-blocks/autonomous-refactoring/ARCHITECTURE.md`
- `building-blocks/autonomous-refactoring/GETTING_STARTED.md`

**Template Locations**: `building-blocks/autonomous-refactoring/phase{0-6}_template_v12_52.sh`

**Violation**: Any script generated from scratch = protocol violation

### 2. Bob CLI Invocation Pattern (MANDATORY)
**ALL phases MUST use this two-step pattern**:

```bash
# Step 1: Create message file
cat > /tmp/phaseX_msg_$EPIC_ID.txt << 'EOFMSG'
[message content]
EOFMSG

# Step 2: Invoke Bob with command substitution
bob --yolo --chat-mode MODE "$(cat /tmp/phaseX_msg_$EPIC_ID.txt)"
```

**Enforcement**:
- ❌ NEVER use inline message strings (causes freeze)
- ✅ ALWAYS use temp file + command substitution
- ✅ VM Bob CLI Path: `~/.npm-global/bin/bob`

**Reference**: `docs/brain/PHASE1_5_FREEZE_COMPLETE_ANALYSIS.md`

### 3. 100% Completion Mandate (V12.28)
**NEVER dismiss any epic as "not our concern"**

**Rules**:
- If epic exists in roadmap OR has brain directory → IN SCOPE
- Naming mismatches do NOT exempt epic (e.g., EPIC-CCN-27 vs EPIC-CCN-027)
- Missing prerequisite files do NOT exempt epic → execute missing phases first
- Goal is ALWAYS N/N (100%), never N-1/N

**Reference**: Wave 4 failure (78/80 completed, 2 epics dismissed incorrectly)

### 4. No Scope Creep Protocol (V12.23)
**ONE EPIC = ONE CONCERN**

**Rules**:
- Never mix unrelated fixes in a single PR
- If unrelated issues found, STOP and report to Director
- Create separate PRs for each concern
- Phase 1.5 is MANDATORY gate to prevent scope creep

**Reference**: EPIC-13 PR #12 failure (mixed concerns = 3 P0 blockers)

### 5. Jane Street Violations (69.8% Overlap)
**Fix violations DURING refactoring, not separately**

**Categories**:
- Philosophy (223, 74.6%): Magic numbers → named constants
- Type Safety (69, 23.1%): Exceptions → `Result<T,E>`
- Concurrency (5, 1.7%): Locks → Actor pattern
- Performance (2, 0.7%): Heap allocation → `Span`/`ArrayPool`

**Per-File Checklist** (Phase 5):
- [ ] Extract magic numbers → named constants
- [ ] Replace exceptions → `Result<T,E>`
- [ ] Replace locks → Actor pattern
- [ ] Use `Span`/`ArrayPool` in hot paths
- [ ] Reduce complexity to CYC ≤ 8

**Reference**: `docs/brain/JANE_STREET_VIOLATION_CATEGORIES_EXPLAINED.md`

### 6. Cost-Optimized Polling (V12.52)
**4-minute polling intervals** (88% cost reduction vs 30 seconds)

**Strategy**:
- Reuse jCodemunch index (minimize API calls)
- Master launch script with polling
- Cache optimization

**Reference**: `docs/protocol/COST_OPTIMIZED_POLLING_PROTOCOL.md`

---

## Wave 8 Execution Flow

### Phase 1: Wave 7 Catch-Up (1 day)
1. Extract Wave 7 methods (101 methods) - `extract_wave7_methods.ps1`
2. Generate Phase 0 scripts (copy Wave 6 template)
3. Execute Phase 0 (hotspot analysis)
4. Generate Phase 1 scripts (copy Wave 6 template)
5. Execute Phase 1 (scope definition)

### Phase 2: Wave 8 Merge Prep (0.5 days)
1. Validate Wave 6 + Wave 7 Phase 0-1 complete
2. Create Wave 8 unified manifest (180 epics)
3. Generate Phase 1.5-6 scripts (all 180 epics)
4. **Fix Phase 1.5 scripts** (use `phase1_5_template_v12_52_FIXED.sh`)

### Phase 3: Wave 8 Pilot (1 day)
1. Select 3 pilot epics (1 Wave 6, 2 Wave 7)
2. Execute Phase 1.5-6 for pilots
3. Validate results
4. Fix any issues

### Phase 4: Wave 8 Full Execution (2-3 days)
1. Execute Phase 1.5 (all 180 epics)
2. Execute Phases 2-6 (sequential)
3. Monitor progress (4-minute polling)
4. Recovery loop for failures

### Phase 5: Final Validation (1 day)
1. Complexity audit (verify 0 methods CYC > 8)
2. Build validation (verify 0 errors)
3. Test validation (verify 100% pass)
4. Jane Street compliance (verify all violations fixed)

---

## Special Cases

### VM Configuration
- **IP**: 34.60.155.195
- **User**: malhitticrypto
- **Cluster**: universal-or-epic-cluster-1
- **Bob Path**: `~/.npm-global/bin/bob`

### Epic Exclusions
- **EPIC-003**: Local execution (due to .dll dependency)
- **EPIC-024**: Excluded (missing Phase 0 script)
- **EPIC-027**: Excluded (user confirmed)

### Scope Validation
- **Total Methods**: 180 with CYC > 8
- **Wave 6**: 79 methods
- **Wave 7**: 101 methods
- **Overlap**: 0 methods (validated)

---

## Quality Gates

### Pre-Push Validation (MANDATORY)
Run `pre_push_validation.ps1` before every push

**Blocking Checks**:
1. ASCII-Only
2. Build (0 errors)
3. Unit Tests (100% pass)
4. Lint (0 violations)
5. Formatting (CSharpier)
6. PR Hygiene (diff <10k)
7. Complexity (CYC ≤ 8)

### Per-Phase Success Criteria
See `docs/brain/WAVE8_COMPLETE_REFERENCE.md` → Quality Gates section

---

## Recovery Protocol

**If epic fails**:
1. Identify failure phase
2. Review phase output artifacts
3. Fix issues in separate session
4. Update manifest status to `pending`
5. Re-run phase
6. Continue only after success

**If VM freezes**:
1. STOP immediately
2. Check for inline Bob CLI messages (violation)
3. Apply temp file pattern fix
4. Restart VM
5. Resume from last successful phase

---

## Reference Document Index

### Primary (MUST READ)
1. **Wave 8 Complete Reference**: `docs/brain/WAVE8_COMPLETE_REFERENCE.md` ← START HERE
2. **Script Generation SOP**: `docs/workflow/WAVE_PHASE_SCRIPT_GENERATION_SOP_V3.md`
3. **Jane Street Violations**: `docs/brain/JANE_STREET_VIOLATION_CATEGORIES_EXPLAINED.md`
4. **Phase 1.5 Freeze**: `docs/brain/PHASE1_5_FREEZE_COMPLETE_ANALYSIS.md`

### Architecture
5. **Building-Blocks Architecture**: `building-blocks/autonomous-refactoring/ARCHITECTURE.md`
6. **Getting Started**: `building-blocks/autonomous-refactoring/GETTING_STARTED.md`
7. **Cost-Optimized Polling**: `docs/protocol/COST_OPTIMIZED_POLLING_PROTOCOL.md`

### Scope & Baseline
8. **Wave 8 Merge Strategy**: `docs/brain/WAVE8_MERGE_STRATEGY.md`
9. **Wave 7 Scope**: `docs/brain/WAVE7_SCOPE_DEFINITION.md`
10. **Complexity Baseline**: `complexity_audit_fresh_2026-06-14.txt`
11. **180 Methods JSON**: `baseline_180_methods.json`

### Standards
12. **Jane Street Rules**: `docs/standards/jane-street/RULES_CATALOG.md`
13. **Violations JSON**: `jane_street_p0_violations.json`

### Templates
14. **Phase 0-6 Templates**: `building-blocks/autonomous-refactoring/phase{0-6}_template_v12_52.sh`
15. **Phase 1.5 FIXED**: `building-blocks/autonomous-refactoring/phase1_5_template_v12_52_FIXED.sh`

---

## Quick Checklist

**Before Starting Any Wave**:
- [ ] Read `docs/brain/WAVE8_COMPLETE_REFERENCE.md`
- [ ] Read Script Generation SOP V3
- [ ] Fresh complexity audit run
- [ ] Epic roadmap updated
- [ ] VM accessible and build passes
- [ ] jCodemunch index current
- [ ] Git status clean (no uncommitted `src/` changes)
- [ ] Branch strategy: GitButler virtual branches active

**Before Generating Scripts**:
- [ ] Copy from previous wave/phase (building-blocks method)
- [ ] Use temp file pattern for Bob CLI (MANDATORY)
- [ ] Verify template has correct mode
- [ ] Verify template has correct output file
- [ ] Test on 1 epic before generating all

**Before Executing Phase**:
- [ ] Pilot test (3 epics) complete
- [ ] All issues from pilot fixed
- [ ] Monitoring scripts deployed
- [ ] Recovery loop protocol ready

**After Each Phase**:
- [ ] Verify all epics completed
- [ ] Check for errors in logs
- [ ] Verify file persistence
- [ ] Update roadmap
- [ ] Document lessons learned

---

## Emergency Procedures

### VM Freeze
1. DO NOT run more commands
2. Check for inline Bob CLI messages (violation)
3. Stop VM from GCP console
4. Apply temp file pattern fix
5. Restart VM
6. Resume from last successful phase

### Script Generation Error
1. DO NOT generate from scratch
2. Find correct template in `building-blocks/`
3. Copy template
4. Modify only phase-specific parameters
5. Test on 1 epic
6. Generate rest after validation

### Scope Creep Detected
1. STOP immediately
2. Close PR
3. Document failure in `docs/brain/EPIC-X/failure-analysis.md`
4. Separate concerns into individual PRs
5. Restart epic cleanly

---

## Post-Wave Audit (MANDATORY)

After wave completion:
1. ✅ Review all generated scripts for pattern compliance
2. ✅ Document any deviations and root causes
3. ✅ Update building-blocks templates if new patterns discovered
4. ✅ State "building-blocks(wave-X): no gaps identified" if no gaps found
5. ✅ Update this document with lessons learned

---

**Version**: 2.0
**Last Updated**: 2026-06-19
**Next Review**: After Wave 8 pilot completion
**Status**: ACTIVE - Use this for all Wave 8 execution