# Using Fuse as the fidelity reference

The 8-bit original is used **only as a reference** while tuning the reproduction. No data,
graphics, code or audio from the original is extracted into the game.

## Setup

Extract the 128K image supplied by the user (these stay outside the repository):

```bash
mkdir -p /tmp/chasehq && unzip -o ~/Downloads/ChaseH.Q.128.tap.zip -d /tmp/chasehq
open -a Fuse /tmp/chasehq/chase.tap
```

In Fuse: `Media > Tape` to confirm the tape is inserted, then press `Enter` at the 128K
menu to run the tape loader. `File > Save Screenshot As…` writes a 256x192 reference PNG.

Screenshots decoded from the tape's `SCREEN$` blocks are the quickest way to compare the
palette and HUD layout without waiting for the tape to load:

```bash
python3 tools/fuse_reference.py /tmp/chasehq/chase.tap /tmp/chasehq/screens
```

## What was checked against the original

| Original behaviour | Reproduction |
| --- | --- |
| Rear-view pursuit, suspect ahead, ram the car | `Race` / `RoadRenderer` |
| Suspect takes several hits, damage bar in HUD | `RivalHealth`, HUD pips |
| 100-unit clock, checkpoints extend the time | `TimeRemaining`, `CheckpointSpacing` |
| Curving road, hills, roadside scenery | `Road.Build`, scanline renderer |
| Traffic to weave through, off-road slows you | `Traffic`, `OffRoad` handling |
| Crashing into traffic costs speed, time and car damage | `TrafficHit`, hull bar, `Wrecked` |
| Stage number and location shown before and during the chase | Briefing header, `STAGE` HUD readout, countdown banner |
| Radio messages from dispatch | White HQ message bar under the HUD |
| HQ video link: dispatcher above, player below, in a fenced panel | Procedural `HqOperator` / `PlayerPortrait` in the HUD's right-hand frame, dispatcher frame flashes on new traffic |
| Top HUD: `SCORE / TIME / SPEED` over `GEAR / DISTANCE / STAGE` in a white frame | `SCORE / TIME / SPEED` over `STAGE / RANGE` in a white frame |
| Turbo boost | `Turbo` meter |
| Scoring with time bonus on a bust | `Race.RawScore` |

The original chases one suspect through a fixed route; this game runs six missions with
different routes/palettes, and the last three missions (`NEON GRID`, `DUNE RUNNER`,
`FROSTLINE`) are new content that was never in the source game.
