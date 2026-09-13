# PTT-DEFERRED-0911 Lane G — Ticket Review

**Epic:** PTT-DEFERRED-0911
**Lane:** G
**Phase:** 3.5 (Ticket Review)
**Reviewer:** ptt-ticket-reviewer
**Date:** 2026-09-12
**Tickets reviewed:** docs/brain/PTT-DEFERRED-0911/04-tickets-lane-g.md
**Plan reviewed:** docs/brain/PTT-DEFERRED-0911/02-architecture-plan-lane-g.md (REVIEW_PASS Rev 3)
**Rules catalog:** docs/standards/RULES_CATALOG.md

---

## Ticket Review: PTT-DEFERRED-0911 Lane G

### T-G1 — Upgrade TradeCopierPanel_BeBufferBox_FieldDoesNotExist to Option A-partial (DW-B26-01)

---

#### Traceability: PASS

- Spec requirement ID `DW-B26-01` present in ticket header. Maps to Plan Section 2 (CLOSED by Lane G). ✓
- All 4 file changes in ticket match Plan Section 3 exactly:
  - File 1 → Plan P1 (PttContracts.cs: add BreakEven to ICopyEngine) ✓
  - File 2 → Plan P2 (CopyEngine.cs: internal → public) ✓
  - File 3 → Plan P4 (TradeCopierPanel.cs: ExecuteBreakEven + DispatchShortcut update) ✓
  - File 4 → Plan T (CopyEngineTests.cs: delete old Skip test, add LaneGTests) ✓
- Plan P3 (DROPPED per Rev 2 V-002 resolution) correctly absent from ticket. ✓
- No phantom work (every ticket item traces to plan). ✓
- No missing plan work (all plan items covered). ✓

---

#### JS Pre-Check: PASS

| Rule | Severity | Ticket section checked | Finding |
|------|----------|----------------------|---------|
| JS-021 | P0 | JS constraints table (ticket lines 181–184); ExecuteBreakEven body (`engine.BreakEven(...)` only); StubEngine body (no lock) | No `lock()`, `Monitor`, `Mutex`, or `SemaphoreSlim` described anywhere. PASS. |
| JS-001 | P0 | ExecuteBreakEven method comment (`// JS-001: no throw`); CYC=1, single statement, no error paths | No `throw` described anywhere in new code. PASS. |
| JS-013 | P1 | CYC analysis table (ticket lines 165–174, `JS-013 PASS` verdict explicit) | All methods ≤ CYC 8. Max = 5 (`CopyEngine.BreakEven`, body unchanged). PASS. |
| JS-002 | P0 | All new methods are void; no null returns | No unexpected null return described. PASS. |
| JS-003 | P0 | No string literals used as state discriminators | Not applicable to this change. PASS. |
| JS-008 | P1 | No structs or SolidColorBrush in new code | Not applicable. PASS. |
| JS-023 | P0 | ExecuteBreakEven is internal static, not a UI event handler; no WPF element mutations | No off-thread UI update described. PASS. |

No JS rule violations found in ticket descriptions.

---

#### CYC Pre-Check: PASS

| Method | Ticket-reported CYC | Limit | Verdict |
|--------|-------------------|-------|---------|
| `ExecuteBreakEven` | 1 (straight-line; one statement) | ≤ 8 | PASS |
| `DispatchShortcut` | 5 (unchanged) | ≤ 8 | PASS |
| `CopyEngine.BreakEven` | 5 (body unchanged; access modifier only) | ≤ 8 | PASS |
| `StubEngine.BreakEven` | 1 (records call; no branches) | ≤ 8 | PASS |
| `TradeCopierPanel_ExecuteBreakEven_PassesBufferToEngine` | 1 (straight-line test) | ≤ 8 | PASS |

No method at risk of exceeding CYC 8.

---

#### NT8 Check: PASS

| Constraint | Ticket section checked | Finding |
|-----------|----------------------|---------|
| ICopyEngine `using NinjaTrader.Cbi` guidance | Ticket lines 41–43 (explicit note: check existing using directive; use short names if present) | Guidance present. PASS. |
| StubEngine `null` for Account/Instrument — no NT8 cctor | Ticket line 129–131 (`// No NT8 cctor triggered`); Plan Section 6 row 5 confirms safe | Explicitly documented. PASS. |
| `ExecuteBreakEven`: no WPF/UI deps | Ticket method comment line 63 (`// No NT8 UI deps`) | Confirmed. PASS. |
| No `async`/`await` in lifecycle methods (NT8-001) | Ticket SCAN-03 guards this | No async described. PASS. |
| No ATM API usage (NT8-008/009) | Ticket SCAN-05 guards this | No ATM calls in new code. PASS. |
| No `DateTime.Now` (NT8-006) | Ticket SCAN-03 row covers async; no DateTime.Now in any described code | Not present. PASS. |
| No hardcoded hex colors (NT8-005) | No WPF markup in any changed file | Not applicable. PASS. |
| No `FontFamily` override (NT8-004) | No WPF markup in any changed file | Not applicable. PASS. |
| `CreateOrder` name prefix "PTT-" (NT8-003) | No `CreateOrder` in any new code | Not applicable. PASS. |
| `InternalsVisibleTo` — ticket confirms already granted | Ticket line 224–225 ("Already granted at CopyEngine.cs:46. No new assembly attribute needed") | Confirmed. PASS. |

No NT8 constraint violations found.

---

#### Test Coverage: PASS

| New/changed method | Ticket [Fact] present | Assertion |
|-------------------|----------------------|-----------|
| `TradeCopierPanel.ExecuteBreakEven` | `TradeCopierPanel_ExecuteBreakEven_PassesBufferToEngine` [Fact] (no Skip) | `Assert.Equal(1, stub.BreakEvenCallCount)` AND `Assert.Equal(3, stub.LastBuffer)` |
| `CopyEngine.BreakEven` (access modifier only; body unchanged) | No new [Fact] required — existing coverage of body unchanged; access-modifier change is compile-only | N/A |
| `StubEngine.BreakEven` | Internal test helper; covered by the [Fact] that exercises it | Covered |

**Behavioral strength:** Ticket test (`BreakEvenCallCount==1 AND LastBuffer==3`) is stronger than
Plan Section T.2 (which only asserted `LastBuffer==3`). The additional `BreakEvenCallCount`
assertion confirms the method was actually called exactly once — not a false positive from a
default field value. This is an additive improvement over the plan. No violation.

**Comparison to Option B:** The old `[Fact(Skip=...)]` was a reflection-based field-absence
assertion — it verified a field did NOT exist. The new `[Fact]` verifies the dispatch path
actually invokes `ICopyEngine.BreakEven` with the correct `beBuffer` value. Demonstrably
stronger. ✓

No methods missing [Fact] coverage.

---

#### Scan Checklist: PASS

All 7 scans present in ticket (lines 192–198). Per-scan verification:

| Scan | Present in ticket | SCAN-07 required content | Verdict |
|------|-----------------|--------------------------|---------|
| SCAN-01 | ✓ `grep -r "lock(" src/` → 0 matches | — | PASS |
| SCAN-02 | ✓ CYC audit all new/changed methods → all ≤ 8 | — | PASS |
| SCAN-03 | ✓ `grep -r "async\|await"` → 0 new async | — | PASS |
| SCAN-04 | ✓ Non-ASCII byte scan → 0 non-ASCII | — | PASS |
| SCAN-05 | ✓ No ATM API usage → 0 | — | PASS |
| SCAN-06 | ✓ `dotnet build` + `dotnet test` → 179p/363s/542t; new [Fact] PASSES | — | PASS |
| SCAN-07 | ✓ Structural confirmation | `ExecuteBreakEven` seam exists ✓ · old `[Fact(Skip=...)]` deleted ✓ · new `[Fact]` passes ✓ · DW-B26-01 CLOSED ✓ | PASS |

All four required SCAN-07 confirmation items present. Engineer contract is complete and
verifier anchor is established (Layer 1 of the 3-layer defense-in-depth chain).

---

#### File Routing: PASS

All 4 file paths point to Wave workspace `src/PropTraderTools/...`:
- `src/PropTraderTools/Core/PttContracts.cs` ✓
- `src/PropTraderTools/CopyEngine.cs` ✓
- `src/PropTraderTools/TradeCopierPanel.cs` ✓
- `src/PropTraderTools/CopyEngineTests.cs` ✓

No Director workspace paths for any `.cs` file. ✓

---

#### Option A-Partial Achievement: PASS

- "WPF full instantiation not required": Ticket line 129 explicitly states
  "Option A-partial: static seam called directly from test, no WPF/NT8 instantiation." ✓
- "Option B REPLACED (not augmented)": Ticket lines 97–101 explicitly instruct deletion of
  lines 7735–7747 with documented rationale ("These lines are the Option B reflection-based
  absence assertion that Lane G replaces."). Ticket line 130 confirms "Replaces … (deleted)." ✓

---

#### Completeness: PASS

| Item | Status |
|------|--------|
| 4 files with exact line/location guidance | ✓ All 4 have exact line numbers and insertion anchors |
| Method signatures compile-ready | ✓ Full signatures with parameter names and access modifiers for all 4 file changes |
| StubEngine implements ALL 5 ICopyEngine methods | ✓ `BreakEven` + `RelayBe` + `RelayTrim` + `RelayFlatten` + `RelayCancel` — all 5 present |
| Old `[Fact(Skip=...)]` explicitly DELETED | ✓ Step 1 in CopyEngineTests.cs section: "Delete lines 7735–7747" |
| Test count delta arithmetic correct | ✓ 178p/364s/542t → 179p/363s/542t (−1 skip, +1 pass, net=0) |

---

#### VERDICT: TICKET_REVIEW_PASS

No violations found across all 8 check categories.

---

## Overall: TICKET_REVIEW_PASS

T-G1 is the only ticket in Lane G. It passes all checks. The engineer contract is complete:

- Spec requirement ID present and traceable to plan. ✓
- All method signatures compile-ready. ✓
- [Fact] test name and assertions specified (stronger than plan). ✓
- 7-scan checklist present and correct (SCAN-01 through SCAN-07 with SCAN-07 carrying all 4 required structural confirmation items). ✓
- JS-021 (P0), JS-001 (P0), JS-013 (P1) all explicitly handled. ✓
- NT8 constraints documented at every affected site. ✓
- File routing: Wave workspace only. ✓
- Option B replaced (not augmented), Option A-partial pattern confirmed. ✓

**Safe to spawn ptt-engineer for Lane G Phase 4a.**

---

*ptt-ticket-reviewer · PTT-DEFERRED-0911 Lane G · Phase 3.5 · 2026-09-12*
