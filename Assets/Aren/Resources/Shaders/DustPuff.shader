// Poeira e fumaça (partículas e cartazes), misturada por alfa: bolota macia com textura de ruído
// que gira e se desfaz; iluminada por uma luz quente vinda de baixo/de um ponto (o impacto) e pela
// lua. Cor do vértice = tinta + alfa. Usada no pó do impacto e nas folhas/poeira da onda na vila.
Shader "Hidden/Aren/DustPuff"
{
    Properties
    {
        _Noise ("Ruído", 2D) = "gray" {}
        _Lit ("Luz quente", Color) = (1,0.6,0.3,1)
        _LitAmount ("Quanto da luz quente", Float) = 0
        _Shade ("Cor na sombra", Color) = (0.05,0.05,0.07,1)
        _Moon ("Luz da lua", Color) = (0.2,0.22,0.3,1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent+5" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _Noise;
            float4 _Lit, _Shade, _Moon;
            float _LitAmount;
            struct appdata { float4 vertex : POSITION; float4 color : COLOR; float4 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float4 color : COLOR; float4 uv : TEXCOORD0; };
            v2f vert (appdata v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.color = v.color; o.uv = v.uv; return o; }
            fixed4 frag (v2f i) : SV_Target
            {
                float2 uv = i.uv.xy - 0.5;
                float d = length(uv) * 2;
                float seed = i.uv.z;
                float ang = seed * 6.28 + _Time.y * 0.05;
                float2 r = float2(uv.x * cos(ang) - uv.y * sin(ang), uv.x * sin(ang) + uv.y * cos(ang));
                float n = tex2D(_Noise, r * 0.9 + seed * 3.7).r * 0.65 + tex2D(_Noise, r * 2.3 - seed * 1.9).g * 0.45;
                float body = saturate(1 - d) ;
                float a = saturate((n * body * 1.8 - 0.25) * 1.6) * i.color.a;
                // luz: de baixo (o lado de baixo do puff fica quente), e um pouco de lua por cima
                float lower = saturate(-uv.y * 2 + 0.3);
                float3 col = _Shade.rgb + _Moon.rgb * saturate(uv.y * 2 + 0.5) * 0.5 + _Lit.rgb * _LitAmount * (0.35 + lower) * (0.6 + n);
                return float4(col * i.color.rgb, a);
            }
            ENDCG
        }
    }
}
