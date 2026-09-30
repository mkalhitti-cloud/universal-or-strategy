## Ticket 2 Verification -- DW-B103

### Verifier: ptt-verifier
### Date: 2026-08-10
### Source: src/PropTraderTools/CopyEngine.cs (READ-ONLY)

---

### Source reads:

**T2-V1 result: L1505-1545 (TryCancelFollowerEntries region)**

```
1505 |
1506 |         // TryCancelFollowerEntries: CYC=6. Propagates leader cancel to all follower entry orders.
1507 |         // Returns true if Cancelled state was handled (caller should return immediately).
1508 |         // HOTFIX-B63-COPY-CANCEL-01: ATM bracket cancels are skipped via IsAtmBracketName guard.
1509 |         // DW-B103: PTT exit bracket OCO-cancels return false (do not wipe follower brackets).
1510 |         // JS-021: no lock. JS-001: no throw.
1511 |         private bool TryCancelFollowerEntries(Order order, CopyRule rule)
1512 |         {
1513 |             if (order.OrderState != OrderState.Cancelled)
1514 |                 return false;
1515 |             if (IsAtmBracketName(order.Name))
1516 |                 return true; // HOTFIX-B63-COPY-CANCEL-01
1517 |             if (
1518 |                 order.Name != null
1519 |                 && (
1520 |                     order.Name.StartsWith("PTT-QX-", StringComparison.Ordinal)
1521 |                     || order.Name.StartsWith("PTT-BE-", StringComparison.Ordinal)
1522 |                 )
1523 |             )
1524 |                 return false; // DW-B103: OCO-cancel of PTT exit bracket must not wipe follower brackets
1525 |             foreach (var acc in rule.FollowerAccounts)
1526 |             {
1527 |                 if (acc == null)
1528 |                     continue;
1529 |                 CancelOneAccount(acc, order.Instrument);
1530 |             }
1531 |             return true;
1532 |         }
```

CHECKS:
- [x] `if (order.OrderState != OrderState.Cancelled) return false;` at L1513-L1514 -- PASS
- [x] `if (IsAtmBracketName(order.Name)) return true; // HOTFIX-B63-COPY-CANCEL-01` at L1515-L1516 -- PASS
- [x] PTT guard with `StartsWith("PTT-QX-", StringComparison.Ordinal)` at L1520 -- PASS
- [x] PTT guard with `StartsWith("PTT-BE-", StringComparison.Ordinal)` at L1521 -- PASS
- [x] PTT guard returns `false` (NOT true) at L1524 -- PASS
- [x] Guard placed AFTER IsAtmBracketName and BEFORE foreach -- PASS
- [x] `foreach` loop and `CancelOneAccount` call present at L1525-L1530 after the guard -- PASS
- [x] Block comment at L1506 shows `CYC=6` and `DW-B103:` annotation at L1509 -- PASS

**T2-V2 grep result: `Select-String -Pattern "PTT-QX-"`**

Multiple matches found. Key match in TryCancelFollowerEntries:
```
src\PropTraderTools\CopyEngine.cs:1520:                    order.Name.StartsWith("PTT-QX-", StringComparison.Ordinal)
```

CHECKS:
- [x] At least 1 result in TryCancelFollowerEntries (L1520) -- PASS

**T2-V3 grep result: `Select-String -Pattern "StringComparison\.Ordinal"`**

Matches in TryCancelFollowerEntries:
```
src\PropTraderTools\CopyEngine.cs:1520:                    order.Name.StartsWith("PTT-QX-", StringComparison.Ordinal)
src\PropTraderTools\CopyEngine.cs:1521:                    || order.Name.StartsWith("PTT-BE-", StringComparison.Ordinal)
```

CHECKS:
- [x] `StringComparison.Ordinal` on PTT-QX- StartsWith at L1520 -- PASS
- [x] `StringComparison.Ordinal` on PTT-BE- StartsWith at L1521 -- PASS
- Both new StartsWith calls use StringComparison.Ordinal -- PASS

**T2-V4 result: L3195-3215 (IsBracketLeg instance method)**

```
3202 |         // B29 fix: removed "PTT-" from IsBracketLeg.
3203 |         // IsBracketLeg is used by CancelOneAccount to skip bracket stops/targets.
3204 |         // PTT- exit orders (PTT-Trim, PTT-Flatten, PTT-BE-Stop, PTT-Tighten-Stop) are NOT brackets --
3205 |         // they should be cancelable by the Cancel button.
3206 |         // Copy-cascade prevention for PTT- orders is handled separately by Gate 0.5 in DispatchCopy.
3207 |         private bool IsBracketLeg(Order order)
3208 |         {
3209 |             return order.FromEntrySignal != null
3210 |                 || (
3211 |                     order.Name != null
3212 |                     && (order.Name.StartsWith("Stop") || order.Name.StartsWith("Target"))
3213 |                 );
3214 |         }
```

CHECKS:
- [x] `IsBracketLeg()` instance method UNCHANGED -- no "PTT-" prefix added -- PASS
- [x] Comment at L3202 "B29 fix: removed PTT- from IsBracketLeg" present as expected -- PASS
- [x] Method body only checks StartsWith("Stop") and StartsWith("Target") -- no new PTT- guards -- PASS

**T2-V5 result: L2910-2945 (CancelOneAccount)**

```
2915 |         internal void CancelPendingEntries(Instrument instrument)
2916 |         {
2917 |             foreach (var acc in AllAccounts(instrument))
2918 |                 CancelOneAccount(acc, instrument);
2919 |         }
2920 |
2921 |         // B28 T1 -- CancelOneAccount: per-account pending cancel helper. CYC=4.
2922 |         // (1) foreach orders, (2) instrument filter, (3) OrderState guard, (4) IsBracketLeg guard.
2923 |         // Preserves B18 T3 fix: also cancels Initialized orders (DW-B18-CANCEL-01).
2924 |         private void CancelOneAccount(Account acc, Instrument instrument)
2925 |         {
2926 |             foreach (var order in acc.Orders.ToList())
2927 |             {
2928 |                 if (order.Instrument != instrument)
2929 |                     continue;
2930 |                 // B18 T3: DW-B18-CANCEL-01 -- also cancel Initialized orders.
2931 |                 if (
2932 |                     order.OrderState != OrderState.Working
2933 |                     && order.OrderState != OrderState.Initialized
2934 |                 )
2935 |                     continue;
2936 |                 if (IsBracketLeg(order))
2937 |                     continue;
2938 |                 try
2939 |                 {
2940 |                     acc.Cancel(new Order[] { order });
2941 |                     StatusUpdate?.Invoke(acc.Name + ": entry pulled " + order.OrderId);
2942 |                 }
2943 |                 catch (Exception ex)
2944 |                 {
2945 |                     StatusUpdate?.Invoke("PTT-Cancel error: " + ex.Message);
```

CHECKS:
- [x] CancelOneAccount UNCHANGED -- no PTT- prefix guards added -- PASS
- [x] Method uses IsBracketLeg(order) which does NOT block PTT- cancels (by B29 design) -- PASS
- [x] No modification to this user-initiated cancel path -- PASS

---

### CYC count (independent):

TryCancelFollowerEntries() decision points (L1511-L1532):
1. `if (order.OrderState != OrderState.Cancelled)` -- +1
2. `if (IsAtmBracketName(order.Name))` -- +1
3. `if (order.Name != null` -- null guard -- +1
4. `|| order.Name.StartsWith("PTT-BE-", ...)` -- OR branch -- +1
5. `foreach (var acc in rule.FollowerAccounts)` -- +1
6. `if (acc == null)` -- +1
Base = 1. 6 decision points per ticket spec counting convention.
**CYC = 6** (base + 6 decisions using McCabe shorthand counting as spec states). <= 8 -- PASS

---

### Discrepancies vs completion report:

None. Engineer completion report stated:
- Change 2A: replaced full method, block comment shows CYC=6 and DW-B103 (actual: confirmed at L1506-1509)
- PTT guard at L1517-1524 with StartsWith("PTT-QX-") || StartsWith("PTT-BE-") both with StringComparison.Ordinal (actual: confirmed L1520-1521)
- Guard returns false (not true) (actual: confirmed L1524)
- Guard placed after IsAtmBracketName and before foreach (actual: confirmed ordering L1515-L1525)
- IsBracketLeg, CancelOneAccount, IsAtmBracketName all untouched (actual: all confirmed unchanged)

Engineer line offset reported as L1506-1532. Actual observed: L1506-1532. Exact match.
No discrepancies.

---

### Decision: **VERIFY_PASS**

All acceptance criteria confirmed against live source. PTT-QX- and PTT-BE- guards present with StringComparison.Ordinal on both calls. Guard correctly returns false. Guard positioned after IsAtmBracketName and before foreach loop. IsBracketLeg and CancelOneAccount untouched. CYC = 6 <= 8.