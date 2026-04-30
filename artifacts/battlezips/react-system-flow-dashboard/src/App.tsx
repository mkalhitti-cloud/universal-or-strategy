import { useState } from "react";

// ─── tiny helper ────────────────────────────────────────────────────────────
function cn(...cls: (string | false | undefined | null)[]) {
  return cls.filter(Boolean).join(" ");
}



// ─── reusable card shell ─────────────────────────────────────────────────────
function Card({
  title,
  icon,
  accent,
  children,
  className,
}: {
  title: string;
  icon: React.ReactNode;
  accent: string;
  children: React.ReactNode;
  className?: string;
}) {
  return (
    <div
      className={cn(
        "rounded-2xl border border-slate-700/60 bg-slate-800/70 backdrop-blur-sm shadow-xl overflow-hidden",
        className
      )}
    >
      <div className={cn("flex items-center gap-3 px-5 py-4 border-b border-slate-700/60", accent)}>
        <span className="text-xl">{icon}</span>
        <h3 className="font-semibold text-white tracking-wide text-sm uppercase letter-spacing-widest">
          {title}
        </h3>
      </div>
      <div className="p-5">{children}</div>
    </div>
  );
}

// ─── inline badge ────────────────────────────────────────────────────────────
function Badge({ label, color }: { label: string; color: string }) {
  return (
    <span
      className={cn(
        "inline-block rounded-full px-2.5 py-0.5 text-xs font-semibold border",
        color
      )}
    >
      {label}
    </span>
  );
}

// ─── code block ─────────────────────────────────────────────────────────────
function CodeBlock({ lines }: { lines: { text: string; cls?: string }[] }) {
  return (
    <pre className="rounded-xl bg-slate-900/80 border border-slate-700 p-4 overflow-x-auto text-sm leading-relaxed">
      {lines.map((l, i) => (
        <div key={i} className={l.cls}>
          {l.text}
        </div>
      ))}
    </pre>
  );
}

// ─── verdict row ─────────────────────────────────────────────────────────────
function VerdictRow({
  label,
  verdict,
  detail,
}: {
  label: string;
  verdict: "PASS" | "FAIL" | "WARN";
  detail: string;
}) {
  const cfg = {
    PASS: { dot: "bg-emerald-400", text: "text-emerald-400", badge: "border-emerald-500/50 text-emerald-400 bg-emerald-500/10" },
    FAIL: { dot: "bg-red-400", text: "text-red-400", badge: "border-red-500/50 text-red-400 bg-red-500/10" },
    WARN: { dot: "bg-amber-400", text: "text-amber-400", badge: "border-amber-500/50 text-amber-400 bg-amber-500/10" },
  }[verdict];
  return (
    <div className="flex items-start gap-3 py-2.5 border-b border-slate-700/40 last:border-0">
      <span className={cn("mt-1.5 h-2 w-2 rounded-full flex-shrink-0", cfg.dot)} />
      <div className="flex-1 min-w-0">
        <div className="flex items-center gap-2 flex-wrap">
          <span className="text-slate-200 text-sm font-medium">{label}</span>
          <span className={cn("text-xs font-bold px-2 py-0.5 rounded-full border", cfg.badge)}>
            {verdict}
          </span>
        </div>
        <p className="text-slate-400 text-xs mt-0.5 leading-relaxed">{detail}</p>
      </div>
    </div>
  );
}

// ─── comparison table ────────────────────────────────────────────────────────
function CompareTable() {
  const rows = [
    {
      approach: "lock(ctx.Sync) { foreach }",
      alloc: "0",
      threadSafe: false,
      hotPath: false,
      note: "Broken — Sync removed",
      verdict: "FAIL" as const,
    },
    {
      approach: "lock(ctx.FollowerEntries) { foreach }",
      alloc: "0",
      threadSafe: false,
      hotPath: false,
      note: "Violates No-Internal-Locks invariant",
      verdict: "FAIL" as const,
    },
    {
      approach: ".Keys.ToArray() + foreach",
      alloc: "string[] + KeyCollection",
      threadSafe: true,
      hotPath: false,
      note: "Two heap allocs; GC pressure on hot path",
      verdict: "WARN" as const,
    },
    {
      approach: ".ToArray() (KVP[])",
      alloc: "KeyValuePair<string,byte>[]",
      threadSafe: true,
      hotPath: false,
      note: "One alloc, but structs boxed internally",
      verdict: "WARN" as const,
    },
    {
      approach: "foreach directly on ConcurrentDictionary",
      alloc: "0 (enumerator is struct)",
      threadSafe: true,
      hotPath: true,
      note: "Lock-free snapshot via internal volatile reads; zero heap alloc",
      verdict: "PASS" as const,
    },
  ];

  const vCfg = {
    PASS: "text-emerald-400 bg-emerald-500/10 border-emerald-500/40",
    FAIL: "text-red-400 bg-red-500/10 border-red-500/40",
    WARN: "text-amber-400 bg-amber-500/10 border-amber-500/40",
  };

  return (
    <div className="overflow-x-auto rounded-xl border border-slate-700">
      <table className="w-full text-xs">
        <thead>
          <tr className="bg-slate-900/60 text-slate-400 uppercase tracking-wider">
            {["Approach", "Heap Alloc", "Thread-Safe", "Hot-Path OK", "Notes", "Verdict"].map(
              (h) => (
                <th key={h} className="px-3 py-2.5 text-left font-semibold whitespace-nowrap">
                  {h}
                </th>
              )
            )}
          </tr>
        </thead>
        <tbody>
          {rows.map((r, i) => (
            <tr
              key={i}
              className={cn(
                "border-t border-slate-700/40 transition-colors",
                i % 2 === 0 ? "bg-slate-800/30" : "bg-slate-800/10",
                "hover:bg-slate-700/30"
              )}
            >
              <td className={cn("px-3 py-2.5 font-mono text-slate-200 whitespace-nowrap")}>{r.approach}</td>
              <td className="px-3 py-2.5 text-slate-300 whitespace-nowrap">{r.alloc}</td>
              <td className="px-3 py-2.5">
                {r.threadSafe ? (
                  <span className="text-emerald-400 font-bold">✓</span>
                ) : (
                  <span className="text-red-400 font-bold">✗</span>
                )}
              </td>
              <td className="px-3 py-2.5">
                {r.hotPath ? (
                  <span className="text-emerald-400 font-bold">✓</span>
                ) : (
                  <span className="text-red-400 font-bold">✗</span>
                )}
              </td>
              <td className="px-3 py-2.5 text-slate-400 max-w-xs">{r.note}</td>
              <td className="px-3 py-2.5">
                <span className={cn("px-2 py-0.5 rounded-full border font-bold", vCfg[r.verdict])}>
                  {r.verdict}
                </span>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

// ─── main app ────────────────────────────────────────────────────────────────
export default function App() {
  const [activeTab, setActiveTab] = useState<"site1" | "site2">("site1");

  const MODEL = "Claude Sonnet 4.5";

  return (
    <div className="min-h-screen bg-slate-900 text-slate-100 font-sans">
      {/* ── header ── */}
      <header className="border-b border-slate-700/60 bg-slate-900/95 backdrop-blur sticky top-0 z-30">
        <div className="max-w-7xl mx-auto px-4 sm:px-6 py-4 flex flex-col sm:flex-row sm:items-center gap-2 sm:gap-0 justify-between">
          <div className="flex items-center gap-3">
            {/* hex icon */}
            <div className="relative flex-shrink-0">
              <div className="h-10 w-10 rounded-xl bg-gradient-to-br from-violet-500 to-sky-500 flex items-center justify-center shadow-lg shadow-violet-500/30">
                <svg className="h-5 w-5 text-white" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={2}>
                  <path d="M4 6h16M4 12h8m-8 6h16" strokeLinecap="round" />
                  <circle cx="19" cy="12" r="2" fill="currentColor" stroke="none" />
                </svg>
              </div>
              <span className="absolute -top-1 -right-1 h-3 w-3 rounded-full bg-emerald-400 border-2 border-slate-900 animate-pulse" />
            </div>
            <div>
              <h1 className="text-lg font-bold text-white leading-tight">System Flow Visualizer</h1>
              <p className="text-xs text-slate-400">Sovereign Substrate Refactor · Lock-Free Migration Dashboard</p>
            </div>
          </div>
          <div className="flex items-center gap-2 self-start sm:self-auto">
            <div className="rounded-lg bg-violet-500/10 border border-violet-500/30 px-3 py-1.5">
              <h2 className="text-xs font-bold text-violet-300 tracking-wide">{MODEL}</h2>
            </div>
            <Badge label="v4.5" color="border-sky-500/40 text-sky-300 bg-sky-500/10" />
            <Badge label="LIVE" color="border-emerald-500/40 text-emerald-300 bg-emerald-500/10" />
          </div>
        </div>
      </header>

      <main className="max-w-7xl mx-auto px-4 sm:px-6 py-8 space-y-8">

        {/* ── model identity banner ── */}
        <div className="rounded-2xl bg-gradient-to-r from-violet-900/40 via-slate-800/60 to-sky-900/40 border border-violet-500/20 p-5 flex flex-col sm:flex-row items-start sm:items-center gap-4">
          <div className="flex-1">
            <h2 className="text-2xl font-extrabold text-white tracking-tight">
              {MODEL}
            </h2>
            <p className="text-slate-400 text-sm mt-0.5">
              Analysis engine · Sovereign Substrate lock-free migration · .NET 4.8 concurrent pattern audit
            </p>
          </div>
          <div className="flex gap-2 flex-wrap">
            <Badge label="No-Internal-Locks Invariant" color="border-violet-500/50 text-violet-300 bg-violet-500/10" />
            <Badge label="Zero-Alloc Hot Path" color="border-emerald-500/50 text-emerald-300 bg-emerald-500/10" />
            <Badge label=".NET 4.8" color="border-sky-500/50 text-sky-300 bg-sky-500/10" />
          </div>
        </div>

        {/* ── ROW 1: Behavioral Extraction ── */}
        <Card
          title="Behavioral Extraction — Quick-Fix Verdict"
          icon="🔬"
          accent="bg-red-900/20"
          className=""
        >
          <div className="grid lg:grid-cols-2 gap-6">
            {/* left: what the quick-fix proposes */}
            <div className="space-y-3">
              <p className="text-xs uppercase tracking-widest text-slate-500 font-semibold">Proposed Quick-Fix</p>
              <CodeBlock
                lines={[
                  { text: "// ❌ Suggested replacement", cls: "text-slate-500" },
                  { text: 'lock(ctx.FollowerEntries)', cls: "text-red-400" },
                  { text: '{', cls: "text-slate-300" },
                  { text: '    foreach (var x in ctx.FollowerEntries) { ... }', cls: "text-slate-300" },
                  { text: '    var arr = ctx.FollowerEntries.ToArray();', cls: "text-slate-300" },
                  { text: '}', cls: "text-slate-300" },
                ]}
              />
              <div className="rounded-xl bg-red-900/20 border border-red-500/30 p-4 space-y-2">
                <div className="flex items-center gap-2">
                  <span className="text-red-400 text-lg">⛔</span>
                  <span className="text-red-300 font-bold text-sm">VERDICT: VIOLATES Sovereign Invariant</span>
                </div>
                <p className="text-slate-300 text-xs leading-relaxed">
                  <span className="text-red-400 font-mono">lock(ctx.FollowerEntries)</span> uses the dictionary object
                  itself as a monitor. <strong>ConcurrentDictionary internally acquires its own locks</strong> on
                  individual bucket segments (in .NET 4.8 it uses lock striping across <span className="font-mono text-amber-300">N</span> segment
                  objects). Wrapping the entire enumeration in an additional external lock does
                  <em> not</em> compose safely with those internal locks — it adds a coarse outer
                  lock on top of fine-grained internal ones, creating deadlock risk under contention
                  and <strong>directly violates the No-Internal-Locks invariant</strong>.
                </p>
              </div>
            </div>

            {/* right: invariant checklist */}
            <div className="space-y-3">
              <p className="text-xs uppercase tracking-widest text-slate-500 font-semibold">Sovereign Invariant Checklist</p>
              <div className="space-y-1">
                <VerdictRow
                  label="Removes ctx.Sync dependency"
                  verdict="PASS"
                  detail="ctx.Sync is no longer referenced — compilation succeeds."
                />
                <VerdictRow
                  label="No-Internal-Locks satisfied"
                  verdict="FAIL"
                  detail="lock(ctx.FollowerEntries) introduces a new external lock on a concurrent primitive, violating the invariant that no lock gates shall exist on shared state objects."
                />
                <VerdictRow
                  label="Deadlock-free under contention"
                  verdict="FAIL"
                  detail="Thread A holds external lock and waits for an internal bucket lock; Thread B holds that bucket lock and tries to enter. Classic lock inversion."
                />
                <VerdictRow
                  label="Zero-allocation on hot path"
                  verdict="WARN"
                  detail="ToArray() still allocates a KeyValuePair<string,byte>[] on the heap every invocation."
                />
                <VerdictRow
                  label="Satisfies Sovereign Substrate refactor contract"
                  verdict="FAIL"
                  detail="The refactor mandate explicitly prohibits lock objects on shared substrate context members. Using the dictionary as a monitor violates this contract."
                />
              </div>
            </div>
          </div>
        </Card>

        {/* ── ROW 2: Logic Matrix — optimal fix ── */}
        <Card
          title="Logic Matrix — Optimal Lock-Free Fix (.NET 4.8)"
          icon="⚡"
          accent="bg-emerald-900/20"
          className=""
        >
          {/* site tabs */}
          <div className="flex gap-2 mb-5">
            {(["site1", "site2"] as const).map((s) => (
              <button
                key={s}
                onClick={() => setActiveTab(s)}
                className={cn(
                  "px-4 py-2 rounded-lg text-xs font-semibold border transition-all",
                  activeTab === s
                    ? "bg-emerald-500/20 border-emerald-500/50 text-emerald-300"
                    : "bg-slate-700/30 border-slate-600/40 text-slate-400 hover:text-slate-200 hover:bg-slate-700/60"
                )}
              >
                {s === "site1"
                  ? "Site 1 · V12_002.SIMA.Shadow.cs:89"
                  : "Site 2 · V12_002.Orders.Callbacks.Propagation.cs:126"}
              </button>
            ))}
          </div>

          {activeTab === "site1" && (
            <div className="grid lg:grid-cols-2 gap-6">
              <div className="space-y-3">
                <p className="text-xs uppercase tracking-widest text-slate-500 font-semibold">Broken Code (Line 89)</p>
                <CodeBlock
                  lines={[
                    { text: "// ❌ BROKEN — ctx.Sync removed", cls: "text-red-400" },
                    { text: "lock (ctx.Sync)", cls: "text-red-400" },
                    { text: "{", cls: "text-slate-300" },
                    { text: "    foreach (var x in ctx.FollowerEntries)", cls: "text-slate-300" },
                    { text: "    {", cls: "text-slate-300" },
                    { text: "        // ... process x.Key ...", cls: "text-slate-500" },
                    { text: "    }", cls: "text-slate-300" },
                    { text: "}", cls: "text-slate-300" },
                  ]}
                />
              </div>
              <div className="space-y-3">
                <p className="text-xs uppercase tracking-widest text-slate-500 font-semibold">
                  ✅ Optimal Fix — Direct Struct Enumeration
                </p>
                <CodeBlock
                  lines={[
                    { text: "// ✅ CORRECT — lock-free, zero-alloc", cls: "text-emerald-400" },
                    { text: "// ConcurrentDictionary<TKey,TValue>.GetEnumerator()", cls: "text-slate-500" },
                    { text: "// returns a VALUE-TYPE enumerator (struct) in .NET 4.8.", cls: "text-slate-500" },
                    { text: "// Each MoveNext() reads via volatile snapshot semantics.", cls: "text-slate-500" },
                    { text: "// No lock. No heap allocation.", cls: "text-slate-500" },
                    { text: "", cls: "" },
                    { text: "foreach (var x in ctx.FollowerEntries)", cls: "text-emerald-300" },
                    { text: "{", cls: "text-slate-300" },
                    { text: "    // x.Key is string (ref type, no extra alloc)", cls: "text-slate-500" },
                    { text: "    // x.Value is byte (value type, stack)", cls: "text-slate-500" },
                    { text: "    Process(x.Key);", cls: "text-slate-300" },
                    { text: "}", cls: "text-slate-300" },
                  ]}
                />
                <div className="rounded-xl bg-emerald-900/20 border border-emerald-500/30 p-3 text-xs text-emerald-200 leading-relaxed">
                  <strong className="text-emerald-300">Why this is safe without a lock:</strong> In .NET 4.8,
                  <span className="font-mono text-amber-300"> ConcurrentDictionary</span> uses lock-striped
                  segment buckets internally. Its enumerator takes a <em>weakly consistent snapshot</em> of each
                  bucket during <span className="font-mono text-amber-300">MoveNext()</span> — it will not throw
                  during concurrent modification (unlike <span className="font-mono text-red-300">Dictionary</span>),
                  and it will observe values that were present at or after enumeration start.
                  This is the documented safe enumeration pattern for this type.
                </div>
              </div>
            </div>
          )}

          {activeTab === "site2" && (
            <div className="grid lg:grid-cols-2 gap-6">
              <div className="space-y-3">
                <p className="text-xs uppercase tracking-widest text-slate-500 font-semibold">Broken Code (Line 126)</p>
                <CodeBlock
                  lines={[
                    { text: "// ❌ BROKEN — ctx.Sync removed", cls: "text-red-400" },
                    { text: "lock (ctx.Sync)", cls: "text-red-400" },
                    { text: "{", cls: "text-slate-300" },
                    { text: "    var arr = ctx.FollowerEntries.ToArray();", cls: "text-slate-300" },
                    { text: "}", cls: "text-slate-300" },
                    { text: "", cls: "" },
                    { text: "// then later:", cls: "text-slate-500" },
                    { text: "foreach (var x in arr) { ... }", cls: "text-slate-300" },
                  ]}
                />
              </div>
              <div className="space-y-3">
                <p className="text-xs uppercase tracking-widest text-slate-500 font-semibold">
                  ✅ Optimal Fix — Enumerate In-Place, No Snapshot
                </p>
                <CodeBlock
                  lines={[
                    { text: "// ✅ CORRECT — if you need a stable set for propagation:", cls: "text-emerald-400" },
                    { text: "// Option A: enumerate directly (zero-alloc, weakly-consistent).", cls: "text-slate-500" },
                    { text: "foreach (var entry in ctx.FollowerEntries)", cls: "text-emerald-300" },
                    { text: "{", cls: "text-slate-300" },
                    { text: "    Propagate(entry.Key);", cls: "text-slate-300" },
                    { text: "}", cls: "text-slate-300" },
                    { text: "", cls: "" },
                    { text: "// Option B: if a true point-in-time snapshot is a hard req:", cls: "text-slate-500" },
                    { text: "// Use Keys property (IEnumerable<TKey>) — still allocs once.", cls: "text-slate-500" },
                    { text: "// Prefer Option A unless atomicity of the snapshot is provably", cls: "text-slate-500" },
                    { text: "// required by the Sovereign Invariant spec.", cls: "text-slate-500" },
                    { text: "", cls: "" },
                    { text: "// ❌ Do NOT do this:", cls: "text-red-400" },
                    { text: "lock(ctx.FollowerEntries) { ... }  // violates invariant", cls: "text-red-400" },
                  ]}
                />
                <div className="rounded-xl bg-sky-900/20 border border-sky-500/30 p-3 text-xs text-sky-200 leading-relaxed">
                  <strong className="text-sky-300">Snapshot semantics note:</strong> If the business rule
                  requires that <em>exactly</em> the set of followers present at call time is propagated
                  (i.e., stragglers added during enumeration must be excluded), then
                  <span className="font-mono text-amber-300"> foreach</span> on
                  <span className="font-mono text-amber-300"> ConcurrentDictionary</span> provides
                  <strong> weakly-consistent</strong> (not strongly-consistent) snapshot semantics. In HFT
                  callback propagation this is typically acceptable. If strong atomicity is required, use
                  a <span className="font-mono text-amber-300">ImmutableDictionary</span> swap pattern (Interlocked.Exchange)
                  instead of locking.
                </div>
              </div>
            </div>
          )}
        </Card>

        {/* ── ROW 3: Comparative Analysis ── */}
        <Card
          title="Comparative Analysis — Hot-Path Pattern Evaluation"
          icon="📊"
          accent="bg-sky-900/20"
          className=""
        >
          <div className="space-y-5">
            <p className="text-slate-300 text-sm leading-relaxed">
              Why <span className="font-mono text-emerald-400">foreach</span> directly on{" "}
              <span className="font-mono text-amber-300">ConcurrentDictionary</span> outperforms{" "}
              <span className="font-mono text-red-400">.Keys</span> or{" "}
              <span className="font-mono text-red-400">.ToArray()</span> on a high-frequency trading hot path:
            </p>

            <CompareTable />

            <div className="grid sm:grid-cols-3 gap-4 mt-4">
              {/* card 1 */}
              <div className="rounded-xl bg-slate-900/60 border border-red-500/20 p-4 space-y-2">
                <div className="flex items-center gap-2">
                  <span className="text-red-400 text-base">🔴</span>
                  <span className="text-red-300 font-semibold text-sm">.Keys or .ToArray()</span>
                </div>
                <ul className="text-xs text-slate-400 space-y-1.5 list-none">
                  <li className="flex gap-1.5"><span className="text-red-400 flex-shrink-0">✗</span> Allocates one or two arrays on the managed heap every call</li>
                  <li className="flex gap-1.5"><span className="text-red-400 flex-shrink-0">✗</span> Under Gen0 GC pressure at HFT throughput (&gt;100 k events/s), these short-lived arrays trigger frequent minor collections, causing microsecond pauses — unacceptable in a latency-sensitive path</li>
                  <li className="flex gap-1.5"><span className="text-red-400 flex-shrink-0">✗</span> <span className="font-mono">.Keys</span> returns a new <span className="font-mono">ICollection&lt;TKey&gt;</span> wrapper — two allocations before you even iterate</li>
                  <li className="flex gap-1.5"><span className="text-red-400 flex-shrink-0">✗</span> <span className="font-mono">.ToArray()</span> calls Linq extension which boxes the enumerator</li>
                </ul>
              </div>
              {/* card 2 */}
              <div className="rounded-xl bg-slate-900/60 border border-amber-500/20 p-4 space-y-2">
                <div className="flex items-center gap-2">
                  <span className="text-amber-400 text-base">🟡</span>
                  <span className="text-amber-300 font-semibold text-sm">lock(obj) + foreach</span>
                </div>
                <ul className="text-xs text-slate-400 space-y-1.5 list-none">
                  <li className="flex gap-1.5"><span className="text-amber-400 flex-shrink-0">!</span> Monitor.Enter/Exit overhead on every call — even uncontended monitors cost ~10–30 ns (cache-line ownership transfer)</li>
                  <li className="flex gap-1.5"><span className="text-amber-400 flex-shrink-0">!</span> Under contention: thread park/unpark via OS scheduler — microseconds to milliseconds of jitter</li>
                  <li className="flex gap-1.5"><span className="text-amber-400 flex-shrink-0">!</span> Coarse lock serialises all readers, destroying concurrency when many threads need read access simultaneously</li>
                  <li className="flex gap-1.5"><span className="text-red-400 flex-shrink-0">✗</span> Violates Sovereign No-Internal-Locks invariant entirely</li>
                </ul>
              </div>
              {/* card 3 */}
              <div className="rounded-xl bg-slate-900/60 border border-emerald-500/20 p-4 space-y-2">
                <div className="flex items-center gap-2">
                  <span className="text-emerald-400 text-base">🟢</span>
                  <span className="text-emerald-300 font-semibold text-sm">Direct foreach (recommended)</span>
                </div>
                <ul className="text-xs text-slate-400 space-y-1.5 list-none">
                  <li className="flex gap-1.5"><span className="text-emerald-400 flex-shrink-0">✓</span> Struct enumerator: no heap allocation, lives entirely on the stack</li>
                  <li className="flex gap-1.5"><span className="text-emerald-400 flex-shrink-0">✓</span> No monitor contention — concurrent writers proceed independently</li>
                  <li className="flex gap-1.5"><span className="text-emerald-400 flex-shrink-0">✓</span> Weakly-consistent snapshot per segment — safe against <span className="font-mono">InvalidOperationException</span> even under mutation</li>
                  <li className="flex gap-1.5"><span className="text-emerald-400 flex-shrink-0">✓</span> Fully satisfies Sovereign Substrate No-Internal-Locks invariant</li>
                  <li className="flex gap-1.5"><span className="text-emerald-400 flex-shrink-0">✓</span> JIT can inline and unroll the iteration loop in Release builds</li>
                </ul>
              </div>
            </div>

            {/* memory model note */}
            <div className="rounded-xl bg-violet-900/20 border border-violet-500/20 p-4">
              <p className="text-xs text-slate-400 leading-relaxed">
                <span className="text-violet-300 font-semibold">Memory model note (.NET 4.8 / CLR 4.x): </span>
                <span className="font-mono text-amber-300">ConcurrentDictionary</span> in .NET 4.8 uses
                a segmented lock table internally (legacy implementation, predating the .NET Core rewrite to
                a single-lock-per-table approach). Each segment uses{" "}
                <span className="font-mono text-amber-300">volatile</span> reads on its bucket array, which
                guarantees the enumerator sees a coherent view of each individual segment without requiring
                the caller to acquire any lock. This is distinct from the "snapshot-on-read" guarantee offered
                by immutable collections, but is sufficient for follower-propagation callbacks where
                eventual-consistency of the membership set is contractually acceptable.
              </p>
            </div>
          </div>
        </Card>

        {/* ── ROW 4: Site Summary ── */}
        <div className="grid sm:grid-cols-2 gap-5">
          {/* Site 1 summary */}
          <Card title="Site 1 · SIMA.Shadow.cs:89" icon="📍" accent="bg-slate-700/30" className="">
            <div className="space-y-3">
              <div className="flex gap-2 flex-wrap">
                <Badge label="Iterate" color="border-sky-500/40 text-sky-300 bg-sky-500/10" />
                <Badge label="ConcurrentDictionary" color="border-amber-500/40 text-amber-300 bg-amber-500/10" />
                <Badge label="Zero-Alloc" color="border-emerald-500/40 text-emerald-300 bg-emerald-500/10" />
              </div>
              <CodeBlock
                lines={[
                  { text: "// ✅ Sovereign-compliant replacement", cls: "text-emerald-400" },
                  { text: "foreach (var x in ctx.FollowerEntries)", cls: "text-emerald-300" },
                  { text: "{", cls: "text-slate-300" },
                  { text: "    Process(x.Key);", cls: "text-slate-300" },
                  { text: "}", cls: "text-slate-300" },
                ]}
              />
              <p className="text-xs text-slate-400">
                Remove <span className="font-mono text-red-400">lock(ctx.Sync)</span> wrapper entirely.
                The <span className="font-mono text-amber-300">foreach</span> compiles to a
                struct-enumerator call with no managed heap allocation.
              </p>
            </div>
          </Card>

          {/* Site 2 summary */}
          <Card title="Site 2 · Orders.Callbacks.Propagation.cs:126" icon="📍" accent="bg-slate-700/30" className="">
            <div className="space-y-3">
              <div className="flex gap-2 flex-wrap">
                <Badge label="Propagation Snapshot" color="border-purple-500/40 text-purple-300 bg-purple-500/10" />
                <Badge label="Weakly-Consistent" color="border-amber-500/40 text-amber-300 bg-amber-500/10" />
                <Badge label="Lock-Free" color="border-emerald-500/40 text-emerald-300 bg-emerald-500/10" />
              </div>
              <CodeBlock
                lines={[
                  { text: "// ✅ Preferred — enumerate directly, no alloc", cls: "text-emerald-400" },
                  { text: "foreach (var entry in ctx.FollowerEntries)", cls: "text-emerald-300" },
                  { text: "{", cls: "text-slate-300" },
                  { text: "    Propagate(entry.Key);", cls: "text-slate-300" },
                  { text: "}", cls: "text-slate-300" },
                  { text: "", cls: "" },
                  { text: "// If hard atomicity required (rare):", cls: "text-slate-500" },
                  { text: "// Use Interlocked.Exchange on ImmutableDictionary ref.", cls: "text-slate-500" },
                ]}
              />
              <p className="text-xs text-slate-400">
                Drop <span className="font-mono text-red-400">lock(ctx.Sync)</span> and
                <span className="font-mono text-red-400"> .ToArray()</span>. The direct enumeration
                satisfies the No-Internal-Locks invariant and eliminates the callback's per-call heap
                allocation.
              </p>
            </div>
          </Card>
        </div>

        {/* ── ROW 5: Final Ruling ── */}
        <div className="rounded-2xl border border-violet-500/30 bg-gradient-to-br from-violet-900/30 via-slate-800/60 to-sky-900/30 p-6 space-y-4">
          <div className="flex items-center gap-3">
            <span className="text-2xl">⚖️</span>
            <h3 className="text-lg font-bold text-white">Final Ruling — Sovereign Invariant Compliance</h3>
          </div>
          <div className="grid sm:grid-cols-3 gap-4">
            {[
              {
                title: "Quick-Fix Proposal",
                verdict: "REJECTED",
                color: "text-red-300 bg-red-500/10 border-red-500/30",
                dot: "bg-red-400",
                reason:
                  "lock(ctx.FollowerEntries) introduces an external monitor on a concurrent collection, violating No-Internal-Locks and risking lock-inversion deadlock.",
              },
              {
                title: "Recommended Pattern",
                verdict: "APPROVED",
                color: "text-emerald-300 bg-emerald-500/10 border-emerald-500/30",
                dot: "bg-emerald-400",
                reason:
                  "Direct foreach on ConcurrentDictionary<string, byte>. Zero heap allocations, no locks, struct enumerator, weakly-consistent and safe under concurrent mutation. Fully satisfies Sovereign Invariant.",
              },
              {
                title: ".Keys / .ToArray() Fallback",
                verdict: "DISCOURAGED",
                color: "text-amber-300 bg-amber-500/10 border-amber-500/30",
                dot: "bg-amber-400",
                reason:
                  "Thread-safe but allocates on every call. Incompatible with zero-allocation hot-path requirements in HFT callback propagation. Use only when a true immutable snapshot is an explicit contract requirement.",
              },
            ].map((r) => (
              <div key={r.title} className={cn("rounded-xl border p-4 space-y-2", r.color)}>
                <div className="flex items-center gap-2">
                  <span className={cn("h-2.5 w-2.5 rounded-full", r.dot)} />
                  <span className="font-bold text-sm">{r.verdict}</span>
                </div>
                <p className="text-xs text-white/70 font-medium">{r.title}</p>
                <p className="text-xs text-slate-300 leading-relaxed">{r.reason}</p>
              </div>
            ))}
          </div>
        </div>
      </main>

      {/* ── footer ── */}
      <footer className="border-t border-slate-700/40 mt-10 py-5 text-center">
        <p className="text-xs text-slate-500">
          System Flow Visualizer · Sovereign Substrate Refactor Dashboard ·{" "}
          <span className="text-violet-400 font-semibold">{MODEL}</span>
        </p>
      </footer>
    </div>
  );
}
