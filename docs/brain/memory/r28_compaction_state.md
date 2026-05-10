# R28 Compaction State — Active Reasoning Cache
**Written:** 2026-04-10 by Claude (P3 Architect)
**Trigger:** Context Compaction Procedure, waiting on Codex background task
**Codex task ID:** ae2a1105a3447e625 (background, awaiting `<task-notification>`)
**Canonical plan:** [docs/brain/implementation_plan.md](../implementation_plan.md) (448 lines, v28.0)

---

## 1. Mission (one line)
Replace in-process heap `SPSCRing<T>` with MMIO-backed lock-free `MmioSpscRing<T>` in V12 NT8 strategy; sync entire codebase to build tag **`1111.002-v28.0`**.

## 2. Runtime Constraints (load-bearing — do not forget)
- **Target:** NinjaTrader 8 / .NET Framework 4.8 / C# 7.3
- **Unsafe:** `<AllowUnsafeBlocks>true</AllowUnsafeBlocks>` AUTHORIZED by Director
- **Banned APIs (do not exist in target):** `nint`, `Unsafe.*` NuGet, `NativeMemory`, `Span<T>`, `ArrayPool<T>`, `stackalloc` in expressions, `Environment.ProcessId`
- **ASCII-only** in all C# string literals (NT compiler breaks on non-ASCII)
- **No `lock(stateLock)`** anywhere in executable code (ban already enforced in working tree)

## 3. Path A API Translation Table (P1 payload → NT8-compatible)
| P1 Payload Symbol | NT8 Replacement | Reason |
|---|---|---|
| `nint` | `long` / `int` | C# 9+ only |
| `Unsafe.CopyBlockUnaligned` | `Buffer.MemoryCopy` | not in NT8 mscorlib |
| `Unsafe.AsPointer(ref Unsafe.AsRef(in x))` | `fixed (T* p = &x)` | not in NT8 mscorlib |
| `Unsafe.ReadUnaligned<T>(ptr)` | `*(T*)ptr` | not in NT8 mscorlib |
| `NativeMemory.Alloc` | `MemoryMappedFile.CreateOrOpen` + `AcquirePointer` | .NET 6+ only |
| `in T item` param | `ref T item` param | C# 7.3 `fixed` ergonomics |
| `namespace AntigravityOS.Kernel` | nested `private unsafe sealed class` inside `V12_002` | strategy-private scope + file-edit access |
| `Environment.ProcessId` | `Process.GetCurrentProcess().Id` | .NET 5+ only |

**Preserved verbatim:** XorShadow algorithm, `SHADOW_SALT = 0xDEADBEEFCAFEBABEUL`, cursor layout `[0..8)=prod, [64..72)=cons, [128..)=slots`, cache-line padding, `where T : unmanaged`, power-of-2 capacity check, `Volatile.Read`/`Volatile.Write` on both cursors.

## 4. D1-D8 Payload Defects — Resolution Map
| # | Defect | Resolution |
|---|---|---|
| D1 | `FleetDispatchSlot` not `unmanaged` (has Account + 2 strings) | New blittable slot; refs moved to parallel `FleetDispatchSideband[]` indexed by `SidebandIndex` (§4 step 2) |
| D2 | **Shadow overwrites last 8 bytes of user data** (correctness bug) | Slot contract: `ulong Shadow` is the LAST field of T; XorShadow computes over `[0..sizeof(T)-8)`, writes hash to that reserved slot (§4 step 3) |
| D3 | C# 11 / .NET 8+ APIs in .NET 4.8 host | Full API translation table above (§4 step 4) |
| D4 | `/unsafe` not established | RESOLVED — Director authorized `<AllowUnsafeBlocks>true</AllowUnsafeBlocks>` |
| D5 | Namespace placement breaks strategy-internal access | Nested inside `V12_002` partial class, namespace `NinjaTrader.NinjaScript.Strategies` |
| D6 | `byte* _region` lifetime undefined | MMF + `SafeMemoryMappedViewHandle.AcquirePointer` → `ReleasePointer` in `OnStateChange(Terminated)` (§4 step 5) |
| D7 | Cursor reads unfenced on hot path | Explicit `Volatile.Read` on every cursor load, not just writes |
| D8 | "14.25 ns/op, 8/8 AMAL" unsubstantiated | Flagged for audit trail only; plan does not depend on benchmark claim |

## 5. Build Tag Sync Targets (all must read `1111.002-v28.0` after Codex run)
- [src/V12_002.Constants.cs:12](../../../src/V12_002.Constants.cs#L12) — currently `"Build 972"` (stale, 139 builds behind)
- `src/V12_002.Properties.cs` — BUILD_TAG constant if present; Codex to grep and report
- [docs/brain/nexus_a2a.json](../nexus_a2a.json) — `mission`, `build_tag`, `phase`, `last_updated`
- Memory hook `project_state_build1004.md` in `~/.claude/...memory/`

## 6. Touch Sites (Codex scope for Phase 4 steps 2-9)
| File | Action |
|---|---|
| `src/V12_002.Photon.Ring.cs` | OVERWRITE — XorShadow + MmioSpscRing<T> nested class; delete legacy `ComputeProxyCrc` |
| `src/V12_002.Photon.Pool.cs` | Refactor `FleetDispatchSlot` to blittable; add `FleetDispatchSideband` + `_photonSideband[]` |
| `src/V12_002.Lifecycle.cs` | Replace line 204 with MMF region setup + `AcquirePointer`; add shutdown `ReleasePointer` + `Dispose` |
| `src/V12_002.cs` | Replace `_photonDispatchRing` type at line 323; add `_photonMmf`, `_photonMmfView`, `_photonRegionPtr` fields |
| `src/V12_002.SIMA.Dispatch.cs` | Lines 400, 505 — strip refs from slot, write sideband first |
| `src/V12_002.SIMA.Fleet.cs` | Lines 184, 211 — read refs from sideband |
| `src/V12_002.SIMA.Lifecycle.cs` | Line 96 — consumer pattern uses sideband |
| `src/V12_002.Constants.cs` | Line 12 — build tag bump |
| `docs/brain/nexus_a2a.json` | mission/build_tag/phase/last_updated fields |
| `docs/brain/implementation_plan.md` | Already overwritten by Architect (Step 1 complete) |

## 7. Verification Gates Codex Must Report (§5 of plan)
1. F5 compile zero errors (after Codex runs deploy-sync; Director presses F5)
2. ASCII gate on all modified .cs files
3. Static assert: `Marshal.OffsetOf(FleetDispatchSlot, "Shadow") == SizeOf(FleetDispatchSlot) - 8`
4. Roundtrip smoke test in `OnStartUp` → log `V12 R28 ring selftest OK`
5. Corruption injection → `crcValid=false`
6. Grep `lock\s*\(\s*stateLock\s*\)` in `src/` → zero hits
7. 10-min paper trading leak check → `PrivateMemorySize64` delta < 5 MB
8. Disable/enable lifecycle → no handle leak
9. Forensics subagent self-audit (per CLAUDE.md Engineer Self-Audit P4)

## 8. Post-Codex Protocol (Architect resumes on `<task-notification>`)
1. `Read(output_file_path)` from the task notification — ingest Codex report once.
2. Verify report matches `=== R28 CODEX EXECUTION REPORT ===` schema; if free-form, demand restructure.
3. Independently verify: grep working tree for the banned symbols (`nint`, `Unsafe\.`, `NativeMemory`, `Environment\.ProcessId`, non-ASCII in strings) — if any hit, report as audit failure to Director.
4. Verify build tag is `1111.002-v28.0` in all 4 sync targets (§5 above).
5. Instruct Director: `powershell -File .\deploy-sync.ps1` then F5 in NT8; verify banner.
6. On banner PASS, run `/multi_agent_audit` workflow per CLAUDE.md.
7. Update `nexus_a2a.json` `last_relay` with completion timestamp.

## 9. Director Decisions Pending (non-blocking)
- **D8 benchmark provenance** — does Director want me to relay defect list back to P1/Antigravity for payload reissue, or is inline Architect correction the permanent pattern? (Plan §7 item 3)
- **Monitor tool** — confirmed shipped in today's Claude Code release; not surfaced in this VSCode-extension session manifest; capability available via `Bash(run_in_background=true)` + `<task-notification>` + `Read(output_file)` (no polling). No action needed.

## 10. Identity & Protocol Anchors (do not drop)
- Claude is **P3 Architect**, plan-only default, **not** authorized to edit `src/` this session.
- Codex is **P4 Engineer** executing via `codex:codex-rescue` subagent in background.
- **NO SIMULATION** — must ingest real Codex output from the notification file, not invented.
- **NO IMPERSONATION** — if Codex fails or is unreachable, report to Director and wait.
- **Post-edit deploy-sync** is mandatory after any `src/` edit — file-edit tools break hard links.

---

**Compacted state above is sufficient to resume Round 28 on notification. Older forensic-verification reports, legacy 1209-line plan discussion, Monitor-tool investigation detail, and P5-rejected plan rejection history may be dropped from active reasoning.**
