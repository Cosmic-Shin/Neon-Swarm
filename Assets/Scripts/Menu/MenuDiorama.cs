using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class MenuDiorama : MonoBehaviour
{
    static readonly int SpeedId = Animator.StringToHash("Speed");
    static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
    static readonly Color Lime = new(0.45f, 1f, 0.3f), Cyan = new(0.3f, 1f, 1f), RainColor = new(0.6f, 0.75f, 1f, 0.28f), RippleColor = new(0.55f, 0.75f, 1f, 0.4f);

    public Camera cam;
    public Vector3 lookAt;
    public Transform harvester, refineryDock, aerie, flyer, queen;
    public Transform[] ores, skitters, spinners;
    public Vector3 armyCentre;
    public Vector3[] searchlightSpots;
    public Transform[] traffic, patrol, adTowers;

    Vector3 camOffset;
    Vector2 parallax;
    float startTime;
    readonly List<(Material m, Color baseColor, float phase)> glows = new();
    readonly List<(Transform t, float speed)> beams = new();
    float rainDebt, rippleDebt;

    void Start()
    {
        startTime = Time.time;
        camOffset = cam.transform.position - lookAt;
        foreach (var a in GetComponentsInChildren<Animator>())
        {
            a.SetFloat(SpeedId, 0f);
            a.Play(0, 0, Random.value);
            a.speed = Random.Range(0.85f, 1.15f);
        }
        CloneGlows();
        Searchlights();
        StartCoroutine(HarvestLoop());
        StartCoroutine(QueenTwitch());
        StartCoroutine(FlyerLoop());
        foreach (var s in skitters) StartCoroutine(Wander(s));
        StartCoroutine(Regroup());
        Skyline();
        HoloAds();
        for (int i = 0; i < traffic.Length; i++) if (traffic[i] != null) StartCoroutine(Fly(traffic[i], i * 2.5f));
        for (int i = 0; i < patrol.Length; i++) if (patrol[i] != null) StartCoroutine(Patrol(patrol[i], i));
        ScreenUI();
    }

    void Update()
    {
        float t = Time.time - startTime;
        OrbitCamera(t);
        foreach (var (m, c, phase) in glows) m.SetColor(EmissionId, c * (1f + 0.15f * Mathf.Sin(t * 1.7f + phase)));
        for (int i = 0; i < spinners.Length; i++) if (spinners[i] != null) spinners[i].Rotate(i % 2 == 0 ? Vector3.up : Vector3.right, (40f + i * 25f) * Time.deltaTime, Space.Self);
        foreach (var (b, speed) in beams) b.localRotation = Quaternion.Euler(-68f + 10f * Mathf.Sin(t * 0.21f + speed), b.localEulerAngles.y + speed * Time.deltaTime, 0f);
        if (GameSettings.Rain) Rain(Time.deltaTime);
        Mist(Time.deltaTime);
        foreach (var (m, c, phase) in ads)
        {
            bool off = Mathf.Sin(t * 13f + phase) > 0.97f || (Mathf.Sin(t * 0.7f + phase) > 0.995f);
            m.SetColor(TintId, off ? c * 0.15f : c * (0.9f + 0.1f * Mathf.Sin(t * 30f + phase)));
        }
        Dim();
    }

    void OrbitCamera(float t)
    {
        var mouse = Mouse.current;
        Vector2 m = Vector2.zero;
        if (mouse != null && Screen.width > 0)
        {
            Vector2 p = mouse.position.ReadValue();
            m = new Vector2(p.x / Screen.width * 2f - 1f, p.y / Screen.height * 2f - 1f);
            m = Vector2.ClampMagnitude(m, 1f);
        }
        parallax = Vector2.Lerp(parallax, m, 1f - Mathf.Exp(-2f * Time.deltaTime));
        float yaw = 15f * Mathf.Sin(t * Mathf.PI * 2f / 60f);
        Vector3 pos = lookAt + Quaternion.Euler(0f, yaw, 0f) * camOffset + Vector3.up * 0.06f * Mathf.Sin(t * 0.9f);
        cam.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(lookAt - pos) * Quaternion.Euler(-parallax.y * 2f, parallax.x * 2f, 0f));
    }

    void CloneGlows()
    {
        var clones = new Dictionary<Material, Material>();
        foreach (var r in GetComponentsInChildren<Renderer>(true))
        {
            if (r is ParticleSystemRenderer || r is LineRenderer) continue;
            var mats = r.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < mats.Length; i++)
            {
                var src = mats[i];
                if (src == null || !src.HasProperty(EmissionId) || !src.IsKeywordEnabled("_EMISSION")) continue;
                if (!clones.TryGetValue(src, out var copy))
                {
                    copy = new Material(src) { name = src.name + " (menu)" };
                    clones[src] = copy;
                    glows.Add((copy, src.GetColor(EmissionId), Random.value * 6f));
                }
                mats[i] = copy;
                changed = true;
            }
            if (changed) r.sharedMaterials = mats;
        }
    }


    void Rain(float dt)
    {
        Vector3 fall = new(1.2f, -22f, 0.5f);
        for (rainDebt += 450f * dt; rainDebt >= 1f; rainDebt--)
        {
            Vector3 at = new(Random.Range(-24f, 18f), Random.Range(12f, 18f), Random.Range(-14f, 18f));
            if ((at - cam.transform.position).sqrMagnitude < 100f) continue;
            Particles.Emit("Rain", at, fall, 0.035f, at.y / -fall.y, RainColor);
        }
        for (rippleDebt += 90f * dt; rippleDebt >= 1f; rippleDebt--)
            Particles.Emit("Ripple", new Vector3(Random.Range(-20f, 16f), 0.03f, Random.Range(-16f, 12f)), Vector3.zero, Random.Range(0.3f, 0.6f), 0.45f, RippleColor);
    }

    void Searchlights()
    {
        var tex = new Texture2D(16, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < 64; y++)
            for (int x = 0; x < 16; x++)
            {
                float a = (1f - Mathf.Abs(x / 15f - 0.5f) * 2f) * Mathf.Pow(1f - y / 63f, 1.5f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        tex.Apply();
        var mat = new Material(Juice.Additive) { mainTexture = tex, color = new Color(0.6f, 0.8f, 1f, 0.035f) };
        for (int i = 0; i < searchlightSpots.Length; i++)
        {
            var pivot = new GameObject("Searchlight").transform;
            pivot.SetParent(transform, false);
            pivot.position = searchlightSpots[i];
            pivot.localRotation = Quaternion.Euler(-68f, Random.Range(-60f, 60f), 0f);
            var beam = pivot.gameObject.AddComponent<LineRenderer>();
            beam.useWorldSpace = false;
            beam.sharedMaterial = mat;
            beam.positionCount = 2;
            beam.SetPosition(0, Vector3.zero);
            beam.SetPosition(1, new Vector3(0f, 0f, 70f));
            beam.startWidth = 0.5f;
            beam.endWidth = 4f;
            beam.textureMode = LineTextureMode.Stretch;
            beam.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            beam.startColor = beam.endColor = new Color(0.6f, 0.8f, 1f, 0.18f);
            beams.Add((pivot, (i % 2 == 0 ? -1f : 1f) * Random.Range(6f, 12f)));
        }
    }


    IEnumerator Drive(Transform unit, Animator anim, Vector3 to, float speed)
    {
        if (anim) anim.SetFloat(SpeedId, 1f);
        to.y = unit.position.y;
        while ((unit.position - to).sqrMagnitude > 0.01f)
        {
            Vector3 dir = to - unit.position;
            if (dir.sqrMagnitude > 0.0001f) unit.rotation = Quaternion.RotateTowards(unit.rotation, Quaternion.LookRotation(dir), 240f * Time.deltaTime);
            unit.position = Vector3.MoveTowards(unit.position, to, speed * Time.deltaTime);
            yield return null;
        }
        if (anim) anim.SetFloat(SpeedId, 0f);
    }

    IEnumerator Face(Transform unit, Vector3 target)
    {
        Vector3 dir = target - unit.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) yield break;
        var want = Quaternion.LookRotation(dir);
        while (Quaternion.Angle(unit.rotation, want) > 1f)
        {
            unit.rotation = Quaternion.RotateTowards(unit.rotation, want, 200f * Time.deltaTime);
            yield return null;
        }
    }

    IEnumerator HarvestLoop()
    {
        if (harvester == null || refineryDock == null || ores.Length == 0) yield break;
        var anim = harvester.GetComponentInChildren<Animator>();
        Vector3 mid = Vector3.Lerp(ores[0].position, refineryDock.position, 0.5f) + new Vector3(-1f, 0f, -1f);
        while (true)
        {
            var ore = ores[Random.Range(0, ores.Length)];
            Vector3 spot = ore.position + (mid - ore.position).normalized * 1.1f;
            yield return Drive(harvester, anim, mid, 1.6f);
            yield return Drive(harvester, anim, spot, 1.6f);
            yield return Face(harvester, ore.position);
            if (anim) anim.Play("Harvest");
            for (float t = 0f; t < 4f; t += Time.deltaTime)
            {
                if (Random.value < 0.5f)
                {
                    Vector3 from = ore.position + Vector3.up * Random.Range(0.1f, 0.5f);
                    Particles.Emit("Mote", from, (harvester.position + Vector3.up * 0.5f - from) * 2f, Random.Range(0.04f, 0.08f), 0.5f, Lime * 2f);
                }
                yield return null;
            }
            if (anim) anim.Play("Idle");
            yield return Drive(harvester, anim, mid, 1.6f);
            yield return Drive(harvester, anim, refineryDock.position, 1.6f);
            if (anim) anim.Play("Unload");
            for (float t = 0f; t < 3f; t += Time.deltaTime)
            {
                if (Random.value < 0.6f)
                    Particles.Emit("Mote", harvester.position + Vector3.up * 0.4f + Random.insideUnitSphere * 0.2f, new Vector3(0f, Random.Range(1.5f, 2.5f), 0f), Random.Range(0.04f, 0.08f), 0.7f, Lime * 2f);
                yield return null;
            }
            if (anim) anim.Play("Idle");
        }
    }

    IEnumerator QueenTwitch()
    {
        if (queen == null) yield break;
        var anim = queen.GetComponentInChildren<Animator>();
        if (anim == null) yield break;
        anim.fireEvents = false;
        while (true)
        {
            yield return new WaitForSeconds(Random.Range(7f, 13f));
            anim.CrossFade("Attack", 0.15f, 0, 0f);
            yield return new WaitForSeconds(0.55f);
            anim.CrossFade("Idle", 0.3f);
        }
    }

    bool regroup;

    IEnumerator Regroup()
    {
        while (true)
        {
            yield return new WaitForSeconds(Random.Range(18f, 28f));
            regroup = true;
            yield return new WaitForSeconds(6f);
            regroup = false;
        }
    }

    IEnumerator Wander(Transform s)
    {
        var anim = s.GetComponentInChildren<Animator>();
        yield return new WaitForSeconds(Random.Range(0f, 4f));
        while (true)
        {
            Vector2 r = Random.insideUnitCircle * (regroup ? 1.2f : 3.2f);
            yield return Drive(s, anim, armyCentre + new Vector3(r.x, 0f, r.y), 0.6f);
            yield return new WaitForSeconds(Random.Range(1.5f, 5f));
        }
    }

    IEnumerator FlyerLoop()
    {
        if (aerie == null || flyer == null) yield break;
        var fx = aerie.GetComponent<AerieFX>();
        Transform pad = null;
        foreach (var t in aerie.GetComponentsInChildren<Transform>(true)) if (t.name == "Aerie_LaunchPoint") pad = t;
        if (pad == null) pad = aerie;
        flyer.gameObject.SetActive(false);
        Vector3 centre = aerie.position + Vector3.up * 5f;
        while (true)
        {
            yield return new WaitForSeconds(Random.Range(10f, 16f));
            if (fx) fx.OpenHatch(2.6f);
            yield return new WaitForSeconds(1.5f);
            flyer.gameObject.SetActive(true);
            Vector3 start = pad.position;
            for (float t = 0f; t < 1.2f; t += Time.deltaTime)
            {
                flyer.position = Vector3.Lerp(start, centre, Mathf.SmoothStep(0f, 1f, t / 1.2f));
                yield return null;
            }
            const float radius = 5f, lap = 7f;
            for (float t = 0f; t < lap * 2f; t += Time.deltaTime)
            {
                float a = t / lap * Mathf.PI * 2f;
                float r = radius * Mathf.Clamp01(t / 1.5f) * Mathf.Clamp01((lap * 2f - t) / 1.5f);
                Vector3 p = centre + new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a * 2f) * 0.3f, Mathf.Sin(a) * r);
                Vector3 d = p - flyer.position;
                if (d.sqrMagnitude > 0.00001f) flyer.rotation = Quaternion.LookRotation(d) * Quaternion.Euler(0f, 0f, -15f);
                flyer.position = p;
                yield return null;
            }
            if (fx) fx.OpenHatch(2.4f);
            yield return new WaitForSeconds(1.5f);
            for (float t = 0f; t < 1.2f; t += Time.deltaTime)
            {
                flyer.position = Vector3.Lerp(centre, pad.position, Mathf.SmoothStep(0f, 1f, t / 1.2f));
                yield return null;
            }
            flyer.gameObject.SetActive(false);
        }
    }


    static readonly int TintId = Shader.PropertyToID("_TintColor");
    static readonly Color[] Neon = { new(1f, 0.15f, 0.75f), new(0.2f, 0.95f, 1f), new(1f, 0.8f, 0.2f) };
    readonly List<(Material m, Color c, float phase)> ads = new();
    float mistDebt;

    static Texture2D Tex(int w, int h, System.Func<int, int, Color> f)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Point };
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) tex.SetPixel(x, y, f(x, y));
        tex.Apply();
        return tex;
    }

    void Skyline()
    {
        var windows = Tex(32, 64, (x, y) =>
        {
            if (x % 4 == 0 || y % 4 == 0) return Color.black;
            int cell = (x / 4) * 97 + (y / 4) * 31;
            float r = Mathf.Abs(Mathf.Sin(cell * 12.9898f) * 43758.5453f) % 1f;
            return r < 0.07f ? Neon[cell % 3] * 0.8f : r < 0.24f ? new Color(0.9f, 0.85f, 0.7f) * 0.3f : Color.black;
        });
        var mat = new Material(Shader.Find("Standard")) { name = "Skyline", color = new Color(0.015f, 0.015f, 0.03f) };
        mat.SetFloat("_Glossiness", 0.4f);
        mat.EnableKeyword("_EMISSION");
        mat.SetTexture("_EmissionMap", windows);
        mat.SetColor("_EmissionColor", Color.white * 0.9f);
        var root = new GameObject("Skyline").transform;
        root.SetParent(transform, false);
        var rng = new System.Random(7);
        for (int i = 0; i < 34; i++)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(box.GetComponent<Collider>());
            box.transform.SetParent(root, false);
            float w = 7f + (float)rng.NextDouble() * 10f, h = 22f + (float)rng.NextDouble() * 45f;
            box.transform.position = new Vector3(-85f + i * 5.2f + (float)rng.NextDouble() * 4f, h / 2f, 62f + (float)rng.NextDouble() * 34f);
            box.transform.localScale = new Vector3(w, h, w);
            var r = box.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            var block = new MaterialPropertyBlock();
            block.SetVector("_MainTex_ST", new Vector4(w / 10f, h / 19f, (float)rng.NextDouble(), (float)rng.NextDouble()));
            r.SetPropertyBlock(block);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (rng.NextDouble() < 0.35)
            {
                var tip = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(tip.GetComponent<Collider>());
                tip.transform.SetParent(box.transform, false);
                tip.transform.localPosition = new Vector3(0f, 0.5f, 0f);
                tip.transform.localScale = new Vector3(0.08f, 0.02f, 0.08f);
                var tm = new Material(Juice.Additive);
                tm.SetColor(TintId, new Color(1f, 0.15f, 0.2f, 1f));
                tip.GetComponent<Renderer>().sharedMaterial = tm;
            }
        }
    }

    void HoloAds()
    {
        var rng = new System.Random(11);
        foreach (var tower in adTowers)
        {
            if (tower == null) continue;
            var b = new Bounds(tower.position, Vector3.zero);
            foreach (var r in tower.GetComponentsInChildren<Renderer>()) b.Encapsulate(r.bounds);
            Color c = Neon[rng.Next(2)];
            int seed = rng.Next(1000);
            var tex = Tex(48, 72, (x, y) =>
            {
                bool frame = x < 2 || x > 45 || y < 2 || y > 69;
                bool logo = y > 40 && y < 64 && Mathf.Abs(x - 24) + Mathf.Abs(y - 52) < 11 && (x + y + seed) % 5 != 0;
                int row = y / 6;
                bool text = y < 36 && y % 6 > 1 && x > 6 && x < 6 + ((row * 37 + seed) % 30) + 10 && Mathf.Sin(x * 1.7f + row * 5f + seed) > -0.3f;
                float scan = (y % 2 == 0) ? 1f : 0.75f;
                return new Color(1f, 1f, 1f, (frame || logo ? 1f : text ? 0.7f : 0.08f) * scan);
            });
            var mat = new Material(Juice.Additive) { mainTexture = tex };
            var color = new Color(c.r, c.g, c.b, 0.55f);
            mat.SetColor(TintId, color);
            ads.Add((mat, color, (float)rng.NextDouble() * 10f));
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(quad.GetComponent<Collider>());
            quad.name = "HoloAd";
            quad.transform.SetParent(transform, false);
            Vector3 toCam = cam.transform.position - b.center;
            toCam.y = 0f;
            quad.transform.position = b.center + toCam.normalized * (Mathf.Max(b.extents.x, b.extents.z) * 0.6f) + Vector3.up * b.extents.y * 0.25f;
            quad.transform.rotation = Quaternion.LookRotation(-toCam.normalized);
            quad.transform.localScale = new Vector3(3.2f, 4.8f, 1f);
            var qr = quad.GetComponent<Renderer>();
            qr.sharedMaterial = mat;
            qr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }

    IEnumerator Fly(Transform craft, float delay)
    {
        var trail = craft.gameObject.AddComponent<TrailRenderer>();
        trail.sharedMaterial = Juice.Additive;
        trail.time = 0.9f;
        trail.startWidth = 0.35f;
        trail.endWidth = 0f;
        Color c = Neon[Random.Range(0, 2)];
        trail.startColor = new Color(c.r, c.g, c.b, 0.6f);
        trail.endColor = new Color(c.r, c.g, c.b, 0f);
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        craft.gameObject.SetActive(false);
        yield return new WaitForSeconds(delay);
        while (true)
        {
            float dir = Random.value < 0.5f ? 1f : -1f;
            float z = Random.Range(24f, 62f), y = Random.Range(9f, 21f), speed = Random.Range(5f, 11f);
            Vector3 from = new(-70f * dir, y, z), to = new(70f * dir, y + Random.Range(-3f, 3f), z + Random.Range(-8f, 8f));
            craft.SetPositionAndRotation(from, Quaternion.LookRotation(to - from));
            trail.Clear();
            craft.gameObject.SetActive(true);
            float len = (to - from).magnitude;
            for (float d = 0f; d < len; d += speed * Time.deltaTime)
            {
                craft.position = Vector3.Lerp(from, to, d / len) + Vector3.up * Mathf.Sin(d * 0.3f) * 0.4f;
                yield return null;
            }
            craft.gameObject.SetActive(false);
            yield return new WaitForSeconds(Random.Range(1f, 7f));
        }
    }

    IEnumerator Patrol(Transform walker, int index)
    {
        var anim = walker.GetComponentInChildren<Animator>();
        float z = 13.5f + (index == 0 ? -0.6f : 0.6f);
        walker.position = new Vector3(index == 0 ? -16f : 16f, 0f, z);
        while (true)
        {
            foreach (float x in index == 0 ? new[] { 16f, -16f } : new[] { -16f, 16f })
            {
                yield return Drive(walker, anim, new Vector3(x, 0f, z), 1.1f);
                yield return new WaitForSeconds(Random.Range(2f, 5f));
            }
        }
    }

    void Mist(float dt)
    {
        for (mistDebt += 3f * dt; mistDebt >= 1f; mistDebt--)
        {
            Vector3 at = new(Random.Range(-26f, 22f), Random.Range(0.2f, 0.8f), Random.Range(-14f, 24f));
            Particles.Emit("Steam", at, new Vector3(Random.Range(0.2f, 0.5f), 0.02f, Random.Range(-0.1f, 0.2f)), Random.Range(3f, 5f), 10f, new Color(0.5f, 0.45f, 0.8f, 0.07f));
        }
    }


    Image dim;
    GameObject lobby, signIn;

    void ScreenUI()
    {
        var canvas = GameObject.Find("Canvas");
        if (canvas == null) return;
        var login = canvas.GetComponent<LoginScreen>();
        if (login != null) { lobby = login.lobbyPanel; signIn = login.signInPanel; }

        if (canvas.transform.Find("MenuBackground") is Transform bg && bg.TryGetComponent(out RawImage shade))
        {
            var tex = new Texture2D(256, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int x = 0; x < 256; x++) tex.SetPixel(x, 0, new Color(0.01f, 0.012f, 0.03f, 0.88f * Mathf.Pow(1f - x / 255f, 1.6f)));
            tex.Apply();
            shade.material = null;
            shade.texture = tex;
            shade.color = Color.white;
            shade.raycastTarget = false;
            var rt = shade.rectTransform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = new Vector2(0.6f, 1f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;

            dim = new GameObject("DioramaDim", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            dim.transform.SetParent(canvas.transform, false);
            dim.transform.SetSiblingIndex(bg.GetSiblingIndex() + 1);
            dim.rectTransform.anchorMin = Vector2.zero; dim.rectTransform.anchorMax = Vector2.one;
            dim.rectTransform.offsetMin = dim.rectTransform.offsetMax = Vector2.zero;
            dim.color = new Color(0.01f, 0.012f, 0.03f, 0f);
            dim.raycastTarget = false;
        }

        var fade = new GameObject("FadeIn", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        fade.transform.SetParent(canvas.transform, false);
        fade.rectTransform.anchorMin = Vector2.zero; fade.rectTransform.anchorMax = Vector2.one;
        fade.rectTransform.offsetMin = fade.rectTransform.offsetMax = Vector2.zero;
        fade.color = Color.black;
        fade.raycastTarget = false;
        StartCoroutine(FadeIn(fade));
    }

    IEnumerator FadeIn(Image fade)
    {
        for (float t = 0f; t < 0.8f; t += Time.unscaledDeltaTime)
        {
            fade.transform.SetAsLastSibling();
            fade.color = new Color(0f, 0f, 0f, 1f - t / 0.8f);
            yield return null;
        }
        Destroy(fade.gameObject);
    }

    Transform canvasRoot;
    GlitchTitle title;
    int screenKey;

    void GlitchOnScreenChange()
    {
        if (canvasRoot == null && GameObject.Find("Canvas") is GameObject c) { canvasRoot = c.transform; title = canvasRoot.GetComponentInChildren<GlitchTitle>(true); }
        if (canvasRoot == null || title == null) return;
        int key = 17;
        foreach (Transform t in canvasRoot) if (t.gameObject.activeSelf && t != title.transform && t.name != "FadeIn") key = key * 31 + t.GetInstanceID();
        if (screenKey != 0 && key != screenKey) title.Glitch();
        screenKey = key;
    }

    void Dim()
    {
        GlitchOnScreenChange();
        if (dim == null) return;
        bool menu = (lobby != null && lobby.activeSelf) || (signIn != null && signIn.activeSelf);
        var c = dim.color;
        c.a = Mathf.MoveTowards(c.a, menu ? 0f : 0.62f, Time.deltaTime * 2.5f);
        dim.color = c;
    }
}
