import { useState } from 'react';

interface PathChange {
  location: string;
  section: string;
  old: string;
  new_: string;
  status: 'consistent' | 'inconsistency';
  note: string;
}

const pathChanges: PathChange[] = [
  {
    location: 'deploy-sync.ps1 · Line 8',
    section: '§D.4.b Sub-block 1',
    old: '$RepoRoot = "C:\\\\WSGTA\\\\universal-or-strategy"',
    new_: '$RepoRoot = $PSScriptRoot',
    status: 'consistent',
    note: 'Consistent with deploy-vm-safe.ps1:10 pattern. $PSScriptRoot resolves to the directory containing the script (repo root). Correctly substitutes hardcoded drive path.',
  },
  {
    location: 'deploy-sync.ps1 · Line 9',
    section: '§D.4.b Sub-block 1',
    old: '$NtCustomDir = "C:\\\\Users\\\\Mohammed Khalid\\\\Documents\\\\NinjaTrader 8\\\\bin\\\\Custom"',
    new_: '$NtCustomDir = Join-Path $env:USERPROFILE "Documents\\\\NinjaTrader 8\\\\bin\\\\Custom"',
    status: 'consistent',
    note: 'Consistent with deploy-vm-safe.ps1:10 model. $env:USERPROFILE is the correct substitution for user-profile paths per §B.2. Portable across Windows user accounts.',
  },
  {
    location: 'deploy-sync.ps1 · Line 89',
    section: '§D.4.b Sub-block 2',
    old: '# Fix: run C:\\\\tmp\\\\byte_purge.py, then re-run deploy-sync.ps1',
    new_: "# Fix: run `python (Join-Path $PSScriptRoot 'check_ascii.py') src/`, then re-run deploy-sync.ps1",
    status: 'inconsistency',
    note: '⚠ INCONSISTENCY: This is a # comment line. In PowerShell, everything after # is a plain string — Join-Path $PSScriptRoot is NOT evaluated at runtime. A developer reading the comment sees the literal text "Join-Path $PSScriptRoot \'check_ascii.py\'", not a resolved path. The comment is more informative than the old one (it names check_ascii.py and anchors to $PSScriptRoot conceptually) but it is NOT a working command. Suggest: change to a plain static relative path comment, e.g. "# Fix: run python check_ascii.py src/ from the repo root" for unambiguous developer guidance.',
  },
  {
    location: 'deploy-sync.ps1 · Line 99',
    section: '§D.4.b Sub-block 2',
    old: 'Write-Host "  Fix: python C:\\\\tmp\\\\byte_purge.py  then re-run deploy-sync.ps1" -ForegroundColor Red',
    new_: 'Write-Host "  Fix: python $(Join-Path $PSScriptRoot \'check_ascii.py\') src/  then re-run deploy-sync.ps1" -ForegroundColor Red',
    status: 'consistent',
    note: 'Inside a double-quoted string in PowerShell, $(…) IS evaluated at runtime. Write-Host will print the resolved path. This is consistent and correct. The developer sees the actual resolved filesystem path in the red error message. No issue.',
  },
  {
    location: 'Linting.csproj · Line 37',
    section: '§D.4.a',
    old: '<HintPath>C:\\\\Users\\\\Mohammed Khalid\\\\Documents\\\\NinjaTrader 8\\\\bin\\\\Custom\\\\NinjaTrader.Custom.dll</HintPath>',
    new_: '<HintPath>$(UserProfile)\\\\Documents\\\\NinjaTrader 8\\\\bin\\\\Custom\\\\NinjaTrader.Custom.dll</HintPath>',
    status: 'inconsistency',
    note: '⚠ INCONSISTENCY / MODERATE: $(UserProfile) resolves correctly on Windows. On Linux CI, NinjaTrader.Custom.dll does not exist — but MSBuild will still attempt to resolve the HintPath and may emit a warning or error if the dll is absent and a build target requires it. The plan states "MSBuild resolves $(UserProfile) … on Linux/macOS" but does NOT add a Condition attribute (e.g., Condition=" \'$(OS)\' == \'Windows_NT\' ") to skip the reference on Linux. Without a Condition, the Linux devcontainer build may fail or warn. The Non-Goal "no touching of NinjaTrader DLL hints beyond the one user-profile path" is in tension with adding a Condition — this needs clarification before engineering.',
  },
];

export default function Section2_PathConsistency() {
  const [expanded, setExpanded] = useState<number | null>(null);

  return (
    <section id="section2" className="mb-12">
      <div className="flex items-center gap-3 mb-2">
        <span className="bg-purple-700 text-white text-xs font-bold px-2 py-1 rounded uppercase tracking-wider">Section D.4</span>
        <h3 className="text-xl font-bold text-white">Path Substitution Consistency — All 5 Proposed Changes</h3>
      </div>
      <p className="text-slate-400 text-sm mb-5">
        Source: <code className="text-purple-300">§D.4.a (Linting.csproj:37)</code> and <code className="text-purple-300">§D.4.b (deploy-sync.ps1 lines 8, 9, 89, 99)</code>
      </p>

      {/* Key question callout */}
      <div className="mb-5 bg-slate-800 border border-purple-600 rounded-xl p-4">
        <div className="text-xs font-bold text-purple-300 uppercase tracking-wide mb-3">Key Architectural Questions — Answered</div>
        <div className="space-y-3">
          <div className="bg-slate-900 rounded-lg p-3 border border-slate-700">
            <div className="text-xs font-bold text-amber-300 mb-1">Q: Does <code className="bg-black/40 px-1 rounded">Join-Path</code> inside a <code className="bg-black/40 px-1 rounded"># comment</code> get evaluated at runtime?</div>
            <div className="text-xs text-slate-300 leading-relaxed">
              <span className="text-red-400 font-bold">NO.</span> In PowerShell, the <code className="bg-black/40 px-1 rounded">#</code> character begins a line comment — everything after it is ignored by the interpreter. <code className="bg-black/40 px-1 rounded">Join-Path $PSScriptRoot 'check_ascii.py'</code> inside a <code className="bg-black/40 px-1 rounded">#</code> comment is a dead string literal. A developer reading that comment sees the literal text <strong>"Join-Path $PSScriptRoot 'check_ascii.py'"</strong>, not a resolved path. The new comment is more informative than <code className="bg-black/40 px-1 rounded">C:\tmp\byte_purge.py</code> (it names the correct tool and the substitution model) but it does NOT produce a resolved command. Recommendation: rephrase to a plain relative path: <em>"# Fix: run python check_ascii.py src/ from repo root, then re-run deploy-sync.ps1"</em>
            </div>
          </div>
          <div className="bg-slate-900 rounded-lg p-3 border border-slate-700">
            <div className="text-xs font-bold text-amber-300 mb-1">Q: Does Linting.csproj HintPath need a <code className="bg-black/40 px-1 rounded">Condition</code> attribute on Linux CI?</div>
            <div className="text-xs text-slate-300 leading-relaxed">
              <span className="text-amber-400 font-bold">YES — strongly advisable.</span> The plan changes the path to <code className="bg-black/40 px-1 rounded">{"$(UserProfile)\\Documents\\NinjaTrader 8\\bin\\Custom\\NinjaTrader.Custom.dll"}</code>. On the Linux devcontainer, <code className="bg-black/40 px-1 rounded">NinjaTrader.Custom.dll</code> does not exist (NinjaTrader is Windows-only). MSBuild will resolve the HintPath but fail to find the DLL. If any build target requires it, the Linux build will fail. A <code className="bg-black/40 px-1 rounded">{"Condition=\" '$(OS)' == 'Windows_NT' \""}</code> attribute on the <code className="bg-black/40 px-1 rounded">Reference</code> element would skip it on Linux. The current plan explicitly excludes this under Non-Goals, creating a platform-CI gap.
            </div>
          </div>
        </div>
      </div>

      {/* Path change table */}
      <div className="space-y-3">
        {pathChanges.map((change, idx) => (
          <div
            key={idx}
            className={`rounded-xl border p-4 cursor-pointer transition-all ${
              change.status === 'inconsistency'
                ? 'border-amber-500 bg-amber-950/20 hover:bg-amber-950/30'
                : 'border-emerald-700 bg-emerald-950/10 hover:bg-emerald-950/20'
            }`}
            onClick={() => setExpanded(expanded === idx ? null : idx)}
          >
            <div className="flex flex-wrap items-center justify-between gap-2 mb-2">
              <div className="flex items-center gap-2">
                <span className={`text-xs font-bold px-2 py-0.5 rounded ${
                  change.status === 'inconsistency'
                    ? 'bg-amber-600 text-black'
                    : 'bg-emerald-700 text-white'
                }`}>
                  {change.status === 'inconsistency' ? '⚠ Inconsistency Found' : '✓ Consistent'}
                </span>
                <span className="text-sm font-semibold text-white">{change.location}</span>
                <span className="text-xs text-slate-400">{change.section}</span>
              </div>
              <span className="text-xs text-slate-500">{expanded === idx ? '▲ hide detail' : '▼ show detail'}</span>
            </div>

            <div className="grid grid-cols-1 gap-2">
              <div className="flex gap-2 items-start">
                <span className="text-xs text-red-400 font-bold w-10 shrink-0 mt-0.5">OLD</span>
                <code className="text-xs text-slate-300 bg-black/40 px-2 py-1 rounded break-all leading-relaxed">{change.old}</code>
              </div>
              <div className="flex gap-2 items-start">
                <span className="text-xs text-green-400 font-bold w-10 shrink-0 mt-0.5">NEW</span>
                <code className="text-xs text-slate-300 bg-black/40 px-2 py-1 rounded break-all leading-relaxed">{change.new_}</code>
              </div>
            </div>

            {expanded === idx && (
              <div className={`mt-3 pt-3 border-t text-xs leading-relaxed ${
                change.status === 'inconsistency'
                  ? 'border-amber-700 text-amber-200'
                  : 'border-emerald-800 text-emerald-200'
              }`}>
                {change.note}
              </div>
            )}
          </div>
        ))}
      </div>

      <div className="mt-4 p-3 bg-slate-800 rounded-lg border border-slate-600 text-xs text-slate-400">
        <strong className="text-slate-200">Summary:</strong> 3 of 5 changes are consistent. 2 findings:
        {' '}<span className="text-amber-300">Line 89 comment</span> — Join-Path not evaluated (advisory);
        {' '}<span className="text-amber-300">Linting.csproj</span> — HintPath missing Condition for Linux CI (moderate).
        {' '}Line 99 Write-Host uses <code className="bg-black/40 px-1 rounded">$(…)</code> in a double-quoted string — this IS evaluated correctly. Lines 8 and 9 are clean substitutions.
      </div>
    </section>
  );
}
