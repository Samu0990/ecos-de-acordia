// Tela inicial: reflexo que atravessa as letras douradas de ECOS DE ACORDIA de tempos em
// tempos. Faixa diagonal em degraus (pixel art) multiplicada pela máscara das letras.
Shader "Hidden/Aren/UITitleShine"
{
    Properties
    {
        [PerRendererData] _MainTex ("Máscara das letras", 2D) = "white" {}
        _ShineColor ("Cor", Color) = (1,0.86,0.6,1)
        _Pos ("Posição da faixa", Float) = -1
        _Width ("Largura", Float) = 0.09
        _Intensity ("Intensidade", Float) = 0.8
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" }
        Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend SrcAlpha One
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex; float4 _MainTex_TexelSize; fixed4 _ShineColor; float _Pos, _Width, _Intensity;
            struct appdata { float4 vertex : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; };
            v2f vert (appdata v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.uv = v.uv; o.color = v.color; return o; }
            fixed4 frag (v2f i) : SV_Target
            {
                // a faixa anda em passos de 2 px da arte
                float2 px = floor(i.uv * _MainTex_TexelSize.zw / 2) * 2;
                float2 q = px * _MainTex_TexelSize.xy;
                float d = q.x + (1 - q.y) * (_MainTex_TexelSize.w * _MainTex_TexelSize.x) * 0.6 - _Pos;   // diagonal ~30°
                float band = saturate(1 - abs(d) / _Width);
                band = floor(band * 4) / 4;
                float m = tex2D(_MainTex, i.uv).a;
                return fixed4(_ShineColor.rgb, m * band * _Intensity) * i.color;
            }
            ENDCG
        }
    }
}
