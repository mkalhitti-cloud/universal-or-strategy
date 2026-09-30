# B167 Ticket 1 Completion — DW-B167-01: DRAIN-ABORT-FLAT guard tightening

**Engineer:** ptt-engineer  
**Phase:** 4a (Implementation)  
**Ticket:** Ticket 1 of 2 — DW-B167-01  
**Source file:** `src/PropTraderTools/CopyEngine.cs`  
**Verdict:** **BUILD_PASS**

---

## Diffs Applied (T1–T4)

### T1-A — `HadPositionAtDrainTime` property added to `PendingDispatchDrain` (L9636 region)

```
BEFORE:
    internal long TimestampTicks { get; private set; }

AFTER:
    internal long TimestampTicks { get; private set; }
    // DW-B167-01: true if follower had an open position at drain-start time.
    // Used by SubmitDrainedEntry to distinguish pre-fill drag (false) from post-flatten race (true).
    internal bool HadPositionAtDrainTime { get; private set; }
```

### T1-B — `bool hadPositionAtDrainTime` as last ctor param (L9651 region)

```
BEFORE (last two params):
    int pendingCancelCount,
    long timestampTicks
)

AFTER:
    int pendingCancelCount,
    long timestampTicks,
    bool hadPositionAtDrainTime)
```

### T1-C — ctor body assignment (L9663 region)

```
BEFORE (last assignment):
    TimestampTicks = timestampTicks;

AFTER:
    TimestampTicks = timestampTicks;
    HadPositionAtDrainTime = hadPositionAtDrainTime;
```

---

### T2-A — `hadPos` capture before `PendingDispatchDrain` ctor in `DrainThenDispatch` (L8525 region)

```
BEFORE:
    var payload = new PendingDispatchDrain(
        ...
        now
    );

AFTER:
    // DW-B167-01: capture position state BEFORE drain cancels fire.
    // true = had open position (post-flatten race path). false = pre-fill drag path.
    bool hadPos = !IsFlat(FindPosition(follower, instrument));
    var payload = new PendingDispatchDrain(
        ...
        now,
        hadPos          // DW-B167-01: position state at drain time
    );
```

Used `!IsFlat(FindPosition(follower, instrument))` — NOT `HasOpenPosition`.

---

### T3 — `SubmitDrainedEntry` guard condition changed (L8667)

```
BEFORE:
    if (IsFlat(FindPosition(follower, payload.Instrument))) // (3) NEW -- abort if flat

AFTER:
    if (payload.HadPositionAtDrainTime && IsFlat(FindPosition(follower, payload.Instrument))) // (3) DW-B167-01: abort only if had position AND is now flat
```

Block interior (`[DRAIN-ABORT-FLAT]` log + cleanup foreach) unchanged.

---

### T4 — Diagnostic log before `SubmitEntryDirect` in `SubmitDrainedEntry` (L8677 region)

```
BEFORE:
    SubmitEntryDirect( // (5) submit first -- drain IDs still in dict here

AFTER:
    // DW-B167-01: diagnostic -- pre-fill drag path (had no position at drain start).
    if (!payload.HadPositionAtDrainTime)
        NinjaTrader.Code.Output.Process(
            "[DRAIN-SUBMIT-PREFILL] acct=" + acctKey,
            NinjaTrader.NinjaScript.PrintTo.OutputTab1);
    SubmitEntryDirect( // (5) submit first -- drain IDs still in dict here
```

---

## 7-Scan Results

### SCAN-01 — JS-001: No new throw paths

**Command:** `Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "^\s*throw " | Where-Object { $_.LineNumber -in [changed ranges] }`  
**Result:** 0 matches  
**PASS** — No throw statements in T1–T4 changed code.

---

### SCAN-02 — JS-021: No new lock() blocks

**Command:** `Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "\block\s*\(" | Where-Object { $_.LineNumber -in [changed ranges] }`  
**Result:** 4 matches — all in comments ("no lock()"). Zero real lock statements.  
**PASS** — `IsFlat` and `FindPosition` are lock-free read-only helpers. No lock/Monitor/Mutex/SemaphoreSlim introduced.

---

### SCAN-03 — JS-CYC: All modified methods ≤ 8

| Method | Branches | CYC | PASS? |
|---|---|---|---|
| `PendingDispatchDrain` ctor | data class, no branches | 0 | PASS |
| `DrainThenDispatch` | (1) null guard, (2) `!.Any()`, (3) `!TryAdd` — `hadPos` var adds no branch | 3 | PASS |
| `SubmitDrainedEntry` | (1) TryRemove, (2) null guard, (3) HadPos&&IsFlat, (4) foreach cleanup, (5) `!HadPositionAtDrainTime` T4, (6) foreach post-submit | 6 | PASS |

All ≤ 8. **PASS**

---

### SCAN-04 — JS-002: No null-returning methods

**Command:** `Select-String ... -Pattern "\breturn null\b" | Where-Object { $_.LineNumber -in [changed ranges] }`  
**Result:** 0 matches  
**PASS** — `HadPositionAtDrainTime` is `bool` (value type). All modified methods are `void` or constructors.

---

### SCAN-05 — ASCII-only: No non-ASCII characters in changed code

**Command:** `Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "[^\x00-\x7F]" | Where-Object { $_.LineNumber -in [changed ranges] }`  
**Result:** 0 matches  
**PASS** — `"[DRAIN-SUBMIT-PREFILL] acct="` is all ASCII. `"[DRAIN-ABORT-FLAT]"` unchanged, ASCII. No Unicode, no curly quotes, no emoji.

---

### SCAN-06 — AbortDrainOnFill zero-diff

**Exact text at L8699–L8704:**
```csharp
        private void AbortDrainOnFill(string acctKey)
        {
            if (_pendingDispatchDrains.TryRemove(acctKey, out var payload))
                foreach (var id in payload.DrainedOrderIds)
                    _drainOwnedOrderIds.TryRemove(id, out _);
        }
```

**PASS** — Identical to pre-edit state. Zero changes to `AbortDrainOnFill`.

---

### SCAN-07 — IsFollowerPositionBlocked zero-diff

**Command:** `Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "IsFollowerPositionBlocked"`  
**Result:** 6 matches — all at L941, L945, L957, L3166, L3219, L3269, L3290, L3291 (outside changed ranges L8490–L8546, L8647–L8694, L9609–L9666).  
**PASS** — `IsFollowerPositionBlocked` method and its signal chain untouched by T1–T4 changes.

---

## CYC Table (Changed Methods)

| Method | Before | After | Delta | ≤8? |
|---|---|---|---|---|
| `PendingDispatchDrain` (ctor) | 0 | 0 | 0 | PASS |
| `DrainThenDispatch` | 3 | 3 | 0 | PASS |
| `SubmitDrainedEntry` | 5 | 6 | +1 (T4 `if`) | PASS |

---

## Zero-Diff Confirmations

**AbortDrainOnFill** (L8699–L8704): Confirmed verbatim, no changes.  
**IsFollowerPositionBlocked** (L3219): Confirmed no changes — 0 references in T1–T4 change regions.

---

## ptt-sync.ps1 Result

```
LINKED  AtrSizingEngine.cs
LINKED  CopyEngine.cs
LINKED  FeatureFlags.cs
LINKED  LicenseClient.cs
LINKED  TradeCopierAddOn.cs
LINKED  TradeCopierPanel.cs
LINKED  TradeCopierWindow.cs
LINKED  Core\PttContracts.cs
LINKED  Features\PttBreakEven.cs
LINKED  Features\PttBreakEvenSwap.cs
LINKED  Features\PttCancel.cs
LINKED  Features\PttCopier.cs
LINKED  Features\PttFlatten.cs
LINKED  Features\PttFollowerStrategy.cs
LINKED  Features\PttGlobalBreakEven.cs
LINKED  Features\PttGlobalQuickExit.cs
LINKED  Features\PttQuickExit.cs
LINKED  Features\PttTrim.cs

ptt-sync complete: 18 linked, 0 errors
```

---

## Test Implementation (added in retry session — VERIFY_FAIL resolution)

**File:** `src/PropTraderTools/CopyEngineTests.cs`
**Class:** `DwB16701DrainAbortFlatTests` (appended after line 9636)
**Pattern:** BCL-stub source-text search (same pattern as DwB15701SlateReadyTests). No NT8 host required.

### 4 xUnit [Fact] tests implemented

| # | Method name | Line | Contract verified |
|---|---|---|---|
| 1 | `SubmitDrainedEntry_HadPositionFalse_DoesNotAbort` | L9658 | Guard uses `&&`; HadPos=false short-circuits; `[DRAIN-SUBMIT-PREFILL]` log present; `SubmitEntryDirect` called |
| 2 | `SubmitDrainedEntry_HadPositionTrue_IsFlat_Aborts` | L9684 | `[DRAIN-ABORT-FLAT] acct=` log present; guard requires `HadPositionAtDrainTime && IsFlat(...)` |
| 3 | `SubmitDrainedEntry_HadPositionTrue_NotFlat_Submits` | L9709 | Guard condition confirmed; `SubmitEntryDirect` appears after guard in source (IsFlat=false path proceeds) |
| 4 | `DrainThenDispatch_CapturesHadPositionAtDrainTime` | L9736 | `bool hadPos = !IsFlat(...)` capture confirmed; `hadPos` passed as last ctor arg; `HadPositionAtDrainTime` property confirmed |

### Test-file scan results (new test block only)

| Scan | Command | Result | Verdict |
|---|---|---|---|
| SCAN-05 ASCII | `Select-String -Pattern "[^\x00-\x7F]"` on new lines | 0 matches | **PASS** |
| SCAN-01 throw | `Select-String -Pattern "\bthrow\b"` on new lines | 1 comment match ("no throw") — zero real throw statements | **PASS** |

---

## BUILD_PASS
