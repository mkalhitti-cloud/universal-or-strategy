# PR #21 Fix Queue — wave7/pr2-s3-ui-ipc
# S3 UI & IPC — 14 files
# Reviewers: Gemini, CodeAnt, CodeRabbit, Sourcery

---

## [LOGIC-BUG] P0 — UI.Compliance.cs: this.Account vs fleet acctName

**File**: `src/V12_002.UI.Compliance.cs`
**Method**: `IsOrderBlocked_TrailingDrawdown` (line ~347)
**Reviewers**: Gemini (high severity)

**Symptom**: Method takes `string acctName` parameter (the fleet account being
checked) but retrieves balance using `this.Account` — always the master/lead
account. For fleet follower accounts, the balance comparison is against the
wrong account's cash value. Trailing drawdown compliance check silently passes
or fails based on master balance, not the actual follower balance.

**Source lines** (current buggy state):
```csharp
private bool IsOrderBlocked_TrailingDrawdown(string acctName)
{
    if (!accountEquityPeak.TryGetValue(acctName, out double peak) || ...)
        return false;
    double balance = 0;
    Account currentAccount = this.Account;   // ← always master, ignores acctName
    if (currentAccount != null)
    {
        balance = currentAccount.Get(AccountItem.CashValue, Currency.UsDollar);
    }
    ...
}
```

**Fix approach**: Resolve the NinjaTrader `Account` object for `acctName` from
the account collection, not `this.Account`. Use
`Account.All.FirstOrDefault(a => a.Name == acctName)` or the existing fleet
account lookup pattern used elsewhere in the file.

**OKF**: production-engineering-billions.md → `independent_tracking`: each
account tracked independently, never proxied through master state.

---

## [LOGIC-BUG] P0 — UI.IPC.cs: allowlist check after validator — ordering reversed

**File**: `src/V12_002.UI.IPC.cs`
**Method**: `TrySingleIpcCommand` (line ~464)
**Reviewers**: CodeAnt (security)

**Symptom**: `ValidateIpcCommand` (rate limiter + circuit breaker + anomaly
detection) runs BEFORE `IsAllowedIpcAction`. Unknown/unlisted commands hit the
circuit breaker and increment `_ipcAllowlistRejectCount` before being rejected
as unknown. Correct order: allowlist first (fast reject of unknowns), validator
second (only known commands consume rate-limit budget).

**Source lines** (current buggy state):
```csharp
// Validator runs first — unknown commands trip circuit breaker
ValidationResult validationResult = ValidateIpcCommand(action, parts);
if (HandleValidationFailure(validationResult, action))
    return;

if (!IsAllowedIpcAction(action))   // ← should be first
{
    Interlocked.Increment(ref _ipcAllowlistRejectCount);
    return;
}
```

**Fix**: Swap the two blocks — allowlist check before validator.

**OKF**: how-to-build-an-exchange.md → `sidecar_lifecycle`: validation
sidecars should only process known commands. production-engineering-billions.md
→ `rate_limiting`: rate limiters must not fire on garbage input.

---

## STATUS
- [ ] LOGIC-BUG: UI.Compliance this.Account → fleet account lookup
- [ ] LOGIC-BUG: UI.IPC allowlist before validator
