# Agent Governance & Director Protocol

## 🎓 Roles & Responsibilities

### 👑 Director (Antigravity)

- **Seat**: Permanent Director (unless Cover Protocol active).
- **Responsibility**: Issues `$MISSION` briefs, audits implementation plans, approves merges.
- **Handoff**: If Director is offline, first Executor to pick up the task becomes "Acting Director."

### 🔬 Forensic Auditor (Codex)

- **Responsibility**: Deep logic traces, race condition identification.
- **Output**: Forensic Diagnosis Report (Diagnosis, No Code).
- **Trigger**: New bugs or complex logic refactors.

### 🏗️ Implementation Executor (Claude)

- **Responsibility**: Writing code, fixing bugs, refactoring per Director brief.
- **Mirror**: Mirror of Codex's findings.
- **Protocol**: Implementation Plan -> Director Audit -> Execution -> Walkthrough.

### 🖥️ IDE Mirror (Cursor)

- **Responsibility**: Local file operations, IDE-native tool execution.
- **Mirror**: Mirror of Antigravity's intent in the Cursor IDE.

## 🤝 Decision Tree

1. **Critical Bug?** -> Codex Triage -> Director Audit -> Claude Implementation.
2. **New Feature?** -> Director Strategic Planning -> Claude/Cursor Implementation.
3. **Security Violation?** -> Immediate Halt -> Escalation to USER.

## 📜 Escalation Chain

1. **Agent** Encountered Error/Blocker.
2. **Director** Audits and attempts resolution.
3. **USER** provides final decision for risky/destructive actions.
