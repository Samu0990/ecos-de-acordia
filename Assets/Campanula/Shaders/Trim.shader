// Props do Fantasy Props MegaKit (Quaternius, CC0): trim sheets com BaseColor, Normal e
// ORM (R = oclusão, G = rugosidade, B = metal). As variantes "_Vertex" e os estandartes
// tingem a textura pela cor de vértice; os estandartes ainda desenham o emblema pelo UV2
// (máscara branca dentro da própria textura de tecido), num tom mais escuro do pano.
Shader "Campanula/Trim"
{
    Properties
    {
        _MainTex ("Base Color", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        [Normal] _BumpMap ("Normal", 2D) = "bump" {}
        _BumpScale ("Normal strength", Range(0, 2)) = 1
        _ORM ("ORM (AO, Rough, Metal)", 2D) = "white" {}
        _AOStrength ("AO strength", Range(0, 1)) = 0.8
        _SmoothMul ("Smoothness mul", Range(0, 1.5)) = 1
        [Toggle(_TRIM_VC)] _UseVC ("Vertex color tint", Float) = 0
        [Toggle(_TRIM_EMBLEM)] _UseEmblem ("Emblem from UV2", Float) = 0
        _EmblemDarken ("Emblem darken", Range(0, 1)) = 0.4
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull (pano de face única = Off)", Float) = 2
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        LOD 300
        Cull [_Cull]

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0
        #pragma multi_compile_instancing
        #pragma shader_feature_local _TRIM_VC
        #pragma shader_feature_local _TRIM_EMBLEM

        sampler2D _MainTex, _BumpMap, _ORM;
        fixed4 _Color;
        half _BumpScale, _AOStrength, _SmoothMul, _EmblemDarken;

        struct Input
        {
            float2 uv_MainTex;
        #ifdef _TRIM_EMBLEM
            float2 uv2_BumpMap;
        #endif
            float4 color : COLOR;
            float facing : VFACE;
        };

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
        #ifdef _TRIM_VC
            c.rgb *= IN.color.rgb;
        #endif
        #ifdef _TRIM_EMBLEM
            half e = tex2D(_MainTex, IN.uv2_BumpMap).r;
            c.rgb = lerp(c.rgb, c.rgb * _EmblemDarken, saturate((e - 0.25) * 2.0));
        #endif
            half3 orm = tex2D(_ORM, IN.uv_MainTex).rgb;
            o.Albedo = c.rgb;
            o.Normal = UnpackScaleNormal(tex2D(_BumpMap, IN.uv_MainTex), _BumpScale);
            o.Normal.z *= IN.facing > 0 ? 1 : -1;   // verso do pano (Cull Off) iluminado pelo lado certo
            o.Metallic = orm.b;
            o.Smoothness = saturate((1.0 - orm.g) * _SmoothMul);
            o.Occlusion = lerp(1.0, orm.r, _AOStrength);
            o.Alpha = 1;
        }
        ENDCG
    }
    // placas muito fracas: sem normal map nem ORM
    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        LOD 150
        Cull [_Cull]
        CGPROGRAM
        #pragma surface surf Lambert
        #pragma multi_compile_instancing
        #pragma shader_feature_local _TRIM_VC
        sampler2D _MainTex; fixed4 _Color;
        struct Input { float2 uv_MainTex; float4 color : COLOR; };
        void surf (Input IN, inout SurfaceOutput o)
        {
            fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
        #ifdef _TRIM_VC
            c.rgb *= IN.color.rgb;
        #endif
            o.Albedo = c.rgb;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
