// A Fenda do Contracanto: rasgo negro no céu que apaga a luz (núcleo opaco), borda
// carmesim recortada por ruído e anéis de distorção concêntricos pulsando para fora.
// Dentro do rasgo: nebulosa e estrelas girando devagar (o céu do outro lado).
Shader "Campanula/Rift"
{
    Properties
    {
        _Noise ("Ruído", 2D) = "gray" {}
        [HDR] _Edge ("Borda", Color) = (2.2,0.25,0.55,1)
        [HDR] _Ring ("Anéis", Color) = (0.7,0.25,1.2,1)
        _Pulse ("Pulso", Range(0,1)) = 0
        _Space ("Céu do outro lado", 2D) = "black" {}
        _Stars ("Estrelas", 2D) = "black" {}
    }
    SubShader
    {
        Tags { "Queue"="Transparent-50" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off Cull Off Fog { Mode Off }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _Noise, _Space, _Stars; float4 _Edge, _Ring; float _Pulse;
            struct a2v { float4 v : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 p : SV_POSITION; float2 uv : TEXCOORD0; };
            v2f vert (a2v i) { v2f o; o.p = UnityObjectToClipPos(i.v); o.uv = i.uv * 2 - 1; return o; }
            fixed4 frag (v2f i) : SV_Target
            {
                float2 p = i.uv;
                float t = _Time.y;
                // fissura vertical que serpenteia, afinando nas pontas
                float wob = sin(p.y * 3.1 + t * 0.3) * 0.08 + (tex2D(_Noise, float2(p.y * 0.6, t * 0.02)).r - 0.5) * 0.25;
                float taper = saturate(1 - abs(p.y) / 0.92);
                float width = 0.17 * pow(taper, 0.55) * (1 + _Pulse * 0.25);
                float jag = (tex2D(_Noise, p * float2(3.5, 1.2) + float2(0, t * 0.05)).r - 0.5) * 0.14;
                float dx = abs(p.x - wob) - width - jag * taper;
                float core = smoothstep(0.012, -0.01, dx);
                float edge = exp(-max(dx, 0) * 22) * taper;
                // anéis elípticos que nascem do rasgo e se afastam
                float r = length(float2((p.x - wob) * 1.6, p.y * 0.55));
                float rings = pow(saturate(sin(r * 22 - t * 2.4) * 0.5 + 0.5), 8) * saturate(1 - r) * 0.75 * (1 - core);
                float3 col = _Edge.rgb * edge * (0.75 + 0.25 * sin(t * 3 + p.y * 9)) + _Ring.rgb * rings * (0.6 + _Pulse);
                float a = saturate(core + edge * 0.9 + rings * 0.8);
                float2 q = float2((p.x - wob) * 2.4, p.y * 1.1);
                float ca = cos(t * 0.04), sa = sin(t * 0.04);
                q = float2(q.x * ca - q.y * sa, q.x * sa + q.y * ca);
                float3 inner = tex2D(_Space, q * 0.55 + float2(t * 0.006, t * 0.002)).rgb * (1.1 + _Pulse * 0.8);
                float st = tex2D(_Stars, q * 1.2 + 0.37).r;
                inner += st * (0.9 + 0.5 * sin(t * 2.3 + q.x * 31 + q.y * 17)) * 1.6;
                inner *= smoothstep(0.0, 0.05, -dx) * 0.85 + 0.15;   // borda interna escura
                col = lerp(col, inner, core);
                return float4(col, a);
            }
            ENDCG
        }
    }
}
