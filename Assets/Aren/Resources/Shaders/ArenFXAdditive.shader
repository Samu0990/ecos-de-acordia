// Partículas e quads aditivos do Aren (Built-in RP). Cor HDR * textura * cor do vértice.
Shader "Aren/FX/Additive"
{
    Properties
    {
        _MainTex ("Textura", 2D) = "white" {}
        [HDR] _Color ("Cor", Color) = (1,1,1,1)
        _Fade ("Fade", Range(0,1)) = 1
    }
    SubShader
    {
        Tags { "Queue"="Transparent+10" "RenderType"="Transparent" "IgnoreProjector"="True" "PreviewType"="Plane" }
        Blend SrcAlpha One
        ZWrite Off Cull Off Lighting Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            sampler2D _MainTex; float4 _MainTex_ST;
            UNITY_INSTANCING_BUFFER_START(P)
                UNITY_DEFINE_INSTANCED_PROP(float4, _Color)
                UNITY_DEFINE_INSTANCED_PROP(float, _Fade)
            UNITY_INSTANCING_BUFFER_END(P)
            struct a2v { float4 v : POSITION; float2 uv : TEXCOORD0; float4 c : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 p : SV_POSITION; float2 uv : TEXCOORD0; float4 c : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            v2f vert (a2v i) { v2f o; UNITY_SETUP_INSTANCE_ID(i); UNITY_TRANSFER_INSTANCE_ID(i, o);
                o.p = UnityObjectToClipPos(i.v); o.uv = TRANSFORM_TEX(i.uv, _MainTex); o.c = i.c; return o; }
            fixed4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float4 col = tex2D(_MainTex, i.uv) * UNITY_ACCESS_INSTANCED_PROP(P, _Color) * i.c;
                col.a *= UNITY_ACCESS_INSTANCED_PROP(P, _Fade);
                return col;
            }
            ENDCG
        }
    }
}
