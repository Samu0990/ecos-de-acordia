// Tela inicial: amostragem "pixel nítido" da arte da referência (1672x941) na resolução
// da tela. Na ampliação (1920x1080 = 1,15x) cada pixel da arte vira um bloco de borda
// firme com só 1 px de transição (sem o borrado do bilinear e sem os pixels de largura
// irregular do ponto); na redução volta ao bilinear normal. Serve para o fundo, as
// placas dos botões e os textos recortados (cor de vértice = tinta + alfa).
Shader "Hidden/Aren/UITitleArt"
{
    Properties
    {
        [PerRendererData] _MainTex ("Arte", 2D) = "white" {}
        _Color ("Tinta", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
        Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex; float4 _MainTex_TexelSize; fixed4 _Color; fixed4 _TextureSampleAdd;
            struct appdata { float4 vertex : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; };
            v2f vert (appdata v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.uv = v.uv; o.color = v.color * _Color; return o; }
            fixed4 frag (v2f i) : SV_Target
            {
                float2 tex = i.uv * _MainTex_TexelSize.zw;
                float2 w = max(fwidth(tex), 1e-4);                 // texels por pixel de tela
                float2 f = frac(tex) - 0.5;
                float2 sharp = (floor(tex) + 0.5 + clamp(f / w, -0.5, 0.5)) * _MainTex_TexelSize.xy;
                float2 uv = max(w.x, w.y) < 1.0 ? sharp : i.uv;
                fixed4 c = tex2Dgrad(_MainTex, uv, ddx(i.uv), ddy(i.uv)) + _TextureSampleAdd;
                return c * i.color;
            }
            ENDCG
        }
    }
}
