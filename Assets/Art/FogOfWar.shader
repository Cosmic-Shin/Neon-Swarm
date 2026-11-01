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

            fixed4 frag (v2f i) : SV_Target
            {
                fixed2 f = tex2D(_FogTex, i.uv).rg;
                float a = lerp(_Unexplored, _Explored, f.g) * (1 - f.r);
                return fixed4(_Color.rgb, a);
            }
            ENDCG
        }
    }
}
