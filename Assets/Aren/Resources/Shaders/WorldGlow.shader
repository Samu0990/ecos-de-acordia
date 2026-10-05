// Brilho no mundo (aditivo, sempre de frente para a câmera): halos dos lampiões e tochas na
// noite de Campanula, a lanterna do sino da estrada e as cabeças dos sete brilhos que saem da
// Fenda. Espera o Quad embutido (a malha vira um cartaz no vertex shader). Sem névoa: os sete
// brilhos voam a ~240 m e a névoa noturna os apagaria. Instanciável (cor por instância).
Shader "Hidden/Aren/WorldGlow"
{
    Properties
    {
        _Color ("Cor", Color) = (1,0.7,0.35,1)
        _Core ("Núcleo (0 = só halo)", Range(0,1)) = 0.35
        _Soft ("Queda do halo", Range(1,12)) = 4
    }
    SubShader
    {
        Tags { "Queue"="Transparent+10" "RenderType"="Transparent" "IgnoreProjector"="True" "DisableBatching"="True" }
        Blend SrcAlpha One
        ZWrite Off Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            UNITY_INSTANCING_BUFFER_START(P)
                UNITY_DEFINE_INSTANCED_PROP(float4, _Color)
            UNITY_INSTANCING_BUFFER_END(P)
            float _Core, _Soft;
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                // cartaz: centro do objeto + deslocamento no plano da câmera (escala do objeto)
                float3 center = mul(unity_ObjectToWorld, float4(0, 0, 0, 1)).xyz;
                float sx = length(unity_ObjectToWorld._m00_m10_m20), sy = length(unity_ObjectToWorld._m01_m11_m21);
                float3 right = UNITY_MATRIX_V[0].xyz, up = UNITY_MATRIX_V[1].xyz;
                float3 wp = center + right * v.vertex.x * sx + up * v.vertex.y * sy;
                o.pos = mul(UNITY_MATRIX_VP, float4(wp, 1));
                o.uv = v.uv;
                return o;
            }
            fixed4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float4 c = UNITY_ACCESS_INSTANCED_PROP(P, _Color);
                float d = length(i.uv - 0.5) * 2;
                float halo = exp(-d * d * _Soft) * (1 - smoothstep(0.85, 1.0, d));
                float core = exp(-d * d * 60) * _Core;
                float a = saturate(halo + core) * c.a;
                return float4(c.rgb + core * 0.6, a);
            }
            ENDCG
        }
    }
}
