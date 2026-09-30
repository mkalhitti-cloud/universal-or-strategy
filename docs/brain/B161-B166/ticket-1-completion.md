# Ticket 1 Completion — B161-VERIFY

**Epic:** B161-B166  
**Ticket:** 1 (B161-VERIFY)  
**Task:** Verification-only — no source modifications made  
**File:** src/PropTraderTools/CopyEngine.cs  
**Method:** TryReplaceOnAtmCancel (L947–L960)  

---

## 1. Exact Lines Found in src

```
935 |         }
936 |
937 |         // TryReplaceOnAtmCancel: CYC=5. Fuses IsPttEntryOrderCancelTrigger predicate with action.
938 |         // Eliminates a branch from OnOrderUpdate parent CCN budget.
939 |         // F3-repair: drain-owned guard added as first check (JS-021: no lock, ConcurrentDictionary).
940 |         // R2-V1-repair: drain-active account guard prevents spurious re-placement during active drain.
941 |         // B161-repair: IsFollowerPositionBlocked guard after IsPttEntryOrderCancelTrigger --
942 |         //   blocks ghost re-place during NT8 event burst when BE-All wipes follower bracket before
943 |         //   leader PTT-BE-Stop fill clears leader position. Guard is placed after the cheap name/state
944 |         //   filter to avoid O(n) signal scan on non-PTT cancels (HasActivePttBeExits, HasSettlingBeTargets).
945 |         //   Reuses B160 helpers: IsFollowerPositionBlocked(L3130). CYC: 4->5. Zero new fields/helpers.
946 |         // JS-001: no throw. JS-002: void. JS-021: no lock. ASCII-only.
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

---

## 2. Guard Present — Confirmation

**CONFIRMED PRESENT** at L955–L958:

```csharp
if (order.Account != null
    && order.Instrument != null
    && IsFollowerPositionBlocked(order.Account, order.Instrument))
    return; // BE-All race -- skip re-place (4)
```

Position: inserted AFTER `IsPttEntryOrderCancelTrigger` check (L953–L954) and BEFORE `ReplaceFollowerCopyOnAtmCancel` call (L959). Exactly as specified.

---

## 3. Old Code Absent — Confirmation

**CONFIRMED ABSENT**: There is no direct call to `ReplaceFollowerCopyOnAtmCancel` immediately after the `IsPttEntryOrderCancelTrigger` check without the `IsFollowerPositionBlocked` guard interposed.

The call chain is strictly:
1. drain-owned guard → early return (L949–L950)
2. drain-active account guard → early return (L951–L952)
3. `IsPttEntryOrderCancelTrigger` filter → early return (L953–L954)
4. `IsFollowerPositionBlocked` guard → early return (L955–L958)  ← **NEW GUARD**
5. `ReplaceFollowerCopyOnAtmCancel(order)` (L959)

No path reaches `ReplaceFollowerCopyOnAtmCancel` without passing through the `IsFollowerPositionBlocked` guard.

---

## 4. CYC Count

| Branch | Source | Running CYC |
|--------|--------|-------------|
| Base | method entry | 1 |
| `if (_drainOwnedOrderIds.ContainsKey(...))` L949 | +1 | 2 |
| `if (order.Account != null && _pendingDispatchDrains.ContainsKey(...))` L951 | +1 | 3 |
| `if (!IsPttEntryOrderCancelTrigger(order))` L953 | +1 | 4 |
| `if (order.Account != null && order.Instrument != null && IsFollowerPositionBlocked(...))` L955 | +1 | 5 |

**CYC = 5** — matches header comment `// TryReplaceOnAtmCancel: CYC=5` on L937.  
Budget: 5 ≤ 8 ✅

---

## 5. 7-Scan Checklist

| Scan | Rule | Finding | Result |
|------|------|---------|--------|
| SCAN-01 | JS-001 no-throw | Guard uses `return;` — no `throw` statement anywhere in method body (L948–L960) | ✅ PASS |
| SCAN-02 | JS-002 no-null-return | Method is `private void` — cannot return null; N/A | ✅ PASS (N/A) |
| SCAN-03 | JS-021 no-lock | No `lock(` in method body or inserted guard code | ✅ PASS |
| SCAN-04 | JS-042 ASCII-only | Comment `// BE-All race -- skip re-place (4)` verified ASCII: hyphen, space, digits — no Unicode, no curly quotes, no emoji | ✅ PASS |
| SCAN-05 | JS-CYC ≤ 8 | CYC = 5 ≤ 8 | ✅ PASS |
| SCAN-06 | NT8 API null-checks | `order.Account != null` and `order.Instrument != null` both checked before passing to `IsFollowerPositionBlocked(order.Account, order.Instrument)` | ✅ PASS |
| SCAN-07 | Signal continuity | N/A — `TryReplaceOnAtmCancel` is not `IsFollowerPositionBlocked`; no signal chain change | ✅ PASS (N/A) |

**All 7 scans: PASS**

---

## Final Status

**TICKET_COMPLETE**

All requirements verified:
- IsFollowerPositionBlocked guard PRESENT at L955–L958
- Guard positioned correctly: after `IsPttEntryOrderCancelTrigger` (L953), before `ReplaceFollowerCopyOnAtmCancel` (L959)
- Old direct-call pattern (no guard) ABSENT
- CYC = 5, within budget (≤ 8)
- All 7 scans pass
- No source modifications were made (verification-only task)
