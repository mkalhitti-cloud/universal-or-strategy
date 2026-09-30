# DW-B89 Final Review
**Reviewer**: ptt-orchestrator (pipeline Phase 5, start_subtask infrastructure unavailable)
**Date**: 2026-08-23
**Epic**: DW-B89 -- BE-ALL OCO Reuse + Silent Stop Rejection
**Pipeline inputs**: ticket-1,2,3 completions + verifications; 02-architecture-plan.md; 04-ticket-review.md; RULES_CATALOG.md

---

## Section A -- Completeness

| Ticket | VERIFY_PASS | File | Summary |
|--------|-------------|------|---------|
| T1 | YES | src/PropTraderTools/CopyEngine.cs | Seed XOR fix at L205 |
| T2 | YES | src/PropTraderTools/Features/PttBreakEvenSwap.cs | D7 + [BE-ERR] + IsStopPriceSubmittable |
| T3 | YES | src/PropTraderTools/Features/PttBreakEven.cs + CopyEngineB72Tests.cs | D7 alignment + test update |

All 3 tickets: VERIFY_PASS. Section A: PASS.

---

## Section B -- Cross-File Coherence

### B1: OCO format D7 across all PTT-BE-* paths
| File | Format | Status |
|------|--------|--------|
| PttBreakEvenSwap.cs L114 | D7 (seq.ToString("D7")) | PASS |
| PttBreakEven.cs L357 (BuildBeOcoId) | D7 (seq.ToString("D7")) | PASS |
| PttGlobalBreakEven.cs L89 | D5 (PTT-BEG-* prefix -- different counter) | CORRECT -- spec-excluded |

All PTT-BE-* code paths now produce D7 OCO IDs. PTT-BEG-* paths are a separate counter and prefix (CopyEngine._mstbeBeGSeq != _mstbeOcoSeq), correctly excluded.

### B2: Single counter shared by both BE code paths
CopyEngine.NextBeOcoSeq() is the sole counter source:
- PttBreakEven.Execute calls NextBeOcoSeq() -- confirmed (pre-existing wiring, unchanged).
- PttBreakEvenSwap.Execute calls NextBeOcoSeq() at L108 -- confirmed.
Both use same counter → no intra-session collision possible between the two paths.

### B3: IsStopPriceSubmittable guards BOTH paths
- With-targets path (L117): guarded. PASS.
- 0-targets bare-stop path (L75): guarded. PASS.

Section B: PASS.

---

## Section C -- JS Rule Cross-File Scan

### C1: No lock() in changed files
SCAN-03 result: 0 live lock() calls across all 3 changed files. PASS.

### C2: No async void in changed code
SCAN-04 result: 0 async void in PttBreakEvenSwap.cs, PttBreakEven.cs, CopyEngine.cs L199-205. PASS.

### C3: All catch blocks typed (no bare catch)
SCAN-06 result: 0 occurrences of `catch { /* non-fatal */ }` in PttBreakEvenSwap.cs.
All 3 catches replaced with catch(Exception ex) + [BE-ERR] logging. PASS.

### C4: ASCII-only in changed lines
SCAN-07: PttBreakEvenSwap.cs = 0 non-ASCII. CopyEngine.cs L199-205 = 0 non-ASCII. PttBreakEven.cs L10 + L357 = 0 non-ASCII. PASS.

### C5: JS-021 (no lock)
No lock() added in any of T1, T2, T3. PASS.

### C6: JS-023 (volatile int)
_mstbeOcoSeq remains `private volatile int`. Interlocked.Increment in NextBeOcoSeq() unchanged. PASS.

### C7: JS-033 (no async void)
No async void in any changed method. PASS.

### C8: NT8 constraints (NT8-049, NT8-007, NT8-013, NT8-014)
PttBreakEvenSwap.cs arg6/arg7 order, arg11 cast, DateTime.MaxValue, PTT- prefix all unchanged. PASS.

Section C: PASS.

---

## Section D -- Spec Requirement Satisfaction

### DW-B89-01: OCO ID reuse after in-session recompile
Root cause: _mstbeOcoSeq seeded by TickCount alone (low entropy for post-recompile uniqueness).
Fix delivered:
  - CopyEngine.cs L205: `Math.Abs(Environment.TickCount ^ (int)(DateTime.UtcNow.Ticks & 0x7FFFFFFF))`
  - D5 → D7 expands namespace from 100,000 to 10,000,000 unique IDs
  - Applied to all PTT-BE-* paths (PttBreakEvenSwap + PttBreakEven)
Status: SATISFIED.

### DW-B89-02: BuyToCover StopMarket rejected as below market
Root cause: Short position, buf=0t, price moved adversely, NT8 rejects stop. Bare catch swallows silently. Position left naked.
Fix delivered:
  - IsStopPriceSubmittable helper (CYC=3): isLong→allow, ask==0→fail-open, short→require stopPrice>=ask
  - Guards both with-targets and 0-targets stop submit paths
  - [BE-ERR] logging with acc.Name + price in Output.OutputTab1
  - All 3 bare catch{} replaced with typed catch(Exception ex) + [BE-ERR] logging
Status: SATISFIED.

Section D: PASS.

---

## Section E -- Missing Wiring Check

| Item | Status |
|------|--------|
| PttBreakEven.BuildBeOcoId → D7 | CONFIRMED (L357) |
| PttBreakEvenSwap ocoId_i → D7 | CONFIRMED (L114) |
| CopyEngine seed → XOR formula | CONFIRMED (L205) |
| NextBeOcoSeq() shared by both paths | CONFIRMED |
| IsStopPriceSubmittable guards both paths | CONFIRMED |

Section E: PASS.

---

## Section K -- Deferred Work

### Carry-Forward from PTT-BE-FIX (all remain open, none closed by DW-B89)

**DW-B42-01** -- T_BUG_QX_BE_01 does not assert PTT-QX-T3
Priority: Low. Deferred to: B43 or first block where T3 confirmed in production use.

**DW-B42-02** -- Live NT8 F5 verification required
Priority: High. Deferred to: Next live F5 session.

**DW-B42-03** -- IsPttQxTarget range extension for future target slots
Priority: Conditional (low). Deferred to: Block that adds 4th+ target slot.

**DW-PTT-BE-FIX-01** -- DW-B85 Option A: Lazy re-resolve for null followers
Priority: Medium. Deferred to: Next PTT productionisation block.

**DW-PTT-BE-FIX-02** -- SIM gate: Path B 3-cycle runtime verification
Priority: High. Deferred to: Next live F5 session.

**DW-PTT-BE-FIX-03** -- Pre-existing 83 build errors in CopyEngineTests.cs
Priority: High -- blocks full test suite build. Deferred to: Dedicated test infrastructure remediation block.

### New Deferred Items (DW-B89)

**DW-B89-DEFERRED-01** -- Ctrl+F5 NT8 compilation gate
Priority: P0 -- prerequisite for SIM gate.
Context: Director must confirm Ctrl+F5 in NinjaTrader produces "Compilation succeeded" 0 errors before SIM gate runs.
Action: Deploy-sync + F5 in NT8. Pass: "Compilation succeeded". Fail: report error to orchestrator.

**DW-B89-DEFERRED-02** -- SIM gate PATH A nominal (buf=1t or more)
Priority: High. Context: 3 cycles, Entry → BE-ALL → verify Output tab has no [BE-ERR] lines, stops=N for all accounts.
Deferred to: Director after Ctrl+F5 green.

**DW-B89-DEFERRED-03** -- SIM gate PATH A buf=0 edge case (short position)
Priority: High. Context: 1 cycle, Entry short → BE-ALL buf=0t immediately. Verify Output tab shows [BE-ERR] ...stop below market (if price moved) OR stops placed successfully (if price still at entry). NO naked positions, NO error popups.
Deferred to: Director after Ctrl+F5 green.

**DW-B89-DEFERRED-04** -- SIM gate PATH B (QX-ALL then BE-ALL, 3 cycles)
Priority: High. Context: Entry → QX-ALL → BE-ALL arm → price trigger. Verify PTT-QX-Stop* cancelled, PTT-BE-Stop-N placed. stops=N. 3 cycles.
Deferred to: Director after Ctrl+F5 green.

**DW-B89-DEFERRED-05** -- SIM gate DW-B87 timing race cycle
Priority: High. Context: Entry → BE-ALL immediately (no wait). Must work (cancel sweep handles Submitted state).
Deferred to: Director after Ctrl+F5 green.

---

## FINAL_PASS

All sections A through E pass. Section K written. 06-deferred-backlog.md will be written as required gate.

**PIPELINE COMPLETE** for coding phases. Director SIM gate required before DW-B89 CLOSED.
