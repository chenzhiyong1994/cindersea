# Cindersea · 烬海天阙

**A single-player tactical roguelike set in a Chinese fantasy city.** Choose three companions, build their skills, and climb a branching twelve-floor route toward the sunwheel above Yaojing.

[简体中文](README.zh-CN.md) · [Project website](https://chenzhiyong1994.github.io/cindersea/) · [Download & play](#play) · [Build the game](#build) · [Report a bug](https://github.com/chenzhiyong1994/cindersea/issues)

**The project is now named Cindersea · 烬海天阙.** The existing **Beta 1** release keeps version **`v0.22.4-beta.1`** and its original `Dicebound` package and executable filenames.

**Beta 1** is based on the playable **0.22.4** version. The game interface and story are currently in **Simplified Chinese**; these English docs do not imply an English localization.

![Yaojing and its sunwheel — project title artwork](docs/assets/yaojing.webp)

*Title artwork from the game.*

## The game

- **Three companions, one turn.** Switch between party members to combine movement, attacks, support, and control. End each character's actions separately; enemies act after every surviving companion finishes.
- **Terrain matters.** Cover, high ground, water mist, shadows, hot pipes, and throwable objects shape positioning and skill use. Inspect enemy skills and their next actions before committing.
- **Build along the climb.** Branch between battles, elite gates, shops, campfires, and events. Learn and upgrade character-specific skills, collect relics, and spend gold to reroll shop offers.
- **Five distinct roles.** Assemble a party of three from the following companions. Each has their own skills and ultimate.

| Companion | Role |
| --- | --- |
| 司玄 · Sixuan | Control, restraint, and protective support |
| 凌风 · Lingfeng | Blade attacks, knockback, and lingering fire |
| 沧泠 · Cangling | Water techniques, healing, and team support |
| 晏烛影 · Yanzhuying | Shadow movement and precision attacks |
| 商朔 · Shangshuo | Shields, defensive positioning, and retaliation |

The presentation combines painted environments, pixel characters, ink-and-jade panels, animated portraits, and scene-specific music. A reduced-motion setting replaces portrait motion and reduces battle effects.

<a id="play"></a>
## Play

**[Download Windows x64 installer · Beta 1](https://github.com/chenzhiyong1994/cindersea/releases/download/v0.22.4-beta.1/Dicebound-0.22.4-beta.1-Windows-x64-Setup.exe)**

[Portable ZIP](https://github.com/chenzhiyong1994/cindersea/releases/download/v0.22.4-beta.1/Dicebound-0.22.4-beta.1-Windows-x64.zip) · [SHA-256 checksums](https://github.com/chenzhiyong1994/cindersea/releases/download/v0.22.4-beta.1/SHA256SUMS.txt) · [Release notes](https://github.com/chenzhiyong1994/cindersea/releases/tag/v0.22.4-beta.1)

Run the installer and follow its setup steps. For the portable version, extract the entire ZIP and run `Dicebound.exe`; keep its data folder, runtime files, and notices beside it. Neither package requires Unity or a development environment. The installer and game executable are not code-signed, so Windows may display a publisher or reputation warning. See the [download and checksum guide](manual/build.md#download) for package verification.

Begin a new journey, select three companions, and choose a connected route node. The default difficulty is **踏岚**; the other settings are **破障**, **逆潮**, and **登阙**.

| Input | Action |
| --- | --- |
| Left mouse button | Select a companion, skill, route, or target |
| `F1`–`F3` | Switch active companion |
| `Space` | Confirm the selected action |
| Right mouse button / `Esc` | Cancel the current selection or close a panel |
| `E` | End the active companion's actions |
| Hold `Alt` | Temporarily reveal the ground beneath characters |
| Mouse wheel / middle-button drag | Zoom / pan the battlefield |
| `Home` / `F11` | Reset the camera / toggle fullscreen |

Select a target before confirming. Blue marks reachable tiles; gold shows the planned movement path. The team and inventory panels explain current skills, statuses, and relics. Underlined terms open their rules.

Progress saves automatically. Settings include save import/export, audio levels, and reduced motion. Back up your save before trying a newer beta; see [save locations and development checks](manual/build.md).

<a id="build"></a>
## Build

The supported build target is **Windows x64**. Use **Unity 6000.3.24f1 (6.3 LTS)** with Windows Build Support and **PowerShell 7**. The project uses **URP 17.3.0**; Unity resolves dependencies from the committed package manifest and lockfile.

```powershell
git clone https://github.com/chenzhiyong1994/cindersea.git
cd cindersea
pwsh -NoProfile -File tools/build-native.ps1 -Editor "C:/path/to/6000.3.24f1/Editor/Unity.exe"
```

The Player is written to `native/Dicebound/Builds/Windows/Dicebound.exe`. You can also open `native/Dicebound` in the matching Unity Editor. The first import requires network access to resolve Unity packages. Playing the built game does not require a game account or a generation service.

[Full build instructions and optional .NET 10 rule checks](manual/build.md)

## Source layout

| Path | Purpose |
| --- | --- |
| `native/Dicebound/Assets/Dicebound/Core/` | Tactical rules, content, and progression |
| `native/Dicebound/Assets/Dicebound/Persistence/` | Journey and chronicle persistence |
| `native/Dicebound/Assets/Dicebound/Runtime/` | Unity presentation, input, UI, and audio |
| `native/Dicebound/Assets/Resources/` | Game art, audio, fonts, and runtime resources |
| `native/Dicebound/Assets/StreamingAssets/` | Local video files |
| `tools/` | Build and development utilities |
| `docs/` | Static project website |

## Beta status

This is an early public beta. The independently exported public project passed a fresh Unity Windows build with no C# or shader compilation errors. The final portrait/video changes have not completed a fresh in-game visual review or full regression pass; successful compilation does not certify those behaviors. Balance, presentation, and compatibility may still change. Linux, macOS, mobile, and English gameplay are not supported release targets.

Some portrait loops use a fixed transparency outline, and the music uses complete tracks rather than phrase-aligned seamless loops. See the [changelog](CHANGELOG.md) for the beta's scope and limitations.

Contributions are welcome: read [CONTRIBUTING.md](CONTRIBUTING.md). Please report reproducible issues with the version, steps, and relevant logs; omit private data. For sensitive reports, follow [SECURITY.md](SECURITY.md).

<a id="licenses"></a>
## Licenses and credits

Original code and tools are licensed under the [MIT License](LICENSE). **Art, story, videos, music, fonts, and third-party assets are not automatically licensed under MIT.** Their scopes and credits are documented in [ASSET_NOTICES.md](ASSET_NOTICES.md).

The project includes AI-assisted illustrations and videos, Suno-generated music supplied by the maintainer, and separately licensed fonts and public asset libraries. These tools and asset authors do not endorse this project.
