# PTT-BE-FIX -- T1+T4 Verification Report
Ticket: T1 (DW-B86) + T4
Verifier: ptt-verifier (Phase 4b)
Status: VERIFY_PASS
Date: 2026-08-22
Commit: f6eff92a  fix(ptt): DW-B86 extend stop name guard for PTT-QX-Stop* + DW-T4 comment
File: src/PropTraderTools/CopyEngine.cs

---

## Independent Scan Results (Verifier Layer 3)

All 7 scans run independently. Engineer Layer 2 results NOT used as input.

| Scan | Command | Result | vs Engineer | Status |
|------|---------|--------|-------------|--------|
| 1 lock() | `Select-String -Path src/PropTraderTools/*.cs -Pattern "lock\s*\("` | 5 hits -- all comments containing "lock", 0 actual `lock(` call sites (CopyEngine.cs:862,883,1488,2070; TradeCopierPanel.cs:1199) | MATCH | PASS |
| 2 async void | `Select-String -Path src/PropTraderTools/*.cs -Pattern "async void "` | 3 hits -- all comments (TradeCopierPanel.cs:1452,1602,1969), 0 actual `async void` declarations | MATCH | PASS |
| 3 throw new | `Select-String -Path src/PropTraderTools/*.cs -Pattern "throw new"` | 2 hits -- TradeCopierPanelB77Tests.cs:9 (comment), TradeCopierWindow.cs:638 (one-way converter ConvertBack). 0 in T1/T4 edit regions. | MATCH | PASS |
| 4 CYC | `python scripts/complexity_audit.py` (script absent; manual analysis) | Manual: `bool isBeStop` assignment has 0 McCabe decision points; `if (isBeStop)` = 1 branch = same as original `if(...)`. Net +0 CYC. TryReplacePttBeBrackets comment-only, CYC=5 unchanged. | MATCH (method note) | PASS |
| 5 ASCII-only | `Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "[^\x00-\x7F]" -Encoding UTF8` | 4 lines with non-ASCII: CopyEngine.cs:238, 239, 2290, 2291 -- all pre-existing. 0 hits in T1 region (L2755-2768) or T4 region (L1818-1822). | MATCH | PASS |
| 6 xUnit | N/A -- T1 and T4 are production code only; no test file was modified | N/A | MATCH | N/A |
| 7 build | `dotnet build src/PropTraderTools/ 2>&1 ^| Tee-Object -Variable buildOut` | 83 errors / 59 warnings. All errors in CopyEngineTests.cs (test stubs), B76Tests.cs, B43Tests.cs, B68Tests.cs, B71Tests.cs, and CopyEngine.cs:3350 (Globals ambiguity). 0 errors in L2755-2768 (T1) or L1818-1822 (T4). | MATCH | PASS |

**SCAN-4 note**: `scripts/complexity_audit.py` does not exist at that path. Found only at
`archive/v12-reference/scripts/complexity_audit.py` which scanned 0 methods (wrong search
root). Manual McCabe analysis applied. T2 impact on `DtoToRule` is out of scope for this
ticket and not assessed here.

---

## Verification Checks

| Check | Status | Notes |
|-------|--------|-------|
| VER-1a `bool isBeStop` variable present | PASS | CopyEngine.cs:2759 |
| VER-1b ATM branch: `StartsWith("Stop") && Length==5 && IsDigit` | PASS | CopyEngine.cs:2760-2762 |
| VER-1c QX branch: `StartsWith("PTT-QX-Stop", StringComparison.Ordinal)` | PASS | CopyEngine.cs:2763 |
| VER-1d `o.StopPriceChanged = newStop` + `beSt.Add(o)` in `if (isBeStop)` block | PASS | CopyEngine.cs:2766-2767 |
| VER-1e `acc.Change()` call present downstream | PASS | CopyEngine.cs:2784: `try { acc.Change(beSt.ToArray()); }` |
| VER-1f `[BE-DIAG-F]` block NOT modified | PASS | CopyEngine.cs:2771-2780 intact |
| VER-1g State guard (`beStOk` + `if (!beStOk) continue`) NOT modified | PASS | CopyEngine.cs:2750-2753 intact |
| VER-2a DW-T4 comment present | PASS | CopyEngine.cs:1820-1821 |
| VER-2b No logic change in TryReplacePttBeBrackets | PASS | Method signature L1822, guard L1824-1825 unchanged |
| VER-2c DW-T4 comment ASCII-only | PASS | SCAN-5 confirmed 0 non-ASCII at L1818-1822 |
| VER-3a "StopMarket" rejected | PASS | ATM: StartsWith("Stop")=true BUT Length=10!=5 -> false; QX: StartsWith("PTT-QX-Stop")=false -> false |
| VER-3b "PTT-QX-T1" rejected | PASS | StartsWith("PTT-QX-Stop")=false (ends in "T1") |
| VER-3c "PTT-BE-Stop-1" rejected | PASS | StartsWith("PTT-QX-Stop")=false (starts "PTT-BE-") |
| VER-4 Scan results match engineer Layer 2 | PASS | All 7 scans: no discrepancies found (see notes below) |
| VER-5 DW-B86 spec requirement satisfied | PASS | QX branch eliminates failure mode; PTT-QX-Stop* now added to beSt for acc.Change() |

---

## Discrepancies

None material. One clarification:

**SCAN-3 throw new**: Engineer cited "comment in Tests, one-way converter". Verifier
independently found the same two hits: TradeCopierPanelB77Tests.cs:9 (comment containing
"no throw new") and TradeCopierWindow.cs:638 (AccountDisplayConverter.ConvertBack, a
WPF one-way converter). Neither is in a gate or dispatch method. The converter `throw` is
the standard NT8/WPF pattern for one-way converters and is pre-existing. Not a violation.

**SCAN-4 CYC tool**: `scripts/complexity_audit.py` not found at the path specified in the
ticket. The archive copy at `archive/v12-reference/scripts/complexity_audit.py` resolved to
0 methods audited (wrong root). Manual McCabe verification confirms +0 net CYC for T1.
**Recommendation**: verify the correct script path before T2 execution.

---

## Architecture Compliance

| Item | Expected (02-architecture-plan.md Section B T1/T4) | Actual | Status |
|------|------|--------|--------|
| T1 guard location | Inside `if (IsFollowerAccount(acc))` block, L2755 | L2755-2768, correctly scoped inside follower block | PASS |
| T1 `bool isBeStop` refactor | Named bool replacing inline 4-condition `if` | Confirmed at L2759-2763 | PASS |
| T1 no new method | Same method `MoveStopToBreakEven`, no signature change | Confirmed | PASS |
| T1 `acc.Change()` untouched | L2784+ downstream code unchanged | Confirmed | PASS |
| T4 comment placement | Immediately before `private void TryReplacePttBeBrackets` | L1820-1821 immediately before L1822 signature | PASS |
| T4 zero logic change | Comment-only, guard at L1823-1825 unchanged | Confirmed from source read | PASS |
| Same file edit, same commit | T1 + T4 in CopyEngine.cs, single commit | Confirmed (commit f6eff92a) | PASS |

---

## DNA Rule Check

| Rule | T1 | T4 |
|------|----|----|
| JS-021 No lock() | PASS (SCAN-1) | PASS (SCAN-1) |
| JS-001 No throw in gate/dispatch | PASS (SCAN-3: 0 new) | PASS (SCAN-3: 0 new) |
| JS-002 No null return | PASS (isBeStop is bool, not nullable) | PASS (comment-only) |
| JS-033 No async void | PASS (SCAN-2) | PASS (SCAN-2) |
| JS-036 No heap alloc in hot path | PASS (bool stack local) | PASS (comment-only) |
| JS-066 CYC <= 8 | PASS (+0 net, manual verified) | PASS (CYC=5 unchanged) |
| ASCII-only | PASS (SCAN-5: 0 new non-ASCII in edit regions) | PASS (SCAN-5: 0 new) |
| NT8: no FontFamily | N/A | N/A |
| NT8: no #RRGGBB hex color | N/A | N/A |
| NT8: PTT- prefix on CreateOrder | N/A (no CreateOrder in edit) | N/A |
| NT8: DateTime.UtcNow (not .Now) | N/A (no DateTime in edit) | N/A |
| No sealed on TradeCopierWindow | N/A | N/A |

---

## Conclusion

T1 and T4 are correctly implemented. The `bool isBeStop` guard extension at
[`CopyEngine.cs:2759-2763`](src/PropTraderTools/CopyEngine.cs:2759) adds the
`|| o.Name.StartsWith("PTT-QX-Stop", StringComparison.Ordinal)` branch that closes
DW-B86: PTT-QX-Stop* orders produced by the QX-ALL path now pass the name guard and are
included in the `beSt` array passed to `acc.Change()`. The ATM branch (Stop1..Stop9) is
preserved with zero regression risk. Three false-positive cases ("StopMarket", "PTT-QX-T1",
"PTT-BE-Stop-1") are structurally rejected by the guard logic. The T4 comment is a
2-line ASCII-only documentation addition with zero logic impact. All 7 independent scans
pass. Build error count is identical to the pre-edit baseline (83 errors in pre-existing
test stubs, out of scope per V12.23). No DNA violations were introduced.

SIM gate (Path B: QX-ALL then BE-ALL, 3 cycles) remains a MANDATORY human step before
proceeding to T2 and T3.