# IsAllowedIpcAction — CYC Reduction Completion

## CYC Gate Output

```
CYC_GATE: NOT_FOUND  EPIC-W7-OVERRUN-IsAllowedIpcAction  IsAllowedIpcAction  (not in CYC>8 list — assumed PASS)
```

**Gate Exit Code**: 0 (PASS)

## Summary

| Field | Value |
|-------|-------|
| Method | `IsAllowedIpcAction` |
| File | `src/V12_002.UI.IPC.cs` |
| CYC Before | 10 |
| CYC After | <=8 (gate: NOT_FOUND = no longer in >8 list) |
| Build | 0 errors |
| wave_ready | true |

## Approach

The original method contained 8 `||` operators in a single return expression, each inflating CYC by 1.

**Original structure** (CYC=10):
```csharp
private bool IsAllowedIpcAction(string action)
{
    if (IsNullOrWhiteSpace)  // +1
        return false;
    if (Contains)            // +1
        return true;
    return A || B || C || D || E || F || G || H;  // +8 operators
}
// Total: 10
```

**Extraction**: Split the 8 prefix checks into two private helpers (4 each).

## New Helper Methods

- `IsAllowedIpcPrefix_A(string action)` — checks `MOVE_TARGET`, `CLOSE_T`, `GET_FLEET`, `SET_MAX_RISK` prefixes (CYC=4)
- `IsAllowedIpcPrefix_B(string action)` — checks `TOGGLE_ACCOUNT`, `SET_ANCHOR`, `MODE_`, `EXEC_` prefixes (CYC=4)

## Final Method CYC Breakdown

| Method | CYC |
|--------|-----|
| `IsAllowedIpcAction` | 4 (base=1 + 2 ifs + 1 `\|\|`) |
| `IsAllowedIpcPrefix_A` | 4 (base=1 + 3 `\|\|`) |
| `IsAllowedIpcPrefix_B` | 4 (base=1 + 3 `\|\|`) |

## Validation

- **Build**: 0 errors, 0 warnings
- **CsharpierFormat**: 83 files formatted cleanly
- **CYC Gate**: Exit 0
- **No lock() blocks**: confirmed
- **ASCII-only string literals**: confirmed
- **Helpers in same class/file**: confirmed
