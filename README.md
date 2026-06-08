# ExileStats

An [ExileCore2](https://github.com/ExileApi) HUD plugin for **Path of Exile 2** that tracks live mapping statistics in a compact, draggable overlay panel.

## Features

- **Map timer** (HH:MM:SS) — runs in any hostile area, including campaign zones.
- **Per-map stats** — kills, kills/min, and XP/hour, measured from a settled baseline to avoid memory-lag spikes on area change.
- **Session stats** — maps completed, maps/hour, and deaths.
- **Endgame map detection** — only counts real Waystone maps (Act 10, area level ≥ 65) by unique instance hash, so re-entering the same map doesn't double-count.
- **Minimised / expanded views** — click the toggle button to show only the timer or the full stats panel.
- **Auto-collapse** — minimises automatically while the inventory panel is open, then restores your previous view.
- Fully configurable panel position, scale, letter-spacing, colour, and per-field offsets via the settings menu.

## Installation

1. Build the project against your ExileCore2 install (set the `exileCore2Package` MSBuild property to your ExileCore2 binaries folder).
2. Copy the build output (DLL + `images/` folder) into your ExileCore2 `Plugins/Compiled/ExileStats` directory, or drop the source folder into `Plugins/Source`.
3. Enable **ExileStats** in the ExileCore2 plugin list.

## Build

```
dotnet build ExileStats.csproj -c Release
```

Requires .NET 8 (`net8.0-windows`) and references `ExileCore2.dll` and `GameOffsets2.dll`.

## License

MIT
