// World-space grid lines that fade out around _Center (the placement ghost).
Shader "RTS/GridFade"
{
    Properties
    {
        _Color ("Color", Color) = (0.2, 1, 1, 0.5)
        _Cell ("Cell Size (m)", Float) = 2
        _LineWidth ("Line Width (m)", Float) = 0.06
        _Center ("Center (world)", Vector) = (0, 0, 0, 0)
        _Radius ("Fade Radius (m)", Float) = 14
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color;
            float _Cell, _LineWidth, _Radius;
            float4 _Center;

            struct v2f { float4 pos : SV_POSITION; float3 world : TEXCOORD0; };

            v2f vert (appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.world = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 p = i.world.xz;
                // Distance (m) to the nearest grid line on each axis; lines sit on multiples of _Cell.
                float2 d = abs(frac(p / _Cell + 0.5) - 0.5) * _Cell;
                float2 aa = fwidth(p);
                float2 l = 1 - smoothstep(_LineWidth * 0.5, _LineWidth * 0.5 + aa, d);
                float onLine = max(l.x, l.y);

                float fade = 1 - smoothstep(_Radius * 0.55, _Radius, distance(p, _Center.xz));
                return fixed4(_Color.rgb, _Color.a * onLine * fade);
            }
            ENDCG
        }
    }
}
