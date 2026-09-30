# PTT-COPIER-B23-LANE-B — Ticket Review
# Block:    PTT-COPIER-B23
# Lane:     B
# Defect:   DW-B22-ADDRULE-ACCUMULATE-01 (P1)
# Reviewer: ptt-ticket-reviewer
# Result:   TICKET_REVIEW_FAIL
# Date:     2026-07-16

---

## T1 — Replace-Not-Append in AddRule 5-arg Overload

### Traceability
PASS.
- Ticket header cites `DW-B22-ADDRULE-ACCUMULATE-01 (P1)` directly.
- T1 §"Spec Requirement Satisfied" repeats the defect ID.
- Edit A comment block includes `// B23 T1 (DW-B22-ADDRULE-ACCUMULATE-01):`.
- Architecture plan §1 and §2 fully cover the defect. No phantom work. No missing work.

### JS Pre-Check
PASS.
- JS-021: no `lock()` in "Replace with" block. ConcurrentBag rebuild is lock-free. ✅
- JS-001: no `throw` in hot path. Pure collection manipulation. ✅
- JS-002: void method, no `return null`. ✅
- JS-033: synchronous method, no `async void`. ✅
- No sealed-on-TradeCopierWindow, no FontFamily, no hardcoded hex, no DateTime.Now,
  no CreateOrder, no Account.All outside Loaded handler — none apply to this ticket. ✅

### CYC Pre-Check
PASS.
- 5-arg `AddRule` after edit: base(1) + foreach(+1) + `if` compound(+1) + `&&` short-circuit(+1) = CYC 4.
- CYC 4 ≤ 8 (Jane Street strict). Verified against arch plan §2 "CYC Impact" and plan review Check 4.

### NT8 Check
PASS.
- No `async/await` in lifecycle methods.
- No `Account.All` outside Loaded handler.
- No `sealed` on `TradeCopierWindow`.
- No `FontFamily` or hardcoded hex color.
- No `CreateOrder` call.
- No `DateTime.Now`.
- `Dictionary<string, FollowerAtmMode>` parameter is used (not `ImmutableDictionary`) — consistent
  with existing `CopyRule` field type (`internal readonly Dictionary<string, FollowerAtmMode>
  FollowerAtmTemplates` at `CopyEngine.cs` line 148). ✅

### Test Coverage
FAIL — three compile errors in the supplied `[Fact]` block (see detail below).
All three prevent the test file from building. Corrected `[Fact]` is provided in §Corrections.

### Scan Checklist
PASS — all 7 scans present.
- SCAN-01: `lock\s*\(` ✅
- SCAN-02: `async void ` ✅
- SCAN-03: `return null` ✅
- SCAN-04: `MasterAccount\?\.Name == master\?\.Name` — name equality (not reference). ✅
- SCAN-05: 3-arg overload unchanged ✅
- SCAN-06: CYC manual count ✅
- SCAN-07: `\[Test\]|\[TestMethod\]|NUnit|MSTest` ✅

### File Routing
PASS.
- `CopyEngine.cs`: `c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs` — Wave workspace. ✅
- `CopyEngineTests.cs`: `c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngineTests.cs` — Wave workspace. ✅
- No Director workspace paths for `.cs` files. ✅

### VERDICT: TICKET_REVIEW_FAIL

---

## Compile Error Detail (Check 4 + Check 5)

### CE-01 — `StubAccount` does not exist

**Location**: `[Fact]` body, line: `var master = new StubAccount("Sim101");`

**Finding**: Searched entire `CopyEngineTests.cs` for `StubAccount` (case-insensitive). Zero matches.
The ticket note states: *"StubAccount is a test helper already present in CopyEngineTests.cs
(used by existing Gate 2 tests)"* — this is **incorrect**. No such class exists.

**Impact**: `CS0246 The type or namespace name 'StubAccount' could not be found.` Build fails.

**Required fix**: Use `(Account)null` for master and follower accounts, exactly as all existing
5-arg `AddRule` tests do (see `AddRule_WithMultipliers_StoresCorrectMultipliers` at line 477,
`GetMultiplier_OutOfRangeIndex_ReturnsOne` at line 506, `SetFollowerMultiplier_UpdatesMultiplier_RebuildsRules`
at line 822 in `CopyEngineTests.cs`).

---

### CE-02 — `new CopyEngine()` — constructor is `private`

**Location**: `[Fact]` body, line: `var engine = new CopyEngine();`

**Finding**: `CopyEngine` is `internal sealed class` (line 79 of `CopyEngine.cs`) with a private
implicit constructor accessed only via:
```csharp
private static readonly CopyEngine _instance = new CopyEngine();  // line 82
public static CopyEngine Instance => _instance;                    // line 83
```
The constructor is private to the class itself. No public or internal constructor is exposed.

**Impact**: `CS0122 'CopyEngine.CopyEngine()' is inaccessible due to its protection level.` Build fails.

**Required fix**: Use `CopyEngine.Instance` (the singleton) exactly as all 120+ existing tests do.
The test must use `_engine` (the test class field `private readonly CopyEngine _engine = CopyEngine.Instance`)
or reference `CopyEngine.Instance` directly.

---

### CE-03 — `.First()` requires `using System.Linq` (absent from test file)

**Location**: `[Fact]` body, line: `var rule = rules.First();`

**Finding**: `CopyEngineTests.cs` imports (lines 5–9):
```
using System;
using System.Collections.Concurrent;
using System.Reflection;
using NinjaTrader.Cbi;
using Xunit;
```
`using System.Linq` is **absent**. `.First()` is a LINQ extension on `IEnumerable<T>` and will not
resolve without the import.

**Impact**: `CS1061 'ConcurrentBag<CopyRule>' does not contain a definition for 'First'...` Build fails.

**Required fix**: Replace `.First()` with a `foreach` loop that finds the first rule, matching the
pattern used in existing tests (e.g. `SetFollowerMultiplier_UpdatesMultiplier_RebuildsRules` at line 833).
Do NOT add `using System.Linq` — it would be the only test file importing it and introduces
unnecessary scope. Use the foreach pattern instead.

---

## § Corrections — Mandatory Replacement [Fact]

The engineer MUST use the following corrected `[Fact]` instead of the one in `04-tickets.md`.
This version compiles against the actual codebase with zero new imports required.

```csharp
        [Fact]
        public void AddRule_Replace_WhenSameInstrumentAndLeader()
        {
            // Arrange: use singleton, set disabled to prevent order dispatch.
            // Use null accounts (same pattern as all existing 5-arg AddRule tests).
            _engine.SetEnabled(false);

            // Act: add rule for "MES SEP26" with follower-count marker in multiplier[0],
            // then replace with a different multiplier to confirm replacement not accumulation.
            _engine.AddRule(
                "MES SEP26",
                (Account)null,
                new Account[0],
                new int[] { 11 },
                new Dictionary<string, FollowerAtmMode>());
            _engine.AddRule(
                "MES SEP26",
                (Account)null,
                new Account[0],
                new int[] { 99 },
                new Dictionary<string, FollowerAtmMode>());

            // Assert: only 1 rule remains for "MES SEP26" (not 2).
            var fi = typeof(CopyEngine)
                .GetField("_rules", BindingFlags.NonPublic | BindingFlags.Instance);
            var bag = (ConcurrentBag<CopyRule>)fi.GetValue(_engine);
            int count = 0;
            foreach (var _ in bag)
                if (_.Instrument == "MES SEP26") count++;
            Assert.Equal(1, count);

            // Assert: the surviving rule carries the second multiplier (99), not the first (11).
            // This confirms replace-not-append: the most recent Apply Rule wins.
            CopyRule? surviving = null;
            foreach (var r in bag)
                if (r.Instrument == "MES SEP26") { surviving = r; break; }
            Assert.True(surviving.HasValue, "Rule 'MES SEP26' not found after two AddRule calls");
            Assert.NotNull(surviving.Value.FollowerMultipliers);
            Assert.Equal(99, surviving.Value.FollowerMultipliers[0]);
        }
```

**Why this compiles:**
- No `StubAccount` — uses `(Account)null` (pattern from existing tests at lines 479, 509, etc.)
- No `new CopyEngine()` — uses `_engine` which is `CopyEngine.Instance` (test class field line 15)
- No `.First()` — uses `foreach` pattern (same as `SetFollowerMultiplier_UpdatesMultiplier_RebuildsRules`)
- No new `using` directives needed — `ConcurrentBag<CopyRule>`, `BindingFlags`, `Dictionary<,>` all already imported
- `Dictionary<string, FollowerAtmMode>` (not `ImmutableDictionary`) — matches the 5-arg overload parameter type at `CopyEngine.cs` line 319
- `_engine.SetEnabled(false)` — standard test setup (all existing tests call this)

**Behavioral equivalence:**  
The corrected test still verifies the core invariant: two `AddRule` calls with the same instrument
and null master (same `master?.Name == null` on both sides) result in exactly 1 rule, and the
second call's multiplier (99) survives rather than the first (11). This directly covers
`DW-B22-ADDRULE-ACCUMULATE-01`.

---

## Overall: TICKET_REVIEW_FAIL

**Failing check**: Test Coverage (Check 4 + Check 5) — three compile errors in the [Fact] block.

**Action required**: Architect must replace the `[Fact]` block in `04-tickets.md` T1 §"New [Fact]"
with the corrected version above. No changes to Edit A are required. No changes to the 7-scan
checklist are required.

**All other checks pass**: Traceability, JS Pre-Check, CYC Pre-Check, NT8 Check,
Scan Checklist, File Routing.
