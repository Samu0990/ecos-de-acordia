// Lâmina do Sussurrante: obsidiana lascada (quase preta, espelhada) com o FIO aceso em violeta e
// energia correndo da guarda para a ponta; cristais da guarda pulsando; empunhadura de couro
// e faixas de pano. Tudo pelas cores de vértice do modelo (suss_06_blade.py):
//   R = parte (1 lâmina, 0.6 cristal, 0.25 faixa, 0.1 couro), G = borda (0 nervura -> 1 fio),
//   B = ao longo da lâmina (0 base -> 1 ponta).
// _Charge acende tudo no golpe; aceita _Flash/_Telegraph/_Dissolve do EnemyBase (some da ponta
// para a mão).
Shader "Aren/SussurranteBlade"
{
    Properties
    {
        [HDR] _GlowColor ("Fio aceso", Color) = (2.0,0.8,4.2,1)
        _Charge ("Carga do golpe", Range(0,1)) = 0
        _Flash ("Flash", Range(0,1)) = 0
        _Telegraph ("Aviso", Range(0,1)) = 0
        _Dissolve ("Desintegrar", Range(0,1)) = 0
        [HDR] _BurnColor ("Borda acesa", Color) = (2.4,1.1,4.6,1)
        _Noise ("Ruído", 2D) = "gray" {}
        _Seed ("Semente", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 300
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows addshadow vertex:vert
        #pragma target 3.0
        sampler2D _Noise;
        float4 _GlowColor, _BurnColor;
        float _Charge, _Flash, _Telegraph, _Dissolve, _Seed;
        struct Input { float4 vc; float3 op; float3 viewDir; };

        void vert (inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            o.vc = v.color;
            o.op = v.vertex.xyz;
        }

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            float part = IN.vc.r, edge = IN.vc.g, along = IN.vc.b;
            float isBlade = step(0.8, part);
            float isCrystal = step(0.45, part) * (1 - isBlade);
            float isWrap = step(0.18, part) * (1 - step(0.45, part));
            float isGrip = 1 - step(0.18, part);
            float t = _Time.y;
            float3 op = IN.op;

            float nz = tex2D(_Noise, op.xy * 3.1 + op.z * 2.0 + _Seed).r;
            // some da ponta para a mão
            float ds = (1 - along) * 0.55 + nz * 0.45 + isGrip * 0.3 - _Dissolve * 1.15 + 0.04;
            clip(ds);
            float dsEdge = (1 - smoothstep(0.0, 0.07, ds)) * step(0.001, _Dissolve);

            float3 obsidian = float3(0.030, 0.020, 0.045);
            float3 crystal = float3(0.10, 0.05, 0.18);
            float3 wrap = float3(0.11, 0.075, 0.15);
            float3 leather = float3(0.05, 0.035, 0.05);
            o.Albedo = obsidian * isBlade + crystal * isCrystal + wrap * isWrap + leather * isGrip;
            o.Smoothness = 0.9 * isBlade + 0.82 * isCrystal + 0.12 * isWrap + 0.32 * isGrip;
            o.Metallic = 0.3 * isBlade + 0.1 * isCrystal;

            float charge = saturate(_Charge + _Telegraph * 0.8);
            // energia correndo no fio (da guarda para a ponta)
            float flow = tex2D(_Noise, float2(along * 2.6 - t * 0.55, edge * 0.4 + _Seed)).r;
            float flow2 = tex2D(_Noise, float2(along * 7.0 - t * 1.6, 0.37 + _Seed)).g;
            float edgeGlow = pow(saturate(edge), 4.0) * (0.25 + 1.1 * flow * flow2 + charge * 1.6);
            // rachaduras internas (curvas de um ruído = veios dentro da pedra)
            float vein = 1 - abs(tex2D(_Noise, op.xy * float2(1.3, 0.45) + _Seed * 0.7).r * 2 - 1);
            vein = smoothstep(0.92, 0.99, vein) * (0.25 + charge);
            float crystalGlow = isCrystal * (0.55 + 0.45 * sin(t * 2.3 + along * 9.0 + op.x * 30.0)) * (1 + charge * 2.0);
            float rim = pow(1 - saturate(dot(normalize(IN.viewDir), o.Normal)), 4.0);
            float3 em = _GlowColor.rgb * (edgeGlow * isBlade + vein * isBlade + crystalGlow * 0.9)
                      + rim * isBlade * float3(0.25, 0.18, 0.45) * (1 + charge * 2.0)
                      + dsEdge * _BurnColor.rgb * 2.0
                      + _Flash * float3(1.2, 1.1, 1.4);
            o.Emission = em;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
