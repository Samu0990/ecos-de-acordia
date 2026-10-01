// Corte musical em arco. UV.x = ao longo do arco (0 cauda -> 1 cabeça), UV.y = interno -> externo.
// _Progress varre a cabeça; listras de frequência correm ao longo do arco.
Shader "Aren/FX/Slash"
{
    Properties
    {
        [HDR] _Color ("Cor da borda", Color) = (0.5,0.95,1,1)
        [HDR] _Core ("Cor do núcleo", Color) = (2,2,2,1)
        _Progress ("Progresso", Range(0,1.6)) = 1
        _Tail ("Comprimento da cauda", Range(0.05,1.5)) = 0.7
        _Freq ("Frequência das listras", Float) = 46
        _Fade ("Fade", Range(0,1)) = 1
        _Noise ("Ruído", 2D) = "gray" {}
        _Uniform ("Arco inteiro (lâmina)", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent+20" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha One
        ZWrite Off Cull Off Lighting Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float4 _Color, _Core; float _Progress, _Tail, _Freq, _Fade, _Uniform; sampler2D _Noise;
            struct a2v { float4 v : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 p : SV_POSITION; float2 uv : TEXCOORD0; };
            v2f vert (a2v i) { v2f o; o.p = UnityObjectToClipPos(i.v); o.uv = i.uv; return o; }
            fixed4 frag (v2f i) : SV_Target
            {
                float x = i.uv.x, y = i.uv.y;
                float head = _Progress, tail = head - _Tail;
                float along = saturate((x - tail) / _Tail) * saturate(1 - (x - head) * 25);
                along = along * along;
                along = lerp(along, 1 - pow(abs(x * 2 - 1), 3), _Uniform);
                // borda externa afiada e brilhante, interna some suave
                float across = smoothstep(0.0, 0.75, y) * (1 - smoothstep(0.93, 1.0, y));
                float edge = pow(saturate(y), 6) * 1.6;
                float n = tex2D(_Noise, float2(x * 3 - _Time.y * 2.5, y * 0.7)).r;
                float stripes = 0.7 + 0.3 * sin(x * _Freq - y * 9 - _Time.y * 38);
                float a = along * (across * 0.75 + edge) * stripes * (0.75 + n * 0.5) * _Fade;
                float3 col = lerp(_Color.rgb, _Core.rgb, saturate(edge * 0.8 + along * along * 0.35));
                return float4(col, saturate(a));
            }
            ENDCG
        }
    }
}
