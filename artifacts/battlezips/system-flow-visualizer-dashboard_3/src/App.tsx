import { useState } from "react";

// ─── Types ───────────────────────────────────────────────────────────────────
interface CodeBlockProps {
  label: string;
  code: string;
  language?: string;
  variant?: "broken" | "fixed" | "neutral";
}

interface AnalysisCardProps {
  title: string;
  icon: string;
  children: React.ReactNode;
  variant?: "default" | "warning" | "danger" | "success";
}

interface SiteInfo {
  file: string;
  line: number;
  target: string;
  brokenCode: string;
  fixedCode: string;
  explanation: string;
}

// ─── Data ────────────────────────────────────────────────────────────────────
const MODEL_NAME = "Claude 3.5 Sonnet";
const MODEL_VERSION = "v2024.10.22";

const sites: SiteInfo[] = [
  {
    file: "V12_002.SIMA.Shadow.cs",
    line: 89,
    target: "Iterate over ctx.FollowerEntries (ConcurrentDictionary<string, byte>)",
    brokenCode:
      'lock (ctx.Sync)\n{\n    foreach (var x in ctx.FollowerEntries)\n    {\n        // process entry\n    }\n}',
    fixedCode:
      '// Lock-free: ConcurrentDictionary.GetEnumerator()\n// returns a snapshot-consistent struct enumerator.\nforeach (var x in ctx.FollowerEntries)\n{\n    // process entry — zero alloc, no lock\n}',
    explanation:
      "ConcurrentDictionary.GetEnumerator() returns a struct enumerator that captures a point-in-time snapshot at construction. No heap allocation. No lock. The enumerator is thread-safe by design — it never throws even if the dictionary is mutated concurrently.",
  },
  {
    file: "V12_002.Orders.Callbacks.Propagation.cs",
    line: 126,
    target: "Snapshot of ctx.FollowerEntries",
    brokenCode:
      'lock (ctx.Sync)\n{\n    var arr = ctx.FollowerEntries.ToArray();\n}',
    fixedCode:
      '// Lock-free: ToArray() on ConcurrentDictionary\n// is already thread-safe and snapshot-consistent.\nvar arr = ctx.FollowerEntries.ToArray();\n\n// ZERO-ALLOC ALTERNATIVE for hot paths:\n// Restructure to process inline via foreach,\n// or use ArrayPool<T>.Shared for reusable buffers.',
    explanation:
      "ConcurrentDictionary.ToArray() internally acquires the dictionary's own fine-grained locks (one per bucket) and copies entries into a new array. This is already thread-safe — wrapping it in an external lock is redundant and harmful. For zero-allocation, restructure to process entries inline or pool arrays.",
  },
];

// ─── Utility Components ──────────────────────────────────────────────────────
function CodeBlock({ label, code, variant = "neutral" }: CodeBlockProps) {
  const borderMap = {
    broken: "border-red-500/60",
    fixed: "border-emerald-500/60",
    neutral: "border-slate-600/60",
  };
  const bgMap = {
    broken: "bg-red-950/40",
    fixed: "bg-emerald-950/40",
    neutral: "bg-slate-900/60",
  };
  const labelMap = {
    broken: "text-red-400",
    fixed: "text-emerald-400",
    neutral: "text-slate-400",
  };

  return (
    <div className={`rounded-lg border ${borderMap[variant]} ${bgMap[variant]} overflow-hidden`}>
      <div className={`px-3 py-1.5 text-xs font-mono font-semibold ${labelMap[variant]} border-b ${borderMap[variant]}`}>
        {label}
      </div>
      <pre className="p-3 text-xs sm:text-sm font-mono text-slate-200 overflow-x-auto leading-relaxed">
        <code>{code}</code>
      </pre>
    </div>
  );
}

function AnalysisCard({ title, icon, children, variant = "default" }: AnalysisCardProps) {
  const borderMap = {
    default: "border-slate-700/60",
    warning: "border-amber-500/50",
    danger: "border-red-500/50",
    success: "border-emerald-500/50",
  };
  const headerBgMap = {
    default: "bg-slate-800/60",
    warning: "bg-amber-950/40",
    danger: "bg-red-950/40",
    success: "bg-emerald-950/40",
  };

  return (
    <div className={`rounded-xl border ${borderMap[variant]} bg-slate-900/80 backdrop-blur-sm overflow-hidden shadow-lg shadow-black/20`}>
      <div className={`px-5 py-3 ${headerBgMap[variant]} border-b ${borderMap[variant]} flex items-center gap-2.5`}>
        <span className="text-lg">{icon}</span>
        <h3 className="text-sm font-bold uppercase tracking-wider text-slate-100">{title}</h3>
      </div>
      <div className="p-5 space-y-4">{children}</div>
    </div>
  );
}

function Badge({ children, variant = "default" }: { children: React.ReactNode; variant?: "default" | "danger" | "success" | "warning" }) {
  const map = {
    default: "bg-slate-700 text-slate-300",
    danger: "bg-red-900/60 text-red-300 ring-1 ring-red-700/50",
    success: "bg-emerald-900/60 text-emerald-300 ring-1 ring-emerald-700/50",
    warning: "bg-amber-900/60 text-amber-300 ring-1 ring-amber-700/50",
  };
  return (
    <span className={`inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-semibold ${map[variant]}`}>
      {children}
    </span>
  );
}

function CheckIcon() {
  return (
    <svg className="w-4 h-4 text-emerald-400 inline mr-1.5 -mt-0.5" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2.5}>
      <path strokeLinecap="round" strokeLinejoin="round" d="M5 13l4 4L19 7" />
    </svg>
  );
}

function XIcon() {
  return (
    <svg className="w-4 h-4 text-red-400 inline mr-1.5 -mt-0.5" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2.5}>
      <path strokeLinecap="round" strokeLinejoin="round" d="M6 18L18 6M6 6l12 12" />
    </svg>
  );
}

// ─── Flow Diagram ────────────────────────────────────────────────────────────
function FlowDiagram() {
  const [activeNode, setActiveNode] = useState<string | null>(null);

  const nodes = [
    { id: "entry", label: "Entry Point", desc: "Request arrives at SIMA layer", color: "bg-blue-600" },
    { id: "shadow", label: "Shadow Gate", desc: "V12_002.SIMA.Shadow.cs:89 — Follower iteration", color: "bg-amber-600" },
    { id: "prop", label: "Propagation Gate", desc: "V12_002.Orders.Callbacks:126 — Snapshot", color: "bg-purple-600" },
    { id: "exit", label: "Exit", desc: "Lock-free, thread-safe completion", color: "bg-emerald-600" },
  ];

  return (
    <div className="rounded-xl border border-slate-700/60 bg-slate-900/80 backdrop-blur-sm overflow-hidden shadow-lg shadow-black/20">
      <div className="px-5 py-3 bg-slate-800/60 border-b border-slate-700/60 flex items-center gap-2.5">
        <span className="text-lg">🔀</span>
        <h3 className="text-sm font-bold uppercase tracking-wider text-slate-100">Logic Flow Map</h3>
      </div>
      <div className="p-5">
        <div className="flex flex-col sm:flex-row items-center justify-center gap-2 sm:gap-0">
          {nodes.map((node, i) => (
            <div key={node.id} className="flex items-center">
              <button
                onClick={() => setActiveNode(activeNode === node.id ? null : node.id)}
                className={`relative group flex flex-col items-center px-4 py-3 rounded-lg ${node.color} bg-opacity-20 border border-slate-600/40 hover:border-slate-400/60 transition-all cursor-pointer min-w-[120px]`}
              >
                <div className={`w-3 h-3 rounded-full ${node.color} mb-2 ring-2 ring-offset-2 ring-offset-slate-900 ${node.color.replace("bg-", "ring-")}`} />
                <span className="text-xs font-bold text-slate-100">{node.label}</span>
                {activeNode === node.id && (
                  <span className="mt-1 text-[10px] text-slate-400 text-center leading-tight">{node.desc}</span>
                )}
              </button>
              {i < nodes.length - 1 && (
                <div className="hidden sm:flex items-center mx-1">
                  <svg className="w-8 h-4 text-slate-600" viewBox="0 0 32 16" fill="none">
                    <path d="M0 8h28m0 0l-6-5m6 5l-6 5" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" />
                  </svg>
                </div>
              )}
              {i < nodes.length - 1 && (
                <div className="sm:hidden flex items-center my-1">
                  <svg className="w-4 h-6 text-slate-600" viewBox="0 0 16 24" fill="none">
                    <path d="M8 0v20m0 0l-5-5m5 5l5-5" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" />
                  </svg>
                </div>
              )}
            </div>
          ))}
        </div>

        {/* Lock status indicators */}
        <div className="mt-6 grid grid-cols-1 sm:grid-cols-2 gap-3">
          <div className="flex items-center gap-2 px-3 py-2 rounded-lg bg-red-950/30 border border-red-800/40">
            <XIcon />
            <span className="text-xs text-red-300">
              <strong>BEFORE:</strong> lock(ctx.Sync) — BLOCKS all threads
            </span>
          </div>
          <div className="flex items-center gap-2 px-3 py-2 rounded-lg bg-emerald-950/30 border border-emerald-800/40">
            <CheckIcon />
            <span className="text-xs text-emerald-300">
              <strong>AFTER:</strong> Lock-free — ConcurrentDictionary handles sync
            </span>
          </div>
        </div>
      </div>
    </div>
  );
}

// ─── Main App ────────────────────────────────────────────────────────────────
export default function App() {
  const [activeTab, setActiveTab] = useState<"behavioral" | "matrix" | "comparative">("behavioral");

  const tabs = [
    { id: "behavioral" as const, label: "1. Behavioral Extraction", icon: "🔍" },
    { id: "matrix" as const, label: "2. Logic Matrix", icon: "⚡" },
    { id: "comparative" as const, label: "3. Comparative Analysis", icon: "📊" },
  ];

  return (
    <div className="min-h-screen bg-gradient-to-br from-slate-950 via-slate-900 to-slate-950 text-slate-200">
      {/* Background grid pattern */}
      <div className="fixed inset-0 opacity-[0.03]" style={{
        backgroundImage: `linear-gradient(rgba(255,255,255,0.1) 1px, transparent 1px), linear-gradient(90deg, rgba(255,255,255,0.1) 1px, transparent 1px)`,
        backgroundSize: "40px 40px"
      }} />

      <div className="relative z-10 max-w-6xl mx-auto px-4 py-6 sm:py-10">
        {/* Header */}
        <header className="text-center mb-8 sm:mb-12">
          <div className="inline-flex items-center gap-2 px-3 py-1 rounded-full bg-blue-900/30 border border-blue-700/40 text-blue-300 text-xs font-mono mb-4">
            <span className="w-2 h-2 rounded-full bg-blue-400 animate-pulse" />
            SOVEREIGN SUBSTRATE REFACTOR — ACTIVE
          </div>
          <h2 className="text-2xl sm:text-4xl font-extrabold text-white mb-2 tracking-tight">
            System Flow Visualizer
          </h2>
          <p className="text-lg sm:text-xl font-semibold bg-gradient-to-r from-blue-400 via-purple-400 to-emerald-400 bg-clip-text text-transparent">
            {MODEL_NAME} — {MODEL_VERSION}
          </p>
          <p className="text-sm text-slate-500 mt-2 font-mono">
            ctx.Sync Removal · Lock-Free Migration · .NET 4.8 Hot Path Optimization
          </p>
        </header>

        {/* Tab Navigation */}
        <nav className="flex flex-wrap justify-center gap-2 mb-8">
          {tabs.map((tab) => (
            <button
              key={tab.id}
              onClick={() => setActiveTab(tab.id)}
              className={`px-4 py-2.5 rounded-lg text-sm font-semibold transition-all ${
                activeTab === tab.id
                  ? "bg-blue-600 text-white shadow-lg shadow-blue-600/30"
                  : "bg-slate-800/60 text-slate-400 hover:text-slate-200 hover:bg-slate-700/60 border border-slate-700/40"
              }`}
            >
              <span className="mr-1.5">{tab.icon}</span>
              {tab.label}
            </button>
          ))}
        </nav>

        {/* Flow Diagram */}
        <div className="mb-8">
          <FlowDiagram />
        </div>

        {/* Tab Content */}
        {activeTab === "behavioral" && (
          <div className="space-y-6 animate-in">
            {/* Invariant Violation Analysis */}
            <AnalysisCard title="Suggested Quick-Fix Analysis" icon="⚠️" variant="danger">
              <div className="space-y-4">
                <div className="flex items-start gap-3">
                  <Badge variant="danger">INVARIANT VIOLATION</Badge>
                </div>

                <p className="text-sm text-slate-300 leading-relaxed">
                  The suggested quick-fix proposes replacing <code className="px-1.5 py-0.5 bg-slate-800 rounded text-amber-300 text-xs font-mono">lock(ctx.Sync)</code> with{" "}
                  <code className="px-1.5 py-0.5 bg-slate-800 rounded text-amber-300 text-xs font-mono">lock(ctx.FollowerEntries)</code> as a 1-line substitution.
                </p>

                <div className="rounded-lg bg-red-950/40 border border-red-800/40 p-4">
                  <h4 className="text-sm font-bold text-red-300 mb-2">
                    <XIcon />
                    This <strong>DOES violate</strong> the Sovereign Invariant (No-Internal-Locks)
                  </h4>
                  <ul className="space-y-2 text-sm text-slate-300">
                    <li className="flex items-start">
                      <XIcon />
                      <span><strong>Redundant Locking:</strong> ConcurrentDictionary&lt;K,V&gt; already implements fine-grained internal locking (per-bucket spin locks in .NET 4.8). Adding an external lock on the same object creates lock nesting with no benefit.</span>
                    </li>
                    <li className="flex items-start">
                      <XIcon />
                      <span><strong>Deadlock Risk:</strong> Locking on the ConcurrentDictionary itself means any internal operation that acquires a bucket lock while the external lock is held creates a potential deadlock if another thread enters via a different code path.</span>
                    </li>
                    <li className="flex items-start">
                      <XIcon />
                      <span><strong>Contention Amplification:</strong> All threads now contend on a single lock object (the dictionary reference) instead of the dictionary's internal striped lock design. This serializes all access and destroys the concurrent throughput.</span>
                    </li>
                    <li className="flex items-start">
                      <XIcon />
                      <span><strong>Sovereign Invariant Breach:</strong> The "No-Internal-Locks" rule explicitly forbids locking on collection objects that manage their own synchronization. The fix introduces exactly what the refactor aims to eliminate.</span>
                    </li>
                    <li className="flex items-start">
                      <XIcon />
                      <span><strong>Defeats Purpose:</strong> If you're going to lock externally, there's no reason to use ConcurrentDictionary over a plain Dictionary. The entire refactor to remove ctx.Sync is undermined.</span>
                    </li>
                  </ul>
                </div>

                <div className="rounded-lg bg-amber-950/30 border border-amber-800/40 p-4">
                  <h4 className="text-sm font-bold text-amber-300 mb-2">
                    ⚡ Correct Approach
                  </h4>
                  <p className="text-sm text-slate-300 leading-relaxed">
                    <strong>Remove the lock entirely.</strong> ConcurrentDictionary is designed to be used without external synchronization for enumeration and snapshot operations. Its enumerator provides snapshot consistency, and ToArray() is already thread-safe. The lock was only needed because the old code used a non-thread-safe collection protected by ctx.Sync.
                  </p>
                </div>
              </div>
            </AnalysisCard>
          </div>
        )}

        {activeTab === "matrix" && (
          <div className="space-y-6">
            {sites.map((site, idx) => (
              <AnalysisCard
                key={idx}
                title={`Site ${idx + 1}: ${site.file.split(".")[0]}`}
                icon={idx === 0 ? "🔧" : "📋"}
                variant="success"
              >
                <div className="space-y-4">
                  {/* Site metadata */}
                  <div className="flex flex-wrap gap-2">
                    <Badge variant="default">📁 {site.file}</Badge>
                    <Badge variant="default">📍 Line {site.line}</Badge>
                    <Badge variant="success">✅ Lock-Free</Badge>
                  </div>

                  <p className="text-sm text-slate-400">
                    <strong className="text-slate-200">Target:</strong> {site.target}
                  </p>

                  {/* Before / After */}
                  <div className="grid grid-cols-1 lg:grid-cols-2 gap-4">
                    <CodeBlock label="❌ BROKEN (lock-based)" code={site.brokenCode} variant="broken" />
                    <CodeBlock label="✅ FIXED (lock-free)" code={site.fixedCode} variant="fixed" />
                  </div>

                  {/* Explanation */}
                  <div className="rounded-lg bg-slate-800/40 border border-slate-700/40 p-4">
                    <h4 className="text-xs font-bold text-slate-400 uppercase tracking-wider mb-2">Technical Explanation</h4>
                    <p className="text-sm text-slate-300 leading-relaxed">{site.explanation}</p>
                  </div>

                  {/* Performance characteristics */}
                  <div className="grid grid-cols-2 sm:grid-cols-4 gap-3">
                    {[
                      { label: "Lock Contention", value: "ZERO", color: "text-emerald-400" },
                      { label: "Heap Allocation", value: idx === 0 ? "ZERO" : "1× Array", color: idx === 0 ? "text-emerald-400" : "text-amber-400" },
                      { label: "Thread Safety", value: "Native", color: "text-emerald-400" },
                      { label: "Snapshot", value: "Consistent", color: "text-blue-400" },
                    ].map((stat) => (
                      <div key={stat.label} className="text-center p-3 rounded-lg bg-slate-800/40 border border-slate-700/30">
                        <div className={`text-lg font-bold ${stat.color}`}>{stat.value}</div>
                        <div className="text-[10px] text-slate-500 uppercase tracking-wider mt-1">{stat.label}</div>
                      </div>
                    ))}
                  </div>
                </div>
              </AnalysisCard>
            ))}

            {/* Zero-Alloc Deep Dive */}
            <AnalysisCard title="Zero-Allocation Deep Dive — Site 2" icon="🎯" variant="warning">
              <div className="space-y-4">
                <p className="text-sm text-slate-300 leading-relaxed">
                  While <code className="px-1.5 py-0.5 bg-slate-800 rounded text-amber-300 text-xs font-mono">ToArray()</code> is lock-free, it <strong>does allocate</strong> a new array on each call. For a high-frequency trading hot path, here are the zero-allocation strategies:
                </p>

                <div className="grid grid-cols-1 lg:grid-cols-2 gap-4">
                  <CodeBlock
                    label="Strategy A: Inline Processing (Preferred)"
                    code={`// Process entries directly — no snapshot, no alloc\nforeach (var entry in ctx.FollowerEntries)\n{\n    PropagateCallback(entry.Key, entry.Value);\n}`}
                    variant="fixed"
                  />
                  <CodeBlock
                    label="Strategy B: ArrayPool Reuse"
                    code={`// Reuse a pooled buffer — amortized alloc\nvar pool = ArrayPool<KeyValuePair<string,byte>>.Shared;\nvar buffer = pool.Rent(ctx.FollowerEntries.Count);\ntry\n{\n    ctx.FollowerEntries.CopyTo(buffer, 0);\n    // process buffer[0..count]\n}\nfinally { pool.Return(buffer); }\n}`}
                    variant="neutral"
                  />
                </div>

                <div className="rounded-lg bg-blue-950/30 border border-blue-800/40 p-4">
                  <h4 className="text-sm font-bold text-blue-300 mb-2">📌 Recommendation</h4>
                  <p className="text-sm text-slate-300 leading-relaxed">
                    <strong>Strategy A (inline foreach)</strong> is the optimal choice for HFT hot paths. It's truly zero-allocation (struct enumerator), lock-free, and processes entries as they exist at enumeration start. The point-in-time snapshot semantics of ConcurrentDictionary's enumerator are sufficient for propagation logic — you don't need a materialized copy.
                  </p>
                </div>
              </div>
            </AnalysisCard>
          </div>
        )}

        {activeTab === "comparative" && (
          <div className="space-y-6">
            {/* Comparison Table */}
            <AnalysisCard title="Pattern Comparison Matrix" icon="📊" variant="default">
              <div className="overflow-x-auto">
                <table className="w-full text-sm">
                  <thead>
                    <tr className="border-b border-slate-700/60">
                      <th className="text-left py-3 px-3 text-xs font-bold text-slate-400 uppercase tracking-wider">Pattern</th>
                      <th className="text-center py-3 px-3 text-xs font-bold text-slate-400 uppercase tracking-wider">Lock-Free</th>
                      <th className="text-center py-3 px-3 text-xs font-bold text-slate-400 uppercase tracking-wider">Zero Alloc</th>
                      <th className="text-center py-3 px-3 text-xs font-bold text-slate-400 uppercase tracking-wider">Snapshot</th>
                      <th className="text-center py-3 px-3 text-xs font-bold text-slate-400 uppercase tracking-wider">HFT Safe</th>
                    </tr>
                  </thead>
                  <tbody>
                    {[
                      { name: "lock(ctx.Sync) + foreach", lock: "❌", alloc: "✅", snap: "✅", hft: "❌", best: false },
                      { name: "lock(ctx.FollowerEntries)", lock: "❌", alloc: "✅", snap: "⚠️", hft: "❌", best: false },
                      { name: ".Keys + lock", lock: "❌", alloc: "❌", snap: "⚠️", hft: "❌", best: false },
                      { name: ".ToArray() (no lock)", lock: "✅", alloc: "❌", snap: "✅", hft: "⚠️", best: false },
                      { name: "foreach (no lock) ★", lock: "✅", alloc: "✅", snap: "✅", hft: "✅", best: true },
                      { name: "ArrayPool + CopyTo", lock: "✅", alloc: "⚠️", snap: "✅", hft: "✅", best: false },
                    ].map((row) => (
                      <tr
                        key={row.name}
                        className={`border-b border-slate-800/40 ${
                          row.best ? "bg-emerald-950/30" : "hover:bg-slate-800/30"
                        }`}
                      >
                        <td className="py-2.5 px-3 font-mono text-xs">
                          {row.best && <span className="text-emerald-400 mr-1">★</span>}
                          {row.name}
                        </td>
                        <td className="py-2.5 px-3 text-center text-sm">{row.lock}</td>
                        <td className="py-2.5 px-3 text-center text-sm">{row.alloc}</td>
                        <td className="py-2.5 px-3 text-center text-sm">{row.snap}</td>
                        <td className="py-2.5 px-3 text-center text-sm">{row.hft}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
              <div className="flex flex-wrap gap-3 mt-3 text-[10px] text-slate-500">
                <span>✅ = Yes</span>
                <span>❌ = No</span>
                <span>⚠️ = Partial</span>
                <span>★ = Recommended</span>
              </div>
            </AnalysisCard>

            {/* Why foreach beats .Keys and .ToArray() */}
            <AnalysisCard title="Why Direct foreach Beats .Keys and .ToArray()" icon="🏆" variant="success">
              <div className="space-y-4">
                <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
                  {/* .Keys analysis */}
                  <div className="rounded-lg bg-red-950/30 border border-red-800/40 p-4">
                    <h4 className="text-sm font-bold text-red-300 mb-2">.Keys Property</h4>
                    <ul className="space-y-1.5 text-xs text-slate-400">
                      <li>• Allocates a new List&lt;K&gt; internally</li>
                      <li>• Copies all keys into the list</li>
                      <li>• You then need another loop to access values</li>
                      <li>• Two allocations per call (List + enumerator)</li>
                      <li>• No advantage over direct enumeration</li>
                    </ul>
                  </div>

                  {/* .ToArray() analysis */}
                  <div className="rounded-lg bg-amber-950/30 border border-amber-800/40 p-4">
                    <h4 className="text-sm font-bold text-amber-300 mb-2">.ToArray()</h4>
                    <ul className="space-y-1.5 text-xs text-slate-400">
                      <li>• Allocates a new array every call</li>
                      <li>• Copies all entries into the array</li>
                      <li>• GC pressure in hot paths</li>
                      <li>• Lock-free and thread-safe ✓</li>
                      <li>• Acceptable for cold paths</li>
                    </ul>
                  </div>

                  {/* Direct foreach analysis */}
                  <div className="rounded-lg bg-emerald-950/30 border border-emerald-800/40 p-4">
                    <h4 className="text-sm font-bold text-emerald-300 mb-2">Direct foreach ★</h4>
                    <ul className="space-y-1.5 text-xs text-slate-400">
                      <li>• Zero heap allocation (struct enumerator)</li>
                      <li>• No lock contention whatsoever</li>
                      <li>• Point-in-time snapshot semantics</li>
                      <li>• Access Key + Value in one pass</li>
                      <li>• Optimal for HFT hot paths</li>
                    </ul>
                  </div>
                </div>

                {/* Deep technical explanation */}
                <div className="rounded-lg bg-slate-800/40 border border-slate-700/40 p-4">
                  <h4 className="text-xs font-bold text-slate-400 uppercase tracking-wider mb-3">Deep Technical Rationale</h4>
                  <div className="space-y-3 text-sm text-slate-300 leading-relaxed">
                    <p>
                      <strong className="text-white">1. Cache-Line Efficiency:</strong> Direct foreach iterates over the ConcurrentDictionary's internal buckets sequentially. There's no intermediate copy, so CPU cache lines are used efficiently. With .ToArray(), you pay the cost of copying every entry into a new array, evicting hot cache lines.
                    </p>
                    <p>
                      <strong className="text-white">2. GC Pressure Elimination:</strong> In a high-frequency trading system processing thousands of messages per second, every allocation matters. ToArray() creates a new array object on every call, generating garbage that the GC must eventually collect. A struct enumerator allocates nothing on the heap — the entire iteration is stack-only.
                    </p>
                    <p>
                      <strong className="text-white">3. Lock Striping Advantage:</strong> ConcurrentDictionary in .NET 4.8 uses lock striping (multiple fine-grained locks, one per bucket group). Direct enumeration acquires these locks briefly per bucket and releases them, allowing other threads to proceed. An external lock serializes everything, destroying this advantage.
                    </p>
                    <p>
                      <strong className="text-white">4. Snapshot Semantics:</strong> The enumerator captures a snapshot at the moment of construction. Entries added after enumeration begins are not seen; entries removed may or may not appear. This "weak consistency" is actually ideal for propagation logic — you process what existed when you started, without blocking concurrent updates.
                    </p>
                  </div>
                </div>

                {/* Code comparison */}
                <div className="grid grid-cols-1 lg:grid-cols-3 gap-4">
                  <CodeBlock
                    label=".Keys (Inefficient)"
                    code={`// BAD: Allocates List<TKey> + iterates twice\nforeach (var key in ctx.FollowerEntries.Keys)\n{\n    var val = ctx.FollowerEntries[key];\n    // Second lookup! Potential race on value\n}`}
                    variant="broken"
                  />
                  <CodeBlock
                    label=".ToArray() (Acceptable)"
                    code={`// OK: Lock-free but allocates array\nvar snapshot = ctx.FollowerEntries.ToArray();\nforeach (var kvp in snapshot)\n{\n    Process(kvp.Key, kvp.Value);\n}`}
                    variant="neutral"
                  />
                  <CodeBlock
                    label="Direct foreach (Optimal ★)"
                    code={`// BEST: Zero alloc, lock-free, one pass\nforeach (var kvp in ctx.FollowerEntries)\n{\n    Process(kvp.Key, kvp.Value);\n}`}
                    variant="fixed"
                  />
                </div>
              </div>
            </AnalysisCard>

            {/* Summary */}
            <AnalysisCard title="Final Verdict" icon="🎯" variant="success">
              <div className="space-y-3">
                <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                  <div className="rounded-lg bg-red-950/40 border border-red-800/40 p-4">
                    <h4 className="text-sm font-bold text-red-300 mb-2">❌ Suggested Quick-Fix</h4>
                    <p className="text-sm text-slate-300">
                      <code className="text-amber-300 text-xs font-mono">lock(ctx.FollowerEntries)</code> violates the No-Internal-Locks invariant, introduces deadlock risk, amplifies contention, and defeats the purpose of using ConcurrentDictionary. <strong>Do not use.</strong>
                    </p>
                  </div>
                  <div className="rounded-lg bg-emerald-950/40 border border-emerald-800/40 p-4">
                    <h4 className="text-sm font-bold text-emerald-300 mb-2">✅ Recommended Fix</h4>
                    <p className="text-sm text-slate-300">
                      <strong>Remove all locks.</strong> Use direct <code className="text-emerald-300 text-xs font-mono">foreach</code> for Site 1 (zero alloc) and either <code className="text-emerald-300 text-xs font-mono">ToArray()</code> or inline foreach for Site 2. ConcurrentDictionary handles all synchronization natively.
                    </p>
                  </div>
                </div>
                <div className="text-center pt-2">
                  <p className="text-xs text-slate-500 font-mono">
                    Sovereign Substrate Refactor — ctx.Sync successfully eliminated — {MODEL_NAME} {MODEL_VERSION}
                  </p>
                </div>
              </div>
            </AnalysisCard>
          </div>
        )}
      </div>
    </div>
  );
}
