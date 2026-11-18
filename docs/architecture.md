# Architecture

Police Chaser is one MonoGame `Game` subclass (`ChaseGame`) shared by every platform, with a
thin native host per platform. There is no content pipeline: art, fonts, music and sound are
baked procedurally at startup, so a build needs only the .NET SDK and the platform SDK.

## Projects

| Project | Target framework | Role |
| --- | --- | --- |
| `src/PoliceChaser.Core` | `net10.0`, `net10.0-android36.0`, `net10.0-ios` | The game: simulation, renderer, procedural art, audio, UI and input |
| `src/PoliceChaser.Desktop` | `net10.0` | Windows / macOS / Linux host, including the self-test entry point |
| `src/PoliceChaser.Android` | `net10.0-android36.0` | `AndroidGameActivity` host, display metrics, system bars and cutout handling |
| `src/PoliceChaser.iOS` | `net10.0-ios` | UIKit scene host, launch and background audio handling |
| `src/PoliceChaser.macOS` | (no project) | Signing entitlements used by `tools/make-macos-app.sh --sign` |
| `tests/PoliceChaser.Tests` | `net10.0` | xunit suite for the pure simulation, data and persistence code |

`Directory.Build.props` pins `MonoGameVersion` (3.8.6.1) and the shared assembly metadata. Core
references `FontStashSharp.MonoGame` and embeds `ChakraPetch-SemiBold.ttf` as a resource, so the
font travels inside the assembly on every target.

## Frame loop and screens

`ChaseGame.Update` and `ChaseGame.Draw` do a little platform housekeeping and then hand off to
`UpdateFrame` / `DrawFrame`. One `GameScreen` value drives both:

`Splash`, `Title`, `DifficultySelect`, `MissionSelect`, `Briefing`, `Racing`, `Busted`,
`MissionCleared`, `CampaignComplete`, `GameOver`, `NameEntry`, `HighScores`, `Settings`,
`Paused`.

Each screen owns an update method (`UpdateTitle`, `UpdateRacing`, ...) and a draw method of the
same name; transitions go through small helpers rather than being scattered across the file.
`_screenTime` is reset on every transition and drives splash timers and fades, while `_time` is
the free-running clock used for flashes and pulses.

Campaign state lives next to the screen: `_difficulty`, `_missionCursor`, `_campaignIndex`,
`_campaignRaw` (unweighted score), `_finalScore` and `_pendingEntry` (the score waiting for a
name). The race itself is a separate `Race` instance; `ChaseGame` only feeds it input, turns its
events into sound/HUD/radio feedback, and decides when the campaign advances or ends.

## Layout and scaling

The layout is authored in a **320x240** grid (`RenderSettings`). Nothing is drawn at that size:
`RenderSettings.Scale` is 4, so the render target is **1280x960** and every shape, glyph and
sprite is rasterised at 4x. Coordinates in drawing code are logical units and get multiplied by
`S` (or by 4 inside baked art), which keeps the layout maths small while the picture stays sharp.

### Desktop and portrait

The render target is drawn to the back buffer with the largest integer-free fit that preserves
4:3, centred, which produces the letterbox or pillarbox bars. A portrait window shows a rotate
prompt instead of a squashed picture.

### Phones

On `ANDROID`/`IOS` the `MobilePlatform` constant is true and the canvas is widened instead:

1. The Android host passes the real display size to `SetBackBuffer` before `Run`, because
   MonoGame would otherwise keep the 4:3 preferred back buffer and leave the rest of the screen
   black.
2. `UpdateCanvas` computes `canvasWidth = round(960 * displayWidth / displayHeight)`, clamps it
   to `1280..2560`, and rounds **up to a multiple of 16**. The multiple of 16 matters: the
   centred picture then starts on a whole logical pixel, so layout and hit testing never fall
   between pixels.
3. `_originX = (canvasWidth - 1280) / 2` is where the authored picture starts inside the wider
   canvas; `_wide` says whether any extra width exists.
4. `ComputeDestination` returns the whole back buffer when `_wide`, otherwise the normal
   letterboxed rectangle.

The authored picture keeps its 4:3 field of view; the extra width is *more world*, not a
stretched world. `RoadRenderer.FocalX` stays at `1280 / 2`, and the renderer is told the canvas
width and origin so it can centre the projection and bleed the road, sky and scenery sideways.

Drawing helpers make the two cases explicit:

| Helper | Coordinates | Use |
| --- | --- | --- |
| `Rect` | 320x240 logical | Everything inside the authored picture |
| `CanvasRect` | canvas pixels | Bands that must start at the screen edge |
| `FullRect` | logical y/height | A band that runs edge to edge (siren strip, HUD backing) |
| `TileImage` | logical, tiled | Menu artwork and skylines that continue across the margins |
| `Image` | logical | Ordinary baked textures |

Hit testing mirrors this: `ToVirtual` undoes MonoGame's touch display scale
(`TouchMapping.UndoTouchScale`) and then maps either through `FullBleedToLogical` (phones) or
`ToLogical` (letterboxed windows).

## Road renderer

`RoadRenderer` is a classic segment-based pseudo-3D projection. `Road` holds 1400 segments of
200 world units each, each carrying a curve and an elevation, plus roadside props.

| Constant | Value | Meaning |
| --- | --- | --- |
| `CameraDepth` | 0.84 | Projection depth factor |
| `RoadWidth` | 2000 | Road width in world units |
| `CameraHeight` | 1000 | Camera height above the road |
| `FocalX` | 640 | Horizontal focal length (half the 1280 picture) |
| `HalfHeight` | 480 | Vertical projection factor |
| `Horizon` | 368 | Horizon line in target pixels |
| `DrawDistance` | 150 | Segments drawn ahead |

Projection is the familiar `scale = CameraDepth / dz`, `x = centre + scale * offset * FocalX`,
`y = Horizon - scale * (height - cameraY) * HalfHeight`. Rendering walks from the camera to the
draw distance, clips each band against the previous one, and fills from the horizon upwards so
hills and curves hide what is behind them. Fog is applied per segment.

Sprites (traffic, props, the suspect, markers) are projected with the same maths, sorted far to
near, clipped against the stored road band for their segment, and culled when they would be too
large or too close. The sky, sun and two parallax skyline layers are drawn first; the skyline
drifts with the accumulated curve.

## Procedural art and type

`Art` bakes every texture at startup using the tiny software `Canvas` in `Art.cs`: the player car
in three steering frames, six suspect colours, traffic and oncoming cars, the HQ operator
animation frames, the badge, splash art, shadows, and per-theme palettes, suns, rain and two
skyline layers. `Canvas.DefaultScale` is set to `RenderSettings.Scale`, so the baked art is
authored in logical pixels and rasterised at 4x. Textures are cached by key and disposed with
the game; nothing is loaded from disk.

`GameFont` rasterises the embedded Chakra Petch TTF through FontStashSharp into a 2048x2048
atlas. Sizes are logical (10px steps) and baked per scale, so headings and HUD text stay smooth
without shipping bitmap fonts. `tools/gen_icons.py` bakes the *icon* artwork separately (see
`docs/tools.md`); the in-game logo, splash art and sprites stay in C#.

## Simulation

`Race` is plain C# with no MonoGame types, which is what makes the xunit suite possible. It owns
the player car, the suspect, traffic spawning, timing, ramming, turbo, damage and scoring.

| Constant | Value |
| --- | --- |
| `MaxSpeed` | 12000 |
| `TurboMaxSpeed` | 15800 |
| `RoadHalfWidth` | 1.0 |
| `OffRoadLimit` | 1.03 |
| `RamRange` | 950 |
| `RamLateral` | 0.44 |
| `CheckpointSpacing` | 200000 |

Each tick takes a `RaceInput` (steer, accelerate, brake, turbo, one-shot gear toggle) and emits
`RaceEvent`s: `Ram`, `NearMiss`, `TrafficHit`, `Checkpoint`, `Busted`, `TimedOut`, `TurboEmpty`,
`OffRoad`, `SuspectEscaping`, `Gunfire`, `GunfireWarning`, `GearChange`, `PlayerHit`, `Wrecked`,
`Repaired`. `ChaseGame` maps those to audio, HUD flashes, radio text and screen transitions, so
game feel can be tuned without touching the physics and the physics can be tested without a
graphics device.

The pursuit clock is a resource: it drains with time and speed, crashes cost seconds, and a
checkpoint repairs one hull plate and tops the clock up. Series II suspects fire when in range;
`WreckCause` records whether traffic or gunfire ended the run so the game-over card can say so.

`Mission` holds the nine pursuit definitions (theme, weather, length, base time, rams required,
suspect speed, traffic density, colour, series and whether the suspect is armed). `Road` is
regenerated per mission, difficulty and seed, so the same mission is a different stretch of road
each time. `DifficultyRules` is the single place skill level changes anything:

| Rule | Beginner | Medium | Hard |
| --- | --- | --- | --- |
| Score divisor | 1 | 2 | 5 |
| Suspect speed | 1.00x | 1.06x | 1.14x |
| Clock drain | 1.00x | 1.15x | 1.35x |
| Traffic spawn modifier | 1.00x | 1.15x | 1.25x |
| Base time factor | 1.00 | 0.92 | 0.84 |
| Catch-up assist | 0.30 | 0.16 | 0 |
| Hull points | 4 | 4 | 6 |
| Crash penalty | 2.5 s | 2.2 s | 1.8 s |

## Input

`ChaseGame` builds one `InputFrame` per tick from keyboard, gamepad and touch, then translates it
into a `RaceInput`. Mouse and touch both feed the same pointer fields, so the menus are driven by
one piece of hit-testing code on every platform. Touch controls appear over the race on phones:
steering pads, brake, turbo and pause, all laid out in the centred picture so they stay clear of
notches. On Android the activity hides the system bars and asks to draw into the cutout; on iOS
the UIKit scene owns the window.

## Audio

`ChipAudio` is a small software synth writing PCM into `SoundEffect`s at 22050 Hz: pulse,
triangle, saw and noise oscillators with ADSR, a note sequencer and five songs (title, chase,
game over, high score and busted, 92-158 BPM). The engine and siren are looping
`SoundEffectInstance`s whose pitch follows the simulation. Race loops are stopped and disposed
when a run ends: iOS caps concurrent voices, and leaving the engine and siren playing made the
game-over theme throw `InstancePlayLimitException` on the 14th voice.

## Persistence

`HighScoreStore` and `SettingsStore` keep JSON in the platform's local application data folder:

| Platform | Folder |
| --- | --- |
| macOS / iOS | `~/Library/Application Support/PoliceChaser` |
| Windows | `%LOCALAPPDATA%\PoliceChaser` |
| Android | app-private storage |

Files are `policechaser.scores.json` and `policechaser.settings.json`. Writes go to a temporary
file and are then moved into place, so a crash mid-save cannot truncate the table, and a corrupt
or missing file starts a clean table instead of throwing. Serialisation uses a source-generated
`JsonSerializerContext`, which keeps the trimmed/AOT iOS build working. Both stores implement
small file interfaces (`IHighScoreFile`, `ISettingsFile`) so tests and self-tests use in-memory
versions and never touch the player's real data.

## Self-test harness

`EnableSelfTest(screen, frames, shotPath)` puts the game into a deterministic mode: it swaps in
memory-backed stores, shows one screen, simulates a fixed number of frames, writes one PNG and
exits. The desktop host exposes it as `--selftest <screen> --frames N --shot path`; the iOS host
reads the `PC_SELFTEST` environment variable, which `simctl` can pass through. See
`docs/tools.md` for the screen list and the touch audit.

## Where to change things

| Task | Files |
| --- | --- |
| Add or retune a mission | `Game/Mission.cs` (definitions), `Graphics/ThemePalette.cs` and `Graphics/Art.cs` (look) |
| Change handling, ramming or scoring | `Game/Race.cs`, `Game/Difficulty.cs` |
| Change road shape or scenery density | `Game/Road.cs`, `Graphics/RoadRenderer.cs` |
| Move or restyle HUD and menus | `ChaseGame.cs` (`Draw*` methods and the drawing helpers above) |
| Add a sound or song | `Audio/ChipAudio.cs` |
| Change the app icon or splash art | `tools/gen_icons.py` |
| Change package identifiers or versions | the platform `.csproj` files, `Info.plist`, `AndroidManifest.xml`, `windows/Package.appxmanifest` |
