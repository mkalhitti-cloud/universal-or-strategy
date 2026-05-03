# V12 Permanent DNA Reference

All code you write in an implementation plan MUST comply with every rule below.
Run through this checklist before finalizing any code block.

## Thread Safety

| Pattern | Status | Notes |
|---|---|---|
| `lock(stateLock)` in business logic | **BANNED** | Legacy pattern, removed in V12 |
| `Enqueue(ctx => ...)` for state mutations | **REQUIRED** | All strategy-thread writes go here |
| Direct write to `stopOrders` during bracket submission | **REQUIRED** | Build 981 exception -- Enqueue BANNED here |
| `Interlocked.*` for primitive counters | **ALLOWED** | |

## Order Safety (Follower Brackets)

| Pattern | Status |
|---|---|
| Raw `Cancel()` then `Submit()` for follower orders | **BANNED** -- creates ghost orders |
| Two-phase Replace FSM (`_followerReplaceSpecs`) | **REQUIRED** |
| FSM States | `PendingCancel` -> `OnAccountOrderUpdate` confirm -> `Submitting` -> `SubmitFollowerReplacement` |
| `Account.Change` for fleet accounts | **BANNED** -- silently no-ops on Apex/Tradovate |
| Raw `CancelOrder()` outside `CancelGateway.cs` | **BANNED** |

## String Safety (CRITICAL -- will cause 300 compiler errors if violated)

| Pattern | Status | Allowed Substitute |
|---|---|---|
| Emoji in C# strings | **BANNED** | `(!)` |
| Em-dash `--` in C# strings | **BANNED** | `--` (double hyphen) |
| Unicode arrows `->` in C# strings | **BANNED** | `->` (ASCII hyphen-gt) |
| Curly quotes `"` `"` in C# strings | **BANNED** | Straight `"` |
| Box-drawing characters | **BANNED** | `+`, `-`, `|` |

## File Operations

| Pattern | Status |
|---|---|
| Manual copy-paste for file splits > 50 lines | **BANNED** |
| Python extractor script (`scripts/<module>_split.py`) | **REQUIRED** for splits |
| Semaphore release outside `finally` block | **BANNED** |

## DNA Compliance Checklist (embed in every plan)

```
### DNA Compliance Checklist
- [ ] No lock(stateLock) introduced
- [ ] All new C# strings are ASCII-only
- [ ] Enqueue used for state mutations (or Direct Write justified per Build 981)
- [ ] FSM guard lines present if follower orders are touched
- [ ] grep -r "lock(stateLock)" src/ -- returns 0 results in modified files
- [ ] python check_ascii.py src/[modified files] -- passes
```
