# DW-B167-01 — DRAIN-ABORT-FLAT kills pre-fill entry drag on followers

**ID:** DW-B167-01
**Block origin:** B167 live-test session 2026-09-18
**Priority:** P1 — data loss. Follower entries disappear silently with no recovery.
**Status:** OPEN
**Source file:** `src/PropTraderTools/CopyEngine.cs`

---

## Symptom

Place a limit entry order on the leader. Before it fills, drag the leader entry to a new
price. The follower entry orders disappear and are never replaced at the new price.

Confirmed by output.md (2026-09-18) and NT log CSV (2026-09-18 01:19 PM):
- `[DRAIN] acct=Sim102 cancel-sent=1` (L31-L33 first instance, L318-L320 second instance)
- `[DRAIN-ABORT-FLAT] acct=Sim104` / `Sim102` / `Sim103` immediately after (L40-L42, L327-L329)
- NT log: `Entry:Cancelled` for all 4 accounts at 1:17:36 PM -- entries gone, never resubmitted.

---

## Root Cause

`SubmitDrainedEntry` at L8650 contains this guard (B155 DW-B154-04):

```csharp
if (IsFlat(FindPosition(follower, payload.Instrument))) // (3) abort if flat
{
    // [DRAIN-ABORT-FLAT] logged here
    return; // drain IDs cleaned up, intent discarded
}
```

This guard was designed for the post-fill flatten path: if the drain fires after the
position is already flat (QX/BE/flatten executed), resubmitting an entry would open a
ghost position -- correctly blocked.

**The bug:** the guard fires on the pre-fill path too. When the leader entry is dragged
before it fills, the follower account is flat (no position exists yet -- the entry has
not filled). `IsFlat` returns true, the guard aborts, and the payload (new price, qty,
action) is discarded. The follower account is left with no working entry and no position.

The condition is identical for both scenarios:
- Post-fill flatten: account flat because position was just closed -> abort CORRECT
- Pre-fill drag: account flat because entry never filled -> abort WRONG

---

## Code Location

`SubmitDrainedEntry` at L8650, guard at L8659:
```csharp
if (IsFlat(FindPosition(follower, payload.Instrument))) // (3) abort if flat
```

`DrainThenDispatch` at L8490 -- calls `IssueDrainCancels` then parks payload.
`OnDrainCancelAck` at L8624 -- decrements counter, fires `SubmitDrainedEntry` when 0.

---

## Fix Design

The flat guard must distinguish:
- Pre-fill drain (account flat, pending entry cancelled): resubmit REQUIRED
- Post-fill drain (account flat, position closed): resubmit MUST NOT happen

**Proposed discriminator:** the drain payload already records `DrainedOrderIds` (the
cancelled entry order IDs). At drain-cancel time the follower account is known to have
had a working entry. The flag `HasPosition` (false at drain time) is insufficient to
distinguish the two scenarios.

**Correct guard:** replace the flat check with a "has open position OR drain is
entry-only (no filled position)" check:

```
// Abort ONLY if flat AND the drain was not an entry-drag (i.e. position existed before drain).
// Entry-drag drain: account was flat at cancel time, intends to resubmit entry.
// Post-flatten drain: account was non-flat at cancel time, flatten fired during drain window.
```

The simplest safe fix is to store a `WasFlat` bool in `PendingDispatchDrain` at the
time `DrainThenDispatch` is called (before the cancels are sent). If the account was
already flat when the drain started (`WasFlat=true`), the resubmit should still happen
(pre-fill drag case). If the account was NOT flat when the drain started (`WasFlat=false`
= had a position), and is now flat when `SubmitDrainedEntry` fires, that is the
flatten/QX path -- abort correctly.

Revised guard:
```csharp
// Only abort if the account went flat DURING the drain (had position -> now flat).
// If account was already flat at drain-start, this is a pre-fill entry drag -- resubmit.
if (!payload.WasFlat && IsFlat(FindPosition(follower, payload.Instrument)))
{
    // [DRAIN-ABORT-FLAT] logged
    return;
}
```

`PendingDispatchDrain` constructor needs one new field:
```csharp
public bool WasFlat { get; }
```

`DrainThenDispatch` sets it at call time:
```csharp
bool wasFlat = IsFlat(FindPosition(follower, instrument));
var payload = new PendingDispatchDrain(..., wasFlat: wasFlat);
```

No other changes. `IsFlat` and `FindPosition` are already present. Zero new methods.
CYC delta on `SubmitDrainedEntry`: 0 (changes existing branch condition, no new branch).
CYC delta on `DrainThenDispatch`: 0 (wasFlat is a local var + constructor arg, no branch).

---

## SIM Gate Criteria

1. Place limit entry on leader (Sim101). Do NOT fill.
2. Drag leader entry to new price. Confirm:
   - `[DRAIN] acct=SimXXX cancel-sent=1` fires for each follower.
   - NO `[DRAIN-ABORT-FLAT]` fires.
   - `[DRAIN-SUBMIT]` fires for each follower with the new price.
   - Follower orders Working at new price (same as leader).
3. Let entry fill. Confirm brackets appear on all followers.
4. Run QX. Confirm `[DRAIN-ABORT-FLAT]` fires (verifies the post-flatten guard still works).

---

## Rules

- JS-CYC: no change to method CYC counts (condition change within existing branch).
- JS-021: no new locks. `IsFlat` and `FindPosition` are lock-free read-only paths.
- JS-001: no new throw paths.
- ASCII-only: no Unicode in added string literals.
- `PendingDispatchDrain` is a value-type record. Adding `WasFlat bool` is additive only.

---

*Written by copier-spec mode — 2026-09-18 — Evidence: output.md L31-L42, L318-L329; NT log 01-19 PM CSV.*
