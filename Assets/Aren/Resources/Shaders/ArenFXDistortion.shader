// Onda de pressão: refrata a tela num anel (GrabPass nomeado = 1 captura por frame,
// barato o bastante para Intel UHD; desligado na qualidade Baixa).
Shader "Aren/FX/Distortion"
{
    Properties
    {
        _Radius ("Raio", Range(0,1)) = 0.5
        _Width ("Espessura", Range(0.01,0.6)) = 0.18
        _Strength ("Força", Range(0,0.2)) = 0.04
        _Fade ("Fade", Range(0,1)) = 1
    }
    SubShader
    {
        Tags { "Queue"="Transparent+30" "RenderType"="Transparent" "IgnoreProjector"="True" }
        GrabPass { "_ArenGrab" }
        ZWrite Off Cull Off Lighting Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _ArenGrab; float _Radius, _Width, _Strength, _Fade;
            struct a2v { float4 v : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 p : SV_POSITION; float2 uv : TEXCOORD0; float4 g : TEXCOORD1; };
            v2f vert (a2v i) { v2f o; o.p = UnityObjectToClipPos(i.v); o.uv = i.uv * 2 - 1; o.g = ComputeGrabScreenPos(o.p); return o; }
            fixed4 frag (v2f i) : SV_Target
            {
                float r = length(i.uv);
                float d = (r - _Radius) / _Width;
                float k = exp(-d * d * 3) * _Fade * (1 - smoothstep(0.9, 1, r));
                float2 dir = r > 0.0001 ? i.uv / r : 0;
                float2 off = dir * k * _Strength * (d < 0 ? -1 : 1);
                float4 g = i.g; g.xy += off * g.w;
                fixed4 col = tex2Dproj(_ArenGrab, g);
                col.a = 1;
                return col;
            }
            ENDCG
        }
    }
}
