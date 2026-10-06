// O Mar de Vidro: oceano que cristaliza sob certas frequências. Facetas de Voronoi (cada célula com sua
// normal inclinada, como placas de vidro), reflexo do céu forte por Fresnel, verde-água profundo embaixo,
// rachaduras claras entre as placas e cintilação lenta. Trechos ainda líquidos ondulam (máscara de ruído).
Shader "Elyndra/GlassSea"
{
    Properties
    {
        _Deep ("Fundo", Color) = (0.02,0.12,0.14,1)
        _Shallow ("Raso", Color) = (0.1,0.45,0.48,1)
        _SkyRefl ("Reflexo do céu", Color) = (0.55,0.75,0.85,1)
        [HDR] _Crack ("Rachaduras", Color) = (0.7,1.1,1.2,1)
        _Cell ("Tamanho das placas (m)", Float) = 9
        _Noise ("Ruído", 2D) = "grey" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry+10" }
        Pass
        {
            Tags { "LightMode"="ForwardBase" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            float4 _Deep, _Shallow, _SkyRefl, _Crack; float _Cell; sampler2D _Noise;
            struct v2f { float4 pos : SV_POSITION; float3 wp : TEXCOORD0; UNITY_FOG_COORDS(1) };
            v2f vert (appdata_base v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.wp = mul(unity_ObjectToWorld, v.vertex).xyz; UNITY_TRANSFER_FOG(o, o.pos); return o; }
            float2 h2(float2 p) { p = float2(dot(p, float2(127.1, 311.7)), dot(p, float2(269.5, 183.3))); return frac(sin(p) * 43758.5453); }
            fixed4 frag (v2f i) : SV_Target
            {
                float2 g = i.wp.xz / _Cell;
                float2 ip = floor(g), fp = frac(g);
                float d1 = 8, d2 = 8; float2 best = 0;
                for (int y = -1; y <= 1; y++) for (int x = -1; x <= 1; x++)
                {
                    float2 o = float2(x, y); float2 r = o + h2(ip + o) - fp; float d = dot(r, r);
                    if (d < d1) { d2 = d1; d1 = d; best = ip + o; } else if (d < d2) d2 = d;
                }
                float edge = sqrt(d2) - sqrt(d1);
                float2 tilt = (h2(best * 1.7) - 0.5) * 0.35;
                float liquid = smoothstep(0.55, 0.75, tex2D(_Noise, i.wp.xz / 260.0).r);
                float2 wave = (tex2D(_Noise, i.wp.xz / 23.0 + _Time.y * 0.02).rg - 0.5) * 0.25;
                float3 N = normalize(float3(lerp(tilt.x, wave.x, liquid), 1, lerp(tilt.y, wave.y, liquid)));
                float3 V = normalize(_WorldSpaceCameraPos - i.wp);
                float fres = 0.04 + 0.96 * pow(1 - saturate(dot(N, V)), 5);
                float3 L = normalize(_WorldSpaceLightPos0.xyz);
                float spec = pow(saturate(dot(reflect(-V, N), L)), 120) * 3;
                float3 body = lerp(_Shallow.rgb, _Deep.rgb, saturate(h2(best).x * 0.6 + 0.3));
                float3 c = lerp(body, _SkyRefl.rgb, fres) + _LightColor0.rgb * spec;
                float crack = (1 - smoothstep(0.0, 0.06, edge)) * (1 - liquid);
                float tw = 0.6 + 0.4 * sin(_Time.y * 0.8 + best.x * 3.1 + best.y * 1.7);
                c += _Crack.rgb * crack * tw;
                fixed4 col = fixed4(c, 1);
                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }
    }
}
