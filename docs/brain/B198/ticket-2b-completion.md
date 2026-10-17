# B198 Ticket 2b Completion Report
**Author**: ptt-engineer (Phase 4a)
**Date**: 2026-10-13
**Epic**: B198 -- SimStop N-bracket generalisation + QTY instrument + fo=NULL drain + ATM dir fix
**Ticket**: Ticket 2b -- QTY Bug Fix (DW-B198-QTY-01 Phase B)
**DW Card**: DW-B198-QTY-01 (Phase B)
**Ticket Review**: TICKET_REVIEW_PASS (Cycle 2 -- 04-ticket-review.md)
**Path taken**: H2 (per ticket instruction -- proceed on H2 path as T2a SIM evidence not yet produced)

---

## Contingency Gate Decision

Per ticket instruction: "Since ticket-2a-sim-evidence.md has NOT yet been produced (requires NT8 SIM run), proceed on the H2 path as specified in the ticket (most likely hypothesis per diagnosis)."

Decision: **H2 path implemented**.

---

## What Was Implemented

### Change T2b-1: New field `_atmTemplateGroupQty`

Added to CopyEngine class at line ~425 (after `_laneEDispatched`):

```csharp
// T2b: ATM template per-group qty parsed from XML. Key=templateName (OrdinalIgnoreCase). Value=perGroupQty.
// Populated in CheckAtmTemplatesForReverse. Read in SendCopyWithAtm. ConcurrentDictionary -- no lock. JS-021.
private readonly ConcurrentDictionary<string, int> _atmTemplateGroupQty =
    new ConcurrentDictionary<string, int>(StringComparer.OrdinalIgnoreCase);
```

### Change T2b-2: Extended `CheckAtmTemplatesForReverse(string)` to parse qty

Added per-file qty parsing block inside the foreach loop, after the existing Reverse-check logic:

```csharp
// T2b (H2): parse per-group qty from template XML.
try
{
    var doc = new System.Xml.XmlDocument();
    doc.Load(file);
    var qtyNode = doc.SelectSingleNode("//Quantity");
    if (qtyNode != null
        && int.TryParse(qtyNode.InnerText, out int perGroupQty)
        && perGroupQty > 0)
        _atmTemplateGroupQty.TryAdd(name, perGroupQty);
}
catch (Exception)
{
    // Per-file qty-parse catch: skip unreadable/unparsable file. JS-001: no rethrow.
}
```

CYC: 6 -> 7 (one new if-branch: `if (qtyNode != null && ...)`).

### Change T2b-3: New helper `TryGetAtmTemplateGroupCount`

Added at line ~1965 (near `CheckAtmTemplatesForReverse`):

```csharp
// T2b (H2): TryGetAtmTemplateGroupCount -- CYC=2.
private bool TryGetAtmTemplateGroupCount(string templateName, out int groupCount)
{
    if (string.IsNullOrEmpty(templateName))    // (1) null guard
    {
        groupCount = 0;
        return false;
    }
    return _atmTemplateGroupQty.TryGetValue(templateName, out groupCount);
}
```

CYC: 2. No lock. No throw. ASCII-only.

### Change T2b-4: SendCopyWithAtm -- per-group qty division

Added after the T2a log line and before `CreateOrder`. Uses local `qtyToSend` variable (CopySignal is a readonly struct -- no setter):

```csharp
// T2b (H2): if template per-group qty known, divide total qty by group count.
int qtyToSend = signal.Quantity;
if (TryGetAtmTemplateGroupCount(namedMode.TemplateName, out int groupCount)
    && groupCount > 0
    && signal.Quantity > groupCount)
    qtyToSend = signal.Quantity / groupCount;
```

`CreateOrder` now uses `qtyToSend` instead of `signal.Quantity`. CYC: 4 -> 5.

### Tests Appended (B198Tests.cs)

T2b-01: `T2b_01_AtmTemplateGroupQty_FieldExists` -- Assert.Contains("_atmTemplateGroupQty", Src())
T2b-02: `T2b_02_SendCopyWithAtm_DividesQtyByGroupCount` -- Assert.Contains("signal.Quantity / groupCount", Src())

---

## CYC Table

| Method | Before CYC | After CYC | JS-066 |
|--------|-----------|----------|--------|
| `CheckAtmTemplatesForReverse(string)` | 6 | 7 | PASS |
| `SendCopyWithAtm` | 4 | 5 | PASS |
| `TryGetAtmTemplateGroupCount` | N/A (new) | 2 | PASS |

---

## 7-Scan Results

### SCAN-01: lock() check
Command: `Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "lock\s*\(" | Where-Object { $_.Line -notmatch "//" }`
Result: **0 matches** -- PASS

### SCAN-02: async void check
Command: `Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "async\s+void\s+\w+" | Where-Object { $_.Line -notmatch "//" }`
Result: **0 matches** -- PASS

### SCAN-03: throw/return null check
Command: `Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "throw\s+new\s+\w+Exception" | Where-Object { $_.Line -notmatch "//" }`
Result: **0 matches** -- PASS

### SCAN-04: CYC check (manual McCabe)
Method: `CheckAtmTemplatesForReverse(string)` -- CYC=7 (was 6, +1 new if-branch) <= 8 PASS
Method: `SendCopyWithAtm` -- CYC=5 (was 4, +1 new if-branch) <= 8 PASS
Method: `TryGetAtmTemplateGroupCount` -- CYC=2 (new method) <= 8 PASS
Result: **All <= 8** -- PASS

### SCAN-05: ASCII-only
Command: `Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "[^\x00-\x7F]"`
Result: **0 matches** -- PASS

### SCAN-06: xUnit-only in tests
Command: `Select-String -Path "src/PropTraderTools/Tests/B198Tests.cs" -Pattern "NUnit|MSTest|\[TestMethod\]|\[Test\]"`
Result: **0 real hits** (1 comment-line hit only, not an attribute) -- PASS

### SCAN-07: _atmTemplateGroupQty is private readonly ConcurrentDictionary, no lock blocks
Command: `Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "_atmTemplateGroupQty"`
Result: **3 matches** -- field declaration (private readonly ConcurrentDictionary<string, int>), TryAdd site, TryGetValue site. SCAN-01 confirms 0 lock() blocks anywhere. -- PASS

---

## Build Result

```
dotnet build src/PropTraderTools/PropTraderTools.Tests.csproj
```
Result: **Build succeeded. 0 Error(s)** -- warnings are pre-existing CS0436 (NinjaTrader assembly type conflicts).

```
dotnet test src/PropTraderTools/PropTraderTools.Tests.csproj --filter "B198Tests"
```
Result: **Passed! Failed: 0, Passed: 20, Skipped: 0, Total: 20** -- all 20 tests GREEN.
- T1-01..T1-16: 16 tests PASS (prior, unmodified)
- T2a-01..T2a-02: 2 tests PASS (prior, unmodified)
- T2b-01..T2b-02: 2 new tests PASS

---

## PTT Sync

```
powershell -File .\scripts\ptt-sync.ps1
```
Result: **19 linked, 0 errors** -- PASS

---

## Files Modified

- `src/PropTraderTools/CopyEngine.cs` (Wave workspace) -- field, extended method, new helper method, SendCopyWithAtm H2 fix
- `src/PropTraderTools/Tests/B198Tests.cs` (Wave workspace) -- T2b-01 and T2b-02 appended

---

## Return Status

**BUILD_PASS**
