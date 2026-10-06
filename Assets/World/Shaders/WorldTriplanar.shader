// Pedra/terra/madeira para a arquitetura PROCEDURAL do mundo (marcos, arenas, masmorras, muralhas): as
// malhas geradas por código não têm UV boa, então a foto (Poly Haven CC0) é projetada nos três eixos do
// mundo, com normal map, variação de cor em grande escala, base mais escura (umidade/sujeira junto ao
// chão) e a Corrupção do reino (_ElyCorruption + Vórtice próximo) puxando a cor para a do reino tomado.
Shader "Elyndra/WorldTriplanar"
{
    Properties
    {
        _MainTex ("Foto (cor)", 2D) = "grey" {}
        [Normal] _BumpMap ("Foto (normal)", 2D) = "bump" {}
        _Color ("Tom", Color) = (1,1,1,1)
        _Scale ("Repetição (m)", Float) = 3
        _BumpScale ("Relevo", Range(0,2)) = 1
        _Glossiness ("Brilho", Range(0,1)) = 0.12
        _Grime ("Sujeira na base", Range(0,1)) = 0.45
        _GrimeHeight ("Altura da sujeira (m)", Float) = 2.5
        _Macro ("Ruído grande", 2D) = "grey" {}
        [HDR] _Emission ("Emissão", Color) = (0,0,0,0)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 300
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows vertex:vert
        #pragma target 3.0
        sampler2D _MainTex, _BumpMap, _Macro;
        fixed4 _Color; half4 _Emission;
        float _Scale, _BumpScale, _Glossiness, _Grime, _GrimeHeight;
        float _ElyCorruption; float4 _ElyCorruptionTint; float4 _ElyVortexPos; float _ElyVortexK;
        struct Input { float3 wp; float3 wn; float3 objBase; };
        void vert (inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            o.wp = mul(unity_ObjectToWorld, v.vertex).xyz;
            o.wn = UnityObjectToWorldNormal(v.normal);
            o.objBase = mul(unity_ObjectToWorld, float4(0,0,0,1)).xyz;
        }
        float3 Tri(sampler2D t, float3 p, float3 w) { return tex2D(t, p.zy).rgb * w.x + tex2D(t, p.xz).rgb * w.y + tex2D(t, p.xy).rgb * w.z; }
        void surf (Input i, inout SurfaceOutputStandard o)
        {
            float3 n = normalize(i.wn);
            float3 w = pow(abs(n), 4); w /= dot(w, 1);
            float3 p = i.wp / _Scale;
            float3 c = Tri(_MainTex, p, w);
            // normal: whiteout simplificado por eixo
            float3 nx = UnpackScaleNormal(tex2D(_BumpMap, p.zy), _BumpScale);
            float3 ny = UnpackScaleNormal(tex2D(_BumpMap, p.xz), _BumpScale);
            float3 nz = UnpackScaleNormal(tex2D(_BumpMap, p.xy), _BumpScale);
            float3 wnN = normalize(float3(0, nx.y, nx.x) * w.x + float3(ny.x, 0, ny.y) * w.y + float3(nz.x, nz.y, 0) * w.z + n);
            float m = tex2D(_Macro, i.wp.xz / 37.0).r;
            c *= _Color.rgb * (0.82 + 0.36 * m);
            float hb = saturate((i.wp.y - i.objBase.y) / max(_GrimeHeight, 0.01));
            c *= lerp(1 - _Grime * 0.55, 1, hb);
            // Corrupção do reino + Vórtice perto: a cor do lugar responde errado
            float vk = _ElyVortexK * saturate(1 - distance(i.wp, _ElyVortexPos.xyz) / max(_ElyVortexPos.w, 1));
            float ck = saturate(_ElyCorruption * 0.35 * (0.5 + m) + vk * 0.6);
            float lum = dot(c, float3(0.3, 0.59, 0.11));
            c = lerp(c, lum * _ElyCorruptionTint.rgb * 1.3, ck);
            o.Albedo = c;
            // a normal do mundo vai para o espaço tangente do surface shader (aproximação: só perturbação)
            float3 t = normalize(cross(n, abs(n.y) < 0.99 ? float3(0,1,0) : float3(1,0,0)));
            float3 b = cross(n, t);
            o.Normal = normalize(float3(dot(wnN, t), dot(wnN, b), dot(wnN, n)));
            o.Smoothness = _Glossiness;
            o.Metallic = 0;
            o.Emission = _Emission.rgb;
        }
        ENDCG
    }
    Fallback "Diffuse"
}
