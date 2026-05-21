---
description: Repeatable 100/100 Perfection Loop. Iteratively repairs and verifies code until the Project Health Score is 100/100.
argument-hint: <pr-number>
---
# PR PERFECTION LOOP (pr-loop)
**Target PR:** $1
**Goal:** 100/100 (25/25 Points)
**Mode:** Orchestrator (YOLO-parity)
**Protocol:** V12 Autonomous Perfection mandate.

You are the V12 Perfection Orchestrator. You MUST NOT STOP until PHS is 100/100.

---

## ORCHESTRATION RULES

- **SCORE 100 MANDATE**: You are BANNED from merging or ending the loop if PHS < 100.
- **HYGIENE GATE**: You MUST pass Step 0 (Clean Branch & Diff Size) before every push.
- **LOCAL FIRST**: You must achieve Local Score 15/15 before every push.
- **FORENSIC AUDIT**: Every failure must be categorized as [VALID], [HALLUCINATION], [INFRA-NOISE], or [ACCESS_BLOCKED].
- **F5 GATE**: The only manual action is the final NinjaTrader verification at Score 100.

---

## THE PERFECTION CYCLE

### Step 0: Pre-Flight Hygiene (MANDATORY)
**Switch to: Advanced mode**
Hand off:
```
TASK: Verify PR Hygiene
PROTOCOL:
  1. Run `powershell -File .\scripts\verify_pr_hygiene.ps1`.
  2. If FAIL: HALT and report the violation (e.g. "Diff > 10k" or "Branch is dirty").
  3. If PASS: Advance to Step 1.
```

### Step 1: Local Integrity (Goal: 15/15)
**Switch to: v12-engineer mode**
Hand off:
```
TASK: Local Repair & Hygiene
INPUT: PR #$1 bot findings + local lint/test results.
PROTOCOL:
  1. FIX all surgical violations (braces, sealed classes, complexity).
  2. CATEGORIZE issues in docs/brain/workflow_health.md ([VALID], [HALLUCINATION], [INFRA-NOISE]).
  3. RUN CSharpier formatter: `powershell -File .\scripts\format_all_csharp.ps1` (auto-fixes SA1210, IDE0005, IDE0009).
  4. VERIFY: Run `powershell -File .\scripts\calculate_fleet_score.ps1`.
  5. If Score < 15, repeat Step 1.
  6. If Score = 15, emit: [LOCAL-READY] PHS 15/15.
```

### Step 2: Global Integrity (Goal: 25/25)
**Switch to: Advanced mode**
Hand off:
```
TASK: Global Audit & Monitor
PROTOCOL:
  1. powershell -File .\deploy-sync.ps1 (MANDATORY before push - syncs NT8 hard links)
  2. git add . && git commit -m "fix: PHS Perfection Loop - PR #$1" && git push
  3. monitor_pr_checks $1 (Wait for all bots).
     - **MANDATORY SLEEP**: Start-Sleep -Seconds 300 (5 min) for the first check.
     - **SUBSEQUENT SLEEP**: Start-Sleep -Seconds 180 (3 min) if checks are still pending.
  4. Run `powershell -File .\scripts\calculate_fleet_score.ps1 -PrNumber $1`.
  5. If Score < 100, emit: [PHS-RETRY] Current: X/100.
  6. If Score = 100, emit: [PHS-PERFECT] 100/100.
```

### Step 3: Loop Control
- If [PHS-RETRY]: **Restart at Step 1.**
- If [PHS-PERFECT]: **Advance to final F5 verification.**

---

## FINAL HANDSHAKE
Once 100/100 is achieved, STOP and ask Director:
"PHS 100/100 achieved. Please press F5 in NinjaTrader. Type 'F5 done' to merge."
