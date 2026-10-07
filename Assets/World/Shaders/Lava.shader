// Lava dos rios e da boca dos vulcões da Coroa de Cinza: brasa que escorre (duas camadas de ruído em
// velocidades diferentes) sob uma crosta escura que racha; emissiva (brilha à noite e com bloom).
Shader "Elyndra/Lava"
{
    Properties
    {
        _Noise ("Ruído", 2D) = "gray" {}
        [HDR] _Hot ("Brasa", Color) = (3.2,0.95,0.2,1)
        _Crust ("Crosta", Color) = (0.07,0.045,0.04,1)
        _Scale ("Escala (1/m)", Float) = 0.06
        _Flow ("Velocidade", Float) = 0.025
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry+5" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            sampler2D _Noise; float4 _Hot, _Crust; float _Scale, _Flow;
            struct v2f { float4 pos : SV_POSITION; float3 wp : TEXCOORD0; UNITY_FOG_COORDS(1) };
            v2f vert (appdata_base v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.wp = mul(unity_ObjectToWorld, v.vertex).xyz; UNITY_TRANSFER_FOG(o, o.pos); return o; }
            fixed4 frag (v2f i) : SV_Target
            {
                float t = _Time.y * _Flow;
                float2 uv = i.wp.xz * _Scale;
                float n1 = tex2D(_Noise, uv + float2(t, t * 0.6)).r;
                float n2 = tex2D(_Noise, uv * 2.7 + float2(-t * 0.7, t * 1.3)).g;
                float n3 = tex2D(_Noise, uv * 7.1 + float2(t * 1.9, -t)).b;
                float crust = smoothstep(0.5, 0.66, n1 * 0.6 + n2 * 0.45 - 0.05);
                float3 hot = _Hot.rgb * (0.6 + 0.7 * n3) * (0.8 + 0.3 * sin(_Time.y * 1.3 + n1 * 6.0));
                float3 col = lerp(hot, _Crust.rgb * (0.8 + 0.4 * n3), crust);
                fixed4 c = fixed4(col, 1);
                UNITY_APPLY_FOG(i.fogCoord, c);
                return c;
            }
            ENDCG
        }
    }
}
