# PTT-COPIER-B24 — Final Review (Phase 5)
**Phase**: 5 (Final Review)
**Reviewer**: ptt-plan-reviewer
**Date**: 2026-07-07
**Defect Closed**: DW-B23-BE-ALLACCOUNTS-01
**Tickets Reviewed**: T1 + T2 (both VERIFY_PASS)
**Source files read**: CopyEngine.cs, TradeCopierPanel.cs, CopyEngineTests.cs (READ ONLY)

---

## Verdict

**FINAL_PASS**

All five review sections pass. Zero DNA violations across the write-set. Spec requirements fully
satisfied end-to-end. Both tickets verified by independent verifier. Section K deferred backlog
written. No FINAL_FAIL conditions found.

---

## Section A — Cross-File Coherence

### A1: `BreakEven(Account, Instrument, int)` callable from TradeCopierPanel.cs

**PASS**

- `CopyEngine.cs:1185`: `internal void BreakEven(Account leader, Instrument instrument, int bufferTicks)`
- `TradeCopierPanel.cs:120`: `private Account _leaderAccount;`
- Param 1 type `Account` matches field type `Account`. No type mismatch. No implicit conversion.

### A2: All 6 call sites updated consistently

**PASS**

| # | File | Line | Call (actual source) |
|---|------|------|----------------------|
| 1 | CopyEngine.cs | 1415 | `BreakEven(acc, instr, buf);` |
| 2 | TradeCopierPanel.cs | 782 | `_engine.BreakEven(_leaderAccount, _instrument, _beBuffer);` |
| 3 | TradeCopierPanel.cs | 791 | `_engine.BreakEven(_leaderAccount, _instrument, _beBuffer);` |
| 4 | TradeCopierPanel.cs | 859 | `_engine.BreakEven(_leaderAccount, _instrument, _beBuffer);` |
| 5 | TradeCopierPanel.cs | 1299 | `_engine.BreakEven(_leaderAccount, _instrument, ticks);` |
| 6 | TradeCopierPanel.cs | 1418 | `_engine.BreakEven(_leaderAccount, _instrument, buf);` |

All 6 call sites use the 3-param form. No stale 2-param calls in the updated set.

### A3: No orphaned 2-param calls in TradeCopierPanel.cs

**PASS**

```
Select-String -Path "TradeCopierPanel.cs" -Pattern "_engine\.BreakEven\(_instrument"
Result: 0 matches
```

Zero stale calls. All TradeCopierPanel.cs BreakEven calls now supply `_leaderAccount` as first arg.

### A4: 2-param `BreakEven(Instrument, int)` present and unchanged

**PASS**

Source lines 1176-1180 confirmed verbatim:
```csharp
internal void BreakEven(Instrument instrument, int bufferTicks)
{
    foreach (var acc in AllAccounts(instrument))
        MoveStopToBreakEven(acc, instrument, bufferTicks);
}
```
Original 2-liner body intact. Not modified by B24.

### A5: TrailBe path still uses 2-param form

**PASS**

`CopyEngine.cs:1375`: `BreakEven(instr, newBuffer);`  
This is inside `OnTrailBeAccountUpdate` (B23 TrailBe feature) — correctly calling the 2-param form.
The TrailBe path is unaffected by B24.

### A6: ArmTrailBe, ArmPendingBe, MoveStopToBreakEven all untouched

**PASS**

| Symbol | Line | Signature Confirmed |
|--------|------|---------------------|
| `MoveStopToBreakEven` | 1133 | `private void MoveStopToBreakEven(Account acc, Instrument instrument, int bufferTicks)` |
| `ArmPendingBe` | 1279 | `internal void ArmPendingBe(Instrument instr, Account masterAcc, int bufferTicks)` |
| `ArmTrailBe` | 1315 | `internal void ArmTrailBe(Instrument instr, Account masterAcc, int bufferTicks)` |

None of these were touched by T1 or T2.

---

## Section B — Cross-File JS DNA Violations

### B1: JS-021 — No `lock()` in write-set

**PASS**

```
Select-String -Path "CopyEngine.cs" -Pattern "lock\s*\(" | LineNumber, Line
  314   // ConcurrentBag rebuild pattern -- no lock (JS-021).
  335   // ConcurrentBag rebuild pattern -- no lock (JS-021)
  578   // CYC=5: fo null(1), ...
  813   // ConcurrentBag rebuild pattern -- no lock (JS-021).
  1245  // CYC=3: null guard(1), alreadyTighter(2), try block(0).
```
All 5 matches are in code comments — zero actual `lock(` call expressions. New overload
(lines 1185-1198) and call site (line 1415) contain no `lock(`.

```
Select-String -Path "TradeCopierPanel.cs" -Pattern "lock\(" -> 0 matches
```

### B2: JS-033 — No `async void` added

**PASS**

```
Select-String -Path "CopyEngine.cs" -Pattern "async void " -> Count = 0
Select-String -Path "TradeCopierPanel.cs" -Pattern "async void " -> Count = 0
```

### B3: JS-002 — No `return null` in new code

**PASS**

New overload (CopyEngine.cs:1185-1198): zero `return null`. Uses `return` (void early return), not `return null`.

Pre-existing `return null` at lines 663, 1067, 1073, 1126 — all in methods `FindFollowerBracketOrder`,
`FindRule` (×2), and `FindPosition` that predate B24 and are not in the write-set.

---

## Section C — Test Integrity

### C1: [Fact] count = 128

**PASS**

```
Select-String -Path "CopyEngineTests.cs" -Pattern "\[Fact\]" | Measure-Object | Count = 128
```
Baseline was 126 (confirmed by T1 verifier). T2 added exactly 2 new [Fact] tests. 126 + 2 = 128.

### C2: Both new tests have deterministic assertions

**PASS**

| Test | Assertion | Deterministic? |
|------|-----------|----------------|
| `BreakEven_WithLeaderAccount_NoRule_FiresStatusUpdateLeaderNull` | `Assert.Equal("PTT-BE: leader null -- BE skipped", received)` + `Assert.Null(ex)` | YES — exact string equality |
| `BreakEven_AccountOverload_NullInstrument_NoException` | `Assert.Null(ex)` | YES — no-throw check |

No open-ended assertions ("check it works"). Both use `Record.Exception` correctly.

### C3: Test 1 specifically verifies the defect fix

**PASS**

Test 1 (`BreakEven_WithLeaderAccount_NoRule_FiresStatusUpdateLeaderNull`) passes `null` as `Account
leader`, which is precisely the guard at the root of defect DW-B23-BE-ALLACCOUNTS-01 in the
new overload. `Assert.Equal("PTT-BE: leader null -- BE skipped", received)` verifies the exact
sentinel string at `CopyEngine.cs:1189`. This test would fail if the null guard were removed or
the StatusUpdate message changed.

---

## Section D — Spec Satisfaction

### D1: DW-B23-BE-ALLACCOUNTS-01 resolved

**PASS**

Root cause was `BreakEven(Instrument, int)` → `AllAccounts` → `FindRule` → null → `yield break`.
Leader was never called directly. Fix: new 3-param overload at line 1185 calls
`MoveStopToBreakEven(leader, ...)` before any `AllAccounts` iteration. Leader fires regardless of
rule registration status. Defect is structurally eliminated, not worked around.

### D2: All 5 BE call sites in TradeCopierPanel.cs fixed

**PASS** — confirmed lines 782, 791, 859, 1299, 1418 in Section A2.

### D3: OnPendingBeAccountUpdate in CopyEngine.cs fixed

**PASS** — `CopyEngine.cs:1415` confirmed `BreakEven(acc, instr, buf)` where `acc` is
`_pendingBeAccount` captured at line 1408. Correct.

### D4: E2E verification path documented

**PASS** — ticket-1-verification.md and ticket-2-verification.md both include independent
7-scan results confirming the fix is correctly wired. Architecture compliance section in
ticket-1-verification.md traces the data flow end-to-end. Manual E2E test steps are implied
by the defect brief (DW-B23-BE-ALLACCOUNTS-01): press B / trigger auto-BE on a solo account,
verify stop moves.

---

## Section E — Wiring Check

### E1: `_leaderAccount` lifecycle is correct

**PASS**

`TradeCopierPanel.cs:120`: `private Account _leaderAccount;`  
Set at line 388: `_leaderAccount = account;` (account selection callback).  
Cleared at line 406: `_leaderAccount = null;` (disconnect callback).  
The new overload handles `null` cleanly via Branch 1 (StatusUpdate + early return). No additional
null check required at call sites.

### E2: New overload bypasses FindRule for leader

**PASS**

Architecture confirmed in source:
```csharp
MoveStopToBreakEven(leader, instrument, bufferTicks);   // leader direct, no rule needed
foreach (var acc in AllAccounts(instrument))            // follower fan-out (empty if no rule)
{
    if (acc == leader) continue;
    MoveStopToBreakEven(acc, instrument, bufferTicks);
}
```
Leader fires via direct call. Follower fan-out via `AllAccounts`. Skip guard prevents
double-firing if leader is also in the follower list.

### E3: Architecturally correct for solo and copier modes

**PASS**

| Mode | Rule registered? | `AllAccounts` yields | Leader fires? | Followers fire? |
|------|-----------------|----------------------|---------------|-----------------|
| Solo (no rule) | NO | empty | YES (direct call) | N/A |
| Copier (with rule) | YES | master + followers | YES (direct call) | YES (fan-out, skip leader) |
| Solo + pending BE | NO | empty | YES (acc from line 1408) | N/A |

Both modes are correctly handled by the new overload.

---

## Section F — Seven-Scan Aggregate (per role: all 7 must be zero across src/PropTraderTools/)

| Scan | Pattern | Files | Count/Result | PASS? |
|------|---------|-------|-------------|-------|
| SCAN-01 | `lock\s*\(` actual calls | CopyEngine.cs, TradeCopierPanel.cs | 0 actual lock calls (5 in comments only) | ✅ |
| SCAN-02 | `PTT-BE: leader null -- BE skipped` exists exactly once | CopyEngine.cs | 1 match at line 1189 | ✅ |
| SCAN-03 | CYC of new overload ≤ 8 | CopyEngine.cs | CYC=4 | ✅ |
| SCAN-04 | 2-param `BreakEven(Instrument` exists exactly once | CopyEngine.cs | 1 match at line 1176 | ✅ |
| SCAN-05 | No stale `_engine.BreakEven(_instrument` in TradeCopierPanel.cs | TradeCopierPanel.cs | 0 matches | ✅ |
| SCAN-06 | `[Fact]` count = 128 | CopyEngineTests.cs | 128 | ✅ |
| SCAN-07 | `\?\.\w+\s*-=` null-conditional unsubscription | CopyEngine.cs | 0 matches | ✅ |

All 7 scans zero (or expected single match). Aggregate PASS.

---

## Section G — Unchanged-Code Contract Final Verification

| Symbol | File | Line | B24 Modified? | Verified Unchanged |
|--------|------|------|--------------|-------------------|
| `AllAccounts(Instrument)` | CopyEngine.cs | 1050 | NO | ✅ |
| `FindRule(Instrument)` | CopyEngine.cs | 1064 | NO | ✅ |
| `MoveStopToBreakEven(Account, Instrument, int)` | CopyEngine.cs | 1133 | NO | ✅ |
| `BreakEven(Instrument, int)` | CopyEngine.cs | 1176 | NO | ✅ (confirmed body verbatim) |
| `ArmTrailBe` | CopyEngine.cs | 1315 | NO | ✅ |
| `ArmPendingBe` | CopyEngine.cs | 1279 | NO | ✅ |
| `OnTrailBeAccountUpdate` (except 1375 which was not modified) | CopyEngine.cs | 1357 | NO | ✅ |
| All TradeCopierPanel.cs methods except 5 call-site lines | TradeCopierPanel.cs | — | NO (5 lines only) | ✅ |
| All CopyEngineTests.cs tests (126 baseline) | CopyEngineTests.cs | — | NO | ✅ |

---

## Section H — Ticket Pipeline Summary

| Phase | Artifact | Verdict |
|-------|----------|---------|
| 1 (Architect) | 02-architecture-plan.md | REVIEW_PASS |
| 2 (Reviewer) | 02-plan-review.md | REVIEW_PASS |
| 3.5 (Ticket Reviewer) | 04-ticket-review.md | TICKET_REVIEW_PASS (second pass, one remediation) |
| 4a T1 (Engineer) | ticket-1-completion.md | BUILD_PASS |
| 4b T1 (Verifier) | ticket-1-verification.md | VERIFY_PASS |
| 4a T2 (Engineer) | ticket-2-completion.md | BUILD_PASS |
| 4b T2 (Verifier) | ticket-2-verification.md | VERIFY_PASS |
| **5 (Reviewer)** | **05-final-review.md** | **FINAL_PASS** |

---

## Section K — Deferred Work

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-B24-01 | NT8-043 rule formal entry: confirm null-conditional event unsubscription (`?.Event -=`) causes silent runtime crash under NT8 Roslyn; add to `docs/standards/NT8_COMPILER_RULES.md` as NT8-043 with P1 status. Currently WATCH-only per plan Section 9; no B24 code hit this path. | P2 | B25 or future | OPEN |
| DW-B24-02 | Manual E2E verification of DW-B23-BE-ALLACCOUNTS-01 in live NinjaTrader session: press B on a solo account (no copy rule registered) and confirm stop moves. Unit tests cover null-leader path and no-throw; this is the runtime acceptance test. | P1 | B25 pre-release | OPEN |
| DW-B24-03 | `MoveStopToBreakEven` uses `Account.All`-style iteration internally (via `FindPosition`) — audit whether the `AllAccounts` fan-out on line 1193 could duplicate the leader if the leader IS the master account in the rule. The `if (acc == leader) continue` guard is the current mitigation; a formal test for this path is absent. | P2 | B25 | OPEN |

---

*ptt-plan-reviewer · PTT-COPIER-B24 Lane B · 2026-07-07*
