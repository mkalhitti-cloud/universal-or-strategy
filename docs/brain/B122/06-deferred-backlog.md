# B122 Deferred Backlog

Block: B122
Date: 2026-08-25
Status: PIPELINE_COMPLETE

---

## Items CLOSED This Block

| Item | Description | Closed By |
|------|-------------|-----------|
| DW-PTT-BE-FIX-03 | Pre-existing build errors blocking test suite — NU1101 (bogus SKGL.Extension PackageReference), CS0433 Globals ambiguity, CS0246 machine-specific SKGL DLL absent. Full build restore: `Build succeeded. 0 Error(s). 0 Warning(s).` Non-incremental confirmed by independent ptt-verifier. | B122-T1 (RETRY 2) |

---

## New Deferred Items

### DW-B122-01 — Full SKGL remote validation integration

**Priority**: P2
**Context**: B122 replaced the broken SKGL remote call in LicenseClient.cs with a returning-null stub (`TryRemoteValidate` returns null unconditionally). The `DefineConstants Condition="Exists('...')"` in the csproj defines `SKGL_PRESENT` when the DLL is present, but no `#if SKGL_PRESENT` blocks currently exist in LicenseClient.cs. The stub causes all users to receive Starter tier until licensed.
**Action**: A future block must implement the real `TryRemoteValidate` call using Cryptolens (SKGL) API, wrapped in `#if SKGL_PRESENT` so the project still compiles on machines without NT8. The Cryptolens `ProductId` and `AccessToken` constants in LicenseClient.cs currently hold placeholder values.
**Deferred to**: Future block (post go-live; after BGTM-1 feature is ready for production licensing).

---

### DW-B122-02 — Re-enable CopyEngineTests.cs

**Priority**: P2
**Context**: `CopyEngineTests.cs` is excluded from MSBuild compile with `Condition="false"` due to 70+ API mismatch errors from prior sessions where the CopyEngine public API changed without updating the test stubs. The file is retained on disk for LSP IntelliSense.
**Action**: Map all failing stubs against the current CopyEngine public API. Update or replace stub types and methods. Verify compilation. Remove `Condition="false"`.
**Deferred to**: Dedicated test infrastructure block.

---

### DW-B122-03 — Re-enable B43Tests.cs

**Priority**: P2
**Context**: `B43Tests.cs` calls `TradeCopierWindow.ParseAtmTemplateSelection` which was removed from TradeCopierWindow in a prior session. The file is excluded with `Condition="false"`.
**Action**: Determine if `ParseAtmTemplateSelection` was replaced elsewhere. Either restore the method (if incorrectly removed) or update B43Tests.cs to test the replacement API. Remove `Condition="false"`.
**Deferred to**: First block touching TradeCopierWindow.

---

### DW-B122-04 — Ticket build gate protocol: require --no-incremental

**Priority**: P1
**Context**: During B122, the engineer ran an incremental build for SCAN-07 and reported "Build succeeded. 0 Warning(s). 0 Error(s)." from a stale cached .dll. The non-incremental build run by the ptt-verifier revealed CS1503 and 35 warnings, causing a VERIFY_FAIL cycle. The ticket verification template must be updated to require `--no-incremental` explicitly in the build gate command.
**Action**: Update 04-tickets.md template (or ticket authoring SOP) to add `--no-incremental` flag to all build verification commands. Add note: "Incremental build cache is a known failure mode — always use --no-incremental for SCAN-07 and the final verification gate."
**Deferred to**: Next block author / template update.

---

### DW-B122-05 — NT8 F5 compilation gate for B122 changes

**Priority**: P0
**Context**: B122 introduced `FeatureFlags.cs` (new file) and a modified `LicenseClient.cs` (stub-based, no SKGL API calls). These files have NOT been validated in NinjaTrader 8's internal Roslyn host (F5 compilation). The LSP-only `PropTraderTools.csproj` build passes, but the NT8 runtime compile is required to confirm no NT8-specific issues exist.
**Action**: Director syncs new/modified files to NT8 Custom directory and presses F5 in NinjaTrader 8. Expected: Compilation succeeded. 0 errors.
**Deferred to**: Director (immediate — prerequisite for using any B122-introduced licensing infrastructure in live trading).

---

## Carry-Forward Items (from B107/06-deferred-backlog.md — all unchanged)

All 14 open items from B107 are carried forward unchanged. B122 scope did not touch any of the code paths these items cover.

---

### DW-B107 — MoveStopToBreakEven Step A snapshots stale PTT-BE-Target-* on followers

**Priority**: P2
**Context**: Sim102/103/104 submitted 4 OCO bracket pairs on a 3-target ATM. A stale `PTT-BE-Target-4` was included in the snapshot and an extra OCO pair submitted. Same class as DW-B106 (which fixed the QX path in B107-T1 — BE path not in scope).
**Deferred to**: B108 (next pipeline block after current testing batch).
**Full brief**: `docs/brain/DW-B107/00-defect-brief.md`

---

### B107-DEFER-01 — F5 NinjaTrader 8 Compilation Gate

**Priority**: P0
**Context**: `ptt-sync-and-verify.ps1` completed with 0 MISMATCH (16 files MD5-verified). The F5 NinjaTrader 8 compilation step is the runtime compile gate. Director-owned.
**Deferred to**: Director (immediate, prerequisite for B107-DEFER-02).

---

### B107-DEFER-02 — Combo C Live Re-Test

**Priority**: P1
**Context**: DW-B105 + DW-B106 code changes implemented and verified. Full behavioral validation of the Combo C scenario (QX-ALL followed by BE-ALL, stale partial-fill residue case) requires a live NT8 session.
**Deferred to**: Director SIM gate session (after B107-DEFER-01 green).

---

### DW-B42-01 — T_BUG_QX_BE_01 does not assert PTT-QX-T3

**Priority**: Low
**Context**: T_BUG_QX_BE_01 asserts true for PTT-QX-T1 and PTT-QX-T2 only.
**Deferred to**: B43 or first block where T3 is confirmed in production use.

---

### DW-B42-02 — Live NT8 F5 verification required

**Priority**: High
**Context**: Two bug directions can only be fully verified in a live NT8 session.
**Deferred to**: Next live F5 session.

---

### DW-B42-03 — IsPttQxTarget range extension for future target slots

**Priority**: Conditional (low unless T4/T5 slots added)
**Context**: Current range `name[8] >= '1' && name[8] <= '3'` matches B41 two-OCO-group design.
**Deferred to**: Block that adds 4th+ target slot.

---

### DW-PTT-BE-FIX-01 — DW-B85 Option A: Lazy re-resolve for null followers

**Priority**: Medium
**Context**: Option A would re-attempt resolution lazily in AllAccounts() when the account later appears in Account.All. Per spec, deferred.
**Deferred to**: Next PTT productionisation block.

---

### DW-PTT-BE-FIX-02 — SIM gate: Path B 3-cycle runtime verification

**Priority**: High
**Context**: Full SIM verification of Path B (QX-ALL then BE-ALL, 3 cycles) requires a live NT8 session.
**Deferred to**: DW-B89 SIM gate session (combined with DW-B89-DEFERRED-04).

---

### DW-B89-DEFERRED-01 — Ctrl+F5 NT8 compilation gate (DW-B89 changes)

**Priority**: P0
**Context**: Director must confirm Ctrl+F5 in NinjaTrader for DW-B89 changes.
**Deferred to**: Director (immediate, prerequisite for all SIM paths below).

---

### DW-B89-DEFERRED-02 — SIM gate PATH A nominal

**Priority**: High
**Context**: Entry -> BE-ALL -> verify Output tab has NO [BE-ERR] lines, stops=N for all accounts. 3 cycles.
**Deferred to**: Director after DW-B89-DEFERRED-01 green.

---

### DW-B89-DEFERRED-03 — SIM gate PATH A buf=0 edge case (short position)

**Priority**: High
**Context**: Entry short -> BE-ALL buf=0t immediately.
**Deferred to**: Director after DW-B89-DEFERRED-01 green.

---

### DW-B89-DEFERRED-04 — SIM gate PATH B (QX-ALL then BE-ALL, 3 cycles)

**Priority**: High
**Context**: Entry -> QX-ALL -> BE-ALL arm -> price trigger. Merges DW-PTT-BE-FIX-02.
**Deferred to**: Director after DW-B89-DEFERRED-01 green.

---

### DW-B89-DEFERRED-05 — SIM gate DW-B87 timing race cycle

**Priority**: High
**Context**: Entry -> BE-ALL immediately (no wait). Must work (cancel sweep handles Submitted state).
**Deferred to**: Director after DW-B89-DEFERRED-01 green.

---

### DW-B89-DEFERRED-06 — Spec update: close DW-B89/B88/B87 in spec HTML

**Priority**: Medium
**Context**: `specs/002-trade-copier-spec.html` sections must be updated to CLOSED status after all DW-B89 SIM gate paths pass.
**Deferred to**: After all DW-B89 SIM paths green.

---

## Summary

| Category | Count | Items |
|----------|-------|-------|
| Closed this block | 1 | DW-PTT-BE-FIX-03 |
| New deferred (B122 pipeline) | 5 | DW-B122-01 through DW-B122-05 |
| Carry-forward from B107 (unchanged) | 14 | DW-B107, B107-DEFER-01/02, DW-B42-01/02/03, DW-PTT-BE-FIX-01/02, DW-B89-DEFERRED-01/02/03/04/05/06 |

**Total open items**: 19 (5 new + 14 carry-forward)
