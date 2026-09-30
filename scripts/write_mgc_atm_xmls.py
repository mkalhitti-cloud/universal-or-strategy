import sys, math, os
sys.stdout.reconfigure(encoding='utf-8')

NT_PATH = r'C:\Users\Mohammed Khalid\Documents\NinjaTrader 8\templates\AtmStrategy'

# MGC contract: 10 ticks/pt, $1/tick, fee=$1.50/contract
# SOP trail steps in ticks (same names as MES SOPs)
SOP_TRAIL = {
    'SOP3':  [(12,2,2, 30,8), (16,2,2, 40,20), (20,1,1, 50,30)],   # not used directly
    'SOP35': None,
    'SOP4':  None,
    'SOP45': None,
    'SOP5':  None,
}
# For MGC, trail steps are proportionally scaled to tick distances
# MES SOP3: trigger@12tk stop@4tk, @16tk stop@10tk, @20tk stop@16tk  (T1=8tk)
# MGC SL4 T1=20tk → scale factor 20/8=2.5:  trigger@30tk stop@10tk, @40tk stop@25tk, @50tk stop@40tk
# General: triggers = T1*1.5, T1*2, T1*2.5   stops = T1*0.5, T1*1.25, T1*2
# This matches the SOP points reference from the spreadsheet

SOP_MAP = {4:'SOP3',5:'SOP3',6:'SOP3',7:'SOP35',8:'SOP4',9:'SOP45',10:'SOP5'}

QTY = {
    400: {4:10, 5:8, 6:6, 7:5, 8:5, 9:4, 10:4},
    200: {4:5,  5:4, 6:3, 7:3, 8:2, 9:2, 10:2},
}

XML_TEMPLATE = '''<?xml version="1.0" encoding="utf-8"?>
<NinjaTrader>
  <AtmStrategy xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
    <IsVisible>true</IsVisible>
    <calculate2>OnBarClose</calculate2>
    <AreLinesConfigurable>true</AreLinesConfigurable>
    <ArePlotsConfigurable>true</ArePlotsConfigurable>
    <BarsToLoad>0</BarsToLoad>
    <Calculate>OnBarClose</Calculate>
    <Displacement>0</Displacement>
    <DisplayInDataBox>true</DisplayInDataBox>
    <From>2099-12-01T00:00:00</From>
    <IsAutoScale>true</IsAutoScale>
    <Lines />
    <MaximumBarsLookBack>TwoHundredFiftySix</MaximumBarsLookBack>
    <Name>AtmStrategy</Name>
    <Panel>0</Panel>
    <Plots />
    <ScaleJustification>Right</ScaleJustification>
    <ShowTransparentPlotsInDataBox>false</ShowTransparentPlotsInDataBox>
    <To>1800-01-01T00:00:00</To>
    <IsDataSeriesRequired>false</IsDataSeriesRequired>
    <IsOverlay>false</IsOverlay>
    <SelectedValueSeries>0</SelectedValueSeries>
    <Gtd>1800-01-01T00:00:00</Gtd>
    <Template>{name}</Template>
    <TimeInForce>Gtc</TimeInForce>
    <BarsRequiredToTrade>0</BarsRequiredToTrade>
    <Category>Atm</Category>
    <ConnectionLossHandling>KeepRunning</ConnectionLossHandling>
    <DaysToLoad>1</DaysToLoad>
    <DefaultQuantity>1</DefaultQuantity>
    <DisconnectDelaySeconds>0</DisconnectDelaySeconds>
    <EntriesPerDirection>1</EntriesPerDirection>
    <EntryHandling>AllEntries</EntryHandling>
    <ExitOnSessionCloseSeconds>0</ExitOnSessionCloseSeconds>
    <IncludeCommission>false</IncludeCommission>
    <IsAggregated>false</IsAggregated>
    <IsExitOnSessionCloseStrategy>false</IsExitOnSessionCloseStrategy>
    <IsFillLimitOnTouch>false</IsFillLimitOnTouch>
    <IsOptimizeDataSeries>false</IsOptimizeDataSeries>
    <IsStableSession>false</IsStableSession>
    <IsTickReplay>false</IsTickReplay>
    <IsTradingHoursBreakLineVisible>false</IsTradingHoursBreakLineVisible>
    <IsWaitUntilFlat>false</IsWaitUntilFlat>
    <NumberRestartAttempts>0</NumberRestartAttempts>
    <OptimizationPeriod>10</OptimizationPeriod>
    <OrderFillResolution>High</OrderFillResolution>
    <OrderFillResolutionType>Tick</OrderFillResolutionType>
    <OrderFillResolutionValue>1</OrderFillResolutionValue>
    <RestartsWithinMinutes>0</RestartsWithinMinutes>
    <SetOrderQuantity>Strategy</SetOrderQuantity>
    <Slippage>0</Slippage>
    <StartBehavior>AdoptAccountPosition</StartBehavior>
    <StopTargetHandling>PerEntryExecution</StopTargetHandling>
    <SupportsOptimizationGraph>false</SupportsOptimizationGraph>
    <TestPeriod>28</TestPeriod>
    <TradingHoursSerializable />
    <Brackets>
{brackets}    </Brackets>
    <EntryQuantity>{total}</EntryQuantity>
    <CalculationMode>Ticks</CalculationMode>
    <ChaseLimit>1</ChaseLimit>
    <InitialTickSize>0</InitialTickSize>
    <IsChase>false</IsChase>
    <IsChaseIfTouched>true</IsChaseIfTouched>
    <IsTargetChase>false</IsTargetChase>
    <ReverseAtStop>false</ReverseAtStop>
    <ReverseAtTarget>false</ReverseAtTarget>
    <UseMitForProfit>false</UseMitForProfit>
    <UseStopLimitForStopLossOrders>false</UseStopLimitForStopLossOrders>
    <AtmSelector>508659aee0554887b3aaf1944107b792</AtmSelector>
    <OnBehalfOf />
    <ReverseAtStopStrategyId>-1</ReverseAtStopStrategyId>
    <ReverseAtTargetStrategyId>-1</ReverseAtTargetStrategyId>
    <ShadowStrategyStrategyId>-1</ShadowStrategyStrategyId>
    <ShadowTemplate />
  </AtmStrategy>
</NinjaTrader>'''

BRACKET1 = '''      <Bracket>
        <Quantity>{q}</Quantity>
        <StopLoss>{sl}</StopLoss>
        <Target>{t1}</Target>
      </Bracket>
'''

BRACKET_SOP = '''      <Bracket>
        <Quantity>{q}</Quantity>
        <StopLoss>{sl}</StopLoss>
        <StopStrategy>
          <AutoBreakEvenPlus>2</AutoBreakEvenPlus>
          <AutoBreakEvenProfitTrigger>{be}</AutoBreakEvenProfitTrigger>
          <AutoTrailSteps>
            <AutoTrailStep>
              <Frequency>2</Frequency>
              <ProfitTrigger>{pt1}</ProfitTrigger>
              <StopLoss>{sl1}</StopLoss>
            </AutoTrailStep>
            <AutoTrailStep>
              <Frequency>2</Frequency>
              <ProfitTrigger>{pt2}</ProfitTrigger>
              <StopLoss>{sl2}</StopLoss>
            </AutoTrailStep>
            <AutoTrailStep>
              <Frequency>1</Frequency>
              <ProfitTrigger>{pt3}</ProfitTrigger>
              <StopLoss>{sl3}</StopLoss>
            </AutoTrailStep>
          </AutoTrailSteps>
          <IsSimStopEnabled>false</IsSimStopEnabled>
          <VolumeTrigger>0</VolumeTrigger>
          <Template>{sop}</Template>
        </StopStrategy>
        <Target>{target}</Target>
      </Bracket>
'''

written = []
for risk_level in [400, 200]:
    for sl_pts in [4,5,6,7,8,9,10]:
        total = QTY[risk_level][sl_pts]
        sl_tk = sl_pts * 10          # 10 ticks/pt for MGC
        t1_tk = sl_tk // 2
        t2_tk = int(sl_tk * 0.75)   # floor
        t3_tk = sl_tk
        sop   = SOP_MAP[sl_pts]

        q1 = math.ceil(total / 2)
        q2 = math.ceil((total - q1) / 2)
        q3 = total - q1 - q2

        # Trail triggers (in ticks): T1*1.5, T1*2.0, T1*2.5
        # Trail stops:               T1*0.5, T1*1.25, T1*2.0
        pt1 = int(t1_tk * 1.5)
        sl1 = int(t1_tk * 0.5)
        pt2 = int(t1_tk * 2.0)
        sl2 = int(t1_tk * 1.25)
        pt3 = int(t1_tk * 2.5)
        sl3 = int(t1_tk * 2.0)

        name = f'MGC ${risk_level} SL{sl_pts}'

        # Build brackets
        b1 = BRACKET1.format(q=q1, sl=sl_tk, t1=t1_tk)

        b2 = BRACKET_SOP.format(
            q=q2, sl=sl_tk, be=t1_tk,
            pt1=pt1, sl1=sl1, pt2=pt2, sl2=sl2, pt3=pt3, sl3=sl3,
            sop=sop, target=t2_tk)

        brackets = b1 + b2

        # Only add bracket 3 if Q3 > 0
        if q3 > 0:
            b3 = BRACKET_SOP.format(
                q=q3, sl=sl_tk, be=t1_tk,
                pt1=pt1, sl1=sl1, pt2=pt2, sl2=sl2, pt3=pt3, sl3=sl3,
                sop=sop, target=t3_tk)
            brackets += b3

        xml = XML_TEMPLATE.format(name=name, brackets=brackets, total=total)
        fpath = os.path.join(NT_PATH, f'{name}.xml')
        with open(fpath, 'w', encoding='utf-8') as f:
            f.write(xml)
        written.append((name, q1, q2, q3, sl_tk, t1_tk, t2_tk, t3_tk, sop))
        print(f'  ✅ {name}  total={total} Q={q1}+{q2}+{q3}  SL={sl_tk}tk  T1={t1_tk}  T2={t2_tk}  T3={t3_tk}  {sop}')

print(f'\nTotal files written: {len(written)}')
