# B140-LaneA — Ticket Generation (Phase 3)
## ptt-architect output | Phase 3 | Status: TICKETS_COMPLETE

---

## TICKET 1 — OCO Cascade Fix: SyncFollowerBracket acc.Change for OCO-linked ATM Stop brackets

### Metadata

| Field | Value |
|-------|-------|
| **Spec Requirement IDs** | DW-B153 (P0 closure) |
| **File** | `src/PropTraderTools/CopyEngine.cs` |
| **Method** | `SyncFollowerBracket` (approx line 2280) |
| **Change Type** | Surgical insert — 9 lines inside existing `if` block |
| **New methods** | None |
| **New files** | `tests/PropTraderTools.Tests/B140Tests.cs` |

---

### Exact BEFORE / AFTER Code

#### BEFORE (locate at approx line 2280 — search for `IsAtmSTPOrder(fo)) // (3)`)

```csharp
if (isStop && IsAtmSTPOrder(fo)) // (3)
{
    SyncAtmFollowerBracket(acc, fo, newPrice);
    return;
}
```

#### AFTER

```csharp
if (isStop && IsAtmSTPOrder(fo)) // (3)
{
    if (!string.IsNullOrEmpty(fo.Oco)) // (3a) B140: OCO-linked -- Change preserves OCO partner
    {
        fo.StopPrice = newPrice;
        try { acc.Change(new Order[] { fo }); }
        catch (Exception ex)
        { StatusUpdate?.Invoke(acc.Name + ": ATM STP Change error: " + ex.Message); }
        return;
    }
    SyncAtmFollowerBracket(acc, fo, newPrice); // (3b) no OCO -- cancel+resubmit (existing path)
    return;
}
```

#### Branch Routing Table

| Branch | Condition | Action |
|--------|-----------|--------|
| (3a) OCO-linked | `fo.Oco` non-empty (Stop1, Stop2, Stop3) | `fo.StopPrice = newPrice; acc.Change(new Order[] { fo })` — preserves OCO partner |
| (3b) No OCO | `fo.Oco` empty (PTT-STP-Drag) | Existing `SyncAtmFollowerBracket` path — cancel+resubmit unchanged |

**Stop3 note**: Stop3 has a non-empty Oco GUID and WILL route to branch (3a). This is intentional — `acc.Change` preserves the `Target3` OCO link and is strictly better than cancel+resubmit. No special Stop3 branch is required. See `02-architecture-plan.md` Section 4.

---

### 7-Scan Checklist

Engineer MUST run each scan and record results in `ticket-1-completion.md`. All items MUST report zero findings.

| Scan | Rule | Command | Expected Result | Notes |
|------|------|---------|-----------------|-------|
| SCAN-01 | JS-021 lock() ban | `grep -n "lock(" src/PropTraderTools/CopyEngine.cs` | **0 hits** | No lock() introduced anywhere in file |
| SCAN-02 | JS-033 async void ban | `grep -n "async void " src/PropTraderTools/CopyEngine.cs` | **0 hits** | No async void anywhere in file |
| SCAN-03 | JS-002 return null ban | `grep -n "return null;" src/PropTraderTools/CopyEngine.cs` | **0 hits** | Void path; no null return introduced |
| SCAN-04 | JS-001 throw/rethrow ban | `grep -n "throw;" src/PropTraderTools/CopyEngine.cs` | **0 hits** | Exception absorbed via StatusUpdate; no rethrow |
| SCAN-05 | ASCII-only | `grep -Pn "[^\x00-\x7F]" src/PropTraderTools/CopyEngine.cs` | **0 hits** | All string literals in change are ASCII-only |
| SCAN-06 | CYC limit (JS-041) | `python scripts/complexity_audit.py src/PropTraderTools/CopyEngine.cs` | **SyncFollowerBracket CYC <= 8** | CYC 7->8; at limit; PASS. Any higher = STOP. |
| SCAN-07 | Build clean | `dotnet build src/PropTraderTools/PropTraderTools.csproj` | **0 errors, 0 warnings** | Clean build required |

**HARD RULE**: If SCAN-06 reports CYC > 8, the change is architecturally invalid. STOP immediately and report to Director before proceeding.

---

### xUnit Test Requirements

**Test file**: `tests/PropTraderTools.Tests/B140Tests.cs` (create new file)
**Framework**: xUnit only — NO NUnit, NO MSTest.

#### Test Stubs

```csharp
using Xunit;

namespace PropTraderTools.Tests
{
    public class B140Tests
    {
        // T_B140_01
        // Validates: New OCO-linked branch calls acc.Change, not acc.Cancel
        // Assert: When fo.Oco is non-empty, the mock acc.Change is invoked exactly once
        //         AND acc.Cancel is never invoked
        [Fact]
        public void T_B140_01_SyncFollowerBracket_OcoLinked_CallsAccChange() { }

        // T_B140_02
        // Validates: Empty Oco regression guard (3b path intact)
        // Assert: When fo.Oco is empty string, SyncAtmFollowerBracket is invoked
        //         (acc.Change is NOT called)
        [Fact]
        public void T_B140_02_SyncFollowerBracket_EmptyOco_CallsSyncAtmFollowerBracket() { }

        // T_B140_03
        // Validates: IsAtmSTPOrder correctly detects Stop1
        // Assert: IsAtmSTPOrder(order named "Stop1") returns true
        [Fact]
        public void T_B140_03_IsAtmSTPOrder_Stop1_ReturnsTrue() { }

        // T_B140_04
        // Validates: IsAtmSTPOrder correctly detects Stop2
        // Assert: IsAtmSTPOrder(order named "Stop2") returns true
        [Fact]
        public void T_B140_04_IsAtmSTPOrder_Stop2_ReturnsTrue() { }

        // T_B140_05
        // Validates: IsAtmSTPOrder correctly detects Stop3
        // Assert: IsAtmSTPOrder(order named "Stop3") returns true
        [Fact]
        public void T_B140_05_IsAtmSTPOrder_Stop3_ReturnsTrue() { }

        // T_B140_06
        // Validates: OCO-linked branch does NOT invoke acc.Cancel (cascade eliminated)
        // Assert: Mock acc.Cancel is never called when fo.Oco is non-empty and isStop=true
        [Fact]
        public void T_B140_06_OcoLinkedBranch_NoAccCancelCall() { }

        // T_B140_07
        // Validates: ATM target branch (isStop=false) is not disturbed by B140 change
        // Assert: When isStop=false, route goes to SyncAtmFollowerTarget (unchanged path)
        [Fact]
        public void T_B140_07_AtmTargetBranch_RouteToSyncAtmFollowerTarget() { }
    }
}
```

#### Test Coverage Map

| Test ID | Validates |
|---------|-----------|
| T_B140_01 | New OCO path calls `acc.Change` |
| T_B140_02 | Empty Oco regression — 3b path intact |
| T_B140_03 | `IsAtmSTPOrder` Stop1 detection |
| T_B140_04 | `IsAtmSTPOrder` Stop2 detection |
| T_B140_05 | `IsAtmSTPOrder` Stop3 detection |
| T_B140_06 | No `acc.Cancel` on OCO-linked order |
| T_B140_07 | ATM target branch unaffected |

---

### SIM Gate Requirements

Verifier records gate results in `ticket-1-verification.md`. ALL gates must pass before merge.

#### Gate 1 — acc.Change() on Stop1/Stop2 is NOT a silent no-op (CRITICAL — BLOCKING)

**Procedure**:
1. Open NT8 SIM environment with PTT leader+follower running.
2. Drag leader stop price to a new level.
3. Observe NT8 Order Grid for follower account.

**Pass criteria (ALL must be true)**:
- Follower `Stop1` price updates to new price in Order Grid.
- Follower `Stop2` price updates to new price in Order Grid.
- `Target1` is **NOT** cancelled after drag.
- `Target2` is **NOT** cancelled after drag.

**Gate 1 FAIL Protocol — NO EXCEPTIONS**:
- If `acc.Change` is confirmed as a no-op on ATM Stop brackets:
  - **STOP immediately. Do NOT implement a fallback.**
  - Report to Director with SIM log.
  - Document as **DW-B154**.
  - Merge is BLOCKED until Director resolution.

#### Gate 2 — Stop3 routes to acc.Change and price updates correctly

**Procedure**: Drag leader stop, observe follower Stop3.

**Pass criteria**:
- Stop3 price updates via `acc.Change` (not cancel+resubmit).
- `Target3` is **NOT** cancelled.
- No OCO cascade observed.

**Note**: Stop3 (`fo.Oco` non-empty) routes to branch (3a) same as Stop1/Stop2. This is intentional and correct. Verify that Stop3 also behaves correctly under the new path.

#### Gate 3 — Second drag works, no cascade on either drag

**Procedure**: Perform two consecutive stop drags.

**Pass criteria**:
- `Stop1` and `Stop2` prices update on both drags.
- No target cancellation on either drag.
- Order Grid state is consistent after second drag.

---

### Verification Citations Required in ticket-1-verification.md

Verifier MUST provide all five citations. Missing any = incomplete verification.

| Citation ID | Requirement |
|-------------|-------------|
| NT8-VERIFY-01 | `grep "Change" docs/standards/NT8_API_SURFACE.md` — confirm B31 `acc.Change` preserves OCO |
| NT8-VERIFY-02 | `grep "Oco" docs/standards/NT8_FULL_REFERENCE.md` — confirm `fo.Oco` property exists on NT8 Order class |
| NT8-VERIFY-03 | SIM Gate 1 result — log or screenshot showing Stop1/Stop2 price updated AND no OCO cascade (Target1/Target2 NOT cancelled) |
| NT8-VERIFY-04 | JS-DNA scan result — `grep -n "lock(" src/PropTraderTools/CopyEngine.cs` showing 0 hits in modified file |
| NT8-VERIFY-05 | CYC check result — `python scripts/complexity_audit.py` output confirming `SyncFollowerBracket` CYC = 8 (not exceeded) |

---

### Definition of Done

Engineer checks each item. All must be checked before writing `ticket-1-completion.md`.

- [ ] Surgical change applied to `src/PropTraderTools/CopyEngine.cs` at approx line 2280 (BEFORE/AFTER diff matches exactly)
- [ ] SCAN-01: `grep -n "lock("` — 0 hits
- [ ] SCAN-02: `grep -n "async void "` — 0 hits
- [ ] SCAN-03: `grep -n "return null;"` — 0 hits
- [ ] SCAN-04: `grep -n "throw;"` — 0 hits
- [ ] SCAN-05: `grep -Pn "[^\x00-\x7F]"` — 0 hits
- [ ] SCAN-06: `SyncFollowerBracket` CYC = 8 (at limit, PASS)
- [ ] SCAN-07: `dotnet build` — 0 errors, 0 warnings
- [ ] `tests/PropTraderTools.Tests/B140Tests.cs` created with 7 xUnit `[Fact]` stubs (all pass with empty bodies or minimal assertions)
- [ ] `powershell -File scripts\ptt-sync-and-verify.ps1` executed — 0 MISMATCH lines
- [ ] F5 in NinjaTrader 8 compile — result noted as PENDING (verifier confirms green)
- [ ] `ticket-1-completion.md` written with all 7 scan results recorded

---

*Tickets authored by ptt-architect, B140-LaneA, Phase 3.*
*Architecture plan: `docs/brain/B140-LaneA/02-architecture-plan.md` (REVIEW_PASS)*
*DW-B153 closure: this ticket implements the P0 fix.*
