// Cachoeiras do desfiladeiro de Campânula (como na referência do storyboard: véus claros caindo dos
// arcos da cidade para dentro da névoa). Cortina curva (malha do builder): uv.x atravessa, uv.y desce.
// Listras de água que escorrem e se partem (duas camadas de ruído em velocidades diferentes), espuma
// branca na boca e no pé, bordas que se desfazem, um pouco da luz da lua e das janelas (cor ambiente)
// e névoa do Unity. Transparente, sem escrever profundidade.
Shader "Campanula/Waterfall"
{
    Properties
    {
        _Noise ("Ruído", 2D) = "gray" {}
        _Color ("Água", Color) = (0.55,0.66,0.8,0.75)
        _Foam ("Espuma", Color) = (0.92,0.95,1,1)
        _Speed ("Velocidade", Float) = 1.6
        _Bright ("Brilho (noite)", Float) = 0.9
    }
    SubShader
    {
        Tags { "Queue"="Transparent+5" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off Cull Off
        Pass
        {
            Tags { "LightMode"="ForwardBase" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            sampler2D _Noise;
            float4 _Color, _Foam;
            float _Speed, _Bright;
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float len : TEXCOORD1; UNITY_FOG_COORDS(2) float3 wp : TEXCOORD3; };
            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.len = v.color.r * 30.0;   // comprimento da queda (m) / 30, gravado na cor do vértice
                o.wp = mul(unity_ObjectToWorld, v.vertex).xyz;
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }
            fixed4 frag (v2f i) : SV_Target
            {
                float t = _Time.y * _Speed;
                float L = max(i.len, 1.0);
                float2 p = float2(i.uv.x * 2.2, i.uv.y * L * 0.18);
                float s1 = tex2D(_Noise, float2(p.x * 1.0, p.y * 0.6 - t * 0.55)).r;
                float s2 = tex2D(_Noise, float2(p.x * 2.3 + 0.37, p.y * 1.1 - t * 0.9)).g;
                float s3 = tex2D(_Noise, float2(p.x * 5.1 + 0.11, p.y * 2.3 - t * 1.6)).b;
                float streak = saturate(s1 * 0.55 + s2 * 0.35 + s3 * 0.25 - 0.18);
                // bordas se desfazendo (e a cortina fica mais rala embaixo, onde vira névoa)
                float edge = smoothstep(0.0, 0.16, i.uv.x) * smoothstep(1.0, 0.84, i.uv.x);
                float breakup = smoothstep(0.25, 0.65, s2 + (1 - edge) * 0.6);
                float a = _Color.a * edge * (0.45 + 0.75 * streak) * (1 - 0.55 * i.uv.y);
                a *= 1 - (1 - edge) * breakup;
                // espuma: na boca (água batendo na pedra) e no pé (some na névoa)
                float foamTop = smoothstep(0.08, 0.0, i.uv.y) * 0.8;
                float foamBot = smoothstep(0.82, 1.0, i.uv.y);
                float foam = saturate(foamTop + foamBot + smoothstep(0.62, 0.9, streak) * 0.6);
                float3 amb = ShadeSH9(float4(0, 1, 0, 1));
                amb = dot(amb, float3(0.3, 0.45, 0.25)) * float3(0.85, 0.93, 1.08);   // luar (sem o tom violeta/laranja do céu)
                float3 col = lerp(_Color.rgb, _Foam.rgb, foam) * (amb * 3.2 + 0.22) * _Bright;
                a = saturate(a + foam * 0.35 * edge);
                a *= smoothstep(1.0, 0.9, i.uv.y);
                UNITY_APPLY_FOG(i.fogCoord, col);
                return float4(col, a);
            }
            ENDCG
        }
    }
}
