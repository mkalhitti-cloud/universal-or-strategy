# B215 Lane B -- Tickets

**Block**: B215-LaneB
**Produced by**: ptt-architect (Phase 3)
**Input**: docs/brain/B215/02-architecture-plan-lane-b.md (REVIEW_PASS)
**Date**: 2026
**Status**: TICKETS_COMPLETE

---

## Ticket LB1

**Ticket ID**: LB1
**Title**: DW-B212-01 Close + DW-B214-01 CYC Annotation Fix (Comment Only)
**Spec Req IDs**: DW-B212-01 (CLOSED-NO-ACTION), DW-B214-01 (CLOSED)

---

### Scope Summary

2 comment-line edits across 2 files. Zero logic changes.

| Edit | File | Change Type | Location |
|------|------|-------------|----------|
| D1 | src/PropTraderTools/CopyEngine.cs | Comment block INSERT (4 lines) | Immediately above `internal void Trim(Instrument instrument)` (~line 5818) |
| D2 | src/PropTraderTools/TradeCopierPanel.cs | Comment line REPLACE (1 line) | CYC annotation line on `TryResolveLeaderAccount` (~line 566) |

- **Method bodies**: UNCHANGED.
- **Logic**: UNCHANGED.
- **New tests required**: NONE.
- **New methods**: NONE.

---

### File Paths in Workspace

- `src/PropTraderTools/CopyEngine.cs`
- `src/PropTraderTools/TradeCopierPanel.cs`

---

### Method Signatures (for reference -- no changes)

```csharp
// CopyEngine.cs -- UNCHANGED
internal void Trim(Instrument instrument)                             // CYC=2, unchanged
private void TrimOneAccount(Account acc, Instrument instrument)       // CYC=3, unchanged

// TradeCopierPanel.cs -- UNCHANGED
private NinjaTrader.Cbi.Account TryResolveLeaderAccount()             // CYC=8, annotation corrected only
```

---

### Edit D1 -- CopyEngine.cs: Insert Comment Block Above Trim(Instrument)

**File**: `src/PropTraderTools/CopyEngine.cs`
**Target method**: `internal void Trim(Instrument instrument)`

**What to do**: Insert the following 4-line comment block IMMEDIATELY ABOVE the line
`internal void Trim(Instrument instrument)`. A blank line must separate this comment from the
closing brace of the prior method. The comment sits directly above the method declaration
with no blank line between comment and declaration.

**Comment text to insert (exact, character-precise, 8-space indent)**:
```csharp
        // B215-DW-B212-01: leader-skip NOT needed for Trim path.
        // TrimOneAccount calls acc.CreateOrder (PTT-Trim partial market sell) only.
        // No CancelAllAccountOrders -> no ATM bracket cancel -> no NT8 ATM re-arm risk.
        // DW-B212-01 CLOSED-NO-ACTION. Compare: Flatten uses HF-08 leader-skip (CancelAllAccountOrders present).
```

**Resulting file structure at that location (exact)**:
```
        }
        }

        // B215-DW-B212-01: leader-skip NOT needed for Trim path.
        // TrimOneAccount calls acc.CreateOrder (PTT-Trim partial market sell) only.
        // No CancelAllAccountOrders -> no ATM bracket cancel -> no NT8 ATM re-arm risk.
        // DW-B212-01 CLOSED-NO-ACTION. Compare: Flatten uses HF-08 leader-skip (CancelAllAccountOrders present).
        internal void Trim(Instrument instrument)
        {
```

**JS rule constraints on this edit**:
- ASCII-only text. No Unicode. No emoji. No curly quotes. ENFORCED.
- `//` line comment style (not block comment `/* */`). ENFORCED.
- No executable code added. P0 rules are N/A.

**Verification after D1**:
1. Open file at the `internal void Trim(Instrument instrument)` line.
2. Confirm 4-line comment block is present immediately above the declaration.
3. Confirm comment contains the string `No CancelAllAccountOrders`.
4. Confirm comment contains the string `DW-B212-01 CLOSED-NO-ACTION`.
5. Confirm comment contains the string `PTT-Trim`.
6. Confirm blank line separates the preceding method close brace from the comment.
7. Confirm no blank line between comment and the `internal void Trim(Instrument instrument)` declaration.

---

### Edit D2 -- TradeCopierPanel.cs: Replace CYC Annotation on TryResolveLeaderAccount

**File**: `src/PropTraderTools/TradeCopierPanel.cs`
**Target method**: `private NinjaTrader.Cbi.Account TryResolveLeaderAccount()`

**What to do**: Locate the existing CYC comment line immediately above
`private NinjaTrader.Cbi.Account TryResolveLeaderAccount()`.

**Old line (exact -- find this)**:
```csharp
        // CYC=6: chart null(1), combo found(2), Account cast(3), string name empty(4), Account.All loop(5), combo found wire(6).
```

**Replace ONLY that one line with (exact)**:
```csharp
        // CYC=8 (strict McCabe: 8 decision points; Ph4a path-counting=6, both pass JS-066 ceiling).
```

**Lines immediately following (DO NOT TOUCH -- must remain verbatim)**:
```csharp
        // JS-002: returns null (not throw) -- callers use null as a no-op sentinel.
        // JS-021: no lock. UI-thread-only (called only from WPF button Click handlers).
```

**Post-edit state: lines 566-568 must read exactly**:
```csharp
        // CYC=8 (strict McCabe: 8 decision points; Ph4a path-counting=6, both pass JS-066 ceiling).
        // JS-002: returns null (not throw) -- callers use null as a no-op sentinel.
        // JS-021: no lock. UI-thread-only (called only from WPF button Click handlers).
```

**JS rule constraints on this edit**:
- ASCII-only text. No Unicode. No emoji. No curly quotes. ENFORCED.
- `//` line comment style. ENFORCED.
- Do NOT remove or alter the JS-002 annotation (documents null-return sentinel pattern -- JS-002 compliance evidence).
- Do NOT remove or alter the JS-021 annotation (documents lock-free UI-thread-only pattern -- JS-021 compliance evidence).
- No executable code added or changed. P0 rules are N/A.

**Verification after D2**:
1. Open file at `TryResolveLeaderAccount` method header.
2. Confirm CYC annotation reads: `CYC=8 (strict McCabe: 8 decision points; Ph4a path-counting=6, both pass JS-066 ceiling).`
3. Confirm the following line reads: `// JS-002: returns null (not throw) -- callers use null as a no-op sentinel.`
4. Confirm the line after that reads: `// JS-021: no lock. UI-thread-only (called only from WPF button Click handlers).`
5. Confirm no other comment lines near this method were modified.

---

### xUnit Tests

**None required.** Comment-only edits. Zero logic changes. No new test vectors.

---

### Post-Edit Verification Commands

Run these commands IN ORDER after both edits are applied:

```powershell
# 1. Hard-link sync (MANDATORY after any .cs edit)
powershell -File .\scripts\ptt-sync.ps1
# Expected: 21/0 (21 links OK, 0 broken)

# 2. Guardian symbol check (MANDATORY)
powershell -File .\scripts\verify_critical_symbols.ps1
# Expected: GUARDIAN PASS
# NOTE: Known false positive DW-B209-01 may appear -- this is pre-existing and documented.
#        A single-hunk false positive from the Guardian script is acceptable for comment-only changes.

# 3. PR hygiene / DIFF GUARD
powershell -File .\scripts\verify_pr_hygiene.ps1
# Expected: DIFF GUARD PASS
# Expected diff size: ~400-600 chars (well under the 10,000 char limit)

# 4. Test suite
dotnet test src/PropTraderTools/PropTraderTools.Tests.csproj
# Expected: passed/failed counts IDENTICAL to B214 baseline (no new failures)
```

---

### 7-Scan Checklist (SCAN-1 through SCAN-7)

Engineer MUST run each scan to zero (or confirm expected result) before issuing VERIFY_PASS.

---

**SCAN-1: File scope gate**
```powershell
git diff --name-only
```
**Required result**: Output MUST show ONLY:
```
src/PropTraderTools/CopyEngine.cs
src/PropTraderTools/TradeCopierPanel.cs
```
No other `.cs` files may appear. If any unexpected file appears: STOP. Investigate before proceeding.

---

**SCAN-2: Test regression gate**
```powershell
dotnet test src/PropTraderTools/PropTraderTools.Tests.csproj
```
**Required result**: Passed/failed counts UNCHANGED from B214 baseline. Zero new failures.
Comment-only edits cannot introduce new test failures. Any new failure = unrelated contamination. STOP and investigate.

---

**SCAN-3: lock() prohibition**
```powershell
grep -rn "lock(" src/PropTraderTools/CopyEngine.cs src/PropTraderTools/TradeCopierPanel.cs
```
**Required result**: 0 matches in any new or changed lines.
(Pre-existing results, if any, must be unchanged from B214 baseline -- no new lock() added.)

---

**SCAN-4: async void prohibition**
```powershell
grep -rn "async void " src/ --include="*.cs"
```
**Required result**: 0 results, OR only pre-existing event handlers that are UNCHANGED from B214 baseline.
No new `async void` may be introduced.

---

**SCAN-5: return null prohibition (new code only)**
```powershell
grep -rn "return null;" src/PropTraderTools/CopyEngine.cs src/PropTraderTools/TradeCopierPanel.cs
```
**Required result**: No new `return null;` in any modified method.
(Pre-existing `return null` in `TryResolveLeaderAccount` is a documented JS-002 sentinel -- UNCHANGED, acceptable.)

---

**SCAN-6: CancelAllAccountOrders absence in Trim path**
```powershell
grep -rn "CancelAllAccountOrders" src/PropTraderTools/CopyEngine.cs
```
**Required result**: The `Trim(Instrument)` method and `TrimOneAccount` method MUST NOT contain
`CancelAllAccountOrders`. This confirms the DW-B212-01 investigation finding (no ATM re-arm risk).
(Other methods such as `FlattenOneAccount` may contain this call -- that is expected and correct.)

---

**SCAN-7: DIFF GUARD (size and hunk count)**
```powershell
powershell -File .\scripts\verify_pr_hygiene.ps1
```
**Required result**: DIFF GUARD PASS.
- CopyEngine.cs hunk count: 1 (the 4-line comment block insertion = 1 hunk)
- TradeCopierPanel.cs hunk count: 1 (the 1-line comment replacement = 1 hunk)
- Total diff chars: well under 10,000 limit (expected ~400-600 chars)

---

### Success Criteria (Engineer Deliverables)

Before issuing VERIFY_PASS, engineer MUST produce:

1. **`ticket-lb1-completion.md`** written with all 7 scan results (pass/fail + output for each scan).
2. **ptt-sync**: shows `21/0`.
3. **DIFF GUARD**: PASS.
4. **dotnet test**: passed/failed counts unchanged from B214 baseline.

---

### Spec Requirements Satisfied

| Spec Req ID | Description | Closure Evidence |
|-------------|-------------|-----------------|
| DW-B212-01 | Trim(Instrument) leader-skip investigation | TrimOneAccount body read (lines 5977-6015): no CancelAllAccountOrders. acc.CreateOrder only. No ATM re-arm risk. 4-line comment added at CopyEngine.cs before line 5818. CLOSED-NO-ACTION. |
| DW-B214-01 | CYC annotation mismatch on TryResolveLeaderAccount | CYC comment on line 566 updated from CYC=6 (path-counting) to CYC=8 (strict McCabe). Both counts pass JS-066. CLOSED. |

---

### Deferred Work Carry-Forward

The following items are OPEN and carry forward to B216+. They are NOT in scope for LB1.

| ID | Item | Priority | Target Block |
|----|------|----------|--------------|
| DW-B214-02 | 24 pre-existing test failures in pre-B214 test classes | P1 | Dedicated test-debt block |
| DW-B214-LB-01 | B74LaneCTests.cs cap assertions require manual update (gitignored file) | P1 | Manual (NT8 project folder) |
| DW-B214-LB-02 | B214Tests.cs B214T2Tests class (3 [Fact] tests) must be created manually | P1 | Manual (NT8 project folder) |
| DW-B214-LB-03 | B73Tests.cs FormatGlobalBeBuffer assertions require manual update (gitignored) | P1 | Manual (NT8 project folder) |
| DW-B209-01 | Guardian script single-hunk false positive fix | P2 | B216+ |
| DW-B209-02 | TradeCopierPanel.cs BrushTeal gap (_instr2tBtn.Background) | P2 | B216+ |
| DW-B209-04 | specs/002-trade-copier-spec.html B209 block updates | P1 | Before SIM R8 |
| DW-B211-01 | Direction-aware validation for Ask/Bid | P2 | Future block |
| DW-B211-02 | Target nudge buttons (depends DW-B211-01) | P2 | Future block (after DW-B211-01) |
| DW-B212-02 | PttMoveTargetAll.cs future role (retain vs deprecate) | P2 | B216+ |

---

**TICKETS_COMPLETE**

*Produced by ptt-architect, Phase 3.*
*Engineer: implement from this ticket exactly. Reviewer: ptt-ticket-reviewer (Phase 3.5).*
