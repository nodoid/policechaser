# Tools

Python tools require Pillow (`python3 -m pip install pillow`) and are run from the repository
root. Everything they generate is either committed on purpose (icons, launch art) or written to a
gitignored output folder (screenshots, store art).

## `gen_icons.py` - app icon and launch artwork

```bash
python3 tools/gen_icons.py
```

Bakes every icon and splash asset from one procedural design: the full-bleed icon master, the
padded macOS/window-tile variant, the iOS asset catalogue (including iOS 18 dark and tinted
appearance variants), the Android icon set with its adaptive foreground layers, the iOS launch
lockup, the Android splash logo, and the Windows tile and `SplashScreen.png` assets referenced by
the MSIX manifest.

| Output | Used by |
| --- | --- |
| `assets/AppIcon.iconset/`, `assets/AppIcon.icns`, `assets/icon-1024.png` | macOS bundle, `tools/make-macos-app.sh` |
| `assets/StoreLogo*.png`, `Square150x150Logo*.png`, `Square44x44Logo*.png`, `SplashScreen*.png` | MSIX manifest via `tools/publish-windows.sh` |
| `src/PoliceChaser.iOS/Assets.xcassets/`, `src/PoliceChaser.iOS/Icons/` | iOS app icon and launch screen |
| `src/PoliceChaser.Android/Resources/mipmap-*`, `drawable-*`, `mipmap-anydpi-v26/icon.xml` | Android launcher icon, adaptive icon and splash |
| `src/PoliceChaser.Desktop/Icon.ico` | Windows executable icon |

The scale and targetsize variants matter: the MSIX manifest references `Square44x44Logo` and
`StoreLogo` by name and expects the scaled PNGs beside them. Regenerate rather than hand-editing a
PNG, or the next run will overwrite it.

## `gen_screenshots.py` - in-game captures

```bash
python3 tools/gen_screenshots.py
```

Drives the desktop build's self-test harness over twelve screens and writes two sets:

| Output | Size | Use |
| --- | --- | --- |
| `artifacts/screenshots/raw/<screen>.png` | 1280x960 | Native captures, source for store art and the README |
| `artifacts/screenshots/store/NN-<screen>.png` | 1920x1080 | Captioned store frames on the game's navy livery |

The script builds the desktop project in `Debug` and then drives `dotnet run --no-build` once per
screen. The harness opens a window, so this is not a headless renderer: run it on a machine with
a display.

## `gen_store_assets.py` - store listing art

```bash
python3 tools/gen_store_assets.py    # after gen_screenshots.py
```

Bakes platform-sized listing art into `Stores/` (gitignored) from the raw captures, using the
sizes required by the stores at the time of writing: Apple iPhone 6.9" (2868x1320) and 6.3"
(2622x1206), iPad 13" (2752x2064), Mac (2880x1800); Google phone (1920x1080), tablet (2560x1440)
and feature graphic (1024x500); Microsoft and Xbox screenshots at 1920x1080 and 3840x2160.

Microsoft and Xbox screenshots stay clean - those stores reject marketing overlays on
screenshots - while the Apple and Google frames carry the wordmark and a caption.

## `publish-windows.sh` - Windows staging

```bash
tools/publish-windows.sh                 # framework-dependent
SELF_CONTAINED=1 tools/publish-windows.sh
RID=win-arm64 SELF_CONTAINED=1 tools/publish-windows.sh
```

Publishes the desktop project as a `WinExe`, stages `windows/Package.appxmanifest` and copies the
Windows tile/splash art into `Assets/` inside `artifacts/$RID`. See `docs/building.md` for the
variables.

## `make-macos-app.sh` - macOS bundle

```bash
tools/make-macos-app.sh                  # ad-hoc signed
tools/make-macos-app.sh --sign           # signed with an installed Apple identity
RID=osx-x64 tools/make-macos-app.sh
```

Publishes the desktop project, writes an `Info.plist`, copies `assets/AppIcon.icns` in, optionally
signs with `src/PoliceChaser.macOS/entitlements.plist`, and refreshes the Launch Services
database so Finder picks up the new icon.

## `fuse_reference.py` and `FUSE_REFERENCE.md`

Reference material for tuning fidelity against the 8-bit original. `fuse_reference.py` decodes the
`SCREEN$` blocks of a supplied tape image to PNG so the palette and HUD can be compared
side-by-side; `FUSE_REFERENCE.md` records what was compared and what changed as a result.

The tape images themselves stay outside the repository. Nothing from the original is copied into
this project, and the script takes the tape path as an argument because it is not shipped here.

## Self-test harness

The desktop build can render one screen and exit, which is how builds are smoke tested:

```bash
dotnet run --project src/PoliceChaser.Desktop -- --selftest race --frames 200 --shot shot.png
```

| Flag | Default | Meaning |
| --- | --- | --- |
| `--selftest <screen>` | - | Screen to show, then exit |
| `--frames N` | 90 | Frames to simulate before capturing |
| `--shot path` | `selftest.png` | PNG to write |

Screens: `splash`, `title`, `difficulty`, `mission`, `briefing`, `briefing2`, `race`, `race2`,
`race3`, `crash`, `wreck`, `pads`, `busted`, `gameover`, `name`, `nametap`, `touch`, `scores`,
`settings`.

* `pads` forces the mobile touch overlay on a desktop build.
* `crash` holds the impact sparks on the player car.
* `wreck` crashes repeatedly until the damaged-car game-over card appears.
* `nametap` sends one simulated touch tap through the name-entry keys.
* `touch` taps through every interactive screen (title, high scores, skill, mission, briefing,
  pause, busted, game over and name entry) and writes a pass/fail audit to `<shot>.audit.txt`.

Self-tests swap in memory-backed score and settings stores, so a smoke run never touches the
player's real table.

### On iOS

The iOS host reads `PC_SELFTEST` from the launch environment, which `simctl` passes through:

```bash
SIMCTL_CHILD_PC_SELFTEST=race xcrun simctl launch <UDID> uk.co.allthejohnsons.policechaser
```

The capture is written to `selftest.png` (and the touch audit alongside it) in the app's
`Documents/PoliceChaser` folder inside the simulator's data container; allow 10-20 seconds before
copying it out, as early copies can pick up a stale frame.
