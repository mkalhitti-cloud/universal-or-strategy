import sys, math, os
sys.stdout.reconfigure(encoding='utf-8')

NT_ATM = r'C:\Users\Mohammed Khalid\Documents\NinjaTrader 8\templates\AtmStrategy'

# MGC $100 tier: qty = floor(100 / (sl_pts * 10 * 1))
# minimum 2 contracts (1+1+0) for a 2-bracket split
QTY_100 = {4:2, 5:2, 6:1, 7:1, 8:1, 9:1, 10:1}
# Override: keep minimum 2 so we have at least Q1+Q2
QTY_100 = {4:2, 5:2, 6:2, 7:2, 8:2, 9:2, 10:2}

ATM_XML = (
    '<?xml version="1.0" encoding="utf-8"?>\n'
    '<NinjaTrader>\n'
    '  <AtmStrategy xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">\n'
    '    <IsVisible>true</IsVisible>\n'
    '    <calculate2>OnBarClose</calculate2>\n'
    '    <AreLinesConfigurable>true</AreLinesConfigurable>\n'
    '    <ArePlotsConfigurable>true</ArePlotsConfigurable>\n'
    '    <BarsToLoad>0</BarsToLoad>\n'
    '    <Calculate>OnBarClose</Calculate>\n'
    '    <Displacement>0</Displacement>\n'
    '    <DisplayInDataBox>true</DisplayInDataBox>\n'
    '    <From>2099-12-01T00:00:00</From>\n'
    '    <IsAutoScale>true</IsAutoScale>\n'
    '    <Lines />\n'
    '    <MaximumBarsLookBack>TwoHundredFiftySix</MaximumBarsLookBack>\n'
    '    <Name>AtmStrategy</Name>\n'
    '    <Panel>0</Panel>\n'
    '    <Plots />\n'
    '    <ScaleJustification>Right</ScaleJustification>\n'
    '    <ShowTransparentPlotsInDataBox>false</ShowTransparentPlotsInDataBox>\n'
    '    <To>1800-01-01T00:00:00</To>\n'
    '    <IsDataSeriesRequired>false</IsDataSeriesRequired>\n'
    '    <IsOverlay>false</IsOverlay>\n'
    '    <SelectedValueSeries>0</SelectedValueSeries>\n'
    '    <Gtd>1800-01-01T00:00:00</Gtd>\n'
    '    <Template>{name}</Template>\n'
    '    <TimeInForce>Gtc</TimeInForce>\n'
    '    <BarsRequiredToTrade>0</BarsRequiredToTrade>\n'
    '    <Category>Atm</Category>\n'
    '    <ConnectionLossHandling>KeepRunning</ConnectionLossHandling>\n'
    '    <DaysToLoad>1</DaysToLoad>\n'
    '    <DefaultQuantity>1</DefaultQuantity>\n'
    '    <DisconnectDelaySeconds>0</DisconnectDelaySeconds>\n'
    '    <EntriesPerDirection>1</EntriesPerDirection>\n'
    '    <EntryHandling>AllEntries</EntryHandling>\n'
    '    <ExitOnSessionCloseSeconds>0</ExitOnSessionCloseSeconds>\n'
    '    <IncludeCommission>false</IncludeCommission>\n'
    '    <IsAggregated>false</IsAggregated>\n'
    '    <IsExitOnSessionCloseStrategy>false</IsExitOnSessionCloseStrategy>\n'
    '    <IsFillLimitOnTouch>false</IsFillLimitOnTouch>\n'
    '    <IsOptimizeDataSeries>false</IsOptimizeDataSeries>\n'
    '    <IsStableSession>false</IsStableSession>\n'
    '    <IsTickReplay>false</IsTickReplay>\n'
    '    <IsTradingHoursBreakLineVisible>false</IsTradingHoursBreakLineVisible>\n'
    '    <IsWaitUntilFlat>false</IsWaitUntilFlat>\n'
    '    <NumberRestartAttempts>0</NumberRestartAttempts>\n'
    '    <OptimizationPeriod>10</OptimizationPeriod>\n'
    '    <OrderFillResolution>High</OrderFillResolution>\n'
    '    <OrderFillResolutionType>Tick</OrderFillResolutionType>\n'
    '    <OrderFillResolutionValue>1</OrderFillResolutionValue>\n'
    '    <RestartsWithinMinutes>0</RestartsWithinMinutes>\n'
    '    <SetOrderQuantity>Strategy</SetOrderQuantity>\n'
    '    <Slippage>0</Slippage>\n'
    '    <StartBehavior>AdoptAccountPosition</StartBehavior>\n'
    '    <StopTargetHandling>PerEntryExecution</StopTargetHandling>\n'
    '    <SupportsOptimizationGraph>false</SupportsOptimizationGraph>\n'
    '    <TestPeriod>28</TestPeriod>\n'
    '    <TradingHoursSerializable />\n'
    '    <Brackets>\n'
    '{brackets}'
    '    </Brackets>\n'
    '    <EntryQuantity>{total}</EntryQuantity>\n'
    '    <CalculationMode>Ticks</CalculationMode>\n'
    '    <ChaseLimit>1</ChaseLimit>\n'
    '    <InitialTickSize>0</InitialTickSize>\n'
    '    <IsChase>false</IsChase>\n'
    '    <IsChaseIfTouched>true</IsChaseIfTouched>\n'
    '    <IsTargetChase>false</IsTargetChase>\n'
    '    <ReverseAtStop>false</ReverseAtStop>\n'
    '    <ReverseAtTarget>false</ReverseAtTarget>\n'
    '    <UseMitForProfit>false</UseMitForProfit>\n'
    '    <UseStopLimitForStopLossOrders>false</UseStopLimitForStopLossOrders>\n'
    '    <AtmSelector>508659aee0554887b3aaf1944107b792</AtmSelector>\n'
    '    <OnBehalfOf />\n'
    '    <ReverseAtStopStrategyId>-1</ReverseAtStopStrategyId>\n'
    '    <ReverseAtTargetStrategyId>-1</ReverseAtTargetStrategyId>\n'
    '    <ShadowStrategyStrategyId>-1</ShadowStrategyStrategyId>\n'
    '    <ShadowTemplate />\n'
    '  </AtmStrategy>\n'
    '</NinjaTrader>'
)

B1 = (
    '      <Bracket>\n'
    '        <Quantity>{q}</Quantity>\n'
    '        <StopLoss>{sl}</StopLoss>\n'
    '        <Target>{t}</Target>\n'
    '      </Bracket>\n'
)

BSOP = (
    '      <Bracket>\n'
    '        <Quantity>{q}</Quantity>\n'
    '        <StopLoss>{sl}</StopLoss>\n'
    '        <StopStrategy>\n'
    '          <AutoBreakEvenPlus>2</AutoBreakEvenPlus>\n'
    '          <AutoBreakEvenProfitTrigger>10</AutoBreakEvenProfitTrigger>\n'
    '          <AutoTrailSteps>\n'
    '            <AutoTrailStep><Frequency>2</Frequency><ProfitTrigger>20</ProfitTrigger><StopLoss>5</StopLoss></AutoTrailStep>\n'
    '            <AutoTrailStep><Frequency>2</Frequency><ProfitTrigger>30</ProfitTrigger><StopLoss>5</StopLoss></AutoTrailStep>\n'
    '            <AutoTrailStep><Frequency>1</Frequency><ProfitTrigger>40</ProfitTrigger><StopLoss>2</StopLoss></AutoTrailStep>\n'
    '          </AutoTrailSteps>\n'
    '          <IsSimStopEnabled>true</IsSimStopEnabled>\n'
    '          <VolumeTrigger>1</VolumeTrigger>\n'
    '          <Template>MGC Scotty SOP</Template>\n'
    '        </StopStrategy>\n'
    '        <Target>{t}</Target>\n'
    '      </Bracket>\n'
)

print('=== Writing MGC $100 ATM XMLs (all SL4-SL10) with Scotty SOP ===')
for sl in [4, 5, 6, 7, 8, 9, 10]:
    total = QTY_100[sl]
    sl_tk = sl * 10
    t1_tk = 10                    # fixed 1pt
    t2_tk = sl_tk // 2           # 50% SL
    q1 = 1
    q2 = total - q1              # 1 for 2-contract; 0 if somehow 1
    name = f'MGC $100 SL{sl}'

    b1 = B1.format(q=q1, sl=sl_tk, t=t1_tk)
    if q2 > 0:
        b2 = BSOP.format(q=q2, sl=sl_tk, t=t2_tk)
        brackets = b1 + b2
    else:
        brackets = b1

    xml = ATM_XML.format(name=name, brackets=brackets, total=total)
    fpath = os.path.join(NT_ATM, name + '.xml')
    with open(fpath, 'w', encoding='utf-8') as f:
        f.write(xml)
    print(f'  OK  {name}  Q={q1}+{q2}  SL={sl_tk}tk  T1={t1_tk}tk  T2={t2_tk}tk  -> MGC Scotty SOP')

print('\nDone.')
