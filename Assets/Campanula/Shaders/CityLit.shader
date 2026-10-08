// Materiais da vila (CMP_*): o Standard de sempre (albedo, normal, suavidade no alfa do _MetallicGlossMap,
// oclusão, emissão do vidro) + a LUZ DA CIDADE à noite: um mapa visto de cima (CityLight) com a luz
// quente de cada janela acesa, lanterna e tocha somada em manchas suaves, e a altura média delas. A
// pedra perto de uma fonte fica dourada (como na referência do storyboard: a cidade inteira brilha),
// sem luzes em tempo real (cada luz pontual custaria uma passada extra no Intel UHD). De dia
// _CityLightK = 0 e o material é igual ao Standard.
Shader "Campanula/CityLit"
{
    Properties
    {
        _Color ("Cor", Color) = (1,1,1,1)
        _MainTex ("Albedo", 2D) = "white" {}
        [Normal] _BumpMap ("Normal", 2D) = "bump" {}
        _BumpScale ("Força do normal", Float) = 1
        _MetallicGlossMap ("Metálico/Suavidade (A)", 2D) = "white" {}
        _GlossMapScale ("Escala da suavidade", Range(0,1)) = 1
        _Glossiness ("Suavidade", Range(0,1)) = 0.2
        _Metallic ("Metálico", Range(0,1)) = 0
        _OcclusionMap ("Oclusão", 2D) = "white" {}
        _OcclusionStrength ("Força da oclusão", Range(0,1)) = 1
        [HDR] _EmissionColor ("Emissão", Color) = (0,0,0,1)
        _ParallaxMap ("Altura (paralaxe)", 2D) = "black" {}
        _Parallax ("Força do relevo", Range(0,0.08)) = 0.035
        _AOAtlas ("Oclusão assada (perto)", 2D) = "white" {}
        _AOAtlasFar ("Oclusão assada (longe)", 2D) = "white" {}
        _AOAtlasTown ("Oclusão assada — casas da cidade (perto)", 2D) = "white" {}
        _AOAtlasTownFar ("Oclusão assada — casas da cidade (longe)", 2D) = "white" {}
        _GrimeNoise ("Ruído da sujeira", 2D) = "gray" {}
        _Grime ("Sujeira e umidade nas paredes", Range(0,1.5)) = 1
        _AOStrength ("Força da oclusão assada", Range(0,1)) = 1
        _SmoothnessTextureChannel ("(compat. Standard)", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 300
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0
        #pragma multi_compile_instancing
        #pragma shader_feature_local _NORMALMAP
        #pragma shader_feature_local _METALLICGLOSSMAP
        #pragma shader_feature_local _EMISSION
        #pragma shader_feature_local _PARALLAXMAP
        sampler2D _MainTex, _BumpMap, _MetallicGlossMap, _OcclusionMap, _ParallaxMap, _AOAtlas, _AOAtlasFar, _AOAtlasTown, _AOAtlasTownFar, _GrimeNoise;
        float _Parallax, _AOStrength, _Grime;
        float4 _Color, _EmissionColor;
        float _BumpScale, _GlossMapScale, _Glossiness, _Metallic, _OcclusionStrength;
        // luz da cidade (CityLight): rgb = luz somada, a = altura média das fontes; retângulo xz e intensidade
        sampler2D _CityLightTex;
        float4 _CityLightRect;   // x0, z0, 1/largura, 1/profundidade
        float _CityLightK;
        struct Input { float2 uv_MainTex; float2 uv2_AOAtlas; float3 worldPos; float3 worldNormal; float3 viewDir; INTERNAL_DATA };
        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            float2 uv = IN.uv_MainTex;
            #ifdef _PARALLAXMAP
            // relevo da pedra (juntas fundas, blocos saltados): desloca o UV pela altura, na direção do olhar
            float hgt = tex2D(_ParallaxMap, uv).r;
            uv += ParallaxOffset(hgt, _Parallax, IN.viewDir);
            #endif
            fixed4 c = tex2D(_MainTex, uv) * _Color;
            #ifdef _NORMALMAP
            o.Normal = UnpackScaleNormal(tex2D(_BumpMap, uv), _BumpScale);
            #endif
            #ifdef _METALLICGLOSSMAP
            float4 mg = tex2D(_MetallicGlossMap, uv);
            o.Metallic = mg.r; o.Smoothness = mg.a * _GlossMapScale;
            #else
            o.Metallic = _Metallic; o.Smoothness = _Glossiness;
            #endif
            o.Occlusion = lerp(1, tex2D(_OcclusionMap, uv).g, _OcclusionStrength);
            // sujeira e umidade (Campânula v3, "nível Dark Souls"): a base das paredes escurece em manchas (respingo
            // da rua, umidade que sobe) e escorridos verticais descem das janelas e cornijas; só nas paredes
            // (telhados e chão ficam), por altura no mundo (a cidade é plana em y = 0)
            if (_Grime > 0.001)
            {
                float3 wp = IN.worldPos;
                float3 wn0 = WorldNormalVector(IN, float3(0, 0, 1));
                float wall = saturate(1 - abs(wn0.y) * 1.6);
                float along = dot(wp.xz, normalize(float2(wn0.z, -wn0.x) + 1e-4));
                float n1 = tex2D(_GrimeNoise, wp.xz * 0.045 + wp.y * 0.03).r;
                float n2 = tex2D(_GrimeNoise, float2(along * 0.11, wp.y * 0.012 + n1 * 0.05)).r;
                float base = saturate(1 - wp.y / 2.4) * (0.45 + 0.55 * n1);
                float streak = smoothstep(0.52, 0.78, n2) * saturate(wp.y / 1.5);
                float g = (base * 0.5 + streak * 0.32) * wall * _Grime;
                c.rgb *= 1 - g;
                c.rgb = lerp(c.rgb, c.rgb * float3(0.86, 0.9, 0.8), base * wall * 0.5 * _Grime);   // limo esverdeado no pé
                o.Smoothness = lerp(o.Smoothness, saturate(o.Smoothness + 0.25), streak * wall * _Grime);   // escorrido úmido brilha
            }
            // oclusão de ambiente assada no Blender (só nos modelos góticos: UV2 com u ≥ 2)
            float2 a2 = IN.uv2_AOAtlas;
            float bao = 1;
            // casas variadas da cidade (kit_town.py): u + 6 perto, u + 8 longe
            if (a2.x >= 7.999) bao = tex2D(_AOAtlasTownFar, a2 - float2(8, 0)).r;
            else if (a2.x >= 5.999) bao = tex2D(_AOAtlasTown, a2 - float2(6, 0)).r;
            else if (a2.x >= 3.999) bao = tex2D(_AOAtlasFar, a2 - float2(4, 0)).r;
            else if (a2.x >= 1.999) bao = tex2D(_AOAtlas, a2 - float2(2, 0)).r;
            bao = lerp(1, bao, _AOStrength);
            o.Occlusion *= bao;
            c.rgb *= lerp(1, bao, 0.6);   // também sob a luz direta (lua): os cantos ganham profundidade
            o.Albedo = c.rgb;
            float3 em = 0;
            #ifdef _EMISSION
            em = _EmissionColor.rgb;
            #endif
            if (_CityLightK > 0.001)
            {
                float2 cu = (IN.worldPos.xz - _CityLightRect.xy) * _CityLightRect.zw;
                if (cu.x > 0 && cu.y > 0 && cu.x < 1 && cu.y < 1)
                {
                    float4 L = tex2Dlod(_CityLightTex, float4(cu, 0, 0));
                    // a luz cai com a distância vertical até a altura média das fontes ali
                    float att = exp(-abs(IN.worldPos.y - L.a) / 3.2);
                    // superfícies viradas para cima (beirais, peitoris, chão) pegam um pouco mais;
                    // telhados altos quase nada (a luz vem de baixo e de dentro)
                    float3 wn = WorldNormalVector(IN, o.Normal);
                    float up = saturate(wn.y);
                    em += c.rgb * L.rgb * att * (0.85 + 0.3 * up) * _CityLightK * o.Occlusion;
                }
            }
            o.Emission = em;
        }
        ENDCG
    }
    FallBack "Standard"
}
