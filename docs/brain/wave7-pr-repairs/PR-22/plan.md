# PR-22 Repair Plan — [LOGIC-BUG] Master Bracket-Order Key Derivation

**Branch**: `wave7/pr3-s1-sima-core`  
**File**: `src/V12_002.SIMA.Lifecycle.cs`  
**Location**: Lines 1220–1222 (master inline adoption path)  
**Status**: PLAN ONLY — do NOT touch src/

---

## 1. Root Cause

The master bracket-order adoption path uses a blanket `Substring(2)` fallback for all non-`Stop_` orders, producing corrupted dictionary keys like `"1_MOMO_001"` for `T1_`–`T5_` target orders instead of the correct `"MOMO_001"`, while the Fleet adoption path (`RouteOrderToTargetDict`) correctly uses `Substring(3)` for those same 3-char prefixes — causing the two paths to write and look up target orders under different keys (ghost orders on master account).

---

## 2. Exact Old Code

**File**: [`src/V12_002.SIMA.Lifecycle.cs`](src/V12_002.SIMA.Lifecycle.cs:1220)  
Lines 1220–1222:

```csharp
// Build dictionary key
string key = name.StartsWith("Stop_", StringComparison.OrdinalIgnoreCase)
    ? name.Substring(5)
    : name.Substring(2);
```

---

## 3. Exact New Code (Minimal Fix)

```csharp
// Build dictionary key — use classification to select correct prefix length
string key = classification == "stop"
    ? (name.StartsWith("Stop_", StringComparison.OrdinalIgnoreCase) ? name.Substring(5) : name.Substring(2))
    : name.Substring(3);
```

**Rationale for `Substring(3)` as the else branch**: All non-stop bracket prefixes are exactly 3 characters — `T1_`, `T2_`, `T3_`, `T4_`, `T5_`. The `"entry"` / `"Fleet_"` classification is already filtered out at line 1216 (`classification == "entry"` → `continue`), so it never reaches key derivation.

---

## 4. Jane Street Rationale (OKF Cite)

**`one_in_flight` — order tracking keys must be exact; wrong key = ghost orders**

The OKF principle requires that every in-flight order is reachable via its tracking dictionary using the exact key under which it was stored. When the master path stores `T1_MOMO_001` under key `"1_MOMO_001"` (Substring(2)) but all downstream lookups — position sync, bracket cancellation, GTC sweep — search for key `"MOMO_001"` (Substring(3) standard), the order becomes unreachable to the strategy. This is the textbook ghost-order scenario: a live bracket order the system cannot cancel or track.

**`correctness by construction` — illegal states unrepresentable**

The key derivation should be structurally incapable of producing a key that mismatches any path. By coupling the Substring offset to `classification` (which already encodes the prefix type), the illegal state of "same order stored under two different keys depending on adoption path" is eliminated by construction. The old code allowed that illegal state to silently compile and run.

---

## 5. Edge Case Analysis — All Prefixes

| Prefix   | Length | Classification | Old key for `T1_MOMO_001`-style | New key | Correct? |
|----------|--------|----------------|---------------------------------|---------|----------|
| `Stop_`  | 5      | `"stop"`       | Substring(5) → `"MOMO_001"` ✓  | classification=="stop" → StartsWith("Stop_") → Substring(5) → `"MOMO_001"` | ✅ |
| `S_`     | 2      | `"stop"`       | Substring(2) → `"MOMO_001"` ✓  | classification=="stop" → !StartsWith("Stop_") → Substring(2) → `"MOMO_001"` | ✅ |
| `T1_`    | 3      | `"target1"`    | Substring(2) → `"1_MOMO_001"` ❌ | classification!="stop" → Substring(3) → `"MOMO_001"` | ✅ FIXED |
| `T2_`    | 3      | `"target2"`    | Substring(2) → `"2_MOMO_001"` ❌ | Substring(3) → `"MOMO_001"` | ✅ FIXED |
| `T3_`    | 3      | `"target3"`    | Substring(2) → `"3_MOMO_001"` ❌ | Substring(3) → `"MOMO_001"` | ✅ FIXED |
| `T4_`    | 3      | `"target4"`    | Substring(2) → `"4_MOMO_001"` ❌ | Substring(3) → `"MOMO_001"` | ✅ FIXED |
| `T5_`    | 3      | `"target5"`    | Substring(2) → `"5_MOMO_001"` ❌ | Substring(3) → `"MOMO_001"` | ✅ FIXED |
| `Fleet_` | 6      | `"entry"`      | (skipped at line 1216 — `continue`) | (skipped — N/A) | ✅ |

**Stop orders in stopOrders that have `Fleet_` prefix (line 364 guard)**: The existing guard at line 364 (`if (key.StartsWith("Fleet_", StringComparison.OrdinalIgnoreCase)) continue`) is for a different lookup path that reconstructs master `activePositions` from `stopOrders`. That guard handles any historic key that might have snuck in via the old bug path; the fix here prevents the corrupted key from being written in the first place.

**Summary**: 5 prefix types were producing wrong keys (T1_–T5_). Both stop prefixes were accidentally correct by coincidence (`S_` is 2 chars, matching the hardcoded `Substring(2)`). The new code is correct for all 8 recognized prefixes.

---

## 6. CYC Delta

**Before fix (lines 1220–1222)**:  
One ternary expression = **1 decision point** added to the enclosing method.

**After fix (lines 1220–1222)**:  
Outer ternary (on `classification == "stop"`) + inner ternary (on `StartsWith("Stop_")`) = **2 decision points** added to the enclosing method.

**Net delta**: +1 decision point.

The enclosing method (master bracket adoption, roughly lines 1185–1249) already has ~5 decision points (state-guard conditions, foreach, switch with 5 cases). Adding 1 brings the method to ~6, well within the V12 mandate of **CYC ≤ 8**.

**No CYC violation.**

---

## Agent Tracking

- **Phase**: PR-22 Repair Plan
- **Authored by**: V12 Architecture Planner (Phase 2 role)
- **Sequential Thinking**: Used (3 thoughts — root cause derivation, fix verification, edge case table)
- **OKF consulted**: `one_in_flight`, `correctness by construction`
- **Src touched**: NO — plan only
- **Next action**: Hand to `v12-engineer` (Bob CLI) for surgical application of the 3-line fix
