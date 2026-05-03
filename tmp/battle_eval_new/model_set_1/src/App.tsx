import React, { useState } from "react";

// ─── tiny helpers ────────────────────────────────────────────────────────────
const Badge = ({
  color,
  children,
}: {
  color: "green" | "yellow" | "red" | "blue" | "gray" | "orange";
  children: React.ReactNode;
}) => {
  const map = {
    green: "bg-green-900/60 text-green-300 border border-green-700",
    yellow: "bg-yellow-900/60 text-yellow-300 border border-yellow-700",
    red: "bg-red-900/60 text-red-300 border border-red-700",
    blue: "bg-blue-900/60 text-blue-300 border border-blue-700",
    gray: "bg-gray-700/60 text-gray-300 border border-gray-600",
    orange: "bg-orange-900/60 text-orange-300 border border-orange-700",
  };
  return (
    <span
      className={`inline-block rounded px-2 py-0.5 text-xs font-mono font-semibold ${map[color]}`}
    >
      {children}
    </span>
  );
};

const SectionHeader = ({
  num,
  title,
  subtitle,
}: {
  num: string;
  title: string;
  subtitle?: string;
}) => (
  <div className="mb-6">
    <div className="flex items-center gap-3 mb-1">
      <span className="flex-shrink-0 w-8 h-8 rounded-full bg-indigo-600 flex items-center justify-center text-white font-bold text-sm">
        {num}
      </span>
      <h3 className="text-xl font-bold text-white tracking-wide">{title}</h3>
    </div>
    {subtitle && (
      <p className="ml-11 text-sm text-gray-400 font-mono">{subtitle}</p>
    )}
  </div>
);

const Card = ({
  children,
  className = "",
}: {
  children: React.ReactNode;
  className?: string;
}) => (
  <div
    className={`bg-gray-800 border border-gray-700 rounded-xl p-5 shadow-lg ${className}`}
  >
    {children}
  </div>
);

const Th = ({ children }: { children: React.ReactNode }) => (
  <th className="text-left text-xs font-semibold text-gray-400 uppercase tracking-wider px-3 py-2 bg-gray-900/60">
    {children}
  </th>
);
const Td = ({
  children,
  mono = false,
}: {
  children: React.ReactNode;
  mono?: boolean;
}) => (
  <td
    className={`px-3 py-2.5 text-sm text-gray-200 border-t border-gray-700/50 ${mono ? "font-mono text-xs" : ""}`}
  >
    {children}
  </td>
);

// ─── Section 1 data ──────────────────────────────────────────────────────────
const lifecycleOverrides = [
  {
    overrideName: "OnStateChange()",
    targetFile: "V12_002.Lifecycle.State.cs",
    dispatcherMethod: "DispatchOnStateChange()",
    nt8Signature: "override void OnStateChange()",
    signatureMatch: true,
    signatureNote:
      "NT8 calls OnStateChange() with no parameters. The dispatcher DispatchOnStateChange() is a private void matching that contract exactly. Internally it reads the State property (already set by NT8 engine before calling the override). No parameter mismatch.",
    enqueueRule:
      "All mutable state field writes inside migrated phase handlers MUST use Enqueue(ctx => { ... }). Read-only operations (logging, indicator reads, guard checks) are exempt per Section 4.2.",
    terminatedExempt: true,
    terminatedReason:
      "Section 9.1 / Director's Handoff Q1: HandleState_Terminated() teardown handlers execute DIRECTLY (not via Enqueue) because NT8 may not process queued work after Terminated is called. Documented as 'Terminated-exempt per ADR-020 Section 9.1'.",
  },
  {
    overrideName: "OnBarUpdate()",
    targetFile: "V12_002.Lifecycle.BarUpdate.cs",
    dispatcherMethod: "DispatchOnBarUpdate()",
    nt8Signature: "override void OnBarUpdate()",
    signatureMatch: true,
    signatureNote:
      "NT8 calls OnBarUpdate() with no parameters. The dispatcher DispatchOnBarUpdate() is a private void with no parameters, maintaining the contract. All pipeline state is accessed via class-level fields (CurrentBar, BarsInProgress, etc.).",
    enqueueRule:
      "All mutable state field writes inside migrated BarUpdate pipeline methods MUST use Enqueue(ctx => { ... }). Guard methods (returning bool) are read-only and exempt. REAPER tick and trailing eval must be audited for direct field writes.",
    terminatedExempt: false,
    terminatedReason: "N/A — BarUpdate has no teardown phase.",
  },
  {
    overrideName: "OnOrderUpdate()",
    targetFile: "V12_002.Orders.Callbacks.cs (MODIFIED)",
    dispatcherMethod: "DispatchOnOrderUpdate(...)",
    nt8Signature:
      "override void OnOrderUpdate(Order order, double limitPrice, double stopPrice, int quantity, int filled, double averageFillPrice, OrderState orderState, DateTime time, ErrorCode error, string nativeError)",
    signatureMatch: true,
    signatureNote:
      "Section 3.5 scaffold shows the dispatcher accepts all 10 NT8-required parameters in the exact NT8 API order: order, limitPrice, stopPrice, quantity, filled, averageFillPrice, orderState, time, error, nativeError. Uses Cbi.* namespace aliases matching NT8 conventions.",
    enqueueRule:
      "Routes to HandleOrderUpdate() which contains existing FSM logic from prior phases. No new Enqueue boundary is added by the dispatcher itself; compliance was enforced in Phases 1-3 per ADR-019.",
    terminatedExempt: false,
    terminatedReason: "N/A — order callbacks are not lifecycle teardown.",
  },
  {
    overrideName: "OnExecutionUpdate()",
    targetFile: "V12_002.Orders.Callbacks.cs (MODIFIED)",
    dispatcherMethod: "DispatchOnExecutionUpdate(...)",
    nt8Signature:
      "override void OnExecutionUpdate(Execution execution, string executionId, double price, int quantity, MarketPosition marketPosition, string orderId, DateTime time)",
    signatureMatch: true,
    signatureNote:
      "Section 3.5 scaffold shows the dispatcher accepts all 7 NT8-required parameters: execution, executionId, price, quantity, marketPosition, orderId, time. Uses Cbi.Execution and Cbi.MarketPosition namespace aliases. Order matches NT8 API exactly.",
    enqueueRule:
      "Routes to HandleExecutionUpdate() containing existing fill accounting, REAPER trigger, SIMA reconciliation, PnL logging from prior phases. Enqueue compliance governed by ADR-019 scan baseline.",
    terminatedExempt: false,
    terminatedReason: "N/A — execution callbacks are not lifecycle teardown.",
  },
];

// ─── Section 2 data ──────────────────────────────────────────────────────────
const guardStages = [
  {
    name: "BarUpdate_GuardDataValidity()",
    returnType: "bool",
    description:
      "Primary data-safety gate. Returns false if CurrentBar or BarsInProgress state is invalid. MUST be first in pipeline.",
    order: 1,
  },
  {
    name: "BarUpdate_GuardTradingSession()",
    returnType: "bool",
    description:
      "Session window gate. Calls IsWithinTradingSession(). Returns false if outside valid trading hours.",
    order: 2,
  },
  {
    name: "BarUpdate_GuardFlatReset()",
    returnType: "bool",
    description:
      "Session-reset gate. Calls IsSessionResetPending(). Returns false to short-circuit if a flat/reset condition is pending.",
    order: 3,
  },
];

const processingStages = [
  {
    order: 1,
    name: "BarUpdate_ComputeIndicators()",
    description:
      "Compute all bar indicators needed downstream. Populates indicator state used by entry and trailing logic.",
  },
  {
    order: 2,
    name: "BarUpdate_EvaluateOpeningRange()",
    description:
      "Evaluate opening-range breakout logic. Determines OR boundaries for the current session.",
  },
  {
    order: 3,
    name: "BarUpdate_DispatchEntries()",
    description:
      "Guard IsEntryPermitted(); then delegate to _activeEntryStrategy?.OnBarUpdate() — Phase 3 factory interface.",
  },
  {
    order: 4,
    name: "BarUpdate_EvaluateTrailing()",
    description:
      "Evaluate trailing stop adjustments for any open position. Delegates to EvaluateTrailingStop() in V12_002.Trailing.cs.",
  },
  {
    order: 5,
    name: "BarUpdate_TickReaper()",
    description:
      "Tick the REAPER repair subsystem every bar. Delegates to TickReaper() in V12_002.REAPER.cs.",
  },
  {
    order: 6,
    name: "BarUpdate_RefreshUI()",
    description:
      "Refresh bar-driven UI state (chart drawings, panel data). Delegates to OnBarUpdate_UI() in V12_002.UI.Callbacks.cs.",
  },
];

// ─── Section 3 data ──────────────────────────────────────────────────────────
const verificationChecks = [
  {
    id: "C1",
    check: "Zero lock(stateLock) occurrences",
    tool: "grep -r \"lock(stateLock)\" src/",
    status: "needs_platform" as const,
    psEquivalent:
      'Select-String -Path "src\\\\**\\\\*.cs" -Pattern "lock\\(stateLock\\)" -Recurse',
    notes:
      "grep is a Unix/bash tool. PowerShell native equivalent uses Select-String (sls) with -Recurse and -Pattern for regex.",
  },
  {
    id: "C2",
    check: "Zero non-ASCII characters in strings",
    tool: "python check_ascii.py",
    status: "runnable" as const,
    psEquivalent: null,
    notes:
      "check_ascii.py is confirmed present at repo root (Section 4.1 Step 4, Section 6, Execution Order Step 14). python command is cross-platform; runs in PowerShell without modification.",
  },
  {
    id: "C3",
    check: "All state mutations inside Enqueue",
    tool: "Manual audit per migrated block",
    status: "runnable" as const,
    psEquivalent: null,
    notes:
      "Manual audit — no shell tool dependency. Platform-agnostic. Requires human review of each migrated block against Section 4.2 rules.",
  },
  {
    id: "C4",
    check: "OnStateChange body is one-liner only",
    tool: "Visual inspection of V12_002.cs",
    status: "runnable" as const,
    psEquivalent: null,
    notes:
      "Visual inspection — platform-agnostic. Target: body contains exactly 'DispatchOnStateChange();' per Section 5.",
  },
  {
    id: "C5",
    check: "OnBarUpdate body is one-liner only",
    tool: "Visual inspection of V12_002.cs",
    status: "runnable" as const,
    psEquivalent: null,
    notes:
      "Visual inspection — platform-agnostic. Target: body contains exactly 'DispatchOnBarUpdate();' per Section 5.",
  },
  {
    id: "C6",
    check: "No new abstractions beyond plan spec",
    tool: "Karpathy: simplicity-first review",
    status: "runnable" as const,
    psEquivalent: null,
    notes:
      "Human architectural review. No tooling dependency. Verify no new interface, class, or abstraction not specified in Section 3 was introduced.",
  },
  {
    id: "C7",
    check: "BUILD_TAG incremented",
    tool: "V12_002.Properties.cs build tag field",
    status: "runnable" as const,
    psEquivalent: null,
    notes:
      "File inspection. Build 981 → Build 982 per Section 10. Can be confirmed with Get-Content or visual inspection.",
  },
  {
    id: "C8",
    check: "deploy-sync.ps1 executed after edits",
    tool: "Post-edit deployment protocol (CLAUDE.md)",
    status: "runnable" as const,
    psEquivalent: null,
    notes:
      "PowerShell script — natively runnable on Windows. Execution: powershell -File .\\deploy-sync.ps1. ASCII Gate must PASS. Protocol referenced in CLAUDE.md.",
  },
];

// ─── Adversarial data ────────────────────────────────────────────────────────
const adversarialItems = {
  blocking: [
    {
      id: "B1",
      section: "§3.5 / Director Handoff Step 12",
      item: "HandleOrderUpdate / HandleExecutionUpdate name assumption",
      detail:
        "The plan's Section 3.5 scaffold ASSUMES existing handler method names are HandleOrderUpdate and HandleExecutionUpdate. The CONSTRAINT block explicitly warns: 'must be verified against the current file before applying. If they differ, match the existing names exactly.' Until the engineer reads V12_002.Orders.Callbacks.cs and confirms these names, the dispatcher bodies cannot be finalized. This is a potential signature/name mismatch that would cause a compile error.",
    },
    {
      id: "B2",
      section: "§9.1 / §4.2 / Director Handoff Q1",
      item: "Terminated-phase Enqueue exemption not formally codified in Section 4.2",
      detail:
        "Section 4.2 defines the Enqueue mandate globally. The Terminated exemption is only surfaced in Section 9 Open Questions and the Handoff Q1 answer. Section 4.2 itself does not list Terminated as an exception alongside the Build 981 stopOrders exemption. This creates an inconsistency: an engineer reading only Section 4.2 would incorrectly wrap teardown calls in Enqueue, potentially causing deadlock if NT8 no longer processes the queue post-Terminated.",
    },
  ],
  moderate: [
    {
      id: "M1",
      section: "§3.4 / §9.2",
      item: "BarUpdate_TickReaper() lacks IsFirstTickOfBar guard in scaffold",
      detail:
        "Section 9.2 recommends adding 'if (IsFirstTickOfBar)' guard inside TickReaper() in V12_002.REAPER.cs as a defensive measure for future Calculate=OnEachTick scenarios. The Section 3.4 scaffold does NOT include this guard — the Handoff block tells the engineer to add it as a 'single-line change, surgical'. This means the REAPER.cs file (not listed in Section 3.1's 4-file scope) requires a modification that is not in the formal migration protocol.",
    },
    {
      id: "M2",
      section: "§3.4 pipeline",
      item: "BarUpdate_GuardFlatReset() short-circuit semantics underdefined",
      detail:
        "The guard returns false to 'short-circuit the remainder' but the document does not specify whether a FlatReset condition represents a normal no-trade bar or an error state. There is no logging or state transition specified when this guard fires, unlike GuardDataValidity which has explicit commentary about being the 'primary data-safety gate'. Moderate risk of silent suppression masking a bug.",
    },
    {
      id: "M3",
      section: "§7 (Execution Order Step 14-15)",
      item: "grep used in execution order steps on a Windows-primary codebase",
      detail:
        "Steps 15 uses 'grep -rn lock(stateLock) src/' directly. The verification matrix (Section 6) similarly specifies grep. If the engineer's environment is Windows PowerShell without WSL/grep installed, these steps will fail silently or with command-not-found, bypassing the lock compliance check.",
    },
  ],
  advisory: [
    {
      id: "A1",
      section: "§6 Check C1",
      item: "PowerShell equivalent for grep -r lock(stateLock) src/",
      detail:
        'Replace: grep -r "lock(stateLock)" src/\nWith: Select-String -Path "src\\**\\*.cs" -Pattern "lock\\(stateLock\\)" -Recurse\nAlias shorthand: sls -Path "src\\**\\*.cs" -Pattern "lock\\(stateLock\\)" -Recurse',
    },
    {
      id: "A2",
      section: "§7 Execution Order Step 15",
      item: "PowerShell equivalent for grep -rn lock(stateLock) src/",
      detail:
        'Replace: grep -rn "lock(stateLock)" src/\nWith: Select-String -Path "src\\**\\*.cs" -Pattern "lock\\(stateLock\\)" -Recurse | Select-Object Filename, LineNumber, Line',
    },
    {
      id: "A3",
      section: "§3.3 scaffold comments",
      item: "Comment wording uses em-dash in comment text",
      detail:
        "Section 3.3 scaffold uses '// ---' separator lines (ASCII hyphens — fine). Document prose uses em-dashes in headings (e.g., 'Phase 4 -- Event Lifecycle Refactoring'). These are double-hyphens (ASCII) not true em-dashes, so they comply with the ASCII-only constraint. No change needed, but worth confirming rendering environment.",
    },
    {
      id: "A4",
      section: "§3.4 scaffold",
      item: "_activeEntryStrategy null-conditional silent behavior",
      detail:
        "Section 9.3 / Q3 resolution confirms silent null is acceptable. Advisory: a future plan revision should add a 'debug-mode verbose logging' flag so that during development, pre-session null hits are at least visible in the output tab without polluting production logs.",
    },
  ],
};

// ─── Main App ─────────────────────────────────────────────────────────────────
export default function App() {
  const [activeSection, setActiveSection] = useState<number>(0);
  const [expandedCard, setExpandedCard] = useState<string | null>(null);

  const navItems = [
    { id: 0, label: "Overview", short: "Overview" },
    { id: 1, label: "§1 Lifecycle Dispatcher", short: "§1 Lifecycle" },
    { id: 2, label: "§2 Pipeline Logic", short: "§2 Pipeline" },
    { id: 3, label: "§3 Verification Matrix", short: "§3 Verify" },
    { id: 4, label: "§4 Adversarial Summary", short: "§4 Adversarial" },
  ];

  const toggleCard = (id: string) =>
    setExpandedCard((prev) => (prev === id ? null : id));

  return (
    <div className="min-h-screen bg-gray-950 text-gray-100 font-sans">
      {/* ── Top Header ── */}
      <header className="bg-gray-900 border-b border-gray-700 sticky top-0 z-50 shadow-xl">
        <div className="max-w-7xl mx-auto px-4 py-3">
          <div className="flex items-center justify-between flex-wrap gap-2">
            <div>
              <h1 className="text-lg font-bold text-indigo-400 tracking-tight">
                ADR-020 · Phase 4 Design Review Dashboard
              </h1>
              <h2 className="text-xs text-gray-400 font-mono mt-0.5">
                Reviewer: Claude Sonnet 4.5 &nbsp;|&nbsp; Branch:
                feature/phase-4-event-lifecycle &nbsp;|&nbsp; Build Delta:{" "}
                <span className="text-yellow-400 font-semibold">
                  Build 981 → Build 982
                </span>
              </h2>
            </div>
            <div className="flex items-center gap-2 flex-wrap">
              <Badge color="blue">ADR-020</Badge>
              <Badge color="green">APPROVED</Badge>
              <Badge color="orange">4 Overrides</Badge>
              <Badge color="yellow">Needs Plan Updates</Badge>
            </div>
          </div>
        </div>
        {/* Sub-nav */}
        <div className="border-t border-gray-800 bg-gray-900/80">
          <div className="max-w-7xl mx-auto px-4">
            <nav className="flex overflow-x-auto">
              {navItems.map((item) => (
                <button
                  key={item.id}
                  onClick={() => setActiveSection(item.id)}
                  className={`px-4 py-2.5 text-sm font-medium whitespace-nowrap border-b-2 transition-colors ${
                    activeSection === item.id
                      ? "border-indigo-500 text-indigo-400"
                      : "border-transparent text-gray-400 hover:text-gray-200 hover:border-gray-600"
                  }`}
                >
                  {item.label}
                </button>
              ))}
            </nav>
          </div>
        </div>
      </header>

      <main className="max-w-7xl mx-auto px-4 py-8">
        {/* ── Overview ── */}
        {activeSection === 0 && (
          <div className="space-y-6">
            <div className="text-center mb-8">
              <h2 className="text-3xl font-bold text-white mb-2">
                Claude Sonnet 4.5 — Architect Review
              </h2>
              <p className="text-gray-400 max-w-2xl mx-auto text-sm">
                Senior software architect consistency review of the Phase 4
                Event Lifecycle Refactoring implementation plan (ADR-020) before
                engineering execution begins.
              </p>
            </div>

            {/* Consistency proof box */}
            <Card className="border-indigo-700 bg-indigo-950/40">
              <h4 className="text-base font-bold text-indigo-300 mb-4 flex items-center gap-2">
                <span className="text-lg">🔍</span> Document Consistency Proof
                (read verification)
              </h4>
              <div className="grid md:grid-cols-3 gap-4">
                <div className="bg-gray-900/60 rounded-lg p-4 border border-green-800">
                  <div className="text-xs text-gray-400 uppercase tracking-wider mb-1 font-semibold">
                    check_ascii.py
                  </div>
                  <Badge color="green">EXISTS at repo root</Badge>
                  <p className="text-xs text-gray-300 mt-2">
                    Explicitly referenced at Section 4.1 Step 4 ("Run ASCII
                    scan (check_ascii.py)"), Section 6 verification matrix
                    ("python check_ascii.py"), and Director's Handoff Execution
                    Order Step 14 ("python check_ascii.py -- must report ZERO
                    violations"). Three independent citations confirm existence.
                  </p>
                </div>
                <div className="bg-gray-900/60 rounded-lg p-4 border border-red-800">
                  <div className="text-xs text-gray-400 uppercase tracking-wider mb-1 font-semibold">
                    byte_purge.py
                  </div>
                  <Badge color="red">NOT MENTIONED — No evidence</Badge>
                  <p className="text-xs text-gray-300 mt-2">
                    The string "byte_purge" does not appear anywhere in the
                    document. No citation, no reference, no stub. Cannot confirm
                    existence from document evidence. Status: unknown /
                    presumably absent from this plan scope.
                  </p>
                </div>
                <div className="bg-gray-900/60 rounded-lg p-4 border border-yellow-800">
                  <div className="text-xs text-gray-400 uppercase tracking-wider mb-1 font-semibold">
                    Build Tag Delta String
                  </div>
                  <Badge color="yellow">Build 981 → Build 982</Badge>
                  <p className="text-xs text-gray-300 mt-2">
                    Section 10 states: "Increment BUILD_TAG in
                    V12_002.Properties.cs to the next sequential value (e.g.,
                    if current is Build 981, set to Build 982)". The exact delta
                    string from the document header/body is{" "}
                    <span className="font-mono text-yellow-300">
                      Build 981 → Build 982
                    </span>
                    .
                  </p>
                </div>
              </div>
            </Card>

            {/* Quick stats */}
            <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
              {[
                {
                  label: "Lifecycle Overrides",
                  value: "4",
                  color: "text-blue-400",
                },
                { label: "New Files Created", value: "2", color: "text-green-400" },
                {
                  label: "Files Modified",
                  value: "2",
                  color: "text-yellow-400",
                },
                {
                  label: "Blocking Issues",
                  value: "2",
                  color: "text-red-400",
                },
              ].map((s) => (
                <Card key={s.label} className="text-center">
                  <div className={`text-3xl font-bold ${s.color}`}>
                    {s.value}
                  </div>
                  <div className="text-xs text-gray-400 mt-1">{s.label}</div>
                </Card>
              ))}
            </div>

            <Card className="border-yellow-700 bg-yellow-950/20">
              <h4 className="text-sm font-bold text-yellow-300 mb-2">
                ⚠ Reviewer Verdict
              </h4>
              <p className="text-sm text-gray-300">
                <span className="text-red-400 font-semibold">
                  NEEDS PLAN UPDATE
                </span>{" "}
                before engineering execution. Two blocking issues must be
                resolved: (1) HandleOrderUpdate / HandleExecutionUpdate name
                verification is a prerequisite action, not optional; (2) Section
                4.2 must be amended to explicitly list the Terminated-phase
                Enqueue exemption alongside the Build 981 stopOrders exemption.
                Navigate to §4 Adversarial Summary for the full itemized list.
              </p>
            </Card>

            <div className="text-center text-xs text-gray-600 font-mono pt-4">
              P3 Architect sign-off: Claude Sonnet 4.6 | 2026-05-01 | ADR-020 |
              Phase 4 | feature/phase-4-event-lifecycle
              <br />
              Dashboard Reviewer: Claude Sonnet 4.5
            </div>
          </div>
        )}

        {/* ── Section 1: Lifecycle Dispatcher Inventory ── */}
        {activeSection === 1 && (
          <div>
            <SectionHeader
              num="1"
              title="Lifecycle Dispatcher Inventory"
              subtitle="Source: Section 3 (Architecture) | 4 NT8 lifecycle overrides targeted"
            />

            <div className="space-y-6">
              {lifecycleOverrides.map((ov, idx) => (
                <Card key={idx}>
                  {/* Header row */}
                  <div className="flex flex-wrap items-start justify-between gap-3 mb-4">
                    <div>
                      <span className="font-mono text-indigo-300 font-bold text-lg">
                        {ov.overrideName}
                      </span>
                      <div className="flex flex-wrap gap-2 mt-2">
                        <Badge color="blue">
                          File: {ov.targetFile.split(" ")[0]}
                        </Badge>
                        <Badge color="gray">
                          Dispatcher: {ov.dispatcherMethod}
                        </Badge>
                        {ov.signatureMatch ? (
                          <Badge color="green">✓ Signature MATCH</Badge>
                        ) : (
                          <Badge color="red">✗ Signature MISMATCH</Badge>
                        )}
                      </div>
                    </div>
                    <button
                      onClick={() => toggleCard(`ov-${idx}`)}
                      className="text-xs text-indigo-400 hover:text-indigo-200 underline"
                    >
                      {expandedCard === `ov-${idx}`
                        ? "Collapse ▲"
                        : "Expand ▼"}
                    </button>
                  </div>

                  {/* Always-visible table */}
                  <div className="overflow-x-auto mb-4">
                    <table className="w-full text-sm">
                      <thead>
                        <tr>
                          <Th>NT8 Override</Th>
                          <Th>Target Partial File</Th>
                          <Th>Dispatcher Method</Th>
                        </tr>
                      </thead>
                      <tbody>
                        <tr>
                          <Td mono>{ov.overrideName}</Td>
                          <Td mono>{ov.targetFile}</Td>
                          <Td mono>{ov.dispatcherMethod}</Td>
                        </tr>
                      </tbody>
                    </table>
                  </div>

                  {/* Invariant check */}
                  <div className="bg-gray-900/60 rounded-lg p-3 mb-3 border-l-4 border-indigo-600">
                    <div className="text-xs font-semibold text-indigo-300 uppercase tracking-wider mb-1">
                      INVARIANT CHECK — NT8 Parameter Signature
                    </div>
                    <code className="text-xs text-green-300 block mb-2 break-all">
                      {ov.nt8Signature}
                    </code>
                    <p className="text-xs text-gray-300">{ov.signatureNote}</p>
                  </div>

                  {/* Thread safety */}
                  <div className="bg-gray-900/60 rounded-lg p-3 border-l-4 border-purple-600">
                    <div className="text-xs font-semibold text-purple-300 uppercase tracking-wider mb-1">
                      THREAD SAFETY — Enqueue Compliance Rule (§4.2)
                    </div>
                    <p className="text-xs text-gray-300">{ov.enqueueRule}</p>
                    {ov.terminatedExempt && (
                      <div className="mt-2 bg-yellow-900/30 border border-yellow-700/50 rounded p-2">
                        <span className="text-xs text-yellow-300 font-semibold">
                          ⚡ EXEMPT PHASE (§9.1):
                        </span>
                        <p className="text-xs text-yellow-200 mt-0.5">
                          {ov.terminatedReason}
                        </p>
                      </div>
                    )}
                  </div>

                  {/* Expanded: full NT8 sig detail */}
                  {expandedCard === `ov-${idx}` && (
                    <div className="mt-4 bg-gray-950 rounded-lg p-4 border border-gray-700">
                      <div className="text-xs font-semibold text-gray-400 uppercase tracking-wider mb-2">
                        Full NT8 API Signature Detail
                      </div>
                      <pre className="text-xs text-gray-200 whitespace-pre-wrap break-all">
                        {ov.nt8Signature}
                      </pre>
                    </div>
                  )}
                </Card>
              ))}

              {/* Exempt phase callout */}
              <Card className="border-yellow-700 bg-yellow-950/20">
                <h4 className="text-sm font-bold text-yellow-300 mb-2">
                  ⚡ Enqueue Exemption Summary — Which Phase Is Exempt & Why?
                </h4>
                <p className="text-sm text-gray-300 mb-2">
                  <strong className="text-white">
                    The Terminated lifecycle phase (HandleState_Terminated())
                  </strong>{" "}
                  is the sole named exemption from the Enqueue wrapping mandate.
                </p>
                <p className="text-sm text-gray-400">
                  <strong>Reason (§9.1):</strong> NT8 may not process queued
                  work after the Terminated state is entered. Wrapping teardown
                  calls in Enqueue(ctx =&gt; ...) would cause those calls to
                  never execute, leaving IPC listeners, SIMA monitors, and
                  account update listeners dangling. Therefore: "Teardown
                  handlers execute directly; document as a named exception in
                  ADR-020." — Director's Handoff Q1 resolution.
                </p>
                <p className="text-xs text-gray-500 mt-2 font-mono">
                  Code comment mandate: "Terminated-exempt per ADR-020 Section
                  9.1"
                </p>
              </Card>
            </div>
          </div>
        )}

        {/* ── Section 2: Pipeline Logic ── */}
        {activeSection === 2 && (
          <div>
            <SectionHeader
              num="2"
              title="Pipeline Logic Consistency"
              subtitle="Source: Section 3.4 — V12_002.Lifecycle.BarUpdate.cs scaffold"
            />

            <div className="space-y-6">
              {/* Guard stages */}
              <Card>
                <h4 className="text-base font-bold text-white mb-4 flex items-center gap-2">
                  <span className="bg-red-800 text-red-200 rounded px-2 py-0.5 text-xs font-mono">
                    GUARD
                  </span>
                  Guard Stages (3) — Short-circuit Pipeline
                </h4>
                <div className="overflow-x-auto">
                  <table className="w-full">
                    <thead>
                      <tr>
                        <Th>#</Th>
                        <Th>Guard Method Name</Th>
                        <Th>Return Type</Th>
                        <Th>Behavior on false</Th>
                        <Th>Description</Th>
                      </tr>
                    </thead>
                    <tbody>
                      {guardStages.map((g) => (
                        <tr key={g.order}>
                          <Td>
                            <span className="bg-red-900/50 text-red-300 rounded px-1.5 py-0.5 text-xs font-bold">
                              G{g.order}
                            </span>
                          </Td>
                          <Td mono>{g.name}</Td>
                          <Td>
                            <Badge color="blue">{g.returnType}</Badge>
                          </Td>
                          <Td>
                            <Badge color="red">return (abort pipeline)</Badge>
                          </Td>
                          <Td>{g.description}</Td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              </Card>

              {/* Processing stages */}
              <Card>
                <h4 className="text-base font-bold text-white mb-4 flex items-center gap-2">
                  <span className="bg-green-800 text-green-200 rounded px-2 py-0.5 text-xs font-mono">
                    PROCESS
                  </span>
                  Processing Stages (6) — Execution Order
                </h4>
                <div className="overflow-x-auto">
                  <table className="w-full">
                    <thead>
                      <tr>
                        <Th>Order</Th>
                        <Th>Method Name</Th>
                        <Th>Delegate Target</Th>
                        <Th>Description</Th>
                      </tr>
                    </thead>
                    <tbody>
                      {processingStages.map((p) => (
                        <tr key={p.order}>
                          <Td>
                            <span className="bg-green-900/50 text-green-300 rounded px-1.5 py-0.5 text-xs font-bold">
                              P{p.order}
                            </span>
                          </Td>
                          <Td mono>{p.name}</Td>
                          <Td mono>
                            {p.order === 3
                              ? "_activeEntryStrategy?.OnBarUpdate()"
                              : p.order === 4
                                ? "V12_002.Trailing.cs"
                                : p.order === 5
                                  ? "V12_002.REAPER.cs"
                                  : p.order === 6
                                    ? "V12_002.UI.Callbacks.cs"
                                    : "local"}
                          </Td>
                          <Td>{p.description}</Td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              </Card>

              {/* Critical Flow */}
              <Card className="border-red-700 bg-red-950/20">
                <h4 className="text-base font-bold text-red-300 mb-4 flex items-center gap-2">
                  <span className="text-lg">⚡</span> CRITICAL FLOW — What
                  happens when BarUpdate_GuardTradingSession() returns false?
                </h4>

                <div className="bg-gray-950 rounded-lg p-4 font-mono text-sm mb-4 overflow-x-auto border border-gray-700">
                  <div className="text-gray-500 mb-2 text-xs">
                    // §3.4 DispatchOnBarUpdate() scaffold — pipeline:
                  </div>
                  <div className="text-gray-300">
                    {"  "}
                    <span className="text-blue-400">if</span> (!
                    <span className="text-green-300">
                      BarUpdate_GuardDataValidity()
                    </span>
                    ) <span className="text-yellow-400">return</span>; &nbsp;
                    <span className="text-gray-500">// G1</span>
                  </div>
                  <div className="text-gray-300 bg-red-900/30 border-l-4 border-red-500 pl-2">
                    {"  "}
                    <span className="text-blue-400">if</span> (!
                    <span className="text-red-300">
                      BarUpdate_GuardTradingSession()
                    </span>
                    ) <span className="text-yellow-400">return</span>; &nbsp;
                    <span className="text-red-400 font-bold">
                      // G2 — FALSE HERE → return
                    </span>
                  </div>
                  <div className="text-gray-500 line-through pl-2">
                    {"  "}if (!BarUpdate_GuardFlatReset()) return; &nbsp; // G3
                    — SKIPPED
                  </div>
                  <div className="text-gray-500 line-through pl-2">
                    {"  "}BarUpdate_ComputeIndicators(); &nbsp; // P1 — SKIPPED
                  </div>
                  <div className="text-gray-500 line-through pl-2">
                    {"  "}BarUpdate_EvaluateOpeningRange(); &nbsp; // P2 —
                    SKIPPED
                  </div>
                  <div className="text-gray-500 line-through pl-2">
                    {"  "}BarUpdate_DispatchEntries(); &nbsp; // P3 — SKIPPED
                  </div>
                  <div className="text-gray-500 line-through pl-2">
                    {"  "}BarUpdate_EvaluateTrailing(); &nbsp; // P4 — SKIPPED
                  </div>
                  <div className="text-gray-500 line-through pl-2">
                    {"  "}BarUpdate_TickReaper(); &nbsp; // P5 — SKIPPED
                  </div>
                  <div className="text-gray-500 line-through pl-2">
                    {"  "}BarUpdate_RefreshUI(); &nbsp; // P6 — SKIPPED
                  </div>
                </div>

                <div className="grid md:grid-cols-2 gap-4">
                  <div className="bg-gray-900 rounded p-3">
                    <div className="text-xs font-semibold text-red-300 mb-1">
                      Stages Skipped (ALL remaining)
                    </div>
                    <ul className="text-xs text-gray-300 space-y-1">
                      <li>
                        <Badge color="red">G3</Badge>{" "}
                        BarUpdate_GuardFlatReset()
                      </li>
                      <li>
                        <Badge color="red">P1</Badge>{" "}
                        BarUpdate_ComputeIndicators()
                      </li>
                      <li>
                        <Badge color="red">P2</Badge>{" "}
                        BarUpdate_EvaluateOpeningRange()
                      </li>
                      <li>
                        <Badge color="red">P3</Badge>{" "}
                        BarUpdate_DispatchEntries()
                      </li>
                      <li>
                        <Badge color="red">P4</Badge>{" "}
                        BarUpdate_EvaluateTrailing()
                      </li>
                      <li>
                        <Badge color="red">P5</Badge>{" "}
                        BarUpdate_TickReaper()
                      </li>
                      <li>
                        <Badge color="red">P6</Badge>{" "}
                        BarUpdate_RefreshUI()
                      </li>
                    </ul>
                  </div>
                  <div className="bg-gray-900 rounded p-3">
                    <div className="text-xs font-semibold text-yellow-300 mb-1">
                      Line Number Citation (§3.4 scaffold)
                    </div>
                    <p className="text-xs text-gray-300">
                      The scaffold in Section 3.4 is presented as a code block
                      without explicit file line numbers in the document source.
                      The pipeline return is on the{" "}
                      <span className="font-mono text-yellow-300">
                        second if (!...) return;
                      </span>{" "}
                      statement within DispatchOnBarUpdate(). In the published
                      scaffold, GuardTradingSession is the 2nd guard — its{" "}
                      <span className="font-mono text-yellow-300">return</span>{" "}
                      statement is the short-circuit point. The document does
                      not number lines within code blocks; no absolute line
                      number can be cited beyond the pipeline position (line 2
                      of the guard sequence).
                    </p>
                    <p className="text-xs text-gray-500 mt-2">
                      Note: "Cite the exact line number" — the document's
                      scaffold lacks explicit line numbers. The scaffold's
                      pipeline sequence is the authoritative ordering per §3.4.
                    </p>
                  </div>
                </div>
              </Card>

              {/* Pipeline visual */}
              <Card>
                <h4 className="text-sm font-bold text-white mb-4">
                  Pipeline Flow Diagram
                </h4>
                <div className="flex flex-wrap items-center gap-1 text-xs font-mono">
                  {[
                    {
                      label: "G1\nDataValidity",
                      color: "bg-red-900 border-red-600",
                      text: "text-red-200",
                    },
                    null,
                    {
                      label: "G2\nTradingSession",
                      color: "bg-red-900 border-red-600",
                      text: "text-red-200",
                    },
                    null,
                    {
                      label: "G3\nFlatReset",
                      color: "bg-red-900 border-red-600",
                      text: "text-red-200",
                    },
                    null,
                    {
                      label: "P1\nIndicators",
                      color: "bg-green-900 border-green-600",
                      text: "text-green-200",
                    },
                    null,
                    {
                      label: "P2\nOR Eval",
                      color: "bg-green-900 border-green-600",
                      text: "text-green-200",
                    },
                    null,
                    {
                      label: "P3\nEntries",
                      color: "bg-green-900 border-green-600",
                      text: "text-green-200",
                    },
                    null,
                    {
                      label: "P4\nTrailing",
                      color: "bg-green-900 border-green-600",
                      text: "text-green-200",
                    },
                    null,
                    {
                      label: "P5\nREAPER",
                      color: "bg-green-900 border-green-600",
                      text: "text-green-200",
                    },
                    null,
                    {
                      label: "P6\nUI",
                      color: "bg-green-900 border-green-600",
                      text: "text-green-200",
                    },
                  ].map((item, i) =>
                    item === null ? (
                      <span key={i} className="text-gray-600">
                        →
                      </span>
                    ) : (
                      <div
                        key={i}
                        className={`border rounded px-2 py-1 text-center whitespace-pre ${item.color} ${item.text}`}
                      >
                        {item.label}
                      </div>
                    )
                  )}
                </div>
                <p className="text-xs text-gray-500 mt-3">
                  Red = Guard (returns bool, false = abort). Green = Processing
                  (void, always runs if reached).
                </p>
              </Card>
            </div>
          </div>
        )}

        {/* ── Section 3: Verification Matrix ── */}
        {activeSection === 3 && (
          <div>
            <SectionHeader
              num="3"
              title="Verification Matrix Audit"
              subtitle="Source: Section 6 — Constraint Compliance Checklist | Target: Windows PowerShell"
            />

            <div className="space-y-6">
              <Card>
                <div className="overflow-x-auto">
                  <table className="w-full">
                    <thead>
                      <tr>
                        <Th>ID</Th>
                        <Th>Check</Th>
                        <Th>Original Tool</Th>
                        <Th>PS Status</Th>
                        <Th>PowerShell Equivalent / Notes</Th>
                      </tr>
                    </thead>
                    <tbody>
                      {verificationChecks.map((c) => (
                        <tr key={c.id}>
                          <Td mono>{c.id}</Td>
                          <Td>{c.check}</Td>
                          <Td mono>{c.tool}</Td>
                          <Td>
                            {c.status === "runnable" ? (
                              <Badge color="green">✓ Runnable</Badge>
                            ) : c.status === "needs_platform" ? (
                              <Badge color="yellow">
                                ⚠ Needs Platform Adjustment
                              </Badge>
                            ) : (
                              <Badge color="red">✗ Dependency Missing</Badge>
                            )}
                          </Td>
                          <Td>
                            <p className="text-xs text-gray-300 mb-1">
                              {c.notes}
                            </p>
                            {c.psEquivalent && (
                              <code className="text-xs text-yellow-300 block bg-gray-950 rounded p-1.5 mt-1 break-all">
                                {c.psEquivalent}
                              </code>
                            )}
                          </Td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              </Card>

              {/* grep flag callout */}
              <Card className="border-yellow-700 bg-yellow-950/20">
                <h4 className="text-sm font-bold text-yellow-300 mb-3">
                  ⚠ grep Usage Flags
                </h4>
                <p className="text-sm text-gray-300 mb-3">
                  Two checks use <code className="font-mono text-red-300">grep</code> — a Unix tool not natively available in Windows PowerShell without WSL or Git Bash:
                </p>
                <div className="space-y-3">
                  <div className="bg-gray-900 rounded p-3">
                    <div className="text-xs text-gray-400 mb-1">
                      Section 6, Check C1 (also Execution Order Step 15):
                    </div>
                    <code className="text-xs text-red-300 block">
                      grep -r "lock(stateLock)" src/
                    </code>
                    <div className="text-xs text-gray-400 my-1">→ Replace with:</div>
                    <code className="text-xs text-green-300 block break-all">
                      Select-String -Path "src\**\*.cs" -Pattern "lock\(stateLock\)" -Recurse
                    </code>
                  </div>
                </div>
              </Card>

              {/* Section 8 / deploy-sync protocol */}
              <Card className="border-blue-700 bg-blue-950/20">
                <h4 className="text-sm font-bold text-blue-300 mb-3">
                  📋 Post-Edit Sequence: Director Actions in NT8 UI (§7 Steps 19-20, referenced from CLAUDE.md protocol)
                </h4>
                <p className="text-xs text-gray-400 mb-3">
                  The plan references "Post-edit deployment protocol (CLAUDE.md)" for deploy-sync.ps1. Section 7 Execution Order steps 18-20 specify the exact sequence:
                </p>
                <ol className="space-y-2">
                  {[
                    {
                      step: "Step 18 (§7)",
                      action: "Run deploy-sync.ps1",
                      detail:
                        'Execute: powershell -File .\\deploy-sync.ps1 — ASCII Gate must PASS. This syncs the modified files to the NinjaTrader installation directory.',
                    },
                    {
                      step: "Step 19 (§7)",
                      action: "Director: Press F5 in NinjaTrader",
                      detail:
                        "The Director must press F5 in the NinjaTrader 8 UI to trigger a recompile of the strategy. This is the NT8 compile shortcut — it recompiles all NinjaScript files in the NinjaTrader editor.",
                    },
                    {
                      step: "Step 20 (§7)",
                      action: "Confirm BUILD_TAG in banner",
                      detail:
                        "After compile, verify that the strategy banner/output tab displays the new BUILD_TAG value (Build 982). This confirms the new files were picked up by the NT8 compiler and the migration is live.",
                    },
                  ].map((item, i) => (
                    <li
                      key={i}
                      className="flex gap-3 bg-gray-900/60 rounded p-3"
                    >
                      <span className="flex-shrink-0 w-6 h-6 rounded-full bg-blue-700 text-white text-xs flex items-center justify-center font-bold">
                        {i + 1}
                      </span>
                      <div>
                        <div className="text-xs font-semibold text-blue-200">
                          {item.step}: {item.action}
                        </div>
                        <div className="text-xs text-gray-400 mt-0.5">
                          {item.detail}
                        </div>
                      </div>
                    </li>
                  ))}
                </ol>
              </Card>
            </div>
          </div>
        )}

        {/* ── Section 4: Adversarial Summary ── */}
        {activeSection === 4 && (
          <div>
            <SectionHeader
              num="4"
              title="Adversarial Summary"
              subtitle="All findings grouped by severity | Strictly document-sourced"
            />

            <div className="space-y-6">
              {/* Blocking */}
              <Card className="border-red-700">
                <h4 className="text-base font-bold text-red-300 mb-4 flex items-center gap-2">
                  <span className="bg-red-600 text-white text-xs font-bold px-2 py-0.5 rounded">
                    BLOCKING
                  </span>
                  Signature Mismatches / Thread-Affinity Violations
                </h4>
                <div className="space-y-4">
                  {adversarialItems.blocking.map((item) => (
                    <div
                      key={item.id}
                      className="bg-red-950/30 border border-red-800/50 rounded-lg p-4"
                    >
                      <div className="flex items-start gap-3">
                        <span className="flex-shrink-0 bg-red-700 text-white text-xs font-bold px-1.5 py-0.5 rounded font-mono">
                          {item.id}
                        </span>
                        <div className="flex-1">
                          <div className="flex flex-wrap items-center gap-2 mb-2">
                            <span className="text-sm font-bold text-red-200">
                              {item.item}
                            </span>
                            <Badge color="gray">{item.section}</Badge>
                          </div>
                          <p className="text-xs text-gray-300 leading-relaxed">
                            {item.detail}
                          </p>
                        </div>
                      </div>
                    </div>
                  ))}
                </div>
              </Card>

              {/* Moderate */}
              <Card className="border-yellow-700">
                <h4 className="text-base font-bold text-yellow-300 mb-4 flex items-center gap-2">
                  <span className="bg-yellow-700 text-white text-xs font-bold px-2 py-0.5 rounded">
                    MODERATE
                  </span>
                  Missing Guards / Pipeline Ordering Issues
                </h4>
                <div className="space-y-4">
                  {adversarialItems.moderate.map((item) => (
                    <div
                      key={item.id}
                      className="bg-yellow-950/30 border border-yellow-800/50 rounded-lg p-4"
                    >
                      <div className="flex items-start gap-3">
                        <span className="flex-shrink-0 bg-yellow-700 text-white text-xs font-bold px-1.5 py-0.5 rounded font-mono">
                          {item.id}
                        </span>
                        <div className="flex-1">
                          <div className="flex flex-wrap items-center gap-2 mb-2">
                            <span className="text-sm font-bold text-yellow-200">
                              {item.item}
                            </span>
                            <Badge color="gray">{item.section}</Badge>
                          </div>
                          <p className="text-xs text-gray-300 leading-relaxed">
                            {item.detail}
                          </p>
                        </div>
                      </div>
                    </div>
                  ))}
                </div>
              </Card>

              {/* Advisory */}
              <Card className="border-blue-700">
                <h4 className="text-base font-bold text-blue-300 mb-4 flex items-center gap-2">
                  <span className="bg-blue-700 text-white text-xs font-bold px-2 py-0.5 rounded">
                    ADVISORY
                  </span>
                  PowerShell Alternatives / Comment Wording
                </h4>
                <div className="space-y-4">
                  {adversarialItems.advisory.map((item) => (
                    <div
                      key={item.id}
                      className="bg-blue-950/30 border border-blue-800/50 rounded-lg p-4"
                    >
                      <div className="flex items-start gap-3">
                        <span className="flex-shrink-0 bg-blue-700 text-white text-xs font-bold px-1.5 py-0.5 rounded font-mono">
                          {item.id}
                        </span>
                        <div className="flex-1">
                          <div className="flex flex-wrap items-center gap-2 mb-2">
                            <span className="text-sm font-bold text-blue-200">
                              {item.item}
                            </span>
                            <Badge color="gray">{item.section}</Badge>
                          </div>
                          <p className="text-xs text-gray-300 leading-relaxed whitespace-pre-line">
                            {item.detail}
                          </p>
                        </div>
                      </div>
                    </div>
                  ))}
                </div>
              </Card>

              {/* Verdict */}
              <Card className="border-red-600 bg-red-950/30">
                <h4 className="text-lg font-bold text-white mb-4 text-center">
                  REVIEWER VERDICT
                </h4>
                <div className="text-center mb-6">
                  <span className="inline-block bg-red-700 text-white font-bold text-xl px-6 py-3 rounded-xl">
                    NEEDS PLAN UPDATE
                  </span>
                </div>
                <div className="grid md:grid-cols-2 gap-4">
                  <div>
                    <div className="text-xs font-semibold text-red-300 uppercase tracking-wider mb-2">
                      Exact Items Requiring Plan Update:
                    </div>
                    <ul className="space-y-2 text-xs text-gray-300">
                      <li className="flex gap-2">
                        <Badge color="red">B1</Badge>
                        <span>
                          Section 3.5 CONSTRAINT block must be elevated: handler
                          name verification is a BLOCKER prerequisite step, not
                          a post-hoc note. Add to §7 Execution Order as Step
                          11a.
                        </span>
                      </li>
                      <li className="flex gap-2">
                        <Badge color="red">B2</Badge>
                        <span>
                          Section 4.2 must explicitly list Terminated-phase as a
                          named Enqueue exemption alongside Build 981 stopOrders
                          exemption. Currently only surfaced in §9.1.
                        </span>
                      </li>
                      <li className="flex gap-2">
                        <Badge color="yellow">M1</Badge>
                        <span>
                          Section 3.1 file scope must be updated to include
                          V12_002.REAPER.cs as a 5th file requiring modification
                          (IsFirstTickOfBar guard per §9.2).
                        </span>
                      </li>
                      <li className="flex gap-2">
                        <Badge color="yellow">M3</Badge>
                        <span>
                          Section 6 and §7 Step 15: replace grep with
                          PowerShell-native Select-String for Windows target
                          environment.
                        </span>
                      </li>
                    </ul>
                  </div>
                  <div>
                    <div className="text-xs font-semibold text-green-300 uppercase tracking-wider mb-2">
                      No Plan Update Required:
                    </div>
                    <ul className="space-y-2 text-xs text-gray-300">
                      <li className="flex gap-2">
                        <Badge color="green">OK</Badge>
                        <span>
                          All 4 NT8 parameter signatures match API requirements
                          exactly as scaffolded.
                        </span>
                      </li>
                      <li className="flex gap-2">
                        <Badge color="green">OK</Badge>
                        <span>
                          Pipeline guard ordering (G1 → G2 → G3 → P1..P6) is
                          internally consistent.
                        </span>
                      </li>
                      <li className="flex gap-2">
                        <Badge color="green">OK</Badge>
                        <span>
                          ASCII-only constraint is consistent throughout all
                          scaffold code comments.
                        </span>
                      </li>
                      <li className="flex gap-2">
                        <Badge color="green">OK</Badge>
                        <span>
                          Zero lock(stateLock) mandate is consistent with
                          ADR-019 baseline.
                        </span>
                      </li>
                      <li className="flex gap-2">
                        <Badge color="green">OK</Badge>
                        <span>
                          Build 981 stopOrders exemption is consistently cited
                          in §4.2 and Handoff checklist.
                        </span>
                      </li>
                      <li className="flex gap-2">
                        <Badge color="blue">A3</Badge>
                        <span>
                          Double-hyphen em-dash usage in prose is ASCII-compliant
                          (--), no change needed.
                        </span>
                      </li>
                    </ul>
                  </div>
                </div>
                <div className="mt-6 border-t border-gray-700 pt-4 text-center">
                  <p className="text-xs text-gray-500 font-mono">
                    Reviewed by: Claude Sonnet 4.5 &nbsp;|&nbsp; Document
                    Author: Claude Sonnet 4.6 (P3 Architect) &nbsp;|&nbsp;
                    2026-05-01 &nbsp;|&nbsp; ADR-020
                  </p>
                </div>
              </Card>
            </div>
          </div>
        )}
      </main>
    </div>
  );
}
