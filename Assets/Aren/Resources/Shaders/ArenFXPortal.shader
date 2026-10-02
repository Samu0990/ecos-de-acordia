// Portal no chão por onde os Ecos sobem: disco de nebulosa girando (céu do outro lado da
// Fenda), núcleo escuro e borda magenta. Quad deitado; UV centrado.
Shader "Aren/FX/Portal"
{
    Properties
    {
        _Space ("Nebulosa", 2D) = "black" {}
        [HDR] _Color ("Tom da nebulosa", Color) = (1.4,0.8,1.6,1)
        [HDR] _Rim ("Borda", Color) = (2.4,0.3,0.8,1)
        _Radius ("Raio (0..1)", Range(0,1)) = 0.8
        _Fade ("Fade", Range(0,1)) = 1
    }
    SubShader
    {
        Tags { "Queue"="Transparent+2" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off Cull Off Lighting Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _Space; float4 _Color, _Rim; float _Radius, _Fade;
            struct a2v { float4 v : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 p : SV_POSITION; float2 uv : TEXCOORD0; };
            v2f vert (a2v i) { v2f o; o.p = UnityObjectToClipPos(i.v); o.uv = i.uv * 2 - 1; return o; }
            fixed4 frag (v2f i) : SV_Target
            {
                float r = length(i.uv);
                float t = _Time.y;
                float ang = t * 0.9 + (1 - r) * 3.0;      // redemoinho: gira mais no centro
                float ca = cos(ang), sa = sin(ang);
                float2 q = float2(i.uv.x * ca - i.uv.y * sa, i.uv.x * sa + i.uv.y * ca);
                float3 neb = tex2D(_Space, q * 0.5 + 0.5).rgb * _Color.rgb;
                float rr = max(_Radius, 0.001);
                float disc = smoothstep(rr, rr - 0.22, r);
                float d = (r - rr) / 0.06;
                float rim = exp(-d * d) * (0.8 + 0.2 * sin(atan2(i.uv.y, i.uv.x) * 7 + t * 5));
                float3 col = neb * 1.5 * disc + _Rim.rgb * rim;
                col *= lerp(0.35, 1.0, smoothstep(0.0, rr, r));   // centro mais fundo
                float a = saturate(disc * (0.75 + dot(neb, 0.3)) + rim) * _Fade;
                return float4(col, a);
            }
            ENDCG
        }
    }
}
