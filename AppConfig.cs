using System;
using System.IO;
using System.Text.Json;

namespace AuroraMonbus
{
    internal sealed class AppConfig
    {
        public string ConnectionType { get; set; } = "Serial";
        public string SerialPort { get; set; } = "COM4";
        public int BaudRate { get; set; } = 19200;
        public string TcpHost { get; set; } = "192.168.1.100"; // unused (for completeness)
        public int TcpPort { get; set; } = 502;                // unused
        public byte Address { get; set; } = 2;
        public int PollingInterval { get; set; } = 10000;

        private const string FileName = "AppConfig.json";

        public static AppConfig Load()
        {
            try
            {
                string exeDir = AppContext.BaseDirectory;
                string path = Path.Combine(exeDir, FileName);
                if (!File.Exists(path))
                    throw new FileNotFoundException("AppConfig.json not found");

                string json = File.ReadAllText(path);
                var cfg = JsonSerializer.Deserialize<AppConfig>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (cfg == null)
                    throw new InvalidDataException("Invalid AppConfig.json");

                return cfg;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Config] Load failed: {ex.Message} — using defaults.");
                return new AppConfig();
            }
        }
    }
}
