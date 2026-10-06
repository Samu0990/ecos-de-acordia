// Sussurrante (prancha do autor: "Eco Primordial · Rasgos", elemento Ressonância Distorcida).
//   - pele manchada de ardósia (albedo da prancha), oclusão e suavidade da máscara;
//   - FENDAS violetas (máscara B: peito e relâmpagos do braço esquerdo) que respiram num pulso
//     duplo, como um coração fora do compasso, com a nebulosa da Fenda vista por dentro;
//   - VEIAS (máscara A) quase apagadas no repouso; acendem no aviso de golpe (_Telegraph, do
//     EnemyBase) e no grito (_Veins), correndo como corrente;
//   - contraluz frio da noite; flash de dano (_Flash);
//   - MATERIALIZAR (_Spawn 0 -> 1): o corpo surge dos pés para cima com a borda acesa;
//   - DESINTEGRAR (_Dissolve 0 -> 1): some da cabeça para baixo em lascas, a borda queima violeta
//     (as partículas de lasca são do SussurranteFX).
// Altura medida no MUNDO a partir da origem do renderer (o FBX do Blender vem girado).
Shader "Aren/Sussurrante"
{
    Properties
    {
        _MainTex ("Albedo", 2D) = "white" {}
        [Normal] _BumpMap ("Normal", 2D) = "bump" {}
        _MaskMap ("Máscara (R AO, G suavidade, B fendas, A veias)", 2D) = "white" {}
        _Color ("Tinta", Color) = (1,1,1,1)
        _AOStrength ("Oclusão", Range(0,1)) = 0.85
        [HDR] _GlowColor ("Fendas", Color) = (2.2,0.95,4.2,1)
        _GlowPulse ("Pulso (Hz)", Float) = 0.8
        [HDR] _VeinColor ("Veias", Color) = (1.5,0.45,3.2,1)
        _Veins ("Veias acesas (grito)", Range(0,1)) = 0
        [HDR] _RimColor ("Contraluz", Color) = (0.30,0.28,0.52,1)
        _RimPower ("Contraluz (expoente)", Range(0.5,8)) = 2.6
        _NightLift ("Leitura no escuro", Range(0,0.5)) = 0.13
        _Flash ("Flash de dano", Range(0,1)) = 0
        _Telegraph ("Aviso de golpe", Range(0,1)) = 0
        _Dissolve ("Desintegrar", Range(0,1)) = 0
        _Spawn ("Materializar (1 = inteiro)", Range(0,1)) = 1
        _Height ("Altura do corpo (m)", Float) = 2.85
        [HDR] _BurnColor ("Borda acesa", Color) = (2.4,1.1,4.6,1)
        _Noise ("Ruído", 2D) = "gray" {}
        _Void ("Nebulosa da Fenda", 2D) = "black" {}
        _Seed ("Semente", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 300
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows addshadow
        #pragma target 3.0
        sampler2D _MainTex, _BumpMap, _MaskMap, _Noise, _Void;
        float4 _Color, _GlowColor, _VeinColor, _RimColor, _BurnColor;
        float _AOStrength, _GlowPulse, _Veins, _RimPower, _Flash, _Telegraph, _Dissolve, _Spawn, _Height, _Seed, _NightLift;

        struct Input { float2 uv_MainTex; float3 worldPos; float3 viewDir; float4 screenPos; };

        // batida dupla ("tum-tum") fora do compasso: base acesa + dois picos
        float Heart(float t)
        {
            float ph = frac(t);
            float b1 = exp(-pow((ph - 0.08) / 0.045, 2));
            float b2 = exp(-pow((ph - 0.27) / 0.06, 2)) * 0.65;
            return 0.42 + 0.58 * saturate(b1 + b2);
        }

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            float3 wp = IN.worldPos;
            float3 origin = mul(unity_ObjectToWorld, float4(0, 0, 0, 1)).xyz;
            float3 lp = wp - origin;
            float h01 = saturate(lp.y / max(_Height, 0.01));
            float nz = tex2D(_Noise, lp.xz * 1.9 + lp.y * 0.7 + _Seed).r;
            float nz2 = tex2D(_Noise, float2(lp.x + lp.z, lp.y) * 4.3 + _Seed * 1.37).g;

            // materializar (dos pés para cima) e desintegrar (da cabeça para baixo), em lascas
            float sp = (_Spawn * 1.18 - 0.09) - (h01 + (nz - 0.5) * 0.16);
            float dsField = (1 - h01) * 0.6 + nz * 0.25 + nz2 * 0.15;
            float ds = dsField - _Dissolve * 1.1 + 0.05;
            clip(min(sp, ds));
            float spEdge = (1 - smoothstep(0.0, 0.05, sp)) * step(_Spawn, 0.999);
            float dsEdge = (1 - smoothstep(0.0, 0.06, ds)) * step(0.001, _Dissolve);

            float4 m = tex2D(_MaskMap, IN.uv_MainTex);
            fixed4 alb = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            o.Albedo = alb.rgb;
            o.Normal = UnpackNormal(tex2D(_BumpMap, IN.uv_MainTex));
            o.Smoothness = m.g;
            o.Metallic = 0;
            o.Occlusion = lerp(1, m.r, _AOStrength);

            float t = _Time.y;
            float heart = Heart(t * _GlowPulse + _Seed * 0.31);
            float tele = _Telegraph;
            float2 suv = IN.screenPos.xy / max(IN.screenPos.w, 1e-4);
            suv.x *= _ScreenParams.x / _ScreenParams.y;
            float3 nebula = tex2D(_Void, suv * 1.4 + float2(t * 0.012, t * 0.004)).rgb;

            // fendas: violeta que respira + a nebulosa da Fenda "dentro" do rasgo
            float glowMask = m.b;
            float flicker = 0.85 + 0.3 * tex2D(_Noise, float2(lp.y * 3.0 - t * 0.9, lp.x * 2.0 + _Seed)).b;
            float3 glow = glowMask * (_GlowColor.rgb * heart * flicker + nebula * 1.6) * (1 + tele * 1.8 + _Veins * 1.2);

            // veias: rede fina que acende e corre (aviso de golpe / grito)
            float run = tex2D(_Noise, float2(lp.y * 1.6 - t * 1.7, (lp.x + lp.z) * 1.3)).r;
            float veinOn = saturate(0.04 + tele * 0.6 + _Veins * 0.7);
            float3 vein = m.a * _VeinColor.rgb * veinOn * (0.2 + 1.0 * run * run);

            float rim = pow(1 - saturate(dot(normalize(IN.viewDir), o.Normal)), _RimPower);
            float3 rimC = rim * (_RimColor.rgb + _GlowColor.rgb * 0.18 * (tele + _Veins));

            float3 burn = (spEdge + dsEdge) * (_BurnColor.rgb * (1.2 + nz2) + nebula * 2.0);
            // a pele bege não pode virar silhueta na noite da vila: um pouco de luar "embutido"
            float3 lift = alb.rgb * _NightLift * float3(0.75, 0.8, 1.0) * o.Occlusion;
            // flash de dano: lampejo violeta claro (branco puro apagava o corpo inteiro)
            o.Emission = glow + vein + rimC + burn + lift + _Flash * float3(0.55, 0.42, 0.75);
        }
        ENDCG
    }
    FallBack "Diffuse"
}
