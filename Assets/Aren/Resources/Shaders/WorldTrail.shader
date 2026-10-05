// Rastro dos sete brilhos e das brasas (TrailRenderer): aditivo em HDR, linha central quente com
// halo macio, cor/transparência do gradiente do rastro, cintilação ao longo do comprimento
// (energia que vibra). Sem névoa (voam a quilômetros).
Shader "Hidden/Aren/WorldTrail"
{
    Properties { _Boost ("Intensidade", Float) = 1.4 }
    SubShader
    {
        Tags { "Queue"="Transparent+9" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha One
        ZWrite Off Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float _Boost;
            struct appdata { float4 vertex : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            v2f vert (appdata v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.color = v.color; o.uv = v.uv; return o; }
            fixed4 frag (v2f i) : SV_Target
            {
                float across = 1 - abs(i.uv.y - 0.5) * 2;
                float core = pow(saturate(across), 8.0);
                float soft = pow(saturate(across), 1.6);
                float shimmer = 0.75 + 0.25 * sin(i.uv.x * 60.0 - _Time.y * 30.0) * sin(i.uv.x * 23.0 + _Time.y * 11.0);
                float a = (soft * 0.55 + core * 0.9) * i.color.a * shimmer;
                float3 col = lerp(i.color.rgb, 1, core * 0.6) * _Boost;
                return float4(col, a);
            }
            ENDCG
        }
    }
}
