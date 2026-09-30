# B68-LaneA Tickets

**Block**: B68-LaneA
**Written by**: ptt-architect (Phase 3)
**Date**: 2026-08-14
**Input plan**: docs/brain/B68-LaneA/02-architecture-plan.md (REVIEW_PASS)
**Engineer contract**: ptt-engineer implements from this file exactly. No deviation without Director approval.

---

## Ticket 1: DW-B68-01 — Cancel follower stale brackets before PTT-QX and PTT-BE orders

### Spec Requirement IDs

- **DW-B68-01** (P0) — stale follower brackets on QX and BE paths. Confirmed live 2026-08-13.

---

### Problem

When Quick Exit fires (`PttGlobalQuickExit.Execute`), follower account ATM bracket orders
(Stop1/Stop2/Target1/Target2) placed at entry are never cancelled — they persist alongside any
new PTT-QX-* orders, producing conflicting bracket protection in live trading. When Break-Even
fires (`CopyEngine.RelayBe`), no bracket cancellation precedes `SubmitBeStop`, so all accounts
retain live ATM brackets when the new BE stop is placed. Both code paths require stale bracket
cancellation before the new protective order is submitted.

---

### Files to Modify

| File | Change | Net lines |
|------|--------|-----------|
| `src/PropTraderTools/CopyEngine.cs` | Add `CancelQxBracketsForFollowers` after line 464; expand `RelayBe` foreach body | +17 |
| `src/PropTraderTools/Features/PttGlobalQuickExit.cs` | Add `engine?.CancelQxBracketsForFollowers(pos.Instrument)` call inside `Execute` inner loop | +3 |
| `tests/PropTraderTools.Tests/CopyEngineB68Tests.cs` | **NEW file** — T_B68_01..T_B68_06 xUnit [Fact] tests | ~180 |

**NOT modified** (hard constraint):
- `src/PropTraderTools/Features/PttQuickExit.cs` — DO NOT TOUCH
- `src/PropTraderTools/CopyEngine.cs` — `IsQxCancelCandidate`, `IsAtmBracketName`, `CancelQxBrackets` — DO NOT TOUCH

---

### Exact Code Changes

#### Change 1 — CopyEngine.cs: New method `CancelQxBracketsForFollowers`

**Action**: INSERT 16 lines immediately after line 464 (the closing brace of `CancelQxBrackets`),
before the blank separator and the `// NextQxOcoId` comment at line 466.

```csharp
        // B68 DW-B68-01: CancelQxBracketsForFollowers -- cancel stale brackets on all followers.
        // Called by PttGlobalQuickExit.Execute before placing new PTT-QX-* orders on the leader.
        // Ensures follower ATM brackets (Stop1/Stop2/Target1/Target2) and prior PTT-QX-*/PTT-BE-*
        // orders do not persist as stale orders alongside new QX bracket pairs.
        // CYC=5: instr-null-guard(1) + rule-null-guard(2) + foreach(3) + acc-null-guard(4) + delegate(5).
        // JS-021: no lock. JS-001: no throw. JS-002: void. JS-033: synchronous void.
        // NT8-REF: Account.Cancel -- via CancelQxBrackets (existing, tested, line 462).
        internal void CancelQxBracketsForFollowers(NinjaTrader.Cbi.Instrument instr)
        {
            if (instr == null) return;                                   // (1)
            var rule = FindRule(instr);
            if (rule == null) return;                                    // (2)
            foreach (var acc in rule.Value.FollowerAccounts)            // (3)
            {
                if (acc == null) continue;                               // (4)
                CancelQxBrackets(acc, instr);                            // (5)
            }
        }
```

**CYC**: 5 (1 base + instr null guard + rule null guard + foreach branch + acc null guard).
**Jane Street**: JS-021 no lock; JS-001 no throw; JS-002 void; JS-033 synchronous void.

---

#### Change 2 — CopyEngine.cs: `RelayBe` expanded foreach body

**Location**: lines 343–352.

**OLD** (exact current code, lines 343–352):
```csharp
        // B58 ICopyEngine -- RelayBe: fan out pre-calculated BE price to all follower accounts.
        // BeEventArgs.BePrice is already computed by PttGlobalBreakEven/BE module before firing.
        // B66 DW-B66-BE-01: e.IsLong passed to SubmitBeStop (was relying on re-read inside method -- race).
        // CYC=2 (1 base + 1 foreach branch). JS-021: no lock -- AllAccounts snapshot; SubmitBeStop lock-free.
        // JS-002: void method, no return null. JS-033: synchronous void.
        public void RelayBe(BeEventArgs e)
        {
            foreach (var acc in AllAccounts(e.Instrument))
                SubmitBeStop(acc, e.Instrument, e.BePrice, e.IsLong);
        }
```

**NEW** (exact replacement):
```csharp
        // B58 ICopyEngine -- RelayBe: fan out pre-calculated BE price to all follower accounts.
        // BeEventArgs.BePrice is already computed by PttGlobalBreakEven/BE module before firing.
        // B66 DW-B66-BE-01: e.IsLong passed to SubmitBeStop (was relying on re-read inside method -- race).
        // B68 DW-B68-01: CancelQxBrackets added before SubmitBeStop -- clears stale ATM brackets
        //   (Stop1/Stop2/Target1/Target2) on each account before the new BE stop is placed.
        //   No new McCabe branch: the cancel is a void call in the loop body, not an if-branch.
        // CYC=2 (unchanged: 1 base + 1 foreach branch). JS-021: no lock. JS-002: void. JS-033: synchronous.
        public void RelayBe(BeEventArgs e)
        {
            foreach (var acc in AllAccounts(e.Instrument))
            {
                CancelQxBrackets(acc, e.Instrument);
                SubmitBeStop(acc, e.Instrument, e.BePrice, e.IsLong);
            }
        }
```

**CYC**: Remains 2 (no new if-branch; `CancelQxBrackets` call is a statement in the loop body, not a decision point).

---

#### Change 3 — PttGlobalQuickExit.cs: `Execute` inner position loop

**Location**: lines 20–39 (method `Execute` with its XML doc comment).

**OLD** (exact current code, lines 20–39):
```csharp
        /// <summary>
        /// Execute: all-accounts Quick Exit bracket swap, skipping follower accounts.
        /// CYC=5: acc loop(1), follower guard(2), pos loop(3), null/flat continue(4), delegate(5).
        /// DW-B47-BE-FOLLOWER-SCOPE: follower accounts skipped via CopyEngine.IsFollowerAccount.
        /// JS-021: no lock. NT8-021: Account.All safe -- called from UI thread after Loaded.
        /// </summary>
        internal void Execute()
        {
            var engine = CopyEngine.Instance;                   // capture once
            foreach (Account acc in Account.All)                // (1)
            {
                if (engine != null && engine.IsFollowerAccount(acc)) continue; // (2) follower skip
                foreach (Position pos in acc.Positions)         // (3)
                {
                    if (pos == null || pos.Quantity == 0) continue;  // (4)
                    var ticks = ResolveQuickTicks(pos.Instrument);
                    ExecuteOne(acc, pos.Instrument, ticks.t1, ticks.t2);
                }
            }
        }
```

**NEW** (exact replacement):
```csharp
        /// <summary>
        /// Execute: all-accounts Quick Exit bracket swap, skipping follower accounts.
        /// CYC=6: acc loop(1), follower guard(2), pos loop(3), null/flat continue(4),
        ///        engine?. null-check on cancel call(5), delegate(6).
        /// DW-B47-BE-FOLLOWER-SCOPE: follower accounts skipped via CopyEngine.IsFollowerAccount.
        /// B68 DW-B68-01: follower brackets cancelled via CancelQxBracketsForFollowers before ExecuteOne.
        /// JS-021: no lock. NT8-021: Account.All safe -- called from UI thread after Loaded.
        /// </summary>
        internal void Execute()
        {
            var engine = CopyEngine.Instance;                   // capture once
            foreach (Account acc in Account.All)                // (1)
            {
                if (engine != null && engine.IsFollowerAccount(acc)) continue; // (2) follower skip
                foreach (Position pos in acc.Positions)         // (3)
                {
                    if (pos == null || pos.Quantity == 0) continue;  // (4)
                    var ticks = ResolveQuickTicks(pos.Instrument);
                    engine?.CancelQxBracketsForFollowers(pos.Instrument); // B68 DW-B68-01 (5)
                    ExecuteOne(acc, pos.Instrument, ticks.t1, ticks.t2); // (6)
                }
            }
        }
```

**CYC**: 5 → 6. The `?.` null-conditional operator adds one McCabe decision point. Remains ≤ 8.

---

### Method Signatures

```csharp
// CopyEngine.cs — new method
internal void CancelQxBracketsForFollowers(NinjaTrader.Cbi.Instrument instr)
```

No other new method signatures. `RelayBe` and `Execute` signatures are unchanged (same name, same params, same return type).

---

### xUnit Test Methods

**File**: `tests/PropTraderTools.Tests/CopyEngineB68Tests.cs` (new)
**Namespace**: `PropTraderTools.Tests`
**Framework**: xUnit only. No NUnit. No MSTest.
**Class**: `CopyEngineB68Tests`

---

#### T_B68_01

**[Fact] name**: `T_B68_01`

**Description**: QX path — `CancelQxBracketsForFollowers` cancels follower brackets and leaves master untouched.

**Arrange**:
- CopyEngine has one rule: master=`MasterAcc`, followers=[`Follower1`, `Follower2`].
- `Follower1` has two Working ATM bracket orders: `"Stop1"` and `"Target1"`.
- `Follower2` has one Working order: `"PTT-QX-00001"` (stale from a prior QX).
- `MasterAcc` has zero Working orders.

**Act**: `engine.CancelQxBracketsForFollowers(instr)`

**Assert**:
- `Follower1`'s tracked cancel list contains `"Stop1"` and `"Target1"`.
- `Follower2`'s tracked cancel list contains `"PTT-QX-00001"`.
- `MasterAcc`'s tracked cancel list is empty (master NOT touched by this helper).

---

#### T_B68_02

**[Fact] name**: `T_B68_02`

**Description**: BE path — `CancelQxBrackets` fires before `SubmitBeStop` in `RelayBe` for every account.

**Arrange**:
- CopyEngine has one rule: master=`MasterAcc`, follower=[`Follower1`].
- `Follower1` has Working orders `"Stop1"` and `"Target1"`; open position qty=1 long.
- `MasterAcc` has Working orders `"Stop2"` and `"Target2"`; open position qty=1 long.
- A call-order tracker records the sequence of cancel calls vs. CreateOrder calls per account.

**Act**: `engine.RelayBe(new BeEventArgs { Instrument = instr, BePrice = 100.0, IsLong = true })`

**Assert**:
- For `Follower1`: cancel was called (with `"Stop1"` and `"Target1"`) BEFORE `SubmitBeStop` fired (CreateOrder recorded after cancel in sequence tracker).
- For `MasterAcc`: cancel was called (with `"Stop2"` and `"Target2"`) BEFORE `SubmitBeStop` fired.
- Both accounts received a new `"PTT-BE-Stop"` order at `bePrice=100.0`.

---

#### T_B68_03

**[Fact] name**: `T_B68_03`

**Description**: No-regression — normal PTT-Copy dispatch does NOT trigger bracket cancellation.

**Arrange**:
- Leader places a new entry order with `Name` NOT starting with `"PTT-"` (e.g. `"MES 09-26"`).
- CopyEngine state is configured with one rule (master + followers).

**Act**: Simulate `DispatchCopy` firing with that non-PTT-prefixed entry order on the leader.

**Assert**:
- `SendCopy` is called for each follower (copy dispatched normally).
- `CancelQxBracketsForFollowers` is NOT called (no spurious cancellation on normal copy path).
- Follower Working order count is unchanged after copy dispatch (no stale bracket removal).

---

#### T_B68_04

**[Fact] name**: `T_B68_04`

**Description**: Follower with no stale brackets — `CancelQxBracketsForFollowers` returns cleanly.

**Arrange**:
- CopyEngine has one rule: follower=[`Follower1`].
- `Follower1` has ZERO Working/Accepted/Initialized orders (empty order book).

**Act**: `engine.CancelQxBracketsForFollowers(instr)`

**Assert**:
- No exception thrown (method completes without error).
- `Account.Cancel` is NOT called with a non-empty array (`CancelQxBrackets` exits early on empty stale list).
- No side effects observed.

---

#### T_B68_05

**[Fact] name**: `T_B68_05`

**Description**: Null instrument guard — `CancelQxBracketsForFollowers` returns immediately on null input.

**Arrange**:
- CopyEngine has a rule configured.

**Act**: `engine.CancelQxBracketsForFollowers(null)`

**Assert**:
- No exception thrown (null guard at branch (1) fires and returns).
- `FindRule` is never called.
- No orders are touched.

---

#### T_B68_06

**[Fact] name**: `T_B68_06`

**Description**: RelayBe with no rule — method returns cleanly without error when instrument has no CopyRule.

**Arrange**:
- CopyEngine has NO rule configured for `unknownInstr`.

**Act**: `engine.RelayBe(new BeEventArgs { Instrument = unknownInstr, BePrice = 99.0, IsLong = true })`

**Assert**:
- No exception thrown.
- `AllAccounts(unknownInstr)` yields no accounts (rule not found, enumerable empty).
- Neither `CancelQxBrackets` nor `SubmitBeStop` is called.

---

### 7-Scan Checklist (Engineer MUST run all 7 before declaring BUILD_PASS)

| Scan | Command | Expected Result | PASS condition |
|------|---------|-----------------|----------------|
| **S1** | `grep -n "lock(" src/PropTraderTools/CopyEngine.cs` | 0 hits in new/changed lines (comments excluded) | Zero results outside comment lines |
| **S2** | `grep -n "throw new" src/PropTraderTools/CopyEngine.cs` | 0 hits in new/changed lines | Zero results in B68-added code |
| **S3** | `python scripts/complexity_audit.py --file src/PropTraderTools/CopyEngine.cs` | `CancelQxBracketsForFollowers` CYC=5 ≤ 8; `RelayBe` CYC=2 ≤ 8; `Execute` CYC=6 ≤ 8 | All three ≤ 8 |
| **S4** | `grep -Pn "[^\x00-\x7F]" src/PropTraderTools/CopyEngine.cs` | 0 hits in B68-added lines (pre-existing exempt) | Zero new non-ASCII characters |
| **S5** | `grep -n "lock(" src/PropTraderTools/Features/PttGlobalQuickExit.cs` | 0 hits | Zero results |
| **S6** | `dotnet build src/PropTraderTools/PropTraderTools.csproj` | 0 errors, 0 new warnings | Exit code 0 |
| **S7** | `dotnet test tests/PropTraderTools.Tests/PropTraderTools.Tests.csproj --filter "FullyQualifiedName~T_B68"` | 6 tests pass, 0 failures | All 6 green |

---

### NT8 Constraints

- **NT8-REF**: `CancelQxBrackets` at [`CopyEngine.cs:447`](src/PropTraderTools/CopyEngine.cs:447) already uses `acc.Cancel(Order[])` — no new NT8 API surface introduced.
- **NT8-REF**: ATM bracket order names (`"Stop1"`, `"Stop2"`, `"Target1"`, `"Target2"`) sourced from `NT8_FULL_REFERENCE.md` line 1631, cited at [`CopyEngine.cs:424`](src/PropTraderTools/CopyEngine.cs:424).
- **NT8-021**: `Account.All` is safe from UI thread after Loaded — cited at [`PttGlobalQuickExit.cs:5`](src/PropTraderTools/Features/PttGlobalQuickExit.cs:5).
- **Constraint**: `AtmStrategyCreate()` is `StrategyBase`-only — NOT used in B68 (irrelevant).
- All new NT8 operations delegate through existing `CancelQxBrackets` and `SubmitBeStop`. No NT8 uncertainty introduced.

---

### Deploy Step (mandatory — runs after `git commit`, before BUILD_PASS declaration)

Engineer MUST copy both modified `.cs` files to the NinjaTrader 8 AddOn directory and verify
SHA-256 hashes match. BUILD_PASS is only valid after all SHA-256 matches are confirmed.

```powershell
# Step 1: Copy CopyEngine.cs
Copy-Item `
    "C:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs" `
    "C:\Users\Mohammed Khalid\Documents\NinjaTrader 8\bin\Custom\AddOns\PropTraderTools\CopyEngine.cs" `
    -Force

# Step 2: Copy PttGlobalQuickExit.cs
Copy-Item `
    "C:\WSGTA\universal-or-strategy\src\PropTraderTools\Features\PttGlobalQuickExit.cs" `
    "C:\Users\Mohammed Khalid\Documents\NinjaTrader 8\bin\Custom\AddOns\PropTraderTools\Features\PttGlobalQuickExit.cs" `
    -Force

# Step 3: Verify SHA-256 for CopyEngine.cs
$srcHash = (Get-FileHash "C:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs" -Algorithm SHA256).Hash
$dstHash = (Get-FileHash "C:\Users\Mohammed Khalid\Documents\NinjaTrader 8\bin\Custom\AddOns\PropTraderTools\CopyEngine.cs" -Algorithm SHA256).Hash
Write-Host "CopyEngine.cs  SRC=$srcHash"
Write-Host "CopyEngine.cs  DST=$dstHash"
if ($srcHash -ne $dstHash) { Write-Error "HASH MISMATCH: CopyEngine.cs" }

# Step 4: Verify SHA-256 for PttGlobalQuickExit.cs
$srcHash2 = (Get-FileHash "C:\WSGTA\universal-or-strategy\src\PropTraderTools\Features\PttGlobalQuickExit.cs" -Algorithm SHA256).Hash
$dstHash2 = (Get-FileHash "C:\Users\Mohammed Khalid\Documents\NinjaTrader 8\bin\Custom\AddOns\PropTraderTools\Features\PttGlobalQuickExit.cs" -Algorithm SHA256).Hash
Write-Host "PttGlobalQuickExit.cs  SRC=$srcHash2"
Write-Host "PttGlobalQuickExit.cs  DST=$dstHash2"
if ($srcHash2 -ne $dstHash2) { Write-Error "HASH MISMATCH: PttGlobalQuickExit.cs" }
```

**Report all four hash values in `ticket-1-completion.md`.** BUILD_PASS requires:
1. `git commit` pushed to `main`
2. Both SHA-256 pairs match
3. S1–S7 scans all PASS
4. All 6 T_B68 tests green

---

### CYC Summary (all new/changed methods)

| Method | File | CYC Before | CYC After | ≤ 8? |
|--------|------|-----------|-----------|------|
| `CancelQxBracketsForFollowers` (new) | `CopyEngine.cs` | N/A | **5** | PASS |
| `RelayBe` | `CopyEngine.cs` | 2 | **2** | PASS |
| `Execute` | `PttGlobalQuickExit.cs` | 5 | **6** | PASS |
| `CancelQxBrackets` | `CopyEngine.cs` | 6 | **6** | PASS (unchanged) |
| `PttQuickExit.Execute` | `PttQuickExit.cs` | unchanged | unchanged | PASS (not modified) |

---

### Deferred Items — Not In Scope

The following OPEN items are explicitly deferred and MUST NOT be addressed in this ticket:

| ID | Reason |
|----|--------|
| DW-B66-C-02 (DispatchCopy dedup Gate 5 StopLimit) | OPEN — B67+ |
| DW-B66-BE-01 (CancelQxBrackets cancels PTT-BE-Stop on QX) | OPEN — Director confirm |
| DW-B63-01 (spurious PTT-Copy on Sim102 after ATM fill) | OPEN — B67+ |
| DW-B58-01/02/03, DW-B54-01 | OPEN — future/blocked |
| PRE-EXISTING-01/02/03 | OPEN — pre-existing |

---

**Return**: TICKETS_COMPLETE
