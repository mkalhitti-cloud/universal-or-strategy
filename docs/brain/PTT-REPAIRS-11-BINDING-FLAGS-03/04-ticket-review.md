# Ticket Review — PTT-REPAIRS-11-BINDING-FLAGS-03

**Reviewer role:** Phase 3.5 — PTT Ticket Reviewer  
**Source tickets:** `docs/brain/PTT-REPAIRS-11-BINDING-FLAGS-03/04-tickets.md`  
**Source plan:** `docs/brain/PTT-REPAIRS-11-BINDING-FLAGS-03/02-architecture-plan.md`  
**Rules applied:** JS hardcoded rules (role definition) — RULES_CATALOG.md not present in Wave workspace; Director-side rules re-applied from role definition mandate  
**Date:** Phase 3.5 review pass

---

## Ticket Review: PTT-REPAIRS-11-BINDING-FLAGS-03

### T1 — Fix BindingFlags.Instance→Static for GetSenderAccountName reflection test

---

#### 1. Traceability

**Status: PASS**

- Ticket opening section explicitly maps to **DW-09-03**: "Remove incorrect Skip from `GetSenderAccountName_ShouldBeReusedByOnTrailBeAccountUpdate`".
- All three edits (A, B, C) trace directly to architecture plan §3 Component List and §4 Class and Method Signatures.
- No phantom work detected: every edit in the ticket corresponds to a plan component.
- No missing work: all plan components (A = new helper, B = attribute change, C = call site switch) appear in exactly one ticket.

---

#### 2. JS Pre-Check (JS-021/023/025/001/002/003/008/009)

**Status: PASS**

| Rule | Check | Result |
|------|-------|--------|
| JS-021 — No `lock()` | SCAN-01 covers this; ticket Edit A explicitly states "No `lock()`" | PASS |
| JS-023 — No concurrent Dictionary | No Dictionary introduced; no shared state | PASS |
| JS-025 — No UI update from non-UI thread | No UI code; test-only change | PASS |
| JS-001 — No `throw` in hot path | SCAN-02 covers this; ticket Edit A explicitly states "No `throw`" | PASS |
| JS-002 — No `return null` as sentinel | `GetStaticMethod` delegates to `Type.GetMethod` which may return null (reflection API contract); this is not a new design-choice null sentinel — `Assert.NotNull(m)` is the explicit validation gate. No new method in the ticket is designed to return null as a mode/state signal | PASS |
| JS-003 — No empty string / missing-key as sentinel | Not applicable; no mode/state enum involved | PASS |
| JS-008 — No mutable struct fields | No structs introduced or modified | PASS |
| JS-009 — No unfrozen SolidColorBrush | No WPF/UI code touched | PASS |

---

#### 3. CYC Pre-Check

**Status: PASS**

| Method | CYC | Assessment |
|--------|-----|------------|
| `GetStaticMethod(string name)` | 1 | Single expression-body, zero branches — confirmed by SCAN-04 inspection |
| `GetSenderAccountName_ShouldBeReusedByOnTrailBeAccountUpdate` (test) | 1 | Two sequential statements, zero branches |

No method exceeds CYC=8. No split required.

---

#### 4. NT8 Check

**Status: PASS**

| Constraint | Check | Result |
|------------|-------|--------|
| No `async/await` in lifecycle method | None introduced | PASS |
| No `Account.All` outside Loaded handler | No NT8 types used | PASS |
| No `sealed` on TradeCopierWindow | Not applicable | PASS |
| No `FontFamily` on WPF element | No UI code | PASS |
| No hardcoded hex color | No UI code | PASS |
| No `CreateOrder` with non-PTT- name | Not applicable | PASS |
| No `DateTime.Now` | SCAN-03 covers this; explicitly stated in Edit A notes | PASS |
| No production `.cs` changes | Ticket §File and DO NOT TOUCH table both confirm: "Zero production changes required or permitted" | PASS |
| No `deploy-sync.ps1` required | Confirmed in plan §3: "No `deploy-sync.ps1` required"; ticket does not invoke it | PASS |
| ASCII-only identifiers | SCAN-05 covers this; all identifiers listed (`GetStaticMethod`, `name`, `BindingFlags`, `NonPublic`, `Static`) are 7-bit ASCII | PASS |

---

#### 5. Test Coverage

**Status: PASS**

| Method | [Fact] test specified |
|--------|-----------------------|
| `GetStaticMethod(string name)` | `GetSenderAccountName_ShouldBeReusedByOnTrailBeAccountUpdate` — asserts `Assert.NotNull(m)` where `m = GetStaticMethod("GetSenderAccountName")`. SCAN-07 validates this test passes. |

The new `GetStaticMethod` helper is a private static infrastructure method whose only consumer within this ticket is the un-skipped test. The test constitutes both functional verification and the [Fact] coverage requirement. All new methods are covered.

---

#### 6. Scan Checklist Presence (SCAN-01 through SCAN-07)

**Status: PASS**

All 7 scans present with exact shell commands and explicit zero-to-pass criteria:

| Scan | Command present | Pass condition explicit |
|------|----------------|------------------------|
| SCAN-01 | `grep -n "lock(" src/PropTraderTools/CopyEngineTests.cs` | Zero new `lock(` matches |
| SCAN-02 | `grep -n "throw " src/PropTraderTools/CopyEngineTests.cs` | Zero new `throw` statements |
| SCAN-03 | `grep -n "DateTime\.Now" src/PropTraderTools/CopyEngineTests.cs` | Zero `DateTime.Now` references |
| SCAN-04 | Inspection of `GetStaticMethod` expression body | CYC = 1, zero branches |
| SCAN-05 | Enumerated identifier list | All 7-bit ASCII |
| SCAN-06 | `dotnet build src/PropTraderTools/PropTraderTools.csproj` | `0 Error(s)` |
| SCAN-07 | `dotnet test src/PropTraderTools/PropTraderTools.csproj --filter "FullyQualifiedName~GetSenderAccountName_ShouldBeReusedByOnTrailBeAccountUpdate"` | `PASSED` |

Per-ticket scan checklist is complete. Engineer contract and verifier anchor are in place.

---

#### 7. File Routing

**Status: PASS**

All C# source paths reference `src/PropTraderTools/CopyEngineTests.cs` — Wave workspace (`c:\WSGTA\universal-or-strategy\src\PropTraderTools\`). No Director-workspace `.cs` paths detected.

---

#### 8. [Fact] Replacement Correctness

**Status: PASS**

Edit B specifies:
- **Find:** `[Fact(Skip = "obfuscation: AgileDotNetRT renames private members; cannot locate by string name")]`
- **Replace:** `[Fact]`

The replacement is a plain `[Fact]` attribute — not `[Fact(Skip = "")]` (empty string skip, which would still cause the test to be skipped by xUnit). This is the correct and intended change.

---

#### 9. Build Gate and Test-Pass Gate

**Status: PASS**

- **Build gate:** SCAN-06 — `dotnet build src/PropTraderTools/PropTraderTools.csproj` → `0 Error(s)` required.
- **Test gate:** SCAN-07 — `dotnet test ... --filter "FullyQualifiedName~GetSenderAccountName_ShouldBeReusedByOnTrailBeAccountUpdate"` → `PASSED` required.
- Both gates are explicit and sequenced (build before test implied by scan order).

---

#### 10. Contingency Path

**Status: PASS**

Ticket includes explicit Contingency section with three steps:
1. Revert Edit B and Edit C (restore `[Fact(Skip=...)]` and `GetMethod(...)`)
2. Keep Edit A (`GetStaticMethod` helper — correct and harmless)
3. Log new deferred item: `DW-REPAIRS-03-FALLBACK: GetSenderAccountName reflection fails with NonPublic|Static despite ObfuscationAttribute(Exclude=true). Investigate assembly load order.`

Contingency triggers when SCAN-07 status is `FAILED`. Engineer instruction is unambiguous: "If status is `FAILED`, do NOT close this ticket — execute the Contingency path above."

---

#### 11. Completeness (1 ticket for 3-edit atomic change)

**Status: PASS**

Exactly one ticket (T1) covers the three edits. Plan §1 Lane-Split Gate Result confirms: "SINGLE PIPELINE — Fix A and Fix B are one indivisible atomic unit." No additional tickets exist or are needed.

---

#### 12. DO NOT TOUCH List

**Status: PASS**

Ticket includes explicit DO NOT TOUCH table covering all five categories required by the review checklist:

| Item | Ticket line reference |
|------|-----------------------|
| `BwaveCycT1R1BeHelperTests` L6571 — `GetSenderAccountName_ShouldReturnEmpty_WhenSenderIsNull` | Present (minor L6570→L6571 variance vs plan; method name is authoritative and matches) |
| `BwaveCycT1R1BeHelperTests` L6580 — `GetSenderAccountName_ShouldReturnEmpty_WhenSenderIsNotAccount` | Present |
| `BwaveCycTaR2HelperTests` L6694–6695 — existing `GetMethod(string name)` | Present |
| All other `[Fact(Skip=...)]` tests | Present |
| All production `.cs` files | Present |

**Note (non-blocking):** Plan §9 references L6570; ticket references L6571. This is a comment-line vs attribute-line distinction for the same test method. Both documents agree on the full method name. The discrepancy is cosmetic and does not affect the engineer's work. The method name is the authoritative identifier.

---

#### 13. Scan File and Class References

**Status: PASS**

- SCAN-01 through SCAN-03 and SCAN-06: all reference `src/PropTraderTools/CopyEngineTests.cs` — correct file, correct Wave path.
- SCAN-04: names `GetStaticMethod` inside `BwaveCycTaR2HelperTests` — correct class.
- SCAN-05: lists identifiers introduced into `BwaveCycTaR2HelperTests` — correct class.
- SCAN-07: filter `FullyQualifiedName~GetSenderAccountName_ShouldBeReusedByOnTrailBeAccountUpdate` resolves within `BwaveCycTaR2HelperTests` — correct.

No scan references an incorrect file, class, or Director-workspace path.

---

## Summary Table

| Check | Status |
|-------|--------|
| 1. Traceability (DW-09-03) | PASS |
| 2. 7-scan checklist (all 7, commands, zero-to-pass) | PASS |
| 3. GetStaticMethod signature (NonPublic\|Static) | PASS |
| 4. xUnit test name match | PASS |
| 5. [Fact] replacement correct (not Skip="") | PASS |
| 6. Call site switch specified | PASS |
| 7. DO NOT TOUCH items listed | PASS |
| 8. JS rules (no lock, no throw, ASCII, CYC=1) | PASS |
| 9. Build gate + test-pass gate both specified | PASS |
| 10. Contingency path documented | PASS |
| 11. NT8 constraints (no prod .cs, no deploy-sync) | PASS |
| 12. Completeness (1 ticket, 3-edit atomic) | PASS |
| 13. Scan items reference correct file/class | PASS |

---

## Overall: TICKET_REVIEW_PASS

All 13 review checklist items pass. No JS rule violations. No NT8 constraint violations. No traceability gaps. No missing [Fact] coverage. All 7 scans present with exact commands and zero-to-pass criteria. Engineer contract and verifier anchor are intact.

**The engineer may proceed to Phase 4a.**
