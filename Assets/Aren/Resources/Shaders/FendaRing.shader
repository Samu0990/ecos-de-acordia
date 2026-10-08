// O círculo da Fenda: o anel de luz que desce do céu rasgado, voa até a pessoa e a consome.
// Malha = faixa em anel (u ao redor, v através). Núcleo branco-violeta fino, halo largo que cai suave,
// marcas rúnicas que giram e piscam ao longo do anel (_Dash = 1 vira um círculo tracejado de runas),
// borda de dentro puxada para o ciano e a de fora para o magenta (aberração de cor da Fenda).
Shader "Aren/FX/FendaRing"
{
    Properties
    {
        [HDR] _Color ("Núcleo", Color) = (2.4,1.6,4.0,1)
        [HDR] _Glow ("Halo", Color) = (1.1,0.35,2.2,1)
        _Noise ("Ruído", 2D) = "gray" {}
        _Spin ("Giro das runas", Float) = 0.6
        _Dash ("Runas tracejadas", Range(0,1)) = 0
        _Intensity ("Intensidade", Range(0,4)) = 1
        _Core ("Espessura do núcleo", Range(0.02,0.6)) = 0.16
    }
    SubShader
    {
        Tags { "Queue"="Transparent+20" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend One One
        ZWrite Off
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float4 _Color, _Glow; sampler2D _Noise; float _Spin, _Dash, _Intensity, _Core;
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
            v2f vert (appdata_base v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.uv = v.texcoord.xy; return o; }
            fixed4 frag (v2f i) : SV_Target
            {
                float u = i.uv.x, v = i.uv.y;
                float d = (v - 0.5) * 2;                      // -1 dentro … +1 fora
                float ad = abs(d);
                float core = exp(-pow(ad / _Core, 2));
                float halo = exp(-ad * 3.2) * (1 - ad);
                float t = _Time.y;
                float flick = tex2D(_Noise, float2(u * 7 + t * _Spin, t * 0.37)).r;
                float runes = tex2D(_Noise, float2(u * 31 - t * _Spin * 2.3, 0.61)).r;
                float tick = smoothstep(0.62, 0.8, runes) * smoothstep(0.7, 0.2, ad);
                float dash = lerp(1, smoothstep(0.25, 0.35, frac(u * 18 + t * _Spin * 1.7)) * smoothstep(0.95, 0.85, frac(u * 18 + t * _Spin * 1.7)), _Dash);
                float3 tint = lerp(float3(0.55, 1.15, 1.35), float3(1.25, 0.6, 1.15), saturate(d * 0.5 + 0.5));
                float3 col = _Color.rgb * core * (0.75 + 0.5 * flick) + _Glow.rgb * tint * halo * (0.55 + 0.6 * flick) + _Color.rgb * tick * 0.9;
                return float4(col * dash * _Intensity, 1);
            }
            ENDCG
        }
    }
}
