=== ROUND 28 v28.0 HANDOFF: MmioDispatchMirror Hybrid ===
FROM:  Claude (P3 Architect, Opus 4.6)
TO:    Codex (P4 primary) / Jules (P4 standby)
TAG:   1111.002-v28.0  (bump from 1109.003-v14.2)
PLAN:  docs/brain/implementation_plan.md  (authoritative spec, 1229 lines)
SUPERSEDES: .claude/plans/abundant-humming-puppy.md  (unsafe-kernel variant, invalidated 2026-04-11)

P5 AUDIT GATE: PASSED
  Codex Forensics: VERDICT PASS, RECOMMENDATION APPROVE (artifacts/audits/r28v28_codex_forensics.md)
  Gemini DNA: WAIVED (API quota exhausted; artifacts/audits/r28v28_gemini_waiver.md)
  Jules Hot-path: WAIVED (binary ENOENT; artifacts/audits/r28v28_jules_waiver.md)

TARGET RUNTIME:
  NinjaTrader 8 / .NET Framework 4.8 / C# 7.3 (NT8 internal NinjaScript compiler)

FORBIDDEN:
  unsafe, byte*, fixed blocks, System.Runtime.CompilerServices.Unsafe.*,
  nint, Span<T>, stackalloc expressions, NativeMemory, Environment.ProcessId,
  AcquirePointer(ref byte*), C# 8+ features

REQUIRED:
  managed-only primitives, MemoryMappedViewAccessor.Read<T>/Write<T>/Write(long,long),
  Marshal.SizeOf + Marshal.OffsetOf, [StructLayout(LayoutKind.Explicit, Size=64)]
  on FleetDispatchSlot, ASCII-only string literals, no lock(stateLock),
  no agent impersonation, no silent fallback on shadow mismatch

EXECUTION ORDER (NO reordering, NO skipping):
  Step 0  -- MmfGate preflight (create src/V12_002.MmfGate.cs, deploy-sync, F5, delete on pass)
  Step 2  -- Delete src/V12_002.UnsafeGate.cs (restores build integrity)
  Step 3  -- Refactor FleetDispatchSlot to blittable [LayoutKind.Explicit, Size=64] in Photon.Pool.cs
             + add FleetDispatchSideband struct + _photonSideband[] + _photonShadowSalt
  Step 4  -- Append ComputeFleetDispatchShadow helper to Photon.Pool.cs
  Step 5  -- OVERWRITE Photon.Ring.cs (strip CRC16; new TryEnqueue(ref T) / TryDequeue(out T) signatures)
  Step 6  -- NEW Photon.MmioMirror.cs (sealed class MmioDispatchMirror + TryPublish + Dispose)
  Step 7  -- V12_002.cs: BUILD_TAG bump line 44; add _photonMmioMirror field line 324
  Step 8  -- Lifecycle.cs: replace lines 201-204 with pool+ring+sideband+salt+mirror init + static assert
             *** FLAG-1 amendment: MMF name = "V12_FleetDispatch_" + pid + "_" + salt.ToString("X16") ***
  Step 9a -- SIMA.Lifecycle.cs lines 94-109: sideband-aware shutdown drain
  Step 9b -- Lifecycle.cs State.Terminated branch at line 369: append mirror Dispose + null-out
             (branch already exists; do NOT create a new one)
  Step 10 -- SIMA.Dispatch.cs lines 400-444: market producer -> sideband-first + shadow stamp + TryEnqueue
             + optional TryPublish via mirror
  Step 11 -- SIMA.Dispatch.cs lines 505-544: limit producer (same pattern)
  Step 12 -- SIMA.Fleet.cs lines 210-253: main drain -> new TryDequeue signature + shadow verify + sideband read
  Step 13 -- SIMA.Fleet.cs lines 178-207: abort drain -> sideband-aware (no shadow verify)
  Step 14 -- BUILD_TAG sync: V12_002.Constants.cs:12 ("Build 1111.002-v28.0"); nexus_a2a.json
             (mission/build_tag/phase/last_updated)
  Step 15 -- MANDATORY: powershell -File .\deploy-sync.ps1 then tell Director to press F5 in NT8

POST-EDIT (every src/ edit, per CLAUDE.md):
  1. powershell -File ".\deploy-sync.ps1"        -> ASCII Gate MUST PASS, hardlinks rehydrated
  2. Director: F5 in NinjaTrader                 -> zero CS0227, CS0103, CS0246
  3. Banner MUST show: "Build 1111.002-v28.0"
  4. Output MUST show:
       [PHOTON MMIO] mirror online: V12_FleetDispatch_<pid>_<salt>
     OR the graceful fallback message if MMF creation throws:
       [PHOTON MMIO] mirror unavailable (hot path unaffected): <reason>

TOUCH LIST (exact, no stray files):
  src/V12_002.UnsafeGate.cs          DELETE (Step 2)
  src/V12_002.MmfGate.cs             NEW preflight (Step 0), DELETED after gate passes
  src/V12_002.Photon.MmioMirror.cs   NEW (Step 6)
  src/V12_002.Photon.Pool.cs         MODIFY (Steps 3 + 4)
  src/V12_002.Photon.Ring.cs         OVERWRITE (Step 5)
  src/V12_002.cs                     MODIFY (Step 7: BUILD_TAG line 44, fields line 322-324)
  src/V12_002.Lifecycle.cs           MODIFY (Step 8 lines 201-204, Step 9b line 369 Terminated branch)
  src/V12_002.SIMA.Dispatch.cs       MODIFY (Step 10 lines 400-444, Step 11 lines 505-544)
  src/V12_002.SIMA.Fleet.cs          MODIFY (Step 12 lines 210-253, Step 13 lines 178-207)
  src/V12_002.SIMA.Lifecycle.cs      MODIFY (Step 9a lines 94-109)
  src/V12_002.Constants.cs           MODIFY (Step 14 line 12 Version bump)
  docs/brain/nexus_a2a.json          MODIFY (Step 14 metadata bump)

AUDIT GATES (Section 6 of plan -- all 10 MUST report PASS before handoff back to P3):
  1  F5 compile zero errors (CS0227/CS0103/CS0246 clean)
  2  ASCII gate across all modified .cs files (check_ascii.py or deploy-sync gate)
  3  Static assert (Marshal.SizeOf + OffsetOf) fires on layout drift (scratch-branch test)
  4  OnStartUp roundtrip selftest logs "[PHOTON SELFTEST] v28.0 ring + shadow + sideband OK"
  5  Scratch-branch corruption injection triggers "[PHOTON_SHADOW] INTEGRITY FAILURE"
  6  grep lock\s*\(\s*stateLock\s*\) src/ -> zero
  7  10-min paper trading: PrivateMemorySize64 delta < 5 MB
  8  Disable/enable lifecycle 3x: zero leaked handles (Process Explorer Section/FileMapping count returns to baseline)
  9  Two NT instances in parallel: each [PHOTON MMIO] mirror online with distinct names
  10 Forensics self-audit: grep \bunsafe\b, Unsafe\., byte\s*\*, fixed\s*\(, stackalloc, \bnint\b -- all zero in src/

HARD RULES:
  - ASCII only in all C# string literals. No emoji, no curly quotes, no em-dashes, no Unicode arrows.
  - No lock(stateLock) anywhere in executable code.
  - No silent shadow-mismatch fallback. On mismatch: log "[PHOTON_SHADOW] INTEGRITY FAILURE",
    rollback delta, clear dictionaries, release pool, clear sideband.
  - MMIO mirror failure is NON-fatal: log and proceed on the heap ring alone.
  - No agent impersonation: if Codex (or Jules standby) is unreachable, report failure and wait
    for Director intervention.
  - All path arguments that contain spaces MUST be quoted, e.g. "C:\Users\Mohammed Khalid\..."
    not raw C:\Users\Mohammed Khalid\... .
  - Every src/ edit is followed by deploy-sync.ps1 before F5. Skipping is a protocol violation.

CODEX SELF-AUDIT CHECKLIST (run after completing all 15 steps):
  [ ] grep -rn "lock\s*(" src/ -- zero hits
  [ ] grep -rn "\bunsafe\b" src/ -- zero hits
  [ ] grep -rn "byte\s*\*" src/ -- zero hits
  [ ] grep -rn "fixed\s*(" src/ -- zero hits
  [ ] grep -rn "stackalloc" src/ -- zero hits
  [ ] grep -rn "\bnint\b" src/ -- zero hits
  [ ] grep -rn "Unsafe\." src/ -- zero hits
  [ ] python check_ascii.py src/ OR deploy-sync ASCII gate PASS
  [ ] git diff --stat matches TOUCH LIST exactly (no stray files)
  [ ] powershell -File ".\deploy-sync.ps1" exits clean

ON COMPLETION, report back to P3:
  - All 10 audit gates PASS
  - Touch list exact (git status matches)
  - Banner confirmed by Director (screen or VLC capture)
  - nexus_a2a.json metadata bumped
  - (Optional) ns/op telemetry if Section 8 item 2 micro-benchmark was run
