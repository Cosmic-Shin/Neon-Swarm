# Aegis Corp models (Neon Swarm, second faction)

Units: Models/Aegis/Units/*.fbx (animated, one timeline, 24 fps; each file's timeline runs from frame 1 to its last clip, e.g. Bastion 640, Carrier 530). Buildings: Models/Aegis/Buildings. Wrecks: Models/Aegis/Wrecks. Icons: Icons/Aegis.
Front is +Y in Blender (same as the Hive kit). Flyers keep their altitude on the root; animation is on the *_Body child.

## Clips (frame ranges, same layout as the Hive units)
| Clip | Frames | Loop | Units |
|---|---|---|---|
| Death | 1-40 | no | all units |
| Walk (move) | 101-125 | yes | all units |
| Idle | 201-249 | yes | all units |
| Harvest | 301-349 | yes | Collector |
| Unload | 401-449 | yes | Collector |
| Attack | 501-end | no | all except Collector (see table) |
| Deploy / Undeploy | 601-620 / 621-640 | no | Bastion (anchors to fire) |

## Attack clip end and Fire frame (put a "Fire" Animation Event here)
| Unit | End | Fire | Notes |
|---|---|---|---|
| Aegis_Peacekeeper | 514 | 503 | rifle burst, second kick at 508 |
| Aegis_Enforcer | 516 | 504 | flamer sweep left->right, flame on 504-514 |
| Aegis_Lancer | 514 | 503 | railgun, hull recoils |
| Aegis_MedicDrone | 516 | 506 | heal pulse from MED_BeamPoint |
| Aegis_Bastion | 516 | 503 | artillery shot from BAS_Muzzle (only when deployed) |
| Aegis_Paladin | 518 | 506 | arm cannon, PAL_Muzzle |
| Aegis_Interceptor | 512 | 503 | missiles from INT_MuzzleL/R |
| Aegis_Gunship | 516 | 505 | strafing dip, GUN_Muzzle1/-1 |
| Aegis_Carrier | 530 | 512 | bay opens 506-524, drop Peacekeepers at CAR_DropPoint |
| Aegis_Colossus | 520 | 510 | twin lances, COL_LanceMuzzleL/R |
| Aegis_Technician | 516 | 504 | repair arc / sidearm, TEC_ToolPoint |
| Aegis_AATrooper | 514 | 503 | twin flak tubes, AAT_Muzzle1 / AAT_Muzzle-1 |
| Aegis_StrikeBike | 512 | 502 | gun burst, BIKE_Muzzle (flash: FX_AegisBikeFlash) |
| Aegis_Marksman | 520 | 506 | sniper shot, MRK_Muzzle (projectile: Proj_AegisSniper) |
| Aegis_Suppressor | 518 | 506 | EMP pulse from SUP_PulsePoint, emitter rises and spins (Proj_AegisEMP) |
| Aegis_Watcher | 512 | 504 | scout drone, light zap from WAT_Muzzle |
| Aegis_PatrolDrone | 510 | 503 | Drone Bay drone, PD_Muzzle |
| Aegis_Dominion | 524 | 510 | sky fortress; wing turrets DOM_MuzzleL/R, lance at DOM_BeamPoint |
| Aegis_Breaker | 516 | 507 | brawler punch (lunges forward), contact point BRK_Muzzle on the right gauntlet |
| Aegis_Cadet | 510 | 503 | cheap rifle trooper, CDT_Muzzle (build in squads of 3) |
| Aegis_Hailstorm | 524 | 504 | rotary cannon, HAL_Barrels spin through the whole clip; fire repeatedly 504-520, HAL_Muzzle |
| Aegis_Warden | 516 | 505 | snare net launcher, WAR_Muzzle (Proj_AegisNet, then FX_AegisStasis on the ground) |
| Aegis_Sapper | 512 | 504 | breaching charge launcher, SAP_Muzzle (Proj_AegisCharge); squads of 3 |
| Aegis_Override | 514 | 505 | hack/stun bolt from OVR_Muzzle (Proj_AegisHackBolt); dish rises while firing |
| Aegis_Seeker | 508 | 503 | tiny attack drone, SEK_Muzzle; 5 per order; altitude 1.4 |
| Aegis_Hammerhead | 524 | 510 | bomber; bay doors open 505-518, drop Proj_AegisBomb at HAM_BombPoint; altitude 2.0 |
| Aegis_Mothership | 530 | 506 | drone carrier; hatches open 504-524, launch Seekers from MOM_LaunchPoint0-3; own gun MOM_Muzzle; altitude 2.4 |
| Aegis_Spotter | 510 | 503 | cheap scout flyer (Firefly Spotter role), marking shot from SPT_Muzzle; altitude 1.6; banks left/right in Walk |
| Aegis_Tribunal | 526 | 510 | titan; dips and shocks everything below from TRB_PulsePoint (FX_AegisShockField); tendrils sway in Idle/Walk; altitude 3.0 |

Aegis_Skylift (vehicle air transport) has no attack. It uses Unload 401-449: drops to the ground (body -0.35), clamp arms open 418-438, cargo at SKY_CargoPoint.
Flyers' altitude is on the root: Watcher 1.5, PatrolDrone 1.2, Skylift 2.2, Dominion 2.8.
Aiming: Suppressor and Dominion aim by turning the whole unit (SUP_Emitter and DOM_TurretL/R are used by the clips).

Aim pivots NOT animated by the clips (free for aiming code): LAN_TurretPivot, BAS_TurretPivot, ST_Head, FB_Head, OL_Yaw.

## Buildings
Animated buildings (one timeline, 24 fps):
| Building | Clips |
|---|---|
| Aegis_OrbitalLance | Idle 1-60 (dish sweeps, rings spin); Fire 100-200 (tilt up 100-125, beam from OL_BeamPoint ~125-150, return 175-200) |
| Aegis_FusionReactor | Idle 1-60 (core spins) |
| Aegis_Airfield | Idle 1-60 (radar spins) |
| Aegis_CommandSpire | Idle 1-60 (halo ring spins) |
| Aegis_TechCenter | Idle 1-60 (holo ring spins) |
| Aegis_ShieldProjector | Idle 1-60 (emitter bobs) |
| Aegis_MotorPool | Idle 1-60 (crane swings); Open 100-120 / Close 130-150 (MP_Door) |
| Aegis_BarracksBay | Open 100-120 / Close 130-150 (BB_Door) |
| Aegis_PulseGate | Closed at frame 1; Open 100-114 (PG_Field sinks), Close 130-144 (field rises) |
| Aegis_ArcTurret | Idle 1-60 (AT_CoilSpin turns, AT_Orb hums); Fire 100-112 (orb charges at 103, snaps at 104). Arc Coil role. AT_Head is the aim pivot (not animated), AT_Muzzle is the arc start. Mk2 = capacitor pylons, Mk3 = crown prongs (on AT_Head) |
| Aegis_SentinelTurret | Fire 100-112 (barrels recoil, left at 102, right at 106; ST_Head stays free for aiming) |
| Aegis_FlakBattery | Fire 100-112 (gun block kicks at 102 and 107; FB_Head stays free for aiming) |
| Aegis_SensorArray | Idle 1-60 (radar dish spins). Radar / minimap building, SA_ScanPoint |
| Aegis_RailBattery | Fire 100-120 (rails recoil). Heavy defence; RB_Yaw is the aim pivot, RB_Muzzle |
| Aegis_DroneBay | Open 100-120 / Close 130-150 (roof hatch); DB_LaunchPoint (drones leave), DB_SpawnPoint |
| Aegis_ResearchLab | Idle 1-60 (holo atom spins) |
| Aegis_ExtractionRig | Idle 1-60 (piston pumps). Second resource building; ER_DockPoint |
Static: Refinery, SentinelTurret, FlakBattery, PulseWall, PulsePost (wall corner/end post), MineField (7 mines, MF_Mine0-6, so you can remove them one by one).
Points: RF_DockPoint, BB_SpawnPoint, MP_SpawnPoint, AF_LandPoint, SP_DomeCenter, PAL_ShieldDome.

## Upgrade looks (same system as the Hive, BuildingUpgrade.cs)
Hidden-by-default child groups named `<Building>_Upgrade_Mk2` / `_Mk3` (they are visible in the FBX; the script hides them until the upgrade is bought):
- Mk2 + Mk3: CommandSpire, Airfield
- Mk2: Refinery, BarracksBay, FusionReactor, TechCenter, SentinelTurret (parented to ST_Head so it turns with the gun), ShieldProjector, SensorArray, DroneBay, ResearchLab, ExtractionRig, RailBattery (parented to RB_Yaw)

## Team colour
Trims that should show the owner's colour use the material **Aegis_TeamColor** (white, emissive). In Unity, tint it per player with a MaterialPropertyBlock (_Color and _EmissionColor) on renderers whose material name starts with Aegis_TeamColor. Everything else keeps the faction's red/amber neon.

Wrecks: Wreck_Aegis_<Name>.fbx for every building except MineField, plus Paladin, Colossus, Bastion, Lancer, Technician, AATrooper, StrikeBike, Suppressor, Dominion, Breaker, Hailstorm, Override, Hammerhead, Mothership and Tribunal.

## Extra FX / projectiles
- FX_AegisRepair: ARep_Arc (1 unit long along +Y, scale to distance), ARep_Sparks (place at target), ARep_Ring (ground ring under the repaired thing).
- FX_AegisBikeFlash: twin muzzle flash for the Strike Bike.
- Proj_AegisEMP, Proj_AegisSniper.

## Aegis-territory props (Models/Aegis/Props, static)
Prop_Aegis_StreetTile (2x2, same size as Tile_Plaza), Prop_Aegis_Barrier, Prop_Aegis_HoloAdPillar, Prop_Aegis_Streetlight, Prop_Aegis_Planter, Prop_Aegis_Checkpoint (PCK_Boom can be rotated to open), Prop_Aegis_Crates, Prop_Aegis_LandingPad.

## Damage states (mid-health look)
Every Aegis building except MineField has a hidden-by-default damage group, same idea as the upgrade groups:
- `<Building>_Damage` (child of the root) holds scorch marks, torn panels, dangling wires and rubble.
- Pieces that sit on moving parts live in extra groups named `<Building>_Damage_<Part>` (e.g. `Aegis_RailBattery_Damage_RB_Yaw`, `Aegis_BarracksBay_Damage_BB_Door`) so they move with that part.
- Show every transform whose name contains `_Damage` when health < ~50%, hide it otherwise.
- Particle spawn points: `<Building>_SmokePoint<n>` (roof, use for smoke/fire) and `<Building>_SparkPoint<n>` (end of the wires, use for sparks).
- Particle meshes: FX/FX_AegisDamage.fbx (ADmg_Puff0-2 smoke, ADmg_Spark0-1, ADmg_Flame, ADmg_Debris0-2).

## Large construction kit (Construction/)
- Build_AegisPadLarge: 4.5 x 4.5 foundation for Command Spire / Airfield / Refinery size buildings.
- Build_AegisScaffoldLarge: 3.8 x 3.8 x 5.5 frame; scale Y (Unity) to the building height.
- Build_AegisCrane: animated, loop 1-120 (jib slews, trolley runs, hook lifts a load). Put one or two beside big build sites.

## Neutral corporate map pieces (Models/Environment)
Neutral_Corp_CapturePoint (Idle 1-60 holo spins; NCP_CapturePoint), Neutral_Corp_RepairBay (Idle 1-60 arms; NRB_RepairPoint), Neutral_Corp_DataVault (Idle 1-60 cores spin), Neutral_Corp_Outpost (Idle 1-60 sensor spins).
Owner banners/strips use Aegis_TeamColor: tint grey when neutral, owner colour when captured.

## Portraits
UI/Portraits/Aegis/portrait_<Unit>.png and UI/Portraits/Hive/portrait_<Unit>.png (256 px, transparent, idle pose).

## Aegis UI skin + emblem
- Panels/buttons: UI/Resources/HoloUI/Aegis/ (same file names and 9-slice borders as HoloUI/, gold + red palette).
- Cursors: UI/Resources/HoloCursors/Aegis/ (same names and hotspots).
- Emblem: UI/Resources/HoloUI/Aegis_Emblem.png, UI/Faction/Aegis_Emblem_<size>.png (+ _Glass versions), UI/Faction/Aegis_Lockup.png (emblem + wordmark), SVG in UI/Faction~/.

## Main menu diorama
UI/MenuDiorama~/MenuDiorama_layout.json is now v2 (Aegis force DA_* on the right facing the swarm, new camera). v1 kept as MenuDiorama_layout_v1_hive_only.json.

## Role fill-ins (Aegis counterparts for Hive units)
Chromejaw -> Breaker, Ratline -> Sapper, Hackling -> Cadet, Locust Swarm -> Seeker, Overclock -> Hailstorm, Netrunner Hive -> Override, Wire Weaver -> Warden, Beetle Bomber -> Hammerhead, Locust Carrier -> Mothership, Jellyfish Dreadnought -> Tribunal.
New FX/projectiles: Proj_AegisBomb, Proj_AegisNet, Proj_AegisCharge, Proj_AegisHackBolt, FX_AegisStasis (slow field decal), FX_AegisShockField (scale to the Tribunal's splash radius).

## FX added for Claude Code's list
- FX_AegisArc: electric arc 1 unit long along +Z in Unity (+Y in Blender), starting at the origin; scale Z to the target distance. Parts AArc_Glow/Main/Core/Fork0-1, flares at each end. Replaces Bolt_Chain (Tribunal tendrils, Arc Turret).
- FX_AegisFlame: already existed (Flame_Outer/Mid/Core/Puff0-3, points along +Z in Unity). Use it instead of FX_Flame for the Enforcer.
- FX_AegisImpact: Impact_ThinRing, Impact_SoftRing, Impact_DashedRing, Impact_PulseWall (+Rim). Same roles as Shock_Thin / Shock_Soft / Shock_Pulse.
- FX_AegisPunch: Punch_Star, Punch_Core, Punch_Ring, Punch_Shard0-7, facing +Z in Unity. Replaces Flash_Heavy for the Breaker.
- FX_AegisScorch: Scorch_Round(+Embers), Scorch_Splat, Scorch_BurnStreak(+Embers), Scorch_Crater(+Embers, +Pit). Same names and layout as FX_Scorch, with red/amber embers.

## Extra wrecks
Wreck_Aegis_Collector, Gunship, Interceptor, Skylift, Carrier, ArcTurret.
