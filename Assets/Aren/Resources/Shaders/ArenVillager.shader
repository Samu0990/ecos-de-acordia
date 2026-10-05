// Aldeões de Campanula e os Ecos que nascem deles. Um material só faz as três fases:
//   1. VIVO      — roupa/pele com normal map (Lambert, barato), um leve contraluz frio da noite;
//   2. CORRUPÇÃO — _Corrupt 0→1: uma frente sobe dos pés à cabeça (altura local + ruído). Atrás dela
//      o corpo escurece, abre rachaduras que pulsam com a nebulosa da Fenda (em espaço de tela, como
//      no Aren/Enemy/Corrupted) e a borda roxa aparece; NA frente, uma faixa acesa (violeta → ciano)
//      com veias que sobem um pouco à frente da mancha;
//   3. DESINTEGRAÇÃO — _Dissolve 0→1: o corpo some da cabeça para os pés em flocos (ruído), a borda
//      queima em brasa (laranja → violeta) antes de virar cinza (as partículas são do script).
// Também aceita os parâmetros que o EnemyBase usa nos Ecos (_Flash, _Telegraph, _Dissolve).
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
        _DissolveEdge ("Largura da borda do dissolve", Range(0.01,0.2)) = 0.07
        _DissolveHeight ("Altura local do dissolve", Float) = 2.0
        [HDR] _BurnColor ("Brasa da desintegração", Color) = (3.0,1.1,0.35,1)
        _Noise ("Ruído", 2D) = "gray" {}
        _Void ("Nebulosa da Fenda", 2D) = "black" {}
        [HDR] _VoidColor ("Tom da nebulosa", Color) = (1.1,0.55,1.3,1)
        _VoidScale ("Escala da nebulosa", Float) = 1.3
        _Seed ("Semente", Float) = 0
        _AlphaClip ("Recorte por alfa (cabelo)", Range(0,1)) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Faces", Float) = 2
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 200
        Cull [_Cull]
        CGPROGRAM
        #pragma surface surf Lambert addshadow fullforwardshadows
        #pragma target 3.0
        sampler2D _MainTex, _BumpMap, _Cracks, _Noise, _Void;
        float4 _Color, _CrackColor, _FrontColor, _RimColor, _VoidColor, _BurnColor;
        float _Corrupt, _CorruptHeight, _Darkness, _CrackTiling, _RimPower, _Flash, _Telegraph;
        float _Dissolve, _DissolveEdge, _DissolveHeight, _VoidScale, _Seed, _AlphaClip;
        struct Input { float2 uv_MainTex; float3 worldPos; float3 viewDir; float4 screenPos; float3 worldNormal; INTERNAL_DATA };
        void surf (Input IN, inout SurfaceOutput o)
        {
            float3 wp = IN.worldPos;
            // altura a partir dos pés no MUNDO (o FBX vem girado do Blender: o eixo local não é o "para cima")
            float3 origin = mul(unity_ObjectToWorld, float4(0, 0, 0, 1)).xyz;
            float3 lp = wp - origin;
            float nz = tex2D(_Noise, lp.xz * 1.7 + lp.y * 0.6 + _Seed).r;
            float nz2 = tex2D(_Noise, float2(lp.x + lp.z, lp.y) * 3.1 + _Seed * 1.7).g;

            // 3. desintegração: da cabeça para os pés, em flocos
            float h01 = saturate(lp.y / max(_DissolveHeight, 0.01));
            float dField = (1 - h01) * 0.62 + nz * 0.38;
            float dis = dField - _Dissolve * 1.08 + 0.04;
            clip(dis);
            float burn = 1 - smoothstep(0, _DissolveEdge, dis);

            fixed4 albedo = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            clip(albedo.a - 0.45 * _AlphaClip - 0.001 * (1 - _AlphaClip) + (1 - _AlphaClip));   // mechas do cabelo
            o.Normal = UnpackNormal(tex2D(_BumpMap, IN.uv_MainTex));

            // 2. corrupção: frente subindo dos pés
            float hC = saturate(lp.y / max(_CorruptHeight, 0.01));
            float front = _Corrupt * 1.25 - (hC + (nz - 0.5) * 0.22);   // > 0 = já corrompido
            float taken = smoothstep(0.0, 0.06, front) * step(0.001, _Corrupt);
            float band = exp(-pow(front / 0.045, 2)) * step(0.001, _Corrupt) * step(_Corrupt, 0.999);
            // veias: sobem um pouco à frente da mancha, finas
            float veinField = abs(frac(nz2 * 3.0 + lp.y * 1.3) - 0.5);
            float veins = smoothstep(0.06, 0.0, veinField) * saturate(1 - abs(front + 0.12) / 0.16) * step(0.001, _Corrupt);

            float3 n = abs(normalize(WorldNormalVector(IN, o.Normal)));
            float c1 = tex2D(_Cracks, wp.xy * _CrackTiling * 0.5).r;
            float c2 = tex2D(_Cracks, wp.zy * _CrackTiling * 0.5).r;
            float c3 = tex2D(_Cracks, wp.xz * _CrackTiling * 0.5).r;
            float crack = (c1 * n.z + c2 * n.x + c3 * n.y) / (n.x + n.y + n.z);
            float pulse = 0.55 + 0.45 * sin(_Time.y * 3.1 + wp.y * 4.0 + _Seed);

            o.Albedo = albedo.rgb * lerp(1, 1 - _Darkness, taken);
            float rim = pow(1 - saturate(dot(normalize(IN.viewDir), o.Normal)), _RimPower);
            float2 suv = IN.screenPos.xy / max(IN.screenPos.w, 1e-4);
            suv.x *= _ScreenParams.x / _ScreenParams.y;
            float3 vcol = tex2D(_Void, suv * _VoidScale + float2(_Time.x * 0.15, _Time.x * 0.05)).rgb * _VoidColor.rgb;

            float3 alive = rim * float3(0.05, 0.06, 0.11);                      // contraluz frio (noite)
            float3 corr = crack * _CrackColor.rgb * pulse * (0.6 + _Telegraph * 1.6)
                        + rim * _RimColor.rgb * (1 + _Telegraph * 2)
                        + vcol * (0.06 + crack * 0.9) * (1 + _Telegraph * 0.6);
            float3 em = lerp(alive, corr, taken)
                      + band * (_FrontColor.rgb * (0.8 + nz2) + vcol * 2.0)
                      + veins * _FrontColor.rgb * 0.9
                      + _Flash * float3(1.3, 1.15, 1.25)
                      + burn * lerp(_BurnColor.rgb, _CrackColor.rgb * 1.5 + vcol * 2, saturate(_Corrupt)) * 2.2;
            o.Emission = em;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
