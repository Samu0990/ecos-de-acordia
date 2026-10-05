// Cacos de rocha em volta da Fenda (FendaShards): a realidade estilhaçada do storyboard. Ficam a ~12 km
// (atrás da paisagem, como o rasgo do céu) e a luz é FALSA, toda no shader (como os detritos do "vazio"
// de Moss 2): a fonte é o eixo do rasgo (uma linha vertical de luz), com queda pela distância a ele.
//   · face virada para o rasgo: violeta claro (difusa enrolada);
//   · contorno (fresnel) do lado da luz: aro forte — contra o brilho de dentro o caco vira silhueta
//     escura com a borda acesa, como na arte;
//   · o resto: rocha quase preta com um pouco de lua fria e ambiente;
//   · _Heat (por caco): quando nasce o caco é LUZ (branco-violeta) e esfria até virar pedra; as faces
//     de fratura (cor de vértice R) ficam em brasa por mais tempo, com veias de rachadura;
//   · névoa de altura da noite (NightFog/NightHaze) — lá em cima ela é fina, o caco fica nítido.
Shader "Hidden/Aren/FendaShard"
{
    Properties
    {
        [HDR] _LightCol ("Luz da Fenda", Color) = (0.62,0.42,1.25,1)
        [HDR] _CoreCol ("Núcleo (aro mais perto)", Color) = (1.3,1.25,1.9,1)
        [HDR] _HeatCol ("Brasa ao nascer", Color) = (2.2,1.6,3.6,1)
        _Rock ("Rocha", Color) = (0.055,0.05,0.065,1)
        _Cracks ("Rachaduras", 2D) = "black" {}
        _Falloff ("Alcance da luz do eixo (m)", Float) = 2200
    }
    SubShader
    {
        Tags { "Queue"="Geometry+30" "RenderType"="Opaque" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma target 3.0
            #include "UnityCG.cginc"
            #include "NightCommon.cginc"
            sampler2D _Cracks;
            float4 _LightCol, _CoreCol, _HeatCol, _Rock;
            float _Falloff;
            float4 _FendaAxis;    // xyz = base do eixo (mundo), w = y do topo
            float4 _FendaShardK;  // x = brilho geral (abertura × pulso), y = lampejo, z = quanto o eixo é visível
            UNITY_INSTANCING_BUFFER_START(P)
                UNITY_DEFINE_INSTANCED_PROP(float, _Heat)
            UNITY_INSTANCING_BUFFER_END(P)
            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; float4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos : SV_POSITION; float3 wp : TEXCOORD0; float3 n : TEXCOORD1; float4 col : TEXCOORD2; float3 lp : TEXCOORD3; UNITY_VERTEX_INPUT_INSTANCE_ID };
            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.wp = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.n = UnityObjectToWorldNormal(v.normal);
                o.col = v.color;
                o.lp = v.vertex.xyz;
                return o;
            }
            fixed4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float heat = UNITY_ACCESS_INSTANCED_PROP(P, _Heat);
                float3 N = normalize(i.n);
                float3 toC = _WorldSpaceCameraPos - i.wp;
                float L = length(toC);
                float3 V = toC / L;
                // o eixo de luz: o ponto mais próximo da linha vertical do rasgo
                float3 ap = float3(_FendaAxis.x, clamp(i.wp.y, _FendaAxis.y, _FendaAxis.w), _FendaAxis.z);
                float3 toL = ap - i.wp;
                float dl = length(toL);
                float3 Ld = toL / max(dl, 1e-3);
                float near = 1.0 / (1.0 + pow(dl / _Falloff, 2.0));            // mais claro perto do rasgo
                float k = _FendaShardK.x * (1.0 + 1.4 * _FendaShardK.y);
                float ndl = dot(N, Ld);
                float wrap = saturate(ndl * 0.6 + 0.4); wrap *= wrap;
                float fres = pow(1.0 - saturate(dot(N, V)), 3.0);
                float rimSide = saturate(ndl + 0.45);
                // contraluz: o rasgo atrás do caco (luz vindo "de dentro da tela") acende mais o contorno
                float back = saturate(-dot(V, Ld)) * 0.6 + 0.4;

                float top = i.col.g, frac = i.col.r, seed = i.col.b;
                float3 rock = _Rock.rgb * (0.75 + 0.5 * seed) * (0.7 + 0.3 * top);
                rock = lerp(rock, rock * 1.6 + float3(0.01, 0.008, 0.02), frac);      // pedra recém-partida, mais clara

                float3 lightC = lerp(_LightCol.rgb, _CoreCol.rgb, near * near);
                float3 col = rock * (_NightAmbSky.rgb * 0.6 + _NightMoonCol.rgb * saturate(dot(N, normalize(_NightMoonDir.xyz))) * 0.5);
                float3 litStone = float3(0.15, 0.14, 0.17) * (0.8 + 0.4 * seed) * (1 + 0.5 * frac);
                col += litStone * lightC * wrap * near * k * 5.0;                    // a face que vê o rasgo (pedra cinza-violeta)
                col += lightC * fres * rimSide * back * (0.25 + near) * k * 3.4;     // o aro
                col += lightC * pow(saturate(ndl), 12.0) * near * k * 0.25 * frac;   // brilho nas faces de fratura viradas para a luz

                // nascer: o caco é luz e esfria até a pedra (fratura e rachaduras por último)
                float3 tp = i.lp * 1.7 + seed * 3.1;
                float3 an = abs(N);
                float cr = (tex2D(_Cracks, tp.yz).r * an.x + tex2D(_Cracks, tp.xz).r * an.y + tex2D(_Cracks, tp.xy).r * an.z) / (an.x + an.y + an.z);
                float h1 = saturate(heat * 1.6 - 0.6);                                 // corpo todo (some antes)
                float h2 = saturate(heat * 1.25);                                      // fratura e veias (some depois)
                float3 hc = lerp(_HeatCol.rgb, _CoreCol.rgb * 1.4, h1);
                col = lerp(col, hc * (0.8 + 0.4 * fres), h1 * h1);
                col += hc * h2 * h2 * (frac * 0.9 + smoothstep(0.35, 0.8, cr) * 1.6) * (1 - h1 * 0.5);

                // névoa de altura da noite (fina lá em cima) e a cor do ar na direção da Fenda
                float fog = NightFog(_WorldSpaceCameraPos, i.wp, L) * 0.85;
                col = lerp(col, NightHaze(-V), fog);
                return float4(col, 1);
            }
            ENDCG
        }
    }
}
