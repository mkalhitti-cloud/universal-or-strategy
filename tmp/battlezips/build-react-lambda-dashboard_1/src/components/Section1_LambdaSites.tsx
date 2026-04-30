import { useState } from 'react';
import { sites } from '../data/sites';

const SITE5_OLD = `bool replacementScheduled = false;
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
}`;

const SITE5_NEW = `bool replacementScheduled = false;
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
}`;

const SITE11_OLD = `_reaperRepairQueue.Enqueue(acct.Name);
// B957/E1: Clear in-flight guard if TriggerCustomEvent fails, preventing permanent lockout.
try { TriggerCustomEvent(o => ProcessReaperRepairQueue(), null); }
catch (Exception repairTriggerEx)
{
    _repairInFlight.TryRemove(repairKey, out _); // [Build 968]
    Print("[REAPER] TriggerCustomEvent failed for " + repairKey + ": " + repairTriggerEx.Message + " -- in-flight cleared.");
}`;

const SITE11_NEW = `_reaperRepairQueue.Enqueue(acct.Name);
// B957/E1: Clear in-flight guard if TriggerCustomEvent fails, preventing permanent lockout.
try { TriggerCustomEvent(o => { if (_isTerminating) return; ProcessReaperRepairQueue(); }, null); }
catch (Exception repairTriggerEx)
{
    _repairInFlight.TryRemove(repairKey, out _); // [Build 968]
    Print("[REAPER] TriggerCustomEvent failed for " + repairKey + ": " + repairTriggerEx.Message + " -- in-flight cleared.");
}`;

type FilterType = 'all' | 'Type1' | 'Type2' | 'critical' | 'needsUpdate';

export default function Section1_LambdaSites() {
  const [filter, setFilter] = useState<FilterType>('all');
  const [expandedId, setExpandedId] = useState<number | null>(null);
  const [showSite5Code, setShowSite5Code] = useState(false);
  const [showSite11Code, setShowSite11Code] = useState(false);

  const filtered = sites.filter(s => {
    if (filter === 'Type1') return s.classification === 'Type1';
    if (filter === 'Type2') return s.classification === 'Type2';
    if (filter === 'critical') return s.redTeamCritical;
    if (filter === 'needsUpdate') return s.needsPlanUpdate;
    return true;
  });

  const type2Count = sites.filter(s => s.classification === 'Type2').length;
  const critCount = sites.filter(s => s.redTeamCritical).length;

  return (
    <section id="section1" className="mb-12">
      <div className="flex items-center gap-3 mb-2">
        <span className="bg-blue-700 text-white text-xs font-bold px-2 py-1 rounded uppercase tracking-wider">Section C.1</span>
        <h3 className="text-xl font-bold text-white">Lambda Site Inventory — All 32 Sites</h3>
      </div>
      <p className="text-slate-400 text-sm mb-4">
        Source: <code className="text-blue-300">docs/brain/implementation_plan.md § C.1 / C.3 / C.4</code> · 32 marshal lambda sites receiving ADR-019 orphan guard injection.
      </p>

      {/* Stats row */}
      <div className="grid grid-cols-2 md:grid-cols-4 gap-3 mb-5">
        {[
          { label: 'Total Sites', value: 32, color: 'bg-slate-700' },
          { label: 'Red-Team-Critical', value: critCount, color: 'bg-red-900' },
          { label: 'Type 2 (Risky)', value: type2Count, color: 'bg-amber-900' },
          { label: 'Needs Plan Update', value: sites.filter(s=>s.needsPlanUpdate).length, color: 'bg-rose-900' },
        ].map(stat => (
          <div key={stat.label} className={`${stat.color} rounded-lg p-3 border border-slate-600`}>
            <div className="text-2xl font-bold text-white">{stat.value}</div>
            <div className="text-xs text-slate-300 mt-1">{stat.label}</div>
          </div>
        ))}
      </div>

      {/* Filters */}
      <div className="flex flex-wrap gap-2 mb-4">
        {(['all','Type1','Type2','critical','needsUpdate'] as FilterType[]).map(f => (
          <button
            key={f}
            onClick={() => setFilter(f)}
            className={`px-3 py-1 rounded text-xs font-semibold border transition-colors ${
              filter === f
                ? 'bg-blue-600 border-blue-400 text-white'
                : 'bg-slate-800 border-slate-600 text-slate-300 hover:border-blue-500'
            }`}
          >
            {f === 'all' ? 'All 32' : f === 'Type1' ? 'Type 1 (Safe)' : f === 'Type2' ? 'Type 2 (Risky)' : f === 'critical' ? 'Red-Team-Critical' : 'Needs Plan Update'}
          </button>
        ))}
      </div>

      {/* Site 5 Deep Dive */}
      <div className="mb-4 bg-amber-950/40 border-2 border-amber-500 rounded-xl p-4">
        <div className="flex items-center justify-between mb-2">
          <div className="flex items-center gap-2">
            <span className="bg-amber-500 text-black text-xs font-black px-2 py-0.5 rounded">SITE #5 — DETAILED REVIEW</span>
            <span className="text-amber-300 text-xs font-semibold">AccountOrders.cs:369 · Transform A</span>
          </div>
          <button
            onClick={() => setShowSite5Code(!showSite5Code)}
            className="text-xs text-amber-300 border border-amber-600 px-2 py-1 rounded hover:bg-amber-900/50 transition-colors"
          >
            {showSite5Code ? 'Hide Code' : 'Show OLD / NEW Code'}
          </button>
        </div>
        <div className="grid grid-cols-1 md:grid-cols-3 gap-3 text-xs mb-2">
          <div><span className="text-slate-400">Method:</span> <span className="text-white font-mono">HandleMatchedFollowerOrder</span></div>
          <div><span className="text-slate-400">Classification:</span> <span className="text-rose-400 font-bold">⚠ TYPE 2</span></div>
          <div><span className="text-slate-400">Transform:</span> <span className="text-blue-300">A (statement-body)</span></div>
        </div>
        <div className="bg-rose-950/50 border border-rose-700 rounded p-2 text-xs text-rose-200 mb-2">
          <span className="font-bold text-rose-400">Variable that would not be cleaned:</span>{' '}
          <code className="bg-black/40 px-1 rounded">_followerReplaceSpecs</code> — the ConcurrentDictionary entry for{' '}
          <code className="bg-black/40 px-1 rounded">sigName</code> is removed by{' '}
          <code className="bg-black/40 px-1 rounded">_followerReplaceSpecs.TryRemove(sigName, out _)</code>{' '}
          AFTER <code className="bg-black/40 px-1 rounded">SubmitFollowerReplacement()</code>. Early return bypasses this removal. Permanent reservation leak.
        </div>
        <div className="bg-amber-950/60 border border-amber-700 rounded px-3 py-2 text-xs text-amber-200">
          <span className="font-bold">⚠ Needs Plan Update Before Engineering:</span> Guard must either (a) be placed AFTER TryRemove — but that defeats the orphan-guard purpose — or (b) the TryRemove must be promoted to a finally block / unconditional call outside the guard scope. Plan currently places guard as first statement which skips TryRemove. Red Team cited this in Section G Task 1.
        </div>
        {showSite5Code && (
          <div className="grid grid-cols-1 md:grid-cols-2 gap-3 mt-3">
            <div>
              <div className="text-xs text-red-400 font-bold mb-1">OLD (lines 367–383, verbatim from §C.3)</div>
              <pre className="bg-black/60 text-green-300 text-xs p-3 rounded overflow-x-auto leading-relaxed border border-slate-700 whitespace-pre-wrap">{SITE5_OLD}</pre>
            </div>
            <div>
              <div className="text-xs text-green-400 font-bold mb-1">NEW (proposed in §C.3)</div>
              <pre className="bg-black/60 text-green-300 text-xs p-3 rounded overflow-x-auto leading-relaxed border border-slate-700 whitespace-pre-wrap">{SITE5_NEW}</pre>
              <div className="mt-2 text-xs text-rose-300 bg-rose-950/50 rounded p-2 border border-rose-800">
                ⚠ <strong>Issue:</strong> <code>_followerReplaceSpecs.TryRemove(sigName, out _)</code> on line 9 of lambda body is still bypassed by the guard on line 1. The NEW block as written in §C.3 does NOT fix the Type 2 problem — the TryRemove remains after SubmitFollowerReplacement and will be skipped.
              </div>
            </div>
          </div>
        )}
      </div>

      {/* Site 11 Deep Dive */}
      <div className="mb-5 bg-amber-950/40 border-2 border-amber-500 rounded-xl p-4">
        <div className="flex items-center justify-between mb-2">
          <div className="flex items-center gap-2">
            <span className="bg-amber-500 text-black text-xs font-black px-2 py-0.5 rounded">SITE #11 — DETAILED REVIEW</span>
            <span className="text-amber-300 text-xs font-semibold">REAPER.Audit.cs:136 · Transform B</span>
          </div>
          <button
            onClick={() => setShowSite11Code(!showSite11Code)}
            className="text-xs text-amber-300 border border-amber-600 px-2 py-1 rounded hover:bg-amber-900/50 transition-colors"
          >
            {showSite11Code ? 'Hide Code' : 'Show OLD / NEW Code'}
          </button>
        </div>
        <div className="grid grid-cols-1 md:grid-cols-3 gap-3 text-xs mb-2">
          <div><span className="text-slate-400">Method:</span> <span className="text-white font-mono">AuditAccountState</span></div>
          <div><span className="text-slate-400">Classification:</span> <span className="text-rose-400 font-bold">⚠ TYPE 2</span></div>
          <div><span className="text-slate-400">Transform:</span> <span className="text-purple-300">B (expression → statement)</span></div>
        </div>
        <div className="bg-rose-950/50 border border-rose-700 rounded p-2 text-xs text-rose-200 mb-2">
          <span className="font-bold text-rose-400">Variable that would not be cleaned:</span>{' '}
          <code className="bg-black/40 px-1 rounded">_repairInFlight</code> — the ConcurrentDictionary entry for{' '}
          <code className="bg-black/40 px-1 rounded">repairKey</code> is removed only in the catch block via{' '}
          <code className="bg-black/40 px-1 rounded">_repairInFlight.TryRemove(repairKey, out _)</code>.
          If TriggerCustomEvent succeeds and the guard fires inside the lambda, <code className="bg-black/40 px-1 rounded">ProcessReaperRepairQueue()</code>{' '}
          never runs. If ProcessReaperRepairQueue internally removes repairKey, that removal is bypassed. Permanent in-flight lockout. Red Team Section G, Task 1 specifically flags sites #5 and #11.
        </div>
        <div className="bg-amber-950/60 border border-amber-700 rounded px-3 py-2 text-xs text-amber-200">
          <span className="font-bold">⚠ Needs Plan Update Before Engineering:</span> ProcessReaperRepairQueue() must clear the in-flight key unconditionally (possibly via finally block outside the guard). Plan as written does not address this scenario.
        </div>
        {showSite11Code && (
          <div className="grid grid-cols-1 md:grid-cols-2 gap-3 mt-3">
            <div>
              <div className="text-xs text-red-400 font-bold mb-1">OLD (lines 134–141, verbatim from §C.3)</div>
              <pre className="bg-black/60 text-green-300 text-xs p-3 rounded overflow-x-auto leading-relaxed border border-slate-700 whitespace-pre-wrap">{SITE11_OLD}</pre>
            </div>
            <div>
              <div className="text-xs text-green-400 font-bold mb-1">NEW (proposed in §C.3)</div>
              <pre className="bg-black/60 text-green-300 text-xs p-3 rounded overflow-x-auto leading-relaxed border border-slate-700 whitespace-pre-wrap">{SITE11_NEW}</pre>
              <div className="mt-2 text-xs text-rose-300 bg-rose-950/50 rounded p-2 border border-rose-800">
                ⚠ <strong>Issue:</strong> If guard fires, <code>ProcessReaperRepairQueue()</code> never executes. If that function clears <code>_repairInFlight[repairKey]</code>, the in-flight reservation is never released — permanent lockout of repairKey.
              </div>
            </div>
          </div>
        )}
      </div>

      {/* Site Cards Grid */}
      <div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-3 gap-3">
        {filtered.map(site => (
          <div
            key={site.id}
            className={`rounded-lg border p-3 cursor-pointer transition-all ${
              site.needsPlanUpdate
                ? 'border-amber-500 bg-amber-950/30 hover:bg-amber-950/50'
                : site.classification === 'Type2'
                ? 'border-rose-700 bg-rose-950/20 hover:bg-rose-950/30'
                : site.redTeamCritical
                ? 'border-red-800 bg-slate-900 hover:bg-slate-800'
                : 'border-slate-700 bg-slate-900 hover:bg-slate-800'
            }`}
            onClick={() => setExpandedId(expandedId === site.id ? null : site.id)}
          >
            <div className="flex items-start justify-between gap-2 mb-2">
              <div className="flex items-center gap-2">
                <span className={`text-lg font-black w-8 text-center ${site.redTeamCritical ? 'text-red-400' : 'text-slate-400'}`}>
                  #{site.id}
                </span>
                <div>
                  <div className="flex flex-wrap items-center gap-1">
                    <span className={`text-xs font-bold px-1.5 py-0.5 rounded ${
                      site.classification === 'Type2'
                        ? 'bg-rose-700 text-rose-100'
                        : 'bg-emerald-800 text-emerald-100'
                    }`}>
                      {site.classification === 'unverifiable' ? 'Unverifiable' : site.classification}
                    </span>
                    <span className={`text-xs px-1.5 py-0.5 rounded font-mono ${
                      site.transform === 'B' ? 'bg-purple-800 text-purple-100' : 'bg-blue-900 text-blue-200'
                    }`}>
                      Transform {site.transform}
                    </span>
                    {site.redTeamCritical && (
                      <span className="text-xs bg-red-900 text-red-200 px-1.5 py-0.5 rounded">🔴 RT-Critical</span>
                    )}
                  </div>
                </div>
              </div>
              {site.needsPlanUpdate && (
                <span className="text-xs bg-amber-600 text-black font-bold px-1.5 py-0.5 rounded whitespace-nowrap shrink-0">⚠ Needs Update</span>
              )}
            </div>

            <div className="text-xs font-mono text-slate-400 truncate mb-1" title={site.file}>
              {site.file.replace('src/', '')}
            </div>
            <div className="text-xs text-slate-300">
              <span className="text-slate-500">L{site.line}</span> · <span className="text-blue-300 font-semibold">{site.method}</span>
            </div>
            <div className="text-xs text-slate-400 mt-1 leading-snug">{site.purpose}</div>

            {expandedId === site.id && (
              <div className="mt-3 pt-3 border-t border-slate-700">
                <div className="text-xs font-bold text-slate-300 mb-1">Evidence / Reasoning:</div>
                <div className={`text-xs leading-relaxed ${site.needsPlanUpdate ? 'text-amber-200' : 'text-slate-300'}`}>
                  {site.evidence}
                </div>
              </div>
            )}
          </div>
        ))}
      </div>
      <div className="mt-3 text-xs text-slate-500 text-right">Showing {filtered.length} of 32 sites · Click any card to expand evidence</div>
    </section>
  );
}
