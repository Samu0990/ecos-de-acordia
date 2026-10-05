// Partículas do mundo (ParticleSystem): brasas e faíscas aditivas em HDR (cor do vértice, círculo
// procedural). Sem textura e sem névoa — funcionam de perto e a quilômetros.
Shader "Hidden/Aren/WorldParticle"
{
    Properties { _Boost ("Intensidade", Float) = 2 }
    SubShader
    {
        Tags { "Queue"="Transparent+11" "RenderType"="Transparent" "IgnoreProjector"="True" }
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
            v2f vert (appdata v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.color = v.color; o.uv = v.uv - 0.5; return o; }
            fixed4 frag (v2f i) : SV_Target
            {
                float d = length(i.uv) * 2;
                float a = exp(-d * d * 5.0) * (1 - smoothstep(0.85, 1.0, d)) + exp(-d * d * 60.0);
                return float4(lerp(i.color.rgb, 1, exp(-d * d * 40.0) * 0.6) * _Boost, a * i.color.a);
            }
            ENDCG
        }
    }
}
