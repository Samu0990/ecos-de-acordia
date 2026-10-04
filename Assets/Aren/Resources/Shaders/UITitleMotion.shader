// Fundo da tela inicial em movimento ("motion" estilo After Effects, em tempo real):
// a arte é deslocada por pixel conforme os mapas de ArtSource/Menu/scripts/motion_maps.py —
// paralaxe 2.5D com a câmera passeando e um zoom lento (o painel tem profundidade zero e fica
// parado), lanterna/lustre/cruz balançando como pêndulos, o estandarte ondulando, o ar
// tremendo acima das chamas e a luz das velas pulsando no cenário. A amostragem final é a
// mesma "pixel nítido" do UITitleArt, então a pixel art continua com borda firme.
Shader "Hidden/Aren/UITitleMotion"
{
    Properties
    {
        [PerRendererData] _MainTex ("Arte", 2D) = "white" {}
        _MotionA ("Paralaxe / lanterna / lustre / estandarte", 2D) = "gray" {}
        _MotionB ("Calor / cruz / luz direita / luz esquerda", 2D) = "black" {}
        _Art ("Arte (largura, altura, centro x, centro y)", Vector) = (1672, 941, 836, 470)
        _Cam ("Câmera (x, y, zoom, tempo)", Vector) = (0, 0, 0, 0)
        _Angles ("Pêndulos (lanterna, lustre, cruz)", Vector) = (0, 0, 0, 0)
        _Piv1 ("Pivô lanterna (xy) e lustre (zw)", Vector) = (340, 40, 1275, 0)
        _Piv2 ("Pivô cruz (xy), topo do estandarte (z)", Vector) = (1421, 0, 345, 0)
        _Flicker ("Luz esquerda, luz direita, calor", Vector) = (0.5, 0.5, 1, 0)
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
            #pragma target 3.0
            #include "UnityCG.cginc"
            sampler2D _MainTex, _MotionA, _MotionB; float4 _MainTex_TexelSize;
            float4 _Art, _Cam, _Angles, _Piv1, _Piv2, _Flicker;
            struct appdata { float4 vertex : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; };
            v2f vert (appdata v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.uv = v.uv; o.color = v.color; return o; }

            float2 Rot (float2 q, float2 pivot, float a)
            {
                float s, c; sincos(a, s, c);
                float2 d = q - pivot;
                return pivot + float2(c * d.x - s * d.y, s * d.x + c * d.y);
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 P = float2(i.uv.x * _Art.x, (1 - i.uv.y) * _Art.y);   // px da arte, y para baixo
                float4 ma = tex2D(_MotionA, i.uv);
                float4 mb = tex2D(_MotionB, i.uv);
                float t = _Cam.w;
                float p = ma.r * 2 - 1;
                // câmera: paralaxe + zoom, ambos proporcionais à profundidade
                float2 src = P - p * (_Cam.xy + (P - _Art.zw) * _Cam.z);
                // pêndulos (gira a posição de leitura ao contrário do balanço)
                src = Rot(src, _Piv1.xy, -_Angles.x * ma.g);
                src = Rot(src, _Piv1.zw, -_Angles.y * ma.b);
                src = Rot(src, _Piv2.xy, -_Angles.z * mb.g);
                // estandarte: onda descendo o pano, presa em cima
                float bk = ma.a * saturate((P.y - _Piv2.z) / 240);
                src.x -= bk * (sin(P.y * 0.05 - t * 2.3) * 2.6 + sin(P.y * 0.11 - t * 3.7) * 0.9);
                src.y -= bk * sin(P.y * 0.03 - t * 1.7) * 0.6;
                // ar quente acima das chamas
                float hz = mb.r * _Flicker.z;
                src.x -= hz * (sin(P.y * 0.55 - t * 9.0) * 0.9 + sin(P.y * 1.3 + t * 13.0) * 0.4);
                float2 uv = float2(src.x / _Art.x, 1 - src.y / _Art.y);

                float2 tex = uv * _MainTex_TexelSize.zw;
                float2 w = max(fwidth(tex), 1e-4);
                float2 f = frac(tex) - 0.5;
                float2 sharp = (floor(tex) + 0.5 + clamp(f / w, -0.5, 0.5)) * _MainTex_TexelSize.xy;
                uv = max(w.x, w.y) < 1.0 ? sharp : uv;
                fixed4 c = tex2Dgrad(_MainTex, uv, ddx(i.uv), ddy(i.uv));
                // luz das velas pulsando no cenário (±25 %)
                c.rgb *= 1 + mb.b * (_Flicker.y - 0.5) * 0.5 + mb.a * (_Flicker.x - 0.5) * 0.5;
                c.a = 1;
                return c * i.color;
            }
            ENDCG
        }
    }
}
