// Rastro do corte da lâmina (fita entre a base e a ponta, amostrada a cada quadro do golpe):
// a "meia-lua" violeta da prancha (ATTACK 1). Aditivo, HDR: borda da frente quase branca, some
// com a idade (uv.x: 0 novo -> 1 velho) e é mais forte perto da ponta (uv.y: 0 base -> 1 ponta),
// com energia correndo no ruído.
Shader "Hidden/Aren/SussSlash"
{
    Properties
    {
        [HDR] _Color ("Cor", Color) = (1.8,0.6,4.0,1)
        _Boost ("Intensidade", Float) = 1.3
        _Noise ("Ruído", 2D) = "gray" {}
    }
    SubShader
    {
        Tags { "Queue"="Transparent+10" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend One One
        ZWrite Off Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _Noise;
            float4 _Color;
            float _Boost;
            struct appdata { float4 vertex : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            v2f vert (appdata v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.color = v.color; o.uv = v.uv; return o; }
            fixed4 frag (v2f i) : SV_Target
            {
                float age = saturate(i.uv.x), across = saturate(i.uv.y);
                float fade = pow(1 - age, 2.4);
                float tipSide = smoothstep(0.25, 1.0, across);
                float leading = exp(-age * age * 90.0);
                float n = tex2D(_Noise, float2(age * 1.8 - _Time.y * 1.3, across * 1.2)).r;
                float edgeLine = exp(-pow((across - 0.97) / 0.05, 2)) * fade;
                float a = fade * tipSide * (0.35 + 0.95 * n) + leading * tipSide * 1.4 + edgeLine * 1.2;
                float3 col = lerp(_Color.rgb, float3(1.6, 1.4, 2.0), saturate(leading * 0.7 + edgeLine * 0.4)) * _Boost;
                return float4(col * a * i.color.a, 0);
            }
            ENDCG
        }
    }
}
