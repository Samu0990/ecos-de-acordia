// Poça de luz quente no chão debaixo dos lampiões e tochas (à noite a luz deles chega no calçamento).
// Quad deitado, aditivo, queda radial suave com o ruído quebrando a borda e tremulação de chama.
// Barato: sem luz de verdade (luzes pontuais custariam uma passada extra em tudo que tocam).
Shader "Hidden/Aren/LightPool"
{
    Properties
    {
        _Color ("Luz", Color) = (1,0.55,0.25,0.5)
        _Noise ("Ruído", 2D) = "gray" {}
    }
    SubShader
    {
        Tags { "Queue"="Transparent-50" "RenderType"="Transparent" "IgnoreProjector"="True" "DisableBatching"="True" }
        Blend One One
        ZWrite Off Cull Off
        Offset -2, -2
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            sampler2D _Noise;
            UNITY_INSTANCING_BUFFER_START(P)
                UNITY_DEFINE_INSTANCED_PROP(float4, _Color)
            UNITY_INSTANCING_BUFFER_END(P)
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float3 wp : TEXCOORD1; UNITY_FOG_COORDS(2) UNITY_VERTEX_INPUT_INSTANCE_ID };
            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                o.wp = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.pos = UnityWorldToClipPos(o.wp);
                o.uv = v.uv - 0.5;
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }
            fixed4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float4 c = UNITY_ACCESS_INSTANCED_PROP(P, _Color);
                float d = length(i.uv) * 2;
                float n = tex2D(_Noise, i.wp.xz * 0.35).r;
                float fall = exp(-d * d * 3.2) * (1 - smoothstep(0.75, 1.0, d));
                float flick = 0.9 + 0.1 * sin(_Time.y * 7.1 + i.wp.x) * sin(_Time.y * 3.7 + i.wp.z);
                float3 col = c.rgb * c.a * fall * (0.75 + 0.5 * n) * flick;
                UNITY_APPLY_FOG_COLOR(i.fogCoord, col, fixed4(0, 0, 0, 0));
                return float4(col, 1);
            }
            ENDCG
        }
    }
}
