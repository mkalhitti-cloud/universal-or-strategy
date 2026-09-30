# B36-LaneB Final Review
# Epic: DW-B35-TARGETS-01 | be-targets-oco
# Reviewer: ptt-plan-reviewer (Phase 5)
# Date: 2026-07-27
# Block: B36 | Lane B

---

## Input Files Read

| File | Read? |
|------|-------|
| `docs/brain/B36-LaneB/02-architecture-plan.md` | YES |
| `docs/brain/B36-LaneB/04-ticket-review.md` | YES |
| `docs/brain/B36-LaneB/ticket-1-completion.md` | YES |
| `docs/brain/B36-LaneB/ticket-1-verification.md` | YES |
| `c:/WSGTA/universal-or-strategy/src/PropTraderTools/Features/PttBreakEven.cs` | YES — full source read |
| `docs/standards/jane-street/RULES_CATALOG.md` | YES |
| `docs/brain/B35-LaneA/06-deferred-backlog.md` | YES — READ-ONLY |
| `docs/brain/B35-LaneB/06-deferred-backlog.md` | YES — READ-ONLY |

---

## Check 1 — Spec Requirement DW-B35-TARGETS-01 Satisfied?

**Requirement**: BE button places bare stop with no OCO group and no take-profit targets.
Fix: (a) snapshot Working ATM targets before cancelling brackets, (b) emit a real OCO group
ID on the stop, (c) resubmit targets as `PTT-BE-Target-N` Limit orders linked to stop by
same OCO group.

| Root Cause Part | Architecture Section | Implemented? | Source Evidence |
|----------------|---------------------|-------------|-----------------|
| Part 1 — `string.Empty` at arg8 of `SubmitBeStopLocal` | C5 | ✅ YES | PttBreakEven.cs:183 — `ocoId, // arg8: OCO group ID (DW-B35-TARGETS-01 FIX)` |
| Part 2 — No `SnapshotTargetsLocal` method existed | C1 | ✅ YES | PttBreakEven.cs:244–264 — method present, returns `List<(double,int,OrderAction)>` |
| Part 3 — No `SubmitBeTargetsLocal` method existed | C3 | ✅ YES | PttBreakEven.cs:288–339 — method present |

**DW-B35-TARGETS-01: CLOSED** ✅

---

## Check 2 — C1–C5 + BuildBeOcoId All Implemented?

Source read: [`src/PropTraderTools/Features/PttBreakEven.cs`](c:/WSGTA/universal-or-strategy/src/PropTraderTools/Features/PttBreakEven.cs)

| Component | Location in Source | Status |
|-----------|-------------------|--------|
| C1 `SnapshotTargetsLocal` | lines 244–264 | ✅ PRESENT |
| C2 `IsAtmTargetName` | lines 230–235 | ✅ PRESENT |
| C3 `SubmitBeTargetsLocal` | lines 288–339 | ✅ PRESENT |
| C4 `Execute()` foreach body: 5-step A→E | lines 95–102 | ✅ PRESENT |
| C5 `SubmitBeStopLocal` ocoId param + arg8 | lines 162–163, 183 | ✅ PRESENT |
| `BuildBeOcoId` helper | lines 270–275 | ✅ PRESENT |

All 6 components confirmed in final source. **Check 2: PASS**

---

## Check 3 — T1–T4 All Present in CopyEngineTests.cs (184 [Fact]s)?

Per verifier Layer 3 independent scan (SCAN-07 in ticket-1-verification.md):

```
Select-String -Path src\PropTraderTools\CopyEngineTests.cs -Pattern "\[Fact\]" | Measure-Object
Result: 184
```

| Test Name | Line | Present? |
|-----------|------|---------|
| `T_B36B_SnapshotTargetsLocal_ReadsAtmTargetOrders` | 3346 | ✅ |
| `T_B36B_IsAtmTargetName_MatchesTarget1To9Only` | 3362 | ✅ |
| `T_B36B_SubmitBeTargetsLocal_MethodExists` | 3380 | ✅ |
| `T_B36B_OcoId_NonEmpty` | 3397 | ✅ |

Baseline delta: 180 → 184 (+4). **Check 3: PASS**

---

## Check 4 — Execute() Ordering Correct (snapshot → ocoId → cancel → stop → targets)?

Source read directly (lines 95–102):

```csharp
var targets = SnapshotTargetsLocal(acc, ctx.Instrument);   // Step A — snapshot FIRST
string ocoId = BuildBeOcoId(acc.Name, bePrice, tickSize);  // Step B — pure computation
CancelStaleBracketsLocal(acc, ctx.Instrument);             // Step C — cancel old brackets
SubmitBeStopLocal(acc, ctx.Instrument, bePrice, isLong, ocoId); // Step D — stop
SubmitBeTargetsLocal(acc, ctx.Instrument, ocoId, targets);  // Step E — targets
```

Mandatory ordering constraints verified:
- A BEFORE C: snapshot taken while targets still `Working` ✅
- C BEFORE D: old brackets cancelled before new stop submitted ✅
- D BEFORE E: stop submitted first; targets link to stop's OCO group ✅
- B BEFORE D: `ocoId` computed before it is passed to `SubmitBeStopLocal` ✅

**Check 4: PASS**

---

## Check 5 — 7 Scans All Zero in Final Source?

Verifier Layer 3 (ticket-1-verification.md) ran all 7 scans independently against final source.
Reviewer confirms: verifier ran scans independently (Layer 3 does not trust Layer 2).

| Scan | Pattern | Result | Evidence |
|------|---------|--------|---------|
| SCAN-01 | `lock(` | **0 code matches** ✅ | Source lines 1–341: no `lock(` anywhere |
| SCAN-02 | `async void` | **0 matches** ✅ | All methods are synchronous |
| SCAN-03 | `.Where\|.First\|.Select\|.Any` | **0 code matches** ✅ | 2 hits in XML doc comments only (lines 122, 239) |
| SCAN-04 | `{ get; init; }` | **0 matches** ✅ | NT8-001 compliant |
| SCAN-05 | `DateTime.Now` | **0 code matches** ✅ | 1 hit in XML doc comment only; code uses `DateTime.MaxValue` |
| SCAN-06 | `dotnet build` | **0 new errors** ✅ | 2 pre-existing errors in AtrSizingEngine.cs only (B34 baseline, unchanged) |
| SCAN-07 | `[Fact]` count | **184** ✅ | Verifier independently confirmed 184 |

All 7 scans: clean. **Check 5: PASS**

---

## Check 6 — Hard-Link PASS?

Verifier independently confirmed (ticket-1-verification.md C10):

```
SUMMARY: OK=11  DESYNC=0  MISSING=0  FIXED=0  SKIPPED=1
PASS -- All deployable source files match NinjaTrader. No stale deploy risk.
```

`PttBreakEven.cs` is confirmed hard-linked and in sync. **Check 6: PASS**

---

## Check 7 — Build Tag = "PTT-COPIER B36 | be-targets-oco | 2026-07-27"?

Verifier independently confirmed (ticket-1-verification.md C7):

```csharp
// CopyEngine.cs line 41:
internal const string Tag = "PTT-COPIER B36 | be-targets-oco | 2026-07-27";
```

**Check 7: PASS**

---

## Check 8 — DNA Rule Audit (JS-021, JS-033, NT8-006, NT8-049, NT8-007, NT8-013)

Source read directly for each rule. Reviewer's independent findings:

| Rule | Check | Source Evidence | Result |
|------|-------|-----------------|--------|
| JS-021 — no `lock()` | SCAN-01: 0 hits | No `lock(` anywhere in PttBreakEven.cs (lines 1–341) | ✅ PASS |
| JS-033 — no `async void` | SCAN-02: 0 hits | All new methods: `bool`, `List<T>`, `void`, `string` — all synchronous | ✅ PASS |
| JS-002 — no `return null` in new code | `SnapshotTargetsLocal` returns `new List<...>()` on null inputs (line 248) | No new `return null` added | ✅ PASS |
| JS-001 — no `throw` in hot paths | All try/catch blocks catch silently and log | No `throw new XxxException` in any new method | ✅ PASS |
| NT8-006 — no LINQ | `SnapshotTargetsLocal` (lines 249–263): raw `foreach (Order o in acc.Orders)` | No `.ToList()`, `.Where()`, `.Select()`, `.Any()` in code | ✅ PASS |
| NT8-049 — Limit arg positions | `SubmitBeTargetsLocal` (lines 308–309): `t.Price` at arg6, `0` at arg7 | Correct: `limitPrice=t.Price`, `stopPrice=0` — NOT swapped | ✅ PASS |
| NT8-007 — arg11 cast | `SubmitBeTargetsLocal` (line 313): `(NinjaTrader.Cbi.CustomOrder)null` | Explicit cast, not string literal | ✅ PASS |
| NT8-013 — DateTime.MaxValue | `SubmitBeTargetsLocal` (line 312): `DateTime.MaxValue` for GTC | No `DateTime.Now` in code | ✅ PASS |
| NT8-014 — PTT- prefix | `SubmitBeTargetsLocal` (line 311): `"PTT-BE-Target-" + (i + 1)` | All signal names start with `"PTT-"` | ✅ PASS |
| CYC ≤ 8 (all methods) | Execute()=8, SnapshotTargetsLocal=3, IsAtmTargetName=2, BuildBeOcoId=2, SubmitBeTargetsLocal=4, SubmitBeStopLocal=3 | All within stated limits | ✅ PASS |

**Zero DNA violations. Check 8: PASS**

---

## Check 9 — Cross-File Coherence

PttBreakEven.cs is a single-file module with no cross-file dependencies beyond `PttContracts.cs`
(interface) and `NinjaTrader.Cbi` (NT8 API). No changes to CopyEngine.cs, PttContracts.cs, or
TradeCopierPanel.cs were required or made. The `Execute()` public signature is unchanged — callers
in TradeCopierPanel are not affected. The OCO group wiring is entirely internal to PttBreakEven.cs.

- `CopyEngine.cs`: Modified only to update build tag (line 41). Zero logic changes. ✅
- `PttContracts.cs`: Zero modifications (IPttModule signature unchanged). ✅
- `TradeCopierPanel.cs`: Zero modifications (calls `Execute()` — same public signature). ✅
- `CopyEngineTests.cs`: 4 new `[Fact]` methods appended. No modifications to existing tests. ✅

**Cross-file coherence: PASS**

---

## Observation: BuildBeOcoId Signature Adaptation

The architecture plan specified `BuildBeOcoId(Account acc, double bePrice, double tickSize)`.
The implementation uses `BuildBeOcoId(string accName, double bePrice, double tickSize)`.
The call site correctly passes `acc.Name`. T4 is pure arithmetic (no reflection on this method),
so all tests pass regardless. This is a functionally equivalent adaptation, not a violation.
The ticket reviewer noted this in ticket-1-verification.md C6. **Not a violation.**

---

## Section K — Deferred Work

### K.1 — Prior Open Items from B35-LaneA

| ID | Description | Priority | Source | Status |
|----|-------------|----------|--------|--------|
| DW-B35-LA-SIM-01 | Sim test gate: validate BE stop-above-market guard in live NT8 sim (8-step gate) | P1 | B35-LaneA | **OPEN** — not addressed by B36-LaneB |
| DW-B32-TRIM-MARKET-01 | buffer=0 forces market fallback — limit path degrades to market order silently | P1 | B32 | **OPEN** |
| DW-B32-TRIM-ANCHOR-01 | ComputeLimitPx wrong price anchor (ask/bid peg) | P1 | B32 | **OPEN** |
| R-B32-03 / DW-B32-TRIM-CLOSE-01 | Trim ATM OCO bracket corruption on market exit path | P1 | B32 | **OPEN** |
| DW-B32-DEFERRED-03 | Limit path ATM bracket detection: TrimOneAccountLimit / FlattenOneAccountLimit | P2 | B32 | **OPEN** — Director approval needed |
| U1 | NT8 `Account.CreateOrder` arg8 OCO group ID effectiveness on sim | LOW | B34 | **OPEN** — sim test session needed |
| U3 | Confirm Limit order arg6=limitPrice, arg7=0 correct in live NT8 | MEDIUM | B34 | **OPEN** — sim test session needed |
| DW-B32-DEFERRED-02 | ATM Target nudge — acc.Change() silently rejected by NT8 ATM engine | — | B32 | **REJECTED** — architectural constraint |

### K.2 — Prior Open Items from B35-LaneB

Same items as B35-LaneA carry-forward (B35-LaneB introduced no new open items):
U1, U3, DW-B32-DEFERRED-02 (REJECTED), DW-B32-DEFERRED-03, DW-B32-TRIM-ANCHOR-01,
DW-B32-TRIM-MARKET-01, R-B32-03/DW-B32-TRIM-CLOSE-01 — all unchanged.

### K.3 — DW-B35-TARGETS-01 Closure

| ID | Description | Status |
|----|-------------|--------|
| DW-B35-TARGETS-01 | BE button places bare stop with no OCO group and no take-profit targets | **CLOSED** by B36-LaneB |

All three root cause parts resolved:
- Part 1: `SubmitBeStopLocal` arg8 now passes `ocoId` instead of `string.Empty` (line 183)
- Part 2: `SnapshotTargetsLocal` now exists and is called before cancel (lines 244–264, line 96)
- Part 3: `SubmitBeTargetsLocal` now exists and is called after stop submission (lines 288–339, line 102)

### K.4 — New Items Introduced by B36-LaneB

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-B36-SIM-01 | Sim test gate: validate BE OCO bracket — press BE button, verify Output shows PTT-BE-Stop and PTT-BE-Target-N orders linked by same OCO group ID in NinjaTrader Active Orders grid | P1 | B37 or sim session | OPEN |
| DW-B36-SIM-02 | Confirm OCO fill behavior: if PTT-BE-Stop fills, verify PTT-BE-Target-N orders are automatically cancelled by NT8 OCO engine | P1 | Sim session | OPEN |
| DW-B36-SIM-03 | Confirm arg8 OCO group wiring is effective on sim (U1 from B34 remains unvalidated; this is the first block to actually use arg8 in BE context) | MEDIUM | Sim session | OPEN |

### K.5 — Deferred Work Table (Consolidated B36-LaneB Snapshot)

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-B35-TARGETS-01 | BE button places bare stop with no OCO group and no targets | P0 | B36-LaneB | **CLOSED** |
| DW-B36-SIM-01 | Sim: verify PTT-BE-Stop + PTT-BE-Target-N appear in Active Orders after BE press | P1 | B37/sim | OPEN |
| DW-B36-SIM-02 | Sim: confirm OCO fill auto-cancel behavior between stop and targets | P1 | Sim session | OPEN |
| DW-B36-SIM-03 | Sim: confirm arg8 OCO group ID effectiveness in BE context (extends U1) | MEDIUM | Sim session | OPEN |
| DW-B35-LA-SIM-01 | Sim: validate BE stop-above-market guard (8-step gate) | P1 | Sim session | OPEN |
| DW-B32-TRIM-MARKET-01 | buffer=0 market fallback in ComputeLimitPx | P1 | Future block | OPEN |
| DW-B32-TRIM-ANCHOR-01 | ComputeLimitPx wrong price anchor | P1 | Future block | OPEN |
| R-B32-03 / DW-B32-TRIM-CLOSE-01 | Trim ATM OCO bracket corruption on market exit | P1 | Future block | OPEN |
| DW-B32-DEFERRED-03 | Limit path ATM bracket detection TrimOneAccountLimit / FlattenOneAccountLimit | P2 | Future (Director approval) | OPEN |
| U1 | NT8 arg8 OCO group ID effectiveness on sim | LOW | Sim session | OPEN (superseded in BE context by DW-B36-SIM-03) |
| U3 | Confirm Limit arg6=limitPrice, arg7=0 in live NT8 | MEDIUM | Sim session | OPEN |
| DW-B32-DEFERRED-02 | ATM Target nudge via acc.Change() | — | N/A | REJECTED — architectural constraint |

---

## Pipeline Gate Summary

| Gate | Result |
|------|--------|
| TICKET_REVIEW_PASS (Phase 3.5) | ✅ PASS |
| BUILD_PASS (Phase 4a self-report) | ✅ PASS (0 new errors) |
| VERIFY_PASS (Phase 4b independent) | ✅ PASS |
| DW-B35-TARGETS-01 closed | ✅ CLOSED |
| C1–C5 + BuildBeOcoId all implemented | ✅ ALL PRESENT |
| T1–T4 in CopyEngineTests.cs, 184 [Fact]s | ✅ CONFIRMED |
| Execute() ordering A→B→C→D→E | ✅ CORRECT |
| 7 scans all zero (new code) | ✅ ALL ZERO |
| Hard-link gate OK=11 DESYNC=0 | ✅ PASS |
| Build tag = "PTT-COPIER B36 \| be-targets-oco \| 2026-07-27" | ✅ CONFIRMED |
| JS-021 / JS-033 / NT8-006 / NT8-049 / NT8-007 / NT8-013 | ✅ ZERO VIOLATIONS |
| Section K present | ✅ PRESENT |
| 06-deferred-backlog.md written | ✅ WRITTEN |

---

## FINAL_PASS

All checks satisfied. Zero violations. All pipeline gates passed.
DW-B35-TARGETS-01 is formally CLOSED.
06-deferred-backlog.md written. Section K present.

**FINAL_PASS**

---

*Reviewer*: ptt-plan-reviewer (Phase 5)
*Date*: 2026-07-27
*Upstream gate*: TICKET_REVIEW_PASS 2026-07-27 | VERIFY_PASS 2026-07-27
*Next gate*: Sim test session (DW-B36-SIM-01, DW-B36-SIM-02, DW-B36-SIM-03)
