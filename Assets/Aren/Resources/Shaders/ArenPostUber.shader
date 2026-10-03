// Pós-processamento leve do jogo (pipeline Built-in, espaço de cor Gamma, sem HDR).
// Uma passada só, embutida no blit do RenderScaler: correção de cor do pôr do sol
// (contraste em S, saturação, sombras frias / luzes quentes), vinheta e o bloom pronto
// (passadas 1–3, em 1/4 e 1/8 da resolução). Feito para o Intel UHD 620: sem efeitos de
// tela cheia extras além do blit que já existia.
Shader "Hidden/Aren/PostUber"
{
    Properties
    {
        _MainTex ("Cena", 2D) = "white" {}
        _BloomTex ("Bloom", 2D) = "black" {}
    }
    CGINCLUDE
    #include "UnityCG.cginc"
    sampler2D _MainTex, _BloomTex;
    float4 _MainTex_TexelSize;
    float _Bloom, _Threshold, _Contrast, _Saturation, _Vignette, _Exposure;
    float3 _ShadowTint, _HighTint;

    struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
    v2f vert (appdata_img v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.uv = v.texcoord; return o; }

    float Luma(float3 c) { return dot(c, float3(0.299, 0.587, 0.114)); }

    // 0 — composição final
    float4 fragUber (v2f i) : SV_Target
    {
        float3 c = tex2D(_MainTex, i.uv).rgb;
        #ifdef ARENPOST_BLOOM
        c += tex2D(_BloomTex, i.uv).rgb * _Bloom;
        #endif
        c *= _Exposure;
        float l = Luma(c);
        // sombras levemente frias (violeta da Fenda), luzes quentes (sol baixo)
        float sh = 1 - smoothstep(0.0, 0.45, l);
        float hi = smoothstep(0.55, 1.0, l);
        c = c * lerp(float3(1, 1, 1), _ShadowTint, sh) * lerp(float3(1, 1, 1), _HighTint, hi);
        // contraste em S em torno do meio-tom
        c = saturate(c);
        c = lerp(c, c * c * (3 - 2 * c), _Contrast);
        // saturação
        l = Luma(c);
        c = lerp(l.xxx, c, _Saturation);
        // vinheta
        float2 d = (i.uv - 0.5) * float2(1.0, 0.8);
        c *= 1 - _Vignette * saturate(dot(d, d) * 2.2);
        return float4(saturate(c), 1);
    }

    // 1 — pré-filtro do bloom: só o que é claro (janelas, tochas, céu, efeitos), 4 amostras
    float4 fragPrefilter (v2f i) : SV_Target
    {
        float2 o = _MainTex_TexelSize.xy * 0.5;
        float3 c = tex2D(_MainTex, i.uv + float2(-o.x, -o.y)).rgb + tex2D(_MainTex, i.uv + float2(o.x, -o.y)).rgb
                 + tex2D(_MainTex, i.uv + float2(-o.x, o.y)).rgb + tex2D(_MainTex, i.uv + float2(o.x, o.y)).rgb;
        c *= 0.25;
        float l = max(c.r, max(c.g, c.b));
        float k = saturate((l - _Threshold) / max(1e-4, 1 - _Threshold));
        return float4(c * k * k, 1);
    }

    // 2 — desfoque de 9 amostras (caixa em X), barato em 1/8 da tela
    float4 fragBlur (v2f i) : SV_Target
    {
        float2 o = _MainTex_TexelSize.xy;
        float3 c = tex2D(_MainTex, i.uv).rgb * 0.2;
        c += (tex2D(_MainTex, i.uv + float2(o.x * 1.5, o.y * 0.5)).rgb + tex2D(_MainTex, i.uv - float2(o.x * 1.5, o.y * 0.5)).rgb) * 0.15;
        c += (tex2D(_MainTex, i.uv + float2(-o.x * 0.5, o.y * 1.5)).rgb + tex2D(_MainTex, i.uv - float2(-o.x * 0.5, o.y * 1.5)).rgb) * 0.15;
        c += (tex2D(_MainTex, i.uv + o * 3).rgb + tex2D(_MainTex, i.uv - o * 3).rgb) * 0.1;
        return float4(c, 1);
    }
    ENDCG

    SubShader
    {
        ZTest Always Cull Off ZWrite Off
        Pass { CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragUber
            #pragma multi_compile_local __ ARENPOST_BLOOM
            ENDCG }
        Pass { CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragPrefilter
            ENDCG }
        Pass { CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragBlur
            ENDCG }
    }
}
