// Corpo dos possuídos: albedo escurecido + rachaduras que pulsam (a Corrupção "respira"),
// borda (rim) roxa, flash branco ao apanhar e dissolução na morte. Lambert (barato).
// Dentro das rachaduras aparece a nebulosa da Fenda em espaço de tela: o corpo do Eco é
// uma janela para o outro lado do céu (a textura não acompanha o corpo, fica "atrás" dele).
Shader "Aren/Enemy/Corrupted"
{
    Properties
    {
        _MainTex ("Albedo", 2D) = "white" {}
        _Color ("Cor", Color) = (1,1,1,1)
        _Darkness ("Escurecer", Range(0,1)) = 0.85
        _Cracks ("Rachaduras", 2D) = "black" {}
        _CrackTiling ("Escala das rachaduras", Float) = 2.5
        [HDR] _CrackColor ("Cor das rachaduras", Color) = (2.2,0.25,0.6,1)
        [HDR] _RimColor ("Cor da borda", Color) = (0.6,0.15,0.9,1)
        _RimPower ("Borda", Range(0.5,8)) = 2.5
        _Flash ("Flash de dano", Range(0,1)) = 0
        _Telegraph ("Aviso de golpe", Range(0,1)) = 0
        _Dissolve ("Dissolver", Range(0,1)) = 0
        _Noise ("Ruído", 2D) = "gray" {}
        _Void ("Nebulosa da Fenda", 2D) = "black" {}
        [HDR] _VoidColor ("Tom da nebulosa", Color) = (1.1,0.55,1.3,1)
        _VoidScale ("Escala da nebulosa", Float) = 1.3
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 200
        CGPROGRAM
        #pragma surface surf Lambert addshadow fullforwardshadows
        #pragma target 3.0
        sampler2D _MainTex, _Cracks, _Noise, _Void;
        float4 _Color, _CrackColor, _RimColor, _VoidColor;
        float _Darkness, _CrackTiling, _RimPower, _Flash, _Telegraph, _Dissolve, _VoidScale;
        struct Input { float2 uv_MainTex; float3 worldPos; float3 viewDir; float3 worldNormal; float4 screenPos; };
        void surf (Input IN, inout SurfaceOutput o)
        {
            float3 wp = IN.worldPos;
            // triplanar barato (2 eixos) para as rachaduras: funciona em malha sem UV boa
            float3 n = abs(normalize(IN.worldNormal));
            float c1 = tex2D(_Cracks, wp.xy * _CrackTiling * 0.5).r;
            float c2 = tex2D(_Cracks, wp.zy * _CrackTiling * 0.5).r;
            float c3 = tex2D(_Cracks, wp.xz * _CrackTiling * 0.5).r;
            float crack = (c1 * n.z + c2 * n.x + c3 * n.y) / (n.x + n.y + n.z);
            float pulse = 0.55 + 0.45 * sin(_Time.y * 3.1 + wp.y * 4.0);
            float nz = tex2D(_Noise, wp.xz * 0.35 + float2(0, _Time.y * 0.05)).r;
            float dis = nz - _Dissolve * 1.15;
            clip(dis);
            fixed4 albedo = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            o.Albedo = albedo.rgb * (1 - _Darkness);
            float rim = pow(1 - saturate(dot(normalize(IN.viewDir), o.Normal)), _RimPower);
            float2 suv = IN.screenPos.xy / max(IN.screenPos.w, 1e-4);
            suv.x *= _ScreenParams.x / _ScreenParams.y;
            float3 vcol = tex2D(_Void, suv * _VoidScale + float2(_Time.x * 0.15, _Time.x * 0.05)).rgb * _VoidColor.rgb;
            float3 em = crack * _CrackColor.rgb * pulse * (0.6 + _Telegraph * 1.6)
                      + rim * _RimColor.rgb * (1 + _Telegraph * 2)
                      + _Flash * float3(1.3, 1.15, 1.25)
                      + vcol * (0.06 + crack * 0.9) * (1 + _Telegraph * 0.6)   // sutil: o corpo continua escuro, a nebulosa vive nas rachaduras
                      + (dis < 0.08 ? _CrackColor.rgb * 3 + vcol * 3 : 0);
            o.Emission = em;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
