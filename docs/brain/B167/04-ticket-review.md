# Ticket Review: B167
**Reviewer:** ptt-ticket-reviewer  
**Phase:** 3.5 (Ticket Review)  
**Tickets under review:** `docs/brain/B167/04-tickets.md`  
**Plan source:** `docs/brain/B167/02-architecture-plan.md` (REVIEW_PASS, 22/22 checks)  
**Spec cards:** `docs/brain/DW-B167-01/00-dw-card.md`, `docs/brain/no-pipeline-repairs.md` (DW-B167-02 entry at lines 596–688)  
**Verdict:** **TICKET_REVIEW_PASS**

---

## Ticket 1 — DW-B167-01: DRAIN-ABORT-FLAT guard tightening

### Per-Check Results

| # | Description | Result | Citation (ticket line) |
|---|-------------|--------|------------------------|
| 1 | Ticket 1 cites DW-B167-01 spec req ID | **PASS** | Line 12: `**Spec req IDs:** DW-B167-01` |
| 2 | All 4 touches (T1–T4) present with BEFORE/AFTER code | **PASS** | T1 lines 51–98, T2 lines 102–151, T3 lines 155–177, T4 lines 181–201 |
| 3 | Field name is `HadPositionAtDrainTime` (NOT `WasFlat`) | **PASS** | Line 65: `internal bool HadPositionAtDrainTime { get; private set; }` — correct name throughout; `WasFlat` nowhere in ticket |
| 4 | New ctor param `bool hadPositionAtDrainTime` as LAST param (after `long timestampTicks`) | **PASS** | Lines 44–46: `long timestampTicks,` → `bool hadPositionAtDrainTime)` as final param |
| 5 | Ctor body assigns `HadPositionAtDrainTime = hadPositionAtDrainTime;` | **PASS** | Lines 94–95: `TimestampTicks = timestampTicks;` followed by `HadPositionAtDrainTime = hadPositionAtDrainTime;` |
| 6 | Capture uses `!IsFlat(FindPosition(follower, instrument))` NOT `HasOpenPosition` | **PASS** | Line 111: `bool hadPos = !IsFlat(FindPosition(follower, instrument));` Line 114 critical note confirms NOT `HasOpenPosition` |
| 7 | Local var named `hadPos` | **PASS** | Line 111: `bool hadPos = ...` |
| 8 | `hadPos` passed as new last positional arg after `now` | **PASS** | Lines 129–148 AFTER block: `now,` → `hadPos` as final arg |
| 9 | Guard changes FROM `if (IsFlat(FindPosition(...)))` TO `if (payload.HadPositionAtDrainTime && IsFlat(FindPosition(...)))` | **PASS** | Lines 160–167: exact BEFORE/AFTER match required text |
| 10 | Comment at guard (3) updated or preserved | **PASS** | BEFORE `// (3) NEW -- abort if flat`; AFTER `// (3) abort if flat and had position at drain-start` — updated |
| 11 | Log token is exactly `[DRAIN-SUBMIT-PREFILL]`, ASCII, includes acctKey | **PASS** | Line 194: `"[DRAIN-SUBMIT-PREFILL] acct=" + acctKey` — ASCII only, acctKey present |
| 12 | Log fires on `!payload.HadPositionAtDrainTime` path | **PASS** | Line 192: `if (!payload.HadPositionAtDrainTime)` |
| 13 | Log placed BEFORE `SubmitEntryDirect` call | **PASS** | Lines 192–196: diagnostic `if` block first, `SubmitEntryDirect(...)` after |
| 14 | Log uses `NinjaTrader.Code.Output.Process(...)` with `OutputTab1` | **PASS** | Lines 193–195: `NinjaTrader.Code.Output.Process(... NinjaTrader.NinjaScript.PrintTo.OutputTab1)` |
| 15 | `SubmitDrainedEntry` CYC 5→6 (T4 adds 1 branch) | **PASS** | Line 201: `SubmitDrainedEntry CYC: 5 → 6. ≤ 8. PASS.` |
| 16 | `DrainThenDispatch` CYC unchanged | **PASS** | Line 151: `CYC impact: Zero.` — local var, no new branch |
| 17 | `PendingDispatchDrain` CYC=0 (data class, unchanged) | **PASS** | Line 98: `CYC impact: Zero. This is a data class. No control-flow added.` |
| 18 | All method CYC ≤8 | **PASS** | Max CYC = 6 (`SubmitDrainedEntry` after T4). All other methods ≤4 or 0. |
| 19 | Ticket explicitly states NO changes to `AbortDrainOnFill` | **PASS** | Lines 239–241 SCAN-06: "`AbortDrainOnFill` method is not referenced in any T1–T4 change instruction." |
| 20 | JS-001 (no new throw paths) | **PASS** | SCAN-01 lines 207–212: all T1–T4 additions confirmed cannot throw |
| 21 | JS-021 (no new `lock()` blocks); `IsFlat`/`FindPosition` lock-free | **PASS** | SCAN-02 lines 214–219: explicit lock-free confirmation, no `lock()`/`Monitor`/`Mutex` introduced |
| 22 | ASCII-only: all new string literals ASCII | **PASS** | SCAN-05 lines 233–236; line 199 explicit assertion: `"[DRAIN-SUBMIT-PREFILL] acct="` — ASCII only |
| 23 | JS-002 (no null return) | **PASS** | SCAN-04 lines 227–231: `HadPositionAtDrainTime` is `bool` (value type), all modified methods are `void`/ctor |
| 24 | Ticket 1 contains ALL 7 scan items (SCAN-01 through SCAN-07) | **PASS** | Lines 207–246: SCAN-01, SCAN-02, SCAN-03, SCAN-04, SCAN-05, SCAN-06, SCAN-07 — all 7 present |
| 25 | All 4 xUnit `[Fact]` method names present | **PASS** | Line 254: `SubmitDrainedEntry_HadPositionFalse_DoesNotAbort`; line 270: `SubmitDrainedEntry_HadPositionTrue_IsFlat_Aborts`; line 285: `SubmitDrainedEntry_HadPositionTrue_NotFlat_Submits`; line 299: `DrainThenDispatch_CapturesHadPositionAtDrainTime` — all 4 present with full setup/assert/confirms |

**Traceability:** PASS — every T1–T4 change instruction maps to plan §2 T1–T4. No phantom work. No missing plan items.  
**JS Pre-Check:** PASS — JS-001, JS-002, JS-021, ASCII-only: all confirmed.  
**CYC Pre-Check:** PASS — no method exceeds CYC 6. Max limit 8.  
**NT8 Check:** PASS — no async/await in lifecycle, no `Account.All` outside Loaded, no `sealed` on window, no `FontFamily`, no hex color, no `CreateOrder` without PTT- prefix, no `DateTime.Now`.  
**Test Coverage:** PASS — all 4 public test method names present with full setup+assert+confirms structure.  
**Scan Checklist:** PASS — SCAN-01 through SCAN-07 all present.  
**File Routing:** PASS — `src/PropTraderTools/CopyEngine.cs` (Wave workspace).  
**SIM Gate:** PASS — pre-fill drag test (steps 1–7) and post-flatten abort test (steps 8–11) both present at lines 311–331.

**VERDICT: TICKET_REVIEW_PASS**

---

## Ticket 2 — DW-B167-02: TrySetSlateReadyOnDrainFill Formal Record

### Per-Check Results

| # | Description | Result | Citation (ticket line) |
|---|-------------|--------|------------------------|
| 26 | Ticket 2 marked READ-ONLY — zero .cs edits | **PASS** | Line 339: `ENGINEER INSTRUCTION: READ-ONLY. Zero .cs edits permitted for this ticket.` |
| 27 | Ticket 2 does NOT attempt re-engineering of the fix | **PASS** | All 5 engineer steps are source confirmation / documentation only; no edit instructions exist |
| 28 | Source confirmation procedure for L1032 and L8313 present | **PASS** | Step 2 (lines 360–369): confirm L1032 content. Step 3 (lines 371–382): confirm `IsPttCopyEntry` at L8313 with both clauses. |
| 29 | Ticket 2 cites no-pipeline-repairs.md DW-B167-02 as authority | **PASS** | Line 384: `Confirm the source matches the no-pipeline-repairs.md DW-B167-02 entry exactly (BEFORE/AFTER blocks at lines 614–628 of that file).` |
| 30 | 7-scan checklist present — all items trivially PASS for no-code-change ticket | **PASS** | Lines 424–447: SCAN-01 through SCAN-07 all present; each explicitly states "No code changes. Trivially PASS." (SCAN-01, SCAN-02, SCAN-04, SCAN-05) or with source-match confirmation (SCAN-03, SCAN-06, SCAN-07) |
| 31 | `TrySetSlateReadyOnDrainFill_EntryFill_SetsSlateReady` present | **PASS** | Line 453: test 5 named exactly; full setup/assert/confirms structure present |

**Traceability:** PASS — ticket maps to DW-B167-02 (no-pipeline-repairs.md). No phantom work. READ-ONLY formal record exactly matches plan §3 scope.  
**JS Pre-Check:** PASS — no code changes; all 7-scan items trivially pass.  
**CYC Pre-Check:** PASS — no code changes; CYC delta = 0.  
**NT8 Check:** PASS — no code changes.  
**Test Coverage:** PASS — 1 [Fact] test `TrySetSlateReadyOnDrainFill_EntryFill_SetsSlateReady` present with full setup/assert/confirms.  
**Scan Checklist:** PASS — SCAN-01 through SCAN-07 all present.  
**File Routing:** PASS — `src/PropTraderTools/CopyEngine.cs` (Wave workspace). READ-ONLY confirmation, no write.  
**SIM Gate:** PASS — steps 12–16 present at lines 468–476.

**VERDICT: TICKET_REVIEW_PASS**

---

## Scope Checks (Both Tickets)

| # | Description | Result | Citation |
|---|-------------|--------|----------|
| 32 | No ticket touches `IsFollowerPositionBlocked` or Signals 1–6 | **PASS** | T1 SCAN-07 lines 243–246: "not touched, referenced, or called anywhere in T1–T4 change instructions." T2 SCAN-07 lines 445–447: "No code changes anywhere in this ticket." |
| 33 | No ticket touches `AbortDrainOnFill` (explicitly noted as no-change) | **PASS** | T1 SCAN-06 lines 238–241: explicit zero-change confirmation. Not referenced in T2. |
| 34 | No ticket references B161–B166 fixes in an edit context | **PASS** | B161–B166 appears only in SCAN-07 scope confirmation statements (read-only reference). Zero edit instructions reference these fixes. |
| 35 | Both tickets include SIM gate criteria | **PASS** | T1 lines 311–331; T2 lines 468–476; Combined session table lines 484–491. |

---

## Violation Summary

**No violations found.**

All 35 mandatory checks PASS. No Jane Street rule violations (JS-001, JS-002, JS-021, JS-CYC, ASCII-only). No scope violations. No naming violations (`HadPositionAtDrainTime` used consistently; `WasFlat` not present in ticket despite appearing as alias in DW card — plan's naming override correctly applied). No missing [Fact] tests. No missing scan checklists. No missing SIM gate criteria. Ticket 2 READ-ONLY constraint correctly enforced.

---

## Overall: TICKET_REVIEW_PASS
