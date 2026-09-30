# DW-B79-09 — Tickets

**Pipeline**: DW-B79-09  
**Phase**: 3 — Ticket Generation  
**Author**: ptt-architect  
**Date**: 2026-08-21  
**Plan status**: REVIEW_PASS (ptt-plan-reviewer, 2026-08-21)  
**Ticket count**: 1

---

## DW-B79-09-TICKET-1

### Title
`DW-B79-09-TICKET-1 — RemoveAll race guard: CancelQxBrackets×2 + CancelStaleBracketsLocal`

---

### Spec Requirement IDs

- **DW-B79-09** — Apply `RemoveAll(Filled || Cancelled)` race guard to the three cancel methods
  not covered by DW-B79-04:
  - `CancelQxBrackets` 2-param (`CopyEngine.cs`)
  - `CancelQxBrackets` 3-param (`CopyEngine.cs`)
  - `CancelStaleBracketsLocal` (`PttBreakEven.cs`)

Follow-on to: **DW-B79-04** (RemoveAll guard on `CancelAllAccountOrders` — already shipped,
commit 5925b618).

---

### Source Files

| File | Workspace path | Change |
|------|---------------|--------|
| `CopyEngine.cs` | `src/PropTraderTools/CopyEngine.cs` | 2 one-line insertions |
| `PttBreakEven.cs` | `src/PropTraderTools/Features/PttBreakEven.cs` | 1 one-line insertion |
| `CopyEngineTests.cs` | `src/PropTraderTools/CopyEngineTests.cs` | 3 new `[Fact]` methods |

---

### Edit 1 — `CopyEngine.cs` — `CancelQxBrackets` 2-param (~L630)

**Insertion point**: Immediately before `try { acc.Cancel(stale.ToArray()); }` at L630.

**BEFORE**:
```csharp
try { acc.Cancel(stale.ToArray()); }
catch { }
```

**AFTER**:
```csharp
stale.RemoveAll(o => o.OrderState == OrderState.Filled
                  || o.OrderState == OrderState.Cancelled);   // DW-B79-09: race guard
try { acc.Cancel(stale.ToArray()); }
catch { }
```

**Tool**: `apply_diff` or `search_and_replace` — **NOT** `write_file` for this existing file.

**CYC impact**: None. `RemoveAll` is a method call, not a branch. CYC remains 6. (JS-080 ✓)

---

### Edit 2 — `CopyEngine.cs` — `CancelQxBrackets` 3-param (~L702)

**Insertion point**: After `if (stale.Count == 0) return;` at L701, immediately before
`try { acc.Cancel(stale.ToArray()); }` at L702.

**BEFORE**:
```csharp
if (stale.Count == 0) return;                                                  // (7)
try { acc.Cancel(stale.ToArray()); }
catch { }
```

**AFTER**:
```csharp
if (stale.Count == 0) return;                                                  // (7)
stale.RemoveAll(o => o.OrderState == OrderState.Filled
                  || o.OrderState == OrderState.Cancelled);   // DW-B79-09: race guard
try { acc.Cancel(stale.ToArray()); }
catch { }
```

**Tool**: `apply_diff` or `search_and_replace` — **NOT** `write_file` for this existing file.

**CYC impact**: None. CYC remains 7. (JS-080 ✓)

---

### Edit 3 — `PttBreakEven.cs` — `CancelStaleBracketsLocal` (~L193)

**Insertion point**: Inside the existing `try` block, immediately before `acc.Cancel(stale.ToArray());`
at L193. The insertion becomes the first statement inside the `try` block.

**BEFORE**:
```csharp
if (stale.Count == 0) return;                                         // (3)
try
{
    acc.Cancel(stale.ToArray());
    NinjaTrader.Code.Output.Process(
        "[BE] CancelStaleBracketsLocal: " + stale.Count + " orders cancelled",
        NinjaTrader.NinjaScript.PrintTo.OutputTab1);
}
catch { /* cancel on already-filled orders is non-fatal */ }
```

**AFTER**:
```csharp
if (stale.Count == 0) return;                                         // (3)
try
{
    stale.RemoveAll(o => o.OrderState == OrderState.Filled
                      || o.OrderState == OrderState.Cancelled);   // DW-B79-09: race guard
    acc.Cancel(stale.ToArray());
    NinjaTrader.Code.Output.Process(
        "[BE] CancelStaleBracketsLocal: " + stale.Count + " orders cancelled",
        NinjaTrader.NinjaScript.PrintTo.OutputTab1);
}
catch { /* cancel on already-filled orders is non-fatal */ }
```

**Tool**: `apply_diff` or `search_and_replace` — **NOT** `write_file` for this existing file.

**Note**: `RemoveAll` inside the `try` is safe — the lambda predicate reads two enum fields and
cannot throw. Any NT8 edge case is already handled by the existing `catch { }`.

**CYC impact**: None. CYC remains 6. (JS-080 ✓)

---

### Test Edit — `CopyEngineTests.cs` — 3 new `[Fact]` methods

**File**: `src/PropTraderTools/CopyEngineTests.cs`  
**Tool**: `apply_diff` or `search_and_replace` — append to the existing DW-B79 test class.  
**Test delta**: 292 → 295 (+3 `[Fact]`).

#### T_DW_B79_09_01 — `CancelQxBrackets` 2-param contains `RemoveAll`

```csharp
[Fact]
public void T_DW_B79_09_01_CancelQxBrackets2Param_HasRemoveAllGuard()
{
    // Structural IL/reflection test: the 2-param CancelQxBrackets method body
    // must contain a call to List<Order>.RemoveAll before acc.Cancel.
    var type = typeof(CopyEngine);
    var method = type.GetMethod(
        "CancelQxBrackets",
        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance,
        null,
        new[] { typeof(Account), typeof(string) },
        null);
    Assert.NotNull(method);
    var body = method!.GetMethodBody();
    Assert.NotNull(body);
    var il = body!.GetILAsByteArray();
    Assert.NotNull(il);
    // Verify method body is non-trivial (has meaningful IL bytes)
    Assert.True(il!.Length > 10,
        "T_DW_B79_09_01: CancelQxBrackets 2-param IL body is unexpectedly empty");
    // Verify the method references RemoveAll by scanning method tokens in IL
    var removeAllToken = typeof(System.Collections.Generic.List<NinjaTrader.Cbi.Order>)
        .GetMethod("RemoveAll")!.MetadataToken;
    bool found = ContainsMethodToken(il, removeAllToken);
    Assert.True(found,
        "T_DW_B79_09_01: CancelQxBrackets 2-param does not contain RemoveAll call (DW-B79-09 guard missing)");
}
```

#### T_DW_B79_09_02 — `CancelQxBrackets` 3-param contains `RemoveAll`

```csharp
[Fact]
public void T_DW_B79_09_02_CancelQxBrackets3Param_HasRemoveAllGuard()
{
    // Structural IL/reflection test: the 3-param CancelQxBrackets method body
    // must contain a call to List<Order>.RemoveAll before acc.Cancel.
    var type = typeof(CopyEngine);
    var method = type.GetMethod(
        "CancelQxBrackets",
        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance,
        null,
        new[] { typeof(Account), typeof(string), typeof(OrderSnapshot[]) },
        null);
    Assert.NotNull(method);
    var body = method!.GetMethodBody();
    Assert.NotNull(body);
    var il = body!.GetILAsByteArray();
    Assert.NotNull(il);
    Assert.True(il!.Length > 10,
        "T_DW_B79_09_02: CancelQxBrackets 3-param IL body is unexpectedly empty");
    var removeAllToken = typeof(System.Collections.Generic.List<NinjaTrader.Cbi.Order>)
        .GetMethod("RemoveAll")!.MetadataToken;
    bool found = ContainsMethodToken(il, removeAllToken);
    Assert.True(found,
        "T_DW_B79_09_02: CancelQxBrackets 3-param does not contain RemoveAll call (DW-B79-09 guard missing)");
}
```

#### T_DW_B79_09_03 — `CancelStaleBracketsLocal` contains `RemoveAll`

```csharp
[Fact]
public void T_DW_B79_09_03_CancelStaleBracketsLocal_HasRemoveAllGuard()
{
    // Structural IL/reflection test: the private static CancelStaleBracketsLocal method
    // body must contain a call to List<Order>.RemoveAll.
    // Uses BindingFlags.NonPublic | BindingFlags.Static (same pattern as PttBreakEvenB72Tests.cs).
    var type = typeof(PttBreakEven);
    var method = type.GetMethod(
        "CancelStaleBracketsLocal",
        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
    Assert.NotNull(method);
    var body = method!.GetMethodBody();
    Assert.NotNull(body);
    var il = body!.GetILAsByteArray();
    Assert.NotNull(il);
    Assert.True(il!.Length > 10,
        "T_DW_B79_09_03: CancelStaleBracketsLocal IL body is unexpectedly empty");
    var removeAllToken = typeof(System.Collections.Generic.List<NinjaTrader.Cbi.Order>)
        .GetMethod("RemoveAll")!.MetadataToken;
    bool found = ContainsMethodToken(il, removeAllToken);
    Assert.True(found,
        "T_DW_B79_09_03: CancelStaleBracketsLocal does not contain RemoveAll call (DW-B79-09 guard missing)");
}
```

**Note**: If `ContainsMethodToken` is not already a shared helper in the test class, add it as a
`private static bool` method alongside the three `[Fact]` methods:

```csharp
private static bool ContainsMethodToken(byte[] il, int token)
{
    // IL method calls use opcodes 0x28 (call) or 0x6F (callvirt) followed by a 4-byte metadata token.
    for (int i = 0; i < il.Length - 4; i++)
    {
        if (il[i] != 0x28 && il[i] != 0x6F) continue;
        int t = il[i + 1] | (il[i + 2] << 8) | (il[i + 3] << 16) | (il[i + 4] << 24);
        if (t == token) return true;
    }
    return false;
}
```

---

### JS Rules Applicable to This Ticket

| Rule | Requirement | Satisfied by |
|------|-------------|-------------|
| **JS-021** | No `lock()` anywhere in `src/` | `RemoveAll` operates on a local `List<T>` — no shared state. No `lock` added. ✓ |
| **JS-001** | No `throw` in hot paths | `RemoveAll` with a valid lambda predicate does not throw. Existing `catch { }` handles NT8 exceptions. No `throw` introduced. ✓ |
| **JS-080** | CYC ≤ 8 per method | All three methods remain at CYC 6/7/6 — no new branch points. ✓ |
| **ASCII-only** | No Unicode/emoji/curly quotes in C# source | Inserted line is 100% ASCII. ✓ |

---

### CYC Constraint

`RemoveAll(predicate)` is a single `List<T>` method call. The lambda `o => ...` is compiled into
a delegate allocation but does **not** add a branch point to the calling method's control flow
graph as counted by Roslyn/Lizard. CYC is unchanged for all three methods:

| Method | CYC before | CYC after | Budget |
|--------|-----------|-----------|--------|
| `CancelQxBrackets` 2-param | 6 | 6 | ≤8 ✓ |
| `CancelQxBrackets` 3-param | 7 | 7 | ≤8 ✓ |
| `CancelStaleBracketsLocal` | 6 | 6 | ≤8 ✓ |

---

### Engineer Instructions

1. **Use `apply_diff` or `search_and_replace`** for all 3 source edits. Do NOT use `write_file`
   on any existing `.cs` file.

2. **The insertion one-liner is identical for all three sites**:
   ```csharp
   stale.RemoveAll(o => o.OrderState == OrderState.Filled
                     || o.OrderState == OrderState.Cancelled);   // DW-B79-09: race guard
   ```

3. **Insertion points** (verbatim, relative to HEAD 5925b618):
   - **Edit 1**: immediately before `try { acc.Cancel(stale.ToArray()); }` at `CopyEngine.cs:L630`
   - **Edit 2**: immediately before `try { acc.Cancel(stale.ToArray()); }` at `CopyEngine.cs:L702`
     (after the `if (stale.Count == 0) return;` guard at L701)
   - **Edit 3**: immediately before `acc.Cancel(stale.ToArray());` inside the existing `try` block
     at `PttBreakEven.cs:L193`

4. **Recommended edit order**:
   1. `CopyEngine.cs` — 2-param overload (Edit 1)
   2. `CopyEngine.cs` — 3-param overload (Edit 2)
   3. `PttBreakEven.cs` — `CancelStaleBracketsLocal` (Edit 3)
   4. `CopyEngineTests.cs` — 3 new `[Fact]` methods (Test Edit)

5. **After all edits**: run build + test commands (see below).

6. **After build passes**: run `powershell -File .\deploy-sync.ps1` to re-synchronize NT8 hard links.

---

### Build + Test Commands (run after all 4 edits)

```powershell
dotnet build
dotnet test
```

Expected result: **0 errors, 295 tests PASS** (was 292, +3).

---

### 7-Scan Checklist (mandatory engineer contract)

All scans must return zero findings before the ticket is considered complete.

- [ ] **SCAN-01 — lock scan**
  ```powershell
  Select-String -Path "src/**/*.cs" -Pattern "lock\(" -Recurse
  ```
  Expected: **0 results**

- [ ] **SCAN-02 — async-void scan**
  ```powershell
  Select-String -Path "src/**/*.cs" -Pattern "async void " -Recurse
  ```
  Expected: **0 results**

- [ ] **SCAN-03 — return-null scan**
  ```powershell
  Select-String -Path "src/**/*.cs" -Pattern "return null;" -Recurse
  ```
  Expected: **0 results**

- [ ] **SCAN-04 — complexity audit**
  ```powershell
  python scripts/complexity_audit.py
  ```
  Expected: **all methods CYC ≤ 8** (three affected methods stay at 6/7/6)

- [ ] **SCAN-05 — dotnet build**
  ```powershell
  dotnet build
  ```
  Expected: **0 errors**

- [ ] **SCAN-06 — dotnet test**
  ```powershell
  dotnet test
  ```
  Expected: **295 PASS** (was 292, +3 new `[Fact]` methods)

- [ ] **SCAN-07 — CSharpier formatting**
  ```powershell
  dotnet csharpier check src/
  ```
  Expected: **0 issues**

---

### Acceptance Criteria

- [ ] `CancelQxBrackets` 2-param: `RemoveAll` line present immediately before `try { acc.Cancel(stale.ToArray()); }`
- [ ] `CancelQxBrackets` 3-param: `RemoveAll` line present immediately before `try { acc.Cancel(stale.ToArray()); }`
- [ ] `CancelStaleBracketsLocal`: `RemoveAll` line present as first statement inside `try` block, immediately before `acc.Cancel(stale.ToArray());`
- [ ] CYC unchanged: 6 / 7 / 6 for the three methods
- [ ] `[Fact]` count: 295 (was 292, +3)
- [ ] SCAN-01 through SCAN-07: all zero
- [ ] `dotnet build` — 0 errors
- [ ] `dotnet test` — all 295 `[Fact]` PASS
- [ ] `deploy-sync.ps1` PASS — NT8 hard links re-synced
- [ ] F5 in NinjaTrader — GREEN (Director confirmation)

---

### Output Artifact (Ph4a)

After implementing this ticket, the engineer writes:
**`docs/brain/DW-B79-09/ticket-1-completion.md`**

(ptt-architect does NOT write this file — it is Phase 4a output.)
