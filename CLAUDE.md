# CLAUDE.md - BMad Project Standards & Safety Guide

## 🚩 Project Overview

**Universal OR Strategy (V12)**: A high-integrity institutional fleet trading strategy for NinjaTrader 8.

## 🛡️ Zero-Trust Protocols (MANDATORY)

1. **IPC Security**: All listeners must bind to Loopback (`127.0.0.1`). Malformed input must be rejected with `V12 IPC REJECT` logs.
2. **Input Validation**: Never trust incoming network payloads. Use strict UTF-8 decoding and bounded command lengths.
3. **Fleet Privacy**: Obscure sensitive account names using BMad aliases (`F01`, `F02`, etc.) in all external-facing responses.

## 🦍 Logic Integrity (FLEET SAFETY)

1. **No Internal Locks**: Legacy `lock(stateLock)` is BANNED for internal execution. Thread-safety should be managed via either the Actor model or direct atomic writes, depending on the mission requirements.
2. **Build 981 Protocol**: Direct writes to `stopOrders` are MANDATORY during bracket submission. Enqueue is BANNED for this operation to eliminate tracking latency during shutdown races.
2. **Ghost-Order Prevention**: Use Signed Delta Rollbacks for expected position cleanup; never use blanket zeroing.
3. **REAPER Bounds**: Repairs must be capped by both ATR-volatility and hard tick fences.
4. **Symmetry Gating**: Follower brackets must wait for the master "Anchor" price before submission.

## 🏷️ Naming Conventions

- **Build Tags**: Must be incremented in `V12_002.Properties.cs` for every production delivery.
- **Prefixes**: All files and primary classes use `V12_001` (Panel) or `V12_002` (Strategy).

## CRITICAL: ASCII-Only in All C# String Literals

- NEVER use emoji, curly quotes, em-dashes, Unicode arrows, or box-drawing in Print() or any string literal.
- Non-ASCII inside C# strings breaks the NinjaTrader compiler with 300+ cascading errors (Historical Precedent).
- Allowed substitutions: (!) not emoji, -- not em-dash, -> not arrow, straight " not curly " "
- See .agent/standards_manifesto.md Section 7 for the full rule table and emergency fix sequence.

## MOVE-SYNC / Follower Order Replace Pattern (Permanent DNA)

- **FSM Required**: Any follower order cancel+resubmit MUST use the two-phase Replace FSM (`_followerReplaceSpecs` dict).
- **Never cancel+submit directly**: Raw `Cancel()` followed immediately by `Submit()` creates ghost orders. BANNED.
- **FSM states**: `PendingCancel` -> wait for `OnAccountOrderUpdate` confirm -> `Submitting` -> `SubmitFollowerReplacement`.
- **ATR tick absorption**: While in `PendingCancel`, sizing changes update `PendingReplacementSpec` only. One cancel, one resubmit.
- **Fill-during-gap guard**: Check if master filled before submitting replacement. If yes, route to REAPER repair.
- **ChangeOrder banned for fleet accounts**: `Account.Change` silently no-ops on Apex/Tradovate.

## Live Bug Triage Protocol (A2A Ready)

- **Codex first**: For any live trading anomaly, run `/live-bug-triage` workflow.
- **Codex = diagnosis, Claude = architecture, Gemini/Jules = engineer**.
- **A2A Delegation**: Use Agent2Agent protocol to delegate surgical repair missions to local CLI agents.
- **Plan audit required**: Paste implementation plans to Antigravity for audit before approving.

## 🕹️ Engineer Self-Audit (P4) mandatory

Every code change must be validated by the Engineer (Gemini/Jules) before architectural sign-off:
1. **Grep Scan**: No `lock(stateLock)` in internal logic.
2. **Modular Audit**: Verification of partial-class destination accuracy.
3. **ASCII Gate**: Straight quotes only; no emoji or Unicode arrows.
