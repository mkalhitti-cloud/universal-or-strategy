# B140-LaneA Ticket 1 Verification
## ptt-verifier | Phase 4b | Status: VERIFY_PASS

---

## 1. SCOPE LOCK

**CONFIRMED.**

`ticket-1-completion.md` line 8: "This session implemented **TICKET 1 ONLY**. No other tickets were read, referenced, or implemented."

Verifier session: read `ticket-1-completion.md`, `04-tickets.md` (Ticket 1 only), `02-architecture-plan.md`, `04-ticket-review.md`, and `src/PropTraderTools/CopyEngine.cs`. No other ticket completion files read. Scope lock honored.

---

## 2. Layer 2 vs Layer 3 Scan Comparison

All 7 scans independently re-run by verifier. Results compared against engineer's Layer 2 report in `ticket-1-completion.md`.

| Scan | Rule | Command | Layer 2 (Engineer) | Layer 3 (Verifier) | Match | Verdict |
|------|------|---------|--------------------|--------------------|-------|---------|
| SCAN-01 | JS-021 lock() | `Select-String -Pattern "lock\("` on CopyEngine.cs | 0 violations | 4 comment-only lines (no actual lock() statement) | MATCH | PASS |
| SCAN-02 | JS-033 async void | `Select-String -Pattern "async void "` on CopyEngine.cs | 0 hits | 0 hits | MATCH | PASS |
| SCAN-03 | JS-002 return null | `Select-String -Pattern "return null;"` on CopyEngine.cs | 0 new; 7 pre-existing (lines 1700, 2764, 2921, 4258, 4264, 4343, 5179) | Exactly same 7 lines; 0 in B140 change region (~2280-2292) | MATCH | PASS (pre-existing only) |
| SCAN-04 | JS-001 throw rethrow | `Select-String -Pattern "throw;"` on CopyEngine.cs | 0 hits | 0 hits | MATCH | PASS |
| SCAN-05 | ASCII-only | `Select-String -Pattern "[^\x00-\x7F]"` on CopyEngine.cs | 0 hits | 0 hits | MATCH | PASS |
| SCAN-06 | CYC <= 8 | Manual branch count on SyncFollowerBracket | CYC = 8 (7 decision points + base 1) | CYC = 8 (verified: base=1 + fo_null=1 + tickSize=1 + isStop_&&_IsAtmSTP=1 + isNullOrEmpty(Oco)=1 + !isStop_&&_IsAtmSTP=1 + isStop_&&_IsTrailingStop=1 + isStop=1 = 8) | MATCH | PASS (at limit) |
| SCAN-07 | Build clean | `dotnet build src/PropTraderTools/PropTraderTools.csproj` | 0 errors, 1 pre-existing xUnit2004 warning (B131Tests.cs:165) | 0 errors, 0 warnings | MINOR DELTA (no new violations; engineer's warning is in tests project, not PropTraderTools.csproj) | PASS |

**SCAN-07 delta note:** Engineer ran the full test suite and observed an xUnit2004 warning in `tests/PropTraderTools.Tests/B131Tests.cs:165`. The verifier's build target is `src/PropTraderTools/PropTraderTools.csproj` (the main assembly, not the tests project), which returns 0 warnings. The pre-existing warning is in the tests project only and was not introduced by B140. Not a VERIFY_FAIL.

**SCAN-03 note:** All 7 `return null;` hits are pre-existing. None are in the B140 change region (lines 2280-2292). B140 change is on a void code path (`SyncFollowerBracket` returns void; `acc.Change()` exception is absorbed via `StatusUpdate?.Invoke()`). No null return was introduced.

---

## 3. Implementation Check

File: `src/PropTraderTools/CopyEngine.cs`, lines 2280-2292.

### Source (verified at lines 2280-2292):

```csharp
if (isStop && IsAtmSTPOrder(fo)) // (3) DW-B134 + DW-B137
{
    if (!string.IsNullOrEmpty(fo.Oco)) // (3a) B140: OCO-linked -- Change preserves OCO partner
    {
        fo.StopPrice = newPrice;
        try { acc.Change(new Order[] { fo }); }
        catch (Exception ex)
        { StatusUpdate?.Invoke(acc.Name + ": ATM STP Change error: " + ex.Message); }
        return;
    }
    SyncAtmFollowerBracket(acc, fo, newPrice); // (3b) no OCO -- cancel+resubmit (existing path)
    return;
}
```

### Checklist

| Item | Expected | Found | Result |
|------|----------|-------|--------|
| Branch (3a) `if (!string.IsNullOrEmpty(fo.Oco))` present | YES | Line 2282 | PASS |
| Branch (3a) sets `fo.StopPrice = newPrice` | YES | Line 2284 | PASS |
| Branch (3a) calls `acc.Change(new Order[] { fo })` | YES | Line 2285 | PASS |
| Branch (3a) has try/catch invoking StatusUpdate on exception | YES | Lines 2285-2287 | PASS |
| Branch (3a) returns before reaching branch (3b) | YES | Line 2288 | PASS |
| Branch (3b) calls `SyncAtmFollowerBracket(acc, fo, newPrice)` | YES | Line 2290 | PASS |
| Comment "(3a) B140: OCO-linked -- Change preserves OCO partner" | YES | Line 2282 | PASS |
| Comment "(3b) no OCO -- cancel+resubmit (existing path)" | YES | Line 2290 | PASS |
| B140 reference appears only once in CopyEngine.cs | YES | grep confirms 1 hit (line 2282 only) | PASS |
| No other changes in CopyEngine.cs | YES | No other B140 markers; surrounding methods (SyncAtmFollowerBracket, IsAtmSTPOrder, SyncAtmFollowerTarget) unmodified | PASS |

All implementation checks: **PASS**

---

## 4. Test Verification

**Command:** `dotnet test tests/PropTraderTools.Tests/ --filter "T_B140" --verbosity normal`

**Result:**
```
Test Run Successful.
Total tests: 7
     Passed: 7
Total time: 0.5445 Seconds
```

| Test ID | Test Method | Result |
|---------|-------------|--------|
| T_B140_01 | `T_B140_01_SyncFollowerBracket_OcoLinked_CallsAccChange` | PASS |
| T_B140_02 | `T_B140_02_SyncFollowerBracket_EmptyOco_CallsSyncAtmFollowerBracket` | PASS |
| T_B140_03 | `T_B140_03_IsAtmSTPOrder_Stop1_ReturnsTrue` | PASS |
| T_B140_04 | `T_B140_04_IsAtmSTPOrder_Stop2_ReturnsTrue` | PASS |
| T_B140_05 | `T_B140_05_IsAtmSTPOrder_Stop3_ReturnsTrue` | PASS |
| T_B140_06 | `T_B140_06_OcoLinkedBranch_NoAccCancelCall` | PASS |
| T_B140_07 | `T_B140_07_AtmTargetBranch_RouteToSyncAtmFollowerTarget` | PASS |

**B140Tests.cs presence confirmed:** `Select-String -Pattern "T_B140_0" tests/PropTraderTools.Tests/B140Tests.cs` returns all 7 method names at the correct lines.

---

## 5. NT8 Verification Citations

### NT8-VERIFY-01 — acc.Change preserves OCO link

**Command:** `Select-String -Path "docs/standards/NT8_API_SURFACE.md" -Pattern "Change"`

**Result (line 151):**
```
| `Account.Change(Order[])` | B31 | Modifies stop price in-place (preserves ATM OCO link) |
```

**Conclusion:** B31 confirms `acc.Change(Order[])` modifies stop price in-place and preserves the ATM OCO link. The B140 branch (3a) correctly uses `acc.Change` instead of `acc.Cancel` to prevent OCO cascade.

---

### NT8-VERIFY-02 — fo.Oco property on NT8 Order

**Command:** `Select-String -Path "docs/standards/NT8_FULL_REFERENCE.md" -Pattern "Oco"`

**Result (lines 849-850):**
```
* Oco
* A string representing the OCO (one cancels other) id of an order
```

**Also line 772:**
```
* The property <order>.Oco WILL be appended with a suffix when the strategy transitions from history...
```

**Conclusion:** NT8_FULL_REFERENCE.md confirms `Oco` is a string property on the NT8 Order class representing the OCO group id. `fo.Oco` is a valid property access. The branch condition `!string.IsNullOrEmpty(fo.Oco)` is architecturally sound.

---

### NT8-VERIFY-03 — SIM Gate 1 Result

**NT8-VERIFY-03: SIM Gate 1 PENDING — requires director SIM run.**

Gate 1 procedure: drag leader stop -> confirm follower Stop1/Stop2 price updated in Order Grid -> confirm Target1/Target2 NOT cancelled after drag.

Gate 2 procedure: drag leader stop -> confirm follower Stop3 price updated via acc.Change -> confirm Target3 NOT cancelled.

Gate 3 procedure: perform two consecutive stop drags -> confirm Stop1/Stop2 update on both drags -> confirm no target cancellation on either drag.

**Status: Code-level verification complete. SIM verification requires live NT8 SIM environment with leader+follower accounts running. Merge is GATED on Gate 1 pass per ticket spec (Gate 1 FAIL = DW-B154, no fallback, Director resolution required).**

---

### NT8-VERIFY-04 — JS-DNA lock() scan (0 hits)

**Command:** `Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "lock\("`

**Result:**
```
309    // JS-021: ConcurrentDictionary -- lock-free. No lock() anywhere.
343    // ConcurrentDictionary: thread-safe without lock(). JS-021: no lock.
1735   // Value: ConcurrentBag<Order> -- thread-safe add, no lock().
3268   // JS-021: no lock() -- ConcurrentDictionary TryGetValue/TryRemove.
```

**Conclusion:** All 4 results are comment text only — no actual `lock(` statement. Zero JS-021 violations in CopyEngine.cs. PASS.

---

### NT8-VERIFY-05 — SyncFollowerBracket CYC = 8

**Method:** `SyncFollowerBracket` in `src/PropTraderTools/CopyEngine.cs` (lines 2254-2324)

**Manual McCabe Complexity Count** (codebase convention: compound conditions do not add extra branches; only decision statements counted):

| # | Line | Branch | Count |
|---|------|--------|-------|
| Base | — | — | 1 |
| 1 | 2269 | `if (fo == null)` | +1 = 2 |
| 2 | 2273 | `if (Math.Abs(newPrice - currentPrice) < tickSize)` | +1 = 3 |
| 3 | 2280 | `if (isStop && IsAtmSTPOrder(fo))` | +1 = 4 |
| 3a | 2282 | `if (!string.IsNullOrEmpty(fo.Oco))` [B140 NEW] | +1 = 5 |
| 3b | 2293 | `if (!isStop && IsAtmSTPOrder(fo))` | +1 = 6 |
| 4 | 2299 | `if (isStop && IsTrailingStop(fo))` | +1 = 7 |
| 5 | 2307 | `if (isStop)` | +1 = 8 |

**CYC = 8. At limit (JS-041 max = 8). PASS.**

Pre-B140 CYC was 7 (no branch 3a). B140 adds exactly 1 branch. CYC is at the hard limit. No further branching may be added to this method.

---

## 6. Cross-Check Summary

| Item | Engineer Layer 2 | Verifier Layer 3 | Match |
|------|-----------------|------------------|-------|
| SCAN-01: lock( | 0 violations | 0 violations (4 comment lines) | MATCH |
| SCAN-02: async void | 0 hits | 0 hits | MATCH |
| SCAN-03: return null; | 0 new; 7 pre-existing | 0 new; 7 pre-existing (same lines) | MATCH |
| SCAN-04: throw; | 0 hits | 0 hits | MATCH |
| SCAN-05: Non-ASCII | 0 hits | 0 hits | MATCH |
| SCAN-06: CYC | 8 (manual) | 8 (manual, verified) | MATCH |
| SCAN-07: build | 0 errors, 1 pre-existing warn | 0 errors, 0 warnings | MINOR DELTA (acceptable) |
| Implementation BEFORE/AFTER | Matches spec | Confirmed at lines 2280-2292 | MATCH |
| Tests T_B140_01..07 | All 7 PASS | All 7 PASS (verified live run) | MATCH |
| Sync script | 0 MISMATCH | Not re-run (verifier is READ-ONLY) | N/A |
| F5 gate | PENDING | PENDING (requires NT8 SIM) | N/A |

**No discrepancies that constitute VERIFY_FAIL.** The SCAN-07 minor delta (engineer reported 1 pre-existing warning in tests project, verifier's build of main .csproj shows 0 warnings) is not a new violation introduced by B140. Acceptable.

---

## 7. OVERALL VERDICT

| Check Category | Result |
|----------------|--------|
| Scope lock | CONFIRMED |
| 7-scan Layer 3 independent run | ALL PASS |
| Layer 2 vs Layer 3 discrepancies | NONE that constitute VERIFY_FAIL |
| Implementation matches ticket AFTER code exactly | PASS |
| All required comments present | PASS |
| No scope creep (B140 change confined to 9 lines in one method) | PASS |
| Tests T_B140_01..T_B140_07 all present and PASS | PASS |
| NT8-VERIFY-01 (B31 acc.Change preserves OCO) | CONFIRMED — NT8_API_SURFACE.md line 151 |
| NT8-VERIFY-02 (fo.Oco property on Order) | CONFIRMED — NT8_FULL_REFERENCE.md lines 849-850 |
| NT8-VERIFY-03 (SIM Gate 1) | PENDING — requires director SIM run (acceptable per protocol) |
| NT8-VERIFY-04 (lock( = 0 hits) | CONFIRMED — 0 lock() statements in CopyEngine.cs |
| NT8-VERIFY-05 (CYC = 8) | CONFIRMED — manual count = 8, at limit |

# OVERALL: VERIFY_PASS

**SIM Gate 1 is PENDING** and is a BLOCKING gate before merge per ticket spec. Code-level verification is complete and PASS. Director must run SIM Gate 1 (and Gates 2/3) before PR merge is authorized. If Gate 1 fails (acc.Change is a no-op on Stop brackets), merge is BLOCKED and DW-B154 is created — no fallback code to be implemented.

---

*Verification authored by ptt-verifier, B140-LaneA, Phase 4b.*
*Input artifacts: `src/PropTraderTools/CopyEngine.cs`, `docs/brain/B140-LaneA/02-architecture-plan.md`, `docs/brain/B140-LaneA/04-tickets.md` (Ticket 1), `docs/brain/B140-LaneA/ticket-1-completion.md`, `docs/brain/B140-LaneA/04-ticket-review.md`*
*READ-ONLY: no C# source files modified.*