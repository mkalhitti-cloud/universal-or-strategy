# Ticket 1 Verification — B161-VERIFY

**Epic:** B161-B166
**Ticket:** 1 (B161-VERIFY)
**Verifier:** ptt-verifier (Phase 4b)
**Date:** independent verification run
**Source:** `src/PropTraderTools/CopyEngine.cs` (READ ONLY)

---

## 1. Exact Lines Found in src (Independent Read — NOT from engineer report)

```
947 |         private void TryReplaceOnAtmCancel(Order order)
948 |         {
949 |             if (_drainOwnedOrderIds.ContainsKey(order.OrderId))
950 |                 return; // drain-owned cancel -- skip replacement (1)
951 |             if (order.Account != null && _pendingDispatchDrains.ContainsKey(order.Account.Name))
952 |                 return; // drain active for this account -- skip replacement (2)
953 |             if (!IsPttEntryOrderCancelTrigger(order))
954 |                 return; // (3)
955 |             if (order.Account != null
956 |                 && order.Instrument != null
957 |                 && IsFollowerPositionBlocked(order.Account, order.Instrument))
958 |                 return; // BE-All race -- skip re-place (4)
959 |             ReplaceFollowerCopyOnAtmCancel(order); // (5 - no branch)
960 |         }
```

Source verified by direct `read_file` on `src/PropTraderTools/CopyEngine.cs` lines 947–960.

---

## 2. IsFollowerPositionBlocked Guard — Presence and Position

**CONFIRMED PRESENT** at L955–L958.

```csharp
if (order.Account != null
    && order.Instrument != null
    && IsFollowerPositionBlocked(order.Account, order.Instrument))
    return; // BE-All race -- skip re-place (4)
```

**Position check:**
- Inserted **AFTER** `IsPttEntryOrderCancelTrigger` check at L953–L954 (guard 3)
- Inserted **BEFORE** `ReplaceFollowerCopyOnAtmCancel(order)` at L959 (guard 5)
- Exactly as specified in ticket T1 requirement

**Old pattern absent:** There is no path from L954 (`return; // (3)`) to L959 (`ReplaceFollowerCopyOnAtmCancel`) without traversing the L955–L958 guard. The old unguarded pattern is provably absent from the method body.

---

## 3. Syntactic Correctness Assessment

| Element | Assessment |
|---------|------------|
| Multi-line `if` compound condition (L955–L957) | Valid C# — three `&&`-joined bool sub-expressions, closing `)` on L957 |
| Single-statement body `return;` (no braces) | Valid C# — permitted for single-statement `if` bodies |
| Null-check ordering: `order.Account != null` before `order.Instrument != null` before method call | Correct short-circuit evaluation |
| `return;` in `private void` context | Correct — void early return |

**SYNTACTICALLY CORRECT.**

---

## 4. 5-Line Context Check — No Adjacent Code Affected

Lines read: L948–L960 (full method body including 5 lines before and after the guard).

| Line | Content | Status |
|------|---------|--------|
| L949 | drain-owned guard (1) — unchanged | Intact |
| L950 | `return;` — unchanged | Intact |
| L951 | drain-active guard (2) — unchanged | Intact |
| L952 | `return;` — unchanged | Intact |
| L953 | `IsPttEntryOrderCancelTrigger` guard (3) — unchanged | Intact |
| L954 | `return;` — unchanged | Intact |
| L955–L958 | NEW guard (4) — the B161 addition | Correct |
| L959 | `ReplaceFollowerCopyOnAtmCancel(order)` (5) — unchanged | Intact |
| L960 | `}` method closing brace — unchanged | Intact |

**No adjacent code was accidentally modified.** All pre-existing guards are verbatim. The addition is surgically isolated to L955–L958.

---

## 5. Jane Street Compliance

### JS-001 (no throw in gate methods)

Targeted scan of L947–L960 via PowerShell `Select-String -Pattern "throw\s"`:
```
0 matches
```
All exits in the method are `return;`. **PASS.**

### JS-021 (no lock)

Full-file scan via `Select-String -Pattern "lock\s*\("`:
```
All matches are in comments only (documenting absence of lock()).
Zero actual lock() statements in source.
```
Targeted scan of L947–L960: 0 actual `lock(` statements. **PASS.**

### JS-042 (ASCII-only comment)

Comment at L958: `// BE-All race -- skip re-place (4)`

Character verification:
- All characters are in ASCII range 0x20–0x7E
- Hyphens are `0x2D` (ASCII hyphen-minus) — NOT em-dash (U+2014)
- No curly quotes, no Unicode, no emoji

Independent non-ASCII scan of L947–L960 via PowerShell:
```
0 non-ASCII characters found
```
**PASS.**

### Full 7-Scan Results (B161 scope)

| Scan | Rule | Method | Finding | Result |
|------|------|--------|---------|--------|
| SCAN-01 | JS-001 no-throw | TryReplaceOnAtmCancel L947–L960 | 0 `throw` statements | ? PASS |
| SCAN-02 | JS-002 no-null-return | TryReplaceOnAtmCancel | `private void` — N/A; no null return possible | ? PASS (N/A) |
| SCAN-03 | JS-021 no-lock | TryReplaceOnAtmCancel L947–L960 | 0 actual `lock(` statements | ? PASS |
| SCAN-04 | JS-042 ASCII-only | L947–L960 | 0 non-ASCII chars; `BE-All race -- skip re-place (4)` verified ASCII | ? PASS |
| SCAN-05 | FontFamily | Full file | 0 actual `FontFamily=` elements; 3 comment hits only | ? PASS |
| SCAN-06 | DateTime.Now | Full file | 0 actual `DateTime.Now` usage; comment hits only | ? PASS |
| SCAN-07 | hex color #RRGGBB | Full file | 0 matches | ? PASS |

---

## 6. Call Chain Order Verification

Independent reading of L948–L960:

| Step | Line | Code | Spec Label | Order OK? |
|------|------|------|------------|-----------|
| (1) | L949–L950 | `if (_drainOwnedOrderIds.ContainsKey(...)) return;` | drain-owned check | ? |
| (2) | L951–L952 | `if (order.Account != null && _pendingDispatchDrains.ContainsKey(...)) return;` | drain-active check | ? |
| (3) | L953–L954 | `if (!IsPttEntryOrderCancelTrigger(order)) return;` | IsPttEntryOrderCancelTrigger | ? |
| (4) | L955–L958 | `if (... IsFollowerPositionBlocked(...)) return;` | IsFollowerPositionBlocked guard [NEW — B161] | ? |
| (5) | L959 | `ReplaceFollowerCopyOnAtmCancel(order);` | Final action | ? |

**Call chain order is correct. The required ordering is preserved exactly.**

---

## 7. CYC Count (Independent)

Method: `TryReplaceOnAtmCancel` (L947–L960).

Counting method: standard McCabe — 1 (base) + 1 per `if` decision point. Compound `&&` within a single `if` condition counts as the `if` only (not per operand).

| # | Line | Decision | Running CYC |
|---|------|----------|-------------|
| Base | — | method entry | 1 |
| +1 | L949 | `if (_drainOwnedOrderIds.ContainsKey(order.OrderId))` | 2 |
| +1 | L951 | `if (order.Account != null && _pendingDispatchDrains.ContainsKey(...))` | 3 |
| +1 | L953 | `if (!IsPttEntryOrderCancelTrigger(order))` | 4 |
| +1 | L955–L957 | `if (order.Account != null && order.Instrument != null && IsFollowerPositionBlocked(...))` | 5 |

**CYC = 5.**

- Matches header comment `// TryReplaceOnAtmCancel: CYC=5` at L937 ?
- Within budget (= 8) ?
- Delta: 4 ? 5 as documented (one new `if`-branch added by B161) ?

---

## 8. Cross-Check With Engineer's Layer 2 Report

| Item | Engineer (Layer 2) | Verifier (Layer 3) | Discrepancy? |
|------|-------------------|--------------------|--------------|
| Line range | L947–L960 | L947–L960 | None |
| Guard present | CONFIRMED L955–L958 | CONFIRMED L955–L958 | None |
| Guard position (after L953, before L959) | CONFIRMED | CONFIRMED | None |
| Exact guard code | Quoted verbatim | Matches source exactly | None |
| CYC | 5 | 5 | None |
| no-throw | PASS | PASS | None |
| no-lock | PASS | PASS | None |
| ASCII-only | PASS | PASS | None |
| Old direct-call absent | CONFIRMED | CONFIRMED | None |
| Call chain order | Correct | Correct | None |

**Zero discrepancies. Engineer's self-report is fully corroborated by independent scan.**

---

## 9. Final Verdict

```
VERIFY_PASS
```

All 7 scans: PASS
All Jane Street DNA rules (JS-001, JS-021, JS-042, JS-CYC): PASS
Guard present at correct location (L955–L958): CONFIRMED
Call chain order preserved: CONFIRMED
Syntactic correctness: CONFIRMED
No adjacent code affected: CONFIRMED
CYC = 5 (within budget): CONFIRMED
Engineer Layer 2 report: fully corroborated, zero discrepancies

**Ticket B161-VERIFY: VERIFY_PASS**
