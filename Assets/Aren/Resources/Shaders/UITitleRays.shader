// Tela inicial: feixes de luz fria caindo do teto da caverna, em degraus de pixel art, com
// poeira correndo dentro deles e respiração lenta. Somados à arte; somem perto do painel.
Shader "Hidden/Aren/UITitleRays"
{
    Properties
    {
        [PerRendererData] _MainTex ("(não usada)", 2D) = "white" {}
        _RayColor ("Cor", Color) = (0.6, 0.66, 0.95, 0.1)
        _Art ("Arte (largura, altura)", Vector) = (1672, 941, 0, 0)
        _Cam ("Câmera (x, y, zoom, tempo)", Vector) = (0, 0, 0, 0)
        _Panel ("Painel (x0, y0, x1, y1)", Vector) = (528, 0, 1147, 820)
        _Reveal ("Revelação (centro xy, raio, suavidade)", Vector) = (836, 116, 100000, 90)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" }
        Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend SrcAlpha One
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            fixed4 _RayColor; float4 _Art, _Cam, _Panel, _Reveal;
            struct appdata { float4 vertex : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; };
            v2f vert (appdata v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.uv = v.uv; o.color = v.color; return o; }

            float Beam (float2 P, float2 origin, float angle, float width, float len, float phase, float t)
            {
                float2 dir = float2(sin(angle), cos(angle));
                float2 n = float2(dir.y, -dir.x);
                float2 d = P - origin;
                float v = dot(d, dir), u = dot(d, n);
                float wv = width * (1 + saturate(v / len) * 0.9);          // abre para baixo
                float b = saturate(1 - abs(u) / wv); b *= b;
                float along = saturate(v / 80) * saturate(1 - v / len);
                float breathe = 0.6 + 0.4 * sin(t * 0.37 + phase) * sin(t * 0.23 + phase * 1.7);
                float dust = 0.72 + 0.28 * sin(v * 0.045 - t * 0.9 + phase);
                return b * along * breathe * dust;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 P = float2(i.uv.x * _Art.x, (1 - i.uv.y) * _Art.y);
                P = floor(P / 2) * 2 - _Cam.xy * 0.5;                        // grade de 2 px + paralaxe leve
                float t = _Cam.w;
                float r = Beam(P, float2(205, -30), 0.30, 34, 860, 0.0, t)
                        + Beam(P, float2(430, -30), 0.26, 20, 720, 2.0, t) * 0.8
                        + Beam(P, float2(1315, -30), -0.28, 32, 820, 4.0, t)
                        + Beam(P, float2(1505, -30), -0.33, 19, 680, 1.3, t) * 0.8;
                float dx = max(_Panel.x - P.x, P.x - _Panel.z);
                float dy = max(_Panel.y - P.y, P.y - _Panel.w);
                r *= saturate(max(dx, dy) / 50);
                r *= saturate((_Reveal.z - length(P - _Reveal.xy)) / _Reveal.w);
                r = floor(saturate(r) * 5) / 5;
                return fixed4(_RayColor.rgb, r * _RayColor.a) * i.color;
            }
            ENDCG
        }
    }
}
