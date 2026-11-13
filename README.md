# Police Chaser

An original 8-bit-style police pursuit racer for **Android (API 32), iOS 18+, macOS 26+ and
Windows 11**, built with MonoGame and .NET 10. It is a faithful interpretation of the
128K ZX Spectrum chase format - rear-view pursuit, a suspect car you have to ram, a burning
clock, checkpoints, traffic and a radio - with new graphics and sound, six missions, three
skill levels and a persistent high score table.

No artwork, code, music or data from any commercial game is included. Every sprite, sound,
music track, the logo and the app icon are generated procedurally by the game at runtime or
baked by `tools/gen_icons.py`.

## Gameplay

You are the pursuing unit. The suspect is ahead and pulling away; close the gap and ram the
back of the car the required number of times before the clock hits zero.

| Action | Keyboard | Gamepad | Touch |
| --- | --- | --- | --- |
| Steer | Left / Right (A / D) | Stick or D-pad | Left / right pads, or drag |
| Accelerate | automatic | automatic | automatic |
| Brake | Down / S / Left Ctrl | Left trigger | BRK pad |
| Turbo | Space / Z | B or X | TURBO pad |
| Fire / confirm | Enter / Space | A or Start | tap |
| Pause | Esc / P | Start | - |

### Skill levels

| Level | Suspect speed | Clock drain | Traffic | Points |
| --- | --- | --- | --- | --- |
| Beginner | 1.00x (the original ride) | 1.00x | 1.00x | full score |
| Medium | 1.06x | 1.15x | 1.35x | score / 2 |
| Hard | 1.14x | 1.35x | 1.80x | score / 5 |

The score shown everywhere (HUD, game over card, table) is already weighted, so a Hard run
has to be five times as good as a Beginner run to top the table.

### Missions

Six pursuits, in campaign order:

1. **Harbour Pursuit** - docks, night city
2. **Sunset Boulevard** - coast road
3. **Alpine Pass** - snow-covered mountain pass
4. **Neon Grid** - rain-soaked arcade district *(new mission)*
5. **Dune Runner** - salt flats in a sandstorm *(new mission)*
6. **Frostline** - blizzard border run *(new mission)*

Each mission has its own palette, scenery set, weather, suspect and traffic density.

## High scores

* Top **10** scores, sorted by weighted score.
* Persisted to `policechaser.scores.json` in the platform's local application data folder
  (`~/Library/Application Support/PoliceChaser` on macOS/iOS, `%LOCALAPPDATA%\PoliceChaser`
  on Windows, app-private storage on Android).
* Written the moment a score is entered **and** again when the app exits, so quitting and
  relaunching keeps the table.
* Three-letter arcade name entry. A corrupt or missing file starts a clean table rather than
  crashing.

## Music and sound

`ChipAudio` is a small software synth: pulse/triangle/saw/noise oscillators with ADSR,
a tiny note sequencer and PCM buffers handed to `SoundEffect`. Generated at runtime:

* title theme, chase theme, **game over theme** and **high score theme**
* Busted sting, checkpoint arpeggio, countdown beeps
* engine loop (pitch follows speed), two-tone siren, skid, crash, ram, turbo

## Building

Requirements: .NET 10 SDK, MonoGame templates not required (no content pipeline is used),
Xcode 26+ for the Apple targets, Android SDK 36 installed (targets/minSdk 32).

```bash
dotnet build PoliceChaser.slnx

# macOS .app bundle (osx-arm64 by default)
tools/make-macos-app.sh                 # ad-hoc signed
tools/make-macos-app.sh --sign          # signed with the installed Apple identity

# Windows 11 (win-x64, or RID=win-arm64 for Parallels on Apple silicon)
SELF_CONTAINED=1 tools/publish-windows.sh
RID=win-arm64 SELF_CONTAINED=1 tools/publish-windows.sh

# Android APK (application id uk.co.allthejohnsons.policechaser)
dotnet publish src/PoliceChaser.Android -c Release -f net10.0-android36.0

# iOS device build (bundle id uk.co.allthejohnsons.policechaser, iOS 18+)
dotnet publish src/PoliceChaser.iOS -c Release -f net10.0-ios26.0 -r ios-arm64 \
  -p:CodesignKey="Apple Development: Your Name (TEAMID)" \
  -p:CodesignProvision=devel-police
xcrun devicectl device install app --device <UDID> artifacts/ios-device/PoliceChaser.app
```

Bundle/package identifiers:

| Platform | Identifier |
| --- | --- |
| Android / iOS / macOS | `uk.co.allthejohnsons.policechaser` |
| Windows (MSIX) | `49556nodoid.PoliceChaser`, publisher `CN=A6EAEB04-6634-41C1-BDFE-695819ECE444` |

The Windows package identity lives in `windows/Package.appxmanifest`.

## Fidelity reference

`tools/FUSE_REFERENCE.md` documents how the supplied 128K Chase H.Q. images were used with
Fuse to compare HUD layout, palette and pursuit rules. `tools/fuse_reference.py` decodes the
tape's `SCREEN$` blocks to PNG for side-by-side comparison. The original images are never
copied into this repository or into the app.

## Tests

```bash
dotnet test tests/PoliceChaser.Tests
```

The xunit suite covers the race simulation (acceleration, clock drain, off-road handling,
turbo, ramming, traffic spawning), the difficulty weighting (1x / 2x / 5x), mission data and
high score ordering, truncation, persistence and corruption recovery.

## Smoke tests / screenshots

The desktop build can render one frame of any screen and exit, which is used to verify the
build on macOS and inside the Windows VM:

```bash
dotnet run --project src/PoliceChaser.Desktop -- --selftest race --frames 200 --shot shot.png
```

Screens: `splash`, `title`, `difficulty`, `mission`, `briefing`, `race`, `race2`, `busted`,
`gameover`, `name`, `scores`.

## Layout

```
src/PoliceChaser.Core        shared game: simulation, renderer, art, audio, UI
src/PoliceChaser.Desktop     Windows / macOS / Linux host
src/PoliceChaser.Android     Android host (API 32)
src/PoliceChaser.iOS         iOS host (18+)
src/PoliceChaser.macOS       macOS signing entitlements
tests/PoliceChaser.Tests     xunit suite
assets/                      generated icon master, .icns, Windows logos
tools/                       icon generation, packaging, Fuse reference helpers
windows/                     MSIX manifest (reserved Microsoft identity)
```
