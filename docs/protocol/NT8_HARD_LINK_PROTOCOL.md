# NT8 Hard Link Integrity Protocol
# Version: 1.0
# Effective: 2026-07-13 (B13 post-mortem)
# Status: MANDATORY -- BLOCKING gate before every F5 compile
# Authority: PTT Orchestrator (Phase 5.5)

---

## Problem Statement (B13 Incident)

After B13 FINAL_PASS, NinjaTrader compiled successfully from a STALE deploy.
TradeCopierPanel.cs in NT8 was 673 bytes smaller than Wave source -- the hard links
had silently broken. NT8 was running the B12 stub GetRefPrice() (returns 0.0) instead
of the B13 live-price implementation. The build appeared green because the stub is valid
C# -- only the runtime behaviour was wrong.

Root cause: NTFS hard links can break when:
  1. A file is overwritten by a tool that creates a new inode (e.g. write_file, Copy-Item)
  2. A git checkout replaces the file
  3. An external editor saves with "replace" instead of "update in place"

There was no gate between FINAL_PASS and F5 to catch this.

---

## The Rule

PHASE 5.5 -- NT8 HARD LINK GATE (Orchestrator-owned, mandatory)

  After Phase 5 FINAL_PASS and BEFORE directing the user to F5 compile:

  STEP 1: Run verify_links.ps1 (audit mode)
    powershell -File scripts\verify_links.ps1
      Expected: All deployable files = OK (hard-linked or matching copy)
      On PASS: proceed to STEP 3

  STEP 2: On FAIL or copy-only warning -- run Fix mode
    powershell -File scripts\verify_links.ps1 -Fix
      This repairs hashes AND promotes plain copies to hard links.
      Expected: All files = FIXED or OK

  STEP 3: Re-run audit to confirm clean state
    powershell -File scripts\verify_links.ps1
      Must return PASS before F5 is authorised.

  STEP 4: Confirm hard link count = 2 on all 5 files
    $nt8 = "$env:USERPROFILE\Documents\NinjaTrader 8\bin\Custom\AddOns\PropTraderTools"
    foreach ($f in @("TradeCopierPanel.cs","CopyEngine.cs","AtrSizingEngine.cs",
                     "TradeCopierAddOn.cs","TradeCopierWindow.cs")) {
        $count = (fsutil hardlink list (Join-Path $nt8 $f) | Measure-Object -Line).Lines
        Write-Host "$f  links=$count"
    }
    All must report links=2. A count of 1 means a plain copy -- re-run -Fix.

  STEP 5: Direct user to F5 compile in NinjaTrader

GATE RULE: F5 instruction is BLOCKED until verify_links.ps1 reports PASS.

---

## Deployable Files (Wave -> NT8)

These 5 files are hard-linked between Wave src/ and NT8 AddOns/:

| Wave Source | NT8 Target | Deploy? |
|-------------|------------|---------|
| src/PropTraderTools/TradeCopierPanel.cs   | AddOns\PropTraderTools\TradeCopierPanel.cs   | YES |
| src/PropTraderTools/CopyEngine.cs         | AddOns\PropTraderTools\CopyEngine.cs         | YES |
| src/PropTraderTools/AtrSizingEngine.cs    | AddOns\PropTraderTools\AtrSizingEngine.cs    | YES |
| src/PropTraderTools/TradeCopierAddOn.cs   | AddOns\PropTraderTools\TradeCopierAddOn.cs   | YES |
| src/PropTraderTools/TradeCopierWindow.cs  | AddOns\PropTraderTools\TradeCopierWindow.cs  | YES |
| src/PropTraderTools/CopyEngineTests.cs    | (not deployed -- xUnit test file)            | NO  |

Wave path:  C:\WSGTA\universal-or-strategy\src\PropTraderTools\
NT8 path:   %USERPROFILE%\Documents\NinjaTrader 8\bin\Custom\AddOns\PropTraderTools\

---

## Script Reference

Script: scripts\verify_links.ps1 (Wave workspace)

| Mode | Command | When to use |
|------|---------|-------------|
| Audit | `powershell -File scripts\verify_links.ps1` | Phase 5.5 gate check |
| Fix   | `powershell -File scripts\verify_links.ps1 -Fix` | After audit finds DESYNC or MISSING |
| Custom paths | `-SrcPath <path> -NtPath <path>` | Non-default installs |

Exit codes:
  0 = PASS (all deployable files in sync)
  1 = FAIL (at least one DESYNC or MISSING)

---

## Why Hard Links (Not Copies)

Hard links (NTFS link count >= 2) mean both paths point to the same disk inode.
Any write to either path -- by Bob, git, or any tool -- instantly updates both.
No manual sync is ever needed. Copies (link count = 1) drift silently.

When hard links are in place:
  - ptt-engineer writes TradeCopierPanel.cs in Wave -> NT8 sees it instantly
  - git checkout in Wave updates the inode -> NT8 sees it instantly
  - No deploy step, no manual copy, no drift possible

When hard links break (link count drops to 1):
  - The two paths have independent inodes
  - Writes to Wave do NOT propagate to NT8
  - NT8 runs stale code. F5 compiles clean but behaviour is wrong.
  - Only verify_links.ps1 catches this before the user discovers wrong behaviour at runtime.

---

## Pipeline Position

```
Phase 5  -- ptt-plan-reviewer FINAL_PASS
             06-deferred-backlog.md confirmed written
             |
             v
Phase 5.5 -- ORCHESTRATOR: NT8 Hard Link Gate  <-- NEW (this protocol)
             powershell -File scripts\verify_links.ps1
             PASS required before proceeding
             On FAIL: run -Fix, re-audit, confirm links=2
             |
             v
             --> Tell user: "F5 compile in NinjaTrader now"
             --> User confirms green build
             |
             v
PIPELINE_COMPLETE: {epic}
```

---

## Hardening Scope (What Was Updated -- B13)

| Artifact | Change |
|----------|--------|
| `scripts/verify_links.ps1` | Fixed: correct AddOns path, test file excluded, -Fix flag, hard link count check |
| `docs/protocol/NT8_HARD_LINK_PROTOCOL.md` | Created: this file -- canonical protocol |
| `docs/protocol/PTT_WORKSPACE_PROTOCOL.md` | Updated: Phase 5.5 gate section added |
| `.bob/custom_modes.yaml` ptt-orchestrator | Updated: Phase 5.5 mandatory step in roleDefinition |
| `docs/standards/NT8_ADDON_KNOWLEDGE.md` | Updated: B13 hard link discovery appended |

---

## Effective
2026-07-13 (V1.0) -- PTT-COPIER-B13 post-mortem
