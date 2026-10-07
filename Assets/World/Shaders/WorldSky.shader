// Céu dos reinos de Elyndra: gradiente do reino, sol (ou só o halo, quando a névoa esconde o disco), nuvens
// em duas camadas que fecham o céu conforme _Overcast (céus pesados = mundo mais sombrio), estrelas só onde
// o reino é de noite ou perto da Fenda, e uma faixa de névoa no horizonte com a MESMA cor da neblina da cena
// — as serras distantes e o chão somem nela sem costura.
Shader "Elyndra/WorldSky"
{
    Properties
    {
        _Zenith ("Zênite", Color) = (0.1,0.1,0.2,1)
        _Mid ("Meio", Color) = (0.4,0.3,0.4,1)
        _Horizon ("Horizonte", Color) = (0.8,0.5,0.4,1)
        _HazeH ("Altura da névoa", Range(0.01,0.5)) = 0.12
        _Ground ("Abaixo do horizonte", Color) = (0.2,0.16,0.16,1)
        [HDR] _SunColor ("Sol", Color) = (3,1.9,0.9,1)
        _SunSize ("Tamanho do sol", Range(0.0005,0.05)) = 0.005
        _SunDisk ("Disco do sol visível", Range(0,1)) = 1
        _Clouds ("Nuvens (ruído)", 2D) = "gray" {}
        _CloudColor ("Cor das nuvens", Color) = (0.9,0.6,0.5,1)
        _CloudShadow ("Sombra das nuvens", Color) = (0.25,0.2,0.27,1)
        _Overcast ("Céu coberto", Range(0,1)) = 0.35
        _RiftDir ("Direção da Fenda", Vector) = (0.3,0.3,0.9,0)
        _RiftTint ("Tom da Fenda", Color) = (0.55,0.25,0.75,1)
        _Stars ("Estrelas", 2D) = "black" {}
        _StarK ("Estrelas (noite do reino)", Range(0,2)) = 0.2
        [HDR] _RingColor ("Eclipse (cor; alfa 0 = sem)", Color) = (0,0,0,0)
        _RingDir ("Eclipse (direção)", Vector) = (0.35,0.45,0.82,0)
        _RingSize ("Eclipse (raio)", Range(0.01,0.3)) = 0.07
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
            float _SunSize, _SunDisk, _Overcast, _StarK, _HazeH, _RingSize; sampler2D _Clouds, _Stars;
            float4 _RingColor, _RingDir;
            struct v2f { float4 pos : SV_POSITION; float3 dir : TEXCOORD0; };
            v2f vert (appdata_base v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.dir = v.vertex.xyz; return o; }
            fixed4 frag (v2f i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float3 sun = normalize(_WorldSpaceLightPos0.xyz);
                float y = d.y;
                float3 col = lerp(_Horizon.rgb, _Mid.rgb, smoothstep(0.0, 0.25, y));
                col = lerp(col, _Zenith.rgb, smoothstep(0.2, 0.8, y));
                float sd = dot(d, sun);
                // halo largo (mais forte perto do horizonte) e disco
                col += _Horizon.rgb * pow(saturate(sd), 8) * 0.5 * (1 - saturate(y * 2));
                col += _SunColor.rgb * pow(saturate(sd), 160) * 0.5;
                float disk = smoothstep(1 - _SunSize, 1 - _SunSize * 0.6, sd) * _SunDisk;
                col = lerp(col, _SunColor.rgb, disk);
                // estrelas: noite do reino + a Fenda "deixa entrar" a noite em volta dela
                float rdir = dot(d, normalize(_RiftDir.xyz));
                if (y > 0.0)
                {
                    float2 suv = d.xz / (y + 0.35) * 0.9;
                    float3 st = tex2D(_Stars, suv).rgb;
                    float tw = 0.65 + 0.35 * sin(_Time.y * 2.7 + suv.x * 41 + suv.y * 23);
                    float vis = saturate(pow(saturate(rdir), 6) * 0.9 + smoothstep(0.1, 0.7, y) * _StarK);
                    col += st * tw * vis * 1.3 * (1 - _Overcast * 0.85);
                }
                // nuvens: duas camadas; _Overcast fecha o céu (cobertura e peso)
                if (y > 0.005)
                {
                    float2 uv = d.xz / (y + 0.08) * 0.22;
                    float n1 = tex2D(_Clouds, uv + float2(_Time.x * 0.12, 0)).r;
                    float n2 = tex2D(_Clouds, uv * 2.3 + float2(-_Time.x * 0.07, 0.3)).r;
                    float n3 = tex2D(_Clouds, uv * 0.45 + float2(0.37, _Time.x * 0.03)).r;
                    float th = lerp(0.5, 0.18, _Overcast);
                    float c = smoothstep(th, th + 0.3, n1 * 0.65 + n2 * 0.4 + n3 * 0.25 - 0.12);
                    c *= smoothstep(0.0, 0.12, y);
                    float lit = pow(saturate(sd * 0.5 + 0.5), 3) * (1 - _Overcast * 0.55);
                    float thick = saturate(c * 1.4 - 0.2);
                    float3 cc = lerp(_CloudShadow.rgb, _CloudColor.rgb, lit * (1 - thick * 0.6)) + _SunColor.rgb * pow(saturate(sd), 10) * 0.22 * (1 - thick);
                    col = lerp(col, cc, c * lerp(0.8, 0.97, _Overcast));
                }
                // eclipse (Nereth): disco escuro com anel aceso e coroa — por cima das nuvens finas
                if (_RingColor.a > 0.001)
                {
                    float ang = acos(clamp(dot(d, normalize(_RingDir.xyz)), -1, 1));
                    float r = _RingSize;
                    float ring = exp(-pow((ang - r) / (r * 0.1), 2));
                    float corona = ang > r ? exp(-(ang - r) / (r * 0.7)) : 0;
                    col = lerp(col, col * 0.08, smoothstep(r, r * 0.94, ang));
                    col += _RingColor.rgb * (ring * 1.4 + corona * 0.45) * _RingColor.a;
                }
                // a Fenda tinge o céu ao redor
                col = lerp(col, _RiftTint.rgb * 0.6, pow(saturate(rdir), 14) * 0.55);
                // névoa do horizonte = a cor da neblina da cena (unity_FogColor, que a Corrupção também muda):
                // as serras e o chão somem nela sem costura
                float haze = 1 - smoothstep(0.0, _HazeH, y);
                col = lerp(col, unity_FogColor.rgb, haze * 0.92);
                col = lerp(col, _Ground.rgb, smoothstep(0.0, -0.15, y));
                return float4(col, 1);
            }
            ENDCG
        }
    }
}
