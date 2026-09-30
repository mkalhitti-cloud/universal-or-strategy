# PTT-COPIER-B2 — Ticket 1 Completion Report

**Ticket**: T1 — CopyEngine.cs  
**File**: `src/PropTraderTools/CopyEngine.cs`  
**Status**: BUILD_PASS  
**Date**: 2026-06-16  

---

## Changes Applied

### Change 1 — Replace `List<CopyRule>` with `ConcurrentBag<CopyRule>` (line 21)

**Before:**
```csharp
private readonly List<CopyRule> _rules = new List<CopyRule>();
```

**After:**
```csharp
private readonly ConcurrentBag<CopyRule> _rules = new ConcurrentBag<CopyRule>();
```

- `System.Collections.Concurrent` was already imported at line 5. No new `using` required.
- `System.Collections.Generic` retained at line 6 (still needed by `IEnumerable<Account>` at line 310).
- Final line: **21**

---

### Change 2 — New `AddRule` overload (after line 96)

**Before:** (single overload only)
```csharp
internal void AddRule(CopyRule rule)
{
    _rules.Add(rule);
}
```

**After:** (original retained, new overload added immediately below)
```csharp
internal void AddRule(CopyRule rule)
{
    _rules.Add(rule);
}

internal void AddRule(string instrument, Account master, Account[] followers)
{
    _rules.Add(CopyRule.Create(instrument, master, followers));
}
```

- New overload starts at line **98** in the final file.
- Allows UI surfaces (`TradeCopierWindow`, `TradeCopierPanel`) to call `AddRule` with primitives without needing access to the private `CopyRule` struct.

---

## Scan Results (all 9 — all PASS)

| # | Scan | Pattern | Result | Status |
|---|------|---------|--------|--------|
| SCAN-01 | lock( | `lock\s*\(` | 0 results | ✅ PASS |
| SCAN-02 | non-ASCII | `[^\x00-\x7F]` | 0 results | ✅ PASS |
| SCAN-03 | FontFamily | `FontFamily` | 0 results | ✅ PASS |
| SCAN-04 | hex color | `#[0-9A-Fa-f]{6}` | 0 results | ✅ PASS |
| SCAN-05 | PTT- prefix | `"PTT-` | Confirmed — PTT-Copy (L180), PTT-Trim (L218), PTT-Flatten (L255) | ✅ PASS |
| SCAN-06 | DateTime.Now | `DateTime\.Now[^U]` | 0 results | ✅ PASS |
| SCAN-07 | lock\s*( | `\block\s*\(` | 0 results | ✅ PASS |
| SCAN-B2-01 | ConcurrentBag | `ConcurrentBag` | 1 result (L21) | ✅ PASS |
| SCAN-B2-02 | List\<CopyRule\> | `List<CopyRule>` | 0 results | ✅ PASS |

---

## Final File Structure (key lines)

| Line | Content |
|------|---------|
| 5 | `using System.Collections.Concurrent;` |
| 6 | `using System.Collections.Generic;` |
| 21 | `private readonly ConcurrentBag<CopyRule> _rules = new ConcurrentBag<CopyRule>();` |
| 93–96 | Original `AddRule(CopyRule rule)` overload |
| 98–101 | New `AddRule(string instrument, Account master, Account[] followers)` overload |
| 180 | `"PTT-Copy"` order name |
| 218 | `"PTT-Trim"` order name |
| 255 | `"PTT-Flatten"` order name |

---

## Constraints Verified

- ✅ No `lock()` added  
- ✅ No `async`/`await` added  
- ✅ `OnOrderUpdate` gate chain untouched  
- ✅ `TradeCopierWindow.cs` / `TradeCopierPanel.cs` not touched  
- ✅ `TrimSignal` struct retained (dead code, accepted)  
- ✅ Only the two listed changes applied  
