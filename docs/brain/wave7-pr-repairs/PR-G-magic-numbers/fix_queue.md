# PR-G Fix Queue -- Magic Numbers / Named Constants
# Branch: repairs/magic-numbers (create fresh from main)
# Cluster: S7 Kernel Infrastructure, S3 UI
# OKF Rules: 6 (JS-100 magic numbers), 12 (named constants for domain values)
# Priority: P3 -- readability/maintainability debt, zero production risk

---

## OVERVIEW

JS-100 (Magic Numbers) was the #1 violation category in the 2026-06-03 Jane Street
baseline audit (222 violations across 52 files). This PR targets the HIGH-RISK subset:
numeric literals that represent domain-specific trading parameters where a wrong value
has production consequences. It does NOT attempt to fix every magic number (that is
impractical at 222+ instances) -- only those where naming the constant adds real safety.

LOW-RISK magic numbers (UI pixel values, array indices, trivial 0/1/2) are intentionally
excluded from this PR.

---

## FINDING G-B6-1 -- Trading constants in Properties.cs

**File**: src/V12_002.Properties.cs
**Issue**: Numeric literals used as NinjaTrader property defaults and bounds
  (e.g. ATR period, risk percentage, tick sizes, max contracts).
  These are the values traders configure -- wrong default = wrong risk.
**Fix**: Extract to private const or static readonly at top of partial class.
  Naming convention: SCREAMING_SNAKE_CASE for const, PascalCase for readonly.
  Examples:
    `[Range(1, 999)]` bounds -> `private const int MinPeriod = 1; private const int MaxPeriod = 999;`
    Default ATR period -> `private const int DefaultAtrPeriod = 14;`
  Read the file first. Only extract literals that appear >1 time OR represent
  a domain concept (not trivial bounds like 0, 1, 100).
**OKF Rule 6**: named constants for domain values.

---

## FINDING G-B6-2 -- Trading constants in LogicAudit.cs

**File**: src/V12_002.LogicAudit.cs
**Issue**: Numeric literals in audit case thresholds (contract counts,
  price offsets, percentage checks). These literals define what is "correct"
  behavior in the audit -- unnamed literals make it impossible to know if
  the right value is being checked.
**Fix**: Extract domain-meaningful literals to named const at top of class.
  Trivial values (0, 1, 2) may be left as-is.
**OKF Rule 6**: named constants.

---

## FINDING G-B6-3 -- Buffer/channel sizes in UI.IPC.Server.cs

**File**: src/V12_002.UI.IPC.Server.cs
**Issue**: Buffer sizes, port numbers, timeout values as bare integers.
  Wrong buffer size = data corruption; wrong timeout = silent hang.
**Fix**: Extract to named const. Examples:
  buffer size -> `private const int IpcBufferSize = 4096;`
  timeout ms -> `private const int IpcTimeoutMs = 5000;`
**OKF Rule 6**: named constants for infrastructure parameters.

---

## FINDING G-B6-4 -- RGB color constants in UI.Panel.Brushes.cs

**File**: src/V12_002.UI.Panel.Brushes.cs
**Issue**: 38 violations -- RGB integer literals used directly in Brush
  construction. Not a production safety issue, but makes the color palette
  completely opaque.
**Fix**: Extract to named static readonly Color fields.
  Group by semantic role: ProfitColor, LossColor, WarningColor, etc.
  Run `dotnet csharpier format src/V12_002.UI.Panel.Brushes.cs` after changes.
**OKF Rule 6**: JS-100 magic numbers.
**Note**: This is the lowest-priority item in this PR. Skip if diff size
  would cause the PR to exceed gate limits.

---

## Commit order recommendation

1. fix(repairs/pr-g): extract trading constants in Properties.cs
2. fix(repairs/pr-g): extract audit thresholds in LogicAudit.cs
3. fix(repairs/pr-g): extract IPC buffer/timeout constants in IPC.Server.cs
4. fix(repairs/pr-g): extract RGB color constants in UI.Panel.Brushes.cs (if size permits)

---

## Gate Requirements

- [ ] dotnet build Linting.csproj -- 0 errors
- [ ] python scripts/wave7_prepush_gate.py --base origin/main -- GATE PASSED
- [ ] dotnet csharpier check src/ -- 0 issues
- [ ] No lock() introduced
- [ ] All modified methods CYC <= 8 (const extraction does not affect CYC)
- [ ] No behavioral change -- all const values must equal the original literals exactly

## PR title
"fix(repairs): named constants for trading parameters, IPC buffers, UI colors (JS-100)"
