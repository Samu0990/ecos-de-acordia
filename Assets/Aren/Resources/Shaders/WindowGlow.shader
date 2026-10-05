// Janelas acesas da vila à noite (VillageLights): quad colado na fachada, caixilho escuro em cruz
// (quatro vidros) e luz quente de dentro, mais forte embaixo (a lamparina na mesa), cada vidro
// com um tom um pouco diferente e uma tremulação lenta de chama. As apagadas refletem um pouco
// do céu. Emissão em HDR (o bloom da abertura pega). Com névoa do Unity (fica junto das paredes).
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
            #include "UnityCG.cginc"
            UNITY_INSTANCING_BUFFER_START(P)
                UNITY_DEFINE_INSTANCED_PROP(float4, _Color)
            UNITY_INSTANCING_BUFFER_END(P)
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float seed : TEXCOORD1; UNITY_FOG_COORDS(2) UNITY_VERTEX_INPUT_INSTANCE_ID };
            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                float3 c = mul(unity_ObjectToWorld, float4(0, 0, 0, 1)).xyz;
                o.seed = frac(sin(dot(c.xz, float2(12.9898, 78.233))) * 43758.5453);
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }
            fixed4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float4 c = UNITY_ACCESS_INSTANCED_PROP(P, _Color);
                float2 uv = i.uv;
                float frame = step(uv.x, 0.09) + step(0.91, uv.x) + step(uv.y, 0.07) + step(0.93, uv.y);
                float mull = step(abs(uv.x - 0.5), 0.035) + step(abs(uv.y - 0.55), 0.03);
                float wood = saturate(frame + mull);
                float pane = floor(uv.x * 2) + floor(uv.y * 2) * 2;
                float tone = 0.85 + 0.3 * frac(sin((pane + i.seed * 17) * 91.3) * 437.5);
                float lamp = lerp(1.25, 0.65, uv.y) * tone;
                float flick = 0.9 + 0.1 * sin(_Time.y * (2.1 + i.seed * 3) + i.seed * 40) * sin(_Time.y * 5.3 + i.seed * 13);
                float3 glass = c.rgb * c.a * lamp * flick;
                float3 col = lerp(glass, float3(0.035, 0.028, 0.025), wood);
                UNITY_APPLY_FOG(i.fogCoord, col);
                return float4(col, 1);
            }
            ENDCG
        }
    }
}
