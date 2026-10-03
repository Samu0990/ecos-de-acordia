// Folhagem das árvores e arbustos de Campanula (kit_trees.py): normais esféricas na copa,
// cor de vértice R = oclusão (miolo/base mais escuros), A = peso do vento. Vento suave
// no vértice (em espaço de mundo: funciona com static batching) e brilho de contraluz
// quando a câmera olha contra o sol baixo do pôr do sol. Lambert: barato no Intel UHD.
Shader "Campanula/Foliage"
{
    Properties
    {
        _MainTex ("Folhas", 2D) = "white" {}
        _Color ("Tom", Color) = (1,1,1,1)
        _AO ("Oclusão (cor de vértice)", Range(0,1)) = 0.85
        _Wind ("Vento", Range(0,2)) = 1
        _WindSpeed ("Velocidade do vento", Float) = 1.3
        [HDR] _Translucency ("Contraluz", Color) = (1.2,0.75,0.35,1)
        _Rim ("Borda iluminada", Range(0,1)) = 0.35
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        LOD 200
        CGPROGRAM
        #pragma surface surf Lambert vertex:vert addshadow fullforwardshadows
        #pragma multi_compile_instancing
        #pragma target 3.0
        sampler2D _MainTex;
        fixed4 _Color, _Translucency;
        half _AO, _Wind, _WindSpeed, _Rim;

        struct Input { float2 uv_MainTex; float4 color : COLOR; float3 worldPos; float3 viewDir; };

        void vert (inout appdata_full v)
        {
            float3 wp = mul(unity_ObjectToWorld, v.vertex).xyz;
            float w = v.color.a * _Wind;
            float t = _Time.y * _WindSpeed;
            float3 off = float3(sin(t + wp.x * 0.31 + wp.z * 0.17), 0, cos(t * 0.83 + wp.z * 0.27 + wp.x * 0.11)) * 0.07;
            off += float3(sin(t * 3.1 + wp.y * 2.1 + wp.x), sin(t * 2.7 + wp.z * 1.9) * 0.5, cos(t * 2.9 + wp.y * 1.7)) * 0.025;
            v.vertex.xyz += mul((float3x3)unity_WorldToObject, off * w);
        }

        void surf (Input IN, inout SurfaceOutput o)
        {
            fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            float ao = lerp(1, IN.color.r, _AO);
            o.Albedo = c.rgb * ao;
            float3 L = normalize(_WorldSpaceLightPos0.xyz);
            float3 V = normalize(IN.viewDir);
            // contraluz: sol atrás da copa (o pôr do sol é rasante)
            float back = pow(saturate(dot(-V, L)), 3);
            float rim = pow(1 - saturate(dot(V, o.Normal)), 3);
            o.Emission = c.rgb * _Translucency.rgb * (back * (0.35 + rim) + rim * _Rim * 0.4) * ao * _LightColor0.rgb;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
