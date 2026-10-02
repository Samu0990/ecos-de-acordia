// Afterimage / Eco Fantasma: silhueta de fresnel com faixas de frequência subindo.
Shader "Aren/FX/Ghost"
{
    Properties
    {
        [HDR] _Color ("Cor", Color) = (0.7,0.45,1,1)
        _Fade ("Fade", Range(0,1)) = 1
        _Inflate ("Inflar", Range(0,0.1)) = 0.0
        _Bands ("Faixas", Float) = 38
    }
    SubShader
    {
        Tags { "Queue"="Transparent+5" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha One
        ZWrite Off Cull Back Lighting Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float4 _Color; float _Fade, _Inflate, _Bands;
            struct a2v { float4 v : POSITION; float3 n : NORMAL; };
            struct v2f { float4 p : SV_POSITION; float3 n : TEXCOORD0; float3 w : TEXCOORD1; };
            v2f vert (a2v i)
            {
                v2f o;
                float4 v = i.v; v.xyz += i.n * _Inflate;
                o.p = UnityObjectToClipPos(v);
                o.n = UnityObjectToWorldNormal(i.n);
                o.w = mul(unity_ObjectToWorld, v).xyz;
                return o;
            }
            fixed4 frag (v2f i) : SV_Target
            {
                float3 V = normalize(_WorldSpaceCameraPos - i.w);
                float fres = pow(1 - saturate(abs(dot(normalize(i.n), V))), 2.2);
                float bands = 0.55 + 0.45 * sin(i.w.y * _Bands - _Time.y * 14);
                float a = (fres * 0.8 + 0.04) * bands * _Fade;
                return float4(_Color.rgb * (0.35 + fres * 1.2), saturate(a));
            }
            ENDCG
        }
    }
}
