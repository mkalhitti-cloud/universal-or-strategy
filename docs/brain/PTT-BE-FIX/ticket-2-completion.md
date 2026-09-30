# PTT-BE-FIX -- T2 Completion Report
Ticket: T2 (DW-B85 Option B)
Status: BUILD_PASS
Date: 2026-08-22
Engineer: ptt-engineer (Phase 4a)

---

## Changes Made

| File | Line Range | Description |
|------|------------|-------------|
| src/PropTraderTools/CopyEngine.cs | L3402-L3414 | Replaced inner foreach (12 lines) with FindFollowerAccount call + null warning if-block (13 lines). Net: -12 +13 = +1 line in DtoToRule body. |
| src/PropTraderTools/CopyEngine.cs | L3441-L3453 | Added private static FindFollowerAccount(string name) helper method (13 lines) immediately after DtoToRule closing brace, before B6 Public persistence API comment. |

### Change 1 -- DtoToRule inner foreach replaced (L3402-3414)

BEFORE:
```csharp
var followers = new Account[dto.FollowerAccountNames.Length];
for (int i = 0; i < dto.FollowerAccountNames.Length; i++)
{
    foreach (var acc in Account.All)
    {
        if (acc.Name == dto.FollowerAccountNames[i])
        {
            followers[i] = acc;
            break;
        }
    }
}
```

AFTER:
```csharp
var followers = new Account[dto.FollowerAccountNames.Length];
for (int i = 0; i < dto.FollowerAccountNames.Length; i++)
{
    followers[i] = FindFollowerAccount(dto.FollowerAccountNames[i]);
    // DW-B85 Option B: warn when follower account is not yet in Account.All at load time.
    // Workaround: uncheck + re-check the follower in the panel after NT8 finishes connecting.
    if (followers[i] == null)
        NinjaTrader.Code.Output.Process(
            "[PTT-COPY] WARNING: follower '" + dto.FollowerAccountNames[i]
                + "' not found in Account.All at load time"
                + " -- will be skipped until rule is re-applied (uncheck + re-check in panel).",
            NinjaTrader.NinjaScript.PrintTo.OutputTab1);
}
```

### Change 2 -- FindFollowerAccount helper added (L3441-3453)

```csharp
// DW-B85: extracted from DtoToRule inner foreach to keep DtoToRule CYC at 7.
// Returns null (Account?) when account name is not found in Account.All.
// CYC=2: foreach(1) + if(1).
// JS-002 compliant: Account? return type makes nullability explicit end-to-end.
private static Account? FindFollowerAccount(string name)
{
    foreach (var acc in Account.All)
    {
        if (acc.Name == name)
            return acc;
    }
    return null;
}
```

---

## 7-Scan Results

| Scan | Command | Result | Status |
|------|---------|--------|--------|
| SCAN 1 -- lock() | `Get-ChildItem src/PropTraderTools/ -Filter *.cs -Recurse \| Select-String "lock\("` | 4 hits, ALL comment lines only (JS-021 compliance comments). Zero actual lock() statements. | PASS |
| SCAN 2 -- async void | `Get-ChildItem src/PropTraderTools/ -Filter *.cs -Recurse \| Select-String "async void "` | 4 hits, ALL comment lines (JS-033 compliance notes). Zero actual async void methods. | PASS |
| SCAN 3 -- throw new | `Get-ChildItem src/PropTraderTools/ -Filter *.cs -Recurse \| Select-String "throw new"` | 2 pre-existing hits in TradeCopierWindow.cs and Tests/B42Tests.cs. Zero introduced by T2. | PASS (0 new) |
| SCAN 4 -- CYC <= 8 | Manual count (complexity_audit.py absent -- script does not exist at scripts/complexity_audit.py) | DtoToRule: branch extraction -2 + null-warn if +1 = net -1. Per architecture plan arithmetic: CYC 8 -> 7. FindFollowerAccount: CYC=2 (foreach=1, if=1). Both <= 8. | PASS |
| SCAN 5 -- ASCII-only | `Get-Content -Encoding Byte + line scan` | 4 pre-existing non-ASCII bytes at L238, L239, L2290, L2291 (damaged comment characters from prior commits). Zero in T2 edit range (L3402-3453). | PASS (0 new) |
| SCAN 6 -- xUnit | N/A -- no test file produced by T2 | SKIP | N/A |
| SCAN 7 -- build | `dotnet build src/PropTraderTools/` | 83 pre-existing errors in CopyEngineTests.cs (known baseline). 1 pre-existing error in CopyEngine.cs at L3350 (Globals ambiguity -- pre-existing, unrelated to T2). Zero errors from T2 edit range (L3402-3453). | PASS (0 new) |

---

## Account? Nullable Note

**Used: `Account?`**

Rationale: The project sets `<Nullable>disable</Nullable>` in PropTraderTools.csproj, but
`<NoWarn>` includes CS8632 (nullable reference annotations in non-nullable context). The
file already uses nullable annotations for `Order?` return types at L1554 and L1601
(`private Order? FindFollowerBracketOrder` and `private static Order? FindFollowerEntryOrder`).
This establishes `Account?` as consistent with the file's existing pattern. CS8632 is suppressed
project-wide, so no new warning is introduced. `Account?` is used as specified.

---

## ASCII Verification

All string literals in the warning block confirmed ASCII-only:
- Apostrophe: 0x27 (') -- standard ASCII apostrophe, not Unicode curly quote
- Hyphens: 0x2D 0x2D (--) -- two ASCII hyphens, not em-dash (U+2014)
- No Unicode characters, no curly quotes, no special symbols in T2 edit range
- Non-ASCII bytes at L238, L239, L2290, L2291 are pre-existing and unrelated to T2

---

## Post-Edit Steps Completed

- [x] sync-ptt-to-nt8.ps1 executed: "COPIED: CopyEngine.cs (1 copied, 14 skipped in sync, 35 excluded)"
- [x] git add src/PropTraderTools/CopyEngine.cs
- [x] git commit -m "fix(ptt): DW-B85 Option B startup warning for null follower at LoadRules time"

---

## Commit Hash

`ee6b1dcf`

---

## Manual Verification Reminder

Start NinjaTrader with a copy rule containing a follower account name not yet connected.
Expected Output Tab 1:
```
[PTT-COPY] WARNING: follower '<name>' not found in Account.All at load time -- will be skipped until rule is re-applied (uncheck + re-check in panel).
```
Expected: one warning per missing follower slot, no warning when all accounts resolve.
