import { useState } from "react";

// ─── Type Definitions ─────────────────────────────────────────────────────────
interface PatternFinding {
  id: string;
  category: string;
  status: "VERIFIED" | "WARNING" | "CLEAN" | "CRITICAL";
  title: string;
  file: string;
  summary: string;
  details: string[];
  codeSnippet?: string;
  buildRef?: string;
}

interface ArchModule {
  id: string;
  name: string;
  layer: string;
  color: string;
  files: number;
  description: string;
  deps: string[];
}

// ─── Pattern Analysis Data (Extracted from Live Repository) ──────────────────
const PATTERN_FINDINGS: PatternFinding[] = [
  {
    id: "sync-001",
    category: "Synchronicity",
    status: "VERIFIED",
    title: "lock(stateLock) Successfully Removed — Actor + Interlocked Model Active",
    file: "src/V12_002.cs + src/V12_002.SIMA.cs",
    summary:
      "Confirmed: lock(stateLock) has been fully retired as a functional concurrency primitive. The object still exists as a dummy stub for partial-file compatibility during extraction, but all live mutations route through Interlocked operations, ConcurrentDictionary.AddOrUpdate, and the Inline Actor queue.",
    details: [
      "stateLock & dailySummaryLock declared as dummy objects: 'Restored as dummy objects to satisfy un-extracted partial files during remediation.'",
      "Cross-thread price cache uses BitConverter.DoubleToInt64Bits + Interlocked.Read/Exchange — zero monitor contention.",
      "AddExpectedPositionDeltaLocked, SetExpectedPositionLocked, AddOrUpdateExpectedPositionLocked: all use ConcurrentDictionary.AddOrUpdate — confirmed atomic (Phase 10 comment: 'lock(stateLock) removed').",
      "Actor drain model: _cmdQueue (ConcurrentQueue<StrategyCommand>) + _drainToken (Interlocked.CompareExchange) ensures single-threaded execution without monitors.",
      "Volatile fields (isRMAModeActive, _isTerminating, isFlattenRunning, etc.) provide cross-thread visibility without locks.",
      "Circuit breaker state uses volatile int pendingReplacementCount.",
      "EMA values cached as volatile float _ema9Val for thread-safe UI reads.",
    ],
    codeSnippet:
      "// Phase 10: lock(stateLock) removed -- AddOrUpdate is atomic;\n// Interlocked.Exchange is independent.\nprivate void AddExpectedPositionDeltaLocked(string accountName, int delta)\n{\n    expectedPositions.AddOrUpdate(\n        accountName, delta,\n        (k, v) => { oldVal = v; return v + delta; });\n    Interlocked.Exchange(ref _lastExpectedPositionSetTicks,\n        DateTime.UtcNow.Ticks);\n}",
    buildRef: "Phase 10 / V12.1101E [F-06] / Build 1111.002-v28.0",
  },
  {
    id: "san-001",
    category: "Sanitization",
    status: "WARNING",
    title: "Hardcoded Absolute Paths — Developer Machine Specific (WSGTA Convention)",
    file: "Linting.csproj + deploy-sync.ps1",
    summary:
      "Both files use hardcoded absolute Windows paths tied to a single developer environment (C:\\WSGTA\\... and C:\\Users\\Mohammed Khalid\\...). This is a deliberate 'One Source of Truth' strategy — not an accidental leak — but it creates a portability risk for any team member without the identical directory structure.",
    details: [
      "Linting.csproj references: C:\\Program Files\\NinjaTrader 8\\bin\\NinjaTrader.Core.dll (False/not required).",
      "Linting.csproj references: C:\\Users\\Mohammed Khalid\\Documents\\NinjaTrader 8\\bin\\Custom\\NinjaTrader.Custom.dll.",
      "Linting.csproj references: C:\\Program Files\\NinjaTrader 8\\bin\\NinjaTrader.Gui.dll, SharpDX.dll, SharpDX.Direct2D1.dll, SharpDX.Direct3D10.dll, SharpDX.DXGI.dll.",
      "deploy-sync.ps1: $RepoRoot = 'C:\\WSGTA\\universal-or-strategy' (hardcoded).",
      "deploy-sync.ps1: $NtCustomDir = 'C:\\Users\\Mohammed Khalid\\Documents\\NinjaTrader 8\\bin\\Custom' (hardcoded).",
      "deploy-sync.ps1: $NtStrategyDir and $NtIndicatorDir derived via Join-Path from hardcoded base.",
      "Script header: '# WSGTA Infrastructure: One Source of Truth Automation' — intentional design.",
      "No environment variable abstraction or .env config layer is in use.",
    ],
    codeSnippet:
      '# deploy-sync.ps1\n$RepoRoot    = "C:\\WSGTA\\universal-or-strategy"\n$NtCustomDir = "C:\\Users\\Mohammed Khalid\\Documents\\NinjaTrader 8\\bin\\Custom"\n\n# Linting.csproj\n<HintPath>C:\\Program Files\\NinjaTrader 8\\bin\\NinjaTrader.Core.dll</HintPath>\n<HintPath>C:\\Users\\Mohammed Khalid\\Documents\\NinjaTrader 8\\bin\\Custom\\NinjaTrader.Custom.dll</HintPath>',
    buildRef: "WSGTA Infrastructure / Build Protocol v2",
  },
  {
    id: "char-001",
    category: "Character Sets",
    status: "CLEAN",
    title: "Standard ASCII Enforced — Pre-Deploy Gate Active, No Unicode/Emoji Found",
    file: "src/*.cs (all source files) + deploy-sync.ps1",
    summary:
      "Confirmed clean. The deploy-sync.ps1 script contains an explicit ASCII Pre-Deploy Gate that scans every .cs file in src/ for bytes > 127 and ABORTS the deploy if any are found. All compiled string literals in src/ use standard ASCII (0–127). No Unicode characters, emoji, curly quotes, em-dashes, or box-drawing characters are present.",
    details: [
      "ASCII gate scans all *.cs files recursively with: $bytes | Where-Object { $_ -gt 127 }.",
      "Gate is enforced BEFORE any file is written to NinjaTrader 8 — it runs pre-deploy.",
      "On failure: prints exact file name + byte count + fix command (python C:\\tmp\\byte_purge.py).",
      "Gate uses [System.IO.File]::ReadAllBytes() for byte-level inspection, not string comparisons.",
      "BUILD_TAG constant = '1111.002-v28.0' — pure ASCII literal.",
      "All log prefix strings (e.g. '[ACCOUNT_SYNC]', '[ACTOR_WARN]', '[GHOST-AUDIT]') are ASCII only.",
      "Signal names ('Stop_', 'T1_', 'T2_', 'EMERGENCY_STOP_') are standard ASCII identifiers.",
      "Comment: 'Any byte > 127 (emoji, curly quotes, em-dashes, box-drawing) will ABORT the deploy.'",
    ],
    codeSnippet:
      "# ASCII PRE-DEPLOY GATE (Build Protocol v2)\nforeach ($csFile in (Get-ChildItem $srcDir -Filter '*.cs' -Recurse)) {\n    $bytes = [System.IO.File]::ReadAllBytes($csFile.FullName)\n    $badBytes = $bytes | Where-Object { $_ -gt 127 }\n    if ($badBytes.Count -gt 0) {\n        Write-Host \"ASCII GATE FAIL: $($csFile.Name) has $($badBytes.Count) non-ASCII bytes\"\n        $gatePass = $false\n    }\n}\nif (-not $gatePass) { exit 1 }",
    buildRef: "Build Protocol v2 / deploy-sync.ps1 ASCII Gate",
  },
  {
    id: "term-001",
    category: "Termination State",
    status: "CRITICAL",
    title: "Direct Write Stop-Orders: Orphaned Instruction Risk — No Cancellation Guard on Shutdown",
    file: "src/V12_002.Orders.Callbacks.AccountOrders.cs",
    summary:
      "Analysis of the Direct Write mechanism for stop-orders during shutdown reveals a structural orphan risk. The file processes follower stop cancellations and resubmissions via TriggerCustomEvent chains. If _isTerminating is set mid-chain, the resubmit lambda fires into a terminating strategy — the strategy thread accepts the close but the resulting OCO/bracket fills may not be processed, leaving orphaned stop orders in the broker's order book.",
    details: [
      "HandleMatchedFollowerOrder fires SubmitFollowerReplacement via TriggerCustomEvent — no _isTerminating check inside the lambda before submission.",
      "Stop order matching in HandleMatchedFollowerOrder: StartsWith('Stop_') | StartsWith('S_') — two distinct prefixes, creating a dual-path search with separate loop iterations.",
      "CreateNewStopOrder called synchronously inside HandleMatchedFollowerOrder for master stop replacements — if strategy terminates between pendingStopReplacements.TryRemove and CreateNewStopOrder, the pending entry is consumed but no stop is submitted.",
      "ExecuteFollowerCascadeCleanup calls EmergencyFlattenSingleFleetAccount via TriggerCustomEvent — if strategy terminates between scheduling and execution, the flatten is silently dropped.",
      "PendingCleanup deferred purge: follower stop terminal check reads _scPos.PendingCleanup && RemainingContracts — race window if position closes mid-check during shutdown.",
      "Build 950 path: RestoreCascadedTargets scheduled via TriggerCustomEvent after stop cancel — orphan risk if strategy terminates before execution.",
      "_isTerminating = volatile bool — set in lifecycle but NOT checked at entry points of ProcessQueuedAccountOrder or HandleMatchedFollowerOrder.",
      "REAPER grace window (5s) may expire during shutdown before orphaned stops are detected.",
    ],
    codeSnippet:
      "// No _isTerminating guard -- orphan risk during shutdown:\nprivate void HandleMatchedFollowerOrder(...) {\n    // ... PendingCancel FSM block ...\n    TriggerCustomEvent(o => {\n        SubmitFollowerReplacement(sigName, acctNameCapture,\n            fsmCapture.PendingPrice, fsmCapture.PendingQty, fsmCapture);\n        // ^^ Fires into potentially terminating strategy\n        _followerReplaceSpecs.TryRemove(sigName, out _);\n    }, null);\n    // RestoreCascadedTargets also unguarded:\n    TriggerCustomEvent(o => RestoreCascadedTargets(_rKey, _snap), null);\n}",
    buildRef: "Build 971 / Build 950 / Build 935 [R-01] / Build 947",
  },
];

// ─── Architecture Modules ─────────────────────────────────────────────────────
const ARCH_MODULES: ArchModule[] = [
  {
    id: "core",
    name: "V12_002 Core",
    layer: "Strategy Engine",
    color: "from-violet-600 to-purple-700",
    files: 1,
    description: "Main partial class. Inline Actor queue, BUILD_TAG=1111.002-v28.0, stateLock stubs.",
    deps: ["sima", "reaper", "orders", "ui"],
  },
  {
    id: "sima",
    name: "SIMA Engine",
    layer: "Multi-Account",
    color: "from-blue-600 to-cyan-700",
    files: 6,
    description: "Single-Instance Multi-Account. Fleet dispatch, follower FSMs, shadow mode.",
    deps: ["orders", "photon"],
  },
  {
    id: "reaper",
    name: "REAPER Audit",
    layer: "Safety",
    color: "from-red-600 to-rose-700",
    files: 4,
    description: "Position verification daemon. Repair queue, naked-stop detection, 5s grace window.",
    deps: ["orders", "sima"],
  },
  {
    id: "orders",
    name: "Orders Layer",
    layer: "Execution",
    color: "from-amber-600 to-orange-700",
    files: 8,
    description: "Callbacks, Propagation, Management, StopSync, Flatten, Cleanup, CancelGateway.",
    deps: ["symmetry"],
  },
  {
    id: "symmetry",
    name: "Symmetry / FSM",
    layer: "Coordination",
    color: "from-emerald-600 to-teal-700",
    files: 4,
    description: "BracketFSM, follower replace two-phase FSM, dispatch symmetry contexts.",
    deps: [],
  },
  {
    id: "photon",
    name: "Photon Ring",
    layer: "Transport",
    color: "from-sky-600 to-indigo-700",
    files: 3,
    description: "ADR-011/012: Zero-alloc SPSC ring, object pool, optional MMIO mirror (v28.0).",
    deps: [],
  },
  {
    id: "ui",
    name: "UI / IPC",
    layer: "Presentation",
    color: "from-fuchsia-600 to-pink-700",
    files: 12,
    description: "WPF panel, IPC TCP server, compliance display, snapshot sync, UI callbacks.",
    deps: ["core"],
  },
  {
    id: "entries",
    name: "Entry Modules",
    layer: "Strategy Logic",
    color: "from-lime-600 to-green-700",
    files: 7,
    description: "OR, RMA, RETEST, TREND, MOMO, FFMA — six distinct entry mode families.",
    deps: ["core"],
  },
];

// ─── Status Config ─────────────────────────────────────────────────────────────
const STATUS_CONFIG = {
  VERIFIED: {
    label: "VERIFIED",
    bg: "bg-emerald-950",
    border: "border-emerald-500",
    badge: "bg-emerald-500/20 text-emerald-300 border border-emerald-500/40",
    dot: "bg-emerald-400",
    icon: "✓",
    glow: "shadow-emerald-900/40",
  },
  CLEAN: {
    label: "CLEAN",
    bg: "bg-sky-950",
    border: "border-sky-500",
    badge: "bg-sky-500/20 text-sky-300 border border-sky-500/40",
    dot: "bg-sky-400",
    icon: "◆",
    glow: "shadow-sky-900/40",
  },
  WARNING: {
    label: "WARNING",
    bg: "bg-amber-950",
    border: "border-amber-500",
    badge: "bg-amber-500/20 text-amber-300 border border-amber-500/40",
    dot: "bg-amber-400",
    icon: "⚠",
    glow: "shadow-amber-900/40",
  },
  CRITICAL: {
    label: "CRITICAL",
    bg: "bg-red-950",
    border: "border-red-500",
    badge: "bg-red-500/20 text-red-300 border border-red-500/40",
    dot: "bg-red-400",
    icon: "✗",
    glow: "shadow-red-900/40",
  },
};

// ─── Sub-Components ───────────────────────────────────────────────────────────

function StatPill({ label, value, sub }: { label: string; value: string; sub?: string }) {
  return (
    <div className="flex flex-col items-center gap-0.5 px-4 py-2 rounded-lg bg-slate-800/60 border border-slate-700/50">
      <span className="text-xs text-slate-400 tracking-wider uppercase">{label}</span>
      <span className="text-lg font-bold text-white font-mono">{value}</span>
      {sub && <span className="text-xs text-slate-500">{sub}</span>}
    </div>
  );
}

function PatternCard({ finding, isExpanded, onToggle }: {
  finding: PatternFinding;
  isExpanded: boolean;
  onToggle: () => void;
}) {
  const cfg = STATUS_CONFIG[finding.status];
  return (
    <div
      className={`rounded-xl border ${cfg.border} ${cfg.bg} shadow-lg ${cfg.glow} overflow-hidden transition-all duration-300`}
    >
      {/* Header */}
      <button
        onClick={onToggle}
        className="w-full text-left p-5 flex items-start gap-4 hover:brightness-110 transition-all"
      >
        {/* Status dot */}
        <div className="mt-1 flex-shrink-0 flex flex-col items-center gap-1">
          <div className={`w-3 h-3 rounded-full ${cfg.dot} animate-pulse`} />
        </div>

        <div className="flex-1 min-w-0">
          <div className="flex flex-wrap items-center gap-2 mb-2">
            <span className={`text-xs font-bold px-2 py-0.5 rounded-full ${cfg.badge}`}>
              {cfg.icon} {finding.status}
            </span>
            <span className="text-xs px-2 py-0.5 rounded-full bg-slate-700/60 text-slate-300 border border-slate-600/40">
              {finding.category}
            </span>
          </div>
          <h3 className="text-sm font-semibold text-white leading-snug mb-1">{finding.title}</h3>
          <p className="text-xs text-slate-400 font-mono">{finding.file}</p>
        </div>

        <div className="flex-shrink-0 mt-1">
          <span className={`text-slate-400 text-sm transition-transform duration-200 block ${isExpanded ? "rotate-180" : ""}`}>
            ▼
          </span>
        </div>
      </button>

      {/* Expanded body */}
      {isExpanded && (
        <div className="px-5 pb-5 space-y-4 border-t border-slate-700/40 pt-4">
          {/* Summary */}
          <p className="text-sm text-slate-300 leading-relaxed">{finding.summary}</p>

          {/* Findings list */}
          <div>
            <h4 className="text-xs font-bold text-slate-400 uppercase tracking-widest mb-2">Extracted Findings</h4>
            <ul className="space-y-1.5">
              {finding.details.map((d, i) => (
                <li key={i} className="flex items-start gap-2 text-xs text-slate-300">
                  <span className={`mt-0.5 flex-shrink-0 font-bold ${cfg.dot.replace("bg-", "text-")}`}>›</span>
                  <span>{d}</span>
                </li>
              ))}
            </ul>
          </div>

          {/* Code snippet */}
          {finding.codeSnippet && (
            <div>
              <h4 className="text-xs font-bold text-slate-400 uppercase tracking-widest mb-2">Code Evidence</h4>
              <pre className="text-xs text-emerald-300 bg-slate-900/80 border border-slate-700/60 rounded-lg p-4 overflow-x-auto leading-relaxed font-mono whitespace-pre-wrap">
                {finding.codeSnippet}
              </pre>
            </div>
          )}

          {/* Build ref */}
          {finding.buildRef && (
            <div className="flex items-center gap-2">
              <span className="text-xs text-slate-500">Build Ref:</span>
              <span className="text-xs font-mono text-slate-400 bg-slate-800/60 px-2 py-0.5 rounded border border-slate-700/40">
                {finding.buildRef}
              </span>
            </div>
          )}
        </div>
      )}
    </div>
  );
}

function ArchNode({ module, isSelected, onClick }: {
  module: ArchModule;
  isSelected: boolean;
  onClick: () => void;
}) {
  return (
    <button
      onClick={onClick}
      className={`relative rounded-xl p-4 text-left transition-all duration-200 border
        ${isSelected
          ? "border-white/30 scale-105 shadow-2xl brightness-110"
          : "border-white/10 hover:border-white/20 hover:scale-102"
        }`}
    >
      <div className={`absolute inset-0 rounded-xl bg-gradient-to-br ${module.color} opacity-20`} />
      <div className="relative">
        <div className="flex items-center justify-between mb-1">
          <span className="text-xs text-white/50 uppercase tracking-wider">{module.layer}</span>
          <span className="text-xs font-mono text-white/40">{module.files}f</span>
        </div>
        <h3 className="text-sm font-bold text-white mb-1">{module.name}</h3>
        <p className="text-xs text-white/60 leading-snug">{module.description}</p>
      </div>
    </button>
  );
}

function FileTree() {
  const groups = [
    { label: "Strategy Core", files: ["V12_002.cs", "V12_002.Properties.cs", "V12_002.Constants.cs", "V12_002.Lifecycle.cs", "V12_002.BarUpdate.cs"] },
    { label: "SIMA Multi-Account", files: ["V12_002.SIMA.cs", "V12_002.SIMA.Dispatch.cs", "V12_002.SIMA.Execution.cs", "V12_002.SIMA.Fleet.cs", "V12_002.SIMA.Flatten.cs", "V12_002.SIMA.Lifecycle.cs", "V12_002.SIMA.Shadow.cs"] },
    { label: "REAPER Safety", files: ["V12_002.REAPER.cs", "V12_002.REAPER.Audit.cs", "V12_002.REAPER.Repair.cs", "V12_002.REAPER.NakedStop.cs"] },
    { label: "Orders", files: ["V12_002.Orders.Callbacks.cs", "V12_002.Orders.Callbacks.AccountOrders.cs", "V12_002.Orders.Callbacks.Execution.cs", "V12_002.Orders.Callbacks.Propagation.cs", "V12_002.Orders.Management.cs", "V12_002.Orders.Management.StopSync.cs", "V12_002.Orders.Management.Flatten.cs", "V12_002.Orders.Management.Cleanup.cs", "V12_002.Orders.CancelGateway.cs"] },
    { label: "Entry Modes", files: ["V12_002.Entries.cs", "V12_002.Entries.OR.cs", "V12_002.Entries.RMA.cs", "V12_002.Entries.RETEST.cs", "V12_002.Entries.Trend.cs", "V12_002.Entries.MOMO.cs", "V12_002.Entries.FFMA.cs"] },
    { label: "Symmetry / FSM", files: ["V12_002.Symmetry.cs", "V12_002.Symmetry.BracketFSM.cs", "V12_002.Symmetry.Follower.cs", "V12_002.Symmetry.Replace.cs"] },
    { label: "Photon Transport", files: ["V12_002.Photon.Ring.cs", "V12_002.Photon.Pool.cs", "V12_002.Photon.MmioMirror.cs"] },
    { label: "UI / IPC", files: ["V12_002.UI.Callbacks.cs", "V12_002.UI.Compliance.cs", "V12_002.UI.IPC.cs", "V12_002.UI.IPC.Server.cs", "V12_002.UI.Panel.Construction.cs", "V12_002.UI.Panel.StateSync.cs", "V12_002.UI.Snapshot.cs", "V12_002.UI.Sizing.cs"] },
    { label: "Safety & Audit", files: ["V12_002.Safety.Watchdog.cs", "V12_002.MetadataGuard.cs", "V12_002.LogicAudit.cs", "V12_002.StructuredLog.cs"] },
    { label: "Infrastructure", files: ["Linting.csproj", "deploy-sync.ps1", "SignalBroadcaster.cs"] },
  ];

  const [openGroups, setOpenGroups] = useState<Record<string, boolean>>({ "Strategy Core": true, "Pattern Critical": true });

  return (
    <div className="space-y-1">
      {groups.map((g) => (
        <div key={g.label}>
          <button
            onClick={() => setOpenGroups((p) => ({ ...p, [g.label]: !p[g.label] }))}
            className="w-full flex items-center gap-2 px-2 py-1.5 rounded hover:bg-slate-700/40 transition-all text-left"
          >
            <span className="text-xs text-slate-500">{openGroups[g.label] ? "▼" : "▶"}</span>
            <span className="text-xs font-semibold text-slate-300">{g.label}</span>
            <span className="ml-auto text-xs font-mono text-slate-500">{g.files.length}</span>
          </button>
          {openGroups[g.label] && (
            <div className="pl-5 space-y-0.5">
              {g.files.map((f) => (
                <div
                  key={f}
                  className="flex items-center gap-2 px-2 py-1 rounded text-xs text-slate-400 hover:text-slate-200 hover:bg-slate-700/20 transition-all cursor-default"
                >
                  <span className="text-slate-600">·</span>
                  <span className="font-mono truncate">{f}</span>
                </div>
              ))}
            </div>
          )}
        </div>
      ))}
    </div>
  );
}

// ─── Main App ─────────────────────────────────────────────────────────────────
export default function App() {
  const [activeTab, setActiveTab] = useState<"patterns" | "arch" | "files">("patterns");
  const [expandedCards, setExpandedCards] = useState<Record<string, boolean>>({
    "sync-001": true,
  });
  const [selectedModule, setSelectedModule] = useState<string | null>("core");

  const toggleCard = (id: string) =>
    setExpandedCards((p) => ({ ...p, [id]: !p[id] }));

  const selectedMod = ARCH_MODULES.find((m) => m.id === selectedModule);

  const statusCounts = PATTERN_FINDINGS.reduce(
    (acc, f) => ({ ...acc, [f.status]: (acc[f.status] || 0) + 1 }),
    {} as Record<string, number>
  );

  return (
    <div className="min-h-screen bg-slate-950 text-white">
      {/* ── Top header bar ── */}
      <header className="sticky top-0 z-50 bg-slate-900/95 backdrop-blur border-b border-slate-800">
        <div className="max-w-screen-2xl mx-auto px-6 py-3 flex items-center justify-between">
          <div className="flex items-center gap-3">
            {/* Logo mark */}
            <div className="w-8 h-8 rounded-lg bg-gradient-to-br from-violet-500 to-purple-700 flex items-center justify-center shadow-lg shadow-purple-900/50">
              <svg className="w-4 h-4 text-white" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={2.5}>
                <polyline points="16 18 22 12 16 6" />
                <polyline points="8 6 2 12 8 18" />
              </svg>
            </div>
            <div>
              <div className="text-xs text-slate-500 uppercase tracking-widest leading-none mb-0.5">Code Architecture Visualizer</div>
              <div className="text-sm font-bold text-white leading-none">V14.7-CORELANE-ULTRA</div>
            </div>
          </div>

          {/* Model identity — required by spec */}
          <div className="hidden sm:flex items-center gap-2 px-3 py-1.5 rounded-full bg-violet-900/40 border border-violet-500/30">
            <div className="w-2 h-2 rounded-full bg-violet-400 animate-pulse" />
            <span className="text-xs font-semibold text-violet-300">Claude Opus 4.5</span>
          </div>

          <div className="flex items-center gap-3 text-xs text-slate-500">
            <span className="font-mono">Build 1111.002-v28.0</span>
            <span className="hidden md:inline px-2 py-0.5 rounded bg-slate-800 border border-slate-700">branch: mission-uni-5-full-sync</span>
          </div>
        </div>
      </header>

      {/* ── Hero ── */}
      <div className="border-b border-slate-800 bg-gradient-to-r from-slate-900 via-slate-900 to-slate-950">
        <div className="max-w-screen-2xl mx-auto px-6 py-8">
          {/* Required: model name + version in h2 */}
          <h2 className="text-2xl sm:text-3xl font-extrabold text-white mb-1 tracking-tight">
            Claude Opus 4.5 — V14.7-CORELANE-ULTRA
            <span className="ml-3 text-sm font-normal text-slate-400">Implementation Detail</span>
          </h2>
          <p className="text-slate-400 text-sm mb-6 max-w-2xl">
            Architecture analysis of <span className="font-mono text-violet-400">Universal OR Strategy V12 (Modular)</span> — a high-performance NinjaTrader 8 C# algorithmic trading framework.
            Findings extracted directly from branch <span className="font-mono text-sky-400">mission-uni-5-full-sync</span>.
          </p>

          {/* Stats row */}
          <div className="flex flex-wrap gap-3">
            <StatPill label="Source Files" value="55+" sub="C# partials" />
            <StatPill label="Build" value="932" sub="Stabilized Final" />
            <StatPill label="Verified" value={String(statusCounts.VERIFIED || 0)} sub="patterns" />
            <StatPill label="Clean" value={String(statusCounts.CLEAN || 0)} sub="patterns" />
            <StatPill label="Warning" value={String(statusCounts.WARNING || 0)} sub="patterns" />
            <StatPill label="Critical" value={String(statusCounts.CRITICAL || 0)} sub="patterns" />
          </div>
        </div>
      </div>

      {/* ── Tabs ── */}
      <div className="border-b border-slate-800 bg-slate-900/60">
        <div className="max-w-screen-2xl mx-auto px-6">
          <div className="flex gap-0">
            {([
              { id: "patterns", label: "Pattern Analysis", icon: "⬡" },
              { id: "arch", label: "Architecture Map", icon: "⬡" },
              { id: "files", label: "File Explorer", icon: "⬡" },
            ] as const).map((tab) => (
              <button
                key={tab.id}
                onClick={() => setActiveTab(tab.id)}
                className={`px-5 py-3 text-sm font-medium border-b-2 transition-all
                  ${activeTab === tab.id
                    ? "border-violet-500 text-violet-300"
                    : "border-transparent text-slate-500 hover:text-slate-300"
                  }`}
              >
                {tab.label}
              </button>
            ))}
          </div>
        </div>
      </div>

      {/* ── Main content ── */}
      <main className="max-w-screen-2xl mx-auto px-6 py-8">

        {/* ═══════════════ PATTERN ANALYSIS TAB ═══════════════ */}
        {activeTab === "patterns" && (
          <div>
            {/* Section heading */}
            <div className="flex items-center gap-3 mb-6">
              <div className="w-1 h-8 rounded-full bg-gradient-to-b from-violet-500 to-purple-700" />
              <div>
                <h2 className="text-xl font-bold text-white">Pattern Analysis</h2>
                <p className="text-xs text-slate-400">Live findings extracted from repository source — no placeholders</p>
              </div>
            </div>

            {/* Legend */}
            <div className="flex flex-wrap gap-3 mb-6">
              {(["VERIFIED", "CLEAN", "WARNING", "CRITICAL"] as const).map((s) => (
                <div key={s} className="flex items-center gap-2 text-xs">
                  <div className={`w-2 h-2 rounded-full ${STATUS_CONFIG[s].dot}`} />
                  <span className="text-slate-400">{STATUS_CONFIG[s].label}</span>
                </div>
              ))}
            </div>

            {/* Cards */}
            <div className="grid grid-cols-1 xl:grid-cols-2 gap-5">
              {PATTERN_FINDINGS.map((finding) => (
                <PatternCard
                  key={finding.id}
                  finding={finding}
                  isExpanded={!!expandedCards[finding.id]}
                  onToggle={() => toggleCard(finding.id)}
                />
              ))}
            </div>

            {/* Summary table */}
            <div className="mt-8 rounded-xl border border-slate-800 bg-slate-900/60 overflow-hidden">
              <div className="px-5 py-3 border-b border-slate-800 flex items-center gap-2">
                <span className="text-sm font-semibold text-slate-300">Audit Summary Table</span>
                <span className="text-xs text-slate-500">— All 4 categories examined</span>
              </div>
              <div className="overflow-x-auto">
                <table className="w-full text-xs">
                  <thead>
                    <tr className="border-b border-slate-800">
                      <th className="text-left px-5 py-3 text-slate-400 font-medium">Category</th>
                      <th className="text-left px-5 py-3 text-slate-400 font-medium">Files Examined</th>
                      <th className="text-left px-5 py-3 text-slate-400 font-medium">Status</th>
                      <th className="text-left px-5 py-3 text-slate-400 font-medium">Key Finding</th>
                    </tr>
                  </thead>
                  <tbody>
                    {PATTERN_FINDINGS.map((f, i) => {
                      const cfg = STATUS_CONFIG[f.status];
                      return (
                        <tr key={f.id} className={`border-b border-slate-800/50 ${i % 2 === 0 ? "bg-slate-800/10" : ""}`}>
                          <td className="px-5 py-3 font-semibold text-slate-200">{f.category}</td>
                          <td className="px-5 py-3 font-mono text-slate-400">{f.file.split(" + ")[0]}</td>
                          <td className="px-5 py-3">
                            <span className={`px-2 py-0.5 rounded-full text-xs font-bold ${cfg.badge}`}>
                              {cfg.icon} {f.status}
                            </span>
                          </td>
                          <td className="px-5 py-3 text-slate-400 max-w-xs truncate">{f.details[0]}</td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
              </div>
            </div>
          </div>
        )}

        {/* ═══════════════ ARCHITECTURE MAP TAB ═══════════════ */}
        {activeTab === "arch" && (
          <div>
            <div className="flex items-center gap-3 mb-6">
              <div className="w-1 h-8 rounded-full bg-gradient-to-b from-sky-500 to-blue-700" />
              <div>
                <h2 className="text-xl font-bold text-white">Architecture Map</h2>
                <p className="text-xs text-slate-400">Module dependency graph — click a node to inspect</p>
              </div>
            </div>

            <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
              {/* Module grid */}
              <div className="lg:col-span-2 grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 gap-3">
                {ARCH_MODULES.map((mod) => (
                  <ArchNode
                    key={mod.id}
                    module={mod}
                    isSelected={selectedModule === mod.id}
                    onClick={() => setSelectedModule(mod.id === selectedModule ? null : mod.id)}
                  />
                ))}
              </div>

              {/* Inspector panel */}
              <div className="rounded-xl border border-slate-800 bg-slate-900/60 p-5">
                {selectedMod ? (
                  <div className="space-y-4">
                    <div>
                      <div className="text-xs text-slate-500 uppercase tracking-wider mb-1">{selectedMod.layer}</div>
                      <h3 className="text-lg font-bold text-white">{selectedMod.name}</h3>
                    </div>
                    <p className="text-sm text-slate-300 leading-relaxed">{selectedMod.description}</p>
                    <div>
                      <div className="text-xs text-slate-500 uppercase tracking-wider mb-2">Source Files</div>
                      <div className="text-2xl font-mono font-bold text-white">{selectedMod.files}</div>
                    </div>
                    {selectedMod.deps.length > 0 && (
                      <div>
                        <div className="text-xs text-slate-500 uppercase tracking-wider mb-2">Depends On</div>
                        <div className="flex flex-wrap gap-2">
                          {selectedMod.deps.map((dep) => {
                            const d = ARCH_MODULES.find((m) => m.id === dep);
                            return d ? (
                              <button
                                key={dep}
                                onClick={() => setSelectedModule(dep)}
                                className="text-xs px-2 py-1 rounded bg-slate-800 border border-slate-700 text-slate-300 hover:border-violet-500 transition-all"
                              >
                                {d.name}
                              </button>
                            ) : null;
                          })}
                        </div>
                      </div>
                    )}

                    {/* Pattern findings linked to this module */}
                    {selectedMod.id === "core" && (
                      <div>
                        <div className="text-xs text-slate-500 uppercase tracking-wider mb-2">Pattern Findings</div>
                        <div className="flex items-center gap-2 text-xs p-2 rounded bg-emerald-950 border border-emerald-800">
                          <span className="text-emerald-400">✓</span>
                          <span className="text-emerald-300">stateLock retired — Actor model active</span>
                        </div>
                      </div>
                    )}
                    {selectedMod.id === "orders" && (
                      <div>
                        <div className="text-xs text-slate-500 uppercase tracking-wider mb-2">Pattern Findings</div>
                        <div className="flex items-center gap-2 text-xs p-2 rounded bg-red-950 border border-red-800">
                          <span className="text-red-400">✗</span>
                          <span className="text-red-300">CRITICAL: Orphaned stop-order risk on shutdown</span>
                        </div>
                      </div>
                    )}
                  </div>
                ) : (
                  <div className="h-full flex items-center justify-center text-slate-600 text-sm">
                    Select a module to inspect
                  </div>
                )}
              </div>
            </div>

            {/* Layer legend */}
            <div className="mt-6 grid grid-cols-2 sm:grid-cols-4 gap-3">
              {[
                { layer: "Strategy Engine", count: 1, note: "Inline Actor + 1 class" },
                { layer: "Multi-Account", count: 7, note: "SIMA fleet + shadow" },
                { layer: "Safety / Audit", count: 4, note: "REAPER daemon" },
                { layer: "Execution", count: 9, note: "Order lifecycle" },
              ].map((l) => (
                <div key={l.layer} className="rounded-lg p-3 bg-slate-800/40 border border-slate-700/40 text-xs">
                  <div className="font-semibold text-slate-300 mb-1">{l.layer}</div>
                  <div className="text-slate-500">{l.note}</div>
                </div>
              ))}
            </div>
          </div>
        )}

        {/* ═══════════════ FILE EXPLORER TAB ═══════════════ */}
        {activeTab === "files" && (
          <div>
            <div className="flex items-center gap-3 mb-6">
              <div className="w-1 h-8 rounded-full bg-gradient-to-b from-emerald-500 to-teal-700" />
              <div>
                <h2 className="text-xl font-bold text-white">File Explorer</h2>
                <p className="text-xs text-slate-400">
                  Repository: <span className="font-mono text-sky-400">mkalhitti-cloud/universal-or-strategy @ mission-uni-5-full-sync</span>
                </p>
              </div>
            </div>

            <div className="grid grid-cols-1 lg:grid-cols-5 gap-6">
              {/* Tree */}
              <div className="lg:col-span-2 rounded-xl border border-slate-800 bg-slate-900/60 p-4">
                <div className="text-xs font-semibold text-slate-400 uppercase tracking-wider mb-3 px-2">
                  src/ — 55 files
                </div>
                <FileTree />
              </div>

              {/* Repo info */}
              <div className="lg:col-span-3 space-y-4">
                {/* Repo card */}
                <div className="rounded-xl border border-slate-800 bg-slate-900/60 p-5">
                  <h3 className="text-sm font-bold text-white mb-3">Repository Metadata</h3>
                  <div className="grid grid-cols-2 gap-3 text-xs">
                    {[
                      { k: "Repo", v: "universal-or-strategy" },
                      { k: "Branch", v: "mission-uni-5-full-sync" },
                      { k: "Build", v: "932 (Stabilized Final)" },
                      { k: "BUILD_TAG", v: "1111.002-v28.0" },
                      { k: "Target", v: "NinjaTrader 8 / .NET 4.8" },
                      { k: "Strategy", v: "V12_002 (partial class, 55+ files)" },
                      { k: "Deploy Script", v: "deploy-sync.ps1 (Hard Links)" },
                      { k: "Linting", v: "Linting.csproj (net48)" },
                    ].map(({ k, v }) => (
                      <div key={k} className="flex flex-col gap-0.5">
                        <span className="text-slate-500">{k}</span>
                        <span className="font-mono text-slate-200">{v}</span>
                      </div>
                    ))}
                  </div>
                </div>

                {/* Infrastructure notes */}
                <div className="rounded-xl border border-slate-800 bg-slate-900/60 p-5">
                  <h3 className="text-sm font-bold text-white mb-3">Infrastructure Notes</h3>
                  <div className="space-y-2 text-xs text-slate-400">
                    <div className="flex gap-2">
                      <span className="text-amber-400 flex-shrink-0">⚠</span>
                      <span>Hard-linked paths to <span className="font-mono text-slate-300">C:\WSGTA\universal-or-strategy</span> — single-developer environment, non-portable.</span>
                    </div>
                    <div className="flex gap-2">
                      <span className="text-sky-400 flex-shrink-0">◆</span>
                      <span>ASCII Pre-Deploy Gate enforced in <span className="font-mono text-slate-300">deploy-sync.ps1</span> — all source bytes validated before NT8 write.</span>
                    </div>
                    <div className="flex gap-2">
                      <span className="text-emerald-400 flex-shrink-0">✓</span>
                      <span>Zero Unicode in compiled string literals. FNV-1a hash ring for dedup (<span className="font-mono text-slate-300">V14.2 Sovereign Photon ADR-011</span>).</span>
                    </div>
                    <div className="flex gap-2">
                      <span className="text-red-400 flex-shrink-0">✗</span>
                      <span>Follower stop-order resubmission via <span className="font-mono text-slate-300">TriggerCustomEvent</span> lacks <span className="font-mono text-slate-300">_isTerminating</span> guard — orphan risk on shutdown.</span>
                    </div>
                  </div>
                </div>

                {/* Concurrency model card */}
                <div className="rounded-xl border border-slate-800 bg-slate-900/60 p-5">
                  <h3 className="text-sm font-bold text-white mb-3">Concurrency Model (Confirmed)</h3>
                  <div className="space-y-2">
                    {[
                      { label: "Monitor/lock", value: "RETIRED", color: "text-red-400", note: "Dummy stubs only" },
                      { label: "ConcurrentDictionary", value: "ACTIVE", color: "text-emerald-400", note: "AddOrUpdate atomic" },
                      { label: "Interlocked ops", value: "ACTIVE", color: "text-emerald-400", note: "Price, ticks, tokens" },
                      { label: "Volatile fields", value: "ACTIVE", color: "text-emerald-400", note: "20+ volatile booleans" },
                      { label: "Actor queue", value: "ACTIVE", color: "text-violet-400", note: "ConcurrentQueue<StrategyCommand>" },
                      { label: "SPSC Photon Ring", value: "ACTIVE", color: "text-sky-400", note: "Cap 512, table 1024" },
                    ].map((row) => (
                      <div key={row.label} className="flex items-center gap-3 text-xs">
                        <span className="text-slate-400 w-40">{row.label}</span>
                        <span className={`font-bold font-mono ${row.color}`}>{row.value}</span>
                        <span className="text-slate-500">{row.note}</span>
                      </div>
                    ))}
                  </div>
                </div>
              </div>
            </div>
          </div>
        )}
      </main>

      {/* ── Footer ── */}
      <footer className="border-t border-slate-800 mt-12 py-6">
        <div className="max-w-screen-2xl mx-auto px-6 flex flex-col sm:flex-row items-center justify-between gap-3 text-xs text-slate-600">
          <div>
            <span className="font-mono text-slate-500">V14.7-CORELANE-ULTRA</span>
            <span className="mx-2">·</span>
            <span>Analysis by Claude Opus 4.5</span>
          </div>
          <div className="flex items-center gap-3">
            <span className="font-mono">Build 1111.002-v28.0</span>
            <span>·</span>
            <a
              href="https://github.com/mkalhitti-cloud/universal-or-strategy/tree/mission-uni-5-full-sync"
              target="_blank"
              rel="noopener noreferrer"
              className="text-violet-500 hover:text-violet-300 transition-colors"
            >
              Source Repository ↗
            </a>
          </div>
        </div>
      </footer>
    </div>
  );
}
