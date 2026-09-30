# Wave 9 Debt Register

**Purpose**: Authoritative list of all known Jane Street OKF violations and structural debt
remaining after Wave 7 (CYC reduction) and Wave 8 (deferred OKF repair).
**Written**: 2026-07-05 live scan of src/ after Wave 8 commit b5b4bb84.
**Format**: Append-only. Mark resolved: wave9 {commit_sha} when fixed.

---

## Lane Map

| Lane | Class | Count | Category | OKF Rule |
|------|-------|-------|----------|----------|
| L1 | A | 18 | DateTime.Now remnants | Rule 3 -- FSM Determinism |
| L2 | A | 12 | Account.All missing .ToArray() | Rule 5 -- Production Safety |
| L3 | A | 10 | Silent empty catch {} | Rule 5 -- Production Safety |
| L4 | A | 35 | LINQ in production code | Rule 7 -- Hot Path Performance |
| L5 | B | 223 | Magic numbers (JS-100) | Rule 6 -- Naming/Constants |
| L6 | B | 12 | Exceptions in hot paths (JS-001) | Rule 5 -- Production Safety |
| L7 | B | 21 | LOC > 80 methods | Rule 6 -- Complexity/Readability |
| L8 | B | 4 | M5 dispatch candidates | Rule 6 -- FSM Decomposition |

**Total entries**: ~335 (L5 is batched by file, not individual line)

---

## L1 -- DateTime.Now Violations (18 occurrences, Rule 3)

Fix: replace `DateTime.Now` with `DateTime.UtcNow`.
Exception: if feeding NT8 API that requires local time, use `DateTime.UtcNow.ToLocalTime()` + comment.

| ID | File | Line | Code | Priority |
|----|------|------|------|----------|
| W9-L1-001 | src/SignalBroadcaster.cs | 289 | `signal.Timestamp = DateTime.Now;` | P1 | resolved: wave9 dfc263fe |
| W9-L1-002 | src/SignalBroadcaster.cs | 306 | `update.Timestamp = DateTime.Now;` | P1 | resolved: wave9 dfc263fe |
| W9-L1-003 | src/SignalBroadcaster.cs | 321 | `action.Timestamp = DateTime.Now;` | P1 | resolved: wave9 dfc263fe |
| W9-L1-004 | src/SignalBroadcaster.cs | 330 | `Timestamp = DateTime.Now` (FlattenSignal) | P1 | resolved: wave9 dfc263fe |
| W9-L1-005 | src/SignalBroadcaster.cs | 340 | `Timestamp = DateTime.Now` (BreakevenSignal) | P1 | resolved: wave9 dfc263fe |
| W9-L1-006 | src/SignalBroadcaster.cs | 355 | `Timestamp = DateTime.Now,` | P1 | resolved: wave9 dfc263fe |
| W9-L1-007 | src/SignalBroadcaster.cs | 370 | `Timestamp = DateTime.Now,` | P1 | resolved: wave9 dfc263fe |
| W9-L1-008 | src/SignalBroadcaster.cs | 385 | `Timestamp = DateTime.Now,` | P1 | resolved: wave9 dfc263fe |
| W9-L1-009 | src/SignalBroadcaster.cs | 400 | `Timestamp = DateTime.Now,` | P1 | resolved: wave9 dfc263fe |
| W9-L1-010 | src/V12_002.Entries.RMA.cs | 107 | `DateTime.Now.ToString("HHmmssffff")` | P1 | resolved: wave9 dfc263fe |
| W9-L1-011 | src/V12_002.LogicAudit.cs | 482 | `DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")` | P2 (logging only) | resolved: wave9 dfc263fe |
| W9-L1-012 | src/V12_002.SIMA.Execution.cs | 360 | `DateTime.Now.Ticks` (ocoId suffix) | P1 | resolved: wave9 dfc263fe |
| W9-L1-013 | src/V12_002.SIMA.Execution.cs | 992 | `DateTime.Now.Ticks` (baseSignal suffix) | P1 | resolved: wave9 dfc263fe |
| W9-L1-014 | src/V12_002.Trailing.cs | 215 | `DateTime now = DateTime.Now;` | P1 | resolved: wave9 dfc263fe |
| W9-L1-015 | src/V12_002.UI.Compliance.cs | 49 | `ConvertToSelectedTimeZone(DateTime.Now)` | P2 (UI display) | resolved: wave9 dfc263fe |
| W9-L1-016 | src/V12_002.UI.Compliance.cs | 893 | `DateTime.Now.ToString(...)` (JSON export) | P2 (reporting) | resolved: wave9 dfc263fe |
| W9-L1-017 | src/V12_002.UI.Sizing.cs | 130 | `DateTime.Now - _lastSyncFailureTime` | P1 (timing logic) | resolved: wave9 dfc263fe |
| W9-L1-018 | src/V12_002.UI.Sizing.cs | 314 | `_lastSyncFailureTime = DateTime.Now;` | P1 (timing logic) | resolved: wave9 dfc263fe |

---

## L2 -- Account.All Missing .ToArray() (12 occurrences, Rule 5)

Fix: `foreach (Account acct in Account.All)` -> `foreach (Account acct in Account.All.ToArray())`
or: `Account.All.FirstOrDefault(...)` -> `Account.All.ToArray().FirstOrDefault(...)`

| ID | File | Line | Code | Priority |
|----|------|------|------|----------|
| W9-L2-001 | src/V12_002.Orders.Callbacks.Propagation.cs | 741 | `Account.All.FirstOrDefault(a =>` | P2 | resolved: wave9 42683dc7 |
| W9-L2-002 | src/V12_002.Orders.Management.Cleanup.cs | 629 | `foreach (Account acct in Account.All)` | P2 | resolved: wave9 42683dc7 |
| W9-L2-003 | src/V12_002.REAPER.NakedStop.cs | 27 | `Account.All.FirstOrDefault(a => a.Name ==` | P2 | resolved: wave9 42683dc7 |
| W9-L2-004 | src/V12_002.SIMA.cs | 221 | `foreach (Account acct in Account.All)` | P2 | resolved: wave9 42683dc7 |
| W9-L2-005 | src/V12_002.SIMA.Execution.cs | 60 | `foreach (Account acct in Account.All)` | P2 | resolved: wave9 42683dc7 |
| W9-L2-006 | src/V12_002.SIMA.Execution.cs | 252 | `foreach (Account acct in Account.All)` | P2 | resolved: wave9 42683dc7 |
| W9-L2-007 | src/V12_002.SIMA.Execution.cs | 1064 | `foreach (Account acct in Account.All)` | P2 | resolved: wave9 42683dc7 |
| W9-L2-008 | src/V12_002.UI.Compliance.cs | 303 | `foreach (Account acct in Account.All)` | P2 | resolved: wave9 42683dc7 |
| W9-L2-009 | src/V12_002.UI.Compliance.cs | 347 | `Account.All.FirstOrDefault(a => a.Name ==` | P2 | resolved: wave9 42683dc7 |
| W9-L2-010 | src/V12_002.UI.IPC.Commands.Fleet.cs | 334 | `foreach (Account acct in Account.All)` | P2 | resolved: wave9 42683dc7 |
| W9-L2-011 | src/V12_002.UI.IPC.Commands.Fleet.cs | 409 | `foreach (Account acct in Account.All)` | P2 | resolved: wave9 42683dc7 |
| W9-L2-012 | src/V12_002.UI.IPC.Commands.Misc.cs | 141 | `foreach (Account acct in Account.All)` | P2 | resolved: wave9 42683dc7 |

---

## L3 -- Silent Empty catch {} (10 occurrences, Rule 5)

Fix: `catch { }` -> `catch (Exception ex) { NinjaTrader.Code.Output.Process("Error {method}: " + ex.Message, PrintTo.OutputTab1); }`

| ID | File | Line | Context | Priority |
|----|------|------|---------|----------|
| W9-L3-001 | src/V12_002.cs | ~TBD | Silent catch in V12_002.cs (scan required) | P2 | resolved: wave9 4c978b88
| W9-L3-002 | src/V12_002.cs | ~TBD | Second silent catch in V12_002.cs | P2 | resolved: wave9 b0626eac
| W9-L3-003 | src/V12_002.Lifecycle.cs | ~TBD | Silent catch #1 (scan required) | P2 | resolved: wave9 414ebdbe
| W9-L3-004 | src/V12_002.Lifecycle.cs | ~TBD | Silent catch #2 | P2 | resolved: wave9 89994413
| W9-L3-005 | src/V12_002.Lifecycle.cs | ~TBD | Silent catch #3 | P2 | resolved: wave9 92520a65
| W9-L3-006 | src/V12_002.Lifecycle.cs | ~TBD | Silent catch #4 | P2 | resolved: wave9 6205dc6d
| W9-L3-007 | src/V12_002.SIMA.Lifecycle.cs | ~TBD | Silent catch #1 (scan required) | P2 | resolved: wave9 f35286c3
| W9-L3-008 | src/V12_002.SIMA.Lifecycle.cs | ~TBD | Silent catch #2 | P2 | resolved: wave9 f35286c3
| W9-L3-009 | src/V12_002.SIMA.Lifecycle.cs | ~TBD | Silent catch #3 | P2 | resolved: wave9 f35286c3
| W9-L3-010 | src/V12_002.SIMA.Lifecycle.cs | ~TBD | Silent catch #4 | P2 | resolved: wave9 f35286c3

*Note: wave9-scan must confirm exact line numbers -- pattern detection requires context check.*

---

## L4 -- LINQ in Production Code (35 occurrences, Rule 7)

Fix strategy (per occurrence):
- `collection.Any(pred)` in hot path -> explicit `for` loop with early return
- `collection.Where(...).ToList()` in hot path -> pre-allocated list + foreach
- `collection.FirstOrDefault(pred)` -> explicit loop or `TryGetValue` if Dictionary
- `.OrderBy(...)` -> pre-sorted structure or sort once at init
- Non-hot-path LINQ (setup/init) -> leave as-is, add comment: // not hot path

Priority: hot-path LINQ (OnBarUpdate, OnExecutionUpdate, OnOrderUpdate, Dispatch*) = P1.
Non-hot-path LINQ (UI, audit, compliance, init) = P3.

| ID | File | Line | LINQ Call | Hot Path? | Priority |
|----|------|------|-----------|-----------|----------|
| W9-L4-001 | src/V12_002.MetadataGuard.cs | 168 | `.Values.Any(f =>` | No | P3 | resolved: wave9 6cc3b5e9 |
| W9-L4-002 | src/V12_002.Orders.Callbacks.AccountOrders.cs | 544 | `.Values.Any(f =>` | Yes | P1 | resolved: wave9 081e9e04 |
| W9-L4-003 | src/V12_002.Orders.Callbacks.AccountOrders.cs | 962 | `.Where(kvp =>` | Yes | P1 | resolved: wave9 fba6aee8 |
| W9-L4-004 | src/V12_002.Orders.Callbacks.AccountOrders.cs | 971 | `.Select(kvp => kvp.Key)` | Yes | P1 | resolved: wave9 fba6aee8 |
| W9-L4-005 | src/V12_002.Orders.Callbacks.Propagation.cs | 741 | `.FirstOrDefault(a =>` | No | P3 | resolved: wave9 fdcad047 |
| W9-L4-006 | src/V12_002.Orders.Management.Cleanup.cs | 275 | `.Positions.FirstOrDefault(p =>` | No | P3 | resolved: wave9 16e0e15a |
| W9-L4-007 | src/V12_002.Orders.Management.StopSync.cs | 129 | `activePositions.ToList()` | Yes | P1 | resolved: wave9 91bfd507 |
| W9-L4-008 | src/V12_002.PureLogic.cs | 58 | `buckets.Sum()` | No | P3 | resolved: wave9 75549a1c |
| W9-L4-009 | src/V12_002.REAPER.Audit.cs | 415 | `.Positions.FirstOrDefault(p =>` | No | P3 | resolved: wave9 d49af8a8 |
| W9-L4-010 | src/V12_002.REAPER.Audit.cs | 419 | `.Values.Where(f =>).ToList()` | No | P3 | resolved: wave9 d49af8a8 |
| W9-L4-011 | src/V12_002.REAPER.Audit.cs | 517 | `.Any(f =>` | No | P3 | resolved: wave9 d49af8a8 |
| W9-L4-012 | src/V12_002.REAPER.Audit.cs | 557 | `.Any(o =>` | No | P3 | resolved: wave9 d49af8a8 |
| W9-L4-013 | src/V12_002.REAPER.Audit.cs | 605 | `.Positions.FirstOrDefault(p =>` | No | P3 | resolved: wave9 d49af8a8 |
| W9-L4-014 | src/V12_002.REAPER.Audit.cs | 746 | `.Any(o =>` | No | P3 | resolved: wave9 d49af8a8 |
| W9-L4-015 | src/V12_002.REAPER.NakedStop.cs | 27 | `.FirstOrDefault(a =>` | No | P3 | resolved: wave9 16e0e15a |
| W9-L4-016 | src/V12_002.REAPER.Repair.cs | 150 | `.Values.Any(f =>` | No | P3 | resolved: wave9 1b70f9a4 |
| W9-L4-017 | src/V12_002.REAPER.Repair.cs | 172 | `.Values.Any(p =>` | No | P3 | resolved: wave9 1b70f9a4 |
| W9-L4-018 | src/V12_002.SIMA.cs | 238 | `.OrderBy(a => a.DailyPL).ToList()` | No | P3 | resolved: wave9 1b70f9a4 |
| W9-L4-019 | src/V12_002.SIMA.Flatten.cs | 501 | `.FirstOrDefault(p =>` | No | P3 | resolved: wave9 1b70f9a4 |
| W9-L4-020 | src/V12_002.SIMA.Lifecycle.cs | 631 | `.FirstOrDefault(p =>` | No | P3 | resolved: wave9 c7c5f45e |
| W9-L4-021 | src/V12_002.SIMA.Lifecycle.cs | 699 | `.Values.Any(f =>` | Yes | P1 | resolved: wave9 48eb89b0 |
| W9-L4-022 | src/V12_002.SIMA.Lifecycle.cs | 708 | `.FirstOrDefault(p =>` | Yes | P1 | resolved: wave9 48eb89b0 |
| W9-L4-023 | src/V12_002.Symmetry.cs | 240 | `.Where(kvp =>` | Yes | P1 | resolved: wave9 48eb89b0 |
| W9-L4-024 | src/V12_002.Symmetry.cs | 241 | `.Select(kvp => kvp.Key)` | Yes | P1 | resolved: wave9 48eb89b0 |
| W9-L4-025 | src/V12_002.Symmetry.cs | 242 | `.ToList()` | Yes | P1 | resolved: wave9 48eb89b0 |
| W9-L4-026 | src/V12_002.UI.Compliance.cs | 347 | `.FirstOrDefault(a =>` | No | P3 | resolved: wave9 1f479056 |
| W9-L4-027 | src/V12_002.UI.Compliance.cs | 839 | `.Positions.FirstOrDefault(p =>` | No | P3 | resolved: wave9 1f479056 |
| W9-L4-028 | src/V12_002.UI.Compliance.cs | 952 | `.Positions.FirstOrDefault(p =>` | No | P3 | resolved: wave9 1f479056 |
| W9-L4-029 | src/V12_002.UI.IPC.Commands.Fleet.cs | 327 | `.Positions.Any(p =>` | No | P3 | resolved: wave9 2b1f4edd |
| W9-L4-030 | src/V12_002.UI.IPC.Commands.Fleet.cs | 351 | `.Values.Any(f =>` | No | P3 | resolved: wave9 2b1f4edd |
| W9-L4-031 | src/V12_002.UI.IPC.Commands.Misc.cs | 188 | `.Positions.FirstOrDefault(p =>` | No | P3 | resolved: wave9 9db8a8a9 |
| W9-L4-032 | src/V12_002.UI.IPC.cs | 258 | `.All.Where(a =>` | No | P3 | resolved: wave9 9db8a8a9 |
| W9-L4-033 | src/V12_002.UI.IPC.cs | 259 | `.OrderBy(a => a.Name,` | No | P3 | resolved: wave9 9db8a8a9 |
| W9-L4-034 | src/V12_002.UI.IPC.cs | 260 | `.ToList()` | No | P3 | resolved: wave9 9db8a8a9 |
| W9-L4-035 | src/V12_002.UI.IPC.cs | 295 | `.FirstOrDefault(a =>` | No | P3 | resolved: wave9 9db8a8a9 |

---

## L5 -- Magic Numbers JS-100 (223 violations across 52 files, Rule 6)

Fix: extract numeric literals to named `private const` or `private static readonly` at top of class.
Naming: `SCREAMING_SNAKE_CASE` for const, `PascalCase` for static readonly.
Trivial values (0, 1, -1, 2) that have no domain meaning: leave as-is.

Batched by file (wave9-scan enumerates exact lines per file):

| ID | File | Violations | Top Literals | Priority |
|----|------|-----------|-------------|----------|
| W9-L5-001 | src/V12_002.UI.Panel.Brushes.cs | 38 | RGB int literals (e.g. 255, 128, 64) | P3 | resolved: wave9 ad31a5a4
| W9-L5-002 | src/V12_002.LogicAudit.cs | 18 | audit thresholds (e.g. 3, 10, 100) | P2 | resolved: wave9 e27d32df
| W9-L5-003 | src/V12_002.Perf.LatencyHistogram.cs | 18 | histogram bucket sizes | P2 | resolved: wave9 826e2f8c |
| W9-L5-004 | src/V12_002.Lifecycle.cs | 17 | strategy defaults, timeouts | P2 | resolved: wave9 bbfdd2ae
| W9-L5-005 | src/V12_002.UI.Panel.Helpers.cs | 17 | UI dimensions, offsets | P3 | resolved: wave9 aa9222b4
| W9-L5-006 | src/V12_002.SIMA.Execution.cs | 16 | execution parameters | P2 | resolved: wave9 1ccf547b
| W9-L5-007 | src/V12_002.Properties.cs | 16 | NinjaTrader property bounds/defaults | P2 | resolved: wave9 8182de13
| W9-L5-008 | src/V12_002.UI.IPC.Server.cs | 10 | buffer sizes, port numbers, timeouts | P2 | resolved: wave9 0fb0a8b2
| W9-L5-009 | src/V12_002.UI.Compliance.cs | 9 | compliance thresholds | P2 | resolved: wave9 384426bb
| W9-L5-010 | src/V12_002.UI.Panel.StateSync.cs | 8 | UI state thresholds | P3 | resolved: wave9 79ab55f7 |
| W9-L5-011 | src/V12_002.Orders.Management.StopSync.cs | 8 | stop management parameters | P2 | resolved: wave9 813ade99 |
| W9-L5-012 | src/V12_002.cs | 7 | core strategy parameters | P2 | resolved: wave9 08293c97 |
| W9-L5-013 | src/V12_002.UI.Callbacks.cs | 7 | UI callback values | P3 | resolved: wave9 f48d79cb |
| W9-L5-014 | src/V12_002.UI.IPC.cs | 7 | IPC queue thresholds (e.g. 2000, 1600) | P2 | resolved: wave9 08293c97 |
| W9-L5-015 | src/V12_002.SIMA.Dispatch.cs | 7 | dispatch parameters | P2 | resolved: wave9 08293c97 |
| W9-L5-016 | src/V12_002.Symmetry.BracketFSM.cs | 7 | FSM parameters | P2 | resolved: wave9 08293c97 |
| W9-L5-017 | src/V12_002.StickyState.cs | 6 | state persistence values | P3 | resolved: wave9 e83c871b |
| W9-L5-018 | src/SignalBroadcaster.cs | 5 | signal parameters | P2 | resolved: wave9 e83c871b |
| W9-L5-019 | src/V12_002.UI.Panel.Construction.cs | 5 | panel layout values | P3 | resolved: wave9 e83c871b |
| W9-L5-020 | src/V12_002.Orders.Management.cs | 5 | order management parameters | P2 | resolved: wave9 e83c871b |
| W9-L5-021 | src/V12_002.REAPER.Audit.cs | 3 | ReaperAuditLogIntervalSeconds=30, IpcQueueRingCapacity=2000, IpcQueueAlertThreshold=1600 | P2 | resolved: wave9 79efebe5 |
| W9-L5-022 | src/V12_002.Trailing.cs | 6 | TrailThrottle{HighTickRate,MaxMs,MinMs,DecreaseMs,IncreaseMs,LowTickRate} | P2 | resolved: wave9 017c16cf |
| W9-L5-023 | src/V12_002.Trailing.StopUpdate.cs | 2 | NtSignalNameMaxLength=50, StopSuffixTicksMod=100000000 | P2 | resolved: wave9 ec6537e4 |
| W9-L5-024 | src/V12_002.Orders.Callbacks.AccountOrders.cs | 2 | AccountOrderQueueDepthWarningThreshold=50, DesyncDrawTextOpacity=50 | P2 | resolved: wave9 af704f7b |
| W9-L5-025 | src/V12_002.UI.Sizing.cs | 1 | SyncFailureCooldownMs=500 | P2 | resolved: wave9 d7a48868 |
| W9-L5-026 | src/V12_002.Orders.Management.Flatten.cs | 1 | FlattenSignalNameMaxLength=50 | P2 | resolved: wave9 f5c19f6d |
| W9-L5-027 | src/V12_002.DrawingHelpers.cs | 2 | FnvOffsetBasis=2166136261, FnvPrime=16777619 | P2 | resolved: wave9 1c7cd5ae |
| W9-L5-028 | src/V12_002.IPC.Hardening.cs | 5 | IpcRateLimitRequestsPerSecond=1600, IpcMalformed/AllowlistBypass CB thresholds+windows | P2 | resolved: wave9 43220b59 |
| W9-L5-029 | src/V12_002.BarUpdate.cs | 1 | ComplianceDailySummaryCheckIntervalSeconds=30 | P2 | resolved: wave9 2b90973f |
| W9-L5-030-052 | 35 remaining files scanned | 0 | No domain-meaningful magic numbers found | P3 | resolved: wave9 scan-only (no fix needed) |

*Note: wave9-scan enumerates exact lines per file before engineer fixes.*

---

## L6 -- Exceptions in Hot Paths JS-001 (12 occurrences, Rule 5)

Fix: wrap throw sites with try/catch returning bool/Result pattern where the caller is on a hot path.
Non-hot-path throws: leave as-is (ArgumentException on validation, etc. are acceptable).

| ID | File | Context | Priority |
|----|------|---------|----------|
| W9-L6-001 through W9-L6-012 | Various src/ | wave9-scan enumerates exact files/lines | P2 |
| W9-L6-001 | src/V12_002.IO.PathValidation.cs:56 | hot-path throw replaced with log+return null; null-guard added in EnsureDailySummaryCsv | resolved: wave9 8cbe00d7 |
| W9-L6-002 | src/V12_002.IO.PathValidation.cs:75,96; src/V12_002.UI.Compliance.cs:173,234,1000; src/V12_002.StickyState.cs:120,210 | all SecurityException throws replaced with log+return null/false; dead catch blocks removed; null-guards added | resolved: wave9 a973504b |
| W9-L6-003 | src/V12_002.IO.PathValidation.cs:95 | co-resolved by W9-L6-002 sweep — PathValidation.cs has 0 remaining throw new statements | resolved: wave9 a973504b |
| W9-L6-004 | src/V12_002.Orders.Callbacks.cs:232 | InvalidOperationException throw replaced with log+return false in HandleOrderState_Terminal | resolved: wave9 2e6453a9 |
| W9-L6-005 | src/SignalBroadcaster.cs:286 | ArgumentException throw in BroadcastTradeSignal replaced with log+return | resolved: wave9 8265e7fc |
| W9-L6-006 | src/SignalBroadcaster.cs:303 | ArgumentException throw in BroadcastTrailUpdate replaced with log+return | resolved: wave9 8265e7fc |
| W9-L6-007 | src/SignalBroadcaster.cs:318 | ArgumentException throw in BroadcastTargetAction replaced with log+return | resolved: wave9 8265e7fc |



*Note: wave9-scan uses grep for `throw new` in methods reachable from OnBarUpdate/OnOrderUpdate.*

---

## L7 -- LOC > 80 Methods (21 methods, Rule 6)

These are CYC<=8 compliant but long -- high cognitive load, high change-coupling risk.
Fix: extract sequential blocks into named private helpers (same class, same visibility).
Each extraction must keep CYC unchanged or reduce it. No new public API.

| ID | Method | File | LOC | CYC | Priority |
|----|--------|------|-----|-----|----------|
| W9-L7-001 | ExecuteRetestManualEntry | src/V12_002.Entries.Retest.cs | 149 | 8 | P2 | resolved: wave9 aa0e5fc6 (LOC=30 CYC=5) |
| W9-L7-002 | ExecuteFFMAManualMarketEntry | src/V12_002.Entries.FFMA.cs | 136 | 8 | P2 | resolved: wave9 c3a131d7 (LOC=69 CYC=6) |
| W9-L7-003 | ExecuteFFMALimitEntry | src/V12_002.Entries.FFMA.cs | 134 | 8 | P2 | resolved: wave9 1970f743 (LOC=34 CYC<=8) |
| W9-L7-004 | ExecuteRetestEntry | src/V12_002.Entries.Retest.cs | 133 | 5 | P2 | resolved: wave9 244850bb (LOC=46 CYC<=8) |
| W9-L7-005 | ExecuteFFMAEntry | src/V12_002.Entries.FFMA.cs | 126 | 8 | P2 | resolved: wave9 0b077f60 (LOC=61 CYC=6) |
| W9-L7-006 | SubmitTrendSplitBrackets | src/V12_002.Entries.RMA.cs | 126 | 6 | P2 | resolved: wave9 b4946364 (LOC=18 CYC<=4) |
| W9-L7-007 | CreateSection1_Execution | src/V12_002.UI.Panel.Construction.cs | 181 | ~5 | P3 | resolved: wave9 be199269 (LOC=55 4 helpers extracted) |
| W9-L7-008 | CreateSection3_Config | src/V12_002.UI.Panel.Construction.cs | 277 | ~5 | P3 | resolved: wave9 95b64aba (LOC=36 7 helpers extracted) |
| W9-L7-009 | CreateSection2_Telemetry | src/V12_002.UI.Panel.Construction.cs | 95 | ~3 | P3 | RESOLVED d1b7dccc -- LOC 24 (was 115) |
| W9-L7-010 | ClearPanelWidgetRefs | src/V12_002.UI.Panel.Construction.cs | 103 | ~2 | P3 | RESOLVED 1905d01f -- LOC 7 (was 112), 4 helpers extracted |
| W9-L7-011 | Dispatch_ProcessFleetLoop | src/V12_002.SIMA.Dispatch.cs | 107 | ~6 | P2 |
| W9-L7-012 | Dispatch_BuildFollowerOrders | src/V12_002.SIMA.Dispatch.cs | 98 | ~5 | P2 |
| W9-L7-013 | Dispatch_PublishLimitEntryToPhoton | src/V12_002.SIMA.Dispatch.cs | 84 | ~4 | P2 |
| W9-L7-014 | ExecuteMOMOEntry | src/V12_002.Entries.MOMO.cs | 98 | 5 | P2 |
| W9-L7-015 | EnterORPosition | src/V12_002.Entries.OR.cs | 106 | 5 | P2 |
| W9-L7-016 | HandleDataLoaded | src/V12_002.Lifecycle.cs | 96 | ~6 | P2 |
| W9-L7-017 | HandleSetDefaults | src/V12_002.Lifecycle.cs | 94 | ~4 | P2 |
| W9-L7-018 | ExecuteRMAEntryV2 | src/V12_002.SIMA.Execution.cs | 81 | ~5 | P2 |
| W9-L7-019 | ProcessSingleFleetRMAAccount | src/V12_002.SIMA.Execution.cs | 84 | ~5 | P2 |
| W9-L7-020 | ExecuteTRENDEntry | src/V12_002.Entries.Trend.cs | 88 | 8 | P2 |
| W9-L7-021 | ExecuteTREND_CalculateLegs | src/V12_002.Entries.Trend.cs | 84 | 2 | P3 |

---

## L8 -- M5 Dispatch Candidates (4 methods, Rule 6)

These are high-complexity FSM dispatch methods that should be decomposed into
typed message dispatch tables (Dictionary<TEvent, Action<TContext>>).
Fix: replace if/else chains with static readonly Dictionary dispatch.

| ID | Method | File | Current Pattern | Priority |
|----|--------|------|-----------------|----------|
| W9-L8-001 | ProcessOnStateChange | src/V12_002.Lifecycle.cs | if/else state chain | P2 | resolved: wave9 f37362a9
| W9-L8-002 | ProcessBracketEvent | src/V12_002.Symmetry.BracketFSM.cs | switch/if FSM | P2 | resolved: wave9 6eb7f212
| W9-L8-003 | RouteTargetActionToHandler | src/V12_002.UI.Callbacks.cs | if/else dispatch | P3 | resolved: wave9 e570657a
| W9-L8-004 | DispatchRunnerAction | src/V12_002.UI.Callbacks.cs | if/else dispatch | P3 | resolved: wave9 d4fc98ae |

---

## Priority Key

| Level | Meaning |
|-------|---------|
| P1 | Direct production safety / determinism violation -- fix in L1 sprint |
| P2 | Production risk under load (race conditions, data drift, GC pauses) |
| P3 | Readability / maintainability debt, zero immediate production risk |

---

## Resolution Tracking

Mark each row: `resolved: wave9 {commit_sha}` when fixed.
No row may be deleted -- append only.

---

## Usage

Before starting any wave that touches a file in this register:
1. Check all entries for that file.
2. Include them in the lane's scan phase.
3. After fix: mark resolved with commit sha.
