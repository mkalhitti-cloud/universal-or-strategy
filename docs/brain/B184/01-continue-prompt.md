# B184 $continue Prompt
# Paste this into a copier-spec session AFTER ptt-orchestrator issues FINAL_PASS on B184

---

$continue B184 complete. FINAL_PASS issued.

Run every check below in order. Execute the PowerShell yourself and report each result.
After ALL checks pass, tell me explicitly: "COMPILE NOW -- load in NinjaTrader."
If ANY check fails, tell me exactly what failed and what must be fixed before I compile.

---

## Step 1: Build (dotnet -- local verification before NT8)

```powershell
dotnet build universal-or-strategy.sln 2>&1 | Select-String "(error CS|Build succeeded|FAILED)" | Select-Object -Last 10
```

Expected: `Build succeeded` with **0 errors**.
If you see any `error CS` line that is NOT `CS0436` (the pre-existing NT8 assembly conflict):
STOP. Report the error. Do not proceed to NT8 compile instructions until fixed.

---

## Step 2: B183 Guardian (regression safety check)

```powershell
powershell -File .\scripts\verify_critical_symbols.ps1
```

Expected: exit 0, output contains `GUARDIAN PASS`
If FAIL: the 16 B183 regression symbols were accidentally disturbed. Stop. Engineer must restore.

---

## Step 3: T1 Symbols Present in CopyEngine.cs (BE event infrastructure)

```powershell
$symbols = @("BeState","PendingBeArmed","GlobalBeAllDisarmed","DisarmPendingBe","IsPendingSlotsEmpty","RaiseBeAllDisarmed")
$ce = Get-Content src/PropTraderTools/CopyEngine.cs -Raw
$missing = $symbols | Where-Object { $ce -notmatch [regex]::Escape($_) }
if ($missing) { Write-Error "T1 MISSING: $($missing -join ', ')" } else { Write-Host "T1 PASS -- all 6 BE symbols present" }
```

Expected: `T1 PASS -- all 6 BE symbols present`

---

## Step 4: T2 Removal Confirmed (ATR sizing symbols gone)

```powershell
$removed = @(
    "AtrSizingEngine","AtrCalc","_riskDollarsBox","_atrFractionBox",
    "BuildRiskAtrRow","_atrRow","_atrDisplayLabel","UpdateMaxRisk",
    "SetAtrEngine","AtrSizing","_atrEngine","_atrPollTimer",
    "StartAtrEngine","StopAtrEngine","UpdateAtrOverlay","NotifyRiskChanged","NotifyAtrFractionChanged"
)
$allSrc = Get-ChildItem src/PropTraderTools -Filter "*.cs" -Recurse | Get-Content -Raw | Out-String
$stillPresent = $removed | Where-Object { $allSrc -match [regex]::Escape($_) }
if ($stillPresent) { Write-Error "T2 STILL PRESENT: $($stillPresent -join ', ')" } else { Write-Host "T2 PASS -- all ATR symbols removed" }
```

Expected: `T2 PASS -- all ATR symbols removed`

---

## Step 5: Files Deleted

```powershell
$deleted = @("src/PropTraderTools/AtrSizingEngine.cs","src/PropTraderTools/AtrCalc.cs")
$stillExist = $deleted | Where-Object { Test-Path $_ }
if ($stillExist) { Write-Error "FILES STILL EXIST: $($stillExist -join ', ')" } else { Write-Host "FILES PASS -- both deleted" }
```

Expected: `FILES PASS -- both deleted`

---

## Step 6: FeatureFlags Arity (record compiles with correct parameter count)

```powershell
$ff = Get-Content src/PropTraderTools/FeatureFlags.cs -Raw
$hasAtrSizing = $ff -match "AtrSizing"
if ($hasAtrSizing) { Write-Error "FeatureFlags FAIL -- AtrSizing still present" } else { Write-Host "FeatureFlags PASS -- AtrSizing removed" }
```

Expected: `FeatureFlags PASS -- AtrSizing removed`

---

## Step 7: Hard-link Sync

```powershell
powershell -File .\scripts\ptt-sync.ps1
```

Expected: `ptt-sync complete: N linked, 0 errors`
If `AtrSizingEngine.cs` or `AtrCalc.cs` appear in errors: the sync script needs updating (add to B184 deferred backlog if not already fixed).

---

## Step 8: Artifact Files Exist

```powershell
$artifacts = @(
    "docs/brain/B184/02-architecture-plan.md",
    "docs/brain/B184/02-plan-review.md",
    "docs/brain/B184/04-tickets.md",
    "docs/brain/B184/04-ticket-review.md",
    "docs/brain/B184/ticket-1-completion.md",
    "docs/brain/B184/ticket-1-verification.md",
    "docs/brain/B184/ticket-2-completion.md",
    "docs/brain/B184/ticket-2-verification.md",
    "docs/brain/B184/05-final-review.md",
    "docs/brain/B184/06-deferred-backlog.md"
)
$missing = $artifacts | Where-Object { -not (Test-Path $_) }
if ($missing) { Write-Error "MISSING ARTIFACTS: $($missing -join ', ')" } else { Write-Host "ARTIFACTS PASS -- all 10 present" }

# Confirm gate strings
$pr = Get-Content docs/brain/B184/02-plan-review.md -Raw
$tr = Get-Content docs/brain/B184/04-ticket-review.md -Raw
$fr = Get-Content docs/brain/B184/05-final-review.md -Raw
$v1 = Get-Content docs/brain/B184/ticket-1-verification.md -Raw
$v2 = Get-Content docs/brain/B184/ticket-2-verification.md -Raw
Write-Host "REVIEW_PASS: $($pr -match 'REVIEW_PASS')"
Write-Host "TICKET_REVIEW_PASS: $($tr -match 'TICKET_REVIEW_PASS')"
Write-Host "VERIFY_PASS T1: $($v1 -match 'VERIFY_PASS')"
Write-Host "VERIFY_PASS T2: $($v2 -match 'VERIFY_PASS')"
Write-Host "FINAL_PASS: $($fr -match 'FINAL_PASS')"
```

Expected: all 5 gate strings = True.

---

## Step 9: Update Spec HTML

Update `DW-B184-T1` status row in spec HTML to RESOLVED (green).
Update `DW-B184-T2` status row in spec HTML to RESOLVED (green).

---

## Reporting Format

Report as a table first, then give the compile verdict at the bottom.

| Check | Result | Notes |
|-------|--------|-------|
| dotnet build 0 errors | PASS/FAIL | list any non-CS0436 errors |
| B183 guardian (16 symbols) | PASS/FAIL | |
| T1: 6 BE symbols present | PASS/FAIL | list missing |
| T2: ATR symbols absent | PASS/FAIL | list any remaining |
| AtrSizingEngine.cs deleted | PASS/FAIL | |
| AtrCalc.cs deleted | PASS/FAIL | |
| FeatureFlags AtrSizing gone | PASS/FAIL | |
| ptt-sync.ps1 clean | PASS/FAIL | |
| All 10 artifacts + gate strings | PASS/FAIL | list missing |
| Spec HTML updated | YES/NO | |

---

## Compile Verdict (REQUIRED -- always end with one of these two lines)

If ALL checks PASS:
> **COMPILE NOW -- load NinjaTrader, open NinjaScript Editor, click Compile. Expect 0 errors.**

If ANY check FAILS:
> **DO NOT COMPILE. Fix required: [describe exact failure]. Come back with $continue after fix.**
