# Slash Command to Custom Mode Mapping

**Date**: 2026-06-21  
**Purpose**: Document how slash commands map to custom modes and which skills each uses  
**Context**: Skills integration for Wave 7 preparation

---

## Command-to-Mode Mapping

| Slash Command | Phase | Custom Mode | Skills Needed |
|---------------|-------|-------------|---------------|
| `/epic-intake` | 0 & 1 | `v12-phase0-hotspot` → `v12-phase1-scope` | launch-agent, gcp-vm-wave-execution, WAVE2_SHELL_WORKAROUND |
| `/epic-scope-boundary` | 1.5 | `v12-phase1-5-boundary` | scope-boundary-check |
| `/epic-plan` | 2 | `v12-phase2-architecture` | architecture-validation, codebase-architecture |
| `/epic-scan` | 2.3 | `v12-phase3-audit` (or similar) | None (pure MCP) |
| `/epic-validate` | 3 | `v12-phase3-audit` | None (pure MCP) |
| `/epic-tickets` | 4 | `v12-phase4-tickets` | None (pure MCP) |
| `/epic-review-tickets` | 4.5 | `v12-phase4-5-review` | None (Sequential Thinking only) |
| `/epic-validate` (ticket) | 5 | `v12-engineer` | gcp-vm-wave-execution, parallel-epic-execution |
| `/epic-verify-ticket` | 5.V | `v12-phase5-v-verify` | wrap-up, check-pr, pr-loop-auto |
| `/epic-review-final` | 6 | `v12-phase6-review` | wrap-up, check-pr, pr-loop-auto |
| `/autonomous-refactor` | Orchestrator | `autonomous-refactor` | launch-agent, gcp-vm-wave-execution, wrap-up, bobcoin-account-switch |

---

## Key Insights

### 1. Slash Commands Are Workflow Orchestrators
Slash commands don't specify modes explicitly. Bob IDE routes them to the appropriate custom mode based on:
- Command name pattern (e.g., `/epic-plan` → Phase 2 → `v12-phase2-architecture`)
- Workflow context (which phase is active)
- Manifest state (which dependencies are satisfied)

### 2. Skills Are Mode-Level, Not Command-Level
Skills are attached to **custom modes**, not slash commands. When a slash command triggers a mode, that mode's skills become available.

### 3. No Changes Needed to Slash Commands
Slash commands don't need skill references because:
- They delegate to custom modes
- Custom modes have the skill references
- Bob IDE auto-loads skills from `.bob/skills/`

---

## Workflow Example

```bash
# User types:
/epic-intake EPIC-W7-001 "Reduce CalculateScore complexity"

# Bob IDE:
1. Parses command → identifies Phase 0 & 1
2. Loads custom mode: v12-phase0-hotspot
3. Auto-loads skills from mode's skills: array:
   - @.bob/skills/launch-agent
   - @.bob/skills/gcp-vm-wave-execution
   - @plugins/multi-agent-orchestrator/WAVE2_SHELL_WORKAROUND.md
4. Executes Phase 0 with those skills available
5. Transitions to v12-phase1-scope (no explicit skills)
6. Completes and waits for next command
```

---

## Action Items

### ✅ Already Complete
- Slash commands cleaned of Greptile references
- Custom modes defined for all 10 phases
- Skills installed (3 Anthropic + 2 Bob + 11 plugins)

### 🔄 In Progress
- Add explicit `skills:` references to `.bob/custom_modes.yaml`

### ⏳ Pending
- Test skill loading in Bob IDE
- Migrate P0 skills to Anthropic format
- Update Integration Matrix V2.3

---

## Conclusion

**No changes needed to slash commands.** They're workflow orchestrators that delegate to custom modes. All skill integration happens at the custom mode level in `.bob/custom_modes.yaml`.

The user's concern about "slash commands might need to be updated too" is addressed by understanding that:
1. Slash commands → trigger custom modes
2. Custom modes → have skill references
3. Skills → auto-load when mode activates

**Next Step**: Complete the `.bob/custom_modes.yaml` update with explicit skill references.