// ============================================================
// DATA extracted from implementation_plan.md
// Branch: mission-uni-5-full-sync
// ============================================================

const buildTagDelta = 'Build 1111.002-v28.0 → Build 1111.003-v28.0-adr019';
export const buildTagDeltaRaw = 'Build 1111.002-v28.0 -> Build 1111.003-v28.0-adr019';

export interface LambdaSite {
  id: number;
  file: string;
  line: number;
  method: string;
  purpose: string;
  redTeamCritical: boolean;
  transform: 'A' | 'B';
  classification: 'Type 1' | 'Type 2' | 'Unverifiable';
  evidence: string;
  needsPlanUpdate: boolean;
  hasWorkedCode: boolean;
  oldCode?: string;
  newCode?: string;
  bypassedVariable?: string;
}

export const lambdaSites: LambdaSite[] = [
  {
    id: 1, file: 'src/V12_002.Orders.Callbacks.AccountOrders.cs', line: 146,
    method: 'OnAccountOrderUpdate', purpose: 'ProcessAccountOrderQueue (broker callback enqueue)',
    redTeamCritical: true, transform: 'A', classification: 'Type 1',
    evidence: 'No dictionary removal, semaphore release, flag clear, or shared-resource operation visible in lambda body per Section C.1 purpose.',
    needsPlanUpdate: false, hasWorkedCode: false,
  },
  {
    id: 2, file: 'src/V12_002.Orders.Callbacks.AccountOrders.cs', line: 162,
    method: 'ProcessAccountOrderQueue', purpose: 'reschedule on budget exhaustion',
    redTeamCritical: true, transform: 'A', classification: 'Type 1',
    evidence: 'No cleanup keywords (TryRemove, Release, Clear, flush) in purpose. Rescheduling is pure work.',
    needsPlanUpdate: false, hasWorkedCode: false,
  },
  {
    id: 3, file: 'src/V12_002.Orders.Callbacks.AccountOrders.cs', line: 173,
    method: 'ProcessAccountOrderQueue', purpose: 're-enqueue on flatten contention',
    redTeamCritical: true, transform: 'A', classification: 'Type 1',
    evidence: 'No cleanup keywords in purpose. Re-enqueue is pure work.',
    needsPlanUpdate: false, hasWorkedCode: false,
  },
  {
    id: 4, file: 'src/V12_002.Orders.Callbacks.AccountOrders.cs', line: 181,
    method: 'ProcessAccountOrderQueue', purpose: 'drain remaining queue',
    redTeamCritical: true, transform: 'A', classification: 'Type 1',
    evidence: 'No cleanup keywords in purpose. Queue drain is pure work.',
    needsPlanUpdate: false, hasWorkedCode: false,
  },
  {
    id: 5, file: 'src/V12_002.Orders.Callbacks.AccountOrders.cs', line: 369,
    method: 'HandleMatchedFollowerOrder', purpose: 'SubmitFollowerReplacement (FSM two-phase)',
    redTeamCritical: true, transform: 'A', classification: 'Type 2',
    evidence: 'Lambda body executes SubmitFollowerReplacement() THEN _followerReplaceSpecs.TryRemove(sigName, out _). Early return bypasses the TryRemove, leaving a stale entry in the concurrent dictionary.',
    needsPlanUpdate: true, hasWorkedCode: true,
    oldCode: `TriggerCustomEvent(o =>
{
    // [P2 FSM CONSISTENCY]: Re-read price/qty from spec at execution time.
    SubmitFollowerReplacement(sigName, acctNameCapture,
        fsmCapture.PendingPrice, fsmCapture.PendingQty, fsmCapture);
    _followerReplaceSpecs.TryRemove(sigName, out _);
}, null);`,
    newCode: `TriggerCustomEvent(o =>
{
    if (_isTerminating) return;  // ADR-019 orphan guard
    // [P2 FSM CONSISTENCY]: Re-read price/qty from spec at execution time.
    SubmitFollowerReplacement(sigName, acctNameCapture,
        fsmCapture.PendingPrice, fsmCapture.PendingQty, fsmCapture);
    _followerReplaceSpecs.TryRemove(sigName, out _);
}, null);`,
    bypassedVariable: '_followerReplaceSpecs',
  },
  {
    id: 6, file: 'src/V12_002.Orders.Callbacks.AccountOrders.cs', line: 410,
    method: 'HandleMatchedFollowerOrder', purpose: 'SubmitFollowerTargetReplacement',
    redTeamCritical: true, transform: 'A', classification: 'Unverifiable',
    evidence: 'No explicit code block in Section C.3. Purpose does not mention TryRemove/Release/Clear/flush, but site is in the same method as #5 which is Type 2. Cannot confirm without source.',
    needsPlanUpdate: false, hasWorkedCode: false,
  },
  {
    id: 7, file: 'src/V12_002.Orders.Callbacks.AccountOrders.cs', line: 463,
    method: 'HandleMatchedFollowerOrder', purpose: 'RestoreCascadedTargets (stop-fill restore)',
    redTeamCritical: true, transform: 'A', classification: 'Unverifiable',
    evidence: 'No explicit code block. "Restore" may modify shared state after primary call. Cannot confirm without source.',
    needsPlanUpdate: false, hasWorkedCode: false,
  },
  {
    id: 8, file: 'src/V12_002.Orders.Callbacks.AccountOrders.cs', line: 591,
    method: 'ExecuteFollowerCascadeCleanup', purpose: 'EmergencyFlattenSingleFleetAccount (CASCADE-FILLED)',
    redTeamCritical: true, transform: 'A', classification: 'Unverifiable',
    evidence: 'No explicit code block. Emergency flatten may involve state cleanup after primary call. Cannot confirm without source.',
    needsPlanUpdate: false, hasWorkedCode: false,
  },
  {
    id: 9, file: 'src/V12_002.Orders.Callbacks.cs', line: 389,
    method: 'HandleOrderCancelled', purpose: 'RestoreCascadedTargets (master-side)',
    redTeamCritical: true, transform: 'A', classification: 'Unverifiable',
    evidence: 'No explicit code block. "Restore" may modify shared state after primary call. Cannot confirm without source.',
    needsPlanUpdate: false, hasWorkedCode: false,
  },
  {
    id: 10, file: 'src/V12_002.Orders.Callbacks.Execution.cs', line: 235,
    method: 'OnAccountExecutionUpdate', purpose: 'UpdateAccountMetricsFromAccount',
    redTeamCritical: true, transform: 'A', classification: 'Type 1',
    evidence: 'No cleanup keywords in purpose. Metrics update is pure work.',
    needsPlanUpdate: false, hasWorkedCode: false,
  },
  {
    id: 11, file: 'src/V12_002.REAPER.Audit.cs', line: 136,
    method: 'AuditAccountState', purpose: 'ProcessReaperRepairQueue (flat desync repair)',
    redTeamCritical: true, transform: 'B', classification: 'Type 1',
    evidence: 'Worked code in Section C.3 Case 2: lambda body is single call ProcessReaperRepairQueue(). No cleanup after primary call in the lambda itself. _repairInFlight.TryRemove is in the catch block, not the lambda body.',
    needsPlanUpdate: false, hasWorkedCode: true,
    oldCode: `try { TriggerCustomEvent(o => ProcessReaperRepairQueue(), null); }
catch (Exception repairTriggerEx)
{
    _repairInFlight.TryRemove(repairKey, out _);
    Print("[REAPER] TriggerCustomEvent failed for " + repairKey);
}`,
    newCode: `try { TriggerCustomEvent(o => { if (_isTerminating) return; ProcessReaperRepairQueue(); }, null); }
catch (Exception repairTriggerEx)
{
    _repairInFlight.TryRemove(repairKey, out _);
    Print("[REAPER] TriggerCustomEvent failed for " + repairKey);
}`,
    bypassedVariable: 'None in lambda body — cleanup is in catch block',
  },
  {
    id: 12, file: 'src/V12_002.REAPER.Audit.cs', line: 183,
    method: 'AuditAccountState', purpose: 'ProcessReaperFlattenQueue (critical desync)',
    redTeamCritical: true, transform: 'B', classification: 'Type 1',
    evidence: 'Sister site to #11 (Section C.3). Same Transform B pattern: single method call in lambda, no cleanup after.',
    needsPlanUpdate: false, hasWorkedCode: false,
  },
  {
    id: 13, file: 'src/V12_002.REAPER.Audit.cs', line: 250,
    method: 'AuditAccountState', purpose: 'ProcessReaperNakedStopQueue (naked stop)',
    redTeamCritical: true, transform: 'B', classification: 'Type 1',
    evidence: 'Sister site to #11. Same Transform B pattern.',
    needsPlanUpdate: false, hasWorkedCode: false,
  },
  {
    id: 14, file: 'src/V12_002.REAPER.Audit.cs', line: 327,
    method: 'AuditAccountState', purpose: 'ProcessReaperFlattenQueue (master flatten)',
    redTeamCritical: true, transform: 'B', classification: 'Type 1',
    evidence: 'Sister site to #11. Same Transform B pattern.',
    needsPlanUpdate: false, hasWorkedCode: false,
  },
  {
    id: 15, file: 'src/V12_002.REAPER.Audit.cs', line: 372,
    method: 'AuditAccountState', purpose: 'ProcessReaperNakedStopQueue (master naked stop)',
    redTeamCritical: true, transform: 'B', classification: 'Type 1',
    evidence: 'Sister site to #11. Same Transform B pattern.',
    needsPlanUpdate: false, hasWorkedCode: false,
  },
  {
    id: 16, file: 'src/V12_002.SIMA.Dispatch.cs', line: 60,
    method: 'ExecuteSmartDispatchEntry', purpose: 'deferred dispatch retry (semaphore contention)',
    redTeamCritical: true, transform: 'A', classification: 'Type 2',
    evidence: 'Purpose explicitly mentions "semaphore contention". A semaphore release or decrement after the primary dispatch call would be bypassed by early return, potentially causing permanent semaphore starvation.',
    needsPlanUpdate: true, hasWorkedCode: false,
  },
  {
    id: 17, file: 'src/V12_002.SIMA.Dispatch.cs', line: 610,
    method: 'ExecuteSmartDispatchEntry', purpose: 'PumpFleetDispatch prime',
    redTeamCritical: true, transform: 'A', classification: 'Type 1',
    evidence: 'No cleanup keywords in purpose. Pump prime is pure work.',
    needsPlanUpdate: false, hasWorkedCode: false,
  },
  {
    id: 18, file: 'src/V12_002.SIMA.Flatten.cs', line: 82,
    method: 'InitiateFlattenOps', purpose: 'PumpFlattenOps kickoff',
    redTeamCritical: false, transform: 'A', classification: 'Type 1',
    evidence: 'No cleanup keywords in purpose. Kickoff is pure work.',
    needsPlanUpdate: false, hasWorkedCode: false,
  },
  {
    id: 19, file: 'src/V12_002.SIMA.Flatten.cs', line: 201,
    method: 'PumpFlattenOps', purpose: 'chain to next account',
    redTeamCritical: false, transform: 'A', classification: 'Type 1',
    evidence: 'No cleanup keywords in purpose. Chaining is pure work.',
    needsPlanUpdate: false, hasWorkedCode: false,
  },
  {
    id: 20, file: 'src/V12_002.SIMA.Flatten.cs', line: 319,
    method: 'FlattenAccountPosition', purpose: 're-kick on completion',
    redTeamCritical: false, transform: 'A', classification: 'Type 1',
    evidence: 'No cleanup keywords in purpose. Re-kick is pure work.',
    needsPlanUpdate: false, hasWorkedCode: false,
  },
  {
    id: 21, file: 'src/V12_002.SIMA.Fleet.cs', line: 174,
    method: 'PumpFleetDispatch', purpose: 'chain from finally',
    redTeamCritical: false, transform: 'A', classification: 'Type 2',
    evidence: 'Purpose states "chain from finally" — a finally block typically contains cleanup (resource release, flag clear). If the guard returns before the finally-equivalent work, cleanup is bypassed.',
    needsPlanUpdate: true, hasWorkedCode: false,
  },
  {
    id: 22, file: 'src/V12_002.SIMA.Fleet.cs', line: 262,
    method: 'PumpFleetDispatch', purpose: 'chain after XorShadow CRC fail',
    redTeamCritical: false, transform: 'A', classification: 'Unverifiable',
    evidence: 'No explicit code block. "After CRC fail" may involve error-state cleanup. Cannot confirm without source.',
    needsPlanUpdate: false, hasWorkedCode: false,
  },
  {
    id: 23, file: 'src/V12_002.SIMA.Lifecycle.cs', line: 57,
    method: 'OnParameterChanged', purpose: 'ProcessApplySimaState deferred toggle',
    redTeamCritical: false, transform: 'A', classification: 'Type 1',
    evidence: 'No cleanup keywords in purpose. Deferred toggle is pure work.',
    needsPlanUpdate: false, hasWorkedCode: false,
  },
  {
    id: 24, file: 'src/V12_002.Trailing.StopUpdate.cs', line: 64,
    method: 'OnOrderUpdate', purpose: 'RestoreCascadedTargets (trailing restore)',
    redTeamCritical: false, transform: 'A', classification: 'Unverifiable',
    evidence: 'No explicit code block. "Restore" may modify shared state after primary call. Cannot confirm without source.',
    needsPlanUpdate: false, hasWorkedCode: false,
  },
  {
    id: 25, file: 'src/V12_002.UI.Compliance.cs', line: 286,
    method: 'OnAccountExecutionUpdate', purpose: 'ProcessAccountExecutionQueue (marshal)',
    redTeamCritical: false, transform: 'A', classification: 'Type 1',
    evidence: 'No cleanup keywords in purpose. Marshal is pure work.',
    needsPlanUpdate: false, hasWorkedCode: false,
  },
  {
    id: 26, file: 'src/V12_002.UI.Compliance.cs', line: 304,
    method: 'ProcessAccountExecutionQueue', purpose: 'reschedule on budget',
    redTeamCritical: false, transform: 'A', classification: 'Type 1',
    evidence: 'No cleanup keywords in purpose. Reschedule is pure work.',
    needsPlanUpdate: false, hasWorkedCode: false,
  },
  {
    id: 27, file: 'src/V12_002.UI.Compliance.cs', line: 316,
    method: 'ProcessAccountExecutionQueue', purpose: 'flatten-contention bailout',
    redTeamCritical: false, transform: 'A', classification: 'Unverifiable',
    evidence: 'No explicit code block. "Bailout" may involve state cleanup. Cannot confirm without source.',
    needsPlanUpdate: false, hasWorkedCode: false,
  },
  {
    id: 28, file: 'src/V12_002.UI.Compliance.cs', line: 324,
    method: 'ProcessAccountExecutionQueue', purpose: 'drain remaining',
    redTeamCritical: false, transform: 'A', classification: 'Type 1',
    evidence: 'No cleanup keywords in purpose. Drain is pure work.',
    needsPlanUpdate: false, hasWorkedCode: false,
  },
  {
    id: 29, file: 'src/V12_002.UI.IPC.cs', line: 328,
    method: 'ProcessIpcCommands', purpose: 'reschedule IPC queue',
    redTeamCritical: false, transform: 'A', classification: 'Type 1',
    evidence: 'No cleanup keywords in purpose. Reschedule is pure work.',
    needsPlanUpdate: false, hasWorkedCode: false,
  },
  {
    id: 30, file: 'src/V12_002.UI.IPC.Server.cs', line: 277,
    method: 'OnIpcCommand', purpose: 'TCP server callback -> strategy marshal',
    redTeamCritical: false, transform: 'B', classification: 'Type 1',
    evidence: 'Worked code in Section C.3 Case 3: lambda body is single call ProcessIpcCommands(). No cleanup after primary call.',
    needsPlanUpdate: false, hasWorkedCode: true,
    oldCode: `TriggerCustomEvent(o => ProcessIpcCommands(), null);`,
    newCode: `TriggerCustomEvent(o => { if (_isTerminating) return; ProcessIpcCommands(); }, null);`,
    bypassedVariable: 'None in lambda body',
  },
  {
    id: 31, file: 'src/V12_002.cs', line: 373,
    method: 'ScheduleActorDrain', purpose: 'TryDrain (actor mailbox)',
    redTeamCritical: false, transform: 'A', classification: 'Type 1',
    evidence: 'No cleanup keywords in purpose. Actor drain is pure work.',
    needsPlanUpdate: false, hasWorkedCode: false,
  },
  {
    id: 32, file: 'src/V12_002.REAPER.cs', line: 132,
    method: 'ReaperAuditThread', purpose: 'AuditApexPositions (bg thread marshal)',
    redTeamCritical: false, transform: 'A', classification: 'Type 1',
    evidence: 'No cleanup keywords in purpose. Audit marshal is pure work.',
    needsPlanUpdate: false, hasWorkedCode: false,
  },
];

export interface PathChange {
  id: number;
  file: string;
  line: number;
  oldPath: string;
  newPath: string;
  consistent: boolean;
  notes: string;
}

export const pathChanges: PathChange[] = [
  {
    id: 1, file: 'deploy-sync.ps1', line: 8,
    oldPath: '$RepoRoot = "C:\\\\WSGTA\\\\universal-or-strategy"',
    newPath: '$RepoRoot = $PSScriptRoot',
    consistent: true,
    notes: 'Section D.4.b Sub-block 1. $PSScriptRoot resolves to script directory (repo root). Matches model used at deploy-vm-safe.ps1:10.',
  },
  {
    id: 2, file: 'deploy-sync.ps1', line: 9,
    oldPath: '$NtCustomDir = "C:\\\\Users\\\\Mohammed Khalid\\\\Documents\\\\NinjaTrader 8\\\\bin\\\\Custom"',
    newPath: '$NtCustomDir = Join-Path $env:USERPROFILE "Documents\\\\NinjaTrader 8\\\\bin\\\\Custom"',
    consistent: true,
    notes: 'Section D.4.b Sub-block 1. $env:USERPROFILE is the substitution model for user-profile paths per Section B.2.',
  },
  {
    id: 3, file: 'deploy-sync.ps1', line: 89,
    oldPath: '# Fix: run C:\\\\tmp\\\\byte_purge.py, then re-run deploy-sync.ps1',
    newPath: "# Fix: run `python (Join-Path $PSScriptRoot 'check_ascii.py') src/`, then re-run deploy-sync.ps1",
    consistent: false,
    notes: 'Section D.4.b Sub-block 2. INCONSISTENCY: Join-Path inside a PowerShell comment is NOT evaluated at runtime. Comments are purely textual. The developer reads the literal string "Join-Path $PSScriptRoot \'check_ascii.py\'" as documentation only — they must manually construct the command. This is a documentation issue, not a functional one.',
  },
  {
    id: 4, file: 'deploy-sync.ps1', line: 99,
    oldPath: 'Write-Host "  Fix: python C:\\\\tmp\\\\byte_purge.py  then re-run deploy-sync.ps1"',
    newPath: 'Write-Host "  Fix: python $(Join-Path $PSScriptRoot \'check_ascii.py\') src/  then re-run deploy-sync.ps1"',
    consistent: true,
    notes: 'Section D.4.b Sub-block 2. This is a Write-Host string (not a comment), so $(Join-Path ...) IS evaluated at runtime and will produce the correct path.',
  },
  {
    id: 5, file: 'Linting.csproj', line: 37,
    oldPath: '<HintPath>C:\\\\Users\\\\Mohammed Khalid\\\\Documents\\\\NinjaTrader 8\\\\bin\\\\Custom\\\\NinjaTrader.Custom.dll</HintPath>',
    newPath: '<HintPath>$(UserProfile)\\\\Documents\\\\NinjaTrader 8\\\\bin\\\\Custom\\\\NinjaTrader.Custom.dll</HintPath>',
    consistent: true,
    notes: 'Section D.4.a. MSBuild resolves $(UserProfile) on Windows and Linux/macOS. HOWEVER: the document does NOT add a Condition attribute for Linux CI. On Linux CI runners, the NinjaTrader DLL will not exist at any path, which may cause a build warning or error. A Condition=" \'$(OS)\' == \'Windows_NT\' " would be advisable.',
  },
];

export interface VerificationStep {
  id: string;
  gate: string;
  check: string;
  expected: string;
  status: 'runnable' | 'needs_platform_adjustment' | 'dependency_missing';
  flagged: boolean;
  flaggedReason?: string;
  powershellEquivalent?: string;
}

export const verificationSteps: VerificationStep[] = [
  {
    id: '1', gate: 'ASCII purity (C#)',
    check: 'python check_ascii.py src/',
    expected: 'zero findings',
    status: 'needs_platform_adjustment', flagged: true,
    flaggedReason: 'Uses "python" command — on Windows, may require "py" or full path. Also assumes check_ascii.py exists at repo root.',
    powershellEquivalent: 'py check_ascii.py src/  (or pwsh -File scripts/run_ascii_check.ps1 if wrapper exists)',
  },
  {
    id: '2', gate: 'Lock-ban',
    check: "grep -n 'lock(stateLock)' src/",
    expected: 'zero hits',
    status: 'needs_platform_adjustment', flagged: true,
    flaggedReason: 'grep is POSIX-only. Not natively available on Windows without Git Bash or WSL.',
    powershellEquivalent: 'Select-String -Path src/* -Pattern "lock\\(stateLock\\)" -List',
  },
  {
    id: '3', gate: 'Guard coverage (Transform A)',
    check: "grep -c 'if (_isTerminating) return; // ADR-019 orphan guard' src/",
    expected: '26 hits',
    status: 'needs_platform_adjustment', flagged: true,
    flaggedReason: 'grep -c is POSIX-only.',
    powershellEquivalent: '(Select-String -Path src/* -Pattern "if \\(_isTerminating\\) return;  // ADR-019 orphan guard" -SimpleMatch).Count',
  },
  {
    id: '3b', gate: 'REAPER.Audit.cs per-file coverage',
    check: "grep -c 'if (_isTerminating) return;' src/V12_002.REAPER.Audit.cs",
    expected: '5 hits',
    status: 'needs_platform_adjustment', flagged: true,
    flaggedReason: 'grep -c is POSIX-only.',
    powershellEquivalent: '(Select-String -Path "src/V12_002.REAPER.Audit.cs" -Pattern "if \\(_isTerminating\\) return;").Count',
  },
  {
    id: '4', gate: 'Guard coverage (Transform B)',
    check: "grep -cE 'o => \\\\{ if \\\\(\\\\_isTerminating\\\\) return; [A-Za-z]+\\\\(\\\\); \\\\}' src/",
    expected: '6 hits',
    status: 'needs_platform_adjustment', flagged: true,
    flaggedReason: 'grep -cE (extended regex) is POSIX-only.',
    powershellEquivalent: '(Select-String -Path src/* -Pattern "o => \\{ if \\(_isTerminating\\) return; [A-Za-z]+\\(\\); \\}").Count',
  },
  {
    id: '5', gate: 'Devcontainer presence',
    check: 'test -f .devcontainer/devcontainer.json && test -f .devcontainer/Dockerfile',
    expected: 'exit 0',
    status: 'needs_platform_adjustment', flagged: true,
    flaggedReason: 'test -f is POSIX-only shell builtin.',
    powershellEquivalent: 'if (Test-Path .devcontainer/devcontainer.json -and (Test-Path .devcontainer/Dockerfile)) { exit 0 } else { exit 1 }',
  },
  {
    id: '6', gate: 'Label-sync presence',
    check: 'test -f .github/workflows/label-sync.yml && test -f .github/labels.yml',
    expected: 'exit 0',
    status: 'needs_platform_adjustment', flagged: true,
    flaggedReason: 'test -f is POSIX-only shell builtin.',
    powershellEquivalent: 'if (Test-Path .github/workflows/label-sync.yml -and (Test-Path .github/labels.yml)) { exit 0 } else { exit 1 }',
  },
  {
    id: '7', gate: 'LFS config presence',
    check: 'test -f .gitattributes',
    expected: 'exit 0',
    status: 'needs_platform_adjustment', flagged: true,
    flaggedReason: 'test -f is POSIX-only shell builtin.',
    powershellEquivalent: 'if (Test-Path .gitattributes) { exit 0 } else { exit 1 }',
  },
  {
    id: '8', gate: 'Hook amendment',
    check: 'grep -q "ADR-019: LFS pointer gate" .git/hooks/pre-commit',
    expected: 'exit 0',
    status: 'needs_platform_adjustment', flagged: true,
    flaggedReason: 'grep -q is POSIX-only.',
    powershellEquivalent: 'if (Select-String -Path ".git/hooks/pre-commit" -Pattern "ADR-019: LFS pointer gate" -Quiet) { exit 0 } else { exit 1 }',
  },
  {
    id: '9', gate: 'Hook live test (LFS)',
    check: 'stage a non-LFS *.dll',
    expected: 'hook rejects with ADR-019 message',
    status: 'runnable', flagged: false,
    flaggedReason: undefined,
    powershellEquivalent: undefined,
  },
  {
    id: '10', gate: 'Hook live test (size)',
    check: 'stage a non-LFS file > 5 MiB',
    expected: 'hook rejects with ADR-019 message',
    status: 'runnable', flagged: false,
    flaggedReason: undefined,
    powershellEquivalent: undefined,
  },
  {
    id: '11', gate: 'Portability (Linting)',
    check: "grep -c 'C:\\\\\\\\Users\\\\\\\\Mohammed' Linting.csproj",
    expected: 'zero hits',
    status: 'needs_platform_adjustment', flagged: true,
    flaggedReason: 'grep -c is POSIX-only.',
    powershellEquivalent: '(Select-String -Path "Linting.csproj" -Pattern "C:\\\\Users\\\\Mohammed" -SimpleMatch).Count',
  },
  {
    id: '12', gate: 'Portability (deploy) — user profile',
    check: "grep -nE 'C:\\\\\\\\\\\\\\\\Users\\\\\\\\\\\\\\\\' deploy-sync.ps1",
    expected: 'zero hits',
    status: 'needs_platform_adjustment', flagged: true,
    flaggedReason: 'grep -nE is POSIX-only.',
    powershellEquivalent: 'Select-String -Path "deploy-sync.ps1" -Pattern "C:\\\\Users\\\\\\\\"',
  },
  {
    id: '12b', gate: 'Portability (deploy) — repo/tool paths',
    check: "grep -nE 'C:\\\\\\\\\\\\\\\\(WSGTA\\|tmp)\\\\\\\\\\\\\\\\' deploy-sync.ps1",
    expected: 'zero hits',
    status: 'needs_platform_adjustment', flagged: true,
    flaggedReason: 'grep -nE is POSIX-only.',
    powershellEquivalent: 'Select-String -Path "deploy-sync.ps1" -Pattern "C:\\\\(WSGTA|tmp)\\\\\\\\"',
  },
  {
    id: '12c', gate: 'Portability (deploy) — positive check',
    check: "grep -cE '\\\\$PSScriptRoot\\|\\\\$env:USERPROFILE|GetFolderPath' deploy-sync.ps1",
    expected: '>= 3 hits',
    status: 'needs_platform_adjustment', flagged: true,
    flaggedReason: 'grep -cE is POSIX-only.',
    powershellEquivalent: '(Select-String -Path "deploy-sync.ps1" -Pattern "\\$PSScriptRoot|\\$env:USERPROFILE|GetFolderPath").Count',
  },
  {
    id: '13', gate: 'Build tag',
    check: "grep -n '1111.003-v28.0-adr019' src/V12_002.Constants.cs",
    expected: 'one hit on line 12',
    status: 'needs_platform_adjustment', flagged: true,
    flaggedReason: 'grep -n is POSIX-only.',
    powershellEquivalent: 'Select-String -Path "src/V12_002.Constants.cs" -Pattern "1111.003-v28.0-adr019" | Select-Object LineNumber, Line',
  },
  {
    id: '14', gate: 'Deploy sync round-trip',
    check: 'pwsh -File ./deploy-sync.ps1 then F5 in NT',
    expected: 'ASCII gate PASS, banner shows new BuildTag',
    status: 'runnable', flagged: false,
    flaggedReason: undefined,
    powershellEquivalent: undefined,
  },
];

export const checkAsciiStatus = {
  exists: 'Not verified by document',
  evidence: 'Section B.2 states byte_purge.py "is not present anywhere in the repo (verified via filesystem search **/byte_purge.py → 0 matches)". For check_ascii.py, the document says it is "the repo-canonical ASCII-purity tool per CLAUDE.md section \'CRITICAL: ASCII-Only in All C# String Literals\'" — but no filesystem verification of check_ascii.py existence is cited. The document references it but does not confirm it exists at the repo root.',
  conclusion: 'Based on document evidence alone: check_ascii.py is REFERENCED as the canonical tool but its existence at the repo root is NOT independently verified in this plan.',
};

export interface SummaryItem {
  category: 'Blocking' | 'Moderate' | 'Advisory';
  item: string;
  section: string;
  detail: string;
}

export const summaryItems: SummaryItem[] = [
  {
    category: 'Blocking',
    item: 'Site #5 — Type 2 without documented fix',
    section: 'Section C.3 Case 1',
    detail: '_followerReplaceSpecs.TryRemove(sigName, out _) is bypassed by early return. The plan adds the guard but does not document how the dictionary entry is cleaned when _isTerminating is true. Needs a documented fix before engineering begins.',
  },
  {
    category: 'Blocking',
    item: 'Site #16 — Type 2 (semaphore contention) without documented fix',
    section: 'Section C.1 / C.4',
    detail: 'Purpose mentions "semaphore contention". If a semaphore release follows the primary call, it would be bypassed. No fix documented.',
  },
  {
    category: 'Blocking',
    item: 'Site #21 — Type 2 (chain from finally) without documented fix',
    section: 'Section C.1 / C.4',
    detail: 'Purpose states "chain from finally" — finally blocks contain cleanup. Early return bypasses it. No fix documented.',
  },
  {
    category: 'Blocking',
    item: 'Sites #6, #7, #8, #9, #22, #24, #27 — Unverifiable classification',
    section: 'Section C.1 / C.4',
    detail: '7 sites lack explicit code blocks and have purpose descriptions that suggest possible state modification after primary call. Cannot classify as Type 1 or Type 2 without source. These must be verified before engineering.',
  },
  {
    category: 'Moderate',
    item: 'deploy-sync.ps1 line 89 — Join-Path in comment not evaluated',
    section: 'Section D.4.b Sub-block 2',
    detail: 'The NEW comment contains Join-Path syntax inside a # comment. PowerShell comments are never evaluated at runtime. The developer reads literal text, not a computed path. Should be reworded to show the actual computed path or use a non-comment instruction.',
  },
  {
    category: 'Moderate',
    item: 'Linting.csproj:37 — Missing Condition attribute for Linux CI',
    section: 'Section D.4.a',
    detail: 'The plan substitutes $(UserProfile) but does not add Condition=" \'$(OS)\' == \'Windows_NT\' ". On Linux CI, the NinjaTrader DLL path will not resolve, potentially causing build warnings or errors.',
  },
  {
    category: 'Moderate',
    item: 'All 17 verification steps use POSIX commands (grep, test -f)',
    section: 'Section F',
    detail: '14 of 17 steps use grep or test -f which are not natively available on Windows. The plan does not provide PowerShell equivalents. Engineering on Windows machines will need to install Git Bash, WSL, or translate all commands.',
  },
  {
    category: 'Advisory',
    item: 'check_ascii.py existence not verified',
    section: 'Section B.2 / F Step 1',
    detail: 'The plan references check_ascii.py as the canonical tool but only verifies byte_purge.py is absent. A filesystem check for check_ascii.py should be added before Step 1 can pass.',
  },
  {
    category: 'Advisory',
    item: 'Section G Red Team Task 1 asks about reservation-leak at sites #5 and #11',
    section: 'Section G',
    detail: 'The plan itself raises the question of whether sites #5 and #11 have reservation-leak issues but does not resolve them. This is appropriate for a pre-engineering plan, but the answers must be resolved before P4 handoff.',
  },
  {
    category: 'Advisory',
    item: 'Transform B regex in Step 4 may not match all 6 sites',
    section: 'Section F Step 4 / Section C.5',
    detail: 'The grep pattern \'o => \\\\{ if \\\\(\\\\_isTerminating\\\\) return; \\\\[A-Za-z\\\\]+\\\\(\\\\); \\\\}\' assumes all Transform B lambdas use parameter "o". Site #30 (Case 3) uses "o" but the REAPER.Audit.cs sister sites use both "o" and "e" (Section C.3 notes: "The lambda parameter name (o vs e) is preserved verbatim"). The regex should account for both.',
  },
  {
    category: 'Advisory',
    item: 'Section F has 14 listed steps, not 17 as stated in task',
    section: 'Section F',
    detail: 'The document lists steps 1, 2, 3, 3b, 4, 5, 6, 7, 8, 9, 10, 11, 12, 12b, 12c, 13, 14 — that is 17 check entries total (counting 3b, 12b, 12c as separate).',
  },
];

export const readinessState = {
  status: 'NEEDS A PLAN UPDATE',
  blockingItems: [
    'Site #5 (Type 2): _followerReplaceSpecs dictionary cleanup bypassed — no fix documented (Section C.3)',
    'Site #16 (Type 2): semaphore contention — potential semaphore leak — no fix documented (Section C.1)',
    'Site #21 (Type 2): "chain from finally" — cleanup bypassed — no fix documented (Section C.1)',
    '7 unverifiable sites (#6, #7, #8, #9, #22, #24, #27): classification cannot be determined from document alone',
  ],
  moderateItems: [
    'deploy-sync.ps1 line 89: Join-Path in comment not evaluated at runtime (Section D.4.b)',
    'Linting.csproj:37: Missing Condition attribute for Linux CI (Section D.4.a)',
    '14 of 17 verification steps use POSIX-only commands (Section F)',
  ],
  advisoryItems: [
    'check_ascii.py existence at repo root not independently verified (Section B.2)',
    'Transform B regex in Step 4 may not match sites using "e" parameter (Section C.3 / F Step 4)',
    'Section G Red Team audit is the required next step — no engineering until 100% consensus (Section G)',
  ],
};
