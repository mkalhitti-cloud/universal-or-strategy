# PTT-REPAIRS-07-OBFUSCATION -- Deferred Backlog
# Created by: ptt-reviewer (Phase 5 Final Review)
# Date: 2026-08-10

---

## Block: PTT-REPAIRS-07-OBFUSCATION (Lane A)
Date: 2026-08-10

### Deferred Items

- NT8-runtime pre-existing skips in BwaveCycTaR6HelperTests: 5 tests with `[Fact(Skip = "NT8-runtime: CopyEngine.cctor requires NT8 host")]` were present before T3 started (confirmed by T1 and T2 verifiers showing 0 NT8-runtime skips in this class range, and T3 verifier noting "5 NT8-runtime pre-existing"). These represent a state entered outside the T1-T4 pipeline sequence. Investigation required -- Lane: B -- Priority: HIGH

- ExtractLegSuffix_ShouldExist TypeInit status unknown in NT8 host: This test passes in the non-NT8 xUnit environment (confirmed by T4 verification). Its behavior in a full NT8 host runtime has not been verified. If it is a masked TypeInit failure, it would be the 6th remaining TypeInit issue in BwaveCycTaR6HelperTests -- Lane: B -- Priority: MED

- 5 remaining Failed tests in full suite (global F=5 after all 4 tickets): All pre-existing, none caused by T1-T4. Classification and root cause unknown. May include B79 TypeInit (T_DW_B79_09_03), plus up to 4 others from other test classes. Require identification. -- Lane: C (diagnostic) -- Priority: MED

- Long-term obfuscation fix (ObfuscationAttribute on CopyEngine private seam methods): Mark CopyEngine private methods used as test reflection targets with `[System.Reflection.ObfuscationAttribute(Feature="rename", Exclude=true)]`. This requires production code changes and would re-enable all 146 currently-skipped obfuscation tests. Out of scope for Lane A (test-only fix). -- Lane: B -- Priority: LOW

- CopyEngine.cs pre-existing uncommitted changes: Working tree shows CopyEngine.cs modified (nullable annotation changes, return null -> return default). These predate this epic's T1-T4 sequence and are attributed to PTT-REPAIRS-07-NT8-BULK-SKIP or a related epic. The owning epic must commit or roll back these changes before next release to avoid orphaned modifications in the working tree. -- Lane: PTT-REPAIRS-07-NT8-BULK-SKIP (epic owner) -- Priority: HIGH
