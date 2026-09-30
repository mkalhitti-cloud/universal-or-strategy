# PR-F Fix Queue -- Propagation + Null returns + StringComparison bulk
# Branch: repairs/propagation-bulk (create fresh from main)
# Cluster: S2 Execution Engine (Propagation), bulk style sweep
# OKF Rules: 5 (Account.All), 6 (SA1204, SA1503, StringComparison), 12 (null returns)

---

## FINDING F-A2-1 -- Account.All bare .FirstOrDefault in Orders.Callbacks.Propagation.cs

**File**: src/V12_002.Orders.Callbacks.Propagation.cs
**Line**: 741
**Issue**: `Account acct = Account.All.FirstOrDefault(a => ...)` -- LINQ lazy
  enumeration over live broker collection without snapshot.
**Fix**: `Account acct = Account.All.ToArray().FirstOrDefault(a => ...)`
**OKF Rule 5**: independent_tracking.

---

## FINDING F-B2-1 -- SA1204 static member ordering in IPC.Hardening.cs

**File**: src/V12_002.IPC.Hardening.cs
**Lines**: 325, 339
**DD entry**: DD-004
**Issue**: `private static readonly SqlInjectionPatterns` and
  `PathTraversalPatterns` appear after instance methods (lines 174-318).
  SA1204: static members should precede non-static.
**Fix**: Move both static readonly fields to immediately after the class-level
  field declarations, before the first instance method. Pure reorder, no logic.
**OKF Rule 6**: SA1204.

---

## FINDING F-B3 -- SA1503 missing braces bulk (remaining Entries files)

**Files**: src/V12_002.Entries.Retest.cs (16), src/V12_002.Entries.Trend.cs (10),
           src/V12_002.Entries.MOMO.cs (8)
**Issue**: Single-line if bodies without braces.
**Fix**: `dotnet csharpier format src/V12_002.Entries.Retest.cs`
         `dotnet csharpier format src/V12_002.Entries.Trend.cs`
         `dotnet csharpier format src/V12_002.Entries.MOMO.cs`
  Run CSharpier per-file to add braces. Verify diff shows only brace additions.
**OKF Rule 12**: SA1503.

---

## FINDING F-B4 -- StringComparison.Ordinal bulk sweep (remaining high-count files)

**Files** (by descending count -- do in this order):
  - src/V12_002.UI.Panel.Handlers.cs (119 instances)
  - src/V12_002.Orders.Callbacks.AccountOrders.cs (89 instances)
  - src/V12_002.SIMA.Lifecycle.cs (81 instances)
  - src/V12_002.REAPER.Audit.cs (74 instances)
  - src/V12_002.UI.Callbacks.cs (65 instances)
  - src/V12_002.Orders.Callbacks.cs (65 instances)
  - src/V12_002.UI.Panel.Helpers.cs (63 instances)
  - src/V12_002.UI.Panel.StateSync.cs (61 instances)
  - src/V12_002.Orders.Management.StopSync.cs (61 instances)
  - src/V12_002.SIMA.Dispatch.cs (61 instances)
  - src/V12_002.Orders.Management.Cleanup.cs (59 instances)
  - src/V12_002.Orders.Callbacks.Propagation.cs (54 instances)
  - src/V12_002.Orders.Management.Flatten.cs (50 instances)
  (plus remaining files with fewer instances)

**Issue**: `.StartsWith(...)`, `.EndsWith(...)`, `.Contains(...)`, `.IndexOf(...)`
  on string literals without `StringComparison.Ordinal`.
**Fix approach**:
  1. Try Roslyn bulk fix first: `dotnet format --diagnostics CA1307 src/`
     This applies the fix automatically to all eligible call sites.
  2. If dotnet format is unavailable or incomplete, use search-and-replace:
     For each file, add `, StringComparison.Ordinal` to each call.
  3. After bulk fix, run `dotnet build Linting.csproj` to confirm 0 errors.
     Some .StartsWith overloads may not accept StringComparison -- use
     string.StartsWith(value, StringComparison.Ordinal) form if needed.
**EXCEPTION**: Do NOT add StringComparison to:
  - Calls where variable (not literal) is the argument and may be locale-sensitive
  - Any call site already specifying a StringComparison
  - Regex or culture-specific display string comparisons
**OKF Rule 6**: culture-safe string operations for all internal fixed-format strings.

---

## FINDING F-B5 -- return null replacements (null-safe guard pattern)

**File**: src/V12_002.UI.Panel.Helpers.cs
**Issue**: 24x `return null` from UI helper methods. These are high-risk because
  callers may not null-check the return.
**Fix approach** (conservative -- do NOT introduce Option<T> wrapper type):
  For each `return null` method, add a null guard at ALL call sites that
  dereference the return value without checking. The goal is not to eliminate
  null returns (that is Wave 9 material) but to ensure all call sites are
  null-safe. If a call site already null-checks, no change needed.
  Read each method and its callers before deciding if a call site is unsafe.
**Priority**: Only address call sites where a NullReferenceException would
  be unhandled (i.e., not inside a try/catch). Skip UI rendering code where
  null return is expected and handled by WPF binding engine.
**OKF Rule 5**: defense in depth -- no unguarded null dereferences.

---

## Commit order recommendation

1. fix(repairs/pr-f): Account.All snapshot in Propagation.cs
2. fix(repairs/pr-f): SA1204 static ordering in IPC.Hardening.cs
3. fix(repairs/pr-f): SA1503 braces -- Entries.Retest/Trend/MOMO (CSharpier)
4. fix(repairs/pr-f): StringComparison.Ordinal bulk sweep (dotnet format / manual)
5. fix(repairs/pr-f): null guard call sites in UI.Panel.Helpers.cs

---

## Gate Requirements

- [ ] dotnet build Linting.csproj -- 0 errors
- [ ] python scripts/wave7_prepush_gate.py --base origin/main -- GATE PASSED
- [ ] dotnet csharpier check src/ -- 0 issues
- [ ] No bare Account.All in Propagation.cs
- [ ] No lock() introduced
- [ ] All modified methods CYC <= 8
- [ ] Diff size < 150,000 chars stripped
  (StringComparison bulk may be large -- split into two branches if needed)

## PR title
"fix(repairs): propagation safety + StringComparison.Ordinal bulk + SA1503/SA1204 sweep"

## NOTE on diff size
This PR will have the largest diff due to StringComparison bulk.
If wave7_prepush_gate.py reports diff size > 150,000 stripped chars,
split into two PRs:
  PR-F1: Propagation + SA1204 + SA1503 (small)
  PR-F2: StringComparison.Ordinal bulk only
