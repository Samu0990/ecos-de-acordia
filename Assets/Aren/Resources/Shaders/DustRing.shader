// Cortina de poeira da onda de choque: cilindro aberto que cresce com a frente da onda. Ruído
// subindo e se abrindo, denso embaixo e em fiapos em cima, aceso por dentro pela luz do impacto,
// com a névoa da noite por cima (é longe). Alfa por _Intensity.
Shader "Hidden/Aren/DustRing"
{
    Properties
    {
        _Noise ("Ruído", 2D) = "gray" {}
        _Intensity ("Intensidade", Range(0,1)) = 1
        _Shade ("Poeira na sombra", Color) = (0.07,0.065,0.07,1)
        _Lit ("Poeira acesa", Color) = (1,0.55,0.25,1)
        _LitAmount ("Luz do impacto", Float) = 1
        _Scroll ("Rolagem", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent+2" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            #include "NightCommon.cginc"
            sampler2D _Noise;
            float _Intensity, _LitAmount, _Scroll;
            float4 _Shade, _Lit;
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float3 wp : TEXCOORD1; };
            v2f vert (appdata v) { v2f o; o.wp = mul(unity_ObjectToWorld, v.vertex).xyz; o.pos = UnityWorldToClipPos(o.wp); o.uv = v.uv; return o; }
            fixed4 frag (v2f i) : SV_Target
            {
                float2 uv = i.uv;
                float n1 = tex2D(_Noise, float2(uv.x * 9.0, uv.y * 0.9 - _Scroll * 0.6)).r;
                float n2 = tex2D(_Noise, float2(uv.x * 23.0 + 0.3, uv.y * 2.1 - _Scroll * 1.3)).g;
                float n = n1 * 0.7 + n2 * 0.5;
                float prof = smoothstep(0.0, 0.08, uv.y) * (1 - smoothstep(0.25, 1.0, uv.y));
                float a = saturate((n - 0.28 - uv.y * 0.3) * 2.6) * prof * _Intensity;
                float lit = (1 - uv.y * 0.8) * (0.6 + n2);
                float3 col = _Shade.rgb + _Lit.rgb * lit * _LitAmount + _NightMoonCol.rgb * 0.15 * uv.y;
                float dist = length(_WorldSpaceCameraPos - i.wp);
                float fogT = NightFog(_WorldSpaceCameraPos, i.wp, dist);
                col = lerp(col, NightHaze(normalize(i.wp - _WorldSpaceCameraPos)), fogT * 0.8);
                a *= saturate((dist - 12.0) / 45.0);   // perto da câmera some (a textura esticada fica feia de perto)
                return float4(col, a * (1 - fogT * 0.5));
            }
            ENDCG
        }
    }
}
