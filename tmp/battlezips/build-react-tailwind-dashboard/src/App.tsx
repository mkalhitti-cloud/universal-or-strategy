import { useState } from 'react';
import {
  FileCode, Route, CheckCircle, AlertTriangle, Shield,
  ChevronRight, XCircle, HelpCircle, Terminal, FileText,
  GitBranch, AlertOctagon, Info, ArrowRight, Search,
} from 'lucide-react';
import {
  lambdaSites, pathChanges, verificationSteps, summaryItems,
  readinessState, checkAsciiStatus, buildTagDeltaRaw,
} from './data';

type TabId = 'inventory' | 'paths' | 'verification' | 'summary';

const tabs: { id: TabId; label: string; icon: React.ReactNode; count?: number }[] = [
  { id: 'inventory', label: 'Lambda Site Inventory', icon: <FileCode size={18} />, count: 32 },
  { id: 'paths', label: 'Path Substitution', icon: <Route size={18} />, count: 5 },
  { id: 'verification', label: 'Verification Matrix', icon: <CheckCircle size={18} />, count: 17 },
  { id: 'summary', label: 'Overall Summary', icon: <Shield size={18} /> },
];

function ClassificationBadge({ type }: { type: string }) {
  const styles: Record<string, string> = {
    'Type 1': 'bg-emerald-100 text-emerald-800 border-emerald-200',
    'Type 2': 'bg-red-100 text-red-800 border-red-200',
    'Unverifiable': 'bg-amber-100 text-amber-800 border-amber-200',
  };
  return (
    <span className={`inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-xs font-semibold border ${styles[type] || ''}`}>
      {type === 'Type 2' && <AlertTriangle size={12} />}
      {type === 'Unverifiable' && <HelpCircle size={12} />}
      {type}
    </span>
  );
}

function TransformBadge({ t }: { t: string }) {
  return (
    <span className="inline-flex items-center px-2 py-0.5 rounded-full text-xs font-mono font-bold bg-slate-100 text-slate-700 border border-slate-200">
      Transform {t}
    </span>
  );
}

function SiteCard({ site }: { site: typeof lambdaSites[0] }) {
  const [expanded, setExpanded] = useState(false);

  return (
    <div className={`rounded-xl border-2 transition-all duration-200 ${
      site.needsPlanUpdate
        ? 'border-red-300 bg-red-50/50 hover:border-red-400'
        : site.classification === 'Unverifiable'
        ? 'border-amber-300 bg-amber-50/50 hover:border-amber-400'
        : 'border-slate-200 bg-white hover:border-slate-300'
    }`}>
      <div
        className="flex items-start gap-3 p-4 cursor-pointer select-none"
        onClick={() => setExpanded(!expanded)}
      >
        <div className={`flex-shrink-0 w-8 h-8 rounded-lg flex items-center justify-center text-sm font-bold ${
          site.redTeamCritical
            ? 'bg-red-100 text-red-700'
            : 'bg-slate-100 text-slate-600'
        }`}>
          {site.id}
        </div>
        <div className="flex-1 min-w-0">
          <div className="flex items-center gap-2 flex-wrap">
            <span className="font-semibold text-sm text-slate-900">{site.method}</span>
            {site.redTeamCritical && (
              <span className="text-[10px] font-bold uppercase tracking-wider text-red-600 bg-red-100 px-1.5 py-0.5 rounded">
                RT-Critical
              </span>
            )}
          </div>
          <div className="text-xs text-slate-500 font-mono mt-0.5 truncate">
            {site.file}:{site.line}
          </div>
          <div className="flex items-center gap-2 mt-1.5">
            <ClassificationBadge type={site.classification} />
            <TransformBadge t={site.transform} />
            {site.needsPlanUpdate && (
              <span className="text-[10px] font-bold uppercase tracking-wider text-red-700 bg-red-200 px-1.5 py-0.5 rounded animate-pulse">
                ⚠ Needs Plan Update
              </span>
            )}
          </div>
        </div>
        <ChevronRight size={18} className={`text-slate-400 flex-shrink-0 mt-1 transition-transform ${expanded ? 'rotate-90' : ''}`} />
      </div>

      {expanded && (
        <div className="px-4 pb-4 border-t border-slate-100 pt-3 space-y-3">
          <div>
            <div className="text-xs font-semibold text-slate-500 uppercase tracking-wider mb-1">Purpose</div>
            <div className="text-sm text-slate-700">{site.purpose}</div>
          </div>
          <div>
            <div className="text-xs font-semibold text-slate-500 uppercase tracking-wider mb-1">Evidence</div>
            <div className="text-sm text-slate-700 bg-slate-50 rounded-lg p-3 border border-slate-100">{site.evidence}</div>
          </div>

          {site.hasWorkedCode && site.oldCode && site.newCode && (
            <div className="grid grid-cols-1 lg:grid-cols-2 gap-3">
              <div>
                <div className="text-xs font-semibold text-red-500 uppercase tracking-wider mb-1 flex items-center gap-1">
                  <XCircle size={12} /> OLD Code
                </div>
                <pre className="text-xs bg-red-50 border border-red-200 rounded-lg p-3 overflow-x-auto font-mono text-slate-800 leading-relaxed">
                  {site.oldCode}
                </pre>
              </div>
              <div>
                <div className="text-xs font-semibold text-emerald-600 uppercase tracking-wider mb-1 flex items-center gap-1">
                  <CheckCircle size={12} /> NEW Code
                </div>
                <pre className="text-xs bg-emerald-50 border border-emerald-200 rounded-lg p-3 overflow-x-auto font-mono text-slate-800 leading-relaxed">
                  {site.newCode}
                </pre>
              </div>
            </div>
          )}

          {site.bypassedVariable && (
            <div>
              <div className="text-xs font-semibold text-amber-600 uppercase tracking-wider mb-1 flex items-center gap-1">
                <AlertOctagon size={12} /> Bypassed Variable / Resource
              </div>
              <div className="text-sm font-mono bg-amber-50 border border-amber-200 rounded-lg p-2 text-amber-900">
                {site.bypassedVariable}
              </div>
            </div>
          )}

          {site.needsPlanUpdate && (
            <div className="flex items-center gap-2 bg-red-100 border border-red-300 rounded-lg p-3">
              <AlertTriangle size={16} className="text-red-600 flex-shrink-0" />
              <span className="text-sm font-semibold text-red-800">
                This Type 2 site needs a plan update before engineering begins.
              </span>
            </div>
          )}
        </div>
      )}
    </div>
  );
}

function Section1Inventory() {
  const [filter, setFilter] = useState<'all' | 'Type 1' | 'Type 2' | 'Unverifiable'>('all');
  const [showCriticalOnly, setShowCriticalOnly] = useState(false);
  const [search, setSearch] = useState('');

  const filtered = lambdaSites.filter(s => {
    if (filter !== 'all' && s.classification !== filter) return false;
    if (showCriticalOnly && !s.redTeamCritical) return false;
    if (search) {
      const q = search.toLowerCase();
      return s.method.toLowerCase().includes(q) || s.file.toLowerCase().includes(q) || s.purpose.toLowerCase().includes(q);
    }
    return true;
  });

  const type1Count = lambdaSites.filter(s => s.classification === 'Type 1').length;
  const type2Count = lambdaSites.filter(s => s.classification === 'Type 2').length;
  const unverifiableCount = lambdaSites.filter(s => s.classification === 'Unverifiable').length;

  return (
    <div className="space-y-6">
      <div className="bg-white rounded-xl border border-slate-200 p-5">
        <h3 className="text-lg font-bold text-slate-900 mb-1">Section C.1 — Complete Lambda Site Inventory</h3>
        <p className="text-sm text-slate-500 mb-4">All 32 sites from the implementation plan. Expand any card for evidence, code diffs, and classification rationale.</p>

        <div className="grid grid-cols-2 md:grid-cols-4 gap-3 mb-4">
          <div className="bg-slate-50 rounded-lg p-3 text-center border border-slate-200">
            <div className="text-2xl font-bold text-slate-900">32</div>
            <div className="text-xs text-slate-500">Total Sites</div>
          </div>
          <div className="bg-emerald-50 rounded-lg p-3 text-center border border-emerald-200">
            <div className="text-2xl font-bold text-emerald-700">{type1Count}</div>
            <div className="text-xs text-emerald-600">Type 1 (Safe)</div>
          </div>
          <div className="bg-red-50 rounded-lg p-3 text-center border border-red-200">
            <div className="text-2xl font-bold text-red-700">{type2Count}</div>
            <div className="text-xs text-red-600">Type 2 (Needs Fix)</div>
          </div>
          <div className="bg-amber-50 rounded-lg p-3 text-center border border-amber-200">
            <div className="text-2xl font-bold text-amber-700">{unverifiableCount}</div>
            <div className="text-xs text-amber-600">Unverifiable</div>
          </div>
        </div>

        <div className="flex flex-wrap gap-2 mb-3">
          {(['all', 'Type 1', 'Type 2', 'Unverifiable'] as const).map(f => (
            <button
              key={f}
              onClick={() => setFilter(f)}
              className={`px-3 py-1.5 rounded-lg text-xs font-semibold transition-all ${
                filter === f
                  ? 'bg-slate-900 text-white'
                  : 'bg-slate-100 text-slate-600 hover:bg-slate-200'
              }`}
            >
              {f === 'all' ? `All (${lambdaSites.length})` : `${f} (${lambdaSites.filter(s => s.classification === f).length})`}
            </button>
          ))}
          <label className="flex items-center gap-1.5 ml-2 text-xs text-slate-600 cursor-pointer">
            <input
              type="checkbox"
              checked={showCriticalOnly}
              onChange={e => setShowCriticalOnly(e.target.checked)}
              className="rounded border-slate-300"
            />
            RT-Critical only
          </label>
        </div>

        <div className="relative">
          <Search size={16} className="absolute left-3 top-1/2 -translate-y-1/2 text-slate-400" />
          <input
            type="text"
            placeholder="Search by method, file, or purpose..."
            value={search}
            onChange={e => setSearch(e.target.value)}
            className="w-full pl-9 pr-4 py-2 rounded-lg border border-slate-200 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-transparent"
          />
        </div>
      </div>

      <div className="text-sm text-slate-500">Showing {filtered.length} of {lambdaSites.length} sites</div>

      <div className="space-y-3">
        {filtered.map(site => (
          <SiteCard key={site.id} site={site} />
        ))}
      </div>
    </div>
  );
}

function Section2PathSubstitution() {
  return (
    <div className="space-y-6">
      <div className="bg-white rounded-xl border border-slate-200 p-5">
        <h3 className="text-lg font-bold text-slate-900 mb-1">Section D.4 — Path Substitution Consistency</h3>
        <p className="text-sm text-slate-500 mb-4">All 5 proposed path changes from the portability bundle. Each change is evaluated for consistency and potential issues.</p>
      </div>

      <div className="space-y-4">
        {pathChanges.map(change => (
          <div key={change.id} className={`rounded-xl border-2 p-5 ${
            change.consistent ? 'border-emerald-200 bg-emerald-50/30' : 'border-amber-200 bg-amber-50/30'
          }`}>
            <div className="flex items-center gap-2 mb-3">
              <span className="flex-shrink-0 w-7 h-7 rounded-lg bg-slate-900 text-white flex items-center justify-center text-sm font-bold">
                {change.id}
              </span>
              <div>
                <span className="font-semibold text-sm text-slate-900">{change.file}</span>
                <span className="text-xs text-slate-500 ml-2">line {change.line}</span>
              </div>
              <span className={`ml-auto text-xs font-bold px-2 py-1 rounded-full ${
                change.consistent
                  ? 'bg-emerald-100 text-emerald-700'
                  : 'bg-amber-100 text-amber-700'
              }`}>
                {change.consistent ? '✓ Consistent' : '⚠ Inconsistency Found'}
              </span>
            </div>

            <div className="grid grid-cols-1 lg:grid-cols-2 gap-3 mb-3">
              <div>
                <div className="text-xs font-semibold text-red-500 uppercase tracking-wider mb-1">OLD</div>
                <pre className="text-xs bg-red-50 border border-red-200 rounded-lg p-3 overflow-x-auto font-mono text-red-900 leading-relaxed">
                  {change.oldPath}
                </pre>
              </div>
              <div>
                <div className="text-xs font-semibold text-emerald-600 uppercase tracking-wider mb-1">NEW</div>
                <pre className="text-xs bg-emerald-50 border border-emerald-200 rounded-lg p-3 overflow-x-auto font-mono text-emerald-900 leading-relaxed">
                  {change.newPath}
                </pre>
              </div>
            </div>

            <div className="flex items-start gap-2 bg-white rounded-lg p-3 border border-slate-200">
              <Info size={14} className="text-blue-500 flex-shrink-0 mt-0.5" />
              <span className="text-sm text-slate-700">{change.notes}</span>
            </div>
          </div>
        ))}
      </div>

      {/* Special Questions */}
      <div className="bg-white rounded-xl border border-slate-200 p-5 space-y-4">
        <h4 className="font-bold text-slate-900 flex items-center gap-2">
          <Terminal size={18} /> Specific Analysis Questions
        </h4>

        <div className="bg-slate-50 rounded-lg p-4 border border-slate-200">
          <div className="font-semibold text-sm text-slate-900 mb-2">
            Q: Does a PowerShell function call inside a comment (e.g., Join-Path inside a # comment line) get evaluated at runtime?
          </div>
          <div className="text-sm text-slate-700 space-y-2">
            <p className="font-semibold text-red-700 flex items-center gap-1">
              <XCircle size={14} /> No — comments are NEVER evaluated at runtime in PowerShell.
            </p>
            <p>
              In deploy-sync.ps1 line 89 (NEW), the comment reads:
            </p>
            <pre className="text-xs bg-slate-100 rounded p-2 font-mono overflow-x-auto">
# Fix: run `python (Join-Path $PSScriptRoot 'check_ascii.py') src/`, then re-run deploy-sync.ps1
            </pre>
            <p>
              The <code className="bg-slate-200 px-1 rounded">Join-Path</code> inside this comment is purely textual. A developer reading this comment sees the literal string <code className="bg-slate-200 px-1 rounded">Join-Path $PSScriptRoot 'check_ascii.py'</code> as documentation only — they must manually construct the actual command. This is a documentation inconsistency: the comment shows PowerShell syntax but is not executable.
            </p>
            <p className="text-xs text-slate-500 mt-1">
              <strong>Recommendation:</strong> Rewrite the comment to show the resolved path pattern or use a non-comment instruction format.
            </p>
          </div>
        </div>

        <div className="bg-slate-50 rounded-lg p-4 border border-slate-200">
          <div className="font-semibold text-sm text-slate-900 mb-2">
            Q: For Linting.csproj, does the HintPath need a Condition attribute on Linux CI?
          </div>
          <div className="text-sm text-slate-700 space-y-2">
            <p className="font-semibold text-amber-700 flex items-center gap-1">
              <AlertTriangle size={14} /> The document does NOT add a Condition attribute — this is a gap.
            </p>
            <p>
              The plan substitutes <code className="bg-slate-200 px-1 rounded">$(UserProfile)</code> which MSBuild resolves on both Windows and Linux/macOS. However, on Linux CI runners, the NinjaTrader DLL at <code className="bg-slate-200 px-1 rounded">~/Documents/NinjaTrader 8/bin/Custom/NinjaTrader.Custom.dll</code> will not exist. This may cause:
            </p>
            <ul className="list-disc list-inside text-xs text-slate-600 space-y-1 ml-2">
              <li>A build warning about missing reference assembly</li>
              <li>Potential build failure if the project requires the DLL to compile</li>
            </ul>
            <p className="text-xs text-slate-500 mt-1">
              <strong>Recommendation:</strong> Add <code className="bg-slate-200 px-1 rounded">Condition=" '$(OS)' == 'Windows_NT' "</code> to the HintPath element, or use a conditional ItemGroup.
            </p>
          </div>
        </div>
      </div>
    </div>
  );
}

function Section3Verification() {
  return (
    <div className="space-y-6">
      <div className="bg-white rounded-xl border border-slate-200 p-5">
        <h3 className="text-lg font-bold text-slate-900 mb-1">Section F — Verification Step Platform Check</h3>
        <p className="text-sm text-slate-500 mb-4">All 17 verification steps evaluated for platform compatibility. Steps using POSIX-only commands (grep, test -f) are flagged with PowerShell equivalents.</p>

        <div className="grid grid-cols-3 gap-3 mb-4">
          <div className="bg-emerald-50 rounded-lg p-3 text-center border border-emerald-200">
            <div className="text-2xl font-bold text-emerald-700">
              {verificationSteps.filter(s => s.status === 'runnable').length}
            </div>
            <div className="text-xs text-emerald-600">Runnable</div>
          </div>
          <div className="bg-amber-50 rounded-lg p-3 text-center border border-amber-200">
            <div className="text-2xl font-bold text-amber-700">
              {verificationSteps.filter(s => s.status === 'needs_platform_adjustment').length}
            </div>
            <div className="text-xs text-amber-600">Needs Platform Adjustment</div>
          </div>
          <div className="bg-red-50 rounded-lg p-3 text-center border border-red-200">
            <div className="text-2xl font-bold text-red-700">
              {verificationSteps.filter(s => s.status === 'dependency_missing').length}
            </div>
            <div className="text-xs text-red-600">Dependency Missing</div>
          </div>
        </div>
      </div>

      <div className="space-y-3">
        {verificationSteps.map(step => (
          <div key={step.id} className={`rounded-xl border-2 p-4 transition-all ${
            step.flagged
              ? 'border-amber-200 bg-amber-50/30'
              : 'border-emerald-200 bg-emerald-50/30'
          }`}>
            <div className="flex items-start gap-3">
              <span className={`flex-shrink-0 w-8 h-8 rounded-lg flex items-center justify-center text-sm font-bold ${
                step.flagged ? 'bg-amber-100 text-amber-700' : 'bg-emerald-100 text-emerald-700'
              }`}>
                {step.id}
              </span>
              <div className="flex-1 min-w-0">
                <div className="flex items-center gap-2 flex-wrap">
                  <span className="font-semibold text-sm text-slate-900">{step.gate}</span>
                  <span className={`text-[10px] font-bold uppercase tracking-wider px-1.5 py-0.5 rounded ${
                    step.status === 'runnable'
                      ? 'bg-emerald-100 text-emerald-700'
                      : step.status === 'needs_platform_adjustment'
                      ? 'bg-amber-100 text-amber-700'
                      : 'bg-red-100 text-red-700'
                  }`}>
                    {step.status === 'runnable' ? '✓ Runnable' : step.status === 'needs_platform_adjustment' ? '⚠ Platform Adjustment' : '✗ Dependency Missing'}
                  </span>
                </div>
                <pre className="text-xs bg-slate-100 rounded-lg p-2 mt-2 font-mono text-slate-700 overflow-x-auto">
                  {step.check}
                </pre>
                <div className="text-xs text-slate-500 mt-1">Expected: {step.expected}</div>

                {step.flagged && step.flaggedReason && (
                  <div className="flex items-start gap-2 mt-2 bg-amber-50 border border-amber-200 rounded-lg p-2.5">
                    <AlertTriangle size={14} className="text-amber-600 flex-shrink-0 mt-0.5" />
                    <span className="text-xs text-amber-800">{step.flaggedReason}</span>
                  </div>
                )}

                {step.powershellEquivalent && (
                  <div className="mt-2">
                    <div className="text-xs font-semibold text-blue-600 uppercase tracking-wider mb-1">PowerShell Equivalent</div>
                    <pre className="text-xs bg-blue-50 border border-blue-200 rounded-lg p-2 font-mono text-blue-900 overflow-x-auto">
                      {step.powershellEquivalent}
                    </pre>
                  </div>
                )}
              </div>
            </div>
          </div>
        ))}
      </div>

      {/* check_ascii.py status */}
      <div className="bg-white rounded-xl border border-slate-200 p-5">
        <h4 className="font-bold text-slate-900 mb-3 flex items-center gap-2">
          <FileText size={18} /> check_ascii.py Existence Check
        </h4>
        <div className="space-y-3">
          <div className={`inline-flex items-center gap-1.5 px-3 py-1.5 rounded-full text-sm font-semibold ${
            checkAsciiStatus.exists === 'Not verified by document'
              ? 'bg-amber-100 text-amber-800'
              : 'bg-emerald-100 text-emerald-800'
          }`}>
            {checkAsciiStatus.exists === 'Not verified by document' ? <HelpCircle size={14} /> : <CheckCircle size={14} />}
            {checkAsciiStatus.exists}
          </div>
          <div className="text-sm text-slate-700 bg-slate-50 rounded-lg p-4 border border-slate-200">
            <p className="mb-2">{checkAsciiStatus.evidence}</p>
            <p className="font-semibold text-slate-900">{checkAsciiStatus.conclusion}</p>
          </div>
        </div>
      </div>
    </div>
  );
}

function Section4Summary() {
  const grouped = {
    Blocking: summaryItems.filter(s => s.category === 'Blocking'),
    Moderate: summaryItems.filter(s => s.category === 'Moderate'),
    Advisory: summaryItems.filter(s => s.category === 'Advisory'),
  };

  const categoryStyles: Record<string, { bg: string; border: string; icon: React.ReactNode; text: string }> = {
    Blocking: {
      bg: 'bg-red-50', border: 'border-red-300', text: 'text-red-800',
      icon: <XCircle size={16} className="text-red-600" />,
    },
    Moderate: {
      bg: 'bg-amber-50', border: 'border-amber-300', text: 'text-amber-800',
      icon: <AlertTriangle size={16} className="text-amber-600" />,
    },
    Advisory: {
      bg: 'bg-blue-50', border: 'border-blue-300', text: 'text-blue-800',
      icon: <Info size={16} className="text-blue-600" />,
    },
  };

  return (
    <div className="space-y-6">
      {/* Readiness State */}
      <div className={`rounded-xl border-2 p-6 ${
        readinessState.status === 'NEEDS A PLAN UPDATE'
          ? 'border-red-300 bg-red-50'
          : 'border-emerald-300 bg-emerald-50'
      }`}>
        <div className="flex items-center gap-3 mb-4">
          <div className={`w-10 h-10 rounded-xl flex items-center justify-center ${
            readinessState.status === 'NEEDS A PLAN UPDATE' ? 'bg-red-200' : 'bg-emerald-200'
          }`}>
            {readinessState.status === 'NEEDS A PLAN UPDATE'
              ? <AlertOctagon size={22} className="text-red-700" />
              : <CheckCircle size={22} className="text-emerald-700" />
            }
          </div>
          <div>
            <div className="text-xs font-semibold text-slate-500 uppercase tracking-wider">Engineering Readiness</div>
            <div className={`text-xl font-bold ${
              readinessState.status === 'NEEDS A PLAN UPDATE' ? 'text-red-800' : 'text-emerald-800'
            }`}>
              {readinessState.status}
            </div>
          </div>
        </div>

        <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
          <div className="bg-white rounded-lg p-4 border border-red-200">
            <div className="text-xs font-bold text-red-600 uppercase tracking-wider mb-2 flex items-center gap-1">
              <XCircle size={12} /> Blocking ({readinessState.blockingItems.length})
            </div>
            <ul className="space-y-2">
              {readinessState.blockingItems.map((item, i) => (
                <li key={i} className="text-xs text-slate-700 flex items-start gap-1.5">
                  <ArrowRight size={12} className="text-red-500 flex-shrink-0 mt-0.5" />
                  {item}
                </li>
              ))}
            </ul>
          </div>
          <div className="bg-white rounded-lg p-4 border border-amber-200">
            <div className="text-xs font-bold text-amber-600 uppercase tracking-wider mb-2 flex items-center gap-1">
              <AlertTriangle size={12} /> Moderate ({readinessState.moderateItems.length})
            </div>
            <ul className="space-y-2">
              {readinessState.moderateItems.map((item, i) => (
                <li key={i} className="text-xs text-slate-700 flex items-start gap-1.5">
                  <ArrowRight size={12} className="text-amber-500 flex-shrink-0 mt-0.5" />
                  {item}
                </li>
              ))}
            </ul>
          </div>
          <div className="bg-white rounded-lg p-4 border border-blue-200">
            <div className="text-xs font-bold text-blue-600 uppercase tracking-wider mb-2 flex items-center gap-1">
              <Info size={12} /> Advisory ({readinessState.advisoryItems.length})
            </div>
            <ul className="space-y-2">
              {readinessState.advisoryItems.map((item, i) => (
                <li key={i} className="text-xs text-slate-700 flex items-start gap-1.5">
                  <ArrowRight size={12} className="text-blue-500 flex-shrink-0 mt-0.5" />
                  {item}
                </li>
              ))}
            </ul>
          </div>
        </div>
      </div>

      {/* Detailed Findings */}
      <div className="bg-white rounded-xl border border-slate-200 p-5">
        <h3 className="text-lg font-bold text-slate-900 mb-1">Detailed Findings by Category</h3>
        <p className="text-sm text-slate-500 mb-4">Every finding cites the exact section from the implementation plan.</p>

        {(['Blocking', 'Moderate', 'Advisory'] as const).map(category => {
          const style = categoryStyles[category];
          return (
            <div key={category} className="mb-6 last:mb-0">
              <div className={`flex items-center gap-2 mb-3 pb-2 border-b-2 ${style.border}`}>
                {style.icon}
                <span className={`font-bold text-sm ${style.text}`}>{category.toUpperCase()} ({grouped[category].length} items)</span>
              </div>
              <div className="space-y-3">
                {grouped[category].map((item, i) => (
                  <div key={i} className={`${style.bg} rounded-lg p-4 border ${style.border}`}>
                    <div className="font-semibold text-sm text-slate-900 mb-1">{item.item}</div>
                    <div className="text-xs text-slate-500 mb-2 flex items-center gap-1">
                      <GitBranch size={12} /> {item.section}
                    </div>
                    <div className="text-sm text-slate-700">{item.detail}</div>
                  </div>
                ))}
              </div>
            </div>
          );
        })}
      </div>

      {/* Final Verdict */}
      <div className="bg-gradient-to-r from-slate-900 to-slate-800 rounded-xl p-6 text-white">
        <h4 className="font-bold text-lg mb-3 flex items-center gap-2">
          <Shield size={20} /> Final Verdict
        </h4>
        <div className="space-y-3 text-sm text-slate-300">
          <p>
            <strong className="text-white">State: NEEDS A PLAN UPDATE</strong> — The implementation plan cannot proceed to the P4 engineering handoff until the following are resolved:
          </p>
          <ul className="space-y-1.5 list-disc list-inside">
            <li><strong className="text-white">3 Type 2 sites</strong> (#5, #16, #21) have documented state-cleanup bypass risks without mitigation strategies</li>
            <li><strong className="text-white">7 unverifiable sites</strong> (#6, #7, #8, #9, #22, #24, #27) cannot be classified from the document alone</li>
            <li><strong className="text-white">Path substitution inconsistency</strong> on deploy-sync.ps1 line 89 (Join-Path in comment)</li>
            <li><strong className="text-white">Missing Condition attribute</strong> on Linting.csproj HintPath for Linux CI</li>
            <li><strong className="text-white">14 of 17 verification steps</strong> use POSIX-only commands without PowerShell equivalents</li>
          </ul>
          <p className="mt-3 text-slate-400 text-xs">
            Per Section G: P4 Engineer handoff is SUSPENDED. The next action is for the Director to paste the Section G block into Antigravity to trigger the 14-model adversarial fleet audit. No implementation begins until every agent returns APPROVED on every target.
          </p>
        </div>
      </div>
    </div>
  );
}

export default function App() {
  const [activeTab, setActiveTab] = useState<TabId>('inventory');

  return (
    <div className="min-h-screen bg-slate-50">
      {/* Header */}
      <header className="bg-white border-b border-slate-200 sticky top-0 z-50">
        <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
          <div className="flex items-center justify-between h-16">
            <div className="flex items-center gap-3">
              <div className="w-9 h-9 rounded-lg bg-gradient-to-br from-violet-600 to-indigo-700 flex items-center justify-center">
                <Shield size={20} className="text-white" />
              </div>
              <div>
                <h2 className="text-base font-bold text-slate-900 leading-tight">
                  Claude — Anthropic AI (Version: Not Determinable from Memory)
                </h2>
                <p className="text-[11px] text-slate-500">ADR-019 Sovereign Substrate Repair — Consistency Review Dashboard</p>
              </div>
            </div>
            <div className="flex items-center gap-2">
              <span className="hidden sm:inline-flex items-center gap-1 text-xs font-mono bg-slate-100 text-slate-600 px-2 py-1 rounded">
                <GitBranch size={12} />
                mission-uni-5-full-sync
              </span>
              <span className="text-xs font-semibold bg-red-100 text-red-700 px-2 py-1 rounded">
                P4 SUSPENDED
              </span>
            </div>
          </div>
        </div>
      </header>

      {/* Build Tag Banner */}
      <div className="bg-gradient-to-r from-indigo-600 to-violet-600 text-white">
        <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-3">
          <div className="flex items-center gap-2 text-sm">
            <span className="font-semibold opacity-80">Build Tag Delta:</span>
            <code className="bg-white/20 px-2 py-0.5 rounded text-xs font-mono">{buildTagDeltaRaw}</code>
          </div>
        </div>
      </div>

      {/* Tab Navigation */}
      <div className="bg-white border-b border-slate-200">
        <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
          <nav className="flex gap-1 overflow-x-auto -mb-px" role="tablist">
            {tabs.map(tab => (
              <button
                key={tab.id}
                role="tab"
                aria-selected={activeTab === tab.id}
                onClick={() => setActiveTab(tab.id)}
                className={`flex items-center gap-2 px-4 py-3 text-sm font-medium border-b-2 transition-all whitespace-nowrap ${
                  activeTab === tab.id
                    ? 'border-indigo-600 text-indigo-700'
                    : 'border-transparent text-slate-500 hover:text-slate-700 hover:border-slate-300'
                }`}
              >
                {tab.icon}
                {tab.label}
                {tab.count !== undefined && (
                  <span className={`text-[10px] font-bold px-1.5 py-0.5 rounded-full ${
                    activeTab === tab.id ? 'bg-indigo-100 text-indigo-700' : 'bg-slate-100 text-slate-500'
                  }`}>
                    {tab.count}
                  </span>
                )}
              </button>
            ))}
          </nav>
        </div>
      </div>

      {/* Content */}
      <main className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-6">
        {activeTab === 'inventory' && <Section1Inventory />}
        {activeTab === 'paths' && <Section2PathSubstitution />}
        {activeTab === 'verification' && <Section3Verification />}
        {activeTab === 'summary' && <Section4Summary />}
      </main>

      {/* Footer */}
      <footer className="bg-white border-t border-slate-200 mt-8">
        <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-4">
          <div className="flex flex-col sm:flex-row items-center justify-between gap-2 text-xs text-slate-500">
            <span>Consistency review based solely on implementation_plan.md @ mission-uni-5-full-sync</span>
            <span>Every finding cites the exact section. No external sources used.</span>
          </div>
        </div>
      </footer>
    </div>
  );
}
