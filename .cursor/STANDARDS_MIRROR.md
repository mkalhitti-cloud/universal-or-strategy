# Cursor Standards Mirror — V12 Project Law

> This file is a Cursor-readable summary of `.agent/standards_manifesto.md`.
> Always refer to the source file for the authoritative version.
> Source: [.agent/standards_manifesto.md](file:///.agent/standards_manifesto.md)

> **Cursor Read Tool Note**: The Cursor Read tool cannot access `.agent/` paths directly (returns
> "Permission denied"). Use PowerShell instead:
> `Get-Content "c:\WSGTA\universal-or-strategy\.agent\standards_manifesto.md"`
> Your skills are in `.cursor/skills/` where the Read tool works normally.

---

## The Hard Rules (Never Break These)

### 1. Zero-Trust Safety

- Never assume strategy state matches broker state. Always use `FirstOrDefault` for instrument lookups.
- Only remove order refs (stopOrders, target1Orders) after broker-confirmed terminal state.
- Always check `isFlattenRunning` before any manual flatten or entry.

### 2. Concurrency & Locking

- Every mutation of `activePositions`, `entryOrders`, `stopOrders`, `expectedPositions` → inside `lock(stateLock)`.
- All `_simaToggleSem.Wait()` calls → paired with `Release()` in a `finally` block.

### 3. ASCII-Only in C# String Literals (BUILD SAFETY)

- NEVER: emoji, curly quotes, em-dashes, Unicode arrows, box-drawing characters.
- ALWAYS use: `(!)` not emoji, `--` not em-dash, `->` not arrow, straight `"` not curly quotes.
- One broken quote caused 300+ compile errors in Build 936. This is not optional.

### 4. MOVE-SYNC FSM (Build 947+)

- All follower order cancel+resubmit MUST use the two-phase FSM (`_followerReplaceSpecs` dict).
- Raw `Cancel()` followed immediately by `Submit()` = BANNED. Creates ghost orders.
- FSM states: `PendingCancel` -> confirm in `OnAccountOrderUpdate` -> `Submitting` -> submit.
- `Account.Change` silently no-ops on Apex/Tradovate. FSM cancel+resubmit is the only path.

### 5. Scope Control

- Only modify files specified in the Mission Brief.
- Always generate `implementation_plan.md` before writing code.
- If a UI task loops more than 2 times → HALT and escalate to Director.

---

## Your Role on This Team

| Agent           | Role                              | Mirror              |
| --------------- | --------------------------------- | ------------------- |
| **Antigravity** | Director + IDE agent              | Mirrors Cursor      |
| **Cursor**      | IDE agent + executor host         | Mirrors Antigravity |
| **Claude**      | Executor (implementation)         | Mirrors Codex       |
| **Codex**       | Forensic auditor (diagnosis only) | Mirrors Claude      |
| **GitHub Bots** | Automated reviewers               | —                   |

**You (Cursor) implement under Director authorization. You do NOT patch. You do NOT diagnose.**
Claude implements. Codex diagnoses. You execute builds, deploy, and audit from the IDE.

---

## Director Commands

- `$MISSION` — Start a new phase via a Mission Brief artifact.
- `$AUDIT` — Run `/audit` skill to scan `src/` directory.
- `$PLAN_AUDIT` — Ingest Sonnet's plan and do a forensic logic audit before approving.

---

## Deployment

```powershell
# Always deploy via:
powershell -ExecutionPolicy Bypass -File "C:\WSGTA\universal-or-strategy\deploy-sync.ps1"
# The ASCII gate runs automatically. If it fails, DO NOT deploy manually.
```

---

## Your Home Folder

Read/write your session state here:

- `.agent/agents/cursor/MEMORY.md` — session memory (update at end of every session)
- `.agent/agents/cursor/IDENTITY.md` — your persistent role definition
- `.agent/agents/cursor/ONBOARDING.md` — cold-start prompt for new sessions
