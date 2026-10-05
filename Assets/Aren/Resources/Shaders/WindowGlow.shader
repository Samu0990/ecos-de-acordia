// Janelas acesas da vila à noite (VillageLights): quad colado na fachada com caixilho escuro em cruz
// e, atrás do vidro, um QUARTO de verdade em perspectiva ("interior mapping": o raio da câmera entra
// numa caixa atrás da parede e acerta fundo, laterais, chão ou teto — dá profundidade quando a câmera
// anda, sem geometria). Uma lamparina no fundo ilumina o quarto (mais claro perto dela), cortinas nas
// bordas deixam passar a luz, cada casa tem um tom e uma tremulação de chama. De perto o brilho cai
// um pouco (não vira um retângulo amarelo chapado). Emissão em HDR; névoa do Unity.
Shader "Hidden/Aren/WindowGlow"
{
    Properties { _Color ("Luz (a = intensidade)", Color) = (1,0.62,0.3,1.6) }
    SubShader
    {
        Tags { "Queue"="Geometry+20" "RenderType"="Opaque" "DisableBatching"="True" }
        Offset -1, -1
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma target 3.0
            #include "UnityCG.cginc"
            UNITY_INSTANCING_BUFFER_START(P)
                UNITY_DEFINE_INSTANCED_PROP(float4, _Color)
                UNITY_DEFINE_INSTANCED_PROP(float4, _Shape)   // janela gótica: x = nascente do arco (fração da altura, 0 = retangular), y = largura/altura, z = raio (× largura)
            UNITY_INSTANCING_BUFFER_END(P)
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float seed : TEXCOORD1; float3 view : TEXCOORD2; float dist : TEXCOORD3; UNITY_FOG_COORDS(4) UNITY_VERTEX_INPUT_INSTANCE_ID };
            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                float3 c = mul(unity_ObjectToWorld, float4(0, 0, 0, 1)).xyz;
                o.seed = frac(sin(dot(c.xz, float2(12.9898, 78.233))) * 43758.5453);
                o.view = ObjSpaceViewDir(v.vertex);   // do vértice para a câmera, no espaço do quad (o vidro é o plano z = 0, o quarto fica em +z)
                o.dist = length(_WorldSpaceCameraPos - mul(unity_ObjectToWorld, v.vertex).xyz);
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            float Hash(float n) { return frac(sin(n) * 43758.5453); }
            float2 Hash2(float2 p) { return frac(sin(float2(dot(p, float2(127.1, 311.7)), dot(p, float2(269.5, 183.3)))) * 43758.5453); }

            // vitral: células de Voronoi (cada uma um vidro de cor) com o chumbo escuro nas bordas, aceso por
            // trás pela luz da igreja (tremula como vela). Na rosácea o padrão é dobrado em 12 pétalas em
            // volta do centro (simetria de caleidoscópio) com anéis de pedra/chumbo.
            fixed4 Stained(v2f i, float4 c, float4 shp, float2 uv)
            {
                float2 sp;
                float ring = 0;
                if (shp.w > 2.5)
                {
                    float2 d = uv - 0.5;
                    float r = length(d) * 2;
                    clip(1 - r);
                    float a = atan2(d.y, d.x);
                    float seg = 6.2831853 / 12.0;
                    float af = abs(frac(a / seg + 0.5) - 0.5) * seg;
                    sp = float2(r * 6.0, af * r * 7.0);
                    ring = (1 - smoothstep(0.012, 0.03, abs(r - 0.36))) + (1 - smoothstep(0.012, 0.03, abs(r - 0.72))) + (1 - smoothstep(0.02, 0.05, abs(r - 0.985)));
                    ring += (1 - smoothstep(0.0, 0.02, abs(af * r))) * step(0.36, r);   // os raios entre as pétalas
                }
                else
                {
                    float asp = shp.y, hw = asp * 0.5, R = shp.z * asp;
                    float2 q = float2((uv.x - 0.5) * asp, uv.y);
                    float inArch = shp.x <= 0.001 || q.y <= shp.x ? 1.0 : step(length(q - float2(-hw + R, shp.x)), R) * step(length(q - float2(hw - R, shp.x)), R);
                    clip(inArch - 0.5);
                    sp = float2(uv.x * asp, uv.y) * 4.5 / max(asp, 0.35);
                }
                float2 ip = floor(sp), fp = frac(sp);
                float d1 = 8, d2 = 8; float2 id1 = 0;
                for (int y = -1; y <= 1; y++)
                    for (int x = -1; x <= 1; x++)
                    {
                        float2 g = float2(x, y);
                        float2 o = Hash2(ip + g + i.seed * 17.0) * 0.8 + 0.1;
                        float2 rr = g + o - fp;
                        float dd = dot(rr, rr);
                        if (dd < d1) { d2 = d1; d1 = dd; id1 = ip + g; } else if (dd < d2) d2 = dd;
                    }
                float edge = sqrt(d2) - sqrt(d1);
                float lead = saturate(1 - smoothstep(0.035, 0.085, edge) + ring);
                float h = Hash(dot(id1, float2(1.7, 9.2)) + i.seed * 31.0);
                float3 pal = h < 0.26 ? float3(0.12, 0.26, 1.0) :      // safira
                             h < 0.46 ? float3(0.95, 0.1, 0.13) :      // rubi
                             h < 0.6  ? float3(1.0, 0.62, 0.1) :       // âmbar
                             h < 0.74 ? float3(0.58, 0.16, 0.95) :     // violeta
                             h < 0.88 ? float3(0.1, 0.72, 0.32) :      // esmeralda
                                        float3(1.0, 0.92, 0.62);       // ouro claro
                float thick = 0.65 + 0.6 * Hash(dot(id1, float2(4.1, 2.3)) + 3.1);
                float flick = 0.9 + 0.1 * sin(_Time.y * (1.7 + i.seed * 2.0) + i.seed * 40.0) * sin(_Time.y * 4.3 + i.seed * 9.0);
                float glow = (1.1 - 0.5 * sqrt(d1)) * thick * flick;
                // a luz de dentro é mais forte no meio da janela
                glow *= 0.75 + 0.5 * (1 - saturate(length(uv - float2(0.5, 0.45)) * 1.4));
                float3 col = pal * glow * c.a * 0.85 * float3(1.0, 0.92, 0.85);
                col = lerp(col, float3(0.025, 0.02, 0.02), lead);
                float near = lerp(0.7, 1.0, saturate((i.dist - 2.0) / 9.0));
                col *= near;
                UNITY_APPLY_FOG(i.fogCoord, col);
                return float4(col, 1);
            }

            fixed4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float4 c = UNITY_ACCESS_INSTANCED_PROP(P, _Color);
                float2 uv = i.uv;
                float4 shp = UNITY_ACCESS_INSTANCED_PROP(P, _Shape);
                float wood;
                // vitral (torres, campanário e rosáceas): w = 2 arco ogival, w = 3 rosácea
                if (shp.w > 1.5) return Stained(i, c, shp, uv);
                if (shp.w > 0.5)
                {
                    clip(0.5 - length(uv - 0.5));   // rosácea: círculo (anel, raios e miolo são da malha)
                    wood = 0;
                }
                else if (shp.x > 0.001)
                {
                    // arco ogival recortado (a moldura de pedra, o mainel e a travessa são da malha da casa)
                    float asp = shp.y, hw = asp * 0.5, R = shp.z * asp;
                    float2 q = float2((uv.x - 0.5) * asp, uv.y);
                    float inArch = q.y <= shp.x ? 1.0 : step(length(q - float2(-hw + R, shp.x)), R) * step(length(q - float2(hw - R, shp.x)), R);
                    clip(inArch - 0.5);
                    wood = 0;
                }
                else
                {
                    // caixilho e travessas
                    float frame = step(uv.x, 0.07) + step(0.93, uv.x) + step(uv.y, 0.06) + step(0.94, uv.y);
                    float mull = step(abs(uv.x - 0.5), 0.028) + step(abs(uv.y - 0.55), 0.025);
                    wood = saturate(frame + mull);
                }

                // o quarto: raio entra pelo vidro e acerta a caixa (largura 1, altura 1, fundo D)
                float D = 0.75 + 0.4 * Hash(i.seed * 7.1);
                float3 r = -normalize(i.view);
                r.z = max(r.z, 0.05);
                float3 p0 = float3(uv, 0);
                float tb = D / r.z;
                float tx = r.x > 0 ? (1 - p0.x) / max(r.x, 1e-4) : -p0.x / min(r.x, -1e-4);
                float ty = r.y > 0 ? (1 - p0.y) / max(r.y, 1e-4) : -p0.y / min(r.y, -1e-4);
                float t = min(tb, min(tx, ty));
                float3 h = p0 + r * t;
                float3 lamp = float3(0.3 + 0.4 * Hash(i.seed * 3.3), 0.32, D * 0.7);
                float ld = length((h - lamp) * float3(1, 1.2, 1));
                float flick = 0.9 + 0.1 * sin(_Time.y * (2.1 + i.seed * 3) + i.seed * 40) * sin(_Time.y * 5.3 + i.seed * 13);
                float light = (0.12 + 0.9 / (1 + ld * ld * 9)) * flick * (1.15 - 0.45 * saturate(h.y));   // mais escuro em cima
                float3 wall = float3(1.0, 0.88, 0.74);                       // reboco (o calor vem da cor da luz)
                float3 surf = wall;
                if (t == ty) surf = r.y > 0 ? float3(0.42, 0.36, 0.32) : float3(0.66, 0.52, 0.4);   // teto (vigas) / chão de tábua
                else if (t == tx) surf = wall * 0.8;
                // um móvel escuro contra a parede do fundo (estante/armário), varia por casa
                float fx = 0.15 + 0.6 * Hash(i.seed * 11.7);
                float furn = (t == tb) * step(abs(h.x - fx), 0.13) * step(h.y, 0.55);
                surf = lerp(surf, float3(0.18, 0.12, 0.08), furn * 0.85);
                float3 room = surf * light * (1 - 0.35 * saturate(t / (D + 0.5)));

                // cortinas nas bordas (tecido aceso por trás: deixa passar a luz, mais escuro e com dobras)
                float side = min(uv.x, 1 - uv.x);
                float cw = 0.14 + 0.12 * Hash(i.seed * 5.9);
                float curtain = 1 - smoothstep(cw - 0.03, cw + 0.03, side);
                float folds = 0.7 + 0.3 * sin(uv.x * 70 + i.seed * 9);
                float3 cloth = float3(0.9, 0.5, 0.4) * folds * (0.45 + 0.35 * light);
                float3 glass = lerp(room, cloth, curtain * 0.9);

                // brilho: um pouco de reflexo frio no vidro; de perto não estoura
                float near = lerp(0.6, 1.0, saturate((i.dist - 2.0) / 9.0));
                float3 col = glass * c.rgb * c.a * near * 0.95 + float3(0.02, 0.025, 0.04);
                col = lerp(col, float3(0.035, 0.028, 0.025), wood);
                UNITY_APPLY_FOG(i.fogCoord, col);
                return float4(col, 1);
            }
            ENDCG
        }
    }
}
