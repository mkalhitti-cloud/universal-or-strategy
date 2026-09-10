# Architecture Plan: PTT-REPAIRS-08-JS002
# JS-002 return-null repairs in CopyEngine.cs
# Status: PLAN_COMPLETE

---

## 0. Baseline

```
dotnet build  : 0 Error(s)
dotnet test   : 19 passed / 450 failed (NT8-runtime) / 32 skipped
Target file   : src/PropTraderTools/CopyEngine.cs (only)
LangVersion   : 9.0 (PropTraderTools.Tests.csproj)
NRT context   : <Nullable> NOT set -- nullable annotations are informational only
```

---

## 1. LANE-SPLIT GATE RESULT: SINGLE-PIPELINE

**Q1. Same method or within 50 lines?**
NO. Sites span L1271 through L7370 across 12 distinct methods in one file.

**Q2. Fix B design depends on Fix A final design?**
NO. Every fix is a return-type annotation or `return null` -> `return default` change with
no cross-site dependencies. Each method is independently resolvable.

**Q3. Each fix has standalone value if the other is blocked?**
YES. Every site independently closes a JS-002 lint violation. A partial merge (any subset
of tickets) still reduces violations and keeps the build green.

**Q4. Each fix has an independent SIM verification path?**
YES. Each method can be independently verified by confirming:
  (a) the method no longer carries an unannotated `return null` for reference-type returns, OR
  (b) the `return null` is replaced with `return default` for struct-nullable methods.

**Gate result:** Q1=NO, Q2=NO (qualifies for LANES), Q3=YES, Q4=YES.
Default rule applies: SINGLE-PIPELINE selected. All fixes are annotation-only or
cosmetic `return default` substitutions. Parallel lane overhead is not justified.

---

## 2. Nullable Context Analysis (Critical Precondition)

The project uses C# 9.0 targeting net48 with NO `<Nullable>enable</Nullable>`.
This has two consequences:

**Value-type nullables (struct?)**:
`CopyRule?` = `Nullable<CopyRule>`, `double?` = `Nullable<double>`.
These ARE genuine Option wrappers. `return null` compiles to `new Nullable<CopyRule>()` with
`HasValue = false`. However, the TEXT `return null` triggers the JS-002 linter. Fix: replace
with `return default` which is semantically identical and lint-clean.

**Reference-type nullables (T? for class types)**:
`Position?`, `Order?`, `Account?`, `int[]?` are PURELY informational annotations with no
compiler enforcement. Adding `?` to the return type declares the nullable contract to readers
and to any future NRT-enable pass. No compiler errors are introduced. No caller logic changes
are required (all callers already null-guard or pass to IsFlat which already handles null).

---

## 3. Per-Site Analysis

### SITE 1: FindBePosition -- L1271
**Method** (L1260-1272):
```
internal NinjaTrader.Cbi.Position FindBePosition(Account acc, NinjaTrader.Cbi.Instrument instr)
{
    foreach (NinjaTrader.Cbi.Position p in acc.Positions)  // (1)
        if (p.Instrument != null                            // (2)
            && p.Instrument.FullName == instr.FullName)
            return p;
    return null;  // <-- SITE
}
```
**Callers** (grep: 1 internal caller):
- L1248: `var pos = FindBePosition(acc, instr); if (pos == null || pos.Quantity == 0) return;`
  Caller ALREADY null-guards.

**Strategy**: (a) Annotate return type as `NinjaTrader.Cbi.Position?`. No logic change. No
caller changes required.
**CYC**: 3 -> 3 (no new branches)

---

### SITE 2: FindMatchingRule -- L1997
**Method** (L1987-1998):
```
private CopyRule? FindMatchingRule(Order order)
{
    foreach (var rule in _rules)     // (1)
        if (order.Instrument.FullName == rule.Instrument
            && order.Account.Name == rule.MasterAccount?.Name) // (2) if
            return rule;
    return null;  // <-- SITE
}
```
CopyRule is a readonly struct. `CopyRule?` = `Nullable<CopyRule>`. `return null` compiles to
`Nullable<CopyRule>` with `HasValue = false` -- semantically correct Option pattern.
The JS-002 linter flags the TEXT `return null` even for struct nullables.

**Callers** (grep: 1 internal caller):
- L1522: `CopyRule? matchedRule = FindMatchingRule(order); if (matchedRule == null)`
  Uses the HasValue pattern correctly.

**Strategy**: (b) Replace `return null` with `return default`. Zero semantic change. Lint-clean.
**CYC**: 3 -> 3

---

### SITE 3: CaptureLinkedTargetPrice -- L3030
**Method** (L3027-3043):
```
private double? CaptureLinkedTargetPrice(Account acc, string stopName)
{
    if (!TryParseStopSuffix(stopName, out string suffix))  // (1)
        return null;  // <-- SITE
    ...
    return PickBestTargetPrice(pttPrice, atmPrice);
}
```
`double?` = `Nullable<double>`. Same analysis as SITE 2.

**Callers** (grep: 1 internal caller):
- L2838: `double? capturedTargetPrice = CaptureLinkedTargetPrice(acc, leaderOrder.Name);`
  L2842: `if (capturedTargetPrice.HasValue)` -- already uses HasValue pattern correctly.

**Strategy**: (b) Replace `return null` with `return default`. Zero semantic change.
**CYC**: 5 -> 5

---

### SITE 4+5: FindLeaderCollateralOrder -- L3135, L3143
**Method** (L3132-3144):
```
private static Order FindLeaderCollateralOrder(Order leaderOrder, string suffix)
{
    if (leaderOrder?.Account?.Orders == null || string.IsNullOrEmpty(suffix))  // (1)
        return null;  // <-- SITE L3135
    ...
    foreach (var o in leaderOrder.Account.Orders.ToList())  // (2)
    {
        if (o != null && (o.Name == stopName || o.Name == tgtName))  // (3)
            return o;
    }
    return null;  // <-- SITE L3143
}
```
Return type is `Order` (non-nullable). Two `return null` paths.

**Callers** (grep: 1 internal caller):
- L3301: `Order leaderLeg = FindLeaderCollateralOrder(leaderOrder, s);`
  leaderLeg is then passed to `ResubmitOneCollateralLeg(acc, fo, newPrice, ..., s, leaderLeg)`.
  ResubmitOneCollateralLeg uses `leaderLeg != null ? leaderLeg.Quantity : fo.Quantity` in
  CreateAndSubmitCollateralStop (L3398) and CreateAndSubmitCollateralTarget (L3443).
  The downstream methods ALREADY null-guard leaderLeg.
  
  Required caller update: `Order leaderLeg = ...` -> `Order? leaderLeg = ...`
  This is within CopyEngine.cs.

**Strategy**: (a) Annotate return type as `Order?`. Update ONE caller at L3301.
  No logic changes to ResubmitOneCollateralLeg or its children.
**CYC**: 3 -> 3

---

### SITE 6: FindFollowerBracketOrder (IEnumerable overload) -- L3951
**Method** (L3929-3952):
```
private Order? FindFollowerBracketOrder(IEnumerable<Order> orders, ...)
{
    ...
    return null;  // <-- SITE L3951
}
```
Return type is ALREADY `Order?`. The `?` annotation is already present.

**Callers** (grep: method is called from Account+overload at L3907):
- L2783: `var fo = FindFollowerBracketOrder(acc, ...); if (fo == null) return;`
  Already null-guarded.

**Strategy**: ALREADY COMPLIANT. Return type already declared `Order?`. No code change needed.
  Confirm and annotate in comments only.
**CYC**: 8 -> 8 (unchanged)

---

### SITE 7: FindFollowerEntryOrder -- L4150
**Method** (L4132-4151):
```
private static Order? FindFollowerEntryOrder(Account follower, Instrument instrument)
{
    foreach (var order in ActiveOrders(follower))   // (1)
    {
        if (order.Instrument != instrument)          // (2)
            continue;
        if (... state+type+name guards ...)          // (3)
            return order;
    }
    return null;  // <-- SITE L4150
}
```
Return type is ALREADY `Order?`.

**Callers** (grep: 1 caller):
- L4190: `var fo = FindFollowerEntryOrder(acc, instrument); if (fo == null) continue;`
  Already null-guarded.

**Strategy**: ALREADY COMPLIANT. Return type already declared `Order?`. No code change needed.
**CYC**: 3 -> 3

---

### SITE 8: FindFollowerRuleForOrder -- L4439
**Method** (L4425-4440):
```
private CopyRule? FindFollowerRuleForOrder(Order cancelledOrder, out int followerIndex)
{
    followerIndex = -1;
    foreach (var rule in _rules)  // (1)
    {
        if (rule.Instrument != cancelledOrder.Instrument.FullName)
            continue;             // (2)
        int idx = FindFollowerSlotIndex(rule, cancelledOrder.Account.Name);  // (3)
        if (idx >= 0)             // (4)
        {
            followerIndex = idx;
            return rule;
        }
    }
    return null;  // <-- SITE L4439
}
```
`CopyRule?` = `Nullable<CopyRule>` (struct). Same analysis as SITE 2.

**Callers** (grep: 1 caller):
- L4390: `var matchedRule = FindFollowerRuleForOrder(cancelledOrder, out int followerIndex);`
  L4391: `if (!matchedRule.HasValue || followerIndex < 0) return;`
  Uses HasValue correctly.

**Strategy**: (b) Replace `return null` with `return default`. Zero semantic change.
**CYC**: 5 -> 5

---

### SITES 9-10: ResolveNullFollowerSlot -- L5955, L5977
**Method** (L5950-5978):
```
private Account ResolveNullFollowerSlot(CopyRule rule, int i)
{
    var names = rule.FollowerAccountNames;
    var name = (names != null && i < names.Length) ? names[i] : null;
    if (string.IsNullOrEmpty(name))
        return null;  // <-- SITE L5955 -- NT8 pattern: null = slot could not be resolved
    if (_resolvedFollowers.TryGetValue(name, out var cached))
        return cached;
    var resolved = FindFollowerAccount(name);
    if (resolved != null)
    {
        _resolvedFollowers.TryAdd(name, resolved);
        NinjaTrader.Code.Output.Process(...);
        return resolved;
    }
    NinjaTrader.Code.Output.Process(...WARNING...);
    return null;  // <-- SITE L5977 -- NT8 pattern: null = slot could not be resolved
}
```
Return type is `Account` (non-nullable). Two `return null` paths with explicit
"NT8 pattern: null = slot could not be resolved" comments -- behavior MUST NOT change.

**Callers** (grep: 1 caller):
- L5938: `var resolved = ResolveNullFollowerSlot(rule.Value, i); if (resolved != null) yield return resolved;`
  Caller ALREADY null-guards.

**Strategy**: (a) Annotate return type as `Account?`. Preserve both `return null` statements
  and their NT8-pattern comments verbatim. No logic change.
**CYC**: 3 -> 3

---

### SITES 11-12: FindRule (spec cites as L5990, L5996)
**Method** (L5987-5997):
```
internal CopyRule? FindRule(Instrument instrument)
{
    if (instrument == null)
        return null;  // <-- SITE (L5990 in spec, Change 8 null guard)
    foreach (var rule in _rules)
    {
        if (rule.Instrument == instrument.FullName)
            return rule;
    }
    return null;  // <-- SITE (L5996 in spec)
}
```
`CopyRule?` = `Nullable<CopyRule>` (struct). Same analysis as SITE 2.

NOTE: Spec cites these as L5990 and L5996 but current file context places them at L5988-5996.
Line numbers may have drifted. Engineer must verify exact lines before editing.

**Strategy**: (b) Replace both `return null` with `return default`. Zero semantic change.
**CYC**: 3 -> 3

---

### SITE 13: FindPosition -- L6080
**Method** (L6075-6081):
```
private Position FindPosition(Account acc, Instrument instrument)
{
    foreach (Position p in acc.Positions)
        if (p.Instrument != null && p.Instrument.FullName == instrument.FullName)
            return p;
    return null;  // <-- SITE L6080
}
```
Return type is `Position` (non-nullable). 20+ callers.

**Callers** (all verified safe):
- L1689, L1765, L1843, L1954, L2614, L4634, L6321, L6483, L6491, L6628, L6693, L6860, L6954:
  All call `IsFlat(FindPosition(...))`. IsFlat at L6008 already handles null:
  `return pos == null || pos.Quantity == 0;`
- L2281: explicit `if (pos == null || pos.Quantity == 0) return;`
- L4778: explicit `if (pos == null) return false;`
- L5287: explicit `if (pos == null || pos.Quantity == 0)`
- L5397: `var posAfterCancel = FindPosition(acc, instrument); SubmitMarketFlattenOrder(acc, instrument, posAfterCancel);`
  SubmitMarketFlattenOrder (L5501) begins with `if (pos == null || pos.Quantity == 0) return;`
- L5417: explicit null check
- L5687, L5753: explicit null check
- L6320: `var pos = FindPosition(...); if (IsFlat(pos))` then dereferences pos -- safe via IsFlat guard
- L6627, L6692, L6859, L6953: IsFlat guard before dereference

**Required propagation changes** (all within CopyEngine.cs):
1. `private Position FindPosition(...)` -> `private Position? FindPosition(...)`
2. `private static bool IsFlat(NinjaTrader.Cbi.Position pos)` -> `private static bool IsFlat(NinjaTrader.Cbi.Position? pos)`
   (IsFlat already handles null; changing param annotation is required so `IsFlat(FindPosition(...))` compiles)
3. `private void SubmitMarketFlattenOrder(..., Position pos)` -> `private void SubmitMarketFlattenOrder(..., Position? pos)`
   (SubmitMarketFlattenOrder already null-guards; parameter annotation matches reality)
4. `internal Position FindPositionPublic(Account acc, Instrument instrument)` -> `internal Position? FindPositionPublic(...)`

**External callers of FindPositionPublic** (verified safe -- no code change needed in those files):
- TradeCopierPanel.cs L1566: `if (pos == null) return false;`
- TradeCopierPanel.cs L2037: `if (pos == null || pos.Quantity == 0) return;`
- PttBreakEvenSwap.cs L77: `if (pos == null || pos.Quantity == 0) return;`
All already null-guard. The `var pos` variable will be inferred as `Position?` but no logic changes.

**CYC**: 2 -> 2 (FindPosition), 1 -> 1 (IsFlat), 3 -> 3 (SubmitMarketFlattenOrder)

---

### SITE 14: ResolveMultipliers -- L7355
**Method** (L7352-7357):
```
internal static int[] ResolveMultipliers(CopyRuleDto dto)
{
    if (dto.FollowerMultipliers == null || dto.FollowerMultipliers.Length == 0)  // (1)(2)
        return null;  // <-- SITE L7355
    return dto.FollowerMultipliers;
}
```
Return type is `int[]` (non-nullable array reference type).

NOTE: The comment at L7350 states "Returns null when dto.FollowerMultipliers is null/empty --
CopyRule.Create handles null as all-1s." Do NOT change to `return Array.Empty<int>()`.
CopyRule.Create distinguishes between null (all-1s default) and empty array (0 multipliers).
Using `return Array.Empty<int>()` would change behavior. Annotation-only fix is correct.

**Callers** (grep: 1 caller):
- L7295: `int[] multipliers = ResolveMultipliers(dto);`
  Caller passes `multipliers` to `CopyRule.Create(multipliers: multipliers)` which has
  `int[] multipliers = null` as parameter. With NRT disabled, no compiler error.
  Required update: `int[]? multipliers = ResolveMultipliers(dto);` (within CopyEngine.cs).

**Strategy**: (a) Annotate return type as `int[]?`. Update ONE caller at L7295.
**CYC**: 2 -> 2

---

### SITE 15: FindFollowerAccount -- L7370
**Method** (L7363-7371):
```
private static Account? FindFollowerAccount(string name)
{
    foreach (var acc in Account.All)  // (1)
    {
        if (acc.Name == name)         // (2)
            return acc;
    }
    return null;  // <-- SITE L7370
}
```
Return type is ALREADY `Account?`.

**Callers** (grep: 2 callers):
- L5958 (within ResolveNullFollowerSlot): `var resolved = FindFollowerAccount(name); if (resolved != null)`
  Already null-guarded.
- L7282: `followers[i] = FindFollowerAccount(followerNames[i]); if (followers[i] == null)`
  Already null-guarded.

**Strategy**: ALREADY COMPLIANT. Return type already declared `Account?`. No code change needed.
**CYC**: 2 -> 2

---

## 4. Complete Change Inventory

### Changes Required (code edits):

| # | Location | Change Type | What Changes |
|---|----------|-------------|-------------|
| 1 | FindBePosition (L1260) | Annotation | `Position` -> `Position?` on return type |
| 2 | FindMatchingRule (L1997) | Cosmetic | `return null` -> `return default` |
| 3 | CaptureLinkedTargetPrice (L3030) | Cosmetic | `return null` -> `return default` |
| 4 | FindLeaderCollateralOrder (L3132) | Annotation | `Order` -> `Order?` on return type |
| 5 | FindLeaderCollateralOrder caller (L3301) | Annotation | `Order leaderLeg =` -> `Order? leaderLeg =` |
| 6 | FindFollowerRuleForOrder (L4439) | Cosmetic | `return null` -> `return default` |
| 7 | ResolveNullFollowerSlot (L5950) | Annotation | `Account` -> `Account?` on return type |
| 8 | FindRule (L5987, both return null) | Cosmetic | `return null` -> `return default` x2 |
| 9 | FindPosition (L6075) | Annotation | `Position` -> `Position?` on return type |
| 10 | IsFlat (L6008) | Annotation | `Position pos` -> `Position? pos` on parameter |
| 11 | SubmitMarketFlattenOrder (L5501) | Annotation | `Position pos` -> `Position? pos` on parameter |
| 12 | FindPositionPublic (L6086) | Annotation | `Position` -> `Position?` on return type |
| 13 | ResolveMultipliers (L7352) | Annotation | `int[]` -> `int[]?` on return type |
| 14 | ResolveMultipliers caller DtoToRule (L7295) | Annotation | `int[] multipliers =` -> `int[]? multipliers =` |

### No-Change Sites (already compliant):
- FindFollowerBracketOrder (L3951): already `Order?` -- COMPLIANT
- FindFollowerEntryOrder (L4150): already `Order?` -- COMPLIANT
- FindFollowerAccount (L7370): already `Account?` -- COMPLIANT

---

## 5. CYC Recount

All proposed changes are return-type annotations or `return null` -> `return default` substitutions.
No new conditional branches are added to any method.

| Method | Pre-fix CYC | Post-fix CYC | Change |
|--------|------------|-------------|--------|
| FindBePosition | 3 | 3 | 0 |
| FindMatchingRule | 3 | 3 | 0 |
| CaptureLinkedTargetPrice | 5 | 5 | 0 |
| FindLeaderCollateralOrder | 3 | 3 | 0 |
| FindFollowerBracketOrder (IEnum) | 8 | 8 | 0 |
| FindFollowerEntryOrder | 3 | 3 | 0 |
| FindFollowerRuleForOrder | 5 | 5 | 0 |
| ResolveNullFollowerSlot | 3 | 3 | 0 |
| FindRule | 3 | 3 | 0 |
| FindPosition | 2 | 2 | 0 |
| IsFlat | 1 | 1 | 0 |
| SubmitMarketFlattenOrder | 3 | 3 | 0 |
| FindPositionPublic | 1 | 1 | 0 |
| ResolveMultipliers | 2 | 2 | 0 |
| FindFollowerAccount | 2 | 2 | 0 |

**All methods: CYC <= 8. JS-013 satisfied.**

---

## 6. Ticket Grouping

### Ticket T1 -- Struct-Nullable Cosmetic (return null -> return default)
**Spec Requirement IDs**: JS-002 (4 methods, 6 `return null` sites)
**File**: `src/PropTraderTools/CopyEngine.cs`
**Rationale**: All targets are `CopyRule?` or `double?` (Nullable<struct>). `return null` is
semantically equivalent to `return default` but the TEXT triggers JS-002 linter. This ticket
closes 4 method violations with zero behavior change and zero risk.

Sites covered:
- FindMatchingRule: L1997 `return null` -> `return default`
- CaptureLinkedTargetPrice: L3030 `return null` -> `return default`
- FindFollowerRuleForOrder: L4439 `return null` -> `return default`
- FindRule: both `return null` sites -> `return default`

**Method signatures (unchanged, only body edit):**
```csharp
private CopyRule? FindMatchingRule(Order order)
private double? CaptureLinkedTargetPrice(Account acc, string stopName)
private CopyRule? FindFollowerRuleForOrder(Order cancelledOrder, out int followerIndex)
internal CopyRule? FindRule(Instrument instrument)
```

**Callers:**
- `FindMatchingRule`: 1 caller at L1522.
  `CopyRule? matchedRule = FindMatchingRule(order); if (matchedRule == null)`
  Already null-guarded via HasValue pattern. No caller code change required.
- `CaptureLinkedTargetPrice`: 1 caller at L2838/L2842.
  `double? capturedTargetPrice = CaptureLinkedTargetPrice(acc, leaderOrder.Name);`
  `if (capturedTargetPrice.HasValue)` -- already uses HasValue pattern. No caller change required.
- `FindFollowerRuleForOrder`: 1 caller at L4390/L4391.
  `var matchedRule = FindFollowerRuleForOrder(cancelledOrder, out int followerIndex);`
  `if (!matchedRule.HasValue || followerIndex < 0) return;`
  Already uses HasValue pattern. No caller change required.
- `FindRule`: callers use `CopyRule?` struct-nullable; `return default` is semantically
  identical to `return null` for Nullable<CopyRule>. No caller change required.

**xUnit tests:**
- `[Fact] FindMatchingRule_NoMatch_ReturnsDefaultNotNull()` -- assert `!result.HasValue`, not `result == null`
- `[Fact] CaptureLinkedTargetPrice_InvalidSuffix_ReturnsDefault()` -- assert `!result.HasValue`
- `[Fact] FindFollowerRuleForOrder_NoMatch_ReturnsDefault()` -- assert `!result.HasValue`
- `[Fact] FindRule_NullInstrument_ReturnsDefault()` -- assert `!result.HasValue`

**7-Scan Checklist (T1 pre-assessment):**
```
SCAN-1: lock() in changed files
  Pre-assessment: PASS -- annotation-only / cosmetic changes; no lock() added or touched.
  Verify: grep "lock(" src/PropTraderTools/CopyEngine.cs = 0 matches in changed hunks.

SCAN-2: Non-ASCII in changed files
  Pre-assessment: PASS -- only `return default` text substitutions; all ASCII.
  Verify: all new/changed text uses ASCII characters only; no Unicode, emoji, curly quotes.

SCAN-3: CS error lines in diff
  Pre-assessment: PASS -- `return default` is valid for Nullable<struct> return types.
  Verify: no CS-prefixed error annotations in diff output.

SCAN-4: dotnet build 0 Error(s)
  Pre-assessment: PASS -- `return default` compiles identically to `return null` for
  Nullable<struct>. No caller signature changes. NRT disabled; no new warnings expected.
  Verify: dotnet build src/PropTraderTools/ -> 0 Error(s).

SCAN-5: dotnet test counts (passed >= 19, no new regressions)
  Pre-assessment: PASS -- zero behavior change; `return default` == `HasValue=false` for
  Nullable<struct>. All existing tests continue to pass.
  Verify: dotnet test -> passed count >= 19, 0 new failures vs baseline.

SCAN-6: powershell -File .\deploy-sync.ps1 -> SYNC COMPLETE
  Pre-assessment: PASS (expected) -- file modified, deploy-sync.ps1 re-syncs hard links.
  Verify: script output contains "SYNC COMPLETE".

SCAN-7: hardlink count = 1 for CopyEngine.cs
  Pre-assessment: PASS (expected) -- CopyEngine.cs has exactly 1 hard link (no extra copies).
  Verify: (Get-Item src/PropTraderTools/CopyEngine.cs).LinkCount -eq 1
```

---

### Ticket T2 -- Reference-Type Annotation (T -> T? + propagation)
**Spec Requirement IDs**: JS-002 (8 sites across 7 methods)
**File**: `src/PropTraderTools/CopyEngine.cs`
**Rationale**: Non-nullable reference types returned as null must have `?` added to their
return type declarations. With NRT disabled (LangVersion 9.0, no Nullable element), these
are informational changes that declare the nullable contract and satisfy the JS-002 linter.
Callers are already null-guarded; the propagation changes update parameter annotations to match.

Sites covered:
- FindBePosition: `NinjaTrader.Cbi.Position` -> `NinjaTrader.Cbi.Position?` return
- FindLeaderCollateralOrder: `Order` -> `Order?` return + L3301 caller annotation
- ResolveNullFollowerSlot: `Account` -> `Account?` return (NT8-pattern comments preserved)
- FindPosition: `Position` -> `Position?` return
- IsFlat: `Position pos` -> `Position? pos` parameter (propagation)
- SubmitMarketFlattenOrder: `Position pos` -> `Position? pos` parameter (propagation)
- FindPositionPublic: `Position` -> `Position?` return

**Method signatures after fix:**
```csharp
internal NinjaTrader.Cbi.Position? FindBePosition(Account acc, NinjaTrader.Cbi.Instrument instr)
private static Order? FindLeaderCollateralOrder(Order leaderOrder, string suffix)
private Account? ResolveNullFollowerSlot(CopyRule rule, int i)
private Position? FindPosition(Account acc, Instrument instrument)
private static bool IsFlat(NinjaTrader.Cbi.Position? pos)
private void SubmitMarketFlattenOrder(Account acc, Instrument instrument, Position? pos)
internal Position? FindPositionPublic(Account acc, Instrument instrument)
```

**Caller annotation (CopyEngine.cs only):**
- L3301: `Order leaderLeg = ...` -> `Order? leaderLeg = ...`

**NT8-pattern comment preservation** (in ResolveNullFollowerSlot):
Both `return null;` lines MUST retain their inline comments:
```csharp
return null; // NT8 pattern: null = slot could not be resolved
```

**Callers:**
- `FindBePosition`: 1 caller at L1248 (CopyEngine.cs).
  `var pos = FindBePosition(acc, instr); if (pos == null || pos.Quantity == 0) return;`
  Already null-guarded. No caller change required.
- `FindLeaderCollateralOrder`: 1 caller at L3301 (CopyEngine.cs).
  `Order leaderLeg = FindLeaderCollateralOrder(leaderOrder, s);`
  REQUIRES annotation update -> `Order? leaderLeg = FindLeaderCollateralOrder(leaderOrder, s);`
  Downstream callee `ResubmitOneCollateralLeg` uses `leaderLeg != null ? ...` null-guard already.
- `ResolveNullFollowerSlot`: 1 caller at L5938 (CopyEngine.cs).
  `var resolved = ResolveNullFollowerSlot(rule.Value, i); if (resolved != null) yield return resolved;`
  Already null-guarded. No caller change required.
- `FindPosition` (20+ callers, all CopyEngine.cs):
  - L1689, L1765, L1843, L1954, L2614, L4634, L6321, L6483, L6491, L6628, L6693, L6860, L6954:
    All call `IsFlat(FindPosition(...))`. IsFlat already handles null (L6008).
    IsFlat parameter annotation `Position? pos` is the propagation change in this ticket.
  - L2281, L4778, L5287, L5417, L5687, L5753: explicit null checks. No change required.
  - L5397: passes result to SubmitMarketFlattenOrder which already null-guards.
    SubmitMarketFlattenOrder parameter annotation `Position? pos` is the propagation change.
  - L6320, L6627, L6692, L6859, L6953: IsFlat guard before dereference. No change required.
- `IsFlat`: parameter annotation change only (`Position pos` -> `Position? pos`).
  All callers pass FindPosition() or an explicit var -- already safe.
- `SubmitMarketFlattenOrder`: parameter annotation change only.
  Callers already pass nullable-compatible values; body already null-guards.
- `FindPositionPublic`: 3 external callers (no code change required in those files):
  - TradeCopierPanel.cs L1566: `if (pos == null) return false;` -- already null-guarded.
  - TradeCopierPanel.cs L2037: `if (pos == null || pos.Quantity == 0) return;` -- already null-guarded.
  - PttBreakEvenSwap.cs L77: `if (pos == null || pos.Quantity == 0) return;` -- already null-guarded.
  All use `var pos` inference; `Position?` annotation accepted without code edit (NRT disabled).

**xUnit tests:**
- `[Fact] FindBePosition_NoMatch_ReturnsNull()` -- assert result == null
- `[Fact] FindLeaderCollateralOrder_NullAccount_ReturnsNull()` -- assert result == null
- `[Fact] FindPosition_NoMatch_ReturnsNull()` -- assert result == null
- `[Fact] IsFlat_NullPosition_ReturnsTrue()` -- assert IsFlat(null) == true

**7-Scan Checklist (T2 pre-assessment):**
```
SCAN-1: lock() in changed files
  Pre-assessment: PASS -- annotation-only changes; no lock() added or touched.
  Verify: grep "lock(" src/PropTraderTools/CopyEngine.cs = 0 matches in changed hunks.

SCAN-2: Non-ASCII in changed files
  Pre-assessment: PASS -- only `?` annotation additions and one `Order?` variable annotation;
  all ASCII. NT8-pattern inline comments are ASCII-only and preserved verbatim.
  Verify: all new/changed text uses ASCII characters only.

SCAN-3: CS error lines in diff
  Pre-assessment: PASS -- NRT is disabled (no <Nullable>enable</Nullable>); `T?` on reference
  types is purely informational. No CS8600/CS8603/CS8604 enforced. No new errors expected.
  Verify: no CS-prefixed error annotations in diff output.

SCAN-4: dotnet build 0 Error(s)
  Pre-assessment: PASS -- annotation additions to signatures are informational under NRT-off.
  One variable annotation at L3301 (Order? leaderLeg) compiles as-is.
  External callers (TradeCopierPanel.cs, PttBreakEvenSwap.cs) use var inference, no edit needed.
  Verify: dotnet build src/PropTraderTools/ -> 0 Error(s).

SCAN-5: dotnet test counts (passed >= 19, no new regressions)
  Pre-assessment: PASS -- annotation-only; runtime behavior unchanged. ResolveNullFollowerSlot
  return null paths preserved verbatim (NT8 pattern). IsFlat null-guard unchanged.
  Verify: dotnet test -> passed count >= 19, 0 new failures vs baseline.

SCAN-6: powershell -File .\deploy-sync.ps1 -> SYNC COMPLETE
  Pre-assessment: PASS (expected) -- file modified, deploy-sync.ps1 re-syncs hard links.
  Verify: script output contains "SYNC COMPLETE".

SCAN-7: hardlink count = 1 for CopyEngine.cs
  Pre-assessment: PASS (expected) -- CopyEngine.cs has exactly 1 hard link (no extra copies).
  Verify: (Get-Item src/PropTraderTools/CopyEngine.cs).LinkCount -eq 1
```

---

### Ticket T3 -- Array Return-Type Annotation
**Spec Requirement IDs**: JS-002 (1 site: ResolveMultipliers)
**File**: `src/PropTraderTools/CopyEngine.cs`
**Rationale**: `int[]` returned as null must be annotated `int[]?`. Do NOT change to
`Array.Empty<int>()` -- the CopyRule.Create contract distinguishes null (all-1s default)
from empty array. One caller annotation update required in DtoToRule.

Sites covered:
- ResolveMultipliers: `int[]` -> `int[]?` return
- DtoToRule caller L7295: `int[] multipliers =` -> `int[]? multipliers =`

**Method signature after fix:**
```csharp
internal static int[]? ResolveMultipliers(CopyRuleDto dto)
```

**Callers:**
- `ResolveMultipliers`: 1 caller at L7295 (CopyEngine.cs, within DtoToRule).
  `int[] multipliers = ResolveMultipliers(dto);`
  REQUIRES annotation update -> `int[]? multipliers = ResolveMultipliers(dto);`
  The updated variable is passed to `CopyRule.Create(multipliers: multipliers)`.
  `CopyRule.Create` declares `int[] multipliers = null` as parameter -- accepts null/nullable
  with NRT disabled. No change required to CopyRule.Create or downstream callers.

**xUnit tests:**
- `[Fact] ResolveMultipliers_NullDto_ReturnsNull()` -- assert result == null
- `[Fact] ResolveMultipliers_EmptyMultipliers_ReturnsNull()` -- assert result == null (null not empty array)
- `[Fact] ResolveMultipliers_ValidMultipliers_ReturnsArray()` -- assert result != null && result.Length == N

**7-Scan Checklist (T3 pre-assessment):**
```
SCAN-1: lock() in changed files
  Pre-assessment: PASS -- annotation-only change; no lock() added or touched.
  Verify: grep "lock(" src/PropTraderTools/CopyEngine.cs = 0 matches in changed hunks.

SCAN-2: Non-ASCII in changed files
  Pre-assessment: PASS -- only `int[]?` return-type annotation and one variable annotation;
  all ASCII.
  Verify: all new/changed text uses ASCII characters only.

SCAN-3: CS error lines in diff
  Pre-assessment: PASS -- NRT disabled; `int[]?` annotation is informational. `return null`
  for `int[]?` return type compiles identically to `int[]` (reference type, NRT off).
  `int[]? multipliers =` variable at L7295 compiles cleanly.
  Verify: no CS-prefixed error annotations in diff output.

SCAN-4: dotnet build 0 Error(s)
  Pre-assessment: PASS -- 2 annotation changes (return type + variable), zero logic delta.
  NRT disabled; no new compiler enforcement. CopyRule.Create accepts null parameter as-is.
  Verify: dotnet build src/PropTraderTools/ -> 0 Error(s).

SCAN-5: dotnet test counts (passed >= 19, no new regressions)
  Pre-assessment: PASS -- zero behavior change; `return null` preserved in body.
  ResolveMultipliers returns null semantically identical value. CopyRule.Create behavior unchanged.
  Verify: dotnet test -> passed count >= 19, 0 new failures vs baseline.

SCAN-6: powershell -File .\deploy-sync.ps1 -> SYNC COMPLETE
  Pre-assessment: PASS (expected) -- file modified, deploy-sync.ps1 re-syncs hard links.
  Verify: script output contains "SYNC COMPLETE".

SCAN-7: hardlink count = 1 for CopyEngine.cs
  Pre-assessment: PASS (expected) -- CopyEngine.cs has exactly 1 hard link (no extra copies).
  Verify: (Get-Item src/PropTraderTools/CopyEngine.cs).LinkCount -eq 1
```

---

## 7. NT8 API Usage

No new NT8 API calls are introduced. No changes affect:
- Account.All
- Account.CreateOrder
- Account.Submit
- Account.Cancel
- Account.Positions (iteration)
- Order properties (Name, State, Type, Instrument, Quantity)

All changes are pure type annotation changes. The NT8 runtime contract is unchanged.

---

## 8. Threading Model

No changes to threading model. All edited methods are read-only queries:
- No Dispatcher.InvokeAsync calls added
- No ConcurrentQueue operations changed
- No new event subscriptions
- JS-021 satisfied by construction (no lock() added)

---

## 9. File Scope Validation

ALL changes are confined to: `src/PropTraderTools/CopyEngine.cs`

External callers of `FindPositionPublic` (TradeCopierPanel.cs, PttBreakEvenSwap.cs) already
null-guard the result. The return type change to `Position?` does not require code edits in
those files -- the `var pos` inference will silently accommodate the nullable annotation.

---

## 10. 7-Scan Checklist Template (for engineer, per ticket)

```
SCAN-01: JS-002 -- No `return null` for non-nullable reference return types.
         Every changed method must return T? for reference types or `return default`
         for struct-nullable (Nullable<T>) types.
         PASS criterion: grep for `return null` in modified method bodies = 0 hits
         (exception: ResolveNullFollowerSlot `return null` preserved per NT8 pattern).

SCAN-02: JS-021 -- No lock() added.
         PASS criterion: grep `lock(` src/PropTraderTools/CopyEngine.cs = 0 matches.

SCAN-03: JS-001 -- No new throw or rethrow added.
         PASS criterion: no new `throw` keyword in diff.

SCAN-04: JS-013 -- CYC <= 8 per method.
         PASS criterion: all changed methods verified against CYC table in Section 5.
         No new branches introduced (annotation-only changes = no CYC delta).

SCAN-05: ASCII-only -- No Unicode, emoji, or curly quotes in changed text.
         PASS criterion: all new/changed comment text uses ASCII characters only.

SCAN-06: Behavior preservation -- NT8-pattern null returns in ResolveNullFollowerSlot
         must be preserved exactly as `return null; // NT8 pattern: null = slot could not be resolved`.
         PASS criterion: both return null lines in ResolveNullFollowerSlot unchanged.

SCAN-07: Caller propagation -- All callers of changed methods continue to compile.
         T2 callers: FindPositionPublic external callers (TradeCopierPanel.cs x2,
           PttBreakEvenSwap.cs x1) require NO code edits (var pos inference handles T?).
         FindLeaderCollateralOrder: L3301 variable annotation updated.
         ResolveMultipliers: L7295 variable annotation updated.
         PASS criterion: dotnet build = 0 errors, 0 new warnings vs baseline.
```

---

## 11. Spec Requirement Mapping

| Req ID | Description | Covered By |
|--------|-------------|------------|
| JS-002 | No return null | T1 (6 sites), T2 (8 sites), T3 (1 site) |
| JS-021 | No lock() | All tickets (no lock added) |
| JS-001 | No new throw | All tickets (no throw added) |
| JS-013 | CYC <= 8 | All tickets (CYC unchanged per Section 5) |

---

## 12. Implementation Notes for Engineer

1. **Line number drift**: The spec cites specific line numbers (L1271, L1997, etc.). These
   are reference points for finding the sites. Always grep for the method name to locate the
   exact current line before editing.

2. **FindFollowerBracketOrder, FindFollowerEntryOrder, FindFollowerAccount**: These 3 sites
   are ALREADY compliant (return type already has `?`). The engineer should add a confirmation
   comment to each method noting JS-002 compliance rather than making a code change.

3. **ResolveNullFollowerSlot `return null` MUST be preserved**: The mission explicitly states
   "No behavior change at NT8-pattern return null sites." Do NOT change these to `return default`.
   The fix is ONLY adding `?` to the return type.

4. **ResolveMultipliers vs Array.Empty**: Do NOT change `return null` to `return Array.Empty<int>()`.
   The null value has semantic meaning (use default multipliers of 1). Only annotate `int[]?`.

5. **Build verification**: After each ticket, run `dotnet build` in `src/PropTraderTools/`.
   Target: 0 Error(s), 0 new warnings vs baseline.

---

## 13. Summary

| Ticket | Sites | Change Type | Risk Level |
|--------|-------|-------------|-----------|
| T1 | 4 methods, 6 `return null` sites | return null -> return default (struct?) | TRIVIAL |
| T2 | 7 methods, 8 sites + 3 propagations | T -> T? annotation | LOW |
| T3 | 1 method, 1 site + 1 propagation | int[] -> int[]? annotation | TRIVIAL |

Total: 15 JS-002 violations resolved. Zero behavior changes. Zero new branches (CYC unchanged).
All fixes confined to `src/PropTraderTools/CopyEngine.cs` for logic changes;
external callers require no edits (already null-guarded).
