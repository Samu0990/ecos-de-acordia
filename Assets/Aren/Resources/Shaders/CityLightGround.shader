// Luz da cidade no chão (ruas e praça são terreno, que não usa o Campanula/CityLit): uma malha que
// acompanha o terreno, aditiva, lendo o mesmo mapa de luz (CityLight). As poças quentes embaixo das
// janelas acesas, lanternas e tochas, somadas. Névoa do Unity (perto ela quase não age).
Shader "Hidden/Aren/CityLightGround"
{
    Properties { _Ground ("Albedo médio do chão", Color) = (0.32,0.29,0.26,1) }
    SubShader
    {
        Tags { "Queue"="Geometry+40" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend One One
        ZWrite Off
        Offset -1, -1
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            sampler2D _CityLightTex;
            float4 _CityLightRect, _Ground;
            float _CityLightK;
            struct v2f { float4 pos : SV_POSITION; float3 wp : TEXCOORD0; UNITY_FOG_COORDS(1) };
            v2f vert (appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.wp = mul(unity_ObjectToWorld, v.vertex).xyz;
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }
            fixed4 frag (v2f i) : SV_Target
            {
                float2 cu = (i.wp.xz - _CityLightRect.xy) * _CityLightRect.zw;
                float4 L = tex2D(_CityLightTex, cu);
                float att = exp(-max(0, L.a - i.wp.y - 1.0) / 6.0);
                float3 col = L.rgb * _Ground.rgb * att * _CityLightK * 1.1;
                UNITY_APPLY_FOG_COLOR(i.fogCoord, col, fixed4(0, 0, 0, 0));
                return float4(col, 1);
            }
            ENDCG
        }
    }
}
