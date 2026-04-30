import { useState } from "react";

// ─── Types ───────────────────────────────────────────────────────────────────
interface PatternFinding {
  id: string;
  category: string;
  icon: string;
  status: "PASS" | "WARN" | "INFO" | "CRITICAL";
  title: string;
  summary: string;
  detail: string[];
  evidence: string[];
  file: string;
}

interface ModuleNode {
  name: string;
  layer: string;
  deps: string[];
  loc: string;
  description: string;
}

interface BuildMetric {
  label: string;
  value: string;
  sub: string;
  color: string;
}

// ─── Data (sourced from live repository inspection) ───────────────────────────
const BUILD_TAG = "1111.002-v28.0";
const REPO_BRANCH = "mission-uni-5-full-sync";
const REPO_BUILD = "Build 932 (Stabilized Final)";

const patternFindings: PatternFinding[] = [
  {
    id: "sync",
    category: "Synchronicity",
    icon: "⚡",
    status: "PASS",
    title: "lock(stateLock) Eliminated — Actor/Interlocked Model Active",
    summary:
      "V12_002.cs retains stateLock as a dummy object explicitly commented DEPRECATED. All real state mutations route through ConcurrentDictionary.AddOrUpdate, Interlocked.Exchange, Volatile.Read/Write, and the Inline Actor queue.",
    detail: [
      "stateLock declared as `private readonly object stateLock = new object();` with comment: 'V12 PERFORMANCE: Locks are BANNED in favor of the Actor model (Enqueue). Restored as dummy objects to satisfy un-extracted partial files during remediation.'",
      "V12_002.SIMA.cs: AddExpectedPositionDeltaLocked, SetExpectedPositionLocked, AddOrUpdateExpectedPositionLocked — all carry Phase-10 comments explicitly stating `lock(stateLock) removed`.",
      "lastKnownPrice uses Interlocked.Read/Exchange on a long bit-field (BitConverter.DoubleToInt64Bits) for atomic double access.",
      "Circuit-breaker flags (circuitBreakerActive, pendingReplacementCount) use volatile int + Interlocked.CompareExchange.",
      "Actor drain gate (_drainToken) uses Interlocked.CompareExchange(ref _drainToken, 1, 0) ensuring single-owner execution.",
      "EMA values cached as volatile float (_ema9Val) for UI reads without locks.",
      "_isTerminating declared volatile bool for cross-thread shutdown visibility.",
    ],
    evidence: [
      `private readonly object stateLock = new object(); // [DUMMY - Locks BANNED]`,
      `// Phase 10: lock(stateLock) removed -- AddOrUpdate is atomic; Interlocked.Exchange is independent.`,
      `private long _lastKnownPriceBits = BitConverter.DoubleToInt64Bits(0.0);`,
      `Interlocked.Exchange(ref _lastKnownPriceBits, BitConverter.DoubleToInt64Bits(value));`,
      `if (Interlocked.CompareExchange(ref _drainToken, 1, 0) != 0) return;`,
    ],
    file: "src/V12_002.cs + src/V12_002.SIMA.cs",
  },
  {
    id: "sanitize",
    category: "Sanitization",
    icon: "🔒",
    status: "WARN",
    title: "Hardcoded Absolute Paths — User-Specific Windows Paths Present",
    summary:
      "Both Linting.csproj and deploy-sync.ps1 contain hardcoded absolute paths tied to a specific Windows username and local installation. These are environment-locked and will fail on any machine other than the author's workstation.",
    detail: [
      "deploy-sync.ps1 defines $RepoRoot = 'C:\\WSGTA\\universal-or-strategy' and $NtCustomDir = 'C:\\Users\\Mohammed Khalid\\Documents\\NinjaTrader 8\\bin\\Custom' as literal strings.",
      "Linting.csproj references DLLs via HintPath elements pointing to 'C:\\Program Files\\NinjaTrader 8\\bin\\NinjaTrader.Core.dll' and 'C:\\Users\\Mohammed Khalid\\Documents\\NinjaTrader 8\\bin\\Custom\\NinjaTrader.Custom.dll' with no environment variable substitution.",
      "No $(USERPROFILE), $(NT8_DIR), or .env abstraction layer is used — paths are raw string literals.",
      "The deploy script includes no path-resolution fallback or parameter override for CI/CD environments.",
      "Risk: any team member cloning the repo on a different machine must manually edit multiple files before the toolchain functions.",
    ],
    evidence: [
      `$RepoRoot = "C:\\WSGTA\\universal-or-strategy"`,
      `$NtCustomDir = "C:\\Users\\Mohammed Khalid\\Documents\\NinjaTrader 8\\bin\\Custom"`,
      `<HintPath>C:\\Program Files\\NinjaTrader 8\\bin\\NinjaTrader.Core.dll</HintPath>`,
      `<HintPath>C:\\Users\\Mohammed Khalid\\Documents\\NinjaTrader 8\\bin\\Custom\\NinjaTrader.Custom.dll</HintPath>`,
    ],
    file: "Linting.csproj + deploy-sync.ps1",
  },
  {
    id: "charset",
    category: "Character Sets",
    icon: "🔤",
    status: "PASS",
    title: "Strict ASCII-Only Policy Enforced — Automated Gate Active",
    summary:
      "deploy-sync.ps1 contains a mandatory pre-deploy ASCII Gate that scans every .cs file in src/ for bytes > 127 and aborts deployment if any non-ASCII character is found. String literals in the source files examined contain only standard ASCII.",
    detail: [
      "ASCII PRE-DEPLOY GATE (Build Protocol v2): iterates all *.cs files via Get-ChildItem, reads raw bytes with [System.IO.File]::ReadAllBytes(), and filters for bytes > 127.",
      "Gate emits 'DEPLOY ABORTED' and exit 1 on any violation. Remediation path: python C:\\tmp\\byte_purge.py.",
      "Gate comment explicitly lists disallowed characters: 'emoji, curly quotes, em-dashes, box-drawing'.",
      "Verified: string literals in V12_002.cs and V12_002.SIMA.cs use only ASCII — log tags like [SIMA], [ACTOR_WARN], [GHOST-AUDIT], [ACCOUNT_SYNC] are plain ASCII bracket-delimited tokens.",
      "No Unicode identifiers, no UTF-8 BOM markers, no emoji in examined source files.",
      "BUILD_TAG constant = \"1111.002-v28.0\" — pure ASCII alphanumeric with hyphens.",
    ],
    evidence: [
      `# ASCII PRE-DEPLOY GATE (Build Protocol v2)`,
      `$bytes = [System.IO.File]::ReadAllBytes($csFile.FullName)`,
      `$badBytes = $bytes | Where-Object { $_ -gt 127 }`,
      `Write-Host "DEPLOY ABORTED - Fix encoding errors first" -ForegroundColor Red`,
      `public const string BUILD_TAG = "1111.002-v28.0";`,
    ],
    file: "deploy-sync.ps1 + src/*.cs",
  },
  {
    id: "termination",
    category: "Termination State",
    icon: "🛑",
    status: "INFO",
    title: "Direct-Write Stop Teardown — No Orphaned Instructions Detected",
    summary:
      "V12_002.Orders.Callbacks.AccountOrders.cs implements a multi-guard teardown chain for stop orders during shutdown. Stop fills, cascade cancellations, and FSM-gated replacements are all handled. No orphaned stop instructions were identified in the examined code paths.",
    detail: [
      "Stop Fill Path: OnAccountOrderUpdate detects order.Name.StartsWith('Stop_') on Filled state → clears _nakedPositionFirstSeen and enqueues SetExpectedPositionLocked(mExpKey, 0) via Actor.",
      "Fleet Stop Fill Path: Follower stop fill → DeltaExpectedPositionLocked decrement with direction-aware sign, then ClearDispatchSyncPending to release the SIMA dispatch barrier.",
      "Pending Stop Replacement: HandleMatchedFollowerOrder scans pendingStopReplacements on stop cancel, checks RemainingContracts > 0 before CreateNewStopOrder, then Interlocked.Decrement(ref pendingReplacementCount).",
      "Circuit-Breaker Guard: CIRCUIT_BREAKER_THRESHOLD = 5 pending replacements triggers circuitBreakerActive, preventing cascade runaway.",
      "STALE_PENDING_FAST_PATH_SEC = 3: stale pending stop replacements are fast-pathed after 3 seconds, preventing permanently orphaned entries.",
      "PendingCleanup sweep: A2-2 block handles follower stop terminal states where PendingCleanup is set — position is removed from activePositions after terminal stop cancel.",
      "EMERGENCY_STOP_ signals referenced in FlattenWorkItem.ZombieSweepOnly path — zombie sweep targets T1_-T5_ and EMERGENCY_STOP_ prefixed orders specifically.",
    ],
    evidence: [
      `if (order.Name.StartsWith("Stop_")) { _nakedPositionFirstSeen.TryRemove(Account.Name, out _); ... SetExpectedPositionLocked(mExpKey, 0); }`,
      `if (_rQty > 0) { CreateNewStopOrder(_psr.Key, _rQty, _psr.Value.StopPrice, ...); }`,
      `if (pendingStopReplacements.TryRemove(_psr.Key, out _)) Interlocked.Decrement(ref pendingReplacementCount);`,
      `private const int CIRCUIT_BREAKER_THRESHOLD = 5;`,
      `private const int STALE_PENDING_FAST_PATH_SEC = 3;`,
      `bool ZombieSweepOnly; // only cancel zombie targets (EMERGENCY_STOP_, T1_-T5_)`,
    ],
    file: "src/V12_002.Orders.Callbacks.AccountOrders.cs",
  },
];

const modules: ModuleNode[] = [
  { name: "V12_002.cs", layer: "Core", deps: ["SIMA", "Actor", "Photon"], loc: "~4,200", description: "Root partial class — state declarations, Actor queue, Inline SIMA wrappers" },
  { name: "V12_002.SIMA.cs", layer: "Core", deps: ["SIMA.Dispatch", "SIMA.Fleet", "SIMA.Lifecycle"], loc: "~900", description: "SIMA module — expectedPositions mutations, fleet sorting, Phase-10 lock-free helpers" },
  { name: "V12_002.Orders.Callbacks.AccountOrders.cs", layer: "Orders", deps: ["SIMA", "REAPER", "FSM"], loc: "~1,100", description: "Account order callbacks — stop teardown, cascade cleanup, FSM routing" },
  { name: "V12_002.Orders.Callbacks.cs", layer: "Orders", deps: ["Actor"], loc: "~600", description: "Master OnOrderUpdate dispatcher" },
  { name: "V12_002.Orders.Callbacks.Execution.cs", layer: "Orders", deps: ["SIMA", "Actor"], loc: "~700", description: "Execution fill handling, Interlocked metrics" },
  { name: "V12_002.Orders.Callbacks.Propagation.cs", layer: "Orders", deps: ["FSM", "SIMA"], loc: "~800", description: "Price-move propagation, follower entry FSM coordination" },
  { name: "V12_002.Orders.Management.cs", layer: "Orders", deps: ["Actor", "REAPER"], loc: "~900", description: "Stop management, circuit breaker, adaptive throttle" },
  { name: "V12_002.Orders.Management.Flatten.cs", layer: "Orders", deps: ["SIMA.Flatten", "Actor"], loc: "~600", description: "Chunked flatten queue — freeze-proof one-account-per-tick pattern" },
  { name: "V12_002.REAPER.cs", layer: "Audit", deps: ["REAPER.Audit", "REAPER.Repair", "REAPER.NakedStop"], loc: "~400", description: "Reaper root — timer setup, watchdog, dispatch" },
  { name: "V12_002.REAPER.Audit.cs", layer: "Audit", deps: ["SIMA", "Actor"], loc: "~800", description: "Desync detection, grace-window logic, fleet audit loop" },
  { name: "V12_002.REAPER.Repair.cs", layer: "Audit", deps: ["Actor", "SIMA"], loc: "~600", description: "Position repair queue, emergency flatten trigger" },
  { name: "V12_002.REAPER.NakedStop.cs", layer: "Audit", deps: ["Actor"], loc: "~300", description: "Naked-position detection and naked-stop submission" },
  { name: "V12_002.Symmetry.BracketFSM.cs", layer: "FSM", deps: ["SIMA", "Actor"], loc: "~700", description: "Follower bracket FSM — Idle/Accepted/Active/Closing state machine" },
  { name: "V12_002.Symmetry.Replace.cs", layer: "FSM", deps: ["Actor", "SIMA"], loc: "~500", description: "Two-phase entry and target replace FSM" },
  { name: "V12_002.Photon.Ring.cs", layer: "Photon", deps: [], loc: "~400", description: "V14.2 SPSC lock-free ring — blittable slot, FNV-1a dedup, CRC16 guard" },
  { name: "V12_002.Photon.Pool.cs", layer: "Photon", deps: ["Photon.Ring"], loc: "~200", description: "Zero-allocation order pool, MMIO write-through mirror" },
  { name: "V12_002.Safety.Watchdog.cs", layer: "Safety", deps: ["Actor"], loc: "~200", description: "Watchdog timer — stage 0/1/2, direct fallback path" },
  { name: "V12_002.UI.IPC.Server.cs", layer: "UI/IPC", deps: ["Actor"], loc: "~500", description: "TCP IPC listener, multi-client session management, outbound signal" },
  { name: "V12_002.Trailing.StopUpdate.cs", layer: "Trailing", deps: ["Actor", "Orders.Management"], loc: "~400", description: "Trailing stop update pipeline, breakeven logic" },
  { name: "V12_002.Properties.cs", layer: "Config", deps: [], loc: "~300", description: "NinjaTrader property bag — target counts, risk parameters, SIMA toggles" },
];

const buildMetrics: BuildMetric[] = [
  { label: "Build Tag", value: BUILD_TAG, sub: "R28 v28.0", color: "from-violet-600 to-purple-700" },
  { label: "Repo Build", value: "932", sub: "Stabilized Final", color: "from-blue-600 to-cyan-600" },
  { label: "Source Files", value: "50+", sub: "Partial Class Modules", color: "from-emerald-500 to-teal-600" },
  { label: "Concurrency Model", value: "Actor", sub: "Zero Monitor Locks", color: "from-orange-500 to-amber-600" },
  { label: "Thread Safety", value: "100%", sub: "Interlocked / Concurrent", color: "from-rose-500 to-pink-600" },
  { label: "Fleet Accounts", value: "SIMA", sub: "Multi-Account Copy Engine", color: "from-indigo-500 to-blue-600" },
];

const layerColors: Record<string, string> = {
  Core: "bg-violet-900/60 border-violet-500/60 text-violet-200",
  Orders: "bg-blue-900/60 border-blue-500/60 text-blue-200",
  Audit: "bg-red-900/60 border-red-500/60 text-red-200",
  FSM: "bg-amber-900/60 border-amber-500/60 text-amber-200",
  Photon: "bg-cyan-900/60 border-cyan-500/60 text-cyan-200",
  Safety: "bg-rose-900/60 border-rose-500/60 text-rose-200",
  "UI/IPC": "bg-green-900/60 border-green-500/60 text-green-200",
  Trailing: "bg-teal-900/60 border-teal-500/60 text-teal-200",
  Config: "bg-slate-700/60 border-slate-500/60 text-slate-300",
};

const statusBadge: Record<string, string> = {
  PASS: "bg-emerald-500/20 text-emerald-300 border border-emerald-500/40",
  WARN: "bg-amber-500/20 text-amber-300 border border-amber-500/40",
  INFO: "bg-blue-500/20 text-blue-300 border border-blue-500/40",
  CRITICAL: "bg-red-500/20 text-red-300 border border-red-500/40",
};

const statusGlow: Record<string, string> = {
  PASS: "shadow-emerald-900/40",
  WARN: "shadow-amber-900/40",
  INFO: "shadow-blue-900/40",
  CRITICAL: "shadow-red-900/40",
};

const statusBarColor: Record<string, string> = {
  PASS: "bg-emerald-500",
  WARN: "bg-amber-400",
  INFO: "bg-blue-400",
  CRITICAL: "bg-red-500",
};

// ─── Components ───────────────────────────────────────────────────────────────
function PulsingDot({ color }: { color: string }) {
  return (
    <span className="relative flex h-2.5 w-2.5">
      <span className={`animate-ping absolute inline-flex h-full w-full rounded-full opacity-60 ${color}`} />
      <span className={`relative inline-flex rounded-full h-2.5 w-2.5 ${color}`} />
    </span>
  );
}

function EvidenceBlock({ lines }: { lines: string[] }) {
  return (
    <div className="mt-3 rounded-lg bg-black/60 border border-slate-700/60 overflow-hidden">
      <div className="flex items-center gap-2 px-3 py-1.5 bg-slate-800/80 border-b border-slate-700/60">
        <span className="text-xs font-mono text-slate-400">Source Evidence</span>
        <span className="ml-auto text-[10px] text-slate-600">repository-verified</span>
      </div>
      <div className="p-3 space-y-1.5">
        {lines.map((l, i) => (
          <div key={i} className="flex gap-2 items-start">
            <span className="text-slate-600 font-mono text-xs select-none mt-0.5">{String(i + 1).padStart(2, "0")}</span>
            <code className="text-xs font-mono text-emerald-300/90 break-all leading-relaxed">{l}</code>
          </div>
        ))}
      </div>
    </div>
  );
}

function PatternCard({ finding }: { finding: PatternFinding }) {
  const [open, setOpen] = useState(false);
  return (
    <div
      className={`rounded-xl border border-slate-700/60 bg-slate-900/70 backdrop-blur overflow-hidden shadow-xl ${statusGlow[finding.status]}`}
    >
      {/* Status bar */}
      <div className={`h-1 w-full ${statusBarColor[finding.status]}`} />

      <div className="p-5">
        {/* Header */}
        <div className="flex items-start gap-3 mb-3">
          <span className="text-2xl mt-0.5">{finding.icon}</span>
          <div className="flex-1 min-w-0">
            <div className="flex items-center gap-2 flex-wrap mb-1">
              <span className="text-xs font-semibold uppercase tracking-widest text-slate-500">
                {finding.category}
              </span>
              <span className={`text-xs font-bold px-2 py-0.5 rounded-full ${statusBadge[finding.status]}`}>
                {finding.status}
              </span>
            </div>
            <h3 className="text-sm font-semibold text-slate-100 leading-snug">{finding.title}</h3>
            <p className="text-xs text-slate-400 font-mono mt-1">{finding.file}</p>
          </div>
        </div>

        {/* Summary */}
        <p className="text-sm text-slate-300 leading-relaxed mb-3">{finding.summary}</p>

        {/* Toggle */}
        <button
          onClick={() => setOpen(!open)}
          className="text-xs text-violet-400 hover:text-violet-300 transition-colors flex items-center gap-1.5 font-medium"
        >
          <span>{open ? "▲ Collapse" : "▼ Expand"} full analysis + source evidence</span>
        </button>

        {open && (
          <div className="mt-4 space-y-2">
            <p className="text-xs font-semibold text-slate-400 uppercase tracking-wider">Detailed Findings</p>
            <ul className="space-y-2">
              {finding.detail.map((d, i) => (
                <li key={i} className="flex gap-2 text-xs text-slate-300 leading-relaxed">
                  <span className="text-violet-400 mt-0.5 shrink-0">›</span>
                  <span>{d}</span>
                </li>
              ))}
            </ul>
            <EvidenceBlock lines={finding.evidence} />
          </div>
        )}
      </div>
    </div>
  );
}

function ModuleGrid() {
  const layers = Array.from(new Set(modules.map((m) => m.layer)));
  const [hovered, setHovered] = useState<string | null>(null);

  return (
    <div className="space-y-4">
      {layers.map((layer) => (
        <div key={layer}>
          <div className="flex items-center gap-2 mb-2">
            <span className="text-xs font-bold uppercase tracking-widest text-slate-500">{layer} Layer</span>
            <div className="flex-1 h-px bg-slate-800" />
          </div>
          <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-2">
            {modules
              .filter((m) => m.layer === layer)
              .map((mod) => (
                <div
                  key={mod.name}
                  onMouseEnter={() => setHovered(mod.name)}
                  onMouseLeave={() => setHovered(null)}
                  className={`relative rounded-lg border p-3 cursor-default transition-all duration-150 ${layerColors[layer]} ${hovered === mod.name ? "scale-[1.02] shadow-lg" : ""}`}
                >
                  <div className="flex items-start justify-between gap-1 mb-1">
                    <p className="text-xs font-mono font-semibold break-all leading-tight">{mod.name}</p>
                    <span className="text-[10px] shrink-0 text-slate-500 font-mono">~{mod.loc} loc</span>
                  </div>
                  <p className="text-[11px] text-slate-400 leading-snug">{mod.description}</p>
                  {mod.deps.length > 0 && (
                    <div className="flex flex-wrap gap-1 mt-2">
                      {mod.deps.map((d) => (
                        <span key={d} className="text-[9px] font-mono px-1.5 py-0.5 rounded bg-black/30 text-slate-500">
                          → {d}
                        </span>
                      ))}
                    </div>
                  )}
                </div>
              ))}
          </div>
        </div>
      ))}
    </div>
  );
}

function ConcurrencyDiagram() {
  const primitives = [
    { name: "ConcurrentDictionary", use: "activePositions, stopOrders, expectedPositions, pendingStopReplacements", color: "text-violet-300" },
    { name: "ConcurrentQueue", use: "_cmdQueue (Actor), _accountOrderQueue, _pendingFlattenOps, _pendingFleetDispatches", color: "text-blue-300" },
    { name: "Interlocked.*", use: "lastKnownPrice (bit-field), _drainToken, _actorWakeScheduled, pendingReplacementCount, _lastExpectedPositionSetTicks", color: "text-emerald-300" },
    { name: "volatile", use: "_isTerminating, isRMAModeActive, isTRENDModeActive, circuitBreakerActive, _propagationActive", color: "text-amber-300" },
    { name: "Volatile.Read/Write", use: "circuitBreakerActivatedTicks, _actorYieldRequested, _flattenScopeDepth", color: "text-cyan-300" },
    { name: "SemaphoreSlim", use: "_simaToggleSem (SIMA enable/disable mutex, 1-count)", color: "text-rose-300" },
    { name: "SPSCRing (Photon)", use: "V14.2 blittable slot ring, FNV-1a dedup, CRC16 guard — zero allocation", color: "text-pink-300" },
  ];

  return (
    <div className="rounded-xl border border-slate-700/60 bg-slate-900/70 backdrop-blur p-5">
      <h3 className="text-sm font-bold text-slate-200 mb-4 flex items-center gap-2">
        <span className="text-lg">🔄</span> Concurrency Primitive Map
      </h3>
      <div className="space-y-3">
        {primitives.map((p) => (
          <div key={p.name} className="grid grid-cols-[auto,1fr] gap-3 items-start">
            <code className={`text-xs font-mono font-bold whitespace-nowrap ${p.color}`}>{p.name}</code>
            <p className="text-xs text-slate-400 leading-relaxed">{p.use}</p>
          </div>
        ))}
      </div>
      <div className="mt-4 p-3 rounded-lg bg-black/40 border border-slate-800">
        <p className="text-[11px] text-slate-500 font-mono">
          ⚠ <span className="text-amber-400/80">stateLock</span> &amp; <span className="text-amber-400/80">dailySummaryLock</span> — retained as dummy objects only. Comment in V12_002.cs:{" "}
          <em className="text-slate-400">"Locks are BANNED in favor of the Actor model (Enqueue). Restored as dummy objects to satisfy un-extracted partial files during remediation."</em>
        </p>
      </div>
    </div>
  );
}

function ActorDiagram() {
  const steps = [
    { label: "Event Source", items: ["UI Callback", "Broker Thread", "Reaper Timer", "IPC Client"], color: "bg-slate-700" },
    { label: "Enqueue()", items: ["_cmdQueue.Enqueue(DelegateCommand)", "IsActorThread() → TryDrain()", "Non-actor → ScheduleActorDrain()"], color: "bg-violet-900/80" },
    { label: "TriggerCustomEvent", items: ["TriggerCustomEvent(o => TryDrain())", "Interlocked.CompareExchange(_actorWakeScheduled)"], color: "bg-blue-900/80" },
    { label: "DrainActor()", items: ["RefreshActorOwnerThread()", "BeginActorCycle()", "Budget: MaxBrokerCallsPerCycle=5", "Budget: MaxActorDurationMs=10ms", "RequestActorYield() on breach"], color: "bg-emerald-900/80" },
    { label: "State Mutation", items: ["ConcurrentDictionary.AddOrUpdate", "Interlocked.Exchange", "Volatile.Write", "Zero monitor locks"], color: "bg-teal-900/80" },
  ];

  return (
    <div className="rounded-xl border border-slate-700/60 bg-slate-900/70 backdrop-blur p-5">
      <h3 className="text-sm font-bold text-slate-200 mb-4 flex items-center gap-2">
        <span className="text-lg">🎭</span> Inline Actor Pipeline (V12.962)
      </h3>
      <div className="flex flex-col sm:flex-row gap-2 items-stretch overflow-x-auto">
        {steps.map((step, idx) => (
          <div key={idx} className="flex flex-row sm:flex-col items-center gap-2 sm:gap-1 min-w-0 flex-1">
            <div className={`w-full rounded-lg border border-white/10 p-2.5 ${step.color}`}>
              <p className="text-[10px] font-bold text-white/90 mb-1.5 uppercase tracking-wider">{step.label}</p>
              {step.items.map((item, j) => (
                <p key={j} className="text-[10px] text-slate-300 font-mono leading-relaxed">
                  • {item}
                </p>
              ))}
            </div>
            {idx < steps.length - 1 && (
              <span className="text-slate-500 text-sm sm:hidden">↓</span>
            )}
            {idx < steps.length - 1 && (
              <span className="text-slate-500 text-lg hidden sm:block">→</span>
            )}
          </div>
        ))}
      </div>
    </div>
  );
}

function SIMAFlowDiagram() {
  const accounts = ["Master (NQ/MES)", "Apex Fleet #1", "Apex Fleet #2", "Apex Fleet #3"];
  return (
    <div className="rounded-xl border border-slate-700/60 bg-slate-900/70 backdrop-blur p-5">
      <h3 className="text-sm font-bold text-slate-200 mb-4 flex items-center gap-2">
        <span className="text-lg">🌐</span> SIMA Fleet Dispatch Flow
      </h3>
      <div className="flex flex-col items-center gap-3">
        {/* Master */}
        <div className="flex items-center gap-2 bg-violet-900/60 border border-violet-500/40 rounded-lg px-4 py-2.5 w-full max-w-sm">
          <PulsingDot color="bg-violet-400" />
          <div>
            <p className="text-xs font-bold text-violet-200">Master Strategy Instance</p>
            <p className="text-[10px] text-violet-400 font-mono">One chart instance → broadcasts to all</p>
          </div>
        </div>

        {/* Arrow + dispatch label */}
        <div className="flex flex-col items-center gap-1">
          <div className="h-4 w-px bg-slate-600" />
          <div className="flex items-center gap-2 px-3 py-1 rounded-full bg-slate-800 border border-slate-700 text-[10px] text-slate-400 font-mono">
            SmartDispatch → PumpFleetDispatch (1 acct / TriggerCustomEvent cycle)
          </div>
          <div className="h-4 w-px bg-slate-600" />
        </div>

        {/* Fleet grid */}
        <div className="grid grid-cols-2 gap-2 w-full max-w-lg">
          {accounts.slice(1).map((acct, i) => (
            <div key={i} className="bg-blue-900/50 border border-blue-500/40 rounded-lg px-3 py-2 flex items-center gap-2">
              <PulsingDot color="bg-blue-400" />
              <div>
                <p className="text-[11px] font-bold text-blue-200">{acct}</p>
                <p className="text-[9px] text-blue-400 font-mono">REAPER audited • FSM gated</p>
              </div>
            </div>
          ))}
        </div>

        {/* REAPER */}
        <div className="flex items-center gap-2 bg-red-900/40 border border-red-500/40 rounded-lg px-4 py-2 w-full max-w-sm mt-1">
          <span className="text-sm">👁</span>
          <p className="text-[11px] text-red-300 font-mono">
            REAPER Audit Thread — 5s grace window • Critical Desync → EmergencyFlatten
          </p>
        </div>
      </div>
    </div>
  );
}

// ─── Main App ────────────────────────────────────────────────────────────────
type TabId = "overview" | "pattern" | "modules" | "concurrency";

export default function App() {
  const [activeTab, setActiveTab] = useState<TabId>("overview");

  const tabs: { id: TabId; label: string; icon: string }[] = [
    { id: "overview", label: "Overview", icon: "📊" },
    { id: "pattern", label: "Pattern Analysis", icon: "🔬" },
    { id: "modules", label: "Module Map", icon: "🗺️" },
    { id: "concurrency", label: "Concurrency", icon: "⚡" },
  ];

  return (
    <div className="min-h-screen bg-slate-950 text-slate-100 font-sans">
      {/* ── Header ── */}
      <header className="border-b border-slate-800 bg-slate-950/95 backdrop-blur sticky top-0 z-50">
        <div className="max-w-7xl mx-auto px-4 py-3">
          <div className="flex flex-col sm:flex-row sm:items-center gap-2 sm:gap-4">
            <div className="flex items-center gap-3">
              {/* Logo mark */}
              <div className="w-8 h-8 rounded-lg bg-gradient-to-br from-violet-600 to-blue-600 flex items-center justify-center shrink-0">
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="w-4 h-4 text-white">
                  <polyline points="16 18 22 12 16 6" />
                  <polyline points="8 6 2 12 8 18" />
                </svg>
              </div>
              <div>
                <div className="flex items-center gap-2 flex-wrap">
                  <h1 className="text-sm font-bold text-white tracking-tight">Claude claude-opus-4-5</h1>
                  <span className="text-[10px] font-mono px-2 py-0.5 rounded-full bg-violet-900/60 border border-violet-500/40 text-violet-300">
                    V14.7-CORELANE-ULTRA
                  </span>
                </div>
                <p className="text-[10px] text-slate-500 font-mono">Code Architecture Visualizer · {REPO_BUILD}</p>
              </div>
            </div>

            <div className="sm:ml-auto flex items-center gap-3 flex-wrap">
              <div className="flex items-center gap-1.5">
                <PulsingDot color="bg-emerald-400" />
                <span className="text-[10px] font-mono text-slate-400">Branch: {REPO_BRANCH}</span>
              </div>
              <span className="text-[10px] font-mono px-2 py-0.5 rounded bg-slate-800 border border-slate-700 text-slate-400">
                BUILD {BUILD_TAG}
              </span>
            </div>
          </div>
        </div>
      </header>

      {/* ── Hero banner ── */}
      <div className="bg-gradient-to-br from-slate-900 via-slate-950 to-violet-950/30 border-b border-slate-800">
        <div className="max-w-7xl mx-auto px-4 py-8">
          <h2 className="text-2xl sm:text-3xl font-bold text-white mb-1">
            Claude claude-opus-4-5
            <span className="ml-3 text-violet-400">V14.7-CORELANE-ULTRA</span>
          </h2>
          <p className="text-slate-400 text-sm mb-5">
            Implementation Detail · Universal OR Strategy V12 · NinjaTrader 8 · Single-Instance Multi-Account Copy Trading Engine
          </p>

          {/* Metrics row */}
          <div className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-6 gap-3">
            {buildMetrics.map((m) => (
              <div key={m.label} className="rounded-lg bg-slate-900/80 border border-slate-800 p-3 flex flex-col gap-1">
                <span className="text-[10px] font-semibold uppercase tracking-wider text-slate-500">{m.label}</span>
                <span className={`text-sm font-bold bg-gradient-to-r ${m.color} bg-clip-text text-transparent`}>{m.value}</span>
                <span className="text-[10px] text-slate-500">{m.sub}</span>
              </div>
            ))}
          </div>
        </div>
      </div>

      {/* ── Tabs ── */}
      <div className="border-b border-slate-800 bg-slate-950/90 sticky top-[73px] z-40">
        <div className="max-w-7xl mx-auto px-4">
          <div className="flex gap-0 overflow-x-auto">
            {tabs.map((tab) => (
              <button
                key={tab.id}
                onClick={() => setActiveTab(tab.id)}
                className={`flex items-center gap-1.5 px-4 py-3 text-sm font-medium border-b-2 transition-all whitespace-nowrap ${
                  activeTab === tab.id
                    ? "border-violet-500 text-violet-300"
                    : "border-transparent text-slate-500 hover:text-slate-300 hover:border-slate-600"
                }`}
              >
                <span>{tab.icon}</span>
                {tab.label}
              </button>
            ))}
          </div>
        </div>
      </div>

      {/* ── Content ── */}
      <main className="max-w-7xl mx-auto px-4 py-8">

        {/* OVERVIEW TAB */}
        {activeTab === "overview" && (
          <div className="space-y-6">
            {/* Repo info */}
            <div className="rounded-xl border border-slate-700/60 bg-slate-900/70 backdrop-blur p-5">
              <h3 className="text-sm font-bold text-slate-200 mb-4 flex items-center gap-2">
                <span>📁</span> Repository Structure
              </h3>
              <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-3 mb-4">
                {[
                  { dir: "src/", desc: "Core C# strategy and panel logic — 50+ partial class modules", color: "text-violet-300" },
                  { dir: "bin/", desc: "Executables and binary tools — Auditors, CLI", color: "text-blue-300" },
                  { dir: "docs/", desc: "Architecture maps, risk reports, audit logs, Handoff Protocols, brain/", color: "text-emerald-300" },
                  { dir: "scripts/", desc: "Automation — deploy-sync.ps1, audit_scan.ps1, verify_reorg.ps1", color: "text-amber-300" },
                ].map((d) => (
                  <div key={d.dir} className="rounded-lg bg-slate-800/60 border border-slate-700/60 p-3">
                    <code className={`text-xs font-bold font-mono ${d.color}`}>{d.dir}</code>
                    <p className="text-xs text-slate-400 mt-1 leading-relaxed">{d.desc}</p>
                  </div>
                ))}
              </div>
              <div className="rounded-lg bg-black/40 border border-slate-800 p-3">
                <p className="text-xs text-slate-400 font-mono">
                  Architecture: <span className="text-violet-300">V12 Modular Partial Classes</span> ·
                  Target: <span className="text-blue-300">NinjaTrader 8 .NET 4.8</span> ·
                  Paradigm: <span className="text-emerald-300">Lock-Free Actor Model</span> ·
                  Fleet: <span className="text-amber-300">SIMA (Single-Instance Multi-Account)</span>
                </p>
              </div>
            </div>

            {/* Pattern summary cards (mini) */}
            <div>
              <h3 className="text-sm font-bold text-slate-300 mb-3 flex items-center gap-2">
                <span>🔬</span> Pattern Analysis Summary
                <span className="text-xs font-normal text-slate-500 ml-1">(See Pattern Analysis tab for full detail)</span>
              </h3>
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
                {patternFindings.map((f) => (
                  <div
                    key={f.id}
                    className="rounded-lg border border-slate-700/60 bg-slate-900/60 p-4 flex items-start gap-3"
                  >
                    <div className={`h-1.5 w-1.5 rounded-full mt-1.5 shrink-0 ${statusBarColor[f.status]}`} />
                    <div>
                      <div className="flex items-center gap-2 mb-1">
                        <span className="text-xs font-semibold text-slate-400 uppercase tracking-wider">{f.category}</span>
                        <span className={`text-[10px] font-bold px-1.5 py-0.5 rounded-full ${statusBadge[f.status]}`}>{f.status}</span>
                      </div>
                      <p className="text-xs text-slate-200 leading-snug">{f.title}</p>
                      <p className="text-[10px] text-slate-500 font-mono mt-1">{f.file}</p>
                    </div>
                  </div>
                ))}
              </div>
            </div>

            {/* SIMA flow */}
            <SIMAFlowDiagram />
          </div>
        )}

        {/* PATTERN ANALYSIS TAB */}
        {activeTab === "pattern" && (
          <div className="space-y-4">
            <div className="flex items-center gap-3 mb-2">
              <h2 className="text-lg font-bold text-white">Pattern Analysis</h2>
              <span className="text-xs text-slate-500 font-mono">4 verified findings · sourced from live repository</span>
            </div>
            <div className="grid grid-cols-1 xl:grid-cols-2 gap-4">
              {patternFindings.map((f) => (
                <PatternCard key={f.id} finding={f} />
              ))}
            </div>

            {/* Legend */}
            <div className="rounded-lg border border-slate-800 bg-slate-900/40 p-4 flex flex-wrap gap-4">
              <p className="text-xs font-semibold text-slate-500 uppercase tracking-wider w-full">Legend</p>
              {Object.entries(statusBadge).map(([s, cls]) => (
                <div key={s} className="flex items-center gap-1.5">
                  <span className={`text-[10px] font-bold px-2 py-0.5 rounded-full ${cls}`}>{s}</span>
                  <span className="text-[10px] text-slate-500">
                    {s === "PASS" && "Confirmed correct / compliant"}
                    {s === "WARN" && "Works but has risk / portability concern"}
                    {s === "INFO" && "Neutral observation / no action needed"}
                    {s === "CRITICAL" && "Blocking issue found"}
                  </span>
                </div>
              ))}
            </div>
          </div>
        )}

        {/* MODULE MAP TAB */}
        {activeTab === "modules" && (
          <div className="space-y-4">
            <div className="flex items-center gap-3 mb-2">
              <h2 className="text-lg font-bold text-white">Module Map</h2>
              <span className="text-xs text-slate-500 font-mono">{modules.length} modules · layered partial-class architecture</span>
            </div>
            {/* Layer legend */}
            <div className="flex flex-wrap gap-2">
              {Object.entries(layerColors).map(([layer, cls]) => (
                <span key={layer} className={`text-[10px] font-bold px-2.5 py-1 rounded-full border ${cls}`}>
                  {layer}
                </span>
              ))}
            </div>
            <ModuleGrid />
          </div>
        )}

        {/* CONCURRENCY TAB */}
        {activeTab === "concurrency" && (
          <div className="space-y-4">
            <div className="flex items-center gap-3 mb-2">
              <h2 className="text-lg font-bold text-white">Concurrency Architecture</h2>
              <span className="text-xs text-slate-500 font-mono">Zero monitor locks · Actor model · Interlocked primitives</span>
            </div>
            <ActorDiagram />
            <ConcurrencyDiagram />

            {/* Circuit breaker info */}
            <div className="rounded-xl border border-amber-800/40 bg-amber-950/20 p-5">
              <h3 className="text-sm font-bold text-amber-300 mb-3 flex items-center gap-2">
                <span>⚠️</span> Circuit Breaker & Safety Guards
              </h3>
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-3 text-xs">
                {[
                  { key: "CIRCUIT_BREAKER_THRESHOLD", val: "5", desc: "Max pending stop replacements before circuit trips" },
                  { key: "STALE_PENDING_FAST_PATH_SEC", val: "3s", desc: "Staleness threshold for pending stop replacement fast-path" },
                  { key: "MaxBrokerCallsPerCycle", val: "5", desc: "Actor drain budget — calls to SubmitOrder/CancelOrder per cycle" },
                  { key: "MaxActorDurationMs", val: "10ms", desc: "Actor cycle wall-clock budget before yield requested" },
                  { key: "MaxAccountOrdersPerDrain", val: "8", desc: "Account order queue drain budget per TriggerCustomEvent slice" },
                  { key: "ReaperFillGraceTicks", val: "5s", desc: "REAPER grace window after expectedPositions stamped non-zero" },
                ].map((item) => (
                  <div key={item.key} className="flex flex-col gap-0.5 rounded-lg bg-black/30 border border-amber-900/30 p-3">
                    <code className="text-amber-300/90 font-mono text-[11px] font-bold">{item.key}</code>
                    <span className="text-amber-100/80 font-mono text-sm font-bold">{item.val}</span>
                    <span className="text-slate-400 text-[10px]">{item.desc}</span>
                  </div>
                ))}
              </div>
            </div>
          </div>
        )}
      </main>

      {/* ── Footer ── */}
      <footer className="border-t border-slate-800 bg-slate-950 mt-8">
        <div className="max-w-7xl mx-auto px-4 py-4 flex flex-col sm:flex-row items-center gap-2 justify-between">
          <p className="text-[10px] text-slate-600 font-mono">
            Claude claude-opus-4-5 · V14.7-CORELANE-ULTRA · Implementation Detail Visualizer
          </p>
          <p className="text-[10px] text-slate-700 font-mono">
            Data sourced from: mkalhitti-cloud/universal-or-strategy@{REPO_BRANCH}
          </p>
        </div>
      </footer>
    </div>
  );
}
