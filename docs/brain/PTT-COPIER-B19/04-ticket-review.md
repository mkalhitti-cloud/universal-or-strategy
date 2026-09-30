# Ticket Review: PTT-COPIER-B19
**Reviewer**: ptt-ticket-reviewer
**Date**: 2026-07-13
**Phase**: 3.5 — pre-engineer gate
**Source tickets**: docs/brain/PTT-COPIER-B19/04-tickets.md
**Source plan**: docs/brain/PTT-COPIER-B19/02-architecture-plan.md (REVIEW_PASS, Cycle 2)
**Director spec**: DW-B19-COPIER-BUG-01 — `CopyEngine.cs` line 381 Account reference equality fix

---

## T1 — DW-B19-LIMIT-PRICE-01: Ask/Bid anchor fix for Trim/Flatten limit overloads

### 1. Traceability

**FAIL**

The ticket title and spec requirement ID are `DW-B19-LIMIT-PRICE-01`.
The Director spec for this block is **`DW-B19-COPIER-BUG-01`**.

These are two entirely different work items:

| Dimension | Director Spec (B19 Lane 1) | Ticket As Written |
|-----------|---------------------------|-------------------|
| Spec ID | DW-B19-COPIER-BUG-01 | DW-B19-LIMIT-PRICE-01 |
| File changed | `CopyEngine.cs` (1 line) | `CopyEngine.cs`, `TradeCopierPanel.cs`, `CopyEngineTests.cs` |
| Change | Line 381: `Account ==` → `Account.Name == ?.Name` | Trim/Flatten 3-arg → 4-arg + GetAsk/GetBid helpers |
| Tests | 2 [Fact]: Gate2_UsesAccountName, Gate2_NullMasterAccount | 10 [Fact]: B12 updates + new B19 limit-price tests |
| Priority | P0 — zero follower orders (live bug) | Not a P0 live bug |

The architecture plan (`02-architecture-plan.md`, REVIEW_PASS) defines scope as:
> Lane 1 only. Source file changed: `CopyEngine.cs`. Line changed: Line 381.
> Files NOT touched: `TradeCopierPanel.cs`, `TradeCopierWindow.cs`, `TradeCopierAddOn.cs`, `AtrSizingEngine.cs`.

The ticket touches `TradeCopierPanel.cs` — a file explicitly listed as NOT TOUCHED in the plan.

The architect submitted the wrong lane's ticket. The entire T1 content is phantom work relative
to the Director spec and the REVIEW_PASS architecture plan for this block.

**Violations:**
- T1 maps to DW-B19-LIMIT-PRICE-01 (Lane 2 work), not DW-B19-COPIER-BUG-01 (Lane 1, Director spec).
- `TradeCopierPanel.cs` is in-scope in the ticket but explicitly out-of-scope in the plan.
- The fix `e.Order.Account.Name == rule.MasterAccount?.Name` is absent from the ticket.
- The tests `Gate2_UsesAccountName_SourceContractVerified` and `Gate2_NullMasterAccount_NoCopyOrder` are absent from the ticket.
- `DW-B19-COPIER-BUG-01` does not appear anywhere in the ticket file.

---

### 2. Fix Completeness

**FAIL**

The required fix — change `e.Order.Account == rule.MasterAccount` to
`e.Order.Account.Name == rule.MasterAccount?.Name` at `CopyEngine.cs` line 381 — is
**not present anywhere in the ticket**.

The ticket instead describes a signature change to `Trim`/`Flatten` from 3-arg to 4-arg,
which is unrelated to the Account reference-equality bug.

---

### 3. JS Pre-Check

**CONDITIONAL — Issues noted; ticket is wrong lane so full JS scan is moot.**

Reviewing the ticket as written for pre-check completeness:
- **JS-021**: No `lock()` described. PASS on this item.
- **JS-001**: `catch (Exception ex)` used in `Trim`/`Flatten` bodies — this is a try/catch
  structure, not a `throw new XxxException` in a hot path. PASS on JS-001.
- **JS-002**: `GetAsk`/`GetBid` return `0.0`, not null. PASS on JS-002.
- **Async void**: No async void described. PASS.

However this analysis is irrelevant — the ticket is for the wrong spec entirely.
The correct fix (1 line, no new methods) has no JS risk surface at all.

---

### 4. CYC Pre-Check

**FAIL (wrong ticket)**

The Director spec states `OnOrderUpdate` CYC remains unchanged at 7 (no branches added or
removed — the fix changes a comparison sub-expression only). The ticket never mentions
`OnOrderUpdate` CYC. Instead it introduces `Trim 4-arg` (CYC=7) and `Flatten 4-arg` (CYC=7)
— these are not the methods under review for this spec.

---

### 5. NT8 Check

**FAIL (wrong ticket)**

The correct fix uses `Account.Name` (a string property confirmed valid in the plan §7 — used
in 10+ existing lines). The null-conditional `?.Name` is valid C# 6+ / .NET 4.8. Neither of
these items appears in the ticket. The ticket instead audits NT8-007, NT8-013, NT8-014,
NT8-029, NT8-032 — all of which pertain to the `CreateOrder` limit-price work that is out
of scope for B19 Lane 1.

---

### 6. Test Coverage — Test 1 (Gate2_UsesAccountName_SourceContractVerified)

**FAIL**

`Gate2_UsesAccountName_SourceContractVerified` is absent from the ticket.
The ticket test table contains `TrimLimit_Long_PlacesAboveAsk`,
`TrimLimit_Short_PlacesBelowBid`, `FlattenLimit_Long_PlacesAboveAsk`,
`FlattenLimit_Short_PlacesBelowBid`, `TrimLimit_FallsBackToMarket_WhenAskIsZero`, and
five B12 reflection-test updates. None of these are the required Gate2 tests.

---

### 7. Test Coverage — Test 2 (Gate2_NullMasterAccount_NoCopyOrder)

**FAIL**

`Gate2_NullMasterAccount_NoCopyOrder` is absent from the ticket for the same reason as above.

---

### 8. 7-Scan Checklist Presence

**CONDITIONAL PASS on form; FAIL on content**

The ticket does contain 7 labeled scan blocks (SCAN-01 through SCAN-07). On form alone they
are present. However:

- SCAN-01 checks for `lock()` — correct in general, but scoped incorrectly (checks all
  `.cs` files instead of the single changed file).
- SCAN-02 checks `async void` — correct in general.
- SCAN-03 checks `return null` — correct in general.
- **SCAN-04** checks for stale 3-arg call sites to `_engine.Trim/Flatten` — entirely
  irrelevant to DW-B19-COPIER-BUG-01.
- **SCAN-05** checks `GetRefPrice` removal — entirely irrelevant to DW-B19-COPIER-BUG-01.
- **SCAN-06** checks `.Ask`/`.Bid`/`.Last` without `.Price` — entirely irrelevant.
- **SCAN-07** checks for `PTT-TrimLimit`/`PTT-FlattenLimit` signal names — entirely
  irrelevant.

The scan checklist is populated with scans for the wrong ticket's work. The scans that MUST
be present for DW-B19-COPIER-BUG-01 (e.g., verify `Account ==` is gone, verify
`Account.Name == ?.Name` is present, verify Gate2 tests pass) are absent.

A scan checklist that checks the wrong work is functionally equivalent to a missing checklist:
it gives the engineer false confidence and the verifier a false anchor.

**Overall scan verdict: FAIL** (scans do not correspond to the spec being implemented).

---

### 9. File Scope

**FAIL**

Director spec: `CopyEngine.cs` (1 line) + `CopyEngineTests.cs` (2 tests). `TradeCopierPanel.cs`
explicitly excluded.

Ticket scope: `CopyEngine.cs`, `TradeCopierPanel.cs`, `CopyEngineTests.cs`. `TradeCopierPanel.cs`
inclusion directly contradicts the plan and the Director spec.

---

### 10. Scope Creep

**FAIL**

The ticket introduces: `GetAsk()`, `GetBid()`, removal of `GetRefPrice()`, 4-arg
`Trim`/`Flatten` overloads, updates to `OnTrimClick`, `OnFlattenClick`, `DispatchShortcut`,
and 10 total tests. None of this is in scope for B19 Lane 1 per the Director spec or the
REVIEW_PASS architecture plan.

This is a wholesale substitution of Lane 2 work for Lane 1 work, not minor scope creep.
AGENTS.md §11 (No Scope Creep Protocol): "ONE EPIC = ONE CONCERN."

---

### T1 Summary

| Check | Result | Blocking Reason |
|-------|--------|----------------|
| Traceability | **FAIL** | Ticket implements DW-B19-LIMIT-PRICE-01 (Lane 2), not DW-B19-COPIER-BUG-01 (Lane 1, Director spec). TradeCopierPanel.cs explicitly excluded by plan but included by ticket. |
| Fix Complete & Correct | **FAIL** | `e.Order.Account.Name == rule.MasterAccount?.Name` at line 381 is absent. |
| JS Pre-Check | PASS (on items present) | Not blocking on JS rules for what is written, but moot given wrong ticket. |
| CYC Pre-Check | **FAIL** | OnOrderUpdate CYC=7 (unchanged) never mentioned. Wrong methods analyzed. |
| NT8 Check | **FAIL** | NT8 items checked (007/013/014/029/032) are for wrong-lane work. |
| Test 1 (Gate2_UsesAccountName) | **FAIL** | Absent from ticket. |
| Test 2 (Gate2_NullMasterAccount) | **FAIL** | Absent from ticket. |
| 7-Scan Checklist | **FAIL** | Scans present in form, but SCAN-04/05/06/07 check wrong-lane work. No scans for the Account.Name fix or Gate2 tests. Verifier anchor is invalid. |
| File Scope | **FAIL** | TradeCopierPanel.cs included; plan and Director spec explicitly exclude it. |
| No Scope Creep | **FAIL** | Entire Lane 2 feature substituted for Lane 1 single-line bug fix. |

**VERDICT: TICKET_REVIEW_FAIL**

---

## Overall: TICKET_REVIEW_FAIL

**Root cause**: The architect submitted the ticket for `DW-B19-LIMIT-PRICE-01` (Lane 2 — Ask/Bid
anchor fix for Trim/Flatten) instead of `DW-B19-COPIER-BUG-01` (Lane 1 — Account reference
equality fix at `CopyEngine.cs:381`).

**Action required**: Architect must discard the current T1 and write a replacement ticket for
`DW-B19-COPIER-BUG-01` that satisfies:

1. **Spec ID**: DW-B19-COPIER-BUG-01
2. **Fix**: Change `e.Order.Account == rule.MasterAccount` → `e.Order.Account.Name == rule.MasterAccount?.Name` at `CopyEngine.cs` line 381
3. **Files**: `CopyEngine.cs` (1 line) + `CopyEngineTests.cs` (2 tests) ONLY
4. **Test 1**: `Gate2_UsesAccountName_SourceContractVerified` — reflection test verifying `Account.Name` is a public string property
5. **Test 2**: `Gate2_NullMasterAccount_NoCopyOrder` — null-safety test verifying `?.Name` prevents NullReferenceException
6. **CYC**: `OnOrderUpdate` CYC unchanged at 7 — confirm no branches added or removed
7. **7-Scan checklist** (SCAN-01 through SCAN-07): must cover Account `==` removal verification, `Account.Name` presence, lock() check, async void check, build, Gate2 test filter, full test suite
8. **File routing**: Wave workspace `c:\WSGTA\universal-or-strategy\src\PropTraderTools\`

The Lane 2 (`DW-B19-LIMIT-PRICE-01`) ticket may be submitted as a separate block (B19 Lane 2)
with its own Director approval. It must NOT be submitted as B19 Lane 1.
