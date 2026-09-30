# B170 Ticket-1 Completion — DW-B170-01 Predicate Extension

**Ticket:** TICKET B170-1 — Extend OrderState predicate in FindLeaderStopPrice
**File:** `src/PropTraderTools/CopyEngine.cs`
**Status:** BUILD_PASS

---

## Scope

TICKET B170-1 ONLY. One method (`FindLeaderStopPrice`) modified in `CopyEngine.cs`. Two lines changed (line 4410 CYC comment, line 4423 predicate). Net line delta: 0. No other methods touched.

---

## Exact Lines Changed

### Change A — Predicate (line 4423)

**BEFORE:**
```csharp
                    && order.OrderState == OrderState.Working
```

**AFTER:**
```csharp
                    && (order.OrderState == OrderState.Working || order.OrderState == OrderState.Accepted)
```

### Change B — CYC comment (line 4410)

**BEFORE:**
```
        // CYC=5: (1) null account; (2) zero index; (3) foreach; (4) name match; (5) state==Working.
```

**AFTER:**
```
        // CYC=6: (1) null account; (2) zero index; (3) foreach; (4) name match; (5) state==Working; (6) state==Accepted. DW-B170-01.
```

---

## 7-Scan Results

| # | Scan | Command / Result |
|---|------|-----------------|
| SCAN-01 | OrderState.Working present in FindLeaderStopPrice region (lines 4412–4428) | `Select-String -Pattern "OrderState\.Working"` restricted to 4412–4428 → **Line 4423: MATCH** ✅ |
| SCAN-02 | OrderState.Accepted present in FindLeaderStopPrice region (lines 4412–4428) | `Select-String -Pattern "OrderState\.Accepted"` restricted to 4412–4428 → **Line 4423: MATCH (1 match)** ✅ |
| SCAN-03 | No new lock() introduced | `Select-String -Path "src/PropTraderTools/*.cs" -Pattern "lock\s*\("` → All matches are in *comments* containing "no lock()" — **0 actual lock() calls** ✅ |
| SCAN-04 | CYC = 6 post-change | Branches: (1) null account + (2) zero index + (3) foreach + (4) name match + (5) state==Working + (6) state==Accepted || branch = **CYC=6** ✅ |
| SCAN-05 | ASCII-only in changed lines | Both changed lines verified: `Get-Content` + non-ASCII regex → **0 non-ASCII characters** ✅ |
| SCAN-06 | ptt-sync.ps1 exits 0 | `powershell -File .\scripts\ptt-sync.ps1` → **"ptt-sync complete: 18 linked, 0 errors"** (exit 0) ✅ |
| SCAN-07 | Only FindLeaderStopPrice region modified | `Select-String` confirms only lines 4410 and 4423 changed (DW-B170-01 tag present at 4410); no other method bodies altered ✅ |

---

## ptt-sync.ps1 Exit Code

```
LINKED  AtrSizingEngine.cs
LINKED  CopyEngine.cs
... (18 files total)
ptt-sync complete: 18 linked, 0 errors
```

Exit code: **0**

---

## Jane Street DNA

- No `lock()` added anywhere (JS-021)
- CYC=6 <= 8 ceiling
- ASCII-only in all changed lines
- No throw in hot path (JS-001)
- Returns 0.0 on all failure paths (JS-002)
- Both OrderState.Working AND OrderState.Accepted present in predicate (regression-safe)

---

## Status: BUILD_PASS
