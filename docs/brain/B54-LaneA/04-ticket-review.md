# Ticket Review: B54-LaneA — UI Live-Truth Sync (DW-B54-03 P0)

**Reviewer**: ptt-ticket-reviewer (Phase 3.5)
**Date**: 2026-08-09
**Ticket file**: `docs/brain/B54-LaneA/04-tickets.md`
**Plan file**: `docs/brain/B54-LaneA/02-architecture-plan.md`
**Spec**: `specs/002-trade-copier-spec.html` id="section-b54"
**Rules**: `docs/standards/jane-street/RULES_CATALOG.md`

---

## T1 — B54-LaneA-T1: UI Live-Truth Sync (DW-B54-03 P0)

### Traceability: PASS

| Check | Result | Evidence |
|---|---|---|
| T1. References DW-B54-03 | PASS | Ticket §1 table row: `DW-B54-03 \| P0 \| UI state desync: copy-enabled button does not reflect engine truth after F5 or surface create` |
| T2. All 3 .cs files covered | PASS | §2 Files Modified lists `CopyEngine.cs`, `TradeCopierPanel.cs`, `TradeCopierWindow.cs` plus test file |
| T3. CopyEngineTests.cs with 3 [Fact] tests | PASS | §3.4 specifies exactly 3 [Fact] methods (`T_B54_01`, `T_B54_02`, `T_B54_03`) plus 2 private helpers |
| T4. Method signatures match plan exactly | PASS | A1–A4 (CopyEngine), B1–B4 (Panel), C1–C4 (Window) all match plan §3–§5 structurally. Test method names differ slightly from the plan draft (plan was REVIEW_PENDING) — semantics and asserts are identical. BuildRulesXml uses string concat vs. plan's interpolation — functionally identical. No structural mismatch. |

No phantom work items (all ticket items trace to plan §3–§6 or spec DW-B54-03).
No missing work items (all plan sections A1–A4, B1–B4, C1–C4, test design §6 are covered).

---

### JS Pre-Check: PASS

| Rule | Check | Result | Evidence |
|---|---|---|---|
| JS-021 | No `lock()` in any new or modified method | PASS | Ticket §5 table lists JS-021 enforcement via SCAN-01. No `lock()` described in any method body. |
| JS-002 | No `return null` in new code | PASS | All new methods are `void` (ApplyCopyState, OnCopyEnabledChanged, toggle handlers) or return `bool` (IsEnabled property). Lambda `return;` in ApplyCopyState is correctly identified as void guard-return, not null return. |
| JS-033 | No `async void` in new code | PASS | ApplyCopyState is `private void` (synchronous). Dispatcher.InvokeAsync is an inner expression inside the synchronous method — does not make the containing method async. §5 explicitly cites JS-033 and explains the distinction. |

---

### NT8 Check: PASS

| Rule | Check | Result | Evidence |
|---|---|---|---|
| NT8-001 | CopyRulesContainer.CopyEnabled uses `{ get; set; }` not `{ get; init; }` | PASS | §3.1 A2: `public bool CopyEnabled { get; set; }` with explicit note "NOT `init`". |
| NT8-003 | No `volatile double` or `volatile float` in new code | PASS | §5 table: "_isCopyEnabled is `volatile bool` (pre-existing, not changed). No new volatile fields added." |
| NT8 WPF lifecycle | Dispatcher.InvokeAsync used for UI updates from non-UI thread | PASS | A4 fires `CopyEnabledChanged` from LoadRules (potentially NT8 init thread). ApplyCopyState correctly marshals to UI thread via `Dispatcher.InvokeAsync`. |
| NT8 WPF null guard | Panel button null-guarded, Window button not | PASS | §3.2 B1 null-guards `_copyToggleBtn2` for ChartTrader panel template quirk. §3.3 C1 correctly omits null guard because Window WPF lifecycle guarantees control init before OnLoaded. |

---

### CYC Pre-Check: PASS

| Check | Method | CYC | Threshold | Result | Evidence |
|---|---|---|---|---|---|
| C1 | ApplyCopyState (Panel) | 2 | 8 | PASS | §9 table: CYC=2 (null-check branch). Matches plan §4 B1. |
| C2 | IsEnabled property | 1 | 8 | PASS | §9 table: CYC=1 (expression-bodied, no branches). |
| C3 | OnCopyToggle (modified) | 1 | 8 | PASS | §9 table: CYC=1 (straight-line call). |
| C4 | OnGlobalToggle (modified) | 1 | 8 | PASS | §9 table: CYC=1 (straight-line call). |

All 17 new and modified methods listed in §9: CYC <= 8. No method exceeds threshold.

---

### Test Coverage: PASS

| Method | [Fact] specified? | Test name |
|---|---|---|
| `IsEnabled` (new property) | PASS — covered by T_B54_01/02/03 which assert `engine.IsEnabled` | Indirect |
| `SaveRules` (modified) | PASS — T_B54_03 exercises SaveRules with overridePath | `T_B54_03_SaveThenLoadRules_RoundTripPreservesCopyEnabled` |
| `LoadRules` (modified) | PASS — T_B54_01 and T_B54_02 exercise LoadRules with overridePath | `T_B54_01_...`, `T_B54_02_...` |
| `CopyEnabledChanged` event firing | PASS — T_B54_01 and T_B54_02 assert `firedValue` captured by handler | `T_B54_01_...`, `T_B54_02_...` |

UI methods (ApplyCopyState, OnCopyEnabledChanged, OnLoaded, toggle handlers) are event-wired WPF methods — covered by the engine-level round-trip tests and INV-4/INV-5/INV-6/INV-7 code-review invariants. This is acceptable for NT8 WPF where unit-testing WPF visuals requires a running Dispatcher.

---

### Scan Checklist: PASS

All 7 scans present in ticket §6 with exact commands and required results:

| Scan | Command | Required Result | Present? |
|---|---|---|---|
| SCAN-01 | `Select-String "lock(" src\ -Recurse -Include *.cs` | 0 results | ✅ YES |
| SCAN-02 | `Select-String "async void " src\ -Recurse -Include *.cs` | 0 results | ✅ YES |
| SCAN-03 | `Select-String "return null" src\ -Recurse -Include *.cs` | 0 new instances | ✅ YES |
| SCAN-04 | `Select-String "throw new " src\ -Recurse -Include *.cs` | 0 new instances | ✅ YES |
| SCAN-05 | `python scripts/complexity_audit.py` | All new methods CYC <= 8 | ✅ YES |
| SCAN-06 | `dotnet build` | 0 errors | ✅ YES |
| SCAN-07 | `dotnet test` | All [Fact] pass | ✅ YES |

Post-scan sync step also specified: `powershell -File scripts\verify_links.ps1 -Fix`

**Defense-in-depth rationale**: This checklist is the engineer's contract (Layer 1 of 3). Layer 2 = engineer self-report in ticket-1-completion.md. Layer 3 = verifier independent run in ticket-1-verification.md. All three layers anchor to the same 7-scan definitions in this ticket.

---

### File Routing: PASS

All `.cs` file paths point to Wave workspace: `C:\WSGTA\universal-or-strategy\src\PropTraderTools\`
No Director workspace (`c:\WSGTA\universal-or-strategy-director`) paths for `.cs` files.

---

### Completeness Checks: PASS

| Check | Result | Evidence |
|---|---|---|
| P1. 7-scan checklist present (SCAN-01 to SCAN-07) | PASS | §6 lists all 7 scans with commands and required results |
| P2. [Fact] test names xUnit (not NUnit/MSTest) | PASS | Tests use `[Fact]`, `Assert.True`, `Assert.False` — xUnit API throughout. No `[Test]`, `[TestMethod]`, `Assert.That`, or `Assert.AreEqual`. |
| P3. Reflection to reset _persistenceLoaded before each test | PASS | `ResetPersistenceLoaded(engine)` via `typeof(CopyEngine).GetField("_persistenceLoaded", BindingFlags.NonPublic \| BindingFlags.Instance)?.SetValue(engine, false)` called before each `LoadRules` call in T_B54_01, T_B54_02, T_B54_03. §7 test isolation note confirms. |
| P4. SaveRules writes CopyEnabled BEFORE serializing | PASS | §3.1 A3 insert point: "AFTER `container.Rules` has been populated" and "BEFORE the `XmlSerializer` constructor call". Code block shows `container.CopyEnabled = _isCopyEnabled;` placed before `var serializer = new XmlSerializer(...)`. |
| P5. LoadRules fires CopyEnabledChanged at END of deserialization block | PASS | §3.1 A4: "Add two statements at the end of the `try` block, AFTER all rules have been added to the engine, BEFORE `_persistenceLoaded = true`." Ordering confirmed correct. |
| P6. Toggle handlers do NOT directly mutate buttons | PASS | §3.2 B4: "Removes ALL direct button mutation from this handler permanently." §3.3 C4: "Removes ALL direct button mutation from this handler." §8 INV-6 and INV-7 enumerate this as verifier-checkable invariants. |
| P7. ApplyCopyState called from event/OnLoaded ONLY (not from toggle handler) | PASS | §8 INV-5: "No surface ever calls `ApplyCopyState` from a toggle handler directly. Code review: only callers are `OnLoaded` and `OnCopyEnabledChanged`." §2 architecture state machine shows the identical constraint. |

---

### Invariant Coverage: PASS

| Check | Result | Evidence |
|---|---|---|
| I1. "button state = engine state always" stated | PASS | §8 Global invariant: `for all surfaces s, at all times t: s.copyButton.IsGreen <-> CopyEngine.Instance.IsEnabled == true`. Also in §2 state machine invariant block. Holds after: F5, window re-open, NT cold start, LoadRules, any SetEnabled call. |
| I2. F5 restore path stated (SaveRules writes, LoadRules restores and fires event) | PASS | §8 INV-3: "After F5 cycle (`SaveRules` + `LoadRules`): enabled state restored — T_B54_03 passes." Architecture plan §7 invariant 5: "Persistence round-trip. `SaveRules()` writes `CopyEnabled` to XML. `LoadRules()` reads it back and fires the event." INV-1, INV-2, INV-3 enumerate specific pre/post conditions. |

---

## Overall: TICKET_REVIEW_PASS

All checks pass. Zero violations found.

| Category | Result |
|---|---|
| Traceability (T1–T4) | PASS |
| JS Pre-Check (J1–J3) | PASS |
| NT8 Check (J4–J5) | PASS |
| CYC Pre-Check (C1–C4) | PASS |
| Test Coverage | PASS |
| Scan Checklist (SCAN-01 to SCAN-07) | PASS |
| File Routing | PASS |
| Completeness (P1–P7) | PASS |
| Invariants (I1–I2) | PASS |

**TICKET_REVIEW_PASS** — ticket is cleared for Phase 4a engineer execution.

The engineer reads this document first, then [`docs/brain/B54-LaneA/04-tickets.md`](docs/brain/B54-LaneA/04-tickets.md).
Engineer entry point: ticket §2 (Files Modified) → §3 (Method Signatures) → §6 (7-Scan Checklist).
Engineer deliverable: `docs/brain/B54-LaneA/ticket-1-completion.md` with self-reported scan results (Layer 2).
Verifier entry point: `ticket-1-completion.md` → independent 7-scan run → `ticket-1-verification.md` (Layer 3).
