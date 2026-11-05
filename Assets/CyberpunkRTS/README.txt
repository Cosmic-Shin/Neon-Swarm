CYBERPUNK RTS KIT (exported from Blender)
GOODLUCK BUSSENG!

1. Let Unity finish importing.
2. Menu: Tools > Cyberpunk RTS > 1. Setup Materials, Imports and Prefabs
   - creates neon materials, fixes FBX import settings (scale 1, Bake Axis Conversion),
     sets up the "Death" clip + Animator for the 6 heavy/elite units, makes icons Sprites,
     and builds prefabs (with UnitDeath) for every unit and building.
3. Menu: Tools > Cyberpunk RTS > 2. Build Test Scene, then press Play.
   WASD/arrows pan, Shift = fast, scroll zoom, click a unit/building to kill it, K = kill nearest.

Folders: Models/(Units, Buildings, Props, Environment, Projectiles, FX, Wrecks), Icons, Scripts, Editor.
Glow: the Post Processing package (3.5.1) is installed. Build Test Scene adds neon bloom automatically;
for any other scene use Tools > Cyberpunk RTS > 3. Add Neon Bloom to Open Scene. Tune it in PostFX/NeonBloom.asset.

ECONOMY (C&C style)
- Ore_DataCrystal: mineable ore, shrinks Full>Half>Low>Depleted, regrows when left alone.
- DataHarvester: auto loop ore -> mine -> refinery -> unload -> credits. OrderHarvest(ore) to direct it.
- DataRefinery: dock pad + queue. ResourceBank holds credits (Add / Spend / OnCreditsChanged).
- Menu: Tools > Cyberpunk RTS > 4. Build Economy Demo Scene.

DERELICT OUTPOST (neutral capture point)
- Starts ruined. Units of one team in the zone capture it (contested = paused).
- Captured: restores itself, glows in team colour, held for 180 s (refreshed while garrisoned), then decays to ruins.
- Up to 4 units garrison inside: outpost.TryGarrison(unit), Eject / EjectAll. Units need a TeamMember (team 0 = Hive, 1 = Aegis).
- Menu: Tools > Cyberpunk RTS > 5. Build Outpost Demo Scene.
