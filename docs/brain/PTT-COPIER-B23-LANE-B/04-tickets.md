# PTT-COPIER-B23-LANE-B — Ticket File
# Block:  PTT-COPIER-B23
# Lane:   B
# Defect: DW-B22-ADDRULE-ACCUMULATE-01 (P1)
# Status: TICKETS_COMPLETE
# Date:   2026-07-16

---

## Preamble

**Source plan**: `docs/brain/PTT-COPIER-B23-LANE-B/02-architecture-plan.md`
**Spec requirement**: `DW-B22-ADDRULE-ACCUMULATE-01` (P1) — `AddRule()` 5-arg overload
appends to `_rules` ConcurrentBag without removing existing rule for same
`(instrument, master)` pair. Stale persisted rules from prior sessions fire instead of
the newly applied rule.
**xUnit baseline entering this ticket**: 122 `[Fact]` tests (or 123 if Lane A ran first).
**xUnit count after ticket**: baseline + 1 (net +1).
**Tickets in this lane**: 1

---

## T1 — Replace-Not-Append in AddRule 5-arg Overload

### Spec Requirement Satisfied
`DW-B22-ADDRULE-ACCUMULATE-01` — rebuild `_rules` ConcurrentBag in `AddRule()` 5-arg
overload to evict any existing rule for the same `(instrument, leader)` before adding.

### Write-Set

| File | Absolute path |
|------|---------------|
| `CopyEngine.cs` | `c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs` |
| `CopyEngineTests.cs` | `c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngineTests.cs` |

**DO NOT TOUCH**: `TradeCopierPanel.cs`, `TradeCopierWindow.cs`, `TradeCopierAddOn.cs`,
`AtrSizingEngine.cs`, any `.md` files.
**DO NOT TOUCH**: The 3-arg `AddRule(string, Account, Account[])` overload at line 307 —
it is used by 122 existing tests and must remain unchanged.

---

### Edit A — CopyEngine.cs: AddRule 5-arg overload (lines 314–322)

**Find this exact block**:

```csharp
        // B8 T1: new 5-arg overload -- adds multipliers + ATM map at apply time
        // JS-021: no lock -- ConcurrentBag.Add is lock-free
        internal void AddRule(
            string instrument,
            Account master,
            Account[] followers,
            int[] multipliers,
            Dictionary<string, FollowerAtmMode> atmMap)
        {
            _rules.Add(CopyRule.Create(instrument, master, followers, true, multipliers, atmMap));
        }
```

**Replace with**:

```csharp
        // B8 T1: new 5-arg overload -- adds multipliers + ATM map at apply time
        // B23 T1 (DW-B22-ADDRULE-ACCUMULATE-01): replace-not-append for same (instrument, leader).
        // ConcurrentBag rebuild pattern -- no lock (JS-021). Same pattern as SetFollowerMultiplier.
        // CYC=4: foreach(1) + string == (2) + name == (3) + continue(4 -- implicit else branch).
        internal void AddRule(
            string instrument,
            Account master,
            Account[] followers,
            int[] multipliers,
            Dictionary<string, FollowerAtmMode> atmMap)
        {
            var snapshot = new List<CopyRule>(_rules);
            _rules = new ConcurrentBag<CopyRule>();
            foreach (var r in snapshot)
            {
                if (r.Instrument == instrument && r.MasterAccount?.Name == master?.Name)
                    continue;
                _rules.Add(r);
            }
            _rules.Add(CopyRule.Create(instrument, master, followers, true, multipliers, atmMap));
        }
```

**Constraints**:
- The `r.MasterAccount?.Name == master?.Name` null-conditional is required — accounts may
  be null in test contexts. Do not use `r.MasterAccount == master` (reference equality
  breaks after NT8 reconnect — same root cause as DW-B19-COPIER-BUG-01).
- CYC = 4 for the updated method. Within ≤ 8 limit.
- JS-021: no `lock()` — `_rules` reassignment is on the UI thread (called from
  `OnApplyRule` which is a WPF Click handler — UI thread). Same thread-safety model as
  `SetFollowerMultiplier`.

---

### New [Fact] — CopyEngineTests.cs

**Method name**: `AddRule_Replace_WhenSameInstrumentAndLeader`

**Append inside `CopyEngineTests` class before the closing `}`**:

```csharp
        [Fact]
        public void AddRule_Replace_WhenSameInstrumentAndLeader()
        {
            // Arrange: fresh engine, two AddRule calls with same (instrument, master.Name).
            var engine = new CopyEngine();
            var master = new StubAccount("Sim101");
            var follower1 = new StubAccount("Sim102");
            var follower2 = new StubAccount("Sim103");

            // Act: add rule with Sim102 follower, then replace with Sim103 follower.
            engine.AddRule("MES SEP26", master, new Account[] { follower1 },
                new int[] { 1 }, new Dictionary<string, FollowerAtmMode>());
            engine.AddRule("MES SEP26", master, new Account[] { follower2 },
                new int[] { 1 }, new Dictionary<string, FollowerAtmMode>());

            // Assert: only 1 rule remains (not 2).
            var rules = (ConcurrentBag<CopyRule>)typeof(CopyEngine)
                .GetField("_rules", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(engine);
            Assert.Equal(1, rules.Count);

            // Assert: the surviving rule has Sim103 as follower (most recent Apply Rule wins).
            var rule = rules.First();
            Assert.Equal("Sim103", rule.FollowerAccounts[0].Name);
        }
```

**Note for engineer**: `StubAccount` is a test helper already present in `CopyEngineTests.cs`
(used by existing Gate 2 tests). Use the same stub. If it doesn't accept a name param,
adapt to match the existing stub pattern. `CopyRule.FollowerAccounts` is `Account[]` — access
via the existing `CopyRule` property or reflection as used in existing tests.

---

### 7-Scan Checklist

**SCAN-01 — JS-021: No `lock()`**
```powershell
Select-String -Path "CopyEngine.cs","CopyEngineTests.cs" -Pattern "lock\s*\("
```
Expected: **0 new matches**.

**SCAN-02 — JS-033: No `async void`**
```powershell
Select-String -Path "CopyEngine.cs","CopyEngineTests.cs" -Pattern "async void "
```
Expected: **0 matches**.

**SCAN-03 — JS-002: No new `return null`**
```powershell
Select-String -Path "CopyEngine.cs" -Pattern "return null"
```
Expected: no new `return null` added.

**SCAN-04 — Name equality used (not reference equality)**
```powershell
Select-String -Path "CopyEngine.cs" -Pattern "MasterAccount\?\.Name == master\?\.Name"
```
Expected: **1 match** in the new `AddRule` overload. If `MasterAccount == master` (reference
equality) appears instead, that is wrong.

**SCAN-05 — 3-arg overload unchanged**
```powershell
Select-String -Path "CopyEngine.cs" -Pattern "internal void AddRule\(string" | Select-Object Line
```
Expected: 2 matches (3-arg and 5-arg). Verify 3-arg still contains only `_rules.Add(...)`.

**SCAN-06 — CYC: AddRule 5-arg ≤ 8**
Manual inspection. Count: foreach(1), string ==(2), Name ==(3), continue else(4). CYC = 4.

**SCAN-07 — Test framework: No NUnit / MSTest**
```powershell
Select-String -Path "CopyEngineTests.cs" -Pattern "\[Test\]|\[TestMethod\]|NUnit|MSTest"
```
Expected: **0 matches**.

---

### Success Criteria

| # | Criterion | Verification |
|---|-----------|--------------|
| 1 | 5-arg `AddRule` rebuilds ConcurrentBag before adding new rule | Read `CopyEngine.cs` — snapshot/rebuild pattern present |
| 2 | Name equality used: `r.MasterAccount?.Name == master?.Name` | SCAN-04 returns 1 match |
| 3 | 3-arg `AddRule` overload unchanged | SCAN-05 + read file |
| 4 | New `[Fact]` `AddRule_Replace_WhenSameInstrumentAndLeader` added | Read `CopyEngineTests.cs` |
| 5 | `[Fact]` count = baseline + 1 | `Select-String -Pattern "\[Fact\]" CopyEngineTests.cs \| Measure-Object` |
| 6 | All 7 scans pass (0 violations) | Run SCAN-01 through SCAN-07 |
| 7 | `dotnet build` passes 0 errors | Run in `c:\WSGTA\universal-or-strategy` |

---

## TICKETS_COMPLETE
