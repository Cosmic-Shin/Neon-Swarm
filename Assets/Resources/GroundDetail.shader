// A map-sized overlay just above the ground (MapDressing): faint glowing seams between 12 m street panels,
// with slow pulse waves rolling out from the map centre, dark cracks, and wet puddles that reflect the neon.
// Everything is in world space, so it lines up across the whole map. Alpha-blended, no depth write.
Shader "GD5/GroundDetail"
{
    Properties
    {
        _Seam ("Seam glow", Color) = (0.25, 0.45, 1.2, 1)
        _PuddleA ("Puddle tint A", Color) = (1.0, 0.15, 0.8, 1)
        _PuddleB ("Puddle tint B", Color) = (0.1, 0.85, 1.0, 1)
        _Panel ("Panel size (m)", Float) = 12
    }
    SubShader
    {
        Tags { "Queue" = "Transparent-50" "RenderType" = "Transparent" "IgnoreProjector" = "True" } // transparent pass: blending in the opaque queue broke here
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        Offset -1, -1

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Seam, _PuddleA, _PuddleB;
            float _Panel;

            struct v2f { float4 pos : SV_POSITION; float2 world : TEXCOORD0; };

            v2f vert (appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.world = mul(unity_ObjectToWorld, v.vertex).xz;
                return o;
            }

            float hash (float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float noise (float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3 - 2 * f);
                return lerp(lerp(hash(i), hash(i + float2(1, 0)), f.x), lerp(hash(i + float2(0, 1)), hash(i + float2(1, 1)), f.x), f.y);
            }
            float fbm (float2 p) { return noise(p) * 0.6 + noise(p * 2.13) * 0.3 + noise(p * 4.7) * 0.1; }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 w = i.world;

                // Panel seams: thin lines every _Panel metres; some panels' seams are lit brighter.
                float2 cell = w / _Panel;
                float2 f = abs(frac(cell) - 0.5);
                float seam = smoothstep(0.47, 0.495, max(f.x, f.y)); // 1 on the panel edges
                float lit = step(0.82, hash(floor(cell)));
                float wave = 0.5 + 0.5 * sin(length(w) * 0.035 - _Time.y * 1.2); // rings rolling out from the centre
                float seamA = seam * (0.06 + 0.2 * lit) * (0.4 + 0.6 * wave);

                // Cracks: thin dark lines where noise crosses a level.
                float n = fbm(w * 0.045);
                float crack = 1 - smoothstep(0.0, fwidth(n) * 1.2, abs(n - 0.52)); // a pixel or two wide at any zoom

                // Puddles: blobs of low noise, shimmering between pink and cyan like reflected signs.
                float p = fbm(w * 0.02 + 17.3);
                float puddle = smoothstep(0.62, 0.68, p);
                float shimmer = noise(w * 0.3 + float2(_Time.y * 0.4, 0)) * 0.6 + 0.4;
                fixed3 reflect = lerp(_PuddleA.rgb, _PuddleB.rgb, noise(w * 0.05 + _Time.y * 0.05)) * shimmer;

                fixed3 rgb = _Seam.rgb; // alpha does the fading
                float a = seamA;
                rgb = lerp(rgb, reflect, puddle);
                a = max(a, puddle * 0.22);
                rgb = lerp(rgb, fixed3(0, 0, 0), crack);
                a = max(a, crack * 0.6 * (1 - puddle));
                return fixed4(rgb, a);
            }
            ENDCG
        }
    }
}
