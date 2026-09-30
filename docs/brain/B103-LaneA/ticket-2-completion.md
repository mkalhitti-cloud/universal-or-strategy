## Ticket 2 Completion — DW-B103

### Status: BUILD_PASS

### Changes Applied

- **Change 2A**: Replaced the full `TryCancelFollowerEntries` method block comment + body (was L1506-1523, 18 lines) with the new 27-line version (L1506-1532). Changes:
  - Block comment updated: `CYC=4` -> `CYC=6`, added `// DW-B103: PTT exit bracket OCO-cancels return false (do not wipe follower brackets).`
  - Inserted new guard after `IsAtmBracketName` check (L1517-1524): `if (order.Name != null && (order.Name.StartsWith("PTT-QX-", StringComparison.Ordinal) || order.Name.StartsWith("PTT-BE-", StringComparison.Ordinal))) return false; // DW-B103: OCO-cancel of PTT exit bracket must not wipe follower brackets`
  - Guard uses `StringComparison.Ordinal` on both `StartsWith` calls
  - Guard returns `false` (not `true`) — followers are NOT cancelled for PTT exit bracket OCO-cancels
  - Existing `IsAtmBracketName`, `foreach` loop, and `return true` unchanged

### Final TryCancelFollowerEntries() state (verified at L1506-1532)

```csharp
// TryCancelFollowerEntries: CYC=6. Propagates leader cancel to all follower entry orders.
// Returns true if Cancelled state was handled (caller should return immediately).
// HOTFIX-B63-COPY-CANCEL-01: ATM bracket cancels are skipped via IsAtmBracketName guard.
// DW-B103: PTT exit bracket OCO-cancels return false (do not wipe follower brackets).
// JS-021: no lock. JS-001: no throw.
private bool TryCancelFollowerEntries(Order order, CopyRule rule)
{
    if (order.OrderState != OrderState.Cancelled)
        return false;
    if (IsAtmBracketName(order.Name))
        return true; // HOTFIX-B63-COPY-CANCEL-01
    if (
        order.Name != null
        && (
            order.Name.StartsWith("PTT-QX-", StringComparison.Ordinal)
            || order.Name.StartsWith("PTT-BE-", StringComparison.Ordinal)
        )
    )
        return false; // DW-B103: OCO-cancel of PTT exit bracket must not wipe follower brackets
    foreach (var acc in rule.FollowerAccounts)
    {
        if (acc == null)
            continue;
        CancelOneAccount(acc, order.Instrument);
    }
    return true;
}
```

### Scan Results

- **SCAN-01 lock()**: `Select-String -Pattern "lock\("` — 1 hit in a comment at L1897 (pre-existing, not in changed region). 0 new `lock(` in changed regions. PASS.
- **SCAN-02 throw new**: 0 results across entire file. PASS.
- **SCAN-03 ASCII**: New string literals `"PTT-QX-"`, `"PTT-BE-"`, comment `"DW-B103: OCO-cancel of PTT exit bracket must not wipe follower brackets"` — all pure ASCII. PASS.
- **SCAN-04 PTT-QX guard present**: `Select-String -Pattern "PTT-QX-"` shows L1520 match inside `TryCancelFollowerEntries` (`order.Name.StartsWith("PTT-QX-", StringComparison.Ordinal)`). PASS.
- **SCAN-05 StringComparison.Ordinal**: Both `StartsWith` calls in the new guard use `StringComparison.Ordinal`. PASS.
- **SCAN-06 CYC TryCancelFollowerEntries**: Decision points: `OrderState != Cancelled` (+1) + `IsAtmBracketName` (+1) + `order.Name != null` (+1) + `|| StartsWith("PTT-BE-")` (+1) + `foreach` (+1) + `acc == null` (+1) + base = **CYC 6** <= 8. PASS.
- **SCAN-07 sync**: `ptt-sync-and-verify.ps1` — 1 COPIED (CopyEngine.cs), 15 In-sync, 0 MISMATCH. All 16 files OK. PASS.

### Protected Regions — Confirmed Untouched

- `IsBracketLeg()` at ~L3198-3205: UNTOUCHED (B29 intentional design)
- `CancelOneAccount()` at ~L2915-2939: UNTOUCHED (user-initiated cancel path)
- `IsAtmBracketName()` at ~L669-682: UNTOUCHED (B63 hotfix guard)

### Acceptance Criteria

- [x] `TryCancelFollowerEntries` contains `StartsWith("PTT-QX-", StringComparison.Ordinal)` guard
- [x] `TryCancelFollowerEntries` contains `StartsWith("PTT-BE-", StringComparison.Ordinal)` guard
- [x] New guard `return false` (NOT `return true`) — followers are NOT cancelled for PTT exit brackets
- [x] `StringComparison.Ordinal` used on **both** `StartsWith` calls
- [x] New guard is placed **after** `IsAtmBracketName` guard and **before** `foreach` loop
- [x] Block comment above method updated: `CYC=6` and `DW-B103:` annotation present
- [x] `IsBracketLeg()` instance method at ~L3198-3205: UNCHANGED
- [x] `CancelOneAccount()` at ~L2915-2939: UNCHANGED
- [x] `IsAtmBracketName()` at ~L669-682: UNCHANGED
- [x] CYC of `TryCancelFollowerEntries()` = **6** <= 8
- [x] `ptt-sync-and-verify.ps1`: 0 MISMATCH
