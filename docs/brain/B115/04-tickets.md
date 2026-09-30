# B115 Tickets — Formalize DW-B119 + DW-B121 + DW-B122 Hotfixes

**Status**: TICKETS_COMPLETE
**Date**: 2026-08-27
**Author**: ptt-architect (Phase 3)
**Block**: B115
**Input**: docs/brain/B115/02-architecture-plan.md (REVIEW_PASS — 26/26 items PASS)

---

## Ticket Index

| Ticket | File | Change Type | DW Reference |
|--------|------|-------------|--------------|
| T1 | `src/PropTraderTools/Tests/B113Tests.cs` | Two-constant update in existing [Fact] | DW-B121 |
| T2 | `src/PropTraderTools/Tests/B115Tests.cs` | New test file — Accepted-state guard | DW-B122 |
| T3 | `src/PropTraderTools/CopyEngine.cs` | Parentheses clarity edit — no logic change | DW-B122 (operator clarity) |

**Production code changed**: T3 only (readability, compiler-equivalent).  
**New files**: T2 only (`B115Tests.cs`).  
**SRC CODE BAN**: These tickets are the engineer's contract. The architect writes no `.cs` files.

---

## T1 — Update T_B113_01 TTL Constants

**Block**: B115  
**Ticket**: T1  
**DW Reference**: DW-B121  
**File**: `src/PropTraderTools/Tests/B113Tests.cs`  
**Method**: `QxPendingFollowerCleanup_SetBeforeExecuteOne_ForFollower` (existing `[Fact]`)

### Spec Requirement IDs

- **DW-B121**: TTL value changed from 2 s to 10 s in `PttGlobalQuickExit.cs` L165
  (`DateTime.UtcNow.AddSeconds(10)`). Test constants must mirror the production value.
- **DW-B119**: TryAdd-before-Execute placement confirmed as already fixed by B114-T1.
  T_B113_01 remains valid coverage for that fix. No structural change needed.

### Context

`T_B113_01` uses `AddSeconds(2)` as the expiry seed and `AddSeconds(3)` as the upper-bound
assertion guard. Both constants were correct when B113 was written. After DW-B121 raised the
production TTL to 10 s, the constants became stale. They do not cause a test failure (an entry
seeded with 2 s is still in the future at assertion time), but they misrepresent the production
value, reducing trust and creating a silent alignment drift.

Source confirmed (plan-review B3 evidence):
- `B113Tests.cs` L32: `var expiry = DateTime.UtcNow.AddSeconds(2);`
- `B113Tests.cs` L42: `Assert.True(entry.Expiry <= DateTime.UtcNow.AddSeconds(3));`

### Exact Changes (Two Lines)

| Location | File Line (approx) | Before | After |
|----------|--------------------|--------|-------|
| Arrange — expiry seed | L32 | `DateTime.UtcNow.AddSeconds(2)` | `DateTime.UtcNow.AddSeconds(10)` |
| Assert — upper-bound guard | L42 | `DateTime.UtcNow.AddSeconds(3)` | `DateTime.UtcNow.AddSeconds(11)` |

**Why `AddSeconds(11)` for the upper bound**: The production TTL seed is 10 s. The upper bound
must exceed 10 s to remain valid across the ~0 ms elapsed between `TryAdd` and `Assert`.
`AddSeconds(10) + 1 s slack = AddSeconds(11)` is a tight but safe bound.

**Line unchanged**: `Assert.True(entry.Expiry > DateTime.UtcNow);` — no change to this assertion.

### Method Signatures

N/A — this ticket edits two constant literals inside an existing `[Fact]`. No signature changes.

### xUnit [Fact] Name

`QxPendingFollowerCleanup_SetBeforeExecuteOne_ForFollower` (existing — not renamed)

### 7-Scan Checklist (SCAN-01 through SCAN-07)

Engineer MUST run all 7 scans and report zero findings for each before marking T1 done.

```
SCAN-01  lock() check
  Command: grep -rn "lock(" src/PropTraderTools/Tests/B113Tests.cs
  Expected: zero results

SCAN-02  async void check
  Command: grep -rn "async void" src/PropTraderTools/Tests/B113Tests.cs
  Expected: zero results

SCAN-03  throw new check
  Command: grep -rn "throw new" src/PropTraderTools/Tests/B113Tests.cs
  Expected: zero results

SCAN-04  return null check
  Command: grep -rn "return null" src/PropTraderTools/Tests/B113Tests.cs
  Expected: zero results

SCAN-05  new byte[] / array allocation check
  Command: grep -rn "new byte\[" src/PropTraderTools/Tests/B113Tests.cs
  Expected: zero results
  Note: Test file; not a hot path. Confirm no byte array inadvertently introduced.

SCAN-06  CYC check
  Confirm: T_B113_01 cyclomatic complexity unchanged at CYC=1.
  Two constant replacements add zero branches. Manual count: no if/for/while/switch added.

SCAN-07  ASCII-only check
  Command: grep -Pn "[^\x00-\x7F]" src/PropTraderTools/Tests/B113Tests.cs
  Expected: zero results (zero non-ASCII bytes in changed file)
```

### Acceptance Criteria

- [ ] `AddSeconds(2)` no longer appears in `QxPendingFollowerCleanup_SetBeforeExecuteOne_ForFollower`
- [ ] `AddSeconds(10)` is the expiry seed in the Arrange block
- [ ] `AddSeconds(11)` is the upper-bound in the Assert block
- [ ] `Assert.True(entry.Expiry > DateTime.UtcNow)` line is unchanged
- [ ] Test passes (xUnit green) after the update
- [ ] All 7 scans: zero findings

---

## T2 — New Test: Accepted-State Guard (B115Tests.cs)

**Block**: B115  
**Ticket**: T2  
**DW Reference**: DW-B122  
**File**: `src/PropTraderTools/Tests/B115Tests.cs` *(new file — does not yet exist)*

### Spec Requirement IDs

- **DW-B122**: Added `&& e.Order.OrderState != OrderState.Accepted` to guard condition (a)
  in `TryCleanupReArmedAtmBracket` (`CopyEngine.cs` L2397-2398). Cleanup now fires on both
  `Working` and `Accepted` states, matching `TryFireFollowerBeRetry` behavior. No test existed
  for the Accepted-state path before this block.

### Context

`TryCleanupReArmedAtmBracket` cannot be invoked directly in tests because it requires a live
`OrderEventArgs` (NT8 sealed class with no public constructor). Instead, T2 uses the
`_qxPendingFollowerCleanup` dict seam (`internal ConcurrentDictionary` on `CopyEngine.Instance`,
accessible via `[assembly: InternalsVisibleTo("PropTraderTools.Tests")]` declared in
`CopyEngine.cs`).

The test validates:
1. The compound state guard sub-expression evaluates correctly for `OrderState.Accepted`.
2. The `_qxPendingFollowerCleanup` dict TryAdd / ContainsKey / TryRemove operations
   used inside `TryCleanupReArmedAtmBracket` behave as expected across tChar paths.

### Method Signatures

New `[Fact]` methods in `B115Tests`:

```csharp
// Primary — mandatory
public void TryCleanupReArmedAtmBracket_GuardAcceptsAcceptedState()

// Optional — secondary
public void TryCleanupReArmedAtmBracket_GuardRejectsUnknownState()
```

Both return `void`. No parameters. No `async`. No `lock`. CYC = 1 each (linear assertions).

### File Header (required)

```csharp
// B115Tests.cs -- DW-B122 Accepted-state guard tests
// Block: B115. Framework: xUnit [Fact] only. JS-021: no lock. JS-033: no async void.
// Seam: _qxPendingFollowerCleanup (internal ConcurrentDictionary, InternalsVisibleTo).
```

### Required Usings

```csharp
using System;
using System.Collections.Concurrent;
using NinjaTrader.Cbi;
using Xunit;
```

### Test Design — `TryCleanupReArmedAtmBracket_GuardAcceptsAcceptedState`

**Purpose**: Prove that the compound state guard `(state != Working && state != Accepted)` evaluates
to `false` when `state == OrderState.Accepted`, meaning the guard does NOT early-return — cleanup
proceeds for Accepted-state orders (the DW-B122 fix intent).

**Guard expression from `CopyEngine.cs` L2396-2398** (verbatim):
```csharp
e.Order.OrderState != OrderState.Working
&& e.Order.OrderState != OrderState.Accepted
```

**Inline evaluation for Accepted**:
```
(OrderState.Accepted != OrderState.Working)   -> true
(OrderState.Accepted != OrderState.Accepted)  -> false
true && false = false
```
`false` means the compound state check does NOT short-circuit the guard chain via the state leg.
Cleanup is NOT skipped for Accepted orders. This is the correct DW-B122 behavior.

**Test body structure**:
```
Arrange: compute the guard sub-expression value for Accepted state
Act:     evaluate (state != Working) && (state != Accepted) where state = OrderState.Accepted
Assert:  Assert.False(result)  -- guard does NOT fire early for Accepted
```

**Implementation sketch** (for engineer reference — engineer writes the actual code):
```csharp
[Fact]
public void TryCleanupReArmedAtmBracket_GuardAcceptsAcceptedState()
{
    // The compound state guard from TryCleanupReArmedAtmBracket (CopyEngine.cs L2397-2398):
    //   e.Order.OrderState != OrderState.Working
    //   && e.Order.OrderState != OrderState.Accepted
    // When state == Accepted: (true && false) == false -> guard does NOT return early.
    var state = OrderState.Accepted;
    bool guardFires = state != OrderState.Working && state != OrderState.Accepted;
    Assert.False(guardFires); // DW-B122: Accepted passes through state guard
}
```

### Test Design — `TryCleanupReArmedAtmBracket_GuardRejectsUnknownState` (optional)

**Purpose**: Prove that the compound state guard evaluates to `true` for a state that is neither
`Working` nor `Accepted` (e.g., `OrderState.Cancelled`), meaning the guard fires early — cleanup
is correctly skipped for orders in irrelevant states.

**Inline evaluation for Cancelled**:
```
(OrderState.Cancelled != OrderState.Working)   -> true
(OrderState.Cancelled != OrderState.Accepted)  -> true
true && true = true
```
`true` means early return fires. Cleanup correctly skipped.

**Implementation sketch**:
```csharp
[Fact]
public void TryCleanupReArmedAtmBracket_GuardRejectsUnknownState()
{
    var state = OrderState.Cancelled;
    bool guardFires = state != OrderState.Working && state != OrderState.Accepted;
    Assert.True(guardFires); // non-Working, non-Accepted -> guard returns early
}
```

### Dict Seam Tests (include in B115Tests, same class)

The engineer SHOULD also include the following dict seam assertions to cover the tChar removal
policy paths (mirrors architecture plan §5 T2 items 2 and 3):

**T1-equivalent path** (tChar = '1', non-expired): entry survives.
```csharp
// Arrange: seed with non-expired entry, simulate T1 (tChar='1') removal decision
// shouldRemove = ('1' == '3') || (expiry <= UtcNow) = false || false = false
// Assert: entry still in dict (ContainsKey returns true)
```

**T3-equivalent path** (tChar = '3'): entry removed.
```csharp
// Arrange: seed with non-expired entry, simulate T3 (tChar='3') removal decision
// shouldRemove = ('3' == '3') || (expiry <= UtcNow) = true
// Act: TryRemove(accName, out _)
// Assert: entry absent from dict (ContainsKey returns false)
```

Always call `engine._qxPendingFollowerCleanup.Clear()` at the start of each test to isolate
from prior test state (same pattern as B113Tests.cs).

### xUnit [Fact] Names

- `TryCleanupReArmedAtmBracket_GuardAcceptsAcceptedState` (mandatory)
- `TryCleanupReArmedAtmBracket_GuardRejectsUnknownState` (optional, recommended)

### 7-Scan Checklist (SCAN-01 through SCAN-07)

Engineer MUST run all 7 scans and report zero findings for each before marking T2 done.

```
SCAN-01  lock() check
  Command: grep -rn "lock(" src/PropTraderTools/Tests/B115Tests.cs
  Expected: zero results
  Note: ConcurrentDictionary operations require no lock(). JS-021 prohibits lock().

SCAN-02  async void check
  Command: grep -rn "async void" src/PropTraderTools/Tests/B115Tests.cs
  Expected: zero results
  Note: All [Fact] methods are synchronous void. No async needed.

SCAN-03  throw new check
  Command: grep -rn "throw new" src/PropTraderTools/Tests/B115Tests.cs
  Expected: zero results
  Note: Tests use Assert.* only. No throw statements.

SCAN-04  return null check
  Command: grep -rn "return null" src/PropTraderTools/Tests/B115Tests.cs
  Expected: zero results
  Note: All test methods return void; no return statements with values.

SCAN-05  new byte[] / array allocation check
  Command: grep -rn "new byte\[" src/PropTraderTools/Tests/B115Tests.cs
  Expected: zero results
  Note: No byte array needed in test setup.

SCAN-06  CYC check
  Confirm: every [Fact] method CYC = 1 (linear assertions only, no branches).
  Manual count: no if/for/while/switch/ternary in test bodies.
  Tool (optional): python scripts/complexity_audit.py src/PropTraderTools/Tests/B115Tests.cs

SCAN-07  ASCII-only check
  Command: grep -Pn "[^\x00-\x7F]" src/PropTraderTools/Tests/B115Tests.cs
  Expected: zero results (zero non-ASCII bytes in changed file)
```

### Acceptance Criteria

- [ ] File `src/PropTraderTools/Tests/B115Tests.cs` created
- [ ] Namespace: `PropTraderTools.Tests`
- [ ] Class: `B115Tests`
- [ ] Framework: xUnit `[Fact]` only — no `[Theory]`, no NUnit, no MSTest
- [ ] `TryCleanupReArmedAtmBracket_GuardAcceptsAcceptedState` present and passes (green)
- [ ] Test asserts `Assert.False(guardFires)` for `OrderState.Accepted`
- [ ] `CopyEngine.Instance._qxPendingFollowerCleanup.Clear()` called at start of dict-seam tests
- [ ] All 7 scans: zero findings

---

## T3 — Parentheses Clarity Edit in TryCleanupReArmedAtmBracket

**Block**: B115  
**Ticket**: T3  
**DW Reference**: DW-B122 (operator precedence clarity confirmation)  
**File**: `src/PropTraderTools/CopyEngine.cs`  
**Method**: `TryCleanupReArmedAtmBracket`  
**Lines affected**: L2396-2408 (guard block; two lines wrapped)

### Spec Requirement IDs

- **DW-B122** (clarity aspect): The operator precedence proof in plan §3 confirms the unparenthesized
  form is semantically correct. T3 adds explicit parentheses to anchor the compound state sub-expression
  visually for future readers. Stated as INCLUDED in plan §3 Clarity Verdict and confirmed PASS in
  plan-review C3/C4.

### Context

Current guard (CopyEngine.cs L2396-2408, confirmed by plan-review source read 2026-08-27):

```csharp
if (
    e.Order.OrderState != OrderState.Working
    && e.Order.OrderState != OrderState.Accepted
    || e.Order.Name == null
    || !e.Order.Name.StartsWith("PTT-QX-T", StringComparison.Ordinal)
    || e.Order.Name.Length < 9
    || !char.IsDigit(e.Order.Name[8])
    || e.Order.Account == null
    || !IsFollowerAccount(e.Order.Account)
    || !_qxPendingFollowerCleanup.TryGetValue(e.Order.Account.Name, out var entry)
    || entry.Expiry <= DateTime.UtcNow
    || entry.Instr?.FullName != e.Order.Instrument?.FullName
)
    return;
```

After T3 (explicit parentheses added around the compound state check):

```csharp
if (
    (   e.Order.OrderState != OrderState.Working
     && e.Order.OrderState != OrderState.Accepted)   // DW-B122
    || e.Order.Name == null
    || !e.Order.Name.StartsWith("PTT-QX-T", StringComparison.Ordinal)
    || e.Order.Name.Length < 9
    || !char.IsDigit(e.Order.Name[8])
    || e.Order.Account == null
    || !IsFollowerAccount(e.Order.Account)
    || !_qxPendingFollowerCleanup.TryGetValue(e.Order.Account.Name, out var entry)
    || entry.Expiry <= DateTime.UtcNow
    || entry.Instr?.FullName != e.Order.Instrument?.FullName
)
    return;
```

### Exact Change Description

1. On the line containing `e.Order.OrderState != OrderState.Working`:
   - Add `(` before `e.Order.OrderState`
   - Align opening paren per project style (see After block above — indent 4 spaces + `(   `)

2. On the line containing `&& e.Order.OrderState != OrderState.Accepted`:
   - Add `)` after `OrderState.Accepted` (before any comment)
   - Preserve the `// DW-B122` inline comment (or update to `// DW-B122: Accepted passes guard`)

**Behavior change**: None. C# ECMA-334 §12.4.2 natural `&&`-before-`||` precedence already
produces identical evaluation. The parentheses are cosmetic only.

**CYC impact**: None. `TryCleanupReArmedAtmBracket` CYC stays at 5 (confirmed by `// CYC=5`
annotation at L2383). Parentheses do not add branches.

**Comment at L2383**: The `// CYC=5: (1) outer guard, (2) foreach, (3) if found, (4) if shouldRemove.`
annotation must remain intact. Do not modify it.

**Comment block L2388-2394**: The `// (1) Compound guard -- all conditions must be true.` block
(including sub-items a–f) must remain intact. The existing `// a. Order just went Working or Accepted`
sub-comment already explains the Accepted-state intent correctly. No wording change needed there.

### Method Signatures

`TryCleanupReArmedAtmBracket` signature is unchanged:

```csharp
internal void TryCleanupReArmedAtmBracket(OrderEventArgs e)
```

### xUnit [Fact] Name

N/A — no new tests for T3. Existing tests `T_B113_01` through `T_B113_04` in `B113Tests.cs`
continue to cover `TryCleanupReArmedAtmBracket` behavior. The parentheses change is
compiler-equivalent and requires no additional test coverage.

### 7-Scan Checklist (SCAN-01 through SCAN-07)

Engineer MUST run all 7 scans and report zero findings for each before marking T3 done.

```
SCAN-01  lock() check scoped to method
  Command: grep -n "lock(" src/PropTraderTools/CopyEngine.cs
  Expected: zero results in TryCleanupReArmedAtmBracket (method spans L2386-2450).
  Note: Parentheses change introduces no lock(). ConcurrentDictionary is the pattern.

SCAN-02  async void check
  Command: grep -rn "async void" src/PropTraderTools/CopyEngine.cs
  Expected: zero new results introduced by T3 edit.
  Note: TryCleanupReArmedAtmBracket is synchronous void. No change to signature.

SCAN-03  throw new check
  Command: grep -n "throw new" src/PropTraderTools/CopyEngine.cs
  Expected: zero new results in the guard block (L2396-2408). Parentheses only.

SCAN-04  return null check
  Command: grep -n "return null" src/PropTraderTools/CopyEngine.cs
  Expected: existing return; at L2409 is not a return-null. Zero new return null introduced.

SCAN-05  new byte[] / array allocation check
  Command: grep -n "new byte\[" src/PropTraderTools/CopyEngine.cs
  Expected: zero results in guard block. Parentheses introduce no allocation.

SCAN-06  CYC check
  Confirm: CYC annotation at L2383 still reads:
    // CYC=5: (1) outer guard, (2) foreach, (3) if found, (4) if shouldRemove.
  Confirm: TryCleanupReArmedAtmBracket CYC = 5 (unchanged).
  Tool (optional): python scripts/complexity_audit.py src/PropTraderTools/CopyEngine.cs

SCAN-07  ASCII-only check
  Command: grep -Pn "[^\x00-\x7F]" src/PropTraderTools/CopyEngine.cs
  Expected: zero results (zero non-ASCII bytes in changed file)
```

### Acceptance Criteria

- [ ] `CopyEngine.cs` compiles without errors after T3 edit
- [ ] `TryCleanupReArmedAtmBracket` CYC annotation at L2383 still reads `// CYC=5`
- [ ] Opening `(` wraps exactly the `e.Order.OrderState != OrderState.Working` line
- [ ] Closing `)` appears after `&& e.Order.OrderState != OrderState.Accepted` on the same line
- [ ] `// DW-B122` inline comment preserved (or updated to `// DW-B122: Accepted passes guard`)
- [ ] Comment block L2388-2394 (sub-items a–f) is intact and unmodified
- [ ] No logic change — identical IL output to pre-T3 state
- [ ] All 7 scans: zero findings
- [ ] `powershell -File scripts\ptt-sync-and-verify.ps1` passes (0 MISMATCH lines) after T3 sync

---

## Post-Implementation Gate

After all three tickets are marked done by the engineer, the following gate must pass:

```powershell
# 1. Build
dotnet build src/PropTraderTools/ --no-restore

# 2. Tests
dotnet test src/PropTraderTools/ --no-build

# 3. Sync to NT8 and MD5-verify
powershell -File scripts\ptt-sync-and-verify.ps1

# 4. P0 scans (must return zero across all modified files)
grep -rn "lock(" src/PropTraderTools/Tests/B113Tests.cs
grep -rn "lock(" src/PropTraderTools/Tests/B115Tests.cs
grep -rn "lock(" src/PropTraderTools/CopyEngine.cs | grep -A5 "TryCleanupReArmedAtmBracket"
```

5. Press **F5** in NinjaTrader 8 to recompile. Green = ready to merge.

---

*End of B115 Tickets*
