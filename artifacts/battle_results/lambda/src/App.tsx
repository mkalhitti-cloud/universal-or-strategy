import { useState } from "react";

// ─────────────────────────────────────────────────────────────────────────────
// DATA: derived strictly from the fetched document
// ─────────────────────────────────────────────────────────────────────────────

const BUILD_TAG_DELTA = "1111.002-v28.0 -> 1111.003-v28.0-adr019";

type Classification = "Type 1" | "Type 2" | "Unverifiable";
type Transform = "A" | "B";

interface SiteCard {
  site: number;
  file: string;
  line: number | string;
  method: string;
  transform: Transform;
  description: string;
  classification: Classification;
  evidenceDetail: string;
  oldCode?: string;
  newCode?: string;
  confirmedVar?: string;
  needsPlanUpdate: boolean;
}

const SITES: SiteCard[] = [
  {
    site: 1,
    file: "Symmetry.cs",
    line: 115,
    method: "SymmetryGuardRegisterFollower",
    transform: "A",
    description: "HashSet.Add under ctx.Sync",
    classification: "Type 1",
    evidenceDetail:
      "Pure write of a follower name into the HashSet. The lock is the only operation; no post-mutation cleanup of shared state follows. Removing the lock and substituting ctx.AddFollower() (CAS loop) is safe — no resource release is bypassed.",
    oldCode: `lock (ctx.Sync) { ctx.FollowerEntries.Add(name); }`,
    newCode: `ctx.AddFollower(name); // CAS loop, cold path`,
    confirmedVar: "FollowerEntries / _followers",
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
    evidenceDetail:
      "Transform B replaces the entire lock block with a CAS loop that publishes a new AnchorSnapshot atomically. The document provides explicit OLD/NEW blocks. No post-mutation semaphore release, dictionary removal, or flag clear follows the primary operation inside the lock — the lock was the serialization boundary, not a cleanup gate. Safe to replace.",
    oldCode: `lock (ctx.Sync) {
  ctx.IsResolved = true;
  ctx.MasterAnchorPrice = avg;
  ctx.MasterWeightedFill = fill;
  ctx.MasterFilledQuantity = qty;
}`,
    newCode: `// CAS loop: TryPublishAnchor(expected, new AnchorSnapshot(true, avg, fill, qty))
// Retries until CompareExchange succeeds.`,
    confirmedVar: "_anchor / AnchorSnapshot",
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
    evidenceDetail:
      "Read-only access to anchor fields. No state mutation, no cleanup, no shared-resource release inside the lock body. Replacing with a single Volatile.Read (ctx.Anchor) is safe — nothing is bypassed.",
    oldCode: `lock (ctx.Sync) {
  bool anchorReady = ctx.IsResolved;
  double preCheckAnchor = ctx.MasterAnchorPrice;
}`,
    newCode: `AnchorSnapshot preSnap = preCheckCtx.Anchor; // single Volatile.Read
bool anchorReady = preSnap.IsResolved;
double preCheckAnchor = preSnap.MasterAnchorPrice;`,
    confirmedVar: "_anchor / AnchorSnapshot",
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
    evidenceDetail:
      "Read-only anchor snapshot. Same reasoning as Site 3. No post-read resource cleanup inside the lock. Volatile.Read substitution is safe.",
    oldCode: `lock (ctx.Sync) {
  bool isResolved = ctx.IsResolved;
  double masterAnchor = ctx.MasterAnchorPrice;
}`,
    newCode: `AnchorSnapshot snap = ctx.Anchor;
bool isResolved = snap.IsResolved;
double masterAnchor = snap.MasterAnchorPrice;`,
    confirmedVar: "_anchor / AnchorSnapshot",
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
    evidenceDetail:
      "Read-only iteration over FollowerEntries. No mutation, removal, or semaphore release inside the lock body. Replacing with immutable string[] snapshot from ctx.Followers is safe — nothing is bypassed by the early substitution.",
    oldCode: `lock (ctx.Sync) {
  foreach (string fleetEntryName in ctx.FollowerEntries) { ... }
}`,
    newCode: `string[] snap = ctx.Followers;
foreach (string fleetEntryName in snap) { ... }`,
    confirmedVar: "FollowerEntries / _followers",
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
    evidenceDetail:
      "Read-only ToArray() snapshot inside the lock; the actual cleanup work happens after the lock block on the snapshot. No shared-resource release is inside the lock itself. Replacing with ctx.Followers (already an immutable array) is safe.",
    oldCode: `string[] followers;
lock (ctx.Sync) { followers = ctx.FollowerEntries.ToArray(); }`,
    newCode: `string[] followers = ctx.Followers; // zero-alloc, no lock`,
    confirmedVar: "FollowerEntries / _followers",
    needsPlanUpdate: false,
  },
  {
    site: 7,
    file: "Symmetry.Replace.cs",
    line: 224,
    method: "SymmetryGuardForgetEntry",
    transform: "A",
    description: "Follower removal under ctx.Sync",
    classification: "Type 1",
    evidenceDetail:
      "HashSet.Remove inside the lock — this is the primary operation, not a cleanup that follows another operation. The CAS-loop RemoveFollower() replaces it atomically. No subsequent resource release inside the lock body is bypassed.",
    oldCode: `lock (ctx.Sync) ctx.FollowerEntries.Remove(entryName);`,
    newCode: `ctx.RemoveFollower(entryName); // CAS loop`,
    confirmedVar: "FollowerEntries / _followers",
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
    evidenceDetail:
      "Read-only iteration checking activePositions.ContainsKey — activePositions is already a ConcurrentDictionary; the inner lookup is lock-free. No shared resource release inside the lock. String[] snapshot substitution is safe.",
    oldCode: `lock (ctx.Sync) {
  foreach (string follower in ctx.FollowerEntries) {
    exists = activePositions.ContainsKey(follower); ...
  }
}`,
    newCode: `string[] snap = ctx.Followers;
foreach (string follower in snap) {
  exists = activePositions.ContainsKey(follower); ...
}`,
    confirmedVar: "FollowerEntries / _followers",
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
    evidenceDetail:
      "HOT PATH. Lock is for a ToArray() read copy only. Document provides explicit OLD/NEW. The immutable string[] returned by ctx.Followers IS the snapshot — no ToArray() copy needed, no lock. Nothing inside the lock body would be bypassed; no cleanup follows the read.",
    oldCode: `string[] snapshot;
lock (ctx.Sync) { snapshot = ctx.FollowerEntries.ToArray(); }
followerEntryNames = snapshot;`,
    newCode: `// ADR-019: zero-alloc, lock-free, point-in-time consistent.
followerEntryNames = ctx.Followers;`,
    confirmedVar: "FollowerEntries / _followers",
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
    evidenceDetail:
      "Read-only ToArray() snapshot. No cleanup or shared-resource release inside the lock body. ctx.Followers substitution is safe; followerEntries.Length check continues to work on string[].",
    oldCode: `lock (ctx.Sync) followerEntries = ctx.FollowerEntries.ToArray();`,
    newCode: `followerEntries = ctx.Followers;`,
    confirmedVar: "FollowerEntries / _followers",
    needsPlanUpdate: false,
  },
  {
    site: 11,
    file: "Orders.Callbacks.AccountOrders.cs",
    line: 300,
    method: "HandleMatchedFollowerOrder",
    transform: "A",
    description: "fsm.State write under stateLock",
    classification: "Type 1",
    evidenceDetail:
      "Document's HALLUCINATION CANARY section explicitly states: this is the ONLY site where the lock is removed entirely without a replacement CAS loop, because the actor pipeline already guarantees single-threaded execution for that specific call graph. The lock is dead weight — no concurrent writers exist. Removing it outright bypasses nothing; the FSM state write and surrounding reads are purely sequential. Classified Type 1 (pure work, safe removal).",
    oldCode: `lock (stateLock) {
  masterFilled = ...;
  if (!masterFilled) {
    qty = fsm.PendingQty; price = fsm.PendingPrice;
    fsm.State = FollowerReplaceState.Submitting;
  }
}`,
    newCode: `// ADR-019: single-threaded by actor pipeline. Lock removed entirely.
masterFilled = !string.IsNullOrEmpty(fsm.MasterSignalName) && ...;
if (!masterFilled) {
  qty = fsm.PendingQty; price = fsm.PendingPrice;
  fsm.State = FollowerReplaceState.Submitting;
}`,
    confirmedVar: "stateLock (removed)",
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
    evidenceDetail:
      "CRITICAL: The NEW code in Step 2.1 shows that after the primary AddOrUpdate, the plan moves Interlocked.Exchange(_lastExpectedPositionSetTicks) and StampAccountFillGrace() OUTSIDE the former lock block. These were previously inside or logically associated with the stateLock serialization boundary. However, the plan notes 'the post-mutation Interlocked stamps and grace-window calls move outside the atomic update (they were never serialised by stateLock in any meaningful sense)'. The plan also includes _dispatchSyncPendingExpKeys.TryRemove in Step 2.3. CLASSIFICATION RATIONALE: The document explicitly confirms these post-update operations exist and are being moved. If an engineer naively adds an early-return as the FIRST statement of the OLD lock body, they would bypass the Interlocked timestamp update and StampAccountFillGrace call. The NEW code correctly positions these outside. The plan documents this — but the transform is B precisely because the body is non-trivial. MARKED Type 2 because the body contains state operations (timestamp stamp, grace window, TryRemove in Step 2.3) that follow the primary call and would be bypassed by a naive early-return before the AddOrUpdate.",
    oldCode: `lock (stateLock) {
  if (expectedPositions != null)
    expectedPositions[accountName] = (expectedPositions.ContainsKey(accountName)
      ? expectedPositions[accountName] : 0) + delta;
}
// timestamp + grace calls outside (but logically chained)`,
    newCode: `int capturedNew = expectedPositions.AddOrUpdate(accountName,
  k => { capturedOld = 0; return delta; },
  (k, v) => { capturedOld = v; return v + delta; });
Print(...);
if (delta != 0) {
  Interlocked.Exchange(ref _lastExpectedPositionSetTicks, DateTime.UtcNow.Ticks);
  if (capturedNew != 0) StampAccountFillGrace(accountName);
}`,
    confirmedVar: "expectedPositions, _lastExpectedPositionSetTicks",
    needsPlanUpdate: false, // plan already documents the fix in Step 2.1
  },
  {
    site: 13,
    file: "SIMA.cs",
    line: 100,
    method: "AddOrUpdateExpectedPositionLocked",
    transform: "B",
    description: "expectedPositions mutation under stateLock",
    classification: "Type 1",
    evidenceDetail:
      "Step 2.2 shows the new body is a direct pass-through to ConcurrentDictionary.AddOrUpdate with no subsequent shared-state operations. No timestamp, no grace window, no TryRemove follows. Classified Type 1 — the lock wraps only the primary call; nothing is bypassed.",
    oldCode: `lock (stateLock) {
  if (expectedPositions != null && updateExisting != null)
    expectedPositions.AddOrUpdate(accountName, addValue, (k, v) => updateExisting(v));
}`,
    newCode: `expectedPositions.AddOrUpdate(accountName, addValue, (k, v) => updateExisting(v));`,
    confirmedVar: "expectedPositions",
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
    evidenceDetail:
      "CRITICAL: Step 2.3 NEW code shows that after the primary AddOrUpdate, there is _dispatchSyncPendingExpKeys.TryRemove(accountName, out _) when value==0, plus Interlocked timestamp and StampAccountFillGrace when value!=0. These shared-state operations follow the primary call and would be bypassed by adding an early-return as the FIRST statement of the old lock body. The plan's NEW code correctly includes all three. Plan documents the fix — classify Type 2 with documented fix.",
    oldCode: `lock (stateLock) {
  if (expectedPositions != null)
    expectedPositions[accountName] = value;
}`,
    newCode: `expectedPositions.AddOrUpdate(accountName, value, (k, v) => value);
if (value == 0) _dispatchSyncPendingExpKeys.TryRemove(accountName, out _);
if (value != 0) {
  Interlocked.Exchange(ref _lastExpectedPositionSetTicks, DateTime.UtcNow.Ticks);
  StampAccountFillGrace(accountName);
}`,
    confirmedVar: "_dispatchSyncPendingExpKeys, _lastExpectedPositionSetTicks",
    needsPlanUpdate: false, // fix is documented in Step 2.3
  },
  {
    site: 15,
    file: "SIMA.cs",
    line: 134,
    method: "DeltaExpectedPositionLocked",
    transform: "B",
    description: "expectedPositions mutation under stateLock",
    classification: "Type 2",
    evidenceDetail:
      "Step 2.4 NEW code shows Interlocked.Exchange(_lastExpectedPositionSetTicks) follows the primary AddOrUpdate. A naive early-return before the AddOrUpdate would bypass the timestamp update. Plan documents the fix — classify Type 2 with documented fix.",
    oldCode: `lock (stateLock) {
  if (expectedPositions != null)
    expectedPositions[accountName] = expectedPositions.GetValueOrDefault(accountName) + delta;
}`,
    newCode: `int capturedUpdated = expectedPositions.AddOrUpdate(accountName,
  k => { capturedCurrent = 0; return delta; },
  (k, v) => { capturedCurrent = v; return v + delta; });
Print(...);
if (delta != 0) Interlocked.Exchange(ref _lastExpectedPositionSetTicks, DateTime.UtcNow.Ticks);`,
    confirmedVar: "_lastExpectedPositionSetTicks",
    needsPlanUpdate: false, // fix is documented in Step 2.4
  },
  {
    site: 16,
    file: "V12_002.cs",
    line: 146,
    method: "Field Declaration",
    transform: "A",
    description: "dailySummaryLock declaration",
    classification: "Type 1",
    evidenceDetail:
      "Field declaration only — not a lambda site with a body. Removal of the declaration is a pure structural cleanup. Step 4.1 replaces it with _dailySummaryHeaderEnsured int field. The stateLock stub is retained for out-of-scope file compatibility. No runtime bypass risk.",
    oldCode: `private readonly object dailySummaryLock = new object();`,
    newCode: `private int _dailySummaryHeaderEnsured = 0; // ADR-019 one-shot CAS guard`,
    confirmedVar: "dailySummaryLock -> _dailySummaryHeaderEnsured",
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
    evidenceDetail:
      "CRITICAL: Step 5.4 NEW code shows that EnsureDailySummaryCsv has a try/catch with Interlocked.Exchange(_dailySummaryHeaderEnsured, 0) in the catch block to reset the flag on I/O failure. If an engineer added an early-return as the FIRST statement of the old lock body (e.g., if already ensured), they would bypass the catch-based retry reset. The CAS guard (CompareExchange returning early if already 1) IS the correct pattern, but the plan documents it correctly. Type 2 because the body has a catch clause that resets shared state (_dailySummaryHeaderEnsured) after the primary File.WriteAllText call — bypassing via naive early-return would prevent retry on I/O failure. Plan documents the correct fix via CAS.",
    oldCode: `lock (dailySummaryLock) {
  if (!File.Exists(dailySummaryCsvPath)) {
    File.WriteAllText(dailySummaryCsvPath, header + ...);
  }
}`,
    newCode: `if (Interlocked.CompareExchange(ref _dailySummaryHeaderEnsured, 1, 0) != 0) return;
try {
  if (!File.Exists(dailySummaryCsvPath))
    File.WriteAllText(dailySummaryCsvPath, header + ...);
} catch {
  Interlocked.Exchange(ref _dailySummaryHeaderEnsured, 0); // retry on I/O failure
}`,
    confirmedVar: "_dailySummaryHeaderEnsured",
    needsPlanUpdate: false, // fix is documented in Step 5.4
  },
  {
    site: 18,
    file: "UI.Compliance.cs",
    line: 144,
    method: "AppendDailySummary",
    transform: "A",
    description: "lock(dailySummaryLock)",
    classification: "Type 1",
    evidenceDetail:
      "Step 5.4 NEW code shows AppendDailySummary calls EnsureDailySummaryCsv() (which now handles its own CAS guard) and then fires a Task.Run for the append — no lock wraps the append itself. The lock removal in AppendDailySummary is pure cleanup; the I/O is best-effort (swallowed catch). No shared-resource release follows the primary operation inside the lock body.",
    oldCode: `lock (dailySummaryLock) {
  EnsureDailySummaryCsv();
  File.AppendAllText(dailySummaryCsvPath, line + ...);
}`,
    newCode: `EnsureDailySummaryCsv(); // CAS-guarded internally
Task.Run(() => { try { File.AppendAllText(pathCopy, lineCopy); } catch { } });`,
    confirmedVar: "dailySummaryLock (removed)",
    needsPlanUpdate: false,
  },
];

// ─────────────────────────────────────────────────────────────────────────────
// SECTION D.4 PATH SUBSTITUTION DATA
// ─────────────────────────────────────────────────────────────────────────────

interface PathChange {
  file: string;
  section: string;
  description: string;
  oldRef: string;
  newRef: string;
  status: "consistent" | "inconsistency" | "advisory";
  notes: string;
}

const PATH_CHANGES: PathChange[] = [
  {
    file: "Symmetry.Follower.cs",
    section: "Step 5.1 / D.4",
    description: "Anchor read — lines 36-42",
    oldRef: "lock (ctx.Sync) { bool anchorReady = ctx.IsResolved; double preCheckAnchor = ctx.MasterAnchorPrice; }",
    newRef: "AnchorSnapshot preSnap = preCheckCtx.Anchor; bool anchorReady = preSnap.IsResolved; double preCheckAnchor = preSnap.MasterAnchorPrice;",
    status: "consistent",
    notes: "Field names map 1-to-1 to AnchorSnapshot.IsResolved and AnchorSnapshot.MasterAnchorPrice as defined in Step 1.1. The variable preCheckCtx is the same ctx reference accessed via a different local alias. Consistent.",
  },
  {
    file: "Symmetry.Follower.cs",
    section: "Step 5.1 / D.4",
    description: "Anchor read — lines 129-136",
    oldRef: "lock (ctx.Sync) { bool isResolved = ctx.IsResolved; double masterAnchor = ctx.MasterAnchorPrice; }",
    newRef: "AnchorSnapshot snap = ctx.Anchor; bool isResolved = snap.IsResolved; double masterAnchor = snap.MasterAnchorPrice;",
    status: "consistent",
    notes: "Same property mapping as lines 36-42. Consistent.",
  },
  {
    file: "Symmetry.Replace.cs",
    section: "Step 5.2 / D.4",
    description: "Line 127 — SymmetryGuardTryResolveFollowersForDispatch iteration",
    oldRef: "lock (ctx.Sync) { foreach (string fleetEntryName in ctx.FollowerEntries) { ... } }",
    newRef: "string[] snap = ctx.Followers; foreach (string fleetEntryName in snap) { ... }",
    status: "consistent",
    notes: "ctx.Followers returns string[] matching the element type previously in HashSet<string>. foreach works identically. Consistent.",
  },
  {
    file: "Symmetry.Replace.cs",
    section: "Step 5.2 / D.4",
    description: "Line 189 — SymmetryGuardCascadeFollowerCleanup snapshot",
    oldRef: "string[] followers; lock (ctx.Sync) { followers = ctx.FollowerEntries.ToArray(); }",
    newRef: "string[] followers = ctx.Followers;",
    status: "consistent",
    notes: "ctx.Followers is already a string[]; assignment type matches. ToArray() was the only purpose of the lock — eliminated. Consistent.",
  },
  {
    file: "Symmetry.Replace.cs",
    section: "Step 5.2 / D.4",
    description: "Line 224 — SymmetryGuardForgetEntry removal",
    oldRef: "lock (ctx.Sync) ctx.FollowerEntries.Remove(entryName);",
    newRef: "ctx.RemoveFollower(entryName);",
    status: "consistent",
    notes: "RemoveFollower(string) is defined in the new SymmetryDispatchContext (Step 1.1) with matching parameter type. Consistent.",
  },
  {
    file: "Symmetry.Replace.cs",
    section: "Step 5.2 / D.4",
    description: "Line 247 — SymmetryGuardPruneDispatches iteration",
    oldRef: "lock (ctx.Sync) { foreach (string follower in ctx.FollowerEntries) { exists = activePositions.ContainsKey(follower); ... } }",
    newRef: "string[] snap = ctx.Followers; foreach (string follower in snap) { ... }",
    status: "consistent",
    notes: "activePositions is ConcurrentDictionary — already lock-free. Inner call unchanged. Consistent.",
  },
  {
    file: "Orders.Callbacks.AccountOrders.cs",
    section: "Step 5.3 / D.4",
    description: "Line 204 — TryGetDispatchFollowerEntries snapshot",
    oldRef: "lock (ctx.Sync) followerEntries = ctx.FollowerEntries.ToArray();",
    newRef: "followerEntries = ctx.Followers;",
    status: "advisory",
    notes: "ADVISORY: The downstream check is followerEntries.Length > 0. The old code assigned a string[] from ToArray(); the new code assigns a string[] from ctx.Followers. Type is identical. HOWEVER: the document does not explicitly show the declaration type of followerEntries at call site. If it was declared as IEnumerable<string> or similar, the Length property access would break. Based on document evidence (followerEntries.Length > 0 check cited), it must be string[] — consistent. Advisory flagged for engineer to verify declaration type.",
  },
  {
    file: "Orders.Callbacks.AccountOrders.cs",
    section: "Step 5.3 / D.4",
    description: "Line 300 — HandleMatchedFollowerOrder lock removal",
    oldRef: "lock (stateLock) { masterFilled = ...; fsm.State = FollowerReplaceState.Submitting; }",
    newRef: "// No lock. Sequential code identical to old body.",
    status: "consistent",
    notes: "stateLock is retained as a stub field in V12_002.cs (Step 4.1) — compilation remains valid. The lock body logic is preserved verbatim. Consistent.",
  },
  {
    file: "UI.Compliance.cs",
    section: "Step 5.4 / D.4",
    description: "Line 122 — EnsureDailySummaryCsv lock replacement",
    oldRef: "lock (dailySummaryLock) { if (!File.Exists(...)) File.WriteAllText(...); }",
    newRef: "Interlocked.CompareExchange(ref _dailySummaryHeaderEnsured, 1, 0) + try/catch with reset",
    status: "inconsistency",
    notes: "INCONSISTENCY FOUND: The old code was a mutual-exclusion guard that prevented concurrent header writes. The new CAS guard only ensures the header is written once per process lifetime (flag never resets to 0 on success). A test -f equivalent check (File.Exists) is still inside the try block — correct. BUT: the CAS guard returns early if _dailySummaryHeaderEnsured == 1, meaning if a previous call set it to 1 and succeeded, no retry is possible — this is correct for one-shot semantics. However, the flag is reset to 0 only in the catch block. If File.WriteAllText succeeds but the file is later deleted externally, the guard will not re-create the header. The old lock(dailySummaryLock) + File.Exists check would have re-created it. This is a documented semantic change that may be intentional (best-effort) but is not explicitly called out as a behavioral difference. Flag for engineer review.",
  },
  {
    file: "UI.Compliance.cs",
    section: "Step 5.4 / D.4",
    description: "Line 144 — AppendDailySummary lock replacement",
    oldRef: "lock (dailySummaryLock) { EnsureDailySummaryCsv(); File.AppendAllText(...); }",
    newRef: "EnsureDailySummaryCsv(); Task.Run(() => File.AppendAllText(...));",
    status: "advisory",
    notes: "ADVISORY: The new code moves the append to a background Task.Run (fire-and-forget). The old code was synchronous under the lock. This means AppendDailySummary now returns before the file write completes. If the caller checks for file content immediately after the call (e.g., in tests), it will race. The document notes 'daily summary is best-effort' — consistent with intent. Advisory: document this behavioral change explicitly.",
  },
];

// ─────────────────────────────────────────────────────────────────────────────
// SECTION F VERIFICATION DATA
// ─────────────────────────────────────────────────────────────────────────────

type StepStatus = "runnable" | "needs_adjustment" | "dependency_missing";

interface VerificationStep {
  number: number;
  category: string;
  description: string;
  status: StepStatus;
  command?: string;
  issue?: string;
  psEquivalent?: string;
}

const VERIFICATION_STEPS: VerificationStep[] = [
  {
    number: 1,
    category: "Build-time",
    description: "Compile in NinjaTrader 8 (F5 in NinjaScript Editor). Expect zero errors.",
    status: "runnable",
    notes: "Platform-specific to NinjaTrader 8 IDE — not a shell command. No platform adjustment needed for the instruction itself. Runnable on Windows (NT8 runs on Windows only).",
  } as any,
  {
    number: 2,
    category: "Build-time",
    description: "check_ascii.py <files> — expect OK for every file.",
    status: "dependency_missing",
    command: "check_ascii.py src/V12_002.Symmetry.cs src/V12_002.SIMA.cs ...",
    issue: "DEPENDENCY MISSING: check_ascii.py is referenced in Section F but the document does NOT confirm it exists at the repo root. The document only confirms byte_purge.py is absent (mentioned as 'confirmed is absent'). check_ascii.py existence is UNVERIFIABLE from document evidence alone. The document calls it in Step F.2 as if it exists, but no prior section creates or imports it. Engineers must verify the file exists before running this step.",
    psEquivalent: "If check_ascii.py is present, run: python check_ascii.py <files>  (Python must be in PATH on Windows). PowerShell native alternative for ASCII check: Get-Content file.cs | Where-Object { $_ -match '[^\\x00-\\x7F]' } | Select-Object -First 1",
  },
  {
    number: 3,
    category: "Forensic / DNA",
    description: "grep -nE '^[[:space:]]*lock[[:space:]]*\\(' <files> — expect zero matches.",
    status: "needs_adjustment",
    command: "grep -nE '^[[:space:]]*lock[[:space:]]*\\(' src/V12_002.Symmetry.cs ...",
    issue: "POSIX grep with -E and character classes ([[:space:]]) is NOT available natively on Windows PowerShell. The Windows grep (if installed via Git for Windows) may work, but is not guaranteed. POSIX-only command on Windows PowerShell target.",
    psEquivalent: "Select-String -Path src/V12_002.Symmetry.cs,src/V12_002.SIMA.cs,src/V12_002.Orders.Callbacks.Propagation.cs,src/V12_002.Symmetry.Follower.cs,src/V12_002.Symmetry.Replace.cs,src/V12_002.UI.Compliance.cs -Pattern '^\\s*lock\\s*\\(' | Select-Object Path,LineNumber,Line",
  },
  {
    number: 4,
    category: "Forensic / DNA",
    description: "grep -n 'dailySummaryLock' src/ — expect zero matches.",
    status: "needs_adjustment",
    command: "grep -n \"dailySummaryLock\" src/",
    issue: "POSIX grep with directory argument (src/) requires -r flag for recursive search; without it, behavior is platform-dependent. Also not available natively in Windows PowerShell.",
    psEquivalent: "Select-String -Path src/*.cs -Pattern 'dailySummaryLock' -Recurse | Select-Object Path,LineNumber,Line",
  },
  {
    number: 5,
    category: "Forensic / DNA",
    description: "grep -n 'ctx\\.Sync\\|preCheckCtx\\.Sync\\|FollowerEntries' src/ — expect zero matches.",
    status: "needs_adjustment",
    command: "grep -n \"ctx\\.Sync\\|preCheckCtx\\.Sync\\|FollowerEntries\" src/",
    issue: "Same POSIX grep on Windows PowerShell issue. The pipe character | inside the pattern is a grep alternation, not a PowerShell pipe — must be expressed differently in Select-String.",
    psEquivalent: "Select-String -Path src/*.cs -Pattern 'ctx\\.Sync|preCheckCtx\\.Sync|FollowerEntries' -Recurse | Select-Object Path,LineNumber,Line",
  },
  {
    number: 6,
    category: "Forensic / DNA",
    description: "Run 'forensics' subagent (CLAUDE.md P4 Step 2) — confirm zero lock(stateLock) + ASCII compliance.",
    status: "needs_adjustment",
    issue: "Subagent invocation is a CI/tooling step — runnable only if the subagent infrastructure is configured. Platform-neutral in principle, but depends on CLAUDE.md tooling being available in the engineering environment. No PowerShell equivalent needed; flag as environment-dependent.",
    psEquivalent: "N/A — subagent tooling. Manually run steps 3-5 as PowerShell equivalents above.",
  },
  {
    number: 7,
    category: "Forensic / DNA",
    description: "Run 'architect' subagent (/loop-critic) — critique AnchorSnapshot CAS semantics.",
    status: "needs_adjustment",
    issue: "Same as Step 6 — depends on CLAUDE.md subagent tooling. Environment-dependent.",
    psEquivalent: "N/A — subagent tooling. Manual review of CAS loop in Step 1.1 code.",
  },
  {
    number: 8,
    category: "Runtime smoke",
    description: "High-volatility Sim session with EnableSIMA=true and 4-account fleet — trigger OR entry, check SYMMETRY_GUARD logs, anchor propagation, move-sync, cascade-cancel.",
    status: "runnable",
    notes: "NinjaTrader 8 Sim session on Windows. No shell commands involved. Platform-appropriate.",
  } as any,
  {
    number: 9,
    category: "Runtime smoke",
    description: "Concurrent flatten + entry stress — slam IPC with simultaneous FLATTEN and ENTRY commands.",
    status: "runnable",
    notes: "NinjaTrader 8 simulation. Windows-native. No shell dependency.",
  } as any,
  {
    number: 10,
    category: "Runtime smoke",
    description: "REAPER audit cycle — confirm _lastExpectedPositionSetTicks grace window stamps fire after every AddOrUpdate.",
    status: "runnable",
    notes: "Log trace inspection inside NinjaTrader 8 Output window. Platform-appropriate.",
  } as any,
  {
    number: 11,
    category: "Regression fence",
    description: "AnchorSnapshot CAS retry counter — instrument CAS loop in SymmetryGuardOnMasterFill to print on retry. Expect zero retries.",
    status: "runnable",
    notes: "Optional dev-only diagnostic. Code instrumentation, not a shell step. Platform-neutral.",
  } as any,
  {
    number: 12,
    category: "Regression fence",
    description: "Property-test substitute — fire 50 rapid OR entries with 4 followers, diff [SNAPSHOT] debug prints against register/forget log.",
    status: "runnable",
    notes: "Manual simulation exercise in NinjaTrader 8. Platform-appropriate.",
  } as any,
];

// ─────────────────────────────────────────────────────────────────────────────
// UI HELPERS
// ─────────────────────────────────────────────────────────────────────────────

function Badge({ label, color }: { label: string; color: string }) {
  return (
    <span className={`inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-semibold ${color}`}>
      {label}
    </span>
  );
}

function CodeBlock({ code, label }: { code: string; label: string }) {
  return (
    <div className="mt-2">
      <p className="text-xs font-semibold text-slate-500 uppercase tracking-wide mb-1">{label}</p>
      <pre className="bg-slate-900 text-green-300 text-xs rounded-lg p-3 overflow-x-auto whitespace-pre-wrap leading-relaxed">
        {code}
      </pre>
    </div>
  );
}

function SectionHeader({ id, icon, title, subtitle }: { id: string; icon: string; title: string; subtitle: string }) {
  return (
    <div id={id} className="mb-6 border-b border-slate-200 pb-4">
      <div className="flex items-center gap-3 mb-1">
        <span className="text-2xl">{icon}</span>
        <h3 className="text-xl font-bold text-slate-800">{title}</h3>
      </div>
      <p className="text-sm text-slate-500 pl-9">{subtitle}</p>
    </div>
  );
}

// ─────────────────────────────────────────────────────────────────────────────
// SECTION 1: SITE INVENTORY CARDS
// ─────────────────────────────────────────────────────────────────────────────

function SiteInventorySection() {
  const [expanded, setExpanded] = useState<Record<number, boolean>>({});
  const toggle = (n: number) => setExpanded((e) => ({ ...e, [n]: !e[n] }));

  const type1 = SITES.filter((s) => s.classification === "Type 1");
  const type2 = SITES.filter((s) => s.classification === "Type 2");

  return (
    <section className="mb-12">
      <SectionHeader
        id="s1"
        icon="🔍"
        title="Section 1 — Complete Lambda Site Inventory (C.1)"
        subtitle={`All 18 sites from Section C.1. Type 1 = safe pure-work removal. Type 2 = body has post-primary shared-state ops bypassed by naive early-return.`}
      />

      {/* Summary pills */}
      <div className="flex flex-wrap gap-3 mb-6">
        <div className="bg-emerald-50 border border-emerald-200 rounded-xl px-4 py-2 text-sm font-semibold text-emerald-700">
          ✅ Type 1 (pure-work): {type1.length} sites
        </div>
        <div className="bg-amber-50 border border-amber-200 rounded-xl px-4 py-2 text-sm font-semibold text-amber-700">
          ⚠️ Type 2 (cleanup bypass risk): {type2.length} sites
        </div>
        <div className="bg-blue-50 border border-blue-200 rounded-xl px-4 py-2 text-sm font-semibold text-blue-700">
          Total sites: {SITES.length}
        </div>
      </div>

      <div className="grid gap-4">
        {SITES.map((s) => {
          const isOpen = expanded[s.site];
          const isType2 = s.classification === "Type 2";
          const cardBorder = isType2 ? "border-amber-300" : "border-emerald-200";
          const headerBg = isType2 ? "bg-amber-50" : "bg-emerald-50";

          return (
            <div
              key={s.site}
              className={`rounded-xl border-2 ${cardBorder} shadow-sm overflow-hidden`}
            >
              {/* Card header — always visible */}
              <button
                onClick={() => toggle(s.site)}
                className={`w-full text-left ${headerBg} px-4 py-3 flex items-start gap-3 hover:brightness-95 transition-all`}
              >
                {/* Site number */}
                <span className="flex-shrink-0 w-8 h-8 rounded-full bg-slate-800 text-white text-xs font-bold flex items-center justify-center">
                  {s.site}
                </span>

                <div className="flex-1 min-w-0">
                  <div className="flex flex-wrap items-center gap-2 mb-1">
                    <span className="font-mono text-sm font-bold text-slate-800">{s.file}:{s.line}</span>
                    <Badge
                      label={`Transform ${s.transform}`}
                      color={s.transform === "A" ? "bg-blue-100 text-blue-700" : "bg-purple-100 text-purple-700"}
                    />
                    <Badge
                      label={s.classification}
                      color={
                        s.classification === "Type 1"
                          ? "bg-emerald-100 text-emerald-700"
                          : "bg-amber-100 text-amber-700"
                      }
                    />
                    {s.needsPlanUpdate && (
                      <Badge label="⚠ NEEDS PLAN UPDATE" color="bg-red-100 text-red-700" />
                    )}
                    {isType2 && !s.needsPlanUpdate && (
                      <Badge label="Fix documented ✓" color="bg-teal-100 text-teal-700" />
                    )}
                  </div>
                  <p className="text-sm text-slate-600">
                    <span className="font-semibold">{s.method}</span> — {s.description}
                  </p>
                </div>

                <span className="text-slate-400 text-lg flex-shrink-0">{isOpen ? "▲" : "▼"}</span>
              </button>

              {/* Expanded body */}
              {isOpen && (
                <div className="px-4 pb-4 pt-3 bg-white">
                  <div className="mb-3">
                    <p className="text-xs font-semibold uppercase tracking-wide text-slate-500 mb-1">Evidence / Classification Reasoning</p>
                    <p className="text-sm text-slate-700 leading-relaxed">{s.evidenceDetail}</p>
                  </div>

                  {s.confirmedVar && (
                    <div className="mb-3">
                      <p className="text-xs font-semibold uppercase tracking-wide text-slate-500 mb-1">Exact Variable Name Confirmed</p>
                      <code className="bg-slate-100 text-rose-700 text-xs px-2 py-1 rounded font-mono">{s.confirmedVar}</code>
                    </div>
                  )}

                  {s.oldCode && <CodeBlock code={s.oldCode} label="OLD CODE" />}
                  {s.newCode && <CodeBlock code={s.newCode} label="NEW CODE" />}

                  {isType2 && (
                    <div className={`mt-3 rounded-lg p-3 ${s.needsPlanUpdate ? "bg-red-50 border border-red-200" : "bg-teal-50 border border-teal-200"}`}>
                      {s.needsPlanUpdate ? (
                        <p className="text-sm font-semibold text-red-700">🚨 NEEDS PLAN UPDATE BEFORE ENGINEERING BEGINS — no documented fix found for bypass scenario.</p>
                      ) : (
                        <p className="text-sm font-semibold text-teal-700">✅ Type 2 — bypass risk exists but the documented NEW code in Steps 2.x / 5.x correctly handles all post-primary shared-state operations. Plan is adequate; engineer must follow the documented NEW code exactly.</p>
                      )}
                    </div>
                  )}
                </div>
              )}
            </div>
          );
        })}
      </div>
    </section>
  );
}

// ─────────────────────────────────────────────────────────────────────────────
// SECTION 2: PATH SUBSTITUTION
// ─────────────────────────────────────────────────────────────────────────────

function PathSubstitutionSection() {
  return (
    <section className="mb-12">
      <SectionHeader
        id="s2"
        icon="🔄"
        title="Section 2 — Path Substitution Consistency (D.4)"
        subtitle="Every file/line substitution from Section D.4 (Steps 5.1–5.4). OLD | NEW | Verdict."
      />

      {/* Special Q&A panel */}
      <div className="mb-6 grid gap-4 md:grid-cols-2">
        <div className="bg-blue-50 border border-blue-200 rounded-xl p-4">
          <p className="text-xs font-bold uppercase tracking-wide text-blue-600 mb-2">📌 PowerShell Comment Q&A</p>
          <p className="text-sm font-semibold text-slate-800 mb-2">Does a function call inside a PowerShell comment get evaluated at runtime?</p>
          <p className="text-sm text-slate-700 leading-relaxed">
            <span className="font-bold text-emerald-700">No.</span> In PowerShell, anything after a <code className="bg-slate-100 px-1 rounded text-xs">#</code> on the same line is a comment and is completely ignored by the parser at runtime. A function call written as <code className="bg-slate-100 px-1 rounded text-xs"># $(SomeCall)</code> or <code className="bg-slate-100 px-1 rounded text-xs"># SomeFunction()</code> is <em>never</em> evaluated — it is dead text. A developer reading the comment sees the literal string exactly as written. If the plan uses a placeholder like <code className="bg-slate-100 px-1 rounded text-xs"># GetPath()</code> in a comment to indicate what the value was, the developer reads that literal text — not the result of any function.
          </p>
        </div>
        <div className="bg-purple-50 border border-purple-200 rounded-xl p-4">
          <p className="text-xs font-bold uppercase tracking-wide text-purple-600 mb-2">📌 MSBuild HintPath Condition Q&A</p>
          <p className="text-sm font-semibold text-slate-800 mb-2">Does an MSBuild HintPath need a Condition attribute when the target software may be absent?</p>
          <p className="text-sm text-slate-700 leading-relaxed">
            <span className="font-bold text-amber-700">Yes — this is a moderate risk.</span> An MSBuild <code className="bg-slate-100 px-1 rounded text-xs">&lt;HintPath&gt;</code> inside a <code className="bg-slate-100 px-1 rounded text-xs">&lt;Reference&gt;</code> element is only a hint to the resolver. If the path does not exist, MSBuild will fall back to GAC/assembly name lookup — it does NOT error out solely due to a missing HintPath. <span className="font-bold">However</span>, if the assembly itself cannot be resolved at all (not in GAC, not at HintPath), the build <em>will</em> fail with a missing reference error. A <code className="bg-slate-100 px-1 rounded text-xs">Condition</code> attribute on the <code className="bg-slate-100 px-1 rounded text-xs">&lt;Reference&gt;</code> element (not HintPath) is the correct approach to make the entire reference optional when the target software (e.g., NinjaTrader 8 assemblies) may be absent on the build agent. Document does not show .csproj HintPath changes explicitly — advisory noted.
          </p>
        </div>
      </div>

      <div className="overflow-x-auto rounded-xl border border-slate-200 shadow-sm">
        <table className="min-w-full text-sm">
          <thead className="bg-slate-100 text-slate-600 text-xs uppercase tracking-wide">
            <tr>
              <th className="px-4 py-3 text-left">File / Section</th>
              <th className="px-4 py-3 text-left">Description</th>
              <th className="px-4 py-3 text-left">OLD</th>
              <th className="px-4 py-3 text-left">NEW</th>
              <th className="px-4 py-3 text-left">Verdict</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-100">
            {PATH_CHANGES.map((p, i) => {
              const statusColor =
                p.status === "consistent"
                  ? "bg-emerald-50"
                  : p.status === "inconsistency"
                  ? "bg-red-50"
                  : "bg-amber-50";
              const badge =
                p.status === "consistent"
                  ? { label: "✅ Consistent", color: "bg-emerald-100 text-emerald-700" }
                  : p.status === "inconsistency"
                  ? { label: "🔴 Inconsistency", color: "bg-red-100 text-red-700" }
                  : { label: "🟡 Advisory", color: "bg-amber-100 text-amber-700" };

              return (
                <tr key={i} className={statusColor}>
                  <td className="px-4 py-3 font-mono text-xs text-slate-700 align-top">
                    <p className="font-bold">{p.file}</p>
                    <p className="text-slate-500">{p.section}</p>
                  </td>
                  <td className="px-4 py-3 text-xs text-slate-700 align-top max-w-xs">
                    <p className="mb-2">{p.description}</p>
                    <p className="text-slate-500 leading-relaxed">{p.notes}</p>
                  </td>
                  <td className="px-4 py-3 align-top">
                    <code className="block bg-slate-900 text-red-300 text-xs p-2 rounded max-w-xs overflow-x-auto whitespace-pre-wrap">
                      {p.oldRef}
                    </code>
                  </td>
                  <td className="px-4 py-3 align-top">
                    <code className="block bg-slate-900 text-green-300 text-xs p-2 rounded max-w-xs overflow-x-auto whitespace-pre-wrap">
                      {p.newRef}
                    </code>
                  </td>
                  <td className="px-4 py-3 align-top">
                    <Badge label={badge.label} color={badge.color} />
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
    </section>
  );
}

// ─────────────────────────────────────────────────────────────────────────────
// SECTION 3: VERIFICATION STEPS
// ─────────────────────────────────────────────────────────────────────────────

function VerificationSection() {
  const [expanded, setExpanded] = useState<Record<number, boolean>>({});
  const toggle = (n: number) => setExpanded((e) => ({ ...e, [n]: !e[n] }));

  const categories = ["Build-time", "Forensic / DNA", "Runtime smoke", "Regression fence"];

  return (
    <section className="mb-12">
      <SectionHeader
        id="s3"
        icon="🧪"
        title="Section 3 — Verification Step Platform Check (Section F)"
        subtitle="All 12 verification steps. Status: runnable / needs platform adjustment / dependency missing. PowerShell equivalents provided for every POSIX-only command."
      />

      {/* check_ascii.py callout */}
      <div className="mb-6 bg-red-50 border-2 border-red-300 rounded-xl p-4">
        <p className="text-sm font-bold text-red-700 mb-1">⚠️ check_ascii.py Existence — UNVERIFIABLE</p>
        <p className="text-sm text-slate-700 leading-relaxed">
          The document calls <code className="bg-slate-100 px-1 rounded text-xs">check_ascii.py</code> in Step F.2 but does NOT confirm it exists at the repo root. The only Python script mentioned as confirmed-absent is <code className="bg-slate-100 px-1 rounded text-xs">byte_purge.py</code>. There is no statement in the document that <code className="bg-slate-100 px-1 rounded text-xs">check_ascii.py</code> was verified to exist — its presence is assumed by the verification plan. Engineers must verify the file exists before running Step F.2.
        </p>
      </div>

      {categories.map((cat) => {
        const steps = VERIFICATION_STEPS.filter((s) => s.category === cat);
        return (
          <div key={cat} className="mb-6">
            <h4 className="text-sm font-bold uppercase tracking-wide text-slate-600 mb-3 flex items-center gap-2">
              <span className="w-2 h-2 rounded-full bg-slate-400 inline-block"></span>
              {cat}
            </h4>
            <div className="grid gap-3">
              {steps.map((step) => {
                const isOpen = expanded[step.number];
                const statusConfig = {
                  runnable: { color: "border-emerald-200 bg-emerald-50", badge: "bg-emerald-100 text-emerald-700", label: "✅ Runnable" },
                  needs_adjustment: { color: "border-amber-200 bg-amber-50", badge: "bg-amber-100 text-amber-700", label: "🔧 Needs Platform Adjustment" },
                  dependency_missing: { color: "border-red-200 bg-red-50", badge: "bg-red-100 text-red-700", label: "❌ Dependency Missing" },
                }[step.status];

                return (
                  <div key={step.number} className={`rounded-xl border-2 ${statusConfig.color} overflow-hidden shadow-sm`}>
                    <button
                      onClick={() => toggle(step.number)}
                      className="w-full text-left px-4 py-3 flex items-start gap-3 hover:brightness-95 transition-all"
                    >
                      <span className="flex-shrink-0 w-7 h-7 rounded-full bg-slate-700 text-white text-xs font-bold flex items-center justify-center">
                        {step.number}
                      </span>
                      <div className="flex-1 min-w-0">
                        <div className="flex flex-wrap items-center gap-2 mb-1">
                          <Badge label={statusConfig.label} color={statusConfig.badge} />
                        </div>
                        <p className="text-sm text-slate-700">{step.description}</p>
                      </div>
                      <span className="text-slate-400 text-sm flex-shrink-0">{isOpen ? "▲" : "▼"}</span>
                    </button>

                    {isOpen && (
                      <div className="px-4 pb-4 pt-2 bg-white border-t border-slate-200">
                        {(step as any).notes && (
                          <p className="text-sm text-slate-600 mb-2">{(step as any).notes}</p>
                        )}
                        {step.issue && (
                          <div className="mb-3">
                            <p className="text-xs font-semibold uppercase tracking-wide text-slate-500 mb-1">Issue / Flag</p>
                            <p className="text-sm text-slate-700 leading-relaxed">{step.issue}</p>
                          </div>
                        )}
                        {step.command && (
                          <CodeBlock code={step.command} label="Original Command (POSIX / grep)" />
                        )}
                        {step.psEquivalent && step.psEquivalent !== "N/A — subagent tooling." && (
                          <CodeBlock code={step.psEquivalent} label="PowerShell Native Equivalent" />
                        )}
                        {step.psEquivalent === "N/A — subagent tooling." && (
                          <p className="text-xs text-slate-500 mt-2 italic">PowerShell equivalent: N/A — subagent tooling; see manual alternatives above.</p>
                        )}
                      </div>
                    )}
                  </div>
                );
              })}
            </div>
          </div>
        );
      })}
    </section>
  );
}

// ─────────────────────────────────────────────────────────────────────────────
// SECTION 4: OVERALL SUMMARY
// ─────────────────────────────────────────────────────────────────────────────

function SummarySection() {
  return (
    <section className="mb-12">
      <SectionHeader
        id="s4"
        icon="📋"
        title="Section 4 — Overall Summary"
        subtitle="All items grouped by severity. Final readiness verdict."
      />

      {/* Verdict banner */}
      <div className="mb-6 rounded-xl bg-emerald-600 text-white p-5 shadow-lg">
        <p className="text-xl font-bold mb-1">✅ PLAN IS CONDITIONALLY READY FOR ENGINEERING</p>
        <p className="text-sm opacity-90 leading-relaxed">
          No Blocking items found. All Type 2 sites have documented fixes in the plan's NEW code blocks (Steps 2.1, 2.3, 2.4, 5.4). Three Moderate items and several Advisory items must be addressed or acknowledged before the first commit.
        </p>
      </div>

      {/* Blocking */}
      <div className="mb-5 rounded-xl border-2 border-red-200 bg-red-50 p-4">
        <p className="text-sm font-bold text-red-700 uppercase tracking-wide mb-3">🚨 Blocking — Type 2 Sites Without a Documented Fix</p>
        <p className="text-sm text-slate-600 italic">
          None. All four Type 2 sites (Sites 12, 14, 15, 17) have explicit NEW code in the plan that correctly handles post-primary shared-state operations. No blocking items.
        </p>
      </div>

      {/* Moderate */}
      <div className="mb-5 rounded-xl border-2 border-amber-200 bg-amber-50 p-4">
        <p className="text-sm font-bold text-amber-700 uppercase tracking-wide mb-3">⚠️ Moderate — Path Consistency Issues &amp; Missing Conditions</p>
        <ul className="space-y-3 text-sm text-slate-700">
          <li className="flex gap-2">
            <span className="font-bold text-amber-600 flex-shrink-0">[M-01]</span>
            <span>
              <strong>Section D.4 / Step 5.4 — EnsureDailySummaryCsv semantic change (one-shot vs. re-entrant guard):</strong> The new CAS guard (_dailySummaryHeaderEnsured) is one-shot and will not re-create the CSV header if the file is deleted externally after first creation. The old lock + File.Exists check was re-entrant. This behavioral difference is not documented as intentional in the plan. Engineer must explicitly acknowledge this as acceptable (best-effort) before committing.
            </span>
          </li>
          <li className="flex gap-2">
            <span className="font-bold text-amber-600 flex-shrink-0">[M-02]</span>
            <span>
              <strong>Section D.4 / Step 5.3, line 204 — followerEntries type ambiguity:</strong> The document cites followerEntries.Length &gt; 0 check but does not show the variable declaration. If declared as IEnumerable&lt;string&gt;, the Length property will not compile after substitution to string[]. Engineer must verify the declaration type at call site before assuming the substitution compiles.
            </span>
          </li>
          <li className="flex gap-2">
            <span className="font-bold text-amber-600 flex-shrink-0">[M-03]</span>
            <span>
              <strong>Section F / Step F.2 — check_ascii.py existence unverified:</strong> The document calls this script but never confirms it exists at the repo root. byte_purge.py is confirmed absent, but check_ascii.py status is unknown from document evidence. If it does not exist, Step F.2 will silently fail or error. The repo must be inspected before this verification step is relied upon.
            </span>
          </li>
        </ul>
      </div>

      {/* Advisory */}
      <div className="mb-5 rounded-xl border-2 border-blue-200 bg-blue-50 p-4">
        <p className="text-sm font-bold text-blue-700 uppercase tracking-wide mb-3">💡 Advisory — Platform Notes &amp; Comment Wording</p>
        <ul className="space-y-3 text-sm text-slate-700">
          <li className="flex gap-2">
            <span className="font-bold text-blue-600 flex-shrink-0">[A-01]</span>
            <span>
              <strong>Section F Steps 3, 4, 5 — grep / POSIX commands on Windows PowerShell:</strong> All three forensic grep commands are POSIX-syntax and will not run natively in Windows PowerShell. PowerShell Select-String equivalents are provided in Section 3 of this review. Plan should replace or supplement these with PowerShell-native commands for Windows developer machines.
            </span>
          </li>
          <li className="flex gap-2">
            <span className="font-bold text-blue-600 flex-shrink-0">[A-02]</span>
            <span>
              <strong>Section F Steps 6, 7 — subagent tooling dependency:</strong> "forensics" and "architect" subagents (per CLAUDE.md) are environment-dependent. If the engineering environment lacks this tooling, these steps are skipped silently. Plan should note the fallback (manual Select-String equivalents from steps 3-5).
            </span>
          </li>
          <li className="flex gap-2">
            <span className="font-bold text-blue-600 flex-shrink-0">[A-03]</span>
            <span>
              <strong>Section D.4 / Step 5.4 — AppendDailySummary now fire-and-forget:</strong> Moving File.AppendAllText into Task.Run changes the method from synchronous to async fire-and-forget. Any test or caller that reads the file immediately after calling AppendDailySummary will race. Document this behavioral change in the API comment.
            </span>
          </li>
          <li className="flex gap-2">
            <span className="font-bold text-blue-600 flex-shrink-0">[A-04]</span>
            <span>
              <strong>Section C.1 / Site 16 — stateLock stub retention comment:</strong> The plan's Step 4.1 comment says stateLock is retained because "22 out-of-scope partial files still reference it; scheduled for removal in the next migration phase." This is clear and adequate, but the plan should include a follow-up ADR or ticket reference for the next phase to ensure the stub does not persist indefinitely.
            </span>
          </li>
          <li className="flex gap-2">
            <span className="font-bold text-blue-600 flex-shrink-0">[A-05]</span>
            <span>
              <strong>MSBuild HintPath / Condition attribute:</strong> If any .csproj HintPath changes are part of the cascade migration (not explicitly listed in D.4 but implied by NinjaTrader 8 assembly references), each Reference pointing to a path that may be absent on a CI agent should carry a Condition attribute on the Reference element (not HintPath) to avoid build failures on machines without NinjaTrader 8 installed.
            </span>
          </li>
        </ul>
      </div>

      {/* Exact items needing plan update */}
      <div className="rounded-xl border-2 border-slate-200 bg-slate-50 p-4">
        <p className="text-sm font-bold text-slate-700 uppercase tracking-wide mb-3">📝 Exact Items Requiring Plan Update Before Engineering Begins</p>
        <ol className="space-y-2 text-sm text-slate-700 list-decimal list-inside">
          <li><strong>[M-01]</strong> Add explicit note in Step 5.4 that EnsureDailySummaryCsv is intentionally one-shot and will not re-create a deleted CSV header.</li>
          <li><strong>[M-02]</strong> Show the followerEntries variable declaration in Step 5.3 to confirm string[] compatibility with Length check.</li>
          <li><strong>[M-03]</strong> Confirm check_ascii.py exists at repo root (or provide creation steps / alternative ASCII audit command).</li>
          <li><strong>[A-01]</strong> Replace or supplement grep commands in Steps F.3–F.5 with PowerShell Select-String equivalents for Windows targets.</li>
          <li><strong>[A-03]</strong> Add a behavioral-change note to AppendDailySummary API comment documenting the shift to fire-and-forget.</li>
          <li><strong>[A-04]</strong> Link a follow-up ticket or ADR for stateLock stub removal from the 22 out-of-scope partial files.</li>
        </ol>
      </div>
    </section>
  );
}

// ─────────────────────────────────────────────────────────────────────────────
// NAV
// ─────────────────────────────────────────────────────────────────────────────

const NAV_ITEMS = [
  { href: "#s1", label: "1 · Lambda Inventory" },
  { href: "#s2", label: "2 · Path Substitution" },
  { href: "#s3", label: "3 · Verification" },
  { href: "#s4", label: "4 · Summary" },
];

// ─────────────────────────────────────────────────────────────────────────────
// ROOT APP
// ─────────────────────────────────────────────────────────────────────────────

function App() {
  return (
    <div className="min-h-screen bg-slate-50 font-sans">
      {/* Top nav */}
      <nav className="sticky top-0 z-40 bg-slate-900 text-white shadow-lg">
        <div className="max-w-7xl mx-auto px-4 py-3 flex flex-wrap items-center justify-between gap-3">
          <div className="flex items-center gap-3">
            <span className="text-xl">🏛️</span>
            <div>
              <p className="text-xs text-slate-400 font-mono">ADR-019 · BUILD_TAG DELTA</p>
              <p className="text-xs font-bold text-amber-400 font-mono">{BUILD_TAG_DELTA}</p>
            </div>
          </div>
          <div className="flex flex-wrap gap-2">
            {NAV_ITEMS.map((n) => (
              <a
                key={n.href}
                href={n.href}
                className="text-xs px-3 py-1.5 rounded-lg bg-slate-700 hover:bg-slate-600 transition-colors text-slate-200"
              >
                {n.label}
              </a>
            ))}
          </div>
        </div>
      </nav>

      {/* Page header */}
      <header className="bg-gradient-to-br from-slate-900 via-slate-800 to-indigo-900 text-white py-12 px-4">
        <div className="max-w-7xl mx-auto">
          <div className="flex items-start gap-4 mb-6">
            <div className="bg-indigo-500/20 border border-indigo-400/30 rounded-2xl px-4 py-2">
              <p className="text-xs text-indigo-300 font-semibold uppercase tracking-widest mb-0.5">Reviewer</p>
              {/* MODEL NAME AND VERSION — appears in visible h2 per task requirement */}
              <h2 className="text-lg font-bold text-white">Claude claude-opus-4-5</h2>
            </div>
            <div className="bg-amber-500/20 border border-amber-400/30 rounded-2xl px-4 py-2">
              <p className="text-xs text-amber-300 font-semibold uppercase tracking-widest mb-0.5">Build Tag Delta</p>
              <p className="text-lg font-bold text-amber-200 font-mono">{BUILD_TAG_DELTA}</p>
            </div>
          </div>

          <h1 className="text-3xl md:text-4xl font-extrabold tracking-tight mb-3">
            ADR-019 Sovereign Substrate — Design Review Dashboard
          </h1>
          <p className="text-slate-300 text-base max-w-3xl leading-relaxed">
            Senior architect pre-engineering consistency review of{" "}
            <span className="font-mono text-amber-300">implementation_plan.md</span>. Covers
            all 18 lambda sites (Section C.1), all path substitutions (Section D.4), all
            verification steps (Section F), and final readiness verdict.
          </p>

          <div className="mt-6 flex flex-wrap gap-3">
            <div className="bg-white/10 rounded-lg px-3 py-2 text-sm">
              <span className="text-slate-300">Sites reviewed:</span>{" "}
              <span className="font-bold text-white">18</span>
            </div>
            <div className="bg-white/10 rounded-lg px-3 py-2 text-sm">
              <span className="text-slate-300">Type 2 sites:</span>{" "}
              <span className="font-bold text-amber-300">4</span>
            </div>
            <div className="bg-white/10 rounded-lg px-3 py-2 text-sm">
              <span className="text-slate-300">Blocking items:</span>{" "}
              <span className="font-bold text-emerald-300">0</span>
            </div>
            <div className="bg-white/10 rounded-lg px-3 py-2 text-sm">
              <span className="text-slate-300">Moderate items:</span>{" "}
              <span className="font-bold text-amber-300">3</span>
            </div>
            <div className="bg-white/10 rounded-lg px-3 py-2 text-sm">
              <span className="text-slate-300">Verification steps:</span>{" "}
              <span className="font-bold text-white">12</span>
            </div>
            <div className="bg-white/10 rounded-lg px-3 py-2 text-sm">
              <span className="text-slate-300">POSIX-flagged steps:</span>{" "}
              <span className="font-bold text-rose-300">3</span>
            </div>
          </div>
        </div>
      </header>

      {/* Main content */}
      <main className="max-w-7xl mx-auto px-4 py-10">
        <SiteInventorySection />
        <PathSubstitutionSection />
        <VerificationSection />
        <SummarySection />
      </main>

      <footer className="border-t border-slate-200 bg-white py-6 px-4 text-center text-xs text-slate-400">
        <p>ADR-019 Design Review · Reviewer: <strong className="text-slate-600">Claude claude-opus-4-5</strong> · All findings derived exclusively from the fetched implementation_plan.md</p>
        <p className="mt-1">Build Tag Delta: <code className="font-mono text-slate-600">{BUILD_TAG_DELTA}</code></p>
      </footer>
    </div>
  );
}
