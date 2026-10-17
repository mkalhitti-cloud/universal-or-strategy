# B199 Orchestrator Prompt

**Epic**: B199 — Direct Repair Promotion + B198 Deferred Backlog
**Date**: 2026-10-14
**Prepared by**: copier-spec mode (post-SIM validation)
**Status**: READY FOR PIPELINE

---

## Context

Eight direct repairs (DR-1 through DR-8) were applied surgically to `src/PropTraderTools/CopyEngine.cs`
during the B199 investigation session. All repairs are confirmed working in live NT8 SIM:

- `[PTT-ATM-SEND]` fires for Sim102, Sim103, Sim104 on every MGC and MES entry.
- `[PTT-DR5-EVICT]` fires on every cancel — dedup state is clean.
- `[PTT-DR4-CLEAR]` fires on Copy ON toggle — both dicts cleared atomically.
- Gate 5 correctly blocks duplicate `Accepted` events. No stale keys.

Evidence file: `docs/brain/B199-direct-repairs.md`
Output file: `docs/brain/output.md` (lines 59-61, 94-102 confirm multi-follower ATM send)

---

## Repairs Being Promoted (keep permanently)

| ID | Method | Change | Why Keep |
|----|--------|--------|---------|
| DR-4 | `SetEnabled` | `_entryInstrKeyByOrderId.Clear()` added alongside `_liveEntryInstruments.Clear()` on Copy ON; `[PTT-DR4-CLEAR]` log | Companion dict must be cleared in sync. Functional fix. |
| DR-5 | `EvictDedup` | Optional `instrKey` param added; direct `_liveEntryInstruments.TryRemove(instrKey)` fallback when orderId-keyed miss; `[PTT-DR5-EVICT]` log | Fixes NT8/Rithmic orderId reassignment (GUID→numeric) causing permanent stale keys. |
| DR-8 | `PassesDailyCapCheck` | SIM/Playback account name prefix guard (`StartsWith("Sim", OrdinalIgnoreCase)` or `"Playback"`) bypasses P&L floor check | Daily cap is a live-account risk control. SIM accounts were returning P&L ≤ −500 → all followers silently blocked. Correct and permanent fix. |

## Diagnostic Logs Being Promoted (keep as permanent telemetry)

| ID | Method | Log Tag | Why Keep |
|----|--------|---------|---------|
| DR-1 | `IsLiveEntryBlocked` | `[PTT-GATE5-BLOCKED]` | Makes stale-key blocks visible. Low noise. |
| DR-2 | `OnOrderUpdate` | `[PTT-GATE25-BLOCKED]` | Makes rule-disabled exits visible. Low noise (Accepted/Working only). |
| DR-7 | `SendCopyWithAtm` catch | `[PTT-ATM-ERROR]` | Surfaces swallowed exceptions. Zero noise when no error. |

## Diagnostic Logs Being REMOVED (noise, root cause resolved)

| ID | Method | Log Tag | Why Remove |
|----|--------|---------|-----------|
| DR-6 | `DispatchCopy` post-Gate-5 | `[PTT-DISPATCH]` | Fires on every entry. Root cause resolved (DR-8). |
| DR-6 | `TryLaneDHook` entry | `[PTT-LANED-ENTRY]` | Fires on every Clone entry. Root cause resolved (DR-8). |
| DR-8 debug | `TryLaneDDispatchFollower` Branch B | `[PTT-LANED-SKIP]` with pnl+floor | Diagnostic only. DR-8 fix eliminates the skip for SIM accounts. |

---

## B198 Deferred Backlog Items Being Included

From `docs/brain/B198/06-deferred-backlog.md` (authoritative as of B198 FINAL_PASS):

### P1 Items (address in this block)

| DW ID | Description | Pipeline Fit |
|-------|-------------|-------------|
| DW-B198-CHECKREV-CYC-01 | `CheckAtmTemplatesForReverse(string)` CYC=11 — extract qty-parse into `TryParseAtmTemplateGroupQty(string, out int)` | Fits as one ticket. CYC extraction only. |
| DW-B198-QTY-SIM-01 | T2a SIM evidence: confirm ATM-send qty=10 fires (already visible in B199 output lines 59-61). Record in `docs/brain/B198/ticket-2a-sim-evidence.md`. | Fits as a documentation ticket (no code). |
| DW-B198-SIMSTP-SIM-01 | B198 SIM gate. SIM-1/2/3/4 scenarios from B198-orchestrator-prompt.md §B198 SIM GATE. | SIM-2 evidence already collected (B199 output). SIM-3/SIM-4 need a dedicated session. |

### P1 Carried Items (not in this block — SIM gate cascade)

DW-B167-DEFER-03, DW-B167-DEFER-04, DW-B170-SIM, DW-B197-SIM-01 — these require a separate
dedicated SIM session. Defer to B200 SIM block unless the Director combines with B199 SIM gate.

---

## Pipeline Tickets

### Ticket 1 — DR-5 `EvictDedup` Promotion + Test Coverage

**Scope**: Formal pipeline promotion of DR-5 changes. Add xUnit coverage.

**Changes already in `CopyEngine.cs`** (direct repairs, confirmed working):
- `OnOrderUpdate` line ~1541: `EvictDedup(e.Order.OrderId.ToString(), e.Order.OrderState, e.Order.Instrument?.FullName + "|" + e.Order.OrderAction)`
- `EvictDedup` signature: `internal void EvictDedup(string orderId, OrderState state, string instrKey = null)`
- `EvictDedup` Cancelled block: fallback `_liveEntryInstruments.TryRemove(instrKey)` when `_entryInstrKeyByOrderId` misses + `[PTT-DR5-EVICT]` / `[PTT-DR5-FALLBACK]` logs

**New tests required (B199Tests.cs)**:
- `T1_DR5_01`: `EvictDedup(Cancelled, instrKey)` with matching `_entryInstrKeyByOrderId` entry → `[PTT-DR5-EVICT]` fires, key removed from both dicts.
- `T1_DR5_02`: `EvictDedup(Cancelled, instrKey)` with NO matching `_entryInstrKeyByOrderId` entry BUT `_liveEntryInstruments.ContainsKey(instrKey)` → `[PTT-DR5-FALLBACK]` path, key removed from `_liveEntryInstruments`.
- `T1_DR5_03`: `EvictDedup(Cancelled, null)` with no matching entry → no-op (null instrKey, fallback skipped).
- `T1_DR5_04`: `EvictDedup(Filled, instrKey)` → `_liveEntryInstruments` NOT removed (trade is live; only `_entryInstrKeyByOrderId` cleared).

**Verification gate**: All 4 facts GREEN. CYC of `EvictDedup` unchanged (DW-B142-MGC-02 noted CYC=5; fallback adds 1 branch → CYC=6, within ceiling).

---

### Ticket 2 — DR-4 `SetEnabled` + DR-8 `PassesDailyCapCheck` Promotion + Cleanup + Tests

**Scope**: Formal promotion of DR-3/DR-4 (extended clear) and DR-8 (SIM bypass). Remove noise logs.
Remove DR-3 (superseded by DR-4). Remove `[PTT-DISPATCH]`, `[PTT-LANED-ENTRY]`, `[PTT-LANED-SKIP]` noise.

**Changes in `CopyEngine.cs`**:

_Keep (already in code)_:
- `SetEnabled(true)`: `_entryInstrKeyByOrderId.Clear()` + `[PTT-DR4-CLEAR]` log (keep — functional + telemetry)
- `PassesDailyCapCheck`: SIM/Playback prefix guard (keep — permanent fix)

_Remove (noise, root cause resolved)_:
- `DispatchCopy` post-Gate-5: remove `[PTT-DISPATCH]` log block (DR-6)
- `TryLaneDHook` entry: remove `[PTT-LANED-ENTRY]` log block (DR-6)
- `TryLaneDDispatchFollower` Branch A null skip: remove `[PTT-LANED-SKIP] null_acc` log
- `TryLaneDDispatchFollower` Branch B daily_cap skip: remove `[PTT-LANED-SKIP] daily_cap pnl=` log (keep the `return false` — just remove the verbose log with pnl+floor)

**New tests required (B199Tests.cs)**:
- `T2_DR4_01`: `SetEnabled(true)` with pre-populated `_liveEntryInstruments` → both dicts cleared, count=N before, count=0 after.
- `T2_DR4_02`: `SetEnabled(false)` → NO clear (clear is enable-only).
- `T2_DR8_01`: `PassesDailyCapCheck(Account{Name="Sim101"})` → returns `true` (SIM bypass, no P&L read).
- `T2_DR8_02`: `PassesDailyCapCheck(Account{Name="Playback1"})` → returns `true` (Playback bypass).
- `T2_DR8_03`: `PassesDailyCapCheck(Account{Name="LiveAcct"})` with `pnl > _dailyCapFloor` → returns `true`.
- `T2_DR8_04`: `PassesDailyCapCheck(Account{Name="LiveAcct"})` with `pnl <= _dailyCapFloor` → returns `false`.

**Verification gate**: All 6 facts GREEN. CYC of `PassesDailyCapCheck` 2→3 (one new branch). Within ceiling.

---

### Ticket 3 — DW-B198-CHECKREV-CYC-01: `CheckAtmTemplatesForReverse(string)` CYC Extraction

**Scope**: Extract qty-parse block into `TryParseAtmTemplateGroupQty(string filePath, out int perGroupQty)`.
Reduces `CheckAtmTemplatesForReverse(string)` from CYC=11 to CYC≤7. Closes DW-B198-CHECKREV-CYC-01.

**Extraction target** (from `docs/brain/B198/06-deferred-backlog.md` DW-B198-CHECKREV-CYC-01):
```csharp
// Extract this block from CheckAtmTemplatesForReverse(string):
private bool TryParseAtmTemplateGroupQty(string filePath, out int perGroupQty)
{
    // try/catch outer (CYC +1)
    // XmlDocument load + SelectSingleNode
    // if (qtyNode != null && int.TryParse(...) && perGroupQty > 0) (CYC +1 branch, +0 for &&)
    // return true / false
}
```

**New tests required (B199Tests.cs)**:
- `T3_CHECKREV_01`: Valid XML with `<Quantity>5</Quantity>` → `TryParseAtmTemplateGroupQty` returns `true`, `perGroupQty=5`.
- `T3_CHECKREV_02`: XML with no `<Quantity>` node → returns `false`.
- `T3_CHECKREV_03`: Invalid XML file path → returns `false` (catch handles gracefully, no throw).

**Verification gate**: `CheckAtmTemplatesForReverse(string)` McCabe ≤ 7. `TryParseAtmTemplateGroupQty` McCabe ≤ 3. DW-B198-CHECKREV-CYC-01 CLOSED.

---

### Ticket 4 — DW-B198-QTY-SIM-01: T2a SIM Evidence Record

**Scope**: Documentation only. No code changes. Collect SIM evidence confirming H1 or H2 for T2b.

**Evidence already collected in B199 output**:
- Line 34: `[PTT-ATM-SEND] acct=Sim104 qty=10 tmpl= obj=set`
- Lines 59-61: `[PTT-ATM-SEND] acct=Sim102 qty=10`, `acct=Sim103 qty=10`, `acct=Sim104 qty=10`

`qty=10` matches leader qty. This confirms **H1** (no divide needed). T2b's H2 code is a benign no-op
(dict is empty, TryGetAtmTemplateGroupCount returns false, division never runs). Close as H1.

**Deliverable**: Create `docs/brain/B198/ticket-2a-sim-evidence.md`:
```
Evidence: [PTT-ATM-SEND] acct=Sim102 qty=10 tmpl= obj=set (B199 output lines 59-61, 2026-10-14)
Leader qty: 10. Follower qty sent: 10. H1 CONFIRMED (no divide needed).
T2b H2 code: benign no-op (TryGetAtmTemplateGroupCount returns false when _atmTemplateGroupQty empty).
Recommendation: Remove T2b H2 logic in next cleanup wave (or leave as benign no-op).
DW-B198-QTY-SIM-01: CLOSED.
```

**Verification gate**: `docs/brain/B198/ticket-2a-sim-evidence.md` exists and contains H1 CONFIRMED verdict.

---

## Pipeline Execution Chain (MANDATORY — ALL PHASES)

The full 7-step PTT pipeline is MANDATORY. No phases may be skipped, combined, or abbreviated.

```
Ph1  ptt-architect      → docs/brain/B199/02-architecture-plan.md
Ph2  ptt-plan-reviewer  → docs/brain/B199/02-plan-review.md          (REVIEW_PASS gate)
Ph3  ptt-architect      → docs/brain/B199/04-tickets.md
Ph3.5 ptt-ticket-reviewer → docs/brain/B199/04-ticket-review.md       (TICKET_REVIEW_PASS gate)
Ph4a ptt-engineer       → src .cs edits + docs/brain/B199/ticket-N-completion.md
Ph4b ptt-verifier       → docs/brain/B199/ticket-N-verification.md    (VERIFY_PASS gate)
Ph5  ptt-plan-reviewer  → docs/brain/B199/05-final-review.md + docs/brain/B199/06-deferred-backlog.md
```

**Note to architect (Ph1)**: The four tickets above are pre-scoped. The architect MUST:
1. Verify all DR changes are already present in `CopyEngine.cs` before writing the plan — this is a promotion epic, not a greenfield implementation.
2. Confirm CYC budgets on all modified methods.
3. Identify any additional deferred backlog items from `docs/brain/B198/06-deferred-backlog.md` that fit the B199 scope but are not listed above.
4. Assign test counts per ticket (estimate: T1=4, T2=6, T3=3, T4=0 code / 1 doc).

---

## Pre-Conditions for Ph1 Start

- [x] `[PTT-ATM-SEND]` confirmed firing for Sim102/Sim103/Sim104 in live NT8 SIM (`docs/brain/output.md` lines 59-61)
- [x] `[PTT-DR5-EVICT]` confirmed firing on cancel
- [x] `[PTT-DR4-CLEAR]` confirmed firing on Copy ON toggle
- [x] PTT sync clean: 19 linked, 0 errors
- [x] `docs/brain/B199-direct-repairs.md` written (DR-1 through DR-8 documented with evidence)
- [ ] NT8 compile clean (Director confirms before pasting prompt to Ph1)

---

## Deferred to B200 (not in this block)

| DW ID | Priority | Reason |
|-------|----------|--------|
| DW-B197-SIM-01 | P1 | Requires dedicated SIM gate session |
| DW-B167-DEFER-03 | P1 | Requires dedicated SIM gate session |
| DW-B167-DEFER-04 | P1 | Requires dedicated SIM gate session |
| DW-B170-SIM | P1 | Requires dedicated SIM gate session |
| DW-B198-SIMSTP-SIM-01 | P1 | SIM-2 evidence covered by T4 above; SIM-1/3/4 need separate session |
| DW-B183-03 | P1 | Triggered only on future `IsLaneEEligible` branch additions |
| DW-B198-GUARDIAN-01 | P2 | TradeCopierPanel.cs not touched this block |
| DW-B192-SINST-01 | P2 | Triggered only on future `SetInstrument` modifications |
| DW-B159-K2/K3/K4 | P2 | Infrastructure dependencies not resolved |
| DW-B193-01, DW-B197-01/02/04 | P3 | Cosmetic, address when convenient |

---

## Lane Orchestrator Prompt (paste into ptt-orchestrator session)

---

You are the ptt-orchestrator for **Epic B199 — Direct Repair Promotion + B198 Deferred Backlog**.

### Mission

Promote eight direct repairs (DR-1 through DR-8) applied to `src/PropTraderTools/CopyEngine.cs`
into the formal pipeline. Repairs are already in-code and confirmed working in live NT8 SIM
(see `docs/brain/B199-direct-repairs.md` and `docs/brain/output.md`).

Also close one P1 deferred backlog item from B198: DW-B198-CHECKREV-CYC-01 (CYC extraction).
And record T2a SIM evidence to close DW-B198-QTY-SIM-01.

### Work Directory

`docs/brain/B199/`

### Inputs

- `docs/brain/B199-direct-repairs.md` — all 8 direct repairs with evidence
- `docs/brain/B199-orchestrator-prompt.md` — this document (tickets pre-scoped)
- `docs/brain/B198/06-deferred-backlog.md` — authoritative B198 carry-forward list
- `src/PropTraderTools/CopyEngine.cs` — current state (repairs already in code)
- `docs/standards/jane-street/RULES_CATALOG.md` — JS-001..JS-110 (mandatory pre-read for Ph1)

### The 4 Tickets

**T1 — DR-5 EvictDedup Promotion + 4 xUnit tests**
Formal promotion of the `EvictDedup(orderId, state, instrKey = null)` signature change and
`_liveEntryInstruments` direct-fallback eviction. Add 4 [Fact] tests covering normal evict,
fallback evict, null-instrKey no-op, and Filled no-remove.

**T2 — DR-4 SetEnabled + DR-8 PassesDailyCapCheck Promotion + Cleanup + 6 xUnit tests**
Keep: `_entryInstrKeyByOrderId.Clear()` in `SetEnabled(true)` + `[PTT-DR4-CLEAR]` log.
Keep: SIM/Playback prefix bypass in `PassesDailyCapCheck`.
Remove: `[PTT-DISPATCH]` log (DR-6), `[PTT-LANED-ENTRY]` log (DR-6), verbose `[PTT-LANED-SKIP]`
  pnl+floor debug log (Branch B of `TryLaneDDispatchFollower`), null_acc log (Branch A).
Add 6 [Fact] tests covering both DR-4 and DR-8 branches.

**T3 — DW-B198-CHECKREV-CYC-01: CYC Extraction**
Extract qty-parse block from `CheckAtmTemplatesForReverse(string)` into
`TryParseAtmTemplateGroupQty(string filePath, out int perGroupQty)`.
Target: `CheckAtmTemplatesForReverse(string)` McCabe ≤ 7. New helper McCabe ≤ 3.
Add 3 [Fact] tests.

**T4 — DW-B198-QTY-SIM-01: SIM Evidence Document**
Documentation only. Create `docs/brain/B198/ticket-2a-sim-evidence.md`.
Evidence: B199 output lines 59-61 show `qty=10` matching leader qty → H1 CONFIRMED.
T2b H2 code is a benign no-op. DW-B198-QTY-SIM-01 CLOSED.

### The Pipeline (MANDATORY — ALL 7 STEPS, NO EXCEPTIONS)

```
Ph1  ptt-architect      → docs/brain/B199/02-architecture-plan.md
Ph2  ptt-plan-reviewer  → docs/brain/B199/02-plan-review.md          (REVIEW_PASS gate)
Ph3  ptt-architect      → docs/brain/B199/04-tickets.md
Ph3.5 ptt-ticket-reviewer → docs/brain/B199/04-ticket-review.md       (TICKET_REVIEW_PASS gate)
Ph4a ptt-engineer       → src .cs edits + docs/brain/B199/ticket-N-completion.md
Ph4b ptt-verifier       → docs/brain/B199/ticket-N-verification.md    (VERIFY_PASS gate)
Ph5  ptt-plan-reviewer  → docs/brain/B199/05-final-review.md + docs/brain/B199/06-deferred-backlog.md
```

No phase may be skipped, combined, or abbreviated.
No direct .cs edits outside of Ph4a.
Ph4a MUST read `CopyEngine.cs` before editing — repairs are already present, do not re-apply.
Ph4b MUST run `powershell -File .\scripts\ptt-sync.ps1` and confirm 19 linked 0 errors.
Ph4b MUST run `powershell -File .\scripts\verify_critical_symbols.ps1`.
Ph5 MUST produce `06-deferred-backlog.md` carrying forward all open DW items from `docs/brain/B198/06-deferred-backlog.md` plus any new items.

### Jane Street Constraints (P0 — non-negotiable)

- JS-021: No `lock()` in any new or modified code. ConcurrentDictionary atomic ops only.
- JS-033: No `async void` (except NT8 event handler exemption).
- JS-066: All modified methods must have McCabe CYC ≤ 8 after edit.
- JS-001: No `throw new XxxException` in hot paths.
- xUnit only: All tests use `[Fact]`. No NUnit. No MSTest.
- ASCII only: No Unicode, emoji, or curly quotes in string literals.

### Acceptance Criteria

- T1: 4 [Fact] tests GREEN. `EvictDedup` CYC ≤ 8.
- T2: 6 [Fact] tests GREEN. `[PTT-DISPATCH]` / `[PTT-LANED-ENTRY]` / verbose `[PTT-LANED-SKIP]` logs removed. `PassesDailyCapCheck` CYC ≤ 3.
- T3: 3 [Fact] tests GREEN. `CheckAtmTemplatesForReverse(string)` McCabe ≤ 7. DW-B198-CHECKREV-CYC-01 CLOSED.
- T4: `docs/brain/B198/ticket-2a-sim-evidence.md` exists with H1 CONFIRMED. DW-B198-QTY-SIM-01 CLOSED.
- All scans GREEN: lock=0, async void=0, CYC≤8 all modified methods, ASCII-only, xUnit-only.
- PTT sync: 19 linked 0 errors after every Ph4a commit.
- `06-deferred-backlog.md` written in Ph5 carrying forward all open B198 items not closed by B199.

### Start

Begin with Ph1 (ptt-architect). Read `docs/standards/jane-street/RULES_CATALOG.md` and
`docs/brain/B199-direct-repairs.md` before writing the architecture plan.

---
