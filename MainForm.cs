using Aurora.Protocol;
using System;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AuroraMonbus
{
    public partial class MainForm : Form
    {
        private AuroraClient? _client;
        private CancellationTokenSource? _cts;
        private AppConfig _config = new();

        public MainForm()
        {
            InitializeComponent();
            btnDisconnect.Enabled = false;
            chkShowRaw.CheckedChanged += chkShowRaw_CheckedChanged;
            btnClear.Click += btnClear_Click;
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            _config = AppConfig.Load();
            Debug.WriteLine($"[Config] Loaded: Port={_config.SerialPort}, Baud={_config.BaudRate}, Addr={_config.Address}, Poll={_config.PollingInterval}ms");
        }

        // ---------------- Connect / Disconnect ----------------

        private async void btnConnect_Click(object sender, EventArgs e)
        {
            try
            {
                _client = new AuroraClient(_config.SerialPort, _config.BaudRate, _config.Address)
                {
                    DiagnosticsEnabled = chkShowRaw.Checked
                };

                await _client.ConnectAsync();

                btnConnect.Enabled = false;
                btnDisconnect.Enabled = true;

                lblStatus.Text = $"Connected ({_config.SerialPort})";
                lblStatus.ForeColor = System.Drawing.Color.Green;

                _cts = new CancellationTokenSource();
                _ = PollInverterAsync(_cts.Token);
            }
            catch (Exception ex)
            {
                lblStatus.Text = $"Connect failed: {ex.Message}";
                lblStatus.ForeColor = System.Drawing.Color.Red;
            }
        }

        private void btnDisconnect_Click(object sender, EventArgs e)
        {
            try
            {
                _cts?.Cancel();
                _cts?.Dispose();
                _cts = null;

                _client?.Dispose();
                _client = null;

                btnConnect.Enabled = true;
                btnDisconnect.Enabled = false;

                lblStatus.Text = "Disconnected";
                lblStatus.ForeColor = System.Drawing.Color.Red;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Disconnect error: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        // ---------------- Button & Checkbox Handlers ----------------

        // Clear output
        private void btnClear_Click(object sender, EventArgs e)
        {
            txtOutput.Clear();
            Debug.WriteLine("[UI] Output cleared.");
        }

        private async void btnSysInfo_Click(object sender, EventArgs e)
        {
            if (_client == null || !_client.IsConnected)
            {
                MessageBox.Show("Not connected to an inverter.", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            btnSysInfo.Enabled = false; // prevent double-clicks
            try
            {
                try
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

                    // 🟢 Step 1: Read the main formatted info
                    string info = await _client.ReadExtendedSystemInfoAsync(cts.Token);

                    // 🟢 Step 2: Get raw debug frames for analysis
                    var rx32 = await _client.DebugProbeAsync(0x32, cts.Token);  // State Info
                    var rx33 = await _client.DebugProbeAsync(0x33, cts.Token);  // System Info

                    // 🟢 Step 3: Display formatted information
                    MessageBox.Show(info, "Inverter System Information",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);

                    // Optional: write to debug for clarity
                    System.Diagnostics.Debug.WriteLine($"[SysInfo Probe] 0x32 -> {BitConverter.ToString(rx32)}");
                    System.Diagnostics.Debug.WriteLine($"[SysInfo Probe] 0x33 -> {BitConverter.ToString(rx33)}");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error reading system info:\r\n{ex.Message}", "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            finally
            {
                btnSysInfo.Enabled = true; // always re-enable after completion
            }            
        }

        // Toggle diagnostics
        private void chkShowRaw_CheckedChanged(object? sender, EventArgs e)
        {
            if (_client == null) return;
            _client.DiagnosticsEnabled = chkShowRaw.Checked;
            Debug.WriteLine($"[Diagnostics] {(chkShowRaw.Checked ? "Enabled" : "Disabled")}");
        }


        // ---------------- Core Polling Loop ----------------
        private async Task PollInverterAsync(CancellationToken token)
        {
            Debug.WriteLine("[System] Polling started...");

            try
            {
                while (!token.IsCancellationRequested)
                {
                    // 🔒 Disable System Info during polling work
                    SafeUI(() => btnSysInfo.Enabled = false);

                    try
                    {
                        if (_client == null)
                            break;

                        // --- Collect readings ---
                        var vGrid = await _client.ReadMeasureAsync(MeasureType.GridVoltage, true, token);
                        var iGrid = await _client.ReadMeasureAsync(MeasureType.GridCurrent, true, token);
                        var pGrid = await _client.ReadMeasureAsync(MeasureType.GridPower, true, token);
                        var freq = await _client.ReadMeasureAsync(MeasureType.Frequency, true, token);
                        var vBulk = await _client.ReadMeasureAsync(MeasureType.Vbulk, true, token);
                        var invTemp = await _client.ReadMeasureAsync(MeasureType.InverterTemp, true, token);
                        var boostTemp = await _client.ReadMeasureAsync(MeasureType.BoosterTemp, true, token);
                        var pv1V = await _client.ReadMeasureAsync(MeasureType.Input1Voltage, true, token);
                        var pv1I = await _client.ReadMeasureAsync(MeasureType.Input1Current, true, token);
                        var pv2V = await _client.ReadMeasureAsync(MeasureType.Input2Voltage, true, token);
                        var pv2I = await _client.ReadMeasureAsync(MeasureType.Input2Current, true, token);
                        var peaksToday = await _client.ReadMeasureAsync(MeasureType.GridPowerPeakToday, true, token);
                        var peaksEver = await _client.ReadMeasureAsync(MeasureType.GridPowerPeakEver, true, token);
                        var energy = await _client.ReadEnergyCountersAsync(token);

                        double pv1Watts = pv1V.AsFloatBigEndian() * pv1I.AsFloatBigEndian();
                        double pv2Watts = pv2V.AsFloatBigEndian() * pv2I.AsFloatBigEndian();

                        // --- UI Update ---
                        SafeUI(() =>
                        {
                            txtOutput.Clear();
                            txtOutput.AppendText($"=== Aurora Data @ {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===\r\n\r\n");

                            void Line(string label, string value)
                                => txtOutput.AppendText($"{label.PadRight(22)}= {value}\r\n");

                            // Grid / inverter data
                            Line("Grid Voltage", $"{vGrid.AsFloatBigEndian():F2} V{(chkShowRaw.Checked ? $" [RX={BitConverter.ToString(vGrid)}]" : "")}");
                            Line("Grid Current", $"{iGrid.AsFloatBigEndian():F2} A{(chkShowRaw.Checked ? $" [RX={BitConverter.ToString(iGrid)}]" : "")}");
                            Line("Grid Power", $"{pGrid.AsFloatBigEndian():F0} W{(chkShowRaw.Checked ? $" [RX={BitConverter.ToString(pGrid)}]" : "")}");
                            Line("Grid Freq", $"{freq.AsFloatBigEndian():F2} Hz{(chkShowRaw.Checked ? $" [RX={BitConverter.ToString(freq)}]" : "")}");
                            Line("Vbulk", $"{vBulk.AsFloatBigEndian():F2} V{(chkShowRaw.Checked ? $" [RX={BitConverter.ToString(vBulk)}]" : "")}");
                            Line("Inv Temp", $"{invTemp.AsFloatBigEndian():F1} °C{(chkShowRaw.Checked ? $" [RX={BitConverter.ToString(invTemp)}]" : "")}");
                            Line("Booster Temp", $"{boostTemp.AsFloatBigEndian():F1} °C{(chkShowRaw.Checked ? $" [RX={BitConverter.ToString(boostTemp)}]" : "")}");

                            // PV1
                            txtOutput.AppendText("\r\nPV1:\r\n");
                            Line("  Voltage", $"{pv1V.AsFloatBigEndian():F2} V{(chkShowRaw.Checked ? $" [RX={BitConverter.ToString(pv1V)}]" : "")}");
                            Line("  Current", $"{pv1I.AsFloatBigEndian():F2} A{(chkShowRaw.Checked ? $" [RX={BitConverter.ToString(pv1I)}]" : "")}");
                            Line("  Power", $"{pv1Watts:F0} W");

                            // PV2
                            txtOutput.AppendText("\r\nPV2:\r\n");
                            Line("  Voltage", $"{pv2V.AsFloatBigEndian():F2} V{(chkShowRaw.Checked ? $" [RX={BitConverter.ToString(pv2V)}]" : "")}");
                            Line("  Current", $"{pv2I.AsFloatBigEndian():F2} A{(chkShowRaw.Checked ? $" [RX={BitConverter.ToString(pv2I)}]" : "")}");
                            Line("  Power", $"{pv2Watts:F0} W");

                            // Peaks
                            txtOutput.AppendText("\r\n");
                            Line("Peak Today", $"{peaksToday.AsUInt16()} W{(chkShowRaw.Checked ? $" [RX={BitConverter.ToString(peaksToday)}]" : "")}");
                            Line("Peak Ever", $"{peaksEver.AsUInt16()} W{(chkShowRaw.Checked ? $" [RX={BitConverter.ToString(peaksEver)}]" : "")}");

                            // Energy
                            txtOutput.AppendText("\r\n");
                            Line("Energy Today", $"{energy.TodayKWh:F3} kWh");
                            Line("Energy Month", $"{energy.MonthKWh:F3} kWh");
                            Line("Energy Year", $"{energy.YearKWh:F3} kWh");
                            Line("Total Energy", $"{energy.TotalKWh:F3} kWh");
                            Line("Partial Energy", $"{energy.PartialKWh:F3} kWh");

                            lblStatus.Text = "Polling OK";
                            lblStatus.ForeColor = System.Drawing.Color.DarkGreen;
                            txtOutput.SelectionStart = txtOutput.TextLength;
                            txtOutput.ScrollToCaret();
                        });
                    }
                    catch (TaskCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        string msg = $"⚠ Polling error: {ex.Message}";
                        Debug.WriteLine(msg);
                        SafeUI(() =>
                        {
                            lblStatus.Text = msg;
                            lblStatus.ForeColor = System.Drawing.Color.OrangeRed;
                        });

                        // Allow recovery before next attempt
                        try { await Task.Delay(5000, token); } catch { /* ignore */ }
                    }
                    finally
                    {
                        // ✅ Enable the System Info button between poll intervals
                        SafeUI(() => btnSysInfo.Enabled = true);
                    }

                    // ⏳ Wait before next poll iteration (button remains enabled)
                    await Task.Delay(_config.PollingInterval, token);
                }
            }
            finally
            {
                Debug.WriteLine("[System] Polling stopped.");
                SafeUI(() =>
                {
                    btnSysInfo.Enabled = true;  // ensure enabled when loop stops
                    lblStatus.Text = "Polling stopped";
                    lblStatus.ForeColor = System.Drawing.Color.Gray;
                });
            }
        }

        // ---------------- Helpers ----------------

        private void SafeUI(Action update)
        {
            if (IsHandleCreated && !IsDisposed)
            {
                if (InvokeRequired)
                    BeginInvoke(update);
                else
                    update();
            }
        }

        private string FormatMeasure(string label, byte[] data, string unit, int decimals = 2)
        {
            float val = data.AsFloatBigEndian();
            string rx = chkShowRaw.Checked ? $" [RX={BitConverter.ToString(data)}]" : string.Empty;
            return $"{label} = {val.ToString($"F{decimals}")} {unit}{rx}\r\n";
        }


        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
            _client?.Dispose();
            _client = null;
            base.OnFormClosing(e);
        }


    }
}
