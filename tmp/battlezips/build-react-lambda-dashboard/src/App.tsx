import { useState } from "react";

// ── types ────────────────────────────────────────────────────────────────────

type SiteType = "Type 1" | "Type 2" | "unverifiable";
type Transform = "A" | "B";

interface LambdaSite {
  num: number;
  file: string;
  line: number;
  method: string;
  purpose: string;
  redTeam: boolean;
  transform: Transform;
  classification: SiteType;
  evidence: string;
  needsPlanUpdate: boolean;
}

interface PathEntry {
  location: string;
  old: string;
  newVal: string;
  status: "consistent" | "inconsistency found";
  note: string;
}

interface VerifyStep {
  id: string;
  gate: string;
  check: string;
  expected: string;
  platform: "runnable" | "needs platform adjustment" | "dependency missing";
  psEquivalent?: string;
}

// ── data ─────────────────────────────────────────────────────────────────────

// Section C.1 + C.4 analysis
// Transform A = statement-body lambda;  Transform B = expression-body
// Type 1 = pure-work; early-return is first statement, safe
// Type 2 = state cleanup AFTER primary call would be bypassed

const SITES: LambdaSite[] = [
  {
    num: 1,
    file: "V12_002.Orders.Callbacks.AccountOrders.cs",
    line: 146,
    method: "OnAccountOrderUpdate",
    purpose: "ProcessAccountOrderQueue (broker callback enqueue)",
    redTeam: true,
    transform: "A",
    classification: "Type 1",
    evidence:
      "Purpose is a simple enqueue/dispatch call. No dictionary removal, semaphore release, or flag clear follows the primary call in description. Early-return as first statement is safe.",
    needsPlanUpdate: false,
  },
  {
    num: 2,
    file: "V12_002.Orders.Callbacks.AccountOrders.cs",
    line: 162,
    method: "ProcessAccountOrderQueue",
    purpose: "reschedule on budget exhaustion",
    redTeam: true,
    transform: "A",
    classification: "Type 1",
    evidence:
      "Reschedule-only path: no shared-resource cleanup mentioned after the reschedule call. Early-return safe.",
    needsPlanUpdate: false,
  },
  {
    num: 3,
    file: "V12_002.Orders.Callbacks.AccountOrders.cs",
    line: 173,
    method: "ProcessAccountOrderQueue",
    purpose: "re-enqueue on flatten contention",
    redTeam: true,
    transform: "A",
    classification: "Type 1",
    evidence:
      "Re-enqueue on contention: purpose describes re-queuing work only; no cleanup after primary call in description. Early-return safe.",
    needsPlanUpdate: false,
  },
  {
    num: 4,
    file: "V12_002.Orders.Callbacks.AccountOrders.cs",
    line: 181,
    method: "ProcessAccountOrderQueue",
    purpose: "drain remaining queue",
    redTeam: true,
    transform: "A",
    classification: "Type 1",
    evidence:
      "Drain operation: no post-primary shared-state mutation described. Early-return safe.",
    needsPlanUpdate: false,
  },
  {
    num: 5,
    file: "V12_002.Orders.Callbacks.AccountOrders.cs",
    line: 369,
    method: "HandleMatchedFollowerOrder",
    purpose: "SubmitFollowerReplacement (FSM two-phase)",
    redTeam: true,
    transform: "A",
    classification: "Type 2",
    evidence:
      "_followerReplaceSpecs.TryRemove(sigName, out _) follows SubmitFollowerReplacement INSIDE the lambda. If early-return fires first, the spec entry is never removed → permanent reservation leak. Variable: _followerReplaceSpecs.",
    needsPlanUpdate: true,
    // OLD/NEW shown inline in component
  },
  {
    num: 6,
    file: "V12_002.Orders.Callbacks.AccountOrders.cs",
    line: 410,
    method: "HandleMatchedFollowerOrder",
    purpose: "SubmitFollowerTargetReplacement",
    redTeam: true,
    transform: "A",
    classification: "Type 1",
    evidence:
      "Purpose describes target-replacement submission only; no TryRemove/Release/Clear after the primary call in description. Early-return safe.",
    needsPlanUpdate: false,
  },
  {
    num: 7,
    file: "V12_002.Orders.Callbacks.AccountOrders.cs",
    line: 463,
    method: "HandleMatchedFollowerOrder",
    purpose: "RestoreCascadedTargets (stop-fill restore)",
    redTeam: true,
    transform: "A",
    classification: "Type 1",
    evidence:
      "RestoreCascadedTargets is the primary work; no post-call shared-state cleanup described. Early-return safe.",
    needsPlanUpdate: false,
  },
  {
    num: 8,
    file: "V12_002.Orders.Callbacks.AccountOrders.cs",
    line: 591,
    method: "ExecuteFollowerCascadeCleanup",
    purpose: "EmergencyFlattenSingleFleetAccount (CASCADE-FILLED)",
    redTeam: true,
    transform: "A",
    classification: "Type 1",
    evidence:
      "Emergency flatten call is the primary work; no subsequent shared-resource release described. Early-return safe.",
    needsPlanUpdate: false,
  },
  {
    num: 9,
    file: "V12_002.Orders.Callbacks.cs",
    line: 389,
    method: "HandleOrderCancelled",
    purpose: "RestoreCascadedTargets (master-side)",
    redTeam: true,
    transform: "A",
    classification: "Type 1",
    evidence:
      "Restore call is the entire work of the lambda body per description; no cleanup after. Early-return safe.",
    needsPlanUpdate: false,
  },
  {
    num: 10,
    file: "V12_002.Orders.Callbacks.Execution.cs",
    line: 235,
    method: "OnAccountExecutionUpdate",
    purpose: "UpdateAccountMetricsFromAccount",
    redTeam: true,
    transform: "A",
    classification: "Type 1",
    evidence:
      "Metric-update call is the sole purpose; no shared-resource operation follows per description. Early-return safe.",
    needsPlanUpdate: false,
  },
  {
    num: 11,
    file: "V12_002.REAPER.Audit.cs",
    line: 136,
    method: "AuditAccountState",
    purpose: "ProcessReaperRepairQueue (flat desync repair)",
    redTeam: true,
    transform: "B",
    classification: "Type 2",
    evidence:
      "The catch block clears _repairInFlight.TryRemove(repairKey, out _) on TriggerCustomEvent failure. The plan's NEW code adds the guard inside the lambda (the happy path) but the catch-block TryRemove remains. However: the guard causes the lambda to return early, meaning ProcessReaperRepairQueue() never runs — yet _repairInFlight is never cleared inside the lambda either, because TryRemove is only in the catch, not in the lambda body. The in-flight reservation is left permanently set after successful scheduling if _isTerminating fires. Variable: _repairInFlight.",
    needsPlanUpdate: true,
  },
  {
    num: 12,
    file: "V12_002.REAPER.Audit.cs",
    line: 183,
    method: "AuditAccountState",
    purpose: "ProcessReaperFlattenQueue (critical desync)",
    redTeam: true,
    transform: "B",
    classification: "unverifiable",
    evidence:
      "Sister site to #11 (same Transform B pattern). No explicit code block shown for this site in C.3; purpose is ProcessReaperFlattenQueue. Cannot confirm whether a _repairInFlight-style guard exists here without viewing source. Classify as unverifiable pending code review.",
    needsPlanUpdate: false,
  },
  {
    num: 13,
    file: "V12_002.REAPER.Audit.cs",
    line: 250,
    method: "AuditAccountState",
    purpose: "ProcessReaperNakedStopQueue (naked stop)",
    redTeam: true,
    transform: "B",
    classification: "unverifiable",
    evidence:
      "No explicit code block in C.3; purpose is ProcessReaperNakedStopQueue. Classification unverifiable without source. Plan only shows sister-site summary.",
    needsPlanUpdate: false,
  },
  {
    num: 14,
    file: "V12_002.REAPER.Audit.cs",
    line: 327,
    method: "AuditAccountState",
    purpose: "ProcessReaperFlattenQueue (master flatten)",
    redTeam: true,
    transform: "B",
    classification: "unverifiable",
    evidence:
      "No explicit code block in C.3; sister site of Case 2. Classification unverifiable without source.",
    needsPlanUpdate: false,
  },
  {
    num: 15,
    file: "V12_002.REAPER.Audit.cs",
    line: 372,
    method: "AuditAccountState",
    purpose: "ProcessReaperNakedStopQueue (master naked stop)",
    redTeam: true,
    transform: "B",
    classification: "unverifiable",
    evidence:
      "No explicit code block in C.3; sister site of Case 2. Classification unverifiable without source.",
    needsPlanUpdate: false,
  },
  {
    num: 16,
    file: "V12_002.SIMA.Dispatch.cs",
    line: 60,
    method: "ExecuteSmartDispatchEntry",
    purpose: "deferred dispatch retry (semaphore contention)",
    redTeam: true,
    transform: "A",
    classification: "Type 2",
    evidence:
      "Purpose explicitly mentions 'semaphore contention'. A deferred retry on semaphore contention implies a semaphore was acquired or a wait-slot reserved; if early-return fires, the semaphore/slot would not be Released. Classified Type 2 per rule: purpose mentions Release. Needs code verification.",
    needsPlanUpdate: true,
  },
  {
    num: 17,
    file: "V12_002.SIMA.Dispatch.cs",
    line: 610,
    method: "ExecuteSmartDispatchEntry",
    purpose: "PumpFleetDispatch prime",
    redTeam: true,
    transform: "A",
    classification: "Type 1",
    evidence:
      "PumpFleetDispatch prime: initial kick of the dispatch pump. No semaphore/TryRemove/Clear mentioned in purpose. Early-return safe.",
    needsPlanUpdate: false,
  },
  {
    num: 18,
    file: "V12_002.SIMA.Flatten.cs",
    line: 82,
    method: "InitiateFlattenOps",
    purpose: "PumpFlattenOps kickoff",
    redTeam: false,
    transform: "A",
    classification: "Type 1",
    evidence:
      "Kickoff/initial dispatch only; no post-call shared-state cleanup described. Early-return safe.",
    needsPlanUpdate: false,
  },
  {
    num: 19,
    file: "V12_002.SIMA.Flatten.cs",
    line: 201,
    method: "PumpFlattenOps",
    purpose: "chain to next account",
    redTeam: false,
    transform: "A",
    classification: "Type 1",
    evidence:
      "Chain continuation only; no cleanup after primary call in description. Early-return safe.",
    needsPlanUpdate: false,
  },
  {
    num: 20,
    file: "V12_002.SIMA.Flatten.cs",
    line: 319,
    method: "FlattenAccountPosition",
    purpose: "re-kick on completion",
    redTeam: false,
    transform: "A",
    classification: "Type 1",
    evidence:
      "Re-kick on completion: no shared resource cleanup in purpose description. Early-return safe.",
    needsPlanUpdate: false,
  },
  {
    num: 21,
    file: "V12_002.SIMA.Fleet.cs",
    line: 174,
    method: "PumpFleetDispatch",
    purpose: "chain from finally",
    redTeam: false,
    transform: "A",
    classification: "Type 1",
    evidence:
      "Chain call from finally block; the finally block's cleanup is outside the lambda. Lambda body is chain-only. Early-return safe.",
    needsPlanUpdate: false,
  },
  {
    num: 22,
    file: "V12_002.SIMA.Fleet.cs",
    line: 262,
    method: "PumpFleetDispatch",
    purpose: "chain after XorShadow CRC fail",
    redTeam: false,
    transform: "A",
    classification: "Type 1",
    evidence:
      "Chain-only path after CRC failure; no post-primary shared-state operation described. Early-return safe.",
    needsPlanUpdate: false,
  },
  {
    num: 23,
    file: "V12_002.SIMA.Lifecycle.cs",
    line: 57,
    method: "OnParameterChanged",
    purpose: "ProcessApplySimaState deferred toggle",
    redTeam: false,
    transform: "A",
    classification: "Type 1",
    evidence:
      "Deferred toggle of SIMA state; no TryRemove/Release/Clear in purpose description. Early-return safe.",
    needsPlanUpdate: false,
  },
  {
    num: 24,
    file: "V12_002.Trailing.StopUpdate.cs",
    line: 64,
    method: "OnOrderUpdate",
    purpose: "RestoreCascadedTargets (trailing restore)",
    redTeam: false,
    transform: "A",
    classification: "Type 1",
    evidence:
      "Restore call is the sole lambda purpose; no cleanup after in description. Early-return safe.",
    needsPlanUpdate: false,
  },
  {
    num: 25,
    file: "V12_002.UI.Compliance.cs",
    line: 286,
    method: "OnAccountExecutionUpdate",
    purpose: "ProcessAccountExecutionQueue (marshal)",
    redTeam: false,
    transform: "A",
    classification: "Type 1",
    evidence:
      "Marshal call only; no shared-state cleanup in purpose description. Early-return safe.",
    needsPlanUpdate: false,
  },
  {
    num: 26,
    file: "V12_002.UI.Compliance.cs",
    line: 304,
    method: "ProcessAccountExecutionQueue",
    purpose: "reschedule on budget",
    redTeam: false,
    transform: "A",
    classification: "Type 1",
    evidence:
      "Reschedule-only; no cleanup after primary call. Early-return safe.",
    needsPlanUpdate: false,
  },
  {
    num: 27,
    file: "V12_002.UI.Compliance.cs",
    line: 316,
    method: "ProcessAccountExecutionQueue",
    purpose: "flatten-contention bailout",
    redTeam: false,
    transform: "A",
    classification: "Type 1",
    evidence:
      "Bailout/reschedule path; no TryRemove/Release described. Early-return safe.",
    needsPlanUpdate: false,
  },
  {
    num: 28,
    file: "V12_002.UI.Compliance.cs",
    line: 324,
    method: "ProcessAccountExecutionQueue",
    purpose: "drain remaining",
    redTeam: false,
    transform: "A",
    classification: "Type 1",
    evidence:
      "Drain operation; no shared-resource cleanup after primary call. Early-return safe.",
    needsPlanUpdate: false,
  },
  {
    num: 29,
    file: "V12_002.UI.IPC.cs",
    line: 328,
    method: "ProcessIpcCommands",
    purpose: "reschedule IPC queue",
    redTeam: false,
    transform: "A",
    classification: "Type 1",
    evidence:
      "Reschedule IPC queue only; no flush/clear/remove in purpose. Early-return safe.",
    needsPlanUpdate: false,
  },
  {
    num: 30,
    file: "V12_002.UI.IPC.Server.cs",
    line: 277,
    method: "OnIpcCommand",
    purpose: "TCP server callback -> strategy marshal",
    redTeam: false,
    transform: "B",
    classification: "Type 1",
    evidence:
      "Transform B: expression body o => ProcessIpcCommands(). The lambda just calls ProcessIpcCommands; no cleanup after in the lambda. Early-return safe (worked example in C.3 Case 3).",
    needsPlanUpdate: false,
  },
  {
    num: 31,
    file: "V12_002.cs",
    line: 373,
    method: "ScheduleActorDrain",
    purpose: "TryDrain (actor mailbox)",
    redTeam: false,
    transform: "A",
    classification: "Type 1",
    evidence:
      "TryDrain is a non-destructive mailbox drain; no shared-state cleanup after primary call in description. Early-return safe.",
    needsPlanUpdate: false,
  },
  {
    num: 32,
    file: "V12_002.REAPER.cs",
    line: 132,
    method: "ReaperAuditThread",
    purpose: "AuditApexPositions (bg thread marshal)",
    redTeam: false,
    transform: "B",
    classification: "Type 1",
    evidence:
      "Expression-body marshal to strategy thread; AuditApexPositions is the sole call. No cleanup after in description. Early-return safe.",
    needsPlanUpdate: false,
  },
];

// Section D.4 path entries
const PATH_ENTRIES: PathEntry[] = [
  {
    location: "deploy-sync.ps1 line 8",
    old: '$RepoRoot = "C:\\WSGTA\\universal-or-strategy"',
    newVal: "$RepoRoot = $PSScriptRoot",
    status: "consistent",
    note:
      "$PSScriptRoot resolves to the script's own directory (= repo root). Matches the substitution model used in deploy-vm-safe.ps1:10. No inconsistency. (§D.4.b sub-block 1)",
  },
  {
    location: "deploy-sync.ps1 line 9",
    old: '$NtCustomDir = "C:\\Users\\Mohammed Khalid\\Documents\\NinjaTrader 8\\bin\\Custom"',
    newVal: '$NtCustomDir = Join-Path $env:USERPROFILE "Documents\\NinjaTrader 8\\bin\\Custom"',
    status: "consistent",
    note:
      "$env:USERPROFILE matches the substitution model from deploy-vm-safe.ps1:10. Consistent with portability standard. (§D.4.b sub-block 1)",
  },
  {
    location: "deploy-sync.ps1 line 89",
    old: "# Fix: run C:\\tmp\\byte_purge.py, then re-run deploy-sync.ps1",
    newVal:
      "# Fix: run `python (Join-Path $PSScriptRoot 'check_ascii.py') src/`, then re-run deploy-sync.ps1",
    status: "inconsistency found",
    note:
      "This is a PowerShell COMMENT line. Join-Path inside a # comment is NOT evaluated at runtime — it is plain text. A developer reading this comment after the change will see the literal string: 'python (Join-Path $PSScriptRoot \\'check_ascii.py\\') src/' — they must mentally substitute the repo root themselves. The comment is technically correct as guidance but could confuse a junior engineer who expects the parenthesised call to resolve. Recommend using a literal portable example path or a note such as '(run from repo root)' instead. (§D.4.b sub-block 2, §B.2)",
  },
  {
    location: "deploy-sync.ps1 line 99",
    old: "Write-Host \"  Fix: python C:\\tmp\\byte_purge.py  then re-run deploy-sync.ps1\" -ForegroundColor Red",
    newVal:
      'Write-Host "  Fix: python $(Join-Path $PSScriptRoot \'check_ascii.py\') src/  then re-run deploy-sync.ps1" -ForegroundColor Red',
    status: "consistent",
    note:
      "This is an EXECUTABLE line (Write-Host with a double-quoted string). $(Join-Path $PSScriptRoot 'check_ascii.py') IS evaluated at runtime by PowerShell's sub-expression operator inside double quotes. The developer will see the fully-resolved absolute path to check_ascii.py at runtime. This is correct and consistent. (§D.4.b sub-block 2)",
  },
  {
    location: "Linting.csproj line 37",
    old: "<HintPath>C:\\Users\\Mohammed Khalid\\Documents\\NinjaTrader 8\\bin\\Custom\\NinjaTrader.Custom.dll</HintPath>",
    newVal:
      "<HintPath>$(UserProfile)\\Documents\\NinjaTrader 8\\bin\\Custom\\NinjaTrader.Custom.dll</HintPath>",
    status: "inconsistency found",
    note:
      "PLATFORM QUESTION — Condition attribute on Linux CI: MSBuild's $(UserProfile) resolves on Windows. On Linux/macOS it maps to $HOME. However, NinjaTrader.Custom.dll is Windows-only and will never exist on a Linux CI runner. The HintPath change removes the hardcoded username (good), but without a Condition='$([MSBuild]::IsOSPlatform(Windows))' attribute on the <Reference> item, the Linux CI build will get a Reference with a non-existent HintPath. MSBuild treats a missing HintPath as a warning rather than an error only if the assembly can be resolved from the GAC or elsewhere — which it cannot on Linux. FINDING: A Condition attribute is needed to prevent Linux CI Reference failures. The plan does not include this condition. (§D.4.a)",
  },
];

// Section F verification steps
const VERIFY_STEPS: VerifyStep[] = [
  {
    id: "1",
    gate: "ASCII purity (C#)",
    check: "python check_ascii.py src/",
    expected: "zero findings",
    platform: "runnable",
    psEquivalent: undefined,
  },
  {
    id: "2",
    gate: "Lock-ban",
    check: "grep -n 'lock(stateLock)' src/",
    expected: "zero hits",
    platform: "needs platform adjustment",
    psEquivalent:
      "Select-String -Path 'src\\*' -Pattern 'lock\\(stateLock\\)' -Recurse",
  },
  {
    id: "3",
    gate: "Guard coverage (Transform A)",
    check: "grep -c 'if (_isTerminating) return; // ADR-019 orphan guard' src/",
    expected: "26 hits",
    platform: "needs platform adjustment",
    psEquivalent:
      "(Select-String -Path 'src\\*' -Pattern 'if \\(_isTerminating\\) return; // ADR-019 orphan guard' -Recurse).Count",
  },
  {
    id: "3b",
    gate: "REAPER.Audit.cs per-file coverage",
    check: "grep -c 'if (_isTerminating) return;' src/V12_002.REAPER.Audit.cs",
    expected: "5 hits",
    platform: "needs platform adjustment",
    psEquivalent:
      "(Select-String -Path 'src\\V12_002.REAPER.Audit.cs' -Pattern 'if \\(_isTerminating\\) return;').Count",
  },
  {
    id: "4",
    gate: "Guard coverage (Transform B)",
    check:
      "grep -cE 'o => \\\\{ if \\\\(_isTerminating\\\\) return; [A-Za-z]+\\\\(\\\\); \\\\}' src/",
    expected: "6 hits",
    platform: "needs platform adjustment",
    psEquivalent:
      "(Select-String -Path 'src\\*' -Pattern 'o => \\{ if \\(_isTerminating\\) return; [A-Za-z]+\\(\\); \\}' -Recurse).Count",
  },
  {
    id: "5",
    gate: "Devcontainer presence",
    check:
      "test -f .devcontainer/devcontainer.json && test -f .devcontainer/Dockerfile",
    expected: "exit 0",
    platform: "needs platform adjustment",
    psEquivalent:
      "(Test-Path .devcontainer/devcontainer.json) -and (Test-Path .devcontainer/Dockerfile)",
  },
  {
    id: "6",
    gate: "Label-sync presence",
    check:
      "test -f .github/workflows/label-sync.yml && test -f .github/labels.yml",
    expected: "exit 0",
    platform: "needs platform adjustment",
    psEquivalent:
      "(Test-Path .github/workflows/label-sync.yml) -and (Test-Path .github/labels.yml)",
  },
  {
    id: "7",
    gate: "LFS config presence",
    check: "test -f .gitattributes",
    expected: "exit 0",
    platform: "needs platform adjustment",
    psEquivalent: "Test-Path .gitattributes",
  },
  {
    id: "8",
    gate: "Hook amendment",
    check:
      'grep -q "ADR-019: LFS pointer gate" .git/hooks/pre-commit',
    expected: "exit 0",
    platform: "needs platform adjustment",
    psEquivalent:
      "Select-String -Path '.git\\hooks\\pre-commit' -Pattern 'ADR-019: LFS pointer gate' -Quiet",
  },
  {
    id: "9",
    gate: "Hook live test (LFS)",
    check: "stage a non-LFS *.dll",
    expected: "hook rejects with ADR-019 message",
    platform: "needs platform adjustment",
    psEquivalent:
      "Hook body uses POSIX shell (bash for/case/do). On Windows, WSL or Git-for-Windows bash is required to execute the hook. The hook script itself is not natively runnable by pwsh.",
  },
  {
    id: "10",
    gate: "Hook live test (size)",
    check: "stage a non-LFS file > 5 MiB",
    expected: "hook rejects with ADR-019 message",
    platform: "needs platform adjustment",
    psEquivalent:
      "Same as #9: hook body is POSIX shell. Requires WSL or Git-for-Windows bash on Windows.",
  },
  {
    id: "11",
    gate: "Portability (Linting)",
    check: "grep -c 'C:\\\\Users\\\\Mohammed' Linting.csproj",
    expected: "zero hits",
    platform: "needs platform adjustment",
    psEquivalent:
      "(Select-String -Path 'Linting.csproj' -Pattern 'C:\\\\Users\\\\Mohammed').Count -eq 0",
  },
  {
    id: "12",
    gate: "Portability (deploy) — user profile",
    check: "grep -nE 'C:\\\\\\\\Users\\\\\\\\' deploy-sync.ps1",
    expected: "zero hits",
    platform: "needs platform adjustment",
    psEquivalent:
      "Select-String -Path 'deploy-sync.ps1' -Pattern 'C:\\\\Users\\\\' -Recurse",
  },
  {
    id: "12b",
    gate: "Portability (deploy) — repo/tool paths",
    check: "grep -nE 'C:\\\\\\\\(WSGTA|tmp)\\\\\\\\' deploy-sync.ps1",
    expected: "zero hits",
    platform: "needs platform adjustment",
    psEquivalent:
      "Select-String -Path 'deploy-sync.ps1' -Pattern 'C:\\\\(WSGTA|tmp)\\\\' -Recurse",
  },
  {
    id: "12c",
    gate: "Portability (deploy) — positive check",
    check:
      "grep -cE '\\$PSScriptRoot|\\$env:USERPROFILE|GetFolderPath' deploy-sync.ps1",
    expected: ">= 3 hits",
    platform: "needs platform adjustment",
    psEquivalent:
      "(Select-String -Path 'deploy-sync.ps1' -Pattern '\\$PSScriptRoot|\\$env:USERPROFILE|GetFolderPath').Count",
  },
  {
    id: "13",
    gate: "Build tag",
    check:
      "grep -n '1111.003-v28.0-adr019' src/V12_002.Constants.cs",
    expected: "one hit on line 12",
    platform: "needs platform adjustment",
    psEquivalent:
      "Select-String -Path 'src\\V12_002.Constants.cs' -Pattern '1111.003-v28.0-adr019'",
  },
  {
    id: "14",
    gate: "Deploy sync round-trip",
    check: "pwsh -File ./deploy-sync.ps1 then F5 in NT",
    expected: "ASCII gate PASS, banner shows new BuildTag",
    platform: "runnable",
    psEquivalent: undefined,
  },
];

// ── helpers ───────────────────────────────────────────────────────────────────

function classColor(c: SiteType) {
  if (c === "Type 2") return "bg-red-900/60 text-red-300 border-red-700";
  if (c === "unverifiable") return "bg-yellow-900/40 text-yellow-300 border-yellow-700";
  return "bg-emerald-900/40 text-emerald-300 border-emerald-700";
}

function platformColor(p: VerifyStep["platform"]) {
  if (p === "runnable") return "bg-emerald-900/40 text-emerald-300 border-emerald-700";
  if (p === "needs platform adjustment") return "bg-yellow-900/40 text-yellow-300 border-yellow-700";
  return "bg-red-900/60 text-red-300 border-red-700";
}

function pathStatusColor(s: PathEntry["status"]) {
  if (s === "consistent") return "bg-emerald-900/40 text-emerald-300 border-emerald-700";
  return "bg-red-900/60 text-red-300 border-red-700";
}

// ── site #5 OLD/NEW ────────────────────────────────────────────────────────

const SITE5_OLD = `bool replacementScheduled = false;
try
{
    TriggerCustomEvent(o =>
    {
        // [P2 FSM CONSISTENCY]: Re-read price/qty from spec at execution time.
        SubmitFollowerReplacement(sigName, acctNameCapture,
            fsmCapture.PendingPrice, fsmCapture.PendingQty, fsmCapture);
        _followerReplaceSpecs.TryRemove(sigName, out _);  // ← cleanup
    }, null);
    replacementScheduled = true;
}
catch (Exception ex)
{
    Print("[FSM] TriggerCustomEvent failed for " + sigName + ": " + ex.Message);
    _followerReplaceSpecs.TryRemove(sigName, out _);
}`;

const SITE5_NEW = `bool replacementScheduled = false;
try
{
    TriggerCustomEvent(o =>
    {
        if (_isTerminating) return;  // ADR-019 orphan guard
        //  ↑ PROBLEM: _followerReplaceSpecs.TryRemove below is SKIPPED
        // [P2 FSM CONSISTENCY]: Re-read price/qty from spec at execution time.
        SubmitFollowerReplacement(sigName, acctNameCapture,
            fsmCapture.PendingPrice, fsmCapture.PendingQty, fsmCapture);
        _followerReplaceSpecs.TryRemove(sigName, out _);  // ← BYPASSED
    }, null);
    replacementScheduled = true;
}
catch (Exception ex)
{
    Print("[FSM] TriggerCustomEvent failed for " + sigName + ": " + ex.Message);
    _followerReplaceSpecs.TryRemove(sigName, out _);
}`;

// ── site #11 OLD/NEW ───────────────────────────────────────────────────────

const SITE11_OLD = `_reaperRepairQueue.Enqueue(acct.Name);
// B957/E1: Clear in-flight guard if TriggerCustomEvent fails.
try { TriggerCustomEvent(o => ProcessReaperRepairQueue(), null); }
catch (Exception repairTriggerEx)
{
    _repairInFlight.TryRemove(repairKey, out _); // [Build 968]
    Print("[REAPER] TriggerCustomEvent failed for " + repairKey + ": "
          + repairTriggerEx.Message + " -- in-flight cleared.");
}`;

const SITE11_NEW = `_reaperRepairQueue.Enqueue(acct.Name);
// B957/E1: Clear in-flight guard if TriggerCustomEvent fails.
try { TriggerCustomEvent(o =>
    { if (_isTerminating) return;  // ADR-019 orphan guard
      // ↑ PROBLEM: if _isTerminating, ProcessReaperRepairQueue() is skipped
      //   but _repairInFlight is also never cleared (TryRemove is only
      //   in the catch block). The key stays in _repairInFlight forever.
      ProcessReaperRepairQueue(); }, null); }
catch (Exception repairTriggerEx)
{
    _repairInFlight.TryRemove(repairKey, out _); // [Build 968]
    Print("[REAPER] TriggerCustomEvent failed for " + repairKey + ": "
          + repairTriggerEx.Message + " -- in-flight cleared.");
}`;

// ── sub-components ────────────────────────────────────────────────────────────

function Badge({ label, colorClass }: { label: string; colorClass: string }) {
  return (
    <span
      className={`inline-block px-2 py-0.5 rounded border text-xs font-semibold ${colorClass}`}
    >
      {label}
    </span>
  );
}

function CodeBlock({ code, label }: { code: string; label?: string }) {
  return (
    <div className="mt-2">
      {label && (
        <div className="text-xs font-bold text-slate-400 mb-1 uppercase tracking-wider">
          {label}
        </div>
      )}
      <pre className="bg-slate-950 border border-slate-700 rounded p-3 text-xs text-slate-200 overflow-x-auto whitespace-pre leading-relaxed font-mono">
        {code}
      </pre>
    </div>
  );
}

function SectionHeader({
  num,
  title,
  sub,
}: {
  num: string;
  title: string;
  sub?: string;
}) {
  return (
    <div className="mb-6">
      <div className="flex items-center gap-3 mb-1">
        <span className="flex-shrink-0 w-9 h-9 rounded-full bg-indigo-600 flex items-center justify-center text-white font-bold text-sm">
          {num}
        </span>
        <h3 className="text-xl font-bold text-white">{title}</h3>
      </div>
      {sub && (
        <p className="ml-12 text-sm text-slate-400 font-mono">{sub}</p>
      )}
    </div>
  );
}

// ── Site Card ────────────────────────────────────────────────────────────────

function SiteCard({ site }: { site: LambdaSite }) {
  const [open, setOpen] = useState(false);
  const isSpecial = site.num === 5 || site.num === 11;

  return (
    <div
      className={`rounded-lg border ${
        site.classification === "Type 2"
          ? "border-red-700 bg-red-950/30"
          : site.classification === "unverifiable"
          ? "border-yellow-700 bg-yellow-950/20"
          : "border-slate-700 bg-slate-800/60"
      } p-4 transition-all`}
    >
      {/* header row */}
      <div className="flex flex-wrap items-start gap-2 mb-2">
        <span className="text-slate-400 font-mono text-xs mt-0.5">
          #{String(site.num).padStart(2, "0")}
        </span>
        <div className="flex-1 min-w-0">
          <div className="flex flex-wrap gap-1.5 items-center">
            <Badge
              label={site.classification}
              colorClass={classColor(site.classification)}
            />
            <Badge
              label={`Transform ${site.transform}`}
              colorClass="bg-indigo-900/50 text-indigo-300 border-indigo-700"
            />
            {site.redTeam && (
              <Badge
                label="Red-Team Critical"
                colorClass="bg-orange-900/50 text-orange-300 border-orange-700"
              />
            )}
            {site.needsPlanUpdate && (
              <Badge
                label="⚠ NEEDS PLAN UPDATE"
                colorClass="bg-red-700 text-white border-red-500"
              />
            )}
          </div>
        </div>
      </div>

      {/* location */}
      <div className="text-xs font-mono text-slate-300 mb-1">
        <span className="text-slate-500">file: </span>
        <span className="text-cyan-400">{site.file}</span>
        <span className="text-slate-500"> · line </span>
        <span className="text-yellow-400">{site.line}</span>
      </div>
      <div className="text-xs font-mono text-slate-300 mb-2">
        <span className="text-slate-500">method: </span>
        <span className="text-purple-300">{site.method}</span>
        <span className="text-slate-500"> · </span>
        <span className="text-slate-300">{site.purpose}</span>
      </div>

      {/* evidence */}
      <p className="text-xs text-slate-400 leading-relaxed">{site.evidence}</p>

      {/* expandable OLD/NEW for #5 and #11 */}
      {isSpecial && (
        <div className="mt-3">
          <button
            onClick={() => setOpen((v) => !v)}
            className="text-xs px-3 py-1 rounded bg-indigo-700 hover:bg-indigo-600 text-white font-semibold transition-colors"
          >
            {open ? "▲ Collapse" : "▼ Show OLD / NEW code"}
          </button>
          {open && (
            <div className="mt-3">
              {site.num === 5 ? (
                <>
                  <CodeBlock code={SITE5_OLD} label="OLD — site #5 (§C.3 Case 1)" />
                  <CodeBlock code={SITE5_NEW} label="NEW (plan as written) — problem visible" />
                  <p className="mt-2 text-xs text-red-300 font-semibold">
                    ✗ Unclean variable:{" "}
                    <code className="bg-slate-900 px-1 rounded">
                      _followerReplaceSpecs
                    </code>{" "}
                    — the TryRemove inside the lambda is bypassed by the early-return. The spec entry for{" "}
                    <code className="bg-slate-900 px-1 rounded">sigName</code>{" "}
                    leaks permanently. Classification confirmed: Type 2.
                  </p>
                </>
              ) : (
                <>
                  <CodeBlock code={SITE11_OLD} label="OLD — site #11 (§C.3 Case 2)" />
                  <CodeBlock code={SITE11_NEW} label="NEW (plan as written) — problem visible" />
                  <p className="mt-2 text-xs text-red-300 font-semibold">
                    ✗ Unclean variable:{" "}
                    <code className="bg-slate-900 px-1 rounded">
                      _repairInFlight
                    </code>{" "}
                    — the TryRemove is only in the catch block. When the early-return fires (success path + _isTerminating), the key is never removed. Permanent lockout on that repairKey. Classification confirmed: Type 2.
                  </p>
                </>
              )}
            </div>
          )}
        </div>
      )}
    </div>
  );
}

// ── main App ──────────────────────────────────────────────────────────────────

export default function App() {
  const [activeSection, setActiveSection] = useState<number>(0);

  const sections = [
    "Lambda Site Inventory",
    "Path Substitution",
    "Verification Steps",
    "Overall Summary",
  ];

  const type2Sites = SITES.filter((s) => s.classification === "Type 2");
  const unverifiableSites = SITES.filter(
    (s) => s.classification === "unverifiable"
  );
  const type1Sites = SITES.filter((s) => s.classification === "Type 1");



  return (
    <div className="min-h-screen bg-slate-900 text-slate-200">
      {/* ── top bar ── */}
      <header className="sticky top-0 z-50 bg-slate-950 border-b border-slate-700 shadow-xl">
        <div className="max-w-7xl mx-auto px-4 py-3 flex flex-col sm:flex-row sm:items-center sm:justify-between gap-2">
          <div>
            <h2 className="text-lg font-bold text-indigo-400 tracking-tight">
              Claude Opus 4.5 — Architect Review Dashboard
            </h2>
            <p className="text-xs text-slate-400">
              ADR-019 Sovereign Substrate Repair · Implementation Plan Review ·{" "}
              <span className="font-mono text-yellow-400">
                Build 1111.002-v28.0 → Build 1111.003-v28.0-adr019
              </span>
            </p>
          </div>
          <div className="flex gap-1.5 flex-wrap">
            {sections.map((s, i) => (
              <button
                key={i}
                onClick={() => setActiveSection(i)}
                className={`px-3 py-1.5 rounded text-xs font-semibold transition-colors ${
                  activeSection === i
                    ? "bg-indigo-600 text-white"
                    : "bg-slate-800 text-slate-300 hover:bg-slate-700"
                }`}
              >
                {i + 1}. {s}
              </button>
            ))}
          </div>
        </div>
      </header>

      {/* ── document provenance banner ── */}
      <div className="bg-indigo-950/60 border-b border-indigo-800 px-4 py-2">
        <p className="text-xs text-indigo-300 max-w-7xl mx-auto">
          <span className="font-bold text-indigo-200">Document read:</span>{" "}
          <span className="font-mono">
            github.com/mkalhitti-cloud/universal-or-strategy @ mission-uni-5-full-sync / docs/brain/implementation_plan.md
          </span>{" "}
          · <span className="font-bold text-yellow-300">Build tag delta (exact from §header):</span>{" "}
          <code className="bg-indigo-900 px-1 rounded font-mono text-yellow-200">
            `Build 1111.002-v28.0` → `Build 1111.003-v28.0-adr019`
          </code>{" "}
          · <code className="text-slate-400 font-mono">src/V12_002.Constants.cs:12</code>
        </p>
      </div>

      <main className="max-w-7xl mx-auto px-4 py-8">
        {/* ══════════════════════════════════════════════════════════════
            SECTION 1: Lambda Site Inventory
        ══════════════════════════════════════════════════════════════ */}
        {activeSection === 0 && (
          <div>
            <SectionHeader
              num="C.1"
              title="Complete Lambda Site Inventory — All 32 Sites"
              sub="Source: §C.1, §C.3, §C.4 — transform types from §C.4 classification table"
            />

            {/* summary stats */}
            <div className="grid grid-cols-2 sm:grid-cols-4 gap-4 mb-8">
              {[
                {
                  label: "Total Sites",
                  value: 32,
                  color: "text-indigo-400",
                },
                {
                  label: "Type 1 (Safe)",
                  value: type1Sites.length,
                  color: "text-emerald-400",
                },
                {
                  label: "Type 2 (Needs Fix)",
                  value: type2Sites.length,
                  color: "text-red-400",
                },
                {
                  label: "Unverifiable",
                  value: unverifiableSites.length,
                  color: "text-yellow-400",
                },
              ].map((s) => (
                <div
                  key={s.label}
                  className="bg-slate-800 border border-slate-700 rounded-lg p-4 text-center"
                >
                  <div className={`text-3xl font-bold ${s.color}`}>
                    {s.value}
                  </div>
                  <div className="text-xs text-slate-400 mt-1">{s.label}</div>
                </div>
              ))}
            </div>

            {/* transform key */}
            <div className="mb-6 flex flex-wrap gap-3 text-xs">
              <div className="bg-slate-800 border border-slate-700 rounded px-3 py-2">
                <span className="font-bold text-indigo-300">Transform A</span>
                <span className="text-slate-400"> — add guard as first line inside existing block (25+1 sites)</span>
              </div>
              <div className="bg-slate-800 border border-slate-700 rounded px-3 py-2">
                <span className="font-bold text-indigo-300">Transform B</span>
                <span className="text-slate-400"> — expand expression-body to statement block (6 sites)</span>
              </div>
            </div>

            {/* site cards */}
            <div className="grid grid-cols-1 lg:grid-cols-2 gap-4">
              {SITES.map((site) => (
                <SiteCard key={site.num} site={site} />
              ))}
            </div>
          </div>
        )}

        {/* ══════════════════════════════════════════════════════════════
            SECTION 2: Path Substitution Consistency
        ══════════════════════════════════════════════════════════════ */}
        {activeSection === 1 && (
          <div>
            <SectionHeader
              num="D.4"
              title="Path Substitution Consistency"
              sub="Source: §D.4.a (Linting.csproj) and §D.4.b (deploy-sync.ps1) — 5 proposed changes"
            />

            <div className="space-y-6">
              {PATH_ENTRIES.map((e, i) => (
                <div
                  key={i}
                  className={`rounded-lg border p-5 ${
                    e.status === "consistent"
                      ? "border-slate-600 bg-slate-800/50"
                      : "border-red-700 bg-red-950/30"
                  }`}
                >
                  <div className="flex flex-wrap items-center gap-2 mb-3">
                    <span className="font-mono text-sm font-bold text-white">
                      {e.location}
                    </span>
                    <Badge
                      label={e.status}
                      colorClass={pathStatusColor(e.status)}
                    />
                  </div>
                  <div className="grid grid-cols-1 md:grid-cols-2 gap-3 mb-3">
                    <div>
                      <div className="text-xs text-slate-500 uppercase font-bold mb-1">
                        OLD
                      </div>
                      <code className="block bg-slate-950 border border-slate-700 rounded p-2 text-xs text-red-300 font-mono break-all">
                        {e.old}
                      </code>
                    </div>
                    <div>
                      <div className="text-xs text-slate-500 uppercase font-bold mb-1">
                        NEW
                      </div>
                      <code className="block bg-slate-950 border border-slate-700 rounded p-2 text-xs text-green-300 font-mono break-all">
                        {e.newVal}
                      </code>
                    </div>
                  </div>
                  <p className="text-sm text-slate-300 leading-relaxed">{e.note}</p>
                </div>
              ))}
            </div>

            {/* special Q&A box */}
            <div className="mt-8 bg-indigo-950/40 border border-indigo-700 rounded-lg p-5">
              <h4 className="font-bold text-indigo-300 mb-3 text-base">
                Specific Questions (§D.4)
              </h4>
              <div className="space-y-4 text-sm text-slate-300 leading-relaxed">
                <div>
                  <p className="font-semibold text-yellow-300 mb-1">
                    Q: Does a PowerShell function call inside a comment (e.g.,{" "}
                    <code className="font-mono bg-slate-900 px-1 rounded">
                      Join-Path
                    </code>{" "}
                    inside a{" "}
                    <code className="font-mono bg-slate-900 px-1 rounded">
                      # comment
                    </code>{" "}
                    line) get evaluated at runtime?
                  </p>
                  <p>
                    <span className="font-bold text-red-300">No.</span> A{" "}
                    <code className="font-mono bg-slate-900 px-1 rounded">#</code>
                    -prefixed line is a PowerShell comment and is entirely ignored by the
                    parser. No expression inside a comment is evaluated. After the change at
                    line 89, a developer reading the comment will see the literal text:
                  </p>
                  <code className="block my-2 bg-slate-950 border border-slate-700 rounded p-2 text-xs text-slate-200 font-mono">
                    # Fix: run `python (Join-Path $PSScriptRoot 'check_ascii.py') src/`, then re-run deploy-sync.ps1
                  </code>
                  <p>
                    They must mentally resolve{" "}
                    <code className="font-mono bg-slate-900 px-1 rounded">
                      $PSScriptRoot
                    </code>{" "}
                    themselves. The comment is valid guidance but not self-executing.
                    Contrast with <strong>line 99</strong> (a{" "}
                    <code className="font-mono bg-slate-900 px-1 rounded">
                      Write-Host
                    </code>{" "}
                    with a double-quoted string), where{" "}
                    <code className="font-mono bg-slate-900 px-1 rounded">
                      $(Join-Path ...)
                    </code>{" "}
                    <em>is</em> evaluated at runtime.{" "}
                    <span className="text-yellow-300">
                      Finding: line 89 wording is advisory-level ambiguous; line 99 is correct.
                    </span>
                  </p>
                </div>
                <hr className="border-slate-700" />
                <div>
                  <p className="font-semibold text-yellow-300 mb-1">
                    Q: Does the{" "}
                    <code className="font-mono bg-slate-900 px-1 rounded">
                      HintPath
                    </code>{" "}
                    in Linting.csproj need a{" "}
                    <code className="font-mono bg-slate-900 px-1 rounded">
                      Condition
                    </code>{" "}
                    attribute on Linux CI?
                  </p>
                  <p>
                    <span className="font-bold text-red-300">Yes.</span> The{" "}
                    <code className="font-mono bg-slate-900 px-1 rounded">
                      $(UserProfile)
                    </code>{" "}
                    substitution removes the hardcoded username (correct), but the
                    reference to{" "}
                    <code className="font-mono bg-slate-900 px-1 rounded">
                      NinjaTrader.Custom.dll
                    </code>{" "}
                    remains. On a Linux CI runner this DLL will never exist regardless
                    of the path expansion. Without a{" "}
                    <code className="font-mono bg-slate-900 px-1 rounded">
                      Condition="$([MSBuild]::IsOSPlatform('Windows'))"
                    </code>{" "}
                    attribute on the{" "}
                    <code className="font-mono bg-slate-900 px-1 rounded">
                      &lt;Reference&gt;
                    </code>{" "}
                    item (or the containing{" "}
                    <code className="font-mono bg-slate-900 px-1 rounded">
                      &lt;ItemGroup&gt;
                    </code>
                    ), the Linux CI build will generate a missing-reference
                    warning or error. The plan does not include this condition.{" "}
                    <span className="text-red-300 font-semibold">
                      Moderate finding — plan should add the Condition.
                    </span>
                  </p>
                </div>
              </div>
            </div>
          </div>
        )}

        {/* ══════════════════════════════════════════════════════════════
            SECTION 3: Verification Step Platform Check
        ══════════════════════════════════════════════════════════════ */}
        {activeSection === 2 && (
          <div>
            <SectionHeader
              num="F"
              title="Verification Step Platform Check — All 17 Steps"
              sub="Source: §F Verification Matrix — 14 numbered + 3 sub-steps = 17 total"
            />

            {/* check_ascii.py note */}
            <div className="mb-6 bg-emerald-950/40 border border-emerald-700 rounded-lg p-4 text-sm text-emerald-200">
              <span className="font-bold">check_ascii.py existence:</span>{" "}
              The document provides positive evidence that{" "}
              <code className="font-mono bg-slate-900 px-1 rounded">
                check_ascii.py
              </code>{" "}
              <span className="text-emerald-300 font-semibold">exists at the repo root</span>. §B.2 states:
              "The repo-canonical ASCII-purity tool is{" "}
              <code className="font-mono bg-slate-900 px-1 rounded">
                check_ascii.py
              </code>{" "}
              at the repo root, referenced by CLAUDE.md section 'CRITICAL: ASCII-Only in All C# String Literals'."
              §D.4.b lines 89/99 are explicitly redirected to it. §B.3 lists it as
              <code className="font-mono bg-slate-900 px-1 rounded">
                {" "}/scripts/*.py{" "}
              </code>
              (scripts context) and §B.2 as repo root. Verification step #1 calls
              <code className="font-mono bg-slate-900 px-1 rounded">
                {" "}python check_ascii.py src/
              </code>
              — implying repo-root location. Document evidence is sufficient to confirm existence.
            </div>

            <div className="overflow-x-auto rounded-lg border border-slate-700">
              <table className="min-w-full text-sm">
                <thead>
                  <tr className="bg-slate-800 border-b border-slate-700">
                    <th className="px-3 py-3 text-left text-xs font-bold text-slate-300 uppercase tracking-wider w-12">
                      #
                    </th>
                    <th className="px-3 py-3 text-left text-xs font-bold text-slate-300 uppercase tracking-wider">
                      Gate
                    </th>
                    <th className="px-3 py-3 text-left text-xs font-bold text-slate-300 uppercase tracking-wider hidden md:table-cell">
                      Check
                    </th>
                    <th className="px-3 py-3 text-left text-xs font-bold text-slate-300 uppercase tracking-wider w-24">
                      Platform
                    </th>
                    <th className="px-3 py-3 text-left text-xs font-bold text-slate-300 uppercase tracking-wider hidden lg:table-cell">
                      PowerShell Equivalent / Note
                    </th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-slate-800">
                  {VERIFY_STEPS.map((step, i) => (
                    <tr
                      key={step.id}
                      className={`${
                        i % 2 === 0 ? "bg-slate-900" : "bg-slate-850"
                      } hover:bg-slate-800/70 transition-colors`}
                    >
                      <td className="px-3 py-3 font-mono text-xs text-slate-400 align-top">
                        {step.id}
                      </td>
                      <td className="px-3 py-3 text-xs text-slate-200 align-top font-semibold">
                        {step.gate}
                        <div className="mt-1 md:hidden">
                          <code className="text-slate-400 font-mono text-xs break-all">
                            {step.check}
                          </code>
                        </div>
                      </td>
                      <td className="px-3 py-3 align-top hidden md:table-cell">
                        <code className="text-xs text-slate-400 font-mono break-all">
                          {step.check}
                        </code>
                        <div className="text-xs text-slate-500 mt-1">
                          expect:{" "}
                          <span className="text-slate-300">{step.expected}</span>
                        </div>
                      </td>
                      <td className="px-3 py-3 align-top">
                        <Badge
                          label={step.platform === "runnable" ? "✓ runnable" : step.platform === "needs platform adjustment" ? "⚠ platform" : "✗ missing"}
                          colorClass={platformColor(step.platform)}
                        />
                      </td>
                      <td className="px-3 py-3 align-top hidden lg:table-cell">
                        {step.psEquivalent ? (
                          <code className="text-xs text-purple-300 font-mono break-all">
                            {step.psEquivalent}
                          </code>
                        ) : (
                          <span className="text-xs text-slate-500">
                            Runs natively on all platforms.
                          </span>
                        )}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>

            {/* expanded PS equivalents for mobile */}
            <div className="mt-6 lg:hidden">
              <h4 className="font-bold text-slate-300 mb-3 text-sm">
                PowerShell Equivalents (all flagged steps)
              </h4>
              <div className="space-y-3">
                {VERIFY_STEPS.filter((s) => s.psEquivalent).map((step) => (
                  <div
                    key={step.id}
                    className="bg-slate-800 border border-slate-700 rounded p-3"
                  >
                    <div className="text-xs font-bold text-slate-300 mb-1">
                      Step {step.id} — {step.gate}
                    </div>
                    <code className="text-xs text-purple-300 font-mono break-all">
                      {step.psEquivalent}
                    </code>
                  </div>
                ))}
              </div>
            </div>

            {/* POSIX flag summary */}
            <div className="mt-6 bg-yellow-950/30 border border-yellow-700 rounded-lg p-4">
              <h4 className="font-bold text-yellow-300 mb-2 text-sm">
                POSIX-Only Command Summary
              </h4>
              <p className="text-xs text-slate-300 mb-2">
                All steps using{" "}
                <code className="font-mono bg-slate-900 px-1 rounded">grep</code>,{" "}
                <code className="font-mono bg-slate-900 px-1 rounded">test -f</code>, or POSIX-shell hook syntax
                require replacement on Windows. Steps flagged:{" "}
                <strong className="text-yellow-200">
                  2, 3, 3b, 4, 5, 6, 7, 8, 9, 10, 11, 12, 12b, 12c, 13
                </strong>{" "}
                (15 of 17 steps).
              </p>
              <p className="text-xs text-slate-300">
                Steps{" "}
                <strong className="text-emerald-300">1</strong> (
                <code className="font-mono bg-slate-900 px-1 rounded">
                  python check_ascii.py
                </code>
                ) and{" "}
                <strong className="text-emerald-300">14</strong> (
                <code className="font-mono bg-slate-900 px-1 rounded">
                  pwsh -File ./deploy-sync.ps1
                </code>
                ) are natively cross-platform. Steps 9 and 10 additionally require the
                hook body to be executed — the hook is written in POSIX shell syntax (
                <code className="font-mono bg-slate-900 px-1 rounded">for/case/do/done</code>) and
                requires WSL or Git-for-Windows bash on Windows even with a PowerShell wrapper.
              </p>
            </div>
          </div>
        )}

        {/* ══════════════════════════════════════════════════════════════
            SECTION 4: Overall Summary
        ══════════════════════════════════════════════════════════════ */}
        {activeSection === 3 && (
          <div>
            <SectionHeader
              num="∑"
              title="Overall Summary — Plan Consistency Assessment"
              sub="Based strictly on findings from §C.1, §C.3, §C.4, §D.4, §F"
            />

            {/* verdict banner */}
            <div className="mb-8 bg-red-950/50 border-2 border-red-600 rounded-xl p-5">
              <div className="flex items-center gap-3 mb-2">
                <span className="text-3xl">🔴</span>
                <span className="text-xl font-bold text-red-300">
                  VERDICT: Needs Plan Update Before Engineering Begins
                </span>
              </div>
              <p className="text-sm text-slate-300 leading-relaxed">
                The plan contains{" "}
                <strong className="text-red-300">blocking issues</strong> (Type 2
                sites without documented fixes), a{" "}
                <strong className="text-yellow-300">moderate issue</strong> (missing
                Condition on Linting.csproj HintPath), and{" "}
                <strong className="text-slate-300">advisory notes</strong> (platform
                portability of verification steps, comment wording). The plan may NOT
                be handed to P4 Codex in its current state.
              </p>
            </div>

            {/* three columns */}
            <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
              {/* BLOCKING */}
              <div className="bg-red-950/30 border border-red-700 rounded-xl p-5">
                <h4 className="font-bold text-red-300 text-base mb-4 flex items-center gap-2">
                  <span className="text-lg">🚫</span> Blocking
                </h4>
                <p className="text-xs text-slate-400 mb-4">
                  Type 2 sites without a documented fix in the plan. Engineering MUST
                  NOT proceed until these are resolved.
                </p>
                <div className="space-y-4">
                  <div className="bg-red-900/30 rounded-lg p-3 border border-red-800">
                    <div className="font-semibold text-red-200 text-xs mb-1">
                      Site #5 — §C.3 Case 1 / §C.1
                    </div>
                    <p className="text-xs text-slate-300">
                      <code className="font-mono bg-slate-900 px-1 rounded">
                        _followerReplaceSpecs.TryRemove(sigName, out _)
                      </code>{" "}
                      inside the lambda is bypassed by the guard. The plan's NEW block
                      shows the guard added but does not address the TryRemove skip.
                      Permanent FSM reservation leak.
                    </p>
                    <div className="mt-2 text-xs text-red-300 font-mono">
                      File: V12_002.Orders.Callbacks.AccountOrders.cs:369
                    </div>
                  </div>
                  <div className="bg-red-900/30 rounded-lg p-3 border border-red-800">
                    <div className="font-semibold text-red-200 text-xs mb-1">
                      Site #11 — §C.3 Case 2 / §C.1
                    </div>
                    <p className="text-xs text-slate-300">
                      <code className="font-mono bg-slate-900 px-1 rounded">
                        _repairInFlight.TryRemove(repairKey, out _)
                      </code>{" "}
                      is only in the catch block. When early-return fires on the
                      success path with _isTerminating true, repairKey stays in
                      _repairInFlight permanently — future REAPER audits for that
                      account are permanently locked out.
                    </p>
                    <div className="mt-2 text-xs text-red-300 font-mono">
                      File: V12_002.REAPER.Audit.cs:136
                    </div>
                  </div>
                  <div className="bg-red-900/30 rounded-lg p-3 border border-red-800">
                    <div className="font-semibold text-red-200 text-xs mb-1">
                      Site #16 — §C.1 (unworked)
                    </div>
                    <p className="text-xs text-slate-300">
                      Purpose: "deferred dispatch retry (semaphore contention)". By
                      the classification rule, mention of semaphore contention implies
                      a Release may be needed after primary call. No code block
                      provided in §C.3 to confirm. Classified Type 2 pending
                      verification. Cannot confirm safe until source is reviewed.
                    </p>
                    <div className="mt-2 text-xs text-red-300 font-mono">
                      File: V12_002.SIMA.Dispatch.cs:60
                    </div>
                  </div>
                  <div className="bg-yellow-900/30 rounded-lg p-3 border border-yellow-800">
                    <div className="font-semibold text-yellow-200 text-xs mb-1">
                      Sites #12–15 — §C.1 (unverifiable)
                    </div>
                    <p className="text-xs text-slate-300">
                      Four REAPER.Audit.cs sister sites to Case 2 have no explicit
                      code blocks in §C.3. Cannot confirm whether _repairInFlight (or
                      analogous in-flight guards) exist at those sites. Unverifiable
                      until source is reviewed.
                    </p>
                  </div>
                </div>
              </div>

              {/* MODERATE */}
              <div className="bg-yellow-950/30 border border-yellow-700 rounded-xl p-5">
                <h4 className="font-bold text-yellow-300 text-base mb-4 flex items-center gap-2">
                  <span className="text-lg">⚠️</span> Moderate
                </h4>
                <p className="text-xs text-slate-400 mb-4">
                  Issues that will cause problems at integration / CI time but do not
                  block the kernel logic review.
                </p>
                <div className="space-y-4">
                  <div className="bg-yellow-900/30 rounded-lg p-3 border border-yellow-800">
                    <div className="font-semibold text-yellow-200 text-xs mb-1">
                      Linting.csproj — missing Condition (§D.4.a)
                    </div>
                    <p className="text-xs text-slate-300">
                      The{" "}
                      <code className="font-mono bg-slate-900 px-1 rounded">
                        &lt;Reference&gt;
                      </code>{" "}
                      for NinjaTrader.Custom.dll (line 37) has no{" "}
                      <code className="font-mono bg-slate-900 px-1 rounded">
                        Condition="$([MSBuild]::IsOSPlatform('Windows'))"
                      </code>
                      . On Linux CI the HintPath resolves to a non-existent path.
                      Plan should add a Windows-only condition on this item or its
                      containing ItemGroup.
                    </p>
                  </div>
                  <div className="bg-yellow-900/30 rounded-lg p-3 border border-yellow-800">
                    <div className="font-semibold text-yellow-200 text-xs mb-1">
                      Verification matrix — 15 of 17 steps POSIX-only (§F)
                    </div>
                    <p className="text-xs text-slate-300">
                      Steps 2, 3, 3b, 4, 5, 6, 7, 8, 9, 10, 11, 12, 12b, 12c, 13
                      use{" "}
                      <code className="font-mono bg-slate-900 px-1 rounded">
                        grep
                      </code>
                      ,{" "}
                      <code className="font-mono bg-slate-900 px-1 rounded">
                        test -f
                      </code>
                      , or POSIX shell. These do not run on Windows without WSL or
                      Git-for-Windows bash. The plan should either add a "Linux/WSL
                      required" note or provide PowerShell equivalents as an
                      appendix.
                    </p>
                  </div>
                  <div className="bg-yellow-900/30 rounded-lg p-3 border border-yellow-800">
                    <div className="font-semibold text-yellow-200 text-xs mb-1">
                      Hook body is POSIX shell (§D.3)
                    </div>
                    <p className="text-xs text-slate-300">
                      The LFS pointer gate and 5 MiB size gate in the hook amendment
                      use POSIX shell syntax (
                      <code className="font-mono bg-slate-900 px-1 rounded">
                        for/case/do/done
                      </code>
                      ). On Windows, the hook runs via Git-for-Windows bash (usually
                      present) but if the Director uses a bare pwsh environment the
                      hook will not execute. Plan should note the WSL/Git-bash
                      dependency.
                    </p>
                  </div>
                </div>
              </div>

              {/* ADVISORY */}
              <div className="bg-slate-800/60 border border-slate-600 rounded-xl p-5">
                <h4 className="font-bold text-slate-300 text-base mb-4 flex items-center gap-2">
                  <span className="text-lg">💡</span> Advisory
                </h4>
                <p className="text-xs text-slate-400 mb-4">
                  Low-severity notes that do not block engineering but should be
                  addressed before UltraReview.
                </p>
                <div className="space-y-4">
                  <div className="bg-slate-700/40 rounded-lg p-3 border border-slate-600">
                    <div className="font-semibold text-slate-200 text-xs mb-1">
                      deploy-sync.ps1 line 89 comment wording (§D.4.b)
                    </div>
                    <p className="text-xs text-slate-300">
                      The updated comment contains{" "}
                      <code className="font-mono bg-slate-900 px-1 rounded">
                        (Join-Path $PSScriptRoot 'check_ascii.py')
                      </code>{" "}
                      which is NOT evaluated at runtime (it is in a{" "}
                      <code className="font-mono bg-slate-900 px-1 rounded">#</code>{" "}
                      comment). A developer will see the literal expression text.
                      Recommend using a plain English path or adding a note that
                      $PSScriptRoot = repo root.
                    </p>
                  </div>
                  <div className="bg-slate-700/40 rounded-lg p-3 border border-slate-600">
                    <div className="font-semibold text-slate-200 text-xs mb-1">
                      Sites #12–15 classification gap (§C.1, §C.3)
                    </div>
                    <p className="text-xs text-slate-300">
                      Four REAPER.Audit.cs sister sites are listed in §C.3 as
                      "same transform B" but no code is shown. If any of these sites
                      also have an in-flight guard pattern like #11, they should be
                      promoted to Type 2. P4 Codex should check these before applying
                      the transform.
                    </p>
                  </div>
                  <div className="bg-slate-700/40 rounded-lg p-3 border border-slate-600">
                    <div className="font-semibold text-slate-200 text-xs mb-1">
                      Verification step count labelling (§F)
                    </div>
                    <p className="text-xs text-slate-300">
                      The §F table has 14 numbered entries (1, 2, 3, 3b, 4–14) =
                      17 rows total. The plan title says "17 steps" which is correct
                      if sub-steps 3b, 12b, 12c are counted separately. No
                      inconsistency, but the numbering could confuse an engineer
                      expecting sequential integers.
                    </p>
                  </div>
                  <div className="bg-slate-700/40 rounded-lg p-3 border border-slate-600">
                    <div className="font-semibold text-slate-200 text-xs mb-1">
                      check_ascii.py location (§B.2 vs §B.3)
                    </div>
                    <p className="text-xs text-slate-300">
                      §B.2 places{" "}
                      <code className="font-mono bg-slate-900 px-1 rounded">
                        check_ascii.py
                      </code>{" "}
                      at "the repo root" while §B.3 lists it alongside
                      "/scripts/*.py". §F step 1 invokes it as{" "}
                      <code className="font-mono bg-slate-900 px-1 rounded">
                        python check_ascii.py
                      </code>{" "}
                      (no path prefix), implying repo root. The plan should
                      clarify the canonical location to avoid ambiguity.
                    </p>
                  </div>
                </div>
              </div>
            </div>

            {/* final table */}
            <div className="mt-8 bg-slate-800/60 border border-slate-600 rounded-xl p-5">
              <h4 className="font-bold text-white text-base mb-4">
                Exact Items Requiring Plan Update (before P4 handoff)
              </h4>
              <div className="overflow-x-auto">
                <table className="min-w-full text-xs">
                  <thead>
                    <tr className="border-b border-slate-600">
                      <th className="text-left px-3 py-2 text-slate-400 uppercase font-bold">
                        Priority
                      </th>
                      <th className="text-left px-3 py-2 text-slate-400 uppercase font-bold">
                        Item
                      </th>
                      <th className="text-left px-3 py-2 text-slate-400 uppercase font-bold">
                        Section
                      </th>
                      <th className="text-left px-3 py-2 text-slate-400 uppercase font-bold">
                        Required Fix
                      </th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-slate-700">
                    {[
                      {
                        p: "🚫 Blocking",
                        item: "Site #5 Type 2",
                        sec: "§C.3 Case 1",
                        fix: "Plan must show _followerReplaceSpecs.TryRemove still executes on early-return path (e.g., move TryRemove before guard, or add finally block).",
                        pc: "text-red-300",
                      },
                      {
                        p: "🚫 Blocking",
                        item: "Site #11 Type 2",
                        sec: "§C.3 Case 2",
                        fix: "Plan must show _repairInFlight.TryRemove(repairKey) executes on the guard-exit path (e.g., add TryRemove inside the lambda before return, or restructure).",
                        pc: "text-red-300",
                      },
                      {
                        p: "🚫 Blocking",
                        item: "Site #16 unconfirmed Type 2",
                        sec: "§C.1",
                        fix: "Add worked code block for site #16 confirming whether a semaphore Release follows the primary call. If yes, add fix. If no, confirm Type 1.",
                        pc: "text-red-300",
                      },
                      {
                        p: "🚫 Blocking",
                        item: "Sites #12–15 unverifiable",
                        sec: "§C.1, §C.3",
                        fix: "P3 must review source for sites #12–15 and confirm no in-flight guard analogous to _repairInFlight exists. If found, promote to Type 2 and provide fix.",
                        pc: "text-red-300",
                      },
                      {
                        p: "⚠️ Moderate",
                        item: "Linting.csproj no Condition",
                        sec: "§D.4.a",
                        fix: "Add Condition=\"$([MSBuild]::IsOSPlatform('Windows'))\" to the NinjaTrader.Custom.dll Reference (or its ItemGroup) to prevent Linux CI failure.",
                        pc: "text-yellow-300",
                      },
                      {
                        p: "⚠️ Moderate",
                        item: "§F matrix POSIX-only",
                        sec: "§F (15 steps)",
                        fix: "Add 'Linux/WSL required' prerequisite note to §F header, or add a PowerShell appendix for steps 2, 3, 3b, 4, 5, 6, 7, 8, 11, 12, 12b, 12c, 13.",
                        pc: "text-yellow-300",
                      },
                      {
                        p: "💡 Advisory",
                        item: "deploy-sync.ps1 line 89 comment",
                        sec: "§D.4.b",
                        fix: "Rephrase comment to avoid unevaluated Join-Path syntax, e.g.: '# Fix: python <repo-root>/check_ascii.py src/'",
                        pc: "text-slate-300",
                      },
                    ].map((row, i) => (
                      <tr key={i} className="hover:bg-slate-700/30">
                        <td className={`px-3 py-2 font-semibold ${row.pc}`}>
                          {row.p}
                        </td>
                        <td className="px-3 py-2 font-mono text-slate-200">
                          {row.item}
                        </td>
                        <td className="px-3 py-2 font-mono text-indigo-300">
                          {row.sec}
                        </td>
                        <td className="px-3 py-2 text-slate-300 leading-relaxed">
                          {row.fix}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </div>
          </div>
        )}
      </main>

      {/* ── footer ── */}
      <footer className="mt-12 border-t border-slate-800 bg-slate-950 py-4 px-4">
        <p className="text-xs text-slate-500 text-center max-w-7xl mx-auto">
          Reviewed by{" "}
          <span className="text-indigo-400 font-semibold">Claude Opus 4.5</span>{" "}
          · All findings strictly sourced from{" "}
          <span className="font-mono">implementation_plan.md @ mission-uni-5-full-sync</span>{" "}
          · ADR-019 Sovereign Substrate Repair · Status:{" "}
          <span className="text-red-400 font-semibold">
            NEEDS PLAN UPDATE — P4 handoff BLOCKED
          </span>
        </p>
      </footer>
    </div>
  );
}
