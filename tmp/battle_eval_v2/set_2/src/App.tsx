import React, { useState } from "react";

// ─────────────────────────────────────────────────────────────────────────────
// Types
// ─────────────────────────────────────────────────────────────────────────────
type Severity = "blocking" | "moderate" | "advisory" | "pass";

interface Finding {
  id: string;
  severity: Severity;
  section: string;
  title: string;
  detail: string;
}

// ─────────────────────────────────────────────────────────────────────────────
// Data — sourced exclusively from the document
// ─────────────────────────────────────────────────────────────────────────────

const MODEL = "Claude Sonnet 4.5";

/* ── Section 3.2 Dispatcher Inventory ─────────────────────────────────────── */
const lifecycleOverrides = [
  {
    ntOverride: "OnStateChange()",
    targetPartialFile: "V12_002.Lifecycle.State.cs (NEW)",
    dispatcherMethod: "DispatchOnStateChange()",
    paramSignature: "void — no parameters (NT8 API reads State property internally)",
    signatureMatch: true,
    signatureNote:
      "NT8 requires the override to live in the concrete class (Section 3.2 ARCHITECT NOTE). The dispatcher preserves the zero-parameter contract of the NT8 override exactly.",
    enqueueRule:
      "All mutable state field writes inside migrated phase handlers MUST use Enqueue(ctx => { ... }). Read-only operations (logging, indicator reads, guard checks) are exempt (Section 4.2).",
    terminatedExemption:
      "HandleState_Terminated() teardown handlers execute directly on the NT8 thread — Enqueue is NOT used here because NT8 may not process queued work after Terminated is called. Named exception per ADR-020 Section 9.1.",
  },
  {
    ntOverride: "OnBarUpdate()",
    targetPartialFile: "V12_002.Lifecycle.BarUpdate.cs (NEW)",
    dispatcherMethod: "DispatchOnBarUpdate()",
    paramSignature: "void — no parameters (NT8 API contract)",
    signatureMatch: true,
    signatureNote:
      "NT8 OnBarUpdate() is a no-parameter override. The dispatcher preserves this signature exactly (Section 3.2 scaffold).",
    enqueueRule:
      "All mutable state writes in BarUpdate pipeline stages must use Enqueue(ctx => ...). Guard stages return bool and perform read-only checks — Enqueue not required for guard return values.",
    terminatedExemption: "N/A — OnBarUpdate is not called during Terminated.",
  },
  {
    ntOverride: "OnOrderUpdate(...)",
    targetPartialFile: "V12_002.Orders.Callbacks.cs (MODIFIED)",
    dispatcherMethod: "DispatchOnOrderUpdate(...)",
    paramSignature:
      "(Cbi.Order order, double limitPrice, double stopPrice, int quantity, int filled, double averageFillPrice, Cbi.OrderState orderState, DateTime time, Cbi.ErrorCode error, string nativeError)",
    signatureMatch: true,
    signatureNote:
      "The dispatcher method reproduces all 10 NT8 API parameters verbatim (Section 3.2 and 3.5 scaffold). Null-guard on order is added; no signature deviation.",
    enqueueRule:
      "Routes through existing HandleOrderUpdate() — Enqueue compliance was established in prior phases; Phase 4 only adds the thin dispatcher header.",
    terminatedExemption: "N/A.",
  },
  {
    ntOverride: "OnExecutionUpdate(...)",
    targetPartialFile: "V12_002.Orders.Callbacks.cs (MODIFIED)",
    dispatcherMethod: "DispatchOnExecutionUpdate(...)",
    paramSignature:
      "(Cbi.Execution execution, string executionId, double price, int quantity, Cbi.MarketPosition marketPosition, string orderId, DateTime time)",
    signatureMatch: true,
    signatureNote:
      "The dispatcher reproduces all 7 NT8 API parameters verbatim (Section 3.2 and 3.5 scaffold). Null-guard on execution is added; no signature deviation.",
    enqueueRule:
      "Routes through existing HandleExecutionUpdate() — same rationale as OnOrderUpdate.",
    terminatedExemption: "N/A.",
  },
];

/* ── Section 3.4 Pipeline Stages ──────────────────────────────────────────── */
const guardStages = [
  {
    name: "BarUpdate_GuardDataValidity()",
    returnType: "bool",
    description:
      "Returns false if CurrentBar < BarsRequiredToTrade OR BarsInProgress != 0. Primary data-safety gate; MUST be first in pipeline.",
    order: 1,
  },
  {
    name: "BarUpdate_GuardTradingSession()",
    returnType: "bool",
    description:
      "Returns false if outside configured trading session window. Reads SessionOpen/SessionClose params from V12_002.Properties.cs.",
    order: 2,
  },
  {
    name: "BarUpdate_GuardFlatReset()",
    returnType: "bool",
    description:
      "Returns false if an end-of-session flat-reset is in progress. Prevents new entries firing during graceful session close.",
    order: 3,
  },
];

const processingStages = [
  {
    order: 1,
    name: "BarUpdate_ComputeIndicators()",
    returnType: "void",
    description:
      "Recomputes all indicator series used by this bar. Hook for any derived/computed indicator state that V12 maintains manually.",
  },
  {
    order: 2,
    name: "BarUpdate_EvaluateOpeningRange()",
    returnType: "void",
    description:
      "Evaluates Opening Range boundaries. Runs on every bar during OR formation window; no-ops after OR is confirmed locked. Delegates to EvaluateOrBoundaries() in V12_002.Entries.OR.cs.",
  },
  {
    order: 3,
    name: "BarUpdate_DispatchEntries()",
    returnType: "void",
    description:
      "Routes to active entry strategy via factory interface. Calls _activeEntryStrategy?.OnBarUpdate() (Phase 3). Inner guard: if (!IsEntryPermitted()) return.",
  },
  {
    order: 4,
    name: "BarUpdate_EvaluateTrailing()",
    returnType: "void",
    description:
      "Evaluates trailing stop adjustments for any open position. Delegates to EvaluateTrailingStop() in V12_002.Trailing.cs.",
  },
  {
    order: 5,
    name: "BarUpdate_TickReaper()",
    returnType: "void",
    description:
      "Ticks the REAPER repair subsystem every bar to evaluate orphaned brackets. Delegates to TickReaper() in V12_002.REAPER.cs.",
  },
  {
    order: 6,
    name: "BarUpdate_RefreshUI()",
    returnType: "void",
    description:
      "Refreshes bar-driven UI state (chart drawings, panel data). Delegates to OnBarUpdate_UI() in V12_002.UI.Callbacks.cs.",
  },
];

// Critical flow: if BarUpdate_GuardTradingSession() returns false,
// what is skipped? The dispatcher is:
//   if (!GuardDataValidity()) return;      // line ~1 of dispatcher
//   if (!GuardTradingSession()) return;    // line ~2 — if FALSE, returns here
//   if (!GuardFlatReset()) return;         // SKIPPED
//   BarUpdate_ComputeIndicators();         // SKIPPED
//   BarUpdate_EvaluateOpeningRange();      // SKIPPED
//   BarUpdate_DispatchEntries();           // SKIPPED
//   BarUpdate_EvaluateTrailing();          // SKIPPED
//   BarUpdate_TickReaper();                // SKIPPED
//   BarUpdate_RefreshUI();                 // SKIPPED
// Section 3.4 scaffold — the dispatcher body occupies the DispatchOnBarUpdate() method.
// The exact lines in the scaffold (Section 3.4 code block):
// Line 1 (inside method): if (!BarUpdate_GuardDataValidity()) return;
// Line 2: if (!BarUpdate_GuardTradingSession()) return;   <-- short-circuit here
// Line 3: if (!BarUpdate_GuardFlatReset()) return;
// Lines 4-9: the six processing stages

const skippedBySessionGuard = [
  "BarUpdate_GuardFlatReset() [Guard 3]",
  "BarUpdate_ComputeIndicators() [Processing Stage 1]",
  "BarUpdate_EvaluateOpeningRange() [Processing Stage 2]",
  "BarUpdate_DispatchEntries() [Processing Stage 3]",
  "BarUpdate_EvaluateTrailing() [Processing Stage 4]",
  "BarUpdate_TickReaper() [Processing Stage 5]",
  "BarUpdate_RefreshUI() [Processing Stage 6]",
];

/* ── Section 6 Verification Matrix ───────────────────────────────────────── */
interface CheckRow {
  check: string;
  tool: string;
  status: "runnable" | "needs_platform_adjustment" | "dependency_missing";
  statusLabel: string;
  flag?: string;
  psEquivalent?: string;
}

const verificationChecks: CheckRow[] = [
  {
    check: "Zero lock(stateLock) occurrences",
    tool: 'grep -r "lock(stateLock)" src/',
    status: "needs_platform_adjustment",
    statusLabel: "Needs Platform Adjustment",
    flag: "Uses Unix grep — not natively available in Windows PowerShell",
    psEquivalent:
      'Get-ChildItem -Path .\\src -Recurse -Filter *.cs | Select-String -Pattern "lock\\(stateLock\\)"',
  },
  {
    check: "Zero non-ASCII characters in strings",
    tool: "python check_ascii.py",
    status: "runnable",
    statusLabel: "Runnable",
    flag: undefined,
    psEquivalent: undefined,
  },
  {
    check: "All state mutations inside Enqueue",
    tool: "Manual audit per migrated block",
    status: "runnable",
    statusLabel: "Runnable",
    flag: undefined,
    psEquivalent: undefined,
  },
  {
    check: "OnStateChange body is one-liner only",
    tool: "Visual inspection of V12_002.cs",
    status: "runnable",
    statusLabel: "Runnable",
    flag: undefined,
    psEquivalent: undefined,
  },
  {
    check: "OnBarUpdate body is one-liner only",
    tool: "Visual inspection of V12_002.cs",
    status: "runnable",
    statusLabel: "Runnable",
    flag: undefined,
    psEquivalent: undefined,
  },
  {
    check: "No new abstractions beyond plan spec",
    tool: "Karpathy: simplicity-first review",
    status: "runnable",
    statusLabel: "Runnable",
    flag: undefined,
    psEquivalent: undefined,
  },
  {
    check: "BUILD_TAG incremented",
    tool: "V12_002.Properties.cs build tag field",
    status: "runnable",
    statusLabel: "Runnable",
    flag: undefined,
    psEquivalent: undefined,
  },
  {
    check: "deploy-sync.ps1 executed after edits",
    tool: "Post-edit deployment protocol (CLAUDE.md)",
    status: "dependency_missing",
    statusLabel: "Dependency Missing",
    flag: "CLAUDE.md protocol is referenced but not reproduced in this plan document — engineer must locate it independently",
    psEquivalent: undefined,
  },
];

// Section 8 of the deployment protocol referenced in the plan (step 19 of Handoff Block):
// After edits and deploy-sync.ps1:
//   Director must press F5 in NinjaTrader UI to compile.
//   Then confirm the banner displays the new BUILD_TAG.
// Evidence: Execution Order steps 19 & 20 in the Handoff Block / step 12 & 13 in Section 7.

/* ── Adversarial Summary ──────────────────────────────────────────────────── */
const findings: Finding[] = [
  // BLOCKING
  {
    id: "B1",
    severity: "blocking",
    section: "Sec 3.5 / Handoff Step 12",
    title: "Handler method name assumption in DispatchOnOrderUpdate",
    detail:
      "The plan's Section 3.5 scaffold hard-codes calls to HandleOrderUpdate() and HandleExecutionUpdate(), but the CONSTRAINT FOR ENGINEER explicitly states these names must be verified against the current file and matched exactly. If the actual names differ, the scaffold will not compile. This is a blocking signature/naming risk — the plan cannot be executed blind.",
  },
  {
    id: "B2",
    severity: "blocking",
    section: "Sec 9.1 / Sec 4.2",
    title: "Terminated-exempt Enqueue rule not formalized in ADR-020 body",
    detail:
      "Section 9.1 (Open Questions) raises the Terminated teardown Enqueue exception as an open question. The Handoff Block resolves it by declaring 'direct-call teardown; document as Terminated-exempt per ADR-020 Section 9.1.' However, ADR-020 in Section 8 does not yet contain this exception text. The formal ADR record is incomplete — a thread-affinity violation could occur if an engineer misreads Section 4.2 (which mandates Enqueue for all mutations) without seeing the Section 9.1 carve-out.",
  },
  // MODERATE
  {
    id: "M1",
    severity: "moderate",
    section: "Sec 3.4 / Sec 9.2",
    title: "BarUpdate_TickReaper() lacks IsFirstTickOfBar guard in scaffold",
    detail:
      "Section 9.2 recommends adding 'if (IsFirstTickOfBar)' inside TickReaper() in V12_002.REAPER.cs as a defensive measure if Calculate is ever changed to OnEachTick. The Section 3.4 scaffold does not include this guard in BarUpdate_TickReaper() or in any stub. It is recommended but not enforced — a plan update should add this as a required checklist item, not merely advisory.",
  },
  {
    id: "M2",
    severity: "moderate",
    section: "Sec 6 / Sec 7 Step 8",
    title: "lock(stateLock) grep step assumes Unix grep on Windows target",
    detail:
      "Section 6 specifies 'grep -r \"lock(stateLock)\" src/' and Section 7 Step 8 repeats this. The deployment target is Windows PowerShell (deploy-sync.ps1). grep is not natively available. The PowerShell equivalent is: Get-ChildItem -Path .\\src -Recurse -Filter *.cs | Select-String -Pattern \"lock\\(stateLock\\)\". This is a moderate issue because the CI verification step will silently fail on a plain Windows environment.",
  },
  {
    id: "M3",
    severity: "moderate",
    section: "Sec 4.2 / Sec 8",
    title: "Build 981 stopOrders direct-write exemption not enumerated",
    detail:
      "Section 4.2 mentions the 'Build 981 exemption (stopOrders direct-write during bracket submission)' but does not enumerate which specific fields or call sites are covered. An engineer cannot perform a reliable Enqueue audit without knowing the exact field names covered by this exemption. The plan should enumerate them.",
  },
  // ADVISORY
  {
    id: "A1",
    severity: "advisory",
    section: "Sec 6",
    title: "grep commands in Sec 6 and Sec 7 need PowerShell equivalents",
    detail:
      "Two distinct grep commands appear: (1) 'grep -r \"lock(stateLock)\" src/' — replace with Get-ChildItem | Select-String as shown. (2) Implicitly, check_ascii.py covers the ASCII scan, so that check is fine. No other grep calls are present in Section 6, but the pattern should be flagged for consistency.",
  },
  {
    id: "A2",
    severity: "advisory",
    section: "Sec 3.3 / 3.4 comments",
    title: "Comments use ASCII-only per plan mandate — verify em-dash absence",
    detail:
      "The plan mandates ASCII-only strings and forbids em-dashes, curly quotes, and Unicode in string literals. The scaffold comments use '--' (double-hyphen) as section separators, which is correct. The engineer should confirm no editor auto-corrects '--' to an em-dash on paste.",
  },
  {
    id: "A3",
    severity: "advisory",
    section: "Sec 9.3",
    title: "_activeEntryStrategy null-conditional — silent suppression",
    detail:
      "Section 9.3 accepts silent null-conditional '?.OnBarUpdate()' for pre-session historical bars. This is architecturally acceptable but means there is no observable signal when the factory resolver returns null during realtime. A debug-mode log (Print only when State == Realtime) would improve observability without changing the accepted behavior.",
  },
];

// ─────────────────────────────────────────────────────────────────────────────
// UI helpers
// ─────────────────────────────────────────────────────────────────────────────


const statusConfig: Record<CheckRow["status"], { label: string; cls: string }> = {
  runnable: { label: "✔ RUNNABLE", cls: "text-emerald-400 bg-emerald-950 border-emerald-700" },
  needs_platform_adjustment: {
    label: "⚠ NEEDS PLATFORM ADJUSTMENT",
    cls: "text-amber-300 bg-amber-950 border-amber-700",
  },
  dependency_missing: { label: "✖ DEPENDENCY MISSING", cls: "text-red-400 bg-red-950 border-red-700" },
};

function SectionHeader({ num, title }: { num: string; title: string }) {
  return (
    <div className="flex items-center gap-3 mb-6">
      <div className="flex-shrink-0 w-10 h-10 rounded-lg bg-indigo-600 flex items-center justify-center text-white font-bold text-sm">
        {num}
      </div>
      <h2 className="text-xl font-bold text-white tracking-wide uppercase">{title}</h2>
    </div>
  );
}

function Badge({ label, cls }: { label: string; cls: string }) {
  return (
    <span className={`inline-block px-2 py-0.5 rounded text-xs font-bold border ${cls}`}>{label}</span>
  );
}

// ─────────────────────────────────────────────────────────────────────────────
// Section 1 — Lifecycle Dispatcher Inventory
// ─────────────────────────────────────────────────────────────────────────────
function Section1() {
  const [open, setOpen] = useState<number | null>(0);
  return (
    <section className="mb-10">
      <SectionHeader num="1" title="Lifecycle Dispatcher Inventory (Section 3)" />
      <p className="text-slate-400 text-sm mb-5">
        Source: Sections 3.2 (dispatcher contracts), 3.5 (Callbacks additions), 4.2 (Enqueue rule), 9.1 (Terminated exception).
      </p>
      <div className="space-y-3">
        {lifecycleOverrides.map((ov, i) => (
          <div
            key={i}
            className="rounded-xl border border-slate-700 bg-slate-900 overflow-hidden"
          >
            {/* Header row */}
            <button
              className="w-full flex items-start justify-between gap-4 p-4 text-left hover:bg-slate-800 transition-colors"
              onClick={() => setOpen(open === i ? null : i)}
            >
              <div className="flex flex-col gap-1 min-w-0">
                <div className="flex flex-wrap items-center gap-2">
                  <span className="font-mono text-indigo-400 font-bold text-sm">{ov.ntOverride}</span>
                  <span className="text-slate-500 text-xs">→</span>
                  <span className="font-mono text-emerald-400 text-xs">{ov.dispatcherMethod}</span>
                </div>
                <span className="text-slate-400 text-xs">{ov.targetPartialFile}</span>
              </div>
              <div className="flex-shrink-0 flex gap-2 items-center mt-1">
                <Badge
                  label={ov.signatureMatch ? "✔ SIGNATURE OK" : "✖ SIGNATURE MISMATCH"}
                  cls={ov.signatureMatch ? "text-emerald-400 bg-emerald-950 border-emerald-700" : "text-red-400 bg-red-950 border-red-700"}
                />
                <span className="text-slate-500 text-lg">{open === i ? "▲" : "▼"}</span>
              </div>
            </button>

            {open === i && (
              <div className="border-t border-slate-700 p-4 space-y-4 bg-slate-950 text-sm">
                {/* Signature */}
                <div>
                  <div className="text-slate-500 text-xs uppercase font-bold mb-1 tracking-wider">NT8 Override Parameter Signature</div>
                  <code className="block bg-slate-800 rounded p-2 text-xs font-mono text-slate-200 whitespace-pre-wrap break-all">
                    {ov.ntOverride.replace("()", "")}({ov.paramSignature})
                  </code>
                </div>
                {/* Invariant check */}
                <div>
                  <div className="text-slate-500 text-xs uppercase font-bold mb-1 tracking-wider">INVARIANT CHECK — Exact Signature Required by NT8 API</div>
                  <p className="text-slate-300 leading-relaxed">{ov.signatureNote}</p>
                </div>
                {/* Thread safety */}
                <div>
                  <div className="text-slate-500 text-xs uppercase font-bold mb-1 tracking-wider">THREAD SAFETY — Enqueue Compliance Rule</div>
                  <p className="text-slate-300 leading-relaxed">{ov.enqueueRule}</p>
                </div>
                {/* Terminated exemption */}
                <div className="rounded-lg border border-amber-800 bg-amber-950/40 p-3">
                  <div className="text-amber-400 text-xs uppercase font-bold mb-1 tracking-wider">
                    Section 9.1 — Terminated-Phase Enqueue Exemption
                  </div>
                  <p className="text-amber-200 text-xs leading-relaxed">{ov.terminatedExemption}</p>
                </div>
              </div>
            )}
          </div>
        ))}
      </div>

      {/* Enqueue exemption callout */}
      <div className="mt-5 rounded-xl border border-indigo-700 bg-indigo-950/40 p-4">
        <div className="text-indigo-300 font-bold text-sm mb-2">
          Which lifecycle phase is exempt from Enqueue wrapping, and why?
        </div>
        <p className="text-indigo-200 text-sm leading-relaxed">
          <strong>HandleState_Terminated()</strong> — Section 9.1 explicitly states: "NT8 may not process queued work after Terminated is called." Therefore teardown handlers (DeactivateIpcListener, DeactivateSimaMonitor, DeactivateAccountUpdateListener, FlushPendingEnqueuedWork) execute directly on the NT8 thread. This is documented as a named exception: <code className="bg-indigo-900 px-1 rounded text-xs">"Terminated-exempt per ADR-020 Section 9.1"</code>. All other lifecycle phases must route mutable state writes through <code className="bg-indigo-900 px-1 rounded text-xs">Enqueue(ctx =&gt; ...)</code>.
        </p>
      </div>
    </section>
  );
}

// ─────────────────────────────────────────────────────────────────────────────
// Section 2 — Pipeline Logic Consistency
// ─────────────────────────────────────────────────────────────────────────────
function Section2() {
  return (
    <section className="mb-10">
      <SectionHeader num="2" title="Pipeline Logic Consistency (Section 3.4)" />
      <p className="text-slate-400 text-sm mb-5">
        Source: Section 3.4 scaffold — DispatchOnBarUpdate() in V12_002.Lifecycle.BarUpdate.cs.
      </p>

      {/* Guard stages */}
      <div className="mb-6">
        <h3 className="text-emerald-400 font-bold uppercase text-xs tracking-widest mb-3">
          Guard Stages (3) — return bool; false short-circuits pipeline
        </h3>
        <div className="grid md:grid-cols-3 gap-3">
          {guardStages.map((g) => (
            <div
              key={g.order}
              className="rounded-xl border border-emerald-800 bg-slate-900 p-4"
            >
              <div className="flex items-center gap-2 mb-2">
                <span className="w-6 h-6 rounded-full bg-emerald-700 text-white flex items-center justify-center text-xs font-bold flex-shrink-0">
                  {g.order}
                </span>
                <code className="text-emerald-300 text-xs font-mono font-bold break-all">
                  {g.name}
                </code>
              </div>
              <Badge
                label={`Returns: ${g.returnType}`}
                cls="text-emerald-400 bg-emerald-950 border-emerald-800 mb-2"
              />
              <p className="text-slate-400 text-xs leading-relaxed mt-2">{g.description}</p>
            </div>
          ))}
        </div>
      </div>

      {/* Processing stages */}
      <div className="mb-6">
        <h3 className="text-indigo-400 font-bold uppercase text-xs tracking-widest mb-3">
          Processing Stages (6) — void; fire sequentially after all guards pass
        </h3>
        <div className="space-y-2">
          {processingStages.map((s) => (
            <div
              key={s.order}
              className="flex gap-3 rounded-lg border border-slate-700 bg-slate-900 p-3"
            >
              <span className="flex-shrink-0 w-7 h-7 rounded bg-indigo-700 text-white flex items-center justify-center text-xs font-bold">
                {s.order}
              </span>
              <div className="min-w-0">
                <div className="flex flex-wrap items-center gap-2 mb-1">
                  <code className="text-indigo-300 text-xs font-mono font-bold">{s.name}</code>
                  <Badge label={`Returns: ${s.returnType}`} cls="text-slate-400 bg-slate-800 border-slate-600" />
                </div>
                <p className="text-slate-400 text-xs leading-relaxed">{s.description}</p>
              </div>
            </div>
          ))}
        </div>
      </div>

      {/* Critical flow */}
      <div className="rounded-xl border border-red-700 bg-red-950/30 p-5">
        <div className="flex items-center gap-2 mb-3">
          <span className="text-red-400 text-lg">⚠</span>
          <h3 className="text-red-300 font-bold uppercase text-sm tracking-wider">
            CRITICAL FLOW: BarUpdate_GuardTradingSession() returns false
          </h3>
        </div>
        <p className="text-red-200 text-sm mb-3 leading-relaxed">
          When Guard 2 (<code className="bg-red-900/50 px-1 rounded">BarUpdate_GuardTradingSession()</code>) returns{" "}
          <code className="bg-red-900/50 px-1 rounded">false</code>, the dispatcher executes{" "}
          <code className="bg-red-900/50 px-1 rounded">return</code> immediately on scaffold line 2 of{" "}
          <code className="bg-red-900/50 px-1 rounded">DispatchOnBarUpdate()</code>. All subsequent stages are skipped:
        </p>

        {/* Scaffold code excerpt */}
        <div className="bg-slate-950 rounded-lg p-3 mb-4 font-mono text-xs border border-red-900">
          <div className="text-slate-500 mb-1">// Section 3.4 — DispatchOnBarUpdate() scaffold (exact line sequence)</div>
          <div className="text-slate-300">{"private void DispatchOnBarUpdate() {"}</div>
          <div className="pl-4 text-slate-300">{"if (!BarUpdate_GuardDataValidity()) return;  "}<span className="text-slate-500">// scaffold line 1</span></div>
          <div className="pl-4 text-red-400 font-bold">{"if (!BarUpdate_GuardTradingSession()) return; "}<span className="text-red-600">// scaffold line 2 — FALSE exits here ↑</span></div>
          <div className="pl-4 text-slate-500">{"if (!BarUpdate_GuardFlatReset()) return;      // SKIPPED"}</div>
          <div className="pl-4 text-slate-500">{"BarUpdate_ComputeIndicators();                // SKIPPED"}</div>
          <div className="pl-4 text-slate-500">{"BarUpdate_EvaluateOpeningRange();             // SKIPPED"}</div>
          <div className="pl-4 text-slate-500">{"BarUpdate_DispatchEntries();                  // SKIPPED"}</div>
          <div className="pl-4 text-slate-500">{"BarUpdate_EvaluateTrailing();                 // SKIPPED"}</div>
          <div className="pl-4 text-slate-500">{"BarUpdate_TickReaper();                       // SKIPPED"}</div>
          <div className="pl-4 text-slate-500">{"BarUpdate_RefreshUI();                        // SKIPPED"}</div>
          <div className="text-slate-300">{"}"}</div>
        </div>
        <div className="text-red-200 text-xs">
          <strong>7 stages skipped:</strong>{" "}
          {skippedBySessionGuard.map((s, i) => (
            <span key={i} className="inline-block mr-2 mb-1 px-2 py-0.5 rounded bg-red-900/50 border border-red-800 font-mono">
              {s}
            </span>
          ))}
        </div>
        <div className="mt-3 text-slate-400 text-xs">
          <strong>Citation:</strong> Section 3.4, DispatchOnBarUpdate() scaffold body — the pipeline is an explicit{" "}
          <code className="bg-slate-800 px-1 rounded">if (!guard) return;</code> chain; each false return
          exits before reaching any subsequent method call.
        </div>
      </div>
    </section>
  );
}

// ─────────────────────────────────────────────────────────────────────────────
// Section 3 — Verification Matrix Audit
// ─────────────────────────────────────────────────────────────────────────────
function Section3() {
  return (
    <section className="mb-10">
      <SectionHeader num="3" title="Verification Matrix Audit (Section 6)" />
      <p className="text-slate-400 text-sm mb-5">
        Target: Windows PowerShell. Source: Section 6 (Constraint Compliance Checklist), Section 7 Steps 7–13, Handoff Block Steps 14–20.
      </p>

      <div className="overflow-x-auto rounded-xl border border-slate-700">
        <table className="w-full text-sm">
          <thead>
            <tr className="border-b border-slate-700 bg-slate-800">
              <th className="text-left p-3 text-slate-300 font-bold text-xs uppercase tracking-wider">Check</th>
              <th className="text-left p-3 text-slate-300 font-bold text-xs uppercase tracking-wider">Tool / Method</th>
              <th className="text-left p-3 text-slate-300 font-bold text-xs uppercase tracking-wider w-52">Status (PowerShell Target)</th>
            </tr>
          </thead>
          <tbody>
            {verificationChecks.map((row, i) => {
              const sc = statusConfig[row.status];
              return (
                <React.Fragment key={i}>
                  <tr className={`border-b border-slate-800 ${i % 2 === 0 ? "bg-slate-900" : "bg-slate-950"}`}>
                    <td className="p-3 text-slate-200 align-top">{row.check}</td>
                    <td className="p-3 align-top">
                      <code className="text-xs font-mono text-slate-400 break-all">{row.tool}</code>
                    </td>
                    <td className="p-3 align-top">
                      <Badge label={sc.label} cls={sc.cls} />
                    </td>
                  </tr>
                  {(row.flag || row.psEquivalent) && (
                    <tr className={`border-b border-slate-800 ${i % 2 === 0 ? "bg-slate-900" : "bg-slate-950"}`}>
                      <td colSpan={3} className="px-4 pb-3 pt-0">
                        {row.flag && (
                          <div className="flex gap-2 items-start mb-2 mt-1">
                            <span className="text-amber-400 text-xs font-bold flex-shrink-0">FLAG:</span>
                            <span className="text-amber-200 text-xs leading-relaxed">{row.flag}</span>
                          </div>
                        )}
                        {row.psEquivalent && (
                          <div className="mt-1">
                            <span className="text-blue-400 text-xs font-bold block mb-1">PowerShell Native Equivalent:</span>
                            <code className="block bg-slate-800 rounded p-2 text-xs font-mono text-blue-300 break-all">
                              {row.psEquivalent}
                            </code>
                          </div>
                        )}
                      </td>
                    </tr>
                  )}
                </React.Fragment>
              );
            })}
          </tbody>
        </table>
      </div>

      {/* Section 8 — Post-edit Director sequence */}
      <div className="mt-6 rounded-xl border border-indigo-700 bg-indigo-950/30 p-5">
        <h3 className="text-indigo-300 font-bold uppercase text-sm tracking-wider mb-3">
          Post-Edit Sequence — What the Director Must Do in NT8 UI
        </h3>
        <p className="text-slate-400 text-xs mb-3">
          Source: Execution Order Steps 19–20 (Handoff Block) and Section 7 Steps 12–13. These correspond to the deployment protocol referenced as CLAUDE.md.
        </p>
        <ol className="space-y-2">
          {[
            {
              step: "1",
              text: "Ensure deploy-sync.ps1 has completed with ASCII Gate PASS (Handoff Step 18 / Section 7 Step 11).",
              color: "text-indigo-300",
            },
            {
              step: "2",
              text: 'Press F5 in NinjaTrader to compile. (Handoff Step 19: "Instruct Director: press F5 in NinjaTrader to compile" / Section 7 Step 12: "Instruct Director: press F5 in NinjaTrader to compile")',
              color: "text-indigo-300",
            },
            {
              step: "3",
              text: 'Verify the strategy banner displays the new BUILD_TAG (e.g., "Build 982"). (Handoff Step 20: "Confirm banner displays new BUILD_TAG" / Section 7 Step 13: "Verify banner shows new BUILD_TAG")',
              color: "text-indigo-300",
            },
          ].map((item) => (
            <li key={item.step} className="flex gap-3 items-start">
              <span className="w-6 h-6 rounded-full bg-indigo-700 text-white text-xs font-bold flex items-center justify-center flex-shrink-0">
                {item.step}
              </span>
              <p className={`${item.color} text-sm leading-relaxed`}>{item.text}</p>
            </li>
          ))}
        </ol>
      </div>
    </section>
  );
}

// ─────────────────────────────────────────────────────────────────────────────
// Section 4 — Adversarial Summary
// ─────────────────────────────────────────────────────────────────────────────
function Section4() {
  const blocking = findings.filter((f) => f.severity === "blocking");
  const moderate = findings.filter((f) => f.severity === "moderate");
  const advisory = findings.filter((f) => f.severity === "advisory");

  const verdict = blocking.length > 0 || moderate.length > 0
    ? "needs_plan_update"
    : "ready";

  return (
    <section className="mb-10">
      <SectionHeader num="4" title="Adversarial Summary" />

      {/* Verdict banner */}
      <div
        className={`rounded-xl border p-5 mb-6 flex items-center gap-4 ${
          verdict === "ready"
            ? "border-emerald-600 bg-emerald-950/40"
            : "border-red-700 bg-red-950/30"
        }`}
      >
        <span className="text-3xl">{verdict === "ready" ? "✅" : "🔴"}</span>
        <div>
          <div
            className={`font-bold text-lg ${verdict === "ready" ? "text-emerald-300" : "text-red-300"}`}
          >
            {verdict === "ready"
              ? "READY FOR ENGINEERING STEP"
              : "NEEDS PLAN UPDATE BEFORE ENGINEERING STEP"}
          </div>
          <div className="text-slate-400 text-sm mt-1">
            {blocking.length} blocking · {moderate.length} moderate · {advisory.length} advisory
          </div>
        </div>
      </div>

      {/* Blocking */}
      <div className="mb-5">
        <h3 className="text-red-400 font-bold uppercase text-xs tracking-widest mb-3 flex items-center gap-2">
          <span className="w-3 h-3 rounded-full bg-red-500 inline-block"></span>
          BLOCKING — Signature Mismatches or Thread-Affinity Violations
        </h3>
        <div className="space-y-3">
          {blocking.map((f) => (
            <div
              key={f.id}
              className="rounded-xl border border-red-700 bg-red-950/20 p-4"
            >
              <div className="flex flex-wrap items-center gap-2 mb-2">
                <Badge label={f.id} cls="text-red-300 bg-red-900 border-red-700 font-mono" />
                <Badge label="BLOCKING" cls="text-red-300 bg-red-950 border-red-700" />
                <span className="text-slate-500 text-xs">{f.section}</span>
                <span className="text-red-200 font-semibold text-sm">{f.title}</span>
              </div>
              <p className="text-slate-300 text-sm leading-relaxed">{f.detail}</p>
            </div>
          ))}
        </div>
      </div>

      {/* Moderate */}
      <div className="mb-5">
        <h3 className="text-amber-400 font-bold uppercase text-xs tracking-widest mb-3 flex items-center gap-2">
          <span className="w-3 h-3 rounded-full bg-amber-500 inline-block"></span>
          MODERATE — Missing Guards or Pipeline Ordering Issues
        </h3>
        <div className="space-y-3">
          {moderate.map((f) => (
            <div
              key={f.id}
              className="rounded-xl border border-amber-700 bg-amber-950/20 p-4"
            >
              <div className="flex flex-wrap items-center gap-2 mb-2">
                <Badge label={f.id} cls="text-amber-300 bg-amber-900 border-amber-700 font-mono" />
                <Badge label="MODERATE" cls="text-amber-300 bg-amber-950 border-amber-700" />
                <span className="text-slate-500 text-xs">{f.section}</span>
                <span className="text-amber-200 font-semibold text-sm">{f.title}</span>
              </div>
              <p className="text-slate-300 text-sm leading-relaxed">{f.detail}</p>
            </div>
          ))}
        </div>
      </div>

      {/* Advisory */}
      <div className="mb-5">
        <h3 className="text-blue-400 font-bold uppercase text-xs tracking-widest mb-3 flex items-center gap-2">
          <span className="w-3 h-3 rounded-full bg-blue-400 inline-block"></span>
          ADVISORY — PowerShell Command Alternatives, Comment Wording
        </h3>
        <div className="space-y-3">
          {advisory.map((f) => (
            <div
              key={f.id}
              className="rounded-xl border border-blue-700 bg-blue-950/20 p-4"
            >
              <div className="flex flex-wrap items-center gap-2 mb-2">
                <Badge label={f.id} cls="text-blue-300 bg-blue-900 border-blue-700 font-mono" />
                <Badge label="ADVISORY" cls="text-blue-300 bg-blue-950 border-blue-700" />
                <span className="text-slate-500 text-xs">{f.section}</span>
                <span className="text-blue-200 font-semibold text-sm">{f.title}</span>
              </div>
              <p className="text-slate-300 text-sm leading-relaxed">{f.detail}</p>
            </div>
          ))}
        </div>
      </div>

      {/* Exact items list */}
      <div className="rounded-xl border border-slate-600 bg-slate-900 p-5">
        <h3 className="text-white font-bold text-sm mb-3 uppercase tracking-wider">
          Exact Items Requiring Plan Update Before Engineering Starts
        </h3>
        <ol className="space-y-2 text-sm">
          {[...blocking, ...moderate].map((f, i) => (
            <li key={f.id} className="flex gap-3 items-start">
              <span
                className={`flex-shrink-0 w-5 h-5 rounded-full flex items-center justify-center text-xs font-bold text-white ${
                  f.severity === "blocking" ? "bg-red-600" : "bg-amber-600"
                }`}
              >
                {i + 1}
              </span>
              <div>
                <span
                  className={`font-bold ${f.severity === "blocking" ? "text-red-300" : "text-amber-300"}`}
                >
                  [{f.id}] {f.title}
                </span>
                <span className="text-slate-500 text-xs ml-2">({f.section})</span>
              </div>
            </li>
          ))}
        </ol>
      </div>
    </section>
  );
}

// ─────────────────────────────────────────────────────────────────────────────
// Top-level consistency proof
// ─────────────────────────────────────────────────────────────────────────────
function ConsistencyProof() {
  return (
    <div className="rounded-xl border border-purple-700 bg-purple-950/30 p-5 mb-8">
      <h3 className="text-purple-300 font-bold uppercase text-sm tracking-wider mb-4">
        Document Consistency Proof — Evidence of Reading
      </h3>
      <div className="grid md:grid-cols-3 gap-4 text-sm">
        {/* check_ascii.py */}
        <div className="rounded-lg border border-emerald-700 bg-emerald-950/30 p-3">
          <div className="text-emerald-400 font-bold text-xs uppercase tracking-wider mb-2">check_ascii.py</div>
          <Badge label="EXISTS AT REPO ROOT" cls="text-emerald-300 bg-emerald-950 border-emerald-700 mb-2" />
          <p className="text-emerald-200 text-xs leading-relaxed mt-2">
            Referenced 4× in the document:{" "}
            <strong>Section 4.1 Step 4</strong> ("Run ASCII scan (check_ascii.py) on the new file"),{" "}
            <strong>Section 6</strong> ("python check_ascii.py"),{" "}
            <strong>Section 7 Step 7</strong> and{" "}
            <strong>Handoff Block Step 14</strong> ("Run: python check_ascii.py -- must report ZERO violations").
            Its existence at repo root is confirmed by all usages calling it as{" "}
            <code className="bg-emerald-900/50 px-1 rounded">python check_ascii.py</code> (no path prefix).
          </p>
        </div>
        {/* byte_purge.py */}
        <div className="rounded-lg border border-red-700 bg-red-950/30 p-3">
          <div className="text-red-400 font-bold text-xs uppercase tracking-wider mb-2">byte_purge.py</div>
          <Badge label="NOT MENTIONED — DOES NOT EXIST (per document)" cls="text-red-300 bg-red-950 border-red-700 mb-2" />
          <p className="text-red-200 text-xs leading-relaxed mt-2">
            The string "byte_purge.py" appears <strong>zero times</strong> in the entire document. There is no reference to this file in any section, checklist, script listing, or step. Contrast: check_ascii.py is explicitly named and invoked. byte_purge.py has no documentary evidence of existence in this plan.
          </p>
        </div>
        {/* Build tag delta */}
        <div className="rounded-lg border border-indigo-700 bg-indigo-950/30 p-3">
          <div className="text-indigo-400 font-bold text-xs uppercase tracking-wider mb-2">Exact Build Tag Delta String</div>
          <div className="bg-slate-900 rounded p-2 font-mono text-indigo-200 text-sm mb-2 text-center font-bold border border-indigo-800">
            Build 981 → Build 982
          </div>
          <p className="text-indigo-200 text-xs leading-relaxed">
            Quoted verbatim from <strong>Section 10</strong> document header: <em>"Increment BUILD_TAG in V12_002.Properties.cs to the next sequential value (e.g., if current is Build 981, set to Build 982)."</em> The delta string as written in the document: <code className="bg-indigo-900/50 px-1 rounded text-xs">Build 981, set to Build 982</code>.
          </p>
        </div>
      </div>
    </div>
  );
}

// ─────────────────────────────────────────────────────────────────────────────
// App
// ─────────────────────────────────────────────────────────────────────────────
export default function App() {
  return (
    <div className="min-h-screen bg-slate-950 text-slate-200">
      {/* Top bar */}
      <div className="sticky top-0 z-50 bg-slate-900/95 backdrop-blur border-b border-slate-700 shadow-lg">
        <div className="max-w-7xl mx-auto px-4 py-3 flex items-center justify-between gap-4 flex-wrap">
          <div className="flex items-center gap-3">
            <span className="text-indigo-400 text-2xl">⚙</span>
            <div>
              <h2 className="text-white font-bold text-base leading-tight">{MODEL}</h2>
              <div className="text-slate-400 text-xs">Senior Architect Review · ADR-020 Phase 4 · feature/phase-4-event-lifecycle</div>
            </div>
          </div>
          <div className="flex gap-2 flex-wrap">
            <Badge label="SOURCE: LOADED" cls="text-emerald-400 bg-emerald-950 border-emerald-700" />
            <Badge label="ADR-020 APPROVED" cls="text-indigo-300 bg-indigo-950 border-indigo-700" />
            <Badge
              label="VERDICT: NEEDS PLAN UPDATE"
              cls="text-red-300 bg-red-950 border-red-700"
            />
          </div>
        </div>
      </div>

      <div className="max-w-7xl mx-auto px-4 py-8">
        {/* Hero */}
        <div className="mb-8 text-center">
          <div className="inline-block px-3 py-1 rounded-full bg-indigo-900/50 border border-indigo-700 text-indigo-300 text-xs font-bold mb-3 tracking-widest uppercase">
            Technical Design Review Dashboard
          </div>
          <h1 className="text-3xl md:text-4xl font-black text-white mb-2 tracking-tight">
            Phase 4 — Event Lifecycle Refactoring
          </h1>
          <p className="text-slate-400 text-sm max-w-2xl mx-auto">
            Internal consistency audit of{" "}
            <code className="bg-slate-800 px-1 rounded text-slate-300">docs/brain/implementation_plan.md</code>{" "}
            · Branch: <code className="bg-slate-800 px-1 rounded text-slate-300">feature/phase-4-event-lifecycle</code>{" "}
            · All findings cited to section and line. No extrapolation beyond the document.
          </p>
        </div>

        {/* Consistency proof */}
        <ConsistencyProof />

        {/* 4 dashboard sections */}
        <Section1 />
        <Section2 />
        <Section3 />
        <Section4 />

        {/* Footer */}
        <div className="border-t border-slate-800 pt-6 mt-2 text-center text-slate-600 text-xs space-y-1">
          <div>
            Reviewing model: <strong className="text-slate-400">{MODEL}</strong>
          </div>
          <div>
            Document: ADR-020 · Phase 4 Event Lifecycle Refactoring ·{" "}
            <code>docs/brain/implementation_plan.md</code>
          </div>
          <div>P3 Architect sign-off in document: Claude Sonnet 4.6 | 2026-05-01</div>
        </div>
      </div>
    </div>
  );
}
