// Riacho e rio do cânion (v2). Antes: uma cor com ondulação de ruído. Agora, como água de verdade:
//   · ondulação em duas camadas de mapa de normais (ondas geradas por soma de senos, sem emenda) que
//     correm com a correnteza para o sul, em velocidades e escalas diferentes;
//   · Fresnel (Schlick, F0 = 0,02): de cima se vê o fundo escuro, rasante vira espelho;
//   · o que reflete: de dia o céu do pôr do sol (_Sky); à noite (_NightOn) o céu noturno na direção
//     refletida, o caminho de luz da lua (brilho especular estreito que cintila nas ondas) e o violeta da
//     Fenda; e as luzes da cidade (CityLight), que viram faixas quentes tremendo na água;
//   · espuma nas margens (uv.x da malha atravessa o rio) e um pouco onde a água corre mais rápido;
//   · névoa do Unity. Transparente, sem escrever profundidade (barato: sem grab nem reflexo real).
Shader "Campanula/Water"
{
    Properties
    {
        _Deep ("Fundo", Color) = (0.08,0.16,0.18,0.85)
        _Sky ("Reflexo do céu (dia)", Color) = (0.9,0.55,0.45,1)
        _Noise ("Ruído", 2D) = "gray" {}
        [Normal] _WaveN ("Ondas (normal)", 2D) = "bump" {}
        _Flow ("Fluxo (xy)", Vector) = (0,0.12,0,0)
        _WaveK ("Força das ondas", Range(0, 1.5)) = 0.55
        _Foam ("Espuma", Color) = (0.75,0.8,0.85,1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Pass
        {
            Tags { "LightMode"="ForwardBase" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma target 3.0
            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "Assets/Aren/Resources/Shaders/NightCommon.cginc"
            float4 _Deep, _Sky, _Flow, _Foam; sampler2D _Noise, _WaveN;
            float _WaveK, _NightOn;
            sampler2D _CityLightTex;
            float4 _CityLightRect;
            float _CityLightK;
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 p : SV_POSITION; float3 w : TEXCOORD0; float2 uv : TEXCOORD1; UNITY_FOG_COORDS(2) };
            v2f vert (appdata v) { v2f o; o.p = UnityObjectToClipPos(v.vertex); o.w = mul(unity_ObjectToWorld, v.vertex).xyz; o.uv = v.uv; UNITY_TRANSFER_FOG(o, o.p); return o; }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 wp = i.w;
                float t = _Time.y;
                // correnteza para o sul (−z); no fundo do cânion (y baixo) corre mais
                float speed = lerp(1.0, 1.8, saturate(-wp.y / 12.0));
                float2 uv1 = wp.xz / 7.0 + float2(0.0, 1.0) * t * 0.11 * speed;
                float2 uv2 = wp.xz / 3.1 * float2(1.0, 0.8) + float2(0.13, 1.0) * t * 0.23 * speed;
                float3 n1 = UnpackNormal(tex2D(_WaveN, uv1));
                float3 n2 = UnpackNormal(tex2D(_WaveN, uv2 + 0.37));
                float2 g = (n1.xy + n2.xy * 0.7) * _WaveK;
                float3 N = normalize(float3(g.x, 1, g.y));
                float3 V = normalize(_WorldSpaceCameraPos - wp);
                // Fresnel com a normal suavizada (rasante, a normal cheia fazia manchas claro/escuro)
                float ndv = saturate(dot(normalize(lerp(N, float3(0, 1, 0), 0.65)), V));
                float fres = 0.02 + 0.98 * pow(1 - ndv, 5);
                float3 R = reflect(-V, N);

                // o que a água reflete
                float3 L = normalize(_WorldSpaceLightPos0.xyz);
                float3 daySky = _Sky.rgb;
                float3 nightSky = NightHorizon(R);
                nightSky += _FendaLight.rgb * pow(saturate(dot(R, normalize(_FendaDirW.xyz + 1e-4))), 30.0) * 0.8;
                float3 refl = lerp(daySky, nightSky * 1.4, _NightOn);
                // lua (ou sol): caminho de luz estreito que cintila
                float spec = pow(saturate(dot(R, L)), 180.0) * 6.0 + pow(saturate(dot(R, L)), 24.0) * 0.25;
                float3 deep = lerp(_Deep.rgb, float3(0.012, 0.022, 0.032), _NightOn);   // à noite o fundo é quase preto
                float3 col = lerp(deep, refl, fres);
                col += _LightColor0.rgb * spec * (0.5 + 0.6 * fres);

                // luzes da cidade refletidas: faixas quentes tremendo nas ondas
                if (_CityLightK > 0.001)
                {
                    float2 cu = (wp.xz + g * 1.6 - _CityLightRect.xy) * _CityLightRect.zw;
                    if (cu.x > 0 && cu.y > 0 && cu.x < 1 && cu.y < 1)
                    {
                        float4 Lc = tex2D(_CityLightTex, cu);
                        // só as cristas viradas para a luz brilham (faixas tremendo, não um lençol claro)
                        float glint = smoothstep(0.12, 0.45, abs(g.x * 0.8 + g.y));
                        col += Lc.rgb * _CityLightK * (0.15 + 0.6 * fres) * glint * 0.32;
                    }
                }

                // espuma nas margens e em manchas que correm com a água
                float edge = min(i.uv.x, 1 - i.uv.x);
                float fn = tex2D(_Noise, wp.xz * 0.35 + float2(0, t * 0.25 * speed)).r;
                float foam = saturate((1 - smoothstep(0.0, 0.12 + fn * 0.1, edge)) * (0.5 + fn));
                foam += smoothstep(0.72, 0.9, fn * 0.6 + tex2D(_Noise, wp.xz * 0.9 + float2(0, t * 0.5 * speed)).r * 0.5) * 0.35;
                float3 amb = ShadeSH9(float4(0, 1, 0, 1));
                col = lerp(col, _Foam.rgb * (amb * 1.4 + _LightColor0.rgb * 0.3), saturate(foam) * 0.4);

                float a = lerp(_Deep.a, 1, fres);
                a = saturate(a + foam * 0.3);
                fixed4 c = fixed4(col, a);
                UNITY_APPLY_FOG(i.fogCoord, c);
                return c;
            }
            ENDCG
        }
    }
}
