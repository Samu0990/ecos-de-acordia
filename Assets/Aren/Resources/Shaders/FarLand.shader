// A paisagem distante de Campanula (FarLands.cs): até ~11 km. Sem o fog do Unity (apagaria tudo
// depois de 400 m): tem a própria perspectiva aérea — névoa exponencial de altura integrada ao
// longo do raio + névoa baixa nos vales — com a cor do horizonte do céu, o brilho da lua e da
// Fenda espalhados na névoa. Luz: lua (difusa com "wrap"), ambiente de céu/chão, contraluz
// violeta da Fenda nas encostas voltadas para ela, a luz quente do impacto, a onda de choque
// (anel que corre pelo chão, com borda irregular, e a segunda onda) e o rio refletindo a lua.
// Máscaras na cor do vértice: r floresta, g rocha, b neve, a água.
Shader "Hidden/Aren/FarLand"
{
    Properties
    {
        _Noise ("Ruído", 2D) = "gray" {}
        _Grass ("Campo", Color) = (0.16,0.18,0.11,1)
        _Forest ("Floresta", Color) = (0.05,0.075,0.05,1)
        _Rock ("Rocha", Color) = (0.2,0.2,0.21,1)
        _Snow ("Neve", Color) = (0.78,0.82,0.9,1)
    }
    SubShader
    {
        Tags { "Queue"="Geometry+50" "RenderType"="Opaque" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            #include "NightCommon.cginc"
            sampler2D _Noise;
            float4 _Grass, _Forest, _Rock, _Snow;

            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; float4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; float3 wp : TEXCOORD0; float3 n : TEXCOORD1; float4 m : TEXCOORD2; };

            v2f vert (appdata v)
            {
                v2f o;
                o.wp = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.pos = UnityWorldToClipPos(o.wp);
                o.n = UnityObjectToWorldNormal(v.normal);
                o.m = v.color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 N = normalize(i.n);
                float3 P = i.wp;
                float3 V = P - _WorldSpaceCameraPos;
                float dist = length(V);
                V /= dist;
                float forest = i.m.r, rock = i.m.g, snow = i.m.b, water = i.m.a;

                // albedo: campo → floresta (copa com textura) → rocha → neve
                float2 xz = P.xz;
                float c1 = tex2D(_Noise, xz / 95.0).r, c2 = tex2D(_Noise, xz / 21.0 + 0.37).r, c3 = tex2D(_Noise, xz / 640.0 + 0.71).r;
                float canopy = 0.45 + c1 * 0.7 + (c2 - 0.5) * 0.6 * saturate(1.0 - dist / 3500.0);
                float3 alb = _Grass.rgb * (0.75 + c3 * 0.5);
                alb = lerp(alb, _Forest.rgb * canopy, forest);
                alb = lerp(alb, _Rock.rgb * (0.7 + c1 * 0.6), rock);
                alb = lerp(alb, _Snow.rgb * (0.85 + c2 * 0.2), snow);

                // relevo fino: normal perturbada pelo gradiente do ruído (quebra as facetas da malha)
                float2 g1 = float2(tex2D(_Noise, xz / 260.0 + float2(0.004, 0)).r - tex2D(_Noise, xz / 260.0 - float2(0.004, 0)).r,
                                   tex2D(_Noise, xz / 260.0 + float2(0, 0.004)).r - tex2D(_Noise, xz / 260.0 - float2(0, 0.004)).r);
                float2 g2 = float2(c1 - tex2D(_Noise, xz / 95.0 + float2(0.01, 0)).r, c1 - tex2D(_Noise, xz / 95.0 + float2(0, 0.01)).r);
                float bump = (0.6 + rock * 1.2) * saturate(1.3 - dist / 9000.0);
                N = normalize(N + float3(g1.x + g2.x * 0.5, 0, g1.y + g2.y * 0.5) * 9.0 * bump);

                // luz
                float3 L = normalize(_NightMoonDir.xyz);
                float ndl = dot(N, L);
                float diff = saturate((ndl + 0.18) / 1.18);
                float3 amb = lerp(_NightAmbGround.rgb, _NightAmbSky.rgb, N.y * 0.5 + 0.5);
                float3 lit = alb * (_NightMoonCol.rgb * diff * 1.6 + amb * 1.5);

                // Fenda: contraluz violeta nas encostas voltadas para ela, mais forte perto do azimute dela
                float3 F = normalize(_FendaDirW.xyz);
                float2 pd = normalize(P.xz + 1e-3);
                float near = exp(-(1.0 - dot(pd, normalize(F.xz + 1e-4))) * 7.0);
                lit += alb * _FendaLight.rgb * saturate(dot(N, F) + 0.2) * (0.1 + near) * 1.2;
                lit += _FendaLight.rgb * near * snow * 0.12;

                // impacto: luz quente pontual (quilômetros de alcance) + brilho do pó perto do centro
                float3 toI = _ImpactPosW.xyz + float3(0, 60, 0) - P;
                float di = length(toI);
                float atten = 1.0 / (1.0 + (di / 450.0) * (di / 450.0));
                lit += alb * _ImpactLight.rgb * saturate(dot(N, toI / di) * 0.8 + 0.2) * atten * 2.5;
                float dI2 = length(P.xz - _ImpactPosW.xz);
                lit += _ImpactLight.rgb * exp(-dI2 / 160.0) * 0.35;

                // a nota que desce: luz pontual móvel (o chão do planalto acende antes do impacto)
                float3 toN = _NoteLightPos.xyz - P;
                float dn = length(toN);
                lit += alb * _NoteLight.rgb * saturate(dot(N, toN / dn) * 0.8 + 0.2) * (1.0 / (1.0 + (dn / 600.0) * (dn / 600.0))) * 6.0;

                // rio: reflete o céu, a lua e a Fenda (ondulação pelo ruído)
                if (water > 0.01)
                {
                    float2 rip = float2(tex2D(_Noise, xz / 40.0 + _Time.x * 0.3).r, tex2D(_Noise, xz / 37.0 - _Time.x * 0.25 + 0.5).r) - 0.5;
                    float3 Nw = normalize(float3(rip.x * 0.18, 1, rip.y * 0.18));
                    float3 R = reflect(V, Nw);
                    float fres = 0.25 + 0.75 * pow(1.0 - saturate(dot(-V, Nw)), 4.0);
                    float3 refl = NightHorizon(R) * fres;
                    refl += _NightMoonCol.rgb * pow(saturate(dot(R, L)), 300.0) * 6.0;
                    refl += _FendaLight.rgb * pow(saturate(dot(R, F)), 60.0) * 0.9;
                    refl += _ImpactLight.rgb * atten * 0.6;
                    lit = lerp(lit, refl, water * 0.9);
                }

                // onda de choque: anel que corre pelo chão (borda irregular) + a segunda, mais fraca
                float ang = atan2(P.x - _ImpactPosW.x, P.z - _ImpactPosW.z);
                float jag = tex2D(_Noise, float2(ang * 1.7, _Shock.x * 0.0004)).r - 0.5;
                float rr = dI2 + jag * (20.0 + _Shock.x * 0.08);
                float w1 = 14.0 + _Shock.x * 0.045;
                float ring1 = exp(-pow((rr - _Shock.x) / w1, 2.0)) * _Shock.y;
                float w2 = 10.0 + _Shock.z * 0.04;
                float ring2 = exp(-pow((rr - _Shock.z) / w2, 2.0)) * _Shock.w;
                float behind = smoothstep(_Shock.x, _Shock.x * 0.55, rr) * _Shock.y;   // o chão logo atrás da frente ainda brilha
                float3 hot = lerp(float3(1.6, 1.25, 0.8), float3(1.0, 0.45, 0.18), saturate(_Shock.x / 1600.0));
                lit += hot * (ring1 * 3.0 + ring2 * 1.6) * (0.55 + c1 * 0.9);
                lit += float3(1.0, 0.5, 0.2) * behind * 0.25 * c2;

                // perspectiva aérea
                float fogT = NightFog(_WorldSpaceCameraPos, P, dist);
                float3 haze = NightHaze(V);
                return float4(lerp(lit, haze, fogT), 1);
            }
            ENDCG
        }
    }
}
