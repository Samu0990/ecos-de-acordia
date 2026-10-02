// Céu do dia da Ruptura: pôr do sol em gradiente, disco do sol, nuvens em faixas
// iluminadas por trás (ruído projetado), e um tom violeta vindo da Fenda.
Shader "Campanula/SunsetSky"
{
    Properties
    {
        _Zenith ("Zênite", Color) = (0.16,0.15,0.30,1)
        _Mid ("Meio", Color) = (0.55,0.36,0.46,1)
        _Horizon ("Horizonte", Color) = (1.0,0.58,0.32,1)
        _Ground ("Abaixo do horizonte", Color) = (0.22,0.16,0.16,1)
        [HDR] _SunColor ("Sol", Color) = (3.2,1.9,0.9,1)
        _SunSize ("Tamanho do sol", Range(0.0005,0.05)) = 0.006
        _Clouds ("Nuvens (ruído)", 2D) = "gray" {}
        _CloudColor ("Cor das nuvens", Color) = (0.95,0.55,0.42,1)
        _CloudShadow ("Sombra das nuvens", Color) = (0.32,0.22,0.32,1)
        _RiftDir ("Direção da Fenda", Vector) = (0.6,0.35,0.7,0)
        _RiftTint ("Tom da Fenda", Color) = (0.55,0.25,0.75,1)
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float4 _Zenith, _Mid, _Horizon, _Ground, _SunColor, _CloudColor, _CloudShadow, _RiftDir, _RiftTint;
            float _SunSize; sampler2D _Clouds;
            struct v2f { float4 pos : SV_POSITION; float3 dir : TEXCOORD0; };
            v2f vert (appdata_base v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.dir = v.vertex.xyz; return o; }
            float3 skyColor(float3 d)
            {
                float y = d.y;
                float3 c = y > 0 ? lerp(_Horizon.rgb, _Mid.rgb, smoothstep(0.0, 0.22, y)) : _Horizon.rgb;
                c = lerp(c, _Zenith.rgb, smoothstep(0.18, 0.75, y));
                c = lerp(c, _Ground.rgb, smoothstep(0.0, -0.12, y));
                return c;
            }
            fixed4 frag (v2f i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float3 sun = normalize(_WorldSpaceLightPos0.xyz);
                float3 col = skyColor(d);
                float sd = dot(d, sun);
                // halo largo e quente em volta do sol (mais forte perto do horizonte)
                col += _Horizon.rgb * pow(saturate(sd), 8) * 0.55 * (1 - saturate(d.y * 2));
                col += _SunColor.rgb * pow(saturate(sd), 180) * 0.6;
                float disk = smoothstep(1 - _SunSize, 1 - _SunSize * 0.6, sd);
                col = lerp(col, _SunColor.rgb, disk);
                // nuvens: ruído projetado num plano alto, duas camadas
                if (d.y > 0.01)
                {
                    float2 uv = d.xz / (d.y + 0.08) * 0.22;
                    float n1 = tex2D(_Clouds, uv + float2(_Time.x * 0.15, 0)).r;
                    float n2 = tex2D(_Clouds, uv * 2.3 + float2(-_Time.x * 0.08, 0.3)).r;
                    float c = smoothstep(0.48, 0.78, n1 * 0.7 + n2 * 0.45);
                    c *= smoothstep(0.02, 0.18, d.y) * (1 - smoothstep(0.55, 0.9, d.y));
                    float lit = pow(saturate(sd * 0.5 + 0.5), 3);
                    float3 cc = lerp(_CloudShadow.rgb, _CloudColor.rgb, lit) + _SunColor.rgb * pow(saturate(sd), 12) * 0.25;
                    col = lerp(col, cc, c * 0.85);
                }
                // a Fenda tinge o céu ao redor
                float rd = dot(d, normalize(_RiftDir.xyz));
                col = lerp(col, _RiftTint.rgb * 0.6, pow(saturate(rd), 14) * 0.55);
                return float4(col, 1);
            }
            ENDCG
        }
    }
}
