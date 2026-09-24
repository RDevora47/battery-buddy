# Battery Buddy

A pixel-art pet that lives above your taskbar and wears your Bluetooth devices: Mochi the
axolotl, Maple the red panda, Bebonio the bunny, Bao the giant panda, Mango the parrot, or the
dogs Choppa and Missy. Each device shows its charge (colored outline, bar or mini battery) and a
bolt while it charges. As your lowest device drains, Mochi's gills (Mango's crest, the others'
ears) droop and its color fades; it gets sleepy (≤ 20 %)
and, at ≤ 10 %, shakes the device to squeeze out the last energy.

- **Click a device** — speech bubble with its latest charge (Galaxy Buds: left / right / case).
- **Click the pet** — rescan now.
- **Drag** — move it; the position is remembered.
- **Right-click or tray icon** — Rescan, Pet, Full-charge hat, Battery display, Start with Windows, Quit.
- **Full-charge hat** — what the pet wears while every device is at 95 % or more: Super Saiyan hair,
  a straw hat (One Piece), or none. Maple and the dogs also wag their tails when they're happy.
- **Mango** is small enough to stand on the keyboard, and hops from key to key as you type.

## Setup

1. Install the .NET 8 SDK: `winget install --id Microsoft.DotNet.SDK.8 -e`
2. Build: `.\build\build.ps1` (runs the tests, then publishes; add `-SkipTests` to skip them,
   or `-SelfContained` to bundle the .NET runtime so the exe runs on PCs without .NET 8)
3. Run: `build\out\BatteryBuddy.exe`

### Logitech devices

Logitech mice and keyboards (Bluetooth or on a Unifying/Bolt/Lightspeed receiver) report
their real battery level and charging state over HID++, with or without Logi Options+
running. Devices that don't support it fall back to the Windows value, with charging
inferred from rising levels.

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
  `dotnet run --project tools/ProbeBuds` (Buds RFCOMM frames),
  `dotnet run --project tools/ProbeLogitech [list|hub|<seconds>]` (HID++ battery; read-only)
- Skins: `src/BatteryBuddy.App/Skins/<pet>/` holds `skin.json` (where things go) and `sprites.txt`
  (its palette, `body_*`, `gills_*`, `paw` and optional `tail*` sprites), drawn over the shared
  `Skins/common/sprites.txt`, which also holds the full-charge hats (`saiyan`, `strawhat`).
  To add a pet, embed both files in `BatteryBuddy.App.csproj` and list it in `SkinLoader.Pets`;
  `SkinTests` checks every skin folder.
- Adding a protocol: implement `IDeviceSource` (from `BatteryBuddy.Devices`) in a new
  `src/BatteryBuddy.Backends.<Name>` project and add it to `src/BatteryBuddy.App/Backends.cs`.
  Set `ChargingKnown` if it reports charging and `Address` if it knows the Bluetooth MAC.

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
