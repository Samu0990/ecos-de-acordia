// Nuvens volumétricas da noite (NightClouds): cartões no céu com nuvens renderizadas como VOLUME no
// Cycles (cloud_impostors.py) e iluminadas aqui pela técnica de 6 direções: o atlas guarda a nuvem
// iluminada da direita, esquerda, cima, baixo, de trás e de frente (+ ambiente e densidade); para cada
// luz da cena o shader passa a direção dela para o espaço do cartão e mistura os passes:
//   · a lua (fria, de cima) — topo claro, base escura;
//   · a Fenda (violeta) — as nuvens do lado do rasgo ganham a borda acesa e o contraluz;
//   · a cidade (laranja, de baixo) — as nuvens sobre a vila são iluminadas por baixo, como na arte;
//   · o impacto da nota (na abertura) — clarão quente;
//   · ambiente do céu; névoa/perspectiva aérea da noite pela distância.
Shader "Hidden/Campanula/CloudImpostor"
{
    Properties
    {
        _A ("Luz: direita, esquerda, cima, baixo", 2D) = "black" {}
        _B ("Luz: trás, frente, ambiente, alfa", 2D) = "black" {}
        _Opacity ("Opacidade", Range(0, 1)) = 0.9
        _CityGlow ("Brilho da cidade (baixo)", Color) = (1.0, 0.55, 0.25, 1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent-20" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma target 3.0
            #include "UnityCG.cginc"
            #include "NightCommon.cginc"
            sampler2D _A, _B;
            float _Opacity;
            float4 _CityGlow;
            UNITY_INSTANCING_BUFFER_START(P)
                UNITY_DEFINE_INSTANCED_PROP(float4, _Cell)    // xy = célula do atlas (0/1), z = espelho, w = brilho
            UNITY_INSTANCING_BUFFER_END(P)
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 wp : TEXCOORD1;
                float3 right : TEXCOORD2;
                float3 back : TEXCOORD3;
                float4 cell : TEXCOORD4;
                float3 center : TEXCOORD5;
            };
            v2f vert (appdata v)
            {
                UNITY_SETUP_INSTANCE_ID(v);
                v2f o;
                float4 cell = UNITY_ACCESS_INSTANCED_PROP(P, _Cell);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.wp = mul(unity_ObjectToWorld, v.vertex).xyz;
                float2 uv = v.uv;
                if (cell.z > 0.5) uv.x = 1 - uv.x;
                o.uv = (uv + cell.xy) * 0.5;
                // eixos do cartão: direita = X do objeto (espelhada se for o caso), trás = +Z (para longe da vila)
                o.right = normalize(mul((float3x3)unity_ObjectToWorld, float3(cell.z > 0.5 ? -1 : 1, 0, 0)));
                o.back = normalize(mul((float3x3)unity_ObjectToWorld, float3(0, 0, 1)));
                o.cell = cell;
                o.center = mul(unity_ObjectToWorld, float4(0, 0, 0, 1)).xyz;
                return o;
            }

            // luz vinda da direção d (mundo), misturando os 6 passes
            float Six(float3 d, float3 right, float3 back, float4 A, float4 B)
            {
                float lx = dot(d, right), ly = d.y, lz = dot(d, back);
                float3 w2 = float3(lx * lx, ly * ly, lz * lz);
                w2 /= max(dot(w2, 1), 1e-4);
                float x = lx > 0 ? A.r : A.g;
                float y = ly > 0 ? A.b : A.a;
                float z = lz > 0 ? B.r : B.g;
                return x * w2.x + y * w2.y + z * w2.z;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float4 A = tex2D(_A, i.uv);
                float4 B = tex2D(_B, i.uv);
                float alpha = pow(B.a, 1.7) * _Opacity;   // borda macia (a densidade cai devagar)
                clip(alpha - 0.004);
                float3 V = i.wp - _WorldSpaceCameraPos;
                float dist = length(V);
                V /= dist;
                float3 right = normalize(i.right), back = normalize(i.back);

                float3 col = _NightAmbSky.rgb * B.b * 0.9;
                // lua
                col += _NightMoonCol.rgb * Six(normalize(_NightMoonDir.xyz), right, back, A, B) * 0.55;
                // a Fenda: mais forte nas nuvens do lado dela
                float3 F = normalize(_FendaDirW.xyz + 1e-4);
                float side = pow(saturate(dot(normalize(i.center.xz), normalize(F.xz + 1e-4)) * 0.5 + 0.5), 5.0);
                float sf = Six(F, right, back, A, B);
                col += _FendaLight.rgb * sf * sf * (0.15 + 0.9 * side);   // ao quadrado: só a borda virada para o rasgo acende
                // a cidade embaixo: nuvens perto da vila são acesas por baixo (passe "baixo")
                float overCity = exp(-dot(i.center.xz, i.center.xz) / (1400.0 * 1400.0));
                col += _CityGlow.rgb * A.a * overCity * 0.28;
                // o impacto da nota (abertura)
                float3 toI = _ImpactPosW.xyz - i.center;
                float di = length(toI);
                col += _ImpactLight.rgb * Six(toI / max(di, 1.0), right, back, A, B) * (1.0 / (1.0 + pow(di / 1600.0, 2.0))) * 2.5;
                col *= i.cell.w;

                // perspectiva aérea: longe a nuvem vira a cor do ar
                float fogT = saturate(NightFog(_WorldSpaceCameraPos, i.wp, dist) * 0.8 + dist / 9000.0);
                col = lerp(col, NightHaze(V), fogT);
                alpha *= 1 - fogT * 0.5;
                return fixed4(col, alpha);
            }
            ENDCG
        }
    }
}
