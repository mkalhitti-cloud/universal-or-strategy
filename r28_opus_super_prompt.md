# V12 ULTRA-INCEPTION: ROUND 28 KERNEL SUPER-PROMPT (CODE-ONLY / AGGRESSIVE)

**INSTRUCTIONS FOR USER:**

1. Open a fresh Claude/Opus session.
2. Paste the text below in its entirety.
3. This is stripped of all HTML/UI overhead to maximize Opus's reasoning on the C# kernel logic.

---

## MISSION START: [V12-PHASE-7-R28-SUPER-CODE]

**ROLE:** Principal Kernel Architect (Antigravity OS)
**PROTOCOL:** ULTRAPLAN + ULTRATHINK + RALPH WIGGUM ADVERSARIAL AUDIT
**WORKSPACE:** c:\WSGTA\universal-or-strategy

### 1. THE MANDATE

You are the V12 Lead Engineer. Your task is to implement the **Round 28 MmioSpscRing<T>** kernel primitive. We have bypassed the Arena sandbox to use your full reasoning capacity. We do NOT want a documentation page; we want the **production-grade, zero-alloc, lock-free C# kernel** in a single, compilable unit.

### 2. PRE-FLIGHT REQUISITES

Before generating any code, you MUST:

1.  **READ** the project identity: `.agent/skills/architect/SKILL.md`
2.  **READ** the current baseline: `docs/brain/nexus_a2a.json`
3.  **READ** our architecture DNA: `v12_dna.md` (via KI summary search)

### 3. THE KERNEL SPECIFICATION (MMIO-SPSC-RING)

- **Implementation:** Single-Producer Single-Consumer (SPSC) Ring Buffer.
- **Backing:** Memory-Mapped I/O (MMIO) with pure `unsafe` pointer arithmetic.
- **Build Tag:** `1111.002-v28.0`
- **Throughput Target:** < 14 ns/op.
- **Integrity:** XorShadow (ADR-016) byte-wise hash integrity (branch-free).

### 4. TECHNICAL CONSTRAINTS (HARD)

- **Environment:** .NET 9 / NinjaTrader 8 Sandbox (C# 7.3 compatible where necessary, but use .NET 9 optimized primitives like `Unsafe.ReadUnaligned`).
- **Memory:** `byte*` raw region. No managed arrays on the hot-path.
- **Zero-Alloc:** MUST allocate 0 bytes on the heap during Dequeue/Enqueue.
- **Lock-Free:** NO `lock`, NO `Monitor`, NO `SpinLock`. Use `Volatile.Read/Write` and manual memory barriers.
- **Structs:** `OrderSlot` and `FillSlot` (64 bytes each, `Pack=8`, `XorShadow` as the final field).

### 5. THE TEST HARNESS (CRITICAL)

Your output must include a `Program.Main` that executes an **8-test battery**:

1. Single Round-trip
2. Sequential Validity (10 items)
3. Corruption Detection (Tamper with slot memory in-place; `Validate` must FAIL)
4. Ring Full Behavior (Capacity 64)
5. Ring Empty Behavior
6. Generation Wrap-around (Fill 64, Drain 64, Refill 64)
7. Throughput Benchmark (10M iterations)
8. Multi-type Generic Test (Prove the kernel handles both `OrderSlot` and `FillSlot` generators).

### 6. EXECUTION PROTOCOL (ULTRAPLAN)

1. **UltraThink:** Analyze the `FleetDispatchSlot` managed-reference restriction. How will you implement the "Sideband" array to keep the MMIO ring purely blittable?
2. **Ralph Wiggum Audit:** Attack your cache-line isolation. Are the producer and consumer cursors perfectly separated by 64 bytes of padding to prevent false sharing?
3. **Output:** Provide the raw, compilable C# source inside a single markdown code block.

**GO.**
