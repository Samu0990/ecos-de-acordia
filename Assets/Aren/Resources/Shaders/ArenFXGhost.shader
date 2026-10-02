// Afterimage / Eco Fantasma: silhueta de fresnel com faixas de frequência subindo e,
// por dentro, um campo de estrelas em espaço de tela (um eco feito de céu noturno).
Shader "Aren/FX/Ghost"
{
    Properties
    {
        [HDR] _Color ("Cor", Color) = (0.7,0.45,1,1)
        _Fade ("Fade", Range(0,1)) = 1
        _Inflate ("Inflar", Range(0,0.1)) = 0.0
        _Bands ("Faixas", Float) = 38
        _Space ("Estrelas", 2D) = "black" {}
        _SpaceAmt ("Quantidade de estrelas", Range(0,2)) = 1
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
            float4 _Color; float _Fade, _Inflate, _Bands, _SpaceAmt; sampler2D _Space;
            struct a2v { float4 v : POSITION; float3 n : NORMAL; };
            struct v2f { float4 p : SV_POSITION; float3 n : TEXCOORD0; float3 w : TEXCOORD1; float4 sp : TEXCOORD2; };
            v2f vert (a2v i)
            {
                v2f o;
                float4 v = i.v; v.xyz += i.n * _Inflate;
                o.p = UnityObjectToClipPos(v);
                o.n = UnityObjectToWorldNormal(i.n);
                o.w = mul(unity_ObjectToWorld, v).xyz;
                o.sp = ComputeScreenPos(o.p);
                return o;
            }
            fixed4 frag (v2f i) : SV_Target
            {
                float3 V = normalize(_WorldSpaceCameraPos - i.w);
                float fres = pow(1 - saturate(abs(dot(normalize(i.n), V))), 2.2);
                float bands = 0.55 + 0.45 * sin(i.w.y * _Bands - _Time.y * 14);
                float2 suv = i.sp.xy / max(i.sp.w, 1e-4);
                suv.x *= _ScreenParams.x / _ScreenParams.y;
                float3 st = tex2D(_Space, suv * 1.4 + float2(_Time.x * 0.25, _Time.x * 0.1)).rgb;
                float stars = dot(st, float3(0.33, 0.33, 0.33));
                float a = ((fres * 0.8 + 0.04) * bands + stars * _SpaceAmt * 0.9) * _Fade;
                float3 col = _Color.rgb * (0.35 + fres * 1.2) + st * _Color.rgb * _SpaceAmt * 1.8;
                return float4(col, saturate(a));
            }
            ENDCG
        }
    }
}
