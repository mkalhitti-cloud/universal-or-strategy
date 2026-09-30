# PTT-COPIER-B6 Plan Review
**Status:** REVIEW_PASS
**Reviewer:** PTT Plan Reviewer
**Review Round:** 2 (second review — V-01 fix verification)
**Plan Version Reviewed:** `02-architecture-plan.md` (V-01 fix applied, produced 2026-07-06)
**Spec Reviewed:** `specs/002-trade-copier-spec.html`
**Rules Catalog Reviewed:** `docs/standards/jane-street/RULES_CATALOG.md`

---

## Result: REVIEW_PASS

All 8 review criteria pass. No violations found. V-01 fix is confirmed present and correct.

---

## V-01 Fix Verification

The first review returned REVIEW_FAIL with one violation (V-01): T4 was missing a fifth
spec-update item for the JSON→XML correction. The fix has been applied. Verification:

| Check | Location | Status |
|-------|----------|--------|
| Item 5 present in Section D.4 | Plan line 169: *"Correct any reference to 'JSON' in the B6 phase-detail section to read 'XML (copy_rules.xml)' — the plan implements `XmlSerializer`, not JSON"* | ✅ CONFIRMED |
| Item present in T4 DoD | Plan line 405: *"Any 'JSON' reference in the B6 phase-detail section corrected to 'XML (copy_rules.xml)'"* | ✅ CONFIRMED |

The spec's B6 phase-detail (line 1531 of `specs/002-trade-copier-spec.html`) currently reads
"JSON to NT UserDataDir" — confirming a real "JSON" reference exists and that T4's correction
item is both necessary and accurately targeted.

---

## Criterion-by-Criterion Findings

### Criterion 1 — Additive-Only Mandate

**PASS.**  
The plan declares explicitly: *"Zero deletions. Zero modifications to existing logic paths."*  
- `CopyEngine.cs`: 7 new private/public members appended; no existing code removed.  
- `TradeCopierWindow.cs`: 2 additive line insertions into existing lifecycle methods (`append` / `prepend`); no deletions.  
- `CopyEngineTests.cs`: 3 new `[Fact]` methods appended; existing 19 tests unchanged.  
- `specs/002-trade-copier-spec.html`: 4 sections added + 1 text correction; no deletions.

### Criterion 2 — JS-021: No lock() Usage (P0)

**PASS.**  
SCAN-01 (`grep -r "lock(" src/PropTraderTools/`) and SCAN-07 (belt-and-suspenders regex) are
both listed with a 0-result target. T1 DoD states explicitly: *"No lock() anywhere in new
code."* All new persistence methods are called on the NT main thread at lifecycle boundaries
(startup / shutdown) — no concurrent access, no lock required. No `lock()` is planned
anywhere in new or modified code.

### Criterion 3 — JS-023: volatile bool _isCopyEnabled Preserved (P1)

**PASS.**  
No changes are planned to `_isCopyEnabled` or any existing `CopyEngine` fields. The plan's
additive-only mandate and the zero-modification policy on `CopyEngine.cs` existing logic
paths ensures this field is untouched.

### Criterion 4 — JS-025: ConcurrentDictionary + ConcurrentBag (lock-free) — New Persistence Must Not Use lock() (P1)

**PASS.**  
`LoadRules()` populates the existing `_rules` (`ConcurrentBag<CopyRule>`) via iterative
`ConcurrentBag.Add()` calls — no lock, no field reassignment. `SaveRules()` calls
`_rules.ToArray()` (atomic snapshot on `ConcurrentBag`) — no lock. No new collection types
are introduced. The plan's constraint note on `_rules` explicitly prescribes the `Add()`
iterative approach to preserve `readonly` compatibility.

### Criterion 5 — JS-010: private CopyEngine() Constructor (Singleton) Preserved (P0)

**PASS.**  
The plan adds only `public void SaveRules(...)` and `public void LoadRules(...)` plus private
static helper methods. The private constructor is not referenced, not modified, and not
removed. Additive-only mandate enforces this structurally.

### Criterion 6 — JS-003: TrimSignal Has NO qty Field (P0)

**PASS.**  
No changes are planned to `TrimSignal`. The struct is not referenced in any B6 ticket scope.
The spec confirms the invariant at lines 717 and 746 and Section E of the spec (line 1057).
B6 scope is purely persistence and documentation.

### Criterion 7 — CYC <= 8 on All New Methods

**PASS.**  
All new methods have explicit CYC annotations in Section E:

| Method | CYC | Status |
|--------|-----|--------|
| `GetPersistencePath` | 1 | ✅ |
| `RuleToDto` | 1 | ✅ |
| `DtoToRule` | 1 | ✅ |
| `SaveRules` | 2 (try/catch = 1 branch) | ✅ |
| `LoadRules` | 3 (File.Exists + try/catch + foreach) | ✅ |

All well within the CYC ≤ 8 threshold. Risk R6 notes a `_persistenceLoaded` guard would add
+1 to `LoadRules` CYC (max 4) — still within threshold.

### Criterion 8 — NT8 Constraints

**PASS on all three sub-checks:**

| Check | Plan Evidence | Status |
|-------|--------------|--------|
| No async/await in OnInitialize or OnDestroyed | T2 DoD: *"No async/await introduced."* Persistence is synchronous `XmlSerializer` I/O. | ✅ |
| Dispatcher.InvokeAsync for UI callbacks from off-thread | D.2 states: *"No new Dispatcher.InvokeAsync calls."* All calls are on the NT main thread at lifecycle boundaries. | ✅ |
| TradeCopierWindow must NOT be sealed | Section G SCAN-07 note confirms the class must NOT contain the `sealed` keyword; no sealed modifier is being added. | ✅ |
| Math.Round for stop prices | No new `CreateOrder` calls; no new stop price calculations. Not applicable to B6 scope. | ✅ (N/A) |

### Criterion 9 — Scan Coverage: All 7 Mandatory Scans Listed with 0-Result Target

**PASS.**  
Section G lists all 7 scans:

| Scan | Pattern | 0-Result Target |
|------|---------|-----------------|
| SCAN-01 | `grep -r "lock(" src/PropTraderTools/` | ✅ |
| SCAN-02 | Non-ASCII chars in .cs files | ✅ |
| SCAN-03 | `Select-String -Pattern "FontFamily"` | ✅ |
| SCAN-04 | `Select-String -Pattern "#[0-9A-Fa-f]{6}"` | ✅ |
| SCAN-05 | `CreateOrder` without `PTT-` prefix | ✅ |
| SCAN-06 | `Select-String -Pattern "DateTime\.Now[^U]"` | ✅ |
| SCAN-07 | `Select-String -Pattern "\block\s*\("` | ✅ |

The SCAN-07 dual-interpretation note is reasonable and both interpretations pass.

### Criterion 10 — Backlog Disposition: DW-B5-03 and DW-B5-04 Explicitly Addressed

**PASS.**  

| Item | Decision | Location in Plan |
|------|----------|-----------------|
| DW-B5-03 (Rule persistence) | **ADDRESS IN B6** — implemented via T1 + T2 | Section B, Section C, Tickets T1/T2, Section J |
| DW-B5-04 (Spec HTML update) | **ADDRESS IN B6** — implemented via T4 | Section B, Section C, Ticket T4, Section J |

Both items are marked CLOSED in the forward ledger (Section J).

### Criterion 11 — Ticket Clarity: File Path, Method Signatures, Test Counts

**PASS.**  

| Ticket | File Path | Method Signatures | Test Count |
|--------|-----------|-------------------|------------|
| T1 | `src/PropTraderTools/CopyEngine.cs` | 5 static methods + 2 nested classes in Section E | N/A (impl) |
| T2 | `src/PropTraderTools/TradeCopierWindow.cs` | Exact insertion code shown in Section E | N/A (impl) |
| T3 | `src/PropTraderTools.Tests/CopyEngineTests.cs` | 3 `[Fact]` signatures in Section E | 3 new / 22 total |
| T4 | `specs/002-trade-copier-spec.html` | N/A (documentation) | N/A |

### Criterion 12 — Spec Alignment: T4 Describes 5 Sections/Items Including JSON→XML Correction

**PASS.**  
T4 lists 4 numbered update items (lines 398–401) plus the JSON→XML correction as a separate
DoD bullet (line 405). Total = 5 distinct items. The JSON→XML item targets the specific
"JSON" string at `specs/002-trade-copier-spec.html` line 1531, which was confirmed to
contain the text "JSON to NT UserDataDir" — a real, pre-existing error in the spec. The
item was added as V-01 fix and is present in both D.4 (line 169) and T4 DoD (line 405).

### Criterion 13 — No Scope Creep

**PASS.**  
Plan Section A states: *"New scope beyond backlog: None. B6 is purely backlog-driven."*
Section B corroborates: *"B6 scope is exactly the two deferred items plus their test
coverage."* No features beyond DW-B5-03 and DW-B5-04 are introduced. The `_persistenceLoaded`
guard (Risk R6) is noted as a conditional mitigation but is bounded within the same ticket.

---

## Summary

| # | Criterion | Result |
|---|-----------|--------|
| 1 | Additive-only mandate | ✅ PASS |
| 2 | JS-021: No lock() | ✅ PASS |
| 3 | JS-023: volatile bool _isCopyEnabled preserved | ✅ PASS |
| 4 | JS-025: ConcurrentBag lock-free (no lock in persistence) | ✅ PASS |
| 5 | JS-010: private CopyEngine() constructor preserved | ✅ PASS |
| 6 | JS-003: TrimSignal has no qty field | ✅ PASS |
| 7 | CYC <= 8 on all new methods | ✅ PASS |
| 8 | NT8 constraints (no async/await in lifecycle, Dispatcher, not sealed, Math.Round) | ✅ PASS |
| 9 | 7 mandatory scans listed with 0-result target | ✅ PASS |
| 10 | DW-B5-03 and DW-B5-04 explicitly addressed | ✅ PASS |
| 11 | Ticket clarity (file path, signatures, test counts) | ✅ PASS |
| 12 | T4 describes 5 items including JSON→XML correction | ✅ PASS |
| 13 | No scope creep | ✅ PASS |

**Violations found: 0**

---

*End of PTT-COPIER-B6 Plan Review*
