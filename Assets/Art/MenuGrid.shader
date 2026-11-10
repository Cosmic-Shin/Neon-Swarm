// Main menu background (on a full-screen RawImage): a synthwave scene - neon grid rolling toward you,
// a striped sun on the horizon, twinkling stars and faint scanlines. All maths, no textures.
Shader "GD5/MenuGrid"
{
    Properties
    {
        _MainTex ("Unused (RawImage)", 2D) = "white" {}
        _Grid ("Grid colour", Color) = (1, 0.15, 0.75, 1)
        _Sky ("Sky colour", Color) = (0.12, 0.02, 0.22, 1)
        _Sun ("Sun colour", Color) = (1, 0.45, 0.2, 1)
        _Speed ("Scroll speed", Float) = 0.6
    }
    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Opaque" "IgnoreProjector" = "True" }
        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Grid, _Sky, _Sun;
            float _Speed;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float hash (float2 p) { return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453); }

            fixed4 frag (v2f i) : SV_Target
            {
                float aspect = _ScreenParams.x / _ScreenParams.y;
                float2 uv = i.uv;
                const float horizon = 0.42;
                float3 col;

                if (uv.y < horizon)
                {
                    // Ground: a perspective grid. Depth grows toward the horizon; lines scroll toward the viewer.
                    float depth = 0.35 / (horizon - uv.y + 0.001);
                    float x = (uv.x - 0.5) * aspect * depth;
                    float z = depth + _Time.y * _Speed;
                    float2 g = abs(frac(float2(x, z)) - 0.5);
                    float width = 0.02 * depth;                           // further lines get thinner on screen
                    float gridLine = saturate(1 - min(g.x, g.y) / max(width, 0.0001));
                    float fade = saturate(1.2 - depth * 0.08);             // lines fade into the distance
                    col = lerp(float3(0.02, 0.0, 0.05), _Grid.rgb * 1.6, gridLine * fade);
                    col += _Grid.rgb * 0.35 * pow(saturate(1 - (horizon - uv.y) * 5), 3); // glow along the horizon
                }
                else
                {
                    // Sky: dark at the top, purple near the horizon, stars, and a sun with bars cut out of its lower half.
                    float h = (uv.y - horizon) / (1 - horizon);
                    col = lerp(_Sky.rgb * 1.6, float3(0.01, 0.0, 0.03), saturate(h * 1.4));
                    float2 sp = float2((uv.x - 0.5) * aspect, uv.y - horizon - 0.16);
                    float r = length(sp);
                    float sun = smoothstep(0.17, 0.165, r);
                    float bars = step(0.5, frac((uv.y - horizon) * 40 - _Time.y * 0.3)) + step(0.09, sp.y); // bars only low down
                    sun *= saturate(bars);
                    col = lerp(col, lerp(_Sun.rgb, float3(1, 0.9, 0.3), saturate(sp.y * 5 + 0.5)), sun);
                    col += _Sun.rgb * 0.25 * smoothstep(0.45, 0.15, r);      // haze around it
                    float2 cell = floor(uv * float2(160 * aspect, 160));
                    float star = step(0.996, hash(cell)) * (0.5 + 0.5 * sin(_Time.y * 3 + hash(cell + 7) * 30));
                    col += star * h * (1 - sun);
                }

                col *= 0.92 + 0.08 * sin(uv.y * _ScreenParams.y * 1.5); // scanlines
                return fixed4(col, 1);
            }
            ENDCG
        }
    }
}
