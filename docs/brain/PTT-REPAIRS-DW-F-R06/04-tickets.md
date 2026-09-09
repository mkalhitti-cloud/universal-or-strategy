# PTT-REPAIRS-DW-F-R06 — Tickets
**Epic**: PTT-REPAIRS-DW-F-R06
**Pipeline**: SINGLE (no lanes)
**Sequence**: T1 THEN T2 (sequential — T1 must reach BUILD_PASS before T2 starts)
**Source plan**: docs/brain/PTT-REPAIRS-DW-F-R06/02-architecture-plan.md (REVIEW_PASS)

---

## T1 — CopyEngine.cs Comment Corrections (F1 + F2)

### Scope Lock
Touch ONLY the two comment lines identified below in
`src/PropTraderTools/CopyEngine.cs`. No code logic change. No other file.

### Spec Requirements Addressed
- F1: CYC annotation on `SetCloneAtmObjectCache` corrected from CYC=1 to CYC=2
- F2: CYC annotation on `GetCloneAtmMode` corrected from CYC=2 to CYC=4

### File
`src/PropTraderTools/CopyEngine.cs`

---

### F1 — Line 727 (doc-only)

**BEFORE (exact text at line 727):**
```
// PTT-REPAIRS-04 BUG-F: SetCloneAtmObjectCache -- per-instrument. CYC=1.
```

**AFTER (exact replacement):**
```
// PTT-REPAIRS-04 BUG-F: SetCloneAtmObjectCache -- per-instrument. CYC=2.
```

Justification: `SetCloneAtmObjectCache` (lines 730-736) has one `if/else` decision.
McCabe: base(1) + if-branch(1) = CYC=2. The original "CYC=1" was incorrect. Zero logic change.

---

### F2 — Line 738 (doc-only)

**BEFORE (exact text at line 738):**
```
// PTT-REPAIRS-04 BUG-F: GetCloneAtmMode -- per-instrument. CYC=2.
```

**AFTER (exact replacement):**
```
// PTT-REPAIRS-04 BUG-F: GetCloneAtmMode -- per-instrument. CYC=4.
```

Justification: `GetCloneAtmMode` (lines 741-753) has base(1) + compound-AND at line 744 (+1)
+ ternary at line 746 (+1) + compound-AND at line 750 (+1) = CYC=4. The original "CYC=2"
was incorrect. Zero logic change.

---

### Method Signatures (unchanged — context only)

```csharp
// Line 730 — signature NOT changed:
internal void SetCloneAtmObjectCache(string instrFullName, NinjaTrader.NinjaScript.AtmStrategy atmObj)

// Line 741 — signature NOT changed:
internal FollowerAtmMode GetCloneAtmMode(string instrFullName)
```

### JS Rule Constraints

| Rule   | Requirement                   | Status |
|--------|-------------------------------|--------|
| JS-021 | No lock() in changed code     | PASS — comment-only edits, no lock() |
| JS-042 | ASCII-only in all changed text | PASS — only ASCII digits and punctuation changed |
| JS-013 | New helpers must be CYC<=1    | N/A — no new helpers introduced |

### Behavior Change
**NONE.** Comment-only edits. No runtime behavior altered.

---

### 7-Scan Checklist — T1

**SCAN-1** — Lock-free verification:
```powershell
grep -r "lock(" src/PropTraderTools/CopyEngine.cs
```
Expected: **0 matches**

**SCAN-2** — ASCII-only verification:
```powershell
grep -rP "[^\x00-\x7F]" src/PropTraderTools/CopyEngine.cs
```
Expected: **0 matches** in CopyEngine.cs

**SCAN-3** — Build error line count:
```powershell
dotnet build 2>&1 | grep " error "
```
Expected: **0 matching lines**

**SCAN-4** — Build error summary:
```powershell
dotnet build 2>&1 | Select-String "Error\(s\)"
```
Expected: output contains **"0 Error(s)"**

**SCAN-5** — Test baseline (no regression):
```powershell
dotnet test --filter "FullyQualifiedName~CopyEngineTests" 2>&1
```
Expected: **passed >= 19** (T1 adds no new tests; baseline is 19 passed / 449 failed / 31 skipped)
No new genuine regressions permitted.

**SCAN-6** — Hard-link sync:
```powershell
powershell -File .\deploy-sync.ps1
```
Expected: output contains **"SYNC COMPLETE"**

**SCAN-7** — Hard-link integrity:
```powershell
(Get-Item src\PropTraderTools\CopyEngine.cs).LinkType
```
Expected: **"HardLink"** (or hardlink count = 1)

---

### Return Status
`BUILD_PASS` if all 7 scans pass.
`BUILD_FAIL` if any scan fails — do NOT proceed to T2.

---

---

## T2 — CopyEngineTests.cs New [Fact] Test (F3)

### Scope Lock
Touch ONLY `src/PropTraderTools/CopyEngineTests.cs`.
Insert exactly the test method specified below. No production code change.
No other file.

**Prerequisite**: T1 must have returned `BUILD_PASS` before this ticket starts.

### Spec Requirements Addressed
- F3: New `[Fact]` test `EvictDedup_CancelledEntry_ClearsLastLeaderDirection` that exercises
  the `EvictDedup` cancelled-order path and asserts `_lastLeaderDirection` is cleared.

### File
`src/PropTraderTools/CopyEngineTests.cs`

---

### Insert Position

Insert the new test method after the last test body closing brace (approximately line 8036),
immediately before the two closing braces that end the test class and namespace.

Pattern to locate insertion point:
- Find the final `}` that closes the last existing test method body.
- Insert the new method block after that `}` and before the class-closing `}`.

---

### Method Signature

```csharp
[Fact]
public void EvictDedup_CancelledEntry_ClearsLastLeaderDirection()
```

---

### Full Test Body to Insert

```csharp
// PTT-REPAIRS-DW-F-R06 F3: verify EvictDedup clears _lastLeaderDirection on Cancelled.
// Covers PTT-REPAIRS-04 BUG-E path: cancelled entry order removes stale direction record.
// Uses InternalsVisibleTo seams declared at CopyEngine.cs:46.
// No NT8 type construction required -- seams operate on string keys and OrderState enum.
[Fact]
public void EvictDedup_CancelledEntry_ClearsLastLeaderDirection()
{
    // Arrange: record a leader direction for the instrument
    _engine.SetLeaderDirection_ForTest("MGC DEC26", OrderAction.Buy);

    // Arrange: simulate a dispatched entry (sets _liveEntryInstruments and _entryInstrKeyByOrderId)
    _engine.IsLiveEntryBlocked_ForTest("MGC DEC26|Buy", "ord-1", 0.0);

    // Act: order is cancelled -- EvictDedup must clear _lastLeaderDirection["MGC DEC26"]
    _engine.EvictDedup_ForTest("ord-1", NinjaTrader.Cbi.OrderState.Cancelled);

    // Assert: direction record must be cleared so next entry is not reversal-blocked
    Assert.False(_engine.HasLeaderDirection("MGC DEC26"));
}
```

---

### Helper Reference (all confirmed in CopyEngine.cs via architecture plan)

| Helper | CopyEngine.cs Line | Signature |
|--------|--------------------|-----------|
| `SetLeaderDirection_ForTest` | 4333 | `internal void SetLeaderDirection_ForTest(string instrFullName, OrderAction action)` |
| `IsLiveEntryBlocked_ForTest` | 4348 | `internal bool IsLiveEntryBlocked_ForTest(string instrKey, string orderId, double limitPrice)` |
| `EvictDedup_ForTest` | 4360 | `internal void EvictDedup_ForTest(string orderId, NinjaTrader.Cbi.OrderState state)` |
| `HasLeaderDirection` | 4330 | `internal bool HasLeaderDirection(string instrFullName)` |

**DISAMBIGUATION**: Use `HasLeaderDirection` (line 4330). There is NO `HasLeaderDirection_ForTest`
method. Any reference to `HasLeaderDirection_ForTest` is incorrect and will cause a compile error.

**InternalsVisibleTo**: Confirmed at `CopyEngine.cs:46`:
```csharp
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("PropTraderTools.Tests")]
```
All four helpers are `internal` and accessible to the test project via this attribute.

---

### NT8 Runtime Fallback

If `TypeInitializationException` occurs at test time (CopyEngine singleton cannot initialize
without NT8 host), apply the Skip attribute:

```csharp
[Fact(Skip = "NT8-runtime: CopyEngine.cctor requires NT8 host")]
public void EvictDedup_CancelledEntry_ClearsLastLeaderDirection()
```

If Skip is applied, document in `docs/brain/PTT-REPAIRS-DW-F-R06/ticket-2-completion.md`.

---

### JS Rule Constraints

| Rule   | Requirement                    | Status |
|--------|--------------------------------|--------|
| JS-021 | No lock() in changed code      | PASS — test code uses no lock(); all ForTest shims use ConcurrentDictionary |
| JS-042 | ASCII-only in all new text     | PASS — all strings, comments, and identifiers are ASCII-only |
| JS-013 | New helpers must be CYC<=1     | N/A — no new production helpers introduced |

### Behavior Change
**NONE** on production side. Test-only addition.
No production code (`src/PropTraderTools/CopyEngine.cs`) is modified in this ticket.

---

### 7-Scan Checklist — T2

**SCAN-1** — Lock-free verification (both files):
```powershell
grep -r "lock(" src/PropTraderTools/CopyEngine.cs
grep -r "lock(" src/PropTraderTools/CopyEngineTests.cs
```
Expected: **0 matches** in both files

**SCAN-2** — ASCII-only verification:
```powershell
grep -rP "[^\x00-\x7F]" src/PropTraderTools/CopyEngineTests.cs
```
Expected: **0 matches** in CopyEngineTests.cs

**SCAN-3** — Build error line count:
```powershell
dotnet build 2>&1 | grep " error "
```
Expected: **0 matching lines**

**SCAN-4** — Build error summary:
```powershell
dotnet build 2>&1 | Select-String "Error\(s\)"
```
Expected: output contains **"0 Error(s)"**

**SCAN-5** — Test count with new test:
```powershell
dotnet test --filter "FullyQualifiedName~CopyEngineTests" 2>&1
```
Two acceptable outcomes — **either is a PASS for SCAN-5**:

| Scenario | passed | failed | skipped | Condition |
|----------|--------|--------|---------|-----------|
| F3 passes | 20 | 448 | 31 | Normal — test runs and asserts correctly |
| F3 skipped (NT8 fallback) | 19 | 449 | 32 | Skip attr applied; no genuine regression |

No new genuine test regressions permitted beyond the Skip fallback above.

**SCAN-6** — Hard-link sync:
```powershell
powershell -File .\deploy-sync.ps1
```
Expected: output contains **"SYNC COMPLETE"**

**SCAN-7** — Hard-link integrity:
```powershell
(Get-Item src\PropTraderTools\CopyEngineTests.cs).LinkType
```
Expected: **"HardLink"** (or hardlink count = 1)

---

### Return Status
`BUILD_PASS` if all 7 scans pass (using either acceptable SCAN-5 outcome).
`BUILD_FAIL` if any scan fails.

---

## Pipeline Completion Criteria

Epic PTT-REPAIRS-DW-F-R06 is complete when:
- [ ] T1 returned `BUILD_PASS`
- [ ] T2 returned `BUILD_PASS`
- [ ] `CopyEngine.cs:727` contains "CYC=2" (not "CYC=1")
- [ ] `CopyEngine.cs:738` contains "CYC=4" (not "CYC=2")
- [ ] `CopyEngineTests.cs` contains method `EvictDedup_CancelledEntry_ClearsLastLeaderDirection`
- [ ] `dotnet build`: 0 Error(s)
- [ ] `dotnet test`: passed >= 19, no new genuine regressions
- [ ] `deploy-sync.ps1`: SYNC COMPLETE on both T1 and T2
- [ ] Hardlink count = 1 for both changed files

---

**TICKETS_COMPLETE**
