# ExileStats — PoE2

Mapping and combat statistics HUD for ExileCore2: map timer, kills, XP/hour,
deaths, endgame-area entries/hour, and a decaying DPS gauge.

## Logic

1. Area changes start a session clock and classify hostile endgame areas using
   PoE2 area/level data.
2. `ExileStatsLogic` samples XP and Life on a throttle to calculate XP/hour and
   session deaths.
3. `DpsTracker` scans nearby hostile monsters, accumulates Life deltas, confirms
   kills, removes stale entities, and applies time decay. If the current host
   does not expose a monster bucket, the scan safely skips that interval.
4. `Tick` refreshes cached display strings and handles the plugin's own
   debounced expand/collapse button.
5. `Render` draws the bundled PNG panels, labels, and DPS fill gauge.

The plugin performs no network I/O or game automation.

Endgame-entry counting also handles a temporarily unavailable zero area hash;
the first qualifying endgame area is not lost because of the default hash value.
The HUD's map counter is an entry counter (`MapsEntered`), not a completion claim,
because the current host does not expose a reliable map-completed event.

## Status

Build: **PASS**. Classification: **CURRENT_WITH_WARNINGS**; live semantics and
multi-resolution placement remain unverified.

Detailed report: [PoE2 plugin catalog](../../README.md) ·
[audit](../../../docs/plugins/ExileStats/AUDIT.md).
