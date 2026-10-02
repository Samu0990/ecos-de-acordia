// Chama das tochas: quad sempre virado para a câmera (billboard no vértice, em torno da
// origem do objeto), forma de gota com ruído subindo. Aditivo, sem luz: barato no Intel UHD.
// O objeto não pode entrar no static batching (a origem se perde): DisableBatching.
Shader "Campanula/Flame"
{
    Properties
    {
        _Noise ("Ruído", 2D) = "gray" {}
        [HDR] _Hot ("Miolo", Color) = (2.6,1.7,0.6,1)
        [HDR] _Cool ("Borda", Color) = (1.6,0.35,0.08,1)
        _Size ("Tamanho (m)", Float) = 0.45
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "DisableBatching"="True" }
        Blend SrcAlpha One
        ZWrite Off Cull Off Lighting Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            sampler2D _Noise; float4 _Hot, _Cool; float _Size;
            struct a2v { float4 v : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 p : SV_POSITION; float2 uv : TEXCOORD0; float seed : TEXCOORD1; };
            v2f vert (a2v i)
            {
                UNITY_SETUP_INSTANCE_ID(i);
                v2f o;
                float3 c = mul(unity_ObjectToWorld, float4(0, 0, 0, 1)).xyz;
                float3 right = normalize(UNITY_MATRIX_V[0].xyz);
                float3 w = c + right * i.v.x * _Size + float3(0, 1, 0) * (i.v.y + 0.5) * _Size * 1.6;
                o.p = mul(UNITY_MATRIX_VP, float4(w, 1));
                o.uv = i.uv;
                o.seed = frac(c.x * 0.37 + c.z * 0.71) * 10;
                return o;
            }
            fixed4 frag (v2f i) : SV_Target
            {
                float t = _Time.y + i.seed;
                float2 uv = i.uv;
                float n = tex2D(_Noise, float2(uv.x * 1.3 + i.seed, uv.y * 0.9 - t * 1.7)).r;
                float n2 = tex2D(_Noise, float2(uv.x * 2.7 - i.seed, uv.y * 1.6 - t * 2.9)).r;
                float sway = (n2 - 0.5) * 0.25 * uv.y;
                float w = lerp(0.42, 0.05, pow(uv.y, 0.8));
                float dx = abs(uv.x - 0.5 - sway) / w;
                float body = saturate(1 - dx) * saturate((1 - uv.y) * 1.4) * saturate(uv.y * 6);
                float f = saturate(body * 1.6 + (n - 0.5) * 0.9 * uv.y);
                float3 col = lerp(_Cool.rgb, _Hot.rgb, f * f);
                float flick = 0.85 + 0.15 * sin(t * 13) * sin(t * 7.3);
                return float4(col * flick, smoothstep(0.08, 0.55, f));
            }
            ENDCG
        }
    }
}
