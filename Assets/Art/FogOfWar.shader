// Map-sized quad drawn over everything (ZTest Always): each screen pixel is shaded by the fog cell of the
// ground point under it. _FogTex: R = visible now, G = explored. Bilinear filtering softens the edges.
Shader "RTS/FogOfWar"
{
    Properties
    {
        _FogTex ("Fog (R visible, G explored)", 2D) = "black" {}
        _Color ("Fog colour", Color) = (0.02, 0.01, 0.05, 1)
        _Explored ("Explored but not visible", Range(0, 1)) = 0.6
        _Unexplored ("Never seen", Range(0, 1)) = 0.94
        _MapSize ("Map size (m)", Float) = 600
    }
    SubShader
    {
        Tags { "Queue" = "Transparent+100" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        ZWrite Off
        ZTest Always
        Cull Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _FogTex;
            fixed4 _Color;
            float _Explored, _Unexplored, _MapSize;

            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            v2f vert (appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                float3 world = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.uv = world.xz / _MapSize + 0.5;
                return o;
            }

            float hash (float n) { return frac(sin(n) * 43758.5453); }

            fixed4 frag (v2f i) : SV_Target
            {
                // Glitch: thin horizontal bands of the edge jump sideways for a moment.
                float2 world = (i.uv - 0.5) * _MapSize;
                float band = floor(world.y * 0.5);
                float tick = floor(_Time.y * 8);
                float jump = step(0.93, hash(band * 1.7 + tick)) * (hash(band + tick * 3.1) - 0.5) * 4 / _MapSize;
                fixed2 f = tex2D(_FogTex, i.uv + float2(jump, 0)).rg;
                // FogOfWar blurs the cells (smooth curves); smoothstep tightens that soft ramp back to ~10 m.
                float vis = smoothstep(0.25, 0.75, f.r);
                float a = lerp(_Unexplored, _Explored, smoothstep(0.2, 0.8, f.g)) * (1 - vis);

                // Where sight ends: a thin cyan scanline fringe that scrolls slowly.
                float edge = saturate(1 - abs(f.r - 0.5) * 7);
                edge = edge * edge * edge; // only the middle of the fade
                float scan = step(0.5, frac(world.y * 1.5 - _Time.y * 1.5)); // 1/3 m stripes drifting
                float glow = edge * scan;
                fixed3 rgb = lerp(_Color.rgb, fixed3(0.2, 0.9, 1.0), glow * 0.6);
                return fixed4(rgb, max(a, glow * 0.12));
            }
            ENDCG
        }
    }
}
