// Brilho em billboard (quad que sempre encara a câmera) para os olhos no fundo das órbitas e a
// fenda do peito. Aditivo em HDR, miolo quente + halo macio, cintila. _Intensity vem do script
// (MaterialPropertyBlock). ZTest normal: visto de lado, o crânio esconde o olho.
Shader "Hidden/Aren/SussGlow"
{
    Properties
    {
        [HDR] _Color ("Cor", Color) = (1.6,0.7,3.2,1)
        _Intensity ("Intensidade", Float) = 1
        _Falloff ("Queda do halo", Float) = 6
        _Flicker ("Cintilação", Range(0,1)) = 0.25
        _Seed ("Semente", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent+14" "RenderType"="Transparent" "IgnoreProjector"="True" "DisableBatching"="True" }
        Blend One One
        ZWrite Off Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float4 _Color;
            float _Intensity, _Falloff, _Flicker, _Seed;
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float fl : TEXCOORD1; };
            v2f vert (appdata v)
            {
                v2f o;
                float3 c = mul(unity_ObjectToWorld, float4(0, 0, 0, 1)).xyz;
                float s = length(float3(unity_ObjectToWorld._m00, unity_ObjectToWorld._m10, unity_ObjectToWorld._m20));
                float3 vc = UnityWorldToViewPos(c);
                vc.xy += v.vertex.xy * s;
                o.pos = mul(UNITY_MATRIX_P, float4(vc, 1));
                o.uv = v.uv;
                float t = _Time.y * 7.3 + _Seed * 13.1;
                o.fl = 1 - _Flicker * (0.5 + 0.5 * sin(t) * sin(t * 1.71 + 1.3));
                return o;
            }
            fixed4 frag (v2f i) : SV_Target
            {
                float d = length(i.uv - 0.5) * 2;
                float halo = exp(-d * d * _Falloff) * (1 - smoothstep(0.8, 1.0, d));
                float core = exp(-d * d * 55.0);
                float3 col = (_Color.rgb * halo + lerp(_Color.rgb, 1, 0.6) * core * 1.5) * _Intensity * i.fl;
                return float4(col, 0);
            }
            ENDCG
        }
    }
}
