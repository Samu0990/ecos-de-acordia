// Partículas com mistura alfa (poeira, cinza da corrupção).
Shader "Aren/FX/AlphaBlend"
{
    Properties
    {
        _MainTex ("Textura", 2D) = "white" {}
        _Color ("Cor", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off Cull Off Lighting Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            sampler2D _MainTex; float4 _MainTex_ST; float4 _Color;
            struct a2v { float4 v : POSITION; float2 uv : TEXCOORD0; float4 c : COLOR; };
            struct v2f { float4 p : SV_POSITION; float2 uv : TEXCOORD0; float4 c : COLOR; UNITY_FOG_COORDS(1) };
            v2f vert (a2v i) { v2f o; o.p = UnityObjectToClipPos(i.v); o.uv = TRANSFORM_TEX(i.uv, _MainTex); o.c = i.c; UNITY_TRANSFER_FOG(o, o.p); return o; }
            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv) * _Color * i.c;
                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }
    }
}
