// Rastro dos sete brilhos (TrailRenderer): aditivo, cor e transparência do gradiente do
// rastro, borda suave na largura. Sem névoa (voam muito longe).
Shader "Hidden/Aren/WorldTrail"
{
    Properties { _Boost ("Intensidade", Float) = 1.4 }
    SubShader
    {
        Tags { "Queue"="Transparent+9" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha One
        ZWrite Off Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float _Boost;
            struct appdata { float4 vertex : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            v2f vert (appdata v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.color = v.color; o.uv = v.uv; return o; }
            fixed4 frag (v2f i) : SV_Target
            {
                float across = 1 - abs(i.uv.y - 0.5) * 2;
                float a = pow(saturate(across), 1.5) * i.color.a;
                return float4(i.color.rgb * _Boost, a);
            }
            ENDCG
        }
    }
}
