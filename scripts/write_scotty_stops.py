import os, sys, math
sys.stdout.reconfigure(encoding='utf-8')

NT_STOP = r'C:\Users\Mohammed Khalid\Documents\NinjaTrader 8\templates\StopStrategy'
NT_ATM  = r'C:\Users\Mohammed Khalid\Documents\NinjaTrader 8\templates\AtmStrategy'

# ── SCOTTY "KEVINATOR" LOGIC ─────────────────────────────────────────────────
# Triggers: proportional to SL (50% / 83% / 100% of SL ticks)
# Trail-by stops: FIXED 12 / 8 / 2 ticks (Scotty's exact values, same for all SL)
# BE trigger: 4 ticks (T1 = 1pt), BE buffer: 0 (MES — room to move)
# Frequencies: 4 / 2 / 1 (Scotty's exact)
# T1 bracket: UNCHANGED (target=4tk, no stop strategy)

def scotty_steps(sl_pts):
    sl_tk = sl_pts * 4
    s1t = round(sl_tk * 24 / 48)   # 50% of SL
    s2t = round(sl_tk * 40 / 48)   # 83% of SL
    s3t = round(sl_tk * 48 / 48)   # 100% of SL
    return s1t, s2t, s3t           # stops are always 12, 8, 2

# ── QUANTITIES (from existing ATMs, verified) ────────────────────────────────
QTY = {
    400: {4:(10,5,5), 5:(8,4,4), 6:(7,3,3), 7:(6,3,2), 8:(5,3,2), 9:(4,2,2), 10:(4,2,2)},
    200: {4:(5,3,2),  5:(4,2,2), 6:(4,2,1), 7:(3,2,1), 8:(3,1,1), 9:(3,1,1), 10:(2,1,1)},
    100: {4:(3,1,1),  5:(2,1,1), 6:(2,1,1), 7:(1,1,1), 8:(1,1,1), 9:(1,1,1), 10:(1,1,1)},
}

# ── TARGETS (from existing ATMs, verified) ───────────────────────────────────
# T1=4tk always, T2=SL/2, T3=SL*0.75
def targets(sl_pts):
    sl_tk = sl_pts * 4
    return 4, sl_tk // 2, int(sl_tk * 0.75)

# ── STOP STRATEGY XML ────────────────────────────────────────────────────────
STOP_TMPL = '''<?xml version="1.0" encoding="utf-8"?>
<NinjaTrader>
  <StopStrategy xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
    <AutoBreakEvenPlus>0</AutoBreakEvenPlus>
    <AutoBreakEvenProfitTrigger>4</AutoBreakEvenProfitTrigger>
    <AutoTrailSteps>
      <AutoTrailStep><Frequency>4</Frequency><ProfitTrigger>{s1t}</ProfitTrigger><StopLoss>12</StopLoss></AutoTrailStep>
      <AutoTrailStep><Frequency>2</Frequency><ProfitTrigger>{s2t}</ProfitTrigger><StopLoss>8</StopLoss></AutoTrailStep>
      <AutoTrailStep><Frequency>1</Frequency><ProfitTrigger>{s3t}</ProfitTrigger><StopLoss>2</StopLoss></AutoTrailStep>
    </AutoTrailSteps>
    <IsSimStopEnabled>true</IsSimStopEnabled>
    <VolumeTrigger>1</VolumeTrigger>
    <Template>{name}</Template>
  </StopStrategy>
</NinjaTrader>'''

# ── ATM XML ───────────────────────────────────────────────────────────────────
ATM_HDR = '''<?xml version="1.0" encoding="utf-8"?>
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

# T1 bracket — no stop strategy, just fills at target
B1 = '''      <Bracket>
        <Quantity>{q}</Quantity>
        <StopLoss>{sl}</StopLoss>
        <Target>{t}</Target>
      </Bracket>
'''

# T2/T3 brackets — Scotty stop strategy embedded
BSOP = '''      <Bracket>
        <Quantity>{q}</Quantity>
        <StopLoss>{sl}</StopLoss>
        <StopStrategy>
          <AutoBreakEvenPlus>0</AutoBreakEvenPlus>
          <AutoBreakEvenProfitTrigger>4</AutoBreakEvenProfitTrigger>
          <AutoTrailSteps>
            <AutoTrailStep><Frequency>4</Frequency><ProfitTrigger>{s1t}</ProfitTrigger><StopLoss>12</StopLoss></AutoTrailStep>
            <AutoTrailStep><Frequency>2</Frequency><ProfitTrigger>{s2t}</ProfitTrigger><StopLoss>8</StopLoss></AutoTrailStep>
            <AutoTrailStep><Frequency>1</Frequency><ProfitTrigger>{s3t}</ProfitTrigger><StopLoss>2</StopLoss></AutoTrailStep>
          </AutoTrailSteps>
          <IsSimStopEnabled>true</IsSimStopEnabled>
          <VolumeTrigger>1</VolumeTrigger>
          <Template>{sop}</Template>
        </StopStrategy>
        <Target>{t}</Target>
      </Bracket>
'''

# ── WRITE STOP STRATEGY FILES ─────────────────────────────────────────────────
print('=== STOP STRATEGY FILES ===')
for sl_pts in range(4, 11):
    s1t, s2t, s3t = scotty_steps(sl_pts)
    name = f'Scotty-SL{sl_pts}'
    xml = STOP_TMPL.format(s1t=s1t, s2t=s2t, s3t=s3t, name=name)
    fpath = os.path.join(NT_STOP, f'{name}.xml')
    with open(fpath, 'w', encoding='utf-8') as f:
        f.write(xml)
    print(f'  OK  {name}:  BE=4+0  S1={s1t}->12x4  S2={s2t}->8x2  S3={s3t}->2x1')

# ── WRITE ATM FILES ───────────────────────────────────────────────────────────
print('\n=== ATM FILES ===')
for risk in [400, 200, 100]:
    for sl_pts in range(4, 11):
        sl_tk = sl_pts * 4
        q1, q2, q3 = QTY[risk][sl_pts]
        total = q1 + q2 + q3
        t1, t2, t3 = targets(sl_pts)
        s1t, s2t, s3t = scotty_steps(sl_pts)
        sop = f'Scotty-SL{sl_pts}'
        name = f'MES ${risk} SL{sl_pts}'

        brackets  = B1.format(q=q1, sl=sl_tk, t=t1)
        brackets += BSOP.format(q=q2, sl=sl_tk, t=t2, s1t=s1t, s2t=s2t, s3t=s3t, sop=sop)
        if q3 > 0:
            brackets += BSOP.format(q=q3, sl=sl_tk, t=t3, s1t=s1t, s2t=s2t, s3t=s3t, sop=sop)

        xml = ATM_HDR.format(name=name, brackets=brackets, total=total)
        fpath = os.path.join(NT_ATM, f'{name}.xml')
        with open(fpath, 'w', encoding='utf-8') as f:
            f.write(xml)
        print(f'  OK  {name:<22}  total={total:>2}  Q={q1}+{q2}+{q3}  SL={sl_tk}tk  T={t1}/{t2}/{t3}  S1={s1t}->12  S2={s2t}->8  S3={s3t}->2')

print(f'\nAll done.')
