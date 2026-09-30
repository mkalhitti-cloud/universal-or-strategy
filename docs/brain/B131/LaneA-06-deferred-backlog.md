# B131 LaneA Deferred Backlog

## Block Entry: B131 LaneA (DW-B138)

Date: 2026-08-31
Status: COMPLETE

---

### Deferred Items

| ID | Item | Priority | Reason Deferred |
|----|------|----------|-----------------|
| DW-H3-B131 | `OrderState.ChangeSubmitted` noise path: `IsWorkingBracket` correctly rejects `ChangeSubmitted` events (Working/Accepted only), causing a spurious `DispatchCopy` call. The subsequent `Working` event still reaches `HandleBracketChange` correctly, so this is not a correctness failure. Architecture plan Section A explicitly marked H3 "out of scope for DW-B138". | P2 | Not a correctness blocker. `ChangeSubmitted` fallthrough to `DispatchCopy` is noise; the drag lifecycle's `Working` event fires next and succeeds. Deferred to a future cleanup block. |

No P0 or P1 items deferred. All DW-B138 requirements fully implemented and independently verified.

---

### Notes for Next Block

1. **DW-H3-B131 context**: If the `ChangeSubmitted` noise path is ever addressed, the fix would be in
   `IsWorkingBracket` to also accept `OrderState.ChangeSubmitted`, or in `TryHandleBracketDrag` to
   suppress the `DispatchCopy` fallthrough for known bracket change events. Either approach is a
   single-method edit with CYC impact <= +1.

2. **B131 LaneB (DW-B139)**: Three placeholder `[Fact]` tests exist in `B131LaneBTests` class in
   `src/PropTraderTools/Tests/B131Tests.cs` (lines 111, 121, 131). These are structural placeholders
   from a prior session for DW-B139. LaneB work (if any) picks up from these stubs.

3. **InternalsVisibleTo**: `[assembly: InternalsVisibleTo("PropTraderTools.Tests")]` confirmed at
   CopyEngine.cs L46 (added by B113). No action needed for subsequent blocks using the same test
   assembly name.

4. **CYC accounting**: `FindFollowerBracketOrder` CYC is 4 (not 5 as the architecture plan stated).
   The architecture plan estimate of 5 was a draft-time error corrected by ticket reviewer annotation
   #1 and confirmed by independent verifier. The authoritative value is CYC=4. Future blocks should
   use 4 as the baseline for this method.

---

*End of Deferred Backlog -- B131 LaneA DW-B138*
