# AuroraMonbus — Power-One Aurora / ABB / FIMER Inverter Monitoring Tool

AuroraMonbus is a Windows desktop application written in C# (.NET 8) that communicates directly with *some* Power-one Aurora / ABB / FIMER Aurora solar inverters over RS-485 (serial).
It provides real-time monitoring, diagnostics, and system performance insights through a modern, responsive WinForms interface.

When I say *some* (inverters), I only have a Power-One Aurora PVI-3.0 OUTD invertor from around 2012 and I have not tested (and sorry dont plan to) on anything else.

I only built this tool for that unit with a view to eventually build in these capabilities:
1. Publish MQTT messages in a format suitable for Home Assistant MQTT to consume.
2. Send to the pvoutput.org API.
3. Minimise to the Windows system tray.

But you can take this code and do what you like for yours / other models as a fair bit went into getting it this far and generally reliable.

## Key Features

- Direct Serial Communication
Implements the Aurora protocol for two-way communication with inverters using CRC-16-CCITT framing and robust retry logic.
Supports all core commands including real-time operating data, temperature, and energy counters.

- Real-Time Polling
Continuously retrieves and displays inverter metrics such as:
  - Grid voltage, current, power, and frequency
  - PV input voltages/currents and derived wattage
  - Inverter and booster temperatures
  - Daily, monthly, yearly, and total energy generation

- Diagnostics Mode
Toggle raw communication bytes ([TX] and [RX] traces) in real time for advanced debugging and protocol analysis.
- System Information Snapshot
A single click shows firmware version, part number, and hardware-level inverter diagnostics via the System Info button.
- Configurable Runtime Settings
Load communication parameters such as COM port, baud rate, inverter address, and polling interval directly from AppConfig.json — no recompilation required.

- Safe Multithreading
Async-safe communication with full UI-thread marshaling for stable, non-blocking operation and graceful cancellation on disconnect.

## Project Structure
| File  | Purpose  |
| :------------ | :------------ |
| MainForm.cs  | WinForms front-end, handles UI updates, background polling, and status display  |
| AuroraClient.cs  | Communication layer implementing Aurora serial protocol, CRC handling, retries, and energy counter decoding  |
| ByteArrayExtensions.cs  | Utility methods for decoding inverter float and integer data (big-endian)  |
| AppConfig.cs  | Loads and manages configuration values from JSON  |
| AppConfig.json  | Runtime configuration for serial settings and polling interval  |

## Technical Highlights
- Implements Aurora protocol commands (0x3B, 0x4E, 0x34, 0x3A, etc.)
- Uses async/await for all I/O operations
- Built-in error recovery, port reopen logic, and CRC validation
- Debug output through System.Diagnostics.Debug.WriteLine()
- Clean disposal pattern with IDisposable and GC.SuppressFinalize
- Optimised polling interval with intelligent UI button toggling

## Example Output
![](https://github.com/DavidDeeds/AuroraMonbus/screenshot.jpg)

## Getting Started
1. Connect your inverter RS-485 port to a RS485 to Ethernet Converter such as this https://www.pusr.com/products/1-port-rs485-to-ethernet-converters-usr-tcp232-304.html or similar Wi-Fi model (recommend) to get a virtual COM port in Windows or use a RS-485 to USB adapter.
2. Configure your AppConfig.json
   `{
      "ConnectionType": "Serial",
      "SerialPort": "COM4",
      "BaudRate": 19200,
      "Address": 2,
      "PollingInterval": 10000
    }`
4. Run the application, click Connect, and watch data stream live.
5. Click System Info for inverter identity and firmware version.

- SerialPort is your Comport number.
- BaudRate is 19200 for serial comms on these units typically.
- Address will nearly always be 2.
- PollingInterval is in milliseconds. 10 seconds (as shown) is safe and generally about all your need. The protoocol and this software implementation could go as low as 5 seconds safely - YMMV.

## Development Notes
- Fully async-safe and thread-isolated for WinForms.
- Ideal base for building on.
