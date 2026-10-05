// Céu da noite da Ruptura (abertura e começo do jogo). Gradiente noturno, lua com halo, estrelas,
// nuvens com borda prateada pela lua e — MUITO longe, a nor-nordeste, atrás das serras da
// paisagem (FarLands) — a Fenda do Contracanto:
//   · um rasgo de luz vertical que VIBRA como uma corda (soma de modos estacionários);
//   · em volta, o próprio céu se estilhaça como vidro: fragmentos escuros com aro de luz, rachaduras
//     luminosas entre eles, e onde os cacos faltam aparece o "outro lado" (nebulosa violeta funda);
//   · cordas de luz finas em volta do rasgo, cada uma num modo harmônico diferente (n = 2..7);
//   · cacos soltos flutuando para fora, feixe fino acima, brilho no ar e nas nuvens;
//   · nuvens puxadas num redemoinho lento para ela e ondas de pulso atravessando o céu.
// A abertura: um ponto de luz → a rachadura corre para cima e para baixo → o céu estilhaça → respira.
// Também: o clarão do impacto no horizonte e as nuvens iluminadas por baixo (impacto, onda de choque
// e a nota que cai, que atravessa a camada de nuvens).
Shader "Hidden/Aren/NightSky"
{
    Properties
    {
        _Zenith ("Zênite", Color) = (0.010,0.012,0.028,1)
        _Mid ("Meio", Color) = (0.028,0.033,0.065,1)
        _Horizon ("Horizonte", Color) = (0.085,0.085,0.135,1)
        _Ground ("Abaixo do horizonte", Color) = (0.03,0.032,0.05,1)
        _MoonDir ("Direção da lua", Vector) = (-0.61,0.5,0.61,0)
        [HDR] _MoonColor ("Lua", Color) = (1.5,1.6,1.9,1)
        _MoonRadius ("Raio da lua (rad)", Float) = 0.024
        _Stars ("Estrelas", 2D) = "black" {}
        _StarBoost ("Brilho das estrelas", Float) = 1.3
        _Clouds ("Nuvens (ruído RGBA)", 2D) = "gray" {}
        _CloudLit ("Nuvem iluminada", Color) = (0.24,0.25,0.31,1)
        _CloudDark ("Nuvem escura", Color) = (0.03,0.034,0.055,1)
        _Space ("Outro lado (nebulosa)", 2D) = "black" {}
        _FendaAz ("Azimute da Fenda (rad)", Float) = 0.30
        _FendaOpen ("Abertura da Fenda", Range(0,1)) = 0
        _FendaPulse ("Pulso da Fenda", Range(0,1)) = 0
        _FendaInhale ("Contração (antes dos sete)", Range(0,1)) = 0
        _FendaBurst ("Lampejo (saída de um brilho)", Range(0,1)) = 0
        _FendaWave ("Onda de pulso (fase 0..1)", Range(0,1)) = 0
        [HDR] _FendaColor ("Luz da Fenda", Color) = (0.46,0.33,1.15,1)
        [HDR] _FendaCore ("Núcleo da Fenda", Color) = (1.35,1.4,1.9,1)
        _Flash ("Clarão do impacto", Range(0,1)) = 0
        [HDR] _FlashColor ("Cor do clarão", Color) = (2.4,1.7,0.9,1)
        _Noise ("Ruído", 2D) = "gray" {}
        _GlowPosW ("Nota que cai (mundo)", Vector) = (0,0,0,0)
        _GlowLight ("Luz da nota", Color) = (0,0,0,0)
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
            #include "NightCommon.cginc"
            float4 _Zenith, _Mid, _Horizon, _Ground, _MoonDir, _MoonColor, _CloudLit, _CloudDark, _FendaColor, _FendaCore, _FlashColor, _GlowPosW, _GlowLight;
            float _MoonRadius, _StarBoost, _FendaAz, _FendaOpen, _FendaPulse, _FendaInhale, _FendaBurst, _FendaWave, _Flash;
            sampler2D _Stars, _Clouds, _Noise, _Space;
            struct v2f { float4 pos : SV_POSITION; float3 dir : TEXCOORD0; };
            v2f vert (appdata_base v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.dir = v.vertex.xyz; return o; }

            float2 Hash2(float2 p)
            {
                p = float2(dot(p, float2(127.1, 311.7)), dot(p, float2(269.5, 183.3)));
                return frac(sin(p) * 43758.5453);
            }

            // Voronoi: x = distância ao centro, y = distância à borda, zw = id da célula; rel = pixel → centro
            float4 Voronoi(float2 p, out float2 rel)
            {
                float2 ip = floor(p), fp = frac(p);
                float d1 = 8, d2 = 8; float2 id = 0, mr = 0;
                for (int j = -1; j <= 1; j++)
                for (int i = -1; i <= 1; i++)
                {
                    float2 g = float2(i, j);
                    float2 o = Hash2(ip + g) * 0.85 + 0.075;
                    float2 r = g + o - fp;
                    float d = dot(r, r);
                    if (d < d1) { d2 = d1; d1 = d; id = ip + g; mr = r; }
                    else if (d < d2) d2 = d;
                }
                rel = mr;
                return float4(sqrt(d1), sqrt(d2) - sqrt(d1), id);
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float y = d.y;
                float az = atan2(d.x, d.z);
                float cosEl = sqrt(saturate(1 - y * y));
                float t = _Time.y;

                // ---------------------------------------------------------------- Fenda: coordenadas
                float fa = NightWrapPi(az - _FendaAz);
                float u = fa * cosEl;                    // ângulo horizontal a partir da Fenda
                const float VC = 0.14;                   // altura (seno da elevação) do centro do rasgo
                float o = _FendaOpen;
                float crack = smoothstep(0.0, 0.5, o);   // comprimento do rasgo
                float burst = smoothstep(0.38, 0.85, o); // o céu estilhaçando
                float halfH = 0.008 + 0.235 * crack;
                float vb = VC - halfH * 0.62, vt = VC + halfH * 1.3;
                float adR = length(float2(u, (y - VC) * 0.8));   // distância angular ao centro do rasgo

                // onda de pulso: anel que sai da Fenda e atravessa o céu (fase dada pela abertura)
                float waveR = _FendaWave * 1.4;
                float wave = _FendaWave > 0.001 ? exp(-pow((adR - waveR) / (0.025 + 0.05 * _FendaWave), 2.0)) * (1 - _FendaWave) * (1 - _FendaWave) : 0;

                // ---------------------------------------------------------------- base
                float3 col = lerp(_Horizon.rgb, _Mid.rgb, smoothstep(0.0, 0.25, y));
                col = lerp(col, _Zenith.rgb, smoothstep(0.2, 0.85, y));
                col += _Horizon.rgb * 0.35 * exp(-abs(y) / 0.04);
                if (y < 0) col = lerp(col, _Ground.rgb, smoothstep(0.0, -0.08, y));

                // lua com halo largo (a névoa clara em volta dela)
                float3 md3 = normalize(_MoonDir.xyz);
                float md = dot(d, md3);
                col += _MoonColor.rgb * (pow(saturate(md), 900) * 0.4 + pow(saturate(md), 90) * 0.07 + pow(saturate(md), 10) * 0.03);
                float disk = smoothstep(cos(_MoonRadius * 1.06), cos(_MoonRadius), md);
                if (disk > 0)
                {
                    float3 t1 = normalize(cross(md3, float3(0, 1, 0))), t2 = cross(t1, md3);
                    float2 luv = float2(dot(d, t1), dot(d, t2)) / _MoonRadius;
                    float mare = tex2D(_Clouds, luv * 0.22 + 0.3).g;
                    float limb = sqrt(saturate(1 - dot(luv, luv)));
                    col = lerp(col, _MoonColor.rgb * (0.7 + 0.3 * mare) * (0.75 + 0.25 * limb), disk);
                }

                // estrelas (a onda de pulso as desloca um pouco — o ar refrata)
                if (y > 0)
                {
                    float2 suv = d.xz / (1 + y) * 2.2;
                    suv += wave * 0.004 * normalize(float2(u, y - VC) + 1e-4);
                    float3 st = tex2D(_Stars, suv).rgb;
                    st = saturate((st - 0.18) * 1.6);
                    float tw = 0.6 + 0.4 * sin(t * 2.3 + suv.x * 37 + suv.y * 29);
                    col += st * st * tw * _StarBoost * smoothstep(0.03, 0.25, y) * (1 - disk) * (1 + wave * 2);
                }

                // ---------------------------------------------------------------- nuvens
                if (y > 0.012)
                {
                    float2 uv = d.xz / (y + 0.28) * 0.11;
                    // redemoinho lento puxado pela Fenda
                    float3 Fd = float3(sin(_FendaAz), VC, cos(_FendaAz));
                    float2 fuv = Fd.xz / (VC + 0.28) * 0.11;
                    float2 rel = uv - fuv;
                    float pull = o * exp(-adR / 0.32);
                    float ang = pull * (0.9 + 0.25 * sin(t * 0.2));
                    float cs = cos(ang), sn = sin(ang);
                    rel = float2(rel.x * cs - rel.y * sn, rel.x * sn + rel.y * cs) * (1 - 0.18 * pull);
                    uv = fuv + rel;
                    float2 wind = float2(t * 0.0022, t * 0.0007);
                    float n1 = tex2D(_Clouds, uv + wind).r;
                    float n2 = tex2D(_Clouds, uv * 3.1 - wind * 1.6 + 0.37).g;
                    float dens = n1 * 0.8 + n2 * 0.35;
                    float c = smoothstep(0.52, 0.82, dens);
                    c *= smoothstep(0.02, 0.16, y) * (1 - smoothstep(0.55, 0.95, y));
                    // borda prateada: amostra deslocada para o lado da lua
                    float2 mdir = normalize(md3.xz + 1e-4) * 0.012;
                    float n1m = tex2D(_Clouds, uv + wind + mdir).r * 0.8 + n2 * 0.35;
                    float rimL = saturate((dens - n1m) * 6.0);
                    float moonNear = pow(saturate(md * 0.5 + 0.5), 5);
                    float3 cc = lerp(_CloudDark.rgb, _CloudLit.rgb, saturate(moonNear * 0.7 + rimL * 0.6));
                    cc += _MoonColor.rgb * rimL * pow(saturate(md), 6) * 0.25;
                    // luz da Fenda nas nuvens perto dela
                    cc += _FendaColor.rgb * 0.28 * o * exp(-adR / 0.25) * (0.7 + 0.6 * _FendaPulse);
                    // nuvens iluminadas por baixo: impacto, onda de choque e a nota que atravessa a camada
                    if (y > 0.02)
                    {
                        float3 cp = _WorldSpaceCameraPos + d * ((1700.0 - _WorldSpaceCameraPos.y) / y);
                        float di = length(cp.xz - _ImpactPosW.xz);
                        cc += _ImpactLight.rgb * (1.0 / (1.0 + pow(di / 1400.0, 2.0))) * 0.6;
                        float rc = _Shock.x * 1.25;
                        cc += float3(1.0, 0.6, 0.3) * exp(-pow((di - rc) / (60.0 + rc * 0.08), 2.0)) * _Shock.y * 0.7 * n2;
                        float dg = length(cp - _GlowPosW.xyz);
                        cc += _GlowLight.rgb * (1.0 / (1.0 + pow(dg / 500.0, 2.0)));
                    }
                    cc *= 1 + wave * 0.8;
                    col = lerp(col, cc, c * 0.85);
                }

                // ---------------------------------------------------------------- a Fenda
                if (o > 0.001 && abs(fa) < 0.75 && y > -0.06)
                {
                    float tv = saturate((y - vb) / max(vt - vb, 1e-3));
                    float inside = smoothstep(vb - 0.004, vb + 0.004, y) * smoothstep(vt + 0.004, vt - 0.004, y);
                    float prof = pow(sin(3.14159 * tv), 0.65);
                    // o rasgo: curva lenta + serrilhado + VIBRAÇÃO (modos de corda, cada um no seu tempo)
                    float jag = (tex2D(_Noise, float2(y * 1.3, 0.37)).r - 0.5) * 0.026 + (tex2D(_Noise, float2(y * 7.0, 0.71)).r - 0.5) * 0.006;
                    float vib = 0;
                    [unroll] for (int k = 1; k <= 4; k++)
                        vib += sin(k * 3.14159 * tv) * cos(t * (2.7 * k + 0.45 * k * k) + k * 1.3) / k;
                    vib *= 0.0014 * (0.5 + _FendaPulse) * crack;
                    float ux = u - jag - vib;
                    float aux = abs(ux);

                    // largura da região (losango alongado), encolhe na contração
                    float W = (0.004 + 0.1 * burst) * prof * (1 - 0.4 * _FendaInhale) + 0.002;
                    float rr = aux / W;                                    // 0 no rasgo, 1 na borda

                    // fragmentos: ilhas escuras (cada célula de Voronoi encolhida), pequenas junto ao rasgo
                    // e maiores longe (espaço logarítmico), separadas pela luz de dentro
                    float rho = aux + 0.0016;
                    float side = ux >= 0 ? 1.0 : -1.0;
                    float2 sp = float2(log(rho / 0.0016) * 1.6, (y - VC) / (rho + 0.018) * 1.25);
                    sp.x -= burst * 0.35 * saturate(rr);                   // os de fora se afastam (explosão congelada)
                    float2 rel;
                    float4 vo = Voronoi(sp + float2(side * 17.3, 0) + float2(0, t * 0.015 * side), rel);
                    float2 h = Hash2(vo.zw + side * 3.1);
                    float edge = vo.y;
                    float zone = inside * burst * (1 - smoothstep(1.4, 2.0, rr));
                    float has = step(h.x, 0.25 + 0.55 * smoothstep(0.1, 0.9, rr) - 0.45 * smoothstep(1.2, 2.2, rr));
                    float margin = 0.07 + 0.22 * h.y;                       // tamanho do caco (borda de luz entre eles)
                    float frag = smoothstep(margin, margin + 0.02, edge) * has * zone;
                    // aro: a borda do caco virada para o rasgo pega a luz
                    float facing = saturate(rel.x * 3.0 + 0.2);             // centro da célula mais longe que o pixel = lado de dentro
                    float rim = (smoothstep(margin, margin + 0.02, edge) - smoothstep(margin + 0.02, margin + 0.07, edge)) * has * zone * facing;

                    // a luz de dentro: volume em losango, mais claro junto ao rasgo, raios saindo do centro
                    float2 vuv = float2(ux * 3.0, (y - VC) * 2.0);
                    float va = t * 0.03;
                    vuv = float2(vuv.x * cos(va) - vuv.y * sin(va), vuv.x * sin(va) + vuv.y * cos(va));
                    float neb = dot(tex2D(_Space, vuv + 0.5).rgb, float3(0.4, 0.3, 0.5));
                    float pulse = 0.82 + 0.18 * _FendaPulse + 0.05 * sin(t * 1.7) + 0.6 * _FendaBurst;
                    float3 F = _FendaColor.rgb * pulse;
                    float ang = atan2(y - VC, ux);
                    float rays = tex2D(_Noise, float2(ang * 3.1, t * 0.01)).r;
                    rays = smoothstep(0.5, 0.9, rays) * exp(-adR / 0.13) * burst;
                    float body = exp(-rr * 2.2) * (1 - smoothstep(0.9, 1.8, rr)) * inside * burst;
                    float3 inner = F * (body * (0.5 + 0.8 * neb) + rays * 0.28) + _FendaCore.rgb * exp(-rr * 10.0) * 0.35 * inside * burst;

                    // núcleo e brilhos
                    float coreW = (0.0011 + 0.0024 * prof) * (0.55 + 0.45 * crack) * (1 + 0.7 * _FendaInhale + 1.6 * _FendaBurst);
                    float core = exp(-ux * ux / (coreW * coreW)) * inside;
                    float pinD = length(float2(u, y - VC));
                    float pinK = (1 - crack) * smoothstep(0.0, 0.04, o);
                    float pin = (exp(-pinD * pinD / 0.000003) * 2.5 + exp(-pinD / 0.008) * 0.2) * pinK;   // o primeiro ponto de luz
                    float glow = exp(-aux / (0.005 + 0.03 * prof)) * smoothstep(vt + 0.1, vt, y) * smoothstep(vb - 0.08, vb, y);
                    float aura = exp(-adR / 0.12) * o;
                    float beamUp = exp(-ux * ux / 0.0000004) * step(vt, y) * exp(-(y - vt) / 0.3) * burst;
                    float beamDn = exp(-ux * ux / 0.0000006) * step(y, vb) * exp(-(vb - y) / 0.05) * burst;

                    // cacos: quase pretos contra a luz, com um pouco de violeta refletido e estrelas tortas
                    float2 suv2 = d.xz / (1 + max(y, 0.0)) * 2.2 + (h - 0.5) * 0.03;
                    float3 st2 = saturate((tex2D(_Stars, suv2).rgb - 0.2) * 1.6);
                    float shade = 0.5 + 0.5 * saturate(-rel.y * 2.0 + 0.5);   // face de cima/baixo
                    float3 fragCol = col * 0.12 + st2 * st2 * 0.25 + F * 0.035 * shade * (1 - saturate(rr * 0.6));

                    // cordas de luz em volta: cada uma num modo harmônico (2, 3, 5, 7), vibrando
                    float strings = 0;
                    [unroll] for (int s = 0; s < 4; s++)
                    {
                        float sideS = (s % 2 == 0) ? 1 : -1;
                        float n = s == 0 ? 2 : s == 1 ? 3 : s == 2 ? 5 : 7;
                        float off = sideS * (0.02 + 0.02 * s) * burst * (1 - 0.3 * _FendaInhale);
                        float disp = sin(n * 3.14159 * tv) * cos(t * (2.1 + 0.9 * s) + s) * 0.004 * (0.6 + _FendaPulse);
                        float dx = ux - off * prof - disp;
                        strings += exp(-dx * dx / 0.0000003) * abs(sin(n * 3.14159 * tv)) * inside;
                    }

                    // compõe: luz de dentro, cacos por cima, aros, cordas, núcleo
                    col += inner + F * (glow * 0.25 + aura * 0.05);
                    col += F * strings * 0.25 * burst;
                    col = lerp(col, fragCol, frag);
                    col += lerp(F, _FendaCore.rgb, 0.4) * rim * (1.2 - saturate(rr * 0.5));
                    col += _FendaCore.rgb * (core * pulse * 1.3 + pin + beamUp * 0.6 + beamDn * 0.4);
                    col += F * wave * 0.12;
                    // névoa em volta (a Fenda tinge o ar, pouco: não pinta o céu inteiro)
                    col += _FendaColor.rgb * 0.03 * exp(-abs(fa) / 0.3) * smoothstep(0.8, 0.0, abs(y - VC)) * o;
                }

                // ---------------------------------------------------------------- clarão do impacto
                float3 toI = _ImpactPosW.xyz - _WorldSpaceCameraPos;
                float3 di3 = normalize(toI);
                float ci = dot(d, di3);
                float above = y - di3.y;
                float dome = pow(saturate(ci), 60.0) * exp(-max(0.0, above) / 0.08) * _Flash;
                col += _FlashColor.rgb * (dome + pow(saturate(ci), 8.0) * 0.1 * _Flash);
                col += _ImpactLight.rgb * pow(saturate(ci), 12.0) * 0.25;
                return float4(col, 1);
            }
            ENDCG
        }
    }
}
