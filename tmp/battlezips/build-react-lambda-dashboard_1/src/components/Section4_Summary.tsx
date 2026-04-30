interface FindingItem {
  id: string;
  section: string;
  description: string;
  recommendation: string;
}

const blocking: FindingItem[] = [
  {
    id: 'B-1',
    section: '§C.1 / §C.3 / §G Task 1',
    description: 'Site #5 (AccountOrders.cs:369): Type 2 — _followerReplaceSpecs.TryRemove(sigName, out _) is executed AFTER SubmitFollowerReplacement() inside the lambda body. The guard as first statement bypasses this TryRemove. The NEW code block in §C.3 does NOT fix this — TryRemove remains after the primary call and will be skipped on early return. Permanent ConcurrentDictionary reservation leak. Red Team explicitly flagged this site in §G Task 1.',
    recommendation: 'Promote TryRemove to a finally block (or unconditional call outside the guard scope) before engineering begins. Requires plan revision.',
  },
  {
    id: 'B-2',
    section: '§C.1 / §C.3 / §G Task 1',
    description: 'Site #11 (REAPER.Audit.cs:136): Type 2 — _repairInFlight.TryRemove(repairKey, out _) clears the in-flight reservation guard. If the guard fires and returns early before ProcessReaperRepairQueue() runs, the in-flight key is never cleared internally. Permanent lockout of repairKey — no future REAPER repair can be triggered for that account. Red Team explicitly flagged this site in §G Task 1.',
    recommendation: 'ProcessReaperRepairQueue() must unconditionally clear repairKey via a finally block or the in-flight removal must occur before TriggerCustomEvent. Requires plan revision.',
  },
  {
    id: 'B-3',
    section: '§C.1 / §C.4',
    description: 'Site #16 (SIMA.Dispatch.cs:60): Type 2 — purpose explicitly names "semaphore contention." A semaphore acquired before this lambda is scheduled must be Released after the retry attempt. Guard as first statement would bypass the semaphore Release. No code block is worked in §C.3 for this site — the document does not show what follows the retry call inside this lambda, making verification impossible from the document alone.',
    recommendation: 'Architect must provide worked OLD/NEW block for site #16 confirming semaphore Release placement. Mark as unverifiable until block is provided.',
  },
];

const moderate: FindingItem[] = [
  {
    id: 'M-1',
    section: '§D.4.b / §F Step 12c',
    description: 'deploy-sync.ps1 Line 89: The proposed new comment contains "Join-Path $PSScriptRoot \'check_ascii.py\'" inside a # comment. PowerShell does NOT evaluate expressions in comment lines. A developer reads the literal text, not a resolved path. This is a wording inconsistency — the comment implies an evaluatable expression but delivers a dead string.',
    recommendation: "Rephrase to plain English: '# Fix: run python check_ascii.py src/ from the repo root, then re-run deploy-sync.ps1'. Advisory but noted as inconsistency.",
  },
  {
    id: 'M-2',
    section: '§D.4.a',
    description: "Linting.csproj:37 — HintPath changed to $(UserProfile)\\Documents\\NinjaTrader 8\\bin\\Custom\\NinjaTrader.Custom.dll without a Condition attribute. On Linux CI (devcontainer), NinjaTrader.Custom.dll does not exist. MSBuild will attempt to resolve the reference and may fail or emit errors blocking CI. Non-Goal explicitly excludes this — creating a platform CI gap.",
    recommendation: "Add Condition=\" '$(OS)' == 'Windows_NT' \" to the Reference element, or document that Linting.csproj is never built in CI (Linux). Requires plan clarification or Non-Goal adjustment.",
  },
  {
    id: 'M-3',
    section: '§F Steps 2–13 (15 of 17 steps)',
    description: "15 of 17 verification steps use grep, test -f, or other POSIX-only commands. The plan does not provide Windows PowerShell equivalents. Since the primary development environment is Windows (deploy-sync.ps1, NinjaTrader 8), the engineer cannot run most verification checks natively without WSL or Git Bash.",
    recommendation: 'Add a PowerShell verification matrix alongside the POSIX one, or state explicitly that all verification must be run in WSL/devcontainer. Moderate blocker for Windows-native P4 engineer.',
  },
];

const advisory: FindingItem[] = [
  {
    id: 'A-1',
    section: '§D.3',
    description: 'The pre-commit hook (§D.3) is written in POSIX shell (bash syntax). On Windows without Git Bash or WSL, the hook may not execute correctly. Git-for-Windows ships a bundled sh.exe that usually handles simple scripts but the LFS pointer check (head -c 50 | grep -q) and size gate (wc -c) are POSIX commands.',
    recommendation: 'Document that install_hooks.ps1 is intended for Linux/devcontainer. Add a note for Windows devs to use Git Bash or WSL for hook execution, or provide a PowerShell hook variant.',
  },
  {
    id: 'A-2',
    section: '§C.4',
    description: "Transform B grep check in §F Step 4 uses regex 'o => \\{ if \\(_isTerminating\\) return; [A-Za-z]+\\(\\); \\}' — this only matches lambda parameter 'o'. Sites #13 and #15 (REAPER.Audit.cs:250 and :372) use parameter 'e' per §C.3 sister-site note ('lambda parameter name (o vs e) is preserved verbatim'). The verification grep would miss those 2 sites and return 4 hits instead of 6.",
    recommendation: "Fix grep pattern to match both: 'o|e => \\{' or use a broader pattern. Advisory — does not block engineering but will cause false verification failures.",
  },
  {
    id: 'A-3',
    section: '§B.4',
    description: 'The 14-model consensus table in §B.4 shows Gemini 4.7 entry as "Gemini-4.7-CLI" in the verdict column rather than an approval status. The format is inconsistent with other rows. This may indicate the entry is incomplete or a placeholder.',
    recommendation: 'Clarify Gemini 4.7 pre-repair verdict for completeness.',
  },
  {
    id: 'A-4',
    section: '§D.3',
    description: 'The 5 MiB hook gate (§D.3) does not make an exception for graphify-out/ or other known large non-LFS files referenced in §G Task 4 Red Team challenge. If any legitimate file in these directories exceeds 5 MiB without LFS tracking, every commit touching that directory would be rejected.',
    recommendation: 'Document known large-file paths or add path exclusions to the size gate. Noted per §G Task 4 adversarial challenge.',
  },
];

const verdict = 'NEEDS PLAN UPDATE';
const verdictItems = [
  'B-1: Site #5 Type 2 cleanup leak — plan revision required',
  'B-2: Site #11 Type 2 in-flight lockout — plan revision required',
  'B-3: Site #16 semaphore release — worked block required',
  'M-2: Linting.csproj missing CI Condition — clarification required',
];

export default function Section4_Summary() {
  return (
    <section id="section4" className="mb-12">
      <div className="flex items-center gap-3 mb-2">
        <span className="bg-rose-700 text-white text-xs font-bold px-2 py-1 rounded uppercase tracking-wider">Overall</span>
        <h3 className="text-xl font-bold text-white">Summary — Plan Readiness Verdict</h3>
      </div>
      <p className="text-slate-400 text-sm mb-5">
        All findings cite exact sections. Grouped by severity. Engineering must not begin until Blocking items are resolved.
      </p>

      {/* Verdict banner */}
      <div className="bg-rose-950/60 border-2 border-rose-500 rounded-xl p-5 mb-6">
        <div className="flex items-center gap-3 mb-3">
          <div className="text-3xl">🚫</div>
          <div>
            <div className="text-xl font-black text-rose-300">VERDICT: {verdict}</div>
            <div className="text-xs text-rose-400 mt-0.5">3 blocking items + 1 moderate requiring clarification before P4 engineering handoff</div>
          </div>
        </div>
        <div className="space-y-1">
          {verdictItems.map((item, i) => (
            <div key={i} className="flex items-start gap-2 text-xs text-rose-200">
              <span className="text-rose-400 mt-0.5">▸</span>
              <span>{item}</span>
            </div>
          ))}
        </div>
      </div>

      {/* Build Tag Confirmation */}
      <div className="bg-blue-950/40 border border-blue-600 rounded-xl p-4 mb-6">
        <div className="text-xs font-bold text-blue-300 uppercase tracking-wide mb-2">Build Tag Delta — Confirmed (§Header / §E)</div>
        <div className="flex items-center gap-3 text-sm font-mono flex-wrap">
          <code className="bg-red-950/50 border border-red-800 px-3 py-1.5 rounded text-red-300">Build 1111.002-v28.0</code>
          <span className="text-slate-400 text-lg">→</span>
          <code className="bg-green-950/50 border border-green-700 px-3 py-1.5 rounded text-green-300">Build 1111.003-v28.0-adr019</code>
        </div>
        <div className="text-xs text-slate-400 mt-2">Exact string from document header · File: <code className="text-blue-300">src/V12_002.Constants.cs:12</code></div>
      </div>

      {/* Blocking */}
      <div className="mb-5">
        <div className="flex items-center gap-2 mb-3">
          <div className="w-3 h-3 rounded-full bg-rose-500"></div>
          <h4 className="text-base font-bold text-rose-400">BLOCKING ({blocking.length} items)</h4>
          <span className="text-xs text-rose-600">Engineering must not begin until resolved</span>
        </div>
        <div className="space-y-3">
          {blocking.map(item => (
            <div key={item.id} className="bg-rose-950/30 border border-rose-700 rounded-xl p-4">
              <div className="flex items-start gap-3 mb-2">
                <span className="text-xs font-black bg-rose-700 text-white px-2 py-0.5 rounded shrink-0">{item.id}</span>
                <span className="text-xs text-rose-300 font-mono">{item.section}</span>
              </div>
              <div className="text-xs text-rose-200 leading-relaxed mb-2">{item.description}</div>
              <div className="bg-rose-900/40 border border-rose-800 rounded p-2">
                <span className="text-xs font-bold text-rose-300">Required action: </span>
                <span className="text-xs text-rose-200">{item.recommendation}</span>
              </div>
            </div>
          ))}
        </div>
      </div>

      {/* Moderate */}
      <div className="mb-5">
        <div className="flex items-center gap-2 mb-3">
          <div className="w-3 h-3 rounded-full bg-amber-500"></div>
          <h4 className="text-base font-bold text-amber-400">MODERATE ({moderate.length} items)</h4>
          <span className="text-xs text-amber-600">Path consistency issues, missing CI conditions</span>
        </div>
        <div className="space-y-3">
          {moderate.map(item => (
            <div key={item.id} className="bg-amber-950/20 border border-amber-600 rounded-xl p-4">
              <div className="flex items-start gap-3 mb-2">
                <span className="text-xs font-black bg-amber-600 text-black px-2 py-0.5 rounded shrink-0">{item.id}</span>
                <span className="text-xs text-amber-300 font-mono">{item.section}</span>
              </div>
              <div className="text-xs text-amber-200 leading-relaxed mb-2">{item.description}</div>
              <div className="bg-amber-900/30 border border-amber-800 rounded p-2">
                <span className="text-xs font-bold text-amber-300">Recommended action: </span>
                <span className="text-xs text-amber-200">{item.recommendation}</span>
              </div>
            </div>
          ))}
        </div>
      </div>

      {/* Advisory */}
      <div className="mb-5">
        <div className="flex items-center gap-2 mb-3">
          <div className="w-3 h-3 rounded-full bg-blue-500"></div>
          <h4 className="text-base font-bold text-blue-400">ADVISORY ({advisory.length} items)</h4>
          <span className="text-xs text-blue-600">Platform notes, comment wording, minor gaps</span>
        </div>
        <div className="space-y-3">
          {advisory.map(item => (
            <div key={item.id} className="bg-blue-950/20 border border-blue-700 rounded-xl p-4">
              <div className="flex items-start gap-3 mb-2">
                <span className="text-xs font-black bg-blue-700 text-white px-2 py-0.5 rounded shrink-0">{item.id}</span>
                <span className="text-xs text-blue-300 font-mono">{item.section}</span>
              </div>
              <div className="text-xs text-blue-200 leading-relaxed mb-2">{item.description}</div>
              <div className="bg-blue-900/30 border border-blue-800 rounded p-2">
                <span className="text-xs font-bold text-blue-300">Suggestion: </span>
                <span className="text-xs text-blue-200">{item.recommendation}</span>
              </div>
            </div>
          ))}
        </div>
      </div>

      {/* Final table */}
      <div className="bg-slate-800 border border-slate-600 rounded-xl p-4">
        <div className="text-xs font-bold text-slate-300 uppercase tracking-wide mb-3">Complete Finding Index</div>
        <div className="overflow-x-auto">
          <table className="w-full text-xs">
            <thead>
              <tr className="border-b border-slate-600">
                <th className="text-left text-slate-400 pb-2 pr-3 w-12">ID</th>
                <th className="text-left text-slate-400 pb-2 pr-3 w-20">Severity</th>
                <th className="text-left text-slate-400 pb-2 pr-3">Section</th>
                <th className="text-left text-slate-400 pb-2">Status</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-700/50">
              {[...blocking.map(i => ({...i, sev: 'Blocking'})), ...moderate.map(i => ({...i, sev: 'Moderate'})), ...advisory.map(i => ({...i, sev: 'Advisory'}))].map(item => (
                <tr key={item.id}>
                  <td className="py-1.5 pr-3 font-bold text-white">{item.id}</td>
                  <td className={`py-1.5 pr-3 font-semibold ${
                    item.sev === 'Blocking' ? 'text-rose-400' : item.sev === 'Moderate' ? 'text-amber-400' : 'text-blue-400'
                  }`}>{item.sev}</td>
                  <td className="py-1.5 pr-3 font-mono text-slate-400">{item.section}</td>
                  <td className="py-1.5 text-slate-300">
                    {item.sev === 'Blocking' ? '🚫 Plan revision required' : item.sev === 'Moderate' ? '⚠ Clarification required' : '💡 Advisory'}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>
    </section>
  );
}
