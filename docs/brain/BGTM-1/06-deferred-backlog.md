# BGTM-1 Deferred Backlog

**Block**: BGTM-1 (License Gating + Feature Flags)
**Reviewer**: ptt-plan-reviewer
**Date**: 2026-08-28
**Status**: PIPELINE_COMPLETE (coding phases)

---

## New Deferred Items — BGTM-1 Block

### BGTM-1-DEFER-01 — F5 NinjaTrader 8 Compilation Gate

**Priority**: P0 — prerequisite for SIM gate and go-live
**Context**: `ptt-sync-and-verify.ps1` must complete with 0 MISMATCH. The F5 NinjaTrader 8
compilation step is the runtime compile gate. It must produce "Compilation succeeded" (zero errors)
after sync. New files: `LicenseClient.cs` (with `IsExternalInit` shim + `sealed record FeatureFlags`),
`csproj` with `LangVersion 9.0` and `SKGL.Extension` reference. All changes require F5 green
before any live or SIM validation of BGTM-1 feature gating.
**Action**: Director presses F5 in NinjaTrader 8 after confirming sync pass.
**Deferred to**: Director (immediate, prerequisite for BGTM-1-DEFER-04).

---

### BGTM-1-DEFER-02 — Cryptolens Dashboard Setup

**Priority**: P0 — required before live license activation
**Context**: `LicenseClient.TryRemoteValidate` calls `SKM.V3.Methods.Key.Activate(...)` using
constants that are currently placeholders: `CRYPTOLENS_ACCESS_TOKEN_PLACEHOLDER` (access token)
and product ID `1234`. These must be replaced with real Cryptolens account values before any
customer can activate a Pro or Elite license remotely. Without this change, `TryRemoteValidate`
will always return null and users will fall back to cache or `Starter()`.
**Action**: Director/product owner:
  1. Creates Cryptolens (https://cryptolens.io) account and product.
  2. Sets up feature data objects: `multi_rule`, `trim_flatten`, `break_even`, `atr_sizing`,
     `click_trader`, `mirror_mode`, `qx_global_exit` on license keys.
  3. Replaces `CRYPTOLENS_ACCESS_TOKEN_PLACEHOLDER` with real access token.
  4. Replaces product ID `1234` with real product ID.
  5. Re-syncs and F5.
**Deferred to**: Director / product owner (before first customer activation).

---

### BGTM-1-DEFER-03 — SKM.NET.Standard DLL Physical Deployment to NT8 bin/Custom/

**Priority**: P1 — required for remote validation at runtime
**Context**: `PropTraderTools.csproj` adds:
  ```xml
  <Reference Include="SKGL.Extension">
    <HintPath>$(USERPROFILE)\Documents\NinjaTrader 8\bin\Custom\SKGL.Extension.dll</HintPath>
    <Private>false</Private>
  </Reference>
  ```
  The `PackageReference` for OmniSharp IntelliSense resolves the type at compile time but NT8's
  internal Roslyn host uses the HintPath DLL. If the DLL is absent from `bin\Custom\`, the NT8
  compile will fail with a missing assembly error, and `TryRemoteValidate` cannot execute.
**Action**: Director:
  1. Downloads `SKGL.Extension` NuGet package (version 2.0.23 or compatible).
  2. Extracts the `net46` or `net48` flavor DLL (`SKGL.Extension.dll`).
  3. Copies to `%USERPROFILE%\Documents\NinjaTrader 8\bin\Custom\`.
  4. Confirms F5 produces 0 errors (see BGTM-1-DEFER-01).
**Deferred to**: Director / environment setup (before BGTM-1-DEFER-01).

---

### BGTM-1-DEFER-04 — BgtmTests.cs Full Execution in xUnit Test Runner

**Priority**: P1 — required for full test suite confidence
**Context**: 11 `[Fact]` methods in `src/PropTraderTools/Tests/BgtmTests.cs` are implemented
and verified by Layer 3 independent scan. The test infrastructure uses `LicenseClient._testCachePath`
injection (an `internal static string` field) to avoid `NinjaTrader.Core.Globals.UserDataDir`
in tests. However, the test project targeting (net48 vs net8.0), xUnit framework availability,
and any residual NT8-stub dependencies must be confirmed in an actual `dotnet test` run.
The pre-existing 83 test build errors in `CopyEngineTests.cs` stub infrastructure (DW-PTT-BE-FIX-03)
may affect the test runner harness.
**Action**: Director / CI gate:
  1. After BGTM-1-DEFER-01 green, run `dotnet test src/PropTraderTools/ --filter "BgtmTests"`.
  2. Confirm all 11 `[Fact]` methods pass.
  3. If test runner fails due to DW-PTT-BE-FIX-03 infrastructure errors, resolve that item first.
**Deferred to**: Director / CI gate (after BGTM-1-DEFER-01 green).

---

## Carry-Forward Items from B107 (unchanged)

All items below are copied from `docs/brain/B107/06-deferred-backlog.md`.
BGTM-1 changes do not affect any of these items.

---

### DW-B107 — MoveStopToBreakEven Step A snapshots stale PTT-BE-Target-* on followers

**Priority**: P2 — correctness violation, functionally benign in observed test
**Discovered**: 2026-08-25 live BE-ALL test (stopped out, Copier ON, 4 accounts)
**Context**: Sim102/103/104 each submitted 4 OCO bracket pairs on a 3-target ATM. Sim101
(leader) correct at 3. `MoveStopToBreakEven` Step A (~L3380) collects target orders with no
native-vs-PTT discrimination and no count cap. A stale `PTT-BE-Target-4` was included and an
extra OCO pair submitted.
**Deferred to**: B108 (next pipeline block after current testing batch).
**Full brief**: `docs/brain/DW-B107/00-defect-brief.md`

---

### B107-DEFER-01 — F5 NinjaTrader 8 Compilation Gate (B107 changes)

**Priority**: P0 — prerequisite for SIM gate and go-live
**Context**: `ptt-sync-and-verify.ps1` completed with 0 MISMATCH (16 files MD5-verified).
Director must press F5 in NinjaTrader 8 to confirm compilation succeeded for B107 changes.
**Deferred to**: Director (immediate, prerequisite for B107-DEFER-02).

---

### B107-DEFER-02 — Combo C Live Re-Test

**Priority**: P1 — required before next live trading session involving BE-ALL then QX-ALL
**Context**: DW-B105 + DW-B106 code changes verified. Full Combo C scenario (QX-ALL followed
by BE-ALL, stale partial-fill residue case) requires a live NT8 session.
**Test sequence**: Enter position → BE-ALL → QX-ALL → confirm zero [BE-DIAG] lines, exactly 3
PTT-QX-T* brackets, no T4, all 4 accounts covered, no naked positions.
**Deferred to**: Director SIM gate session (after B107-DEFER-01 green).

---

### DW-B42-01 — T_BUG_QX_BE_01 does not assert PTT-QX-T3

**Priority**: Low
**Fix**: Add `Assert.True(IsPttQxTargetInline("PTT-QX-T3"))` to T_BUG_QX_BE_01.
**Deferred to**: B43 or first block where T3 is confirmed in production use.

---

### DW-B42-02 — Live NT8 F5 verification required

**Priority**: High — required before next live trading session
**Context**: Direction 1 (QX→BE) and Direction 2 (BE→QX) both require live NT8 session confirm.
**Deferred to**: Next live F5 session.

---

### DW-B42-03 — IsPttQxTarget range extension for future target slots

**Priority**: Conditional (low unless T4/T5 slots added)
**Deferred to**: Block that adds 4th+ target slot.

---

### DW-PTT-BE-FIX-01 — Lazy re-resolve for null followers (Option A)

**Priority**: Medium
**Context**: When a follower account is not in Account.All at LoadRules() time, Option A would
re-attempt resolution lazily. Per spec, Option A is deferred.
**Deferred to**: Next PTT productionisation block.

---

### DW-PTT-BE-FIX-02 — SIM gate: Path B 3-cycle runtime verification

**Priority**: High — required before next live trading session with QX-ALL then BE-ALL sequence
**Deferred to**: DW-B89 SIM gate session (combined with DW-B89-DEFERRED-04).

---

### DW-PTT-BE-FIX-03 — Pre-existing test build errors (83 errors + CS0433 Globals ambiguity)

**Priority**: High — blocks full test suite build
**Context**: Pre-existing errors in CopyEngineTests.cs stub infrastructure (83 errors) plus
CS0433 Globals ambiguity at CopyEngine.cs:L3350. Unrelated to B107 or BGTM-1.
**Deferred to**: Dedicated test infrastructure remediation block.

---

### DW-B89-DEFERRED-01 — Ctrl+F5 NT8 compilation gate (DW-B89 changes)

**Priority**: P0 — blocks DW-B89 SIM gate
**Deferred to**: Director (immediate, prerequisite for all SIM paths below).

---

### DW-B89-DEFERRED-02 — SIM gate PATH A nominal

**Priority**: High
**Context**: Entry → BE-ALL → verify no [BE-ERR] lines, stops=N for all accounts. 3 cycles.
**Deferred to**: Director after DW-B89-DEFERRED-01 green.

---

### DW-B89-DEFERRED-03 — SIM gate PATH A buf=0 edge case (short position)

**Priority**: High
**Context**: Entry short → BE-ALL buf=0t immediately. 1 cycle.
**Deferred to**: Director after DW-B89-DEFERRED-01 green.

---

### DW-B89-DEFERRED-04 — SIM gate PATH B (QX-ALL then BE-ALL, 3 cycles)

**Priority**: High
**Merges**: DW-PTT-BE-FIX-02 (Path B 3-cycle verification).
**Deferred to**: Director after DW-B89-DEFERRED-01 green.

---

### DW-B89-DEFERRED-05 — SIM gate DW-B87 timing race cycle

**Priority**: High
**Context**: Entry → BE-ALL immediately (no wait).
**Deferred to**: Director after DW-B89-DEFERRED-01 green.

---

### DW-B89-DEFERRED-06 — Spec update: close DW-B89/B88/B87 in spec HTML

**Priority**: Medium
**Action**: Director updates spec after full SIM gate PASS.
**Deferred to**: After all DW-B89 SIM paths green.

---

## Summary

| Category | Count | Items |
|----------|-------|-------|
| New deferred — BGTM-1 | 4 | BGTM-1-DEFER-01 through 04 |
| Carry-forward — DW-B107 post-pipeline | 1 | DW-B107 |
| Carry-forward — B107 pipeline | 2 | B107-DEFER-01, B107-DEFER-02 |
| Carry-forward — DW-B89/B42/PTT-BE-FIX | 11 | DW-B42-01/02/03, DW-PTT-BE-FIX-01/02/03, DW-B89-DEFERRED-01/02/03/04/05/06 |

**Total open items**: 18 (4 new + 14 carry-forward)
