# Ticket 4 (B163-VERIFY) -- Verification Report

**Epic**: B161-B166
**Ticket**: 4 -- B163-VERIFY
**Verifier**: ptt-verifier (independent Layer 3 scan)
**File**: `src/PropTraderTools/CopyEngine.cs`
**Method under review**: `IsPttEntryOrderCancelTrigger`
**Lines read independently**: L915-L935 (method + header comment)
**Date**: 2025-07-16

---

## FINAL VERDICT

**VERIFY_PASS**

All 7 mandatory scans pass (0 violations). AFP guard is present, live, correct.
B163/B164 coexist in different methods. All DNA rules satisfied. CYC within budget.

---

## 1. Independent Read -- AFP Guard (L932-L933)

Exact source lines read from `src/PropTraderTools/CopyEngine.cs`:

```
L915  // IsPttEntryOrderCancelTrigger: CYC=4. Returns true when a follower entry cancel should trigger re-place.
L916  // Matches HOTFIX-B66-COPY-REPLACE + HOTFIX-B66-NATIVE-ATM: "PTT-Copy" or "Entry" + Cancelled + LimitPrice>0.
L917  // B163 DW-B163: AverageFillPrice guard rejects lifecycle cancels of previously-filled Entry orders.
L918  //   NT8 cancels a filled ATM bracket Entry as normal lifecycle -- AverageFillPrice>0 identifies these.
L919  //   A never-filled cancelled Entry has AverageFillPrice==0.0 and IS a genuine re-place trigger.
L920  //   CYC: 3->4. Zero new fields. Zero new helpers. ASCII-only.
L921  // Called from pre-Gate-1 block in OnOrderUpdate.
L922  // JS-001: no throw. JS-002: returns bool (never null). JS-021: no lock. ASCII-only.
L923  // TESTABILITY: internal static, Order parameter, no NT8 runtime deps beyond field reads.
L924  internal static bool IsPttEntryOrderCancelTrigger(Order order)
L925  {
L926      if (order == null)
L927          return false; // (1)
L928      if (order.OrderState != OrderState.Cancelled)
L929          return false; // (2)
L930      if (order.Name != "PTT-Copy" && order.Name != "Entry")
L931          return false; // (3)
L932      if (order.AverageFillPrice > 0)
L933          return false; // (4) B163: lifecycle cancel of a previously-filled Entry -- not a re-place trigger
L934      return order.LimitPrice > 0 && order.Instrument?.FullName != null;
L935  }
```

### AFP Guard Checks

| Check | Result |
|---|---|
| `if (order.AverageFillPrice > 0)` present at L932 | CONFIRMED |
| `return false;` immediately follows at L933 | CONFIRMED |
| Inline comment includes "B163" tag | CONFIRMED ("(4) B163:") |
| Guard is live executable code (not commented out) | CONFIRMED -- not preceded by `//` |
| Guard is NOT reverted | CONFIRMED |

---

## 2. B163 Not Reverted

- L932 begins with 12 spaces + `if` (standard indentation) -- confirmed not `//`
- Guard is syntactically positioned between the Name check (L930-L931) and the final boolean return (L934)
- SCAN-02 confirms 0 non-ASCII bytes -- no invisible Unicode or ZWJ characters that could hide a comment marker

**Status: LIVE CODE CONFIRMED**

---

## 3. B163 + B164 Coexistence (Different Methods)

| Fix | Method | File location |
|---|---|---|
| B163 -- `AverageFillPrice > 0` guard | `IsPttEntryOrderCancelTrigger` | L932-L933 |
| B164 -- Signal 4 (`HasActivePttBeTargets`) | `IsFollowerPositionBlocked` | L3213 |

- `IsPttEntryOrderCancelTrigger`: internal static method, L924-L935
- `IsFollowerPositionBlocked`: internal instance method, L3182+
- These are **different methods** in different regions of the file
- B163 is NOT superseded by B164; they are orthogonal fixes
- B164 Signal 4 verified at L3213: `if (HasActivePttBeTargets(acc, instr)) // (7) Signal 4 -- post-Stop-fill race (B164)`

**Status: COEXISTENCE CONFIRMED -- DIFFERENT METHODS**

---

## 4. Five-Line Context -- No Side Effects

Context around AFP guard (L927-L934):

```
L927  return false; // (1) -- null guard result
L928  if (order.OrderState != OrderState.Cancelled)
L929      return false; // (2)
L930  if (order.Name != "PTT-Copy" && order.Name != "Entry")
L931      return false; // (3)
L932  if (order.AverageFillPrice > 0)       <-- B163 guard
L933      return false; // (4) B163: ...
L934  return order.LimitPrice > 0 && order.Instrument?.FullName != null;
```

Observations:
- All lines are field reads on the `Order` parameter -- zero mutations
- No logging, no allocations, no event invocations
- Method is a pure predicate (static, no instance state accessed)
- Method logic flow is intact: null -> state -> name -> AFP -> final predicate
- AFP guard insertion does not alter any pre-existing branch semantics

**Status: NO SIDE EFFECTS CONFIRMED**

---

## 5. Jane Street DNA Rules

### JS-001 (no throw in gate method)
- Scan: `Select-String -Pattern "throw"` scoped to L924-L935
- Result: 0 hits
- Method uses only `return false` and `return <bool expr>` -- no exceptions thrown
- **PASS**

### JS-002 (no null return path)
- Method return type: `bool` (value type, cannot be null in C#)
- All 5 exit paths: `return false` (x4) or `return order.LimitPrice > 0 && ...` (bool expr)
- Zero `return null` possible
- **PASS**

### JS-042 (ASCII-only comment text)
- SCAN-02 global: 0 non-ASCII bytes in entire CopyEngine.cs
- Specific L933 comment: `// (4) B163: lifecycle cancel of a previously-filled Entry -- not a re-place trigger`
  - Double hyphen `--` is ASCII 0x2D 0x2D (NOT em-dash U+2014)
  - No smart quotes, no Unicode arrows, no special punctuation
- **PASS**

### JS-021 (no lock)
- SCAN-01: `Select-String -Pattern "lock\("` across all PropTraderTools/*.cs
- Result: 0 hits across entire file set
- **PASS**

---

## 6. CYC Count (Independent)

Method: `IsPttEntryOrderCancelTrigger` (L924-L935)

**Project convention (if-block branches only):**

| # | Decision point | Line |
|---|---|---|
| 1 | `if (order == null)` | L926 |
| 2 | `if (order.OrderState != OrderState.Cancelled)` | L928 |
| 3 | `if (order.Name != "PTT-Copy" && order.Name != "Entry")` | L930 |
| 4 | `if (order.AverageFillPrice > 0)` [B163] | L932 |

**CYC = 4** (project convention -- matches L915 header `CYC=4` and L920 `CYC: 3->4`)

**Full McCabe (also counting `&&` and `?.` logical operators):**

Additional operators: 1 `&&` in L930, 1 `&&` in L934 = +2
Full McCabe CYC = 6 (well below P1 threshold of 8)

**Budget check: CYC <= 8 -- PASS (actual = 4 / 6 depending on convention)**

---

## 7. Logic Correctness Assessment: `AverageFillPrice > 0`

**NT8 invariant** (confirmed from L917-L919 header comment and NT8 AddOn API docs):
- `Order.AverageFillPrice` is a `double` property set by NT8 post-fill
- Unfilled order: `AverageFillPrice == 0.0` (NT8 default)
- Fully/partially filled order: `AverageFillPrice > 0` (set to fill price)
- After an ATM Entry fills and arms its bracket, NT8 subsequently cancels the original Entry
  order as lifecycle cleanup; this cancel arrives in `OnOrderUpdate` with
  `OrderState == Cancelled` AND `AverageFillPrice > 0`

**Logic flow:**

```
AverageFillPrice > 0  -->  return false   (lifecycle cancel, NOT a re-place trigger) [CORRECT]
AverageFillPrice == 0 -->  falls through  (genuine unfilled cancel, IS a re-place trigger) [CORRECT]
```

**Discriminant soundness:** The guard correctly separates the two cases using a single
field read. No edge cases exist where a genuine unfilled Entry would have AFP > 0 (NT8
only sets AFP after a fill has occurred). The logic is sound and minimal.

**Status: LOGIC CORRECT**

---

## 8. Full 7-Scan Results (Layer 3 -- Independent)

| Scan | Command run | Result | Status |
|---|---|---|---|
| SCAN-01 lock | `Select-String -Pattern "lock\("` all *.cs | 0 hits | PASS |
| SCAN-02 non-ASCII | `Get-Content *.cs \| Where-Object {$_ -match '[^\x00-\x7F]'}` | 0 hits | PASS |
| SCAN-03 FontFamily | `Select-String -Pattern "FontFamily"` | hits in comments only | PASS |
| SCAN-04 hex color | `Select-String -Pattern "#[0-9A-Fa-f]{6}"` | hits in comments only | PASS |
| SCAN-05 CreateOrder PTT- | all CreateOrder call sites reviewed | all PTT-* or "Entry" (NT8 ATM constraint) | PASS |
| SCAN-06 DateTime.Now | `Select-String -Pattern "DateTime\.Now[^U]"` | hits in comments only | PASS |
| SCAN-07 \block\s*\( | `Select-String -Pattern "\bblock\s*\("` | hits in comments only | PASS |

**SCAN-03/04/06/07 note**: All hits were in comment text (lines containing `No FontFamily`,
`// green #22c55e`, `No DateTime.Now`, `drain terminal-state block`). No executable code
violations found.

**SCAN-05 detail**: The only non-PTT- name in a CreateOrder call is `"Entry"` at L5731.
This is an NT8 architectural constraint: `StartAtmStrategy` requires the order be named
`"Entry"` to arm the bracket. This is documented in the NT8_FULL_REFERENCE.md and in the
method comment at L5707. This is not a SCAN-05 violation -- it is a required NT8 API pattern.

---

## 9. Cross-Check vs Engineer's Report (Layer 2 vs Layer 3)

| Item | Engineer (Layer 2) | Verifier (Layer 3) | Match? |
|---|---|---|---|
| AFP guard line | L932-L933 | L932-L933 | YES |
| Guard text | `if (order.AverageFillPrice > 0)` | `if (order.AverageFillPrice > 0)` | YES |
| Return value | `false` | `false` | YES |
| Comment tag | "(4) B163:" | "(4) B163:" | YES |
| CYC count | 4 | 4 (project conv.) / 6 (full McCabe) | YES |
| B164 method | `IsFollowerPositionBlocked` | `IsFollowerPositionBlocked` L3213 | YES |
| All 7 scans | PASS | PASS | YES |
| Guard is live | YES | YES | YES |

**No discrepancies between Layer 2 and Layer 3.**

---

## Final Status

**VERIFY_PASS**

- AFP guard `if (order.AverageFillPrice > 0) return false;` is present at L932-L933
- Guard is live executable code, not reverted, not commented out
- B163 is in `IsPttEntryOrderCancelTrigger`; B164 Signal 4 is in `IsFollowerPositionBlocked` -- confirmed different methods, both present
- 5-line context confirms zero side effects (pure field reads only)
- JS-001, JS-002, JS-042, JS-021: all PASS
- CYC = 4 (project convention), well within budget of 8
- `AverageFillPrice > 0` logic is sound and correctly discriminates lifecycle cancels from genuine re-place triggers
- All 7 mandatory scans: 0 violations
- Zero discrepancies with engineer's Layer 2 report

**Violations: none**
