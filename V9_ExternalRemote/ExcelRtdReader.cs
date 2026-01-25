using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Threading;

namespace V9_ExternalRemote
{
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
            try
            {
                _logCallback("ExcelRtdReader: Attempting to connect to Excel...");

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

                // Check if workbook is already open
                foreach (dynamic wb in _excelApp.Workbooks)
                {
                    try
                    {
                        if (((string)wb.FullName).Equals(_workbookPath, StringComparison.OrdinalIgnoreCase))
                        {
                            _workbook = wb;
                            _logCallback("ExcelRtdReader: Found already-open workbook");
                            break;
                        }
                    }
                    catch { }
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
            catch (Exception ex)
            {
                _logCallback($"ExcelRtdReader: Connection failed - {ex.Message}");
                return false;
            }
        }

        private void PollExcel(object? sender, EventArgs e)
        {
            if (!_isConnected || _workbook == null) return;

            try
            {
                dynamic sheet = _workbook.Sheets[1];

                // Read MES data (Row 1)
                string mesSymbol = GetCellValue(sheet, 1, 1);
                double mesEma9 = GetCellDouble(sheet, 1, 2);
                double mesEma15 = GetCellDouble(sheet, 1, 3);

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

        private double GetCellDouble(dynamic sheet, int row, int col)
        {
            try
            {
                dynamic cell = sheet.Cells[row, col];
                object? val = cell.Value;
                if (val != null)
                {
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
