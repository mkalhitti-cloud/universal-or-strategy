# Lane Contract: SIMA God Function Extraction (V12 Pilot)
# Target: ExecuteSmartDispatchEntry (CC=471)

## 1. Lane: DEV (Bob CLI / Crafter)
**Goal**: Partition the 471-line God Function into smaller, atomic units using the Python Extractor Protocol.

### Definition of Done (Artifacts Required):
- [ ] `src/Sima/ExecuteSmartDispatchEntry.cs`: The original entry point, reduced to a high-level dispatcher.
- [ ] `src/Sima/Extracts/Dispatch_OrderValidation.cs`: Surgical extraction of validation logic.
- [ ] `src/Sima/Extracts/Dispatch_AccountMarshalling.cs`: Surgical extraction of account-selection logic.
- [ ] `scripts/sima_dispatch_split.py`: The Python script used for the extraction (Manual copy-paste is BANNED).

### Constraints:
- **Lock-Free**: Zero `lock()` statements in the new files.
- **ASCII Only**: No Unicode in `Print()` statements.
- **Pattern**: Must use lock-free state transitions (Interlocked.Exchange) or the FSM/Actor Enqueue model. Blocking semaphores are BANNED.

---

## 2. Lane: REVIEW (Codex/Arena / Gate)
**Goal**: Adversarial audit of the DEV artifacts.

### Definition of Done (Evidence Required):
- [ ] **ASCII Gate**: `python check_ascii.py` output showing PASS.
- [ ] **Complexity Audit**: `complexity_audit_report.txt` showing the new files have CC <= 15.
- [ ] **Build Check**: `build_output.txt` showing 0 errors in NinjaScript compilation.
- [ ] **Lock Audit**: `grep -r "lock(" src/Sima/Extracts/` returns 0 results.

---

## 3. Lane: DONE (Adjudicator / Nexus)
**Goal**: Final promotion to `universal-or-strategy.sln`.

### Handoff:
- [ ] Update `docs/brain/nexus_a2a.json` with the new Build Tag.
- [ ] Run `powershell -File .\deploy-sync.ps1` to re-establish hard links.
