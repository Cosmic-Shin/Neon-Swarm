# Neon Swarm cursors ("C · Anime Shard")

Files: `Assets/CyberpunkRTS/UI/Resources/HoloCursors/Cursor_<Kind>_<32|48|64>.png` (+ hotspots.json).
Script: `Assets/CyberpunkRTS/Scripts/CursorManager.cs` (auto-creates itself; `CursorManager.Set(CursorKind.X)`).
Importer: `Assets/CyberpunkRTS/Editor/HoloCursorImporter.cs` (Texture Type = Cursor; menu Tools > Cyberpunk RTS > Reimport Cursors).

| Kind | When |
|---|---|
| Default | UI, nothing under the mouse, nothing selected |
| Select | Hovering your own unit/building (selectable) |
| Move | Units selected, hovering walkable ground |
| Attack | Units selected, hovering an enemy (or force-attack key held) |
| Invalid | Order impossible there (unwalkable, out of map, can't attack air, blocked placement) |
| Build | Placing a building (valid spot) |
| Repair | Repair mode, hovering a damaged own building |
| Sell | Sell mode, hovering an own building |
| Harvest | Harvester selected, hovering an ore node |
| Rally | Production building selected, setting a rally point |
| Capture | Capture-capable unit selected, hovering the Derelict Outpost |
| Load | Transport-capable target (Wasp dropship / Swarm carrier) under mouse with infantry selected |
| Scroll* | Mouse at the screen edge (8 directions; use CursorManager.SetScroll(dir)) |

Hotspot: shard tip (top-left) for all action cursors; arrow tip for scroll cursors.
Size: auto (32 below 1440p, 48 at 1440p+, 64 at 4K) or player choice via CursorManager.SetSizePreference(0/32/48/64).
