# PR-21 Repair Plan — wave7/pr2-s3-ui-ipc

**Branch**: `wave7/pr2-s3-ui-ipc`
**Files in scope**: `src/V12_002.UI.Compliance.cs`, `src/V12_002.UI.IPC.cs`
**Agent Tracking**: V12 Architecture Planner — Phase 2 Sequential Thinking validated

---

## Bug 1 — UI.Compliance.cs: `this.Account` used instead of named account

### Root cause
`IsOrderAllowed` resolves the compliance target to `acctName` but then fetches the live balance from `this.Account` (the chart's primary account), so fleet-account drawdown buffers are calculated with the wrong equity curve.

### Exact old code
**File**: [`src/V12_002.UI.Compliance.cs`](../../src/V12_002.UI.Compliance.cs:336)
**Location**: `IsOrderAllowed`, line 336

```csharp
Account currentAccount = this.Account;
```

### Exact new code
```csharp
Account currentAccount = Account.All.FirstOrDefault(a => a.Name == acctName) ?? this.Account;
```

### Rationale
The established pattern in this file (see [`GetComplianceAccounts()`](../../src/V12_002.UI.Compliance.cs:293) and [`UpdateAccountMetricsFromAccount()`](../../src/V12_002.UI.Compliance.cs:99)) resolves Account objects by iterating `Account.All`. The `acctName` dictionary key is the per-account identity; the balance fetch must use the matching NinjaTrader `Account` object, not the chart-level `this.Account`.

**OKF cite — `independent_tracking`** (`production-engineering-billions.md`):
> "each account tracked independently"

Each fleet account owns its own equity peak and drawdown state. Reading the balance from a different account's `CashValue` silently corrupts the buffer calculation for every non-primary fleet account.

### Edge cases
| Case | Behaviour |
|---|---|
| `acctName` found in `Account.All` | Correct account's `CashValue` is read — intended behaviour |
| `acctName` not found (e.g. sim name not registered yet) | Falls back to `this.Account` — identical to current behaviour, no regression |
| `Account.All` empty (NT not connected) | `FirstOrDefault` returns `null`; `?? this.Account` applies; existing null-check at line 337 handles `this.Account == null` |
| SIMA disabled, single-account mode | `acctName == Account?.Name`; `FirstOrDefault` matches `this.Account` by name; result is the same object |

### CYC delta
**0** — no new branches introduced; assignment expression only.

---

## Bug 2 — UI.IPC.cs: `ValidateIpcCommand` runs before `IsAllowedIpcAction`

### Root cause
`ProcessIpcCommands` calls `ValidateIpcCommand` (which consumes a rate-limiter token and can trip the circuit breaker) before checking `IsAllowedIpcAction`, so unknown/garbage actions exhaust rate-limiter capacity and can open the circuit breaker on legitimate traffic.

### Exact old code
**File**: [`src/V12_002.UI.IPC.cs`](../../src/V12_002.UI.IPC.cs:418)
**Location**: `ProcessIpcCommands`, lines 418–428

```csharp
                    // EPIC-4 Ticket 03: IPC Hardening validation (rate limiting, circuit breakers, anomaly detection)
                    ValidationResult validationResult = ValidateIpcCommand(action, parts);
                    if (HandleValidationFailure(validationResult, action))
                        continue;

                    if (!IsAllowedIpcAction(action))
                    {
                        Interlocked.Increment(ref _ipcAllowlistRejectCount);
                        Print($"V12 IPC REJECT: action '{action}' is not allowed");
                        continue;
                    }
```

### Exact new code
```csharp
                    if (!IsAllowedIpcAction(action))
                    {
                        Interlocked.Increment(ref _ipcAllowlistRejectCount);
                        Print($"V12 IPC REJECT: action '{action}' is not allowed");
                        continue;
                    }

                    // EPIC-4 Ticket 03: IPC Hardening validation (rate limiting, circuit breakers, anomaly detection)
                    ValidationResult validationResult = ValidateIpcCommand(action, parts);
                    if (HandleValidationFailure(validationResult, action))
                        continue;
```

### Rationale
`ValidateIpcCommand` (see [`src/V12_002.IPC.Hardening.cs:201`](../../src/V12_002.IPC.Hardening.cs:201)) calls `_ipcCommandRateLimiter.TryAcquire()` on every invocation — a stateful, slot-consuming operation. An attacker or a misconfigured sender replaying unknown action strings will drain the rate-limiter and open the circuit breaker, causing legitimate allowlisted commands to be rejected with `CircuitBreakerOpen`.

**OKF cite — `rate_limiting`** (`production-engineering-billions.md`):
> "rate limiters must not fire on garbage input"

**OKF cite — `sidecar_lifecycle`** (`how-to-build-an-exchange.md`):
> "validation sidecars only process known commands"

The allowlist check (`IsAllowedIpcAction`) is O(1) HashSet lookup + StartsWith checks — zero side effects. It is the correct first gate.

### Edge cases
| Case | Behaviour |
|---|---|
| `action` is null or whitespace | `IsAllowedIpcAction` returns `false` at its first guard (line 187); rate limiter never touched |
| Unknown action that resembles an allowlist prefix (bypass attempt) | Rejected at `IsAllowedIpcAction`; `_ipcAllowlistRejectCount` incremented; `ValidateIpcCommand`'s `IsAllowlistBypassAttempt` path is never reached for non-allowlisted actions — acceptable because they are rejected anyway |
| Known allowlisted action | `IsAllowedIpcAction` returns `true`; execution falls through to `ValidateIpcCommand` exactly as before |
| Rate-limited burst of valid commands | Rate limiter only counts valid allowlisted commands — correct behaviour per OKF |

### CYC delta
**0** — no new branches; only statement reordering within the existing `try` block.

---

## Summary table

| # | File | Lines | Change type | CYC delta |
|---|---|---|---|---|
| 1 | `V12_002.UI.Compliance.cs` | 336 | Assignment expression | 0 |
| 2 | `V12_002.UI.IPC.cs` | 418–428 | Statement reorder | 0 |

**Total src/ lines changed**: 1 modified (Bug 1) + 10 reordered (Bug 2, no net additions)
