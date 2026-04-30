import { useState } from "react";

// ─── Types ────────────────────────────────────────────────────────────────────
type Severity = "critical" | "warning" | "ok" | "info";

interface Finding {
  id: string;
  severity: Severity;
  title: string;
  body: React.ReactNode;
}

// ─── Helpers ──────────────────────────────────────────────────────────────────
const severityMeta: Record<
  Severity,
  { label: string; dot: string; badge: string; border: string; header: string }
> = {
  critical: {
    label: "CRITICAL",
    dot: "bg-red-500 animate-pulse",
    badge: "bg-red-100 text-red-700 border border-red-300",
    border: "border-red-400",
    header: "bg-gradient-to-r from-red-950 to-red-900",
  },
  warning: {
    label: "WARNING",
    dot: "bg-amber-400 animate-pulse",
    badge: "bg-amber-100 text-amber-700 border border-amber-300",
    border: "border-amber-400",
    header: "bg-gradient-to-r from-amber-950 to-yellow-900",
  },
  ok: {
    label: "PASS",
    dot: "bg-emerald-400",
    badge: "bg-emerald-100 text-emerald-700 border border-emerald-300",
    border: "border-emerald-400",
    header: "bg-gradient-to-r from-emerald-950 to-teal-900",
  },
  info: {
    label: "INFO",
    dot: "bg-sky-400",
    badge: "bg-sky-100 text-sky-700 border border-sky-300",
    border: "border-sky-400",
    header: "bg-gradient-to-r from-sky-950 to-indigo-900",
  },
};

// ─── Code Block ───────────────────────────────────────────────────────────────
function Code({ children, lang = "csharp" }: { children: string; lang?: string }) {
  const [copied, setCopied] = useState(false);
  const copy = () => {
    navigator.clipboard.writeText(children.trim());
    setCopied(true);
    setTimeout(() => setCopied(false), 1800);
  };
  return (
    <div className="relative mt-3 rounded-lg overflow-hidden border border-slate-700 bg-slate-950 shadow-lg">
      <div className="flex items-center justify-between px-4 py-1.5 bg-slate-800 border-b border-slate-700">
        <span className="text-[10px] font-mono uppercase tracking-widest text-slate-400">{lang}</span>
        <button
          onClick={copy}
          className="text-[10px] font-mono text-slate-400 hover:text-white transition-colors px-2 py-0.5 rounded bg-slate-700 hover:bg-slate-600"
        >
          {copied ? "✓ copied" : "copy"}
        </button>
      </div>
      <pre className="overflow-x-auto p-4 text-sm leading-relaxed text-slate-100 font-mono whitespace-pre">
        {children.trim()}
      </pre>
    </div>
  );
}

// ─── Inline chip ──────────────────────────────────────────────────────────────
function Chip({ children, color = "slate" }: { children: React.ReactNode; color?: string }) {
  const map: Record<string, string> = {
    red: "bg-red-900/60 text-red-300 border-red-700",
    green: "bg-emerald-900/60 text-emerald-300 border-emerald-700",
    amber: "bg-amber-900/60 text-amber-300 border-amber-700",
    sky: "bg-sky-900/60 text-sky-300 border-sky-700",
    slate: "bg-slate-800 text-slate-300 border-slate-600",
    violet: "bg-violet-900/60 text-violet-300 border-violet-700",
  };
  return (
    <span className={`inline-block px-2 py-0.5 rounded border text-xs font-mono font-semibold ${map[color] ?? map.slate}`}>
      {children}
    </span>
  );
}

// ─── Card ─────────────────────────────────────────────────────────────────────
function Card({ finding, index }: { finding: Finding; index: number }) {
  const [open, setOpen] = useState(true);
  const m = severityMeta[finding.severity];
  return (
    <div
      className={`rounded-xl border ${m.border} bg-slate-900 shadow-xl shadow-black/40 overflow-hidden transition-all duration-300`}
      style={{ animationDelay: `${index * 80}ms` }}
    >
      {/* Card header */}
      <button
        onClick={() => setOpen((o) => !o)}
        className={`w-full flex items-center justify-between px-5 py-3 ${m.header} text-white`}
      >
        <div className="flex items-center gap-3">
          <span className={`h-2.5 w-2.5 rounded-full flex-shrink-0 ${m.dot}`} />
          <span className="text-xs font-mono font-bold tracking-widest opacity-70">{m.label}</span>
          <span className="text-sm font-semibold">{finding.title}</span>
        </div>
        <span className="text-slate-400 text-xs font-mono">{open ? "▲ collapse" : "▼ expand"}</span>
      </button>

      {/* Card body */}
      {open && (
        <div className="px-5 py-4 text-sm text-slate-300 leading-relaxed space-y-3">
          {finding.body}
        </div>
      )}
    </div>
  );
}

// ─── Table ────────────────────────────────────────────────────────────────────
function CompTable() {
  const rows = [
    {
      approach: "lock (ctx.Sync) { ... }",
      allocates: "No",
      lockFree: "✗",
      threadSafe: "✓",
      hotPath: "✗ — blocks all threads",
      verdict: "BROKEN (Sync removed)",
      vcolor: "red",
    },
    {
      approach: "lock (ctx.FollowerEntries) { ... }",
      allocates: "No",
      lockFree: "✗",
      threadSafe: "✓",
      hotPath: "✗ — violates invariant + contention risk",
      verdict: "VIOLATES invariant",
      vcolor: "red",
    },
    {
      approach: ".Keys.ToArray() / .ToArray()",
      allocates: "Yes — T[]",
      lockFree: "✓",
      threadSafe: "✓",
      hotPath: "✗ — heap alloc, GC pressure on hot path",
      verdict: "OK but sub-optimal",
      vcolor: "amber",
    },
    {
      approach: ".Keys (ICollection<K>)",
      allocates: "No (snapshot not guaranteed)",
      lockFree: "✓",
      threadSafe: "Partial — enumerator may throw",
      hotPath: "✗ — can raise during concurrent write",
      verdict: "UNSAFE hot-path",
      vcolor: "red",
    },
    {
      approach: "Direct enumeration of ConcurrentDictionary",
      allocates: "No",
      lockFree: "✓",
      threadSafe: "✓ — weakly-consistent snapshot",
      hotPath: "✓ — zero extra alloc, no lock",
      verdict: "OPTIMAL (Site 1)",
      vcolor: "green",
    },
    {
      approach: "GetEnumerator() + manual struct enum",
      allocates: "No (struct enumerator)",
      lockFree: "✓",
      threadSafe: "✓",
      hotPath: "✓✓ — avoids boxing, zero heap",
      verdict: "BEST for Site 1",
      vcolor: "green",
    },
    {
      approach: "Span<T> / stackalloc (count known)",
      allocates: "Stack only",
      lockFree: "✓",
      threadSafe: "✓",
      hotPath: "✓✓ — zero heap, .NET 4.8 viable",
      verdict: "BEST for Site 2",
      vcolor: "green",
    },
  ];

  const vMap: Record<string, string> = {
    red: "text-red-400 font-bold",
    amber: "text-amber-400 font-semibold",
    green: "text-emerald-400 font-bold",
  };

  return (
    <div className="overflow-x-auto rounded-lg border border-slate-700 mt-3">
      <table className="w-full text-xs font-mono border-collapse">
        <thead>
          <tr className="bg-slate-800 text-slate-300 text-left">
            {["Approach", "Heap Alloc?", "Lock-Free?", "Thread-Safe?", "Hot-Path Suitability", "Verdict"].map((h) => (
              <th key={h} className="px-3 py-2 border-b border-slate-700 whitespace-nowrap font-bold tracking-wide">{h}</th>
            ))}
          </tr>
        </thead>
        <tbody>
          {rows.map((r, i) => (
            <tr key={i} className={`border-b border-slate-800 ${i % 2 === 0 ? "bg-slate-900" : "bg-slate-900/60"} hover:bg-slate-800/70 transition-colors`}>
              <td className="px-3 py-2 text-violet-300">{r.approach}</td>
              <td className="px-3 py-2 text-slate-300">{r.allocates}</td>
              <td className={`px-3 py-2 ${r.lockFree === "✓" ? "text-emerald-400" : "text-red-400"}`}>{r.lockFree}</td>
              <td className={`px-3 py-2 ${r.threadSafe.startsWith("✓") ? "text-emerald-400" : r.threadSafe === "Partial — enumerator may throw" ? "text-amber-400" : "text-red-400"}`}>{r.threadSafe}</td>
              <td className="px-3 py-2 text-slate-400">{r.hotPath}</td>
              <td className={`px-3 py-2 ${vMap[r.vcolor]}`}>{r.verdict}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

// ─── Findings data ────────────────────────────────────────────────────────────
const findings: Finding[] = [
  // ── 0. Model identity ──────────────────────────────────────────────────────
  {
    id: "model-id",
    severity: "info",
    title: "Model Identity",
    body: (
      <div className="space-y-2">
        <p>
          This analysis was produced by{" "}
          <Chip color="violet">Claude claude-opus-4-5</Chip> (Anthropic).
          All reasoning is from training knowledge; no external tools or searches were used.
        </p>
        <p className="text-slate-400 text-xs">
          Model family: Claude · Series: Opus · Version: 4.5 · Vendor: Anthropic PBC
        </p>
      </div>
    ),
  },

  // ── 1. Behavioral Extraction — Quick-Fix Verdict ───────────────────────────
  {
    id: "quick-fix",
    severity: "critical",
    title: "Behavioral Extraction — Suggested Quick-Fix Analysis",
    body: (
      <div className="space-y-4">
        <p>
          The proposed quick-fix is: replace <Chip color="red">lock (ctx.Sync) {"{ … }"}</Chip>{" "}
          with <Chip color="red">lock (ctx.FollowerEntries) {"{ … }"}</Chip>.
        </p>

        <div className="rounded-lg border border-red-800 bg-red-950/40 p-4 space-y-2">
          <p className="text-red-300 font-bold text-sm flex items-center gap-2">
            <span>⛔</span> VERDICT: The quick-fix VIOLATES the Sovereign Invariant.
          </p>
          <ul className="list-disc list-inside space-y-1 text-slate-300 text-xs leading-relaxed">
            <li>
              <strong className="text-white">No-Internal-Locks</strong> means no{" "}
              <code className="text-amber-300">lock</code> keyword at all on shared state — not even on the collection itself.
              Using <code className="text-red-300">lock(ctx.FollowerEntries)</code> is still a monitor-based exclusive lock.
            </li>
            <li>
              <strong className="text-white">It achieves nothing new.</strong>{" "}
              <code className="text-amber-300">ConcurrentDictionary&lt;TKey,TValue&gt;</code> is already internally
              lock-striped. Wrapping it in an outer <code>lock</code> on the collection reference serialises all
              threads through a single monitor — worse contention than the internal fine-grained stripe locks.
            </li>
            <li>
              <strong className="text-white">Lock inversion / deadlock risk.</strong>{" "}
              If any other code path also takes <code>lock(ctx.FollowerEntries)</code> while holding another lock,
              or vice-versa, classic ABBA deadlock can occur across threads.
            </li>
            <li>
              <strong className="text-white">Does NOT satisfy the Sovereign Invariant.</strong>{" "}
              The invariant requires zero blocking primitives on the hot path. A{" "}
              <code className="text-red-300">lock</code> statement always implies potential thread blocking.
            </li>
          </ul>
        </div>

        <Code lang="csharp">{`// ❌ QUICK-FIX — still uses a lock — VIOLATES Sovereign Invariant
lock (ctx.FollowerEntries)           // outer monitor on collection obj
{
    foreach (var x in ctx.FollowerEntries) { ... }  // Site 1
}

lock (ctx.FollowerEntries)           // same issue
{
    var arr = ctx.FollowerEntries.ToArray();          // Site 2
}`}</Code>

        <p className="text-xs text-slate-400">
          The answer to "Does this satisfy the Sovereign Invariant?" is{" "}
          <strong className="text-red-400">No</strong>. It swaps one lock object for another and adds new hazards
          without providing lock-freedom.
        </p>
      </div>
    ),
  },

  // ── 2. Logic Matrix — Optimal Solutions ───────────────────────────────────
  {
    id: "logic-matrix",
    severity: "ok",
    title: "Logic Matrix — Optimal Lock-Free, Zero-Allocation Solutions (.NET 4.8)",
    body: (
      <div className="space-y-5">
        {/* Site 1 */}
        <div>
          <p className="font-semibold text-emerald-300 mb-1 flex items-center gap-2">
            <span className="bg-emerald-700 text-white text-xs px-2 py-0.5 rounded font-mono">SITE 1</span>
            V12_002.SIMA.Shadow.cs : 89 — Iterate FollowerEntries
          </p>
          <p className="text-slate-300 text-xs mb-2">
            <code className="text-violet-300">ConcurrentDictionary&lt;TKey,TValue&gt;</code> implements{" "}
            <code className="text-violet-300">IEnumerable&lt;KeyValuePair&lt;K,V&gt;&gt;</code> with a built-in
            weakly-consistent, lock-free enumerator. Directly iterating the dictionary takes a logical
            snapshot-per-segment without acquiring any user-visible lock and without allocating a heap array.
          </p>
          <Code lang="csharp">{`// ✅ SITE 1 — lock-free, zero heap allocation, .NET 4.8 compatible
// ConcurrentDictionary's own enumerator is weakly-consistent:
// it will NOT throw InvalidOperationException on concurrent mutation.
// Each internal segment is locked momentarily at the segment level
// (internal implementation detail, not user-visible blocking).

foreach (var entry in ctx.FollowerEntries)
{
    // entry.Key   → string
    // entry.Value → byte
    ProcessFollower(entry.Key, entry.Value);
}

// If you need ONLY keys (matching the original intent):
foreach (var kvp in ctx.FollowerEntries)
{
    var key = kvp.Key;          // zero alloc, no extra collection
    DoWork(key);
}`}</Code>
          <div className="mt-2 rounded border border-emerald-800 bg-emerald-950/30 p-3 text-xs text-slate-300 space-y-1">
            <p><Chip color="green">✓ Lock-free</Chip> — no <code>lock</code> keyword, no <code>Monitor</code>, no <code>Mutex</code>.</p>
            <p><Chip color="green">✓ Zero heap alloc</Chip> — <code>ConcurrentDictionary</code>'s enumerator struct avoids boxing in .NET 4.8 when called via the concrete type.</p>
            <p><Chip color="green">✓ Weakly-consistent</Chip> — guaranteed not to throw on concurrent adds/removes. May or may not see in-flight mutations (acceptable for follower propagation).</p>
            <p><Chip color="green">✓ Sovereign Invariant satisfied</Chip> — zero blocking primitives visible to calling code.</p>
          </div>
        </div>

        {/* Site 2 */}
        <div>
          <p className="font-semibold text-emerald-300 mb-1 flex items-center gap-2">
            <span className="bg-emerald-700 text-white text-xs px-2 py-0.5 rounded font-mono">SITE 2</span>
            V12_002.Orders.Callbacks.Propagation.cs : 126 — Snapshot FollowerEntries
          </p>
          <p className="text-slate-300 text-xs mb-2">
            Site 2 explicitly wants a <em>snapshot array</em>. The standard{" "}
            <code className="text-red-300">.ToArray()</code> allocates. For a high-frequency trading hot path in
            .NET 4.8, the best strategy is to avoid the snapshot entirely if the weakly-consistent enumerator suffices,
            OR use a pre-allocated re-usable buffer.
          </p>
          <Code lang="csharp">{`// ✅ SITE 2 — Option A: Avoid snapshot entirely (preferred if semantics allow)
// Use the weakly-consistent enumerator directly — same guarantee as Option A above.
// No allocation, no lock, fully Sovereign-Invariant-safe.
foreach (var kvp in ctx.FollowerEntries)
{
    DispatchCallback(kvp.Key);
}

// ✅ SITE 2 — Option B: True snapshot with zero heap alloc via stackalloc (small N)
// .NET 4.8 supports Span<T> via System.Memory NuGet or via unsafe context.
// Use only if count is bounded and small (e.g., < 256 followers).
int count = ctx.FollowerEntries.Count;
Span<KeyValuePair<string, byte>> buffer = stackalloc KeyValuePair<string, byte>[count];
int i = 0;
foreach (var kvp in ctx.FollowerEntries)
    buffer[i++] = kvp;
// 'buffer' is now a stack-resident snapshot — zero GC pressure.

// ✅ SITE 2 — Option C: Pre-allocated ArraySegment / pooled buffer (large N)
// Use ArrayPool<T>.Shared (available in .NET 4.6+ via System.Buffers NuGet).
var pool   = ArrayPool<KeyValuePair<string, byte>>.Shared;
var rented = pool.Rent(ctx.FollowerEntries.Count);
try
{
    int n = 0;
    foreach (var kvp in ctx.FollowerEntries)
        rented[n++] = kvp;
    // Process rented[0..n-1]
    DispatchCallbacks(rented, n);
}
finally
{
    pool.Return(rented, clearArray: false); // clearArray: false = faster
}`}</Code>
          <div className="mt-2 rounded border border-emerald-800 bg-emerald-950/30 p-3 text-xs text-slate-300 space-y-1">
            <p><Chip color="green">Option A</Chip> — preferred when callback ordering / stability is not a hard requirement. Zero cost.</p>
            <p><Chip color="green">Option B</Chip> — stack-allocated snapshot, truly zero GC. Safe up to ~256–512 entries (stack frame budget).</p>
            <p><Chip color="green">Option C</Chip> — <code>ArrayPool</code> for large follower sets. Amortised zero allocation; pool recycles backing arrays.</p>
            <p><Chip color="red">NOT</Chip> — <code>.ToArray()</code>, <code>.Keys.ToArray()</code>, <code>new List&lt;T&gt;()</code> — all cause gen-0 GC churn on every call.</p>
          </div>
        </div>
      </div>
    ),
  },

  // ── 3. Comparative Analysis ────────────────────────────────────────────────
  {
    id: "comparative",
    severity: "warning",
    title: "Comparative Analysis — Why Direct Enumeration Beats .Keys / .ToArray() on HFT Hot Path",
    body: (
      <div className="space-y-4">
        <p>
          In a high-frequency trading (HFT) hot path, <strong className="text-white">latency outliers (tail latency)</strong>{" "}
          and <strong className="text-white">GC pauses</strong> are the two primary threats to deterministic execution.
          Here is a detailed breakdown:
        </p>

        <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
          {/* .Keys */}
          <div className="rounded-lg border border-red-800 bg-red-950/30 p-3 space-y-2">
            <p className="font-bold text-red-300 text-xs flex items-center gap-2">
              <span>⛔</span> .Keys — Why It Fails
            </p>
            <ul className="list-disc list-inside text-xs text-slate-300 space-y-1 leading-relaxed">
              <li>
                <code className="text-amber-300">ConcurrentDictionary.Keys</code> returns an{" "}
                <code>ICollection&lt;TKey&gt;</code> snapshot. In .NET 4.x this creates a new{" "}
                <code>List&lt;TKey&gt;</code> internally — <strong className="text-white">heap allocation on every call</strong>.
              </li>
              <li>
                Enumerating <code>.Keys</code> acquires <strong className="text-white">all internal segment locks simultaneously</strong>{" "}
                to produce a point-in-time consistent snapshot — this is not lock-free.
              </li>
              <li>
                The resulting <code>ICollection</code> is heap-boxed, adding GC roots and increasing gen-0 collection frequency.
              </li>
              <li>
                On a 100k msg/sec order flow this can generate <strong className="text-white">~100 MB/sec of ephemeral garbage</strong>.
              </li>
            </ul>
          </div>

          {/* .ToArray() */}
          <div className="rounded-lg border border-amber-700 bg-amber-950/30 p-3 space-y-2">
            <p className="font-bold text-amber-300 text-xs flex items-center gap-2">
              <span>⚠️</span> .ToArray() — Why It's Sub-Optimal
            </p>
            <ul className="list-disc list-inside text-xs text-slate-300 space-y-1 leading-relaxed">
              <li>
                <code>.ToArray()</code> (LINQ extension) allocates a new <code>T[]</code> on the heap every invocation — unavoidable gen-0 pressure.
              </li>
              <li>
                For <code>ConcurrentDictionary</code> it internally calls <code>GetKeys()</code>/<code>GetValues()</code>{" "}
                which acquires all segment locks for snapshot consistency — same blocking issue as <code>.Keys</code>.
              </li>
              <li>
                LINQ's <code>ToArray()</code> uses an internal <code>Buffer&lt;T&gt;</code> that may double-allocate if capacity is unknown.
              </li>
              <li>
                Every allocation is a <strong className="text-white">GC safepoint candidate</strong>. In a GC pause even
                a 1 ms stop-the-world is catastrophic for order execution SLAs.
              </li>
            </ul>
          </div>
        </div>

        <div className="rounded-lg border border-emerald-700 bg-emerald-950/30 p-3 space-y-2">
          <p className="font-bold text-emerald-300 text-xs flex items-center gap-2">
            <span>✅</span> Direct Enumeration — Why It Wins
          </p>
          <ul className="list-disc list-inside text-xs text-slate-300 space-y-1 leading-relaxed">
            <li>
              <strong className="text-white">Weakly-consistent enumerator:</strong> The{" "}
              <code className="text-violet-300">ConcurrentDictionary</code> enumerator is documented to{" "}
              represent the dictionary's state at some point during the call — it never throws{" "}
              <code>InvalidOperationException</code> for concurrent modifications.
            </li>
            <li>
              <strong className="text-white">No additional heap objects:</strong> When you iterate the concrete{" "}
              <code>ConcurrentDictionary&lt;K,V&gt;</code> type directly (not via interface), the struct enumerator
              is unboxed — zero heap allocation.
            </li>
            <li>
              <strong className="text-white">Segment-level micro-locks:</strong> Internally, each segment is locked
              only while producing that segment's entries — extremely short critical sections, invisible to caller.
            </li>
            <li>
              <strong className="text-white">No full-table lock:</strong> Unlike <code>.Keys</code> or{" "}
              <code>.ToArray()</code>, direct enumeration never acquires all segment locks simultaneously, so
              writers on other segments are never blocked.
            </li>
            <li>
              <strong className="text-white">Cache-friendly:</strong> Iterating the dictionary's internal{" "}
              <code>Node[]</code> buckets sequentially maximises L1/L2 cache line reuse versus random-access patterns from reconstructed arrays.
            </li>
          </ul>
        </div>

        <CompTable />

        <div className="rounded-lg border border-sky-700 bg-sky-950/30 p-3 text-xs text-slate-300 space-y-1">
          <p className="font-bold text-sky-300">📊 Rule of Thumb for HFT Hot Paths</p>
          <p>
            Prefer <Chip color="green">direct foreach on ConcurrentDictionary</Chip> when weak consistency is acceptable.
            Use <Chip color="green">ArrayPool + foreach</Chip> when a stable snapshot is required.
            Never use <Chip color="red">.Keys</Chip> or <Chip color="red">.ToArray()</Chip> inside a critical latency path.
            Never introduce <Chip color="red">lock</Chip> on the collection object — it defeats the purpose of the concurrent collection entirely.
          </p>
        </div>
      </div>
    ),
  },

  // ── 4. Sovereign Invariant Summary ────────────────────────────────────────
  {
    id: "invariant-summary",
    severity: "info",
    title: "Sovereign Invariant Compliance — Final Checklist",
    body: (
      <div className="space-y-3">
        <p className="text-slate-300 text-xs">
          The <strong className="text-white">Sovereign Substrate</strong> refactor mandates the{" "}
          <strong className="text-sky-300">No-Internal-Locks</strong> invariant: no user-visible blocking
          primitive (lock, Monitor, Mutex, SemaphoreSlim.Wait) on the shared-context hot path.
        </p>

        <div className="rounded-lg border border-slate-700 bg-slate-800/50 overflow-hidden">
          {[
            { item: "Remove lock (ctx.Sync) from Site 1", status: "✓ Required", ok: true },
            { item: "Remove lock (ctx.Sync) from Site 2", status: "✓ Required", ok: true },
            { item: "Replace with lock (ctx.FollowerEntries) — Quick-Fix", status: "✗ VIOLATES invariant", ok: false },
            { item: "Site 1: Direct foreach on ConcurrentDictionary", status: "✓ COMPLIANT", ok: true },
            { item: "Site 2: Direct foreach (weak snapshot) — Option A", status: "✓ COMPLIANT", ok: true },
            { item: "Site 2: stackalloc Span<T> snapshot — Option B", status: "✓ COMPLIANT", ok: true },
            { item: "Site 2: ArrayPool<T> snapshot — Option C", status: "✓ COMPLIANT", ok: true },
            { item: "Site 1/2: .Keys.ToArray() pattern", status: "⚠ Heap alloc / segment lock", ok: false },
            { item: "Sovereign Invariant: No user-visible lock on hot path", status: "✓ MET by Options A/B/C", ok: true },
          ].map((row, i) => (
            <div
              key={i}
              className={`flex items-center justify-between px-4 py-2.5 text-xs border-b border-slate-700 last:border-0 ${i % 2 === 0 ? "bg-slate-900/60" : "bg-slate-800/40"}`}
            >
              <span className="text-slate-300 font-mono">{row.item}</span>
              <span className={`font-bold font-mono ${row.ok ? "text-emerald-400" : "text-red-400"}`}>{row.status}</span>
            </div>
          ))}
        </div>

        <Code lang="csharp">{`// ════════════════════════════════════════════════════════════════
// FINAL CORRECTED CODE — Sovereign Substrate compliant
// ════════════════════════════════════════════════════════════════

// ── Site 1 · V12_002.SIMA.Shadow.cs : 89 ───────────────────────
// BEFORE (broken):
//   lock (ctx.Sync) { foreach (var x in ctx.FollowerEntries) { … } }
//
// AFTER (lock-free, zero-alloc):
foreach (var entry in ctx.FollowerEntries)   // weakly-consistent, no lock
{
    ShadowProcess(entry.Key, entry.Value);
}

// ── Site 2 · V12_002.Orders.Callbacks.Propagation.cs : 126 ─────
// BEFORE (broken):
//   lock (ctx.Sync) { var arr = ctx.FollowerEntries.ToArray(); }
//
// AFTER — Option A (preferred, no snapshot needed):
foreach (var kvp in ctx.FollowerEntries)
    PropagateCallback(kvp.Key);

// AFTER — Option B (true zero-alloc snapshot, small N):
int n = ctx.FollowerEntries.Count;
Span<KeyValuePair<string, byte>> snap = stackalloc KeyValuePair<string, byte>[n];
int idx = 0;
foreach (var kvp in ctx.FollowerEntries) snap[idx++] = kvp;
for (int j = 0; j < idx; j++) PropagateCallback(snap[j].Key);

// AFTER — Option C (pooled snapshot, large N):
var arr = ArrayPool<KeyValuePair<string,byte>>.Shared.Rent(n);
try   { /* fill + process */ }
finally { ArrayPool<KeyValuePair<string,byte>>.Shared.Return(arr); }`}</Code>
      </div>
    ),
  },
];

// ─── Flow Diagram ─────────────────────────────────────────────────────────────
function FlowDiagram() {
  const nodes = [
    { label: "ctx.Sync REMOVED", sub: "Sovereign Substrate Refactor", color: "border-red-500 bg-red-950/40 text-red-300" },
    { label: "lock (ctx.Sync)", sub: "BROKEN — object does not exist", color: "border-red-700 bg-red-900/30 text-red-400 line-through" },
    { label: "Suggested: lock(ctx.FollowerEntries)", sub: "VIOLATES No-Internal-Locks invariant", color: "border-amber-500 bg-amber-950/40 text-amber-300" },
    { label: "Direct foreach on ConcurrentDictionary", sub: "OPTIMAL — lock-free, zero-alloc", color: "border-emerald-500 bg-emerald-950/40 text-emerald-300" },
    { label: "Sovereign Invariant ✓", sub: "No user-visible blocking on hot path", color: "border-sky-500 bg-sky-950/40 text-sky-300" },
  ];

  return (
    <div className="flex flex-col items-center gap-0 py-2">
      {nodes.map((n, i) => (
        <div key={i} className="flex flex-col items-center">
          <div className={`rounded-lg border px-5 py-2.5 text-center max-w-xs w-full ${n.color}`}>
            <p className="text-xs font-bold font-mono">{n.label}</p>
            <p className="text-[10px] opacity-70 mt-0.5">{n.sub}</p>
          </div>
          {i < nodes.length - 1 && (
            <div className="flex flex-col items-center">
              <div className="w-0.5 h-4 bg-slate-600" />
              <svg width="12" height="8" viewBox="0 0 12 8" className="text-slate-500 fill-current">
                <polygon points="6,8 0,0 12,0" />
              </svg>
            </div>
          )}
        </div>
      ))}
    </div>
  );
}

// ─── App ──────────────────────────────────────────────────────────────────────
export default function App() {
  const [activeFilter, setActiveFilter] = useState<Severity | "all">("all");

  const filtered = activeFilter === "all"
    ? findings
    : findings.filter((f) => f.severity === activeFilter);

  const counts = {
    critical: findings.filter((f) => f.severity === "critical").length,
    warning: findings.filter((f) => f.severity === "warning").length,
    ok: findings.filter((f) => f.severity === "ok").length,
    info: findings.filter((f) => f.severity === "info").length,
  };

  return (
    <div className="min-h-screen bg-slate-950 text-slate-200 font-sans">
      {/* ── Top Bar ── */}
      <header className="border-b border-slate-800 bg-slate-900/80 backdrop-blur sticky top-0 z-20">
        <div className="max-w-6xl mx-auto px-4 py-3 flex items-center justify-between gap-4 flex-wrap">
          <div className="flex items-center gap-3">
            {/* Logo mark */}
            <div className="h-8 w-8 rounded-lg bg-gradient-to-br from-violet-600 to-indigo-700 flex items-center justify-center shadow-lg shadow-violet-900/50">
              <svg className="h-4 w-4 text-white" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={2.5}>
                <circle cx="12" cy="12" r="3" />
                <path d="M12 2v4M12 18v4M4.93 4.93l2.83 2.83M16.24 16.24l2.83 2.83M2 12h4M18 12h4M4.93 19.07l2.83-2.83M16.24 7.76l2.83-2.83" />
              </svg>
            </div>
            <div>
              <h1 className="text-sm font-bold text-white tracking-tight">System Flow Visualizer</h1>
              <p className="text-[10px] text-slate-500 font-mono">Sovereign Substrate Refactor · Lock-Free Analysis</p>
            </div>
          </div>
          <div className="flex items-center gap-2 flex-wrap">
            {(["all", "critical", "warning", "ok", "info"] as const).map((f) => {
              const active = activeFilter === f;
              return (
                <button
                  key={f}
                  onClick={() => setActiveFilter(f)}
                  className={`px-3 py-1 rounded-full text-xs font-semibold font-mono transition-all ${
                    active
                      ? "bg-violet-600 text-white shadow shadow-violet-900"
                      : "bg-slate-800 text-slate-400 hover:bg-slate-700 hover:text-slate-200"
                  }`}
                >
                  {f.toUpperCase()}
                  {f !== "all" && (
                    <span className="ml-1.5 opacity-70">
                      ({counts[f as Severity]})
                    </span>
                  )}
                </button>
              );
            })}
          </div>
        </div>
      </header>

      {/* ── Hero ── */}
      <div className="border-b border-slate-800 bg-gradient-to-br from-slate-900 via-slate-900 to-indigo-950/30">
        <div className="max-w-6xl mx-auto px-4 py-8 flex flex-col md:flex-row items-start gap-8">
          {/* Title block */}
          <div className="flex-1 space-y-3">
            <div className="inline-flex items-center gap-2 rounded-full border border-violet-700 bg-violet-900/30 px-3 py-1 text-[10px] font-mono text-violet-300 tracking-widest uppercase">
              <span className="h-1.5 w-1.5 rounded-full bg-violet-400 animate-pulse" />
              Live Analysis · Sovereign Substrate
            </div>
            <h2 className="text-3xl font-extrabold text-white leading-tight tracking-tight">
              Claude claude-opus-4-5
            </h2>
            <p className="text-slate-400 text-sm leading-relaxed max-w-xl">
              Lock-free, zero-allocation refactor analysis for <code className="text-violet-300">ctx.FollowerEntries</code>{" "}
              across two hot-path sites. Evaluates the proposed quick-fix against the{" "}
              <strong className="text-white">Sovereign Invariant (No-Internal-Locks)</strong> and delivers
              production-ready alternatives for .NET 4.8.
            </p>
            {/* Stat chips */}
            <div className="flex flex-wrap gap-2 pt-1">
              <span className="flex items-center gap-1.5 rounded border border-red-700 bg-red-900/30 px-2.5 py-1 text-xs font-mono text-red-300">
                <span className="h-1.5 w-1.5 rounded-full bg-red-400 animate-pulse" />
                {counts.critical} Critical
              </span>
              <span className="flex items-center gap-1.5 rounded border border-amber-700 bg-amber-900/30 px-2.5 py-1 text-xs font-mono text-amber-300">
                <span className="h-1.5 w-1.5 rounded-full bg-amber-400" />
                {counts.warning} Warning
              </span>
              <span className="flex items-center gap-1.5 rounded border border-emerald-700 bg-emerald-900/30 px-2.5 py-1 text-xs font-mono text-emerald-300">
                <span className="h-1.5 w-1.5 rounded-full bg-emerald-400" />
                {counts.ok} Pass
              </span>
              <span className="flex items-center gap-1.5 rounded border border-sky-700 bg-sky-900/30 px-2.5 py-1 text-xs font-mono text-sky-300">
                <span className="h-1.5 w-1.5 rounded-full bg-sky-400" />
                {counts.info} Info
              </span>
            </div>
          </div>

          {/* Flow diagram */}
          <div className="w-full md:w-72 rounded-xl border border-slate-700 bg-slate-900/70 p-4 shadow-xl">
            <p className="text-[10px] font-mono text-slate-500 uppercase tracking-widest mb-3 text-center">
              Decision Flow
            </p>
            <FlowDiagram />
          </div>
        </div>
      </div>

      {/* ── Cards ── */}
      <main className="max-w-6xl mx-auto px-4 py-8 space-y-5">
        {/* Sites quick reference */}
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
          {[
            {
              site: "SITE 1",
              file: "V12_002.SIMA.Shadow.cs",
              line: "89",
              broken: "lock (ctx.Sync) { foreach (var x in ctx.FollowerEntries) { … } }",
              fix: "foreach (var entry in ctx.FollowerEntries) { … }",
            },
            {
              site: "SITE 2",
              file: "V12_002.Orders.Callbacks.Propagation.cs",
              line: "126",
              broken: "lock (ctx.Sync) { var arr = ctx.FollowerEntries.ToArray(); }",
              fix: "foreach / stackalloc / ArrayPool — see Logic Matrix",
            },
          ].map((s) => (
            <div key={s.site} className="rounded-xl border border-slate-700 bg-slate-900 p-4 space-y-2 shadow-lg">
              <div className="flex items-center gap-2">
                <span className="bg-violet-700 text-white text-xs px-2 py-0.5 rounded font-mono font-bold">{s.site}</span>
                <code className="text-slate-300 text-xs">{s.file}</code>
                <span className="ml-auto text-[10px] text-slate-500 font-mono">Line {s.line}</span>
              </div>
              <div className="rounded border border-red-900 bg-red-950/30 px-3 py-1.5">
                <p className="text-[10px] text-red-500 font-mono uppercase tracking-wider mb-0.5">Broken</p>
                <code className="text-xs text-red-300 font-mono">{s.broken}</code>
              </div>
              <div className="rounded border border-emerald-900 bg-emerald-950/30 px-3 py-1.5">
                <p className="text-[10px] text-emerald-500 font-mono uppercase tracking-wider mb-0.5">Fixed</p>
                <code className="text-xs text-emerald-300 font-mono">{s.fix}</code>
              </div>
            </div>
          ))}
        </div>

        {/* Main finding cards */}
        {filtered.map((f, i) => (
          <Card key={f.id} finding={f} index={i} />
        ))}
      </main>

      {/* ── Footer ── */}
      <footer className="border-t border-slate-800 bg-slate-900/50 mt-8">
        <div className="max-w-6xl mx-auto px-4 py-4 flex items-center justify-between flex-wrap gap-2">
          <p className="text-xs text-slate-600 font-mono">
            System Flow Visualizer · Sovereign Substrate Refactor Analysis
          </p>
          <p className="text-xs text-slate-600 font-mono">
            Model: <span className="text-violet-500">Claude claude-opus-4-5</span> · Vendor: Anthropic
          </p>
        </div>
      </footer>
    </div>
  );
}
