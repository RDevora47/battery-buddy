<h1 align="center">Battery Buddy</h1>

<p align="center">
  <b>A pixel-art pet that lives above your taskbar and wears your Bluetooth devices.</b><br>
  Earbuds on its ears, a mouse under its paw, a keyboard on its desk, and a glance is all it takes to see what needs charging.
</p>

<p align="center">
  <img src="docs/images/banner.gif" alt="All eight pets typing at their desks, wearing earbuds with a mouse and keyboard">
</p>

<p align="center">
  <a href="https://github.com/RDevora47/battery-buddy/releases/latest/download/BatteryBuddy-standalone-win-x64.zip"><img src="https://img.shields.io/badge/Download-Windows%2010%20%2F%2011-2ea44f?style=for-the-badge&logo=windows&logoColor=white" alt="Download for Windows"></a>
  <br>
  <a href="https://github.com/RDevora47/battery-buddy/releases/latest"><img src="https://img.shields.io/github/v/release/RDevora47/battery-buddy?label=latest&style=flat-square" alt="Latest release"></a>
  <a href="LICENSE"><img src="https://img.shields.io/badge/license-MIT-blue?style=flat-square" alt="MIT license"></a>
  <img src="https://img.shields.io/badge/.NET-8-512BD4?style=flat-square" alt=".NET 8">
</p>

## Meet the pets

Pick one from the tray menu. They all type along with your keyboard and click along with your mouse.

<table>
  <tr>
    <td align="center"><img src="docs/images/pet-axolotl.gif" alt="Mochi the axolotl"><br><b>Mochi</b><br><sub>axolotl · gills droop as batteries drain</sub></td>
    <td align="center"><img src="docs/images/pet-redpanda.gif" alt="Maple the red panda"><br><b>Maple</b><br><sub>red panda · wags its tail when happy</sub></td>
    <td align="center"><img src="docs/images/pet-bunny.gif" alt="Bebonio the bunny"><br><b>Bebonio</b><br><sub>bunny</sub></td>
    <td align="center"><img src="docs/images/pet-panda.gif" alt="Bao the giant panda"><br><b>Bao</b><br><sub>giant panda</sub></td>
  </tr>
  <tr>
    <td align="center"><img src="docs/images/pet-parrot.gif" alt="Mango the parrot"><br><b>Mango</b><br><sub>parrot · hops from key to key</sub></td>
    <td align="center"><img src="docs/images/pet-jellyfish.gif" alt="Boba the jellyfish"><br><b>Boba</b><br><sub>jellyfish · a long tentacle works the mouse</sub></td>
    <td align="center"><img src="docs/images/pet-choppa.gif" alt="Choppa the dog"><br><b>Choppa</b><br><sub>dog · wags its tail when happy</sub></td>
    <td align="center"><img src="docs/images/pet-missy.gif" alt="Missy the dog"><br><b>Missy</b><br><sub>havapoo · wags its tail when happy</sub></td>
  </tr>
</table>

## How it shows your batteries

Every device sits in its own spot with its charge around it (a colored outline, a bar or a mini
battery, your choice), and a lightning bolt while it charges. The pet's mood follows your
**lowest** device: its gills or ears droop and its color fades as that one drains.

<table>
  <tr>
    <td align="center"><img src="docs/images/mood-happy.gif" alt="Happy pet"><br><b>Happy</b><br><sub>everything above 20 %</sub></td>
    <td align="center"><img src="docs/images/mood-sleepy.gif" alt="Sleepy pet with a Zzz"><br><b>Sleepy</b><br><sub>a device at 20 % or less</sub></td>
    <td align="center"><img src="docs/images/mood-critical.gif" alt="Worried pet shaking a nearly empty mouse"><br><b>Worried</b><br><sub>at 10 % it shakes the device<br>for the last bit of energy</sub></td>
  </tr>
  <tr>
    <td align="center"><img src="docs/images/mood-full-saiyan.gif" alt="Pet with spiky golden hair"><br><b>Fully charged</b><br><sub>every device at 95 %+:<br>Super Saiyan hair…</sub></td>
    <td align="center"><img src="docs/images/mood-full-strawhat.gif" alt="Red panda in a straw hat"><br><b>…or a straw hat</b><br><sub>(or nothing: your pick)</sub></td>
    <td align="center"><img src="docs/images/rescan.gif" alt="Pet searching with a magnifying glass"><br><b>Rescanning</b><br><sub>click the pet to look<br>for devices right now</sub></td>
  </tr>
</table>

### Things to try

- **Click a device**: a speech bubble with its latest charge (Galaxy Buds: left / right / case).
- **Click the pet**: rescan now.
- **Right-click the mouse** (Logitech Easy-Switch mice): switch it to another computer's channel.
- **Drag** it anywhere; it remembers where.
- **Right-click the pet or the tray icon**: Rescan, Pet, Full-charge hat, Battery display, Start with Windows, Quit.

## Download

Two builds of the same app; both are a single `BatteryBuddy.exe` inside a zip, nothing to install:

| | Size | Runs on |
|---|---|---|
| **[Standalone](https://github.com/RDevora47/battery-buddy/releases/latest/download/BatteryBuddy-standalone-win-x64.zip)** | ~70 MB | Any 64-bit Windows 10 or 11. Pick this one if unsure. |
| **[Small](https://github.com/RDevora47/battery-buddy/releases/latest/download/BatteryBuddy-dotnet8-win-x64.zip)** | ~7 MB | PCs with the [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) installed. |

Unzip it anywhere and run `BatteryBuddy.exe`. It isn't code-signed, so the first launch may show
"Windows protected your PC": click **More info → Run anyway**. Older versions are on the
[Releases](https://github.com/RDevora47/battery-buddy/releases) page.

## Supported devices

Anything Windows reports a Bluetooth battery level for, plus richer detail for:

- **Logitech mice and keyboards** (Bluetooth or on a Unifying / Bolt / Lightspeed receiver): real
  battery level and charging state over HID++, with or without Logi Options+ running. Devices that
  don't support it fall back to the Windows value, with charging inferred from rising levels.
- **Samsung Galaxy Buds**: left, right and case separately. Battery Buddy talks to the Buds directly,
  and only one app can hold that link, so turn off Samsung's tray helper:
  **Settings → Apps → Startup → "Galaxy Buds" (QuickControls) → Off**, then sign out or run
  `Stop-Process -Name QuickControls`. Opening the full Galaxy Buds app still works; while it's open,
  Battery Buddy shows the single Windows value ("L/R detail unavailable").

Settings live in `%APPDATA%\BatteryBuddy\settings.json`, logs (7 days) in `%LOCALAPPDATA%\BatteryBuddy\logs`.

## Build from source

1. Install the .NET 8 SDK: `winget install --id Microsoft.DotNet.SDK.8 -e`
2. Build: `.\build\build.ps1` (runs the tests, then publishes; add `-SkipTests` to skip them,
   or `-SelfContained` to bundle the .NET runtime so the exe runs on PCs without .NET 8;
   `-AutoClose` stops any running Battery Buddy first, `-AutoLaunch` starts the new build when done;
   with several branches it asks which to build, or pass `-Branch <name>`)
3. Run: `build\out\BatteryBuddy.exe`

### Development

- Tests: `dotnet test`
- Hardware probes: `dotnet run --project tools/ProbeWindows` (PnP battery + events),
  `dotnet run --project tools/ProbeBuds` (Buds RFCOMM frames),
  `dotnet run --project tools/ProbeLogitech [list|hub|<seconds>]` (HID++ battery; read-only)
- Skins: `src/BatteryBuddy.App/Skins/<pet>/` holds `skin.json` (where things go) and `sprites.txt`
  (its palette, `body_*`, `gills_*`, `paw` and optional `tail*` sprites), drawn over the shared
  `Skins/common/sprites.txt`, which also holds the full-charge hats (`saiyan`, `strawhat`).
  To add a pet, embed both files in `BatteryBuddy.App.csproj` and list it in `SkinLoader.Pets`;
  `SkinTests` checks every skin folder.
- README pictures: `dotnet run --project tools/MakeShowcase` redraws every GIF in `docs/images`
  with the app's own animator, so rerun it after changing a skin.
- Adding a protocol: implement `IDeviceSource` (from `BatteryBuddy.Devices`) in a new
  `src/BatteryBuddy.Backends.<Name>` project and add it to `src/BatteryBuddy.App/Backends.cs`.
  Set `ChargingKnown` if it reports charging and `Address` if it knows the Bluetooth MAC.
- Releasing: on a clean `main`, `.\build\build.ps1 -Release 1.2.0` runs the tests, builds both
  downloads into `build\release\` (`-Package` does just that part), sets the version in
  `Directory.Build.props`, commits and tags `v1.2.0`. Pushing (`git push origin main --follow-tags`)
  makes GitHub Actions build the tag the same way and publish the GitHub release with both zips.

## License

[MIT](LICENSE) © 2026 Roberto Devora

Battery Buddy is an independent hobby project. It is not affiliated with, endorsed by or
sponsored by Logitech, Samsung or Microsoft; their product names are used only to say which
devices it works with, and belong to their owners. The Galaxy Buds and HID++ support is written
from publicly documented protocol details. Use it at your own risk.
