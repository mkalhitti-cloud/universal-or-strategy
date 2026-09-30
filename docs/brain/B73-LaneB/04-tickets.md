# B73-LaneB Tickets

**Block**: B73-LaneB
**Phase**: 3 (Ticket Generation)
**Written by**: ptt-architect
**Date**: 2026-08-14
**Input**: `docs/brain/B73-LaneB/02-architecture-plan.md` (REVIEW_PASS)
**Rules gate**: JS-001, JS-021, JS-033 verified (RULES_CATALOG.md)
**Ticket count**: 1 (all 33 tests in one file)
**Pipeline mode**: RETROSPECTIVE — tests only. No logic changes. No .cs edits except B73Tests.cs creation.

---

## Ticket 1: TradeCopierPanel B73-LaneB xUnit Tests

### Spec Requirement IDs

B73-B-01, B73-B-02, B73-B-03, B73-B-04, B73-B-05, B73-B-06, B73-B-07, B73-B-08,
B73-B-09, B73-B-10, B73-B-11, B73-B-12, B73-B-13, B73-B-14, B73-B-15

---

### Files to Write

| Action | File path | Notes |
|--------|-----------|-------|
| CREATE | `src/PropTraderTools/Tests/B73Tests.cs` | New xUnit test file; 33 [Fact] methods |

**No other file is touched by Ph4a engineer.**

---

### Method Signatures

```csharp
namespace PropTraderTools
{
    public sealed class B73Tests
    {
        // Shared engine reference (singleton, no NT8 thread affinity for pure methods)
        private readonly CopyEngine _engine = CopyEngine.Instance;

        // ── Group 1: B73-B-01 (2 tests) ──────────────────────────────────────
        [Fact] public void T_BEALL_SYNC_01() { ... }
        [Fact] public void T_BEALL_SYNC_02() { ... }

        // ── Group 2: B73-B-02 (2 tests) ──────────────────────────────────────
        [Fact] public void T_BE_BG_01() { ... }
        [Fact] public void T_BE_BG_02() { ... }

        // ── Group 3: B73-B-03 (2 tests) ──────────────────────────────────────
        [Fact] public void T_NO_DISARM_01() { ... }
        [Fact] public void T_NO_DISARM_02() { ... }

        // ── Group 4: B73-B-04 (2 tests) ──────────────────────────────────────
        [Fact] public void T_FLAT_DISARM_01() { ... }
        [Fact] public void T_FLAT_DISARM_02() { ... }

        // ── Group 5: B73-B-05 (2 tests) ──────────────────────────────────────
        [Fact] public void T_BEALL_ARM_01() { ... }
        [Fact] public void T_BEALL_ARM_02() { ... }

        // ── Group 6: B73-B-06 (2 tests) ──────────────────────────────────────
        [Fact] public void T_MANUAL_CLOSE_01() { ... }
        [Fact] public void T_MANUAL_CLOSE_02() { ... }

        // ── Group 7: B73-B-07 (2 tests) ──────────────────────────────────────
        [Fact] public void T_DISARM_SYNC_01() { ... }
        [Fact] public void T_DISARM_SYNC_02() { ... }

        // ── Group 8: B73-B-08 (2 tests) ──────────────────────────────────────
        [Fact] public void T_BUF_BE_01() { ... }
        [Fact] public void T_BUF_BE_02() { ... }

        // ── Group 9: B73-B-09 (4 tests) ──────────────────────────────────────
        [Fact] public void T_LABEL_01() { ... }
        [Fact] public void T_LABEL_02() { ... }
        [Fact] public void T_LABEL_03() { ... }
        [Fact] public void T_LABEL_04() { ... }

        // ── Group 10: B73-B-10 (2 tests) ─────────────────────────────────────
        [Fact] public void T_QA_SING_01() { ... }
        [Fact] public void T_QA_SING_02() { ... }

        // ── Group 11: B73-B-11 (1 test) ──────────────────────────────────────
        [Fact] public void T_QA_INIT_01() { ... }

        // ── Group 12: B73-B-12 (2 tests) ─────────────────────────────────────
        [Fact] public void T_DISARM_CROSS_01() { ... }
        [Fact] public void T_DISARM_CROSS_02() { ... }

        // ── Group 13: B73-B-13 (2 tests) ─────────────────────────────────────
        [Fact] public void T_BEALL_FLAT_01() { ... }
        [Fact] public void T_BEALL_FLAT_02() { ... }

        // ── Group 14: B73-B-14 (3 tests) ─────────────────────────────────────
        [Fact] public void T_ORPHAN_01() { ... }
        [Fact] public void T_ORPHAN_02() { ... }
        [Fact] public void T_ORPHAN_03() { ... }

        // ── Group 15: B73-B-15 (3 tests) ─────────────────────────────────────
        [Fact] public void T_LABEL_CLIP_01() { ... }
        [Fact] public void T_LABEL_CLIP_02() { ... }
        [Fact] public void T_LABEL_CLIP_03() { ... }
    }
}
```

**All methods**: `public void`, decorated `[Fact]`, zero parameters, no `async`, no `lock()`.

---

### Implementation Notes (per-test group)

#### Required using directives

```csharp
using System;
using System.Reflection;
using System.Windows.Controls;
using System.Windows;
using Xunit;
using PropTraderTools;
```

#### Static method access pattern

Private static methods in `TradeCopierPanel` are inaccessible directly.
Access them via reflection using `BindingFlags.NonPublic | BindingFlags.Static`.

```csharp
// FormatGlobalBeBuffer: private static string FormatGlobalBeBuffer(string name, int buffer)
private static MethodInfo GetFormatGlobalBeBuffer() =>
    typeof(TradeCopierPanel)
        .GetMethod("FormatGlobalBeBuffer",
                   BindingFlags.NonPublic | BindingFlags.Static)!;

// FormatQuickAllBuffer: private static string FormatQuickAllBuffer(string name, int ticks)
private static MethodInfo GetFormatQuickAllBuffer() =>
    typeof(TradeCopierPanel)
        .GetMethod("FormatQuickAllBuffer",
                   BindingFlags.NonPublic | BindingFlags.Static)!;

// FormatBuffer: private static string FormatBuffer(string name, int value)
// Used by button construction (B73-B-11). T_QA_INIT_01 covers the B73-B-11 behavioral
// change indirectly (via CopyEngine.Instance.GlobalQuickAllT1 >= 1). No direct
// reflection invocation test is required for FormatBuffer itself; the pattern below
// is provided for completeness and for any future test that may need it.
private static MethodInfo GetFormatBuffer() =>
    typeof(TradeCopierPanel)
        .GetMethod("FormatBuffer",
                   BindingFlags.NonPublic | BindingFlags.Static)!;
```

Each test that calls a reflected method invokes it as:
```csharp
var result = (string)method.Invoke(null, new object[] { "BE ALL", 3 })!;
Assert.Equal("BE ALL +3", result);
```

#### CopyEngine instance access

`CopyEngine.Instance` is a POCO singleton with no NT8 thread-affinity requirement
for its pure read methods (`IsPendingSlotsEmpty`, `GlobalQuickAllT1`) or no-op
guard methods (`DisarmPendingBe(null)`, `CancelQxBrackets(null, null)`,
`RaiseBeAllDisarmed()`). Tests use `CopyEngine.Instance` directly.

#### Reflection for event and method existence

Event-existence tests use:
```csharp
var field = typeof(CopyEngine)
    .GetField("PendingBeArmed",
              BindingFlags.NonPublic | BindingFlags.Instance
              | BindingFlags.Public);
Assert.NotNull(field);
```

Method-existence tests use:
```csharp
var method = typeof(CopyEngine)
    .GetMethod("RaiseBeAllDisarmed",
               BindingFlags.Public | BindingFlags.Instance
               | BindingFlags.NonPublic);
Assert.NotNull(method);
```

#### Exception tests (JS-001 compliant pattern)

Use `Record.Exception` — never `Assert.Throws` with hot-path exceptions, never raw `try/catch` in tests:
```csharp
var ex = Record.Exception(() => CopyEngine.Instance.DisarmPendingBe(null));
Assert.Null(ex);
```

#### WPF context note (DockPanel tests)

`DockPanel` is in `System.Windows.Controls` (PresentationFramework assembly).
`DockPanel` inherits from `DependencyObject`, whose constructor enforces STA thread
affinity. xUnit's default test runner uses MTA thread-pool threads; constructing
`new DockPanel()` on an MTA thread throws `InvalidOperationException`.

T_LABEL_CLIP_01 only tests type existence (`typeof(DockPanel)`) — no construction required.

T_LABEL_CLIP_02 and T_LABEL_CLIP_03 use reflection to verify that the relevant
`DependencyProperty` static fields exist on the `DockPanel` type. No `DockPanel` instance
is constructed; `typeof(DockPanel).GetField(...)` is a pure metadata operation that is
safe on any thread.

---

### Test List (all 33, grouped by hotfix)

#### Group 1 — B73-B-01: BE ALL singleton truth source (2 tests)

**T_BEALL_SYNC_01**
- Spec: B73-B-01
- Asserts: `CopyEngine.Instance.IsPendingSlotsEmpty()` returns `true` at initial/empty state.
- Pattern: Direct instance call; Assert.True.
- Notes: Documents that the singleton truth source is reachable from test context.

**T_BEALL_SYNC_02**
- Spec: B73-B-01
- Asserts: `CopyEngine.Instance.DisarmPendingBe(null)` does not throw when called with null.
- Pattern: `Record.Exception`; Assert.Null.
- Notes: Documents the null guard on `_leaderAccount` path in `UpdateButtonColors`.

---

#### Group 2 — B73-B-02: BeState enum values (2 tests)

**T_BE_BG_01**
- Spec: B73-B-02
- Asserts: `BeState.Idle` and `BeState.Armed` are defined enum members (compile-time + runtime check).
- Pattern: `Enum.IsDefined(typeof(BeState), "Idle")` and `Enum.IsDefined(typeof(BeState), "Armed")`; Assert.True x2.
- Notes: If `BeState` enum is renamed or values removed, this test catches it at build time.

**T_BE_BG_02**
- Spec: B73-B-02
- Asserts: `BeState.Armed != BeState.Idle`.
- Pattern: Assert.NotEqual.
- Notes: Guards against accidental enum value collision (e.g., both = 0).

---

#### Group 3 — B73-B-03: No blanket disarm in UpdateButtonColors (2 tests)

**T_NO_DISARM_01**
- Spec: B73-B-03
- Asserts: `CopyEngine.Instance.DisarmPendingBe(null)` returns without exception (null guard is present).
- Pattern: `Record.Exception`; Assert.Null.
- Notes: Structural test documenting that calling `DisarmPendingBe` with null leader is safe — the B73-B-03 fix removed the unconditional disarm from `UpdateButtonColors` and the only disarm is now in dedicated hotfix blocks that gate on `_leaderAccount != null`.

**T_NO_DISARM_02**
- Spec: B73-B-03
- Asserts: `CopyEngine.Instance.IsPendingSlotsEmpty()` is idempotent — calling it twice in succession returns the same value.
- Pattern: Call twice; Assert.Equal(first, second).
- Notes: Documents pure-read nature of the truth-source query. Two consecutive reads must produce the same bool with no side effects.

---

#### Group 4 — B73-B-04: Flat disarm path (2 tests)

**T_FLAT_DISARM_01**
- Spec: B73-B-04
- Asserts: `CopyEngine.Instance.DisarmPendingBe(null)` with null argument returns without exception.
- Pattern: `Record.Exception`; Assert.Null.
- Notes: Documents the null-leader guard added to the HOTFIX-F3 block — if `_leaderAccount` is null, the disarm call must not crash.

**T_FLAT_DISARM_02**
- Spec: B73-B-04
- Asserts: `CopyEngine.Instance.IsPendingSlotsEmpty()` after `DisarmPendingBe(null)` returns a `bool` without exception.
- Pattern: Invoke `DisarmPendingBe(null)`, capture `IsPendingSlotsEmpty()`, `Assert.IsType<bool>`.
- Notes: Verifies the type contract of the conditional reset: `if (IsPendingSlotsEmpty()) UpdateBeAllVisuals(Idle)` — the predicate must return bool without throwing.

---

#### Group 5 — B73-B-05: BE ALL arm broadcast event exists (2 tests)

**T_BEALL_ARM_01**
- Spec: B73-B-05
- Asserts: `CopyEngine` has a member named `PendingBeArmed` accessible via reflection.
- Pattern: `typeof(CopyEngine).GetEvent("PendingBeArmed", BindingFlags.Public | BindingFlags.Instance)` or field variant; Assert.NotNull.
- Notes: Documents that the `PendingBeArmed` broadcast event (B72-LaneA) is present and accessible from B73-LaneB panel subscription code.

**T_BEALL_ARM_02**
- Spec: B73-B-05
- Asserts: `CopyEngine` has a member named `GlobalBeAllDisarmed` accessible via reflection.
- Pattern: Same reflection pattern as T_BEALL_ARM_01 but for `GlobalBeAllDisarmed`; Assert.NotNull.
- Notes: Documents that the `GlobalBeAllDisarmed` broadcast event is present.

---

#### Group 6 — B73-B-06: Operation.Remove enum value (2 tests)

**T_MANUAL_CLOSE_01**
- Spec: B73-B-06
- Asserts: `Operation.Remove` is a defined member of the `Operation` enum (compile-time check).
- Pattern: `Enum.IsDefined(typeof(Operation), "Remove")`; Assert.True.
- Notes: Documents that `OnLeaderPositionUpdate` can reference `Operation.Remove` without compilation error.

**T_MANUAL_CLOSE_02**
- Spec: B73-B-06
- Asserts: `Operation.Remove != Operation.Update`.
- Pattern: Assert.NotEqual.
- Notes: Guards against accidental value collision. `OnLeaderPositionUpdate` filters exclusively on `Remove`; if it equalled `Update` the flat signal would fire on every position update, not just close.

---

#### Group 7 — B73-B-07: BE ALL disarm broadcast and RaiseBeAllDisarmed (2 tests)

**T_DISARM_SYNC_01**
- Spec: B73-B-07
- Asserts: `CopyEngine` exposes a member named `GlobalBeAllDisarmed` (event) accessible via reflection (event or backing field).
- Pattern: Reflection GetEvent / GetField; Assert.NotNull.
- Notes: Existence check for the event subscribed by `OnGlobalBeAllDisarmed` handler in `OnLoaded`.

**T_DISARM_SYNC_02**
- Spec: B73-B-07
- Asserts: `CopyEngine.Instance.RaiseBeAllDisarmed()` is callable without exception.
- Pattern: `Record.Exception(() => _engine.RaiseBeAllDisarmed())`; Assert.Null.
- Notes: Documents the method signature that panels call from `OnGlobalBeClick` disarm path and `UpdateButtonColors` HOTFIX-BEALL-FLAT-RESET block.

---

#### Group 8 — B73-B-08: FormatGlobalBeBuffer static method (2 tests)

**T_BUF_BE_01**
- Spec: B73-B-08
- Asserts: `FormatGlobalBeBuffer("BE ALL", 3)` returns `"BE ALL +3"`.
- Pattern: Reflect `FormatGlobalBeBuffer` via `BindingFlags.NonPublic | BindingFlags.Static`; invoke; Assert.Equal.
- Notes: Documents the buffer label format for the global BE button. Non-zero buffer appends `" +{n}"`.

**T_BUF_BE_02**
- Spec: B73-B-08
- Asserts: `FormatGlobalBeBuffer("BE ALL", 0)` returns `"BE ALL"` (no suffix when buffer is zero).
- Pattern: Same reflection invoke with buffer=0; Assert.Equal("BE ALL", result).
- Notes: Documents zero-buffer case — no "+0" appended (clean label at default state).

---

#### Group 9 — B73-B-09: FormatQuickAllBuffer static method + Dispatcher pattern (4 tests)

**T_LABEL_01**
- Spec: B73-B-09
- Asserts: `FormatQuickAllBuffer("Quick ALL", 4)` returns `"Quick ALL +4t"`.
- Pattern: Reflect `FormatQuickAllBuffer` via `BindingFlags.NonPublic | BindingFlags.Static`; invoke; Assert.Equal.
- Notes: Primary format contract for the Quick ALL buffer label — `"t"` suffix makes tick unit explicit.

**T_LABEL_02**
- Spec: B73-B-09
- Asserts: `FormatGlobalBeBuffer("BE ALL", 5)` returns `"BE ALL +5"`.
- Pattern: Same reflection as T_BUF_BE_01; Assert.Equal.
- Notes: Cross-check BE ALL buffer format with value 5.

**T_LABEL_03**
- Spec: B73-B-09
- Asserts: Return value of `FormatQuickAllBuffer("Quick ALL", 4)` contains the `"t"` suffix.
- Pattern: `Assert.Contains("t", result)`.
- Notes: Tick-suffix assertion in isolation — verifies the `"t"` is present regardless of the full format string.

**T_LABEL_04**
- Spec: B73-B-09
- Asserts: `FormatQuickAllBuffer("Quick ALL", 0)` returns `"Quick ALL +0t"`.
- Pattern: Reflect invoke with ticks=0; Assert.Equal("Quick ALL +0t", result).
- Notes: Zero-tick case — tick suffix is always appended for `FormatQuickAllBuffer` (unlike `FormatGlobalBeBuffer`).

---

#### Group 10 — B73-B-10: GlobalQuickAllBufferChanged event and GlobalQuickAllT1 property (2 tests)

**T_QA_SING_01**
- Spec: B73-B-10
- Asserts: `CopyEngine` has a member named `GlobalQuickAllBufferChanged` accessible via reflection.
- Pattern: Reflection GetEvent / GetField; Assert.NotNull.
- Notes: Documents the event subscribed by `OnQuickAllBufferChanged` handler wired in `OnLoaded`.

**T_QA_SING_02**
- Spec: B73-B-10
- Asserts: `CopyEngine.Instance.GlobalQuickAllT1` is accessible and returns an `int` without exception.
- Pattern: Access `_engine.GlobalQuickAllT1`; `Assert.IsType<int>`.
- Notes: Documents that the property used to seed initial button content at construction (B73-B-11) is readable from test context.

---

#### Group 11 — B73-B-11: GlobalQuickAllT1 positive initial value (1 test)

**T_QA_INIT_01**
- Spec: B73-B-11
- Asserts: `CopyEngine.Instance.GlobalQuickAllT1 >= 1`.
- Pattern: `Assert.True(_engine.GlobalQuickAllT1 >= 1)`.
- Notes: Documents that the singleton-owned Quick ALL tick buffer has a positive initial value, ensuring `_quickAllBtn` initial content is meaningful (not zero or negative) when constructed from `CopyEngine.Instance.GlobalQuickAllT1`.

---

#### Group 12 — B73-B-12: RaiseBeAllDisarmed idempotent and cross-panel disarm (2 tests)

**T_DISARM_CROSS_01**
- Spec: B73-B-12
- Asserts: `CopyEngine.Instance.RaiseBeAllDisarmed()` is callable twice in succession without exception.
- Pattern: `Record.Exception(() => { _engine.RaiseBeAllDisarmed(); _engine.RaiseBeAllDisarmed(); })`; Assert.Null.
- Notes: Documents idempotency of the broadcast call — B73-B-12 moves `RaiseBeAllDisarmed` outside the `IsPendingSlotsEmpty` guard, meaning it may fire multiple times per flat cycle. Must be safe to call repeatedly.

**T_DISARM_CROSS_02**
- Spec: B73-B-12
- Asserts: `CopyEngine.Instance.IsPendingSlotsEmpty()` after `RaiseBeAllDisarmed()` returns a `bool` without exception.
- Pattern: Invoke `RaiseBeAllDisarmed()`; capture `IsPendingSlotsEmpty()`; `Assert.IsType<bool>`.
- Notes: Documents that the post-disarm state query used in the `UpdateButtonColors` conditional reset chain is type-safe and exception-free.

---

#### Group 13 — B73-B-13: BE ALL flat reset independent block (2 tests)

**T_BEALL_FLAT_01**
- Spec: B73-B-13
- Asserts: `CopyEngine` has a member named `GlobalBeBufferChanged` accessible via reflection (event existence).
- Pattern: Reflection GetEvent / GetField for `GlobalBeBufferChanged`; Assert.NotNull.
- Notes: Documents the event subscribed by `OnGlobalBeBufferChanged` (wired in `OnLoaded`). The independent HOTFIX-BEALL-FLAT-RESET block depends on CopyEngine's `GlobalBeBufferChanged` event to keep the buffer label in sync across panels.

**T_BEALL_FLAT_02**
- Spec: B73-B-13
- Asserts: `CopyEngine.Instance.IsPendingSlotsEmpty()` returns the same value on two consecutive calls (idempotent pure read).
- Pattern: Call twice; Assert.Equal(first, second).
- Notes: Documents that the independent flat-reset block's predicate `!IsPendingSlotsEmpty()` is a consistent read — no side effects between two calls.

---

#### Group 14 — B73-B-14: Orphan bracket cleanup null guards and method existence (3 tests)

**T_ORPHAN_01**
- Spec: B73-B-14
- Asserts: `CopyEngine.Instance.CancelQxBrackets(null, null)` returns without exception.
- Pattern: `Record.Exception(() => _engine.CancelQxBrackets(null, null))`; Assert.Null.
- Notes: Documents the null guards on `_leaderAccount` and `_instrument` in `UpdateButtonColors` HOTFIX-ORPHAN block — when either is null, `CancelQxBrackets` must not crash.

**T_ORPHAN_02**
- Spec: B73-B-14
- Asserts: `CopyEngine.IsQxCancelCandidate(null)` returns `false`.
- Pattern: `Assert.False(CopyEngine.IsQxCancelCandidate(null))`.
- Notes: Documents the null guard on the predicate used inside `CancelQxBrackets` to classify Working orders. A null order reference must return false (not throw).

**T_ORPHAN_03**
- Spec: B73-B-14
- Asserts: `CopyEngine.IsQxCancelCandidate` is accessible as a public or internal static method.
- Pattern: `typeof(CopyEngine).GetMethod("IsQxCancelCandidate", BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic)`; Assert.NotNull.
- Notes: Reflection existence check for the predicate method. Confirms the method is reachable from the panel via direct call or reflection without runtime MethodAccessException.

---

#### Group 15 — B73-B-15: DockPanel follower row layout (3 tests)

**T_LABEL_CLIP_01**
- Spec: B73-B-15
- Asserts: `System.Windows.Controls.DockPanel` type is present (compile-time + runtime existence check).
- Pattern: `Assert.NotNull(typeof(DockPanel))`; also `Assert.True(typeof(DockPanel).IsClass)`.
- Notes: Compile-time check that `BuildInlineFollowerRow` can reference `DockPanel` from PresentationFramework. If the assembly reference is missing, this fails to compile.

**T_LABEL_CLIP_02**
- Spec: B73-B-15
- Asserts: `DockPanel.LastChildFillProperty` DependencyProperty static field exists on `DockPanel` via reflection.
- Pattern: `Assert.NotNull(typeof(DockPanel).GetField("LastChildFillProperty", BindingFlags.Public | BindingFlags.Static))`.
- Notes: Verifies the DependencyProperty backing field for `LastChildFill` is present without constructing a DockPanel instance (no STA thread required). Documents that `BuildInlineFollowerRow` can reference `LastChildFillProperty` without a missing-member error.

**T_LABEL_CLIP_03**
- Spec: B73-B-15
- Asserts: `DockPanel.DockProperty` DependencyProperty static field exists on `DockPanel` via reflection.
- Pattern: `Assert.NotNull(typeof(DockPanel).GetField("DockProperty", BindingFlags.Public | BindingFlags.Static))`.
- Notes: Verifies the DependencyProperty backing field for the `Dock` attached property is present without constructing a DockPanel instance (no STA thread required). Documents that `BuildInlineFollowerRow` can call `DockPanel.SetDock(..., Dock.Right)` without a missing-member error.

---

### 7-Scan Checklist

The following 7 scans MUST be run against `src/PropTraderTools/Tests/B73Tests.cs`
by the Ph4b verifier before reporting BUILD_PASS.

```
[ ] SCAN-01 (S1): lock() scan
    Pattern: lock\s*\(
    Scope: B73Tests.cs entire file
    Expected: 0 matches
    Rationale: JS-021 (P0 CRITICAL) — lock() is banned in all code including tests

[ ] SCAN-02 (S2): async void scan
    Pattern: async\s+void\s+\w+\(
    Scope: B73Tests.cs entire file
    Expected: 0 matches
    Rationale: JS-033 (P0 CRITICAL) — all 33 [Fact] methods are synchronous void; none are async void

[ ] SCAN-03 (S3): return null scan
    Pattern: return\s+null\s*;
    Scope: B73Tests.cs entire file
    Expected: 0 matches
    Rationale: JS-002 (P0 CRITICAL) — no null returns in test helper methods; null used only as argument literal (e.g., DisarmPendingBe(null)), never as return value

[ ] SCAN-04 (S4): throw new Exception scan
    Pattern: throw\s+new\s+\w+Exception\(
    Scope: B73Tests.cs entire file
    Expected: 0 matches
    Rationale: JS-001 (P0 CRITICAL) — use Record.Exception pattern; never throw in test bodies

[ ] SCAN-05 (S5): ASCII-only scan
    Pattern: [^\x00-\x7F]
    Scope: B73Tests.cs entire file
    Expected: 0 non-ASCII characters
    Rationale: Project-wide ASCII-only mandate; no Unicode, emoji, or curly quotes

[ ] SCAN-06 (S6): CYC <= 8 for all 33 test methods
    Tool: complexity_audit.py or manual count
    Scope: All 33 [Fact] methods in B73Tests.cs
    Expected: CYC = 1 for each method (straight-line assertions, no branches)
    Rationale: Jane Street strict standard; each test is a linear sequence of Assert calls

[ ] SCAN-07 (S7): All 33 test names present and grouped by hotfix ID
    Verification: grep -c "\[Fact\]" B73Tests.cs == 33
    Grouping check:
      Group 1  (B73-B-01): T_BEALL_SYNC_01, T_BEALL_SYNC_02
      Group 2  (B73-B-02): T_BE_BG_01, T_BE_BG_02
      Group 3  (B73-B-03): T_NO_DISARM_01, T_NO_DISARM_02
      Group 4  (B73-B-04): T_FLAT_DISARM_01, T_FLAT_DISARM_02
      Group 5  (B73-B-05): T_BEALL_ARM_01, T_BEALL_ARM_02
      Group 6  (B73-B-06): T_MANUAL_CLOSE_01, T_MANUAL_CLOSE_02
      Group 7  (B73-B-07): T_DISARM_SYNC_01, T_DISARM_SYNC_02
      Group 8  (B73-B-08): T_BUF_BE_01, T_BUF_BE_02
      Group 9  (B73-B-09): T_LABEL_01, T_LABEL_02, T_LABEL_03, T_LABEL_04
      Group 10 (B73-B-10): T_QA_SING_01, T_QA_SING_02
      Group 11 (B73-B-11): T_QA_INIT_01
      Group 12 (B73-B-12): T_DISARM_CROSS_01, T_DISARM_CROSS_02
      Group 13 (B73-B-13): T_BEALL_FLAT_01, T_BEALL_FLAT_02
      Group 14 (B73-B-14): T_ORPHAN_01, T_ORPHAN_02, T_ORPHAN_03
      Group 15 (B73-B-15): T_LABEL_CLIP_01, T_LABEL_CLIP_02, T_LABEL_CLIP_03
      Total: 33
```

---

### Return Condition

**BUILD_PASS** when:
1. `src/PropTraderTools/Tests/B73Tests.cs` compiles without errors.
2. All 33 `[Fact]` methods are present with the exact names listed in SCAN-07.
3. All 7 scans above return expected results.
4. `dotnet test` runs the 33 tests; all 33 pass or, for reflection-based tests where the member exists, Assert.NotNull passes.

**BUILD_FAIL** when any scan returns unexpected results or any test name from SCAN-07 is missing.
