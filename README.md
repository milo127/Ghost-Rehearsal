# Ghost Rehearsal

Ghost Rehearsal is a Kerbal Space Program 1 plugin for practicing launches, landings, and repeatable maneuvers against a replay of your previous attempts.

Record a flight, relaunch the same vessel, and fly alongside a visible ghost of the saved run. The ghost marker follows the previous trajectory in real time, with distance and speed deltas, event markers, named save slots, and a best-run comparison to help you improve without handing control to an autopilot.

## Features

- Records vessel telemetry during flight.
- Replays saved runs as a visible ghost craft marker.
- Smooth interpolation between recorded samples.
- Ghost distance and speed delta overlay.
- Named ghost slots per vessel.
- Best-run slot for comparing fuel and altitude.
- Event markers for staging, touchdown, splashdown, crash, and vessel destruction.
- Map-view ghost trail around the active celestial body.
- Saves ghost runs to `GameData/GhostRehearsal/Runs`.

## Requirements

- Kerbal Space Program 1.
- Tested against a modern KSP 1 install using Unity split assemblies.

## Installation

Download the release zip and merge its `GameData` folder into your Kerbal Space Program folder.

The installed layout should look like this:

```text
Kerbal Space Program/
  GameData/
    GhostRehearsal/
      GhostRehearsal.version
      Plugins/
        GhostRehearsal.dll
```

## Usage

1. Launch a vessel.
2. Open the `Ghost Rehearsal` window. Press `F8` to show or hide it.
3. Choose a slot name, or use `default`.
4. Leave `Record` enabled while flying.
5. Press `Save` to save the current run to the selected slot.
6. Relaunch the same vessel.
7. Select the same slot and press `Load Ghost`.
8. Fly against the ghost marker and use the overlay deltas to compare your attempt.

To save a benchmark attempt, press `Save Best`. Use the `Best` button to switch to that slot later.

