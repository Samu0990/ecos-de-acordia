// Tela inicial: faixa de névoa em pixel art (ruído quantizado com pontilhado, blocos de
// 4 px da arte) em duas camadas que correm em velocidades diferentes. Some perto do
// painel central para não passar por cima do título e dos botões.
Shader "Hidden/Aren/UITitleFog"
{
    Properties
    {
        [PerRendererData] _MainTex ("Névoa", 2D) = "white" {}
        _FogColor ("Cor", Color) = (0.36,0.31,0.44,0.2)
        _Tiling ("Repetição (x, y)", Vector) = (2.6,1,0,0)
        _Scroll ("Deslocamento camada 1 (x) / 2 (y)", Vector) = (0,0,0,0)
        _Band ("Faixa em px da arte (x0, y0, w, h)", Vector) = (0,760,1672,181)
        _Panel ("Painel em px da arte (x0, y0, x1, y1)", Vector) = (528,22,1146,905)
        _Reveal ("Revelação (centro xy, raio, suavidade)", Vector) = (836, 116, 100000, 90)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" }
        Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex; fixed4 _FogColor; float4 _Tiling, _Scroll, _Band, _Panel, _Reveal;
            struct appdata { float4 vertex : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; };
            v2f vert (appdata v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.uv = v.uv; o.color = v.color; return o; }
            fixed4 frag (v2f i) : SV_Target
            {
                float2 uv = float2(i.uv.x, 1 - i.uv.y);            // y para baixo, como a arte
                float a1 = tex2D(_MainTex, float2(uv.x * _Tiling.x + _Scroll.x, uv.y * _Tiling.y)).a;
                float a2 = tex2D(_MainTex, float2(uv.x * _Tiling.x * 0.71 - _Scroll.y + 0.37, uv.y * _Tiling.y * 0.9 + 0.05)).a;
                float a = saturate(a1 * 0.65 + a2 * 0.55);
                // afasta do painel (borda suave de ~40 px da arte), em blocos de 4 px
                float2 p = _Band.xy + uv * _Band.zw;
                p = floor(p / 4) * 4;
                float dx = max(_Panel.x - p.x, p.x - _Panel.z);
                float dy = max(_Panel.y - p.y, p.y - _Panel.w);
                float outside = saturate(max(dx, dy) / 40 + 0.15);
                a *= outside;
                a *= saturate((_Reveal.z - length(p - _Reveal.xy)) / _Reveal.w);
                a = floor(a * 6) / 6;
                return fixed4(_FogColor.rgb, a * _FogColor.a) * i.color;
            }
            ENDCG
        }
    }
}
