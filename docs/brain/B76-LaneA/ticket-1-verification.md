# B76-LaneA Ticket-1 Verification
**Status**: VERIFY_PASS
**Ticket**: TICKET-B76-1 -- FlattenOneAccount: in-flight guard + race guard (tests only)
**Verifier**: ptt-verifier (Phase 4b)
**Date**: 2026-08-18
**Engineer completion report**: docs/brain/B76-LaneA/ticket-1-completion.md (BUILD_PASS)

---

## Layer 3 Independent Verification

### Code Verification (CopyEngine.cs FlattenOneAccount, lines 1878-1932)

Read source directly and confirmed independently:

| Claim | Location | Verified |
|-------|----------|----------|
| `foreach (var o in acc.Orders.ToList())` in-flight loop | line 1888 | PASS |
| `o.Name != "PTT-Flatten"` guard | line 1890 | PASS |
| `o.Instrument?.FullName != instrument.FullName` guard | line 1891 | PASS |
| `OrderState.Submitted || Accepted || Working` tri-state | lines 1892-1894 | PASS |
| `StatusUpdate?.Invoke(acc.Name + ": flat-guard: in-flight skip")` | line 1896 | PASS |
| `return;` immediately after StatusUpdate | line 1897 | PASS |
| First `FindPosition(acc, instrument)` pre-cancel call | line 1900 | PASS |
| `CancelAllAccountOrders(acc, instrument)` | line 1906 | PASS |
| `var posAfterCancel = FindPosition(acc, instrument)` post-cancel | line 1909 | PASS |
| `posAfterCancel == null \|\| posAfterCancel.Quantity == 0` guard | line 1910 | PASS |
| `StatusUpdate?.Invoke(...)` "flat-race skip (pos cleared by bracket fill)" | line 1912 | PASS |
| `posAfterCancel.Quantity` and `posAfterCancel.MarketPosition` in CreateOrder | lines 1915-1922 | PASS |
| Header comment `CYC=6` | line 1874 | PASS |

Local variables counted in body: `o` (foreach), `pos`, `posAfterCancel`, `action`, `order` = **5** >= 5. PASS.

HOTFIX-B76-FLATTEN-GUARD-01 v2 and HOTFIX-B76-FLATTEN-RACE-01 both verified in source.

### Test Verification (B76Tests.cs T_B76_01..T_B76_06)

Independently confirmed all 6 tests present and match ticket spec:

| Test | Assertion | Ticket Spec Match | Code Present |
|------|-----------|-------------------|--------------|
| T_B76_01 | private instance (Account,Instrument)->void | YES | PASS |
| T_B76_02 | ldstr scan: "flat-guard: in-flight skip" | YES | PASS |
| T_B76_03 | ldstr scan: "flat-race skip" | YES | PASS |
| T_B76_04 | >= 2 FindPosition call tokens via 0x28/0x6F opcodes | YES | PASS |
| T_B76_05 | CancelAllAccountOrders offset < second FindPosition offset | YES | PASS |
| T_B76_06 | LocalVariables.Count >= 5 | YES | PASS |

All tests use established IL-scan pattern (opcode 0x72 ldstr, 0x28/0x6F call/callvirt). No NUnit or
MSTest. xUnit [Fact] only. ASCII identifiers. JS-021 compliant (no lock in test file).

### 7-Scan Cross-Check (Layer 3 independent re-run)

| Scan | Pattern | Files | Result |
|------|---------|-------|--------|
| SCAN-01 | `^\s*lock\s*\(` | B76Tests.cs, CopyEngine.cs, TradeCopierPanel.cs, TradeCopierAddOn.cs, TradeCopierWindow.cs | 0 hits PASS |
| SCAN-02 | `async\s+void\s+\w+\(` | all 5 files | 0 hits PASS |
| SCAN-03 | `throw\s+new\s+\w+Exception\(` | all 5 files | 1 pre-existing hit TradeCopierWindow.cs:638 (ConvertBack one-way converter stub, not in B76 scope) PASS |
| SCAN-04 | `return\s+null\s*;` in B76Tests.cs | B76Tests.cs | 0 hits PASS |
| SCAN-04b | `return\s+null\s*;` in TradeCopierPanel.cs B76 diff area (lines 2221-2249) | 0 hits PASS |
| SCAN-05 | Non-ASCII in B76 diff areas | B76Tests.cs + changed regions | 0 hits PASS (pre-existing non-ASCII in comments elsewhere, not in B76 diff) |
| SCAN-06 | `DateTime\.Now[^U]` | all 5 files | 0 hits PASS |
| SCAN-07 | NUnit/MSTest | B76Tests.cs | 0 hits PASS (comment reference only, no import) |

### sync-ptt-to-nt8.ps1

`Copied: 0  Skipped (in sync): 15` -- all NT8 hard links current. PASS.

---

## Verdict

**VERIFY_PASS**

All claims in ticket-1-completion.md independently confirmed:
- HOTFIX-B76-FLATTEN-GUARD-01 v2 and HOTFIX-B76-FLATTEN-RACE-01 are present and correct in source.
- T_B76_01..T_B76_06 are present, correct, and match the ticket specification.
- 7 scans: zero new violations (pre-existing items pre-date B76).
- CYC=6 (<=8). JS-021 compliant throughout.
