// Cristal / vidro fosco (torres de Miralume, cristais de Eco de Sombrafonte, recifes do Mar de Vidro,
// sal de Calíria): borda acesa por Fresnel, brilho interno que pulsa devagar e faceta pela normal da
// malha (malhas com vértices separados = facetas duras). Transparente leve, sem escrever profundidade.
Shader "Elyndra/Crystal"
{
    Properties
    {
        _Color ("Corpo", Color) = (0.4,0.7,0.9,0.55)
        [HDR] _Rim ("Borda (Fresnel)", Color) = (0.8,1.2,1.6,1)
        [HDR] _Core ("Brilho interno", Color) = (0.2,0.5,0.8,1)
        _Pulse ("Pulso", Float) = 0.7
        _Opacity ("Opacidade", Range(0,1)) = 0.75
    }
    SubShader
    {
        Tags { "Queue"="Transparent-10" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite On
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            fixed4 _Color; half4 _Rim, _Core; float _Pulse, _Opacity;
            struct v2f { float4 pos : SV_POSITION; float3 n : TEXCOORD0; float3 wp : TEXCOORD1; UNITY_FOG_COORDS(2) };
            v2f vert (appdata_base v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.n = UnityObjectToWorldNormal(v.normal); o.wp = mul(unity_ObjectToWorld, v.vertex).xyz; UNITY_TRANSFER_FOG(o, o.pos); return o; }
            fixed4 frag (v2f i) : SV_Target
            {
                float3 V = normalize(_WorldSpaceCameraPos - i.wp);
                float3 N = normalize(i.n);
                float f = pow(1 - saturate(abs(dot(N, V))), 3);
                float pulse = 0.75 + 0.25 * sin(_Time.y * _Pulse + i.wp.y * 0.3 + i.wp.x * 0.1);
                float3 L = normalize(_WorldSpaceLightPos0.xyz);
                float spec = pow(saturate(dot(reflect(-L, N), V)), 40);
                float3 c = _Color.rgb * (0.35 + 0.65 * saturate(dot(N, L) * 0.5 + 0.5)) + _Core.rgb * pulse * 0.5 + _Rim.rgb * f + spec;
                fixed4 col = fixed4(c, saturate(_Opacity * (0.55 + 0.45 * f)));
                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }
    }
}
