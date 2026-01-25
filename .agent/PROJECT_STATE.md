# V9 External Remote - RTD Debug Status

## Current State (Market Closed)
- **Direct TOS RTD**: Connects successfully, heartbeat works, but topicCount=0 (expected when market closed)
- **Excel RTD Bridge**: Connects to TOS_RTD_Bridge.xlsx, but RTD formulas show "loading" (expected when market closed)
- **Hub Connection**: Working (green LED)

## What's Been Implemented
1. Shotgun scan subscribing to CUSTOM1-12 to discover field mapping
2. Display shows "C#:value" format to reveal which CUSTOM numbers have EMA data
3. COM message filter to handle Excel busy states
4. Excel workbook detection by filename matching

## Excel RTD Formula Format (from debug log)
- B1: `=RTD("tos.rtd",,"CUSTOM4","/MES:XCME")`
- C1: `=RTD("tos.rtd",,"CUSTOM6","/MES:XCME")`

## To Test When Market Opens
1. Launch V9_Milestone_FINAL.exe
2. Check if values appear (should show "C4:xxxx.xx" style in EMA fields)
3. Once we see which CUSTOM numbers work, update code with correct mapping
4. Previous working mapping was: CUSTOM4=EMA9, CUSTOM6=EMA15 (may have changed when watchlist columns were renamed)

## Key Files
- V9_ExternalRemote/MainWindow.xaml.cs - Main WPF app
- V9_ExternalRemote/TosRtdClient.cs - Direct RTD connection
- V9_ExternalRemote/ExcelRtdReader.cs - Excel bridge fallback
- V9_ExternalRemote/TOS_RTD_Bridge.xlsx - Excel with RTD formulas

## Log Files
- v9_remote_log.txt - RTD connection/subscription logs
- v9_shotgun_hits.txt - Successful RTD data received
