# Ticket 1 Verification: DW-B159-T1 — FM-A Ghost Re-entry Guard
<!-- status: VERIFY_PASS -->
<!-- wave: B159 -->
<!-- ticket: DW-B159-T1 -->
<!-- phase: 4b -->
<!-- verifier: ptt-verifier -->

---

## Scope

**DW-B159-T1 ONLY.** No other ticket code inspected in this session.

Source of truth: `src/PropTraderTools/CopyEngine.cs` (Wave workspace, READ-ONLY).

---

## Check 1 — Method Exists

**Required:** `internal bool IsFollowerPositionBlocked(Account acc, Instrument instr)` declared as a distinct method.

**Finding:**

```
Line 3056: internal bool IsFollowerPositionBlocked(Account acc, Instrument instr)
```

- Access modifier: `internal` (matches ticket spec "New Symbols" table — accessible to xUnit via InternalsVisibleTo)
- Return type: `bool`
- Parameters: `Account acc, Instrument instr`
- Location: immediately before `ShouldSkipFollower` declaration at line 3068

**Result: PASS**

---

## Check 2 — Implementation Correctness

**Source (lines 3056–3061, read independently):**

```csharp
internal bool IsFollowerPositionBlocked(Account acc, Instrument instr)
{
    if (acc == null)   return false;    // (2)
    if (instr == null) return false;    // (3)
    return HasOpenPosition(acc, instr);
}
```

| Sub-check | Requirement | Finding | Result |
|-----------|-------------|---------|--------|
| (a) Null guard `acc` | `if (acc == null) return false` | Line 3058: exact match | PASS |
| (b) Null guard `instr` | `if (instr == null) return false` | Line 3059: exact match | PASS |
| (c) Log line emitted | `[PTT-COPY-GUARD] ghost-entry blocked: ` + acc.Name + " " + instr.FullName | Lines 3079–3080 in ShouldSkipFollower (correct location — see Check 3) | PASS |
| (d) Returns true | `return true` on block path | Line 3083 in ShouldSkipFollower block (correct location) | PASS |
| Delegates to HasOpenPosition | `return HasOpenPosition(acc, instr)` — no reimplemented logic | Line 3060: exact match | PASS |
| CYC = 3 | base(1) + acc-null(+1) + instr-null(+1) = 3 | Independent count: 3 | PASS |

**Note on log/return-true placement:** The ticket spec places the log and `return true` inside `ShouldSkipFollower`'s hook block, not inside `IsFollowerPositionBlocked` itself. `IsFollowerPositionBlocked` is a pure predicate (returns bool). The log is emitted at the call site in `ShouldSkipFollower` (lines 3079–3083). This is the correct architecture per the ticket spec.

**Result: PASS**

---

## Check 3 — Hook Placement in ShouldSkipFollower

**Source (lines 3068–3090, read independently):**

```csharp
private bool ShouldSkipFollower(
    Account acc,
    NinjaTrader.Cbi.Instrument instr,
    OrderAction currentAction,
    OrderAction lastAction,
    bool hasLastDirection
)
{
    // B159 DW-B159-FM-A: ghost re-entry guard -- block copy when follower already has position.
    if (IsFollowerPositionBlocked(acc, instr))          // line 3077 -- FIRST EXECUTABLE STATEMENT
    {
        NinjaTrader.Code.Output.Process(
            "[PTT-COPY-GUARD] ghost-entry blocked: " + acc.Name + " " + instr.FullName,
            NinjaTrader.NinjaScript.PrintTo.OutputTab1
        );
        return true;
    }
    if (ShouldSkipFollowerDispatch(acc))                // line 3085 -- second conditional (pre-existing)
        return true;
    if (ShouldSkipForReversalGuard(acc, instr, currentAction, lastAction, hasLastDirection))
        return true;
    return false;
}
```

| Sub-check | Requirement | Finding | Result |
|-----------|-------------|---------|--------|
| First statement | `if (IsFollowerPositionBlocked(acc, instr)) return true;` is FIRST | Line 3077: confirmed first executable statement | PASS |
| No other changes | Only the B159 block added; ShouldSkipFollowerDispatch and ShouldSkipForReversalGuard unchanged | Confirmed — original two conditionals intact | PASS |
| CYC = 4 | base(1) + IsFollowerPositionBlocked(+1) + ShouldSkipFollowerDispatch(+1) + ShouldSkipForReversalGuard(+1) = 4 | Independent count: 4 | PASS |

**Result: PASS**

---

## Check 4 — All 7 Scans (Independent — NOT from engineer's self-report)

All scans executed independently against `src/PropTraderTools/CopyEngine.cs`.

### SCAN-01: lock( calls

**Command:** `Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "lock\("`

**Result (all matches):**

| Line | Content |
|------|---------|
| 334 | `// JS-021: ConcurrentDictionary -- lock-free. No lock() anywhere.` |
| 368 | `// ConcurrentDictionary: thread-safe without lock(). JS-021: no lock.` |
| 2255 | `// Value: ConcurrentBag<Order> -- thread-safe add, no lock().` |
| 3533 | `// ASCII-only. No lock(). JS-002 compliant (returns -1 on invalid, not throw).` |
| 3538 | `// ASCII-only. No lock(). JS-002 compliant (returns -1 on invalid, not throw).` |
| 4971 | `// ASCII-only. No DateTime.Now. No lock().` |
| 4994 | `// ASCII-only. No DateTime.Now. No lock().` |
| 5193 | `// JS-021: no lock() -- ConcurrentDictionary TryGetValue/TryRemove.` |
| 8166 | `// JS-021: no lock(). ConcurrentDictionary + Interlocked only.` |
| 8301 | `// JS-021: no lock(). Interlocked.Decrement is atomic.` |
| 8363 | `// CYC=3: base(1) + TryRemove guard(1) + foreach(1). JS-021: no lock().` |
| 8376 | `// JS-021: no lock(). Account.Cancel() is AddOnBase available pattern.` |
| 8405 | `// JS-021: no lock(). ConcurrentDictionary enumeration is thread-safe.` |

**Assessment:** All matches are in **comments only** (the text "no lock()" or "lock-free"). Zero actual `lock(` executable statements anywhere in the file.

**Result: PASS — 0 actual lock() calls**

---

### SCAN-02: async void declarations

**Command:** `Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "async void"`

**Result (all matches):**

| Line | Content |
|------|---------|
| 2152 | `// JS-021: no lock. JS-001: no throw. JS-033: Tick is not async void. ASCII-only.` |
| 8299 | `// Called directly from OnOrderUpdate -- NOT an event handler. Synchronous void. NOT async void (...)` |

**Assessment:** All matches are in **comments only** (explicitly stating NOT async void). Zero actual `async void` declarations anywhere in the file.

**Result: PASS — 0 async void declarations**

---

### SCAN-03: DateTime.Now usage

**Command:** `Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "DateTime\.Now[^U]"`

**Result (all matches):**

| Line | Content |
|------|---------|
| 1245 | `// ASCII-only. No DateTime.Now.` |
| 4971 | `// ASCII-only. No DateTime.Now. No lock().` |
| 4994 | `// ASCII-only. No DateTime.Now. No lock().` |
| 5889 | `// ASCII-only. No DateTime.Now (uses Environment.TickCount ...)` |
| 6020 | `// NT8: DateTime.MaxValue (not DateTime.Now) and null as last two args to CreateOrder.` |
| 6836 | `// ASCII-only. No DateTime.Now.` |
| 6944 | `// JS-001: no throw. JS-002: void. ASCII-only. No DateTime.Now.` |

**Assessment:** All matches are in **comments only** (the text "No DateTime.Now"). Zero actual `DateTime.Now` calls in executable code. Pattern `DateTime.Now[^U]` excludes `DateTime.UtcNow` (correct usage).

**Result: PASS — 0 DateTime.Now calls**

---

### SCAN-04: Thread.Sleep calls

**Command:** `Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "Thread\.Sleep"`

**Result:** No output — zero matches.

**Result: PASS — 0 Thread.Sleep calls**

---

### SCAN-05: Non-ASCII characters (full file)

**Command:** `Get-Content src/PropTraderTools/CopyEngine.cs | Where-Object { $_ -match '[^\x00-\x7F]' }`

**Result:** No output — zero non-ASCII bytes in the entire file.

**Visual verification of new string literal at line 3080:**
`"[PTT-COPY-GUARD] ghost-entry blocked: "` — all characters are printable ASCII (0x20–0x7E). Brackets, hyphens, letters, colon, space — all ASCII. ?

**Result: PASS — ASCII-only confirmed**

---

### SCAN-06: CYC audit

**Independent branch count:**

**`IsFollowerPositionBlocked` (lines 3056–3061):**

| Branch | Point | Delta | Running Total |
|--------|-------|-------|---------------|
| base | entry | 1 | 1 |
| `if (acc == null)` | line 3058 | +1 | 2 |
| `if (instr == null)` | line 3059 | +1 | 3 |
| `return HasOpenPosition(...)` | line 3060 | 0 (no branch) | 3 |

**CYC = 3 ? (budget =8)**

**`ShouldSkipFollower` (lines 3068–3090):**

| Branch | Point | Delta | Running Total |
|--------|-------|-------|---------------|
| base | entry | 1 | 1 |
| `if (IsFollowerPositionBlocked(acc, instr))` | line 3077 | +1 | 2 |
| `if (ShouldSkipFollowerDispatch(acc))` | line 3085 | +1 | 3 |
| `if (ShouldSkipForReversalGuard(...))` | line 3087 | +1 | 4 |
| `return false` | line 3089 | 0 | 4 |

**CYC = 4 ? (budget =8)**

**Result: PASS — IsFollowerPositionBlocked=3, ShouldSkipFollower=4, both =8**

---

### SCAN-07: acc.Orders enumeration in new/modified code

**Command:** `Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "acc\.Orders"` filtered to lines 3050–3095.

**Result:** No output — zero `acc.Orders` references in the T1 code region.

**Assessment:** `IsFollowerPositionBlocked` delegates to `HasOpenPosition` (line 5294), which in turn calls `FindPosition` — no direct `acc.Orders` enumeration. The ghost re-entry guard does NOT enumerate `acc.Orders`. ?

**Result: PASS — 0 acc.Orders calls in new/modified T1 code**

---

## Check 5 — Spec Satisfaction

**Requirement (FM-A root cause):** `DispatchCopy` ? `ShouldSkipFollower` ? `IsFollowerPositionBlocked` ? `HasOpenPosition` — live NT8 position truth.

| Chain Link | Expected | Found | Result |
|------------|----------|-------|--------|
| `DispatchCopy` calls `ShouldSkipFollower` | Pre-existing call | Confirmed in architecture plan (line 2679 ? 3068); not modified by T1 | PASS |
| `ShouldSkipFollower` calls `IsFollowerPositionBlocked` as FIRST statement | Line 3077 | Confirmed | PASS |
| `IsFollowerPositionBlocked` calls `HasOpenPosition` | Line 3060 | `return HasOpenPosition(acc, instr)` — exact match | PASS |
| `HasOpenPosition` exists | Line 5294 | `private bool HasOpenPosition(Account acc, Instrument instrument)` confirmed | PASS |
| `HasOpenPosition` uses live NT8 position (not reimplemented) | Delegates to `FindPosition` + `pos.Quantity > 0` | Lines 5294–5299 confirmed | PASS |

**Result: PASS — full dispatch chain verified**

---

## Check 6 — Cross-Check Against Engineer Self-Report

All claims in [`ticket-1-completion.md`](docs/brain/B159/ticket-1-completion.md) were independently verified:

| Engineer Claim | Independent Finding | Discrepancy |
|---|---|---|
| Signature: `internal bool IsFollowerPositionBlocked(Account acc, Instrument instr)` | Line 3056: confirmed exact | None |
| Location: lines 3056–3061 | Confirmed | None |
| CYC=3 | Independently counted: 3 | None |
| Delegates to `HasOpenPosition` — no duplicate logic | Line 3060 confirmed | None |
| Hook as FIRST statement in ShouldSkipFollower | Line 3077 confirmed | None |
| ShouldSkipFollower lines 3068–3090 | Confirmed | None |
| ShouldSkipFollower CYC rises 3?4 | Independently counted: 4 | None |
| Log: `[PTT-COPY-GUARD] ghost-entry blocked: {acc.Name} {instr.FullName}` | Lines 3079–3080 confirmed exact | None |
| SCAN-01: 0 lock() calls | Confirmed: comment-only hits | None |
| SCAN-02: 0 async void | Confirmed: comment-only hits | None |
| SCAN-03: 0 DateTime.Now | Confirmed: comment-only hits | None |
| SCAN-04: 0 Thread.Sleep | Confirmed: zero | None |
| SCAN-05: ASCII-only | Confirmed: zero non-ASCII bytes | None |
| SCAN-06: CYC=3/4 | Confirmed | None |
| SCAN-07: 0 bare acc.Orders in new code | Confirmed | None |
| ptt-sync: 18 linked, 0 errors | Not verifiable by verifier (sync output not in scope) | N/A |

**Minor doc discrepancy (non-code, no code violation):**
- Comment at line 3052 says "Delegates to HasOpenPosition (line 5273)" but `HasOpenPosition` is actually declared at **line 5294**. This is a stale line number in a comment — no functional impact. Not a VERIFY_FAIL.

**Result: PASS — no substantive discrepancies between engineer self-report and independent findings**

---

## DNA Rules Checklist

| Rule | Check | Result |
|------|-------|--------|
| JS-021: No `lock(` | SCAN-01: 0 executable lock() calls | PASS |
| JS-023: No Mutex/Semaphore for state | No such usage in new/modified code | PASS |
| JS-025: No shared Dictionary<K,V> | No new Dictionary fields; T1 adds no fields | PASS |
| JS-001: No throw in gate methods | No throw in IsFollowerPositionBlocked or ShouldSkipFollower | PASS |
| JS-002: No null return where non-null expected | Returns bool — no null possibility | PASS |
| JS-003: No magic string for state | No state string discrimination | PASS |
| JS-008/JS-009: No mutable struct / unfreezed brush | No struct or brush in new code | PASS |
| JS-010: Constructor visibility | No new constructor added | PASS |
| NT8: No async/await in gate methods | Confirmed — no async | PASS |
| NT8: No Account.All outside Loaded handler | Not used in new code | PASS |
| NT8: No `sealed` on TradeCopierWindow | Not touched | PASS |
| SCAN-03: No FontFamily WPF element | Not applicable — no WPF in new code | PASS |
| SCAN-04: No #RRGGBB hex color | No hex colors in new code | PASS |
| SCAN-05: No CreateOrder without PTT- prefix | No CreateOrder in T1 code | PASS |
| SCAN-06: No DateTime.Now | Confirmed — zero | PASS |
| ASCII-only (JS-042) | SCAN-05: zero non-ASCII bytes | PASS |

---

## Summary

| Check | Result |
|-------|--------|
| 1. Method exists with correct signature | **PASS** |
| 2. Implementation body correct (null guards, HasOpenPosition delegate, CYC=3) | **PASS** |
| 3. Hook placement in ShouldSkipFollower (first statement, log, return true, CYC=4) | **PASS** |
| 4a. SCAN-01: lock() | **PASS** |
| 4b. SCAN-02: async void | **PASS** |
| 4c. SCAN-03: DateTime.Now | **PASS** |
| 4d. SCAN-04: Thread.Sleep | **PASS** |
| 4e. SCAN-05: ASCII-only | **PASS** |
| 4f. SCAN-06: CYC audit | **PASS** |
| 4g. SCAN-07: acc.Orders | **PASS** |
| 5. Spec satisfaction (dispatch chain) | **PASS** |
| 6. Cross-check vs engineer self-report | **PASS** |
| DNA rules all clear | **PASS** |

---

## VERDICT

```
VERIFY_PASS
```

**Wave:** B159  
**Ticket:** DW-B159-T1  
**Violations:** none  
**Minor doc note (non-violation):** Comment at line 3052 cites HasOpenPosition at "line 5273"; actual declaration is line 5294. No functional impact.

---

*Verification performed by ptt-verifier. Phase 4b. Wave B159. Ticket DW-B159-T1 only.*
*All 7 scans run independently. Engineer self-report not used as substitute for independent verification.*
