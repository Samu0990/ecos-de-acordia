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
        sampler2D _MainTex, _BumpMap, _MetallicGlossMap, _OcclusionMap;
        float4 _Color, _EmissionColor;
        float _BumpScale, _GlossMapScale, _Glossiness, _Metallic, _OcclusionStrength;
        // luz da cidade (CityLight): rgb = luz somada, a = altura média das fontes; retângulo xz e intensidade
        sampler2D _CityLightTex;
        float4 _CityLightRect;   // x0, z0, 1/largura, 1/profundidade
        float _CityLightK;
        struct Input { float2 uv_MainTex; float3 worldPos; float3 worldNormal; INTERNAL_DATA };
        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            o.Albedo = c.rgb;
            #ifdef _NORMALMAP
            o.Normal = UnpackScaleNormal(tex2D(_BumpMap, IN.uv_MainTex), _BumpScale);
            #endif
            #ifdef _METALLICGLOSSMAP
            float4 mg = tex2D(_MetallicGlossMap, IN.uv_MainTex);
            o.Metallic = mg.r; o.Smoothness = mg.a * _GlossMapScale;
            #else
            o.Metallic = _Metallic; o.Smoothness = _Glossiness;
            #endif
            o.Occlusion = lerp(1, tex2D(_OcclusionMap, IN.uv_MainTex).g, _OcclusionStrength);
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
