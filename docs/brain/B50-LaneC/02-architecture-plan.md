# B50-LaneC Architecture Plan
## Fix: DW-B48-01 — Make CopyEngineTests.cs Compile and `dotnet test` Pass

**Status**: REVIEW_PASS  
**Epic**: B50-LaneC  
**Spec Req**: DW-B48-01  
**Date**: 2026-08-07  

---

## 1. Problem Statement

`CopyEngineTests.cs` produces 60 compilation errors when run against the current state of
`CopyEngine.cs`. These errors fall into four classes:

| Class | Error Code | Count | Root Cause |
|-------|-----------|-------|-----------|
| CopyRule not found | CS0246 | ~30 | `CopyRule` is `private` in `CopyEngine.cs`; tests reference it directly |
| ImmutableDictionary not found | CS0234 | 9 | NT8-004 banned type; test file still uses it |
| DisarmTrailBe not found | CS0246 | 2 | Method deleted in B33 T8; two dead test methods remain |
| Globals ambiguous | CS0433 | N/A | Already fully qualified in `CopyEngine.cs`; not in test file; out-of-scope |

---

## 2. Root Cause Analysis

### Error Class 1 — CS0246: `CopyRule` not found

`CopyRule` is declared at [`CopyEngine.cs:173`](C:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs:173):
```csharp
private readonly struct CopyRule { ... }
```

`CopyEngineTests.cs` references `CopyRule` at approximately 15 sites:
- `ConcurrentBag<CopyRule>` casts (lines 71, 92, 113, 122, 126)
- `CopyRule?` nullable references (lines 389, 515, 545)
- `typeof(CopyRule)` in reflection calls

Because the struct is `private`, it is not visible even within the same assembly. The test project
compiles against the same assembly (no separate `.Tests.csproj` — tests are co-located in
`PropTraderTools`), so the correct fix is `internal` per JS-010 (never `public` for internal types).

### Error Class 2 — CS0234: `System.Collections.Immutable` not found (NT8-004)

`CopyEngineTests.cs` uses `System.Collections.Immutable.ImmutableDictionary<string, FollowerAtmMode>`
in 9 places. NT8 Rule NT8-004 bans `ImmutableDictionary` / `System.Collections.Immutable` because
.NET Framework 4.8 in NinjaTrader does not carry that assembly without an explicit NuGet reference.

`CopyEngine.AddRule` already accepts `Dictionary<string, FollowerAtmMode>` — no API surface change
is needed. All test-side uses fall into two sub-patterns:

- **Sub-pattern A** (7 sites): `ImmutableDictionary<string, FollowerAtmMode>.Empty` passed directly
  as an empty-map argument → replace with `new Dictionary<string, FollowerAtmMode>()`

- **Sub-pattern B** (2 sites): Builder chain `.SetItem(key, value)` to construct a single-entry map
  → replace with collection-initializer `new Dictionary<string, FollowerAtmMode> { { key, value } }`

### Error Class 3 — CS0246: `DisarmTrailBe` not found

`DisarmTrailBe` was deleted in B33 T8 (comment at `CopyEngine.cs:2152`):
> "B33 T8: ArmTrailBe, DisarmTrailBe, OnTrailBeAccountUpdate REMOVED -- dead since B32 (DW-B32-05)"

Two test methods in `CopyEngineTests.cs` still call `_engine.DisarmTrailBe(null)`:
- `DisarmTrailBe_WhenNotArmed_NoException` (lines 1747–1753)
- `DisarmTrailBe_Idempotent_NoExceptionOnDoubleCall` (lines 1755–1765)

These are dead tests — they test a deleted method. The correct action is removal. No replacement
test is needed; the production method itself was confirmed dead and removed in B32/B33.

### Error Class 4 — CS0433: `Globals` ambiguous (OUT-OF-SCOPE)

`grep` confirmed `Globals` does **not** appear in `CopyEngineTests.cs`. The CS0433 ambiguity
originates in `CopyEngine.cs` at line 2319 (`NinjaTrader.Core.Globals.UserDataDir`), which
already uses the fully qualified name. Any residual CS0433 from other source files is unrelated
to DW-B48-01. **This lane does not touch `Globals`.**

---

## 3. Solution Approach

### Fix 1 — `CopyEngine.cs`: Widen `CopyRule` access to `internal`

**File**: `C:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs`  
**Line**: 173  
**Change**: `private readonly struct CopyRule` → `internal readonly struct CopyRule`

This is a single-word substitution. No other code in `CopyEngine.cs` changes.

**JS-010 compliance**: `internal` is the correct minimum-visibility modifier for a type that must
be visible within the assembly but should not be part of the public API.

### Fix 2 — `CopyEngineTests.cs`: Replace all `ImmutableDictionary` usages

**File**: `C:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngineTests.cs`

**Sub-pattern A replacements** (7 sites — direct empty-map arguments):
```csharp
// BEFORE
System.Collections.Immutable.ImmutableDictionary<string, FollowerAtmMode>.Empty
// AFTER
new Dictionary<string, FollowerAtmMode>()
```
Affected lines: 482, 511, 541, 684, 712, 827, 865 (approximate; verify by grep during ticket execution).

**Sub-pattern B replacements** (2 sites — single-entry builder):
```csharp
// BEFORE
var atmMap = System.Collections.Immutable.ImmutableDictionary<string, FollowerAtmMode>.Empty
    .SetItem("FollowerA", new FollowerAtmMode.Named("ScalpTemplate"));
// AFTER
var atmMap = new Dictionary<string, FollowerAtmMode> { { "FollowerA", new FollowerAtmMode.Named("ScalpTemplate") } };
```
```csharp
// BEFORE
var atmMap = System.Collections.Immutable.ImmutableDictionary<string, FollowerAtmMode>.Empty
    .SetItem("FollowerB", new FollowerAtmMode.Market());
// AFTER
var atmMap = new Dictionary<string, FollowerAtmMode> { { "FollowerB", new FollowerAtmMode.Market() } };
```
Affected lines: ~640–641 and ~712–713.

`using System.Collections.Generic;` is already present (required for `ConcurrentBag`). No new using
directive is needed.

### Fix 3 — `CopyEngineTests.cs`: Remove two dead test methods

**File**: `C:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngineTests.cs`  
**Lines**: 1747–1765  
**Action**: Delete both `[Fact]` methods entirely:
- `DisarmTrailBe_WhenNotArmed_NoException`
- `DisarmTrailBe_Idempotent_NoExceptionOnDoubleCall`

No replacement tests. The production method is gone; the tests have no value.

---

## 4. File Change Summary

| File | Change Type | Count |
|------|------------|-------|
| `CopyEngine.cs` | 1-word access modifier change | 1 |
| `CopyEngineTests.cs` | ImmutableDictionary → Dictionary replacements | 9 sites |
| `CopyEngineTests.cs` | Dead test method removal | 2 methods (~19 lines) |

**No new files. No new classes. No new methods. No new interfaces.**

---

## 5. Component / Class Map

No new components are introduced. Affected types:

| Type | File | Change |
|------|------|--------|
| `CopyRule` (struct) | `CopyEngine.cs:173` | `private` → `internal` |
| `CopyEngineTests` (test class) | `CopyEngineTests.cs` | 9 replacements + 2 method deletions |

---

## 6. Threading Model

No threading changes. `CopyRule` is a `readonly struct` — immutable value type. Widening its
access modifier does not affect thread safety. The `Dictionary` instances created in tests are
local variables within `[Fact]` methods and are never shared across threads.

---

## 7. Jane Street / NT8 Compliance

| Rule | Requirement | Status |
|------|------------|--------|
| JS-010 | `internal` not `public` for non-API types | PASS — `internal` used |
| JS-021 | No `lock()` added | PASS — no lock() introduced |
| JS-002 | No `return null` added | PASS — no return null introduced |
| JS-033 | No `async void` added | PASS — no async void introduced |
| NT8-004 | No `ImmutableDictionary` / `System.Collections.Immutable` | PASS — all replaced with `Dictionary<K,V>` |
| CYC | No new methods; all existing methods ≤8 | PASS — no complexity added |
| ASCII | No non-ASCII characters introduced | PASS |

---

## 8. Out-of-Scope Items

- **CS0433 `Globals` ambiguity**: `NinjaTrader.Core.Globals.UserDataDir` at `CopyEngine.cs:2319`
  is already fully qualified. This error does not originate in `CopyEngineTests.cs`. Not touched.
- **Any other compilation errors** in source files outside `CopyEngine.cs` / `CopyEngineTests.cs`.
- **Test logic changes**: No test assertions are modified; only dead tests are removed and
  type-incompatible arguments are corrected to the already-accepted `Dictionary<K,V>` type.

---

## 9. Data Flow (unchanged)

```
[Fact] test method
  → AddRule(leaderAcct, qty, direction, new Dictionary<string, FollowerAtmMode>())
  → CopyEngine.AddRule stores CopyRule (now internal-visible)
  → Assertions on ConcurrentBag<CopyRule> via reflection
```

The data flow is identical to the intended design; the errors were purely access-modifier and
banned-type issues blocking compilation.
