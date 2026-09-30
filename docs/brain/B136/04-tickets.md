# B136 Tickets

**Block**: B136
**Produced by**: ptt-architect (Phase 3)
**Date**: 2026-09-07
**Status**: TICKET_REVIEW_PASS (Phase 3.5 confirmed)

---

## Ticket B136-T1 — OrderPassesBracketGate Fused Guard

### Scope Lock Statement
SCOPE LOCK - TICKET 1 ONLY.
Do NOT read, reference, or implement any other ticket in this session.

---

### 1. Spec Requirement Traceability

| DW Item | Title | Spec Section | This Ticket |
|---------|-------|--------------|-------------|
| DW-B148 | SignalOrNameMatches PTT-prefix gate | specs/002-trade-copier-spec.html §DW-B148 | CLOSES |
| DW-B146 | Second drag fo=null | specs/002-trade-copier-spec.html §DW-B146 | CLOSES (consequence) |

---

### 2. Method Signatures (exact)

**UNCHANGED** (do not modify):
```csharp
private static bool SignalOrNameMatches(Order order, string? signalName, string? leaderName)
internal static bool SignalOrNameMatchesTestable(Order order, string? signalName, string? leaderName)
private static bool MatchesLeaderName(Order order, string? leaderName, bool isStop)
internal static bool MatchesLeaderNameTestable(Order order, string? leaderName, bool isStop)
```

**MODIFIED** (loop body only — signature unchanged):
```csharp
private Order? FindFollowerBracketOrder(IEnumerable<Order> orders, string? fromEntrySignalName, bool isStop, string? leaderName = null)
```

**NEW** (add after MatchesLeaderNameTestable ~L2659):
```csharp
private static bool OrderPassesBracketGate(Order order, string? signalName, string? leaderName, bool isStop)
internal static bool OrderPassesBracketGateTestable(Order order, string? signalName, string? leaderName, bool isStop)
```

---

### 3. Implementation Instructions (precise)

#### Change 1 — CopyEngine.cs: Update CYC comment in FindFollowerBracketOrder (~L2596-2599)

Replace the CYC comment block from:
```
// CYC=8 (AT LIMIT). ...
```
With:
```csharp
// CYC=7 (post-B136). AT LIMIT RESOLVED; headroom = 1.
// foreach(1) + OrderPassesBracketGate guard(1) + state filter(3) + isStop(1) + type match(1) = 7.
// DW-B143: Accepted added. DW-B144: Submitted added. DW-B145: leaderName exact guard. DW-B146: MatchesLeaderName helper. DW-B148: OrderPassesBracketGate fused guard (B136).
// JS-021: no lock. JS-001: no throw. JS-002: Order? null contract unchanged.
```

#### Change 2 — CopyEngine.cs: Replace two-guard sequence in FindFollowerBracketOrder loop (~L2609-2612)

Replace these two lines:
```csharp
if (!SignalOrNameMatches(order, fromEntrySignalName, leaderName))
    continue;
if (!MatchesLeaderName(order, leaderName, isStop))
    continue;
```
With this single line:
```csharp
if (!OrderPassesBracketGate(order, fromEntrySignalName, leaderName, isStop)) // (1) branch -- B136 DW-B148: fused guard (ATM path routes to MatchesLeaderName)
    continue;
```

#### Change 3 — CopyEngine.cs: Add OrderPassesBracketGate method (insert after MatchesLeaderNameTestable ~L2659)

Insert the following block:
```csharp
// B136 DW-B148: fused bracket-gate predicate -- replaces the sequential SignalOrNameMatches +
// MatchesLeaderName guard pair in FindFollowerBracketOrder.
// Signal path (signalName != null): exclusive signal-match only. Preserves original signal
//   exclusivity -- orders from a different entry signal are rejected even if name matches.
// ATM path (signalName == null): delegates to MatchesLeaderName, which passes exact ATM name
//   (e.g. "Target3") AND PTT-prefix replacements ("PTT-TGT-Drag", "PTT-STP-Drag").
//   This is the fix: PTT-TGT-Drag now reaches MatchesLeaderName and returns true.
// CYC=2: base(1) + if(signalName != null)(1) = 2. Well within <= 8.
// JS-021: no lock (static, no shared state). JS-001: no throw. JS-002: returns bool.
// ASCII-only. No DateTime. No FontFamily. No hex color literals.
private static bool OrderPassesBracketGate(
    Order order,
    string? signalName,
    string? leaderName,
    bool isStop)
{
    if (signalName != null)                                    // (1) signal path: exact match only
        return order.FromEntrySignal == signalName;
    return MatchesLeaderName(order, leaderName, isStop);       // ATM path: exact name OR PTT-prefix
}

// B136 DW-B148: test seam -- delegates to OrderPassesBracketGate for xUnit test access.
// InternalsVisibleTo("PropTraderTools.Tests") granted at L46.
internal static bool OrderPassesBracketGateTestable(
    Order order,
    string? signalName,
    string? leaderName,
    bool isStop)
    => OrderPassesBracketGate(order, signalName, leaderName, isStop);
```

#### Change 4 — PropTraderTools.csproj: Add B136Tests.cs compile entry

Find the line:
```xml
<Compile Include="Tests\B135Tests.cs" />
```
Add immediately after:
```xml
<Compile Include="Tests\B136Tests.cs" />
```

#### Change 5 — Create src/PropTraderTools/Tests/B136Tests.cs (new file)

New xUnit test file. See Section 6 for all 9 [Fact] tests.

---

### 4. NT8 API Constraints

No NT8 API changes. `OrderPassesBracketGate` is a pure predicate operating on `Order.FromEntrySignal`, `Order.Name`, and the existing `MatchesLeaderName` helper. No new NT8 calls introduced.

---

### 5. CYC Pre-Check

| Method | Pre-B136 CYC | Post-B136 CYC | Limit | Pass? |
|--------|-------------|--------------|-------|-------|
| `FindFollowerBracketOrder` (list) | 8 | 7 | 8 | YES |
| `OrderPassesBracketGate` (NEW) | — | 2 | 8 | YES |
| `OrderPassesBracketGateTestable` (NEW) | — | 1 | 8 | YES |
| `SignalOrNameMatches` | 3 | 3 (UNCHANGED) | 8 | YES |
| `MatchesLeaderName` | 5 | 5 (UNCHANGED) | 8 | YES |

---

### 6. Test File Instructions

**File**: `src/PropTraderTools/Tests/B136Tests.cs`
**Namespace**: `PropTraderTools`
**Framework**: xUnit only. `[Fact]` attribute. NO NUnit. NO MSTest.
**Test seam**: `CopyEngine.OrderPassesBracketGateTestable(order, signalName, leaderName, isStop)`

| [Fact] Method Name | signalName | order.Name / FromEntrySignal | leaderName | isStop | Expected |
|--------------------|-----------|-------------------------------|-----------|--------|----------|
| `T1_SignalPath_Match_ReturnsTrue` | "S1" | FromEntrySignal="S1" | any | false | true |
| `T1_SignalPath_Mismatch_ReturnsFalse` | "S1" | FromEntrySignal="S2" | any | false | false |
| `T1_SignalPath_NullFromEntry_ReturnsFalse` | "S1" | FromEntrySignal=null | any | false | false |
| `T1_AtmPath_PttTgtDrag_ReturnsTrue` | null | Name="PTT-TGT-Drag" | "Target3" | false | **true** (THE FIX) |
| `T1_AtmPath_PttStpDrag_ReturnsTrue` | null | Name="PTT-STP-Drag" | "Stop1" | true | **true** (THE FIX) |
| `T1_AtmPath_PttTgtDrag_WrongLeg_ReturnsFalse` | null | Name="PTT-TGT-Drag" | "Stop1" | true | false |
| `T1_AtmPath_NativeAtmTarget_ReturnsTrue` | null | Name="Target3" | "Target3" | false | true |
| `T1_AtmPath_NativeAtmStop_ReturnsTrue` | null | Name="Stop1" | "Stop1" | true | true |
| `T1_AtmPath_UnknownOrder_ReturnsFalse` | null | Name="OtherOrder" | "Target3" | false | false |

**Existing test suites — confirm GREEN, no changes needed**:
- B133Tests.cs (SignalOrNameMatchesTestable — UNCHANGED method) — GREEN
- B135Tests.cs (MatchesLeaderNameTestable — UNCHANGED method) — GREEN
- B129-B134 (52 tests — no touched methods) — GREEN

---

### 7. 7-SCAN CHECKLIST (mandatory — defense in depth)

Engineer MUST run ALL 7 scans to zero before BUILD_PASS:

- [ ] **SCAN 1**: `grep -r "lock(" src/PropTraderTools --include="*.cs"` → zero results in new/modified code
- [ ] **SCAN 2**: `grep -rn "async void " src/PropTraderTools --include="*.cs"` → zero results in new code
- [ ] **SCAN 3**: `grep -rn "return null;" src/PropTraderTools --include="*.cs"` → zero in new/modified methods (OrderPassesBracketGate returns bool)
- [ ] **SCAN 4**: `python scripts/complexity_audit.py` → `FindFollowerBracketOrder` ≤7, `OrderPassesBracketGate` ≤2
- [ ] **SCAN 5**: ASCII-only check on new/modified lines → zero non-ASCII chars
- [ ] **SCAN 6**: `dotnet build src/PropTraderTools` → zero errors, zero new warnings
- [ ] **SCAN 7**: `dotnet test` → all test suites GREEN (B129-B136), expect 71/71 total (9 new + 62 prior)

---

### 8. Completion Artifact

Engineer MUST write: `docs/brain/B136/ticket-1-completion.md`

Required contents:
- Scope lock confirmation: "SCOPE LOCK - TICKET 1 ONLY — confirmed"
- Changes made (file, method, line range for each change)
- 7-scan results table:

| SCAN ID | Command | Result | Status |
|---------|---------|--------|--------|
| SCAN-01 | grep lock() | [output] | PASS/FAIL |
| SCAN-02 | grep async void | [output] | PASS/FAIL |
| SCAN-03 | grep return null | [output] | PASS/FAIL |
| SCAN-04 | complexity_audit.py | [CYC values] | PASS/FAIL |
| SCAN-05 | ASCII check | [output] | PASS/FAIL |
| SCAN-06 | dotnet build | [error count] | PASS/FAIL |
| SCAN-07 | dotnet test | [X/71 pass] | PASS/FAIL |

- Test count: X/71 pass (9 new + 62 prior)
- BUILD_PASS declaration
