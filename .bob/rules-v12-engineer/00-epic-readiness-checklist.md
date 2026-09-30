# Epic Readiness Checklist (V12.22)

## MANDATORY: Read Before Starting ANY EPIC

This checklist ensures Bob Shell (`v12-engineer` mode) has full protocol awareness before executing any EPIC work.

---

## ✅ Protocol Awareness Verification

Before starting EPIC work, Bob Shell MUST confirm awareness of:

### 1. Branch Strategy (Three-Tier Model)

**Source Code Branches** (`feature/src-epic-*`):
- ✅ ONLY `.cs` files in `src/` directory
- ✅ ONLY `.csproj` files (if needed)
- ❌ NO docs, scripts, markdown, configs, or any non-source files
- ✅ Full PR review required (Arena AI + Codacy + CodeRabbit)
- ✅ Complexity audit enforced (CYC ≤ 15)

**Key Files:**
- `docs/protocol/BRANCH_STRATEGY.md` (lines 8-37)
- `docs/protocol/SRC_ONLY_PUSH.md` (complete protocol)
- `.bob/rules-v12-engineer/branch-guard.md` (enforcement rules)

**Violation Protocol:**
- If ANY non-.cs file is staged → ABORT commit immediately
- Stash non-conforming files: `git stash push -m "infra-changes" -- <files>`
- Report to Director for separate infra branch

### 2. GitHub Push Protocol (Src-Only)

**Standard Workflow:**
```powershell
# 1. Verify hygiene (checks src/ only)
powershell -File .\scripts\verify_pr_hygiene.ps1

# 2. Stage ONLY src/ files
git add src/

# 3. Commit with descriptive message
git commit -m "feat: implement X in src/Y.cs"

# 4. Push for PR review
git push origin feature/src-epic-X
```

**NEVER:**
- ❌ `git add .` (adds everything)
- ❌ Push docs, scripts, brain files, or configs
- ❌ Mix src/ and non-src/ changes in same commit

**Key File:**
- `docs/protocol/SRC_ONLY_PUSH.md` (lines 24-40)

### 3. V12 DNA Constraints (Non-Negotiable)

**Architectural Mandates:**
1. **No Internal Locks**: `lock(stateLock)` is BANNED. Use FSM/Actor `Enqueue` or atomic primitives.
2. **ASCII-Only**: No Unicode, emoji, or curly quotes in C# strings.
3. **Surgical Splits**: Use `scripts/v12_split.py` for splits >50 lines.
4. **FSM-Driven**: Follower order cancel+resubmit MUST use Replace FSM.
5. **Post-Edit Deployment**: Run `powershell -File .\deploy-sync.ps1` after EVERY src/ edit.
6. **Complexity Standards**: CYC < 20 per method, LOC ≥ 15 for extractions.
7. **Zero Logic Drift**: Pure structural movement only during extraction.

**Key File:**
- `.bob/rules-v12-engineer/dna.md` (complete DNA)

### 4. Jane Street Integration

**Mandatory Reading:**
- `docs/intel/jane-street/` directory (HFT patterns, testing standards)
- Query before complex design: `python scripts/query_kb.py "<term>"`

**Alignment Principles:**
- Cognitive simplicity over clever abstractions
- CYC ≤ 15 (microsecond-latency reasoning)
- Make illegal states unrepresentable
- Exhaustive testing (exponential path growth prevention)

**Key File:**
- `.bob/rules-v12-engineer/99-jane-street-auto.md`

### 5. Pre-Push Validation (13 Checks)

**MANDATORY before EVERY push:**
```powershell
# Fast mode (skip slow checks 10-13)
powershell -File .\scripts\pre_push_validation.ps1 -Fast

# Full mode (all 13 checks)
powershell -File .\scripts\pre_push_validation.ps1
```

**Blocking Checks:**
1. ASCII-Only (zero non-ASCII)
2. Build (zero errors)
3. Unit Tests (100% pass)
4. Lint (zero violations)
5. Formatting (zero issues)
6. PR Hygiene (diff <10k chars)
7. Complexity (CYC ≤ 15)

**Warning Checks:**
8. Security (Gitleaks + Snyk)
9. Markdown Links
10. Dead Code
11. Codacy Preview
12. Semgrep
13. CodeRabbit AI (WARNING until 2026-06-09, then BLOCKING)

**Key Reference:**
- `AGENTS.md` (lines 95-150)

### 6. Parallel Execution Awareness

**Subgraph Isolation:**
- ✅ 2 EPICs can run in parallel IF in different subgraphs
- ❌ NEVER run 2 EPICs in same subgraph simultaneously
- ❌ NEVER run 2 EPICs that modify the same file

**Branch Naming:**
- Pattern: `feature/src-epic-{N}-{subgraph}-{description}`
- Example: `feature/src-epic-8-s1-extract-sima`

**Key File:**
- `docs/protocol/BRANCH_STRATEGY.md` (lines 145-257)

### 7. Telemetry Collection

**Automatic Collection:**
- All Bob Shell sessions are wrapped by `agent_session_wrapper.py`
- Data flows to 4 backends: Phoenix, LangSmith, Firebase, Obsidian
- Zero manual intervention required

**Key Files:**
- `docs/protocol/V12_TELEMETRY_SETUP.md`
- `scripts/agent_session_wrapper.py`

---

## 🚀 Epic Execution Checklist

Before starting EPIC work, Bob Shell MUST:

- [ ] Confirm current branch matches pattern `feature/src-epic-*`
- [ ] Verify no non-.cs files are staged (`git status`)
- [ ] Read EPIC ticket brief from `docs/brain/epic-*/ticket-*.md`
- [ ] Verify all line numbers and CYC values against live `src/`
- [ ] Produce written PLAN before any edit
- [ ] Execute surgical edits ONLY (no scope creep)
- [ ] Run `python scripts/complexity_audit.py` after edits
- [ ] Run `powershell -File .\deploy-sync.ps1` (ASCII gate MUST pass)
- [ ] Bump `BUILD_TAG` in `src/V12_002.cs`
- [ ] Stage ONLY `src/` files (`git add src/`)
- [ ] Run `powershell -File .\scripts\pre_push_validation.ps1 -Fast`
- [ ] Commit with clear message: `feat: implement X in src/Y.cs`
- [ ] Push to branch: `git push origin feature/src-epic-X`
- [ ] Report to Director: files modified, CYC before/after, deploy-sync result

---

## 📋 Quick Reference Commands

**Pre-Work:**
```powershell
# Verify branch
git branch --show-current

# Check staged files
git status

# Query Jane Street KB
python scripts/query_kb.py "lock-free patterns"
```

**Post-Edit:**
```powershell
# Complexity audit
python scripts/complexity_audit.py

# Deploy sync (ASCII gate)
powershell -File .\deploy-sync.ps1

# Pre-push validation (fast)
powershell -File .\scripts\pre_push_validation.ps1 -Fast
```

**Push Workflow:**
```powershell
# Stage src/ only
git add src/

# Commit
git commit -m "feat: extract OnKeyDown handlers (CYC 45→12)"

# Push
git push origin feature/src-epic-8
```

---

## ⚠️ Common Violations to Avoid

1. **Mixed File Types**: Staging docs/scripts with src/ files
2. **Skipping Pre-Push**: Pushing without running validation
3. **Logic Drift**: "Improving" code during extraction
4. **Missing BUILD_TAG**: Forgetting to bump version
5. **Non-ASCII**: Using Unicode/emoji in strings
6. **Lock Usage**: Using `lock()` instead of FSM/Actor
7. **Vague Commits**: "update" instead of "feat: specific change"
8. **Skipping Deploy-Sync**: Not running after src/ edits

---

## 📚 Protocol File Index

| Protocol | File Path | Purpose |
|----------|-----------|---------|
| Branch Strategy | `docs/protocol/BRANCH_STRATEGY.md` | Three-tier model + parallel execution |
| Src-Only Push | `docs/protocol/SRC_ONLY_PUSH.md` | GitHub push protocol |
| Branch Guard | `.bob/rules-v12-engineer/branch-guard.md` | File type enforcement |
| V12 DNA | `.bob/rules-v12-engineer/dna.md` | Architectural constraints |
| Jane Street | `.bob/rules-v12-engineer/99-jane-street-auto.md` | HFT alignment |
| Pre-Push | `AGENTS.md` (lines 95-150) | 13-check validation |
| Telemetry | `docs/protocol/V12_TELEMETRY_SETUP.md` | Observability setup |

---

## ✅ Confirmation Statement

Before starting ANY EPIC, Bob Shell MUST state:

> "I confirm awareness of:
> 1. Three-tier branch strategy (src-only on feature/src-* branches)
> 2. GitHub push protocol (ONLY .cs files, no docs/scripts/configs)
> 3. V12 DNA constraints (no locks, ASCII-only, surgical splits)
> 4. Jane Street alignment (CYC ≤ 15, cognitive simplicity)
> 5. Pre-push validation (13 checks, -Fast mode minimum)
> 6. Subgraph isolation (parallel execution rules)
> 7. Telemetry collection (automatic, zero manual work)
>
> Ready to execute EPIC-X."

---

**Version:** V12.22  
**Effective Date:** 2026-05-31  
**Mandatory Compliance:** All Bob Shell sessions