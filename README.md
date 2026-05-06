# Ghost Rehearsal

Ghost Rehearsal is a Kerbal Space Program 1 plugin that records a previous flight and lets you rehearse against it. Think "ghost car" from a racing game, but for launches, landings, and messy Kerbal learning loops.

This first version records telemetry once per second, saves it per vessel, draws the previous run as a yellow trail, and shows live deltas for mission time, altitude, speed, and fuel.

## Features

- Automatic flight telemetry recording.
- Previous-run replay per vessel name.
- Flight-scene UI with recording and ghost controls.
- World-space ghost trail using the active vessel's current reference body.
- Event markers for stage changes, touchdown/splashdown, and crash/destruction when KSP exposes the event.
- Saves runs to `GameData/GhostRehearsal/Runs`.

## Build

Install the KSP 1 assemblies locally, then build with MSBuild:

```powershell
$env:KSP_ROOT = "C:\Games\Kerbal Space Program"
msbuild .\GhostRehearsal.csproj /p:Configuration=Release
```

This project targets .NET Framework 4.0 because modern KSP 1 releases use Unity's newer split assemblies. The built DLL is created at:

```text
bin\Release\GhostRehearsal.dll
```

If `KSP_ROOT` points at a valid KSP install, the build copies the DLL to:

```text
GameData\GhostRehearsal\Plugins\GhostRehearsal.dll
```

You can also pass the path directly:

```powershell
msbuild .\GhostRehearsal.csproj /p:Configuration=Release /p:KspRoot="C:\Games\Kerbal Space Program"
```

## Install

Copy this folder into your KSP install:

```text
GhostRehearsal\GameData\GhostRehearsal
```

Then copy the compiled DLL into:

```text
Kerbal Space Program\GameData\GhostRehearsal\Plugins
```

## Use

1. Start a flight.
2. Open the `Ghost Rehearsal` window.
3. Leave `Record` on during an attempt.
4. Revert or launch again with the same vessel name.
5. Press `Load Ghost` to compare against the saved run.

The ghost trail appears when your active vessel is around the same celestial body as the saved run.

## Notes

This is a practical MVP. The later "wow" version would add a translucent craft marker, map-view orbit projection, named mission slots, and cross-vessel ghost selection.
