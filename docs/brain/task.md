# ADR-020 Phase 4 Event Lifecycle Refactoring: Live Mission Dashboard

**Protocol Version**: V14 Alpha (Full Lifecycle Coverage)
**Target Build**: `Build 982` (M8/M9 Transition)
**Current Milestone**: `M8` (Distributed Optimization) / `M9` (Full Autonomy)
**Blackboard Sync**: [nexus_a2a.json](file:///C:/WSGTA/universal-or-strategy/docs/brain/nexus_a2a.json)

---

## 🛰️ Mission Progress Matrix

| Phase  | Role             | Purpose                        | Status                           |
| :----- | :--------------- | :----------------------------- | :------------------------------- |
| **P1** | **Orchestrator** | Central Switchboard            | 🟢 **ACTIVE** (ADR-020 Intake)   |
| **P2** | **Forensics**    | Logic Trace & Evidence         | 🟢 **ACTIVE** (Plan Validation)  |
| **P3** | **Architect**    | Structural Design              | ✅ **COMPLETE** (Plan Authored)  |
| **P4** | **Adjudicator**  | Red Team Arena Audit           | 🔴 **FAILED** (Type 2 Leaks)    |
| **P5** | **Engineer**     | Surgical Implementation        | ⚪ **WAITING**                   |
| **P6** | **Validator**    | Logic & AMAL Vetting           | ⚪ **WAITING**                   |
| **P7** | **Sentinel**     | GitHub / Security Audit        | ⚪ **WAITING**                   |

---

## 🛠️ Task Execution Log

### [/] P1: ORCHESTRATION & INTAKE

- [x] Initial ADR-020 Plan Review (docs/brain/implementation_plan.md)
- [x] Initialize Phase 4 Mission in nexus_a2a.json
- [x] Sync Implementation Plan to GitHub for Red Team Battle (Commit: `5110a1d`)
- [x] **Video Knowledge Synthesis** (Rithmic Hub + C++ Performance Principles)
- [x] Authored [strategic_synthesis_v12.md](file:///c:/WSGTA/universal-or-strategy/docs/brain/strategic_synthesis_v12.md)

### [x] P2: FORENSIC AUDIT (CONSOLIDATED)

- [x] Verify Section 2.1 Lifecycle Surface map
- [x] Audit OnBarUpdate pipeline stages for short-circuit safety

### [x] P3: ARCHITECTURAL DESIGN (CLAUDE)

- [x] Claude: Phase 4 Implementation Plan authored (ADR-020)
- [x] Plan includes Lifecycle.State.cs and Lifecycle.BarUpdate.cs scaffolds

### [/] P4: ADJUDICATION GATE (ARENA)

- [x] Prepare $redteambattle prompt (MODE A)
- [x] Generate Hallucination Canary (check_ascii vs byte_purge)
- [x] Launch Red Team Battle
- [x] Collect 3+ clean model verdicts
- [x] **Audit Analysis**: P4 FAILED. Type 2 Resource Leaks and Institutional Gaps identified.
- [ ] **Consolidated Redesign (P3 Architect)**: Rewrite implementation_plan.md to integrate:
    - [ ] 1. M1-M9 Overarching Mission (Droid Method).
    - [ ] 2. Forensic Fixes for FAILED ADR-019 Audit (Resource Leak Mitigation).
    - [ ] 3. 9 Category A Video Insights (Zero-Allocation, RAII, Structured Concurrency).


---

## 2. P4 ADJUDICATION (RED TEAM BATTLE)

**Mode**: MODE A (Plan Integrity Audit)
**Status**: [WAITING_FOR_ARENA_VERDICTS]
**Exhaustive Scope**: 100% (OnStateChange, OnBarUpdate, OnOrderUpdate, OnExecutionUpdate)

### 2.1 Battle Parameters
- **Source Commit**: `5110a1d`
- **Plan URL**: [implementation_plan.md](https://github.com/mkalhitti-cloud/universal-or-strategy/blob/5110a1d/docs/brain/implementation_plan.md)
- **Canary Fact**: `byte_purge.py` is confirmed absent; `check_ascii.py` is confirmed present.
- **Audit Mandate**: Verification of NT8 thread affinity, Enqueue compliance, and pipeline short-circuit logic.

### 2.2 Battle Prompt (MODE A)
> [!NOTE]
> Copy the following prompt into at least 3 Arena AI models (e.g., Claude 3.5 Sonnet, GPT-4o, Gemini 1.5 Pro).

```markdown
Do not use any web search. Answer from memory only for language knowledge.
For project-specific data, read the linked document.

Task: Build a React + Tailwind dashboard. You are a senior software architect
reviewing a technical design document for internal consistency before the
engineering team begins work. In the <title> and a visible <h2>, write your
model name and version.

Document (read before populating anything):
https://raw.githubusercontent.com/mkalhitti-cloud/universal-or-strategy/5110a1d/docs/brain/implementation_plan.md
If the linked document is not reachable, state "SOURCE UNAVAILABLE" and stop.

Consistency check (prove you read it): confirm whether check_ascii.py exists
at the repo root based on document evidence, and contrast this with the
status of byte_purge.py. Note the exact build tag delta string from the
document header.

Build a 4-section dashboard -- populate from the document only:

1. LIFECYCLE DISPATCHER INVENTORY (Section 3)
For EVERY lifecycle override targeted in the plan (Section 3.2), produce a
card with:
- NT8 Override Name | Target Partial File | Dispatcher Method Name
- INVARIANT CHECK: Does the dispatcher maintain the exact parameter signature
  required by the NinjaTrader 8 API?
- THREAD SAFETY: Identify the Enqueue compliance rule for each dispatcher.
  Which specific lifecycle phase (Section 9.1) is exempt from Enqueue wrapping
  and why?

2. PIPELINE LOGIC CONSISTENCY (Section 3.4)
Analyze the OnBarUpdate pipeline stages.
- List all 3 Guard Stages (names) and their return types.
- List all 6 Processing Stages (names) and their execution order.
- CRITICAL FLOW: If 'BarUpdate_GuardTradingSession()' returns false, which
  subsequent processing stages are skipped? Cite the exact line number from
  the scaffold code (Section 3.4).

3. VERIFICATION MATRIX AUDIT (Section 6)
For every check in Section 6: mark runnable / needs platform adjustment /
dependency missing for a Windows PowerShell target.
- Flag any step using 'grep' and provide the PowerShell native equivalent.
- Confirm the sequence of events after an edit (Section 8 of the protocol
  referenced in the plan): what must the Director do in the NT8 UI?

4. ADVERSARIAL SUMMARY
List every item needing a plan update, grouped:
- Blocking: any signature mismatches or thread-affinity violations.
- Moderate: missing guards or pipeline ordering issues.
- Advisory: PowerShell command alternatives, comment wording.
State: ready for the engineering step, or needs a plan update (list exact items).

Rules:
Base all findings strictly on what you read in the document.
Every finding must cite the exact section and line number if available.
Complete all 4 sections.
Your model name and version must appear in the h2 and title.
```
