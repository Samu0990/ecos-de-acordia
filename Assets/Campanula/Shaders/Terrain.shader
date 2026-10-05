// Terreno de Campânula (substitui o Nature/Terrain/Standard). O de antes tinha três defeitos à vista:
// longe de 70 m virava o "basemap" (uma textura resumida de 256 px — os morros lisos e as manchas
// rosadas na estrada do morro), repetia a mesma foto a cada 2–3 m e não tinha rocha nas encostas.
// Aqui, tudo calculado no pixel e em qualquer distância:
//   · 4 canais do splatmap (R capim, G terra/estrada, B calçamento, A campo) misturados PELA ALTURA de
//     cada foto (a luminância faz as vezes do mapa de altura: as pedras do caminho aparecem entre a
//     grama em vez de um degradê de lama);
//   · o capim tem duas fotos: o chão de perto (folhas, gravetos, 3 m) e o prado visto de drone (morros,
//     30 m), trocadas pela distância e por manchas de ruído — sem repetição visível de longe;
//   · variação de cor em grande escala (manchas mais secas/mais verdes de dezenas de metros);
//   · rocha com musgo nas encostas íngremes, projetada nos três eixos (paredes do cânion, ravinas dos
//     morros) — sem a textura esticada das paredes;
//   · normal map em todas as camadas (combinação "whiteout" no espaço do mundo);
//   · a luz da cidade (CityLight) já no terreno: as poças de luz das janelas sem a malha extra por cima.
Shader "Campanula/Terrain"
{
    Properties
    {
        [HideInInspector] _Control ("Control (RGBA)", 2D) = "red" {}
        [HideInInspector] _Splat0 ("L0", 2D) = "grey" {}
        [HideInInspector] _Splat1 ("L1", 2D) = "grey" {}
        [HideInInspector] _Splat2 ("L2", 2D) = "grey" {}
        [HideInInspector] _Splat3 ("L3", 2D) = "grey" {}
        [HideInInspector] _MainTex ("BaseMap (RGB)", 2D) = "grey" {}
        [HideInInspector] _Color ("Main Color", Color) = (1,1,1,1)

        _GNear ("Capim perto", 2D) = "grey" {}
        [Normal] _GNearN ("Capim perto (normal)", 2D) = "bump" {}
        _GMeadow ("Prado (drone)", 2D) = "grey" {}
        [Normal] _GMeadowN ("Prado (normal)", 2D) = "bump" {}
        _Path ("Terra / estrada", 2D) = "grey" {}
        [Normal] _PathN ("Terra (normal)", 2D) = "bump" {}
        _Cobble ("Calçamento", 2D) = "grey" {}
        [Normal] _CobbleN ("Calçamento (normal)", 2D) = "bump" {}
        _Field ("Campo (palha)", 2D) = "grey" {}
        _Rock ("Rocha das encostas", 2D) = "grey" {}
        [Normal] _RockN ("Rocha (normal)", 2D) = "bump" {}
        _Macro ("Ruído de variação", 2D) = "grey" {}
        _Tiles ("Repetição (m): perto, prado, terra, calçamento", Vector) = (3, 30, 3.2, 2)
        _Tiles2 ("Repetição (m): campo, rocha", Vector) = (5, 16, 0, 0)
        _GrassTint ("Tom do capim", Color) = (0.82, 0.9, 0.7, 1)
        _MeadowTint ("Tom do prado", Color) = (0.8, 0.86, 0.72, 1)
        _DryTint ("Manchas secas", Color) = (1.12, 0.98, 0.72, 1)
        _RockSlope ("Encosta vira rocha (início, fim)", Vector) = (0.42, 0.62, 0, 0)
        _NormalK ("Força do relevo", Range(0, 2)) = 1
    }

    SubShader
    {
        Tags { "Queue" = "Geometry-100" "RenderType" = "Opaque" "TerrainCompatible" = "True" }

        CGPROGRAM
        #pragma surface surf Standard vertex:TVert finalcolor:TFinal addshadow fullforwardshadows
        #pragma instancing_options assumeuniformscaling nomatrices nolightprobe nolightmap forwardadd
        #pragma multi_compile_fog
        #pragma target 3.5
        #include "UnityPBSLighting.cginc"

        #if defined(SHADER_API_GLCORE) || defined(SHADER_API_GLES3) || defined(SHADER_API_GLES)
            #undef TERRAIN_USE_SEPARATE_VERTEX_SAMPLER
        #endif

        sampler2D _Control;
        float4 _Control_TexelSize;
        #if defined(UNITY_INSTANCING_ENABLED)
            sampler2D _TerrainHeightmapTexture;
            sampler2D _TerrainNormalmapTexture;
            float4 _TerrainHeightmapRecipSize;
            float4 _TerrainHeightmapScale;
        #endif
        UNITY_INSTANCING_BUFFER_START(Terrain)
            UNITY_DEFINE_INSTANCED_PROP(float4, _TerrainPatchInstanceData)
        UNITY_INSTANCING_BUFFER_END(Terrain)

        // as 12 texturas das camadas dividem UM amostrador (repetição + anisotrópico): o limite é 16
        // amostradores por passada, e as fotos, o controle, o mapa de normais, a luz da cidade e a sombra
        // passavam disso
        UNITY_DECLARE_TEX2D(_GNear);
        UNITY_DECLARE_TEX2D_NOSAMPLER(_GNearN); UNITY_DECLARE_TEX2D_NOSAMPLER(_GMeadow); UNITY_DECLARE_TEX2D_NOSAMPLER(_GMeadowN);
        UNITY_DECLARE_TEX2D_NOSAMPLER(_Path); UNITY_DECLARE_TEX2D_NOSAMPLER(_PathN); UNITY_DECLARE_TEX2D_NOSAMPLER(_Cobble);
        UNITY_DECLARE_TEX2D_NOSAMPLER(_CobbleN); UNITY_DECLARE_TEX2D_NOSAMPLER(_Field); UNITY_DECLARE_TEX2D_NOSAMPLER(_Rock);
        UNITY_DECLARE_TEX2D_NOSAMPLER(_RockN); UNITY_DECLARE_TEX2D_NOSAMPLER(_Macro);
        #define S2(t, uv) UNITY_SAMPLE_TEX2D_SAMPLER(t, _GNear, uv)
        #if defined(UNITY_SEPARATE_TEXTURE_SAMPLER)
            #define SGRAD(t, uv, dx, dy) t.SampleGrad(sampler_GNear, uv, dx, dy)
        #else
            #define SGRAD(t, uv, dx, dy) tex2Dgrad(t, uv, dx, dy)
        #endif
        float4 _Tiles, _Tiles2, _GrassTint, _MeadowTint, _DryTint, _RockSlope;
        float _NormalK;
        // luz da cidade (CityLight)
        sampler2D _CityLightTex;
        float4 _CityLightRect;
        float _CityLightK;

        struct Input
        {
            float4 tc;          // xy = uv do splatmap, zw = coordenada do mapa de normais do terreno
            float3 worldPos;
            float3 vN;          // normal do vértice (sem instancing)
            UNITY_FOG_COORDS(0)
        };

        void TVert(inout appdata_full v, out Input data)
        {
            UNITY_INITIALIZE_OUTPUT(Input, data);
        #if defined(UNITY_INSTANCING_ENABLED)
            float2 patchVertex = v.vertex.xy;
            float4 instanceData = UNITY_ACCESS_INSTANCED_PROP(Terrain, _TerrainPatchInstanceData);
            float4 uvscale = instanceData.z * _TerrainHeightmapRecipSize;
            float4 uvoffset = instanceData.xyxy * uvscale;
            uvoffset.xy += 0.5f * _TerrainHeightmapRecipSize.xy;
            float2 sampleCoords = (patchVertex.xy * uvscale.xy + uvoffset.xy);
            float hm = UnpackHeightmap(tex2Dlod(_TerrainHeightmapTexture, float4(sampleCoords, 0, 0)));
            v.vertex.xz = (patchVertex.xy + instanceData.xy) * _TerrainHeightmapScale.xz * instanceData.z;
            v.vertex.y = hm * _TerrainHeightmapScale.y;
            v.vertex.w = 1.0f;
            v.texcoord.xy = (patchVertex.xy * uvscale.zw + uvoffset.zw);
            v.texcoord3 = v.texcoord2 = v.texcoord1 = v.texcoord;
            v.normal = float3(0, 1, 0);       // normal por pixel (mapa de normais do terreno)
            data.tc.zw = sampleCoords;
        #endif
            // base tangente = (x, z, y) do mundo: o normal final sai como mundo.xzy
            v.tangent.xyz = cross(v.normal, float3(0, 0, 1));
            v.tangent.w = -1;
            data.tc.xy = v.texcoord.xy;
            data.vN = v.normal;
            float4 pos = UnityObjectToClipPos(v.vertex);
            UNITY_TRANSFER_FOG(data, pos);
        }

        void TFinal(Input IN, SurfaceOutputStandard o, inout fixed4 color)
        {
            UNITY_APPLY_FOG(IN.fogCoord, color);
        }

        // normal de um mapa projetado de cima (uv = xz) sobre a normal geométrica (whiteout)
        float3 TopN(float3 tn, float3 gN) { tn = float3(tn.xy + gN.xz, abs(tn.z) * gN.y); return tn.xzy; }
        float Lum(float3 c) { return dot(c, float3(0.3, 0.55, 0.15)); }

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            float3 wp = IN.worldPos;
            float dist = distance(wp, _WorldSpaceCameraPos);
            float2 splatUV = (IN.tc.xy * (_Control_TexelSize.zw - 1.0f) + 0.5f) * _Control_TexelSize.xy;
            float4 ctl = tex2D(_Control, splatUV);
            ctl /= max(dot(ctl, 1), 1e-3);

            float3 gN = normalize(IN.vN);
        #if defined(UNITY_INSTANCING_ENABLED) && !defined(SHADER_TARGET_SURFACE_ANALYSIS)
            gN = normalize(tex2D(_TerrainNormalmapTexture, IN.tc.zw).xyz * 2 - 1);
        #endif

            // ruído de variação: duas escalas (manchas de ~80 m e ~20 m)
            float m1 = S2(_Macro, wp.xz / 160.0).r;
            float m2 = S2(_Macro, wp.xz / 37.0 + 0.31).g;
            float m3 = S2(_Macro, wp.xz / 9.0 + 0.7).b;
            float macro = m1 * 0.6 + m2 * 0.4;

            float2 uvN = wp.xz / _Tiles.x, uvM = wp.xz / _Tiles.y, uvP = wp.xz / _Tiles.z, uvC = wp.xz / _Tiles.w, uvF = wp.xz / _Tiles2.x;
            float4 gNear = S2(_GNear, uvN);
            float4 gMead = S2(_GMeadow, uvM);
            float4 path = S2(_Path, uvP);
            float4 cob = S2(_Cobble, uvC);
            float4 fld = S2(_Field, uvF);

            // capim: perto o chão de folhas; longe (e em manchas) o prado de drone
            float farK = saturate(smoothstep(10, 46, dist) + (m2 - 0.5) * 0.9 + (m3 - 0.5) * 0.35);
            float3 grass = lerp(gNear.rgb * _GrassTint.rgb, gMead.rgb * _MeadowTint.rgb, farK);
            // manchas secas/mais verdes de dezenas de metros
            float dry = smoothstep(0.42, 0.75, macro);
            grass *= lerp(float3(0.92, 1.03, 0.9), _DryTint.rgb, dry) * (0.85 + 0.3 * m3);
            float hG = Lum(grass) * 1.2;
            float3 nG = lerp(UnpackNormal(S2(_GNearN, uvN)), UnpackNormal(S2(_GMeadowN, uvM)), farK);

            float3 pathC = path.rgb * (0.9 + 0.25 * m2);
            float hP = Lum(path.rgb) * 1.6 + 0.05;
            float3 cobC = cob.rgb;
            float hC = Lum(cob.rgb) * 1.8 + 0.1;
            float3 fldC = fld.rgb * (0.9 + 0.2 * m3);
            float hF = Lum(fld.rgb);

            // mistura pela altura: a camada mais "alta" naquele ponto ganha a transição
            float4 h = float4(hG, hP, hC, hF) + ctl * 1.2;
            float hmax = max(max(h.x, h.y), max(h.z, h.w));
            float4 w = max(h - (hmax - 0.22), 0) * ctl;
            w /= max(dot(w, 1), 1e-4);

            float3 alb = grass * w.x + pathC * w.y + cobC * w.z + fldC * w.w;
            float3 tn = nG * w.x + UnpackNormal(S2(_PathN, uvP)) * w.y + UnpackNormal(S2(_CobbleN, uvC)) * w.z + float3(0, 0, 1) * w.w;
            tn.xy *= _NormalK;
            float3 N = TopN(normalize(tn), gN);
            float smooth = 0.06 * w.x + 0.1 * w.y + 0.22 * w.z + 0.04 * w.w;
            float occ = lerp(1, saturate(hG * 1.6), w.x * 0.35);

            // encostas íngremes viram rocha (projeção triplanar, só onde precisa)
            float slope = 1 - gN.y;
            float rockW = smoothstep(_RockSlope.x, _RockSlope.y, slope + (m3 - 0.5) * 0.18 + (m2 - 0.5) * 0.1);
            // derivadas fora do if (dentro de um desvio elas não valem)
            float3 p = wp / _Tiles2.y;
            float3 px = ddx(p), py = ddy(p);
            UNITY_BRANCH
            if (rockW > 0.001)
            {
                float3 bw = pow(abs(gN), 4); bw /= dot(bw, 1);
                float2 ux = p.zy, uy = p.xz, uz = p.xy;
                float3 ax = SGRAD(_Rock, ux, px.zy, py.zy).rgb;
                float3 ay = SGRAD(_Rock, uy, px.xz, py.xz).rgb;
                float3 az = SGRAD(_Rock, uz, px.xy, py.xy).rgb;
                float3 rock = ax * bw.x + ay * bw.y + az * bw.z;
                float3 tx = UnpackNormal(SGRAD(_RockN, ux, px.zy, py.zy));
                float3 ty = UnpackNormal(SGRAD(_RockN, uy, px.xz, py.xz));
                float3 tz = UnpackNormal(SGRAD(_RockN, uz, px.xy, py.xy));
                tx = float3(tx.xy + gN.zy, abs(tx.z) * gN.x);
                ty = float3(ty.xy + gN.xz, abs(ty.z) * gN.y);
                tz = float3(tz.xy + gN.xy, abs(tz.z) * gN.z);
                float3 rN = normalize(tx.zyx * bw.x + ty.xzy * bw.y + tz.xyz * bw.z);
                // a rocha também aparece "por cima" da grama onde a pedra é mais alta (borda irregular)
                float rk = saturate(rockW * 1.25 + (Lum(rock) - hG) * rockW * 1.5);
                rock *= 0.85 + 0.3 * m2;
                alb = lerp(alb, rock, rk);
                N = normalize(lerp(N, rN, rk));
                smooth = lerp(smooth, 0.16, rk);
                occ = lerp(occ, saturate(Lum(rock) * 2.2), rk * 0.5);
            }

            o.Albedo = alb;
            o.Normal = N.xzy;
            o.Smoothness = smooth;
            o.Metallic = 0;
            o.Occlusion = occ;
            o.Alpha = 1;

            // luz da cidade: as poças quentes das janelas, lanternas e tochas no chão
            float3 em = 0;
            if (_CityLightK > 0.001)
            {
                float2 cu = (wp.xz - _CityLightRect.xy) * _CityLightRect.zw;
                if (cu.x > 0 && cu.y > 0 && cu.x < 1 && cu.y < 1)
                {
                    float4 L = tex2Dlod(_CityLightTex, float4(cu, 0, 0));
                    float att = exp(-max(0, L.a - wp.y - 1.0) / 6.0);
                    em = alb * L.rgb * att * _CityLightK * 1.25 * occ;
                }
            }
            o.Emission = em;
        }
        ENDCG
    }
    Fallback "Nature/Terrain/Diffuse"
}
