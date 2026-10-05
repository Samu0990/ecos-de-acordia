// Folhagem das árvores v2 (kit_trees.py): cartões com o atlas de folhas renderizado no Blender
// (leaf_atlas.py), recortados pelo alfa e vistos dos dois lados. As normais vêm do modelo (direção a
// partir do centro da copa): a copa é iluminada como um volume. Por cima:
//   · normal map do atlas (dobra de cada folha / leque das agulhas), suave;
//   · vento em espaço de mundo (funciona com static batching): balanço da copa + tremor das folhas,
//     pesado pela cor do vértice A (0 no tronco → 1 nas pontas);
//   · oclusão pela cor do vértice R (miolo e base da copa mais escuros);
//   · luz atravessando as folhas (contraluz) e a luz da cidade (CityLight) — a árvore perto das janelas
//     acesas fica dourada embaixo.
Shader "Campanula/LeafCards"
{
    Properties
    {
        _MainTex ("Atlas de folhas", 2D) = "white" {}
        [Normal] _BumpMap ("Normal do atlas", 2D) = "bump" {}
        _BumpScale ("Força do normal", Range(0, 1.5)) = 0.6
        _Color ("Tom", Color) = (1,1,1,1)
        _Cutoff ("Recorte", Range(0, 1)) = 0.45
        _AO ("Oclusão (cor de vértice)", Range(0, 1)) = 0.9
        _Wind ("Vento", Range(0, 2)) = 1
        _WindSpeed ("Velocidade do vento", Float) = 1.2
        _Translucency ("Contraluz", Color) = (0.55, 0.7, 0.25, 1)
    }
    SubShader
    {
        Tags { "Queue" = "AlphaTest" "RenderType" = "TransparentCutout" "IgnoreProjector" = "True" }
        LOD 200
        Cull Off
        CGPROGRAM
        #pragma surface surf Lambert vertex:vert alphatest:_Cutoff addshadow fullforwardshadows
        #pragma multi_compile_instancing
        #pragma target 3.0
        sampler2D _MainTex, _BumpMap;
        fixed4 _Color, _Translucency;
        half _AO, _Wind, _WindSpeed, _BumpScale;
        sampler2D _CityLightTex;
        float4 _CityLightRect;
        float _CityLightK;

        struct Input { float2 uv_MainTex; float4 color : COLOR; float3 worldPos; float3 viewDir; };

        void vert (inout appdata_full v)
        {
            float3 wp = mul(unity_ObjectToWorld, v.vertex).xyz;
            float w = v.color.a * _Wind;
            float t = _Time.y * _WindSpeed;
            // a copa balança inteira (fase pela posição da árvore) e cada folha treme
            float3 root = float3(unity_ObjectToWorld._m03, 0, unity_ObjectToWorld._m23);
            float ph = dot(wp.xz, float2(0.11, 0.07));
            float3 off = float3(sin(t + ph), 0, cos(t * 0.83 + ph * 1.3)) * 0.09;
            off += float3(sin(t * 3.3 + wp.y * 2.1 + wp.x * 1.7), sin(t * 2.9 + wp.z * 1.9) * 0.6, cos(t * 3.1 + wp.y * 1.7 + wp.z)) * 0.03;
            v.vertex.xyz += mul((float3x3)unity_WorldToObject, off * w);
        }

        void surf (Input IN, inout SurfaceOutput o)
        {
            fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            float ao = lerp(1, IN.color.r, _AO);
            o.Albedo = c.rgb * ao;
            o.Alpha = c.a;
            o.Normal = UnpackScaleNormal(tex2D(_BumpMap, IN.uv_MainTex), _BumpScale);
            // contraluz: a lua/sol atrás da copa acende as folhas da borda
            float3 L = normalize(_WorldSpaceLightPos0.xyz);
            float3 V = normalize(IN.viewDir);
            float back = pow(saturate(dot(-normalize(_WorldSpaceCameraPos - IN.worldPos), L)), 4.0);
            float3 em = c.rgb * _Translucency.rgb * _LightColor0.rgb * back * (0.3 + 0.7 * IN.color.a) * ao * 1.4;
            if (_CityLightK > 0.001)
            {
                float2 cu = (IN.worldPos.xz - _CityLightRect.xy) * _CityLightRect.zw;
                if (cu.x > 0 && cu.y > 0 && cu.x < 1 && cu.y < 1)
                {
                    float4 Lc = tex2Dlod(_CityLightTex, float4(cu, 0, 0));
                    float att = exp(-abs(IN.worldPos.y - Lc.a) / 4.0);
                    em += c.rgb * Lc.rgb * att * _CityLightK * ao;
                }
            }
            o.Emission = em;
        }
        ENDCG
    }
    FallBack "Legacy Shaders/Transparent/Cutout/VertexLit"
}
