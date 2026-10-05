// Céu da noite da Ruptura (abertura e começo do jogo): gradiente noturno, lua com halo,
// estrelas cintilando, nuvens finas iluminadas pela lua, duas serras distantes no horizonte
// (desenhadas aqui no céu, então nunca somem pelo far clip), a Fenda MUITO longe a
// nor-nordeste — rasgo de luz serrilhado, feixe fino acima, estilhaços escuros flutuando e o
// céu tingido de violeta em volta — e o clarão do impacto atrás da serra do leste.
// A função Ridge() tem uma cópia idêntica em C# (NightSky.Ridge) para os brilhos sumirem
// atrás das montanhas no lugar certo.
Shader "Hidden/Aren/NightSky"
{
    Properties
    {
        _Zenith ("Zênite", Color) = (0.010,0.012,0.028,1)
        _Mid ("Meio", Color) = (0.028,0.033,0.065,1)
        _Horizon ("Horizonte", Color) = (0.085,0.085,0.135,1)
        _Ground ("Abaixo do horizonte", Color) = (0.02,0.02,0.035,1)
        _MoonDir ("Direção da lua", Vector) = (-0.78,0.52,-0.34,0)
        [HDR] _MoonColor ("Lua", Color) = (1.5,1.6,1.9,1)
        _MoonRadius ("Raio da lua (rad)", Float) = 0.022
        _Stars ("Estrelas", 2D) = "black" {}
        _StarBoost ("Brilho das estrelas", Float) = 1.3
        _Clouds ("Nuvens (ruído)", 2D) = "gray" {}
        _CloudLit ("Nuvem iluminada", Color) = (0.2,0.21,0.27,1)
        _CloudDark ("Nuvem escura", Color) = (0.022,0.025,0.042,1)
        _RidgeNear ("Serra perto", Color) = (0.022,0.026,0.048,1)
        _RidgeFar ("Serra longe", Color) = (0.055,0.06,0.105,1)
        _FendaAz ("Azimute da Fenda (rad)", Float) = 0.30
        _FendaOpen ("Abertura da Fenda", Range(0,1)) = 0
        _FendaPulse ("Pulso da Fenda", Range(0,1)) = 0
        [HDR] _FendaColor ("Luz da Fenda", Color) = (0.6,0.34,1.25,1)
        [HDR] _FendaCore ("Núcleo da Fenda", Color) = (1.5,1.35,2.1,1)
        _FlashAz ("Azimute do impacto (rad)", Float) = 1.31
        _Flash ("Clarão do impacto", Range(0,1)) = 0
        [HDR] _FlashColor ("Cor do clarão", Color) = (2.4,1.7,0.9,1)
        _Noise ("Ruído", 2D) = "gray" {}
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
            #pragma target 3.0
            #include "UnityCG.cginc"
            float4 _Zenith, _Mid, _Horizon, _Ground, _MoonDir, _MoonColor, _CloudLit, _CloudDark, _RidgeNear, _RidgeFar, _FendaColor, _FendaCore, _FlashColor;
            float _MoonRadius, _StarBoost, _FendaAz, _FendaOpen, _FendaPulse, _FlashAz, _Flash;
            sampler2D _Stars, _Clouds, _Noise;
            struct v2f { float4 pos : SV_POSITION; float3 dir : TEXCOORD0; };
            v2f vert (appdata_base v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.dir = v.vertex.xyz; return o; }

            float WrapPi(float a) { return a - 6.2831853 * floor((a + 3.14159265) / 6.2831853); }

            // altura da serra (seno da elevação) em função do azimute (0 = norte, +x = leste)
            float Ridge(float az)
            {
                float r = 0.032 + 0.012 * sin(3 * az + 0.8) + 0.008 * sin(7 * az + 2.1) + 0.005 * sin(13 * az + 4.4)
                        + 0.0025 * sin(29 * az + 1.3) + 0.0012 * sin(61 * az + 2.9);
                float e = WrapPi(az - 1.31); r += 0.04 * exp(-e * e / 0.3);    // serra do leste (onde o brilho cai)
                float f = WrapPi(az - 0.30); r += 0.018 * exp(-f * f / 0.4);   // montanhas atrás da Fenda
                return r;
            }
            float RidgeFar(float az)
            {
                return 0.046 + 0.012 * sin(5 * az + 1.7) + 0.007 * sin(11 * az + 0.4) + 0.0035 * sin(23 * az + 3.3) + 0.0015 * sin(47 * az + 0.9);
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float y = d.y;
                float az = atan2(d.x, d.z);
                // gradiente noturno + névoa clara no horizonte
                float3 col = lerp(_Horizon.rgb, _Mid.rgb, smoothstep(0.0, 0.25, y));
                col = lerp(col, _Zenith.rgb, smoothstep(0.2, 0.85, y));
                col += _Horizon.rgb * 0.35 * exp(-abs(y) / 0.04);
                if (y < 0) col = lerp(col, _Ground.rgb, smoothstep(0.0, -0.1, y));

                // lua
                float3 md3 = normalize(_MoonDir.xyz);
                float md = dot(d, md3);
                col += _MoonColor.rgb * (pow(saturate(md), 600) * 0.35 + pow(saturate(md), 60) * 0.05 + pow(saturate(md), 8) * 0.02);
                float cr = cos(_MoonRadius);
                float disk = smoothstep(cos(_MoonRadius * 1.08), cr, md);
                if (disk > 0)
                {
                    float3 t1 = normalize(cross(md3, float3(0, 1, 0))), t2 = cross(t1, md3);
                    float2 luv = float2(dot(d, t1), dot(d, t2)) / _MoonRadius;
                    float mare = tex2D(_Noise, luv * 0.35 + 0.3).r;
                    col = lerp(col, _MoonColor.rgb * (0.78 + 0.22 * mare), disk);
                }

                // estrelas (somem perto do horizonte e atrás das nuvens)
                float stars = 0;
                if (y > 0)
                {
                    float2 suv = d.xz / (1 + y) * 2.2;            // estereográfica: sem riscos perto do horizonte
                    float3 st = tex2D(_Stars, suv).rgb;
                    st = saturate((st - 0.18) * 1.6);              // só as estrelas mais fortes
                    float tw = 0.6 + 0.4 * sin(_Time.y * 2.3 + suv.x * 37 + suv.y * 29);
                    stars = 1;
                    col += st * st * tw * _StarBoost * smoothstep(0.03, 0.25, y) * (1 - disk);
                }

                // nuvens finas (prateadas pela lua; violeta perto da Fenda)
                float fa = WrapPi(az - _FendaAz);
                if (y > 0.015)
                {
                    float2 uv = d.xz / (y + 0.3) * 0.18;
                    float n1 = tex2D(_Clouds, uv + float2(_Time.x * 0.12, 0)).r;
                    float n2 = tex2D(_Clouds, uv * 2.6 + float2(-_Time.x * 0.07, 0.4)).r;
                    float c = smoothstep(0.55, 0.85, n1 * 0.7 + n2 * 0.45) * 0.7;
                    c *= smoothstep(0.03, 0.2, y) * (1 - smoothstep(0.6, 0.95, y));
                    float lit = pow(saturate(md * 0.5 + 0.5), 4);
                    float3 cc = lerp(_CloudDark.rgb, _CloudLit.rgb, lit * 0.8);
                    cc += _FendaColor.rgb * 0.25 * _FendaOpen * exp(-abs(fa) / 0.5) * smoothstep(0.7, 0.05, y);
                    col = lerp(col, cc, c * 0.75);
                }

                // a Fenda: rasgo de luz muito distante, serrilhado, com feixe e estilhaços
                if (_FendaOpen > 0.001 && abs(fa) < 0.6 && y > -0.05)
                {
                    float u = fa * sqrt(saturate(1 - y * y));
                    float v = y;
                    float h = 0.24 * _FendaOpen;   // muito longe: alto, mas não domina o céu
                    // rasgo: curva suave + serrilhado fino (sem o zigue-zague de raio)
                    float jag = (tex2D(_Noise, float2(v * 1.6, 0.37)).r - 0.5) * 0.016 + (tex2D(_Noise, float2(v * 9.0, 0.71)).r - 0.5) * 0.0035;
                    float ux = u - jag - v * 0.02;
                    float taper = sqrt(saturate(1 - v / max(h, 1e-3)));
                    float w = _FendaOpen * (0.0016 + 0.0075 * taper) * (0.85 + 0.3 * tex2D(_Noise, float2(v * 5 + _Time.y * 0.04, 0.5)).r);
                    float inside = v < h ? 1 : 0;
                    float core = exp(-(ux * ux) / (w * w + 1e-7)) * inside;
                    float glow = exp(-abs(ux) / (0.014 + 0.035 * taper)) * smoothstep(h + 0.1, 0.0, v) * _FendaOpen;
                    float aura = exp(-(ux * ux) / 0.008) * smoothstep(h + 0.2, 0.0, v) * _FendaOpen;   // véu largo
                    float haze = 0.55 + 0.45 * smoothstep(0.0, 0.12, v);   // a atmosfera apaga a base (distância)
                    float beam = exp(-ux * ux / 0.000015) * step(h, v) * smoothstep(0.95, h, v) * _FendaOpen * 0.55;
                    float n = tex2D(_Noise, float2(ux * 9 + 0.13, v * 9 - _Time.y * 0.015)).r;
                    float region = smoothstep(0.24, 0.04, abs(ux)) * smoothstep(h + 0.16, 0.0, v) * _FendaOpen;
                    float shard = smoothstep(0.63, 0.66, n) * region * (1 - core);
                    float rim = (smoothstep(0.6, 0.625, n) - smoothstep(0.625, 0.65, n)) * region;
                    float pulse = 0.85 + 0.15 * _FendaPulse + 0.05 * sin(_Time.y * 1.7);
                    col *= 1 - shard * 0.85;
                    col += (_FendaColor.rgb * (glow * 0.4 * pulse + aura * 0.1 + rim * 0.6) + _FendaCore.rgb * (core + beam * 0.5)) * haze;
                    col += _FendaColor.rgb * 0.08 * exp(-abs(fa) / 0.35) * smoothstep(0.75, 0.0, v) * _FendaOpen;
                }

                // clarão do impacto subindo atrás da serra do leste
                float fe = WrapPi(az - _FlashAz);
                float dome = exp(-fe * fe / 0.03) * exp(-max(0.0, y) / 0.1) * _Flash;
                col += _FlashColor.rgb * dome;

                // serras: a de longe (mais clara, névoa) e a de perto (silhueta), com aro de luz
                float rf = RidgeFar(az), rn = Ridge(az);
                float lightAt = _Flash * exp(-fe * fe / 0.05) * 2.0 + _FendaOpen * 0.6 * exp(-fa * fa / 0.04);
                if (y < rf)
                {
                    float k = saturate((rf - y) / 0.05);
                    float3 far = lerp(_Horizon.rgb * 0.95, _RidgeFar.rgb, sqrt(k));
                    far += (_FlashColor.rgb * 0.5 + _FendaColor.rgb * 0.3) * lightAt * exp(-(rf - y) / 0.004) * 0.6;
                    col = far;
                }
                if (y < rn)
                {
                    float k = saturate((rn - y) / 0.07);
                    float3 nearc = lerp(_Horizon.rgb * 0.7, _RidgeNear.rgb, sqrt(k));
                    float edge = exp(-(rn - y) / 0.0025);
                    nearc += (_FlashColor.rgb * _Flash * exp(-fe * fe / 0.05) * 1.6 + _FendaColor.rgb * _FendaOpen * 0.25 * exp(-fa * fa / 0.05) + _MoonColor.rgb * 0.05) * edge;
                    col = nearc;
                }
                return float4(col, 1);
            }
            ENDCG
        }
    }
}
