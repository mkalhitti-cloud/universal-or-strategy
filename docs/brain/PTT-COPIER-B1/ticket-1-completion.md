# Ticket T1 Completion Report -- CopyEngine.cs

**Ticket:** T1 -- CopyEngine.cs  
**Epic:** PTT-COPIER-B1  
**Date:** 2026-07-06  
**Engineer:** PTT Engineer (Bob IDE)

---

## File Written

**Path:** `c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs`  
**Line count:** 346

---

## 7-Scan Results

### SCAN-01 -- No `lock()` (grep)
```
Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "lock\("
```
**Result:** 0 results -- PASS

### SCAN-02 -- ASCII-only
```
Get-Content src/PropTraderTools/CopyEngine.cs | Where-Object {$_ -match '[^\x00-\x7F]'}
```
**Result:** 0 results -- PASS

### SCAN-03 -- No FontFamily
```
Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "FontFamily"
```
**Result:** 0 results -- PASS

### SCAN-04 -- No hardcoded hex colors
```
Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "#[0-9A-Fa-f]{6}"
```
**Result:** 0 results -- PASS

### SCAN-05 -- PTT- prefix on all CreateOrder calls
```
Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "CreateOrder"
```
3 CreateOrder calls found. Each verified to carry a PTT- prefixed name parameter:
- Line 165 (SendCopy): `"PTT-Copy"` at line 175
- Line 203 (Trim): `"PTT-Trim"` at line 213
- Line 240 (Flatten): `"PTT-Flatten"` at line 250

**Result:** 0 violations -- PASS

### SCAN-06 -- No DateTime.Now
```
Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "DateTime\.Now[^U]"
```
**Result:** 0 results -- PASS

### SCAN-07 -- No `lock()` (regex, belt-and-suspenders)
```
Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "\block\s*\("
```
**Result:** 0 results -- PASS

---

## Methods Implemented

1. `private CopyEngine()` -- singleton private constructor (JS-010)
2. `internal void SetEnabled(bool enabled)` -- volatile bool write + StatusUpdate fire (JS-023)
3. `internal void AddRule(CopyRule rule)` -- adds rule to _rules list
4. `internal void Subscribe()` -- Account.All.OrderUpdate += OnOrderUpdate
5. `internal void Unsubscribe()` -- Account.All.OrderUpdate -= OnOrderUpdate
6. `private void OnOrderUpdate(object sender, OrderEventArgs e)` -- 4-gate chain (JS-001, JS-021, JS-023, JS-025)
7. `private bool SendCopy(Account follower, Instrument instrument, in CopySignal signal)` -- PTT-Copy order, returns bool, no throw (JS-001)
8. `internal void Trim(Instrument instrument)` -- ceil(qty/2), PTT-Trim (SCAN-05)
9. `internal void Flatten(Instrument instrument)` -- full qty, PTT-Flatten (SCAN-05)
10. `internal void CancelPendingEntries(Instrument instrument)` -- working non-bracket cancel
11. `private bool IsDedup(string orderId)` -- 10-second TTL, ConcurrentDictionary (JS-025)
12. `private IEnumerable<Account> AllAccounts(Instrument instrument)` -- instrument fence (JS-003)
13. `private CopyRule? FindRule(Instrument instrument)` -- linear search over _rules
14. `private bool PassesDailyCapCheck(Account acc)` -- Phase 1 stub, returns true
15. `private bool IsBracketLeg(Order order)` -- 3-layer guard (JS-003)

---

## Structs Implemented

1. `private readonly struct CopyRule` -- private ctor + static Create() (JS-008, JS-010)
   - Fields: Instrument (string), MasterAccount (Account), FollowerAccounts (Account[])
2. `private readonly struct CopySignal` -- private ctor + static Create() (JS-008, JS-010)
   - Fields: Action, Type, Quantity, LimitPrice, OrderId
3. `private readonly struct TrimSignal` -- NO qty field (JS-003), private ctor + static Create() (JS-008, JS-010)
   - Fields: UtcTime (DateTime.UtcNow), Instrument (string)

---

## Deviations from Ticket Spec

None. All methods, fields, and structs implemented exactly as specified in T1.

Notes:
- `_rules` uses `List<CopyRule>` as specified for Block 1 multi-rule support
- `SendCopy` signature includes `Instrument` parameter as required by the NT8 CreateOrder API
- `TrimSignal` struct is declared in the class (correctness by construction guard) but Trim() uses AllAccounts + live position reads as specified -- TrimSignal.Create() is available for callers that need the stamp
- `IsBracketLeg` exactly matches the 3-layer guard from architecture plan Section 6

---

## Jane Street Rule Compliance

| Rule | Applied Where |
|------|---------------|
| JS-001 | OnOrderUpdate (no throw, early returns), SendCopy (returns bool, catch/return false) |
| JS-003 | TrimSignal (no qty field), IsBracketLeg (3-layer guard), AllAccounts (instrument fence) |
| JS-008 | CopyRule, CopySignal, TrimSignal (all readonly struct, all fields readonly) |
| JS-010 | CopyRule, CopySignal, TrimSignal, CopyEngine (private constructors + static Create()) |
| JS-021 | No lock() anywhere -- volatile + ConcurrentDictionary only |
| JS-023 | _isCopyEnabled declared volatile |
| JS-025 | _dedupCache is ConcurrentDictionary<string, long> |
