import { useState } from 'react';

type StepStatus = 'runnable' | 'needs-adjustment' | 'dependency-missing';

interface VerificationStep {
  id: string;
  gate: string;
  check: string;
  expected: string;
  status: StepStatus;
  issue?: string;
  psEquivalent?: string;
}

const steps: VerificationStep[] = [
  {
    id: '1',
    gate: 'ASCII purity (C#)',
    check: 'python check_ascii.py src/',
    expected: 'zero findings',
    status: 'runnable',
    issue: 'check_ascii.py is confirmed to exist at the repo root per §B.2: "the repo-canonical ASCII-purity tool is check_ascii.py at the repo root, referenced by CLAUDE.md." Runnable on both Linux and Windows with Python installed.',
  },
  {
    id: '2',
    gate: 'Lock-ban',
    check: "grep -n 'lock(stateLock)' src/",
    expected: 'zero hits',
    status: 'needs-adjustment',
    issue: '⚠ grep is a POSIX/Linux command. On Windows (without WSL or Git Bash), grep is not available in PowerShell or cmd.exe natively.',
    psEquivalent: "Get-ChildItem -Recurse src/ -Filter *.cs | Select-String -Pattern 'lock\\(stateLock\\)'",
  },
  {
    id: '3',
    gate: 'Guard coverage (Transform A)',
    check: "grep -c 'if (_isTerminating) return;  // ADR-019 orphan guard' src/",
    expected: '26 hits',
    status: 'needs-adjustment',
    issue: '⚠ grep is POSIX-only on Windows. Additionally, grep -c across multiple files sums per-file; the plan expects 26 total hits but -c returns per-file counts. Recommend grep -r -c or summing.',
    psEquivalent: "(Get-ChildItem -Recurse src/ -Filter *.cs | Select-String -Pattern 'if \\(_isTerminating\\) return;  // ADR-019 orphan guard').Count",
  },
  {
    id: '3b',
    gate: 'REAPER.Audit.cs per-file coverage',
    check: "grep -c 'if (_isTerminating) return;' src/V12_002.REAPER.Audit.cs",
    expected: '5 hits',
    status: 'needs-adjustment',
    issue: '⚠ grep -c on a single file returns a count — this works on POSIX. On Windows PowerShell, grep is not available.',
    psEquivalent: "(Select-String -Path 'src/V12_002.REAPER.Audit.cs' -Pattern 'if \\(_isTerminating\\) return;').Count",
  },
  {
    id: '4',
    gate: 'Guard coverage (Transform B)',
    check: "grep -cE 'o => \\{ if \\(_isTerminating\\) return; [A-Za-z]+\\(\\); \\}' src/",
    expected: '6 hits',
    status: 'needs-adjustment',
    issue: '⚠ grep -cE (extended regex) is POSIX-only. On Windows, not available natively. Also note this regex only matches parameter name "o" — site #13 and #15 use parameter "e" — the regex [A-Za-z]+\\(\\) covers the method name but the lambda param must also be flexible. The plan notes "lambda parameter name (o vs e) is preserved verbatim" — the grep pattern uses only "o =>" and would miss "e => {" sites.',
    psEquivalent: "(Get-ChildItem -Recurse src/ -Filter *.cs | Select-String -Pattern 'o => \\{ if \\(_isTerminating\\) return; [A-Za-z]+\\(\\); \\}' -AllMatches).Matches.Count",
  },
  {
    id: '5',
    gate: 'Devcontainer presence',
    check: 'test -f .devcontainer/devcontainer.json && test -f .devcontainer/Dockerfile',
    expected: 'exit 0',
    status: 'needs-adjustment',
    issue: '⚠ test -f is a POSIX shell builtin. Not available in PowerShell or cmd.exe on Windows.',
    psEquivalent: "(Test-Path '.devcontainer/devcontainer.json') -and (Test-Path '.devcontainer/Dockerfile')",
  },
  {
    id: '6',
    gate: 'Label-sync presence',
    check: 'test -f .github/workflows/label-sync.yml && test -f .github/labels.yml',
    expected: 'exit 0',
    status: 'needs-adjustment',
    issue: '⚠ test -f is POSIX-only.',
    psEquivalent: "(Test-Path '.github/workflows/label-sync.yml') -and (Test-Path '.github/labels.yml')",
  },
  {
    id: '7',
    gate: 'LFS config presence',
    check: 'test -f .gitattributes',
    expected: 'exit 0',
    status: 'needs-adjustment',
    issue: '⚠ test -f is POSIX-only.',
    psEquivalent: "Test-Path '.gitattributes'",
  },
  {
    id: '8',
    gate: 'Hook amendment',
    check: 'grep -q "ADR-019: LFS pointer gate" .git/hooks/pre-commit',
    expected: 'exit 0',
    status: 'needs-adjustment',
    issue: '⚠ grep is POSIX-only on Windows.',
    psEquivalent: "Select-String -Path '.git/hooks/pre-commit' -Pattern 'ADR-019: LFS pointer gate' -Quiet",
  },
  {
    id: '9',
    gate: 'Hook live test (LFS)',
    check: 'stage a non-LFS *.dll',
    expected: 'hook rejects with ADR-019 message',
    status: 'needs-adjustment',
    issue: '⚠ The pre-commit hook script (§D.3) is written in POSIX shell (bash syntax: for/case/do/done, head -c, wc -c). On Windows, Git\'s bundled sh.exe may run it if Git-for-Windows is installed, but this is not guaranteed for all dev environments. The hook itself uses POSIX commands and may fail on Windows without WSL or Git Bash.',
    psEquivalent: 'No direct PS equivalent for hook testing. Verify on Linux devcontainer or Git Bash. Alternatively, rewrite hook in PowerShell with pwsh shebang for Windows compatibility.',
  },
  {
    id: '10',
    gate: 'Hook live test (size)',
    check: 'stage a non-LFS file > 5 MiB',
    expected: 'hook rejects with ADR-019 message',
    status: 'needs-adjustment',
    issue: '⚠ Same as step 9 — hook uses POSIX shell commands (wc -c, head -c). Windows compatibility depends on Git Bash.',
    psEquivalent: 'Run on Linux devcontainer or in Git Bash on Windows to validate.',
  },
  {
    id: '11',
    gate: 'Portability (Linting)',
    check: "grep -c 'C:\\\\Users\\\\Mohammed' Linting.csproj",
    expected: 'zero hits',
    status: 'needs-adjustment',
    issue: '⚠ grep is POSIX-only. Backslash escaping in the pattern also differs between shells.',
    psEquivalent: "(Select-String -Path 'Linting.csproj' -Pattern 'C:\\\\Users\\\\Mohammed').Count -eq 0",
  },
  {
    id: '12',
    gate: 'Portability (deploy) — user profile',
    check: "grep -nE 'C:\\\\\\\\Users\\\\\\\\' deploy-sync.ps1",
    expected: 'zero hits',
    status: 'needs-adjustment',
    issue: '⚠ grep -nE (extended regex) is POSIX-only.',
    psEquivalent: "Select-String -Path 'deploy-sync.ps1' -Pattern 'C:\\\\Users\\\\'",
  },
  {
    id: '12b',
    gate: 'Portability (deploy) — repo/tool paths',
    check: "grep -nE 'C:\\\\\\\\(WSGTA|tmp)\\\\\\\\' deploy-sync.ps1",
    expected: 'zero hits',
    status: 'needs-adjustment',
    issue: '⚠ grep -nE with alternation is POSIX-only.',
    psEquivalent: "Select-String -Path 'deploy-sync.ps1' -Pattern 'C:\\\\(WSGTA|tmp)\\\\'",
  },
  {
    id: '12c',
    gate: 'Portability (deploy) — positive check',
    check: "grep -cE '\\$PSScriptRoot|\\$env:USERPROFILE|GetFolderPath' deploy-sync.ps1",
    expected: '>= 3 hits',
    status: 'needs-adjustment',
    issue: '⚠ grep -cE (extended regex) is POSIX-only. The -c flag returns per-file count; on a single file it gives a total — this part works on POSIX but not Windows.',
    psEquivalent: "(Select-String -Path 'deploy-sync.ps1' -Pattern '\\$PSScriptRoot|\\$env:USERPROFILE|GetFolderPath' -AllMatches).Matches.Count -ge 3",
  },
  {
    id: '13',
    gate: 'Build tag',
    check: "grep -n '1111.003-v28.0-adr019' src/V12_002.Constants.cs",
    expected: 'one hit on line 12',
    status: 'needs-adjustment',
    issue: '⚠ grep is POSIX-only on Windows.',
    psEquivalent: "Select-String -Path 'src/V12_002.Constants.cs' -Pattern '1111.003-v28.0-adr019'",
  },
  {
    id: '14',
    gate: 'Deploy sync round-trip',
    check: 'pwsh -File ./deploy-sync.ps1 then F5 in NT',
    expected: 'ASCII gate PASS, banner shows new BuildTag',
    status: 'runnable',
    issue: 'Requires Windows + NinjaTrader 8 installed. Fully runnable on the target developer machine. pwsh is cross-platform. The F5 step is NinjaTrader-specific and cannot run on the Linux devcontainer.',
  },
];

const statusColors: Record<StepStatus, string> = {
  'runnable': 'border-emerald-700 bg-emerald-950/10',
  'needs-adjustment': 'border-amber-600 bg-amber-950/20',
  'dependency-missing': 'border-red-700 bg-red-950/20',
};

const statusBadge: Record<StepStatus, string> = {
  'runnable': 'bg-emerald-700 text-white',
  'needs-adjustment': 'bg-amber-600 text-black',
  'dependency-missing': 'bg-red-700 text-white',
};

const statusLabel: Record<StepStatus, string> = {
  'runnable': '✓ Runnable',
  'needs-adjustment': '⚠ Needs Platform Adjustment',
  'dependency-missing': '✗ Dependency Missing',
};

export default function Section3_VerificationSteps() {
  const [expanded, setExpanded] = useState<string | null>(null);

  const counts = {
    runnable: steps.filter(s => s.status === 'runnable').length,
    adjustment: steps.filter(s => s.status === 'needs-adjustment').length,
    missing: steps.filter(s => s.status === 'dependency-missing').length,
  };

  const posixFlagged = steps.filter(s => s.status === 'needs-adjustment');

  return (
    <section id="section3" className="mb-12">
      <div className="flex items-center gap-3 mb-2">
        <span className="bg-teal-700 text-white text-xs font-bold px-2 py-1 rounded uppercase tracking-wider">Section F</span>
        <h3 className="text-xl font-bold text-white">Verification Step Platform Check — All 17 Steps</h3>
      </div>
      <p className="text-slate-400 text-sm mb-5">
        Source: <code className="text-teal-300">§F Verification Matrix</code> · 17 gate checks (steps 1–14, including 3b, 12b, 12c) reviewed for cross-platform compatibility.
      </p>

      {/* Stats + check_ascii callout */}
      <div className="grid grid-cols-1 md:grid-cols-2 gap-4 mb-5">
        <div className="grid grid-cols-3 gap-3">
          <div className="bg-emerald-950/40 border border-emerald-700 rounded-lg p-3 text-center">
            <div className="text-2xl font-bold text-emerald-400">{counts.runnable}</div>
            <div className="text-xs text-slate-300 mt-1">Runnable</div>
          </div>
          <div className="bg-amber-950/40 border border-amber-600 rounded-lg p-3 text-center">
            <div className="text-2xl font-bold text-amber-400">{counts.adjustment}</div>
            <div className="text-xs text-slate-300 mt-1">Needs Adjustment</div>
          </div>
          <div className="bg-red-950/40 border border-red-700 rounded-lg p-3 text-center">
            <div className="text-2xl font-bold text-red-400">{counts.missing}</div>
            <div className="text-xs text-slate-300 mt-1">Missing Deps</div>
          </div>
        </div>
        <div className="bg-slate-800 border border-teal-700 rounded-lg p-3">
          <div className="text-xs font-bold text-teal-300 uppercase tracking-wide mb-1">check_ascii.py — Exists at Repo Root?</div>
          <div className="text-xs text-slate-300 leading-relaxed">
            <span className="text-emerald-400 font-bold">✓ CONFIRMED.</span> Section §B.2 states: <em>"The repo-canonical ASCII-purity tool is check_ascii.py at the repo root, referenced by CLAUDE.md section 'CRITICAL: ASCII-Only in All C# String Literals.'"</em> Additionally §B.2 notes byte_purge.py is absent (<em>"verified via filesystem search **/byte_purge.py → 0 matches"</em>) while check_ascii.py is the replacement target for lines 89 and 99. The document provides direct evidence of its existence.
          </div>
        </div>
      </div>

      {/* POSIX flag summary */}
      <div className="bg-amber-950/30 border border-amber-600 rounded-xl p-4 mb-5">
        <div className="text-xs font-bold text-amber-300 uppercase tracking-wide mb-2">
          POSIX-Only Commands Flagged ({posixFlagged.length} steps)
        </div>
        <div className="flex flex-wrap gap-2">
          {posixFlagged.map(s => (
            <span key={s.id} className="text-xs bg-amber-900/60 border border-amber-700 text-amber-200 px-2 py-0.5 rounded font-mono">
              Step {s.id}
            </span>
          ))}
        </div>
        <div className="mt-2 text-xs text-amber-200">
          All use <code className="bg-black/40 px-1 rounded">grep</code>, <code className="bg-black/40 px-1 rounded">test -f</code>, or POSIX shell constructs not available in Windows PowerShell without WSL or Git Bash. PowerShell equivalents are provided for each below.
        </div>
      </div>

      {/* Step list */}
      <div className="space-y-2">
        {steps.map(step => (
          <div
            key={step.id}
            className={`rounded-lg border p-3 cursor-pointer transition-all hover:opacity-90 ${statusColors[step.status]}`}
            onClick={() => setExpanded(expanded === step.id ? null : step.id)}
          >
            <div className="flex flex-wrap items-start justify-between gap-2">
              <div className="flex items-center gap-2 flex-wrap">
                <span className="text-slate-500 text-xs font-bold w-8">#{step.id}</span>
                <span className={`text-xs font-bold px-2 py-0.5 rounded ${statusBadge[step.status]}`}>
                  {statusLabel[step.status]}
                </span>
                <span className="text-sm text-white font-medium">{step.gate}</span>
              </div>
              <span className="text-xs text-slate-500">{expanded === step.id ? '▲' : '▼'}</span>
            </div>

            <div className="mt-2 flex gap-2 items-start">
              <span className="text-xs text-slate-500 w-14 shrink-0">Check:</span>
              <code className="text-xs font-mono text-slate-300 bg-black/30 px-2 py-0.5 rounded break-all">{step.check}</code>
            </div>
            <div className="mt-1 flex gap-2 items-start">
              <span className="text-xs text-slate-500 w-14 shrink-0">Expected:</span>
              <span className="text-xs text-slate-300">{step.expected}</span>
            </div>

            {expanded === step.id && (
              <div className="mt-3 pt-3 border-t border-slate-700 space-y-2">
                <div className={`text-xs leading-relaxed ${step.status === 'needs-adjustment' ? 'text-amber-200' : 'text-slate-300'}`}>
                  {step.issue}
                </div>
                {step.psEquivalent && (
                  <div>
                    <div className="text-xs font-bold text-blue-300 mb-1">PowerShell Equivalent:</div>
                    <code className="block text-xs font-mono text-green-300 bg-black/50 px-3 py-2 rounded border border-slate-700 break-all leading-relaxed">
                      {step.psEquivalent}
                    </code>
                  </div>
                )}
              </div>
            )}
          </div>
        ))}
      </div>
    </section>
  );
}
