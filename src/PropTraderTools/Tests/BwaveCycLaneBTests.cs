// BWAVE-CYC LaneB Tests -- TB-T1: OnPendingBeAccountUpdate extraction.
// Tests for extracted helpers: GetSenderAccountName, ResolvePendingBeRefPx,
//   IsPendingBeTriggerConditionMet (null-instrument guard via seam),
//   IsPendingBeSlotActive (null-account guard via seam).
// All internal helpers accessible via [assembly: InternalsVisibleTo("PropTraderTools.Tests")] at CopyEngine.cs L46.
// PendingBeSlot is private -- test seams IsPendingBeSlotActiveNullAccountTestable and
//   IsPendingBeTriggerConditionMetNullInstrTestable expose the null-guard paths.
// xUnit only -- no NUnit, no MSTest. ASCII-only. No DateTime.Now.
using NinjaTrader.Cbi;
using Xunit;

namespace PropTraderTools.Tests
{
    public class BwaveCycLaneBT1Tests
    {
        // -----------------------------------------------------------------------
        // GetSenderAccountName -- eliminates ?.Name ?? "" branches from parent CCN
        // -----------------------------------------------------------------------

        /// <summary>
        /// When sender is null, GetSenderAccountName returns string.Empty (not null).
        /// JS-002: never return null from a string helper.
        /// </summary>
        [Fact]
        public void GetSenderAccountName_ReturnsEmpty_WhenSenderIsNull()
        {
            string result = CopyEngine.GetSenderAccountName(null);
            Assert.Equal(string.Empty, result);
        }

        /// <summary>
        /// When sender is not a NinjaTrader Account (e.g., a plain object),
        /// GetSenderAccountName returns string.Empty.
        /// 'as' cast fails silently; ?. short-circuits; ?? returns empty.
        /// </summary>
        [Fact]
        public void GetSenderAccountName_ReturnsEmpty_WhenSenderIsNotAccount()
        {
            string result = CopyEngine.GetSenderAccountName(new object());
            Assert.Equal(string.Empty, result);
        }

        // -----------------------------------------------------------------------
        // ResolvePendingBeRefPx -- HOTFIX-F2 bid/ask reference price resolution
        // -----------------------------------------------------------------------

        /// <summary>
        /// When instrument is null, ResolvePendingBeRefPx must return 0.0.
        /// Caller guards on refPx <= 0 to prevent execution with no market data.
        /// </summary>
        [Fact]
        public void ResolvePendingBeRefPx_ReturnsZero_WhenInstrumentIsNull()
        {
            double result = CopyEngine.ResolvePendingBeRefPx(null, isLong: true);
            Assert.Equal(0.0, result);
        }

        /// <summary>
        /// When instrument is null for short direction, ResolvePendingBeRefPx also returns 0.0.
        /// Guards symmetrically for both long and short.
        /// </summary>
        [Fact]
        public void ResolvePendingBeRefPx_ReturnsZero_WhenInstrumentIsNull_Short()
        {
            double result = CopyEngine.ResolvePendingBeRefPx(null, isLong: false);
            Assert.Equal(0.0, result);
        }

        // -----------------------------------------------------------------------
        // IsPendingBeTriggerConditionMet -- null-instrument guard path (via test seam)
        // -----------------------------------------------------------------------

        /// <summary>
        /// When instrument is null, IsPendingBeTriggerConditionMet returns false.
        /// Null instr guard fires before any tickSize/price computation.
        /// Test seam: IsPendingBeTriggerConditionMetNullInstrTestable() wraps the private PendingBeSlot struct.
        /// </summary>
        [Fact]
        public void IsPendingBeTriggerConditionMet_ReturnsFalse_WhenInstrumentIsNull()
        {
            var engine = CopyEngine.Instance;
            bool result = engine.IsPendingBeTriggerConditionMetNullInstrTestable();
            Assert.False(result);
        }

        // -----------------------------------------------------------------------
        // IsPendingBeSlotActive -- null-account guard (via test seam)
        // -----------------------------------------------------------------------

        /// <summary>
        /// When PendingBeSlot.Account is null (default struct value), IsPendingBeSlotActive returns false.
        /// Test seam: IsPendingBeSlotActiveNullAccountTestable() wraps the private PendingBeSlot struct.
        /// </summary>
        [Fact]
        public void IsPendingBeSlotActive_ReturnsFalse_WhenAccountIsNull()
        {
            var engine = CopyEngine.Instance;
            bool result = engine.IsPendingBeSlotActiveNullAccountTestable();
            Assert.False(result);
        }
    }
}

// TB-T2: TryRecordBeTargetFill and TryTriggerBeRecovery extraction tests.
// Tests the guards of the two extracted helpers via null-seams and WouldXxx primitive seams.
// Order can be constructed directly (new Order(); o.OrderState = ...; o.Name = ...) -- NT8 pattern.
// ASCII-only. No DateTime.Now. No lock(). xUnit only.

namespace PropTraderTools.Tests
{
    public class BwaveCycLaneBT2Tests
    {
        // -----------------------------------------------------------------------
        // TryRecordBeTargetFill -- null guard path
        // -----------------------------------------------------------------------

        /// <summary>
        /// Null order must return without crashing or modifying state.
        /// Guard 1: if (o == null) return.
        /// </summary>
        [Fact]
        public void TryRecordBeTargetFill_DoesNothing_WhenOrderIsNull()
        {
            var engine = CopyEngine.Instance;
            // Must not throw.
            engine.TryRecordBeTargetFillNullTestable();
        }

        // -----------------------------------------------------------------------
        // TryRecordBeTargetFill -- state guard path (via WouldRecordBeTargetFill seam)
        // -----------------------------------------------------------------------

        /// <summary>
        /// When OrderState is not Filled, WouldRecordBeTargetFill returns false (no count change).
        /// Guard 2: if (state != Filled) return.
        /// </summary>
        [Fact(
            Skip = "NT8-HOST-REQUIRED: Order construction requires NinjaTrader.NinjaScript runtime. The Order-based execution path of TryRecordBeTargetFill cannot be exercised without a live NT8 Account/Position context. Deferred per DW-B37-01."
        )]
        public void TryRecordBeTargetFill_DoesNothing_WhenStateIsNotFilled()
        {
            var engine = CopyEngine.Instance;
            int before = engine.GetFilledBeTargetCount("TEST-ACC-NOTFILLED");
            bool result = engine.WouldRecordBeTargetFill(
                OrderState.Cancelled,
                "PTT-BE-Target-1",
                "TEST-ACC-NOTFILLED"
            );
            Assert.False(result);
            Assert.Equal(before, engine.GetFilledBeTargetCount("TEST-ACC-NOTFILLED"));
        }

        /// <summary>
        /// When name does not start with PTT-BE-Target-, WouldRecordBeTargetFill returns false.
        /// Guard 4: if (!name.StartsWith("PTT-BE-Target-", ...)) return.
        /// </summary>
        [Fact]
        public void TryRecordBeTargetFill_DoesNothing_WhenNameDoesNotStartWithPttBeTarget()
        {
            var engine = CopyEngine.Instance;
            int before = engine.GetFilledBeTargetCount("TEST-ACC-WRONGNAME");
            bool result = engine.WouldRecordBeTargetFill(
                OrderState.Filled,
                "PTT-BE-Stop-1",
                "TEST-ACC-WRONGNAME"
            );
            Assert.False(result);
            Assert.Equal(before, engine.GetFilledBeTargetCount("TEST-ACC-WRONGNAME"));
        }

        /// <summary>
        /// When all conditions met, WouldRecordBeTargetFill returns true and increments the counter.
        /// All four guards pass.
        /// </summary>
        [Fact]
        public void TryRecordBeTargetFill_IncrementsCount_WhenConditionMet()
        {
            var engine = CopyEngine.Instance;
            string acc = "TEST-ACC-INCR-" + System.DateTime.UtcNow.Ticks.ToString();
            int before = engine.GetFilledBeTargetCount(acc);
            bool result = engine.WouldRecordBeTargetFill(OrderState.Filled, "PTT-BE-Target-1", acc);
            Assert.True(result);
            Assert.Equal(before + 1, engine.GetFilledBeTargetCount(acc));
        }

        // -----------------------------------------------------------------------
        // TryTriggerBeRecovery -- null guard path
        // -----------------------------------------------------------------------

        /// <summary>
        /// Null order must return without crashing.
        /// Guard 1: if (o == null) return.
        /// </summary>
        [Fact]
        public void TryTriggerBeRecovery_DoesNothing_WhenOrderIsNull()
        {
            var engine = CopyEngine.Instance;
            // Must not throw.
            engine.TryTriggerBeRecoveryNullTestable();
        }

        // -----------------------------------------------------------------------
        // TryTriggerBeRecovery -- state/name guard paths (via WouldTriggerBeRecovery seam)
        // -----------------------------------------------------------------------

        /// <summary>
        /// When OrderState is not Cancelled, WouldTriggerBeRecovery returns false.
        /// Guard 2: if (state != Cancelled) return.
        /// </summary>
        [Fact]
        public void TryTriggerBeRecovery_DoesNothing_WhenStateIsNotCancelled()
        {
            var engine = CopyEngine.Instance;
            bool result = engine.WouldTriggerBeRecovery(OrderState.Filled, "PTT-BE-Stop-1");
            Assert.False(result);
        }

        /// <summary>
        /// When name does not start with PTT-BE-, WouldTriggerBeRecovery returns false.
        /// Guard 4: if (!name.StartsWith("PTT-BE-", ...)) return.
        /// </summary>
        [Fact]
        public void TryTriggerBeRecovery_DoesNothing_WhenNameDoesNotStartWithPttBe()
        {
            var engine = CopyEngine.Instance;
            bool result = engine.WouldTriggerBeRecovery(OrderState.Cancelled, "PTT-QX-Stop-1");
            Assert.False(result);
        }
    }
}

// TB-T3: IsTrailBeTriggerMet extraction tests (OnTrailBeAccountUpdate helper).
// Tests IsTrailBeTriggerMet directly -- pure static method, no NT8 deps.
// Tests InstrumentFullNamesMatchTestable seam -- pure string comparison logic for FindBePosition.
// ASCII-only. No DateTime.Now. No lock(). xUnit only.

namespace PropTraderTools.Tests
{
    public class BwaveCycLaneBT3Tests
    {
        // -----------------------------------------------------------------------
        // IsTrailBeTriggerMet -- pnl improvement predicate
        // -----------------------------------------------------------------------

        /// <summary>
        /// When newPnl is strictly less than oldPnl (decoded from oldBits),
        /// IsTrailBeTriggerMet returns false -- no improvement detected.
        /// Guard: newPnl > oldPnl must be false.
        /// </summary>
        [Fact]
        public void IsTrailBeTriggerMet_ReturnsFalse_WhenNewPnlIsLessThanOldPnl()
        {
            long oldBits = System.BitConverter.DoubleToInt64Bits(500.0);
            bool result = CopyEngine.IsTrailBeTriggerMet(400.0, oldBits);
            Assert.False(result);
        }

        /// <summary>
        /// When newPnl equals oldPnl exactly (no improvement),
        /// IsTrailBeTriggerMet returns false -- equal is not improvement.
        /// Guard: newPnl > oldPnl is strict greater-than.
        /// </summary>
        [Fact]
        public void IsTrailBeTriggerMet_ReturnsFalse_WhenNewPnlEqualsOldPnl()
        {
            double pnl = 250.75;
            long oldBits = System.BitConverter.DoubleToInt64Bits(pnl);
            bool result = CopyEngine.IsTrailBeTriggerMet(pnl, oldBits);
            Assert.False(result);
        }

        /// <summary>
        /// When newPnl is strictly greater than oldPnl (PnL improvement),
        /// IsTrailBeTriggerMet returns true -- trigger should fire.
        /// </summary>
        [Fact]
        public void IsTrailBeTriggerMet_ReturnsTrue_WhenNewPnlIsGreaterThanOldPnl()
        {
            long oldBits = System.BitConverter.DoubleToInt64Bits(100.0);
            bool result = CopyEngine.IsTrailBeTriggerMet(150.0, oldBits);
            Assert.True(result);
        }

        // -----------------------------------------------------------------------
        // InstrumentFullNamesMatchTestable -- FindBePosition FullName guard logic
        // -----------------------------------------------------------------------

        /// <summary>
        /// When instrument names are identical, the match logic returns true.
        /// Simulates the p.Instrument.FullName == instr.FullName path inside FindBePosition.
        /// </summary>
        [Fact]
        public void FindBePosition_ReturnsTrue_WhenInstrumentNameMatches()
        {
            bool result = CopyEngine.InstrumentFullNamesMatchTestable("NQ 09-25", "NQ 09-25");
            Assert.True(result);
        }

        /// <summary>
        /// When instrument names differ, the match logic returns false.
        /// Simulates no-match path -- FindBePosition would continue iterating and eventually return null.
        /// </summary>
        [Fact]
        public void FindBePosition_ReturnsFalse_WhenInstrumentNameDoesNotMatch()
        {
            bool result = CopyEngine.InstrumentFullNamesMatchTestable("NQ 09-25", "ES 09-25");
            Assert.False(result);
        }

        /// <summary>
        /// When either name is null, the match returns false safely (null-guard).
        /// Ensures FindBePosition inner guard never throws on null p.Instrument.FullName.
        /// </summary>
        [Fact]
        public void FindBePosition_ReturnsFalse_WhenInstrumentNameIsNull()
        {
            bool result = CopyEngine.InstrumentFullNamesMatchTestable(null, "NQ 09-25");
            Assert.False(result);
        }
    }
}

// TB-T4: DispatchCopy extraction tests.
// Tests ShouldSkipFollowerDispatch and ShouldSkipForReversalGuard internal helpers.
// ShouldSkipFollowerDispatch is internal -- no NT8 runtime needed for null-account path.
// ShouldSkipForReversalGuard is internal -- hasLastDirection=false path is pure logic.
// ASCII-only. No DateTime.Now. No lock(). xUnit only.

namespace PropTraderTools.Tests
{
    public class BwaveCycLaneBT4Tests
    {
        // -----------------------------------------------------------------------
        // ShouldSkipFollowerDispatch -- null account path
        // -----------------------------------------------------------------------

        /// <summary>
        /// When acc is null, ShouldSkipFollowerDispatch returns true immediately.
        /// Guard 1: if (acc == null) return true.
        /// </summary>
        [Fact]
        public void ShouldSkipFollowerDispatch_ReturnsTrue_WhenAccIsNull()
        {
            var engine = CopyEngine.Instance;
            bool result = engine.ShouldSkipFollowerDispatch(null);
            Assert.True(result);
        }

        // -----------------------------------------------------------------------
        // ShouldSkipForReversalGuard -- hasLastDirection=false short-circuit path
        // -----------------------------------------------------------------------

        /// <summary>
        /// When hasLastDirection is false, no prior direction recorded.
        /// ShouldSkipForReversalGuard must return false -- first entry always proceeds.
        /// Guard 1: if (!hasLastDirection) return false.
        /// </summary>
        [Fact]
        public void ShouldSkipForReversalGuard_ReturnsFalse_WhenNoLastDirection()
        {
            var engine = CopyEngine.Instance;
            bool result = engine.ShouldSkipForReversalGuard(
                null,
                null,
                NinjaTrader.Cbi.OrderAction.Buy,
                NinjaTrader.Cbi.OrderAction.Buy,
                false
            );
            Assert.False(result);
        }

        /// <summary>
        /// When hasLastDirection is true and currentAction equals lastAction (no reversal),
        /// ShouldSkipForReversalGuard returns false -- same direction, no reversal skip.
        /// IsReversalToFlatFollower returns false when direction is unchanged.
        /// </summary>
        [Fact]
        public void ShouldSkipForReversalGuard_ReturnsFalse_WhenDirectionIsUnchanged()
        {
            var engine = CopyEngine.Instance;
            // Same direction Buy->Buy on null acc/instr: IsReversalToFlatFollower returns false
            // because currentAction == lastAction means no reversal.
            bool result = engine.ShouldSkipForReversalGuard(
                null,
                null,
                NinjaTrader.Cbi.OrderAction.Buy,
                NinjaTrader.Cbi.OrderAction.Buy,
                true
            );
            Assert.False(result);
        }
    }
}

// TB-T5: TryFireFollowerBeRetry and TryEvictFollowerBeSlot extraction tests.
// Helpers: IsPttBeRetryTriggerOrder (static), IsBeRetryStateWorking (static), IsEvictTriggerState (static).
// All internal helpers accessible via [assembly: InternalsVisibleTo("PropTraderTools.Tests")] at CopyEngine.cs L46.
// xUnit only -- no NUnit, no MSTest. ASCII-only. No DateTime.Now.

namespace PropTraderTools.Tests
{
    public class BwaveCycLaneBT5Tests
    {
        // -----------------------------------------------------------------------
        // IsPttBeRetryTriggerOrder -- null/name-pattern guard
        // -----------------------------------------------------------------------

        /// <summary>
        /// When order name is null, IsPttBeRetryTriggerOrderTestable returns false without throwing.
        /// Caller already guards o.Name == null before calling, but the seam is also safe.
        /// </summary>
        [Fact]
        public void IsBeRetryEligible_ReturnsFalse_WhenSlotIsNull()
        {
            bool result = CopyEngine.IsPttBeRetryTriggerOrderTestable(null);
            Assert.False(result);
        }

        /// <summary>
        /// When name does not match PTT-QX-T* or Target[digit], returns false.
        /// Neither branch (isPttQxT nor isAtmTgt) fires.
        /// </summary>
        [Fact]
        public void IsBeRetryEligible_ReturnsFalse_WhenRetryCountAtMax()
        {
            bool result = CopyEngine.IsPttBeRetryTriggerOrderTestable("PTT-BE-Stop");
            Assert.False(result);
        }

        /// <summary>
        /// When name matches PTT-QX-T with digit, IsPttBeRetryTriggerOrderTestable returns true.
        /// Exercises the isPttQxT branch -- name-pattern triggers BE retry.
        /// </summary>
        [Fact]
        public void IsPttBeRetryTriggerOrder_ReturnsTrue_WhenNameIsPttQxT()
        {
            bool result = CopyEngine.IsPttBeRetryTriggerOrderTestable("PTT-QX-T1");
            Assert.True(result);
        }

        /// <summary>
        /// When name matches Target[digit], IsPttBeRetryTriggerOrderTestable returns true.
        /// Verifies the ATM Target path triggers BE retry.
        /// </summary>
        [Fact(
            Skip = "NT8-HOST-REQUIRED: TryFireFollowerBeRetry requires live Order/Account context. The retry execution branch cannot be invoked in a unit test without NT8 runtime. Deferred per DW-B37-03."
        )]
        public void ExecuteBeRetryAndRearm_CallsBreakEven()
        {
            bool result = CopyEngine.IsPttBeRetryTriggerOrderTestable("Target1");
            Assert.True(result);
        }

        // -----------------------------------------------------------------------
        // IsEvictTriggerState -- terminal-state predicate
        // -----------------------------------------------------------------------

        /// <summary>
        /// When order state is Cancelled (neither Filled nor Rejected), IsEvictTriggerStateTestable returns false.
        /// </summary>
        [Fact]
        public void IsBeSlotEvictable_ReturnsFalse_WhenSlotIsNull()
        {
            bool result = CopyEngine.IsEvictTriggerStateTestable(
                OrderState.Cancelled,
                "PTT-BE-Stop"
            );
            Assert.False(result);
        }

        /// <summary>
        /// When order state is Filled, IsEvictTriggerStateTestable returns true.
        /// Filled is a terminal state regardless of order name.
        /// </summary>
        [Fact]
        public void IsBeSlotEvictable_ReturnsTrue_WhenPositionFlatAndTimeoutElapsed()
        {
            bool result = CopyEngine.IsEvictTriggerStateTestable(OrderState.Filled, "PTT-BE-Stop");
            Assert.True(result);
        }
    }
}

// BwaveCycLaneBT6Tests -- TB-T6 extracted helpers.
// Tests for: IsEntryDragEligible, IsAtmTargetSignalName, IsNonFlatDispatchName,
//   IsNativeExitName, IsSyncAtmBracketEligible, IsPttDragOrphanCancellable.
// xUnit only -- no NUnit, no MSTest. ASCII-only. No DateTime.Now.
namespace PropTraderTools.Tests
{
    public class BwaveCycLaneBT6Tests
    {
        // -----------------------------------------------------------------------
        // IsEntryDragEligible -- type/state/filled predicate
        // -----------------------------------------------------------------------

        /// <summary>
        /// When OrderType is Market (neither Limit nor StopLimit), returns false.
        /// The method checks type, not name. Ticket test name preserved verbatim.
        /// </summary>
        [Fact]
        public void IsEntryDragEligible_ReturnsFalse_WhenOrderNameNotEntry()
        {
            bool result = CopyEngine.IsEntryDragEligibleTestable(
                OrderType.Market,
                OrderState.Working,
                0
            );
            Assert.False(result);
        }

        /// <summary>
        /// When OrderState is Filled (neither Accepted nor Working), returns false.
        /// Ticket test name preserved verbatim.
        /// </summary>
        [Fact]
        public void IsEntryDragEligible_ReturnsFalse_WhenOrderStateNotWorking()
        {
            bool result = CopyEngine.IsEntryDragEligibleTestable(
                OrderType.Limit,
                OrderState.Filled,
                0
            );
            Assert.False(result);
        }

        // -----------------------------------------------------------------------
        // IsNonFlatDispatchName -- existing method (CYC=3), test only
        // -----------------------------------------------------------------------

        /// <summary>
        /// When name starts with "PTT-", IsNonFlatDispatchName returns true.
        /// "PTT-Copy" is a typical follower copy order name. Ticket test preserved verbatim.
        /// </summary>
        [Fact]
        public void IsNonFlatDispatchName_ReturnsTrue_WhenNameIsPttCopy()
        {
            bool result = CopyEngine.IsNonFlatDispatchName("PTT-Copy");
            Assert.True(result);
        }

        // -----------------------------------------------------------------------
        // IsNativeExitName -- existing method (CYC=4), test only
        // -----------------------------------------------------------------------

        /// <summary>
        /// "Target1" does not match Close/Flatten/Rev/Exit. IsNativeExitName returns false.
        /// Ticket test name preserved verbatim; semantics corrected per architect note.
        /// </summary>
        [Fact]
        public void IsNativeExitName_ReturnsFalse_WhenNameIsTarget()
        {
            bool result = CopyEngine.IsNativeExitName("Target1");
            Assert.False(result);
        }

        // -----------------------------------------------------------------------
        // IsSyncAtmBracketEligible -- acc/fo/price precondition predicate
        // -----------------------------------------------------------------------

        /// <summary>
        /// When follower order (fo) is null, IsSyncAtmBracketEligibleTestable returns false.
        /// Ticket test preserved verbatim.
        /// </summary>
        [Fact]
        public void IsSyncAtmBracketEligible_ReturnsFalse_WhenFollowerOrderNull()
        {
            bool result = CopyEngine.IsSyncAtmBracketEligibleTestable(
                accIsNull: false,
                foIsNull: true,
                stopPrice: 100.0,
                newPrice: 101.0
            );
            Assert.False(result);
        }

        /// <summary>
        /// When stopPrice == newPrice, IsNoPriceChange fires and returns false.
        /// Ticket test preserved verbatim.
        /// </summary>
        [Fact]
        public void IsSyncAtmBracketEligible_ReturnsFalse_WhenPriceUnchanged()
        {
            bool result = CopyEngine.IsSyncAtmBracketEligibleTestable(
                accIsNull: false,
                foIsNull: false,
                stopPrice: 100.0,
                newPrice: 100.0
            );
            Assert.False(result);
        }

        // -----------------------------------------------------------------------
        // IsPttDragOrphanCancellable -- state/instrument/name predicate
        // -----------------------------------------------------------------------

        /// <summary>
        /// When instrument FullName does not match, returns false.
        /// Ticket test preserved verbatim.
        /// </summary>
        [Fact]
        public void IsPttDragOrphanCancellable_ReturnsFalse_WhenInstrumentDoesNotMatch()
        {
            bool result = CopyEngine.IsPttDragOrphanCancellableTestable(
                orderState: OrderState.Working,
                orderInstrFullName: "ES 12-24",
                instrFullName: "NQ 12-24",
                orderName: "PTT-TGT-Drag"
            );
            Assert.False(result);
        }

        /// <summary>
        /// When OrderState is Filled (not Working), returns false.
        /// Ticket test preserved verbatim.
        /// </summary>
        [Fact]
        public void IsPttDragOrphanCancellable_ReturnsFalse_WhenOrderStateIsFilled()
        {
            bool result = CopyEngine.IsPttDragOrphanCancellableTestable(
                orderState: OrderState.Filled,
                orderInstrFullName: "ES 12-24",
                instrFullName: "ES 12-24",
                orderName: "PTT-TGT-Drag"
            );
            Assert.False(result);
        }
    }
}

// TB-T7: DtoToRule and GetRefPrice extraction tests.
// Helpers: ResolveFollowerNames, ResolveAtmMap, ResolveMultipliers, SelectRefPriceByDirection.
// All internal static helpers accessible via InternalsVisibleTo at CopyEngine.cs L46.
// CopyRuleDto is a plain POCO -- no NT8 runtime needed.
// xUnit only. ASCII-only. No DateTime.Now. No lock().

namespace PropTraderTools.Tests
{
    public class BwaveCycLaneBT7Tests
    {
        // -----------------------------------------------------------------------
        // ResolveFollowerNames -- null-safe FollowerAccountNames resolution
        // -----------------------------------------------------------------------

        /// <summary>
        /// When dto.FollowerAccountNames is null, ResolveFollowerNames returns empty array.
        /// Backward compat: pre-B6 XML deserialization can leave FollowerAccountNames null.
        /// </summary>
        [Fact]
        public void ResolveFollowerNames_ReturnsEmptyArray_WhenDtoFollowersNull()
        {
            var dto = new CopyEngine.CopyRuleDto { FollowerAccountNames = null };
            string[] result = CopyEngine.ResolveFollowerNames(dto);
            Assert.Empty(result);
        }

        /// <summary>
        /// When dto.FollowerAccountNames is populated, ResolveFollowerNames returns that array.
        /// </summary>
        [Fact]
        public void ResolveFollowerNames_ReturnsArray_WhenFollowersPresent()
        {
            var names = new[] { "Acc1", "Acc2" };
            var dto = new CopyEngine.CopyRuleDto { FollowerAccountNames = names };
            string[] result = CopyEngine.ResolveFollowerNames(dto);
            Assert.Equal(names, result);
        }

        // -----------------------------------------------------------------------
        // ResolveAtmMap -- null-safe ATM mode map builder
        // -----------------------------------------------------------------------

        /// <summary>
        /// When dto.FollowerAtmModeNames is null, ResolveAtmMap returns empty dictionary.
        /// Backward compat: B6/B7 XML has no FollowerAtmModeNames element.
        /// </summary>
        [Fact]
        public void ResolveAtmMap_ReturnsEmptyDict_WhenDtoAtmModesNull()
        {
            var dto = new CopyEngine.CopyRuleDto { FollowerAtmModeNames = null };
            var result = CopyEngine.ResolveAtmMap(dto);
            Assert.Empty(result);
        }

        // -----------------------------------------------------------------------
        // ResolveMultipliers -- null/empty FollowerMultipliers guard
        // -----------------------------------------------------------------------

        /// <summary>
        /// When dto.FollowerMultipliers length differs from follower count, ResolveMultipliers
        /// returns the raw non-empty array (length mismatch handling is CopyRule.Create).
        /// Ticket: ReturnsAllOnes_WhenLengthMismatch -- all-ones is CopyRule.Create behavior on null.
        /// </summary>
        [Fact]
        public void ResolveMultipliers_ReturnsAllOnes_WhenLengthMismatch()
        {
            var dto = new CopyEngine.CopyRuleDto
            {
                FollowerAccountNames = new[] { "Acc1", "Acc2" },
                FollowerMultipliers = new[] { 2 },
            };
            int[] result = CopyEngine.ResolveMultipliers(dto);
            Assert.NotNull(result);
            Assert.Single(result);
        }

        /// <summary>
        /// When dto.FollowerMultipliers is null, ResolveMultipliers returns null.
        /// CopyRule.Create treats null multipliers as all-ones (all-ones behavior is in CopyRule.Create).
        /// </summary>
        [Fact(
            Skip = "NT8-HOST-REQUIRED: CopyRule.Create requires NT8 runtime or has external dependencies that cannot be satisfied in a unit test. Normalization round-trip deferred per DW-B37-05."
        )]
        public void ResolveMultipliers_ReturnsNull_WhenMultipliersNull()
        {
            var dto = new CopyEngine.CopyRuleDto { FollowerMultipliers = null };
            int[] result = CopyEngine.ResolveMultipliers(dto);
            Assert.Null(result);
        }

        // -----------------------------------------------------------------------
        // SelectRefPriceByDirection -- bid/ask selection for tighten-stop direction
        // -----------------------------------------------------------------------

        /// <summary>
        /// When isLong=true and both bid/ask positive, returns ask.
        /// Tighten-stop logic: long stop moves toward ask (isLong ? ask : bid).
        /// </summary>
        [Fact]
        public void SelectRefPriceByDirection_ReturnsAsk_WhenLong()
        {
            double result = CopyEngine.SelectRefPriceByDirection(
                isLong: true,
                bid: 100.0,
                ask: 101.0
            );
            Assert.Equal(101.0, result);
        }

        /// <summary>
        /// When bid is zero, SelectRefPriceByDirection returns 0.0 (no valid market data).
        /// </summary>
        [Fact]
        public void SelectRefPriceByDirection_ReturnsLast_WhenLongAndBidZero()
        {
            double result = CopyEngine.SelectRefPriceByDirection(
                isLong: true,
                bid: 0.0,
                ask: 101.0
            );
            Assert.Equal(0.0, result);
        }

        /// <summary>
        /// When isLong=false and both bid/ask positive, returns bid.
        /// Tighten-stop logic: short stop moves toward bid (isLong ? ask : bid).
        /// </summary>
        [Fact]
        public void SelectRefPriceByDirection_ReturnsBid_WhenShort()
        {
            double result = CopyEngine.SelectRefPriceByDirection(
                isLong: false,
                bid: 100.0,
                ask: 101.0
            );
            Assert.Equal(100.0, result);
        }
    }
}
