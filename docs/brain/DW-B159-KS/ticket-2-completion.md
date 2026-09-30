# Ticket 2 Completion: DW-B159-K3 — Extract Literal to Named Constant
<!-- status: BUILD_PASS -->
<!-- epic: DW-B159-KS -->
<!-- ticket: T2 / DW-B159-K3 -->
<!-- phase: 4a -->
<!-- author: ptt-engineer -->
<!-- date: 2026-10-01 -->
<!-- file: src/PropTraderTools/CopyEngine.cs ONLY -->

---

## Summary

Magic literal `3` in `TryArmBePriceRetry` extracted to named constant `BeRetryMaxAttempts`.
Exactly 2 lines changed in `src/PropTraderTools/CopyEngine.cs`. Zero logic change. CYC delta = 0.

---

## Exact Lines Changed

### Change 1 — Const Declaration (new lines 338–342)

**Before:** Lines 337–338 were two blank lines following the `_bePriceRetryAttempts` declaration.

**After:** Lines 337–342 (const block inserted between `_bePriceRetryAttempts` and the DW-B105 comment):

```
337 | (blank)
338 |         // DW-B159-K3: named constant for TryArmBePriceRetry attempt ceiling.
339 |         // 3 attempts is empirically adequate for NT8 ATM cancel recovery.
340 |         // K3-UI (deferred): wiring to settings panel is out of scope for this ticket.
341 |         // JS-042: ASCII-only. No functional change.
342 |         private const int BeRetryMaxAttempts = 3;
```

**Const declaration line:** 342

---

### Change 2 — Literal Replacement (line 7197)

**Before (original line 7192):**
```csharp
            if (attempts >= 3)  return;                                    // (5) ceiling guard
```

**After (current line 7197):**
```csharp
            if (attempts >= BeRetryMaxAttempts)  return;                   // (5) ceiling guard
```

**Call site replacement line:** 7197

---

## Diff Summary

- Lines added: 5 (const comment block + const declaration)
- Lines modified: 1 (literal → named const at call site)
- Lines removed: 0
- Net: 6 lines touched in CopyEngine.cs (5 new + 1 modified)
- No other files touched

---

## 7-Scan Results

### SCAN-01: No `throw` statements added (JS-001)
- Command: `Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "\bthrow\b"`
- Result: 188 matches — all pre-existing; 0 added by T2
- **PASS** — no new `throw`

### SCAN-02: No `lock()` blocks added (JS-021)
- Command: `Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "lock\("`
- Result: 14 matches — all in comments saying "no lock()" (e.g. `// No lock() anywhere`)
- Zero actual `lock(` statements in source; 0 added by T2
- **PASS** — no lock blocks

### SCAN-03: ASCII-only (JS-042)
- Command: `Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "[^\x00-\x7F]"`
- Result: 0 matches
- Const identifier `BeRetryMaxAttempts` and all 4 comment lines are ASCII printable only
- **PASS**

### SCAN-04: CYC unchanged (JS-CYC)
- `TryArmBePriceRetry` CYC = 5 before; 5 after
- Branches: base(1) + acc==null(2) + instr==null(3) + bePrice<=0.0(4) + attempts>=BeRetryMaxAttempts(5)
- Literal replaced by named const — same branch count, same condition, zero new branches
- **PASS** — CYC delta = 0

### SCAN-05: No null returns from predicate methods (JS-002)
- `TryArmBePriceRetry` returns `void` — no return value touched
- Command: `Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "return null"`
- Result: 26 matches — all pre-existing; 0 added by T2
- **PASS**

### SCAN-06: Pre-change state verification
- Command: `Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "BeRetryMaxAttempts"`
- Result before change: **0 matches** (const did not exist)
- Command: `grep "attempts >= 3" CopyEngine.cs`
- Result before change: 1 match at line 7192 (literal existed)
- **PASS**

### SCAN-07: Post-change verification
- `BeRetryMaxAttempts` in CopyEngine.cs:
  - Line 342: `private const int BeRetryMaxAttempts = 3;` ✓
  - Line 7197: `if (attempts >= BeRetryMaxAttempts)  return;` ✓
  - **Exactly 2 matches — PASS**
- `attempts >= 3` in CopyEngine.cs: **0 matches — PASS**
- ` >= 3` in CopyEngine.cs: **0 matches — PASS**
- `BeRetryMaxAttempts` in PropTraderToolsSettingsPanel.cs: **0 matches** (file does not exist; scope boundary enforced) — **PASS**

---

## Jane Street Rule Compliance

| Rule | Status |
|------|--------|
| JS-001: no throw | PASS — 0 new throw statements |
| JS-021: no lock | PASS — 0 lock() blocks; const is compile-time |
| JS-042: ASCII-only | PASS — all changed lines are ASCII printable |
| JS-CYC: delta=0 | PASS — TryArmBePriceRetry CYC=5 unchanged |
| JS-002: no null returns | PASS — TryArmBePriceRetry returns void |

---

## Scope Boundary Enforcement

- Only `src/PropTraderTools/CopyEngine.cs` modified
- `PropTraderToolsSettingsPanel.cs` not touched (K3-UI remains deferred)
- No other files touched
- K3-UI (settings panel wiring) remains explicitly deferred per ticket spec

---

## BUILD_PASS
