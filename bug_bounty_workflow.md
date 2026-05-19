# Bug Bounty Workflow
## V12 Photon Kernel -- 7-Cluster Parallel Bug Hunt

> **Version**: 1.0
> **Status**: Planned -- executes AFTER all 7 clusters have 100% test coverage
> **Bob Command**: `/bug-bounty`
> **Universal Workflow**: `.agent/workflows/bug-bounty.md`
> **Last Updated**: 2026-05-16

---

## Purpose

After the Testing Setup Epic establishes 100% test coverage across all 7 clusters (67 src files),
this workflow dispatches 7 parallel sub-agents -- one per cluster -- to perform a deep, focused
bug hunt. Each agent operates with single-cluster context to maximize signal quality and minimize
cross-cluster noise. The Orchestrator consolidates all findings, validates accuracy, and filters
hallucinations before the output feeds the `/epic-tdd` repair workflow.

**Prerequisite**: All 7 cluster test suites MUST be complete before running this workflow.
Tests serve as the regression safety net during repairs.

---

## Stage 1: Parallel Dispatch (Bob Orchestrator Mode)

Bob dispatches 7 sub-agents simultaneously, one per cluster:

| Agent | Cluster | Scope | Output |
|:------|:--------|:------|:-------|
| Agent-S1 | SIMA Core | 7 files | `docs/brain/bug_report_s1.md` |
| Agent-S2 | Execution Engine | 16 files | `docs/brain/bug_report_s2.md` |
| Agent-S3 | UI & Photon IO | 16 files | `docs/brain/bug_report_s3.md` |
| Agent-S4 | REAPER Defense | 5 files | `docs/brain/bug_report_s4.md` |
| Agent-S5 | Kernel State | 5 files | `docs/brain/bug_report_s5.md` |
| Agent-S6 | Signals & Entries | 7 files | `docs/brain/bug_report_s6.md` |
| Agent-S7 | Kernel Infrastructure | 11 files | `docs/brain/bug_report_s7.md` |

### Per-Agent Bug Hunt Protocol

Each agent operates in **Plan mode** (read-only, no src/ edits) and must:

1. **Structural Scan** (jCodemunch):
   - `get_file_outline` on all cluster files
   - `get_blast_radius` on high-complexity methods
   - `find_references` on all shared state mutations
   - `get_dependency_graph` for cross-file coupling

2. **Pattern Audit** (AST):
   - Race conditions: shared state accessed from multiple code paths without atomic guards
   - Use-after-free windows: resource released before all references cleared
   - Re-entrancy: callbacks triggered inside critical sections
   - Ghost order windows: async ID registration before submission completes
   - FSM state leaks: incomplete reset during cancel/error paths
   - Null ref hot paths: missing null checks before property access
   - O(N^2) loops: nested iterations on fleet/account lists
   - Semaphore leaks: missing finally blocks on toggle release
   - Wildcard Logic & Architectural Anomalies: Leverage your unconstrained reasoning capacity to identify deep structural vulnerabilities or subtle bugs violating the V12 Platinum Standard (even if they fall completely outside this checklist).

3. **DNA Compliance Check**:
   - Any remaining `lock()` statements
   - Non-ASCII in string literals
   - `Thread.Sleep()` in hot path
   - `Dictionary<K,V>` writes without atomic guard

4. **Bug Report Format** per finding:
   ```
   BUG-[CLUSTER]-[NNN]
   Title: [short description]
   Severity: Critical / High / Med / Low
   Location: [file].[method]
   Root Cause: [exact mechanism]
   Evidence: [line range or pattern]
   Test Impact: [which existing test would catch this if the test existed]
   ```

---

## Stage 1.5: Mid-Task Recovery & Checkpointing Protocol

Since the 7-cluster scan can be run sequentially or parallelly in the foreground:
- **Zero Work Loss**: Each of the 7 cluster agents writes its findings physically to disk (`docs/brain/bug_report_s[1-7].md`) immediately upon completing its single-cluster scan.
- **Limit Halts**: If Qwen CLI hits a usage limit during the sweep (e.g., at Agent S4):
  1. The work for S1, S2, and S3 is already fully saved on your disk.
  2. The next session or backup agent can read the existing reports, see that S1–S3 are complete, and skip them—resuming directly with Agent S4.
- **Qwen Checkpointing**: When running Qwen Code with `--checkpointing` or setting it to true in global `settings.json`, Qwen automatically saves a shadow Git snapshot and conversation history before tool calls. In case of session disconnection, you can list and recover conversation states using:
  ```bash
  /restore
  /restore <checkpoint_file>
  ```

---

## Stage 2: Orchestrator Consolidation

After all 7 agents report back, Bob Orchestrator runs the consolidation phase:

### 2a. Hallucination Filter
For each reported bug:
- Verify the cited file and method actually exist in src/
- Verify the cited line range matches actual code (cross-ref against jCodemunch index)
- Discard any finding where the evidence does not match src/ reality
- Mark filtered bugs as `[FILTERED: hallucination]` with reason

### 2b. Cross-Cluster Deduplication
- Identify bugs reported by multiple agents for the same root cause
- Consolidate into single canonical entry with all affected clusters noted
- Flag cross-cluster bugs as higher severity (blast radius spans clusters)

### 2c. Severity Ranking
Rank all validated bugs:
- **Critical**: Data corruption, race conditions, use-after-free
- **High**: FSM state leaks, ghost order windows, O(N^2) hot paths
- **Med**: Missing null guards, incomplete resets
- **Low**: Style violations, minor inefficiencies

### 2d. Consolidated Output
Write `docs/brain/cluster_bug_bounty_report.md` containing:
- Total validated bugs (by severity)
- Per-cluster breakdown
- Filtered/hallucination count (transparency)
- Recommended repair sequence (Critical first, then by cluster dependency order)
- Ready-to-use `/epic-tdd` ticket format for each validated bug

---

## Stage 3: Repair via Epic TDD

Use the consolidated report to drive `/epic-tdd` repairs, one cluster at a time:

```
cluster_bug_bounty_report.md
         |
  [Director selects cluster to repair]
         |
/epic-tdd (with bug report as the ticket source)
  P2 Forensics   -> validates bug evidence against src/
  P3 Architect   -> designs repair preserving caller invariants
  P4 Adjudicator -> confirms no logic drift
  P5 Engineer    -> RED test (reproduces bug), GREEN (fix), deploy-sync
  P6 Verifier    -> full test suite confirms fix + no regressions
         |
  Next cluster
```

**Key constraint**: Existing cluster test suite acts as regression net.
Any repair that breaks a passing test is a logic drift -- HALT and re-examine.

---

## Bob Mode Usage

| Phase | Bob Mode | Purpose |
|:------|:---------|:--------|
| Stage 1 Dispatch | Orchestrator | Spawn 7 parallel cluster agents |
| Per-agent hunt | Ask/Plan | Read-only forensic scan |
| Stage 2 Consolidation | Plan | Validate, filter, rank findings |
| Stage 3 Repair | Advanced/Code (via /epic-tdd) | Surgical fixes with TDD gate |

---

## Workflow Sequence (Full Picture)

```
[Testing Setup Epic]  (current task)
  7 clusters get 100% test coverage
  67 src files covered: unit + property + integration
         |
[Bug Bounty Workflow]  (this document -- next task)
  Bob dispatches 7 parallel agents
  Each hunts bugs in single-cluster context
  Orchestrator consolidates + filters hallucinations
  Output: cluster_bug_bounty_report.md
         |
[Epic TDD Repair]  (repair phase)
  /epic-tdd per bug, one cluster at a time
  Tests catch regressions in real time
```

---

**Document Owner**: Antigravity Orchestrator
**Bob Command**: `.bob/commands/bug-bounty.md`
**Universal Workflow**: `.agent/workflows/bug-bounty.md`
**Prerequisite**: `docs/brain/epic_tdd_workflow.md` (testing setup must be complete first)
**Linked Manifesto Entry**: `docs/brain/V12_Workflow_Manifesto.md` Section 5
