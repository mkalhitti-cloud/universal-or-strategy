# B69-LaneA — Tickets

**Status**: TICKETS_COMPLETE
**Epic**: B69-LaneA
**Author**: ptt-architect
**Date**: 2026-08-13
**Source plan**: `docs/brain/B69-LaneA/02-architecture-plan.md` (REVIEW_PASS)

---

## T1 — B69-LaneA Fix FlattenOneAccount full-cancel + SubmitBeStop FullName + HandleEntryChange dedup preload

### Overview

| Field | Value |
|-------|-------|
| **Spec requirements** | DW-B69-01, DW-B69-02, DW-B69-03 |
| **Files modified** | `src/PropTraderTools/CopyEngine.cs` (edits), `src/PropTraderTools/CopyEngineTests.cs` (appends) |
| **Commit scope** | Single commit — one file, one concern per DW item, all 3 DW items in one pass |
| **Deploy step** | Copy to NinjaTrader bin + SHA-256 match verification (see §7) |

---

### Spec Requirement Summary

| DW ID | Severity | Description |
|-------|----------|-------------|
| **DW-B69-01** | P0 | `FlattenOneAccount`: replace name-gated `CancelQxBrackets` with name-agnostic `CancelAllAccountOrders`; add missing `acc.Submit(new[]{order})` after `CreateOrder` |
| **DW-B69-02** | P1 | `SubmitBeStop` line 512 and `FindPosition` line 1778: replace reference equality (`==`) with `FullName` string comparison + null-guard |
| **DW-B69-03** | P1 | `HandleEntryChange`: after `acc.Submit(new[]{order})`, preload `order.OrderId.ToString()` into `_dedupCache` at `newPrice` to close double-copy race window |

---

### Method Signatures

All methods are in `src/PropTraderTools/CopyEngine.cs`.

#### NEW — CancelAllAccountOrders
```csharp
// B69 DW-B69-01: name-agnostic cancel of all active orders on acc for instr.
// CYC=4: (1) null-guard, (2) foreach, (3) stateOk compound, (4) FullName gate.
// JS-021: no lock. JS-001: no throw. JS-002: void. ASCII-only.
internal void CancelAllAccountOrders(Account acc, NinjaTrader.Cbi.Instrument instr)
```

CYC=4 breakdown:

| Branch | Code path |
|--------|-----------|
| 1 | `if (acc == null \|\| instr == null) return;` |
| 2 | `foreach (Order o in acc.Orders)` |
| 3 | `stateOk` compound: `Working \| Initialized \| Submitted \| Accepted \| ChangeSubmitted` |
| 4 | `o.Instrument == null \|\| o.Instrument.FullName != instr.FullName` — FullName gate |

#### MODIFIED — FlattenOneAccount (signature unchanged)
```csharp
// CYC=4: (1) pos null/qty guard, (2) CancelAllAccountOrders, (3) action ternary, (4) try/catch.
// JS-021: no lock. JS-001: no throw in hot path. JS-002: void.
private void FlattenOneAccount(Account acc, Instrument instrument)
```

#### MODIFIED — SubmitBeStop (signature unchanged)
```csharp
// B69 DW-B69-02: pos-find uses FullName comparison (not reference equality).
// CYC unchanged from pre-B69.
internal void SubmitBeStop(Account acc, NinjaTrader.Cbi.Instrument instr, double bePrice, bool isLong)
```

#### MODIFIED — FindPosition (signature unchanged)
```csharp
// B69 DW-B69-02: FullName comparison with null-guard. CYC unchanged (no new branch).
private Position FindPosition(Account acc, Instrument instrument)
```

#### MODIFIED — HandleEntryChange (signature unchanged)
```csharp
// B69 DW-B69-03: _dedupCache preload after acc.Submit. CYC delta=0 (straight-line inside existing block).
private void HandleEntryChange(Order leaderOrder, CopyRule rule)
```

---

### Exact Changes (implement verbatim, in order)

All line numbers reference the **pre-change** state of `src/PropTraderTools/CopyEngine.cs`.

---

#### CHANGE A — Delete line 450 (CancelQxBrackets stale comment)

**Location:** `CopyEngine.cs` line 450

**Action:** Delete exactly this line:
```
// Also called by FlattenOneAccount (B67 DW-B67-01) before market order submission.
```

**Rationale:** `FlattenOneAccount` will no longer call `CancelQxBrackets` after DW-B69-01; this
comment becomes a lie and must be removed before the method body change to avoid misleading readers.

---

#### CHANGE B — Insert new method CancelAllAccountOrders after line 470

**Location:** `CopyEngine.cs` — insert after line 470 (after the end of the `CancelQxBrackets`
block), before line 472.

**Insert the following block verbatim:**
```csharp
    // B69 DW-B69-01: CancelAllAccountOrders -- cancel every active order on acc for instr
    // before submitting a market flatten. No name filter -- all order names cancelled.
    // NT8 precedent: @2Custom-0909edcc EmergencyFlattenSingleFleetAccount [938-EF-GUARD]:
    //   "Step 1: Cancel ALL working orders on this instrument for this account."
    //   States: Working|Submitted|Accepted|ChangePending|ChangeSubmitted.
    // CYC=4: null-guard(1) + foreach(2) + stateOk(3) + instrument-name(4). JS-021: no lock.
    // JS-001: no throw. JS-002: void. ASCII-only.
    internal void CancelAllAccountOrders(Account acc, NinjaTrader.Cbi.Instrument instr)
    {
        if (acc == null || instr == null) return;                              // (1)
        var toCancel = new System.Collections.Generic.List<Order>();
        foreach (Order o in acc.Orders)                                        // (2)
        {
            bool stateOk = o.OrderState == OrderState.Working
                        || o.OrderState == OrderState.Initialized
                        || o.OrderState == OrderState.Submitted
                        || o.OrderState == OrderState.Accepted
                        || o.OrderState == OrderState.ChangeSubmitted;
            if (!stateOk) continue;                                            // (3)
            if (o.Instrument == null
                || o.Instrument.FullName != instr.FullName) continue;          // (4)
            toCancel.Add(o);
        }
        if (toCancel.Count == 0) return;
        try { acc.Cancel(toCancel); } catch { }
    }
```

---

#### CHANGE C — Update FlattenOneAccount comment block (lines 1467-1474) and body

**Sub-change C1 — Replace comment lines 1467-1474** with:
```csharp
    // B28 T1 -- FlattenOneAccount: per-account market flatten helper.
    // B67 DW-B67-01: cancel follower ATM+QX brackets BEFORE submitting market order.
    // B69 DW-B69-01: widened from CancelQxBrackets to cancel ALL active orders (name-agnostic).
    // NT8 precedent: @2Custom-0909edcc FlattenPositionByName V8.31 comment:
    //   "Cancel ALL bracket orders first to prevent race conditions."
    // Rithmic/Apex: incoming market order conflicts with live OCO bracket at broker layer
    //   -> "Close operation failed. Operation timed out." without this cancel step.
    // CYC=4: (1) pos null/qty guard, (2) CancelAllAccountOrders, (3) action ternary, (4) try/catch.
    // JS-021: no lock. JS-001: no throw in hot path. JS-002: void.
```

**Sub-change C2 — Replace line 1483:**

OLD:
```csharp
                CancelQxBrackets(acc, instrument);
```

NEW:
```csharp
                CancelAllAccountOrders(acc, instrument);
```

**Sub-change C3 — Replace lines 1487-1491 (the CreateOrder block):**

OLD (lines 1487-1491):
```csharp
                acc.CreateOrder(
                    instrument, action, OrderType.Market, OrderEntry.Manual,
                    TimeInForce.Gtc, pos.Quantity, 0, 0, null, "PTT-Flatten",
                    DateTime.MaxValue, null);
                StatusUpdate?.Invoke(acc.Name + ": flatten " + pos.Quantity);
```

NEW:
```csharp
                var order = acc.CreateOrder(
                    instrument, action, OrderType.Market, OrderEntry.Manual,
                    TimeInForce.Gtc, pos.Quantity, 0, 0, null, "PTT-Flatten",
                    DateTime.MaxValue, null);
                if (order != null)
                    acc.Submit(new[] { order });
                StatusUpdate?.Invoke(acc.Name + ": flatten " + pos.Quantity);
```

---

#### CHANGE D — Update SubmitBeStop line 512

**Location:** `CopyEngine.cs` line 512

Add comment to the region header (lines 498-506) referencing DW-B69-02:
```
// B69 DW-B69-02: pos-find uses FullName comparison (not reference equality).
```
(Place at end of the existing comment block, line 506 or nearest natural position.)

**Replace line 512:**

OLD:
```csharp
            if (p.Instrument == instr) { pos = p; break; }    // (3)
```

NEW:
```csharp
            if (p.Instrument != null                                                          // (3)
                && p.Instrument.FullName == instr.FullName) { pos = p; break; }
```

---

#### CHANGE E — Update FindPosition line 1778

**Location:** `CopyEngine.cs` line 1778

**Replace:**

OLD:
```csharp
            if (p.Instrument == instrument) return p;
```

NEW:
```csharp
            if (p.Instrument != null && p.Instrument.FullName == instrument.FullName) return p;
```

---

#### CHANGE F — Update HandleEntryChange block (lines 1127-1129)

**Location:** `CopyEngine.cs` lines 1127-1129

**Replace:**

OLD:
```csharp
            if (order != null)                                                       // (7)
                acc.Submit(new[] { order });
            StatusUpdate?.Invoke(acc.Name + ": entry dragged -> " + newPrice);
```

NEW:
```csharp
            if (order != null)                                                       // (7)
            {
                acc.Submit(new[] { order });
                // B69 DW-B69-03: preload new orderId into _dedupCache at newPrice.
                // Prevents the new order's Accepted event from re-entering DispatchCopy
                // (same-account double-copy guard, lightweight FSM-in-flight equivalent).
                // Ref: @2Custom PropagateFollowerEntryReplace Build 947 -- PendingCancel absorb.
                _dedupCache[order.OrderId.ToString()] = newPrice;
            }
            StatusUpdate?.Invoke(acc.Name + ": entry dragged -> " + newPrice);
```

---

#### CHANGE G — Append 7 [Fact] tests to CopyEngineTests.cs

**Location:** `src/PropTraderTools/CopyEngineTests.cs` — insert **after line 3552** (last test
method body closing brace), **before line 3554** (class closing brace). Do NOT modify any
existing test.

Append the following block verbatim:

```csharp
    // =====================================================================
    // B69-LaneA Tests: DW-B69-01 / DW-B69-02 / DW-B69-03
    // =====================================================================

    [Fact]
    public void T_B69_01_CancelAllAccountOrders_cancels_PTT_Copy_orders()
    {
        // Arrange: Working order named PTT-Entry (not matched by CancelQxBrackets name filter)
        var instr = StubInstrument("ES 09-26");
        var order = StubOrder("PTT-Entry", OrderState.Working, instr);
        var acc   = StubAccount(new[] { order });
        var engine = CreateEngine();

        // Act
        engine.CancelAllAccountOrders(acc, instr);

        // Assert: acc.Cancel called with list containing that one order
        Assert.True(acc.CancelCallCount == 1, "acc.Cancel must be called exactly once");
        Assert.Single(acc.LastCancelledOrders);
        Assert.Same(order, acc.LastCancelledOrders[0]);
    }

    [Fact]
    public void T_B69_02_CancelAllAccountOrders_cancels_ChangeSubmitted_orders()
    {
        // Arrange: ChangeSubmitted order (absent from CancelQxBrackets state set)
        var instr = StubInstrument("NQ 09-26");
        var order = StubOrder("PTT-Limit", OrderState.ChangeSubmitted, instr);
        var acc   = StubAccount(new[] { order });
        var engine = CreateEngine();

        // Act
        engine.CancelAllAccountOrders(acc, instr);

        // Assert
        Assert.Equal(1, acc.CancelCallCount);
        Assert.Single(acc.LastCancelledOrders);
        Assert.Same(order, acc.LastCancelledOrders[0]);
    }

    [Fact]
    public void T_B69_03_CancelAllAccountOrders_skips_Filled_orders()
    {
        // Arrange: one Filled order + one Working order, same instrument
        var instr        = StubInstrument("ES 09-26");
        var filledOrder  = StubOrder("PTT-Entry", OrderState.Filled,   instr);
        var workingOrder = StubOrder("PTT-Entry", OrderState.Working,  instr);
        var acc          = StubAccount(new[] { filledOrder, workingOrder });
        var engine       = CreateEngine();

        // Act
        engine.CancelAllAccountOrders(acc, instr);

        // Assert: only the Working order is in the cancel list
        Assert.Equal(1, acc.CancelCallCount);
        Assert.Single(acc.LastCancelledOrders);
        Assert.Same(workingOrder, acc.LastCancelledOrders[0]);
        Assert.DoesNotContain(filledOrder, acc.LastCancelledOrders);
    }

    [Fact]
    public void T_B69_04_CancelAllAccountOrders_skips_different_instrument()
    {
        // Arrange: two Working orders on different instruments
        var targetInstr  = StubInstrument("ES 09-26");
        var otherInstr   = StubInstrument("NQ 09-26");
        var targetOrder  = StubOrder("PTT-Entry", OrderState.Working, targetInstr);
        var otherOrder   = StubOrder("PTT-Entry", OrderState.Working, otherInstr);
        var acc          = StubAccount(new[] { targetOrder, otherOrder });
        var engine       = CreateEngine();

        // Act
        engine.CancelAllAccountOrders(acc, targetInstr);

        // Assert: only the target-instrument order is cancelled
        Assert.Equal(1, acc.CancelCallCount);
        Assert.Single(acc.LastCancelledOrders);
        Assert.Same(targetOrder, acc.LastCancelledOrders[0]);
        Assert.DoesNotContain(otherOrder, acc.LastCancelledOrders);
    }

    [Fact]
    public void T_B69_05_SubmitBeStop_finds_position_by_FullName()
    {
        // Arrange: two Instrument objects with equal FullName but distinct references
        var instrA = StubInstrument("ES 09-26");   // position holds this reference
        var instrB = StubInstrument("ES 09-26");   // SubmitBeStop called with this reference
        Assert.NotSame(instrA, instrB);             // reference inequality confirmed

        var pos    = StubPosition(instrA, quantity: 1, isLong: true);
        var acc    = StubAccountWithPositions(new[] { pos });
        var engine = CreateEngine();

        // Act: pass instrB (different reference, same FullName)
        engine.SubmitBeStop(acc, instrB, bePrice: 4500.0, isLong: true);

        // Assert: CreateOrder was called (position was found by FullName)
        Assert.True(acc.CreateOrderCallCount >= 1,
            "CreateOrder must be called when position found by FullName");
        Assert.True(acc.SubmitCallCount >= 1,
            "acc.Submit must be called after CreateOrder");
    }

    [Fact]
    public void T_B69_06_HandleEntryChange_preloads_new_orderId_into_dedupCache()
    {
        // Arrange: leader order in _dedupCache; follower account has a matching entry order
        var oldOrderId = "OLD-ID-001";
        var newOrderId = "NEW-ID-002";
        double newPrice = 4510.25;

        var engine = CreateEngine();
        engine.InjectDedupCacheEntry(oldOrderId, 4500.0);

        var leaderOrder   = StubLeaderOrder(orderId: oldOrderId, price: 4500.0);
        var newFollowOrder = StubOrder("PTT-Entry", OrderState.Working,
                                       StubInstrument("ES 09-26"), orderId: newOrderId);
        var acc = StubAccountWithActiveEntry(leaderOrder, newFollowOrder);
        var rule = StubCopyRule(acc);

        // Act
        engine.HandleEntryChange(leaderOrder, rule);

        // Assert: old key removed, new key present at newPrice
        Assert.False(engine.DedupCacheContains(oldOrderId),
            "_dedupCache must not contain old OrderId after HandleEntryChange");
        Assert.True(engine.DedupCacheContains(newOrderId),
            "_dedupCache must contain new OrderId after HandleEntryChange");
        Assert.Equal(newPrice, engine.DedupCacheGet(newOrderId));
    }

    [Fact]
    public void T_B69_07_CancelAllAccountOrders_null_acc_noOp()
    {
        // Arrange: null account
        var instr  = StubInstrument("ES 09-26");
        var engine = CreateEngine();

        // Act + Assert: no exception thrown
        var ex = Record.Exception(() => engine.CancelAllAccountOrders(null, instr));
        Assert.Null(ex);
    }
```

---

### 7-Scan Checklist (SCAN-01 through SCAN-07)

The engineer MUST run all 7 scans after implementing the changes and confirm 0 hits / PASS for
each before marking the ticket complete. Results must be pasted into `ticket-1-completion.md`.

---

#### SCAN-01 — No `lock()` in new code
```powershell
Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "lock\s*\(" | Where-Object { $_.LineNumber -ge 470 -and $_.LineNumber -le 490 }
```
**Expected:** 0 hits in the newly inserted `CancelAllAccountOrders` method block.

Also verify full-file:
```powershell
Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "lock\s*\("
```
**Expected:** 0 new hits versus pre-B69 baseline. JS-021 compliance.

---

#### SCAN-02 — No `throw new` in new code
```powershell
Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "throw\s+new" | Where-Object { $_.LineNumber -ge 470 -and $_.LineNumber -le 490 }
```
**Expected:** 0 hits. `CancelAllAccountOrders` uses `try { acc.Cancel(toCancel); } catch { }` — no re-throw. JS-001 compliance.

---

#### SCAN-03 — No `p.Instrument == instr` reference equality in SubmitBeStop
```powershell
Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "p\.Instrument\s*==\s*instr"
```
**Expected:** 0 hits. DW-B69-02 FullName fix applied at line 512.

---

#### SCAN-04 — No `p.Instrument == instrument` reference equality in FindPosition
```powershell
Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "p\.Instrument\s*==\s*instrument"
```
**Expected:** 0 hits. DW-B69-02 FullName fix applied at line 1778.

---

#### SCAN-05 — CYC audit on all modified/new methods
```powershell
python scripts/complexity_audit.py src/PropTraderTools/CopyEngine.cs
```
**Expected CYC values (must not exceed 8):**

| Method | Expected CYC | Max allowed |
|--------|-------------|-------------|
| `CancelAllAccountOrders` | 4 | 8 |
| `FlattenOneAccount` | 4 | 8 |
| `SubmitBeStop` | 7 | 8 |
| `HandleEntryChange` | 7 | 8 |
| `FindPosition` | 1 | 8 |

If `complexity_audit.py` is not available, count branches manually:
- `CancelAllAccountOrders`: null-guard(1) + foreach(2) + stateOk(3) + FullName-gate(4) = **4**
- `FlattenOneAccount`: pos-guard(1) + CancelAllAccountOrders-branch(2) + action-ternary(3) + try/catch(4) = **4**
- `SubmitBeStop`: verify unchanged from pre-B69 baseline (architect annotated CYC=7 pre-B69)
- `HandleEntryChange`: verify unchanged from pre-B69 baseline (architect annotated CYC=7 pre-B69)

---

#### SCAN-06 — ASCII-only on new string literals
```powershell
# Check CancelAllAccountOrders insertion range for non-ASCII characters
$content = [System.IO.File]::ReadAllBytes("src/PropTraderTools/CopyEngine.cs")
$text = [System.Text.Encoding]::UTF8.GetString($content)
$lines = $text -split "`n"
# Examine lines in the CancelAllAccountOrders insertion area (~470-490 post-insertion)
$lines[469..490] | ForEach-Object { if ($_ -match '[^\x00-\x7F]') { Write-Host "NON-ASCII: $_" } }
```
**Expected:** 0 non-ASCII characters in new code. All comment text and string literals are
ASCII-only per project mandate.

---

#### SCAN-07 — No `async void` in new code
```powershell
Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "async\s+void\s+\w+" | Where-Object { $_.LineNumber -ge 470 -and $_.LineNumber -le 490 }
```
**Expected:** 0 hits in the `CancelAllAccountOrders` insertion block. All new and modified
methods are synchronous `void` or `internal void`. JS-033 compliance.

Full-file check:
```powershell
Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "async\s+void\s+"
```
**Expected:** 0 new hits versus pre-B69 baseline.

---

### xUnit Test Summary

All 7 tests are appended to `src/PropTraderTools/CopyEngineTests.cs` after line 3552 (before
the closing class brace at lines 3554-3555). Test framework: xUnit `[Fact]` only.

| Test | DW | What it asserts |
|------|----|-----------------|
| `T_B69_01_CancelAllAccountOrders_cancels_PTT_Copy_orders` | DW-B69-01 | `Working` order with non-Qx name is cancelled (name filter bypassed) |
| `T_B69_02_CancelAllAccountOrders_cancels_ChangeSubmitted_orders` | DW-B69-01 | `ChangeSubmitted` state is in the cancel set |
| `T_B69_03_CancelAllAccountOrders_skips_Filled_orders` | DW-B69-01 | `Filled` order is NOT in the cancel list; only `Working` order is |
| `T_B69_04_CancelAllAccountOrders_skips_different_instrument` | DW-B69-01 | Only the target-instrument order is cancelled; other-instrument order is skipped |
| `T_B69_05_SubmitBeStop_finds_position_by_FullName` | DW-B69-02 | Position found when `Instrument` is a distinct object with equal `FullName`; `CreateOrder` + `Submit` called |
| `T_B69_06_HandleEntryChange_preloads_new_orderId_into_dedupCache` | DW-B69-03 | Old `OrderId` removed from `_dedupCache`; new `OrderId` present at `newPrice` after `HandleEntryChange` |
| `T_B69_07_CancelAllAccountOrders_null_acc_noOp` | DW-B69-01 | `null` acc returns without throw; no `acc.Cancel` call attempted |

---

### Deploy Step

After the engineer verifies all 7 scans pass and `dotnet test` is green:

```powershell
# Step 1: Build
dotnet build src/PropTraderTools/PropTraderTools.csproj -c Release

# Step 2: Sync hard-link to NinjaTrader bin
powershell -File .\deploy-sync.ps1

# Step 3: SHA-256 match verification
# Compute hash of source DLL
$srcHash = (Get-FileHash "src/PropTraderTools/bin/Release/net8.0/PropTraderTools.dll" -Algorithm SHA256).Hash
# Compute hash of NinjaTrader hard-link target
$ntHash  = (Get-FileHash "$env:USERPROFILE\Documents\NinjaTrader 8\bin\Custom\PropTraderTools.dll" -Algorithm SHA256).Hash
# Assert match
if ($srcHash -ne $ntHash) { throw "SHA-256 mismatch: deploy-sync.ps1 did not hard-link correctly" }
Write-Host "SHA-256 MATCH: $srcHash"
```

**Expected:** Both hashes identical. If mismatch, re-run `deploy-sync.ps1` and verify the
hard-link target path.

---

### JS-DNA Compliance Summary

| Rule | Requirement | Status |
|------|-------------|--------|
| JS-021 | No `lock()` anywhere in new code | PASS — `_dedupCache` is `ConcurrentDictionary`; NT8 broker calls not our lock |
| JS-001 | No `throw` in hot-path dispatch | PASS — `try { acc.Cancel(toCancel); } catch { }` swallows; no re-throw |
| JS-002 | No new `return null` sites | PASS — `FindPosition` retains pre-existing contract; not a new null return site |
| JS-033 | No `async void` | PASS — all new/modified methods are synchronous `void` |
| JS-036/037 | No heap alloc on tick hot-path | PASS — `new List<Order>()` and `new[]{order}` are broker-event paths, not per-tick |
| ASCII-only | No Unicode/emoji/curly quotes in identifiers or literals | PASS |
| PTT- prefix | All `CreateOrder` order names use `PTT-` prefix | PASS — `"PTT-Flatten"` unchanged |
| No `DateTime.Now` | Use `DateTime.UtcNow` or `DateTime.MaxValue` | PASS — `DateTime.MaxValue` in `CreateOrder` is unchanged |
| No FontFamily/hex | No hardcoded hex colors or FontFamily | PASS — backend methods only |
| CYC <= 8 | Every method within 8 branches | PASS — max CYC=4 for new method; all modified methods within limit |
| FullName identity | Instrument comparison via `FullName` | PASS — all new comparison sites use `FullName` |

---

### Ticket Completion Checklist (engineer must check all before closing T1)

- [ ] CHANGE A applied: line 450 deleted (stale comment removed from `CancelQxBrackets`)
- [ ] CHANGE B applied: `CancelAllAccountOrders` inserted after line 470
- [ ] CHANGE C1 applied: `FlattenOneAccount` comment block lines 1467-1474 replaced
- [ ] CHANGE C2 applied: line 1483 `CancelQxBrackets` → `CancelAllAccountOrders`
- [ ] CHANGE C3 applied: lines 1487-1491 `CreateOrder` block now captures `var order` + `acc.Submit`
- [ ] CHANGE D applied: `SubmitBeStop` line 512 uses `FullName` comparison with null-guard
- [ ] CHANGE E applied: `FindPosition` line 1778 uses `FullName` comparison with null-guard
- [ ] CHANGE F applied: `HandleEntryChange` block adds `_dedupCache[order.OrderId.ToString()] = newPrice;`
- [ ] CHANGE G applied: 7 `[Fact]` tests appended to `CopyEngineTests.cs` after line 3552
- [ ] SCAN-01 PASS: 0 `lock(` hits in new code
- [ ] SCAN-02 PASS: 0 `throw new` hits in new code
- [ ] SCAN-03 PASS: 0 `p.Instrument == instr` hits
- [ ] SCAN-04 PASS: 0 `p.Instrument == instrument` hits
- [ ] SCAN-05 PASS: CYC audit — all methods within limits
- [ ] SCAN-06 PASS: ASCII-only — 0 non-ASCII characters in new code
- [ ] SCAN-07 PASS: 0 `async void` hits in new code
- [ ] `dotnet build` — 0 errors, 0 new warnings
- [ ] `dotnet test` — all 7 new tests GREEN; no regressions
- [ ] `deploy-sync.ps1` — ran; SHA-256 match verified
- [ ] `ticket-1-completion.md` written with SCAN output pasted in
