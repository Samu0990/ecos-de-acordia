// Brilho aditivo barato (lanternas, brasas, olhos de cristal, janelas distantes, auras de arena): cor HDR
// com suavização pela normal (miolo forte, borda some) e pulso opcional. Some na neblina.
Shader "Elyndra/Glow"
{
    Properties
    {
        [HDR] _Color ("Cor", Color) = (1,0.7,0.3,1)
        _Pulse ("Pulso (Hz)", Float) = 0
        _Soft ("Borda suave", Range(0.5,6)) = 2
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend One One
        ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            half4 _Color; float _Pulse, _Soft;
            struct v2f { float4 pos : SV_POSITION; float3 n : TEXCOORD0; float3 wp : TEXCOORD1; UNITY_FOG_COORDS(2) };
            v2f vert (appdata_base v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.n = UnityObjectToWorldNormal(v.normal); o.wp = mul(unity_ObjectToWorld, v.vertex).xyz; UNITY_TRANSFER_FOG(o, o.pos); return o; }
            fixed4 frag (v2f i) : SV_Target
            {
                float3 V = normalize(_WorldSpaceCameraPos - i.wp);
                float k = pow(saturate(abs(dot(normalize(i.n), V))), _Soft);
                float p = _Pulse > 0 ? 0.7 + 0.3 * sin(_Time.y * _Pulse * 6.2832 + i.wp.x) : 1;
                fixed4 c = fixed4(_Color.rgb * k * p, 1);
                UNITY_APPLY_FOG_COLOR(i.fogCoord, c, fixed4(0,0,0,0));
                return c;
            }
            ENDCG
        }
    }
}
