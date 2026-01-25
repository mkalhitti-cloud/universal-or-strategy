using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Threading;

namespace V9_ExternalRemote
{
    // COM Message Filter to handle RPC_E_CALL_REJECTED when Excel is busy
    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("00000016-0000-0000-C000-000000000046")]
    public interface IMessageFilter
    {
        [PreserveSig]
        int HandleInComingCall(int dwCallType, IntPtr hTaskCaller, int dwTickCount, IntPtr lpInterfaceInfo);
        [PreserveSig]
        int RetryRejectedCall(IntPtr hTaskCallee, int dwTickCount, int dwRejectType);
        [PreserveSig]
        int MessagePending(IntPtr hTaskCallee, int dwTickCount, int dwPendingType);
    }

    public class RetryMessageFilter : IMessageFilter
    {
        [DllImport("ole32.dll")]
        private static extern int CoRegisterMessageFilter(IMessageFilter lpMessageFilter, out IMessageFilter lplpMessageFilter);

        private static IMessageFilter? _oldFilter;
        private static RetryMessageFilter? _instance;

        public static void Register()
        {
            _instance = new RetryMessageFilter();
            CoRegisterMessageFilter(_instance, out _oldFilter);
        }

        public static void Unregister()
        {
            if (_instance != null)
            {
                IMessageFilter oldFilter;
                CoRegisterMessageFilter(null!, out oldFilter);
                _instance = null;
            }
        }

        public int HandleInComingCall(int dwCallType, IntPtr hTaskCaller, int dwTickCount, IntPtr lpInterfaceInfo) => 0;
        public int RetryRejectedCall(IntPtr hTaskCallee, int dwTickCount, int dwRejectType)
        {
            // SERVERCALL_RETRYLATER = 2
            if (dwRejectType == 2)
                return 200; // Retry after 200ms
            return -1; // Cancel
        }
        public int MessagePending(IntPtr hTaskCallee, int dwTickCount, int dwPendingType) => 2; // PENDINGMSG_WAITNOPROCESS
    }

    /// <summary>
    /// Reads TOS indicator values from an Excel workbook that has live RTD formulas.
    /// Uses late binding (dynamic) to work with .NET 6 without COM references.
    /// </summary>
    public class ExcelRtdReader : IDisposable
    {
        // P/Invoke for GetActiveObject (not available in .NET Core)
        [DllImport("oleaut32.dll", PreserveSig = false)]
        private static extern void GetActiveObject([MarshalAs(UnmanagedType.LPStruct)] Guid rclsid, IntPtr pvReserved, [MarshalAs(UnmanagedType.IUnknown)] out object ppunk);

        private static readonly Guid ExcelClsid = new Guid("00024500-0000-0000-C000-000000000046");

        private dynamic? _excelApp;
        private dynamic? _workbook;
        private DispatcherTimer _pollTimer;
        private readonly string _workbookPath;
        private bool _isConnected;
        private readonly Action<string, string, double, double> _onDataCallback;
        private readonly Action<string> _logCallback;

        // Expected cell layout:
        // A1=MES, B1=EMA9, C1=EMA15
        // A2=MGC, B2=EMA9, C2=EMA15

        public ExcelRtdReader(string workbookPath, Action<string, string, double, double> onDataCallback, Action<string> logCallback)
        {
            _workbookPath = workbookPath;
            _onDataCallback = onDataCallback;
            _logCallback = logCallback;
            _pollTimer = new DispatcherTimer();
            _pollTimer.Interval = TimeSpan.FromMilliseconds(500);
            _pollTimer.Tick += PollExcel;
        }

        public bool Connect()
        {
            // Register COM message filter to handle Excel being busy with RTD
            RetryMessageFilter.Register();
            _logCallback("ExcelRtdReader: Registered COM message filter for Excel");

            // Retry loop for RPC_E_CALL_REJECTED errors
            for (int attempt = 1; attempt <= 5; attempt++)
            {
                try
                {
                    return ConnectInternal(attempt);
                }
                catch (System.Runtime.InteropServices.COMException comEx) when (comEx.HResult == unchecked((int)0x80010001))
                {
                    _logCallback($"ExcelRtdReader: Excel busy (attempt {attempt}/5), waiting...");
                    System.Threading.Thread.Sleep(1000);
                }
                catch (Exception ex)
                {
                    _logCallback($"ExcelRtdReader: Connection error: {ex.Message}");
                    return false;
                }
            }
            _logCallback("ExcelRtdReader: Failed after 5 attempts - Excel too busy");
            return false;
        }

        private bool ConnectInternal(int attempt)
        {
            _logCallback($"ExcelRtdReader: Attempting to connect to Excel (attempt {attempt})...");

            // Check if workbook exists
            if (!File.Exists(_workbookPath))
            {
                _logCallback($"ExcelRtdReader: Workbook not found at {_workbookPath}");
                _logCallback("ExcelRtdReader: Create TOS_RTD_Bridge.xlsx with RTD formulas - see instructions file.");
                return false;
            }

            // Try to get running Excel instance using P/Invoke
            Type? excelType = Type.GetTypeFromProgID("Excel.Application");
            if (excelType == null)
            {
                _logCallback("ExcelRtdReader: Excel not installed");
                return false;
            }

            try
            {
                GetActiveObject(ExcelClsid, IntPtr.Zero, out object excelObj);
                _excelApp = excelObj;
                _logCallback("ExcelRtdReader: Found running Excel instance");
            }
            catch
            {
                // No running instance, start new
                _excelApp = Activator.CreateInstance(excelType);
                if (_excelApp != null)
                {
                    _excelApp.Visible = true;
                }
                _logCallback("ExcelRtdReader: Started new Excel instance");
            }

            if (_excelApp == null)
            {
                _logCallback("ExcelRtdReader: Failed to get Excel application");
                return false;
            }

            // Check if workbook is already open - use filename matching, not full path
            string targetFileName = System.IO.Path.GetFileName(_workbookPath).ToLower();
            foreach (dynamic wb in _excelApp.Workbooks)
            {
                try
                {
                    string wbName = ((string)wb.Name).ToLower();
                    string wbFullName = (string)wb.FullName;
                    _logCallback($"ExcelRtdReader: Checking workbook: {wbName} ({wbFullName})");

                    // Match by filename (case-insensitive)
                    if (wbName == targetFileName || wbName.Contains("rtd_bridge") || wbName.Contains("tos"))
                    {
                        _workbook = wb;
                        _logCallback($"ExcelRtdReader: Found already-open workbook: {wbName}");
                        break;
                    }
                }
                catch (Exception ex)
                {
                    _logCallback($"ExcelRtdReader: Error checking workbook: {ex.Message}");
                }
            }

            // Open workbook if not already open
            if (_workbook == null)
            {
                _workbook = _excelApp.Workbooks.Open(_workbookPath);
                _logCallback("ExcelRtdReader: Opened workbook");
            }

            _isConnected = true;
            _pollTimer.Start();
            _logCallback("ExcelRtdReader: Connected and polling started");
            return true;
        }

        private int _pollCount = 0;
        private void PollExcel(object? sender, EventArgs e)
        {
            if (!_isConnected || _workbook == null) return;

            try
            {
                // Force recalculate to get fresh RTD values
                try
                {
                    _excelApp?.Calculate();
                }
                catch { }

                dynamic sheet = _workbook.Sheets[1];

                // Every 10 polls, log which workbook we're reading
                _pollCount++;
                if (_pollCount % 10 == 1)
                {
                    try
                    {
                        string wbName = _workbook.Name;
                        string wbPath = _workbook.FullName;
                        _logCallback($"ExcelRtdReader: Reading from '{wbName}' at '{wbPath}'");
                    }
                    catch { }
                }

                // Read MES data (Row 1)
                string mesSymbol = GetCellValue(sheet, 1, 1);
                double mesEma9 = GetCellDouble(sheet, 1, 2);
                double mesEma15 = GetCellDouble(sheet, 1, 3);

                // Debug: Log raw cell values periodically
                if (_pollCount % 10 == 1)
                {
                    _logCallback($"ExcelRtdReader: A1='{mesSymbol}', B1 raw={GetRawCellValue(sheet, 1, 2)}, C1 raw={GetRawCellValue(sheet, 1, 3)}");
                }

                // Read MGC data (Row 2)  
                string mgcSymbol = GetCellValue(sheet, 2, 1);
                double mgcEma9 = GetCellDouble(sheet, 2, 2);
                double mgcEma15 = GetCellDouble(sheet, 2, 3);

                // Callback with data
                if (!string.IsNullOrEmpty(mesSymbol) && mesEma9 > 0)
                    _onDataCallback("MES", "EMA", mesEma9, mesEma15);

                if (!string.IsNullOrEmpty(mgcSymbol) && mgcEma9 > 0)
                    _onDataCallback("MGC", "EMA", mgcEma9, mgcEma15);
            }
            catch (Exception ex)
            {
                _logCallback($"ExcelRtdReader: Poll error - {ex.Message}");
            }
        }

        private string GetCellValue(dynamic sheet, int row, int col)
        {
            try
            {
                dynamic cell = sheet.Cells[row, col];
                object? val = cell.Value;
                return val?.ToString() ?? "";
            }
            catch { return ""; }
        }

        private string GetRawCellValue(dynamic sheet, int row, int col)
        {
            try
            {
                dynamic cell = sheet.Cells[row, col];
                object? val = cell.Value2;
                string formula = "";
                try { formula = cell.Formula?.ToString() ?? ""; } catch { }
                return $"V2={val} F={formula}";
            }
            catch (Exception ex) { return $"ERR:{ex.Message}"; }
        }

        private double GetCellDouble(dynamic sheet, int row, int col)
        {
            try
            {
                dynamic cell = sheet.Cells[row, col];

                // Try Value2 first (more reliable for numbers), then Value
                object? val = null;
                try { val = cell.Value2; } catch { }
                if (val == null)
                    try { val = cell.Value; } catch { }

                if (val != null)
                {
                    // If it's already a number, return it directly
                    if (val is double dbl)
                        return dbl;
                    if (val is int i)
                        return i;
                    if (val is float f)
                        return f;

                    string valStr = val.ToString() ?? "";
                    if (double.TryParse(valStr, out double result))
                        return result;

                    // Log if it's not a number (e.g., "loading", "N/A", "#N/A")
                    if (!string.IsNullOrEmpty(valStr) && valStr != "0")
                        _logCallback($"ExcelRtdReader: Cell at [{row},{col}] is '{valStr}' (not a number)");
                }
            }
            catch (Exception ex)
            {
                _logCallback($"ExcelRtdReader: Error reading cell [{row},{col}] - {ex.Message}");
            }
            return 0;
        }

        public void Disconnect()
        {
            _pollTimer.Stop();
            _isConnected = false;
            _logCallback("ExcelRtdReader: Disconnected");
            // Don't close Excel - user may need it
        }

        public void Dispose()
        {
            Disconnect();
            if (_excelApp != null)
            {
                try { Marshal.ReleaseComObject(_excelApp); } catch { }
                _excelApp = null;
            }
        }
    }
}
