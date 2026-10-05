// A cúpula da onda de choque do impacto: casca fina e brilhante na borda (fresnel) que DISTORCE
// o que está atrás (GrabPass: o ar comprimido refrata a paisagem), com turbulência no ruído. A
// névoa da noite apaga o que estiver longe. Intensidade por _Intensity (animada pelo ImpactFX).
Shader "Hidden/Aren/ShockDome"
{
    Properties
    {
        _Noise ("Ruído", 2D) = "gray" {}
        _Color ("Brilho da borda", Color) = (1.2,0.85,0.5,1)
        _Intensity ("Intensidade", Range(0,2)) = 1
        _Distort ("Distorção", Float) = 0.02
    }
    SubShader
    {
        Tags { "Queue"="Transparent+3" "RenderType"="Transparent" "IgnoreProjector"="True" }
        GrabPass { "_ShockGrab" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off Cull Back
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            #include "NightCommon.cginc"
            sampler2D _ShockGrab, _Noise;
            float4 _Color;
            float _Intensity, _Distort;
            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float4 grab : TEXCOORD0; float3 n : TEXCOORD1; float3 wp : TEXCOORD2; float2 uv : TEXCOORD3; };
            v2f vert (appdata v)
            {
                v2f o;
                o.wp = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.pos = UnityWorldToClipPos(o.wp);
                o.grab = ComputeGrabScreenPos(o.pos);
                o.n = UnityObjectToWorldNormal(v.normal);
                o.uv = v.uv;
                return o;
            }
            fixed4 frag (v2f i) : SV_Target
            {
                float3 N = normalize(i.n);
                float3 V = normalize(_WorldSpaceCameraPos - i.wp);
                float ndv = abs(dot(N, V));
                float rim = pow(1 - ndv, 5.0);
                float n = tex2D(_Noise, i.uv * float2(6, 3) + float2(_Time.y * 0.05, 0)).r;
                float shell = rim * (0.6 + 0.8 * n);
                float3 nv = mul((float3x3)UNITY_MATRIX_V, N);
                float2 off = nv.xy * _Distort * shell * _Intensity;
                float3 behind = tex2Dproj(_ShockGrab, i.grab + float4(off * i.grab.w, 0, 0)).rgb;
                float dist = length(_WorldSpaceCameraPos - i.wp);
                float fogT = NightFog(_WorldSpaceCameraPos, i.wp, dist);
                float3 col = behind + _Color.rgb * pow(rim, 2.0) * _Intensity * 0.6 * (1 - fogT * 0.7) * (0.5 + n);
                float a = saturate(shell * 2.0) * saturate(_Intensity * 1.5);
                return float4(col, a);
            }
            ENDCG
        }
    }
}
