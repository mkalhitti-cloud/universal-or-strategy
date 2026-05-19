# P3 Loop-Critic: Symmetry.Replace.cs DNA Violations -- Repair Plan

Date: 2026-03-17 | Architect: Claude (P3) | Engineer: Codex (P4)
Trigger: Codex P2 Forensic Audit FAIL on Phase 8 delivery
Scope: src/V12_002.Symmetry.Replace.cs only -- 1 file, 2 surgical changes
Build: 1004 (repair-in-place, no BUILD_TAG increment)

---
## Violations Found by Codex

1. SymmetryGuardReplaceExistingFollowerTarget() lines 62-90: Banned lock(stateLock) at line 74 + raw Cancel()+Submit() anti-pattern at lines 65/88
2. SymmetryGuardSkipFollower() lines 106-111: Banned lock(stateLock) wrapping two field writes

---
## Change 1 (P0): SymmetryGuardReplaceExistingFollowerTarget

File: src/V12_002.Symmetry.Replace.cs

**Replacement Code:**

```csharp
            // Build 1004 [DNA-FIX]: Replace raw Cancel+lock(stateLock)+Submit with FollowerTargetReplaceSpec
            // two-phase FSM. Mirror pattern from Trailing.Breakeven.cs Build 957 C1.
            // Phase 1 (here): store spec and cancel only.
            // Phase 2 (automatic): AccountOrders.cs lines 352-382 detects cancel confirm by CancellingOrderId,
            // fires TriggerCustomEvent -> SubmitFollowerTargetReplacement() in Propagation.cs.
            if (oldTarget.OrderState == OrderState.Working ||
                oldTarget.OrderState == OrderState.Accepted ||
                oldTarget.OrderState == OrderState.Submitted ||
                oldTarget.OrderState == OrderState.ChangePending)
            {
                double newPrice = GetTargetPrice(pos, targetNumber);
                if (newPrice <= 0) return;

                OrderAction exitAction = pos.Direction == MarketPosition.Long ? OrderAction.Sell : OrderAction.BuyToCover;
                string signalName = SymmetryTrim(targetTag + "_" + fleetEntryName, 40);

                var tSpec = new FollowerTargetReplaceSpec
                {
                    EntryName         = fleetEntryName,
                    TargetNum         = targetNumber,
                    NewTargetPrice    = Instrument.MasterInstrument.RoundToTickSize(newPrice),
                    Quantity          = qty,
                    ExitAction        = exitAction,
                    TargetAccount     = pos.ExecutingAccount,
                    CancellingOrderId = oldTarget.OrderId
                };
                _followerTargetReplaceSpecs[signalName] = tSpec;
                // A1-2: Stamp REAPER grace window before cancel to suppress false desync during replace gap.
                StampReaperMoveGrace();
                pos.ExecutingAccount.Cancel(new[] { oldTarget });
            }
```

What changed:
- lock(stateLock) block: removed entirely
- CreateOrder + Submit: removed (deferred to SubmitFollowerTargetReplacement())
- dict[fleetEntryName] = replacement: removed (done by Propagation.cs:577)
- FollowerTargetReplaceSpec spec built and stored in _followerTargetReplaceSpecs before cancel
- newPrice and exitAction moved inside the cancellable-state guard (only needed when we proceed)

---
## Change 2 (P1): SymmetryGuardSkipFollower

File: src/V12_002.Symmetry.Replace.cs

**Replacement Code:**

```csharp
            // Build 1004 [DNA-FIX]: Replace lock(stateLock) with Enqueue actor write (no internal locks).
            // TotalContracts snapshot captured before lambda to prevent closure mutation.
            int _skipContractsSnap = pos.TotalContracts;
            Enqueue(ctx =>
            {
                pos.EntryFilled = true;
                if (pos.RemainingContracts <= 0)
                    pos.RemainingContracts = Math.Max(1, _skipContractsSnap);
            });
```

What changed:
- lock(stateLock): removed
- Two field writes serialized through actor Enqueue() pipeline
- TotalContracts captured before lambda (prevents closure over mutable field)

---
## P4 Self-Audit Checklist (P3 will verify on completion)

- No lock(stateLock) anywhere in Symmetry.Replace.cs
- No CreateOrder + Submit sequence in SymmetryGuardReplaceExistingFollowerTarget
- _followerTargetReplaceSpecs spec stored before Cancel() call
- CancellingOrderId = oldTarget.OrderId set correctly
- StampReaperMoveGrace() present immediately before Cancel() call
- dict[fleetEntryName] write removed from SymmetryGuardReplaceExistingFollowerTarget
- Enqueue() replaces lock(stateLock) in SymmetryGuardSkipFollower
- ASCII scan: check_ascii.py passes on Symmetry.Replace.cs
