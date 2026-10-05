// Bronze do sino da estrada: metal (fluxo Standard) com variação de cor, pátina esverdeada nas
// partes de cima e manchas de oxidação pelo ruído — pega o brilho da lanterna e o recorte da lua
// em vez de parecer plástico laranja.
Shader "Hidden/Aren/Bronze"
{
    Properties
    {
        _Color ("Bronze", Color) = (0.58,0.4,0.21,1)
        _Patina ("Pátina", Color) = (0.22,0.32,0.26,1)
        _Noise ("Ruído", 2D) = "gray" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0
        sampler2D _Noise;
        float4 _Color, _Patina;
        struct Input { float3 worldPos; float3 worldNormal; };
        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            float3 p = IN.worldPos;
            float n = tex2D(_Noise, p.xz * 1.7 + p.y * 0.9).r * 0.6 + tex2D(_Noise, p.xy * 3.1).g * 0.4;
            float top = saturate(IN.worldNormal.y * 1.5);
            float patina = saturate((n - 0.48) * 3.0 + top * 0.35);
            o.Albedo = lerp(_Color.rgb * (0.85 + 0.3 * n), _Patina.rgb, patina * 0.65);
            o.Metallic = lerp(0.85, 0.2, patina);
            o.Smoothness = lerp(0.62, 0.3, patina) * (0.85 + 0.15 * n);
        }
        ENDCG
    }
    FallBack "Diffuse"
}
