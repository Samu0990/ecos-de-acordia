// Rochas da natureza (ArtSource/Campanula/scripts/kit_nature.py): penedos, afloramentos, lajes, pedras soltas e
// entulho esculpidos em alta no Blender e assados num atlas (normal em espaço tangente + máscara: R oclusão de
// ambiente com o chão, G arestas, B fendas, A tom de cada estrato). Por cima do relevo assado:
//   · a foto do Poly Haven (lichen_rock) em projeção TRIPLANAR no mundo, na escala real (2 m) — sem costuras de
//     UV e sem esticar; a projeção segue o relevo assado (as fendas e lascas mudam a mistura dos eixos);
//   · musgo (mossy_rock, projetado de cima) onde a pedra olha para o céu, nas fendas e não nas arestas gastas;
//   · terra na base (a oclusão assada com o chão) e no fundo das fendas, arestas mais claras (gastas);
//   · variação de tom por rocha (ruído do mundo) e por estrato;
//   · a luz da cidade (CityLight) à noite, igual ao Campanula/CityLit.
// De longe (> ~70 m) o relevo da foto sai (só a cor): os afloramentos nos morros custam pouco.
// _WOOD (troncos e tocos): a casca vem pelo 2º UV em metros, com o veio ao longo do tronco, e a máscara A marca
// as pontas (cerne claro) em vez do tom dos estratos.
Shader "Campanula/NatureRock"
{
    Properties
    {
        [Normal] _Atlas ("Normal assado (atlas)", 2D) = "bump" {}
        _Mask ("Máscara assada (R oclusão, G arestas, B fendas, A tom)", 2D) = "white" {}
        _Rock ("Rocha (foto)", 2D) = "grey" {}
        [Normal] _RockN ("Rocha (normal)", 2D) = "bump" {}
        _Moss ("Musgo (foto)", 2D) = "grey" {}
        _Macro ("Ruído de variação", 2D) = "grey" {}
        _RockTile ("Tamanho real da foto da rocha (m)", Float) = 2
        _MossTile ("Tamanho real da foto do musgo (m)", Float) = 3
        _RockTint ("Tom da rocha", Color) = (0.82, 0.82, 0.8, 1)
        _Desat ("Tirar saturação da rocha", Range(0, 1)) = 0.5
        _MossTint ("Tom do musgo", Color) = (0.8, 0.92, 0.66, 1)
        _MossAmount ("Musgo", Range(0, 1)) = 0.7
        _DirtColor ("Terra (base e fendas)", Color) = (0.17, 0.14, 0.1, 1)
        _DetailN ("Força do relevo da foto", Range(0, 2)) = 0.9
        _AOK ("Força da oclusão assada", Range(0, 1)) = 1
        [Toggle(_WOOD)] _Wood ("Madeira: casca pelo 2º UV (veio ao longo do tronco)", Float) = 0
        _EndColor ("Cerne (pontas da madeira)", Color) = (0.5, 0.4, 0.29, 1)
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        LOD 300
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.5
        #pragma multi_compile_instancing
        #pragma shader_feature_local _WOOD
        sampler2D _Atlas, _Mask, _Macro;
        // as três fotos dividem um amostrador (repetição + anisotrópico)
        UNITY_DECLARE_TEX2D(_Rock);
        UNITY_DECLARE_TEX2D_NOSAMPLER(_RockN);
        UNITY_DECLARE_TEX2D_NOSAMPLER(_Moss);
        float _RockTile, _MossTile, _Desat, _MossAmount, _DetailN, _AOK;
        float4 _RockTint, _MossTint, _DirtColor, _EndColor;
        sampler2D _CityLightTex;
        float4 _CityLightRect;
        float _CityLightK;

        struct Input { float2 uv_Atlas; float2 uv2_Rock; float3 worldPos; float3 worldNormal; INTERNAL_DATA };

        float Lum(float3 c) { return dot(c, float3(0.3, 0.55, 0.15)); }

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            float3 wp = IN.worldPos;
            float dist = distance(wp, _WorldSpaceCameraPos);
            float4 mk = tex2D(_Mask, IN.uv_Atlas);
            float3 tnA = UnpackNormal(tex2D(_Atlas, IN.uv_Atlas));
            // base tangente no mundo (linhas da matriz do surface shader)
            float3 T = WorldNormalVector(IN, float3(1, 0, 0));
            float3 B = WorldNormalVector(IN, float3(0, 1, 0));
            float3 Ng = WorldNormalVector(IN, float3(0, 0, 1));
            float3 N1 = normalize(tnA.x * T + tnA.y * B + tnA.z * Ng);

        #ifdef _WOOD
            // madeira (troncos e tocos): a foto da casca pelo 2º UV em metros (u em volta, v ao longo do eixo); a
            // base tangente desse UV sai das derivadas na tela (o UV2 não tem tangentes próprias)
            float2 u2 = IN.uv2_Rock / _RockTile;
            float3 rock = UNITY_SAMPLE_TEX2D(_Rock, u2).rgb;
            float3 tnb = UnpackNormal(UNITY_SAMPLE_TEX2D_SAMPLER(_RockN, _Rock, u2));
            tnb.xy *= _DetailN;
            float3 dp1 = ddx(wp), dp2 = ddy(wp);
            float2 du1 = ddx(u2), du2 = ddy(u2);
            float3 pp2 = cross(dp2, Ng), pp1 = cross(Ng, dp1);
            float3 Tw = pp2 * du1.x + pp1 * du2.x, Bw = pp2 * du1.y + pp1 * du2.y;
            float im = rsqrt(max(max(dot(Tw, Tw), dot(Bw, Bw)), 1e-20));
            float3 Nw = normalize(Tw * im * tnb.x + Bw * im * tnb.y + N1 * tnb.z);
        #else
            // projeção triplanar da foto (pesos pelo relevo assado; UV espelhado do lado de trás corrigido)
            float3 bw = pow(abs(N1), 4); bw /= dot(bw, 1);
            float3 sgn = N1 >= 0 ? 1 : -1;
            float3 p = wp / _RockTile;
            float2 ux = float2(p.z * sgn.x, p.y), uy = float2(p.x * sgn.y, p.z), uz = float2(-p.x * sgn.z, p.y);
            float3 rock = UNITY_SAMPLE_TEX2D(_Rock, ux).rgb * bw.x + UNITY_SAMPLE_TEX2D(_Rock, uy).rgb * bw.y + UNITY_SAMPLE_TEX2D(_Rock, uz).rgb * bw.z;
            float3 Nw = N1;
            UNITY_BRANCH
            if (dist < 70)
            {
                float3 tx = UnpackNormal(UNITY_SAMPLE_TEX2D_SAMPLER(_RockN, _Rock, ux));
                float3 ty = UnpackNormal(UNITY_SAMPLE_TEX2D_SAMPLER(_RockN, _Rock, uy));
                float3 tz = UnpackNormal(UNITY_SAMPLE_TEX2D_SAMPLER(_RockN, _Rock, uz));
                float k = _DetailN * (1 - smoothstep(45, 70, dist));
                tx.xy *= k; ty.xy *= k; tz.xy *= k;
                tx.x *= sgn.x; ty.x *= sgn.y; tz.x *= -sgn.z;
                // "whiteout": o relevo da foto por cima do relevo assado
                tx = float3(tx.xy + N1.zy, abs(tx.z) * N1.x);
                ty = float3(ty.xy + N1.xz, abs(ty.z) * N1.y);
                tz = float3(tz.xy + N1.xy, abs(tz.z) * N1.z);
                Nw = normalize(tx.zyx * bw.x + ty.xzy * bw.y + tz.xyz * bw.z);
            }
        #endif

            // tom: menos saturado (pedra cinza da cidade), variação por rocha e por estrato
            float3 mac = tex2D(_Macro, wp.xz / 23.0).rgb;
            rock = lerp(rock, Lum(rock).xxx, _Desat) * _RockTint.rgb;
        #ifdef _WOOD
            rock *= 0.84 + 0.32 * mac.r;
            rock = lerp(rock, _EndColor.rgb * (0.6 + 0.6 * mk.r), mk.a);   // pontas: o cerne claro (A = madeira exposta)
        #else
            rock *= (0.84 + 0.32 * mac.r) * (0.9 + 0.2 * mk.a);           // A = tom de cada estrato
        #endif
            rock = lerp(rock, rock * float3(1.06, 1.0, 0.92), mac.g);   // umas mais quentes, outras mais frias
            rock *= 1 + mk.g * 0.35;                                    // arestas gastas mais claras

            // musgo: onde olha para o céu (relevo assado), mais nas fendas e na sombra, nunca nas arestas
            float3 moss = UNITY_SAMPLE_TEX2D_SAMPLER(_Moss, _Rock, wp.xz / _MossTile).rgb * _MossTint.rgb;
            float mw = N1.y + (mac.b - 0.5) * 0.7 + mk.b * 0.35 - mk.g * 0.5 + (1 - mk.r) * 0.2;
            mw = smoothstep(0.42, 0.78, mw) * _MossAmount;
            mw = saturate(mw * 1.5 - (1 - Lum(moss) * 2.2) * 0.35);       // borda irregular pela própria foto
            float3 alb = lerp(rock, moss, mw);

            // terra: na base (oclusão do chão) e no fundo das fendas
            float dirt = saturate((1 - mk.r) * 1.3 - 0.15) * 0.75 + mk.b * 0.3;
            alb = lerp(alb, _DirtColor.rgb, saturate(dirt) * (1 - mw * 0.6));
            float ao = lerp(1, mk.r, _AOK);
            alb *= lerp(1, ao, 0.45);   // a oclusão também sob a luz direta (lua): volume sem sombra em tempo real

            o.Albedo = alb;
            o.Normal = float3(dot(Nw, T), dot(Nw, B), dot(Nw, Ng));
            o.Smoothness = lerp(0.14, 0.04, mw) * (1 - dirt * 0.4);
            o.Metallic = 0;
            o.Occlusion = ao;

            float3 em = 0;
            if (_CityLightK > 0.001)
            {
                float2 cu = (wp.xz - _CityLightRect.xy) * _CityLightRect.zw;
                if (cu.x > 0 && cu.y > 0 && cu.x < 1 && cu.y < 1)
                {
                    float4 L = tex2Dlod(_CityLightTex, float4(cu, 0, 0));
                    float att = exp(-abs(wp.y - L.a) / 3.2);
                    em = alb * L.rgb * att * (0.85 + 0.3 * saturate(Nw.y)) * _CityLightK * ao;
                }
            }
            o.Emission = em;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
