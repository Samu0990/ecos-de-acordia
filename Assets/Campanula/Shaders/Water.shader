// Riacho: transparente, ondulação por ruído rolando, reflexo aproximado do céu (fresnel)
// e brilho do sol baixo. Barato (sem grab/reflexo real).
Shader "Campanula/Water"
{
    Properties
    {
        _Deep ("Fundo", Color) = (0.08,0.16,0.18,0.85)
        _Sky ("Reflexo do céu", Color) = (0.9,0.55,0.45,1)
        _Noise ("Ruído", 2D) = "gray" {}
        _Flow ("Fluxo (xy)", Vector) = (0,0.12,0,0)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Pass
        {
            Tags { "LightMode"="ForwardBase" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            float4 _Deep, _Sky, _Flow; sampler2D _Noise;
            float4 _LightColor0;
            struct v2f { float4 p : SV_POSITION; float3 w : TEXCOORD0; UNITY_FOG_COORDS(1) };
            v2f vert (appdata_base v) { v2f o; o.p = UnityObjectToClipPos(v.vertex); o.w = mul(unity_ObjectToWorld, v.vertex).xyz; UNITY_TRANSFER_FOG(o, o.p); return o; }
            fixed4 frag (v2f i) : SV_Target
            {
                float2 uv = i.w.xz * 0.18;
                float h1 = tex2D(_Noise, uv + _Flow.xy * _Time.y).r;
                float h2 = tex2D(_Noise, uv * 1.7 - _Flow.yx * _Time.y * 0.7).r;
                float3 n = normalize(float3((h1 - 0.5) * 0.6, 1, (h2 - 0.5) * 0.6));
                float3 V = normalize(_WorldSpaceCameraPos - i.w);
                float fres = pow(1 - saturate(dot(n, V)), 3);
                float3 L = normalize(_WorldSpaceLightPos0.xyz);
                float spec = pow(saturate(dot(reflect(-L, n), V)), 90) * 2.5;
                float3 col = lerp(_Deep.rgb, _Sky.rgb, fres * 0.85) + _LightColor0.rgb * spec;
                fixed4 c = fixed4(col, lerp(_Deep.a, 1, fres));
                UNITY_APPLY_FOG(i.fogCoord, c);
                return c;
            }
            ENDCG
        }
    }
}
