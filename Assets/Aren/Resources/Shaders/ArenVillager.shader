// Aldeões de Campanula e os Ecos que nascem deles. Um material só faz as fases:
//   1. VIVO      — roupa/pele com normal map (Lambert, barato), um leve contraluz frio da noite;
//   2. CORRUPÇÃO — _Corrupt 0→1: uma frente sobe dos pés à cabeça (altura local + ruído). Atrás dela
//      a roupa escurece e a PELE vira a do Sussurrante (bege-acinzentada com manchas de ardósia), abrem
//      rachaduras que pulsam com a nebulosa da Fenda (em espaço de tela, como no Aren/Enemy/Corrupted) e
//      a borda roxa aparece; NA frente, uma faixa acesa (violeta → ciano) com veias um pouco à frente;
//   3. TATUAGENS — _Tattoo 0→1: linhas de tinta escura (veias da prancha do Sussurrante: ruído "crista")
//      nascem no peito e se espalham pelo corpo; a frente que avança brilha violeta, atrás ela assenta e
//      pulsa fraco. Ficam presas ao corpo (coordenadas da pose de bind, gravadas na malha pelo VillagerSetup);
//   4. CRÂNIO — _Skull 0→1: no rosto a pele descasca em placas e o osso aparece (órbitas fundas com um
//      ponto aceso, cavidade do nariz, dentes, maçãs afundadas); os olhos apagam e acendem violeta, o cabelo
//      cai em tufos. Usa a posição do rosto gravada na malha (relativa ao meio dos olhos) — exata em cada
//      pixel porque a cabeça é rígida;
//   5. DESINTEGRAÇÃO — _Dissolve 0→1: o corpo some em flocos (da cabeça para os pés, ou dos pés para a
//      cabeça com _DissolveUp = 1, quando outra forma nasce no lugar), a borda queima antes de virar cinza.
// Também aceita os parâmetros que o EnemyBase usa nos Ecos (_Flash, _Telegraph, _Dissolve).
// Dados na malha (VillagerSetup.BakeBodyData): UV2 = posição na pose de bind (m, raiz do modelo) + meia
// distância entre os olhos; UV3 = posição relativa ao meio dos olhos (x direita, y cima, z frente) + peso da
// cabeça. Malhas sem esses canais simplesmente não ganham tatuagem nem crânio.
Shader "Aren/Villager"
{
    Properties
    {
        _MainTex ("Albedo", 2D) = "white" {}
        _Color ("Cor", Color) = (1,1,1,1)
        [Normal] _BumpMap ("Normal", 2D) = "bump" {}
        _Corrupt ("Corrupção (0 vivo, 1 Eco)", Range(0,1)) = 0
        _CorruptHeight ("Altura do corpo (m)", Float) = 1.85
        _Darkness ("Escurecer quando corrompido", Range(0,1)) = 0.8
        _Cracks ("Rachaduras", 2D) = "black" {}
        _CrackTiling ("Escala das rachaduras", Float) = 2.5
        [HDR] _CrackColor ("Cor das rachaduras", Color) = (2.2,0.25,0.6,1)
        [HDR] _FrontColor ("Frente da corrupção", Color) = (1.6,0.5,3.2,1)
        [HDR] _RimColor ("Cor da borda", Color) = (0.6,0.15,0.9,1)
        _RimPower ("Borda", Range(0.5,8)) = 2.5
        _Flash ("Flash de dano", Range(0,1)) = 0
        _Telegraph ("Aviso de golpe", Range(0,1)) = 0
        _Dissolve ("Dissolver", Range(0,1)) = 0
        _DissolveUp ("Dissolver dos pés para cima", Range(0,1)) = 0
        _DissolveEdge ("Largura da borda do dissolve", Range(0.01,0.2)) = 0.07
        _DissolveHeight ("Altura local do dissolve", Float) = 2.0
        [HDR] _BurnColor ("Brasa da desintegração", Color) = (3.0,1.1,0.35,1)
        _Noise ("Ruído", 2D) = "gray" {}
        _Void ("Nebulosa da Fenda", 2D) = "black" {}
        [HDR] _VoidColor ("Tom da nebulosa", Color) = (1.1,0.55,1.3,1)
        _VoidScale ("Escala da nebulosa", Float) = 1.3
        _Seed ("Semente", Float) = 0
        _AlphaClip ("Recorte por alfa (cabelo)", Range(0,1)) = 0
        _SkinPart ("Peça de pele (1) ou roupa (0)", Range(0,1)) = 0
        _EyePart ("Olhos (1)", Range(0,1)) = 0
        _Tattoo ("Tatuagens", Range(0,1)) = 0
        _TattooOrigin ("Origem das tatuagens (pose de bind, m)", Vector) = (0,1.3,0.08,0)
        [HDR] _TattooColor ("Brilho das tatuagens", Color) = (1.5,0.42,3.2,1)
        _Skull ("Crânio", Range(0,1)) = 0
        [HDR] _EyeGlow ("Olhos acesos", Color) = (2.6,0.7,4.2,1)
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Faces", Float) = 2
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 200
        Cull [_Cull]
        CGPROGRAM
        #pragma surface surf Lambert addshadow fullforwardshadows vertex:vert
        #pragma target 3.5
        sampler2D _MainTex, _BumpMap, _Cracks, _Noise, _Void;
        float4 _Color, _CrackColor, _FrontColor, _RimColor, _VoidColor, _BurnColor, _TattooColor, _EyeGlow, _TattooOrigin;
        float _Corrupt, _CorruptHeight, _Darkness, _CrackTiling, _RimPower, _Flash, _Telegraph;
        float _Dissolve, _DissolveUp, _DissolveEdge, _DissolveHeight, _VoidScale, _Seed, _AlphaClip;
        float _SkinPart, _EyePart, _Tattoo, _Skull;
        struct Input
        {
            float2 uv_MainTex; float3 worldPos; float3 viewDir; float4 screenPos; float3 worldNormal;
            float4 bodyP;   // pose de bind (m) + meia distância dos olhos
            float4 faceP;   // relativo ao meio dos olhos + peso da cabeça
            INTERNAL_DATA
        };

        void vert (inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            o.bodyP = v.texcoord2;
            o.faceP = v.texcoord3;
        }

        // ruído "crista": 1 no meio de cada faixa do ruído — linhas finas e orgânicas (veias da prancha)
        float Ridge(float n) { return 1 - abs(2 * n - 1); }
        float Tri(float3 p, float3 w)
        {
            return (tex2D(_Noise, p.xy).r * w.z + tex2D(_Noise, p.zy).r * w.x + tex2D(_Noise, p.xz).r * w.y) / (w.x + w.y + w.z);
        }

        void surf (Input IN, inout SurfaceOutput o)
        {
            float3 wp = IN.worldPos;
            // altura a partir dos pés no MUNDO (o FBX vem girado do Blender: o eixo local não é o "para cima")
            float3 origin = mul(unity_ObjectToWorld, float4(0, 0, 0, 1)).xyz;
            float3 lp = wp - origin;
            float nz = tex2D(_Noise, lp.xz * 1.7 + lp.y * 0.6 + _Seed).r;
            float nz2 = tex2D(_Noise, float2(lp.x + lp.z, lp.y) * 3.1 + _Seed * 1.7).g;
            float3 bp = IN.bodyP.xyz;
            float hasBody = step(0.001, abs(bp.y));
            float headW = saturate(IN.faceP.w);

            // 5. desintegração: da cabeça para os pés (ou dos pés para a cabeça), em flocos
            float h01 = saturate(lp.y / max(_DissolveHeight, 0.01));
            float disDown = ((1 - h01) * 0.62 + nz * 0.38) - _Dissolve * 1.08 + 0.04;
            // dos pés para cima: frente fina, a MESMA fórmula do _Spawn do Sussurrante — com a mesma altura e o
            // mesmo valor, o aldeão some exatamente onde o Sussurrante se forma (uma forma vira a outra)
            float disUp = (h01 + (nz - 0.5) * 0.16) - (_Dissolve * 1.18 - 0.09);
            float dis = lerp(disDown, disUp, step(0.5, _DissolveUp));
            clip(dis);
            float burn = 1 - smoothstep(0, _DissolveEdge, dis);

            fixed4 albedo = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            clip(albedo.a - 0.45 * _AlphaClip - 0.001 * (1 - _AlphaClip) + (1 - _AlphaClip));   // mechas do cabelo
            // o cabelo cai em tufos quando o crânio aparece
            float tuft = tex2D(_Noise, bp.xz * 9 + bp.y * 4 + _Seed).r;
            clip(tuft + 0.02 - _Skull * _AlphaClip * 1.05);
            o.Normal = UnpackNormal(tex2D(_BumpMap, IN.uv_MainTex));

            // 2. corrupção: frente subindo dos pés
            float hC = saturate(lp.y / max(_CorruptHeight, 0.01));
            float front = _Corrupt * 1.25 - (hC + (nz - 0.5) * 0.22);   // > 0 = já corrompido
            float taken = smoothstep(0.0, 0.06, front) * step(0.001, _Corrupt);
            float band = exp(-pow(front / 0.045, 2)) * step(0.001, _Corrupt) * step(_Corrupt, 0.999);
            float veinField = abs(frac(nz2 * 3.0 + lp.y * 1.3) - 0.5);
            float veins = smoothstep(0.06, 0.0, veinField) * saturate(1 - abs(front + 0.12) / 0.16) * step(0.001, _Corrupt);

            float3 wn = normalize(WorldNormalVector(IN, o.Normal));
            float3 n = abs(wn);
            float c1 = tex2D(_Cracks, wp.xy * _CrackTiling * 0.5).r;
            float c2 = tex2D(_Cracks, wp.zy * _CrackTiling * 0.5).r;
            float c3 = tex2D(_Cracks, wp.xz * _CrackTiling * 0.5).r;
            float crack = (c1 * n.z + c2 * n.x + c3 * n.y) / (n.x + n.y + n.z);
            float pulse = 0.55 + 0.45 * sin(_Time.y * 3.1 + wp.y * 4.0 + _Seed);
            float crackLine = smoothstep(0.55, 0.92, crack) * smoothstep(0.38, 0.55, nz2);

            // pele tomada = a pele do Sussurrante (bege-acinzentada, manchas de ardósia); a roupa só escurece
            float patch = smoothstep(0.44, 0.555, Tri(bp * 2.2 + _Seed, n));
            float3 sussSkin = lerp(float3(0.50, 0.47, 0.43), float3(0.20, 0.22, 0.27), patch);
            float3 aliveAlb = albedo.rgb;
            float lum = dot(albedo.rgb, float3(0.3, 0.59, 0.11));
            float3 cloth = lerp(albedo.rgb, float3(0.16, 0.16, 0.23) * (0.6 + lum), 0.55) * (1 - _Darkness * 0.85);
            float3 takenAlb = lerp(cloth, sussSkin * (0.75 + 0.25 * albedo.rgb), _SkinPart * hasBody);
            float3 alb = lerp(aliveAlb, takenAlb, taken);

            // 3. tatuagens: nascem no peito (_TattooOrigin) e se espalham com a frente acesa
            float tatK = 0, tatEdge = 0;
            if (_Tattoo > 0.001)
            {
                float3 q = bp + float3(0.37, 0.11, 0.53) * _Seed;
                float warp = Tri(q * 1.9, n);
                float l1 = smoothstep(0.90, 0.985, Ridge(Tri(q * 3.4 + warp * 0.35, n)));
                float l2 = smoothstep(0.93, 0.99, Ridge(Tri(q * 7.5 + warp * 0.6 + 0.37, n))) * step(0.45, Tri(q * 1.3 + 0.7, n));
                float lines = max(l1, l2 * 0.8);
                float dist = length(bp - _TattooOrigin.xyz) + (warp - 0.5) * 0.22;
                float reach = _Tattoo * 1.75;
                float shown = smoothstep(reach + 0.04, reach - 0.06, dist) * hasBody;
                tatEdge = exp(-pow((dist - reach) / 0.07, 2)) * hasBody * step(_Tattoo, 0.999);
                float skin = lerp(0.35, 1.0, _SkinPart);   // por baixo da roupa só o brilho atravessa, fraco
                tatK = lines * shown * skin;
                alb = lerp(alb, alb * 0.12 + float3(0.02, 0.0, 0.04), lines * shown * _SkinPart);
                tatEdge *= lines * skin + 0.25 * shown * skin;
            }

            // 4. crânio no rosto
            float boneK = 0, darkK = 0, eyeK = 0;
            if (_Skull > 0.001 && headW > 0.3)
            {
                float3 f = IN.faceP.xyz;
                float sep = max(IN.bodyP.w, 0.02);
                float frontK = smoothstep(-0.075, -0.025, f.z) * headW;
                float peel = Tri(bp * 26 + _Seed, n);
                float reveal = smoothstep(peel - 0.12, peel + 0.12, _Skull * 1.3 - 0.05);
                float2 eo = float2(abs(f.x) - sep, f.y + 0.004);
                float er = length(eo / float2(0.023, 0.019));
                float socket = 1 - smoothstep(0.55, 1.05, er);
                float pupil = 1 - smoothstep(0.08, 0.26, er);
                float nose = 1 - smoothstep(0.7, 1.0, length(float2(f.x / 0.009, (f.y + 0.042) / 0.015)));
                float cheek = 1 - smoothstep(0.5, 1.0, length(float2((abs(f.x) - 0.04) / 0.017, (f.y + 0.045) / 0.022)));
                float teethBand = step(abs(f.y + 0.071), 0.009) * step(abs(f.x), 0.027);
                float gaps = smoothstep(0.55, 0.85, abs(frac(f.x / 0.0072 + 0.5) - 0.5) * 2) + smoothstep(0.0, 0.0025, -abs(f.y + 0.071) + 0.0012);
                float brow = exp(-pow((f.y - 0.022) / 0.007, 2)) * step(abs(f.x), sep + 0.022);
                boneK = reveal * lerp(0.55, 1.0, frontK) * headW;
                darkK = saturate(socket * 1.1 + nose * 0.9 + cheek * 0.35 + teethBand * saturate(gaps) * 0.8) * frontK * reveal;
                eyeK = pupil * frontK * smoothstep(0.45, 0.85, _Skull);
                float grime = Tri(bp * 40 + 1.3, n);
                float3 bone = float3(0.80, 0.76, 0.67) * (0.72 + 0.35 * grime) * (0.85 + 0.15 * crack) + brow * 0.06;
                float3 cavity = lerp(float3(0.10, 0.07, 0.06), float3(0.012, 0.008, 0.02), socket);
                alb = lerp(alb, bone, boneK);
                alb = lerp(alb, cavity, darkK);
            }
            // os olhos (malha própria) apagam e acendem violeta
            if (_EyePart > 0.5) { alb = lerp(alb, float3(0.01, 0.0, 0.02), smoothstep(0.3, 0.7, _Skull)); eyeK = max(eyeK, smoothstep(0.3, 0.6, _Skull)); }

            tatK *= 1 - boneK; tatEdge *= 1 - boneK * 0.7;
            o.Albedo = alb;
            float rim = pow(1 - saturate(dot(normalize(IN.viewDir), o.Normal)), _RimPower);
            float2 suv = IN.screenPos.xy / max(IN.screenPos.w, 1e-4);
            suv.x *= _ScreenParams.x / _ScreenParams.y;
            float3 vcol = tex2D(_Void, suv * _VoidScale + float2(_Time.x * 0.15, _Time.x * 0.05)).rgb * _VoidColor.rgb;

            float3 alive = rim * float3(0.05, 0.06, 0.11);                      // contraluz frio (noite)
            float3 corr = (crackLine * _CrackColor.rgb * pulse * (0.7 + _Telegraph * 1.6)
                        + vcol * (0.04 + crackLine * 0.6) * (1 + _Telegraph * 0.6)) * (1 - boneK * 0.85)
                        + rim * _RimColor.rgb * (0.7 + _Telegraph * 2);
            float tpulse = 0.35 + 0.65 * (0.5 + 0.5 * sin(_Time.y * 2.3 + bp.y * 6.0 + _Seed));
            float3 em = lerp(alive, corr, taken)
                      + band * (_FrontColor.rgb * (0.3 + 0.4 * nz2) + vcol * 0.8)
                      + veins * _FrontColor.rgb * 0.9
                      + tatK * _TattooColor.rgb * (0.05 + 0.22 * tpulse) * (1 + _Telegraph * 2.5)
                      + tatEdge * _TattooColor.rgb * 1.8
                      + eyeK * _EyeGlow.rgb * (0.75 + 0.25 * sin(_Time.y * 7 + _Seed))
                      + _Flash * float3(1.3, 1.15, 1.25)
                      + burn * lerp(_BurnColor.rgb, _CrackColor.rgb * 1.5 + vcol * 2, saturate(_Corrupt)) * 2.2;
            o.Emission = em;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
