// Pós-processamento do jogo (pipeline Built-in, espaço de cor Gamma). Embutido no blit do
// RenderScaler, que já existia: correção de cor, vinheta e bloom barato (passadas 1–2) no jogo.
// Na abertura (RenderScaler.Cinematic) a cena é desenhada em HDR e entram: curva de filme nos
// claros (ombro suave, sem estourar o branco), bloom em várias escalas (passadas 3–5), um leve
// rastro anamórfico nas luzes fortes (6), raios de luz a partir de um ponto da tela (7: a Fenda,
// o clarão do impacto), grão de filme e aberração cromática bem sutil nas bordas.
Shader "Hidden/Aren/PostUber"
{
    Properties
    {
        _MainTex ("Cena", 2D) = "white" {}
        _BloomTex ("Bloom", 2D) = "black" {}
        _StreakTex ("Rastro", 2D) = "black" {}
        _ShaftTex ("Raios", 2D) = "black" {}
        _LowTex ("Nível de baixo", 2D) = "black" {}
    }
    CGINCLUDE
    #include "UnityCG.cginc"
    sampler2D _MainTex, _BloomTex, _StreakTex, _ShaftTex, _LowTex;
    float4 _MainTex_TexelSize;
    float _Bloom, _Threshold, _Contrast, _Saturation, _Vignette, _Exposure;
    float3 _ShadowTint, _HighTint;
    float _Streak, _Grain, _Aberration, _ShaftIntensity, _Knee, _Lift;
    float4 _ShaftPos, _ShaftColor, _StreakColor;
    float4 _Ripple;   // onda de choque passando pela câmera: xy centro (uv), z raio (alturas de tela), w força

    struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
    v2f vert (appdata_img v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.uv = v.texcoord; return o; }

    float Luma(float3 c) { return dot(c, float3(0.299, 0.587, 0.114)); }

    // ombro de filme: linear até 'knee', depois comprime suavemente até 1 (e dessatura os claros extremos)
    float3 FilmShoulder(float3 c)
    {
        float k = _Knee;
        float3 over = max(c - k, 0);
        float3 comp = k + (1 - k) * (1 - exp(-over / (1 - k)));
        float3 r = c < k ? c : comp;
        float l = Luma(c);
        r = lerp(r, Luma(r).xxx, saturate((l - 1.2) * 0.25) * 0.5);
        return r;
    }

    float3 Grade(float3 c, float2 uv)
    {
        c *= _Exposure;
        #ifdef ARENPOST_HDR
        c = FilmShoulder(c);
        #endif
        float l = Luma(c);
        float sh = 1 - smoothstep(0.0, 0.45, l);
        float hi = smoothstep(0.55, 1.0, l);
        c = c * lerp(float3(1, 1, 1), _ShadowTint, sh) * lerp(float3(1, 1, 1), _HighTint, hi);
        c = saturate(c);
        c = lerp(c, c * c * (3 - 2 * c), _Contrast);
        l = Luma(c);
        c = lerp(l.xxx, c, _Saturation);
        c = c * (1 - _Lift) + _Lift * float3(0.55, 0.6, 0.85) * 0.12;   // pretos levemente erguidos e frios (filme)
        float2 d = (uv - 0.5) * float2(1.0, 0.8);
        c *= 1 - _Vignette * saturate(dot(d, d) * 2.2);
        return c;
    }

    // 0 — composição final
    float4 fragUber (v2f i) : SV_Target
    {
        float3 c;
        // anel de refração (frente de pressão da abertura; golpes pesados no jogo): desloca a imagem só na casca do anel
        float2 uv = i.uv;
        if (_Ripple.w > 0.0001)
        {
            float asp = _ScreenParams.x / _ScreenParams.y;
            float2 rd = (uv - _Ripple.xy) * float2(asp, 1);
            float rl = length(rd);
            float x = (rl - _Ripple.z) / 0.085;
            float shell = exp(-x * x) * x;                      // empurra para fora na frente, puxa atrás
            uv += (rd / max(rl, 1e-4)) * shell * _Ripple.w * float2(1 / asp, 1);
        }
        #ifdef ARENPOST_CINE
        // aberração cromática radial (só nas bordas, muito sutil; o golpe dá um pico)
        float2 dc = uv - 0.5;
        float2 off = dc * dot(dc, dc) * _Aberration;
        c.r = tex2D(_MainTex, uv - off).r;
        c.g = tex2D(_MainTex, uv).g;
        c.b = tex2D(_MainTex, uv + off).b;
        #else
        c = tex2D(_MainTex, uv).rgb;
        #endif
        #ifdef ARENPOST_BLOOM
        c += tex2D(_BloomTex, i.uv).rgb * _Bloom;
        #endif
        #ifdef ARENPOST_CINE
        c += tex2D(_StreakTex, i.uv).rgb * _StreakColor.rgb * _Streak;
        c += tex2D(_ShaftTex, i.uv).rgb * _ShaftColor.rgb * _ShaftIntensity;
        #endif
        c = Grade(c, i.uv);
        #ifdef ARENPOST_CINE
        // grão de filme (mais visível nos médios)
        float n = frac(sin(dot(i.uv * _ScreenParams.xy + frac(_Time.y * 7.13) * 91.7, float2(12.9898, 78.233))) * 43758.5453) - 0.5;
        float l = Luma(c);
        c += n * _Grain * (0.35 + l * (1 - l) * 2.0);
        #endif
        return float4(saturate(c), 1);
    }

    // 1 — pré-filtro do bloom barato do jogo: só o que é claro, 4 amostras
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

    // 2 — desfoque de 9 amostras do bloom barato
    float4 fragBlur (v2f i) : SV_Target
    {
        float2 o = _MainTex_TexelSize.xy;
        float3 c = tex2D(_MainTex, i.uv).rgb * 0.2;
        c += (tex2D(_MainTex, i.uv + float2(o.x * 1.5, o.y * 0.5)).rgb + tex2D(_MainTex, i.uv - float2(o.x * 1.5, o.y * 0.5)).rgb) * 0.15;
        c += (tex2D(_MainTex, i.uv + float2(-o.x * 0.5, o.y * 1.5)).rgb + tex2D(_MainTex, i.uv - float2(-o.x * 0.5, o.y * 1.5)).rgb) * 0.15;
        c += (tex2D(_MainTex, i.uv + o * 3).rgb + tex2D(_MainTex, i.uv - o * 3).rgb) * 0.1;
        return float4(c, 1);
    }

    // amostragem de 13 toques (reduz e já desfoca, sem cintilar)
    float3 Down13(float2 uv)
    {
        float2 o = _MainTex_TexelSize.xy;
        float3 A = tex2D(_MainTex, uv + o * float2(-1, -1)).rgb, B = tex2D(_MainTex, uv + o * float2(1, -1)).rgb;
        float3 C = tex2D(_MainTex, uv + o * float2(-1, 1)).rgb, D = tex2D(_MainTex, uv + o * float2(1, 1)).rgb;
        float3 E = tex2D(_MainTex, uv + o * float2(-2, -2)).rgb, F = tex2D(_MainTex, uv + o * float2(0, -2)).rgb, G = tex2D(_MainTex, uv + o * float2(2, -2)).rgb;
        float3 H = tex2D(_MainTex, uv + o * float2(-2, 0)).rgb, I = tex2D(_MainTex, uv).rgb, J = tex2D(_MainTex, uv + o * float2(2, 0)).rgb;
        float3 K = tex2D(_MainTex, uv + o * float2(-2, 2)).rgb, L = tex2D(_MainTex, uv + o * float2(0, 2)).rgb, M = tex2D(_MainTex, uv + o * float2(2, 2)).rgb;
        return (A + B + C + D) * 0.125 + (E + G + K + M) * 0.03125 + (F + H + J + L) * 0.0625 + I * 0.125;
    }

    // 3 — pré-filtro HDR (joelho suave) com redução de 13 toques
    float4 fragPrefilterHDR (v2f i) : SV_Target
    {
        float3 c = Down13(i.uv);
        c = min(c, 12.0);
        float br = max(c.r, max(c.g, c.b));
        float knee = _Threshold * 0.6;
        float soft = clamp(br - _Threshold + knee, 0, 2 * knee);
        soft = soft * soft / (4 * knee + 1e-4);
        float contrib = max(soft, br - _Threshold) / max(br, 1e-4);
        return float4(c * contrib, 1);
    }

    // 4 — redução de 13 toques
    float4 fragDown (v2f i) : SV_Target { return float4(Down13(i.uv), 1); }

    // 5 — subida com filtro tenda, somando o nível de baixo
    float4 fragUp (v2f i) : SV_Target
    {
        float2 o = _MainTex_TexelSize.xy;   // texel do nível menor (_MainTex = nível de baixo)
        float3 s = tex2D(_MainTex, i.uv + o * float2(-1, -1)).rgb + tex2D(_MainTex, i.uv + o * float2(1, -1)).rgb
                 + tex2D(_MainTex, i.uv + o * float2(-1, 1)).rgb + tex2D(_MainTex, i.uv + o * float2(1, 1)).rgb;
        s += (tex2D(_MainTex, i.uv + o * float2(0, -1)).rgb + tex2D(_MainTex, i.uv + o * float2(0, 1)).rgb
            + tex2D(_MainTex, i.uv + o * float2(-1, 0)).rgb + tex2D(_MainTex, i.uv + o * float2(1, 0)).rgb) * 2;
        s += tex2D(_MainTex, i.uv).rgb * 4;
        return float4(s / 16 + tex2D(_LowTex, i.uv).rgb, 1);
    }

    // 6 — rastro anamórfico: desfoque horizontal largo dos claros
    float4 fragStreak (v2f i) : SV_Target
    {
        float2 o = float2(_MainTex_TexelSize.x, 0);
        float3 c = 0; float wsum = 0;
        [unroll] for (int k = -8; k <= 8; k++)
        {
            float w = exp(-abs(k) * 0.28);
            c += tex2D(_MainTex, i.uv + o * k * 5.0).rgb * w; wsum += w;
        }
        return float4(c / wsum, 1);
    }

    // 7 — raios de luz: desfoque radial dos claros a partir de _ShaftPos (uv)
    float4 fragShafts (v2f i) : SV_Target
    {
        float2 dir = (_ShaftPos.xy - i.uv);
        float3 c = 0;
        float decay = 1;
        [unroll] for (int k = 0; k < 20; k++)
        {
            float2 uv = i.uv + dir * (k / 20.0) * 0.85;
            c += tex2D(_MainTex, uv).rgb * decay;
            decay *= 0.94;
        }
        return float4(c / 12.0, 1);
    }
    ENDCG

    SubShader
    {
        ZTest Always Cull Off ZWrite Off
        Pass { CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragUber
            #pragma multi_compile_local __ ARENPOST_BLOOM
            #pragma multi_compile_local __ ARENPOST_HDR
            #pragma multi_compile_local __ ARENPOST_CINE
            ENDCG }
        Pass { CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragPrefilter
            ENDCG }
        Pass { CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragBlur
            ENDCG }
        Pass { CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragPrefilterHDR
            ENDCG }
        Pass { CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragDown
            ENDCG }
        Pass { CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragUp
            ENDCG }
        Pass { CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragStreak
            ENDCG }
        Pass { CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragShafts
            ENDCG }
    }
}
