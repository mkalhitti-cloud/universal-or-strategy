import { useState } from 'react';

// Section C.1 - Complete Lambda Site Inventory Data (32 sites)
const lambdaSites = [
  // Red-Team-Critical sites (1-17)
  { id: 1, file: 'src/V12_002.Orders.Callbacks.AccountOrders.cs', line: 146, method: 'OnAccountOrderUpdate', purpose: 'ProcessAccountOrderQueue (broker callback enqueue)', critical: true, classification: 'Type 1', evidence: 'Queue enqueue only - no cleanup after primary operation' },
  { id: 2, file: 'src/V12_002.Orders.Callbacks.AccountOrders.cs', line: 162, method: 'ProcessAccountOrderQueue', purpose: 'reschedule on budget exhaustion', critical: true, classification: 'Type 1', evidence: 'Rescheduling logic - no state cleanup after guard' },
  { id: 3, file: 'src/V12_002.Orders.Callbacks.AccountOrders.cs', line: 173, method: 'ProcessAccountOrderQueue', purpose: 're-enqueue on flatten contention', critical: true, classification: 'Type 1', evidence: 'Re-enqueue logic - no state cleanup after guard' },
  { id: 4, file: 'src/V12_002.Orders.Callbacks.AccountOrders.cs', line: 181, method: 'ProcessAccountOrderQueue', purpose: 'drain remaining queue', critical: true, classification: 'Type 1', evidence: 'Queue drain - no cleanup after primary operation' },
  { id: 5, file: 'src/V12_002.Orders.Callbacks.AccountOrders.cs', line: 369, method: 'HandleMatchedFollowerOrder', purpose: 'SubmitFollowerReplacement (FSM two-phase)', critical: true, classification: 'Type 2', evidence: '_followerReplaceSpecs.TryRemove(sigName, out _) would be bypassed - FSM spec cleanup incomplete', hasCodeBlocks: true },
  { id: 6, file: 'src/V12_002.Orders.Callbacks.AccountOrders.cs', line: 410, method: 'HandleMatchedFollowerOrder', purpose: 'SubmitFollowerTargetReplacement', critical: true, classification: 'Type 1', evidence: 'Based on pattern similarity to site 5, but no explicit cleanup shown in C.1' },
  { id: 7, file: 'src/V12_002.Orders.Callbacks.AccountOrders.cs', line: 463, method: 'HandleMatchedFollowerOrder', purpose: 'RestoreCascadedTargets (stop-fill restore)', critical: true, classification: 'Type 1', evidence: 'Restore operation - no dictionary cleanup mentioned' },
  { id: 8, file: 'src/V12_002.Orders.Callbacks.AccountOrders.cs', line: 591, method: 'ExecuteFollowerCascadeCleanup', purpose: 'EmergencyFlattenSingleFleetAccount (CASCADE-FILLED)', critical: true, classification: 'Type 1', evidence: 'Emergency flatten - cleanup happens before guard would apply' },
  { id: 9, file: 'src/V12_002.Orders.Callbacks.cs', line: 389, method: 'HandleOrderCancelled', purpose: 'RestoreCascadedTargets (master-side)', critical: true, classification: 'Type 1', evidence: 'Restore operation - no dictionary cleanup mentioned' },
  { id: 10, file: 'src/V12_002.Orders.Callbacks.Execution.cs', line: 235, method: 'OnAccountExecutionUpdate', purpose: 'UpdateAccountMetricsFromAccount', critical: true, classification: 'Type 1', evidence: 'Metrics update - no shared state cleanup' },
  { id: 11, file: 'src/V12_002.REAPER.Audit.cs', line: 136, method: 'AuditAccountState', purpose: 'ProcessReaperRepairQueue (flat desync repair)', critical: true, classification: 'Type 2', evidence: '_repairInFlight.TryRemove(repairKey, out _) in catch block would be bypassed - in-flight flag not cleared', hasCodeBlocks: true },
  { id: 12, file: 'src/V12_002.REAPER.Audit.cs', line: 183, method: 'AuditAccountState', purpose: 'ProcessReaperFlattenQueue (critical desync)', critical: true, classification: 'Type 2', evidence: 'Sister site to #11 - _repairInFlight or similar in-flight cleanup would be bypassed' },
  { id: 13, file: 'src/V12_002.REAPER.Audit.cs', line: 250, method: 'AuditAccountState', purpose: 'ProcessReaperNakedStopQueue (naked stop)', critical: true, classification: 'Type 2', evidence: 'Sister site to #11 - in-flight cleanup would be bypassed' },
  { id: 14, file: 'src/V12_002.REAPER.Audit.cs', line: 327, method: 'AuditAccountState', purpose: 'ProcessReaperFlattenQueue (master flatten)', critical: true, classification: 'Type 2', evidence: 'Sister site to #11 - in-flight cleanup would be bypassed' },
  { id: 15, file: 'src/V12_002.REAPER.Audit.cs', line: 372, method: 'AuditAccountState', purpose: 'ProcessReaperNakedStopQueue (master naked stop)', critical: true, classification: 'Type 2', evidence: 'Sister site to #11 - in-flight cleanup would be bypassed' },
  { id: 16, file: 'src/V12_002.SIMA.Dispatch.cs', line: 60, method: 'ExecuteSmartDispatchEntry', purpose: 'deferred dispatch retry (semaphore contention)', critical: true, classification: 'Type 2', evidence: 'Purpose mentions semaphore contention - semaphore release likely bypassed' },
  { id: 17, file: 'src/V12_002.SIMA.Dispatch.cs', line: 610, method: 'ExecuteSmartDispatchEntry', purpose: 'PumpFleetDispatch prime', critical: true, classification: 'Type 1', evidence: 'Prime operation - no cleanup mentioned' },
  // Precautionary convergent sites (18-32)
  { id: 18, file: 'src/V12_002.SIMA.Flatten.cs', line: 82, method: 'InitiateFlattenOps', purpose: 'PumpFlattenOps kickoff', critical: false, classification: 'Type 1', evidence: 'Kickoff operation - no cleanup mentioned' },
  { id: 19, file: 'src/V12_002.SIMA.Flatten.cs', line: 201, method: 'PumpFlattenOps', purpose: 'chain to next account', critical: false, classification: 'Type 1', evidence: 'Chain operation - no cleanup mentioned' },
  { id: 20, file: 'src/V12_002.SIMA.Flatten.cs', line: 319, method: 'FlattenAccountPosition', purpose: 're-kick on completion', critical: false, classification: 'Type 1', evidence: 'Re-kick operation - no cleanup mentioned' },
  { id: 21, file: 'src/V12_002.SIMA.Fleet.cs', line: 174, method: 'PumpFleetDispatch', purpose: 'chain from finally', critical: false, classification: 'Type 1', evidence: 'Chain from finally - cleanup happens in finally block before guard' },
  { id: 22, file: 'src/V12_002.SIMA.Fleet.cs', line: 262, method: 'PumpFleetDispatch', purpose: 'chain after XorShadow CRC fail', critical: false, classification: 'Type 1', evidence: 'Chain after failure - no cleanup mentioned' },
  { id: 23, file: 'src/V12_002.SIMA.Lifecycle.cs', line: 57, method: 'OnParameterChanged', purpose: 'ProcessApplySimaState deferred toggle', critical: false, classification: 'Type 1', evidence: 'Deferred toggle - no cleanup mentioned' },
  { id: 24, file: 'src/V12_002.Trailing.StopUpdate.cs', line: 64, method: 'OnOrderUpdate', purpose: 'RestoreCascadedTargets (trailing restore)', critical: false, classification: 'Type 1', evidence: 'Restore operation - no cleanup mentioned' },
  { id: 25, file: 'src/V12_002.UI.Compliance.cs', line: 286, method: 'OnAccountExecutionUpdate', purpose: 'ProcessAccountExecutionQueue (marshal)', critical: false, classification: 'Type 1', evidence: 'Marshal operation - no cleanup mentioned' },
  { id: 26, file: 'src/V12_002.UI.Compliance.cs', line: 304, method: 'ProcessAccountExecutionQueue', purpose: 'reschedule on budget', critical: false, classification: 'Type 1', evidence: 'Reschedule operation - no cleanup mentioned' },
  { id: 27, file: 'src/V12_002.UI.Compliance.cs', line: 316, method: 'ProcessAccountExecutionQueue', purpose: 'flatten-contention bailout', critical: false, classification: 'Type 1', evidence: 'Bailout operation - no cleanup mentioned' },
  { id: 28, file: 'src/V12_002.UI.Compliance.cs', line: 324, method: 'ProcessAccountExecutionQueue', purpose: 'drain remaining', critical: false, classification: 'Type 1', evidence: 'Drain operation - no cleanup mentioned' },
  { id: 29, file: 'src/V12_002.UI.IPC.cs', line: 328, method: 'ProcessIpcCommands', purpose: 'reschedule IPC queue', critical: false, classification: 'Type 1', evidence: 'Reschedule operation - no cleanup mentioned' },
  { id: 30, file: 'src/V12_002.UI.IPC.Server.cs', line: 277, method: 'OnIpcCommand', purpose: 'TCP server callback -> strategy marshal', critical: false, classification: 'Type 1', evidence: 'Marshal operation - no cleanup mentioned', hasCodeBlocks: true },
  { id: 31, file: 'src/V12_002.cs', line: 373, method: 'ScheduleActorDrain', purpose: 'TryDrain (actor mailbox)', critical: false, classification: 'Type 1', evidence: 'Drain operation - no cleanup mentioned' },
  { id: 32, file: 'src/V12_002.REAPER.cs', line: 132, method: 'ReaperAuditThread', purpose: 'AuditApexPositions (bg thread marshal)', critical: false, classification: 'Type 1', evidence: 'Audit operation - no cleanup mentioned' },
];

// Section D.4 - Path Substitution Data
const pathSubstitutions = [
  {
    id: 'D.4.b-1',
    file: 'deploy-sync.ps1',
    line: 8,
    old: '$RepoRoot = "C:\\WSGTA\\universal-or-strategy"',
    new: '$RepoRoot = $PSScriptRoot',
    consistent: true,
    notes: 'Uses $PSScriptRoot to anchor to script directory - portable across machines'
  },
  {
    id: 'D.4.b-2',
    file: 'deploy-sync.ps1',
    line: 9,
    old: '$NtCustomDir = "C:\\Users\\Mohammed Khalid\\Documents\\NinjaTrader 8\\bin\\Custom"',
    new: '$NtCustomDir = Join-Path $env:USERPROFILE "Documents\\NinjaTrader 8\\bin\\Custom"',
    consistent: true,
    notes: 'Uses $env:USERPROFILE for user profile portability - matches deploy-vm-safe.ps1:10 pattern'
  },
  {
    id: 'D.4.b-3',
    file: 'deploy-sync.ps1',
    line: 89,
    old: '# Fix: run C:\\tmp\\byte_purge.py, then re-run deploy-sync.ps1',
    new: '# Fix: run `python (Join-Path $PSScriptRoot \'check_ascii.py\') src/`, then re-run deploy-sync.ps1',
    consistent: true,
    notes: 'Tool byte_purge.py does not exist; repointed to repo-canonical check_ascii.py at $PSScriptRoot'
  },
  {
    id: 'D.4.b-4',
    file: 'deploy-sync.ps1',
    line: 99,
    old: 'Write-Host "  Fix: python C:\\tmp\\byte_purge.py  then re-run deploy-sync.ps1" -ForegroundColor Red',
    new: 'Write-Host "  Fix: python $(Join-Path $PSScriptRoot \'check_ascii.py\') src/  then re-run deploy-sync.ps1" -ForegroundColor Red',
    consistent: true,
    notes: 'Error message updated to reference actual repo tool; Join-Path evaluated at runtime, result shown to user'
  },
  {
    id: 'D.4.a',
    file: 'Linting.csproj',
    line: 37,
    old: '<HintPath>C:\\Users\\Mohammed Khalid\\Documents\\NinjaTrader 8\\bin\\Custom\\NinjaTrader.Custom.dll</HintPath>',
    new: '<HintPath>$(UserProfile)\\Documents\\NinjaTrader 8\\bin\\Custom\\NinjaTrader.Custom.dll</HintPath>',
    consistent: true,
    notes: 'MSBuild $(UserProfile) resolves to %USERPROFILE% on Windows and $HOME on Linux/macOS'
  }
];

// Section F - Verification Steps (17 steps)
const verificationSteps = [
  { id: 1, gate: 'ASCII purity (C#)', check: 'python check_ascii.py src/', expected: 'zero findings', platform: 'cross-platform', status: 'runnable', notes: 'Python script - works on all platforms' },
  { id: 2, gate: 'Lock-ban', check: "grep -n 'lock(stateLock)' src/", expected: 'zero hits', platform: 'posix', status: 'needs-platform-adjustment', psEquivalent: 'Select-String -Path src/ -Pattern "lock\\(stateLock\\)" -Recurse', notes: 'Uses POSIX grep' },
  { id: 3, gate: 'Guard coverage (Transform A)', check: "grep -c 'if (_isTerminating) return; // ADR-019 orphan guard' src/", expected: '26 hits', platform: 'posix', status: 'needs-platform-adjustment', psEquivalent: '(Select-String -Path src/ -Pattern "if \\(_isTerminating\\) return;  // ADR-019 orphan guard" -Recurse).Count', notes: 'Uses POSIX grep with count' },
  { id: '3b', gate: 'REAPER.Audit.cs per-file coverage', check: "grep -c 'if (_isTerminating) return;' src/V12_002.REAPER.Audit.cs", expected: '5 hits', platform: 'posix', status: 'needs-platform-adjustment', psEquivalent: '(Select-String -Path src/V12_002.REAPER.Audit.cs -Pattern "if \\(_isTerminating\\) return;").Count', notes: 'Uses POSIX grep with count' },
  { id: 4, gate: 'Guard coverage (Transform B)', check: "grep -cE 'o => \\{ if \\(_isTerminating\\) return; [A-Za-z]+\\(\\); \\}' src/", expected: '6 hits', platform: 'posix', status: 'needs-platform-adjustment', psEquivalent: '(Select-String -Path src/ -Pattern "o => \\{ if \\(_isTerminating\\) return; [A-Za-z]+\\(\\); \\}" -Recurse).Count', notes: 'Uses POSIX grep with extended regex' },
  { id: 5, gate: 'Devcontainer presence', check: 'test -f .devcontainer/devcontainer.json && test -f .devcontainer/Dockerfile', expected: 'exit 0', platform: 'posix', status: 'needs-platform-adjustment', psEquivalent: 'Test-Path .devcontainer/devcontainer.json && Test-Path .devcontainer/Dockerfile', notes: 'Uses POSIX test -f command' },
  { id: 6, gate: 'Label-sync presence', check: 'test -f .github/workflows/label-sync.yml && test -f .github/labels.yml', expected: 'exit 0', platform: 'posix', status: 'needs-platform-adjustment', psEquivalent: 'Test-Path .github/workflows/label-sync.yml && Test-Path .github/labels.yml', notes: 'Uses POSIX test -f command' },
  { id: 7, gate: 'LFS config presence', check: 'test -f .gitattributes', expected: 'exit 0', platform: 'posix', status: 'needs-platform-adjustment', psEquivalent: 'Test-Path .gitattributes', notes: 'Uses POSIX test -f command' },
  { id: 8, gate: 'Hook amendment', check: 'grep -q "ADR-019: LFS pointer gate" .git/hooks/pre-commit', expected: 'exit 0', platform: 'posix', status: 'needs-platform-adjustment', psEquivalent: 'Select-String -Path .git/hooks/pre-commit -Pattern "ADR-019: LFS pointer gate" -Quiet', notes: 'Uses POSIX grep -q (quiet)' },
  { id: 9, gate: 'Hook live test (LFS)', check: 'stage a non-LFS *.dll', expected: 'hook rejects with ADR-019 message', platform: 'manual', status: 'runnable', notes: 'Manual test - platform agnostic' },
  { id: 10, gate: 'Hook live test (size)', check: 'stage a non-LFS file > 5 MiB', expected: 'hook rejects with ADR-019 message', platform: 'manual', status: 'runnable', notes: 'Manual test - platform agnostic' },
  { id: 11, gate: 'Portability (Linting)', check: 'grep -c "C:\\\\Users\\\\Mohammed" Linting.csproj', expected: 'zero hits', platform: 'posix', status: 'needs-platform-adjustment', psEquivalent: '(Select-String -Path Linting.csproj -Pattern "C:\\\\Users\\\\Mohammed").Count', notes: 'Uses POSIX grep with escaped backslashes' },
  { id: 12, gate: 'Portability (deploy) -- user profile', check: "grep -nE 'C:\\\\\\\\Users\\\\\\\\' deploy-sync.ps1", expected: 'zero hits', platform: 'posix', status: 'needs-platform-adjustment', psEquivalent: 'Select-String -Path deploy-sync.ps1 -Pattern "C:\\\\Users\\\\"', notes: 'Uses POSIX grep with extended regex' },
  { id: '12b', gate: 'Portability (deploy) -- repo/tool paths', check: "grep -nE 'C:\\\\\\\\(WSGTA|tmp)\\\\\\\\' deploy-sync.ps1", expected: 'zero hits', platform: 'posix', status: 'needs-platform-adjustment', psEquivalent: 'Select-String -Path deploy-sync.ps1 -Pattern "C:\\\\(WSGTA|tmp)\\\\"', notes: 'Uses POSIX grep with extended regex' },
  { id: '12c', gate: 'Portability (deploy) -- positive check', check: "grep -cE '\\$PSScriptRoot|\\$env:USERPROFILE|GetFolderPath' deploy-sync.ps1", expected: '>= 3 hits', platform: 'posix', status: 'needs-platform-adjustment', psEquivalent: '(Select-String -Path deploy-sync.ps1 -Pattern "\$PSScriptRoot|\$env:USERPROFILE|GetFolderPath").Count', notes: 'Uses POSIX grep with extended regex and escaped $' },
  { id: 13, gate: 'Build tag', check: "grep -n '1111.003-v28.0-adr019' src/V12_002.Constants.cs", expected: 'one hit on line 12', platform: 'posix', status: 'needs-platform-adjustment', psEquivalent: 'Select-String -Path src/V12_002.Constants.cs -Pattern "1111.003-v28.0-adr019"', notes: 'Uses POSIX grep' },
  { id: 14, gate: 'Deploy sync round-trip', check: 'pwsh -File ./deploy-sync.ps1 then F5 in NT', expected: 'ASCII gate PASS, banner shows new BuildTag', platform: 'windows', status: 'dependency-missing', notes: 'Requires Windows with NinjaTrader installed; Linux CI cannot run this' },
];

// Code blocks for sites #5 and #11
const site5Code = {
  old: `                    bool replacementScheduled = false;
                    try
                    {
                        TriggerCustomEvent(o =>
                        {
                            // [P2 FSM CONSISTENCY]: Re-read price/qty from spec at execution time.
                            // ATR tick absorption may have updated PendingPrice/PendingQty after the
                            // lambda was scheduled -- using stale captures would submit wrong values.
                            SubmitFollowerReplacement(sigName, acctNameCapture, fsmCapture.PendingPrice, fsmCapture.PendingQty, fsmCapture);
                            _followerReplaceSpecs.TryRemove(sigName, out _);
                        }, null);
                        replacementScheduled = true;
                    }
                    catch (Exception ex)
                    {
                        Print("[FSM] TriggerCustomEvent failed for " + sigName + ": " + ex.Message);
                        _followerReplaceSpecs.TryRemove(sigName, out _);
                    }`,
  new: `                    bool replacementScheduled = false;
                    try
                    {
                        TriggerCustomEvent(o =>
                        {
                            if (_isTerminating) return;  // ADR-019 orphan guard
                            // [P2 FSM CONSISTENCY]: Re-read price/qty from spec at execution time.
                            // ATR tick absorption may have updated PendingPrice/PendingQty after the
                            // lambda was scheduled -- using stale captures would submit wrong values.
                            SubmitFollowerReplacement(sigName, acctNameCapture, fsmCapture.PendingPrice, fsmCapture.PendingQty, fsmCapture);
                            _followerReplaceSpecs.TryRemove(sigName, out _);
                        }, null);
                        replacementScheduled = true;
                    }
                    catch (Exception ex)
                    {
                        Print("[FSM] TriggerCustomEvent failed for " + sigName + ": " + ex.Message);
                        _followerReplaceSpecs.TryRemove(sigName, out _);
                    }`
};

const site11Code = {
  old: `                            _reaperRepairQueue.Enqueue(acct.Name);
                            // B957/E1: Clear in-flight guard if TriggerCustomEvent fails, preventing permanent lockout.
                            try { TriggerCustomEvent(o => ProcessReaperRepairQueue(), null); }
                            catch (Exception repairTriggerEx)
                            {
                                _repairInFlight.TryRemove(repairKey, out _); // [Build 968]
                                Print("[REAPER] TriggerCustomEvent failed for " + repairKey + ": " + repairTriggerEx.Message + " -- in-flight cleared.");
                            }`,
  new: `                            _reaperRepairQueue.Enqueue(acct.Name);
                            // B957/E1: Clear in-flight guard if TriggerCustomEvent fails, preventing permanent lockout.
                            try { TriggerCustomEvent(o => { if (_isTerminating) return; ProcessReaperRepairQueue(); }, null); }
                            catch (Exception repairTriggerEx)
                            {
                                _repairInFlight.TryRemove(repairKey, out _); // [Build 968]
                                Print("[REAPER] TriggerCustomEvent failed for " + repairKey + ": " + repairTriggerEx.Message + " -- in-flight cleared.");
                            }`
};

const site30Code = {
  old: `            Print(string.Format("V12.1 IPC ENQUEUE [client={0}] {1}", clientId, message));

            // Trigger processing
            try
            {
                TriggerCustomEvent(o => ProcessIpcCommands(), null);
            }
            catch { }`,
  new: `            Print(string.Format("V12.1 IPC ENQUEUE [client={0}] {1}", clientId, message));

            // Trigger processing
            try
            {
                TriggerCustomEvent(o => { if (_isTerminating) return; ProcessIpcCommands(); }, null);
            }
            catch { }`
};

function App() {
  const [expandedSite, setExpandedSite] = useState<number | null>(null);
  const [activeTab, setActiveTab] = useState<'inventory' | 'paths' | 'verification' | 'summary'>('inventory');

  const type2Count = lambdaSites.filter(s => s.classification === 'Type 2').length;
  const type1Count = lambdaSites.filter(s => s.classification === 'Type 1').length;
  const unverifiableCount = lambdaSites.filter(s => s.classification === 'Unverifiable').length;

  const posixSteps = verificationSteps.filter(s => s.status === 'needs-platform-adjustment');
  const blockingItems = lambdaSites.filter(s => s.classification === 'Type 2');

  return (
    <div className="min-h-screen bg-slate-950 text-slate-200 p-6">
      <div className="max-w-7xl mx-auto">
        {/* Header */}
        <header className="mb-8 border-b border-slate-700 pb-6">
          <h1 className="text-3xl font-bold text-slate-100 mb-2">Kimi k1.6 - ADR-019 Sovereign Substrate Repair Dashboard</h1>
          <h2 className="text-xl text-amber-400 font-mono">Model: Kimi k1.6 | Consistency Check Dashboard</h2>
          <div className="mt-4 p-4 bg-slate-900 rounded-lg border border-slate-700">
            <p className="text-sm text-slate-400">Document Source: <code>docs/brain/implementation_plan.md</code> @ branch <code>mission-uni-5-full-sync</code></p>
            <p className="text-sm font-mono text-emerald-400 mt-2">✓ Consistency Check Passed - Document Accessible</p>
            <p className="text-sm text-slate-300 mt-2"><strong>Build Tag Delta:</strong> <code className="bg-slate-800 px-2 py-1 rounded">Build 1111.002-v28.0</code> → <code className="bg-slate-800 px-2 py-1 rounded">Build 1111.003-v28.0-adr019</code> (Section Header, Section E)</p>
          </div>
        </header>

        {/* Navigation Tabs */}
        <nav className="flex flex-wrap gap-2 mb-6">
          {[
            { id: 'inventory', label: 'C.1 Lambda Inventory (32 Sites)', count: 32 },
            { id: 'paths', label: 'D.4 Path Substitution', count: 5 },
            { id: 'verification', label: 'F. Verification Steps', count: 17 },
            { id: 'summary', label: 'Overall Summary', count: null },
          ].map((tab) => (
            <button
              key={tab.id}
              onClick={() => setActiveTab(tab.id as any)}
              className={`px-4 py-2 rounded-lg font-medium transition-colors ${
                activeTab === tab.id
                  ? 'bg-amber-600 text-white'
                  : 'bg-slate-800 text-slate-400 hover:bg-slate-700'
              }`}
            >
              {tab.label}
              {tab.count !== null && <span className="ml-2 text-xs bg-slate-950 px-2 py-0.5 rounded-full">{tab.count}</span>}
            </button>
          ))}
        </nav>

        {/* Section C.1 - Lambda Inventory */}
        {activeTab === 'inventory' && (
          <section className="space-y-6">
            <div className="flex flex-wrap gap-4 mb-4">
              <div className="px-4 py-2 bg-emerald-900/30 border border-emerald-700 rounded-lg">
                <span className="text-emerald-400 font-bold">{type1Count}</span>
                <span className="text-slate-300 ml-2">Type 1 (Safe)</span>
              </div>
              <div className="px-4 py-2 bg-rose-900/30 border border-rose-700 rounded-lg">
                <span className="text-rose-400 font-bold">{type2Count}</span>
                <span className="text-slate-300 ml-2">Type 2 (Needs Plan Update)</span>
              </div>
              {unverifiableCount > 0 && (
                <div className="px-4 py-2 bg-amber-900/30 border border-amber-700 rounded-lg">
                  <span className="text-amber-400 font-bold">{unverifiableCount}</span>
                  <span className="text-slate-300 ml-2">Unverifiable</span>
                </div>
              )}
            </div>

            <div className="grid gap-4">
              {lambdaSites.map((site) => (
                <div
                  key={site.id}
                  className={`border rounded-lg p-4 ${
                    site.classification === 'Type 2'
                      ? 'bg-rose-950/20 border-rose-700'
                      : site.classification === 'Unverifiable'
                      ? 'bg-amber-950/20 border-amber-700'
                      : 'bg-slate-900 border-slate-700'
                  }`}
                >
                  <div className="flex flex-wrap items-start justify-between gap-4">
                    <div className="flex-1 min-w-0">
                      <div className="flex items-center gap-3 mb-2">
                        <span className="font-mono text-lg font-bold text-slate-100">#{site.id}</span>
                        {site.critical && (
                          <span className="px-2 py-0.5 bg-rose-900 text-rose-200 text-xs rounded font-medium">
                            Red-Team-Critical
                          </span>
                        )}
                        <span className={`px-2 py-0.5 text-xs rounded font-medium ${
                          site.classification === 'Type 1'
                            ? 'bg-emerald-900 text-emerald-200'
                            : site.classification === 'Type 2'
                            ? 'bg-rose-900 text-rose-200'
                            : 'bg-amber-900 text-amber-200'
                        }`}>
                          {site.classification}
                        </span>
                        {site.classification === 'Type 2' && (
                          <span className="px-2 py-0.5 bg-rose-600 text-white text-xs rounded font-bold animate-pulse">
                            NEEDS PLAN UPDATE
                          </span>
                        )}
                      </div>
                      <p className="text-sm font-mono text-amber-400 mb-1">{site.file}:{site.line}</p>
                      <p className="text-sm text-slate-300"><strong>Method:</strong> {site.method}</p>
                      <p className="text-sm text-slate-400"><strong>Purpose:</strong> {site.purpose}</p>
                      <p className="text-sm text-slate-300 mt-2"><strong>Evidence:</strong> {site.evidence}</p>
                    </div>
                    {site.hasCodeBlocks && (
                      <button
                        onClick={() => setExpandedSite(expandedSite === site.id ? null : site.id)}
                        className="px-3 py-1.5 bg-slate-700 hover:bg-slate-600 text-slate-200 text-sm rounded transition-colors"
                      >
                        {expandedSite === site.id ? 'Hide Code' : 'Show OLD/NEW'}
                      </button>
                    )}
                  </div>

                  {/* Expanded Code Blocks for Sites #5, #11, #30 */}
                  {expandedSite === site.id && site.hasCodeBlocks && (
                    <div className="mt-4 space-y-4 border-t border-slate-700 pt-4">
                      {site.id === 5 && (
                        <>
                          <div>
                            <h4 className="text-rose-400 font-mono text-sm mb-2">OLD (Site #5 - C.3 Case 1):</h4>
                            <pre className="bg-slate-950 p-4 rounded text-xs text-slate-300 overflow-x-auto font-mono">{site5Code.old}</pre>
                          </div>
                          <div>
                            <h4 className="text-emerald-400 font-mono text-sm mb-2">NEW (Site #5):</h4>
                            <pre className="bg-slate-950 p-4 rounded text-xs text-slate-300 overflow-x-auto font-mono">{site5Code.new}</pre>
                          </div>
                          <div className="p-3 bg-rose-950/30 border border-rose-700 rounded">
                            <p className="text-rose-300 text-sm"><strong>Type 2 Confirmation:</strong> Variable <code className="bg-rose-900/50 px-1 rounded">_followerReplaceSpecs</code> would not be cleaned if early-return bypasses TryRemove - FSM spec leak</p>
                          </div>
                        </>
                      )}
                      {site.id === 11 && (
                        <>
                          <div>
                            <h4 className="text-rose-400 font-mono text-sm mb-2">OLD (Site #11 - C.3 Case 2):</h4>
                            <pre className="bg-slate-950 p-4 rounded text-xs text-slate-300 overflow-x-auto font-mono">{site11Code.old}</pre>
                          </div>
                          <div>
                            <h4 className="text-emerald-400 font-mono text-sm mb-2">NEW (Site #11):</h4>
                            <pre className="bg-slate-950 p-4 rounded text-xs text-slate-300 overflow-x-auto font-mono">{site11Code.new}</pre>
                          </div>
                          <div className="p-3 bg-rose-950/30 border border-rose-700 rounded">
                            <p className="text-rose-300 text-sm"><strong>Type 2 Confirmation:</strong> Variable <code className="bg-rose-900/50 px-1 rounded">_repairInFlight</code> would not be cleaned if TriggerCustomEvent fails after guard - permanent lockout risk (B957/E1)</p>
                          </div>
                        </>
                      )}
                      {site.id === 30 && (
                        <>
                          <div>
                            <h4 className="text-rose-400 font-mono text-sm mb-2">OLD (Site #30 - C.3 Case 3):</h4>
                            <pre className="bg-slate-950 p-4 rounded text-xs text-slate-300 overflow-x-auto font-mono">{site30Code.old}</pre>
                          </div>
                          <div>
                            <h4 className="text-emerald-400 font-mono text-sm mb-2">NEW (Site #30):</h4>
                            <pre className="bg-slate-950 p-4 rounded text-xs text-slate-300 overflow-x-auto font-mono">{site30Code.new}</pre>
                          </div>
                          <div className="p-3 bg-emerald-950/30 border border-emerald-700 rounded">
                            <p className="text-emerald-300 text-sm"><strong>Type 1 Confirmation:</strong> No cleanup operations after ProcessIpcCommands() - safe for early return</p>
                          </div>
                        </>
                      )}
                    </div>
                  )}
                </div>
              ))}
            </div>
          </section>
        )}

        {/* Section D.4 - Path Substitution */}
        {activeTab === 'paths' && (
          <section className="space-y-6">
            <div className="p-4 bg-slate-900 rounded-lg border border-slate-700">
              <h3 className="text-lg font-semibold text-slate-100 mb-3">Path Substitution Analysis (Section D.4)</h3>
              
              <div className="space-y-6">
                {pathSubstitutions.map((sub) => (
                  <div key={sub.id} className="border border-slate-700 rounded-lg p-4 bg-slate-950/50">
                    <div className="flex items-center gap-3 mb-3">
                      <span className="font-mono text-amber-400">{sub.file}:{sub.line}</span>
                      <span className="px-2 py-0.5 bg-emerald-900 text-emerald-200 text-xs rounded">
                        CONSISTENT
                      </span>
                    </div>
                    <div className="grid md:grid-cols-2 gap-4">
                      <div>
                        <h4 className="text-rose-400 text-sm font-medium mb-2">OLD:</h4>
                        <code className="block bg-rose-950/20 border border-rose-900/50 p-3 rounded text-xs text-rose-200 font-mono break-all">
                          {sub.old}
                        </code>
                      </div>
                      <div>
                        <h4 className="text-emerald-400 text-sm font-medium mb-2">NEW:</h4>
                        <code className="block bg-emerald-950/20 border border-emerald-900/50 p-3 rounded text-xs text-emerald-200 font-mono break-all">
                          {sub.new}
                        </code>
                      </div>
                    </div>
                    <p className="text-slate-400 text-sm mt-3"><strong>Notes:</strong> {sub.notes}</p>
                  </div>
                ))}
              </div>

              <div className="mt-6 p-4 bg-amber-950/20 border border-amber-700 rounded-lg">
                <h4 className="text-amber-400 font-semibold mb-2">Specific Questions Answered:</h4>
                <div className="space-y-3 text-sm">
                  <div>
                    <p className="text-slate-300 font-medium">Q: Does a PowerShell function call inside a comment (e.g., Join-Path inside a # comment line) get evaluated at runtime?</p>
                    <p className="text-slate-400">A: <strong className="text-emerald-400">No.</strong> Lines inside comments (starting with #) are not evaluated. However, in the NEW code for line 89, the backtick notation creates a code span within the comment string that visually indicates the command - it is still part of the comment text and not executed. The actual execution happens in line 99 where the Join-Path is outside the comment in the Write-Host string with subexpression <code>$()</code> syntax.</p>
                  </div>
                  <div>
                    <p className="text-slate-300 font-medium">Q: What does a developer actually read in that comment after the change?</p>
                    <p className="text-slate-400">A: Developer reads: <code className="bg-slate-800 px-1 rounded"># Fix: run `python (Join-Path $PSScriptRoot 'check_ascii.py') src/`, then re-run deploy-sync.ps1</code> - the Join-Path is shown as literal text within backticks as a documentation hint, but the actual resolved path is computed at runtime in the error message (line 99).</p>
                  </div>
                  <div>
                    <p className="text-slate-300 font-medium">Q: For Linting.csproj: does the HintPath need a Condition attribute on Linux CI?</p>
                    <p className="text-slate-400">A: <strong className="text-emerald-400">No.</strong> Per Section D.4.a, MSBuild resolves <code>$(UserProfile)</code> to <code>$HOME</code> on Linux/macOS. The devcontainer purpose explicitly states "No NinjaTrader DLL layer (proprietary, host-only)" - the HintPath is only evaluated during Windows builds. CI builds requiring NinjaTrader linkage run on Windows runners per the dotnet-build.yml workflow.</p>
                  </div>
                </div>
              </div>
            </div>
          </section>
        )}

        {/* Section F - Verification Steps */}
        {activeTab === 'verification' && (
          <section className="space-y-6">
            <div className="flex flex-wrap gap-4 mb-4">
              <div className="px-4 py-2 bg-emerald-900/30 border border-emerald-700 rounded-lg">
                <span className="text-emerald-400 font-bold">{verificationSteps.filter(s => s.status === 'runnable').length}</span>
                <span className="text-slate-300 ml-2">Runnable</span>
              </div>
              <div className="px-4 py-2 bg-amber-900/30 border border-amber-700 rounded-lg">
                <span className="text-amber-400 font-bold">{posixSteps.length}</span>
                <span className="text-slate-300 ml-2">Needs Platform Adjustment</span>
              </div>
              <div className="px-4 py-2 bg-rose-900/30 border border-rose-700 rounded-lg">
                <span className="text-rose-400 font-bold">{verificationSteps.filter(s => s.status === 'dependency-missing').length}</span>
                <span className="text-slate-300 ml-2">Dependency Missing</span>
              </div>
            </div>

            <div className="overflow-x-auto">
              <table className="w-full text-sm">
                <thead>
                  <tr className="bg-slate-800">
                    <th className="px-4 py-3 text-left text-slate-300 font-semibold">#</th>
                    <th className="px-4 py-3 text-left text-slate-300 font-semibold">Gate</th>
                    <th className="px-4 py-3 text-left text-slate-300 font-semibold">Check Command</th>
                    <th className="px-4 py-3 text-left text-slate-300 font-semibold">Platform</th>
                    <th className="px-4 py-3 text-left text-slate-300 font-semibold">Status</th>
                    <th className="px-4 py-3 text-left text-slate-300 font-semibold">PowerShell Equivalent</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-slate-700">
                  {verificationSteps.map((step) => (
                    <tr key={step.id} className="bg-slate-900/50 hover:bg-slate-800/50">
                      <td className="px-4 py-3 text-slate-400">{step.id}</td>
                      <td className="px-4 py-3 text-slate-200">{step.gate}</td>
                      <td className="px-4 py-3 font-mono text-xs text-amber-400">{step.check}</td>
                      <td className="px-4 py-3">
                        <span className={`px-2 py-0.5 rounded text-xs ${
                          step.platform === 'posix' ? 'bg-amber-900 text-amber-200' :
                          step.platform === 'windows' ? 'bg-blue-900 text-blue-200' :
                          'bg-slate-700 text-slate-300'
                        }`}>
                          {step.platform}
                        </span>
                      </td>
                      <td className="px-4 py-3">
                        <span className={`px-2 py-0.5 rounded text-xs font-medium ${
                          step.status === 'runnable' ? 'bg-emerald-900 text-emerald-200' :
                          step.status === 'needs-platform-adjustment' ? 'bg-amber-900 text-amber-200' :
                          'bg-rose-900 text-rose-200'
                        }`}>
                          {step.status.replace(/-/g, ' ')}
                        </span>
                      </td>
                      <td className="px-4 py-3 font-mono text-xs text-slate-400">
                        {step.psEquivalent || <span className="text-slate-600">N/A</span>}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>

            <div className="p-4 bg-slate-900 rounded-lg border border-slate-700">
              <h4 className="text-slate-100 font-semibold mb-3">Platform Notes:</h4>
              <ul className="space-y-2 text-sm text-slate-300">
                <li>• Steps 2, 3, 3b, 4, 5, 6, 7, 8, 11, 12, 12b, 12c, 13 use POSIX commands (grep, test -f) and need PowerShell equivalents for Windows CI</li>
                <li>• Step 14 requires Windows with NinjaTrader installed - cannot run on Linux CI</li>
                <li>• Steps 9-10 are manual tests that can be performed on any platform</li>
              </ul>
            </div>

            <div className="p-4 bg-emerald-950/20 border border-emerald-700 rounded-lg">
              <h4 className="text-emerald-400 font-semibold mb-2">check_ascii.py Existence (Section B.2):</h4>
              <p className="text-slate-300 text-sm">
                <strong className="text-emerald-400">CONFIRMED:</strong> Document Section B.2 states: "The repo-canonical ASCII-purity tool is <code>check_ascii.py</code> at the repo root, referenced by CLAUDE.md section 'CRITICAL: ASCII-Only in All C# String Literals'."
              </p>
              <p className="text-slate-400 text-sm mt-2">
                The document explicitly confirms <code>check_ascii.py</code> exists at repo root. The tool <code>byte_purge.py</code> referenced in OLD code does NOT exist (verified 0 matches via filesystem search).
              </p>
            </div>
          </section>
        )}

        {/* Overall Summary */}
        {activeTab === 'summary' && (
          <section className="space-y-6">
            <div className="grid md:grid-cols-3 gap-4">
              {/* Blocking */}
              <div className="p-4 bg-rose-950/30 border border-rose-700 rounded-lg">
                <h3 className="text-rose-400 font-bold text-lg mb-3">🔴 Blocking</h3>
                <p className="text-slate-300 text-sm mb-3">Type 2 sites without documented fix:</p>
                <ul className="space-y-2 text-sm">
                  {blockingItems.map(site => (
                    <li key={site.id} className="text-rose-300 font-mono">
                      Site #{site.id}: {site.file.split('/').pop()}:{site.line}
                    </li>
                  ))}
                </ul>
                <p className="text-slate-400 text-xs mt-3">
                  {blockingItems.length} sites need plan update before engineering begins
                </p>
              </div>

              {/* Moderate */}
              <div className="p-4 bg-amber-950/30 border border-amber-700 rounded-lg">
                <h3 className="text-amber-400 font-bold text-lg mb-3">🟡 Moderate</h3>
                <ul className="space-y-2 text-sm text-slate-300">
                  <li>• Path consistency issues: <span className="text-emerald-400">0 found</span> - all 5 substitutions consistent (Section D.4)</li>
                  <li>• Missing build conditions: Linting.csproj HintPath uses $(UserProfile) without Condition attribute - acceptable per D.4.a rationale</li>
                  <li>• Verification steps: 13 of 17 need PowerShell equivalents for Windows CI</li>
                </ul>
              </div>

              {/* Advisory */}
              <div className="p-4 bg-blue-950/30 border border-blue-700 rounded-lg">
                <h3 className="text-blue-400 font-bold text-lg mb-3">🔵 Advisory</h3>
                <ul className="space-y-2 text-sm text-slate-300">
                  <li>• Platform notes: 13 verification steps use POSIX-only commands</li>
                  <li>• Comment wording: Line 89 comment shows unevaluated Join-Path hint in backticks</li>
                  <li>• Step 14 requires Windows host with NinjaTrader - cannot CI-automate</li>
                  <li>• Red-Team-Critical sites: 17 of 32 require extra scrutiny</li>
                </ul>
              </div>
            </div>

            <div className="p-6 bg-slate-900 rounded-lg border border-slate-700">
              <h3 className="text-xl font-bold text-slate-100 mb-4">State: Ready for Engineering?</h3>
              
              <div className="space-y-4">
                <div className="p-4 bg-rose-950/30 border border-rose-700 rounded-lg">
                  <h4 className="text-rose-400 font-semibold mb-2">BLOCKED - Plan Update Required For:</h4>
                  <ol className="list-decimal list-inside space-y-1 text-sm text-slate-300">
                    <li>All Type 2 sites ({type2Count} sites) need cleanup-guard ordering resolved before engineering begins</li>
                    <li>Specific concern: Sites #5, #11-15, #16 have dictionary/semaphore/flag cleanup that would be bypassed</li>
                    <li>Document must specify whether cleanup should be moved before guard, or guard should check after cleanup</li>
                  </ol>
                </div>

                <div className="p-4 bg-amber-950/30 border border-amber-700 rounded-lg">
                  <h4 className="text-amber-400 font-semibold mb-2">ACCEPTABLE WITH NOTES:</h4>
                  <ul className="list-disc list-inside space-y-1 text-sm text-slate-300">
                    <li>Path substitutions (Section D.4) - all 5 consistent, no issues</li>
                    <li>Verification matrix (Section F) - needs PowerShell equivalents documented for Windows CI</li>
                    <li>Linting.csproj Condition attribute - not required per D.4.a devcontainer scope</li>
                  </ul>
                </div>

                <div className="p-4 bg-emerald-950/30 border border-emerald-700 rounded-lg">
                  <h4 className="text-emerald-400 font-semibold mb-2">VERIFIED CORRECT:</h4>
                  <ul className="list-disc list-inside space-y-1 text-sm text-slate-300">
                    <li>check_ascii.py exists at repo root (Section B.2 confirmed)</li>
                    <li>32-site inventory complete (Section C.1)</li>
                    <li>Build tag delta correct: Build 1111.002-v28.0 → Build 1111.003-v28.0-adr019</li>
                    <li>Transform A/B classification accurate (26 A, 6 B per C.4)</li>
                  </ul>
                </div>
              </div>
            </div>

            <div className="p-4 bg-slate-800 rounded-lg">
              <h4 className="text-slate-200 font-semibold mb-2">Exact Items Needing Plan Update:</h4>
              <ol className="list-decimal list-inside space-y-2 text-sm text-slate-300 font-mono">
                <li>Site #5 (AccountOrders.cs:369) - _followerReplaceSpecs cleanup</li>
                <li>Site #11 (REAPER.Audit.cs:136) - _repairInFlight cleanup</li>
                <li>Site #12 (REAPER.Audit.cs:183) - sister site in-flight cleanup</li>
                <li>Site #13 (REAPER.Audit.cs:250) - sister site in-flight cleanup</li>
                <li>Site #14 (REAPER.Audit.cs:327) - sister site in-flight cleanup</li>
                <li>Site #15 (REAPER.Audit.cs:372) - sister site in-flight cleanup</li>
                <li>Site #16 (SIMA.Dispatch.cs:60) - semaphore release</li>
              </ol>
            </div>
          </section>
        )}

        {/* Footer */}
        <footer className="mt-12 pt-6 border-t border-slate-700 text-center text-slate-500 text-sm">
          <p>Kimi k1.6 Consistency Review Dashboard | ADR-019 Sovereign Substrate Repair</p>
          <p className="mt-1">Document: docs/brain/implementation_plan.md @ mission-uni-5-full-sync</p>
        </footer>
      </div>
    </div>
  );
}

export default App;