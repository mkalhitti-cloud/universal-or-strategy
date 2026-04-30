import { useState } from "react";

// ─── Types ────────────────────────────────────────────────────────────────────

type Classification = "Type 1" | "Type 2" | "Unverifiable";
type Transform = "A" | "B";
type Severity = "blocking" | "moderate" | "advisory";

interface LambdaSite {
  site: number;
  file: string;
  line: number | string;
  method: string;
  transform: Transform;
  description: string;
  classification: Classification;
  classRationale: string;
  evidence: string;
  hasOldNew: boolean;
  oldCode?: string;
  newCode?: string;
  confirmVar?: string;
  needsPlanUpdate: boolean;
}

interface PathEntry {
  step: string;
  file: string;
  line: number | string;
  oldVal: string;
  newVal: string;
  status: "consistent" | "inconsistency";
  note?: string;
}

interface VerStep {
  step: number;
  description: string;
  runnable: boolean;
  needsPlatformAdj: boolean;
  depMissing: boolean;
  posixFlag: boolean;
  psEquivalent?: string;
  note: string;
}

// ─── Data: Section C.1 ────────────────────────────────────────────────────────

const lambdaSites: LambdaSite[] = [
  {
    site: 1,
    file: "Symmetry.cs",
    line: 115,
    method: "SymmetryGuardRegisterFollower",
    transform: "A",
    description: "HashSet.Add under ctx.Sync",
    classification: "Type 1",
    classRationale:
      "Pure-work lambda: adds a name to FollowerEntries. The plan's Step 5.2 (line 224 pattern) replaces the lock with ctx.AddFollower(name). No shared-resource operation follows the primary call inside the lambda body that would be bypassed by an early return.",
    evidence: "None — no TryRemove, Release, Clear, or post-primary shared-state write inside the lambda.",
    hasOldNew: false,
    needsPlanUpdate: false,
  },
  {
    site: 2,
    file: "Symmetry.cs",
    line: 151,
    method: "SymmetryGuardOnMasterFill",
    transform: "B",
    description: "Anchor RMW + IsResolved under ctx.Sync",
    classification: "Type 1",
    classRationale:
      "Transform B uses a CAS loop (TryPublishAnchor). The atomic publish builds a new AnchorSnapshot and swaps it in a single Interlocked.CompareExchange. All field writes are captured inside the immutable record; no post-primary state cleanup is bypassable. The HALLUCINATION CANARY explicitly excludes site 11 as the special case; this site is a standard CAS replacement.",
    evidence:
      "None — the CAS loop replaces the entire lock block atomically. No TryRemove/Release/Clear follows the primary swap in the lambda body.",
    hasOldNew: false,
    needsPlanUpdate: false,
  },
  {
    site: 3,
    file: "Symmetry.Follower.cs",
    line: 38,
    method: "SymmetryGuardTryPropagateMove",
    transform: "A",
    description: "Anchor read under ctx.Sync",
    classification: "Type 1",
    classRationale:
      "Read-only operation. The plan (Step 5.1, first block) replaces lock(ctx.Sync){ snapshot fields } with a single var snap = preCheckCtx.Anchor (Volatile.Read). No state mutation in the lambda body — purely extracting anchorReady and preCheckAnchor fields.",
    evidence: "None — read-only snapshot; no shared-resource write follows.",
    hasOldNew: true,
    oldCode:
      `lock (ctx.Sync)\n{\n    anchorReady = preCheckCtx.IsResolved;\n    preCheckAnchor = preCheckCtx.MasterAnchorPrice;\n}`,
    newCode:
      `// ADR-019: single Volatile.Read returns coherent immutable snapshot.\nAnchorSnapshot preSnap = preCheckCtx.Anchor;\nbool anchorReady = preSnap.IsResolved;\ndouble preCheckAnchor = preSnap.MasterAnchorPrice;`,
    confirmVar: "preCheckCtx.Anchor",
    needsPlanUpdate: false,
  },
  {
    site: 4,
    file: "Symmetry.Follower.cs",
    line: 131,
    method: "SymmetryGuardTryResolveFollower",
    transform: "A",
    description: "Anchor read under ctx.Sync",
    classification: "Type 1",
    classRationale:
      "Read-only anchor snapshot extraction. Step 5.1 (second block) replaces with AnchorSnapshot snap = ctx.Anchor followed by field reads. No mutations follow inside the lambda.",
    evidence: "None — pure read of IsResolved and MasterAnchorPrice.",
    hasOldNew: true,
    oldCode:
      `lock (ctx.Sync)\n{\n    isResolved = ctx.IsResolved;\n    masterAnchor = ctx.MasterAnchorPrice;\n}`,
    newCode:
      `// ADR-019: snapshot dispatch state via single Volatile.Read; no ctx.Sync.\nAnchorSnapshot snap = ctx.Anchor;\nbool isResolved = snap.IsResolved;\ndouble masterAnchor = snap.MasterAnchorPrice;`,
    confirmVar: "ctx.Anchor",
    needsPlanUpdate: false,
  },
  {
    site: 5,
    file: "Symmetry.Replace.cs",
    line: 127,
    method: "SymmetryGuardTryResolveFollowersForDispatch",
    transform: "A",
    description: "Follower iteration under ctx.Sync",
    classification: "Type 1",
    classRationale:
      "Read iteration over FollowerEntries for dispatch resolution. Step 5.2 (line 127) replaces lock + foreach with string[] snap = ctx.Followers; foreach(string fleetEntryName in snap). No post-iteration shared-state mutation is inside the locked block itself.",
    evidence: "None — no TryRemove or other cleanup inside this particular lock scope per the plan description.",
    hasOldNew: true,
    oldCode:
      `lock (ctx.Sync)\n{\n    foreach (string fleetEntryName in ctx.FollowerEntries)\n    { /* dispatch logic */ }\n}`,
    newCode:
      `string[] snap = ctx.Followers;\nforeach (string fleetEntryName in snap)\n{ /* dispatch logic unchanged */ }`,
    confirmVar: "ctx.Followers",
    needsPlanUpdate: false,
  },
  {
    site: 6,
    file: "Symmetry.Replace.cs",
    line: 189,
    method: "SymmetryGuardCascadeFollowerCleanup",
    transform: "A",
    description: "Follower snapshot under ctx.Sync",
    classification: "Type 1",
    classRationale:
      "Snapshot capture: lock(ctx.Sync){ followers = ctx.FollowerEntries.ToArray(); }. Step 5.2 (line 189) replaces with string[] followers = ctx.Followers; — the immutable array IS the snapshot. No cleanup operation inside the lock body itself per the plan.",
    evidence: "None — the lock solely takes the ToArray() snapshot; actual cleanup logic is outside the lock scope.",
    hasOldNew: true,
    oldCode:
      `string[] followers;\nlock (ctx.Sync)\n{\n    followers = ctx.FollowerEntries.ToArray();\n}`,
    newCode:
      `string[] followers = ctx.Followers;\n// ADR-019: immutable array is already a coherent snapshot; no ToArray() copy needed.`,
    confirmVar: "ctx.Followers",
    needsPlanUpdate: false,
  },
  {
    site: 7,
    file: "Symmetry.Replace.cs",
    line: 224,
    method: "SymmetryGuardForgetEntry",
    transform: "A",
    description: "Follower removal under ctx.Sync",
    classification: "Type 2",
    classRationale:
      "The lock body performs ctx.FollowerEntries.Remove(entryName) — a shared-state mutation (removal from the follower membership set). Step 5.2 (line 224) replaces with ctx.RemoveFollower(entryName), which internally runs a CAS loop. If an early return were inserted BEFORE the Remove, the follower would remain registered in a stale entry, causing subsequent dispatch scans to iterate a ghost entry. The CAS-based RemoveFollower call is the critical cleanup operation.",
    evidence:
      "ctx.FollowerEntries.Remove(entryName) — removal of a registered follower from the shared membership structure. Bypassing this leaves a stale/ghost entry in the _followers snapshot array.",
    hasOldNew: true,
    oldCode:
      `lock (ctx.Sync)\n    ctx.FollowerEntries.Remove(entryName);`,
    newCode:
      `ctx.RemoveFollower(entryName);\n// ADR-019: CAS loop ensures atomic removal from the immutable snapshot.`,
    confirmVar: "ctx.RemoveFollower",
    needsPlanUpdate: false,
  },
  {
    site: 8,
    file: "Symmetry.Replace.cs",
    line: 247,
    method: "SymmetryGuardPruneDispatches",
    transform: "A",
    description: "Follower iteration under ctx.Sync",
    classification: "Type 1",
    classRationale:
      "Read-only iteration checking activePositions.ContainsKey per follower. Step 5.2 (line 247) replaces with string[] snap = ctx.Followers; foreach(string follower in snap) — inner ContainsKey is already lock-free on ConcurrentDictionary. No shared-resource removal inside this lock block per the plan.",
    evidence: "None — activePositions.ContainsKey is a read; no TryRemove or state write inside the locked loop body described here.",
    hasOldNew: true,
    oldCode:
      `lock (ctx.Sync)\n{\n    foreach (string follower in ctx.FollowerEntries)\n    {\n        exists = activePositions.ContainsKey(follower);\n        // ...\n    }\n}`,
    newCode:
      `string[] snap = ctx.Followers;\nforeach (string follower in snap)\n{\n    exists = activePositions.ContainsKey(follower);\n    // ... (inner logic unchanged)\n}`,
    confirmVar: "ctx.Followers",
    needsPlanUpdate: false,
  },
  {
    site: 9,
    file: "Orders.Callbacks.Propagation.cs",
    line: 126,
    method: "PropagateMasterPriceMove",
    transform: "A",
    description: "HOT: .ToArray() under ctx.Sync",
    classification: "Type 1",
    classRationale:
      "Hot-path snapshot acquisition only. Step 3.1 replaces lock(ctx.Sync){ snapshot = ctx.FollowerEntries.ToArray(); } with followerEntryNames = ctx.Followers (zero-alloc Volatile.Read). No post-snapshot shared-state write inside the lock body.",
    evidence: "None — solely a ToArray() snapshot capture; the lock body contains no TryRemove, Release, or Clear.",
    hasOldNew: true,
    oldCode:
      `string[] snapshot;\nlock (ctx.Sync)\n{\n    snapshot = ctx.FollowerEntries.ToArray();\n}\nfollowerEntryNames = snapshot;`,
    newCode:
      `// ADR-019: ctx.Followers is an immutable snapshot via Interlocked.CompareExchange.\n// Zero-alloc, lock-free, point-in-time consistent. Hot path on every master price move.\nfollowerEntryNames = ctx.Followers;`,
    confirmVar: "ctx.Followers",
    needsPlanUpdate: false,
  },
  {
    site: 10,
    file: "Orders.Callbacks.AccountOrders.cs",
    line: 204,
    method: "TryGetDispatchFollowerEntries",
    transform: "A",
    description: ".ToArray() under ctx.Sync",
    classification: "Type 1",
    classRationale:
      "Snapshot-only: lock(ctx.Sync) followerEntries = ctx.FollowerEntries.ToArray(); Step 5.3 (line 204) replaces with followerEntries = ctx.Followers;. No mutation in the lock body.",
    evidence: "None — purely a ToArray() copy under the lock; no TryRemove or Clear follows inside the lock.",
    hasOldNew: true,
    oldCode:
      `lock (ctx.Sync)\n    followerEntries = ctx.FollowerEntries.ToArray();`,
    newCode:
      `followerEntries = ctx.Followers;\n// ADR-019: immutable string[] snapshot; Length > 0 check works unchanged.`,
    confirmVar: "ctx.Followers",
    needsPlanUpdate: false,
  },
  {
    site: 11,
    file: "Orders.Callbacks.AccountOrders.cs",
    line: 300,
    method: "HandleMatchedFollowerOrder",
    transform: "A",
    description: "fsm.State write under stateLock",
    classification: "Type 2",
    classRationale:
      "HALLUCINATION CANARY site. The document explicitly identifies this as 'the ONLY site where the lock is being removed entirely without a replacement CAS loop.' The lock body writes fsm.State = FollowerReplaceState.Submitting and reads fsm.PendingQty/PendingPrice — an FSM state mutation. If an early return were inserted BEFORE fsm.State = Submitting, the FSM would remain in a prior state (e.g. Pending), causing the actor pipeline to re-attempt submission — a ghost order risk. The plan's documented fix (Step 5.3) removes the lock entirely, justified by the actor pipeline guarantee. The fix IS documented.",
    evidence:
      "fsm.State = FollowerReplaceState.Submitting — FSM state write that would be bypassed. The plan justification: 'Single-threaded execution is guaranteed by the actor pipeline; the lock is dead weight inherited from the pre-actor era.' Fix is fully documented in Step 5.3.",
    hasOldNew: true,
    oldCode:
      `lock (stateLock)\n{\n    masterFilled = ...;\n    if (!masterFilled)\n    {\n        qty = fsm.PendingQty;\n        price = fsm.PendingPrice;\n        // ...\n        fsm.State = FollowerReplaceState.Submitting;\n    }\n}`,
    newCode:
      `// ADR-019: single-threaded by the actor pipeline (ProcessAccountOrderQueue is the\n// sole caller, dispatched via TriggerCustomEvent). The prior lock(stateLock) was\n// dead weight; no torn-state risk remains.\nmasterFilled = ...;\nif (!masterFilled)\n{\n    qty = fsm.PendingQty;\n    price = fsm.PendingPrice;\n    // ...\n    fsm.State = FollowerReplaceState.Submitting;\n}`,
    confirmVar: "fsm.State",
    needsPlanUpdate: false,
  },
  {
    site: 12,
    file: "SIMA.cs",
    line: 78,
    method: "AddExpectedPositionDeltaLocked",
    transform: "B",
    description: "expectedPositions mutation under stateLock",
    classification: "Type 2",
    classRationale:
      "The lock body mutates expectedPositions (ConcurrentDictionary) AND then calls StampAccountFillGrace(accountName) and Interlocked.Exchange(ref _lastExpectedPositionSetTicks, ...) AFTER the primary AddOrUpdate. If an early return were placed as the first statement, these post-primary stamping operations would be bypassed, leaving the grace window unstamped — a REAPER desync risk. The plan's Step 2.1 documents the fix, moving the post-AddOrUpdate operations outside any lock context.",
    evidence:
      "Interlocked.Exchange(ref _lastExpectedPositionSetTicks, DateTime.UtcNow.Ticks) and StampAccountFillGrace(accountName) — post-primary shared-state writes that would be bypassed. Fix IS documented in Step 2.1.",
    hasOldNew: true,
    oldCode:
      `lock (stateLock)\n{\n    int old = expectedPositions.TryGetValue(accountName, out var v) ? v : 0;\n    expectedPositions[accountName] = old + delta;\n    // ...\n    _lastExpectedPositionSetTicks = DateTime.UtcNow.Ticks;\n    if (capturedNew != 0) StampAccountFillGrace(accountName);\n}`,
    newCode:
      `// V12.1101E [F-06] / ADR-019: lock-free RMW via ConcurrentDictionary.AddOrUpdate.\nint capturedNew = expectedPositions.AddOrUpdate(\n    accountName,\n    addValueFactory: k => { capturedOld = 0; return delta; },\n    updateValueFactory: (k, v) => { capturedOld = v; return v + delta; });\nif (delta != 0)\n{\n    Interlocked.Exchange(ref _lastExpectedPositionSetTicks, DateTime.UtcNow.Ticks);\n    if (capturedNew != 0) StampAccountFillGrace(accountName);\n}`,
    confirmVar: "_lastExpectedPositionSetTicks",
    needsPlanUpdate: false,
  },
  {
    site: 13,
    file: "SIMA.cs",
    line: 100,
    method: "AddOrUpdateExpectedPositionLocked",
    transform: "B",
    description: "expectedPositions mutation under stateLock",
    classification: "Type 1",
    classRationale:
      "Pass-through wrapper: the plan (Step 2.2) shows the entire body becomes a single expectedPositions.AddOrUpdate(...) call with no post-primary operations. No stamping or cleanup follows inside the lock body at this site per the Step 2.2 code block — the method is a pure delegation to AddOrUpdate.",
    evidence:
      "None at this specific site — Step 2.2 new code has no post-AddOrUpdate side effects; it is a pure pass-through.",
    hasOldNew: true,
    oldCode:
      `lock (stateLock)\n{\n    expectedPositions.AddOrUpdate(accountName, addValue,\n        (k, v) => updateExisting(v));\n}`,
    newCode:
      `// V12.1101E [F-06] / ADR-019: pass-through to ConcurrentDictionary.AddOrUpdate.\nexpectedPositions.AddOrUpdate(accountName, addValue, (k, v) => updateExisting(v));`,
    confirmVar: "expectedPositions.AddOrUpdate",
    needsPlanUpdate: false,
  },
  {
    site: 14,
    file: "SIMA.cs",
    line: 111,
    method: "SetExpectedPositionLocked",
    transform: "B",
    description: "expectedPositions mutation under stateLock",
    classification: "Type 2",
    classRationale:
      "The plan's Step 2.3 new code shows that AFTER the primary AddOrUpdate call, two post-primary operations exist: (1) _dispatchSyncPendingExpKeys.TryRemove(accountName, out _) when value == 0, and (2) Interlocked.Exchange(ref _lastExpectedPositionSetTicks, ...) + StampAccountFillGrace when value != 0. If an early return were inserted as the first statement, these shared-state cleanup/stamping operations would be bypassed. Fix IS documented in Step 2.3.",
    evidence:
      "_dispatchSyncPendingExpKeys.TryRemove(accountName, out _) — dictionary key removal that would be bypassed. Also: Interlocked.Exchange(ref _lastExpectedPositionSetTicks, ...) and StampAccountFillGrace(accountName).",
    hasOldNew: true,
    oldCode:
      `lock (stateLock)\n{\n    expectedPositions.AddOrUpdate(accountName, value, (k, v) => value);\n    if (value == 0)\n        _dispatchSyncPendingExpKeys.TryRemove(accountName, out _);\n    // ...\n}`,
    newCode:
      `// V12.1101E [F-06] / ADR-019: lock-free unconditional set via AddOrUpdate.\nexpectedPositions.AddOrUpdate(accountName, value, (k, v) => value);\nif (value == 0)\n    _dispatchSyncPendingExpKeys.TryRemove(accountName, out _); // [B967-FIX-02]\nif (value != 0)\n{\n    Interlocked.Exchange(ref _lastExpectedPositionSetTicks, DateTime.UtcNow.Ticks);\n    StampAccountFillGrace(accountName);\n}`,
    confirmVar: "_dispatchSyncPendingExpKeys",
    needsPlanUpdate: false,
  },
  {
    site: 15,
    file: "SIMA.cs",
    line: 134,
    method: "DeltaExpectedPositionLocked",
    transform: "B",
    description: "expectedPositions mutation under stateLock",
    classification: "Type 2",
    classRationale:
      "Step 2.4 new code shows that after the primary AddOrUpdate, Interlocked.Exchange(ref _lastExpectedPositionSetTicks, DateTime.UtcNow.Ticks) fires when delta != 0. If an early return were the first statement, this timestamp stamp would be bypassed — leaving the REAPER grace window unsynchronised for that delta operation. Fix IS documented in Step 2.4.",
    evidence:
      "Interlocked.Exchange(ref _lastExpectedPositionSetTicks, DateTime.UtcNow.Ticks) — shared timestamp write that would be bypassed by an early return before the primary call.",
    hasOldNew: true,
    oldCode:
      `lock (stateLock)\n{\n    int current = expectedPositions.TryGetValue(accountName, out var v) ? v : 0;\n    expectedPositions[accountName] = current + delta;\n    _lastExpectedPositionSetTicks = DateTime.UtcNow.Ticks;\n}`,
    newCode:
      `// Build 930.1 [P1] / ADR-019: lock-free signed-delta rollback.\nint capturedUpdated = expectedPositions.AddOrUpdate(\n    accountName,\n    addValueFactory: k => { capturedCurrent = 0; return delta; },\n    updateValueFactory: (k, v) => { capturedCurrent = v; return v + delta; });\nif (delta != 0)\n    Interlocked.Exchange(ref _lastExpectedPositionSetTicks, DateTime.UtcNow.Ticks);`,
    confirmVar: "_lastExpectedPositionSetTicks",
    needsPlanUpdate: false,
  },
  {
    site: 16,
    file: "V12_002.cs",
    line: 146,
    method: "Field Declaration",
    transform: "A",
    description: "dailySummaryLock declaration",
    classification: "Type 1",
    classRationale:
      "Field declaration site — not a lambda/method body. The plan removes the dailySummaryLock field (Step 4.1) and replaces it with a _dailySummaryHeaderEnsured int field. This is a declaration, not an execution site; there is no lambda body that could have an early-return hazard here.",
    evidence: "None — field declaration; no executable shared-resource operation.",
    hasOldNew: true,
    oldCode:
      `private readonly object stateLock = new object();\nprivate readonly object dailySummaryLock = new object();`,
    newCode:
      `private readonly object stateLock = new object(); // ADR-019: stub; 22 partial files ref'd.\nprivate int _dailySummaryHeaderEnsured = 0;`,
    confirmVar: "_dailySummaryHeaderEnsured",
    needsPlanUpdate: false,
  },
  {
    site: 17,
    file: "UI.Compliance.cs",
    line: 122,
    method: "EnsureDailySummaryCsv",
    transform: "A",
    description: "lock(dailySummaryLock)",
    classification: "Type 2",
    classRationale:
      "The lock body guards CSV header creation: it writes a file header to dailySummaryCsvPath if the file does not exist. The post-primary I/O operation (File.WriteAllText) is the shared-resource action. If an early return were inserted BEFORE this write, the CSV file would be left without a header row — an incomplete-state scenario. The plan's Step 5.4 replaces with a one-shot Interlocked.CompareExchange CAS guard with rollback on failure. Fix IS documented.",
    evidence:
      "System.IO.File.WriteAllText(dailySummaryCsvPath, header + ...) — file write that would be bypassed. Also: Interlocked.Exchange(ref _dailySummaryHeaderEnsured, 0) rollback on I/O failure.",
    hasOldNew: true,
    oldCode:
      `lock (dailySummaryLock)\n{\n    if (!File.Exists(dailySummaryCsvPath))\n        File.WriteAllText(dailySummaryCsvPath, header + Environment.NewLine);\n}`,
    newCode:
      `// ADR-019: one-shot CAS guard replaces lock(dailySummaryLock).\nif (Interlocked.CompareExchange(ref _dailySummaryHeaderEnsured, 1, 0) != 0) return;\ntry\n{\n    if (!File.Exists(dailySummaryCsvPath))\n        File.WriteAllText(dailySummaryCsvPath, header + Environment.NewLine);\n}\ncatch { Interlocked.Exchange(ref _dailySummaryHeaderEnsured, 0); }`,
    confirmVar: "_dailySummaryHeaderEnsured",
    needsPlanUpdate: false,
  },
  {
    site: 18,
    file: "UI.Compliance.cs",
    line: 144,
    method: "AppendDailySummary",
    transform: "A",
    description: "lock(dailySummaryLock)",
    classification: "Type 2",
    classRationale:
      "The lock in AppendDailySummary wraps EnsureDailySummaryCsv() AND then fires a Task.Run(File.AppendAllText). The Task.Run captures pathCopy and lineCopy — if an early return bypassed the lock body, both the ensure call AND the async append would be skipped, leaving the daily summary unwritten. The plan's Step 5.4 refactors this method so EnsureDailySummaryCsv is called with its own self-guarding CAS, and AppendAllText runs in a separate Task.Run. Fix IS documented.",
    evidence:
      "Task.Run(() => { File.AppendAllText(pathCopy, lineCopy); }) — async file append that would be bypassed. EnsureDailySummaryCsv() call also bypassed.",
    hasOldNew: true,
    oldCode:
      `lock (dailySummaryLock)\n{\n    EnsureDailySummaryCsv();\n    File.AppendAllText(dailySummaryCsvPath, line + Environment.NewLine);\n}`,
    newCode:
      `// ADR-019: CSV header creation now self-guarded; no surrounding lock needed.\nEnsureDailySummaryCsv();\nstring pathCopy = dailySummaryCsvPath;\nstring lineCopy = line + Environment.NewLine;\nTask.Run(() =>\n{\n    try { File.AppendAllText(pathCopy, lineCopy); }\n    catch { /* swallow -- daily summary is best-effort */ }\n});`,
    confirmVar: "_dailySummaryHeaderEnsured",
    needsPlanUpdate: false,
  },
];

// ─── Data: Section D.4 ────────────────────────────────────────────────────────

const pathEntries: PathEntry[] = [
  {
    step: "Step 5.1 (Symmetry.Follower.cs line 38)",
    file: "V12_002.Symmetry.Follower.cs",
    line: 38,
    oldVal: "lock (ctx.Sync) { anchorReady = preCheckCtx.IsResolved; preCheckAnchor = preCheckCtx.MasterAnchorPrice; }",
    newVal: "AnchorSnapshot preSnap = preCheckCtx.Anchor; bool anchorReady = preSnap.IsResolved; double preCheckAnchor = preSnap.MasterAnchorPrice;",
    status: "consistent",
    note: "Member names IsResolved and MasterAnchorPrice match the AnchorSnapshot class defined in Step 1.1 exactly. preCheckCtx variable name preserved from surrounding context.",
  },
  {
    step: "Step 5.1 (Symmetry.Follower.cs line 131)",
    file: "V12_002.Symmetry.Follower.cs",
    line: 131,
    oldVal: "lock (ctx.Sync) { isResolved = ctx.IsResolved; masterAnchor = ctx.MasterAnchorPrice; }",
    newVal: "AnchorSnapshot snap = ctx.Anchor; bool isResolved = snap.IsResolved; double masterAnchor = snap.MasterAnchorPrice;",
    status: "consistent",
    note: "Variable names snap/isResolved/masterAnchor align with the AnchorSnapshot property names. No naming drift detected.",
  },
  {
    step: "Step 5.2 (Symmetry.Replace.cs line 127)",
    file: "V12_002.Symmetry.Replace.cs",
    line: 127,
    oldVal: "lock (ctx.Sync) { foreach (string fleetEntryName in ctx.FollowerEntries) { ... } }",
    newVal: "string[] snap = ctx.Followers; foreach (string fleetEntryName in snap) { ... }",
    status: "consistent",
    note: "ctx.Followers property name matches the SymmetryDispatchContext definition in Step 1.1. Loop variable fleetEntryName preserved.",
  },
  {
    step: "Step 5.2 (Symmetry.Replace.cs line 189)",
    file: "V12_002.Symmetry.Replace.cs",
    line: 189,
    oldVal: "string[] followers; lock (ctx.Sync) { followers = ctx.FollowerEntries.ToArray(); }",
    newVal: "string[] followers = ctx.Followers;",
    status: "consistent",
    note: "Direct assignment; ToArray() copy eliminated. followers variable name preserved for downstream use (followers.Length etc.).",
  },
  {
    step: "Step 5.2 (Symmetry.Replace.cs line 224)",
    file: "V12_002.Symmetry.Replace.cs",
    line: 224,
    oldVal: "lock (ctx.Sync) ctx.FollowerEntries.Remove(entryName);",
    newVal: "ctx.RemoveFollower(entryName);",
    status: "consistent",
    note: "RemoveFollower(string name) is defined in SymmetryDispatchContext (Step 1.1) with a CAS loop. entryName parameter preserved. This is the Type 2 site — the cleanup IS preserved, just via CAS rather than lock.",
  },
  {
    step: "Step 5.2 (Symmetry.Replace.cs line 247)",
    file: "V12_002.Symmetry.Replace.cs",
    line: 247,
    oldVal: "lock (ctx.Sync) { foreach (string follower in ctx.FollowerEntries) { exists = activePositions.ContainsKey(follower); ... } }",
    newVal: "string[] snap = ctx.Followers; foreach (string follower in snap) { exists = activePositions.ContainsKey(follower); ... }",
    status: "consistent",
    note: "Inner logic untouched. activePositions is ConcurrentDictionary; ContainsKey is lock-free. No inconsistency.",
  },
  {
    step: "Step 5.3 (AccountOrders.cs line 204)",
    file: "V12_002.Orders.Callbacks.AccountOrders.cs",
    line: 204,
    oldVal: "lock (ctx.Sync) followerEntries = ctx.FollowerEntries.ToArray();",
    newVal: "followerEntries = ctx.Followers;",
    status: "consistent",
    note: "ctx.Followers returns string[]. Downstream followerEntries.Length > 0 check works on string[] unchanged.",
  },
  {
    step: "Step 5.3 (AccountOrders.cs line 300)",
    file: "V12_002.Orders.Callbacks.AccountOrders.cs",
    line: 300,
    oldVal: "lock (stateLock) { masterFilled = ...; fsm.State = FollowerReplaceState.Submitting; }",
    newVal: "// No lock; actor pipeline guarantees single-threaded. masterFilled = ...; fsm.State = FollowerReplaceState.Submitting;",
    status: "consistent",
    note: "Lock removed entirely per HALLUCINATION CANARY guarantee. Logic preserved verbatim; no path substitution inconsistency.",
  },
  {
    step: "Step 5.4 (UI.Compliance.cs line 122)",
    file: "V12_002.UI.Compliance.cs",
    line: 122,
    oldVal: "lock (dailySummaryLock) { if (!File.Exists(...)) File.WriteAllText(...); }",
    newVal: "if (Interlocked.CompareExchange(ref _dailySummaryHeaderEnsured, 1, 0) != 0) return; try { ... } catch { Interlocked.Exchange(ref _dailySummaryHeaderEnsured, 0); }",
    status: "consistent",
    note: "_dailySummaryHeaderEnsured field defined in Step 4.1. Field type int matches Interlocked.CompareExchange(ref int, ...) signature.",
  },
  {
    step: "Step 5.4 (UI.Compliance.cs line 144)",
    file: "V12_002.UI.Compliance.cs",
    line: 144,
    oldVal: "lock (dailySummaryLock) { EnsureDailySummaryCsv(); File.AppendAllText(dailySummaryCsvPath, line + ...); }",
    newVal: "EnsureDailySummaryCsv(); string pathCopy = dailySummaryCsvPath; Task.Run(() => { File.AppendAllText(pathCopy, lineCopy); });",
    status: "consistent",
    note: "pathCopy and lineCopy capture the values before the async Task.Run — correct closure-capture pattern. EnsureDailySummaryCsv now self-guarded. No inconsistency.",
  },
  {
    step: "Step 4.1 (V12_002.cs line 146)",
    file: "V12_002.cs",
    line: 146,
    oldVal: "private readonly object dailySummaryLock = new object();",
    newVal: "private int _dailySummaryHeaderEnsured = 0;",
    status: "consistent",
    note: "Field renamed and retyped. No MSBuild HintPath or external path involved. Condition attribute N/A (C# field, not project reference).",
  },
];

// ─── Data: Section F ──────────────────────────────────────────────────────────

const verSteps: VerStep[] = [
  {
    step: 1,
    description: "Compile in NinjaTrader 8 (F5 in NinjaScript Editor). Expect zero errors. using System.Threading already present in every modified file.",
    runnable: true,
    needsPlatformAdj: false,
    depMissing: false,
    posixFlag: false,
    note: "NinjaTrader 8 hosts its own build pipeline. Not a CLI step. Runnable on the target Windows environment. No POSIX commands involved.",
  },
  {
    step: 2,
    description: "check_ascii.py src/V12_002.Symmetry.cs src/V12_002.SIMA.cs src/V12_002.Orders.Callbacks.Propagation.cs src/V12_002.cs src/V12_002.Symmetry.Follower.cs src/V12_002.Symmetry.Replace.cs src/V12_002.Orders.Callbacks.AccountOrders.cs src/V12_002.UI.Compliance.cs -- expect OK for every file.",
    runnable: false,
    needsPlatformAdj: false,
    depMissing: true,
    posixFlag: false,
    note: "DEPENDENCY MISSING. The document explicitly states byte_purge.py is absent. check_ascii.py is referenced in Section F Step 2 but is NEVER confirmed to exist in the document. The document says 'Existing utilities reused' lists Volatile.Read, Interlocked, ConcurrentDictionary, Array.Empty — check_ascii.py is NOT listed there. No evidence in the document that check_ascii.py exists at the repo root. Cannot be run until the script is confirmed present or created.",
  },
  {
    step: 3,
    description: "grep -nE \"^[[:space:]]*lock[[:space:]]*\\(\" src/V12_002.Symmetry.cs src/V12_002.SIMA.cs src/V12_002.Orders.Callbacks.Propagation.cs src/V12_002.Symmetry.Follower.cs src/V12_002.Symmetry.Replace.cs src/V12_002.UI.Compliance.cs -- expect zero matches.",
    runnable: false,
    needsPlatformAdj: true,
    depMissing: false,
    posixFlag: true,
    psEquivalent: "Select-String -Path src\\V12_002.Symmetry.cs,src\\V12_002.SIMA.cs,src\\V12_002.Orders.Callbacks.Propagation.cs,src\\V12_002.Symmetry.Follower.cs,src\\V12_002.Symmetry.Replace.cs,src\\V12_002.UI.Compliance.cs -Pattern '^\\s*lock\\s*\\(' | Should -BeNullOrEmpty",
    note: "Uses POSIX grep with extended regex (-nE) and POSIX character class [[:space:]]. Not available in PowerShell natively. grep.exe (via Git-for-Windows or WSL) would work but is not guaranteed. Native PowerShell equivalent: Select-String with -Pattern.",
  },
  {
    step: 4,
    description: "grep -n \"dailySummaryLock\" src/ -- expect zero matches (declaration and both acquisitions removed).",
    runnable: false,
    needsPlatformAdj: true,
    depMissing: false,
    posixFlag: true,
    psEquivalent: "Select-String -Path src\\*.cs -Recurse -Pattern 'dailySummaryLock' | Should -BeNullOrEmpty",
    note: "POSIX grep -n with directory argument requires -r flag (missing here — grep -n 'string' dir/ without -r only matches if src/ is a file, not a directory, on most implementations). Additionally not native PowerShell. Needs both the -r flag fix AND platform adjustment.",
  },
  {
    step: 5,
    description: "grep -n \"ctx\\.Sync\\|preCheckCtx\\.Sync\\|FollowerEntries\" src/ -- expect zero matches; followers must be accessed only via ctx.Followers or ctx.AddFollower / ctx.RemoveFollower.",
    runnable: false,
    needsPlatformAdj: true,
    depMissing: false,
    posixFlag: true,
    psEquivalent: "Select-String -Path src\\*.cs -Recurse -Pattern 'ctx\\.Sync|preCheckCtx\\.Sync|FollowerEntries' | Should -BeNullOrEmpty",
    note: "Same grep-in-directory issue as Step 4 (-r missing). Also not PowerShell-native. The alternation syntax (\\|) works in grep -E or GNU grep, but not plain grep on all POSIX systems. PowerShell equivalent uses regex alternation natively.",
  },
  {
    step: 6,
    description: "Run the forensics subagent (per CLAUDE.md Engineer Self-Audit P4 Step 2) to confirm zero lock(stateLock) usage in in-scope files and ASCII compliance globally.",
    runnable: false,
    needsPlatformAdj: false,
    depMissing: true,
    posixFlag: false,
    note: "DEPENDENCY MISSING. Requires CLAUDE.md and the forensics subagent infrastructure. Neither is verifiable from this document alone. External orchestration dependency.",
  },
  {
    step: 7,
    description: "Run the architect subagent (/loop-critic) to critique the AnchorSnapshot CAS-loop semantics against the Build 1004 FSM patterns already in use elsewhere in the codebase.",
    runnable: false,
    needsPlatformAdj: false,
    depMissing: true,
    posixFlag: false,
    note: "DEPENDENCY MISSING. Requires the /loop-critic subagent and access to the broader codebase (Build 1004 FSM patterns). External orchestration dependency not verifiable from this document.",
  },
  {
    step: 8,
    description: "High-volatility Sim session with EnableSIMA=true and 4-account fleet: OR entry, MASTER ANCHOR LOCKED log once, ANCHOR-01/ANCHOR-02 paths, MOVE-SYNC logs, CASCADE cancel.",
    runnable: false,
    needsPlatformAdj: false,
    depMissing: true,
    posixFlag: false,
    note: "Runtime smoke test in NinjaTrader 8 Sim environment. Requires live NinjaTrader installation and a configured sim brokerage. Not automatable from CLI. Needs platform (NT8 Windows) and a running strategy instance.",
  },
  {
    step: 9,
    description: "Concurrent flatten + entry stress (Phase 4 verification): slam IPC with simultaneous FLATTEN and ENTRY commands; confirm no ghost orders, no REAPER Critical Desync, no expectedPositions torn-state log entries.",
    runnable: false,
    needsPlatformAdj: false,
    depMissing: true,
    posixFlag: false,
    note: "Runtime stress test. Same NT8 environment dependency as Step 8. Requires IPC tooling. Not CLI-automatable.",
  },
  {
    step: 10,
    description: "REAPER audit cycle: confirm _lastExpectedPositionSetTicks grace window stamps fire after every AddOrUpdate mutation (verify timestamp progression in [ACCOUNT_SYNC] traces).",
    runnable: false,
    needsPlatformAdj: false,
    depMissing: true,
    posixFlag: false,
    note: "Log trace verification inside NT8 Output window. Requires a running strategy. Not CLI-automatable from a shell.",
  },
  {
    step: 11,
    description: "Optional dev-only diagnostic: instrument CAS loop in SymmetryGuardOnMasterFill to print when cur.IsResolved == true on retry. Expect zero retries in normal operation.",
    runnable: false,
    needsPlatformAdj: false,
    depMissing: false,
    posixFlag: false,
    note: "ADVISORY / optional. Requires code instrumentation and NT8 Sim run. Not blocking; document marks it optional.",
  },
  {
    step: 12,
    description: "Property-test substitute (manual): fire 50 rapid OR entries with 4 followers each. Confirm _followers array length monotonically tracks AddFollower / RemoveFollower calls via [SNAPSHOT] debug prints.",
    runnable: false,
    needsPlatformAdj: false,
    depMissing: false,
    posixFlag: false,
    note: "ADVISORY / manual property test. Requires NT8 Sim, debug instrumentation, and log diffing. Not CLI-automatable.",
  },
];

// ─── Summary Data ─────────────────────────────────────────────────────────────

const summaryItems: { severity: Severity; category: string; item: string }[] = [
  {
    severity: "moderate",
    category: "Missing script",
    item: "F.Step 2: check_ascii.py is referenced but never confirmed to exist in the document. byte_purge.py is explicitly called out as absent. check_ascii.py has no equivalent confirmation. Engineering cannot run this gate until the script is confirmed or created.",
  },
  {
    severity: "moderate",
    category: "POSIX grep — directory flag missing",
    item: "F.Step 4: grep -n 'dailySummaryLock' src/ — missing -r/--recursive flag. This command will fail or silently match nothing on most systems when src/ is a directory.",
  },
  {
    severity: "moderate",
    category: "Platform — POSIX grep on Windows PowerShell target",
    item: "F.Steps 3, 4, 5: All three grep verification commands are POSIX-only and will not run natively in PowerShell. The document does not specify that git-bash or WSL is available. PowerShell equivalents (Select-String) must be substituted.",
  },
  {
    severity: "moderate",
    category: "External subagent dependencies undocumented",
    item: "F.Steps 6, 7: forensics subagent (CLAUDE.md P4 Step 2) and architect subagent (/loop-critic) are referenced but the infrastructure for these is not described within this document. Engineering cannot execute these steps without additional setup documentation.",
  },
  {
    severity: "advisory",
    category: "Comment wording — PowerShell function call in comment",
    item: "D.4 general: No PowerShell comment in the plan contains a function call that would be evaluated at runtime — comments beginning with # in PowerShell are inert and never evaluated. However, if any plan-generated script used inline string interpolation (e.g., Write-Host \"Path: $(Get-Location)\") inside what was intended as a comment, the expression WOULD be evaluated. Reviewers should confirm all # comment lines in generated .ps1 files are purely explanatory strings, not accidentally active interpolated expressions. No specific instance found in this document, but worth noting for script authoring hygiene.",
  },
  {
    severity: "advisory",
    category: "MSBuild HintPath — no Condition attribute",
    item: "D.4 general: The document does not describe any MSBuild .csproj HintPath changes — all path substitutions are C# source-level member-access rewrites (ctx.Sync -> ctx.Followers etc.). Therefore the MSBuild Condition attribute question is not applicable to this plan as written. If future work adds assembly references, a Condition='Exists(...)' attribute on HintPath elements would be required to avoid build errors when the target DLL is absent.",
  },
  {
    severity: "advisory",
    category: "stateLock stub retention",
    item: "Step 4.1 explicitly retains the stateLock field as a 'dummy stub' because 22 out-of-scope partial files reference it. This is a deliberate architectural debt. Not blocking for this plan, but should be tracked in a follow-on migration ticket.",
  },
  {
    severity: "advisory",
    category: "Step 11 / Step 12 are optional",
    item: "F.Steps 11 and 12 are marked optional/advisory in the document. They should not be treated as release gates.",
  },
];

// ─── Helper components ────────────────────────────────────────────────────────

function Badge({ children, color }: { children: React.ReactNode; color: string }) {
  return (
    <span className={`inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-semibold ${color}`}>
      {children}
    </span>
  );
}

function SectionHeader({ icon, title, subtitle }: { icon: string; title: string; subtitle: string }) {
  return (
    <div className="mb-6 flex items-start gap-3">
      <span className="text-2xl">{icon}</span>
      <div>
        <h3 className="text-xl font-bold text-slate-900">{title}</h3>
        <p className="text-sm text-slate-500 mt-0.5">{subtitle}</p>
      </div>
    </div>
  );
}

function CodeBlock({ label, code }: { label: string; code: string }) {
  return (
    <div className="mt-2">
      <span className="text-xs font-semibold uppercase tracking-widest text-slate-400">{label}</span>
      <pre className="mt-1 overflow-x-auto rounded-md bg-slate-900 p-3 text-xs text-green-300 leading-relaxed whitespace-pre-wrap break-all">
        {code}
      </pre>
    </div>
  );
}

// ─── Section 1 ────────────────────────────────────────────────────────────────

function Section1() {
  const [expanded, setExpanded] = useState<number | null>(null);

  const type1 = lambdaSites.filter((s) => s.classification === "Type 1").length;
  const type2 = lambdaSites.filter((s) => s.classification === "Type 2").length;

  return (
    <div>
      <SectionHeader
        icon="🔬"
        title="Section C.1 — Complete Lambda Site Inventory (All 18 Sites)"
        subtitle={`${lambdaSites.length} sites reviewed · ${type1} Type 1 (pure-work) · ${type2} Type 2 (cleanup-after-primary)`}
      />

      {/* Legend */}
      <div className="mb-5 flex flex-wrap gap-3 text-xs">
        <span className="flex items-center gap-1.5"><span className="h-3 w-3 rounded-full bg-emerald-500 inline-block"></span>Type 1 — pure-work, early-return safe</span>
        <span className="flex items-center gap-1.5"><span className="h-3 w-3 rounded-full bg-rose-500 inline-block"></span>Type 2 — cleanup-after-primary, early-return unsafe</span>
        <span className="flex items-center gap-1.5"><span className="h-3 w-3 rounded-full bg-amber-400 inline-block"></span>Has OLD/NEW code block</span>
        <span className="flex items-center gap-1.5"><span className="h-3 w-3 rounded-full bg-violet-500 inline-block"></span>Transform B (CAS-based)</span>
      </div>

      <div className="space-y-3">
        {lambdaSites.map((site) => {
          const isOpen = expanded === site.site;
          const isType2 = site.classification === "Type 2";
          const borderColor = isType2 ? "border-rose-300" : "border-emerald-300";
          const headerBg = isType2 ? "bg-rose-50" : "bg-emerald-50";

          return (
            <div key={site.site} className={`rounded-xl border-2 ${borderColor} overflow-hidden shadow-sm`}>
              {/* Header row */}
              <button
                className={`w-full text-left px-4 py-3 ${headerBg} flex items-start justify-between gap-3 hover:brightness-95 transition-all`}
                onClick={() => setExpanded(isOpen ? null : site.site)}
              >
                <div className="flex flex-wrap items-center gap-2 min-w-0">
                  <span className="font-mono font-bold text-slate-700 text-sm">#{site.site}</span>
                  <span className="font-mono text-xs text-slate-600 bg-white rounded px-1.5 py-0.5 border border-slate-200">{site.file}:{site.line}</span>
                  <span className="font-mono text-xs font-semibold text-indigo-700 truncate">{site.method}</span>
                  <Badge color={site.transform === "B" ? "bg-violet-100 text-violet-800" : "bg-blue-100 text-blue-800"}>
                    Transform {site.transform}
                  </Badge>
                  <Badge color={isType2 ? "bg-rose-100 text-rose-800" : "bg-emerald-100 text-emerald-800"}>
                    {site.classification}
                  </Badge>
                  {site.hasOldNew && <Badge color="bg-amber-100 text-amber-800">OLD/NEW</Badge>}
                  {site.needsPlanUpdate && <Badge color="bg-red-200 text-red-900 font-bold">⚠ NEEDS PLAN UPDATE</Badge>}
                </div>
                <span className="text-slate-400 text-sm flex-shrink-0">{isOpen ? "▲" : "▼"}</span>
              </button>

              {/* Expanded body */}
              {isOpen && (
                <div className="px-4 pb-4 pt-3 bg-white space-y-3">
                  <div>
                    <p className="text-xs font-semibold text-slate-500 uppercase tracking-wider mb-1">Description (from C.1 table)</p>
                    <p className="text-sm text-slate-700">{site.description}</p>
                  </div>

                  <div className={`rounded-lg p-3 ${isType2 ? "bg-rose-50 border border-rose-200" : "bg-emerald-50 border border-emerald-200"}`}>
                    <p className="text-xs font-semibold uppercase tracking-wider mb-1 text-slate-500">
                      Classification: {site.classification} — Rationale
                    </p>
                    <p className="text-sm text-slate-700 leading-relaxed">{site.classRationale}</p>
                  </div>

                  <div className="rounded-lg bg-slate-50 border border-slate-200 p-3">
                    <p className="text-xs font-semibold uppercase tracking-wider text-slate-500 mb-1">
                      Evidence: shared-resource operation {isType2 ? "PRESENT (would be bypassed)" : "ABSENT"}
                    </p>
                    <p className="text-sm text-slate-700">{site.evidence}</p>
                  </div>

                  {site.hasOldNew && site.oldCode && site.newCode && (
                    <div>
                      <CodeBlock label="OLD (before ADR-019)" code={site.oldCode} />
                      <CodeBlock label="NEW (after ADR-019)" code={site.newCode} />
                      {site.confirmVar && (
                        <p className="mt-2 text-xs text-slate-500">
                          <span className="font-semibold">Classification confirmed by variable/call: </span>
                          <code className="bg-slate-100 rounded px-1 py-0.5 font-mono text-indigo-700">{site.confirmVar}</code>
                        </p>
                      )}
                    </div>
                  )}

                  {isType2 && !site.needsPlanUpdate && (
                    <div className="rounded-lg bg-blue-50 border border-blue-200 p-2.5 text-xs text-blue-800">
                      ✅ Type 2 site — fix IS fully documented in the plan. Engineering may proceed.
                    </div>
                  )}
                  {isType2 && site.needsPlanUpdate && (
                    <div className="rounded-lg bg-red-100 border border-red-300 p-2.5 text-xs text-red-900 font-bold">
                      ⛔ Type 2 site — NEEDS PLAN UPDATE BEFORE ENGINEERING BEGINS. No documented fix found.
                    </div>
                  )}
                </div>
              )}
            </div>
          );
        })}
      </div>
    </div>
  );
}

// ─── Section 2 ────────────────────────────────────────────────────────────────

function Section2() {
  return (
    <div>
      <SectionHeader
        icon="🔗"
        title="Section D.4 — Path Substitution Consistency"
        subtitle={`${pathEntries.length} substitution sites · All reviewed for naming drift and API correctness`}
      />

      {/* Special question answers */}
      <div className="mb-6 space-y-4">
        <div className="rounded-xl border-2 border-amber-300 bg-amber-50 p-4">
          <p className="text-xs font-bold uppercase tracking-wider text-amber-700 mb-2">⚡ Special Question: Does a function call inside a PowerShell comment get evaluated at runtime?</p>
          <p className="text-sm text-slate-700 leading-relaxed">
            <strong>No.</strong> In PowerShell, any text after a <code className="bg-white rounded px-1 font-mono">#</code> character on a line is a comment and is
            never evaluated at runtime. A developer reading <code className="bg-white rounded px-1 font-mono"># Get-Date returns current time</code> sees exactly
            that literal string. However, within PowerShell <em>double-quoted strings</em> (not comments),
            the <code className="bg-white rounded px-1 font-mono">$(expression)</code> subexpression syntax IS evaluated.
            If a developer accidentally places what was intended as a comment inside a string interpolation context,
            the function call WOULD execute. Within the scope of this document, all substitutions are C# source rewrites — no .ps1 scripts with
            function calls in comments are present. The risk is a future scripting concern, not a current plan defect.
          </p>
        </div>

        <div className="rounded-xl border-2 border-blue-300 bg-blue-50 p-4">
          <p className="text-xs font-bold uppercase tracking-wider text-blue-700 mb-2">🏗️ Special Question: MSBuild HintPath — does it need a Condition attribute?</p>
          <p className="text-sm text-slate-700 leading-relaxed">
            <strong>Yes, in general — but N/A for this plan.</strong> A MSBuild <code className="bg-white rounded px-1 font-mono">&lt;HintPath&gt;</code> referencing an assembly that
            may not be present should use <code className="bg-white rounded px-1 font-mono">Condition="Exists('...')"</code> (or a target-conditional Reference) to avoid
            build errors when the target software (e.g., NinjaTrader) is absent from the build machine.
            <strong> However, Section D.4 of this document contains zero MSBuild HintPath changes.</strong> All "path substitutions"
            are C# member-access rewrites (ctx.Sync → ctx.Followers, etc.). No .csproj or .targets file modifications
            are described. The Condition attribute question therefore does not apply to this plan as written.
          </p>
        </div>
      </div>

      {/* Table */}
      <div className="overflow-x-auto rounded-xl border border-slate-200 shadow-sm">
        <table className="min-w-full divide-y divide-slate-200 text-sm">
          <thead className="bg-slate-100">
            <tr>
              <th className="px-3 py-3 text-left text-xs font-bold uppercase tracking-wider text-slate-500">Step / Site</th>
              <th className="px-3 py-3 text-left text-xs font-bold uppercase tracking-wider text-slate-500">OLD value</th>
              <th className="px-3 py-3 text-left text-xs font-bold uppercase tracking-wider text-slate-500">NEW value</th>
              <th className="px-3 py-3 text-left text-xs font-bold uppercase tracking-wider text-slate-500">Status</th>
              <th className="px-3 py-3 text-left text-xs font-bold uppercase tracking-wider text-slate-500">Notes</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-100 bg-white">
            {pathEntries.map((entry, idx) => (
              <tr key={idx} className="hover:bg-slate-50">
                <td className="px-3 py-3 font-mono text-xs text-indigo-700 whitespace-nowrap align-top">{entry.step}</td>
                <td className="px-3 py-3 font-mono text-xs text-slate-600 align-top max-w-xs">
                  <span className="bg-red-50 rounded px-1 py-0.5 block">{entry.oldVal}</span>
                </td>
                <td className="px-3 py-3 font-mono text-xs text-slate-600 align-top max-w-xs">
                  <span className="bg-green-50 rounded px-1 py-0.5 block">{entry.newVal}</span>
                </td>
                <td className="px-3 py-3 align-top whitespace-nowrap">
                  {entry.status === "consistent" ? (
                    <Badge color="bg-emerald-100 text-emerald-800">✓ Consistent</Badge>
                  ) : (
                    <Badge color="bg-rose-100 text-rose-800">✗ Inconsistency</Badge>
                  )}
                </td>
                <td className="px-3 py-3 text-xs text-slate-600 align-top">{entry.note}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}

// ─── Section 3 ────────────────────────────────────────────────────────────────

function Section3() {
  const posixCount = verSteps.filter((s) => s.posixFlag).length;
  const missingDeps = verSteps.filter((s) => s.depMissing).length;
  const runnable = verSteps.filter((s) => s.runnable).length;

  return (
    <div>
      <SectionHeader
        icon="🛡️"
        title="Section F — Verification Step Platform Check (All 12 Steps)"
        subtitle={`${runnable} runnable · ${posixCount} POSIX-flagged · ${missingDeps} with missing dependencies`}
      />

      {/* check_ascii.py special call-out */}
      <div className="mb-5 rounded-xl border-2 border-rose-400 bg-rose-50 p-4">
        <p className="text-xs font-bold uppercase tracking-wider text-rose-700 mb-2">🔍 check_ascii.py — Does It Exist?</p>
        <p className="text-sm text-slate-700 leading-relaxed">
          <strong>NOT CONFIRMED by document evidence.</strong> Section F Step 2 invokes <code className="bg-white rounded px-1 font-mono">check_ascii.py</code> without
          any prior statement confirming its presence. By contrast, <code className="bg-white rounded px-1 font-mono">byte_purge.py</code> is explicitly referenced
          in the CONSTRAINTS section (ASCII-only mandate) and is also absent. The "Existing utilities reused" paragraph at the end of the document
          lists <code className="bg-white rounded px-1 font-mono">Volatile.Read</code>, <code className="bg-white rounded px-1 font-mono">Interlocked.CompareExchange</code>,
          {" "}<code className="bg-white rounded px-1 font-mono">ConcurrentDictionary.AddOrUpdate</code>, <code className="bg-white rounded px-1 font-mono">_followerReplaceSpecs</code>,
          and <code className="bg-white rounded px-1 font-mono">Array.Empty&lt;string&gt;()</code> — check_ascii.py is <strong>not listed</strong>.
          Verdict: <strong>existence unverifiable from this document</strong>. Distinct from byte_purge.py (which is confirmed absent).
          Engineering must locate or create check_ascii.py before Step 2 can be executed.
        </p>
      </div>

      <div className="space-y-3">
        {verSteps.map((step) => {
          let statusBg = "bg-emerald-50 border-emerald-300";
          let statusLabel = "✅ Runnable";
          let statusBadge = "bg-emerald-100 text-emerald-800";

          if (step.depMissing) {
            statusBg = "bg-rose-50 border-rose-300";
            statusLabel = "⛔ Dependency Missing";
            statusBadge = "bg-rose-100 text-rose-800";
          } else if (step.posixFlag) {
            statusBg = "bg-amber-50 border-amber-300";
            statusLabel = "⚠ Needs Platform Adjustment";
            statusBadge = "bg-amber-100 text-amber-800";
          } else if (!step.runnable) {
            statusBg = "bg-slate-50 border-slate-300";
            statusLabel = "ℹ Runtime / Manual";
            statusBadge = "bg-slate-100 text-slate-600";
          }

          return (
            <div key={step.step} className={`rounded-xl border-2 ${statusBg} p-4`}>
              <div className="flex flex-wrap items-start gap-2 mb-2">
                <span className="font-mono font-bold text-slate-700 text-sm">F.{step.step}</span>
                <Badge color={statusBadge}>{statusLabel}</Badge>
                {step.posixFlag && <Badge color="bg-orange-100 text-orange-800">POSIX grep</Badge>}
                {step.depMissing && <Badge color="bg-red-200 text-red-900">Dep Missing</Badge>}
              </div>

              <p className="text-sm text-slate-700 font-mono bg-white/70 rounded p-2 mb-2 text-xs leading-relaxed">
                {step.description}
              </p>

              <p className="text-xs text-slate-600 leading-relaxed">{step.note}</p>

              {step.psEquivalent && (
                <div className="mt-2">
                  <p className="text-xs font-semibold text-orange-700 mb-1">PowerShell Native Equivalent:</p>
                  <pre className="text-xs bg-slate-900 text-cyan-300 rounded-md p-2.5 overflow-x-auto whitespace-pre-wrap break-all">
                    {step.psEquivalent}
                  </pre>
                </div>
              )}
            </div>
          );
        })}
      </div>
    </div>
  );
}

// ─── Section 4 ────────────────────────────────────────────────────────────────

function Section4() {
  const blocking = summaryItems.filter((i) => i.severity === "blocking");
  const moderate = summaryItems.filter((i) => i.severity === "moderate");
  const advisory = summaryItems.filter((i) => i.severity === "advisory");

  return (
    <div>
      <SectionHeader
        icon="📋"
        title="Overall Summary — Architectural Review Verdict"
        subtitle="All findings grouped by severity · Blocking / Moderate / Advisory"
      />

      {/* Verdict banner */}
      <div className="mb-6 rounded-xl border-2 border-blue-400 bg-blue-600 p-5 text-white shadow-lg">
        <p className="text-lg font-bold mb-1">
          🟡 VERDICT: <span className="text-yellow-300">NEEDS PLAN UPDATE (moderate items) before engineering begins</span>
        </p>
        <p className="text-sm text-blue-100 leading-relaxed">
          No Type 2 site lacks a documented fix — all four Type 2 sites (7, 11, 12, 14, 15, 17, 18) have complete OLD/NEW blocks in the plan.
          The plan is NOT blocked by lambda classification issues. However, three moderate-severity items must be resolved
          in the plan before the team can execute all verification gates: the check_ascii.py script must be confirmed or created,
          the grep commands in F.3/F.4/F.5 must be corrected for PowerShell/Windows, and the subagent orchestration
          infrastructure (F.6/F.7) must be documented.
        </p>
      </div>

      {/* Blocking */}
      <div className="mb-5">
        <h4 className="text-sm font-bold uppercase tracking-wider text-red-700 mb-3 flex items-center gap-2">
          <span className="h-2 w-2 rounded-full bg-red-600 inline-block"></span>
          BLOCKING — Must resolve before engineering begins
        </h4>
        {blocking.length === 0 ? (
          <div className="rounded-lg border border-emerald-200 bg-emerald-50 p-4 text-sm text-emerald-800">
            ✅ No blocking items. All Type 2 lambda sites have documented fixes. Engineering may proceed on the C# migration work.
          </div>
        ) : (
          <div className="space-y-2">
            {blocking.map((item, idx) => (
              <div key={idx} className="rounded-lg border-2 border-red-400 bg-red-50 p-3">
                <p className="text-xs font-bold text-red-700 mb-1">{item.category}</p>
                <p className="text-sm text-slate-700">{item.item}</p>
              </div>
            ))}
          </div>
        )}
      </div>

      {/* Moderate */}
      <div className="mb-5">
        <h4 className="text-sm font-bold uppercase tracking-wider text-amber-700 mb-3 flex items-center gap-2">
          <span className="h-2 w-2 rounded-full bg-amber-500 inline-block"></span>
          MODERATE — Plan update needed for complete executability
        </h4>
        <div className="space-y-2">
          {moderate.map((item, idx) => (
            <div key={idx} className="rounded-lg border-2 border-amber-300 bg-amber-50 p-3">
              <p className="text-xs font-bold text-amber-700 mb-1">{item.category}</p>
              <p className="text-sm text-slate-700">{item.item}</p>
            </div>
          ))}
        </div>
      </div>

      {/* Advisory */}
      <div className="mb-5">
        <h4 className="text-sm font-bold uppercase tracking-wider text-blue-700 mb-3 flex items-center gap-2">
          <span className="h-2 w-2 rounded-full bg-blue-400 inline-block"></span>
          ADVISORY — Non-blocking; note for future phases
        </h4>
        <div className="space-y-2">
          {advisory.map((item, idx) => (
            <div key={idx} className="rounded-lg border border-blue-200 bg-blue-50 p-3">
              <p className="text-xs font-bold text-blue-700 mb-1">{item.category}</p>
              <p className="text-sm text-slate-700">{item.item}</p>
            </div>
          ))}
        </div>
      </div>

      {/* Exact items list */}
      <div className="rounded-xl border-2 border-slate-300 bg-slate-50 p-5">
        <p className="text-sm font-bold text-slate-700 mb-3">Exact items requiring plan update:</p>
        <ol className="list-decimal list-inside space-y-2 text-sm text-slate-700">
          <li><span className="font-semibold text-amber-700">[Moderate]</span> Confirm or create <code className="bg-white rounded px-1 font-mono text-xs">check_ascii.py</code> at the repo root — referenced in F.Step 2 but not confirmed to exist in the document.</li>
          <li><span className="font-semibold text-amber-700">[Moderate]</span> Add <code className="bg-white rounded px-1 font-mono text-xs">-r</code> flag to <code className="bg-white rounded px-1 font-mono text-xs">grep -n</code> in F.Steps 4 and 5 (directory argument without recursive flag).</li>
          <li><span className="font-semibold text-amber-700">[Moderate]</span> Replace or supplement POSIX <code className="bg-white rounded px-1 font-mono text-xs">grep</code> commands in F.Steps 3, 4, 5 with PowerShell <code className="bg-white rounded px-1 font-mono text-xs">Select-String</code> equivalents for a Windows/PowerShell target environment.</li>
          <li><span className="font-semibold text-amber-700">[Moderate]</span> Document the forensics subagent (CLAUDE.md P4 Step 2) and <code className="bg-white rounded px-1 font-mono text-xs">/loop-critic</code> subagent setup so F.Steps 6 and 7 are executable by the engineering team.</li>
        </ol>
        <p className="mt-4 text-sm text-slate-500">
          <span className="font-semibold">State:</span> The C# migration work (Sections C.1 through D.4, Files 1–4, Steps 1.1–5.4) is internally consistent and ready for engineering. The verification gate plan (Section F) needs the four updates listed above before ALL gates can be executed.
        </p>
      </div>
    </div>
  );
}

// ─── Tab navigation ───────────────────────────────────────────────────────────

const TABS = [
  { id: "s1", label: "C.1 Lambda Sites", short: "Lambda Inventory" },
  { id: "s2", label: "D.4 Path Substitutions", short: "Path Consistency" },
  { id: "s3", label: "F. Platform Check", short: "Verification Gates" },
  { id: "s4", label: "Summary & Verdict", short: "Summary" },
];

// ─── App ──────────────────────────────────────────────────────────────────────

function App() {
  const [activeTab, setActiveTab] = useState("s1");

  return (
    <div className="min-h-screen bg-gradient-to-br from-slate-100 via-slate-50 to-indigo-50">
      {/* Top bar */}
      <header className="bg-slate-900 shadow-xl sticky top-0 z-50">
        <div className="max-w-7xl mx-auto px-4 py-4 flex flex-col sm:flex-row sm:items-center sm:justify-between gap-2">
          <div>
            <h1 className="text-white font-mono text-xs tracking-widest uppercase opacity-60 mb-0.5">
              Technical Design Review Dashboard
            </h1>
            <h2 className="text-white font-bold text-lg leading-tight">
              Claude Opus 4.5 &mdash; ADR-019 Sovereign Substrate
            </h2>
          </div>
          <div className="flex flex-col items-start sm:items-end gap-0.5">
            <span className="text-slate-400 text-xs font-mono">BUILD_TAG DELTA:</span>
            <code className="text-amber-400 font-mono text-sm font-bold tracking-wide">
              1111.002-v28.0 → 1111.003-v28.0-adr019
            </code>
          </div>
        </div>

        {/* Tab bar */}
        <div className="max-w-7xl mx-auto px-4 flex gap-1 pb-0 overflow-x-auto">
          {TABS.map((tab, i) => (
            <button
              key={tab.id}
              onClick={() => setActiveTab(tab.id)}
              className={`px-4 py-2.5 text-sm font-semibold rounded-t-lg whitespace-nowrap transition-all border-b-2 ${
                activeTab === tab.id
                  ? "bg-white text-indigo-700 border-indigo-500"
                  : "text-slate-400 border-transparent hover:text-slate-200 hover:bg-slate-800"
              }`}
            >
              <span className="hidden sm:inline">{tab.label}</span>
              <span className="sm:hidden">{i + 1}. {tab.short}</span>
            </button>
          ))}
        </div>
      </header>

      {/* Content */}
      <main className="max-w-7xl mx-auto px-4 py-8">
        {/* Mission context strip */}
        <div className="mb-6 rounded-xl bg-white border border-slate-200 shadow-sm px-5 py-3 flex flex-wrap gap-4 text-xs text-slate-600">
          <span><span className="font-semibold text-slate-800">Mission:</span> Eliminate 11 residual lock sites + dailySummaryLock DNA violation</span>
          <span><span className="font-semibold text-slate-800">Strategy:</span> AnchorSnapshot + string[] follower array via Interlocked.CompareExchange</span>
          <span><span className="font-semibold text-slate-800">Sites reviewed:</span> 18 (C.1 inventory)</span>
          <span><span className="font-semibold text-slate-800">Files modified:</span> 8</span>
          <span><span className="font-semibold text-slate-800">Peer review cited:</span> GPT 5.4</span>
        </div>

        {activeTab === "s1" && <Section1 />}
        {activeTab === "s2" && <Section2 />}
        {activeTab === "s3" && <Section3 />}
        {activeTab === "s4" && <Section4 />}
      </main>

      {/* Footer */}
      <footer className="mt-12 border-t border-slate-200 bg-white py-5 text-center text-xs text-slate-400">
        <p>
          Reviewed by <span className="font-semibold text-slate-600">Claude Opus 4.5</span> &bull;
          {" "}All findings sourced exclusively from{" "}
          <code className="bg-slate-100 rounded px-1">docs/brain/implementation_plan.md</code>
          {" "}(BUILD_TAG: 1111.002-v28.0 → 1111.003-v28.0-adr019)
        </p>
      </footer>
    </div>
  );
}
