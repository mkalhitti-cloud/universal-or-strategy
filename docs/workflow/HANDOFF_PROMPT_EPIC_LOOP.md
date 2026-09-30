# Handoff Prompt for Epic Loop Autonomous Execution

---

## Context Summary

**Session Goal**: Execute Epic 51 autonomously using the triple-nested loop architecture

**Current State**:
```
Repository: universal-or-strategy
Branch: main (clean, no uncommitted changes)
Last Commit: 0b3d7dc2 "feat(protocol): register GitButler after_task hook"
Build Status: ✅ Passing (0 errors, 0 warnings)
Hard Links: ✅ Synced (81/81 files)
Pre-Push: ✅ 11/11 checks passed
```

**Validation Complete**:
- ✅ Triple-nested loop protocols active (epic-loop → epic-run → pre-push/pr-loop)
- ✅ GitButler workspace model configured
- ✅ Jane Street KB integration ready (pre_session.py hook)
- ✅ Quality gates validated (11/11 pre-push checks)
- ✅ All protocols documented

---

## Triple-Nested Loop Architecture

### Outer Loop: `/epic-loop`
**Purpose**: Process 31 methods (CYC 15-20 → ≤8) via EPIC-CCN-15 through EPIC-CCN-45

**Location**: `.bob/commands/epic-loop.md`

**Protocol**:
1. Pre-flight validation (GODMODE checks)
2. For each epic: delegate to `/epic-run`
3. Post-loop verification (complexity audit)

### Middle Loop: `/epic-run`
**Purpose**: Execute single epic end-to-end (6 phases)

**Location**: `.bob/commands/epic-run.md`

**Phases**:
- Phase 0: Hotspot Analysis (CodeScene)
- Phase 1: Intake (scope definition)
- Phase 2: Plan (analysis + approach)
- Phase 2.3: Scan (Greptile semantic audit)
- Phase 3: Validate (stress-test approach)
- Phase 4: Tickets (breakdown into executable units)
- Phase 5: Execution (ticket loop with nested quality gates)
- Phase 6: PR submission + perfection

### Inner Loop 1: `/pre-push`
**Purpose**: 11 local quality checks before every push

**Location**: `scripts/pre_push_validation.ps1`

**Checks**: ASCII, Build, Tests, Lint, Format, Security, Links, Hygiene, Complexity, Dead Code, Codacy

**Status**: ✅ 11/11 checks passed (validated)

### Inner Loop 2: `/pr-loop`
**Purpose**: Drive PR to 100/100 PHS autonomously

**Location**: `.bob/commands/pr-loop.md`

**Protocol**: V2 with Bot Forensics + Jane Street Audit

**Steps**:
1. Extract bot findings (categorize: VALID-FIX, VALID-SUPPRESS, HALLUCINATION)
2. Apply fixes autonomously
3. Run pre-push validation
4. Push and monitor bots
5. Repeat until PHS 100/100

---

## GitButler Workspace Model

**Permanent Workspace**: `gitbutler/workspace`
- Never leave workspace (no branch switching)
- Virtual branches for parallel work
- Zero context loss across sessions
- Zero merge conflicts

**Two-Track Merge Strategy**:
- `.cs files` (src/, tests/, benchmarks/) → PR required
- `non-.cs files` (docs/, scripts/, .bob/) → Direct merge to main

**Hook Integration**:
- `after_task` hook registered in `.bob/settings.json`
- Auto-commit to virtual branches
- File-type-based routing
- PR creation automation

---

## Epic 51 Details

**Target**: `HydrateWorkingOrdersFromBroker` method
**Current CYC**: 79
**Target CYC**: ≤19 (76% reduction)

**5 Tickets Created**:
1. Fleet Order Adoption - CYC reduction ~25 points
2. Shared Helper Extraction - Eliminates duplication
3. Fleet Position Reconstruction - CYC reduction ~10 points
4. Master Order Adoption - CYC reduction ~15 points
5. FSM Hydration Orchestration - Includes Greptile safety repairs

**Planning Complete**: `docs/brain/EPIC-CCN-51/`
- 00-scope.md
- 01-analysis.md
- 02-approach.md
- 02-greptile-report.md
- ticket-01-*.md through ticket-05-*.md
- EXECUTION_GUIDE.md

---

## Autonomous Execution Protocol

### Your Role
You are the V12 Epic Orchestrator running in **Orchestrator mode**. You coordinate the entire epic lifecycle by delegating to specialized modes.

### Execution Flow

**Step 1: Start Epic Loop**
```bash
/epic-loop 51 51
```

**Step 2: Phase 0 - Hotspot Analysis**
- Switch to Advanced mode
- Run: `python scripts/epic_planner.py`
- Present top 5 candidates
- Await Director confirmation

**Step 3: Phases 1-4 - Planning**
- Switch to v12-epic-planner mode for each phase
- Run: `/epic-intake`, `/epic-plan`, `/epic-scan`, `/epic-validate`, `/epic-tickets`
- Stop at each gate for Director approval

**Step 4: Phase 5 - Ticket Execution Loop**
For each of 5 tickets:
1. Switch to v12-engineer mode
2. Run: `/ticket docs/brain/EPIC-CCN-51/ticket-XX-*.md`
3. Switch to Advanced mode for verification
4. Run: `powershell -File .\scripts\pre_push_validation.ps1`
5. **F5 Gate** (human verification - ONLY manual step)
6. Switch to Advanced mode for commit
7. Switch to Orchestrator mode for `/pr-loop <PR_NUMBER>`
8. Drive to 100/100 PHS autonomously
9. Merge PR and continue to next ticket

**Step 5: Phase 6 - Epic Completion**
- Submit final PR
- Run `/pr-loop` to 100/100 PHS
- Merge and emit [EPIC-COMPLETE]

### Human Intervention Points

**F5 Verification** (5 times - once per ticket):
```
[F5-GATE] Ticket XX - All automated gates PASSED
ACTION REQUIRED: Press F5 in NinjaTrader IDE.
When you see the BUILD_TAG banner, type: F5 done [BUILD_TAG]
```

**Strategic Approvals** (at planning gates):
- Gate 0: Confirm hotspot target
- Gate 1: Confirm scope
- Gate 2: Approve plan
- Gate 2.3: Approve sentinel audit
- Gate 3: Approve validation
- Gate 4: Approve tickets

**Everything Else**: Fully autonomous

---

## Quality Gates

### Pre-Push Validation (11 checks)
✅ ASCII Gate: All source files ASCII-clean
✅ Build: Linting.csproj compiled successfully
✅ Unit Tests: All tests passed
✅ Lint: No violations
✅ Formatting: All files properly formatted
✅ Security: Gitleaks + Snyk
✅ Markdown Links: All valid
✅ PR Hygiene: Diff size + rebase check
✅ Hard Links: 81/81 synchronized
✅ Codacy Preview: Optional
✅ CodeScene Delta: Optional

### PR Loop (100/100 PHS)
1. Bot Forensics Extraction
2. Jane Street Audit (VALID-FIX vs VALID-SUPPRESS)
3. Autonomous Fix Application
4. Pre-Push Validation
5. Push + Monitor Bots
6. Repeat until PHS 100/100

---

## Jane Street KB Integration

**Status**: ⚠️ Firebase credentials missing (expected)
- Hook: `.bob/hooks/pre_session.py`
- Auto-load: On every Bob CLI session start
- Fallback: Graceful degradation if unavailable
- Rules: Auto-generated to `.bob/rules-v12-engineer/99-jane-street-auto.md`

**Key Principles**:
- Correctness by Construction (make illegal states unrepresentable)
- Lock-Free Concurrency (FSM/Actor Enqueue model)
- Cognitive Simplicity (CYC ≤ 8)
- Immutable Data (readonly structs)
- Explicit Error Handling (no exceptions in hot paths)

---

## MCP Servers Available

### jcodemunch-mcp
**Tools**: 50+ code navigation tools
- `search_symbols`, `get_symbol_source`, `get_blast_radius`
- `find_references`, `get_dependency_graph`
- `get_call_hierarchy`, `get_impact_preview`

### greptile
**Tools**: 17 semantic analysis tools
- `list_custom_context`, `search_custom_context`
- `list_merge_requests`, `get_merge_request`
- `list_code_reviews`, `get_code_review`

### sequential-thinking
**Tool**: `sequentialthinking` (multi-step reasoning)

---

## Success Metrics

**Epic 51 Completion Criteria**:
- ✅ 5/5 tickets completed successfully
- ✅ HydrateWorkingOrdersFromBroker CYC 79 → ≤19
- ✅ Zero new Jane Street violations
- ✅ All quality gates passing (11/11 pre-push + 100/100 PHS)
- ✅ Hard links synchronized (81/81 files)
- ✅ Documentation updated

**CodeScene Target**: All files → 8+ Code Health Score

**Greptile Target**: 5/5 review score

---

## Error Recovery

### Epic Failure Protocol
1. STOP immediately
2. Analyze failure: `docs/brain/EPIC-CCN-51/failure-analysis.md`
3. Fix root cause
4. Restart: `/epic-loop 51 51`

### Common Failure Modes
- Scope creep → Separate PRs, restart epic
- Compilation errors → Fix separately, restart epic
- Jane Street violations → Revert, apply patterns
- Complexity regression → Re-plan with stricter target

---

## Ready to Execute

**Confirmation Required**:
1. Read this entire handoff prompt
2. Confirm you understand the triple-nested loop architecture
3. Confirm you're ready to execute autonomously
4. Reply: "READY TO EXECUTE EPIC 51"

**Then I will run**:
```bash
/epic-loop 51 51
```

**And you will**:
- Orchestrate all 6 phases
- Delegate to specialized modes
- Run nested quality loops
- Wait for F5 verification only
- Drive to 100/100 PHS autonomously
- Complete Epic 51 end-to-end

---

**All protocols validated. All systems ready. Awaiting your confirmation to begin autonomous execution.** 🚀