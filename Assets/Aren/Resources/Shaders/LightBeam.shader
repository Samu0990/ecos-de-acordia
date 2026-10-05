// Pilar de luz que sobe do lugar do impacto (fica, fraco, depois da abertura: o mistério no
// horizonte). Cartaz que gira só em torno do eixo vertical para encarar a câmera; aditivo em HDR,
// núcleo fino + brilho, some para cima, tremula devagar; a névoa da noite o atenua.
Shader "Hidden/Aren/LightBeam"
{
    Properties
    {
        _Noise ("Ruído", 2D) = "gray" {}
        _Color ("Cor", Color) = (1.4,1.0,0.55,1)
        _Intensity ("Intensidade", Float) = 1
    }
    SubShader
    {
        Tags { "Queue"="Transparent+8" "RenderType"="Transparent" "IgnoreProjector"="True" "DisableBatching"="True" }
        Blend One One
        ZWrite Off Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            #include "NightCommon.cginc"
            sampler2D _Noise;
            float4 _Color;
            float _Intensity;
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float fog : TEXCOORD1; };
            v2f vert (appdata v)
            {
                v2f o;
                float3 base = mul(unity_ObjectToWorld, float4(0, 0, 0, 1)).xyz;
                float sx = length(unity_ObjectToWorld._m00_m10_m20), sy = length(unity_ObjectToWorld._m01_m11_m21);
                float3 toCam = _WorldSpaceCameraPos - base; toCam.y = 0;
                float3 right = normalize(cross(float3(0, 1, 0), normalize(toCam + 1e-3)));
                float3 wp = base + right * v.vertex.x * sx + float3(0, 1, 0) * (v.vertex.y + 0.5) * sy;
                o.pos = UnityWorldToClipPos(wp);
                o.uv = v.uv;
                float d = length(_WorldSpaceCameraPos - base);
                o.fog = NightFog(_WorldSpaceCameraPos, base + float3(0, 200, 0), d);
                return o;
            }
            fixed4 frag (v2f i) : SV_Target
            {
                float across = abs(i.uv.x - 0.5) * 2;
                float core = exp(-across * across * 400.0);
                float glow = exp(-across * 5.0) * 0.35;
                float n = tex2D(_Noise, float2(i.uv.x * 2.0, i.uv.y * 1.5 - _Time.y * 0.08)).r;
                float vert = exp(-i.uv.y * 2.6) * smoothstep(0.0, 0.02, i.uv.y);
                float3 col = _Color.rgb * (core * 2.0 + glow * (0.6 + 0.8 * n)) * vert;
                return float4(col * _Intensity * (1 - i.fog * 0.75), 1);
            }
            ENDCG
        }
    }
}
