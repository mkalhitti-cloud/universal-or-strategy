import { useState } from "react";

// ─── Type Definitions ────────────────────────────────────────────────────────

type Severity = "critical" | "warning" | "ok" | "info";

interface BadgeProps {
  severity: Severity;
  label: string;
}

interface CardProps {
  title: string;
  icon: React.ReactNode;
  children: React.ReactNode;
  accent?: string;
}

interface CodeBlockProps {
  code: string;
  label?: string;
  variant?: "broken" | "fixed" | "neutral";
}

// ─── Sub-components ──────────────────────────────────────────────────────────

function Badge({ severity, label }: BadgeProps) {
  const styles: Record<Severity, string> = {
    critical: "bg-red-950 text-red-300 border border-red-700 shadow-red-900/40",
    warning:  "bg-amber-950 text-amber-300 border border-amber-700 shadow-amber-900/40",
    ok:       "bg-emerald-950 text-emerald-300 border border-emerald-700 shadow-emerald-900/40",
    info:     "bg-sky-950 text-sky-300 border border-sky-700 shadow-sky-900/40",
  };
  const dots: Record<Severity, string> = {
    critical: "bg-red-400 animate-pulse",
    warning:  "bg-amber-400 animate-pulse",
    ok:       "bg-emerald-400",
    info:     "bg-sky-400",
  };
  return (
    <span className={`inline-flex items-center gap-1.5 px-2.5 py-0.5 rounded-full text-xs font-semibold shadow ${styles[severity]}`}>
      <span className={`w-1.5 h-1.5 rounded-full ${dots[severity]}`} />
      {label}
    </span>
  );
}

function Card({ title, icon, children, accent = "from-slate-800 to-slate-900" }: CardProps) {
  return (
    <div className={`rounded-2xl border border-slate-700/60 bg-gradient-to-br ${accent} shadow-xl shadow-black/30 overflow-hidden`}>
      <div className="flex items-center gap-3 px-6 py-4 border-b border-slate-700/50 bg-black/20">
        <span className="text-slate-300">{icon}</span>
        <h3 className="text-sm font-semibold uppercase tracking-widest text-slate-300">{title}</h3>
      </div>
      <div className="px-6 py-5">{children}</div>
    </div>
  );
}

function CodeBlock({ code, label, variant = "neutral" }: CodeBlockProps) {
  const [copied, setCopied] = useState(false);
  const variantStyles: Record<string, string> = {
    broken:  "border-red-800/60 bg-red-950/30",
    fixed:   "border-emerald-800/60 bg-emerald-950/20",
    neutral: "border-slate-700/60 bg-slate-950/50",
  };
  const labelStyles: Record<string, string> = {
    broken:  "text-red-400",
    fixed:   "text-emerald-400",
    neutral: "text-slate-400",
  };
  const handleCopy = () => {
    navigator.clipboard.writeText(code).then(() => {
      setCopied(true);
      setTimeout(() => setCopied(false), 1800);
    });
  };
  return (
    <div className={`rounded-xl border ${variantStyles[variant]} overflow-hidden mt-3`}>
      {label && (
        <div className={`flex items-center justify-between px-4 py-1.5 border-b border-inherit bg-black/20`}>
          <span className={`text-xs font-semibold ${labelStyles[variant]}`}>{label}</span>
          <button
            onClick={handleCopy}
            className="text-xs text-slate-500 hover:text-slate-300 transition-colors"
          >
            {copied ? "✓ Copied" : "Copy"}
          </button>
        </div>
      )}
      <pre className="text-xs text-slate-200 font-mono leading-relaxed px-4 py-3 overflow-x-auto whitespace-pre-wrap break-words">
        {code}
      </pre>
    </div>
  );
}

function SectionLabel({ text }: { text: string }) {
  return (
    <p className="text-xs font-bold uppercase tracking-widest text-slate-500 mt-5 mb-2">{text}</p>
  );
}

function Divider() {
  return <div className="border-t border-slate-700/50 my-4" />;
}

// ─── Icons ───────────────────────────────────────────────────────────────────

const IconShield = () => (
  <svg className="w-5 h-5" fill="none" stroke="currentColor" strokeWidth={1.8} viewBox="0 0 24 24">
    <path strokeLinecap="round" strokeLinejoin="round" d="M12 3l7.5 4v5c0 5-3.5 8.5-7.5 10C4.5 20.5 4.5 17 4.5 12V7L12 3z" />
  </svg>
);
const IconCode = () => (
  <svg className="w-5 h-5" fill="none" stroke="currentColor" strokeWidth={1.8} viewBox="0 0 24 24">
    <polyline points="16 18 22 12 16 6" />
    <polyline points="8 6 2 12 8 18" />
  </svg>
);
const IconMatrix = () => (
  <svg className="w-5 h-5" fill="none" stroke="currentColor" strokeWidth={1.8} viewBox="0 0 24 24">
    <rect x="3" y="3" width="7" height="7" rx="1" />
    <rect x="14" y="3" width="7" height="7" rx="1" />
    <rect x="3" y="14" width="7" height="7" rx="1" />
    <rect x="14" y="14" width="7" height="7" rx="1" />
  </svg>
);
const IconChart = () => (
  <svg className="w-5 h-5" fill="none" stroke="currentColor" strokeWidth={1.8} viewBox="0 0 24 24">
    <polyline points="22 12 18 12 15 21 9 3 6 12 2 12" />
  </svg>
);
const IconCpu = () => (
  <svg className="w-5 h-5" fill="none" stroke="currentColor" strokeWidth={1.8} viewBox="0 0 24 24">
    <rect x="4" y="4" width="16" height="16" rx="2" />
    <rect x="9" y="9" width="6" height="6" />
    <line x1="9" y1="1" x2="9" y2="4" /><line x1="15" y1="1" x2="15" y2="4" />
    <line x1="9" y1="20" x2="9" y2="23" /><line x1="15" y1="20" x2="15" y2="23" />
    <line x1="20" y1="9" x2="23" y2="9" /><line x1="20" y1="15" x2="23" y2="15" />
    <line x1="1" y1="9" x2="4" y2="9" /><line x1="1" y1="15" x2="4" y2="15" />
  </svg>
);
const IconInfo = () => (
  <svg className="w-4 h-4" fill="none" stroke="currentColor" strokeWidth={2} viewBox="0 0 24 24">
    <circle cx="12" cy="12" r="10" />
    <line x1="12" y1="8" x2="12" y2="8" strokeLinecap="round" strokeWidth={2.5} />
    <line x1="12" y1="12" x2="12" y2="16" />
  </svg>
);

// ─── Data / Analysis Content ──────────────────────────────────────────────────

const BROKEN_SITE1 = `// Site 1 — V12_002.SIMA.Shadow.cs : Line 89
// ❌ BROKEN — ctx.Sync has been removed
lock (ctx.Sync)
{
    foreach (var x in ctx.FollowerEntries)
    {
        // ... process each entry
    }
}`;

const BROKEN_SITE2 = `// Site 2 — V12_002.Orders.Callbacks.Propagation.cs : Line 126
// ❌ BROKEN — ctx.Sync has been removed
lock (ctx.Sync)
{
    var arr = ctx.FollowerEntries.ToArray();
}`;

const QUICK_FIX_PROPOSED = `// ⚠️  Suggested Quick-Fix (DO NOT USE)
// Replacing lock(ctx.Sync) with lock(ctx.FollowerEntries)

// Site 1
lock (ctx.FollowerEntries)                // ← locks on a public object
{
    foreach (var x in ctx.FollowerEntries) { ... }
}

// Site 2
lock (ctx.FollowerEntries)
{
    var arr = ctx.FollowerEntries.ToArray();
}`;

const FIXED_SITE1 = `// ✅ FIXED — Site 1 — V12_002.SIMA.Shadow.cs : Line 89
// ConcurrentDictionary<TKey,TValue> implements IEnumerable<KeyValuePair<TKey,TValue>>
// Its GetEnumerator() takes a point-in-time snapshot of the internal bucket array
// segments and yields pairs WITHOUT holding any external lock.
// This is lock-free and safe under concurrent mutation in .NET 4.8.

foreach (var x in ctx.FollowerEntries)   // no lock needed — CD is lock-free here
{
    // process x.Key / x.Value
}`;

const FIXED_SITE2 = `// ✅ FIXED — Site 2 — V12_002.Orders.Callbacks.Propagation.cs : Line 126
// For a true immutable snapshot use GetEnumerator manually into a pre-sized
// buffer, OR — if you genuinely need an array — accept one allocation:

// Option A (zero-extra-lock, minimal alloc — preferred for hot path):
// Iterate with GetEnumerator; do NOT call .ToArray() inside a lock.
using (var e = ctx.FollowerEntries.GetEnumerator())
{
    while (e.MoveNext())
    {
        var pair = e.Current;   // KeyValuePair<string, byte>
        // process pair
    }
}

// Option B (snapshot needed, single allocation, no lock):
// ConcurrentDictionary<K,V>.ToArray() is already internally safe in .NET 4+.
// The lock(obj).ToArray() pattern ADDS a lock AROUND a method that takes its
// OWN internal locks — causing unnecessary contention.
var snapshot = ctx.FollowerEntries.ToArray();  // thread-safe without outer lock`;

const OPTIMIZED_HOT_PATH = `// 🚀 Sovereign Invariant — Zero-Lock Hot Path (.NET 4.8)
// For maximum throughput on a HFT order-flow path:

// 1. Prefer direct enumeration — no lock, no heap allocation beyond the
//    enumerator struct itself (ValueType enumerator in newer builds):
foreach (KeyValuePair<string, byte> entry in ctx.FollowerEntries)
{
    ProcessFollower(entry.Key, entry.Value);
}

// 2. If you need a snapshot (e.g., to pass across an async boundary) and
//    the dictionary is stable in size, pre-allocate a reusable buffer:
private static KeyValuePair<string,byte>[] _scratchBuffer
    = new KeyValuePair<string,byte>[256]; // tune to expected max followers

// then fill without any lock:
int count = 0;
foreach (var kv in ctx.FollowerEntries)
    _scratchBuffer[count++] = kv;
// use _scratchBuffer[0..count-1]

// WHY: ConcurrentDictionary's internal enumerator uses volatile reads over
// segments. No external lock is taken. Consistent with the
// "Sovereign Invariant — No-Internal-Locks" contract.`;

// ─── Main App ─────────────────────────────────────────────────────────────────

export default function App() {
  const [activeTab, setActiveTab] = useState<"site1" | "site2">("site1");

  return (
    <div className="min-h-screen bg-slate-950 text-slate-100 font-sans">
      {/* ── Header ── */}
      <header className="sticky top-0 z-50 border-b border-slate-800 bg-slate-950/90 backdrop-blur-md">
        <div className="max-w-7xl mx-auto px-6 py-4 flex flex-col sm:flex-row sm:items-center sm:justify-between gap-3">
          <div className="flex items-center gap-3">
            <div className="flex items-center justify-center w-9 h-9 rounded-xl bg-gradient-to-br from-violet-600 to-indigo-700 shadow-lg shadow-indigo-900/50">
              <IconCpu />
            </div>
            <div>
              <h1 className="text-base font-bold tracking-tight text-white leading-none">
                System Flow Visualizer
              </h1>
              <p className="text-xs text-slate-500 leading-none mt-0.5">Sovereign Substrate Refactor · Lock-Free Analysis</p>
            </div>
          </div>
          {/* ── Model Badge ── */}
          <div className="flex items-center gap-2">
            <Badge severity="info" label="Analysis Engine" />
            <h2 className="text-sm font-bold text-violet-300 bg-violet-950/60 border border-violet-700/50 px-3 py-1 rounded-full tracking-wide">
              Claude claude-opus-4-5
            </h2>
          </div>
        </div>
      </header>

      <main className="max-w-7xl mx-auto px-6 py-8 space-y-8">

        {/* ── Context Banner ── */}
        <div className="rounded-xl border border-slate-700/50 bg-slate-900/60 px-6 py-4 flex flex-col sm:flex-row sm:items-center gap-4">
          <div className="flex-1">
            <p className="text-xs font-bold uppercase tracking-widest text-slate-500 mb-1">Refactor Context</p>
            <p className="text-sm text-slate-300">
              The <span className="text-violet-300 font-mono font-semibold">ctx.Sync</span> lock object has been
              removed as part of the <span className="text-amber-300 font-semibold">Sovereign Substrate</span> refactor.
              Two call sites must be updated to be <strong className="text-white">lock-free</strong> and thread-safe
              under the <strong className="text-emerald-300">No-Internal-Locks</strong> invariant.
            </p>
          </div>
          <div className="flex flex-col gap-1.5 shrink-0">
            <Badge severity="critical" label="ctx.Sync removed" />
            <Badge severity="warning" label="2 sites broken" />
            <Badge severity="ok"       label="ConcurrentDictionary safe" />
          </div>
        </div>

        {/* ── Row 1: Behavioral Extraction ── */}
        <Card
          title="Behavioral Extraction — Quick-Fix Verdict"
          icon={<IconShield />}
          accent="from-red-950/60 to-slate-900"
        >
          <div className="flex flex-wrap items-center gap-2 mb-4">
            <Badge severity="critical" label="INVARIANT VIOLATED" />
            <span className="text-xs text-slate-400">Suggested Quick-Fix is rejected</span>
          </div>

          <CodeBlock
            code={QUICK_FIX_PROPOSED}
            label="Proposed Quick-Fix — lock(ctx.FollowerEntries)"
            variant="broken"
          />

          <Divider />
          <SectionLabel text="Why it fails the Sovereign Invariant" />

          <div className="space-y-3">
            {[
              {
                n: "1",
                title: "Locks a public, externally-visible object",
                body: "ctx.FollowerEntries is a ConcurrentDictionary<string,byte> exposed through the context. Locking on any object that external callers can reach creates a deadlock vector — any other thread that also decides to lock on the same reference (even accidentally, e.g. a LINQ provider or serializer) will block the gate. The Sovereign Invariant explicitly forbids this: 'No-Internal-Locks' means no monitor-style locks anywhere in the hot path.",
              },
              {
                n: "2",
                title: "The lock is completely redundant for ConcurrentDictionary",
                body: "ConcurrentDictionary<TKey,TValue> in .NET 4.5+ (and 4.8) is designed to be enumerated and snapshotted WITHOUT any external lock. Its GetEnumerator() implementation uses per-segment volatile reads and does not require an outer monitor. Adding lock(obj) around ConcurrentDictionary operations does not make them safer — it only serializes access and reduces throughput while providing a false sense of safety.",
              },
              {
                n: "3",
                title: "Defeats the entire purpose of the Sovereign Substrate refactor",
                body: "The removal of ctx.Sync was a deliberate architectural decision to move the system toward a fully lock-free, obstruction-free concurrency model. Replacing one lock object with another (even temporarily) is a regression, not a fix. It reintroduces blocking, priority inversion, and the very contention the refactor was designed to eliminate.",
              },
              {
                n: "4",
                title: "No-Internal-Locks invariant — precise definition",
                body: "The invariant states that no synchronization primitive (Monitor, Mutex, SemaphoreSlim in blocking mode, SpinLock in spin mode) may appear inside a gate that processes FollowerEntries on the critical path. lock(ctx.FollowerEntries) is a Monitor.Enter call — it is a direct violation by definition.",
              },
            ].map(({ n, title, body }) => (
              <div key={n} className="flex gap-3 rounded-lg bg-red-950/20 border border-red-900/40 p-3">
                <span className="flex-shrink-0 w-6 h-6 rounded-full bg-red-700 text-white text-xs font-bold flex items-center justify-center mt-0.5">
                  {n}
                </span>
                <div>
                  <p className="text-sm font-semibold text-red-200">{title}</p>
                  <p className="text-xs text-slate-400 mt-1 leading-relaxed">{body}</p>
                </div>
              </div>
            ))}
          </div>

          <div className="mt-4 rounded-lg bg-amber-950/30 border border-amber-700/50 p-3 flex gap-2">
            <span className="text-amber-400 mt-0.5"><IconInfo /></span>
            <p className="text-xs text-amber-200 leading-relaxed">
              <strong>Verdict:</strong> The suggested quick-fix does <em>not</em> satisfy the Sovereign Invariant.
              It replaces one illegal lock with another and introduces additional deadlock surface.
              It must be rejected. See the Logic Matrix below for the correct approach.
            </p>
          </div>
        </Card>

        {/* ── Row 2: Logic Matrix ── */}
        <Card
          title="Logic Matrix — Broken Code & Correct Fix"
          icon={<IconMatrix />}
          accent="from-slate-900 to-indigo-950/40"
        >
          {/* Tab switcher */}
          <div className="flex gap-2 mb-4">
            {(["site1", "site2"] as const).map((s) => (
              <button
                key={s}
                onClick={() => setActiveTab(s)}
                className={`px-4 py-1.5 rounded-lg text-xs font-semibold transition-all ${
                  activeTab === s
                    ? "bg-indigo-600 text-white shadow shadow-indigo-900"
                    : "bg-slate-800 text-slate-400 hover:text-slate-200"
                }`}
              >
                {s === "site1"
                  ? "Site 1 · SIMA.Shadow.cs:89"
                  : "Site 2 · Callbacks.Propagation.cs:126"}
              </button>
            ))}
          </div>

          {activeTab === "site1" && (
            <div className="space-y-1">
              <div className="flex flex-wrap gap-2 mb-3">
                <Badge severity="critical" label="V12_002.SIMA.Shadow.cs" />
                <Badge severity="info" label="Line 89" />
                <Badge severity="warning" label="foreach iteration" />
              </div>
              <CodeBlock code={BROKEN_SITE1} label="❌ Broken (pre-refactor)" variant="broken" />
              <CodeBlock code={FIXED_SITE1}  label="✅ Correct lock-free fix" variant="fixed" />
              <div className="rounded-lg bg-indigo-950/30 border border-indigo-700/40 p-3 mt-3">
                <p className="text-xs text-indigo-200 leading-relaxed">
                  <strong>Rationale:</strong> <span className="font-mono text-indigo-300">ConcurrentDictionary&lt;K,V&gt;.GetEnumerator()</span> in
                  .NET 4.8 is implemented via an internal <span className="font-mono text-indigo-300">DictionaryEnumerator</span> that captures
                  a consistent view of each segment using volatile reads. The <span className="font-mono text-indigo-300">foreach</span> over the
                  dictionary does not require any external lock. It may observe concurrent additions/removals (it
                  provides <em>weakly consistent</em> iteration semantics — the same guarantee given by
                  <span className="font-mono text-indigo-300"> java.util.concurrent</span> maps), which is exactly what
                  the Sovereign model requires. Remove the lock entirely.
                </p>
              </div>
            </div>
          )}

          {activeTab === "site2" && (
            <div className="space-y-1">
              <div className="flex flex-wrap gap-2 mb-3">
                <Badge severity="critical" label="V12_002.Orders.Callbacks.Propagation.cs" />
                <Badge severity="info" label="Line 126" />
                <Badge severity="warning" label="snapshot / ToArray" />
              </div>
              <CodeBlock code={BROKEN_SITE2} label="❌ Broken (pre-refactor)" variant="broken" />
              <CodeBlock code={FIXED_SITE2}  label="✅ Correct lock-free fix" variant="fixed" />
              <div className="rounded-lg bg-indigo-950/30 border border-indigo-700/40 p-3 mt-3">
                <p className="text-xs text-indigo-200 leading-relaxed">
                  <strong>Rationale:</strong> <span className="font-mono text-indigo-300">ConcurrentDictionary.ToArray()</span> in .NET 4.8
                  acquires all internal segment locks briefly and atomically to produce a snapshot — this is handled
                  entirely inside the BCL implementation. Wrapping it in an additional external lock
                  causes <em>double-locking</em> and unnecessary contention. For the hot path, prefer direct
                  <span className="font-mono text-indigo-300"> GetEnumerator()</span> streaming (Option A) over snapshot
                  allocation (Option B) unless downstream consumers require a stable array reference.
                </p>
              </div>
            </div>
          )}
        </Card>

        {/* ── Row 3: Optimized Hot Path ── */}
        <Card
          title="Performance-Optimized Lock-Free Pattern · .NET 4.8"
          icon={<IconCode />}
          accent="from-emerald-950/40 to-slate-900"
        >
          <div className="flex flex-wrap gap-2 mb-3">
            <Badge severity="ok"   label="Zero external locks" />
            <Badge severity="ok"   label="Volatile reads only" />
            <Badge severity="info" label="Weakly-consistent iteration" />
          </div>
          <CodeBlock code={OPTIMIZED_HOT_PATH} label="🚀 Recommended sovereign pattern" variant="fixed" />
        </Card>

        {/* ── Row 4: Comparative Analysis ── */}
        <Card
          title="Comparative Analysis — Hot-Path Pattern Selection"
          icon={<IconChart />}
          accent="from-slate-900 to-violet-950/30"
        >
          <p className="text-xs text-slate-400 leading-relaxed mb-5">
            Why direct <span className="font-mono text-violet-300">foreach</span> enumeration over
            <span className="font-mono text-violet-300"> ConcurrentDictionary</span> is superior to
            <span className="font-mono text-violet-300"> .Keys</span> or
            <span className="font-mono text-violet-300"> .ToArray()</span> on a high-frequency trading hot path:
          </p>

          {/* Comparison table */}
          <div className="overflow-x-auto rounded-xl border border-slate-700/50">
            <table className="w-full text-xs">
              <thead>
                <tr className="bg-slate-800/60 text-slate-400 uppercase tracking-wider">
                  <th className="text-left px-4 py-3 font-semibold">Pattern</th>
                  <th className="text-left px-4 py-3 font-semibold">Allocations</th>
                  <th className="text-left px-4 py-3 font-semibold">Lock Cost</th>
                  <th className="text-left px-4 py-3 font-semibold">GC Pressure</th>
                  <th className="text-left px-4 py-3 font-semibold">Invariant</th>
                  <th className="text-left px-4 py-3 font-semibold">Verdict</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-800">
                {[
                  {
                    pattern: "lock(obj) + foreach",
                    alloc: "Enumerator object",
                    lock: "Monitor (kernel transition)",
                    gc: "Low-Med",
                    inv: "❌ Violated",
                    verdict: "Reject",
                    vColor: "text-red-400",
                  },
                  {
                    pattern: "lock(obj) + .ToArray()",
                    alloc: "KVP[] heap array",
                    lock: "Monitor + internal seg locks",
                    gc: "High",
                    inv: "❌ Violated",
                    verdict: "Reject",
                    vColor: "text-red-400",
                  },
                  {
                    pattern: ".Keys (ICollection)",
                    alloc: "New List<string> on access",
                    lock: "Internal seg locks (snapshot)",
                    gc: "High",
                    inv: "❌ Violated",
                    verdict: "Reject",
                    vColor: "text-red-400",
                  },
                  {
                    pattern: ".ToArray() (no outer lock)",
                    alloc: "KVP[] heap array",
                    lock: "Internal seg locks only",
                    gc: "Med",
                    inv: "✅ OK (no external lock)",
                    verdict: "Acceptable",
                    vColor: "text-amber-400",
                  },
                  {
                    pattern: "foreach (direct CD enum)",
                    alloc: "Enumerator struct (stack)",
                    lock: "Volatile reads only",
                    gc: "Near-zero",
                    inv: "✅ Sovereign",
                    verdict: "Preferred ★",
                    vColor: "text-emerald-400",
                  },
                  {
                    pattern: "GetEnumerator + reuse buffer",
                    alloc: "Zero (reused scratch)",
                    lock: "Volatile reads only",
                    gc: "Zero",
                    inv: "✅ Sovereign",
                    verdict: "Optimal ★★",
                    vColor: "text-emerald-300",
                  },
                ].map((row, i) => (
                  <tr
                    key={i}
                    className={`transition-colors ${
                      row.verdict.startsWith("Reject")
                        ? "bg-red-950/10"
                        : row.verdict.startsWith("Acceptable")
                        ? "bg-amber-950/10"
                        : "bg-emerald-950/10"
                    }`}
                  >
                    <td className="px-4 py-2.5 font-mono text-slate-200">{row.pattern}</td>
                    <td className="px-4 py-2.5 text-slate-400">{row.alloc}</td>
                    <td className="px-4 py-2.5 text-slate-400">{row.lock}</td>
                    <td className="px-4 py-2.5 text-slate-400">{row.gc}</td>
                    <td className="px-4 py-2.5 text-slate-300">{row.inv}</td>
                    <td className={`px-4 py-2.5 font-bold ${row.vColor}`}>{row.verdict}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          <Divider />
          <SectionLabel text="Deep-dive: why .Keys is especially dangerous" />

          <div className="grid sm:grid-cols-2 gap-3">
            {[
              {
                title: "`.Keys` materialises a full copy",
                body: "Accessing the `.Keys` property on a ConcurrentDictionary in .NET 4.8 acquires all internal segment locks and copies every key into a new List<TKey> on the heap. For a dictionary with N entries this is O(N) allocation on every call. On a 1 µs HFT tick this triggers the GC in seconds.",
              },
              {
                title: "`.ToArray()` is better but still allocates",
                body: "`.ToArray()` at least gives you KeyValuePair<K,V> so you have both key and value without a second lookup. However it still allocates an array per call. Without an outer lock it IS invariant-compliant, but the allocation cost makes it inferior to direct enumeration in steady state.",
              },
              {
                title: "Direct `foreach` — weakly consistent, zero extra alloc",
                body: "The `foreach` over a ConcurrentDictionary calls `GetEnumerator()` which walks internal segments with volatile reads. No heap allocation beyond the iterator struct. In .NET 4.8 the enumerator is a class (small GC object), but it is short-lived and quickly collected. No external lock is held.",
              },
              {
                title: "Reusable scratch buffer — the sovereign optimum",
                body: "For absolute zero GC pressure on the hot path, maintain a static or thread-local KeyValuePair[] buffer and fill it via GetEnumerator(). This eliminates all per-call allocation. Pair this with Span<T> slices (available via System.Memory NuGet on .NET 4.8) to pass the slice downstream without copying.",
              },
            ].map(({ title, body }) => (
              <div key={title} className="rounded-lg bg-slate-800/40 border border-slate-700/40 p-3">
                <p className="text-xs font-semibold text-violet-300 font-mono mb-1">{title}</p>
                <p className="text-xs text-slate-400 leading-relaxed">{body}</p>
              </div>
            ))}
          </div>
        </Card>

        {/* ── Row 5: Summary ── */}
        <Card
          title="Sovereign Substrate — Final Disposition"
          icon={<IconShield />}
          accent="from-slate-900 to-slate-800/80"
        >
          <div className="grid sm:grid-cols-3 gap-4">
            {[
              {
                site: "Quick-Fix Verdict",
                status: "REJECTED",
                color: "border-red-700 bg-red-950/30",
                badgeColor: "critical" as Severity,
                detail: "lock(ctx.FollowerEntries) violates the No-Internal-Locks invariant. Locks a public object, redundant over ConcurrentDictionary, reintroduces blocking on the critical path.",
              },
              {
                site: "Site 1 Fix",
                status: "APPROVED",
                color: "border-emerald-700 bg-emerald-950/20",
                badgeColor: "ok" as Severity,
                detail: "Remove lock entirely. Use plain foreach over ctx.FollowerEntries. ConcurrentDictionary provides safe iteration via volatile segment reads. No external synchronisation required.",
              },
              {
                site: "Site 2 Fix",
                status: "APPROVED",
                color: "border-emerald-700 bg-emerald-950/20",
                badgeColor: "ok" as Severity,
                detail: "Remove lock entirely. For streaming: foreach/GetEnumerator. For snapshot: ctx.FollowerEntries.ToArray() without any outer lock — BCL handles internal consistency.",
              },
            ].map(({ site, status, color, badgeColor, detail }) => (
              <div key={site} className={`rounded-xl border p-4 ${color}`}>
                <p className="text-xs font-bold uppercase tracking-widest text-slate-400 mb-2">{site}</p>
                <Badge severity={badgeColor} label={status} />
                <p className="text-xs text-slate-400 mt-3 leading-relaxed">{detail}</p>
              </div>
            ))}
          </div>

          <div className="mt-5 rounded-xl bg-gradient-to-r from-violet-950/60 to-indigo-950/60 border border-violet-700/40 p-4">
            <p className="text-xs font-bold uppercase tracking-widest text-violet-400 mb-2">
              Sovereign Invariant Compliance Summary
            </p>
            <p className="text-sm text-slate-300 leading-relaxed">
              <span className="font-mono text-violet-300">ConcurrentDictionary&lt;string, byte&gt;</span> in .NET 4.8 is an intrinsically
              thread-safe collection. Both iteration and snapshot operations are defined by the BCL to be safe for concurrent
              read/write access <strong className="text-white">without any external locking</strong>. The Sovereign Substrate refactor's
              removal of <span className="font-mono text-amber-300">ctx.Sync</span> is therefore <em>correct</em> and the required
              fix at both sites is simply: <strong className="text-emerald-300">delete the lock statement</strong>.
              No replacement lock object is needed. No new synchronisation primitive is needed.
              The No-Internal-Locks invariant is fully satisfied by the fixes above.
            </p>
          </div>
        </Card>

      </main>

      {/* ── Footer ── */}
      <footer className="border-t border-slate-800 mt-10 py-6 text-center">
        <p className="text-xs text-slate-600">
          System Flow Visualizer · Sovereign Substrate Refactor Analysis ·{" "}
          <span className="text-violet-500 font-semibold">Claude claude-opus-4-5</span>
        </p>
      </footer>
    </div>
  );
}
