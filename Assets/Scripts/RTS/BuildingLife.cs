using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using Random = UnityEngine.Random;

public class BuildingLife : MonoBehaviour
{
    static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
    static readonly Color Cyan = new(0.25f, 1f, 1f), Magenta = new(1f, 0.2f, 0.8f), Red = new(1f, 0.15f, 0.2f), Amber = new(1f, 0.65f, 0.15f);

    readonly List<Action<float>> ticks = new();
    readonly List<Action<float>> glowTicks = new();
    Transform[] parts;
    Structure s;
    Renderer eye;
    Color team;
    MaterialPropertyBlock block;

    bool Powered => s == null || s.power >= 0 || !Net.LowPower(s.Health.team);
    float Pace => Powered ? 1f : 0.2f;

    void Start()
    {
        s = GetComponent<Structure>();
        parts = GetComponentsInChildren<Transform>(true);
        block = new MaterialPropertyBlock();
        team = ProductionFX.HoloColor(gameObject);
        float biggest = 0f;
        foreach (var r in GetComponentsInChildren<MeshRenderer>())
        {
            float size = r.bounds.size.sqrMagnitude;
            if (size > biggest) { biggest = size; eye = r; }
        }
        Setup();
        Common();
    }

    void Update()
    {
        if (s != null && (s.Health.IsDead || !s.Health.enabled)) { enabled = false; return; }
        if (eye != null && !eye.isVisible) return;
        if (!Juice.Seen(transform.position)) return;
        float dt = Time.deltaTime;
        for (int i = 0; i < ticks.Count; i++) ticks[i](dt);
        if (Powered) for (int i = 0; i < glowTicks.Count; i++) glowTicks[i](dt);
    }

    void OnDisable() { foreach (var z in zaps) if (z.lr != null) z.lr.enabled = false; }


    void Setup()
    {
        var building = GetComponent<Building>();
        Func<float> working = () => building != null && building.Producing >= 0 ? 1f : 0.25f;
        var turret = GetComponent<Turret>();

        // Aegis first: their prefabs were cloned from Hive templates and can keep template parts (Aegis_DroneBay has Hangar_Pad)
        if (P("CS_TowerCore") != null) AegisSpire();
        else if (P("FR_Core") != null) AegisReactor();
        else if (P("RF_DockGlow") != null) AegisRefinery();
        else if (P("ER_WellGlow") != null) AegisRig();
        else if (P("RL_AtomCore") != null) AegisLab(working);
        else if (P("TC_EntryLight") != null) AegisTech(working);
        else if (P("SA_Dish") != null) AegisSensor();
        else if (P("SP_Emitter") != null) AegisShield();
        else if (P("OL_FeedCore") != null) AegisLance();
        else if (P("ST_Eye") != null) AegisSentinel(turret);
        else if (P("FB_Ring") != null) AegisFlak(turret);
        else if (P("RB_Coil0") != null) AegisRail(turret);
        else if (P("PG_Field") != null || P("PW_Field") != null || P("PP_Light0") != null) AegisWall();
        else if (P("MF_Eye0") != null) AegisMines();
        else if (P("DB_PadRing") != null) { var bay = GetComponent<DroneHangar>(); AegisFactory(bay != null ? () => bay.Busy ? 1f : 0.25f : working, "DB"); }
        else if (P("AF_PadRing") != null) AegisFactory(working, "AF");
        else if (P("BB_DoorLight") != null) AegisFactory(working, "BB");
        else if (P("MP_DoorLight") != null) AegisFactory(working, "MP");
        else if (P("Arc_Orb") != null) ArcCoil(turret);
        else if (P("NeonTurret_Head") != null) NeonTurret(turret);
        else if (P("FlakLantern_Head") != null) Flak(turret);
        else if (P("RailspikeTower_Head") != null) Railspike(turret);
        else if (P("OverwatchSpire_Nest") != null) Overwatch();
        else if (P("UplinkTower_Radar") != null) Uplink();
        else if (P("Pylon_Orb") != null) Pylon();
        else if (P("Nexus_EnergyCore") != null) Nexus();
        else if (P("Ref_Funnel") != null) Refinery();
        else if (P("Siphon_FissureCore") != null) Siphon();
        else if (P("Field_NodeBase") != null) MineField();
        else if (P("Gate_EnergyField") != null) Firewall();
        else if (P("Cloak_GlitchCore") != null) Cloak();
        else if (P("Lab_Dome") != null) Lab(working);
        else if (P("Hangar_Pad") != null) { var bay = GetComponent<DroneHangar>(); Hangar(bay != null ? () => bay.Busy ? 1f : 0.25f : working); }
        else if (P("Hive_Hall") != null) Hive(working);
        else if (P("Aerie_Base") != null) Aerie();
    }

    // ---------------- Aegis Corp: amber running lights, team stripes, clean corporate pulses ----------------
    // Idle clips already move dishes/rings/pistons, so these recipes only drive lights and effects.

    void AegisCommon(string prefix)
    {
        Blink($@"^{prefix}2?_(Beacon|BeaconLight|TowerBeacon|AntennaTip.*)$", 0.9f);
        Glow($@"^{prefix}_(FrontStrip|TeamStripe|TeamStripeB)$", () => 0.8f + 0.2f * Mathf.Sin(Time.time * 1.6f));
    }

    void AegisFactory(Func<float> working, string p)
    {
        AegisCommon(p);
        Glow($@"^{p}_(DoorLight|PadRing)$|^{p}2_(Pad2Ring|HangarLight|RoofLight|TowerLight)$", () => 0.5f + 1.8f * working() * (0.8f + 0.2f * Mathf.Sin(Time.time * 6f)));
        Chase($@"^({p}_SideLight[LR]|{p}2_ArmorLight-?1|{p}_CraneLight)$", 3f);
        Every(0.08f, 0.22f, () => working() > 0.5f, () =>
        {
            var at = P($"{p}_SpawnPoint") ?? P($"{p}_LaunchPoint") ?? P($"{p}_LandPoint");
            if (at == null) return;
            Vector3 pos = at.position + Vector3.up * Random.Range(0.5f, 2f) + Random.insideUnitSphere * 0.8f;
            Juice.Burst(pos, new Color(1f, 0.75f, 0.35f), 3, 4f, 0.07f, 0.3f, 0.6f, true);
        });
        if (p == "AF") Every(3f, 3f, () => true, () => { var pad = P("AF_PadRing"); if (pad != null) Juice.Ring(pad.position + Vector3.up * 0.1f, team, 1f, 5f, 0.8f, 0.15f); });
        if (p == "DB" && GetComponent<DroneHangar>() is DroneHangar bay)
        {
            Glow(@"^DB_SideLight[LR]$", () => bay.Target != null ? (Mathf.Repeat(Time.time * 4f, 1f) < 0.5f ? 3f : 0.2f) : 0.8f);
            float launch = bay.LastLaunch;
            ticks.Add(dt =>
            {
                if (bay.LastLaunch == launch) return;
                launch = bay.LastLaunch;
                Vector3 at = (P("DB_LaunchPoint") ?? transform).position;
                Juice.Flash(at, team, 6f, 12f, 0.2f);
                Juice.Ring(at, team, 0.5f, 5f, 0.4f, 0.25f);
                Kick(-0.4f);
            });
        }
    }

    void AegisSpire()
    {
        AegisCommon("CS");
        Glow(@"^CS_(TowerCore|GateGlow)$", () => 0.6f + 1.6f * Heartbeat(0.9f));
        Chase(@"^CS_AntennaTip\d$", 2.5f);
        Glow(@"^CS3_HaloGlow$", () => 0.7f + 0.5f * Mathf.Sin(Time.time * 2f));
        Chase(@"^CS3_Emitter\d$", 4f);
        Blink(@"^CS2_DishTip-?1$", 1.3f);
        Motes(P("CS_TowerCore"), team, -0.5f, 8f);
        var tips = Ps(@"^CS3_Emitter\d$");
        Every(0.6f, 1.6f, () => tips.Count > 0, () => Zap(tips[Random.Range(0, tips.Count)].position, P("CS3_HaloGlow") != null ? P("CS3_HaloGlow").position : P("CS_TowerCore").position, team, 0.12f));
    }

    void AegisReactor()
    {
        AegisCommon("FR");
        Glow(@"^FR_(Core|CoreRingA|CoreRingB)$|^FR2_CoreRingC$", () => 0.8f + 1.2f * Heartbeat(1.2f));
        Chase(@"^FR_CoolantLight\d$", 5f);
        Chase(@"^FR2_TowerRing\d$", 3f);
        var coolant = Ps(@"^FR_CoolantLight\d$");
        Every(0.3f, 1.1f, () => coolant.Count > 0, () => Zap(P("FR_Core").position, coolant[Random.Range(0, coolant.Count)].position, new Color(0.4f, 0.9f, 1f), 0.1f));
        Motes(P("FR_Core"), new Color(0.5f, 0.9f, 1f), -0.7f, 12f);
    }

    void AegisRefinery()
    {
        AegisCommon("RF");
        var refinery = GetComponent<Refinery>();
        Func<float> busy = () => refinery != null && refinery.Busy ? 1f : 0.15f;
        Glow(@"^RF_(DockGlow|DockStripe)$", () => 0.4f + 2.5f * busy() * (0.7f + 0.3f * Mathf.Sin(Time.time * 12f)));
        Glow(@"^RF2?_TankGauge\d?$", () => 0.5f + 1.5f * busy());
        Chase(@"^RF2?_TankBand\d?$", 4f);
        Glow(@"^RF2?_StackRing$", () => 0.5f + 2f * busy());
        Smoke(P("RF_StackRing"), () => busy() > 0.5f, 0.14f);
        Smoke(P("RF2_StackRing"), () => busy() > 0.5f, 0.14f);
        Every(0.06f, 0.12f, () => busy() > 0.5f, () =>
        {
            var d = P("RF_DockPoint");
            if (d != null) Juice.Burst(d.position + Vector3.up * 0.6f, Random.value < 0.5f ? new Color(1f, 0.7f, 0.3f) : team, 3, 3f, 0.1f, 0.4f, 0.8f, true);
        });
    }

    void AegisRig()
    {
        AegisCommon("ER");
        var siphon = GetComponent<Siphon>();
        Func<float> fill = () => siphon != null ? siphon.Progress01 : Mathf.Repeat(Time.time / 4f, 1f);
        Glow(@"^ER_(WellGlow|HeadGlow|CrownLight)$", () => 0.7f + 0.3f * Mathf.Sin(Time.time * 5f) + 3f * Flare(() => siphon != null ? siphon.LastPayout : -99f, 0.5f));
        Glow(@"^ER2?_TankGauge\d$|^ER2_TankBand\d$", () => 0.2f + 2.5f * fill() * fill());
        Motes(P("ER_WellGlow"), Juice.Gold, -0.8f, 12f);
        Smoke(P("Aegis_ExtractionRig_SmokePoint6"), () => Random.value < 0.15f, 0.6f);
    }

    void AegisLab(Func<float> working)
    {
        AegisCommon("RL");
        Glow(@"^RL_(AtomCore|Orbit\d)$|^RL2_HaloGlow$", () => 0.4f + 1.8f * working() * (0.8f + 0.2f * Mathf.Sin(Time.time * 7f)));
        Chase(@"^RL_Window\d$", 6f);
        Blink(@"^RL_DoorLight$", 0.7f);
        foreach (var tank in Ps(@"^RL_Tank\d$")) Bubbles(tank, () => working() > 0.5f);
        Motes(P("RL_AtomCore"), team, -0.2f, 6f);
    }

    void AegisTech(Func<float> working)
    {
        AegisCommon("TC");
        Glow(@"^TC2_HoloRingGlow$|^TC_EntryLight$", () => 0.5f + 1.5f * working() * (0.8f + 0.2f * Mathf.Sin(Time.time * 5f)));
        Glitch(@"^TC2_HoloRing$");
        Blink(@"^TC2_Dish$", 0.5f);
    }

    void AegisSensor()
    {
        AegisCommon("SA");
        Glitch(@"^SA_Screen-?1$");
        Chase(@"^SA2?_MastRing\d$", 3f);
        Glow(@"^SA_(DishInner|DishRim)$", () => 0.6f + 0.8f * Mathf.Abs(Mathf.Sin(Time.time * 2.2f)));
        Every(2.8f, 2.8f, () => true, () => Juice.Ring(new Vector3(transform.position.x, 0.2f, transform.position.z), team, 2f, 28f, 1.6f, 0.12f));
        Motes(P("SA_ScanPoint"), team, -0.6f, 5f);
    }

    void AegisShield()
    {
        AegisCommon("SP");
        var field = GetComponent<ShieldField>();
        Func<float> on = () => field == null || field.Online ? 1f : 0.2f;
        Chase(@"^SP_Coil\d$", 5f);
        Glow(@"^SP_(Emitter|CradleRing|CradleLight\d)$|^SP2_(HaloGlow|HaloRing)$", () => on() * (0.7f + 0.6f * Mathf.Abs(Mathf.Sin(Time.time * 1.7f))));
        var coils = Ps(@"^SP_Coil\d$");
        Every(0.35f, 1f, () => coils.Count > 0 && on() > 0.5f, () => Zap(P("SP_Emitter").position, coils[Random.Range(0, coils.Count)].position, new Color(0.45f, 0.85f, 1f), 0.1f));
        Motes(P("SP_Emitter"), new Color(0.45f, 0.85f, 1f), -0.4f, 8f);
    }

    void AegisLance()
    {
        var silo = GetComponent<SwarmSilo>();
        Func<float> charge = () => silo != null ? silo.Charge01 : 0.5f;
        var lights = Ps(@"^OL_BaseLight\d$");
        lights.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        glowTicks.Add(dt =>   // base lights fill up as the lance charges, all blink when ready
        {
            float c = charge();
            bool ready = silo != null && silo.Ready;
            for (int i = 0; i < lights.Count; i++)
                SetGlow(lights[i], ready ? (Mathf.Repeat(Time.time * 2f, 1f) < 0.5f ? 3f : 0.4f) : (i < c * lights.Count ? 1.6f : 0.15f));
        });
        Glow(@"^OL_(FeedCore|DishInner|ChargeRingA|ChargeRingB|DishCone)$", () => 0.3f + 2.5f * charge() * charge() * (0.85f + 0.15f * Mathf.Sin(Time.time * (4f + 10f * charge()))));
        Every(0.1f, 0.5f, () => charge() > 0.6f, () =>
        {
            var core = P("OL_FeedCore"); var tip = P("OL_BeamPoint");
            if (core != null && tip != null) Zap(core.position, tip.position, new Color(1f, 0.8f, 0.4f), 0.08f);
        });
        Motes(P("OL_FeedCore"), new Color(1f, 0.75f, 0.35f), -0.6f, 10f);
    }

    void AegisSentinel(Turret t)
    {
        Glow(@"^ST_Eye$", () => t != null && t.Aim != null ? (Mathf.Repeat(Time.time * 6f, 1f) < 0.5f ? 3f : 0.4f) : 1f);
        Glow(@"^ST_MuzzleGlow-?1$", () => 0.3f + 1.2f * Charge(t) + 4f * Flare(t, 0.15f));
        OnShot(t, () => { foreach (var m in Ps(@"^ST2?_Muzzle-?1$")) if (m.gameObject.activeInHierarchy) Juice.Burst(m.position, new Color(1f, 0.8f, 0.4f), 4, 4f, 0.06f, 0.2f); });
    }

    void AegisFlak(Turret t)
    {
        Glow(@"^FB_Ring$", () => 0.5f + Charge(t) + 3f * Flare(t, 0.25f));
        OnShot(t, () =>
        {
            var m = P("FB_Muzzle");
            if (m == null) return;
            Juice.Flash(m.position, new Color(1f, 0.7f, 0.3f), 6f, 12f, 0.15f);
            Juice.Burst(m.position + Vector3.up, new Color(0.6f, 0.6f, 0.65f), 8, 3f, 0.35f, 0.8f, 1f);
        });
    }

    void AegisRail(Turret t)
    {
        AegisCommon("RB");
        Chase(@"^RB_Coil\d$", 2f);
        Glow(@"^RB_(Coil\d|ColumnRing)$|^RB2_(CellGlow-?\d+|DomeEye)$", () => 0.15f + 3f * Charge(t) * Charge(t) + 4f * Flare(t, 0.2f));
        var coils = Ps(@"^RB_Coil\d$");
        Every(0.05f, 0.15f, () => coils.Count >= 2 && t != null && t.Online && Charge(t) > 0.8f && t.Aim != null, () =>
            Zap(coils[Random.Range(0, coils.Count)].position, coils[Random.Range(0, coils.Count)].position, new Color(1f, 0.85f, 0.4f), 0.08f));
        OnShot(t, () =>
        {
            Vector3 at = (P("RB_Muzzle") ?? transform).position;
            Juice.Flash(at, new Color(1f, 0.9f, 0.4f), 14f, 20f, 0.3f);
            Juice.Ring(at, new Color(1f, 0.85f, 0.3f), 0.5f, 6f, 0.35f, 0.3f);
            Juice.Shake(at, 0.15f);
        });
    }

    void AegisWall()
    {
        Toggle(@"^(PG|PW)_Beam\d$", () => Random.value > 0.1f);
        Glow(@"^(PG|PW)_(Field|FieldSheet|Beam\d)$", () => 0.7f + 0.3f * Mathf.Sin(Time.time * 9f) + (Random.value < 0.02f ? 2f : 0f));
        Blink(@"^PG_Beacon-?1$", 1f);
        Glow(@"^(PG_(HeaderLight|TowerLight-?1)|PW_PostLight-?1)$", () => 0.8f + 0.2f * Mathf.Sin(Time.time * 2f));
        Chase(@"^PP_Light\d$", 2f);
    }

    void AegisMines()
    {
        var mine = GetComponent<EmpMine>();
        Func<float> charge = () => mine != null ? mine.Charge01 : 1f;
        var eyes = Ps(@"^MF_Eye\d$");
        glowTicks.Add(dt =>
        {
            float c = charge();
            for (int i = 0; i < eyes.Count; i++)
                SetGlow(eyes[i], c >= 1f ? (Mathf.Repeat(Time.time * 1.5f - i * 0.15f, 1f) < 0.2f ? 3f : 0.6f) : 0.1f + 0.5f * c);
        });
    }

    void ArcCoil(Turret t)
    {
        Spin(P("Arc_Toroid"), 60f, () => 1f + 4f * Charge(t));
        Spin(P("Arc_Orb"), -90f, () => 1f + 4f * Charge(t));
        Toggle(@"^Arc_Bolt", () => Since(t) < 0.15f || Random.value < 0.08f + 0.25f * Charge(t));
        Glow(@"Glow$|^Arc_Orb$", () => (0.6f + 0.4f * Mathf.Sin(Time.time * 8f)) * (1f + 3f * Flare(t, 0.3f)));
        var rods = Ps(@"^Arc_GroundRodTip");
        Every(0.15f, 0.6f, () => rods.Count > 0 && t != null && t.Online, () =>
            Zap(P("Arc_Orb").position, rods[Random.Range(0, rods.Count)].position, new Color(0.7f, 0.5f, 1f), 0.12f));
        OnShot(t, () =>
        {
            Vector3 orb = P("Arc_Orb").position;
            Juice.Flash(orb, new Color(0.7f, 0.5f, 1f), 8f, 14f, 0.2f);
            Juice.Burst(orb, new Color(0.8f, 0.6f, 1f), 14, 7f, 0.1f, 0.35f, 0.2f);
        });
    }

    void NeonTurret(Turret t)
    {
        TrackHead(P("NeonTurret_Head"), t, 240f);
        Glow(@"^Turret_(Sensor|EyeSmall)", () => t != null && t.Aim != null ? (Mathf.Repeat(Time.time * 6f, 1f) < 0.5f ? 3f : 0.4f) : 1f);
        Blink(@"^Turret_AntennaTip", 1.2f);
        Glow(@"^Turret_(Coil|TopGlow|BearingGlow|AmmoGlow)|^TurMk2_(Coil|TopGlow|SideArmorGlow)", () => 0.4f + 1.6f * Charge(t) + 2f * Flare(t, 0.15f));
        OnShot(t, () => Juice.Burst(P("NeonTurret_Head").position + Vector3.up, team, 6, 4f, 0.08f, 0.25f));
    }

    void Flak(Turret t)
    {
        TrackHead(P("FlakLantern_Head"), t, 180f);
        Glow(@"^Flak_Lantern(Core|Band)", () => (0.7f + 0.3f * Mathf.Sin(Time.time * 3f)) * (1f + 4f * Flare(t, 0.25f)));
        Chase(@"^Flak_(PodGlow|TowerStrip)", 6f);
        Glow(@"Glow$", () => 0.5f + Charge(t));
        OnShot(t, () =>
        {
            var core = P("Flak_LanternCore");
            if (core != null) Juice.Flash(core.position, Amber, 6f, 12f, 0.15f);
            Juice.Burst(P("FlakLantern_Head").position + Vector3.up * 1.5f, new Color(0.6f, 0.6f, 0.65f), 8, 3f, 0.35f, 0.8f, 1f);
        });
    }

    void Railspike(Turret t)
    {
        TrackHead(P("RailspikeTower_Head"), t, 90f);
        foreach (var coil in Ps(@"^Rail_Coil")) Spin(coil, 200f, () => 0.2f + 3f * Charge(t) * Charge(t));
        Glow(@"^Rail_(RailGlow|Capacitor|ButtressGlow|TurntableGlow)", () => 0.15f + 3f * Charge(t) * Charge(t) + 4f * Flare(t, 0.2f));
        Blink(@"^Rail_Beacon", 0.8f);
        var rails = Ps(@"^Rail_Rail-?\d");
        Every(0.05f, 0.15f, () => rails.Count >= 2 && t != null && t.Online && Charge(t) > 0.8f && t.Aim != null, () =>
            Zap(rails[0].position, rails[1].position, new Color(1f, 0.9f, 0.4f), 0.08f));
        OnShot(t, () =>
        {
            Vector3 head = P("RailspikeTower_Head").position + Vector3.up * 1.5f;
            Juice.Flash(head, new Color(1f, 0.9f, 0.4f), 14f, 20f, 0.3f);
            Juice.Ring(head, new Color(1f, 0.85f, 0.3f), 0.5f, 6f, 0.35f, 0.3f);
            Juice.Shake(head, 0.15f);
            foreach (var cap in Ps(@"^Rail_CapacitorCap")) Juice.Burst(cap.position, new Color(0.75f, 0.8f, 0.85f), 6, 2.5f, 0.4f, 0.9f, 1.5f);
        });
    }

    void Overwatch()
    {
        var nest = P("OverwatchSpire_Nest");
        var lens = P("OverS_SpotLens");
        if (nest == null) return;
        Vector3 restFwd = Flat(Center(@"^OverS_(Barrel|Scope)") - nest.position, transform.forward);
        Quaternion rest = nest.rotation;
        float yaw = 0f, look = 0f;
        Health spotted = null;
        float range = s != null && s.sight > 0f ? s.sight : 40f;

        var spot = new GameObject("Searchlight").AddComponent<Light>();
        spot.type = LightType.Spot;
        spot.range = range * 1.2f;
        spot.spotAngle = 28f;
        spot.intensity = 8f;
        spot.shadows = LightShadows.None;
        spot.transform.SetParent(transform, true);
        ticks.Add(dt =>
        {
            look -= dt;
            if (look <= 0f) { look = 0.3f; spotted = Nearest(range); }
            float want = spotted != null ? Vector3.SignedAngle(restFwd, Flat(spotted.transform.position - nest.position, restFwd), Vector3.up)
                                          : Mathf.Repeat(Time.time * 25f, 360f) - 180f;
            yaw = Mathf.MoveTowardsAngle(yaw, want, (spotted != null ? 160f : 25f) * Pace * dt);
            nest.rotation = Quaternion.AngleAxis(yaw, Vector3.up) * rest;
            Vector3 from = lens != null ? lens.position : nest.position;
            Vector3 aim = spotted != null ? spotted.transform.position : from + Quaternion.AngleAxis(yaw, Vector3.up) * restFwd * range * 0.6f;
            aim.y = 0f;
            spot.transform.SetPositionAndRotation(from, Quaternion.LookRotation(aim - from));
            spot.color = spotted != null ? Red : Color.Lerp(Cyan, Color.white, 0.5f);
            spot.enabled = Powered;
        });
        Blink(@"^OverS_Beacon", 1f);
        Glow(@"^OverS_(ScopeLens|SpotLens|MuzzleGlow|RearLight)", () => spotted != null ? 3f : 1f);
        Glow(@"^OverS_(BandGlow|FinGlow)", () => 0.6f + 0.4f * Mathf.Sin(Time.time * 2f));
    }

    void Uplink()
    {
        Spin(P("UplinkTower_Radar"), 45f);
        Spin(P("UplMk2_Dish"), -70f);
        Spin(P("UplMk2_ScanRing"), 120f);
        Glow(@"^Uplink_ScanCone", () => 0.5f + 0.5f * Mathf.Abs(Mathf.Sin(Time.time * 2.2f)));
        Chase(@"^Uplink_(Beacon|ButtressLight)", 5f);
        Blink(@"^Uplink_TopLight", 0.9f);
        Glow(@"^Uplink_(BandGlow|TurntableGlow|ButtressGlow|DishCore)", () => 0.7f + 0.3f * Mathf.Sin(Time.time * 4f));
        Every(2.8f, 2.8f, () => true, () =>
        {
            Vector3 c = new(transform.position.x, 0.2f, transform.position.z);
            Juice.Ring(c, team, 2f, 28f, 1.6f, 0.12f);
        });
        Motes(P("Uplink_TopSpike"), team, -0.6f, 6f);
    }

    void Pylon()
    {
        Spin(P("Pylon_OrbRingA"), 80f);
        Spin(P("Pylon_OrbRingB"), -120f);
        Spin(P("PylMk2_TopOrbRing"), 150f);
        int i = 0;
        foreach (var ring in Ps(@"^Pylon_EnergyRing")) { Spin(ring, i % 2 == 0 ? 90f : -90f); Bob(ring, 0.25f, 0.6f, i++ * 0.7f); }
        Toggle(@"^(Pylon_Arc|PylMk2_Arc)", () => Random.value < 0.35f);
        Glow(@"^Pylon_(Core|Orb)$|Glow$", () => 0.8f + 0.5f * Mathf.Pow(Mathf.Abs(Mathf.Sin(Time.time * 2.5f)), 6f) * 3f);
        var struts = Ps(@"^Pylon_Strut");
        Every(0.2f, 0.7f, () => struts.Count > 0, () => Zap(P("Pylon_Orb").position, struts[Random.Range(0, struts.Count)].position, team, 0.1f));
    }

    void Nexus()
    {
        Spin(P("Nexus_CoreOrbitA"), 50f);
        Spin(P("Nexus_CoreOrbitB"), -70f);
        Spin(P("Nexus_CoreOrbitC"), 95f);
        Spin(P("NexusMk2_OrbitD"), -120f);
        Spin(P("NexusMk3_Halo"), 30f);
        int i = 0;
        foreach (var d in Ps(@"^Nexus_Dish\d")) Spin(d, i++ % 2 == 0 ? 20f : -25f);
        i = 0;
        foreach (var r in Ps(@"^(Nexus|NexusMk3)_SpireRing")) Spin(r, i++ % 2 == 0 ? 60f : -60f);
        Bob(P("Nexus_EnergyCore"), 0.15f, 0.5f, 0f);
        Bob(P("Nexus_EnergyCoreInner"), 0.15f, 0.5f, 0f);
        Glow(@"^Nexus_(EnergyCore|EnergyCoreInner|InnerGlow|CradleGlow)", () => Heartbeat(1.1f) * 1.6f + 0.6f);
        Glitch(@"^Nexus_Bill(Screen|Glyph)");
        Chase(@"^Nexus_(PylonLight|ACLight|T3Strip)", 3f);
        Blink(@"^(Nexus|NexusMk3)_Beacon", 1.4f);
        Motes(P("Nexus_EnergyCore"), team, -0.4f, 10f);
    }

    void Refinery()
    {
        var refinery = GetComponent<Refinery>();
        Func<float> busy = () => refinery != null && refinery.Busy ? 1f : 0.15f;
        Spin(P("Ref_EmblemRing"), 40f, () => 1f + 5f * busy());
        Spin(P("Ref_DockRing"), 60f, busy);
        Spin(P("RefMk2_DockRing"), 60f, busy);
        Glow(@"^(Ref|RefMk2)_(FunnelGlow|IntakeArmGlow|SiloGaugeFill|DockInner)", () => 0.4f + 2.5f * busy() * (0.7f + 0.3f * Mathf.Sin(Time.time * 12f)));
        Chase(@"^Ref_DockChevron", 8f);
        Glow(@"^Ref_(HallRoofGlow|SlabGlow)", () => 0.8f + 0.2f * Mathf.Sin(Time.time * 1.5f));
        Every(0.06f, 0.12f, () => busy() > 0.5f, () =>
        {
            var f = P("Ref_Funnel");
            if (f != null) Juice.Burst(f.position + Vector3.up * 0.5f, Random.value < 0.5f ? Magenta : Cyan, 3, 3f, 0.1f, 0.4f, 0.8f, true);
        });
        Smoke(P("Ref_Stack"), () => busy() > 0.5f, 0.12f);
    }

    void Siphon()
    {
        var siphon = GetComponent<Siphon>();
        Func<float> fill = () => siphon != null ? siphon.Progress01 : Mathf.Repeat(Time.time / 4f, 1f);
        Spin(P("Siphon_FissureRing"), 70f);
        Scroll(P("Siphon_StreamRingA"), 3f, 0.6f, 0f);
        Scroll(P("Siphon_StreamRingB"), 3f, 0.6f, 0.5f);
        Glow(@"^Siphon_CapsuleFluid", () => 0.2f + 2.5f * fill() * fill());
        Glow(@"^Siphon_(FissureCore|FunnelGlow|BandGlow|Stream)", () => 0.7f + 0.3f * Mathf.Sin(Time.time * 6f) + 3f * Flare(() => siphon != null ? siphon.LastPayout : -99f, 0.5f));
        Glitch(@"^Siphon_Window");
        Blink(@"^Siphon_MastLight", 0.7f);
        Motes(P("Siphon_FissureCore"), Juice.Gold, -0.8f, 14f);
    }

    void MineField()
    {
        var mine = GetComponent<EmpMine>();
        Func<float> charge = () => mine != null ? mine.Charge01 : 1f;
        var cores = Ps(@"^Field_MineCore");
        glowTicks.Add(dt =>
        {
            float c = charge();
            for (int i = 0; i < cores.Count; i++)
            {
                float k = c >= 1f ? (Mathf.Repeat(Time.time * 1.5f - i * 0.15f, 1f) < 0.2f ? 3f : 0.6f) : 0.1f + 0.5f * c;
                SetGlow(cores[i], k);
            }
        });
        Spin(P("Field_NodeRing"), 90f, () => 0.2f + charge());
        Blink(@"^Field_NodeBlink", 0.6f);
        Glow(@"^Field_(Wire|Boundary|MineRing)", () => charge() >= 1f ? 0.7f + 0.3f * Mathf.Sin(Time.time * 5f) : 0.2f);
    }

    void Firewall()
    {
        Toggle(@"^Gate_Laser", () => Random.value > 0.12f);
        Bob(P("FirewallGate_Lasers"), 0.2f, 0.4f, 0f);
        Glow(@"^Gate_(EnergyField|Laser)", () => 0.7f + 0.3f * Mathf.Sin(Time.time * 9f) + (Random.value < 0.02f ? 2f : 0f));
        Glow(@"^Gate_PostLightL", () => Mathf.Repeat(Time.time, 1f) < 0.5f ? 2f : 0.2f);
        Glow(@"^Gate_PostLightR", () => Mathf.Repeat(Time.time, 1f) >= 0.5f ? 2f : 0.2f);
        Glitch(@"^Gate_Emblem");
        Chase(@"^Gate_(PostWindow|ArchHazard)", 4f);
    }

    void Cloak()
    {
        Spin(P("Cloak_CoreRingA"), 90f);
        Spin(P("Cloak_CoreRingB"), -140f);
        Spin(P("CloakFieldGenerator_Field"), 8f);
        Toggle(@"^Cloak_(FieldGlitch|Pixel)", () => Random.value < 0.5f);
        Glitch(@"^Cloak_GlitchCore");
        Glow(@"^Cloak_(FieldDome|FieldEdge)", () => 0.5f + 0.5f * Mathf.Abs(Mathf.Sin(Time.time * 1.3f)));
        var fins = Ps(@"^Cloak_FinGlow");
        glowTicks.Add(dt => { for (int i = 0; i < fins.Count; i++) SetGlow(fins[i], 0.4f + 1.6f * Mathf.Max(0f, Mathf.Sin(Time.time * 3f - i * 0.8f))); });
        Motes(P("Cloak_GlitchCore"), new Color(0.6f, 0.4f, 1f), -0.2f, 8f);
    }

    void Lab(Func<float> working)
    {
        foreach (var h in Ps(@"^Lab_Helix")) Spin(h, 50f, () => working() * 4f);
        Spin(P("Lab_Dish"), 25f, working);
        Spin(P("Lab_ScopeRing"), 80f, working);
        int i = 0;
        foreach (var r in Ps(@"^Lab_DomeRing")) Spin(r, i++ % 2 == 0 ? 30f : -30f, working);
        foreach (var sp in Ps(@"^Lab_Specimen")) Bob(sp, 0.08f, 0.7f, Random.value * 6f);
        Glow(@"^Lab_(HoloEmitterGlow|TubeLight|TubeFluid|PlatformGlow)", () => 0.4f + 1.6f * working() * (0.8f + 0.2f * Mathf.Sin(Time.time * 7f)));
        Glitch(@"^Lab_DoorSign");
        foreach (var tube in Ps(@"^Lab_TubeFluid")) Bubbles(tube, () => working() > 0.5f);
    }

    void Hangar(Func<float> working)
    {
        int i = 0;
        foreach (var sh in Ps(@"^Hangar_BayShutter"))
        {
            Vector3 rest = sh.localPosition;
            float delay = i++ * 0.15f, open = 0f;
            ticks.Add(dt =>
            {
                open = Mathf.MoveTowards(open, working() > 0.5f ? 1f : 0f, dt * 1.5f);
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(open * 1.6f - delay));
                sh.position = sh.parent.TransformPoint(rest) + Vector3.up * 1.6f * k;
            });
        }
        var bay = GetComponent<DroneHangar>();
        Func<bool> alert = () => bay != null && bay.Target != null;
        Chase(@"^Hangar_PadLight", 4f);
        Spin(P("Hangar_PadRing"), 40f, () => working() * (alert() ? 4f : 1f));
        Spin(P("Hangar_Dish"), 30f, () => alert() ? 7f : 1f);
        Glow(@"^Hangar_MastLight", () => Mathf.Repeat(Time.time * (alert() ? 5f : 1f), 1f) < 0.25f ? (alert() ? 4f : 3f) : 0.15f);
        if (bay != null) HangarEvents(bay);
        Glow(@"^Hangar_(BayGlow|SlabGlow)", () => 0.5f + 1.5f * working());
        Glitch(@"^Hangar_Sign");
    }

    void HangarEvents(DroneHangar bay)
    {
        var pad = P("Hangar_Pad");
        float launch = bay.LastLaunch, dock = bay.LastDock;
        ticks.Add(dt =>
        {
            Vector3 at = (pad != null ? pad.position : transform.position) + Vector3.up * 0.4f;
            if (bay.LastLaunch != launch)
            {
                launch = bay.LastLaunch;
                Juice.Flash(at + Vector3.up * 2f, team, 6f, 12f, 0.2f);
                Juice.Ring(at, team, 0.6f, 6f, 0.4f, 0.25f);
                Juice.Burst(at, Color.Lerp(team, Color.white, 0.4f), 10, 7f, 0.1f, 0.35f, 2f);
                Particles.Emit("Dust", at, Vector3.up * 1.5f, 2f, 1f, new Color(0.5f, 0.5f, 0.6f, 0.4f));
                Kick(-0.45f);
            }
            if (bay.LastDock != dock)
            {
                dock = bay.LastDock;
                Juice.Ring(at, team, 5f, 0.6f, 0.45f, 0.2f);
                Kick(0.3f);
            }
        });
    }

    void Hive(Func<float> working)
    {
        Glow(@"^(Hive_PodGlow|HiveMk2_PodGlow)", () => 0.4f + 2f * working() * Heartbeat(1.4f));
        foreach (var e in Ps(@"^Hive_PodEmbryo")) Bob(e, 0.06f, 1.4f, Random.value * 6f);
        Glitch(@"^Hive_(Emblem|EmblemCell)");
        Glow(@"^Hive_Lamp(Lens)?", () => Random.value < 0.03f ? 0.1f : 1.2f);
        Chase(@"^Hive_Chevron", 6f);
        Glow(@"^Hive_(DoorLintelGlow|DoorSlatGlow|RoofGlow|SlabGlow|ArmBaseGlow|ArmForeGlow)", () => 0.6f + 0.6f * working());
        Toggle(@"^HiveMk2_WeldSpark", () => working() > 0.5f && Random.value < 0.5f);
        Every(0.05f, 0.18f, () => working() > 0.5f, () =>
        {
            var held = P("Hive_HeldPart");
            var hand = P("Hive_ArmHand0") ?? P("Hive_ArmHand");
            if (held == null) return;
            Juice.Burst(held.position, new Color(1f, 0.8f, 0.4f), 4, 4f, 0.08f, 0.35f, 0.5f, true);
            if (hand != null) Zap(hand.position, held.position, Color.white, 0.06f);
        });
        Smoke(P("Hive_StackTop") ?? P("Hive_Stack"), () => working() > 0.5f, 0.15f);
        Smoke(P("HiveMk2_StackTop"), () => working() > 0.5f, 0.15f);
        foreach (var v in Ps(@"^Hive_VentSlit")) Smoke(v, () => Random.value < 0.02f, 0.5f);
    }

    void Aerie()
    {
        Chase(@"^Aerie_EdgeLight", 8f);
        Glow(@"^Aerie_(ShaftGlow|BaseGlow)", () => 0.7f + 0.3f * Mathf.Sin(Time.time * 3f));
        Blink(@"^Aerie_AntennaTip", 1.1f);
        foreach (var w in Ps(@"^Aerie_WindRing")) Spin(w, 120f);
    }

    Transform visual;
    Vector3 rest;
    float punch, punchVel;
    bool springing, landing;

    public void Land() => landing = true;
    Collider body;

    void Common()
    {
        var hp = GetComponent<Health>();
        var building = GetComponent<Building>();
        body = GetComponent<Collider>();
        if (eye != null && eye.transform != transform)
        {
            visual = eye.transform;
            while (visual.parent != transform) visual = visual.parent;
        }
        float last = hp != null ? hp.Current : 0f, nextJolt = 0f, released = building != null ? building.LastRelease : 0f;
        bool wasPowered = Powered;
        ticks.Add(dt =>
        {
            if (hp != null)
            {
                float now = hp.Current;
                if (now < last - 0.01f && Time.time >= nextJolt && !hp.IsDead)
                {
                    nextJolt = Time.time + 0.18f;
                    float hit = Mathf.Clamp01((last - now) / hp.MaxHealth * 12f);
                    Kick(-(0.35f + 0.6f * hit));
                    if (hit > 0.3f) Juice.Burst(Roof(), Juice.Fire, 4 + (int)(10f * hit), 6f, 0.1f, 0.45f, 1f, true);
                }
                last = now;
            }
            if (building != null && building.LastRelease != released)
            {
                released = building.LastRelease;
                Kick(0.8f);
                Juice.Ring(Ground(), team, Size() * 0.35f, Size() * 0.8f, 0.45f, 0.2f);
            }
            if (Powered != wasPowered)
            {
                wasPowered = Powered;
                if (Powered)
                {
                    Kick(0.6f);
                    Juice.Ring(Ground(), team, 1f, Size(), 0.6f, 0.25f);
                    Juice.Burst(Roof(), team, 14, 5f, 0.1f, 0.5f, 1f);
                }
                else
                {
                    Kick(-0.45f);
                    Juice.Burst(Roof(), new Color(0.6f, 0.6f, 0.7f), 10, 3f, 0.12f, 0.7f, 0.5f, true);
                }
            }
            if (landing && !TryGetComponent(out Construction _))
            {
                landing = false;
                Kick(-1.3f);
                Juice.Burst(Roof(), team, 16, 6f, 0.12f, 0.5f, 1.2f);
            }
            Spring(dt);
        });
    }

    public void Kick(float amount)
    {
        if (visual == null || TryGetComponent(out Construction _)) return;
        if (!springing) { rest = visual.localScale; springing = true; }
        punchVel += amount * (GameSettings.ReduceFlashing ? 0.5f : 1f);
    }

    void Spring(float dt)
    {
        if (!springing) return;
        dt = Mathf.Min(dt, 0.05f);
        punchVel += (-punch * 300f - punchVel * 14f) * dt;
        punch += punchVel * dt;
        if (Mathf.Abs(punch) < 0.002f && Mathf.Abs(punchVel) < 0.02f)
        {
            visual.localScale = rest;
            springing = false;
            punch = punchVel = 0f;
            return;
        }
        visual.localScale = Vector3.Scale(rest, new Vector3(1f - punch * 0.5f, 1f + punch, 1f - punch * 0.5f));
    }

    float Size() => body != null ? Mathf.Max(body.bounds.size.x, body.bounds.size.z) : 6f;
    Vector3 Ground() => new(transform.position.x, 0.25f, transform.position.z);

    Vector3 Roof()
    {
        if (body == null) return transform.position + Vector3.up * 3f;
        Bounds b = body.bounds;
        return new Vector3(b.center.x + b.size.x * Random.Range(-0.3f, 0.3f), b.max.y * 0.9f, b.center.z + b.size.z * Random.Range(-0.3f, 0.3f));
    }

    Transform P(string name)
    {
        foreach (var t in parts) if (t != null && t.name == name && t != transform) return t;
        return null;
    }

    List<Transform> Ps(string pattern)
    {
        var rx = new Regex(pattern);
        var list = new List<Transform>();
        foreach (var t in parts) if (t != null && t != transform && rx.IsMatch(t.name)) list.Add(t);
        return list;
    }

    Vector3 Center(string pattern)
    {
        var list = Ps(pattern);
        if (list.Count == 0) return transform.position + transform.forward;
        Vector3 sum = Vector3.zero;
        foreach (var t in list) sum += t.position;
        return sum / list.Count;
    }

    static Vector3 Flat(Vector3 v, Vector3 fallback)
    {
        v.y = 0f;
        return v.sqrMagnitude > 0.001f ? v.normalized : fallback;
    }

    void Spin(Transform t, float degPerSec, Func<float> rate = null)
    {
        if (t == null || degPerSec == 0f) return;
        ticks.Add(dt => t.Rotate(Vector3.up, degPerSec * (rate != null ? rate() : 1f) * Pace * dt, Space.World));
    }

    void Bob(Transform t, float metres, float hz, float phase)
    {
        if (t == null) return;
        Vector3 rest = t.localPosition;
        ticks.Add(dt => t.position = t.parent.TransformPoint(rest) + Vector3.up * Mathf.Sin((Time.time * hz * Pace + phase) * Mathf.PI * 2f) * metres);
    }

    void Scroll(Transform t, float height, float hz, float phase)
    {
        if (t == null) return;
        Vector3 rest = t.localPosition;
        ticks.Add(dt => t.position = t.parent.TransformPoint(rest) + Vector3.up * Mathf.Repeat(Time.time * hz * Pace + phase, 1f) * height);
    }

    void Glow(string pattern, Func<float> k)
    {
        var list = Ps(pattern);
        if (list.Count == 0) return;
        glowTicks.Add(dt => { float v = k(); foreach (var t in list) SetGlow(t, v); });
    }

    readonly Dictionary<Renderer, Color[]> baseGlow = new();
    void SetGlow(Transform t, float k)
    {
        if (t == null || !t.TryGetComponent(out Renderer r)) return;
        if (!baseGlow.TryGetValue(r, out var colors))
        {
            var mats = r.sharedMaterials;
            colors = new Color[mats.Length];
            for (int i = 0; i < mats.Length; i++)
                colors[i] = mats[i] != null && mats[i].HasProperty(EmissionId) && mats[i].IsKeywordEnabled("_EMISSION") ? mats[i].GetColor(EmissionId) : Color.clear;
            baseGlow[r] = colors;
        }
        for (int i = 0; i < colors.Length; i++)
        {
            if (colors[i].maxColorComponent <= 0.001f) continue;
            r.GetPropertyBlock(block, i);
            block.SetColor(EmissionId, colors[i] * k);
            r.SetPropertyBlock(block, i);
        }
    }

    void Blink(string pattern, float hz) => Glow(pattern, () => Mathf.Repeat(Time.time * hz, 1f) < 0.2f ? 3f : 0.15f);

    void Chase(string pattern, float hz)
    {
        var list = Ps(pattern);
        if (list.Count == 0) return;
        list.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        glowTicks.Add(dt =>
        {
            int lit = (int)(Time.time * hz) % list.Count;
            for (int i = 0; i < list.Count; i++) SetGlow(list[i], i == lit ? 3f : 0.35f);
        });
    }

    void Glitch(string pattern)
    {
        var list = Ps(pattern);
        if (list.Count == 0) return;
        float until = 0f, level = 1f;
        glowTicks.Add(dt =>
        {
            if (Time.time >= until)
            {
                float roll = Random.value;
                level = GameSettings.ReduceFlashing ? 1f : roll < 0.06f ? 0f : roll < 0.12f ? 2.5f : 1f;
                until = Time.time + (level == 1f ? Random.Range(0.4f, 2.5f) : Random.Range(0.03f, 0.12f));
            }
            foreach (var t in list) SetGlow(t, level);
        });
    }

    void Toggle(string pattern, Func<bool> on)
    {
        var rends = new List<Renderer>();
        foreach (var t in Ps(pattern)) if (t.TryGetComponent(out Renderer r)) rends.Add(r);
        if (rends.Count == 0) return;
        ticks.Add(dt => { bool powered = Powered; foreach (var r in rends) r.enabled = powered && on(); });
    }

    void Every(float min, float max, Func<bool> when, Action act)
    {
        float next = Time.time + Random.Range(min, max);
        ticks.Add(dt =>
        {
            if (Time.time < next) return;
            next = Time.time + Random.Range(min, max) / Mathf.Max(0.2f, Pace);
            if (Powered && when()) act();
        });
    }


    static float Charge(Turret t) => t != null && t.Online ? t.Charge01 : 0f;
    static float Since(Turret t) => t != null ? Time.time - t.LastShot : 99f;
    static float Flare(Turret t, float time) => Mathf.Clamp01(1f - Since(t) / time);
    static float Flare(Func<float> stamp, float time) => Mathf.Clamp01(1f - (Time.time - stamp()) / time);
    static float Heartbeat(float hz) { float p = Mathf.Repeat(Time.time * hz, 1f); return p < 0.1f ? 1f : p < 0.2f ? 0.3f : p < 0.3f ? 0.8f : 0.2f; }

    void OnShot(Turret t, Action act)
    {
        if (t == null) return;
        float seen = t.LastShot;
        ticks.Add(dt => { if (t.LastShot != seen) { seen = t.LastShot; act(); } });
    }

    void TrackHead(Transform head, Turret t, float degPerSec)
    {
        if (head == null || t == null) return;
        Quaternion rest = head.rotation;
        Vector3 restFwd = Vector3.zero;
        int n = 0;
        foreach (var m in head.GetComponentsInChildren<Transform>(true)) if (m.name.Contains("_Muzzle") || m.name.Contains("_Barrel") || m.name == "Rail_Spike") { restFwd += m.position - head.position; n++; }
        restFwd = Flat(n > 0 ? restFwd : transform.forward, transform.forward);
        float yaw = 0f, phase = Random.value * 6f;
        ticks.Add(dt =>
        {
            if (!t.Online) return;
            float want = t.Aim != null ? Vector3.SignedAngle(restFwd, Flat(t.Aim.transform.position - head.position, restFwd), Vector3.up)
                                       : Mathf.Sin(Time.time * 0.35f + phase) * 60f;
            yaw = Mathf.MoveTowardsAngle(yaw, want, (t.Aim != null ? degPerSec : degPerSec * 0.15f) * dt);
            head.rotation = Quaternion.AngleAxis(yaw, Vector3.up) * rest;
        });
    }

    Health Nearest(float range)
    {
        Health best = null;
        float bestSqr = range * range;
        int myTeam = s != null ? s.Health.team : 0;
        foreach (var h in Health.All)
        {
            if (Net.Allied(h.team, myTeam) || h.CloakedFrom(transform.position)) continue;
            float sqr = (h.transform.position - transform.position).sqrMagnitude;
            if (sqr < bestSqr) { bestSqr = sqr; best = h; }
        }
        return best;
    }


    readonly List<(LineRenderer lr, float until, Vector3 a, Vector3 b)> zaps = new();
    void Zap(Vector3 a, Vector3 b, Color c, float time)
    {
        int slot = zaps.FindIndex(z => Time.time >= z.until);
        LineRenderer lr;
        if (slot < 0)
        {
            if (zaps.Count >= 4) return;
            lr = new GameObject("Zap").AddComponent<LineRenderer>();
            lr.transform.SetParent(transform, false);
            lr.sharedMaterial = Resources.Load<Material>("HarvestFX");
            lr.positionCount = 7;
            lr.widthMultiplier = 0.12f;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            zaps.Add((lr, 0f, a, b));
            slot = zaps.Count - 1;
            int i = slot;
            ticks.Add(dt => DrawZap(i));
        }
        lr = zaps[slot].lr;
        lr.startColor = c * 3f;
        lr.endColor = Color.white * 2f;
        zaps[slot] = (lr, Time.time + time, a, b);
        DrawZap(slot);
    }

    void DrawZap(int i)
    {
        var (lr, until, a, b) = zaps[i];
        lr.enabled = Time.time < until;
        if (!lr.enabled) return;
        float jitter = Vector3.Distance(a, b) * 0.08f;
        for (int p = 0; p < lr.positionCount; p++)
            lr.SetPosition(p, Vector3.Lerp(a, b, p / (lr.positionCount - 1f)) + (p == 0 || p == lr.positionCount - 1 ? Vector3.zero : Random.insideUnitSphere * jitter));
    }

    void Motes(Transform at, Color c, float gravity, float perSecond)
    {
        if (at == null) return;
        var ps = ProductionFX.MakeSparks(transform, gameObject.layer);
        var main = ps.main;
        main.gravityModifier = gravity;
        ps.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.Billboard;
        float debt = 0f;
        var p = new ParticleSystem.EmitParams { startLifetime = 1.6f };
        ticks.Add(dt =>
        {
            if (!Powered) return;
            debt += perSecond * dt;
            for (; debt >= 1f; debt--)
            {
                p.position = at.position + Random.insideUnitSphere * 0.6f;
                p.velocity = Random.insideUnitSphere * 0.6f;
                p.startSize = Random.Range(0.06f, 0.16f);
                p.startColor = Random.value < 0.8f ? c : Color.white;
                ps.Emit(p, 1);
            }
        });
    }

    void Bubbles(Transform tube, Func<bool> on)
    {
        if (tube == null) return;
        Every(0.08f, 0.2f, on, () => Juice.Burst(tube.position + Vector3.down * 0.4f, new Color(0.4f, 1f, 0.6f), 1, 0.6f, 0.08f, 0.8f, 2.5f));
    }

    void Smoke(Transform at, Func<bool> on, float gap)
    {
        if (at == null) return;
        Every(gap * 0.7f, gap * 1.3f, on, () => Juice.Burst(at.position + Vector3.up * 0.3f, new Color(0.5f, 0.5f, 0.55f, 0.6f), 2, 1.2f, 0.45f, 1.4f, 2f));
    }
}
