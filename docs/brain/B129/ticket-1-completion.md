# B129 Ticket 1 — Completion Report

**Block**: B129 — Instrument Row Redesign: Quick2t + QAll2t Buttons
**Ticket**: T1 (T1a + T1b + T1c)
**Engineer**: ptt-engineer
**Result**: BUILD_PASS

---

## Changes Made

### T1a — TradeCopierPanel.cs

**Fields (lines ~269-273):**
- REMOVED: `_instrBeBtn` (Button)
- REMOVED: `_instrQxT1` (int, value 4)
- RENAMED: `_instrQxBtn` → `_instr2tBtn` (Button)
- ADDED: `_instrQAll2tBtn` (Button)
- Comment updated from B128 to B129

**Added import:**
- `using System.Linq;` (required for `FirstOrDefault` on `Collection<Position>`)

**BuildInstrRow() — replaced:**
- Removed: spinner cluster (DockPanel + Grid + 2x RepeatButton + QX-Instr button + BE-Instr button)
- Added: 2-col UniformGrid with "Quick2t" button (`OnInstr2tClick`) and "QAll2t" button (`OnInstrQAll2tClick`)
- CYC=1. No branches. ASCII-only labels. No lock. No async.

**ComputeInstrSplit() — removed entirely:**
- Was: `internal static (int t1, int t2) ComputeInstrSplit(int instrQxT1) => ...`
- Replaced with Build2TargetList below.

**Build2TargetList() — added:**
```csharp
internal static List<(double Price, int Qty)> Build2TargetList(int totalQty)
{
    int t1Qty = (totalQty + 1) / 2;
    int t2Qty = totalQty - t1Qty;
    return new List<(double, int)> { (0.0, t1Qty), (0.0, t2Qty) };
}
```
- CYC=1. Never null. internal static for test access.

**OnInstrQxClick() — removed.** Replaced by OnInstr2tClick below.
**OnInstrQxUp() — removed entirely.**
**OnInstrQxDown() — removed entirely.**
**OnInstrBeClick() — removed.** Replaced by OnInstrQAll2tClick below.

**OnInstr2tClick() — added:**
- Resolves position from leader account, builds 2-target list, calls PttQuickExit.Execute(7-arg)
- Log tag: `[PTT-QX-2T]` with `T1=` and `T2=` keys
- CYC=4: (1) `_instrument==null`, (2) `_leaderAccount==null` after resolve, (3) FirstOrDefault lambda, (4) `pos?.Quantity??1`

**OnInstrQAll2tClick() — added:**
- Delegates to `new PttGlobalQuickExit().Execute()` — zero other logic
- CYC=1. Log produced internally by PttGlobalQuickExit.

### T1b — PttQuickExit.cs

**tNQty <= 0 guard added (1 line):**
```csharp
if (tNQty <= 0)
    continue; // B129: skip T2 when pos.Quantity==1 and t2Qty==0
```
Inserted immediately after tNQty assignment block, before `string ocoId_i =`.
CYC of Execute() (7-arg): was 7, now 8. Still within budget.

### T1c — B128Tests.cs

**Replaced 4 ComputeInstrSplit tests with 5 Build2TargetList tests:**
- `T_B129_01_Build2TargetList_Even_T1EqualT2` — qty=4, T1=2, T2=2, Price=0.0
- `T_B129_02_Build2TargetList_Odd_T1Heavier` — qty=5, T1=3, T2=2, Price=0.0
- `T_B129_03_Build2TargetList_One_T2IsZero` — qty=1, T1=1, T2=0, Price=0.0
- `T_B129_04_Build2TargetList_Large_Odd` — qty=7, T1=4, T2=3, Price=0.0
- `T_B129_05_Build2TargetList_Six_BothThree` — qty=6, T1=3, T2=3, Price=0.0
- Class name `B128Tests` retained as required. File `B128Tests.cs` unchanged.

---

## 7-Scan Results

### SCAN-01 — lock() check
```
TradeCopierPanel.cs  (actual code, comments excluded): Count=0 ✓
PttQuickExit.cs:                                        Count=0 ✓
```

### SCAN-02 — async void check
```
TradeCopierPanel.cs: Count=7 — all in comments only (JS-021/JS-033 annotations), no actual async void ✓
PttQuickExit.cs:     Count=0 ✓
```

### SCAN-03 — return null check
```
TradeCopierPanel.cs: 12 hits — all pre-existing methods not touched by B129.
Build2TargetList contains NO return null; returns new List<> always. ✓
```

### SCAN-04 — throw new check
```
TradeCopierPanel.cs: Count=0 ✓
PttQuickExit.cs:     Count=0 ✓
```

### SCAN-05 — Log tag [PTT-QX-2T]
```
TradeCopierPanel.cs line 1960: "[PTT-QX-2T] button: " ✓
Keys T1= and T2= present in format string. ✓
```

### SCAN-06 — CYC compliance (manual)
| Method | File | CYC | Budget | Status |
|--------|------|-----|--------|--------|
| `Build2TargetList` | TradeCopierPanel.cs | 1 | <=8 | PASS |
| `BuildInstrRow` | TradeCopierPanel.cs | 1 | <=8 | PASS |
| `OnInstr2tClick` | TradeCopierPanel.cs | 4 | <=8 | PASS |
| `OnInstrQAll2tClick` | TradeCopierPanel.cs | 1 | <=8 | PASS |
| `Execute()` 7-arg | PttQuickExit.cs | 8 | <=8 | PASS |

### SCAN-07 — Build + Tests
```
dotnet build --no-incremental:
  Build succeeded.  0 Warning(s)  0 Error(s)

dotnet test --filter "FullyQualifiedName~B128Tests" --no-build:
  Passed!  - Failed: 0, Passed: 5, Skipped: 0, Total: 5
  T_B129_01_Build2TargetList_Even_T1EqualT2  PASS
  T_B129_02_Build2TargetList_Odd_T1Heavier   PASS
  T_B129_03_Build2TargetList_One_T2IsZero    PASS
  T_B129_04_Build2TargetList_Large_Odd       PASS
  T_B129_05_Build2TargetList_Six_BothThree   PASS
```

---

## H-Criteria Verification

| Check | Description | Result |
|-------|-------------|--------|
| H.1 | Build 0 errors, 0 warnings | PASS |
| H.2 | 5 new tests pass; 4 old absent | PASS |
| H.3a | `_instrQxT1` removed | PASS |
| H.3b | `_instrBeBtn` removed | PASS |
| H.3c | `OnInstrQxUp` removed | PASS |
| H.3d | `OnInstrQxDown` removed | PASS |
| H.3e | `OnInstrBeClick` removed | PASS |
| H.3f | `ComputeInstrSplit` removed | PASS |
| H.4a | `_instr2tBtn` present | PASS |
| H.4b | `_instrQAll2tBtn` present | PASS |
| H.4c | `Build2TargetList` present | PASS |
| H.4d | `OnInstr2tClick` present | PASS |
| H.4e | `OnInstrQAll2tClick` present | PASS |
| H.5 | `tNQty <= 0` guard in PttQuickExit.cs | PASS |
| H.6 | PttGlobalQuickExit.cs unchanged | PASS (zero diff) |
| H.7 | All 7 scans pass | PASS |

---

## Overall: BUILD_PASS
