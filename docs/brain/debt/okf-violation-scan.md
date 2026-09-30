# OKF Violation Scan Report

**Generated**: 2026-07-04
**Scope**: src/*.cs (83 files)
**Total new violations**: 45 (excluding already-registered entries)

| Count | Priority | Rule |
|-------|----------|------|
| 24 | P1 | DateTime.Now |
| 21 | P2 | missing .ToArray() |

---

## Findings

### P1

- **src/SignalBroadcaster.cs:289** -- DateTime.Now -- use DateTime.UtcNow
  `signal.Timestamp = DateTime.Now;`

- **src/SignalBroadcaster.cs:306** -- DateTime.Now -- use DateTime.UtcNow
  `update.Timestamp = DateTime.Now;`

- **src/SignalBroadcaster.cs:321** -- DateTime.Now -- use DateTime.UtcNow
  `action.Timestamp = DateTime.Now;`

- **src/SignalBroadcaster.cs:330** -- DateTime.Now -- use DateTime.UtcNow
  `var signal = new FlattenSignal { Reason = reason ?? "Manual flatten", Timestamp = DateTime.Now };`

- **src/SignalBroadcaster.cs:340** -- DateTime.Now -- use DateTime.UtcNow
  `var signal = new BreakevenSignal { SignalId = signalId, Timestamp = DateTime.Now };`

- **src/SignalBroadcaster.cs:355** -- DateTime.Now -- use DateTime.UtcNow
  `Timestamp = DateTime.Now,`

- **src/SignalBroadcaster.cs:370** -- DateTime.Now -- use DateTime.UtcNow
  `Timestamp = DateTime.Now,`

- **src/SignalBroadcaster.cs:385** -- DateTime.Now -- use DateTime.UtcNow
  `Timestamp = DateTime.Now,`

- **src/SignalBroadcaster.cs:400** -- DateTime.Now -- use DateTime.UtcNow
  `Timestamp = DateTime.Now,`

- **src/V12_002.Entries.OR.cs:62** -- DateTime.Now -- use DateTime.UtcNow
  `lastArmedTime = DateTime.Now;`

- **src/V12_002.Entries.OR.cs:106** -- DateTime.Now -- use DateTime.UtcNow
  `lastArmedTime = DateTime.Now;`

- **src/V12_002.Orders.Management.StopSync.cs:763** -- DateTime.Now -- use DateTime.UtcNow
  `double ocoLatencyMs = (DateTime.Now - pendingForLatency.CreatedTime).TotalMilliseconds;`

- **src/V12_002.Orders.Management.StopSync.cs:968** -- DateTime.Now -- use DateTime.UtcNow
  `string suffix = (DateTime.Now.Ticks % 100000000).ToString();`

- **src/V12_002.SIMA.Execution.cs:360** -- DateTime.Now -- use DateTime.UtcNow
  `string ocoId = action.ToString() + "_" + DateTime.Now.Ticks;`

- **src/V12_002.SIMA.Execution.cs:992** -- DateTime.Now -- use DateTime.UtcNow
  `string baseSignal = "RMA_" + DateTime.Now.Ticks;`

- **src/V12_002.Trailing.StopUpdate.cs:176** -- DateTime.Now -- use DateTime.UtcNow
  `CreatedTime = DateTime.Now,`

- **src/V12_002.Trailing.StopUpdate.cs:188** -- DateTime.Now -- use DateTime.UtcNow
  `circuitBreakerActivatedTime = DateTime.Now;`

- **src/V12_002.Trailing.StopUpdate.cs:342** -- DateTime.Now -- use DateTime.UtcNow
  `circuitBreakerActivatedTime = DateTime.Now;`

- **src/V12_002.Trailing.cs:215** -- DateTime.Now -- use DateTime.UtcNow
  `DateTime now = DateTime.Now;`

- **src/V12_002.UI.Compliance.cs:45** -- DateTime.Now -- use DateTime.UtcNow
  `return ConvertToSelectedTimeZone(DateTime.Now);`

- **src/V12_002.UI.Compliance.cs:915** -- DateTime.Now -- use DateTime.UtcNow
  `lastComplianceLog = DateTime.Now;`

- **src/V12_002.UI.Compliance.cs:931** -- DateTime.Now -- use DateTime.UtcNow
  `if ((DateTime.Now - lastComplianceLog).TotalSeconds < 5)`

- **src/V12_002.UI.Sizing.cs:130** -- DateTime.Now -- use DateTime.UtcNow
  `if ((DateTime.Now - _lastSyncFailureTime).TotalMilliseconds < 500)`

- **src/V12_002.UI.Sizing.cs:314** -- DateTime.Now -- use DateTime.UtcNow
  `_lastSyncFailureTime = DateTime.Now;`

### P2

- **src/V12_002.Orders.Management.Cleanup.cs:521** -- acct.Orders enumerated without .ToArray() snapshot
  `foreach (Order order in Account.Orders)`

- **src/V12_002.Orders.Management.Cleanup.cs:623** -- Account.All enumerated without .ToArray() snapshot
  `foreach (Account acct in Account.All)`

- **src/V12_002.Orders.Management.Cleanup.cs:627** -- acct.Orders enumerated without .ToArray() snapshot
  `foreach (Order fleetOrder in acct.Orders)`

- **src/V12_002.Orders.Management.Cleanup.cs:639** -- acct.Orders enumerated without .ToArray() snapshot
  `foreach (Order brokerOrder in Account.Orders)`

- **src/V12_002.Orders.Management.StopSync.cs:479** -- acct.Orders enumerated without .ToArray() snapshot
  `foreach (Order o in Account.Orders)`

- **src/V12_002.REAPER.Audit.cs:912** -- Account.All enumerated without .ToArray() snapshot
  `foreach (Account acct in Account.All)`

- **src/V12_002.SIMA.Execution.cs:60** -- Account.All enumerated without .ToArray() snapshot
  `foreach (Account acct in Account.All)`

- **src/V12_002.SIMA.Execution.cs:252** -- Account.All enumerated without .ToArray() snapshot
  `foreach (Account acct in Account.All)`

- **src/V12_002.SIMA.Execution.cs:1064** -- Account.All enumerated without .ToArray() snapshot
  `foreach (Account acct in Account.All)`

- **src/V12_002.SIMA.Flatten.cs:241** -- acct.Positions enumerated without .ToArray() snapshot
  `foreach (Position position in acct.Positions)`

- **src/V12_002.SIMA.Flatten.cs:471** -- acct.Orders enumerated without .ToArray() snapshot
  `foreach (Order o in acct.Orders)`

- **src/V12_002.SIMA.Fleet.cs:576** -- acct.Positions enumerated without .ToArray() snapshot
  `if (acct == null || acct.Positions == null)`

- **src/V12_002.SIMA.Lifecycle.cs:157** -- Account.All enumerated without .ToArray() snapshot
  `foreach (Account acct in Account.All)`

- **src/V12_002.SIMA.Lifecycle.cs:1419** -- Account.All enumerated without .ToArray() snapshot
  `foreach (Account acct in Account.All)`

- **src/V12_002.SIMA.cs:221** -- Account.All enumerated without .ToArray() snapshot
  `foreach (Account acct in Account.All)`

- **src/V12_002.UI.Compliance.cs:302** -- Account.All enumerated without .ToArray() snapshot
  `foreach (Account acct in Account.All)`

- **src/V12_002.UI.IPC.Commands.Fleet.cs:234** -- acct.Orders enumerated without .ToArray() snapshot
  `foreach (Order order in Account.Orders)`

- **src/V12_002.UI.IPC.Commands.Fleet.cs:317** -- Account.All enumerated without .ToArray() snapshot
  `foreach (Account acct in Account.All)`

- **src/V12_002.UI.IPC.Commands.Fleet.cs:338** -- acct.Orders enumerated without .ToArray() snapshot
  `foreach (Order order in acct.Orders)`

- **src/V12_002.UI.IPC.Commands.Fleet.cs:404** -- Account.All enumerated without .ToArray() snapshot
  `foreach (Account acct in Account.All)`

- **src/V12_002.UI.IPC.Commands.Misc.cs:141** -- Account.All enumerated without .ToArray() snapshot
  `foreach (Account acct in Account.All)`

---

## Next Steps

1. P0 findings: HARD STOP. Fix before any merge. Escalate to Director.
2. P1 findings: Add to next wave Phase 0 hotspot list as mandatory pre-scan.
3. P2/P3 findings: Triage per file. Add to deferred-debt-register.md rows.
4. P4 findings: Group by file. Fix in next wave touchin the same file.

**Machine-readable**: docs/brain/debt/okf-violation-scan.json
