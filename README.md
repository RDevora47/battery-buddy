# Battery Buddy

A pixel-art axolotl that lives above your taskbar and wears your Bluetooth devices.
Each device shows its battery bar; the axolotl gets sleepy (≤ 20 %) or worried (≤ 10 %)
when your lowest device runs low.

- **Click a device** — speech bubble with its latest charge (Galaxy Buds: left / right / case).
- **Click the axolotl** — rescan now.
- **Drag** — move it; the position is remembered.
- **Right-click or tray icon** — Rescan, Start with Windows, Quit.

## Setup

1. Install the .NET 8 SDK: `winget install --id Microsoft.DotNet.SDK.8 -e`
2. Build: `dotnet build -c Release`
3. Run: `src/BatteryBuddy.App/bin/Release/net8.0-windows10.0.22621.0/BatteryBuddy.exe`

### Galaxy Buds detail (left / right / case)

Battery Buddy talks to the Buds directly, and only one app can hold that link.
Turn off Samsung's tray helper so it doesn't take the link at startup:
**Settings → Apps → Startup → "Galaxy Buds" (QuickControls) → Off**, then sign out or run
`Stop-Process -Name QuickControls`.

Opening the full Galaxy Buds app still works; it takes the link over while it's open, and Battery
Buddy shows the single Windows battery value ("L/R detail unavailable") until it closes.

## Files

- Settings: `%APPDATA%\BatteryBuddy\settings.json`
- Logs (7 days): `%LOCALAPPDATA%\BatteryBuddy\logs`

## Development

- Tests: `dotnet test`
- Hardware probes: `dotnet run --project tools/ProbeWindows` (PnP battery + events),
  `dotnet run --project tools/ProbeBuds` (Buds RFCOMM frames)

## Manual checklist

- [ ] Starts bottom-right above the taskbar; crisp pixels.
- [ ] Drag moves it without stealing focus from the active window; position survives a restart.
- [ ] Clicks on transparent pixels pass through to the window underneath.
- [ ] Not in Alt+Tab or the taskbar; tray icon shows the axolotl.
- [ ] Each connected device appears at its spot with the right bar color.
- [ ] Device click → bubble; fades after 6 s; another click replaces it.
- [ ] Body click → sniff animation, values refresh.
- [ ] Sleepy at ≤ 20 %, worried with a blinking device at ≤ 10 %.
- [ ] Disconnecting a device → smoke puff and "ploof!".
- [ ] Bluetooth off → devices gone, "Bluetooth is off"; back on → devices return.
- [ ] With QuickControls stopped, the Buds bubble shows L / R / case; opening the Galaxy Buds app
      switches it to "L/R detail unavailable"; closing it brings the detail back.
- [ ] Start with Windows toggles the `HKCU\…\Run\BatteryBuddy` value.
- [ ] Launching a second copy exits immediately.
