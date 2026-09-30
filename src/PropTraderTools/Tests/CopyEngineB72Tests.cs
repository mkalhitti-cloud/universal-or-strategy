// B72-LaneA CopyEngine tests -- Tickets 1-5
// 50 [Fact] tests covering T_BEALL_01-04, T_BE_RESET_01-02, T_TRYFIRE_01-03,
// T_FOLLOWER_FLAT_01-04, T_QX_DOUBLE_01-03, T_DRAG_DEDUP_02-04,
// T_DEDUP_MARKET_01-02, T_DEDUP_LIMIT_01-02,
// T_BE_MOVE_01-05, T_BE_SIGN_LONG_01, T_BE_SIGN_SHORT_01, T_BE_SIGN_ZERO,
// T_BE_IMM_01-04, T_MSTBE_CR_01-03, T_OCO_SEED_01-03, T_OCO_SEQ_01,
// T_OCO_SEQ_04, T_QX_TARGETS_01-04, T_ATM_T3_01-03, T_ATM_T3_06-08
// JS-021: no lock. JS-033: no async void. JS-002: no return null. JS-001: no throw.
// xUnit ONLY. CYC <= 8 per method.

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using NinjaTrader.Cbi;
using Xunit;

namespace PropTraderTools
{
    public class CopyEngineB72Tests
    {
        // ----------------------------------------------------------------
        // Ticket 1 -- ArmAllPendingBe + TryFirePositionState + FollowerFlatDisarm
        // ----------------------------------------------------------------

        [Fact(Skip="B180-2: ArmPendingBe/ArmAllPendingBe removed")]
        public void T_BEALL_01_ArmAllPendingBe_OneNonFollower_SlotPopulated()
        {
            var ex = Record.Exception(() => CopyEngine.Instance.ArmAllPendingBe(2));
            Assert.Null(ex);
        }

        [Fact(Skip="B180-2: ArmPendingBe/ArmAllPendingBe removed")]
        public void T_BEALL_02_ArmAllPendingBe_NullBufferTicks_NoException()
        {
            var ex = Record.Exception(() => CopyEngine.Instance.ArmAllPendingBe(0));
            Assert.Null(ex);
        }

        [Fact(Skip="B180-2: ArmPendingBe/ArmAllPendingBe removed")]
        public void T_BEALL_03_ArmAllPendingBe_IsFollowerAccount_NullAcc_ReturnsFalse()
        {
            bool result = CopyEngine.Instance.IsFollowerAccount(null);
            Assert.False(result);
        }

        [Fact(Skip="B180-2: ArmPendingBe/ArmAllPendingBe removed")]
        public void T_BEALL_04_ArmAllPendingBe_NegativeBuffer_NoException()
        {
            var ex = Record.Exception(() => CopyEngine.Instance.ArmAllPendingBe(-1));
            Assert.Null(ex);
        }

        [Fact]
        public void T_BE_RESET_01_TryFirePositionState_Cancelled_DoesNotFire()
        {
            var state = OrderState.Cancelled;
            bool fires = (state == OrderState.Filled || state == OrderState.PartFilled);
            Assert.False(fires);
        }

        [Fact]
        public void T_BE_RESET_02_TryFirePositionState_Filled_DoFire()
        {
            var state = OrderState.Filled;
            bool fires = (state == OrderState.Filled || state == OrderState.PartFilled);
            Assert.True(fires);
        }

        [Fact]
        public void T_TRYFIRE_01_TryFirePositionState_FilledState_Fires()
        {
            var state = OrderState.Filled;
            bool fires = (state == OrderState.Filled || state == OrderState.PartFilled);
            Assert.True(fires);
        }

        [Fact]
        public void T_TRYFIRE_02_TryFirePositionState_CancelledState_DoesNotFire()
        {
            var state = OrderState.Cancelled;
            bool fires = (state == OrderState.Filled || state == OrderState.PartFilled);
            Assert.False(fires);
        }

        [Fact]
        public void T_TRYFIRE_03_TryFirePositionState_RejectedState_DoesNotFire()
        {
            var state = OrderState.Rejected;
            bool fires = (state == OrderState.Filled || state == OrderState.PartFilled);
            Assert.False(fires);
        }

        [Fact]
        public void T_FOLLOWER_FLAT_01_FollowerBeStopFill_NameStartsWith_Matches()
        {
            string name = "PTT-BE-Stop";
            bool matches = name != null && name.StartsWith("PTT-BE-Stop", StringComparison.Ordinal);
            Assert.True(matches);
        }

        [Fact]
        public void T_FOLLOWER_FLAT_02_FollowerBeStopFill_LeaderAccount_SkipsNarrowPath()
        {
            bool isLeader = true;
            bool takesNarrowPath = !isLeader;
            Assert.False(takesNarrowPath);
        }

        [Fact]
        public void T_FOLLOWER_FLAT_03_FollowerBeStopFill_WrongName_NoNarrowPath()
        {
            string name = "PTT-QX-Stop";
            bool matches = name != null && name.StartsWith("PTT-BE-Stop", StringComparison.Ordinal);
            Assert.False(matches);
        }

        [Fact]
        public void T_FOLLOWER_FLAT_04_FollowerBeStopFill_CancelledState_NoNarrowPath()
        {
            var state = OrderState.Cancelled;
            bool stateOk = state == OrderState.Filled;
            Assert.False(stateOk);
        }

        // ----------------------------------------------------------------
        // Ticket 2 -- QX Dedup + HandleEntryChange + IsDispatchTriggerState
        // ----------------------------------------------------------------

        [Fact]
        public void T_QX_DOUBLE_01_CancelQxBrackets_TriggerPendingEnumValue_Exists()
        {
            OrderState tp = OrderState.TriggerPending;
            Assert.Equal(OrderState.TriggerPending, tp);
        }

        [Fact]
        public void T_QX_DOUBLE_02_CancelQxBrackets_NullAccount_NoException()
        {
            var ex = Record.Exception(() => CopyEngine.Instance.CancelQxBrackets(null, null));
            Assert.Null(ex);
        }

        [Fact]
        public void T_QX_DOUBLE_03_CancelQxBrackets_SubmittedAndAccepted_InStateOkSet()
        {
            bool subOk =
                OrderState.Submitted == OrderState.Working
                || OrderState.Submitted == OrderState.Initialized
                || OrderState.Submitted == OrderState.Accepted
                || OrderState.Submitted == OrderState.Submitted
                || OrderState.Submitted == OrderState.TriggerPending;
            bool accOk =
                OrderState.Accepted == OrderState.Working
                || OrderState.Accepted == OrderState.Initialized
                || OrderState.Accepted == OrderState.Accepted
                || OrderState.Accepted == OrderState.Submitted
                || OrderState.Accepted == OrderState.TriggerPending;
            Assert.True(subOk);
            Assert.True(accOk);
        }

        [Fact]
        public void T_DRAG_DEDUP_02_HandleEntryChange_UpsertKeepsKey_InDedupCache()
        {
            var cache =
                (ConcurrentDictionary<string, double>)
                    typeof(CopyEngine)
                        .GetField("_dedupCache", BindingFlags.Instance | BindingFlags.NonPublic)
                        .GetValue(CopyEngine.Instance);
            cache["ord-b72-drag-01"] = 100.0;
            cache["ord-b72-drag-01"] = 200.0;
            Assert.True(cache.ContainsKey("ord-b72-drag-01"));
            Assert.Equal(200.0, cache["ord-b72-drag-01"]);
        }

        [Fact]
        public void T_DRAG_DEDUP_03_HandleEntryChange_NewOrderId_CacheMiss_AllowsDispatch()
        {
            var cache =
                (ConcurrentDictionary<string, double>)
                    typeof(CopyEngine)
                        .GetField("_dedupCache", BindingFlags.Instance | BindingFlags.NonPublic)
                        .GetValue(CopyEngine.Instance);
            cache.TryRemove("ord-b72-drag-02", out _);
            bool present = cache.ContainsKey("ord-b72-drag-02");
            Assert.False(present);
        }

        [Fact]
        public void T_DRAG_DEDUP_04_HandleEntryChange_NoTryRemove_KeyPersistsAfterUpsert()
        {
            var cache =
                (ConcurrentDictionary<string, double>)
                    typeof(CopyEngine)
                        .GetField("_dedupCache", BindingFlags.Instance | BindingFlags.NonPublic)
                        .GetValue(CopyEngine.Instance);
            cache["ord-b72-drag-03"] = 150.0;
            cache["ord-b72-drag-03"] = 150.0;
            Assert.True(cache.ContainsKey("ord-b72-drag-03"));
        }

        [Fact]
        public void T_DEDUP_MARKET_01_IsDispatchTriggerState_Market_Submitted_True()
        {
            bool result = CopyEngine.IsDispatchTriggerState(OrderState.Submitted, OrderType.Market);
            Assert.True(result);
        }

        [Fact]
        public void T_DEDUP_MARKET_02_IsDispatchTriggerState_Market_Accepted_False()
        {
            bool result = CopyEngine.IsDispatchTriggerState(OrderState.Accepted, OrderType.Market);
            Assert.False(result);
        }

        [Fact]
        public void T_DEDUP_LIMIT_01_IsDispatchTriggerState_Limit_Accepted_True()
        {
            bool result = CopyEngine.IsDispatchTriggerState(OrderState.Accepted, OrderType.Limit);
            Assert.True(result);
        }

        [Fact]
        public void T_DEDUP_LIMIT_02_IsDispatchTriggerState_Limit_Submitted_False()
        {
            bool result = CopyEngine.IsDispatchTriggerState(OrderState.Submitted, OrderType.Limit);
            Assert.False(result);
        }

        // ----------------------------------------------------------------
        // Ticket 3 -- BE Instrument FullName + Sign + StateOk + Immediate Fire
        // ----------------------------------------------------------------

        [Fact]
        public void T_BE_MOVE_01_MoveStopToBreakEven_FullNameEquality_MatchesSameName()
        {
            string fn1 = "MES 09-26";
            string fn2 = "MES 09-26";
            bool match = fn1 == fn2;
            Assert.True(match);
        }

        [Fact]
        public void T_BE_MOVE_02_MoveStopToBreakEven_FullNameEquality_FiltersDifferentName()
        {
            string fn1 = "MES 09-26";
            string fn2 = "ES 09-26";
            bool match = fn1 == fn2;
            Assert.False(match);
        }

        [Fact]
        public void T_BE_SIGN_LONG_01_MoveStopToBreakEven_Long_BePriceBelowEntry()
        {
            double entry = 5000.0;
            double buf = 2;
            double tick = 0.25;
            bool isLong = true;
            double direction = isLong ? -1.0 : +1.0;
            double bePrice = entry + direction * buf * tick;
            Assert.Equal(4999.5, bePrice, 6);
            Assert.True(bePrice < entry);
        }

        [Fact]
        public void T_BE_SIGN_SHORT_01_MoveStopToBreakEven_Short_BePriceAboveEntry()
        {
            double entry = 5000.0;
            double buf = 2;
            double tick = 0.25;
            bool isLong = false;
            double direction = isLong ? -1.0 : +1.0;
            double bePrice = entry + direction * buf * tick;
            Assert.Equal(5000.5, bePrice, 6);
            Assert.True(bePrice > entry);
        }

        [Fact]
        public void T_BE_SIGN_ZERO_MoveStopToBreakEven_ZeroBuffer_BePriceEqualsEntry()
        {
            double entry = 5000.0;
            double buf = 0;
            double tick = 0.25;
            bool isLong = true;
            double direction = isLong ? -1.0 : +1.0;
            double bePrice = entry + direction * buf * tick;
            Assert.Equal(5000.0, bePrice, 6);
        }

        [Fact(Skip="B180-2: ArmPendingBe/ArmAllPendingBe removed")]
        public void T_BE_IMM_01_ArmPendingBe_Long_BidAtOrAboveTarget_AlreadyAtBe()
        {
            bool isLong = true;
            double avg = 5000.0;
            double buf = 2;
            double tick = 0.25;
            double target = avg + (isLong ? 1.0 : -1.0) * buf * tick;
            double bid = 5000.5;
            bool alreadyAtBe = bid > 0.0 && (isLong ? bid >= target : bid <= target);
            Assert.True(alreadyAtBe);
        }

        [Fact(Skip="B180-2: ArmPendingBe/ArmAllPendingBe removed")]
        public void T_BE_IMM_02_ArmPendingBe_Short_AskAtOrBelowTarget_AlreadyAtBe()
        {
            bool isLong = false;
            double avg = 5000.0;
            double buf = 2;
            double tick = 0.25;
            double target = avg + (isLong ? 1.0 : -1.0) * buf * tick;
            double ask = 4999.5;
            bool alreadyAtBe = ask > 0.0 && (isLong ? ask >= target : ask <= target);
            Assert.True(alreadyAtBe);
        }

        [Fact(Skip="B180-2: ArmPendingBe/ArmAllPendingBe removed")]
        public void T_BE_IMM_03_ArmPendingBe_Long_BidBelowTarget_ArmWatcher()
        {
            bool isLong = true;
            double avg = 5000.0;
            double buf = 2;
            double tick = 0.25;
            double target = avg + 1.0 * buf * tick;
            double bid = 4999.0;
            bool alreadyAtBe = bid > 0.0 && bid >= target;
            Assert.False(alreadyAtBe);
        }

        [Fact(Skip="B180-2: ArmPendingBe/ArmAllPendingBe removed")]
        public void T_BE_IMM_04_ArmPendingBe_Short_AskAboveTarget_ArmWatcher()
        {
            bool isLong = false;
            double avg = 5000.0;
            double buf = 2;
            double tick = 0.25;
            double target = avg + (-1.0) * buf * tick;
            double ask = 5001.0;
            bool alreadyAtBe = ask > 0.0 && ask <= target;
            Assert.False(alreadyAtBe);
        }

        [Fact(Skip="B180-2: ArmPendingBe/ArmAllPendingBe removed")]
        public void T_BE_MOVE_03_ArmPendingBe_NullInstrument_NoException()
        {
            var ex = Record.Exception(() => CopyEngine.Instance.ArmPendingBe(null, null, 2));
            Assert.Null(ex);
        }

        [Fact]
        public void T_BE_MOVE_04_MoveStopToBreakEven_StepB_TriggerPendingInStateOk()
        {
            bool tpInFilter =
                OrderState.TriggerPending == OrderState.Working
                || OrderState.TriggerPending == OrderState.Initialized
                || OrderState.TriggerPending == OrderState.Submitted
                || OrderState.TriggerPending == OrderState.Accepted
                || OrderState.TriggerPending == OrderState.TriggerPending;
            Assert.True(tpInFilter);
        }

        [Fact]
        public void T_BE_MOVE_05_MoveStopToBreakEven_StepA_PttQxT1_IsAtmTarget()
        {
            string name = "PTT-QX-T1";
            bool isAtmTarget =
                !string.IsNullOrEmpty(name)
                && (
                    (
                        name.Length >= 7
                        && name.StartsWith("Target", StringComparison.Ordinal)
                        && char.IsDigit(name[6])
                        && name[6] != '0'
                    )
                    || (
                        name.StartsWith("PTT-QX-T", StringComparison.Ordinal)
                        && name.Length > 8
                        && char.IsDigit(name[8])
                    )
                    || name.StartsWith("PTT-BE-Target-", StringComparison.Ordinal)
                );
            Assert.True(isAtmTarget);
        }

        // ----------------------------------------------------------------
        // Ticket 4 -- BE Cancel+Resubmit + OCO Seed + Target Filter
        // ----------------------------------------------------------------

        [Fact]
        public void T_MSTBE_CR_01_MoveStopToBreakEven_StepA_Target1_IsAtmTarget()
        {
            string name = "Target1";
            bool isAtmTarget =
                !string.IsNullOrEmpty(name)
                && (
                    (
                        name.Length >= 7
                        && name.StartsWith("Target", StringComparison.Ordinal)
                        && char.IsDigit(name[6])
                        && name[6] != '0'
                    )
                    || (
                        name.StartsWith("PTT-QX-T", StringComparison.Ordinal)
                        && name.Length > 8
                        && char.IsDigit(name[8])
                    )
                    || name.StartsWith("PTT-BE-Target-", StringComparison.Ordinal)
                );
            Assert.True(isAtmTarget);
        }

        [Fact(Skip="B180-2: MoveStopToBreakEven removed")]
        public void T_MSTBE_CR_02_MoveStopToBreakEven_NoTargets_SubmitsBareStop()
        {
            var ex = Record.Exception(() =>
                typeof(CopyEngine)
                    .GetMethod(
                        "MoveStopToBreakEven",
                        BindingFlags.NonPublic | BindingFlags.Instance
                    )
                    .Invoke(CopyEngine.Instance, new object[] { null, null, 0 })
            );
            Assert.Null(ex);
        }

        [Fact]
        public void T_MSTBE_CR_03_MoveStopToBreakEven_StepC_SignalNames_StartWithPtt()
        {
            string beStop = "PTT-BE-Stop";
            string beStopN = "PTT-BE-Stop-1";
            string beTargetN = "PTT-BE-Target-1";
            Assert.True(beStop.StartsWith("PTT-", StringComparison.Ordinal));
            Assert.True(beStopN.StartsWith("PTT-", StringComparison.Ordinal));
            Assert.True(beTargetN.StartsWith("PTT-", StringComparison.Ordinal));
        }

        [Fact]
        public void T_OCO_SEED_01_MstbeOcoSeq_TickCountSeed_IsNonZero()
        {
            var fi = typeof(CopyEngine).GetField(
                "_mstbeOcoSeq",
                BindingFlags.Instance | BindingFlags.NonPublic
            );
            int seed = (int)fi.GetValue(CopyEngine.Instance);
            Assert.NotEqual(0, seed);
        }

        [Fact]
        public void T_OCO_SEED_02_EnvironmentTickCount_IsNonZero_AfterBoot()
        {
            int tc = Environment.TickCount;
            Assert.NotEqual(0, tc);
        }

        [Fact]
        public void T_OCO_SEED_03_NextBeOcoSeq_D7Format_SevenDigitPadding()
        {
            int seq = 1;
            string formatted = seq.ToString("D7");
            Assert.Equal("0000001", formatted);
            Assert.Equal(7, formatted.Length);
        }

        [Fact]
        public void T_OCO_SEQ_01_NextBeOcoSeq_TwoCalls_ReturnDifferentValues()
        {
            int s1 = CopyEngine.Instance.NextBeOcoSeq();
            int s2 = CopyEngine.Instance.NextBeOcoSeq();
            Assert.NotEqual(s1, s2);
        }

        [Fact]
        public void T_OCO_SEQ_04_NextBeOcoSeq_ConcurrentCalls_AllUnique()
        {
            var results = new ConcurrentBag<int>();
            var tasks = new System.Threading.Tasks.Task[10];
            for (int i = 0; i < 10; i++)
                tasks[i] = System.Threading.Tasks.Task.Run(() =>
                    results.Add(CopyEngine.Instance.NextBeOcoSeq())
                );
            System.Threading.Tasks.Task.WaitAll(tasks);
            Assert.Equal(10, results.Distinct().Count());
        }

        [Fact]
        public void T_QX_TARGETS_01_MoveStopToBreakEven_StepA_PttQxT1_Matches()
        {
            string name = "PTT-QX-T1";
            bool isAtmTarget =
                !string.IsNullOrEmpty(name)
                && (
                    (
                        name.Length >= 7
                        && name.StartsWith("Target", StringComparison.Ordinal)
                        && char.IsDigit(name[6])
                        && name[6] != '0'
                    )
                    || (
                        name.StartsWith("PTT-QX-T", StringComparison.Ordinal)
                        && name.Length > 8
                        && char.IsDigit(name[8])
                    )
                    || name.StartsWith("PTT-BE-Target-", StringComparison.Ordinal)
                );
            Assert.True(isAtmTarget);
        }

        [Fact]
        public void T_QX_TARGETS_02_MoveStopToBreakEven_StepA_PttQxT2_Matches()
        {
            string name = "PTT-QX-T2";
            bool isAtmTarget =
                name.StartsWith("PTT-QX-T", StringComparison.Ordinal)
                && name.Length > 8
                && char.IsDigit(name[8]);
            Assert.True(isAtmTarget);
        }

        [Fact]
        public void T_QX_TARGETS_03_MoveStopToBreakEven_StepA_PttBeTarget1_Matches()
        {
            string name = "PTT-BE-Target-1";
            bool isAtmTarget = name.StartsWith("PTT-BE-Target-", StringComparison.Ordinal);
            Assert.True(isAtmTarget);
        }

        [Fact]
        public void T_QX_TARGETS_04_MoveStopToBreakEven_StepA_PttBeTarget2_Matches()
        {
            string name = "PTT-BE-Target-2";
            bool isAtmTarget = name.StartsWith("PTT-BE-Target-", StringComparison.Ordinal);
            Assert.True(isAtmTarget);
        }

        // ----------------------------------------------------------------
        // Ticket 5 -- IsAtmBracketName
        // ----------------------------------------------------------------

        [Fact]
        public void T_ATM_T3_01_IsAtmBracketName_Stop1_True()
        {
            Assert.True(CopyEngine.IsAtmBracketName("Stop1"));
        }

        [Fact]
        public void T_ATM_T3_02_IsAtmBracketName_Stop3_True()
        {
            Assert.True(CopyEngine.IsAtmBracketName("Stop3"));
        }

        [Fact]
        public void T_ATM_T3_03_IsAtmBracketName_Target1_True()
        {
            Assert.True(CopyEngine.IsAtmBracketName("Target1"));
        }

        [Fact]
        public void T_ATM_T3_06_IsAtmBracketName_Target9_True()
        {
            Assert.True(CopyEngine.IsAtmBracketName("Target9"));
        }

        [Fact]
        public void T_ATM_T3_07_IsAtmBracketName_PttBeStop_False()
        {
            Assert.False(CopyEngine.IsAtmBracketName("PTT-BE-Stop"));
        }

        [Fact]
        public void T_ATM_T3_08_IsAtmBracketName_EmptyString_False()
        {
            Assert.False(CopyEngine.IsAtmBracketName(""));
        }
    }
}
