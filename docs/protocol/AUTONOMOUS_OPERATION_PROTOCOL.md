# Autonomous Operation Protocol (V12.24)

**Effective**: 2026-06-02
**Approved By**: Director

## Scope

Once an epic plan is approved (Stages 0-3 complete with DNA audit PASS), all subsequent execution (Stage 4-6) proceeds autonomously without approval prompts for:

- Code modifications (within approved plan scope)
- File reads/writes
- Command executions (build, test, format, sync)
- Git operations (commit, push)
- Documentation updates

## Approval Gates (MANDATORY)

Agents MUST stop and request approval for:

1. **Plan Approval** (Stage 3): DNA audit results
2. **F5 Verification** (Stage 5): Integration testing in NinjaTrader
3. **Scope Changes**: Any deviation from approved plan
4. **Critical Decisions**: Architecture changes, API modifications, breaking changes

## Autonomous Execution Checklist

For each approved epic:
- [x] Stage 0-3: Complete with DNA audit PASS
- [ ] Stage 4: Execute implementation plan autonomously
- [ ] Stage 5: Stop for F5 verification
- [ ] Stage 6: Complete documentation autonomously

## Mode Behavior

- **v12-engineer**: Full autonomy for src/ modifications within approved plan
- **advanced**: Full autonomy for non-src modifications and documentation
- **orchestrator**: Coordinates autonomous execution, stops at approval gates

## Safety Constraints

- Pre-push validation MUST pass (13/13 checks)
- Hard link sync MUST succeed (81/81 files)
- Build MUST compile (zero errors)
- No scope creep (V12.23 mandate)

## Configuration Notes

**Extension Settings**: The actual approval mechanism is controlled by the Cline/Roo-Cline extension settings in VS Code, not by mode configuration files. The `.bob/custom_modes.yaml` defines mode capabilities, but approval prompts are managed at the extension level.

**Recommended Settings** (for autonomous operation):
- Auto-approve tool uses: Enabled for approved plans
- Checkpoint frequency: Every 5 tool uses
- Safety rollback: Enabled

## Workflow Integration

### Stage 4: Autonomous Execution
1. Agent reads approved implementation plan
2. Executes extraction/refactoring steps sequentially
3. Runs pre-push validation after each logical unit
4. Commits and pushes autonomously
5. Stops at F5 verification gate

### Stage 5: F5 Verification
1. Agent requests Director approval for F5 test
2. Director tests in NinjaTrader
3. If PASS: Agent proceeds to Stage 6
4. If FAIL: Agent enters fix loop

### Stage 6: Documentation
1. Agent updates completion documentation autonomously
2. Merges documentation to main (no PR for docs)
3. Presents completion summary

## Emergency Stop

Director can interrupt autonomous execution at any time via:
- `/stop` command
- Manual intervention in VS Code
- Git branch protection (if enabled)

## Audit Trail

All autonomous operations are logged in:
- Git commit history
- Session transcripts
- `docs/brain/EPIC-X/` completion documentation