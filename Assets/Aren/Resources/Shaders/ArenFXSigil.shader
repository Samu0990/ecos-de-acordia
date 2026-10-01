// Sigilo do Contracanto no chão: anéis concêntricos + marcas radiais tipo dial de afinação,
// girando; _Charge (0..1) acende anel por anel.
Shader "Aren/FX/Sigil"
{
    Properties
    {
        [HDR] _Color ("Cor", Color) = (1.6,1.1,0.45,1)
        _Charge ("Carga", Range(0,1)) = 0
        _Spin ("Giro", Float) = 0
        _Fade ("Fade", Range(0,1)) = 1
    }
    SubShader
    {
        Tags { "Queue"="Transparent+12" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha One
        ZWrite Off Cull Off Lighting Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float4 _Color; float _Charge, _Spin, _Fade;
            struct a2v { float4 v : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 p : SV_POSITION; float2 uv : TEXCOORD0; };
            v2f vert (a2v i) { v2f o; o.p = UnityObjectToClipPos(i.v); o.uv = i.uv * 2 - 1; return o; }
            float band(float r, float c, float w) { float d = (r - c) / w; return exp(-d * d * 3); }
            fixed4 frag (v2f i) : SV_Target
            {
                float r = length(i.uv);
                float ang = atan2(i.uv.y, i.uv.x) + _Spin;
                float a1 = band(r, 0.35, 0.025) * smoothstep(0.0, 0.33, _Charge);
                float a2 = band(r, 0.62, 0.02) * smoothstep(0.33, 0.66, _Charge);
                float a3 = band(r, 0.9, 0.03) * smoothstep(0.66, 1.0, _Charge);
                // marcas radiais (12, como os sinos de Campanula) entre os anéis 2 e 3
                float ticks = pow(saturate(cos(ang * 12) ), 40) * step(0.66, r) * step(r, 0.86) * smoothstep(0.2, 0.7, _Charge);
                // escrita ondulada entre 1 e 2
                float wave = band(r, 0.48 + sin(ang * 7 - _Spin * 3) * 0.03, 0.012) * smoothstep(0.15, 0.5, _Charge);
                float core = exp(-r * r * 30) * 0.4 * _Charge;
                float a = (a1 + a2 + a3 + ticks + wave * 0.8 + core) * _Fade * (1 - smoothstep(0.96, 1, r));
                return float4(_Color.rgb, saturate(a));
            }
            ENDCG
        }
    }
}
