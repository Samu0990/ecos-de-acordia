// Folhas de capim e caules de trigo (Campanula.GrassField): malhas de touceiras desenhadas com GPU
// instancing. Cor do vértice: r = altura relativa na folha (0 raiz → 1 ponta), g = aleatório da folha,
// b = altura da folha (m ÷ 2), a = 1 na espiga do trigo.
//   · vento: rajadas que correm pelo campo (ruído rolando na direção do vento) + balanço de cada
//     touceira; dobra mais na ponta (h²), como uma haste presa na raiz;
//   · o jogador abre o capim por onde passa (_GrassPush);
//   · some aos poucos na borda da distância (encolhe para a raiz; as touceiras "sorteadas" somem antes)
//     — nada de estalo quando um pedaço entra ou sai;
//   · cor: raiz escura → ponta clara, as mesmas manchas secas do terreno (Campanula/Terrain usa o mesmo
//     ruído e as mesmas escalas), variação por folha;
//   · luz: lua com difusa "enrolada" (a folha é fina), translucidez quando a lua está atrás da folha,
//     ambiente, sombra recebida, a luz da cidade (CityLight) e o brilho violeta da Fenda nas pontas.
Shader "Hidden/Campanula/GrassBlades"
{
    Properties
    {
        _Root ("Raiz", Color) = (0.1,0.13,0.06,1)
        _Tip ("Ponta", Color) = (0.42,0.5,0.24,1)
        _Ear ("Espiga", Color) = (0.86,0.7,0.38,1)
        _DryTint ("Manchas secas", Color) = (1.25,1.05,0.62,1)
        _GrassMacro ("Ruído de variação", 2D) = "grey" {}
        _Sway ("Força do vento", Float) = 1
    }
    SubShader
    {
        Tags { "Queue"="Geometry+10" "RenderType"="Opaque" "IgnoreProjector"="True" }
        Cull Off
        Pass
        {
            Tags { "LightMode"="ForwardBase" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_instancing
            #pragma multi_compile_fwdbase nolightmap nodynlightmap novertexlight
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            #include "AutoLight.cginc"
            #include "Lighting.cginc"

            float4 _Root, _Tip, _Ear, _DryTint;
            float _Sway;
            sampler2D _GrassMacro;
            float4 _GrassDist;   // x = distância de corte, y = distância do trigo
            float4 _GrassWind;   // xy = direção, z = força, w = velocidade
            float4 _GrassPush;   // xyz = jogador, w = raio
            sampler2D _CityLightTex;
            float4 _CityLightRect;
            float _CityLightK;
            float4 _FendaLight, _FendaDirW;

            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; float4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 wp : TEXCOORD0;
                float3 n : TEXCOORD1;
                float4 col : TEXCOORD2;
                float4 root : TEXCOORD3;     // xyz raiz, w aleatório da touceira
                SHADOW_COORDS(4)
                UNITY_FOG_COORDS(5)
            };

            float Hash(float2 p) { return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453); }

            v2f vert (appdata v)
            {
                UNITY_SETUP_INSTANCE_ID(v);
                v2f o;
                float3 root = float3(unity_ObjectToWorld._m03, unity_ObjectToWorld._m13, unity_ObjectToWorld._m23);
                float3 wp = mul(unity_ObjectToWorld, v.vertex).xyz;
                float h = v.color.r;
                float rnd = Hash(root.xz);
                bool wheat = _Sway > 1.2;
                float R = wheat ? _GrassDist.y : _GrassDist.x;

                // borda da distância: encolhe para a raiz; as touceiras sorteadas somem antes (afina o campo)
                float d = distance(root, _WorldSpaceCameraPos);
                float edge = saturate((R - d) / (R * 0.28));
                float thin = saturate(1.15 - max(0, d / R - 0.4) * 1.5);
                float keep = edge * saturate((thin - rnd) * 6 + 0.5);
                wp = root + (wp - root) * keep;

                // vento: rajada (ruído rolando) + balanço; dobra na ponta
                float2 wd = normalize(_GrassWind.xy);
                float t = _Time.y * _GrassWind.w;
                float gust = tex2Dlod(_GrassMacro, float4(root.xz * 0.021 - wd * t * 0.045, 0, 0)).r;
                gust = smoothstep(0.3, 0.75, gust);
                float sway = sin(t * (wheat ? 1.5 : 2.3) + dot(root.xz, float2(0.61, 0.43)) + v.color.g * 6.283);
                float flutter = sin(t * 7.0 + v.color.g * 31.0 + root.x * 3.0) * 0.25;
                float amp = h * h * v.color.b * 2.0 * _GrassWind.z * _Sway;
                float2 off = wd * (gust * 0.42 + sway * 0.1 + 0.05) * amp + float2(-wd.y, wd.x) * (sway * 0.06 + flutter * 0.04) * amp;
                // o jogador abre o capim
                float3 pd = root - _GrassPush.xyz;
                float pl = length(pd.xz);
                float push = saturate(1 - pl / _GrassPush.w) * step(abs(pd.y), 2.5);
                off += normalize(pd.xz + 1e-4) * push * h * v.color.b * 2.0 * 0.75;
                wp.xz += off * keep;
                wp.y -= (dot(off, off) * 0.35 + push * h * 0.12) * keep;   // a haste dobrada fica mais baixa

                o.pos = mul(UNITY_MATRIX_VP, float4(wp, 1));
                o.wp = wp;
                o.n = UnityObjectToWorldNormal(v.normal);
                o.col = v.color;
                o.root = float4(root, rnd);
                TRANSFER_SHADOW(o);
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag (v2f i, fixed facing : VFACE) : SV_Target
            {
                float h = i.col.r;
                float wheatK = step(1.2, _Sway);
                float3 V = normalize(_WorldSpaceCameraPos - i.wp);
                float3 N = normalize(i.n) * (facing > 0 ? 1 : -1);
                N = normalize(lerp(N, float3(0, 1, 0), 0.45));

                // cor: raiz → ponta, manchas secas do terreno, variação por folha
                float m1 = tex2D(_GrassMacro, i.root.xz / 160.0).r;
                float m2 = tex2D(_GrassMacro, i.root.xz / 37.0 + 0.31).g;
                float m3 = tex2D(_GrassMacro, i.root.xz / 9.0 + 0.7).b;
                float dry = smoothstep(0.42, 0.75, m1 * 0.6 + m2 * 0.4);
                float3 base = lerp(_Root.rgb, _Tip.rgb, smoothstep(0.0, 0.9, h));
                base *= lerp(float3(0.92, 1.03, 0.9), _DryTint.rgb, dry);
                base *= 0.8 + 0.35 * i.col.g + 0.2 * (m3 - 0.5);
                base = lerp(base, _Ear.rgb * (0.85 + 0.3 * i.col.g), i.col.a);
                float ao = lerp(0.35, 1.0, smoothstep(0.0, 0.7, h));
                // de longe as folhas finas viram cintilação: a cor e a luz ficam mais uniformes com a distância
                float df = saturate((distance(i.root.xyz, _WorldSpaceCameraPos) - 10.0) / 22.0);
                float3 avg = lerp(_Root.rgb, _Tip.rgb, 0.6) * lerp(float3(0.92, 1.03, 0.9), _DryTint.rgb, dry);
                avg = lerp(avg, _Ear.rgb, wheatK * 0.35);
                base = lerp(base, avg, df * 0.65);
                ao = lerp(ao, 0.75, df * 0.6);
                N = normalize(lerp(N, float3(0, 1, 0), df * 0.7));

                // luz da lua (ou do sol): difusa enrolada + translucidez
                float3 L = normalize(_WorldSpaceLightPos0.xyz);
                float ndl = saturate(dot(N, L) * 0.6 + 0.4);
                float trans = pow(saturate(dot(-V, L)), 4.0) * h * (wheatK > 0.5 ? 0.35 : 0.9);
                float atten = SHADOW_ATTENUATION(i);
                float3 amb = ShadeSH9(float4(N, 1));
                float3 col = base * (_LightColor0.rgb * (ndl + trans) * atten + amb) * ao;
                // brilho especular fraco nas pontas (folha encerada)
                float3 Hh = normalize(L + V);
                col += _LightColor0.rgb * pow(saturate(dot(N, Hh)), 24.0) * 0.06 * h * atten;

                // luz da cidade: o capim perto das janelas e lanternas fica dourado
                if (_CityLightK > 0.001)
                {
                    float2 cu = (i.root.xz - _CityLightRect.xy) * _CityLightRect.zw;
                    if (cu.x > 0 && cu.y > 0 && cu.x < 1 && cu.y < 1)
                    {
                        float4 Lc = tex2D(_CityLightTex, cu);
                        float att = exp(-max(0, Lc.a - i.wp.y - 1.0) / 6.0);
                        col += base * Lc.rgb * att * _CityLightK * 1.3 * (0.5 + 0.5 * h);
                    }
                }
                // a Fenda: contraluz violeta nas pontas viradas para ela
                col += base * _FendaLight.rgb * saturate(dot(N, normalize(_FendaDirW.xyz + 1e-4)) + 0.35) * 0.45 * h;

                UNITY_APPLY_FOG(i.fogCoord, col);
                return fixed4(col, 1);
            }
            ENDCG
        }
    }
}
