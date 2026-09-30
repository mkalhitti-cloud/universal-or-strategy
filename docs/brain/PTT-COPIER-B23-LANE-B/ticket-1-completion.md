# PTT-COPIER-B23-LANE-B — Ticket 1 Completion Report
# Block:   PTT-COPIER-B23
# Lane:    B
# Defect:  DW-B22-ADDRULE-ACCUMULATE-01 (P1)
# Ticket:  T1 — Replace-Not-Append in AddRule 5-arg Overload
# Date:    2026-07-16
# Result:  BUILD_PASS

---

## What Was Implemented

### Edit A — CopyEngine.cs: 5-arg AddRule overload (lines 312-336)

Replaced the original single-line append implementation of the 5-arg `AddRule` overload with a
ConcurrentBag snapshot-rebuild pattern that enforces replace-not-append semantics for the same
`(instrument, leader)` pair.

**Before (line 321):**
```csharp
_rules.Add(CopyRule.Create(instrument, master, followers, true, multipliers, atmMap));
```

**After (lines 320-336):**
```csharp
var snapshot = new List<CopyRule>(_rules);
_rules = new ConcurrentBag<CopyRule>();
foreach (var r in snapshot)
{
    if (r.Instrument == instrument && r.MasterAccount?.Name == master?.Name)
        continue;
    _rules.Add(r);
}
_rules.Add(CopyRule.Create(instrument, master, followers, true, multipliers, atmMap));
```

- Pattern matches `SetFollowerMultiplier` and `SetAtmMode` — ConcurrentBag rebuild (JS-021, no lock).
- Identity equality: `r.Instrument == instrument` (string ==) AND `r.MasterAccount?.Name == master?.Name` (name string ==, null-safe).
- CYC=4: base(1) + foreach(+1) + compound if(+1) + && short-circuit(+1).
- 3-arg overload at line 307 left completely unchanged.

### New [Fact] — CopyEngineTests.cs (line 2230)

Appended `AddRule_Replace_WhenSameInstrumentAndLeader` inside `CopyEngineTests` class before closing `}`.

Uses corrected [Fact] from `04-ticket-review.md` §Corrections:
- No `StubAccount` — uses `(Account)null` (matches existing 5-arg test pattern).
- Uses `_engine` (singleton), not `new CopyEngine()`.
- No `.First()` LINQ — uses `foreach` pattern.
- No new `using` directives required.
- Instrument: `"MES SEP26"`, sentinel multipliers: 11 (first call) and 99 (second call).

---

## 7-Scan Results (Layer 2)

### SCAN-01: lock() in code
```
Select-String -Path "CopyEngine.cs","CopyEngineTests.cs" -Pattern "lock\s*\("
```
**Result: 0 violations** — 5 matches found, all in comments (`// no lock (JS-021)`, `// try block(0)` etc.). Zero actual `lock(` statements in executable code.

### SCAN-02: async void
```
Select-String -Path "CopyEngine.cs","CopyEngineTests.cs" -Pattern "async void "
```
**Result: 0 violations** — 1 match found, in a comment (`// no await, no async void (JS-033 compliant)`). Zero `async void` method declarations.

### SCAN-03: return null (no new additions)
```
Select-String -Path "CopyEngine.cs" -Pattern "return null"
```
**Result: 0 new violations** — 4 pre-existing matches in `FindFollowerBracketOrder`, `FindRule`, and `FindPosition` methods. None introduced by this lane's edits.

### SCAN-04: MasterAccount?.Name == master?.Name
```
Select-String -Path "CopyEngine.cs" -Pattern "MasterAccount\?\.Name == master\?\.Name"
```
**Result: 1 match (expected 1)** — Line 327, in our newly added `AddRule` body. Exactly 1 match confirmed.

### SCAN-05: 3-arg overload unchanged
```
Lines 307-310 of CopyEngine.cs:
    internal void AddRule(string instrument, Account master, Account[] followers)
    {
        _rules.Add(CopyRule.Create(instrument, master, followers));
    }
```
**Result: PASS** — 3-arg overload body is unchanged.

### SCAN-06: CYC manual count — 5-arg AddRule
```
CYC calculation:
  base = 1
  foreach (var r in snapshot) = +1
  if (r.Instrument == instrument && r.MasterAccount?.Name == master?.Name) = +1
  && short-circuit = +1
  Total = CYC 4
```
**Result: CYC=4** — ≤ 8 (Jane Street strict standard). PASS.

### SCAN-07: NUnit/MSTest
```
Select-String -Path "CopyEngineTests.cs" -Pattern "\[Test\]|\[TestMethod\]|NUnit|MSTest"
```
**Result: 0 violations** — No output. Zero [Test], [TestMethod], NUnit, or MSTest references.

---

## [Fact] Count
```
Select-String -Path "CopyEngineTests.cs" -Pattern "\[Fact\]" | Measure-Object
```
**Result: 126** — Pre-edit baseline was 125. +1 new [Fact] (`AddRule_Replace_WhenSameInstrumentAndLeader`). Confirmed.

---

## Build Result
```
dotnet build src/PropTraderTools/PropTraderTools.csproj
```
**3 pre-existing errors (not introduced by this lane):**
- `AtrSizingEngine.cs:20` — CS0234: `NinjaTrader.NinjaScript.Indicators` (assembly reference, pre-existing)
- `AtrSizingEngine.cs:24` — CS0246: `Indicator` (assembly reference, pre-existing)
- `CopyEngine.cs:644` — CS8370: nullable reference types require C# 8.0+ (pre-existing)

**0 new errors from this lane's edits.**

---

## BUILD_PASS
