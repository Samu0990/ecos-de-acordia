// Os sete (e o núcleo do impacto): cartaz aditivo em HDR — núcleo branco quente, coroa na cor
// da nota, raios finos de difração girando devagar e o "tremor" de cada nota (o tamanho pulsa na
// frequência própria dela: são notas, não partículas). Cor e parâmetros por instância.
Shader "Hidden/Aren/NoteGlow"
{
    Properties
    {
        _Color ("Cor (a = intensidade)", Color) = (1,0.8,0.4,1)
        _Params ("x núcleo, y raios, z giro, w pulso", Vector) = (1,0.6,0,0)
    }
    SubShader
    {
        Tags { "Queue"="Transparent+12" "RenderType"="Transparent" "IgnoreProjector"="True" "DisableBatching"="True" }
        Blend One One
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
                UNITY_DEFINE_INSTANCED_PROP(float4, _Params)
            UNITY_INSTANCING_BUFFER_END(P)
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                float3 center = mul(unity_ObjectToWorld, float4(0, 0, 0, 1)).xyz;
                float sx = length(unity_ObjectToWorld._m00_m10_m20);
                float3 right = UNITY_MATRIX_V[0].xyz, up = UNITY_MATRIX_V[1].xyz;
                float3 wp = center + (right * v.vertex.x + up * v.vertex.y) * sx;
                o.pos = mul(UNITY_MATRIX_VP, float4(wp, 1));
                o.uv = v.uv - 0.5;
                return o;
            }
            fixed4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float4 c = UNITY_ACCESS_INSTANCED_PROP(P, _Color);
                float4 p = UNITY_ACCESS_INSTANCED_PROP(P, _Params);
                float2 uv = i.uv * 2;                 // -1..1
                float d = length(uv);
                float core = exp(-d * d * 140.0 / max(p.x, 0.05)) * 3.0;
                float hot = exp(-d * d * 30.0 / max(p.x, 0.05));
                float corona = exp(-d * 5.5) * 0.55 + exp(-d * 14.0) * 0.6;
                // raios de difração (4 + 2 mais fracos), girando devagar
                float a = p.z;
                float rays = 0;
                [unroll] for (int k = 0; k < 3; k++)
                {
                    float ang = a + k * 1.0472;
                    float2 dir = float2(cos(ang), sin(ang));
                    float across = abs(dot(uv, float2(-dir.y, dir.x)));
                    float along = abs(dot(uv, dir));
                    float w = k == 0 ? 1.0 : 0.55;
                    rays += exp(-across * across * 9000.0) * exp(-along * 3.2) * w;
                }
                rays *= p.y;
                float edge = 1 - smoothstep(0.8, 1.0, d);
                float3 col = c.rgb * (corona + rays * 0.9) + lerp(c.rgb, 1, 0.75) * (core + hot * 0.8);
                return float4(col * c.a * edge, 1);
            }
            ENDCG
        }
    }
}
