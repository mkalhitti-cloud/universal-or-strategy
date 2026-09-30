# B129 LaneB Ticket 2 Verification Report
## Layer 3 — Independent Verifier Report
## Block: B129 LaneB — DW-B134: ATM Bracket Drag Not Synced to Followers
## Ticket: B129-LaneB-T2
## Date: 2026-08-31
## Verifier: ptt-verifier (Phase 4b)

---

## Verification Checklist

### VR-01: IsBracketLegStatic (Layer 1 fix)
**PASS**

- File: `src/PropTraderTools/CopyEngine.cs`
- Method: `private static bool IsBracketLegStatic(Order order)` — confirmed private static (L3612)
- CYC comment at L3610: `// DW-B134: added STP EndsWith clause -- NT8 ATM stop brackets are named "Buy STP"/"Sell STP". // Mirrors IsStopLeg (L3521) which already has this clause. CYC: 3 -> 4.`
- STP clause present at L3621: `|| order.Name.EndsWith("STP", StringComparison.OrdinalIgnoreCase)`
- Structure confirmed: 4th OR clause inside null-guarded Name block — matches ticket Step 1 spec exactly
- All 3 pre-existing StartsWith clauses intact (Stop, Target, PTT-)

**Evidence**: CopyEngine.cs L3609-3624

---

### VR-02: IsAtmSTPOrder (new helper)
**PASS**

- File: `src/PropTraderTools/CopyEngine.cs`
- Location: L2028-2030 (immediately after IsTrailingStop closing brace at L2023)
- Declaration: `internal static bool IsAtmSTPOrder(Order order) =>` (L2028) — confirmed internal static
- Body: `order.Name != null && order.Name.EndsWith("STP", StringComparison.OrdinalIgnoreCase);`
- Null guard: `order.Name != null` — confirmed null-safe
- Return type: bool (expression body, no return null possible)
- CYC comment at L2025-2027 confirms CYC=1, JS-021: no lock, JS-001: no throw

**Evidence**: CopyEngine.cs L2025-2030

---

### VR-03: SyncFollowerBracket (Layer 2+3 fix — branch ordering)
**PASS**

- ATM STP branch (IsAtmSTPOrder) at L2067: `if (isStop && IsAtmSTPOrder(fo)) // (3) DW-B134`
  - Calls `SyncAtmFollowerBracket(acc, fo, newPrice);` (L2069) then `return;` (L2070) — confirmed
- IsTrailingStop guard at L2073: `if (isStop && IsTrailingStop(fo)) // (4)` — confirmed still present
- Ordering: L2067 (ATM STP) < L2073 (IsTrailingStop) — ATM STP fires FIRST (correct)
- CYC comment updated at L2044: `DW-B134: CYC=6: fo null(1), price delta(2), ATM STP(3), IsTrailingStop(4), isStop branch(5).`
- DW-B134 rationale comment at L2064-2066 present

**Branch numbering note**: The `try` body at L2081 uses an internal `// (4)` comment for the isStop
branch — this appears to be a minor comment artefact from the pre-existing code (the `(4)` in the
try body is not a CYC branch; CYC counting treats catch paths as 0). This does not affect correctness.

**Evidence**: CopyEngine.cs L2043-2098

---

### VR-04: SyncAtmFollowerBracket (new helper — cancel+resubmit)
**PASS**

- File: `src/PropTraderTools/CopyEngine.cs`
- Location: L2100-2159 (immediately after SyncFollowerBracket closing brace)
- TWO INDEPENDENT try/catch blocks confirmed:
  - Block A (L2121-2128): `acc.Cancel(new Order[] { fo });` — isolated Cancel
  - Block B (L2131-2158): `acc.CreateOrder(...)` + `acc.Submit(new[] { newStop });` — isolated CreateOrder+Submit
  - Block B runs regardless of Block A outcome (independent isolation per JS-001)
- `acc.Submit(new[] { newStop })` at L2152 — correct NT8 API (NOT newStop.Submit() or newStop?.Submit())
- Order name `"PTT-STP-Drag"` at L2143 — NT8-014 PTT- prefix satisfied
- Null guard on `newStop` at L2147: `if (newStop == null) // (3)` — confirmed present before Submit
- Null guards: `acc == null` (L2115) and `fo == null` (L2117) — both confirmed
- OQ-03 safety comment at L2111-2112: confirmed present
- CYC comment at L2103: `CYC=4: (1) acc null guard, (2) fo null guard, (3) newStop null guard in Block B`
- Two independent catch blocks — no rethrow — JS-001 compliant

**Discrepancy vs architecture plan D.4**: Plan D.4 designed a single try block. Ticket Step 4
also showed single try in the reference code. The engineer implemented TWO independent try/catch
blocks. This is STRICTLY CORRECT and matches the ticket Section 4.4 method-level spec
(`// JS-001: two independent try/catch -- Block A (Cancel) isolated from Block B`).
The engineer correctly deviated from the plan's D.4 code block in favour of the Section 4.4
specification. The result is better fault isolation. NOT a violation.

**Evidence**: CopyEngine.cs L2100-2159

---

### VR-05: B129Tests.cs — Test file content
**PASS**

- File: `src/PropTraderTools/Tests/B129Tests.cs`
- Namespace: `PropTraderTools.Tests` — correct
- Class: `B129Tests` — correct
- Framework: xUnit only (`using Xunit; [Fact]`) — no NUnit, no MSTest
- Three [Fact] methods confirmed:

| # | Method Name | Status |
|---|-------------|--------|
| 1 | `B129_DW134_STPSuffixDetectedByIsBracketLegStatic` | PRESENT |
| 2 | `B129_DW134_SyncFollowerBracketCancelResubmitFiredForAtmBracket` | PRESENT |
| 3 | `B129_DW134_OQ03_CancelledBracketDoesNotTriggerFollowerEntryCancel` | PRESENT |

All 3 method names match the ticket Section 8 contract exactly.

**Test approach discrepancy vs Layer 2 report description**: The Layer 2 report described Test 1
as using reflection via `GetMethod("IsAtmSTPOrder", BindingFlags.NonPublic...)` with indirect
string assertions. The actual implementation calls `CopyEngine.IsAtmSTPOrder(buyStop)` directly
via InternalsVisibleTo — more direct and more correct. The Layer 2 description was a pre-write
sketch; the actual implementation is superior. NOT a violation.

**All 3 tests pass**: Confirmed by SCAN-07 (see below).

---

### VR-06: PropTraderTools.csproj Compile entry
**PASS**

- File: `src/PropTraderTools/PropTraderTools.csproj`
- Line 157: `<Compile Include="Tests\B129Tests.cs" />` — confirmed present

---

## Layer 3 Independent Scan Results

### SCAN-01: lock() in new/modified code
**PASS**

Command: `Select-String -Path "src\PropTraderTools\CopyEngine.cs" -Pattern "lock\(" | Where-Object { $_.Line -notmatch "//.*lock\(" }`

Result: **0 hits** — no live lock() usage in any code (new or pre-existing).

Layer 2 report: 0 live hits. **No discrepancy.**

---

### SCAN-02: async void in new code
**PASS**

Command: `Select-String -Path "src\PropTraderTools\CopyEngine.cs" -Pattern "async void" | Where-Object { $_.Line -notmatch "//.*async void" }`

Result: **0 hits** — no async void anywhere in CopyEngine.cs.

Layer 2 report: 0 hits. **No discrepancy.**

---

### SCAN-03: return null in new methods (IsAtmSTPOrder, SyncAtmFollowerBracket)
**PASS**

Command: `Select-String -Path "src\PropTraderTools\CopyEngine.cs" -Pattern "return null" | Where-Object { $_.LineNumber -ge 2025 -and $_.LineNumber -le 2160 }`

Result: **0 hits** in line range 2025-2160 (new methods). IsAtmSTPOrder returns bool (expression body). SyncAtmFollowerBracket returns void. Neither can return null.

Pre-existing `return null` occurrences (L1613, L2216, L2262, L3561, L3567, L3645, L4478 per Layer 2) are
outside new method scope.

Layer 2 report: 0 hits in new methods. **No discrepancy.**

---

### SCAN-04: throw new in hot path
**PASS**

Command: `Select-String -Path "src\PropTraderTools\CopyEngine.cs" -Pattern "throw new" | Where-Object { $_.Line -notmatch "//.*throw new" }`

Result: **0 hits** — no throw new anywhere in CopyEngine.cs.

Layer 2 report: 0 hits. **No discrepancy.**

---

### SCAN-05: PTT-STP-Drag present (exactly 1 hit)
**PASS**

Command: `Select-String -Path "src\PropTraderTools\CopyEngine.cs" -Pattern "PTT-STP-Drag"`

Result: **1 hit** — Line 2143: `"PTT-STP-Drag",` inside SyncAtmFollowerBracket Block B (CreateOrder name arg).

Layer 2 report: 1 hit at L2143. **No discrepancy.**

---

### SCAN-06: IsTrailingStop guard still present + branch ordering
**PASS**

Command: `Select-String -Path "src\PropTraderTools\CopyEngine.cs" -Pattern "IsAtmSTPOrder|IsTrailingStop"`

Results:
- L2014: `// B10 T1 -- IsTrailingStop:` (comment)
- L2018: `private static bool IsTrailingStop(Order order)` (definition)
- L2028: `internal static bool IsAtmSTPOrder(Order order) =>` (definition)
- L2044: comment referencing both (CYC annotation)
- L2064: comment referencing IsTrailingStop (rationale)
- L2065: comment
- L2066: comment
- L2067: `if (isStop && IsAtmSTPOrder(fo)) // (3) DW-B134` (CALL — line 2067)
- L2073: `if (isStop && IsTrailingStop(fo)) // (4)` (CALL — line 2073)

Pass criteria:
1. IsAtmSTPOrder(fo) call at L2067 < IsTrailingStop(fo) call at L2073 — CONFIRMED (ATM STP fires first)
2. IsTrailingStop guard still present — CONFIRMED (not removed)

Layer 2 report: L2067 before L2073. **No discrepancy.**

---

### SCAN-07: Build clean + all B129 tests pass
**PASS**

Build command: `dotnet build src/PropTraderTools --no-incremental`
Result: **Build succeeded. 0 Warning(s). 0 Error(s).**

Test command: `dotnet test src/PropTraderTools --filter "FullyQualifiedName~B129" --no-build`
Result: **Failed: 0, Passed: 8, Skipped: 0, Total: 8, Duration: ~1s**

Tests that passed:
- (B128Tests suite matched by filter): 5 pre-existing B128/B129 integration tests
- B129Tests.B129_DW134_STPSuffixDetectedByIsBracketLegStatic (LaneB Test 1)
- B129Tests.B129_DW134_SyncFollowerBracketCancelResubmitFiredForAtmBracket (LaneB Test 2)
- B129Tests.B129_DW134_OQ03_CancelledBracketDoesNotTriggerFollowerEntryCancel (LaneB Test 3)

Layer 2 report: 8 passed, 0 failed. **No discrepancy.**

---

## Layer 2 vs Layer 3 Comparison Summary

| Scan | Layer 2 (Engineer) | Layer 3 (Verifier) | Match? |
|------|--------------------|--------------------|--------|
| SCAN-01 (lock) | 0 live hits | 0 hits | MATCH |
| SCAN-02 (async void) | 0 hits | 0 hits | MATCH |
| SCAN-03 (return null, new methods) | 0 hits in new methods | 0 hits in L2025-2160 | MATCH |
| SCAN-04 (throw new) | 0 hits | 0 hits | MATCH |
| SCAN-05 (PTT-STP-Drag) | 1 hit at L2143 | 1 hit at L2143 | MATCH |
| SCAN-06 (IsTrailingStop ordering) | L2067 < L2073 | L2067 < L2073 | MATCH |
| SCAN-07 (build + tests) | 8 passed, 0 failed | 8 passed, 0 failed | MATCH |

**No discrepancies found between Layer 2 and Layer 3 results.**

---

## DNA Rule Compliance

| Rule | Check | Result |
|------|-------|--------|
| JS-021 (no lock) | SCAN-01: 0 hits | PASS |
| JS-001 (no throw in hot path) | SCAN-04: 0 throw new; two independent try/catch | PASS |
| JS-002 (no return null) | New methods return bool/void, no null return | PASS |
| JS-008 (immutable structs) | No new structs introduced | N/A |
| NT8-014 (PTT- prefix on CreateOrder) | "PTT-STP-Drag" at L2143 | PASS |
| NT8-013 (DateTime.UtcNow) | No DateTime.Now in new code | PASS |
| SCAN-03 (FontFamily) | Not applicable — no WPF changes | N/A |
| SCAN-04 (hex colors) | Not applicable — no WPF changes | N/A |
| CYC <= 8 | IsBracketLegStatic=4, IsAtmSTPOrder=1, SyncFollowerBracket=6, SyncAtmFollowerBracket=4 | PASS |
| xUnit only | B129Tests.cs: using Xunit; [Fact] only, no NUnit/MSTest | PASS |
| ASCII-only | No non-ASCII in new methods confirmed | PASS |

---

## Spec Coverage (DW-B134 Requirements)

| Req ID | Requirement | Implemented | Line |
|--------|-------------|-------------|------|
| DW-B134-L1 | IsBracketLegStatic detects "Buy STP"/"Sell STP" | EndsWith STP clause | L3621 |
| DW-B134-L2 | IsWorkingBracket returns true for STP Working orders | Via IsBracketLegStatic fix | L2011 |
| DW-B134-L3 | TryHandleBracketDrag dispatches ATM STP drags | Via IsBracketLegStatic fix | upstream |
| DW-B134-L4 | Follower ATM STP brackets updated to new leader price | SyncAtmFollowerBracket | L2100 |
| DW-B134-L5 | acc.Change() NOT called on ATM-owned brackets | cancel+resubmit: acc.Cancel+CreateOrder+Submit | L2121-2152 |
| DW-B134-L6 | IsTrailingStop guard does NOT skip ATM STP | ATM STP branch BEFORE guard | L2067 |
| DW-B134-OQ03 | cancel+resubmit safe from TryCancelFollowerEntries | Gate 2 null-return confirmed; OQ-03 comment | L2111 |
| DW-B134-PTT | New order name starts with "PTT-" | "PTT-STP-Drag" | L2143 |

All 8 spec requirements satisfied.

---

## Notable Observations (Non-Blocking)

1. **Architecture plan D.4 vs implementation**: Plan D.4 showed a single try/catch block around both
   Cancel and CreateOrder+Submit. The ticket Section 4.4 specification (method-level) correctly describes
   two independent try/catch blocks. The engineer implemented two independent try/catch blocks (correct).
   Plan D.4 was a sketch; Section 4.4 is the authoritative spec. Implementation matches Section 4.4.

2. **Test approach vs Layer 2 description**: Layer 2 described Test 1 using reflection to access
   IsAtmSTPOrder. The actual test calls CopyEngine.IsAtmSTPOrder() directly via InternalsVisibleTo.
   The actual approach is superior (direct, readable, no reflection overhead). Not a violation.

3. **Comment artefact in SyncFollowerBracket L2081**: The try body contains `// (4)` for the isStop
   branch — this is a pre-existing comment numbering style. The CYC header correctly documents the
   new numbering (branches 1-5). Not a code defect.

---

## Overall Verdict

**VERIFY_PASS**

All VR-01 through VR-06 checks: PASS
All 7 independent scans (SCAN-01 through SCAN-07): PASS
No discrepancies between Layer 2 (engineer self-report) and Layer 3 (independent verification)
All DW-B134 spec requirements (L1-L6, OQ-03, PTT) satisfied
All Jane Street DNA rules (JS-001, JS-021, CYC<=8) satisfied
Build: 0 errors, 0 warnings
Tests: 8/8 passed (including all 3 new B129 LaneB tests)

*Verification completed: B129 LaneB Phase 4b*
*Verifier: ptt-verifier*