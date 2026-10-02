// Anel de ressonância procedural num quad (UV centrado). _Lobes deforma o anel como uma
// onda estacionária (identidade musical) sem textura.
Shader "Aren/FX/Ring"
{
    Properties
    {
        [HDR] _Color ("Cor", Color) = (0.6,0.95,1,1)
        _Radius ("Raio (0..1)", Range(0,1)) = 0.6
        _Width ("Espessura", Range(0.002,0.6)) = 0.08
        _Lobes ("Lóbulos da onda", Float) = 9
        _Wobble ("Amplitude da onda", Range(0,0.1)) = 0.012
        _Fade ("Fade", Range(0,1)) = 1
        _Inner ("Preenchimento interno", Range(0,1)) = 0.12
        _Space ("Nebulosa", 2D) = "black" {}
    }
    SubShader
    {
        Tags { "Queue"="Transparent+15" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha One
        ZWrite Off Cull Off Lighting Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float4 _Color; float _Radius, _Width, _Lobes, _Wobble, _Fade, _Inner; sampler2D _Space;
            struct a2v { float4 v : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 p : SV_POSITION; float2 uv : TEXCOORD0; };
            v2f vert (a2v i) { v2f o; o.p = UnityObjectToClipPos(i.v); o.uv = i.uv * 2 - 1; return o; }
            fixed4 frag (v2f i) : SV_Target
            {
                float r = length(i.uv);
                float ang = atan2(i.uv.y, i.uv.x);
                float rr = _Radius + sin(ang * _Lobes + _Time.y * 6) * _Wobble;
                float d = (r - rr) / _Width;
                float ring = exp(-d * d * 3.5);
                float fill = saturate(1 - r / max(rr, 0.001)) * _Inner * smoothstep(0, 0.4, r / max(rr, 0.001));
                float3 sp = tex2D(_Space, i.uv * 0.45 + 0.5 + float2(_Time.x * 0.4, 0)).rgb;
                float spl = dot(sp, float3(0.4, 0.4, 0.4));
                fill *= 0.6 + spl * 1.8;
                ring *= 0.85 + spl * 0.6;
                float a = (ring + fill) * _Fade * (1 - smoothstep(0.96, 1.0, r));
                return float4(_Color.rgb * (1 + ring * 0.6) + sp * fill * 0.8, saturate(a));
            }
            ENDCG
        }
    }
}
