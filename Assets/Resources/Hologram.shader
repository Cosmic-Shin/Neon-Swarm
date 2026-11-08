// Additive see-through hologram that is only drawn below _CutY (world height), with moving scanlines,
// a brighter rim and a hot band at the cut line. Used by ProductionFX to "print" a unit bottom-up.
Shader "GD5/Hologram"
{
    Properties
    {
        _Color ("Color", Color) = (0.3, 1, 1, 1)
        _CutY ("Cut height (world)", Float) = 1000
        _MainTex ("Picture (optional)", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Blend One One
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color;
            float _CutY;
            sampler2D _MainTex;
            float4 _MainTex_ST;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 world : TEXCOORD0;
                float3 normal : TEXCOORD1;
                float2 uv : TEXCOORD2;
            };

            v2f vert (appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.world = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.normal = UnityObjectToWorldNormal(v.normal);
                o.uv = TRANSFORM_TEX(v.texcoord, _MainTex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                clip(_CutY - i.world.y);
                float3 view = normalize(_WorldSpaceCameraPos - i.world);
                float rim = 1 - abs(dot(normalize(i.normal), view));
                float scan = 0.6 + 0.4 * sin(i.world.y * 40 - _Time.y * 8);
                float edge = saturate(1 - (_CutY - i.world.y) * 6); // hot band at the cut line
                float a = (0.15 + rim * rim * 0.8) * scan + edge * 1.5;
                fixed4 tex = tex2D(_MainTex, i.uv);
                return _Color * a * tex * tex.a;
            }
            ENDCG
        }
    }
}
