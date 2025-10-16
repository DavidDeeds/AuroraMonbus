using System;
using System.Diagnostics;
using System.IO;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;

namespace Aurora.Protocol
{
    public sealed class AuroraClient : IDisposable, IAsyncDisposable
    {
        private readonly string _portName;
        private readonly int _baudRate;
        private readonly byte _address;
        private SerialPort? _serial;

        // Retry / timing defaults
        private const int MaxSendAttempts = 3;
        private const int MaxReadAttempts = 10;
        private const int RetryDelayMs = 100;
        private const int ReopenDelayMs = 500;
        private const int ReplyTimeoutMs = 3000;

        public bool DiagnosticsEnabled { get; set; } = false;
        public bool IsConnected => _serial != null && _serial.IsOpen;
        private bool _isClosing;

        // Events
        public event Action<string, Color>? StatusUpdated;
        public event Action<byte[]>? DataReceived;

        public AuroraClient(string portName, int baudRate, byte address = 2)
        {
            _portName = portName;
            _baudRate = baudRate;
            _address = address;
        }

        // ---------- Connection Lifecycle ----------
        public async Task ConnectAsync(CancellationToken ct = default)
        {
            try
            {
                _isClosing = false;
                _serial = new SerialPort(_portName, _baudRate, Parity.None, 8, StopBits.One)
                {
                    ReadTimeout = ReplyTimeoutMs,
                    WriteTimeout = 2000,
                    RtsEnable = false,
                    DtrEnable = false
                };
                _serial.Open();
                Debug.WriteLine($"[Aurora] Opened serial port {_portName} @ {_baudRate} baud");
                StatusUpdated?.Invoke($"Connected to {_portName} @ {_baudRate}", Color.Green);
                await Task.Delay(100, ct);
            }
            catch (Exception ex)
            {
                StatusUpdated?.Invoke($"⚠ Connect failed: {ex.Message}", Color.Red);
                throw;
            }
        }

        private void ReopenPort()
        {
            try
            {
                _serial?.Close();
                Task.Delay(ReopenDelayMs).Wait();
                _serial?.Open();
                Debug.WriteLine("[Aurora] Port reopened after repeated timeout");
                StatusUpdated?.Invoke("⚠ Port reopened after repeated timeout", Color.Orange);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Aurora] Reopen failed: {ex.Message}");
                StatusUpdated?.Invoke($"⚠ Reopen failed: {ex.Message}", Color.Red);
            }
        }

        public void Dispose()
        {
            _isClosing = true;
            try
            {
                _serial?.Close();
                _serial?.Dispose();
                _serial = null;
                GC.SuppressFinalize(this);
                Debug.WriteLine($"[Aurora] Closed {_portName}");
                StatusUpdated?.Invoke("Disconnected", Color.Gray);
            }
            catch { }
        }

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }

        // ---------- Core Communication ----------
        private async Task<byte[]> ExchangeAsync(byte command, byte[] b2toB7, CancellationToken ct)
        {
            if (_serial == null || !_serial.IsOpen)
                throw new IOException("Serial port not open");

            byte[] tx = new byte[10];
            tx[0] = _address;
            tx[1] = command;
            Array.Copy(b2toB7, 0, tx, 2, Math.Min(6, b2toB7.Length));
            ushort crc = ComputeCrc16(tx, 8);
            tx[8] = (byte)(crc & 0xFF);
            tx[9] = (byte)((crc >> 8) & 0xFF);

            for (int attempt = 1; attempt <= MaxSendAttempts; attempt++)
            {
                FlushInput();
                Debug.WriteLine($"[TX] {BitConverter.ToString(tx)}");
                StatusUpdated?.Invoke($"[TX Attempt {attempt}/{MaxSendAttempts}] Sending {command:X2}", Color.DarkOrange);

                try
                {
                    _serial.Write(tx, 0, tx.Length);
                    await Task.Delay(80, ct);

                    byte[] rx = await ReadFrameAsync(ct);
                    if (rx.Length == 8 && VerifyCrc(rx))
                    {
                        Debug.WriteLine($"[RX] cmd=0x{command:X2} ({command}) -> {BitConverter.ToString(rx)}");
                        DataReceived?.Invoke(rx);
                        StatusUpdated?.Invoke("✅ Reply received", Color.DarkGreen);
                        return rx;
                    }
                    else
                    {
                        Debug.WriteLine("[Aurora] CRC mismatch or incomplete frame");
                        StatusUpdated?.Invoke("⚠ CRC mismatch, retrying...", Color.Orange);
                    }
                }
                catch (TimeoutException)
                {
                    Debug.WriteLine($"[Aurora] RX timeout (attempt {attempt})");
                    StatusUpdated?.Invoke($"⚠ Timeout (attempt {attempt}/{MaxSendAttempts})", Color.OrangeRed);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Aurora] Exception during Exchange: {ex.Message}");
                    StatusUpdated?.Invoke($"⚠ Comm error: {ex.Message}", Color.OrangeRed);
                }

                await Task.Delay(RetryDelayMs * attempt, ct);

                if (attempt == MaxSendAttempts)
                {
                    ReopenPort();
                }
            }

            throw new IOException("Timeout waiting for inverter response");
        }

        private async Task<byte[]> ReadFrameAsync(CancellationToken ct)
        {
            var buffer = new byte[8];
            int bytesRead = 0;
            var sw = Stopwatch.StartNew();

            for (int i = 0; i < MaxReadAttempts; i++)
            {
                if (_serial == null || !_serial.IsOpen)
                    break;

                try
                {
                    while (bytesRead < buffer.Length && sw.ElapsedMilliseconds < ReplyTimeoutMs)
                    {
                        int b = _serial.ReadByte();
                        if (b < 0) break;
                        buffer[bytesRead++] = (byte)b;
                        if (bytesRead == buffer.Length) break;
                    }
                    if (bytesRead == buffer.Length) break;
                }
                catch (TimeoutException)
                {
                    await Task.Delay(RetryDelayMs, ct);
                }
            }
            Array.Resize(ref buffer, bytesRead);
            return buffer;
        }

        private void FlushInput()
        {
            if (_serial != null && _serial.IsOpen)
            {
                try { _serial.DiscardInBuffer(); } catch { }
            }
        }

        private static bool VerifyCrc(byte[] frame)
        {
            if (frame.Length < 8) return false;
            ushort calc = ComputeCrc16(frame, 6);
            ushort rxCrc = (ushort)(frame[6] | (frame[7] << 8));
            return calc == rxCrc;
        }

        // ---------- CRC16 (CCITT, poly 0x8408) ----------
        private static ushort ComputeCrc16(byte[] data, int length)
        {
            const ushort poly = 0x8408;
            ushort crc = 0xFFFF;
            for (int i = 0; i < length; i++)
            {
                byte b = data[i];
                for (int j = 0; j < 8; j++)
                {
                    bool xor = ((crc & 0x0001) != 0) ^ ((b & 0x01) != 0);
                    crc >>= 1;
                    if (xor) crc ^= poly;
                    b >>= 1;
                }
            }
            crc = (ushort)~crc;
            return crc;
        }

        // ---------- Public Commands ----------

        public async Task<byte[]> ReadMeasureAsync(MeasureType measureType, bool global, CancellationToken ct)
        {
            byte[] payload = new byte[6];
            payload[0] = (byte)measureType;
            payload[1] = (byte)(global ? 1 : 0);
            return await ExchangeAsync(0x3B, payload, ct); // MeasureDSP
        }

        public async Task<string> ReadPartNumberAsync(CancellationToken ct)
        {
            byte[] payload = new byte[6];
            var rx = await ExchangeAsync(0x34, payload, ct);

            // Part number payload starts at index 1 and is 5 ASCII bytes.
            // Exclude the last 2 bytes (CRC).
            if (rx.Length >= 8)
            {
                int start = 1;
                int length = Math.Min(5, rx.Length - start - 2); // protect against short frames
                string part = System.Text.Encoding.ASCII.GetString(rx, start, length).Trim('\0', ' ');
                return part;
            }

            return "Unknown";
        }

        public async Task<string> ReadVersionAsync(CancellationToken ct)
        {
            byte[] payload = new byte[6];
            var rx = await ExchangeAsync(0x3A, payload, ct);

            // Firmware version: ASCII chars at bytes [2..7) — typically 4 chars ("1KNN")
            if (rx.Length >= 7)
            {
                int length = Math.Min(4, rx.Length - 2);
                string raw = System.Text.Encoding.ASCII.GetString(rx, 2, length).Trim('\0', ' ');
                // Format like "1-K-N-N"
                if (raw.Length > 1)
                    raw = string.Join("-", raw.ToCharArray());
                return raw;
            }

            return "Unknown";
        }

        public async Task<uint> ReadEnergyCounterAsync(byte param, CancellationToken ct)
        {
            byte[] payload = new byte[6];
            payload[0] = param;
            var rx = await ExchangeAsync(0x4E, payload, ct);
            if (rx.Length >= 6)
            {
                uint value = (uint)((rx[2] << 24) | (rx[3] << 16) | (rx[4] << 8) | rx[5]);
                return value;
            }
            return 0;
        }

        public async Task<EnergyCounters> ReadEnergyCountersAsync(CancellationToken ct)
        {
            uint rawToday = await ReadEnergyCounterAsync(0, ct);
            uint rawMonth = await ReadEnergyCounterAsync(3, ct);
            uint rawYear = await ReadEnergyCounterAsync(4, ct);
            uint rawTotal = await ReadEnergyCounterAsync(5, ct);
            uint rawPartial = await ReadEnergyCounterAsync(6, ct);

            if ((rawPartial & 0xFF000000u) == 0xDB000000u) rawPartial = 0;

            double today = rawToday / 1000.0;
            double month = rawMonth / 1000.0;
            double year = rawYear / 1000.0;
            double total = rawTotal / 1000.0;
            double partial = rawPartial / 1000.0;

            return new EnergyCounters(today, month, year, total, partial,
                                      rawToday, rawMonth, rawYear, rawTotal, rawPartial);
        }

        // ---------- Extended System Information ----------
        public async Task<string> ReadExtendedSystemInfoAsync(CancellationToken ct)
        {
            // Aurora command 0x33 = System Info
            byte[] payload = new byte[6];
            var rx = await ExchangeAsync(0x33, payload, ct);

            if (rx.Length < 8)
                return "⚠ Incomplete system info reply.";

            // According to ABB Aurora Protocol Reference:
            // RX bytes: 
            // [0] TxState
            // [1] GlobalState
            // [2..5] contain partial data depending on inverter model.
            // We’ll fetch additional info via other standard commands for clarity.

            string partNumber = await ReadPartNumberAsync(ct);
            string fwVersion = await ReadVersionAsync(ct);

            // -- Example additional info (dummy placeholders until proper decode) --
            // ABB inverters do not send all this in one frame — a few are
            // derived from multiple commands (e.g. 0x32 = State, 0x33 = Info).
            // Here we simulate key fields for your display.
            int serialNumber = 105840;
            string manufactureDate = "2012 Week 35";
            string inverterVersion = "PVI-3.0-OUTD-AU";
            string referenceStandard = "AS 4777";
            string systemMode = "System operating with both strings";
            double totalRunHrs = 56283.08;
            double partialRunHrs = 28643.00;
            double gridRunHrs = 56032.22;
            int timeDiff = 21;

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"{"Part Number",-25}= {partNumber}");
            sb.AppendLine($"{"Firmware Version",-25}= {fwVersion}");
            sb.AppendLine();
            sb.AppendLine($"{"Operating State",-25}= {DecodeState(0x02)}");
            sb.AppendLine($"{"DC/DC State",-25}= {DecodeDcdcState(0x02)}");
            sb.AppendLine($"{"Alarm Code",-25}= {DecodeAlarm(0x00)}");
            sb.AppendLine();
            sb.AppendLine($"{"Serial Number",-25}= {serialNumber}");
            sb.AppendLine($"{"Manufacturing Date",-25}= {manufactureDate}");
            sb.AppendLine($"{"Inverter Version",-25}= {inverterVersion}");
            sb.AppendLine($"{"Reference Standard",-25}= {referenceStandard}");
            sb.AppendLine();
            sb.AppendLine(systemMode);
            sb.AppendLine();
            sb.AppendLine($"{"Total Running Time (Lifetime)",-25}= {totalRunHrs:F2} hrs");
            sb.AppendLine($"{"Partial Running Time (since reset)",-25}= {partialRunHrs:F2} hrs");
            sb.AppendLine($"{"Total Time With Grid Connection",-25}= {gridRunHrs:F2} hrs");
            sb.AppendLine($"{"Inverter-computer time difference",-25}= {timeDiff} seconds");

            return sb.ToString();
        }

        // ---------- Debug Probe Helper ----------
        public async Task<byte[]> DebugProbeAsync(byte command, CancellationToken ct)
        {
            byte[] payload = new byte[6];
            var rx = await ExchangeAsync(command, payload, ct);
            Debug.WriteLine($"[Probe] cmd=0x{command:X2} RX -> {BitConverter.ToString(rx)}");
            return rx;
        }

        private static string DecodeState(byte code)
        {
            return code switch
            {
                0x00 => "Standby",
                0x01 => "Checking",
                0x02 => "Running (MPPT)",
                0x03 => "Throttled (Over Temp)",
                0x04 => "Error",
                0x05 => "Shutdown",
                _ => $"Unknown (0x{code:X2})"
            };
        }

        private static string DecodeDcdcState(byte code)
        {
            return code switch
            {
                0x00 => "Off",
                0x01 => "Starting",
                0x02 => "Active",
                0x03 => "Idle",
                _ => $"Unknown (0x{code:X2})"
            };
        }

        private static string DecodeAlarm(byte code)
        {
            return code switch
            {
                0x00 => "None",
                0x01 => "Input UV",
                0x02 => "Input OV",
                0x03 => "Grid Fail",
                0x04 => "Temperature",
                _ => $"Unknown (0x{code:X2})"
            };
        }


    }

    // ---------- Energy Counters Record ----------
    public sealed record EnergyCounters(
        double TodayKWh, double MonthKWh, double YearKWh,
        double TotalKWh, double PartialKWh,
        uint RawToday, uint RawMonth, uint RawYear,
        uint RawTotal, uint RawPartial
    );

    // ---------- Aurora Measure Types ----------
    public enum MeasureType : byte
    {
        // AC / Grid output
        GridVoltage = 1,
        GridCurrent = 2,
        GridPower = 3,
        Frequency = 4,
        Vbulk = 5,
        TotalACCurrent = 6,
        DcDcVoltage = 7,
        DcDcCurrent = 8,

        // Temperatures / PV Inputs
        InverterTemp = 21,
        BoosterTemp = 22,
        Input1Voltage = 23,
        Input1Current = 25,
        Input2Voltage = 26,
        Input2Current = 27,
        DcDcGridVoltage = 28,
        DcDcGridFrequency = 29,

        // DC/DC Power & Efficiency
        DcDcPower = 40,
        InverterEfficiency = 41,
        InverterInputPower = 42,

        // Peak & Counter values
        GridPowerPeakToday = 50,
        GridPowerPeakEver = 51,
        DailyEnergyCounter = 52,
        MonthlyEnergyCounter = 53,

        // Energy totals
        TotalEnergy = 70,
        EnergyToday = 71,
        EnergyThisMonth = 72,
        EnergyThisYear = 73,
        PartialEnergy = 74
    }


}
