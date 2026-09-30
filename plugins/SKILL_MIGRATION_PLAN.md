# V12 Skill Migration to Anthropic Format

**Version**: 1.0  
**Date**: 2026-06-21  
**Status**: Ready for Execution

## Executive Summary

Migrate all V12 skills to Anthropic skill-creator format for:
- ✅ Auto-loading by Bob IDE (no manual `@skill` references needed)
- ✅ Better discoverability and documentation
- ✅ Anthropic spec compliance (https://agentskills.io/specification)
- ✅ Self-improvement via skill-creator tool

## Current State

### Bob Skills (`.bob/skills/`) - 5 total
1. ✅ **skill-creator** - Anthropic format (installed 2026-06-21)
2. ✅ **wrap-up** - Anthropic format (installed 2026-06-21)
3. ✅ **launch-agent** - Anthropic format (installed 2026-06-21)
4. ❌ **gcp-vm-wave-execution** - V12 format (needs migration)
5. ❌ **lamport-clock-recovery** - V12 format (needs migration)

### Plugin Skills (`plugins/`) - 11 total
1. ❌ **architecture-validation** - V12 format (needs migration)
2. ❌ **scope-boundary-check** - V12 format (needs migration)
3. ❌ **parallel-epic-execution** - V12 format (needs migration)
4. ❌ **check-pr** - V12 format (needs migration)
5. ❌ **pr-loop-auto** - V12 format (needs migration)
6. ❓ **bobcoin-account-switch** - Unknown status
7. ❓ **codebase-architecture** - Unknown status
8. ❓ **frontend-design** - Unknown status
9. ❓ **github-migration** - Unknown status
10. ❓ **multi-agent-orchestrator** - POC status
11. ❓ **WAVE2_SHELL_WORKAROUND** - Unknown status

## Anthropic Skill Format Requirements

### YAML Frontmatter (Required)
```yaml
---
name: skill-name
description: When to trigger, what it does. Include both what the skill does AND specific contexts for when to use it. All "when to use" info goes here, not in the body.
version: 1.0.0 (optional)
dependencies: Required tools, dependencies (optional)
---
```

### Key Differences from V12 Format

| Aspect | V12 Format | Anthropic Format |
|--------|-----------|------------------|
| **Frontmatter** | Plain text `name:` and `description:` | YAML block with `---` delimiters |
| **Description** | Brief, technical | Detailed, includes triggering contexts |
| **Structure** | Freeform markdown | Progressive disclosure (metadata → body → resources) |
| **Size** | No limit | <500 lines ideal for SKILL.md body |
| **Resources** | Inline | Separate `scripts/`, `references/`, `assets/` dirs |

## Migration Strategy

### Phase 1: Migrate Active Skills (Priority 1)
**Target**: 7 active skills used in 10-phase workflow

1. **gcp-vm-wave-execution** (.bob/skills/)
   - Used in: Phase 0, Phase 5, Autonomous Refactor
   - Priority: P0 (critical for wave execution)
   
2. **architecture-validation** (plugins/)
   - Used in: Phase 2
   - Priority: P1 (architectural decisions)
   
3. **scope-boundary-check** (plugins/)
   - Used in: Phase 1.5
   - Priority: P1 (scope validation gate)
   
4. **parallel-epic-execution** (plugins/)
   - Used in: Phase 5
   - Priority: P1 (concurrent ticket execution)
   
5. **lamport-clock-recovery** (.bob/skills/)
   - Used in: Wave 7 recovery scenarios
   - Priority: P2 (recovery tool)
   
6. **check-pr** (plugins/)
   - Used in: Phase 5.V, Phase 6
   - Priority: P2 (PR validation)
   
7. **pr-loop-auto** (plugins/)
   - Used in: Phase 5.V, Phase 6
   - Priority: P2 (PR automation)

### Phase 2: Investigate Unknown Skills (Priority 2)
**Target**: 6 skills with unknown status

1. **bobcoin-account-switch** - Determine if still needed
2. **codebase-architecture** - May be duplicate of architecture-validation
3. **frontend-design** - Assess relevance to V12 workflow
4. **github-migration** - One-time use? Archive?
5. **multi-agent-orchestrator** - POC status, production-ready?
6. **WAVE2_SHELL_WORKAROUND** - Still needed post-Wave 2?

## Migration Process (Per Skill)

### Step 1: Backup Original
```bash
cp plugins/skill-name/SKILL.md plugins/skill-name/SKILL.md.v12-backup
```

### Step 2: Add YAML Frontmatter
```yaml
---
name: skill-name
description: [Extract from current description + add triggering contexts]
version: 1.0.0
dependencies: [List any MCP servers, tools, or prerequisites]
---
```

### Step 3: Enhance Description
- Include WHEN to use (triggering contexts)
- Include WHAT it does (capabilities)
- Make it "pushy" (Anthropic recommendation to combat undertriggering)

**Example**:
```yaml
# Before (V12)
description: Systematic architectural validation using jCodemunch tools

# After (Anthropic)
description: Systematic architectural validation between planning and implementation using jCodemunch tools to prevent circular dependencies, coupling degradation, layer violations, and unclear interface contracts. Use when an epic touches >3 files, introduces new abstractions, modifies public APIs, or changes cross-file dependencies. Also use when the user mentions "architecture review", "dependency analysis", "coupling metrics", or "layer violations".
```

### Step 4: Organize Resources
If skill has >500 lines or includes code/scripts:
```
skill-name/
├── SKILL.md (main instructions, <500 lines)
├── scripts/ (executable code)
├── references/ (docs loaded as needed)
└── assets/ (templates, files used in output)
```

### Step 5: Test with skill-creator
```bash
# Validate format
python .bob/skills/skill-creator/scripts/quick_validate.py plugins/skill-name/

# Optional: Run evals if skill has test cases
python .bob/skills/skill-creator/scripts/run_eval.py \
  --skill-path plugins/skill-name/ \
  --eval-set plugins/skill-name/evals/evals.json
```

### Step 6: Move to .bob/skills/ (Optional)
For skills that should be auto-loaded:
```bash
mv plugins/skill-name .bob/skills/
```

**Decision Criteria**:
- **Keep in plugins/**: Skill is V12-specific, needs explicit `@skill` reference
- **Move to .bob/skills/**: Skill is general-purpose, should auto-load

## Custom Modes Integration

### Current State (.bob/custom_modes.yaml)
```yaml
# No explicit skill references currently
# Skills are implicitly used via agent knowledge
```

### Target State (Explicit References)
```yaml
- slug: v12-phase0-hotspot
  name: V12 Phase 0 Hotspot Analyzer
  skills:
    - "@.bob/skills/launch-agent"
    - "@.bob/skills/gcp-vm-wave-execution"
    - "@plugins/WAVE2_SHELL_WORKAROUND.md"
  # ... rest of mode config

- slug: v12-phase1-5-boundary
  name: V12 Phase 1.5 Boundary Validator
  skills:
    - "@plugins/scope-boundary-check/SKILL.md"
  # ... rest of mode config

- slug: v12-phase2-architecture
  name: V12 Phase 2 Architecture Planner
  skills:
    - "@plugins/architecture-validation/SKILL.md"
    - "@plugins/codebase-architecture/SKILL.md"  # if not duplicate
  # ... rest of mode config

- slug: v12-engineer
  name: V12 Photon Engineer
  skills:
    - "@.bob/skills/gcp-vm-wave-execution"
    - "@plugins/parallel-epic-execution/SKILL.md"
  # ... rest of mode config

- slug: v12-phase5-v-verify
  name: V12 Phase 5.V Verifier
  skills:
    - "@.bob/skills/wrap-up"
    - "@plugins/check-pr/SKILL.md"
    - "@plugins/pr-loop-auto/SKILL.md"
  # ... rest of mode config

- slug: v12-phase6-review
  name: V12 Phase 6 Final Reviewer
  skills:
    - "@.bob/skills/wrap-up"
    - "@plugins/check-pr/SKILL.md"
    - "@plugins/pr-loop-auto/SKILL.md"
  # ... rest of mode config

- slug: autonomous-refactor
  name: 🤖 Autonomous Refactor
  skills:
    - "@.bob/skills/launch-agent"
    - "@.bob/skills/gcp-vm-wave-execution"
    - "@.bob/skills/wrap-up"
    - "@plugins/bobcoin-account-switch/SKILL.md"  # if still needed
  # ... rest of mode config
```

## Implementation Timeline

### Week 1: Critical Skills (P0)
- [ ] Day 1-2: Migrate gcp-vm-wave-execution
- [ ] Day 3: Test with Phase 0, Phase 5, Autonomous Refactor modes
- [ ] Day 4-5: Migrate architecture-validation, scope-boundary-check

### Week 2: Active Skills (P1)
- [ ] Day 1-2: Migrate parallel-epic-execution, lamport-clock-recovery
- [ ] Day 3-4: Migrate check-pr, pr-loop-auto
- [ ] Day 5: Update .bob/custom_modes.yaml with explicit references

### Week 3: Investigation & Cleanup (P2)
- [ ] Day 1-3: Investigate 6 unknown skills
- [ ] Day 4: Archive obsolete skills
- [ ] Day 5: Update integration matrix to V2.3

### Week 4: Testing & Documentation
- [ ] Day 1-2: Run full 10-phase workflow with new skills
- [ ] Day 3: Test autonomous-refactor mode
- [ ] Day 4: Update SKILLS_INVENTORY.md
- [ ] Day 5: Final documentation and handoff

## Success Criteria

### Per-Skill Migration
- ✅ YAML frontmatter present and valid
- ✅ Description includes triggering contexts
- ✅ Body <500 lines (or resources extracted)
- ✅ Validates with skill-creator quick_validate.py
- ✅ Works in target custom mode(s)

### Overall Integration
- ✅ All 10 phases reference skills explicitly
- ✅ Autonomous-refactor mode references skills explicitly
- ✅ Integration matrix V2.3 updated
- ✅ SKILLS_INVENTORY.md reflects new structure
- ✅ Test epic completes successfully with new skills

## Rollback Plan

If migration causes issues:
1. Restore from `.v12-backup` files
2. Remove explicit skill references from .bob/custom_modes.yaml
3. Revert to implicit skill usage
4. Document failure in plugins/SKILL_MIGRATION_FAILURE_ANALYSIS.md

## Post-Migration Maintenance

### Skill Self-Improvement Protocol
After every skill use, agents MUST:
1. Check if instruction was ambiguous or produced unexpected result
2. Update SKILL.md if gap found
3. State `skill(name): no gaps identified` if no gap found
4. Skipping post-use audit = protocol violation

### Skill Creation Protocol
For new skills:
1. Use skill-creator tool (not manual authoring)
2. Follow Anthropic format from start
3. Add to appropriate location (.bob/skills/ or plugins/)
4. Update custom modes with explicit reference
5. Update SKILLS_INVENTORY.md

## References

- Anthropic Skills Spec: https://agentskills.io/specification
- skill-creator SKILL.md: `.bob/skills/skill-creator/SKILL.md`
- Optimal Setup Plan: `plugins/OPTIMAL_SKILL_SETUP.md`
- Skills Inventory: `plugins/SKILLS_INVENTORY.md`
- Integration Matrix V2.2: `docs/workflow/AUTONOMOUS_REFACTOR_INTEGRATION_MATRIX_V2.md`

## Next Steps

1. **Immediate**: Migrate gcp-vm-wave-execution (P0 critical)
2. **This Week**: Migrate architecture-validation, scope-boundary-check (P1)
3. **Next Week**: Complete remaining active skills, update custom modes
4. **Following Week**: Investigate unknown skills, update documentation

---

**Status**: Ready for execution  
**Owner**: Autonomous Refactor Agent  
**Last Updated**: 2026-06-21