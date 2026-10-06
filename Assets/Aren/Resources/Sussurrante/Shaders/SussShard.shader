// Lascas do Sussurrante (ParticleSystem): os "pixels" que se soltam do corpo na prancha. Quadrado
// nítido com miolo escuro e borda acesa (alfa pré-multiplicado: a lasca escura cobre o fundo e a
// borda soma luz). Cor/alfa da partícula: lascas escuras (alfa alto) ou faíscas (alfa baixo =
// quase só luz). Sem textura.
Shader "Hidden/Aren/SussShard"
{
    Properties { _Boost ("Brilho da borda", Float) = 2.2 }
    SubShader
    {
        Tags { "Queue"="Transparent+12" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend One OneMinusSrcAlpha
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
                float2 d = abs(i.uv - 0.5) * 2;
                float m = max(d.x, d.y);
                float inside = 1 - smoothstep(0.86, 0.96, m);
                float border = smoothstep(0.45, 0.86, m);
                // pixel cheio (como na prancha): miolo violeta escuro com um degrau mais claro para a
                // borda; a faísca (alfa baixo) é quase só luz
                float3 core = i.color.rgb * (0.45 + 0.25 * (1 - i.color.a));
                float3 edge = i.color.rgb * _Boost;
                float a = inside * i.color.a;
                float3 rgb = lerp(core, edge, border * 0.6) * inside * (0.25 + 0.75 * i.color.a) + edge * border * inside * (1 - i.color.a) * 0.6;
                return float4(rgb, a * 0.92);
            }
            ENDCG
        }
    }
}
